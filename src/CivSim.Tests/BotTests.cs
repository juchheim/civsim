using System;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The scripted opponents (docs/MVP/14 §10.2) built on the AI interfaces.</summary>
public class BotTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Match(params CivControl[] controls)
        => MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced, controls);

    public static readonly TheoryData<BotKind> Kinds = new()
    {
        BotKind.Random, BotKind.OpeningGrower, BotKind.OpeningBuilder, BotKind.OpeningWanderer, BotKind.Rusher, BotKind.Turtle,
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void A_bot_keeps_its_research_slots_full_and_makes_progress(BotKind kind)
    {
        MatchModel match = Match(CivControl.Scripted(kind), CivControl.Council, CivControl.Council, CivControl.Council);
        var runner = new SimRunner(match);
        ResearchSystem research = match.Research;
        ResearchCatalog catalog = research.Catalog;
        int idleSeasonsWithAvailableProject = 0;
        for (int season = 0; season < 16; season++)
        {
            runner.Advance(150);
            if (season < 2) continue;
            // A completion can open a slot late in the season. Let the configured 30-tick AI selection grace expire
            // before treating an empty slot with an Available project as a bot omission. A completion during that
            // grace window starts a fresh timer, so defer this sample to the next season in that case.
            long graceStart = runner.Tick;
            runner.Advance(30);
            bool completedDuringGrace = false;
            foreach (ResearchEvent e in research.Events)
                if (e.Civ == 0 && e.Kind == ResearchEventKind.Completed && e.Tick >= graceStart)
                { completedDuringGrace = true; break; }
            if (completedDuringGrace) continue;
            bool hasEmptySlot = false;
            for (int s = 0; s < CivResearch.SlotCount; s++)
                if (research.Slot(0, s) < 0) { hasEmptySlot = true; break; }
            if (!hasEmptySlot) continue;

            bool hasUnslottedAvailableProject = false;
            for (int p = 0; p < catalog.Count; p++)
            {
                if (!catalog.Projects[p].Numeric || research.State(0, p) != ProjectState.Available) continue;
                bool slotted = false;
                for (int s = 0; s < CivResearch.SlotCount; s++)
                    if (research.Slot(0, s) == p) { slotted = true; break; }
                if (!slotted) { hasUnslottedAvailableProject = true; break; }
            }
            if (hasUnslottedAvailableProject) idleSeasonsWithAvailableProject++;
        }
        Assert.True(research.DoneCount(0) >= 8, $"{kind} finished only {research.DoneCount(0)} projects in 16 minutes.");
        Assert.True(idleSeasonsWithAvailableProject <= 1,
            $"{kind} left a slot empty while an Available project remained in {idleSeasonsWithAvailableProject} seasons.");
    }

    [Fact]
    public void The_opening_scripts_research_in_their_own_order()
    {
        MatchModel match = Match(CivControl.Scripted(BotKind.OpeningBuilder), CivControl.Scripted(BotKind.OpeningWanderer), CivControl.Council, CivControl.Council);
        new SimRunner(match).Advance(150 * 8);
        ResearchCatalog catalog = match.Research.Catalog;
        // The Builder opening starts STN-01 and FIR-01; the Wanderer opening WAY-01 and COM-01.
        Assert.True(match.Research.IsDone(0, catalog.IndexOf("STN-01")));
        Assert.True(match.Research.IsDone(1, catalog.IndexOf("WAY-01")));
    }

    [Fact]
    public void The_Turtle_fortifies_once_it_can_and_never_raids()
    {
        MatchModel match = Match(CivControl.Scripted(BotKind.Turtle), CivControl.Council, CivControl.Council, CivControl.Council);
        new SimRunner(match).Advance(150 * 4 * 24);
        if (match.Research.IsDone(0, match.Research.Catalog.IndexOf("GOV-02")))
            Assert.Equal(Posture.Fortify, match.Politics.PostureOf(0));
        foreach (var e in match.Politics.Events)
            Assert.False(e.Civ == 0 && e.Kind == Sim.Politics.PoliticsEventKind.RaidLaunched);
    }

    [Theory]
    [InlineData(BotKind.Random)]
    [InlineData(BotKind.Rusher)]
    public void Bots_are_deterministic_and_resume_exactly_from_a_save(BotKind kind)
    {
        CivControl[] controls = { CivControl.Scripted(kind), CivControl.Council, CivControl.Scripted(BotKind.OpeningGrower), CivControl.Council };
        MatchModel straight = Match(controls);
        new SimRunner(straight).Advance(150 * 12);
        MatchModel again = Match(controls);
        new SimRunner(again).Advance(150 * 12);
        Assert.Equal(straight.ComputeChecksum(), again.ComputeChecksum());
        MatchModel first = Match(controls);
        new SimRunner(first).Advance(150 * 6);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel resumed = Match(controls);
        using (var r = new BinaryReader(stream)) resumed.ReadSnapshot(r);
        new SimRunner(resumed, 50, 150 * 6).Advance(150 * 6);
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
    }
}
