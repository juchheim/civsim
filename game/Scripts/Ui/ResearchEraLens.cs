using System;

namespace CivSim.Game.Ui;

/// <summary>
/// The research Map's "era lens" (pure: no Godot types, unit-tested from CivSim.Tests). One age is drawn at full size at a fixed radius;
/// every other age shrinks to a thin band, older ones packed inside it and later ones outside. It is a monotone, piecewise-linear remap of
/// the layout radius only, so angles, sectors, field rays and "every edge points outward" all survive.
/// <para>The focus <see cref="F"/> is continuous over ring indices (not age ids): 2.0 is the third ring at full size, 2.5 is halfway
/// between the third and the fourth. See docs/MVP/research-tree-era-focus-design.md §2.</para>
/// </summary>
public sealed class ResearchEraLens
{
    public const float FullLane = ResearchRadialLayout.LaneSpacing, DotLane = 7f;
    /// <summary>The focused ring's lanes are drawn this far apart: roomier than the layout's own, so the era in view fills more of the screen.</summary>
    public const float FocusLane = 56f;
    public const float FullGap = ResearchRadialLayout.RingGap, DotGap = 14f;
    /// <summary>The focused ring's inner edge is never nearer the centre than this, and never so near that lane-0 neighbours are closer than <see cref="MinLaneArc"/>.</summary>
    public const float MinFocusInner = 480f, MinLaneArc = 44f;
    public const float HubMin = 200f;

    public int RingCount { get; }
    /// <summary>Per ring index: the age it draws.</summary>
    public int[] RingEra { get; }
    public int[] RingLanes { get; }
    /// <summary>Per ring index: the layout's own bounds of the ring.</summary>
    public float[] BaseInner { get; }
    public float[] BaseOuter { get; }
    public float FocusInner { get; }

    public float F { get; private set; } = -1;
    public float Hub { get; private set; }
    /// <summary>Per ring index, for the current focus.</summary>
    public float[] Weights { get; }
    public float[] LaneSpacing { get; }
    public float[] RingStart { get; }
    public float[] RingEnd { get; }
    /// <summary>Where the gap after the ring ends (the next ring's start).</summary>
    public float[] GapEnd { get; }
    public float OuterRadius => RingCount == 0 ? ResearchRadialLayout.CenterRadius : GapEnd[RingCount - 1];

    private readonly ResearchRadialLayout? _layout;
    private readonly int[] _ringOfEra;
    // knots of the piecewise-linear map: base radius -> drawn radius
    private readonly float[] _kb, _kd;
    // edges in polar form, converted once
    private readonly float[][] _edgeRadius = Array.Empty<float[]>();
    private readonly float[][] _edgeCos = Array.Empty<float[]>(), _edgeSin = Array.Empty<float[]>();

    public ResearchEraLens(ResearchRadialLayout layout)
        : this(RingLanesOf(layout), layout.SlotAngle, layout.Rings)
    {
        _layout = layout;
        int edges = layout.Edges.Length;
        _edgeRadius = new float[edges][];
        _edgeCos = new float[edges][];
        _edgeSin = new float[edges][];
        for (int e = 0; e < edges; e++)
        {
            float[] pts = layout.Edges[e].Points;
            int count = pts.Length / 2;
            var r = new float[count];
            var c = new float[count];
            var s = new float[count];
            for (int i = 0; i < count; i++)
            {
                double x = pts[i * 2], y = pts[i * 2 + 1];
                double a = Math.Atan2(y, x);
                r[i] = (float)Math.Sqrt(x * x + y * y);
                c[i] = (float)Math.Cos(a);
                s[i] = (float)Math.Sin(a);
            }
            _edgeRadius[e] = r;
            _edgeCos[e] = c;
            _edgeSin[e] = s;
        }
    }

    /// <summary>A lens over synthetic rings (tests): lanes per ring, placed the way the layout places them.</summary>
    public ResearchEraLens(int[] lanesPerRing, float slotAngle) : this(lanesPerRing, slotAngle, null) { }

