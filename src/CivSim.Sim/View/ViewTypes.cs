using System;

namespace CivSim.Sim.View;

// Numeric values in this file are part of the sim-to-renderer contract (docs/MVP/18): append, never renumber.

/// <summary>What a figure is doing, for its gesture and badge (docs/MVP/08 §2.2).</summary>
public enum FigureActivity : byte
{
    Idle = 0,
    Forage = 1, Hunt = 2, Fish = 3, Farm = 4, Chop = 5, Gather = 6, Craft = 7, Build = 8, Preserve = 9,
    Rest = 10, TendFire = 11, Herd = 12,
    /// <summary>A child (under 14): small, follows the adults around home.</summary>
    Child = 13,
    /// <summary>Laid up by a wound or illness: resting at home.</summary>
    Convalescing = 14,
    OnGift = 15, OnBarter = 16, OnRaid = 17, OnAssault = 18,
    /// <summary>Holding a taken settlement until it changes hands (docs/MVP/13 §8.2).</summary>
    Occupying = 19,
    /// <summary>With an Expedition Decree's party (docs/MVP/22 §4.3).</summary>
    OnExpedition = 20,
    /// <summary>Carrying a compact's goods (docs/MVP/22 §6.3).</summary>
    OnCaravan = 21,
    /// <summary>Exploring the frontier from a settlement (docs/MVP/13 §2.3).</summary>
    Scout = 22,
}

/// <summary>Bits of <see cref="MatchFrame.FigureFlags"/>.</summary>
[Flags]
public enum FigureFlags : ushort
{
    None = 0,
    Female = 1,
    /// <summary>Walking between home and work or along a road (the renderer plays a walk cycle).</summary>
    Walking = 2,
    Wounded = 4,
    Ill = 8,
    Pregnant = 16,
    Hungry = 32,
    Cold = 64,
    /// <summary>A painted warrior (WAR-04: one adult man in five, docs/MVP/13 §6.1).</summary>
    Warrior = 128,
    /// <summary>Carrying weapons: on a raid, an assault or an occupation, or a warrior.</summary>
    Armed = 256,
    /// <summary>Away from home with a party.</summary>
    Away = 512,
    /// <summary>Belongs to a Free Folk settlement (neutral grey, docs/MVP/08 §4.1).</summary>
    FreeFolk = 1024,
}

/// <summary>
/// Bits of <see cref="MatchFrame.CivLooks"/>: projects that change how a civilization's land and people look. View-only (the
/// renderer's fields, flocks, carts, chariots, temples and clothes); nothing here feeds back into the simulation.
/// </summary>
[Flags]
public enum CivLook : ushort
{
    None = 0,
    /// <summary>AGR-05 Ard Plough: furrowed fields and ox teams drawing the ard.</summary>
    ArdPlough = 1,
    /// <summary>AGR-07 Fallow &amp; Manuring: fields rest in turn and cattle graze them.</summary>
    Fallow = 2,
    /// <summary>HUS-02 Herd Following: herders walk beside the wild herds on their land.</summary>
    HerdFollowing = 4,
    /// <summary>HUS-06 Wool Breeds: big woolly flocks and bales of fleece.</summary>
    WoolBreeds = 8,
    /// <summary>TRN-05 Wheeled Carts: ox carts shuttle along the trails.</summary>
    Carts = 16,
    /// <summary>WAR-06 Chariots: raiders ride chariots over open ground and chariots stand in the villages.</summary>
    Chariots = 32,
    /// <summary>BEL-07 State Cult: a temple in the greatest village.</summary>
    StateCult = 64,
    /// <summary>SOC-07 Chiefdom: a chief's hall in the greatest village.</summary>
    Chiefdom = 128,
    /// <summary>FIB-04 Tailored Clothing: fitted parkas, sleeves, leggings and boots.</summary>
    TailoredClothing = 256,
}

