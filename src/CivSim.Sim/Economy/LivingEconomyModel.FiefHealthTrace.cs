using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>Per-civilization expected hazard totals from one completed season; diagnostic only.</summary>
internal readonly record struct FiefExpectedHazardSnapshot(
    Fix64 Endemic, Fix64 Epidemic, Fix64 Starvation, Fix64 Exposure, Fix64 Injury,
    Fix64 Predators, Fix64 Violence, Fix64 Senescence,
    Fix64 AdultEquivalentPersonSeasons, int EpidemicDeaths, int TotalDeaths,
    int PoxDeaths, int PlagueDeaths, bool Quarantine,
    int ImportedAttempts, int ImportedBlocked, int ImportedSuccesses)
{
    public Fix64 Total => Endemic + Epidemic + Starvation + Exposure + Injury + Predators + Violence + Senescence;
    public Fix64 EpidemicShare => Total > Fix64.Zero ? Epidemic / Total : Fix64.Zero;
}

/// <summary>Opt-in per-civ health telemetry for the F-T7 measurement harness. Never affects mechanics or persisted state.</summary>
public sealed partial class LivingEconomyModel
{
    internal static Fix64 CombinedStarvationHazardForDiagnostics(Fix64 starvation, Fix64 winterShortfall)
        => starvation + winterShortfall;
    private Fix64[]? _fiefExpectedHazardsByCiv;
    private Fix64[]? _fiefAdultEquivalentPersonSeasonsByCiv;
    private int[]? _fiefEpidemicDeathsByCiv;
    private int[]? _fiefTotalDeathsByCiv;
    private int[]? _fiefPoxDeathsByCiv;
    private int[]? _fiefPlagueDeathsByCiv;
    private int[]? _fiefImportedAttemptsByCiv;
    private int[]? _fiefImportedBlockedByCiv;
    private int[]? _fiefImportedSuccessesByCiv;
    internal bool FiefHealthTraceEnabledForDiagnostics { get; private set; }

    internal void EnableFiefHealthTraceForDiagnostics()
    {
        if (!FiefChain) return;
        FiefHealthTraceEnabledForDiagnostics = true;
        _fiefExpectedHazardsByCiv = new Fix64[CivilizationCount * DeathCauseCount];
        _fiefAdultEquivalentPersonSeasonsByCiv = new Fix64[CivilizationCount];
        _fiefEpidemicDeathsByCiv = new int[CivilizationCount];
        _fiefTotalDeathsByCiv = new int[CivilizationCount];
        _fiefPoxDeathsByCiv = new int[CivilizationCount];
        _fiefPlagueDeathsByCiv = new int[CivilizationCount];
        _fiefImportedAttemptsByCiv = new int[CivilizationCount];
        _fiefImportedBlockedByCiv = new int[CivilizationCount];
        _fiefImportedSuccessesByCiv = new int[CivilizationCount];
    }

    internal void ResetFiefExpectedHazardsForDiagnostics()
    {
        if (FiefHealthTraceEnabledForDiagnostics && _fiefExpectedHazardsByCiv is { } values)
            Array.Clear(values);
        if (_fiefAdultEquivalentPersonSeasonsByCiv is { } personSeasons) Array.Clear(personSeasons);
        if (_fiefEpidemicDeathsByCiv is { } epidemicDeaths) Array.Clear(epidemicDeaths);
        if (_fiefTotalDeathsByCiv is { } totalDeaths) Array.Clear(totalDeaths);
        if (_fiefPoxDeathsByCiv is { } poxDeaths) Array.Clear(poxDeaths);
        if (_fiefPlagueDeathsByCiv is { } plagueDeaths) Array.Clear(plagueDeaths);
        if (_fiefImportedAttemptsByCiv is { } attempts) Array.Clear(attempts);
        if (_fiefImportedBlockedByCiv is { } blocked) Array.Clear(blocked);
        if (_fiefImportedSuccessesByCiv is { } successes) Array.Clear(successes);
    }

