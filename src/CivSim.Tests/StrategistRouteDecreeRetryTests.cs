using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Politics;
using Xunit;

namespace CivSim.Tests;

public sealed class StrategistRouteDecreeRetryTests
{
    [Fact]
    public void Out_of_reach_route_decree_at_mandate_cap_starts_eight_season_cooldown()
    {
        const int civ = 0, season = 20;
        const long tick = 12345;
        var intent = new StrategicIntent { PendingRouteDecreeKind = (int)DecreeKind.Expedition, PendingRouteDecreeSeason = season };
        var events = new[]
        {
            new PoliticsEvent(tick, civ, PoliticsEventKind.DecreeRefused, -1,
                (int)DecreeKind.Expedition, (int)OrderRefusalReason.OutOfReach),
        };

        AiSystem.ObserveRouteDecreeOutcome(intent, civ, season, tick, mandate: 4, mandateCap: 4, events);

        Assert.Equal(28, intent.RouteDecreeRetryAfterSeason);
        Assert.Equal(-1, intent.PendingRouteDecreeKind);
        Assert.Equal(-1, intent.PendingRouteDecreeSeason);
        Assert.True(StrategistDecreePolicy.IsRouteDecreeCooldownActive(27, intent.RouteDecreeRetryAfterSeason));
        Assert.False(StrategistDecreePolicy.IsRouteDecreeCooldownActive(28, intent.RouteDecreeRetryAfterSeason));
    }

    [Fact]
    public void Levy_refusal_at_cap_uses_the_same_route_cooldown()
    {
        const int civ = 0, season = 20;
        const long tick = 12345;
        var intent = new StrategicIntent { PendingRouteDecreeKind = (int)DecreeKind.Levy, PendingRouteDecreeSeason = season };
        var events = new[]
        {
            new PoliticsEvent(tick, civ, PoliticsEventKind.DecreeRefused, -1,
                (int)DecreeKind.Levy, (int)OrderRefusalReason.OutOfReach),
        };

        AiSystem.ObserveRouteDecreeOutcome(intent, civ, season, tick, mandate: 4, mandateCap: 4, events);

        Assert.Equal(28, intent.RouteDecreeRetryAfterSeason);
    }

    [Fact]
    public void Route_decree_refusal_below_cap_does_not_delay_other_exploration()
    {
        const int civ = 0, season = 20;
        const long tick = 12345;
        var intent = new StrategicIntent { PendingRouteDecreeKind = (int)DecreeKind.Expedition, PendingRouteDecreeSeason = season };
        var events = new[]
        {
            new PoliticsEvent(tick, civ, PoliticsEventKind.DecreeRefused, -1,
                (int)DecreeKind.Expedition, (int)OrderRefusalReason.OutOfReach),
        };

        AiSystem.ObserveRouteDecreeOutcome(intent, civ, season, tick, mandate: 3, mandateCap: 4, events);

        Assert.Equal(-1, intent.RouteDecreeRetryAfterSeason);
        Assert.Equal(-1, intent.PendingRouteDecreeKind);
    }

    [Fact]
    public void Retry_cooldown_survives_strategic_intent_snapshot_round_trip()
    {
        var intent = new StrategicIntent { RouteDecreeRetryAfterSeason = 28 };
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) intent.Write(writer);
        stream.Position = 0;
        var restored = new StrategicIntent();
        using (var reader = new BinaryReader(stream)) restored.Read(reader);

        Assert.Equal(28, restored.RouteDecreeRetryAfterSeason);
        Assert.False(StrategistDecreePolicy.IsRouteDecreeCooldownActive(28, restored.RouteDecreeRetryAfterSeason));
    }

    [Fact]
    public void Route_decree_cooldown_pauses_only_expedition_and_levy()
    {
        Assert.True(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Expedition, cooldownActive: true));
        Assert.True(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Levy, cooldownActive: true));
        Assert.False(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Mine, cooldownActive: true));
        Assert.False(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Found, cooldownActive: true));
        Assert.False(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Compact, cooldownActive: true));
        Assert.False(StrategistDecreePolicy.ShouldPauseRouteDecree(DecreeKind.Expedition, cooldownActive: false));
    }
}
