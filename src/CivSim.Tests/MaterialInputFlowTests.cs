using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Tests;

public sealed class MaterialInputFlowTests
{
    [Fact]
    public void Forest_biome_stock_doubles_without_changing_forest_class_and_forest_regrowth_rises_50_percent()
    {
        GeneratedWorld world = WorldGenerator.Generate(20261120);
        WorldEconomyInput input = WorldEconomyInput.FromGeneratedWorld(world);
        int[] forestBiomes = [(int)Biome.TemperateForest, (int)Biome.OpenWoodland, (int)Biome.TropicalForest, (int)Biome.Taiga];
        int checkedForestTiles = 0;
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            WorldEconomyTile econTile = input.Tile(tile);
            if (forestBiomes.Contains(econTile.BiomeId))
            {
                checkedForestTiles++;
                Assert.True(econTile.InitialWood >= Fix64.FromInt(15), $"existing forest biome {econTile.BiomeId} remains classified as forest");
                Assert.Equal(Fix64.FromInt(9), econTile.WoodGrowthPerYear);
            }
            else
            {
                Assert.True(econTile.InitialWood < Fix64.FromInt(15), $"nonforest biome {econTile.BiomeId} stays below the forest threshold");
                Assert.Equal(Fix64.FromInt(6), econTile.WoodGrowthPerYear);
            }

            if (econTile.BiomeId == (int)Biome.TemperateForest) Assert.Equal(Fix64.FromInt(60), econTile.InitialWood);
            if (econTile.BiomeId == (int)Biome.OpenWoodland) Assert.Equal(Fix64.FromInt(30), econTile.InitialWood);
            if (econTile.BiomeId == (int)Biome.TropicalForest) Assert.Equal(Fix64.FromInt(80), econTile.InitialWood);
            if (econTile.BiomeId == (int)Biome.Taiga) Assert.Equal(Fix64.FromInt(50), econTile.InitialWood);
        }
        Assert.True(checkedForestTiles > 0);
    }

    [Fact]
    public void Finite_stone_clay_and_flint_deposit_stock_is_tripled_without_adding_nodes()
    {
        GeneratedWorld world = WorldGenerator.Generate(20261121);
        WorldEconomyInput input = WorldEconomyInput.FromGeneratedWorld(world);
        int stoneNodes = 0, clayNodes = 0, flintNodes = 0;
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            WorldTile source = world.TileAt(tile);
            WorldEconomyTile econ = input.Tile(tile);
            switch (source.Deposit)
            {
                case DepositKind.Stone:
                    stoneNodes++;
                    Assert.Equal(Fix64.FromInt(3 * (2 + source.DepositAmount)), econ.InitialStoneStock);
                    break;
                case DepositKind.Clay:
                    clayNodes++;
                    Assert.Equal(Fix64.FromInt(3 * source.DepositAmount), econ.InitialClayStock);
                    break;
                case DepositKind.Flint:
                    flintNodes++;
                    Assert.Equal(Fix64.FromInt(3 * source.DepositAmount), econ.InitialFlintStock);
                    break;
            }
        }
        Assert.True(stoneNodes > 0 && clayNodes > 0 && flintNodes > 0);
    }

    [Fact]
    public void Recipes_and_live_gather_rates_match_the_input_flow_fit()
    {
        EconomyContent content = EconomyContent.FromPack(TestPack.Pack);
        EconomyRecipe Recipe(string id) => content.Recipes.ToArray().Single(recipe => recipe.Id == id);
        EconomyAction Action(string id) => content.Actions.ToArray().Single(action => action.Id == id);
        void Rate(string actionId, string expected) => Assert.Equal(Fix64.Parse(expected), Action(actionId).BaseRate);

        EconomyRecipe charcoal = Recipe("burn_charcoal");
        Assert.Equal(Fix64.FromInt(4), charcoal.Inputs[0].Quantity);
        Assert.Equal(Fix64.Parse("0.3"), charcoal.Labor);
        foreach (string id in new[] { "smelt_copper", "cosmelt_arsenical", "smelt_tin", "bloomery" })
            Assert.Equal(Fix64.One, Recipe(id).Outputs[0].Quantity);
        foreach (string id in new[] { "forge_wrought", "carburize_steel", "cast_iron_goods" })
            Assert.Equal(Fix64.Parse("0.32"), Recipe(id).Labor);
        Assert.Equal(Fix64.Parse("0.48"), Recipe("crucible_steel").Labor);
        Rate("mine", "2");
        Rate("quarry", "3");
        Rate("chop", "3");
        Rate("gather_clay", "4.5");
        Rate("collect_flint_ochre", "1.5");
        Rate("haul", "60");
    }
}
