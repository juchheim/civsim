using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>A button that can draw a glyph beside (or instead of) its text.</summary>
public partial class UiButton : Button
{
    private UiGlyph _glyph;
    private int _glyphSize = 16;
    private Color? _glyphColor;
    private float _rotation;

    public UiGlyph Glyph { get => _glyph; set { if (_glyph != value) { _glyph = value; QueueRedraw(); } } }
    public int GlyphSize { get => _glyphSize; set { _glyphSize = value; QueueRedraw(); } }
    public Color? GlyphColor { get => _glyphColor; set { _glyphColor = value; QueueRedraw(); } }
    public bool GlyphTop { get; set; }
    public float GlyphRotation { get => _rotation; set { if (!Mathf.IsEqualApprox(_rotation, value)) { _rotation = value; QueueRedraw(); } } }

    public UiButton()
    {
        FocusMode = FocusModeEnum.None;
    }

    public override void _Draw()
    {
        if (_glyph == UiGlyph.None) return;
        string colorName = GetDrawMode() switch
        {
            DrawMode.Disabled => "font_disabled_color",
            DrawMode.Pressed => "font_pressed_color",
            DrawMode.HoverPressed => "font_hover_pressed_color",
            DrawMode.Hover => "font_hover_color",
            _ => "font_color",
        };
        Color color = _glyphColor ?? GetThemeColor(colorName);
        if (Disabled && _glyphColor is not null) color = UiTokens.WithAlpha(color, 0.45f);
        float size = _glyphSize;
        float stroke = size >= 26 ? 2.0f : 1.5f;
        float y = (Size.Y - size) * 0.5f;
        float x;
        if (GlyphTop) { x = (Size.X - size) * 0.5f; y = 6; }
        else if (Text.Length == 0) x = (Size.X - size) * 0.5f;
        else if (ThemeTypeVariation.ToString() == "ChipButtonIcon") x = UiTokens.Sp3;
        else if (ThemeTypeVariation.ToString().StartsWith("Ghost", StringComparison.Ordinal)) x = UiTokens.Sp3;
        else x = 21;
        UiDraw.Glyph(this, _glyph, new Rect2(x, y, size, size), color, stroke, _rotation);
    }
}

/// <summary>A switch with a label: the whole row is the hit target.</summary>
public partial class UiToggle : Button
{
    public UiToggle(string label, bool on, Action<bool>? changed = null)
    {
        Text = label;
        ToggleMode = true;
        ButtonPressed = on;
        FocusMode = FocusModeEnum.None;
        ThemeTypeVariation = "ToggleRow";
        Alignment = HorizontalAlignment.Left;
        CustomMinimumSize = new Vector2(0, UiTokens.ControlS);
        if (changed is not null) Toggled += v => changed(v);
    }

    public override void _Draw()
    {
        float h = Size.Y;
        var track = new Rect2(0, (h - 20) * 0.5f, 34, 20);
        Color trackColor = ButtonPressed ? UiTokens.Accent : new Color("#16130F");
        DrawRoundedRect(this, track, 10, trackColor, ButtonPressed ? null : UiTokens.BorderStrong);
        float knobX = ButtonPressed ? track.Position.X + 34 - 18 : track.Position.X + 2;
        DrawCircle(new Vector2(knobX + 8, track.Position.Y + 10), 8, ButtonPressed ? UiTokens.TextOnAccent : UiTokens.TextTertiary);
    }

    public static void DrawRoundedRect(CanvasItem ci, Rect2 rect, float radius, Color fill, Color? border)
    {
        var box = new StyleBoxFlat { BgColor = fill };
        box.SetCornerRadiusAll((int)radius);
        if (border is Color edge) { box.BorderColor = edge; box.SetBorderWidthAll(1); }
        ci.DrawStyleBox(box, rect);
    }
}

/// <summary>Two-colour progress bar drawn directly; the shown value can ease toward the target.</summary>
public partial class UiProgressBar : Control
{
    private float _target, _shown;
    private Color _fill = UiTokens.Accent;
    private Tween? _tween;

    public Color Fill { get => _fill; set { if (_fill != value) { _fill = value; QueueRedraw(); } } }

    public UiProgressBar(int height = 6)
    {
        CustomMinimumSize = new Vector2(0, height);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public float Value => _target;

    public void SetValue(float value, bool animate = true)
    {
        value = Mathf.Clamp(value, 0, 1);
        if (Mathf.IsEqualApprox(value, _target)) return;
        float from = _shown;
        _target = value;
        _tween?.Kill();
        if (!animate || UiSettings.Current.ReduceMotion || Mathf.Abs(value - from) > 0.2f || !IsInsideTree())
        {
            _shown = value;
            QueueRedraw();
            return;
        }
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(v => { _shown = v; QueueRedraw(); }), from, value, 0.3);
    }

