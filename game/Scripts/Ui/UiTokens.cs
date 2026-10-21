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

    // Type scale: 16 × φ = 26, 26 × φ = 42; 20 is the half step; 14 is the floor.
    public const int FontDisplay = 42, FontTitle = 26, FontHeading = 20, FontBody = 16, FontSmall = 14;

    // Surfaces
    public static readonly Color Surface1 = new("#1F1B16E6");
    public static readonly Color Surface2 = new("#2A251E");
    public static readonly Color Surface3 = new("#322C25");
    public static readonly Color SurfaceSunken = new("#16130F99");
    public static readonly Color ScrimModal = new("#0E0C0A8C");
    public static readonly Color ScrimWeb = new("#0E0C0A73");
    public static readonly Color ScrimPause = new("#2A221624");
    public static readonly Color Border = new("#F3ECDD1A");
    public static readonly Color BorderStrong = new("#F3ECDD38");
    public static readonly Color Hover = new("#F3ECDD0F");
    public static readonly Color Pressed = new("#F3ECDD1A");

    // Text
    public static readonly Color TextPrimary = new("#F3ECDD");
    public static readonly Color TextSecondary = new("#C9BFAC");
    public static readonly Color TextTertiary = new("#A79E8B");
    public static readonly Color TextDisabled = new("#7A7263");
    public static readonly Color TextOnAccent = new("#1E1608");

    // Accent and semantic
    public static readonly Color Accent = new("#E9BE4E");
    public static readonly Color AccentHover = new("#F2CD6A");
    public static readonly Color AccentPressed = new("#CFA13A");
    public static readonly Color Success = new("#5FC49B");
    public static readonly Color Warning = new("#F57F3A");
    public static readonly Color Danger = new("#F2786A");
    public static readonly Color Info = new("#9FC3E0");
    public static readonly Color SuccessDark = new("#1F4536");
    public static readonly Color WarningDark = new("#4F2C17");
    public static readonly Color DangerDark = new("#4E2220");

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
