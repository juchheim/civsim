using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>
/// The Discovery chapter mode (docs/MVP/38, M13 WP-E6-4): a fourth, opt-in mode that contains the Faith &amp; Fief chapter (and so the Iron chapter),
/// opens E6 after a civilization's own KEY-6, ends on the human's KEY-7 and starts no clock for a rival's. Plumbing only: no E6 system runs, and every
/// Bronze, Iron and Fief byte, hash and end rule is unchanged.
/// </summary>
public class DiscoveryChapterTests
{
    private const int Season = ResearchSystem.SeasonTicks;
    private const string PromotedE6 = "COM-11"; // requires only COM-10, so the test needs no gate or deposit

    // ---- the rules ----------------------------------------------------------------------------------

    [Fact]
    public void Discovery_rules_open_E6_contain_Fief_and_Iron_and_keep_the_economy_ceiling()
    {
        ChapterRules d = ChapterRules.Discovery;
        Assert.Equal(ChapterMode.Discovery, d.Mode);
        Assert.Equal((byte)3, (byte)ChapterMode.Discovery);   // appended after Fief (2); never renumbered
        Assert.Equal(ResearchSystem.LastResearchEra + 3, d.LastResearchEra);
        Assert.Equal(6, d.LastResearchEra);
        Assert.Equal(ResearchSystem.LastResearchEra, d.EconomyMaxEra);
        Assert.True(d.IsIron);    // every Iron system runs in Discovery
        Assert.True(d.IsFief);    // and every Faith & Fief system
        Assert.True(d.IsDiscovery);
        Assert.True(d.HasMode);
        Assert.False(d.HasRivalCountdown);
        Assert.Same(d, ChapterRules.For(ChapterMode.Discovery));

        // The earlier modes are untouched: Fief is not Discovery, Iron is not Fief, Bronze is none of them.
        Assert.True(ChapterRules.Fief.IsIron && ChapterRules.Fief.IsFief && !ChapterRules.Fief.IsDiscovery && !ChapterRules.Fief.HasRivalCountdown);
        Assert.Equal(5, ChapterRules.Fief.LastResearchEra);
        Assert.True(ChapterRules.Iron.IsIron && !ChapterRules.Iron.IsFief && !ChapterRules.Iron.IsDiscovery && ChapterRules.Iron.HasRivalCountdown);
        Assert.True(!ChapterRules.Bronze.IsIron && !ChapterRules.Bronze.IsFief && !ChapterRules.Bronze.IsDiscovery && !ChapterRules.Bronze.HasMode);
    }