/// <summary>Age band by size: child under 14, adult 14 to 44, elder 45 and over (docs/MVP/08 §2.1 "life stage").</summary>
public enum AgeBand : byte { Child = 0, Adult = 1, Elder = 2 }

/// <summary>Health band: Good ≥ 0.75, Fair ≥ 0.5, Poor ≥ 0.25, Critical below (pale tint, slow walk).</summary>
public enum HealthBand : byte { Good = 0, Fair = 1, Poor = 2, Critical = 3 }

/// <summary>The bundle a figure carries on its way home (docs/MVP/08 §2.1 "carry item").</summary>
public enum CarryKind : byte { None = 0, Plants = 1, Meat = 2, Fish = 3, Wood = 4, Stone = 5, Grain = 6, Goods = 7, Loot = 8 }

/// <summary>Per-tile visibility for the viewer (docs/MVP/18 §4).</summary>
public enum TileVisibility : byte
{
    /// <summary>Never seen: draw fog, nothing else.</summary>
    Unknown = 0,
    /// <summary>Seen before: terrain and the owner as last seen; no figures, herds or parties.</summary>
    Explored = 1,
    /// <summary>In sight now: everything on it is current.</summary>
    Visible = 2,
}

/// <summary>Bits of <see cref="MatchFrame.TileFlags"/>.</summary>
[Flags]
public enum TileViewFlags : byte
{
    None = 0,
    /// <summary>Two civilizations both hold claim at the ownership threshold (stripes, docs/MVP/08 §4.1). Visible tiles only.</summary>
    Contested = 1,
    /// <summary>A seasonal camp stands here.</summary>
    Camp = 2,
    /// <summary>A Beacon stands here.</summary>
    Beacon = 4,
    /// <summary>Inside the viewer's home range (½ reach around its settlements, the hatch of 08 §4.1).</summary>
    HomeRange = 8,
}

/// <summary>Food security bands (docs/MVP/08 §5 gauge rings): green, amber, red, famine.</summary>
public enum FoodBand : byte { Secure = 0, Tight = 1, Short = 2, Famine = 3 }

/// <summary>Cohesion bands (docs/MVP/08 §5 "fraying rope"): Cohesion runs 0.6 to 1.2.</summary>
public enum CohesionBand : byte { Strong = 0, Steady = 1, Strained = 2, Fraying = 3 }

/// <summary>Kind of camp in <see cref="MatchFrame.CampKind"/>.</summary>
public enum CampKind : byte { Seasonal = 0, Mining = 1 }

/// <summary>Bits of <see cref="MatchFrame.SettlementFlags"/>.</summary>
[Flags]
public enum SettlementViewFlags : ushort
{
    None = 0,
    FreeFolk = 1,
    FireLit = 2,
    FissionPending = 4,
    /// <summary>Held by an occupying force.</summary>
    Occupied = 8,
    /// <summary>Not in sight now: owner and head count are as last seen, structures are not shown.</summary>
    Remembered = 16,
    /// <summary>Nobody lives here (razed or abandoned).</summary>
    Empty = 32,
    ThornBarrier = 64,
    Palisade = 128,
    Lookout = 256,
    /// <summary>One of the viewer's own settlements.</summary>
    Own = 512,
    /// <summary>A Tin Folk village (docs/MVP/22 §6.4).</summary>
    TinFolk = 1024,
    /// <summary>A Corvée drives its public work now (docs/MVP/22 §4.3; own settlements only).</summary>
    Corvee = 2048,
    /// <summary>The viewer's levy host stands against it.</summary>
    LevyTarget = 4096,
}

