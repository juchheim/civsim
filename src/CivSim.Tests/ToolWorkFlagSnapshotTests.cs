using System.IO;
using System.Linq;
using CivSim.Sim.Economy;

namespace CivSim.Tests;

/// <summary>
/// "Worked with a tool last season" is state the Iron kit-sharing and fission rules read at the next season start. It was left out of the
/// snapshot, the checksum and the figure clone, so a match restored from a save matched the live run at the restore tick (the checksums agreed)
/// and then diverged at the next season end (found in a Fief match restored at minute 90: discovery-match --seed 20261019 --mode fief --resume-at 90).
/// </summary>
public class ToolWorkFlagSnapshotTests
{
    private const ulong Seed = 20261019;

    private static (BronzeFixture Live, BronzeFixture Restored, int[] Flagged) Roundtrip(System.Func<BronzeFixture> create)
    {
        BronzeFixture live = create();
        int[] adults = Enumerable.Range(0, live.Economy.FigureCount).Where(i => live.Economy.Figure(i).Age >= 14).ToArray();
        Assert.True(adults.Length >= 4);
        int[] flagged = { adults[0], adults[2], adults[3] };
        foreach (int i in flagged) live.Economy.FixtureSetWorkedWithToolLastSeason(i, worked: true);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) live.Economy.WriteSnapshot(writer);
        stream.Position = 0;
        BronzeFixture restored = create();
        using (var reader = new BinaryReader(stream)) restored.Economy.ReadSnapshot(reader);
        return (live, restored, flagged);
    }

    private static void AssertFlagSurvives(BronzeFixture live, BronzeFixture restored, int[] flagged)
    {
        for (int i = 0; i < live.Economy.FigureCount; i++)
            Assert.Equal(flagged.Contains(i), restored.Economy.FixtureWorkedWithToolLastSeason(i));
        Assert.Equal(live.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
        // The checksum sees the flag, so a restore that dropped it could not hide behind equal checksums again.
        restored.Economy.FixtureSetWorkedWithToolLastSeason(flagged[0], worked: false);
        Assert.NotEqual(live.Economy.ComputeChecksum(), restored.Economy.ComputeChecksum());
    }

    [Fact]
    public void An_iron_economy_snapshot_keeps_who_worked_with_a_tool_last_season()
    {
        var (live, restored, flagged) = Roundtrip(() => BronzeFixture.CreateIron(TestPack.Pack, Seed, 60));
        AssertFlagSurvives(live, restored, flagged);
    }

    [Fact]
    public void A_fief_economy_snapshot_keeps_who_worked_with_a_tool_last_season()
    {
        var (live, restored, flagged) = Roundtrip(() => BronzeFixture.CreateFiefWithCamp(TestPack.Pack, Seed, 60));
        AssertFlagSurvives(live, restored, flagged);
    }
}
