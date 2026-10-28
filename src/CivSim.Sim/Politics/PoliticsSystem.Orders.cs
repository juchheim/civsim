using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>What a settlement can field and what stands in a raider's way (docs/MVP/13 §6 to §7).</summary>
public readonly record struct SettlementMilitary(int Settlement, int Adults, int Fighters, int Warriors, Fix64 DefendingStrength,
    Fix64 AttackingStrength, Defenses Defenses, Fix64 FoodStores);

public sealed partial class PoliticsSystem
{
    // ---- external minds: the AI Strategist takes over a civilization's decisions -----------------

    /// <summary>
    /// Hands a civilization's gifts, barter, raids, wars and peace to an outside decision maker (the Strategist, docs/MVP/14
    /// §2). The v0 rules then stay out of its way; it acts only through the <c>Order*</c> methods below, which keep every
    /// hard rule (projects, contact, travel, food to spare) while leaving the choice to the caller.
    /// </summary>
    public void SetExternalMind(int civ, bool external) => _externalMind[civ] = external;
    public bool IsExternalMind(int civ) => _externalMind[civ];

    public int SettlementTotal => _sCount;
    public int SettlementCivOf(int settlement) => _sCiv[settlement];
    public int SettlementTileOf(int settlement) => _sTile[settlement];
    public int SettlementPopulationOf(int settlement) => _sPop[settlement];

    /// <summary>The fighters, strength and defenses of a settlement, as they are now (true state; the belief layer decides who may read it).</summary>
    public SettlementMilitary MilitaryOf(int settlement)
    {
        List<Fighter> defending = Fighters(settlement, attacking: false, out _);
        Fix64 defend = Fix64.Zero, attack = Fix64.Zero;
        foreach (Fighter f in defending) defend += f.Strength;
        foreach (Fighter f in Fighters(settlement, attacking: true, out _)) attack += f.Strength;
        int civ = _sCiv[settlement];
        int adults = AdultCount(settlement);
        int warriors = _research.IsDone(civ, _p.War04) ? adults / 5 : 0;
        return new SettlementMilitary(settlement, adults, defending.Count, warriors, defend, attack, DefensesOf(settlement),
            _economy.FoodStores(settlement));
    }

    /// <summary>Weapon strength multiplier of a civilization's best weapon, and whether it is ranged (13 §7.1).</summary>
    public Fix64 WeaponOf(int civ, out bool ranged) => Weapon(civ, out ranged);

    // ---- orders ---------------------------------------------------------------------------

    public bool OrderGift(int c, int o, int targetSettlement = -1) => c != o && TryGift(c, o, targetSettlement);
    public bool OrderBarter(int c, int o, int targetSettlement = -1) => c != o && TryBarter(c, o, targetSettlement);

    private void Refused(Command command, int target, OrderRefusalReason reason)
        => _events.Add(new PoliticsEvent(_tick, command.Civ, PoliticsEventKind.OrderRefused, target,
            (int)command.Kind, (int)reason));

    /// <summary>Decode optional destination settlement (B = index + 1; zero keeps the automatic closest-pair choice).</summary>
    private bool TryDecodeOrderDestination(int encoded, int targetCiv, out int settlement)
    {
        settlement = -1;
        if (encoded == 0) return true;
        if (encoded < 0 || encoded > _sCount) return false;
        settlement = encoded - 1;
        return _sCiv[settlement] == targetCiv && !_free[settlement] && _sPop[settlement] > 0;
    }

    /// <summary>Cancel the zero-based own-party ordinal. Its surviving members return to the origin settlement.</summary>
    public bool OrderRecall(int civ, int ownPartyIndex)
    {
        if (ownPartyIndex < 0) return false;
        int ordinal = 0;
        for (int i = 0; i < _expeditions.Count; i++)
        {
            if (_expeditions[i].Civ != civ) continue;
            if (ordinal++ != ownPartyIndex) continue;
            _expeditions.RemoveAt(i);
            return true;
        }
        return false;
    }

    public bool OrderRecallCommand(int civ, int expeditionIndex, Command command)
    {
        bool recalled = OrderRecall(civ, expeditionIndex);
        if (!recalled) Refused(command, expeditionIndex, OrderRefusalReason.InvalidState);
        return recalled;
    }

