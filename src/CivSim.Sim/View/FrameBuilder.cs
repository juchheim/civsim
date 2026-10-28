using System;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>
/// Fills <see cref="MatchFrame"/>s from a running <see cref="MatchModel"/> for one viewer (docs/MVP/18). It only reads
/// the model: no state is written, no random stream is drawn, so a match with or without a builder is bit-identical.
/// <para><b>Viewer.</b> A civilization index, or -1 for the omniscient debug view. A human viewer sees what its people see
/// (§4): tiles within routine sight (reach + 3, the belief builder's rule) of its settlements, within 5 of its camps and
/// within 3 of its parties or scouting figures are visible; everything ever visible stays explored with the owner as last seen;
/// rival figures, herds, parties and structures show only on visible tiles, and rival settlements once seen stay as
/// remembered markers. The builder keeps that memory (and the event log) itself: <see cref="WriteState"/> and
/// <see cref="ReadState"/> save it next to a simulation snapshot.</para>
/// <para><b>Figures.</b> The economy is aggregate per settlement and has no positions, so the builder lays figures out as a
/// pure function of (figure id, settlement, job, season, tick): a home spot by the settlement, a work tile chosen by
/// the job among the settlement's work tiles, and a 50-tick round trip between them. Scouts keep a view-state frontier
/// target and walk a terrain path out and back over one or more seasons. Party members walk with their party, occupiers
/// stand at the settlement they hold.</para>
/// <para>Call <see cref="Build(MatchModel, MatchFrame)"/> between ticks, at least once a season (150 ticks) so that no
/// season's births and deaths are missed. Allocation-free once its buffers and the target frame's have grown.</para>
/// </summary>
public sealed partial class FrameBuilder
{
    private static readonly int PalisadeStructure = LivingEconomyModel.StructureIndex("palisade");
    public const int DefaultEventCapacity = 1024;
    public const int DefaultEventWindow = 256;

    // The figure day: out to work, at work, back home, at home (ticks of a 50-tick cycle; 5 s at 1×).
    private const int CyclePeriod = 50, OutEnd = 8, WorkEnd = 35, BackEnd = 43;
    /// <summary>How far a figure stands from its tile's centre, in map units (inside the hex's inscribed circle).</summary>
    private const int Spread = 300;
    private const int WorkRadius = 3, HuntRadius = 4;
    private const int Categories = 8, MaxJobTiles = 64;
    private const int CatPlants = 0, CatHunt = 1, CatFish = 2, CatFields = 3, CatWood = 4, CatGraze = 5, CatStone = 6, CatLand = 7;
    private const int JobKinds = 14;

    private const ulong SaltHome = 0x5A17_0001UL, SaltWork = 0x5A17_0002UL, SaltWander = 0x5A17_0003UL, SaltHerd = 0x5A17_0004UL;

    private readonly GeneratedWorld _world;
    private readonly LivingEconomyModel _economy;
    private readonly WorldTile[] _tiles;
    private readonly int _width, _height, _tileCount, _civs;
    private readonly bool[] _passable;
    private readonly ResearchCatalog _catalog;
    private readonly int _war01, _war04, _hnt02, _hnt03, _hnt05, _hnt06, _stn10;
    private readonly int[] _boatProjects;
    private readonly (int Project, CivLook Look)[] _lookProjects;
    private readonly int _com04, _fir05, _agr04, _soc05, _hus01, _travois;
    /// <summary>Per civilization: the tile and village its megalith stands at (-1 until the project is done). View-only, not part of the saved view state: a loaded game picks it again.</summary>
    private readonly int[] _monumentTile, _monumentSettlement;

    // ---- memory: saved by WriteState ---------------------------------------------------------
    private int _viewer;
    private byte[] _explored;
    private sbyte[] _rememberedOwner;
    private long[] _rememberedOwnerTick;
    private int[] _seenSeason = Array.Empty<int>();
    private sbyte[] _seenCiv = Array.Empty<sbyte>();
    private int[] _seenPopulation = Array.Empty<int>();
    private bool[] _seenFree = Array.Empty<bool>();
    private int _seenSettlements;
    private long _frameSequence;

    // ---- per-build scratch (never state) -------------------------------------------------------
    private readonly int[] _stamp;
    private int _stampValue;
    private bool _omniscient;
    private int _settlementCount;
    private int[] _sPop = Array.Empty<int>(), _sAdults = Array.Empty<int>(), _sWarriors = Array.Empty<int>(), _sTile = Array.Empty<int>();
    private sbyte[] _sCiv = Array.Empty<sbyte>();
    private bool[] _sFree = Array.Empty<bool>(), _sOccupied = Array.Empty<bool>(), _sWarriorCiv = Array.Empty<bool>();

    // Parties on the road, all of them, before the visibility filter.
    private int _partyCount;
    private byte[] _pKind = Array.Empty<byte>(), _pLeg = Array.Empty<byte>();
    private sbyte[] _pCiv = Array.Empty<sbyte>(), _pTarget = Array.Empty<sbyte>();
    private int[] _pFrom = Array.Empty<int>(), _pTo = Array.Empty<int>(), _pFromTile = Array.Empty<int>(), _pToTile = Array.Empty<int>();
    private int[] _pX = Array.Empty<int>(), _pY = Array.Empty<int>(), _pTile = Array.Empty<int>(), _pMembers = Array.Empty<int>(), _pArrive = Array.Empty<int>();
    private short[] _pProgress = Array.Empty<short>();
    private byte[] _pGood = Array.Empty<byte>();
    /// <summary>The politics of the match being built (a reference for this build only, not view state).</summary>
    private PoliticsSystem? _politics;
    private int[] _pCargo = Array.Empty<int>(), _pId = Array.Empty<int>();

    // Party and occupation members by figure id: slot >= 0 a party, < 0 occupation -(slot + 1).
    private int _memberCount;
    private long[] _memberId = Array.Empty<long>();
    private int[] _memberSlot = Array.Empty<int>();

    // ---- caches: pure functions of simulation state, rebuilt when their key changes ------------
    private int _layoutSeason = -1, _layoutSettlements = -1;
    private int[] _jobTiles = Array.Empty<int>();
    private int[] _jobTileCount = Array.Empty<int>();
    private sbyte[] _rung = Array.Empty<sbyte>();
    private int _claimSeason = -1;
    private readonly byte[] _topClaim;
    private readonly sbyte[] _topCiv;
    private readonly byte[] _secondClaim, _thirdClaim;
    private readonly sbyte[] _secondCiv, _thirdCiv;
    private readonly bool[] _contested;
    private readonly int[] _walkFrom = new int[64], _walkTo = new int[64], _walkTiles = new int[64];
    private int _walkCount, _walkNext;
    private readonly int[] _bfsDistance, _bfsQueue;

    public FrameBuilder(MatchModel match, int viewerCiv, int eventCapacity = DefaultEventCapacity, int eventWindow = DefaultEventWindow)
    {
        ArgumentNullException.ThrowIfNull(match);
        _economy = match.Economy;
        _world = match.Politics.World;
        _tiles = _world.Tiles.ToArray();
        _width = _world.Width;
        _height = _world.Height;
        _tileCount = _tiles.Length;
        _civs = match.Economy.CivilizationCount;
        if (viewerCiv < -1 || viewerCiv >= _civs) throw new ArgumentOutOfRangeException(nameof(viewerCiv));
        if (eventCapacity < 16 || eventWindow < 1 || eventWindow > eventCapacity) throw new ArgumentOutOfRangeException(nameof(eventCapacity));
        _viewer = viewerCiv;
        _passable = HexGrid.BuildWalkingPassability(_tiles, _width, _height);
        _catalog = match.Research.Catalog;
        _war01 = _catalog.IndexOf("WAR-01"); _war04 = _catalog.IndexOf("WAR-04"); _hnt02 = _catalog.IndexOf("HNT-02");
        _hnt03 = _catalog.IndexOf("HNT-03"); _hnt05 = _catalog.IndexOf("HNT-05"); _hnt06 = _catalog.IndexOf("HNT-06");
        _stn10 = _catalog.IndexOf("STN-10");
        _com04 = _catalog.IndexOf("COM-04");
        _fir05 = _catalog.IndexOf("FIR-05"); _agr04 = _catalog.IndexOf("AGR-04"); _soc05 = _catalog.IndexOf("SOC-05");
        _hus01 = _catalog.IndexOf("HUS-01"); _travois = _catalog.IndexOf("TRN-02");
        _boatProjects = new[] { _catalog.IndexOf("SEA-01"), _catalog.IndexOf("SEA-02"), _catalog.IndexOf("SEA-03"), _catalog.IndexOf("SEA-04"),
            _catalog.IndexOf("SEA-05"), _catalog.IndexOf("SEA-06") };
        _lookProjects = new[]
        {
            (_catalog.IndexOf("AGR-05"), CivLook.ArdPlough), (_catalog.IndexOf("AGR-07"), CivLook.Fallow),
            (_catalog.IndexOf("HUS-02"), CivLook.HerdFollowing), (_catalog.IndexOf("HUS-06"), CivLook.WoolBreeds),
            (_catalog.IndexOf("TRN-05"), CivLook.Carts), (_catalog.IndexOf("WAR-06"), CivLook.Chariots),
            (_catalog.IndexOf("BEL-07"), CivLook.StateCult), (_catalog.IndexOf("SOC-07"), CivLook.Chiefdom),
            (_catalog.IndexOf("FIB-04"), CivLook.TailoredClothing),
        };
        _monumentTile = new int[_civs]; _monumentSettlement = new int[_civs];
        Array.Fill(_monumentTile, -1); Array.Fill(_monumentSettlement, -1);
        _explored = new byte[_tileCount];
        _rememberedOwner = new sbyte[_tileCount];
        Array.Fill(_rememberedOwner, (sbyte)-1);
        _rememberedOwnerTick = new long[_tileCount];
        Array.Fill(_rememberedOwnerTick, -1L);
        _stamp = new int[_tileCount];
        _topClaim = new byte[_tileCount];
        _topCiv = new sbyte[_tileCount];
        _secondClaim = new byte[_tileCount]; _thirdClaim = new byte[_tileCount];
        _secondCiv = new sbyte[_tileCount]; _thirdCiv = new sbyte[_tileCount];
        Array.Fill(_secondCiv, (sbyte)-1); Array.Fill(_thirdCiv, (sbyte)-1);
        _contested = new bool[_tileCount];
        _bfsDistance = new int[_tileCount];
        _bfsQueue = new int[_tileCount];
        InitializeTrailScratch();
        _ring = new ViewEvent[eventCapacity];
        _window = eventWindow;
        InitEventScratch();
        // Room for a full MVP match, so building frames does not allocate as it grows (docs/MVP/18 §9).
        EnsureSettlementScratch(128);
        EnsureSeen(128);
        EnsureParties(64);
        MatchFrame.Ensure(ref _memberId, 512);
        MatchFrame.Ensure(ref _memberSlot, 512);
        MatchFrame.Ensure(ref _jobTiles, 128 * Categories * MaxJobTiles);
        MatchFrame.Ensure(ref _jobTileCount, 128 * Categories);
        MatchFrame.Ensure(ref _rung, 128 * JobKinds);
    }

