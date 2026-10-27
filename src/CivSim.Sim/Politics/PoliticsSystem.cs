using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

public enum PoliticsEventKind : byte
{
    /// <summary>A figure saw the other civilization. Other = civ.</summary>
    Aware,
    /// <summary>First contact. Other = civ, A = this side's <see cref="Stance"/>, B = the other side's.</summary>
    Contact,
    /// <summary>First blood between meeting parties. A = this side's casualties, B = the other's.</summary>
    Skirmish,
    /// <summary>A gift party arrived. Other = recipient, A = value.</summary>
    Gift,
    /// <summary>A barter trip completed. Other = partner, A = value given.</summary>
    Barter,
    /// <summary>A raid set out. Other = target, A = party size, B = target settlement.</summary>
    RaidLaunched,
    /// <summary>A raid ended. Other = target, A = 1 if it succeeded, B = loot value.</summary>
    RaidResolved,
    WarDeclared,
    PeaceMade,
    /// <summary>An assault on a settlement was fought. Other = defender, A = 1 if the attackers won, B = settlement.</summary>
    AssaultResolved,
    Occupied,
    OccupationBroken,
    /// <summary>A settlement changed hands. Other = former owner, A = settlement, B = people who stayed.</summary>
    Captured,
    FreeFolkFounded,
    AllianceFormed,
    BeaconPlaced,
    /// <summary>A player order was refused. Other = target, A = command kind, B = refusal reason.</summary>
    OrderRefused,
    /// <summary>A voyage turned back (docs/MVP/21 §3). Other = target civilization or -1, A = open-sea tiles on the route, B = what sailed: 0 gift, 1 barter, 2 raid, 3 assault, 4 settlers.</summary>
    VoyageLost,
    /// <summary>A settlement seceded and became Free Folk (docs/MVP/22 §8.3). Other = -1, A = settlement, B = its people.</summary>
    Seceded,

    // ---- Bronze: Decrees and trade (docs/MVP/22 §4, §6; WP8a). Append only. ----
    /// <summary>A Decree was carried out. Other = civilization concerned or -1, A = <see cref="DecreeKind"/>, B = subject (Found: new settlement; Expedition and Mine: tile; Levy: target settlement; Corvée: settlement; Compact: compact id or -1 for a pact or treaty).</summary>
    DecreeResolved,
    /// <summary>A Decree was refused (nothing spent). Other = civilization concerned or -1, A = <see cref="DecreeKind"/>, B = <see cref="OrderRefusalReason"/>.</summary>
    DecreeRefused,
    /// <summary>An expedition came home (or reported, with WAY-04). Other = -1, A = tiles revealed, B = target tile.</summary>
    ExpeditionReturned,
    /// <summary>A compact began. Other = partner civilization or -1 (a Free Folk village), A = compact id, B = <see cref="TradeGood"/> (+100 when this side sells).</summary>
    CompactSigned,
    /// <summary>A compact ended. Other = partner or -1, A = compact id, B = <see cref="CompactEndReason"/>.</summary>
    CompactEnded,
    /// <summary>An embargo was set or lifted. Other = the civilization embargoed (or embargoing, on its record), A = <see cref="TradeGood"/>, B = 1 on, 0 off.</summary>
    EmbargoSet,
    /// <summary>A caravan was raided. Other = the raider (on the traders' records) or the seller (on the raider's), A = compact id, B = loot in the good's unit, permille.</summary>
    CaravanRaided,
    /// <summary>Tin Folk appeared (docs/MVP/22 §6.4). Other = -1, A = settlement, B = the placer tile. One record per civilization.</summary>
    TinFolkAppeared,
    /// <summary>A non-aggression pact or border treaty was signed. Other = partner, A = <see cref="CompactVariant"/>, B = seasons it runs.</summary>
    PactSigned,
    /// <summary>A pact was broken by war. Other = partner, A = <see cref="CompactVariant"/>, B = 1 on the breaker's record.</summary>
    PactBroken,
    /// <summary>A Free Folk village accepted an Invite and joined. Other = -1, A = settlement, B = people.</summary>
    VillageJoined,
    // Siege investment outcomes (docs/MVP/22 §7.4). Append only.
    InvestmentBegan,
    InvestmentRelieved,
    InvestmentCapitulated,
    InvestmentSally,
    /// <summary>Some besiegers deserted with no carried food. Other = target civilization, A = settlement, B = remaining members.</summary>
    InvestmentDeserted,
    /// <summary>An assault expedition launched. Other = target civilization, A = party size, B = target settlement.</summary>
    AssaultLaunched,
    /// <summary>Telemetry baseline for an investment. Other = target civilization, A = settlement, B = initial members.</summary>
    InvestmentTelemetryStarted,
    /// <summary>A raid on a mining camp set out. Other = target civilization, A = party size, B = target tile.</summary>
    MiningCampRaidLaunched,
}

/// <summary>Diagnostic-only mining-camp claim-radius alternatives for frontier projection.</summary>
public enum MiningCampClaimRadiusProjection : byte
{
    Current,
    OneTileSmaller,
    RadiusZero,
    Omitted,
}

/// <summary>Stable reason codes for a rejected player order.</summary>
public enum OrderRefusalReason : byte
{
    InvalidTarget = 1, RequirementsNotMet = 2, InvalidState = 3,
    // Decrees (docs/MVP/22 §4.5); append only.
    NoMandate = 4, NoDonor = 5, NoSite = 6, OutOfReach = 7, Rejected = 8,
}

/// <summary>Something that happened between civilizations, for the Chronicle (docs/MVP/13 §10).</summary>
public readonly record struct PoliticsEvent(long Tick, byte Civ, PoliticsEventKind Kind, int Other, int A, int B);

/// <summary>What a party on the road is doing. Explore is the Expedition Decree, Caravan a compact delivery (docs/MVP/22 §4.3, §6.3). Append only.</summary>
public enum ExpeditionKind : byte { Gift, Barter, Raid, Assault, Explore, Caravan }

/// <summary>A party on the road: it arrives at <see cref="ArriveSeason"/>.</summary>
public sealed class Expedition
{
    public ExpeditionKind Kind;
    public int Civ;
    public int TargetCiv;
    public int FromSettlement;
    public int ToSettlement;
    public int ArriveSeason;
    /// <summary>Barter: 0 outbound, 1 returning with the partner's goods.</summary>
    public int Leg;
    public Fix64 Value;
    public readonly List<long> Members = new();
    public readonly List<Fix64> Strengths = new();

    // Bronze fields (WP8a): default for the M5 kinds, so their hash and replay are unchanged.
    /// <summary>A stable id for caravans and explorers (-1 for the M5 kinds), what a raid on a caravan targets.</summary>
    public int Id = -1;
    /// <summary>An explorer's target tile, or the caravan a raid hunts (its id), else -1.</summary>
    public int TargetTile = -1;
    /// <summary>A raid on a caravan: the caravan's id (-1 for a raid on a settlement).</summary>
    public int TargetParty = -1;
    /// <summary>A raid on a mining camp: the camp's stable id, else -1.</summary>
    public int TargetCampId = -1;
    /// <summary>Goods carried: a caravan's or a goods-barter load (<see cref="TradeGood"/>), with <see cref="Cargo"/> in the good's unit.</summary>
    public TradeGood Good;
    public Fix64 Cargo;
    /// <summary>The compact a caravan serves, else -1.</summary>
    public int CompactId = -1;
    /// <summary>A Free Folk village that sent the caravan (its seller), else -1; the buyer's village is <see cref="ToSettlement"/>.</summary>
    public int FolkSeller = -1;
    /// <summary>Additional warriors travelling with this caravan as escorts; the first two members are its carriers.</summary>
    public int EscortCount;
    /// <summary>Tiles an explorer has seen and will report on its return (sorted).</summary>
    public readonly List<int> Revealed = new();

    internal bool HasBronzeFields => Id >= 0 || TargetTile >= 0 || TargetParty >= 0 || TargetCampId >= 0 || Good != TradeGood.None || Cargo != Fix64.Zero ||
                                     CompactId >= 0 || FolkSeller >= 0 || EscortCount > 0 || Revealed.Count > 0;
}

/// <summary>Attackers holding a taken settlement tile until it is theirs (docs/MVP/13 §8.2).</summary>
public sealed class Occupation
{
    public int Civ;
    public int Settlement;
    public int SinceSeason;
    public int RequiredSeasons;
    public readonly List<long> Members = new();
    public readonly List<Fix64> Strengths = new();
}

/// <summary>
/// Territory, contact and conflict (M5; docs/MVP/05 §11 to §12, 07b, 13). Each tick it advances scout routes and direct
/// sighting contacts. Once a season it recomputes claims and ownership, notices rivals, moves parties on the road, and
/// lets each civilization's Civ Mind (a plain rule set until M6) choose gifts, barter, raids and wars. The same rules
/// run for every civilization; the player steers with Posture and Beacons.
/// </summary>
public sealed partial class PoliticsSystem : IFissionSiteAdvisor, IFreeFolkRegistry, ITileOwnership, IFreeFolkStatus
{
    private static readonly ulong ContactStream = Hash64.OfString("politics.contact");
    private static readonly ulong SkirmishStream = Hash64.OfString("politics.skirmish");
    private static readonly ulong RaidStream = Hash64.OfString("politics.raid");
    private static readonly ulong ForecastStream = Hash64.OfString("politics.forecast");
    private static readonly ulong AssaultStream = Hash64.OfString("politics.assault");
    private static readonly ulong CaptureStream = Hash64.OfString("politics.capture");

    /// <summary>Tiles a party covers in a season on foot.</summary>
    public const int TilesPerSeason = 10;
    /// <summary>Radius around a campaign's chosen origin from which settlements may contribute fighters.</summary>
    public const int CampaignGatherRange = 12;
    /// <summary>Fighters each settlement must keep home when contributing to a campaign.</summary>
    public const int CampaignHomeReserve = 4;
    /// <summary>Minimum field army size for a probability-gated campaign launch.</summary>
    public const int CampaignMinimumAttackers = 6;

    private readonly LivingEconomyModel _economy;
    private readonly ResearchSystem _research;
    private readonly ResearchCatalog _catalog;
    private readonly GeneratedWorld _generated;
    private readonly int _width, _height, _civs;
    private readonly ulong _seed;
    private readonly bool _gentleStart;
    private readonly bool[] _passable;
    private readonly WorldTile[] _tileArray;
    private readonly EconomyArchetype[] _archetypes;
    private readonly int[] _eraReach, _eraRadius;
    private readonly Fix64[] _eraClaimMult;
    private readonly Fix64[] _raidMargin;
    private readonly Territory _territory;
    private readonly Diplomacy _dip;
    private readonly byte[] _posture;
    private readonly Fix64?[] _warlordE3ExpandFissionThresholdOverride;
    private readonly bool[] _externalMind;
    private readonly bool[] _maxCampaignOriginForDiagnostics;
    private readonly List<int>[] _beacons;
    private readonly List<(int Tile, int Settlement, int Civ)> _camps = new();
    private readonly List<bool> _campsPlaced = new();
    private readonly List<bool> _free = new();
    private readonly List<Fix64> _cohesionDebt = new();
    private readonly List<Expedition> _expeditions = new();
    private readonly List<Occupation> _occupations = new();
    private readonly List<PoliticsEvent> _events = new();
    private readonly int[] _failedCampaigns;
    private readonly int[] _warSince;
    private readonly P _p;
    private int _season;
    private long _tick;

    // Per-season caches of the economy's settlements.
    private int[] _sCiv = Array.Empty<int>(), _sTile = Array.Empty<int>(), _sPop = Array.Empty<int>();
    private int _sCount;

    public int RaidCount { get; private set; }
    public int RaidsSucceeded { get; private set; }
    public int Gifts { get; private set; }
    public int BarterTrips { get; private set; }
    public int CaptureCount { get; private set; }
    public int Skirmishes { get; private set; }
    public int Wars { get; private set; }

    /// <summary>The projects politics reads, resolved to dense indices once.</summary>
    private readonly struct P
    {
        public readonly int Way01, Way02, Way03, Trn02, Ter02, Trd01, Trd02, Dip01, Dip03, Com01, Com02, Com04,
            Def01, Def02, Def03, Hus01, War01, War02, War03, War04, War05, War06, Def04, Def05,
            Hnt02, Hnt03, Hnt05, Hnt06, Gov02, Gov03, Art05, Bel01, Sea04, Fod07;

        public P(ResearchCatalog c)
        {
            Way01 = c.IndexOf("WAY-01"); Way02 = c.IndexOf("WAY-02"); Way03 = c.IndexOf("WAY-03"); Trn02 = c.IndexOf("TRN-02");
            Ter02 = c.IndexOf("TER-02"); Trd01 = c.IndexOf("TRD-01"); Trd02 = c.IndexOf("TRD-02"); Dip01 = c.IndexOf("DIP-01");
            Dip03 = c.IndexOf("DIP-03"); Com01 = c.IndexOf("COM-01"); Com02 = c.IndexOf("COM-02"); Com04 = c.IndexOf("COM-04");
            Def01 = c.IndexOf("DEF-01"); Def02 = c.IndexOf("DEF-02"); Def03 = c.IndexOf("DEF-03"); Hus01 = c.IndexOf("HUS-01");
            War01 = c.IndexOf("WAR-01"); War02 = c.IndexOf("WAR-02"); War03 = c.IndexOf("WAR-03"); War04 = c.IndexOf("WAR-04");
            War05 = c.IndexOf("WAR-05"); War06 = c.IndexOf("WAR-06"); Def04 = c.IndexOf("DEF-04"); Def05 = c.IndexOf("DEF-05");
            Hnt02 = c.IndexOf("HNT-02"); Hnt03 = c.IndexOf("HNT-03"); Hnt05 = c.IndexOf("HNT-05"); Hnt06 = c.IndexOf("HNT-06");
            Gov02 = c.IndexOf("GOV-02"); Gov03 = c.IndexOf("GOV-03"); Art05 = c.IndexOf("ART-05"); Bel01 = c.IndexOf("BEL-01");
            Sea04 = c.IndexOf("SEA-04"); Fod07 = c.IndexOf("FOD-07");
        }
    }

