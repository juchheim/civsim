using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The five Discovery rows that had no system behind them (M13 WP-E6-7 slice 7, docs/MVP/38 §5.3 and §5.4). Each reads its value from the row's own
/// <c>mod</c> effect through <see cref="Modifier"/>, so the number stays in the YAML; nothing here is stored, so no snapshot version moves, and every
/// term is exactly 1 outside a Discovery match.
/// <list type="bullet">
/// <item>BLD-13 (<c>city_move_and_build_speed</c> x1.15): a City builds faster, so its construction labor is divided by the row's value.</item>
/// <item>NUM-10 (<c>navigation_calc_speed</c> x2): the calculation behind precision parts, chronometers and telescopes takes half the labor.</item>
/// <item>INQ-09 (<c>mec_nrg_sea_effects</c> x1.1): every MEC, NRG and SEA project effect is a tenth stronger (see <see cref="NewtonLift"/>).</item>
/// <item>COM-11 (<c>cohesion_event_response</c> x1.5, <c>gov_effects</c> x1.1): in a settlement with a print shop Cohesion closes on its target faster and the GOV terms count a tenth more.</item>
/// <item>ART-10 (<c>legend_chance</c> x2, <c>art_insight</c> x1.1, +1 Beacon): while a patron stands, see <see cref="PatronStands"/>.</item>
/// <item>INQ-07 (<c>tower_sight</c> x2): a settlement with a lookout post or a tower-bearing work sees farther, see <see cref="TowerSight"/>.</item>
/// </list>
/// </summary>
public sealed partial class LivingEconomyModel
{
    // ---- BLD-13: perspective-planned streets ---------------------------------------------------------------------------------------------------

    /// <summary>The factor on a settlement's construction labor: 1 / the row's speed in a City of a civilization with BLD-13 (Discovery), else 1.</summary>
    private Fix64 CityBuildLaborFactor(SettlementState s)
    {
        if (!DiscoveryChain || s.Civic is not { } civic || civic.Tier < (byte)SettlementTier.City || !Done(s.Civilization, "BLD-13")) return Fix64.One;
        Fix64 speed = Modifier(s.Civilization, "city_move_and_build_speed");
        return speed > Fix64.One ? Fix64.One / speed : Fix64.One;
    }

    // ---- NUM-10: logarithms and analytic geometry ----------------------------------------------------------------------------------------------

    /// <summary>The factor on the labor of the instrument recipes (parts, chronometer, telescope): 1 / the row's calculation speed once NUM-10 is learned.</summary>
    private Fix64 InstrumentCalcFactor(int civ, string recipeId)
    {
        if (!DiscoveryChain || recipeId is not ("make_parts" or "make_chronometer" or "make_telescope")) return Fix64.One;
        Fix64 speed = Modifier(civ, "navigation_calc_speed");
        return speed > Fix64.One ? Fix64.One / speed : Fix64.One;
    }

    // ---- INQ-09: Newtonian physics -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// INQ-09 makes the effects of the mechanics, energy and seafaring projects a tenth stronger: the part of a multiplier above 1 (or a percentage) grows by the
    /// row's value, so a x1.5 becomes x1.55 and a +30% becomes +33%. The registry's own floor and the effect's sign are untouched, and a discount (a value below 1)
    /// is left alone: physics explains why machines work, it does not make them cheaper to build. Discovery only.
    /// </summary>
    private static Fix64 NewtonLift(Fix64 strength, string projectId, string? operation, Fix64 value)
    {
        if (strength <= Fix64.One || projectId.Length < 4 || projectId[3] != '-') return value;
        string lineage = projectId.Substring(0, 3);
        if (lineage is not ("MEC" or "NRG" or "SEA")) return value;
        return operation switch
        {
            "mult" when value > Fix64.One => Fix64.One + (value - Fix64.One) * strength,
            "pct" when value > Fix64.Zero => value * strength,
            _ => value,
        };
    }

    /// <summary>
    /// INQ-09's strength read straight from the effect list, not through <see cref="Modifier"/>: <see cref="ComputeModifier"/> calls it while the registry cache is
    /// mid-insert, and a nested <c>Modifier</c> call would shift the slot the outer call is about to fill.
    /// </summary>
    private Fix64 NewtonStrength(int civ)
    {
        if (!DiscoveryChain || !HasProject(civ, "INQ-09")) return Fix64.One;
        Fix64 strength = Fix64.One;
        for (int i = 0; i < _content.Effects.Length; i++)
        {
            EconomyEffect effect = _content.Effects[i];
            if (effect.Kind == "mod" && effect.Stat == "mec_nrg_sea_effects" && effect.Value is { } v && _completedProjects[civ][effect.ProjectIndex] && effect.Operation == "mult")
                strength *= v;
        }
        return strength;
    }

    // ---- COM-11: newspapers and pamphlets ------------------------------------------------------------------------------------------------------

