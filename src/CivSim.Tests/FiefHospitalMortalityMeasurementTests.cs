using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>Controlled WP6 measurement: hospital effect on deaths in the first season of a forced Plague outbreak.</summary>
public sealed class FiefHospitalMortalityMeasurementTests(ITestOutputHelper output)
{
    [Fact]
    public void Pre_mortality_observer_does_not_change_economy_state_or_snapshot()
    {
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261114, 200, "KEY-5", "MED-07");
        BronzeFixture observed = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261114, 200, "KEY-5", "MED-07");
        foreach (BronzeFixture fixture in new[] { plain, observed })
        {
            fixture.Economy.FixtureSetTier(0, 1);
            fixture.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        }
        int observations = 0;
        observed.Economy.FixtureAfterOutbreaks = () =>
        {
            _ = observed.Economy.FixtureInfectedResidentCount();
            observations++;
        };

        plain.RunSeasons(1);
        observed.RunSeasons(1);

        Assert.Equal(1, observations);
        Assert.Equal(plain.Economy.ComputeChecksum(), observed.Economy.ComputeChecksum());
        Assert.Equal(Snapshot(plain.Economy), Snapshot(observed.Economy));
    }

    [Fact]
    public void Report_matched_seed_hospital_mortality_cohort()
    {
        const int seeds = 16;
        long totalExposures = 0, controlDeaths = 0, treatedDeaths = 0;
        var pairedDifferences = new double[seeds];
        for (ulong i = 0; i < seeds; i++)
        {
            ulong seed = 20261114 + i;
            (int exposures, int deaths) control = Run(seed, hospital: false);
            (int exposures, int deaths) treated = Run(seed, hospital: true);
            Assert.Equal(control.exposures, treated.exposures);
            totalExposures += control.exposures;
            controlDeaths += control.deaths;
            treatedDeaths += treated.deaths;
            pairedDifferences[i] = control.deaths - treated.deaths;
            output.WriteLine($"seed={seed} exposures={control.exposures} epidemicDeathsUnstaffed={control.deaths} epidemicDeathsStaffed={treated.deaths} pairedReduction={pairedDifferences[i]:+0;-0;0}");
        }

        double mean = Mean(pairedDifferences);
        double variance = 0;
        foreach (double difference in pairedDifferences) variance += (difference - mean) * (difference - mean);
        double standardError = Math.Sqrt(variance / (seeds - 1) / seeds);
        const double tCriticalDf15 = 2.131;
        output.WriteLine($"Matched seeds={seeds}; exact infected-season exposures={totalExposures}; epidemic deaths: unstaffed={controlDeaths}, staffed={treatedDeaths}.");
        output.WriteLine($"Paired mean death reduction (unstaffed - staffed)={mean:F2} per seed; 95% paired t interval [{mean - tCriticalDf15 * standardError:F2}, {mean + tCriticalDf15 * standardError:F2}] over {seeds} seed pairs.");
        output.WriteLine($"Pooled deaths per exposure: unstaffed={(double)controlDeaths / totalExposures:P3}; staffed={(double)treatedDeaths / totalExposures:P3}; relative reduction={1d - (double)treatedDeaths / controlDeaths:P2}.");
    }

    private static (int Exposures, int Deaths) Run(ulong seed, bool hospital)
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 200, "KEY-5", "MED-07");
        fixture.Economy.FixtureSetTier(0, 1);
        // Both arms pay for and operate the same structure; the control clears only its posts after staffing.
        fixture.Economy.FixtureBuildIronStructure(0, "hospital");
        fixture.RunSeasons(1); // Establish the same Fief state and staff the hospital before the outbreak is forced.
        bool staffed = fixture.Economy.FixtureHospitalStaffed(0);
        if (hospital) Assert.True(staffed);
        else
        {
            fixture.Economy.FixtureSetIronWork(0, "hospital", 0);
            fixture.Economy.FixtureClearRoles(0);
        }
        fixture.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        int exposures = -1;
        fixture.Economy.FixtureAfterOutbreaks = () => exposures = fixture.Economy.FixtureInfectedResidentCount();
        fixture.RunSeasons(1); // First outbreak season: same 60% infection sampling in both arms.
        Assert.True(exposures >= 0, "The actual seasonal outbreak path must invoke the pre-mortality observer.");

        long deaths = Enumerable.Range(0, fixture.Economy.LastDeathEventCount)
            .LongCount(i => fixture.Economy.LastDeathEvent(i).Cause == EconomyDeathCause.Epidemic);
        return (exposures, checked((int)deaths));
    }

    private static double Mean(double[] values)
    {
        double sum = 0;
        foreach (double value in values) sum += value;
        return sum / values.Length;
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }
}
