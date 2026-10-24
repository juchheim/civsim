using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Research;

/// <summary>Settlements that belong to no civilization (Free Folk, docs/MVP/13 §9) produce no Insight and no claims.</summary>
public interface IFreeFolkRegistry
{
    bool IsFreeFolk(int settlementIndex);
}

/// <summary>
/// Adapts M3's <see cref="LivingEconomyModel"/> to what research reads (docs/MVP/05 §16): adults and elders for
/// Commons Insight, the season's work by practice family for Know-how, urgency signals from food security and
/// deaths, settlement populations for adoption, and exposure of resources for [Seen] gates.
/// <para>
/// Exposure is a stand-in for figures' line of sight, which M3 does not model: a resource is Seen once it lies
/// within the civilization's routine reach (docs/MVP/05 §20, 1.5× with WAY-01) of one of its settlements, and
/// Stone &amp; Ore Lore (INQ-03) reveals ores within the start-guarantee radius.
/// </para>
/// </summary>
public sealed class LivingEconomySource : IResearchSource
{
    /// <summary>Set by politics once it exists; until then every settlement belongs to its civilization.</summary>
    public IFreeFolkRegistry? FreeFolk { get; set; }

    private const int OreRevealRadius = 25;
    private static readonly Fix64 ReachWithTrails = Fix64.FromRatio(3, 2);

    private readonly LivingEconomyModel _economy;
    private readonly WorldEconomyInput _world;
    private readonly ResearchCatalog _catalog;
    private readonly EconomyArchetype[] _archetypes;
    private readonly int[] _eraReach;
    private readonly int _wayLandmarks, _oreLore;
    private readonly int[] _climateTarget;
    private readonly int[][] _targetTiles;
    private readonly bool[] _oreTarget;
    private readonly int[] _jobPractice = new int[Enum.GetValues<EconomyJob>().Length];
    private readonly int _tinTarget, _arsenicTarget;
    private readonly int[] _taskPractice = new int[16];

    // Per-civilization caches rebuilt each season.
    private readonly List<int>[] _settlementIndices;
    private readonly Fix64[][] _adults;
    private readonly Fix64[][] _elders;
    private readonly Fix64[][] _people;
    private readonly bool[][] _hearth;
    // E3 roles by structure (docs/MVP/22 §8.4), read at the season start like the head counts: Priests, Scribes, Cities, and
    // the settlements with a Scribe (literacy: they adopt from the capital without the distance lag).
    private readonly Fix64[] _priests, _scribes, _cities;
    private readonly bool[][] _scribeAt;
    private readonly Fix64 _cityInsight;
    private readonly int _com05;
    private readonly int[] _lastSettlementCount;
    private readonly int[][] _lastFissions;
    private readonly List<(int Parent, int Daughter)>[] _pendingFissions;
    private readonly int[] _lag;

