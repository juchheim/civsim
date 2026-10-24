using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Harness;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class A7HarnessTelemetryTests
{
    [Fact]
    public void Check_a7_option_is_recognized_by_the_ai_command_parser()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => AiCommand.Run(new[] { "--check-a7" }));
        Assert.Contains("ai needs --pack FILE", error.Message);
    }

    [Fact]
    public void Short_tournament_match_populates_A7_match_counters_from_AI_seats()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes =
        {
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
            EconomyArchetype.Balanced, EconomyArchetype.Balanced,
        };

        MatchOutcome match = Tournament.Play(TestPack.Pack, Tournament.BaseSeed, seats, archetypes, minutes: 1);

        Assert.Equal(1, match.A7AiSeatMatches);
        Assert.Equal(match.Civs[0].AvoidableStarvationDeaths, match.AvoidableStarvationDeaths);
        Assert.Equal(match.Civs[0].UndefendedLosses, match.UndefendedLosses);
        Assert.Equal(match.Civs[0].AttributedHerdCrashes, match.AttributedHerdCrashes);
        Assert.Equal(match.Civs[0].IdleLaborSeasons, match.IdleLaborSeasons);
        Assert.True(match.AvoidableStarvationDeaths >= 0);
        Assert.True(match.UndefendedLosses >= 0);
        Assert.True(match.AttributedHerdCrashes >= 0);
        Assert.True(match.IdleLaborSeasons >= 0);
    }

    [Fact]
    public void Tournament_stops_and_scores_at_the_chapter_cap_even_when_more_minutes_were_requested()
    {
        Player[] seats =
        {
            Player.Parse("warlord", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
            Player.Parse("council", EconomyArchetype.Balanced),
        };
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        // A one-year cap ends at four seasons (one simulated minute); request three minutes to catch
        // regressions where the harness keeps recording post-chapter AI scores or telemetry.
        MatchOutcome match = Tournament.Play(PackWithChapterCapYear(1), Tournament.BaseSeed,
            seats, archetypes, minutes: 3);

        Assert.Equal(600, match.FinalTick);
        Assert.Equal(4, match.SeasonsProcessed);
        Assert.Equal(1, match.A7AiSeatMatches);
        Assert.Equal(match.Civs[0].AvoidableStarvationDeaths, match.AvoidableStarvationDeaths);
        Assert.Equal(match.Civs[0].IdleLaborSeasons, match.IdleLaborSeasons);
    }

    [Fact]
    public void Tournament_stops_and_scores_at_the_KEY4_rival_countdown()
    {
        Player[] seats = Enumerable.Repeat(Player.Parse("council", EconomyArchetype.Balanced), 4).ToArray();
        EconomyArchetype[] archetypes = Enumerable.Repeat(EconomyArchetype.Balanced, 4).ToArray();

        // Make the complete research tree cheap and immediately eligible so this stays a small harness regression,
        // while retaining the real KEY-4 end rule and a Year 64 cap.
        MatchOutcome match = Tournament.Play(PackWithFastResearch(), Tournament.BaseSeed,
            seats, archetypes, minutes: 64);

        Assert.True(match.FinalTick < 64 * 600, $"Expected the KEY-4 countdown before Year 64, got tick {match.FinalTick}.");
        Assert.Equal(match.FinalTick / 150, match.SeasonsProcessed);
        Assert.Equal(0, match.FinalTick % 150); // the match closes only after a complete season advance
        Assert.All(match.Civs, civ => Assert.True(civ.Projects > 0));
    }

    private static ContentPack PackWithChapterCapYear(int year)
    {
        ContentValue root = TestPack.Pack.Root;
        ContentValue balance = root.Get("balance")!;
        ContentValue constants = balance.Get("constants")!;
        var changedConstants = new List<ContentValue>();
        foreach (ContentValue constant in constants.Items)
        {
            if (constant.GetString("id") != "ChapterCapYear")
            {
                changedConstants.Add(constant);
                continue;
            }

            changedConstants.Add(ContentValue.MapOf(constant.Entries.Select(entry =>
                new KeyValuePair<string, ContentValue>(entry.Key,
                    entry.Key == "value" ? ContentValue.Of((long)year) : entry.Value))));
        }

        ContentValue changedBalance = ContentValue.MapOf(balance.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "constants" ? ContentValue.ListOf(changedConstants) : entry.Value)));
        ContentValue changedRoot = ContentValue.MapOf(root.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "balance" ? changedBalance : entry.Value)));
        return ContentPack.FromTree(changedRoot);
    }

    private static ContentPack PackWithFastResearch()
    {
        ContentValue root = TestPack.Pack.Root;
        ContentValue projects = root.Get("projects")!;
        var changedProjects = new List<ContentValue>();
        foreach (ContentValue project in projects.Items)
        {
            if (project.GetString("status") != "numeric")
            {
                changedProjects.Add(project);
                continue;
            }

            changedProjects.Add(ContentValue.MapOf(project.Entries.Select(entry =>
                new KeyValuePair<string, ContentValue>(entry.Key, entry.Key switch
                {
                    "cost" => ContentValue.Of(Fix64.One),
                    "requires" or "requires_any" => ContentValue.ListOf(Array.Empty<ContentValue>()),
                    "breadth" when entry.Value.Kind == ValueKind.Map => ContentValue.MapOf(entry.Value.Entries.Select(breadth =>
                        new KeyValuePair<string, ContentValue>(breadth.Key,
                            breadth.Key == "min" ? ContentValue.Of(0L) : breadth.Value))),
                    _ => entry.Value,
                }))));
        }

        ContentValue changedRoot = ContentValue.MapOf(root.Entries.Select(entry =>
            new KeyValuePair<string, ContentValue>(entry.Key,
                entry.Key == "projects" ? ContentValue.ListOf(changedProjects) : entry.Value)));
        return ContentPack.FromTree(changedRoot);
    }
}
