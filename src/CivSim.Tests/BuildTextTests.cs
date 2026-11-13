using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.Numerics;
using CivSim.Sim.View;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>The wording of the build panel and the way the frame carries it (docs/MVP/30).</summary>
public sealed class BuildTextTests
{
    private const ulong Seed = 20261019;
    private readonly ITestOutputHelper _output;

    public BuildTextTests(ITestOutputHelper output) => _output = output;

    private static BronzeFixture Bronze(params string[] extra)
        => BronzeFixture.Create(TestPack.Pack, Seed, 60, false, true, true, true, extra);

    private static readonly BuildNames Names = new(
        id => id.Replace('_', ' '), id => id.StartsWith("G-", StringComparison.Ordinal) ? id[2..].ToLowerInvariant() : id, p => p);

    private static void Foundation(LivingEconomyModel e)
    {
        e.FixtureSetTier(0, 0);
        foreach (string id in new[] { "storage_pit", "drying_rack", "hearth", "lean_to", "pit_house" })
            if (e.StructureCount(0, LivingEconomyModel.StructureIndex(id)) == 0) e.FixtureBuildStructure(0, id);
        e.FixtureFillFields(0);
    }

    [Fact]
    public void A_short_good_reads_in_plain_words_with_the_numbers()
    {
        BronzeFixture f = Bronze();
        Foundation(f.Economy);
        BuildItem granary = f.Economy.BuildOutlookFor(0).Items.First(i => i.Id == "granary");
        string[] lines = BuildText.Explain(granary, Names).ToArray();
        foreach (string line in lines) _output.WriteLine(line);
        Assert.Contains("granary: wanted, but the town is short of brick (0 of 80).", lines);
        Assert.Contains(lines, l => l.StartsWith("The town is already gathering brick", StringComparison.Ordinal));
        Assert.Equal("brick 0 of 80", BuildText.Short(granary, Names));
    }

    [Fact]
    public void An_unresolvable_stop_gives_the_exact_cause_and_says_nothing_can_be_ordered()
    {
        BronzeFixture f = Bronze();
        LivingEconomyModel e = f.Economy;
        Foundation(e);
        e.FixtureSetStructureCount(0, "hearth", 0);
        e.FixtureStock(0, "G-STONE", -e.GoodStock(0, "G-STONE"));
        e.FixtureSetRangeStone(0, Fix64.Zero);
        BuildItem hearth = e.BuildOutlookFor(0).Items.First(i => i.Id == "hearth");
        string verdict = BuildText.Verdict(hearth, Names);
        Assert.Equal("There is no stone left within three tiles of this town, and stone does not grow back. Nothing you can order changes that here.", verdict);
        // A rule rather than a good:
        BronzeFixture small = BronzeFixture.Create(TestPack.Pack, Seed, 15, false, true, true, true, "BEL-05");
        BuildItem shrine = small.Economy.BuildOutlookFor(0).Items.First(i => i.Id == "shrine");
        Assert.Equal("The town needs 20 people first. Nothing you can order changes that here.", BuildText.Verdict(shrine, Names));
    }

    [Fact]
    public void The_headline_names_the_structure_the_town_waits_on()
    {
        BronzeFixture f = BronzeFixture.CreateIron(TestPack.Pack, Seed, 60, "BEL-06");
        f.Economy.FixtureSetTier(0, 1);
        f.Economy.FixtureSetCivicBuildTarget(0, "temple");
        BuildOutlook o = f.Economy.BuildOutlookFor(0);
        Assert.Equal("Waiting on materials for the temple.", BuildText.Headline(o, Names));
        string[] lines = BuildText.Explain(o.Items.First(i => i.Id == "temple"), Names).ToArray();
        foreach (string line in lines) _output.WriteLine(line);
        Assert.Contains("Learn STN-11.", lines);
        Assert.Contains(lines, l => l.Contains("Set it aside for now", StringComparison.Ordinal));
    }

    [Fact]
    public void Learned_but_unbuilt_structures_are_listed_across_towns_with_the_commonest_reason()
    {
        BronzeFixture f = Bronze();
        Foundation(f.Economy);
        BuildOutlook one = f.Economy.BuildOutlookFor(0);
        System.Collections.Generic.IReadOnlyList<BuildText.NotBuilt> list = BuildText.NotBuiltAnywhere(new[] { one, one }, Names);
        foreach (BuildText.NotBuilt n in list) _output.WriteLine($"{n.Id}: {n.Reason}");
        BuildText.NotBuilt granary = list.First(n => n.Id == "granary");
        Assert.Equal(2, granary.Towns);
        Assert.Equal("short of brick in 2 towns", granary.Reason);
        Assert.DoesNotContain(list, n => n.Id == "kiln"); // built, so not "unbuilt"
    }

    [Fact]
    public void The_frame_carries_the_outlook_of_each_own_town_and_nothing_elsewhere()
    {
        MatchModel match = ViewKit.NewMatch();
        new SimRunner(match).Advance(3000);
        var builder = new FrameBuilder(match, 0);
        var frame = new MatchFrame();
        frame.Reserve(4096, 128, match.Economy.WildHerdCount);
        builder.Build(match, frame);
        Assert.All(frame.SettlementBuild.Take(frame.SettlementCount), outlook => Assert.Null(outlook)); // off until the Building tab asks
        builder.WantBuildOutlooks = true;
        builder.Build(match, frame);
        int own = 0;
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            var flags = (SettlementViewFlags)frame.SettlementFlags[w];
            bool ours = (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            if (ours)
            {
                own++;
                BuildOutlook outlook = Assert.IsType<BuildOutlook>(frame.SettlementBuild[w]);
                Assert.Equal(frame.SettlementIndex[w], outlook.Settlement);
                Assert.Equal(frame.Season, outlook.Season);
            }
            else Assert.Null(frame.SettlementBuild[w]);
        }
        Assert.True(own > 0);
    }
}
