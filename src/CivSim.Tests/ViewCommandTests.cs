using System.IO;
using System;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>A human player's politics orders as commands (docs/MVP/18 §7): appended kinds, the AI's rules, replayable.</summary>
public class ViewCommandTests
{
    [Fact]
    public void War_peace_and_raids_can_be_ordered_by_command_and_show_in_the_frame()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Advance(10);
        match.Politics.ForceContact(0, 1);
        match.Politics.ForceContact(0, 2);
        match.Politics.ForceContact(0, 3);
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, match.Research.Catalog.IndexOf("WAR-02"), 0, 0));
        runner.Submit(PlayerCommands.DeclareWar(runner.Tick, 0, 2));
        // Malformed orders report stable refusal codes through the view event stream.
        runner.Submit(PlayerCommands.LaunchRaid(runner.Tick, 0, -5, 4));
        runner.Submit(PlayerCommands.DeclareWar(runner.Tick, 0, 99));
        runner.Submit(PlayerCommands.MakePeace(runner.Tick, 0, -1));
        runner.Submit(PlayerCommands.RemoveBeacon(runner.Tick, 0, -1));
        runner.Submit(PlayerCommands.FormAlliance(runner.Tick, 0, 3)); // needs DIP-03 on both sides
        runner.Advance(1);
        Assert.True(match.Politics.Diplomacy.AtWar(0, 2));
        Assert.False(match.Politics.Diplomacy.Allied(0, 3));

        runner.Submit(PlayerCommands.MakePeace(runner.Tick, 0, 2));
        runner.Advance(1);
        Assert.False(match.Politics.Diplomacy.AtWar(0, 2));

        // A raid on each rival's first settlement until one can be walked to (civilization 1 shares the continent).
        for (int s = 0; s < match.Economy.SettlementCount && match.Politics.Expeditions.Count == 0; s++)
        {
            if (match.Economy.SettlementLite(s).Civilization == 0) continue;
            runner.Submit(PlayerCommands.LaunchRaid(runner.Tick, 0, s, 4));
            runner.Advance(1);
        }
        Assert.NotEmpty(match.Politics.Expeditions);
        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);
        Assert.Equal(1, frame.PartyCount);
        Assert.Equal((byte)PartyKind.Raid, frame.PartyKind[0]);
        int raiders = 0;
        for (int i = 0; i < frame.FigureCount; i++)
            if (frame.FigureActivity[i] == (byte)FigureActivity.OnRaid)
            {
                raiders++;
                Assert.NotEqual(0, frame.FigureFlags[i] & (ushort)(FigureFlags.Away | FigureFlags.Armed));
            }
        Assert.Equal(frame.PartyMembers[0], raiders);
        bool logged = false;
        for (int i = 0; i < frame.EventCount; i++) logged |= frame.Events[i].Kind == ViewEventKind.WarDeclared;
        Assert.True(logged);
        bool refusalLogged = false;
        for (int i = 0; i < frame.EventCount; i++)
            refusalLogged |= frame.Events[i].Kind == ViewEventKind.OrderRefused && frame.Events[i].A == (int)CommandKind.LaunchRaid;
        Assert.True(refusalLogged);
        runner.Submit(PlayerCommands.RecallParty(runner.Tick, 0, 0));
        runner.Advance(1);
        Assert.Empty(match.Politics.Expeditions);
    }

    [Fact]
    public void Replays_carry_the_new_command_kinds()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        runner.Submit(PlayerCommands.DeclareWar(5, 0, 1)); // refused (no contact) but recorded
        runner.Submit(PlayerCommands.SendGift(7, 0, 2));
        runner.Submit(PlayerCommands.LaunchRaid(9, 0, 1, 6));
        runner.Advance(300);
        MatchReplay replay = MatchReplay.FromRun(ViewKit.Seed, TestPack.Pack, ViewKit.Balanced, runner,
            Sim.Economy.WorldEconomyInput.FromGeneratedWorld(ViewKit.World.Value).Fingerprint);
        using var stream = new MemoryStream();
        replay.Write(stream);
        stream.Position = 0;
        MatchReplay read = MatchReplay.Read(stream);
        Assert.Equal(3, read.Replay.Commands.Count);
        Assert.Equal(CommandKind.LaunchRaid, read.Replay.Commands[2].Kind);
        Assert.Equal(6, read.Replay.Commands[2].B);
        Assert.True(read.Verify(TestPack.Pack).Ok);
        // Numbering is append-only.
        Assert.Equal(9, (int)CommandKind.SetCouncil);
        Assert.Equal(10, (int)CommandKind.SendGift);
        Assert.Equal(16, (int)CommandKind.FormAlliance);
        Assert.Equal(17, (int)CommandKind.RecallParty);
    }

    [Fact]
    public void External_mind_is_match_setup_not_snapshot_state_and_party_recall_is_a_command()
    {
        MatchModel match = ViewKit.NewMatch();
        ulong checksum = match.ComputeChecksum();
        byte[] before = WriteSnapshot(match);
        match.Politics.SetExternalMind(0, true);
        Assert.True(match.Politics.IsExternalMind(0));
        Assert.Equal(checksum, match.ComputeChecksum());
        Assert.Equal(before, WriteSnapshot(match));

        MatchModel restored = ViewKit.NewMatch();
        using (var stream = new MemoryStream(before))
        using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true)) restored.ReadSnapshot(reader);
        Assert.False(restored.Politics.IsExternalMind(0));
        restored.Politics.SetExternalMind(0, true);
        Assert.True(restored.Politics.IsExternalMind(0));
    }

    private static byte[] WriteSnapshot(MatchModel match)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        return stream.ToArray();
    }
}
