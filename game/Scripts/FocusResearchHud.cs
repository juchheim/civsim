using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>M7b focus bar and viewer-filtered research web. All actions become simulation commands.</summary>
public partial class Game
{
    private static readonly Color[] ResearchThemeColors =
    {
        new("#d79a4b"), new("#79aa81"), new("#71a8c8"), new("#c9b36a"),
    };
    private static readonly string[] ResearchThemeNames = { "Craft", "Kin", "Reach", "Keystone" };
    private static readonly string[] ResearchTypeNames = { "Discovery", "Method", "Structure", "Role", "Institution", "Wonder", "Keystone" };
    private static readonly string[] ResearchStateNames = { "Hidden", "Glimpsed", "Available", "Slotted", "Completed", "Dormant" };

    private PanelContainer? _focusBar;
    private PanelContainer? _researchWebPanel;
    private ColorRect? _researchDimmer;
    private PanelContainer? _leaningPanel;
    private CheckButton? _councilToggle;
    private Button? _researchOpenButton;
    private Label? _insightLabel;
    private readonly Button?[] _focusProjectLabels = new Button?[3];
    private readonly Label?[] _focusMetaLabels = new Label?[3];
    private readonly Label?[] _focusQueueLabels = new Label?[3];
    private readonly ProgressBar?[] _focusProgressBars = new ProgressBar?[3];
    private readonly HSlider?[] _leaningSliders = new HSlider?[3];
    private readonly Label?[] _leaningValues = new Label?[3];
    private readonly Dictionary<int, Button> _researchProjectButtons = new();
    private readonly Dictionary<int, Button> _researchKeystoneButtons = new();
    private readonly HSlider?[] _leaningEditSliders = new HSlider?[3];
    private readonly Label?[] _leaningEditValues = new Label?[3];
    private LineEdit? _researchSearch;
    private OptionButton? _researchThemeFilter, _researchTypeFilter, _researchStateFilter;
    private Label? _researchSelectionTitle, _researchSelectionText, _researchWebMessage;
    private Button? _researchQueueButton, _researchRemoveQueueButton;
    private Button?[] _researchSlotButtons = new Button?[3];
    private VBoxContainer? _researchRows;
    private ScrollContainer? _researchScroll;
    private readonly double[] _focusRatePermillePerSecond = new double[3];
    private readonly double[] _focusObservedAt = new double[3];
    private readonly int[] _focusObservedProject = { -1, -1, -1 };
    private readonly short[] _focusObservedPermille = new short[3];
    private int _selectedFocusSlot;
    private int _selectedResearchProject = -1;
    private bool _researchWebVisible;
    private bool _leaningVisible;
    private bool _updatingResearchHud;

