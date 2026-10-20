using System;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>
/// Tile geometry for the renderer, exactly as <see cref="HexGrid"/>: odd-r offset coordinates (odd rows shifted half a
/// tile east), pointy-top hexes, tile index = y × width + x, no wrapping (docs/MVP/05 §4.1).
/// <para><b>Map units.</b> Positions in frames are integers: one tile column is <see cref="Unit"/> units east, one row is
/// <see cref="Unit"/> units south. The centre of tile (x, y) is (x·U + U/2 + (y odd ? U/2 : 0), y·U + U/2). Rows of a
/// regular pointy-top hex grid are √3/2 of a column apart, so a renderer maps a position to world space as
/// <c>worldX = X / U × hexWidth</c>, <c>worldZ = Y / U × hexWidth × 0.8660254</c> (docs/MVP/18 §3).</para>
/// </summary>
public static class HexLayout
{
    /// <summary>Map units per tile column (and per row before the √3/2 row squash).</summary>
    public const int Unit = 1024;
    public const int HalfUnit = Unit / 2;

    public static int CenterX(int tile, int width)
    {
        int x = tile % width, y = tile / width;
        return x * Unit + HalfUnit + ((y & 1) != 0 ? HalfUnit : 0);
    }

    public static int CenterY(int tile, int width) => tile / width * Unit + HalfUnit;

    /// <summary>Odd-r offset (x, y) to axial (q, r).</summary>
    public static (int Q, int R) OffsetToAxial(int x, int y) => (x - (y - (y & 1)) / 2, y);

    /// <summary>Axial (q, r) to odd-r offset (x, y).</summary>
    public static (int X, int Y) AxialToOffset(int q, int r) => (q + (r - (r & 1)) / 2, r);

    /// <summary>Tile index of offset (x, y), or -1 off the map.</summary>
    public static int TileAt(int x, int y, int width, int height)
        => x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;

    /// <summary>Hex distance in tiles (as <see cref="HexGrid.Distance"/>).</summary>
    public static int Distance(int a, int b, int width) => HexGrid.Distance(a, b, width);

    /// <summary>
    /// The tile whose centre is nearest to a map-unit position, with the true hex metric (rows √3/2 apart), or -1 off
    /// the map. Pure integer arithmetic.
    /// </summary>
    public static int NearestTile(int px, int py, int width, int height)
    {
        int row = Math.Clamp(py / Unit, 0, height - 1);
        int best = -1;
        long bestDistance = long.MaxValue;
        for (int y = Math.Max(0, row - 1); y <= Math.Min(height - 1, row + 1); y++)
        {
            int shift = (y & 1) != 0 ? HalfUnit : 0;
            int column = Math.Clamp((px - shift) / Unit, 0, width - 1);
            for (int x = Math.Max(0, column - 1); x <= Math.Min(width - 1, column + 1); x++)
            {
                long dx = px - (x * Unit + HalfUnit + shift);
                long dy = py - (y * Unit + HalfUnit);
                long d = 4 * dx * dx + 3 * dy * dy;
                if (d < bestDistance) { bestDistance = d; best = y * width + x; }
            }
        }
        return best;
    }

    /// <summary>Neighbours of a tile (as <see cref="HexGrid.Neighbors"/>); returns the count written.</summary>
    public static int Neighbors(int tile, int width, int height, Span<int> destination)
        => HexGrid.Neighbors(tile, width, height, destination);
}
