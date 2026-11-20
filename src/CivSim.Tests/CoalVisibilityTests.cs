using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>
/// Docs/MVP/38 D13 (M13 WP-E6-3 follow-up): MIN-09 must never be locked out by a missing dark-seam sign. The generator keeps both guaranteed signs
/// (the oil-seep pass used to overwrite the dark seam in 28% of worlds), and MIN-08 Mine Surveying lets coal deposits within reach count as [Seen: Coal].
/// </summary>
public sealed class CoalVisibilityTests
{
    [Fact]
    public void Every_generated_world_keeps_both_guaranteed_surface_signs()
    {
        for (ulong seed = 1; seed <= 24; seed++)
        {
            GeneratedWorld world = WorldGenerator.Generate(seed);
            int seams = 0, seeps = 0;
            for (int i = 0; i < world.TileCount; i++)
            {
                SurfaceEvidence e = world.TileAt(i).SurfaceEvidence;
                if (e == SurfaceEvidence.DarkSeam) seams++;
                else if (e == SurfaceEvidence.OilSeep) seeps++;
            }
            Assert.True(seams > 0, $"seed {seed}: no dark seam, so [Seen: Coal] could never open");
            Assert.True(seeps > 0, $"seed {seed}: no oil seep");
        }
    }

    [Fact]
    public void MIN08_makes_coal_deposits_within_reach_count_as_Seen_Coal()
    {
        ResearchCatalog catalog = TestPack.Catalog;
        int coalBit = catalog.GateTargetIndex("Coal"), min08 = catalog.IndexOf("MIN-08");
        Assert.InRange(coalBit, 0, 63);
        const int era = 5, reach = 25; // E5 reach in the eras registry; MIN-08 is an E5 project
        for (ulong seed = 1; seed <= 3; seed++)
        {
            GeneratedWorld world = WorldGenerator.Generate(seed);
            MatchModel match = MatchModel.Create(seed, TestPack.Pack, world, default, default, rules: ChapterRules.Fief);
            var source = new LivingEconomySource(match.Economy, WorldEconomyInput.FromGeneratedWorld(world), world, catalog, ReadOnlySpan<EconomyArchetype>.Empty);
            for (int civ = 0; civ < 4; civ++)
            {
                int start = WorldEconomyInput.FromGeneratedWorld(world).StartingTile(civ);
                bool seam = false, coal = false;
                for (int i = 0; i < world.TileCount; i++)
                {
                    if (HexGrid.Distance(start, i, world.Width) > reach) continue;
                    WorldTile tile = world.TileAt(i);
                    seam |= tile.SurfaceEvidence == SurfaceEvidence.DarkSeam;
                    coal |= tile.Deposit == DepositKind.Coal;
                }
                bool without = (source.SeenMask(civ, new View(era, -1)) >> coalBit & 1) != 0;
                bool with = (source.SeenMask(civ, new View(era, min08)) >> coalBit & 1) != 0;
                Assert.Equal(seam, without);          // the surface sign alone, exactly as before
                Assert.Equal(seam || coal, with);     // surveying adds the deposits themselves
            }
        }
    }

    private sealed class View(int era, int done) : IResearchView
    {
        public int CivCount => 4;
        public int Era(int civ) => era;
        public bool IsDone(int civ, int project) => project == done;
        public ResearchCatalog Catalog => TestPack.Catalog;
    }
}