    private void CreateResearchHud(CanvasLayer layer)
    {
        _focusBar = new PanelContainer
        {
            Name = "FocusBar",
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = 14,
            OffsetRight = -14,
            OffsetTop = -164,
            OffsetBottom = -42,
            CustomMinimumSize = new Vector2(0, 122),
        };
        var focusStyle = new StyleBoxFlat { BgColor = new Color(0.075f, 0.09f, 0.09f, 0.94f) };
        focusStyle.SetCornerRadiusAll(6);
        focusStyle.SetContentMarginAll(8);
        _focusBar.AddThemeStyleboxOverride("panel", focusStyle);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 5);
        _focusBar.AddChild(root);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 10);
        _insightLabel = new Label { Text = "Insight —" };
        _insightLabel.AddThemeFontSizeOverride("font_size", 15);
        _insightLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        toolbar.AddChild(_insightLabel);

        var leaningButton = new Button { Text = "Leaning", TooltipText = "Set the Council's theme preference" };
        leaningButton.Pressed += ToggleLeaningPanel;
        toolbar.AddChild(leaningButton);
        _councilToggle = new CheckButton { Text = "Council" };
        _councilToggle.Toggled += enabled =>
        {
            if (!_updatingResearchHud && _session is not null && _current is not null)
                _session.Queue(PlayerCommands.SetCouncil(_session.Tick, _current.FocusCiv, enabled));
        };
        toolbar.AddChild(_councilToggle);
        _researchOpenButton = new Button { Text = "Research web  [T]" };
        _researchOpenButton.Pressed += () => SetResearchWebVisible(true);
        toolbar.AddChild(_researchOpenButton);
        root.AddChild(toolbar);

        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 8);
        int[] ratios = { 4, 2, 1 };
        string[] slotNames = { "PRIMARY · 4/7", "SECONDARY · 2/7", "TERTIARY · 1/7" };
        for (int slot = 0; slot < 3; slot++)
        {
            int capturedSlot = slot;
            var card = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = ratios[slot],
                CustomMinimumSize = new Vector2(0, 78),
            };
            var cardContent = new VBoxContainer();
            cardContent.AddThemeConstantOverride("separation", 1);
            card.AddChild(cardContent);
            var title = new Label { Text = slotNames[slot] };
            title.AddThemeFontSizeOverride("font_size", 11);
            title.AddThemeColorOverride("font_color", ResearchThemeColors[slot == 0 ? 0 : slot]);
            cardContent.AddChild(title);
            var project = new Button
            {
                Text = "Choose a project",
                Alignment = HorizontalAlignment.Left,
                Flat = true,
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            project.Pressed += () => OpenResearchForSlot(capturedSlot);
            cardContent.AddChild(project);
            _focusProjectLabels[slot] = project;
            var progress = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 9) };
            cardContent.AddChild(progress);
            _focusProgressBars[slot] = progress;
            var meta = new Label { Text = "Empty" };
            meta.AddThemeFontSizeOverride("font_size", 11);
            cardContent.AddChild(meta);
            _focusMetaLabels[slot] = meta;
            var queue = new Label { Text = "Queue empty" };
            queue.AddThemeFontSizeOverride("font_size", 10);
            queue.AddThemeColorOverride("font_color", new Color("#c7c1b1"));
            cardContent.AddChild(queue);
            _focusQueueLabels[slot] = queue;
            slots.AddChild(card);
        }
        root.AddChild(slots);
        layer.AddChild(_focusBar);

        CreateLeaningPanel(layer);
        CreateResearchWeb(layer);
        if (_current is not null) UpdateResearchHud(_current);
    }

    private void CreateLeaningPanel(CanvasLayer layer)
    {
        _leaningPanel = new PanelContainer
        {
            Name = "ResearchLeaning",
            AnchorLeft = 0.25f,
            AnchorRight = 0.72f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetTop = -292,
            OffsetBottom = -170,
            Visible = false,
        };
        var contents = new VBoxContainer();
        _leaningPanel.AddChild(contents);
        contents.AddChild(new Label { Text = "Council Leaning · shares add to 100%" });
        for (int theme = 0; theme < 3; theme++)
        {
            var row = new HBoxContainer();
            var title = new Label { Text = ResearchThemeNames[theme], CustomMinimumSize = new Vector2(72, 0) };
            title.AddThemeColorOverride("font_color", ResearchThemeColors[theme]);
            row.AddChild(title);
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            int captured = theme;
            slider.ValueChanged += value =>
            {
                if (_leaningEditValues[captured] is not null) _leaningEditValues[captured]!.Text = $"{value:0}%";
            };
            row.AddChild(slider);
            _leaningEditSliders[theme] = slider;
            var valueLabel = new Label { Text = "0%", CustomMinimumSize = new Vector2(42, 0) };
            row.AddChild(valueLabel);
            _leaningEditValues[theme] = valueLabel;
            contents.AddChild(row);
        }
        var actions = new HBoxContainer();
        var apply = new Button { Text = "Apply leaning" };
        apply.Pressed += ApplyLeanings;
        actions.AddChild(apply);
        var close = new Button { Text = "Close" };
        close.Pressed += ToggleLeaningPanel;
        actions.AddChild(close);
        contents.AddChild(actions);
        layer.AddChild(_leaningPanel);
    }

    private void CreateResearchWeb(CanvasLayer layer)
    {
        _researchDimmer = new ColorRect
        {
            Name = "ResearchDimmer",
            Color = new Color(0.035f, 0.045f, 0.055f, 0.78f),
            AnchorRight = 1,
            AnchorBottom = 1,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        layer.AddChild(_researchDimmer);
        _researchWebPanel = new PanelContainer
        {
            Name = "ResearchWeb",
            AnchorLeft = 0.025f,
            AnchorRight = 0.975f,
            AnchorTop = 0.045f,
            AnchorBottom = 0.955f,
            Visible = false,
        };
        var webStyle = new StyleBoxFlat { BgColor = new Color(0.07f, 0.085f, 0.09f, 0.98f) };
        webStyle.SetCornerRadiusAll(7);
        webStyle.SetContentMarginAll(8);
        _researchWebPanel.AddThemeStyleboxOverride("panel", webStyle);
        var outer = new MarginContainer();
        outer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddThemeConstantOverride("margin_left", 12);
        outer.AddThemeConstantOverride("margin_right", 12);
        outer.AddThemeConstantOverride("margin_top", 10);
        outer.AddThemeConstantOverride("margin_bottom", 10);
        _researchWebPanel.AddChild(outer);
        var contents = new VBoxContainer();
        contents.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        contents.AddThemeConstantOverride("separation", 7);
        outer.AddChild(contents);

        var header = new HBoxContainer();
        var title = new Label { Text = "Research web · Dawn to Roots" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        _researchSearch = new LineEdit { PlaceholderText = "Search name, ID, lineage or effect…", CustomMinimumSize = new Vector2(260, 0) };
        _researchSearch.TextChanged += _ => RefreshResearchGrid();
        header.AddChild(_researchSearch);
        _researchThemeFilter = NewResearchFilter(new[] { "All themes", "Craft", "Kin", "Reach", "Keystone" }, RefreshResearchGrid);
        header.AddChild(_researchThemeFilter);
        _researchTypeFilter = NewResearchFilter(new[] { "All types", "Discovery", "Method", "Structure", "Role", "Institution", "Wonder", "Keystone" }, RefreshResearchGrid);
        header.AddChild(_researchTypeFilter);
        _researchStateFilter = NewResearchFilter(new[] { "All visible states", "Glimpsed", "Available", "Slotted", "Completed", "Dormant" }, RefreshResearchGrid);
        header.AddChild(_researchStateFilter);
        var close = new Button { Text = "Close  [Esc]" };
        close.Pressed += () => SetResearchWebVisible(false);
        header.AddChild(close);
        contents.AddChild(header);

        var body = new HBoxContainer();
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 12);
        _researchScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _researchRows = new VBoxContainer();
        _researchRows.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _researchRows.AddThemeConstantOverride("separation", 2);
        BuildResearchRows();
        _researchScroll.AddChild(_researchRows);
        body.AddChild(_researchScroll);
        body.AddChild(BuildResearchInspector());
        contents.AddChild(body);
        _researchWebMessage = new Label { Text = "Select a node to review it and assign a slot.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        contents.AddChild(_researchWebMessage);
        layer.AddChild(_researchWebPanel);
    }

    private OptionButton NewResearchFilter(string[] names, Action changed)
    {
        var filter = new OptionButton { CustomMinimumSize = new Vector2(130, 0) };
        for (int i = 0; i < names.Length; i++) filter.AddItem(names[i], i);
        filter.ItemSelected += _ => changed();
        return filter;
    }

    private void BuildResearchRows()
    {
        if (_catalog is null || _researchRows is null) return;
        _researchRows.AddChild(BuildResearchColumnHeader());
        var keyRow = new HBoxContainer();
        keyRow.AddThemeConstantOverride("separation", 4);
        keyRow.AddChild(new Label { Text = "KEYSTONE", CustomMinimumSize = new Vector2(74, 0), VerticalAlignment = VerticalAlignment.Center });
        for (int era = 0; era < 3; era++)
            keyRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 });
        var keyNodes = new HFlowContainer { CustomMinimumSize = new Vector2(84, 26) };
        foreach (ProjectDef def in _catalog.Projects)
            if (def.IsKeystone && def.Era <= 2) keyNodes.AddChild(CreateResearchNode(def, true));
        keyRow.AddChild(keyNodes);
        _researchRows.AddChild(keyRow);
        for (int theme = 0; theme < 3; theme++)
        {
            var band = new Label { Text = ResearchThemeNames[theme].ToUpperInvariant(), CustomMinimumSize = new Vector2(0, 22) };
            band.AddThemeFontSizeOverride("font_size", 13);
            band.AddThemeColorOverride("font_color", ResearchThemeColors[theme]);
            _researchRows.AddChild(band);
            for (int lineage = 0; lineage < _catalog.LineageCodes.Count; lineage++)
            {
                if (!LineageBelongsToTheme(lineage, theme)) continue;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);
                var label = new Label { Text = _catalog.LineageCodes[lineage], CustomMinimumSize = new Vector2(74, 0), VerticalAlignment = VerticalAlignment.Center, TooltipText = $"{ResearchThemeNames[theme]} lineage" };
                label.AddThemeColorOverride("font_color", ResearchThemeColors[theme]);
                row.AddChild(label);
                for (int era = 0; era < 3; era++)
                {
                    var cell = new HFlowContainer
                    {
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                        SizeFlagsStretchRatio = 1,
                        CustomMinimumSize = new Vector2(0, 26),
                    };
                    foreach (ProjectDef def in _catalog.Projects)
                        if (def.Era == era && def.Lineage == lineage && !def.IsKeystone)
                            cell.AddChild(CreateResearchNode(def));
                    row.AddChild(cell);
                }
                row.AddChild(new Control { CustomMinimumSize = new Vector2(84, 0) });
                _researchRows.AddChild(row);
            }
        }
    }

    private Control BuildResearchColumnHeader()
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "LANE", CustomMinimumSize = new Vector2(74, 0) });
        string[] names = { "E0 · DAWN", "E1 · HUNTERS", "E2 · ROOTS", "KEY" };
        foreach (string name in names)
        {
            var label = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsStretchRatio = name == "KEY" ? 0.26f : 1 };
            label.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(label);
        }
        return row;
    }

    private bool LineageBelongsToTheme(int lineage, int theme)
    {
        if (_catalog is null) return false;
        foreach (ProjectDef def in _catalog.Projects)
            if (!def.IsKeystone && def.Lineage == lineage) return (int)def.Theme == theme;
        return false;
    }

    private Button CreateResearchNode(ProjectDef def, bool keystone = false)
    {
        var button = new Button
        {
            Text = def.Id,
            CustomMinimumSize = new Vector2(63, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            TooltipText = $"{def.Id} · {def.Name}",
            Visible = false,
        };
        button.AddThemeFontSizeOverride("font_size", 10);
        button.Pressed += () => SelectResearchProject(def.Index);
        if (keystone) _researchKeystoneButtons.Add(def.Index, button);
        else _researchProjectButtons.Add(def.Index, button);
        return button;
    }

    private Control BuildResearchInspector()
    {
        var inspector = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        var contents = new VBoxContainer();
        contents.AddThemeConstantOverride("separation", 6);
        inspector.AddChild(contents);
        _researchSelectionTitle = new Label { Text = "Choose a project", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _researchSelectionTitle.AddThemeFontSizeOverride("font_size", 17);
        contents.AddChild(_researchSelectionTitle);
        _researchSelectionText = new Label { Text = "Available nodes can go straight into a focus slot. Glimpsed nodes can be queued while you meet their last requirement.", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _researchSelectionText.AddThemeFontSizeOverride("font_size", 12);
        contents.AddChild(_researchSelectionText);
        _researchQueueButton = new Button { Text = "Add to queue" };
        _researchQueueButton.Pressed += QueueSelectedResearch;
        contents.AddChild(_researchQueueButton);
        _researchRemoveQueueButton = new Button { Text = "Remove from queue" };
        _researchRemoveQueueButton.Pressed += RemoveSelectedResearchFromQueue;
        contents.AddChild(_researchRemoveQueueButton);
        for (int slot = 0; slot < 3; slot++)
        {
            int captured = slot;
            string[] labels = { "Set as Primary", "Set as Secondary", "Set as Tertiary" };
            var button = new Button { Text = labels[slot] };
            button.Pressed += () => AssignSelectedResearch(captured);
            contents.AddChild(button);
            _researchSlotButtons[slot] = button;
        }
        return inspector;
    }

    private void SetResearchWebVisible(bool visible)
    {
        _researchWebVisible = visible;
        if (_researchDimmer is not null) _researchDimmer.Visible = visible;
        if (_researchWebPanel is not null) _researchWebPanel.Visible = visible;
        if (visible)
        {
            _leaningVisible = false;
            if (_leaningPanel is not null) _leaningPanel.Visible = false;
            RefreshResearchGrid();
            UpdateResearchSelection();
        }
    }

    private void OpenResearchForSlot(int slot)
    {
        _selectedFocusSlot = slot;
        if (_current is not null && (uint)slot < 3u) _selectedResearchProject = _current.FocusSlotProject[slot];
        SetResearchWebVisible(true);
    }

    private void SelectResearchProject(int project)
    {
        _selectedResearchProject = project;
        UpdateResearchSelection();
    }

    private void UpdateResearchHud(MatchFrame frame)
    {
        if (_catalog is null) return;
        ObserveFocusProgress(frame);
        _updatingResearchHud = true;
        long insightRaw = frame.FocusCiv >= 0 && frame.FocusCiv < frame.CivCount ? frame.CivInsightPerYear[frame.FocusCiv] : 0;
        string insight = FormatInsightPerYear(insightRaw);
        if (_insightLabel is not null) _insightLabel.Text = $"Insight · {insight}/year";
        if (_councilToggle is not null)
        {
            _councilToggle.ButtonPressed = frame.FocusCouncil;
            _councilToggle.Disabled = frame.FocusCiv != 0;
        }

        for (int slot = 0; slot < 3; slot++)
        {
            int project = frame.FocusSlotProject[slot];
            short permille = frame.FocusSlotPermille[slot];
            if (_focusProgressBars[slot] is not null) _focusProgressBars[slot]!.Value = permille / 10.0;
            if (_focusProjectLabels[slot] is not null)
            {
                Button label = _focusProjectLabels[slot]!;
                label.Text = project >= 0 && project < _catalog.Count
                    ? $"{_catalog.Projects[project].Id} · {_catalog.Projects[project].Name}"
                    : "Choose a project";
                label.TooltipText = project >= 0 && project < _catalog.Count ? _catalog.Projects[project].Flavor : "Open the research web to choose a project";
            }
            if (_focusMetaLabels[slot] is not null)
            {
                string meta = "Empty slot";
                if (project >= 0 && project < _catalog.Count)
                {
                    ProjectDef focus = _catalog.Projects[project];
                    string lane = focus.Lineage >= 0 ? _catalog.LineageCodes[focus.Lineage] : "Keystone";
                    meta = $"{lane} · {ResearchTypeNames[(int)focus.Type]} · {permille / 10.0:0.#}%" + FormatEta(slot, permille);
                }
                else if (frame.FocusCouncil) meta = "Elders will suggest a project";
                _focusMetaLabels[slot]!.Text = meta;
            }
            string next = slot == 0 ? FormatQueuePeek(frame) : frame.FocusQueueCount == 0 ? "Queue empty" : $"{frame.FocusQueueCount} queued";
            if (_focusQueueLabels[slot] is not null) _focusQueueLabels[slot]!.Text = next;
        }
        _updatingResearchHud = false;
        if (_researchWebVisible) RefreshResearchGrid();
        if (_selectedResearchProject >= 0) UpdateResearchSelection();
    }

    private string FormatQueuePeek(MatchFrame frame)
    {
        if (frame.FocusQueueCount <= 0 || _catalog is null) return "Queue empty";
        var peek = new StringBuilder("Next: ");
        int count = Math.Min(2, Math.Min(frame.FocusQueueCount, frame.FocusQueue.Length));
        for (int i = 0; i < count; i++)
        {
            int project = frame.FocusQueue[i];
            if ((uint)project >= (uint)_catalog.Count) continue;
            if (i > 0) peek.Append("  ·  ");
            peek.Append(_catalog.Projects[project].Id).Append(' ').Append(_catalog.Projects[project].Name);
        }
        return peek.ToString();
    }

    private void ObserveFocusProgress(MatchFrame frame)
    {
        double now = Stopwatch.GetTimestamp();
        for (int slot = 0; slot < 3; slot++)
        {
            int project = frame.FocusSlotProject[slot];
            short progress = frame.FocusSlotPermille[slot];
            if (project == _focusObservedProject[slot] && project >= 0 && progress > _focusObservedPermille[slot])
            {
                double elapsed = (now - _focusObservedAt[slot]) / Stopwatch.Frequency;
                if (elapsed > 0.05) _focusRatePermillePerSecond[slot] = (progress - _focusObservedPermille[slot]) / elapsed;
            }
            else if (project != _focusObservedProject[slot]) _focusRatePermillePerSecond[slot] = 0;
            if (project != _focusObservedProject[slot] || progress > _focusObservedPermille[slot])
            {
                _focusObservedProject[slot] = project;
                _focusObservedPermille[slot] = progress;
                _focusObservedAt[slot] = now;
            }
        }
    }

    private string FormatEta(int slot, short progress)
    {
        if (_session?.Paused == true) return " · paused";
        double rate = _focusRatePermillePerSecond[slot];
        if (rate <= 0.01) return " · ETA calibrating";
        double minutes = (1000 - progress) / rate / 60.0;
        return minutes >= 60 ? $" · ETA {minutes / 60.0:0.#}h" : $" · ETA {minutes:0.#}m";
    }

    private static string FormatInsightPerYear(long raw)
    {
        double value = raw / 4294967296.0;
        return value >= 1000 ? $"{value / 1000.0:0.#}k" : $"{value:0.#}";
    }

    private void RefreshResearchGrid()
    {
        if (!_researchWebVisible || _current is null || _catalog is null) return;
        int themeFilter = _researchThemeFilter?.Selected ?? 0;
        int typeFilter = _researchTypeFilter?.Selected ?? 0;
        int stateFilter = _researchStateFilter?.Selected ?? 0;
        string query = _researchSearch?.Text.Trim() ?? "";
        foreach ((int project, Button button) in _researchProjectButtons)
            UpdateResearchNodeButton(project, button, themeFilter, typeFilter, stateFilter, query);
        foreach ((int project, Button button) in _researchKeystoneButtons)
            UpdateResearchNodeButton(project, button, themeFilter, typeFilter, stateFilter, query);
    }

    private void UpdateResearchNodeButton(int project, Button button, int themeFilter, int typeFilter, int stateFilter, string query)
    {
        if (_current is null || _catalog is null || project >= _current.FocusProjectCount) return;
        ProjectDef def = _catalog.Projects[project];
        ProjectState state = (ProjectState)_current.FocusProjectState[project];
        bool visible = state != ProjectState.Hidden &&
                       (themeFilter == 0 || (int)def.Theme == themeFilter - 1) &&
                       (typeFilter == 0 || (int)def.Type == typeFilter - 1) &&
                       (stateFilter == 0 || (int)state == stateFilter);
        if (visible && query.Length > 0) visible = ResearchMatches(def, query);
        button.Visible = visible;
        if (!visible) return;
        int slot = FindFocusSlot(project);
        string marker = state switch
        {
            ProjectState.Glimpsed => "?",
            ProjectState.Slotted => slot >= 0 ? $"{slot + 1}·" : "◉",
            ProjectState.Completed => "✓",
            ProjectState.Dormant => "◒",
            _ => "",
        };
        button.Text = marker.Length == 0 ? def.Id : $"{marker}{def.Id}";
        Color color = state switch
        {
            ProjectState.Glimpsed => new Color("#c4b998"),
            ProjectState.Available => ResearchThemeColors[(int)def.Theme],
            ProjectState.Slotted => new Color("#fff1a1"),
            ProjectState.Completed => new Color("#9cbf99"),
            ProjectState.Dormant => new Color("#bd9a77"),
            _ => Colors.White,
        };
        button.AddThemeColorOverride("font_color", color);
        button.TooltipText = $"{def.Id} · {def.Name}\n{ResearchThemeNames[(int)def.Theme]} · {def.Type} · {ResearchStateNames[(int)state]}";
    }

    private bool ResearchMatches(ProjectDef def, string query)
    {
        if (_catalog is null) return false;
        string needle = query.ToLowerInvariant();
        bool Contains(string? text) => text is not null && text.ToLowerInvariant().Contains(needle, StringComparison.Ordinal);
        if (Contains(def.Id) || Contains(def.Name) || Contains(def.Flavor) || Contains(def.Moment) ||
            (def.Lineage >= 0 && Contains(_catalog.LineageCodes[def.Lineage])) || Contains(def.Type.ToString())) return true;
        foreach (EffectDef effect in def.Effects)
            if (Contains(effect.Id) || Contains(effect.Stat) || Contains(effect.Text) || Contains(effect.Action) || Contains(effect.Cause) || Contains(effect.Note)) return true;
        foreach (Gate gate in def.Gates)
            if ((uint)gate.Target < (uint)_catalog.GateTargets.Count && Contains(_catalog.GateTargets[gate.Target])) return true;
        return false;
    }

    private int FindFocusSlot(int project)
    {
        if (_current is null) return -1;
        for (int slot = 0; slot < 3; slot++) if (_current.FocusSlotProject[slot] == project) return slot;
        return -1;
    }

    private void UpdateResearchSelection()
    {
        if (_current is null || _catalog is null || _researchSelectionTitle is null || _researchSelectionText is null) return;
        int project = _selectedResearchProject;
        bool known = project >= 0 && project < _current.FocusProjectCount && (ProjectState)_current.FocusProjectState[project] != ProjectState.Hidden;
        if (!known)
        {
            _researchSelectionTitle.Text = "Choose a project";
            _researchSelectionText.Text = "Hidden projects stay outside the viewer's web. Select a visible node to review its prerequisites and expected changes.";
            SetResearchActionAvailability(false, false, false);
            return;
        }

        ProjectDef def = _catalog.Projects[project];
        ProjectState state = (ProjectState)_current.FocusProjectState[project];
        _researchSelectionTitle.Text = $"{def.Id} · {def.Name}";
        var text = new StringBuilder();
        text.Append(ResearchThemeNames[(int)def.Theme]).Append(" · ").Append(def.Type).Append(" · E").Append(def.Era).Append(" · ").Append(ResearchStateNames[(int)state]);
        if (def.Lineage >= 0) text.Append("\nLineage: ").Append(_catalog.LineageCodes[def.Lineage]);
        if (def.Requires.Length > 0)
        {
            text.Append("\n\nRequires: ");
            AppendProjectNames(text, def.Requires);
        }
        if (def.RequiresAny.Length > 0)
        {
            text.Append("\nOne of: ");
            AppendProjectNames(text, def.RequiresAny);
        }
        if (def.Effects.Length > 0)
        {
            text.Append("\n\nWhat changes");
            int shown = 0;
            foreach (EffectDef effect in def.Effects)
            {
                if (effect.Visual == VisibleChange.None && effect.Text is null) continue;
                text.Append("\n• ").Append(FormatEffect(effect));
                if (++shown == 5) break;
            }
            if (shown == 0) text.Append("\n• Enables the project's simulation effects.");
        }
        if (!string.IsNullOrWhiteSpace(def.Flavor)) text.Append("\n\n“").Append(def.Flavor).Append('”');
        _researchSelectionText.Text = text.ToString();

        bool assignable = state is ProjectState.Available or ProjectState.Slotted;
        bool queued = false;
        for (int i = 0; i < _current.FocusQueueCount; i++) queued |= _current.FocusQueue[i] == project;
        SetResearchActionAvailability(assignable, state is ProjectState.Glimpsed or ProjectState.Available, queued);
    }

    private void AppendProjectNames(StringBuilder text, int[] projects)
    {
        if (_catalog is null) return;
        for (int i = 0; i < projects.Length; i++)
        {
            if (i > 0) text.Append(", ");
            int index = projects[i];
            if ((uint)index < (uint)_catalog.Count) text.Append(_catalog.Projects[index].Id);
        }
    }

    private static string FormatEffect(EffectDef effect)
    {
        var text = new StringBuilder();
        text.Append(effect.Kind);
        if (!string.IsNullOrEmpty(effect.Action)) text.Append(" · ").Append(effect.Action);
        if (!string.IsNullOrEmpty(effect.Id)) text.Append(" · ").Append(effect.Id);
        if (!string.IsNullOrEmpty(effect.Stat)) text.Append(" · ").Append(effect.Stat);
        if (!string.IsNullOrEmpty(effect.Text)) text.Append(" · ").Append(effect.Text);
        if (effect.HasValue) text.Append(" · ").Append(effect.Value.ToString());
        return text.ToString();
    }

    private void SetResearchActionAvailability(bool assignable, bool queueable, bool queued)
    {
        if (_researchQueueButton is not null) _researchQueueButton.Disabled = !queueable || queued || (_current?.FocusQueueCount ?? 0) >= 8;
        if (_researchRemoveQueueButton is not null) _researchRemoveQueueButton.Disabled = !queued;
        for (int slot = 0; slot < 3; slot++) if (_researchSlotButtons[slot] is not null) _researchSlotButtons[slot]!.Disabled = !assignable;
    }

    private void UpdateResearchHudActions()
    {
        UpdateResearchSelection();
    }

    private void AssignSelectedResearch(int slot)
    {
        if (_session is null || _current is null || _selectedResearchProject < 0) return;
        _session.Queue(PlayerCommands.SetSlot(_session.Tick, _current.FocusCiv, slot, _selectedResearchProject));
        _selectedFocusSlot = slot;
        string[] slotNames = { "Primary", "Secondary", "Tertiary" };
        _researchWebMessage?.SetDeferred("text", $"{_catalog!.Projects[_selectedResearchProject].Id} sent to the {slotNames[slot]} slot.");
    }

    private void QueueSelectedResearch()
    {
        if (_session is null || _current is null || _selectedResearchProject < 0) return;
        _session.Queue(PlayerCommands.Enqueue(_session.Tick, _current.FocusCiv, _selectedResearchProject));
        _researchWebMessage?.SetDeferred("text", $"{_catalog!.Projects[_selectedResearchProject].Id} added to the Council queue.");
    }

    private void RemoveSelectedResearchFromQueue()
    {
        if (_session is null || _current is null || _selectedResearchProject < 0) return;
        for (int i = 0; i < _current.FocusQueueCount; i++)
            if (_current.FocusQueue[i] == _selectedResearchProject)
            {
                _session.Queue(PlayerCommands.Dequeue(_session.Tick, _current.FocusCiv, i));
                _researchWebMessage?.SetDeferred("text", $"{_catalog!.Projects[_selectedResearchProject].Id} removed from the queue.");
                break;
            }
    }

    private void ToggleLeaningPanel()
    {
        _leaningVisible = !_leaningVisible;
        if (_leaningPanel is not null) _leaningPanel.Visible = _leaningVisible;
        if (_leaningVisible && _current is not null)
            for (int i = 0; i < 3; i++)
            {
                if (_leaningEditSliders[i] is not null) _leaningEditSliders[i]!.Value = _current.FocusLeaning[i];
                if (_leaningEditValues[i] is not null) _leaningEditValues[i]!.Text = $"{_current.FocusLeaning[i]}%";
            }
    }

    private void ApplyLeanings()
    {
        if (_session is null || _current is null) return;
        var values = new int[3];
        int total = 0;
        for (int i = 0; i < 3; i++) { values[i] = (int)Math.Round(_leaningEditSliders[i]?.Value ?? 0); total += values[i]; }
        if (total == 0) { values[0] = 34; values[1] = 33; values[2] = 33; total = 100; }
        int assigned = 0;
        for (int i = 0; i < 3; i++)
        {
            values[i] = values[i] * 100 / total;
            assigned += values[i];
        }
        for (int i = 0; assigned < 100; i = (i + 1) % 3, assigned++) values[i]++;
        for (int i = 0; i < 3; i++) _session.Queue(PlayerCommands.SetLeaning(_session.Tick, _current.FocusCiv, i, values[i]));
        _leaningVisible = false;
        if (_leaningPanel is not null) _leaningPanel.Visible = false;
    }

    private bool HandleResearchInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return false;
        if (_researchWebVisible)
        {
            if (key.Keycode == Key.Escape) SetResearchWebVisible(false);
            else if ((key.Keycode is Key.Key1 or Key.Key2 or Key.Key3) && _selectedResearchProject >= 0)
                AssignSelectedResearch((int)key.Keycode - (int)Key.Key1);
            return true;
        }
        if (key.Keycode == Key.T)
        {
            SetResearchWebVisible(true);
            return true;
        }
        return false;
    }

    private static bool ReadResearchWebArgument()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (string.Equals(arg, "--research-web", StringComparison.Ordinal)) return true;
        return false;
    }
}
