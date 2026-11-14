using System;
using CivSim.Sim.Economy;

namespace CivSim.Sim.View;

/// <summary>How big a settlement looks on its map plate. Mirrors the renderer's village stage (<c>core_layout.gd stage_of</c>).</summary>
public enum PlateTier : byte { Band = 0, Village = 1, Town = 2, City = 3 }

/// <summary>The viewer's relation to a settlement's owner, as the plate's rim shows it (docs/MVP/33 §4.1).</summary>
public enum PlateRelation : byte
{
    /// <summary>A civilization the viewer has not met (or does not know the relation to).</summary>
    Unmet = 0,
    /// <summary>One of the viewer's own settlements.</summary>
    Own = 1,
    AtWar = 2,
    Allied = 3,
    /// <summary>Met, with a pact, truce or border treaty, or simply in contact.</summary>
    Contacted = 4,
    /// <summary>A Free Folk or Tin Folk village: nobody's civilization.</summary>
    Wild = 5,
}

/// <summary>The defence silhouette on the dome's rim, strongest standing work wins (docs/MVP/33 §4.1).</summary>
public enum PlateDefence : byte { None = 0, Thorn = 1, Palisade = 2, Walls = 3, Castle = 4 }

/// <summary>What the ring inside the dome measures (docs/MVP/33 §4.1, Q20): build progress, or wall integrity while besieged.</summary>
public enum PlateMeter : byte { None = 0, Build = 1, Siege = 2 }

/// <summary>The one alert a plate shows, in priority order: a smaller number beats a larger one (docs/MVP/33 §2.2). 1 to 3 pulse.</summary>
public enum PlateAlert : byte
{
    None = 0,
    Besieged = 1,
    /// <summary>A raid or assault is on its way to one of the viewer's settlements; <see cref="PlateFacts.AlertNumber"/> is the seasons left.</summary>
    Raid = 2,
    Occupied = 3,
    Famine = 4,
    Short = 5,
    Fraying = 6,
    HungryWinter = 7,
    FireOut = 8,
    Splitting = 9,
    /// <summary>The viewer's levy host stands against it.</summary>
    LevyTarget = 10,
}

/// <summary>
/// Everything a settlement's name plate shows, as plain numbers (docs/MVP/33 §7.1). It is a pure function of a <see cref="MatchFrame"/>, so the front end
/// only draws it, and "did anything change" is one <c>==</c>.
/// </summary>
public readonly record struct PlateFacts(
    int Civ,
    int Population,
    PlateTier Tier,
    PlateRelation Relation,
    bool Capital,
    bool Remembered,
    bool Empty,
    bool Occupied,
    bool TinFolk,
    bool LevyTarget,
    int SeenSeason,
    PlateDefence Defence = PlateDefence.None,
    PlateMeter Meter = PlateMeter.None,
    int MeterPermille = 0,
    PlateAlert Alert = PlateAlert.None,
    int AlertNumber = 0)
{
    /// <summary>True for the alerts that pulse the rim: siege, raid, occupation.</summary>
    public bool Urgent => Alert is PlateAlert.Besieged or PlateAlert.Raid or PlateAlert.Occupied;
}

/// <summary>Builds <see cref="PlateFacts"/> and ranks plates for the map overlay (docs/MVP/33).</summary>
public static class SettlementPlate
{
    /// <summary>People at which a settlement with permanent dwellings stops being a band. Same number as the renderer's <c>stage_of</c>.</summary>
    public const int BandBelow = 20;
    public const int TownMinPopulation = 60;
    public const int CityMinPopulation = 90;

    private static readonly int PitHouse = LivingEconomyModel.StructureIndex("pit_house");
    private static readonly int WattleHouse = LivingEconomyModel.StructureIndex("wattle_daub_house");
    private static readonly int MudbrickHouse = LivingEconomyModel.StructureIndex("mudbrick_house");
    private static readonly int Longhouse = LivingEconomyModel.StructureIndex("longhouse");
    private static readonly int[] DwellingKinds = { PitHouse, WattleHouse, MudbrickHouse, Longhouse };

