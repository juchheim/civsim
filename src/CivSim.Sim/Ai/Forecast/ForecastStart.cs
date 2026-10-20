using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>
/// Read access to one civilization's own research, exactly what <see cref="AiResearchPort"/> offers. The Spark reads it
/// through the port; tests and the harness read a Council civilization through <see cref="SystemReadout"/>.
/// </summary>
public interface IResearchReadout
{
    ResearchCatalog Catalog { get; }
    int Era { get; }
    bool IsDone(int project);
    ProjectState State(int project);
    Fix64 Progress(int project);
    Fix64 EffectiveCost(int project);
    int Slot(int slot);
    IReadOnlyList<int> Queue { get; }
    int DoneInEra(int era);
    Fix64 KnowHow(int lineage);
    Fix64 KnowHowApplied(int project);
    Fix64 InsightPerYear { get; }
    Fix64 CommonsPerYear { get; }
    int Leaning(int theme);
    ulong SeenMask { get; }
    int FirstBy(int project);
    Fix64 ModifierMultiplier(string stat);
}

/// <summary>The AI's own research, through the port (the only way the Spark reads it).</summary>
public sealed class PortReadout : IResearchReadout
{
    private readonly AiResearchPort _port;
    public PortReadout(AiResearchPort port) => _port = port;
    public ResearchCatalog Catalog => _port.Catalog;
    public int Era => _port.Era;
    public bool IsDone(int project) => _port.IsDone(project);
    public ProjectState State(int project) => _port.State(project);
    public Fix64 Progress(int project) => _port.Progress(project);
    public Fix64 EffectiveCost(int project) => _port.EffectiveCost(project);
    public int Slot(int slot) => _port.Slot(slot);
    public IReadOnlyList<int> Queue => _port.Queue;
    public int DoneInEra(int era) => _port.DoneInEra(era);
    public Fix64 KnowHow(int lineage) => _port.KnowHow(lineage);
    public Fix64 KnowHowApplied(int project) => _port.KnowHowApplied(project);
    public Fix64 InsightPerYear => _port.InsightPerYear;
    public Fix64 CommonsPerYear => _port.CommonsPerYear;
    public int Leaning(int theme) => _port.Leaning(theme);
    public ulong SeenMask => _port.SeenMask;
    public int FirstBy(int project) => _port.FirstBy(project);
    public Fix64 ModifierMultiplier(string stat) => _port.ModifierMultiplier(stat);
}

/// <summary>Any civilization's research read straight from the research system: for validation and the harness only.</summary>
public sealed class SystemReadout : IResearchReadout
{
    private readonly ResearchSystem _r;
    private readonly int _civ;
    public SystemReadout(ResearchSystem research, int civ) { _r = research; _civ = civ; }
    public ResearchCatalog Catalog => _r.Catalog;
    public int Era => _r.Era(_civ);
    public bool IsDone(int project) => _r.IsDone(_civ, project);
    public ProjectState State(int project) => _r.State(_civ, project);
    public Fix64 Progress(int project) => _r.Progress(_civ, project);
    public Fix64 EffectiveCost(int project) => _r.EffectiveCost(_civ, project);
    public int Slot(int slot) => _r.Slot(_civ, slot);
    public IReadOnlyList<int> Queue => _r.Queue(_civ);
    public int DoneInEra(int era) => _r.DoneInEra(_civ, era);
    public Fix64 KnowHow(int lineage) => _r.KnowHow(_civ, lineage);
    public Fix64 KnowHowApplied(int project) => _r.KnowHowApplied(_civ, project);
    public Fix64 InsightPerYear => _r.InsightPerYear(_civ);
    public Fix64 CommonsPerYear => _r.CommonsPerYear(_civ);
    public int Leaning(int theme) => _r.Leaning(_civ, theme);
    public ulong SeenMask => _r.SeenMask(_civ);
    public int FirstBy(int project) => _r.FirstBy(project);
    public Fix64 ModifierMultiplier(string stat) => _r.ModifierMultiplier(_civ, stat);
}

