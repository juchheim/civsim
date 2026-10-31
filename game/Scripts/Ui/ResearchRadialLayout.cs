using System;
using System.Collections.Generic;
using CivSim.Sim.Research;

namespace CivSim.Game.Ui;

/// <summary>
/// Geometry of the research Map (pure: no Godot types, unit-tested from CivSim.Tests).
/// <para>Rings are ages, Dawn innermost. Sectors are tracks, clockwise from the top: Craft, Kin, Reach. Inside a sector each
/// field (lineage) owns a wedge that runs through every ring, so a field's projects line up along one ray. Inside an age ring a
/// project sits on lane = the length of its longest chain of same-age prerequisites, so every prerequisite edge points outward.
/// Turning points sit in the gap after the ring of the age they leave, at the mean angle of their prerequisites.</para>
/// <para>World units: the centre is (0, 0), +y points down (screen convention), angle 0 points right and grows clockwise.</para>
/// </summary>
public sealed class ResearchRadialLayout
{
    public const float CenterRadius = 230f, LaneSpacing = 44f, RingGap = 52f;
    /// <summary>Angular padding per wedge, the gap between sectors and the empty wedge at 12 o'clock that holds the age names, in slots.</summary>
    public const float WedgePad = 0.6f, SectorGap = 1.4f, LabelGap = 3.4f;

    public readonly record struct Sector(int Theme, float Start, float End);
    public readonly record struct Wedge(int Lineage, int Theme, float Start, float End);
    /// <summary>A prerequisite edge from <see cref="From"/> to the project that needs it. <see cref="Points"/> holds x, y pairs.</summary>
    public readonly record struct Edge(int From, int To, bool Any, float[] Points);

    public int Count { get; }
    public bool[] Placed { get; }
    public bool[] Gate { get; }
    public float[] X { get; }
    public float[] Y { get; }
    public float[] Angle { get; }
    public float[] Radius { get; }
    public int[] Era { get; }
    public int[] Lane { get; }
    /// <summary>Ages with a ring, ascending.</summary>
    public int[] Rings { get; }
    /// <summary>Per age (index = era): ring bounds and the radius of the gate circle after it; zero when the age has no ring.</summary>
    public float[] RingInner { get; }
    public float[] RingOuter { get; }
    public float[] GateRadius { get; }
    public int[] Lanes { get; }
    public Sector[] Sectors { get; }
    public Wedge[] Wedges { get; }
    public Edge[] Edges { get; }
    /// <summary>Placed prerequisites (all-of then any-of) and placed dependants of every project.</summary>
    public int[][] Prerequisites { get; }
    public int[][] Dependents { get; }
    public float SlotAngle { get; }
    public float OuterRadius { get; }

    private readonly bool[] _regular;

    public static ResearchRadialLayout Build(ResearchCatalog catalog) => new(catalog);

