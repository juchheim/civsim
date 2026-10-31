using System;
using CivSim.Sim.Economy;

namespace CivSim.Sim.View;

/// <summary>What is holding a settlement or the civilization back. Append only: the front end maps each code to its text.</summary>
public enum DiagCode : ushort
{
    /// <summary>Food met less than 70% of need last season. A = security permille, B = people.</summary>
    Starving = 1,
    /// <summary>Food met 70–90% of need. A = security permille, B = people.</summary>
    FoodShort = 2,
    /// <summary>The autumn forecast for late winter is poor. A = forecast permille.</summary>
    WinterRisk = 3,
    /// <summary>Stores cover less than a season and fell last season. A = stores, B = need, C = net change (hundredths of a ration).</summary>
    StoresRunningOut = 4,
    /// <summary>Food was lost to full stores, or stores sit near the cap. A = lost, B = stores, C = capacity (hundredths).</summary>
    StoresFull = 5,
    /// <summary>A large share of what was gathered spoiled. A = spoiled (hundredths), B = percent of gathered.</summary>
    Spoilage = 6,
    /// <summary>Births slow because the land feeds few more. A = people, B = food cap, C = births percent of normal.</summary>
    CrowdingFood = 7,
    /// <summary>Births slow because housing is nearly full. A = people, B = housing cap, C = births percent of normal.</summary>
    CrowdingHousing = 8,
    /// <summary>A daughter band will leave soon. A = people, B = group cap.</summary>
    SplitComing = 9,
    /// <summary>Wood for the fires will not last a season. A = wood, B = fuel per season (hundredths).</summary>
    FuelLow = 10,
    /// <summary>The settlement's fire is out.</summary>
    FireOut = 11,
    /// <summary>Workers lack the tool for the best method they know. A = <see cref="FigureActivity"/>, B = workers lacking it, C = workers.</summary>
    ToolsMissing = 12,
    /// <summary>Hunters are too few for the party method. A = hunters affected.</summary>
    HuntParty = 13,
    /// <summary>Metal tools are blocked. A = <see cref="MethodFallbackReason"/>, B = workers affected.</summary>
    MetalAccess = 14,
    /// <summary>People were cold. A = people, B = settlement size.</summary>
    Cold = 15,
    /// <summary>Many people are sick or wounded. A = ill, B = wounded.</summary>
    Illness = 16,
    /// <summary>Cohesion is strained or fraying. A = distance penalty, B = span penalty, C = <see cref="CohesionBand"/>.</summary>
    CohesionLow = 17,
    /// <summary>No research is under way (civilization-wide).</summary>
    ResearchIdle = 18,
    /// <summary>Scouts are away while food is short. A = scouts.</summary>
    ScoutsWhileHungry = 19,
    /// <summary>The Mandate is full and unspent (civilization-wide). A = Mandate.</summary>
    MandateFull = 20,
    /// <summary>Builders cannot pay for a dwelling or store. A = structure kind | (good slot + 1) &lt;&lt; 8, B = in store, C = needed (hundredths).</summary>
    BuildWaiting = 21,
    /// <summary>Nearly all hands went to food. A = food-share rule (1 hunger, 2 low stores), B = percent of plannable work on food.</summary>
    FoodLaborSqueeze = 22,
    /// <summary>Work could not be used for want of tools. A = action (0 forage, 1 hunt, 2 fish, 3 farm, 4 chop), B = work-seasons lost (hundredths), C = assigned.</summary>
    ToolLimitedWork = 23,
    /// <summary>An action found much less than it asked for. A = action, B = taken, C = asked for (hundredths of a unit).</summary>
    LandLimited = 24,
}

public enum DiagSeverity : byte { Info = 0, Warning = 1, Critical = 2 }

/// <summary>One finding: the code, how bad, where (economy settlement index, -1 for the civilization) and its numbers.</summary>
public struct DiagnosisEntry
{
    public DiagCode Code;
    public DiagSeverity Severity;
    public int Settlement;
    public int A, B, C;
    /// <summary>Severity first, then the share of the people it touches.</summary>
    public int Score;
}