    public PoliticsSystem(LivingEconomyModel economy, ResearchSystem research, GeneratedWorld generated,
        ReadOnlySpan<EconomyArchetype> archetypes, ulong seed, bool gentleStart = false)
    {
        _economy = economy;
        _research = research;
        _catalog = research.Catalog;
        _generated = generated;
        _width = generated.Width;
        _height = generated.Height;
        _civs = economy.CivilizationCount;
        _seed = seed;
        _gentleStart = gentleStart;
        _archetypes = archetypes.ToArray();
        if (_archetypes.Length != _civs) throw new ArgumentException("One archetype per civilization.", nameof(archetypes));
        _p = new P(_catalog);
        _tileArray = generated.Tiles.ToArray();
        _passable = HexGrid.BuildWalkingPassability(_tileArray, _width, _height);
        InitVoyages();
        _territory = new Territory(generated, _civs);
        _dip = new Diplomacy(_civs);
        _posture = new byte[_civs];
        _warlordE3ExpandFissionThresholdOverride = new Fix64?[_civs];
        _externalMind = new bool[_civs];
        _maxCampaignOriginForDiagnostics = new bool[_civs];
        _beacons = new List<int>[_civs];
        for (int c = 0; c < _civs; c++) _beacons[c] = new List<int>();
        _failedCampaigns = new int[_civs * _civs];
        _warSince = new int[_civs * _civs];

        _eraReach = new int[ResearchCatalog.EraCount];
        _eraRadius = new int[ResearchCatalog.EraCount];
        _eraClaimMult = new Fix64[ResearchCatalog.EraCount];
        foreach (ContentValue era in _catalog.Pack.Root.Get("registries")!.Get("eras")!.Items)
        {
            int id = (int)era.GetInt("id")!;
            _eraReach[id] = (int)era.GetInt("reach")!;
            _eraRadius[id] = (int)era.GetInt("claim_radius")!;
            _eraClaimMult[id] = era.GetFix("claim_mult")!.Value;
        }
        _raidMargin = new Fix64[_civs];
        for (int c = 0; c < _civs; c++) _raidMargin[c] = MarginFor(_archetypes[c]);

        economy.SiteAdvisor = this;
        economy.TileOwnership = this;
        economy.FreeFolkStatus = this;
        InitializeScoutSimulation();
        InitDecrees();
        RefreshSettlements();
        ApplyEconomyInputs();
    }

    /// <summary>The success share a civilization wants before it raids (13 §5.5; the archetypes registry's raid margin: high needs the best odds).</summary>
    private Fix64 MarginFor(EconomyArchetype archetype)
    {
        string id = archetype.ToString().ToLowerInvariant();
        foreach (ContentValue a in _catalog.Pack.Root.Get("registries")!.Get("archetypes")!.Items)
            if (a.GetString("id") == id)
                return a.GetString("raid_margin") switch
                {
                    "high" => Fix64.FromRatio(65, 100),
                    "low" => Fix64.FromRatio(35, 100),
                    _ => Fix64.FromRatio(50, 100),
                };
        return Fix64.FromRatio(50, 100);
    }

    // ---- public surface -------------------------------------------------------------------

    /// <summary>Optional diagnostic sink (the harness sets it); never read by the simulation.</summary>
    public Action<string>? Trace { get; set; }

    public Territory Territory => _territory;
    public Diplomacy Diplomacy => _dip;
    public IReadOnlyList<PoliticsEvent> Events => _events;
    public void ClearEvents() => _events.Clear();
    internal void UseMaxCampaignOriginForDiagnostics(int civ, bool enabled) => _maxCampaignOriginForDiagnostics[civ] = enabled;

    internal void SetWarlordE3ExpandFissionThresholdOverrideForDiagnostics(int civ, Fix64 threshold)
    {
        if ((uint)civ >= (uint)_civs) throw new ArgumentOutOfRangeException(nameof(civ));
        _warlordE3ExpandFissionThresholdOverride[civ] = threshold;
    }
    public int Season => _season;
    public Posture PostureOf(int civ) => (Posture)_posture[civ];
    public IReadOnlyList<int> BeaconsOf(int civ) => _beacons[civ];
    public IReadOnlyList<Expedition> Expeditions => _expeditions;
    public IReadOnlyList<Occupation> Occupations => _occupations;
    public bool IsFreeFolk(int settlementIndex) => settlementIndex < _free.Count && _free[settlementIndex];

    /// <summary>Beacons a civilization may hold: one from ART-05 Songlines, plus Era Boons (docs/MVP/05 §11.4).</summary>
    public int BeaconAllowance(int civ) => (_research.IsDone(civ, _p.Art05) ? 1 : 0) + _research.BeaconBoons(civ);

    /// <summary>First contact between two civilizations, whatever their reach (a test and scenario hook).</summary>
    public void ForceContact(int a, int b) => MakeContact(a, b);

