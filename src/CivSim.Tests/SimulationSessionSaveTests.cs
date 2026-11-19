using System;
using System.IO;
using System.Threading;
using CivSim.Game;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class SimulationSessionSaveTests
{
    [Fact]
    public void Fief_replay_footer_round_trip_starts_the_watcher_in_Fief_mode()
    {
        const ulong seed = 842171;
        using var original = new SimulationSession(seed, TestPack.Pack, mode: ChapterMode.Fief);
        original.SetPaused(true);
        WaitForStableTick(original);

        using var stream = new MemoryStream();
        GameSave.WriteReplay(stream, original.CreateReplay(), original.Difficulty, original.Mode);
        stream.Position = 0;
        var replay = GameSave.ReadReplay(stream, out var difficulty, out var mode);
        using var watcher = SimulationSession.Watch(replay, TestPack.Pack, difficulty, mode: mode);

        Assert.Equal(ChapterMode.Fief, mode);
        Assert.Equal(ChapterMode.Fief, watcher.Mode);
    }

    [Fact]
    public void Fief_session_save_load_and_resume_preserves_mode_and_exact_state()
    {
        const ulong seed = 842170;
        string path = Path.Combine(Path.GetTempPath(), $"civsim-session-{Guid.NewGuid():N}.civsave");
        using var original = new SimulationSession(seed, TestPack.Pack, mode: ChapterMode.Fief);
        try
        {
            original.SetPaused(true);
            WaitForStableTick(original);
            GameSaveData captured = original.CaptureSave(0, false);
            Assert.Equal(ChapterMode.Fief, captured.Mode);

            GameSave.Write(path, captured);
            GameSaveData loaded = GameSave.Read(path, TestPack.Pack.Hash);
            using var restored = SimulationSession.Restore(loaded, TestPack.Pack);
            Assert.True(restored.Paused);
            Assert.Equal(ChapterMode.Fief, restored.Mode);
            Assert.Equal(captured.Tick, restored.Tick);

            GameSaveData restoredState = restored.CaptureSave(0, false);
            Assert.Equal(captured.StateChecksum, restoredState.StateChecksum);
            Assert.Equal(captured.Snapshot, restoredState.Snapshot);

            restored.SetSpeedIndex(3);
            restored.SetPaused(false);
            WaitForTickAdvance(restored, captured.Tick);
            restored.SetPaused(true);
            WaitForStableTick(restored);
            GameSaveData resumed = restored.CaptureSave(0, false);
            Assert.Equal(ChapterMode.Fief, resumed.Mode);
            Assert.True(resumed.Tick > captured.Tick);
            Assert.NotEqual(captured.StateChecksum, resumed.StateChecksum);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static long WaitForStableTick(SimulationSession session)
    {
        long previous = session.Tick;
        int stableSamples = 0;
        for (int i = 0; i < 1000; i++)
        {
            Thread.Sleep(10);
            long current = session.Tick;
            if (current == previous) stableSamples++;
            else { previous = current; stableSamples = 0; }
            if (session.Paused && stableSamples >= 3) return current;
        }
        throw new TimeoutException("The paused session did not reach a stable tick.");
    }

    private static void WaitForTickAdvance(SimulationSession session, long startTick)
    {
        for (int i = 0; i < 500; i++)
        {
            if (session.Tick > startTick) return;
            Thread.Sleep(10);
        }
        throw new TimeoutException("The resumed session did not advance.");
    }
}
