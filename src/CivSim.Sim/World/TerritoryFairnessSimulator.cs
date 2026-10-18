using System;
using System.Collections.Generic;

namespace CivSim.Sim.World;

/// <summary>
/// Integer port of the 07b territory model through E7. It replays the tuned Standard Balanced
/// trajectory by season, placing fissions against last season's ownership and claims, then resolves
/// claims with the model's threshold, lead margin and one-season flip delay.
/// </summary>
public static class TerritoryFairnessSimulator
{
    private const int EndOfE7Season = 462;
    private const int CampsStartSeason = 52; // middle of E1, from 07b §9.4
    private const int CampRadius = 2;
    private const int CampEmitPermille = 500;
    private const int ClaimThreshold = 700; // T = 0.7, in permille
    private const int LeadNumerator = 6;    // top claim must be at least 1.2× the runner-up
    private const int LeadDenominator = 5;

    // Endpoints from balance/tools/territory_sim.py's live tuned Standard Balanced run.
    private static readonly int[] EraEnds = { 31, 71, 118, 170, 229, 295, 371, 462 };
    private static readonly int[] FissionSeasons = { 28, 180, 244, 267, 308, 330, 378, 397 };
    private static readonly int[] FissionTargets = { 2, 3, 4, 5, 7, 9, 12, 15 };
    private static readonly int[] ClaimRadii = { 3, 3, 4, 4, 5, 5, 5, 7 };
    private static readonly int[] ClaimMultiplierPermille = { 1000, 1150, 1300, 1300, 1300, 1300, 1300, 1560 };
    private static readonly int[] ReachByEra = { 6, 9, 12, 15, 20, 25, 35, 50 };
    private static readonly int[] GaussianFitPermille =
    {
        1000, 995, 980, 956, 923, 882, 835, 783, 726, 667,
        607, 546, 487, 430, 375, 325, 278, 236, 198, 164,
        135, 110, 89, 71, 56, 44, 34, 26, 20, 15, 11
    };

    private readonly record struct Proposal(int Civ, int Tile, int Score);

    public static TerritoryFairnessResult Evaluate(WorldTile[] tiles, int width, int height, int[] starts)
        => EvaluateCore(tiles, width, height, starts, stopWhenCandidateCannotPass: false);

    internal static TerritoryFairnessResult EvaluateCandidate(WorldTile[] tiles, int width, int height, int[] starts)
        => EvaluateCore(tiles, width, height, starts, stopWhenCandidateCannotPass: true);

