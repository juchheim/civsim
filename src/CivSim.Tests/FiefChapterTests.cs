using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>
/// The Faith &amp; Fief chapter mode (docs/MVP/27, M12 WP-E5-4): a third, opt-in mode that contains the Iron chapter, opens E5 after a civilization's own
/// KEY-5, ends on the human's KEY-6 and starts no clock for a rival's. Plumbing only: no E5 system runs, and every Bronze and Iron byte, hash and
/// end rule is unchanged.
/// </summary>
public class FiefChapterTests
{
    private const int Season = ResearchSystem.SeasonTicks;

    // ---- the rules ----------------------------------------------------------------------------------

    [Fact]
    public void Fief_rules_open_E5_contain_Iron_and_keep_the_economy_ceiling()
    {
        ChapterRules fief = ChapterRules.Fief;
        Assert.Equal(ChapterMode.Fief, fief.Mode);
        Assert.Equal((byte)2, (byte)ChapterMode.Fief);   // appended after Iron (1); never renumbered
        Assert.Equal(ResearchSystem.LastResearchEra + 2, fief.LastResearchEra);
        Assert.Equal(5, fief.LastResearchEra);
        Assert.Equal(ResearchSystem.LastResearchEra, fief.EconomyMaxEra);
        Assert.True(fief.IsIron);   // every Iron system runs in Fief
        Assert.True(fief.IsFief);
        Assert.True(fief.HasMode);
        Assert.False(fief.HasRivalCountdown);
        Assert.Same(fief, ChapterRules.For(ChapterMode.Fief));

        Assert.True(ChapterRules.Iron.IsIron && !ChapterRules.Iron.IsFief && ChapterRules.Iron.HasMode && ChapterRules.Iron.HasRivalCountdown);
        Assert.True(!ChapterRules.Bronze.IsIron && !ChapterRules.Bronze.IsFief && !ChapterRules.Bronze.HasMode && ChapterRules.Bronze.HasRivalCountdown);
    }

