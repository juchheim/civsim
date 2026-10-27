using System;
using System.Collections.Generic;
using System.Globalization;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The omniscient test double of the belief layer: copies every rival's true settlements, military, defenses, projects and
/// the whole map into the belief, with the same fields the fog-limited builder fills. The Strategist is developed against
/// it; it must also behave (more cautiously) against the real, fog-limited beliefs.
/// </summary>
internal sealed class PerfectBeliefBuilder : IBeliefBuilder
{
    /// <summary>Added to the season stamp: 1 when filled just before the season boundary the mind decides on.</summary>
    public int SeasonOffset { get; init; }

    public void Update(Belief belief, TrueWorld world, DifficultyProfile profile)
    {
        PoliticsSystem pol = world.Politics;
        ResearchSystem res = world.Research;
        int season = pol.Season + SeasonOffset;
        belief.Season = season;
        MapKnowledge map = belief.Map;
        for (int t = 0; t < map.TileCount; t++)
        {
            map.MarkExplored(t);
            map.RecordOwner(t, pol.Territory.OwnerOf(t), season);
        }
        for (int o = 0; o < belief.Rivals.Length; o++)
        {
            if (o == belief.Civ) continue;
            RivalBelief r = belief.Rivals[o];
            Diplomacy d = pol.Diplomacy;
            r.Aware = d.Aware(belief.Civ, o);
            r.Contacted = d.Contacted(belief.Civ, o);
            r.Relations = d.Relations(belief.Civ, o);
            r.Reputation = d.Reputation(o);
            r.AtWar = d.AtWar(belief.Civ, o);
            r.Allied = d.Allied(belief.Civ, o);
            r.InTruce = d.InTruce(belief.Civ, o, pol.Season);
            r.Era = res.Era(o);
            r.LastSeenSeason = season;
            r.Confidence = Fix64.One;
            for (int p = 0; p < r.ProjectBelieved.Length; p++) r.ProjectBelieved[p] = res.IsDone(o, p);
            r.WeaponStrength = pol.WeaponOf(o, out bool ranged);
            r.RangedSeen = ranged;
            r.WarriorsSeen = res.IsDone(o, res.Catalog.IndexOf("WAR-04"));
            r.RaidPostureSeen = pol.PostureOf(o) == Posture.Raid;
            for (int a = 0; a < r.ArchetypePosterior.Length; a++) r.ArchetypePosterior[a] = Fix64.FromRatio(1, 5);
            r.Settlements.Clear();
            int pop = 0, warriors = 0;
            for (int s = 0; s < pol.SettlementTotal; s++)
            {
                if (pol.SettlementCivOf(s) != o || pol.IsFreeFolk(s)) continue;
                SettlementMilitary m = pol.MilitaryOf(s);
                int people = pol.SettlementPopulationOf(s);
                pop += people;
                warriors += m.Warriors;
                r.Settlements.Add(new BeliefSettlement
                {
                    Settlement = s, Tile = pol.SettlementTileOf(s), LastSeenSeason = season, PopulationEstimate = people,
                    FightersEstimate = m.Fighters, WarriorsSeen = m.Warriors, DefendingStrengthEstimate = m.DefendingStrength,
                    ThornBarrierSeen = m.Defenses.ThornBarrier, PalisadeSeen = m.Defenses.Palisade, WatchersSeen = m.Defenses.Watchers,
                    DistressSeen = world.Economy.SettlementLite(s).LastFoodSecurity < Fix64.FromRatio(8, 10),
                });
            }
            r.PopulationLow = r.PopulationHigh = pop;
            r.WarriorsEstimate = warriors;
        }
    }

    public void Hash(ref StateHasher hasher) { }
    public void Write(System.IO.BinaryWriter writer) { }
    public void Read(System.IO.BinaryReader reader) { }
}

/// <summary>
/// The scripted "Rusher" of docs/MVP/14 §10.2: WAR lineage first, Raid posture at GOV-02, raids from first contact
/// (as soon as Raiding allows), and the v0 rules' wars under Raid posture. Driven from the test loop through the same
/// public orders a player has.
/// </summary>
internal sealed class Rusher
{
    private static readonly string[] Priority =
        { "WAR-01", "HNT-02", "HNT-03", "HNT-04", "WAR-02", "GOV-01", "SOC-05", "GOV-02", "HNT-06", "WAR-03", "WAR-04" };
    private readonly int _civ, _target;
    private int _lastRaid = -1000;

    public Rusher(int civ, int target) { _civ = civ; _target = target; }

