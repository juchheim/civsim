using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// Steers where daughter bands settle (docs/MVP/05 §9.4, §11.4): rival-owned land is off limits and Beacons make
/// nearby sites more attractive. Politics implements it; without one the economy chooses on food alone.
/// </summary>
public interface IFissionSiteAdvisor
{
    bool Allows(int civ, int tile);
    /// <summary>Multiplier on the site's food score, 1 when neutral.</summary>
    Fix64 Multiplier(int civ, int tile);
    /// <summary>The multiplier with a hypothetical Beacon placement, optionally replacing one existing Beacon.</summary>
    Fix64 PreviewMultiplier(int civ, int tile, int beaconTile, int replacedBeaconIndex);
    /// <summary>The farthest (hex distance) a daughter band may settle from its parent: on foot, or by boat once the civilization has one (docs/MVP/21).</summary>
    int SettleReach(int civ);
    /// <summary>Whether the band can actually get from the parent's village to the site.</summary>
    bool Reachable(int civ, int fromTile, int toTile);
    /// <summary>True if the band's voyage turns back (shipwreck); it then stays home this season.</summary>
    bool VoyageLost(int civ, int fromTile, int toTile, int season);
}

/// <summary>A read-only estimate of where a newly placed Beacon would rank as a daughter-band settlement site.</summary>
public readonly record struct BeaconSitePreview(bool CanFound, Fix64 RelativeSiteFit, int PendingSplits, int PendingSplitsChoosingTile);

/// <summary>What conflict and trade take from or add to a settlement's goods (docs/MVP/13 §4, §5.4).</summary>
public enum LootPreference { PreservedFoodThenToolsThenFresh, FoodOnly }

