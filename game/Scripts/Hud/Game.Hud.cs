using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Core;
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

    // ---- the right dock (docs/MVP/ui-ux-pass2.md §1) ------------------------------------------------
    // Decrees, Diplomacy and the Chronicle share one column on the right: the same top edge (under the top bar), the same right
    // edge, and the same lowest edge (above the focus bar). Only one is open at a time. Two widths in a golden ratio: the wide
    // one (Diplomacy's list and detail) is at most 0.382 of the screen so the map keeps the major share; the narrow one
    // (lists and forms) is the wide one ÷ φ. The floors keep the controls usable in small or scaled windows.
    private const float Phi = 1.618034f;
    private float DockTop => Margin + TopBandHeight + UiTokens.Sp3;
    private float DockMaxHeight => Math.Max(233, _logicalSize.Y - DockTop - (Margin + FocusBarHeight + UiTokens.Sp3));
    private float DockWideWidth => MathF.Round(Math.Clamp(_logicalSize.X * (1 - 1 / Phi), 520, 610));
    private float DockNarrowWidth => MathF.Round(DockWideWidth / Phi);

    /// <summary>Width of whichever dock panel is open, or 0.</summary>
    private float OpenDockWidth => _diplomacyOpen ? DockWideWidth : _chronicleOpen || _decreeOpen ? DockNarrowWidth : 0;

    /// <summary>Places a dock panel: top-right, <paramref name="height"/> tall (0 lets it size to its content, growing down).</summary>
    private void PlaceDock(Control panel, float width, float height)
    {
        float top = DockTop;
        AnchorTo(panel, 1, 0, 1, 0, -Margin - width, top, -Margin, top + height, Control.GrowDirection.Begin, Control.GrowDirection.End);
        panel.CustomMinimumSize = new Vector2(width, height);
    }

    /// <summary>Opening one dock panel closes the others, so they never stack on top of each other.</summary>
    private void CloseOtherDocks(string keep)
    {
        // A panel opening under a resting cursor would otherwise leave the hover card floating on top of it.
        if (_pinnedTile < 0) HideTileCard();
        if (keep != "chronicle" && _chronicleOpen) ToggleChronicle(false);
        if (keep != "diplomacy" && _diplomacyOpen) ToggleDiplomacy(false);
        if (keep != "decrees" && _decreeOpen) ToggleDecrees(false);
    }

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
        ApplyResearchEraArgument();
        if (ReadResearchWebArgument()) OpenResearchWeb(-1);
        if (HasArgument("--research-map")) { OpenResearchWeb(-1); SetResearchView(1); }
        // Developer option: after the map has had a moment to fill in, zoom it out to show everything visible (for screenshots).
        if (HasArgument("--map-fit")) GetTree().CreateTimer(1.5).Timeout += () => _mapView?.FitVisible();
        ApplyUiOpenArgument();
        if (HasArgument("--paused")) _session?.SetPaused(true);
        UpdatePausedTreatment();
        StartScenarioIfRequested();
        _devVisible = HasArgument("--perf-debug");
        if (_devOverlay is not null) _devOverlay.Visible = _devVisible;
    }

    /// <summary>
    /// Developer option <c>--research-era=N</c> (0 to 9): the human civilization is given every project of the ages before N, as if it had played them, so a later
    /// age can be looked at without playing there. It uses the sim's own project-grant command, so it is recorded like any command; saves and replays of such a match
    /// are for inspection, not for play.
    /// </summary>
    private void ApplyResearchEraArgument()
    {
        if (ArgumentValue("--research-era=") is not { } text || !int.TryParse(text, out int era) || _session is null || _catalog is null) return;
        int granted = 0;
        foreach (ProjectDef def in _catalog.Projects)
        {
            if (!def.Numeric || def.Era >= era) continue;
            _session.Queue(new Command(_session.Tick, 0, CommandKind.CompleteProject, def.Index, 0, 0));
            granted++;
        }
        GD.Print($"--research-era={era}: granted {granted} projects to the human civilization.");
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
        if (ArgumentValue("--people-scope=") is { } scope && int.TryParse(scope, out int scoped)) _peopleScopeSelected = scoped;
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
            case "people-stores": OpenPeoplePanel(PeopleTab.Stores, int.MinValue); break;
            case "people-work": OpenPeoplePanel(PeopleTab.Work, int.MinValue); break;
            case "people-research": OpenPeoplePanel(PeopleTab.Research, int.MinValue); break;
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

    /// <summary>Queues a player command and refreshes the affected paused UI from its pending-command projection.</summary>
    private void QueueCommand(Command command)
    {
        if (_session is null) return;
        _session.Queue(command);
        if (!_session.Paused || _current is null) return;

        UpdatePausedTreatment();
        switch (command.Kind)
        {
            case CommandKind.DeclareWar:
            case CommandKind.MakePeace:
            case CommandKind.FormAlliance:
            case CommandKind.FormLeague:
            case CommandKind.ImposeTribute:
            case CommandKind.SendGift:
            case CommandKind.SendBarter:
            case CommandKind.LaunchRaid:
            case CommandKind.LaunchRaidAgainst:
            case CommandKind.LaunchAssault:
            case CommandKind.RecallParty:
            case CommandKind.Blockade:
            case CommandKind.SetEmbargo:
                if (_diplomacyOpen) RefreshDiplomacy(_current);
                break;
            case CommandKind.SetScoutPolicy:
                if (_peopleOpen) RefreshPeopleScoutControls(_current);
                if (_tileCard is { Visible: true } && _pinnedTile >= 0) RefreshTileCardNow(_hoverScreen);
                break;
            case CommandKind.SetCouncil:
                RefreshFocusBar(_current);
                if (_researchWebVisible) RefreshResearchWeb(_current);
                break;
            case CommandKind.QueueProject:
            case CommandKind.DequeueProject:
                RefreshFocusBar(_current);
                if (_researchWebVisible) RefreshResearchWeb(_current);
                RefreshQueuePopover();
                break;
            case CommandKind.IssueDecree:
                if (_decreeOpen) RefreshDecrees(_current);
                break;
        }
    }

    /// <summary>Per-rendered-frame work that must be cheap: timers, tweens' companions, the hover card.</summary>
    private void TickHud(double delta)
    {
        if (!_hudReady) return;
        TickScenario((float)delta);
        TickNotifications((float)delta);
        TickDiplomacy((float)delta);
        TickFocusBar((float)delta);
        if (_decreeOpen) FitDecreePanel();
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
        UpdatePauseLogo();
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
        _session is { Tick: < 100, IsReplay: false } && _current is { FocusCiv: 0 } && !EffectiveCouncil(_current) &&
        _current.FocusSlotProject[0] < 0 && _current.FocusSlotProject[1] < 0 && _current.FocusSlotProject[2] < 0;

    private string YearText(MatchFrame frame) => $"Year {frame.Year + 1}";
}
