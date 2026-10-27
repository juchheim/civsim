using System;
using System.IO;
using CivSim.Game.Ui;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Quick-save, replay and new-match plumbing behind the menu (was M7d). Results are reported as toasts.</summary>
public partial class Game
{
    private const uint OnboardingFileMagic = 0x4F534943; // "CISO", little-endian

    private void SaveQuickMatch()
    {
        try
        {
            if (_session is null || !_session.CanSave) throw new InvalidOperationException("Quick-save is available during a live match.");
            GameSave.Write(GameSave.QuickSavePath, _session.CaptureSave(_onboardingSeen, _introDismissed));
            ShowToast("Game saved", ToastKind.Info, "", key: "saved");
        }
        catch (Exception ex) { ShowToast("Couldn't save", ToastKind.Warning, ex.Message, key: "save-failed"); }
    }

    private void LoadQuickMatch()
    {
        try
        {
            if (_contentPack is null) throw new InvalidOperationException("The content pack is not loaded.");
            GameSaveData data = GameSave.Read(GameSave.QuickSavePath, _contentPack.Hash);
            SimulationSession restored = SimulationSession.Restore(data, _contentPack);
            _onboardingSeen = data.OnboardingSeen;
            _introDismissed |= data.IntroDismissed;
            if (_introDismissed) WriteIntroPreference();
            ReplaceSession(restored);
            ShowToast("Game loaded", ToastKind.Info, $"Year {(_current?.Year ?? 0) + 1}.", key: "loaded");
        }
        catch (Exception ex) { ShowToast("Couldn't load", ToastKind.Warning, ex.Message, key: "load-failed"); }
    }

    private void SaveLatestReplay()
    {
        try
        {
            if (_session is null || !_session.CanSave) throw new InvalidOperationException("Replay recording is unavailable for this session.");
            string path = GameSave.LatestReplayPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
                GameSave.WriteReplay(stream, _session.CreateReplay(), _session.Difficulty);
            File.Move(temporary, path, overwrite: true);
            ShowToast("Replay saved", ToastKind.Info, "", key: "replay-saved");
        }
        catch (Exception ex) { ShowToast("Couldn't save the replay", ToastKind.Warning, ex.Message, key: "replay-failed"); }
    }

    private void WatchLatestReplay()
    {
        try
        {
            if (_contentPack is null) throw new InvalidOperationException("The content pack is not loaded.");
            using var stream = new FileStream(GameSave.LatestReplayPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
            Replay replay = GameSave.ReadReplay(stream, out Difficulty difficulty);
            SimulationSession watcher = SimulationSession.Watch(replay, _contentPack, difficulty, 0);
            _onboardingSeen = 0;
            HideEndScreen();
            ReplaceSession(watcher);
            ShowToast("Watching a replay", ToastKind.Info, $"{replay.TotalTicks / 600:N0} simulated minutes.", key: "replay-watch");
        }
        catch (Exception ex) { ShowToast("Couldn't load the replay", ToastKind.Warning, ex.Message, key: "replay-load-failed"); }
    }

    private void ReplaceSession(SimulationSession replacement)
    {
        ClearSkirmishes();
        if (_peopleOpen) ClosePeoplePanel();
        _session?.Dispose();
        _session = replacement;
        if (!_session.Frames.TryAcquire() || _session.Frames.Current is null)
            throw new InvalidOperationException("The replacement simulation did not publish its initial frame.");
        _previous = _session.Frames.Previous;
        _current = _session.Frames.Current;
        _currentSequence = _current!.Sequence;
        _renderTick = _current.Tick;
        _width = _session.WorldView.Width;
        _height = _session.WorldView.Height;
        _terrainHeights = MakeHeights(_session.WorldView);
        _lastEventSequence = 0;
        _lastOnboardingEventSequence = 0;
        _endTimelineSequence = 0;
        _endTimeline.Clear();
        _chronicle.Clear();
        _chronicleUnread = 0;
        _chronicleUnseenCritical = false;
        _momentQueue.Clear();
        _momentProject = -1;
        if (_momentCard is not null) _momentCard.Visible = false;
        for (int s = 0; s < 3; s++) { _pendingProject[s] = -2; _lastSlotProject[s] = -2; _slotCards[s].Mode = SlotMode.Open; _slotCards[s].Project = -2; _slotCards[s].DoneTimer = 0; }
        foreach (ToastView toast in _toasts) DismissToast(toast);
        _endScreenShown = false;
        _endScreenDismissed = false;
        _openingHold = false;
        _openingHoldAssignmentPending = false;
        _pinnedTile = -1;
        _selectedResearchProject = -1;
        RebuildVisualRenderer(_current, _session);
        _visualRenderer?.Call("set_overlay", _mapOverlay);
        ApplyNewFrame(_current);
        FrameCapital();
        UpdatePausedTreatment();
        if (_chronicleOpen) RefreshChronicleRows();
    }

    private void StartNewMatch(ulong seed) => StartNewMatch(seed, _uiSettings.Difficulty);

    private void StartNewMatch(ulong seed, Difficulty difficulty)
    {
        if (_contentPack is null) return;
        _onboardingSeen = 0;
        _matchSeed = seed;
        HideEndScreen();
        ReplaceSession(new SimulationSession(seed, _contentPack, 0, 0, difficulty));
        if (_current is { FocusSlotProject: var slots } && slots[0] < 0 && slots[1] < 0 && slots[2] < 0)
        {
            BeginOpeningHold();
            OpenResearchWeb(-1);
        }
        ShowToast("New match", ToastKind.Info, $"Rivals: {DifficultyName(difficulty)}. Seed {seed}.", key: "new-match");
    }

    /// <summary>Per rendered frame: end-of-match detection and replay status.</summary>
    private void UpdateSessionState()
    {
        if (_session is null) return;
        if (_session.MatchEnded && !_endScreenShown && !_endScreenDismissed && _benchmarkSeconds == 0) ShowEndScreen();
        if (_session.IsReplay && _session.ReplayFinished && !_replayFinishedShown)
        {
            _replayFinishedShown = true;
            ShowToast("Replay finished", ToastKind.Info, _session.ReplayStatus, key: "replay-done");
        }
        if (!_session.IsReplay) _replayFinishedShown = false;
    }

    private bool _replayFinishedShown;

    private static bool IsDevelopmentPresentationRun()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--benchmark-", StringComparison.Ordinal) ||
                arg.StartsWith("--start-minute=", StringComparison.Ordinal) ||
                arg.StartsWith("--screenshot=", StringComparison.Ordinal) ||
                arg.StartsWith("--scenario=", StringComparison.Ordinal)) return true;
        return false;
    }

    private static string IntroPreferencePath => Path.Combine(OS.GetUserDataDir(), "onboarding.state");

    private static bool ReadIntroPreference()
    {
        try
        {
            string path = IntroPreferencePath;
            if (!File.Exists(path)) return false;
            using var reader = new BinaryReader(File.Open(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read));
            return reader.ReadUInt32() == OnboardingFileMagic && reader.ReadUInt32() == 1 && reader.ReadBoolean();
        }
        catch { return false; }
    }

    private static void WriteIntroPreference()
    {
        try
        {
            string path = IntroPreferencePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var writer = new BinaryWriter(File.Open(path, FileMode.Create, System.IO.FileAccess.Write, FileShare.None));
            writer.Write(OnboardingFileMagic);
            writer.Write(1U);
            writer.Write(true);
        }
        catch (IOException ex) { GD.PushWarning($"Could not save onboarding preference: {ex.Message}"); }
    }
}