/// <summary>
/// The few ways conflict, trade and diplomacy (M5) reach into the living economy. Everything here is called
/// between ticks, never from inside a season update, and keeps the economy's own invariants.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private static readonly Good[] PreservedFirst = { Good.DriedFood };
    private const int ToolValue = 3;
    private readonly Fix64[] _fissionThresholdScale = InitialScale();

    private static Fix64[] InitialScale()
    {
        var scale = new Fix64[8];
        Array.Fill(scale, Fix64.One);
        return scale;
    }

    public IFissionSiteAdvisor? SiteAdvisor { get; set; }

    /// <summary>
    /// Estimates the Beacon tile against legal nearby fission sites. RelativeSiteFit is the target score divided by the strongest
    /// reachable site score within the Beacon's 15-tile influence radius. Pending splits are checked against the full fission chooser.
    /// This is a display-only query and does not change simulation state.
    /// </summary>
    public BeaconSitePreview PreviewBeaconSite(int civ, int beaconTile, int replacedBeaconIndex = -1)
    {
        if ((uint)beaconTile >= (uint)_world.TileCount || !IsHabitable(beaconTile))
            return new BeaconSitePreview(false, Fix64.Zero, 0, 0);

        var parents = new List<SettlementState>();
        int pendingSplits = 0, pendingSplitsChoosingTile = 0;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState parent = _settlements[i];
            if (parent.Civilization != civ || IsFreeFolk(parent) || Population(parent.Id) <= 0) continue;
            parents.Add(parent);
            if (!parent.FissionPending) continue;
            pendingSplits++;
            if (ChooseFissionSite(parent, checked((int)_seasonsCompleted), beaconTile, replacedBeaconIndex, recordDiagnostics: false) == beaconTile)
                pendingSplitsChoosingTile++;
        }
        if (parents.Count == 0) return new BeaconSitePreview(false, Fix64.Zero, pendingSplits, pendingSplitsChoosingTile);

        var localCandidates = new List<(int Tile, Fix64 Score)>(768);
        for (int tile = 0; tile < _world.TileCount; tile++)
        {
            if (HexGrid.Distance(beaconTile, tile, _world.Width) > 15 || !IsHabitable(tile)) continue;
            bool farEnough = true;
            for (int s = 0; s < _settlements.Count; s++)
                if (HexGrid.Distance(tile, _settlements[s].SiteTile, _world.Width) < Radius * 2 + 1)
                { farEnough = false; break; }
            if (!farEnough || SiteAdvisor is not null && !SiteAdvisor.Allows(civ, tile)) continue;
            Fix64 score = _siteFoodScores[tile] * (SiteAdvisor is null
                ? Fix64.One
                : SiteAdvisor.PreviewMultiplier(civ, tile, beaconTile, replacedBeaconIndex)) * IronFuelSitingMultiplier(civ, tile);
            if (score >= Fix64.Zero) localCandidates.Add((tile, score));
        }

        bool canFound = false;
        Fix64 bestFit = Fix64.Zero;
        foreach (SettlementState parent in parents)
        {
            int reach = Math.Max(14, SiteAdvisor?.SettleReach(civ) ?? 14);
            Fix64 best = Fix64.Zero, target = Fix64.Zero;
            bool targetReachable = false;
            foreach ((int tile, Fix64 score) in localCandidates)
            {
                int distance = HexGrid.Distance(parent.SiteTile, tile, _world.Width);
                if (distance < Radius * 2 + 1 || distance > reach ||
                    SiteAdvisor is not null && !SiteAdvisor.Reachable(civ, parent.SiteTile, tile)) continue;
                if (score > best) best = score;
                if (tile == beaconTile) { target = score; targetReachable = true; }
            }
            if (!targetReachable) continue;
            canFound = true;
            Fix64 fit = best > Fix64.Zero ? Fix64.Clamp(target / best, Fix64.Zero, Fix64.One) : Fix64.Zero;
            if (fit > bestFit) bestFit = fit;
        }

        return new BeaconSitePreview(canFound, bestFit, pendingSplits, pendingSplitsChoosingTile);
    }

    // Diagnostic-only sink: deliberately excluded from state hashes, snapshots and serialization.
    internal FissionSiteDiagnostics? FissionSiteDiagnosticsForDiagnostics { get; set; }

    public Fix64 FissionScaleOf(int civ) => FissionThresholdScale(civ);

    public Fix64 FissionScaleOf(int civ, int settlementId)
    {
        if ((uint)civ >= (uint)_completedProjects.Length) throw new ArgumentOutOfRangeException(nameof(civ));
        if (CurrentEra(civ) < 3 && PreE3FissionThresholdScaleOverridesForDiagnostics is { } overrides &&
            civ < overrides.Length && overrides[civ] is { } diagnosticOverride &&
            (!PreE3FissionThresholdOverrideInitialSettlementsOnlyForDiagnostics || settlementId == civ) &&
            (!PreE3FissionThresholdOverrideFirstGenerationDaughtersOnlyForDiagnostics ||
                _firstGenerationDaughterSettlementIdsForDiagnostics.Contains(settlementId)))
            return diagnosticOverride;
        return FissionThresholdScale(civ);
    }



    private Fix64 FissionThresholdScale(int civ) => civ < _fissionThresholdScale.Length ? _fissionThresholdScale[civ] : Fix64.One;

    /// <summary>Posture Expand lowers the fission threshold 20%, Prosper raises it 20% (docs/MVP/05 §6.2).</summary>
    public void SetFissionThresholdScale(int civ, Fix64 scale)
    {
        if ((uint)civ >= (uint)_fissionThresholdScale.Length) throw new ArgumentOutOfRangeException(nameof(civ));
        _fissionThresholdScale[civ] = scale;
    }

    /// <summary>
    /// The settlement fields that are stored rather than derived. <see cref="Settlement"/> builds a full view, which
    /// recomputes food, housing and group caps; callers that only need identity, place or the last season's food
    /// security should use this (it is about 5,000 times cheaper).
    /// </summary>
    public (int Id, int Civilization, int SiteTile, Fix64 LastFoodSecurity, Fix64 LateWinterFoodForecast, int Fissions) SettlementLite(int index)
    {
        SettlementState s = _settlements[index];
        return (s.Id, s.Civilization, s.SiteTile, s.FoodSecurity, s.LateWinterForecast, s.Fissions);
    }

    public Fix64 SettlementCohesion(int settlementIndex) => _settlements[settlementIndex].Cohesion;

    /// <summary>Changes a settlement's Cohesion factor within its 0.6 to 1.2 range.</summary>
    public void AdjustCohesion(int settlementIndex, Fix64 delta)
    {
        SettlementState s = _settlements[settlementIndex];
        s.Cohesion = Fix64.Clamp(s.Cohesion + delta, Fix64.Parse("0.6"), Fix64.Parse("1.2"));
    }

    /// <summary>Count of a defensive structure the settlement has: thorn barriers and lookout posts.</summary>
    public int DefenseStructures(int settlementIndex, bool lookout)
        => _settlements[settlementIndex].Structures[(int)(lookout ? Structure.Lookout : Structure.ThornBarrier)];

    /// <summary>
    /// Removes up to <paramref name="maxValue"/> rations' worth of goods and returns what was taken, in rations.
    /// Raiders prefer preserved food, then tools, then fresh food (docs/MVP/13 §5.4); a gift or trade takes food only.
    /// </summary>
    public Fix64 TakeGoods(int settlementIndex, Fix64 maxValue, LootPreference preference)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 taken = Fix64.Zero;
        foreach (Good good in PreservedFirst)
            taken += TakeFood(s, good, maxValue - taken);
        if (preference == LootPreference.PreservedFoodThenToolsThenFresh)
            for (int g = 0; g < s.Stores.Length && taken < maxValue; g++)
            {
                Good tool = (Good)g;
                Fix64 durability = ToolDurability(s, tool);
                if (durability <= Fix64.Zero) continue;
                int units = Math.Min(s.Stores[g].FloorToInt(), ((maxValue - taken) / ToolValue).FloorToInt());
                if (units <= 0) continue;
                s.Stores[g] -= Fix64.FromInt(units);
                s.ToolRemainingWork[g] = Fix64.Max(Fix64.Zero, s.ToolRemainingWork[g] - Fix64.FromInt(units) * durability);
                taken += Fix64.FromInt(units * ToolValue);
            }
        foreach (Good good in FoodGoods)
            if (good != Good.DriedFood) taken += TakeFood(s, good, maxValue - taken);
        return taken;
    }

    private Fix64 TakeFood(SettlementState s, Good good, Fix64 want)
    {
        if (want <= Fix64.Zero || s.Stores[(int)good] <= Fix64.Zero) return Fix64.Zero;
        Fix64 unit = FoodValue(good, s);
        if (unit <= Fix64.Zero) return Fix64.Zero;
        Fix64 quantity = Fix64.Min(s.Stores[(int)good], want / unit);
        s.Stores[(int)good] -= quantity;
        return quantity * unit;
    }

    /// <summary>Adds <paramref name="value"/> rations of preserved food (a gift, the proceeds of trade or the spoils of a raid).</summary>
    public void GiveGoods(int settlementIndex, Fix64 value)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 unit = FoodValue(Good.DriedFood, s);
        if (unit > Fix64.Zero && value > Fix64.Zero) s.Stores[(int)Good.DriedFood] += value / unit;
    }

    /// <summary>
    /// Destroys one flammable structure (tent, lean-to, drying rack, granary, thorn barrier and the like); mudbrick
    /// houses survive once BLD-05 is known (docs/MVP/13 §5.4). Returns false when nothing could burn.
    /// </summary>
    public bool BurnStructure(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        // CER-08: a town roofed in fired brick and tile catches fire half as often. Roofed means a fired-brick store of at least one for every twenty people; the
        // draw is keyed on the settlement, the tick and how much is left to burn, so no new state is kept.
        if (IronChain && Done(s.Civilization, "CER-08") && IronStock(s, IronGood.FiredBrick) >= Fix64.FromInt((Population(s.Id) + 19) / 20))
        {
            int left = 0;
            foreach (int count in s.Structures) left += count;
            if (Pcg32.ForStream(_seed, 0x46495245UL, (ulong)settlementIndex, Tick * 4096 + left).NextBelow(2) == 0) return false;
        }
        Structure[] flammable =
        {
            Structure.HideTent, Structure.LeanTo, Structure.DryingRack, Structure.ThornBarrier, Structure.PitHouse,
            Structure.WattleHouse, Structure.Longhouse, Structure.Granary, Structure.StoragePit, Structure.Lookout,
            Structure.Kiln, Structure.Quern, Structure.Palisade,
        };
        foreach (Structure structure in flammable)
        {
            if (s.Structures[(int)structure] <= 0) continue;
            s.Structures[(int)structure]--;
            return true;
        }
        return false;
    }

    /// <summary>Removes a figure from the world (battle death). Returns false if there is no such figure.</summary>
    public bool KillFigure(long figureId)
    {
        for (int i = 0; i < _figures.Count; i++)
        {
            if (_figures[i].Id != figureId) continue;
            RecordIT6IronKitRetired(_figures[i].Civilization, _figures[i].SettlementId, _figures[i].Kit, Fix64.One);
            _figures.RemoveAt(i);
            return true;
        }
        return false;
    }

    /// <summary>Wounds a figure for the given number of seasons: out of work and out of the fight.</summary>
    public void WoundFigure(long figureId, int seasons)
    {
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id == figureId)
            {
                _figures[i].WoundSeasons = (byte)Math.Clamp(Math.Max(_figures[i].WoundSeasons, seasons), 0, 255);
                return;
            }
    }

    /// <summary>Moves a figure to another settlement (fleeing refugees, assimilated survivors).</summary>
    public void RelocateFigure(long figureId, int settlementIndex)
    {
        SettlementState to = _settlements[settlementIndex];
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id == figureId)
            {
                RecordIT6KitRelocation(_figures[i].SettlementId, to.Id, _figures[i].Kit, Fix64.One);
                _figures[i].SettlementId = to.Id;
                _figures[i].Civilization = to.Civilization;
                _figures[i].Role = 0;
                _figures[i].Camp = -1;
                return;
            }
    }

    /// <summary>
    /// Captures a settlement for another civilization: the settlement, its structures and stores, and the people
    /// who stay all change hands (docs/MVP/13 §8.3). The caller moves the ones who flee first.
    /// </summary>
    public void TransferSettlement(int settlementIndex, int newCiv)
    {
        if ((uint)newCiv >= (uint)_completedProjects.Length) throw new ArgumentOutOfRangeException(nameof(newCiv));
        SettlementState s = _settlements[settlementIndex];
        s.Civilization = newCiv;
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].SettlementId == s.Id) { _figures[i].Civilization = newCiv; _figures[i].Role = 0; }
        if (s.Civic is { } civic) civic.BuildKind = CivicBuild.None;
        s.FissionPending = false;
    }

    /// <summary>Razes a settlement: its people are gone and the buildings are ashes (docs/MVP/13 §8.3). The record stays so ids remain stable.</summary>
    public void RazeSettlement(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        for (int i = _figures.Count - 1; i >= 0; i--)
            if (_figures[i].SettlementId == s.Id)
            {
                RecordIT6IronKitRetired(_figures[i].Civilization, _figures[i].SettlementId, _figures[i].Kit, Fix64.One);
                _figures.RemoveAt(i);
            }
        Array.Clear(s.Structures);
        Array.Clear(s.Stores);
        if (s.Metal is { } metal)
        {
            if (metal.IronGoods is { } iron)
            {
                RecordIT6IronKitRetired(s.Civilization, s.Id, 11, iron[(int)IronGood.Kit11]);
                RecordIT6IronKitRetired(s.Civilization, s.Id, 12, iron[(int)IronGood.Kit12]);
                Array.Clear(iron);
            }
            Array.Clear(metal.Goods); metal.IronWorks = null; metal.IronBuildKind = -1; metal.IronBuildProgress = Fix64.Zero;
            metal.IronTerraces = null; metal.IronFacing = null; metal.Forges = 0; metal.Smelters = 0; metal.MineHeads.Clear();
        }
        if (s.Civic is not null) s.Civic = new CivicState { Seceded = s.Civic.Seceded };
        s.FissionPending = false;
        s.FireLit = false;
    }

    /// <summary>Figures of a settlement as (id, age, female, wounded) in ascending id order, for building a party.</summary>
    public void ListFigures(int settlementIndex, List<FigureView> into)
    {
        int id = _settlements[settlementIndex].Id;
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].SettlementId == id) into.Add(Figure(i));
    }

    public bool IsWounded(long figureId)
    {
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id == figureId) return _figures[i].WoundSeasons > 0;
        return false;
    }

    /// <summary>A person who could fight: id, age, sex and whether a wound has laid them up.</summary>
    public readonly record struct FighterRecord(long Id, int Age, bool Female, bool Wounded, bool Soldier = false);

    /// <summary>Adults and elders of a settlement in ascending id order (children stay out of the fight, docs/MVP/13 §6.1).</summary>
    public void ListFightingAge(int settlementIndex, List<FighterRecord> into)
    {
        int id = _settlements[settlementIndex].Id;
        // The Iron chapter's standing soldiers (WAR-07): a staffed soldier fights as a professional only if upkeep was paid this season.
        int supplied = IronChain ? _settlements[settlementIndex].Metal?.IronSoldiersSupplied ?? 0 : 0;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId != id || f.Age < 14) continue;
            bool soldier = supplied > 0 && f.Role == Role.Soldier;
            if (soldier) supplied--;
            into.Add(new FighterRecord(f.Id, f.Age, f.Female, f.WoundSeasons > 0, soldier));
        }
    }

    /// <summary>Lists only people assigned to this mining camp, so an attack cannot hit the parent's home guard.</summary>
    public void ListMiningCampCrew(int campId, List<FighterRecord> into)
    {
        if (campId < 0) return;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.Camp == campId && f.Age >= 14)
                into.Add(new FighterRecord(f.Id, f.Age, f.Female, f.WoundSeasons > 0));
        }
    }

    public bool TryFigure(long figureId, out FigureView view)
    {
        for (int i = 0; i < _figures.Count; i++)
            if (_figures[i].Id == figureId) { view = Figure(i); return true; }
        view = default;
        return false;
    }

    /// <summary>
    /// Founds a camp for people with nowhere to go (refugees become Free Folk, docs/MVP/13 §9). They carry only what
    /// they stand in; the caller records that the settlement belongs to no civilization.
    /// </summary>
    public int FoundSettlement(int civ, int siteTile, IReadOnlyList<long> figureIds)
    {
        SettlementState camp = NewSettlement(civ, siteTile);
        _settlements.Add(camp);
        foreach (long id in figureIds)
            for (int i = 0; i < _figures.Count; i++)
                if (_figures[i].Id == id)
                {
                    RecordIT6KitRelocation(_figures[i].SettlementId, camp.Id, _figures[i].Kit, Fix64.One);
                    _figures[i].SettlementId = camp.Id; _figures[i].Civilization = civ; break;
                }
        camp.FireLit = false;
        return camp.Id;
    }
}
