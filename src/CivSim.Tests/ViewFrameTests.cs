using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>What a frame shows (docs/MVP/18 §3 to §4): the truth in omniscient mode, and only what the viewer's people see otherwise.</summary>
public class ViewFrameTests
{
    [Fact]
    public void Bronze_frame_carries_live_forest_mining_and_cohesion_data_without_revealing_unseen_tiles()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        for (int civ = 0; civ < 4; civ++) match.Politics.SetDecreePolicy(civ, null);
        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, TestPack.Catalog.IndexOf("KEY-3"), 0, 0));
        runner.Advance(1);

        LivingEconomyModel eco = match.Economy;
        GeneratedWorld world = ViewKit.World.Value;
        int home = Enumerable.Range(0, eco.SettlementCount)
            .First(s => eco.SettlementLite(s).Civilization == 0 && !match.Politics.IsFreeFolk(s));
        int homeTile = eco.SettlementLite(home).SiteTile;
        eco.FixtureAddPeople(home, 80, 24);
        int forestTile = Enumerable.Range(0, world.TileCount).First(tile =>
            HexGrid.Distance(homeTile, tile, world.Width) <= 8 &&
            world.TileAt(tile).Biome is Biome.TemperateForest or Biome.TropicalForest or Biome.OpenWoodland);
        eco.FixtureSetWood(forestTile, Fix64.FromInt(20));

        int campTile = Enumerable.Range(0, world.TileCount).First(tile =>
            world.TileAt(tile).Deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin);
        eco.FixtureAddMiningCamp(home, campTile, crew: 4, built: true, mineHead: true);
        eco.FixtureExhaust(campTile); // A worked marker means ore has actually been removed from this deposit.

        int hiddenTile = Enumerable.Range(0, world.TileCount).First(tile =>
            tile != campTile && HexGrid.Distance(homeTile, tile, world.Width) > 20 && HexGrid.Distance(campTile, tile, world.Width) > 10);
        eco.FixtureSetWood(hiddenTile, Fix64.FromInt(30));
        eco.FixtureExhaust(hiddenTile);

        var frame = new MatchFrame();
        new FrameBuilder(match, 0).Build(match, frame);

        Assert.Equal((byte)2, frame.ForestBand[forestTile]);
        Assert.Equal((byte)1, frame.DepositWorked[campTile]);
        Assert.Equal((byte)1, frame.MiningReach[homeTile]);
        Assert.Equal((byte)0, frame.ForestBand[hiddenTile]);
        Assert.Equal((byte)0, frame.DepositWorked[hiddenTile]);
        Assert.Equal((byte)0, frame.MiningReach[hiddenTile]);
        Assert.Equal((byte)TileVisibility.Unknown, frame.Visibility[hiddenTile]);

        int camp = Enumerable.Range(0, frame.CampCount).First(i => frame.CampTile[i] == campTile);
        Assert.Equal((byte)CampKind.Mining, frame.CampKind[camp]);
        Assert.Equal(home, frame.CampParent[camp]);
        Assert.Equal((byte)world.TileAt(campTile).Deposit, frame.CampDeposit[camp]);
        Assert.Equal(4, frame.CampCrew[camp]);
        Assert.Equal((byte)1, frame.CampBuilt[camp]);
        Assert.Equal((byte)1, frame.CampMineHead[camp]);

        int homeFrame = frame.IndexOfSettlement(home);
        (int overreach, int spanPenalty) = eco.CohesionPressures(home);
        Assert.Equal(overreach, frame.SettlementOverreach[homeFrame]);
        Assert.Equal(spanPenalty, frame.SettlementSpanPenalty[homeFrame]);
    }

    [Fact]
    public void An_omniscient_frame_matches_the_true_state()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        MatchModel match = w.Match;
        LivingEconomyModel eco = match.Economy;
        PoliticsSystem pol = match.Politics;
        var f = new MatchFrame();
        new FrameBuilder(match, -1).Build(match, f);

        Assert.Equal(eco.Tick, f.Tick);
        Assert.Equal((int)eco.SeasonsCompleted % 4, f.SeasonOfYear);
        Assert.Equal(eco.FigureCount, f.FigureCount);
        Assert.Equal(eco.FigureCount, f.TotalFigures);
        int width = f.Width, height = f.Height;
        for (int i = 0; i < eco.FigureCount; i++)
        {
            FigureLook truth = eco.LookFigure(i);
            Assert.Equal(truth.Id, f.FigureId[i]);
            Assert.Equal(truth.Civilization, f.FigureCiv[i]);
            Assert.Equal(truth.SettlementId, f.FigureSettlement[i]);
            Assert.Equal(truth.Age < 14 ? AgeBand.Child : truth.Age < 45 ? AgeBand.Adult : AgeBand.Elder, (AgeBand)f.FigureAge[i]);
            Assert.Equal(truth.Female, ((FigureFlags)f.FigureFlags[i] & FigureFlags.Female) != 0);
            Assert.Equal(truth.WoundSeasons > 0, ((FigureFlags)f.FigureFlags[i] & FigureFlags.Wounded) != 0);
            Assert.True(f.FigureKnownToolRung[i] >= f.FigureToolRung[i]);
            Assert.InRange(f.FigureMethodEfficiencyPermille[i], (short)0, (short)1000);
            if (f.FigureMethodFallback[i] != (byte)MethodFallbackReason.None)
                Assert.True(f.FigureKnownToolRung[i] > f.FigureToolRung[i]);
            Assert.Equal(f.FigureId[i], f.FigureId[f.IndexOfFigure(truth.Id)]);
            // A figure stands on (or, while walking or with a party, near) its tile.
            Assert.InRange(f.FigureX[i], 0, width * HexLayout.Unit + HexLayout.Unit);
            Assert.InRange(f.FigureY[i], 0, height * HexLayout.Unit);
            var flags = (FigureFlags)f.FigureFlags[i];
            if ((flags & (FigureFlags.Walking | FigureFlags.Away)) == 0)
                Assert.Equal(f.FigureTile[i], HexLayout.NearestTile(f.FigureX[i], f.FigureY[i], width, height));
            if ((FigureActivity)f.FigureActivity[i] <= FigureActivity.Herd && truth.Age >= 14 && truth.WoundSeasons == 0 && truth.IllnessSeasons == 0 &&
                (flags & FigureFlags.Away) == 0)
                Assert.Equal((int)truth.Job, f.FigureActivity[i]);
        }

        Assert.Equal(eco.SettlementCount, f.SettlementCount);
        for (int s = 0; s < eco.SettlementCount; s++)
        {
            var lite = eco.SettlementLite(s);
            Assert.Equal(s, f.SettlementIndex[s]);
            Assert.Equal(lite.Civilization, f.SettlementCiv[s]);
            Assert.Equal(lite.SiteTile, f.SettlementTile[s]);
            Assert.Equal(eco.Population(lite.Id), f.SettlementPopulation[s]);
            Assert.Equal(pol.IsFreeFolk(s), (f.SettlementFlags[s] & (ushort)SettlementViewFlags.FreeFolk) != 0);
            for (int k = 0; k < MatchFrame.StructureKinds; k++)
                Assert.Equal(Math.Min(255, eco.StructureCount(s, k)), f.SettlementStructures[s * MatchFrame.StructureKinds + k]);
            Assert.Equal(Math.Min(255, eco.HallCount(s)), f.SettlementHalls[s]);
            (int ziggurats, int zigguratPermille) = eco.ZigguratView(s);
            Assert.Equal(Math.Min(255, ziggurats), f.SettlementZiggurats[s]);
            Assert.Equal(zigguratPermille, f.SettlementZigguratPermille[s]);
        }

        for (int t = 0; t < f.Visibility.Length; t++)
        {
            Assert.Equal((byte)TileVisibility.Visible, f.Visibility[t]);
            Assert.Equal(pol.Territory.OwnerOf(t), f.Owner[t]);
        }
        int herds = 0;
        for (int h = 0; h < eco.WildHerdCount; h++) if (eco.WildHerd(h).AnimalCount.FloorToInt() > 0) herds++;
        Assert.Equal(herds, f.HerdCount);
        for (int i = 0; i < f.HerdCount; i++)
            Assert.Equal(f.HerdTile[i], HexLayout.NearestTile(f.HerdX[i], f.HerdY[i], width, height));
        Assert.Equal(pol.Expeditions.Count, f.PartyCount);
        Assert.Equal(pol.Occupations.Count, f.OccupationCount);
        Assert.Equal(match.Research.Era(0), f.CivEra[0]);
        Assert.Equal(match.Research.DoneCount(2), f.CivProjectsDone[2]);
        Assert.True(f.FigureCount > 200, $"a 30-minute match has a few hundred people, got {f.FigureCount}");
        Assert.Equal(match.Research.Catalog.Count, f.FocusProjectCount);
        for (int p = 0; p < f.FocusProjectCount; p++) Assert.Equal((byte)match.Research.State(0, p), f.FocusProjectState[p]);
        for (int slot = 0; slot < 3; slot++) Assert.Equal(match.Research.Slot(0, slot), f.FocusSlotProject[slot]);
    }

    [Fact]
    public void Two_builders_lay_figures_out_identically_and_rebuilding_changes_nothing()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        var a = new MatchFrame();
        var b = new MatchFrame();
        var builder = new FrameBuilder(w.Match, 0);
        var secondBuilder = new FrameBuilder(w.Match, 0);
        ViewKit.CopyState(w.Viewer, builder);
        ViewKit.CopyState(w.Viewer, secondBuilder);
        builder.Build(w.Match, a);
        secondBuilder.Build(w.Match, b);
        Assert.Equal(ViewKit.Hash(a), ViewKit.Hash(b));
        // The watched builder has memory (explored tiles, remembered settlements), so its figures and current data agree too.
        Assert.Equal(a.FigureCount, w.ViewerFrame.FigureCount);
        Assert.True(a.FigureId.AsSpan(0, a.FigureCount).SequenceEqual(w.ViewerFrame.FigureId.AsSpan(0, a.FigureCount)));
        Assert.True(a.FigureX.AsSpan(0, a.FigureCount).SequenceEqual(w.ViewerFrame.FigureX.AsSpan(0, a.FigureCount)));
    }

    [Fact]
    public void A_viewer_sees_its_own_people_and_only_what_they_see_of_others()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        Assert.Empty(w.Violations);
        MatchFrame f = w.ViewerFrame, all = w.OmniFrame;
        Assert.Equal(all.Tick, f.Tick);
        int own = 0, hiddenRivals = 0;
        for (int i = 0; i < all.FigureCount; i++)
        {
            bool free = (all.FigureFlags[i] & (ushort)FigureFlags.FreeFolk) != 0;
            bool mine = all.FigureCiv[i] == 0 && !free;
            int index = f.IndexOfFigure(all.FigureId[i]);
            if (mine) { own++; Assert.True(index >= 0, "an own figure is missing"); continue; }
            bool visible = f.Visibility[all.FigureTile[i]] == (byte)TileVisibility.Visible;
            Assert.Equal(visible, index >= 0);
            if (!visible) hiddenRivals++;
        }
        Assert.True(own > 0);
        Assert.True(hiddenRivals > 0, "rival figures out of sight must be absent");
        for (int i = 0; i < f.FigureCount; i++)
            if (f.FigureCiv[i] != 0) Assert.Equal((byte)TileVisibility.Visible, f.Visibility[f.FigureTile[i]]);
        for (int i = 0; i < f.HerdCount; i++) Assert.Equal((byte)TileVisibility.Visible, f.Visibility[f.HerdTile[i]]);
        for (int i = 0; i < f.PredatorCount; i++) Assert.Equal((byte)TileVisibility.Visible, f.Visibility[f.PredatorTile[i]]);
        Assert.True(f.HerdCount < all.HerdCount);

        // Rival settlements: current only in sight; otherwise remembered with no details, or absent.
        for (int i = 0; i < all.SettlementCount; i++)
        {
            int s = all.SettlementIndex[i];
            int index = f.IndexOfSettlement(s);
            bool visible = f.Visibility[all.SettlementTile[i]] == (byte)TileVisibility.Visible;
            bool mine = all.SettlementCiv[i] == 0 && (all.SettlementFlags[i] & (ushort)SettlementViewFlags.FreeFolk) == 0;
            if (mine || visible) { Assert.True(index >= 0); Assert.Equal(0, f.SettlementFlags[index] & (ushort)SettlementViewFlags.Remembered); continue; }
            if (index < 0) continue;
            Assert.NotEqual(0, f.SettlementFlags[index] & (ushort)SettlementViewFlags.Remembered);
            for (int k = 0; k < MatchFrame.StructureKinds; k++) Assert.Equal(0, f.SettlementStructures[index * MatchFrame.StructureKinds + k]);
            Assert.Equal(0, f.SettlementHalls[index]);
            Assert.Equal(0, f.SettlementZiggurats[index]);
            Assert.Equal(0, f.SettlementZigguratPermille[index]);
        }
        // A rival's private numbers are not in the frame; public ones are.
        for (int c = 1; c < f.CivCount; c++)
        {
            Assert.Equal(0, f.CivFlags[c] & (ushort)CivViewFlags.Detailed);
            Assert.Equal(0, f.CivPopulation[c]);
            Assert.Equal(0L, f.CivInsightPerYear[c]);
            Assert.Equal((0, 0, 0), (f.CivMandate[c], f.CivMandateCap[c], f.CivMandateNext[c]));
            Assert.Equal(all.CivEra[c], f.CivEra[c]);
            Assert.Equal(all.CivLegacy[c], f.CivLegacy[c]);
        }
        Assert.Equal(all.CivPopulation[0], f.CivPopulation[0]);
    }

    [Fact]
    public void Explored_land_stays_explored_with_the_owner_as_last_seen()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        Assert.Empty(w.Violations);
        Assert.True(w.ExploredObservations > 0, "parties on the road should have shown land that later went out of sight");
        Assert.True(w.PartiesSeen > 0);
        int explored = 0, visible = 0;
        foreach (byte v in w.ViewerFrame.Visibility)
        {
            if (v == (byte)TileVisibility.Explored) explored++;
            if (v == (byte)TileVisibility.Visible) visible++;
        }
        Assert.True(visible > 300);
        Assert.True(visible + explored < w.ViewerFrame.Visibility.Length / 2, "most of the map is still unknown after 30 minutes");
    }

    [Fact]
    public void Known_deposit_mask_never_exposes_unseen_resources()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        MatchFrame viewer = w.ViewerFrame;
        MatchFrame omniscient = w.OmniFrame;
        GeneratedWorld world = ViewKit.World.Value;
        for (int tile = 0; tile < viewer.Width * viewer.Height; tile++)
        {
            bool hasDeposit = world.TileAt(tile).Deposit != DepositKind.None;
            bool seen = viewer.Visibility[tile] != (byte)TileVisibility.Unknown;
            Assert.Equal(hasDeposit && seen, viewer.KnownDeposits[tile] != 0);
            Assert.Equal(hasDeposit, omniscient.KnownDeposits[tile] != 0);
        }
    }

    [Fact]
    public void Tile_claim_top_three_are_ordered_and_filtered_by_visibility()
    {
        WatchedMatch w = ViewKit.Watched.Value;
        MatchFrame viewer = w.ViewerFrame, omniscient = w.OmniFrame;
        for (int tile = 0; tile < viewer.Width * viewer.Height; tile++)
        {
            if (viewer.Visibility[tile] != (byte)TileVisibility.Visible)
            {
                Assert.Equal(0, viewer.Claim[tile]); Assert.Equal(-1, viewer.Claimant[tile]);
                Assert.Equal(0, viewer.Claim2[tile]); Assert.Equal(-1, viewer.Claimant2[tile]);
                Assert.Equal(0, viewer.Claim3[tile]); Assert.Equal(-1, viewer.Claimant3[tile]);
                continue;
            }
            Assert.Equal(omniscient.Claim[tile], viewer.Claim[tile]);
            Assert.Equal(omniscient.Claimant[tile], viewer.Claimant[tile]);
            Assert.Equal(omniscient.Claim2[tile], viewer.Claim2[tile]);
            Assert.Equal(omniscient.Claimant2[tile], viewer.Claimant2[tile]);
            Assert.Equal(omniscient.Claim3[tile], viewer.Claim3[tile]);
            Assert.Equal(omniscient.Claimant3[tile], viewer.Claimant3[tile]);
            Assert.True(viewer.Claim[tile] >= viewer.Claim2[tile] && viewer.Claim2[tile] >= viewer.Claim3[tile]);
        }
    }

    [Fact]
    public void Fog_does_not_leak_unseen_state_into_a_frame()
    {
        // In the spirit of the AI's fog-fairness test (docs/MVP/15 D10): change state the viewer cannot see and require
        // the same frame.
        WatchedMatch w = ViewKit.Watched.Value;
        byte[] snapshot = ViewKit.Snapshot(w.Match);
        MatchModel plain = ViewKit.Restore(snapshot), changed = ViewKit.Restore(snapshot);
        var before = new MatchFrame();
        new FrameBuilder(plain, 0).Build(plain, before);

        LivingEconomyModel eco = changed.Economy;
        int hidden = -1;
        for (int s = 0; s < eco.SettlementCount; s++)
        {
            var lite = eco.SettlementLite(s);
            if (lite.Civilization != 0 && eco.Population(lite.Id) > 6 && before.Visibility[lite.SiteTile] != (byte)TileVisibility.Visible)
            { hidden = s; break; }
        }
        Assert.True(hidden >= 0, "expected a rival settlement out of sight");
        var people = new List<FigureView>();
        eco.ListFigures(hidden, people);
        eco.KillFigure(people[0].Id);
        eco.WoundFigure(people[1].Id, 3);
        eco.AdjustCohesion(hidden, Sim.Numerics.Fix64.FromRatio(-2, 10));
        eco.GiveGoods(hidden, Sim.Numerics.Fix64.FromInt(50));

        var after = new MatchFrame();
        new FrameBuilder(changed, 0).Build(changed, after);
        Assert.Equal(ViewKit.Hash(before, includeTotals: false), ViewKit.Hash(after, includeTotals: false));

        // The omniscient view does see the change, so the test is not vacuous.
        var omniBefore = new MatchFrame();
        var omniAfter = new MatchFrame();
        new FrameBuilder(plain, -1).Build(plain, omniBefore);
        new FrameBuilder(changed, -1).Build(changed, omniAfter);
        Assert.NotEqual(ViewKit.Hash(omniBefore), ViewKit.Hash(omniAfter));
    }

    [Fact]
    public void Static_world_view_and_hex_layout_agree_with_the_hex_grid()
    {
        GeneratedWorld world = ViewKit.World.Value;
        WorldView view = WorldView.FromGenerated(world);
        Assert.Equal(world.TileCount, view.TileCount);
        for (int t = 0; t < world.TileCount; t += 7)
        {
            Assert.Equal((byte)world.TileAt(t).Biome, view.Biome[t]);
            Assert.Equal(world.TileAt(t).Elevation, view.Elevation[t]);
            Assert.Equal(t, HexLayout.NearestTile(HexLayout.CenterX(t, world.Width), HexLayout.CenterY(t, world.Width), world.Width, world.Height));
            HexGrid.Coordinates(t, world.Width, out int x, out int y);
            (int q, int r) = HexLayout.OffsetToAxial(x, y);
            Assert.Equal((x, y), HexLayout.AxialToOffset(q, r));
        }
        foreach (WorldStart start in world.Starts) Assert.Equal(start.Tile, view.StartTiles[start.Civilization]);
        // Neighbours are one tile apart, and each sits nearer its own centre than the shared edge's far side.
        Span<int> neighbors = stackalloc int[6];
        int tile = world.Width * 11 + 17;
        int n = HexLayout.Neighbors(tile, world.Width, world.Height, neighbors);
        Assert.Equal(6, n);
        for (int i = 0; i < n; i++)
        {
            Assert.Equal(1, HexLayout.Distance(tile, neighbors[i], world.Width));
            int mx = (HexLayout.CenterX(tile, world.Width) * 3 + HexLayout.CenterX(neighbors[i], world.Width)) / 4;
            int my = (HexLayout.CenterY(tile, world.Width) * 3 + HexLayout.CenterY(neighbors[i], world.Width)) / 4;
            Assert.Equal(tile, HexLayout.NearestTile(mx, my, world.Width, world.Height));
        }
    }
}
