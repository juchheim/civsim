using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Faith &amp; Fief field rotation (docs/MVP/27 §5.3, M12 WP-E5-5). The E5-era farm has a two-field schedule: one of every two field strips is
/// cropped in a year. AGR-11 changes that to two of every three and adds the provisional legume yield. The schedule is derived from field order,
/// year and completed research, so it needs no additional snapshot or hash state. It is inert outside Fief and before that civilization's KEY-5.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private static readonly Fix64 LegumeRotationYield = Fix64.FromRatio(9, 8);
    private static readonly Fix64 SelectiveBreedingYield = Fix64.FromRatio(3, 2);
    private static readonly Fix64 SelectiveBreedingDraft = Fix64.FromRatio(6, 5);
    private static readonly Fix64 ClaimUpkeepRate = Fix64.FromRatio(1, 10);

    private bool FiefE5Policy(SettlementState s) => FiefChain && HasProject(s.Civilization, "KEY-5");

    private bool FiefFieldPolicy(SettlementState s) => FiefE5Policy(s);

    /// <summary>
    /// AGR-12's four-course rotation (docs/MVP/38 §5.5, M13 WP-E6-8 slice 2), Discovery only: wheat, turnips, barley, clover. Fallow is gone, but only half the strips bear grain
    /// in a year (the other half are fodder and legume), so each grain strip yields 9/5 of a strip: an average of 0.9 grain per strip-year against the three-field's 0.75, the
    /// row's field_output x1.2. Enclosure cuts the hands the fields need to 0.85 (see <c>FieldLaborNeed</c>); the adults it frees go to other work, they are not erased.
    /// </summary>
    private bool FourCourse(SettlementState s) => DiscoveryChain && FiefFieldPolicy(s) && HasProject(s.Civilization, "AGR-12");

    private static readonly Fix64 FourCourseGrainYield = Fix64.FromRatio(9, 5);
    private static readonly Fix64 FourCourseLabor = Fix64.FromRatio(85, 100);

    /// <summary>Share of field strips cropped in a year by the Fief two-field baseline, AGR-11's three-field rotation or AGR-12's four-course.</summary>
    private Fix64 FiefFieldCropShare(SettlementState s)
        => !FiefFieldPolicy(s) ? Fix64.One : FourCourse(s) ? Fix64.Half : HasProject(s.Civilization, "AGR-11") ? Fix64.FromRatio(2, 3) : Fix64.Half;

    private bool FiefFieldCropped(SettlementState s, int fieldIndex, int year)
    {
        if (!FiefFieldPolicy(s)) return true;
        if (FourCourse(s)) return ((fieldIndex + year) % 4 + 4) % 4 % 2 == 0;
        int cycle = HasProject(s.Civilization, "AGR-11") ? 3 : 2;
        int phase = ((fieldIndex + year) % cycle + cycle) % cycle;
        return phase < cycle - 1;
    }

    private Fix64 FiefFieldYield(SettlementState s)
        => (FourCourse(s) ? FourCourseGrainYield : FiefFieldPolicy(s) && HasProject(s.Civilization, "AGR-11") ? LegumeRotationYield : Fix64.One) * ExchangeCropYield(s);

    private bool FiefHorseCollar(SettlementState s)
        => FiefFieldPolicy(s) && HasProject(s.Civilization, "NRG-04") && s.Civic is { Horses: > 0 };

    private Fix64 FiefHerdYield(SettlementState s)
        => FiefE5Policy(s) && HasProject(s.Civilization, "HUS-10") ? SelectiveBreedingYield : Fix64.One;

    private Fix64 FiefDraftStrength(SettlementState s)
        => FiefE5Policy(s) && HasProject(s.Civilization, "HUS-10") ? SelectiveBreedingDraft : Fix64.One;

    private Fix64 FiefClaimUpkeep(SettlementState s, Fix64 claimedFieldYield)
    {
        if (!FiefE5Policy(s) || claimedFieldYield <= Fix64.Zero || TileOwnership is null) return Fix64.Zero;
        Fix64 rate = ClaimUpkeepRate * Modifier(s.Civilization, "claim_upkeep_farm");
        Fix64 due = claimedFieldYield * rate;
        if (due <= Fix64.Zero) return Fix64.Zero;

        SettlementState? receiver = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState candidate = _settlements[i];
            if (candidate.Id == s.Id || !IsMember(candidate, s.Civilization) || candidate.Civic is not { Tier: >= 1 } ||
                !HasNonFarmingSeatStaff(candidate)) continue;
            int distance = HexGrid.Distance(s.SiteTile, candidate.SiteTile, _world.Width);
            if (distance >= bestDistance) continue;
            receiver = candidate;
            bestDistance = distance;
        }
        if (receiver is null) return Fix64.Zero;

        Fix64 moved = Fix64.Min(due, Fix64.Max(Fix64.Zero, s.Stores[(int)Good.Grain]));
        if (moved <= Fix64.Zero) return Fix64.Zero;
        s.Stores[(int)Good.Grain] -= moved;
        receiver.Stores[(int)Good.Grain] += moved;
        if (receiver.Id != s.Id) RecordSupplyTransfer(s.Id, receiver.Id, "G-GRAIN", moved);
        return moved;
    }

    /// <summary>
    /// Towns and cities with at least one working-age resident represent a staffed seat; the economy pools wages and food in a settlement store
    /// rather than maintaining a separate inventory for each occupation.
    /// </summary>
    private bool HasNonFarmingSeatStaff(SettlementState s)
    {
        if (Population(s.Id) <= 0) return false;
        foreach (FigureState figure in Residents(s.Id))
            if (figure.Age >= 14 && WorkCapacity(figure) > Fix64.Zero) return true;
        return false;
    }

    /// <summary>Fief's reserve is the game occupying a tile owned by a KEY-5 civilization with HNT-11.</summary>
    private int FiefGameReserveOwner(int tile)
    {
        if (!FiefChain || TileOwnership is not { } ownership) return -1;
        int owner = ownership.OwnerOf(tile);
        return owner >= 0 && HasProject(owner, "KEY-5") && HasProject(owner, "HNT-11") ? owner : -1;
    }

    private Fix64 WildHerdCarryingCapacity(WildHerdState herd)
    {
        WorldHerd source = _world.Herd(herd.Id);
        Fix64 growthRate = Fix64.FromRatio(source.AnnualGrowthPercent, 100);
        if (_wildfireSeasonsRemaining[herd.CurrentTile] > 0) growthRate *= Fix64.Parse("1.3");
        return Fix64.FromInt(source.InitialAnimalCount) * (Fix64.One + growthRate);
    }

    /// <summary>Wild animals a civilization may remove; reserve tiles are exclusive and preserve half of local carrying capacity.</summary>
    private Fix64 FiefWildHarvestable(WildHerdState herd, int civilization)
    {
        int owner = FiefGameReserveOwner(herd.CurrentTile);
        if (owner < 0) return herd.CurrentAnimals;
        if (owner != civilization) return Fix64.Zero;
        return Fix64.Max(Fix64.Zero, herd.CurrentAnimals - WildHerdCarryingCapacity(herd) * Fix64.Half);
    }

    private bool FiefReserveHuntReady(WildHerdState herd, int civilization, int huntersAssigned)
        => FiefGameReserveOwner(herd.CurrentTile) != civilization || huntersAssigned >= 2;

    /// <summary>A settlement's active reserve contributes HNT-11's local cohesion term while game occupies claimed ground nearby.</summary>
    private bool FiefSettlementHasReserve(SettlementState s)
    {
        if (!FiefE5Policy(s) || !HasProject(s.Civilization, "HNT-11")) return false;
        int reach = ClaimRadius(s.Id, 5);
        foreach (WildHerdState herd in _wildHerds)
            if (FiefGameReserveOwner(herd.CurrentTile) == s.Civilization &&
                HexGrid.Distance(s.SiteTile, herd.CurrentTile, _world.Width) <= reach) return true;
        return false;
    }

    /// <summary>NRG-04 doubles draft power; provisionally, horse-held Fief towns pay half labor for modeled hauling.</summary>
    private Fix64 FiefHorseHaulFactor(SettlementState s) => FiefHorseCollar(s) ? Fix64.Half : Fix64.One;

    /// <summary>Tests only: the share of strips cropped in a year and the yield of each cropped strip (the rotation's two numbers).</summary>
    internal (Fix64 CropShare, Fix64 StripYield) FixtureFieldPlan(int settlementIndex)
        => (FiefFieldCropShare(_settlements[settlementIndex]), FiefFieldYield(_settlements[settlementIndex]));

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

    internal Fix64 FixtureFiefHerdYield(int settlementIndex) => FiefHerdYield(_settlements[settlementIndex]);

    internal Fix64 FixtureFiefDraftStrength(int settlementIndex) => FiefDraftStrength(_settlements[settlementIndex]);

    internal Fix64 FixtureClaimUpkeep(int settlementIndex, Fix64 claimedFieldYield)
        => FiefClaimUpkeep(_settlements[settlementIndex], claimedFieldYield);

    internal bool FixtureSettlementHasReserve(int settlementIndex) => FiefSettlementHasReserve(_settlements[settlementIndex]);

    internal Fix64 FixtureWildHerdCarryingCapacity(int herdIndex) => WildHerdCarryingCapacity(_wildHerds[herdIndex]);

    internal void FixtureSetTameHorses(int settlementIndex, int horses)
    {
        if (horses < 0) throw new ArgumentOutOfRangeException(nameof(horses));
        EnsureCivic(_settlements[settlementIndex]).Horses = horses;
    }

    internal Fix64 FixtureHarvestFieldYear(int settlementIndex, int year)
    {
        SettlementState s = _settlements[settlementIndex];
        s.FarmWorkThisYear = FieldLaborNeed(s);
        Fix64 before = s.Stores[(int)Good.Grain];
        HarvestFields(s, year);
        return s.Stores[(int)Good.Grain] - before;
    }
}