/// <summary>Bits of <see cref="MatchFrame.CivFlags"/>: the viewer's relation to a civilization.</summary>
[Flags]
public enum CivViewFlags : ushort
{
    None = 0,
    /// <summary>The viewer itself (or every civilization in omniscient mode).</summary>
    Self = 1,
    /// <summary>Private numbers (population, Insight, projects) are filled in; otherwise only the public record is.</summary>
    Detailed = 2,
    Aware = 4,
    Contacted = 8,
    AtWar = 16,
    Allied = 32,
    InTruce = 64,
    /// <summary>A non-aggression pact with the viewer stands (DIP-04).</summary>
    Pact = 128,
    /// <summary>A border treaty with the viewer stands (DIP-05).</summary>
    Treaty = 256,
}

/// <summary>A party on the road (docs/MVP/13 §4 to §8).</summary>
public enum PartyKind : byte { Gift = 0, Barter = 1, Raid = 2, Assault = 3, Explore = 4, Caravan = 5 }

/// <summary>How loudly the front end should present an event (docs/MVP/08 §10.1).</summary>
public enum ViewEventTier : byte { Ambient = 0, Info = 1, Milestone = 2, Moment = 3, Critical = 4 }

/// <summary>What a <see cref="ViewEventKind.RaidLaunched"/> party marches on (the event's C).</summary>
public static class RaidTarget
{
    public const int Settlement = 0, Caravan = 1, MiningCamp = 2, Assault = 3;
}

/// <summary>What happened. Stable numbers (docs/MVP/18 §6): append, never renumber.</summary>
public enum ViewEventKind : ushort
{
    None = 0,

    // ---- derived by the view builder from economy state, for the viewer's own civilization ----
    /// <summary>A new season began. A = season of year (0 spring .. 3 winter), B = year, C = 1 if the last winter was harsh.</summary>
    SeasonBegan = 1,
    /// <summary>Children born last season. A = count.</summary>
    Births = 2,
    /// <summary>Deaths last season by cause (seasonal hazards; battle deaths come with the battle). A = <see cref="Economy.EconomyDeathCause"/>, B = count.</summary>
    Deaths = 3,
    /// <summary>A settlement appeared (fission, or refugees founding a camp). A = 1 if Free Folk.</summary>
    SettlementFounded = 4,
    /// <summary>A structure was finished. A = economy structure kind (see <see cref="Economy.LivingEconomyModel.StructureName"/>), B = how many now stand.</summary>
    StructureBuilt = 5,
    /// <summary>A structure was lost (burned in a raid). A = structure kind, B = how many still stand.</summary>
    StructureLost = 6,
    /// <summary>A settlement's fire went out.</summary>
    FireOut = 7,
    /// <summary>A settlement's food fell to the famine band, or its winter forecast did (B = 1 for the forecast). A = <see cref="FoodBand"/>.</summary>
    FamineRisk = 8,
    /// <summary>A hostile party heading for one of the viewer's settlements came into sight. A = <see cref="PartyKind"/>, B = heads, C = arrival season.</summary>
    PartyIncoming = 9,

    // ---- research (from ResearchSystem events) ----
    SlotFilled = 20,
    CouncilPick = 21,
    /// <summary>A project completed. Project = dense index; Figure = innovator; A = the first <see cref="Research.VisibleChange"/> of its effects (or None).</summary>
    ProjectCompleted = 22,
    /// <summary>The innovator Moment (08 §10.1). Project = index (look up ProjectDef.Moment / Flavor); A = 1 in-era (plays), 0 backfill (batched).</summary>
    Moment = 23,
    /// <summary>One visible change from an effect. A = <see cref="Research.VisibleChange"/>, B = effect index.</summary>
    VisibleChange = 24,
    /// <summary>First in the world. A = Legacy points. Public: every viewer sees every civilization's Firsts.</summary>
    FirstClaimed = 25,
    /// <summary>A keystone completed. Project = keystone, A = new era, B = boon (0 Insight, 1 Beacon). Public.</summary>
    EraAdvanced = 26,
    /// <summary>Adoption milestone. A = percent who know it (50, 90, 100), B = settlements.</summary>
    Adopted = 27,
    /// <summary>A completed project went Dormant (A = 1) or came back (A = 0).</summary>
    DormantChanged = 28,
    /// <summary>A gate resource was sighted. A = gate target index (ResearchCatalog.GateTargets).</summary>
    ResourceSeen = 29,
    /// <summary>A project became Available.</summary>
    ProjectUnlocked = 30,

