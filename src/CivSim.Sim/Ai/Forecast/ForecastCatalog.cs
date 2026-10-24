using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>
/// The research catalog flattened into the arrays the forecast reads in its inner loop (docs/MVP/14 §3.2): prerequisites
/// and dependents, Legacy per project, and the numeric effects that move Insight or Know-how. Built once per catalog and
/// never changed, so it is shared by every rollout.
/// </summary>
public sealed class ForecastCatalog
{
    public ResearchCatalog Catalog { get; }
    public int Count { get; }
    /// <summary>Numeric projects in index order: the only ones research can complete.</summary>
    public int[] Numeric { get; }
    public bool[] IsNumeric { get; }
    public int[] Era { get; }
    public int[] Lineage { get; }
    public Theme[] Theme { get; }
    public bool[] IsKeystone { get; }
    public int[] BreadthEra { get; }
    public int[] BreadthMin { get; }
    public int[] Enters { get; }
    public int[][] Requires { get; }
    public int[][] RequiresAny { get; }
    /// <summary>Projects that list this one in <see cref="Requires"/>.</summary>
    public int[][] Dependents { get; }
    /// <summary>Projects that list this one among their one-of prerequisites.</summary>
    public int[][] AnyDependents { get; }
    /// <summary>Keystones whose breadth counts this era, by era.</summary>
    public int[] KeystoneByBreadthEra { get; }
    /// <summary>Numeric projects by era, for the era gate.</summary>
    public int[][] ByEra { get; }
    /// <summary>Mastery Legacy for completing the project: 2 × (era + 1) (docs/MVP/22 §3.3).</summary>
    public int[] Mastery { get; }
    /// <summary>Firsts Legacy if this civilization is the first in the world to complete it.</summary>
    public Fix64[] FirstPoints { get; }
    /// <summary>Commons Insight percent mods (as a fraction, summed) of each project.</summary>
    public Fix64[] CommonsPct { get; }
    /// <summary>Elder Insight multiplier of each project (1 when none).</summary>
    public Fix64[] ElderMult { get; }
    /// <summary>The Insight-producing role a project unlocks, -1 if none.</summary>
    public int[] RoleOf { get; }
    /// <summary>Know-how gain on every lineage (1.2 for +20%), 1 when none.</summary>
    public Fix64[] KnowHowGainAll { get; }
    /// <summary>Know-how gain on one lineage: the lineage (-1 if none) and its multiplier.</summary>
    public int[] KnowHowGainLineage { get; }
    public Fix64[] KnowHowGainLineageMult { get; }
    /// <summary>Number of projects (transitively, numeric) that need this one: the hub weight of 03 §4.3.</summary>
    public int[] HubWeight { get; }
    public int Soc03 { get; }
    /// <summary>
    /// Standing practice a completed project starts (talk, ritual, teaching, counting: the living economy's ambient practice
    /// of docs/MVP/07c §7a, work-seasons per adult a season), and the Know-how lineages it feeds, as a share of one adult's
    /// work. Zero work when the project starts none.
    /// </summary>
    public Fix64[] AmbientWork { get; }
    public Fix64[]?[] AmbientLineageWork { get; }
    /// <summary>Ambient work every civilization has from the start (socialize 0.2, scout 0.1) plus a job's work (about 0.7).</summary>
    public Fix64 BaseWork { get; } = Fix64.One;
    /// <summary>Projects that widen what the civilization sees (WAY-01 trails, INQ-03 ore lore).</summary>
    public bool[] Reveals { get; }
    public int ElderRole { get; }
    public int ShamanRole { get; }
    public Fix64[] RoleInsight { get; }
    public int LineageCount { get; }

    // Constants of the Insight formula (docs/MVP/02 §8.1) and the cost formula (§8.3).
    public Fix64 CommonsCoef { get; }
    public Fix64 CommonsExp { get; }
    public Fix64 ElderCommons { get; }
    public Fix64 LowTies { get; }
    public Fix64 Calibration { get; }
    public Fix64 EraLag { get; }
    public Fix64 EraBoon { get; }
    public Fix64 KnowHowCostShare { get; } = Fix64.Half;
    public int[] SlotWeight { get; } = new int[CivResearch.SlotCount];

