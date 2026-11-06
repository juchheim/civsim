using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// Fonts and the single Theme the whole HUD uses. Built once; state changes swap theme type variations, never per-control overrides.
/// One family, Commissioner (variable: wght, slnt, FLAR, VOLM), in two voices: the flared display voice for anything 20 px and up,
/// and the plain text voice (no flare) for everything at reading size. docs/MVP/ui-typography.md.
/// </summary>
public static class UiTheme
{
    public const string FontPath = "res://assets/fonts/commissioner/Commissioner-Variable.ttf";

    /// <summary>System fonts behind Commissioner, only for glyphs it lacks (✓ ✗ → ★ and non-Latin scripts).</summary>
    public static readonly string[] FallbackNames =
        { "Avenir Next", "Segoe UI", "SF Pro Text", "Helvetica Neue", "Noto Sans", "Arial" };

    private static Theme? _theme;
    private static FontFile? _family;
    private static FontVariation? _regular, _medium, _bold, _caps, _num, _numBold;
    private static FontVariation? _display, _displaySemi, _displayLead, _displayNum;

    // Text voice: no flare, slightly heavier than nominal because light text on dark surfaces needs it at 14 to 16 px.
    public static FontVariation Regular => _regular ??= Voice(UiTokens.WeightRegular, 0);
    public static FontVariation Medium => _medium ??= Voice(UiTokens.WeightMedium, 0);
    public static FontVariation Bold => _bold ??= Voice(UiTokens.WeightBold, 0);

    /// <summary>Semibold with a little tracking, for ALL-CAPS eyebrows.</summary>
    public static FontVariation Caps => _caps ??= Voice(UiTokens.WeightCaps, 0, spacing: 1);

    /// <summary>Numbers. Commissioner has no tabular figures (no tnum), so every changing number also sits in a fixed-width, aligned slot.</summary>
    public static FontVariation Num => _num ??= Voice(UiTokens.WeightRegular, 0, numbers: true);
    public static FontVariation NumBold => _numBold ??= Voice(UiTokens.WeightBold, 0, numbers: true);

    // Display voice: full flare (FLAR 100) for titles, headings and big numbers (20 px and up).
    public static FontVariation Display => _display ??= Voice(UiTokens.WeightDisplay, UiTokens.FlareDisplay, UiTokens.VolumeDisplay);
    public static FontVariation DisplaySemi => _displaySemi ??= Voice(UiTokens.WeightDisplaySemi, UiTokens.FlareDisplay, UiTokens.VolumeDisplay);
    public static FontVariation DisplayNum => _displayNum ??= Voice(UiTokens.WeightDisplay, UiTokens.FlareDisplay, UiTokens.VolumeDisplay, numbers: true);

    /// <summary>The 18 px bridge step between the two voices: half flare.</summary>
    public static FontVariation DisplayLead => _displayLead ??= Voice(UiTokens.WeightDisplaySemi, UiTokens.FlareLead, 0);

    /// <summary>The variable font file. Falls back to loading the TTF directly when the project has not been imported yet.</summary>
    public static FontFile Family
    {
        get
        {
            if (_family is not null) return _family;
            FontFile? file = ResourceLoader.Exists(FontPath) ? ResourceLoader.Load<FontFile>(FontPath) : null;
            if (file is null)
            {
                file = new FontFile();
                if (file.LoadDynamicFont(ProjectSettings.GlobalizePath(FontPath)) != Error.Ok)
                    GD.PushWarning($"UiTheme: could not load {FontPath}; text falls back to system fonts.");
            }
            file.Antialiasing = TextServer.FontAntialiasing.Gray;
            file.Hinting = TextServer.Hinting.Light;
            file.SubpixelPositioning = TextServer.SubpixelPositioning.Auto;
            file.GenerateMipmaps = false;
            file.AllowSystemFallback = true;
            file.Fallbacks = new Godot.Collections.Array<Font>
            {
                new SystemFont { FontNames = FallbackNames, FontWeight = 500, Antialiasing = TextServer.FontAntialiasing.Gray, AllowSystemFallback = true },
            };
            return _family = file;
        }
    }