    // ---- politics (from PoliticsSystem events; one record per side) ----
    Aware = 40,
    /// <summary>First contact. A = this side's stance, B = theirs (Friendly, Wary, Hostile).</summary>
    Contact = 41,
    /// <summary>First blood. A = this side's hurt, B = theirs.</summary>
    Skirmish = 42,
    /// <summary>A gift arrived. A = value, B = Relations gained.</summary>
    GiftArrived = 43,
    /// <summary>A barter trip came home. A = value.</summary>
    BarterDone = 44,
    /// <summary>A raid or assault set out. A = party size; B = 0; C = what it marches on (<see cref="RaidTarget"/>); Settlement = target settlement or -1; Tile = target tile or -1.</summary>
    RaidLaunched = 45,
    /// <summary>A raid ended. A = 1 raided successfully, 0 failed, 2 we were raided, 3 we repelled it; B = loot in rations (what the raiders brought home, or what the target lost). Tile = where it was fought; C = head counts, bytes low to high: raiders, defenders, raiders down, defenders down.</summary>
    RaidResolved = 46,
    /// <summary>War. A = 0 we declared, 1 it was declared on us.</summary>
    WarDeclared = 47,
    /// <summary>Peace. A = seasons the war lasted.</summary>
    PeaceMade = 48,
    /// <summary>An assault was fought. A = 1 we won, 0 lost (attacking); 2 they took it, 3 we held (defending). Settlement = target. C = head counts as for <see cref="RaidResolved"/>, or 0 when no battle was fought (an investment ended).</summary>
    AssaultResolved = 49,
    /// <summary>A settlement is occupied. A = holders; Settlement = it.</summary>
    Occupied = 50,
    /// <summary>An occupation ended without capture. A = 0 broken, 1 abandoned.</summary>
    OccupationBroken = 51,
    /// <summary>A settlement changed hands. A = people who stayed (negative on the losing side's record); Settlement = it.</summary>
    Captured = 52,
    /// <summary>Refugees founded a Free Folk camp. A = people; Settlement = it.</summary>
    FreeFolkFounded = 53,
    AllianceFormed = 54,
    /// <summary>A Beacon was placed. Tile = where.</summary>
    BeaconPlaced = 55,
    /// <summary>A player command was rejected. A = command kind, B = refusal reason code.</summary>
    OrderRefused = 56,
    /// <summary>A voyage turned back at sea (docs/MVP/21). A = open-sea tiles, B = what sailed (0 gift, 1 barter, 2 raid, 3 assault, 4 settlers).</summary>
    VoyageLost = 57,

    // ---- Bronze: Decrees and trade (docs/MVP/22 §14; PoliticsSystem events, one record per side) ----
    /// <summary>A Decree was carried out. A = <see cref="Politics.DecreeKind"/>, B = subject (see PoliticsEventKind.DecreeResolved); Settlement/Tile set where it has one.</summary>
    DecreeResolved = 58,
    /// <summary>A Decree was refused, nothing spent. A = <see cref="Politics.DecreeKind"/>, B = <see cref="Politics.OrderRefusalReason"/>.</summary>
    DecreeRefused = 59,
    /// <summary>An expedition reported. A = tiles revealed; Tile = its target.</summary>
    ExpeditionReturned = 60,
    /// <summary>A compact began. A = compact id, B = <see cref="Politics.TradeGood"/> (+100 when this side sells).</summary>
    CompactSigned = 61,
    /// <summary>A compact ended. A = compact id, B = <see cref="Politics.CompactEndReason"/>.</summary>
    CompactEnded = 62,
    /// <summary>An embargo. A = <see cref="Politics.TradeGood"/>, B = 1 we set it, 0 we lifted it, 3 set on us, 2 lifted on us.</summary>
    EmbargoSet = 63,
    /// <summary>A caravan was raided. A = compact id, B = loot (permille of the good's unit; -1 the raid failed).</summary>
    CaravanRaided = 64,
    /// <summary>Tin Folk appeared. Settlement = the village, Tile = its placer.</summary>
    TinFolkAppeared = 65,
    /// <summary>A pact or treaty was signed. A = <see cref="Politics.CompactVariant"/>, B = seasons.</summary>
    PactSigned = 66,
    /// <summary>A pact was broken by war. A = <see cref="Politics.CompactVariant"/>, B = 1 we broke it.</summary>
    PactBroken = 67,
    /// <summary>A Free Folk village joined by Invite. Settlement = it, A = people.</summary>
    VillageJoined = 68,
}

