using System;
using ResearchTheme = CivSim.Sim.Research.Theme;
using Godot;

namespace CivSim.Game.Ui;

public enum UiGlyph
{
    None, Pause, Play, Plus, Minus, Close, ChevronRight, ChevronDown, ChevronUp, Check, CheckBadge, BlockedBadge, Ring, HalfRing,
    Lock, Queue, ClockRing, Warning, Danger, Info, Hammer, Hearth, Compass, Gem, People, Hut, Flags, Scroll, Menu, Spark, Globe,
    Age, LayerClaims, LayerFood, LayerMethods, LayerResources, Beacon, CompassRose, Spring, Summer, Autumn, Winter, Search, Dot, Star, StarOutline,
    EmblemSun, EmblemWave, EmblemLeaf, EmblemStar, EmblemMountain, EmblemFlame, EmblemMoon, EmblemAntler,
}

/// <summary>Draws every icon the HUD uses with CanvasItem primitives, so no emoji or icon font is needed.</summary>
public static class UiDraw
{
    public static UiGlyph ThemeGlyph(ResearchTheme theme) => theme switch
    {
        ResearchTheme.Craft => UiGlyph.Hammer,
        ResearchTheme.Kin => UiGlyph.Hearth,
        ResearchTheme.Reach => UiGlyph.Compass,
        _ => UiGlyph.Gem,
    };

    public static UiGlyph EmblemGlyph(int civ) => (UiGlyph)((int)UiGlyph.EmblemSun + Math.Clamp(civ, 0, 7));

