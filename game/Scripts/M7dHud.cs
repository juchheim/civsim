using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>M7d onboarding, save/load, replay controls, and the match end screen.</summary>
public partial class Game
{
    private static readonly string[] OnboardingTipTitles =
    {
        "Your focus slots", "A new method", "Winter", "Efficiency badges", "A growing people", "A new era", "First contact", "Food security",
    };

    private static readonly string[] OnboardingTipText =
    {
        "Primary, Secondary, and Tertiary projects progress at 4:2:1. Open T to compare what each choice will change.",
        "A discovery changes daily life. Watch the Chronicle and follow the innovator's Moment on the map.",
        "Seasons turn every 15 seconds at Standard speed. Winter is when stored food and shelter matter most.",
        "A badge shows how efficiently a figure can use a method, and when they fall back to an older tool.",
        "A new settlement means your people have split and claimed more land. Use Claims to see the frontier.",
        "A Keystone advances your civilization and reveals the next era's research. The Chronicle records the milestone.",
        "First contact reveals a neighboring people. Their visible posture and the Chronicle explain how relations change.",
        "A food forecast is a warning to build stores and shelter before winter. Use the Food overlay to find pressure points.",
    };

    private const uint OnboardingFileMagic = 0x4F534943; // "CISO", little-endian
    private PanelContainer? _m7dToolbar;
    private Label? _m7dStatusLabel;
    private Button? _m7dSaveButton;
    private Button? _m7dLoadButton;
    private Button? _m7dReplaySaveButton;
    private Button? _m7dReplayWatchButton;
    private PanelContainer? _onboardingTipPanel;
    private Label? _onboardingTipTitle;
    private Label? _onboardingTipBody;
    private PanelContainer? _introPanel;
    private ColorRect? _introDimmer;
    private PanelContainer? _helpPanel;
    private VBoxContainer? _helpContents;
    private ColorRect? _endDimmer;
    private PanelContainer? _endPanel;
    private Label? _endTitle;
    private Label? _endBody;
    private bool _helpVisible;
    private bool _endScreenShown;
    private bool _introDismissed;
    private ulong _onboardingSeen;
    private long _lastOnboardingEventSequence;
    private long _endTimelineSequence;
    private float _onboardingTipTimer;
    private readonly List<ViewEvent> _endTimeline = new(64);

