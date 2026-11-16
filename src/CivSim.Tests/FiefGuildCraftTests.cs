using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class FiefGuildCraftTests
{
    private const ulong Seed = 20261115;

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    [Fact]
    public void SOC11_reduces_recipe_input_loss_and_records_Fief_craft_diagnostics_only_in_Fief()
    {
        var pack = TestPack.WithNumeric("SOC-11");
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "MEC-03");
        BronzeFixture guild = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "MEC-03", "SOC-11");
        foreach (BronzeFixture fixture in new[] { plain, guild })
        {
            fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
            Assert.Equal(Fix64.One, fixture.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.One, Fix64.FromInt(2)));
        }

        Assert.True(guild.Economy.FiefRecipeWaste(0) < plain.Economy.FiefRecipeWaste(0));
        Assert.Equal(Fix64.Parse("1.2"), guild.Economy.FiefCraftQuality(0) / plain.Economy.FiefCraftQuality(0));
        Assert.True(guild.Economy.FiefCraftRoleTraining(0) > Fix64.Zero);

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "MEC-03", "SOC-11");
        Assert.Equal(Fix64.Zero, iron.Economy.FiefRecipeWaste(0));
        Assert.Equal(Fix64.Zero, iron.Economy.FiefCraftQuality(0));
        Assert.Equal(Fix64.Zero, iron.Economy.FiefCraftRoleTraining(0));

        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, Seed, 100, true, true, true, "SOC-11", "MEC-03");
        Assert.Equal(Fix64.Zero, bronze.Economy.FiefRecipeWaste(0));
        Assert.Equal(Fix64.Zero, bronze.Economy.FiefCraftQuality(0));
        Assert.Equal(Fix64.Zero, bronze.Economy.FiefCraftRoleTraining(0));
    }

    [Fact]
    public void SOC11_reduces_craft_role_recipe_labor_by_the_catalog_speed_for_equal_work()
    {
        var pack = TestPack.WithNumeric("SOC-11");
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(pack, Seed + 2, 100, "MEC-03");
        BronzeFixture guild = BronzeFixture.CreateFiefWithCamp(pack, Seed + 2, 100, "MEC-03", "SOC-11");

        Fix64 plainLabor = plain.Economy.FixtureBatch(0, "make_wheel").Labor;
        Fix64 guildLabor = guild.Economy.FixtureBatch(0, "make_wheel").Labor;
        var catalog = new ResearchCatalog(pack);
        Fix64 catalogSpeed = Fix64.One;
        foreach (var effect in catalog.Projects[catalog.IndexOf("SOC-11")].Effects)
            if (effect.Stat == "apprenticeship_speed") catalogSpeed = effect.Value;

        Assert.True(catalogSpeed > Fix64.One);
        Assert.Equal(plainLabor / catalogSpeed, guildLabor);
        Assert.Equal(plain.Economy.FixtureBatch(0, "bloomery").Labor, guild.Economy.FixtureBatch(0, "bloomery").Labor);

        foreach (BronzeFixture fixture in new[] { plain, guild })
            fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
        Fix64 plainOutput = plain.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.FromInt(10), Fix64.FromInt(2));
        Fix64 guildOutput = guild.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.FromInt(10), Fix64.FromInt(2));
        Assert.True(guildOutput > plainOutput, $"equal assigned work should make more craft output: {plainOutput} vs {guildOutput}");

        BronzeFixture ironPlain = BronzeFixture.CreateIron(pack, Seed + 3, 100, "MEC-03");
        BronzeFixture ironGuild = BronzeFixture.CreateIron(pack, Seed + 3, 100, "MEC-03", "SOC-11");
        Assert.Equal(ironPlain.Economy.FixtureBatch(0, "make_wheel").Labor, ironGuild.Economy.FixtureBatch(0, "make_wheel").Labor);
    }

    [Fact]
    public void SOC11_extends_registry_life_of_non_kit_craft_goods_and_keeps_T12_service_life_outside_scope()
    {
        var pack = TestPack.WithNumeric("SOC-11");
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(pack, Seed + 4, 100, "MEC-03");
        BronzeFixture guild = BronzeFixture.CreateFiefWithCamp(pack, Seed + 4, 100, "MEC-03", "SOC-11");
        foreach (BronzeFixture fixture in new[] { plain, guild })
            fixture.Economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(48));

        plain.RunSeasons(1);
        guild.RunSeasons(1);

        Fix64 ordinary = plain.Economy.FixtureGoodStock(0, "G-ARMS3");
        Fix64 extended = guild.Economy.FixtureGoodStock(0, "G-ARMS3");
        Assert.True(extended > ordinary, $"SOC-11 should reduce registry age-wear: {ordinary} vs {extended}");
        Assert.Equal(Fix64.One, guild.Economy.FixtureCraftServiceLifeMultiplier(0, "G-TOOL12", 60));
        Assert.Equal(Fix64.Parse("1.2"), guild.Economy.FixtureCraftServiceLifeMultiplier(0, "G-ARMS3", 24));

        BronzeFixture deterministic = BronzeFixture.CreateFiefWithCamp(pack, Seed + 4, 100, "MEC-03", "SOC-11");
        deterministic.Economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(48));
        deterministic.RunSeasons(1);
        Assert.Equal(guild.Economy.ComputeChecksum(), deterministic.Economy.ComputeChecksum());
        Assert.Equal(Snapshot(guild.Economy), Snapshot(deterministic.Economy));
    }

    [Fact]
    public void SOC11_does_not_change_T12_kit_service_life_or_per_work_durability()
    {
        var pack = TestPack.WithNumeric("SOC-11");
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(pack, Seed + 6, 100, "MEC-03");
        BronzeFixture guild = BronzeFixture.CreateFiefWithCamp(pack, Seed + 6, 100, "MEC-03", "SOC-11");
        int adult = -1;
        foreach (BronzeFixture fixture in new[] { plain, guild })
        {
            int candidate = Enumerable.Range(0, fixture.Economy.FigureCount)
                .FirstOrDefault(i => fixture.Economy.Figure(i).Age >= 14, -1);
            Assert.True(candidate >= 0, "the paired Fief fixture needs an adult carrying a T12 kit");
            if (adult < 0) adult = candidate;
            else Assert.Equal(adult, candidate);
            fixture.Economy.FixtureSetKit(candidate, 12, Fix64.FromInt(40));
        }

        Assert.Equal(Fix64.FromInt(60), plain.Economy.FixtureKitLife(12).Service);
        Assert.Equal(plain.Economy.FixtureKitLife(12), guild.Economy.FixtureKitLife(12));
        Assert.Equal(Fix64.One, plain.Economy.FixtureCraftServiceLifeMultiplier(0, "G-TOOL12", 60));
        Assert.Equal(Fix64.One, guild.Economy.FixtureCraftServiceLifeMultiplier(0, "G-TOOL12", 60));

        plain.Economy.FixtureWearKit(adult, Fix64.FromInt(10));
        guild.Economy.FixtureWearKit(adult, Fix64.FromInt(10));
        Assert.Equal(Fix64.FromInt(30), plain.Economy.FixtureKitLeft(adult));
        Assert.Equal(plain.Economy.FixtureKitLeft(adult), guild.Economy.FixtureKitLeft(adult));
    }

    [Fact]
    public void Fief_craft_training_and_waste_continue_through_snapshot()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MEC-03");
        fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
        fixture.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.One, Fix64.FromInt(2));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) fixture.Economy.WriteSnapshot(writer);
        stream.Position = 0;
        BronzeFixture restoredFixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MEC-03");
        LivingEconomyModel restored = restoredFixture.Economy;
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true)) restored.ReadSnapshot(reader);
        Assert.Equal(fixture.Economy.FiefRecipeWaste(0), restored.FiefRecipeWaste(0));
        Assert.Equal(fixture.Economy.FiefCraftQuality(0), restored.FiefCraftQuality(0));
        Assert.Equal(fixture.Economy.FiefCraftRoleTraining(0), restored.FiefCraftRoleTraining(0));
    }

    [Fact]
    public void SOC11_trace_is_checksum_neutral_and_records_recipe_denominator()
    {
        BronzeFixture control = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 1, 100, "MEC-03", "SOC-11");
        BronzeFixture traced = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed + 1, 100, "MEC-03", "SOC-11");
        foreach (BronzeFixture fixture in new[] { control, traced }) fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
        var rows = new System.Collections.Generic.List<string>();
        traced.Economy.FiefWp7TraceObserverForDiagnostics = rows.Add;
        Assert.Equal(Fix64.One, control.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.One, Fix64.FromInt(2)));
        Assert.Equal(Fix64.One, traced.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.One, Fix64.FromInt(2)));
        Assert.Equal(Snapshot(control.Economy), Snapshot(traced.Economy));
        string row = Assert.Single(rows, line => line.Contains("wp7-soc11", System.StringComparison.Ordinal));
        Assert.Contains("recipe=make_wheel", row, System.StringComparison.Ordinal);
        Assert.Contains("soc11=true", row, System.StringComparison.Ordinal);
        Assert.Contains("qualityIndex=", row, System.StringComparison.Ordinal);
        Assert.Contains("trainingWork=", row, System.StringComparison.Ordinal);
    }

    [Fact]
    public void SOC11_trace_observer_is_exact_state_neutral_for_tool_durability_and_T12_life()
    {
        var pack = TestPack.WithNumeric("SOC-11");
        BronzeFixture control = BronzeFixture.CreateFiefWithCamp(pack, Seed + 5, 100, "MEC-03", "SOC-11");
        BronzeFixture traced = BronzeFixture.CreateFiefWithCamp(pack, Seed + 5, 100, "MEC-03", "SOC-11");
        int t12Figure = -1;
        foreach (BronzeFixture fixture in new[] { control, traced })
        {
            fixture.Economy.FixtureSetGoodStock(0, "G-ARMS3", Fix64.FromInt(48));
            fixture.Economy.FixtureStock(0, "G-TOOL12", Fix64.FromInt(2));
            for (int adult = 0; adult < fixture.Economy.FigureCount; adult++)
            {
                if (fixture.Economy.Figure(adult).Age < 14) continue;
                fixture.Economy.FixtureSetKit(adult, 12, Fix64.FromInt(40));
                if (t12Figure < 0) t12Figure = adult;
                else Assert.Equal(t12Figure, adult);
                break;
            }
        }
        Assert.True(t12Figure >= 0, "the paired Fief fixture needs an adult carrying a T12 kit");

        // Exercise actual kit wear before the traced season. The dedicated T12 kit model has a
        // 60 work-season base life; SOC-11's registry crafted-good service-life modifier must
        // remain exactly one for kits and must not change this one unit of wear.
        Assert.Equal(Fix64.FromInt(60), control.Economy.FixtureKitLife(12).Service);
        Assert.Equal(control.Economy.FixtureKitLife(12), traced.Economy.FixtureKitLife(12));
        Assert.Equal(Fix64.One, control.Economy.FixtureCraftServiceLifeMultiplier(0, "G-TOOL12", 60));
        Assert.Equal(Fix64.One, traced.Economy.FixtureCraftServiceLifeMultiplier(0, "G-TOOL12", 60));
        control.Economy.FixtureWearKit(t12Figure, Fix64.One);
        traced.Economy.FixtureWearKit(t12Figure, Fix64.One);
        Assert.Equal(Fix64.FromInt(39), control.Economy.FixtureKitLeft(t12Figure));
        Assert.Equal(control.Economy.FixtureKitLeft(t12Figure), traced.Economy.FixtureKitLeft(t12Figure));

        var rows = new System.Collections.Generic.List<string>();
        traced.Economy.FiefWp7TraceObserverForDiagnostics = rows.Add;
        control.RunSeasons(1);
        traced.RunSeasons(1);

        // The serialized state covers each figure's kit tier/remaining service, plus registry goods.
        // Therefore this is an exact paired observer-on/off assertion for durability and T12 life.
        Assert.Equal(Snapshot(control.Economy), Snapshot(traced.Economy));
        Assert.Equal(control.Economy.ComputeChecksum(), traced.Economy.ComputeChecksum());
        Assert.NotEmpty(rows);
        Assert.True(control.Economy.FixtureGoodStock(0, "G-ARMS3") < Fix64.FromInt(48), "the paired season should exercise registry life aging");
        Assert.Equal(control.Economy.FixtureGoodStock(0, "G-TOOL12"), traced.Economy.FixtureGoodStock(0, "G-TOOL12"));
        for (int i = 0; i < control.Economy.FigureCount; i++)
        {
            Assert.Equal(control.Economy.FigureKit(i).Tier, traced.Economy.FigureKit(i).Tier);
            Assert.Equal(control.Economy.FixtureKitLeft(i), traced.Economy.FixtureKitLeft(i));
        }
    }
}
