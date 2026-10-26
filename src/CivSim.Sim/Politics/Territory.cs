using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A place that emits claim: a settlement or a seasonal camp.</summary>
public readonly record struct ClaimSource(int Civ, int Tile, int Radius, Fix64 Emission);

/// <summary>One legal Found-site projection in the paired-frontier diagnostic.</summary>
public readonly record struct FoundFrontierCandidateProjection(int Civ, int Tile, int DonorSettlement,
    int DonorPopulation, int DaughterPopulation, Fix64 DonorFill, int DistanceToFrontier,
    int ClaimedWeight, int FrontierConnectionWeight, bool PairOwnersTouching, bool Under42PercentCap,
    bool CouncilPreferred);

/// <summary>Found-site legality and policy gates for one civilization in the frontier diagnostic.</summary>
public readonly record struct FoundFrontierCivSummary(int Civ, int LegalSiteCount, int StrategistCandidateCount,
    int RelevantSiteCount, int ProjectedSiteCount, bool AiFoundGateOpen, int Mandate, int MandateCap,
    int FoundDecrees, int NaturalFoundings, int CouncilPreferredTile);

/// <summary>Read-only Found-site frontier counterfactual at a paired B17 snapshot.</summary>
public readonly record struct FoundFrontierAudit(int BaselineClaimedWeight, int BaselineFrontierConnectionWeight,
    bool BaselinePairOwnersTouching, int OwnershipUpdatesProjected, FoundFrontierCivSummary[] Civilizations,
    FoundFrontierCandidateProjection[] Candidates);

/// <summary>
/// Claims, ownership and Dominion (docs/MVP/05 §11, 07b §9.4). Each source adds
/// <c>Emission × max(0, 1 − d / (r + 1))</c> to its civilization's claim on every tile in its disk. A tile belongs to the
/// civilization whose claim is at least T = 0.7 and leads the runner-up by 20%, and only after leading for a full season,
/// so borders do not flicker. Tile data is read-only; all state is arrays in ascending tile order.
/// </summary>
public sealed class Territory
{
    /// <summary>Claim strength is kept in milli-units; T = 0.7.</summary>
    public const int OwnThresholdMilli = 700;
    private const int LeadNumerator = 6, LeadDenominator = 5; // leader must hold 1.2x the runner-up
    public const int CampRadius = 2;
    private const int NoOwner = -1;

    private readonly WorldTile[] _tiles;
    private readonly int _width, _height, _civs;
    private readonly int[][] _claim;       // [civ][tile], milli
    private readonly sbyte[] _owner;
    private readonly sbyte[] _pendingWho;  // -2 none
    private readonly byte[] _pendingSeasons;
    private readonly int[] _ownedWeight;   // per civ, hundredths of habitable weight
    private readonly int[] _ownedTiles;
    private int _totalHabitableWeight;
    private readonly Fix64[] _dominion;
    private long _seasons;

    public Territory(GeneratedWorld world, int civs)
    {
        _tiles = world.Tiles.ToArray();
        _width = world.Width;
        _height = world.Height;
        _civs = civs;
        _claim = new int[civs][];
        for (int c = 0; c < civs; c++) _claim[c] = new int[_tiles.Length];
        _owner = new sbyte[_tiles.Length];
        Array.Fill(_owner, (sbyte)NoOwner);
        _pendingWho = new sbyte[_tiles.Length];
        Array.Fill(_pendingWho, (sbyte)-2);
        _pendingSeasons = new byte[_tiles.Length];
        _ownedWeight = new int[civs];
        _ownedTiles = new int[civs];
        _dominion = new Fix64[civs];
        foreach (WorldTile t in _tiles) _totalHabitableWeight += t.HabitableWeight;
    }

