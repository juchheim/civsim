using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A besieging host and the state it carries between seasonal politics updates.</summary>
public sealed class Investment
{
    public int Civ { get; internal set; }
    public int TargetCiv { get; internal set; }
    public int Settlement { get; internal set; }
    public int HomeSettlement { get; internal set; }
    public int BeganSeason { get; internal set; }
    public SiegeState State { get; internal set; }
    public List<long> Members { get; } = new();
    public List<Fix64> Strengths { get; } = new();
}

public sealed partial class PoliticsSystem
{
    private static readonly ulong SiegeStream = Hash64.OfString("politics.siege");
    private readonly List<Investment> _investments = new();

    /// <summary>Active walled-settlement investments, in ascending settlement order.</summary>
    public IReadOnlyList<Investment> Investments => _investments;

    public Investment? InvestmentAt(int settlement)
    {
        foreach (Investment investment in _investments)
            if (investment.Settlement == settlement) return investment;
        return null;
    }

    private bool UnderInvestment(int settlement) => InvestmentAt(settlement) is not null;

    private bool HasSupplyLine(int civ, int target)
    {
        int[] distances = WalkFrom(_sTile[target]);
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && distances[_sTile[s]] <= Sieges.SupplyReachTiles) return true;
        return false;
    }

    private Fix64 UnwalledDefenseStrength(int settlement)
    {
        Fix64 strength = Fix64.Zero;
        foreach (Fighter fighter in Fighters(settlement, attacking: false, out _, _sTile[settlement])) strength += fighter.Strength;
        // Keep the raw field strength here; callers add the ×1.1 home-ground bonus for investment odds and sally rules.
        return strength;
    }

    private void BeginInvestment(Expedition party)
    {
        int settlement = party.ToSettlement;
        if (!TargetStillThere(party) || UnderInvestment(settlement)) return;
        List<Fighter> fighters = AliveFighters(party, out List<long> ids);
        if (fighters.Count < Captures.MinHolders) return;
        Fix64 force = Fix64.Zero;
        foreach (Fighter fighter in fighters) force += fighter.Strength;
        Fix64 unwalledDefense = UnwalledDefenseStrength(settlement);
        if (!Sieges.CanInvest(force, unwalledDefense * Fix64.FromRatio(11, 10)))
        {
            _failedCampaigns[party.Civ * _civs + party.TargetCiv]++;
            _events.Add(new PoliticsEvent(_tick, (byte)party.Civ, PoliticsEventKind.AssaultResolved,
                party.TargetCiv, 0, settlement));
            _events.Add(new PoliticsEvent(_tick, (byte)party.TargetCiv, PoliticsEventKind.AssaultResolved,
                party.Civ, 0, settlement));
            return;
        }

        Fix64 need = _economy.FoodNeedForSeason(settlement, _season % 4 == 3);
        Fix64 stores = _economy.FoodStores(settlement);
        Fix64 storesSeasons = need <= Fix64.Zero ? Fix64.Zero : stores / need;
        int carry = _research.IsDone(party.Civ, _p.Fod07) ? 2 : 1; // 23 §21.1 C4: FOD-07 alone adds a season.
        var state = new SiegeState(force, unwalledDefense, unwalledDefense * Sieges.WallDefenseMultiplier,
            storesSeasons, carry, _season);
        var investment = new Investment
        {
            Civ = party.Civ, TargetCiv = party.TargetCiv, Settlement = settlement,
            HomeSettlement = party.FromSettlement, BeganSeason = _season, State = state,
        };
        for (int i = 0; i < ids.Count; i++)
        {
            investment.Members.Add(ids[i]);
            investment.Strengths.Add(party.Strengths[PartyMemberIndex(party, ids[i])]);
        }
        int at = 0;
        while (at < _investments.Count && _investments[at].Settlement < settlement) at++;
        _investments.Insert(at, investment);
        _economy.SetUnderInvestment(settlement, true);
        CutInvestmentRoutes(settlement);
        _economy.SetIdle(ids, int.MaxValue);
        _events.Add(new PoliticsEvent(_tick, (byte)party.Civ, PoliticsEventKind.InvestmentBegan,
            party.TargetCiv, settlement, _season));
        _events.Add(new PoliticsEvent(_tick, (byte)party.TargetCiv, PoliticsEventKind.InvestmentBegan,
            party.Civ, settlement, _season));
        _events.Add(new PoliticsEvent(_tick, (byte)party.Civ, PoliticsEventKind.InvestmentTelemetryStarted,
            party.TargetCiv, settlement, ids.Count));
    }

    private static int PartyMemberIndex(Expedition party, long id)
    {
        for (int i = 0; i < party.Members.Count; i++) if (party.Members[i] == id) return i;
        return -1;
    }

    private Fix64 CurrentInvestmentStrength(Investment investment, List<Fighter> into)
    {
        into.Clear();
        Fix64 strength = Fix64.Zero;
        for (int i = 0; i < investment.Members.Count; i++)
        {
            long id = investment.Members[i];
            if (!_economy.TryFigure(id, out _) || _economy.IsWounded(id)) continue;
            var fighter = new Fighter(id, investment.Strengths[i]);
            into.Add(fighter);
            strength += fighter.Strength;
        }
        return strength;
    }

    private void AdvanceInvestments()
    {
        for (int i = _investments.Count - 1; i >= 0; i--)
        {
            Investment investment = _investments[i];
            if (investment.BeganSeason >= _season) continue;
            if (investment.Settlement >= _sCount || _free[investment.Settlement] ||
                _sCiv[investment.Settlement] != investment.TargetCiv ||
                !_research.IsDone(investment.TargetCiv, _p.Def05) ||
                LivingEconomyModel.StructureIndex("stone_walls") < 0 ||
                _economy.StructureCount(investment.Settlement, LivingEconomyModel.StructureIndex("stone_walls")) == 0)
            {
                EndInvestment(i, relieved: true);
                continue;
            }

            var besiegers = new List<Fighter>();
            Fix64 force = CurrentInvestmentStrength(investment, besiegers);
            Fix64 unwalledDefense = UnwalledDefenseStrength(investment.Settlement);
            investment.State = investment.State with { BesiegerStrength = force, UnwalledDefenderStrength = unwalledDefense,
                DefenderStrength = unwalledDefense * Sieges.WallDefenseMultiplier };
            bool supplied = HasSupplyLine(investment.Civ, investment.Settlement);
            if (!supplied && investment.State.BesiegerFoodSeasons <= 0 && besiegers.Count > 0)
                ApplyDesertion(investment, besiegers, force);

            Fix64 need = _economy.FoodNeedForSeason(investment.Settlement, _season % 4 == 3);
            Fix64 stores = _economy.FoodStores(investment.Settlement);
            // The economy has already consumed this season's 75% ration before OnSeasonEnd. Restore that amount in the
            // helper's starting value so its per-season subtraction leaves the actual post-ration store level.
            investment.State = investment.State with { StoresSeasons = need <= Fix64.Zero ? Fix64.Zero : stores / need + Sieges.RationNeed };
            int carriedFood = investment.State.BesiegerFoodSeasons;
            Fix64 strengthBefore = force;
            SiegeState next = investment.State;
            SiegeResult result = Sieges.Advance(ref next, supplied, _seed, SiegeStream, (ulong)investment.Settlement);
            investment.State = next;
            if (!supplied && carriedFood <= 0 && strengthBefore > investment.State.BesiegerStrength)
            {
                ApplyDesertion(investment, besiegers, investment.State.BesiegerStrength);
                force = CurrentInvestmentStrength(investment, besiegers);
                investment.State = investment.State with { BesiegerStrength = force };
                Fix64 homeDefense = unwalledDefense * Fix64.FromRatio(11, 10);
                if (force < homeDefense * Sieges.MinimumInvestmentRatio) result = SiegeResult.Lifted;
                else if (homeDefense > Fix64.FromRatio(8, 10) * force) result = SiegeResult.Sally;
            }
            _economy.BurnStructure(investment.Settlement);
            _economy.AdjustCohesion(investment.Settlement, Fix64.FromRatio(-5, 100));
            if ((uint)investment.HomeSettlement < (uint)_sCount)
                _economy.AdjustCohesion(investment.HomeSettlement, Fix64.FromRatio(-2, 100));

            if (result == SiegeResult.Sally)
            {
                if (ResolveSally(investment, besiegers)) EndInvestment(i, relieved: true);
                else if (force < unwalledDefense * Fix64.FromRatio(11, 10) * Sieges.MinimumInvestmentRatio) EndInvestment(i, relieved: false);
                continue;
            }
            if (result == SiegeResult.Lifted) { EndInvestment(i, relieved: false); continue; }
            if (result == SiegeResult.Capitulated)
            {
                int loser = _sCiv[investment.Settlement];
                var occupation = new Occupation { Civ = investment.Civ, Settlement = investment.Settlement,
                    SinceSeason = _season, RequiredSeasons = 0 };
                for (int m = 0; m < investment.Members.Count; m++)
                {
                    long id = investment.Members[m];
                    if (!_economy.TryFigure(id, out _) || _economy.IsWounded(id)) continue;
                    occupation.Members.Add(id); occupation.Strengths.Add(investment.Strengths[m]);
                }
                if (occupation.Members.Count >= Captures.MinHolders)
                {
                    _economy.SetUnderInvestment(investment.Settlement, false);
                    Capture(occupation, loser, assimilationPenalty: 10);
                    _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentCapitulated,
                        loser, investment.Settlement, _season - investment.BeganSeason));
                    _events.Add(new PoliticsEvent(_tick, (byte)loser, PoliticsEventKind.InvestmentCapitulated,
                        investment.Civ, investment.Settlement, _season - investment.BeganSeason));
                    _economy.ClearIdle(investment.Members);
                    _investments.RemoveAt(i);
                }
                else EndInvestment(i, relieved: false);
            }
        }
        foreach (int settlement in AllInvestedSettlements()) _economy.SetUnderInvestment(settlement, true);
    }

    private List<int> AllInvestedSettlements()
    {
        var result = new List<int>(_investments.Count);
        foreach (Investment investment in _investments) result.Add(investment.Settlement);
        return result;
    }

    private void ApplyDesertion(Investment investment, List<Fighter> fighters, Fix64 targetStrength)
    {
        int membersBefore = investment.Members.Count;
        Fix64 strength = Fix64.Zero;
        foreach (Fighter fighter in fighters) strength += fighter.Strength;
        if (fighters.Count == 0 || strength <= targetStrength) return;
        Pcg32 rng = Pcg32.ForStream(_seed, SiegeStream ^ 0x444553455254UL,
            (ulong)investment.Settlement, _season);
        // Pick deserters by a seeded shuffle; they return to their home settlement alive.
        for (int i = fighters.Count - 1; i > 0; i--)
        {
            int j = (int)rng.NextBelow((uint)(i + 1));
            (fighters[i], fighters[j]) = (fighters[j], fighters[i]);
        }
        Fix64 remaining = strength;
        for (int f = 0; f < fighters.Count && remaining > targetStrength;)
        {
            Fighter fighter = fighters[f++];
            _economy.RelocateFigure(fighter.Id, investment.HomeSettlement);
            _economy.ClearIdle(new[] { fighter.Id });
            for (int i = 0; i < investment.Members.Count; i++)
                if (investment.Members[i] == fighter.Id) { investment.Members.RemoveAt(i); investment.Strengths.RemoveAt(i); break; }
            remaining -= fighter.Strength;
        }
        if (investment.Members.Count < membersBefore)
            _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentDeserted,
                investment.TargetCiv, investment.Settlement, investment.Members.Count));
        investment.State = investment.State with { BesiegerStrength = remaining };
    }

    private bool ResolveSally(Investment investment, List<Fighter> besiegers)
    {
        List<Fighter> defenders = Fighters(investment.Settlement, attacking: false, out _, _sTile[investment.Settlement]);
        if (defenders.Count == 0 || besiegers.Count == 0) return false;
        var setup = new BattleSetup(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10),
            Raids.MaxRounds, false);
        BattleResult battle = Combat.Fight(defenders, besiegers, setup, _seed,
            SiegeStream ^ 0x53414C4C59UL ^ (ulong)investment.Settlement ^ (ulong)_season * 104729UL);
        ApplyCasualties(battle.AttackerDead, battle.AttackerWounded);
        ApplyCasualties(battle.DefenderDead, battle.DefenderWounded);
        _events.Add(new PoliticsEvent(_tick, (byte)investment.TargetCiv, PoliticsEventKind.InvestmentSally,
            investment.Civ, investment.Settlement, battle.Winner == BattleWinner.Attacker ? 1 : 0));
        _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentSally,
            investment.TargetCiv, investment.Settlement, battle.Winner == BattleWinner.Attacker ? 1 : 0));
        return battle.Winner == BattleWinner.Attacker;
    }

    private void EndInvestment(int index, bool relieved)
    {
        Investment investment = _investments[index];
        _economy.SetUnderInvestment(investment.Settlement, false);
        _economy.ClearIdle(investment.Members);
        _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentRelieved,
            investment.TargetCiv, investment.Settlement, relieved ? 1 : 0));
        _events.Add(new PoliticsEvent(_tick, (byte)investment.TargetCiv, PoliticsEventKind.InvestmentRelieved,
            investment.Civ, investment.Settlement, relieved ? 1 : 0));
        _investments.RemoveAt(index);
    }

    /// <summary>
    /// Resolves an already-arrived owner or ally relief host against an investment in open field. The current order model has
    /// no way to represent a force traveling to an allied besieged settlement, so callers supply its arrival here.
    /// </summary>
    public bool ResolveInvestmentRelief(int civ, int settlement, IReadOnlyList<long> reliefMembers)
    {
        Investment? investment = InvestmentAt(settlement);
        if (investment is null || civ == investment.Civ || civ < 0 || civ >= _civs ||
            civ != investment.TargetCiv && !_dip.Allied(civ, investment.TargetCiv) || reliefMembers.Count == 0) return false;
        var alive = new List<Fighter>();
        foreach (long id in reliefMembers)
        {
            if (!_economy.TryFigure(id, out FigureView figure) || _economy.IsWounded(id) ||
                figure.Civilization != civ || (uint)figure.SettlementId >= (uint)_sCount) continue;
            List<Fighter> settlementFighters = Fighters(figure.SettlementId, attacking: true, out _, _sTile[settlement]);
            foreach (Fighter candidate in settlementFighters)
                if (candidate.Id == id) { alive.Add(candidate); break; }
        }
        if (alive.Count == 0) return false;
        var besiegers = new List<Fighter>();
        CurrentInvestmentStrength(investment, besiegers);
        if (besiegers.Count == 0) { EndInvestment(_investments.IndexOf(investment), relieved: true); return true; }
        var setup = new BattleSetup(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10),
            Raids.MaxRounds, false);
        BattleResult battle = Combat.Fight(alive, besiegers, setup, _seed,
            SiegeStream ^ 0x52454C494546UL ^ (ulong)settlement ^ (ulong)_season * 13007UL);
        ApplyCasualties(battle.AttackerDead, battle.AttackerWounded);
        ApplyCasualties(battle.DefenderDead, battle.DefenderWounded);
        if (battle.Winner == BattleWinner.Attacker) EndInvestment(_investments.IndexOf(investment), relieved: true);
        return true;
    }

    private void CutInvestmentRoutes(int settlement)
    {
        for (int i = _expeditions.Count - 1; i >= 0; i--)
        {
            Expedition caravan = _expeditions[i];
            if (caravan.Kind != ExpeditionKind.Caravan ||
                caravan.FromSettlement != settlement && caravan.ToSettlement != settlement) continue;
            if ((uint)caravan.FromSettlement < (uint)_sCount) Unload(caravan.FromSettlement, caravan.Good, caravan.Cargo);
            _expeditions.RemoveAt(i);
        }
    }

    private void HashInvestments(ref StateHasher h)
    {
        h.Add(_investments.Count);
        foreach (Investment investment in _investments)
        {
            h.Add(investment.Civ); h.Add(investment.TargetCiv); h.Add(investment.Settlement); h.Add(investment.HomeSettlement);
            h.Add(investment.BeganSeason); h.Add(investment.State.BesiegerStrength); h.Add(investment.State.UnwalledDefenderStrength);
            h.Add(investment.State.DefenderStrength); h.Add(investment.State.StoresSeasons); h.Add(investment.State.BesiegerFoodSeasons);
            h.Add(investment.State.Season); h.Add(investment.Members.Count);
            for (int i = 0; i < investment.Members.Count; i++) { h.Add(investment.Members[i]); h.Add(investment.Strengths[i]); }
        }
    }

    private void WriteInvestments(BinaryWriter writer)
    {
        writer.Write(_investments.Count);
        foreach (Investment investment in _investments)
        {
            writer.Write(investment.Civ); writer.Write(investment.TargetCiv); writer.Write(investment.Settlement);
            writer.Write(investment.HomeSettlement); writer.Write(investment.BeganSeason);
            writer.Write(investment.State.BesiegerStrength.Raw); writer.Write(investment.State.UnwalledDefenderStrength.Raw);
            writer.Write(investment.State.DefenderStrength.Raw); writer.Write(investment.State.StoresSeasons.Raw);
            writer.Write(investment.State.BesiegerFoodSeasons); writer.Write(investment.State.Season);
            writer.Write(investment.Members.Count);
            for (int i = 0; i < investment.Members.Count; i++)
            { writer.Write(investment.Members[i]); writer.Write(investment.Strengths[i].Raw); }
        }
    }

    private void ReadInvestments(BinaryReader reader)
    {
        _investments.Clear();
        int count = reader.ReadInt32();
        if (count < 0 || count > _economy.SettlementCount) throw new InvalidDataException("Snapshot investment count is invalid.");
        for (int i = 0; i < count; i++)
        {
            var investment = new Investment
            {
                Civ = reader.ReadInt32(), TargetCiv = reader.ReadInt32(), Settlement = reader.ReadInt32(),
                HomeSettlement = reader.ReadInt32(), BeganSeason = reader.ReadInt32(),
                State = new SiegeState(Fix64.FromRaw(reader.ReadInt64()), Fix64.FromRaw(reader.ReadInt64()),
                    Fix64.FromRaw(reader.ReadInt64()), Fix64.FromRaw(reader.ReadInt64()), reader.ReadInt32(), reader.ReadInt32()),
            };
            int members = reader.ReadInt32();
            if (members < 0 || members > 100_000 || (uint)investment.Settlement >= (uint)_economy.SettlementCount ||
                (uint)investment.HomeSettlement >= (uint)_economy.SettlementCount || (uint)investment.Civ >= (uint)_civs ||
                (uint)investment.TargetCiv >= (uint)_civs || investment.Civ == investment.TargetCiv)
                throw new InvalidDataException("Snapshot investment fields are invalid.");
            for (int m = 0; m < members; m++)
            { investment.Members.Add(reader.ReadInt64()); investment.Strengths.Add(Fix64.FromRaw(reader.ReadInt64())); }
            if (investment.Members.Count < Captures.MinHolders || investment.Strengths.Count != investment.Members.Count ||
                i > 0 && _investments[i - 1].Settlement >= investment.Settlement)
                throw new InvalidDataException("Snapshot investment members or order are invalid.");
            _investments.Add(investment);
            _economy.SetUnderInvestment(investment.Settlement, true);
            _economy.SetIdle(investment.Members, int.MaxValue);
        }
    }
}