    public LivingEconomySource(LivingEconomyModel economy, WorldEconomyInput world, GeneratedWorld generated,
        ResearchCatalog catalog, ReadOnlySpan<EconomyArchetype> archetypes)
    {
        _economy = economy;
        _world = world;
        _catalog = catalog;
        int civs = economy.CivilizationCount;
        if (archetypes.IsEmpty) _archetypes = new EconomyArchetype[civs];
        else if (archetypes.Length == civs) _archetypes = archetypes.ToArray();
        else throw new ArgumentException("One archetype per civilization.", nameof(archetypes));
        CivCount = civs;

        _eraReach = new int[ResearchCatalog.EraCount];
        foreach (ContentValue era in catalog.Pack.Root.Get("registries")!.Get("eras")!.Items)
            _eraReach[(int)era.GetInt("id")!] = (int)era.GetInt("reach")!;
        _wayLandmarks = catalog.IndexOf("WAY-01");
        _oreLore = catalog.IndexOf("INQ-03");

        // Resources that [Seen] gates name, as tile lists in the generated world.
        int targets = catalog.GateTargets.Count;
        _targetTiles = new int[targets][];
        _oreTarget = new bool[targets];
        for (int t = 0; t < targets; t++)
        {
            var tiles = new List<int>();
            string name = catalog.GateTargets[t];
            for (int i = 0; i < generated.TileCount; i++)
            {
                WorldTile tile = generated.TileAt(i);
                bool match = name switch
                {
                    "Clay" => tile.Deposit == DepositKind.Clay || _world.Tile(i).Clay,
                    "Native copper" => tile.Deposit == DepositKind.NativeCopper,
                    "Salt" => tile.Deposit == DepositKind.Salt,
                    "Tin" => tile.Deposit == DepositKind.Tin,
                    "Coal" => tile.Deposit == DepositKind.Coal,
                    // E3 [Seen] gates (docs/MVP/23 §15-§17): gold glints for MET-07, silk moths for FIB-09.
                    "Gold or Silver" => tile.Deposit is DepositKind.Gold or DepositKind.Silver,
                    "Silk moths" => tile.Deposit == DepositKind.SilkMoths,
                    _ => false,
                };
                if (match) tiles.Add(i);
            }
            if (name == "Wolves")
                foreach (PredatorPack pack in generated.Predators) if (pack.Species == PredatorSpecies.Wolves) tiles.Add(pack.Tile);
            if (name == "Wild goats or sheep")
                foreach (Herd herd in generated.Herds) if (herd.Species is HerdSpecies.WildGoatSheep or HerdSpecies.CamelLlama) tiles.Add(herd.Tile);
            if (name == "Wild horses")
                foreach (Herd herd in generated.Herds) if (herd.Species == HerdSpecies.Horse) tiles.Add(herd.Tile);
            if (name == "Camels or Llamas")
                foreach (Herd herd in generated.Herds) if (herd.Species == HerdSpecies.CamelLlama) tiles.Add(herd.Tile);
            _targetTiles[t] = tiles.ToArray();
            _oreTarget[t] = name is "Native copper" or "Tin" or "Gold or Silver";
        }

        _climateTarget = new int[civs];
        for (int c = 0; c < civs; c++)
            _climateTarget[c] = catalog.GateTargetIndex(((ClimateBand)world.Tile(world.StartingTile(c)).ClimateId).ToString());

        Array.Fill(_jobPractice, -1);
        _jobPractice[(int)EconomyJob.Forage] = catalog.PracticeIndex("forage");
        _jobPractice[(int)EconomyJob.Hunt] = catalog.PracticeIndex("hunt");
        _jobPractice[(int)EconomyJob.Fish] = catalog.PracticeIndex("fish");
        _jobPractice[(int)EconomyJob.Farm] = catalog.PracticeIndex("farm");
        _jobPractice[(int)EconomyJob.Chop] = catalog.PracticeIndex("chop");
        _jobPractice[(int)EconomyJob.Gather] = catalog.PracticeIndex("mine");
        _jobPractice[(int)EconomyJob.Craft] = catalog.PracticeIndex("craft");
        _jobPractice[(int)EconomyJob.Build] = catalog.PracticeIndex("build");
        _jobPractice[(int)EconomyJob.Preserve] = catalog.PracticeIndex("preserve");
        _jobPractice[(int)EconomyJob.TendFire] = catalog.PracticeIndex("tend_fire");
        _jobPractice[(int)EconomyJob.Herd] = catalog.PracticeIndex("herd");
        for (int t = 0; t < _taskPractice.Length; t++)
            _taskPractice[t] = LivingEconomyModel.TaskPractice((byte)t) is { } id ? catalog.PracticeIndex(id) : -1;
        _tinTarget = catalog.GateTargetIndex("Tin");
        _arsenicTarget = catalog.GateTargetIndex("Arsenic ore");

        _settlementIndices = new List<int>[civs];
        _adults = new Fix64[civs][];
        _elders = new Fix64[civs][];
        _people = new Fix64[civs][];
        _hearth = new bool[civs][];
        _lastSettlementCount = new int[civs];
        _lastFissions = new int[civs][];
        _pendingFissions = new List<(int, int)>[civs];
        _lag = new int[CivResearch.MaxSettlements];
        _priests = new Fix64[civs];
        _scribes = new Fix64[civs];
        _cities = new Fix64[civs];
        _scribeAt = new bool[civs][];
        for (int c = 0; c < civs; c++) _scribeAt[c] = new bool[CivResearch.MaxSettlements];
        // +8 Insight a year per City: OutCity × RoleScale (docs/MVP/23 §12), then InsightCalibration as all Insight.
        _cityInsight = catalog.Constant("OutCity") * catalog.Constant("RoleScale");
        _com05 = catalog.IndexOf("COM-05");
        for (int c = 0; c < civs; c++)
        {
            _settlementIndices[c] = new List<int>();
            _adults[c] = new Fix64[CivResearch.MaxSettlements];
            _elders[c] = new Fix64[CivResearch.MaxSettlements];
            _people[c] = new Fix64[CivResearch.MaxSettlements];
            _hearth[c] = new bool[CivResearch.MaxSettlements];
            _lastFissions[c] = new int[CivResearch.MaxSettlements];
            _pendingFissions[c] = new List<(int, int)>();
        }
        Refresh(detectFissions: false);
        for (int c = 0; c < civs; c++) _lastSettlementCount[c] = _settlementIndices[c].Count;
    }

