using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Game;

/// <summary>Owns the deterministic match thread and publishes only viewer-filtered frames to Godot.</summary>
public sealed class SimulationSession : IDisposable
{
    private const int TicksPerSecond = 10;
    private const int PublishEveryTicks = 10;
    private const long MatchTimeCapTicks = 40L * 60 * TicksPerSecond;

    private readonly MatchModel _match;
    private readonly SimRunner _runner;
    private readonly FrameBuilder _builder;
    private readonly Thread _thread;
    private readonly object _stateLock = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly ConcurrentQueue<Command> _commands = new();
    private readonly List<Command> _recordedCommands = new();
    private readonly List<ChecksumRecord> _recordedChecksums = new();
    private readonly List<ChecksumRecord> _expectedReplayChecksums = new();
    private readonly ulong _seed;
    private readonly byte[] _packHash;
    private readonly int _viewerCiv;
    private readonly bool _replayMode;
    private readonly long _replayTargetTick;
    private volatile bool _running = true;
    private int _paused;
    private int _speedIndex = 1;
    private int _runnerChecksumCount;
    private int _replayChecksumsMatched;
    private long _publishedTick;
    private int _matchEnded;
    private int _replayFinished;
    private int _initialStartMinute;
    private string? _replayStatus;

    public SimulationSession(ulong seed, ContentPack pack, int startMinute = 0, int viewerCiv = 0)
        : this(seed, pack, startMinute, viewerCiv, null, null)
    {
    }

    private SimulationSession(ulong seed, ContentPack pack, int startMinute, int viewerCiv,
        GameSaveData? saveData, Replay? replay)
    {
        if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
        if (saveData is not null && startMinute != 0) throw new ArgumentException("A saved match cannot also use a start minute.");
        if (replay is not null && saveData is not null) throw new ArgumentException("A session cannot load both a save and a replay.");
        _seed = seed;
        _packHash = (byte[])pack.Hash.Clone();
        _viewerCiv = viewerCiv;
        _replayMode = replay is not null;
        _replayTargetTick = replay?.TotalTicks ?? 0;
        _initialStartMinute = startMinute;

        World = WorldGenerator.Generate(seed);
        WorldView = CivSim.Sim.View.WorldView.FromGenerated(World);
        var archetypes = StandardArchetypes();
        var controls = StandardControls();
        _match = MatchModel.Create(seed, pack, World, archetypes, controls, playerCouncil: false, gentleStart: true);
        // Civilization 0 is the human seat: political orders come from the front end.
        _match.Politics.SetExternalMind(0, true);

        if (saveData is not null)
        {
            using var snapshot = new MemoryStream(saveData.Snapshot, writable: false);
            using var reader = new BinaryReader(snapshot);
            _match.ReadSnapshot(reader);
            if (snapshot.Position != snapshot.Length || _match.Economy.Tick != saveData.Tick)
                throw new InvalidDataException("The save snapshot tick or length is invalid.");
            if (_match.ComputeChecksum() != saveData.StateChecksum)
                throw new InvalidDataException("The save snapshot checksum does not match its header.");
            if (_match.Ai is not null)
                foreach (AiWhyEntry entry in saveData.AiWhyEntries)
                    _match.Ai.Log.Add(new AiDecision(entry.Tick, entry.Tick / ResearchSystem.SeasonTicks > int.MaxValue
                        ? int.MaxValue : (int)(entry.Tick / ResearchSystem.SeasonTicks), entry.Civ, entry.Layer,
                        entry.Kind, -1, -1, Fix64.FromRaw(entry.ScoreRaw), entry.Reason));
            _runner = new SimRunner(_match, saveData.ChecksumInterval, saveData.Tick);
            _recordedCommands.AddRange(saveData.Commands);
            _recordedChecksums.AddRange(saveData.Checksums);
            _runnerChecksumCount = 0;
            _speedIndex = saveData.SpeedIndex;
            _paused = saveData.Paused ? 1 : 0;
            foreach (Command pending in saveData.QueuedCommands) _commands.Enqueue(pending);
        }
        else
        {
            _runner = new SimRunner(_match, replay?.ChecksumInterval ?? SimRunner.DefaultChecksumInterval);
            if (replay is not null)
            {
                if (!replay.ContentHash.AsSpan().SequenceEqual(pack.Hash))
                    throw new InvalidDataException("Replay content hash differs from the loaded content pack.");
                if (replay.TotalTicks < 0 || replay.ChecksumInterval <= 0)
                    throw new InvalidDataException("Replay tick count or checksum interval is invalid.");
                foreach (Command command in replay.Commands) _runner.Submit(command);
                _expectedReplayChecksums.AddRange(replay.Checksums);
                _speedIndex = 1;
                _paused = replay.TotalTicks == 0 ? 1 : 0;
                if (replay.TotalTicks == 0)
                {
                    _replayFinished = 1;
                    _replayStatus = "Replay is empty.";
                }
            }
            else if (startMinute > 0)
            {
                _runner.Advance((long)startMinute * 60 * TicksPerSecond);
#if DEBUG
                if (Array.IndexOf(Godot.OS.GetCmdlineUserArgs(), "--dev-island-colony") >= 0) DevIslandColony();
#endif
            }
        }

        _builder = new FrameBuilder(_match, viewerCiv);
        if (saveData is not null)
        {
            using var viewState = new MemoryStream(saveData.ViewState, writable: false);
            using var reader = new BinaryReader(viewState);
            _builder.ReadState(reader);
            if (viewState.Position != viewState.Length) throw new InvalidDataException("Unexpected trailing view-state data in save.");
        }

        Frames = new FrameStore();
        Frames.Reserve(figures: 4096, settlements: 128, herds: _match.Economy.WildHerdCount,
            parties: 128, plots: 4096, aiWhy: 32);
        Frames.BuildAndPublish(_builder, _match);
        _publishedTick = _runner.Tick;
        if (!_replayMode && IsMatchOver()) _matchEnded = 1;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "CivSim simulation",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.Start();
    }