/// <summary>
/// The text key of an event: an index into the front end's string table of Chronicle templates (docs/MVP/18 §6). The
/// simulation never writes prose; <see cref="ViewText.Key"/> gives each a stable dotted name for localisation files.
/// Append, never renumber.
/// </summary>
public enum ChronicleKey : ushort
{
    None = 0,
    SeasonSpring = 1, SeasonSummer = 2, SeasonAutumn = 3, SeasonWinter = 4, SeasonHarshWinter = 5,
    Births = 6, Deaths = 7, SettlementFounded = 8, FreeFolkCampFounded = 9, StructureBuilt = 10, StructureBurned = 11,
    FireOut = 12, FamineNow = 13, FamineForecast = 14, RaidIncoming = 15, AssaultIncoming = 16, PartyIncoming = 17,

    SlotFilledByPlayer = 30, SlotFilledByCouncil = 31, SlotFilledFromQueue = 32, CouncilPick = 33, ProjectCompleted = 34,
    MomentPlays = 35, MomentBackfill = 36, VisibleChange = 37, FirstClaimed = 38, EraAdvanced = 39, Adopted = 40,
    ProjectDormant = 41, ProjectRevived = 42, ResourceSeen = 43, ProjectUnlocked = 44,

    Aware = 60, ContactFriendly = 61, ContactWary = 62, ContactHostile = 63, Skirmish = 64, GiftSent = 65,
    BarterDone = 66, RaidLaunched = 67, RaidSucceeded = 68, RaidFailed = 69, RaidSuffered = 70, RaidRepelled = 71,
    WarDeclaredByUs = 72, WarDeclaredOnUs = 73, PeaceMade = 74, AssaultWon = 75, AssaultLost = 76, SettlementFell = 77,
    SettlementHeld = 78, Occupied = 79, OccupationBroken = 80, OccupationAbandoned = 81, SettlementCaptured = 82,
    SettlementLost = 83, FreeFolkFounded = 84, AllianceFormed = 85, BeaconPlaced = 86, OrderRefused = 87,
    VoyageLost = 88, IslandSettled = 89,

    DecreeFound = 90, DecreeExpedition = 91, DecreeMine = 92, DecreeLevy = 93, DecreeCorvee = 94, DecreeCompact = 95,
    DecreeRefused = 96, ExpeditionReturned = 97, CompactSigned = 98, CompactEnded = 99, EmbargoSet = 100, EmbargoLifted = 101,
    CaravanRaided = 102, CaravanRaidFailed = 103, TinFolkAppeared = 104, PactSigned = 105, TreatySigned = 106, PactBroken = 107,
    VillageJoined = 108, EmbargoOnUs = 109, DecreePact = 110,
}