    public int CivCount { get; }

    public void BeginSeason(long tick) => Refresh(detectFissions: true);

    private void Refresh(bool detectFissions)
    {
        for (int c = 0; c < CivCount; c++)
        {
            _settlementIndices[c].Clear();
            Array.Clear(_adults[c]);
            Array.Clear(_elders[c]);
            Array.Clear(_people[c]);
            Array.Clear(_hearth[c]);
            Array.Clear(_scribeAt[c]);
            _priests[c] = Fix64.Zero; _scribes[c] = Fix64.Zero; _cities[c] = Fix64.Zero;
        }
        for (int i = 0; i < _economy.SettlementCount; i++)
        {
            if (FreeFolk is not null && FreeFolk.IsFreeFolk(i)) continue;
            var view = _economy.SettlementLite(i);
            List<int> list = _settlementIndices[view.Civilization];
            if (list.Count < CivResearch.MaxSettlements) list.Add(i);
        }
        // Figures reference settlements by id; map ids to the civilization-local index.
        int maxId = 0;
        for (int i = 0; i < _economy.SettlementCount; i++) maxId = Math.Max(maxId, _economy.SettlementLite(i).Id);
        var localById = new int[maxId + 1];
        Array.Fill(localById, -1);
        for (int c = 0; c < CivCount; c++)
            for (int s = 0; s < _settlementIndices[c].Count; s++)
            {
                var view = _economy.SettlementLite(_settlementIndices[c][s]);
                localById[view.Id] = s;
                _hearth[c][s] = _economy.HearthCount(_settlementIndices[c][s]) > 0;
                if (!_economy.BronzeActive) continue;
                (int priests, int scribes) = _economy.RoleHeads(_settlementIndices[c][s]);
                _priests[c] += Fix64.FromInt(priests);
                _scribes[c] += Fix64.FromInt(scribes);
                _scribeAt[c][s] = scribes > 0;
                if (_economy.Tier(_settlementIndices[c][s]) == SettlementTier.City) _cities[c] += Fix64.One;
            }
        for (int f = 0; f < _economy.FigureCount; f++)
        {
            FigureView figure = _economy.Figure(f);
            if ((uint)figure.SettlementId >= (uint)localById.Length || localById[figure.SettlementId] < 0) continue;
            int local = localById[figure.SettlementId];
            int c = figure.Civilization;
            _people[c][local] += Fix64.One;
            if (figure.Age >= 45) _elders[c][local] += Fix64.One;
            else if (figure.Age >= 14) _adults[c][local] += Fix64.One;
        }
        if (!detectFissions) return;
        for (int c = 0; c < CivCount; c++)
        {
            int now = _settlementIndices[c].Count;
            for (int s = _lastSettlementCount[c]; s < now; s++)
            {
                int parent = 0;
                for (int o = 0; o < _lastSettlementCount[c]; o++)
                    if (_economy.SettlementLite(_settlementIndices[c][o]).Fissions > _lastFissions[c][o]) { parent = o; break; }
                _pendingFissions[c].Add((parent, s));
            }
            _lastSettlementCount[c] = now;
            for (int s = 0; s < now; s++) _lastFissions[c][s] = _economy.SettlementLite(_settlementIndices[c][s]).Fissions;
        }
    }

