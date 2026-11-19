using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using System.Linq;

namespace CivSim.Tests;

public sealed class FiefWildernessFarmingTests
{
    private const ulong Seed = 20261114;

    [Fact]
    public void Staffed_monastery_assigns_distinct_fief_wilderness_plots_and_iron_stays_inert()
    {
        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 100, "KEY-5", "BEL-10");
        fief.Economy.FixtureSetTier(0, 1);
        fief.Economy.FixtureBuildIronStructure(0, "monastery");
        fief.RunSeasons(1);
        Assert.Equal(4, fief.Economy.MonkHeads(0));
        int[] plots = fief.Economy.FixtureFiefWildernessPlots(0);
        Assert.InRange(plots.Length, 1, 4); // four monks, one annual plot assignment per monk; BEL-10 caps at six per monastery
        Assert.Equal(plots.Length, plots.Distinct().Count());
        foreach (int tile in plots)
        {
            Assert.False(fief.Economy.FixtureIsInsideAnySettlementFootprint(tile));
            Assert.False(fief.Economy.FixtureIsAnySettledField(tile));
            Assert.False(fief.Economy.FixtureIsSettlementSite(tile));
        }
        Fix64 expected = Fix64.Zero;
        foreach (int tile in plots) expected += fief.Economy.FixtureWildernessPlotYield(0, tile);
        Assert.True(expected > Fix64.Zero);
        Assert.Equal(expected, fief.Economy.FixtureFiefWildernessFarmYield(0));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, Seed, 100, "KEY-5", "BEL-10");
        iron.Economy.FixtureSetTier(0, 1);
        iron.Economy.FixtureBuildIronStructure(0, "monastery");
        iron.RunSeasons(1);
        Assert.Equal(Fix64.Zero, iron.Economy.FixtureFiefWildernessFarmYield(0));
    }
}