/// <summary>The people the forecast starts from: adults (14 to 44), elders, children and settlements.</summary>
public readonly record struct ForecastPeople(Fix64 Adults, Fix64 Elders, Fix64 Children, int Settlements);

/// <summary>
/// One civilization's state at the moment the forecast starts (docs/MVP/14 §3.2 "inputs: current state"): the exact own
/// research state plus the people. Captured once per decision; every rollout starts from a copy.
/// </summary>
public sealed class ForecastStart
{
    public ForecastStart(ForecastCatalog catalog)
    {
        Catalog = catalog;
        int n = catalog.Count;
        Done = new bool[n];
        Progress = new Fix64[n];
        KnowHowApplied = new Fix64[n];
        Cost = new Fix64[n];
        FirstBy = new int[n];
        SeenBlocked = new bool[n];
        DoneByEra = new int[ResearchCatalog.EraCount];
        KnowHow = new Fix64[catalog.LineageCount];
        KnowHowGen = new Fix64[catalog.LineageCount];
    }

    public ForecastCatalog Catalog { get; }
    public int Civ;
    /// <summary>Seasons completed in the match when captured.</summary>
    public int Season;
    public int Era;
    public bool[] Done;
    public Fix64[] Progress;
    public Fix64[] KnowHowApplied;
    /// <summary>Effective cost now (Diffusion, EraLag, climate), as research would charge it.</summary>
    public Fix64[] Cost;
    /// <summary>Public record of Firsts (docs/MVP/02 §14). A project nobody has claimed is undone everywhere.</summary>
    public int[] FirstBy;
    /// <summary>Projects held back by a [Seen] gate the civilization has not met: the forecast never unlocks them.</summary>
    public bool[] SeenBlocked;
    public int[] DoneByEra;
    public Fix64[] KnowHow;
    /// <summary>Know-how each lineage gained in the last season (observed), the base of the projection.</summary>
    public Fix64[] KnowHowGen;
    public readonly int[] Slots = { -1, -1, -1 };
    public readonly List<int> Queue = new();
    public readonly int[] Leaning = new int[3];
    public Fix64 InsightPerYear, CommonsPerYear;
    /// <summary>The Commons Insight multiplier and the elder multiplier of completed projects.</summary>
    public Fix64 LmCommons = Fix64.One, LmElder = Fix64.One;
    /// <summary>Archetype Insight scale × InsightCalibration × Era Boons so far.</summary>
    public Fix64 Scale = Fix64.One;
    public Fix64 RolesScale = Fix64.One;
    public ForecastPeople People;
    public Fix64 Mastery, Firsts;

    /// <summary>Copies the research state from a readout. People and Know-how generation are set by the caller.</summary>
    public void CaptureResearch(IResearchReadout r, int civ, EconomyArchetype archetype)
    {
        ForecastCatalog fc = Catalog;
        Civ = civ;
        Era = r.Era;
        ulong seen = r.SeenMask;
        Mastery = Fix64.Zero;
        Firsts = Fix64.Zero;
        for (int p = 0; p < fc.Count; p++)
        {
            Done[p] = r.IsDone(p);
            Progress[p] = Done[p] ? Fix64.Zero : r.Progress(p);
            KnowHowApplied[p] = Done[p] ? Fix64.Zero : r.KnowHowApplied(p);
            FirstBy[p] = r.FirstBy(p);
            Cost[p] = fc.IsNumeric[p] && !Done[p] ? r.EffectiveCost(p) : Fix64.Zero;
            bool blocked = false;
            if (fc.IsNumeric[p] && !Done[p] && r.State(p) == ProjectState.Hidden)
                foreach (Gate g in fc.Catalog.Projects[p].Gates)
                    if (g.Kind == GateKind.Seen && (seen & (1UL << g.Target)) == 0) blocked = true;
            SeenBlocked[p] = blocked;
            if (Done[p])
            {
                Mastery += Fix64.FromInt(fc.Mastery[p]);
                if (FirstBy[p] == civ) Firsts += fc.FirstPoints[p];
            }
        }
        for (int e = 0; e < DoneByEra.Length; e++) DoneByEra[e] = r.DoneInEra(e);
        for (int l = 0; l < KnowHow.Length; l++) KnowHow[l] = r.KnowHow(l);
        for (int s = 0; s < Slots.Length; s++) Slots[s] = r.Slot(s);
        Queue.Clear();
        foreach (int q in r.Queue) Queue.Add(q);
        for (int t = 0; t < 3; t++) Leaning[t] = r.Leaning(t);
        InsightPerYear = r.InsightPerYear;
        CommonsPerYear = r.CommonsPerYear;
        LmCommons = r.ModifierMultiplier("commons_insight");
        LmElder = r.ModifierMultiplier("elder_insight");
        (Fix64 insight, Fix64 roles) = ArchetypeScale(archetype);
        RolesScale = roles;
        Scale = insight * fc.Calibration * BoonSoFar();
    }

