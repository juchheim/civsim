using System.Collections.Generic;
using CivSim.Harness;

namespace CivSim.Tests;

/// <summary>The I-T6 fuel check is read-only tooling: its report is a pure function of its arguments.</summary>
public class IronFuelCheckTests
{
    [Fact]
    public void The_fuel_report_is_identical_across_runs_and_names_every_scenario()
    {
        var seeds = new List<ulong> { 20261019 };
        string first = IronFuelCheck.Report(TestPack.Pack, seeds, seasons: 8, people: 60, scenarioLimit: 2);
        string second = IronFuelCheck.Report(TestPack.Pack, seeds, seasons: 8, people: 60, scenarioLimit: 2);
        Assert.Equal(first, second);
        foreach (string name in new[] { "bronze-reference", "iron " })
            Assert.Contains(name, first);
        Assert.Contains("I-T6", first);
    }
}
