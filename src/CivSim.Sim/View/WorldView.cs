using System;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>
/// The map as the renderer draws it, built once from the generated world (docs/MVP/18 §2): terrain never changes in the
/// MVP, so it is not repeated in every frame. Plain arrays indexed by tile, safe to read from any thread once built.
/// </summary>
public sealed class WorldView
{
    private WorldView(int width, int height)
    {
        Width = width;
        Height = height;
        int n = width * height;
        Biome = new byte[n];
        Elevation = new byte[n];
        Climate = new byte[n];
        Flags = new ushort[n];
        Rainfall = new byte[n];
        Soil = new byte[n];
        Continent = new byte[n];
        Deposit = new byte[n];
        DepositAmount = new byte[n];
    }

    public int Width { get; }
    public int Height { get; }
    public int TileCount => Width * Height;
    public ulong Seed { get; private set; }

    /// <summary><see cref="World.Biome"/> per tile (Ocean, Coast, Ice, ..., Peak).</summary>
    public byte[] Biome { get; }
    /// <summary>Elevation 0 to 255 (sea level is where the generator put land, not a fixed value).</summary>
    public byte[] Elevation { get; }
    /// <summary><see cref="ClimateBand"/> per tile.</summary>
    public byte[] Climate { get; }
    /// <summary><see cref="TileFlags"/>: River, Lake, Spring, SalmonRun, WildGrain, CoastAdjacent.</summary>
    public ushort[] Flags { get; }
    public byte[] Rainfall { get; }
    public byte[] Soil { get; }
    public byte[] Continent { get; }
    /// <summary><see cref="DepositKind"/> per tile. The front end shows a deposit only once the viewer knows it (08 §5 Resources).</summary>
    public byte[] Deposit { get; }
    public byte[] DepositAmount { get; }
    /// <summary>Each civilization's starting tile, by civilization.</summary>
    public int[] StartTiles { get; private set; } = Array.Empty<int>();

    public bool IsLand(int tile) => (World.Biome)Biome[tile] is not (World.Biome.Ocean or World.Biome.Coast);
    public bool HasRiver(int tile) => (Flags[tile] & (ushort)TileFlags.River) != 0;
    public bool HasLake(int tile) => (Flags[tile] & (ushort)TileFlags.Lake) != 0;

    /// <summary>Copies the terrain of a generated world.</summary>
    public static WorldView FromGenerated(GeneratedWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var view = new WorldView(world.Width, world.Height) { Seed = world.Seed };
        ReadOnlySpan<WorldTile> tiles = world.Tiles;
        for (int t = 0; t < tiles.Length; t++)
        {
            WorldTile tile = tiles[t];
            view.Biome[t] = (byte)tile.Biome;
            view.Elevation[t] = tile.Elevation;
            view.Climate[t] = (byte)tile.Climate;
            view.Flags[t] = (ushort)tile.Flags;
            view.Rainfall[t] = tile.Rainfall;
            view.Soil[t] = tile.SoilFertility;
            view.Continent[t] = tile.Continent;
            view.Deposit[t] = (byte)tile.Deposit;
            view.DepositAmount[t] = tile.DepositAmount;
        }
        var starts = new int[world.Starts.Length];
        foreach (WorldStart start in world.Starts) starts[start.Civilization] = start.Tile;
        view.StartTiles = starts;
        return view;
    }
}
