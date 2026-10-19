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

    public bool OrderGift(int c, int o) => c != o && TryGift(c, o);
    public bool OrderBarter(int c, int o) => c != o && TryBarter(c, o);

    /// <summary>A raid on a chosen settlement, from the nearest settlement that can spare the party. Hard rules: WAR-02, contact, no alliance or truce.</summary>
    public bool OrderRaid(int c, int targetSettlement, int partySize)
    {
        if ((uint)targetSettlement >= (uint)_sCount || _free[targetSettlement]) return false;
        int o = _sCiv[targetSettlement];
        if (o == c || !_research.IsDone(c, _p.War02) || !_dip.Contacted(c, o) || _dip.Allied(c, o)) return false;
        if (_dip.InTruce(c, o, _season)) return false;
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
