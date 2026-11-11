using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// The basic build planner as a table (docs/MVP/30). <see cref="SelectBuildCandidate"/> used to be one long chain of
/// <c>if (CanBuild(...) &amp;&amp; wanted &amp;&amp; materials) return ...</c>. The same three questions (is it learned, does the town
/// want it, are its materials in store) are now asked by <see cref="BasicRuleWanted"/>, <see cref="BasicNeeds"/> and
/// <see cref="CanBuild"/> in the same order, so the planner and the "what is this town waiting on" readout can never disagree.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The planner's rules in the order it tries them. Some structures appear twice (a pit house for warmth, then for room).</summary>
    private enum BasicRule : byte
    {
        StoragePit, DryingRack, Granary, WarmPitHouse, WarmWattleHouse, ShelterLeanTo, Hearth, Field,
        HousePitHouse, HideTent, HouseWattleHouse, HouseMudbrickHouse, HouseLonghouse, Kiln, PottersWorkshop, Quern, ThornBarrier, Lookout,
        Totem, Marker, SignalPost, Shrine, Hillfort, Palisade, StoneWalls, Weir, Pasture, Orchard,
        Count
    }

    /// <summary>
    /// Rules before this index are the foundation: food storage, shelter against the cold, the hearth and the first fields. A player's
    /// build focus (<see cref="BuildFocusCode"/>) never goes ahead of them, so redirecting a town cannot leave it cold or unhoused first.
    /// </summary>
    private const int FoundationRuleCount = (int)BasicRule.Field + 1;

    private static Structure BasicRuleKind(int rule) => (BasicRule)rule switch
    {
        BasicRule.StoragePit => Structure.StoragePit, BasicRule.DryingRack => Structure.DryingRack, BasicRule.Granary => Structure.Granary,
        BasicRule.WarmPitHouse or BasicRule.HousePitHouse => Structure.PitHouse,
        BasicRule.WarmWattleHouse or BasicRule.HouseWattleHouse => Structure.WattleHouse,
        BasicRule.ShelterLeanTo => Structure.LeanTo, BasicRule.Hearth => Structure.Hearth, BasicRule.Field => Structure.Field,
        BasicRule.HideTent => Structure.HideTent, BasicRule.HouseMudbrickHouse => Structure.MudbrickHouse,
        BasicRule.HouseLonghouse => Structure.Longhouse, BasicRule.Kiln => Structure.Kiln, BasicRule.PottersWorkshop => Structure.PottersWorkshop,
        BasicRule.Quern => Structure.Quern, BasicRule.ThornBarrier => Structure.ThornBarrier, BasicRule.Lookout => Structure.Lookout,
        BasicRule.Totem => Structure.Totem, BasicRule.Marker => Structure.Marker, BasicRule.SignalPost => Structure.SignalPost,
        BasicRule.Shrine => Structure.Shrine, BasicRule.Hillfort => Structure.Hillfort, BasicRule.Palisade => Structure.Palisade,
        BasicRule.StoneWalls => Structure.StoneWalls, BasicRule.Weir => Structure.Weir, BasicRule.Pasture => Structure.Pasture,
        BasicRule.Orchard => Structure.Orchard, _ => Structure.Count,
    };

    /// <summary>What the planner reads about a settlement once per call; <see cref="Cold"/> is worked out only if a rule asks (it walks every figure).</summary>
    private struct BasicCtx
    {
        public int Population;
        public bool NeedsHousing, HasWarmShelter, HasBasicShelter, ExtraLeanTo;
        public int Cold; // -1 not yet asked, 0 no, 1 yes
        public int Season;
    }

    private BasicCtx MakeBasicCtx(SettlementState s, int season)
    {
        int population = Population(s.Id);
        bool needsHousing = HousingCap(s) < (ShouldBuildMarketHousingAhead(s) ? HousingTarget(s) : Fix64.FromInt(population + 8));
        bool hasWarmShelter = s.Structures[(int)Structure.PitHouse] > 0 || s.Structures[(int)Structure.WattleHouse] > 0 ||
            s.Structures[(int)Structure.MudbrickHouse] > 0 || s.Structures[(int)Structure.Longhouse] > 0;
        bool hasBasicShelter = hasWarmShelter || s.Structures[(int)Structure.LeanTo] > 0 || s.Structures[(int)Structure.HideTent] > 0;
        return new BasicCtx
        {
            Population = population, NeedsHousing = needsHousing, HasWarmShelter = hasWarmShelter, HasBasicShelter = hasBasicShelter,
            ExtraLeanTo = s.Civic is null && ShouldBuildMarketHousingAhead(s) && needsHousing, Cold = -1, Season = season,
        };
    }

    private bool BasicCold(SettlementState s, ref BasicCtx c)
    {
        if (c.Cold < 0) c.Cold = WarmthDeficit(s, true) > 0 ? 1 : 0;
        return c.Cold == 1;
    }

    /// <summary>Whether the settlement's own situation calls for the rule's structure (everything except "is it learned" and "are the materials in store").</summary>
    private bool BasicRuleWanted(SettlementState s, int rule, ref BasicCtx c)
    {
        int[] built = s.Structures;
        switch ((BasicRule)rule)
        {
            case BasicRule.StoragePit: return built[(int)Structure.StoragePit] == 0;
            case BasicRule.DryingRack: return built[(int)Structure.DryingRack] == 0;
            case BasicRule.Granary: return built[(int)Structure.Granary] == 0;
            case BasicRule.WarmPitHouse:
            case BasicRule.WarmWattleHouse: return !c.HasWarmShelter && c.HasBasicShelter && BasicCold(s, ref c);
            case BasicRule.ShelterLeanTo:
                return (!c.HasBasicShelter || c.ExtraLeanTo) && (c.NeedsHousing || BasicCold(s, ref c));
            case BasicRule.Hearth: return built[(int)Structure.Hearth] == 0;
            case BasicRule.Field: return FieldLimit(s) > s.FieldTiles.Count && CanPlaceField(s);
            case BasicRule.HousePitHouse: return built[(int)Structure.PitHouse] == 0 && c.NeedsHousing;
            case BasicRule.HideTent: return built[(int)Structure.HideTent] == 0;
            case BasicRule.HouseWattleHouse:
            case BasicRule.HouseMudbrickHouse:
            case BasicRule.HouseLonghouse: return c.NeedsHousing;
            case BasicRule.Kiln: return built[(int)Structure.Kiln] == 0;
            case BasicRule.PottersWorkshop:
            case BasicRule.Totem:
            case BasicRule.Marker:
            case BasicRule.SignalPost:
            case BasicRule.Shrine: return EarlyWorkSiteWanted(s, BasicRuleKind(rule), c.Population);
            case BasicRule.Quern: return built[(int)Structure.Quern] == 0;
            case BasicRule.ThornBarrier: return built[(int)Structure.ThornBarrier] == 0;
            case BasicRule.Lookout: return built[(int)Structure.Lookout] == 0;
            case BasicRule.Hillfort: return built[(int)Structure.Hillfort] == 0 && HasHillfortSite(s);
            case BasicRule.Palisade: return built[(int)Structure.Palisade] == 0;
            case BasicRule.StoneWalls: return WallsAreWanted(s);
            case BasicRule.Weir: return CanPlaceWeir(s);
            case BasicRule.Pasture: return built[(int)Structure.Pasture] < PastureLimit(s);
            case BasicRule.Orchard: return CanPlaceOrchard(s);
            default: return false;
        }
    }

    /// <summary>
    /// What a structure takes from the stores, as the planner reads it: it will not pick the structure until every line is met. These can differ from
    /// <see cref="CanPayStructure"/> (a storage pit is paid with nothing but is only chosen once five rations are in store).
    /// Returns how many lines it wrote (at most 2) to <paramref name="first"/> and <paramref name="second"/>.
    /// </summary>
    private int BasicNeeds(SettlementState s, Structure kind, int season, out BuildNeed first, out BuildNeed second)
    {
        BuildNeed Store(Good good, Fix64 amount) => new((int)good, s.Stores[(int)good], amount);
        BuildNeed none = default;
        first = second = none;
        switch (kind)
        {
            case Structure.StoragePit: first = new BuildNeed(-1, FoodStores(s), Fix64.FromInt(5)); return 1;
            // The rack exists to preserve meat before the settlement can hold a two-unit fresh-meat surplus.
            // Requiring that surplus here made the structure self-defeating; its only paid cost is wood.
            case Structure.DryingRack: first = Store(Good.Wood, Fix64.Half); return 1;
            case Structure.Granary: first = Store(Good.Mudbrick, Fix64.FromInt(80)); return 1;
            case Structure.PitHouse: first = Store(Good.Wood, Fix64.FromInt(4)); return 1;
            case Structure.WattleHouse: case Structure.PottersWorkshop:
                first = Store(Good.Wood, Fix64.FromInt(3)); second = Store(Good.Clay, Fix64.FromInt(2)); return 2;
            case Structure.LeanTo: case Structure.Lookout: case Structure.Pasture: case Structure.Totem: case Structure.SignalPost:
                first = Store(Good.Wood, Fix64.One); return 1;
            case Structure.Hearth: first = Store(Good.Stone, Fix64.Half); return 1;
            case Structure.HideTent: first = Store(Good.Hide, Fix64.FromInt(3)); second = Store(Good.Wood, Fix64.One); return 2;
            case Structure.MudbrickHouse: first = Store(Good.Mudbrick, Fix64.FromInt(60)); return 1;
            case Structure.Longhouse: first = Store(Good.Wood, Fix64.FromInt(12)); second = Store(Good.Clay, Fix64.FromInt(4)); return 2;
            case Structure.Kiln: first = Store(Good.Clay, Fix64.FromInt(3)); second = Store(Good.Stone, Fix64.FromInt(2)); return 2;
            case Structure.Quern: first = Store(Good.Stone, Fix64.One); return 1;
            case Structure.ThornBarrier: first = Store(Good.Wood, Fix64.FromInt(2)); return 1;
            // A marker takes a litre of stone or, failing that, of wood: report the wood line when it is the one in store, else the stone line.
            case Structure.Marker:
                first = s.Stores[(int)Good.Wood] >= Fix64.One && s.Stores[(int)Good.Stone] < Fix64.One ? Store(Good.Wood, Fix64.One) : Store(Good.Stone, Fix64.One);
                return 1;
            case Structure.Shrine: first = Store(Good.Stone, Fix64.FromInt(4)); return 1;
            case Structure.Hillfort: first = Store(Good.Wood, Fix64.FromInt(10)); return 1;
            case Structure.Palisade: first = Store(Good.Wood, PalisadeRequiredWood((int)Tier(s.Id), season)); return 1;
            case Structure.StoneWalls: first = new BuildNeed((int)Good.Stone, WallStoneAvailable(s), Fix64.FromInt(WallCutStoneCost(s))); return 1;
            case Structure.Weir: first = Store(Good.Wood, Fix64.FromInt(3)); second = Store(Good.Stone, Fix64.FromInt(2)); return 2;
            case Structure.Orchard: first = Store(Good.Sapling, Fix64.One); return 1;
            default: return 0;
        }
    }

    private bool BasicNeedsMet(SettlementState s, Structure kind, int season)
    {
        int n = BasicNeeds(s, kind, season, out BuildNeed a, out BuildNeed b);
        return (n < 1 || !a.Short) && (n < 2 || !b.Short);
    }

    /// <summary>
    /// The wood shortage of the first wanted basic structure that can be supplied here, whether or not it is affordable yet.
    /// This is procurement only: the construction selector below still requires all materials before it opens a build slot.
    /// </summary>
    private Fix64 BasicBuildWoodProcurementNeed(SettlementState settlement, int season)
    {
        BasicCtx ctx = MakeBasicCtx(settlement, season);
        // Foundation needs outrank a focus, just as they do in SelectBuildCandidate.
        for (int rule = 0; rule < FoundationRuleCount; rule++)
        {
            Fix64 need = BasicRuleWoodProcurementNeed(settlement, season, rule, ref ctx);
            if (need > Fix64.Zero) return need;
        }

        // A valid focus comes next, matching the affordable-build planner's preference ordering.
        Structure focus = BasicFocusKind(settlement);
        if (focus != Structure.Count && BasicGate(settlement, focus).Open)
        {
            Fix64 need = BasicStructureWoodProcurementNeed(settlement, focus, season);
            if (need > Fix64.Zero) return need;
        }

        Fix64 toolWoodNeed = BasicToolWoodProcurementNeed(settlement);
        if (toolWoodNeed > Fix64.Zero) return toolWoodNeed;

        // When civilization guidance is active, use its highest-priority wanted wood build.
        CivilizationDirection direction = Guidance?.DirectionOf(settlement.Civilization) ?? CivilizationDirection.Balanced;
        if (direction != CivilizationDirection.Balanced)
        {
            Fix64 preferredNeed = Fix64.Zero;
            int bestPreference = 0;
            for (int rule = FoundationRuleCount; rule < (int)BasicRule.Count; rule++)
            {
                Structure kind = BasicRuleKind(rule);
                if (!BasicGate(settlement, kind).Open || !BasicRuleWanted(settlement, rule, ref ctx)) continue;
                Fix64 need = BasicStructureWoodProcurementNeed(settlement, kind, season);
                int preference = need > Fix64.Zero ? CivilizationGuidance.BuildPreference(direction, StructureId(kind)) : 0;
                if (preference <= bestPreference) continue;
                preferredNeed = need;
                bestPreference = preference;
            }
            if (preferredNeed > Fix64.Zero) return preferredNeed;
        }

        // Stable fallback order, still limited to wanted and sourceable works.
        for (int rule = FoundationRuleCount; rule < (int)BasicRule.Count; rule++)
        {
            Fix64 need = BasicRuleWoodProcurementNeed(settlement, season, rule, ref ctx);
            if (need > Fix64.Zero) return need;
        }
        return Fix64.Zero;
    }

    private Fix64 BasicRuleWoodProcurementNeed(SettlementState settlement, int season, int rule, ref BasicCtx ctx)
    {
        Structure kind = BasicRuleKind(rule);
        return BasicGate(settlement, kind).Open && BasicRuleWanted(settlement, rule, ref ctx)
            ? BasicStructureWoodProcurementNeed(settlement, kind, season)
            : Fix64.Zero;
    }

    private Fix64 BasicStructureWoodProcurementNeed(SettlementState settlement, Structure kind, int season)
    {
        int count = BasicNeeds(settlement, kind, season, out BuildNeed first, out BuildNeed second);
        if (count == 0) return Fix64.Zero;
        bool allInputsSourceable = true;
        if (first.Short && first.Slot >= 0 && SupplyOf(settlement, first.Slot).Kind != SupplyKind.Flowing) allInputsSourceable = false;
        if (count > 1 && second.Short && second.Slot >= 0 && SupplyOf(settlement, second.Slot).Kind != SupplyKind.Flowing) allInputsSourceable = false;
        if (!allInputsSourceable) return Fix64.Zero;
        if (first.Slot == (int)Good.Wood && first.Short && SupplyOf(settlement, first.Slot).Kind == SupplyKind.Flowing)
            return first.Need;
        if (count > 1 && second.Slot == (int)Good.Wood && second.Short && SupplyOf(settlement, second.Slot).Kind == SupplyKind.Flowing)
            return second.Need;
        return Fix64.Zero;
    }

    private Fix64 BasicToolWoodProcurementNeed(SettlementState settlement)
    {
        int civ = settlement.Civilization;
        Fix64 wood = settlement.Stores[(int)Good.Wood];
        if (HasProject(civ, "STN-06") && settlement.Stores[(int)Good.Tool6] < Fix64.FromInt(2) &&
            settlement.Stores[(int)Good.Flint] >= Fix64.Parse("0.2") && wood < Fix64.Parse("0.1")) return Fix64.Parse("0.1");
        if (HasProject(civ, "STN-04") && settlement.Stores[(int)Good.Tool4] < Fix64.FromInt(2) &&
            settlement.Stores[(int)Good.Stone] >= Fix64.Parse("0.3") && wood < Fix64.Parse("0.1")) return Fix64.Parse("0.1");
        return Fix64.Zero;
    }

    /// <summary>
    /// The structure the planner builds next: the first rule, in order, whose structure is learned, wanted by the settlement and paid for from
    /// the stores. Nothing here is kept between calls; the planner asks again every season.
    /// </summary>
    private Structure SelectBuildCandidate(SettlementState settlement, int season)
    {
        BasicCtx ctx = MakeBasicCtx(settlement, season);
        Structure focus = BasicFocusKind(settlement);
        // Foundation, housing and food-safety work always comes first.
        for (int rule = 0; rule < FoundationRuleCount; rule++)
        {
            Structure kind = BasicRuleKind(rule);
            if (!CanBuild(settlement.Civilization, kind)) continue;
            if (!BasicRuleWanted(settlement, rule, ref ctx)) continue;
            if (!BasicNeedsMet(settlement, kind, season)) continue;
            return kind;
        }

        // A valid direct player focus retains priority over civilization direction.
        if (focus != Structure.Count && BasicFocusReady(settlement, focus, season)) return focus;

        CivilizationDirection direction = Guidance?.DirectionOf(settlement.Civilization) ?? CivilizationDirection.Balanced;
        if (direction != CivilizationDirection.Balanced)
        {
            Structure preferred = Structure.Count;
            int bestPreference = 0;
            for (int rule = FoundationRuleCount; rule < (int)BasicRule.Count; rule++)
            {
                Structure kind = BasicRuleKind(rule);
                if (!CanBuild(settlement.Civilization, kind) || !BasicRuleWanted(settlement, rule, ref ctx) ||
                    !BasicNeedsMet(settlement, kind, season)) continue;
                int preference = CivilizationGuidance.BuildPreference(direction, StructureId(kind));
                if (preference <= bestPreference) continue;
                preferred = kind;
                bestPreference = preference;
            }
            if (preferred != Structure.Count) return preferred;
        }

        // No preferred candidate is ready; use the existing stable order as the fallback.
        for (int rule = FoundationRuleCount; rule < (int)BasicRule.Count; rule++)
        {
            Structure kind = BasicRuleKind(rule);
            if (!CanBuild(settlement.Civilization, kind)) continue;
            if (!BasicRuleWanted(settlement, rule, ref ctx)) continue;
            if (!BasicNeedsMet(settlement, kind, season)) continue;
            return kind;
        }
        return Structure.Count;
    }

    /// <summary>The site, size and cap rules of the early works (docs/MVP/29), without the materials.</summary>
    private bool EarlyWorkSiteWanted(SettlementState s, Structure kind, int population) => kind switch
    {
        Structure.Totem => s.Structures[(int)Structure.Totem] == 0 && population >= 8,
        Structure.Marker => s.Structures[(int)Structure.Marker] < MarkersPerSettlement && population >= 10,
        Structure.SignalPost => s.Structures[(int)Structure.SignalPost] == 0 && population >= 12,
        Structure.Shrine => s.Structures[(int)Structure.Shrine] == 0 && population >= 20 && !OwnShrineWithin(s, ShrineSpacing),
        Structure.PottersWorkshop => s.Structures[(int)Structure.PottersWorkshop] == 0 && s.Structures[(int)Structure.Kiln] > 0 && population >= PottersMinPop,
        _ => false,
    };

    /// <summary>
    /// Whether the settlement wants one of the works its research unlocked without any other effect before (docs/MVP/29): a totem for a camp of eight, up to two
    /// markers from ten, a signal post from twelve, a shrine from twenty at least six tiles from another, a potter's workshop beside a kiln from twenty-five.
    /// </summary>
    private bool EarlyWorkWanted(SettlementState s, Structure kind, int season, int population)
    {
        if (!CanBuild(s.Civilization, kind)) return false;
        return EarlyWorkSiteWanted(s, kind, population) && CanPayStructure(s, kind, season);
    }
}