    public static void Glyph(CanvasItem ci, UiGlyph glyph, Rect2 box, Color color, float stroke = 2.0f, float rotation = 0.0f)
    {
        if (glyph == UiGlyph.None) return;
        float s = Math.Min(box.Size.X, box.Size.Y);
        Vector2 o = box.Position + (box.Size - new Vector2(s, s)) * 0.5f;
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        Vector2 C = P(0.5f, 0.5f);
        float line = stroke;
        float Q(float v) => v * s;

        switch (glyph)
        {
            case UiGlyph.Pause:
                ci.DrawRect(new Rect2(P(0.26f, 0.2f), new Vector2(Q(0.18f), Q(0.6f))), color);
                ci.DrawRect(new Rect2(P(0.56f, 0.2f), new Vector2(Q(0.18f), Q(0.6f))), color);
                break;
            case UiGlyph.Play:
                ci.DrawColoredPolygon(new[] { P(0.3f, 0.2f), P(0.8f, 0.5f), P(0.3f, 0.8f) }, color);
                break;
            case UiGlyph.Plus:
                ci.DrawLine(P(0.5f, 0.2f), P(0.5f, 0.8f), color, line);
                ci.DrawLine(P(0.2f, 0.5f), P(0.8f, 0.5f), color, line);
                break;
            case UiGlyph.Minus:
                ci.DrawLine(P(0.2f, 0.5f), P(0.8f, 0.5f), color, line);
                break;
            case UiGlyph.Close:
                ci.DrawLine(P(0.25f, 0.25f), P(0.75f, 0.75f), color, line);
                ci.DrawLine(P(0.75f, 0.25f), P(0.25f, 0.75f), color, line);
                break;
            case UiGlyph.ChevronRight:
                ci.DrawPolyline(new[] { P(0.38f, 0.22f), P(0.65f, 0.5f), P(0.38f, 0.78f) }, color, line);
                break;
            case UiGlyph.ChevronDown:
                ci.DrawPolyline(new[] { P(0.22f, 0.38f), P(0.5f, 0.65f), P(0.78f, 0.38f) }, color, line);
                break;
            case UiGlyph.ChevronUp:
                ci.DrawPolyline(new[] { P(0.22f, 0.62f), P(0.5f, 0.35f), P(0.78f, 0.62f) }, color, line);
                break;
            case UiGlyph.Check:
                ci.DrawPolyline(new[] { P(0.2f, 0.55f), P(0.42f, 0.75f), P(0.8f, 0.3f) }, color, line);
                break;
            case UiGlyph.CheckBadge:
            case UiGlyph.BlockedBadge:
                ci.DrawCircle(C, Q(0.46f), color);
                ci.DrawPolyline(new[] { P(0.27f, 0.52f), P(0.43f, 0.67f), P(0.74f, 0.33f) }, UiTokens.Surface3, Math.Max(1.5f, line));
                if (glyph == UiGlyph.BlockedBadge) ci.DrawLine(P(0.2f, 0.8f), P(0.8f, 0.2f), UiTokens.Surface3, Math.Max(2.0f, line + 1));
                break;
            case UiGlyph.Ring:
                ci.DrawArc(C, Q(0.36f), 0, Mathf.Tau, 28, color, line, true);
                break;
            case UiGlyph.HalfRing:
                ci.DrawArc(C, Q(0.36f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawColoredPolygon(HalfDisc(C, Q(0.36f)), color);
                break;
            case UiGlyph.Lock:
                ci.DrawArc(P(0.5f, 0.4f), Q(0.18f), Mathf.Pi, Mathf.Tau, 12, color, line, true);
                ci.DrawLine(P(0.32f, 0.4f), P(0.32f, 0.48f), color, line);
                ci.DrawLine(P(0.68f, 0.4f), P(0.68f, 0.48f), color, line);
                ci.DrawRect(new Rect2(P(0.24f, 0.48f), new Vector2(Q(0.52f), Q(0.36f))), color);
                break;
            case UiGlyph.Queue:
                ci.DrawRect(new Rect2(P(0.2f, 0.28f), new Vector2(Q(0.46f), Q(0.34f))), color, false, line);
                ci.DrawRect(new Rect2(P(0.34f, 0.42f), new Vector2(Q(0.46f), Q(0.34f))), color, false, line);
                break;
            case UiGlyph.ClockRing:
                ci.DrawArc(C, Q(0.38f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawLine(C, P(0.5f, 0.28f), color, Math.Max(1.5f, line * 0.8f));
                ci.DrawLine(C, P(0.65f, 0.5f), color, Math.Max(1.5f, line * 0.8f));
                break;
            case UiGlyph.Warning:
                ci.DrawPolyline(new[] { P(0.5f, 0.14f), P(0.9f, 0.82f), P(0.1f, 0.82f), P(0.5f, 0.14f) }, color, line);
                ci.DrawLine(P(0.5f, 0.4f), P(0.5f, 0.6f), color, line);
                ci.DrawCircle(P(0.5f, 0.71f), Math.Max(1.2f, Q(0.04f)), color);
                break;
            case UiGlyph.Danger:
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.12f), P(0.92f, 0.84f), P(0.08f, 0.84f) }, color);
                ci.DrawLine(P(0.5f, 0.38f), P(0.5f, 0.6f), UiTokens.Surface3, line);
                ci.DrawCircle(P(0.5f, 0.72f), Math.Max(1.2f, Q(0.045f)), UiTokens.Surface3);
                break;
            case UiGlyph.Info:
                ci.DrawArc(C, Q(0.4f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawLine(P(0.5f, 0.44f), P(0.5f, 0.72f), color, line);
                ci.DrawCircle(P(0.5f, 0.3f), Math.Max(1.2f, Q(0.05f)), color);
                break;
            case UiGlyph.Hammer:
                DrawRotatedRect(ci, C + new Vector2(0, -Q(0.13f)), new Vector2(Q(0.56f), Q(0.24f)), Mathf.DegToRad(-45), color);
                ci.DrawLine(P(0.46f, 0.54f), P(0.2f, 0.82f), color, line + 0.5f);
                break;
            case UiGlyph.Hearth:
                ci.DrawColoredPolygon(new[]
                {
                    P(0.5f, 0.1f), P(0.68f, 0.38f), P(0.7f, 0.56f), P(0.58f, 0.7f), P(0.42f, 0.7f), P(0.3f, 0.56f), P(0.34f, 0.4f), P(0.44f, 0.46f),
                }, color);
                ci.DrawRect(new Rect2(P(0.2f, 0.78f), new Vector2(Q(0.6f), Math.Max(2f, Q(0.1f)))), color);
                break;
            case UiGlyph.Compass:
                ci.DrawArc(C, Q(0.4f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.2f), P(0.6f, 0.5f), P(0.4f, 0.5f) }, color);
                ci.DrawPolyline(new[] { P(0.4f, 0.5f), P(0.5f, 0.8f), P(0.6f, 0.5f) }, color, Math.Max(1.2f, line * 0.7f));
                break;
            case UiGlyph.Gem:
                ci.DrawPolyline(new[] { P(0.5f, 0.1f), P(0.9f, 0.5f), P(0.5f, 0.9f), P(0.1f, 0.5f), P(0.5f, 0.1f) }, color, line);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.3f), P(0.7f, 0.5f), P(0.5f, 0.7f), P(0.3f, 0.5f) }, color);
                break;
            case UiGlyph.People:
                ci.DrawCircle(P(0.5f, 0.32f), Q(0.16f), color);
                ci.DrawArc(P(0.5f, 0.9f), Q(0.34f), Mathf.Pi * 1.08f, Mathf.Pi * 1.92f, 16, color, line + 1, true);
                break;
            case UiGlyph.Hut:
                ci.DrawRect(new Rect2(P(0.24f, 0.46f), new Vector2(Q(0.52f), Q(0.36f))), color, false, line);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.14f), P(0.86f, 0.48f), P(0.14f, 0.48f) }, color);
                break;
            case UiGlyph.Flags:
                ci.DrawLine(P(0.22f, 0.15f), P(0.22f, 0.9f), color, line);
                ci.DrawColoredPolygon(new[] { P(0.22f, 0.18f), P(0.52f, 0.28f), P(0.22f, 0.42f) }, color);
                ci.DrawLine(P(0.78f, 0.15f), P(0.78f, 0.9f), color, line);
                ci.DrawColoredPolygon(new[] { P(0.78f, 0.34f), P(0.5f, 0.44f), P(0.78f, 0.58f) }, color);
                break;
            case UiGlyph.Scroll:
                ci.DrawRect(new Rect2(P(0.2f, 0.14f), new Vector2(Q(0.6f), Q(0.72f))), color, false, line);
                ci.DrawLine(P(0.32f, 0.34f), P(0.68f, 0.34f), color, Math.Max(1.2f, line * 0.7f));
                ci.DrawLine(P(0.32f, 0.5f), P(0.68f, 0.5f), color, Math.Max(1.2f, line * 0.7f));
                ci.DrawLine(P(0.32f, 0.66f), P(0.56f, 0.66f), color, Math.Max(1.2f, line * 0.7f));
                break;
            case UiGlyph.Menu:
                ci.DrawLine(P(0.2f, 0.3f), P(0.8f, 0.3f), color, line);
                ci.DrawLine(P(0.2f, 0.5f), P(0.8f, 0.5f), color, line);
                ci.DrawLine(P(0.2f, 0.7f), P(0.8f, 0.7f), color, line);
                break;
            case UiGlyph.Spark:
                ci.DrawColoredPolygon(new[]
                {
                    P(0.5f, 0.08f), P(0.6f, 0.4f), P(0.92f, 0.5f), P(0.6f, 0.6f), P(0.5f, 0.92f), P(0.4f, 0.6f), P(0.08f, 0.5f), P(0.4f, 0.4f),
                }, color);
                break;
            case UiGlyph.Globe:
                ci.DrawArc(C, Q(0.4f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawLine(P(0.1f, 0.5f), P(0.9f, 0.5f), color, Math.Max(1.2f, line * 0.7f));
                DrawEllipse(ci, C, Q(0.18f), Q(0.4f), color, Math.Max(1.2f, line * 0.7f));
                break;
            case UiGlyph.Age:
                ci.DrawArc(C, Q(0.4f), 0, Mathf.Tau, 28, color, line, true);
                ci.DrawPolyline(new[] { P(0.32f, 0.58f), P(0.5f, 0.38f), P(0.68f, 0.58f) }, color, line);
                break;
            case UiGlyph.LayerClaims:
                ci.DrawPolyline(Hexagon(C, Q(0.4f), true), color, line);
                ci.DrawColoredPolygon(new[] { P(0.12f, 0.5f), P(0.88f, 0.5f), P(0.84f, 0.7f), P(0.5f, 0.9f), P(0.16f, 0.7f) }, color);
                break;
            case UiGlyph.LayerFood:
                ci.DrawLine(P(0.5f, 0.2f), P(0.5f, 0.9f), color, line);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.28f + i * 0.2f;
                    DrawEllipse(ci, P(0.34f, y), Q(0.1f), Q(0.06f), color, line * 0.9f);
                    DrawEllipse(ci, P(0.66f, y), Q(0.1f), Q(0.06f), color, line * 0.9f);
                }
                break;
            case UiGlyph.LayerMethods:
                ci.DrawRect(new Rect2(P(0.16f, 0.62f), new Vector2(Q(0.18f), Q(0.24f))), color);
                ci.DrawRect(new Rect2(P(0.41f, 0.42f), new Vector2(Q(0.18f), Q(0.44f))), color);
                ci.DrawRect(new Rect2(P(0.66f, 0.18f), new Vector2(Q(0.18f), Q(0.68f))), color);
                break;
            case UiGlyph.LayerResources:
                ci.DrawCircle(P(0.34f, 0.68f), Q(0.17f), color);
                ci.DrawCircle(P(0.66f, 0.68f), Q(0.17f), color);
                ci.DrawCircle(P(0.5f, 0.38f), Q(0.17f), color);
                break;
            case UiGlyph.Beacon:
                ci.DrawRect(new Rect2(P(0.45f, 0.2f), new Vector2(Q(0.1f), Q(0.55f))), color);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.06f), P(0.64f, 0.26f), P(0.36f, 0.26f) }, color);
                DrawEllipse(ci, P(0.5f, 0.84f), Q(0.3f), Q(0.09f), color, line * 0.8f);
                break;
            case UiGlyph.CompassRose:
                ci.DrawArc(C, Q(0.42f), 0, Mathf.Tau, 32, UiTokens.WithAlpha(color, 0.55f), Math.Max(1.2f, line * 0.7f), true);
                Vector2 north = Rot(new Vector2(0, -Q(0.36f)), rotation), south = -north;
                Vector2 side = Rot(new Vector2(Q(0.12f), 0), rotation);
                ci.DrawColoredPolygon(new[] { C + north, C + side, C - side }, UiTokens.Accent);
                ci.DrawColoredPolygon(new[] { C + south, C + side, C - side }, UiTokens.WithAlpha(color, 0.6f));
                break;
            case UiGlyph.Spring:
                ci.DrawLine(P(0.5f, 0.88f), P(0.5f, 0.3f), color, line);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.55f), P(0.18f, 0.42f), P(0.34f, 0.24f) }, color);
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.4f), P(0.84f, 0.28f), P(0.68f, 0.1f) }, color);
                break;
            case UiGlyph.Summer:
                ci.DrawCircle(C, Q(0.2f), color);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(C + dir * Q(0.3f), C + dir * Q(0.45f), color, Math.Max(1.2f, line * 0.8f));
                }
                break;
            case UiGlyph.Autumn:
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.1f), P(0.8f, 0.38f), P(0.7f, 0.7f), P(0.4f, 0.86f), P(0.2f, 0.6f), P(0.28f, 0.28f) }, color);
                ci.DrawLine(P(0.24f, 0.86f), P(0.58f, 0.34f), UiTokens.Surface1, Math.Max(1.2f, line * 0.7f));
                break;
            case UiGlyph.Winter:
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.Pi / 3 + Mathf.Pi / 6;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Q(0.4f);
                    ci.DrawLine(C - dir, C + dir, color, Math.Max(1.4f, line * 0.8f));
                }
                break;
            case UiGlyph.Search:
                ci.DrawArc(P(0.44f, 0.44f), Q(0.24f), 0, Mathf.Tau, 24, color, line, true);
                ci.DrawLine(P(0.62f, 0.62f), P(0.84f, 0.84f), color, line);
                break;
            case UiGlyph.Dot:
                ci.DrawCircle(C, Q(0.18f), color);
                break;
            case UiGlyph.Star:
            case UiGlyph.StarOutline:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float r = (i % 2 == 0 ? 0.46f : 0.2f) * s;
                        float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                        pts[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    if (glyph == UiGlyph.Star) ci.DrawColoredPolygon(pts, color);
                    else
                    {
                        var outline = new Vector2[11];
                        Array.Copy(pts, outline, pts.Length);
                        outline[^1] = pts[0];
                        ci.DrawPolyline(outline, color, line);
                    }
                    break;
                }
            default:
                if (glyph >= UiGlyph.EmblemSun) DrawEmblemShape(ci, glyph, o, s, color, line);
                break;
        }
    }

    /// <summary>A civilization disc: its colour with the white emblem on top and a dark outline.</summary>
    public static void Emblem(CanvasItem ci, int civ, Vector2 center, float radius)
    {
        ci.DrawCircle(center, radius + 1.0f, new Color(0, 0, 0, 0.55f));
        ci.DrawCircle(center, radius, UiTokens.Civ(civ));
        float inner = radius * 1.45f;
        var box = new Rect2(center - new Vector2(inner, inner) * 0.5f, new Vector2(inner, inner));
        Color ink = civ is 3 ? new Color("#1E1608") : Colors.White;
        DrawEmblemShape(ci, EmblemGlyph(civ), box.Position, inner, ink, Math.Max(1.2f, radius * 0.12f));
    }

    private static void DrawEmblemShape(CanvasItem ci, UiGlyph glyph, Vector2 o, float s, Color color, float line)
    {
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        Vector2 C = P(0.5f, 0.5f);
        switch (glyph)
        {
            case UiGlyph.EmblemSun:
                ci.DrawCircle(C, s * 0.2f, color);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(C + dir * s * 0.3f, C + dir * s * 0.46f, color, line);
                }
                break;
            case UiGlyph.EmblemWave:
                ci.DrawPolyline(Wave(P(0.1f, 0.38f), s * 0.8f, s * 0.12f), color, line);
                ci.DrawPolyline(Wave(P(0.1f, 0.64f), s * 0.8f, s * 0.12f), color, line);
                break;
            case UiGlyph.EmblemLeaf:
                ci.DrawColoredPolygon(new[] { P(0.16f, 0.84f), P(0.22f, 0.4f), P(0.5f, 0.14f), P(0.84f, 0.16f), P(0.78f, 0.52f), P(0.5f, 0.8f) }, color);
                ci.DrawLine(P(0.16f, 0.84f), P(0.62f, 0.38f), new Color(0, 0, 0, 0.5f), Math.Max(1.0f, line * 0.8f));
                break;
            case UiGlyph.EmblemStar:
                {
                    var pts = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float r = (i % 2 == 0 ? 0.46f : 0.19f) * s;
                        float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                        pts[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    ci.DrawColoredPolygon(pts, color);
                    break;
                }
            case UiGlyph.EmblemMountain:
                ci.DrawColoredPolygon(new[] { P(0.08f, 0.82f), P(0.34f, 0.3f), P(0.58f, 0.82f) }, color);
                ci.DrawColoredPolygon(new[] { P(0.42f, 0.82f), P(0.66f, 0.42f), P(0.92f, 0.82f) }, color);
                break;
            case UiGlyph.EmblemFlame:
                ci.DrawColoredPolygon(new[] { P(0.5f, 0.08f), P(0.74f, 0.42f), P(0.76f, 0.62f), P(0.62f, 0.84f), P(0.38f, 0.84f), P(0.24f, 0.62f), P(0.3f, 0.4f), P(0.44f, 0.5f) }, color);
                break;
            case UiGlyph.EmblemMoon:
                ci.DrawArc(C, s * 0.34f, Mathf.DegToRad(60), Mathf.DegToRad(300), 20, color, line * 2.2f, true);
                break;
            case UiGlyph.EmblemAntler:
                ci.DrawPolyline(new[] { P(0.5f, 0.9f), P(0.5f, 0.5f) }, color, line);
                ci.DrawPolyline(new[] { P(0.5f, 0.6f), P(0.26f, 0.4f), P(0.2f, 0.14f) }, color, line);
                ci.DrawPolyline(new[] { P(0.5f, 0.6f), P(0.74f, 0.4f), P(0.8f, 0.14f) }, color, line);
                ci.DrawPolyline(new[] { P(0.3f, 0.3f), P(0.14f, 0.34f) }, color, line);
                ci.DrawPolyline(new[] { P(0.7f, 0.3f), P(0.86f, 0.34f) }, color, line);
                break;
        }
    }

    private static Vector2[] Wave(Vector2 start, float width, float amp)
    {
        var pts = new Vector2[17];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = i / 16f;
            pts[i] = start + new Vector2(t * width, Mathf.Sin(t * Mathf.Tau * 1.5f) * amp);
        }
        return pts;
    }

    private static Vector2 Rot(Vector2 v, float angle)
    {
        float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        return new Vector2(v.X * c - v.Y * sn, v.X * sn + v.Y * c);
    }

    private static void DrawRotatedRect(CanvasItem ci, Vector2 center, Vector2 size, float angle, Color color)
    {
        Vector2 hx = Rot(new Vector2(size.X * 0.5f, 0), angle), hy = Rot(new Vector2(0, size.Y * 0.5f), angle);
        ci.DrawColoredPolygon(new[] { center - hx - hy, center + hx - hy, center + hx + hy, center - hx + hy }, color);
    }

    private static void DrawEllipse(CanvasItem ci, Vector2 center, float rx, float ry, Color color, float width)
    {
        var pts = new Vector2[25];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = i * Mathf.Tau / 24;
            pts[i] = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        ci.DrawPolyline(pts, color, width, true);
    }

    private static Vector2[] HalfDisc(Vector2 center, float radius)
    {
        var pts = new Vector2[14];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.Pi / 2 + i * Mathf.Pi / (pts.Length - 1);
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return pts;
    }

    public static Vector2[] Hexagon(Vector2 center, float radius, bool closed)
    {
        var pts = new Vector2[closed ? 7 : 6];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.DegToRad(60 * (i % 6) - 30);
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        return pts;
    }
}

