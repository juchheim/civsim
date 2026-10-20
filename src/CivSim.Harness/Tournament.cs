using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>Who plays a seat: the Council, the smart AI at a level, or a scripted bot (docs/MVP/14 §10.2).</summary>
internal readonly record struct Player(string Name, CivControl Control)
{
    public static readonly string[] Names =
        { "council", "random", "grower", "builder", "wanderer", "rusher", "turtle", "settler", "chieftain", "warlord", "legend" };

    public static Player Parse(string name, EconomyArchetype archetype) => name.ToLowerInvariant() switch
    {
        "council" => new Player("council", CivControl.Council),
        "random" => new Player("random", CivControl.Scripted(BotKind.Random)),
        "grower" => new Player("grower", CivControl.Scripted(BotKind.OpeningGrower)),
        "builder" => new Player("builder", CivControl.Scripted(BotKind.OpeningBuilder)),
        "wanderer" => new Player("wanderer", CivControl.Scripted(BotKind.OpeningWanderer)),
        "rusher" => new Player("rusher", CivControl.Scripted(BotKind.Rusher)),
        "turtle" => new Player("turtle", CivControl.Scripted(BotKind.Turtle)),
        "settler" => new Player("settler", CivControl.Ai(Difficulty.Settler, archetype)),
        "chieftain" => new Player("chieftain", CivControl.Ai(Difficulty.Chieftain, archetype)),
        "warlord" => new Player("warlord", CivControl.Ai(Difficulty.Warlord, archetype)),
        "legend" => new Player("legend", CivControl.Ai(Difficulty.Legend, archetype)),
        _ => throw new ArgumentException($"Unknown player '{name}' (one of {string.Join(", ", Names)})."),
    };
}

/// <summary>One civilization's result and the blunders its mind committed (docs/MVP/14 §10.4).</summary>
internal sealed class CivOutcome
{
    public int Civ;
    public double Dominion, Mastery, Firsts;
    public int Settlements, SettlementsLost, SettlementsCaptured, Projects;
    public readonly double[] KeyMinutes = { double.NaN, double.NaN, double.NaN };
    public int RaidsLaunched, RaidsWon;
    // Blunders sampled once a season.
    public int KeystoneStalls, IdleSlotSeasons, PlanSwitchesInThreeYears;
    public double Legacy => Dominion + Mastery + Firsts;
}

internal sealed class MatchOutcome
{
    public ulong Seed;
    public Player[] Seats = Array.Empty<Player>();
    public CivOutcome[] Civs = Array.Empty<CivOutcome>();
    public ulong Checksum;
}

/// <summary>Runs matches headless and scores them. The harness may use doubles and threads; the simulation does not.</summary>
internal static class Tournament
{
    public const ulong BaseSeed = 20261019;
    public static readonly int[] TeamASeats = { 0, 3 };
    public static readonly int[] TeamBSeats = { 1, 2 };

    public static MatchOutcome Play(ContentPack pack, ulong seed, Player[] seats, EconomyArchetype[] archetypes, int minutes)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[seats.Length];
        for (int i = 0; i < seats.Length; i++) controls[i] = seats[i].Control;
        MatchModel match = MatchModel.Create(seed, pack, world, archetypes, controls);
        var runner = new SimRunner(match);
        int civs = seats.Length;
        var outcome = new MatchOutcome { Seed = seed, Seats = seats, Civs = new CivOutcome[civs] };
        for (int c = 0; c < civs; c++) outcome.Civs[c] = new CivOutcome { Civ = c };
        ResearchCatalog catalog = match.Research.Catalog;
        int[] keys = { catalog.IndexOf("KEY-1"), catalog.IndexOf("KEY-2"), catalog.IndexOf("KEY-3") };
        var stalled = new bool[civs * 3];
        var lastPlan = new int[civs];
        var switches = new List<int>[civs];
        for (int c = 0; c < civs; c++) { lastPlan[c] = -1; switches[c] = new List<int>(); }

        long ticks = minutes * 600L;
        int season = 0;
        for (long t = 0; t < ticks; t += 150, season++)
        {
            runner.Advance(Math.Min(150, ticks - t));
            for (int c = 0; c < civs; c++)
            {
                if (seats[c].Control.Kind == ControlKind.Council) continue;
                CivOutcome o = outcome.Civs[c];
                // Keystone stall: breadth met and the keystone Available, but unslotted two seasons running.
                for (int k = 0; k < 3; k++)
                {
                    ProjectDef def = catalog.Projects[keys[k]];
                    bool waiting = !match.Research.IsDone(c, keys[k]) && match.Research.State(c, keys[k]) == ProjectState.Available;
                    if (waiting && stalled[c * 3 + k]) { o.KeystoneStalls++; stalled[c * 3 + k] = false; }
                    else stalled[c * 3 + k] = waiting;
                }
                // Idle research slot while a project could take it.
                bool anyEmpty = false;
                for (int s = 0; s < CivResearch.SlotCount; s++) if (match.Research.Slot(c, s) < 0) anyEmpty = true;
                if (anyEmpty && HasAvailable(match.Research, catalog, c)) o.IdleSlotSeasons++;
                AiSeat? seat = match.Ai?.SeatOf(c);
                if (seat is not null && seat.Control.Kind == ControlKind.Ai)
                {
                    int plan = (int)seat.Context.Intent.Plan;
                    if (lastPlan[c] >= 0 && plan != lastPlan[c]) switches[c].Add(season);
                    lastPlan[c] = plan;
                    int recent = 0;
                    foreach (int s in switches[c]) if (s > season - 12) recent++;
                    o.PlanSwitchesInThreeYears = Math.Max(o.PlanSwitchesInThreeYears, recent);
                }
            }
        }

