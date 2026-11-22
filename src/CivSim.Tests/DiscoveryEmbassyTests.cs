using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>DIP-09 resident embassies (M13 WP-E6-7 slice 6, docs/MVP/38 §5.3): the diffusion bonus is an embassy's, one per rival. Discovery chapter only.</summary>
public class DiscoveryEmbassyTests
{
    private static ResearchRig Rig(ChapterRules rules, params string[] granted)
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("KEY-5", "KEY-6", "DIP-09", "COM-10", "MEC-05"));
        var rig = new ResearchRig(civs: 2, catalog: catalog, rules: rules, insightPerYear: 4000);
        rig.GrantAll(0, granted);
        rig.Do(rig.Grant(1, "MEC-05"));
        rig.Research.RecordContact(0, 1);
        return rig;
    }

    private static readonly string[] WithDip = { "KEY-5", "KEY-6", "COM-10", "DIP-09" };
    private static readonly string[] WithoutDip = { "KEY-5", "KEY-6", "COM-10" };

    [Fact]
    public void The_DIP09_discount_applies_only_to_rivals_the_civilization_has_an_embassy_with()
    {
        ResearchRig embassy = Rig(ChapterRules.Discovery, WithDip);
        embassy.Research.EmbassyOracle = (_, _) => true;
        ResearchRig none = Rig(ChapterRules.Discovery, WithDip);
        none.Research.EmbassyOracle = (_, _) => false;
        ResearchRig without = Rig(ChapterRules.Discovery, WithoutDip);
        int p = embassy.P("MEC-05");
        Fix64 withEmbassy = embassy.Research.EffectiveCost(0, p), noEmbassy = none.Research.EffectiveCost(0, p), plain = without.Research.EffectiveCost(0, p);
        Assert.True(withEmbassy < noEmbassy, "an embassy deepens the discount from the rival's finished project");
        Assert.True(Fix64.Abs(noEmbassy - plain) < Fix64.FromRatio(1, 1000), "without an embassy DIP-09 adds nothing (to within fixed-point rounding)");
    }

    [Fact]
    public void Without_an_oracle_every_contacted_rival_counts_and_Fief_never_applies_the_rule()
    {
        ResearchRig open = Rig(ChapterRules.Discovery, WithDip);
        ResearchRig yes = Rig(ChapterRules.Discovery, WithDip);
        yes.Research.EmbassyOracle = (_, _) => true;
        int p = open.P("MEC-05");
        Assert.Equal(yes.Research.EffectiveCost(0, p), open.Research.EffectiveCost(0, p));

        ResearchRig fief = Rig(ChapterRules.Fief, "KEY-5", "COM-10");
        fief.Research.EmbassyOracle = (_, _) => false;
        ResearchRig fiefPlain = Rig(ChapterRules.Fief, "KEY-5", "COM-10");
        Assert.Equal(fiefPlain.Research.EffectiveCost(0, p), fief.Research.EffectiveCost(0, p));
    }
}