    public InsightInputs GetInsightInputs(int civ)
    {
        Fix64 adults = Fix64.Zero, elders = Fix64.Zero, hearthAdults = Fix64.Zero;
        int settlements = _settlementIndices[civ].Count;
        var heads = new Fix64[_catalog.Roles.Count];
        int elderRole = _catalog.RoleIndex("elder"), shamanRole = _catalog.RoleIndex("shaman");
        for (int s = 0; s < settlements; s++)
        {
            adults += _adults[civ][s];
            elders += _elders[civ][s];
            if (_hearth[civ][s]) hearthAdults += _adults[civ][s] + _elders[civ][s];
            // One Elder per settlement that has an elder; one Shaman per settlement with a viable group (docs/MVP/02 §8.1).
            if (elderRole >= 0 && _elders[civ][s] > Fix64.Zero) heads[elderRole] += Fix64.One;
            if (shamanRole >= 0 && _adults[civ][s] >= Fix64.FromInt(6)) heads[shamanRole] += Fix64.One;
        }
        int priestRole = _catalog.RoleIndex("priest"), scribeRole = _catalog.RoleIndex("scribe");
        if (priestRole >= 0) heads[priestRole] += _priests[civ];
        if (scribeRole >= 0) heads[scribeRole] += _scribes[civ];
        Fix64 workers = adults + elders;
        (Fix64 insight, Fix64 roles, Fix64 knowHow) = ArchetypeScales(_archetypes[civ]);
        return new InsightInputs
        {
            Adults = adults,
            Elders = elders,
            Settlements = settlements,
            RoleHeads = heads,
            StructureOutput = _cities[civ] * _cityInsight,
            HearthShare = workers.Raw == 0 ? Fix64.Zero : hearthAdults / workers,
            InsightScale = insight,
            RolesScale = roles,
            KnowHowScale = knowHow,
        };
    }

    /// <summary>Archetype multipliers on Insight, specialist output and Know-how (docs/MVP/14 §17, <see cref="ArchetypeResearch"/>).</summary>
    private static (Fix64 Insight, Fix64 Roles, Fix64 KnowHow) ArchetypeScales(EconomyArchetype a)
    {
        ArchetypeResearch.Scales s = ArchetypeResearch.Of(a);
        return (s.Insight, s.Roles, s.KnowHow);
    }

    /// <summary>
    /// Practice that is not an economy job, per adult per season in work-seasons, once its enabling project is done
    /// (docs/MVP/10 §11). M3 only simulates the subsistence and craft jobs; talk, ritual, teaching, counting and the
    /// like are standing background activity until the systems that own them arrive (M5 contact and trade, M7 figures).
    /// </summary>
    private static readonly (string Practice, string? Project, int Hundredths)[] AmbientPractice =
    {
        ("socialize", null, 20), ("scout", null, 10), ("haul", "TRN-01", 5), ("cook", "FOD-01", 10),
        ("tell_stories", "ART-02", 15), ("teach", "LRN-01", 10), ("ritual", "BEL-01", 10), ("council", "GOV-01", 5),
        ("count_stores", "NUM-01", 5), ("mark", "TER-01", 5), ("guard", "DEF-02", 5), ("feast", "SOC-05", 10),
        ("heal", "MED-01", 5), ("fight", "WAR-01", 5), ("paddle", "SEA-01", 5), ("trade", "TRD-01", 5),
        // E3 practice with no economy job yet (docs/MVP/23 §10): the jobs arrive with the metal chain, scribes, horses and
        // sail; until then the enabling project starts the standing activity so the lineages those projects feed fill.
        // Smelting and charcoal burning are economy jobs since the metal chain (WP6), read from each figure's task.
        ("scribe", "COM-05", 5), ("ride", "TRN-06", 5),
        ("sail", "SEA-06", 5),
    };

