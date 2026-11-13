using Godot;

namespace CivSim.Game.Ui;

/// <summary>A list row (icon, one or two lines of text, optional chevron). Pooled; Set() re-binds without allocating controls.</summary>
public partial class UiRowButton : Button
{
    private readonly UiIcon _icon = new(UiGlyph.Dot, 16, UiTokens.TextSecondary);
    private readonly Label _text = UiKit.Text("", "Body", wrap: true);
    private readonly Label _meta = UiKit.Text("", "CaptionTertiary", ellipsis: true);
    private readonly UiIcon _chevron = new(UiGlyph.ChevronRight, 16, UiTokens.TextTertiary);
    private readonly MarginContainer _margin = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly float _minHeight;
    private bool _settled;
    public int Tile { get; private set; } = -1;
    public long Key { get; set; }

    public UiRowButton(int height = 56)
    {
        _minHeight = height;
        ThemeTypeVariation = "RowButton";
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(0, height);
        ClipContents = true;
        var margin = _margin;
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", UiTokens.Sp3);
        margin.AddThemeConstantOverride("margin_right", UiTokens.Sp2);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        var row = UiKit.Row(UiTokens.Sp2);
        row.MouseFilter = MouseFilterEnum.Ignore;
        _icon.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        row.AddChild(_icon);
        var column = UiKit.Column(0);
        column.MouseFilter = MouseFilterEnum.Ignore;
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _text.MaxLinesVisible = 2;
        _text.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_text);
        column.AddChild(_meta);
        row.AddChild(column);
        row.AddChild(_chevron);
        margin.AddChild(row);
        AddChild(margin);
        // The row grows with a wrapped two-line text so the date line under it is never clipped (ui-ux-pass2 §10).
        Resized += () => CallDeferred(nameof(FitHeight));
    }

    private void FitHeight()
    {
        // Count the wrapped lines from the font at the label's width (the label's own count lags a frame behind a re-bind).
        Font font = _text.GetThemeFont("font");
        int size = _text.GetThemeFontSize("font_size");
        float width = _text.Size.X > 1 ? _text.Size.X : Mathf.Max(1, Size.X - 60);
        float oneLine = font.GetHeight(size);
        float wrapped = font.GetMultilineStringSize(_text.Text, HorizontalAlignment.Left, width, size, 2).Y;
        int lines = Mathf.Clamp(Mathf.RoundToInt(wrapped / Mathf.Max(1, oneLine)), 1, 2);
        // Once the label has laid itself out, its own count is exact (the font estimate can be one line high at the edge).
        if (_settled && _text.GetLineCount() > 0) lines = Mathf.Clamp(_text.GetLineCount(), 1, 2);
        float text = lines * (_text.GetLineHeight() + _text.GetThemeConstant("line_spacing")) - _text.GetThemeConstant("line_spacing");
        float meta = _meta.Visible && _meta.Text.Length > 0 ? _meta.GetCombinedMinimumSize().Y : 0;
        float height = Mathf.Ceil(Mathf.Max(_minHeight, text + meta + 8 + 2));
        if (Mathf.Abs(CustomMinimumSize.Y - height) > 0.5f) CustomMinimumSize = new Vector2(CustomMinimumSize.X, height);
    }

    public void Set(UiGlyph glyph, Color tint, string text, string meta, int tile, bool dim)
    {
        _icon.Glyph = glyph;
        _icon.Tint = tint;
        UiKit.SetText(_text, text);
        UiKit.SetVariation(_text, dim ? "BodySecondary" : "Body");
        UiKit.SetText(_meta, meta);
        Tile = tile;
        _chevron.Visible = tile >= 0;
        _settled = false;
        CallDeferred(nameof(FitHeight));
        if (IsInsideTree()) GetTree().CreateTimer(0.05).Timeout += () => { _settled = true; FitHeight(); };
        TooltipText = tile >= 0 ? "Go to this event's location" : "";
    }
}