    public void Season(MatchModel match)
    {
        ResearchSystem res = match.Research;
        PoliticsSystem pol = match.Politics;
        res.SetAiControlled(_civ, true);
        int next = NextProject(res);
        if (next >= 0 && res.Slot(_civ, 0) != next && res.Slot(_civ, 1) != next && res.Slot(_civ, 2) != next)
            res.ApplyOrder(new Command(match.Economy.SeasonsCompleted * 150, (byte)_civ, CommandKind.SetSlot, 0, next, 0));
        if (res.IsDone(_civ, res.Catalog.IndexOf("GOV-02")) && pol.PostureOf(_civ) != Posture.Raid) pol.OrderPosture(_civ, Posture.Raid);
        if (!pol.Diplomacy.Contacted(_civ, _target) || pol.Season - _lastRaid < 4) return;
        int prey = -1, home = -1, best = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != _target || pol.IsFreeFolk(s)) continue;
            for (int h = 0; h < pol.SettlementTotal; h++)
            {
                if (pol.SettlementCivOf(h) != _civ || pol.IsFreeFolk(h)) continue;
                int d = HexGrid.Distance(pol.SettlementTileOf(s), pol.SettlementTileOf(h), 160);
                if (d < best) { best = d; prey = s; home = h; }
            }
        }
        if (prey < 0) return;
        int size = Math.Max(4, pol.MilitaryOf(home).Adults * 3 / 10);
        if (pol.OrderRaid(_civ, prey, size)) _lastRaid = pol.Season;
    }

    private int NextProject(ResearchSystem res)
    {
        foreach (string id in Priority)
        {
            int p = res.Catalog.IndexOf(id);
            if (p < 0 || res.IsDone(_civ, p)) continue;
            int found = Available(res, p, 0);
            if (found >= 0) return found;
        }
        return -1;
    }

    private int Available(ResearchSystem res, int p, int depth)
    {
        if (res.IsDone(_civ, p) || depth > 6) return -1;
        ProjectState state = res.State(_civ, p);
        if (state is ProjectState.Available or ProjectState.Slotted) return p;
        foreach (int q in res.Catalog.Projects[p].Requires)
        {
            int found = Available(res, q, depth + 1);
            if (found >= 0) return found;
        }
        return -1;
    }
}

/// <summary>A match run season by season with optional omniscient beliefs and Rushers, for scenario tests and benches.</summary>
internal sealed class StrategistRig
{
    public readonly MatchModel Match;
    public readonly SimRunner Runner;
    public bool Omniscient = true;
    public readonly List<Rusher> Rushers = new();
    private readonly PerfectBeliefBuilder _builder = new() { SeasonOffset = 1 };

    public StrategistRig(ulong seed, GeneratedWorld world, CivControl[] controls, EconomyArchetype[]? archetypes = null, byte[]? snapshot = null)
    {
        archetypes ??= new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        Match = MatchModel.Create(seed, TestPack.Pack, world, archetypes, controls);
        long start = 0;
        if (snapshot is not null)
        {
            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(snapshot));
            Match.ReadSnapshot(r);
            start = (Match.Economy.SeasonsCompleted) * 150;
        }
        Runner = new SimRunner(Match, SimRunner.DefaultChecksumInterval, start);
    }

    public byte[] Snapshot()
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) Match.WriteSnapshot(w);
        return ms.ToArray();
    }

    public PoliticsSystem Politics => Match.Politics;
    public AiLog Log => Match.Ai!.Log;
    public int Season => Match.Politics.Season;

    public void Seasons(int n)
    {
        for (int i = 0; i < n; i++)
        {
            long toEdge = 149 - Runner.Tick % 150;
            if (toEdge > 0) Runner.Advance(toEdge);
            foreach (Rusher r in Rushers) r.Season(Match);
            if (Omniscient && Match.Ai is { } ai)
                for (int c = 0; c < ai.CivCount; c++)
                    if (ai.SeatOf(c) is { } seat) _builder.Update(seat.Belief, ai.World, seat.Profile);
            Runner.Advance(1);
        }
    }

    public void Minutes(int minutes) => Seasons(minutes * 4 - Season);

    public void Grant(int civ, params string[] ids)
    {
        foreach (string id in ids)
            Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(id), 0, 0));
        Runner.Advance(1);
    }

    public List<int> SettlementsOf(int civ)
    {
        var list = new List<int>();
        for (int s = 0; s < Politics.SettlementTotal; s++)
            if (Politics.SettlementCivOf(s) == civ && !Politics.IsFreeFolk(s)) list.Add(s);
        return list;
    }

    public IEnumerable<AiDecision> Notes(int civ, string kind)
    {
        foreach (AiDecision d in Log.Entries)
            if (d.Civ == civ && d.Layer == Strategist.Layer && d.Kind == kind) yield return d;
    }
}

/// <summary>
/// The blunder detectors of docs/MVP/14 §10.4 that concern the Strategist, counted from the AI log and the politics
/// events. Reusable by the integrator's tournaments.
/// </summary>
public static class StrategistAudit
{
    public readonly record struct Result(int SuicideRaids, int UndefendedLosses, int WinterAggression, int Dithering,
        int Raids, int Assaults, int ThreatsNoticed, int LossesToRaids, int SettlementsLost)
    {
        public override string ToString()
            => $"suicide raids {SuicideRaids}, undefended losses {UndefendedLosses}, winter aggression {WinterAggression}, dithering {Dithering} " +
               $"(raids {Raids}, assaults {Assaults}, threats {ThreatsNoticed}, raided {LossesToRaids}, settlements lost {SettlementsLost})";
    }