    public ForecastCatalog(ResearchCatalog catalog)
    {
        Catalog = catalog;
        Count = catalog.Count;
        LineageCount = catalog.LineageCodes.Count;
        IsNumeric = new bool[Count];
        Era = new int[Count];
        Lineage = new int[Count];
        Theme = new Theme[Count];
        IsKeystone = new bool[Count];
        BreadthEra = new int[Count];
        BreadthMin = new int[Count];
        Enters = new int[Count];
        Requires = new int[Count][];
        RequiresAny = new int[Count][];
        Mastery = new int[Count];
        FirstPoints = new Fix64[Count];
        CommonsPct = new Fix64[Count];
        ElderMult = new Fix64[Count];
        RoleOf = new int[Count];
        KnowHowGainAll = new Fix64[Count];
        KnowHowGainLineage = new int[Count];
        KnowHowGainLineageMult = new Fix64[Count];
        KeystoneByBreadthEra = new int[ResearchCatalog.EraCount];
        Array.Fill(KeystoneByBreadthEra, -1);
        Soc03 = catalog.IndexOf("SOC-03");
        AmbientWork = new Fix64[Count];
        AmbientLineageWork = new Fix64[]?[Count];
        foreach ((string practice, string project, int hundredths) in Ambient)
        {
            int p = catalog.IndexOf(project), a = catalog.PracticeIndex(practice);
            if (p < 0 || a < 0) continue;
            Fix64 work = Fix64.FromRatio(hundredths, 100);
            AmbientWork[p] = work;
            var share = new Fix64[catalog.LineageCodes.Count];
            foreach ((int lineage, int percent) in catalog.PracticeSplit(a, 0)) share[lineage] += work * percent / 100;
            AmbientLineageWork[p] = share;
        }
        Reveals = new bool[Count];
        foreach (string id in new[] { "WAY-01", "INQ-03" }) if (catalog.IndexOf(id) is int r and >= 0) Reveals[r] = true;
        ElderRole = catalog.RoleIndex("elder");
        ShamanRole = catalog.RoleIndex("shaman");
        RoleInsight = new Fix64[catalog.Roles.Count];
        for (int r = 0; r < catalog.Roles.Count; r++) RoleInsight[r] = catalog.Roles[r].Insight;

        var numeric = new List<int>();
        var dependents = new List<int>[Count];
        var anyDependents = new List<int>[Count];
        var byEra = new List<int>[ResearchCatalog.EraCount];
        for (int e = 0; e < byEra.Length; e++) byEra[e] = new List<int>();
        for (int p = 0; p < Count; p++) { dependents[p] = new List<int>(); anyDependents[p] = new List<int>(); }
        for (int p = 0; p < Count; p++)
        {
            ProjectDef def = catalog.Projects[p];
            IsNumeric[p] = def.Numeric;
            Era[p] = def.Era;
            Lineage[p] = def.Lineage;
            Theme[p] = def.Theme;
            IsKeystone[p] = def.IsKeystone;
            BreadthEra[p] = def.BreadthEra;
            BreadthMin[p] = def.BreadthMin;
            Enters[p] = def.Enters;
            Requires[p] = def.Requires;
            RequiresAny[p] = def.RequiresAny;
            Mastery[p] = catalog.MasteryUnits(def).FloorToInt();
            FirstPoints[p] = catalog.FirstUnits(def);
            ElderMult[p] = Fix64.One;
            RoleOf[p] = -1;
            KnowHowGainAll[p] = Fix64.One;
            KnowHowGainLineage[p] = -1;
            KnowHowGainLineageMult[p] = Fix64.One;
            if (!def.Numeric) continue;
            numeric.Add(p);
            byEra[def.Era].Add(p);
            if (def.IsKeystone && def.BreadthEra >= 0) KeystoneByBreadthEra[def.BreadthEra] = p;
            foreach (int r in def.Requires) dependents[r].Add(p);
            foreach (int r in def.RequiresAny) anyDependents[r].Add(p);
            foreach (EffectDef e in def.Effects)
            {
                switch (e.Kind)
                {
                    case EffectKind.Mod when e.HasValue && e.Stat == "commons_insight" && e.Op == "pct":
                        CommonsPct[p] += e.Value;
                        break;
                    case EffectKind.Mod when e.HasValue && e.Stat == "elder_insight" && e.Op == "mult":
                        ElderMult[p] = ElderMult[p] * e.Value;
                        break;
                    case EffectKind.KnowHow when e.Text is not null:
                        ParseKnowHow(catalog, e.Text, out int lineage, out Fix64 gain);
                        if (lineage < 0) KnowHowGainAll[p] = KnowHowGainAll[p] * gain;
                        else { KnowHowGainLineage[p] = lineage; KnowHowGainLineageMult[p] = gain; }
                        break;
                }
            }
            for (int r = 0; r < catalog.Roles.Count; r++)
                if (catalog.Roles[r].UnlockedBy == p && catalog.Roles[r].Insight.Raw > 0) RoleOf[p] = r;
        }
        Numeric = numeric.ToArray();
        Dependents = new int[Count][];
        AnyDependents = new int[Count][];
        for (int p = 0; p < Count; p++) { Dependents[p] = dependents[p].ToArray(); AnyDependents[p] = anyDependents[p].ToArray(); }
        ByEra = new int[byEra.Length][];
        for (int e = 0; e < byEra.Length; e++) ByEra[e] = byEra[e].ToArray();

        HubWeight = new int[Count];
        var seen = new bool[Count];
        var stack = new Stack<int>();
        foreach (int p in Numeric)
        {
            Array.Clear(seen);
            stack.Push(p);
            int n = 0;
            while (stack.Count > 0)
            {
                int q = stack.Pop();
                foreach (int d in Dependents[q]) if (!seen[d]) { seen[d] = true; n++; stack.Push(d); }
                foreach (int d in AnyDependents[q]) if (!seen[d]) { seen[d] = true; n++; stack.Push(d); }
            }
            HubWeight[p] = n;
        }

        CommonsCoef = catalog.Constant("CommonsCoef");
        CommonsExp = catalog.Constant("CommonsExp");
        ElderCommons = catalog.Constant("ElderCommons");
        LowTies = catalog.Constant("LowTiesShare");
        Calibration = catalog.Constant("InsightCalibration");
        EraLag = catalog.Constant("EraLag");
        EraBoon = catalog.Constant("EraBoonPct");
        SlotWeight[0] = catalog.Constant("SlotPrimary").FloorToInt();
        SlotWeight[1] = catalog.Constant("SlotSecondary").FloorToInt();
        SlotWeight[2] = catalog.Constant("SlotTertiary").FloorToInt();
    }