    private static TerritoryFairnessResult EvaluateCore(WorldTile[] tiles, int width, int height, int[] starts,
        bool stopWhenCandidateCannotPass)
    {
        if (starts.Length != 4) throw new ArgumentException("The M2 fairness model expects four starts.", nameof(starts));

        int tileCount = tiles.Length;
        int[][] settlements = new int[4][];
        int[] counts = { 1, 1, 1, 1 };
        int[][] claims = new int[4][];
        int[][] camps = new int[4][];
        int[] campCounts = new int[4];
        bool[][] campPlacementAttempted = new bool[4][];
        int[][] snapshots = new int[4][];
        int[][] planned = new int[4][];
        int[] snapshotCounts = new int[4];
        int[] plannedCounts = new int[4];
        int[] blocked = new int[4];
        int[] owner = new int[tileCount];
        Array.Fill(owner, -1);
        int[] pendingWho = new int[tileCount];
        Array.Fill(pendingWho, -2);
        byte[] pendingSeasons = new byte[tileCount];
        int[] freeHabitable = new int[tileCount];
        for (int civ = 0; civ < 4; civ++)
        {
            settlements[civ] = new int[FissionTargets[^1]];
            settlements[civ][0] = starts[civ];
            claims[civ] = new int[tileCount];
            camps[civ] = new int[FissionTargets[^1] * 2];
            campPlacementAttempted[civ] = new bool[FissionTargets[^1]];
            snapshots[civ] = new int[FissionTargets[^1]];
            planned[civ] = new int[FissionTargets[^1]];
        }

        List<Proposal> proposals = new(12);
        Proposal[] accepted = new Proposal[12];
        int[][] distanceMapCache = new int[tileCount][];
        int[] waterDistance = WaterDistanceToLand(tiles, width, height);
        bool[][] walkable = BuildWalkability(tiles, waterDistance);
        int[][] walkDistances = new int[4][];
        bool[] walkDirty = { true, true, true, true };
        int activeSeaLevel = -1;
        int blockedSeasons = 0;
        int firstContactSeason = int.MaxValue;

        for (int season = 1; season <= EndOfE7Season; season++)
        {
            int era = EraAt(season);
            int radius = ClaimRadii[era];
            int target = TargetAt(season);
            int populationMilli = BalancedTerritoryTrajectory.PopulationMilli[season];

            for (int civ = 0; civ < 4; civ++)
            {
                while (counts[civ] > target) counts[civ]--;
                snapshotCounts[civ] = counts[civ];
                Array.Copy(settlements[civ], snapshots[civ], counts[civ]);
                plannedCounts[civ] = counts[civ];
                Array.Copy(settlements[civ], planned[civ], counts[civ]);
            }

            // All civilizations propose from the same previous-season claims and settlement snapshot.
            proposals.Clear();
            bool needsFreeHabitable = false;
            for (int civ = 0; civ < 4; civ++)
                needsFreeHabitable |= plannedCounts[civ] < target;
            if (needsFreeHabitable)
                BuildFreeHabitable(tiles, claims, owner, freeHabitable);
            for (int civ = 0; civ < 4; civ++)
            {
                int newSites = 0;
                while (plannedCounts[civ] < target && newSites < 3)
                {
                    (int site, int score) = PickSite(tiles, width, height, planned[civ], plannedCounts[civ],
                        snapshots, snapshotCounts, civ, starts[civ], owner, claims, freeHabitable,
                        distanceMapCache, era);
                    if (site < 0)
                    {
                        break;
                    }
                    planned[civ][plannedCounts[civ]++] = site;
                    proposals.Add(new Proposal(civ, site, score));
                    newSites++;
                }
            }

            proposals.Sort(static (a, b) =>
            {
                int byScore = b.Score.CompareTo(a.Score);
                return byScore != 0 ? byScore : a.Civ.CompareTo(b.Civ);
            });
            int acceptedCount = 0;
            for (int p = 0; p < proposals.Count; p++)
            {
                Proposal proposal = proposals[p];
                bool conflict = false;
                int exclusion = 2 * ClaimRadii[era] + 1;
                for (int i = 0; i < acceptedCount; i++)
                {
                    Proposal other = accepted[i];
                    if (other.Civ != proposal.Civ &&
                        HexGrid.Distance(proposal.Tile, other.Tile, width) < exclusion)
                    { conflict = true; break; }
                }
                if (conflict) continue;
                accepted[acceptedCount++] = proposal;
                settlements[proposal.Civ][counts[proposal.Civ]++] = proposal.Tile;
                walkDirty[proposal.Civ] = true;
            }

            // The acceptance gate measures seasons spent below the trajectory target, not
            // individual failed site proposals. A failed proposal only becomes a blocked
            // season if the civilization is still short after all same-season proposals resolve.
            for (int civ = 0; civ < 4; civ++)
                if (counts[civ] < target) blocked[civ]++;
            if (stopWhenCandidateCannotPass)
            {
                int maxBlocked = 0;
                for (int civ = 0; civ < 4; civ++) maxBlocked = Math.Max(maxBlocked, blocked[civ]);
                if (maxBlocked > 8) return IncompleteResult(maxBlocked, firstContactSeason, counts);
            }

            if (season >= CampsStartSeason && era <= 3)
                for (int civ = 0; civ < 4; civ++)
                for (int settlement = 0; settlement < counts[civ]; settlement++)
                {
                    if (campPlacementAttempted[civ][settlement]) continue;
                    campPlacementAttempted[civ][settlement] = true;
                    PlaceCamps(tiles, width, height, settlements, counts, owner,
                        camps, campCounts, civ, settlement, starts[civ], era);
                }

            RebuildClaims(tiles, width, height, settlements, counts, claims,
                camps, campCounts, owner, populationMilli, era);
            ResolveOwnership(tiles, claims, owner, pendingWho, pendingSeasons);

            if (firstContactSeason == int.MaxValue)
            {
                int seaLevel = SeaLevelAt(era);
                if (seaLevel != activeSeaLevel)
                {
                    activeSeaLevel = seaLevel;
                    for (int civ = 0; civ < 4; civ++) walkDirty[civ] = true;
                }
                for (int civ = 0; civ < 4; civ++)
                {
                    if (!walkDirty[civ]) continue;
                    walkDistances[civ] = WalkDistances(width, height, walkable[activeSeaLevel],
                        settlements[civ], counts[civ]);
                    walkDirty[civ] = false;
                }
                int contactRange = ReachByEra[era] * 2 + 2;
                for (int a = 0; a < 4 && firstContactSeason == int.MaxValue; a++)
                for (int b = a + 1; b < 4; b++)
                {
                    int distance = int.MaxValue;
                    for (int i = 0; i < counts[b]; i++)
                        distance = Math.Min(distance, walkDistances[a][settlements[b][i]]);
                    if (distance <= contactRange) { firstContactSeason = season; break; }
                }

                if (stopWhenCandidateCannotPass &&
                    ((firstContactSeason != int.MaxValue && !WorldAcceptance.ContactSeasonIsInStandardWindow(firstContactSeason)) ||
                     (season >= 100 && firstContactSeason == int.MaxValue)))
                    return IncompleteResult(blockedSeasons, firstContactSeason, counts);
            }
        }

        long[] owned = new long[4];
        long totalHabitable = 0;
        for (int tile = 0; tile < tileCount; tile++)
        {
            int habitable = tiles[tile].HabitableWeight;
            if (habitable == 0) continue;
            totalHabitable += habitable;
            if (owner[tile] >= 0 && owner[tile] < 4) owned[owner[tile]] += habitable;
        }
        int[] shares = new int[4];
        if (totalHabitable > 0)
            for (int civ = 0; civ < 4; civ++) shares[civ] = checked((int)(owned[civ] * 10_000 / totalHabitable));

        for (int civ = 0; civ < 4; civ++) blockedSeasons = Math.Max(blockedSeasons, blocked[civ]);
        return new TerritoryFairnessResult(shares, blockedSeasons, firstContactSeason, 1, (int[])counts.Clone());
    }

