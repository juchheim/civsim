using System;
using System.Collections.Generic;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>How much of a settlement plate is drawn (docs/MVP/33 §6.3).</summary>
public enum PlateLevel : byte
{
    /// <summary>A tiny half-disc on the settlement, no name unless it matters.</summary>
    Pip = 0,
    /// <summary>A small emblem dome standing on a short name ribbon.</summary>
    Compact = 1,
    /// <summary>The whole dome plate: crest, rims, emblem, name ribbon, head count.</summary>
    Full = 2,
    /// <summary>"Names only" setting: just the ribbon with a small owner emblem and, when it matters, an alert dot. No dome.</summary>
    Names = 3,
}

/// <summary>
/// The motion state of one plate (docs/MVP/33 §4.5). Each timer runs 0 to 1 and rests at 1; <see cref="Rest"/> is a plate that is not moving.
/// The overlay advances the timers and the drawing only reads them, so with Reduce motion on every timer simply stays at rest.
/// </summary>
public struct PlateMotion
{
    /// <summary>The plate appears or gains detail: cross-fade, the dome rising from the ribbon.</summary>
    public float Grow;
    /// <summary>The alert chip slides in.</summary>
    public float Chip;
    /// <summary>One bright pulse travels up the rim from both feet to the apex.</summary>
    public float Sweep;
    /// <summary>Ownership or relation changed: the rim recolours from the feet up and (when <see cref="FlipsEmblem"/>) the emblem turns over.</summary>
    public float Flip;
    /// <summary>A new defence outline rises out of the rim.</summary>
    public float Rise;
    /// <summary>A finished build lights the whole ring once; 1 right after, fading to 0.</summary>
    public float Flash;
    /// <summary>The ring's displayed progress in permille while it is catching up with the real value, or -1 when it simply shows the real value.</summary>
    public float MeterShown;
    public bool FlipsEmblem;
    public Color OldRim;
    public PlateFacts OldFacts;

    public static PlateMotion Rest => new() { Grow = 1, Chip = 1, Sweep = 1, Flip = 1, Rise = 1, Flash = 0, MeterShown = -1 };
    public readonly bool Active => Grow < 1 || Chip < 1 || Sweep < 1 || Flip < 1 || Rise < 1 || Flash > 0 || MeterShown >= 0;
}

/// <summary>Sizes of one plate at one level, in screen pixels, relative to its anchor (the pointer tip; y grows downward, so plates rise into negative y).</summary>
public readonly struct PlateMetrics
{
    public PlateLevel Level { get; init; }
    public float DomeW { get; init; }
    public float DomeH { get; init; }
    public float RibbonW { get; init; }
    public float RibbonH { get; init; }
    public float PointerH { get; init; }
    public int NameSize { get; init; }
    public int CountSize { get; init; }
    public float NameW { get; init; }
    public float CountW { get; init; }
    public bool HasCount { get; init; }
    public bool HexEnds { get; init; }
    public bool Crest { get; init; }
    public float DomeBaseY { get; init; }
    public float DomeTopY { get; init; }
    public float RibbonTopY { get; init; }
    /// <summary>The dome and its crest.</summary>
    public Rect2 DomeBox { get; init; }
    /// <summary>The ribbon and pointer (for a named pip: the name).</summary>
    public Rect2 RibbonBox { get; init; }
    /// <summary>The alert chip's words (Full level only), or null. The chip hangs under the ribbon; the pointer moves under it.</summary>
    public string? Chip { get; init; }
    public float ChipW { get; init; }
    public float ChipTopY { get; init; }
    public bool HasRibbon => RibbonBox.Size.X > 0.0f;
}

/// <summary>
/// The dome plate of a settlement (docs/MVP/33 §3 to §5): a flattened half-circle holding the owner's emblem, standing on a hexagon-ended name
/// ribbon, with a pointer under it that marks the settlement. Everything is drawn with CanvasItem primitives, like the icons in <see cref="UiDraw"/>.
/// The silhouettes are cached by size; every draw call runs in plate-local space through <c>DrawSetTransform</c>.
/// </summary>
public static class UiDome
{
    /// <summary>Dome width (close level) by size step: band, village, town, city, and a capital one step up from its tier.</summary>
    public static readonly float[] DomeWidths = { 40, 50, 60, 70, 80 };
    private static readonly int[] NameSizes = { 16, 17, 18, 19, 20 };
    public const float Aspect = 2.2f;
    public const float CompactAspect = 2.0f;
    public const float PipRadius = 8.0f;
    private const float Overlap = 3.0f;
    private const float CrestHeight = 11.0f;
    private const float ChipHeight = 20.0f;
    private const float ChipTuck = 1.0f;
    private const int ChipFontSize = 13;

    private static readonly Color Keyline = new(0.051f, 0.024f, 0.016f, 0.62f);
    private static readonly Color Shadow = new(0.04f, 0.02f, 0.012f, 1.0f);
    private static readonly Color RibbonFill = new("#2A1911");
    private static readonly Color DomeTop = new("#41291D");
    private static readonly Color DomeBase = new("#1B100B");
    private static readonly Color WildGrey = new("#8A8478");

    // ---- sizes -------------------------------------------------------------------------------

    private static int Step(in PlateFacts f) => Math.Min((int)f.Tier + (f.Capital ? 1 : 0), DomeWidths.Length - 1);

    private static float FullDomeWidth(in PlateFacts f)
    {
        if (f.Empty) return DomeWidths[0];
        if (f.Relation == PlateRelation.Wild) return 44.0f;
        return DomeWidths[Step(f)];
    }

    private static Font NameFont(in PlateFacts f) => f.Tier == PlateTier.City || f.Capital ? UiTheme.Display : UiTheme.DisplaySemi;

    private static readonly Dictionary<(string, int, int), float> WidthCache = new();

    private static float TextWidth(Font font, int kind, string text, int size)
    {
        if (text.Length == 0) return 0.0f;
        if (WidthCache.TryGetValue((text, size, kind), out float cached)) return cached;
        float width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        if (WidthCache.Count > 2048) WidthCache.Clear();
        WidthCache[(text, size, kind)] = width;
        return width;
    }

