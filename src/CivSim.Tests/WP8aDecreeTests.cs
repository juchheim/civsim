using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>WP8a command and Decree queue contracts (docs/MVP/22 §4.5, §4.6).</summary>
public sealed class WP8aDecreeTests
{
    private const ulong Seed = 20261019;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private sealed class Rig
    {
        public readonly MatchModel Match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        public readonly SimRunner Runner;
        public Rig()
        {
            Runner = new SimRunner(Match);
            for (int civ = 0; civ < 4; civ++) Match.Politics.SetDecreePolicy(civ, null);
        }
        public void Season() => Runner.Advance(150);
        public void Grant(int civ, string project)
        {
            Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, TestPack.Catalog.IndexOf(project), 0, 0));
            Runner.Advance(1);
        }
    }

    [Fact]
    public void Compact_arguments_round_trip_each_packed_field()
    {
        int packed = DecreeArgs.PackCompact(TradeGood.Tin, sell: true, CompactVariant.Treaty);

        Assert.Equal(TradeGood.Tin, DecreeArgs.Good(packed));
        Assert.True(DecreeArgs.Sell(packed));
        Assert.Equal(CompactVariant.Treaty, DecreeArgs.Variant(packed));
        Assert.Equal(17, DecreeArgs.FolkSettlement(DecreeArgs.FolkParty(17)));
        Assert.True(DecreeArgs.IsFolkParty(DecreeArgs.FolkParty(17)));
    }

    [Fact]
    public void IssueDecree_rejects_invalid_civilization_and_kind_without_queueing()
    {
        var rig = new Rig();

        Assert.False(rig.Match.Politics.IssueDecree(-1, DecreeKind.Found, 0, 0));
        Assert.False(rig.Match.Politics.IssueDecree(0, (DecreeKind)7, 0, 0));
        Assert.Empty(rig.Match.Politics.PendingDecrees);
    }

    [Fact]
    public void Player_Decree_without_unlocked_Mandate_is_refused_and_spends_nothing()
    {
        var rig = new Rig();
        int before = rig.Match.Research.Mandate(0);
        rig.Runner.Submit(new Command(rig.Runner.Tick, 0, CommandKind.IssueDecree,
            (int)DecreeKind.Found, 0, 0));
        rig.Season();

        PoliticsEvent refusal = Assert.Single(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.DecreeRefused && e.Civ == 0);
        Assert.Equal((int)DecreeKind.Found, refusal.A);
        Assert.Equal((int)OrderRefusalReason.NoMandate, refusal.B);
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.OrderRefused && e.Civ == 0 && e.A == (int)CommandKind.IssueDecree);
        Assert.Equal(before, rig.Match.Research.Mandate(0));
        Assert.Equal(0, rig.Match.Politics.DecreesDone(0));
    }

    [Fact]
    public void Key3_unlocks_the_Mandate_command_gate()
    {
        var rig = new Rig();
        rig.Grant(0, "KEY-3");
        Assert.True(rig.Match.Research.MandateUnlocked(0));
        Assert.True(rig.Match.Politics.DecreeCost(0, DecreeKind.Found) > 0);
    }

    [Fact]
    public void Council_seeks_Tin_after_MET03_before_MET05_is_gated_on_Tin()
    {
        var rig = new Rig();
        var research = rig.Match.Politics.DecreeResearch;
        Assert.False(CouncilDecreePolicy.ShouldSeekTinCompact(research, rig.Match.Economy, rig.Match.Politics, 0));

        rig.Grant(0, "KEY-3");
        rig.Grant(0, "MET-03");

        Assert.True(research.IsDone(0, "MET-03"));
        Assert.False(research.IsDone(0, "MET-05"));
        Assert.False(rig.Match.Economy.CivHasMetalAccess(0, MetalAccessKind.Tin));
        Assert.True(CouncilDecreePolicy.ShouldSeekTinCompact(research, rig.Match.Economy, rig.Match.Politics, 0));
        Assert.True(rig.Match.Economy.TradeNeed(0, "G-TIN", 4) > Fix64.Zero);
    }

    [Fact]
    public void FoundCheck_rejects_an_invalid_tile_as_NoSite()
    {
        var rig = new Rig();

        Assert.Equal(OrderRefusalReason.NoSite, rig.Match.Politics.FoundCheck(0, -1, out int donor));
        Assert.Equal(-1, donor);
    }

    [Fact]
    public void Embargo_requires_Key3_and_toggles_the_ordered_trade_lane()
    {
        var rig = new Rig();
        Assert.False(rig.Match.Politics.OrderEmbargo(0, 1, TradeGood.Tin, on: true));

        rig.Grant(0, "KEY-3");
        rig.Match.Politics.ForceContact(0, 1);
        int relations = rig.Match.Politics.Diplomacy.Relations(0, 1);
        Assert.True(rig.Match.Politics.OrderEmbargo(0, 1, TradeGood.Tin, on: true));
        Assert.True(rig.Match.Politics.Embargoes(0, 1, TradeGood.Tin));
        Assert.Equal(relations - 10, rig.Match.Politics.Diplomacy.Relations(0, 1));
        Assert.True(rig.Match.Politics.OrderEmbargo(0, 1, TradeGood.Tin, on: false));
        Assert.False(rig.Match.Politics.Embargoes(0, 1, TradeGood.Tin));
    }

    [Fact]
    public void Snapshot_and_match_replay_after_18_and_19_commands_reproduce_queued_refusals()
    {
        var first = new Rig();
        first.Runner.Advance(149);
        first.Runner.Submit(new Command(first.Runner.Tick, 0, CommandKind.IssueDecree, (int)DecreeKind.Found, 0, 0));
        first.Runner.Submit(new Command(first.Runner.Tick, 0, CommandKind.SetEmbargo, 1, (int)TradeGood.Tin, 1));
        first.Runner.Advance(1); // both commands reach the season-boundary politics step
        Assert.Contains(first.Match.Politics.Events, e => e.Kind == PoliticsEventKind.DecreeRefused && e.A == (int)DecreeKind.Found);
        Assert.Contains(first.Match.Politics.Events, e => e.Kind == PoliticsEventKind.OrderRefused && e.A == (int)CommandKind.SetEmbargo);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) first.Match.WriteSnapshot(writer);
        stream.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        using (var reader = new BinaryReader(stream)) resumed.ReadSnapshot(reader);
        Assert.Equal(first.Match.ComputeChecksum(), resumed.ComputeChecksum());

        first.Runner.Advance(150);
        var resumedRunner = new SimRunner(resumed, 50, first.Runner.Tick - 150);
        resumedRunner.Advance(150);
        Assert.Equal(first.Match.ComputeChecksum(), resumed.ComputeChecksum());

        MatchReplay replay = MatchReplay.FromRun(Seed, TestPack.Pack, Balanced, first.Runner,
            WorldEconomyInput.FromGeneratedWorld(World.Value).Fingerprint);
        using var replayStream = new MemoryStream();
        replay.Write(replayStream);
        replayStream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(replayStream);
        Assert.Equal(
            new[] { CommandKind.IssueDecree, CommandKind.SetEmbargo },
            loaded.Replay.Commands.Select(c => c.Kind));
        ReplayVerification verification = loaded.Verify(TestPack.Pack);
        Assert.True(verification.Ok, verification.Message);
    }

    [Fact]
    public void Successful_embargo_command_round_trips_through_binary_match_replay()
    {
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);
        var runner = new SimRunner(match, checksumInterval: 1);
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject,
            TestPack.Catalog.IndexOf("KEY-3"), 0, 0));
        runner.Advance(1);
        Assert.True(match.Research.MandateUnlocked(0));

        runner.Submit(new Command(runner.Tick, 0, CommandKind.SetEmbargo, 1, (int)TradeGood.Tin, 1));
        runner.Advance(1);
        Assert.True(match.Politics.Embargoes(0, 1, TradeGood.Tin));
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.EmbargoSet &&
            e.Civ == 0 && e.Other == 1 && e.A == (int)TradeGood.Tin && e.B == 1);

        MatchReplay replay = MatchReplay.FromRun(Seed, TestPack.Pack, Balanced, runner,
            WorldEconomyInput.FromGeneratedWorld(World.Value).Fingerprint);
        using var replayStream = new MemoryStream();
        replay.Write(replayStream);
        replayStream.Position = 0;
        MatchReplay loaded = MatchReplay.Read(replayStream);
        Assert.Equal(new[] { CommandKind.CompleteProject, CommandKind.SetEmbargo },
            loaded.Replay.Commands.Select(c => c.Kind));
        ReplayVerification verification = loaded.Verify(TestPack.Pack);
        Assert.True(verification.Ok, verification.Message);
    }
}
