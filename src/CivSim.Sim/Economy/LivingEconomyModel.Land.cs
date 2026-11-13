using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// Faith &amp; Fief field rotation (docs/MVP/27 §5.3, M12 WP-E5-5). The E5-era farm has a two-field schedule: one of every two field strips is
/// cropped in a year. AGR-11 changes that to two of every three and adds the provisional legume yield. The schedule is derived from field order,
/// year and completed research, so it needs no additional snapshot or hash state. It is inert outside Fief and before that civilization's KEY-5.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private static readonly Fix64 LegumeRotationYield = Fix64.FromRatio(9, 8);

    private bool FiefFieldPolicy(SettlementState s) => FiefChain && HasProject(s.Civilization, "KEY-5");

    /// <summary>Share of field strips cropped in a year by the Fief two-field baseline or AGR-11's three-field rotation.</summary>
    private Fix64 FiefFieldCropShare(SettlementState s)
        => !FiefFieldPolicy(s) ? Fix64.One : HasProject(s.Civilization, "AGR-11") ? Fix64.FromRatio(2, 3) : Fix64.Half;

    private bool FiefFieldCropped(SettlementState s, int fieldIndex, int year)
    {
        if (!FiefFieldPolicy(s)) return true;
        int cycle = HasProject(s.Civilization, "AGR-11") ? 3 : 2;
        int phase = ((fieldIndex + year) % cycle + cycle) % cycle;
        return phase < cycle - 1;
    }

    private Fix64 FiefFieldYield(SettlementState s)
        => FiefFieldPolicy(s) && HasProject(s.Civilization, "AGR-11") ? LegumeRotationYield : Fix64.One;

    // Test fixtures intentionally use the same production path as the live harvest.
    internal int FixtureCroppedFields(int settlementIndex, int year)
    {
        SettlementState s = _settlements[settlementIndex];
        int cropped = 0;
        for (int i = 0; i < s.FieldTiles.Count; i++)
            if (FiefFieldCropped(s, i, year)) cropped++;
        return cropped;
    }

    internal Fix64 FixtureFiefFieldYield(int settlementIndex) => FiefFieldYield(_settlements[settlementIndex]);

    internal Fix64 FixtureHarvestFieldYear(int settlementIndex, int year)
    {
        SettlementState s = _settlements[settlementIndex];
        s.FarmWorkThisYear = FieldLaborNeed(s);
        Fix64 before = s.Stores[(int)Good.Grain];
        HarvestFields(s, year);
        return s.Stores[(int)Good.Grain] - before;
    }
}