    /// <summary>The civilization frames are filtered for; -1 omniscient.</summary>
    public int ViewerCiv => _viewer;

    /// <summary>Frames built so far.</summary>
    public long FramesBuilt => _frameSequence;

    /// <summary>Switches the viewer. Exploration memory and the event log start over (a new viewer has its own fog).</summary>
    public void SetViewer(int viewerCiv)
    {
        if (viewerCiv < -1 || viewerCiv >= _civs) throw new ArgumentOutOfRangeException(nameof(viewerCiv));
        if (viewerCiv == _viewer) return;
        _viewer = viewerCiv;
        Array.Clear(_explored);
        Array.Fill(_rememberedOwner, (sbyte)-1);
        Array.Fill(_rememberedOwnerTick, -1L);
        ClearScoutViewState();
        _seenSettlements = 0;
        ResetEvents();
    }

    /// <summary>Builds a frame for <paramref name="viewerCiv"/> (switching the viewer first if it differs).</summary>
    public void Build(MatchModel match, int viewerCiv, MatchFrame target)
    {
        SetViewer(viewerCiv);
        Build(match, target);
    }

    /// <summary>Refills <paramref name="target"/> with the match as the current viewer sees it now.</summary>
    public void Build(MatchModel match, MatchFrame target)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(target);
        CheckWorld(match.Politics.World);
        LivingEconomyModel eco = match.Economy;
        PoliticsSystem pol = match.Politics;
        _omniscient = _viewer < 0;
        int season = checked((int)eco.SeasonsCompleted);

        // ---- header ---------------------------------------------------------------------------
        target.Sequence = ++_frameSequence;
        target.Tick = eco.Tick;
        target.Season = season;
        target.SeasonOfYear = season % 4;
        target.Year = season / 4;
        target.TickInSeason = (int)(eco.Tick % LivingEconomyModel.SeasonTicks);
        target.LastWinterHarsh = eco.LastWinterWasHarsh;
        target.ViewerCiv = _viewer;
        target.Width = _width;
        target.Height = _height;
        target.TotalFigures = eco.FigureCount;

