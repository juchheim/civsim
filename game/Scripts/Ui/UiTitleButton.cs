using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The title screen's Start Game button: a textured oxblood plate with a gold hairline, scroll flourishes on both
/// sides and a soft shadow. Art is in assets/ui/title (docs/MVP/34-era-ui-skins.md §4). If the art is missing it falls
/// back to the plain Secondary button.
/// </summary>
public partial class UiTitleButton : Button
{
    public const int PlateWidth = 360, PlateHeight = 72;
    private const int WingWidth = 110, WingOverlap = 14;
    private const string Folder = "res://assets/ui/title/";

    private static readonly Color Ink = UiTokens.Parchment;
    private static readonly Color InkHot = new("#FFE7C4");
    private static readonly Color InkShadow = new("#1C0905CC");

    private TextureRect? _hotPlate, _leftWing, _rightWing;
    private Tween? _hover, _breath;
    private bool _hot, _pressed;
    private float _wingOut;

    public UiTitleButton()
    {
        Theme = UiTheme.Build();
        CustomMinimumSize = new Vector2(PlateWidth, PlateHeight);
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AddThemeFontOverride("font", UiTheme.Display);
        AddThemeFontSizeOverride("font_size", UiTokens.FontYear);

        Texture2D? plate = Load("start_plate"), hot = Load("start_plate_hot"), wing = Load("start_wing");
        if (plate is null || hot is null || wing is null)
        {
            ThemeTypeVariation = "Secondary";
            return;
        }

        var empty = new StyleBoxEmpty();
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "hover_pressed" }) AddThemeStyleboxOverride(state, empty);
        var focus = new StyleBoxFlat { BgColor = Colors.Transparent, DrawCenter = false, BorderColor = new Color("#E8B468B3") };
        focus.SetBorderWidthAll(2);
        focus.SetCornerRadiusAll(9);
        focus.SetExpandMarginAll(4);
        AddThemeStyleboxOverride("focus", focus);
        foreach (string name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color", "font_disabled_color" })
            AddThemeColorOverride(name, name is "font_hover_color" or "font_hover_pressed_color" ? InkHot : Ink);
        AddThemeColorOverride("font_shadow_color", InkShadow);
        AddThemeConstantOverride("shadow_offset_y", 2);

        // Drawn behind the button's own text, in this order: shadow, plate, hot plate.
        var shadow = new Panel { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        var shadowBox = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowColor = new Color(0.05f, 0.02f, 0.01f, 0.55f), ShadowSize = 26 };
        shadowBox.ShadowOffset = new Vector2(0, 8);
        shadowBox.SetCornerRadiusAll(12);
        shadow.AddThemeStyleboxOverride("panel", shadowBox);
        shadow.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(shadow);

        TextureRect basePlate = Layer(plate, true);
        AddChild(basePlate);
        _hotPlate = Layer(hot, true);
        _hotPlate.Modulate = new Color(1, 1, 1, 0);
        AddChild(_hotPlate);

        _leftWing = Wing(wing, false);
        _rightWing = Wing(wing, true);
        AddChild(_leftWing);
        AddChild(_rightWing);
        SetWings(0);

        MouseEntered += () => SetHot(true);
        MouseExited += () => SetHot(false);
        FocusEntered += () => { if (!_hot) SetWingGlow(true); };
        FocusExited += () => SetWingGlow(false);
        ButtonDown += () => SetPressLook(true);
        ButtonUp += () => SetPressLook(false);
        TreeEntered += StartBreathing;
        TreeExiting += () => { _breath?.Kill(); _hover?.Kill(); };
    }

    /// <summary>Forces a look for development screenshots (--title-state=hover|pressed).</summary>
    public void PreviewState(string state)
    {
        SetHot(state is "hover" or "pressed");
        SetPressLook(state == "pressed");
    }

    private static Texture2D? Load(string name)
    {
        string path = $"{Folder}{name}.png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private static TextureRect Layer(Texture2D texture, bool behind) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = MouseFilterEnum.Ignore,
        ShowBehindParent = behind,
        AnchorRight = 1,
        AnchorBottom = 1,
    };

    private static TextureRect Wing(Texture2D texture, bool right) => new()
    {
        Texture = texture,
        FlipH = right,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = MouseFilterEnum.Ignore,
        Size = new Vector2(WingWidth, PlateHeight),
        CustomMinimumSize = new Vector2(WingWidth, PlateHeight),
    };

    /// <summary>Slides both wings <paramref name="out"/> px away from the plate.</summary>
    private void SetWings(float @out)
    {
        _wingOut = @out;
        if (_leftWing is null || _rightWing is null) return;
        _leftWing.Position = new Vector2(-WingWidth + WingOverlap - @out, 0);
        _rightWing.Position = new Vector2(PlateWidth - WingOverlap + @out, 0);
    }

    private void SetHot(bool hot)
    {
        _hot = hot;
        if (_hotPlate is null) return;
        _hover?.Kill();
        float target = hot ? 1f : 0f;
        Color wings = hot ? new Color(1.25f, 1.2f, 1.1f) : Colors.White;
        if (UiSettings.Current.ReduceMotion)
        {
            _hotPlate.Modulate = new Color(1, 1, 1, target);
            if (_leftWing is not null) _leftWing.Modulate = wings;
            if (_rightWing is not null) _rightWing.Modulate = wings;
            SetWings(hot ? 7f : 0f);
            return;
        }
        _hover = CreateTween().SetParallel(true);
        _hover.TweenProperty(_hotPlate, "modulate:a", target, UiTokens.FastS);
        if (_leftWing is not null) _hover.TweenProperty(_leftWing, "modulate", wings, UiTokens.BaseS);
        if (_rightWing is not null) _hover.TweenProperty(_rightWing, "modulate", wings, UiTokens.BaseS);
        _hover.TweenMethod(Callable.From<float>(SetWings), _wingOut, hot ? 7f : 0f, UiTokens.BaseS)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void SetWingGlow(bool on)
    {
        Color wings = on ? new Color(1.12f, 1.08f, 1.04f) : Colors.White;
        if (_leftWing is not null && !_hot) _leftWing.Modulate = wings;
        if (_rightWing is not null && !_hot) _rightWing.Modulate = wings;
    }

    private void SetPressLook(bool pressed)
    {
        _pressed = pressed;
        Color dim = pressed ? new Color(0.82f, 0.78f, 0.76f) : Colors.White;
        foreach (Node child in GetChildren())
            if (child is TextureRect plate && plate.ShowBehindParent) plate.SelfModulate = dim;
        AddThemeConstantOverride("shadow_offset_y", pressed ? 1 : 2);
    }

    /// <summary>A slow drift of the wings, one pixel-ish in and out; off with Reduce motion.</summary>
    private void StartBreathing()
    {
        _breath?.Kill();
        if (_leftWing is null || UiSettings.Current.ReduceMotion) return;
        _breath = CreateTween().SetLoops();
        _breath.TweenMethod(Callable.From<float>(v => { if (!_hot && !_pressed) SetWings(v); }), 0f, 4f, 1.6)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _breath.TweenMethod(Callable.From<float>(v => { if (!_hot && !_pressed) SetWings(v); }), 4f, 0f, 1.6)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }
}
