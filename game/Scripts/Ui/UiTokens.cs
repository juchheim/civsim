using ResearchTheme = CivSim.Sim.Research.Theme;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>Design tokens for the in-game UI (docs/MVP/20-ui-design-spec.md §1). All sizes are design px in the 1600×900 canvas.</summary>
public static class UiTokens
{
    // Spacing: Fibonacci, with 4 as the practical inner gap.
    public const int Sp1 = 4, Sp2 = 8, Sp3 = 13, Sp4 = 21, Sp5 = 34, Sp6 = 55, Sp7 = 89;
    // Radii and control heights. Heights are deliberately not φ: 36 is the click-target floor.
    public const int R1 = 5, R2 = 8, R3 = 13;
    public const int ControlS = 36, ControlM = 42, ControlL = 55;

    // Type scale (docs/MVP/ui-typography.md): every size is 16 × φ^(n/4), rounded. Whole-φ steps 16 → 26 → 42;
    // quarter steps between them. 14 is the reading floor; 13 is only for badges and moment captions.
    //   n:   -2   -1    0    1    2    4    5    8
    //   px:  13   14   16   18   20   26   29   42
    public const int FontMicro = 13, FontSmall = 14, FontBody = 16, FontLead = 18, FontHeading = 20, FontTitle = 26, FontYear = 29, FontDisplay = 42;

    // Leading (line pitch ÷ size): φ for reading text, √φ for display and all-caps lines.
    public const double LeadingText = 1.618, LeadingDisplay = 1.272;

    // Commissioner axes. Display voice is full flare; the 18 px bridge has half; text has none.
    public const int FlareDisplay = 100, FlareLead = 50, VolumeDisplay = 40;
    public const int WeightRegular = 430, WeightMedium = 540, WeightCaps = 600, WeightBold = 650, WeightDisplaySemi = 660, WeightDisplay = 760;

    // Brand (from the Living Ages logo): terracotta mark, oxblood word, parchment ground.
    public static readonly Color BrandTerracotta = new("#B8501F");
    public static readonly Color BrandEmber = new("#D9703A");
    public static readonly Color BrandDeep = new("#9C3B14");
    public static readonly Color BrandOxblood = new("#4A1505");
    public static readonly Color Parchment = new("#F5EAD7");

    // Surfaces: oxblood-browns, warm and dark so the terracotta reads as the one bright accent.
    public static readonly Color Surface1 = new("#21130EE6");
    public static readonly Color Surface2 = new("#2D1B13");
    public static readonly Color Surface3 = new("#37231A");
    public static readonly Color Surface2Hover = new("#3B261C");
    public static readonly Color Surface2Pressed = new("#462E22");
    public static readonly Color SurfaceDisabled = new("#25160F");
    public static readonly Color SurfaceSunken = new("#140A0699");
    public static readonly Color SurfaceSunkenSolid = new("#140A06");
    public static readonly Color SurfaceMap = new("#21130ED9");
    public static readonly Color SurfaceMapLabel = new("#21130EE0");
    public static readonly Color SurfaceMapLabelStrong = new("#2D1B13F2");
    public static readonly Color ScrimModal = new("#0D06048C");
    public static readonly Color ScrimWeb = new("#0D060473");
    public static readonly Color ScrimPause = new("#3A1A0C24");
    public static readonly Color Border = new("#F5EAD71A");
    public static readonly Color BorderStrong = new("#F5EAD738");
    public static readonly Color Hover = new("#F5EAD70F");
    public static readonly Color Pressed = new("#F5EAD71A");

    // Text: parchment on oxblood.
    public static readonly Color TextPrimary = new("#F5EAD7");
    public static readonly Color TextSecondary = new("#D6C3AA");
    public static readonly Color TextTertiary = new("#B39C84");
    public static readonly Color TextDisabled = new("#806B5B");
    public static readonly Color TextOnAccent = new("#2A0D04");

    // Accent: the logo's terracotta, lifted one step so it reads as text on the dark surfaces (5.0:1) and carries oxblood text (6.3:1).
    public static readonly Color Accent = new("#E07A45");
    public static readonly Color AccentHover = new("#EA8D5A");
    public static readonly Color AccentPressed = new("#CA6835");
    public static readonly Color AccentDisabled = new("#4E3426");

    // Semantic: kept apart from terracotta in hue. Warning moves to ochre, danger to a rose crimson.
    public static readonly Color Success = new("#6FC79B");
    public static readonly Color Warning = new("#E8B54A");
    public static readonly Color Danger = new("#F0697E");
    public static readonly Color Info = new("#9FC3E0");
    public static readonly Color SuccessDark = new("#1E4332");
    public static readonly Color WarningDark = new("#4A3613");
    public static readonly Color DangerDark = new("#4F1A24");
    public static readonly Color DangerHover = new("#66222F");
    public static readonly Color DangerPressed = new("#3D141C");
    public static readonly Color InfoDark = new("#22394D");

    // Research themes (UI only, never on the map). Index = (int)Theme.
    private static readonly Color[] ThemeBase = { new("#DDA15E"), new("#8DBA74"), new("#74A9D8"), new("#E2D3A6") };
    private static readonly Color[] ThemeLight = { new("#EDC08E"), new("#B5D9A0"), new("#A7CBEA"), new("#F3ECDD") };
    private static readonly Color[] ThemeDark = { new("#4A3420"), new("#2A4127"), new("#22394D"), new("#3D3626") };

    public static Color ThemeColor(ResearchTheme theme) => ThemeBase[(int)theme];
    public static Color ThemeText(ResearchTheme theme) => ThemeLight[(int)theme];
    public static Color ThemeFill(ResearchTheme theme) => ThemeDark[(int)theme];

    // Civilization colours: the Okabe–Ito table the map uses.
    public static readonly Color[] CivColors =
    {
        new("#E69F00"), new("#56B4E9"), new("#009E73"), new("#F0E442"),
        new("#0072B2"), new("#D55E00"), new("#CC79A7"), new("#4D4D4D"),
    };

    public static Color Civ(int civ) => civ >= 0 && civ < CivColors.Length ? CivColors[civ] : new Color("#8A8478");

    public static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    // Motion (seconds)
    public const float FastS = 0.12f, BaseS = 0.18f, BaseCloseS = 0.135f, SlowS = 0.26f, SlowCloseS = 0.195f;
    public const float ToastInS = 0.22f, ToastOutS = 0.16f;
}
