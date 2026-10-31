using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Economy;

internal readonly record struct FissionFigureCandidate(int FigureIndex, long Id, int Age, bool Female, bool Skilled);

internal sealed class FissionCohortSample
{
    internal FissionCohortSample(bool[] daughterFigures, string result)
    {
        DaughterFigures = daughterFigures;
        Result = result;
    }

    internal bool[] DaughterFigures { get; }
    internal string Result { get; }
    internal bool Success => Result == "selectedRequiredWomanAndElderOrSkilledAdult";
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
        int daughterCount, IReadOnlyList<FissionFigureCandidate> source)
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
        return moved < 6
            ? new FissionCohortSample(selected, "daughterSampleBelowSixMembers")
            : new FissionCohortSample(selected, "selectedRequiredWomanAndElderOrSkilledAdult");
    }
}
