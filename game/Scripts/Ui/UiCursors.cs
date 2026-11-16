using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The game-styled mouse pointers (docs/MVP/36-cursors.md). Each Godot cursor shape the game actually uses gets an image from
/// res://assets/ui/cursor/, drawn by tools/skin-gen/gen_cursors.py. They are hardware cursors (Input.SetCustomMouseCursor), so they
/// move with no lag. Shapes without an image (resize arrows, busy) keep the OS pointer. Off: the "Game-styled mouse pointer"
/// setting, or <see cref="Enabled"/> = false for every player.
/// </summary>
public static class UiCursors
{
    /// <summary>Master switch. False keeps the OS pointer everywhere, whatever the setting says.</summary>
    public const bool Enabled = true;

    private const string Dir = "res://assets/ui/cursor/";

    /// <summary>File stem, Godot shape, hotspot in 1x pixels (the generator's HOTSPOTS; keep the two in sync).</summary>
    private static readonly (string File, Input.CursorShape Shape, Vector2 Hotspot)[] Set =
    {
        ("arrow", Input.CursorShape.Arrow, new Vector2(4, 3)),
        ("hand", Input.CursorShape.PointingHand, new Vector2(16, 3)),
        ("grab", Input.CursorShape.Drag, new Vector2(19, 20)),
        ("drop", Input.CursorShape.CanDrop, new Vector2(19, 20)),
        ("ibeam", Input.CursorShape.Ibeam, new Vector2(20, 20)),
        ("forbidden", Input.CursorShape.Forbidden, new Vector2(20, 20)),
        ("target", Input.CursorShape.Cross, new Vector2(20, 20)),
    };

    /// <summary>Installs the themed pointers, or restores the OS ones. Safe to call again (settings toggle, start-up).</summary>
    public static void Apply()
    {
        if (!_hooked) { _hooked = true; UiSkin.ShownEraChanged += Apply; } // the gold follows the viewer's age
        bool on = Enabled && UiSettings.Current.ThemedCursor && DisplayServer.GetName() != "headless";
        string suffix = PickSuffix();
        Color? tint = UiSkin.ShownEra >= 0 ? UiSkins.For(UiSkin.ShownEra).Tint : null;
        foreach ((string file, Input.CursorShape shape, Vector2 hotspot) in Set)
        {
            if (!on) { Input.SetCustomMouseCursor(null, shape); continue; }
            string path = Dir + file + suffix + ".png";
            if (!ResourceLoader.Exists(path)) path = Dir + file + ".png"; // a hand-made set may ship only 1x
            if (!ResourceLoader.Exists(path)) { Input.SetCustomMouseCursor(null, shape); continue; }
            // Hand Input a CPU-side Image, not the GPU texture: a texture held by Input outlives the renderer and leaks at quit.
            Image? image;
            using (var texture = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Ignore)) image = texture?.GetImage();
            if (image is null) { Input.SetCustomMouseCursor(null, shape); continue; }
            if (tint is Color t) Retint(image, Dir + file + "_accent" + (path.EndsWith(suffix + ".png") ? suffix : "") + ".png", t);
            float s = image.GetWidth() / 40f; // the asset's own scale, whatever file was found
            Input.SetCustomMouseCursor(image, shape, (hotspot * s).Floor());
        }
    }

    /// <summary>
    /// Recolours the gold accents (diamonds, cuffs, badge, hairline) to the age's tint. The accent file has the gold's own colour in RGB and how much
    /// of each pixel is gold in alpha, so out = base + mask * (tint * luma(old) / luma(gold) - old). That keeps the light-to-dark shading of the
    /// gold and the anti-aliased edges. Same maths as retint() in gen_cursors.py. No mask file (hand-made art, forbidden): the image is left alone.
    /// </summary>
    private static void Retint(Image image, string maskPath, Color tint)
    {
        if (!ResourceLoader.Exists(maskPath)) return;
        Image? mask;
        using (var texture = ResourceLoader.Load<Texture2D>(maskPath, null, ResourceLoader.CacheMode.Ignore)) mask = texture?.GetImage();
        if (mask is null || mask.GetSize() != image.GetSize()) return;
        image.Convert(Image.Format.Rgba8);
        mask.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color m = mask.GetPixel(x, y);
            if (m.A <= 0f) continue;
            Color b = image.GetPixel(x, y);
            float ratio = Luma(m.R, m.G, m.B) / GoldLuma;
            image.SetPixel(x, y, new Color(
                Mathf.Clamp(b.R + m.A * (Mathf.Min(1f, tint.R * ratio) - m.R), 0f, 1f),
                Mathf.Clamp(b.G + m.A * (Mathf.Min(1f, tint.G * ratio) - m.G), 0f, 1f),
                Mathf.Clamp(b.B + m.A * (Mathf.Min(1f, tint.B * ratio) - m.B), 0f, 1f),
                b.A));
        }
    }

    private static bool _hooked;
    private static float Luma(float r, float g, float b) => 0.299f * r + 0.587f * g + 0.114f * b;
    private static readonly float GoldLuma = Luma(0xD9 / 255f, 0xA3 / 255f, 0x5A / 255f); // the generator's GOLD

    /// <summary>
    /// macOS sizes a custom cursor in points (the 40 px image is 40 pt; its arrow is about 31 pt tall against the OS arrow's 23), so it always gets the 1x set; on Retina
    /// it is upscaled and a little soft, which is why the art uses bold shapes and no hairline-only detail. Windows and Linux size it in
    /// physical pixels, so a high-DPI screen gets the 1.5x or 2x set.
    /// </summary>
    private static string PickSuffix()
    {
        if (OS.GetName() == "macOS") return "";
        int screen = DisplayServer.WindowGetCurrentScreen();
        float scale = Mathf.Max(DisplayServer.ScreenGetScale(screen), DisplayServer.ScreenGetDpi(screen) / 96f);
        return scale < 1.25f ? "" : scale < 1.75f ? "@1.5x" : "@2x";
    }
}
