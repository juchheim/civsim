using System;
using System.Linq;
using CivSim.Harness;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;

namespace CivSim.Tests;

public sealed class B28ProactiveDefenseDiagnosticTests
{
    [Fact]
    public void Proactive_defense_diagnostic_targets_both_b28_strategist_roles_only()
    {
        Player[] seats =
        {
            Player.Parse("legend", EconomyArchetype.Balanced),
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("random", EconomyArchetype.Balanced),
        };

        Assert.Equal(new[] { 0, 1 }, Tournament.B28ProactiveDefenseCivs(seats));
    }

    [Fact]
    public void Public_era_shadow_is_report_only_checksum_stable_and_limited_to_b28_defender_seats()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced), Player.Parse("random", EconomyArchetype.Balanced),
            Player.Parse("legend", EconomyArchetype.Balanced), Player.Parse("random", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, seats.Length).ToArray();

        MatchOutcome baseline = Tournament.Play(TestPack.Pack, 20261019, seats, archetypes, minutes: 2);
        MatchOutcome shadow = Tournament.Play(TestPack.Pack, 20261019, seats, archetypes, minutes: 2,
            warlordB28Trace: true, warlordB28PublicEraShadowDiagnostic: true);

        Assert.Equal(baseline.Checksum, shadow.Checksum);
        Assert.Equal(new[] { 0, 2 }, Tournament.B28ProactiveDefenseCivs(seats));
        Assert.Equal("2.0", Strategist.PublicEraDefenseWoodGap("candidate=Palisade wood=6.0 palisade[costWood=8,hearthReserve=0,requiredWood=8,queueLegal=True]"));
        Assert.Equal("n/a", Strategist.PublicEraDefenseWoodGap("candidate=Count wood=6.0"));
        Assert.All(shadow.B28Traces.Where(row => row.StartsWith("B28 public-E3 shadow", StringComparison.Ordinal)), row =>
        {
            Assert.Contains("order=shadow-only", row, StringComparison.Ordinal);
            Assert.DoesNotContain("c1", row, StringComparison.Ordinal);
            Assert.DoesNotContain("c3", row, StringComparison.Ordinal);
        });
    }


}