    private void CreateM7dHud(CanvasLayer layer, ulong seed)
    {
        bool forceIntro = Array.IndexOf(OS.GetCmdlineUserArgs(), "--show-onboarding") >= 0;
        _introDismissed = !forceIntro && (ReadIntroPreference() || IsDevelopmentPresentationRun());
        var toolbarPanel = new PanelContainer
        {
            Name = "M7dToolbar",
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -748,
            OffsetRight = -14,
            OffsetTop = 12,
            OffsetBottom = 86,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var toolbarRoot = new VBoxContainer();
        toolbarPanel.AddChild(toolbarRoot);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        _m7dSaveButton = ToolbarButton("Save", "Save the current match to the quick-save slot.", SaveQuickMatch);
        _m7dLoadButton = ToolbarButton("Load", "Load the quick-save slot.", LoadQuickMatch);
        _m7dReplaySaveButton = ToolbarButton("Save replay", "Write a replay of this match so far.", SaveLatestReplay);
        _m7dReplayWatchButton = ToolbarButton("Watch replay", "Play the latest saved replay and check its periodic checksums.", WatchLatestReplay);
        var helpButton = ToolbarButton("Help / tips", "Review the onboarding tips you've seen.", ToggleHelpPanel);
        row.AddChild(_m7dSaveButton);
        row.AddChild(_m7dLoadButton);
        row.AddChild(_m7dReplaySaveButton);
        row.AddChild(_m7dReplayWatchButton);
        row.AddChild(helpButton);
        toolbarRoot.AddChild(row);
        _m7dStatusLabel = new Label { Text = "" };
        _m7dStatusLabel.AddThemeFontSizeOverride("font_size", 12);
        toolbarRoot.AddChild(_m7dStatusLabel);
        layer.AddChild(toolbarPanel);
        _m7dToolbar = toolbarPanel;

        _onboardingTipPanel = new PanelContainer
        {
            Name = "ContextTip",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -260,
            OffsetRight = 260,
            OffsetTop = 122,
            OffsetBottom = 226,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        var tipRoot = new VBoxContainer();
        var tipHeader = new HBoxContainer();
        _onboardingTipTitle = new Label { Text = "Tip", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _onboardingTipTitle.AddThemeFontSizeOverride("font_size", 17);
        tipHeader.AddChild(_onboardingTipTitle);
        var tipClose = new Button { Text = "×", CustomMinimumSize = new Vector2(34, 28) };
        tipClose.Pressed += HideOnboardingTip;
        tipHeader.AddChild(tipClose);
        tipRoot.AddChild(tipHeader);
        _onboardingTipBody = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _onboardingTipBody.AddThemeFontSizeOverride("font_size", 15);
        tipRoot.AddChild(_onboardingTipBody);
        _onboardingTipPanel.AddChild(tipRoot);
        layer.AddChild(_onboardingTipPanel);

        CreateIntroModal(layer);
        CreateHelpPanel(layer);
        CreateEndScreen(layer, seed);
        if (!_introDismissed) ShowIntroModal();
        RefreshM7dButtons();
    }

    private Button ToolbarButton(string text, string tooltip, Action action)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(text == "Save replay" || text == "Watch replay" ? 118 : 88, 32),
        };
        button.Pressed += action;
        return button;
    }

    private void CreateIntroModal(CanvasLayer layer)
    {
        _introDimmer = new ColorRect
        {
            Name = "IntroDimmer",
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.025f, 0.035f, 0.04f, 0.54f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        layer.AddChild(_introDimmer);
        _introPanel = new PanelContainer
        {
            Name = "FirstSparkTip",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -300,
            OffsetRight = 300,
            OffsetTop = 224,
            OffsetBottom = 392,
            Visible = false,
        };
        var root = new VBoxContainer();
        var title = new Label { Text = "The Spark", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 23);
        root.AddChild(title);
        var body = new Label
        {
            Text = "You are their curiosity. Choose an Available project and set it as Primary for the largest share of Insight. Ideas with unmet prerequisites can wait in the queue.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        body.AddThemeFontSizeOverride("font_size", 17);
        root.AddChild(body);
        var button = new Button { Text = "Choose your first project", CustomMinimumSize = new Vector2(0, 38) };
        button.Pressed += DismissIntroModal;
        root.AddChild(button);
        _introPanel.AddChild(root);
        layer.AddChild(_introPanel);
    }

    private void CreateHelpPanel(CanvasLayer layer)
    {
        _helpPanel = new PanelContainer
        {
            Name = "OnboardingHelp",
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -530,
            OffsetRight = -14,
            OffsetTop = 96,
            OffsetBottom = 440,
            Visible = false,
        };
        var root = new VBoxContainer();
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "Help and tips", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "Close" };
        close.Pressed += ToggleHelpPanel;
        header.AddChild(close);
        root.AddChild(header);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _helpContents = new VBoxContainer();
        scroll.AddChild(_helpContents);
        root.AddChild(scroll);
        _helpPanel.AddChild(root);
        layer.AddChild(_helpPanel);
    }

    private void CreateEndScreen(CanvasLayer layer, ulong seed)
    {
        _endDimmer = new ColorRect
        {
            Name = "MatchEndDimmer",
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.02f, 0.025f, 0.03f, 0.78f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        layer.AddChild(_endDimmer);
        _endPanel = new PanelContainer
        {
            Name = "MatchResults",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -510,
            OffsetRight = 510,
            OffsetTop = -360,
            OffsetBottom = 360,
            Visible = false,
        };
        var root = new VBoxContainer();
        _endTitle = new Label { Text = "Match ended", HorizontalAlignment = HorizontalAlignment.Center };
        _endTitle.AddThemeFontSizeOverride("font_size", 25);
        root.AddChild(_endTitle);
        _endBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(930, 0),
        };
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_endBody);
        root.AddChild(scroll);
        var row = new HBoxContainer();
        var saveReplay = new Button { Text = "Save replay" };
        saveReplay.Pressed += SaveLatestReplay;
        var watchReplay = new Button { Text = "Watch replay" };
        watchReplay.Pressed += WatchLatestReplay;
        var close = new Button { Text = "Return to world" };
        close.Pressed += HideEndScreen;
        var newMatch = new Button { Text = "New match" };
        newMatch.Pressed += () => StartNewMatch(unchecked(seed + 1));
        row.AddChild(saveReplay);
        row.AddChild(watchReplay);
        row.AddChild(newMatch);
        row.AddChild(close);
        root.AddChild(row);
        _endPanel.AddChild(root);
        layer.AddChild(_endPanel);
    }

    private void ShowIntroModal()
    {
        if (_introDismissed || _introPanel is null) return;
        if (_introDimmer is not null) _introDimmer.Visible = true;
        _introPanel.Visible = true;
    }

    private void DismissIntroModal()
    {
        _introDismissed = true;
        if (_introPanel is not null) _introPanel.Visible = false;
        if (_introDimmer is not null) _introDimmer.Visible = false;
        WriteIntroPreference();
        if (!_researchWebVisible) SetResearchWebVisible(true);
    }

    private void ToggleHelpPanel()
    {
        _helpVisible = !_helpVisible;
        if (_helpPanel is null || _helpContents is null) return;
        _helpPanel.Visible = _helpVisible;
        if (!_helpVisible) return;
        foreach (Node child in _helpContents.GetChildren())
        {
            _helpContents.RemoveChild(child);
            child.QueueFree();
        }
        for (int i = 0; i < OnboardingTipText.Length; i++)
        {
            if ((_onboardingSeen & (1UL << i)) == 0) continue;
            var label = new Label
            {
                Text = $"{OnboardingTipTitles[i]}\n{OnboardingTipText[i]}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(370, 0),
            };
            label.AddThemeFontSizeOverride("font_size", 14);
            _helpContents.AddChild(label);
        }
        if (_helpContents.GetChildCount() == 0)
            _helpContents.AddChild(new Label { Text = "Tips appear when the simulation reaches their trigger." });
    }

    private bool HandleM7dInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return false;
        if (_introPanel?.Visible == true)
        {
            if (key.Keycode is Key.Enter or Key.Space)
            {
                DismissIntroModal();
                return true;
            }
            return true;
        }
        if (key.Keycode == Key.H)
        {
            ToggleHelpPanel();
            return true;
        }
        if (key.Keycode == Key.Escape)
        {
            if (_helpVisible)
            {
                ToggleHelpPanel();
                return true;
            }
            if (_onboardingTipPanel?.Visible == true)
            {
                HideOnboardingTip();
                return true;
            }
            if (_endScreenShown)
            {
                HideEndScreen();
                return true;
            }
        }
        return false;
    }

