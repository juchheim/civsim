using System.IO;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class FiefGuildCraftTests
{
    private const ulong Seed = 20261115;

    [Fact]
    public void Guild_reduces_recipe_waste_and_accelerates_craft_role_training_only_in_Fief()
    {
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MEC-03");
        BronzeFixture guild = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MEC-03", "SOC-11");
        foreach (BronzeFixture fixture in new[] { plain, guild })
        {
            fixture.Economy.FixtureSetGoodStock(0, "G-WOOD", Fix64.FromInt(10));
            Assert.Equal(Fix64.One, fixture.Economy.FixtureRunRecipe(0, "make_wheel", Fix64.One, Fix64.FromInt(2)));
        }

        Assert.True(guild.Economy.FiefRecipeWaste(0) < plain.Economy.FiefRecipeWaste(0));
        Assert.Equal(Fix64.Parse("1.2"), guild.Economy.FiefCraftQuality(0) / plain.Economy.FiefCraftQuality(0));
        Assert.Equal(Fix64.Parse("1.5"), guild.Economy.FiefCraftRoleTraining(0) / plain.Economy.FiefCraftRoleTraining(0));

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
        static byte[] Snapshot(BronzeFixture fixture)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) fixture.Economy.WriteSnapshot(writer);
            return stream.ToArray();
        }
        Assert.Equal(Snapshot(control), Snapshot(traced));
        string row = Assert.Single(rows, line => line.Contains("wp7-soc11", System.StringComparison.Ordinal));
        Assert.Contains("recipe=make_wheel", row, System.StringComparison.Ordinal);
        Assert.Contains("soc11=true", row, System.StringComparison.Ordinal);
        Assert.Contains("qualityIndex=", row, System.StringComparison.Ordinal);
        Assert.Contains("trainingWork=", row, System.StringComparison.Ordinal);
    }
}
