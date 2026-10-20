using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Game;

/// <summary>Owns the deterministic match thread and publishes only viewer-filtered frames to Godot.</summary>
public sealed class SimulationSession : IDisposable
{
    private const int TicksPerSecond = 10;
    private const int PublishEveryTicks = 10;

    private readonly MatchModel _match;
    private readonly SimRunner _runner;
    private readonly FrameBuilder _builder;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly ConcurrentQueue<Command> _commands = new();
    private volatile bool _running = true;
    private int _paused;
    private int _speedIndex = 1;
    private long _publishedTick;

    public SimulationSession(ulong seed, ContentPack pack, int startMinute = 0, int viewerCiv = 0)
    {
        if (startMinute < 0) throw new ArgumentOutOfRangeException(nameof(startMinute));
        World = WorldGenerator.Generate(seed);
        WorldView = CivSim.Sim.View.WorldView.FromGenerated(World);
        var archetypes = new[]
        {
            EconomyArchetype.Balanced, EconomyArchetype.Grower,
            EconomyArchetype.Builder, EconomyArchetype.Wanderer,
        };
        var controls = new[]
        {
            CivControl.Council,
            CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Grower),
            CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Builder),
            CivControl.Ai(Difficulty.Chieftain, EconomyArchetype.Wanderer),
        };
        _match = MatchModel.Create(seed, pack, World, archetypes, controls);
        // Civilization 0 is the human seat: political orders come from the front end.
        _match.Politics.SetExternalMind(0, true);
        _runner = new SimRunner(_match);
        if (startMinute > 0) _runner.Advance((long)startMinute * 600);
        _builder = new FrameBuilder(_match, viewerCiv);
        Frames = new FrameStore();
        Frames.Reserve(figures: 4096, settlements: 128, herds: _match.Economy.WildHerdCount,
            parties: 128, plots: 4096);
        Frames.BuildAndPublish(_builder, _match);
        _publishedTick = _runner.Tick;
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
    public long Tick => Interlocked.Read(ref _publishedTick);
    public bool Paused => Volatile.Read(ref _paused) != 0;
    public double Speed => SpeedAt(Volatile.Read(ref _speedIndex));

    public void SetPaused(bool paused)
    {
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

    public void Queue(Command command) => _commands.Enqueue(command);

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
            while (_commands.TryDequeue(out Command command))
                _runner.Submit(command with { Tick = _runner.Tick });

            _runner.Advance(PublishEveryTicks);
            Frames.BuildAndPublish(_builder, _match);
            Interlocked.Exchange(ref _publishedTick, _runner.Tick);

            double targetSeconds = PublishEveryTicks / (TicksPerSecond * speed);
            double elapsed = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
            int waitMs = (int)Math.Max(0, (targetSeconds - elapsed) * 1000.0);
            if (waitMs > 0) _wake.WaitOne(waitMs);
        }
    }

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
        if (_thread.IsAlive && Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(2));
        _wake.Dispose();
    }
}
