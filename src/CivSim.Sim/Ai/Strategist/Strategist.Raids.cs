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
        int bestRejectedCiv = -1;
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            RivalRead read = s.Rivals[o];
            RivalMemory m = _memory[o];
            if (!read.Contacted || p.Allied || (p.InTruce && !p.AtWar)) continue;
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
            if (local is null)
            {
                if (staleOnly > 0 && v.Season - m.LastRejectNote >= 4 && read.Sites.Count > 0)
                {
                    m.LastRejectNote = v.Season;
                    v.Note("RaidRejected", read.Sites[0].Settlement, o, Fix64.Zero,
                        $"no fresh sighting of civ {o} (newest {read.Sites[0].Stale} seasons old): do not raid blind, scout first");
                }
                continue;
            }
            if (local.Blocker.Length == 0 && (best is null || Better(local, best))) best = local;
            else if (local.Blocker.Length > 0 && (bestRejected is null || local.Value > bestRejected.Value)) { bestRejected = local; bestRejectedCiv = o; }
        }

        bool paid = best is not null;
        _raidStreak = paid ? _raidStreak + 1 : 0;
        string? postureBlock = v.Own.Posture is Posture.Fortify or Posture.Prosper ? $"Posture {v.Own.Posture} does not raid" : null;
        if (best is not null && postureBlock is null && s.Urgency >= StrategistTuning.FortifyAt && best.Civ != s.TopThreatCiv)
            postureBlock = $"civ {s.TopThreatCiv} threatens us; no losses to spare on civ {best.Civ}";
        if (best is not null && (postureBlock is not null || s.OrderedLaunch || v.Own.RaidsOnTheRoad > 0))
        {
            best.Blocker = postureBlock ?? "a party is already out";
            bestRejected = best;
            bestRejectedCiv = best.Civ;
            best = null;
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
