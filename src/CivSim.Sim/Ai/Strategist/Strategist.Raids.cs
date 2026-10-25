using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    private sealed class RaidCandidate
    {
        public int Civ, Settlement, Party, Stale, LandsSeason;
        public Fix64 P, Deaths, Loot, Value, OriginWinter;
        public bool Palisade, Watchers;
        public string Blocker = "";
        // What the candidate was judged from, so the choice can be played again with more trials before it is acted on.
        public Site Site = null!;
        public OwnSettlement Origin = null!;
        public RivalRead Read = null!;
        public PairView Pair = null!;
        public int Distance;
        public Fix64 Share;
    }

    private sealed class CaravanRaidCandidate
    {
        public int Civ, CaravanId, Party, Distance, LandsSeason;
        public Fix64 P, Deaths, Loot, Value, OriginWinter;
        public string Blocker = "";
        public BeliefCaravan Caravan = null!;
        public OwnSettlement Origin = null!;
        public RivalRead Read = null!;
        public PairView Pair = null!;
        public Fix64 Share;
    }

    /// <summary>
    /// The raid decision (14 §6.1): for the nearest fresh sightings of each rival, a Monte Carlo of the raid against the
    /// believed defenders and walls gives P(success), deaths and loot; the expected value in rations (13 §11 F4 ledger)
    /// subtracts lives, Relations, reputation and the chance of retaliation. A raid goes only with P ≥ the archetype's
    /// margin (never below 30%), value ≥ its margin, a sighting no older than two seasons, and food for the winter.
    /// </summary>
    private void DecideRaids(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        if (!Done(v, _war02) || v.Own.Settlements.Count == 0)
        {
            _raidStreak = 0;
            return;
        }
        int width = v.Belief.Map.Width;
        int trials = StrategistTuning.Trials(v.Profile);
        bool ranged = v.Own.RangedWeapon;
        RaidCandidate? best = null, bestRejected = null;
        CaravanRaidCandidate? bestCaravan = null;
        int bestRejectedCiv = -1;
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            RivalRead read = s.Rivals[o];
            RivalMemory m = _memory[o];
            if (!read.Contacted || p.Allied || p.NonAggressionPact || (p.InTruce && !p.AtWar)) continue;
            if (v.Season - m.LastRaidOrder < StrategistTuning.RaidCooldownSeasons) continue;
            RaidCandidate? local = null;
            int considered = 0, staleOnly = 0;
            foreach (Site site in read.Sites)
            {
                if (considered >= StrategistTuning.RaidTargetsConsidered) break;
                if (site.Stale > StrategistTuning.StaleSeasons) { staleOnly++; continue; }
                OwnSettlement? origin = BestOrigin(v, site.Tile, width);
                if (origin is null) continue;
                int distance = HexGrid.Distance(origin.Tile, site.Tile, width);
                if (distance > StrategistTuning.RaidRangeReaches * Math.Max(6, v.Own.ReachTiles)) continue;
                considered++;
                foreach (Fix64 share in StrategistTuning.RaidPartyShares)
                {
                    RaidCandidate c = EvaluateRaid(v, s, read, p, site, origin, distance, share, ranged, trials);
                    if (c.Party < 4) continue;
                    if (local is null || Better(c, local)) local = c;
                }
            }
            CaravanRaidCandidate? localCaravan = null;
            foreach (BeliefCaravan caravan in v.Belief.Rivals[o].CaravansSeen)
            {
                if (caravan.LastSeenSeason != v.Season || caravan.Civ != o || caravan.CrewEstimate < 1 ||
                    caravan.CargoBarterValue <= Fix64.Zero || caravan.ArrivalSeason <= v.Season) continue;
                OwnSettlement? origin = BestOrigin(v, caravan.Tile, width);
                if (origin is null) continue;
                int distance = HexGrid.Distance(origin.Tile, caravan.Tile, width);
                if (distance > StrategistTuning.RaidRangeReaches * Math.Max(6, v.Own.ReachTiles) ||
                    distance > PoliticsSystem.TilesPerSeason * (caravan.ArrivalSeason - v.Season)) continue;
                foreach (Fix64 share in StrategistTuning.RaidPartyShares)
                {
                    CaravanRaidCandidate c = EvaluateCaravanRaid(v, s, read, p, caravan, origin, distance, share, ranged, trials);
                    if (c.Party < 4) continue;
                    if (localCaravan is null || Better(c, localCaravan)) localCaravan = c;
                }
            }
            if (local is null)
            {
                if (staleOnly > 0 && v.Season - m.LastRejectNote >= 4 && read.Sites.Count > 0)
                {
                    m.LastRejectNote = v.Season;
                    v.Note("RaidRejected", read.Sites[0].Settlement, o, Fix64.Zero,
                        $"no fresh sighting of civ {o} (newest {read.Sites[0].Stale} seasons old): do not raid blind, scout first");
                }
            }
            if (localCaravan is not null && (bestCaravan is null || Better(localCaravan, bestCaravan))) bestCaravan = localCaravan;
            if (local is null)
            {
                continue;
            }
            if (local.Blocker.Length == 0 && (best is null || Better(local, best))) best = local;
            else if (local.Blocker.Length > 0 && (bestRejected is null || local.Value > bestRejected.Value)) { bestRejected = local; bestRejectedCiv = o; }
        }

        bool chooseCaravan = bestCaravan is { Blocker.Length: 0 } && (best is null || bestCaravan.Value > best.Value);
        bool paid = best is not null || chooseCaravan;
        _raidStreak = paid ? _raidStreak + 1 : 0;
        string? postureBlock = v.Own.Posture is Posture.Fortify or Posture.Prosper ? $"Posture {v.Own.Posture} does not raid" : null;
        int candidateCiv = chooseCaravan ? bestCaravan!.Civ : best?.Civ ?? -1;
        if (candidateCiv >= 0 && postureBlock is null && s.Urgency >= StrategistTuning.FortifyAt && candidateCiv != s.TopThreatCiv)
            postureBlock = $"civ {s.TopThreatCiv} threatens us; no losses to spare on civ {candidateCiv}";
        if (best is not null && (postureBlock is not null || s.OrderedLaunch || v.Own.RaidsOnTheRoad > 0))
        {
            best.Blocker = postureBlock ?? "a party is already out";
            bestRejected = best;
            bestRejectedCiv = best.Civ;
            best = null;
        }
        if (chooseCaravan && (postureBlock is not null || s.OrderedLaunch || v.Own.RaidsOnTheRoad > 0))
        {
            bestCaravan!.Blocker = postureBlock ?? "a party is already out";
            chooseCaravan = false;
        }
        if (chooseCaravan)
        {
            CaravanRaidCandidate sure = EvaluateCaravanRaid(v, s, bestCaravan!.Read, bestCaravan.Pair, bestCaravan.Caravan,
                bestCaravan.Origin, bestCaravan.Distance, bestCaravan.Share, ranged, StrategistTuning.RaidConfirmTrials, confirm: true);
            if (sure.Blocker.Length > 0)
            {
                if (v.Season - _memory[sure.Civ].LastRejectNote >= 4)
                {
                    _memory[sure.Civ].LastRejectNote = v.Season;
                    v.Note("CaravanRaidRejected", sure.CaravanId, sure.Civ, sure.P, CaravanRaidReason(sure, "rejected: " + sure.Blocker));
                }
                return;
            }
            if (act.RaidCaravan(sure.CaravanId, sure.Party))
            {
                s.OrderedLaunch = true;
                _memory[sure.Civ].LastRaidOrder = v.Season;
                v.Note("CaravanRaid", sure.CaravanId, sure.Civ, sure.P, CaravanRaidReason(sure, "intercept"));
                return;
            }
            // The existing order port rechecks ownership, alliance, pact and path legality against live state. If any
            // changed since the visible sighting, retain the safer settlement candidate from this same decision pass.
            bestCaravan!.Blocker = "the caravan order failed its live legality checks";
            chooseCaravan = false;
        }
        if (best is not null)
        {
            // A few trials per candidate, tried every season for every village, will throw up a lucky 0.7 for a raid that is
            // really 0.2 (the best of many noisy estimates is biased high). Before sending anyone, play the chosen raid again
            // with many more trials on a fresh stream and hold it to the same margins.
            RaidCandidate sure = EvaluateRaid(v, s, best.Read, best.Pair, best.Site, best.Origin, best.Distance, best.Share, ranged,
                StrategistTuning.RaidConfirmTrials, confirm: true);
            if (sure.Blocker.Length > 0)
            {
                sure.Blocker = "on a second look: " + sure.Blocker;
                if (v.Season - _memory[sure.Civ].LastRejectNote >= 4)
                {
                    _memory[sure.Civ].LastRejectNote = v.Season;
                    v.Note("RaidRejected", sure.Settlement, sure.Civ, sure.P, RaidReason(sure, "rejected: " + sure.Blocker));
                }
                return;
            }
            best = sure;
            if (act.Raid(best.Settlement, best.Party))
            {
                s.OrderedLaunch = true;
                _memory[best.Civ].LastRaidOrder = v.Season;
                v.Note("Raid", best.Settlement, best.Civ, best.P, RaidReason(best, "raid"));
            }
            return;
        }
        if (bestRejected is not null && v.Season - _memory[bestRejectedCiv].LastRejectNote >= 4)
        {
            _memory[bestRejectedCiv].LastRejectNote = v.Season;
            v.Note("RaidRejected", bestRejected.Settlement, bestRejectedCiv, bestRejected.P, RaidReason(bestRejected, "rejected: " + bestRejected.Blocker));
        }
        else if (!chooseCaravan && bestCaravan is { Blocker.Length: > 0 } &&
                 v.Season - _memory[bestCaravan.Civ].LastRejectNote >= 4)
        {
            _memory[bestCaravan.Civ].LastRejectNote = v.Season;
            v.Note("CaravanRaidRejected", bestCaravan.CaravanId, bestCaravan.Civ, bestCaravan.P,
                CaravanRaidReason(bestCaravan, "rejected: " + bestCaravan.Blocker));
        }
    }

    private static bool Better(CaravanRaidCandidate a, CaravanRaidCandidate b)
    {
        bool aOk = a.Blocker.Length == 0, bOk = b.Blocker.Length == 0;
        if (aOk != bOk) return aOk;
        return a.Value > b.Value;
    }

    private static string CaravanRaidReason(CaravanRaidCandidate c, string what)
        => $"p={F2(c.P)} ev={F1(c.Value)} lw={F2(c.OriginWinter)} {what} on caravan {c.CaravanId} of civ {c.Civ} " +
           $"with {c.Party}: success {F2(c.P)}, cargo value {F1(c.Loot)}, deaths {F2(c.Deaths)}";

    private CaravanRaidCandidate EvaluateCaravanRaid(StrategistView v, SeasonRead s, RivalRead read, PairView p,
        BeliefCaravan caravan, OwnSettlement origin, int distance, Fix64 share, bool ranged, int trials, bool confirm = false)
    {
        var c = new CaravanRaidCandidate
        {
            Caravan = caravan, Origin = origin, Read = read, Pair = p, Distance = distance, Share = share,
            Civ = read.Civ, CaravanId = caravan.Id, OriginWinter = origin.LateWinterForecast,
        };
        int spare = origin.Fighters - 4;
        c.Party = Math.Min(spare, Math.Max(4, (Fix64.FromInt(origin.Fighters) * share).FloorToInt() + 1));
        if (c.Party < 4) { c.Blocker = "fewer than four fighters can leave home"; return c; }

        Fix64 each = origin.AttackingStrength / Math.Max(1, origin.Fighters) * StrategistTuning.PartyPickBonus;
        Uniform(_a, c.Party, each, 1_000_000);
        Fix64 defenderStrength = v.Belief.Rivals[read.Civ].WeaponStrength;
        if (defenderStrength <= Fix64.Zero) defenderStrength = Fix64.FromRatio(6, 10);
        Uniform(_b, caravan.CrewEstimate, defenderStrength, 0);
        ulong stream = Hash64.Combine(Hash64.Combine(RaidStream, (ulong)(v.Civ * 64 + read.Civ)),
            (ulong)(caravan.Id * 16 + c.Party));
        if (confirm) stream = Hash64.Combine(stream, ConfirmStream);
        (Fix64 simulatedP, c.Deaths, _, Fix64 defenderDeaths) = SimulateRaid(_a, _b, Defenses.None, ranged,
            v.MatchSeed, stream, v.Season, trials);
        c.P = StrategistTuning.CalibrateRaidSuccess(simulatedP);
        c.Loot = caravan.CargoBarterValue;
        int seasons = Math.Max(1, (distance + PoliticsSystem.TilesPerSeason - 1) / PoliticsSystem.TilesPerSeason);
        c.LandsSeason = (v.Season + seasons - 1) % 4;
        bool atWar = p.AtWar;
        Fix64 relations = atWar || p.Relations < Diplomacy.HostileBelow ? Fix64.Zero : StrategistTuning.RaidRelationsCost;
        Fix64 reputation = !atWar && p.Relations > 0 ? StrategistTuning.RaidReputationCost : Fix64.Zero;
        int after = p.Relations - (defenderDeaths >= Fix64.Half ? 50 : 30);
        Fix64 answer = atWar || after < Diplomacy.HostileBelow ? StrategistTuning.RetaliationHostile : StrategistTuning.RetaliationOther;
        Fix64 capability = read.Capable ? Fix64.One : StrategistTuning.RetaliationUnarmed;
        c.Value = c.P * c.Loot * StrategistTuning.LootDelivered - c.Deaths * StrategistTuning.LifeValue -
                  relations - reputation - answer * capability * read.ExpectedLoss;

        string? counterExposure = null;
        IReadOnlyList<SettlementDeparture> departures = RaidDepartures(origin, c.Party);
        for (int other = 0; other < _civs; other++)
        {
            if (other == v.Civ) continue;
            PairView relation = v.Own.Others[other];
            RivalRead rival = s.Rivals[other];
            RivalBelief belief = v.Belief.Rivals[other];
            bool credibleCounterThreat = relation.AtWar || rival.EffectiveThreat >= StrategistTuning.ThreatNoticeAt;
            if (!credibleCounterThreat || !relation.Contacted || relation.Allied || (relation.InTruce && !relation.AtWar) ||
                rival.Sites.Count == 0 || (!rival.Capable && belief.Era < 2)) continue;
            (Fix64 army, OwnSettlement? exposed) = CounterThreat(v, rival, v.Belief.Map.Width, departures);
            if (exposed is null) continue;
            counterExposure = $"civ {other}'s field army ({F1(army)}) could take settlement {exposed.Index} after the raid party leaves";
            break;
        }

        if (!StrategistTuning.MeetsRaidSuccessThreshold(c.P, _minRaidSuccess))
            c.Blocker = $"believed success {F2(c.P)} below {F2(_minRaidSuccess)}";
        else if (c.Value < _minRaidValue) c.Blocker = $"expected value {F1(c.Value)} below the margin {F1(_minRaidValue)}";
        else if (v.Season - caravan.LastSeenSeason != 0) c.Blocker = "caravan sighting is stale";
        else if (caravan.ArrivalSeason <= v.Season || distance > PoliticsSystem.TilesPerSeason * (caravan.ArrivalSeason - v.Season))
            c.Blocker = "caravan is outside the intercept window";
        else if (distance > StrategistTuning.RaidRangeReaches * Math.Max(6, v.Own.ReachTiles))
            c.Blocker = "caravan is outside raid range";
        else if (c.LandsSeason >= 2 && origin.LateWinterForecast < StrategistTuning.WinterFoodFloor)
            c.Blocker = "raid lands in autumn or winter and our late-winter food is low";
        else if (counterExposure is not null) c.Blocker = counterExposure;
        else if (p.Allied || p.InTruce) c.Blocker = "alliance or truce bars the caravan raid";
        return c;
    }

    private static bool Better(RaidCandidate a, RaidCandidate b)
    {
        bool aOk = a.Blocker.Length == 0, bOk = b.Blocker.Length == 0;
        if (aOk != bOk) return aOk;
        return a.Value > b.Value;
    }

    private static string RaidReason(RaidCandidate c, string what)
        => $"p={F2(c.P)} ev={F1(c.Value)} lw={F2(c.OriginWinter)} cold={(c.LandsSeason >= 2 ? 1 : 0)} stale={c.Stale} " +
           $"{what} on settlement {c.Settlement} of civ {c.Civ} with {c.Party}: success {F2(c.P)}, loot {F1(c.Loot)}, deaths {F2(c.Deaths)}" +
           $"{(c.Palisade ? ", palisade seen" : "")}{(c.Watchers ? ", watchers seen" : "")}";

    private RaidCandidate EvaluateRaid(StrategistView v, SeasonRead s, RivalRead read, PairView p, Site site, OwnSettlement origin, int distance,
        Fix64 share, bool ranged, int trials, bool confirm = false)
    {
        var c = new RaidCandidate
        {
            Site = site, Origin = origin, Read = read, Pair = p, Distance = distance, Share = share,
            Civ = read.Civ, Settlement = site.Settlement, Stale = site.Stale, Palisade = site.Defenses.Palisade,
            Watchers = site.Defenses.Watchers, OriginWinter = origin.LateWinterForecast,
        };
        int spare = origin.Fighters - 4;
        int party = Math.Min(spare, Math.Max(4, (Fix64.FromInt(origin.Fighters) * share).FloorToInt() + 1));
        c.Party = party;
        if (party < 4) return c;
        Fix64 each = origin.AttackingStrength / Math.Max(1, origin.Fighters) * StrategistTuning.PartyPickBonus;
        Uniform(_a, party, each, 1_000_000);
        Uniform(_b, site.Fighters, site.PerDefend, 0);
        ulong stream = Hash64.Combine(Hash64.Combine(RaidStream, (ulong)(v.Civ * 64 + read.Civ)), (ulong)(site.Settlement * 16 + party));
        if (confirm) stream = Hash64.Combine(stream, ConfirmStream);
        (Fix64 simulatedP, c.Deaths, Fix64 carriers, Fix64 defDeaths) = SimulateRaid(_a, _b, site.Defenses, ranged, v.MatchSeed, stream, v.Season, trials);
        c.P = StrategistTuning.CalibrateRaidSuccess(simulatedP);

        int seasons = Math.Max(1, (distance + PoliticsSystem.TilesPerSeason - 1) / PoliticsSystem.TilesPerSeason);
        c.LandsSeason = (v.Season + seasons - 1) % 4;
        Fix64 stores = StrategistTuning.StoresPerHead * Math.Max(0, site.Population) * StrategistTuning.SeasonalStores[c.LandsSeason];
        c.Loot = Fix64.Min(Raids.CarryPerRaider * carriers, stores) * StrategistTuning.LootDelivered;

        bool atWar = p.AtWar;
        // Souring a neighbour who is not yet hostile is the ledger's Relations cost (13 §11 F4); with one already hostile
        // the cost is the retaliation below.
        Fix64 relations = atWar || p.Relations < Diplomacy.HostileBelow ? Fix64.Zero : StrategistTuning.RaidRelationsCost;
        Fix64 reputation = !atWar && p.Relations > 0 ? StrategistTuning.RaidReputationCost : Fix64.Zero;
        int after = p.Relations - (defDeaths >= Fix64.Half ? 50 : 30);
        Fix64 answer = atWar || after < Diplomacy.HostileBelow ? StrategistTuning.RetaliationHostile : StrategistTuning.RetaliationOther;
        // Without Raiding they cannot answer in kind until they research it.
        Fix64 capability = read.Capable ? Fix64.One : StrategistTuning.RetaliationUnarmed;
        Fix64 retaliation = answer * capability * read.ExpectedLoss;
        c.Value = c.P * c.Loot - c.Deaths * StrategistTuning.LifeValue - relations - reputation - retaliation;

        string? counterExposure = null;
        IReadOnlyList<SettlementDeparture> departures = RaidDepartures(origin, party);
        for (int other = 0; other < _civs; other++)
        {
            if (other == v.Civ) continue;
            PairView relation = v.Own.Others[other];
            RivalRead rival = s.Rivals[other];
            RivalBelief belief = v.Belief.Rivals[other];
            bool credibleCounterThreat = relation.AtWar || rival.EffectiveThreat >= StrategistTuning.ThreatNoticeAt;
            if (!credibleCounterThreat || !relation.Contacted || relation.Allied || (relation.InTruce && !relation.AtWar) || rival.Sites.Count == 0 ||
                (!rival.Capable && belief.Era < 2)) continue;
            (Fix64 army, OwnSettlement? exposed) = CounterThreat(v, rival, v.Belief.Map.Width, departures);
            if (exposed is null) continue;
            counterExposure = $"civ {other}'s field army ({F1(army)}) could take settlement {exposed.Index} after the raid party leaves";
            break;
        }

        if (!StrategistTuning.MeetsRaidSuccessThreshold(c.P, _minRaidSuccess)) c.Blocker = $"believed success {F2(c.P)} below {F2(_minRaidSuccess)}";
        else if (c.Value < _minRaidValue) c.Blocker = $"expected value {F1(c.Value)} below the margin {F1(_minRaidValue)}";
        else if (c.LandsSeason >= 2 && origin.LateWinterForecast < StrategistTuning.WinterFoodFloor)
            c.Blocker = $"lands in {(c.LandsSeason == 2 ? "autumn" : "winter")} and our late-winter food is {F2(origin.LateWinterForecast)}";
        else if (counterExposure is not null) c.Blocker = counterExposure;
        return c;
    }
}