        PoliticsSystem pol = match.Politics;
        foreach (PoliticsEvent e in pol.Events)
        {
            if (e.Kind == PoliticsEventKind.Captured)
            {
                // Each capture is logged twice: the captor's event has B >= 0, the loser's B <= 0 (both 0 if nobody stayed, so
                // the final owner decides that case).
                bool captor = e.B > 0 || e.B == 0 && pol.SettlementCivOf(e.A) == e.Civ;
                if (captor && e.Other >= 0 && e.Other < civs && e.Other != e.Civ) { outcome.Civs[e.Other].SettlementsLost++; outcome.Civs[e.Civ].SettlementsCaptured++; }
            }
            else if (e.Kind == PoliticsEventKind.RaidLaunched) outcome.Civs[e.Civ].RaidsLaunched++;
            else if (e.Kind == PoliticsEventKind.RaidResolved && e.A == 1) outcome.Civs[e.Civ].RaidsWon++;
        }
        for (int i = 0; i < match.Economy.SettlementCount; i++)
        {
            int owner = pol.SettlementCivOf(i);
            if (owner >= 0 && owner < civs && !pol.IsFreeFolk(i)) outcome.Civs[owner].Settlements++;
        }
        for (int c = 0; c < civs; c++)
        {
            CivOutcome o = outcome.Civs[c];
            Standing s = Scoring.Of(match, c);
            o.Dominion = s.Dominion.ToDoubleForDisplay();
            o.Mastery = s.Mastery.ToDoubleForDisplay();
            o.Firsts = s.Firsts.ToDoubleForDisplay();
            o.Projects = match.Research.DoneCount(c);
            for (int k = 0; k < 3; k++)
                if (match.Research.IsDone(c, keys[k])) o.KeyMinutes[k] = match.Research.CompletedTick(c, keys[k]) / 600.0;
        }
        outcome.Checksum = runner.CurrentChecksum;
        return outcome;
    }

    private static bool HasAvailable(ResearchSystem research, ResearchCatalog catalog, int civ)
    {
        for (int p = 0; p < catalog.Count; p++)
            if (catalog.Projects[p].Numeric && research.State(civ, p) == ProjectState.Available) return true;
        return false;
    }

    /// <summary>
    /// A mirrored duel: team A (seats 0 and 3) against team B (seats 1 and 2) on each seed, then again with the teams
    /// swapped, so neither side gets the better half of the map. Returns one result per match from team A's view.
    /// </summary>
    public sealed class DuelResult
    {
        public int Matches;
        public double WinsA;
        public double SettlementsLostA, SettlementsLostB;
        public int MatchesWithLossA, MatchesWithLossB;
        public double LegacyA, LegacyB;
        public int RaidsA, RaidsB;
        public int KeystoneStallsA, IdleSlotSeasonsA, MaxPlanSwitchesA;
        public int PlanDitherMatchesA;
        public double MeanKey1A = double.NaN, MeanKey1B = double.NaN;
        public readonly List<MatchOutcome> Outcomes = new();
        public double WinRateA => Matches == 0 ? 0 : WinsA / Matches;
    }

    public static DuelResult Duel(ContentPack pack, string teamA, string teamB, int seeds, int minutes, EconomyArchetype archetype, ulong firstSeed = BaseSeed)
    {
        var jobs = new List<(ulong Seed, bool Swapped)>();
        for (int i = 0; i < seeds; i++) { jobs.Add((firstSeed + (ulong)i, false)); jobs.Add((firstSeed + (ulong)i, true)); }
        var results = new (MatchOutcome Outcome, bool Swapped)[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, j =>
        {
            (ulong seed, bool swapped) = jobs[j];
            Player a = Player.Parse(teamA, archetype), b = Player.Parse(teamB, archetype);
            var seats = new Player[4];
            foreach (int s in TeamASeats) seats[s] = swapped ? b : a;
            foreach (int s in TeamBSeats) seats[s] = swapped ? a : b;
            var archetypes = new[] { archetype, archetype, archetype, archetype };
            results[j] = (Play(pack, seed, seats, archetypes, minutes), swapped);
        });
        var r = new DuelResult();
        double key1A = 0, key1B = 0; int nA = 0, nB = 0;
        foreach ((MatchOutcome m, bool swapped) in results)
        {
            int[] aSeats = swapped ? TeamBSeats : TeamASeats, bSeats = swapped ? TeamASeats : TeamBSeats;
            double la = 0, lb = 0; int lostA = 0, lostB = 0;
            foreach (int s in aSeats)
            {
                CivOutcome c = m.Civs[s];
                la += c.Legacy; lostA += c.SettlementsLost; r.RaidsA += c.RaidsLaunched;
                r.KeystoneStallsA += c.KeystoneStalls; r.IdleSlotSeasonsA += c.IdleSlotSeasons;
                r.MaxPlanSwitchesA = Math.Max(r.MaxPlanSwitchesA, c.PlanSwitchesInThreeYears);
                if (c.PlanSwitchesInThreeYears > 2) r.PlanDitherMatchesA++;
                if (!double.IsNaN(c.KeyMinutes[0])) { key1A += c.KeyMinutes[0]; nA++; }
            }
            foreach (int s in bSeats)
            {
                CivOutcome c = m.Civs[s];
                lb += c.Legacy; lostB += c.SettlementsLost; r.RaidsB += c.RaidsLaunched;
                if (!double.IsNaN(c.KeyMinutes[0])) { key1B += c.KeyMinutes[0]; nB++; }
            }
            r.Matches++;
            r.WinsA += la > lb ? 1 : la == lb ? 0.5 : 0;
            r.LegacyA += la; r.LegacyB += lb;
            r.SettlementsLostA += lostA; r.SettlementsLostB += lostB;
            if (lostA > 0) r.MatchesWithLossA++;
            if (lostB > 0) r.MatchesWithLossB++;
            r.Outcomes.Add(m);
        }
        if (nA > 0) r.MeanKey1A = key1A / nA;
        if (nB > 0) r.MeanKey1B = key1B / nB;
        return r;
    }

    /// <summary>
    /// A free-for-all: four AIs of one level with different archetypes, rotated through the seats so position does
    /// not decide. Returns wins and appearances per archetype (docs/MVP/14 §10.3 archetype balance).
    /// </summary>
    public static (double[] Wins, int[] Appearances) FreeForAll(ContentPack pack, string level, int seeds, int minutes, ulong firstSeed = BaseSeed)
    {
        EconomyArchetype[] all = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Wanderer, EconomyArchetype.Warlord, EconomyArchetype.Sage };
        var jobs = new List<(ulong Seed, int Rotation)>();
        for (int i = 0; i < seeds; i++) for (int rot = 0; rot < 5; rot++) jobs.Add((firstSeed + (ulong)i, rot));
        var outcomes = new (MatchOutcome Outcome, EconomyArchetype[] Seating)[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, j =>
        {
            (ulong seed, int rot) = jobs[j];
            // The rotation drops one archetype; the shift moves the others round the table so position does not decide.
            var seating = new EconomyArchetype[4];
            int shift = (int)(seed % 4);
            for (int k = 0; k < 4; k++) seating[(k + shift) % 4] = all[(rot + 1 + k) % 5];
            var seats = new Player[4];
            for (int s = 0; s < 4; s++) seats[s] = Player.Parse(level, seating[s]);
            outcomes[j] = (Play(pack, seed, seats, seating, minutes), seating);
        });
        var wins = new double[5];
        var apps = new int[5];
        foreach ((MatchOutcome m, EconomyArchetype[] seating) in outcomes)
        {
            double best = double.MinValue; int bestSeat = 0;
            for (int s = 0; s < 4; s++) if (m.Civs[s].Legacy > best) { best = m.Civs[s].Legacy; bestSeat = s; }
            for (int s = 0; s < 4; s++) apps[Array.IndexOf(all, seating[s])]++;
            wins[Array.IndexOf(all, seating[bestSeat])]++;
        }
        return (wins, apps);
    }

    /// <summary>Elo ratings from pairwise win rates by repeated updates against the aggregate (starts at 1000).</summary>
    public static double[] Elo(int players, double[,] winRate, int[,] games)
    {
        var rating = new double[players];
        for (int i = 0; i < players; i++) rating[i] = 1000;
        for (int pass = 0; pass < 400; pass++)
        {
            for (int a = 0; a < players; a++)
                for (int b = a + 1; b < players; b++)
                {
                    if (games[a, b] == 0) continue;
                    double expected = 1.0 / (1.0 + Math.Pow(10, (rating[b] - rating[a]) / 400.0));
                    double actual = winRate[a, b];
                    double delta = 4.0 * (actual - expected);
                    rating[a] += delta; rating[b] -= delta;
                }
        }
        return rating;
    }
}
