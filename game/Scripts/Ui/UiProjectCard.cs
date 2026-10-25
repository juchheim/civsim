using System;
using CivSim.Sim.Research;
using Godot;
using ResearchTheme = CivSim.Sim.Research.Theme;

namespace CivSim.Game.Ui;

public enum ProjectCardMode { Card, Row, Chip }

/// <summary>How a project looks in the research web, resolved from the frame by the caller.</summary>
public struct ProjectLook
{
    public ProjectState State;
    public int SlotRank;          // 0..2 when in a slot, else -1
    public int QueueIndex;        // 0-based position in the queue, else -1
    public float Progress;        // 0..1 when in a slot
    public bool HiddenPrerequisite;
    public bool Selected;
    public bool Related;
    public bool OnAgePath;
    public bool OnBreadthPath;
    public string Line2, Line3, Sub;
}

/// <summary>
/// A project as a card (89 high), a list row or a chip (36 high). Built once and re-bound: it owns its labels, draws its own
/// state border, and never allocates after construction.
/// </summary>
public partial class UiProjectCard : Button
{
    private static readonly StyleBoxFlat[] ReadyBorder = MakeBorders(1, true), SlottedBorder = MakeBorders(2, true);
    private static readonly StyleBoxFlat Known = FlatBox(null, UiTokens.Border, 1), Almost = FlatBox(null, UiTokens.TextTertiary, 1);
    private static readonly StyleBoxFlat Blocked = FlatBox(UiTokens.WithAlpha(UiTokens.WarningDark, 0.5f), UiTokens.Warning, 1);
    private static readonly StyleBoxFlat SelectedRing = FlatBox(null, UiTokens.Accent, 2);
    private static readonly StyleBoxFlat RelatedRing = FlatBox(null, UiTokens.WithAlpha(UiTokens.Accent, 0.6f), 1);
    private static readonly StyleBoxFlat HoverFill = FlatBox(UiTokens.Hover, new Color(0, 0, 0, 0), 0);

    private readonly ProjectCardMode _mode;
    private readonly UiIcon _themeIcon = new(UiGlyph.Hammer, 16, UiTokens.TextPrimary);
    private readonly UiIcon _stateIcon = new(UiGlyph.Ring, 16, UiTokens.TextPrimary);
    private readonly UiIcon _agePathIcon = new(UiGlyph.Star, 14, UiTokens.Accent) { Visible = false };
    private readonly UiRankBadge[] _ranks = { new(1), new(2), new(3) };
    private readonly Label _name = UiKit.Text("", "BodyStrong", ellipsis: true);
    private readonly Label _line2 = UiKit.Text("", "Caption", ellipsis: true);
    private readonly Label _line3 = UiKit.Text("", "CaptionTertiary", ellipsis: true);
    private readonly Label _sub = UiKit.Text("", "Caption", ellipsis: true);
    private readonly Label _queue = UiKit.Text("", "Num", UiTokens.TextSecondary);
    private readonly UiProgressBar _bar = new(4);
    private ProjectLook _look;
    private ResearchTheme _theme;

    public int Project { get; private set; } = -1;

    public UiProjectCard(ProjectCardMode mode)
    {
        _mode = mode;
        FocusMode = FocusModeEnum.All;
        ThemeTypeVariation = "CardButton";
        ClipContents = true;
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", UiTokens.Sp3);
        margin.AddThemeConstantOverride("margin_right", UiTokens.Sp3);
        margin.AddThemeConstantOverride("margin_top", mode == ProjectCardMode.Card ? 10 : 2);
        margin.AddThemeConstantOverride("margin_bottom", mode == ProjectCardMode.Card ? 8 : 2);
        AddChild(margin);

        foreach (UiRankBadge badge in _ranks) badge.Visible = false;
        _queue.Visible = false;
        _bar.Visible = false;

        if (mode == ProjectCardMode.Card)
        {
            CustomMinimumSize = new Vector2(0, 89);
            var column = UiKit.Column(2);
            column.MouseFilter = MouseFilterEnum.Ignore;
            var top = UiKit.Row(UiTokens.Sp2);
            top.MouseFilter = MouseFilterEnum.Ignore;
            top.AddChild(_themeIcon);
            top.AddChild(_name);
            top.AddChild(_queue);
            top.AddChild(_stateIcon);
            top.AddChild(_agePathIcon);
            foreach (UiRankBadge badge in _ranks) top.AddChild(badge);
            column.AddChild(top);
            column.AddChild(_line2);
            _line3.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.AddChild(_line3);
            column.AddChild(_bar);
            margin.AddChild(column);
        }
        else
        {
            CustomMinimumSize = new Vector2(mode == ProjectCardMode.Chip ? 144 : 0, UiTokens.ControlS);
            var row = UiKit.Row(UiTokens.Sp2);
            row.MouseFilter = MouseFilterEnum.Ignore;
            if (mode == ProjectCardMode.Chip) row.AddChild(_themeIcon);
            row.AddChild(_stateIcon);
            row.AddChild(_agePathIcon);
            row.AddChild(_name);
            _sub.HorizontalAlignment = HorizontalAlignment.Right;
            if (mode == ProjectCardMode.Row)
            {
                _sub.CustomMinimumSize = new Vector2(140, 0);
                _sub.SizeFlagsStretchRatio = 1.4f;
                row.AddChild(_sub);
            }
            row.AddChild(_queue);
            foreach (UiRankBadge badge in _ranks) row.AddChild(badge);
            margin.AddChild(row);
        }
    }

