using System;
using System.Collections.Generic;
using System.IO;
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
}

/// <summary>Stable reason codes for a rejected player order.</summary>
public enum OrderRefusalReason : byte { InvalidTarget = 1, RequirementsNotMet = 2, InvalidState = 3 }

/// <summary>Something that happened between civilizations, for the Chronicle (docs/MVP/13 §10).</summary>
public readonly record struct PoliticsEvent(long Tick, byte Civ, PoliticsEventKind Kind, int Other, int A, int B);

public enum ExpeditionKind : byte { Gift, Barter, Raid, Assault }

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
/// Territory, contact and conflict (M5; docs/MVP/05 §11 to §12, 07b, 13). Once a season it recomputes claims and
/// ownership, notices rivals and rolls first contact, moves the parties on the road, and lets each civilization's Civ
/// Mind (a plain rule set until M6) choose gifts, barter, raids and wars. The same rules run for every civilization;
/// the player steers with Posture and Beacons.
/// </summary>
public sealed partial class PoliticsSystem : IFissionSiteAdvisor, IFreeFolkRegistry
{
    private static readonly ulong ContactStream = Hash64.OfString("politics.contact");
    private static readonly ulong SkirmishStream = Hash64.OfString("politics.skirmish");
    private static readonly ulong RaidStream = Hash64.OfString("politics.raid");
    private static readonly ulong ForecastStream = Hash64.OfString("politics.forecast");
    private static readonly ulong AssaultStream = Hash64.OfString("politics.assault");
    private static readonly ulong CaptureStream = Hash64.OfString("politics.capture");

    /// <summary>Tiles a party covers in a season on foot.</summary>
    public const int TilesPerSeason = 10;

    private readonly LivingEconomyModel _economy;
    private readonly ResearchSystem _research;
    private readonly ResearchCatalog _catalog;
    private readonly GeneratedWorld _generated;
    private readonly int _width, _height, _civs;
    private readonly ulong _seed;
    private readonly bool[] _passable;
    private readonly WorldTile[] _tileArray;
    private readonly EconomyArchetype[] _archetypes;
    private readonly int[] _eraReach, _eraRadius;
    private readonly Fix64[] _eraClaimMult;
    private readonly Fix64[] _raidMargin;
    private readonly Territory _territory;
    private readonly Diplomacy _dip;
    private readonly byte[] _posture;
    private readonly bool[] _externalMind;
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
            Def01, Def02, Def03, Hus01, War01, War02, War03, War04, Hnt02, Hnt03, Hnt05, Hnt06, Gov02, Gov03, Art05, Bel01;