    private static TerritoryFairnessResult IncompleteResult(int blockedSeasons, int contactSeason, int[] counts) =>
        new(new int[4], blockedSeasons, contactSeason, 1, (int[])counts.Clone(), isComplete: false);

    private static int EraAt(int season)
    {
        for (int era = 0; era < EraEnds.Length; era++) if (season <= EraEnds[era]) return era;
        return EraEnds.Length - 1;
    }

    private static int TargetAt(int season)
    {
        int target = 1;
        for (int i = 0; i < FissionSeasons.Length; i++)
        {
            if (season < FissionSeasons[i]) break;
            target = FissionTargets[i];
        }
        return target;
    }

    private static int SeaLevelAt(int era) => era < 2 ? 0 : era < 4 ? 1 : 2;

    private static void BuildFreeHabitable(WorldTile[] tiles, int[][] claims, int[] owner, int[] free)
    {
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            int max = Math.Max(Math.Max(claims[0][tile], claims[1][tile]), Math.Max(claims[2][tile], claims[3][tile]));
            free[tile] = owner[tile] == -1 && max < ClaimThreshold ? tiles[tile].HabitableWeight : 0;
        }
    }

    private static (int Tile, int Score) PickSite(WorldTile[] tiles, int width, int height,
        int[] ownSites, int ownCount, int[][] rivalSites, int[] rivalCounts, int civ, int start,
        int[] owner, int[][] claims, int[] freeHabitable, int[][] distanceMapCache, int era)
    {
        int radius = ClaimRadii[era];
        int desired = 2 * radius + 1;
        int full = 3 * radius * (radius + 1) + 1;
        int startContinent = tiles[start].Continent;
        int[][] ownDistanceMaps = new int[ownCount][];
        for (int n = 0; n < ownCount; n++)
            ownDistanceMaps[n] = GetDistanceMap(ownSites[n], width, tiles.Length, distanceMapCache);
        List<int[]> rivalDistanceMaps = new(45);
        for (int other = 0; other < 4; other++)
            if (other != civ)
                for (int n = 0; n < rivalCounts[other]; n++)
                    rivalDistanceMaps.Add(GetDistanceMap(rivalSites[other][n], width, tiles.Length, distanceMapCache));
        int[] homeDistanceMap = GetDistanceMap(start, width, tiles.Length, distanceMapCache);
        int best = -1, bestScore = 0, bestHome = int.MaxValue, bestRival = int.MinValue, bestFacing = int.MaxValue;
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            if (tiles[tile].HabitableWeight == 0 || tiles[tile].Biome == Biome.Mountain) continue;
            int nearestOwn = int.MaxValue;
            for (int n = 0; n < ownCount; n++)
                nearestOwn = Math.Min(nearestOwn, ownDistanceMaps[n][tile]);
            if (nearestOwn < radius + 1 || nearestOwn > desired + 3 * radius) continue;

            int nearestRival = int.MaxValue;
            for (int n = 0; n < rivalDistanceMaps.Count; n++)
                nearestRival = Math.Min(nearestRival, rivalDistanceMaps[n][tile]);
            if (nearestRival < 2 * radius + 1 || (owner[tile] != -1 && owner[tile] != civ)) continue;
            if (era < 4 && tiles[tile].Continent != startContinent) continue;

            int freeWeight = 0;
            int x = tile % width, y = tile / width;
            for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
            for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(width - 1, x + radius + 1); nx++)
            {
                int nearby = ny * width + nx;
                if (HexGrid.Distance(tile, nearby, width) <= radius) freeWeight += freeHabitable[nearby];
            }
            int gain = freeWeight * 1000 / (full * 100);
            int infill = 150 * Math.Min(nearestOwn, desired) / desired;
            int offset = Math.Max(0, nearestOwn - desired);
            int fitIndex = Math.Clamp((offset * 10 + radius / 2) / radius, 0, GaussianFitPermille.Length - 1);
            int score = (gain + infill) * GaussianFitPermille[fitIndex] * tiles[tile].HabitableWeight / 100_000;
            if (score <= 50) continue; // reference cutoff: 0.05

            int home = homeDistanceMap[tile];
            int facing = FacingIndex(tile, start, width, height);
            if (score > bestScore || (score == bestScore &&
                (home < bestHome || (home == bestHome &&
                (nearestRival > bestRival || (nearestRival == bestRival && facing < bestFacing))))))
            {
                best = tile; bestScore = score; bestHome = home; bestRival = nearestRival; bestFacing = facing;
            }
        }
        return (best, bestScore);
    }

    private static int[] GetDistanceMap(int source, int width, int tileCount, int[][] cache)
    {
        int[]? existing = cache[source];
        if (existing is not null) return existing;
        int[] distances = new int[tileCount];
        for (int tile = 0; tile < tileCount; tile++) distances[tile] = HexGrid.Distance(tile, source, width);
        cache[source] = distances;
        return distances;
    }

    private static void RebuildClaims(WorldTile[] tiles, int width, int height, int[][] settlements,
        int[] counts, int[][] claims, int[][] camps, int[] campCounts, int[] owner,
        int populationMilli, int era)
    {
        for (int civ = 0; civ < 4; civ++) Array.Clear(claims[civ]);
        int radius = ClaimRadii[era];
        int denominator = radius + 1;
        for (int civ = 0; civ < 4; civ++)
        {
            ulong perSettlementMilli = (ulong)(populationMilli / Math.Max(1, counts[civ]));
            int emit = checked((int)(FixSqrt(perSettlementMilli * 1000) * (ulong)ClaimMultiplierPermille[era] / 1000));
            for (int s = 0; s < counts[civ]; s++)
            {
                int center = settlements[civ][s];
                int x = center % width, y = center / width;
                for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
                for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(width - 1, x + radius + 1); nx++)
                {
                    int tile = ny * width + nx;
                    int distance = HexGrid.Distance(center, tile, width);
                    if (distance > radius) continue;
                    claims[civ][tile] += emit * (denominator - distance) / denominator;
                }
            }
            int campEmit = emit * CampEmitPermille / 1000;
            for (int c = 0; c < campCounts[civ]; c++)
            {
                int center = camps[civ][c];
                if (owner[center] != -1 && owner[center] != civ) continue;
                int x = center % width, y = center / width;
                for (int ny = Math.Max(0, y - CampRadius); ny <= Math.Min(height - 1, y + CampRadius); ny++)
                for (int nx = Math.Max(0, x - CampRadius - 1); nx <= Math.Min(width - 1, x + CampRadius + 1); nx++)
                {
                    int tile = ny * width + nx;
                    int distance = HexGrid.Distance(center, tile, width);
                    if (distance > CampRadius) continue;
                    claims[civ][tile] += campEmit * (CampRadius + 1 - distance) / (CampRadius + 1);
                }
            }
        }
    }

    private static void PlaceCamps(WorldTile[] tiles, int width, int height,
        int[][] settlements, int[] counts, int[] owner, int[][] camps, int[] campCounts,
        int civ, int settlementIndex, int start, int era)
    {
        int tileCount = tiles.Length;
        int radius = ClaimRadii[era];
        int center = settlements[civ][settlementIndex];
        int upperRange = radius + 3;
        int[] rivalNearest = new int[tileCount];
        Array.Fill(rivalNearest, int.MaxValue);
        for (int other = 0; other < 4; other++)
        {
            if (other == civ) continue;
            for (int n = 0; n < counts[other]; n++)
            {
                int rival = settlements[other][n];
                for (int tile = 0; tile < tileCount; tile++)
                    rivalNearest[tile] = Math.Min(rivalNearest[tile], HexGrid.Distance(tile, rival, width));
            }
        }

        bool[] covered = new bool[tileCount];
        for (int n = 0; n < counts[civ]; n++) MarkDisk(covered, settlements[civ][n], radius, width, height);
        for (int n = 0; n < campCounts[civ]; n++) MarkDisk(covered, camps[civ][n], CampRadius, width, height);

        for (int placed = 0; placed < 2; placed++)
        {
            int best = -1, bestGain = 0, bestFacing = int.MaxValue;
            for (int tile = 0; tile < tileCount; tile++)
            {
                WorldTile candidate = tiles[tile];
                if (candidate.HabitableWeight == 0 || candidate.Biome == Biome.Mountain ||
                    candidate.Continent != tiles[center].Continent ||
                    (owner[tile] != -1 && owner[tile] != civ) || rivalNearest[tile] < radius + 3)
                    continue;
                int distance = HexGrid.Distance(center, tile, width);
                if (distance < radius + 1 || distance > upperRange) continue;

                int gain = HabitableGain(tiles, width, height, tile, covered, owner, civ, CampRadius);
                int facing = FacingIndex(tile, start, width, height);
                if (gain > bestGain || (gain == bestGain && gain > 0 && facing < bestFacing))
                {
                    best = tile;
                    bestGain = gain;
                    bestFacing = facing;
                }
            }
            if (best < 0) break;
            camps[civ][campCounts[civ]++] = best;
            MarkDisk(covered, best, CampRadius, width, height);
        }
    }

    private static int HabitableGain(WorldTile[] tiles, int width, int height, int center,
        bool[] covered, int[] owner, int civ, int radius)
    {
        int gain = 0;
        int x = center % width, y = center / width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(width - 1, x + radius + 1); nx++)
        {
            int tile = ny * width + nx;
            if (!covered[tile] && (owner[tile] == -1 || owner[tile] == civ) &&
                HexGrid.Distance(center, tile, width) <= radius)
                gain += tiles[tile].HabitableWeight;
        }
        return gain;
    }

    private static void MarkDisk(bool[] marked, int center, int radius, int width, int height)
    {
        int x = center % width, y = center / width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
        for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(width - 1, x + radius + 1); nx++)
        {
            int tile = ny * width + nx;
            if (HexGrid.Distance(center, tile, width) <= radius) marked[tile] = true;
        }
    }

    private static void ResolveOwnership(WorldTile[] tiles, int[][] claims, int[] owner,
        int[] pendingWho, byte[] pendingSeasons)
    {
        for (int tile = 0; tile < tiles.Length; tile++)
        {
            if (!tiles[tile].IsLand) continue;
            int leader = 0, top = claims[0][tile], second = 0;
            for (int civ = 1; civ < 4; civ++)
            {
                int value = claims[civ][tile];
                if (value > top) { second = top; top = value; leader = civ; }
                else if (value > second) second = value;
            }
            int want = top >= ClaimThreshold && (long)top * LeadDenominator >= (long)second * LeadNumerator ? leader : -1;
            if (want == owner[tile])
            {
                pendingWho[tile] = -2;
                pendingSeasons[tile] = 0;
                continue;
            }
            pendingSeasons[tile] = (byte)(pendingWho[tile] == want ? pendingSeasons[tile] + 1 : 1);
            pendingWho[tile] = want;
            if (pendingSeasons[tile] > 1)
            {
                owner[tile] = want;
                pendingWho[tile] = -2;
                pendingSeasons[tile] = 0;
            }
        }
    }

    private static int[] WaterDistanceToLand(WorldTile[] tiles, int width, int height)
    {
        int[] distance = new int[tiles.Length];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[tiles.Length];
        int head = 0, tail = 0;
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i].IsLand) { distance[i] = 0; queue[tail++] = i; }
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int current = queue[head++];
            if (distance[current] >= 2) continue;
            int count = HexGrid.Neighbors(current, width, height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (distance[next] <= distance[current] + 1) continue;
                distance[next] = distance[current] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    private static bool[][] BuildWalkability(WorldTile[] tiles, int[] waterDistance)
    {
        var passable = new bool[3][];
        for (int sea = 0; sea < 3; sea++)
        {
            passable[sea] = new bool[tiles.Length];
            for (int tile = 0; tile < tiles.Length; tile++)
                passable[sea][tile] = sea == 2 || tiles[tile].IsLand || waterDistance[tile] <= sea + 1;
        }
        return passable;
    }

    private static int[] WalkDistances(int width, int height, bool[] passable, int[] sources, int sourceCount)
    {
        int[] distance = new int[passable.Length];
        Array.Fill(distance, int.MaxValue);
        int[] queue = new int[passable.Length];
        int head = 0, tail = 0;
        for (int i = 0; i < sourceCount; i++)
        {
            int source = sources[i];
            if (!passable[source] || distance[source] == 0) continue;
            distance[source] = 0;
            queue[tail++] = source;
        }
        Span<int> neighbors = stackalloc int[6];
        while (head < tail)
        {
            int current = queue[head++];
            int count = HexGrid.Neighbors(current, width, height, neighbors);
            for (int n = 0; n < count; n++)
            {
                int next = neighbors[n];
                if (!passable[next] || distance[next] <= distance[current] + 1) continue;
                distance[next] = distance[current] + 1;
                queue[tail++] = next;
            }
        }
        return distance;
    }

    private static ulong FixSqrt(ulong n)
    {
        if (n == 0) return 0;
        ulong x = n;
        ulong y = (x + 1) / 2;
        while (y < x) { x = y; y = (x + n / x) / 2; }
        return x;
    }

    private static int FacingIndex(int tile, int start, int width, int height) =>
        start >= width * height / 2 ? width * height - 1 - tile : tile;
}

public sealed class TerritoryFairnessResult
{
    internal TerritoryFairnessResult(int[] shares, int blockedSeasons, int contactSeason, int attempts,
        int[] settlementCounts, bool isComplete = true)
    {
        E7SharesBasisPoints = shares;
        BlockedSeasons = blockedSeasons;
        FirstContactSeason = contactSeason;
        Attempts = attempts;
        SettlementCounts = settlementCounts;
        IsComplete = isComplete;
    }

    public ReadOnlyMemory<int> E7SharesBasisPoints { get; }
    public int BlockedSeasons { get; }
    public int FirstContactSeason { get; }
    public int Attempts { get; }
    public ReadOnlyMemory<int> SettlementCounts { get; }
    public bool IsComplete { get; }

    public bool PassesE7Tolerance(int tolerancePercent = 15)
    {
        ReadOnlySpan<int> shares = E7SharesBasisPoints.Span;
        long total = 0;
        foreach (int share in shares) total += share;
        int mean = shares.Length == 0 ? 0 : (int)(total / shares.Length);
        foreach (int share in shares)
            if ((long)Math.Abs(share - mean) * 100 > (long)mean * tolerancePercent) return false;
        return true;
    }
}