    private void ProcessM7dFrame(MatchFrame frame)
    {
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _endTimelineSequence) continue;
            _endTimelineSequence = item.Sequence;
            if (item.Kind is ViewEventKind.EraAdvanced or ViewEventKind.FirstClaimed)
            {
                _endTimeline.Add(item);
                if (_endTimeline.Count > 64) _endTimeline.RemoveAt(0);
            }
        }

        if (_introPanel?.Visible == true) return;

        int tip = -1;
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _lastOnboardingEventSequence) continue;
            _lastOnboardingEventSequence = item.Sequence;
            if (item.Civ != 0) continue;
            if (item.Kind == ViewEventKind.SeasonBegan && item.A == 3 && tip < 0) tip = 2;
            else if (item.Kind == ViewEventKind.Moment && tip < 0) tip = 1;
            else if (item.Kind == ViewEventKind.VisibleChange && tip < 0) tip = 3;
            else if (item.Kind == ViewEventKind.Contact && tip < 0) tip = 6;
            else if (item.Kind == ViewEventKind.EraAdvanced && tip < 0) tip = 5;
            else if (item.Kind == ViewEventKind.FamineRisk && tip < 0) tip = 7;
        }
        if (tip < 0 && frame.CivSettlements.Length > 0 && frame.CivSettlements[0] > 1 && !TipSeen(4)) tip = 4;
        if (tip < 0 && !_researchWebVisible && frame.FocusCiv == 0 &&
            (frame.FocusSlotProject[0] >= 0 || frame.FocusSlotProject[1] >= 0 || frame.FocusSlotProject[2] >= 0) && !TipSeen(0)) tip = 0;
        if (tip >= 0) ShowOnboardingTip(tip);
    }

    private void UpdateM7d(double delta)
    {
        if (_onboardingTipTimer > 0)
        {
            _onboardingTipTimer = Math.Max(0, _onboardingTipTimer - (float)delta);
            if (_onboardingTipTimer == 0) HideOnboardingTip();
        }
        if (_session is null) return;
        if (_session.MatchEnded && !_endScreenShown) ShowEndScreen();
        if (_m7dStatusLabel is not null && _session.IsReplay && _session.ReplayFinished)
            _m7dStatusLabel.Text = _session.ReplayStatus;
        RefreshM7dButtons();
    }

    private void ShowOnboardingTip(int index)
    {
        if (index < 0 || index >= OnboardingTipText.Length || TipSeen(index)) return;
        _onboardingSeen |= 1UL << index;
        if (_onboardingTipTitle is not null) _onboardingTipTitle.Text = OnboardingTipTitles[index];
        if (_onboardingTipBody is not null) _onboardingTipBody.Text = OnboardingTipText[index];
        if (_onboardingTipPanel is not null) _onboardingTipPanel.Visible = true;
        _onboardingTipTimer = 14.0f;
    }

    private bool TipSeen(int index) => (_onboardingSeen & (1UL << index)) != 0;

    private void HideOnboardingTip()
    {
        if (_onboardingTipPanel is not null) _onboardingTipPanel.Visible = false;
        _onboardingTipTimer = 0;
    }

    private void SaveQuickMatch()
    {
        try
        {
            if (_session is null || !_session.CanSave) throw new InvalidOperationException("Quick-save is available during a live match.");
            GameSave.Write(GameSave.QuickSavePath, _session.CaptureSave(_onboardingSeen, _introDismissed));
            SetM7dStatus($"Saved · {GameSave.QuickSavePath}");
        }
        catch (Exception ex) { SetM7dStatus($"Save failed · {ex.Message}"); }
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
            SetM7dStatus($"Loaded · Year {_current?.Year + 1 ?? 1}");
        }
        catch (Exception ex) { SetM7dStatus($"Load failed · {ex.Message}"); }
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
                _session.CreateReplay().Write(stream);
            File.Move(temporary, path, overwrite: true);
            SetM7dStatus($"Replay saved · tick {_session.Tick:N0}");
        }
        catch (Exception ex) { SetM7dStatus($"Replay save failed · {ex.Message}"); }
    }

    private void WatchLatestReplay()
    {
        try
        {
            if (_contentPack is null) throw new InvalidOperationException("The content pack is not loaded.");
            using var stream = new FileStream(GameSave.LatestReplayPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
            Replay replay = Replay.Read(stream);
            SimulationSession watcher = SimulationSession.Watch(replay, _contentPack, 0);
            _onboardingSeen = 0;
            HideEndScreen();
            ReplaceSession(watcher);
            SetM7dStatus($"Replay · {replay.TotalTicks / 600:N0} simulated minutes");
        }
        catch (Exception ex) { SetM7dStatus($"Replay load failed · {ex.Message}"); }
    }

    private void ReplaceSession(SimulationSession replacement)
    {
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
        _chronicleEntries.Clear();
        _chronicleSubjects.Clear();
        _endScreenShown = false;
        if (_chronicleList is not null) RefreshChronicleText();
        RebuildVisualRenderer(_current, _session);
        if (_current is not null) ApplyNewFrame(_current);
        FrameCapital();
        UpdateHud();
        RefreshM7dButtons();
    }

    private void StartNewMatch(ulong seed)
    {
        if (_contentPack is null) return;
        _onboardingSeen = 0;
        _endScreenShown = false;
        HideEndScreen();
        ReplaceSession(new SimulationSession(seed, _contentPack));
        if (_current is { FocusSlotProject: var slots } && slots[0] < 0 && slots[1] < 0 && slots[2] < 0)
            SetResearchWebVisible(true);
        SetM7dStatus($"New match · seed {seed}");
    }

    private void ShowEndScreen()
    {
        if (_current is null || _endPanel is null || _endDimmer is null || _endBody is null) return;
        _endScreenShown = true;
        HideOnboardingTip();
        if (_introPanel is not null) _introPanel.Visible = false;
        if (_introDimmer is not null) _introDimmer.Visible = false;

        int[] order = new int[_current.CivCount];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) => _current.CivLegacy[b].CompareTo(_current.CivLegacy[a]));
        var lines = new List<string>(80) { "Final Legacy standings" };
        bool key3Reached = false;
        for (int civ = 0; civ < _current.CivCount; civ++) key3Reached |= _current.CivEra[civ] >= 3;
        if (_endTitle is not null) _endTitle.Text = key3Reached ? "Age of Metal reached" : "40-minute limit";
        for (int rank = 0; rank < order.Length; rank++)
        {
            int civ = order[rank];
            string name = civ == _current.ViewerCiv ? "Your people" : $"Civilization {civ + 1}";
            lines.Add($"{rank + 1}. {name} · Legacy {FormatFixed(_current.CivLegacy[civ])} · " +
                $"Mastery {FormatFixed(_current.CivLegacy[civ] - _current.CivFirstsLegacy[civ])} · " +
                $"Firsts {FormatFixed(_current.CivFirstsLegacy[civ])} · Dominion {FormatFixed(_current.CivDominion[civ])} · " +
                $"land {_current.CivLandBasisPoints[civ] / 100.0:F1}%");
        }
        lines.Add("");
        lines.Add("Keystone timeline");
        if (_endTimeline.Count == 0) lines.Add("No keystones were recorded in the viewer's event history.");
        foreach (ViewEvent item in _endTimeline)
        {
            if (item.Kind != ViewEventKind.EraAdvanced) continue;
            string civName = item.Civ == _current.ViewerCiv ? "Your people" : $"Civilization {item.Civ + 1}";
            string keystone = ProjectName(item.Project);
            lines.Add($"Year {item.Tick / 600 + 1} · {civName} entered era {item.A} through {keystone}.");
        }
        lines.Add("");
        lines.Add("What the AI was thinking");
        bool hadAiWhy = false;
        for (int i = 0; i < _current.AiWhyCount; i++)
        {
            AiWhyEntry why = _current.AiWhyEntries[i];
            if (why.Civ == _current.ViewerCiv) continue;
            string civName = $"Civilization {why.Civ + 1}";
            lines.Add($"Year {why.Tick / 600 + 1} · {civName} · {why.Layer} / {why.Kind}: {why.Reason}");
            hadAiWhy = true;
        }
        if (!hadAiWhy) lines.Add("No rival decision explanations were captured in this match.");
        _endBody.Text = string.Join("\n", lines);
        _endDimmer.Visible = true;
        _endPanel.Visible = true;
        _session?.SetPaused(true);
        RefreshM7dButtons();
    }

    private static string FormatFixed(long raw) => (raw / 4294967296.0).ToString("N1", CultureInfo.InvariantCulture);

    private void HideEndScreen()
    {
        _endScreenShown = false;
        if (_endDimmer is not null) _endDimmer.Visible = false;
        if (_endPanel is not null) _endPanel.Visible = false;
    }

    private void RefreshM7dButtons()
    {
        if (_session is null) return;
        if (_m7dSaveButton is not null) _m7dSaveButton.Disabled = !_session.CanSave;
        if (_m7dReplaySaveButton is not null) _m7dReplaySaveButton.Disabled = !_session.CanSave;
        if (_m7dLoadButton is not null) _m7dLoadButton.Disabled = !File.Exists(GameSave.QuickSavePath);
        if (_m7dReplayWatchButton is not null) _m7dReplayWatchButton.Disabled = !File.Exists(GameSave.LatestReplayPath);
    }

    private void SetM7dStatus(string message)
    {
        if (_m7dStatusLabel is not null) _m7dStatusLabel.Text = message;
    }

    private static bool IsDevelopmentPresentationRun()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--benchmark-", StringComparison.Ordinal) ||
                arg.StartsWith("--start-minute=", StringComparison.Ordinal) ||
                arg.StartsWith("--screenshot=", StringComparison.Ordinal)) return true;
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