    private ResearchRadialLayout(ResearchCatalog catalog)
    {
        int n = catalog.Count;
        Count = n;
        Placed = new bool[n];
        Gate = new bool[n];
        _regular = new bool[n];
        X = new float[n];
        Y = new float[n];
        Angle = new float[n];
        Radius = new float[n];
        Era = new int[n];
        Lane = new int[n];
        RingInner = new float[ResearchCatalog.EraCount];
        RingOuter = new float[ResearchCatalog.EraCount];
        GateRadius = new float[ResearchCatalog.EraCount];
        Lanes = new int[ResearchCatalog.EraCount];

        for (int p = 0; p < n; p++)
        {
            ProjectDef def = catalog.Projects[p];
            Era[p] = Math.Clamp(def.Era, 0, ResearchCatalog.EraCount - 1);
            if (!def.Numeric) continue;
            if (def.IsKeystone) { Placed[p] = true; Gate[p] = true; }
            else if (def.Lineage >= 0 && (int)def.Theme < 3) { Placed[p] = true; _regular[p] = true; }
        }

        // prerequisite and dependant lists among placed projects
        var dependents = new List<int>[n];
        Prerequisites = new int[n][];
        var anyFlags = new bool[n][];
        for (int p = 0; p < n; p++)
        {
            ProjectDef def = catalog.Projects[p];
            var list = new List<int>();
            var any = new List<bool>();
            if (Placed[p])
            {
                foreach (int r in def.Requires) if ((uint)r < (uint)n && Placed[r] && !list.Contains(r)) { list.Add(r); any.Add(false); }
                foreach (int r in def.RequiresAny) if ((uint)r < (uint)n && Placed[r] && !list.Contains(r)) { list.Add(r); any.Add(true); }
                foreach (int r in list) (dependents[r] ??= new List<int>()).Add(p);
            }
            Prerequisites[p] = list.ToArray();
            anyFlags[p] = any.ToArray();
        }
        Dependents = new int[n][];
        for (int p = 0; p < n; p++) Dependents[p] = dependents[p]?.ToArray() ?? Array.Empty<int>();

        // lanes: depth among same-age prerequisites
        var state = new byte[n];
        for (int p = 0; p < n; p++) if (_regular[p]) Depth(p, state);
        var ringList = new List<int>();
        for (int p = 0; p < n; p++)
        {
            if (!_regular[p]) continue;
            Lanes[Era[p]] = Math.Max(Lanes[Era[p]], Lane[p] + 1);
            if (!ringList.Contains(Era[p])) ringList.Add(Era[p]);
        }
        ringList.Sort();
        Rings = ringList.ToArray();

        // radii
        float inner = CenterRadius;
        foreach (int era in Rings)
        {
            RingInner[era] = inner;
            RingOuter[era] = inner + Lanes[era] * LaneSpacing;
            GateRadius[era] = RingOuter[era] + RingGap * 0.5f;
            inner = RingOuter[era] + RingGap;
        }
        OuterRadius = inner;

        // fields per track, sized by their widest (age, lane) group
        int lineageCount = catalog.LineageCodes.Count;
        var weight = new int[lineageCount];
        var lineageTheme = new int[lineageCount];
        var groupCount = new Dictionary<(int Lineage, int Era, int Lane), int>();
        for (int p = 0; p < n; p++)
        {
            if (!_regular[p]) continue;
            ProjectDef def = catalog.Projects[p];
            var key = (def.Lineage, Era[p], Lane[p]);
            groupCount[key] = groupCount.TryGetValue(key, out int c) ? c + 1 : 1;
            weight[def.Lineage] = Math.Max(weight[def.Lineage], groupCount[key]);
            lineageTheme[def.Lineage] = (int)def.Theme;
        }
        var order = new List<int>[3];
        for (int t = 0; t < 3; t++)
        {
            order[t] = new List<int>();
            for (int l = 0; l < lineageCount; l++) if (weight[l] > 0 && lineageTheme[l] == t) order[t].Add(l);
            order[t].Sort((a, b) => string.CompareOrdinal(catalog.LineageCodes[a], catalog.LineageCodes[b]));
        }

        float totalSlots = 2 * SectorGap + LabelGap;
        for (int l = 0; l < lineageCount; l++) if (weight[l] > 0) totalSlots += weight[l] + WedgePad;
        SlotAngle = (float)(2 * Math.PI / totalSlots);

        var lineageOf = new int[n];
        for (int p = 0; p < n; p++) lineageOf[p] = _regular[p] ? catalog.Projects[p].Lineage : -1;
        var center = new double[lineageCount];
        OrderFields(order, weight, lineageOf, center);

        // wedges and sectors from the chosen order
        var wedges = new List<Wedge>();
        var sectors = new List<Sector>();
        var wedgeStart = new double[lineageCount];
        double theta = -Math.PI / 2 + LabelGap * 0.5 * SlotAngle;
        for (int t = 0; t < 3; t++)
        {
            double sectorStart = theta;
            foreach (int l in order[t])
            {
                wedgeStart[l] = theta;
                double end = theta + (weight[l] + WedgePad) * SlotAngle;
                wedges.Add(new Wedge(l, t, (float)theta, (float)end));
                theta = end;
            }
            sectors.Add(new Sector(t, (float)sectorStart, (float)theta));
            if (t < 2) theta += SectorGap * SlotAngle;
        }
        Wedges = wedges.ToArray();
        Sectors = sectors.ToArray();

        // projects: age by age, lane by lane, so prerequisites are placed before the projects that need them
        var placedYet = new bool[n];
        var groups = new SortedDictionary<(int Era, int Lane, int Lineage), List<int>>();
        for (int p = 0; p < n; p++)
        {
            if (!_regular[p]) continue;
            var key = (Era[p], Lane[p], lineageOf[p]);
            if (!groups.TryGetValue(key, out List<int>? list)) groups[key] = list = new List<int>();
            list.Add(p);
        }
        foreach (((int era, int lane, int lineage), List<int> members) in groups)
        {
            double mid = wedgeStart[lineage] + (weight[lineage] + WedgePad) * 0.5 * SlotAngle;
            var bary = new double[members.Count];
            for (int i = 0; i < members.Count; i++) bary[i] = Barycentre(members[i], placedYet, mid);
            int[] idx = new int[members.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => bary[a] != bary[b] ? bary[a].CompareTo(bary[b]) : members[a].CompareTo(members[b]));
            int w = weight[lineage];
            double First(int slot) => wedgeStart[lineage] + (WedgePad * 0.5 + slot + 0.5) * SlotAngle;
            int[] slots = AssignSlots(idx, bary, w, First);
            float radius = RingInner[era] + (lane + 0.5f) * LaneSpacing;
            for (int i = 0; i < idx.Length; i++)
            {
                int p = members[idx[i]];
                SetPolar(p, First(slots[i]), radius);
                placedYet[p] = true;
            }
        }

        // turning points: in the gap after their age's ring, at the mean angle of their prerequisites
        for (int p = 0; p < n; p++)
        {
            if (!Gate[p]) continue;
            double sx = 0, sy = 0;
            foreach (int r in Prerequisites[p]) if (_regular[r]) { sx += Math.Cos(Angle[r]); sy += Math.Sin(Angle[r]); }
            double angle = sx * sx + sy * sy > 1e-6 ? Math.Atan2(sy, sx) : -Math.PI / 2;
            float radius = GateRadius[Era[p]] > 0 ? GateRadius[Era[p]] : OuterRadius - RingGap * 0.5f;
            SetPolar(p, angle, radius);
        }

        // edges
        var edges = new List<Edge>();
        for (int p = 0; p < n; p++)
            for (int i = 0; i < Prerequisites[p].Length; i++)
            {
                int r = Prerequisites[p][i];
                edges.Add(new Edge(r, p, anyFlags[p][i], Curve(r, p)));
            }
        Edges = edges.ToArray();
    }

