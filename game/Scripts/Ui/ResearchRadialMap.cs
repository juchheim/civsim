using System;
using System.Collections.Generic;
using CivSim.Sim.Research;
using Godot;
using ResearchTheme = CivSim.Sim.Research.Theme;

namespace CivSim.Game.Ui;

/// <summary>How one project looks on the research Map, resolved from the frame by the caller.</summary>
public struct RadialNodeLook
{
    /// <summary>Known to the player: drawn, named and clickable.</summary>
    public bool Visible;
    /// <summary>Hidden, but in an age the player has reached: a faint unnamed dot.</summary>
    public bool Undiscovered;
    public ProjectState State;
    public int SlotRank, QueueIndex;
    public float Progress;
    /// <summary>Turning points only: how many projects of the age already count toward its breadth requirement.</summary>
    public int BreadthDone;
    /// <summary>Turning points only: how many projects are still on the required path to it.</summary>
    public int PathLeft;
    public bool HiddenPrerequisite, Filtered, Match, RequiredForNextAge, OptionalNextAgeRoute, OnBreadthPath, TurnsAge;
}

/// <summary>
/// The research Map: one custom-drawn control. Rings are ages, sectors are tracks, curves run from a prerequisite outward to
/// what it unlocks. Layout comes from <see cref="ResearchRadialLayout"/> once per catalog; a frame only changes looks.
/// Wheel or pinch zooms toward the cursor, drag pans, click selects, double-click starts.
/// </summary>
public partial class ResearchRadialMap : Control
{
    public const float MaxZoom = 3.0f;
    private const int LabelFont = UiTokens.FontSmall;
    private const float LabelHeight = 19f;
    /// <summary>Room at the left of a turning point's label for its gem.</summary>
    private const float GateLabelPad = 18f;

    public Action<int>? ProjectClicked;
    public Action<int>? ProjectActivated;
    /// <summary>A search result was chosen: the host selects it, clears the search and brings it into view.</summary>
    public Action<int>? ProjectJumped;
    public Func<int, string>? TooltipFor;
    /// <summary>The first prerequisite still missing for a project, or -1 (the inspector's own rule).</summary>
    public Func<int, int>? FirstMissing;

    private ResearchCatalog? _catalog;
    private ResearchRadialLayout? _layout;
    private RadialNodeLook[] _looks = Array.Empty<RadialNodeLook>();
    private string[] _fieldNames = Array.Empty<string>();
    private int _selected = -1, _hover = -1, _focusEra, _drawEra, _matchCount;
    private bool _searching;
    private int _themeFilter = -1;
    private string _centerTitle = "", _centerSub = "";

    // view: the world point at the middle of the control, and pixels per world unit
    private Vector2 _center;
    private float _zoom = 1f, _targetZoom = 1f;
    private Vector2 _targetCenter;
    private bool _animating, _viewReady, _pendingCenter;
    private int _pendingProject = -1;

    // pointer
    private bool _pressed, _dragging, _doubleClicked;
    private Vector2 _pressAt;

    // highlight for the focused project (hovered, else selected)
    private int _focus = int.MinValue;
    private bool[] _upstream = Array.Empty<bool>(), _downstream = Array.Empty<bool>(), _onPath = Array.Empty<bool>();
    private int[] _blockerOf = Array.Empty<int>();
    // what a focused turning point or project still needs beyond the named chain
    private bool[] _breadth = Array.Empty<bool>(), _unmetDirect = Array.Empty<bool>(), _fogRequired = Array.Empty<bool>();
    private int _breadthEra = -1, _breadthMin;
    private int _nextStep = -1;
    private float _pulse;

    // labels, placed in zoomed world space so a pan does not move them relative to each other
    private readonly struct PlacedLabel
    {
        public readonly int Project;
        public readonly Rect2 Box;
        public readonly string Text;
        public PlacedLabel(int project, Rect2 box, string text) { Project = project; Box = box; Text = text; }
    }
    private readonly List<PlacedLabel> _labels = new();
    private readonly List<Rect2> _obstacles = new();
    private bool _labelsDirty = true;
    private float _labelZoom = -1;
    private readonly Dictionary<long, string> _truncated = new();
    private float[] _fullWidth = Array.Empty<float>();
    private Vector2[][] _edgeScreen = Array.Empty<Vector2[]>();
    private Rect2[] _edgeBounds = Array.Empty<Rect2>();

    // search results: the host passes the matches best-first; the list jumps to one
    private const int MaxResultRows = 5;
    private readonly List<ResultRow> _resultRows = new();
    private int[] _searchOrder = Array.Empty<int>();
    private int _searchCursor;

    // a ring that finds the project a jump landed on
    private int _pingProject = -1;
    private float _pingAge;
    private const float PingSeconds = 2.4f;

    // the next turning point, pinned to the edge of the view while it is off screen
    private Rect2 _edgeMarker;
    private int _edgeMarkerProject = -1;
    private float _pulseRedraw;
    private readonly UiButton _gateButton;

    // overlays
    private readonly Label _status;
    private readonly PanelContainer _statusPill;
    private readonly VBoxContainer _resultColumn;
    private readonly PanelContainer _legend;
    private readonly UiButton _legendButton;
    private readonly PanelContainer _toolPill;

    private static readonly StyleBoxFlat WellBox = UiTheme.Box(UiTokens.SurfaceSunken, UiTokens.R1, null, 0);
    private static readonly StyleBoxFlat LabelBox = UiTheme.Box(new Color("#1F1B16E0"), 4, null, 0);
    private static readonly StyleBoxFlat LabelBoxSelected = UiTheme.Box(new Color("#2A251EF2"), 4, UiTokens.Accent, 1);
    private static readonly StyleBoxFlat LabelBoxGate = UiTheme.Box(new Color("#2A251EF5"), 4, UiTokens.WithAlpha(new Color("#E2D3A6"), 0.8f), 1);
    private static readonly StyleBoxFlat LabelBoxPath = UiTheme.Box(new Color("#2A251EF2"), 4, UiTokens.WithAlpha(UiTokens.Accent, 0.55f), 1);
    private static readonly StyleBoxFlat TrackPill = UiTheme.Box(new Color("#1F1B16D9"), 12, UiTokens.Border, 1);
    private static readonly Color GateColor = new("#E2D3A6");

    public ResearchRadialMap()
    {
        ClipContents = true;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(320, 240);

        PanelContainer toolPill = _toolPill = UiKit.Panel("PillPanel", "MapTools");
        toolPill.SetAnchorsPreset(LayoutPreset.TopRight);
        toolPill.GrowHorizontal = GrowDirection.Begin;
        toolPill.OffsetLeft = -UiTokens.Sp2;
        toolPill.OffsetRight = -UiTokens.Sp2;
        toolPill.OffsetTop = UiTokens.Sp2;
        toolPill.OffsetBottom = UiTokens.Sp2;
        var tools = UiKit.Row(UiTokens.Sp1);
        toolPill.AddChild(tools);
        tools.AddChild(UiKit.IconButton(UiGlyph.Minus, "Zoom out (−)", () => ZoomBy(1 / 1.3f), 32, 16));
        tools.AddChild(UiKit.IconButton(UiGlyph.Plus, "Zoom in (=)", () => ZoomBy(1.3f), 32, 16));
        tools.AddChild(UiKit.Ghost("Fit", FitVisible, "Show every project you know (Home)", UiGlyph.None, 32));
        tools.AddChild(UiKit.Ghost("Selected", () => FocusProject(_selected, true), "Centre the selected project", UiGlyph.None, 32));
        _gateButton = UiKit.Ghost("Turning point", FocusNextTurningPoint, "Go to the project that opens the next age", UiGlyph.Gem, 32);
        tools.AddChild(_gateButton);
        _legendButton = UiKit.Ghost("Key", () => SetLegendVisible(!_legend!.Visible), "Show or hide the key", UiGlyph.None, 32);
        _legendButton.ToggleMode = true;
        tools.AddChild(_legendButton);
        AddChild(toolPill);

        _statusPill = UiKit.Panel("HudPanel", "MapSearchResults");
        _statusPill.Position = new Vector2(UiTokens.Sp2, UiTokens.Sp2);
        _statusPill.CustomMinimumSize = new Vector2(300, 0);
        _statusPill.Visible = false;
        _resultColumn = UiKit.Column(2);
        _status = UiKit.Text("", "Caption");
        _resultColumn.AddChild(UiKit.Margin(_status, UiTokens.Sp2, 2, UiTokens.Sp2, 2));
        for (int i = 0; i < MaxResultRows; i++)
        {
            var row = new ResultRow { ToggleMode = true, Visible = false };
            int slot = i;
            row.Pressed += () => ChooseResult(slot);
            row.MouseEntered += () => HighlightResult(slot);
            _resultRows.Add(row);
            _resultColumn.AddChild(row);
        }
        _statusPill.AddChild(_resultColumn);
        AddChild(_statusPill);

        _legend = BuildLegend();
        AddChild(_legend);
        SetLegendVisible(true);
        SetProcess(false);
    }

    // ---- data --------------------------------------------------------------------------------

    public ResearchRadialLayout? Layout => _layout;

