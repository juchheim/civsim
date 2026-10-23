using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

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
    /// <summary>The farthest (hex distance) a daughter band may settle from its parent: on foot, or by boat once the civilization has one (docs/MVP/21).</summary>
    int SettleReach(int civ);
    /// <summary>Whether the band can actually get from the parent's village to the site.</summary>
    bool Reachable(int civ, int fromTile, int toTile);
    /// <summary>True if the band's voyage turns back (shipwreck); it then stays home this season.</summary>
    bool VoyageLost(int civ, int fromTile, int toTile, int season);
}

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

    public Fix64 FissionScaleOf(int civ) => FissionThresholdScale(civ);

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
        Structure[] flammable =
        {
            Structure.HideTent, Structure.LeanTo, Structure.DryingRack, Structure.ThornBarrier, Structure.PitHouse,
            Structure.WattleHouse, Structure.Longhouse, Structure.Granary, Structure.StoragePit, Structure.Lookout,
            Structure.Kiln, Structure.Quern,
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
            if (_figures[i].SettlementId == s.Id) _figures.RemoveAt(i);
        Array.Clear(s.Structures);
        Array.Clear(s.Stores);
        if (s.Metal is { } metal) { Array.Clear(metal.Goods); metal.Forges = 0; metal.Smelters = 0; metal.MineHeads.Clear(); }
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
    public readonly record struct FighterRecord(long Id, int Age, bool Female, bool Wounded);

    /// <summary>Adults and elders of a settlement in ascending id order (children stay out of the fight, docs/MVP/13 §6.1).</summary>
    public void ListFightingAge(int settlementIndex, List<FighterRecord> into)
    {
        int id = _settlements[settlementIndex].Id;
        for (int i = 0; i < _figures.Count; i++)
        {
            FigureState f = _figures[i];
            if (f.SettlementId == id && f.Age >= 14) into.Add(new FighterRecord(f.Id, f.Age, f.Female, f.WoundSeasons > 0));
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
                if (_figures[i].Id == id) { _figures[i].SettlementId = camp.Id; _figures[i].Civilization = civ; break; }
        camp.FireLit = false;
        return camp.Id;
    }
}
