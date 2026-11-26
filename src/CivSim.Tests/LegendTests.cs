using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Legends;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Phase L1 of the Legends (docs/MVP/40): deed scores, the roll, names, the log and its frame. Nothing else in the match reads them yet.</summary>
public sealed class LegendTests
{
    private const ulong Seed = 20261127;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Start()
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, params string[] projects)
    {
        foreach (string project in projects)
        {
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf(project), 0, 0));
            runner.Advance(2);
        }
    }

    /// <summary>An adult of civilization 0 who is likely to live through the test.</summary>
    private static long AnAdult(MatchModel match, int skip = 0)
    {
        int seen = 0;
        for (int i = 0; i < match.Economy.FigureCount; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization == 0 && f.Age is >= 18 and <= 30 && seen++ >= skip) return f.Id;
        }
        throw new InvalidOperationException("no adult");
    }

    [Fact]
    public void Deeds_add_up_and_the_best_deed_is_remembered()
    {
        (MatchModel match, _) = Start();
        long id = AnAdult(match);
        match.Legends.NoteDeed(id, LegendDeed.Innovator, 10, 3);
        match.Legends.NoteDeed(id, LegendDeed.Founder, 60, 7);
        match.Legends.NoteDeed(id, LegendDeed.Innovator, 10, 4);
        Assert.Equal(80, match.Legends.ScoreOf(id));
        Assert.False(match.Legends.IsLegend(id));
        match.Legends.NoteDeed(-1, LegendDeed.Founder, 60); // not a person
        match.Legends.NoteDeed(id, LegendDeed.Founder, 0);   // nothing
        Assert.Equal(80, match.Legends.ScoreOf(id));
    }

    [Fact]
    public void A_figure_at_the_threshold_becomes_a_Legend_only_after_the_first_keystone()
    {
        (MatchModel match, SimRunner runner) = Start();
        long id = AnAdult(match);
        match.Legends.NoteDeed(id, LegendDeed.Founder, LegendSystem.Threshold, 5);
        runner.Advance(150 * 24); // two years: plenty of rolls at 35%
        Assert.False(match.Legends.IsLegend(id), "no Legends before KEY-1");

        Grant(runner, match, "KEY-1");
        for (int i = 0; i < 40 && !match.Legends.IsLegend(id); i++) runner.Advance(150);
        Assert.True(match.Legends.IsLegend(id), "a figure at the threshold becomes a Legend within a few seasons of KEY-1");
        LegendRecord record = match.Legends.Records.Single(r => r.FigureId == id);
        Assert.Equal(LegendDeed.Founder, record.Deed);
        Assert.Equal(0, record.Civilization);
        Assert.False(string.IsNullOrEmpty(LegendNames.Compose(record.NameCode)));
    }

    [Fact]
    public void A_settlement_holds_at_most_three_living_Legends()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        var ids = new List<long>();
        int home = -1;
        for (int i = 0; i < match.Economy.FigureCount && ids.Count < 6; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization != 0 || f.Age is < 18 or > 30) continue;
            if (home < 0) home = f.SettlementId;
            if (f.SettlementId == home) ids.Add(f.Id);
        }
        Assert.True(ids.Count >= 4, "the home settlement has several adults");
        foreach (long id in ids) match.Legends.NoteDeed(id, LegendDeed.Founder, 200, 1);
        for (int i = 0; i < 60; i++) runner.Advance(150);
        Assert.True(match.Legends.LivingInSettlement(home) <= LegendSystem.PerSettlementCap);
        Assert.True(match.Legends.Records.Count(r => r.Settlement == home) >= 1);
    }

    [Fact]
    public void The_chance_is_35_percent_and_ART10_doubles_it()
    {
        (MatchModel match, _) = Start();
        Assert.Equal(3500, match.Legends.ChancePermyriad(0));
        var pack = new Lazy<CivSim.Sim.Content.ContentPack>(() => TestPack.WithNumeric("KEY-6", "ART-10"));
        EconomyArchetype[] a = Balanced;
        MatchModel patron = MatchModel.Create(Seed, pack.Value, World.Value, a, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(patron);
        Grant(runner, patron, "KEY-5", "KEY-6", "ART-10");
        int home = Enumerable.Range(0, patron.Economy.SettlementCount).First(i => patron.Economy.SettlementLite(i).Civilization == 0);
        patron.Economy.FixtureSetPalaces(home, 1);
        Assert.Equal(7000, patron.Legends.ChancePermyriad(0));
    }

    [Fact]
    public void Legends_are_the_same_in_two_runs_and_survive_a_round_trip()
    {
        List<(long, int, int)> Run(out ulong hash, out byte[] bytes)
        {
            (MatchModel match, SimRunner runner) = Start();
            Grant(runner, match, "KEY-1");
            for (int i = 0; i < 3; i++) match.Legends.NoteDeed(AnAdult(match, i), LegendDeed.First, 150, i);
            runner.Advance(150 * 40);
            var h = new StateHasher();
            match.Legends.Hash(ref h);
            hash = h.Finish();
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) match.Legends.Write(w);
            bytes = ms.ToArray();
            return match.Legends.Records.Select(r => (r.FigureId, r.BornSeason, r.NameCode)).ToList();
        }
        var first = Run(out ulong h1, out byte[] b1);
        var second = Run(out ulong h2, out byte[] b2);
        Assert.Equal(first, second);
        Assert.Equal(h1, h2);
        Assert.Equal(b1, b2);

        (MatchModel other, _) = Start();
        using (var r = new BinaryReader(new MemoryStream(b1))) other.Legends.Read(r);
        var hh = new StateHasher();
        other.Legends.Hash(ref hh);
        Assert.Equal(h1, hh.Finish());
    }

    [Fact]
    public void The_log_is_part_of_the_match_checksum_once_it_holds_anything()
    {
        (MatchModel a, SimRunner ra) = Start();
        (MatchModel b, SimRunner rb) = Start();
        Assert.False(a.Legends.HasState);
        Assert.Equal(a.ComputeChecksum(), b.ComputeChecksum());
        b.Legends.NoteDeed(AnAdult(b), LegendDeed.First, 500, 1);
        Assert.True(b.Legends.HasState);
        Assert.NotEqual(a.ComputeChecksum(), b.ComputeChecksum());
    }

    [Fact]
    public void Names_are_stable_and_cover_the_tables()
    {
        Assert.Equal(LegendNames.Compose(0), LegendNames.Compose(0));
        Assert.True(LegendNames.Count > 1000);
        var seen = new HashSet<string>(Enumerable.Range(0, LegendNames.Count).Select(LegendNames.Compose));
        Assert.True(seen.Count > 200, "many distinct names");
        Assert.Equal(LegendNames.Compose(0), LegendNames.Compose(-5)); // a bad code falls back, never throws
    }

    [Fact]
    public void The_frame_shows_the_viewers_Legends_only()
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        match.Legends.NoteDeed(AnAdult(match), LegendDeed.Founder, 300, 2);
        for (int i = 0; i < 40 && match.Legends.Records.Count == 0; i++) runner.Advance(150);
        Assert.True(match.Legends.Records.Count >= 1);
        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);
        Assert.Equal(match.Legends.Records.Count(r => r.Civilization == 0), frame.LegendCount);
        var other = new MatchFrame();
        new FrameBuilder(match, 1).Build(match, other);
        Assert.Equal(0, other.LegendCount);
    }

    [Fact]
    public void A_match_snapshot_carries_the_Legends_and_a_match_with_none_writes_the_same_bytes_as_before()
    {
        (MatchModel empty, _) = Start();
        using (var plain = new MemoryStream())
        {
            using (var w = new BinaryWriter(plain, System.Text.Encoding.UTF8, leaveOpen: true)) empty.WriteSnapshot(w);
            using var again = new MemoryStream();
            using (var w = new BinaryWriter(again, System.Text.Encoding.UTF8, leaveOpen: true)) empty.Legends.WriteOptionalTrailer(w);
            Assert.Equal(0, again.Length); // nothing to keep: no trailer
        }

        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1");
        match.Legends.NoteDeed(AnAdult(match), LegendDeed.First, 400, 3);
        runner.Advance(150 * 30);
        Assert.True(match.Legends.Records.Count >= 1);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(w);
        ms.Position = 0;
        MatchModel restored = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var r = new BinaryReader(ms)) restored.ReadSnapshot(r);
        var a = new StateHasher(); match.Legends.Hash(ref a);
        var b = new StateHasher(); restored.Legends.Hash(ref b);
        Assert.Equal(a.Finish(), b.Finish());
        Assert.Equal(match.Legends.Records.Count, restored.Legends.Records.Count);
    }

    // ---- L1b deeds ------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_won_battle_scores_its_survivors_and_the_strongest_most_and_a_bloodless_one_scores_nothing()
    {
        (MatchModel match, _) = Start();
        var ids = new List<long>();
        for (int i = 0; i < match.Economy.FigureCount && ids.Count < 8; i++)
            if (match.Economy.Figure(i).Civilization == 0) ids.Add(match.Economy.Figure(i).Id);
        Assert.True(ids.Count >= 8);
        var attackers = new List<Fighter> { new(ids[0], Fix64.FromInt(2)), new(ids[1], Fix64.FromInt(5)), new(ids[2], Fix64.FromInt(3)) };
        var defenders = new List<Fighter> { new(ids[3], Fix64.FromInt(2)), new(ids[4], Fix64.FromInt(2)) };

        var won = new BattleResult { Winner = BattleWinner.Attacker };
        won.AttackerDead.Add(ids[2]);
        won.DefenderDead.Add(ids[3]);
        match.Politics.NoteBattleDeeds(attackers, defenders, won, 1);
        Assert.Equal(LegendSystem.BattlePoints, match.Legends.ScoreOf(ids[0]));
        Assert.Equal(LegendSystem.BattlePoints + LegendSystem.BattleBestBonus, match.Legends.ScoreOf(ids[1])); // the strongest survivor
        Assert.Equal(0, match.Legends.ScoreOf(ids[2]));                                                          // fell
        Assert.Equal(0, match.Legends.ScoreOf(ids[3]));                                                          // the losing side
        Assert.Equal(0, match.Legends.ScoreOf(ids[4]));

        var bloodless = new BattleResult { Winner = BattleWinner.Attacker };
        match.Politics.NoteBattleDeeds(attackers, defenders, bloodless, 1);
        var standOff = new BattleResult { Winner = BattleWinner.StandOff };
        standOff.AttackerDead.Add(ids[0]);
        match.Politics.NoteBattleDeeds(attackers, defenders, standOff, 1);
        Assert.Equal(LegendSystem.BattlePoints, match.Legends.ScoreOf(ids[0])); // unchanged

        var lost = new BattleResult { Winner = BattleWinner.Defender };
        lost.AttackerDead.Add(ids[0]);
        match.Politics.NoteBattleDeeds(attackers, defenders, lost, 1);
        Assert.Equal(LegendSystem.BattlePoints + LegendSystem.BattleBestBonus, match.Legends.ScoreOf(ids[3])); // the defenders won this one: the first survivor of equal strength is best
        Assert.Equal(LegendSystem.BattlePoints, match.Legends.ScoreOf(ids[4]));
        Assert.Equal(LegendSystem.BattlePoints, match.Legends.ScoreOf(ids[0]));                                // the losers' scores are untouched
    }

    [Fact]
    public void A_settlement_that_comes_through_a_famine_scores_its_leading_elder()
    {
        (MatchModel match, _) = Start();
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(i => match.Economy.SettlementLite(i).Civilization == 0);
        int people = match.Economy.Population(match.Economy.SettlementLite(home).Id);
        var elders = new List<FigureView>();
        match.Economy.ListFigures(home, elders);
        long eldest = elders.Where(f => f.Age is >= 14 and <= 70).OrderByDescending(f => f.Age).ThenBy(f => f.Id).First().Id;

        Fix64 starving = Fix64.FromRatio(3, 10), fed = Fix64.FromRatio(9, 10);
        match.Legends.StepFamine(home, people, starving);
        match.Legends.StepFamine(home, people, starving);
        match.Legends.StepFamine(home, people, fed);          // two hungry seasons are not a famine
        Assert.Equal(0, match.Legends.ScoreOf(eldest));

        match.Legends.StepFamine(home, people, starving);
        match.Legends.StepFamine(home, people, starving);
        match.Legends.StepFamine(home, people, starving);
        match.Legends.StepFamine(home, people, Fix64.FromRatio(65, 100)); // better, but not recovered yet
        Assert.Equal(0, match.Legends.ScoreOf(eldest));
        match.Legends.StepFamine(home, people, fed);
        Assert.Equal(LegendSystem.FamineHeldPoints, match.Legends.ScoreOf(eldest));

        long other = elders.First(f => f.Id != eldest).Id;
        match.Legends.StepFamine(home, 100, starving);
        match.Legends.StepFamine(home, 100, starving);
        match.Legends.StepFamine(home, 100, starving);
        match.Legends.StepFamine(home, 70, fed);               // a third of the people were lost: no deed
        Assert.Equal(LegendSystem.FamineHeldPoints, match.Legends.ScoreOf(eldest));
        Assert.Equal(0, match.Legends.ScoreOf(other));
    }

    [Fact]
    public void Raising_a_Wonder_scores_the_most_skilled_adult_of_the_settlement()
    {
        var pack = new Lazy<CivSim.Sim.Content.ContentPack>(() => TestPack.WithNumeric("KEY-6"));
        MatchModel match = MatchModel.Create(Seed, pack.Value, World.Value, Balanced, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(match);
        Grant(runner, match, "KEY-5", "KEY-6");
        int home = Enumerable.Range(0, match.Economy.SettlementCount).First(i => match.Economy.SettlementLite(i).Civilization == 0);
        int born = match.Legends.Records.Count;
        var residents = new List<FigureView>();
        match.Economy.ListFigures(home, residents);
        Dictionary<long, int> was = residents.ToDictionary(f => f.Id, f => match.Legends.ScoreOf(f.Id));

        match.Economy.FixtureCompleteIron(home, "great_library");
        int raised = residents.Count(f => match.Legends.ScoreOf(f.Id) - was[f.Id] == LegendSystem.WonderPoints);
        Assert.Equal(1, raised);
        Assert.Equal(born, match.Legends.Records.Count);

        match.Economy.FixtureCompleteIron(home, "harbor"); // not a Wonder: nobody scores again
        Assert.Equal(1, residents.Count(f => match.Legends.ScoreOf(f.Id) != was[f.Id]));
    }

    // ---- L2 effects -----------------------------------------------------------------------------------------------------------------------------

    /// <summary>A match in which the player's civilization has KEY-3 and one adult has become a Legend; returns the Legend's id and settlement.</summary>
    private static (MatchModel Match, SimRunner Runner, long Legend, int Settlement) WithLegend(int skip = 0)
    {
        (MatchModel match, SimRunner runner) = Start();
        Grant(runner, match, "KEY-1", "KEY-2", "KEY-3");
        long id = AnAdult(match, skip);
        match.Legends.NoteDeed(id, LegendDeed.First, 1000, 1);
        for (int i = 0; i < 40 && !match.Legends.IsLegend(id); i++) runner.Advance(150);
        Assert.True(match.Legends.IsLegend(id));
        Assert.True(match.Economy.TryFigure(id, out FigureView who));
        return (match, runner, id, who.SettlementId);
    }

    [Fact]
    public void A_living_Legend_adds_two_Cohesion_points_to_their_settlement_up_to_four()
    {
        (MatchModel match, SimRunner runner, long legend, int home) = WithLegend();
        Assert.True(match.Legends.IsLivingLegend(legend));
        Assert.Equal(2, match.Legends.CohesionPoints(home));
        int target = match.Economy.FixtureCohesionTarget(home);

        // Remove the effect (without touching the figure) by comparing with a settlement that has none.
        int other = Enumerable.Range(0, match.Economy.SettlementCount).First(i => i != home && match.Economy.SettlementLite(i).Civilization == 0 && match.Legends.CohesionPoints(i) == 0);
        Assert.Equal(0, match.Legends.CohesionPoints(other));
        Assert.Equal(target, match.Economy.FixtureCohesionTarget(home)); // stable

        // Two more Legends in the same settlement: capped at four.
        var more = new List<long>();
        for (int i = 0; i < match.Economy.FigureCount && more.Count < 3; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization == 0 && f.SettlementId == home && f.Id != legend && f.Age is >= 18 and <= 40) more.Add(f.Id);
        }
        foreach (long id in more) match.Legends.NoteDeed(id, LegendDeed.First, 1000, 1);
        for (int i = 0; i < 60; i++) runner.Advance(150);
        Assert.True(match.Legends.CohesionPoints(home) <= LegendSystem.CohesionPointsCap);
        Assert.Equal(Math.Min(LegendSystem.CohesionPointsCap, 2 * match.Legends.LivingInSettlement(home)), match.Legends.CohesionPoints(home));
    }

    [Fact]
    public void The_Cohesion_target_rises_by_the_Legend_points_and_a_death_is_mourned_once()
    {
        (MatchModel match, _, long legend, int home) = WithLegend();
        int with = match.Economy.FixtureCohesionTarget(home);
        int points = match.Legends.CohesionPoints(home);
        Assert.True(points >= 2);
        ILegendEffects? effects = match.Economy.LegendEffects;
        match.Economy.LegendEffects = null; // the same settlement, as if no Legend lived there
        int without = match.Economy.FixtureCohesionTarget(home);
        match.Economy.LegendEffects = effects;
        Assert.Equal(points, with - without);

        Fix64 before = match.Economy.SettlementCohesion(home);
        match.Economy.NoteMourning(home, LegendSystem.MourningPoints);
        Fix64 after = match.Economy.SettlementCohesion(home);
        Assert.True(after < before, "mourning lowers the stored Cohesion");
        Assert.Equal(Fix64.FromRatio(6, 1000) * Fix64.FromInt(LegendSystem.MourningPoints), before - after);
        Assert.True(with > 0);
        Assert.True(match.Legends.IsLivingLegend(legend));
    }

    [Fact]
    public void A_Legends_death_is_marked_and_mourned_and_ends_their_effects()
    {
        (MatchModel match, SimRunner runner, long legend, int home) = WithLegend();
        Assert.True(match.Economy.KillFigure(legend));
        Fix64 before = match.Economy.SettlementCohesion(home);
        for (int i = 0; i < 2; i++) runner.Advance(150);
        LegendRecord record = match.Legends.Records.Single(r => r.FigureId == legend);
        Assert.True(record.DiedSeason >= 0);
        Assert.False(match.Legends.IsLivingLegend(legend));
        Assert.Equal(0, match.Legends.CohesionPoints(home) - 2 * match.Legends.LivingInSettlement(home));
        Assert.True(match.Economy.SettlementCohesion(home) <= before + Fix64.FromRatio(6, 1000) * Fix64.FromInt(20), "the dip is small and closes");
    }

    [Fact]
    public void The_adults_around_a_Legend_practise_ten_percent_faster_but_the_Legend_does_not()
    {
        (MatchModel match, _, long legend, int home) = WithLegend();
        var residents = new List<FigureView>();
        match.Economy.ListFigures(home, residents);
        long neighbour = residents.First(f => f.Id != legend && f.Age >= 14).Id;
        Fix64 work = Fix64.FromInt(10);
        Assert.Equal(work * Fix64.FromRatio(11, 10), match.Economy.FixtureSkillSpreadWork(neighbour, work));
        Assert.Equal(work, match.Economy.FixtureSkillSpreadWork(legend, work));

        int far = Enumerable.Range(0, match.Economy.SettlementCount).FirstOrDefault(i => i != home && match.Legends.CohesionPoints(i) == 0, -1);
        if (far >= 0)
        {
            var others = new List<FigureView>();
            match.Economy.ListFigures(far, others);
            if (others.Count > 0) Assert.Equal(work, match.Economy.FixtureSkillSpreadWork(others[0].Id, work));
        }
    }

    [Fact]
    public void A_Legend_fights_as_a_warrior_at_one_and_a_half_and_steadies_the_group()
    {
        (MatchModel match, _, long legend, int home) = WithLegend();
        List<Fighter> defenders = match.Politics.OwnDefendersOf(0, home).ToList();
        Fighter mine = defenders.FirstOrDefault(f => f.Id == legend);
        Assert.Equal(legend, mine.Id);
        Assert.True(mine.Steady);
        Fighter? neighbour = defenders.FirstOrDefault(f => f.Id != legend && !f.Steady);
        Assert.NotNull(neighbour);
        Assert.True(mine.Strength > neighbour.Value.Strength, $"a Legend ({mine.Strength}) fights harder than the levy beside them ({neighbour.Value.Strength})");
    }

    [Fact]
    public void A_group_with_a_Legend_breaks_ten_points_later()
    {
        var setup = new BattleSetup(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10), 12, false);
        List<Fighter> Force(int n, bool steady) => Enumerable.Range(0, n).Select(i => new Fighter(i, Fix64.FromInt(2)) { Steady = steady && i == 0 }).ToList();
        var defenders = Enumerable.Range(100, 14).Select(i => new Fighter(i, Fix64.FromInt(3))).ToList();
        int plain = 0, steady = 0;
        for (ulong stream = 1; stream <= 40; stream++)
        {
            plain += Combat.Fight(Force(12, false), defenders, setup, Seed, stream).Rounds;
            steady += Combat.Fight(Force(12, true), defenders, setup, Seed, stream).Rounds;
        }
        Assert.True(steady >= plain, $"a steadied line fights at least as long ({steady} against {plain} rounds in total)");
        Assert.True(steady > plain, "and longer in some of the fights");
    }

    // ---- L3: memory ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>Makes <paramref name="count"/> adults of the Legend's settlement into Legends too, so a settlement has several to lose.</summary>
    private static List<long> MoreLegends(MatchModel match, SimRunner runner, long first, int home, int count)
    {
        var ids = new List<long> { first };
        for (int i = 0; i < match.Economy.FigureCount && ids.Count < count + 1; i++)
        {
            FigureView f = match.Economy.Figure(i);
            if (f.Civilization == 0 && f.SettlementId == home && !ids.Contains(f.Id) && f.Age is >= 18 and <= 40) ids.Add(f.Id);
        }
        foreach (long id in ids.Skip(1)) match.Legends.NoteDeed(id, LegendDeed.First, 1000, 1);
        for (int i = 0; i < 40 && ids.Any(id => !match.Legends.IsLegend(id)); i++) runner.Advance(150);
        return ids.Where(match.Legends.IsLegend).ToList();
    }

    private static LegendRecord Record(MatchModel match, long id) => match.Legends.Records.Single(r => r.FigureId == id);

    [Fact]
    public void Without_a_relief_a_dead_Legends_bonus_ends_with_them()
    {
        (MatchModel match, SimRunner runner, long legend, int home) = WithLegend();
        Assert.True(match.Economy.KillFigure(legend));
        runner.Advance(150 * 2);
        Assert.False(match.Legends.IsRemembered(Record(match, legend)));
        Assert.Equal(2 * match.Legends.LivingInSettlement(home), match.Legends.CohesionPoints(home));
    }

    [Fact]
    public void ART06s_relief_keeps_one_dead_Legends_bonus_in_the_settlement_and_a_statue_in_a_City_keeps_a_second()
    {
        (MatchModel match, SimRunner runner, long first, int home) = WithLegend();
        Grant(runner, match, "ART-06");
        List<long> legends = MoreLegends(match, runner, first, home, 2);
        Assert.True(legends.Count >= 3, "three Legends in the home settlement");
        int living = match.Legends.LivingInSettlement(home);

        Assert.True(match.Economy.KillFigure(legends[0]));
        runner.Advance(150 * 2);
        Assert.True(match.Legends.IsRemembered(Record(match, legends[0])), "the first to die fills the relief");
        Assert.Equal(Math.Min(LegendSystem.CohesionPointsCap, 2 * (match.Legends.LivingInSettlement(home) + 1)), match.Legends.CohesionPoints(home));

        Assert.True(match.Economy.KillFigure(legends[1]));
        runner.Advance(150 * 2);
        Assert.False(match.Legends.IsRemembered(Record(match, legends[1])), "one relief a settlement; a Town has no statue");
        Assert.True(match.Legends.IsRemembered(Record(match, legends[0])));

        // MET-06's bronze statue in a City remembers a second.
        Grant(runner, match, "MET-06");
        match.Economy.FixtureSetTier(home, 2);
        runner.Advance(150 * 2);
        Assert.True(match.Legends.IsRemembered(Record(match, legends[1])), "a statue in a City keeps the second");
        Assert.True(match.Legends.CohesionPoints(home) <= LegendSystem.CohesionPointsCap);
        Assert.True(living >= 2);
    }

    [Fact]
    public void A_relief_learned_after_a_Legend_died_remembers_them_too()
    {
        (MatchModel match, SimRunner runner, long legend, int home) = WithLegend();
        Assert.True(match.Economy.KillFigure(legend));
        runner.Advance(150 * 2);
        int before = match.Legends.CohesionPoints(home);
        Assert.False(match.Legends.IsRemembered(Record(match, legend)));

        Grant(runner, match, "ART-06");
        runner.Advance(150 * 2);
        Assert.True(match.Legends.IsRemembered(Record(match, legend)));
        Assert.True(match.Legends.CohesionPoints(home) >= before + 2 || match.Legends.CohesionPoints(home) == LegendSystem.CohesionPointsCap);
    }

    [Fact]
    public void A_remembered_Legend_gives_no_skill_spread_and_no_warrior_effects()
    {
        (MatchModel match, SimRunner runner, long legend, int home) = WithLegend();
        Grant(runner, match, "ART-06");
        Assert.True(match.Economy.KillFigure(legend));
        runner.Advance(150 * 2);
        if (match.Legends.LivingInSettlement(home) == 0) Assert.False(match.Legends.SpreadsSkill(home), "the relief keeps the Cohesion only");
        Assert.False(match.Legends.IsLivingLegend(legend));
    }
}