    // ---- commands -------------------------------------------------------------------------

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        _tick = tick;
        foreach (Command command in commands)
        {
            if (command.Civ >= _civs) continue;
            int civ = command.Civ;
            switch (command.Kind)
            {
                case CommandKind.SetPosture:
                    // The Posture lever comes with Chieftainship (GOV-02); before it, Balanced is the only option.
                    if (_research.IsDone(civ, _p.Gov02) && command.A is >= 0 and <= (int)Posture.Raid)
                    {
                        _posture[civ] = (byte)command.A;
                        ApplyEconomyInputs();
                    }
                    break;
                case CommandKind.PlaceBeacon:
                {
                    if (command.A < 0 || command.A >= _width || command.B < 0 || command.B >= _height) break;
                    int beaconTile = command.B * _width + command.A;
                    List<int> beacons = _beacons[civ];
                    if (beacons.Count < BeaconAllowance(civ))
                    {
                        beacons.Add(beaconTile);
                    }
                    else if (command.C == 1 && beacons.Count > 0)
                    {
                        int nearest = 0;
                        int nearestDistance = HexGrid.Distance(beacons[0], beaconTile, _width);
                        for (int i = 1; i < beacons.Count; i++)
                        {
                            int distance = HexGrid.Distance(beacons[i], beaconTile, _width);
                            if (distance >= nearestDistance) continue;
                            nearest = i;
                            nearestDistance = distance;
                        }
                        beacons[nearest] = beaconTile;
                    }
                    else break;
                    _events.Add(new PoliticsEvent(tick, (byte)civ, PoliticsEventKind.BeaconPlaced, -1, command.A, command.B));
                    break;
                }
                case CommandKind.RemoveBeacon:
                    if (command.A >= 0 && command.A < _beacons[civ].Count) _beacons[civ].RemoveAt(command.A);
                    break;
                // A human player's politics orders: the AI's Order* rules, checked here (docs/MVP/18 §7).
                case CommandKind.SendGift:
                {
                    bool invalidCiv = command.A < 0 || command.A >= _civs;
                    int destination = -1;
                    bool validDestination = !invalidCiv && TryDecodeOrderDestination(command.B, command.A, out destination);
                    if (invalidCiv || !validDestination || !OrderGift(civ, command.A, destination))
                        Refused(command, command.A, invalidCiv || !validDestination ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                }
                case CommandKind.SendBarter:
                {
                    bool invalidCiv = command.A < 0 || command.A >= _civs;
                    int destination = -1;
                    bool validDestination = !invalidCiv && TryDecodeOrderDestination(command.B, command.A, out destination);
                    if (invalidCiv || !validDestination || !OrderBarter(civ, command.A, destination))
                        Refused(command, command.A, invalidCiv || !validDestination ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                }
                case CommandKind.LaunchRaid:
                    // A < 0 targets a caravan: −(caravan id + 1) (docs/MVP/22 §6.3).
                    if (command.A < 0 && _economy.BronzeActive)
                    {
                        if (!OrderRaidCaravan(civ, -command.A - 1, command.B)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
                    }
                    else if (command.A < 0 || command.A >= _sCount) Refused(command, command.A, OrderRefusalReason.InvalidTarget);
                    else if (!OrderRaid(civ, command.A, command.B)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.LaunchRaidAgainst:
                    if (command.A < 0 || command.A >= _civs) Refused(command, command.A, OrderRefusalReason.InvalidTarget);
                    else if (!OrderRaidAgainst(civ, command.A, command.B)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.LaunchMiningCampRaid:
                    if (command.A < 0) Refused(command, command.A, OrderRefusalReason.InvalidTarget);
                    else if (!OrderRaidMiningCamp(civ, command.A, command.B)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.LaunchAssault:
                    if (command.A < 0 || command.A >= _sCount) Refused(command, command.A, OrderRefusalReason.InvalidTarget);
                    else if (!OrderAssault(civ, command.A)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.DeclareWar:
                    if (command.A < 0 || command.A >= _civs || !OrderWar(civ, command.A))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.MakePeace:
                    if (command.A < 0 || command.A >= _civs || !OrderPeace(civ, command.A))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.InvalidState);
                    break;
                case CommandKind.FormAlliance:
                    if (command.A < 0 || command.A >= _civs || !OrderAlliance(civ, command.A))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.RecallParty:
                    OrderRecallCommand(civ, command.A, command);
                    break;
                case CommandKind.IssueDecree:
                    QueueDecreeCommand(command);
                    break;
                case CommandKind.SetEmbargo:
                    if (!OrderEmbargo(civ, command.A, (TradeGood)Math.Clamp(command.B, 0, 255), command.C != 0))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs || command.A == civ ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
            }
        }
        AdvanceScoutSimulation(tick);
    }

    /// <summary>Expand lowers the threshold 20% before E3 and 17% in Bronze; Prosper raises it 20% (docs/MVP/05 §6.2; 07d B17).</summary>
    public void ApplyEconomyInputs()
    {
        for (int c = 0; c < _civs; c++)
        {
            int era = _research.Era(c);
            Fix64 expandScale = era == 3 ? Fix64.FromRatio(83, 100) : Fix64.FromRatio(8, 10);
            Posture posture = (Posture)_posture[c];
            if (era == 3 && posture == Posture.Expand && _warlordE3ExpandFissionThresholdOverride[c] is { } overrideScale)
                expandScale = overrideScale;
            _economy.SetFissionThresholdScale(c, posture switch
            {
                Posture.Expand => expandScale,
                Posture.Prosper => Fix64.FromRatio(12, 10),
                _ => Fix64.One,
            });
        }
    }

    // ---- fission site advice (IFissionSiteAdvisor) ---------------------------------------

    /// <summary>
    /// Rival-owned land is off limits, and so is ground within 23 terrain-weighted tiles of a rival settlement, just
    /// beyond what two E1 reaches span: bands settle where they can hold a claim, not on a neighbour's doorstep
    /// (docs/MVP/05 §9.4, 07b fission sites), and contact comes from reach, not from a daughter band wandering over.
    /// </summary>
    /// <summary>The owner of a tile for the economy's camp planner (docs/MVP/23 §7.2), -1 when unowned.</summary>
    int ITileOwnership.OwnerOf(int tile) => _tinFolk.Count > 0 && FreeFolkOwns(tile) ? -2 : _territory.OwnerOf(tile);

    public bool Allows(int civ, int tile)
    {
        int owner = _territory.OwnerOf(tile);
        if (owner != -1 && owner != civ) return false;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] == civ || _free[s]) continue;
            if (RivalCost(s)[tile] < MinRivalCostTenths) return false;
        }
        // Tin Folk land is theirs: daughters keep the founding distance from a free village (docs/MVP/22 §4.3, §6.4).
        foreach (TinFolkVillage v in _tinFolk)
            if (v.Joined < 0 && (uint)v.Settlement < (uint)_sCount && _free[v.Settlement] &&
                HexGrid.Distance(_sTile[v.Settlement], tile, _width) < _rules.RivalMinDist) return false;
        return true;
    }

    private const int MinRivalCostTenths = 230;
    private int[]?[] _rivalCost = new int[]?[0];
    private int _rivalCostSeason = -1;

    private int[] RivalCost(int settlement)
    {
        if (_rivalCostSeason != _season || _rivalCost.Length < _sCount)
        {
            _rivalCost = new int[]?[Math.Max(_sCount, 8)];
            _rivalCostSeason = _season;
        }
        return _rivalCost[settlement] ??= HexGrid.WalkingCostsFrom(_tileArray, _width, _height, _sTile[settlement], MinRivalCostTenths);
    }

    /// <summary>
    /// Site preference for a daughter band (docs/MVP/05 §9.4: "tile quality × distance fit × Beacon bias − rival pressure"):
    /// a Beacon within 15 tiles gains up to +50% by how close it is, and sites within 16 tiles of a rival settlement lose up
    /// to half, so bands do not wander into a neighbour's doorstep before contact.
    /// </summary>
    public Fix64 Multiplier(int civ, int tile)
    {
        Fix64 best = Fix64.Zero;
        foreach (int beacon in _beacons[civ])
        {
            int d = HexGrid.Distance(beacon, tile, _width);
            if (d >= 15) continue;
            Fix64 pull = Fix64.FromRatio(15 - d, 15);
            if (pull > best) best = pull;
        }
        Fix64 factor = Fix64.One + best / 2;
        int nearestRival = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] == civ || _free[s]) continue;
            nearestRival = Math.Min(nearestRival, HexGrid.Distance(_sTile[s], tile, _width));
        }
        if (nearestRival < RivalPressureRange)
            factor = factor * (Fix64.Half + Fix64.FromRatio(nearestRival, 2 * RivalPressureRange));
        // an island nobody of ours holds yet is an early-reach prize (docs/MVP/12 §3.1, 21 §3)
        if (IsIsland(tile) && !HasVillageOnLandmass(civ, tile)) factor = factor * Fix64.FromRatio(3, 2);
        return factor;
    }

    private const int RivalPressureRange = 16;

    // ---- the season -----------------------------------------------------------------------

    /// <summary>Call after the economy and research have finished their season.</summary>
    public void OnSeasonEnd(long tick)
    {
        _tick = tick;
        _season = checked((int)_economy.SeasonsCompleted);
        RefreshSettlements();
        TakeSecessions();
        SpawnTinFolk();
        PlaceCamps();
        UpdateTerritory();
        if (_season % 4 == 0)
        {
            _territory.AccrueDominion();
            _dip.YearlyDrift();
        }
        DetectContacts();
        ResolveExpeditions();
        AdvanceInvestments();
        AdvanceOccupations();
        RecoverCohesion();
        RunCivMinds();
        RunTrade();
        ResolveDecrees();
        DriftOverlap();
        ApplyEconomyInputs();
    }

    private void RefreshSettlements()
    {
        _sCount = _economy.SettlementCount;
        if (_sCiv.Length < _sCount)
        {
            int size = Math.Max(_sCount, _sCiv.Length * 2);
            _sCiv = new int[size]; _sTile = new int[size]; _sPop = new int[size];
        }
        while (_free.Count < _sCount) _free.Add(false);
        while (_campsPlaced.Count < _sCount) _campsPlaced.Add(false);
        while (_cohesionDebt.Count < _sCount) _cohesionDebt.Add(Fix64.Zero);
        for (int i = 0; i < _sCount; i++)
        {
            var v = _economy.SettlementLite(i);
            _sCiv[i] = v.Civilization;
            _sTile[i] = v.SiteTile;
            _sPop[i] = _economy.Population(v.Id);
        }
    }

    /// <summary>
    /// Routine reach in tiles: the era's value from the eras registry (6, 9, 12 ...). The table already folds in the
    /// scouting projects (WAY-01 to WAY-03, TRN-02), as the territory model that sets contact at the start of E2 assumes
    /// (docs/MVP/07b §2, 13 §2.2); adding them again would put first contact in E1.
    /// </summary>
    public int ReachOf(int civ) => _eraReach[Math.Min(_research.Era(civ), _eraReach.Length - 1)];

    private Fix64 EmissionOf(int settlement)
    {
        int civ = _sCiv[settlement];
        Fix64 cohesion = _economy.SettlementCohesion(settlement);
        return Fix64.Sqrt(Fix64.FromInt(Math.Max(1, _sPop[settlement]))) * _eraClaimMult[Math.Min(_research.Era(civ), _eraClaimMult.Length - 1)] * cohesion;
    }

    private void PlaceCamps()
    {
        for (int s = 0; s < _sCount; s++)
        {
            int civ = _sCiv[s];
            if (_campsPlaced[s] || _free[s]) continue;
            int era = _research.Era(civ);
            if (era > 3 || !_research.IsDone(civ, _p.Way02)) continue;
            var own = new List<ClaimSource>();
            var rivals = new List<ClaimSource>();
            for (int i = 0; i < _sCount; i++)
            {
                if (_free[i]) continue;
                var source = new ClaimSource(_sCiv[i], _sTile[i], _eraRadius[Math.Min(_research.Era(_sCiv[i]), _eraRadius.Length - 1)], Fix64.Zero);
                if (_sCiv[i] == civ) own.Add(source); else rivals.Add(source);
            }
            var ownCamps = new List<ClaimSource>();
            foreach ((int tile, int _, int campCiv) in _camps) if (campCiv == civ) ownCamps.Add(new ClaimSource(civ, tile, Territory.CampRadius, Fix64.Zero));
            int radius = _eraRadius[Math.Min(era, _eraRadius.Length - 1)];
            foreach (int tile in _territory.ChooseCompactCampSites(civ, _sTile[s], radius, own, ownCamps, rivals, 2))
                _camps.Add((tile, s, civ));
            _campsPlaced[s] = true;
        }
    }

    /// <summary>
    /// Projects whole-map claimed habitable weight from current settlement sources, then seasonal camps, then mining
    /// camps. Results omit ownership hysteresis and are intended for diagnostics; live ownership is unchanged.
    /// </summary>
    public (int SettlementsOnly, int WithSeasonalCamps, int WithMiningCamps) ProjectedClaimedLandWeights()
    {
        var settlements = new List<ClaimSource>();
        var seasonalCamps = new List<ClaimSource>();
        var miningCamps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, seasonalCamps, miningCamps);
        var withSeasonal = new List<ClaimSource>(seasonalCamps);
        var withMining = new List<ClaimSource>(seasonalCamps.Count + miningCamps.Count);
        withMining.AddRange(seasonalCamps);
        withMining.AddRange(miningCamps);
        return (
            _territory.ProjectedClaimedWeight(settlements, Array.Empty<ClaimSource>()),
            _territory.ProjectedClaimedWeight(settlements, withSeasonal),
            _territory.ProjectedClaimedWeight(settlements, withMining));
    }

    /// <summary>Minimum unowned habitable weight on a land-only path between the projected settlement borders.</summary>
    public (int Weight, int[] Path) ProjectedSettlementOnlyFrontier(int civA, int civB)
    {
        var settlements = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, new List<ClaimSource>(), new List<ClaimSource>());
        var projection = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, Array.Empty<ClaimSource>(), civA, civB);
        return (projection.FrontierConnectionWeight, projection.FrontierPath);
    }

    /// <summary>
    /// Read-only frontier audit for known Found sites that pass the live execution rules. It uses the Strategist donor floor
    /// (FoundCheck's 60% minimum), projects at most 32 route-adjacent sites per civilization, and includes the Council's current
    /// choice when it falls outside the shortlist. Founding reduces donor population and cohesion without changing live state;
    /// optional ownership updates use fixed projected sources to measure how hysteresis settles the candidate borders.
    /// </summary>
    public FoundFrontierAudit ProjectedFoundSiteFrontier(int civA, int civB,
        Func<int, int, bool>? strategistBeliefAllows = null, int ownershipUpdatesProjected = 0)
    {
        if (ownershipUpdatesProjected < 0) throw new ArgumentOutOfRangeException(nameof(ownershipUpdatesProjected));
        var settlements = new List<ClaimSource>();
        var seasonalCamps = new List<ClaimSource>();
        var miningCamps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, seasonalCamps, miningCamps);
        var baselineCamps = new List<ClaimSource>(seasonalCamps.Count + miningCamps.Count);
        baselineCamps.AddRange(seasonalCamps);
        baselineCamps.AddRange(miningCamps);
        var baseline = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, baselineCamps, civA, civB);
        bool baselinePairTouching = ownershipUpdatesProjected == 0 ? baseline.PairOwnersTouching :
            _territory.ProjectedPairOwnersTouchingAfterUpdates(settlements, baselineCamps, civA, civB, ownershipUpdatesProjected);

        const int MaxNearbySitesPerCiv = 32;
        int maxClaimedWeight = (int)((long)_territory.TotalHabitableWeight * 42 / 100);
        var civSummaries = new List<FoundFrontierCivSummary>(2);
        var projections = new List<FoundFrontierCandidateProjection>(66);
        foreach (int civ in new[] { civA, civB })
        {
            int foundDecrees = DecreesDone(civ, DecreeKind.Found);
            int naturalFoundings = E3Foundings(civ) - foundDecrees;
            int mandate = _research.Mandate(civ);
            int mandateCap = _research.MandateCap(civ);
            int preferredTile = ChooseFoundSite(civ, CouncilDecreePolicy.FoundFill);
            bool aiGateOpen = _research.CouncilOn(civ) && _research.MandateUnlocked(civ) &&
                mandate >= mandateCap && DecreeCost(civ, DecreeKind.Found) <= mandate &&
                foundDecrees < CouncilDecreePolicy.MaxFoundsPerAge && naturalFoundings >= foundDecrees + 1;

            var legalSites = new List<(int Tile, int Donor, int DonorPopulation, int DaughterPopulation,
                Fix64 DonorFill, int PathDistance)>();
            int eraRadius = EraRadiusOf(civ);
            int executionLegalSiteCount = 0;
            for (int tile = 0; tile < _territory.TileCount; tile++)
            {
                if (!_economy.IsHabitableSite(tile) || !KnowsTile(civ, tile)) continue;
                if (FoundCheck(civ, tile, out int donor) != 0 || donor < 0) continue;
                executionLegalSiteCount++;
                if (strategistBeliefAllows is not null && !strategistBeliefAllows(civ, tile)) continue;
                Fix64 donorFill = _economy.PopulationFill(donor);
                int donorPopulation = _sPop[donor];
                int daughterPopulation = (Fix64.FromInt(donorPopulation) * _rules.FoundShare).RoundToInt();
                int pathDistance = DistanceToPath(tile, baseline.FrontierPath);
                legalSites.Add((tile, donor, donorPopulation, daughterPopulation, donorFill, pathDistance));
            }

            int relevantRadius = eraRadius + 1;
            var nearbySites = legalSites.Where(site => site.PathDistance <= relevantRadius)
                .OrderBy(site => site.PathDistance).ThenBy(site => site.Tile)
                .Take(MaxNearbySitesPerCiv).ToList();
            int relevantSiteCount = legalSites.Count(site => site.PathDistance <= relevantRadius);
            if (preferredTile >= 0 && legalSites.All(site => site.Tile != preferredTile) &&
                (strategistBeliefAllows is null || strategistBeliefAllows(civ, preferredTile)))
            {
                int donor = -1;
                if (FoundCheck(civ, preferredTile, out donor) == 0 && donor >= 0)
                {
                    int donorPopulation = _sPop[donor];
                    legalSites.Add((preferredTile, donor, donorPopulation,
                        (Fix64.FromInt(donorPopulation) * _rules.FoundShare).RoundToInt(),
                        _economy.PopulationFill(donor), DistanceToPath(preferredTile, baseline.FrontierPath)));
                }
            }
            if (preferredTile >= 0 && legalSites.FirstOrDefault(site => site.Tile == preferredTile) is var preferred &&
                preferred.Tile == preferredTile && nearbySites.All(site => site.Tile != preferredTile))
                nearbySites.Add(preferred);

            int projectedSiteCount = 0;
            foreach (var site in nearbySites.OrderBy(site => site.PathDistance).ThenBy(site => site.Tile))
            {
                if (site.DaughterPopulation < Math.Max(_rules.MinDaughter, 6) || site.DaughterPopulation >= site.DonorPopulation)
                    continue;
                var projection = ProjectFound(site.Donor, civ, site.Tile, site.DaughterPopulation,
                    settlements, seasonalCamps, miningCamps);
                projections.Add(new FoundFrontierCandidateProjection(civ, site.Tile, site.Donor,
                    site.DonorPopulation, site.DaughterPopulation, site.DonorFill, site.PathDistance,
                    projection.ClaimedWeight, projection.FrontierConnectionWeight,
                    projection.PairOwnersTouching, projection.ClaimedWeight <= maxClaimedWeight,
                    site.Tile == preferredTile));
                projectedSiteCount++;
            }
            civSummaries.Add(new FoundFrontierCivSummary(civ, executionLegalSiteCount, legalSites.Count,
                relevantSiteCount, projectedSiteCount, aiGateOpen, mandate, mandateCap, foundDecrees,
                naturalFoundings, preferredTile));
        }

        return new FoundFrontierAudit(baseline.ClaimedWeight, baseline.FrontierConnectionWeight,
            baselinePairTouching, ownershipUpdatesProjected, civSummaries.ToArray(), projections.ToArray());

        int DistanceToPath(int tile, IReadOnlyList<int> path)
        {
            int best = int.MaxValue;
            foreach (int pathTile in path)
                best = Math.Min(best, HexGrid.Distance(tile, pathTile, _width));
            return best;
        }

        (int ClaimedWeight, int FrontierConnectionWeight, bool PairOwnersTouching) ProjectFound(
            int donor, int civ, int siteTile, int daughterPopulation,
            IReadOnlyList<ClaimSource> baseSettlements, IReadOnlyList<ClaimSource> baseSeasonalCamps,
            IReadOnlyList<ClaimSource> baseMiningCamps)
        {
            int donorPopulationAfter = _sPop[donor] - daughterPopulation;
            Fix64 currentEmission = EmissionOf(donor);
            Fix64 parentCohesionAfter = Fix64.Max(Fix64.Parse("0.6"),
                _economy.SettlementCohesion(donor) - Fix64.Parse("0.05"));
            int era = Math.Min(_research.Era(civ), _eraClaimMult.Length - 1);
            Fix64 parentEmissionAfter = Fix64.Sqrt(Fix64.FromInt(Math.Max(1, donorPopulationAfter))) *
                _eraClaimMult[era] * parentCohesionAfter;
            Fix64 parentEmissionScale = currentEmission > Fix64.Zero
                ? parentEmissionAfter / currentEmission : Fix64.Zero;

            var projectedSettlements = new List<ClaimSource>(baseSettlements.Count + 1);
            int donorTile = _sTile[donor];
            foreach (ClaimSource source in baseSettlements)
                projectedSettlements.Add(source.Civ == civ && source.Tile == donorTile
                    ? source with { Emission = source.Emission * parentEmissionScale }
                    : source);

            var projectedCamps = new List<ClaimSource>(baseSeasonalCamps.Count + baseMiningCamps.Count);
            int seasonalIndex = 0;
            foreach ((int tile, int settlement, int campCiv) in _camps)
            {
                if (settlement >= _sCount || _free[settlement] || _sCiv[settlement] != campCiv) continue;
                ClaimSource source = baseSeasonalCamps[seasonalIndex++];
                projectedCamps.Add(settlement == donor
                    ? source with { Emission = source.Emission * parentEmissionScale }
                    : source);
            }
            projectedCamps.AddRange(baseMiningCamps);

            Fix64 daughterCohesion = Fix64.Clamp(Fix64.Parse("0.6") +
                Fix64.FromRatio(6, 1000) * _rules.StartCohesion, Fix64.Parse("0.6"), Fix64.Parse("1.2"));
            Fix64 daughterEmission = Fix64.Sqrt(Fix64.FromInt(daughterPopulation)) *
                _eraClaimMult[era] * daughterCohesion;
            if (Done(civ, _bp.Ter05)) daughterEmission *= _rules.ClaimBoost;
            projectedSettlements.Add(new ClaimSource(civ, siteTile, EraRadiusOf(civ), daughterEmission));

            var projection = _territory.ProjectedClaimedWeightAndFrontierCost(
                projectedSettlements, projectedCamps, civA, civB);
            return (projection.ClaimedWeight, projection.FrontierConnectionWeight,
                ownershipUpdatesProjected == 0 ? projection.PairOwnersTouching :
                    _territory.ProjectedPairOwnersTouchingAfterUpdates(projectedSettlements, projectedCamps,
                        civA, civB, ownershipUpdatesProjected));
        }
    }

    /// <summary>
    /// Diagnostic counterfactual: keeps settlement, hinterland, and mining-camp claims unchanged while overriding
    /// every seasonal camp's radius and emission scale. Uses current camp sites and the normal live-owner exclusion.
    /// </summary>
    public (int ClaimedWeight, int FrontierConnectionWeight, int[] FrontierPath) ProjectedSeasonalCampVariant(
        int radius, Fix64 emissionScale, int civA, int civB)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        if (emissionScale < Fix64.Zero || emissionScale > Fix64.One)
            throw new ArgumentOutOfRangeException(nameof(emissionScale));

        var settlements = new List<ClaimSource>();
        var seasonalCamps = new List<ClaimSource>();
        var miningCamps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, seasonalCamps, miningCamps);
        var variantCamps = new List<ClaimSource>(seasonalCamps.Count + miningCamps.Count);
        foreach (ClaimSource camp in seasonalCamps)
            variantCamps.Add(new ClaimSource(camp.Civ, camp.Tile, radius, camp.Emission * emissionScale));
        variantCamps.AddRange(miningCamps);
        var projection = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, variantCamps, civA, civB);
        return (projection.ClaimedWeight, projection.FrontierConnectionWeight, projection.FrontierPath);
    }

    /// <summary>
    /// Diagnostic counterfactual: replaces seasonal camp disks with one-tile claims along the settlement-only
    /// frontier path. Each half is assigned to its neighboring civilization and can be supported by that civ's
    /// nearest existing seasonal camp within <paramref name="supportRadius"/>. Mining-camp claims remain fixed.
    /// </summary>
    public (int ClaimedWeight, int FrontierConnectionWeight, int[] FrontierPath, int SupportedTiles,
        int UnsupportedTiles, int CorridorWeight) ProjectedSeasonalCampTrail(int supportRadius, int civA, int civB,
        MiningCampClaimRadiusProjection miningProjection = MiningCampClaimRadiusProjection.Current)
    {
        if (supportRadius < 0) throw new ArgumentOutOfRangeException(nameof(supportRadius));
        if (!Enum.IsDefined(miningProjection)) throw new ArgumentOutOfRangeException(nameof(miningProjection));

        var settlements = new List<ClaimSource>();
        var seasonalCamps = new List<ClaimSource>();
        var miningCamps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, seasonalCamps, miningCamps);
        var projectedMiningCamps = new List<ClaimSource>(miningCamps.Count);
        if (miningProjection != MiningCampClaimRadiusProjection.Omitted)
        foreach (ClaimSource camp in miningCamps)
        {
            int radius = miningProjection switch
            {
                MiningCampClaimRadiusProjection.Current => camp.Radius,
                MiningCampClaimRadiusProjection.OneTileSmaller => Math.Max(0, camp.Radius - 1),
                MiningCampClaimRadiusProjection.RadiusZero => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(miningProjection)),
            };
            projectedMiningCamps.Add(new ClaimSource(camp.Civ, camp.Tile, radius, camp.Emission));
        }
        var trailClaims = new List<ClaimSource>();
        var trailMetrics = AddSeasonalCampTrailClaims(settlements, seasonalCamps, supportRadius, civA, civB, trailClaims);
        trailClaims.AddRange(projectedMiningCamps);
        var projection = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, trailClaims, civA, civB);
        return (projection.ClaimedWeight, projection.FrontierConnectionWeight, projection.FrontierPath,
            trailMetrics.SupportedTiles, trailMetrics.UnsupportedTiles, trailMetrics.CorridorWeight);
    }

    /// <summary>
    /// Diagnostic snapshot counterfactual: relocates each existing seasonal camp group in settlement order using
    /// compact legal sites, while holding settlement claims, camp counts, emission, and mining claims fixed.
    /// </summary>
    public (int BaselineClaimedWeight, int CompactClaimedWeight, int BaselineFrontierConnectionWeight,
        int CompactFrontierConnectionWeight, int BaselineCampCount, int CompactCampCount, int ChangedSites,
        int SnapshotLegalOriginalSites, int ProtectedClaimedWeight, int ProtectedFrontierConnectionWeight,
        int ProtectedCampSites, int SearchClaimedWeight, int SearchFrontierConnectionWeight,
        int SearchChangedSites, int SearchSupportSlots, int SearchSupportPairEvaluations,
        int SearchOptimizedMoves, int SearchCoupledPairEvaluations, int SearchOptimizedPairs,
        int SearchEvaluations, bool SearchMeetsTargets, int RouteCandidateSlots, int RouteSingleEvaluations,
        int RoutePairEvaluations, int RouteBestClaimedWeight, int RouteBestFrontierConnectionWeight,
        bool RouteMeetsContactAndLandCap)
        ProjectedCompactCampPlacement(int civA, int civB)
    {
        var settlements = new List<ClaimSource>();
        var baselineSeasonalCamps = new List<ClaimSource>();
        var miningCamps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, baselineSeasonalCamps, miningCamps);

        var originalSites = new List<int>[_sCount];
        foreach ((int tile, int settlement, int civ) in _camps)
        {
            if (settlement >= _sCount || _free[settlement] || _sCiv[settlement] != civ) continue;
            (originalSites[settlement] ??= new List<int>()).Add(tile);
        }

        var compactSeasonalCamps = new List<ClaimSource>(baselineSeasonalCamps.Count);
        var compactSites = new List<int>[_sCount];
        for (int s = 0; s < _sCount; s++)
        {
            List<int>? existingSites = originalSites[s];
            if (existingSites is null || existingSites.Count == 0) continue;
            int civ = _sCiv[s];
            var own = new List<ClaimSource>();
            var rivals = new List<ClaimSource>();
            for (int i = 0; i < _sCount; i++)
            {
                if (_free[i]) continue;
                var source = new ClaimSource(_sCiv[i], _sTile[i],
                    _eraRadius[Math.Min(_research.Era(_sCiv[i]), _eraRadius.Length - 1)], Fix64.Zero);
                if (_sCiv[i] == civ) own.Add(source); else rivals.Add(source);
            }
            var ownCamps = new List<ClaimSource>();
            foreach (ClaimSource camp in compactSeasonalCamps)
                if (camp.Civ == civ) ownCamps.Add(camp);
            List<int> sites = _territory.ChooseCompactCampSites(civ, _sTile[s],
                _eraRadius[Math.Min(_research.Era(civ), _eraRadius.Length - 1)], own, ownCamps, rivals, existingSites.Count);
            compactSites[s] = sites;
            Fix64 share = _research.IsDone(civ, _p.Ter02) ? Fix64.FromRatio(3, 4) : Fix64.Half;
            Fix64 emission = EmissionOf(s) * share;
            foreach (int tile in sites)
                compactSeasonalCamps.Add(new ClaimSource(civ, tile, Territory.CampRadius, emission));
        }

        var baselineCamps = new List<ClaimSource>(baselineSeasonalCamps.Count + miningCamps.Count);
        baselineCamps.AddRange(baselineSeasonalCamps);
        baselineCamps.AddRange(miningCamps);
        var compactCamps = new List<ClaimSource>(compactSeasonalCamps.Count + miningCamps.Count);
        compactCamps.AddRange(compactSeasonalCamps);
        compactCamps.AddRange(miningCamps);
        var baseline = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, baselineCamps, civA, civB);
        var compact = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, compactCamps, civA, civB);

        var slots = new List<(int Settlement, int Civ, int OriginalTile, int CompactTile, Fix64 Emission)>(baselineSeasonalCamps.Count);
        var groupSlot = new int[_sCount];
        var snapshotLegalSlots = new List<bool>(baselineSeasonalCamps.Count);
        int snapshotLegalOriginalSites = 0;
        foreach ((int tile, int settlement, int civ) in _camps)
        {
            if (settlement >= _sCount || _free[settlement] || _sCiv[settlement] != civ) continue;
            int withinGroup = groupSlot[settlement]++;
            List<int>? replacements = compactSites[settlement];
            int compactTile = replacements is not null && withinGroup < replacements.Count
                ? replacements[withinGroup]
                : tile;
            Fix64 share = _research.IsDone(civ, _p.Ter02) ? Fix64.FromRatio(3, 4) : Fix64.Half;
            var slot = (settlement, civ, tile, compactTile, EmissionOf(settlement) * share);
            slots.Add(slot);

            var rivals = new List<ClaimSource>();
            for (int i = 0; i < _sCount; i++)
            {
                if (_free[i] || _sCiv[i] == civ) continue;
                rivals.Add(new ClaimSource(_sCiv[i], _sTile[i],
                    _eraRadius[Math.Min(_research.Era(_sCiv[i]), _eraRadius.Length - 1)], Fix64.Zero));
            }
            bool legal = _territory.IsCampSiteLegal(civ, _sTile[settlement], tile,
                _eraRadius[Math.Min(_research.Era(civ), _eraRadius.Length - 1)], rivals);
            snapshotLegalSlots.Add(legal);
            if (legal)
                snapshotLegalOriginalSites++;
        }

        bool[] supportsBaselinePath = _territory.ProjectedCampSupportsPairPath(
            settlements, baselineCamps, baseline.FrontierPath, civA, civB);
        var protectedSites = new int[slots.Count];
        var protectedSlots = new bool[slots.Count];
        int protectedCampSites = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            bool preserve = i < baselineSeasonalCamps.Count && i < supportsBaselinePath.Length &&
                supportsBaselinePath[i] && snapshotLegalSlots[i];
            protectedSites[i] = preserve ? slots[i].OriginalTile : slots[i].CompactTile;
            protectedSlots[i] = preserve;
            if (preserve) protectedCampSites++;
        }
        const int MaxSearchEvaluations = 200;
        const int MaxSupportCandidatesPerSlot = 8;
        const int MaxFullMovesPerPass = 24;
        int searchEvaluations = 2; // current and compact endpoint projections above
        int supportPairEvaluations = 0;
        int optimizedMoves = 0;
        int coupledPairEvaluations = 0;
        int optimizedPairs = 0;
        int landCap = (int)((long)_territory.TotalHabitableWeight * 42 / 100);
        int pathLimit = baseline.FrontierConnectionWeight >= 0
            ? Math.Min(600, baseline.FrontierConnectionWeight)
            : 600;
        bool PathIsAcceptable(int path) => path >= 0 && path <= pathLimit;

        var protectedProjection = ProjectSites(protectedSites);
        int[] bestSites = (int[])protectedSites.Clone();
        var bestProjection = PathIsAcceptable(protectedProjection.FrontierConnectionWeight)
            ? protectedProjection
            : (baseline.ClaimedWeight, baseline.FrontierConnectionWeight);
        if (!PathIsAcceptable(protectedProjection.FrontierConnectionWeight))
        {
            for (int i = 0; i < slots.Count; i++) bestSites[i] = slots[i].OriginalTile;
        }
        if (PathIsAcceptable(baseline.FrontierConnectionWeight) &&
            (baseline.ClaimedWeight < bestProjection.ClaimedWeight ||
                baseline.ClaimedWeight == bestProjection.ClaimedWeight && baseline.FrontierConnectionWeight < bestProjection.FrontierConnectionWeight))
        {
            bestProjection = (baseline.ClaimedWeight, baseline.FrontierConnectionWeight);
            for (int i = 0; i < slots.Count; i++) bestSites[i] = slots[i].OriginalTile;
        }
        var pairBaseSites = (int[])protectedSites.Clone();

        // Diagnostic only: test route-touching locations for any legal camp slot from the target pair.
        // This intentionally allows a small claim increase, provided the whole map remains under the 42% cap.
        const int MaxRouteCandidatesPerSlot = 4;
        const int MaxRouteSearchEvaluations = 180;
        const int MaxRouteSingleProbes = 48;
        int routeSearchEvaluations = 0, routeSingleEvaluations = 0, routePairEvaluations = 0;
        int routeBestClaimedWeight = baseline.ClaimedWeight;
        int routeBestFrontierConnectionWeight = baseline.FrontierConnectionWeight;
        var routeSlots = new List<(int Slot, List<(int Tile, int Delta)> Candidates, int BestClaimed, int BestPath)>();
        foreach (int slot in Enumerable.Range(0, slots.Count))
        {
            if (slots[slot].Civ != civA && slots[slot].Civ != civB) continue;
            List<(int Tile, int Delta)> candidates = LegalCampCandidates(slot, pairBaseSites,
                baseline.FrontierPath, routeOnly: true, MaxRouteCandidatesPerSlot);
            if (candidates.Count > 0) routeSlots.Add((slot, candidates, int.MaxValue, int.MaxValue));
        }
        var routeSingleProbes = new List<(int Slot, int Tile, int Delta)>();
        foreach (var candidateSlot in routeSlots)
            foreach ((int tile, int delta) in candidateSlot.Candidates)
                routeSingleProbes.Add((candidateSlot.Slot, tile, delta));
        routeSingleProbes.Sort(static (a, b) => a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) :
            a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Tile.CompareTo(b.Tile));
        if (routeSingleProbes.Count > MaxRouteSingleProbes)
            routeSingleProbes.RemoveRange(MaxRouteSingleProbes, routeSingleProbes.Count - MaxRouteSingleProbes);

        for (int i = 0; i < routeSingleProbes.Count && routeSearchEvaluations < MaxRouteSearchEvaluations; i++)
        {
            (int slot, int tile, _) = routeSingleProbes[i];
            int[] trialSites = (int[])pairBaseSites.Clone();
            trialSites[slot] = tile;
            var trial = ProjectRouteSites(trialSites);
            routeSingleEvaluations++;
            int routeSlotIndex = routeSlots.FindIndex(candidateSlot => candidateSlot.Slot == slot);
            routeSlots[routeSlotIndex] = BetterRouteSlot(routeSlots[routeSlotIndex], trial.ClaimedWeight, trial.FrontierConnectionWeight);
            UpdateRouteBest(trial.ClaimedWeight, trial.FrontierConnectionWeight);
            if (RouteMeetsTargets(trial.ClaimedWeight, trial.FrontierConnectionWeight)) break;
        }

        if (!RouteMeetsTargets(routeBestClaimedWeight, routeBestFrontierConnectionWeight))
        {
            var pairSlots = routeSlots.Where(candidateSlot => candidateSlot.BestPath != int.MaxValue)
                .OrderBy(candidateSlot => candidateSlot.BestPath).ThenBy(candidateSlot => candidateSlot.BestClaimed)
                .ThenBy(candidateSlot => candidateSlot.Slot).Take(10).ToArray();
            for (int firstIndex = 0; firstIndex < pairSlots.Length && routeSearchEvaluations < MaxRouteSearchEvaluations; firstIndex++)
            for (int secondIndex = firstIndex + 1; secondIndex < pairSlots.Length && routeSearchEvaluations < MaxRouteSearchEvaluations; secondIndex++)
            {
                var first = pairSlots[firstIndex];
                var second = pairSlots[secondIndex];
                foreach ((int firstTile, _) in first.Candidates)
                foreach ((int secondTile, _) in second.Candidates)
                {
                    if (routeSearchEvaluations >= MaxRouteSearchEvaluations) break;
                    if (firstTile == secondTile && slots[first.Slot].Settlement == slots[second.Slot].Settlement) continue;
                    int[] trialSites = (int[])pairBaseSites.Clone();
                    trialSites[first.Slot] = firstTile;
                    trialSites[second.Slot] = secondTile;
                    if (HasDuplicateParentCenters(trialSites)) continue;
                    var trial = ProjectRouteSites(trialSites);
                    routePairEvaluations++;
                    UpdateRouteBest(trial.ClaimedWeight, trial.FrontierConnectionWeight);
                    if (RouteMeetsTargets(trial.ClaimedWeight, trial.FrontierConnectionWeight)) break;
                }
                if (RouteMeetsTargets(routeBestClaimedWeight, routeBestFrontierConnectionWeight)) break;
            }
        }

        // Search at most two route-support camps together. Remaining support camps stay at their legal baseline sites.
        var supportSlots = new List<int>();
        for (int i = 0; i < protectedSlots.Length; i++) if (protectedSlots[i]) supportSlots.Add(i);
        if (supportSlots.Count > 2) supportSlots.RemoveRange(2, supportSlots.Count - 2);
        var supportCandidates = new List<(int Tile, int Delta)>[supportSlots.Count];
        for (int i = 0; i < supportSlots.Count; i++)
            supportCandidates[i] = LegalCampCandidates(supportSlots[i], pairBaseSites,
                baseline.FrontierPath, routeOnly: true, MaxSupportCandidatesPerSlot);

        if (supportSlots.Count == 1)
        {
            int slot = supportSlots[0];
            foreach ((int tile, _) in supportCandidates[0])
            {
                if (searchEvaluations >= MaxSearchEvaluations) break;
                int[] trialSites = (int[])pairBaseSites.Clone();
                trialSites[slot] = tile;
                EvaluateSupportPair(trialSites);
            }
        }
        else if (supportSlots.Count >= 2)
        {
            int first = supportSlots[0], second = supportSlots[1];
            for (int firstCandidate = 0; firstCandidate < supportCandidates[0].Count; firstCandidate++)
            {
                if (searchEvaluations >= MaxSearchEvaluations) break;
                int firstTile = supportCandidates[0][firstCandidate].Tile;
                foreach ((int secondTile, _) in supportCandidates[1])
                {
                    if (searchEvaluations >= MaxSearchEvaluations) break;
                    if (firstTile == secondTile && slots[first].Settlement == slots[second].Settlement) continue;
                    int[] trialSites = (int[])pairBaseSites.Clone();
                    trialSites[first] = firstTile;
                    trialSites[second] = secondTile;
                    EvaluateSupportPair(trialSites);
                }
            }
        }

        // Use exact local ownership deltas to screen legal moves, then spend whole-map projections only on the best
        // candidates. Recompute after each accepted move because camp claims overlap.
        while (searchEvaluations < MaxSearchEvaluations &&
            (bestProjection.ClaimedWeight > landCap || !PathIsAcceptable(bestProjection.FrontierConnectionWeight)))
        {
            List<(int Slot, int Tile, int Delta)> moves = RankedLocalMoves(bestSites, protectedSlots);
            if (moves.Count == 0) break;
            bool found = false;
            int[]? chosenSites = null;
            (int ClaimedWeight, int FrontierConnectionWeight) chosenProjection = bestProjection;
            int candidatesThisPass = Math.Min(MaxFullMovesPerPass, moves.Count);
            for (int i = 0; i < candidatesThisPass && searchEvaluations < MaxSearchEvaluations; i++)
            {
                (int slot, int tile, _) = moves[i];
                int[] trialSites = (int[])bestSites.Clone();
                trialSites[slot] = tile;
                var trial = ProjectSites(trialSites);
                if (!PathIsAcceptable(trial.FrontierConnectionWeight) ||
                    trial.ClaimedWeight >= bestProjection.ClaimedWeight)
                    continue;
                if (!found || trial.ClaimedWeight < chosenProjection.ClaimedWeight ||
                    trial.ClaimedWeight == chosenProjection.ClaimedWeight &&
                        trial.FrontierConnectionWeight < chosenProjection.FrontierConnectionWeight)
                {
                    found = true;
                    chosenSites = trialSites;
                    chosenProjection = trial;
                }
            }
            if (!found || chosenSites is null) break;
            bestSites = chosenSites;
            bestProjection = chosenProjection;
            optimizedMoves++;
        }

        // Greedy single moves can stop at a local minimum when two overlapping camp changes are jointly useful.
        // Rank those pairs by exact local ownership delta, then check final land and frontier with the whole-map projector.
        while (searchEvaluations < MaxSearchEvaluations &&
            (bestProjection.ClaimedWeight > landCap || !PathIsAcceptable(bestProjection.FrontierConnectionWeight)))
        {
            List<(int FirstSlot, int FirstTile, int SecondSlot, int SecondTile, int Delta)> movePairs =
                RankedLocalMovePairs(bestSites, protectedSlots);
            if (movePairs.Count == 0) break;
            bool found = false;
            int[]? chosenSites = null;
            (int ClaimedWeight, int FrontierConnectionWeight) chosenProjection = bestProjection;
            foreach ((int firstSlot, int firstTile, int secondSlot, int secondTile, _) in movePairs)
            {
                if (searchEvaluations >= MaxSearchEvaluations) break;
                int[] trialSites = (int[])bestSites.Clone();
                trialSites[firstSlot] = firstTile;
                trialSites[secondSlot] = secondTile;
                var trial = ProjectSites(trialSites);
                coupledPairEvaluations++;
                if (!PathIsAcceptable(trial.FrontierConnectionWeight) ||
                    trial.ClaimedWeight >= bestProjection.ClaimedWeight)
                    continue;
                if (!found || trial.ClaimedWeight < chosenProjection.ClaimedWeight ||
                    trial.ClaimedWeight == chosenProjection.ClaimedWeight &&
                        trial.FrontierConnectionWeight < chosenProjection.FrontierConnectionWeight)
                {
                    found = true;
                    chosenSites = trialSites;
                    chosenProjection = trial;
                }
                // Pair candidates are ordered by their exact local claimed-weight delta, which equals the
                // whole-map claimed-weight delta for the affected disks. The first passing target is sufficient.
                if (trial.ClaimedWeight <= landCap) break;
            }
            if (!found || chosenSites is null) break;
            bestSites = chosenSites;
            bestProjection = chosenProjection;
            optimizedPairs++;
            if (bestProjection.ClaimedWeight <= landCap &&
                bestProjection.FrontierConnectionWeight >= 0 && bestProjection.FrontierConnectionWeight <= 600)
                break;
        }

        int changedSites = 0;
        for (int s = 0; s < _sCount; s++)
        {
            List<int>? previous = originalSites[s];
            if (previous is null) continue;
            List<int> replacement = compactSites[s] ?? new List<int>();
            previous.Sort();
            replacement.Sort();
            int common = Math.Min(previous.Count, replacement.Count);
            for (int i = 0; i < common; i++) if (previous[i] != replacement[i]) changedSites++;
            changedSites += Math.Abs(previous.Count - replacement.Count);
        }
        int searchChangedSites = 0;
        for (int i = 0; i < slots.Count; i++)
            if (bestSites[i] != slots[i].OriginalTile) searchChangedSites++;
        return (baseline.ClaimedWeight, compact.ClaimedWeight, baseline.FrontierConnectionWeight,
            compact.FrontierConnectionWeight, baselineSeasonalCamps.Count, compactSeasonalCamps.Count, changedSites,
            snapshotLegalOriginalSites, protectedProjection.ClaimedWeight, protectedProjection.FrontierConnectionWeight,
            protectedCampSites, bestProjection.ClaimedWeight, bestProjection.FrontierConnectionWeight,
            searchChangedSites, supportSlots.Count, supportPairEvaluations, optimizedMoves,
            coupledPairEvaluations, optimizedPairs, searchEvaluations,
            bestProjection.ClaimedWeight <= landCap && bestProjection.FrontierConnectionWeight >= 0 &&
                bestProjection.FrontierConnectionWeight <= 600, routeSlots.Count, routeSingleEvaluations,
            routePairEvaluations, routeBestClaimedWeight, routeBestFrontierConnectionWeight,
            RouteMeetsTargets(routeBestClaimedWeight, routeBestFrontierConnectionWeight));

        bool RouteMeetsTargets(int claimedWeight, int frontierWeight)
            => claimedWeight <= landCap && frontierWeight == 0;

        void UpdateRouteBest(int claimedWeight, int frontierWeight)
        {
            if (frontierWeight < routeBestFrontierConnectionWeight ||
                frontierWeight == routeBestFrontierConnectionWeight && claimedWeight < routeBestClaimedWeight)
            {
                routeBestClaimedWeight = claimedWeight;
                routeBestFrontierConnectionWeight = frontierWeight;
            }
        }

        (int ClaimedWeight, int FrontierConnectionWeight) ProjectRouteSites(IReadOnlyList<int> campSites)
        {
            routeSearchEvaluations++;
            var camps = new List<ClaimSource>(campSites.Count + miningCamps.Count);
            for (int i = 0; i < slots.Count; i++)
                camps.Add(new ClaimSource(slots[i].Civ, campSites[i], Territory.CampRadius, slots[i].Emission));
            camps.AddRange(miningCamps);
            var projection = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, camps, civA, civB);
            return (projection.ClaimedWeight, projection.FrontierConnectionWeight);
        }

        static (int Slot, List<(int Tile, int Delta)> Candidates, int BestClaimed, int BestPath) BetterRouteSlot(
            (int Slot, List<(int Tile, int Delta)> Candidates, int BestClaimed, int BestPath) current,
            int claimed, int path)
            => path < current.BestPath || path == current.BestPath && claimed < current.BestClaimed
                ? (current.Slot, current.Candidates, claimed, path)
                : current;

        (int ClaimedWeight, int FrontierConnectionWeight) ProjectSites(IReadOnlyList<int> campSites)
        {
            if (searchEvaluations >= MaxSearchEvaluations)
                return (int.MaxValue, int.MaxValue);
            searchEvaluations++;
            var camps = new List<ClaimSource>(campSites.Count + miningCamps.Count);
            for (int i = 0; i < slots.Count; i++)
                camps.Add(new ClaimSource(slots[i].Civ, campSites[i], Territory.CampRadius, slots[i].Emission));
            camps.AddRange(miningCamps);
            var projection = _territory.ProjectedClaimedWeightAndFrontierCost(settlements, camps, civA, civB);
            return (projection.ClaimedWeight, projection.FrontierConnectionWeight);
        }

        void EvaluateSupportPair(int[] trialSites)
        {
            var trial = ProjectSites(trialSites);
            supportPairEvaluations++;
            if (!PathIsAcceptable(trial.FrontierConnectionWeight)) return;
            if (trial.ClaimedWeight < bestProjection.ClaimedWeight ||
                trial.ClaimedWeight == bestProjection.ClaimedWeight &&
                    trial.FrontierConnectionWeight < bestProjection.FrontierConnectionWeight)
            {
                bestSites = trialSites;
                bestProjection = trial;
            }
        }

        List<(int Tile, int Delta)> LegalCampCandidates(int slotIndex, IReadOnlyList<int> currentSites,
            IReadOnlyList<int> route, bool routeOnly, int limit)
        {
            var candidates = new List<(int Tile, int Delta)>();
            var rivals = RivalSettlements(slots[slotIndex].Civ);
            int settlement = slots[slotIndex].Settlement;
            int radius = _eraRadius[Math.Min(_research.Era(slots[slotIndex].Civ), _eraRadius.Length - 1)];
            var camps = CampSources(currentSites);
            for (int tile = 0; tile < _territory.TileCount; tile++)
            {
                if (!CanUseCampSite(slotIndex, tile, currentSites, settlement, radius, rivals) ||
                    routeOnly && !CampTouchesPath(tile, route)) continue;
                int delta = _territory.ProjectedCampMoveClaimedWeightDelta(settlements, camps, slotIndex, tile);
                candidates.Add((tile, delta));
            }
            candidates.Sort(static (a, b) => a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) : a.Tile.CompareTo(b.Tile));
            int currentTile = currentSites[slotIndex];
            int currentIndex = candidates.FindIndex(candidate => candidate.Tile == currentTile);
            (int Tile, int Delta)? currentCandidate = currentIndex >= 0 ? candidates[currentIndex] : null;
            if (candidates.Count > limit) candidates.RemoveRange(limit, candidates.Count - limit);
            if (currentCandidate is { } retained && !candidates.Exists(candidate => candidate.Tile == currentTile))
            {
                if (candidates.Count == limit && candidates.Count > 0) candidates.RemoveAt(candidates.Count - 1);
                candidates.Add(retained);
                candidates.Sort(static (a, b) => a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) : a.Tile.CompareTo(b.Tile));
            }
            return candidates;
        }

        List<(int Slot, int Tile, int Delta)> RankedLocalMoves(IReadOnlyList<int> currentSites,
            IReadOnlyList<bool> protectedCampSlots)
        {
            var moves = new List<(int Slot, int Tile, int Delta)>();
            for (int slot = 0; slot < slots.Count; slot++)
            {
                if (protectedCampSlots[slot]) continue;
                var candidates = LegalCampCandidates(slot, currentSites, Array.Empty<int>(), routeOnly: false, limit: 4);
                foreach ((int tile, int delta) in candidates)
                {
                    if (tile == currentSites[slot] || delta >= 0) continue;
                    moves.Add((slot, tile, delta));
                }
            }
            moves.Sort(static (a, b) => a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) :
                a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : a.Tile.CompareTo(b.Tile));
            return moves;
        }

        List<(int FirstSlot, int FirstTile, int SecondSlot, int SecondTile, int Delta)> RankedLocalMovePairs(
            IReadOnlyList<int> currentSites, IReadOnlyList<bool> protectedCampSlots)
        {
            var options = new List<(int Tile, int Delta)>[slots.Count];
            for (int slot = 0; slot < slots.Count; slot++)
            {
                List<(int Tile, int Delta)> candidates = LegalCampCandidates(slot, currentSites,
                    baseline.FrontierPath, routeOnly: protectedCampSlots[slot], limit: 4);
                options[slot] = new List<(int Tile, int Delta)>(3);
                foreach ((int tile, int delta) in candidates)
                {
                    if (tile == currentSites[slot]) continue;
                    options[slot].Add((tile, delta));
                    if (options[slot].Count == 3) break;
                }
            }

            var camps = CampSources(currentSites);
            var pairs = new List<(int FirstSlot, int FirstTile, int SecondSlot, int SecondTile, int Delta)>();
            for (int firstSlot = 0; firstSlot < slots.Count; firstSlot++)
            {
                for (int secondSlot = firstSlot + 1; secondSlot < slots.Count; secondSlot++)
                {
                    if (options[firstSlot].Count == 0 || options[secondSlot].Count == 0) continue;
                    bool sameParent = slots[firstSlot].Settlement == slots[secondSlot].Settlement;
                    foreach ((int firstTile, _) in options[firstSlot])
                    foreach ((int secondTile, _) in options[secondSlot])
                    {
                        if (firstTile == secondTile && sameParent) continue;
                        var trialSites = new int[currentSites.Count];
                        for (int i = 0; i < currentSites.Count; i++) trialSites[i] = currentSites[i];
                        trialSites[firstSlot] = firstTile;
                        trialSites[secondSlot] = secondTile;
                        if (HasDuplicateParentCenters(trialSites)) continue;
                        bool disksOverlap = CampMoveFootprintsOverlap(firstSlot, firstTile, secondSlot, secondTile, currentSites);
                        bool bothTouchRoute = CampTouchesPath(firstTile, baseline.FrontierPath) &&
                            CampTouchesPath(secondTile, baseline.FrontierPath);
                        if (!sameParent && !disksOverlap && !bothTouchRoute) continue;

                        int delta = _territory.ProjectedCampMovesClaimedWeightDelta(settlements, camps,
                            firstSlot, firstTile, secondSlot, secondTile);
                        if (delta < 0)
                            pairs.Add((firstSlot, firstTile, secondSlot, secondTile, delta));
                    }
                }
            }
            pairs.Sort(static (a, b) => a.Delta != b.Delta ? a.Delta.CompareTo(b.Delta) :
                a.FirstSlot != b.FirstSlot ? a.FirstSlot.CompareTo(b.FirstSlot) :
                a.SecondSlot != b.SecondSlot ? a.SecondSlot.CompareTo(b.SecondSlot) :
                a.FirstTile != b.FirstTile ? a.FirstTile.CompareTo(b.FirstTile) : a.SecondTile.CompareTo(b.SecondTile));
            return pairs;
        }

        bool HasDuplicateParentCenters(IReadOnlyList<int> sites)
        {
            for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
                if (slots[i].Settlement == slots[j].Settlement && sites[i] == sites[j]) return true;
            return false;
        }

        bool CampMoveFootprintsOverlap(int firstSlot, int firstTile, int secondSlot, int secondTile,
            IReadOnlyList<int> currentSites)
        {
            int firstOld = currentSites[firstSlot], secondOld = currentSites[secondSlot];
            int maxCenterDistance = Territory.CampRadius * 2;
            return HexGrid.Distance(firstOld, secondOld, _territory.Width) <= maxCenterDistance ||
                HexGrid.Distance(firstOld, secondTile, _territory.Width) <= maxCenterDistance ||
                HexGrid.Distance(firstTile, secondOld, _territory.Width) <= maxCenterDistance ||
                HexGrid.Distance(firstTile, secondTile, _territory.Width) <= maxCenterDistance;
        }

        bool CanUseCampSite(int slotIndex, int tile, IReadOnlyList<int> currentSites, int settlement,
            int radius, IReadOnlyList<ClaimSource> rivals)
        {
            if (!_territory.IsCampSiteLegal(slots[slotIndex].Civ, _sTile[settlement], tile, radius, rivals)) return false;
            for (int other = 0; other < slots.Count; other++)
                if (other != slotIndex && slots[other].Settlement == settlement && currentSites[other] == tile) return false;
            return true;
        }

        bool CampTouchesPath(int center, IReadOnlyList<int> path)
        {
            foreach (int tile in path)
                if (HexGrid.Distance(center, tile, _territory.Width) <= Territory.CampRadius) return true;
            return false;
        }

        List<ClaimSource> CampSources(IReadOnlyList<int> sites)
        {
            var camps = new List<ClaimSource>(sites.Count + miningCamps.Count);
            for (int i = 0; i < slots.Count; i++)
                camps.Add(new ClaimSource(slots[i].Civ, sites[i], Territory.CampRadius, slots[i].Emission));
            camps.AddRange(miningCamps);
            return camps;
        }

        List<ClaimSource> RivalSettlements(int civ)
        {
            var rivals = new List<ClaimSource>();
            for (int settlement = 0; settlement < _sCount; settlement++)
            {
                if (_free[settlement] || _sCiv[settlement] == civ) continue;
                rivals.Add(new ClaimSource(_sCiv[settlement], _sTile[settlement],
                    _eraRadius[Math.Min(_research.Era(_sCiv[settlement]), _eraRadius.Length - 1)], Fix64.Zero));
            }
            return rivals;
        }
    }

    private (int SupportedTiles, int UnsupportedTiles, int CorridorWeight) AddSeasonalCampTrailClaims(
        IReadOnlyList<ClaimSource> settlements, IReadOnlyList<ClaimSource> seasonalCamps, int supportRadius,
        int civA, int civB, List<ClaimSource> trailClaims)
    {
        var settlementProjection = _territory.ProjectedClaimedWeightAndFrontierCost(
            settlements, Array.Empty<ClaimSource>(), civA, civB);
        int[] corridor = settlementProjection.FrontierUnownedPath;
        int supported = 0, unsupported = 0, corridorWeight = 0;
        int firstHalf = (corridor.Length + 1) / 2;
        for (int i = 0; i < corridor.Length; i++)
        {
            int tile = corridor[i];
            corridorWeight += _territory.TileAt(tile).HabitableWeight;
            int civ = i < firstHalf ? civA : civB;
            int liveOwner = _territory.OwnerOf(tile);
            if (liveOwner >= 0 && liveOwner != civ) { unsupported++; continue; }
            ClaimSource nearest = default;
            int nearestDistance = int.MaxValue;
            foreach (ClaimSource camp in seasonalCamps)
            {
                if (camp.Civ != civ) continue;
                int campOwner = _territory.OwnerOf(camp.Tile);
                if (campOwner >= 0 && campOwner != civ) continue;
                int distance = HexGrid.Distance(tile, camp.Tile, _width);
                if (distance > supportRadius || distance > nearestDistance) continue;
                if (distance == nearestDistance && nearest.Tile <= camp.Tile) continue;
                nearest = camp;
                nearestDistance = distance;
            }
            if (nearestDistance == int.MaxValue) { unsupported++; continue; }
            supported++;
            trailClaims.Add(new ClaimSource(civ, tile, 0, nearest.Emission));
        }
        return (supported, unsupported, corridorWeight);
    }

    private void UpdateTerritory()
    {
        var settlements = new List<ClaimSource>();
        var camps = new List<ClaimSource>();
        BuildTerritoryClaimSources(settlements, camps);
        _territory.Update(settlements, camps);
    }

    private void BuildTerritoryClaimSources(List<ClaimSource> settlements, List<ClaimSource> camps)
    {
        BuildSettlementClaimSources(settlements);
        BuildSeasonalCampClaimSources(camps);
        BuildMiningCampClaimSources(camps);
    }

    private void BuildTerritoryClaimSources(List<ClaimSource> settlements, List<ClaimSource> seasonalCamps,
        List<ClaimSource> miningCamps)
    {
        BuildSettlementClaimSources(settlements);
        BuildSeasonalCampClaimSources(seasonalCamps);
        BuildMiningCampClaimSources(miningCamps);
    }

    private void BuildSettlementClaimSources(List<ClaimSource> settlements)
    {
        for (int s = 0; s < _sCount; s++)
        {
            if (_free[s]) continue;
            int civ = _sCiv[s];
            // Towns claim radius 5, Cities 6, a Palace capital ×1.25, a City's hinterland ring 7 at half (docs/MVP/22 §8.1, §11).
            int eraRadius = _eraRadius[Math.Min(_research.Era(civ), _eraRadius.Length - 1)];
            Fix64 emission = EmissionOf(s);
            Fix64 mult = _economy.ClaimMultiplier(s);
            // A village founded by Decree claims ×1.5 for two years (TER-05, docs/MVP/22 §4.3).
            if (_foundBoost.Count > 0) mult *= FoundBoost(s);
            settlements.Add(new ClaimSource(civ, _sTile[s], _economy.ClaimRadius(s, eraRadius), mult == Fix64.One ? emission : emission * mult));
            (int ring, Fix64 share) = _economy.Hinterland(s);
            if (ring > 0) settlements.Add(new ClaimSource(civ, _sTile[s], ring, emission * share));
        }
    }

    private void BuildSeasonalCampClaimSources(List<ClaimSource> camps)
    {
        foreach ((int tile, int settlement, int civ) in _camps)
        {
            if (settlement >= _sCount || _free[settlement] || _sCiv[settlement] != civ) continue;
            // A band's seasonal camp claims at half its emission, three quarters with Camp Circuits (07b §9.4).
            Fix64 share = _research.IsDone(civ, _p.Ter02) ? Fix64.FromRatio(3, 4) : Fix64.Half;
            camps.Add(new ClaimSource(civ, tile, Territory.CampRadius, EmissionOf(settlement) * share));
        }
    }

    private void BuildMiningCampClaimSources(List<ClaimSource> camps)
    {
        // Mining camps claim radius 1 (2 with TER-06) at 75% of a six-person band's emission (docs/MVP/22 §5.2).
        for (int i = 0; i < _economy.MiningCampCount; i++)
        {
            MiningCampView camp = _economy.MiningCamp(i);
            if (camp.ParentSettlementIndex >= _sCount || _free[camp.ParentSettlementIndex]) continue;
            camps.Add(new ClaimSource(camp.Civilization, camp.Tile, camp.ClaimRadius, camp.ClaimEmission));
        }
    }

    // ---- contact --------------------------------------------------------------------------

    private List<int> PresencePoints(int civ)
    {
        var points = new List<int>();
        for (int s = 0; s < _sCount; s++) if (_sCiv[s] == civ && !_free[s]) points.Add(_sTile[s]);
        // Figures range out from their settlements (13 §2.2); seasonal camps claim land but are not where scouts start.
        return points;
    }

    /// <summary>
    /// Walking distance between the nearest settlements of two civilizations in terrain-weighted tiles (forest 1.5, wetland
    /// 2, mountain 4: the same cost the start pairs are placed by, docs/MVP/12 §9.3), searched no further than
    /// <paramref name="limit"/> tiles. int.MaxValue when out of range.
    /// </summary>
    private int WalkingTilesBetween(int a, int b, int limit)
    {
        WorldTile[] tiles = _tileArray;
        int best = int.MaxValue;
        var targets = PresencePoints(b);
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != a || _free[s]) continue;
            int[] cost = HexGrid.WalkingCostsFrom(tiles, _width, _height, _sTile[s], limit * 10);
            foreach (int tile in targets)
                if (cost[tile] != int.MaxValue) best = Math.Min(best, (cost[tile] + 9) / 10);
        }
        return best;
    }

    private int[] MultiSourceDistances(List<int> sources)
    {
        int[] distance = new int[_passable.Length];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[_passable.Length];
        int head = 0, tail = 0;
        foreach (int source in sources)
            if (_passable[source] && distance[source] != 0) { distance[source] = 0; queue[tail++] = source; }
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int tile = queue[head++];
            int count = HexGrid.Neighbors(tile, _width, _height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (!_passable[next] || distance[next] != int.MaxValue) continue;
                distance[next] = distance[tile] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    private void DetectContacts()
    {
        for (int a = 0; a < _civs; a++)
            for (int b = a + 1; b < _civs; b++)
            {
                if (_dip.Contacted(a, b)) continue;
                if (_gentleStart && (a == 0 || b == 0) && _research.Era(0) < 1) continue;
                // Reach is counted in the terrain-weighted tiles the start pairs are placed by, with a tenth added for trails.
                int sight = (ReachOf(a) + ReachOf(b)) * 11 / 10;
                int d = WalkingTilesBetween(a, b, sight + 3);
                if (d == int.MaxValue && (CanSail(a) || CanSail(b))) d = SailTilesBetween(a, b, sight + 3);
                Trace?.Invoke($"season {_season}: civ{a}-civ{b} walking {d} sight {sight} eraA {_research.Era(a)}");
                if (d == int.MaxValue) continue;
                if (!_dip.Aware(a, b) && d <= sight + 3)
                {
                    _dip.MarkAware(a, b);
                    _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.Aware, b, d, 0));
                    _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.Aware, a, d, 0));
                }
                if (d <= sight + 2) MakeContact(a, b);
            }
    }

    private ContactModifiers ModifiersFor(int civ, int other)
    {
        Posture posture = (Posture)_posture[civ];
        bool hungry = false;
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _economy.SettlementLite(s).LastFoodSecurity < Fix64.One) { hungry = true; break; }
        return new ContactModifiers(
            _research.IsDone(civ, _p.Dip01), _research.IsDone(civ, _p.Trd01), _research.IsDone(civ, _p.Com02),
            posture == Posture.Prosper, posture == Posture.Raid, hungry,
            PopulationOf(other) * 2 < PopulationOf(civ), _dip.Reputation(other));
    }

    private int PopulationOf(int civ)
    {
        int total = 0;
        for (int s = 0; s < _sCount; s++) if (_sCiv[s] == civ && !_free[s]) total += _sPop[s];
        return total;
    }

    private void MakeContact(int a, int b)
    {
        if (_dip.Contacted(a, b)) return;
        _dip.MarkContacted(a, b);
        Pcg32 rollA = Pcg32.ForStream(_seed, ContactStream, (ulong)(a * 16 + b), _season);
        Pcg32 rollB = Pcg32.ForStream(_seed, ContactStream, (ulong)(b * 16 + a), _season);
        Stance stanceA = FirstContact.Roll(ModifiersFor(a, b), ref rollA);
        Stance stanceB = FirstContact.Roll(ModifiersFor(b, a), ref rollB);
        (bool violence, int delta) = FirstContact.Combine(stanceA, stanceB);
        _dip.Set(a, b, delta);
        _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.Contact, b, (int)stanceA, (int)stanceB));
        _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.Contact, a, (int)stanceB, (int)stanceA));
        _research.RecordContact(a, b);
        if (violence) Skirmish(a, b);
    }

    /// <summary>First blood between the meeting parties (13 §3.1): two small parties fight until one breaks.</summary>
    private void Skirmish(int a, int b)
    {
        int sa = NearestSettlement(a, b), sb = NearestSettlement(b, a);
        if (sa < 0 || sb < 0) return;
        List<Fighter> partyA = PickParty(sa, a, 4, attacking: true, out _);
        List<Fighter> partyB = PickParty(sb, b, 4, attacking: true, out _);
        if (partyA.Count == 0 || partyB.Count == 0) return;
        var setup = new BattleSetup(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10), 6, false);
        BattleResult r = Combat.Fight(partyA, partyB, setup, _seed, SkirmishStream ^ (ulong)(a * 16 + b) ^ (ulong)_season);
        ApplyCasualties(r.AttackerDead, r.AttackerWounded);
        ApplyCasualties(r.DefenderDead, r.DefenderWounded);
        Skirmishes++;
        int hurtA = r.AttackerDead.Count + r.AttackerWounded.Count, hurtB = r.DefenderDead.Count + r.DefenderWounded.Count;
        _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.Skirmish, b, hurtA, hurtB));
        _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.Skirmish, a, hurtB, hurtA));
    }

    private void ApplyCasualties(List<long> dead, List<long> wounded)
    {
        foreach (long id in dead) _economy.KillFigure(id);
        foreach (long id in wounded) _economy.WoundFigure(id, 2);
    }

    // ---- shared helpers -------------------------------------------------------------------

    private int NearestSettlement(int civ, int towardCiv)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s]) continue;
            for (int t = 0; t < _sCount; t++)
            {
                if (_sCiv[t] != towardCiv || _free[t]) continue;
                int d = HexGrid.Distance(_sTile[s], _sTile[t], _width);
                if (d < bestDistance) { bestDistance = d; best = s; }
            }
        }
        return best;
    }

    /// <summary>Weapon strength multiplier of the best weapon a civilization has, and whether it is ranged (13 §7.1).</summary>
    private Fix64 Weapon(int civ, out bool ranged)
    {
        ranged = false;
        // Metal arms are locally stocked kits, applied by Fighters below. This is the fallback
        // for parties that lack cast arms and for ranged weapon technology.
        if (_research.IsDone(civ, _p.Hnt06)) { ranged = true; return Fix64.FromRatio(18, 10) * (_research.IsDone(civ, _p.War03) ? Fix64.FromRatio(15, 10) : Fix64.One); }
        if (_research.IsDone(civ, _p.Hnt05)) { ranged = true; return Fix64.FromRatio(17, 10); }
        if (_research.IsDone(civ, _p.Hnt03)) return Fix64.FromRatio(16, 10);
        if (_research.IsDone(civ, _p.Hnt02)) return Fix64.FromRatio(13, 10);
        if (_research.IsDone(civ, _p.War01)) return Fix64.One;
        return Fix64.FromRatio(6, 10);
    }

    /// <summary>Fighters a settlement can put up: adults and elders who are not laid up, with warriors first (13 §6.1, §7.1).</summary>
    private List<Fighter> Fighters(int settlement, bool attacking, out List<long> ids, int battleTile = -1)
    {
        int civ = _sCiv[settlement];
        Fix64 weapon = Weapon(civ, out _);
        bool warriors = _research.IsDone(civ, _p.War04);
        var records = new List<LivingEconomyModel.FighterRecord>();
        _economy.ListFightingAge(settlement, records);
        int adults = 0;
        foreach (var r in records) if (r.Age < 45) adults++;
        int warriorSlots = warriors ? adults / 5 : 0;
        int tinArms = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS2T").FloorToInt() : 0;
        int arsenicalArms = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS2A").FloorToInt() : 0;
        int copperArms = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS1").FloorToInt() : 0;
        int chariotCrew = _economy.BronzeActive && _research.IsDone(civ, _p.War06)
            ? _economy.GoodStock(settlement, "G-CHARIOT").FloorToInt() * 2 : 0;
        bool openTerrain = IsChariotOpenTerrain(battleTile);
        Defenses targetDefenses = DefensesAtTile(battleTile);
        // Tin Folk defend at 0.8 strength (docs/MVP/22 §6.4).
        bool tinFolk = _tinFolk.Count > 0 && IsTinFolk(settlement);
        var list = new List<Fighter>();
        ids = new List<long>();
        foreach (var r in records)
        {
            if (r.Wounded) continue;
            bool warrior = r.Age < 45 && !r.Female && warriorSlots > 0;
            if (warrior) warriorSlots--;
            Fix64 weaponStrength = weapon;
            Fix64 casualtiesTaken = Fix64.One;
            if (tinArms > 0) { tinArms--; weaponStrength = Combat.E3WeaponStrength("tin"); casualtiesTaken *= Combat.ShieldCasualtyFactor(Fix64.One); }
            else if (arsenicalArms > 0) { arsenicalArms--; weaponStrength = Combat.E3WeaponStrength("arsenical"); casualtiesTaken *= Combat.ShieldCasualtyFactor(Fix64.One); }
            else if (copperArms > 0) { copperArms--; weaponStrength = Combat.E3WeaponStrength("copper"); }
            Fix64 strength;
            if (r.Age >= 45) strength = Fix64.FromRatio(3, 10) * weaponStrength;
            else if (warrior)
            {
                strength = weaponStrength * Fix64.FromRatio(13, 10);
            }
            else strength = (attacking ? Fix64.One : Fix64.FromRatio(6, 10)) * weaponStrength;
            if (chariotCrew > 0)
            {
                chariotCrew--;
                casualtiesTaken *= Combat.ChariotCasualtyFactor;
                if (openTerrain && !targetDefenses.Hillfort && !targetDefenses.StoneWalls) strength *= Fix64.FromInt(2);
            }
            if (tinFolk) strength *= _trade.FolkDefense;
            list.Add(new Fighter(r.Id, strength) { CasualtiesTaken = casualtiesTaken });
            ids.Add(r.Id);
        }
        return list;
    }

    private bool IsChariotOpenTerrain(int tile)
    {
        if (unchecked((uint)tile) >= unchecked((uint)_tileArray.Length)) return false;
        return _tileArray[tile].Biome is Biome.GrasslandSteppe or Biome.Savanna or Biome.Desert;
    }

    private Defenses DefensesAtTile(int tile)
    {
        for (int s = 0; s < _sCount; s++) if (_sTile[s] == tile) return DefensesOf(s);
        return Defenses.None;
    }

    /// <summary>The adults who go: warriors and men first, never so many that fewer than four stay home (13 §5.2).</summary>
    private List<Fighter> PickParty(int settlement, int civ, int size, bool attacking, out List<long> ids, int stayHome = 4, int battleTile = -1)
    {
        List<Fighter> all = Fighters(settlement, attacking, out _, battleTile);
        var records = new List<LivingEconomyModel.FighterRecord>();
        _economy.ListFightingAge(settlement, records);
        var party = new List<Fighter>();
        ids = new List<long>();
        int available = 0;
        foreach (Fighter f in all) available++;
        int take = Math.Min(size, Math.Max(0, available - stayHome));
        // Strongest first, ties by id: the party is warriors, then the rest of the best-armed adults.
        var ordered = new List<Fighter>(all);
        ordered.Sort((x, y) => x.Strength != y.Strength ? y.Strength.CompareTo(x.Strength) : x.Id.CompareTo(y.Id));
        for (int i = 0; i < take; i++) { party.Add(ordered[i]); ids.Add(ordered[i].Id); }
        return party;
    }

    private Defenses DefensesOf(int settlement)
    {
        int civ = _sCiv[settlement];
        int palisadeKind = LivingEconomyModel.StructureIndex("palisade");
        bool palisade = palisadeKind >= 0 && _economy.StructureCount(settlement, palisadeKind) > 0 ||
                        _tinFolk.Count > 0 && IsTinFolk(settlement);
        bool watchers = _research.IsDone(civ, _p.Def02) || _economy.DefenseStructures(settlement, lookout: true) > 0;
        bool thorn = _economy.DefenseStructures(settlement, lookout: false) > 0;
        int hillfortKind = LivingEconomyModel.StructureIndex("hillfort");
        int wallsKind = LivingEconomyModel.StructureIndex("stone_walls");
        bool hillfort = hillfortKind >= 0 && _research.IsDone(civ, _p.Def04) && _economy.StructureCount(settlement, hillfortKind) > 0;
        bool walls = wallsKind >= 0 && _research.IsDone(civ, _p.Def05) && _economy.StructureCount(settlement, wallsKind) > 0;
        bool hillfortRefuge = false;
        if (hillfortKind >= 0 && _research.IsDone(civ, _p.Def04))
        {
            for (int other = 0; other < _sCount; other++)
            {
                if (_sCiv[other] != civ || _economy.StructureCount(other, hillfortKind) == 0) continue;
                if (HexGrid.Distance(_sTile[settlement], _sTile[other], _width) <= 6)
                {
                    hillfortRefuge = other != settlement;
                    if (hillfortRefuge) break;
                }
            }
        }
        return WithPostureDefenses(civ, new Defenses(thorn, palisade, watchers, _research.IsDone(civ, _p.Hus01), _research.IsDone(civ, _p.Com04),
            _research.IsDone(civ, _p.Com01), HillSite: false, _research.IsDone(civ, _p.War03), Night: false,
            RaidedRecently: _season - _dip.LastRaidOnAny(civ) <= 4, Hillfort: hillfort, StoneWalls: walls,
            HillfortRefuge: hillfortRefuge, BronzeRules: _economy.BronzeActive));
    }

    private int ContestedOverlapTiles(int a, int b)
    {
        int count = 0;
        for (int t = 0; t < _territory.TileCount; t++)
            if (_territory.ClaimOf(a, t) >= Territory.OwnThresholdMilli && _territory.ClaimOf(b, t) >= Territory.OwnThresholdMilli) count++;
        return count;
    }

    /// <summary>Claim overlap sours neighbours: -2 Relations a season per five contested tiles (13 §3.2).</summary>
    private void DriftOverlap()
    {
        for (int a = 0; a < _civs; a++)
            for (int b = a + 1; b < _civs; b++)
            {
                if (!_dip.Contacted(a, b) || _dip.Allied(a, b)) continue;
                // A border treaty fixes the line: claims across it do not sour Relations (DIP-05).
                if (_pacts.Count > 0 && HasTreaty(a, b)) continue;
                int overlap = ContestedOverlapTiles(a, b);
                if (overlap >= 5) _dip.Shift(a, b, -2 * (overlap / 5));
            }
    }

    /// <summary>
    /// Settlements the economy reports as seceded (docs/MVP/22 §8.3) become Free Folk. WP8 owns what Free Folk do (claims,
    /// barter, Relations +20 with the old civilization, re-absorption); here they only leave their civilization.
    /// </summary>
    private readonly List<int> _secededScratch = new();

    private void TakeSecessions()
    {
        _secededScratch.Clear();
        _economy.DrainSecessions(_secededScratch);
        foreach (int s in _secededScratch)
        {
            if ((uint)s >= (uint)_sCount || _free[s]) continue;
            _free[s] = true;
            _cohesionDebt[s] = Fix64.Zero;
            _events.Add(new PoliticsEvent(_tick, (byte)_sCiv[s], PoliticsEventKind.Seceded, -1, s, _sPop[s]));
        }
    }

    /// <summary>Settlements that seceded in the match (docs/MVP/22 §15 T25), read from the economy's state.</summary>
    public int Secessions
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _economy.SettlementCount; i++) if (_economy.Civic(i).Seceded) n++;
            return n;
        }
    }

    private void RecoverCohesion()
    {
        for (int s = 0; s < _sCount; s++)
        {
            if (_cohesionDebt[s] <= Fix64.Zero) continue;
            Fix64 step = Fix64.Min(_cohesionDebt[s], Fix64.FromRatio(2, 100));
            _economy.AdjustCohesion(s, step);
            _cohesionDebt[s] -= step;
        }
    }

    // ---- hashing and snapshots ------------------------------------------------------------

    public void Hash(ref StateHasher h)
    {
        if (_gentleStart) h.Add(0x47454E544C455354UL);
        h.Add(_season);
        _territory.Hash(ref h);
        _dip.Hash(ref h);
        foreach (byte p in _posture) h.Add(p);
        for (int c = 0; c < _civs; c++) { h.Add(_beacons[c].Count); foreach (int b in _beacons[c]) h.Add(b); }
        h.Add(_camps.Count);
        foreach ((int tile, int settlement, int civ) in _camps) { h.Add(tile); h.Add(settlement); h.Add(civ); }
        // The per-settlement lists lag the economy until the next RefreshSettlements; a restore refreshes them at once, so
        // hash and snapshot both cover every settlement the economy has, with the defaults a refresh would add.
        int tracked = Math.Max(_free.Count, _economy.SettlementCount);
        for (int i = 0; i < tracked; i++) { h.Add(i < _free.Count && _free[i]); h.Add(i < _campsPlaced.Count && _campsPlaced[i]); h.Add(i < _cohesionDebt.Count ? _cohesionDebt[i] : Fix64.Zero); }
        h.Add(_expeditions.Count);
        foreach (Expedition e in _expeditions)
        {
            h.Add((int)e.Kind); h.Add(e.Civ); h.Add(e.TargetCiv); h.Add(e.FromSettlement); h.Add(e.ToSettlement);
            h.Add(e.ArriveSeason); h.Add(e.Leg); h.Add(e.Value);
            foreach (long m in e.Members) h.Add(m);
            foreach (Fix64 s in e.Strengths) h.Add(s);
            if (e.HasBronzeFields) HashExpeditionBronze(ref h, e);
        }
        h.Add(_occupations.Count);
        foreach (Occupation o in _occupations)
        {
            h.Add(o.Civ); h.Add(o.Settlement); h.Add(o.SinceSeason); h.Add(o.RequiredSeasons);
            foreach (long m in o.Members) h.Add(m);
            foreach (Fix64 s in o.Strengths) h.Add(s);
        }
        foreach (int v in _failedCampaigns) h.Add(v);
        foreach (int v in _warSince) h.Add(v);
        h.Add(RaidCount); h.Add(RaidsSucceeded); h.Add(Gifts); h.Add(BarterTrips); h.Add(CaptureCount); h.Add(Skirmishes); h.Add(Wars);
        HashBronze(ref h);
        HashScoutSimulation(ref h);
    }

    public void WriteSnapshot(BinaryWriter w)
    {
        w.Write(_season);
        w.Write(_tick);
        _territory.Write(w);
        _dip.Write(w);
        foreach (byte p in _posture) w.Write(p);
        for (int c = 0; c < _civs; c++) { w.Write(_beacons[c].Count); foreach (int b in _beacons[c]) w.Write(b); }
        w.Write(_camps.Count);
        foreach ((int tile, int settlement, int civ) in _camps) { w.Write(tile); w.Write(settlement); w.Write(civ); }
        int tracked = Math.Max(_free.Count, _economy.SettlementCount);
        w.Write(tracked);
        for (int i = 0; i < tracked; i++)
        {
            w.Write(i < _free.Count && _free[i]);
            w.Write(i < _campsPlaced.Count && _campsPlaced[i]);
            w.Write((i < _cohesionDebt.Count ? _cohesionDebt[i] : Fix64.Zero).Raw);
        }
        w.Write(_expeditions.Count);
        foreach (Expedition e in _expeditions)
        {
            w.Write((byte)e.Kind); w.Write(e.Civ); w.Write(e.TargetCiv); w.Write(e.FromSettlement); w.Write(e.ToSettlement);
            w.Write(e.ArriveSeason); w.Write(e.Leg); w.Write(e.Value.Raw);
            w.Write(e.Members.Count);
            for (int i = 0; i < e.Members.Count; i++) { w.Write(e.Members[i]); w.Write(e.Strengths[i].Raw); }
        }
        w.Write(_occupations.Count);
        foreach (Occupation o in _occupations)
        {
            w.Write(o.Civ); w.Write(o.Settlement); w.Write(o.SinceSeason); w.Write(o.RequiredSeasons);
            w.Write(o.Members.Count);
            for (int i = 0; i < o.Members.Count; i++) { w.Write(o.Members[i]); w.Write(o.Strengths[i].Raw); }
        }
        foreach (int v in _failedCampaigns) w.Write(v);
        foreach (int v in _warSince) w.Write(v);
        w.Write(RaidCount); w.Write(RaidsSucceeded); w.Write(Gifts); w.Write(BarterTrips); w.Write(CaptureCount); w.Write(Skirmishes); w.Write(Wars);
        WriteBronze(w);
        WriteScoutSimulation(w);
    }

    public void ReadSnapshot(BinaryReader r)
    {
        _season = r.ReadInt32();
        _tick = r.ReadInt64();
        _territory.Read(r);
        _dip.Read(r);
        for (int c = 0; c < _civs; c++) _posture[c] = r.ReadByte();
        for (int c = 0; c < _civs; c++)
        {
            _beacons[c].Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) _beacons[c].Add(r.ReadInt32());
        }
        _camps.Clear();
        int camps = r.ReadInt32();
        for (int i = 0; i < camps; i++) _camps.Add((r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
        _free.Clear(); _campsPlaced.Clear(); _cohesionDebt.Clear();
        int settlements = r.ReadInt32();
        for (int i = 0; i < settlements; i++)
        {
            _free.Add(r.ReadBoolean());
            _campsPlaced.Add(r.ReadBoolean());
            _cohesionDebt.Add(Fix64.FromRaw(r.ReadInt64()));
        }
        _expeditions.Clear();
        int expeditions = r.ReadInt32();
        for (int i = 0; i < expeditions; i++)
        {
            var e = new Expedition
            {
                Kind = (ExpeditionKind)r.ReadByte(), Civ = r.ReadInt32(), TargetCiv = r.ReadInt32(),
                FromSettlement = r.ReadInt32(), ToSettlement = r.ReadInt32(), ArriveSeason = r.ReadInt32(),
                Leg = r.ReadInt32(), Value = Fix64.FromRaw(r.ReadInt64()),
            };
            int members = r.ReadInt32();
            for (int m = 0; m < members; m++) { e.Members.Add(r.ReadInt64()); e.Strengths.Add(Fix64.FromRaw(r.ReadInt64())); }
            _expeditions.Add(e);
        }
        _occupations.Clear();
        int occupations = r.ReadInt32();
        for (int i = 0; i < occupations; i++)
        {
            var o = new Occupation { Civ = r.ReadInt32(), Settlement = r.ReadInt32(), SinceSeason = r.ReadInt32(), RequiredSeasons = r.ReadInt32() };
            int members = r.ReadInt32();
            for (int m = 0; m < members; m++) { o.Members.Add(r.ReadInt64()); o.Strengths.Add(Fix64.FromRaw(r.ReadInt64())); }
            _occupations.Add(o);
        }
        for (int i = 0; i < _failedCampaigns.Length; i++) _failedCampaigns[i] = r.ReadInt32();
        for (int i = 0; i < _warSince.Length; i++) _warSince[i] = r.ReadInt32();
        RaidCount = r.ReadInt32(); RaidsSucceeded = r.ReadInt32(); Gifts = r.ReadInt32(); BarterTrips = r.ReadInt32();
        CaptureCount = r.ReadInt32(); Skirmishes = r.ReadInt32(); Wars = r.ReadInt32();
        ReadBronze(r);
        ReadScoutSimulation(r);
        _events.Clear();
        RefreshSettlements();
        ApplyEconomyInputs();
    }
}
