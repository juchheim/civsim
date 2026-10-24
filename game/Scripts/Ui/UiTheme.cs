using Godot;

namespace CivSim.Game.Ui;

/// <summary>Fonts and the single Theme the whole HUD uses. Built once; state changes swap theme type variations, never per-control overrides.</summary>
public static class UiTheme
{
    public static readonly string[] FontNames =
        { "Avenir Next", "Segoe UI", "SF Pro Text", "Helvetica Neue", "Inter", "Noto Sans", "Arial" };

    private static Theme? _theme;
    private static Font? _regular, _medium, _bold;
    private static FontVariation? _caps, _num, _numBold;

    public static Font Regular => _regular ??= MakeFont(400);
    public static Font Medium => _medium ??= MakeFont(500);
    public static Font Bold => _bold ??= MakeFont(600);

    /// <summary>Medium weight with a little tracking, for ALL-CAPS eyebrows.</summary>
    public static FontVariation Caps => _caps ??= new FontVariation { BaseFont = Medium, SpacingGlyph = 1 };

    /// <summary>Tabular numerals; changing numbers should not make a row jitter.</summary>
    public static FontVariation Num => _num ??= MakeNum(Regular);
    public static FontVariation NumBold => _numBold ??= MakeNum(Bold);

    private static Font MakeFont(int weight)
        => new SystemFont
        {
            FontNames = FontNames,
            FontWeight = weight,
            Antialiasing = TextServer.FontAntialiasing.Gray,
            Hinting = TextServer.Hinting.Light,
            SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
            GenerateMipmaps = false,
            AllowSystemFallback = true,
        };

    private static FontVariation MakeNum(Font baseFont)
    {
        var variation = new FontVariation { BaseFont = baseFont };
        var features = new Godot.Collections.Dictionary { ["tnum"] = 1 };
        variation.OpentypeFeatures = features;
        return variation;
    }

    public static Theme Build() => _theme ??= CreateTheme();

    public static StyleBoxFlat Box(Color fill, int radius = UiTokens.R2, Color? border = null, int borderWidth = 1,
        int padH = UiTokens.Sp3, int padV = UiTokens.Sp2, int shadow = 0, int shadowY = 0, float shadowAlpha = 0.3f)
    {
        var box = new StyleBoxFlat { BgColor = fill };
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = padH;
        box.ContentMarginRight = padH;
        box.ContentMarginTop = padV;
        box.ContentMarginBottom = padV;
        if (border is Color edge)
        {
            box.BorderColor = edge;
            box.SetBorderWidthAll(borderWidth);
        }
        if (shadow > 0)
        {
            box.ShadowSize = shadow;
            box.ShadowOffset = new Vector2(0, shadowY);
            box.ShadowColor = new Color(0, 0, 0, shadowAlpha);
        }
        return box;
    }

    private static StyleBoxEmpty Empty(int padH = 0, int padV = 0)
    {
        var box = new StyleBoxEmpty();
        box.ContentMarginLeft = padH;
        box.ContentMarginRight = padH;
        box.ContentMarginTop = padV;
        box.ContentMarginBottom = padV;
        return box;
    }

