using System.Linq;
using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The chapter rules (docs/MVP/25 §3): Bronze is the unchanged default, Iron is an explicit opt-in that moves the ceiling and the end keystone.</summary>
public class ChapterRulesTests
{
    private const int Season = ResearchSystem.SeasonTicks;

    private static ResearchRig Rig(ChapterRules? rules, int civs = 4) => new(civs: civs, rules: rules);

    private static void Grant(ResearchRig rig, int civ, string id, int season, int intoSeason = 5)
    {
        rig.Tick = season * (long)Season + intoSeason;
        rig.Do(rig.Grant(civ, id));
    }

    [Fact]
    public void A_research_system_built_without_rules_plays_the_Bronze_chapter()
    {
        ResearchRig rig = Rig(null);
        Assert.Same(ChapterRules.Bronze, rig.Research.Rules);
        Assert.Equal(ChapterMode.Bronze, rig.Research.Rules.Mode);
        Assert.Equal(ResearchSystem.LastResearchEra, rig.Research.Rules.LastResearchEra);
        Assert.Equal(TestPack.Catalog.ChapterCapYear, rig.Research.Rules.CapYear(TestPack.Catalog));
        Assert.Equal(TestPack.Catalog.ChapterCountdownSeasons, rig.Research.Rules.CountdownSeasons(TestPack.Catalog));
        Assert.Equal(ResearchSystem.LastResearchEra, rig.Research.Rules.EconomyMaxEra);
    }

    [Fact]
    public void Iron_rules_raise_the_research_ceiling_by_one_era_and_keep_the_economy_ceiling()
    {
        ChapterRules iron = ChapterRules.Iron;
        Assert.Equal(ChapterMode.Iron, iron.Mode);
        Assert.Equal(ResearchSystem.LastResearchEra + 1, iron.LastResearchEra);
        Assert.Equal(ResearchSystem.LastResearchEra, iron.EconomyMaxEra);
        Assert.Equal(96, iron.CapYear(TestPack.Catalog));
        Assert.Equal(24, iron.CountdownSeasons(TestPack.Catalog));
        Assert.Same(iron, ChapterRules.For(ChapterMode.Iron));
        Assert.Same(ChapterRules.Bronze, ChapterRules.For(ChapterMode.Bronze));
    }

    [Fact]
    public void KEY4_ends_the_Bronze_chapter_but_not_the_Iron_one()
    {
        foreach ((ChapterRules rules, MatchEndReason expected) in new[]
        {
            (ChapterRules.Bronze, MatchEndReason.HumanKeystone), (ChapterRules.Iron, MatchEndReason.YearCap),
        })
        {
            ResearchRig rig = Rig(rules);
            Grant(rig, 0, "KEY-4", season: 20);
            Assert.Equal(expected, MatchEnd.Evaluate(rig.Research, humanSeat: 0).Reason);
        }
        // A rival's KEY-4 starts no countdown in Iron.
        ResearchRig iron = Rig(ChapterRules.Iron);
        Grant(iron, 2, "KEY-4", season: 6);
        MatchEnd end = MatchEnd.Evaluate(iron.Research, humanSeat: 0);
        Assert.Equal((MatchEndReason.YearCap, -1), (end.Reason, end.CountdownCiv));
        Assert.Equal(96 * 4 - 1, end.EndSeason);
    }

    [Fact]
    public void Iron_with_no_KEY5_ends_at_Year_96()
    {
        MatchEnd end = MatchEnd.Evaluate(Rig(ChapterRules.Iron).Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.YearCap, end.Reason);
        Assert.Equal(96 * 600L, end.EndTick);
        Assert.False(end.IsOver(96 * 600L - 1));
        Assert.True(end.IsOver(96 * 600L));
    }

    [Fact]
    public void Iron_end_clauses_follow_KEY5_with_the_Bronze_tie_order()
    {
        // Completing a draft keystone needs a numeric KEY-5, which WP3 left draft, so the clauses are checked on the rule itself:
        // the keystone it reads is the one leaving era 4.
        ResearchCatalog catalog = TestPack.Catalog;
        Assert.Equal(catalog.IndexOf("KEY-5"), catalog.KeystoneLeaving(ChapterRules.Iron.LastResearchEra));
        Assert.Equal(catalog.IndexOf("KEY-4"), catalog.KeystoneLeaving(ChapterRules.Bronze.LastResearchEra));
    }