/// <summary>Stable dotted names for <see cref="ChronicleKey"/> values: the keys of the front end's localisation table.</summary>
public static class ViewText
{
    /// <summary>"chronicle.raid.succeeded" and the like; "" for an unknown value.</summary>
    public static string Key(ChronicleKey key) => key switch
    {
        ChronicleKey.SeasonSpring => "chronicle.season.spring",
        ChronicleKey.SeasonSummer => "chronicle.season.summer",
        ChronicleKey.SeasonAutumn => "chronicle.season.autumn",
        ChronicleKey.SeasonWinter => "chronicle.season.winter",
        ChronicleKey.SeasonHarshWinter => "chronicle.season.harsh_winter",
        ChronicleKey.Births => "chronicle.people.births",
        ChronicleKey.Deaths => "chronicle.people.deaths",
        ChronicleKey.SettlementFounded => "chronicle.settlement.founded",
        ChronicleKey.FreeFolkCampFounded => "chronicle.settlement.free_folk_camp",
        ChronicleKey.StructureBuilt => "chronicle.structure.built",
        ChronicleKey.StructureBurned => "chronicle.structure.burned",
        ChronicleKey.FireOut => "chronicle.settlement.fire_out",
        ChronicleKey.FamineNow => "alert.famine.now",
        ChronicleKey.FamineForecast => "alert.famine.forecast",
        ChronicleKey.RaidIncoming => "alert.party.raid_incoming",
        ChronicleKey.AssaultIncoming => "alert.party.assault_incoming",
        ChronicleKey.PartyIncoming => "alert.party.incoming",
        ChronicleKey.SlotFilledByPlayer => "chronicle.research.slot_player",
        ChronicleKey.SlotFilledByCouncil => "chronicle.research.slot_council",
        ChronicleKey.SlotFilledFromQueue => "chronicle.research.slot_queue",
        ChronicleKey.CouncilPick => "chronicle.research.council_pick",
        ChronicleKey.ProjectCompleted => "chronicle.research.completed",
        ChronicleKey.MomentPlays => "moment.project.in_era",
        ChronicleKey.MomentBackfill => "moment.project.backfill",
        ChronicleKey.VisibleChange => "chronicle.research.visible_change",
        ChronicleKey.FirstClaimed => "banner.first.claimed",
        ChronicleKey.EraAdvanced => "banner.era.advanced",
        ChronicleKey.Adopted => "chronicle.research.adopted",
        ChronicleKey.ProjectDormant => "chronicle.research.dormant",
        ChronicleKey.ProjectRevived => "chronicle.research.revived",
        ChronicleKey.ResourceSeen => "chronicle.research.resource_seen",
        ChronicleKey.ProjectUnlocked => "chronicle.research.unlocked",
        ChronicleKey.Aware => "chronicle.contact.aware",
        ChronicleKey.ContactFriendly => "chronicle.contact.friendly",
        ChronicleKey.ContactWary => "chronicle.contact.wary",
        ChronicleKey.ContactHostile => "chronicle.contact.hostile",
        ChronicleKey.Skirmish => "chronicle.contact.skirmish",
        ChronicleKey.GiftSent => "chronicle.trade.gift",
        ChronicleKey.BarterDone => "chronicle.trade.barter",
        ChronicleKey.RaidLaunched => "chronicle.raid.launched",
        ChronicleKey.RaidSucceeded => "chronicle.raid.succeeded",
        ChronicleKey.RaidFailed => "chronicle.raid.failed",
        ChronicleKey.RaidSuffered => "chronicle.raid.suffered",
        ChronicleKey.RaidRepelled => "chronicle.raid.repelled",
        ChronicleKey.WarDeclaredByUs => "chronicle.war.declared_by_us",
        ChronicleKey.WarDeclaredOnUs => "chronicle.war.declared_on_us",
        ChronicleKey.PeaceMade => "chronicle.war.peace",
        ChronicleKey.AssaultWon => "chronicle.assault.won",
        ChronicleKey.AssaultLost => "chronicle.assault.lost",
        ChronicleKey.SettlementFell => "chronicle.assault.fell",
        ChronicleKey.SettlementHeld => "chronicle.assault.held",
        ChronicleKey.Occupied => "chronicle.occupation.began",
        ChronicleKey.OccupationBroken => "chronicle.occupation.broken",
        ChronicleKey.OccupationAbandoned => "chronicle.occupation.abandoned",
        ChronicleKey.SettlementCaptured => "chronicle.capture.taken",
        ChronicleKey.SettlementLost => "chronicle.capture.lost",
        ChronicleKey.FreeFolkFounded => "chronicle.capture.free_folk",
        ChronicleKey.AllianceFormed => "chronicle.alliance.formed",
        ChronicleKey.BeaconPlaced => "chronicle.beacon.placed",
        ChronicleKey.OrderRefused => "alert.order.refused",
        ChronicleKey.VoyageLost => "chronicle.voyage.lost",
        ChronicleKey.IslandSettled => "chronicle.settlement.island",
        ChronicleKey.DecreeFound => "chronicle.decree.found",
        ChronicleKey.DecreeExpedition => "chronicle.decree.expedition",
        ChronicleKey.DecreeMine => "chronicle.decree.mine",
        ChronicleKey.DecreeLevy => "chronicle.decree.levy",
        ChronicleKey.DecreeCorvee => "chronicle.decree.corvee",
        ChronicleKey.DecreeCompact => "chronicle.decree.compact",
        ChronicleKey.DecreePact => "chronicle.decree.pact",
        ChronicleKey.DecreeRefused => "alert.decree.refused",
        ChronicleKey.ExpeditionReturned => "chronicle.expedition.returned",
        ChronicleKey.CompactSigned => "chronicle.compact.signed",
        ChronicleKey.CompactEnded => "chronicle.compact.ended",
        ChronicleKey.EmbargoSet => "chronicle.embargo.set",
        ChronicleKey.EmbargoLifted => "chronicle.embargo.lifted",
        ChronicleKey.EmbargoOnUs => "alert.embargo.on_us",
        ChronicleKey.CaravanRaided => "chronicle.caravan.raided",
        ChronicleKey.CaravanRaidFailed => "chronicle.caravan.raid_failed",
        ChronicleKey.TinFolkAppeared => "banner.tin_folk.appeared",
        ChronicleKey.PactSigned => "chronicle.pact.signed",
        ChronicleKey.TreatySigned => "chronicle.treaty.signed",
        ChronicleKey.PactBroken => "chronicle.pact.broken",
        ChronicleKey.VillageJoined => "chronicle.settlement.joined",
        _ => "",
    };
}