    private Territory(Territory source)
    {
        _tiles = source._tiles;
        _width = source._width;
        _height = source._height;
        _civs = source._civs;
        _claim = new int[source._claim.Length][];
        for (int civ = 0; civ < _claim.Length; civ++) _claim[civ] = (int[])source._claim[civ].Clone();
        _owner = (sbyte[])source._owner.Clone();
        _pendingWho = (sbyte[])source._pendingWho.Clone();
        _pendingSeasons = (byte[])source._pendingSeasons.Clone();
        _ownedWeight = (int[])source._ownedWeight.Clone();
        _ownedTiles = (int[])source._ownedTiles.Clone();
        _totalHabitableWeight = source._totalHabitableWeight;
        _dominion = (Fix64[])source._dominion.Clone();
        _seasons = source._seasons;
    }

    public int Width => _width;
    public int Height => _height;
    public int TileCount => _tiles.Length;
    public WorldTile TileAt(int tile) => _tiles[tile];
    public int OwnerOf(int tile) => _owner[tile];
    public int ClaimOf(int civ, int tile) => _claim[civ][tile];
    public int OwnedTiles(int civ) => _ownedTiles[civ];
    /// <summary>Habitable weight (hundredths) owned by a civilization.</summary>
    public int OwnedWeight(int civ) => _ownedWeight[civ];
    public int TotalHabitableWeight => _totalHabitableWeight;
    public Fix64 Dominion(int civ) => _dominion[civ];

    /// <summary>Share of all habitable land owned by anyone (the "claimed land" of 07b), in basis points.</summary>
    public int ClaimedBasisPoints()
    {
        long owned = 0;
        foreach (int w in _ownedWeight) owned += w;
        return _totalHabitableWeight == 0 ? 0 : (int)(owned * 10_000 / _totalHabitableWeight);
    }

    /// <summary>Share of all habitable land owned by one civilization, in basis points.</summary>
    public int ShareBasisPoints(int civ)
        => _totalHabitableWeight == 0 ? 0 : (int)((long)_ownedWeight[civ] * 10_000 / _totalHabitableWeight);

    /// <summary>
    /// Recomputes every civilization's claim field from its sources (docs/MVP/05 §11.1), then moves ownership one
    /// season's worth toward it. Camps on land another civilization owns do not claim (07b §9.4).
    /// </summary>
    public void Update(IReadOnlyList<ClaimSource> settlements, IReadOnlyList<ClaimSource> camps)
    {
        for (int c = 0; c < _civs; c++) Array.Clear(_claim[c]);
        foreach (ClaimSource s in settlements) Radiate(s);
        foreach (ClaimSource camp in camps)
        {
            int owner = _owner[camp.Tile];
            if (owner != NoOwner && owner != camp.Civ) continue;
            Radiate(camp);
        }
        ResolveOwnership();
        _seasons++;
    }

    /// <summary>
    /// Returns habitable weight that would be claimed from these sources if ownership changed immediately. This is a
    /// diagnostic projection: it uses the normal claim threshold and lead rule, but does not change live ownership or
    /// its one-season hysteresis state.
    /// </summary>
    public int ProjectedClaimedWeight(IReadOnlyList<ClaimSource> settlements, IReadOnlyList<ClaimSource> camps)
        => ProjectedClaimedWeightAndFrontierCost(settlements, camps, -1, -1).ClaimedWeight;

    /// <summary>
    /// Projects pair contact after a bounded number of normal ownership updates while keeping claim sources fixed.
    /// Copies hysteresis state, then advances the copy so a counterfactual can include border-settling seasons.
    /// </summary>
    public bool ProjectedPairOwnersTouchingAfterUpdates(IReadOnlyList<ClaimSource> settlements,
        IReadOnlyList<ClaimSource> camps, int civA, int civB, int updates)
    {
        if (updates < 0) throw new ArgumentOutOfRangeException(nameof(updates));
        if (updates == 0)
            return ProjectedClaimedWeightAndFrontierCost(settlements, camps, civA, civB).PairOwnersTouching;
        var projection = new Territory(this);
        for (int i = 0; i < updates; i++) projection.Update(settlements, camps);
        return PairOwnersTouching(projection._owner, civA, civB);
    }

