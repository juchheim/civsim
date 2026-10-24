using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
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
    public void Snapshot_after_18_and_19_commands_replays_the_queued_refusals_identically()
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
    }
}