    [Fact]
    public void The_Discovery_end_keystone_is_KEY7_and_Discovery_with_no_KEY7_has_no_end()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        Assert.Equal(catalog.IndexOf("KEY-7"), catalog.KeystoneLeaving(ChapterRules.Discovery.LastResearchEra));
        ResearchRig rig = new(civs: 4, rules: ChapterRules.Discovery);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.None, end.Reason);
        Assert.False(end.HasEnd);
        Assert.False(end.IsOver(long.MaxValue));
    }

    // ---- the end rule, on a pack where KEY-6, KEY-7 and one E6 project are numeric ----------------------

    [Fact]
    public void A_rivals_KEY7_starts_no_clock_and_the_humans_KEY7_ends_the_chapter()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        ResearchRig rig = new(civs: 4, catalog: catalog, rules: ChapterRules.Discovery);
        foreach (int civ in new[] { 0, 2 })
        {
            Grant(rig, civ, "KEY-4", season: 10);
            Grant(rig, civ, "KEY-5", season: 12);
            Grant(rig, civ, "KEY-6", season: 14);
            Assert.Equal(6, rig.Research.Era(civ));
        }

        // A rival completes KEY-7: no countdown at all.
        Grant(rig, 2, "KEY-7", season: 20);
        Assert.True(rig.Research.IsDone(2, catalog.IndexOf("KEY-7")));
        MatchEnd afterRival = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal((MatchEndReason.None, -1, -1), (afterRival.Reason, afterRival.CountdownCiv, afterRival.CountdownStartSeason));
        Assert.False(afterRival.HasEnd);
        Assert.False(MatchEnd.Evaluate(rig.Research, humanSeat: -1).HasEnd); // a headless match has no human clause and so no end

        // The human completes it: the chapter ends at the end of that season, whoever was first.
        Grant(rig, 0, "KEY-7", season: 40);
        MatchEnd end = MatchEnd.Evaluate(rig.Research, humanSeat: 0);
        Assert.Equal(MatchEndReason.HumanKeystone, end.Reason);
        Assert.Equal((40, 0), (end.EndSeason, end.Civ));
        Assert.True(end.IsOver((40 + 1L) * Season));
        Assert.False(end.IsOver(40L * Season));

        // KEY-6 does not end a Discovery match (it ends a Fief one).
        ResearchRig fiefRig = new(civs: 4, catalog: catalog, rules: ChapterRules.Fief);
        Grant(fiefRig, 0, "KEY-4", season: 10);
        Grant(fiefRig, 0, "KEY-5", season: 12);
        Grant(fiefRig, 0, "KEY-6", season: 14);
        Assert.Equal(MatchEndReason.HumanKeystone, MatchEnd.Evaluate(fiefRig.Research, humanSeat: 0).Reason);
        ResearchRig discoveryRig = new(civs: 4, catalog: catalog, rules: ChapterRules.Discovery);
        Grant(discoveryRig, 0, "KEY-4", season: 10);
        Grant(discoveryRig, 0, "KEY-5", season: 12);
        Grant(discoveryRig, 0, "KEY-6", season: 14);
        Assert.False(MatchEnd.Evaluate(discoveryRig.Research, humanSeat: 0).HasEnd);
    }

    [Fact]
    public void E6_projects_open_only_in_Discovery_only_after_the_civilizations_own_KEY6()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        int e6 = catalog.IndexOf(PromotedE6);
        Assert.True(catalog.Projects[e6].Numeric);

        // Bronze, Iron and Fief: Hidden, even for a civilization that is past KEY-4, KEY-5 or KEY-6.
        foreach (ChapterRules rules in new[] { ChapterRules.Bronze, ChapterRules.Iron, ChapterRules.Fief })
        {
            ResearchRig rig = new(civs: 4, catalog: catalog, rules: rules);
            Grant(rig, 0, "KEY-4", season: 10);
            if (rules.IsFief) { Grant(rig, 0, "KEY-5", season: 12); Grant(rig, 0, "KEY-6", season: 14); }
            Assert.Equal(ProjectState.Hidden, rig.Research.State(0, e6));
            rig.Do(rig.Grant(0, PromotedE6));
            Assert.False(rig.Research.IsDone(0, e6)); // the grant is refused past the ceiling
        }

        // Discovery: Hidden until the civilization's own KEY-6 even with its prerequisites done, then open.
        ResearchRig discovery = new(civs: 4, catalog: catalog, rules: ChapterRules.Discovery);
        Grant(discovery, 0, "KEY-4", season: 10);
        Grant(discovery, 0, "KEY-5", season: 12);
        Assert.Equal(5, discovery.Research.Era(0));
        Assert.Equal(ProjectState.Hidden, discovery.Research.State(0, e6));
        discovery.Do(discovery.Grant(0, PromotedE6));
        Assert.False(discovery.Research.IsDone(0, e6));
        Grant(discovery, 0, "KEY-6", season: 14);
        Assert.Equal(6, discovery.Research.Era(0));
        Assert.NotEqual(ProjectState.Hidden, discovery.Research.State(0, e6));
        Assert.Equal(ProjectState.Hidden, discovery.Research.State(1, e6)); // another civilization is unaffected
        discovery.Do(discovery.Grant(0, PromotedE6));
        Assert.True(discovery.Research.IsDone(0, e6));

        // E7 stays Hidden: the chapter ends at KEY-7.
        foreach (ProjectDef p in catalog.Projects)
            if (p.Era >= 7) Assert.Equal(ProjectState.Hidden, discovery.Research.State(0, p.Index));
    }

    [Fact]
    public void The_forecast_catalog_bounds_E6_by_the_match_ceiling_and_reports_KEY7_only_inside_Discovery()
    {
        var catalog = new ResearchCatalog(PromotedPack());
        int e6 = catalog.IndexOf(PromotedE6), key7 = catalog.IndexOf("KEY-7");
        var fief = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog, ChapterRules.Fief.LastResearchEra);
        var discovery = new CivSim.Sim.Ai.Forecast.ForecastCatalog(catalog, ChapterRules.Discovery.LastResearchEra);
        Assert.False(fief.IsNumeric[e6]);
        Assert.False(fief.IsNumeric[key7]);
        Assert.True(discovery.IsNumeric[e6]);
        Assert.True(discovery.IsNumeric[key7]);
        Assert.Contains(e6, discovery.Numeric);
        Assert.DoesNotContain(e6, fief.Numeric);
    }

    // ---- hashes, snapshots and replays -----------------------------------------------------------------

    [Fact]
    public void The_four_modes_hash_differently_and_Bronze_adds_nothing()
    {
        ulong Hash(ChapterRules? rules)
        {
            var h = new StateHasher();
            new ResearchRig(civs: 4, rules: rules).Research.Hash(ref h);
            return h.Finish();
        }
        ulong bronze = Hash(null), explicitBronze = Hash(ChapterRules.Bronze), iron = Hash(ChapterRules.Iron), fief = Hash(ChapterRules.Fief), discovery = Hash(ChapterRules.Discovery);
        Assert.Equal(bronze, explicitBronze);
        Assert.Equal(4, new[] { bronze, iron, fief, discovery }.Distinct().Count());
    }

    [Fact]
    public void A_Discovery_economy_runs_the_Fief_and_Iron_chains_and_differs_from_a_Fief_one_only_by_the_E6_registry_slots()
    {
        const ulong seed = 4242;
        MatchModel fief = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Fief);
        MatchModel discovery = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: ChapterRules.Discovery);
        Assert.Same(ChapterRules.Discovery, discovery.Rules);
        Assert.False(fief.Economy.DiscoveryChain);
        Assert.True(discovery.Economy.IronChain);
        Assert.True(discovery.Economy.FiefChain);
        Assert.True(discovery.Economy.DiscoveryChain);

        foreach (MatchModel m in new[] { fief, discovery })
            for (long t = 0; t < 3 * Season; t++) m.Step(t, default);
        // No E6 system exists yet. The Discovery payload only grows by the goods and structure slots of the E6 registry rows (the registry era
        // ceiling is 6 there), and the match checksum differs through the mode.
        Assert.True(EconomyBytes(discovery).Length >= EconomyBytes(fief).Length);
        Assert.NotEqual(fief.ComputeChecksum(), discovery.ComputeChecksum());
    }

    [Fact]
    public void Snapshots_carry_the_mode_and_no_mode_reads_another_modes_save()
    {
        const ulong seed = 4242;
        MatchModel Make(ChapterRules rules) => MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), default, default, rules: rules);
        MatchModel bronze = Make(ChapterRules.Bronze), iron = Make(ChapterRules.Iron), fief = Make(ChapterRules.Fief), discovery = Make(ChapterRules.Discovery);
        foreach (MatchModel m in new[] { bronze, iron, fief, discovery })
            for (long t = 0; t < 3 * Season; t++) m.Step(t, default);
        byte[] bronzeBytes = Snapshot(bronze), ironBytes = Snapshot(iron), fiefBytes = Snapshot(fief), discoveryBytes = Snapshot(discovery);
        Assert.NotEqual(fiefBytes, discoveryBytes); // the mode trailer differs

        MatchModel restored = Make(ChapterRules.Discovery);
        Restore(restored, discoveryBytes);
        Assert.Equal(discovery.ComputeChecksum(), restored.ComputeChecksum());

        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Fief), discoveryBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Discovery), fiefBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Discovery), ironBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Discovery), bronzeBytes));
        Assert.ThrowsAny<Exception>(() => Restore(Make(ChapterRules.Iron), discoveryBytes));
        // A Bronze match never accepts a Discovery save as its own.
        MatchModel bronzeFresh = Make(ChapterRules.Bronze);
        using var stream = new MemoryStream(discoveryBytes);
        using var reader = new BinaryReader(stream);
        bool accepted;
        try { bronzeFresh.ReadSnapshot(reader); accepted = stream.Position == stream.Length; }
        catch (InvalidDataException) { accepted = false; }
        Assert.False(accepted);
    }

    [Fact]
    public void Match_replays_record_the_Discovery_mode_and_a_Discovery_replay_does_not_verify_as_Fief()
    {
        const ulong seed = 31337;
        var seats = new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        ulong fingerprint = WorldEconomyInput.FromGeneratedWorld(WorldGen(seed)).Fingerprint;
        MatchModel match = MatchModel.Create(seed, TestPack.Pack, WorldGen(seed), seats, default, rules: ChapterRules.Discovery);
        var runner = new SimRunner(match);
        runner.Advance(2 * Season);
        MatchReplay replay = MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, ChapterRules.Discovery);
        Assert.Same(ChapterRules.Discovery, replay.Rules);
        Assert.True(replay.Verify(TestPack.Pack).Ok);

        using var bytes = new MemoryStream();
        replay.Write(bytes);
        byte[] raw = bytes.ToArray();
        Assert.Equal(MatchReplay.ModeFormatVersion, BitConverter.ToUInt32(raw, 4));
        Assert.Equal((byte)ChapterMode.Discovery, raw[8]);          // the mode byte follows the format version
        bytes.Position = 0;
        MatchReplay read = MatchReplay.Read(bytes);
        Assert.Same(ChapterRules.Discovery, read.Rules);
        Assert.True(read.Verify(TestPack.Pack).Ok);

        // The same recorded run, claimed to be a Fief one, must not pass: its checksums came from Discovery rules.
        MatchReplay asFief = MatchReplay.FromRun(seed, TestPack.Pack, seats, runner, fingerprint, ChapterRules.Fief);
        Assert.False(asFief.Verify(TestPack.Pack).Ok);

        // An unknown mode byte is refused, not guessed.
        raw[8] = 4;
        Assert.Throws<InvalidDataException>(() => MatchReplay.Read(new MemoryStream(raw)));
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    /// <summary>
    /// The pack with KEY-6, KEY-7 and one E6 project (COM-11) numeric. The validator forbids a partly promoted age, so the pack is compiled without
    /// validation, the way the E5 mode-bound test does, to exercise the mode bounds ahead of the real promotion.
    /// </summary>
    private static ContentPack PromotedPack()
    {
        var files = ContentFixture.Files();
        foreach ((string path, string id) in new[] { ("catalog/com-language-writing-communication.yaml", PromotedE6), ("catalog/keystones.yaml", "KEY-6"), ("catalog/keystones.yaml", "KEY-7") })
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
