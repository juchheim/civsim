using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Game.Ui;
using CivSim.Sim.Ai;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>One shell for every modal (golden rectangles, Esc closes) plus the menu, intro, help, settings, confirm and end screen (spec §3.10–§3.11).</summary>
public partial class Game
{
    private sealed class ModalView
    {
        public Control Root = null!;
        public ColorRect Scrim = null!;
        public PanelContainer Panel = null!;
        public VBoxContainer Body = null!;
        public Label Title = null!, Subtitle = null!;
        public HBoxContainer Actions = null!;
        public float Width, Height;
        public UiLayer Layer = null!;
        public bool Open;
    }

    private ModalView? _introModal, _helpModal, _settingsModal, _confirmModal, _endModal, _newMatchModal;
    private bool _introPanelVisible, _helpOpen, _gameMenuOpen, _endScreenShown, _endScreenDismissed, _introDismissed;
    private UiPopover? _gameMenu;
    private UiLayer? _gameMenuLayer;
    private UiSegmented? _helpTabs, _uiSizeSegments;
    private readonly Control?[] _helpPanes = new Control?[4];
    private VBoxContainer? _helpTipsList;
    private Action? _confirmAction;
    private Label? _confirmBody;
    private UiButton? _confirmOk;
    private VBoxContainer? _endContent;
    private readonly List<ViewEvent> _endTimeline = new(64);
    private long _endTimelineSequence;

