using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

public sealed partial class PoliticsSystem
{
    private const int GiftIntervalSeasons = 8;
    private const int BarterIntervalSeasons = 4;
    private const int RaidCooldownSeasons = 8;
    private const int ForecastTrials = 12;
    private static readonly Fix64 GiftValue = Fix64.FromInt(5);
    private static readonly Fix64 BarterValue = Fix64.FromInt(6);
    private static readonly Fix64 TradeGain = Fix64.FromRatio(11, 10);
    private static readonly Fix64 LifeValue = Fix64.FromInt(8);
    private static readonly Fix64 RaidRelationsCost = Fix64.FromInt(6);

    /// <summary>Gate targets whose goods change hands in barter, so a partner's sight of them becomes yours (docs/MVP/13 §4.2).</summary>
    private static readonly string[] TradedGoods = { "Clay", "Native copper", "Salt", "Tin", "Gold or Silver", "Coal", "Silk moths" };

    // ---- the Civ Mind, version 0 ----------------------------------------------------------

    /// <summary>
    /// The rule set every civilization runs once a season (docs/MVP/05 §6.2, 13): alliances, gifts, barter, raids, war and
    /// peace. It reads true state, not what its figures have seen; the belief state and forecast model of M6 replace it.
    /// </summary>
    private void RunCivMinds()
    {
        var launched = new bool[_civs];
        for (int c = 0; c < _civs; c++)
            for (int o = 0; o < _civs; o++)
            {
                if (c == o || _externalMind[c] || !_dip.Contacted(c, o)) continue;
                if (c < o) MaybeAlly(c, o);
                if (_dip.Allied(c, o)) continue;
                if (_dip.AtWar(c, o))
                {
                    if (!launched[c] && ConsiderCampaign(c, o)) launched[c] = true;
                    if (c < o) ConsiderPeace(c, o);
                    continue;
                }
                if (!launched[c] && ConsiderGift(c, o)) launched[c] = true;
                if (c < o && ConsiderBarter(c, o)) launched[c] = true;
                if (!launched[c] && ConsiderRaid(c, o)) launched[c] = true;
                if (c < o || !_dip.AtWar(c, o)) ConsiderWar(c, o);
            }
    }

