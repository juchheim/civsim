using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai.Forecast;

/// <summary>
/// Learns how much Know-how each lineage gains a season from the civilization's own pools (docs/MVP/02 §9): gain = change
/// in the pool + what drained into projects since the last look. Part of the mind's saved state, so a resumed match
/// forecasts exactly as the straight one.
/// </summary>
public sealed class KnowHowTracker
{
    private Fix64[] _lastPool = Array.Empty<Fix64>();
    private Fix64[] _lastApplied = Array.Empty<Fix64>();
    private Fix64[] _gain = Array.Empty<Fix64>();
    private int _lastSeason = -1;

    /// <summary>Know-how gained per season by each lineage, as last observed (blended with the observation before).</summary>
    public ReadOnlySpan<Fix64> Gain => _gain;
    public int LastSeason => _lastSeason;

    public void Observe(IResearchReadout r, int season)
    {
        ResearchCatalog cat = r.Catalog;
        int lineages = cat.LineageCodes.Count;
        if (_gain.Length != lineages) { _lastPool = new Fix64[lineages]; _lastApplied = new Fix64[lineages]; _gain = new Fix64[lineages]; }
        if (season == _lastSeason) return;
        Span<Fix64> applied = lineages <= 64 ? stackalloc Fix64[lineages] : new Fix64[lineages];
        applied.Clear();
        for (int p = 0; p < cat.Count; p++)
        {
            int l = cat.Projects[p].Lineage;
            if (l >= 0) applied[l] += r.KnowHowApplied(p);
        }
        // Seasons covered by this look: since the last one, or since the match began (the season counter is the number of
        // seasons completed, so the first season-end look sees season 1).
        int seasons = _lastSeason < 0 ? season : season - _lastSeason;
        seasons = Math.Max(1, seasons);
        for (int l = 0; l < lineages; l++)
        {
            Fix64 pool = r.KnowHow(l);
            Fix64 observed = Fix64.Max(Fix64.Zero, pool - _lastPool[l] + applied[l] - _lastApplied[l]) / seasons;
            _gain[l] = _lastSeason < 0 ? observed : (_gain[l] + observed * 3) / 4;
            _lastPool[l] = pool;
            _lastApplied[l] = applied[l];
        }
        _lastSeason = season;
    }

    public void Hash(ref StateHasher h)
    {
        h.Add(_lastSeason);
        h.Add(_gain.Length);
        h.Add(_lastPool);
        h.Add(_lastApplied);
        h.Add(_gain);
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_lastSeason);
        w.Write(_gain.Length);
        for (int i = 0; i < _gain.Length; i++) { w.Write(_lastPool[i].Raw); w.Write(_lastApplied[i].Raw); w.Write(_gain[i].Raw); }
    }

    public void Read(BinaryReader r)
    {
        _lastSeason = r.ReadInt32();
        int n = r.ReadInt32();
        _lastPool = new Fix64[n]; _lastApplied = new Fix64[n]; _gain = new Fix64[n];
        for (int i = 0; i < n; i++)
        {
            _lastPool[i] = Fix64.FromRaw(r.ReadInt64());
            _lastApplied[i] = Fix64.FromRaw(r.ReadInt64());
            _gain[i] = Fix64.FromRaw(r.ReadInt64());
        }
    }
}

/// <summary>Builds a <see cref="ForecastStart"/> for a civilization of a running match (validation and the harness).</summary>
public static class ForecastCapture
{
    /// <summary>Exact capture from the research system: people from the same inputs research uses.</summary>
    public static void FromSystem(ForecastStart start, ResearchSystem research, int civ, Economy.EconomyArchetype archetype, KnowHowTracker tracker, int season)
    {
        var readout = new SystemReadout(research, civ);
        tracker.Observe(readout, season);
        start.CaptureResearch(readout, civ, archetype);
        start.Season = season;
        InsightInputs inputs = research.ForecastInsightInputs(civ);
        (Fix64 insight, Fix64 roles) = ForecastStart.ArchetypeScale(archetype);
        start.Scale = insight * start.Catalog.Calibration * research.ForecastEraBoon(civ);
        start.RolesScale = roles;
        start.People = new ForecastPeople(inputs.Adults, inputs.Elders, Fix64.Zero, Math.Max(1, inputs.Settlements));
        tracker.Gain.CopyTo(start.KnowHowGen);
    }

    /// <summary>Sets the children (the economy's population minus adults and elders) on a captured start.</summary>
    public static void SetPopulation(ForecastStart start, int population)
    {
        ForecastPeople p = start.People;
        Fix64 kids = Fix64.Max(Fix64.Zero, Fix64.FromInt(population) - p.Adults - p.Elders);
        start.People = p with { Children = kids };
    }
}
