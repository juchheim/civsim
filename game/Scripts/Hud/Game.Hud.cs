using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The HUD shell: root control, scaling, zones, paused treatment, developer overlay. Layout follows docs/MVP/20 §3.</summary>
public partial class Game
{
    private const int TopBandHeight = 64;
    private const int FocusBarHeight = 105;

    private CanvasLayer? _hudLayer, _scrimLayer;
    private Control? _hudRoot;
    private readonly UiStack _ui = new();
    private UiSettings _uiSettings = UiSettings.Current;
    private bool _compact;
    private Vector2 _logicalSize = new(1600, 900);
    private ColorRect? _pauseScrim;
    private readonly ColorRect?[] _pauseFrame = new ColorRect?[4];
    private PanelContainer? _devOverlay;
    private Label? _devLabel;
    private bool _devVisible;
    private float _devTimer;
    private long _uiFrameStamp;
    private double _uiUpdateMs;
    private bool _hudReady;

    private int Margin => _compact ? UiTokens.Sp3 : UiTokens.Sp4;

    private void CreateHud(ulong seed)
    {
        _uiSettings = UiSettings.Load();
        if (ArgumentValue("--ui-scale=") is { } scaleArg && float.TryParse(scaleArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float forcedScale))
            _uiSettings.UiScale = Math.Clamp(forcedScale, 1.0f, 1.3f);
        if (HasArgument("--reduce-motion")) _uiSettings.ReduceMotion = true;
        if (_contentPack is not null && _catalog is not null) UiText.Init(_contentPack, _catalog);
        Godot.Theme theme = UiTheme.Build();
        GetWindow().Theme = theme;

        _scrimLayer = new CanvasLayer { Layer = 9 };
        AddChild(_scrimLayer);
        _pauseScrim = new ColorRect { Color = UiTokens.ScrimPause, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _pauseScrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _scrimLayer.AddChild(_pauseScrim);

        _hudLayer = new CanvasLayer { Layer = 10 };
        AddChild(_hudLayer);
        _hudRoot = new Control { Name = "Hud", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = theme };
        _hudRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hudLayer.AddChild(_hudRoot);

        // Paused frame: four thin accent bars on the viewport edge.
        for (int i = 0; i < 4; i++)
        {
            var bar = new ColorRect { Color = UiTokens.Accent, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            bar.SetAnchorsPreset(i switch { 0 => Control.LayoutPreset.TopWide, 1 => Control.LayoutPreset.BottomWide, 2 => Control.LayoutPreset.LeftWide, _ => Control.LayoutPreset.RightWide });
            switch (i)
            {
                case 0: bar.OffsetBottom = 3; break;
                case 1: bar.OffsetTop = -3; break;
                case 2: bar.OffsetRight = 3; break;
                default: bar.OffsetLeft = -3; break;
            }
            _hudRoot.AddChild(bar);
            _pauseFrame[i] = bar;
        }

        CreateRail();
        CreateTopBar();
        CreateFocusBar();
        CreateNotifications();
        CreateTileCard();
        CreateDiplomacyDrawer();
        CreateDecreePanel();
        CreateResearchWeb();
        CreatePeoplePanel();
        // The time controls stay usable above the research web's scrim (spec §3.4).
        foreach (Control? top in new Control?[] { _civCard, _timeCluster, _pausedPill, _topRight })
            if (top is not null) _hudRoot.MoveChild(top, _hudRoot.GetChildCount() - 1);
        // Transient notices float above the drawers; only modals cover them.
        foreach (Control? top in new Control?[] { _toastStack, _momentCard, _ageBanner, _coachCard, _hintPill, _tileCard })
            if (top is not null) _hudRoot.MoveChild(top, _hudRoot.GetChildCount() - 1);
        CreateModals(seed);
        CreateDevOverlay();

        DisplayServer.WindowSetMinSize(new Vector2I(1280, 720));
        GetWindow().SizeChanged += OnWindowResized;
        ApplyWindowScale();
        ApplyHudLayout();
        _hudReady = true;
        if (_current is not null) RefreshHud(_current);

        bool freshStart = IsFreshUnassignedMatch();
        if (freshStart && (!IsDevelopmentPresentationRun() || HasArgument("--opening-hold"))) BeginOpeningHold();
        if (ReadResearchWebArgument()) OpenResearchWeb(-1);
        if (HasArgument("--research-map")) { OpenResearchWeb(-1); SetResearchView(1); }
        ApplyUiOpenArgument();
        if (HasArgument("--paused")) _session?.SetPaused(true);
        UpdatePausedTreatment();
        StartScenarioIfRequested();
        _devVisible = HasArgument("--perf-debug");
        if (_devOverlay is not null) _devOverlay.Visible = _devVisible;
    }

    private static bool HasArgument(string name)
    {
        foreach (string arg in OS.GetCmdlineUserArgs()) if (string.Equals(arg, name, StringComparison.Ordinal)) return true;
        return false;
    }

    private static string? ArgumentValue(string prefix)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith(prefix, StringComparison.Ordinal)) return arg[prefix.Length..];
        return null;
    }