    /// <summary>The size of a plate at <paramref name="level"/>. <paramref name="count"/> is the head-count text, or null for none.</summary>
    public static PlateMetrics Measure(PlateLevel level, in PlateFacts f, string name, string? count, bool withChip = true)
    {
        if (level == PlateLevel.Pip) return MeasurePip(f, name);
        if (level == PlateLevel.Names) return MeasureNames(f, name);
        bool full = level == PlateLevel.Full;
        float domeW = full ? FullDomeWidth(f) : f.Empty || f.Relation == PlateRelation.Wild ? 30.0f : f.Capital ? 38.0f : 34.0f;
        float domeH = domeW / (full ? Aspect : CompactAspect);
        int nameSize = full ? NameSizes[f.Empty ? 0 : f.Relation == PlateRelation.Wild ? 1 : Step(f)] : UiTokens.FontSmall;
        int countSize = full ? UiTokens.FontMicro : 12;
        float ribbonH = nameSize + (full ? 10 : 7);
        float pointerH = full ? 6.0f : 5.0f;
        bool hex = f.Tier != PlateTier.Band || f.Relation == PlateRelation.Wild;
        float cut = hex ? ribbonH * 0.2887f : 0.0f;
        float nameW = TextWidth(NameFont(f), f.Capital || f.Tier == PlateTier.City ? 1 : 0, name, nameSize);
        bool hasCount = !string.IsNullOrEmpty(count);
        float countW = hasCount ? TextWidth(UiTheme.Num, 2, count!, countSize) : 0.0f;
        float content = nameW + (hasCount ? 21.0f + countW : 0.0f);
        float ribbonW = Math.Max(content + 2.0f * (10.0f + cut), domeW + 8.0f + 2.0f * cut);
        ribbonW = MathF.Ceiling(ribbonW / 2.0f) * 2.0f;
        string? chip = full && withChip ? AlertText(f) : null;
        float chipH = chip is null ? 0.0f : ChipHeight;
        float chipW = chip is null ? 0.0f : TextWidth(UiTheme.Medium, 3, chip, ChipFontSize) + 36.0f;
        float chipTop = -pointerH - chipH;
        float ribbonBottom = chip is null ? -pointerH : chipTop + ChipTuck;
        float ribbonTop = ribbonBottom - ribbonH;
        float boxW = Math.Max(ribbonW, chipW);
        float domeBase = ribbonTop + Overlap;
        float domeTop = domeBase - domeH;
        bool crest = f.Capital && !f.Empty;
        float crestH = crest ? CrestHeight : 0.0f;
        return new PlateMetrics
        {
            Level = level, DomeW = domeW, DomeH = domeH, RibbonW = ribbonW, RibbonH = ribbonH, PointerH = pointerH,
            NameSize = nameSize, CountSize = countSize, NameW = nameW, CountW = countW, HasCount = hasCount, HexEnds = hex, Crest = crest,
            DomeBaseY = domeBase, DomeTopY = domeTop, RibbonTopY = ribbonTop,
            DomeBox = new Rect2(-domeW * 0.5f, domeTop - crestH, domeW, domeH + crestH),
            RibbonBox = new Rect2(-boxW * 0.5f, ribbonTop, boxW, -ribbonTop),
            Chip = chip, ChipW = chipW, ChipTopY = chipTop,
        };
    }

    private const int NamesFontSize = 15;
    private const float NamesEmblemSlot = 17.0f, NamesDotSlot = 13.0f;

    private static float NamesLeadSlot(in PlateFacts f) => f.Empty ? 0.0f : NamesEmblemSlot;
    private static float NamesTrailSlot(in PlateFacts f) => f.Alert != PlateAlert.None && !QuietAlert(f.Alert) && !f.Empty && !f.Remembered ? NamesDotSlot : 0.0f;

    private static PlateMetrics MeasureNames(in PlateFacts f, string name)
    {
        float ribbonH = NamesFontSize + 9.0f;
        const float pointerH = 5.0f;
        bool hex = f.Tier != PlateTier.Band || f.Relation == PlateRelation.Wild;
        float cut = hex ? ribbonH * 0.2887f : 0.0f;
        float nameW = TextWidth(NameFont(f), f.Capital || f.Tier == PlateTier.City ? 1 : 0, name, NamesFontSize);
        float ribbonW = MathF.Ceiling((NamesLeadSlot(f) + nameW + NamesTrailSlot(f) + 2.0f * (9.0f + cut)) / 2.0f) * 2.0f;
        float top = -pointerH - ribbonH;
        return new PlateMetrics
        {
            Level = PlateLevel.Names, RibbonW = ribbonW, RibbonH = ribbonH, PointerH = pointerH, NameSize = NamesFontSize, NameW = nameW, HexEnds = hex,
            DomeBaseY = top, DomeTopY = top, RibbonTopY = top,
            RibbonBox = new Rect2(-ribbonW * 0.5f, top, ribbonW, -top),
        };
    }

    private static PlateMetrics MeasurePip(in PlateFacts f, string name)
    {
        float textW = name.Length > 0 ? TextWidth(UiTheme.DisplaySemi, 0, name, UiTokens.FontSmall) : 0.0f;
        return new PlateMetrics
        {
            Level = PlateLevel.Pip, DomeW = PipRadius * 2, DomeH = PipRadius, NameSize = UiTokens.FontSmall, NameW = textW,
            DomeBaseY = 0, DomeTopY = -PipRadius, Crest = f.Capital,
            DomeBox = new Rect2(-PipRadius, -PipRadius - (f.Capital ? 8.0f : 0.0f), PipRadius * 2, PipRadius + (f.Capital ? 8.0f : 0.0f)),
            RibbonBox = textW > 0 ? new Rect2(PipRadius + 2.0f, -PipRadius - 5.0f, textW + 4.0f, 18.0f) : default,
        };
    }

    // ---- alerts ------------------------------------------------------------------------------

    /// <summary>The chip's words for an alert, or null (docs/MVP/33 §2.2). Besieged carries the wall's integrity, a raid its seasons.</summary>
    public static string? AlertText(in PlateFacts f) => f.Alert switch
    {
        PlateAlert.Besieged => $"Besieged · {f.AlertNumber}%",
        PlateAlert.Raid => f.AlertNumber <= 0 ? "Raid arriving" : f.AlertNumber == 1 ? "Raid · 1 season" : $"Raid · {f.AlertNumber} seasons",
        PlateAlert.Occupied => "Occupied",
        PlateAlert.Famine => "Famine",
        PlateAlert.Short => "Short of food",
        PlateAlert.Fraying => "Fraying",
        PlateAlert.HungryWinter => "Hungry winter ahead",
        PlateAlert.FireOut => "Fire out",
        PlateAlert.Splitting => "Splitting",
        PlateAlert.LevyTarget => "Levy target",
        _ => null,
    };

    /// <summary>Hungry winter, fire out and splitting are only worth a chip at close zoom or on hover (Q10).</summary>
    public static bool QuietAlert(PlateAlert alert) => alert is PlateAlert.HungryWinter or PlateAlert.FireOut or PlateAlert.Splitting;

    public static Color AlertColor(in PlateFacts f) => f.Alert switch
    {
        PlateAlert.Besieged or PlateAlert.Raid or PlateAlert.Famine => UiTokens.Danger,
        PlateAlert.Occupied => f.Relation == PlateRelation.Own ? UiTokens.Danger : UiTokens.Warning,
        PlateAlert.Splitting => UiTokens.Info,
        PlateAlert.LevyTarget => UiTokens.Accent,
        _ => UiTokens.Warning,
    };

    private static UiGlyph AlertGlyph(in PlateFacts f) => f.Alert switch
    {
        PlateAlert.Besieged or PlateAlert.Raid or PlateAlert.Famine => UiGlyph.Danger,
        PlateAlert.Occupied => f.Relation == PlateRelation.Own ? UiGlyph.Danger : UiGlyph.Warning,
        PlateAlert.FireOut => UiGlyph.Hearth,
        PlateAlert.Splitting => UiGlyph.Info,
        PlateAlert.LevyTarget => UiGlyph.Flags,
        _ => UiGlyph.Warning,
    };

    // ---- shapes ------------------------------------------------------------------------------

    private static readonly Dictionary<long, Vector2[]> ArcCache = new();

