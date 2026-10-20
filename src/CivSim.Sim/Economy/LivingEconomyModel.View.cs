using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// What a figure looks like to the renderer: the stored fields a sprite needs (docs/MVP/08 §2), read without building a
/// full <see cref="FigureView"/>. Read-only; the view layer (<c>CivSim.Sim.View</c>) never writes economy state.
/// </summary>
public readonly record struct FigureLook(
    long Id, int Civilization, int SettlementId, int Age, bool Female, Fix64 Health, Fix64 Hunger, Fix64 Warmth,
    EconomyJob Job, byte IllnessSeasons, byte WoundSeasons, byte PregnancySeasons);

/// <summary>
/// Read-only accessors for the view layer (docs/MVP/18). None of them changes state, consumes randomness or allocates,
/// so building frames cannot change a checksum.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The figure at a list position. The list is kept in ascending id order (births append, deaths remove).</summary>
    public FigureLook LookFigure(int index)
    {
        FigureState f = _figures[index];
        return new FigureLook(f.Id, f.Civilization, f.SettlementId, f.Age, f.Female, f.Health, f.Hunger, f.Warmth,
            f.Job, f.IllnessSeasons, f.WoundSeasons, f.PregnancySeasons);
    }

    /// <summary>The next figure id the economy will hand out (every id below it has been used).</summary>
    public long NextFigureId => _nextFigureId;

    /// <summary>Whether the settlement's fire burns (a camp fire, a hearth with fuel, or fire-making).</summary>
    public bool SettlementFireLit(int settlementIndex) => _settlements[settlementIndex].FireLit;

    /// <summary>Whether a daughter band will split off at the next season boundary.</summary>
    public bool SettlementFissionPending(int settlementIndex) => _settlements[settlementIndex].FissionPending;

    /// <summary>The structure being built (its kind, or -1) and the share of its labor done, 0 to 1.</summary>
    public (int Kind, Fix64 Share) SettlementBuild(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.BuildTarget == Structure.Count || s.BuildProgress <= Fix64.Zero) return (-1, Fix64.Zero);
        Fix64 labor = BuildLabor(s.BuildTarget);
        return ((int)s.BuildTarget, labor <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, s.BuildProgress / labor));
    }

    /// <summary>Field plots a settlement tends.</summary>
    public int FieldCount(int settlementIndex) => _settlements[settlementIndex].FieldTiles.Count;
    public int FieldTileAt(int settlementIndex, int index) => _settlements[settlementIndex].FieldTiles[index];

    /// <summary>Orchard plots a settlement tends.</summary>
    public int OrchardCount(int settlementIndex) => _settlements[settlementIndex].OrchardPlots.Count;
    public int OrchardTileAt(int settlementIndex, int index) => _settlements[settlementIndex].OrchardPlots[index].Tile;

    /// <summary>Domestic stock kept at a settlement, in whole animals (small stock, cattle, pigs).</summary>
    public (int SmallStock, int Cattle, int Pigs) DomesticStock(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return (s.DomesticSmallStock.FloorToInt(), s.DomesticCattle.FloorToInt(), s.DomesticPigs.FloorToInt());
    }

    /// <summary>
    /// The best method rung the settlement can practise for a job with the tools it has now (docs/MVP/08 §2.1 "method
    /// tier" for the held-tool sprite), or -1 for none. Hunting is judged for a party of three.
    /// </summary>
    public int MethodRung(int settlementIndex, EconomyJob job)
    {
        string? action = job switch
        {
            EconomyJob.Forage => "forage",
            EconomyJob.Hunt => "hunt",
            EconomyJob.Fish => "fish",
            EconomyJob.Chop or EconomyJob.Gather => "chop",
            _ => null,
        };
        return action is null ? -1 : BestActionRung(_settlements[settlementIndex], action, 3);
    }
}