    private ModalView MakeModal(string name, float width, float height, string title, string subtitle, Func<bool> close)
    {
        var view = new ModalView { Width = width, Height = height };
        view.Root = new Control { Name = name, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        view.Root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        view.Scrim = new ColorRect { Color = UiTokens.ScrimModal, MouseFilter = Control.MouseFilterEnum.Stop };
        view.Scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        view.Root.AddChild(view.Scrim);
        view.Panel = UiKit.Panel("SheetPanel");
        view.Root.AddChild(view.Panel);
        var column = UiKit.Column(UiTokens.Sp3);
        view.Panel.AddChild(column);
        var head = UiKit.Row(UiTokens.Sp2);
        view.Title = UiKit.Text(title, height >= 400 ? "Display" : "Title");
        view.Title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(view.Title);
        column.AddChild(head);
        view.Subtitle = UiKit.Text(subtitle, "BodySecondary", wrap: true);
        view.Subtitle.Visible = subtitle.Length > 0;
        column.AddChild(view.Subtitle);
        view.Body = UiKit.Column(UiTokens.Sp3);
        view.Body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        column.AddChild(view.Body);
        view.Actions = UiKit.Row(UiTokens.Sp2);
        view.Actions.Alignment = BoxContainer.AlignmentMode.End;
        column.AddChild(view.Actions);
        view.Layer = RegisterLayer(name, true, close);
        _hudRoot!.AddChild(view.Root);
        return view;
    }

    private void OpenModal(ModalView view)
    {
        if (view.Open) return;
        CloseDrawersAndMenus();
        CloseQueuePopover();
        CloseLeaningPopover();
        view.Open = true;
        LayoutModals();
        view.Panel.Modulate = new Color(1, 1, 1, 0);
        view.Root.Visible = true;
        UiKit.Fade(view.Panel, true, UiTokens.SlowS);
        _ui.Push(view.Layer);
        UpdatePausedTreatment();
    }

    private void CloseModal(ModalView view)
    {
        if (!view.Open) return;
        view.Open = false;
        view.Root.Visible = false;
        _ui.Remove(view.Layer);
        UpdatePausedTreatment();
    }

    private void LayoutModals()
    {
        foreach (ModalView? view in new[] { _introModal, _helpModal, _settingsModal, _confirmModal, _endModal, _newMatchModal })
        {
            if (view is null) continue;
            float w = Math.Min(view.Width, _logicalSize.X - 2 * Margin);
            float h = Math.Min(view.Height, _logicalSize.Y - 2 * Margin);
            view.Panel.AnchorLeft = view.Panel.AnchorRight = 0.5f;
            view.Panel.AnchorTop = view.Panel.AnchorBottom = 0.5f;
            view.Panel.OffsetLeft = -w / 2; view.Panel.OffsetRight = w / 2;
            view.Panel.OffsetTop = -h / 2; view.Panel.OffsetBottom = h / 2;
            view.Panel.GrowHorizontal = Control.GrowDirection.Both;
            view.Panel.GrowVertical = Control.GrowDirection.Both;
            view.Panel.CustomMinimumSize = new Vector2(w, h);
        }
    }

    private void CreateModals(ulong seed)
    {
        bool forceIntro = HasArgument("--show-onboarding");
        _introDismissed = !forceIntro && (ReadIntroPreference() || IsDevelopmentPresentationRun());
        BuildIntro();
        BuildHelp();
        BuildSettings();
        BuildConfirm();
        BuildNewMatch();
        BuildEndScreen(seed);
        BuildGameMenu();
        if (!_introDismissed && (!IsDevelopmentPresentationRun() || forceIntro)) ShowIntroModal(false);
    }

    // ---- intro ----------------------------------------------------------------------------------

    private void BuildIntro()
    {
        _introModal = MakeModal("intro", 610, 377, "The Spark", "You are your people's curiosity.", () => { DismissIntro(); return true; });
        var steps = UiKit.Column(UiTokens.Sp3);
        string[] texts =
        {
            "Choose what they learn: click a project, then Start.",
            "Watch them use it. New knowledge changes how they live.",
            "Let time run. Press Space to pause whenever you like.",
        };
        for (int i = 0; i < 3; i++)
        {
            var row = UiKit.Row(UiTokens.Sp3);
            row.AddChild(new UiRankBadge(i + 1));
            row.AddChild(UiKit.Text(texts[i], "Body", wrap: true));
            steps.AddChild(row);
        }
        _introModal.Body.AddChild(steps);
        _introModal.Actions.AddChild(UiKit.Primary("Choose a first project", DismissIntro, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void ShowIntroModal(bool force)
    {
        if (_introModal is null || (_introDismissed && !force)) return;
        _introPanelVisible = true;
        OpenModal(_introModal);
    }

    private void DismissIntro()
    {
        if (_introModal is null) return;
        _introPanelVisible = false;
        _introDismissed = true;
        CloseModal(_introModal);
        WriteIntroPreference();
        if (!_researchWebVisible) OpenResearchWeb(-1);
    }

    // ---- help -----------------------------------------------------------------------------------

    private void BuildHelp()
    {
        _helpModal = MakeModal("help", 987, 610, "Help", "", () => { ToggleHelp(false); return true; });
        _helpModal.Title.ThemeTypeVariation = "Title";
        _helpTabs = new UiSegmented(new[] { "Controls", "Research", "Map layers", "Tips" }, 0, SelectHelpTab, 110);
        _helpTabs.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _helpModal.Body.AddChild(_helpTabs);
        var host = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _helpModal.Body.AddChild(host);

        // Controls
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", UiTokens.Sp4);
        grid.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        string alt = OS.GetName() == "macOS" ? "Option" : "Alt";
        string[] head = { "ACTION", "MOUSE", "TRACKPAD", "KEYS" };
        foreach (string h in head) grid.AddChild(UiKit.Text(h, "EyebrowTertiary"));
        string[][] rows =
        {
            new[] { "Move the map", "Drag", "Drag", "W A S D · arrows" },
            new[] { "Zoom", "Scroll wheel", "Pinch or two-finger scroll", "− / = · 0 1 2 3" },
            new[] { "Turn and tilt", "Right-drag", $"{alt}-drag", "Q / E" },
            new[] { "Pause or resume", "", "", "Space" },
            new[] { "Slower / faster", "", "", "[ and ]" },
            new[] { "Research", "", "", "T" },
            new[] { "Diplomacy", "", "", "O" },
            new[] { "Chronicle", "", "", "C" },
            new[] { "Resources layer", "", "", "R" },
            new[] { "Place a Beacon", "Click a tile", "Click a tile", "B first" },
            new[] { "Frame your settlement", "", "", "F" },
            new[] { "Save / load", "", "", "F5 / F9" },
            new[] { "Close the top panel", "", "", "Esc" },
        };
        foreach (string[] row in rows)
            for (int i = 0; i < 4; i++) grid.AddChild(UiKit.Text(row[i], i == 0 ? "BodyStrong" : "Body", i == 0 ? null : UiTokens.TextSecondary));
        _helpPanes[0] = UiKit.Scroll(grid);

        // Research
        var research = UiKit.Column(UiTokens.Sp3);
        string[] paragraphs =
        {
            "Slots. You have three research slots. Primary gets 4 of every 7 research points, Secondary 2, Tertiary 1. An empty slot passes its share to the others.",
            "Insight is your research speed. More people, elders and learning raise it.",
            "Ready projects can start now. Almost ready projects need one more thing first. Queue them and they start by themselves.",
            "Turning points move your people into a new age once enough projects of the current age are known.",
            "Auto-fill lets the elders choose when a slot opens. Their leaning says which themes they favour.",
            "Progress is never lost. Replace or stop a project any time; it keeps its progress.",
        };
        foreach (string p in paragraphs)
        {
            int dot = p.IndexOf('.');
            var row = UiKit.Row(UiTokens.Sp2);
            row.AddChild(new UiIcon(UiGlyph.Spark, 16, UiTokens.Accent) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });
            row.AddChild(UiKit.Text(p, "Body", wrap: true));
            research.AddChild(row);
            _ = dot;
        }
        _helpPanes[1] = UiKit.Scroll(research);

        // Map layers
        var layers = UiKit.Column(UiTokens.Sp3);
        (string, string)[] entries =
        {
            ("Claims", "Who holds the land, and how firmly. A stronger colour means a stronger claim; 70 or more makes land yours."),
            ("Food", "Which settlements have enough food: green is well fed, then tight, short and famine. In autumn and winter it shows the forecast for late winter."),
            ("Methods", "How good your people's tools are. Purple tiles use newer tools, blue older ones, and orange tiles know a better method they can't use yet."),
            ("Resources", "Coloured stones mark deposits your people have found. Hover a tile to see what is there."),
        };
        foreach ((string title, string text) in entries)
        {
            layers.AddChild(UiKit.Text(title, "BodyStrong"));
            layers.AddChild(UiKit.Text(text, "BodySecondary", wrap: true));
        }
        _helpPanes[2] = UiKit.Scroll(layers);

        // Tips
        _helpTipsList = UiKit.Column(UiTokens.Sp3);
        _helpPanes[3] = UiKit.Scroll(_helpTipsList);

        foreach (Control? pane in _helpPanes)
        {
            if (pane is null) continue;
            pane.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            pane.Visible = false;
            host.AddChild(pane);
        }
        _helpPanes[0]!.Visible = true;
        _helpModal.Actions.AddChild(UiKit.Primary("Close", () => ToggleHelp(false), null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void SelectHelpTab(int index)
    {
        for (int i = 0; i < _helpPanes.Length; i++) if (_helpPanes[i] is Control pane) pane.Visible = i == index;
        if (index == 3) RebuildHelpTips();
    }

    private void RebuildHelpTips()
    {
        if (_helpTipsList is null) return;
        foreach (Node child in _helpTipsList.GetChildren()) { _helpTipsList.RemoveChild(child); child.QueueFree(); }
        int shown = 0;
        for (int i = 0; i < CoachTitles.Length; i++)
        {
            if (!TipSeen(i)) continue;
            _helpTipsList.AddChild(UiKit.Text(CoachTitles[i], "BodyStrong"));
            _helpTipsList.AddChild(UiKit.Text(CoachBodies[i], "BodySecondary", wrap: true));
            shown++;
        }
        if (shown == 0) _helpTipsList.AddChild(UiKit.Text("Tips appear when something new happens in your world.", "BodySecondary", wrap: true));
        if (!_uiSettings.ShowTips)
            _helpTipsList.AddChild(UiKit.Secondary("Turn tips back on", () => { _uiSettings.ShowTips = true; _uiSettings.Save(); RebuildHelpTips(); }, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void ToggleHelp(bool? force = null)
    {
        if (_helpModal is null) return;
        bool open = force ?? !_helpOpen;
        if (open == _helpOpen) return;
        _helpOpen = open;
        if (open)
        {
            if (_researchWebVisible) CloseResearchWeb();
            _helpTabs!.Select(0, true);
            OpenModal(_helpModal);
        }
        else CloseModal(_helpModal);
    }

    // ---- settings -------------------------------------------------------------------------------

    private void BuildSettings()
    {
        _settingsModal = MakeModal("settings", 610, 377, "Settings", "", () => { CloseSettings(); return true; });
        var column = UiKit.Column(UiTokens.Sp2);
        var sizeRow = UiKit.Row(UiTokens.Sp3);
        sizeRow.AddChild(UiKit.Text("UI size", "BodyStrong", minWidth: 150));
        int current = _uiSettings.UiScale >= 1.25f ? 2 : _uiSettings.UiScale >= 1.1f ? 1 : 0;
        _uiSizeSegments = new UiSegmented(new[] { "100%", "115%", "130%" }, current, index =>
        {
            _uiSettings.UiScale = index switch { 1 => 1.15f, 2 => 1.3f, _ => 1.0f };
            _uiSettings.Save();
            ApplyWindowScale();
            ApplyHudLayout();
        }, 64);
        sizeRow.AddChild(_uiSizeSegments);
        column.AddChild(sizeRow);
        column.AddChild(new UiToggle("Reduce motion", _uiSettings.ReduceMotion, v => { _uiSettings.ReduceMotion = v; _uiSettings.Save(); }) { ThemeTypeVariation = "ToggleRow" });
        column.AddChild(new UiToggle("Show control hints", _uiSettings.ShowHints, v => { _uiSettings.ShowHints = v; _uiSettings.Save(); }) { ThemeTypeVariation = "ToggleRow" });
        column.AddChild(new UiToggle("Show tips", _uiSettings.ShowTips, v => { _uiSettings.ShowTips = v; _uiSettings.Save(); }) { ThemeTypeVariation = "ToggleRow" });
        column.AddChild(new UiToggle("Pause while the research web is open", _uiSettings.PauseInWeb, v => { _uiSettings.PauseInWeb = v; _uiSettings.Save(); }) { ThemeTypeVariation = "ToggleRow" });
        column.AddChild(new UiToggle("Pause on danger", _uiSettings.PauseOnDanger, v => { _uiSettings.PauseOnDanger = v; _uiSettings.Save(); }) { ThemeTypeVariation = "ToggleRow" });
        _settingsModal.Body.AddChild(column);
        _settingsModal.Actions.AddChild(UiKit.Primary("Close", CloseSettings, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void OpenSettings()
    {
        if (_settingsModal is null) return;
        ToggleGameMenu(false);
        OpenModal(_settingsModal);
    }

    private void CloseSettings()
    {
        if (_settingsModal is not null) CloseModal(_settingsModal);
    }

    // ---- confirm --------------------------------------------------------------------------------

    private void BuildConfirm()
    {
        _confirmModal = MakeModal("confirm", 377, 233, "Are you sure?", "", () => { CloseModal(_confirmModal!); return true; });
        _confirmBody = UiKit.Text("", "Body", wrap: true);
        _confirmModal.Body.AddChild(_confirmBody);
        _confirmModal.Actions.AddChild(UiKit.Ghost("Cancel", () => CloseModal(_confirmModal), null, UiGlyph.None, UiTokens.ControlM, true));
        _confirmOk = UiKit.Primary("OK", () => { Action? act = _confirmAction; CloseModal(_confirmModal); act?.Invoke(); }, null, UiGlyph.None, UiTokens.ControlM, true);
        _confirmModal.Actions.AddChild(_confirmOk);
    }

    private void ShowConfirm(string title, string body, string okLabel, Action action)
    {
        if (_confirmModal is null) return;
        ToggleGameMenu(false);
        _confirmModal.Title.Text = title;
        _confirmBody!.Text = body;
        _confirmOk!.Text = okLabel;
        _confirmAction = action;
        OpenModal(_confirmModal);
    }

    private void AskLoadQuickMatch()
    {
        if (!File.Exists(GameSave.QuickSavePath)) { ShowToast("No save yet", ToastKind.Info, "Press F5 to save first.", key: "nosave"); return; }
        ShowConfirm("Load the quick-save?", "Anything since that save will be lost.", "Load", LoadQuickMatch);
    }

    // ---- new match ------------------------------------------------------------------------------

    private static readonly Difficulty[] DifficultyLevels = { Difficulty.Settler, Difficulty.Chieftain, Difficulty.Warlord, Difficulty.Legend };
    private UiSegmented? _difficultySegments;
    private Label? _difficultyBlurb;
    private Difficulty _newMatchDifficulty = Difficulty.Settler;

    private static string DifficultyName(Difficulty level) => level.ToString();

    private static string DifficultyBlurb(Difficulty level) => level switch
    {
        Difficulty.Settler => "The gentlest rivals. They plan one era ahead, react late to threats and sometimes pick from a simple list. Good for learning and for trying odd research paths.",
        Difficulty.Chieftain => "A real race. Rivals plan two eras ahead and re-plan every season. Experimenting will cost you.",
        Difficulty.Warlord => "Rivals plan three eras ahead, read your moves fully and re-plan the moment something happens. Expect to lose without a tight path.",
        _ => "The sharpest rivals. They plan four eras ahead and time their moves against the leader. For experts.",
    };

    private void BuildNewMatch()
    {
        _newMatchModal = MakeModal("newmatch", 610, 330, "New match", "Rivals think harder at higher levels. They never get extra resources or research, at any level.", () => { CloseModal(_newMatchModal!); return true; });
        var column = UiKit.Column(UiTokens.Sp3);
        var row = UiKit.Row(UiTokens.Sp3);
        row.AddChild(UiKit.Text("Rivals", "BodyStrong", minWidth: 80));
        _difficultySegments = new UiSegmented(new[] { "Settler", "Chieftain", "Warlord", "Legend" }, 0, index =>
        {
            _newMatchDifficulty = DifficultyLevels[index];
            UiKit.SetText(_difficultyBlurb!, DifficultyBlurb(_newMatchDifficulty));
        }, 96);
        row.AddChild(_difficultySegments);
        column.AddChild(row);
        _difficultyBlurb = UiKit.Text(DifficultyBlurb(Difficulty.Settler), "Body", wrap: true);
        column.AddChild(_difficultyBlurb);
        column.AddChild(UiKit.Text("This match ends when you start the new one. Save first if you want to come back.", "CaptionTertiary", wrap: true));
        _newMatchModal.Body.AddChild(column);
        _newMatchModal.Actions.AddChild(UiKit.Ghost("Cancel", () => CloseModal(_newMatchModal), null, UiGlyph.None, UiTokens.ControlM));
        _newMatchModal.Actions.AddChild(UiKit.Primary("Start", () =>
        {
            _uiSettings.Difficulty = _newMatchDifficulty;
            _uiSettings.Save();
            CloseModal(_newMatchModal);
            StartNewMatch(unchecked(_matchSeed + 1), _newMatchDifficulty);
        }, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private void OpenNewMatch()
    {
        if (_newMatchModal is null) return;
        ToggleGameMenu(false);
        if (_endModal is { Open: true }) HideEndScreen();
        _newMatchDifficulty = _uiSettings.Difficulty;
        int index = Array.IndexOf(DifficultyLevels, _newMatchDifficulty);
        _difficultySegments!.Select(Math.Max(0, index), true);
        OpenModal(_newMatchModal);
    }

    // ---- game menu ------------------------------------------------------------------------------

    private void BuildGameMenu()
    {
        _gameMenu = new UiPopover("CardPanel") { Dismissed = () => ToggleGameMenu(false) };
        _hudRoot!.AddChild(_gameMenu);
        _gameMenuLayer = RegisterLayer("menu", false, () => { ToggleGameMenu(false); return true; });
    }

    private void ToggleGameMenu(bool? force = null)
    {
        if (_gameMenu is null || _btnMenu is null || _session is null) return;
        bool open = force ?? !_gameMenuOpen;
        if (open == _gameMenuOpen) return;
        _gameMenuOpen = open;
        if (!open)
        {
            _gameMenu.Close();
            _ui.Remove(_gameMenuLayer!);
            return;
        }
        if (_chronicleOpen) ToggleChronicle(false);
        if (_diplomacyOpen) ToggleDiplomacy(false);
        if (_researchWebVisible) CloseResearchWeb();
        foreach (Node child in _gameMenu.Body.GetChildren()) { _gameMenu.Body.RemoveChild(child); child.QueueFree(); }
        var column = UiKit.Column(2);
        void Row(string label, Action action, bool enabled = true, string? tip = null, string key = "")
        {
            UiButton row = UiKit.Ghost(key.Length > 0 ? $"{label}   {key}" : label, () => { ToggleGameMenu(false); action(); }, tip);
            row.ThemeTypeVariation = "RowButton";
            row.Alignment = HorizontalAlignment.Left;
            row.Disabled = !enabled;
            row.CustomMinimumSize = new Vector2(233, UiTokens.ControlM);
            row.FocusMode = Control.FocusModeEnum.All;
            column.AddChild(row);
        }
        bool saveExists = File.Exists(GameSave.QuickSavePath), replayExists = File.Exists(GameSave.LatestReplayPath);
        if (_session.Paused && !_session.MatchEnded) Row("Resume", TogglePause);
        Row("Save game", SaveQuickMatch, _session.CanSave, _session.CanSave ? null : "Saving is available in a live match.", "F5");
        Row("Load game…", AskLoadQuickMatch, saveExists, saveExists ? null : "No save yet.", "F9");
        Row("Save replay", SaveLatestReplay, _session.CanSave);
        Row("Watch last replay", WatchLatestReplay, replayExists, replayExists ? null : "No replay yet.");
        column.AddChild(UiKit.Divider());
        Row("Settings…", OpenSettings);
        Row("Help", () => ToggleHelp(true), true, null, "H");
        column.AddChild(UiKit.Divider());
        Row("New match…", OpenNewMatch);
        Row("Quit…", () => ShowConfirm("Quit CivSim?", "Unsaved progress will be lost.", "Quit", () => GetTree().Quit()));
        _gameMenu.Body.AddChild(column);
        Vector2 pos = _btnMenu.GlobalPosition;
        _gameMenu.OpenRightAligned(pos.X + _btnMenu.Size.X, pos.Y + _btnMenu.Size.Y + 6, 233);
        _ui.Push(_gameMenuLayer!);
    }

    // ---- end screen -----------------------------------------------------------------------------

    private void BuildEndScreen(ulong seed)
    {
        _matchSeed = seed;
        _endModal = MakeModal("end", 987, 610, "Match ended", "", () => { HideEndScreen(); return true; });
        _endContent = UiKit.Column(UiTokens.Sp3);
        _endModal.Body.AddChild(UiKit.Scroll(_endContent));
        _endModal.Actions.AddChild(UiKit.Ghost("Save replay", SaveLatestReplay, null, UiGlyph.None, UiTokens.ControlM));
        _endModal.Actions.AddChild(UiKit.Ghost("Watch replay", WatchLatestReplay, null, UiGlyph.None, UiTokens.ControlM));
        _endModal.Actions.AddChild(UiKit.Secondary("New match", OpenNewMatch, null, UiGlyph.None, UiTokens.ControlM, true));
        _endModal.Actions.AddChild(UiKit.Primary("Keep watching", HideEndScreen, null, UiGlyph.None, UiTokens.ControlM, true));
    }

    private ulong _matchSeed;

    private void ShowEndScreen()
    {
        if (_current is null || _endModal is null || _endContent is null) return;
        _endScreenShown = true;
        HideCoachTip();
        if (_introModal is { Open: true }) { _introPanelVisible = false; CloseModal(_introModal); }
        MatchFrame frame = _current;
        int viewer = Math.Max(0, frame.ViewerCiv);
        var order = new int[frame.CivCount];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) => frame.CivLegacy[b].CompareTo(frame.CivLegacy[a]));
        _endModal.Title.Text = _session?.EndReason == MatchEndReason.HumanKeystone || frame.CivEra[viewer] >= 4
            ? "The Iron Age dawns"
            : _session?.EndReason == MatchEndReason.RivalCountdown ? "The Iron Age has come"
            : $"{_session?.MatchYears ?? 64} years have passed";
        int myRank = Array.IndexOf(order, viewer) + 1;
        string leader = order[0] == viewer ? "Your people lead" : $"The {UiText.CivName(order[0])} lead";
        _endModal.Subtitle.Text = $"{leader} the world in Legacy. Your people finished {myRank} of {frame.CivCount}.";
        _endModal.Subtitle.Visible = true;

        foreach (Node child in _endContent.GetChildren()) { _endContent.RemoveChild(child); child.QueueFree(); }
        var columns = UiKit.Row(UiTokens.Sp4);
        var left = UiKit.Column(UiTokens.Sp3);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.SizeFlagsStretchRatio = 0.618f;
        var right = UiKit.Column(UiTokens.Sp3);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsStretchRatio = 0.382f;

        left.AddChild(UiKit.Text("STANDINGS", "EyebrowTertiary"));
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", UiTokens.Sp4);
        grid.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        foreach (string h in new[] { "", "PEOPLE", "LEGACY", "FIRSTS", "LAND" }) grid.AddChild(UiKit.Text(h, "EyebrowTertiary"));
        for (int rank = 0; rank < order.Length; rank++)
        {
            int civ = order[rank];
            var who = UiKit.Row(UiTokens.Sp2);
            who.AddChild(UiKit.Text($"{rank + 1}", "NumStrong", minWidth: 20));
            who.AddChild(new UiEmblem(civ, 26));
            grid.AddChild(who);
            grid.AddChild(UiKit.Text(civ == viewer ? "Your people" : UiText.CivName(civ), civ == viewer ? "BodyStrong" : "Body", ellipsis: true));
            grid.AddChild(UiKit.Text(FormatFixed(frame.CivLegacy[civ]), "Num"));
            grid.AddChild(UiKit.Text(FormatFixed(frame.CivFirstsLegacy[civ]), "Num"));
            grid.AddChild(UiKit.Text($"{frame.CivLandBasisPoints[civ] / 100.0:F1}%", "Num"));
        }
        left.AddChild(grid);
        left.AddChild(UiKit.Text("Legacy = points for knowledge mastered plus world firsts. Land = share of claimed land.", "CaptionTertiary", wrap: true));
        if (_session is not null)
            left.AddChild(UiKit.Text($"Rivals played at {DifficultyName(_session.Difficulty)}.", "CaptionTertiary", wrap: true));

        left.AddChild(UiKit.Text("TURNING POINTS", "EyebrowTertiary"));
        int turning = 0;
        foreach (ViewEvent item in _endTimeline)
        {
            if (item.Kind != ViewEventKind.EraAdvanced) continue;
            string who = item.Civ == viewer ? "Your people" : $"The {UiText.CivName(item.Civ)}";
            left.AddChild(UiKit.Text($"Year {item.Tick / 600 + 1} · {who} entered the {UiText.AgeName(item.A)} age through {ProjectName(item.Project)}", "Body", wrap: true));
            turning++;
        }
        if (turning == 0) left.AddChild(UiKit.Text("No turning points were recorded.", "BodySecondary"));

        right.AddChild(UiKit.Text("WHAT YOUR RIVALS WERE THINKING", "EyebrowTertiary"));
        bool any = false;
        for (int civ = 0; civ < frame.CivCount; civ++)
        {
            if (civ == viewer) continue;
            var entries = new List<AiWhyEntry>();
            for (int i = frame.AiWhyCount - 1; i >= 0 && entries.Count < 4; i--)
                if (frame.AiWhyEntries[i].Civ == civ) entries.Add(frame.AiWhyEntries[i]);
            if (entries.Count == 0) continue;
            any = true;
            var head = UiKit.Row(UiTokens.Sp2);
            head.AddChild(new UiEmblem(civ, 26));
            head.AddChild(UiKit.Text(UiText.CivName(civ), "BodyStrong"));
            right.AddChild(head);
            foreach (AiWhyEntry why in entries)
                right.AddChild(UiKit.Text($"Year {why.Tick / 600 + 1} · {why.Reason}", "Caption", wrap: true));
        }
        if (!any) right.AddChild(UiKit.Text("No rival explanations were recorded.", "BodySecondary", wrap: true));
        columns.AddChild(left);
        columns.AddChild(right);
        _endContent.AddChild(columns);

        OpenModal(_endModal);
        _session?.SetPaused(true);
    }

    private static string FormatFixed(long raw) => (raw / 4294967296.0).ToString("N1", System.Globalization.CultureInfo.InvariantCulture);

    private void HideEndScreen()
    {
        _endScreenShown = false;
        _endScreenDismissed = true;
        if (_endModal is not null) CloseModal(_endModal);
    }

    private void ProcessEndTimeline(MatchFrame frame)
    {
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _endTimelineSequence) continue;
            _endTimelineSequence = item.Sequence;
            if (item.Kind is ViewEventKind.EraAdvanced)
            {
                // A rival completing KEY-4 starts the Iron Age countdown (docs/MVP/22 §3.1): 3 years to finish or the age closes.
                if (item.A >= 4 && item.Civ != frame.ViewerCiv && _session is { IsReplay: false })
                    ShowToast($"The Iron Age is coming: {Math.Max(1, (_session.ChapterSeasonsLeft + 3) / 4)} years left", ToastKind.Info,
                        $"The {UiText.CivName(item.Civ)} reached the {UiText.AgeName(item.A)} age. The match ends when your people get there too, or in 3 years, or at Year {_session.MatchYears}.",
                        key: $"rival-era-{item.Civ}-{item.A}");
                _endTimeline.Add(item);
                if (_endTimeline.Count > 64) _endTimeline.RemoveAt(0);
            }
        }
    }
}
