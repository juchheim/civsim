using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

internal enum FissionSiteRejectionReason
{
    Uninhabitable,
    ParentDistanceBelowMinimum,
    BeyondReach,
    TooCloseToAnotherSettlement,
    Unreachable,
    DisallowedByAdvisor,
}

internal readonly record struct FissionSiteSelectionAttempt(int Civ, int Parent, int Season, int ScannedTiles,
    int LegalSites, int SelectedTile, int[] Rejections, bool RecoveredAfterBlocked,
    Fix64 SelectedRawFood, Fix64 SelectedMultiplier, Fix64 SelectedScore, int RawBestTile,
    Fix64 RawBestFood, Fix64 RawBestMultiplier, Fix64 RawBestScore, int SelectedRawRank, Fix64 RawFoodShortfall);
internal readonly record struct FissionResolutionAttempt(int Civ, string Trace);
internal readonly record struct FissionTriggerTimelineRow(int Civ, string Trace);
internal readonly record struct FissionLeadCheckpointRow(int Civ, string Trace);

/// <summary>Opt-in, nonserialized observation of fission-site selection; never participates in simulation decisions.</summary>
internal sealed class FissionSiteDiagnostics
{
    private readonly List<FissionSiteSelectionAttempt> _attempts = new();
    private readonly SortedSet<(int Civ, int Parent)> _blocked = new();
    private readonly SortedSet<(int Civ, int Parent)> _recovered = new();
    private readonly List<FissionResolutionAttempt> _resolutions = new();
    private readonly List<FissionTriggerTimelineRow> _triggerTimeline = new();
    private readonly List<FissionLeadCheckpointRow> _leadCheckpoints = new();

    internal IReadOnlyList<FissionSiteSelectionAttempt> Attempts => _attempts;
    internal IReadOnlyList<FissionResolutionAttempt> Resolutions => _resolutions;
    internal IReadOnlyList<FissionTriggerTimelineRow> TriggerTimeline => _triggerTimeline;
    internal IReadOnlyList<FissionLeadCheckpointRow> LeadCheckpoints => _leadCheckpoints;

    internal void RecordResolution(int civ, string trace) => _resolutions.Add(new FissionResolutionAttempt(civ, trace));
    internal void RecordTriggerTimeline(int civ, string trace) => _triggerTimeline.Add(new FissionTriggerTimelineRow(civ, trace));
    internal void RecordLeadCheckpoint(int civ, string trace) => _leadCheckpoints.Add(new FissionLeadCheckpointRow(civ, trace));

    internal void RecordAttempt(int civ, int parent, int season, int scannedTiles, int legalSites,
        int selectedTile, int[] rejections, Fix64 selectedRawFood = default, Fix64 selectedMultiplier = default,
        Fix64 selectedScore = default, int rawBestTile = -1, Fix64 rawBestFood = default,
        Fix64 rawBestMultiplier = default, Fix64 rawBestScore = default, int selectedRawRank = 0,
        Fix64 rawFoodShortfall = default)
    {
        var key = (civ, parent);
        bool recovered = selectedTile >= 0 && _blocked.Contains(key) && _recovered.Add(key);
        if (selectedTile < 0) _blocked.Add(key);
        _attempts.Add(new FissionSiteSelectionAttempt(civ, parent, season, scannedTiles, legalSites,
            selectedTile, rejections, recovered, selectedRawFood, selectedMultiplier, selectedScore, rawBestTile,
            rawBestFood, rawBestMultiplier, rawBestScore, selectedRawRank, rawFoodShortfall));
    }

    internal static string Format(FissionSiteSelectionAttempt attempt, string civName)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        string[] names = { "uninhabitable", "parentDistanceBelowMin", "beyondReach",
            "tooCloseToSettlement", "unreachable", "disallowedByAdvisor" };
        var parts = new string[names.Length];
        for (int i = 0; i < parts.Length; i++) parts[i] = $"{names[i]}={(i < attempt.Rejections.Length ? attempt.Rejections[i] : 0)}";
        return $"FISSION_SITE_DIAG civ={attempt.Civ}/{civName} parent=s{attempt.Parent} season={attempt.Season} " +
            $"scanned={attempt.ScannedTiles} legal={attempt.LegalSites} selected={attempt.SelectedTile} " +
            $"noSite={(attempt.SelectedTile < 0 ? "true" : "false")} reject[{string.Join(',', parts)}]" +
            (attempt.RecoveredAfterBlocked ? " recoveredAfterBlocked=true" : string.Empty) +
            $" selectedRawFood={F(attempt.SelectedRawFood)} selectedMultiplier={F(attempt.SelectedMultiplier)} selectedScore={F(attempt.SelectedScore)}" +
            $" rawBestTile={attempt.RawBestTile} rawBestFood={F(attempt.RawBestFood)} rawBestMultiplier={F(attempt.RawBestMultiplier)} rawBestScore={F(attempt.RawBestScore)}" +
            $" selectedRawRank={attempt.SelectedRawRank} rawFoodShortfall={F(attempt.RawFoodShortfall)}";
    }
}
