using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;
using ResearchTheme = CivSim.Sim.Research.Theme;

namespace CivSim.Game;

/// <summary>
/// The research web: a "Choose" view of what is ready now and a "Map" view of every known field, with one inspector.
/// Click to look, click the big button to start. docs/MVP/20 §3.4 and §4.4.
/// </summary>
public partial class Game
{
    private const int ViewChoose = 0, ViewMap = 1;

    private PanelContainer? _webPanel;
    private ColorRect? _webScrim;
    private bool _researchWebVisible;
    private UiLayer? _webLayer;
    private UiSegmented? _webViewSwitch;
    private readonly UiButton[] _themeChips = new UiButton[4];
    private LineEdit? _webSearch;
    private int _webView, _webThemeFilter = -1;
    private int _selectedResearchProject = -1, _webTarget = -1, _backProject = -1;
    private bool _replaceRowOpen;

    // slot strip
    private sealed class StripChip
    {
        public Control Root = null!;
        public UiRankBadge Badge = null!;
        public Label Name = null!, Eta = null!, Target = null!;
        public UiProgressBar Bar = null!;
        public UiFrame Frame = null!;
    }
    private readonly StripChip[] _strip = new StripChip[3];
    private UiButton? _queueChip;

    // canvas
    private ScrollContainer? _canvasScroll;
    private VBoxContainer? _chooseView, _mapView;
    private PanelContainer? _noticeBar;
    private Label? _noticeLabel;
    private UiButton? _noticeButton;
    private int _noticeSlot = -1;
    private float _noticeTimer;
    private Label? _readyHeader, _almostHeader, _moreLabel, _noResultsLabel;
    private UiButton? _showAllAlmost;
    private HBoxContainer? _keystoneRow;
    private readonly VBoxContainer?[] _readyColumns = new VBoxContainer?[3];
    private readonly Label?[] _columnCounts = new Label?[3];
    private readonly Control?[] _columnEmpty = new Control?[3];
    private readonly List<UiProjectCard>[] _cardPools = { new(), new(), new(), new() };
    private readonly List<UiProjectCard> _rowPool = new();
    private VBoxContainer? _almostList, _searchList, _readyBlock;
    private bool _showAllAlmostRows;
    private readonly List<UiProjectCard> _navCards = new();
    private bool[] _agePath = Array.Empty<bool>();
    private bool[] _breadthPath = Array.Empty<bool>();

    // map view
    private bool _mapBuilt;
    private readonly Dictionary<int, UiProjectCard> _chips = new();
    private readonly List<(Control Row, int Lineage, int Theme)> _mapRows = new();
    private readonly List<Control> _mapEraHeaders = new();
    private readonly List<(HFlowContainer Cell, int Era, Control Row)> _mapCells = new();
    private readonly List<(Label Header, int Theme)> _mapBandHeaders = new();
    private HFlowContainer? _mapKeystoneCell;
    private Control? _mapKeystoneRow;
    private int[][] _dependents = Array.Empty<int[]>();

    // inspector
    private ScrollContainer? _inspectorScroll;
    private VBoxContainer? _inspector;
    private UiButton? _backLink;
    private HBoxContainer? _inspectorChips;
    private PanelContainer? _chipTheme, _chipAge, _chipType;
    private Label? _inspName, _inspState, _inspWhy, _inspEstimate, _inspSeeText, _inspFlavor, _inspChanges, _inspEmpty;
    private Label? _peopleProjectContextText;
    private UiButton? _ctaButton;
    private VBoxContainer? _orRow;
    private Label? _orLabel;
    private readonly UiButton[] _orButtons = new UiButton[3];
    private VBoxContainer? _replaceRow;
    private readonly UiButton[] _replaceChips = new UiButton[3];
    private Control? _changesBlock, _peopleProjectContext, _seeBlock, _needsBlock, _leadsBlock;
    private VBoxContainer? _needsList, _needsAnyList;
    private readonly List<UiProjectCard> _needsAnyPool = new();
    private HFlowContainer? _leadsList;
    private readonly List<UiProjectCard> _needsPool = new(), _leadsPool = new();
    private Label? _needsHeaderAll, _needsHeaderAny, _needsNote, _needsKeystone;
    private UiProgressBar? _needsKeystoneBar;
    private int _inspSignature = int.MinValue;
    private int _webSignature = int.MinValue;
    private Label? _footerNote;
    private float _footerNoteTimer;
    private UiToggle? _webAutoFill;
    private UiPopover? _queuePopover, _leaningPopover;
    private UiLayer? _queueLayer, _leaningLayer;
    private VBoxContainer? _queueList;
    private readonly HSlider?[] _leaningSliders = new HSlider?[3];
    private readonly Label?[] _leaningValues = new Label?[3];

