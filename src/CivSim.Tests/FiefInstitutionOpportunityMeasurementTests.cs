using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>Matched WP6 fixture measurement of institution staffing and its research-input contribution.</summary>
public sealed class FiefInstitutionOpportunityMeasurementTests(ITestOutputHelper output)
{
    private const int SeedCount = 16;
    private const ulong FirstSeed = 20261114;

    private readonly record struct Row(int Adults, int Monks, int Scholars, Fix64 ScholarHeads, Fix64 RoleInsight, Fix64 BaseInsight);

    [Fact]
    public void Report_matched_monastery_and_university_staffing_and_research_input()
    {
        var totals = new Dictionary<string, Row>
        {
            ["baseline"] = default,
            ["monastery"] = default,
            ["university"] = default,
        };

        for (int i = 0; i < SeedCount; i++)
        {
            ulong seed = FirstSeed + (ulong)i;
            foreach (string arm in new[] { "baseline", "monastery", "university" })
            {
                Row row = Run(seed, arm);
                Row prior = totals[arm];
                totals[arm] = new Row(prior.Adults + row.Adults, prior.Monks + row.Monks,
                    prior.Scholars + row.Scholars, prior.ScholarHeads + row.ScholarHeads,
                    prior.RoleInsight + row.RoleInsight, prior.BaseInsight + row.BaseInsight);
            }
        }

        foreach ((string arm, Row r) in totals)
        {
            output.WriteLine($"{arm}: seeds={SeedCount}, adult population={r.Adults}, monks assigned={r.Monks}, scholars assigned={r.Scholars}, scholar-equivalent input heads={r.ScholarHeads}, institution role Insight/year={r.RoleInsight}, all role Insight/year={r.BaseInsight}.");
        }
        output.WriteLine($"Per-seed mean institutional adult slots diverted from ordinary work: monastery={(double)totals["monastery"].Monks / SeedCount:F2} monks; university={(double)totals["university"].Scholars / SeedCount:F2} scholars.");
    }

    private static Row Run(ulong seed, string arm)
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 100,
            "KEY-5", "BEL-10", "LRN-05", "LRN-08");
        fixture.Economy.FixtureSetTier(0, 1);
        if (arm == "monastery") fixture.Economy.FixtureBuildIronStructure(0, "monastery");
        else if (arm == "university") fixture.Economy.FixtureBuildIronStructure(0, "university");
        fixture.RunSeasons(1);

        int monks = fixture.Economy.MonkHeads(0);
        int scholars = fixture.Economy.ScholarHeads(0);
        if (arm == "monastery") Assert.Equal(4, monks);
        if (arm == "university") Assert.True(scholars > 0);
        int adults = fixture.Economy.AgeBandPopulation(0, 1);

        GeneratedWorld world = WorldGenerator.Generate(seed);
        var source = new LivingEconomySource(fixture.Economy, WorldEconomyInput.FromGeneratedWorld(world), world,
            TestPack.Catalog, ReadOnlySpan<EconomyArchetype>.Empty);
        InsightInputs input = source.GetInsightInputs(0);
        ResearchCatalog catalog = TestPack.Catalog;
        Fix64 institutionInsight = Fix64.Zero;
        foreach (string roleName in new[] { "monk", "scholar" })
        {
            int role = catalog.RoleIndex(roleName);
            institutionInsight += input.RoleHeads[role] * catalog.Roles[role].Insight;
        }
        institutionInsight *= input.RolesScale * input.InsightScale;
        Fix64 totalRoleInsight = Fix64.Zero;
        for (int role = 0; role < catalog.Roles.Count; role++)
            totalRoleInsight += input.RoleHeads[role] * catalog.Roles[role].Insight;
        totalRoleInsight *= input.RolesScale * input.InsightScale;
        return new Row(adults, monks, scholars,
            input.RoleHeads[catalog.RoleIndex("scholar")], institutionInsight, totalRoleInsight + input.StructureOutput);
    }
}