    /// <summary>The plate facts of frame settlement row <paramref name="row"/>.</summary>
    public static PlateFacts Facts(MatchFrame frame, int row)
    {
        if (frame is null) throw new ArgumentNullException(nameof(frame));
        if ((uint)row >= (uint)frame.SettlementCount) throw new ArgumentOutOfRangeException(nameof(row));
        var flags = (SettlementViewFlags)frame.SettlementFlags[row];
        bool free = (flags & SettlementViewFlags.FreeFolk) != 0;
        bool tin = (flags & SettlementViewFlags.TinFolk) != 0;
        bool remembered = (flags & SettlementViewFlags.Remembered) != 0;
        int civ = frame.SettlementCiv[row];
        int population = Math.Max(0, frame.SettlementPopulation[row]);
        bool empty = (flags & SettlementViewFlags.Empty) != 0;
        bool capital = (flags & SettlementViewFlags.Capital) != 0 && !remembered;
        PlateRelation relation = RelationOf(frame, flags, civ, free, tin);
        bool live = !remembered && !empty;
        (int siege, bool besieged) = live ? Siege(frame, row) : (0, false);
        PlateMeter meter = PlateMeter.None;
        int permille = 0;
        if (besieged) { meter = PlateMeter.Siege; permille = siege; }
        else if (live && relation == PlateRelation.Own) (meter, permille) = Build(frame, row);
        (PlateAlert alert, int number) = live ? Alert(frame, row, flags, relation, capital, besieged, siege) : (PlateAlert.None, 0);
        return new PlateFacts(
            civ,
            population,
            Tier(frame, row, population),
            relation,
            capital,
            remembered,
            empty,
            (flags & SettlementViewFlags.Occupied) != 0,
            tin,
            (flags & SettlementViewFlags.LevyTarget) != 0,
            frame.SettlementSeenSeason[row],
            live ? Defence(frame, row, flags) : PlateDefence.None,
            meter,
            permille,
            alert,
            number);
    }

    private static readonly int StoneWalls = LivingEconomyModel.StructureIndex("stone_walls");
    private static readonly int Hillfort = LivingEconomyModel.StructureIndex("hillfort");

    private static int IronKind(string id)
    {
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
            if (LivingEconomyModel.IronWorkId(k) == id) return k;
        return -1;
    }

    private static readonly int CastleKind = IronKind("castle");

    private static int Count(MatchFrame frame, int row, int kind)
        => kind >= 0 && kind < MatchFrame.StructureKinds ? frame.SettlementStructures[row * MatchFrame.StructureKinds + kind] : 0;

    /// <summary>The strongest defence standing at a settlement in sight: castle, stone walls or hillfort, palisade, thorn barrier.</summary>
    public static PlateDefence Defence(MatchFrame frame, int row, SettlementViewFlags flags)
    {
        IronFrame iron = frame.Iron;
        if (iron.Active && CastleKind >= 0 && CastleKind < iron.WorkKinds && row * iron.WorkKinds + CastleKind < iron.Works.Length
            && iron.Works[row * iron.WorkKinds + CastleKind] > 0) return PlateDefence.Castle;
        if (Count(frame, row, StoneWalls) > 0 || Count(frame, row, Hillfort) > 0) return PlateDefence.Walls;
        if ((flags & SettlementViewFlags.Palisade) != 0) return PlateDefence.Palisade;
        return (flags & SettlementViewFlags.ThornBarrier) != 0 ? PlateDefence.Thorn : PlateDefence.None;
    }

    /// <summary>Wall integrity (permille, 1000 intact) of a settlement under siege, and whether it is.</summary>
    private static (int Integrity, bool Besieged) Siege(MatchFrame frame, int row)
    {
        IronFrame iron = frame.Iron;
        if (!iron.Active) return (0, false);
        int settlement = frame.SettlementIndex[row];
        for (int i = 0; i < iron.SiegeCount; i++)
            if (iron.SiegeSettlement[i] == settlement) return (Math.Clamp((int)iron.SiegeIntegrityPermille[i], 0, 1000), true);
        return (0, false);
    }

    /// <summary>Build progress of an own settlement: the basic build, else the Iron works one.</summary>
    private static (PlateMeter, int) Build(MatchFrame frame, int row)
    {
        if (frame.SettlementBuildKind[row] >= 0) return (PlateMeter.Build, Math.Clamp((int)frame.SettlementBuildPermille[row], 0, 1000));
        IronFrame iron = frame.Iron;
        if (iron.Active && row < iron.BuildKind.Length && iron.BuildKind[row] >= 0) return (PlateMeter.Build, Math.Clamp((int)iron.BuildPermille[row], 0, 1000));
        return (PlateMeter.None, 0);
    }