    private static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFont = Regular, DefaultFontSize = UiTokens.FontBody };

        theme.SetColor("font_color", "Label", UiTokens.TextPrimary);
        theme.SetFont("font", "Label", Regular);
        theme.SetFontSize("font_size", "Label", UiTokens.FontBody);
        theme.SetConstant("line_spacing", "Label", 3);
        theme.SetColor("font_outline_color", "Label", new Color(0, 0, 0, 0));
        theme.SetConstant("outline_size", "Label", 0);

        void LabelStyle(string name, Font font, int size, Color color, int lineSpacing)
        {
            theme.SetTypeVariation(name, "Label");
            theme.SetFont("font", name, font);
            theme.SetFontSize("font_size", name, size);
            theme.SetColor("font_color", name, color);
            theme.SetConstant("line_spacing", name, lineSpacing);
        }
        LabelStyle("Display", Bold, UiTokens.FontDisplay, UiTokens.TextPrimary, 4);
        LabelStyle("Title", Bold, UiTokens.FontTitle, UiTokens.TextPrimary, 3);
        LabelStyle("Heading", Medium, UiTokens.FontHeading, UiTokens.TextPrimary, 3);
        LabelStyle("Body", Regular, UiTokens.FontBody, UiTokens.TextPrimary, 4);
        LabelStyle("BodySecondary", Regular, UiTokens.FontBody, UiTokens.TextSecondary, 4);
        LabelStyle("BodyStrong", Medium, UiTokens.FontBody, UiTokens.TextPrimary, 4);
        LabelStyle("BodyStrongSecondary", Medium, UiTokens.FontBody, UiTokens.TextSecondary, 4);
        LabelStyle("BodyStrongAccent", Medium, UiTokens.FontBody, UiTokens.Accent, 4);
        LabelStyle("CaptionSuccess", Regular, UiTokens.FontSmall, UiTokens.Success, 3);
        LabelStyle("CaptionWarning", Regular, UiTokens.FontSmall, UiTokens.Warning, 3);
        LabelStyle("Eyebrow", Caps, UiTokens.FontSmall, UiTokens.TextSecondary, 2);
        LabelStyle("EyebrowTertiary", Caps, UiTokens.FontSmall, UiTokens.TextTertiary, 2);
        LabelStyle("EyebrowAccent", Caps, UiTokens.FontSmall, UiTokens.Accent, 2);
        LabelStyle("Caption", Regular, UiTokens.FontSmall, UiTokens.TextSecondary, 3);
        LabelStyle("CaptionTertiary", Regular, UiTokens.FontSmall, UiTokens.TextTertiary, 3);
        LabelStyle("CaptionAccent", Regular, UiTokens.FontSmall, UiTokens.Accent, 3);
        LabelStyle("Num", Num, UiTokens.FontSmall, UiTokens.TextSecondary, 3);
        LabelStyle("NumStrong", NumBold, UiTokens.FontBody, UiTokens.TextPrimary, 3);
        LabelStyle("NumTitle", NumBold, UiTokens.FontTitle, UiTokens.TextPrimary, 3);

        // Panels
        theme.SetStylebox("panel", "PanelContainer", Box(UiTokens.Surface1, UiTokens.R2, UiTokens.Border));
        void PanelStyle(string name, StyleBox box)
        {
            theme.SetTypeVariation(name, "PanelContainer");
            theme.SetStylebox("panel", name, box);
        }
        PanelStyle("HudPanel", Box(UiTokens.Surface1, UiTokens.R2, UiTokens.Border, 1, UiTokens.Sp3, UiTokens.Sp2, 6, 2, 0.30f));
        PanelStyle("CardPanel", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 10, 10, 4, 0.35f));
        PanelStyle("InnerCard", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 10));
        PanelStyle("SheetPanel", Box(UiTokens.Surface3, UiTokens.R3, UiTokens.BorderStrong, 1, UiTokens.Sp4, UiTokens.Sp4, 21, 8, 0.45f));
        PanelStyle("ToastPanel", Box(UiTokens.Surface2, UiTokens.R2, UiTokens.BorderStrong, 1, UiTokens.Sp3, UiTokens.Sp2, 10, 4, 0.35f));
        PanelStyle("PillPanel", Box(UiTokens.Surface1, 17, UiTokens.Border, 1, UiTokens.Sp3, 6));
        PanelStyle("PausedPill", Box(UiTokens.Surface2, 17, UiTokens.Accent, 1, UiTokens.Sp3, 6, 6, 2, 0.3f));
        PanelStyle("SlotCard", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 10));
        PanelStyle("SlotCardOpen", Box(UiTokens.WithAlpha(UiTokens.Surface1, 0.6f), UiTokens.R1, null, 0, UiTokens.Sp3, 10));
        PanelStyle("SlotCardPending", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.Accent, 1, UiTokens.Sp3, 10));
        PanelStyle("SlotCardDone", Box(UiTokens.SuccessDark, UiTokens.R1, UiTokens.Success, 2, UiTokens.Sp3, 10));
        PanelStyle("WellPanel", Box(UiTokens.SurfaceSunken, UiTokens.R1, null, 0, UiTokens.Sp2, UiTokens.Sp2));
        PanelStyle("SegmentedPanel", Box(UiTokens.SurfaceSunken, UiTokens.R1, null, 0, 2, 2));
        PanelStyle("ChipPanel", Box(new Color("#F3ECDD14"), 13, UiTokens.Border, 1, 10, 2));
        PanelStyle("ChipPanelAccent", Box(UiTokens.WithAlpha(UiTokens.Accent, 0.16f), 13, UiTokens.Accent, 1, 10, 2));
        PanelStyle("ChipPanelWarning", Box(UiTokens.WarningDark, 13, UiTokens.Warning, 1, 10, 2));
        PanelStyle("ChipPanelDanger", Box(UiTokens.DangerDark, 13, UiTokens.Danger, 1, 10, 2));
        PanelStyle("ChipPanelSuccess", Box(UiTokens.SuccessDark, 13, UiTokens.Success, 1, 10, 2));
        PanelStyle("ChipPanelInfo", Box(new Color("#22394D"), 13, UiTokens.Info, 1, 10, 2));
        PanelStyle("BadgePanel", Box(UiTokens.Accent, 9, null, 0, 6, 0));
        PanelStyle("BadgePanelDanger", Box(UiTokens.Danger, 9, null, 0, 6, 0));

        theme.SetStylebox("panel", "TooltipPanel", Box(UiTokens.Surface3, UiTokens.R2, UiTokens.BorderStrong, 1, UiTokens.Sp3, UiTokens.Sp2, 12, 4, 0.4f));
        theme.SetColor("font_color", "TooltipLabel", UiTokens.TextPrimary);
        theme.SetFontSize("font_size", "TooltipLabel", UiTokens.FontSmall + 1);
        theme.SetFont("font", "TooltipLabel", Regular);

        // Buttons
        theme.SetStylebox("normal", "Button", Box(UiTokens.Surface2, UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("hover", "Button", Box(new Color("#38322A"), UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("pressed", "Button", Box(new Color("#423B31"), UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("disabled", "Button", Box(new Color("#25201B"), UiTokens.R2, UiTokens.Border, 1, 21, 6));
        theme.SetStylebox("focus", "Button", FocusRing(UiTokens.R2 + 2));
        theme.SetFont("font", "Button", Medium);
        theme.SetFontSize("font_size", "Button", UiTokens.FontBody);
        theme.SetColor("font_color", "Button", UiTokens.TextPrimary);
        theme.SetColor("font_hover_color", "Button", UiTokens.TextPrimary);
        theme.SetColor("font_pressed_color", "Button", UiTokens.TextPrimary);
        theme.SetColor("font_focus_color", "Button", UiTokens.TextPrimary);
        theme.SetColor("font_hover_pressed_color", "Button", UiTokens.TextPrimary);
        theme.SetColor("font_disabled_color", "Button", UiTokens.TextDisabled);
        theme.SetConstant("h_separation", "Button", 4);

        void ButtonStyle(string name, StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled,
            Color text, Color textHover, Color textDisabled, StyleBox? hoverPressed = null, Color? textPressed = null)
        {
            theme.SetTypeVariation(name, "Button");
            theme.SetStylebox("normal", name, normal);
            theme.SetStylebox("hover", name, hover);
            theme.SetStylebox("pressed", name, pressed);
            theme.SetStylebox("hover_pressed", name, hoverPressed ?? hover);
            theme.SetStylebox("disabled", name, disabled);
            theme.SetStylebox("focus", name, FocusRing(UiTokens.R2 + 2));
            theme.SetColor("font_color", name, text);
            theme.SetColor("font_hover_color", name, textHover);
            theme.SetColor("font_pressed_color", name, textPressed ?? text);
            theme.SetColor("font_hover_pressed_color", name, textPressed ?? textHover);
            theme.SetColor("font_focus_color", name, text);
            theme.SetColor("font_disabled_color", name, textDisabled);
        }

        for (int icon = 0; icon < 2; icon++)
        {
            int padL = icon == 0 ? 21 : 21 + 16 + 4;
            string suffix = icon == 0 ? "" : "Icon";
            StyleBoxFlat PrimaryBox(Color fill)
            {
                StyleBoxFlat b = Box(fill, UiTokens.R2, null, 0, 21, 6);
                b.ContentMarginLeft = padL;
                return b;
            }
            StyleBoxFlat SecondaryBox(Color fill, Color border)
            {
                StyleBoxFlat b = Box(fill, UiTokens.R2, border, 1, 21, 6);
                b.ContentMarginLeft = padL;
                return b;
            }
            StyleBoxFlat GhostBox(Color fill)
            {
                StyleBoxFlat b = Box(fill, UiTokens.R2, null, 0, UiTokens.Sp3, 4);
                b.ContentMarginLeft = icon == 0 ? UiTokens.Sp3 : UiTokens.Sp3 + 16 + 4;
                return b;
            }
            ButtonStyle("Primary" + suffix, PrimaryBox(UiTokens.Accent), PrimaryBox(UiTokens.AccentHover), PrimaryBox(UiTokens.AccentPressed),
                PrimaryBox(new Color("#4A4234")), UiTokens.TextOnAccent, UiTokens.TextOnAccent, UiTokens.TextDisabled);
            ButtonStyle("Secondary" + suffix, SecondaryBox(UiTokens.Surface2, UiTokens.BorderStrong), SecondaryBox(new Color("#38322A"), UiTokens.BorderStrong),
                SecondaryBox(new Color("#423B31"), UiTokens.BorderStrong), SecondaryBox(new Color("#25201B"), UiTokens.Border),
                UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);
            ButtonStyle("Ghost" + suffix, GhostBox(new Color(0, 0, 0, 0)), GhostBox(UiTokens.Hover), GhostBox(UiTokens.Pressed),
                GhostBox(new Color(0, 0, 0, 0)), UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled);
            ButtonStyle("Danger" + suffix, SecondaryBox(UiTokens.DangerDark, UiTokens.Danger), SecondaryBox(new Color("#612B28"), UiTokens.Danger),
                SecondaryBox(new Color("#3B1B19"), UiTokens.Danger), SecondaryBox(new Color("#25201B"), UiTokens.Border),
                UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);
        }

        // Icon buttons: square, ghost fill, centred glyph drawn by UiButton.
        ButtonStyle("IconButton", Box(new Color(0, 0, 0, 0), UiTokens.R2, null, 0, 0, 0), Box(UiTokens.Hover, UiTokens.R2, null, 0, 0, 0),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.22f), UiTokens.R2, UiTokens.Accent, 1, 0, 0), Box(new Color(0, 0, 0, 0), UiTokens.R2, null, 0, 0, 0),
            UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.22f), UiTokens.R2, UiTokens.Accent, 1, 0, 0), UiTokens.TextPrimary);
        ButtonStyle("IconButtonOn", Box(UiTokens.WithAlpha(UiTokens.Accent, 0.22f), UiTokens.R2, UiTokens.Accent, 1, 0, 0),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.30f), UiTokens.R2, UiTokens.Accent, 1, 0, 0),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.30f), UiTokens.R2, UiTokens.Accent, 1, 0, 0),
            Box(new Color(0, 0, 0, 0), UiTokens.R2, null, 0, 0, 0), UiTokens.Accent, UiTokens.Accent, UiTokens.TextDisabled);
        ButtonStyle("RailButton", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 4, 2), Box(UiTokens.Hover, UiTokens.R1, null, 0, 4, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.20f), UiTokens.R1, UiTokens.Accent, 1, 4, 2),
            Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 4, 2), UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.28f), UiTokens.R1, UiTokens.Accent, 1, 4, 2), UiTokens.Accent);

        ButtonStyle("RailButtonTall", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 2, 2), Box(UiTokens.Hover, UiTokens.R1, null, 0, 2, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.20f), UiTokens.R1, UiTokens.Accent, 1, 2, 2),
            Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 2, 2), UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.28f), UiTokens.R1, UiTokens.Accent, 1, 2, 2), UiTokens.Accent);

        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            ((StyleBoxFlat)theme.GetStylebox(state, "RailButtonTall")).ContentMarginTop = 34;

        // Segmented control segments
        ButtonStyle("SegButton", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 2), Box(UiTokens.Hover, UiTokens.R1, null, 0, UiTokens.Sp3, 2),
            Box(UiTokens.Pressed, UiTokens.R1, null, 0, UiTokens.Sp3, 2), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 2),
            UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled);
        ButtonStyle("SegButtonOn", Box(UiTokens.Accent, UiTokens.R1, null, 0, UiTokens.Sp3, 2), Box(UiTokens.AccentHover, UiTokens.R1, null, 0, UiTokens.Sp3, 2),
            Box(UiTokens.AccentPressed, UiTokens.R1, null, 0, UiTokens.Sp3, 2), Box(UiTokens.Accent, UiTokens.R1, null, 0, UiTokens.Sp3, 2),
            UiTokens.TextOnAccent, UiTokens.TextOnAccent, UiTokens.TextOnAccent);

        // Switch row: transparent; the switch itself is drawn by UiToggle
        ButtonStyle("ToggleRow", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2), Box(UiTokens.Hover, UiTokens.R1, null, 0, 42, 2),
            Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);
        ButtonStyle("ToggleRowCompact", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2), Box(UiTokens.Hover, UiTokens.R1, null, 0, 42, 2),
            Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 42, 2),
            UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled);

        // Interactive chips (theme filters etc.)
        ButtonStyle("ChipButton", Box(new Color("#F3ECDD14"), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2), Box(new Color("#F3ECDD24"), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.18f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), Box(new Color("#F3ECDD0A"), 18, UiTokens.Border, 1, UiTokens.Sp3, 2),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.26f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), UiTokens.Accent);

        ButtonStyle("ChipButtonIcon", Box(new Color("#F3ECDD14"), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2), Box(new Color("#F3ECDD24"), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.18f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), Box(new Color("#F3ECDD0A"), 18, UiTokens.Border, 1, UiTokens.Sp3, 2),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.26f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), UiTokens.Accent);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            ((StyleBoxFlat)theme.GetStylebox(state, "ChipButtonIcon")).ContentMarginLeft = UiTokens.Sp4 + 16 + UiTokens.Sp1;

        // Rows (lists): hover fill only
        ButtonStyle("RowButton", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 4), Box(UiTokens.Hover, UiTokens.R1, null, 0, UiTokens.Sp3, 4),
            Box(UiTokens.Pressed, UiTokens.R1, null, 0, UiTokens.Sp3, 4), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 4),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.12f), UiTokens.R1, null, 0, UiTokens.Sp3, 4));

        // Project cards: bordered, transparent until hover
        ButtonStyle("CardButton", Box(new Color("#00000000"), UiTokens.R1, null, 0, 0, 0), Box(UiTokens.Hover, UiTokens.R1, null, 0, 0, 0),
            Box(UiTokens.Pressed, UiTokens.R1, null, 0, 0, 0), Box(new Color("#00000000"), UiTokens.R1, null, 0, 0, 0),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);

        // Text fields
        theme.SetStylebox("normal", "LineEdit", Box(UiTokens.SurfaceSunken, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 6));
        theme.SetStylebox("focus", "LineEdit", Box(UiTokens.SurfaceSunken, UiTokens.R1, UiTokens.Accent, 2, UiTokens.Sp3, 6));
        theme.SetStylebox("read_only", "LineEdit", Box(UiTokens.SurfaceSunken, UiTokens.R1, UiTokens.Border, 1, UiTokens.Sp3, 6));
        theme.SetColor("font_color", "LineEdit", UiTokens.TextPrimary);
        theme.SetColor("font_placeholder_color", "LineEdit", UiTokens.TextTertiary);
        theme.SetColor("caret_color", "LineEdit", UiTokens.Accent);
        theme.SetColor("selection_color", "LineEdit", UiTokens.WithAlpha(UiTokens.Accent, 0.35f));
        theme.SetFont("font", "LineEdit", Regular);
        theme.SetFontSize("font_size", "LineEdit", UiTokens.FontBody);

        // Scroll bars: thin, themed
        StyleBoxFlat Grabber(Color c)
        {
            var b = new StyleBoxFlat { BgColor = c };
            b.SetCornerRadiusAll(3);
            b.ContentMarginLeft = 3;
            b.ContentMarginRight = 3;
            b.ContentMarginTop = 3;
            b.ContentMarginBottom = 3;
            return b;
        }
        foreach (string bar in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", bar, Empty(3, 3));
            theme.SetStylebox("scroll_focus", bar, Empty(3, 3));
            theme.SetStylebox("grabber", bar, Grabber(UiTokens.BorderStrong));
            theme.SetStylebox("grabber_highlight", bar, Grabber(UiTokens.TextTertiary));
            theme.SetStylebox("grabber_pressed", bar, Grabber(UiTokens.TextSecondary));
        }

        // Sliders
        theme.SetStylebox("slider", "HSlider", Box(UiTokens.SurfaceSunken, 3, null, 0, 0, 3));
        theme.SetStylebox("grabber_area", "HSlider", Box(UiTokens.Accent, 3, null, 0, 0, 3));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Box(UiTokens.AccentHover, 3, null, 0, 0, 3));
        theme.SetConstant("center_grabber", "HSlider", 1);
        return theme;
    }

    public static StyleBoxFlat FocusRing(int radius)
    {
        var ring = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = UiTokens.Accent, DrawCenter = false };
        ring.SetBorderWidthAll(2);
        ring.SetCornerRadiusAll(radius);
        ring.SetExpandMarginAll(2);
        return ring;
    }
}