    public GeneratedWorld World { get; }
    public WorldView WorldView { get; }
    public FrameStore Frames { get; }
#if DEBUG
    /// <summary>Dev only: the index of the rival village the island colony targets (-1 until <c>--dev-island-colony</c> has run).</summary>
    public int DevRivalSettlement { get; private set; } = -1;

    /// <summary>
    /// Dev only (<c>--dev-island-colony</c>, debug builds): moves civilization 0's fighting-age people to the island nearest a
    /// rival village and gives them the outrigger, so a party can be seen sailing (docs/MVP/21). It writes the economy directly,
    /// outside the command path, so a match started this way is never a valid replay; it exists for screenshots.
    /// </summary>
    private void DevIslandColony()
    {
        PoliticsSystem politics = _match.Politics;
        var probe = new FrameBuilder(_match, -1);
        var frame = new MatchFrame();
        probe.Build(_match, frame);
        int home = -1, rivalTile = -1;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (home < 0 && frame.SettlementCiv[i] == 0) home = frame.SettlementTile[i];
            if (rivalTile < 0 && frame.SettlementCiv[i] == 1) { rivalTile = frame.SettlementTile[i]; DevRivalSettlement = frame.SettlementIndex[i]; }
        }
        if (home < 0 || rivalTile < 0) { Godot.GD.Print($"DEV island colony: no home/rival (home {home}, rival {rivalTile})"); return; }
        foreach (string project in new[] { "SEA-04", "WAR-02" })
            _runner.Submit(new Command(_runner.Tick, 0, CommandKind.CompleteProject, _match.Research.Catalog.IndexOf(project), 0, 0));
        _runner.Advance(2);
        int island = -1, best = int.MaxValue;
        for (int t = 0; t < frame.Width * frame.Height; t++)
            if (politics.IsIsland(t) && politics.CanReach(0, home, t) && HexGrid.Distance(t, rivalTile, frame.Width) < best)
            { best = HexGrid.Distance(t, rivalTile, frame.Width); island = t; }
        if (island < 0) { Godot.GD.Print("DEV island colony: no reachable island"); return; }
        var people = new List<long>();
        for (int i = 0; i < frame.FigureCount; i++)
            if (frame.FigureCiv[i] == 0 || frame.FigureCiv[i] is 2 or 3 && i % 2 == 0) people.Add(frame.FigureId[i]);   // plenty of fighters; half of the far civilizations stay put
        _match.Economy.FoundSettlement(0, island, people);
        politics.ForceContact(0, 1);
        _runner.Advance(160);
        Godot.GD.Print($"DEV island colony: {people.Count} people moved to island tile {island} (rival at {rivalTile}, home {home}); sail={politics.CanSail(0)}");
    }