    /// <summary>
    /// The one alert a plate shows (docs/MVP/33 §2.2). Own settlements show every kind; a foreign one only the public ones (siege, occupation) and
    /// the viewer's own levy target, so private numbers (food, cohesion) never leak (Q5).
    /// </summary>
    private static (PlateAlert, int) Alert(MatchFrame frame, int row, SettlementViewFlags flags, PlateRelation relation, bool capital, bool besieged, int integrity)
    {
        bool own = relation == PlateRelation.Own;
        if (besieged) return (PlateAlert.Besieged, integrity / 10);
        if (own)
        {
            int incoming = IncomingRaid(frame, frame.SettlementIndex[row]);
            if (incoming >= 0) return (PlateAlert.Raid, incoming);
        }
        if ((flags & SettlementViewFlags.Occupied) != 0) return (PlateAlert.Occupied, 0);
        if (own)
        {
            int food = frame.SettlementFood[row];
            if (food >= (int)FoodBand.Famine) return (PlateAlert.Famine, 0);
            if (food >= (int)FoodBand.Short) return (PlateAlert.Short, 0);
            if (!capital && frame.SettlementCohesion[row] >= (int)CohesionBand.Fraying) return (PlateAlert.Fraying, 0);
            if (frame.SettlementWinterForecast[row] >= (int)FoodBand.Short) return (PlateAlert.HungryWinter, 0);
            if ((flags & SettlementViewFlags.FireLit) == 0 && !frame.FocusMandateUnlocked) return (PlateAlert.FireOut, 0);
            if ((flags & SettlementViewFlags.FissionPending) != 0) return (PlateAlert.Splitting, 0);
        }
        else if ((flags & SettlementViewFlags.LevyTarget) != 0) return (PlateAlert.LevyTarget, 0);
        return (PlateAlert.None, 0);
    }

    /// <summary>Seasons until the soonest raid or assault on the viewer's settlement <paramref name="settlement"/> arrives, or -1.</summary>
    private static int IncomingRaid(MatchFrame frame, int settlement)
    {
        int best = -1;
        for (int i = 0; i < frame.PartyCount; i++)
        {
            var kind = (PartyKind)frame.PartyKind[i];
            if (kind is not (PartyKind.Raid or PartyKind.Assault)) continue;
            if (frame.PartyTo[i] != settlement || frame.PartyTargetCiv[i] != frame.ViewerCiv) continue;
            int left = Math.Max(0, frame.PartyArriveSeason[i] - frame.Season);
            if (best < 0 || left < best) best = left;
        }
        return best;
    }

    private static PlateRelation RelationOf(MatchFrame frame, SettlementViewFlags flags, int civ, bool free, bool tin)
    {
        if (free || tin) return PlateRelation.Wild;
        if ((flags & SettlementViewFlags.Own) != 0) return PlateRelation.Own;
        if ((uint)civ >= (uint)frame.CivFlags.Length) return PlateRelation.Unmet;
        var civFlags = (CivViewFlags)frame.CivFlags[civ];
        if ((civFlags & CivViewFlags.Self) != 0) return PlateRelation.Own;
        if ((civFlags & CivViewFlags.AtWar) != 0) return PlateRelation.AtWar;
        if ((civFlags & CivViewFlags.Allied) != 0) return PlateRelation.Allied;
        if ((civFlags & (CivViewFlags.Contacted | CivViewFlags.Pact | CivViewFlags.InTruce | CivViewFlags.Treaty)) != 0) return PlateRelation.Contacted;
        return PlateRelation.Unmet;
    }

    /// <summary>
    /// Band, village, town or city. A band has no permanent dwelling and fewer than 20 people; a city has 90 and a temple or ziggurat; a town has 60.
    /// A settlement only remembered shows no structures, so it is judged on its head count alone.
    /// </summary>
    public static PlateTier Tier(MatchFrame frame, int row, int population)
    {
        int kinds = MatchFrame.StructureKinds;
        int basis = row * kinds;
        int permanent = frame.SettlementHalls[row];
        foreach (int kind in DwellingKinds)
            if (kind >= 0 && kind < kinds) permanent += frame.SettlementStructures[basis + kind];
        bool remembered = (frame.SettlementFlags[row] & (ushort)SettlementViewFlags.Remembered) != 0;
        if (population < BandBelow && (permanent == 0 || remembered)) return PlateTier.Band;
        bool shrine = frame.SettlementTemples[row] > 0 || frame.SettlementZiggurats[row] > 0 || frame.SettlementZigguratPermille[row] > 0;
        if (population >= CityMinPopulation && shrine) return PlateTier.City;
        return population >= TownMinPopulation ? PlateTier.Town : PlateTier.Village;
    }

    /// <summary>
    /// How much a plate deserves screen space when plates overlap (docs/MVP/33 §6.3): higher wins. The overlay adds selection, hover and
    /// alerts on top of this. Ties go to the larger head count, then the lower settlement index.
    /// </summary>
    public static int Relevance(in PlateFacts facts)
    {
        if (facts.Empty) return 0;
        if (facts.Remembered) return 10;
        if (facts.Relation == PlateRelation.Own) return facts.Urgent ? 80 : facts.Capital ? 70 : 50;
        if (facts.Occupied || facts.LevyTarget || facts.Alert == PlateAlert.Besieged) return 60;
        return facts.Relation switch
        {
            PlateRelation.AtWar => 40,
            PlateRelation.Allied or PlateRelation.Contacted => 30,
            _ => 20,
        };
    }
}