    /// <summary>
    /// Projects immediate ownership without mutating live territory, and optionally measures the minimum added
    /// habitable weight needed to connect two projected owners through land. Water and third-party-owned tiles are
    /// excluded; camp-on-rival-land filtering uses current live ownership, as it does during an update.
    /// </summary>
    public (int ClaimedWeight, int FrontierConnectionWeight, int[] FrontierPath, int[] FrontierUnownedPath,
        bool PairOwnersTouching) ProjectedClaimedWeightAndFrontierCost(
        IReadOnlyList<ClaimSource> settlements, IReadOnlyList<ClaimSource> camps, int civA, int civB)
    {
        var claims = new int[_civs][];
        for (int c = 0; c < _civs; c++) claims[c] = new int[_tiles.Length];
        foreach (ClaimSource source in settlements) Radiate(source, claims);
        foreach (ClaimSource camp in camps)
        {
            int owner = _owner[camp.Tile];
            if (owner != NoOwner && owner != camp.Civ) continue;
            Radiate(camp, claims);
        }

        var owners = new sbyte[_tiles.Length];
        Array.Fill(owners, (sbyte)NoOwner);
        int ownedWeight = 0;
        for (int tile = 0; tile < _tiles.Length; tile++)
        {
            if (!_tiles[tile].IsLand) continue;
            int leader = 0, top = claims[0][tile], second = 0;
            for (int c = 1; c < _civs; c++)
            {
                int value = claims[c][tile];
                if (value > top) { second = top; top = value; leader = c; }
                else if (value > second) second = value;
            }
            if (top >= OwnThresholdMilli && (long)top * LeadDenominator >= (long)second * LeadNumerator)
            {
                owners[tile] = (sbyte)leader;
                ownedWeight += _tiles[tile].HabitableWeight;
            }
        }
        var frontier = MinimumFrontierConnection(owners, civA, civB);
        return (ownedWeight, frontier.Weight, frontier.Path, frontier.UnownedPath,
            PairOwnersTouching(owners, civA, civB));
    }

    /// <summary>
    /// Finds camp sources whose removal would change the projected owner of a pair-owned tile on a supplied path.
    /// The returned indices correspond to <paramref name="camps"/>; this is a read-only diagnostic.
    /// </summary>
    public bool[] ProjectedCampSupportsPairPath(IReadOnlyList<ClaimSource> settlements,
        IReadOnlyList<ClaimSource> camps, IReadOnlyList<int> path, int civA, int civB)
    {
        var supports = new bool[camps.Count];
        foreach (int tile in path)
        {
            if (tile < 0 || tile >= _tiles.Length) continue;
            var claims = new int[_civs];
            foreach (ClaimSource source in settlements) claims[source.Civ] += ClaimAt(source, tile);
            for (int i = 0; i < camps.Count; i++)
            {
                ClaimSource camp = camps[i];
                int owner = _owner[camp.Tile];
                if (owner != NoOwner && owner != camp.Civ) continue;
                claims[camp.Civ] += ClaimAt(camp, tile);
            }
            int projectedOwner = ResolveProjectedOwner(claims);
            if (projectedOwner != civA && projectedOwner != civB) continue;

            for (int i = 0; i < camps.Count; i++)
            {
                if (supports[i]) continue;
                ClaimSource camp = camps[i];
                if (camp.Civ != projectedOwner) continue;
                int liveOwner = _owner[camp.Tile];
                if (liveOwner != NoOwner && liveOwner != camp.Civ) continue;
                int contribution = ClaimAt(camp, tile);
                if (contribution == 0) continue;
                claims[camp.Civ] -= contribution;
                bool stillOwned = ResolveProjectedOwner(claims) == projectedOwner;
                claims[camp.Civ] += contribution;
                if (!stillOwned) supports[i] = true;
            }
        }
        return supports;
    }

