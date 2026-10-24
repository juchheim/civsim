using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The economy fixture of docs/MVP/22 §16 B12 and B13 (the live counterpart of <c>metal_sim.py</c>): one settlement of a
/// given size on a generated map, its radius-3 range given 13 forest tiles of 30 L (22 §5.5), a copper-ore surface
/// deposit, a tin placer and a deep arsenical deposit in range, every E0–E2 project plus KEY-3 and the metal projects
/// done, fed from outside so its size holds. Fixture state only; never part of a match.
/// </summary>
internal sealed class BronzeFixture
{
    public const int Settlement = 0;
    public LivingEconomyModel Economy { get; }
    public int CopperTile { get; }
    public int TinTile { get; }
    public int ArsenicTile { get; }
    /// <summary>A surface copper deposit beyond the range (5–6 tiles out), for mining camps; -1 when the map has no land there.</summary>
    public int CampTile { get; }
    public int[] ForestTiles { get; }
    private readonly int _people;
    private long _tick;

    public long Tick => _tick;

    private BronzeFixture(LivingEconomyModel economy, int copper, int tin, int arsenic, int camp, int[] forest, int people)
    {
        Economy = economy; CopperTile = copper; TinTile = tin; ArsenicTile = arsenic; CampTile = camp; ForestTiles = forest; _people = people;
    }

    /// <param name="people">Settlement size (about 35% adults, 12% elders, the rest children: the live mix of 22 §5.6).</param>
    /// <param name="coppice">Whether WOD-04 Coppicing is known.</param>
    /// <param name="extraProjects">More projects to complete (e.g. MIN-05, WAR-05).</param>
    public static BronzeFixture Create(ContentPack pack, ulong seed, int people, bool coppice, bool tin = true, bool arsenic = true,
        params string[] extraProjects) => Create(pack, seed, people, coppice, tin, arsenic, shaftMining: true, extraProjects);