    /// <summary>A settlement whose civilization has COM-11 and which runs a print shop (Discovery only).</summary>
    private bool PrintedNews(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "COM-11") && IronWorksCount(s, IronStructure.PrintShop) > 0;

    /// <summary>The multiplier on how far Cohesion moves toward its target in a season: the row's event response in a print-shop town, else 1.</summary>
    private Fix64 CohesionResponse(SettlementState s)
    {
        if (!PrintedNews(s)) return Fix64.One;
        Fix64 response = Modifier(s.Civilization, "cohesion_event_response");
        return response > Fix64.One ? response : Fix64.One;
    }

    /// <summary>The share by which the GOV terms of a print-shop town's Cohesion target count more (0 elsewhere): the row's 10%, a bounded opinion effect.</summary>
    private Fix64 PublicOpinionGain(SettlementState s)
    {
        if (!PrintedNews(s)) return Fix64.Zero;
        Fix64 gov = Modifier(s.Civilization, "gov_effects");
        return gov > Fix64.One ? gov - Fix64.One : Fix64.Zero;
    }

    // ---- ART-10: Renaissance patronage ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A patron stands in the realm: the capital holds a Palace (the court) or any settlement holds a Wonder (the Ziggurat or an Iron Wonder, the Renaissance
    /// Palace among them). A settlement that seceded or was lost does not count. Discovery only.
    /// </summary>
    internal bool PatronStands(int civ)
    {
        if (!DiscoveryChain || !Done(civ, "ART-10")) return false;
        int capital = CapitalOf(civ);
        if (capital >= 0 && _settlements[capital].Civic is { Palaces: > 0, Seceded: false }) return true;
        if (CivHasZiggurat(civ)) return true;
        for (int i = 0; i < _settlements.Count; i++)
        {
            SettlementState s = _settlements[i];
            if (s.Civilization != civ || s.Civic is not { Seceded: false }) continue;
            for (int k = 0; k < (int)IronStructure.Count; k++)
                if (IsWonder((IronStructure)k) && IronWorksCount(s, (IronStructure)k) > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// The factor on the chance that a notable figure becomes a Legend: the row's x2 while a patron stands, else 1. <b>Nothing reads it yet</b>: the simulation has no
    /// Legend system in any era (docs/MVP/17 lists "Legends beyond naming in the Chronicle" as deferred), so this is the single place a future one multiplies by.
    /// </summary>
    internal Fix64 LegendChanceFactor(int civ)
    {
        if (!PatronStands(civ)) return Fix64.One;
        Fix64 chance = Modifier(civ, "legend_chance");
        return chance > Fix64.One ? chance : Fix64.One;
    }

    /// <summary>The Beacon slot a patron court adds (the row's lever, "one more slot while a patron court stands").</summary>
    public int PatronBeaconSlots(int civ) => PatronStands(civ) ? 1 : 0;

    /// <summary>Whether a patron funds the civilization's masters this season: the research side lifts the monks' Insight by the row's <c>art_insight</c> only then.</summary>
    public bool PatronFundsMasters(int civ) => PatronStands(civ);

    // ---- INQ-07: the telescope ------------------------------------------------------------------------------------------------------------------

    /// <summary>A lookout post's sight in tiles (docs/MVP/10 and 13 §2.1: "+3 sight"); the telescope's <c>tower_sight</c> multiplier scales it.</summary>
    private const int LookoutSight = 3;

    /// <summary>A settlement holds a lookout post or a work with towers: the gatehouse, a castle, a concentric castle or a bastion fort.</summary>
    private bool HasSightTower(SettlementState s)
        => s.Structures[(int)Structure.Lookout] > 0 || IronWorksCount(s, IronStructure.Gatehouse) > 0 || IronWorksCount(s, IronStructure.Castle) > 0
           || IronWorksCount(s, IronStructure.ConcentricCastle) > 0 || IronWorksCount(s, IronStructure.BastionFort) > 0;

    /// <summary>
    /// The extra tiles a settlement sees once its civilization has the telescope: a tower's <see cref="LookoutSight"/> times the row's <c>tower_sight</c> minus one,
    /// so x2 doubles the tower's reach (+3 tiles). The sight a tower gives before the telescope is not modelled (the belief's routine sight is a flat reach + 3,
    /// docs/MVP/14 §3.1), so this is the telescope's increment only: 0 without INQ-07, without a tower, in a seceded settlement, and outside a Discovery match.
    /// The belief builder (what the AI sees) and the frame builder (the player's fog) both add it to the settlement's routine sight.
    /// </summary>
    public int TowerSight(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return 0;
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is { Seceded: true } || !Done(s.Civilization, "INQ-07") || !HasSightTower(s)) return 0;
        Fix64 sight = Modifier(s.Civilization, "tower_sight");
        return sight > Fix64.One ? ((sight - Fix64.One) * LookoutSight).RoundToInt() : 0;
    }
}