    /// <summary>
    /// Exact local change in immediately projected owned habitable weight when one camp moves. Only tiles in the
    /// old/new camp disks can change owner, so this avoids a whole-map projection while screening diagnostic moves.
    /// A <paramref name="newCenter"/> of -1 removes the camp. This does not mutate territory.
    /// </summary>
    public int ProjectedCampMoveClaimedWeightDelta(IReadOnlyList<ClaimSource> settlements,
        IReadOnlyList<ClaimSource> camps, int campIndex, int newCenter)
    {
        if ((uint)campIndex >= (uint)camps.Count) throw new ArgumentOutOfRangeException(nameof(campIndex));
        if (newCenter < -1 || newCenter >= _tiles.Length) throw new ArgumentOutOfRangeException(nameof(newCenter));

        ClaimSource moved = camps[campIndex];
        var affected = new HashSet<int>();
        AddDisk(affected, moved.Tile, moved.Radius);
        if (newCenter >= 0) AddDisk(affected, newCenter, moved.Radius);

        int delta = 0;
        foreach (int tile in affected)
        {
            if (!_tiles[tile].IsLand) continue;
            var claims = new int[_civs];
            foreach (ClaimSource source in settlements) claims[source.Civ] += ClaimAt(source, tile);
            for (int i = 0; i < camps.Count; i++)
            {
                ClaimSource source = camps[i];
                if (i == campIndex) continue;
                int liveOwner = _owner[source.Tile];
                if (liveOwner != NoOwner && liveOwner != source.Civ) continue;
                claims[source.Civ] += ClaimAt(source, tile);
            }

            int oldContribution = IsActiveCamp(moved, moved.Tile) ? ClaimAt(moved, tile) : 0;
            claims[moved.Civ] += oldContribution;
            int beforeOwner = ResolveProjectedOwner(claims);
            claims[moved.Civ] -= oldContribution;

            int newContribution = newCenter >= 0 && IsActiveCamp(moved, newCenter)
                ? ClaimAt(moved with { Tile = newCenter }, tile)
                : 0;
            claims[moved.Civ] += newContribution;
            int afterOwner = ResolveProjectedOwner(claims);
            if (beforeOwner >= 0) delta -= _tiles[tile].HabitableWeight;
            if (afterOwner >= 0) delta += _tiles[tile].HabitableWeight;
        }
        return delta;

        void AddDisk(HashSet<int> into, int center, int radius)
        {
            int x = center % _width, y = center / _width;
            for (int ny = Math.Max(0, y - radius); ny <= Math.Min(_height - 1, y + radius); ny++)
            for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(_width - 1, x + radius + 1); nx++)
            {
                int tile = ny * _width + nx;
                if (HexGrid.Distance(center, tile, _width) <= radius) into.Add(tile);
            }
        }

