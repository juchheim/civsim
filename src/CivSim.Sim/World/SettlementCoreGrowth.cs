using System;
using CivSim.Sim.Economy;

namespace CivSim.Sim.World;

/// <summary>Stable ids for the districts that reserve ring-two settlement hexes (docs/MVP/31b §1.2).</summary>
public enum CoreDistrict : byte
{
    Ziggurat,
    GreatDome,
    GreatLibrary,
    Citadel,
    Learning,
    Civic,
    Health,
    HomesC,
    HomesD,
    Count,
}

/// <summary>Pure geometry and deterministic assignment for settlement outer hexes (docs/MVP/31b §1.3–§1.6).</summary>
public static class SettlementCoreGrowth
{
    public const int OuterPositions = 12;
    private const int DistrictCount = (int)CoreDistrict.Count;
    private const ushort PositionMask = (1 << OuterPositions) - 1;
    private const ushort DistrictMask = (1 << DistrictCount) - 1;

    private static readonly byte[] PriorityData =
    {
        (byte)CoreDistrict.Ziggurat, (byte)CoreDistrict.GreatDome, (byte)CoreDistrict.GreatLibrary,
        (byte)CoreDistrict.Citadel, (byte)CoreDistrict.Learning, (byte)CoreDistrict.Civic,
        (byte)CoreDistrict.Health, (byte)CoreDistrict.HomesC, (byte)CoreDistrict.HomesD,
    };

    private static readonly byte[] HomeData = { 6, 7, 5, 8, 4, 3, 2, 10, 11 };
    private static readonly byte[] PreferenceData = BuildPreferences();

    public static ReadOnlySpan<byte> Priority => PriorityData;
    public static ReadOnlySpan<byte> Home => HomeData;

    /// <summary>The static gate/front direction for <paramref name="site"/> (docs/MVP/31b §1.3).</summary>
    public static int Front(WorldEconomyInput world, int site)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (site < 0 || site >= world.TileCount) throw new ArgumentOutOfRangeException(nameof(site));

        int first = (site % 997) * 6 / 997;
        for (int i = 0; i < 6; i++)
        {
            int direction = (first + i) % 6;
            int tile = HexGrid.Step(site, direction, world.Width, world.Height);
            if (tile < 0) continue;
            Biome biome = (Biome)world.Tile(tile).BiomeId;
            if (biome is Biome.Ocean or Biome.Coast) return direction;
        }

        for (int direction = 0; direction < 6; direction++)
        {
            int tile = HexGrid.Step(site, direction, world.Width, world.Height);
            if (tile < 0) continue;
            WorldEconomyTile neighbor = world.Tile(tile);
            Biome biome = (Biome)neighbor.BiomeId;
            if (biome is not (Biome.Ocean or Biome.Coast) && neighbor.River) return direction;
        }

        return site % 6;
    }

    /// <summary>Gets one ring-two tile by its angular position; returns -1 when the position leaves the map.</summary>
    public static int PositionTile(int site, int front, int position, int width, int height)
    {
        if ((uint)front >= 6u) throw new ArgumentOutOfRangeException(nameof(front));
        if ((uint)position >= OuterPositions) throw new ArgumentOutOfRangeException(nameof(position));
        if (site < 0 || (long)site >= (long)width * height) return -1;

        int cornerOrEdge = position / 2;
        int firstDirection = (front + cornerOrEdge) % 6;
        int first = HexGrid.Step(site, firstDirection, width, height);
        if (first < 0) return -1;
        if ((position & 1) == 0) return HexGrid.Step(first, firstDirection, width, height);
        return HexGrid.Step(first, (firstDirection + 1) % 6, width, height);
    }

    /// <summary>Whether the map facts that do not change during a match allow a building on this tile (§1.5).</summary>
    public static bool IsUsableStatic(WorldEconomyInput world, int tile)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (tile < 0 || tile >= world.TileCount) return false;
        WorldEconomyTile value = world.Tile(tile);
        Biome biome = (Biome)value.BiomeId;
        if (biome is Biome.Ocean or Biome.Coast or Biome.Mountain or Biome.Peak or Biome.Ice || value.Lake || value.River)
            return false;
        return !SettlementCoreRules.IsValuableCoreDeposit(world.Deposit(tile).Kind);
    }

    /// <summary>
    /// Assigns present districts to usable positions. A usable home is reserved in the first pass so it cannot move when
    /// another district appears. The returned bits are the positions claimed; <paramref name="positionOf"/> is indexed by district.
    /// </summary>
    public static ushort Assign(ushort usableMask, ushort presentMask, Span<sbyte> positionOf)
    {
        if (positionOf.Length < DistrictCount) throw new ArgumentException("The district output is too small.", nameof(positionOf));
        positionOf[..DistrictCount].Fill(-1);
        usableMask &= PositionMask;
        presentMask &= DistrictMask;
        ushort claimed = 0;

        foreach (byte district in PriorityData)
        {
            ushort districtBit = (ushort)(1 << district);
            int home = HomeData[district];
            ushort homeBit = (ushort)(1 << home);
            if ((presentMask & districtBit) == 0 || (usableMask & homeBit) == 0) continue;
            positionOf[district] = (sbyte)home;
            claimed |= homeBit;
        }

        foreach (byte district in PriorityData)
        {
            if ((presentMask & (1 << district)) == 0 || positionOf[district] >= 0) continue;
            int offset = district * OuterPositions;
            for (int i = 0; i < OuterPositions; i++)
            {
                int position = PreferenceData[offset + i];
                ushort bit = (ushort)(1 << position);
                if ((usableMask & bit) == 0 || (claimed & bit) != 0) continue;
                positionOf[district] = (sbyte)position;
                claimed |= bit;
                break;
            }
        }

        return claimed;
    }

    private static byte[] BuildPreferences()
    {
        var result = new byte[DistrictCount * OuterPositions];
        for (int district = 0; district < DistrictCount; district++)
        {
            int home = HomeData[district];
            int offset = district * OuterPositions, count = 0;
            for (int distance = 0; distance <= 6; distance++)
            {
                int clockwise = (home + distance) % OuterPositions;
                if (clockwise != 0) result[offset + count++] = (byte)clockwise;
                if (distance is > 0 and < 6)
                {
                    int counterClockwise = (home - distance + OuterPositions) % OuterPositions;
                    if (counterClockwise != 0) result[offset + count++] = (byte)counterClockwise;
                }
            }
            result[offset + count++] = 0; // Straight out of the gate is the last fallback for every district.
            if (count != OuterPositions) throw new InvalidOperationException("A district preference list is incomplete.");
        }
        return result;
    }
}
