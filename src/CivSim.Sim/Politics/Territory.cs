using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A place that emits claim: a settlement or a seasonal camp.</summary>
public readonly record struct ClaimSource(int Civ, int Tile, int Radius, Fix64 Emission);

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

    private void Radiate(ClaimSource source)
    {
        int[] field = _claim[source.Civ];
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
    {
        var sites = new List<int>();
        var covered = new bool[_tiles.Length];
        foreach (ClaimSource s in own) MarkDisk(covered, s.Tile, s.Radius);
        foreach (ClaimSource c in ownCamps) MarkDisk(covered, c.Tile, CampRadius);
        int continent = _tiles[settlementTile].Continent;
        for (int placed = 0; placed < count; placed++)
        {
            int best = -1, bestGain = 0;
            for (int tile = 0; tile < _tiles.Length; tile++)
            {
                WorldTile t = _tiles[tile];
                if (t.HabitableWeight == 0 || t.Biome == Biome.Mountain || t.Continent != continent) continue;
                if (_owner[tile] != NoOwner && _owner[tile] != civ) continue;
                int d = HexGrid.Distance(settlementTile, tile, _width);
                if (d < radius + 1 || d > radius + 3) continue;
                bool nearRival = false;
                foreach (ClaimSource rival in rivals)
                    if (HexGrid.Distance(tile, rival.Tile, _width) < radius + 3) { nearRival = true; break; }
                if (nearRival) continue;
                int gain = CampGain(tile, civ, covered);
                if (gain > bestGain) { best = tile; bestGain = gain; }
            }
            if (best < 0) break;
            sites.Add(best);
            MarkDisk(covered, best, CampRadius);
        }
        return sites;
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