/// <summary>
/// One line for the Chronicle, a Moment or an alert (docs/MVP/08 §10, 18 §6). Subject ids and small numbers only: the
/// front end renders the text from <see cref="Text"/> and these fields. -1 means "none" for ids.
/// </summary>
public struct ViewEvent
{
    /// <summary>Strictly increasing over the whole match (1, 2, 3, ...); the reader uses it to skip events it has shown.</summary>
    public long Sequence;
    /// <summary>The simulation tick in which it happened (for derived events: the last tick before the frame).</summary>
    public long Tick;
    /// <summary>A figure (the innovator of a project), or -1.</summary>
    public long Figure;
    /// <summary>Dense project index in the content pack, or -1.</summary>
    public int Project;
    /// <summary>Economy settlement index, or -1.</summary>
    public int Settlement;
    /// <summary>The place to jump the camera to ("one click to the location", 08 §10.2), or -1.</summary>
    public int Tile;
    public int A, B, C;
    public ViewEventKind Kind;
    public ChronicleKey Text;
    public ViewEventTier Tier;
    /// <summary>The civilization whose record this is (each politics event comes once per side).</summary>
    public sbyte Civ;
    /// <summary>The other civilization involved, or -1.</summary>
    public sbyte Other;
}

/// <summary>A compact public explanation from a rival civilization's AI, retained for the match end screen.</summary>
public readonly record struct AiWhyEntry(long Tick, byte Civ, string Layer, string Kind, long ScoreRaw, string Reason);