    public static BronzeFixture Create(ContentPack pack, ulong seed, int people, bool coppice, bool tin, bool arsenic, bool shaftMining,
        params string[] extraProjects)
    {
        GeneratedWorld generated = WorldGenerator.Generate(seed);
        WorldEconomyInput source = WorldEconomyInput.FromGeneratedWorld(generated);
        int start = source.StartingTile(0);
        var range = new List<int>();
        for (int t = 0; t < source.TileCount; t++)
        {
            int d = HexGrid.Distance(start, t, source.Width);
            if (d >= 1 && d <= 3 && source.Tile(t).BiomeId is not ((int)Biome.Ocean or (int)Biome.Coast)) range.Add(t);
        }
        range.Sort((a, b) =>
        {
            int da = HexGrid.Distance(start, a, source.Width), db = HexGrid.Distance(start, b, source.Width);
            return da != db ? db.CompareTo(da) : a.CompareTo(b);
        });
        if (range.Count < 16) throw new InvalidOperationException("The fixture start needs at least 16 land tiles in range.");
        int copper = range[0], tinTile = range[1], arsenicTile = range[2];
        var tiles = new WorldEconomyTile[source.TileCount];
        var deposits = new WorldDeposit[source.TileCount];
        for (int t = 0; t < tiles.Length; t++) { tiles[t] = source.Tile(t); deposits[t] = source.Deposit(t); }
        // The range's deposits are the fixture's own: clear the generated ones so only the three below count.
        foreach (int t in range) if (deposits[t].Kind is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin) deposits[t] = default;
        deposits[copper] = new WorldDeposit(DepositKind.CopperOre, 2, 1);
        if (tin) deposits[tinTile] = new WorldDeposit(DepositKind.Tin, 2, 1);
        if (arsenic) deposits[arsenicTile] = new WorldDeposit(DepositKind.ArsenicalCopper, 2, 2);
        int campTile = -1;
        for (int t = 0; t < source.TileCount && campTile < 0; t++)
        {
            int d = HexGrid.Distance(start, t, source.Width);
            if (d is 5 or 6 && source.Tile(t).BiomeId is not ((int)Biome.Ocean or (int)Biome.Coast or (int)Biome.Ice or (int)Biome.Peak))
                campTile = t;
        }
        if (campTile >= 0) deposits[campTile] = new WorldDeposit(DepositKind.CopperOre, 2, 1);
        var forest = new List<int>();
        for (int i = 3; i < range.Count && forest.Count < 13; i++) forest.Add(range[i]);
        foreach (int t in range)
            if (!forest.Contains(t) && tiles[t].InitialWood >= Fix64.FromInt(15)) tiles[t] = tiles[t] with { InitialWood = Fix64.FromInt(4) };
        foreach (int t in forest) tiles[t] = tiles[t] with { BiomeId = (int)Biome.TemperateForest, InitialWood = Fix64.FromInt(30), WoodGrowthPerYear = Fix64.FromInt(2) };
        var starts = new int[source.CivilizationCount];
        for (int c = 0; c < starts.Length; c++) starts[c] = source.StartingTile(c);
        var world = new WorldEconomyInput(source.Width, source.Height, tiles, starts, source.Herds, deposits);
        var content = EconomyContent.FromPack(pack);
        var economy = new LivingEconomyModel(seed, content, world) { FixtureHoldSettlements = true };

        var projects = new List<string>();
        foreach (EconomyProject p in content.Projects)
            if (p.Era <= 2 && (coppice || p.Id != "WOD-04")) projects.Add(p.Id);
        projects.AddRange(new[] { "KEY-3", "MET-03", "MET-04", "MET-05", "FIR-09" });
        if (shaftMining) projects.Add("MIN-04");
        projects.AddRange(extraProjects);
        economy.FixtureComplete(0, projects.ToArray());

        // Ages as the live E3 mix (22 §5.6): 35% adults of 14-44, 12% elders, the rest children of 0-13, the founding band included.
        int adults = people * 35 / 100, elders = people * 12 / 100;
        int children = Math.Max(0, people - adults - elders);
        int have = economy.Population(economy.SettlementLite(Settlement).Id);
        for (int i = 0; i < adults - 8; i++) economy.FixtureAddPeople(Settlement, 1, 14 + i * 31 / Math.Max(1, adults - 8));
        for (int i = 0; i < elders - 2; i++) economy.FixtureAddPeople(Settlement, 1, 45 + i * 15 / Math.Max(1, elders - 2));
        for (int i = 0; i < children - 2; i++) economy.FixtureAddPeople(Settlement, 1, i * 14 / Math.Max(1, children - 2));
        _ = have;
        // The forge (MET-03), furnaces (FIR-07, FIR-09) and mine heads (MIN-04) come before MET-05 in research, so the fixture
        // starts with them: the clock of T18 starts at the re-kit, not at the first forge.
        economy.FixtureBuild(Settlement, "forge");
        economy.FixtureBuild(Settlement, "kiln");
        economy.FixtureBuild(Settlement, "smelter");
        economy.FixtureBuild(Settlement, "mine_head", copper);
        economy.FixtureBuild(Settlement, "mine_head", tinTile);
        if (shaftMining) economy.FixtureBuild(Settlement, "mine_head", arsenicTile);
        economy.FixtureStock(Settlement, "G-STONE", Fix64.FromInt(20));
        economy.FixtureStock(Settlement, "G-CLAY", Fix64.FromInt(12));
        economy.FixtureStock(Settlement, "G-LEATHER", Fix64.FromInt(4));
        economy.FixtureStock(Settlement, "G-WOOD", Fix64.FromInt(20));
        return new BronzeFixture(economy, copper, tinTile, arsenicTile, campTile, forest.ToArray(), people);
    }

    /// <summary>Runs whole seasons, topping the stores up to a season of food before each so the settlement neither starves nor splits.</summary>
    public void RunSeasons(int seasons)
    {
        for (int s = 0; s < seasons; s++)
        {
            int pop = Economy.Population(Economy.SettlementLite(Settlement).Id);
            Fix64 food = Economy.FoodStores(Settlement);
            // A season and a fifth of food: enough not to starve, not so much that the planner frees food labor (stores ≥ 2× need).
            Fix64 target = Fix64.FromInt(pop) * Fix64.FromRatio(6, 5);
            if (food < target) Economy.FixtureStock(Settlement, "G-GRAIN", target - food);
            for (int t = 0; t < LivingEconomyModel.SeasonTicks; t++) Economy.Step(_tick++, ReadOnlySpan<Command>.Empty);
        }
        _ = _people;
    }

    /// <summary>Share of the settlement's adults (14+) carrying a bronze kit (rung 6).</summary>
    public Fix64 BronzeShare()
    {
        MetalReadout m = Economy.Metal(Settlement);
        return m.Adults == 0 ? Fix64.Zero : Fix64.FromInt(m.Kits9 + m.Kits10) / m.Adults;
    }

    /// <summary>Standing wood on the fixture's forest tiles.</summary>
    public Fix64 StandingWood()
    {
        Fix64 total = Fix64.Zero;
        foreach (int t in ForestTiles) total += Economy.StandingWood(t);
        return total;
    }
}
