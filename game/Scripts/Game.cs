using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CivSim.Sim.Core;
using CivSim.Sim.Content;
using CivSim.Sim.View;
using CivSim.Sim.World;
using CivSim.Sim.Research;
using CivSim.Game.Looks;
using Godot;

namespace CivSim.Game;

/// <summary>The Godot presentation layer. It reads MatchFrame only and sends orders through a queue.</summary>
public partial class Game : Node3D
{
    private const float HexWidth = 1.0f;
    private const float RowSpacing = 0.8660254f;
    private const float HexRadius = 0.5773503f;
    private const float MapUnit = HexLayout.Unit;

    private static readonly Color[] CivColors =
    {
        new("#E69F00"), new("#56B4E9"), new("#009E73"), new("#F0E442"),
        new("#0072B2"), new("#D55E00"), new("#CC79A7"), new("#4D4D4D"),
    };

    private static readonly Color[] SpeciesColors =
    {
        new("#8b5a33"), new("#3b2c22"), new("#4a3d33"), new("#5a3d26"), new("#9a6a3c"),
        new("#8c8478"), new("#e3d9c3"), new("#c79a5f"), new("#c8a26e"), new("#8f8372"),
    };

    private SimulationSession? _session;
    private ContentPack? _contentPack;
    private IGameLook _look = null!;
    private Node3D? _visualRenderer;
    private Camera3D? _camera;
    private DirectionalLight3D? _sun;
    private Godot.Environment? _environment;
    private ShaderMaterial? _terrainMaterial;
    private ShaderMaterial? _waterMaterial;
    private Image? _stateImage;
    private ImageTexture? _stateTexture;
    private MeshInstance3D? _terrainNode;
    private MeshInstance3D? _waterNode;
    private MeshInstance3D? _riverNode;
    private MeshInstance3D? _borderNode;
    private MultiMeshInstance3D? _figureBodyNode;
    private MultiMeshInstance3D? _figureHeadNode;
    private MultiMeshInstance3D? _figureCarryNode;
    private MultiMeshInstance3D? _herdNode;
    private MultiMeshInstance3D? _campNode;
    private MultiMeshInstance3D? _beaconNode;
    private MultiMeshInstance3D? _bannerPoleNode;
    private MultiMeshInstance3D? _bannerFlagNode;
    private MultiMeshInstance3D? _orchardCanopyNode;
    private MultiMeshInstance3D? _resourceNode;
    private MultiMeshInstance3D?[] _structureNodes = Array.Empty<MultiMeshInstance3D?>();
    private MultiMesh[] _structureMeshes = Array.Empty<MultiMesh>();
    private Mesh[] _structureModels = Array.Empty<Mesh>();
    private ResearchCatalog? _catalog;
    private readonly Dictionary<MultiMesh, float[]> _instanceBuffers = new();
    private readonly List<Transform3D> _bodyTransforms = new(4096), _headTransforms = new(4096), _carryTransforms = new(4096);
    private readonly List<Color> _bodyColors = new(4096), _headColors = new(4096), _carryColors = new(4096);
    private readonly List<Transform3D> _animalTransforms = new(4096);
    private readonly List<Color> _animalColors = new(4096);

    private MatchFrame? _current;
    private MatchFrame? _previous;
    private long _currentSequence;
    private int _structureSignature = int.MinValue;
    private int _width;
    private int _height;
    private float[] _terrainHeights = Array.Empty<float>();
    private Vector3 _target;
    private float _zoom = 20.0f;
    private float _yaw = -0.6f;
    private float _pitch = Mathf.DegToRad(45.0f);
    private double _renderTick;
    private string? _screenshotPath;
    private float _screenshotTimer;
    private int _benchmarkSeconds;
    private bool _benchmarkConfigured;
    private bool _benchmarkMatrix;
    private string[] _benchmarkOverlays = Array.Empty<string>();
    private int _benchmarkCaseIndex;
    private string _benchmarkOverlay = "none";
    private double _benchmarkWarmupRemaining;
    private readonly Stopwatch _benchmarkWatch = new();
    private readonly Stopwatch _benchmarkWindowWatch = new();
    private int _benchmarkFrames;
    private int _benchmarkWindowFrames;
    private long _benchmarkStartTick;
    private double _benchmarkFrameUpdateMs;
    private double _benchmarkMaxFrameUpdateMs;
    private int _benchmarkFrameUpdates;
    private double _benchmarkMinWindowFps = double.MaxValue;
    private int _lastSeason = -1;
    private double _hudRefreshMs, _benchmarkHudMs;
    private bool _resourcesVisible;
    private int _mapOverlay;

