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

/// <summary>Why a researched method rung cannot currently be used at a settlement.</summary>
public enum MethodFallbackReason : byte
{
    None, MissingTool, HuntParty,
    /// <summary>The settlement cannot cast tin-bronze kits: no tin in hand, from a sister settlement or a workable deposit (docs/MVP/22 §5.7).</summary>
    NoTin,
    /// <summary>No arsenical ore for arsenical-bronze kits.</summary>
    NoArsenic,
    /// <summary>No copper ore for any metal kit.</summary>
    NoOre,
    /// <summary>Charcoal fell short for want of wood (docs/MVP/22 §5.5).</summary>
    NoFuel,
}

/// <summary>Stable, presentation-facing ids for the tools that can block a method rung.</summary>
public enum MethodToolId : byte
{
    None, Stick, Club, Spear, Tool1, Tool2, Tool4, Tool7, SpearThrower, Bow, Harpoon, FishingKit, Net, Basket, Sickle,
    /// <summary>Metal kits of tiers T8–T10 (docs/MVP/23 §4.1).</summary>
    CopperKit, ArsenicalKit, TinBronzeKit,
}

/// <summary>Read-only efficiency information for one job at one settlement.</summary>
public readonly record struct MethodReadout(
    int EffectiveRung, int KnownRung, int EfficiencyPermille, MethodFallbackReason Reason, MethodToolId RequiredTool);

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
        if (action is null) return -1;
        SettlementState s = _settlements[settlementIndex];
        int rung = BestActionRung(s, action, 3);
        return job == EconomyJob.Chop && s.Metal is not null ? Math.Max(rung, KitRungOfMost(s)) : rung;
    }

    /// <summary>
    /// The Chop rung most adults' kits give (docs/MVP/23 §6.2): 6 when at least half carry bronze, 5 when at least half
    /// carry copper or better, else -1. Kits are per figure, so the settlement's badge is its majority.
    /// </summary>
    private int KitRungOfMost(SettlementState s)
    {
        int adults = 0, bronze = 0, copper = 0;
        foreach (FigureState f in _figures)
        {
            if (f.SettlementId != s.Id || f.Age < 14) continue;
            adults++;
            int rung = KitChopRung(f);
            if (rung == 6) bronze++;
            else if (rung == 5) copper++;
        }
        if (adults == 0) return -1;
        if (bronze * 2 >= adults) return 6;
        if ((bronze + copper) * 2 >= adults) return 5;
        return -1;
    }

    /// <summary>
    /// Describes the best currently usable and highest researched method for a job. Efficiency is the active method's
    /// base output rate as a permille of the researched method's rate; tool quality and unrelated modifiers are omitted.
    /// For hunting, method requirements are evaluated for the three-person display party used by <see cref="MethodRung"/>.
    /// </summary>
    public MethodReadout MethodReadout(int settlementIndex, EconomyJob job)
    {
        SettlementState settlement = _settlements[settlementIndex];
        string? action = job switch
        {
            EconomyJob.Forage => "forage",
            EconomyJob.Hunt => "hunt",
            EconomyJob.Fish => "fish",
            EconomyJob.Chop or EconomyJob.Gather => "chop",
            _ => null,
        };
        int effective = MethodRung(settlementIndex, job);
        if (action is null) return new MethodReadout(effective, effective, 1000, MethodFallbackReason.None, MethodToolId.None);

        int known = effective;
        MethodFallbackReason reason = MethodFallbackReason.None;
        MethodToolId requiredTool = MethodToolId.None;
        ReadOnlySpan<EconomyEffect> effects = _content.Effects;
        for (int i = 0; i < effects.Length; i++)
        {
            EconomyEffect effect = effects[i];
            if (effect.Kind != "method" || effect.Action != action || effect.Rung is not { } rung || rung <= known ||
                !_completedProjects[settlement.Civilization][effect.ProjectIndex]) continue;
            known = rung;
            if (!HuntPartyMatches(action, rung, 3))
            {
                reason = MethodFallbackReason.HuntParty;
                requiredTool = MethodToolId.None;
            }
            else if (action == "chop" && rung >= 5)
            {
                // Metal axes are per-figure kits: the reason is the settlement's metal access ("no tin"), else the kit.
                reason = settlement.Metal is { Reason: not MethodFallbackReason.None } metal && rung > effective
                    ? metal.Reason : MethodFallbackReason.MissingTool;
                requiredTool = rung == 5 ? MethodToolId.CopperKit
                    : HasProject(settlement.Civilization, "MET-05") ? MethodToolId.TinBronzeKit : MethodToolId.ArsenicalKit;
                if (rung <= effective) { reason = MethodFallbackReason.None; requiredTool = MethodToolId.None; }
            }
            else if (!HasRequiredTool(settlement, action, rung))
            {
                reason = MethodFallbackReason.MissingTool;
                requiredTool = MissingMethodTool(settlement, action, rung);
            }
            else
            {
                // A usable higher rung should already be reflected by MethodRung. Keep this readout consistent if a
                // future method gains a requirement outside the tool/party checks used by the economy.
                reason = MethodFallbackReason.None;
                requiredTool = MethodToolId.None;
            }
        }
        if (known <= effective || reason == MethodFallbackReason.None)
            return new MethodReadout(effective, Math.Max(effective, known), 1000, MethodFallbackReason.None, MethodToolId.None);

        Fix64 baseline = JobBaseRate(job);
        Fix64 activeRate = MethodRate(settlement.Civilization, action, effective, baseline);
        Fix64 knownRate = MethodRate(settlement.Civilization, action, known, baseline);
        int efficiency = knownRate <= Fix64.Zero
            ? 0
            : Math.Clamp((activeRate * Fix64.FromInt(1000) / knownRate).FloorToInt(), 0, 1000);
        return new MethodReadout(effective, known, efficiency, reason, requiredTool);
    }

    private Fix64 JobBaseRate(EconomyJob job)
    {
        string id = job switch
        {
            EconomyJob.Forage => "forage",
            EconomyJob.Hunt => "hunt",
            EconomyJob.Fish => "fish",
            EconomyJob.Chop or EconomyJob.Gather => "chop",
            _ => "",
        };
        Fix64 fallback = job switch
        {
            EconomyJob.Forage => Fix64.FromRatio(16, 5),
            EconomyJob.Hunt => Fix64.FromRatio(9, 10),
            EconomyJob.Fish => Fix64.One,
            EconomyJob.Chop or EconomyJob.Gather => Fix64.FromInt(2),
            _ => Fix64.One,
        };
        ReadOnlySpan<EconomyAction> actions = _content.Actions;
        for (int i = 0; i < actions.Length; i++)
            if (actions[i].Id == id && actions[i].BaseRate is { } rate) return rate;
        return fallback;
    }

    private Fix64 MethodRate(int civilization, string action, int rung, Fix64 baseline)
    {
        if (rung < 0) return baseline;
        ReadOnlySpan<EconomyEffect> effects = _content.Effects;
        for (int i = 0; i < effects.Length; i++)
        {
            EconomyEffect effect = effects[i];
            if (effect.Kind != "method" || effect.Action != action || effect.Rung != rung ||
                !_completedProjects[civilization][effect.ProjectIndex]) continue;
            return effect.Rate ?? (effect.Mult is { } mult ? baseline * mult : baseline);
        }
        return baseline;
    }

    private MethodToolId MissingMethodTool(SettlementState settlement, string action, int rung)
    {
        if (action == "fish" && rung == 2)
        {
            if (!HasGood(settlement, "G-FISHKIT")) return MethodToolId.FishingKit;
            if (!HasGood(settlement, "G-NET")) return MethodToolId.Net;
            return MethodToolId.None;
        }
        string? id = action switch
        {
            "forage" when rung == 1 => "G-STICK",
            "forage" when rung == 2 => "G-BASKET",
            "forage" when rung == 3 => "G-SICKLE",
            "scavenge" when rung == 1 => "G-CLUB",
            "hunt" when rung == 1 => "G-SPEAR",
            "hunt" when rung is 2 or 3 => "G-TOOL4",
            "hunt" when rung == 4 => "G-ATLATL",
            "hunt" when rung == 5 => "G-BOW",
            "fish" when rung == 1 => "G-HARPOON",
            "chop" when rung == 1 => "G-TOOL1",
            "chop" when rung == 2 => "G-TOOL2",
            "chop" when rung == 3 => "G-TOOL4",
            "chop" when rung == 4 => "G-TOOL7",
            _ => null,
        };
        if (id is null || HasGood(settlement, id)) return MethodToolId.None;
        return id switch
        {
            "G-STICK" => MethodToolId.Stick,
            "G-CLUB" => MethodToolId.Club,
            "G-SPEAR" => MethodToolId.Spear,
            "G-TOOL1" => MethodToolId.Tool1,
            "G-TOOL2" => MethodToolId.Tool2,
            "G-TOOL4" => MethodToolId.Tool4,
            "G-TOOL7" => MethodToolId.Tool7,
            "G-ATLATL" => MethodToolId.SpearThrower,
            "G-BOW" => MethodToolId.Bow,
            "G-HARPOON" => MethodToolId.Harpoon,
            "G-BASKET" => MethodToolId.Basket,
            "G-SICKLE" => MethodToolId.Sickle,
            _ => MethodToolId.None,
        };
    }
}
