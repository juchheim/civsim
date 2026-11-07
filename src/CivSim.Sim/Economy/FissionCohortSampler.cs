using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Economy;

internal readonly record struct FissionFigureCandidate(int FigureIndex, long Id, int Age, bool Female, bool Skilled,
    byte Kit = 0, bool WorkedWithToolLastSeason = false);

internal sealed class FissionCohortSample
{
    internal FissionCohortSample(bool[] daughterFigures, string result)
    {
        DaughterFigures = daughterFigures;
        Result = result;
    }

    internal bool[] DaughterFigures { get; }
    internal string Result { get; }
    internal bool Success => Result is "selectedRequiredWomanAndElderOrSkilledAdult" or
        "selectedRequiredWomanAndElderOrSkilledAdultCoveragePreserved";
}

/// <summary>The one deterministic selection path used by live fission and its report-only preview.</summary>
internal static class FissionCohortSampler
{
    internal static Fix64 EstimatedQuarterFoodOutput(Fix64 adultWork, Fix64 yieldPerWork,
        Fix64 observedFoodWorkShare, Fix64 sustainableAnnualFoodIncome)
    {
        Fix64 recentYieldEstimate = Fix64.Max(Fix64.Zero, adultWork) *
            Fix64.Max(Fix64.Zero, yieldPerWork) *
            Fix64.Clamp(observedFoodWorkShare, Fix64.Zero, Fix64.One);
        Fix64 sustainableQuarterCap = Fix64.Max(Fix64.Zero, sustainableAnnualFoodIncome) / Fix64.FromInt(4);
        return Fix64.Min(recentYieldEstimate, sustainableQuarterCap);
    }

    internal static Fix64 StoreTransfer(Fix64 quantity, Fix64 toolDurability)
    {
        if (toolDurability <= Fix64.Zero) return quantity * Fix64.FromRatio(2, 5);
        int wholeUnits = quantity.FloorToInt();
        int transferableUnits = wholeUnits < 2 ? 0 : System.Math.Min(wholeUnits - 1, (wholeUnits * 2 + 2) / 5);
        return Fix64.FromInt(transferableUnits);
    }

    internal static FissionCohortSample Select(ulong seed, ulong stream, int parentId, int season, int figureCount,
        int daughterCount, IReadOnlyList<FissionFigureCandidate> source, bool preserveCurrentToolCoverage = false)
    {
        var candidates = new List<FissionFigureCandidate>(source.Count);
        for (int i = 0; i < source.Count; i++) candidates.Add(source[i]);
        candidates.Sort((left, right) => left.Id.CompareTo(right.Id));

        Pcg32 sample = Pcg32.ForStream(seed, stream, (ulong)(uint)parentId, season);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = (int)sample.NextBelow((uint)(i + 1));
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int womanIndex = -1;
        int elderOrSkilledAdultIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            FissionFigureCandidate figure = candidates[i];
            if (figure.Female && figure.Age is >= 14 and < 45)
            {
                womanIndex = figure.FigureIndex;
                break;
            }
        }
        for (int i = 0; i < candidates.Count; i++)
        {
            FissionFigureCandidate figure = candidates[i];
            if (figure.FigureIndex != womanIndex &&
                (figure.Age >= 45 || figure.Age is >= 14 and < 45 && figure.Skilled))
            {
                elderOrSkilledAdultIndex = figure.FigureIndex;
                break;
            }
        }
        if (womanIndex < 0)
        {
            bool donorHasWoman = source.Any(figure => figure.Female && figure.Age is >= 14 and < 45);
            return new FissionCohortSample(new bool[figureCount], donorHasWoman
                ? "daughterSampleMissingRequiredWoman14to44"
                : "donorMissingRequiredWoman14to44");
        }
        if (elderOrSkilledAdultIndex < 0)
        {
            bool donorHasElderOrSkilledAdult = source.Any(figure => figure.Age >= 45 ||
                figure.Age is >= 14 and < 45 && figure.Skilled);
            return new FissionCohortSample(new bool[figureCount], donorHasElderOrSkilledAdult
                ? "daughterSampleMissingRequiredElderOrSkilledAdult"
                : "donorMissingRequiredElderOrSkilledAdult");
        }

