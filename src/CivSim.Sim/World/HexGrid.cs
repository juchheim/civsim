using System;
using System.Collections.Generic;

namespace CivSim.Sim.World;

/// <summary>Odd-r offset hex geometry (05 §4.1), with axial distance and no map wrapping.</summary>
public static class HexGrid
{
    private static readonly (int Q, int R)[] Directions =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)
    };

    public static int Index(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            throw new ArgumentOutOfRangeException(nameof(x), "Hex coordinate is outside the map.");
        return checked(y * width + x);
    }

    public static void Coordinates(int index, int width, out int x, out int y)
    {
        if (width <= 0 || index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        x = index % width;
        y = index / width;
    }

    public static int Distance(int first, int second, int width)
    {
        int x1 = first % width, y1 = first / width;
        int x2 = second % width, y2 = second / width;
        int q1 = x1 - (y1 - (y1 & 1)) / 2;
        int q2 = x2 - (y2 - (y2 & 1)) / 2;
        int dq = q1 - q2, dr = y1 - y2;
        return Math.Max(Math.Max(Math.Abs(dq), Math.Abs(dr)), Math.Abs(dq + dr));
    }

    public static int Neighbors(int index, int width, int height, Span<int> destination)
    {
        int x = index % width, y = index / width;
        int q = x - (y - (y & 1)) / 2;
        int count = 0;
        foreach ((int dq, int dr) in Directions)
        {
            int nr = y + dr;
            int nq = q + dq;
            int nx = nq + (nr - (nr & 1)) / 2;
            if (nx >= 0 && nx < width && nr >= 0 && nr < height)
                destination[count++] = nr * width + nx;
        }
        return count;
    }

    /// <summary>
    /// Exact 180° axial rotation for even-sized odd-r maps. With even height, this is the index
    /// reversal also used by the reference territory model, but the transform is derived in axial
    /// space so callers do not mistake offset-coordinate reflection for hex rotation.
    /// </summary>
    public static int Rotate180(int index, int width, int height)
    {
        _ = Index(index % width, index / width, width, height);
        return checked(width * height - 1 - index);
    }

    public static int ReflectNorthSouth(int index, int width, int height)
    {
        _ = Index(index % width, index / width, width, height);
        int x = index % width, y = index / width;
        return (height - 1 - y) * width + x;
    }

    public static int ReflectEastWest(int index, int width, int height)
    {
        _ = Index(index % width, index / width, width, height);
        int x = index % width, y = index / width;
        return y * width + (width - 1 - x);
    }

    /// <summary>
    /// Land walking graph used by the M2 start search. Land is passable, as is a water tile
    /// between adjacent shores (the one-tile strait allowed by 12 §9.3).
    /// </summary>
    public static bool[] BuildWalkingPassability(WorldTile[] tiles, int width, int height)
    {
        bool[] passable = new bool[tiles.Length];
        Span<int> neighbors = stackalloc int[6];
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            if (tiles[tile].IsLand) { passable[tile] = true; continue; }
            int count = Neighbors(tile, width, height, neighbors);
            for (int n = 0; n < count; n++)
                if (tiles[neighbors[n]].IsLand) { passable[tile] = true; break; }
        }
        return passable;
    }

    /// <summary>Unit-cost shortest-path distances on a passability map; unreachable cells are Int32.MaxValue.</summary>
    public static int[] WalkingDistancesFrom(int width, int height, bool[] passable, int start)
    {
        int[] distance = new int[passable.Length];
        Array.Fill(distance, int.MaxValue);
        if (start < 0 || start >= passable.Length || !passable[start]) return distance;
        int[] queue = new int[passable.Length];
        int head = 0, tail = 0;
        distance[start] = 0;
        queue[tail++] = start;
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int tile = queue[head++];
            int count = Neighbors(tile, width, height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (!passable[next] || distance[next] != int.MaxValue) continue;
                distance[next] = distance[tile] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    /// <summary>Cost in tenths of a tile of one step over open water by outrigger (method multiplier 2.5 against 10 on foot; docs/MVP/21 §3).</summary>
    public const int SeaCostTenths = 4;

    /// <summary>
    /// The realm of every land tile: a continent (a landmass of more than <paramref name="islandMaxTiles"/> tiles) is its own realm
    /// and an island belongs to the realm of the nearest continent. 0 on water.
    /// </summary>
    public static int[] LandRealms(WorldTile[] tiles, int width, int height, int islandMaxTiles)
    {
        int n = tiles.Length;
        var landmass = new int[n];
        var sizes = new List<int> { 0 };
        var queue = new int[n];
        Span<int> neighbors = stackalloc int[6];
        for (int t = 0; t < n; t++)
        {
            if (!tiles[t].IsLand || landmass[t] != 0) continue;
            int id = sizes.Count, head = 0, tail = 0, size = 0;
            landmass[t] = id; queue[tail++] = t;
            while (head < tail)
            {
                int tile = queue[head++]; size++;
                int count = Neighbors(tile, width, height, neighbors);
                for (int k = 0; k < count; k++)
                    if (tiles[neighbors[k]].IsLand && landmass[neighbors[k]] == 0) { landmass[neighbors[k]] = id; queue[tail++] = neighbors[k]; }
            }
            sizes.Add(size);
        }
        // label every tile with the continent nearest to it (multi-source breadth-first, ties by tile order)
        var label = new int[n];
        var distance = new int[n];
        Array.Fill(distance, int.MaxValue);
        int h2 = 0, t2 = 0;
        for (int t = 0; t < n; t++)
            if (tiles[t].IsLand && sizes[landmass[t]] > islandMaxTiles) { label[t] = landmass[t]; distance[t] = 0; queue[t2++] = t; }
        while (h2 < t2)
        {
            int tile = queue[h2++];
            int count = Neighbors(tile, width, height, neighbors);
            for (int k = 0; k < count; k++)
            {
                int next = neighbors[k];
                if (distance[next] != int.MaxValue) continue;
                distance[next] = distance[tile] + 1; label[next] = label[tile]; queue[t2++] = next;
            }
        }
        var realm = new int[n];
        var islandRealm = new int[sizes.Count];
        for (int t = 0; t < n; t++)
        {
            if (!tiles[t].IsLand) continue;
            int id = landmass[t];
            if (sizes[id] > islandMaxTiles) realm[t] = id;
            else
            {
                if (islandRealm[id] == 0) islandRealm[id] = label[t];
                realm[t] = islandRealm[id];
            }
        }
        return realm;
    }

    /// <summary>Hex distance from every tile to the nearest land tile (0 on land).</summary>
    public static int[] LandDistances(WorldTile[] tiles, int width, int height)
    {
        int[] distance = new int[tiles.Length];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[tiles.Length];
        int head = 0, tail = 0;
        for (int t = 0; t < tiles.Length; t++)
            if (tiles[t].IsLand) { distance[t] = 0; queue[tail++] = t; }
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int tile = queue[head++];
            int count = Neighbors(tile, width, height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (distance[next] != int.MaxValue) continue;
                distance[next] = distance[tile] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    /// <summary>
    /// Dijkstra costs in tenths of a tile using the biome movement costs in 12 §4. A single
    /// shallow-water tile may connect shores, but a path cannot chain water tiles together. With
    /// <paramref name="landDistance"/> (see <see cref="LandDistances"/>) and <paramref name="seaReach"/> the walker also has a
    /// boat: any water tile within that many tiles of land is enterable at <see cref="SeaCostTenths"/>.
    /// </summary>
    public static int[] WalkingCostsFrom(WorldTile[] tiles, int width, int height, int start,
        int maximumCost = int.MaxValue, int[]? landDistance = null, int seaReach = 0, int[]? realm = null)
    {
        int[] distance = new int[tiles.Length];
        Array.Fill(distance, int.MaxValue);
        if (start < 0 || start >= tiles.Length || !tiles[start].IsLand || tiles[start].Biome == Biome.Peak)
            return distance;

        var queue = new PriorityQueue<int, int>();
        distance[start] = 0;
        queue.Enqueue(start, 0);
        Span<int> neighbors = stackalloc int[6];
        while (queue.TryDequeue(out int tile, out int priority))
        {
            if (priority > maximumCost) break;
            if (priority != distance[tile]) continue;
            int count = Neighbors(tile, width, height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                WorldTile nextTile = tiles[next];
                int moveCost;
                if (nextTile.IsLand)
                {
                    if (nextTile.Biome == Biome.Peak) continue;
                    // a boat may land only in the traveller's own realm: its continent and that continent's islands (docs/MVP/21 §3)
                    if (realm is not null && !tiles[tile].IsLand && realm[next] != realm[start]) continue;
                    moveCost = nextTile.Biome switch
                    {
                        Biome.TemperateForest or Biome.Taiga => 15,
                        Biome.OpenWoodland => 12,
                        Biome.GrasslandSteppe or Biome.Savanna => 10,
                        Biome.TropicalForest => 16,
                        Biome.Wetland => 20,
                        Biome.Tundra => 13,
                        Biome.Desert => 15,
                        Biome.Mountain => 40,
                        Biome.Ice => 30,
                        _ => 10
                    };
                }
                else if (landDistance is not null && landDistance[next] <= seaReach)
                {
                    // Open water by boat (docs/MVP/21): enterable from anywhere while the tile is within reach of land.
                    moveCost = SeaCostTenths;
                }
                else
                {
                    // Water is only an edge/strait step between two land tiles.
                    if (!tiles[tile].IsLand || !HasLandNeighbor(tiles, next, width, height)) continue;
                    moveCost = 10;
                }

                int candidate = checked(distance[tile] + moveCost);
                if (candidate > maximumCost) continue;
                if (candidate >= distance[next]) continue;
                distance[next] = candidate;
                queue.Enqueue(next, candidate);
            }
        }
        return distance;
    }

    private static bool HasLandNeighbor(WorldTile[] tiles, int tile, int width, int height)
    {
        Span<int> neighbors = stackalloc int[6];
        int count = Neighbors(tile, width, height, neighbors);
        for (int n = 0; n < count; n++)
            if (tiles[neighbors[n]].IsLand) return true;
        return false;
    }

    public static int CanonicalWestNorth(int index, int width, int height)
    {
        int x = index % width, y = index / width;
        if (x >= width / 2) x = width - 1 - x;
        if (y >= height / 2) y = height - 1 - y;
        return y * width + x;
    }
}
