using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The age skin. Panels opt in through <see cref="Attach"/>; the overlay is drawn on the panel itself, above its stylebox and
/// below its children, so layout and hit-testing are untouched. The skin follows the viewer's age, or the age picked in Settings,
/// and cross-fades when it changes (docs/MVP/34-era-ui-skins.md).
/// </summary>
public static class UiSkin
{
    /// <summary>What a panel variation gets: texture strength, hairline strength, corner/crest sizes (0 = none).</summary>
    private readonly record struct Look(float Tex, float Outline, int Corner, int Crest);

    private static Look? LookFor(string variation) => variation switch
    {
        // Texture strengths are kept low on purpose: the light speckle lifts the surface under the text, so at
        // full strength it pushes tertiary text below 4.5:1. Re-check contrast before raising any of these.
        "SheetPanel" => new Look(0.4f, 0.45f, 48, 192),
        "CardPanel" or "MomentCard" or "ToastPanel" => new Look(0.45f, 0.35f, 36, 0),
        "HudPanel" or "TimeClusterPanel" or "PillPanel" => new Look(0.4f, 0.30f, 0, 0),
        "WebStagePanel" => new Look(0.6f, 0f, 0, 0),
        _ => null,
    };

    private const float FadeSeconds = 0.9f;
    private const int CornerInset = 4;

    private static int _viewerEra = -1;
    private static int _shown = -1, _previous = -1;
    private static float _blend = 1f;
    private static Tween? _tween;
    private static StyleBoxFlat? _edge;

    private static event Action? Changed;

    /// <summary>The age whose skin is on screen (its target while a fade runs).</summary>
    public static int ShownEra => _shown;

    /// <summary>Registers a panel for the skin. Safe to call once per panel, at construction.</summary>
    public static void Attach(PanelContainer panel)
    {
        Action redraw = panel.QueueRedraw;
        panel.Draw += () => Paint(panel);
        panel.TreeEntered += () => Changed += redraw;
        panel.TreeExited += () => Changed -= redraw;
        if (panel.IsInsideTree()) Changed += redraw;
    }

    /// <summary>Called every HUD refresh with the viewer's current age; only a change does any work.</summary>
    public static void SetViewerEra(int era)
    {
        if (era == _viewerEra) return;
        _viewerEra = era;
        Refresh();
    }

    /// <summary>Re-reads the Settings override. Call after the setting changes.</summary>
    public static void Refresh()
    {
        int wanted = UiSettings.Current.SkinOverride is >= 0 and < 10 ? UiSettings.Current.SkinOverride : _viewerEra;
        if (wanted < 0 || wanted == _shown) return;
        _tween?.Kill();
        _tween = null;
        bool animate = _shown >= 0 && !UiSettings.Current.ReduceMotion && Engine.GetMainLoop() is SceneTree;
        _previous = _shown;
        _shown = wanted;
        if (!animate)
        {
            _blend = 1f;
            Changed?.Invoke();
            return;
        }
        _blend = 0f;
        _tween = ((SceneTree)Engine.GetMainLoop()).CreateTween();
        _tween.TweenMethod(Callable.From<float>(v => { _blend = v; Changed?.Invoke(); }), 0f, 1f, FadeSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private static void Paint(PanelContainer panel)
    {
        if (_shown < 0 || LookFor(panel.ThemeTypeVariation) is not Look look) return;
        if (_blend < 1f && _previous >= 0) PaintLayer(panel, look, _previous, 1f - _blend);
        PaintLayer(panel, look, _shown, _blend < 1f && _previous >= 0 ? _blend : 1f);
    }

    private static void PaintLayer(PanelContainer panel, Look look, int era, float alpha)
    {
        if (alpha <= 0.002f) return;
        UiSkinSet skin = UiSkins.For(era);
        Vector2 size = panel.Size;
        if (size.X < 24 || size.Y < 24) return;

        int radius = panel.GetThemeStylebox("panel") is StyleBoxFlat flat ? flat.CornerRadiusTopLeft : 0;
        float inset = 1 + radius * 0.3f;

        if (skin.Tex is Texture2D tex && look.Tex > 0)
            panel.DrawTextureRect(tex, new Rect2(inset, inset, size.X - 2 * inset, size.Y - 2 * inset), true, new Color(1, 1, 1, Mathf.Min(1f, look.Tex * skin.Strength * alpha)));

        if (look.Outline > 0)
        {
            _edge ??= new StyleBoxFlat { DrawCenter = false };
            _edge.SetBorderWidthAll(1);
            _edge.SetCornerRadiusAll(radius);
            _edge.BorderColor = new Color(skin.Tint.R, skin.Tint.G, skin.Tint.B, look.Outline * alpha);
            panel.DrawStyleBox(_edge, new Rect2(Vector2.Zero, size));
        }

        Color ink = new(1, 1, 1, 0.55f * alpha);
        if (look.Corner > 0 && skin.Corner is Texture2D corner && size.X >= look.Corner * 3 && size.Y >= look.Corner * 2)
        {
            float c = look.Corner, w = size.X, h = size.Y, i = CornerInset;
            // Mirror through a draw transform: a negative rect size does not anchor where it looks like it should.
            void Place(Vector2 origin, Vector2 flip)
            {
                panel.DrawSetTransform(origin, 0f, flip);
                panel.DrawTextureRect(corner, new Rect2(0, 0, c, c), false, ink);
            }
            Place(new Vector2(i, i), new Vector2(1, 1));
            Place(new Vector2(w - i, i), new Vector2(-1, 1));
            Place(new Vector2(i, h - i), new Vector2(1, -1));
            Place(new Vector2(w - i, h - i), new Vector2(-1, -1));
            panel.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        if (look.Crest > 0 && skin.Crest is Texture2D crest && size.X >= look.Crest + 48)
        {
            float cw = look.Crest, ch = cw / 16f;
            panel.DrawTextureRect(crest, new Rect2((size.X - cw) * 0.5f, 3, cw, ch), false, new Color(1, 1, 1, 0.8f * alpha));
        }
    }
}