    private void ApplyUiOpenArgument()
    {
        string? open = ArgumentValue("--ui-open=");
        if (open is null) return;
        switch (open)
        {
            case "web": OpenResearchWeb(-1); break;
            case "map": OpenResearchWeb(-1); SetResearchView(1); break;
            case "inspector-almost": OpenResearchWeb(-1); SelectFirstAlmostReady(); break;
            case "replace": OpenResearchWeb(0); ShowReplaceRow(); break;
            case "diplomacy": ToggleDiplomacy(true); break;
            case "chronicle": ToggleChronicle(true); break;
            case "help": ToggleHelp(true); break;
            case "settings": OpenSettings(); break;
            case "menu": ToggleGameMenu(true); break;
            case "people": OpenPeoplePanel(); break;
            case "end": ShowEndScreen(); break;
            case "intro": ShowIntroModal(true); break;
        }
    }

    private void OnWindowResized()
    {
        ApplyWindowScale();
        ApplyHudLayout();
    }

    /// <summary>
    /// Scale floor: 14 design px must never render below ~12.6 px, whatever the window size (spec §1.9). The stretch mode already
    /// scales the canvas by window pixels / 1600, so the floor works on pixels; on a Retina Mac the default 1600×900 window is
    /// 1600×900 pixels, which keeps the same layout the game always had.
    /// </summary>
    private void ApplyWindowScale()
    {
        Window window = GetWindow();
        Vector2 size = window.Size;
        float apparent = Math.Min(size.X / 1600.0f, size.Y / 900.0f);
        float auto = Math.Max(1.0f, 0.9f / Math.Max(0.1f, apparent));
        window.ContentScaleFactor = auto * _uiSettings.UiScale;
        _logicalSize = GetViewport().GetVisibleRect().Size;
        if (HasArgument("--ui-debug")) GD.Print($"UIDEBUG window={size} auto={auto} logical={_logicalSize}");
    }

    private void ApplyHudLayout()
    {
        if (_hudRoot is null) return;
        _logicalSize = GetViewport().GetVisibleRect().Size;
        _compact = _logicalSize.X < 1440 || _logicalSize.Y < 820;
        LayoutTopBar();
        LayoutFocusBar();
        LayoutRail();
        LayoutDecreePanel();
        LayoutNotifications();
        LayoutDrawers();
        LayoutResearchWeb();
        LayoutPeoplePanel();
        LayoutModals();
        if (_devOverlay is not null)
        {
            _devOverlay.Position = new Vector2(Margin, Margin + TopBandHeight + 8);
        }
    }