    public override void _Draw()
    {
        float h = Size.Y;
        UiToggle.DrawRoundedRect(this, new Rect2(0, 0, Size.X, h), h * 0.5f, UiTokens.SurfaceSunken, null);
        float w = Mathf.Max(0, Size.X * _shown);
        if (w >= 1) UiToggle.DrawRoundedRect(this, new Rect2(0, 0, Mathf.Max(w, h), h), h * 0.5f, _fill, null);
    }
}

/// <summary>A dashed rounded rectangle outline (open slots, almost-ready projects).</summary>
public partial class UiDashedBorder : Control
{
    private Color _color = UiTokens.Accent;
    private float _width = 2.0f, _dash = 8.0f, _gap = 5.0f, _inset = 1.0f;
    private Tween? _pulse;

    public Color Color { get => _color; set { if (_color != value) { _color = value; QueueRedraw(); } } }

    public UiDashedBorder(Color color, float width = 2.0f, float dash = 8.0f, float gap = 5.0f)
    {
        _color = color;
        _width = width;
        _dash = dash;
        _gap = gap;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void SetPulse(bool on)
    {
        _pulse?.Kill();
        Modulate = Colors.White;
        if (!on || UiSettings.Current.ReduceMotion) return;
        if (!IsInsideTree()) return;
        _pulse = CreateTween().SetLoops();
        _pulse.TweenProperty(this, "modulate:a", 0.45f, 0.8).SetTrans(Tween.TransitionType.Sine);
        _pulse.TweenProperty(this, "modulate:a", 1.0f, 0.8).SetTrans(Tween.TransitionType.Sine);
    }

    public override void _Draw()
    {
        float i = _inset + _width * 0.5f;
        float x0 = i, y0 = i, x1 = Size.X - i, y1 = Size.Y - i;
        DrawDashedLine(new Vector2(x0 + 5, y0), new Vector2(x1 - 5, y0), _color, _width, _dash);
        DrawDashedLine(new Vector2(x0 + 5, y1), new Vector2(x1 - 5, y1), _color, _width, _dash);
        DrawDashedLine(new Vector2(x0, y0 + 5), new Vector2(x0, y1 - 5), _color, _width, _dash);
        DrawDashedLine(new Vector2(x1, y0 + 5), new Vector2(x1, y1 - 5), _color, _width, _dash);
        DrawArc(new Vector2(x0 + 5, y0 + 5), 5, Mathf.Pi, Mathf.Pi * 1.5f, 6, _color, _width);
        DrawArc(new Vector2(x1 - 5, y0 + 5), 5, Mathf.Pi * 1.5f, Mathf.Tau, 6, _color, _width);
        DrawArc(new Vector2(x1 - 5, y1 - 5), 5, 0, Mathf.Pi * 0.5f, 6, _color, _width);
        DrawArc(new Vector2(x0 + 5, y1 - 5), 5, Mathf.Pi * 0.5f, Mathf.Pi, 6, _color, _width);
    }
}

/// <summary>A solid or tinted border drawn over a card (selection ring, rank bar).</summary>
public partial class UiFrame : Control
{
    private StyleBoxFlat _style;

    public UiFrame(Color border, int width, int radius, Color? fill = null, int expand = 0)
    {
        _style = new StyleBoxFlat { BgColor = fill ?? new Color(0, 0, 0, 0), BorderColor = border, DrawCenter = fill is not null };
        _style.SetBorderWidthAll(width);
        _style.SetCornerRadiusAll(radius);
        if (expand > 0) _style.SetExpandMarginAll(expand);
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public void Set(Color border, Color? fill = null)
    {
        if (_style.BorderColor == border && (fill is null ? !_style.DrawCenter : _style.BgColor == fill.Value)) return;
        _style.BorderColor = border;
        _style.DrawCenter = fill is not null;
        if (fill is Color f) _style.BgColor = f;
        QueueRedraw();
    }

    public override void _Draw() => DrawStyleBox(_style, new Rect2(Vector2.Zero, Size));
}

/// <summary>Equal-width segments in a sunken well; the selected segment is accent-filled.</summary>
public partial class UiSegmented : PanelContainer
{
    private readonly Button[] _buttons;
    private int _selected;
    private readonly Action<int>? _changed;

    public UiSegmented(string[] labels, int selected, Action<int>? changed, int minWidth = 38)
    {
        ThemeTypeVariation = "SegmentedPanel";
        _changed = changed;
        _selected = selected;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        AddChild(row);
        _buttons = new Button[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Text = labels[i],
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(minWidth, 32),
                ThemeTypeVariation = i == selected ? "SegButtonOn" : "SegButton",
            };
            button.Pressed += () => Select(index, true);
            _buttons[i] = button;
            row.AddChild(button);
        }
    }