    public void GetPractice(int civ, IResearchView view, Span<Fix64> workByPracticeIndex, out Fix64 practisingShare)
    {
        Fix64 weighted = Fix64.Zero, weights = Fix64.Zero, adults = Fix64.Zero;
        for (int f = 0; f < _economy.FigureCount; f++)
        {
            FigureView figure = _economy.Figure(f);
            if (figure.Civilization != civ || figure.Age < 14) continue;
            // Free Folk (refugees, seceded towns, Tin Folk) carry a civilization's id but practise for no one.
            if (FreeFolk is not null && FreeFolk.IsFreeFolk(figure.SettlementId)) continue;
            Fix64 weight = figure.Age >= 45 ? Fix64.FromInt(2) : Fix64.One;
            weights += weight;
            adults += Fix64.One;
            Fix64 work = Fix64.Min(Fix64.One, figure.WorkThisSeason);
            int practice = figure.Task is > 0 and < 16 && _taskPractice[figure.Task] >= 0 ? _taskPractice[figure.Task] : _jobPractice[(int)figure.Job];
            if (work.Raw <= 0 || practice < 0) continue;
            workByPracticeIndex[practice] += work;
            weighted += work * weight;
        }
        foreach ((string practiceId, string? projectId, int hundredths) in AmbientPractice)
        {
            int practice = _catalog.PracticeIndex(practiceId);
            if (practice < 0) continue;
            if (projectId is not null && !view.IsDone(civ, _catalog.IndexOf(projectId))) continue;
            Fix64 work = adults * hundredths / 100;
            workByPracticeIndex[practice] += work;
            weighted += work;
        }
        practisingShare = weights.Raw == 0 ? Fix64.Zero : Fix64.Min(Fix64.One, weighted / weights);
    }

    public UrgencySignals GetSignals(int civ)
    {
        Fix64 famine = Fix64.Zero;
        foreach (int index in _settlementIndices[civ])
        {
            var view = _economy.SettlementLite(index);
            Fix64 security = Fix64.Min(view.LastFoodSecurity, view.LateWinterFoodForecast);
            famine = Fix64.Max(famine, Fix64.Clamp(Fix64.One - security, Fix64.Zero, Fix64.One));
        }
        int illDeaths = 0;
        for (int i = 0; i < _economy.LastDeathEventCount; i++)
        {
            EconomyDeathEvent death = _economy.LastDeathEvent(i);
            if (death.Civilization == civ && death.Cause is EconomyDeathCause.Endemic or EconomyDeathCause.Epidemic) illDeaths++;
        }
        Fix64 people = Fix64.Zero;
        for (int s = 0; s < _settlementIndices[civ].Count; s++) people += _people[civ][s];
        Fix64 disease = people.Raw == 0 ? Fix64.Zero : Fix64.Clamp(Fix64.FromInt(illDeaths) * 8 / people, Fix64.Zero, Fix64.One);
        // From KEY-3: the share of the civilization's settlements whose live Cohesion is below 40 (docs/MVP/22 §8.3), which the
        // Council answers with Kin institutions; zero before KEY-3, so E0-E2 picks are unchanged.
        Fix64 cohesion = Fix64.Zero;
        if (_economy.BronzeActive)
        {
            int live = 0, low = 0;
            foreach (int index in _settlementIndices[civ])
            {
                CivicReadout r = _economy.Civic(index);
                if (!r.CohesionLive) continue;
                live++;
                if (r.Cohesion < 40) low++;
            }
            if (live > 0) cohesion = Fix64.FromInt(low) / live;
        }
        return new UrgencySignals(famine, Fix64.Zero, disease, cohesion);
    }