    public void SetCatalog(ResearchCatalog catalog, Func<int, string> fieldName)
    {
        if (ReferenceEquals(_catalog, catalog) && _layout is not null) return;
        _catalog = catalog;
        _layout = ResearchRadialLayout.Build(catalog);
        int n = catalog.Count;
        _looks = new RadialNodeLook[n];
        _upstream = new bool[n];
        _downstream = new bool[n];
        _onPath = new bool[n];
        _blockerOf = new int[n];
        _breadth = new bool[n];
        _unmetDirect = new bool[n];
        _fogRequired = new bool[n];
        _fullWidth = new float[n];
        Font font = UiTheme.Medium;
        for (int p = 0; p < n; p++)
            _fullWidth[p] = _layout.Gate[p]
                ? UiTheme.Bold.GetStringSize(catalog.Projects[p].Name, HorizontalAlignment.Left, -1, LabelFont).X + GateLabelPad
                : font.GetStringSize(catalog.Projects[p].Name, HorizontalAlignment.Left, -1, LabelFont).X;
        _fieldNames = new string[catalog.LineageCodes.Count];
        for (int l = 0; l < _fieldNames.Length; l++) _fieldNames[l] = fieldName(l);
        _edgeScreen = new Vector2[_layout.Edges.Length][];
        _edgeBounds = new Rect2[_layout.Edges.Length];
        for (int e = 0; e < _layout.Edges.Length; e++)
        {
            float[] pts = _layout.Edges[e].Points;
            _edgeScreen[e] = new Vector2[pts.Length / 2];
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < pts.Length; i += 2)
            {
                min = new Vector2(Math.Min(min.X, pts[i]), Math.Min(min.Y, pts[i + 1]));
                max = new Vector2(Math.Max(max.X, pts[i]), Math.Max(max.Y, pts[i + 1]));
            }
            _edgeBounds[e] = new Rect2(min, max - min);
        }
        _focus = int.MinValue;
        _labelsDirty = true;
        _viewReady = false;
    }

    /// <summary>Applies the frame's looks. Copies <paramref name="looks"/>; call again whenever the web refreshes.</summary>
    public void SetLooks(RadialNodeLook[] looks, int selected, int focusEra, bool searching, int themeFilter, string centerTitle, string centerSub)
    {
        _themeFilter = themeFilter;
        if (_layout is null) return;
        Array.Copy(looks, _looks, Math.Min(looks.Length, _looks.Length));
        _selected = selected >= 0 && selected < _looks.Length && _looks[selected].Visible ? selected : -1;
        _focusEra = focusEra;
        _searching = searching;
        _centerTitle = centerTitle;
        _centerSub = centerSub;
        _matchCount = 0;
        int maxEra = focusEra + 1;
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Visible) continue;
            if (_looks[p].Match) _matchCount++;
            maxEra = Math.Max(maxEra, _layout.Era[p]);
        }
        _drawEra = maxEra;
        if (_hover >= 0 && !_looks[_hover].Visible) _hover = -1;
        _statusPill.Visible = searching;
        if (!searching) SetSearchResults(Array.Empty<int>(), 0);
        int next = NextTurningPoint();
        _gateButton.Disabled = next < 0;
        _gateButton.TooltipText = next >= 0 ? $"Go to {_catalog!.Projects[next].Name}, the project that opens the next age" : "Every turning point in your reach is known";
        _focus = int.MinValue;
        _labelsDirty = true;
        QueueRedraw();
    }

    // ---- search ------------------------------------------------------------------------------

    /// <summary>The known projects that match the search, best first, and how many undiscovered ones in reach match too. Shown as a list that jumps to the one chosen.</summary>
    public void SetSearchResults(ReadOnlySpan<int> ordered, int undiscovered)
    {
        bool same = ordered.Length == _searchOrder.Length;
        for (int i = 0; same && i < ordered.Length; i++) same = ordered[i] == _searchOrder[i];
        if (!same)
        {
            _searchOrder = ordered.ToArray();
            _searchCursor = 0;
        }
        if (_catalog is null || _layout is null) return;
        int shown = Math.Min(_searchOrder.Length, MaxResultRows);
        string more = undiscovered > 0 ? (undiscovered == 1 ? " · 1 more not discovered yet" : $" · {undiscovered} more not discovered yet") : "";
        string status = _searchOrder.Length == 0 ? "No known project matches" + more
            : _searchOrder.Length > MaxResultRows ? $"{_searchOrder.Length} matches · best {MaxResultRows} listed, all marked on the tree{more}"
            : (_searchOrder.Length == 1 ? "1 match" : $"{_searchOrder.Length} matches") + more;
        UiKit.SetText(_status, status);
        for (int i = 0; i < _resultRows.Count; i++)
        {
            ResultRow row = _resultRows[i];
            row.Visible = i < shown;
            if (i >= shown) continue;
            int p = _searchOrder[i];
            ProjectDef def = _catalog.Projects[p];
            bool gate = _layout.Gate[p];
            string field = gate ? "Turning point" : def.Lineage >= 0 && def.Lineage < _fieldNames.Length ? _fieldNames[def.Lineage] : "";
            row.Set(gate ? UiGlyph.Gem : UiDraw.ThemeGlyph(def.Theme), gate ? GateColor : UiTokens.ThemeColor(def.Theme), def.Name,
                $"{ResultState(_looks[p].State)} · {field}");
            row.SetPressedNoSignal(i == _searchCursor);
        }
    }

    private void HighlightResult(int row)
    {
        int shown = Math.Min(_searchOrder.Length, MaxResultRows);
        if (row >= shown || row == _searchCursor) return;
        _searchCursor = row;
        for (int i = 0; i < shown; i++) _resultRows[i].SetPressedNoSignal(i == _searchCursor);
    }

    /// <summary>One line of the result list: icon, name, and state · field at the right.</summary>
    private sealed partial class ResultRow : Button
    {
        private readonly UiIcon _icon = new(UiGlyph.Dot, 16, UiTokens.TextSecondary);
        private readonly Label _name = UiKit.Text("", "BodyStrong", ellipsis: true);
        private readonly Label _meta = UiKit.Text("", "CaptionTertiary");

        public ResultRow()
        {
            ThemeTypeVariation = "RowButton";
            FocusMode = FocusModeEnum.None;
            CustomMinimumSize = new Vector2(0, 32);
            ClipContents = true;
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.SetAnchorsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", UiTokens.Sp2);
            margin.AddThemeConstantOverride("margin_right", UiTokens.Sp2);
            var row = UiKit.Row(UiTokens.Sp2);
            row.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(_icon);
            _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(_name);
            row.AddChild(_meta);
            margin.AddChild(row);
            AddChild(margin);
        }

        public void Set(UiGlyph glyph, Color tint, string name, string meta)
        {
            _icon.Glyph = glyph;
            _icon.Tint = tint;
            UiKit.SetText(_name, name);
            UiKit.SetText(_meta, meta);
        }
    }

    private static string ResultState(ProjectState state) => state switch
    {
        ProjectState.Available => "Ready",
        ProjectState.Slotted => "In progress",
        ProjectState.Completed => "Known",
        ProjectState.Dormant => "Blocked",
        _ => "Locked",
    };

    /// <summary>Moves the highlight in the result list (arrow keys in the search box).</summary>
    public void MoveSearchCursor(int delta)
    {
        int shown = Math.Min(_searchOrder.Length, MaxResultRows);
        if (shown == 0) return;
        _searchCursor = (_searchCursor + delta + shown) % shown;
        for (int i = 0; i < shown; i++) _resultRows[i].SetPressedNoSignal(i == _searchCursor);
    }

    /// <summary>The project the highlighted result stands for, or -1 when nothing matches.</summary>
    public int SearchCursorProject => _searchCursor < _searchOrder.Length ? _searchOrder[_searchCursor] : -1;

    private void ChooseResult(int row)
    {
        if (row < _searchOrder.Length) ProjectJumped?.Invoke(_searchOrder[row]);
    }

    /// <summary>Rings a project for a moment, so a jump to it is plain to see.</summary>
    public void Ping(int project)
    {
        if ((uint)project >= (uint)_looks.Length) return;
        _pingProject = project;
        _pingAge = 0;
        SetProcess(true);
        QueueRedraw();
    }

    /// <summary>Brings the turning point that opens the next age into view, with a ring around it.</summary>
    public void FocusNextTurningPoint()
    {
        int gate = NextTurningPoint();
        if (gate < 0) return;
        ProjectClicked?.Invoke(gate);
        FocusProject(gate, true);
        Ping(gate);
    }

    /// <summary>The turning point of the current age can be started now: it breathes so nobody misses it.</summary>
    private bool TurningPointReady()
    {
        int gate = NextTurningPoint();
        return gate >= 0 && _looks[gate].State is ProjectState.Available;
    }

    private int NextTurningPoint()
    {
        if (_layout is null) return -1;
        for (int p = 0; p < _looks.Length; p++)
            if (_layout.Gate[p] && _looks[p].Visible && _looks[p].TurnsAge && _looks[p].State is not (ProjectState.Completed or ProjectState.Dormant)) return p;
        return -1;
    }

    // ---- view --------------------------------------------------------------------------------

    /// <summary>The part of the control not covered by the tools along the top or the key along the bottom.</summary>
    private Rect2 Usable()
    {
        float top = 48, bottom = _legend.Visible ? Math.Max(8, Size.Y - _legend.Position.Y) + 4 : 8;
        if (top + bottom > Size.Y * 0.6f) { top = 8; bottom = 8; }
        return new Rect2(8, top, Math.Max(1, Size.X - 16), Math.Max(1, Size.Y - top - bottom));
    }

    /// <summary>The world centre that puts <paramref name="world"/> in the middle of the usable part at <paramref name="zoom"/>.</summary>
    private Vector2 CenterFor(Vector2 world, float zoom) => world - (Usable().GetCenter() - Size * 0.5f) / zoom;

    private Vector2 ToScreen(float x, float y) => new Vector2((x - _center.X) * _zoom, (y - _center.Y) * _zoom) + Size * 0.5f;
    private Vector2 ToWorld(Vector2 screen) => (screen - Size * 0.5f) / _zoom + _center;

    private float DrawnOuter()
    {
        if (_layout is null) return 400;
        float outer = ResearchRadialLayout.CenterRadius;
        foreach (int era in _layout.Rings) if (era <= _drawEra) outer = _layout.RingOuter[era] + ResearchRadialLayout.RingGap;
        return outer;
    }

    private float MinZoom()
    {
        float extent = Math.Max(1, Math.Min(Size.X, Size.Y));
        return Math.Clamp(extent / (2 * (DrawnOuter() + 60)), 0.12f, 0.6f);
    }

    private void SetView(Vector2 center, float zoom, bool animate)
    {
        zoom = Math.Clamp(zoom, MinZoom(), MaxZoom);
        float limit = DrawnOuter();
        if (center.Length() > limit) center = center.Normalized() * limit;
        if (animate && !UiSettings.Current.ReduceMotion && _viewReady && IsVisibleInTree())
        {
            _targetCenter = center;
            _targetZoom = zoom;
            _animating = true;
            SetProcess(true);
        }
        else
        {
            _animating = false;
            _center = _targetCenter = center;
            if (!Mathf.IsEqualApprox(_zoom, zoom)) _labelsDirty = true;
            _zoom = _targetZoom = zoom;
        }
        _viewReady = true;
        QueueRedraw();
    }

    public void ZoomBy(float factor) => ZoomAt(Size * 0.5f, factor);

    private void ZoomAt(Vector2 screen, float factor)
    {
        Vector2 world = ToWorld(screen);
        float zoom = Math.Clamp(_zoom * factor, MinZoom(), MaxZoom);
        _animating = false;
        _zoom = _targetZoom = zoom;
        _center = _targetCenter = world - (screen - Size * 0.5f) / zoom;
        _labelsDirty = true;
        QueueRedraw();
    }

    /// <summary>Shows every known project, or the whole drawn map when nothing is known yet.</summary>
    public void FitVisible() => SetView(FitCenter(out float zoom), zoom, true);

    private Vector2 FitCenter(out float zoom)
    {
        if (_layout is null || Size.X < 2) { zoom = 1; return Vector2.Zero; }
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        int count = 0;
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Visible) continue;
            min = new Vector2(Math.Min(min.X, _layout.X[p]), Math.Min(min.Y, _layout.Y[p]));
            max = new Vector2(Math.Max(max.X, _layout.X[p]), Math.Max(max.Y, _layout.Y[p]));
            count++;
        }
        if (count == 0)
        {
            float outer = DrawnOuter();
            min = new Vector2(-outer, -outer);
            max = new Vector2(outer, outer);
        }
        Vector2 extent = max - min + new Vector2(260, 60);
        Rect2 usable = Usable();
        zoom = Math.Min(usable.Size.X / extent.X, usable.Size.Y / extent.Y);
        return CenterFor((min + max) * 0.5f, Math.Clamp(zoom, MinZoom(), MaxZoom));
    }

    /// <summary>Brings a project into view: always centred when <paramref name="center"/>, else only when it is off screen.</summary>
    public void FocusProject(int project, bool center)
    {
        if (_layout is null || (uint)project >= (uint)_looks.Length || !_looks[project].Visible) return;
        if (Size.X < 2 || !IsVisibleInTree())
        {
            _pendingProject = project;
            _pendingCenter = _pendingCenter || center;
            return;
        }
        if (!_viewReady) { ResetView(project); return; }
        Vector2 world = new(_layout.X[project], _layout.Y[project]);
        float zoom = _zoom;
        if (center)
        {
            // a locked project: frame it with its chain of missing steps, so the way in is on screen too
            Vector2 min = world, max = world;
            int current = project;
            for (int guard = 0; guard < 64 && FirstMissing is not null; guard++)
            {
                int missing = FirstMissing(current);
                if ((uint)missing >= (uint)_looks.Length || !_looks[missing].Visible || missing == project) break;
                var at = new Vector2(_layout.X[missing], _layout.Y[missing]);
                min = min.Min(at);
                max = max.Max(at);
                current = missing;
            }
            Vector2 extent = max - min + new Vector2(300, 80);
            Rect2 usable = Usable();
            float fit = Math.Min(usable.Size.X / extent.X, usable.Size.Y / extent.Y);
            zoom = Math.Clamp(Math.Max(zoom, 0.9f), 0.5f, Math.Max(0.5f, fit));
            zoom = Math.Min(zoom, 1.4f);
            var target = new Vector2(_layout.X[project], _layout.Y[project]);
            world = CenterFor((min + max) * 0.5f, zoom);
            // framing the chain must not leave the project itself at the edge, under the key or the tools
            Rect2 safe = Usable().Grow(-(NodeRadius(_layout.Gate[project]) + 48));
            if (!safe.HasPoint((target - world) * zoom + Size * 0.5f)) world = world.Lerp(CenterFor(target, zoom), 0.5f);
        }
        Vector2 screen = (new Vector2(_layout.X[project], _layout.Y[project]) - _center) * _zoom + Size * 0.5f;
        Rect2 inner = Usable().Grow(-40);
        if (center) SetView(world, zoom, true);
        else if (!inner.HasPoint(screen)) SetView(CenterFor(new Vector2(_layout.X[project], _layout.Y[project]), zoom), zoom, true);
    }

    /// <summary>The opening view: every known project if that is still readable, else the selection at a middle zoom.</summary>
    public void ResetView(int project)
    {
        if (Size.X < 2 || !IsVisibleInTree())
        {
            _viewReady = false;
            _pendingProject = project;
            _pendingCenter = true;
            return;
        }
        Vector2 fit = FitCenter(out float zoom);
        if (zoom >= 0.5f || (uint)project >= (uint)_looks.Length || !_looks[project].Visible || _layout is null)
            SetView(fit, Math.Clamp(zoom, 0.5f, 1.4f), false);
        else
        {
            // too much to read at once: the selection at a middle zoom, its neighbourhood around it
            SetView(CenterFor(new Vector2(_layout.X[project], _layout.Y[project]), 0.75f) * 0.6f + fit * 0.4f, 0.75f, false);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized || what == NotificationVisibilityChanged)
        {
            // the result list takes what the tools along the top leave
            float room = Size.X - _toolPill.GetCombinedMinimumSize().X - 3 * UiTokens.Sp2;
            _statusPill.CustomMinimumSize = new Vector2(Math.Clamp(room, 220, 320), 0);
            bool narrow = Size.X < 780;
            _gateButton.Text = narrow ? "" : "Turning point";
            _statusPill.Position = new Vector2(UiTokens.Sp2, room < 240 ? _toolPill.GetCombinedMinimumSize().Y + 2 * UiTokens.Sp2 : UiTokens.Sp2);
            if (Size.X > 2 && IsVisibleInTree() && _layout is not null)
            {
                if (!_viewReady) ResetView(_pendingProject >= 0 ? _pendingProject : _selected);
                else if (_pendingProject >= 0) FocusProject(_pendingProject, _pendingCenter);
                _pendingProject = -1;
                _pendingCenter = false;
            }
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        bool busy = false;
        if (_animating)
        {
            float k = 1f - (float)Math.Exp(-delta / 0.09);
            _center = _center.Lerp(_targetCenter, k);
            float logZoom = Mathf.Lerp(Mathf.Log(_zoom), Mathf.Log(_targetZoom), k);
            _zoom = Mathf.Exp(logZoom);
            _labelsDirty = true;
            if (_center.DistanceTo(_targetCenter) * _zoom < 0.5f && Math.Abs(_zoom - _targetZoom) < 0.002f)
            {
                _center = _targetCenter;
                _zoom = _targetZoom;
                _animating = false;
            }
            busy = true;
        }
        bool pulsing = (_nextStep >= 0 || TurningPointReady()) && !UiSettings.Current.ReduceMotion;
        if (pulsing)
        {
            _pulse = (_pulse + (float)delta) % 1.6f;
            busy = true;
        }
        if (_pingProject >= 0)
        {
            _pingAge += (float)delta;
            if (_pingAge >= PingSeconds) _pingProject = -1;
            busy = true;
        }
        // a pulse alone is slow enough to redraw at ~20 fps; moving the view or a ping needs every frame
        _pulseRedraw += (float)delta;
        bool moving = _animating || _pingProject >= 0;
        if (busy && IsVisibleInTree())
        {
            if (moving || _pulseRedraw >= 0.05f) { _pulseRedraw = 0; QueueRedraw(); }
        }
        else SetProcess(false);
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _GuiInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } wheel:
                ZoomAt(wheel.Position, Mathf.Pow(1.15f, Math.Max(1f, wheel.Factor)));
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } wheel:
                ZoomAt(wheel.Position, 1f / Mathf.Pow(1.15f, Math.Max(1f, wheel.Factor)));
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle } button:
                if (button.Pressed)
                {
                    GrabFocus();
                    _pressed = true;
                    _dragging = false;
                    _pressAt = button.Position;
                    _doubleClicked = false;
                    if (button.ButtonIndex == MouseButton.Left && button.DoubleClick)
                    {
                        int hit = HitTest(button.Position);
                        // the edge tab only says "go there": a double click on it must not start the project
                        if (hit >= 0 && !(hit == _edgeMarkerProject && _edgeMarker.HasPoint(button.Position))) { _doubleClicked = true; ProjectActivated?.Invoke(hit); }
                    }
                }
                else
                {
                    if (_pressed && !_dragging && !_doubleClicked && button.ButtonIndex == MouseButton.Left)
                    {
                        int hit = HitTest(button.Position);
                        if (hit >= 0)
                        {
                            bool pointer = hit == _edgeMarkerProject && _edgeMarker.HasPoint(button.Position);
                            ProjectClicked?.Invoke(hit);
                            if (pointer) { FocusProject(hit, true); Ping(hit); }
                        }
                    }
                    _pressed = false;
                    _dragging = false;
                    UpdateHover(button.Position);
                }
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                if (_pressed && (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Middle)) != 0)
                {
                    if (!_dragging && motion.Position.DistanceTo(_pressAt) > 5) _dragging = true;
                    if (_dragging)
                    {
                        _animating = false;
                        _center -= motion.Relative / _zoom;
                        _targetCenter = _center;
                        MouseDefaultCursorShape = CursorShape.Drag;
                        QueueRedraw();
                    }
                }
                else UpdateHover(motion.Position);
                break;
            case InputEventMagnifyGesture magnify:
                ZoomAt(magnify.Position, Math.Clamp(magnify.Factor, 0.8f, 1.25f));
                AcceptEvent();
                break;
            case InputEventPanGesture pan:
                // Two-finger scroll zooms, as on the world map (docs/MVP/20 §4.1); drag pans.
                if (Math.Abs(pan.Delta.Y) > 0.01f) ZoomAt(pan.Position, Mathf.Pow(1.15f, -pan.Delta.Y * 0.5f));
                AcceptEvent();
                break;
        }
    }

    private void UpdateHover(Vector2 position)
    {
        int hit = HitTest(position);
        MouseDefaultCursorShape = hit >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
        if (hit == _hover) return;
        _hover = hit;
        _labelsDirty = true;
        QueueRedraw();
    }

    private int HitTest(Vector2 screen)
    {
        if (_layout is null) return -1;
        if (_edgeMarkerProject >= 0 && _edgeMarker.HasPoint(screen)) return _edgeMarkerProject;
        float best = float.MaxValue;
        int found = -1;
        float reach = Math.Max(13f, NodeRadius(false) + 6);
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Visible) continue;
            float d = ToScreen(_layout.X[p], _layout.Y[p]).DistanceTo(screen);
            float limit = _layout.Gate[p] ? Math.Max(reach * 1.4f, NodeRadius(true) * 1.3f) : reach;
            if (d <= limit && d < best) { best = d; found = p; }
        }
        return found;
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        int hit = HitTest(atPosition);
        return hit >= 0 && TooltipFor is not null ? TooltipFor(hit) : "";
    }

    /// <summary>The nearest known project from <paramref name="from"/> in a screen direction, for arrow keys; -1 when none.</summary>
    public int Neighbour(int from, int dx, int dy)
    {
        if (_layout is null) return -1;
        if ((uint)from >= (uint)_looks.Length || !_looks[from].Visible)
        {
            // nothing selected: the known project nearest the middle of the view
            float best0 = float.MaxValue;
            int found0 = -1;
            for (int p = 0; p < _looks.Length; p++)
            {
                if (!_looks[p].Visible) continue;
                float d = ToScreen(_layout.X[p], _layout.Y[p]).DistanceTo(Size * 0.5f);
                if (d < best0) { best0 = d; found0 = p; }
            }
            return found0;
        }
        var dir = new Vector2(dx, dy);
        Vector2 origin = new(_layout.X[from], _layout.Y[from]);
        float best = float.MaxValue;
        int found = -1;
        for (int p = 0; p < _looks.Length; p++)
        {
            if (p == from || !_looks[p].Visible || _looks[p].Filtered) continue;
            Vector2 v = new Vector2(_layout.X[p], _layout.Y[p]) - origin;
            float length = v.Length();
            if (length < 0.01f) continue;
            float cos = v.Dot(dir) / length;
            if (cos < 0.5f) continue;
            float score = length * (1 + 2.5f * (1 - cos));
            if (score < best) { best = score; found = p; }
        }
        return found;
    }

    // ---- highlight ---------------------------------------------------------------------------

    private void RebuildHighlight()
    {
        int focus = _hover >= 0 ? _hover : _selected;
        if (focus == _focus) return;
        _focus = focus;
        Array.Clear(_upstream);
        Array.Clear(_downstream);
        Array.Clear(_onPath);
        Array.Fill(_blockerOf, -1);
        Array.Clear(_breadth);
        Array.Clear(_unmetDirect);
        Array.Clear(_fogRequired);
        _breadthEra = -1;
        _nextStep = -1;
        if (_layout is null || focus < 0) return;

        var stack = new Stack<int>();
        stack.Push(focus);
        while (stack.Count > 0)
        {
            int p = stack.Pop();
            foreach (int r in _layout.Prerequisites[p])
                if (!_upstream[r]) { _upstream[r] = true; stack.Push(r); }
        }
        foreach (int d in _layout.Dependents[focus]) _downstream[d] = true;

        // every named prerequisite still missing, including ones the player has not discovered (shown unnamed)
        foreach (ResearchRadialLayout.Edge edge in _layout.Edges)
        {
            if (!_looks[edge.To].Visible || !(edge.To == focus || _upstream[edge.To])) continue;
            bool fog = !_looks[edge.From].Visible && _looks[edge.From].Undiscovered;
            if (fog) _fogRequired[edge.From] = true;
            if (edge.To == focus && !edge.Any && (fog || (_looks[edge.From].Visible && !IsKnown(edge.From)))) _unmetDirect[edge.From] = true;
        }

        // a turning point also wants a count of projects from its age, whichever they are
        ProjectDef focusDef = _catalog!.Projects[focus];
        if (focusDef.IsKeystone && focusDef.Numeric && focusDef.BreadthEra >= 0 && _looks[focus].TurnsAge)
        {
            _breadthEra = focusDef.BreadthEra;
            _breadthMin = focusDef.BreadthMin;
            for (int p = 0; p < _breadth.Length; p++)
            {
                ProjectDef d = _catalog.Projects[p];
                _breadth[p] = d.Numeric && !d.IsKeystone && d.Era == focusDef.BreadthEra && _layout.Placed[p] && (_looks[p].Visible || _looks[p].Undiscovered);
            }
        }

        // the chain of first missing prerequisites, down to something that can start now
        int current = focus;
        for (int guard = 0; guard < 64 && FirstMissing is not null; guard++)
        {
            int missing = FirstMissing(current);
            if (missing < 0 || missing >= _looks.Length || _onPath[missing]) break;
            _blockerOf[current] = missing;
            _onPath[missing] = true;
            ProjectState state = _looks[missing].State;
            if (state is ProjectState.Available or ProjectState.Slotted) { _nextStep = missing; break; }
            if (!_looks[missing].Visible) break;
            current = missing;
        }
        if (_nextStep >= 0 && !UiSettings.Current.ReduceMotion) SetProcess(true);
    }

    private bool IsKnown(int p) => _looks[p].Visible && _looks[p].State is ProjectState.Completed or ProjectState.Dormant;

    private bool InFocus(int p) => _focus < 0 || p == _focus || _upstream[p] || _downstream[p] || _breadth[p] || _fogRequired[p];

    /// <summary>Hovering is a question about one project, so it dims the rest harder than a standing selection does.</summary>
    private float OutOfFocusAlpha => _hover >= 0 ? 0.32f : 0.5f;

    // ---- drawing -----------------------------------------------------------------------------

    /// <summary>Turning points are the biggest marks on the map at any zoom: never smaller than 10 px, so they read from across the screen.</summary>
    private float NodeRadius(bool gate) => gate ? Math.Max(10f, Math.Clamp(8f * _zoom, 3f, 12f) * 1.9f) : Math.Clamp(8f * _zoom, 3f, 12f);

    public override void _Draw()
    {
        DrawStyleBox(WellBox, new Rect2(Vector2.Zero, Size));
        if (_layout is null || _catalog is null) return;
        RebuildHighlight();
        if ((_nextStep >= 0 || TurningPointReady()) && !UiSettings.Current.ReduceMotion && !IsProcessing()) SetProcess(true);
        if (_labelsDirty || !Mathf.IsEqualApprox(_labelZoom, _zoom)) PlaceLabels();

        DrawRings();
        DrawEdges();
        DrawNodes();
        DrawLabels();
        DrawPing();
        DrawBreadthCounter();
        DrawTrackNames();
        DrawTurningPointPointer();
    }

    private Vector2 Origin => ToScreen(0, 0);

    private void DrawRings()
    {
        ResearchRadialLayout layout = _layout!;
        Vector2 o = Origin;
        float slot = layout.SlotAngle;

        // centre disc
        float centre = ResearchRadialLayout.CenterRadius * _zoom;
        DrawCircle(o, centre - 6 * _zoom, UiTokens.WithAlpha(UiTokens.Surface2, 0.55f));

        foreach (int era in layout.Rings)
        {
            if (era > _drawEra) break;
            bool next = era > _focusEra;
            float inner = layout.RingInner[era] * _zoom, outer = layout.RingOuter[era] * _zoom;
            foreach (ResearchRadialLayout.Sector sector in layout.Sectors)
            {
                Color tint = UiTokens.ThemeFill((ResearchTheme)sector.Theme);
                tint.A = next ? 0.16f : era == _focusEra ? 0.42f : 0.30f;
                if (_themeFilter >= 0) tint.A *= sector.Theme == _themeFilter ? 1.6f : 0.35f;
                DrawAnnulus(o, inner, outer, sector.Start - slot * 0.35f, sector.End + slot * 0.35f, tint);
            }
            Color edge = next ? UiTokens.WithAlpha(UiTokens.TextTertiary, 0.25f) : UiTokens.Border;
            if (next)
            {
                DrawDashedCircle(o, inner, edge);
                DrawDashedCircle(o, outer, edge);
            }
            else
            {
                DrawArc(o, inner, 0, Mathf.Tau, ArcPoints(inner), edge, 1f, true);
                DrawArc(o, outer, 0, Mathf.Tau, ArcPoints(outer), edge, 1f, true);
            }
        }

        // field separators inside each track
        if (_zoom >= 0.45f)
        {
            float from = ResearchRadialLayout.CenterRadius * _zoom, to = (DrawnOuter() - ResearchRadialLayout.RingGap) * _zoom;
            Color line = UiTokens.WithAlpha(UiTokens.TextPrimary, 0.05f);
            for (int w = 0; w < layout.Wedges.Length; w++)
            {
                ResearchRadialLayout.Wedge wedge = layout.Wedges[w];
                if (w + 1 >= layout.Wedges.Length || layout.Wedges[w + 1].Theme != wedge.Theme) continue;
                var dir = new Vector2(Mathf.Cos(wedge.End), Mathf.Sin(wedge.End));
                DrawLine(o + dir * from, o + dir * to, line, 1f, true);
            }
        }

        // gates: a dashed circle where a turning point opens the next age
        for (int p = 0; p < layout.Count; p++)
        {
            if (!layout.Gate[p] || !_looks[p].Visible) continue;
            float r = layout.Radius[p] * _zoom;
            bool knownGate = _looks[p].State is ProjectState.Completed or ProjectState.Dormant;
            Color c = UiTokens.WithAlpha(GateColor, knownGate ? 0.15f : _looks[p].TurnsAge ? 0.6f : 0.25f);
            DrawDashedCircle(o, r, c);
        }

        // age names in the empty wedge at 12 o'clock
        Font caps = UiTheme.Caps;
        foreach (int era in layout.Rings)
        {
            if (era > _drawEra) break;
            float mid = (layout.RingInner[era] + layout.RingOuter[era]) * 0.5f * _zoom;
            string name = UiText.AgeName(era).ToUpperInvariant();
            Vector2 extent = caps.GetStringSize(name, HorizontalAlignment.Left, -1, UiTokens.FontSmall);
            float room = (layout.RingOuter[era] - layout.RingInner[era]) * _zoom;
            if (room < 16 && era != _focusEra) continue;
            Vector2 at = o + new Vector2(-extent.X * 0.5f, -mid + 5);
            Color color = era == _focusEra ? UiTokens.Accent : era > _focusEra ? UiTokens.TextDisabled : UiTokens.TextTertiary;
            DrawString(caps, at, name, HorizontalAlignment.Left, -1, UiTokens.FontSmall, color);
        }

        // centre text: the age you are in and how far its turning point is
        if (centre >= 52)
        {
            Vector2 t = caps.GetStringSize(_centerTitle, HorizontalAlignment.Left, -1, UiTokens.FontSmall);
            float y = _centerSub.Length > 0 && centre >= 105 ? -6 : 5;
            DrawString(caps, o + new Vector2(-t.X * 0.5f, y), _centerTitle, HorizontalAlignment.Left, -1, UiTokens.FontSmall, UiTokens.Accent);
            if (_centerSub.Length > 0 && centre >= 105)
            {
                float width = Math.Min(centre * 1.6f, 260);
                DrawMultilineString(UiTheme.Regular, o + new Vector2(-width * 0.5f, y + 20), _centerSub, HorizontalAlignment.Center, width,
                    UiTokens.FontSmall, 3, UiTokens.TextSecondary);
            }
        }
    }

    /// <summary>Track names just outside the outermost drawn ring, pulled onto the edge of the view when that is off screen.</summary>
    private void DrawTrackNames()
    {
        ResearchRadialLayout layout = _layout!;
        Vector2 o = Origin;
        Font caps = UiTheme.Caps;
        float trackRadius = (DrawnOuter() - ResearchRadialLayout.RingGap) * _zoom + 22;
        Rect2 inset = Usable();
        foreach (ResearchRadialLayout.Sector sector in layout.Sectors)
        {
            var theme = (ResearchTheme)sector.Theme;
            float angle = (sector.Start + sector.End) * 0.5f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 anchor = o + dir * trackRadius;
            if (!inset.HasPoint(anchor))
                anchor = inset.HasPoint(o) ? o + dir * (ExitDistance(o, dir, inset) - 4) : anchor.Clamp(inset.Position, inset.End);
            string name = UiText.ThemeNames[sector.Theme].ToUpperInvariant();
            Vector2 extent = caps.GetStringSize(name, HorizontalAlignment.Left, -1, UiTokens.FontSmall);
            var size = new Vector2(extent.X + 22, 18);
            Vector2 topLeft = AnchorBox(anchor, angle, size).Clamp(inset.Position, inset.End - size);
            DrawStyleBox(TrackPill, new Rect2(topLeft - new Vector2(7, 3), size + new Vector2(14, 6)));
            float alpha = _themeFilter >= 0 && _themeFilter != sector.Theme ? 0.4f : 1f;
            UiDraw.Glyph(this, UiDraw.ThemeGlyph(theme), new Rect2(topLeft + new Vector2(0, 1), new Vector2(16, 16)), UiTokens.WithAlpha(UiTokens.ThemeColor(theme), alpha), 1.5f);
            DrawString(caps, topLeft + new Vector2(22, 14), name, HorizontalAlignment.Left, -1, UiTokens.FontSmall, UiTokens.WithAlpha(UiTokens.ThemeText(theme), alpha));
        }
    }

    /// <summary>How far a ray from a point inside a rectangle travels before it leaves it.</summary>
    private static float ExitDistance(Vector2 from, Vector2 dir, Rect2 rect)
    {
        float t = float.MaxValue;
        if (dir.X > 1e-4f) t = Math.Min(t, (rect.End.X - from.X) / dir.X);
        if (dir.X < -1e-4f) t = Math.Min(t, (rect.Position.X - from.X) / dir.X);
        if (dir.Y > 1e-4f) t = Math.Min(t, (rect.End.Y - from.Y) / dir.Y);
        if (dir.Y < -1e-4f) t = Math.Min(t, (rect.Position.Y - from.Y) / dir.Y);
        return t == float.MaxValue ? 0 : Math.Max(0, t);
    }

    /// <summary>Top-left of a box that hangs off a point on a circle, on the outward side.</summary>
    private static Vector2 AnchorBox(Vector2 anchor, float angle, Vector2 size)
    {
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        float x = c > 0.3f ? anchor.X : c < -0.3f ? anchor.X - size.X : anchor.X - size.X * 0.5f;
        float y = s > 0.3f ? anchor.Y : s < -0.3f ? anchor.Y - size.Y : anchor.Y - size.Y * 0.5f;
        return new Vector2(x, y);
    }

    private static int ArcPoints(float radius) => Math.Clamp((int)(radius * 0.35f), 48, 256);

    private void DrawAnnulus(Vector2 o, float inner, float outer, float start, float end, Color color)
    {
        int steps = Math.Clamp((int)((end - start) * outer * 0.12f), 8, 96);
        var points = new Vector2[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++)
        {
            float a = start + (end - start) * i / steps;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            points[i] = o + dir * outer;
            points[points.Length - 1 - i] = o + dir * inner;
        }
        DrawColoredPolygon(points, color);
    }

    private void DrawDashedCircle(Vector2 o, float radius, Color color)
    {
        int dashes = Math.Clamp((int)(radius * Mathf.Tau / 9f), 24, 360);
        float step = Mathf.Tau / dashes;
        for (int i = 0; i < dashes; i++)
            DrawArc(o, radius, i * step, i * step + step * 0.5f, 3, color, 1f, true);
    }

    private void DrawEdges()
    {
        ResearchRadialLayout layout = _layout!;
        bool focused = _focus >= 0;
        var view = new Rect2(ToWorld(Vector2.Zero), Size / _zoom).Grow(20);
        // pass 0: quiet edges, pass 1: highlighted ones on top
        for (int pass = 0; pass < 2; pass++)
            for (int e = 0; e < layout.Edges.Length; e++)
            {
                ResearchRadialLayout.Edge edge = layout.Edges[e];
                bool fogEdge = !_looks[edge.From].Visible;
                if (!_looks[edge.To].Visible) continue;
                if (fogEdge && !(focused && _fogRequired[edge.From] && (edge.To == _focus || _upstream[edge.To]))) continue;
                if (!view.Intersects(_edgeBounds[e])) continue;
                Color color;
                float width;
                bool strong;
                bool dimmed = _looks[edge.From].Filtered || _looks[edge.To].Filtered || (_searching && !_looks[edge.From].Match && !_looks[edge.To].Match);
                if (focused && _blockerOf[edge.To] == edge.From)
                {
                    strong = true;
                    color = UiTokens.Accent;
                    width = 3f;
                }
                else if (focused && edge.To == _focus && _unmetDirect[edge.From] && !edge.Any)
                {
                    strong = true;
                    color = UiTokens.Accent;
                    width = 3f;
                }
                else if (focused && (edge.To == _focus || _upstream[edge.To]))
                {
                    strong = true;
                    bool met = _looks[edge.From].State is ProjectState.Completed or ProjectState.Dormant;
                    color = met ? UiTokens.WithAlpha(UiTokens.TextSecondary, 0.7f) : UiTokens.WithAlpha(UiTokens.Accent, 0.6f);
                    width = 2f;
                }
                else if (focused && edge.From == _focus)
                {
                    strong = true;
                    color = UiTokens.WithAlpha(UiTokens.ThemeColor(_catalog!.Projects[edge.To].Theme), 0.65f);
                    width = 1.5f;
                }
                else
                {
                    strong = false;
                    color = edge.Any ? UiTokens.WithAlpha(UiTokens.Info, 0.22f) : UiTokens.WithAlpha(UiTokens.TextTertiary, 0.17f);
                    if (focused) color.A *= _hover >= 0 ? 0.3f : 0.55f;
                    if (dimmed) color.A *= 0.3f;
                    width = 1f;
                }
                if (strong != (pass == 1)) continue;
                Vector2[] screen = _edgeScreen[e];
                float[] pts = edge.Points;
                for (int i = 0; i < screen.Length; i++) screen[i] = ToScreen(pts[i * 2], pts[i * 2 + 1]);
                if (edge.Any || fogEdge) DrawDashedPolyline(screen, color, width);
                else DrawPolyline(screen, color, width, true);
                if (strong) DrawArrowHead(screen, color, width);
            }
    }

    private void DrawDashedPolyline(Vector2[] points, Color color, float width)
    {
        const float dash = 6f, gap = 4f;
        float carried = 0;
        bool on = true;
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 a = points[i], b = points[i + 1];
            float length = a.DistanceTo(b);
            float at = 0;
            while (at < length)
            {
                float span = (on ? dash : gap) - carried;
                float end = Math.Min(length, at + span);
                if (on) DrawLine(a.Lerp(b, at / length), a.Lerp(b, end / length), color, width, true);
                carried += end - at;
                if (carried >= (on ? dash : gap) - 0.001f) { carried = 0; on = !on; }
                at = end;
            }
        }
    }

    private void DrawArrowHead(Vector2[] points, Color color, float width)
    {
        if (points.Length < 2 || _zoom < 0.45f) return;
        Vector2 tip = points[^1], back = points[^2];
        Vector2 dir = (tip - back).Normalized();
        if (dir == Vector2.Zero) return;
        tip -= dir * (NodeRadius(false) + 2);
        float size = 5 + width;
        Vector2 side = new(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { tip, tip - dir * size + side * size * 0.55f, tip - dir * size - side * size * 0.55f }, color);
    }

    private void DrawNodes()
    {
        ResearchRadialLayout layout = _layout!;
        float r = NodeRadius(false);
        var view = new Rect2(-30, -30, Size.X + 60, Size.Y + 60);

        // undiscovered: faint dots, never named
        Color fog = UiTokens.WithAlpha(UiTokens.TextTertiary, 0.18f);
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Undiscovered || _looks[p].Visible) continue;
            Vector2 c = ToScreen(layout.X[p], layout.Y[p]);
            if (!view.HasPoint(c)) continue;
            if (_fogRequired[p])
            {
                // a prerequisite the player has not found yet: unnamed, but plainly there
                float fr = Math.Max(5f, r * 0.9f);
                DrawCircle(c, fr, UiTokens.WithAlpha(UiTokens.Surface2, 0.9f));
                DrawArc(c, fr, 0, Mathf.Tau, 24, UiTokens.WithAlpha(UiTokens.Accent, 0.9f), _unmetDirect[p] ? 2.5f : 1.5f, true);
                if (_zoom >= 0.5f) DrawString(UiTheme.Medium, c + new Vector2(-r * 0.3f, r * 0.38f), "?", HorizontalAlignment.Left, -1, (int)(r * 1.2f), UiTokens.Accent);
            }
            else if (_breadth[p]) DrawCircle(c, Math.Max(2f, r * 0.45f), UiTokens.WithAlpha(UiTokens.Accent, 0.45f));
            else DrawCircle(c, Math.Max(1.5f, r * 0.3f), fog);
        }

        // quiet nodes first, the focus chain above them, the focus itself last
        for (int pass = 0; pass < 3; pass++)
            for (int p = 0; p < _looks.Length; p++)
            {
                if (!_looks[p].Visible) continue;
                int want = p == _focus || p == _selected || p == _hover ? 2 : _focus >= 0 && InFocus(p) || _onPath[p] ? 1 : 0;
                if (want != pass) continue;
                Vector2 c = ToScreen(layout.X[p], layout.Y[p]);
                if (!view.HasPoint(c)) continue;
                bool gate = layout.Gate[p];
                float alpha = 1f;
                if (_focus >= 0 && !InFocus(p) && !_onPath[p]) alpha = OutOfFocusAlpha;
                if (_looks[p].Filtered) alpha = Math.Min(alpha, 0.22f);
                if (_searching && !_looks[p].Match) alpha = Math.Min(alpha, 0.28f);
                // a turning point never fades to a ghost: its label stays readable, so the mark must match it
                if (gate) alpha = _searching && !_looks[p].Match ? 0.45f : Math.Max(alpha, 0.6f);
                if (pass == 2 || _onPath[p]) alpha = 1f;
                float radius = NodeRadius(gate);
                int theme = gate ? 3 : (int)_catalog!.Projects[p].Theme;
                if (_searching && _looks[p].Match)
                    DrawArc(c, radius + 6, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.85f), 2f, true);
                PaintNode(this, c, radius, _looks[p], theme, gate, alpha, _zoom >= 0.7f);
                if (_breadth[p] && !IsKnown(p))
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.6f), 1.5f, true);
                if (_unmetDirect[p] && !_onPath[p])
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.9f), 2f, true);
                if (_onPath[p] && p != _nextStep)
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.9f), 2f, true);
                if (p == _nextStep)
                {
                    float wave = UiSettings.Current.ReduceMotion ? 0.5f : _pulse / 1.6f;
                    DrawArc(c, radius + 4 + 8 * wave, 0, Mathf.Tau, 40, UiTokens.WithAlpha(UiTokens.Accent, 0.95f * (1 - wave)), 2f, true);
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.Accent, 2f, true);
                }
                if (gate && _looks[p].State is ProjectState.Available && !_searching)
                {
                    float breath = UiSettings.Current.ReduceMotion ? 0.5f : _pulse / 1.6f;
                    DrawArc(c, radius * 1.65f + 6 * breath, 0, Mathf.Tau, 48, UiTokens.WithAlpha(GateColor, 0.7f * (1 - breath)), 2.5f, true);
                }
                if (p == _hover && p != _selected)
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.TextPrimary, 0.6f), 1.5f, true);
                if (p == _selected)
                {
                    DrawArc(c, radius + 5, 0, Mathf.Tau, 36, UiTokens.Accent, 2.5f, true);
                    if (HasFocus()) DrawArc(c, radius + 9, 0, Mathf.Tau, 36, UiTokens.WithAlpha(UiTokens.Accent, 0.35f), 1.5f, true);
                }
            }
    }

    /// <summary>Draws one project node: shared by the map and its key.</summary>
    public static void PaintNode(CanvasItem ci, Vector2 c, float r, in RadialNodeLook look, int theme, bool gate, float alpha, bool icons)
    {
        var t = (ResearchTheme)Math.Clamp(theme, 0, 3);
        Color baseColor = UiTokens.ThemeColor(t), fill = UiTokens.ThemeFill(t);
        Color A(Color color, float a = 1f) => UiTokens.WithAlpha(color, color.A * a * alpha);

        if (gate)
        {
            // a turning point is the biggest, brightest mark on the map: a glowing gold diamond inside a second outline, whatever the zoom.
            // The one that opens the next age is at full strength; ones already passed or still far ahead step back, so thirteen of them do not shout at once.
            float s = r * 1.15f;
            bool known = look.State is ProjectState.Completed or ProjectState.Dormant;
            bool ready = look.State is ProjectState.Available or ProjectState.Slotted;
            bool next = look.TurnsAge && !known;
            Vector2[] Diamond(float k) => new[] { c + new Vector2(0, -s * k), c + new Vector2(s * k, 0), c + new Vector2(0, s * k), c + new Vector2(-s * k, 0), c + new Vector2(0, -s * k) };
            if (!known) ci.DrawColoredPolygon(Diamond(1.65f)[..4], A(GateColor, ready ? 0.30f : next ? 0.30f : 0.12f));
            ci.DrawPolyline(Diamond(1.34f), A(GateColor, known ? 0.3f : next ? 1f : 0.45f), 1.3f, true);
            Vector2[] diamond = Diamond(1f);
            ci.DrawColoredPolygon(diamond[..4], A(known ? GateColor : UiTokens.ThemeFill(ResearchTheme.Keystone)));
            ci.DrawPolyline(diamond, A(GateColor, known || next ? 1f : 0.7f), ready && next ? 3.5f : known ? 2f : 2.5f, true);
            if (look.Progress > 0.001f && !known)
                ci.DrawArc(c, s + 7, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Math.Clamp(look.Progress, 0, 1), 40, A(UiTokens.Accent), 3.5f, true);
            if (r >= 8)
                UiDraw.Glyph(ci, known ? UiGlyph.Check : UiGlyph.Gem, new Rect2(c - new Vector2(r * 0.62f, r * 0.62f), new Vector2(r * 1.24f, r * 1.24f)),
                    A(known ? UiTokens.TextOnAccent : ready ? UiTokens.TextPrimary : GateColor), 1.8f);
            if (!known && !ready && r >= 8)
            {
                // still locked: a lock on the upper corner, clear of the ring that runs through the gate, so "not yet" reads without opening anything
                Vector2 at = c + new Vector2(s * 0.9f, -s * 0.9f);
                ci.DrawCircle(at, 7f, A(UiTokens.Surface2));
                ci.DrawArc(at, 7f, 0, Mathf.Tau, 20, A(GateColor), 1.5f, true);
                UiDraw.Glyph(ci, UiGlyph.Lock, new Rect2(at - new Vector2(4.5f, 4.5f), new Vector2(9, 9)), A(GateColor), 1.3f);
            }
            return;
        }

        switch (look.State)
        {
            case ProjectState.Completed:
                ci.DrawCircle(c, r, A(baseColor, 0.9f));
                if (icons && r >= 7) UiDraw.Glyph(ci, UiGlyph.Check, new Rect2(c - new Vector2(r * 0.6f, r * 0.6f), new Vector2(r * 1.2f, r * 1.2f)), A(UiTokens.TextOnAccent), 1.8f);
                break;
            case ProjectState.Dormant:
                ci.DrawCircle(c, r, A(UiTokens.WarningDark));
                ci.DrawArc(c, r, 0, Mathf.Tau, 28, A(UiTokens.Warning), 1.5f, true);
                if (icons && r >= 7) UiDraw.Glyph(ci, UiGlyph.BlockedBadge, new Rect2(c - new Vector2(r * 0.65f, r * 0.65f), new Vector2(r * 1.3f, r * 1.3f)), A(UiTokens.Warning), 1.5f);
                break;
            case ProjectState.Available:
                ci.DrawCircle(c, r + 3, A(baseColor, 0.18f));
                ci.DrawCircle(c, r, A(UiTokens.Surface2));
                ci.DrawArc(c, r, 0, Mathf.Tau, 28, A(baseColor), 2.5f, true);
                break;
            case ProjectState.Slotted:
                ci.DrawCircle(c, r, A(fill));
                ci.DrawArc(c, r, 0, Mathf.Tau, 28, A(baseColor), 2f, true);
                ci.DrawArc(c, r + 3.5f, 0, Mathf.Tau, 32, A(UiTokens.Border), 2.5f, true);
                if (look.Progress > 0.001f)
                    ci.DrawArc(c, r + 3.5f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Math.Clamp(look.Progress, 0, 1), 32, A(baseColor), 2.5f, true);
                break;
            default:
            {
                // locked (almost ready): dim, dotted outline and a lock while something before it is missing. Zoomed out the disc keeps
                // a floor of 5 px and the lock is drawn by hand at any size, so a locked project still reads as locked from afar.
                float lr = Math.Max(r, 5f);
                ci.DrawCircle(c, lr, A(UiTokens.Surface1, 0.95f));
                if (lr >= 7)
                {
                    int dots = 10;
                    for (int i = 0; i < dots; i++)
                    {
                        float a0 = Mathf.Tau * i / dots;
                        ci.DrawArc(c, lr, a0, a0 + Mathf.Tau / dots * 0.55f, 3, A(UiTokens.TextTertiary), 1.2f, true);
                    }
                }
                else ci.DrawArc(c, lr, 0, Mathf.Tau, 16, A(UiTokens.TextTertiary), 1f, true);
                if (icons && lr >= 7) UiDraw.Glyph(ci, UiGlyph.Lock, new Rect2(c - new Vector2(lr * 0.55f, lr * 0.55f), new Vector2(lr * 1.1f, lr * 1.1f)), A(UiTokens.TextTertiary), 1.3f);
                else PaintMiniLock(ci, c, lr, A(UiTokens.TextSecondary));
                break;
            }
        }

        if (look.State == ProjectState.Slotted && look.SlotRank is >= 0 and <= 2)
        {
            Vector2 at = c + new Vector2(r * 0.95f, -r * 0.95f);
            float badge = Math.Max(5.5f, Math.Min(9f, r * 0.8f));
            ci.DrawCircle(at, badge, A(GateColor));
            if (badge >= 7.5f)
            {
                Font font = UiTheme.NumBold;
                string digit = (look.SlotRank + 1).ToString();
                Vector2 extent = font.GetStringSize(digit, HorizontalAlignment.Left, -1, 12);
                ci.DrawString(font, at + new Vector2(-extent.X * 0.5f, 4.5f), digit, HorizontalAlignment.Left, -1, 12, A(UiTokens.TextOnAccent));
            }
        }
        else if (look.QueueIndex >= 0 && icons && r >= 7)
        {
            Font font = UiTheme.NumBold;
            string text = $"Q{look.QueueIndex + 1}";
            Vector2 at = c + new Vector2(r * 0.7f, r * 0.7f);
            Vector2 extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, 12);
            ci.DrawRect(new Rect2(at - new Vector2(2, 1), extent + new Vector2(4, 2)), A(UiTokens.Surface3));
            ci.DrawString(font, at + new Vector2(0, extent.Y - 3), text, HorizontalAlignment.Left, -1, 12, A(UiTokens.TextSecondary));
        }
        if (icons && r >= 9 && (look.RequiredForNextAge || look.OnBreadthPath))
        {
            Vector2 at = c + new Vector2(-r * 1.05f, -r * 1.05f);
            if (look.RequiredForNextAge) ci.DrawCircle(at, 3f, A(UiTokens.Info));
            else ci.DrawCircle(at, 2.5f, A(UiTokens.Accent, 0.8f));
        }
    }

    /// <summary>A padlock small enough for a 5 px disc: a body and a shackle, no glyph metrics.</summary>
    private static void PaintMiniLock(CanvasItem ci, Vector2 c, float r, Color color)
    {
        float w = r * 1.0f, h = r * 0.78f;
        var body = new Rect2(c.X - w * 0.5f, c.Y - h * 0.12f, w, h);
        ci.DrawRect(body, color);
        ci.DrawArc(new Vector2(c.X, body.Position.Y), w * 0.3f, Mathf.Pi, Mathf.Tau, 8, color, Math.Max(1f, r * 0.15f), true);
    }

    /// <summary>Expanding gold rings around the project a search or button just took the view to.</summary>
    private void DrawPing()
    {
        if (_pingProject < 0 || _layout is null || !_looks[_pingProject].Visible) return;
        Vector2 c = ToScreen(_layout.X[_pingProject], _layout.Y[_pingProject]);
        float baseR = NodeRadius(_layout.Gate[_pingProject]) + 8;
        float fade = 1f - Math.Clamp((_pingAge - PingSeconds * 0.6f) / (PingSeconds * 0.4f), 0f, 1f);
        if (UiSettings.Current.ReduceMotion)
        {
            DrawArc(c, baseR + 6, 0, Mathf.Tau, 40, UiTokens.WithAlpha(UiTokens.Accent, 0.9f * fade), 3f, true);
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            float t = (_pingAge * 1.1f - i * 0.28f) % 1f;
            if (_pingAge * 1.1f - i * 0.28f < 0) continue;
            DrawArc(c, baseR + 34 * t, 0, Mathf.Tau, 48, UiTokens.WithAlpha(UiTokens.Accent, 0.9f * (1 - t) * fade), 3f, true);
        }
    }

    /// <summary>While the turning point of this age is off screen: a gold tab on the edge of the view that points to it. A click goes there.</summary>
    private void DrawTurningPointPointer()
    {
        _edgeMarkerProject = -1;
        if (_layout is null || _searching) return;
        int gate = NextTurningPoint();
        if (gate < 0) return;
        Vector2 at = ToScreen(_layout.X[gate], _layout.Y[gate]);
        Rect2 room = Usable().Grow(-4);
        if (room.Grow(NodeRadius(true)).HasPoint(at)) return;

        Vector2 from = room.GetCenter();
        Vector2 dir = (at - from).Normalized();
        if (dir == Vector2.Zero) dir = Vector2.Up;
        Font font = UiTheme.Bold;
        string name = _catalog!.Projects[gate].Name;
        string text = name + "  " + (dir.Y < -0.6f ? "↑" : dir.Y > 0.6f ? "↓" : dir.X < 0 ? "←" : "→");
        Vector2 extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFont);
        var size = new Vector2(extent.X + 40, 28);
        // where the ray from the middle leaves the usable area, with the tab pulled wholly inside it
        Vector2 edge = from + dir * ExitDistance(from, dir, room);
        var box = new Rect2((edge - size * 0.5f).Clamp(room.Position, room.End - size), size);
        _edgeMarker = box;
        _edgeMarkerProject = gate;

        bool ready = _looks[gate].State is ProjectState.Available or ProjectState.Slotted;
        float glow = ready && !UiSettings.Current.ReduceMotion ? 0.5f + 0.5f * Mathf.Sin(_pulse / 1.6f * Mathf.Tau) : 0.5f;
        DrawStyleBox(UiTheme.Box(new Color("#2A251EF5"), 14, UiTokens.WithAlpha(GateColor, 0.55f + 0.4f * glow), 2), box);
        UiDraw.Glyph(this, UiGlyph.Gem, new Rect2(box.Position + new Vector2(8, 5), new Vector2(18, 18)), GateColor, 1.6f);
        DrawString(font, box.Position + new Vector2(32, 5 + font.GetAscent(LabelFont) + (18 - font.GetHeight(LabelFont)) * 0.5f), text, HorizontalAlignment.Left, -1, LabelFont, UiTokens.TextPrimary);
    }

    // ---- labels ------------------------------------------------------------------------------

    private int LabelPriority(int p)
    {
        if (p == _selected) return 1000;
        if (p == _hover) return 950;
        if (p == _nextStep) return 900;
        // turning points keep their names at every zoom: they are the landmarks of the whole tree
        if (_layout!.Gate[p])
        {
            bool known = _looks[p].State is ProjectState.Completed or ProjectState.Dormant;
            if (_searching && !_looks[p].Match) return 790;
            // the gate of this age is a landmark at every zoom; the ones behind and beyond it only name themselves up close
            return _looks[p].TurnsAge && !known ? 880 : known ? 560 : 480;
        }
        if (_onPath[p]) return 850;
        if (_searching && _looks[p].Match) return 800;
        if (_unmetDirect[p]) return 780;
        if (_focus >= 0 && (_upstream[p] || _downstream[p])) return 700;
        // what you can act on keeps its name at middle zoom; the rest steps back while something is in focus
        int rank = _looks[p].State switch
        {
            ProjectState.Slotted => 600,
            ProjectState.Available => 520,
            ProjectState.Glimpsed => 300,
            ProjectState.Dormant => 250,
            _ => 200,
        };
        if (_looks[p].Filtered || (_searching && !_looks[p].Match)) rank -= 150;
        else if (_focus >= 0 && rank < 500) rank -= 100;
        return rank;
    }

    private static readonly List<(int Project, int Priority)> LabelOrder = new();

    private void PlaceLabels()
    {
        _labelsDirty = false;
        _labelZoom = _zoom;
        _labels.Clear();
        _obstacles.Clear();
        ResearchRadialLayout layout = _layout!;
        int minimum = _zoom < 0.3f ? 840 : _zoom < 0.5f ? 500 : 0;

        LabelOrder.Clear();
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Visible) continue;
            float r = NodeRadius(layout.Gate[p]) + 1;
            Vector2 q = new Vector2(layout.X[p], layout.Y[p]) * _zoom;
            _obstacles.Add(new Rect2(q - new Vector2(r, r), new Vector2(r * 2, r * 2)));
            int priority = LabelPriority(p);
            if (priority >= minimum) LabelOrder.Add((p, priority));
        }
        LabelOrder.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : a.Project.CompareTo(b.Project));

        // the centre text is not to be covered
        float centre = ResearchRadialLayout.CenterRadius * _zoom;
        if (centre >= 52)
        {
            float half = Math.Min(centre * 0.85f, 130);
            float height = _centerSub.Length > 0 && centre >= 105 ? 80 : 22;
            _obstacles.Add(new Rect2(-half, -20, half * 2, height));
        }

        foreach ((int p, int priority) in LabelOrder)
        {
            bool gate = layout.Gate[p];
            float r = NodeRadius(gate) + (gate ? 4 : 3);
            Vector2 q = new Vector2(layout.X[p], layout.Y[p]) * _zoom;
            bool important = priority >= 850;
            float maxWidth = important ? 300 : 150;
            float cos = Mathf.Cos(layout.Angle[p]);
            bool preferRight = cos >= -0.15f;
            bool placed = false;
            for (int attempt = 0; attempt < (important ? 1 : 3) && !placed; attempt++)
            {
                float width = Math.Min(_fullWidth[p], attempt == 0 ? maxWidth : attempt == 1 ? 96 : 64);
                if (attempt > 0 && width >= _fullWidth[p]) break;
                var size = new Vector2(width + 8, LabelHeight);
                Rect2 right = new(q + new Vector2(r + 2, -size.Y * 0.5f), size);
                Rect2 left = new(q + new Vector2(-r - 2 - size.X, -size.Y * 0.5f), size);
                Rect2 below = new(q + new Vector2(-size.X * 0.5f, r + 1), size);
                Rect2 above = new(q + new Vector2(-size.X * 0.5f, -r - 1 - size.Y), size);
                Rect2[] candidates = preferRight ? new[] { right, below, above, left } : new[] { left, below, above, right };
                foreach (Rect2 box in candidates)
                {
                    if (Collides(box, q)) continue;
                    _labels.Add(new PlacedLabel(p, box, Fit(p, width)));
                    _obstacles.Add(box);
                    placed = true;
                    break;
                }
            }
            if (!placed && important)
            {
                float width = Math.Min(_fullWidth[p], maxWidth);
                var size = new Vector2(width + 8, LabelHeight);
                Rect2 box = preferRight ? new Rect2(q + new Vector2(r + 2, -size.Y * 0.5f), size) : new Rect2(q + new Vector2(-r - 2 - size.X, -size.Y * 0.5f), size);
                _labels.Add(new PlacedLabel(p, box, Fit(p, width)));
                _obstacles.Add(box);
            }
        }
        PlaceFieldLabels();
    }

    /// <summary>Field names just outside the outermost drawn ring, where they do not cover a project or its name.</summary>
    private void PlaceFieldLabels()
    {
        if (_zoom < 0.6f) return;
        ResearchRadialLayout layout = _layout!;
        Font font = UiTheme.Medium;
        float radius = (DrawnOuter() - ResearchRadialLayout.RingGap) * _zoom + 6;
        foreach (ResearchRadialLayout.Wedge wedge in layout.Wedges)
        {
            bool known = false;
            for (int p = 0; p < _looks.Length && !known; p++)
                known = _looks[p].Visible && !layout.Gate[p] && _catalog!.Projects[p].Lineage == wedge.Lineage;
            if (!known || (uint)wedge.Lineage >= (uint)_fieldNames.Length) continue;
            string name = _fieldNames[wedge.Lineage];
            float angle = (wedge.Start + wedge.End) * 0.5f;
            var size = new Vector2(font.GetStringSize(name, HorizontalAlignment.Left, -1, LabelFont).X + 8, LabelHeight);
            Vector2 anchor = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var box = new Rect2(AnchorBox(anchor, angle, size), size);
            if (Collides(box, new Vector2(float.NaN, float.NaN))) continue;
            _labels.Add(new PlacedLabel(-1 - wedge.Lineage, box, name));
            _obstacles.Add(box);
        }
    }

    private bool Collides(Rect2 box, Vector2 selfAt)
    {
        Rect2 shrunk = box.Grow(-1);
        foreach (Rect2 other in _obstacles)
        {
            if (!shrunk.Intersects(other)) continue;
            // a node's own disc is not in the way of its label
            if (Math.Abs(other.GetCenter().X - selfAt.X) < 0.01f && Math.Abs(other.GetCenter().Y - selfAt.Y) < 0.01f) continue;
            return true;
        }
        return false;
    }

    private string Fit(int p, float width)
    {
        string name = _catalog!.Projects[p].Name;
        if (_fullWidth[p] <= width + 0.5f) return name;
        bool gate = _layout!.Gate[p];
        if (gate) width -= GateLabelPad;
        long key = ((long)p << 16) | (long)Math.Clamp((int)width, 0, 0xFFFF);
        if (_truncated.TryGetValue(key, out string? cached)) return cached;
        Font font = gate ? UiTheme.Bold : UiTheme.Medium;
        int lo = 1, hi = name.Length - 1, best = 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            float w = font.GetStringSize(name[..mid].TrimEnd() + "…", HorizontalAlignment.Left, -1, LabelFont).X;
            if (w <= width) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        string text = name[..best].TrimEnd() + "…";
        _truncated[key] = text;
        return text;
    }

    /// <summary>Beside a focused turning point: how many of the age's projects it still wants.</summary>
    private void DrawBreadthCounter()
    {
        if (_breadthEra < 0 || _focus < 0 || !_looks[_focus].Visible || _layout is null) return;
        int done = Math.Min(_looks[_focus].BreadthDone, _breadthMin);
        int pathLeft = _looks[_focus].PathLeft;
        string text = pathLeft > 0
            ? $"{done} of {_breadthMin} {UiText.AgeName(_breadthEra)} projects · {pathLeft} on the path"
            : $"{done} of {_breadthMin} {UiText.AgeName(_breadthEra)} projects · ringed";
        Font font = UiTheme.Medium;
        Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFont);
        Vector2 c = ToScreen(_layout.X[_focus], _layout.Y[_focus]);
        var box = new Rect2(c + new Vector2(-size.X * 0.5f - 8, NodeRadius(true) + 12), size + new Vector2(16, 8));
        Rect2 room = Usable();
        if (box.End.Y > room.End.Y) box.Position = new Vector2(box.Position.X, c.Y - NodeRadius(true) - 12 - box.Size.Y);
        box.Position = new Vector2(Math.Clamp(box.Position.X, room.Position.X, Math.Max(room.Position.X, room.End.X - box.Size.X)), Math.Clamp(box.Position.Y, room.Position.Y, Math.Max(room.Position.Y, room.End.Y - box.Size.Y)));
        DrawStyleBox(LabelBoxSelected, box);
        DrawString(font, box.Position + new Vector2(8, 4 + font.GetAscent(LabelFont)), text, HorizontalAlignment.Left, -1, LabelFont, UiTokens.TextPrimary);
    }

    private void DrawLabels()
    {
        Font font = UiTheme.Medium;
        float ascent = font.GetAscent(LabelFont);
        Vector2 shift = ToScreen(0, 0);
        var view = new Rect2(Vector2.Zero, Size);
        foreach (PlacedLabel label in _labels)
        {
            var box = new Rect2(label.Box.Position + shift, label.Box.Size);
            if (!view.Intersects(box)) continue;
            int p = label.Project;
            if (p < 0)
            {
                DrawString(font, box.Position + new Vector2(4, (LabelHeight - font.GetHeight(LabelFont)) * 0.5f + ascent), label.Text,
                    HorizontalAlignment.Left, -1, LabelFont, UiTokens.WithAlpha(UiTokens.TextTertiary, 0.9f));
                continue;
            }
            bool selected = p == _selected || p == _hover;
            bool path = _onPath[p] || p == _nextStep;
            float alpha = 1f;
            if (_focus >= 0 && !InFocus(p) && !path) alpha = Math.Min(1f, OutOfFocusAlpha + 0.15f);
            if (_looks[p].Filtered) alpha = Math.Min(alpha, 0.35f);
            if (_searching && !_looks[p].Match) alpha = Math.Min(alpha, 0.4f);
            if (selected || path) alpha = 1f;
            bool gateLabel = _layout!.Gate[p];
            bool knownGate = gateLabel && _looks[p].State is ProjectState.Completed or ProjectState.Dormant;
            bool majorGate = gateLabel && !knownGate && (_looks[p].TurnsAge || selected);
            if (gateLabel) alpha = _searching && !_looks[p].Match ? 0.45f : Math.Max(alpha, 0.6f);
            if (alpha < 1f && !majorGate) DrawRect(box, UiTokens.WithAlpha(LabelBox.BgColor, 0.75f * alpha));
            else DrawStyleBox(selected ? LabelBoxSelected : majorGate ? LabelBoxGate : path ? LabelBoxPath : LabelBox, box);
            ProjectState state = _looks[p].State;
            Color text = state is ProjectState.Available or ProjectState.Slotted || selected || path || _layout!.Gate[p]
                ? UiTokens.TextPrimary
                : state == ProjectState.Dormant ? UiTokens.Warning : UiTokens.TextSecondary;
            if (p == _nextStep) text = UiTokens.Accent;
            text.A *= alpha;
            Font face = gateLabel ? UiTheme.Bold : font;
            float indent = 4;
            if (gateLabel && !knownGate)
            {
                UiDraw.Glyph(this, UiGlyph.Gem, new Rect2(box.Position + new Vector2(3, 2), new Vector2(15, 15)), UiTokens.WithAlpha(GateColor, alpha * (majorGate ? 1f : 0.7f)), 1.4f);
                indent = 4 + GateLabelPad;
            }
            Color gateText = knownGate ? UiTokens.WithAlpha(UiTokens.TextTertiary, alpha) : UiTokens.WithAlpha(GateColor, alpha * (majorGate ? 1f : 0.75f));
            DrawString(face, box.Position + new Vector2(indent, (LabelHeight - face.GetHeight(LabelFont)) * 0.5f + face.GetAscent(LabelFont)), label.Text,
                HorizontalAlignment.Left, -1, LabelFont, gateLabel ? gateText : text);
        }
    }

    // ---- key ---------------------------------------------------------------------------------

    private void SetLegendVisible(bool visible)
    {
        _legend.Visible = visible;
        _legendButton.SetPressedNoSignal(visible);
    }

    private PanelContainer BuildLegend()
    {
        PanelContainer panel = UiKit.Panel("HudPanel", "MapKey");
        panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
        panel.GrowVertical = GrowDirection.Begin;
        panel.OffsetLeft = UiTokens.Sp2;
        panel.OffsetBottom = -UiTokens.Sp2;
        panel.OffsetTop = -UiTokens.Sp2;
        var column = UiKit.Column(UiTokens.Sp1);
        column.MouseFilter = MouseFilterEnum.Ignore;
        var head = UiKit.Row(UiTokens.Sp2);
        head.AddChild(UiKit.Text("KEY", "EyebrowTertiary"));
        head.AddChild(UiKit.Text("Rings are ages, Dawn in the middle · sectors are tracks · lines run outward to what they unlock", "CaptionTertiary"));
        head.AddChild(UiKit.Spacer(expand: true));
        head.AddChild(UiKit.IconButton(UiGlyph.Close, "Hide the key", () => SetLegendVisible(false), 22, 12));
        column.AddChild(head);

        var grid = new GridContainer { Columns = 10, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", UiTokens.Sp1);
        grid.AddThemeConstantOverride("v_separation", 0);
        void Item(KeySwatch.Kind kind, string text)
        {
            grid.AddChild(new KeySwatch(kind));
            Label label = UiKit.Text(text, "Caption");
            label.CustomMinimumSize = new Vector2(0, 20);
            grid.AddChild(UiKit.Margin(label, 0, 0, UiTokens.Sp2, 0));
        }
        Item(KeySwatch.Kind.Known, "Known");
        Item(KeySwatch.Kind.Ready, "Ready");
        Item(KeySwatch.Kind.Slotted, "In slot 1–3");
        Item(KeySwatch.Kind.Almost, "Locked");
        Item(KeySwatch.Kind.Blocked, "Blocked");
        Item(KeySwatch.Kind.Gate, "Turning point");
        Item(KeySwatch.Kind.Required, "Required");
        Item(KeySwatch.Kind.AnyOf, "Any one of");
        Item(KeySwatch.Kind.NextStep, "Missing step");
        Item(KeySwatch.Kind.Fog, "Undiscovered");
        column.AddChild(grid);
        panel.AddChild(column);
        return panel;
    }

    /// <summary>A small sample of a node or line style for the key.</summary>
    private sealed partial class KeySwatch : Control
    {
        public enum Kind { Known, Ready, Slotted, Almost, Blocked, Gate, Required, AnyOf, NextStep, Fog }
        private readonly Kind _kind;

        public KeySwatch(Kind kind)
        {
            _kind = kind;
            CustomMinimumSize = new Vector2(30, 20);
            MouseFilter = MouseFilterEnum.Ignore;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        public override void _Draw()
        {
            Vector2 c = Size * 0.5f;
            var look = new RadialNodeLook { Visible = true, SlotRank = -1, QueueIndex = -1 };
            switch (_kind)
            {
                case Kind.Known: look.State = ProjectState.Completed; PaintNode(this, c, 7, look, 0, false, 1, true); break;
                case Kind.Ready: look.State = ProjectState.Available; PaintNode(this, c, 7, look, 1, false, 1, true); break;
                case Kind.Slotted: look.State = ProjectState.Slotted; look.SlotRank = 0; look.Progress = 0.4f; PaintNode(this, c, 6, look, 2, false, 1, true); break;
                case Kind.Almost: look.State = ProjectState.Glimpsed; PaintNode(this, c, 7, look, 0, false, 1, true); break;
                case Kind.Blocked: look.State = ProjectState.Dormant; PaintNode(this, c, 7, look, 0, false, 1, true); break;
                case Kind.Gate: look.State = ProjectState.Glimpsed; PaintNode(this, c, 6, look, 3, true, 1, false); break;
                case Kind.Required: DrawLine(new Vector2(2, c.Y), new Vector2(Size.X - 2, c.Y), UiTokens.TextSecondary, 2f, true); break;
                case Kind.AnyOf:
                    for (float x = 2; x < Size.X - 2; x += 8) DrawLine(new Vector2(x, c.Y), new Vector2(Math.Min(x + 5, Size.X - 2), c.Y), UiTokens.Info, 2f, true);
                    break;
                case Kind.NextStep:
                    DrawLine(new Vector2(2, c.Y), new Vector2(Size.X - 2, c.Y), UiTokens.Accent, 3f, true);
                    break;
                case Kind.Fog: DrawCircle(c, 2.5f, UiTokens.WithAlpha(UiTokens.TextTertiary, 0.45f)); break;
            }
        }
    }
}