/// <summary>
/// Turns a frame into a ranked list of what slows the viewer's civilization (the Overview tab). Pure: it reads the
/// frame's own-settlement economy block and the figures, writes nothing back, and keeps scratch space so a call allocates
/// nothing after the first.
/// </summary>
public sealed class DiagnosisEngine
{
    private int[] _cold = Array.Empty<int>(), _ill = Array.Empty<int>(), _wounded = Array.Empty<int>(), _scouts = Array.Empty<int>();
    private int[] _huntParty = Array.Empty<int>();
    private int[] _noTin = Array.Empty<int>(), _noArsenic = Array.Empty<int>(), _noOre = Array.Empty<int>(), _noFuel = Array.Empty<int>();
    private int[] _workers = Array.Empty<int>(), _missingTool = Array.Empty<int>();
    private const int Activities = 23;
    private static readonly int WoodSlot = SlotOf("G-WOOD");

    private static int SlotOf(string id)
    {
        for (int slot = 0; slot < LivingEconomyModel.StoreSlotCount; slot++)
            if (LivingEconomyModel.StoreSlotId(slot) == id) return slot;
        return -1;
    }

    /// <summary>Hundredths of a unit from a raw Q32.32 value.</summary>
    public static int Hundredths(long raw)
    {
        long whole = raw >> 32, fraction = raw & 0xFFFFFFFFL;
        return (int)Math.Clamp(whole * 100 + ((fraction * 100) >> 32), int.MinValue, int.MaxValue);
    }

    /// <summary>Whole people or whole units from a raw Q32.32 value.</summary>
    public static int Whole(long raw) => (int)Math.Clamp(raw >> 32, int.MinValue, int.MaxValue);