    /// <summary>The arch from the left foot over the apex to the right foot of a half-ellipse (half-width <paramref name="a"/>, height <paramref name="b"/>), standing on y = 0.</summary>
    private static Vector2[] Arc(float a, float b)
    {
        long key = ((long)MathF.Round(a * 4.0f) << 28) | (long)MathF.Round(b * 4.0f);
        if (ArcCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        int segments = Math.Clamp((int)(a * 0.8f), 14, 44);
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Pi + Mathf.Pi * i / segments;
            points[i] = new Vector2(Mathf.Cos(angle) * a, Mathf.Sin(angle) * b);
        }
        ArcCache[key] = points;
        return points;
    }

    private static readonly Dictionary<long, Vector2[]> RimCache = new();

    /// <summary>Spacing, depth and half-width of one notch of a defence silhouette, in pixels.</summary>
    private static (float Spacing, float Depth, float Half) Notch(PlateDefence d) => d switch
    {
        PlateDefence.Thorn => (5.0f, 3.5f, 1.0f),
        PlateDefence.Palisade => (5.5f, 6.0f, 2.0f),
        _ => (8.5f, 3.5f, 2.5f),
    };

    /// <summary>
    /// The dome's outline from the left foot over the apex to the right foot, with the defence spliced in: fine spikes (thorn barrier), pointed stakes
    /// (palisade) or square battlements (stone walls, hillfort, castle). The notches are spread evenly along the arc, so the silhouette is symmetric.
    /// </summary>
    private static Vector2[] Rim(float a, float b, PlateDefence defence, float rise = 1.0f)
    {
        if (defence == PlateDefence.None) return Arc(a, b);
        long key = ((long)MathF.Round(a * 4.0f) << 32) | ((long)MathF.Round(b * 4.0f) << 4) | (long)defence;
        if (rise >= 1.0f && RimCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        const int n = 240;
        var fine = new Vector2[n + 1];
        float total = 0.0f;
        for (int i = 0; i <= n; i++)
        {
            float angle = Mathf.Pi + Mathf.Pi * i / n;
            fine[i] = new Vector2(Mathf.Cos(angle) * a, Mathf.Sin(angle) * b);
            if (i > 0) total += fine[i].DistanceTo(fine[i - 1]);
        }
        (float spacing, float depth, float half) = Notch(defence);
        depth *= Math.Clamp(rise, 0.0f, 1.0f); // a new defence grows out of the rim (stakes rise), so its depth is scaled while it does
        int count = Math.Max(2, (int)MathF.Round(total / spacing));
        spacing = total / count;
        var points = new List<Vector2>(n) { fine[0] };
        var at = new List<float> { 0.0f };
        float acc = 0.0f, next = spacing * 0.5f, resume = 0.0f;
        for (int i = 1; i <= n; i++)
        {
            acc += fine[i].DistanceTo(fine[i - 1]);
            if (i < n && acc >= next && next < total)
            {
                while (points.Count > 1 && at[^1] > acc - half) { points.RemoveAt(points.Count - 1); at.RemoveAt(at.Count - 1); }
                Vector2 p = fine[i], t = (fine[i + 1] - fine[i - 1]).Normalized(), normal = new(t.Y, -t.X);
                Vector2 l = p - t * half, r = p + t * half;
                switch (defence)
                {
                    case PlateDefence.Thorn:
                        points.Add(l); points.Add(p + normal * depth); points.Add(r);
                        break;
                    case PlateDefence.Palisade:
                        points.Add(l); points.Add(l + normal * depth * 0.7f); points.Add(p + normal * depth); points.Add(r + normal * depth * 0.7f); points.Add(r);
                        break;
                    default:
                        points.Add(l); points.Add(l + normal * depth); points.Add(r + normal * depth); points.Add(r);
                        break;
                }
                for (int j = at.Count; j < points.Count; j++) at.Add(acc);
                next += spacing;
                resume = acc + half;
            }
            else if (acc >= resume || i == n)
            {
                points.Add(fine[i]);
                at.Add(acc);
            }
        }
        Vector2[] result = points.ToArray();
        if (rise >= 1.0f) RimCache[key] = result;
        return result;
    }

    private static readonly Dictionary<long, Vector2[][]> TowerCache = new();

    /// <summary>The two small towers a castle stands on at the dome's feet, with a notch between their battlements.</summary>
    private static Vector2[][] Towers(float a, float b)
    {
        long key = ((long)MathF.Round(a * 4.0f) << 28) | (long)MathF.Round(b * 4.0f);
        if (TowerCache.TryGetValue(key, out Vector2[][]? cached)) return cached;
        float w = 4.0f, cx = a - 3.5f;
        float ratio = Math.Min(cx / a, 0.99f);
        float top = -(b * MathF.Sqrt(1.0f - ratio * ratio) + 5.0f);
        Vector2[] Tower(float centre) => new[]
        {
            new Vector2(centre - w, 1.0f), new Vector2(centre - w, top - 2.5f), new Vector2(centre - w + 2.2f, top - 2.5f), new Vector2(centre - w + 2.2f, top),
            new Vector2(centre + w - 2.2f, top), new Vector2(centre + w - 2.2f, top - 2.5f), new Vector2(centre + w, top - 2.5f), new Vector2(centre + w, 1.0f),
        };
        Vector2[][] towers = { Tower(-cx), Tower(cx) };
        TowerCache[key] = towers;
        return towers;
    }

    private static readonly Dictionary<long, Vector2[][]> MeterCache = new();

    /// <summary>The eight arcs of the ring meter, left foot to right foot, on an ellipse of half-width <paramref name="a"/> and height <paramref name="b"/>.</summary>
    private static Vector2[][] MeterSegments(float a, float b)
    {
        long key = ((long)MathF.Round(a * 4.0f) << 28) | (long)MathF.Round(b * 4.0f);
        if (MeterCache.TryGetValue(key, out Vector2[][]? cached)) return cached;
        const int perSegment = 5;
        const float gap = 0.045f;
        var segments = new Vector2[8][];
        for (int i = 0; i < 8; i++)
        {
            float from = Mathf.Pi + i * Mathf.Pi / 8.0f + gap, to = Mathf.Pi + (i + 1) * Mathf.Pi / 8.0f - gap;
            var points = new Vector2[perSegment];
            for (int j = 0; j < perSegment; j++)
            {
                float angle = Mathf.Lerp(from, to, j / (float)(perSegment - 1));
                points[j] = new Vector2(Mathf.Cos(angle) * a, Mathf.Sin(angle) * b);
            }
            segments[i] = points;
        }
        MeterCache[key] = segments;
        return segments;
    }

    private static readonly Dictionary<long, Vector2[]> CapsuleCache = new();

    /// <summary>A pill outline centred on the origin.</summary>
    private static Vector2[] Capsule(float w, float h)
    {
        long key = ((long)MathF.Round(w) << 16) | (long)MathF.Round(h);
        if (CapsuleCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        float r = h * 0.5f, cx = w * 0.5f - r;
        const int steps = 7;
        var points = new Vector2[(steps + 1) * 2];
        for (int i = 0; i <= steps; i++)
        {
            float angle = -Mathf.Pi * 0.5f + Mathf.Pi * i / steps;
            points[i] = new Vector2(cx + Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            points[steps + 1 + i] = new Vector2(-cx - Mathf.Cos(angle) * r, -Mathf.Sin(angle) * r);
        }
        CapsuleCache[key] = points;
        return points;
    }

    private static readonly Dictionary<long, Vector2[]> RibbonCache = new();

    /// <summary>The ribbon outline in plate space, clockwise from the left tip. A hexagon with 60° ends, or a plain rectangle for a band.</summary>
    private static Vector2[] RibbonOutline(in PlateMetrics m)
    {
        long key = ((long)MathF.Round(m.RibbonW) << 32) | ((long)MathF.Round(-m.RibbonTopY) << 16) | ((long)MathF.Round(m.RibbonH) << 4) | (m.HexEnds ? 1L : 0L);
        if (RibbonCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        float w = m.RibbonW * 0.5f, top = m.RibbonTopY, bottom = m.RibbonTopY + m.RibbonH, mid = (top + bottom) * 0.5f;
        float cut = m.HexEnds ? m.RibbonH * 0.2887f : 0.0f;
        Vector2[] outline = m.HexEnds
            ? new[] { new Vector2(-w, mid), new Vector2(-w + cut, top), new Vector2(w - cut, top), new Vector2(w, mid), new Vector2(w - cut, bottom), new Vector2(-w + cut, bottom) }
            : new[] { new Vector2(-w, top), new Vector2(w, top), new Vector2(w, bottom), new Vector2(-w, bottom) };
        RibbonCache[key] = outline;
        return outline;
    }

    private static Vector2[] ClosedLoop(Vector2[] outline)
    {
        var loop = new Vector2[outline.Length + 1];
        Array.Copy(outline, loop, outline.Length);
        loop[^1] = outline[0];
        return loop;
    }

    private static readonly Dictionary<Vector2[], Vector2[]> LoopCache = new();

    private static Vector2[] Loop(Vector2[] outline)
    {
        if (LoopCache.TryGetValue(outline, out Vector2[]? cached)) return cached;
        Vector2[] loop = ClosedLoop(outline);
        LoopCache[outline] = loop;
        return loop;
    }

    private static readonly Dictionary<int, Color[]> ColorScratch = new();

    private static Color[] Scratch(int length)
    {
        if (!ColorScratch.TryGetValue(length, out Color[]? colors)) ColorScratch[length] = colors = new Color[length];
        return colors;
    }

    // ---- colours -----------------------------------------------------------------------------

    private static Color OwnerColor(in PlateFacts f) => f.Relation == PlateRelation.Wild || f.Civ < 0 ? WildGrey : UiTokens.Civ(f.Civ);

    /// <summary>The rim colour: friend or foe at a glance (docs/MVP/33 §4.1).</summary>
    public static Color RelationColor(in PlateFacts f) => f.Relation switch
    {
        PlateRelation.Own => UiTokens.Civ(f.Civ),
        PlateRelation.AtWar => UiTokens.Danger,
        PlateRelation.Allied => UiTokens.Success,
        PlateRelation.Contacted => UiTokens.WithAlpha(UiTokens.TextTertiary, 0.75f),
        PlateRelation.Wild => UiTokens.WithAlpha(UiTokens.TextTertiary, 0.55f),
        _ => UiTokens.WithAlpha(UiTokens.TextTertiary, 0.45f),
    };

    private static Color Faded(Color c, float alpha) => new(c.R, c.G, c.B, c.A * alpha);

    // ---- drawing -----------------------------------------------------------------------------

    /// <summary>
    /// Draws one plate with its anchor at <paramref name="at"/>. <paramref name="alpha"/> multiplies every colour; <paramref name="pulse"/> (0.55 to 1)
    /// breathes the rim of a plate with an urgent alert and is 1 when motion is off.
    /// </summary>
    public static void Draw(CanvasItem ci, Vector2 at, in PlateMetrics m, in PlateFacts f, string name, string? count, float alpha, bool focus, float pulse = 1.0f)
        => Draw(ci, at, m, f, name, count, alpha, focus, pulse, PlateMotion.Rest);

    public static void Draw(CanvasItem ci, Vector2 at, in PlateMetrics m, in PlateFacts f, string name, string? count, float alpha, bool focus, float pulse, in PlateMotion mo)
    {
        at = at.Round();
        switch (m.Level)
        {
            case PlateLevel.Pip: DrawPip(ci, at, m, f, name, alpha, focus, pulse, mo); break;
            case PlateLevel.Names: DrawNames(ci, at, m, f, name, alpha, focus, pulse, mo); break;
            default: DrawPlate(ci, at, m, f, name, count, alpha, focus, pulse, mo); break;
        }
        ci.DrawSetTransform(Vector2.Zero);
    }

    private static float Ease(float t) { t = Math.Clamp(t, 0.0f, 1.0f); return t * t * (3.0f - 2.0f * t); }

    /// <summary>
    /// Draws the part of an outline (left foot, over the apex, right foot) that lies between <paramref name="from"/> and <paramref name="to"/> (0 at a foot, 1 at the
    /// apex), on both sides at once: a pulse that climbs from the feet, or a colour that recolours the rim from the base up.
    /// </summary>
    private static void DrawRimBand(CanvasItem ci, Vector2[] shape, float from, float to, Color colour, float width)
    {
        int half = shape.Length / 2;
        int first = Math.Clamp((int)MathF.Floor(from * half), 0, half), last = Math.Clamp((int)MathF.Ceiling(to * half), 0, half);
        if (last - first < 1) return;
        var left = new Vector2[last - first + 1];
        var right = new Vector2[last - first + 1];
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = shape[first + i];
            right[i] = shape[shape.Length - 1 - first - i];
        }
        ci.DrawPolyline(left, colour, width, true);
        ci.DrawPolyline(right, colour, width, true);
    }

    private static void DrawDashed(CanvasItem ci, Vector2[] points, Color color, float width, int on, int off)
    {
        int period = on + off;
        for (int i = 0; i + 1 < points.Length; i++)
            if (i % period < on) ci.DrawLine(points[i], points[i + 1], color, width, true);
    }

    private static void DrawPlate(CanvasItem ci, Vector2 at, in PlateMetrics m, in PlateFacts f, string name, string? count, float alpha, bool focus, float pulse, in PlateMotion mo)
    {
        bool full = m.Level == PlateLevel.Full;
        float a = m.DomeW * 0.5f, b = m.DomeH;
        PlateDefence defence = full ? f.Defence : PlateDefence.None;
        Vector2[] arc = Arc(a, b);
        Vector2[] rimShape = Rim(a, b, defence, Ease(mo.Rise));
        Vector2[] ribbon = RibbonOutline(m);
        Color owner = OwnerColor(f);
        Color rim = RelationColor(f);
        bool war = f.Relation == PlateRelation.AtWar;
        float rimWidth = war || focus ? 2.0f : 1.5f;
        float fade = f.Remembered ? 0.62f : 1.0f;
        float grow = Ease(mo.Grow);
        float k = alpha * fade * grow;
        float rimK = f.Urgent ? k * pulse : k;
        float baseY = m.DomeBaseY;
        // The dome rises out of the ribbon (scale-y 0.6 to 1) while the plate fades in; everything drawn in dome space shares that scale.
        Vector2 domeScale = new(1.0f, 0.6f + 0.4f * grow);
        float domeLift = (m.DomeBaseY - m.DomeTopY) * domeScale.Y;
        float chipE = Ease(mo.Chip);
        bool ring = full && !f.Empty && (f.Meter == PlateMeter.Siege || f.Relation == PlateRelation.Own && m.DomeH > 18.0f);
        Vector2[]? chipShape = m.Chip is null ? null : Capsule(m.ChipW, ChipHeight);
        Vector2 chipCentre = new(0.0f, m.ChipTopY + ChipHeight * 0.5f - 4.0f * (1.0f - chipE)); // the chip slides down 4 px as it appears
        float chipK = k * chipE;

        // 1. Soft two-step shadow lifts the plate off bright sand and snow.
        for (int step = 0; step < 2; step++)
        {
            float drop = step == 0 ? 3.0f : 6.0f;
            Color shade = Faded(new Color(Shadow, step == 0 ? 0.34f : 0.14f), k);
            if (!f.Empty)
            {
                ci.DrawSetTransform(at + new Vector2(0, baseY + drop), 0.0f, domeScale);
                ci.DrawColoredPolygon(arc, shade);
            }
            ci.DrawSetTransform(at + new Vector2(0, drop));
            ci.DrawColoredPolygon(ribbon, shade);
            if (chipShape is not null)
            {
                ci.DrawSetTransform(at + chipCentre + new Vector2(0, drop));
                ci.DrawColoredPolygon(chipShape, Faded(shade, chipE));
            }
        }

        // 2. Pointer: the tip is the settlement.
        ci.DrawSetTransform(at);
        Vector2[] pointer = Pointer(-m.PointerH);
        ci.DrawColoredPolygon(pointer, Faded(RibbonFill, k));
        ci.DrawPolyline(pointer, Faded(Keyline, k), 1.0f, true);

        // 3. Dome (drawn in its own space, origin at the middle of its foot): defence silhouette, gradient fill with a wash of the owner's colour, keyline, rim.
        ci.DrawSetTransform(at + new Vector2(0, baseY), 0.0f, domeScale);
        if (defence != PlateDefence.None) ci.DrawColoredPolygon(rimShape, Faded(DomeTop, k));
        if (!f.Empty)
        {
            Color[] colors = Scratch(arc.Length);
            for (int i = 0; i < arc.Length; i++)
            {
                float t = Mathf.Clamp(-arc[i].Y / b, 0.0f, 1.0f);
                Color fill = DomeBase.Lerp(DomeTop, t).Lerp(owner, 0.20f * t);
                colors[i] = Faded(new Color(fill, 0.94f), k);
            }
            ci.DrawPolygon(arc, colors);
        }
        if (defence == PlateDefence.Castle)
            foreach (Vector2[] tower in Towers(a, b))
            {
                ci.DrawColoredPolygon(tower, Faded(DomeTop, k * mo.Rise));
                ci.DrawPolyline(tower, Faded(Keyline, k * mo.Rise), 3.0f, true);
                ci.DrawPolyline(tower, Faded(rim, rimK * mo.Rise), 1.0f, true);
            }
        ci.DrawPolyline(rimShape, Faded(Keyline, k), rimWidth + 2.0f, true);
        if (f.Remembered) DrawDashed(ci, arc, Faded(rim, k), rimWidth, 3, 2);
        else if (f.Empty) DrawDashed(ci, arc, Faded(rim, k), rimWidth, 1, 2);
        else
        {
            // A change of owner or relation recolours the rim from the feet up: the old colour stays above the advancing new one.
            ci.DrawPolyline(rimShape, Faded(mo.Flip < 1.0f ? mo.OldRim : rim, rimK), rimWidth, true);
            if (mo.Flip < 1.0f) DrawRimBand(ci, rimShape, 0.0f, Ease(mo.Flip), Faded(rim, rimK), rimWidth);
            // A new alert sends one bright pulse up each side of the rim.
            if (mo.Sweep < 1.0f)
            {
                float head = Ease(mo.Sweep) * 1.25f;
                DrawRimBand(ci, rimShape, head - 0.25f, Math.Min(head, 1.0f), Faded(UiTokens.Parchment, k * 0.85f), rimWidth + 1.0f);
            }
        }

        if (full && !f.Empty)
        {
            if (f.Tier == PlateTier.City)
                ci.DrawPolyline(Arc(a - 3.5f, b - 3.5f), Faded(rim, k * 0.8f), 1.0f, true); // a city wears a second rim
            if (ring) DrawMeter(ci, a - (f.Tier == PlateTier.City ? 7.5f : 5.0f), b - (f.Tier == PlateTier.City ? 7.5f : 5.0f), f, owner, k, mo);
            // Top bevel: the diorama sun is high, so the upper arc catches a hairline of light.
            ci.DrawPolyline(Bevel(a - 1.5f, b - 1.5f), Faded(new Color(UiTokens.Parchment, 0.16f), k), 1.0f, true);
        }
        ci.DrawSetTransform(at);

        // 4. Emblem.
        float emblemR = Math.Min(m.DomeH * (full ? 0.36f : 0.40f), m.DomeW * 0.3f) * (ring ? (f.Tier == PlateTier.City ? 0.82f : 0.88f) : 1.0f) * (0.75f + 0.25f * grow);
        Vector2 emblemAt = new(0.0f, baseY - m.DomeH * domeScale.Y * (full ? (ring ? 0.41f : 0.43f) : 0.46f));
        DrawFlippingEmblem(ci, at, f, emblemAt, emblemR, k, mo);

        // 5. Capital crest: a raised notch topped with a star.
        if (m.Crest) DrawCrest(ci, new Vector2(0.0f, baseY - domeLift), k, full);

        // 6. Alert chip, hung under the ribbon, then the ribbon on top of the dome's foot (it overlaps the chip's top edge).
        if (chipShape is not null) DrawChip(ci, at, at + chipCentre, chipShape, m, f, chipK);
        ci.DrawColoredPolygon(ribbon, Faded(RibbonFill, 0.97f * k));
        Vector2[] loop = Loop(ribbon);
        ci.DrawPolyline(loop, Faded(Keyline, k), 3.0f, true);
        ci.DrawPolyline(loop, Faded(UiTokens.WithAlpha(rim, rim.A * 0.85f), k), 1.0f, true);
        float topHalf = m.RibbonW * 0.5f - (m.HexEnds ? m.RibbonH * 0.2887f : 0.0f) - 1.5f;
        ci.DrawLine(new Vector2(-topHalf, m.RibbonTopY + 1.5f), new Vector2(topHalf, m.RibbonTopY + 1.5f), Faded(new Color(UiTokens.Parchment, 0.10f), k), 1.0f);

        // 7. Name, hairline, head count.
        Font font = NameFont(f);
        Color nameColor = f.Remembered || f.Empty ? UiTokens.TextSecondary : UiTokens.TextPrimary;
        float middle = m.RibbonTopY + m.RibbonH * 0.5f;
        float content = m.NameW + (m.HasCount ? 21.0f + m.CountW : 0.0f);
        float x = -content * 0.5f;
        float baseline = middle + (font.GetAscent(m.NameSize) - font.GetDescent(m.NameSize)) * 0.5f;
        ci.DrawString(font, new Vector2(x, baseline), name, HorizontalAlignment.Left, -1, m.NameSize, Faded(nameColor, k));
        if (m.HasCount)
        {
            float dividerX = x + m.NameW + 10.0f;
            ci.DrawLine(new Vector2(dividerX, middle - m.RibbonH * 0.28f), new Vector2(dividerX, middle + m.RibbonH * 0.28f), Faded(UiTokens.BorderStrong, k), 1.0f);
            Font num = UiTheme.Num;
            float countBase = middle + (num.GetAscent(m.CountSize) - num.GetDescent(m.CountSize)) * 0.5f;
            ci.DrawString(num, new Vector2(dividerX + 10.0f, countBase), count!, HorizontalAlignment.Left, -1, m.CountSize, Faded(UiTokens.TextSecondary, k));
        }

        // 8a. War and alliance also get a mark on the left shoulder, so the difference does not rest on colour alone.
        if (!f.Empty && !f.Remembered && f.Relation is PlateRelation.AtWar or PlateRelation.Allied)
            DrawRelationMark(ci, at + new Vector2(-0.80f * a, baseY - 0.80f * b * domeScale.Y), f, k);

        // 8. Compact plates have no room for a chip: the alert is a dot on the dome's right shoulder.
        if (!full && f.Alert != PlateAlert.None && !QuietAlert(f.Alert))
            DrawShoulderDot(ci, at + new Vector2(0.7071f * a, baseY - 0.7071f * b), 3.5f, f, k * (f.Urgent ? pulse : 1.0f));
    }

    /// <summary>The "Names only" plate: the ribbon, the owner's small emblem, the name, and a dot for an alert that matters. No dome, no head count.</summary>
    private static void DrawNames(CanvasItem ci, Vector2 at, in PlateMetrics m, in PlateFacts f, string name, float alpha, bool focus, float pulse, in PlateMotion mo)
    {
        float k = alpha * (f.Remembered ? 0.62f : 1.0f) * Ease(mo.Grow);
        Color rim = RelationColor(f);
        float rimK = f.Urgent ? k * pulse : k;
        Vector2[] ribbon = RibbonOutline(m);
        for (int step = 0; step < 2; step++)
        {
            ci.DrawSetTransform(at + new Vector2(0, step == 0 ? 3.0f : 6.0f));
            ci.DrawColoredPolygon(ribbon, Faded(new Color(Shadow, step == 0 ? 0.34f : 0.14f), k));
        }
        ci.DrawSetTransform(at);
        Vector2[] pointer = Pointer(-m.PointerH);
        ci.DrawColoredPolygon(pointer, Faded(RibbonFill, k));
        ci.DrawPolyline(pointer, Faded(Keyline, k), 1.0f, true);
        ci.DrawColoredPolygon(ribbon, Faded(RibbonFill, 0.97f * k));
        Vector2[] loop = Loop(ribbon);
        ci.DrawPolyline(loop, Faded(Keyline, k), 3.0f, true);
        ci.DrawPolyline(loop, Faded(UiTokens.WithAlpha(mo.Flip < 1.0f ? mo.OldRim : rim, rim.A * 0.85f), rimK), f.Relation == PlateRelation.AtWar || focus ? 1.6f : 1.0f, true);
        float topHalf = m.RibbonW * 0.5f - (m.HexEnds ? m.RibbonH * 0.2887f : 0.0f) - 1.5f;
        ci.DrawLine(new Vector2(-topHalf, m.RibbonTopY + 1.5f), new Vector2(topHalf, m.RibbonTopY + 1.5f), Faded(new Color(UiTokens.Parchment, 0.10f), k), 1.0f);

        float lead = NamesLeadSlot(f), trail = NamesTrailSlot(f);
        float x = -(lead + m.NameW + trail) * 0.5f;
        float middle = m.RibbonTopY + m.RibbonH * 0.5f;
        if (lead > 0.0f) DrawFlippingEmblem(ci, at, f, new Vector2(x + 6.0f, middle), 6.0f, k, mo);
        Font font = NameFont(f);
        Color nameColor = f.Remembered || f.Empty ? UiTokens.TextSecondary : UiTokens.TextPrimary;
        float baseline = middle + (font.GetAscent(m.NameSize) - font.GetDescent(m.NameSize)) * 0.5f;
        ci.DrawString(font, new Vector2(x + lead, baseline), name, HorizontalAlignment.Left, -1, m.NameSize, Faded(nameColor, k));
        if (trail > 0.0f)
        {
            Vector2 dot = new(x + lead + m.NameW + 8.0f, middle);
            ci.DrawCircle(dot, 5.0f, Faded(Keyline, k));
            ci.DrawCircle(dot, 3.5f, Faded(AlertColor(f), f.Urgent ? k * pulse : k));
        }
    }

    /// <summary>
    /// A shape as well as a colour for the two relations a player must never mix up: a cross on the left shoulder for war, a check for an alliance. Red and
    /// green rims turn into the same olive for many colour-blind players (checked with deuteranopia and protanopia filters on the gallery).
    /// </summary>
    private static void DrawRelationMark(CanvasItem ci, Vector2 at, in PlateFacts f, float k)
    {
        bool war = f.Relation == PlateRelation.AtWar;
        Color c = war ? UiTokens.Danger : UiTokens.Success;
        ci.DrawSetTransform(Vector2.Zero);
        ci.DrawCircle(at, 7.0f, Faded(Keyline, k));
        ci.DrawCircle(at, 5.7f, Faded(c, k));
        Color ink = Faded(new Color(0.05f, 0.02f, 0.01f, 0.95f), k);
        if (war)
        {
            ci.DrawLine(at + new Vector2(-2.6f, -2.6f), at + new Vector2(2.6f, 2.6f), ink, 1.9f, true);
            ci.DrawLine(at + new Vector2(-2.6f, 2.6f), at + new Vector2(2.6f, -2.6f), ink, 1.9f, true);
        }
        else
        {
            ci.DrawPolyline(new[] { at + new Vector2(-2.9f, 0.2f), at + new Vector2(-0.8f, 2.4f), at + new Vector2(3.0f, -2.4f) }, ink, 1.9f, true);
        }
    }

    private static void DrawShoulderDot(CanvasItem ci, Vector2 at, float radius, in PlateFacts f, float k)
    {
        ci.DrawSetTransform(Vector2.Zero);
        ci.DrawCircle(at, radius + 1.5f, Faded(Keyline, k));
        ci.DrawCircle(at, radius, Faded(AlertColor(f), k));
    }

    private static void DrawChip(CanvasItem ci, Vector2 at, Vector2 centre, Vector2[] shape, in PlateMetrics m, in PlateFacts f, float k)
    {
        Color c = AlertColor(f);
        ci.DrawSetTransform(centre);
        ci.DrawColoredPolygon(shape, Faded(new Color(RibbonFill.Lerp(c, 0.22f), 0.97f), k));
        var loop = new Vector2[shape.Length + 1];
        Array.Copy(shape, loop, shape.Length);
        loop[^1] = shape[0];
        ci.DrawPolyline(loop, Faded(Keyline, k), 3.0f, true);
        ci.DrawPolyline(loop, Faded(c, k * 0.9f), 1.0f, true);
        float left = -m.ChipW * 0.5f;
        UiDraw.Glyph(ci, AlertGlyph(f), new Rect2(left + 7.0f, -6.0f, 12.0f, 12.0f), Faded(c, k), 1.6f);
        Font font = UiTheme.Medium;
        float baseline = (font.GetAscent(ChipFontSize) - font.GetDescent(ChipFontSize)) * 0.5f;
        ci.DrawString(font, new Vector2(left + 23.0f, baseline), m.Chip!, HorizontalAlignment.Left, -1, ChipFontSize, Faded(UiTokens.TextPrimary, k));
        ci.DrawSetTransform(at);
    }

    private static readonly Color MeterTrack = new(0.95f, 0.88f, 0.76f, 0.16f);

    /// <summary>
    /// The ring inside the rim: eight segments from the left foot over the top. Build progress lights them in the owner's colour; under siege it shows the
    /// wall's integrity in Danger, draining from the right. An idle own town shows the empty track (docs/MVP/33 §4.1, Q20).
    /// </summary>
    private static void DrawMeter(CanvasItem ci, float a, float b, in PlateFacts f, Color owner, float k, in PlateMotion mo)
    {
        Vector2[][] segments = MeterSegments(a, b);
        int lit = 0;
        float partial = 0.0f;
        Color colour = owner;
        // While the ring catches up with the real value (or flashes after a finished build) it shows the motion's value; segments light one at a time.
        float shown = mo.MeterShown >= 0.0f ? mo.MeterShown : f.MeterPermille;
        if (f.Meter == PlateMeter.Siege)
        {
            lit = (int)MathF.Ceiling(shown * 8.0f / 1000.0f - 0.001f);
            colour = UiTokens.Danger;
        }
        else if (f.Meter == PlateMeter.Build)
        {
            lit = (int)(shown * 8.0f / 1000.0f);
            partial = shown * 8.0f / 1000.0f - lit;
        }
        if (mo.Flash > 0.0f) colour = colour.Lerp(UiTokens.Success, Math.Min(1.0f, mo.Flash * 1.5f));
        for (int i = 0; i < 8; i++)
        {
            ci.DrawPolyline(segments[i], Faded(MeterTrack, k), 3.0f, true);
            if (i < lit) ci.DrawPolyline(segments[i], Faded(colour, k), 3.0f, true);
            else if (i == lit && partial > 0.05f) ci.DrawPolyline(segments[i], Faded(colour, k * (0.25f + 0.5f * partial)), 3.0f, true);
        }
    }

    private static readonly Dictionary<long, Vector2[]> BevelCache = new();

    /// <summary>The upper three fifths of an arch, where the light catches.</summary>
    private static Vector2[] Bevel(float a, float b)
    {
        long key = ((long)MathF.Round(a * 4.0f) << 28) | (long)MathF.Round(b * 4.0f);
        if (BevelCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        Vector2[] full = Arc(a, b);
        int from = full.Length / 5, to = full.Length - full.Length / 5;
        var part = new Vector2[to - from];
        Array.Copy(full, from, part, 0, part.Length);
        BevelCache[key] = part;
        return part;
    }

    private static readonly Dictionary<int, Vector2[]> PointerCache = new();

    private static Vector2[] Pointer(float ribbonBottom)
    {
        int key = (int)MathF.Round(ribbonBottom);
        if (PointerCache.TryGetValue(key, out Vector2[]? cached)) return cached;
        Vector2[] points = { new(-5.0f, ribbonBottom - 1.0f), new(0.0f, 0.0f), new(5.0f, ribbonBottom - 1.0f) };
        PointerCache[key] = points;
        return points;
    }

    /// <summary>The emblem, turning over (scale-x 1 to 0 to 1) when the settlement changed hands: the old owner's emblem first, then the new one.</summary>
    private static void DrawFlippingEmblem(CanvasItem ci, Vector2 at, in PlateFacts f, Vector2 center, float radius, float k, in PlateMotion mo)
    {
        if (!mo.FlipsEmblem || mo.Flip >= 1.0f)
        {
            DrawEmblem(ci, f, center, radius, k, f.Empty);
            return;
        }
        float sx = Math.Max(MathF.Abs(MathF.Cos(mo.Flip * MathF.PI)), 0.04f);
        PlateFacts shown = mo.Flip < 0.5f ? mo.OldFacts : f;
        ci.DrawSetTransform(at + center, 0.0f, new Vector2(sx, 1.0f));
        DrawEmblem(ci, shown, Vector2.Zero, radius, k, shown.Empty);
        ci.DrawSetTransform(at);
    }

    private static readonly Color TinMetal = new("#9DB1BF");
    private static readonly Color TinInk = new("#1F2B33");

    private static void DrawEmblem(CanvasItem ci, in PlateFacts f, Vector2 center, float radius, float k, bool hollow)
    {
        if (hollow)
        {
            ci.DrawArc(center, radius, 0, Mathf.Tau, 20, Faded(UiTokens.WithAlpha(UiTokens.TextTertiary, 0.55f), k), 1.0f, true);
            return;
        }
        if (f.TinFolk && (f.Relation == PlateRelation.Wild || f.Civ < 0))
        {
            // The Tin Folk wear their own mark: a pale tin disc with an ingot on it, so they never read as a plain Free Folk village.
            ci.DrawCircle(center, radius + 1.0f, Faded(new Color(0, 0, 0, 0.55f), k));
            ci.DrawCircle(center, radius, Faded(TinMetal, k));
            float s = radius * 0.62f;
            Vector2[] ingot =
            {
                center + new Vector2(-s * 1.05f, s * 0.6f), center + new Vector2(s * 1.05f, s * 0.6f),
                center + new Vector2(s * 0.62f, -s * 0.5f), center + new Vector2(-s * 0.62f, -s * 0.5f),
            };
            ci.DrawColoredPolygon(ingot, Faded(TinInk, k));
            ci.DrawLine(center + new Vector2(-s * 0.45f, -s * 0.12f), center + new Vector2(s * 0.45f, -s * 0.12f), Faded(TinMetal, k), Math.Max(1.0f, radius * 0.1f), true);
            return;
        }
        if (f.Relation == PlateRelation.Wild || f.Civ < 0)
        {
            ci.DrawCircle(center, radius + 1.0f, Faded(new Color(0, 0, 0, 0.55f), k));
            ci.DrawCircle(center, radius, Faded(WildGrey, k));
            float inner = radius * 1.45f;
            UiDraw.Glyph(ci, f.TinFolk ? UiGlyph.EmblemMountain : UiGlyph.EmblemLeaf,
                new Rect2(center - new Vector2(inner, inner) * 0.5f, new Vector2(inner, inner)), Faded(Colors.White, k), Math.Max(1.2f, radius * 0.12f));
            return;
        }
        if (k >= 0.99f) { UiDraw.Emblem(ci, f.Civ, center, radius); return; }
        // Faded emblems: same shapes with every colour dimmed.
        ci.DrawCircle(center, radius + 1.0f, Faded(new Color(0, 0, 0, 0.55f), k));
        ci.DrawCircle(center, radius, Faded(UiTokens.Civ(f.Civ), k));
        float size = radius * 1.45f;
        Color ink = f.Civ is 3 ? new Color("#1E1608") : Colors.White;
        UiDraw.Glyph(ci, UiDraw.EmblemGlyph(f.Civ), new Rect2(center - new Vector2(size, size) * 0.5f, new Vector2(size, size)), Faded(ink, k), Math.Max(1.2f, radius * 0.12f));
    }

    private static void DrawCrest(CanvasItem ci, Vector2 apex, float k, bool full)
    {
        float s = full ? 1.0f : 0.8f;
        var cap = new[]
        {
            apex + new Vector2(-5.5f, 2.0f) * s, apex + new Vector2(-3.5f, -3.5f) * s,
            apex + new Vector2(3.5f, -3.5f) * s, apex + new Vector2(5.5f, 2.0f) * s,
        };
        ci.DrawColoredPolygon(cap, Faded(DomeTop, k));
        ci.DrawPolyline(new[] { cap[0], cap[1], cap[2], cap[3] }, Faded(UiTokens.Warning, k), 1.2f, true);
        float star = (full ? 12.0f : 10.0f);
        var box = new Rect2(apex + new Vector2(-star * 0.5f, -3.5f * s - star + 1.0f), new Vector2(star, star));
        UiDraw.Glyph(ci, UiGlyph.Star, new Rect2(box.Position - new Vector2(1, 1), box.Size + new Vector2(2, 2)), Faded(new Color(0.05f, 0.02f, 0.01f, 0.9f), k));
        UiDraw.Glyph(ci, UiGlyph.Star, box, Faded(UiTokens.Warning, k));
    }

    private static void DrawPip(CanvasItem ci, Vector2 at, in PlateMetrics m, in PlateFacts f, string name, float alpha, bool focus, float pulse, in PlateMotion mo)
    {
        float fade = f.Remembered ? 0.62f : 1.0f;
        float k = alpha * fade * Ease(mo.Grow);
        Vector2[] arc = Arc(PipRadius, PipRadius);
        Color owner = OwnerColor(f);
        Color rim = RelationColor(f);
        ci.DrawSetTransform(at + new Vector2(0, 1.5f));
        ci.DrawColoredPolygon(arc, Faded(new Color(Shadow, 0.35f), k));
        ci.DrawSetTransform(at);
        if (!f.Empty) ci.DrawColoredPolygon(arc, Faded(owner.Lerp(DomeBase, 0.15f), k));
        ci.DrawPolyline(arc, Faded(Keyline, k), 3.0f, true);
        if (f.Remembered || f.Empty) DrawDashed(ci, arc, Faded(rim, k), 1.5f, 1, 1);
        else ci.DrawPolyline(arc, Faded(rim, f.Urgent ? k * pulse : k), f.Relation == PlateRelation.AtWar || focus ? 2.0f : 1.5f, true);
        ci.DrawLine(new Vector2(-PipRadius, 0), new Vector2(PipRadius, 0), Faded(Keyline, k), 1.0f);
        if (f.Urgent) DrawShoulderDot(ci, at + new Vector2(0.7071f * PipRadius, -0.7071f * PipRadius), 3.0f, f, k * pulse);
        ci.DrawSetTransform(at);
        if (m.Crest)
        {
            var box = new Rect2(new Vector2(-5.0f, -PipRadius - 11.0f), new Vector2(10, 10));
            UiDraw.Glyph(ci, UiGlyph.Star, new Rect2(box.Position - new Vector2(1, 1), box.Size + new Vector2(2, 2)), Faded(new Color(0.05f, 0.02f, 0.01f, 0.9f), k));
            UiDraw.Glyph(ci, UiGlyph.Star, box, Faded(UiTokens.Warning, k));
        }
        if (name.Length > 0)
        {
            Font font = UiTheme.DisplaySemi;
            float baseline = -PipRadius * 0.5f + (font.GetAscent(m.NameSize) - font.GetDescent(m.NameSize)) * 0.5f;
            var pos = new Vector2(PipRadius + 4.0f, baseline);
            ci.DrawStringOutline(font, pos, name, HorizontalAlignment.Left, -1, m.NameSize, 5, Faded(new Color(0.055f, 0.045f, 0.03f, 0.95f), k));
            ci.DrawString(font, pos, name, HorizontalAlignment.Left, -1, m.NameSize, Faded(UiTokens.Parchment, k));
        }
    }

    /// <summary>The hover line: a small pill beside the ribbon with what the settlement is doing or whose it is. It flips to the left near the screen's right edge.</summary>
    public static void DrawCaption(CanvasItem ci, Vector2 at, in PlateMetrics m, string text, float screenWidth)
    {
        at = at.Round();
        const int size = 13;
        float width = TextWidth(UiTheme.Regular, 4, text, size) + 20.0f;
        float height = 22.0f;
        float centreY = at.Y + m.RibbonTopY + m.RibbonH * 0.5f;
        float reach = Math.Max(m.RibbonW, m.ChipW) * 0.5f + 8.0f;
        float centreX = at.X + reach + width * 0.5f;
        if (centreX + width * 0.5f > screenWidth - 8.0f) centreX = at.X - reach - width * 0.5f;
        Vector2[] shape = Capsule(width, height);
        ci.DrawSetTransform(new Vector2(centreX, centreY + 2.0f));
        ci.DrawColoredPolygon(shape, new Color(Shadow, 0.30f));
        ci.DrawSetTransform(new Vector2(centreX, centreY));
        ci.DrawColoredPolygon(shape, new Color(RibbonFill, 0.96f));
        var loop = new Vector2[shape.Length + 1];
        Array.Copy(shape, loop, shape.Length);
        loop[^1] = shape[0];
        ci.DrawPolyline(loop, Keyline, 3.0f, true);
        ci.DrawPolyline(loop, UiTokens.BorderStrong, 1.0f, true);
        Font font = UiTheme.Regular;
        ci.DrawString(font, new Vector2(-width * 0.5f + 10.0f, (font.GetAscent(size) - font.GetDescent(size)) * 0.5f), text, HorizontalAlignment.Left, -1, size, UiTokens.TextPrimary);
        ci.DrawSetTransform(Vector2.Zero);
    }

    // ---- hit testing -------------------------------------------------------------------------

    /// <summary>Whether <paramref name="local"/> (relative to the plate's anchor) lies on its dome or ribbon, not in the empty corners around them.</summary>
    public static bool Hits(in PlateMetrics m, Vector2 local)
    {
        if (m.Level == PlateLevel.Pip)
        {
            if (local.DistanceTo(new Vector2(0, -PipRadius * 0.5f)) <= PipRadius + 3.0f) return true;
            return m.HasRibbon && m.RibbonBox.Grow(2.0f).HasPoint(local);
        }
        if (m.RibbonBox.HasPoint(local)) return true;
        if (m.Level == PlateLevel.Names) return false;
        float a = m.DomeW * 0.5f, b = m.DomeH;
        float x = local.X / a, y = (m.DomeBaseY - local.Y) / b;
        return y >= 0.0f && x * x + y * y <= 1.0f;
    }
}
