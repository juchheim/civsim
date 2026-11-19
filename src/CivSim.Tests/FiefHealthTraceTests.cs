using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class FiefHealthTraceTests
{
    [Fact]
    public void Draft_epidemic_hazard_scale_is_fief_only()
    {
        BronzeFixture fief = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261160, 200, "KEY-5");
        BronzeFixture bronze = BronzeFixture.Create(TestPack.Pack, 20261160, 200, true, true, true, "KEY-5");
        fief.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);
        bronze.Economy.FixtureSetOutbreak(0, OutbreakClass.Plague);

        Fix64 fiefHazard = fief.Economy.FixtureInfectedHazard(0);
        Fix64 bronzeHazard = bronze.Economy.FixtureInfectedHazard(0);

        Assert.True(bronzeHazard > Fix64.Zero);
        Assert.Equal(bronzeHazard * Fix64.Parse("0.282944"), fiefHazard);
    }

    [Fact]
    public void Trace_disabled_and_enabled_leave_simulation_checksum_unchanged()
    {
        BronzeFixture plain = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261116, 200, "KEY-5", "MED-07");
        BronzeFixture traced = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261116, 200, "KEY-5", "MED-07");
        traced.Economy.EnableFiefHealthTraceForDiagnostics();

        plain.RunSeasons(4);
        traced.RunSeasons(4);

        Assert.Equal(plain.Economy.ComputeChecksum(), traced.Economy.ComputeChecksum());
        Assert.Equal(default, plain.Economy.CaptureFiefExpectedHazardsForDiagnostics(0));
        Assert.True(traced.Economy.CaptureFiefExpectedHazardsForDiagnostics(0).AdultEquivalentPersonSeasons > Fix64.Zero);
    }

    [Fact]
    public void Trace_aggregates_winter_shortfall_into_starvation_and_counts_death_causes()
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261117, 200, "KEY-5", "MED-07");
        fixture.Economy.EnableFiefHealthTraceForDiagnostics();
        fixture.RunSeasons(3);
        fixture.Economy.ResetFiefExpectedHazardsForDiagnostics();
        fixture.RunSeasons(1); // Season four is winter.

        Fix64 traceStarvation = Fix64.Zero;
        int traceTotalDeaths = 0, traceEpidemicDeaths = 0;
        for (int civ = 0; civ < fixture.Economy.CivilizationCount; civ++)
        {
            FiefExpectedHazardSnapshot trace = fixture.Economy.CaptureFiefExpectedHazardsForDiagnostics(civ);
            traceStarvation += trace.Starvation;
            traceTotalDeaths += trace.TotalDeaths;
            traceEpidemicDeaths += trace.EpidemicDeaths;
        }
        Fix64 starvationExpected = Fix64.Zero;
        for (int band = 0; band < 3; band++)
            starvationExpected += fixture.Economy.LastExpectedHazardsByCauseAndAgeBand(band)[(int)EconomyDeathCause.Starvation];
        int totalDeaths = 0;
        foreach (int count in fixture.Economy.LastDeathsByCause) totalDeaths += count;

        Assert.Equal(starvationExpected, traceStarvation);
        Assert.Equal(Fix64.FromRatio(3, 10), LivingEconomyModel.CombinedStarvationHazardForDiagnostics(
            Fix64.FromRatio(1, 10), Fix64.FromRatio(2, 10)));
        Assert.Equal(totalDeaths, traceTotalDeaths);
        Assert.Equal(fixture.Economy.LastDeathsByCause[(int)EconomyDeathCause.Epidemic], traceEpidemicDeaths);
    }

    [Theory]
    [InlineData(OutbreakClass.Pox)]
    [InlineData(OutbreakClass.Plague)]
    public void Trace_attributes_epidemic_deaths_to_the_active_outbreak_class(OutbreakClass outbreak)
    {
        BronzeFixture fixture = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261152, 1200, "KEY-5");
        fixture.Economy.EnableFiefHealthTraceForDiagnostics();
        fixture.Economy.FixtureSetOutbreak(0, outbreak);
        fixture.RunSeasons(1);

        FiefExpectedHazardSnapshot trace = fixture.Economy.CaptureFiefExpectedHazardsForDiagnostics(0);
        int epidemicDeaths = fixture.Economy.LastDeathsByCause[(int)EconomyDeathCause.Epidemic];
        Assert.True(epidemicDeaths > 0);
        Assert.Equal(epidemicDeaths, trace.EpidemicDeaths);
        Assert.Equal(epidemicDeaths, trace.PoxDeaths + trace.PlagueDeaths);
        Assert.Equal(outbreak == OutbreakClass.Pox ? epidemicDeaths : 0, trace.PoxDeaths);
        Assert.Equal(outbreak == OutbreakClass.Plague ? epidemicDeaths : 0, trace.PlagueDeaths);
    }

    [Fact]
    public void Trace_records_quarantine_import_attempts_blocks_and_successes_from_real_gate_results()
    {
        BronzeFixture quarantined = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261140, 100,
            "KEY-5", "MED-08");
        quarantined.Economy.EnableFiefHealthTraceForDiagnostics();
        int successfulImports = 0;
        for (int eventId = 0; eventId < 256; eventId++)
            if (quarantined.Economy.ImportOutbreak(0, OutbreakClass.Plague, eventId)) successfulImports++;

        FiefExpectedHazardSnapshot trace = quarantined.Economy.CaptureFiefExpectedHazardsForDiagnostics(0);
        Assert.True(trace.Quarantine);
        Assert.Equal(256, trace.ImportedAttempts);
        Assert.Equal(256 - successfulImports, trace.ImportedBlocked);
        Assert.Equal(successfulImports, trace.ImportedSuccesses);
        Assert.Equal(trace.ImportedAttempts, trace.ImportedBlocked + trace.ImportedSuccesses);

        quarantined.Economy.ResetFiefExpectedHazardsForDiagnostics();
        trace = quarantined.Economy.CaptureFiefExpectedHazardsForDiagnostics(0);
        Assert.True(trace.Quarantine);
        Assert.Equal(0, trace.ImportedAttempts);
        Assert.Equal(0, trace.ImportedBlocked);
        Assert.Equal(0, trace.ImportedSuccesses);

        BronzeFixture open = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, 20261140, 100, "KEY-5");
        open.Economy.EnableFiefHealthTraceForDiagnostics();
        Assert.True(open.Economy.ImportOutbreak(0, OutbreakClass.Plague, importEvent: 0));
        FiefExpectedHazardSnapshot openTrace = open.Economy.CaptureFiefExpectedHazardsForDiagnostics(0);
        Assert.False(openTrace.Quarantine);
        Assert.Equal(1, openTrace.ImportedAttempts);
        Assert.Equal(0, openTrace.ImportedBlocked);
        Assert.Equal(1, openTrace.ImportedSuccesses);
    }
}