    private int Depth(int p, byte[] state)
    {
        if (state[p] == 2) return Lane[p];
        if (state[p] == 1) return 0; // a cycle in content; validation forbids it, never loop here
        state[p] = 1;
        int depth = 0;
        foreach (int r in Prerequisites[p])
            if (_regular[r] && Era[r] == Era[p]) depth = Math.Max(depth, Depth(r, state) + 1);
        Lane[p] = depth;
        state[p] = 2;
        return depth;
    }

    private void SetPolar(int p, double angle, float radius)
    {
        angle = Wrap(angle);
        Angle[p] = (float)angle;
        Radius[p] = radius;
        X[p] = (float)(Math.Cos(angle) * radius);
        Y[p] = (float)(Math.Sin(angle) * radius);
    }

    private double Barycentre(int p, bool[] placedYet, double fallback)
    {
        double sx = 0, sy = 0;
        int count = 0;
        foreach (int r in Prerequisites[p])
        {
            if (!placedYet[r]) continue;
            sx += Math.Cos(Angle[r]);
            sy += Math.Sin(Angle[r]);
            count++;
        }
        if (count == 0 || sx * sx + sy * sy < 1e-9) return fallback;
        return Unwrap(Math.Atan2(sy, sx), fallback);
    }

    /// <summary>Chooses increasing slots for nodes sorted by barycentre, minimising the total distance to their barycentres.</summary>
    private static int[] AssignSlots(int[] idx, double[] bary, int width, Func<int, double> slotAngle)
    {
        int k = idx.Length;
        var cost = new double[k + 1, width + 1];
        var take = new bool[k + 1, width + 1];
        for (int i = 1; i <= k; i++) cost[i, 0] = double.MaxValue / 4;
        for (int j = 1; j <= width; j++)
            for (int i = 1; i <= k; i++)
            {
                double skip = cost[i, j - 1];
                double use = cost[i - 1, j - 1] + Math.Abs(slotAngle(j - 1) - bary[idx[i - 1]]);
                if (i > j) { cost[i, j] = double.MaxValue / 4; continue; }
                if (use <= skip) { cost[i, j] = use; take[i, j] = true; }
                else cost[i, j] = skip;
            }
        var slots = new int[k];
        for (int i = k, j = width; i > 0; j--)
            if (take[i, j]) { slots[i - 1] = j - 1; i--; }
        return slots;
    }

