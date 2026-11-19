using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>Effect-specific regression coverage for E5 rows consumed by generic research/civic systems.</summary>
public sealed class FiefGenericConsumerTests
{
    private const ulong Seed = 20261116;

    [Fact]
    public void LRN08_adopt_multiplier_speeds_Fief_skill_diffusion_without_Iron_leakage()
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("LRN-08"));
        static Fix64 AdoptionAfterTwoSeasons(ResearchRig rig)
        {
            rig.Do(rig.Grant(0, "STN-01"));
            rig.Run(ResearchSystem.SeasonTicks * 2);
            return rig.Research.AdoptionFraction(0, rig.P("STN-01"));
        }

        Fix64 fiefPlain = AdoptionAfterTwoSeasons(new ResearchRig(catalog: catalog, rules: ChapterRules.Fief));
        var fief = new ResearchRig(catalog: catalog, rules: ChapterRules.Fief);
        fief.GrantAll(0, "KEY-5", "LRN-08");
        Fix64 fiefAdopt = AdoptionAfterTwoSeasons(fief);
        Fix64 ironPlain = AdoptionAfterTwoSeasons(new ResearchRig(catalog: catalog, rules: ChapterRules.Iron));
        var iron = new ResearchRig(catalog: catalog, rules: ChapterRules.Iron);
        iron.GrantAll(0, "KEY-5", "LRN-08");
        Fix64 ironAdopt = AdoptionAfterTwoSeasons(iron);

        Assert.True(fiefAdopt > fiefPlain, $"LRN-08 should increase Fief adoption by season two: {fiefAdopt} vs {fiefPlain}");
        Assert.Equal(ironPlain, ironAdopt);
    }

    [Fact]
    public void COM10_commons_insight_multiplier_is_Fief_only()
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("COM-10"));
        static (Fix64 Insight, Fix64 Modifier, Fix64 Adoption) Insight(ResearchCatalog c, ChapterRules rules, bool grant)
        {
            var rig = new ResearchRig(catalog: c, rules: rules);
            rig.GrantAll(0, "KEY-5");
            if (grant) rig.GrantAll(0, "COM-10");
            rig.Run(ResearchSystem.SeasonTicks);
            return (rig.Research.InsightPerYear(0), rig.Research.ModifierMultiplier(0, "commons_insight"),
                rig.Research.AdoptionFraction(0, c.IndexOf("COM-10")));
        }
        (Fix64 plain, _, _) = Insight(catalog, ChapterRules.Fief, false);
        (Fix64 printed, Fix64 modifier, _) = Insight(catalog, ChapterRules.Fief, true);
        Assert.True(printed > plain);
        Assert.True(modifier > Fix64.One && modifier <= Fix64.Parse("1.15"));
        Assert.Equal(Insight(catalog, ChapterRules.Iron, false), Insight(catalog, ChapterRules.Iron, true));
    }

    [Fact]
    public void NUM09_scholar_insight_multiplier_is_Fief_only()
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("NUM-09"));
        static (Fix64 Insight, Fix64 Modifier, Fix64 Adoption) Insight(ResearchCatalog c, ChapterRules rules, bool grant)
        {
            var rig = new ResearchRig(catalog: c, rules: rules, configure: source =>
            {
                source.Adults = Fix64.Zero;
                source.Elders = Fix64.Zero;
                var heads = new Fix64[c.Roles.Count];
                heads[c.RoleIndex("scholar")] = Fix64.One;
                source.RoleHeadsOverride = heads;
            });
            rig.GrantAll(0, "KEY-5");
            rig.GrantAll(0, "LRN-05");
            if (grant) rig.GrantAll(0, "NUM-09");
            rig.Run(ResearchSystem.SeasonTicks);
            return (rig.Research.InsightPerYear(0), rig.Research.ModifierMultiplier(0, "scholar_insight"),
                rig.Research.AdoptionFraction(0, c.IndexOf("NUM-09")));
        }
        (Fix64 plain, _, _) = Insight(catalog, ChapterRules.Fief, false);
        (Fix64 algebra, Fix64 modifier, _) = Insight(catalog, ChapterRules.Fief, true);
        Assert.True(algebra > plain);
        Assert.True(modifier > Fix64.One && modifier <= Fix64.Parse("1.15"));
        Assert.Equal(Insight(catalog, ChapterRules.Iron, false), Insight(catalog, ChapterRules.Iron, true));
    }

    [Fact]
    public void TRD08_reduces_Fief_wonder_labor_by_catalog_value_without_Iron_leakage()
    {
        var pack = TestPack.WithNumeric("TRD-08");
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5");
        BronzeFixture funded = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5", "TRD-08");
        Fix64 baseLabor = plain.Economy.FixtureCivicBuildLabor(0, "ziggurat");
        Fix64 fundedLabor = funded.Economy.FixtureCivicBuildLabor(0, "ziggurat");
        Assert.Equal(baseLabor * Fix64.Parse("0.8696"), fundedLabor);

        var catalog = new ResearchCatalog(pack);
        var ironPlain = new ResearchRig(catalog: catalog, rules: ChapterRules.Iron);
        var ironFunded = new ResearchRig(catalog: catalog, rules: ChapterRules.Iron);
        ironPlain.GrantAll(0, "KEY-4");
        ironFunded.GrantAll(0, "KEY-4", "TRD-08");
        Assert.Equal(Fix64.One, ironFunded.Research.ModifierMultiplier(0, "wonder_build_labor"));
    }
}
