using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    private readonly Dictionary<(int Lineage, int Era), ResearchOverviewCell> _researchOverviewCells = new();
    private readonly HSlider?[] _leaningEditSliders = new HSlider?[3];
    private readonly Label?[] _leaningEditValues = new Label?[3];
    private LineEdit? _researchSearch;
    private OptionButton? _researchThemeFilter, _researchTypeFilter, _researchStateFilter;
    private Label? _researchSelectionTitle, _researchSelectionText, _researchWebMessage;
    private VBoxContainer? _researchPrerequisiteLinks, _researchUnlockLinks;
    private Button? _researchOverviewButton, _researchLanesButton;
    private CheckButton? _researchHubEmphasis;
    private Button? _researchQueueButton, _researchRemoveQueueButton;
    private Label? _researchActionHint;
    private Button?[] _researchSlotButtons = new Button?[3];
    private VBoxContainer? _researchRows, _researchOverviewRows;
    private ScrollContainer? _researchScroll;
    private readonly double[] _focusRatePermillePerSecond = new double[3];
    private readonly double[] _focusObservedAt = new double[3];
    private readonly int[] _focusObservedProject = { -1, -1, -1 };
    private readonly short[] _focusObservedPermille = new short[3];
    private int _selectedFocusSlot;
    private int _selectedResearchProject = -1;
    private bool _researchWebVisible;
    private bool _researchOverviewMode;
    private bool _leaningVisible;
    private bool _updatingResearchHud;

    private sealed class ResearchOverviewCell
    {
        public Button Button = null!;
        public ColorRect[] Segments = new ColorRect[3];
    }

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
            var project = new ResearchProjectButton
            {
                Text = "Choose a project",
                ProjectIndex = -1,
                DropSlot = capturedSlot,
                CanAssignProject = CanAssignResearchProject,
                Alignment = HorizontalAlignment.Left,
                Flat = true,
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            project.Pressed += () => OpenResearchForSlot(capturedSlot);
            project.ProjectDropped += AssignResearchToSlot;
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
        _researchOverviewButton = new Button { Text = "W1 Overview", TooltipText = "Summarize known progress by lineage and era" };
        _researchOverviewButton.Pressed += () => SetResearchOverviewMode(true);
        header.AddChild(_researchOverviewButton);
        _researchLanesButton = new Button { Text = "W2 Lanes", TooltipText = "Browse visible research projects by lineage" };
        _researchLanesButton.Pressed += () => SetResearchOverviewMode(false);
        header.AddChild(_researchLanesButton);
        _researchHubEmphasis = new CheckButton
        {
            Text = "Hub size",
            ButtonPressed = true,
            TooltipText = "Size nodes by the number of currently known projects they lead to",
        };
        _researchHubEmphasis.Toggled += _ => RefreshResearchGrid();
        header.AddChild(_researchHubEmphasis);
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
        _researchOverviewRows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Visible = false };
        _researchOverviewRows.AddThemeConstantOverride("separation", 2);
        BuildResearchOverview();
        _researchScroll.AddChild(_researchOverviewRows);
        body.AddChild(_researchScroll);
        body.AddChild(BuildResearchInspector());
        contents.AddChild(body);
        _researchWebMessage = new Label
        {
            Text = "Choose an Available project and set it as Primary for 4/7 of Insight. Glimpsed projects can be queued until their prerequisites are met. You can change focus any time.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        contents.AddChild(_researchWebMessage);
        layer.AddChild(_researchWebPanel);
        SetResearchOverviewMode(false);
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

    private void BuildResearchOverview()
    {
        if (_catalog is null || _researchOverviewRows is null) return;
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "LANE", CustomMinimumSize = new Vector2(74, 0) });
        string[] eras = { "E0 · DAWN", "E1 · HUNTERS", "E2 · ROOTS" };
        foreach (string era in eras)
            header.AddChild(new Label { Text = era, HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _researchOverviewRows.AddChild(header);

        var keystones = new HBoxContainer();
        keystones.AddChild(new Label { Text = "KEYSTONE", CustomMinimumSize = new Vector2(74, 0), VerticalAlignment = VerticalAlignment.Center });
        for (int era = 0; era < 3; era++) keystones.AddChild(CreateResearchOverviewCell(-1, era).Button.GetParent<Control>());
        _researchOverviewRows.AddChild(keystones);

        for (int theme = 0; theme < 3; theme++)
        {
            var band = new Label { Text = ResearchThemeNames[theme].ToUpperInvariant(), CustomMinimumSize = new Vector2(0, 22) };
            band.AddThemeFontSizeOverride("font_size", 13);
            band.AddThemeColorOverride("font_color", ResearchThemeColors[theme]);
            _researchOverviewRows.AddChild(band);
            for (int lineage = 0; lineage < _catalog.LineageCodes.Count; lineage++)
            {
                if (!LineageBelongsToTheme(lineage, theme)) continue;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);
                var label = new Label { Text = _catalog.LineageCodes[lineage], CustomMinimumSize = new Vector2(74, 0), VerticalAlignment = VerticalAlignment.Center };
                label.AddThemeColorOverride("font_color", ResearchThemeColors[theme]);
                row.AddChild(label);
                for (int era = 0; era < 3; era++) row.AddChild(CreateResearchOverviewCell(lineage, era).Button.GetParent<Control>());
                _researchOverviewRows.AddChild(row);
            }
        }
    }

    private ResearchOverviewCell CreateResearchOverviewCell(int lineage, int era)
    {
        var cell = new ResearchOverviewCell();
        var contents = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddThemeConstantOverride("separation", 1);
        cell.Button = new Button
        {
            Text = "—",
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Disabled = true,
            TooltipText = "No currently visible projects",
        };
        cell.Button.Pressed += () => OpenResearchOverviewCell(lineage, era);
        contents.AddChild(cell.Button);
        var bar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 4), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        Color[] colors = { new Color("#8eb58b"), new Color("#d5ad5e"), new Color("#858477") };
        for (int i = 0; i < cell.Segments.Length; i++)
        {
            cell.Segments[i] = new ColorRect { Color = colors[i], Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bar.AddChild(cell.Segments[i]);
        }
        contents.AddChild(bar);
        _researchOverviewCells.Add((lineage, era), cell);
        return cell;
    }

    private void SetResearchOverviewMode(bool overview)
    {
        _researchOverviewMode = overview;
        if (_researchRows is not null) _researchRows.Visible = !overview;
        if (_researchOverviewRows is not null) _researchOverviewRows.Visible = overview;
        if (_researchOverviewButton is not null) _researchOverviewButton.Text = overview ? "● W1 Overview" : "W1 Overview";
        if (_researchLanesButton is not null) _researchLanesButton.Text = overview ? "W2 Lanes" : "● W2 Lanes";
        if (overview && _researchScroll is not null) _researchScroll.ScrollVertical = 0;
        RefreshResearchGrid();
    }

    private void OpenResearchOverviewCell(int lineage, int era)
    {
        if (_catalog is null || _current is null) return;
        int candidate = -1;
        int candidateRank = int.MaxValue;
        int themeFilter = _researchThemeFilter?.Selected ?? 0;
        int typeFilter = _researchTypeFilter?.Selected ?? 0;
        int stateFilter = _researchStateFilter?.Selected ?? 0;
        string query = _researchSearch?.Text.Trim() ?? "";
        foreach (ProjectDef def in _catalog.Projects)
        {
            if (def.Era != era || (lineage < 0 ? !def.IsKeystone : def.IsKeystone || def.Lineage != lineage)) continue;
            if (!IsResearchVisible(def, themeFilter, typeFilter, stateFilter, query)) continue;
            ProjectState state = (ProjectState)_current.FocusProjectState[def.Index];
            int rank = state switch
            {
                ProjectState.Available => 0,
                ProjectState.Slotted => 1,
                ProjectState.Glimpsed => 2,
                ProjectState.Completed => 3,
                ProjectState.Dormant => 4,
                _ => int.MaxValue,
            };
            if (rank < candidateRank) { candidate = def.Index; candidateRank = rank; }
        }
        if (candidate < 0) return;
        _selectedResearchProject = candidate;
        SetResearchOverviewMode(false);
        UpdateResearchSelection();
        Dictionary<int, Button> buttons = _researchProjectButtons;
        if (lineage < 0) buttons = _researchKeystoneButtons;
        if (_researchScroll is not null && buttons.TryGetValue(candidate, out Button? button))
            _researchScroll.EnsureControlVisible(button);
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
        var button = new ResearchProjectButton
        {
            Text = def.Id,
            ProjectIndex = def.Index,
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
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        inspector.AddChild(scroll);
        var contents = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(contents);
        _researchSelectionTitle = new Label { Text = "Choose a project", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _researchSelectionTitle.AddThemeFontSizeOverride("font_size", 17);
        contents.AddChild(_researchSelectionTitle);
        var primaryButton = new Button { Text = "Set as Primary · 4/7", CustomMinimumSize = new Vector2(0, 36), TooltipText = "Give this project the largest share of your civilization's Insight." };
        primaryButton.AddThemeColorOverride("font_color", new Color("#f4d58d"));
        primaryButton.Pressed += () => AssignSelectedResearch(0);
        contents.AddChild(primaryButton);
        _researchSlotButtons[0] = primaryButton;
        var secondaryTertiary = new HBoxContainer();
        secondaryTertiary.AddThemeConstantOverride("separation", 6);
        string[] labels = { "", "Secondary · 2/7", "Tertiary · 1/7" };
        for (int slot = 1; slot < 3; slot++)
        {
            int captured = slot;
            var button = new Button { Text = labels[slot], SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            button.Pressed += () => AssignSelectedResearch(captured);
            secondaryTertiary.AddChild(button);
            _researchSlotButtons[slot] = button;
        }
        contents.AddChild(secondaryTertiary);
        _researchActionHint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _researchActionHint.AddThemeFontSizeOverride("font_size", 11);
        contents.AddChild(_researchActionHint);
        _researchQueueButton = new Button { Text = "Queue for later", TooltipText = "Keep this idea in line for a future open focus slot." };
        _researchQueueButton.Pressed += QueueSelectedResearch;
        contents.AddChild(_researchQueueButton);
        _researchRemoveQueueButton = new Button { Text = "Remove from queue" };
        _researchRemoveQueueButton.Pressed += RemoveSelectedResearchFromQueue;
        contents.AddChild(_researchRemoveQueueButton);
        _researchSelectionText = new Label { Text = "Available nodes can go straight into a focus slot. Glimpsed nodes can be queued until their prerequisites are met.", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _researchSelectionText.AddThemeFontSizeOverride("font_size", 12);
        contents.AddChild(_researchSelectionText);
        contents.AddChild(new Label { Text = "Prerequisite path" });
        _researchPrerequisiteLinks = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddChild(_researchPrerequisiteLinks);
        contents.AddChild(new Label { Text = "Known projects unlocked" });
        _researchUnlockLinks = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contents.AddChild(_researchUnlockLinks);
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
            if (_selectedResearchProject < 0 || _current is null || _selectedResearchProject >= _current.FocusProjectCount ||
                (ProjectState)_current.FocusProjectState[_selectedResearchProject] == ProjectState.Hidden)
                _selectedResearchProject = FindDefaultResearchProject();
            RefreshResearchGrid();
            UpdateResearchSelection();
        }
    }

    private int FindDefaultResearchProject()
    {
        if (_current is null || _catalog is null) return -1;
        int glimpsed = -1;
        for (int project = 0; project < Math.Min(_current.FocusProjectCount, _catalog.Count); project++)
        {
            ProjectState state = (ProjectState)_current.FocusProjectState[project];
            if (state is ProjectState.Available or ProjectState.Slotted) return project;
            if (state == ProjectState.Glimpsed && glimpsed < 0) glimpsed = project;
        }
        return glimpsed;
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
        if (_researchScroll is not null)
        {
            if (_researchKeystoneButtons.TryGetValue(project, out Button? keystone)) _researchScroll.EnsureControlVisible(keystone);
            else if (_researchProjectButtons.TryGetValue(project, out Button? button)) _researchScroll.EnsureControlVisible(button);
        }
    }

    private void NavigateResearchPath(int project)
    {
        if (_researchOverviewMode) SetResearchOverviewMode(false);
        SelectResearchProject(project);
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
            if (_focusProjectLabels[slot] is ResearchProjectButton dragButton) dragButton.ProjectIndex = project;
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
        int[] visibleDependents = CountVisibleDependents();
        foreach ((int project, Button button) in _researchProjectButtons)
            UpdateResearchNodeButton(project, button, themeFilter, typeFilter, stateFilter, query, visibleDependents[project]);
        foreach ((int project, Button button) in _researchKeystoneButtons)
            UpdateResearchNodeButton(project, button, themeFilter, typeFilter, stateFilter, query, visibleDependents[project]);
        RefreshResearchOverview(themeFilter, typeFilter, stateFilter, query);
    }

    private bool IsResearchVisible(ProjectDef def, int themeFilter, int typeFilter, int stateFilter, string query)
    {
        if (_current is null || def.Index >= _current.FocusProjectCount) return false;
        ProjectState state = (ProjectState)_current.FocusProjectState[def.Index];
        return state != ProjectState.Hidden &&
               (themeFilter == 0 || (int)def.Theme == themeFilter - 1) &&
               (typeFilter == 0 || (int)def.Type == typeFilter - 1) &&
               (stateFilter == 0 || (int)state == stateFilter) &&
               (query.Length == 0 || ResearchMatches(def, query));
    }

    private void UpdateResearchNodeButton(int project, Button button, int themeFilter, int typeFilter, int stateFilter, string query, int visibleDependents)
    {
        if (_current is null || _catalog is null || project >= _current.FocusProjectCount) return;
        ProjectDef def = _catalog.Projects[project];
        ProjectState state = (ProjectState)_current.FocusProjectState[project];
        bool visible = IsResearchVisible(def, themeFilter, typeFilter, stateFilter, query);
        button.Visible = visible;
        if (!visible) return;
        bool emphasize = _researchHubEmphasis?.ButtonPressed ?? true;
        float width = emphasize ? 63 + Math.Min(48, visibleDependents * 4) : 63;
        button.CustomMinimumSize = new Vector2(width, 24);
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
        string hub = visibleDependents > 0 ? $"\nKnown visible dependents: {visibleDependents}" : "";
        button.TooltipText = $"{def.Id} · {def.Name}\n{ResearchThemeNames[(int)def.Theme]} · {def.Type} · {ResearchStateNames[(int)state]}{hub}";
    }

    private int[] CountVisibleDependents()
    {
        if (_current is null || _catalog is null) return Array.Empty<int>();
        int count = _catalog.Count;
        int visibleCount = Math.Min(_current.FocusProjectCount, count);
        var dependents = new int[count];
        var countedBy = new int[count];
        for (int dependent = 0; dependent < visibleCount; dependent++)
        {
            if ((ProjectState)_current.FocusProjectState[dependent] == ProjectState.Hidden) continue;
            int stamp = dependent + 1;
            ProjectDef def = _catalog.Projects[dependent];
            foreach (int prerequisite in def.Requires) AddVisibleDependent(prerequisite, stamp, countedBy, dependents);
            foreach (int prerequisite in def.RequiresAny) AddVisibleDependent(prerequisite, stamp, countedBy, dependents);
        }
        return dependents;
    }

    private static void AddVisibleDependent(int prerequisite, int stamp, int[] countedBy, int[] dependents)
    {
        if ((uint)prerequisite >= (uint)dependents.Length || countedBy[prerequisite] == stamp) return;
        countedBy[prerequisite] = stamp;
        dependents[prerequisite]++;
    }

    private void RefreshResearchOverview(int themeFilter, int typeFilter, int stateFilter, string query)
    {
        if (_current is null || _catalog is null) return;
        var counts = new Dictionary<(int Lineage, int Era), int[]>();
        foreach (ProjectDef def in _catalog.Projects)
        {
            if (!IsResearchVisible(def, themeFilter, typeFilter, stateFilter, query)) continue;
            var key = (def.IsKeystone ? -1 : def.Lineage, def.Era);
            if (!counts.TryGetValue(key, out int[]? stateCounts)) counts.Add(key, stateCounts = new int[3]);
            switch ((ProjectState)_current.FocusProjectState[def.Index])
            {
                case ProjectState.Completed:
                case ProjectState.Dormant: stateCounts[0]++; break;
                case ProjectState.Available:
                case ProjectState.Slotted: stateCounts[1]++; break;
                case ProjectState.Glimpsed: stateCounts[2]++; break;
            }
        }

        foreach (KeyValuePair<(int Lineage, int Era), ResearchOverviewCell> pair in _researchOverviewCells)
        {
            int lineage = pair.Key.Lineage, era = pair.Key.Era;
            ResearchOverviewCell cell = pair.Value;
            counts.TryGetValue((lineage, era), out int[]? values);
            int completed = values?[0] ?? 0, available = values?[1] ?? 0, glimpsed = values?[2] ?? 0;
            int total = completed + available + glimpsed;
            cell.Button.Disabled = total == 0;
            cell.Button.Text = total == 0 ? "—" : $"✓ {completed}   ◇ {available}   ? {glimpsed}";
            cell.Button.TooltipText = total == 0
                ? "No currently visible projects in this cell"
                : $"Known projects only: {completed} completed · {available} available or slotted · {glimpsed} glimpsed. Hidden projects are not counted. Click to open this lane.";
            int[] segments = { completed, available, glimpsed };
            for (int i = 0; i < segments.Length; i++)
            {
                cell.Segments[i].Visible = segments[i] > 0;
                cell.Segments[i].SizeFlagsStretchRatio = Math.Max(0.1f, segments[i]);
            }
        }
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
            if (_researchActionHint is not null) _researchActionHint.Text = "Select an Available project to set a focus slot.";
            ClearResearchLinks(_researchPrerequisiteLinks);
            ClearResearchLinks(_researchUnlockLinks);
            SetResearchActionAvailability(false, false, false);
            return;
        }

        ProjectDef def = _catalog.Projects[project];
        ProjectState state = (ProjectState)_current.FocusProjectState[project];
        _researchSelectionTitle.Text = $"{def.Id} · {def.Name}";
        if (_researchActionHint is not null)
            _researchActionHint.Text = state switch
            {
                ProjectState.Available => "Available now — set it as Primary for the largest share of Insight.",
                ProjectState.Slotted => "Already in a focus slot. Choose another rank here to move it.",
                ProjectState.Glimpsed => "Not ready for a focus slot yet. Complete its prerequisites; queue it to wait for an open slot.",
                _ => "This project cannot be assigned to a focus slot right now.",
            };
        var text = new StringBuilder();
        text.Append(ResearchThemeNames[(int)def.Theme]).Append(" · ").Append(def.Type).Append(" · E").Append(def.Era).Append(" · ").Append(ResearchStateNames[(int)state]);
        if (def.Lineage >= 0) text.Append("\nLineage: ").Append(_catalog.LineageCodes[def.Lineage]);
        if (def.Effects.Length > 0)
        {
            text.Append("\n\nExpected changes");
            int shown = 0;
            foreach (EffectDef effect in def.Effects)
            {
                if (effect.Visual == VisibleChange.None && effect.Text is null) continue;
                text.Append("\n• ").Append(effect.Kind == EffectKind.Method ? FormatMethodImpact(effect) : FormatVisibleChange(effect));
                if (++shown == 5) break;
            }
            if (shown == 0) text.Append("\n• Enables the project's simulation effects.");
        }
        if (def.Gates.Length > 0)
        {
            text.Append("\n\nConditions");
            foreach (Gate gate in def.Gates)
            {
                string target = (uint)gate.Target < (uint)_catalog.GateTargets.Count ? _catalog.GateTargets[gate.Target] : "known condition";
                text.Append("\n• ").Append(gate.Kind switch { GateKind.Seen => "See", GateKind.Has => "Have", GateKind.Near => "Near", GateKind.Climate => "Climate", _ => "Meet" }).Append(' ').Append(target);
            }
        }
        if (!string.IsNullOrWhiteSpace(def.Flavor)) text.Append("\n\n“").Append(def.Flavor).Append('”');
        _researchSelectionText.Text = text.ToString();
        BuildResearchPathLinks(def);

        bool assignable = state is ProjectState.Available or ProjectState.Slotted;
        bool queued = false;
        for (int i = 0; i < _current.FocusQueueCount; i++) queued |= _current.FocusQueue[i] == project;
        SetResearchActionAvailability(assignable, state is ProjectState.Glimpsed or ProjectState.Available, queued);
    }

    private void BuildResearchPathLinks(ProjectDef selected)
    {
        ClearResearchLinks(_researchPrerequisiteLinks);
        ClearResearchLinks(_researchUnlockLinks);
        if (_catalog is null || _current is null) return;

        bool hasPrerequisite = false;
        bool unrevealedRequired = false;
        foreach (int prerequisite in selected.Requires)
        {
            hasPrerequisite = true;
            if ((uint)prerequisite >= (uint)_current.FocusProjectCount || (ProjectState)_current.FocusProjectState[prerequisite] == ProjectState.Hidden)
                unrevealedRequired = true;
            else AddKnownResearchPath(prerequisite, _researchPrerequisiteLinks, "");
        }
        if (unrevealedRequired) AddResearchPathNote(_researchPrerequisiteLinks, "Unrevealed requirement");
        bool knownAlternative = false;
        foreach (int prerequisite in selected.RequiresAny)
        {
            if ((uint)prerequisite >= (uint)_current.FocusProjectCount || (ProjectState)_current.FocusProjectState[prerequisite] == ProjectState.Hidden) continue;
            hasPrerequisite = true;
            knownAlternative = true;
            AddKnownResearchPath(prerequisite, _researchPrerequisiteLinks, "");
        }
        if (selected.RequiresAny.Length > 0 && !knownAlternative && selected.Requires.Length == 0)
        {
            hasPrerequisite = true;
            AddResearchPathNote(_researchPrerequisiteLinks, "Prerequisite routes not revealed yet");
        }
        if (!hasPrerequisite) AddResearchPathNote(_researchPrerequisiteLinks, "No earlier project prerequisite");

        int added = 0, more = 0;
        foreach (ProjectDef candidate in _catalog.Projects)
        {
            if (candidate.Index >= _current.FocusProjectCount || candidate.Index == selected.Index ||
                (ProjectState)_current.FocusProjectState[candidate.Index] == ProjectState.Hidden ||
                !DependsOn(candidate, selected.Index)) continue;
            if (added < 8) { AddKnownResearchPath(candidate.Index, _researchUnlockLinks, ""); added++; }
            else more++;
        }
        if (added == 0) AddResearchPathNote(_researchUnlockLinks, "No known direct unlocks");
        else if (more > 0) AddResearchPathNote(_researchUnlockLinks, $"+{more} more known");
    }

    private static bool DependsOn(ProjectDef candidate, int prerequisite)
    {
        foreach (int required in candidate.Requires) if (required == prerequisite) return true;
        foreach (int required in candidate.RequiresAny) if (required == prerequisite) return true;
        return false;
    }

    private void AddKnownResearchPath(int project, VBoxContainer? container, string hiddenLabel)
    {
        if (_catalog is null || _current is null || container is null) return;
        if ((uint)project >= (uint)_current.FocusProjectCount || (ProjectState)_current.FocusProjectState[project] == ProjectState.Hidden)
        {
            if (hiddenLabel.Length > 0) AddResearchPathNote(container, hiddenLabel);
            return;
        }
        ProjectDef def = _catalog.Projects[project];
        var button = new Button { Text = def.Id, TooltipText = $"Open {def.Id} · {def.Name}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", 10);
        button.AddThemeColorOverride("font_color", ResearchThemeColors[(int)def.Theme]);
        button.Pressed += () => NavigateResearchPath(project);
        container.AddChild(button);
    }

    private static void AddResearchPathNote(VBoxContainer? container, string note)
    {
        if (container is not null) container.AddChild(new Label { Text = note, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
    }

    private static void ClearResearchLinks(VBoxContainer? container)
    {
        if (container is null) return;
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string FormatVisibleChange(EffectDef effect)
    {
        string target = effect.Action ?? effect.Id ?? effect.Stat ?? effect.Text ?? effect.Kind.ToString();
        double value = effect.Value.ToDoubleForDisplay();
        if (effect.Kind == EffectKind.Mod)
        {
            string stat = effect.Stat ?? effect.Id ?? effect.Action ?? "behavior";
            return effect.Op switch
            {
                "pct" => $"{stat}: {value * 100:+0.##;-0.##;0}%",
                "mult" => $"{stat}: multiplier ×{value.ToString("0.##", CultureInfo.InvariantCulture)}",
                "flat" => $"{stat}: {value:+0.##;-0.##;0} flat",
                "set" => $"{stat}: set to {value.ToString("0.##", CultureInfo.InvariantCulture)}",
                _ => FormatEffect(effect),
            };
        }
        if (effect.Kind == EffectKind.Hazard)
        {
            string cause = effect.Cause ?? effect.Id ?? "hazard";
            return $"{cause} risk ×{value.ToString("0.##", CultureInfo.InvariantCulture)}";
        }
        return effect.Visual switch
        {
            VisibleChange.NewAction => $"Unlocks {target}",
            VisibleChange.ToolOrMethod => $"Improves {target} methods",
            VisibleChange.Good => $"Adds {target}",
            VisibleChange.Structure => $"Enables {target}",
            VisibleChange.Role => $"Adds the {target} role",
            VisibleChange.Tier => $"Advances {target}",
            VisibleChange.Overlay => $"Opens {target} on the map",
            VisibleChange.EraCelebration => "Advances the era",
            _ => FormatEffect(effect),
        };
    }

    private string FormatMethodImpact(EffectDef effect)
    {
        string action = effect.Action ?? "work";
        int rung = (int)effect.Int;
        if (_current is null || _current.FocusCiv < 0 || !TryGetActivity(action, out FigureActivity activity))
            return $"Adds the {action} method rung {rung}";

        int figures = 0, belowRung = 0;
        for (int i = 0; i < _current.FigureCount; i++)
        {
            if (_current.FigureCiv[i] != _current.FocusCiv || (FigureActivity)_current.FigureActivity[i] != activity) continue;
            figures++;
            if (_current.FigureToolRung[i] < rung) belowRung++;
        }
        if (figures == 0) return $"Adds the {action} method rung {rung}; no current figures are doing this action.";
        return $"Adds the {action} method rung {rung}; {figures} current figures do this action, {belowRung} are below this rung at their settlement.";
    }

    private static bool TryGetActivity(string action, out FigureActivity activity)
    {
        activity = action switch
        {
            "forage" => FigureActivity.Forage,
            "hunt" => FigureActivity.Hunt,
            "fish" => FigureActivity.Fish,
            "farm" => FigureActivity.Farm,
            "chop" => FigureActivity.Chop,
            "gather" => FigureActivity.Gather,
            "craft" => FigureActivity.Craft,
            "build" => FigureActivity.Build,
            "preserve" => FigureActivity.Preserve,
            "tend_fire" => FigureActivity.TendFire,
            "herd" => FigureActivity.Herd,
            _ => (FigureActivity)byte.MaxValue,
        };
        return (byte)activity != byte.MaxValue;
    }

    private static string FormatEffect(EffectDef effect)
    {
        var text = new StringBuilder();
        text.Append(effect.Kind);
        if (!string.IsNullOrEmpty(effect.Action)) text.Append(" · ").Append(effect.Action);
        if (!string.IsNullOrEmpty(effect.Id)) text.Append(" · ").Append(effect.Id);
        if (!string.IsNullOrEmpty(effect.Stat)) text.Append(" · ").Append(effect.Stat);
        if (!string.IsNullOrEmpty(effect.Text)) text.Append(" · ").Append(effect.Text);
        if (effect.HasValue) text.Append(" · ").Append(effect.Value.ToDoubleForDisplay().ToString("0.###", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private void SetResearchActionAvailability(bool assignable, bool queueable, bool queued)
    {
        if (_researchQueueButton is not null) _researchQueueButton.Disabled = !queueable || queued || (_current?.FocusQueueCount ?? 0) >= 8;
        if (_researchRemoveQueueButton is not null) _researchRemoveQueueButton.Disabled = !queued;
        for (int slot = 0; slot < 3; slot++)
            if (_researchSlotButtons[slot] is not null)
            {
                _researchSlotButtons[slot]!.Disabled = !assignable;
                _researchSlotButtons[slot]!.TooltipText = assignable
                    ? $"Assign this project to the {(slot == 0 ? "Primary" : slot == 1 ? "Secondary" : "Tertiary")} focus slot."
                    : "Only Available or already slotted projects can be assigned to a focus slot.";
            }
    }

    private void UpdateResearchHudActions()
    {
        UpdateResearchSelection();
    }

    private void AssignSelectedResearch(int slot)
    {
        AssignResearchToSlot(_selectedResearchProject, slot);
    }

    private bool CanAssignResearchProject(int project)
        => _session is not null && _current is not null && project >= 0 && project < _current.FocusProjectCount &&
           (ProjectState)_current.FocusProjectState[project] is ProjectState.Available or ProjectState.Slotted;

    private void AssignResearchToSlot(int project, int slot)
    {
        if (_session is null || _current is null || (uint)slot >= 3u || !CanAssignResearchProject(project)) return;
        _session.Queue(PlayerCommands.SetSlot(_session.Tick, _current.FocusCiv, slot, project));
        _selectedResearchProject = project;
        _selectedFocusSlot = slot;
        string[] slotNames = { "Primary", "Secondary", "Tertiary" };
        _researchWebMessage?.SetDeferred("text", $"{_catalog!.Projects[project].Id} sent to the {slotNames[slot]} slot. Drag a slotted project to another slot to change its rank.");
        UpdateResearchSelection();
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

    private static bool ReadResearchOverviewArgument()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (string.Equals(arg, "--research-overview", StringComparison.Ordinal)) return true;
        return false;
    }
}