    private void MaybeAlly(int a, int b)
    {
        if (_dip.AtWar(a, b) || _dip.Allied(a, b) || _dip.Relations(a, b) <= Diplomacy.AllianceAbove) return;
        if (!_research.IsDone(a, _p.Dip03) || !_research.IsDone(b, _p.Dip03)) return;
        _dip.Ally(a, b);
        _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.AllianceFormed, b, 0, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.AllianceFormed, a, 0, 0));
    }

    private int NearestOwnSettlement(int civ, int tile)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s]) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (d < bestDistance) { bestDistance = d; best = s; }
        }
        return best;
    }

    /// <summary>Walking distance in tiles between two tiles, or int.MaxValue when there is no way.</summary>
    private int Walk(int from, int to)
    {
        int[] d = HexGrid.WalkingDistancesFrom(_width, _height, _passable, from);
        return d[to];
    }

    private static int SeasonsFor(int tiles) => Math.Max(1, (tiles + TilesPerSeason - 1) / TilesPerSeason);

    /// <summary>
    /// The settlement a party sets out from: the nearest to the target that can spare eight fighters, else the one with most.
    /// A hamlet of six cannot raid however close it is.
    /// </summary>
    private int BestOrigin(int civ, int targetTile)
    {
        int best = -1, bestDistance = int.MaxValue, fullest = -1, mostFighters = -1;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s]) continue;
            int fighters = AdultCount(s);
            if (fighters > mostFighters) { mostFighters = fighters; fullest = s; }
            int d = HexGrid.Distance(_sTile[s], targetTile, _width);
            if (fighters >= 8 && d < bestDistance) { bestDistance = d; best = s; }
        }
        return best >= 0 ? best : fullest;
    }

    private (int Origin, int Target) ClosestPair(int civ, int other, int targetSettlement = -1)
    {
        if (targetSettlement >= 0)
        {
            if ((uint)targetSettlement >= (uint)_sCount || _sCiv[targetSettlement] != other || _free[targetSettlement] || _sPop[targetSettlement] <= 0)
                return (-1, -1);
            int origin = -1, distance = int.MaxValue;
            for (int s = 0; s < _sCount; s++)
            {
                if (_sCiv[s] != civ || _free[s]) continue;
                int d = HexGrid.Distance(_sTile[s], _sTile[targetSettlement], _width);
                if (d < distance) { distance = d; origin = s; }
            }
            return (origin, targetSettlement);
        }

        int bestA = -1, bestB = -1, best = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s]) continue;
            for (int t = 0; t < _sCount; t++)
            {
                if (_sCiv[t] != other || _free[t]) continue;
                int d = HexGrid.Distance(_sTile[s], _sTile[t], _width);
                if (d < best) { best = d; bestA = s; bestB = t; }
            }
        }
        return (bestA, bestB);
    }

    // ---- gifts and barter -----------------------------------------------------------------

    private bool ConsiderGift(int c, int o)
    {
        if (!_research.IsDone(c, _p.Trd01)) return false;
        int rel = _dip.Relations(c, o);
        if (rel < Diplomacy.HostileBelow || rel > 60 || _season - _dip.LastGift(c, o) < GiftIntervalSeasons) return false;
        if ((Posture)_posture[c] is Posture.Raid) return false;
        return TryGift(c, o);
    }

    /// <summary>Sends a gift party if the hard rules allow (TRD-01, contact, no war, food to spare); desirability is the caller's.</summary>
    private bool TryGift(int c, int o, int targetSettlement = -1)
    {
        if (!_research.IsDone(c, _p.Trd01) || !_dip.Contacted(c, o) || _dip.AtWar(c, o) || _season - _dip.LastGift(c, o) < 2) return false;
        (int origin, int target) = ClosestPair(c, o, targetSettlement);
        if (origin < 0) return false;
        int travel = Walk(_sTile[origin], _sTile[target]);
        if (travel == int.MaxValue) return false;
        if (_economy.FoodStores(origin) < GiftValue * 4) return false;
        List<Fighter> party = PickParty(origin, c, 3, attacking: true, out List<long> ids);
        if (party.Count < 2) return false;
        Fix64 taken = _economy.TakeGoods(origin, GiftValue, LootPreference.FoodOnly);
        if (taken <= Fix64.Zero) return false;
        var e = new Expedition { Kind = ExpeditionKind.Gift, Civ = c, TargetCiv = o, FromSettlement = origin, ToSettlement = target,
            ArriveSeason = _season + SeasonsFor(travel), Value = taken };
        for (int i = 0; i < ids.Count; i++) { e.Members.Add(ids[i]); e.Strengths.Add(party[i].Strength); }
        _expeditions.Add(e);
        _dip.RecordGift(c, o, _season);
        return true;
    }

    private bool ConsiderBarter(int a, int b)
    {
        if (!_research.IsDone(a, _p.Trd02) || !_research.IsDone(b, _p.Trd02)) return false;
        if (_dip.Relations(a, b) <= 0 || _season - _dip.LastBarter(a, b) < BarterIntervalSeasons) return false;
        return TryBarter(a, b);
    }

    /// <summary>Sends a barter trip if the hard rules allow (TRD-02 on both sides, Peaceful, none in flight).</summary>
    private bool TryBarter(int a, int b, int targetSettlement = -1)
    {
        if (!_research.IsDone(a, _p.Trd02) || !_research.IsDone(b, _p.Trd02) || _dip.Relations(a, b) <= 0 ||
            !_dip.Contacted(a, b) || _dip.AtWar(a, b) || _season - _dip.LastBarter(a, b) < 2) return false;
        foreach (Expedition e in _expeditions)
            if (e.Kind == ExpeditionKind.Barter && (e.Civ == a && e.TargetCiv == b || e.Civ == b && e.TargetCiv == a)) return false;
        (int origin, int target) = ClosestPair(a, b, targetSettlement);
        if (origin < 0) return false;
        int travel = Walk(_sTile[origin], _sTile[target]);
        if (travel == int.MaxValue) return false;
        if (_economy.FoodStores(origin) < BarterValue * 4) return false;
        List<Fighter> party = PickParty(origin, a, 3, attacking: true, out List<long> ids);
        if (party.Count < 2) return false;
        Fix64 taken = _economy.TakeGoods(origin, BarterValue, LootPreference.FoodOnly);
        if (taken <= Fix64.Zero) return false;
        var trip = new Expedition { Kind = ExpeditionKind.Barter, Civ = a, TargetCiv = b, FromSettlement = origin, ToSettlement = target,
            ArriveSeason = _season + SeasonsFor(travel), Value = taken };
        for (int i = 0; i < ids.Count; i++) { trip.Members.Add(ids[i]); trip.Strengths.Add(party[i].Strength); }
        _expeditions.Add(trip);
        _dip.RecordBarter(a, b, _season);
        return true;
    }

    private ulong TradedGoodsMask()
    {
        ulong mask = 0;
        foreach (string good in TradedGoods)
        {
            int t = _catalog.GateTargetIndex(good);
            if (t >= 0) mask |= 1UL << t;
        }
        return mask;
    }

    // ---- raids ----------------------------------------------------------------------------

    private bool Hungry(int civ)
    {
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _economy.SettlementLite(s).LastFoodSecurity < Fix64.FromRatio(8, 10)) return true;
        return false;
    }

    private bool ConsiderRaid(int c, int o)
    {
        if (!_research.IsDone(c, _p.War02)) return false;
        Posture posture = (Posture)_posture[c];
        if (posture is Posture.Fortify or Posture.Prosper) return false;
        if (_dip.InTruce(c, o, _season) || _season - _dip.LastRaidBy(c, o) < RaidCooldownSeasons) return false;
        bool wants = posture == Posture.Raid || _dip.Relations(c, o) < Diplomacy.HostileBelow ||
                     (_archetypes[c] == EconomyArchetype.Warlord && Hungry(c) && _dip.Relations(c, o) < 10);
        if (!wants) return false;
        return LaunchRaid(c, o, forecast: true);
    }

    /// <summary>
    /// Sends a raid now. With <paramref name="forecast"/> the Elders rehearse it first and stay home unless the odds and the
    /// value are good (the Civ Mind's rule); without it the raid goes regardless (a scenario hook for tests and the harness).
    /// </summary>
    public bool LaunchRaid(int c, int o, bool forecast)
    {
        Posture posture = (Posture)_posture[c];
        (int origin, int target) = ClosestPair(c, o);
        if (origin < 0) { Trace?.Invoke("raid: no pair"); return false; }
        origin = BestOrigin(c, _sTile[target]);
        int adults = AdultCount(origin);
        int percent = posture == Posture.Raid ? 30 : 20; // of the adults, rounded up (13 §5.2)
        int size = Math.Max(4, (adults * percent + 99) / 100);
        return SendRaid(c, o, origin, target, size, forecast);
    }

    /// <summary>Sends a raid party of up to <paramref name="size"/> from <paramref name="origin"/> to a chosen settlement.</summary>
    private bool SendRaid(int c, int o, int origin, int target, int size, bool forecast)
    {
        int travel = Walk(_sTile[origin], _sTile[target]);
        if (travel == int.MaxValue || (forecast && travel > ReachOf(c) * 3)) { Trace?.Invoke($"raid: travel {travel}"); return false; }
        List<Fighter> party = PickParty(origin, c, size, attacking: true, out List<long> ids);
        if (party.Count < 4) { Trace?.Invoke($"raid: party {party.Count}"); return false; }

        if (forecast)
        {
            // The Elders' forecast: rehearse the raid against what is there and go if it pays and the odds are good enough.
            List<Fighter> defenders = Fighters(target, attacking: false, out _);
            Defenses defenses = DefensesOf(target);
            Weapon(c, out bool ranged);
            int wins = 0, deaths = 0;
            for (int t = 0; t < ForecastTrials; t++)
            {
                RaidOutcome rehearsal = Raids.Resolve(party, defenders, defenses, ranged, _seed, ForecastStream ^ (ulong)(c * 4099 + o * 97 + t) ^ (ulong)_season * 7919UL);
                if (rehearsal.Success) wins++;
                deaths += rehearsal.Battle.AttackerDead.Count;
            }
            Fix64 p = Fix64.FromRatio(wins, ForecastTrials);
            Fix64 net = Raids.NetValue(p, Raids.CarryPerRaider * party.Count, Fix64.FromRatio(deaths, ForecastTrials), LifeValue, RaidRelationsCost);
            Trace?.Invoke($"raid forecast civ{c}->civ{o}: party {party.Count} vs {defenders.Count} defenders, success {p.ToDoubleForDisplay():F2}, net {net.ToDoubleForDisplay():F1}");
            if (p < _raidMargin[c] || net <= Fix64.Zero) return false;
        }

        var e = new Expedition { Kind = ExpeditionKind.Raid, Civ = c, TargetCiv = o, FromSettlement = origin, ToSettlement = target,
            ArriveSeason = _season + SeasonsFor(travel) };
        for (int i = 0; i < ids.Count; i++) { e.Members.Add(ids[i]); e.Strengths.Add(party[i].Strength); }
        _expeditions.Add(e);
        RaidCount++;
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.RaidLaunched, o, party.Count, target));
        return true;
    }

    private int AdultCount(int settlement)
    {
        var records = new List<LivingEconomyModel.FighterRecord>();
        _economy.ListFightingAge(settlement, records);
        int n = 0;
        foreach (var r in records) if (r.Age < 45) n++;
        return n;
    }

    // ---- war, campaigns, peace ------------------------------------------------------------

    private Fix64 FieldStrength(int civ)
    {
        Fix64 total = Fix64.Zero;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s]) continue;
            foreach (Fighter f in Fighters(s, attacking: true, out _)) total += f.Strength;
        }
        return total;
    }

    private void ConsiderWar(int c, int o)
    {
        if (_dip.AtWar(c, o) || _dip.Allied(c, o) || _dip.InTruce(c, o, _season)) return;
        if (!_research.IsDone(c, _p.War02)) return;
        Posture posture = (Posture)_posture[c];
        if (posture is Posture.Fortify or Posture.Prosper) return;
        if (_dip.Relations(c, o) >= Diplomacy.HostileBelow) return;
        if (posture != Posture.Raid && _archetypes[c] != EconomyArchetype.Warlord) return;
        // A war of conquest needs the field: one and a half times the rival's strength (13 §8.4).
        if (FieldStrength(c) < FieldStrength(o) * Fix64.FromRatio(15, 10)) return;
        _dip.DeclareWar(c, o);
        _warSince[c * _civs + o] = _season;
        _warSince[o * _civs + c] = _season;
        Wars++;
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.WarDeclared, o, 0, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.WarDeclared, c, 1, 0));
    }

    private bool ConsiderCampaign(int c, int o) => LaunchAssault(c, o, requireOdds: true);

    /// <summary>
    /// Sends the field army against the rival's nearest settlement. With <paramref name="requireOdds"/> it goes only with 1.5
    /// times the defenders' effective strength (13 §8.4); without it, regardless (a scenario hook).
    /// </summary>
    public bool LaunchAssault(int c, int o, bool requireOdds)
    {
        (int origin, int target) = ClosestPair(c, o);
        if (origin < 0) return false;
        return SendAssault(c, o, target, requireOdds);
    }

    private bool SendAssault(int c, int o, int target, bool requireOdds)
    {
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Assault && e.Civ == c && e.TargetCiv == o) return false;
        foreach (Occupation occ in _occupations) if (occ.Civ == c && _sCiv[occ.Settlement] == o) return false;
        int origin = BestOrigin(c, _sTile[target]);
        if (origin < 0) return false;
        int travel = Walk(_sTile[origin], _sTile[target]);
        if (travel == int.MaxValue || (requireOdds && travel > ReachOf(c) * 3)) return false;

        // The field army: up to 60% of the fighters in every own settlement within ten tiles of the origin.
        var force = new List<Fighter>();
        var ids = new List<long>();
        Fix64 strength = Fix64.Zero;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != c || _free[s] || HexGrid.Distance(_sTile[s], _sTile[origin], _width) > 10) continue;
            List<Fighter> here = Fighters(s, attacking: true, out List<long> hereIds);
            int allowed = Math.Max(0, Math.Min(here.Count - 4, here.Count * 6 / 10));
            var ordered = new List<Fighter>(here);
            ordered.Sort((x, y) => x.Strength != y.Strength ? y.Strength.CompareTo(x.Strength) : x.Id.CompareTo(y.Id));
            for (int i = 0; i < allowed; i++) { force.Add(ordered[i]); ids.Add(ordered[i].Id); strength += ordered[i].Strength; }
        }
        if (force.Count < (requireOdds ? 6 : Captures.MinHolders)) return false;
        Fix64 defence = Fix64.Zero;
        foreach (Fighter f in Fighters(target, attacking: false, out _)) defence += f.Strength;
        defence = defence * DefensesOf(target).Multiplier();
        // Move out only with the odds: 1.5 times the defenders' effective strength (13 §8.4).
        if (requireOdds && strength < defence * Fix64.FromRatio(15, 10)) return false;
        var assault = new Expedition { Kind = ExpeditionKind.Assault, Civ = c, TargetCiv = o, FromSettlement = origin, ToSettlement = target,
            ArriveSeason = _season + SeasonsFor(travel) };
        for (int i = 0; i < ids.Count; i++) { assault.Members.Add(ids[i]); assault.Strengths.Add(force[i].Strength); }
        _expeditions.Add(assault);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.RaidLaunched, o, force.Count, target));
        return true;
    }

    private void ConsiderPeace(int a, int b)
    {
        int since = _warSince[a * _civs + b];
        int age = _season - since;
        foreach (Occupation occ in _occupations)
            if (occ.Civ == a && _sCiv[occ.Settlement] == b || occ.Civ == b && _sCiv[occ.Settlement] == a) return;
        bool weary = age >= 12 || _failedCampaigns[a * _civs + b] >= 2 || _failedCampaigns[b * _civs + a] >= 2;
        if (!weary || age < 4) return;
        _dip.MakePeace(a, b, _season);
        _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.PeaceMade, b, age, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.PeaceMade, a, age, 0));
        _failedCampaigns[a * _civs + b] = 0;
        _failedCampaigns[b * _civs + a] = 0;
    }

    // ---- parties arrive -------------------------------------------------------------------

    private void ResolveExpeditions()
    {
        var arriving = new List<Expedition>();
        foreach (Expedition e in _expeditions) if (e.ArriveSeason <= _season) arriving.Add(e);
        foreach (Expedition e in arriving)
        {
            _expeditions.Remove(e);
            switch (e.Kind)
            {
                case ExpeditionKind.Gift: ArriveGift(e); break;
                case ExpeditionKind.Barter: ArriveBarter(e); break;
                case ExpeditionKind.Raid: ArriveRaid(e); break;
                case ExpeditionKind.Assault: ArriveAssault(e); break;
            }
        }
    }

    private List<Fighter> AliveFighters(Expedition e, out List<long> ids)
    {
        var list = new List<Fighter>();
        ids = new List<long>();
        for (int i = 0; i < e.Members.Count; i++)
        {
            if (!_economy.TryFigure(e.Members[i], out _) || _economy.IsWounded(e.Members[i])) continue;
            list.Add(new Fighter(e.Members[i], e.Strengths[i]));
            ids.Add(e.Members[i]);
        }
        return list;
    }

    private bool TargetStillThere(Expedition e)
        => e.ToSettlement < _sCount && !_free[e.ToSettlement] && _sCiv[e.ToSettlement] == e.TargetCiv;

    private void ArriveGift(Expedition e)
    {
        if (!TargetStillThere(e)) { _economy.GiveGoods(e.FromSettlement, e.Value); return; }
        // Spoilage on the road costs a tenth; the goods become the recipients' (13 §4.1).
        _economy.GiveGoods(e.ToSettlement, e.Value * Fix64.FromRatio(9, 10));
        int delta = Math.Max(2, 10 - 2 * Math.Max(0, _dip.GiftsThisYear(e.Civ, e.TargetCiv) - 1));
        _dip.Shift(e.Civ, e.TargetCiv, delta);
        Gifts++;
        _events.Add(new PoliticsEvent(_tick, (byte)e.Civ, PoliticsEventKind.Gift, e.TargetCiv, e.Value.FloorToInt(), delta));
    }

    private void ArriveBarter(Expedition e)
    {
        if (e.Leg == 0)
        {
            if (!TargetStillThere(e)) { _economy.GiveGoods(e.FromSettlement, e.Value); return; }
            // Both sides gain at least 10% in value (13 §4.2): the partner gets our goods and loads theirs for the way back.
            _economy.GiveGoods(e.ToSettlement, e.Value * TradeGain);
            Fix64 back = _economy.TakeGoods(e.ToSettlement, e.Value, LootPreference.FoodOnly);
            ulong goods = TradedGoodsMask();
            _research.AddSeen(e.Civ, _research.SeenMask(e.TargetCiv) & goods);
            _research.AddSeen(e.TargetCiv, _research.SeenMask(e.Civ) & goods);
            e.Leg = 1;
            e.Value = back;
            e.ArriveSeason = _season + Math.Max(1, SeasonsFor(Walk(_sTile[e.ToSettlement], _sTile[e.FromSettlement])));
            _expeditions.Add(e);
            return;
        }
        _economy.GiveGoods(e.FromSettlement, e.Value * TradeGain);
        _dip.Shift(e.Civ, e.TargetCiv, 3);
        BarterTrips++;
        _events.Add(new PoliticsEvent(_tick, (byte)e.Civ, PoliticsEventKind.Barter, e.TargetCiv, e.Value.FloorToInt(), 0));
    }

    private void ArriveRaid(Expedition e)
    {
        int c = e.Civ, o = e.TargetCiv;
        if (!TargetStillThere(e)) return;
        List<Fighter> raiders = AliveFighters(e, out _);
        if (raiders.Count < 3) return;
        List<Fighter> defenders = Fighters(e.ToSettlement, attacking: false, out _);
        Defenses defenses = DefensesOf(e.ToSettlement);
        Weapon(c, out bool ranged);
        RaidOutcome outcome = Raids.Resolve(raiders, defenders, defenses, ranged, _seed, RaidStream ^ (ulong)(c * 4099 + o * 97) ^ (ulong)_season * 104729UL);
        ApplyCasualties(outcome.Battle.AttackerDead, outcome.Battle.AttackerWounded);
        ApplyCasualties(outcome.Battle.DefenderDead, outcome.Battle.DefenderWounded);

        Fix64 loot = Fix64.Zero;
        if (outcome.Success)
        {
            loot = _economy.TakeGoods(e.ToSettlement, outcome.LootCapacity, LootPreference.PreservedFoodThenToolsThenFresh);
            _economy.GiveGoods(e.FromSettlement, loot * Fix64.FromRatio(8, 10));
            for (int i = 0; i < outcome.StructuresBurned; i++) _economy.BurnStructure(e.ToSettlement);
            RaidsSucceeded++;
        }
        bool atWar = _dip.AtWar(c, o);
        bool peaceful = _dip.Relations(c, o) > 0;
        bool deaths = outcome.Battle.DefenderDead.Count > 0;
        // Target Cohesion -10 (-5 with burial rites); the raiders +5 on success and -10 on failure (13 §5.4).
        _economy.AdjustCohesion(e.ToSettlement, Fix64.FromRatio(_research.IsDone(o, _p.Bel01) ? -5 : -10, 100));
        if (e.FromSettlement < _sCount) _economy.AdjustCohesion(e.FromSettlement, Fix64.FromRatio(outcome.Success ? 5 : -10, 100));
        _dip.Shift(c, o, deaths ? -50 : -30);
        if (peaceful && !atWar) _dip.AdjustReputation(c, -10);
        _dip.RecordRaid(c, o, _season);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.RaidResolved, o, outcome.Success ? 1 : 0, loot.FloorToInt()));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.RaidResolved, c, outcome.Success ? 2 : 3, loot.FloorToInt()));
    }

    private void ArriveAssault(Expedition e)
    {
        int c = e.Civ, o = e.TargetCiv;
        if (!TargetStillThere(e)) return;
        // Peace made while the army was on the road calls it off (docs/MVP/13 §8.1).
        if (!_dip.AtWar(c, o)) return;
        List<Fighter> attackers = AliveFighters(e, out _);
        if (attackers.Count < Captures.MinHolders) return;
        List<Fighter> defenders = Fighters(e.ToSettlement, attacking: false, out _);
        Defenses defenses = DefensesOf(e.ToSettlement);
        BattleResult result = Captures.Assault(attackers, defenders, defenses, childrenInside: true, _seed, AssaultStream ^ (ulong)(c * 4099 + o * 97) ^ (ulong)_season * 7907UL);
        ApplyCasualties(result.AttackerDead, result.AttackerWounded);
        ApplyCasualties(result.DefenderDead, result.DefenderWounded);
        bool won = Captures.CanHold(result);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.AssaultResolved, o, won ? 1 : 0, e.ToSettlement));
        _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.AssaultResolved, c, won ? 2 : 3, e.ToSettlement));
        if (!won)
        {
            _failedCampaigns[c * _civs + o]++;
            return;
        }
        var occupation = new Occupation { Civ = c, Settlement = e.ToSettlement, SinceSeason = _season,
            RequiredSeasons = Captures.OccupationSeasons(_sPop[e.ToSettlement]) };
        for (int i = 0; i < e.Members.Count; i++)
        {
            long id = e.Members[i];
            bool fought = false;
            foreach (Fighter f in attackers) if (f.Id == id) { fought = true; break; }
            // Survivors are the ones who fought and neither fell nor were hurt.
            if (fought && !result.AttackerDead.Contains(id) && !result.AttackerWounded.Contains(id))
            { occupation.Members.Add(id); occupation.Strengths.Add(e.Strengths[i]); }
        }
        _occupations.Add(occupation);
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.Occupied, o, occupation.Members.Count, e.ToSettlement));
    }

    // ---- occupation and capture -----------------------------------------------------------

    private void AdvanceOccupations()
    {
        var done = new List<Occupation>();
        foreach (Occupation occ in _occupations)
        {
            var holders = new List<Fighter>();
            for (int i = 0; i < occ.Members.Count; i++)
                if (_economy.TryFigure(occ.Members[i], out _) && !_economy.IsWounded(occ.Members[i]))
                    holders.Add(new Fighter(occ.Members[i], occ.Strengths[i]));
            int loser = _sCiv[occ.Settlement];
            if (holders.Count < Captures.MinHolders || _free[occ.Settlement])
            {
                _events.Add(new PoliticsEvent(_tick, (byte)occ.Civ, PoliticsEventKind.OccupationBroken, loser, occ.Settlement, 0));
                done.Add(occ);
                continue;
            }
            if (ReliefBreaksOccupation(occ, holders))
            {
                _events.Add(new PoliticsEvent(_tick, (byte)occ.Civ, PoliticsEventKind.OccupationBroken, loser, occ.Settlement, 1));
                done.Add(occ);
                continue;
            }
            if (_season - occ.SinceSeason >= occ.RequiredSeasons)
            {
                Capture(occ, loser);
                done.Add(occ);
            }
        }
        foreach (Occupation occ in done) _occupations.Remove(occ);
    }

    /// <summary>A relief force from the loser's nearby settlements fights the occupiers; if it breaks them the hold fails (13 §8.2).</summary>
    private bool ReliefBreaksOccupation(Occupation occ, List<Fighter> holders)
    {
        int loser = _sCiv[occ.Settlement];
        var relief = new List<Fighter>();
        for (int s = 0; s < _sCount; s++)
        {
            if (s == occ.Settlement || _sCiv[s] != loser || _free[s]) continue;
            if (HexGrid.Distance(_sTile[s], _sTile[occ.Settlement], _width) > 8) continue;
            List<Fighter> here = Fighters(s, attacking: true, out _);
            int allowed = Math.Max(0, Math.Min(here.Count - 2, here.Count * 6 / 10));
            for (int i = 0; i < allowed; i++) relief.Add(here[i]);
        }
        if (relief.Count == 0) return false;
        BattleResult result = Captures.Assault(relief, holders, Defenses.None, childrenInside: false, _seed,
            AssaultStream ^ 0x4C4945UL ^ (ulong)(occ.Settlement * 131) ^ (ulong)_season * 6151UL);
        ApplyCasualties(result.AttackerDead, result.AttackerWounded);
        ApplyCasualties(result.DefenderDead, result.DefenderWounded);
        return result.Winner == BattleWinner.Attacker;
    }

    /// <summary>
    /// The settlement changes hands (docs/MVP/13 §8.3): about half its people stay and join the captor; the rest walk to
    /// the loser's nearest settlement, or become Free Folk if there is none within reach. Assimilated people are
    /// unsettled until their Cohesion recovers.
    /// </summary>
    private void Capture(Occupation occ, int loser)
    {
        int s = occ.Settlement, captor = occ.Civ;
        var people = new List<FigureView>();
        _economy.ListFigures(s, people);
        int staying = AssimilatedShare(captor, loser, people.Count);
        // Who stays is drawn by lot, not by age or sex.
        Pcg32 rng = Pcg32.ForStream(_seed, CaptureStream, (ulong)s, _season);
        var order = new List<long>();
        foreach (FigureView p in people) order.Add(p.Id);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = (int)rng.NextBelow((uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
        var fleeing = new List<long>();
        for (int i = staying; i < order.Count; i++) fleeing.Add(order[i]);
        fleeing.Sort();

        int refuge = NearestRefuge(loser, s);
        if (fleeing.Count > 0)
        {
            if (refuge >= 0) foreach (long id in fleeing) _economy.RelocateFigure(id, refuge);
            else FoundFreeFolk(loser, s, fleeing);
        }
        _economy.TransferSettlement(s, captor);
        RefreshSettlements();
        // Taken people carry a Cohesion debt of 20 points that recovers at 2 a season (13 §8.3).
        Fix64 debt = Fix64.FromRatio(20, 100);
        _economy.AdjustCohesion(s, -debt);
        _cohesionDebt[s] = debt;
        _dip.Shift(captor, loser, -70);
        CaptureCount++;
        _events.Add(new PoliticsEvent(_tick, (byte)captor, PoliticsEventKind.Captured, loser, s, staying));
        _events.Add(new PoliticsEvent(_tick, (byte)loser, PoliticsEventKind.Captured, captor, s, -staying));
    }

    private int AssimilatedShare(int captor, int loser, int people)
    {
        int percent = 50;
        if (_research.IsDone(captor, _p.Gov03)) percent += 10;
        if (_dip.Relations(captor, loser) > Diplomacy.HostileBelow) percent += 10;
        if (_season - _dip.LastRaidBy(captor, loser) <= 8) percent -= 20;
        return people * Math.Clamp(percent, 0, 100) / 100;
    }

    private int NearestRefuge(int loser, int lost)
    {
        int best = -1, bestDistance = 16;
        for (int s = 0; s < _sCount; s++)
        {
            if (s == lost || _sCiv[s] != loser || _free[s]) continue;
            int d = HexGrid.Distance(_sTile[s], _sTile[lost], _width);
            if (d < bestDistance) { bestDistance = d; best = s; }
        }
        return best;
    }

    /// <summary>People with nowhere to go found a neutral camp near the lost settlement (13 §9).</summary>
    private void FoundFreeFolk(int loser, int lost, List<long> people)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int tile = 0; tile < _generated.TileCount; tile++)
        {
            WorldTile t = _generated.TileAt(tile);
            if (t.HabitableWeight == 0 || t.Biome == Biome.Mountain) continue;
            bool clear = true;
            for (int s = 0; s < _sCount && clear; s++)
                if (HexGrid.Distance(_sTile[s], tile, _width) < 7) clear = false;
            if (!clear) continue;
            int d = HexGrid.Distance(_sTile[lost], tile, _width);
            if (d < bestDistance) { bestDistance = d; best = tile; }
        }
        if (best < 0) return;
        int id = _economy.FoundSettlement(loser, best, people);
        RefreshSettlements();
        _free[id] = true;
        _events.Add(new PoliticsEvent(_tick, (byte)loser, PoliticsEventKind.FreeFolkFounded, -1, id, people.Count));
    }

    /// <summary>
    /// Puts the civilization's best fighters in a settlement it has just beaten, as if an assault had won (a scenario hook):
    /// the hold then runs its course and the settlement changes hands unless relief breaks it.
    /// </summary>
    public bool Occupy(int attacker, int settlement)
    {
        int origin = BestOrigin(attacker, _sTile[settlement]);
        if (origin < 0) return false;
        List<Fighter> party = PickParty(origin, attacker, 8, attacking: true, out List<long> ids, stayHome: 0);
        if (party.Count < Captures.MinHolders) return false;
        var occupation = new Occupation { Civ = attacker, Settlement = settlement, SinceSeason = _season,
            RequiredSeasons = Captures.OccupationSeasons(_sPop[settlement]) };
        for (int i = 0; i < ids.Count; i++) { occupation.Members.Add(ids[i]); occupation.Strengths.Add(party[i].Strength); }
        _occupations.Add(occupation);
        _events.Add(new PoliticsEvent(_tick, (byte)attacker, PoliticsEventKind.Occupied, _sCiv[settlement], occupation.Members.Count, settlement));
        return true;
    }

    /// <summary>Makes peace or war directly (a scenario hook; the Civ Mind decides in play).</summary>
    public void SetWar(int a, int b, bool war)
    {
        if (war && !_dip.AtWar(a, b))
        {
            _dip.DeclareWar(a, b);
            _warSince[a * _civs + b] = _season;
            _warSince[b * _civs + a] = _season;
            Wars++;
        }
        else if (!war && _dip.AtWar(a, b)) _dip.MakePeace(a, b, _season);
    }
}
