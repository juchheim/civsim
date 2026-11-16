using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefMineSurveyingTests
{
    private const ulong Seed = 20261116;

    [Fact]
    public void MIN_08_increases_known_own_territory_deposit_yield_by_25_percent()
    {
        BronzeFixture baseline = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MET-08");
        BronzeFixture surveyed = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MET-08", "MIN-08");
        baseline.Economy.TileOwnership = OwnOnly(baseline.CopperTile);
        surveyed.Economy.TileOwnership = OwnOnly(surveyed.CopperTile);

        Fix64 baseRate = baseline.Economy.FixtureMineRate(0, baseline.CopperTile);
        Fix64 surveyedRate = surveyed.Economy.FixtureMineRate(0, surveyed.CopperTile);

        Assert.True(baseRate > Fix64.Zero);
        Assert.Equal(baseRate * Fix64.FromRatio(5, 4), surveyedRate);
    }

    [Fact]
    public void MIN_08_does_not_boost_unknown_or_out_of_territory_deposits()
    {
        BronzeFixture baseline = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MET-08");
        BronzeFixture surveyed = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "MET-08", "MIN-08");
        baseline.Economy.TileOwnership = NoOwnedTiles();
        surveyed.Economy.TileOwnership = NoOwnedTiles();

        Fix64 baselineOutsideRate = baseline.Economy.FixtureMineRate(0, baseline.CopperTile);
        Fix64 surveyedOutsideRate = surveyed.Economy.FixtureMineRate(0, surveyed.CopperTile);
        Assert.True(baselineOutsideRate > Fix64.Zero);
        Assert.Equal(baselineOutsideRate, surveyedOutsideRate);

        int unknownTile = surveyed.Economy.FixtureFirstUnknownDepositTile();
        Assert.True(unknownTile >= 0);
        surveyed.Economy.TileOwnership = OwnOnly(unknownTile);
        Assert.Equal(Fix64.Zero, surveyed.Economy.FixtureMineRate(0, unknownTile));
    }

    [Fact]
    public void MIN_08_has_no_effect_in_Iron()
    {
        BronzeFixture baseline = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, "MET-08");
        BronzeFixture withMin08 = BronzeFixture.CreateIronWithCamp(TestPack.Pack, Seed, 100, "MET-08", "MIN-08");
        baseline.Economy.TileOwnership = OwnOnly(baseline.IronTile);
        withMin08.Economy.TileOwnership = OwnOnly(withMin08.IronTile);

        Fix64 baselineRate = baseline.Economy.FixtureMineRate(0, baseline.IronTile);
        Fix64 withMin08Rate = withMin08.Economy.FixtureMineRate(0, withMin08.IronTile);

        Assert.True(baselineRate > Fix64.Zero);
        Assert.Equal(baselineRate, withMin08Rate);
    }

    private static ITileOwnership OwnOnly(int tile) => new FixedOwnership(tile);
    private static ITileOwnership NoOwnedTiles() => new FixedOwnership(-1);

    private sealed class FixedOwnership(int ownedTile) : ITileOwnership
    {
        public int OwnerOf(int tile) => tile == ownedTile ? 0 : -1;
    }
}
