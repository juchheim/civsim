using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

/// <summary>TRD-09's administrative-waste draft modifier applies to explicit staffed administration work only in Fief.</summary>
public sealed class FiefDoubleEntryTests
{
    private const ulong Seed = 20261114;

    [Fact]
    public void TRD09_reduces_only_the_completed_civilizations_admin_work_waste()
    {
        var pack = TestPack.WithNumeric("TRD-09");
        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(pack, Seed, 100, "TRD-09");
        Fix64 administrativeWork = Fix64.FromInt(10);

        Fix64 savings = fief.Economy.FixtureAdminWasteSavings(0, administrativeWork);
        Assert.InRange((savings - Fix64.One).Raw, -32L, 32L);
        Assert.Equal(Fix64.Zero, fief.Economy.FixtureAdminWasteSavings(1, administrativeWork));

        BronzeFixture iron = BronzeFixture.CreateIronWithCamp(pack, Seed, 100, "TRD-09");
        Assert.Equal(Fix64.Zero, iron.Economy.FixtureAdminWasteSavings(0, administrativeWork));
        BronzeFixture bronze = BronzeFixture.Create(pack, Seed, 100, true, true, true, true, false, "TRD-09");
        Assert.Equal(Fix64.Zero, bronze.Economy.FixtureAdminWasteSavings(0, administrativeWork));
    }
}