        bool IsActiveCamp(ClaimSource camp, int center)
        {
            int liveOwner = _owner[center];
            return liveOwner == NoOwner || liveOwner == camp.Civ;
        }
    }

    /// <summary>
    /// Exact local change in projected owned habitable weight when two camps move together. This captures claim
    /// threshold interactions between overlapping disks without projecting unaffected map tiles.
    /// </summary>
    public int ProjectedCampMovesClaimedWeightDelta(IReadOnlyList<ClaimSource> settlements,
        IReadOnlyList<ClaimSource> camps, int firstIndex, int firstCenter, int secondIndex, int secondCenter)
    {
        if ((uint)firstIndex >= (uint)camps.Count) throw new ArgumentOutOfRangeException(nameof(firstIndex));
        if ((uint)secondIndex >= (uint)camps.Count) throw new ArgumentOutOfRangeException(nameof(secondIndex));
        if (firstIndex == secondIndex) throw new ArgumentException("The moved camp indices must be different.");
        ValidateCenter(firstCenter, nameof(firstCenter));
        ValidateCenter(secondCenter, nameof(secondCenter));

        ClaimSource first = camps[firstIndex], second = camps[secondIndex];
        var affected = new HashSet<int>();
        AddDisk(affected, first.Tile, first.Radius);
        AddDisk(affected, second.Tile, second.Radius);
        if (firstCenter >= 0) AddDisk(affected, firstCenter, first.Radius);
        if (secondCenter >= 0) AddDisk(affected, secondCenter, second.Radius);

        int delta = 0;
        foreach (int tile in affected)
        {
            if (!_tiles[tile].IsLand) continue;
            var claims = new int[_civs];
            foreach (ClaimSource source in settlements) claims[source.Civ] += ClaimAt(source, tile);
            for (int i = 0; i < camps.Count; i++)
            {
                if (i == firstIndex || i == secondIndex) continue;
                ClaimSource source = camps[i];
                if (!IsActiveCamp(source, source.Tile)) continue;
                claims[source.Civ] += ClaimAt(source, tile);
            }

            int firstOldClaim = IsActiveCamp(first, first.Tile) ? ClaimAt(first, tile) : 0;
            int secondOldClaim = IsActiveCamp(second, second.Tile) ? ClaimAt(second, tile) : 0;
            claims[first.Civ] += firstOldClaim;
            claims[second.Civ] += secondOldClaim;
            int beforeOwner = ResolveProjectedOwner(claims);
            claims[first.Civ] -= firstOldClaim;
            claims[second.Civ] -= secondOldClaim;

            int firstNewClaim = firstCenter >= 0 && IsActiveCamp(first, firstCenter)
                ? ClaimAt(first with { Tile = firstCenter }, tile)
                : 0;
            int secondNewClaim = secondCenter >= 0 && IsActiveCamp(second, secondCenter)
                ? ClaimAt(second with { Tile = secondCenter }, tile)
                : 0;
            claims[first.Civ] += firstNewClaim;
            claims[second.Civ] += secondNewClaim;
            int afterOwner = ResolveProjectedOwner(claims);
            if (beforeOwner >= 0) delta -= _tiles[tile].HabitableWeight;
            if (afterOwner >= 0) delta += _tiles[tile].HabitableWeight;
        }
        return delta;

        void ValidateCenter(int center, string parameterName)
        {
            if (center < -1 || center >= _tiles.Length) throw new ArgumentOutOfRangeException(parameterName);
        }

        bool IsActiveCamp(ClaimSource camp, int center)
        {
            int liveOwner = _owner[center];
            return liveOwner == NoOwner || liveOwner == camp.Civ;
        }

        void AddDisk(HashSet<int> into, int center, int radius)
        {
            int x = center % _width, y = center / _width;
            for (int ny = Math.Max(0, y - radius); ny <= Math.Min(_height - 1, y + radius); ny++)
            for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(_width - 1, x + radius + 1); nx++)
            {
                int tile = ny * _width + nx;
                if (HexGrid.Distance(center, tile, _width) <= radius) into.Add(tile);
            }
        }
    }

    private int ClaimAt(ClaimSource source, int tile)
    {
        int distance = HexGrid.Distance(source.Tile, tile, _width);
        if (distance > source.Radius) return 0;
        long emission = source.Emission.Raw * 1000 >> Fix64.FractionalBits;
        return (int)(emission * (source.Radius + 1 - distance) / (source.Radius + 1));
    }

    private int ResolveProjectedOwner(int[] claims)
    {
        int leader = 0, top = claims[0], second = 0;
        for (int civ = 1; civ < _civs; civ++)
        {
            int value = claims[civ];
            if (value > top) { second = top; top = value; leader = civ; }
            else if (value > second) second = value;
        }
        return top >= OwnThresholdMilli && (long)top * LeadDenominator >= (long)second * LeadNumerator
            ? leader
            : NoOwner;
    }

    private (int Weight, int[] Path, int[] UnownedPath) MinimumFrontierConnection(sbyte[] owners, int civA, int civB)
    {
        if (civA < 0 || civA >= _civs || civB < 0 || civB >= _civs)
            return (-1, Array.Empty<int>(), Array.Empty<int>());
        var cost = new int[_tiles.Length];
        Array.Fill(cost, int.MaxValue);
        var previous = new int[_tiles.Length];
        Array.Fill(previous, -1);
        var queue = new PriorityQueue<int, (int Cost, int Tile)>();
        for (int tile = 0; tile < owners.Length; tile++)
        {
            if (owners[tile] != civA || !_tiles[tile].IsLand) continue;
            cost[tile] = 0;
            queue.Enqueue(tile, (0, tile));
        }
        Span<int> neighbors = stackalloc int[6];
        while (queue.TryDequeue(out int tile, out (int Cost, int Tile) priority))
        {
            int currentCost = priority.Cost;
            if (currentCost != cost[tile]) continue;
            if (owners[tile] == civB)
            {
                var path = new List<int>();
                for (int current = tile; current >= 0; current = previous[current]) path.Add(current);
                path.Reverse();
                var unownedPath = new List<int>();
                foreach (int pathTile in path)
                    if (owners[pathTile] == NoOwner) unownedPath.Add(pathTile);
                return (currentCost, path.ToArray(), unownedPath.ToArray());
            }
            int count = HexGrid.Neighbors(tile, _width, _height, neighbors);
            for (int i = 0; i < count; i++)
            {
                int next = neighbors[i];
                if (!_tiles[next].IsLand) continue;
                int owner = owners[next];
                if (owner >= 0 && owner != civA && owner != civB) continue;
                int step = owner == NoOwner ? _tiles[next].HabitableWeight : 0;
                int nextCost = checked(currentCost + step);
                if (nextCost >= cost[next]) continue;
                cost[next] = nextCost;
                previous[next] = tile;
                queue.Enqueue(next, (nextCost, next));
            }
        }
        return (-1, Array.Empty<int>(), Array.Empty<int>());
    }

    private bool PairOwnersTouching(sbyte[] owners, int civA, int civB)
    {
        if (civA < 0 || civA >= _civs || civB < 0 || civB >= _civs) return false;
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < owners.Length; tile++)
        {
            if (owners[tile] != civA || !_tiles[tile].IsLand) continue;
            int count = HexGrid.Neighbors(tile, _width, _height, neighbors);
            for (int i = 0; i < count; i++)
                if (owners[neighbors[i]] == civB) return true;
        }
        return false;
    }

    private void Radiate(ClaimSource source)
        => Radiate(source, _claim);

    private void Radiate(ClaimSource source, int[][] claims)
    {
        int[] field = claims[source.Civ];
        int x = source.Tile % _width, y = source.Tile / _width;
        int r = source.Radius, denominator = r + 1;
        long emission = source.Emission.Raw * 1000 >> Fix64.FractionalBits;
        for (int ny = Math.Max(0, y - r); ny <= Math.Min(_height - 1, y + r); ny++)
        for (int nx = Math.Max(0, x - r - 1); nx <= Math.Min(_width - 1, x + r + 1); nx++)
        {
            int tile = ny * _width + nx;
            int d = HexGrid.Distance(source.Tile, tile, _width);
            if (d > r) continue;
            field[tile] += (int)(emission * (denominator - d) / denominator);
        }
    }

    private void ResolveOwnership()
    {
        Array.Clear(_ownedWeight);
        Array.Clear(_ownedTiles);
        for (int tile = 0; tile < _tiles.Length; tile++)
        {
            if (_tiles[tile].IsLand)
            {
                int leader = 0, top = _claim[0][tile], second = 0;
                for (int c = 1; c < _civs; c++)
                {
                    int v = _claim[c][tile];
                    if (v > top) { second = top; top = v; leader = c; }
                    else if (v > second) second = v;
                }
                int want = top >= OwnThresholdMilli && (long)top * LeadDenominator >= (long)second * LeadNumerator ? leader : NoOwner;
                if (want == _owner[tile])
                {
                    _pendingWho[tile] = -2;
                    _pendingSeasons[tile] = 0;
                }
                else
                {
                    _pendingSeasons[tile] = (byte)(_pendingWho[tile] == want ? Math.Min(255, _pendingSeasons[tile] + 1) : 1);
                    _pendingWho[tile] = (sbyte)want;
                    // The challenger must lead for a full season before the border moves.
                    if (_pendingSeasons[tile] > 1)
                    {
                        _owner[tile] = (sbyte)want;
                        _pendingWho[tile] = -2;
                        _pendingSeasons[tile] = 0;
                    }
                }
            }
            int o = _owner[tile];
            if (o >= 0)
            {
                _ownedWeight[o] += _tiles[tile].HabitableWeight;
                _ownedTiles[o]++;
            }
        }
    }

    /// <summary>
    /// Yearly Dominion Legacy: each civilization gains 100 × its owned habitable weight ÷ the habitable weight owned by
    /// anyone (docs/MVP/05 §11.3, 07b R8). Call once a year.
    /// </summary>
    public void AccrueDominion()
    {
        long owned = 0;
        foreach (int w in _ownedWeight) owned += w;
        if (owned == 0) return;
        for (int c = 0; c < _civs; c++)
            _dominion[c] += Fix64.FromRatio(100L * _ownedWeight[c], owned);
    }

    /// <summary>
    /// Habitable weight a new camp at <paramref name="center"/> would add: tiles within its disk that are unowned or
    /// the civilization's own and not yet covered (07b §9.4).
    /// </summary>
    public int CampGain(int center, int civ, bool[] covered)
    {
        int gain = 0;
        int x = center % _width, y = center / _width;
        for (int ny = Math.Max(0, y - CampRadius); ny <= Math.Min(_height - 1, y + CampRadius); ny++)
        for (int nx = Math.Max(0, x - CampRadius - 1); nx <= Math.Min(_width - 1, x + CampRadius + 1); nx++)
        {
            int tile = ny * _width + nx;
            if (!covered[tile] && (_owner[tile] == NoOwner || _owner[tile] == civ) &&
                HexGrid.Distance(center, tile, _width) <= CampRadius)
                gain += _tiles[tile].HabitableWeight;
        }
        return gain;
    }

    /// <summary>Whether a camp center satisfies the current settlement, landmass, ownership, and rival-clearance gates.</summary>
    public bool IsCampSiteLegal(int civ, int settlementTile, int tile, int radius, IReadOnlyList<ClaimSource> rivals)
    {
        if (settlementTile < 0 || settlementTile >= _tiles.Length || tile < 0 || tile >= _tiles.Length) return false;
        WorldTile candidate = _tiles[tile];
        if (candidate.HabitableWeight == 0 || candidate.Biome == Biome.Mountain ||
            candidate.Continent != _tiles[settlementTile].Continent) return false;
        if (_owner[tile] != NoOwner && _owner[tile] != civ) return false;
        int distance = HexGrid.Distance(settlementTile, tile, _width);
        if (distance < radius + 1 || distance > radius + 3) return false;
        foreach (ClaimSource rival in rivals)
            if (HexGrid.Distance(tile, rival.Tile, _width) < radius + 3) return false;
        return true;
    }

    public void MarkDisk(bool[] marked, int center, int radius)
    {
        int x = center % _width, y = center / _width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(_height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(_width - 1, x + radius + 1); nx++)
        {
            int tile = ny * _width + nx;
            if (HexGrid.Distance(center, tile, _width) <= radius) marked[tile] = true;
        }
    }

    /// <summary>
    /// Picks up to <paramref name="count"/> camp sites for a settlement (07b §9.4): habitable land on the same
    /// landmass, one to three tiles beyond its claim radius, not on rival land and clear of rival settlements, chosen
    /// greedily for new habitable weight. Ties go to the lowest tile index.
    /// </summary>
    public List<int> ChooseCampSites(int civ, int settlementTile, int radius, IReadOnlyList<ClaimSource> own,
        IReadOnlyList<ClaimSource> ownCamps, IReadOnlyList<ClaimSource> rivals, int count)
        => ChooseCampSites(civ, settlementTile, radius, own, ownCamps, rivals, count, compact: false);

    /// <summary>
    /// Compact alternative to <see cref="ChooseCampSites"/>: keeps the same site eligibility rules but chooses
    /// the smallest marginal habitable gain, then the nearest eligible rival settlement, then the lowest tile index.
    /// It can return zero-gain sites and is used for seasonal-camp placement.
    /// </summary>
    public List<int> ChooseCompactCampSites(int civ, int settlementTile, int radius, IReadOnlyList<ClaimSource> own,
        IReadOnlyList<ClaimSource> ownCamps, IReadOnlyList<ClaimSource> rivals, int count)
        => ChooseCampSites(civ, settlementTile, radius, own, ownCamps, rivals, count, compact: true);

    private List<int> ChooseCampSites(int civ, int settlementTile, int radius, IReadOnlyList<ClaimSource> own,
        IReadOnlyList<ClaimSource> ownCamps, IReadOnlyList<ClaimSource> rivals, int count, bool compact)
    {
        var sites = new List<int>();
        var selected = new HashSet<int>();
        var covered = new bool[_tiles.Length];
        foreach (ClaimSource s in own) MarkDisk(covered, s.Tile, s.Radius);
        foreach (ClaimSource c in ownCamps) MarkDisk(covered, c.Tile, CampRadius);
        int continent = _tiles[settlementTile].Continent;
        for (int placed = 0; placed < count; placed++)
        {
            int best = -1, bestGain = compact ? int.MaxValue : 0, bestRivalDistance = int.MaxValue;
            for (int tile = 0; tile < _tiles.Length; tile++)
            {
                if (selected.Contains(tile)) continue;
                if (!IsCampSiteLegal(civ, settlementTile, tile, radius, rivals)) continue;
                int gain = CampGain(tile, civ, covered);
                if (compact)
                {
                    int rivalDistance = NearestRivalDistance(tile, continent, rivals);
                    if (best < 0 || gain < bestGain ||
                        gain == bestGain && (rivalDistance < bestRivalDistance ||
                            rivalDistance == bestRivalDistance && tile < best))
                    {
                        best = tile;
                        bestGain = gain;
                        bestRivalDistance = rivalDistance;
                    }
                }
                else if (gain > bestGain) { best = tile; bestGain = gain; }
            }
            if (best < 0) break;
            sites.Add(best);
            selected.Add(best);
            MarkDisk(covered, best, CampRadius);
        }
        return sites;
    }

    private int NearestRivalDistance(int tile, int continent, IReadOnlyList<ClaimSource> rivals)
    {
        int nearest = int.MaxValue;
        foreach (ClaimSource rival in rivals)
        {
            if (_tiles[rival.Tile].Continent != continent) continue;
            nearest = Math.Min(nearest, HexGrid.Distance(tile, rival.Tile, _width));
        }
        return nearest;
    }

    public void Hash(ref StateHasher h)
    {
        h.Add(_seasons);
        for (int i = 0; i < _owner.Length; i++)
        {
            h.Add((int)_owner[i]);
            if (_pendingWho[i] != -2) { h.Add(i); h.Add((int)_pendingWho[i]); h.Add(_pendingSeasons[i]); }
        }
        foreach (Fix64 d in _dominion) h.Add(d);
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_seasons);
        w.Write(_owner.Length);
        for (int i = 0; i < _owner.Length; i++) { w.Write(_owner[i]); w.Write(_pendingWho[i]); w.Write(_pendingSeasons[i]); }
        foreach (Fix64 d in _dominion) w.Write(d.Raw);
    }

    public void Read(BinaryReader r)
    {
        _seasons = r.ReadInt64();
        if (r.ReadInt32() != _owner.Length) throw new InvalidDataException("Territory snapshot does not match this world.");
        for (int i = 0; i < _owner.Length; i++) { _owner[i] = r.ReadSByte(); _pendingWho[i] = r.ReadSByte(); _pendingSeasons[i] = r.ReadByte(); }
        for (int i = 0; i < _dominion.Length; i++) _dominion[i] = Fix64.FromRaw(r.ReadInt64());
        Array.Clear(_ownedWeight);
        Array.Clear(_ownedTiles);
        for (int i = 0; i < _owner.Length; i++)
            if (_owner[i] >= 0) { _ownedWeight[_owner[i]] += _tiles[i].HabitableWeight; _ownedTiles[_owner[i]]++; }
    }
}
