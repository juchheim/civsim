using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;
using ResearchTheme = CivSim.Sim.Research.Theme;

namespace CivSim.Game;

/// <summary>
/// The research web: an "Available" view of what is ready now and a radial "Tree" of every known project and its prerequisites,
/// with one inspector.
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
    /// <summary>The viewer's latest project completed while the web was closed; the next open lands on it instead of the last selection.</summary>
    private int _completedWhileClosed = -1;
    private bool _replaceRowOpen;
    /// <summary>Near-full-screen research web: no width cap, over the top bar, inspector at a fixed narrow width.</summary>
    private bool _webExpanded;
    private UiButton? _webExpandButton;
    private PanelContainer? _inspectorHost;

    // Full-screen stage (expanded Tree view): the map fills the window and these float over it. The widgets are reparented, never copied.
    private VBoxContainer? _webRoot, _canvasColumn;
    private HBoxContainer? _webHeader, _webStrip, _webFooter, _webBody;
    private Control? _webOverlay;
    private PanelContainer? _webTopBar, _webDock, _inspectorCard;
    private BoxContainer? _webDockBox;
    private VBoxContainer? _inspectorCardBody;
    private UiButton? _inspectorTab;
    private bool _webStageApplied, _inspectorCollapsed;
    private float _stageTop, _stageDockTop;

    // slot strip
    private sealed class StripChip
    {
        public UiResearchSlot Root = null!;
        public UiRankBadge Badge = null!;
        public Label Name = null!, Eta = null!, Target = null!;
        public UiProgressBar Bar = null!;
        public UiFrame Frame = null!;
    }
    private readonly StripChip[] _strip = new StripChip[3];
    private UiButton? _queueChip;

    // canvas
    private ScrollContainer? _canvasScroll;
    private VBoxContainer? _chooseView;
    private ResearchRadialMap? _mapView;
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
    private bool[] _agePathRequired = Array.Empty<bool>();
    private bool[] _agePathOption = Array.Empty<bool>();
    private bool[] _breadthPath = Array.Empty<bool>();

    // map view
    private RadialNodeLook[] _mapLooks = Array.Empty<RadialNodeLook>();
    /// <summary>The next ScrollToSelected centres the selection instead of only keeping it on screen.</summary>
    private bool _mapCenterNext;
    private int[][] _dependents = Array.Empty<int[]>();

    // inspector
    private ScrollContainer? _inspectorScroll;
    private VBoxContainer? _inspector;
    private UiButton? _backLink;
    private HBoxContainer? _inspectorChips;
    private PanelContainer? _chipTheme, _chipAge, _chipType;
    private Label? _inspName, _inspState, _ageRoleNote, _inspWhy, _inspEstimate, _inspSeeText, _inspFlavor, _inspChanges, _inspEmpty;
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
        _webRoot = root;
        _webPanel.AddChild(root);

        // Header
        var header = UiKit.Row(UiTokens.Sp3);
        _webHeader = header;
        header.AddChild(UiKit.Text("Research", "Title"));
        _webViewSwitch = new UiSegmented(new[] { "Available", "Tree" }, 0, SetResearchView, 88);
        _webViewSwitch.SetTooltip(0, "Projects you can start now");
        _webViewSwitch.SetTooltip(1, "Every project you know and what it needs, by age");
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
            PlaceholderText = "Find a project: fire, fish, tools  ( / )",
            CustomMinimumSize = new Vector2(300, UiTokens.ControlS),
            ClearButtonEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _webSearch.TextChanged += _ => { _webSignature = int.MinValue; RefreshResearchWebNow(); };
        _webSearch.TextSubmitted += _ => JumpToSearchResult();
        _webSearch.GuiInput += input =>
        {
            if (input is not InputEventKey { Pressed: true, Keycode: Key.Up or Key.Down } key || _webView != ViewMap || _mapView is null) return;
            _mapView.MoveSearchCursor(key.Keycode == Key.Down ? 1 : -1);
            _webSearch.AcceptEvent();
        };
        header.AddChild(_webSearch);
        _webExpandButton = UiKit.IconButton(UiGlyph.Expand, "Expand the tree (F)", ToggleWebExpanded);
        header.AddChild(_webExpandButton);
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (Esc)", CloseResearchWeb));
        root.AddChild(header);

        // Slot strip
        var stripRow = UiKit.Row(UiTokens.Sp2);
        _webStrip = stripRow;
        stripRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
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
        _webBody = body;
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var canvasColumn = UiKit.Column(UiTokens.Sp2);
        _canvasColumn = canvasColumn;
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
        _canvasScroll = UiKit.Scroll(_chooseView);
        _canvasScroll.FollowFocus = false;
        canvasColumn.AddChild(_canvasScroll);
        _mapView = BuildMapView();
        canvasColumn.AddChild(_mapView);
        body.AddChild(canvasColumn);

        var inspectorHost = UiKit.Panel("WellPanel", null);
        inspectorHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inspectorHost.SizeFlagsStretchRatio = 0.382f;
        inspectorHost.CustomMinimumSize = new Vector2(300, 0);
        _inspectorHost = inspectorHost;
        BuildInspector(inspectorHost);
        body.AddChild(inspectorHost);
        root.AddChild(body);

        // Footer
        var footer = UiKit.Row(UiTokens.Sp3);
        _webFooter = footer;
        _webAutoFill = new UiToggle("Auto-fill", false, enabled => { if (!_updatingFocusBar) SetAutoFill(enabled); })
        {
            TooltipText = "When a slot opens, the elders pick a project after one season. Your own choices stay.",
            CustomMinimumSize = new Vector2(144, UiTokens.ControlS),
        };
        _webAutoFill.ThemeTypeVariation = "ToggleRowCompact";
        footer.AddChild(_webAutoFill);
        footer.AddChild(UiKit.Ghost("Elders' leaning…", ToggleLeaningPopover, "Which themes the elders favour when Auto-fill picks"));
        _peopleWebButton = UiKit.Secondary("People & Stores", () => OpenPeoplePanel(PeopleTab.Overview, int.MinValue),
            "See what is slowing your people down, what you hold, who does what, and what your research is paying off", UiGlyph.People, UiTokens.ControlS, true);
        footer.AddChild(_peopleWebButton);
        _footerNote = UiKit.Text("Drag projects above to reprioritize · Star: age count · Link: prerequisite · Age: transition", "CaptionTertiary", right: true, ellipsis: true);
        _footerNote.HorizontalAlignment = HorizontalAlignment.Right;
        footer.AddChild(_footerNote);
        root.AddChild(footer);
        BuildWebStage();
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
        chip.Root = new UiResearchSlot(slot) { CustomMinimumSize = new Vector2(0, 55) };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.MouseFilter = Control.MouseFilterEnum.Pass;
        chip.Root.AddChild(panel);
        chip.Root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        chip.Root.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        chip.Root.ProjectAtSlot = () => EffectiveSlotProject(slot);
        chip.Root.CanDragProject = () => _session?.CanIssueCommands == true;
        chip.Root.ProjectName = project => _catalog is not null && (uint)project < (uint)_catalog.Count
            ? _catalog.Projects[project].Name
            : "research project";
        chip.Root.CanDropResearchSlot = CanDropResearchSlot;
        chip.Root.ResearchSlotDropped = MoveResearchSlot;
        chip.Root.TooltipText = "Drag a project to another slot to change its priority. Click to choose this slot for a project.";
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
        _ageRoleNote = UiKit.Text("", "Caption", wrap: true);
        _inspector.AddChild(_ageRoleNote);

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
            chip.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
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
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
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
        _needsHeaderAll = UiKit.Text("Required first:", "Caption");
        _needsBlock.AddChild(_needsHeaderAll);
        _needsList = UiKit.Column(UiTokens.Sp1);
        _needsBlock.AddChild(_needsList);
        _needsHeaderAny = UiKit.Text("Choose one route:", "Caption");
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
        ApplyWebStage();
        float m = Margin;
        if (_webStageApplied)
        {
            // full screen: the map is the panel; the controls float over it
            AnchorTo(_webPanel, 0, 0, 0, 0, 0, 0, _logicalSize.X, _logicalSize.Y, Control.GrowDirection.End);
            _webPanel.CustomMinimumSize = _logicalSize;
            _webPanel.Size = _logicalSize;
            PositionWebStage();
        }
        else
        {
            float width = _webExpanded ? _logicalSize.X - 2 * m : Math.Min(1597, _logicalSize.X - 2 * m);
            float top = m + TopBandHeight + (_compact ? 8 : 4);
            float bottom = _logicalSize.Y - m;
            AnchorTo(_webPanel, 0.5f, 0, 0.5f, 0, -width / 2, top, width / 2, bottom, Control.GrowDirection.Both);
            _webPanel.CustomMinimumSize = new Vector2(width, bottom - top);
            _webPanel.Size = new Vector2(width, bottom - top);
            _webPanel.Position = new Vector2((_logicalSize.X - width) / 2, top);
        }
        ApplyWebExpandedLook();
    }

    // ---- full-screen stage -------------------------------------------------------------------

    /// <summary>Expanded Tree view: the map fills the window and everything else floats over it.</summary>
    private bool WantsWebStage => _webExpanded && _webView == ViewMap;

    private void BuildWebStage()
    {
        _webOverlay = new Control { Name = "WebStage", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _webPanel!.AddChild(_webOverlay);

        _webTopBar = UiKit.Panel("HudPanel", "WebTopBar");
        _webOverlay.AddChild(_webTopBar);

        _webDock = UiKit.Panel("HudPanel", "WebDock");
        _webDockBox = new BoxContainer { Vertical = false };
        _webDockBox.AddThemeConstantOverride("separation", UiTokens.Sp3);
        _webDock.AddChild(_webDockBox);
        _webOverlay.AddChild(_webDock);

        _inspectorCard = UiKit.Panel("CardPanel", "WebInspectorCard");
        _inspectorCardBody = UiKit.Column(UiTokens.Sp2);
        var head = UiKit.Row(UiTokens.Sp2);
        head.AddChild(UiKit.Text("SELECTED PROJECT", "EyebrowTertiary"));
        head.AddChild(UiKit.Spacer(expand: true));
        head.AddChild(UiKit.IconButton(UiGlyph.ChevronRight, "Hide details (I)", ToggleInspectorCard, UiTokens.ControlS, 16));
        _inspectorCardBody.AddChild(head);
        _inspectorCard.AddChild(_inspectorCardBody);
        _webOverlay.AddChild(_inspectorCard);

        _inspectorTab = UiKit.Secondary("Details", ToggleInspectorCard, "Show the selected project (I)", UiGlyph.ChevronDown, UiTokens.ControlS);
        _inspectorTab.Visible = false;
        _webOverlay.AddChild(_inspectorTab);
    }

    private static void MoveTo(Control child, Node parent, int index = -1)
    {
        Node? old = child.GetParent();
        if (old != parent)
        {
            old?.RemoveChild(child);
            parent.AddChild(child);
        }
        if (index >= 0) parent.MoveChild(child, Math.Min(index, parent.GetChildCount() - 1));
    }

    private static void ResetAnchors(Control c)
    {
        c.AnchorLeft = c.AnchorTop = c.AnchorRight = c.AnchorBottom = 0;
        c.OffsetLeft = c.OffsetTop = c.OffsetRight = c.OffsetBottom = 0;
        c.GrowHorizontal = Control.GrowDirection.End;
        c.GrowVertical = Control.GrowDirection.End;
    }

    /// <summary>Switches between the sheet layout and the full-screen stage. Idempotent; safe to call on every layout.</summary>
    private void ApplyWebStage()
    {
        if (_webPanel is null || _webRoot is null || _webOverlay is null || _mapView is null) return;
        bool want = WantsWebStage;
        if (want == _webStageApplied) return;
        if (GetViewport().GuiIsDragging()) return; // reparenting mid-drag would cancel it; the next layout retries
        Control? focus = GetViewport().GuiGetFocusOwner();
        if (want) EnterWebStage(); else LeaveWebStage();
        _webStageApplied = want;
        // reparenting drops focus
        if (focus is not null && GodotObject.IsInstanceValid(focus)) focus.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void EnterWebStage()
    {
        MoveTo(_webHeader!, _webTopBar!, 0);
        MoveTo(_webStrip!, _webDockBox!, 0);
        MoveTo(_webFooter!, _webDockBox!, 1);
        MoveTo(_inspectorHost!, _inspectorCardBody!, 1);
        MoveTo(_noticeBar!, _webOverlay!);
        _webPanel!.ThemeTypeVariation = "WebStagePanel";
        _webOverlay!.Visible = true;
        for (int slot = 0; slot < 3; slot++) _strip[slot].Root.SizeFlagsStretchRatio = 1f; // equal chips: the dock is narrower than the sheet
        _queueChip!.CustomMinimumSize = new Vector2(120, 55);
        RaiseWebStage(true);
    }

    private void LeaveWebStage()
    {
        MoveTo(_webHeader!, _webRoot!, 0);
        MoveTo(_webStrip!, _webRoot!, 1);
        MoveTo(_webBody!, _webRoot!, 2);
        MoveTo(_webFooter!, _webRoot!, 3);
        MoveTo(_inspectorHost!, _webBody!, 1);
        MoveTo(_noticeBar!, _canvasColumn!, 0);
        ResetAnchors(_noticeBar!);
        ResetAnchors(_webHeader!);
        _webPanel!.ThemeTypeVariation = "SheetPanel";
        _webOverlay!.Visible = false;
        float[] ratios = { 1.0f, 0.618f, 0.382f };
        for (int slot = 0; slot < 3; slot++) _strip[slot].Root.SizeFlagsStretchRatio = ratios[slot];
        _queueChip!.CustomMinimumSize = new Vector2(144, 55);
        _mapView!.SetOverlayInsets(0, 0, 0, 0, false, 0);
        RaiseWebStage(false);
    }

    /// <summary>
    /// The top bar and the left rail are drawn above the web (spec §3.4). In the full-screen stage the web goes over them too,
    /// but still under toasts, the People sheet and modals. Same CanvasLayer, so only the child order changes.
    /// </summary>
    private void RaiseWebStage(bool over)
    {
        if (_hudRoot is null || _webScrim is null || _webPanel is null) return;
        var sheet = new List<Control> { _webScrim, _webPanel };
        if (_queuePopover is not null) sheet.Add(_queuePopover);
        if (_leaningPopover is not null) sheet.Add(_leaningPopover);
        void PlaceBefore(Control c, Control anchor)
        {
            int at = anchor.GetIndex();
            _hudRoot.MoveChild(c, c.GetIndex() < at ? at - 1 : at);
        }
        if (over)
        {
            // scrim, panel, pause bars (so the frame still shows), popovers, all just under the toasts
            Control anchor = _toastStack ?? (Control)_hudRoot.GetChild(_hudRoot.GetChildCount() - 1);
            var order = new List<Control> { _webScrim, _webPanel };
            foreach (ColorRect? bar in _pauseFrame) if (bar is not null) order.Add(bar);
            if (_queuePopover is not null) order.Add(_queuePopover);
            if (_leaningPopover is not null) order.Add(_leaningPopover);
            foreach (Control c in order) PlaceBefore(c, anchor);
        }
        else
        {
            // back where they were built: the pause bars first; scrim, panel and popovers just under the People sheet
            for (int i = 0; i < _pauseFrame.Length; i++)
                if (_pauseFrame[i] is { } bar) _hudRoot.MoveChild(bar, i);
            Control? anchor = _peopleRoot as Control ?? _civCard;
            if (anchor is null) return;
            foreach (Control c in sheet) PlaceBefore(c, anchor);
        }
    }

    /// <summary>Places the floating controls and tells the map how much of its area they cover.</summary>
    private void PositionWebStage()
    {
        if (_webTopBar is null || _webDock is null || _inspectorCard is null || _inspectorTab is null || _noticeBar is null || _mapView is null) return;
        float m = _compact ? 8 : 12;
        float W = _logicalSize.X, H = _logicalSize.Y;
        _webDockBox!.Vertical = _compact;

        AnchorTo(_webTopBar, 0, 0, 1, 0, m, m, -m, m);
        float barHeight = Math.Max(_webTopBar.GetCombinedMinimumSize().Y, 44);
        float top = m + barHeight + m;
        _stageTop = top;

        // the card is never narrower than what its chips need
        float cardWidth = Math.Max(_compact ? 300 : W >= 2200 ? 380 : 340, _inspectorCard.GetCombinedMinimumSize().X);
        bool showCard = !_inspectorCollapsed;
        _inspectorCard.Visible = showCard;
        _inspectorTab.Visible = !showCard;
        AnchorTo(_inspectorCard, 1, 0, 1, 1, -(m + cardWidth), top, -m, -m, Control.GrowDirection.Begin);
        _inspectorHost!.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _inspectorHost.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _inspectorHost.CustomMinimumSize = new Vector2(0, 0);
        const float tabWidth = 120;
        AnchorTo(_inspectorTab, 1, 0, 1, 0, -(m + tabWidth), top, -m, top, Control.GrowDirection.Begin);

        // the slot chips and footer controls sit in a second bar right under the top bar, beside the card
        float dockRight = showCard ? m + cardWidth + m : m;
        AnchorTo(_webDock, 0, 0, 1, 0, m, top, -dockRight, top);
        float dockHeight = _webDock.GetCombinedMinimumSize().Y;
        float mapTop = top + dockHeight + m;
        _stageDockTop = top + dockHeight + 6; // where popovers hang from

        AnchorTo(_noticeBar, 0.5f, 0, 0.5f, 0, 0, mapTop, 0, mapTop, Control.GrowDirection.Both);

        // with the card collapsed its tab sits at the right end of the dock row, so the dock gives it room
        if (!showCard) AnchorTo(_webDock, 0, 0, 1, 0, m, top, -(m + tabWidth + m), top);
        _mapView.SetOverlayInsets(0, mapTop, showCard ? cardWidth + m : 0, m, true, showCard ? 0 : 0);
    }

    private void ToggleInspectorCard()
    {
        if (!_webStageApplied) return;
        _inspectorCollapsed = !_inspectorCollapsed;
        PositionWebStage();
    }

    /// <summary>Inspector width, toggle glyph and footer hint for the current expanded state.</summary>
    private void ApplyWebExpandedLook()
    {
        if (_inspectorHost is not null && !_webStageApplied)
        {
            if (_webExpanded)
            {
                // fixed width: the extra room all goes to the tree; the slot buttons (150 wide minimum) still fit
                _inspectorHost.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
                _inspectorHost.CustomMinimumSize = new Vector2(_compact ? 300 : 360, 0);
            }
            else
            {
                _inspectorHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                _inspectorHost.SizeFlagsStretchRatio = 0.382f;
                _inspectorHost.CustomMinimumSize = new Vector2(300, 0);
            }
        }
        if (_webExpandButton is not null)
        {
            _webExpandButton.Glyph = _webExpanded ? UiGlyph.Collapse : UiGlyph.Expand;
            _webExpandButton.TooltipText = _webExpanded ? "Restore size (F)" : "Expand the tree (F)";
        }
        if (_footerNote is not null) _footerNote.Visible = !_webExpanded;
        if (_webExpandButton is not null && _webStageApplied) _webExpandButton.TooltipText = "Restore size (F)";
    }

    private void ToggleWebExpanded()
    {
        _webExpanded = !_webExpanded;
        _uiSettings.ResearchExpanded = _webExpanded;
        _uiSettings.Save();
        // the popovers were placed once at open time, so they would end up misplaced
        CloseQueuePopover();
        CloseLeaningPopover();
        _inspectorCollapsed = false;
        LayoutResearchWeb();
        _mapCenterNext = true; // the layout changed under the map: keep the zoom, re-centre on the selection
        _webSignature = int.MinValue;
        RefreshResearchWebNow();
        if (_webView == ViewMap) CallDeferred(nameof(ScrollToSelected));
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
            _webExpanded = _uiSettings.ResearchExpanded;
            // the tree never remembers the age it was looking at: it opens on the current age, or the age of the project it opened for
            _mapView?.BeginOpen();
            _webView = _uiSettings.ResearchMapView ? ViewMap : ViewChoose;
            _mapCenterNext = _webView == ViewMap;
            _webViewSwitch?.Select(_webView, false);
            ShowResearchView(_webView);
            _webThemeFilter = -1;
            for (int i = 0; i < 4; i++) _themeChips[i].SetPressedNoSignal(i == 0);
            if (_webSearch is not null) _webSearch.Text = "";
            _showAllAlmostRows = false;
        }
        int selected = project;
        if (selected < 0 && targetSlot >= 0) selected = EffectiveSlotProject(targetSlot);
        if (selected >= 0 && selected < _current.FocusProjectCount && StateOf(selected) != ProjectState.Hidden) _selectedResearchProject = selected;
        else if (!wasOpen && _completedWhileClosed >= 0 && IsKnownToPlayer(_completedWhileClosed)) _selectedResearchProject = _completedWhileClosed;
        else if (_selectedResearchProject < 0 || !IsKnownToPlayer(_selectedResearchProject)) _selectedResearchProject = FindDefaultResearchProject();
        if (!wasOpen) _completedWhileClosed = -1;
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
        if (_webView == ViewMap) CallDeferred(nameof(ScrollToSelected));
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
        if (_uiSettings.ResearchMapView != (_webView == ViewMap))
        {
            _uiSettings.ResearchMapView = _webView == ViewMap;
            _uiSettings.Save();
        }
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
        if (view == ViewMap && _webView != ViewMap) { _mapCenterNext = true; _mapView?.BeginOpen(); }
        _webView = view;
        _webViewSwitch?.Select(view, false);
        ShowResearchView(view);
        if (_footerNote is not null && _footerNoteTimer <= 0 && _footerNote.Text.StartsWith("Click a project", StringComparison.Ordinal)) _footerNote.Text = DefaultFooterNote();
        if (_canvasScroll is not null) _canvasScroll.ScrollVertical = 0;
        _webSignature = int.MinValue;
        RefreshResearchWebNow();
        if (view == ViewMap) CallDeferred(nameof(ScrollToSelected));
    }

    private void ShowResearchView(int view)
    {
        // the stage must be in place before the map becomes visible, so its first fit already knows the insets
        _webView = view;
        LayoutResearchWeb();
        if (_canvasScroll is not null) _canvasScroll.Visible = view == ViewChoose;
        if (_mapView is not null) _mapView.Visible = view == ViewMap;
    }

    /// <summary>Keeps the selection on the Map's screen; centres it after a jump from elsewhere (view switch, inspector link, search).</summary>
    private void ScrollToSelected()
    {
        if (_mapView is null || _webView != ViewMap || _selectedResearchProject < 0) return;
        _mapView.FocusProject(_selectedResearchProject, _mapCenterNext);
        _mapCenterNext = false;
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

    /// <summary>
    /// A hidden project on the required path to this age's turning point. The goal of an age should be plannable, so the path
    /// is shown by name as a locked step; everything else stays hidden. Display only: the sim still treats it as hidden.
    /// </summary>
    private bool IsRevealed(int project)
    {
        if (!RevealsPath) return false;
        if (_catalog is null || (uint)project >= (uint)_agePathRequired.Length || !_agePathRequired[project] || StateOf(project) != ProjectState.Hidden) return false;
        ProjectDef def = _catalog.Projects[project];
        return def.Numeric && !def.IsKeystone;
    }

    /// <summary>Ages up to and including this one (0 = Dawn, 1 = Hunters) show the required path to their turning point by name. Later ages keep it hidden; tune after playtests.</summary>
    private const int RevealPathThroughEra = 1;

    private bool RevealsPath => _current is not null && _current.FocusEra <= RevealPathThroughEra;

    private bool IsKnownToPlayer(int project) => StateOf(project) != ProjectState.Hidden || IsRevealed(project);

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
        List<int> queue = EffectiveResearchQueue(_current);
        for (int i = 0; i < queue.Count; i++) if (queue[i] == project) return i;
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
        if (_webStageApplied && _inspectorCollapsed) { _inspectorCollapsed = false; PositionWebStage(); }
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
