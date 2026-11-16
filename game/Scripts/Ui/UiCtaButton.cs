using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The research inspector's main action ("Start in Primary", "Put in Secondary…", "Move to Tertiary"): the most important
/// button in the game. When it is a real call to action it wears a terracotta plate with a gold border and small scroll
/// ornaments at both ends; when the action is unavailable it dims to a dark plate. As a quieter Secondary look (queue,
/// known, blocked) it falls back to the theme style. Art is in assets/ui/cta (docs/MVP/34-era-ui-skins.md §5).
/// </summary>
public partial class UiCtaButton : UiButton
{
    private const string Folder = "res://assets/ui/cta/";
    private const int Cap = 84;          // texture px of each fixed end cap (the plate is drawn at half size)
    private const int SideMargin = 30;   // text inset, design px

    private static readonly string[] States = { "normal", "hover", "pressed", "disabled", "hover_pressed" };

    private readonly Control? _holder;
    private readonly NinePatchRect? _ready, _hot, _off;
    private readonly Panel? _shadow;
    private Tween? _fade;
    private bool _primary, _hovering;

    public UiCtaButton(Action pressed)
    {
        ThemeTypeVariation = "Primary";
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = new Vector2(0, UiTokens.ControlL);
        Pressed += pressed;

        Texture2D? ready = Load("cta_plate"), hot = Load("cta_plate_hot"), off = Load("cta_plate_off");
        if (ready is null || hot is null || off is null) return; // the plain theme button still works without the art

        _shadow = new Panel { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true, Visible = false };
        var shadowBox = new StyleBoxFlat { BgColor = Colors.Transparent, ShadowColor = new Color(0.05f, 0.02f, 0.01f, 0.5f), ShadowSize = 12, ShadowOffset = new Vector2(0, 4) };
        shadowBox.SetCornerRadiusAll(9);
        _shadow.AddThemeStyleboxOverride("panel", shadowBox);
        _shadow.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_shadow);

        // The plate is drawn twice as large and scaled to half, so its lines are crisp on high-density screens.
        _holder = new Control { MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true, Scale = new Vector2(0.5f, 0.5f), Visible = false };
        AddChild(_holder);
        _off = Plate(off);
        _ready = Plate(ready);
        _hot = Plate(hot);
        _hot.Modulate = new Color(1, 1, 1, 0);
        _holder.AddChild(_off);
        _holder.AddChild(_ready);
        _holder.AddChild(_hot);
        Resized += () => _holder.Size = Size * 2f;

        MouseEntered += () => SetHover(true);
        MouseExited += () => SetHover(false);
        ButtonDown += () => Dim(true);
        ButtonUp += () => Dim(false);
    }

    /// <summary>Call whenever the button's meaning changes. <paramref name="primary"/> = a real call to action; otherwise the quiet theme style.</summary>
    public void ApplyLook(bool primary, bool disabled)
    {
        if (_holder is null) return;
        if (primary != _primary)
        {
            _primary = primary;
            foreach (string state in States)
            {
                if (primary)
                {
                    var box = new StyleBoxEmpty { ContentMarginLeft = SideMargin, ContentMarginRight = SideMargin, ContentMarginTop = 6, ContentMarginBottom = 6 };
                    AddThemeStyleboxOverride(state, box);
                }
                else RemoveThemeStyleboxOverride(state);
            }
        }
        _holder.Visible = primary;
        _shadow!.Visible = primary && !disabled;
        _ready!.Visible = !disabled;
        _off!.Visible = disabled;
        if (primary && disabled) AddThemeColorOverride("font_disabled_color", new Color("#BBA283"));
        else RemoveThemeColorOverride("font_disabled_color");
        if (disabled) SetHover(false, true);
    }

    private static Texture2D? Load(string name)
    {
        string path = $"{Folder}{name}.png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private static NinePatchRect Plate(Texture2D texture) => new()
    {
        Texture = texture,
        PatchMarginLeft = Cap,
        PatchMarginRight = Cap,
        AxisStretchHorizontal = NinePatchRect.AxisStretchMode.TileFit,
        AxisStretchVertical = NinePatchRect.AxisStretchMode.Stretch,
        MouseFilter = MouseFilterEnum.Ignore,
        AnchorRight = 1,
        AnchorBottom = 1,
    };

    private void SetHover(bool hover, bool instant = false)
    {
        if (_hot is null) return;
        _hovering = hover && !Disabled;
        _fade?.Kill();
        float target = _hovering ? 1f : 0f;
        if (instant || UiSettings.Current.ReduceMotion) { _hot.Modulate = new Color(1, 1, 1, target); return; }
        _fade = CreateTween();
        _fade.TweenProperty(_hot, "modulate:a", target, UiTokens.FastS);
    }

    private void Dim(bool down)
    {
        Color tint = down ? new Color(0.84f, 0.8f, 0.78f) : Colors.White;
        if (_ready is not null) _ready.SelfModulate = tint;
        if (_hot is not null) _hot.SelfModulate = tint;
    }
}