    public int Selected => _selected;

    public void SetTooltip(int index, string text) => _buttons[index].TooltipText = text;

    public void Select(int index, bool notify)
    {
        if (index == _selected && !notify) return;
        _selected = index;
        for (int i = 0; i < _buttons.Length; i++)
        {
            string variation = i == index ? "SegButtonOn" : "SegButton";
            if (_buttons[i].ThemeTypeVariation != variation) _buttons[i].ThemeTypeVariation = variation;
        }
        if (notify) _changed?.Invoke(index);
    }
}

/// <summary>A small rounded status label ("Paying off", "Blocked"); the tone picks the chip fill and text colour.</summary>
public partial class UiPill : PanelContainer
{
    public enum Tone { Neutral, Success, Warning, Danger, Info, Accent }

    private readonly Label _label;
    private Tone _tone = (Tone)(-1);

    public UiPill(string text = "", Tone tone = Tone.Neutral)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _label = UiKit.Text(text, "Caption");
        AddChild(_label);
        Set(text, tone);
    }

    public void Set(string text, Tone tone)
    {
        UiKit.SetText(_label, text);
        if (tone == _tone) return;
        _tone = tone;
        (string variation, Color color) = tone switch
        {
            Tone.Success => ("ChipPanelSuccess", UiTokens.Success),
            Tone.Warning => ("ChipPanelWarning", UiTokens.Warning),
            Tone.Danger => ("ChipPanelDanger", UiTokens.Danger),
            Tone.Info => ("ChipPanelInfo", UiTokens.Info),
            Tone.Accent => ("ChipPanelAccent", UiTokens.Accent),
            _ => ("ChipPanel", UiTokens.TextSecondary),
        };
        UiKit.SetVariation(this, variation);
        _label.AddThemeColorOverride("font_color", color);
    }
}