    private static FontVariation Voice(int weight, int flare, int volume = 0, int spacing = 0, bool numbers = false)
    {
        TextServer ts = TextServerManager.GetPrimaryInterface();
        var axes = new Godot.Collections.Dictionary
        {
            [ts.NameToTag("wght")] = weight,
            [ts.NameToTag("FLAR")] = flare,
            [ts.NameToTag("VOLM")] = volume,
            [ts.NameToTag("slnt")] = 0,
        };
        var variation = new FontVariation { BaseFont = Family, VariationOpentype = axes, SpacingGlyph = spacing };
        // Lining case forms keep ( ) – × aligned with figures; tnum is asked for in case a future cut gains it (ignored today).
        if (numbers) variation.OpentypeFeatures = new Godot.Collections.Dictionary { [ts.NameToTag("tnum")] = 1 };
        return variation;
    }

    /// <summary>
    /// Extra pixels between wrapped lines so the line pitch is <paramref name="size"/> × <paramref name="leading"/>
    /// (φ for reading text, √φ for headings). Godot adds line_spacing only between lines, so single lines keep their height.
    /// </summary>
    public static int Leading(Font font, int size, double leading)
        => System.Math.Max(0, (int)System.Math.Round(size * leading) - (int)System.Math.Round(font.GetHeight(size)));

    public static Theme Build()
    {
        if (_theme is not null) return _theme;
        // Controls outside the HUD theme (Label3D map labels, boot messages) fall back to the text voice too.
        ThemeDB.FallbackFont = Regular;
        ThemeDB.FallbackFontSize = UiTokens.FontBody;
        return _theme = CreateTheme();
    }

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
        theme.SetConstant("line_spacing", "Label", Leading(Regular, UiTokens.FontBody, UiTokens.LeadingText));
        theme.SetColor("font_outline_color", "Label", new Color(0, 0, 0, 0));
        theme.SetConstant("outline_size", "Label", 0);

        // Every style takes its leading from the scale: φ for reading text, √φ for display and caps.
        void LabelStyle(string name, Font font, int size, Color color, double leading)
        {
            theme.SetTypeVariation(name, "Label");
            theme.SetFont("font", name, font);
            theme.SetFontSize("font_size", name, size);
            theme.SetColor("font_color", name, color);
            theme.SetConstant("line_spacing", name, Leading(font, size, leading));
        }
        const double text = UiTokens.LeadingText, tight = UiTokens.LeadingDisplay;
        // Display voice (flared)
        LabelStyle("Display", Display, UiTokens.FontDisplay, UiTokens.TextPrimary, tight);
        LabelStyle("Title", Display, UiTokens.FontTitle, UiTokens.TextPrimary, tight);
        LabelStyle("Heading", DisplaySemi, UiTokens.FontHeading, UiTokens.TextPrimary, tight);
        LabelStyle("NumTitle", DisplayNum, UiTokens.FontTitle, UiTokens.TextPrimary, tight);
        LabelStyle("NumYear", DisplayNum, UiTokens.FontYear, UiTokens.TextPrimary, tight);
        // Bridge step (half flare): section titles inside sheets, moment headings.
        LabelStyle("SectionTitle", DisplayLead, UiTokens.FontLead, UiTokens.TextPrimary, tight);
        LabelStyle("MomentHeading", DisplayLead, UiTokens.FontLead, UiTokens.TextPrimary, tight);
        // Text voice (no flare)
        LabelStyle("Body", Regular, UiTokens.FontBody, UiTokens.TextPrimary, text);
        LabelStyle("BodySecondary", Regular, UiTokens.FontBody, UiTokens.TextSecondary, text);
        LabelStyle("BodyStrong", Medium, UiTokens.FontBody, UiTokens.TextPrimary, text);
        LabelStyle("BodyStrongSecondary", Medium, UiTokens.FontBody, UiTokens.TextSecondary, text);
        LabelStyle("BodyStrongAccent", Medium, UiTokens.FontBody, UiTokens.Accent, text);
        LabelStyle("CaptionSuccess", Regular, UiTokens.FontSmall, UiTokens.Success, text);
        LabelStyle("CaptionWarning", Regular, UiTokens.FontSmall, UiTokens.Warning, text);
        LabelStyle("Eyebrow", Caps, UiTokens.FontSmall, UiTokens.TextSecondary, tight);
        LabelStyle("EyebrowTertiary", Caps, UiTokens.FontSmall, UiTokens.TextTertiary, tight);
        LabelStyle("EyebrowAccent", Caps, UiTokens.FontSmall, UiTokens.Accent, tight);
        LabelStyle("MomentBody", Regular, UiTokens.FontSmall, UiTokens.TextSecondary, text);
        LabelStyle("MomentCaption", Regular, UiTokens.FontMicro, UiTokens.TextTertiary, text);
        LabelStyle("MomentEyebrow", Caps, UiTokens.FontMicro, UiTokens.Accent, tight);
        LabelStyle("Caption", Regular, UiTokens.FontSmall, UiTokens.TextSecondary, text);
        LabelStyle("CaptionTertiary", Regular, UiTokens.FontSmall, UiTokens.TextTertiary, text);
        LabelStyle("CaptionAccent", Regular, UiTokens.FontSmall, UiTokens.Accent, text);
        LabelStyle("Num", Num, UiTokens.FontSmall, UiTokens.TextSecondary, text);
        LabelStyle("NumBody", Num, UiTokens.FontBody, UiTokens.TextPrimary, text);
        LabelStyle("NumStrong", NumBold, UiTokens.FontBody, UiTokens.TextPrimary, text);
        LabelStyle("NumBadge", NumBold, UiTokens.FontMicro, UiTokens.TextOnAccent, tight);