    internal FiefExpectedHazardSnapshot CaptureFiefExpectedHazardsForDiagnostics(int civilization)
    {
        if (!FiefHealthTraceEnabledForDiagnostics || _fiefExpectedHazardsByCiv is not { } values ||
            (uint)civilization >= (uint)CivilizationCount) return default;
        int offset = civilization * DeathCauseCount;
        return new FiefExpectedHazardSnapshot(
            values[offset + (int)EconomyDeathCause.Endemic],
            values[offset + (int)EconomyDeathCause.Epidemic],
            values[offset + (int)EconomyDeathCause.Starvation],
            values[offset + (int)EconomyDeathCause.Exposure],
            values[offset + (int)EconomyDeathCause.Injury],
            values[offset + (int)EconomyDeathCause.Predators],
            values[offset + (int)EconomyDeathCause.Violence],
            values[offset + (int)EconomyDeathCause.Senescence],
            _fiefAdultEquivalentPersonSeasonsByCiv?[civilization] ?? Fix64.Zero,
            _fiefEpidemicDeathsByCiv?[civilization] ?? 0,
            _fiefTotalDeathsByCiv?[civilization] ?? 0,
            _fiefPoxDeathsByCiv?[civilization] ?? 0,
            _fiefPlagueDeathsByCiv?[civilization] ?? 0,
            FiefQuarantineForDiagnostics(civilization),
            _fiefImportedAttemptsByCiv?[civilization] ?? 0,
            _fiefImportedBlockedByCiv?[civilization] ?? 0,
            _fiefImportedSuccessesByCiv?[civilization] ?? 0);
    }

    private void RecordFiefExpectedHazardsForDiagnostics(FigureState figure, HazardBreakdown hazards)
    {
        if (!FiefHealthTraceEnabledForDiagnostics || _fiefExpectedHazardsByCiv is not { } values ||
            (uint)figure.Civilization >= (uint)CivilizationCount) return;
        int offset = figure.Civilization * DeathCauseCount;
        // Adult-equivalent exposure: children contribute 0.5; adults and elders contribute 1.
        if (_fiefAdultEquivalentPersonSeasonsByCiv is { } exposure)
            exposure[figure.Civilization] += figure.Age < 14 ? Fix64.FromRatio(1, 2) : Fix64.One;
        values[offset + (int)EconomyDeathCause.Endemic] += hazards.Endemic;
        values[offset + (int)EconomyDeathCause.Epidemic] += hazards.Epidemic;
        values[offset + (int)EconomyDeathCause.Starvation] += CombinedStarvationHazardForDiagnostics(hazards.Starvation, hazards.WinterShortfall);
        values[offset + (int)EconomyDeathCause.Exposure] += hazards.Exposure;
        values[offset + (int)EconomyDeathCause.Injury] += hazards.Injury;
        values[offset + (int)EconomyDeathCause.Predators] += hazards.Predators;
        values[offset + (int)EconomyDeathCause.Violence] += hazards.Violence;
        values[offset + (int)EconomyDeathCause.Senescence] += hazards.Senescence;
    }

    private void RecordFiefDeathForDiagnostics(FigureState figure, EconomyDeathCause cause)
    {
        if (!FiefHealthTraceEnabledForDiagnostics || (uint)figure.Civilization >= (uint)CivilizationCount) return;
        if (_fiefTotalDeathsByCiv is { } total) total[figure.Civilization]++;
        if (cause == EconomyDeathCause.Epidemic && _fiefEpidemicDeathsByCiv is { } epidemic)
            epidemic[figure.Civilization]++;
        if (cause == EconomyDeathCause.Epidemic && figure.Infected && (uint)figure.SettlementId < (uint)_settlements.Count &&
            _settlements[figure.SettlementId].Civic is { Outbreak: (byte)OutbreakClass.Pox } &&
            _fiefPoxDeathsByCiv is { } pox) pox[figure.Civilization]++;
        if (cause == EconomyDeathCause.Epidemic && figure.Infected && (uint)figure.SettlementId < (uint)_settlements.Count &&
            _settlements[figure.SettlementId].Civic is { Outbreak: (byte)OutbreakClass.Plague } &&
            _fiefPlagueDeathsByCiv is { } plague) plague[figure.Civilization]++;
    }

    private bool FiefQuarantineForDiagnostics(int civilization)
    {
        if (!FiefHealthTraceEnabledForDiagnostics || (uint)civilization >= (uint)CivilizationCount) return false;
        foreach (SettlementState settlement in _settlements)
            if (settlement.Civilization == civilization && HasFiefQuarantine(settlement)) return true;
        return false;
    }

    private void RecordFiefImportForDiagnostics(int civilization, bool blocked)
    {
        if (!FiefHealthTraceEnabledForDiagnostics || (uint)civilization >= (uint)CivilizationCount) return;
        if (_fiefImportedAttemptsByCiv is { } attempts) attempts[civilization]++;
        if (blocked)
        {
            if (_fiefImportedBlockedByCiv is { } rejected) rejected[civilization]++;
        }
        else if (_fiefImportedSuccessesByCiv is { } successes) successes[civilization]++;
    }
}