    public static readonly Fix64 SuicideBelow = Fix64.FromRatio(30, 100);
    public const int WinterFloorPercent = 80;

    public static Result Run(int civ, IReadOnlyList<AiDecision> log, IReadOnlyList<PoliticsEvent> events)
    {
        int suicide = 0, winter = 0, raids = 0, assaults = 0, threats = 0;
        var switches = new List<int>();
        var threatNotes = new List<(int Season, int Rival)>();
        var defenseNotes = new List<(int Season, int Rival)>();
        foreach (AiDecision d in log)
        {
            if (d.Civ != civ || d.Layer != Strategist.Layer) continue;
            switch (d.Kind)
            {
                case "Raid":
                    raids++;
                    if (d.Score < SuicideBelow) suicide++;
                    if (ColdAndHungry(d.Reason)) winter++;
                    break;
                case "Assault":
                    assaults++;
                    if (ColdAndHungry(d.Reason)) winter++;
                    break;
                case "Plan":
                    if (d.Other >= 0 && d.Reason.StartsWith("hard=0", StringComparison.Ordinal)) switches.Add(d.Season);
                    break;
                case "Threat": threats++; threatNotes.Add((d.Season, d.Subject)); break;
                case "Defense": defenseNotes.Add((d.Season, d.Subject)); break;
            }
        }
        int dithering = 0;
        for (int i = 0; i < switches.Count; i++)
        {
            int inWindow = 0;
            for (int j = i; j < switches.Count && switches[j] - switches[i] < 12; j++) inWindow++;
            if (inWindow > 2) dithering++;
        }
        int undefended = 0, raided = 0, lost = 0;
        foreach (PoliticsEvent e in events)
        {
            if (e.Civ != civ) continue;
            bool raidLoss = e.Kind == PoliticsEventKind.RaidResolved && e.A == 2;
            bool captured = e.Kind == PoliticsEventKind.Captured && e.B < 0;
            if (!raidLoss && !captured) continue;
            if (raidLoss) raided++; else lost++;
            int season = (int)((e.Tick + 1) / 150);
            int threatSeason = -1;
            foreach ((int s, int rival) in threatNotes) if (rival == e.Other && s <= season - 2) threatSeason = s;
            if (threatSeason < 0) continue;
            bool answered = false;
            foreach ((int s, int rival) in defenseNotes) if (rival == e.Other && s >= threatSeason && s <= season) answered = true;
            if (!answered) undefended++;
        }
        return new Result(suicide, undefended, winter, dithering, raids, assaults, threats, raided, lost);
    }

    private static bool ColdAndHungry(string reason)
    {
        if (!reason.Contains("cold=1", StringComparison.Ordinal)) return false;
        int at = reason.IndexOf("lw=", StringComparison.Ordinal);
        if (at < 0) return false;
        int end = reason.IndexOf(' ', at);
        string value = reason.Substring(at + 3, (end < 0 ? reason.Length : end) - at - 3);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal lw) && lw * 100 < WinterFloorPercent;
    }
}

/// <summary>Records the Strategist's orders instead of carrying them out (unit tests on hand-built views).</summary>
internal sealed class ActionRecorder : IStrategistActions
{
    public readonly List<string> Orders = new();
    public bool Accept = true;
    public int Mandate { get; set; }
    public int DecreeCost(DecreeKind kind) => kind == DecreeKind.Levy ? 1 : int.MaxValue;
    public bool IssueDecree(DecreeKind kind, int target, int extra)
    { Orders.Add($"decree {kind} {target} {extra}"); return Accept; }
    public bool Gift(int toCiv) { Orders.Add($"gift {toCiv}"); return Accept; }
    public bool Barter(int withCiv) { Orders.Add($"barter {withCiv}"); return Accept; }
    public bool Raid(int targetSettlement, int partySize) { Orders.Add($"raid {targetSettlement} {partySize}"); return Accept; }
    public bool RaidCaravan(int caravanId, int partySize) { Orders.Add($"raid-caravan {caravanId} {partySize}"); return Accept; }
    public bool RaidMiningCamp(int campId, int partySize) { Orders.Add($"raid-mine {campId} {partySize}"); return Accept; }
    public bool Assault(int targetSettlement) { Orders.Add($"assault {targetSettlement}"); return Accept; }
    public bool DeclareWar(int civ) { Orders.Add($"war {civ}"); return Accept; }
    public bool MakePeace(int civ) { Orders.Add($"peace {civ}"); return Accept; }
    public bool Ally(int civ) { Orders.Add($"ally {civ}"); return Accept; }
    public bool SetEmbargo(int civ, TradeGood good, bool on) { Orders.Add($"embargo {civ} {good} {on}"); return Accept; }
    public bool SetPosture(Posture posture) { Orders.Add($"posture {posture}"); return Accept; }
    public bool PrioritizePhysicalDefenseBuild(int settlement) { Orders.Add($"defense-build {settlement}"); return Accept; }
    public bool PlaceBeacon(int tile) { Orders.Add($"beacon {tile}"); return Accept; }
    public void RemoveBeacon(int index) => Orders.Add($"unbeacon {index}");
}