/// <summary>A drawn glyph as a control. Redraws only when the glyph, tint or rotation actually changes.</summary>
public partial class UiIcon : Control
{
    private UiGlyph _glyph;
    private Color _tint = UiTokens.TextPrimary;
    private float _rotation;
    private float _stroke = 2.0f;

    public UiGlyph Glyph { get => _glyph; set { if (_glyph != value) { _glyph = value; QueueRedraw(); } } }
    public Color Tint { get => _tint; set { if (_tint != value) { _tint = value; QueueRedraw(); } } }
    public float Rotation2 { get => _rotation; set { if (!Mathf.IsEqualApprox(_rotation, value)) { _rotation = value; QueueRedraw(); } } }

    public UiIcon() { MouseFilter = MouseFilterEnum.Ignore; }

    public UiIcon(UiGlyph glyph, int size, Color tint) : this()
    {
        _glyph = glyph;
        _tint = tint;
        _stroke = size >= 26 ? 2.0f : 1.5f;
        CustomMinimumSize = new Vector2(size, size);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
    }

    public override void _Draw() => UiDraw.Glyph(this, _glyph, new Rect2(Vector2.Zero, Size), _tint, _stroke, _rotation);
}

/// <summary>A civilization emblem on its colour disc.</summary>
public partial class UiEmblem : Control
{
    private int _civ;