    private void RefreshHud(MatchFrame frame)
    {
        if (!_hudReady) return;
        RefreshTopBar(frame);
        RefreshFocusBar(frame);
        RefreshRail(frame);
        RefreshResearchWeb(frame);
        RefreshPeoplePanel(frame);
        RefreshDiplomacy(frame);
        RefreshDecrees(frame);
        RefreshNotifications(frame);
        UpdatePausedTreatment();
    }

    /// <summary>Per-rendered-frame work that must be cheap: timers, tweens' companions, the hover card.</summary>
    private void TickHud(double delta)
    {
        if (!_hudReady) return;
        TickScenario((float)delta);
        TickNotifications((float)delta);
        TickDiplomacy((float)delta);
        TickFocusBar((float)delta);
        TickTileCard((float)delta);
        TickCamera((float)delta);
        TickPulses((float)delta);
        TickSkirmishes((float)delta);
        TickHint((float)delta);
        _devTimer += (float)delta;
        if (_devTimer >= 0.25f)
        {
            _devTimer = 0;
            UpdateDevOverlay();
            RefreshSeasonDial();
            RefreshEtas();
        }
    }

    private void UpdatePausedTreatment()
    {
        if (_session is null) return;
        bool paused = _session.Paused && !_session.MatchEnded;
        if (_pauseScrim is not null) UiKit.Fade(_pauseScrim, paused);
        foreach (ColorRect? bar in _pauseFrame)
            if (bar is not null) UiKit.SetVisible(bar, paused);
        UpdatePausedPill(paused);
        UpdatePauseButton();
    }

    private void CreateDevOverlay()
    {
        _devOverlay = UiKit.Panel("HudPanel", "DevOverlay", UiKit.MouseFilter.Ignore);
        _devOverlay.CustomMinimumSize = new Vector2(233, 0);
        _devOverlay.Visible = false;
        _devLabel = UiKit.Text("", "Num");
        _devOverlay.AddChild(_devLabel);
        _hudRoot!.AddChild(_devOverlay);
    }

    private void ToggleDevOverlay()
    {
        _devVisible = !_devVisible;
        if (_devOverlay is not null) _devOverlay.Visible = _devVisible;
        UpdateDevOverlay();
    }

    private void UpdateDevOverlay()
    {
        if (_devOverlay is null || _devLabel is null) return;
        _devOverlay.Visible = _devVisible;
        if (!_devVisible || _current is null || _session is null) return;
        _devLabel.Text = $"{Engine.GetFramesPerSecond()} fps\n" +
                         $"Figures {_current.FigureCount:N0} (all {_current.TotalFigures:N0})\n" +
                         $"Zoom {_zoom:0.0} · Z{ZoomLevel}\n" +
                         $"Tick {_session.Tick:N0} · frame #{_currentSequence}\n" +
                         $"Speed {_session.Speed:0.#}× · paused: {(_session.Paused ? "yes" : "no")}\n" +
                         $"UI tick {_uiUpdateMs:0.00} ms · refresh {_hudRefreshMs:0.00} ms";
    }

    private UiLayer RegisterLayer(string name, bool modal, Func<bool> close) => new(name, modal, close);

    /// <summary>Puts the player's attention on the first choice: time holds until the web closes after the first assignment (spec §4.3).</summary>
    private bool _openingHold;
    private bool _openingHoldAssignmentPending;

    private void BeginOpeningHold()
    {
        if (_session is null) return;
        _openingHold = true;
        _openingHoldAssignmentPending = false;
        _session.SetPaused(true);
    }

    private void EndOpeningHold()
    {
        if (!_openingHold) return;
        _openingHold = false;
        _openingHoldAssignmentPending = false;
        _session?.SetPaused(false);
    }

    private bool IsFreshUnassignedMatch() =>
        _session is { Tick: < 100, IsReplay: false } && _current is { FocusCiv: 0, FocusCouncil: false } &&
        _current.FocusSlotProject[0] < 0 && _current.FocusSlotProject[1] < 0 && _current.FocusSlotProject[2] < 0;

    private string YearText(MatchFrame frame) => $"Year {frame.Year + 1}";
}
