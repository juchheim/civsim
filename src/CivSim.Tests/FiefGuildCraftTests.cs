using System.IO;
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
}
