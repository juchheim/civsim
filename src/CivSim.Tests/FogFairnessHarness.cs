using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// The fog-fairness fuzz harness (acceptance A8, docs/MVP/14 §10.4 "Cheating check", 15 rule D10): clone a match, change
/// in one copy only what an observer cannot see, run both on, and compare what the observer's mind received and did.
/// Built so the integrator can extend <see cref="CompareMinds"/> to the full AiDecision log once the Spark and Strategist land.
/// </summary>
public static class FogFairnessHarness
{
    public const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> LazyWorld = new(() => WorldGenerator.Generate(Seed));
    public static GeneratedWorld World => LazyWorld.Value;

    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    /// <summary>Smart AI seats at civilizations 0 and 2 (Warlord level: the full opponent model), the Council at 1 and 3.</summary>
    public static CivControl[] DefaultControls() => new[]
    {
        CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Warlord), CivControl.Council,
        CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Grower), CivControl.Council,
    };

    public static MatchModel NewMatch(CivControl[] controls) => MatchModel.Create(Seed, TestPack.Pack, World, Balanced, controls);

    /// <summary>An exact copy of a match through its snapshot (the copy's checksum equals the source's).</summary>
    public static MatchModel Clone(MatchModel source, CivControl[] controls)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) source.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel copy = NewMatch(controls);
        using (var r = new BinaryReader(stream)) copy.ReadSnapshot(r);
        return copy;
    }

    /// <summary>
    /// Copies <paramref name="baseline"/> at <paramref name="tick"/>, lets <paramref name="perturb"/> change the copy (it may
    /// submit commands to the copy's runner for this tick), then steps both <paramref name="ticks"/> ticks. With the tick
    /// one before a season end and one step, both run exactly one season-end pass: politics, then every AI seat's own view,
    /// belief, Strategist and Spark. Returns both matches; the baseline is the same object, advanced.
    /// </summary>
    public static (MatchModel Baseline, MatchModel Perturbed) RunPair(MatchModel baseline, long tick, CivControl[] controls,
        Action<MatchModel, SimRunner> perturb, long ticks = 1)
    {
        MatchModel copy = Clone(baseline, controls);
        Assert.Equal(baseline.ComputeChecksum(), copy.ComputeChecksum());
        var copyRunner = new SimRunner(copy, SimRunner.DefaultChecksumInterval, tick);
        perturb(copy, copyRunner);
        new SimRunner(baseline, SimRunner.DefaultChecksumInterval, tick).Advance(ticks);
        copyRunner.Advance(ticks);
        return (baseline, copy);
    }

    // ---- what an observer's mind received and did ---------------------------------------------------------

    /// <summary>Every difference between what two seats of the same civilization received and did; empty when fog-fair.</summary>
    public static List<string> CompareMinds(MatchModel a, MatchModel b, int observer, long sinceTick)
    {
        var diffs = new List<string>();
        AiSeat sa = a.Ai!.SeatOf(observer)!, sb = b.Ai!.SeatOf(observer)!;
        CompareBelief(sa.Belief, sb.Belief, diffs);
        CompareOwnView(sa.Own, sb.Own, diffs);
        // What the mind did: research steering, posture and beacons, and the decision log.
        for (int slot = 0; slot < 3; slot++)
            Check(diffs, $"slot {slot}", a.Research.Slot(observer, slot), b.Research.Slot(observer, slot));
        Check(diffs, "queue", string.Join(",", a.Research.Queue(observer)), string.Join(",", b.Research.Queue(observer)));
        for (int t = 0; t < 3; t++) Check(diffs, $"leaning {t}", a.Research.Leaning(observer, t), b.Research.Leaning(observer, t));
        Check(diffs, "posture", a.Politics.PostureOf(observer), b.Politics.PostureOf(observer));
        Check(diffs, "beacons", string.Join(",", a.Politics.BeaconsOf(observer)), string.Join(",", b.Politics.BeaconsOf(observer)));
        Check(diffs, "decisions", Decisions(a, observer, sinceTick), Decisions(b, observer, sinceTick));
        return diffs;
    }

    private static string Decisions(MatchModel m, int civ, long sinceTick)
    {
        var parts = new List<string>();
        foreach (AiDecision d in m.Ai!.Log.Entries) if (d.Civ == civ && d.Tick >= sinceTick) parts.Add(d.ToString());
        return string.Join("|", parts);
    }

    public static ulong HashOf(Belief belief)
    {
        var h = new StateHasher();
        belief.Hash(ref h);
        return h.Finish();
    }

    private static void Check<T>(List<string> diffs, string what, T x, T y)
    {
        if (!EqualityComparer<T>.Default.Equals(x, y)) diffs.Add($"{what}: {x} vs {y}");
    }

    public static void CompareBelief(Belief a, Belief b, List<string> diffs)
    {
        Check(diffs, "belief season", a.Season, b.Season);
        for (int t = 0; t < a.Map.TileCount; t++)
        {
            if (a.Map.IsExplored(t) != b.Map.IsExplored(t) || a.Map.OwnerSeen(t) != b.Map.OwnerSeen(t)) { diffs.Add($"map tile {t}"); break; }
        }
        for (int c = 0; c < a.Rivals.Length; c++)
        {
            RivalBelief x = a.Rivals[c], y = b.Rivals[c];
            string p = $"rival {c} ";
            Check(diffs, p + "aware", x.Aware, y.Aware); Check(diffs, p + "contacted", x.Contacted, y.Contacted);
            Check(diffs, p + "first contact", x.FirstContactSeason, y.FirstContactSeason); Check(diffs, p + "last seen", x.LastSeenSeason, y.LastSeenSeason);
            Check(diffs, p + "era", x.Era, y.Era); Check(diffs, p + "population", (x.PopulationLow, x.PopulationHigh), (y.PopulationLow, y.PopulationHigh));
            Check(diffs, p + "weapon", (x.WeaponStrength, x.RangedSeen), (y.WeaponStrength, y.RangedSeen));
            Check(diffs, p + "warriors", (x.WarriorsSeen, x.WarriorsEstimate, x.RaidPostureSeen), (y.WarriorsSeen, y.WarriorsEstimate, y.RaidPostureSeen));
            Check(diffs, p + "public", (x.KeystonesAnnounced, x.FirstsAnnounced), (y.KeystonesAnnounced, y.FirstsAnnounced));
            Check(diffs, p + "diplomacy", (x.Relations, x.Reputation, x.AtWar, x.Allied, x.InTruce), (y.Relations, y.Reputation, y.AtWar, y.Allied, y.InTruce));
            Check(diffs, p + "confidence", x.Confidence, y.Confidence);
            Check(diffs, p + "threat", (x.ThreatProbability, x.ThreatLoss), (y.ThreatProbability, y.ThreatLoss));
            Check(diffs, p + "opportunity", x.Opportunity, y.Opportunity);
            Check(diffs, p + "posterior", string.Join(",", x.ArchetypePosterior), string.Join(",", y.ArchetypePosterior));
            Check(diffs, p + "projects", string.Join("", Array.ConvertAll(x.ProjectBelieved, v => v ? '1' : '0')),
                string.Join("", Array.ConvertAll(y.ProjectBelieved, v => v ? '1' : '0')));
            Check(diffs, p + "record", (x.SettlementsSeenEver, x.ClaimTilesSeen, x.ClaimTilesSeenMax, x.StructureKindsSeen, x.SeenFlags, x.WarriorsSeenMax),
                (y.SettlementsSeenEver, y.ClaimTilesSeen, y.ClaimTilesSeenMax, y.StructureKindsSeen, y.SeenFlags, y.WarriorsSeenMax));
            Check(diffs, p + "history", (x.RaidsOnUs, x.LastRaidOnUsSeason, x.LastGiftFromSeason, x.PartiesAwaySeen),
                (y.RaidsOnUs, y.LastRaidOnUsSeason, y.LastGiftFromSeason, y.PartiesAwaySeen));
            Check(diffs, p + "settlement count", x.Settlements.Count, y.Settlements.Count);
            for (int i = 0; i < Math.Min(x.Settlements.Count, y.Settlements.Count); i++)
            {
                BeliefSettlement s = x.Settlements[i], t = y.Settlements[i];
                Check(diffs, p + $"settlement {i}",
                    (s.Settlement, s.Tile, s.LastSeenSeason, s.FirstSeenSeason, s.PopulationEstimate, s.FightersEstimate, s.WarriorsSeen,
                        s.DefendingStrengthEstimate, s.ThornBarrierSeen, s.PalisadeSeen, s.WatchersSeen, s.DistressSeen),
                    (t.Settlement, t.Tile, t.LastSeenSeason, t.FirstSeenSeason, t.PopulationEstimate, t.FightersEstimate, t.WarriorsSeen,
                        t.DefendingStrengthEstimate, t.ThornBarrierSeen, t.PalisadeSeen, t.WatchersSeen, t.DistressSeen));
            }
        }
        Check(diffs, "incoming", a.IncomingParties.Count, b.IncomingParties.Count);
        for (int i = 0; i < Math.Min(a.IncomingParties.Count, b.IncomingParties.Count); i++)
        {
            IncomingParty x = a.IncomingParties[i], y = b.IncomingParties[i];
            Check(diffs, $"incoming {i}", (x.Civ, x.Kind, x.FromSettlement, x.TargetSettlement, x.ArriveSeason, x.PartySize),
                (y.Civ, y.Kind, y.FromSettlement, y.TargetSettlement, y.ArriveSeason, y.PartySize));
        }
        // The hash covers every field, including any added later.
        Check(diffs, "belief hash", HashOf(a), HashOf(b));
    }

    public static void CompareOwnView(OwnView a, OwnView b, List<string> diffs)
    {
        Check(diffs, "own", (a.Civ, a.Era, a.Season, a.Tick, a.Population, a.Adults, a.ClaimedTiles, a.BeaconAllowance, a.ReachTiles),
            (b.Civ, b.Era, b.Season, b.Tick, b.Population, b.Adults, b.ClaimedTiles, b.BeaconAllowance, b.ReachTiles));
        Check(diffs, "own insight", (a.InsightPerYear, a.Posture), (b.InsightPerYear, b.Posture));
        // Dominion and Legacy are public aggregate scores; an unseen rival's territory can change their denominator.
        // CompareMinds still checks the resulting decisions, so score movement only matters here if it changes behavior.
        Check(diffs, "own parties", (a.RaidsOnTheRoad, a.AssaultsOnTheRoad, a.GiftsOnTheRoad, a.Occupations),
            (b.RaidsOnTheRoad, b.AssaultsOnTheRoad, b.GiftsOnTheRoad, b.Occupations));
        Check(diffs, "own settlements", a.Settlements.Count, b.Settlements.Count);
        for (int i = 0; i < Math.Min(a.Settlements.Count, b.Settlements.Count); i++)
        {
            OwnSettlement x = a.Settlements[i], y = b.Settlements[i];
            Check(diffs, $"own settlement {i}",
                (x.Index, x.Tile, x.Population, x.Adults, x.Fighters, x.Warriors, x.DefendingStrength, x.AttackingStrength, x.FoodStores,
                    x.FoodSecurity, x.LateWinterForecast, x.Cohesion, x.Defenses),
                (y.Index, y.Tile, y.Population, y.Adults, y.Fighters, y.Warriors, y.DefendingStrength, y.AttackingStrength, y.FoodStores,
                    y.FoodSecurity, y.LateWinterForecast, y.Cohesion, y.Defenses));
        }
        for (int i = 0; i < Math.Min(a.Others.Length, b.Others.Length); i++)
        {
            PairView x = a.Others[i], y = b.Others[i];
            if (x is null || y is null) continue;
            Check(diffs, $"own pair {i}",
                (x.Aware, x.Contacted, x.AtWar, x.Allied, x.InTruce, x.Relations, x.Reputation, x.LastGiftSeason, x.LastBarterSeason,
                    x.LastRaidOnThemSeason, x.LastRaidOnUsSeason),
                (y.Aware, y.Contacted, y.AtWar, y.Allied, y.InTruce, y.Relations, y.Reputation, y.LastGiftSeason, y.LastBarterSeason,
                    y.LastRaidOnThemSeason, y.LastRaidOnUsSeason));
        }
    }

    // ---- perturbations --------------------------------------------------------------------------------------

    /// <summary>
    /// Changes, in <paramref name="m"/>, true state of <paramref name="observer"/>'s rivals that it cannot see, keeping at
    /// least <paramref name="margin"/> tiles clear of every sight rule: people killed and wounded, stores taken, Cohesion
    /// lowered in settlements out of sight; and, for rivals it has never contacted and whose figures it cannot see,
    /// research granted that wins no First and moves no era. Returns how many changes were made.
    /// </summary>
    public static int PerturbUnseen(MatchModel m, SimRunner runner, int observer, int margin, bool research = true)
    {
        TrueWorld world = m.Ai!.World;
        int changes = 0;
        var figures = new List<FigureView>();
        for (int s = 0; s < m.Politics.SettlementTotal; s++)
        {
            int owner = m.Politics.SettlementCivOf(s);
            if (owner == observer || m.Politics.IsFreeFolk(s) || m.Politics.SettlementPopulationOf(s) <= 0) continue;
            if (BeliefBuilder.TilesOutOfSight(world, observer, s) <= margin) continue;
            // The settlement nearest the observer is what any party between the two would set out from or aim at.
            if (m.Politics.Diplomacy.Contacted(observer, owner) && IsNearestTo(m, observer, owner, s)) continue;
            figures.Clear();
            m.Economy.ListFigures(s, figures);
            for (int i = 0; i < figures.Count; i++)
            {
                if (i % 3 == 0) { m.Economy.KillFigure(figures[i].Id); changes++; }
                else if (i % 3 == 1) { m.Economy.WoundFigure(figures[i].Id, 2); changes++; }
            }
            m.Economy.TakeGoods(s, Fix64.FromInt(1000), LootPreference.FoodOnly);
            m.Economy.AdjustCohesion(s, Fix64.FromRatio(-1, 10));
            changes += 2;
        }
        if (!research) return changes;
        for (int o = 0; o < m.Research.CivCount; o++)
        {
            if (o == observer || m.Politics.Diplomacy.Contacted(observer, o)) continue;
            if (BeliefBuilder.TilesOutOfFigureSight(world, observer, o) <= margin) continue;
            int granted = 0;
            for (int p = 0; p < m.Research.Catalog.Count && granted < 3; p++)
            {
                ProjectDef def = m.Research.Catalog.Projects[p];
                if (!def.Numeric || def.IsKeystone || m.Research.IsDone(o, p) || m.Research.FirstBy(p) < 0) continue;
                bool ready = true;
                foreach (int r in def.Requires) ready &= m.Research.IsDone(o, r);
                if (!ready) continue;
                runner.Submit(new Command(runner.Tick, (byte)o, CommandKind.CompleteProject, p, 0, 0));
                granted++;
                changes++;
            }
        }
        return changes;
    }

    private static bool IsNearestTo(MatchModel m, int observer, int owner, int settlement)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int t = 0; t < m.Politics.SettlementTotal; t++)
        {
            if (m.Politics.SettlementCivOf(t) != owner || m.Politics.IsFreeFolk(t)) continue;
            for (int s = 0; s < m.Politics.SettlementTotal; s++)
            {
                if (m.Politics.SettlementCivOf(s) != observer || m.Politics.IsFreeFolk(s)) continue;
                int d = HexGrid.Distance(m.Politics.SettlementTileOf(s), m.Politics.SettlementTileOf(t), World.Width);
                if (d < bestDistance) { bestDistance = d; best = t; }
            }
        }
        return best == settlement;
    }
}
