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
    // A one-season override for controlled lifecycle fixtures; never serialized or used by production matches.
    internal Fix64? FixtureStoresAfterRation { get; set; }
    /// <summary>Siege engines the host carries (WAR-09), and the seasons they have been in the field.</summary>
    public int Engines { get; internal set; }
    public int EngineSeasons { get; internal set; }
    /// <summary>Fief cannons carried by this host (WAR-12).</summary>
    public int Cannons { get; internal set; }
    public int CannonSeasons { get; internal set; }
    /// <summary>True when powder-backed cannon fire has opened the standing wall.</summary>
    public bool PowderBreach { get; internal set; }
    /// <summary>Wall integrity: 1 intact, 0 breached. Only an engine train reduces it.</summary>
    public Fix64 Integrity { get; internal set; } = Fix64.One;
    public List<long> Members { get; } = new();
    public List<Fix64> Strengths { get; } = new();
}

/// <summary>Diagnostic-only observation of a live Fief siege turn; never included in simulation state.</summary>
public readonly record struct SiegeTraceObservation
{
    private readonly SiegeTraceObservationData? _data;

    internal SiegeTraceObservation(int season, int attacker, int defender, int settlement, int engines, int cannons, Fix64 integrity)
        => _data = new SiegeTraceObservationData(season, attacker, defender, settlement, engines, cannons, integrity);

    internal bool IsInitialized => _data is not null;
    public int Season => _data?.Season ?? 0;
    public int Attacker => _data?.Attacker ?? 0;
    public int Defender => _data?.Defender ?? 0;
    public int Settlement => _data?.Settlement ?? 0;
    public int Engines { get => _data?.Engines ?? 0; internal set { if (_data is not null) _data.Engines = value; } }
    public int Cannons { get => _data?.Cannons ?? 0; internal set { if (_data is not null) _data.Cannons = value; } }
    public int EngineCrews { get => _data?.EngineCrews ?? 0; internal set { if (_data is not null) _data.EngineCrews = value; } }
    public int CannonCrews { get => _data?.CannonCrews ?? 0; internal set { if (_data is not null) _data.CannonCrews = value; } }
    public int CannonShots { get => _data?.CannonShots ?? 0; internal set { if (_data is not null) _data.CannonShots = value; } }
    public Fix64 PowderPaid { get => _data?.PowderPaid ?? Fix64.Zero; internal set { if (_data is not null) _data.PowderPaid = value; } }
    public int AttritionAssignedCrews { get => _data?.AttritionAssignedCrews ?? 0; internal set { if (_data is not null) _data.AttritionAssignedCrews = value; } }
    public int AttritionDeaths { get => _data?.AttritionDeaths ?? 0; internal set { if (_data is not null) _data.AttritionDeaths = value; } }
    public bool Def09Active { get => _data?.Def09Active ?? false; internal set { if (_data is not null) _data.Def09Active = value; } }
    public Fix64 IntegrityBefore { get => _data?.IntegrityBefore ?? Fix64.Zero; internal set { if (_data is not null) _data.IntegrityBefore = value; } }
    public Fix64 IntegrityAfter { get => _data?.IntegrityAfter ?? Fix64.Zero; internal set { if (_data is not null) _data.IntegrityAfter = value; } }
    public bool PowderBreach { get => _data?.PowderBreach ?? false; internal set { if (_data is not null) _data.PowderBreach = value; } }
    public bool Supplied { get => _data?.Supplied ?? false; internal set { if (_data is not null) _data.Supplied = value; } }
    public bool FireAttempted { get => _data?.FireAttempted ?? false; internal set { if (_data is not null) _data.FireAttempted = value; } }
}

internal sealed class SiegeTraceObservationData(int season, int attacker, int defender, int settlement,
    int engines, int cannons, Fix64 integrity)
{
    public readonly int Season = season, Attacker = attacker, Defender = defender, Settlement = settlement;
    public int Engines = engines, Cannons = cannons, EngineCrews, CannonCrews, CannonShots;
    public Fix64 PowderPaid, IntegrityBefore = integrity, IntegrityAfter = integrity;
    public int AttritionAssignedCrews, AttritionDeaths;
    public bool PowderBreach, Supplied, FireAttempted, Def09Active;
}