        _politics = pol;
        ReadSettlements(eco, pol);
        RefreshLayout(eco, season);
        RefreshClaims(pol);
        ReadParties(match);
        PrepareScouts(eco, pol, target.Tick);
        MarkVisibility(eco, pol);
        FillTiles(eco, pol, target);
        ReadMembers(pol);
        FillFigures(eco, match.Research, target, season);
        FillSettlements(eco, match.Research, target, season);
        FillPlotsCampsBeacons(eco, pol, target);
        FillMonuments(match.Research, target);
        FillTrails(match, target);
        FillSeafaring(match.Research, target);
        FillHerdsAndPredators(eco, target);
        FillParties(target);
        FillRoutes(pol, target);
        FillOccupations(pol, target);
        FillCivs(match, target);
        FillFocus(match, target);
        FillAiWhy(match, target);
        CollectEvents(match, target, season);
    }

    private static void FillAiWhy(MatchModel match, MatchFrame target)
    {
        if (match.Ai is null)
        {
            target.AiWhyCount = 0;
            return;
        }

        var decisions = match.Ai.Log.Entries;
        int count = Math.Min(target.AiWhyEntries.Length, decisions.Count);
        int first = decisions.Count - count;
        for (int i = 0; i < count; i++)
        {
            AiDecision decision = decisions[first + i];
            target.AiWhyEntries[i] = new AiWhyEntry(decision.Tick, (byte)decision.Civ,
                decision.Layer, decision.Kind, decision.Score.Raw, decision.Reason);
        }
        target.AiWhyCount = count;
    }

    private GeneratedWorld? _checkedWorld;

    /// <summary>A builder belongs to one world; a resumed match may bring its own copy of the same world.</summary>
    private void CheckWorld(GeneratedWorld world)
    {
        if (ReferenceEquals(world, _world) || ReferenceEquals(world, _checkedWorld)) return;
        if (world.Fingerprint() != _world.Fingerprint()) throw new ArgumentException("The builder was made for another world.", nameof(world));
        _checkedWorld = world;
    }

    // ---- settlements ---------------------------------------------------------------------------

    private void ReadSettlements(LivingEconomyModel eco, PoliticsSystem pol)
    {
        int n = eco.SettlementCount;
        _settlementCount = n;
        EnsureSettlementScratch(n);
        for (int s = 0; s < n; s++)
        {
            var lite = eco.SettlementLite(s);
            _sCiv[s] = (sbyte)lite.Civilization;
            _sTile[s] = lite.SiteTile;
            _sFree[s] = pol.IsFreeFolk(s);
            _sOccupied[s] = false;
            _sPop[s] = 0;
            _sAdults[s] = 0;
        }
        for (int i = 0; i < pol.Occupations.Count; i++)
        {
            int s = pol.Occupations[i].Settlement;
            if ((s >= 0 && s < n)) _sOccupied[s] = true;
        }
        // Population and fighting-age adults (14 to 44) per settlement, from every figure.
        for (int i = 0; i < eco.FigureCount; i++)
        {
            FigureLook f = eco.LookFigure(i);
            if ((f.SettlementId < 0 || f.SettlementId >= n)) continue;
            _sPop[f.SettlementId]++;
            if (f.Age is >= 14 and < 45) _sAdults[f.SettlementId]++;
        }
    }

    private void EnsureSettlementScratch(int n)
    {
        if (_sPop.Length >= n) return;
        int size = Math.Max(n, Math.Max(16, _sPop.Length * 2));
        _sPop = new int[size]; _sAdults = new int[size]; _sWarriors = new int[size]; _sTile = new int[size];
        _sCiv = new sbyte[size]; _sFree = new bool[size]; _sOccupied = new bool[size]; _sWarriorCiv = new bool[size];
    }

    private bool IsOwn(int settlement) => !_omniscient && _sCiv[settlement] == _viewer && !_sFree[settlement];

    // ---- layout caches -------------------------------------------------------------------------

    /// <summary>Work tiles per settlement and job, and method rungs: once a season (and when settlements appear).</summary>
    private void RefreshLayout(LivingEconomyModel eco, int season)
    {
        int n = _settlementCount;
        if (season == _layoutSeason && n == _layoutSettlements) return;
        _layoutSeason = season;
        _layoutSettlements = n;
        MatchFrame.Ensure(ref _jobTiles, n * Categories * MaxJobTiles);
        MatchFrame.Ensure(ref _jobTileCount, n * Categories);
        MatchFrame.Ensure(ref _rung, n * JobKinds);
        for (int s = 0; s < n; s++)
        {
            int site = _sTile[s];
            int baseCount = s * Categories;
            for (int c = 0; c < Categories; c++) _jobTileCount[baseCount + c] = 0;
            int x = site % _width, y = site / _width;
            for (int ny = Math.Max(0, y - WorkRadius); ny <= Math.Min(_height - 1, y + WorkRadius); ny++)
            for (int nx = Math.Max(0, x - WorkRadius - 1); nx <= Math.Min(_width - 1, x + WorkRadius + 1); nx++)
            {
                int t = ny * _width + nx;
                if (t == site || HexGrid.Distance(site, t, _width) > WorkRadius) continue;
                WorldTile tile = _tiles[t];
                bool land = tile.IsLand;
                if ((tile.Flags & (TileFlags.River | TileFlags.Lake | TileFlags.CoastAdjacent)) != 0 || tile.Biome == Biome.Coast)
                    AddJobTile(s, CatFish, t);
                if (!land) continue;
                AddJobTile(s, CatLand, t);
                if (tile.ForageScore > 0) AddJobTile(s, CatPlants, t);
                if (tile.Biome is Biome.TemperateForest or Biome.Taiga or Biome.TropicalForest or Biome.OpenWoodland) AddJobTile(s, CatWood, t);
                if (tile.Biome is Biome.GrasslandSteppe or Biome.Savanna or Biome.OpenWoodland or Biome.Tundra) AddJobTile(s, CatGraze, t);
                if (tile.Deposit is DepositKind.Stone or DepositKind.Flint or DepositKind.Clay || tile.Biome == Biome.Mountain) AddJobTile(s, CatStone, t);
                if (HexGrid.Distance(site, t, _width) >= 2) AddJobTile(s, CatHunt, t);
            }
            // Hunters go where the herds are, when a herd grazes within reach.
            int herdsNear = 0;
            for (int h = 0; h < eco.WildHerdCount; h++)
            {
                WildHerdView herd = eco.WildHerd(h);
                if (herd.AnimalCount <= Fix64.Zero || HexGrid.Distance(site, herd.CurrentTile, _width) > HuntRadius) continue;
                if (herdsNear == 0) _jobTileCount[baseCount + CatHunt] = 0;
                herdsNear++;
                AddJobTile(s, CatHunt, herd.CurrentTile);
            }
            for (int i = 0; i < eco.FieldCount(s); i++) AddJobTile(s, CatFields, eco.FieldTileAt(s, i));
            for (int i = 0; i < eco.OrchardCount(s); i++) AddJobTile(s, CatFields, eco.OrchardTileAt(s, i));
            for (int j = 0; j < JobKinds; j++) _rung[s * JobKinds + j] = -1;
            foreach (EconomyJob job in RungJobs)
                _rung[s * JobKinds + (int)job] = (sbyte)Math.Clamp(eco.MethodRung(s, job), -1, 100);
        }
    }

    private static readonly EconomyJob[] RungJobs = { EconomyJob.Forage, EconomyJob.Hunt, EconomyJob.Fish, EconomyJob.Chop, EconomyJob.Gather };

    private void AddJobTile(int settlement, int category, int tile)
    {
        int slot = settlement * Categories + category;
        int count = _jobTileCount[slot];
        if (count >= MaxJobTiles) return;
        _jobTiles[slot * MaxJobTiles + count] = tile;
        _jobTileCount[slot] = count + 1;
    }

    private static int CategoryOf(EconomyJob job) => job switch
    {
        EconomyJob.Forage => CatPlants,
        EconomyJob.Hunt => CatHunt,
        EconomyJob.Fish => CatFish,
        EconomyJob.Farm => CatFields,
        EconomyJob.Chop => CatWood,
        EconomyJob.Herd => CatGraze,
        EconomyJob.Gather => CatStone,
        _ => -1,
    };

    /// <summary>A work tile for a job at a settlement, chosen by <paramref name="hash"/>; the site when there is none.</summary>
    private int WorkTile(int settlement, int category, ulong hash)
    {
        int slot = settlement * Categories + category;
        int count = _jobTileCount[slot];
        if (count == 0)
        {
            slot = settlement * Categories + CatLand;
            count = _jobTileCount[slot];
            if (count == 0) return _sTile[settlement];
        }
        return _jobTiles[slot * MaxJobTiles + (int)(hash % (ulong)count)];
    }

    /// <summary>The top claim on every tile and whether a second civilization also holds one: once a season.</summary>
    private void RefreshClaims(PoliticsSystem pol)
    {
        if (pol.Season == _claimSeason) return;
        _claimSeason = pol.Season;
        Territory territory = pol.Territory;
        for (int t = 0; t < _tileCount; t++)
        {
            int best = 0, second = 0, third = 0, who = -1, secondWho = -1, thirdWho = -1;
            for (int c = 0; c < _civs; c++)
            {
                int claim = territory.ClaimOf(c, t);
                if (claim > best)
                {
                    third = second; thirdWho = secondWho;
                    second = best; secondWho = who;
                    best = claim; who = c;
                }
                else if (claim > second)
                {
                    third = second; thirdWho = secondWho;
                    second = claim; secondWho = c;
                }
                else if (claim > third) { third = claim; thirdWho = c; }
            }
            _topClaim[t] = (byte)Math.Min(255, best / 10);
            _topCiv[t] = (sbyte)(best > 0 ? who : -1);
            _secondClaim[t] = (byte)Math.Min(255, second / 10);
            _secondCiv[t] = (sbyte)(second > 0 ? secondWho : -1);
            _thirdClaim[t] = (byte)Math.Min(255, third / 10);
            _thirdCiv[t] = (sbyte)(third > 0 ? thirdWho : -1);
            _contested[t] = second >= Territory.OwnThresholdMilli;
        }
    }

    // ---- parties -------------------------------------------------------------------------------

    private void ReadParties(MatchModel match)
    {
        PoliticsSystem pol = match.Politics;
        int n = pol.Expeditions.Count;
        EnsureParties(n);
        _partyCount = 0;
        long tick = match.Economy.Tick;
        for (int i = 0; i < n; i++)
        {
            Expedition e = pol.Expeditions[i];
            if (!PartyShown(e)) continue;
            int p = _partyCount++;
            bool back = e.Kind == ExpeditionKind.Barter && e.Leg == 1 || e.Kind == ExpeditionKind.Explore && e.Leg == 1;
            // Explorers, camp raids, and raids on caravans head for a tile, not a settlement (docs/MVP/22 §4.3, §6.3).
            int from = back ? e.ToSettlement : e.FromSettlement, to = back ? e.FromSettlement : e.ToSettlement;
            int fromTile = from >= 0 ? _sTile[from] : e.TargetTile, toTile = to >= 0 ? _sTile[to] : e.TargetTile;
            int seasons = match.Politics.TravelSeasons(e.Civ, fromTile, toTile);
            if (seasons == int.MaxValue) seasons = Math.Max(1, (WalkTiles(fromTile, toTile) + PoliticsSystem.TilesPerSeason - 1) / PoliticsSystem.TilesPerSeason);
            long leftAt = (long)(e.ArriveSeason - seasons) * LivingEconomyModel.SeasonTicks;
            long total = (long)seasons * LivingEconomyModel.SeasonTicks;
            int progress = (int)Math.Clamp((tick - leftAt) * 1000 / total, 0, 1000);
            int fx = HexLayout.CenterX(fromTile, _width), fy = HexLayout.CenterY(fromTile, _width);
            int tx = HexLayout.CenterX(toTile, _width), ty = HexLayout.CenterY(toTile, _width);
            int x = fx + (int)((long)(tx - fx) * progress / 1000), y = fy + (int)((long)(ty - fy) * progress / 1000);
            _pKind[p] = (byte)e.Kind;
            _pLeg[p] = (byte)e.Leg;
            _pCiv[p] = (sbyte)e.Civ;
            _pTarget[p] = (sbyte)e.TargetCiv;
            _pFrom[p] = from; _pTo[p] = to; _pFromTile[p] = fromTile; _pToTile[p] = toTile;
            _pX[p] = x; _pY[p] = y;
            _pTile[p] = HexLayout.NearestTile(x, y, _width, _height);
            _pProgress[p] = (short)progress;
            _pMembers[p] = e.Members.Count;
            _pArrive[p] = e.ArriveSeason;
            _pGood[p] = (byte)e.Good;
            _pCargo[p] = (int)Math.Clamp((e.Cargo * 1000).FloorToInt(), 0, int.MaxValue);
            _pId[p] = e.Id;
        }
    }

    private void EnsureParties(int n)
    {
        if (_pKind.Length >= n) return;
        int size = Math.Max(n, Math.Max(8, _pKind.Length * 2));
        Array.Resize(ref _pKind, size); Array.Resize(ref _pLeg, size); Array.Resize(ref _pCiv, size); Array.Resize(ref _pTarget, size);
        Array.Resize(ref _pFrom, size); Array.Resize(ref _pTo, size); Array.Resize(ref _pFromTile, size); Array.Resize(ref _pToTile, size);
        Array.Resize(ref _pX, size); Array.Resize(ref _pY, size); Array.Resize(ref _pTile, size); Array.Resize(ref _pMembers, size);
        Array.Resize(ref _pArrive, size); Array.Resize(ref _pProgress, size);
        Array.Resize(ref _pGood, size); Array.Resize(ref _pCargo, size); Array.Resize(ref _pId, size);
    }

    /// <summary>A party the frame can place: from a known settlement to a settlement, or to a tile (explorers, raids on caravans).</summary>
    private bool PartyShown(Expedition e)
        => e.FromSettlement >= 0 && e.FromSettlement < _settlementCount &&
           (e.ToSettlement >= 0 && e.ToSettlement < _settlementCount || e.ToSettlement < 0 && e.TargetTile >= 0 && e.TargetTile < _tileCount);

    /// <summary>Walking tiles between two tiles on the parties' graph (as PoliticsSystem.Walk), cached for 64 pairs.</summary>
    private int WalkTiles(int from, int to)
    {
        for (int i = 0; i < _walkCount; i++)
            if (_walkFrom[i] == from && _walkTo[i] == to) return _walkTiles[i];
        Array.Fill(_bfsDistance, int.MaxValue);
        int distance = HexGrid.Distance(from, to, _width);
        if (_passable[from])
        {
            int head = 0, tail = 0;
            _bfsDistance[from] = 0;
            _bfsQueue[tail++] = from;
            Span<int> neighbors = stackalloc int[6];
            while (head < tail && _bfsDistance[to] == int.MaxValue)
            {
                int tile = _bfsQueue[head++];
                int count = HexGrid.Neighbors(tile, _width, _height, neighbors);
                for (int k = 0; k < count; k++)
                {
                    int next = neighbors[k];
                    if (!_passable[next] || _bfsDistance[next] != int.MaxValue) continue;
                    _bfsDistance[next] = _bfsDistance[tile] + 1;
                    _bfsQueue[tail++] = next;
                }
            }
            if (_bfsDistance[to] != int.MaxValue) distance = _bfsDistance[to];
        }
        int slot = _walkCount < _walkFrom.Length ? _walkCount++ : _walkNext++ % _walkFrom.Length;
        _walkFrom[slot] = from; _walkTo[slot] = to; _walkTiles[slot] = distance;
        return distance;
    }

    // ---- visibility ----------------------------------------------------------------------------

    private void MarkVisibility(LivingEconomyModel eco, PoliticsSystem pol)
    {
        _stampValue = _stampValue == int.MaxValue ? 1 : _stampValue + 1;
        if (_stampValue == 1) Array.Clear(_stamp);
        if (_omniscient) return;
        int routine = BeliefRules.RoutineSight(pol.ReachOf(_viewer));
        for (int s = 0; s < _settlementCount; s++)
            if (IsOwn(s) && _sPop[s] > 0) MarkDisk(_sTile[s], routine);
        for (int i = 0; i < pol.CampCount; i++)
        {
            var camp = pol.Camp(i);
            if (camp.Civ == _viewer && (camp.Settlement >= 0 && camp.Settlement < _settlementCount) && IsOwn(camp.Settlement))
                MarkDisk(camp.Tile, Territory.CampRadius + BeliefRules.SightRadius);
        }
        for (int i = 0; i < eco.MiningCampCount; i++)
        {
            MiningCampView camp = eco.MiningCamp(i);
            if (camp.Civilization == _viewer) MarkDisk(camp.Tile, camp.ClaimRadius + BeliefRules.SightRadius);
        }
        for (int p = 0; p < _partyCount; p++)
            if (_pCiv[p] == _viewer) MarkDisk(_pTile[p], BeliefRules.SightRadius);
        MarkScoutVisibility();
        for (int i = 0; i < pol.Occupations.Count; i++)
        {
            Occupation o = pol.Occupations[i];
            if (o.Civ == _viewer && (o.Settlement >= 0 && o.Settlement < _settlementCount)) MarkDisk(_sTile[o.Settlement], BeliefRules.SightRadius);
        }
    }

    private void MarkDisk(int center, int radius)
    {
        int x = center % _width, y = center / _width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(_height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(_width - 1, x + radius + 1); nx++)
        {
            int t = ny * _width + nx;
            if (HexGrid.Distance(center, t, _width) <= radius) _stamp[t] = _stampValue;
        }
    }

    private bool Visible(int tile) => _omniscient || (tile >= 0 && tile < _tileCount) && _stamp[tile] == _stampValue;

    private void FillTiles(LivingEconomyModel eco, PoliticsSystem pol, MatchFrame target)
    {
        target.EnsureTiles(_tileCount);
        if (!_omniscient)
            for (int t = 0; t < _tileCount; t++)
                if (pol.ScoutMapExplored(_viewer, t))
                {
                    _explored[t] = 1;
                    long observationTick = pol.ScoutMapOwnerSeenTick(_viewer, t);
                    if (observationTick > _rememberedOwnerTick[t])
                    {
                        _rememberedOwner[t] = (sbyte)pol.ScoutMapOwnerSeen(_viewer, t).Owner;
                        _rememberedOwnerTick[t] = observationTick;
                    }
                }
        Territory territory = pol.Territory;
        byte[] vis = target.Visibility, claim = target.Claim, claim2 = target.Claim2, claim3 = target.Claim3,
            flags = target.TileFlags, knownDeposits = target.KnownDeposits, forest = target.ForestBand,
            worked = target.DepositWorked, miningReach = target.MiningReach;
        sbyte[] owner = target.Owner, claimant = target.Claimant, claimant2 = target.Claimant2, claimant3 = target.Claimant3;
        bool reachEnabled = _omniscient ? eco.BronzeActive : eco.IsBronzeCivilization(_viewer);
        for (int t = 0; t < _tileCount; t++)
        {
            if (Visible(t))
            {
                sbyte now = (sbyte)territory.OwnerOf(t);
                vis[t] = (byte)TileVisibility.Visible;
                owner[t] = now;
                claim[t] = _topClaim[t];
                claimant[t] = _topCiv[t];
                claim2[t] = _secondClaim[t]; claimant2[t] = _secondCiv[t];
                claim3[t] = _thirdClaim[t]; claimant3[t] = _thirdCiv[t];
                flags[t] = _contested[t] ? (byte)TileViewFlags.Contested : (byte)0;
                if (!_omniscient) { _explored[t] = 1; _rememberedOwner[t] = now; _rememberedOwnerTick[t] = target.Tick; }
            }
            else
            {
                // An expedition's report reveals terrain to the whole civilization (docs/MVP/22 §4.3).
                if (!_omniscient && _explored[t] == 0 && pol.IsRevealed(_viewer, t)) _explored[t] = 1;
                bool known = _explored[t] != 0;
                vis[t] = known ? (byte)TileVisibility.Explored : (byte)TileVisibility.Unknown;
                owner[t] = known ? _rememberedOwner[t] : (sbyte)-1;
                claim[t] = 0;
                claimant[t] = -1;
                claim2[t] = 0; claimant2[t] = -1;
                claim3[t] = 0; claimant3[t] = -1;
                flags[t] = 0;
            }
            knownDeposits[t] = _tiles[t].Deposit != DepositKind.None && (_omniscient || _explored[t] != 0)
                ? (byte)1
                : (byte)0;
            forest[t] = Visible(t) ? (byte)eco.ForestBand(t) : (byte)0;
            worked[t] = Visible(t) && eco.DepositWorked(t) ? (byte)1 : (byte)0;
            miningReach[t] = 0;
            if ((_omniscient || _explored[t] != 0) && reachEnabled)
            {
                for (int s = 0; s < _settlementCount; s++)
                {
                    if (_sPop[s] == 0 || _sFree[s] || (!_omniscient && _sCiv[s] != _viewer)) continue;
                    if (HexGrid.Distance(_sTile[s], t, _width) <= eco.MiningReachFrom(s))
                    {
                        miningReach[t] = 1;
                        break;
                    }
                }
            }
        }
        if (_omniscient) return;
        // The home range hatch: half the scouting reach around the viewer's settlements (08 §4.1).
        int half = pol.ReachOf(_viewer) / 2;
        for (int s = 0; s < _settlementCount; s++)
        {
            if (!IsOwn(s) || _sPop[s] == 0) continue;
            int center = _sTile[s], x = center % _width, y = center / _width;
            for (int ny = Math.Max(0, y - half); ny <= Math.Min(_height - 1, y + half); ny++)
            for (int nx = Math.Max(0, x - half - 1); nx <= Math.Min(_width - 1, x + half + 1); nx++)
            {
                int t = ny * _width + nx;
                if (HexGrid.Distance(center, t, _width) <= half) flags[t] |= (byte)TileViewFlags.HomeRange;
            }
        }
    }

    // ---- figures -------------------------------------------------------------------------------

    private void ReadMembers(PoliticsSystem pol)
    {
        int n = 0;
        for (int i = 0; i < pol.Expeditions.Count; i++) n += pol.Expeditions[i].Members.Count;
        for (int i = 0; i < pol.Occupations.Count; i++) n += pol.Occupations[i].Members.Count;
        MatchFrame.Ensure(ref _memberId, n);
        MatchFrame.Ensure(ref _memberSlot, n);
        _memberCount = 0;
        // Party slots follow ReadParties, which skips expeditions with a settlement out of range.
        int party = 0;
        for (int i = 0; i < pol.Expeditions.Count; i++)
        {
            Expedition e = pol.Expeditions[i];
            if (!PartyShown(e)) continue;
            for (int m = 0; m < e.Members.Count; m++) { _memberId[_memberCount] = e.Members[m]; _memberSlot[_memberCount++] = party; }
            party++;
        }
        for (int i = 0; i < pol.Occupations.Count; i++)
        {
            Occupation o = pol.Occupations[i];
            if ((o.Settlement < 0 || o.Settlement >= _settlementCount)) continue;
            for (int m = 0; m < o.Members.Count; m++) { _memberId[_memberCount] = o.Members[m]; _memberSlot[_memberCount++] = -(o.Settlement + 1); }
        }
        // Insertion sort by id (parties are small; Array.Sort would allocate its helper on first use).
        for (int i = 1; i < _memberCount; i++)
        {
            long id = _memberId[i];
            int slot = _memberSlot[i], j = i - 1;
            while (j >= 0 && _memberId[j] > id) { _memberId[j + 1] = _memberId[j]; _memberSlot[j + 1] = _memberSlot[j]; j--; }
            _memberId[j + 1] = id;
            _memberSlot[j + 1] = slot;
        }
    }

    private void FillFigures(LivingEconomyModel eco, ResearchSystem research, MatchFrame target, int season)
    {
        int count = eco.FigureCount;
        target.EnsureFigures(count);
        for (int s = 0; s < _settlementCount; s++)
        {
            _sWarriorCiv[s] = _war04 >= 0 && _sCiv[s] >= 0 && _sCiv[s] < _civs && research.IsDone(_sCiv[s], _war04);
            _sWarriors[s] = _sWarriorCiv[s] ? _sAdults[s] / 5 : 0;
        }
        long tick = target.Tick;
        int member = 0, written = 0;
        long previousId = long.MinValue;
        Array.Clear(_birthsByCiv);
        for (int i = 0; i < count; i++)
        {
            FigureLook f = eco.LookFigure(i);
            if (f.Id <= previousId) throw new InvalidOperationException("Economy figures are expected in ascending id order.");
            previousId = f.Id;
            int s = f.SettlementId;
            if ((s < 0 || s >= _settlementCount)) continue;
            if (f.Id > _maxFigureId)
            {
                if (_eventsPrimed && (f.Civilization >= 0 && f.Civilization < _civs)) _birthsByCiv[f.Civilization]++;
                _maxFigureId = f.Id;
            }
            while (member < _memberCount && _memberId[member] < f.Id) member++;
            int slot = member < _memberCount && _memberId[member] == f.Id ? _memberSlot[member] : int.MaxValue;

            bool wounded = f.WoundSeasons > 0, ill = f.IllnessSeasons > 0;
            bool adult = f.Age is >= 14 and < 45;
            bool warrior = false;
            if (_sWarriors[s] > 0 && adult && !f.Female && !wounded) { warrior = true; _sWarriors[s]--; }

            FigureFlags flags = FigureFlags.None;
            if (f.Female) flags |= FigureFlags.Female;
            if (wounded) flags |= FigureFlags.Wounded;
            if (ill) flags |= FigureFlags.Ill;
            if (f.PregnancySeasons > 0) flags |= FigureFlags.Pregnant;
            if (f.Hunger >= HungryLevel) flags |= FigureFlags.Hungry;
            if (f.Warmth < ColdLevel) flags |= FigureFlags.Cold;
            if (warrior) flags |= FigureFlags.Warrior | FigureFlags.Armed;
            if (_sFree[s]) flags |= FigureFlags.FreeFolk;

            ulong home = Hash64.Mix(unchecked((ulong)f.Id ^ SaltHome));
            int siteTile = _sTile[s];
            int homeX = HexLayout.CenterX(siteTile, _width) + Jitter(home, 0, Spread);
            int homeY = HexLayout.CenterY(siteTile, _width) + Jitter(home, 10, Spread);
            FigureActivity activity;
            CarryKind carry = CarryKind.None;
            int x, y, tile;
            if (slot != int.MaxValue && slot >= 0)
            {
                // With a party on the road.
                int p = slot;
                activity = (ExpeditionKind)_pKind[p] switch
                {
                    ExpeditionKind.Gift => FigureActivity.OnGift,
                    ExpeditionKind.Barter => FigureActivity.OnBarter,
                    ExpeditionKind.Raid => FigureActivity.OnRaid,
                    ExpeditionKind.Explore => FigureActivity.OnExpedition,
                    ExpeditionKind.Caravan => FigureActivity.OnCaravan,
                    _ => FigureActivity.OnAssault,
                };
                flags |= FigureFlags.Away;
                if (_pProgress[p] < 1000) flags |= FigureFlags.Walking;
                if (activity is FigureActivity.OnRaid or FigureActivity.OnAssault) flags |= FigureFlags.Armed;
                else carry = CarryKind.Goods;
                x = _pX[p] + Jitter(home, 20, Spread / 2);
                y = _pY[p] + Jitter(home, 30, Spread / 2);
                tile = _pTile[p];
            }
            else if (slot != int.MaxValue)
            {
                // Holding an occupied settlement.
                int held = -slot - 1;
                activity = FigureActivity.Occupying;
                flags |= FigureFlags.Away | FigureFlags.Armed;
                tile = _sTile[held];
                x = HexLayout.CenterX(tile, _width) + Jitter(home, 20, Spread);
                y = HexLayout.CenterY(tile, _width) + Jitter(home, 30, Spread);
            }
            else if (f.Job == EconomyJob.Scout && TryScoutPosition(f.Id, out ScoutPosition scout))
            {
                activity = FigureActivity.Scout;
                flags |= FigureFlags.Away | FigureFlags.Walking;
                tile = scout.Tile;
                x = scout.X + Jitter(home, 20, Spread / 2);
                y = scout.Y + Jitter(home, 30, Spread / 2);
            }
            else
            {
                activity = f.Age < 14 ? FigureActivity.Child
                    : wounded || ill ? FigureActivity.Convalescing
                    : ActivityForJob(f.Job);
                int category = activity is FigureActivity.Child or FigureActivity.Convalescing ? -1 : CategoryOf(f.Job);
                if (category < 0)
                {
                    // Home-bound: a slow wander around the home spot.
                    ulong wander = Hash64.Mix(unchecked(home ^ SaltWander ^ (ulong)(tick / 20)));
                    int reach = activity == FigureActivity.TendFire ? Spread / 3 : Spread / 6;
                    if (activity == FigureActivity.TendFire)
                    {
                        homeX = HexLayout.CenterX(siteTile, _width) + Jitter(home, 0, Spread / 4);
                        homeY = HexLayout.CenterY(siteTile, _width) + Jitter(home, 10, Spread / 4);
                    }
                    x = homeX + Jitter(wander, 0, reach);
                    y = homeY + Jitter(wander, 10, reach);
                    tile = siteTile;
                }
                else
                {
                    ulong work = Hash64.Mix(unchecked((ulong)f.Id ^ SaltWork ^ ((ulong)season * 0x9E3779B97F4A7C15UL)));
                    int workTile = WorkTile(s, category, work);
                    int workX = HexLayout.CenterX(workTile, _width) + Jitter(work, 20, Spread);
                    int workY = HexLayout.CenterY(workTile, _width) + Jitter(work, 30, Spread);
                    int phase = (int)((tick + (long)((home >> 40) % CyclePeriod)) % CyclePeriod);
                    if (phase < OutEnd)
                    {
                        x = homeX + (workX - homeX) * phase / OutEnd;
                        y = homeY + (workY - homeY) * phase / OutEnd;
                        tile = phase * 2 < OutEnd ? siteTile : workTile;
                        flags |= FigureFlags.Walking;
                    }
                    else if (phase < WorkEnd)
                    {
                        ulong wander = Hash64.Mix(unchecked(work ^ SaltWander ^ (ulong)(tick / 10)));
                        x = workX + Jitter(wander, 0, Spread / 6);
                        y = workY + Jitter(wander, 10, Spread / 6);
                        tile = workTile;
                    }
                    else if (phase < BackEnd)
                    {
                        int step = phase - WorkEnd, span = BackEnd - WorkEnd;
                        x = workX + (homeX - workX) * step / span;
                        y = workY + (homeY - workY) * step / span;
                        tile = step * 2 < span ? workTile : siteTile;
                        flags |= FigureFlags.Walking;
                        carry = CarryOf(f.Job);
                    }
                    else
                    {
                        x = homeX;
                        y = homeY;
                        tile = siteTile;
                    }
                }
            }

            bool own = !_omniscient && f.Civilization == _viewer && !_sFree[s];
            if (!own && !Visible(tile)) continue;
            int w = written++;
            target.FigureId[w] = f.Id;
            target.FigureCiv[w] = (sbyte)f.Civilization;
            target.FigureSettlement[w] = s;
            target.FigureTile[w] = tile;
            target.FigureX[w] = x;
            target.FigureY[w] = y;
            target.FigureActivity[w] = (byte)activity;
            target.FigureRole[w] = f.Role;
            target.FigureAge[w] = (byte)(f.Age < 14 ? AgeBand.Child : f.Age < 45 ? AgeBand.Adult : AgeBand.Elder);
            target.FigureHealth[w] = (byte)HealthOf(f.Health);
            target.FigureFlags[w] = (ushort)flags;
            int rungJob = (int)f.Job;
            target.FigureToolRung[w] = activity <= FigureActivity.Herd && rungJob < JobKinds ? _rung[s * JobKinds + rungJob] : (sbyte)-1;
            MethodReadout method = activity is > FigureActivity.Idle and <= FigureActivity.Herd
                ? eco.MethodReadout(s, (EconomyJob)activity)
                : new MethodReadout(target.FigureToolRung[w], target.FigureToolRung[w], 1000, MethodFallbackReason.None, MethodToolId.None);
            target.FigureKnownToolRung[w] = (sbyte)Math.Clamp(method.KnownRung, -1, 100);
            target.FigureMethodEfficiencyPermille[w] = (short)Math.Clamp(method.EfficiencyPermille, 0, 1000);
            target.FigureMethodFallback[w] = (byte)method.Reason;
            target.FigureMethodFallbackTool[w] = (byte)method.RequiredTool;
            target.FigureCarry[w] = (byte)carry;
        }
        target.FigureCount = written;
    }

    private static readonly Fix64 HungryLevel = Fix64.FromRatio(3, 10);
    private static readonly Fix64 ColdLevel = Fix64.Half;

    private static FigureActivity ActivityForJob(EconomyJob job)
        => job == EconomyJob.Scout ? FigureActivity.Scout : (FigureActivity)(int)job;

    /// <summary>A signed offset in [-range, range] from ten bits of a hash.</summary>
    private static int Jitter(ulong hash, int shift, int range) => (int)((hash >> shift) & 1023) * (2 * range) / 1023 - range;

    private static HealthBand HealthOf(Fix64 health)
        => health >= Fix64.FromRatio(3, 4) ? HealthBand.Good : health >= Fix64.Half ? HealthBand.Fair
            : health >= Fix64.FromRatio(1, 4) ? HealthBand.Poor : HealthBand.Critical;

    private static CarryKind CarryOf(EconomyJob job) => job switch
    {
        EconomyJob.Forage => CarryKind.Plants,
        EconomyJob.Hunt => CarryKind.Meat,
        EconomyJob.Fish => CarryKind.Fish,
        EconomyJob.Farm => CarryKind.Grain,
        EconomyJob.Chop => CarryKind.Wood,
        EconomyJob.Gather => CarryKind.Stone,
        _ => CarryKind.None,
    };

    // ---- settlements, plots, camps, Beacons -----------------------------------------------------

    private void EnsureSeen(int n)
    {
        if (_seenSeason.Length >= n) return;
        int old = _seenSeason.Length, size = Math.Max(n, Math.Max(16, old * 2));
        Array.Resize(ref _seenSeason, size); Array.Resize(ref _seenCiv, size); Array.Resize(ref _seenPopulation, size); Array.Resize(ref _seenFree, size);
        for (int i = old; i < size; i++) _seenSeason[i] = -1;
    }

    private void FillSettlements(LivingEconomyModel eco, ResearchSystem research, MatchFrame target, int season)
    {
        int n = _settlementCount;
        EnsureSeen(n);
        for (int s = _seenSettlements; s < n; s++) _seenSeason[s] = -1;
        _seenSettlements = Math.Max(_seenSettlements, n);
        target.EnsureSettlements(n);
        int kinds = MatchFrame.StructureKinds;
        int written = 0;
        for (int s = 0; s < n; s++)
        {
            bool own = IsOwn(s);
            bool current = _omniscient || own || Visible(_sTile[s]);
            if (current && !_omniscient)
            {
                _seenSeason[s] = season; _seenCiv[s] = _sCiv[s]; _seenPopulation[s] = _sPop[s]; _seenFree[s] = _sFree[s];
            }
            else if (!current && _seenSeason[s] < 0) continue;
            int w = written++;
            target.SettlementIndex[w] = s;
            target.SettlementTile[w] = _sTile[s];
            int structureBase = w * kinds;
            if (!current)
            {
                target.SettlementCiv[w] = _seenCiv[s];
                target.SettlementPopulation[w] = _seenPopulation[s];
                target.SettlementFood[w] = 0; target.SettlementWinterForecast[w] = 0; target.SettlementCohesion[w] = 0;
                target.SettlementOverreach[w] = 0; target.SettlementSpanPenalty[w] = 0;
                SettlementViewFlags remembered = SettlementViewFlags.Remembered;
                if (_seenFree[s]) remembered |= SettlementViewFlags.FreeFolk;
                if (_seenPopulation[s] == 0) remembered |= SettlementViewFlags.Empty;
                target.SettlementFlags[w] = (ushort)remembered;
                target.SettlementBuildKind[w] = -1; target.SettlementBuildPermille[w] = 0;
                target.SettlementSmallStock[w] = 0; target.SettlementCattle[w] = 0; target.SettlementPigs[w] = 0;
                target.SettlementSeenSeason[w] = _seenSeason[s];
                Array.Clear(target.SettlementStructures, structureBase, kinds);
                target.SettlementHalls[w] = 0;
                continue;
            }
            var lite = eco.SettlementLite(s);
            target.SettlementCiv[w] = _sCiv[s];
            target.SettlementPopulation[w] = _sPop[s];
            target.SettlementFood[w] = (byte)FoodBandOf(lite.LastFoodSecurity);
            target.SettlementWinterForecast[w] = (byte)FoodBandOf(lite.LateWinterFoodForecast);
            target.SettlementCohesion[w] = (byte)CohesionOf(eco.SettlementCohesion(s));
            if (own)
            {
                (int overreach, int spanPenalty) = eco.CohesionPressures(s);
                target.SettlementOverreach[w] = (short)Math.Clamp(overreach, 0, short.MaxValue);
                target.SettlementSpanPenalty[w] = (short)Math.Clamp(spanPenalty, 0, short.MaxValue);
            }
            else { target.SettlementOverreach[w] = 0; target.SettlementSpanPenalty[w] = 0; }
            SettlementViewFlags flags = SettlementViewFlags.None;
            if (_sFree[s]) flags |= SettlementViewFlags.FreeFolk;
            if (eco.SettlementFireLit(s)) flags |= SettlementViewFlags.FireLit;
            if (eco.SettlementFissionPending(s)) flags |= SettlementViewFlags.FissionPending;
            if (_sOccupied[s]) flags |= SettlementViewFlags.Occupied;
            if (_sPop[s] == 0) flags |= SettlementViewFlags.Empty;
            if (own) flags |= SettlementViewFlags.Own;
            for (int k = 0; k < kinds; k++)
                target.SettlementStructures[structureBase + k] = (byte)Math.Min(255, eco.StructureCount(s, k));
            target.SettlementHalls[w] = (byte)Math.Min(255, eco.HallCount(s));
            if (eco.DefenseStructures(s, lookout: false) > 0) flags |= SettlementViewFlags.ThornBarrier;
            if (eco.DefenseStructures(s, lookout: true) > 0) flags |= SettlementViewFlags.Lookout;
            if (PalisadeStructure >= 0 && eco.StructureCount(s, PalisadeStructure) > 0)
                flags |= SettlementViewFlags.Palisade;
            if (_politics is { } pol && pol.TinFolk.Count > 0 && pol.IsTinFolk(s)) flags |= SettlementViewFlags.TinFolk | SettlementViewFlags.Palisade;
            if (own && eco.CorveeAt(s) >= 0) flags |= SettlementViewFlags.Corvee;
            if (_politics is { } lp && !_omniscient && lp.LevyFor(_viewer, s) is not null) flags |= SettlementViewFlags.LevyTarget;
            target.SettlementFlags[w] = (ushort)flags;
            (int kind, Fix64 share) = eco.SettlementBuild(s);
            target.SettlementBuildKind[w] = (sbyte)kind;
            target.SettlementBuildPermille[w] = (short)(share * 1000).FloorToInt();
            (int small, int cattle, int pigs) = eco.DomesticStock(s);
            target.SettlementSmallStock[w] = small; target.SettlementCattle[w] = cattle; target.SettlementPigs[w] = pigs;
            target.SettlementSeenSeason[w] = season;
        }
        target.SettlementCount = written;
    }

    private static FoodBand FoodBandOf(Fix64 security)
        => security >= Fix64.One ? FoodBand.Secure : security >= Fix64.FromRatio(9, 10) ? FoodBand.Tight
            : security >= Fix64.FromRatio(7, 10) ? FoodBand.Short : FoodBand.Famine;

    private static CohesionBand CohesionOf(Fix64 cohesion)
        => cohesion >= Fix64.One ? CohesionBand.Strong : cohesion >= Fix64.FromRatio(9, 10) ? CohesionBand.Steady
            : cohesion >= Fix64.FromRatio(3, 4) ? CohesionBand.Strained : CohesionBand.Fraying;

    private void FillPlotsCampsBeacons(LivingEconomyModel eco, PoliticsSystem pol, MatchFrame target)
    {
        int plots = 0;
        for (int s = 0; s < _settlementCount; s++) plots += eco.FieldCount(s) + eco.OrchardCount(s);
        target.EnsurePlots(plots);
        int w = 0;
        for (int s = 0; s < _settlementCount; s++)
        {
            bool own = IsOwn(s);
            for (int i = 0; i < eco.FieldCount(s); i++)
            {
                int tile = eco.FieldTileAt(s, i);
                if (!own && !Visible(tile)) continue;
                target.PlotTile[w] = tile; target.PlotSettlement[w] = s; target.PlotKind[w++] = 0;
            }
            for (int i = 0; i < eco.OrchardCount(s); i++)
            {
                int tile = eco.OrchardTileAt(s, i);
                if (!own && !Visible(tile)) continue;
                target.PlotTile[w] = tile; target.PlotSettlement[w] = s; target.PlotKind[w++] = 1;
            }
        }
        target.PlotCount = w;

        target.EnsureCamps(pol.CampCount + eco.MiningCampCount);
        w = 0;
        for (int i = 0; i < pol.CampCount; i++)
        {
            var camp = pol.Camp(i);
            if ((camp.Settlement < 0 || camp.Settlement >= _settlementCount) || _sFree[camp.Settlement] || _sCiv[camp.Settlement] != camp.Civ) continue;
            bool own = !_omniscient && camp.Civ == _viewer;
            if (!own && !Visible(camp.Tile)) continue;
            target.CampTile[w] = camp.Tile; target.CampCiv[w] = (sbyte)camp.Civ;
            target.CampId[w] = -1;
            target.CampKind[w] = (byte)CampKind.Seasonal; target.CampParent[w] = camp.Settlement;
            target.CampDeposit[w] = (byte)DepositKind.None; target.CampCrew[w] = 0;
            target.CampBuilt[w] = 1; target.CampMineHead[w] = 0; w++;
            target.TileFlags[camp.Tile] |= (byte)TileViewFlags.Camp;
        }
        for (int i = 0; i < eco.MiningCampCount; i++)
        {
            MiningCampView camp = eco.MiningCamp(i);
            bool own = !_omniscient && camp.Civilization == _viewer;
            if (!own && !Visible(camp.Tile)) continue;
            target.CampTile[w] = camp.Tile; target.CampCiv[w] = (sbyte)camp.Civilization;
            target.CampId[w] = camp.Id;
            target.CampKind[w] = (byte)CampKind.Mining; target.CampParent[w] = camp.ParentSettlementIndex;
            target.CampDeposit[w] = (byte)camp.Deposit; target.CampCrew[w] = (short)Math.Clamp(camp.Crew, 0, short.MaxValue);
            target.CampBuilt[w] = camp.Built ? (byte)1 : (byte)0; target.CampMineHead[w] = camp.MineHead ? (byte)1 : (byte)0; w++;
            target.TileFlags[camp.Tile] |= (byte)TileViewFlags.Camp;
        }
        target.CampCount = w;

        int beacons = 0;
        for (int c = 0; c < _civs; c++) beacons += pol.BeaconsOf(c).Count;
        target.EnsureBeacons(beacons);
        w = 0;
        for (int c = 0; c < _civs; c++)
        {
            var list = pol.BeaconsOf(c);
            for (int i = 0; i < list.Count; i++)
            {
                int tile = list[i];
                if ((tile < 0 || tile >= _tileCount)) continue;
                bool own = !_omniscient && c == _viewer;
                if (!own && !Visible(tile)) continue;
                target.BeaconTile[w] = tile; target.BeaconCiv[w++] = (sbyte)c;
                target.TileFlags[tile] |= (byte)TileViewFlags.Beacon;
            }
        }
        target.BeaconCount = w;
    }

    // ---- herds, predators, parties, occupations -------------------------------------------------

    /// <summary>The best boat each civilization knows; the front end puts it under fishers working a shore or a river.</summary>
    private void FillSeafaring(ResearchSystem research, MatchFrame target)
    {
        if (target.CivSeaTier.Length < _civs) target.CivSeaTier = new byte[_civs];
        for (int c = 0; c < _civs; c++)
        {
            int tier = 0;
            for (int i = 0; i < _boatProjects.Length; i++)
                if (_boatProjects[i] >= 0 && research.IsDone(c, _boatProjects[i])) tier = i + 1;
            target.CivSeaTier[c] = (byte)tier;
        }
        if (target.CivSignals.Length < _civs) target.CivSignals = new byte[_civs];
        for (int c = 0; c < _civs; c++) target.CivSignals[c] = (byte)(_com04 >= 0 && research.IsDone(c, _com04) ? 1 : 0);
        if (target.CivBurnLearned.Length < _civs * 2) target.CivBurnLearned = new long[_civs * 2];
        for (int c = 0; c < _civs; c++)
        {
            target.CivBurnLearned[c * 2] = _fir05 >= 0 && research.IsDone(c, _fir05) ? research.CompletedTick(c, _fir05) : -1;
            target.CivBurnLearned[c * 2 + 1] = _agr04 >= 0 && research.IsDone(c, _agr04) ? research.CompletedTick(c, _agr04) : -1;
        }
        if (target.CivFeastLearned.Length < _civs) target.CivFeastLearned = new long[_civs];
        for (int c = 0; c < _civs; c++)
            target.CivFeastLearned[c] = _soc05 >= 0 && research.IsDone(c, _soc05) ? research.CompletedTick(c, _soc05) : -1;
        if (target.CivDogs.Length < _civs) target.CivDogs = new byte[_civs];
        for (int c = 0; c < _civs; c++)
        {
            bool dogs = _hus01 >= 0 && research.IsDone(c, _hus01);
            target.CivDogs[c] = (byte)(!dogs ? 0 : _travois >= 0 && research.IsDone(c, _travois) ? 2 : 1);
        }
        if (target.CivLooks.Length < _civs) target.CivLooks = new ushort[_civs];
        for (int c = 0; c < _civs; c++)
        {
            CivLook looks = CivLook.None;
            foreach ((int project, CivLook look) in _lookProjects)
                if (project >= 0 && research.IsDone(c, project)) looks |= look;
            target.CivLooks[c] = (ushort)looks;
        }
    }

    /// <summary>
    /// The megalith a civilization raised stands at its oldest living village from the moment the project is learned, and stays
    /// there. Only the tile, the learning tick and the village size go out; the front end plays the hauling from them.
    /// </summary>
    private void FillMonuments(ResearchSystem research, MatchFrame target)
    {
        target.EnsureMonuments(_civs);
        int w = 0;
        for (int c = 0; c < _civs && _stn10 >= 0; c++)
        {
            if (!research.IsDone(c, _stn10)) continue;
            if (_monumentTile[c] < 0)
                for (int s = 0; s < _settlementCount; s++)
                    if (!_sFree[s] && _sCiv[s] == c && _sPop[s] > 0) { _monumentTile[c] = _sTile[s]; _monumentSettlement[c] = s; break; }
            int tile = _monumentTile[c];
            if (tile < 0 || !(_omniscient || c == _viewer || Visible(tile))) continue;
            int home = _monumentSettlement[c];
            target.MonumentTile[w] = tile;
            target.MonumentCiv[w] = (sbyte)c;
            target.MonumentTick[w] = research.CompletedTick(c, _stn10);
            target.MonumentPopulation[w++] = (uint)home < (uint)_settlementCount ? _sPop[home] : 8;
        }
        target.MonumentCount = w;
    }

    private void FillHerdsAndPredators(LivingEconomyModel eco, MatchFrame target)
    {
        int n = eco.WildHerdCount;
        target.EnsureHerds(n);
        int w = 0, previousId = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            WildHerdView herd = eco.WildHerd(i);
            if (herd.Id <= previousId) throw new InvalidOperationException("Wild herds are expected in ascending id order.");
            previousId = herd.Id;
            int animals = herd.AnimalCount.FloorToInt();
            if (animals <= 0 || !Visible(herd.CurrentTile)) continue;
            ulong h = Hash64.Mix(unchecked((ulong)herd.Id ^ SaltHerd ^ (ulong)herd.CurrentTile * 0x9E3779B97F4A7C15UL));
            target.HerdId[w] = herd.Id;
            target.HerdSpecies[w] = (byte)herd.Species;
            target.HerdTile[w] = herd.CurrentTile;
            target.HerdX[w] = HexLayout.CenterX(herd.CurrentTile, _width) + Jitter(h, 0, Spread / 2);
            target.HerdY[w] = HexLayout.CenterY(herd.CurrentTile, _width) + Jitter(h, 10, Spread / 2);
            target.HerdAnimals[w] = animals;
            target.HerdFear[w] = (byte)Math.Clamp((herd.Fear * 255).FloorToInt(), 0, 255);
            target.HerdAway[w++] = herd.CurrentTile != herd.HomeTile ? (byte)1 : (byte)0;
        }
        target.HerdCount = w;

        ReadOnlySpan<PredatorPack> packs = _world.Predators;
        target.EnsurePredators(packs.Length);
        w = 0;
        for (int i = 0; i < packs.Length; i++)
        {
            if (!Visible(packs[i].Tile)) continue;
            target.PredatorTile[w] = packs[i].Tile;
            target.PredatorSpecies[w] = (byte)packs[i].Species;
            target.PredatorSize[w++] = packs[i].Count;
        }
        target.PredatorCount = w;
    }

    private void FillParties(MatchFrame target)
    {
        target.EnsureParties(_partyCount);
        int w = 0;
        for (int p = 0; p < _partyCount; p++)
        {
            bool own = !_omniscient && _pCiv[p] == _viewer;
            if (!own && !Visible(_pTile[p])) continue;
            target.PartyKind[w] = _pKind[p]; target.PartyCiv[w] = _pCiv[p]; target.PartyTargetCiv[w] = _pTarget[p];
            target.PartyFrom[w] = _pFrom[p]; target.PartyTo[w] = _pTo[p];
            target.PartyFromTile[w] = _pFromTile[p]; target.PartyToTile[w] = _pToTile[p];
            target.PartyX[w] = _pX[p]; target.PartyY[w] = _pY[p]; target.PartyProgress[w] = _pProgress[p];
            target.PartyMembers[w] = _pMembers[p]; target.PartyArriveSeason[w] = _pArrive[p];
            target.PartyGood[w] = _pGood[p]; target.PartyCargoPermille[w] = _pCargo[p]; target.PartyId[w] = _pId[p];
            target.PartyLeg[w++] = _pLeg[p];
        }
        target.PartyCount = w;
    }

    /// <summary>The viewer's compacts as routes (every compact in omniscient mode), docs/MVP/22 §6.2 and §14.</summary>
    private void FillRoutes(PoliticsSystem pol, MatchFrame target)
    {
        int n = 0;
        foreach (Compact c in pol.Compacts) if (c.Active) n++;
        target.EnsureRoutes(n);
        int w = 0;
        foreach (Compact c in pol.Compacts)
        {
            if (!c.Active || !_omniscient && !c.Involves(_viewer)) continue;
            int from = c.LastSeller >= 0 ? c.LastSeller : c.SellerFolk, to = c.LastBuyer >= 0 ? c.LastBuyer : c.BuyerFolk;
            target.RouteCompact[w] = c.Id;
            target.RouteSellerCiv[w] = (sbyte)c.SellerCiv; target.RouteBuyerCiv[w] = (sbyte)c.BuyerCiv;
            target.RouteSellerSettlement[w] = from; target.RouteBuyerSettlement[w] = to;
            target.RouteFromTile[w] = from >= 0 && from < _settlementCount ? _sTile[from] : -1;
            target.RouteToTile[w] = to >= 0 && to < _settlementCount ? _sTile[to] : -1;
            target.RouteGood[w] = (byte)c.Good;
            target.RouteVolumePermille[w] = (int)Math.Clamp((c.LastVolume * 1000).FloorToInt(), 0, int.MaxValue);
            target.RouteDeliveries[w] = c.Deliveries; target.RouteSinceSeason[w++] = c.SinceSeason;
        }
        target.RouteCount = w;
    }

    private void FillOccupations(PoliticsSystem pol, MatchFrame target)
    {
        target.EnsureOccupations(pol.Occupations.Count);
        int w = 0;
        for (int i = 0; i < pol.Occupations.Count; i++)
        {
            Occupation o = pol.Occupations[i];
            if ((o.Settlement < 0 || o.Settlement >= _settlementCount)) continue;
            int tile = _sTile[o.Settlement];
            bool own = !_omniscient && (o.Civ == _viewer || IsOwn(o.Settlement));
            if (!own && !Visible(tile)) continue;
            target.OccupationCiv[w] = (sbyte)o.Civ; target.OccupationSettlement[w] = o.Settlement; target.OccupationTile[w] = tile;
            target.OccupationHolders[w] = o.Members.Count; target.OccupationSinceSeason[w] = o.SinceSeason;
            target.OccupationRequiredSeasons[w++] = o.RequiredSeasons;
        }
        target.OccupationCount = w;
    }

    // ---- civilizations and the focus bar --------------------------------------------------------

    private void FillCivs(MatchModel match, MatchFrame target)
    {
        ResearchSystem research = match.Research;
        PoliticsSystem pol = match.Politics;
        Diplomacy dip = pol.Diplomacy;
        target.EnsureCivs(_civs);
        target.CivCount = _civs;
        for (int c = 0; c < _civs; c++)
        {
            bool self = _omniscient || c == _viewer;
            CivViewFlags flags = self ? CivViewFlags.Self | CivViewFlags.Detailed : CivViewFlags.None;
            if (!_omniscient && c != _viewer)
            {
                if (dip.Aware(_viewer, c)) flags |= CivViewFlags.Aware;
                if (dip.Contacted(_viewer, c)) flags |= CivViewFlags.Contacted;
                if (dip.AtWar(_viewer, c)) flags |= CivViewFlags.AtWar;
                if (dip.Allied(_viewer, c)) flags |= CivViewFlags.Allied;
                if (dip.InTruce(_viewer, c, pol.Season)) flags |= CivViewFlags.InTruce;
                if (pol.HasPact(_viewer, c)) flags |= CivViewFlags.Pact;
                if (pol.HasTreaty(_viewer, c)) flags |= CivViewFlags.Treaty;
            }
            target.CivEmbargoByViewer[c] = (byte)(_omniscient || c == _viewer ? 0 : pol.EmbargoMask(_viewer, c));
            target.CivEmbargoOnViewer[c] = (byte)(_omniscient || c == _viewer ? 0 : pol.EmbargoMask(c, _viewer));
            target.CivFlags[c] = (ushort)flags;
            int era = research.Era(c);
            target.CivEra[c] = (byte)era;
            target.CivBreadthDone[c] = (short)research.DoneInEra(c, era);
            target.CivBreadthNeeded[c] = (short)_catalog.EraBreadth[Math.Min(era, ResearchCatalog.EraCount - 1)];
            target.CivLegacy[c] = research.Legacy(c).Raw;
            target.CivFirstsLegacy[c] = research.FirstsLegacy(c).Raw;
            target.CivDominion[c] = pol.Territory.Dominion(c).Raw;
            target.CivLandBasisPoints[c] = pol.Territory.ShareBasisPoints(c);
            target.CivPosture[c] = (byte)pol.PostureOf(c);
            target.CivRelations[c] = (short)(_omniscient || c == _viewer ? 0 : dip.Relations(_viewer, c));
            target.CivWeaponTier[c] = WeaponTier(research, c);
            if (self)
            {
                int population = 0, settlements = 0;
                for (int s = 0; s < _settlementCount; s++)
                    if (_sCiv[s] == c && !_sFree[s] && _sPop[s] > 0) { population += _sPop[s]; settlements++; }
                target.CivPopulation[c] = population;
                target.CivSettlements[c] = settlements;
                target.CivProjectsDone[c] = research.DoneCount(c);
                target.CivInsightPerYear[c] = research.InsightPerYear(c).Raw;
                target.CivMandate[c] = (byte)Math.Clamp(research.Mandate(c), 0, 255);
                target.CivMandateCap[c] = (byte)Math.Clamp(research.MandateCap(c), 0, 255);
                target.CivMandateNext[c] = (byte)Math.Clamp(research.MandateSeasonsToNext(c), 0, 255);
            }
            else
            {
                target.CivPopulation[c] = 0; target.CivSettlements[c] = 0; target.CivProjectsDone[c] = 0; target.CivInsightPerYear[c] = 0;
                target.CivMandate[c] = 0; target.CivMandateCap[c] = 0; target.CivMandateNext[c] = 0;
            }
        }
    }

    private byte WeaponTier(ResearchSystem research, int civ)
    {
        if (Done(research, civ, _hnt06)) return 5;
        if (Done(research, civ, _hnt05)) return 4;
        if (Done(research, civ, _hnt03)) return 3;
        if (Done(research, civ, _hnt02)) return 2;
        if (Done(research, civ, _war01)) return 1;
        return 0;
    }

    private static bool Done(ResearchSystem research, int civ, int project) => project >= 0 && research.IsDone(civ, project);

    private void FillFocus(MatchModel match, MatchFrame target)
    {
        ResearchSystem research = match.Research;
        int civ = _omniscient ? 0 : _viewer;
        target.FocusCiv = civ;
        for (int slot = 0; slot < 3; slot++)
        {
            int project = research.Slot(civ, slot);
            target.FocusSlotProject[slot] = project;
            short permille = 0;
            if (project >= 0)
            {
                Fix64 cost = research.EffectiveCost(civ, project);
                if (cost > Fix64.Zero)
                    permille = (short)Math.Clamp((research.Progress(civ, project) * 1000 / cost).FloorToInt(), 0, 1000);
            }
            target.FocusSlotPermille[slot] = permille;
        }
        var queue = research.Queue(civ);
        int n = Math.Min(queue.Count, target.FocusQueue.Length);
        for (int i = 0; i < n; i++) target.FocusQueue[i] = queue[i];
        target.FocusQueueCount = n;
        target.FocusCouncil = research.CouncilOn(civ);
        for (int theme = 0; theme < 3; theme++) target.FocusLeaning[theme] = (byte)Math.Clamp(research.Leaning(civ, theme), 0, 100);
        target.FocusBeaconsAllowed = match.Politics.BeaconAllowance(civ);
        target.FocusBeaconsPlaced = match.Politics.BeaconsOf(civ).Count;
        target.FocusEra = research.Era(civ);
        int lineages = _catalog.LineageCodes.Count;
        MatchFrame.Ensure(ref target.FocusKnowHow, lineages);
        target.FocusKnowHowCount = lineages;
        for (int lineage = 0; lineage < lineages; lineage++)
            target.FocusKnowHow[lineage] = research.KnowHow(civ, lineage).Raw;
        int projects = _catalog.Count;
        MatchFrame.Ensure(ref target.FocusProjectState, projects);
        for (int p = 0; p < projects; p++) target.FocusProjectState[p] = (byte)research.State(civ, p);
        target.FocusProjectCount = projects;
        PoliticsSystem pol = match.Politics;
        target.FocusMandateUnlocked = research.MandateUnlocked(civ);
        target.FocusMandate = research.Mandate(civ);
        target.FocusMandateCap = research.MandateCap(civ);
        target.FocusMandateNextSeason = research.MandateSeasonsToNext(civ);
        for (int k = 1; k <= (int)DecreeKind.Compact; k++)
            target.FocusDecreeCost[k] = (byte)Math.Clamp(target.FocusMandateUnlocked ? pol.DecreeCost(civ, (DecreeKind)k) : 0, 0, 255);
        int pending = 0;
        foreach (PendingDecree d in pol.PendingDecrees) if (d.Civ == civ) pending++;
        target.FocusPendingDecrees = pending;
        target.FocusLevyTarget = -1; target.FocusLevyHost = 0; target.FocusLevyUntilSeason = 0;
        foreach (Levy l in pol.Levies)
            if (l.Civ == civ && l.UntilSeason > pol.Season)
            { target.FocusLevyTarget = l.TargetSettlement; target.FocusLevyHost = l.Members.Count; target.FocusLevyUntilSeason = l.UntilSeason; }
        target.FocusDecreesDone = pol.DecreesDone(civ);
        target.FocusSeasonsAtCap = pol.MandateSeasonsAtCap(civ);
    }
}
