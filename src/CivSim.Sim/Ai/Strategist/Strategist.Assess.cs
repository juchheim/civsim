using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    private static readonly ulong ThreatStream = Hash64.OfString("strategist.threat");
    private static readonly ulong RaidStream = Hash64.OfString("strategist.raid");
    private static readonly ulong ConfirmStream = Hash64.OfString("strategist.raid.confirm");
    private static readonly ulong AssaultStream = Hash64.OfString("strategist.assault");

    private readonly List<Fighter> _a = new(), _b = new();
    private bool[] _visited = Array.Empty<bool>();

    // ---- reading the rivals (14 §3.1, §5.4) -------------------------------------------------

    private void ReadRivals(StrategistView v, SeasonRead s)
    {
        int width = v.Belief.Map.Width;
        int trials = StrategistTuning.Trials(v.Profile);
        Fix64 bestThreat = Fix64.Zero;
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            RivalBelief r = v.Belief.Rivals[o];
            PairView p = v.Own.Others[o];
            RivalRead read = s.Rivals[o];
            RivalMemory m = _memory[o];
            read.Known = r.Aware || r.Contacted || p.Aware || p.Contacted;
            read.Contacted = p.Contacted || r.Contacted;
            read.Reach = ReachOfEra(r.Era);
            if (read.Known)
            {
                ReadSites(v, r, p, read, width);
                ReadThreat(v, r, p, read, m, width, trials);
            }
            read.Threat = Fix64.Max(read.Threat, m.SmoothedThreat * StrategistTuning.ThreatFade);
            read.EffectiveThreat = v.Profile.ReactionDelaySeasons > 0
                ? m.ThreatAsOf(v.Season, v.Profile.ReactionDelaySeasons)
                : read.Threat;
            if (read.EffectiveThreat > bestThreat) { bestThreat = read.EffectiveThreat; s.TopThreatCiv = o; }
        }
        s.Urgency = Fix64.Clamp(bestThreat * StrategistTuning.UrgencyScale, Fix64.Zero, Fix64.One);
    }

    private void ReadSites(StrategistView v, RivalBelief r, PairView p, RivalRead read, int width)
    {
        Fix64 weapon = r.WeaponStrength.Raw > 0 ? r.WeaponStrength : Fix64.FromRatio(6, 10);
        bool raidedRecently = v.Season - p.LastRaidOnThemSeason <= 4;
        foreach (BeliefSettlement b in r.Settlements)
        {
            int stale = Math.Max(0, v.Season - b.LastSeenSeason);
            Fix64 fresh = Fix64.Clamp(Fix64.One - StrategistTuning.FreshnessDecay * stale, Fix64.Zero, Fix64.One);
            // Pessimistic head count: the ±25% band, and more the older the sighting.
            Fix64 grow = StrategistTuning.BeliefPessimism * (Fix64.One + StrategistTuning.StaleGrowth * (Fix64.One - fresh));
            int fighters = Math.Max(b.FightersEstimate, 0);
            // Below Full the village's fighters and strength are not seen, only its head count: assume a share of the people fight
            // and that each is as strong as ours (times 0.6, measured), rather than reading "no defenders" and a certain failure.
            bool blind = v.Profile.Opponent < OpponentModel.Full && fighters == 0 && b.PopulationEstimate > 0;
            if (blind)
                fighters = (Fix64.FromInt(b.PopulationEstimate) * StrategistTuning.UnseenFighterShare - Fix64.FromRatio(1, 1000)).FloorToInt() + 1;
            int counted = fighters == 0 ? 0 : (Fix64.FromInt(fighters) * grow - Fix64.FromRatio(1, 1000)).FloorToInt() + 1;
            Fix64 perDefend = b.DefendingStrengthEstimate > Fix64.Zero && fighters > 0
                ? b.DefendingStrengthEstimate / fighters
                : Fix64.FromRatio(6, 10) * weapon;
            if (blind)
            {
                Fix64 ownStrength = Fix64.Zero;
                int ownFighters = 0;
                foreach (OwnSettlement own in v.Own.Settlements) { ownStrength += own.AttackingStrength; ownFighters += own.Fighters; }
                if (ownFighters > 0) perDefend = ownStrength / ownFighters * StrategistTuning.UnseenDefenderStrength;
            }
            // A blind observer cannot see works, so use the era prior. A fresh detailed sighting overrides the rival-wide
            // palisade technology inference: DEF-03 unlocks construction, but each settlement must pay and build one.
            bool typicalWorks = blind && r.Era >= StrategistTuning.UnseenWorksEra;
            bool typicalPalisade = blind && r.Era >= StrategistTuning.UnseenPalisadeEra;
            bool freshDetailedSite = !blind && stale == 0;
            var defenses = new Defenses(
                ThornBarrier: b.ThornBarrierSeen || typicalWorks,
                Palisade: b.PalisadeSeen || (!freshDetailedSite && Believed(r, _def03)) || typicalPalisade,
                Watchers: b.WatchersSeen || Believed(r, _def02) || typicalWorks,
                Dogs: Believed(r, _hus01) || typicalWorks, SignalFires: Believed(r, _com04) || typicalPalisade,
                AlarmCalls: Believed(r, _com01), HillSite: false,
                Archery: Believed(r, _war03) || typicalPalisade, Night: false, RaidedRecently: raidedRecently,
                Hillfort: b.HillfortSeen, StoneWalls: b.StoneWallsSeen, BronzeRules: r.Era >= 3);
            var site = new Site
            {
                Settlement = b.Settlement, Tile = b.Tile, Stale = stale, Population = b.PopulationEstimate, Fighters = counted,
                Fresh = fresh, PerDefend = perDefend, PerAttack = weapon * (b.WarriorsSeen > 0 || r.WarriorsSeen ? Fix64.FromRatio(11, 10) : Fix64.One),
                Defenses = defenses, Distress = b.DistressSeen,
                PalisadeInferred = !b.PalisadeSeen && ((!freshDetailedSite && Believed(r, _def03)) || typicalPalisade),
            };
            read.Sites.Add(site);
            read.Field += site.PerAttack * site.Fighters;
            foreach (OwnSettlement own in v.Own.Settlements)
                read.Distance = Math.Min(read.Distance, HexGrid.Distance(own.Tile, b.Tile, width));
        }
        if (read.Sites.Count == 0 && r.PopulationHigh > 0)
            // Known to exist but not where: a third of the people can fight.
            read.Field = weapon * (r.PopulationHigh * 3 / 10);
        // Nearest first, ties by settlement id.
        read.Sites.Sort((x, y) =>
        {
            int dx = NearestOwnDistance(v, x.Tile, width), dy = NearestOwnDistance(v, y.Tile, width);
            return dx != dy ? dx.CompareTo(dy) : x.Settlement.CompareTo(y.Settlement);
        });
    }

    private static int NearestOwnDistance(StrategistView v, int tile, int width)
    {
        int best = int.MaxValue;
        foreach (OwnSettlement o in v.Own.Settlements) best = Math.Min(best, HexGrid.Distance(o.Tile, tile, width));
        return best;
    }

    /// <summary>Threat(rival → me) = P(it attacks within the horizon) × P(its raid succeeds on our most exposed settlement) (14 §5.4).</summary>
    private void ReadThreat(StrategistView v, RivalBelief r, PairView p, RivalRead read, RivalMemory m, int width, int trials)
    {
        // Capability: Raiding believed, likely in E2+, else unlikely.
        Fix64 capability = Believed(r, _war02) ? StrategistTuning.CapabilityKnown
            : r.Era >= 2 ? StrategistTuning.CapabilityLikely : StrategistTuning.CapabilityUnlikely;
        read.Capable = Believed(r, _war02);
        // Intent from Relations and pacts (13 §3.2, §8.1).
        Fix64 intent;
        if (p.AtWar) intent = StrategistTuning.IntentAtWar;
        else if (p.Relations < Diplomacy.HostileBelow) intent = StrategistTuning.IntentHostile;
        else if (p.Relations < 0) intent = StrategistTuning.IntentUneasy;
        else if (p.Relations < 20) intent = StrategistTuning.IntentNeutral;
        else intent = StrategistTuning.IntentFriendly;
        if (!p.AtWar && (p.InTruce || p.Allied)) intent = intent * StrategistTuning.PactFactor;
        if (v.Season - p.LastRaidOnUsSeason <= StrategistTuning.RecentRaidSeasons) intent += StrategistTuning.RecentRaidBonus;
        // Opponent model (14 §9.2): Settler treats rivals as average; Chieftain reads the archetype posterior; Warlord and
        // Legend also read behaviour (warpaint, hunger, warriors).
        Fix64 pWarlord = Fix64.FromRatio(1, 5);
        if (v.Profile.Opponent != OpponentModel.None && r.ArchetypePosterior.Length >= 5)
        {
            Fix64 sum = Fix64.Zero;
            foreach (Fix64 x in r.ArchetypePosterior) sum += x;
            if (sum > Fix64.Zero) pWarlord = r.ArchetypePosterior[3] / sum;
        }
        read.PWarlord = pWarlord;
        Fix64 archetype = StrategistTuning.ArchetypeBase + StrategistTuning.ArchetypeWarlordSlope * pWarlord;
        if (v.Profile.Opponent is OpponentModel.Full or OpponentModel.FullCoalition)
        {
            if (r.RaidPostureSeen) intent += StrategistTuning.RaidPostureBonus;
            foreach (Site site in read.Sites) if (site.Distress) { intent += StrategistTuning.DistressBonus; break; }
            if (r.WarriorsSeen) intent += StrategistTuning.WarriorsBonus;
        }
        Fix64 range = Fix64.One;
        int reachLimit = StrategistTuning.RaidRangeReaches * read.Reach;
        if (read.Distance != int.MaxValue ? read.Distance > reachLimit : !read.Contacted) range = StrategistTuning.OutOfRangeFactor;
        read.ThreatP = Fix64.Clamp(intent * capability * archetype * range, Fix64.Zero, Fix64.FromRatio(95, 100));

        // The raid they could send: 30% of their strongest believed settlement's fighters, never leaving fewer than four.
        int theirFighters = 0, originTile = -1;
        Fix64 perAttack = r.WeaponStrength.Raw > 0 ? r.WeaponStrength : Fix64.FromRatio(6, 10);
        foreach (Site site in read.Sites)
            if (site.Fighters > theirFighters) { theirFighters = site.Fighters; originTile = site.Tile; perAttack = site.PerAttack; }
        if (read.Sites.Count == 0 && r.PopulationHigh > 0) theirFighters = r.PopulationHigh * 3 / 10;
        int party = Math.Min(Math.Max(4, (Fix64.FromInt(theirFighters) * StrategistTuning.ThreatPartyShare).FloorToInt() + 1), theirFighters - 4);
        if (party < 4 || v.Own.Settlements.Count == 0) return;

        // Our most exposed settlement: the nearest to them and the weakest within their range; the worse of the two counts.
        OwnSettlement? nearest = null, weakest = null;
        int nearestD = int.MaxValue;
        Fix64 weakestD = Fix64.MaxValue;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            int d = originTile >= 0 ? HexGrid.Distance(o.Tile, originTile, width) : 0;
            if (d < nearestD) { nearestD = d; nearest = o; }
            Fix64 strength = o.DefendingStrength * o.Defenses.Multiplier();
            if (d <= reachLimit && strength < weakestD) { weakestD = strength; weakest = o; }
        }
        bool ranged = r.RangedSeen;
        Fix64 worstP = Fix64.Zero, worstLoss = Fix64.Zero;
        for (int pass = 0; pass < 2; pass++)
        {
            OwnSettlement? target = pass == 0 ? nearest : weakest;
            if (target is null || (pass == 1 && ReferenceEquals(target, nearest))) continue;
            Uniform(_a, party, perAttack, 1_000_000);
            Uniform(_b, target.Fighters, target.Fighters > 0 ? target.DefendingStrength / target.Fighters : Fix64.Zero, 0);
            ulong stream = Hash64.Combine(ThreatStream, (ulong)(v.Civ * 64 + r.Civ)) ^ (ulong)target.Index * 0x9E37UL;
            (Fix64 pSuccess, _, _, Fix64 defDeaths) = SimulateRaid(_a, _b, target.Defenses, ranged, v.MatchSeed, stream, v.Season, trials);
            Fix64 loot = Fix64.Min(StrategistTuning.ExposedLoot, Fix64.Max(target.FoodStores, Fix64.Zero)) * pSuccess;
            Fix64 loss = loot + defDeaths * StrategistTuning.LifeValue;
            if (pSuccess > worstP) worstP = pSuccess;
            if (loss > worstLoss) worstLoss = loss;
        }
        read.PAgainst = worstP;
        read.ExpectedLoss = worstLoss;
        read.Threat = read.ThreatP * read.PAgainst;
    }

    // ---- Monte Carlo on synthetic armies (13 §5, §8; CombatTables shows the method) --------

    private static void Uniform(List<Fighter> list, int count, Fix64 strength, long idBase)
    {
        list.Clear();
        for (int i = 0; i < count; i++) list.Add(new Fighter(idBase + i, strength));
    }

    /// <summary>Runs <paramref name="trials"/> raids: success share, mean raider deaths, mean unhurt raiders on success, mean defender deaths.</summary>
    private static (Fix64 P, Fix64 Deaths, Fix64 Carriers, Fix64 DefenderDeaths) SimulateRaid(List<Fighter> raiders, List<Fighter> defenders,
        in Defenses defenses, bool ranged, ulong seed, ulong stream, int season, int trials)
    {
        if (raiders.Count == 0) return (Fix64.Zero, Fix64.Zero, Fix64.Zero, Fix64.Zero);
        int wins = 0, deaths = 0, carriers = 0, defDeaths = 0;
        ulong baseStream = Hash64.Combine(stream, (ulong)season);
        for (int t = 0; t < trials; t++)
        {
            RaidOutcome o = Raids.Resolve(raiders, defenders, defenses, ranged, seed, unchecked(baseStream + (ulong)t));
            if (o.Success) { wins++; carriers += o.Battle.AttackerSurvivors; }
            deaths += o.Battle.AttackerDead.Count;
            defDeaths += o.Battle.DefenderDead.Count;
        }
        return (Fix64.FromRatio(wins, trials), Fix64.FromRatio(deaths, trials),
            wins == 0 ? Fix64.Zero : Fix64.FromRatio(carriers, wins), Fix64.FromRatio(defDeaths, trials));
    }

    /// <summary>The settlement a party leaves from (the rule PoliticsSystem uses): the nearest that can spare eight adults, else the fullest.</summary>
    private static OwnSettlement? BestOrigin(StrategistView v, int targetTile, int width)
    {
        OwnSettlement? best = null, fullest = null;
        int bestD = int.MaxValue, most = -1;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            if (o.Adults > most) { most = o.Adults; fullest = o; }
            int d = HexGrid.Distance(o.Tile, targetTile, width);
            if (o.Adults >= 8 && d < bestD) { bestD = d; best = o; }
        }
        return best ?? fullest;
    }

    // ---- the frontier (14 §4.5, §8.4) -------------------------------------------------------

    /// <summary>
    /// Share of the explored habitable land within two reaches of our settlements that nobody owned when last seen. Until
    /// enough of it has been read, the E0-E3 prior fills the gap: the frontier race is the default (14 §3.3).
    /// </summary>
    private Fix64 FrontierValue(StrategistView v)
    {
        MapKnowledge map = v.Belief.Map;
        if (_visited.Length != map.TileCount) _visited = new bool[map.TileCount];
        else Array.Clear(_visited);
        int radius = Math.Clamp(2 * Math.Max(v.Own.ReachTiles, 6), 6, 24);
        int total = 0, open = 0;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            int ox = o.Tile % map.Width, oy = o.Tile / map.Width;
            for (int y = Math.Max(0, oy - radius); y <= Math.Min(map.Height - 1, oy + radius); y++)
                for (int x = Math.Max(0, ox - radius - 1); x <= Math.Min(map.Width - 1, ox + radius + 1); x++)
                {
                    int tile = y * map.Width + x;
                    if (_visited[tile]) continue;
                    int d = HexGrid.Distance(o.Tile, tile, map.Width);
                    if (d < 3 || d > radius) continue;
                    _visited[tile] = true;
                    if (!map.TryTile(tile, out WorldTile t) || t.HabitableWeight == 0) continue;
                    total++;
                    if (map.OwnerSeen(tile).Owner < 0) open++;
                }
        }
        Fix64 prior = v.Own.Era <= 3 ? StrategistTuning.FrontierPrior : Fix64.FromRatio(40, 100);
        int min = StrategistTuning.FrontierMinSamples;
        if (total >= min) return Fix64.FromRatio(open, total);
        return (Fix64.FromInt(open) + prior * (min - total)) / min;
    }

    // ---- balance of power (14 §7.3) -----------------------------------------------------------

    private void ReadLeader(StrategistView v, SeasonRead s)
    {
        MapKnowledge map = v.Belief.Map;
        for (int tile = 0; tile < map.TileCount; tile++)
        {
            if (!map.IsExplored(tile)) continue;
            int owner = map.OwnerSeen(tile).Owner;
            if (owner >= 0 && owner < _civs && owner != v.Civ) s.Rivals[owner].LandSeen++;
        }
        int seenRivals = 0, leader = v.Civ, leaderLand = v.Own.ClaimedTiles, total = v.Own.ClaimedTiles;
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            int land = s.Rivals[o].LandSeen;
            if (land > 0 && s.Rivals[o].Contacted) seenRivals++;
            total += land;
            if (land > leaderLand) { leaderLand = land; leader = o; }
        }
        if (leader == v.Civ || seenRivals < 2 || total == 0) return;
        int second = leader == v.Civ ? 0 : v.Own.ClaimedTiles;
        for (int o = 0; o < _civs; o++) if (o != v.Civ && o != leader) second = Math.Max(second, s.Rivals[o].LandSeen);
        Fix64 lead = second == 0 ? Fix64.FromInt(2) : Fix64.FromRatio(leaderLand, second);
        Fix64 share = Fix64.FromRatio(leaderLand, total);
        bool allSeen = seenRivals == _civs - 1;
        if (lead < StrategistTuning.LeaderLead && !(allSeen && share >= StrategistTuning.LeaderShare)) return;
        s.Leader = leader;
        s.CoalitionValue = StrategistTuning.CoalitionBase + Fix64.Clamp(lead - StrategistTuning.LeaderLead, Fix64.Zero, Fix64.FromRatio(3, 10));
    }
}