public sealed partial class PoliticsSystem
{
    private static readonly ulong SiegeStream = Hash64.OfString("politics.siege");
    private readonly List<Investment> _investments = new();

    /// <summary>Active walled-settlement investments, in ascending settlement order.</summary>
    public IReadOnlyList<Investment> Investments => _investments;

    /// <summary>Optional report-only observer. Null by default; observations are not saved or checksummed.</summary>
    public Action<SiegeTraceObservation>? SiegeTraceObserverForDiagnostics { get; set; }

    public Investment? InvestmentAt(int settlement)
    {
        foreach (Investment investment in _investments)
            if (investment.Settlement == settlement) return investment;
        return null;
    }

    /// <summary>Sets the controlled post-ration store ratio consumed by the next live siege update in a fixture.</summary>
    internal void FixtureSetInvestmentStoresAfterRation(int settlement, Fix64 storesAfterRation)
    {
        Investment investment = InvestmentAt(settlement) ?? throw new InvalidOperationException("The settlement has no active investment.");
        investment.FixtureStoresAfterRation = Fix64.Max(Fix64.Zero, storesAfterRation);
    }

    private bool UnderInvestment(int settlement) => InvestmentAt(settlement) is not null;

    /// <summary>
    /// A conquest prospect (docs/MVP/37 §5): the civilization knows WAR-12 and a hostile, contacted, not allied rival holds a walled settlement within siege supply
    /// reach of one of its towns, and its field force (60% of its fighters, as a campaign gathers) beats that town's defenders plus nearby relief by the AI's 1.5 margin.
    /// It reads true state and keeps none of its own, so a restored match answers it exactly as an uninterrupted one.
    /// </summary>
    public bool ConquestProspect(int civ)
    {
        if (!_economy.FiefChain || (uint)civ >= (uint)_civs || !_research.IsDone(civ, _p.War12)) return false;
        Fix64 force = Fix64.Zero;
        bool haveForce = false;
        for (int t = 0; t < _sCount; t++)
        {
            int o = _sCiv[t];
            if (o == civ || o < 0 || _free[t] || !_dip.Contacted(civ, o) || _dip.Allied(civ, o)) continue;
            if (!_dip.AtWar(civ, o) && _dip.Relations(civ, o) >= Diplomacy.HostileBelow) continue;
            if (!DefensesOf(t).StoneWalls) continue;
            int[] distances = WalkFrom(_sTile[t]);
            bool inSupply = false;
            for (int s = 0; s < _sCount && !inSupply; s++)
                if (_sCiv[s] == civ && !_free[s] && distances[_sTile[s]] <= Sieges.SupplyReach(true)) inSupply = true;
            if (!inSupply) continue;
            if (!haveForce) { force = FieldStrength(civ) * Fix64.FromRatio(6, 10); haveForce = true; }
            Fix64 defence = UnwalledDefenseStrength(t) * Fix64.FromRatio(11, 10);
            for (int r = 0; r < _sCount; r++)
                if (r != t && _sCiv[r] == o && !_free[r] && HexGrid.Distance(_sTile[r], _sTile[t], _width) <= CivSim.Sim.Ai.StrategistTuning.FiefReliefRange)
                    defence += UnwalledDefenseStrength(r) * Fix64.FromRatio(6, 10);
            if (force >= defence * Fix64.FromRatio(15, 10)) return true;
        }
        return false;
    }

    private bool HasSupplyLine(int civ, int target)
    {
        int[] distances = WalkFrom(_sTile[target]);
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && distances[_sTile[s]] <= Sieges.SupplyReach(_economy.FiefChain)) return true;
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
        // A host with crew takes the home settlement's engines (WAR-09): four crew for each, up to three.
        if (_economy.IronChain && _research.IsDone(party.Civ, _p.War09) && party.FromSettlement < _sCount)
            investment.Engines = _economy.TakeEngines(party.FromSettlement, Math.Min(Sieges.MaxEngines, ids.Count / Sieges.CrewPerEngine));
        if (_economy.FiefChain && _research.IsDone(party.Civ, _p.War12) && party.FromSettlement < _sCount) _economy.StageFiefTrain(party.FromSettlement); // the Arsenal hauls its train to the host
        if (_economy.FiefChain && _research.IsDone(party.Civ, _p.War12) && party.FromSettlement < _sCount)
        {
            int cannonSlots = Math.Max(0, ids.Count / Sieges.CrewPerEngine - investment.Engines);
            investment.Cannons = _economy.TakeCannons(party.FromSettlement, Math.Min(Sieges.MaxCannons, cannonSlots));
        }
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