    public int Civ { get => _civ; set { if (_civ != value) { _civ = value; QueueRedraw(); } } }

    public UiEmblem(int civ, int size)
    {
        _civ = civ;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public override void _Draw() => UiDraw.Emblem(this, _civ, Size * 0.5f, Math.Min(Size.X, Size.Y) * 0.5f - 1);
}

/// <summary>Season glyph ringed by a progress arc for the part of the season that has passed.</summary>
public partial class UiSeasonDial : Control
{
    private int _season;
    private float _progress;

    public UiSeasonDial()
    {
        CustomMinimumSize = new Vector2(34, 34);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public void Set(int season, float progress)
    {
        progress = Mathf.Clamp(progress, 0, 1);
        if (season == _season && Mathf.Abs(progress - _progress) < 0.02f) return;
        _season = season;
        _progress = progress;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 c = Size * 0.5f;
        DrawArc(c, 16, 0, Mathf.Tau, 36, UiTokens.Border, 2.0f, true);
        if (_progress > 0.01f)
            DrawArc(c, 16, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _progress, 36, UiTokens.Accent, 2.0f, true);
        UiGlyph glyph = (UiGlyph)((int)UiGlyph.Spring + (_season & 3));
        UiDraw.Glyph(this, glyph, new Rect2(c - new Vector2(10, 10), new Vector2(20, 20)), UiTokens.TextPrimary, 1.5f);
    }
}

/// <summary>A numbered disc: the research slot rank (1 Primary, 2 Secondary, 3 Tertiary).</summary>
public partial class UiRankBadge : Control
{
    private int _rank;
    private Color _fill = new("#E2D3A6");