    [Fact]
    public void The_Fief_end_keystone_is_KEY6_and_Fief_with_no_KEY6_has_no_end()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        Assert.Equal(catalog.IndexOf("KEY-6"), catalog.KeystoneLeaving(ChapterRules.Fief.LastResearchEra));
        ResearchRig rig = new(civs: 4, rules: ChapterRules.Fief);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.None, end.Reason);
        Assert.False(end.HasEnd);
        Assert.False(end.IsOver(long.MaxValue));
    }

    // ---- the end rule, on a pack where KEY-6 and an E5 project are numeric ------------------------------

    [Fact]
    public void A_rivals_KEY6_starts_no_clock_and_the_humans_KEY6_ends_the_chapter()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        ResearchRig rig = new(civs: 4, catalog: catalog, rules: ChapterRules.Fief);
        foreach (int civ in new[] { 0, 2 })
        {
            Grant(rig, civ, "KEY-4", season: 10);
            Grant(rig, civ, "KEY-5", season: 12);
            Assert.Equal(5, rig.Research.Era(civ));
        }

        // A rival completes KEY-6: no countdown at all.
        Grant(rig, 2, "KEY-6", season: 20);
        Assert.True(rig.Research.IsDone(2, catalog.IndexOf("KEY-6")));
        MatchEnd afterRival = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal((MatchEndReason.None, -1, -1), (afterRival.Reason, afterRival.CountdownCiv, afterRival.CountdownStartSeason));
        Assert.False(afterRival.HasEnd);
        Assert.False(MatchEnd.Evaluate(rig.Research, humanSeat: -1).HasEnd); // a headless match has no human clause and so no end

        // The human completes it: the chapter ends at the end of that season, whoever was first.
        Grant(rig, 0, "KEY-6", season: 40);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.HumanKeystone, end.Reason);
        Assert.Equal((40, 0), (end.EndSeason, end.Civ));
        Assert.True(end.IsOver((40 + 1L) * Season));
        Assert.False(end.IsOver(40L * Season));
    }

    [Fact]
    public void A_same_season_rival_KEY6_does_not_change_the_humans_Fief_end()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        ResearchRig rig = new(civs: 4, catalog: catalog, rules: ChapterRules.Fief);
        Grant(rig, 0, "KEY-5", season: 12);
        Grant(rig, 2, "KEY-5", season: 12);
        Grant(rig, 0, "KEY-6", season: 40);
        Grant(rig, 2, "KEY-6", season: 40);

        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.HumanKeystone, end.Reason);
        Assert.Equal((40, 0), (end.EndSeason, end.Civ));
        Assert.Equal((-1, -1), (end.CountdownStartSeason, end.CountdownCiv));
    }

    [Fact]
    public void E5_projects_open_only_in_Fief_only_after_the_civilizations_own_KEY5()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        int e5 = catalog.IndexOf(PromotedE5);
        Assert.True(catalog.Projects[e5].Numeric);

        // Bronze and Iron: Hidden, even for a civilization that is past KEY-4 or KEY-5.
        foreach (ChapterRules rules in new[] { ChapterRules.Bronze, ChapterRules.Iron })
        {
            ResearchRig rig = new(civs: 4, catalog: catalog, rules: rules);
            Grant(rig, 0, "KEY-4", season: 10);
            Assert.Equal(ProjectState.Hidden, rig.Research.State(0, e5));
            rig.Do(rig.Grant(0, PromotedE5));
            Assert.False(rig.Research.IsDone(0, e5)); // the grant is refused past the ceiling
        }

        // Fief: Hidden until the civilization's own KEY-5 even with its prerequisites done, then open.
        ResearchRig fief = new(civs: 4, catalog: catalog, rules: ChapterRules.Fief);
        Grant(fief, 0, "KEY-4", season: 10);
        Grant(fief, 0, "AGR-03", season: 10);
        Grant(fief, 0, "AGR-10", season: 11);
        Assert.Equal(4, fief.Research.Era(0));
        Assert.Equal(ProjectState.Hidden, fief.Research.State(0, e5));
        Grant(fief, 0, "KEY-5", season: 12);
        Assert.Equal(5, fief.Research.Era(0));
        Assert.NotEqual(ProjectState.Hidden, fief.Research.State(0, e5));
        Assert.Equal(ProjectState.Hidden, fief.Research.State(1, e5)); // another civilization is unaffected
        fief.Do(fief.Grant(0, PromotedE5));
        Assert.True(fief.Research.IsDone(0, e5));

        // E6 stays Hidden: the chapter ends at KEY-6.
        foreach (ProjectDef p in catalog.Projects)
            if (p.Era >= 6) Assert.Equal(ProjectState.Hidden, fief.Research.State(0, p.Index));
    }

    [Fact]
    public void The_forecast_catalog_bounds_E5_by_the_match_ceiling_and_reports_KEY6_only_inside_Fief()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        int e5 = catalog.IndexOf(PromotedE5), key6 = catalog.IndexOf("KEY-6");
        var iron = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog, ChapterRules.Iron.LastResearchEra);
        var fief = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog, ChapterRules.Fief.LastResearchEra);
        Assert.False(iron.IsNumeric[e5]);
        Assert.False(iron.IsNumeric[key6]);
        Assert.True(fief.IsNumeric[e5]);
        Assert.True(fief.IsNumeric[key6]);
        Assert.Contains(e5, fief.Numeric);
        Assert.DoesNotContain(e5, iron.Numeric);
    }

    // ---- hashes, snapshots and replays -----------------------------------------------------------------

    [Fact]
    public void The_three_modes_hash_differently_and_Bronze_adds_nothing()
    {
        ulong Hash(ChapterRules? rules)
        {
            var h = new StateHasher();
            new ResearchRig(civs: 4, rules: rules).Research.Hash(ref h);
            return h.Finish();
        }
        ulong bronze = Hash(null), explicitBronze = Hash(ChapterRules.Bronze), iron = Hash(ChapterRules.Iron), fief = Hash(ChapterRules.Fief);
        Assert.Equal(bronze, explicitBronze);
        Assert.NotEqual(bronze, iron);
        Assert.NotEqual(bronze, fief);
        Assert.NotEqual(iron, fief);
    }

    [Fact]
    public void A_Fief_economy_runs_the_Iron_chain_and_uses_its_own_snapshot_version()
    {
        const ulong seed = 4242;
        MatchModel bronze = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Bronze);
        MatchModel iron = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Iron);
        MatchModel fief = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Fief);
        Assert.Same(ChapterRules.Fief, fief.Rules);
        Assert.False(bronze.Economy.IronChain || bronze.Economy.FiefChain);
        Assert.True(iron.Economy.IronChain);
        Assert.False(iron.Economy.FiefChain);
        Assert.True(fief.Economy.IronChain);
        Assert.True(fief.Economy.FiefChain);

        foreach (MatchModel m in new[] { iron, fief })
            for (long t = 0; t < 3 * Season; t++) m.Step(t, default);
        byte[] ironBytes = EconomyBytes(iron), fiefBytes = EconomyBytes(fief);
        Assert.Equal(30, System.BitConverter.ToInt32(ironBytes, 4));
        Assert.Equal(47, System.BitConverter.ToInt32(fiefBytes, 4));
        Assert.NotEqual(ironBytes, fiefBytes); // E5's expanded structure state belongs to the Fief snapshot only.
        Assert.NotEqual(iron.ComputeChecksum(), fief.ComputeChecksum());
    }

    [Fact]
    public void Snapshots_carry_the_mode_and_no_mode_reads_another_modes_save()
    {
        const ulong seed = 4242;
        MatchModel Make(ChapterRules rules) => MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: rules);
        MatchModel bronze = Make(ChapterRules.Bronze), iron = Make(ChapterRules.Iron), fief = Make(ChapterRules.Fief);
        foreach (MatchModel m in new[] { bronze, iron, fief })
            for (long t = 0; t < 3 * Season; t++) m.Step(t, default);
        byte[] bronzeBytes = Snapshot(bronze), ironBytes = Snapshot(iron), fiefBytes = Snapshot(fief);
        Assert.True(fiefBytes.Length > ironBytes.Length); // Fief adds its goods and structure slots to the Iron economy payload.
        Assert.NotEqual(ironBytes, fiefBytes);
        Assert.True(fiefBytes.Length > bronzeBytes.Length);

        MatchModel fiefRestored = Make(ChapterRules.Fief);
        Restore(fiefRestored, fiefBytes);
        Assert.Equal(fief.ComputeChecksum(), fiefRestored.ComputeChecksum());

        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Iron), fiefBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Fief), ironBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Fief), bronzeBytes));
        // A Bronze match never accepts a Fief save as its own.
        MatchModel bronzeFresh = Make(ChapterRules.Bronze);
        using var stream = new MemoryStream(fiefBytes);
        using var reader = new BinaryReader(stream);
        bool accepted;
        try { bronzeFresh.ReadSnapshot(reader); accepted = stream.Position == stream.Length; }
        catch (InvalidDataException) { accepted = false; }
        Assert.False(accepted);
    }

    [Fact]
    public void Match_replays_record_the_Fief_mode_and_a_Fief_replay_does_not_verify_as_Iron()
    {
        const ulong seed = 31337;
        var seats = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        ulong fingerprint = WorldEconomyInput.FromGeneratedWorld(WorldGen(seed)).Fingerprint;
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), seats, default, rules: ChapterRules.Fief);
        var runner = new SimRunner(match);
        runner.Advance(2 * Season);
        MatchReplay replay = MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, ChapterRules.Fief);
        Assert.Same(ChapterRules.Fief, replay.Rules);
        Assert.True(replay.Verify(TestPack.Pack).Ok);

        using var bytes = new MemoryStream();
        replay.Write(bytes);
        byte[] raw = bytes.ToArray();
        Assert.Equal(MatchReplay.ModeFormatVersion, BitConverter.ToUInt32(raw, 4));
        Assert.Equal((byte)ChapterMode.Fief, raw[8]);          // the mode byte follows the format version
        bytes.Position = 0;
        MatchReplay read = MatchReplay.Read(bytes);
        Assert.Same(ChapterRules.Fief, read.Rules);
        Assert.True(read.Verify(TestPack.Pack).Ok);

        // The same recorded run, claimed to be an Iron one, must not pass: its checksums came from Fief rules.
        MatchReplay asIron = MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, ChapterRules.Iron);
        Assert.False(asIron.Verify(TestPack.Pack).Ok);

        // An unknown mode byte is refused, not guessed.
        raw[8] = 3;
        Assert.Throws<InvalidDataException>(() => MatchReplay.Read(new MemoryStream(raw)));
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    private const string PromotedE5 = "AGR-11";

    /// <summary>
    /// The pack with KEY-6 and one E5 project (AGR-11) numeric. The validator's V22 forbids a partly promoted E5, so the pack is compiled without
    /// validation, the way the E4 mode-bound test does, to exercise the mode bounds ahead of the real promotion.
    /// </summary>
    private static ContentPack PromotedPack()
    {
        var files = ContentFixture.Files();
        foreach ((string path, string id) in new[] { ("catalog/agr-agriculture.yaml", PromotedE5), ("catalog/keystones.yaml", "KEY-6") })
        {
            int index = files.FindIndex(f => f.Path == path);
            string text = files[index].Text;
            int at = text.IndexOf($"id: {id}", StringComparison.Ordinal);
            int status = text.IndexOf("status: draft", at, StringComparison.Ordinal);
            files[index] = (files[index].Path, text[..status] + "status: numeric" + text[(status + "status: draft".Length)..]);
        }
        (CivSim.Content.ContentSet set, _) = ContentFixture.Load(files);
        return ContentPack.FromTree(CivSim.Content.PackBuilder.Compile(set));
    }

    private static void Grant(ResearchRig rig, int civ, string id, int season)
    {
        rig.Tick = season * (long)Season + 5;
        rig.Do(rig.Grant(civ, id));
    }

    private static CivSim.Sim.World.GeneratedWorld WorldGen(ulong seed) => CivSim.Sim.World.WorldGenerator.Generate(seed);

    private static byte[] EconomyBytes(MatchModel m)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) m.Economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

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