    private ResearchEraLens(int[] lanesPerRing, float slotAngle, int[]? eras)
    {
        RingCount = lanesPerRing.Length;
        RingLanes = (int[])lanesPerRing.Clone();
        RingEra = eras is null ? new int[RingCount] : (int[])eras.Clone();
        if (eras is null) for (int i = 0; i < RingCount; i++) RingEra[i] = i;
        int maxEra = 0;
        foreach (int era in RingEra) maxEra = Math.Max(maxEra, era);
        _ringOfEra = new int[Math.Max(maxEra + 1, 10)];
        Array.Fill(_ringOfEra, -1);
        for (int i = 0; i < RingCount; i++) _ringOfEra[RingEra[i]] = i;

        BaseInner = new float[RingCount];
        BaseOuter = new float[RingCount];
        float inner = ResearchRadialLayout.CenterRadius;
        for (int i = 0; i < RingCount; i++)
        {
            BaseInner[i] = inner;
            BaseOuter[i] = inner + RingLanes[i] * FullLane;
            inner = BaseOuter[i] + FullGap;
        }
        FocusInner = Math.Max(MinFocusInner, slotAngle > 1e-6f ? MinLaneArc / slotAngle : MinFocusInner);

        Weights = new float[RingCount];
        LaneSpacing = new float[RingCount];
        RingStart = new float[RingCount];
        RingEnd = new float[RingCount];
        GapEnd = new float[RingCount];
        _kb = new float[2 + 2 * RingCount];
        _kd = new float[_kb.Length];
        _kb[0] = 0;
        _kb[1] = ResearchRadialLayout.CenterRadius;
        for (int i = 0; i < RingCount; i++)
        {
            _kb[2 + 2 * i] = BaseOuter[i];
            _kb[3 + 2 * i] = BaseOuter[i] + FullGap;
        }
        SetFocus(0);
    }

    private static int[] RingLanesOf(ResearchRadialLayout layout)
    {
        var lanes = new int[layout.Rings.Length];
        for (int i = 0; i < lanes.Length; i++) lanes[i] = Math.Max(1, layout.Lanes[layout.Rings[i]]);
        return lanes;
    }

    /// <summary>The largest focus: the last ring.</summary>
    public float MaxFocus => Math.Max(0, RingCount - 1);

    /// <summary>The ring index of an age, or -1.</summary>
    public int RingOf(int era) => (uint)era < (uint)_ringOfEra.Length ? _ringOfEra[era] : -1;

    public static float Smoothstep(float x) => x * x * (3 - 2 * x);

    /// <summary>How open a ring is at focus <paramref name="f"/>: 1 when focused, 0 one ring or more away.</summary>
    public static float WeightAt(int ring, float f) => Smoothstep(Math.Clamp(1 - Math.Abs(ring - f), 0, 1));

    /// <summary>How open an age's ring is now: 1 focused, 0 collapsed (also 0 for an age without a ring).</summary>
    public float Weight(int era)
    {
        int ring = RingOf(era);
        return ring < 0 ? 0 : Weights[ring];
    }

    /// <summary>The age nearest the focus.</summary>
    public int FocusedEra => RingCount == 0 ? 0 : RingEra[Math.Clamp((int)MathF.Round(F), 0, RingCount - 1)];

    public void SetFocus(float f)
    {
        f = Math.Clamp(f, 0, MaxFocus);
        if (f == F) return;
        F = f;
        if (RingCount == 0) { Hub = ResearchRadialLayout.CenterRadius; FillKnots(); return; }
        for (int i = 0; i < RingCount; i++)
        {
            Weights[i] = WeightAt(i, f);
            LaneSpacing[i] = Lerp(DotLane, FocusLane, Weights[i]);
        }
        var block = new float[RingCount];
        for (int i = 0; i < RingCount; i++)
        {
            float next = i + 1 < RingCount ? Weights[i + 1] : 0;
            float gap = Lerp(DotGap, FullGap, Math.Max(Weights[i], next));
            block[i] = RingLanes[i] * LaneSpacing[i] + gap;
        }
        int i0 = Math.Min((int)MathF.Floor(f), RingCount - 1);
        float t = f - i0;
        float before = 0;
        for (int k = 0; k < i0; k++) before += block[k];
        Hub = Math.Max(HubMin, FocusInner - before - t * block[i0]);
        float at = Hub;
        for (int i = 0; i < RingCount; i++)
        {
            RingStart[i] = at;
            RingEnd[i] = at + RingLanes[i] * LaneSpacing[i];
            GapEnd[i] = at + block[i];
            at = GapEnd[i];
        }
        FillKnots();
    }

