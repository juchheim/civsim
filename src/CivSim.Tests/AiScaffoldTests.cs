using System;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The AI plumbing (docs/MVP/14 §2): seats, the own view, the fog boundary, orders and saves. The minds themselves are tested with their own parts.</summary>
public class AiScaffoldTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static MatchModel Match(params CivControl[] controls)
        => MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced, controls);

    [Fact]
    public void Difficulty_profiles_come_from_the_registry()
    {
        DifficultyProfile settler = DifficultyProfile.From(TestPack.Pack, Difficulty.Settler);
        Assert.Equal((1, 2, 20, ReplanCadence.Yearly, 2), (settler.HorizonEras, settler.BeamWidth, settler.CouncilPriorPercent, settler.Replan, settler.ReactionDelaySeasons));
        DifficultyProfile warlord = DifficultyProfile.From(TestPack.Pack, Difficulty.Warlord);
        Assert.Equal((3, 8, 0, OpponentModel.Full, ReplanCadence.SeasonAndEvents), (warlord.HorizonEras, warlord.BeamWidth, warlord.CouncilPriorPercent, warlord.Opponent, warlord.Replan));
        Assert.Equal(16, DifficultyProfile.From(TestPack.Pack, Difficulty.Legend).BeamWidth);
    }

    [Fact]
    public void A_match_with_only_Council_civilizations_has_no_AI_system()
    {
        Assert.Null(Match(CivControl.Council, CivControl.Council, CivControl.Council, CivControl.Council).Ai);
        Assert.Null(MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced).Ai);
    }

    [Fact]
    public void AI_seats_build_their_own_view_and_hand_research_to_the_Spark()
    {
        MatchModel match = Match(CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Sage), CivControl.Council,
            CivControl.Scripted(BotKind.Rusher), CivControl.Council);
        Assert.NotNull(match.Ai);
        Assert.NotNull(match.Ai!.SeatOf(0));
        Assert.Null(match.Ai.SeatOf(1));
        Assert.NotNull(match.Ai.SeatOf(2));
        Assert.True(match.Research.IsAiControlled(0));
        Assert.False(match.Research.IsAiControlled(1));
        new SimRunner(match).Advance(150 * 8);
        OwnView own = match.Ai.SeatOf(0)!.Own;
        Assert.Equal(0, own.Civ);
        Assert.True(own.Population >= 10 && own.Settlements.Count >= 1);
        Assert.True(own.InsightPerYear > Sim.Numerics.Fix64.Zero);
        Assert.Equal(8, own.Season);
        Assert.False(own.Others[0].Contacted);
        // Even with the placeholder Spark the Council fills the slots, so research runs.
        Assert.True(match.Research.DoneCount(0) >= 3);
    }

    [Fact]
    public void Orders_keep_the_hard_rules_and_act_only_for_their_civilization()
    {
        MatchModel match = Match(CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Warlord), CivControl.Council, CivControl.Council, CivControl.Council);
        new SimRunner(match).Advance(150 * 4);
        AiContext ctx = match.Ai!.SeatOf(0)!.Context;
        // No contact, no projects: nothing can be ordered.
        Assert.False(ctx.Orders.Gift(1));
        Assert.False(ctx.Orders.Barter(1));
        Assert.False(ctx.Orders.DeclareWar(1));
        Assert.False(ctx.Orders.SetPosture(CivSim.Sim.Core.Posture.Raid)); // GOV-02 first
        Assert.False(ctx.Orders.PlaceBeacon(100));                         // ART-05 first
        // The port can set slots like a player: Available project accepted, Hidden refused.
        int forOne = ctx.Catalog.IndexOf("FOR-01"), hidden = ctx.Catalog.IndexOf("STN-05");
        ctx.Research.SetSlot(2, ctx.Catalog.IndexOf("WAY-01"));
        Assert.Equal(ctx.Catalog.IndexOf("WAY-01"), match.Research.Slot(0, 2));
        ctx.Research.SetSlot(1, hidden);
        Assert.NotEqual(hidden, match.Research.Slot(0, 1));
        _ = forOne;
    }

    [Fact]
    public void The_AI_system_saves_and_resumes_exactly()
    {
        CivControl[] controls = { CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Grower), CivControl.Council, CivControl.Scripted(BotKind.Turtle), CivControl.Council };
        MatchModel straight = Match(controls);
        new SimRunner(straight).Advance(150 * 12);
        MatchModel first = Match(controls);
        new SimRunner(first).Advance(150 * 6);
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.WriteSnapshot(w);
        stream.Position = 0;
        MatchModel resumed = Match(controls);
        using (var r = new BinaryReader(stream)) resumed.ReadSnapshot(r);
        Assert.Equal(first.ComputeChecksum(), resumed.ComputeChecksum());
        new SimRunner(resumed, 50, 150 * 6).Advance(150 * 6);
        Assert.Equal(straight.ComputeChecksum(), resumed.ComputeChecksum());
    }
}