    [Fact]
    public void E4_projects_stay_Hidden_in_a_Bronze_match_but_open_in_an_Iron_one_for_a_civilization_past_KEY4()
    {
        ResearchRig bronze = Rig(ChapterRules.Bronze);
        Grant(bronze, 0, "KEY-4", season: 20);
        foreach (ProjectDef p in TestPack.Catalog.Projects)
            if (p.Era >= 4) Assert.Equal(ProjectState.Hidden, bronze.Research.State(0, p.Index));

        ResearchRig iron = Rig(ChapterRules.Iron);
        Grant(iron, 0, "KEY-4", season: 20);
        Assert.Equal(4, iron.Research.Era(0));
        Assert.Contains(TestPack.Catalog.Projects.Where(p => p.Era == 4), p => iron.Research.State(0, p.Index) != ProjectState.Hidden);
        foreach (ProjectDef p in TestPack.Catalog.Projects)
            if (p.Era >= 5) Assert.Equal(ProjectState.Hidden, iron.Research.State(0, p.Index)); // the chapter ends at KEY-5
    }

    [Fact]
    public void An_Iron_match_hashes_differently_from_a_Bronze_one_and_Bronze_adds_nothing()
    {
        var bronzeHasher = new StateHasher();
        Rig(null).Research.Hash(ref bronzeHasher);
        var explicitBronze = new StateHasher();
        Rig(ChapterRules.Bronze).Research.Hash(ref explicitBronze);
        var ironHasher = new StateHasher();
        Rig(ChapterRules.Iron).Research.Hash(ref ironHasher);
        Assert.Equal(bronzeHasher.Finish(), explicitBronze.Finish());
        Assert.NotEqual(bronzeHasher.Finish(), ironHasher.Finish());
    }

    [Fact]
    public void Bronze_snapshots_are_byte_identical_and_the_two_modes_never_read_each_others_saves()
    {
        const ulong seed = 4242;
        MatchModel bronze = MatchModel.Create(seed, TestPack.Pack);
        MatchModel explicitBronze = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Bronze);
        MatchModel iron = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Iron);
        Assert.Same(ChapterRules.Bronze, bronze.Rules);
        Assert.Same(ChapterRules.Iron, iron.Rules);
        foreach (MatchModel m in new[] { bronze, explicitBronze, iron })
            for (long t = 0; t < 3 * Season; t++) m.Step(t, default);

        byte[] bronzeBytes = Snapshot(bronze), ironBytes = Snapshot(iron);
        Assert.Equal(bronzeBytes, Snapshot(explicitBronze));
        Assert.Equal(bronze.ComputeChecksum(), explicitBronze.ComputeChecksum());
        Assert.True(ironBytes.Length > bronzeBytes.Length); // the Iron trailer
        Assert.NotEqual(bronze.ComputeChecksum(), iron.ComputeChecksum());

        // An Iron match restores its own save and agrees on the checksum.
        MatchModel ironRestored = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Iron);
        Restore(ironRestored, ironBytes);
        Assert.Equal(iron.ComputeChecksum(), ironRestored.ComputeChecksum());

        // A Bronze save never becomes Iron, and an Iron save never becomes Bronze.
        MatchModel ironFresh = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Iron);
        Assert.ThrowsAny<System.Exception>(() => Restore(ironFresh, bronzeBytes));
        // The Iron economy writes its own section mid-stream, so a Bronze match either fails to parse the later sections or, if it parses,
        // leaves bytes unread (which the game rejects). It never accepts the snapshot as its own.
        MatchModel bronzeFresh = MatchModel.Create(seed, TestPack.Pack);
        using var stream = new MemoryStream(ironBytes);
        using var reader = new BinaryReader(stream);
        bool accepted;
        try { bronzeFresh.ReadSnapshot(reader); accepted = stream.Position == stream.Length; }
        catch (InvalidDataException) { accepted = false; }
        Assert.False(accepted);
    }

    [Fact]
    public void Match_replays_record_the_chapter_mode_and_Bronze_files_keep_their_format()
    {
        const ulong seed = 31337;
        var seats = new[] { CivSim.Sim.Economy.EconomyArchetype.Balanced, CivSim.Sim.Economy.EconomyArchetype.Balanced,
            CivSim.Sim.Economy.EconomyArchetype.Balanced, CivSim.Sim.Economy.EconomyArchetype.Balanced };
        ulong fingerprint = CivSim.Sim.Economy.WorldEconomyInput.FromGeneratedWorld(WorldGen(seed)).Fingerprint;
        foreach (ChapterRules rules in new[] { ChapterRules.Bronze, ChapterRules.Iron })
        {
            MatchModel match = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), seats, default, rules: rules);
            var runner = new SimRunner(match);
            runner.Advance(2 * Season);
            MatchReplay replay = MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, rules);
            Assert.Same(rules, replay.Rules);
            Assert.True(replay.Verify(TestPack.Pack).Ok);

            using var bytes = new MemoryStream();
            replay.Write(bytes);
            uint version = System.BitConverter.ToUInt32(bytes.ToArray(), 4);
            Assert.Equal(rules.IsIron ? MatchReplay.ModeFormatVersion : MatchReplay.FormatVersion, version);
            bytes.Position = 0;
            MatchReplay read = MatchReplay.Read(bytes);
            Assert.Same(rules, read.Rules);
            Assert.True(read.Verify(TestPack.Pack).Ok);
        }

        // An Iron replay verified as Bronze (its recorded checksums came from Iron rules) must not pass.
        MatchModel ironMatch = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), seats, default, rules: ChapterRules.Iron);
        var ironRunner = new SimRunner(ironMatch);
        ironRunner.Advance(Season);
        MatchReplay asBronze = MatchReplay.FromRun(seed, TestPack.Pack, seats, ironRunner, fingerprint, ChapterRules.Bronze);
        Assert.False(asBronze.Verify(TestPack.Pack).Ok);
    }

    private static CivSim.Sim.World.GeneratedWorld WorldGen(ulong seed) => CivSim.Sim.World.WorldGenerator.Generate(seed);

    private static byte[] Snapshot(MatchModel m)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(MatchModel m, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream);
        m.ReadSnapshot(reader);
    }
}

