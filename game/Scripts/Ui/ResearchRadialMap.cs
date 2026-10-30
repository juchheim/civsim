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

    public Action<int>? ProjectClicked;
    public Action<int>? ProjectActivated;
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

    // overlays
    private readonly Label _status;
    private readonly PanelContainer _statusPill;
    private readonly PanelContainer _legend;
    private readonly UiButton _legendButton;

    private static readonly StyleBoxFlat WellBox = UiTheme.Box(UiTokens.SurfaceSunken, UiTokens.R1, null, 0);
    private static readonly StyleBoxFlat LabelBox = UiTheme.Box(new Color("#1F1B16E0"), 4, null, 0);
    private static readonly StyleBoxFlat LabelBoxSelected = UiTheme.Box(new Color("#2A251EF2"), 4, UiTokens.Accent, 1);
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

        PanelContainer toolPill = UiKit.Panel("PillPanel", "MapTools");
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
        _legendButton = UiKit.Ghost("Key", () => SetLegendVisible(!_legend!.Visible), "Show or hide the key", UiGlyph.None, 32);
        _legendButton.ToggleMode = true;
        tools.AddChild(_legendButton);
        AddChild(toolPill);

        _statusPill = UiKit.Panel("PillPanel", "MapSearchStatus", UiKit.MouseFilter.Ignore);
        _statusPill.Position = new Vector2(UiTokens.Sp2, UiTokens.Sp2);
        _statusPill.Visible = false;
        _status = UiKit.Text("", "Caption");
        _statusPill.AddChild(_status);
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
        _fullWidth = new float[n];
        Font font = UiTheme.Medium;
        for (int p = 0; p < n; p++) _fullWidth[p] = font.GetStringSize(catalog.Projects[p].Name, HorizontalAlignment.Left, -1, LabelFont).X;
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
        if (searching)
            UiKit.SetText(_status, _matchCount == 0 ? "No known project matches" : _matchCount == 1 ? "1 match · Enter to jump to it" : $"{_matchCount} matches · Enter to jump to the first");
        _focus = int.MinValue;
        _labelsDirty = true;
        QueueRedraw();
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
            world = CenterFor((min + max) * 0.5f, zoom);
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
        if (_nextStep >= 0 && !UiSettings.Current.ReduceMotion)
        {
            _pulse = (_pulse + (float)delta) % 1.6f;
            busy = true;
        }
        if (busy && IsVisibleInTree()) QueueRedraw();
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
                        if (hit >= 0) { _doubleClicked = true; ProjectActivated?.Invoke(hit); }
                    }
                }
                else
                {
                    if (_pressed && !_dragging && !_doubleClicked && button.ButtonIndex == MouseButton.Left)
                    {
                        int hit = HitTest(button.Position);
                        if (hit >= 0) ProjectClicked?.Invoke(hit);
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
        float best = float.MaxValue;
        int found = -1;
        float reach = Math.Max(13f, NodeRadius(false) + 6);
        for (int p = 0; p < _looks.Length; p++)
        {
            if (!_looks[p].Visible) continue;
            float d = ToScreen(_layout.X[p], _layout.Y[p]).DistanceTo(screen);
            float limit = _layout.Gate[p] ? reach * 1.4f : reach;
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

    private bool InFocus(int p) => _focus < 0 || p == _focus || _upstream[p] || _downstream[p];

    /// <summary>Hovering is a question about one project, so it dims the rest harder than a standing selection does.</summary>
    private float OutOfFocusAlpha => _hover >= 0 ? 0.32f : 0.5f;

    // ---- drawing -----------------------------------------------------------------------------

    private float NodeRadius(bool gate) => Math.Clamp(8f * _zoom, 3f, 12f) * (gate ? 1.45f : 1f);

    public override void _Draw()
    {
        DrawStyleBox(WellBox, new Rect2(Vector2.Zero, Size));
        if (_layout is null || _catalog is null) return;
        RebuildHighlight();
        if (_nextStep >= 0 && !UiSettings.Current.ReduceMotion && !IsProcessing()) SetProcess(true);
        if (_labelsDirty || !Mathf.IsEqualApprox(_labelZoom, _zoom)) PlaceLabels();

        DrawRings();
        DrawEdges();
        DrawNodes();
        DrawLabels();
        DrawTrackNames();
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
            Color c = UiTokens.WithAlpha(GateColor, _looks[p].State is ProjectState.Completed ? 0.22f : 0.38f);
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
                if (!_looks[edge.From].Visible || !_looks[edge.To].Visible) continue;
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
                if (edge.Any) DrawDashedPolyline(screen, color, width);
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
            if (view.HasPoint(c)) DrawCircle(c, Math.Max(1.5f, r * 0.3f), fog);
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
                if (pass == 2 || _onPath[p]) alpha = 1f;
                float radius = NodeRadius(gate);
                int theme = gate ? 3 : (int)_catalog!.Projects[p].Theme;
                if (_searching && _looks[p].Match)
                    DrawArc(c, radius + 6, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.85f), 2f, true);
                PaintNode(this, c, radius, _looks[p], theme, gate, alpha, _zoom >= 0.7f);
                if (_onPath[p] && p != _nextStep)
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.WithAlpha(UiTokens.Accent, 0.9f), 2f, true);
                if (p == _nextStep)
                {
                    float wave = UiSettings.Current.ReduceMotion ? 0.5f : _pulse / 1.6f;
                    DrawArc(c, radius + 4 + 8 * wave, 0, Mathf.Tau, 40, UiTokens.WithAlpha(UiTokens.Accent, 0.95f * (1 - wave)), 2f, true);
                    DrawArc(c, radius + 4, 0, Mathf.Tau, 32, UiTokens.Accent, 2f, true);
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
            float s = r * 1.15f;
            Vector2[] diamond = { c + new Vector2(0, -s), c + new Vector2(s, 0), c + new Vector2(0, s), c + new Vector2(-s, 0), c + new Vector2(0, -s) };
            bool known = look.State is ProjectState.Completed or ProjectState.Dormant;
            ci.DrawColoredPolygon(diamond[..4], A(known ? GateColor : UiTokens.ThemeFill(ResearchTheme.Keystone)));
            ci.DrawPolyline(diamond, A(GateColor), look.State is ProjectState.Available or ProjectState.Slotted ? 2.5f : 1.5f, true);
            if (look.Progress > 0.001f && !known)
                ci.DrawArc(c, s + 5, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Math.Clamp(look.Progress, 0, 1), 40, A(UiTokens.Accent), 2.5f, true);
            if (icons && r >= 8)
                UiDraw.Glyph(ci, known ? UiGlyph.Check : UiGlyph.Gem, new Rect2(c - new Vector2(r * 0.55f, r * 0.55f), new Vector2(r * 1.1f, r * 1.1f)),
                    A(known ? UiTokens.TextOnAccent : GateColor), 1.5f);
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
                // almost ready: dim, dotted outline, a lock while something before it is missing
                ci.DrawCircle(c, r, A(UiTokens.Surface1, 0.9f));
                int dots = 10;
                for (int i = 0; i < dots; i++)
                {
                    float a0 = Mathf.Tau * i / dots;
                    ci.DrawArc(c, r, a0, a0 + Mathf.Tau / dots * 0.55f, 3, A(UiTokens.TextTertiary), 1.2f, true);
                }
                if (icons && r >= 7) UiDraw.Glyph(ci, UiGlyph.Lock, new Rect2(c - new Vector2(r * 0.55f, r * 0.55f), new Vector2(r * 1.1f, r * 1.1f)), A(UiTokens.TextTertiary), 1.3f);
                break;
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

    // ---- labels ------------------------------------------------------------------------------

    private int LabelPriority(int p)
    {
        if (p == _selected) return 1000;
        if (p == _hover) return 950;
        if (p == _nextStep) return 900;
        if (_onPath[p]) return 850;
        if (_searching && _looks[p].Match) return 800;
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
        if (_layout!.Gate[p]) rank = Math.Max(rank, 560);
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
        long key = ((long)p << 16) | (long)Math.Clamp((int)width, 0, 0xFFFF);
        if (_truncated.TryGetValue(key, out string? cached)) return cached;
        Font font = UiTheme.Medium;
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
            if (alpha < 1f) DrawRect(box, UiTokens.WithAlpha(LabelBox.BgColor, 0.75f * alpha));
            else DrawStyleBox(selected ? LabelBoxSelected : path ? LabelBoxPath : LabelBox, box);
            ProjectState state = _looks[p].State;
            Color text = state is ProjectState.Available or ProjectState.Slotted || selected || path || _layout!.Gate[p]
                ? UiTokens.TextPrimary
                : state == ProjectState.Dormant ? UiTokens.Warning : UiTokens.TextSecondary;
            if (p == _nextStep) text = UiTokens.Accent;
            text.A *= alpha;
            DrawString(font, box.Position + new Vector2(4, (LabelHeight - font.GetHeight(LabelFont)) * 0.5f + ascent), label.Text,
                HorizontalAlignment.Left, -1, LabelFont, text);
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
        Item(KeySwatch.Kind.Almost, "Almost ready");
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