    /// <summary>A raid on a chosen settlement, from the nearest settlement that can spare the party. Hard rules: WAR-02, contact, no alliance or truce.</summary>
    public bool OrderRaid(int c, int targetSettlement, int partySize)
    {
        if ((uint)targetSettlement >= (uint)_sCount || _free[targetSettlement]) return false;
        int o = _sCiv[targetSettlement];
        if (o == c || !_research.IsDone(c, _p.War02) || !_dip.Contacted(c, o) || _dip.Allied(c, o)) return false;
        if (_dip.InTruce(c, o, _season)) return false;
        if (_pacts.Count > 0 && HasPact(c, o)) return false;
        int origin = BestOrigin(c, _sTile[targetSettlement]);
        return origin >= 0 && SendRaid(c, o, origin, targetSettlement, Math.Max(4, partySize), forecast: false);
    }

    /// <summary>The field army against a chosen settlement. Hard rules: at war with its owner, contact.</summary>
    public bool OrderAssault(int c, int targetSettlement)
    {
        if ((uint)targetSettlement >= (uint)_sCount || _free[targetSettlement]) return false;
        int o = _sCiv[targetSettlement];
        return o != c && _dip.AtWar(c, o) && SendAssault(c, o, targetSettlement, requireOdds: false);
    }

    public bool OrderWar(int c, int o)
    {
        if (c == o || !_dip.Contacted(c, o) || _dip.AtWar(c, o) || _dip.Allied(c, o)) return false;
        // Breaking a truce is a reputation event (13 §8.1).
        if (_dip.InTruce(c, o, _season)) _dip.AdjustReputation(c, -40);
        // War breaks a non-aggression pact: Relations −60, reputation −30 (DIP-04).
        if (_pacts.Count > 0) BreakPacts(c, o);
        SetWar(c, o, true);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.WarDeclared, o, 0, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.WarDeclared, c, 1, 0));
        return true;
    }

    public bool OrderPeace(int c, int o)
    {
        if (c == o || !_dip.AtWar(c, o)) return false;
        foreach (Occupation occ in _occupations)
            if (occ.Civ == c && _sCiv[occ.Settlement] == o || occ.Civ == o && _sCiv[occ.Settlement] == c) return false;
        _dip.MakePeace(c, o, _season);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.PeaceMade, o, _season - _warSince[c * _civs + o], 0));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.PeaceMade, c, _season - _warSince[c * _civs + o], 0));
        return true;
    }

    /// <summary>An alliance (DIP-03 on both sides, Relations above 40, no war).</summary>
    public bool OrderAlliance(int c, int o)
    {
        if (c == o || !_dip.Contacted(c, o) || _dip.AtWar(c, o) || _dip.Allied(c, o) || _dip.Relations(c, o) <= Diplomacy.AllianceAbove) return false;
        if (!_research.IsDone(c, _p.Dip03) || !_research.IsDone(o, _p.Dip03)) return false;
        _dip.Ally(c, o);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.AllianceFormed, o, 0, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.AllianceFormed, c, 0, 0));
        return true;
    }

    /// <summary>AI-only build priority, validated against the caller's own settlement.</summary>
    public bool OrderPrioritizePhysicalDefenseBuild(int c, int settlement)
    {
        if ((uint)c >= (uint)_civs || (uint)settlement >= (uint)_sCiv.Length || _sCiv[settlement] != c || IsFreeFolk(settlement))
            return false;
        return _economy.PrioritizePhysicalDefenseBuild(settlement);
    }

    /// <summary>Read-only explanation for the optional B28 diagnostic trace.</summary>
    public string DescribePrioritizePhysicalDefenseBuild(int c, int settlement)
    {
        if ((uint)c >= (uint)_civs || (uint)settlement >= (uint)_sCiv.Length || _sCiv[settlement] != c || IsFreeFolk(settlement))
            return "order refusal=caller-does-not-own-settlement";
        return _economy.DescribePrioritizePhysicalDefenseBuild(settlement);
    }

    /// <summary>Posture (needs GOV-02, the same rule as the player's lever).</summary>
    public bool OrderPosture(int c, Posture posture)
    {
        if (!_research.IsDone(c, _p.Gov02)) return false;
        _posture[c] = (byte)posture;
        ApplyEconomyInputs();
        return true;
    }

    public bool OrderBeacon(int c, int tile)
    {
        if ((uint)tile >= (uint)_territory.TileCount || _beacons[c].Count >= BeaconAllowance(c)) return false;
        _beacons[c].Add(tile);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.BeaconPlaced, -1, tile % _width, tile / _width));
        return true;
    }

    public void OrderRemoveBeacon(int c, int index)
    {
        if ((uint)index < (uint)_beacons[c].Count) _beacons[c].RemoveAt(index);
    }
}