/// <summary>Pooled factory helpers so every screen builds controls the same way.</summary>
public static class UiKit
{
    public static PanelContainer Panel(string variation, string? name = null, MouseFilter filter = MouseFilter.Stop)
    {
        var panel = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = (Control.MouseFilterEnum)(int)filter };
        if (name is not null) panel.Name = name;
        return panel;
    }

    public enum MouseFilter { Stop = 0, Pass = 1, Ignore = 2 }

    public static Label Text(string text, string style = "Body", Color? color = null, int minWidth = 0, bool right = false,
        bool wrap = false, bool ellipsis = false)
    {
        var label = new Label
        {
            Text = text,
            ThemeTypeVariation = style,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (color is Color c) label.AddThemeColorOverride("font_color", c);
        if (minWidth > 0) label.CustomMinimumSize = new Vector2(minWidth, 0);
        if (right) label.HorizontalAlignment = HorizontalAlignment.Right;
        if (wrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        if (ellipsis)
        {
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.ClipText = true;
        }
        return label;
    }

    /// <summary>
    /// A table cell: exactly <paramref name="width"/> wide, never clipped. Long text wraps inside the column instead of ending
    /// in an ellipsis, so the same width can be used for a header and every row below it.
    /// </summary>
    public static Label Cell(string text, string style, int width, bool right = false, bool wrap = false)
    {
        Label label = Text(text, style, right: right);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    /// <summary>Sets a label's text only when it differs, so an idle HUD relays out nothing.</summary>
    public static bool SetText(Label label, string text)
    {
        if (label.Text == text) return false;
        label.Text = text;
        return true;
    }

    public static bool SetText(Button button, string text)
    {
        if (button.Text == text) return false;
        button.Text = text;
        return true;
    }

    public static void SetVariation(Control control, string variation)
    {
        if (control.ThemeTypeVariation != variation) control.ThemeTypeVariation = variation;
    }

    public static void SetVisible(CanvasItem item, bool visible)
    {
        if (item.Visible != visible) item.Visible = visible;
    }

    private static UiButton MakeButton(string text, string variation, Action? pressed, string? tooltip, UiGlyph icon, int height, bool focus)
    {
        string name = icon == UiGlyph.None || text.Length == 0 ? variation : variation + "Icon";
        var button = new UiButton
        {
            Text = text,
            Glyph = icon,
            ThemeTypeVariation = name,
            CustomMinimumSize = new Vector2(text.Length == 0 ? height : 0, height),
            FocusMode = focus ? Control.FocusModeEnum.All : Control.FocusModeEnum.None,
        };
        if (tooltip is not null) button.TooltipText = tooltip;
        if (pressed is not null) button.Pressed += pressed;
        return button;
    }

    public static UiButton Primary(string text, Action? pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None, int height = UiTokens.ControlM, bool focus = false)
        => MakeButton(text, "Primary", pressed, tooltip, icon, height, focus);

    public static UiButton Secondary(string text, Action? pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None, int height = UiTokens.ControlM, bool focus = false)
        => MakeButton(text, "Secondary", pressed, tooltip, icon, height, focus);

    public static UiButton Ghost(string text, Action? pressed, string? tooltip = null, UiGlyph icon = UiGlyph.None, int height = UiTokens.ControlS, bool focus = false)
        => MakeButton(text, "Ghost", pressed, tooltip, icon, height, focus);

    public static UiButton Danger(string text, Action? pressed, string? tooltip = null, int height = UiTokens.ControlM, bool focus = false)
        => MakeButton(text, "Danger", pressed, tooltip, UiGlyph.None, height, focus);

    public static UiButton IconButton(UiGlyph glyph, string tooltip, Action? pressed, int size = UiTokens.ControlM, int glyphSize = 26, bool toggle = false)
    {
        var button = new UiButton
        {
            Glyph = glyph,
            GlyphSize = glyphSize,
            ThemeTypeVariation = "IconButton",
            CustomMinimumSize = new Vector2(size, size),
            TooltipText = tooltip,
            ToggleMode = toggle,
        };
        if (pressed is not null) button.Pressed += pressed;
        return button;
    }

    public static HBoxContainer Row(int separation = UiTokens.Sp2)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    public static VBoxContainer Column(int separation = UiTokens.Sp2)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", separation);
        return column;
    }

    public static Control Spacer(float width = 0, float height = 0, bool expand = false)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (expand) spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return spacer;
    }

    /// <summary>
    /// Containers lay every child out inside their padding, so overlays (borders, accent bars) cannot live in a PanelContainer.
    /// This wraps the panel in a plain Control: the panel fills it, overlays added to the wrapper cover the whole card.
    /// </summary>
    public static Control WrapOverlay(PanelContainer panel, Vector2 minimum)
    {
        var wrapper = new Control { CustomMinimumSize = minimum, MouseFilter = Control.MouseFilterEnum.Stop };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.MouseFilter = Control.MouseFilterEnum.Pass;
        wrapper.AddChild(panel);
        return wrapper;
    }

    public static MarginContainer Margin(Control child, int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        margin.AddChild(child);
        return margin;
    }

    public static ScrollContainer Scroll(Control content)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FollowFocus = true,
        };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(content);
        return scroll;
    }

    public static Control Divider(bool vertical = false)
    {
        var line = new ColorRect { Color = UiTokens.Border, CustomMinimumSize = vertical ? new Vector2(1, 0) : new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        return line;
    }

    /// <summary>Small display chip: optional glyph plus short text.</summary>
    public static PanelContainer Chip(string text, UiGlyph glyph = UiGlyph.None, Color? tint = null, string variation = "ChipPanel")
    {
        var chip = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var row = Row(UiTokens.Sp1);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (glyph != UiGlyph.None) row.AddChild(new UiIcon(glyph, 16, tint ?? UiTokens.TextSecondary));
        var label = Text(text, "Eyebrow", tint);
        label.Name = "ChipText";
        row.AddChild(label);
        chip.AddChild(row);
        return chip;
    }

    public static void SetChipText(Control chip, string text)
    {
        if (chip.FindChild("ChipText", true, false) is Label label) SetText(label, text);
    }

    public static Button InteractiveChip(string text, UiGlyph glyph, Action pressed, string? tooltip = null)
    {
        var chip = new UiButton
        {
            Text = text,
            Glyph = glyph,
            ToggleMode = true,
            ThemeTypeVariation = glyph == UiGlyph.None ? "ChipButton" : "ChipButtonIcon",
            CustomMinimumSize = new Vector2(0, UiTokens.ControlS),
        };
        if (glyph != UiGlyph.None) chip.GlyphSize = 16;
        if (tooltip is not null) chip.TooltipText = tooltip;
        chip.Pressed += pressed;
        return chip;
    }

    public static void Fade(CanvasItem item, bool show, float seconds = UiTokens.BaseS)
    {
        if (!show && !item.Visible) return;
        if (show && item.Visible && item.Modulate.A >= 0.99f) return;
        if (UiSettings.Current.ReduceMotion || !item.IsInsideTree())
        {
            item.Modulate = Colors.White;
            item.Visible = show;
            return;
        }
        Tween tween = item.CreateTween();
        if (show)
        {
            Color start = item.Modulate;
            start.A = item.Visible ? start.A : 0;
            item.Modulate = start;
            item.Visible = true;
            tween.TweenProperty(item, "modulate:a", 1.0f, seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else
        {
            tween.TweenProperty(item, "modulate:a", 0.0f, seconds * 0.75f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() => { item.Visible = false; item.Modulate = Colors.White; }));
        }
    }
}