    private static bool ReadResearchWebArgument() => HasArgument("--research-web");

    // ---- build -------------------------------------------------------------------------------

    private void CreateResearchWeb()
    {
        if (_catalog is null) return;
        _webScrim = new ColorRect { Color = UiTokens.ScrimWeb, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _webScrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _webScrim.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) CloseResearchWeb();
        };
        _hudRoot!.AddChild(_webScrim);

        _webPanel = UiKit.Panel("SheetPanel", "ResearchWeb");
        _webPanel.Visible = false;
        var root = UiKit.Column(UiTokens.Sp3);
        _webPanel.AddChild(root);

        // Header
        var header = UiKit.Row(UiTokens.Sp3);
        header.AddChild(UiKit.Text("Research", "Title"));
        _webViewSwitch = new UiSegmented(new[] { "Choose", "Map" }, 0, SetResearchView, 76);
        _webViewSwitch.SetTooltip(0, "Projects you can start now");
        _webViewSwitch.SetTooltip(1, "Every field you know, by age");
        _webViewSwitch.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(_webViewSwitch);
        var chips = UiKit.Row(UiTokens.Sp2);
        chips.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        string[] chipNames = { "All", "Craft", "Kin", "Reach" };
        for (int i = 0; i < 4; i++)
        {
            int filter = i - 1;
            UiGlyph glyph = i == 0 ? UiGlyph.None : UiDraw.ThemeGlyph((ResearchTheme)(i - 1));
            UiButton chip = (UiButton)UiKit.InteractiveChip(chipNames[i], glyph, () => SetThemeFilter(filter),
                i == 0 ? "Show every theme" : UiText.ThemePurpose[i - 1]);
            if (glyph != UiGlyph.None) chip.GlyphColor = UiTokens.ThemeColor((ResearchTheme)(i - 1));
            chip.ButtonPressed = i == 0;
            _themeChips[i] = chip;
            chips.AddChild(chip);
        }
        header.AddChild(chips);
        header.AddChild(UiKit.Spacer(expand: true));
        _webSearch = new LineEdit
        {
            PlaceholderText = "Search projects: fire, fish, tools",
            CustomMinimumSize = new Vector2(300, UiTokens.ControlS),
            ClearButtonEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _webSearch.TextChanged += _ => { _webSignature = int.MinValue; RefreshResearchWebNow(); };
        header.AddChild(_webSearch);
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (Esc)", CloseResearchWeb));
        root.AddChild(header);

        // Slot strip
        var stripRow = UiKit.Row(UiTokens.Sp2);
        float[] ratios = { 1.0f, 0.618f, 0.382f };
        for (int slot = 0; slot < 3; slot++)
        {
            StripChip chip = BuildStripChip(slot);
            chip.Root.SizeFlagsStretchRatio = ratios[slot];
            _strip[slot] = chip;
            stripRow.AddChild(chip.Root);
        }
        _queueChip = UiKit.Secondary("Queue", ToggleQueuePopover, "Projects waiting for a free slot", UiGlyph.Queue, 55);
        _queueChip.CustomMinimumSize = new Vector2(144, 55);
        stripRow.AddChild(_queueChip);
        root.AddChild(stripRow);

        // Body
        var body = UiKit.Row(UiTokens.Sp4);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var canvasColumn = UiKit.Column(UiTokens.Sp2);
        canvasColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        canvasColumn.SizeFlagsStretchRatio = 0.618f;
        canvasColumn.CustomMinimumSize = new Vector2(360, 0);
        _noticeBar = UiKit.Panel("ChipPanelSuccess", null);
        _noticeBar.Visible = false;
        var noticeRow = UiKit.Row(UiTokens.Sp2);
        noticeRow.AddChild(new UiIcon(UiGlyph.CheckBadge, 20, UiTokens.Success));
        _noticeLabel = UiKit.Text("", "BodyStrong", ellipsis: true);
        noticeRow.AddChild(_noticeLabel);
        _noticeButton = UiKit.Ghost("Pick next", () => { if (_noticeSlot >= 0) { _webTarget = _noticeSlot; _noticeBar.Visible = false; RefreshResearchWebNow(); } }, null, UiGlyph.None, 32);
        noticeRow.AddChild(_noticeButton);
        _noticeBar.AddChild(noticeRow);
        canvasColumn.AddChild(_noticeBar);

        _chooseView = UiKit.Column(UiTokens.Sp3);
        BuildChooseView(_chooseView);
        _mapView = UiKit.Column(UiTokens.Sp2);
        _mapView.Visible = false;
        var canvasHost = UiKit.Column(0);
        canvasHost.AddChild(_chooseView);
        canvasHost.AddChild(_mapView);
        _canvasScroll = UiKit.Scroll(canvasHost);
        _canvasScroll.FollowFocus = false;
        canvasColumn.AddChild(_canvasScroll);
        body.AddChild(canvasColumn);

        var inspectorHost = UiKit.Panel("WellPanel", null);
        inspectorHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inspectorHost.SizeFlagsStretchRatio = 0.382f;
        inspectorHost.CustomMinimumSize = new Vector2(300, 0);
        BuildInspector(inspectorHost);
        body.AddChild(inspectorHost);
        root.AddChild(body);

        // Footer
        var footer = UiKit.Row(UiTokens.Sp3);
        _webAutoFill = new UiToggle("Auto-fill", false, enabled => { if (!_updatingFocusBar) SetAutoFill(enabled); })
        {
            TooltipText = "When a slot opens, the elders pick a project after one season. Your own choices stay.",
            CustomMinimumSize = new Vector2(144, UiTokens.ControlS),
        };
        _webAutoFill.ThemeTypeVariation = "ToggleRowCompact";
        footer.AddChild(_webAutoFill);
        footer.AddChild(UiKit.Ghost("Elders' leaning…", ToggleLeaningPopover, "Which themes the elders favour when Auto-fill picks"));
        _peopleWebButton = UiKit.Secondary("People & needs", OpenPeoplePanel,
            "See what your people do and the needs shaping research choices", UiGlyph.People, UiTokens.ControlS, true);
        footer.AddChild(_peopleWebButton);
        _footerNote = UiKit.Text("Filled star: keystone path · outline star: breadth project · Click to inspect · Double-click or Enter to start · 1 2 3 choose a slot", "CaptionTertiary", right: true, ellipsis: true);
        _footerNote.HorizontalAlignment = HorizontalAlignment.Right;
        footer.AddChild(_footerNote);
        root.AddChild(footer);
        _hudRoot.AddChild(_webPanel);

        _queuePopover = new UiPopover("CardPanel") { Dismissed = CloseQueuePopover };
        _hudRoot.AddChild(_queuePopover);
        _leaningPopover = new UiPopover("CardPanel") { Dismissed = CloseLeaningPopover };
        BuildLeaningPopover();
        _hudRoot.AddChild(_leaningPopover);

        _webLayer = RegisterLayer("research", false, HandleWebEscape);
    }