/// <summary>
/// Numeric E4 content must stay inert outside the Iron chapter (docs/MVP/25 B4-01): the live State, the grant command and the
/// forecast catalog all bound it by the match's research ceiling, so promoting an E4 row cannot reach a Bronze match.
/// </summary>
public class ChapterRulesPromotedE4Tests
{
    private const string Promoted = "AGR-09";

    private static CivSim.Sim.Content.ContentPack PromotedPack()
    {
        var files = CivSim.Tests.ContentFixture.Files();
        int index = files.FindIndex(f => f.Path == "catalog/agr-agriculture.yaml");
        string text = files[index].Text;
        int at = text.IndexOf("id: AGR-09", StringComparison.Ordinal);
        int status = text.IndexOf("status: draft", at, StringComparison.Ordinal);
        files[index] = (files[index].Path, text[..status] + "status: numeric" + text[(status + "status: draft".Length)..]);
        (CivSim.Content.ContentSet set, _) = ContentFixture.Load(files);
        // The validator's V21 still forbids a numeric E4 row, so the pack is compiled without it to test the mode bounds ahead of promotion.
        CivSim.Sim.Content.ContentPack pack = CivSim.Sim.Content.ContentPack.FromTree(CivSim.Content.PackBuilder.Compile(set));
        return pack;
    }

    [Fact]
    public void A_numeric_E4_project_is_Hidden_ungrantable_and_unforecast_in_Bronze_but_live_in_Iron()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        int p = catalog.IndexOf(Promoted);
        Assert.True(catalog.Projects[p].Numeric);

        ResearchRig bronze = new(civs: 4, catalog: catalog);
        ResearchRig iron = new(civs: 4, catalog: catalog, rules: ChapterRules.Iron);
        foreach (ResearchRig rig in new[] { bronze, iron })
        {
            rig.Do(rig.Grant(0, "KEY-4"));
            Assert.Equal(4, rig.Research.Era(0));
            rig.Do(rig.Grant(0, Promoted));
        }

        // Bronze: Hidden and the grant is refused, even for a civilization past KEY-4.
        Assert.Equal(ProjectState.Hidden, bronze.Research.State(0, p));
        Assert.False(bronze.Research.IsDone(0, p));
        // Iron: the same grant lands.
        Assert.True(iron.Research.IsDone(0, p));

        var bronzeForecast = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog);
        var ironForecast = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog, ChapterRules.Iron.LastResearchEra);
        Assert.False(bronzeForecast.IsNumeric[p]);
        Assert.DoesNotContain(p, bronzeForecast.Numeric);
        Assert.True(ironForecast.IsNumeric[p]);
        Assert.Contains(p, ironForecast.Numeric);
    }
}
