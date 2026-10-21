using Godot;

namespace CivSim.Game.Ui;

/// <summary>A list row (icon, one or two lines of text, optional chevron). Pooled; Set() re-binds without allocating controls.</summary>
public partial class UiRowButton : Button
{
    private readonly UiIcon _icon = new(UiGlyph.Dot, 16, UiTokens.TextSecondary);
    private readonly Label _text = UiKit.Text("", "Body", wrap: true);
    private readonly Label _meta = UiKit.Text("", "CaptionTertiary", ellipsis: true);
    private readonly UiIcon _chevron = new(UiGlyph.ChevronRight, 16, UiTokens.TextTertiary);
    public int Tile { get; private set; } = -1;
    public long Key { get; set; }

    public UiRowButton(int height = 56)
    {
        ThemeTypeVariation = "RowButton";
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(0, height);
        ClipContents = true;
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
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
        TooltipText = tile >= 0 ? "Go to this event's location" : "";
    }
}