    private StripChip BuildStripChip(int slot)
    {
        var chip = new StripChip();
        var panel = new PanelContainer { ThemeTypeVariation = "InnerCard" };
        chip.Root = UiKit.WrapOverlay(panel, new Vector2(0, 55));
        chip.Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        chip.Root.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        chip.Root.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _webTarget = slot;
                RefreshResearchWebNow();
            }
        };
        var row = UiKit.Row(UiTokens.Sp2);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        chip.Badge = new UiRankBadge(slot + 1);
        row.AddChild(chip.Badge);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        column.AddThemeConstantOverride("separation", 2);
        chip.Name = UiKit.Text("", "BodyStrong", ellipsis: true);
        column.AddChild(chip.Name);
        chip.Bar = new UiProgressBar(4);
        column.AddChild(chip.Bar);
        row.AddChild(column);
        var right = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        right.AddThemeConstantOverride("separation", 0);
        chip.Target = UiKit.Text("TARGET", "EyebrowAccent", right: true);
        chip.Target.Visible = false;
        chip.Eta = UiKit.Text("", "Num", minWidth: 52, right: true);
        right.AddChild(chip.Target);
        right.AddChild(chip.Eta);
        row.AddChild(right);
        panel.AddChild(row);
        chip.Frame = new UiFrame(UiTokens.Accent, 2, UiTokens.R1) { Visible = false };
        chip.Root.AddChild(chip.Frame);
        return chip;
    }

    private void BuildChooseView(VBoxContainer view)
    {
        _readyBlock = UiKit.Column(UiTokens.Sp3);
        _readyHeader = UiKit.Text("READY NOW", "Eyebrow");
        _readyBlock.AddChild(_readyHeader);
        _keystoneRow = UiKit.Row(UiTokens.Sp2);
        _keystoneRow.Visible = false;
        _readyBlock.AddChild(_keystoneRow);
        var columns = UiKit.Row(UiTokens.Sp3);
        for (int theme = 0; theme < 3; theme++)
        {
            var column = UiKit.Column(UiTokens.Sp2);
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            column.CustomMinimumSize = new Vector2(170, 0);
            var head = UiKit.Row(UiTokens.Sp2);
            head.AddChild(new UiIcon(UiDraw.ThemeGlyph((ResearchTheme)theme), 16, UiTokens.ThemeColor((ResearchTheme)theme)));
            Label title = UiKit.Text(UiText.ThemeNames[theme].ToUpperInvariant(), "Eyebrow", UiTokens.ThemeText((ResearchTheme)theme));
            head.AddChild(title);
            _columnCounts[theme] = UiKit.Text("", "Num", UiTokens.TextTertiary);
            head.AddChild(_columnCounts[theme]!);
            column.AddChild(head);
            Label purpose = UiKit.Text(UiText.ThemePurpose[theme], "CaptionTertiary", wrap: true);
            column.AddChild(purpose);
            var cards = UiKit.Column(UiTokens.Sp2);
            column.AddChild(cards);
            _readyColumns[theme] = cards;
            var empty = UiKit.Text("Nothing ready here yet", "CaptionTertiary");
            empty.Visible = false;
            column.AddChild(empty);
            _columnEmpty[theme] = empty;
            columns.AddChild(column);
        }
        _readyBlock.AddChild(columns);
        view.AddChild(_readyBlock);

        var almostHead = UiKit.Row(UiTokens.Sp2);
        _almostHeader = UiKit.Text("ALMOST READY", "Eyebrow", right: false);
        almostHead.AddChild(_almostHeader);
        almostHead.AddChild(UiKit.Spacer(expand: true));
        _showAllAlmost = UiKit.Ghost("Show all", () => { _showAllAlmostRows = !_showAllAlmostRows; _webSignature = int.MinValue; RefreshResearchWebNow(); }, null, UiGlyph.None, 30);
        almostHead.AddChild(_showAllAlmost);
        view.AddChild(almostHead);
        _almostList = UiKit.Column(UiTokens.Sp1);
        view.AddChild(_almostList);
        _moreLabel = UiKit.Text("", "CaptionTertiary");
        view.AddChild(_moreLabel);
        _searchList = UiKit.Column(UiTokens.Sp1);
        _searchList.Visible = false;
        view.AddChild(_searchList);
        _noResultsLabel = UiKit.Text("", "BodySecondary");
        _noResultsLabel.Visible = false;
        view.AddChild(_noResultsLabel);
    }

    private void BuildInspector(PanelContainer host)
    {
        _inspector = UiKit.Column(UiTokens.Sp3);
        _inspectorScroll = UiKit.Scroll(_inspector);
        host.AddChild(_inspectorScroll);

        _inspEmpty = UiKit.Text("Pick a project to see what it does.", "BodySecondary", wrap: true);
        _inspector.AddChild(_inspEmpty);

        _backLink = UiKit.Ghost("", () => { if (_backProject >= 0) { int back = _backProject; _backProject = -1; SelectResearchProject(back); } }, null, UiGlyph.None, 30);
        _backLink.Visible = false;
        _backLink.Alignment = HorizontalAlignment.Left;
        _inspector.AddChild(_backLink);

        _inspectorChips = UiKit.Row(UiTokens.Sp2);
        _chipTheme = UiKit.Chip("CRAFT", UiGlyph.Hammer);
        _chipAge = UiKit.Chip("DAWN AGE");
        _chipType = UiKit.Chip("METHOD");
        _inspectorChips.AddChild(_chipTheme);
        _inspectorChips.AddChild(_chipAge);
        _inspectorChips.AddChild(_chipType);
        _inspector.AddChild(_inspectorChips);

        _inspName = UiKit.Text("", "Title", wrap: true);
        _inspector.AddChild(_inspName);
        _inspState = UiKit.Text("", "BodyStrong", wrap: true);
        _inspector.AddChild(_inspState);

        _ctaButton = UiKit.Primary("", OnCtaPressed, null, UiGlyph.None, UiTokens.ControlL, focus: true);
        _ctaButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _inspector.AddChild(_ctaButton);
        _replaceRow = UiKit.Column(UiTokens.Sp2);
        _replaceRow.Visible = false;
        for (int slot = 0; slot < 3; slot++)
        {
            int captured = slot;
            UiButton chip = UiKit.Secondary("", () => AssignSelectedToSlot(captured), null, UiGlyph.None, UiTokens.ControlS, true);
            chip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            chip.CustomMinimumSize = new Vector2(150, UiTokens.ControlM);
            chip.ClipText = true;
            chip.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            chip.AddThemeFontSizeOverride("font_size", 14);
            _replaceChips[slot] = chip;
            _replaceRow.AddChild(chip);
        }
        _inspector.AddChild(_replaceRow);
        _inspWhy = UiKit.Text("", "Caption", wrap: true);
        _inspector.AddChild(_inspWhy);
        _inspEstimate = UiKit.Text("", "CaptionTertiary", wrap: true);
        _inspector.AddChild(_inspEstimate);

        _orRow = UiKit.Column(UiTokens.Sp2);
        _orLabel = UiKit.Text("Or:", "Caption");
        _orRow.AddChild(_orLabel);
        for (int i = 0; i < 3; i++)
        {
            int orIndex = i;
            UiButton button = UiKit.Secondary("", () => _orActions[orIndex]?.Invoke(), null, UiGlyph.None, UiTokens.ControlS, true);
            button.AddThemeFontSizeOverride("font_size", 14);
            button.ClipText = true;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.CustomMinimumSize = new Vector2(150, UiTokens.ControlS);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _orButtons[i] = button;
            _orRow.AddChild(button);
        }
        _inspector.AddChild(_orRow);

        _changesBlock = UiKit.Column(UiTokens.Sp1);
        _changesBlock.AddChild(UiKit.Text("WHAT CHANGES", "EyebrowTertiary"));
        _inspChanges = UiKit.Text("", "Body", wrap: true);
        _changesBlock.AddChild(_inspChanges);
        _inspector.AddChild(_changesBlock);

        _peopleProjectContext = UiKit.Column(UiTokens.Sp1);
        _peopleProjectContext.AddChild(UiKit.Text("YOUR PEOPLE & PRACTICE", "EyebrowTertiary"));
        _peopleProjectContextText = UiKit.Text("", "BodySecondary", wrap: true);
        _peopleProjectContext.AddChild(_peopleProjectContextText);
        _peopleProjectContext.Visible = false;
        _inspector.AddChild(_peopleProjectContext);

        _seeBlock = UiKit.Column(UiTokens.Sp1);
        _seeBlock.AddChild(UiKit.Text("WHAT YOU'LL SEE", "EyebrowTertiary"));
        _inspSeeText = UiKit.Text("", "BodySecondary", wrap: true);
        _seeBlock.AddChild(_inspSeeText);
        _inspFlavor = UiKit.Text("", "CaptionTertiary", wrap: true);
        _seeBlock.AddChild(_inspFlavor);
        _inspector.AddChild(_seeBlock);

        _needsBlock = UiKit.Column(UiTokens.Sp1);
        _needsBlock.AddChild(UiKit.Text("NEEDS", "EyebrowTertiary"));
        _needsNote = UiKit.Text("", "BodySecondary", wrap: true);
        _needsBlock.AddChild(_needsNote);
        _needsHeaderAll = UiKit.Text("All of these:", "Caption");
        _needsBlock.AddChild(_needsHeaderAll);
        _needsList = UiKit.Column(UiTokens.Sp1);
        _needsBlock.AddChild(_needsList);
        _needsHeaderAny = UiKit.Text("Any one of these:", "Caption");
        _needsBlock.AddChild(_needsHeaderAny);
        _needsAnyList = UiKit.Column(UiTokens.Sp1);
        _needsBlock.AddChild(_needsAnyList);
        _needsKeystone = UiKit.Text("", "Caption");
        _needsBlock.AddChild(_needsKeystone);
        _needsKeystoneBar = new UiProgressBar(6);
        _needsBlock.AddChild(_needsKeystoneBar);
        _inspector.AddChild(_needsBlock);

        _leadsBlock = UiKit.Column(UiTokens.Sp1);
        _leadsBlock.AddChild(UiKit.Text("LEADS TO", "EyebrowTertiary"));
        _leadsList = new HFlowContainer();
        _leadsList.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        _leadsList.AddThemeConstantOverride("v_separation", UiTokens.Sp2);
        _leadsBlock.AddChild(_leadsList);
        _inspector.AddChild(_leadsBlock);
    }

    private void BuildLeaningPopover()
    {
        VBoxContainer body = UiKit.Column(UiTokens.Sp3);
        body.CustomMinimumSize = new Vector2(377, 0);
        body.AddChild(UiKit.Text("Elders' leaning", "Heading"));
        body.AddChild(UiKit.Text("When Auto-fill picks, the elders favour these themes.", "BodySecondary", wrap: true));
        for (int theme = 0; theme < 3; theme++)
        {
            int captured = theme;
            var row = UiKit.Row(UiTokens.Sp2);
            row.AddChild(new UiIcon(UiDraw.ThemeGlyph((ResearchTheme)theme), 16, UiTokens.ThemeColor((ResearchTheme)theme)));
            row.AddChild(UiKit.Text(UiText.ThemeNames[theme], "BodyStrong", UiTokens.ThemeText((ResearchTheme)theme), minWidth: 64));
            var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(120, 24) };
            slider.ValueChanged += value => { if (_leaningValues[captured] is Label l) l.Text = $"{value:0}%"; };
            _leaningSliders[theme] = slider;
            row.AddChild(slider);
            _leaningValues[theme] = UiKit.Text("0%", "Num", minWidth: 44, right: true);
            row.AddChild(_leaningValues[theme]!);
            body.AddChild(row);
        }
        var actions = UiKit.Row(UiTokens.Sp2);
        actions.Alignment = BoxContainer.AlignmentMode.End;
        actions.AddChild(UiKit.Ghost("Cancel", CloseLeaningPopover));
        actions.AddChild(UiKit.Primary("Apply", () => { ApplyLeanings(); CloseLeaningPopover(); }, null, UiGlyph.None, UiTokens.ControlM, true));
        body.AddChild(actions);
        _leaningPopover!.Body.AddChild(body);
    }

    private void LayoutResearchWeb()
    {
        if (_webPanel is null) return;
        float m = Margin;
        float width = Math.Min(1597, _logicalSize.X - 2 * m);
        float top = m + TopBandHeight + (_compact ? 8 : 4);
        float bottom = _logicalSize.Y - m;
        AnchorTo(_webPanel, 0.5f, 0, 0.5f, 0, -width / 2, top, width / 2, bottom, Control.GrowDirection.Both);
        _webPanel.CustomMinimumSize = new Vector2(width, bottom - top);
        _webPanel.Size = new Vector2(width, bottom - top);
    }

    // ---- open / close ------------------------------------------------------------------------

    private void OpenResearchWeb(int targetSlot, int project = -1)
    {
        if (_webPanel is null || _current is null) return;
        if (_peopleOpen) ClosePeoplePanel();
        if (_ui.Contains("menu") || _ui.Contains("diplomacy") || _ui.Contains("chronicle")) CloseDrawersAndMenus();
        bool wasOpen = _researchWebVisible;
        if (!wasOpen && !_openingHold && _session is { Paused: true } && IsFreshUnassignedMatch())
            _openingHold = true;
        _researchWebVisible = true;
        _newProjectCount = 0;
        UpdateNewBadge();
        _webTarget = targetSlot;
        _replaceRowOpen = false;
        _backProject = -1;
        if (!wasOpen)
        {
            _webView = ViewChoose;
            _webViewSwitch?.Select(ViewChoose, false);
            _chooseView!.Visible = true;
            _mapView!.Visible = false;
            _webThemeFilter = -1;
            for (int i = 0; i < 4; i++) _themeChips[i].SetPressedNoSignal(i == 0);
            if (_webSearch is not null) _webSearch.Text = "";
            _showAllAlmostRows = false;
        }
        int selected = project;
        if (selected < 0 && targetSlot >= 0) selected = EffectiveSlotProject(targetSlot);
        if (selected >= 0 && selected < _current.FocusProjectCount && StateOf(selected) != ProjectState.Hidden) _selectedResearchProject = selected;
        else if (!wasOpen || _selectedResearchProject < 0 || StateOf(_selectedResearchProject) == ProjectState.Hidden) _selectedResearchProject = FindDefaultResearchProject();
        _webSignature = int.MinValue;
        _inspSignature = int.MinValue;
        _webScrim!.Visible = true;
        if (!wasOpen)
        {
            _webPanel.Modulate = new Color(1, 1, 1, 0);
            _webPanel.Visible = true;
            UiKit.Fade(_webPanel, true, UiTokens.SlowS);
            _ui.Push(_webLayer!);
        }
        RefreshResearchWebNow();
        if (_uiSettings.PauseInWeb && _session is { Paused: false } && !_pausedByWeb) { _session.SetPaused(true); _pausedByWeb = true; }
        UpdatePausedTreatment();
        CallDeferred(nameof(FocusSelectedCard));
    }

    private bool _pausedByWeb;

    private void FocusSelectedCard()
    {
        if (!_researchWebVisible) return;
        foreach (UiProjectCard card in _navCards)
            if (card.Project == _selectedResearchProject && card.IsVisibleInTree()) { card.GrabFocus(); return; }
    }

    private void CloseResearchWeb()
    {
        if (!_researchWebVisible) return;
        if (_peopleOpen) ClosePeoplePanel();
        _researchWebVisible = false;
        CloseQueuePopover();
        CloseLeaningPopover();
        if (_webScrim is not null) _webScrim.Visible = false;
        if (_webPanel is not null) UiKit.Fade(_webPanel, false, UiTokens.SlowCloseS);
        if (_webLayer is not null) _ui.Remove(_webLayer);
        if (_openingHold && _openingHoldAssignmentPending) EndOpeningHold();
        else if (_pausedByWeb && _session is { Paused: true } && !_openingHold) _session.SetPaused(false);
        _pausedByWeb = false;
        UpdatePausedTreatment();
        MaybeShowLookAroundTip();
    }

    private bool HandleWebEscape()
    {
        if (_webSearch is not null && _webSearch.Text.Length > 0)
        {
            _webSearch.Text = "";
            _webSearch.ReleaseFocus();
            _webSignature = int.MinValue;
            RefreshResearchWebNow();
            return false;
        }
        if (_replaceRowOpen)
        {
            _replaceRowOpen = false;
            _inspSignature = int.MinValue;
            RefreshResearchWebNow();
            return false;
        }
        CloseResearchWeb();
        return true;
    }

    private void SetResearchView(int view)
    {
        _webView = view;
        _webViewSwitch?.Select(view, false);
        if (_chooseView is not null) _chooseView.Visible = view == ViewChoose;
        if (_mapView is not null) _mapView.Visible = view == ViewMap;
        if (view == ViewMap && !_mapBuilt) BuildMapView();
        if (_canvasScroll is not null) _canvasScroll.ScrollVertical = 0;
        _webSignature = int.MinValue;
        RefreshResearchWebNow();
        if (view == ViewMap) CallDeferred(nameof(ScrollToSelected));
    }

    private void ScrollToSelected()
    {
        if (_canvasScroll is null || _selectedResearchProject < 0) return;
        if (_chips.TryGetValue(_selectedResearchProject, out UiProjectCard? chip) && chip.IsVisibleInTree()) _canvasScroll.EnsureControlVisible(chip);
    }

    private void SetThemeFilter(int filter)
    {
        _webThemeFilter = filter;
        for (int i = 0; i < 4; i++) _themeChips[i].SetPressedNoSignal(i == filter + 1);
        _webSignature = int.MinValue;
        RefreshResearchWebNow();
    }

    private void ShowFooterNote(string text)
    {
        if (_researchWebVisible && _footerNote is not null)
        {
            _footerNote.Text = text;
            _footerNote.ThemeTypeVariation = "CaptionSuccess";
            _footerNoteTimer = 4.0f;
        }
        else ShowToast(text, ToastKind.Info, "");
    }

    private void SelectFirstAlmostReady()
    {
        if (_current is null) return;
        for (int p = 0; p < _current.FocusProjectCount; p++)
            if (StateOf(p) == ProjectState.Glimpsed && _catalog!.Projects[p].Numeric) { SelectResearchProject(p); return; }
    }

    private void ShowReplaceRow()
    {
        _replaceRowOpen = true;
        _inspSignature = int.MinValue;
        RefreshResearchWebNow();
    }

    // ---- state helpers -----------------------------------------------------------------------

    private ProjectState StateOf(int project)
        => _current is not null && (uint)project < (uint)_current.FocusProjectCount ? (ProjectState)_current.FocusProjectState[project] : ProjectState.Hidden;

    /// <summary>The project a slot shows: the player's pending order while paused, else what the frame says.</summary>
    private int EffectiveSlotProject(int slot)
        => _pendingProject[slot] != -2 ? _pendingProject[slot] : _current?.FocusSlotProject[slot] ?? -1;

    private int SlotOfProject(int project)
    {
        for (int slot = 0; slot < 3; slot++) if (EffectiveSlotProject(slot) == project) return slot;
        return -1;
    }

    private int QueueIndexOf(int project)
    {
        if (_current is null) return -1;
        for (int i = 0; i < _current.FocusQueueCount; i++) if (_current.FocusQueue[i] == project) return i;
        return -1;
    }

    private int FirstOpenSlot()
    {
        for (int slot = 0; slot < 3; slot++) if (EffectiveSlotProject(slot) < 0) return slot;
        return -1;
    }

    private int ResolveTargetSlot() => _webTarget >= 0 ? _webTarget : FirstOpenSlot();

    private int FindDefaultResearchProject()
    {
        if (_current is null || _catalog is null) return -1;
        int best = -1;
        double bestCost = double.MaxValue;
        for (int p = 0; p < Math.Min(_current.FocusProjectCount, _catalog.Count); p++)
        {
            if (StateOf(p) != ProjectState.Available) continue;
            ProjectDef def = _catalog.Projects[p];
            double cost = def.Cost.ToDoubleForDisplay() + (int)def.Theme * 0.001;
            if (cost < bestCost) { bestCost = cost; best = p; }
        }
        return best;
    }

    private void SelectResearchProject(int project, bool fromPath = false)
    {
        if (_current is null) return;
        if (fromPath && _selectedResearchProject >= 0 && _selectedResearchProject != project) _backProject = _selectedResearchProject;
        else if (!fromPath) _backProject = -1;
        _selectedResearchProject = project;
        _replaceRowOpen = false;
        _inspSignature = int.MinValue;
        _webSignature = int.MinValue;
        if (fromPath && _webView == ViewChoose && StateOf(project) is ProjectState.Completed or ProjectState.Dormant) SetResearchView(ViewMap);
        RefreshResearchWebNow();
        if (_canvasScroll is not null)
        {
            if (_webView == ViewMap) CallDeferred(nameof(ScrollToSelected));
            else foreach (UiProjectCard card in _navCards)
                if (card.Project == project && card.IsVisibleInTree()) { _canvasScroll.EnsureControlVisible(card); break; }
        }
    }
}