    public override void _Ready()
    {
        _look = LoadLook();
        DisplayServer.WindowSetTitle($"CivSim — {_look.Name}");
        string? packPath = FindPackPath();
        if (packPath is null)
        {
            ShowBootError();
            return;
        }

        try
        {
            ulong seed = ReadSeed();
            ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
            _contentPack = pack;
            _catalog = new ResearchCatalog(pack);
            int startMinute = ReadStartMinute();
            int viewerCiv = ReadViewerCiv();
            _session = new SimulationSession(seed, pack, startMinute, viewerCiv);
            _width = _session.WorldView.Width;
            _height = _session.WorldView.Height;
            _terrainHeights = MakeHeights(_session.WorldView);
            if (_session.Frames.TryAcquire())
            {
                _previous = _session.Frames.Previous;
                _current = _session.Frames.Current;
                if (_current is not null)
                {
                    _currentSequence = _current.Sequence;
                    _renderTick = _previous?.Tick ?? _current.Tick;
                }
            }
            if (_current is null) throw new InvalidOperationException("Simulation did not publish its initial MatchFrame.");
            RebuildVisualRenderer(_current, _session);
            _resourceNode = CreateMultiMeshNode(_look.CreateSphere(), "KnownResourceDeposits", _look.LitVertexMaterial());
            _resourceNode.Visible = false;
            ReadOverlayArgument();
            CreateHud(seed);
            UpdateKnownResources(_current);
            FrameCapital();
            ReadCaptureArgument();
            _benchmarkSeconds = ReadBenchmarkSeconds();
            _benchmarkConfigured = _benchmarkSeconds > 0;
            if (_benchmarkSeconds > 0)
            {
                _session.SetPaused(false);
                _session.SetSpeedIndex(1);
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                _benchmarkOverlays = ReadBenchmarkMatrix();
                _benchmarkMatrix = _benchmarkOverlays.Length > 0;
                if (_benchmarkMatrix)
                {
                    _benchmarkCaseIndex = 0;
                    ConfigureBenchmarkCase();
                    _benchmarkWarmupRemaining = 1.0;
                    GD.Print($"M7A benchmark matrix ready: {3 * _benchmarkOverlays.Length} cases, " +
                             $"{_current?.FigureCount ?? 0} figures, live 1x simulation, 1s warm-up + {_benchmarkSeconds}s per case.");
                }
                else
                {
                    int zoomLevel = ReadBenchmarkZoom();
                    _benchmarkOverlay = ReadOverlayName();
                    _benchmarkStartTick = _session.Tick;
                    SetZoom(zoomLevel switch { 0 => 5.0f, 1 => 12.0f, 2 => 40.0f, _ => 90.0f });
                    _benchmarkWarmupRemaining = 2.0;
                    GD.Print($"M7A benchmark ready: Z{zoomLevel}, overlay={_benchmarkOverlay}, {_current?.FigureCount ?? 0} figures, " +
                             $"live 1x simulation (paused={_session.Paused}), 2s warm-up + {_benchmarkSeconds}s measurement.");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"CivSim startup failed: {ex}");
            ShowBootError(ex.Message);
        }
    }

    public override void _ExitTree()
    {
        _session?.Dispose();
        _session = null;
    }

    public override void _Process(double delta)
    {
        if (_session is null || _current is null) return;
        if (_session.Frames.TryAcquire())
        {
            MatchFrame? newest = _session.Frames.Current;
            if (newest is not null && newest.Sequence != _currentSequence)
            {
                _previous = _session.Frames.Previous;
                _current = newest;
                _currentSequence = newest.Sequence;
                _renderTick = _previous?.Tick ?? newest.Tick;
                long frameUpdateStarted = _benchmarkSeconds > 0 ? Stopwatch.GetTimestamp() : 0;
                ApplyNewFrame(newest);
                if (_benchmarkSeconds > 0)
                {
                    double updateMs = (Stopwatch.GetTimestamp() - frameUpdateStarted) * 1000.0 / Stopwatch.Frequency;
                    _benchmarkFrameUpdateMs += updateMs;
                    _benchmarkMaxFrameUpdateMs = Math.Max(_benchmarkMaxFrameUpdateMs, updateMs);
                    _benchmarkFrameUpdates++;
                }
            }
        }

        UpdateSessionState();

        if (!_session.Paused)
            _renderTick = Math.Min(_current.Tick, _renderTick + delta * 10.0 * _session.Speed);
        float alpha = FrameAlpha(_renderTick);
        UpdateActors(alpha);
        if (_benchmarkSeconds > 0)
            RecordBenchmarkFrame(delta);
        long hudStarted = Stopwatch.GetTimestamp();
        TickHud(delta);
        _uiUpdateMs = _uiUpdateMs * 0.95 + (Stopwatch.GetTimestamp() - hudStarted) * 1000.0 / Stopwatch.Frequency * 0.05;

        if (_screenshotPath is not null && !_benchmarkConfigured && !_scenarioActive)
        {
            _screenshotTimer += (float)delta;
            if (_screenshotTimer >= 3.0f)
            {
                Error result = SaveScreenshot(_screenshotPath);
                GD.Print($"Screenshot {result}: {_screenshotPath}");
                _screenshotPath = null;
                GetTree().Quit();
            }
        }
    }

    private string? FindPackPath()
    {
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string? envPath = System.Environment.GetEnvironmentVariable("CIVSIM_PACK");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath)) return Path.GetFullPath(envPath);
        string[] candidates =
        {
            Path.Combine(projectDir, "civsim.pack"),
            Path.GetFullPath(Path.Combine(projectDir, "..", "civsim.pack")),
        };
        foreach (string path in candidates)
            if (File.Exists(path)) return path;
        return null;
    }

    private static ulong ReadSeed()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--seed=", StringComparison.Ordinal) &&
                ulong.TryParse(arg.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
                return seed;
        return 20261019UL;
    }

    private static int ReadStartMinute()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--start-minute=", StringComparison.Ordinal) &&
                int.TryParse(arg.Substring("--start-minute=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minute))
                return Math.Clamp(minute, 0, 240);
        return 0;
    }

    private static int ReadViewerCiv()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--viewer=", StringComparison.Ordinal) &&
                int.TryParse(arg.Substring("--viewer=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int viewer))
                return viewer == -1 ? -1 : Math.Clamp(viewer, 0, 3);
        return 0;
    }

    private void ReadCaptureArgument()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--screenshot=", StringComparison.Ordinal))
                _screenshotPath = Path.GetFullPath(arg.Substring("--screenshot=".Length));
    }

    private static int ReadBenchmarkSeconds()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--benchmark-seconds=", StringComparison.Ordinal) &&
                int.TryParse(arg.Substring("--benchmark-seconds=".Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int seconds))
                return Math.Clamp(seconds, 1, 60);
        return 0;
    }

    private static int ReadBenchmarkZoom()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--benchmark-zoom=", StringComparison.Ordinal) &&
                int.TryParse(arg.Substring("--benchmark-zoom=".Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int zoom))
                return Math.Clamp(zoom, 0, 3);
        return 0;
    }

    private static string[] ReadBenchmarkMatrix()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--benchmark-matrix=", StringComparison.Ordinal))
            {
                string value = arg.Substring("--benchmark-matrix=".Length).ToLowerInvariant();
                if (value == "all") return new[] { "claims", "food", "methods", "resources" };
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(value => value is "claims" or "food" or "methods" or "resources")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        return Array.Empty<string>();
    }

    private static string ReadOverlayName()
    {
        string overlay = ArgumentValue("--overlay=")?.ToLowerInvariant() ?? "none";
        return overlay is "claims" or "food" or "methods" or "resources" ? overlay : "none";
    }

    private static Godot.Collections.Dictionary BuildFramePayload(MatchFrame frame, WorldView world, bool includeWorld)
    {
        int tiles = frame.Width * frame.Height;
        var figures = new int[frame.FigureCount * 8];
        for (int i = 0; i < frame.FigureCount; i++)
        {
            int o = i * 8;
            figures[o] = frame.FigureX[i];
            figures[o + 1] = frame.FigureY[i];
            figures[o + 2] = frame.FigureCiv[i];
            figures[o + 3] = frame.FigureAge[i];
            figures[o + 4] = frame.FigureActivity[i];
            figures[o + 5] = frame.FigureFlags[i];
            figures[o + 6] = frame.FigureToolRung[i];
            figures[o + 7] = frame.FigureSettlement[i];
        }

        var herds = new int[frame.HerdCount * 4];
        for (int i = 0; i < frame.HerdCount; i++)
        {
            int o = i * 4;
            herds[o] = frame.HerdX[i];
            herds[o + 1] = frame.HerdY[i];
            herds[o + 2] = frame.HerdSpecies[i];
            herds[o + 3] = frame.HerdAnimals[i];
        }

        var settlements = new Godot.Collections.Array();
        int kinds = MatchFrame.StructureKinds;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            var structures = new int[kinds];
            for (int kind = 0; kind < kinds; kind++)
                structures[kind] = frame.SettlementStructures[i * kinds + kind];
            var settlement = new Godot.Collections.Dictionary
            {
                ["tile"] = frame.SettlementTile[i],
                ["civ"] = (int)frame.SettlementCiv[i],
                ["population"] = frame.SettlementPopulation[i],
                ["food"] = (int)frame.SettlementFood[i],
                ["structures"] = structures,
                ["remembered"] = (frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) != 0,
            };
            settlements.Add(settlement);
        }

        var plots = new int[frame.PlotCount * 2];
        for (int i = 0; i < frame.PlotCount; i++)
        {
            plots[i * 2] = frame.PlotTile[i];
            plots[i * 2 + 1] = frame.PlotKind[i];
        }

        byte[] foodOverlay = BuildFoodOverlay(frame);
        byte[] forecastOverlay = BuildFoodForecastOverlay(frame);
        byte[] methodOverlay = BuildMethodOverlay(frame);
        byte[] methodFallbackOverlay = BuildMethodFallbackOverlay(frame);

        var owners = new int[tiles];
        for (int i = 0; i < tiles; i++) owners[i] = frame.Owner[i];
        var claimants = ToIntArray(frame.Claimant, tiles);
        var payload = new Godot.Collections.Dictionary
        {
            ["width"] = frame.Width,
            ["height"] = frame.Height,
            ["tick"] = frame.Tick,
            ["year"] = frame.Year,
            ["seasonOfYear"] = frame.SeasonOfYear,
            ["owner"] = owners,
            ["claimant"] = claimants,
            ["claim"] = Slice(frame.Claim, tiles),
            ["visibility"] = Slice(frame.Visibility, tiles),
            ["camps"] = Slice(frame.CampTile, frame.CampCount),
            ["campCiv"] = ToIntArray(frame.CampCiv, frame.CampCount),
            ["beacons"] = Slice(frame.BeaconTile, frame.BeaconCount),
            ["beaconCiv"] = ToIntArray(frame.BeaconCiv, frame.BeaconCount),
            ["figures"] = figures,
            ["herds"] = herds,
            ["settlements"] = settlements,
            ["plots"] = plots,
            ["foodOverlay"] = foodOverlay,
            ["forecastOverlay"] = forecastOverlay,
            ["methodOverlay"] = methodOverlay,
            ["methodFallbackOverlay"] = methodFallbackOverlay,
        };
        if (includeWorld)
        {
            payload["biome"] = world.Biome;
            payload["elevation"] = world.Elevation;
            var worldFlags = new int[tiles];
            for (int i = 0; i < tiles; i++) worldFlags[i] = world.Flags[i];
            payload["flags"] = worldFlags;
        }
        return payload;
    }

    private static T[] Slice<T>(T[] values, int count)
    {
        var result = new T[count];
        Array.Copy(values, result, count);
        return result;
    }

    private static int[] ToIntArray(sbyte[] values, int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = values[i];
        return result;
    }

    private void RecordBenchmarkFrame(double delta)
    {
        if (_benchmarkWarmupRemaining > 0)
        {
            _benchmarkWarmupRemaining -= delta;
            if (_benchmarkWarmupRemaining <= 0)
            {
                _benchmarkWatch.Restart();
                _benchmarkWindowWatch.Restart();
            }
            return;
        }

        _benchmarkFrames++;
        _benchmarkWindowFrames++;
        if (_benchmarkWindowWatch.Elapsed.TotalSeconds >= 1.0)
        {
            _benchmarkMinWindowFps = Math.Min(_benchmarkMinWindowFps,
                _benchmarkWindowFrames / _benchmarkWindowWatch.Elapsed.TotalSeconds);
            _benchmarkWindowFrames = 0;
            _benchmarkWindowWatch.Restart();
        }

        if (_benchmarkWatch.Elapsed.TotalSeconds < _benchmarkSeconds) return;
        double elapsed = _benchmarkWatch.Elapsed.TotalSeconds;
        double averageFps = _benchmarkFrames / elapsed;
        long simulationTicks = _session is null ? 0 : _session.Tick - _benchmarkStartTick;
        bool simulationPaused = _session?.Paused ?? true;
        int renderObjects = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame);
        int renderPrimitives = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
        int renderDrawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        int zoom = _zoom switch { < 7 => 0, < 22 => 1, < 60 => 2, _ => 3 };
        GD.Print($"M7A_PERF zoom=Z{zoom} overlay={_benchmarkOverlay} sample={_benchmarkCaseIndex + 1} figures={_current?.FigureCount ?? 0} " +
                 $"simulation_paused={simulationPaused} tick_advance={simulationTicks} " +
                 $"seconds={elapsed:F2} frames={_benchmarkFrames} avg_fps={averageFps:F1} " +
                 $"min_1s_fps={_benchmarkMinWindowFps:F1} " +
                 $"render_objects={renderObjects} render_primitives={renderPrimitives} draw_calls={renderDrawCalls} " +
                 $"frame_updates={_benchmarkFrameUpdates} avg_frame_update_ms=" +
                 $"{(_benchmarkFrameUpdates > 0 ? _benchmarkFrameUpdateMs / _benchmarkFrameUpdates : 0):F2} " +
                 $"max_frame_update_ms={_benchmarkMaxFrameUpdateMs:F2}");
        if (_screenshotPath is not null)
        {
            Error result = SaveScreenshot(_screenshotPath);
            GD.Print($"Screenshot {result}: {_screenshotPath}");
        }
        if (_benchmarkMatrix && _benchmarkCaseIndex + 1 < 3 * _benchmarkOverlays.Length)
        {
            _benchmarkCaseIndex++;
            ConfigureBenchmarkCase();
            return;
        }
        _benchmarkSeconds = 0;
        GetTree().Quit();
    }

    private void ShowBootError(string details = "")
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var label = new Label
        {
            Position = new Vector2(48, 48),
            Size = new Vector2(1300, 300),
            Text = "CivSim content pack not found.\nBuild it from the repository root:\n\n" +
                   "dotnet run --project src/CivSim.Cli -c Release -- build-pack --out civsim.pack\n\n" + details,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", new Color("#f3efe4"));
        layer.AddChild(label);
    }

    private Error SaveScreenshot(string path)
    {
        using ViewportTexture texture = GetViewport().GetTexture();
        using Image image = texture.GetImage();
        return image.SavePng(path);
    }

    private void CreateEnvironment()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("#456f9c"),
            SkyHorizonColor = new Color("#d1dedf"),
            GroundHorizonColor = new Color("#aababb"),
            GroundBottomColor = new Color("#536a6a"),
            SunAngleMax = 18.0f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightColor = new Color("#a4bed8"),
            AmbientLightSkyContribution = 0.48f,
            AmbientLightEnergy = 0.65f,
            TonemapMode = Godot.Environment.ToneMapper.Aces,
            TonemapExposure = 1.0f,
            SsaoEnabled = true,
            SsaoRadius = 0.4f,
            SsaoIntensity = 1.5f,
            FogEnabled = true,
            FogLightColor = new Color("#b8cbd0"),
            FogMode = Godot.Environment.FogModeEnum.Depth,
            FogDepthBegin = 80.0f,
            FogDepthEnd = 320.0f,
            FogDepthCurve = 1.2f,
        };
        _environment = env;
        AddChild(new WorldEnvironment { Environment = env });

        _sun = new DirectionalLight3D
        {
            Rotation = new Vector3(Mathf.DegToRad(-40), Mathf.DegToRad(-35), 0),
            LightColor = new Color("#ffe4bd"),
            LightEnergy = 1.8f,
            ShadowEnabled = true,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
            DirectionalShadowMaxDistance = 110.0f,
            ShadowBias = 0.035f,
            ShadowNormalBias = 0.7f,
        };
        AddChild(_sun);
    }

    private void CreateCamera()
    {
        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = _zoom,
            Near = 0.05f,
            Far = 600.0f,
            Current = true,
        };
        AddChild(_camera);
    }

    private void CreateTerrain(WorldView world)
    {
        _terrainMaterial = new ShaderMaterial
        {
            Shader = _look.TerrainShader,
        };
        _waterMaterial = new ShaderMaterial
        {
            Shader = _look.WaterShader,
        };
        CreateStateTexture(world.Width, world.Height);
        _terrainMaterial.SetShaderParameter("tile_state", _stateTexture!);
        _waterMaterial.SetShaderParameter("tile_state", _stateTexture!);

        ArrayMesh ground = BuildTerrainMesh(world);
        _terrainNode = new MeshInstance3D { Name = "HexTerrain", Mesh = ground, MaterialOverride = _terrainMaterial };
        _terrainNode.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        AddChild(_terrainNode);

        ArrayMesh water = BuildWaterMesh(world);
        _waterNode = new MeshInstance3D { Name = "WaterAndLakes", Mesh = water, MaterialOverride = _waterMaterial };
        _waterNode.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_waterNode);

        ArrayMesh rivers = BuildRiverMesh(world);
        _riverNode = new MeshInstance3D
        {
            Name = "Rivers",
            Mesh = rivers,
            MaterialOverride = _waterMaterial,
        };
        _riverNode.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_riverNode);
        CreateTerrainBorderNode();
    }

    private void CreateStateTexture(int width, int height)
    {
        _stateImage = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        byte[] data = new byte[width * height * 4];
        _stateImage.SetData(width, height, false, Image.Format.Rgba8, data);
        _stateTexture = ImageTexture.CreateFromImage(_stateImage);
    }

    private ArrayMesh BuildTerrainMesh(WorldView world)
    {
        var data = new MeshData();
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            int x = tile % world.Width;
            int y = tile / world.Width;
            Vector3 center = TileCenter(x, y);
            float top = _terrainHeights[tile];
            byte biome = world.Biome[tile];
            Color baseColor = _look.TerrainColor(biome);
            float variation = 0.955f + ((x * 13 + y * 29 + tile * 7) & 7) * 0.012f;
            baseColor = new Color(baseColor.R * variation, baseColor.G * variation, baseColor.B * variation, 1).SrgbToLinear();
            Vector2 stateUv = TileUv(x, y, world.Width, world.Height);

            for (int edge = 0; edge < 6; edge++)
            {
                Vector2 a2 = HexCorner(edge) * HexRadius;
                Vector2 b2 = HexCorner((edge + 1) % 6) * HexRadius;
                Vector3 a = center + new Vector3(a2.X, 0, a2.Y);
                Vector3 b = center + new Vector3(b2.X, 0, b2.Y);
                Color ca = Shade(baseColor, 0.96f + edge * 0.008f);
                Color cb = Shade(baseColor, 1.02f - edge * 0.006f);
                data.Triangle(center with { Y = top }, a with { Y = top }, b with { Y = top },
                    baseColor, ca, cb, stateUv, stateUv, stateUv, true);
            }

            HexLayout.Neighbors(tile, world.Width, world.Height, neighbors);
            for (int edge = 0; edge < 6; edge++)
            {
                int next = neighbors[edge];
                float lower = next >= 0 ? _terrainHeights[next] : -0.85f;
                if (top - lower < 0.075f) continue;
                Vector2 a2 = HexCorner(edge) * HexRadius;
                Vector2 b2 = HexCorner((edge + 1) % 6) * HexRadius;
                Vector3 highA = center + new Vector3(a2.X, top, a2.Y);
                Vector3 highB = center + new Vector3(b2.X, top, b2.Y);
                Vector3 lowA = highA with { Y = lower };
                Vector3 lowB = highB with { Y = lower };
                Color cliff = _look.CliffColor(biome).SrgbToLinear();
                data.Triangle(highA, lowA, lowB, Shade(cliff, 1.0f), Shade(cliff, 0.77f), Shade(cliff, 0.82f),
                    stateUv, stateUv, stateUv, false);
                data.Triangle(highA, lowB, highB, Shade(cliff, 1.0f), Shade(cliff, 0.82f), Shade(cliff, 0.94f),
                    stateUv, stateUv, stateUv, false);
            }
        }
        return data.ToMesh();
    }

    private ArrayMesh BuildWaterMesh(WorldView world)
    {
        var data = new MeshData();
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            if (world.Biome[tile] > (byte)Biome.Coast) continue;
            int x = tile % world.Width;
            int y = tile / world.Width;
            Vector3 center = TileCenter(x, y);
            Vector2 uv = TileUv(x, y, world.Width, world.Height);
            const float waterHeight = -0.035f;
            Color water = Colors.White;
            for (int edge = 0; edge < 6; edge++)
            {
                Vector2 a2 = HexCorner(edge) * HexRadius;
                Vector2 b2 = HexCorner((edge + 1) % 6) * HexRadius;
                data.Triangle(center with { Y = waterHeight },
                    (center + new Vector3(a2.X, waterHeight, a2.Y)),
                    (center + new Vector3(b2.X, waterHeight, b2.Y)), water, water, water, uv, uv, uv, true);
            }
        }
        return data.ToMesh();
    }

    private ArrayMesh BuildRiverMesh(WorldView world)
    {
        var data = new MeshData();
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            if (!world.HasRiver(tile)) continue;
            int x = tile % world.Width;
            int y = tile / world.Width;
            Vector3 start = TileCenter(x, y);
            start.Y = Math.Max(_terrainHeights[tile], 0.02f) + 0.016f;
            HexLayout.Neighbors(tile, world.Width, world.Height, neighbors);
            int links = 0;
            for (int edge = 0; edge < 6; edge++)
            {
                int next = neighbors[edge];
                if (next < 0 || next <= tile || !world.HasRiver(next)) continue;
                links++;
                Vector3 end = TileCenter(next % world.Width, next / world.Width);
                end.Y = Math.Max(_terrainHeights[next], 0.02f) + 0.016f;
                AddRibbon(data, start, end, 0.075f, new Color("#4d9aa4").SrgbToLinear(),
                    TileUv(x, y, world.Width, world.Height), TileUv(next % world.Width, next / world.Width, world.Width, world.Height));
            }
            if (links == 0)
            {
                Vector3 end = start + new Vector3(0.18f, 0, 0.09f);
                AddRibbon(data, start, end, 0.07f, new Color("#4d9aa4").SrgbToLinear(),
                    TileUv(x, y, world.Width, world.Height), TileUv(x, y, world.Width, world.Height));
            }
        }
        return data.ToMesh();
    }

    private void CreateActors()
    {
        _figureBodyNode = CreateMultiMeshNode(_look.CreateFigureBody(), "People", _look.UnshadedVertexMaterial());
        _figureHeadNode = CreateMultiMeshNode(_look.CreateSphere(), "Heads", _look.UnshadedVertexMaterial());
        _figureCarryNode = CreateMultiMeshNode(_look.CreateBox(new Vector3(0.08f, 0.08f, 0.08f)), "Carry", _look.UnshadedVertexMaterial());
        _herdNode = CreateMultiMeshNode(_look.CreateSphere(), "WildHerds", _look.LitVertexMaterial());
        _campNode = CreateMultiMeshNode(_look.CreateStructure(1), "SeasonalCamps", _look.LitVertexMaterial());
        _beaconNode = CreateMultiMeshNode(_look.CreateStructure(13), "Beacons", _look.LitVertexMaterial());
        _bannerPoleNode = CreateMultiMeshNode(_look.CreateCylinder(0.018f, 0.018f, 0.62f, 6), "SettlementBannerPoles", _look.LitVertexMaterial());
        _bannerFlagNode = CreateMultiMeshNode(_look.CreateBox(new Vector3(0.24f, 0.15f, 0.025f)), "SettlementBanners", _look.UnshadedVertexMaterial());
        _orchardCanopyNode = CreateMultiMeshNode(_look.CreateSphere(), "OrchardCanopies", _look.LitVertexMaterial());
        _resourceNode = CreateMultiMeshNode(_look.CreateSphere(), "KnownResourceDeposits", _look.LitVertexMaterial());
        _resourceNode.Visible = false;

        int kinds = MatchFrame.StructureKinds;
        _structureNodes = new MultiMeshInstance3D?[kinds];
        _structureMeshes = new MultiMesh[kinds];
        _structureModels = new Mesh[kinds];
        for (int kind = 0; kind < kinds; kind++)
        {
            _structureModels[kind] = _look.CreateStructure(kind);
            _structureNodes[kind] = CreateMultiMeshNode(_structureModels[kind], $"Structure_{kind}", _look.LitVertexMaterial());
            _structureMeshes[kind] = _structureNodes[kind]!.Multimesh!;
        }
    }

    private void ConfigureBenchmarkCase()
    {
        if (_session is null || _current is null || !_benchmarkMatrix || _benchmarkOverlays.Length == 0) return;
        int zoom = _benchmarkCaseIndex / _benchmarkOverlays.Length;
        _benchmarkOverlay = _benchmarkOverlays[_benchmarkCaseIndex % _benchmarkOverlays.Length];
        _mapOverlay = _benchmarkOverlay switch { "claims" => 1, "food" => 2, "methods" => 3, _ => 0 };
        _resourcesVisible = _benchmarkOverlay == "resources";
        _visualRenderer?.Call("set_overlay", _mapOverlay);
        RefreshOverlayState();
        UpdateKnownResources(_current);
        SetZoom(zoom switch { 0 => 5.0f, 1 => 12.0f, _ => 40.0f });
        _benchmarkStartTick = _session.Tick;
        _benchmarkFrames = 0;
        _benchmarkWindowFrames = 0;
        _benchmarkFrameUpdateMs = 0;
        _benchmarkMaxFrameUpdateMs = 0;
        _benchmarkFrameUpdates = 0;
        _benchmarkMinWindowFps = double.MaxValue;
        _benchmarkWatch.Reset();
        _benchmarkWindowWatch.Reset();
        _benchmarkWarmupRemaining = 1.0;
    }

    private void ApplyNewFrame(MatchFrame frame)
    {
        if (_visualRenderer is not null && _session is not null)
        {
            long started = Stopwatch.GetTimestamp();
            var payload = BuildFramePayload(frame, _session.WorldView, includeWorld: false);
            double payloadMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            _visualRenderer.Call("update_frame", payload);
            if (OS.GetCmdlineUserArgs().Contains("--perf-debug"))
                GD.Print($"M7A_FRAME_HOST payload={payloadMs:F2}ms figures={frame.FigureCount} herds={frame.HerdCount}");
        }
        UpdateKnownResources(frame);
        ProcessEndTimeline(frame);
        long hudRefreshStarted = Stopwatch.GetTimestamp();
        RefreshHud(frame);
        _hudRefreshMs = (Stopwatch.GetTimestamp() - hudRefreshStarted) * 1000.0 / Stopwatch.Frequency;
        if (_benchmarkSeconds > 0) _benchmarkHudMs = Math.Max(_benchmarkHudMs, _hudRefreshMs);
    }

    private void RebuildVisualRenderer(MatchFrame frame, SimulationSession session)
    {
        if (_visualRenderer is not null)
        {
            RemoveChild(_visualRenderer);
            _visualRenderer.QueueFree();
        }
        string rendererPath = ProjectSettings.GetSetting(
            "civsim/renderer_script", "res://Renderer/LowPoly3DView.gd").AsString();
        var rendererScript = GD.Load<GDScript>(rendererPath)
            ?? throw new InvalidOperationException($"Could not load renderer script '{rendererPath}'.");
        _visualRenderer = rendererScript.New().AsGodotObject() as Node3D
            ?? throw new InvalidOperationException("Could not instantiate the M7a low-poly renderer.");
        AddChild(_visualRenderer);
        _visualRenderer.Call("setup_frame", BuildFramePayload(frame, session.WorldView, includeWorld: true));
        _camera = _visualRenderer.GetNode<Camera3D>("Camera3D");
    }

    private void UpdateTileState(MatchFrame frame)
    {
        if (_stateImage is null || _stateTexture is null) return;
        byte[] data = new byte[frame.Width * frame.Height * 4];
        for (int tile = 0; tile < frame.Width * frame.Height; tile++)
        {
            int offset = tile * 4;
            data[offset] = (byte)Math.Clamp(frame.Owner[tile] + 1, 0, 255);
            data[offset + 1] = frame.Claim[tile];
            data[offset + 2] = (byte)(frame.Visibility[tile] * 127);
            data[offset + 3] = 255;
        }
        _stateImage.SetData(frame.Width, frame.Height, false, Image.Format.Rgba8, data);
        _stateTexture.Update(_stateImage);
    }

    private void UpdateKnownResources(MatchFrame frame)
    {
        if (_resourceNode?.Multimesh is null || _session is null) return;
        _resourceNode.Visible = _resourcesVisible;
        var transforms = new List<Transform3D>();
        var colors = new List<Color>();
        if (_resourcesVisible)
        {
            WorldView world = _session.WorldView;
            for (int tile = 0; tile < world.TileCount; tile++)
            {
                if (frame.KnownDeposits[tile] == 0) continue;
                byte raw = world.Deposit[tile];
                if (raw == (byte)DepositKind.None) continue;
                Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
                float size = 0.08f + world.DepositAmount[tile] * 0.012f;
                transforms.Add(TransformAt(center + new Vector3(0, _terrainHeights[tile] + 0.11f, 0), tile * 0.37f,
                    new Vector3(size, size, size)));
                colors.Add(DepositColor((DepositKind)raw).SrgbToLinear());
            }
        }
        SetInstances(_resourceNode.Multimesh, transforms, colors);
    }

    private static Color DepositColor(DepositKind kind) => kind switch
    {
        DepositKind.Stone => new Color("#a5a39c"),
        DepositKind.Flint => new Color("#626a72"),
        DepositKind.Clay => new Color("#a86145"),
        DepositKind.Ochre => new Color("#cf8738"),
        DepositKind.NativeCopper or DepositKind.CopperOre or DepositKind.ArsenicalCopper => new Color("#b56e4a"),
        DepositKind.Tin or DepositKind.Silver => new Color("#c4c7c0"),
        DepositKind.Gold => new Color("#e4c24c"),
        DepositKind.Iron => new Color("#696d72"),
        DepositKind.Salt => new Color("#ece6d3"),
        DepositKind.Coal or DepositKind.Oil => new Color("#383936"),
        DepositKind.Saltpeter or DepositKind.Sulfur => new Color("#cfb34e"),
        DepositKind.Bauxite or DepositKind.RareEarths or DepositKind.Lithium => new Color("#8496b0"),
        DepositKind.SilkMoths => new Color("#bd8bb1"),
        DepositKind.Sugarcane => new Color("#72a54e"),
        _ => new Color("#9a9a92"),
    };

    private void UpdateBorders(MatchFrame frame)
    {
        if (_borderNode is null) return;
        var data = new MeshData();
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < frame.Width * frame.Height; tile++)
        {
            int owner = frame.Owner[tile];
            if (owner < 0 || frame.Visibility[tile] == (byte)TileVisibility.Unknown) continue;
            int x = tile % frame.Width;
            int y = tile / frame.Width;
            Vector3 center = TileCenter(x, y);
            float top = _terrainHeights[tile] + 0.016f;
            HexLayout.Neighbors(tile, frame.Width, frame.Height, neighbors);
            for (int edge = 0; edge < 6; edge++)
            {
                int next = neighbors[edge];
                int other = next >= 0 ? frame.Owner[next] : -1;
                if (other == owner && next >= 0) continue;
                if (next >= 0 && frame.Visibility[next] == (byte)TileVisibility.Unknown) continue;
                Vector2 center2 = new(center.X, center.Z);
                Vector2 edgeA = center2 + HexCorner(edge) * HexRadius;
                Vector2 edgeB = center2 + HexCorner((edge + 1) % 6) * HexRadius;
                Color civ = CivColor(owner).SrgbToLinear();
                AddRibbon(data, new Vector3(edgeA.X, top, edgeA.Y), new Vector3(edgeB.X, top, edgeB.Y), 0.042f,
                    new Color(0.06f, 0.07f, 0.07f), frame.Width, frame.Height);
                AddRibbon(data, new Vector3(edgeA.X, top + 0.002f, edgeA.Y), new Vector3(edgeB.X, top + 0.002f, edgeB.Y), 0.022f,
                    civ, frame.Width, frame.Height);
            }
        }
        _borderNode.Mesh = data.ToMesh();
    }

    private void UpdateStructures(MatchFrame frame)
    {
        int signature = StructureDigest(frame);
        if (signature == _structureSignature) return;
        _structureSignature = signature;
        int kinds = MatchFrame.StructureKinds;
        var transforms = new List<Transform3D>[kinds];
        var colors = new List<Color>[kinds];
        for (int kind = 0; kind < kinds; kind++)
        {
            transforms[kind] = new List<Transform3D>();
            colors[kind] = new List<Color>();
        }
        var poles = new List<Transform3D>();
        var poleColors = new List<Color>();
        var flags = new List<Transform3D>();
        var flagColors = new List<Color>();
        var camps = new List<Transform3D>();
        var campColors = new List<Color>();
        var beacons = new List<Transform3D>();
        var beaconColors = new List<Color>();
        var orchardCanopies = new List<Transform3D>();
        var orchardColors = new List<Color>();

        for (int s = 0; s < frame.SettlementCount; s++)
        {
            if ((frame.SettlementFlags[s] & (ushort)SettlementViewFlags.Remembered) != 0) continue;
            int tile = frame.SettlementTile[s];
            int civ = frame.SettlementCiv[s];
            Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
            float y = _terrainHeights[tile] + 0.035f;
            Color tint = CivColor(civ).SrgbToLinear();
            float seed = (tile % 29) * 0.11f;
            int slot = 0;
            for (int kind = 0; kind < kinds; kind++)
            {
                int count = frame.SettlementStructures[s * kinds + kind];
                if (count <= 0) continue;
                for (int i = 0; i < Math.Min(count, 24); i++)
                {
                    float angle = seed + slot * 2.3999632f;
                    float radius = slot == 0 && kind == 0 ? 0 : Math.Min(0.46f, 0.115f * Mathf.Sqrt(slot + 0.7f));
                    float sx = center.X + Mathf.Cos(angle) * radius;
                    float sz = center.Z + Mathf.Sin(angle) * radius;
                    int placedTile = HexLayout.NearestTile((int)(sx * MapUnit), (int)(sz / RowSpacing * MapUnit), frame.Width, frame.Height);
                    float sy = _terrainHeights[placedTile >= 0 ? placedTile : tile] + 0.025f;
                    float scale = kind switch
                    {
                        0 => 0.50f, 1 or 2 => 0.72f, 3 or 4 or 5 or 6 => 0.82f,
                        12 => 0.92f, 13 => 1.12f, 15 => 0.92f, 16 => 0.86f, 17 => 1.0f,
                        _ => 0.64f,
                    };
                    var pos = new Vector3(sx, sy + (kind == 17 ? 0.16f : 0), sz);
                    transforms[kind].Add(TransformAt(pos, angle, new Vector3(scale, scale, scale)));
                    colors[kind].Add(_look.StructureTint(kind, tint));
                    if (kind == 17)
                    {
                        orchardCanopies.Add(TransformAt(new Vector3(sx, sy + 0.47f, sz), angle,
                            new Vector3(0.18f, 0.22f, 0.18f)));
                        orchardColors.Add(new Color("#638541").SrgbToLinear());
                    }
                    slot++;
                }
            }
            poles.Add(TransformAt(new Vector3(center.X + 0.10f, y + 0.31f, center.Z - 0.08f), 0,
                new Vector3(1, 1, 1)));
            poleColors.Add(new Color("#806342").SrgbToLinear());
            flags.Add(TransformAt(new Vector3(center.X + 0.10f, y + 0.55f, center.Z - 0.08f), 0,
                new Vector3(1, 1, 1)));
            flagColors.Add(tint);
        }

        for (int i = 0; i < frame.CampCount; i++)
        {
            int tile = frame.CampTile[i];
            Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
            camps.Add(TransformAt(new Vector3(center.X + 0.08f, _terrainHeights[tile] + 0.03f, center.Z - 0.05f),
                tile * 0.01f, new Vector3(0.76f, 0.72f, 0.76f)));
            campColors.Add(CivColor(frame.CampCiv[i]).SrgbToLinear());
        }
        for (int i = 0; i < frame.BeaconCount; i++)
        {
            int tile = frame.BeaconTile[i];
            Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
            beacons.Add(TransformAt(new Vector3(center.X, _terrainHeights[tile] + 0.22f, center.Z), 0,
                new Vector3(0.36f, 1.35f, 0.36f)));
            beaconColors.Add(CivColor(frame.BeaconCiv[i]).SrgbToLinear());
        }

        for (int kind = 0; kind < kinds; kind++) SetInstances(_structureMeshes[kind], transforms[kind], colors[kind]);
        SetInstances(_bannerPoleNode?.Multimesh, poles, poleColors);
        SetInstances(_bannerFlagNode?.Multimesh, flags, flagColors);
        SetInstances(_campNode?.Multimesh, camps, campColors);
        SetInstances(_beaconNode?.Multimesh, beacons, beaconColors);
        SetInstances(_orchardCanopyNode?.Multimesh, orchardCanopies, orchardColors);
    }

    private static int StructureDigest(MatchFrame frame)
    {
        unchecked
        {
            int hash = 17;
            int kinds = MatchFrame.StructureKinds;
            for (int s = 0; s < frame.SettlementCount; s++)
            {
                hash = hash * 31 + frame.SettlementTile[s];
                hash = hash * 31 + frame.SettlementCiv[s];
                hash = hash * 31 + frame.SettlementFlags[s];
                for (int kind = 0; kind < kinds; kind++) hash = hash * 31 + frame.SettlementStructures[s * kinds + kind];
            }
            for (int i = 0; i < frame.CampCount; i++) hash = hash * 31 + frame.CampTile[i] * 7 + frame.CampCiv[i];
            for (int i = 0; i < frame.BeaconCount; i++) hash = hash * 31 + frame.BeaconTile[i] * 7 + frame.BeaconCiv[i];
            return hash;
        }
    }

    private void UpdateActors(float alpha)
    {
        if (_visualRenderer is not null) return;
        if (_current is null || _session is null) return;
        MatchFrame current = _current;
        MatchFrame? previous = _previous;
        IdLink links = _session.Frames.Figures;
        int count = current.FigureCount;
        List<Transform3D> bodyTransforms = _bodyTransforms, headTransforms = _headTransforms, carryTransforms = _carryTransforms;
        List<Color> bodyColors = _bodyColors, headColors = _headColors, carryColors = _carryColors;
        bodyTransforms.Clear(); headTransforms.Clear(); carryTransforms.Clear();
        bodyColors.Clear(); headColors.Clear(); carryColors.Clear();
        for (int i = 0; i < count; i++)
        {
            float x = current.FigureX[i] / MapUnit;
            float z = current.FigureY[i] / MapUnit * RowSpacing;
            int prev = previous is not null && i < links.Count ? links.PreviousIndex(i) : -1;
            if (prev >= 0 && previous is not null)
            {
                float px = previous.FigureX[prev] / MapUnit;
                float pz = previous.FigureY[prev] / MapUnit * RowSpacing;
                x = Mathf.Lerp(px, x, alpha);
                z = Mathf.Lerp(pz, z, alpha);
            }
            int tile = Math.Clamp(current.FigureTile[i], 0, _terrainHeights.Length - 1);
            float y = _terrainHeights[tile];
            float scale = current.FigureAge[i] switch { 0 => 0.62f, 2 => 0.92f, _ => 1.0f };
            long id = current.FigureId[i];
            float angle = StableAngle(id);
            if (prev >= 0 && previous is not null)
            {
                float dx = current.FigureX[i] - previous.FigureX[prev];
                float dz = current.FigureY[i] - previous.FigureY[prev];
                if (Math.Abs(dx) + Math.Abs(dz) > 1) angle = Mathf.Atan2(dx, dz);
            }
            // People share tile-centre positions in the frame. Give each a stable spot
            // within that tile so hearth crowds remain individuals at the close zoom.
            float scatterRadius = 0.31f * Mathf.Sqrt((float)((ulong)id & 0xFFFF) / 65535.0f);
            x += Mathf.Cos(StableAngle(id ^ 0x5bd1e995L)) * scatterRadius;
            z += Mathf.Sin(StableAngle(id ^ 0x27d4eb2fL)) * scatterRadius;
            var ground = new Vector3(x, y, z);
            bodyTransforms.Add(TransformAt(ground + new Vector3(0, 0.16f * scale, 0), angle,
                new Vector3(scale, scale, scale)));
            headTransforms.Add(TransformAt(ground + new Vector3(0, 0.34f * scale, 0), angle,
                new Vector3(0.075f * scale, 0.075f * scale, 0.075f * scale)));
            Color civ = (current.FigureFlags[i] & (ushort)FigureFlags.FreeFolk) != 0
                ? new Color("#88877f").SrgbToLinear()
                : CivColor(current.FigureCiv[i]).SrgbToLinear();
            bodyColors.Add(civ);
            headColors.Add(new Color("#c5a078").SrgbToLinear());
            int carry = current.FigureCarry[i];
            float markerScale = carry == 0 ? 0.001f : 0.065f;
            Color carryColor = carry switch
            {
                1 or 6 => new Color("#d6ae43"), 2 => new Color("#a34431"), 3 => new Color("#4d9aa4"),
                4 => new Color("#805333"), 5 => new Color("#74716a"), 7 or 8 => new Color("#bf8c58"),
                _ => new Color("#afafaf"),
            };
            carryTransforms.Add(TransformAt(ground + new Vector3(0.09f * scale, 0.20f * scale, 0.02f), angle,
                new Vector3(markerScale, markerScale, markerScale)));
            carryColors.Add(carryColor.SrgbToLinear());
        }
        SetInstances(_figureBodyNode?.Multimesh, bodyTransforms, bodyColors);
        SetInstances(_figureHeadNode?.Multimesh, headTransforms, headColors);
        SetInstances(_figureCarryNode?.Multimesh, carryTransforms, carryColors);

        List<Transform3D> animalTransforms = _animalTransforms;
        List<Color> animalColors = _animalColors;
        animalTransforms.Clear();
        animalColors.Clear();
        for (int i = 0; i < current.HerdCount; i++)
        {
            float baseX = current.HerdX[i] / MapUnit;
            float baseZ = current.HerdY[i] / MapUnit * RowSpacing;
            int tile = Math.Clamp(current.HerdTile[i], 0, _terrainHeights.Length - 1);
            float y = _terrainHeights[tile] + 0.04f;
            int species = current.HerdSpecies[i] % SpeciesColors.Length;
            int animals = Math.Clamp((current.HerdAnimals[i] + 3) / 5, 1, 6);
            float heading = StableAngle(current.HerdId[i] * 13L + 5);
            for (int j = 0; j < animals; j++)
            {
                float angle = heading + j * 2.3999632f;
                float radius = 0.06f * Mathf.Sqrt(j + 0.4f);
                float size = species is 1 or 3 or 8 ? 1.2f : species == 9 ? 0.72f : 1.0f;
                var pos = new Vector3(baseX + Mathf.Cos(angle) * radius, y, baseZ + Mathf.Sin(angle) * radius);
                animalTransforms.Add(TransformAt(pos + new Vector3(0, 0.1f * size, 0), angle,
                    new Vector3(0.19f * size, 0.12f * size, 0.28f * size)));
                animalColors.Add(SpeciesColors[species].SrgbToLinear());
            }
        }
        for (int i = 0; i < current.PredatorCount; i++)
        {
            int tile = Math.Clamp(current.PredatorTile[i], 0, _terrainHeights.Length - 1);
            Vector3 center = TileCenter(tile % current.Width, tile / current.Width);
            animalTransforms.Add(TransformAt(center + new Vector3(0, _terrainHeights[tile] + 0.11f, 0), i * 0.53f,
                new Vector3(0.23f, 0.14f, 0.32f)));
            animalColors.Add(new Color("#45362c").SrgbToLinear());
        }
        SetInstances(_herdNode?.Multimesh, animalTransforms, animalColors);
    }

    private float FrameAlpha(double renderTick)
    {
        if (_current is null || _previous is null || _current.Tick <= _previous.Tick) return 1.0f;
        return Mathf.Clamp((float)((renderTick - _previous.Tick) / (_current.Tick - _previous.Tick)), 0, 1);
    }

    private void ApplySeasonLight(int season)
    {
        if (_visualRenderer is not null)
        {
            _visualRenderer.Call("set_season", season);
            return;
        }
        if (_sun is null || _environment is null) return;
        bool winter = season == 3;
        _sun.LightColor = winter ? new Color("#e4ebf4") : new Color("#ffe6bf");
        _sun.LightEnergy = winter ? 1.45f : 1.8f;
        _environment.AmbientLightEnergy = winter ? 0.78f : 0.65f;
        _environment.AmbientLightColor = winter ? new Color("#a6bed4") : new Color("#a4bed8");
    }

    private void CreateTerrainBorderNode()
    {
        if (_borderNode is not null) return;
        _borderNode = new MeshInstance3D
        {
            Name = "TerritoryBorders",
            MaterialOverride = _look.UnshadedVertexMaterial(),
        };
        _borderNode.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_borderNode);
    }


    private static float[] MakeHeights(WorldView world)
    {
        var heights = new float[world.TileCount];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            byte biome = world.Biome[tile];
            float elevation = world.Elevation[tile];
            if (biome == (byte)Biome.Ocean) heights[tile] = -0.7f;
            else if (biome == (byte)Biome.Coast) heights[tile] = -0.14f;
            else if (biome == (byte)Biome.Mountain) heights[tile] = Mathf.Snapped(1.0f + (elevation - 183.0f) / 42.0f * 0.3f, 0.1f);
            else if (biome == (byte)Biome.Peak) heights[tile] = Mathf.Snapped(1.3f + (elevation - 226.0f) / 14.0f * 0.2f, 0.1f);
            else heights[tile] = Math.Max(0.08f, Mathf.Snapped(0.08f + Mathf.Clamp((elevation - 70) / 112.0f, 0, 1) * 0.85f, 0.1f));
        }
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < world.TileCount; tile++)
        {
            if (world.Biome[tile] <= (byte)Biome.Coast || world.Biome[tile] >= (byte)Biome.Mountain) continue;
            HexLayout.Neighbors(tile, world.Width, world.Height, neighbors);
            for (int i = 0; i < 6; i++)
                if (neighbors[i] >= 0 && world.Biome[neighbors[i]] <= (byte)Biome.Coast)
                {
                    heights[tile] = Math.Min(heights[tile], 0.08f);
                    break;
                }
        }
        return heights;
    }

    private static Color CivColor(int civ) => civ < 0 ? new Color("#88877f") : CivColors[civ % CivColors.Length];

    private static Color Shade(Color color, float scale) => new(
        Math.Clamp(color.R * scale, 0, 1), Math.Clamp(color.G * scale, 0, 1), Math.Clamp(color.B * scale, 0, 1), color.A);

    private static IGameLook LoadLook()
    {
        string look = ProjectSettings.GetSetting("civsim/look", "low_poly_3d").AsString();
        return look switch
        {
            "low_poly_3d" => new LowPoly3DLook(),
            _ => throw new InvalidOperationException($"Unsupported civsim/look '{look}'."),
        };
    }

    private static Vector3 TileCenter(int x, int y) => new(x + 0.5f + ((y & 1) == 1 ? 0.5f : 0), 0, (y + 0.5f) * RowSpacing);

    private static Vector2 TileUv(int x, int y, int width, int height) => new((x + 0.5f) / width, (y + 0.5f) / height);

    private static Vector2 HexCorner(int i)
    {
        float angle = Mathf.DegToRad(60.0f * i - 30.0f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float StableAngle(long id)
    {
        unchecked
        {
            ulong value = (ulong)id;
            value ^= value >> 33;
            value *= 0xff51afd7ed558ccdUL;
            value ^= value >> 33;
            value *= 0xc4ceb9fe1a85ec53UL;
            value ^= value >> 33;
            return (float)(value & 0xFFFFFF) / 16777216.0f * Mathf.Tau;
        }
    }

    private static Transform3D TransformAt(Vector3 position, float angle, Vector3 scale)
        => new(new Basis(Vector3.Up, angle).Scaled(scale), position);

    private MultiMeshInstance3D CreateMultiMeshNode(Mesh mesh, string name, Material material)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = 0,
        };
        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = mm,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(node);
        return node;
    }

    private void SetInstances(MultiMesh? multimesh, List<Transform3D> transforms, List<Color> colors)
    {
        if (multimesh is null) return;
        int count = transforms.Count;
        if (multimesh.InstanceCount != count) multimesh.InstanceCount = count;
        if (count == 0) return;
        if (!_instanceBuffers.TryGetValue(multimesh, out float[]? packed) || packed.Length < count * 16)
        {
            packed = new float[count * 16];
            _instanceBuffers[multimesh] = packed;
        }
        for (int i = 0; i < count; i++)
        {
            Transform3D t = transforms[i];
            int offset = i * 16;
            packed[offset] = t.Basis.X.X;
            packed[offset + 1] = t.Basis.Y.X;
            packed[offset + 2] = t.Basis.Z.X;
            packed[offset + 3] = t.Origin.X;
            packed[offset + 4] = t.Basis.X.Y;
            packed[offset + 5] = t.Basis.Y.Y;
            packed[offset + 6] = t.Basis.Z.Y;
            packed[offset + 7] = t.Origin.Y;
            packed[offset + 8] = t.Basis.X.Z;
            packed[offset + 9] = t.Basis.Y.Z;
            packed[offset + 10] = t.Basis.Z.Z;
            packed[offset + 11] = t.Origin.Z;
            Color color = colors[i];
            packed[offset + 12] = color.R;
            packed[offset + 13] = color.G;
            packed[offset + 14] = color.B;
            packed[offset + 15] = color.A;
        }
        RenderingServer.MultimeshSetBuffer(multimesh.GetRid(), packed.AsSpan(0, count * 16));
    }

    private sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new(400_000);
        public readonly List<Vector3> Normals = new(400_000);
        public readonly List<Color> Colors = new(400_000);
        public readonly List<Vector2> Uvs = new(400_000);

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc,
            Vector2 ua, Vector2 ub, Vector2 uc, bool upward)
        {
            Vector3 normal = (b - a).Cross(c - a);
            if (normal.LengthSquared() < 0.0000001f) return;
            normal = normal.Normalized();
            if (upward && normal.Y < 0)
            {
                (b, c) = (c, b);
                (cb, cc) = (cc, cb);
                (ub, uc) = (uc, ub);
                normal = -normal;
            }
            Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc);
            Uvs.Add(ua); Uvs.Add(ub); Uvs.Add(uc);
        }

        public ArrayMesh ToMesh()
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = Normals.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = Colors.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = Uvs.ToArray();
            var mesh = new ArrayMesh();
            if (Vertices.Count > 0) mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }
    }

    private static void AddRibbon(MeshData data, Vector3 start, Vector3 end, float width, Color color, int mapWidth, int mapHeight)
    {
        Vector2 uv = new(0.5f / mapWidth, 0.5f / mapHeight);
        AddRibbon(data, start, end, width, color, uv, uv);
    }

    private static void AddRibbon(MeshData data, Vector3 start, Vector3 end, float width, Color color, Vector2 startUv, Vector2 endUv)
    {
        Vector2 direction = new Vector2(end.X - start.X, end.Z - start.Z);
        if (direction.LengthSquared() < 0.00001f) return;
        direction = direction.Normalized();
        Vector2 side = new(-direction.Y, direction.X);
        side *= width * 0.5f;
        Vector3 a = start + new Vector3(side.X, 0, side.Y);
        Vector3 b = start - new Vector3(side.X, 0, side.Y);
        Vector3 c = end - new Vector3(side.X, 0, side.Y);
        Vector3 d = end + new Vector3(side.X, 0, side.Y);
        data.Triangle(a, b, c, color, color, color, startUv, startUv, endUv, true);
        data.Triangle(a, c, d, color, color, color, startUv, endUv, endUv, true);
    }
}