    /// <summary>Orders fields inside each track so cross-field edges stay short: barycentre sweeps, then adjacent swaps.</summary>
    private void OrderFields(List<int>[] order, int[] weight, int[] lineageOf, double[] center)
    {
        var pairs = new List<(int A, int B)>();
        for (int p = 0; p < Count; p++)
            foreach (int r in Prerequisites[p])
                if (lineageOf[p] >= 0 && lineageOf[r] >= 0 && lineageOf[p] != lineageOf[r]) pairs.Add((lineageOf[r], lineageOf[p]));

        double Cost()
        {
            Centres(order, weight, center);
            double total = 0;
            foreach ((int a, int b) in pairs) total += Math.Abs(Unwrap(center[a], center[b]) - center[b]);
            return total;
        }

        double best = Cost();
        for (int sweep = 0; sweep < 8; sweep++)
        {
            bool improved = false;
            for (int t = 0; t < 3; t++)
            {
                var bary = new Dictionary<int, double>();
                foreach (int l in order[t])
                {
                    double sx = 0, sy = 0;
                    foreach ((int a, int b) in pairs)
                    {
                        int other = a == l ? b : b == l ? a : -1;
                        if (other < 0) continue;
                        sx += Math.Cos(center[other]);
                        sy += Math.Sin(center[other]);
                    }
                    bary[l] = sx * sx + sy * sy > 1e-9 ? Unwrap(Math.Atan2(sy, sx), center[l]) : center[l];
                }
                var trial = new List<int>(order[t]);
                trial.Sort((a, b) => bary[a] != bary[b] ? bary[a].CompareTo(bary[b]) : a.CompareTo(b));
                List<int> previous = order[t];
                order[t] = trial;
                double cost = Cost();
                if (cost + 1e-9 < best) { best = cost; improved = true; }
                else order[t] = previous;

                for (int i = 0; i + 1 < order[t].Count; i++)
                {
                    (order[t][i], order[t][i + 1]) = (order[t][i + 1], order[t][i]);
                    cost = Cost();
                    if (cost + 1e-9 < best) { best = cost; improved = true; }
                    else (order[t][i], order[t][i + 1]) = (order[t][i + 1], order[t][i]);
                }
            }
            if (!improved) break;
        }
        Centres(order, weight, center);
    }

    private void Centres(List<int>[] order, int[] weight, double[] center)
    {
        double theta = -Math.PI / 2 + LabelGap * 0.5 * SlotAngle;
        for (int t = 0; t < 3; t++)
        {
            foreach (int l in order[t])
            {
                double width = (weight[l] + WedgePad) * SlotAngle;
                center[l] = theta + width * 0.5;
                theta += width;
            }
            theta += SectorGap * SlotAngle;
        }
    }

    /// <summary>A curve that leaves the prerequisite outward, turns around the centre in the middle and arrives outward.</summary>
    private float[] Curve(int from, int to)
    {
        double a0 = Angle[from], a1 = Unwrap(Angle[to], a0);
        double r0 = Radius[from], r1 = Radius[to];
        double sweep = a1 - a0;
        double length = Math.Abs(r1 - r0) + Math.Abs(sweep) * (r0 + r1) * 0.5;
        int segments = Math.Clamp((int)(length / 16) + 4, 6, 40);
        // A straight radial edge that skips a lane would run through the project between; bow it sideways.
        double bow = Math.Abs(sweep) < SlotAngle * 0.5 && r1 - r0 > LaneSpacing * 1.5 ? SlotAngle * 0.45 * (((from + to) & 1) == 0 ? 1 : -1) : 0;
        var points = new float[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++)
        {
            double t = i / (double)segments;
            double ease = t * t * (3 - 2 * t);
            double angle = a0 + sweep * ease + bow * Math.Sin(Math.PI * t);
            double radius = r0 + (r1 - r0) * t;
            points[i * 2] = (float)(Math.Cos(angle) * radius);
            points[i * 2 + 1] = (float)(Math.Sin(angle) * radius);
        }
        return points;
    }

    public static double Wrap(double angle)
    {
        while (angle <= -Math.PI) angle += 2 * Math.PI;
        while (angle > Math.PI) angle -= 2 * Math.PI;
        return angle;
    }

    /// <summary>The value equal to <paramref name="angle"/> modulo 2π that is closest to <paramref name="near"/>.</summary>
    public static double Unwrap(double angle, double near)
    {
        double d = Wrap(angle - near);
        return near + d;
    }
}