    public UiRankBadge(int rank)
    {
        _rank = rank;
        CustomMinimumSize = new Vector2(20, 20);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public void SetFill(Color fill)
    {
        if (_fill == fill) return;
        _fill = fill;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(Size * 0.5f, 10, _fill);
        Font font = UiTheme.NumBold;
        string digit = _rank.ToString();
        Vector2 extent = font.GetStringSize(digit, HorizontalAlignment.Left, -1, 14);
        DrawString(font, new Vector2((Size.X - extent.X) * 0.5f, Size.Y * 0.5f + 5), digit, HorizontalAlignment.Left, -1, 14, UiTokens.TextOnAccent);
    }
}

/// <summary>Vertical zoom ladder: four labelled stops on a log scale with a continuous marker. Clicking a stop zooms to that level.</summary>
public partial class UiZoomLadder : Control
{
    private float _t;
    private int _level;
    private readonly System.Action<int> _pick;
    private static readonly float[] StopT = { 0.16f, 0.39f, 0.71f, 0.92f };

    public UiZoomLadder(System.Action<int> pick, int width, int height)
    {
        _pick = pick;
        CustomMinimumSize = new Vector2(width, height);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Set(float t, int level)
    {
        t = Mathf.Clamp(t, 0, 1);
        if (Mathf.Abs(t - _t) < 0.004f && level == _level) return;
        _t = t;
        _level = level;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            float d = Mathf.Abs(click.Position.Y - StopY(i));
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        _pick(best);
        AcceptEvent();
    }

    private float StopY(int i) => 8 + StopT[i] * (Size.Y - 16);

    public override void _Draw()
    {
        float x = Size.X >= 70 ? 18 : Size.X * 0.5f;
        DrawLine(new Vector2(x, 6), new Vector2(x, Size.Y - 6), UiTokens.BorderStrong, 2.0f);
        for (int i = 0; i < 4; i++)
        {
            float y = StopY(i);
            bool on = i == _level;
            DrawCircle(new Vector2(x, y), on ? 4.0f : 3.0f, on ? UiTokens.Accent : UiTokens.TextTertiary);
            if (Size.X >= 70)
            {
                string text = $"Z{i}";
                DrawString(UiTheme.Medium, new Vector2(x + 12, y + 5), text, HorizontalAlignment.Left, -1, 14, on ? UiTokens.Accent : UiTokens.TextSecondary);
            }
        }
        float my = 8 + _t * (Size.Y - 16);
        DrawColoredPolygon(new[] { new Vector2(x - 9, my - 5), new Vector2(x - 9, my + 5), new Vector2(x - 2, my) }, UiTokens.Accent);
    }
}