        bool[] selected = new bool[figureCount];
        selected[womanIndex] = true;
        selected[elderOrSkilledAdultIndex] = true;
        int moved = 2;
        for (int i = 0; i < candidates.Count && moved < daughterCount; i++)
        {
            int figureIndex = candidates[i].FigureIndex;
            if (selected[figureIndex]) continue;
            selected[figureIndex] = true;
            moved++;
        }
        bool hasCurrentIronToolWorkers = candidates.Any(figure => figure.WorkedWithToolLastSeason &&
            figure.Age >= 14 && figure.Kit is 11 or 12);
        bool coverageSwap = preserveCurrentToolCoverage && PreserveCurrentToolCoverage(selected, candidates);
        if (preserveCurrentToolCoverage && hasCurrentIronToolWorkers && !MeetsCurrentToolCoverage(selected, candidates))
            return new FissionCohortSample(new bool[figureCount], "ironToolCoverageCannotBePreserved");
        if (preserveCurrentToolCoverage && BalanceIdleKitHolderShare(selected, candidates)) coverageSwap = true;
        if (preserveCurrentToolCoverage && hasCurrentIronToolWorkers &&
            !MeetsCurrentToolCoverageWithHolderBuffer(selected, candidates))
        {
            if (!PreserveCurrentToolCoverageHolderBuffer(selected, candidates))
                return new FissionCohortSample(new bool[figureCount], "ironToolCoverageCannotBePreserved");
            coverageSwap = true;
        }
        return moved < 6
            ? new FissionCohortSample(selected, "daughterSampleBelowSixMembers")
            : new FissionCohortSample(selected, coverageSwap
                ? "selectedRequiredWomanAndElderOrSkilledAdultCoveragePreserved"
                : "selectedRequiredWomanAndElderOrSkilledAdult");
    }

    private static bool PreserveCurrentToolCoverage(bool[] selected, IReadOnlyList<FissionFigureCandidate> candidates)
    {
        if (!candidates.Any(figure => figure.WorkedWithToolLastSeason && figure.Kit is 11 or 12)) return false;
        if (MeetsCurrentToolCoverage(selected, candidates)) return false;
        bool[] original = (bool[])selected.Clone();
        int previousDeficit = CurrentToolCoverageDeficit(selected, candidates);
        int maximumSwaps = candidates.Count / 2;
        for (int pass = 0; pass < maximumSwaps && previousDeficit > 0; pass++)
        {
            int bestDaughterIndex = -1, bestParentIndex = -1, bestDeficit = previousDeficit;
            // Candidate order is already the deterministic seeded shuffle. Prefer the first best-scoring swap
            // so equal improvements have a stable, seed-specific tie-break.
            for (int selectedIndex = 0; selectedIndex < candidates.Count; selectedIndex++)
            {
                FissionFigureCandidate movingBack = candidates[selectedIndex];
                if (!selected[movingBack.FigureIndex]) continue;
                selected[movingBack.FigureIndex] = false;
                for (int parentIndex = 0; parentIndex < candidates.Count; parentIndex++)
                {
                    FissionFigureCandidate movingOut = candidates[parentIndex];
                    if (selected[movingOut.FigureIndex]) continue;
                    selected[movingOut.FigureIndex] = true;
                    if (HasRequiredDaughterRepresentatives(selected, candidates))
                    {
                        int deficit = CurrentToolCoverageDeficit(selected, candidates);
                        if (deficit < bestDeficit)
                        {
                            bestDeficit = deficit;
                            bestDaughterIndex = selectedIndex;
                            bestParentIndex = parentIndex;
                        }
                    }
                    selected[movingOut.FigureIndex] = false;
                }
                selected[movingBack.FigureIndex] = true;
            }

            if (bestDaughterIndex < 0) break;
            selected[candidates[bestDaughterIndex].FigureIndex] = false;
            selected[candidates[bestParentIndex].FigureIndex] = true;
            previousDeficit = bestDeficit;
            if (MeetsCurrentToolCoverage(selected, candidates)) return true;
        }

        Array.Copy(original, selected, selected.Length);
        return false;
    }

    private static bool MeetsCurrentToolCoverage(bool[] selected, IReadOnlyList<FissionFigureCandidate> candidates)
    {
        int daughterWorkers = 0, daughterEquipped = 0, parentWorkers = 0, parentEquipped = 0;
        foreach (FissionFigureCandidate figure in candidates)
        {
            if (!figure.WorkedWithToolLastSeason || figure.Age < 14) continue;
            bool equipped = figure.Kit is 11 or 12;
            if (selected[figure.FigureIndex])
            {
                daughterWorkers++;
                if (equipped) daughterEquipped++;
            }
            else
            {
                parentWorkers++;
                if (equipped) parentEquipped++;
            }
        }
        bool hasToolWorkers = daughterWorkers + parentWorkers > 0;
        return (!hasToolWorkers || daughterWorkers > 0 && parentWorkers > 0) &&
            MeetsCoverage(daughterEquipped, daughterWorkers) && MeetsCoverage(parentEquipped, parentWorkers);
    }

    private static bool MeetsCurrentToolCoverageWithHolderBuffer(bool[] selected,
        IReadOnlyList<FissionFigureCandidate> candidates)
    {
        int daughterWorkers = 0, parentWorkers = 0, daughterHolders = 0, parentHolders = 0;
        foreach (FissionFigureCandidate figure in candidates)
        {
            bool daughter = selected[figure.FigureIndex];
            if (figure.Age >= 14 && figure.WorkedWithToolLastSeason)
            {
                if (daughter) daughterWorkers++;
                else parentWorkers++;
            }
            if (figure.Age >= 14 && figure.Kit is 11 or 12)
            {
                if (daughter) daughterHolders++;
                else parentHolders++;
            }
        }
        if (!MeetsCurrentToolCoverage(selected, candidates)) return false;
        int totalHolders = daughterHolders + parentHolders;
        (int parentNeed, int daughterNeed) = FissionHolderRequirements(candidates.Count, parentWorkers,
            daughterWorkers, totalHolders);
        return parentHolders >= parentNeed && daughterHolders >= daughterNeed;
    }

    private static int HolderNeed(int workers, int growthBuffer)
        => workers == 0 ? 0 : (workers * 7 + 9) / 10 + growthBuffer;

    private static (int ParentNeed, int DaughterNeed) FissionHolderRequirements(int parentPopulation,
        int parentWorkers, int daughterWorkers, int totalHolders)
    {
        // A small split with one recent toolworker on each side and exactly four holders leaves no
        // flexible kit after the usual one-spare allocation. Protect the retained parent against a
        // two-worker rebound; the daughter keeps the 70% floor. Other fissions keep the one-spare rule.
        if (parentPopulation <= 20 && parentWorkers == 1 && daughterWorkers == 1 && totalHolders == 4)
        {
            int parentNeed = HolderNeed(parentWorkers, growthBuffer: 2);
            return (parentNeed, HolderNeed(daughterWorkers, growthBuffer: 0));
        }

        return (HolderNeed(parentWorkers, growthBuffer: 1), HolderNeed(daughterWorkers, growthBuffer: 1));
    }

    private static bool PreserveCurrentToolCoverageHolderBuffer(bool[] selected,
        IReadOnlyList<FissionFigureCandidate> candidates)
    {
        bool[] original = (bool[])selected.Clone();
        int previousDeficit = CurrentHolderGrowthBufferDeficit(selected, candidates);
        int maximumSwaps = candidates.Count / 2;
        for (int pass = 0; pass < maximumSwaps && previousDeficit > 0; pass++)
        {
            int bestDaughterIndex = -1, bestParentIndex = -1, bestDeficit = previousDeficit;
            for (int selectedIndex = 0; selectedIndex < candidates.Count; selectedIndex++)
            {
                FissionFigureCandidate movingBack = candidates[selectedIndex];
                if (!selected[movingBack.FigureIndex]) continue;
                selected[movingBack.FigureIndex] = false;
                for (int parentIndex = 0; parentIndex < candidates.Count; parentIndex++)
                {
                    FissionFigureCandidate movingOut = candidates[parentIndex];
                    if (selected[movingOut.FigureIndex]) continue;
                    selected[movingOut.FigureIndex] = true;
                    if (HasRequiredDaughterRepresentatives(selected, candidates) &&
                        MeetsCurrentToolCoverage(selected, candidates))
                    {
                        int deficit = CurrentHolderGrowthBufferDeficit(selected, candidates);
                        if (deficit < bestDeficit)
                        {
                            bestDeficit = deficit;
                            bestDaughterIndex = selectedIndex;
                            bestParentIndex = parentIndex;
                        }
                    }
                    selected[movingOut.FigureIndex] = false;
                }
                selected[movingBack.FigureIndex] = true;
            }

            if (bestDaughterIndex < 0) break;
            selected[candidates[bestDaughterIndex].FigureIndex] = false;
            selected[candidates[bestParentIndex].FigureIndex] = true;
            previousDeficit = bestDeficit;
            if (MeetsCurrentToolCoverageWithHolderBuffer(selected, candidates)) return true;
        }

        Array.Copy(original, selected, selected.Length);
        return false;
    }

    private static int CurrentHolderGrowthBufferDeficit(bool[] selected,
        IReadOnlyList<FissionFigureCandidate> candidates)
    {
        int daughterWorkers = 0, parentWorkers = 0, daughterHolders = 0, parentHolders = 0;
        foreach (FissionFigureCandidate figure in candidates)
        {
            bool daughter = selected[figure.FigureIndex];
            if (figure.Age >= 14 && figure.WorkedWithToolLastSeason)
            {
                if (daughter) daughterWorkers++;
                else parentWorkers++;
            }
            if (figure.Age >= 14 && figure.Kit is 11 or 12)
            {
                if (daughter) daughterHolders++;
                else parentHolders++;
            }
        }
        int totalHolders = daughterHolders + parentHolders;
        (int parentNeed, int daughterNeed) = FissionHolderRequirements(candidates.Count, parentWorkers,
            daughterWorkers, totalHolders);
        return System.Math.Max(0, daughterNeed - daughterHolders) +
            System.Math.Max(0, parentNeed - parentHolders);
    }

    private static bool MeetsCoverage(int equipped, int workers)
        => workers == 0 || equipped * 10 >= workers * 7;

    private static int CurrentToolCoverageDeficit(bool[] selected, IReadOnlyList<FissionFigureCandidate> candidates)
    {
        int daughterWorkers = 0, daughterEquipped = 0, parentWorkers = 0, parentEquipped = 0;
        foreach (FissionFigureCandidate figure in candidates)
        {
            if (!figure.WorkedWithToolLastSeason || figure.Age < 14) continue;
            bool equipped = figure.Kit is 11 or 12;
            if (selected[figure.FigureIndex])
            {
                daughterWorkers++;
                if (equipped) daughterEquipped++;
            }
            else
            {
                parentWorkers++;
                if (equipped) parentEquipped++;
            }
        }
        int deficit = System.Math.Max(0, daughterWorkers * 7 - daughterEquipped * 10) +
            System.Math.Max(0, parentWorkers * 7 - parentEquipped * 10);
        if (daughterWorkers + parentWorkers > 0 && (daughterWorkers == 0 || parentWorkers == 0))
            deficit += candidates.Count * 7 + 1;
        return deficit;
    }

    private static bool HasRequiredDaughterRepresentatives(bool[] selected,
        IReadOnlyList<FissionFigureCandidate> candidates)
    {
        bool woman = false, elderOrSkilledAdult = false;
        foreach (FissionFigureCandidate figure in candidates)
        {
            if (!selected[figure.FigureIndex]) continue;
            woman |= figure.Female && figure.Age is >= 14 and < 45;
            elderOrSkilledAdult |= figure.Age >= 45 || figure.Age is >= 14 and < 45 && figure.Skilled;
        }
        return woman && elderOrSkilledAdult;
    }

    private static bool BalanceIdleKitHolderShare(bool[] selected, IReadOnlyList<FissionFigureCandidate> candidates)
    {
        if (!MeetsCurrentToolCoverage(selected, candidates)) return false;
        bool changed = false;
        int previousDifference = KitHolderShareDifference(selected, candidates);
        for (int pass = 0; pass < candidates.Count / 2 && previousDifference > 0; pass++)
        {
            int bestDaughterIndex = -1, bestParentIndex = -1, bestDifference = previousDifference;
            for (int selectedIndex = 0; selectedIndex < candidates.Count; selectedIndex++)
            {
                FissionFigureCandidate movingBack = candidates[selectedIndex];
                if (!selected[movingBack.FigureIndex] || movingBack.Age < 14 || movingBack.Kit is not (11 or 12) ||
                    movingBack.WorkedWithToolLastSeason) continue;
                selected[movingBack.FigureIndex] = false;
                for (int parentIndex = 0; parentIndex < candidates.Count; parentIndex++)
                {
                    FissionFigureCandidate movingOut = candidates[parentIndex];
                    if (selected[movingOut.FigureIndex] || movingOut.Age < 14 || movingOut.Kit is 11 or 12 ||
                        movingOut.WorkedWithToolLastSeason) continue;
                    selected[movingOut.FigureIndex] = true;
                    if (HasRequiredDaughterRepresentatives(selected, candidates) && MeetsCurrentToolCoverage(selected, candidates))
                    {
                        int difference = KitHolderShareDifference(selected, candidates);
                        if (difference < bestDifference)
                        {
                            bestDifference = difference;
                            bestDaughterIndex = selectedIndex;
                            bestParentIndex = parentIndex;
                        }
                    }
                    selected[movingOut.FigureIndex] = false;
                }
                selected[movingBack.FigureIndex] = true;
            }

            if (bestDaughterIndex < 0) break;
            selected[candidates[bestDaughterIndex].FigureIndex] = false;
            selected[candidates[bestParentIndex].FigureIndex] = true;
            previousDifference = bestDifference;
            changed = true;
        }
        return changed;
    }

    private static int KitHolderShareDifference(bool[] selected, IReadOnlyList<FissionFigureCandidate> candidates)
    {
        int daughterPopulation = selected.Count(value => value);
        int parentPopulation = candidates.Count - daughterPopulation;
        if (daughterPopulation == 0 || parentPopulation == 0) return int.MaxValue;
        int daughterHolders = 0, parentHolders = 0;
        foreach (FissionFigureCandidate figure in candidates)
        {
            if (figure.Kit is not (11 or 12)) continue;
            if (selected[figure.FigureIndex]) daughterHolders++;
            else parentHolders++;
        }
        return System.Math.Abs(daughterHolders * parentPopulation - parentHolders * daughterPopulation);
    }
}