    private void FillKnots()
    {
        _kd[0] = 0;
        _kd[1] = Hub;
        for (int i = 0; i < RingCount; i++)
        {
            _kd[2 + 2 * i] = RingEnd[i];
            _kd[3 + 2 * i] = GapEnd[i];
        }
    }

    /// <summary>The drawn radius of a layout radius.</summary>
    public float Map(float rBase) => Remap(rBase, _kb, _kd);

    /// <summary>The layout radius of a drawn radius.</summary>
    public float Unmap(float r) => Remap(r, _kd, _kb);

    private static float Remap(float r, float[] from, float[] to)
    {
        if (r <= 0) return r * (to[1] / Math.Max(1e-3f, from[1]));
        int last = from.Length - 1;
        if (r >= from[last]) return to[last] + (r - from[last]);
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (from[mid] <= r) lo = mid; else hi = mid;
        }
        float span = from[hi] - from[lo];
        return span <= 1e-6f ? to[lo] : to[lo] + (to[hi] - to[lo]) * (r - from[lo]) / span;
    }

    /// <summary>The drawn bounds of an age's ring, or zeros when it has none.</summary>
    public float Inner(int era) { int ring = RingOf(era); return ring < 0 ? 0 : RingStart[ring]; }
    public float Outer(int era) { int ring = RingOf(era); return ring < 0 ? 0 : RingEnd[ring]; }
    public float RingMid(int era) { int ring = RingOf(era); return ring < 0 ? 0 : (RingStart[ring] + RingEnd[ring]) * 0.5f; }

    /// <summary>The ring index whose band (the ring and half of each gap beside it) holds drawn radius <paramref name="r"/>, or -1.</summary>
    public int RingAt(float r)
    {
        for (int i = 0; i < RingCount; i++)
        {
            float lo = i == 0 ? RingStart[0] - DotGap * 0.5f : (GapEnd[i - 1] + RingEnd[i - 1]) * 0.5f;
            float hi = (RingEnd[i] + GapEnd[i]) * 0.5f;
            if (r >= lo && r < hi) return i;
        }
        return -1;
    }

    /// <summary>Drawn positions of every project (layout lenses only).</summary>
    public void Positions(float[] x, float[] y)
    {
        ResearchRadialLayout layout = _layout ?? throw new InvalidOperationException("a synthetic lens has no projects");
        for (int p = 0; p < layout.Count; p++)
        {
            if (!layout.Placed[p]) { x[p] = layout.X[p]; y[p] = layout.Y[p]; continue; }
            float r = Map(layout.Radius[p]);
            x[p] = MathF.Cos(layout.Angle[p]) * r;
            y[p] = MathF.Sin(layout.Angle[p]) * r;
        }
    }

    /// <summary>The drawn polyline of edge <paramref name="e"/> as x, y pairs (same length as the layout's).</summary>
    public void EdgePoints(int e, float[] xy)
    {
        float[] r = _edgeRadius[e], c = _edgeCos[e], s = _edgeSin[e];
        for (int i = 0; i < r.Length; i++)
        {
            float d = Map(r[i]);
            xy[i * 2] = c[i] * d;
            xy[i * 2 + 1] = s[i] * d;
        }
    }

    /// <summary>The base radius of each point of edge <paramref name="e"/>.</summary>
    public float[] EdgeBaseRadius(int e) => _edgeRadius[e];

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