        public P(ResearchCatalog c)
        {
            Way01 = c.IndexOf("WAY-01"); Way02 = c.IndexOf("WAY-02"); Way03 = c.IndexOf("WAY-03"); Trn02 = c.IndexOf("TRN-02");
            Ter02 = c.IndexOf("TER-02"); Trd01 = c.IndexOf("TRD-01"); Trd02 = c.IndexOf("TRD-02"); Dip01 = c.IndexOf("DIP-01");
            Dip03 = c.IndexOf("DIP-03"); Com01 = c.IndexOf("COM-01"); Com02 = c.IndexOf("COM-02"); Com04 = c.IndexOf("COM-04");
            Def01 = c.IndexOf("DEF-01"); Def02 = c.IndexOf("DEF-02"); Def03 = c.IndexOf("DEF-03"); Hus01 = c.IndexOf("HUS-01");
            War01 = c.IndexOf("WAR-01"); War02 = c.IndexOf("WAR-02"); War03 = c.IndexOf("WAR-03"); War04 = c.IndexOf("WAR-04");
            Hnt02 = c.IndexOf("HNT-02"); Hnt03 = c.IndexOf("HNT-03"); Hnt05 = c.IndexOf("HNT-05"); Hnt06 = c.IndexOf("HNT-06");
            Gov02 = c.IndexOf("GOV-02"); Gov03 = c.IndexOf("GOV-03"); Art05 = c.IndexOf("ART-05"); Bel01 = c.IndexOf("BEL-01");
        }
    }

    public PoliticsSystem(LivingEconomyModel economy, ResearchSystem research, GeneratedWorld generated,
        ReadOnlySpan<EconomyArchetype> archetypes, ulong seed)
    {
        _economy = economy;
        _research = research;
        _catalog = research.Catalog;
        _generated = generated;
        _width = generated.Width;
        _height = generated.Height;
        _civs = economy.CivilizationCount;
        _seed = seed;
        _archetypes = archetypes.ToArray();
        if (_archetypes.Length != _civs) throw new ArgumentException("One archetype per civilization.", nameof(archetypes));
        _p = new P(_catalog);
        _tileArray = generated.Tiles.ToArray();
        _passable = HexGrid.BuildWalkingPassability(_tileArray, _width, _height);
        _territory = new Territory(generated, _civs);
        _dip = new Diplomacy(_civs);
        _posture = new byte[_civs];
        _externalMind = new bool[_civs];
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
                    if (command.A >= 0 && command.A < _width && command.B >= 0 && command.B < _height &&
                        _beacons[civ].Count < BeaconAllowance(civ))
                    {
                        _beacons[civ].Add(command.B * _width + command.A);
                        _events.Add(new PoliticsEvent(tick, (byte)civ, PoliticsEventKind.BeaconPlaced, -1, command.A, command.B));
                    }
                    break;
                case CommandKind.RemoveBeacon:
                    if (command.A >= 0 && command.A < _beacons[civ].Count) _beacons[civ].RemoveAt(command.A);
                    break;
                // A human player's politics orders: the AI's Order* rules, checked here (docs/MVP/18 §7).
                case CommandKind.SendGift:
                    if (command.A < 0 || command.A >= _civs || !OrderGift(civ, command.A))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.SendBarter:
                    if (command.A < 0 || command.A >= _civs || !OrderBarter(civ, command.A))
                        Refused(command, command.A, command.A < 0 || command.A >= _civs ? OrderRefusalReason.InvalidTarget : OrderRefusalReason.RequirementsNotMet);
                    break;
                case CommandKind.LaunchRaid:
                    if (command.A < 0 || command.A >= _sCount) Refused(command, command.A, OrderRefusalReason.InvalidTarget);
                    else if (!OrderRaid(civ, command.A, command.B)) Refused(command, command.A, OrderRefusalReason.RequirementsNotMet);
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
            }
        }
    }

    /// <summary>Posture Expand lowers the fission threshold 20%, Prosper raises it 20% (docs/MVP/05 §6.2).</summary>
    public void ApplyEconomyInputs()
    {
        for (int c = 0; c < _civs; c++)
            _economy.SetFissionThresholdScale(c, (Posture)_posture[c] switch
            {
                Posture.Expand => Fix64.FromRatio(8, 10),
                Posture.Prosper => Fix64.FromRatio(12, 10),
                _ => Fix64.One,
            });
    }

    // ---- fission site advice (IFissionSiteAdvisor) ---------------------------------------

    /// <summary>
    /// Rival-owned land is off limits, and so is ground within 23 terrain-weighted tiles of a rival settlement, just
    /// beyond what two E1 reaches span: bands settle where they can hold a claim, not on a neighbour's doorstep
    /// (docs/MVP/05 §9.4, 07b fission sites), and contact comes from reach, not from a daughter band wandering over.
    /// </summary>
    public bool Allows(int civ, int tile)
    {
        int owner = _territory.OwnerOf(tile);
        if (owner != -1 && owner != civ) return false;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] == civ || _free[s]) continue;
            if (RivalCost(s)[tile] < MinRivalCostTenths) return false;
        }
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
        PlaceCamps();
        UpdateTerritory();
        if (_season % 4 == 0)
        {
            _territory.AccrueDominion();
            _dip.YearlyDrift();
        }
        DetectContacts();
        ResolveExpeditions();
        AdvanceOccupations();
        RecoverCohesion();
        RunCivMinds();
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
            foreach (int tile in _territory.ChooseCampSites(civ, _sTile[s], radius, own, ownCamps, rivals, 2))
                _camps.Add((tile, s, civ));
            _campsPlaced[s] = true;
        }
    }

    private void UpdateTerritory()
    {
        var settlements = new List<ClaimSource>();
        var camps = new List<ClaimSource>();
        for (int s = 0; s < _sCount; s++)
        {
            if (_free[s]) continue;
            int civ = _sCiv[s];
            settlements.Add(new ClaimSource(civ, _sTile[s], _eraRadius[Math.Min(_research.Era(civ), _eraRadius.Length - 1)], EmissionOf(s)));
        }
        foreach ((int tile, int settlement, int civ) in _camps)
        {
            if (settlement >= _sCount || _free[settlement] || _sCiv[settlement] != civ) continue;
            // A band's seasonal camp claims at half its emission, three quarters with Camp Circuits (07b §9.4).
            Fix64 share = _research.IsDone(civ, _p.Ter02) ? Fix64.FromRatio(3, 4) : Fix64.Half;
            camps.Add(new ClaimSource(civ, tile, Territory.CampRadius, EmissionOf(settlement) * share));
        }
        _territory.Update(settlements, camps);
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
                // Reach is counted in the terrain-weighted tiles the start pairs are placed by, with a tenth added for trails.
                int sight = (ReachOf(a) + ReachOf(b)) * 11 / 10;
                int d = WalkingTilesBetween(a, b, sight + 3);
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
        if (_research.IsDone(civ, _p.Hnt06)) { ranged = true; return Fix64.FromRatio(18, 10) * (_research.IsDone(civ, _p.War03) ? Fix64.FromRatio(15, 10) : Fix64.One); }
        if (_research.IsDone(civ, _p.Hnt05)) { ranged = true; return Fix64.FromRatio(17, 10); }
        if (_research.IsDone(civ, _p.Hnt03)) return Fix64.FromRatio(16, 10);
        if (_research.IsDone(civ, _p.Hnt02)) return Fix64.FromRatio(13, 10);
        if (_research.IsDone(civ, _p.War01)) return Fix64.One;
        return Fix64.FromRatio(6, 10);
    }

    /// <summary>Fighters a settlement can put up: adults and elders who are not laid up, with warriors first (13 §6.1, §7.1).</summary>
    private List<Fighter> Fighters(int settlement, bool attacking, out List<long> ids)
    {
        int civ = _sCiv[settlement];
        Fix64 weapon = Weapon(civ, out _);
        bool warriors = _research.IsDone(civ, _p.War04);
        var records = new List<LivingEconomyModel.FighterRecord>();
        _economy.ListFightingAge(settlement, records);
        int adults = 0;
        foreach (var r in records) if (r.Age < 45) adults++;
        int warriorSlots = warriors ? adults / 5 : 0;
        var list = new List<Fighter>();
        ids = new List<long>();
        foreach (var r in records)
        {
            if (r.Wounded) continue;
            Fix64 strength;
            if (r.Age >= 45) strength = Fix64.FromRatio(3, 10) * weapon;
            else if (!r.Female && warriorSlots > 0)
            {
                warriorSlots--;
                strength = Fix64.One * weapon * Fix64.FromRatio(13, 10);
            }
            else strength = (attacking ? Fix64.One : Fix64.FromRatio(6, 10)) * weapon;
            list.Add(new Fighter(r.Id, strength));
            ids.Add(r.Id);
        }
        return list;
    }

    /// <summary>The adults who go: warriors and men first, never so many that fewer than four stay home (13 §5.2).</summary>
    private List<Fighter> PickParty(int settlement, int civ, int size, bool attacking, out List<long> ids, int stayHome = 4)
    {
        List<Fighter> all = Fighters(settlement, attacking, out _);
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
        bool palisade = _research.IsDone(civ, _p.Def03) && _season - SeasonOf(_research.CompletedTick(civ, _p.Def03)) >= 2;
        bool watchers = _research.IsDone(civ, _p.Def02) || _economy.DefenseStructures(settlement, lookout: true) > 0;
        bool thorn = _economy.DefenseStructures(settlement, lookout: false) > 0;
        return WithPostureDefenses(civ, new Defenses(thorn, palisade, watchers, _research.IsDone(civ, _p.Hus01), _research.IsDone(civ, _p.Com04),
            _research.IsDone(civ, _p.Com01), HillSite: false, _research.IsDone(civ, _p.War03), Night: false,
            RaidedRecently: _season - _dip.LastRaidOnAny(civ) <= 4));
    }

    private static int SeasonOf(long tick) => checked((int)(tick / ResearchSystem.SeasonTicks)) + 1;

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
                int overlap = ContestedOverlapTiles(a, b);
                if (overlap >= 5) _dip.Shift(a, b, -2 * (overlap / 5));
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
        h.Add(_season);
        _territory.Hash(ref h);
        _dip.Hash(ref h);
        foreach (byte p in _posture) h.Add(p);
        for (int c = 0; c < _civs; c++) { h.Add(_beacons[c].Count); foreach (int b in _beacons[c]) h.Add(b); }
        h.Add(_camps.Count);
        foreach ((int tile, int settlement, int civ) in _camps) { h.Add(tile); h.Add(settlement); h.Add(civ); }
        for (int i = 0; i < _free.Count; i++) { h.Add(_free[i]); h.Add(i < _campsPlaced.Count && _campsPlaced[i]); h.Add(i < _cohesionDebt.Count ? _cohesionDebt[i] : Fix64.Zero); }
        h.Add(_expeditions.Count);
        foreach (Expedition e in _expeditions)
        {
            h.Add((int)e.Kind); h.Add(e.Civ); h.Add(e.TargetCiv); h.Add(e.FromSettlement); h.Add(e.ToSettlement);
            h.Add(e.ArriveSeason); h.Add(e.Leg); h.Add(e.Value);
            foreach (long m in e.Members) h.Add(m);
            foreach (Fix64 s in e.Strengths) h.Add(s);
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
        w.Write(_free.Count);
        for (int i = 0; i < _free.Count; i++)
        {
            w.Write(_free[i]);
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
        _events.Clear();
        RefreshSettlements();
        ApplyEconomyInputs();
    }
}
