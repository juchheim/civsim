using System.Collections.Generic;
using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;

namespace CivSim.Tests;

/// <summary>
/// The E3 Bronze content (docs/MVP/23): numeric nodes, the new effect kinds, the recipes registry, the widened
/// goods/tools/roles schemas and the new validator rules (V11 alternate route, V19 [Has] gates, V20 Decrees).
/// </summary>
public class E3ContentTests
{
    private const string Met = "catalog/met-metallurgy.yaml";

    private static (ContentSet Set, Report Report) Validated()
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        Validator.Run(set, report);
        return (set, report);
    }

    [Fact]
    public void The_real_content_is_clean_under_strict_validation()
    {
        (_, Report report) = Validated();
        Assert.True(report.ErrorCount == 0 && report.WarningCount == 0, ContentFixture.Describe(report));
    }

    [Fact]
    public void All_66_E3_nodes_are_numeric_with_effects_practice_flavor_and_moment()
    {
        (ContentSet set, _) = Validated();
        var e3 = set.Regular.Where(p => p.Era == 3).Append(set.ById["KEY-4"]).ToList();
        Assert.Equal(66, e3.Count);
        foreach (Project p in e3)
        {
            Assert.True(p.Status == "numeric", $"{p.Id} is {p.Status}");
            Assert.NotEmpty(p.Effects);
            Assert.DoesNotContain(p.Effects, e => e.Kind == "prose");
            Assert.False(string.IsNullOrWhiteSpace(p.Flavor), $"{p.Id} has no flavor");
            Assert.False(string.IsNullOrWhiteSpace(p.Moment), $"{p.Id} has no moment");
            if (!p.IsKeystone) Assert.True(p.Practice.Sum(x => x.Share) == 100, $"{p.Id} practice");
        }
    }

    [Fact]
    public void KEY3_carries_the_E3_switches()
    {
        (ContentSet set, _) = Validated();
        Project k = set.ById["KEY-3"];
        Effect mandate = k.Effects.Single(e => e.Kind == "mandate");
        Assert.Equal(1, mandate.Int("start"));
        Assert.Equal(2, mandate.Int("cap"));
        Assert.Equal(8, mandate.Int("recharge"));
        Assert.Equal(4, k.Effects.Single(e => e.Kind == "admin").Int("span"));
        Assert.Contains(k.Effects, e => e.Kind == "unlock_action" && e.Str("action") == "mine");
        Assert.Contains(k.Effects, e => e.Kind == "build" && e.Str("structure") == "mining_camp");
    }

    [Fact]
    public void Current_research_cadence_values_are_in_the_content()
    {
        (ContentSet set, _) = Validated();
        Assert.Equal(400, set.Reg("eras").Find("3")!.GetInt("base_cost"));
        Assert.Equal(CivSim.Sim.Numerics.Fix64.FromInt(400), set.CostOf(set.ById["MET-03"]));
        Assert.Equal(CivSim.Sim.Numerics.Fix64.FromInt(1000), set.CostOf(set.ById["KEY-4"]));
        Assert.Equal(90, set.ById["SOC-09"].Effects.Single(e => e.Kind == "tier").Int("min_pop"));
        Assert.Equal(60, set.ById["TRD-04"].Effects.Single(e => e.Kind == "tier").Int("min_pop"));
        Assert.Equal(CivSim.Sim.Numerics.Fix64.Parse("0.4"), set.ById["AGR-05"].Effects.Single(e => e.Str("stat") == "field_limit").Num("value"));
    }

    [Fact]
    public void Mandate_and_admin_sums_match_the_spec()
    {
        (ContentSet set, _) = Validated();
        long Sum(string kind, string field) => set.Projects.Where(p => p.Status == "numeric")
            .SelectMany(p => p.Effects).Where(e => e.Kind == kind).Sum(e => e.Int(field) ?? 0);
        Assert.Equal(15, Sum("admin", "span"));       // 23 §20.7: 4 + 2 + 3 + 2 + 2 + 2
        Assert.Equal(4, Sum("mandate", "cap"));        // cap 2 + GOV-05 + GOV-06
        Assert.Equal(4, Sum("mandate", "recharge"));   // KEY-3 +8, then -2 -1 -1: the floor of 4
    }

    [Fact]
    public void Recipes_registry_loads_and_references_resolve()
    {
        (ContentSet set, Report report) = Validated();
        Registry recipes = set.Reg("recipes");
        Assert.Equal(30, recipes.Records.Count); // 26 of docs/MVP/23 §5 + 4 doc-10 supply crafts (lime, pigment, yarn, cloth; WP6)
        ContentValue smelt = recipes.Find("smelt_copper")!;
        Assert.Equal(20, smelt.GetInt("order"));
        Assert.True(smelt.Get("bellows")!.BoolValue);
        Assert.Equal(new[] { "kiln", "smelter" }, smelt.GetStringList("structure"));
        Assert.Equal(CivSim.Sim.Numerics.Fix64.FromRatio(1, 2), smelt.Get("outputs")!.GetFix("G-COPPER")!.Value);
        Assert.NotNull(recipes.Find("build_chariot")!.Get("inputs")!.Get("G-BRONZE|G-ABRONZE"));
        Assert.Equal(0, report.ErrorCount);
    }

    [Fact]
    public void The_pack_carries_recipes_roles_and_new_effect_kinds()
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        ContentPack? pack = PackBuilder.Build(set, report);
        Assert.True(pack is not null, ContentFixture.Describe(report));
        ContentValue regs = pack!.Root.Get("registries")!;
        Assert.Equal(30, regs.Get("recipes")!.Items.Count);
        ContentValue scribe = regs.Get("roles")!.Items.Single(r => r.GetString("id") == "scribe");
        Assert.Equal("COM-05", scribe.GetString("unlocked_by"));
        Assert.NotNull(scribe.GetString("staffing"));
        ContentValue t10 = regs.Get("tools")!.Items.Single(r => r.GetString("id") == "t10");
        Assert.Equal("G-BRONZE", t10.GetString("metal"));
        Assert.NotNull(t10.Get("mods")!.GetFix("harvest_labor"));
        Assert.Equal(400, regs.Get("eras")!.Items.Single(r => r.GetInt("id") == 3).GetInt("base_cost"));
        // the Sim loads the same pack, including the new kinds
        var catalog = new CivSim.Sim.Research.ResearchCatalog(pack);
        Assert.Contains(catalog.Get("KEY-3").Effects, e => e.Kind == CivSim.Sim.Research.EffectKind.Mandate);
        Assert.Contains(catalog.Get("CHM-05").Effects, e => e.Kind == CivSim.Sim.Research.EffectKind.Sanitation);
    }

    [Fact]
    public void Goods_categories_ore_and_arms_exist_and_unknown_ones_do_not()
    {
        (ContentSet set, _) = Validated();
        Assert.Equal("ore", set.Reg("goods").Find("G-CUORE")!.GetString("category"));
        Assert.Equal("arms", set.Reg("goods").Find("G-ARMS1")!.GetString("category"));
        Report bad = ContentFixture.Mutate("registries/goods.yaml", "category: ore, first_from: [KEY-3]", "category: rock, first_from: [KEY-3]");
        Assert.True(ContentFixture.Has(bad, "V1", Severity.Error, "expected one of"), ContentFixture.Describe(bad));
    }

    [Theory]
    [InlineData("registries/tools.yaml", "service_ws: 12,", "service_ws: soon,", "V1", "expected a number")]
    [InlineData("registries/tools.yaml", "mods: {quarry: 1.1, mine: 1.1,", "mods: {quarry: fast, mine: 1.1,", "V1", "expected a number")]
    [InlineData("registries/tools.yaml", "crew: 2, horses: 2", "crew: 2, horses: 2, wheels: 2", "V1", "unknown field 'wheels'")]
    [InlineData("registries/roles.yaml", "staffing: \"1 per forge\"", "staffing: 3", "V1", "expected non-empty text")]
    [InlineData("registries/recipes.yaml", "inputs: {G-WOOD: 6}", "inputs: {G-WOODX: 6}", "V12", "G-WOODX")]
    [InlineData("registries/recipes.yaml", "unlocked_by: [FIR-08], inputs", "unlocked_by: [FIR-98], inputs", "V2", "FIR-98")]
    [InlineData("registries/recipes.yaml", "structure: [forge], inputs: {G-COPPER: 0.12}", "structure: [anvil], inputs: {G-COPPER: 0.12}", "V12", "anvil")]
    [InlineData("registries/recipes.yaml", "order: 10, practice: burn_charcoal", "order: 10, practice: burn_coal", "V14", "burn_coal")]
    [InlineData("registries/recipes.yaml", "labor: 0.4, order: 10", "labor: 0.4, order: ten", "V1", "expected an integer")]
    public void Registry_schema_and_reference_rules_catch_defects(string file, string oldText, string newText, string code, string message)
    {
        Report report = ContentFixture.Mutate(file, oldText, newText);
        Assert.True(ContentFixture.Has(report, code, Severity.Error, message), ContentFixture.Describe(report));
    }

    [Theory]
    [InlineData("catalog/keystones.yaml", "{kind: admin, span: 4,", "{kind: admin, span: 40,", "V10", "outside")]
    [InlineData("catalog/keystones.yaml", "{kind: admin, span: 4, note:", "{kind: admin, note:", "V10", "needs 'span' or 'overreach'")]
    [InlineData("catalog/keystones.yaml", "{kind: mandate, start: 1, cap: 2, recharge: 8,", "{kind: mandate, start: 1, cap: 2, recharge: 80,", "V10", "outside")]
    [InlineData("catalog/dip-contact-diplomacy.yaml", "{kind: mandate, decree: compact, cost: 0}", "{kind: mandate, decree: coup, cost: 0}", "V20", "not a known Decree")]
    [InlineData("catalog/dip-contact-diplomacy.yaml", "{kind: mandate, decree: compact, cost: 0}", "{kind: mandate, decree: compact}", "V10", "go together")]
    [InlineData("catalog/dip-contact-diplomacy.yaml", "{kind: mandate, decree: compact, cost: 0}", "{kind: mandate}", "V10", "at least one")]
    [InlineData("catalog/chm-chemistry-materials.yaml", "{kind: sanitation, value: 0.10}", "{kind: sanitation, value: 1.5}", "V10", "above the allowed maximum")]
    [InlineData("catalog/bel-belief-ritual.yaml", "{kind: cohesion, value: 2, scope: radius, radius: 10}", "{kind: cohesion, value: 2, scope: radius}", "V10", "needs a 'radius'")]
    [InlineData("catalog/bel-belief-ritual.yaml", "{kind: cohesion, value: 2, scope: radius, radius: 10}", "{kind: cohesion, value: 2, scope: nearby, radius: 10}", "V10", "expected one of")]
    [InlineData("catalog/bel-belief-ritual.yaml", "{kind: cohesion, value: 6, scope: here}", "{kind: cohesion, value: 6, scope: here, radius: 3}", "V10", "only applies with scope 'radius'")]
    [InlineData("catalog/def-defense-fortification.yaml", "refuge_radius: 6, site_within: 4,", "refuge_radius: 6, site_within: 4, shape: round,", "V10", "unknown field 'shape'")]
    [InlineData("catalog/def-defense-fortification.yaml", "structure: stone_walls, mult: 3.0,", "structure: stone_walls, mult: 30,", "V10", "above the allowed maximum")]
    [InlineData("catalog/def-defense-fortification.yaml", "structure: stone_walls, mult: 3.0,", "structure: moat, mult: 3.0,", "V12", "moat")]
    [InlineData("catalog/soc-kinship-society.yaml", "needs: [temple, palace]", "needs: [temple, throne]", "V12", "throne")]
    [InlineData("catalog/soc-kinship-society.yaml", "min_pop: 90,", "min_pop: many,", "V10", "expected an integer")]
    public void Effect_schema_and_validator_rules_catch_defects(string file, string oldText, string newText, string code, string message)
    {
        Report report = ContentFixture.Mutate(file, oldText, newText);
        Assert.True(ContentFixture.Has(report, code, Severity.Error, message), ContentFixture.Describe(report));
    }

    [Fact]
    public void A_text_only_defense_stays_valid_and_a_typed_one_needs_its_multiplier()
    {
        (ContentSet set, _) = Validated();
        Assert.Contains(set.ById["WAR-03"].Effects, e => e.Kind == "defense" && !e.Has("structure"));
        Report bad = ContentFixture.Mutate("catalog/def-defense-fortification.yaml", "structure: stone_walls, mult: 3.0,", "structure: stone_walls,");
        Assert.True(ContentFixture.Has(bad, "V10", Severity.Error, "'structure' and a 'mult' together"), ContentFixture.Describe(bad));
    }

    [Fact]
    public void A_Has_gate_must_map_to_a_deposit()
    {
        Report report = ContentFixture.Mutate("registries/deposits.yaml", ", has_gate: Tin}", "}");
        Assert.True(ContentFixture.Has(report, "V19", Severity.Error, "[Has: Tin]"), ContentFixture.Describe(report));
    }

    [Fact]
    public void Two_projects_may_share_a_rung_only_as_an_alternate_route()
    {
        // MET-04 and MET-05 both define chop 6: valid as shipped.
        (ContentSet set, Report report) = Validated();
        Assert.False(ContentFixture.Has(report, "V11"), ContentFixture.Describe(report));
        Assert.Equal(2, Validator.Ladders(set)["chop"].Count(r => r.Rung == 6));

        // different multipliers: not the same rung any more
        Report differs = ContentFixture.Mutate(Met, "name: Bronze axe, mult: 6.0, note: \"alternate route: MET-04 defines the same rung\"",
            "name: Bronze axe, mult: 5.5, note: \"alternate route: MET-04 defines the same rung\"");
        Assert.True(ContentFixture.Has(differs, "V11", Severity.Error, "rung 6 is defined by both"), ContentFixture.Describe(differs));

        // nobody lists them as one-of: not an alternate route
        var files = ContentFixture.Files();
        for (int i = 0; i < files.Count; i++)
            files[i] = (files[i].Path, files[i].Text.Replace("requires_any: [MET-04, MET-05]", "requires_any: [MET-04, MET-02]"));
        (ContentSet set2, Report report2) = ContentFixture.Load(files);
        Validator.Run(set2, report2);
        Assert.True(ContentFixture.Has(report2, "V11", Severity.Error, "rung 6 is defined by both"), ContentFixture.Describe(report2));
    }
}