    /// <summary>Fills <paramref name="into"/> with findings, most serious first; returns how many.</summary>
    public int Evaluate(MatchFrame frame, Span<DiagnosisEntry> into)
    {
        int count = 0;
        int rows = frame.SettlementCount;
        if (_cold.Length < rows)
        {
            int size = Math.Max(rows, 32);
            _cold = new int[size]; _ill = new int[size]; _wounded = new int[size]; _scouts = new int[size]; _huntParty = new int[size];
            _noTin = new int[size]; _noArsenic = new int[size]; _noOre = new int[size]; _noFuel = new int[size];
            _workers = new int[size * Activities]; _missingTool = new int[size * Activities];
        }
        Array.Clear(_cold, 0, rows); Array.Clear(_ill, 0, rows); Array.Clear(_wounded, 0, rows); Array.Clear(_scouts, 0, rows);
        Array.Clear(_huntParty, 0, rows); Array.Clear(_noTin, 0, rows); Array.Clear(_noArsenic, 0, rows);
        Array.Clear(_noOre, 0, rows); Array.Clear(_noFuel, 0, rows);
        Array.Clear(_workers, 0, rows * Activities); Array.Clear(_missingTool, 0, rows * Activities);

        int civ = frame.FocusCiv;
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureCiv[i] != civ) continue;
            FigureFlags flags = (FigureFlags)frame.FigureFlags[i];
            if ((flags & FigureFlags.FreeFolk) != 0) continue;
            int row = frame.IndexOfSettlement(frame.FigureSettlement[i]);
            if (row < 0) continue;
            if ((flags & FigureFlags.Cold) != 0) _cold[row]++;
            if ((flags & FigureFlags.Ill) != 0) _ill[row]++;
            if ((flags & FigureFlags.Wounded) != 0) _wounded[row]++;
            int activity = frame.FigureActivity[i];
            if (activity == (int)FigureActivity.Scout) _scouts[row]++;
            if ((uint)activity < Activities && activity != (int)FigureActivity.Child) _workers[row * Activities + activity]++;
            switch ((MethodFallbackReason)frame.FigureMethodFallback[i])
            {
                case MethodFallbackReason.MissingTool:
                    if ((uint)activity < Activities) _missingTool[row * Activities + activity]++;
                    break;
                case MethodFallbackReason.HuntParty: _huntParty[row]++; break;
                case MethodFallbackReason.NoTin: _noTin[row]++; break;
                case MethodFallbackReason.NoArsenic: _noArsenic[row]++; break;
                case MethodFallbackReason.NoOre: _noOre[row]++; break;
                case MethodFallbackReason.NoFuel: _noFuel[row]++; break;
            }
        }

        int civPopulation = 0;
        for (int w = 0; w < rows; w++)
            if (IsRow(frame, w)) civPopulation += frame.SettlementPopulation[w];
        civPopulation = Math.Max(1, civPopulation);

        for (int w = 0; w < rows; w++)
        {
            if (!IsRow(frame, w)) continue;
            int s = frame.SettlementIndex[w];
            int pop = frame.SettlementPopulation[w];
            if (pop <= 0) continue;
            int econ = w * EconField.Stride, ledger = econ + EconField.Ledger;
            long[] e = frame.SettlementEcon;
            bool ledgerValid = e[econ + EconField.LedgerValid] != 0;
            int fs = (int)e[econ + EconField.FoodSecurityPermille];
            int stores = Hundredths(frame.SettlementFoodStores[w]);
            int need = Hundredths(e[econ + EconField.FoodNeed]);
            int capacity = Hundredths(e[econ + EconField.FoodCapacity]);
            int gathered = Hundredths(e[ledger + EconField.LedgerGathered]);
            int spoiled = Hundredths(e[ledger + EconField.LedgerSpoiled]);
            int lost = Hundredths(e[ledger + EconField.LedgerLostToCap]);
            int start = Hundredths(e[ledger + EconField.LedgerStart]);
            int end = Hundredths(e[ledger + EconField.LedgerEnd]);
            SettlementViewFlags sf = (SettlementViewFlags)frame.SettlementFlags[w];
            bool winter = frame.SeasonOfYear == 3;

            if (ledgerValid && fs < 700) Add(into, ref count, DiagCode.Starving, DiagSeverity.Critical, s, fs, pop, 0, pop, civPopulation);
            else if (ledgerValid && fs < 900) Add(into, ref count, DiagCode.FoodShort, DiagSeverity.Warning, s, fs, pop, 0, pop, civPopulation);

            if (frame.SeasonOfYear is 2 or 3)
            {
                int forecast = (int)e[econ + EconField.ForecastPermille];
                if (forecast < 700) Add(into, ref count, DiagCode.WinterRisk, DiagSeverity.Critical, s, forecast, 0, 0, pop, civPopulation);
                else if (forecast < 900) Add(into, ref count, DiagCode.WinterRisk, DiagSeverity.Warning, s, forecast, 0, 0, pop, civPopulation);
            }

            if (ledgerValid && need > 0 && stores < need && end < start)
                Add(into, ref count, DiagCode.StoresRunningOut, stores * 2 < need ? DiagSeverity.Critical : DiagSeverity.Warning,
                    s, stores, need, end - start, pop, civPopulation);

            if (ledgerValid && (lost >= 50 || capacity > 0 && stores * 100 >= capacity * 95))
                Add(into, ref count, DiagCode.StoresFull, lost >= 100 ? DiagSeverity.Warning : DiagSeverity.Info,
                    s, lost, stores, capacity, 0, civPopulation);

            if (ledgerValid && spoiled >= 200 && gathered > 0 && spoiled * 100 >= gathered * 25)
            {
                int percent = spoiled * 100 / gathered;
                Add(into, ref count, DiagCode.Spoilage, percent >= 40 ? DiagSeverity.Warning : DiagSeverity.Info,
                    s, spoiled, percent, 0, 0, civPopulation);
            }

            int foodCap = Whole(e[econ + EconField.FoodCap]), housingCap = Whole(e[econ + EconField.HousingCap]);
            int groupCap = Whole(e[econ + EconField.GroupCap]);
            int cap = Math.Min(foodCap, housingCap);
            if (cap > 0 && pop * 100 >= cap * 85)
            {
                long births = (long)fs * e[econ + EconField.HealthPermille] / 1000 * e[econ + EconField.CrowdingFreePermille] / 1000;
                int birthPercent = (int)Math.Clamp(births / 10, 0, 100);
                bool food = foodCap <= housingCap;
                Add(into, ref count, food ? DiagCode.CrowdingFood : DiagCode.CrowdingHousing,
                    birthPercent < 50 ? DiagSeverity.Warning : DiagSeverity.Info, s, pop, cap, birthPercent, pop, civPopulation);
            }

            if ((sf & SettlementViewFlags.FissionPending) != 0 || groupCap > 0 && pop * 100 >= groupCap * 85)
                Add(into, ref count, DiagCode.SplitComing, DiagSeverity.Info, s, pop, groupCap, 0, 0, civPopulation);

            int fuel = Hundredths(e[econ + EconField.FuelPerSeason]);
            if ((sf & SettlementViewFlags.FireLit) == 0)
                Add(into, ref count, DiagCode.FireOut, winter ? DiagSeverity.Critical : DiagSeverity.Warning, s, 0, 0, 0, pop, civPopulation);
            else if (fuel > 0 && WoodSlot >= 0)
            {
                int wood = Hundredths(frame.SettlementStores[w * MatchFrame.StoreSlots + WoodSlot]);
                if (wood < fuel) Add(into, ref count, DiagCode.FuelLow, DiagSeverity.Warning, s, wood, fuel, 0, pop, civPopulation);
            }

            for (int a = 0; a < Activities; a++)
            {
                int total = _workers[w * Activities + a], missing = _missingTool[w * Activities + a];
                if (missing >= 2 && missing * 4 >= total)
                    Add(into, ref count, DiagCode.ToolsMissing, DiagSeverity.Warning, s, a, missing, total, missing, civPopulation);
            }
            if (_huntParty[w] > 0) Add(into, ref count, DiagCode.HuntParty, DiagSeverity.Info, s, _huntParty[w], 0, 0, 0, civPopulation);
            AddMetal(into, ref count, s, MethodFallbackReason.NoTin, _noTin[w], civPopulation);
            AddMetal(into, ref count, s, MethodFallbackReason.NoArsenic, _noArsenic[w], civPopulation);
            AddMetal(into, ref count, s, MethodFallbackReason.NoOre, _noOre[w], civPopulation);
            AddMetal(into, ref count, s, MethodFallbackReason.NoFuel, _noFuel[w], civPopulation);

            if (_cold[w] >= 2 && _cold[w] * 10 >= pop)
                Add(into, ref count, DiagCode.Cold, winter && _cold[w] * 10 >= pop * 3 ? DiagSeverity.Critical : DiagSeverity.Warning,
                    s, _cold[w], pop, 0, _cold[w], civPopulation);

            int sick = _ill[w] + _wounded[w];
            if (sick >= 3 && sick * 100 >= pop * 15)
                Add(into, ref count, DiagCode.Illness, DiagSeverity.Warning, s, _ill[w], _wounded[w], 0, sick, civPopulation);

            int band = frame.SettlementCohesion[w];
            if (band >= (int)CohesionBand.Strained)
                Add(into, ref count, DiagCode.CohesionLow, band >= (int)CohesionBand.Fraying ? DiagSeverity.Critical : DiagSeverity.Warning,
                    s, frame.SettlementOverreach[w], frame.SettlementSpanPenalty[w], band, pop, civPopulation);

            if (e[econ + EconField.BlockerStructure] >= 0)
                Add(into, ref count, DiagCode.BuildWaiting, DiagSeverity.Warning, s,
                    (int)e[econ + EconField.BlockerStructure] | (int)(e[econ + EconField.BlockerSlot] + 1) << 8,
                    Hundredths(e[econ + EconField.BlockerHave]), Hundredths(e[econ + EconField.BlockerNeed]), 0, civPopulation);

            int rule = (int)(e[econ + EconField.Labor + LivingEconomyModel.LaborRule] >> 32);
            long plannable = e[econ + EconField.Labor + LivingEconomyModel.LaborCapacity] - e[econ + EconField.Labor + LivingEconomyModel.LaborReserved];
            long foodWork = e[econ + EconField.Labor + LivingEconomyModel.LaborFood];
            if (rule is 1 or 2 && plannable > 0 && foodWork * 100 >= plannable * 80)
                Add(into, ref count, DiagCode.FoodLaborSqueeze, DiagSeverity.Info, s, rule, (int)(foodWork * 100 / plannable), 0, 0, civPopulation);

            for (int k = 0; k < LivingEconomyModel.ActionLedgerKinds; k++)
            {
                int line = econ + EconField.Actions + k * LivingEconomyModel.ActionLedgerFields;
                int assigned = Hundredths(e[line + LivingEconomyModel.ActionAssigned]), applied = Hundredths(e[line + LivingEconomyModel.ActionApplied]);
                if (assigned - applied >= 100)
                    Add(into, ref count, DiagCode.ToolLimitedWork, DiagSeverity.Warning, s, k, assigned - applied, assigned, 0, civPopulation);
                int requested = Hundredths(e[line + LivingEconomyModel.ActionRequested]), harvested = Hundredths(e[line + LivingEconomyModel.ActionHarvested]);
                if (requested - harvested >= 200 && harvested * 100 < requested * 80)
                    Add(into, ref count, DiagCode.LandLimited, DiagSeverity.Warning, s, k, harvested, requested, 0, civPopulation);
            }

            ScoutPolicy policy = (ScoutPolicy)frame.SettlementScoutPolicy[w];
            if (_scouts[w] > 0 && fs < 900 && policy is ScoutPolicy.One or ScoutPolicy.Two)
                Add(into, ref count, DiagCode.ScoutsWhileHungry, DiagSeverity.Warning, s, _scouts[w], 0, 0, _scouts[w], civPopulation);
        }

        bool anyOwn = false;
        for (int w = 0; w < rows; w++) if (IsRow(frame, w)) { anyOwn = true; break; }
        if (anyOwn && frame.FocusSlotProject[0] < 0 && frame.FocusSlotProject[1] < 0 && frame.FocusSlotProject[2] < 0 &&
            frame.FocusQueueCount == 0 && !frame.FocusCouncil && frame.Season > 0)
            Add(into, ref count, DiagCode.ResearchIdle, DiagSeverity.Critical, -1, 0, 0, 0, 0, civPopulation);
        if (frame.FocusMandateUnlocked && frame.FocusMandateCap > 0 && frame.FocusMandate >= frame.FocusMandateCap)
            Add(into, ref count, DiagCode.MandateFull, DiagSeverity.Info, -1, frame.FocusMandate, 0, 0, 0, civPopulation);
        return count;
    }

    private static bool IsRow(MatchFrame frame, int w)
    {
        SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[w];
        return (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own &&
            frame.SettlementEcon[w * EconField.Stride + EconField.Valid] != 0;
    }

    private static void AddMetal(Span<DiagnosisEntry> into, ref int count, int settlement, MethodFallbackReason reason, int workers, int civPopulation)
    {
        if (workers > 0)
            Add(into, ref count, DiagCode.MetalAccess, DiagSeverity.Warning, settlement, (int)reason, workers, 0, workers, civPopulation);
    }

    private static void Add(Span<DiagnosisEntry> into, ref int count, DiagCode code, DiagSeverity severity, int settlement,
        int a, int b, int c, int affected, int civPopulation)
    {
        int impact = Math.Min(1000, affected * 1000 / civPopulation);
        var entry = new DiagnosisEntry
        {
            Code = code, Severity = severity, Settlement = settlement, A = a, B = b, C = c,
            Score = (int)severity * 100_000 + impact,
        };
        // Insertion keeps the list sorted: score descending, then settlement and code ascending, so the order is stable.
        int at = count;
        while (at > 0 && Before(entry, into[at - 1])) at--;
        if (at >= into.Length) return;
        int last = Math.Min(count, into.Length - 1);
        for (int i = last; i > at; i--) into[i] = into[i - 1];
        into[at] = entry;
        if (count < into.Length) count++;
    }

    private static bool Before(in DiagnosisEntry a, in DiagnosisEntry b)
        => a.Score != b.Score ? a.Score > b.Score : a.Settlement != b.Settlement ? a.Settlement < b.Settlement : a.Code < b.Code;
}