        // Panels
        theme.SetStylebox("panel", "PanelContainer", Box(UiTokens.Surface1, UiTokens.R2, UiTokens.Border));
        void PanelStyle(string name, StyleBox box)
        {
            theme.SetTypeVariation(name, "PanelContainer");
            theme.SetStylebox("panel", name, box);
        }
        PanelStyle("HudPanel", Box(UiTokens.Surface1, UiTokens.R2, UiTokens.Border, 1, UiTokens.Sp3, UiTokens.Sp2, 6, 2, 0.30f));
        PanelStyle("TimeClusterPanel", Box(UiTokens.Surface1, UiTokens.R2, UiTokens.Border, 1, UiTokens.Sp3, 4, 6, 2, 0.30f));
        PanelStyle("CardPanel", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 10, 10, 4, 0.35f));
        PanelStyle("MomentCard", Box(UiTokens.Surface2, UiTokens.R1, UiTokens.BorderStrong, 1, UiTokens.Sp3, 8, 10, 4, 0.35f));
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
        // Sheet sections and table rows: a roomier sunken well, and rows that share one padding so headers line up with cells.
        PanelStyle("SectionPanel", Box(UiTokens.SurfaceSunken, UiTokens.R2, null, 0, UiTokens.Sp3 + 3, UiTokens.Sp3 + 1));
        PanelStyle("TableRow", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp2, 6));
        PanelStyle("TableRowAlt", Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.043f), UiTokens.R1, null, 0, UiTokens.Sp2, 6));
        PanelStyle("TableHead", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp2, 2));
        PanelStyle("ChipPanel", Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.08f), 13, UiTokens.Border, 1, 10, 2));
        PanelStyle("ChipPanelAccent", Box(UiTokens.WithAlpha(UiTokens.Accent, 0.16f), 13, UiTokens.Accent, 1, 10, 2));
        PanelStyle("ChipPanelWarning", Box(UiTokens.WarningDark, 13, UiTokens.Warning, 1, 10, 2));
        PanelStyle("ChipPanelDanger", Box(UiTokens.DangerDark, 13, UiTokens.Danger, 1, 10, 2));
        PanelStyle("ChipPanelSuccess", Box(UiTokens.SuccessDark, 13, UiTokens.Success, 1, 10, 2));
        PanelStyle("ChipPanelInfo", Box(UiTokens.InfoDark, 13, UiTokens.Info, 1, 10, 2));
        PanelStyle("BadgePanel", Box(UiTokens.Accent, 9, null, 0, 6, 0));
        PanelStyle("BadgePanelDanger", Box(UiTokens.Danger, 9, null, 0, 6, 0));
        // UI pass 2: a parchment badge for the accent-filled Research web button (an accent badge would vanish on it), and the
        // hover state of the civ card, which is now a button that opens People & Stores.
        PanelStyle("BadgePanelInk", Box(UiTokens.Parchment, 9, UiTokens.AccentPressed, 1, 6, 0));
        PanelStyle("HudPanelHover", Box(UiTokens.Surface2Hover, UiTokens.R2, UiTokens.BorderStrong, 1, UiTokens.Sp3, UiTokens.Sp2, 6, 2, 0.30f));

        theme.SetStylebox("panel", "TooltipPanel", Box(UiTokens.Surface3, UiTokens.R2, UiTokens.BorderStrong, 1, UiTokens.Sp3, UiTokens.Sp2, 12, 4, 0.4f));
        theme.SetColor("font_color", "TooltipLabel", UiTokens.TextPrimary);
        theme.SetFontSize("font_size", "TooltipLabel", UiTokens.FontSmall);
        theme.SetFont("font", "TooltipLabel", Regular);
        theme.SetConstant("line_spacing", "TooltipLabel", Leading(Regular, UiTokens.FontSmall, UiTokens.LeadingText));

        // Buttons
        theme.SetStylebox("normal", "Button", Box(UiTokens.Surface2, UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("hover", "Button", Box(UiTokens.Surface2Hover, UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("pressed", "Button", Box(UiTokens.Surface2Pressed, UiTokens.R2, UiTokens.BorderStrong, 1, 21, 6));
        theme.SetStylebox("disabled", "Button", Box(UiTokens.SurfaceDisabled, UiTokens.R2, UiTokens.Border, 1, 21, 6));
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
                PrimaryBox(UiTokens.AccentDisabled), UiTokens.TextOnAccent, UiTokens.TextOnAccent, UiTokens.TextDisabled);
            ButtonStyle("Secondary" + suffix, SecondaryBox(UiTokens.Surface2, UiTokens.BorderStrong), SecondaryBox(UiTokens.Surface2Hover, UiTokens.BorderStrong),
                SecondaryBox(UiTokens.Surface2Pressed, UiTokens.BorderStrong), SecondaryBox(UiTokens.SurfaceDisabled, UiTokens.Border),
                UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);
            ButtonStyle("Ghost" + suffix, GhostBox(new Color(0, 0, 0, 0)), GhostBox(UiTokens.Hover), GhostBox(UiTokens.Pressed),
                GhostBox(new Color(0, 0, 0, 0)), UiTokens.TextSecondary, UiTokens.TextPrimary, UiTokens.TextDisabled);
            ButtonStyle("Danger" + suffix, SecondaryBox(UiTokens.DangerDark, UiTokens.Danger), SecondaryBox(UiTokens.DangerHover, UiTokens.Danger),
                SecondaryBox(UiTokens.DangerPressed, UiTokens.Danger), SecondaryBox(UiTokens.SurfaceDisabled, UiTokens.Border),
                UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled);
        }

        theme.SetTypeVariation("MomentPrimary", "Primary");
        theme.SetFontSize("font_size", "MomentPrimary", UiTokens.FontSmall);
        theme.SetTypeVariation("MomentSecondary", "Secondary");
        theme.SetFontSize("font_size", "MomentSecondary", UiTokens.FontSmall);

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
        ButtonStyle("ChipButton", Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.08f), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2), Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.14f), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.18f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.04f), 18, UiTokens.Border, 1, UiTokens.Sp3, 2),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.26f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), UiTokens.Accent);

        ButtonStyle("ChipButtonIcon", Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.08f), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2), Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.14f), 18, UiTokens.BorderStrong, 1, UiTokens.Sp3, 2),
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.18f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), Box(UiTokens.WithAlpha(UiTokens.Parchment, 0.04f), 18, UiTokens.Border, 1, UiTokens.Sp3, 2),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.26f), 18, UiTokens.Accent, 1, UiTokens.Sp3, 2), UiTokens.Accent);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            StyleBoxFlat style = (StyleBoxFlat)theme.GetStylebox(state, "ChipButtonIcon");
            style.ContentMarginLeft = UiTokens.Sp3 + 16 + UiTokens.Sp1;
            style.ContentMarginRight = UiTokens.Sp3 + UiTokens.Sp1;
        }

        // Rows (lists): hover fill only
        ButtonStyle("RowButton", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 4), Box(UiTokens.Hover, UiTokens.R1, null, 0, UiTokens.Sp3, 4),
            Box(UiTokens.Pressed, UiTokens.R1, null, 0, UiTokens.Sp3, 4), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, UiTokens.Sp3, 4),
            UiTokens.TextPrimary, UiTokens.TextPrimary, UiTokens.TextDisabled,
            Box(UiTokens.WithAlpha(UiTokens.Accent, 0.12f), UiTokens.R1, null, 0, UiTokens.Sp3, 4));

        // Project cards: bordered, transparent until hover
        ButtonStyle("CardButton", Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 0, 0), Box(UiTokens.Hover, UiTokens.R1, null, 0, 0, 0),
            Box(UiTokens.Pressed, UiTokens.R1, null, 0, 0, 0), Box(new Color(0, 0, 0, 0), UiTokens.R1, null, 0, 0, 0),
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