    public int SettlementCount(int civ) => _settlementIndices[civ].Count;
    public Fix64 SettlementPeople(int civ, int settlement) => _people[civ][settlement];

    public int AdoptionLagSeasons(int civ, int settlement)
    {
        if (settlement == 0 || settlement >= _settlementIndices[civ].Count) return 0;
        // Literacy (COM-05): a settlement with a Scribe adopts new methods from the capital without the distance lag (docs/MVP/22 §8.4).
        if (_scribeAt[civ][settlement] && _com05 >= 0) return 0;
        int home = _economy.SettlementLite(_settlementIndices[civ][0]).SiteTile;
        int there = _economy.SettlementLite(_settlementIndices[civ][settlement]).SiteTile;
        return HexGrid.Distance(home, there, _world.Width) / 6;
    }

    public long ChooseInnovator(int civ, int project, int primaryPractice)
    {
        long best = -1, fallback = -1;
        Fix64 bestWork = Fix64.Zero;
        for (int f = 0; f < _economy.FigureCount; f++)
        {
            FigureView figure = _economy.Figure(f);
            if (figure.Civilization != civ || figure.Age < 14) continue;
            if (FreeFolk is not null && FreeFolk.IsFreeFolk(figure.SettlementId)) continue;
            if (fallback < 0 || figure.Id < fallback) fallback = figure.Id;
            if (primaryPractice < 0 || _jobPractice[(int)figure.Job] != primaryPractice) continue;
            if (best < 0 || figure.WorkThisSeason > bestWork || (figure.WorkThisSeason == bestWork && figure.Id < best))
            {
                best = figure.Id;
                bestWork = figure.WorkThisSeason;
            }
        }
        return best >= 0 ? best : fallback;
    }

    public ulong SeenMask(int civ, IResearchView view)
    {
        ulong mask = 0;
        int reach = _eraReach[Math.Min(view.Era(civ), ResearchCatalog.EraCount - 1)];
        Fix64 radius = Fix64.FromInt(reach);
        if (_wayLandmarks >= 0 && view.IsDone(civ, _wayLandmarks)) radius = radius * ReachWithTrails;
        int routine = radius.FloorToInt();
        bool ores = _oreLore >= 0 && view.IsDone(civ, _oreLore);
        for (int t = 0; t < _targetTiles.Length; t++)
        {
            int range = ores && _oreTarget[t] ? Math.Max(routine, OreRevealRadius) : routine;
            if (NearAny(civ, _targetTiles[t], range)) mask |= 1UL << t;
        }
        return mask;
    }

    private bool NearAny(int civ, int[] tiles, int range)
    {
        foreach (int settlementIndex in _settlementIndices[civ])
        {
            int site = _economy.SettlementLite(settlementIndex).SiteTile;
            foreach (int tile in tiles)
                if (HexGrid.Distance(site, tile, _world.Width) <= range) return true;
        }
        return false;
    }

    public int ClimateTarget(int civ) => _climateTarget[civ];

    /// <summary>
    /// [Has: Tin] and [Has: Arsenic ore] from the economy (docs/MVP/23 §7.3): MET-05 and MET-04 go Dormant while no own
    /// settlement has the metal in hand. Decided only once the Bronze Age has begun, so earlier checksums are unchanged.
    /// </summary>
    public bool AccessMask(int civ, out ulong decided, out ulong available)
    {
        decided = 0;
        available = 0;
        if (!_economy.BronzeActive) return false;
        if (_tinTarget is >= 0 and < 64)
        {
            decided |= 1UL << _tinTarget;
            if (_economy.CivHasMetalAccess(civ, MetalAccessKind.Tin)) available |= 1UL << _tinTarget;
        }
        if (_arsenicTarget is >= 0 and < 64)
        {
            decided |= 1UL << _arsenicTarget;
            if (_economy.CivHasMetalAccess(civ, MetalAccessKind.Arsenic)) available |= 1UL << _arsenicTarget;
        }
        return decided != 0;
    }