    private static StyleBoxFlat FlatBox(Color? fill, Color border, int width)
    {
        var box = new StyleBoxFlat { BgColor = fill ?? new Color(0, 0, 0, 0), DrawCenter = fill is not null, BorderColor = border };
        box.SetBorderWidthAll(width);
        box.SetCornerRadiusAll(UiTokens.R1);
        return box;
    }

    private static StyleBoxFlat[] MakeBorders(int width, bool filled)
    {
        var boxes = new StyleBoxFlat[4];
        for (int i = 0; i < 4; i++)
        {
            ResearchTheme theme = (ResearchTheme)i;
            boxes[i] = FlatBox(width == 1 ? UiTokens.Surface2 : UiTokens.ThemeFill(theme), UiTokens.ThemeColor(theme), width);
        }
        return boxes;
    }

    public void SetRelated(bool related)
    {
        if (_look.Related == related) return;
        _look.Related = related;
        QueueRedraw();
    }

    public void Bind(ProjectDef def, in ProjectLook look)
    {
        bool projectChanged = Project != def.Index;
        Project = def.Index;
        _theme = def.Theme;
        _look = look;
        if (projectChanged)
        {
            _name.Text = def.Name;
            _themeIcon.Glyph = UiDraw.ThemeGlyph(def.Theme);
            _themeIcon.Tint = UiTokens.ThemeText(def.Theme);
        }
        if (_mode == ProjectCardMode.Card)
        {
            UiKit.SetText(_line2, look.Line2);
            UiKit.SetText(_line3, look.Line3);
        }
        else if (_mode == ProjectCardMode.Row) UiKit.SetText(_sub, look.Sub);

        bool quiet = look.State is ProjectState.Glimpsed or ProjectState.Completed;
        UiKit.SetVariation(_name, quiet ? "BodyStrongSecondary" : "BodyStrong");

        UiGlyph glyph;
        Color tint;
        switch (look.State)
        {
            case ProjectState.Available: glyph = UiGlyph.Ring; tint = UiTokens.ThemeColor(def.Theme); break;
            case ProjectState.Slotted: glyph = UiGlyph.None; tint = UiTokens.ThemeColor(def.Theme); break;
            case ProjectState.Completed: glyph = UiGlyph.CheckBadge; tint = UiTokens.Success; break;
            case ProjectState.Dormant: glyph = UiGlyph.BlockedBadge; tint = UiTokens.Warning; break;
            default: glyph = look.HiddenPrerequisite ? UiGlyph.Lock : UiGlyph.HalfRing; tint = UiTokens.TextTertiary; break;
        }
        _stateIcon.Glyph = glyph;
        _stateIcon.Tint = tint;
        _stateIcon.Visible = glyph != UiGlyph.None;
        _agePathIcon.Visible = look.OnAgePath;
        _agePathIcon.Glyph = look.OnBreadthPath ? UiGlyph.StarOutline : UiGlyph.Star;
        _agePathIcon.TooltipText = look.OnBreadthPath
            ? "Counts toward the next age's breadth"
            : "Part of the path to the next age";
        TooltipText = _agePathIcon.Visible ? _agePathIcon.TooltipText : "";

        for (int i = 0; i < 3; i++) _ranks[i].Visible = look.State == ProjectState.Slotted && look.SlotRank == i;
        bool queued = look.QueueIndex >= 0 && look.State != ProjectState.Slotted;
        _queue.Visible = queued;
        if (queued) UiKit.SetText(_queue, $"Q{look.QueueIndex + 1}");

        bool showBar = _mode == ProjectCardMode.Card && look.State == ProjectState.Slotted;
        _bar.Visible = showBar;
        _line3.Visible = !showBar;
        if (showBar)
        {
            _bar.Fill = UiTokens.ThemeColor(def.Theme);
            _bar.SetValue(look.Progress, false);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        int theme = Mathf.Clamp((int)_theme, 0, 3);
        switch (_look.State)
        {
            case ProjectState.Available: DrawStyleBox(ReadyBorder[theme], rect); break;
            case ProjectState.Slotted: DrawStyleBox(SlottedBorder[theme], rect); break;
            case ProjectState.Completed: DrawStyleBox(Known, rect); break;
            case ProjectState.Dormant: DrawStyleBox(Blocked, rect); break;
            default:
                // Dotted border for "almost ready"
                DrawDashedLine(new Vector2(4, 0.5f), new Vector2(rect.Size.X - 4, 0.5f), UiTokens.TextTertiary, 1.0f, 3.0f);
                DrawDashedLine(new Vector2(4, rect.Size.Y - 0.5f), new Vector2(rect.Size.X - 4, rect.Size.Y - 0.5f), UiTokens.TextTertiary, 1.0f, 3.0f);
                DrawDashedLine(new Vector2(0.5f, 4), new Vector2(0.5f, rect.Size.Y - 4), UiTokens.TextTertiary, 1.0f, 3.0f);
                DrawDashedLine(new Vector2(rect.Size.X - 0.5f, 4), new Vector2(rect.Size.X - 0.5f, rect.Size.Y - 4), UiTokens.TextTertiary, 1.0f, 3.0f);
                break;
        }
        if (_look.Related && !_look.Selected) DrawStyleBox(RelatedRing, rect);
        if (GetDrawMode() is DrawMode.Hover or DrawMode.HoverPressed) DrawStyleBox(HoverFill, rect);
        if (_look.Selected) DrawStyleBox(SelectedRing, rect);
    }
}