    private bool FiefCrewAttrition(Investment investment, List<Fighter> besiegers, out int assigned, out int deaths, out bool def09Active)
    {
        assigned = 0;
        deaths = 0;
        def09Active = false;
        if (!_economy.FiefChain || investment.Engines + investment.Cannons <= 0 || besiegers.Count == 0) return false;
        Defenses defenses = DefensesOf(investment.Settlement);
        def09Active = defenses.ConcentricCastle && UnwalledDefenseStrength(investment.Settlement) > Fix64.Zero &&
                     _research.IsDone(investment.TargetCiv, _p.Def09);
        assigned = Math.Min((investment.Engines + investment.Cannons) * Sieges.CrewPerEngine, besiegers.Count);
        var dead = new List<long>();
        for (int i = 0; i < assigned; i++)
            if (Sieges.RollCrewAttrition(_seed, investment.Settlement, _season, besiegers[i].Id, def09Active)) dead.Add(besiegers[i].Id);
        deaths = dead.Count;
        if (deaths == 0) return false;
        ApplyCasualties(dead, new List<long>(), "siegeCrewAttrition");
        return true;
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

            if (investment.FixtureStoresAfterRation is { } fixtureStores)
            {
                investment.State = investment.State with { StoresSeasons = fixtureStores + Sieges.RationNeed };
                investment.FixtureStoresAfterRation = null;
            }
            else
            {
                Fix64 need = _economy.FoodNeedForSeason(investment.Settlement, _season % 4 == 3);
                Fix64 stores = _economy.FoodStores(investment.Settlement);
                // The economy has already consumed this season's 75% ration before OnSeasonEnd. Restore that amount in the
                // helper's starting value so its per-season subtraction leaves the actual post-ration store level.
                investment.State = investment.State with { StoresSeasons = need <= Fix64.Zero ? Fix64.Zero : stores / need + Sieges.RationNeed };
            }
            SiegeTraceObservation siegeTrace = SiegeTraceObserverForDiagnostics is null ? default :
                new SiegeTraceObservation(_season, investment.Civ, investment.TargetCiv, investment.Settlement,
                    investment.Engines, investment.Cannons, investment.Integrity)
                {
                    PowderBreach = investment.PowderBreach,
                    Supplied = supplied,
                };
            bool crewCasualties = FiefCrewAttrition(investment, besiegers, out int assignedCrewCount, out int crewDeathCount, out bool def09Active);
            if (siegeTrace.IsInitialized)
            {
                siegeTrace.AttritionAssignedCrews = assignedCrewCount;
                siegeTrace.AttritionDeaths = crewDeathCount;
                siegeTrace.Def09Active = def09Active;
                SiegeTraceObserverForDiagnostics?.Invoke(siegeTrace);
            }
            if (crewCasualties)
            {
                force = CurrentInvestmentStrength(investment, besiegers);
                investment.State = investment.State with { BesiegerStrength = force };
            }
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
            if ((investment.Engines > 0 || investment.Cannons > 0) && (result is SiegeResult.Ongoing or SiegeResult.Capitulated) && BatterWalls(investment, supplied, besiegers, siegeTrace))
            {
                // The wall is breached: the besiegers storm it at once, and the existing assault resolver decides (breach, defenders, casualties).
                StormBreach(i, investment, besiegers);
                continue;
            }
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
                    ReturnEngines(investment);
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

    /// <summary>
    /// A season of engine fire (WAR-09). The train needs supply and charcoal from home to run; the engines that run take the wall down at
    /// <see cref="Sieges.Batter"/>'s rate, and spent engines (twelve seasons in the field) are lost. Returns true on the season the wall is breached.
    /// </summary>
    private bool BatterWalls(Investment investment, bool supplied, List<Fighter> besiegers, SiegeTraceObservation trace)
    {
        Fix64 integrityBefore = investment.Integrity;
        int enginesBefore = investment.Engines, cannonsBefore = investment.Cannons;
        int engineCrews = Math.Min(investment.Engines, besiegers.Count / Sieges.CrewPerEngine);
        int cannonCrews = Math.Min(investment.Cannons, Math.Max(0, besiegers.Count / Sieges.CrewPerEngine - engineCrews));
        int running = supplied ? _economy.PayEngineUpkeep(investment.HomeSettlement, engineCrews) : 0;
        int firingCannons = supplied ? _economy.PayCannonPowder(investment.HomeSettlement, cannonCrews) : 0;
        investment.EngineSeasons++;
        if (investment.EngineSeasons >= Sieges.EngineSeasons) investment.Engines = 0;
        if (investment.Integrity <= Fix64.Zero)
        {
            UpdateSiegeTrace(trace, investment, supplied, enginesBefore, cannonsBefore, engineCrews, cannonCrews,
                firingCannons, integrityBefore);
            return true;
        }
        Defenses defenses = DefensesOf(investment.Settlement);
        investment.Integrity = Sieges.Batter(investment.Integrity, Fix64.FromInt(running) + firingCannons * Sieges.CannonWallDamage, defenses.BastionFort ? Defenses.BastionWall : defenses.Gatehouse ? Defenses.IntactTowerWall : Fix64.FromInt(3));
        if (firingCannons > 0) investment.PowderBreach = true;
        UpdateSiegeTrace(trace, investment, supplied, enginesBefore, cannonsBefore, engineCrews, cannonCrews,
            firingCannons, integrityBefore);
        return investment.Integrity <= Fix64.Zero;
    }

    private void UpdateSiegeTrace(SiegeTraceObservation trace, Investment investment, bool supplied,
        int engines, int cannons, int engineCrews, int cannonCrews, int shots, Fix64 integrityBefore)
    {
        if (!trace.IsInitialized) return;
        trace.Engines = engines;
        trace.Cannons = cannons;
        trace.EngineCrews = engineCrews;
        trace.CannonCrews = cannonCrews;
        trace.CannonShots = shots;
        trace.PowderPaid = Fix64.FromRatio(shots, 4);
        trace.IntegrityBefore = integrityBefore;
        trace.IntegrityAfter = investment.Integrity;
        trace.PowderBreach = investment.PowderBreach;
        trace.Supplied = supplied;
        trace.FireAttempted = true;
        SiegeTraceObserverForDiagnostics?.Invoke(trace);
    }

    /// <summary>
    /// The storm after a breach: the existing assault resolver against the defenders with the wall gone (a castle's strongpoint and the defenders' home ground still count).
    /// A win that the besiegers can hold takes the settlement; a rout ends the siege; a stand-off leaves the breach open for next season.
    /// </summary>
    private void StormBreach(int index, Investment investment, List<Fighter> besiegers)
    {
        int settlement = investment.Settlement, loser = _sCiv[settlement];
        List<Fighter> defenders = Fighters(settlement, attacking: false, out _, _sTile[settlement]);
        Defenses breached = investment.PowderBreach
            ? DefensesOf(settlement) with { PowderBreach = true }
            : DefensesOf(settlement) with { StoneWalls = false, Gatehouse = false };
        BattleResult result = Captures.Assault(besiegers, defenders, breached, childrenInside: true, _seed,
            SiegeStream ^ 0x425245414348UL ^ (ulong)settlement ^ (ulong)_season * 7919UL);
        ApplyCasualties(result.AttackerDead, result.AttackerWounded, "siegeBreach");
        ApplyCasualties(result.DefenderDead, result.DefenderWounded, "siegeBreach");
        int fight = PoliticsEvent.PackFight(besiegers.Count, defenders.Count,
            result.AttackerDead.Count + result.AttackerWounded.Count, result.DefenderDead.Count + result.DefenderWounded.Count);
        bool won = Captures.CanHold(result);
        _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.AssaultResolved, loser, won ? 1 : 0, settlement, _sTile[settlement], fight));
        _events.Add(new PoliticsEvent(_tick, (byte)loser, PoliticsEventKind.AssaultResolved, investment.Civ, won ? 2 : 3, settlement, _sTile[settlement], fight));
        if (won)
        {
            var occupation = new Occupation { Civ = investment.Civ, Settlement = settlement, SinceSeason = _season, RequiredSeasons = 0 };
            for (int m = 0; m < investment.Members.Count; m++)
            {
                long id = investment.Members[m];
                if (!_economy.TryFigure(id, out _) || _economy.IsWounded(id) || result.AttackerDead.Contains(id)) continue;
                occupation.Members.Add(id); occupation.Strengths.Add(investment.Strengths[m]);
            }
            if (occupation.Members.Count >= Captures.MinHolders)
            {
                _economy.SetUnderInvestment(settlement, false);
                ReturnEngines(investment);
                Capture(occupation, loser, assimilationPenalty: 10);
                _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentCapitulated, loser, settlement, _season - investment.BeganSeason));
                _events.Add(new PoliticsEvent(_tick, (byte)loser, PoliticsEventKind.InvestmentCapitulated, investment.Civ, settlement, _season - investment.BeganSeason));
                _economy.ClearIdle(investment.Members);
                _investments.RemoveAt(index);
                return;
            }
        }
        if (result.Winner == BattleWinner.Defender || won)
        {
            _failedCampaigns[investment.Civ * _civs + loser]++;
            EndInvestment(index, relieved: false);
        }
    }

    /// <summary>A siege that ends brings its engines home (the ones not yet spent).</summary>
    private void ReturnEngines(Investment investment)
    {
        if (investment.Engines > 0 && investment.HomeSettlement < _sCount && _sCiv[investment.HomeSettlement] == investment.Civ)
            _economy.ReturnEngines(investment.HomeSettlement, investment.Engines);
        investment.Engines = 0;
        ReturnCannons(investment);
    }

    private void ReturnCannons(Investment investment)
    {
        if (investment.Cannons > 0 && investment.HomeSettlement < _sCount && _sCiv[investment.HomeSettlement] == investment.Civ)
            _economy.ReturnCannons(investment.HomeSettlement, investment.Cannons);
        investment.Cannons = 0;
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
        ApplyCasualties(battle.AttackerDead, battle.AttackerWounded, "siegeSally");
        ApplyCasualties(battle.DefenderDead, battle.DefenderWounded, "siegeSally");
        _events.Add(new PoliticsEvent(_tick, (byte)investment.TargetCiv, PoliticsEventKind.InvestmentSally,
            investment.Civ, investment.Settlement, battle.Winner == BattleWinner.Attacker ? 1 : 0));
        _events.Add(new PoliticsEvent(_tick, (byte)investment.Civ, PoliticsEventKind.InvestmentSally,
            investment.TargetCiv, investment.Settlement, battle.Winner == BattleWinner.Attacker ? 1 : 0));
        return battle.Winner == BattleWinner.Attacker;
    }

    private void EndInvestment(int index, bool relieved)
    {
        Investment investment = _investments[index];
        ReturnEngines(investment);
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
        ApplyCasualties(battle.AttackerDead, battle.AttackerWounded, "siegeRelief");
        ApplyCasualties(battle.DefenderDead, battle.DefenderWounded, "siegeRelief");
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
            if (_economy.IronChain) { h.Add(investment.Engines); h.Add(investment.EngineSeasons); h.Add(investment.Integrity); }
            if (_economy.FiefChain) { h.Add(investment.Cannons); h.Add(investment.CannonSeasons); h.Add(investment.PowderBreach); }
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
            if (_economy.IronChain) { writer.Write(investment.Engines); writer.Write(investment.EngineSeasons); writer.Write(investment.Integrity.Raw); }
            if (_economy.FiefChain) { writer.Write(investment.Cannons); writer.Write(investment.CannonSeasons); writer.Write(investment.PowderBreach); }
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
            if (_economy.IronChain)
            {
                investment.Engines = reader.ReadInt32(); investment.EngineSeasons = reader.ReadInt32(); investment.Integrity = Fix64.FromRaw(reader.ReadInt64());
                if (investment.Engines < 0 || investment.Engines > Sieges.MaxEngines || investment.EngineSeasons < 0 || investment.Integrity < Fix64.Zero || investment.Integrity > Fix64.One)
                    throw new InvalidDataException("Snapshot investment engines are invalid.");
            }
            if (_economy.FiefChain)
            {
                investment.Cannons = reader.ReadInt32(); investment.CannonSeasons = reader.ReadInt32(); investment.PowderBreach = reader.ReadBoolean();
                if (investment.Cannons < 0 || investment.Cannons > Sieges.MaxCannons || investment.CannonSeasons < 0)
                    throw new InvalidDataException("Snapshot investment cannons are invalid.");
            }
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