    public void DrainFissions(int civ, List<(int Parent, int Daughter)> into)
    {
        into.AddRange(_pendingFissions[civ]);
        _pendingFissions[civ].Clear();
    }

    public void Hash(ref StateHasher hasher)
    {
        for (int c = 0; c < CivCount; c++)
        {
            hasher.Add(_lastSettlementCount[c]);
            foreach (int f in _lastFissions[c]) hasher.Add(f);
            hasher.Add(_pendingFissions[c].Count);
            foreach ((int p, int d) in _pendingFissions[c]) { hasher.Add(p); hasher.Add(d); }
        }
    }

    public void WriteSnapshot(BinaryWriter writer)
    {
        for (int c = 0; c < CivCount; c++)
        {
            writer.Write(_lastSettlementCount[c]);
            foreach (int f in _lastFissions[c]) writer.Write(f);
            writer.Write(_pendingFissions[c].Count);
            foreach ((int p, int d) in _pendingFissions[c]) { writer.Write(p); writer.Write(d); }
        }
        // The season's caches as research last read them (docs/MVP/15 §6 D9): Insight, people and practice come from the
        // economy as it stood at the season's start, not as it stands now. Fissions resolved since then, and figures that
        // politics moved or killed, change the economy mid-season; re-deriving the caches on restore made a restored match
        // diverge from the live one within a tick.
        for (int c = 0; c < CivCount; c++)
        {
            writer.Write(_settlementIndices[c].Count);
            foreach (int i in _settlementIndices[c]) writer.Write(i);
            for (int s = 0; s < CivResearch.MaxSettlements; s++)
            {
                writer.Write(_adults[c][s].Raw); writer.Write(_elders[c][s].Raw); writer.Write(_people[c][s].Raw); writer.Write(_hearth[c][s]);
                writer.Write(_scribeAt[c][s]);
            }
            writer.Write(_priests[c].Raw); writer.Write(_scribes[c].Raw); writer.Write(_cities[c].Raw);
        }
    }

    public void ReadSnapshot(BinaryReader reader)
    {
        for (int c = 0; c < CivCount; c++)
        {
            _lastSettlementCount[c] = reader.ReadInt32();
            for (int i = 0; i < _lastFissions[c].Length; i++) _lastFissions[c][i] = reader.ReadInt32();
            int pending = reader.ReadInt32();
            _pendingFissions[c].Clear();
            for (int i = 0; i < pending; i++) _pendingFissions[c].Add((reader.ReadInt32(), reader.ReadInt32()));
        }
        for (int c = 0; c < CivCount; c++)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > CivResearch.MaxSettlements) throw new InvalidDataException("Snapshot settlement cache is invalid.");
            _settlementIndices[c].Clear();
            for (int i = 0; i < count; i++) _settlementIndices[c].Add(reader.ReadInt32());
            for (int s = 0; s < CivResearch.MaxSettlements; s++)
            {
                _adults[c][s] = Fix64.FromRaw(reader.ReadInt64()); _elders[c][s] = Fix64.FromRaw(reader.ReadInt64());
                _people[c][s] = Fix64.FromRaw(reader.ReadInt64()); _hearth[c][s] = reader.ReadBoolean();
                _scribeAt[c][s] = reader.ReadBoolean();
            }
            _priests[c] = Fix64.FromRaw(reader.ReadInt64()); _scribes[c] = Fix64.FromRaw(reader.ReadInt64()); _cities[c] = Fix64.FromRaw(reader.ReadInt64());
        }
    }
}