    /// <summary>The living economy's ambient practice unlocked by projects (LivingEconomySource.AmbientPractice), per adult a season.</summary>
    private static readonly (string Practice, string Project, int Hundredths)[] Ambient =
    {
        ("haul", "TRN-01", 5), ("cook", "FOD-01", 10), ("tell_stories", "ART-02", 15), ("teach", "LRN-01", 10),
        ("ritual", "BEL-01", 10), ("council", "GOV-01", 5), ("count_stores", "NUM-01", 5), ("mark", "TER-01", 5),
        ("guard", "DEF-02", 5), ("feast", "SOC-05", 10), ("heal", "MED-01", 5), ("fight", "WAR-01", 5),
        ("paddle", "SEA-01", 5), ("trade", "TRD-01", 5),
    };

    /// <summary>Same reading as the research system's know_how effect: "+20%" (all lineages) or "HNT, +10%" (one).</summary>
    private static void ParseKnowHow(ResearchCatalog catalog, string text, out int lineage, out Fix64 gain)
    {
        int percent = 0;
        bool digit = false;
        foreach (char ch in text)
        {
            if (ch is >= '0' and <= '9') { percent = checked(percent * 10 + (ch - '0')); digit = true; }
            else if (digit) break;
        }
        gain = Fix64.One + Fix64.FromRatio(percent, 100);
        int comma = text.IndexOf(',', StringComparison.Ordinal);
        lineage = comma > 0 ? catalog.LineageIndex(text[..comma].Trim()) : -1;
    }

    /// <summary>Insight sharing between bands before SOC-03 (docs/MVP/09 §7.4): (1 + 0.5 (n − 1)) / n.</summary>
    public Fix64 Ties(int settlements, bool soc03)
    {
        if (soc03 || settlements <= 1) return Fix64.One;
        return (Fix64.One + LowTies * (settlements - 1)) / settlements;
    }
}