    /// <summary>Era Boons taken as Insight so far: +5% per Keystone unless the civilization leans to Reach (docs/MVP/02 §13).</summary>
    private Fix64 BoonSoFar()
    {
        if (BoonIsBeacon) return Fix64.One;
        return FixMath.PowInt(Fix64.One + Catalog.EraBoon, Era);
    }

    /// <summary>A Reach-leaning civilization takes the Era Boon as a Beacon instead of Insight.</summary>
    public bool BoonIsBeacon => Leaning[2] > Leaning[0] && Leaning[2] > Leaning[1];

    /// <summary>
    /// Archetype multipliers on Insight and specialist output (docs/MVP/07 sensitivity; the same table the living economy
    /// reports through its research source). Know-how scale is observed, not assumed.
    /// </summary>
    public static (Fix64 Insight, Fix64 Roles) ArchetypeScale(EconomyArchetype a) => a switch
    {
        EconomyArchetype.Grower => (Fix64.One, Fix64.FromRatio(9, 10)),
        EconomyArchetype.Builder => (Fix64.One, Fix64.One),
        EconomyArchetype.Wanderer => (Fix64.FromRatio(95, 100), Fix64.FromRatio(95, 100)),
        EconomyArchetype.Warlord => (Fix64.FromRatio(9, 10), Fix64.FromRatio(9, 10)),
        EconomyArchetype.Sage => (Fix64.FromRatio(11, 10), Fix64.FromRatio(14, 10)),
        _ => (Fix64.One, Fix64.One),
    };

    /// <summary>
    /// People from the own view: adults and population are exact, elders are recovered from Commons Insight
    /// (Commons = 4.8 × ((adults + 2 × elder mult × elders) × ties)^0.85 × mods × scale, docs/MVP/02 §8.1).
    /// </summary>
    public ForecastPeople EstimatePeople(int population, int adults, int settlements)
    {
        ForecastCatalog fc = Catalog;
        settlements = Math.Max(1, settlements);
        Fix64 a = Fix64.FromInt(adults);
        Fix64 rest = Fix64.FromInt(Math.Max(0, population - adults));
        Fix64 elders = Fix64.Zero;
        Fix64 denominator = fc.CommonsCoef * LmCommons * Scale;
        if (CommonsPerYear.Raw > 0 && denominator.Raw > 0)
        {
            Fix64 ties = fc.Ties(settlements, Done[fc.Soc03]);
            Fix64 w = FixMath.Pow(CommonsPerYear / denominator, Fix64.One / fc.CommonsExp);
            Fix64 e = (w / ties - a) / (fc.ElderCommons * LmElder);
            elders = Fix64.Clamp(e, Fix64.Zero, rest);
        }
        return new ForecastPeople(a, elders, rest - elders, settlements);
    }
}
