using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>MIN-10 black-powder blasting (docs/MVP/38 §5.1): mine and quarry output x2 while a town holds powder, paid at 0.02 a litre. Discovery only.</summary>
public class DiscoveryBlastingTests
{
    private const ulong Seed = 20261019;
    private static readonly string[] Kit = { "KEY-5", "MIN-03", "MIN-10" };

    private static BronzeFixture Town(params string[] projects)
    {
        BronzeFixture f = BronzeFixture.CreateDiscoveryWithCoal(TestPack.Pack, Seed, 150, projects.Length == 0 ? Kit : projects);
        f.Economy.FixtureSetTier(0, 1);
        return f;
    }

    [Fact]
    public void A_town_with_powder_and_MIN10_blasts_at_the_rows_multiplier_and_otherwise_works_at_the_ordinary_rate()
    {
        BronzeFixture f = Town();
        Assert.Equal(Fix64.One, f.Economy.FixtureBlastingOutput(0));            // no powder
        Fix64 ordinary = f.Economy.FixtureQuarryRate(0);
        f.Economy.FixtureAddPowder(0, Fix64.FromInt(2));
        Fix64 blast = f.Economy.FixtureBlastingOutput(0);
        Assert.True(blast >= Fix64.FromRatio(3, 2) && blast <= Fix64.FromInt(2), "the typed x2, scaled by adoption");
        Assert.True(f.Economy.FixtureQuarryRate(0) == ordinary * blast, "the quarry works at the blasting rate");

        BronzeFixture noProject = Town("KEY-5", "MIN-03");
        noProject.Economy.FixtureAddPowder(0, Fix64.FromInt(2));
        Assert.Equal(Fix64.One, noProject.Economy.FixtureBlastingOutput(0));

        BronzeFixture fief = BronzeFixture.CreateFiefWithCoalDeposit(TestPack.Pack, Seed, 150, Kit);
        fief.Economy.FixtureSetTier(0, 1);
        fief.Economy.FixtureAddPowder(0, Fix64.FromInt(2));
        Assert.Equal(Fix64.One, fief.Economy.FixtureBlastingOutput(0));
    }

    [Fact]
    public void Blasting_pays_two_hundredths_of_a_powder_a_litre_and_never_more_than_the_store_holds()
    {
        BronzeFixture f = Town();
        f.Economy.FixtureAddPowder(0, Fix64.FromInt(2));
        f.Economy.FixturePayBlasting(0, Fix64.FromInt(50));
        Assert.True(f.Economy.FixturePowder(0) == Fix64.FromInt(2) - Fix64.FromInt(50) * Fix64.FromRatio(2, 100), "50 litres cost one powder");
        f.Economy.FixturePayBlasting(0, Fix64.FromInt(1000));
        Assert.Equal(Fix64.Zero, f.Economy.FixturePowder(0));
        Assert.Equal(Fix64.One, f.Economy.FixtureBlastingOutput(0));            // the store is empty: back to the ordinary rate
    }
}