#endif

    public long Tick => Interlocked.Read(ref _publishedTick);
    public bool Paused => Volatile.Read(ref _paused) != 0;
    public double Speed => SpeedAt(Volatile.Read(ref _speedIndex));
    public bool MatchEnded => Volatile.Read(ref _matchEnded) != 0;
    public bool IsReplay => _replayMode;
    public bool ReplayFinished => Volatile.Read(ref _replayFinished) != 0;
    public string ReplayStatus => Volatile.Read(ref _replayStatus) ?? "Replay is playing.";
    public bool CanSave => _initialStartMinute == 0 && !_replayMode;
    public bool CanIssueCommands => !_replayMode && !MatchEnded;

    internal static SimulationSession Restore(GameSaveData save, ContentPack pack)
        => new(save.Seed, pack, 0, save.ViewerCiv, save, null);

    internal static SimulationSession Watch(Replay replay, ContentPack pack, int viewerCiv = 0)
        => new(replay.MatchSeed, pack, 0, viewerCiv, null, replay);

    public void SetPaused(bool paused)
    {
        if (MatchEnded || ReplayFinished) paused = true;
        Volatile.Write(ref _paused, paused ? 1 : 0);
        _wake.Set();
    }

    public void TogglePaused() => SetPaused(!Paused);

    public void SetSpeedIndex(int index)
    {
        Volatile.Write(ref _speedIndex, Math.Clamp(index, 0, 3));
        _wake.Set();
    }

    public void ChangeSpeed(int delta) => SetSpeedIndex(Math.Clamp(Volatile.Read(ref _speedIndex) + delta, 0, 3));

    public void Queue(Command command)
    {
        if (!CanIssueCommands) return;
        _commands.Enqueue(command);
        _wake.Set();
    }

    internal GameSaveData CaptureSave(ulong onboardingSeen, bool introDismissed)
    {
        if (!CanSave) throw new InvalidOperationException("Development fast-forward sessions cannot be saved.");
        lock (_stateLock)
        {
            using var snapshotStream = new MemoryStream();
            using (var writer = new BinaryWriter(snapshotStream, System.Text.Encoding.UTF8, leaveOpen: true))
                _match.WriteSnapshot(writer);
            using var viewStream = new MemoryStream();
            using (var writer = new BinaryWriter(viewStream, System.Text.Encoding.UTF8, leaveOpen: true))
                _builder.WriteState(writer);
            var whyEntries = new List<AiWhyEntry>(32);
            IReadOnlyList<AiDecision>? decisions = _match.Ai?.Log.Entries;
            if (decisions is not null)
            {
                int count = Math.Min(32, decisions.Count);
                int first = decisions.Count - count;
                for (int i = first; i < decisions.Count; i++)
                {
                    AiDecision decision = decisions[i];
                    whyEntries.Add(new AiWhyEntry(decision.Tick, (byte)decision.Civ, decision.Layer,
                        decision.Kind, decision.Score.Raw, decision.Reason));
                }
            }
            return new GameSaveData
            {
                Seed = _seed,
                ViewerCiv = _viewerCiv,
                Tick = _runner.Tick,
                ChecksumInterval = _runner.ChecksumInterval,
                SpeedIndex = Volatile.Read(ref _speedIndex),
                Paused = Paused,
                StateChecksum = _runner.CurrentChecksum,
                PackHash = (byte[])_packHash.Clone(),
                Snapshot = snapshotStream.ToArray(),
                ViewState = viewStream.ToArray(),
                Commands = new List<Command>(_recordedCommands),
                Checksums = new List<ChecksumRecord>(_recordedChecksums),
                QueuedCommands = new List<Command>(_commands.ToArray()),
                AiWhyEntries = whyEntries,
                OnboardingSeen = onboardingSeen,
                IntroDismissed = introDismissed,
            };
        }
    }

    public Replay CreateReplay()
    {
        lock (_stateLock)
        {
            return new Replay
            {
                MatchSeed = _seed,
                ContentHash = (byte[])_packHash.Clone(),
                ChecksumInterval = _runner.ChecksumInterval,
                TotalTicks = _runner.Tick,
                Commands = new List<Command>(_recordedCommands),
                Checksums = new List<ChecksumRecord>(_recordedChecksums),
            };
        }
    }

    private void Run()
    {
        while (_running)
        {
            if (Paused)
            {
                _wake.WaitOne(200);
                continue;
            }

            int speedIndex = Volatile.Read(ref _speedIndex);
            double speed = SpeedAt(speedIndex);
            long started = Stopwatch.GetTimestamp();
            bool stop = false;
            lock (_stateLock)
            {
                if (!_running) break;
                if (Paused) continue;
                while (_commands.TryDequeue(out Command command))
                {
                    Command applied = command with { Tick = _runner.Tick };
                    _runner.Submit(applied);
                    if (!_replayMode) _recordedCommands.Add(applied);
                }

                long steps = PublishEveryTicks;
                if (_replayMode) steps = Math.Min(steps, Math.Max(0, _replayTargetTick - _runner.Tick));
                for (long step = 0; step < steps; step++)
                {
                    _runner.Advance(1);
                    CaptureNewChecksums();
                    if (_replayMode && ReplayFinished)
                    {
                        stop = true;
                        break;
                    }
                    if (_replayMode && _runner.Tick >= _replayTargetTick)
                    {
                        FinishReplay();
                        stop = true;
                        break;
                    }
                    if (!_replayMode && IsMatchOver())
                    {
                        Volatile.Write(ref _matchEnded, 1);
                        stop = true;
                        break;
                    }
                }
                Frames.BuildAndPublish(_builder, _match);
                Interlocked.Exchange(ref _publishedTick, _runner.Tick);
            }

            if (stop) SetPaused(true);
            if (!_running || Paused) continue;

            double targetSeconds = PublishEveryTicks / (TicksPerSecond * speed);
            double elapsed = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            int waitMs = (int)Math.Max(0, (targetSeconds - elapsed) * 1000.0);
            if (waitMs > 0) _wake.WaitOne(waitMs);
        }
    }

    private void CaptureNewChecksums()
    {
        IReadOnlyList<ChecksumRecord> current = _runner.Checksums;
        while (_runnerChecksumCount < current.Count)
        {
            ChecksumRecord actual = current[_runnerChecksumCount++];
            if (_replayMode)
            {
                if (_replayChecksumsMatched >= _expectedReplayChecksums.Count ||
                    _expectedReplayChecksums[_replayChecksumsMatched] != actual)
                {
                    Volatile.Write(ref _replayStatus, $"Checksum mismatch at tick {actual.Tick}.");
                    Volatile.Write(ref _replayFinished, 1);
                    Volatile.Write(ref _paused, 1);
                    return;
                }
                _replayChecksumsMatched++;
            }
            else _recordedChecksums.Add(actual);
        }
    }

    private void FinishReplay()
    {
        if (_replayChecksumsMatched == _expectedReplayChecksums.Count)
            Volatile.Write(ref _replayStatus, _replayChecksumsMatched == 0
                ? "Replay finished; no periodic checksums were recorded."
                : $"Replay finished; {_replayChecksumsMatched:N0} periodic checksums match.");
        else
            Volatile.Write(ref _replayStatus, $"Replay ended before all checksums were reached ({_replayChecksumsMatched}/{_expectedReplayChecksums.Count}).");
        Volatile.Write(ref _replayFinished, 1);
    }

    private bool IsMatchOver()
    {
        if (_runner.Tick >= MatchTimeCapTicks) return true;
        for (int civ = 0; civ < 4; civ++)
            if (_match.Research.Era(civ) >= 3) return true;
        return false;
    }

    private static EconomyArchetype[] StandardArchetypes() => new[]
    {
        EconomyArchetype.Balanced, EconomyArchetype.Grower,
        EconomyArchetype.Builder, EconomyArchetype.Wanderer,
    };

    private static CivControl[] StandardControls() => new[]
    {
        CivControl.Council,
        CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Grower),
        CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Builder),
        CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Wanderer),
    };

    private static double SpeedAt(int index) => index switch
    {
        0 => 0.5,
        1 => 1.0,
        2 => 2.0,
        _ => 4.0,
    };

    public void Dispose()
    {
        _running = false;
        _wake.Set();
        if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join();
        _wake.Dispose();
    }
}
