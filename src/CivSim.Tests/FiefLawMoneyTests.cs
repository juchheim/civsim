using CivSim.Sim.Economy;

namespace CivSim.Tests;

/// <summary>First measurable WP-E5-7 rule: chartered representation adds one admin slot.</summary>
public sealed class FiefLawMoneyTests
{
    private const ulong Seed = 20261115;

    [Fact]
    public void GOV11_charter_adds_one_admin_span_in_Fief()
    {
        var pack = TestPack.WithNumeric("GOV-11");
        BronzeFixture before = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5");
        BronzeFixture charter = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "KEY-5", "GOV-11");

        Assert.Equal(before.Economy.AdminSpan(0) + 1, charter.Economy.AdminSpan(0));

        BronzeFixture iron = BronzeFixture.CreateIron(pack, Seed, 100, "KEY-5");
        Assert.Equal(before.Economy.AdminSpan(0), iron.Economy.AdminSpan(0));
    }
}
