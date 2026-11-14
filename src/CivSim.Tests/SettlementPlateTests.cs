using System;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Tests;

/// <summary>What a settlement's map plate shows, and how plates rank (docs/MVP/33).</summary>
public sealed class SettlementPlateTests
{
    private static readonly int PitHouse = LivingEconomyModel.StructureIndex("pit_house");

    private static MatchFrame Frame(int settlements = 4, int civs = 4)
    {
        var f = new MatchFrame();
        f.Reserve(figures: 4, settlements: settlements, herds: 1);
        f.EnsureCivs(civs);
        f.ViewerCiv = 0; f.CivCount = civs;
        return f;
    }

    private static void Put(MatchFrame f, int row, int civ, int population, SettlementViewFlags flags, int houses = 0, int halls = 0)
    {
        if (row >= f.SettlementCount) f.SettlementCount = row + 1;
        f.SettlementIndex[row] = row; f.SettlementCiv[row] = (sbyte)civ; f.SettlementPopulation[row] = population;
        f.SettlementFlags[row] = (ushort)flags;
        f.SettlementStructures[row * MatchFrame.StructureKinds + PitHouse] = (byte)houses;
        f.SettlementHalls[row] = (byte)halls;
    }

    [Fact]
    public void A_camp_with_no_dwelling_and_few_people_is_a_band()
    {
        MatchFrame f = Frame();
        Put(f, 0, 0, 12, SettlementViewFlags.Own);
        Assert.Equal(PlateTier.Band, SettlementPlate.Facts(f, 0).Tier);
    }

    [Fact]
    public void A_dwelling_or_a_crowd_makes_a_village_then_a_town_then_a_city()
    {
        MatchFrame f = Frame();
        Put(f, 0, 0, 12, SettlementViewFlags.Own, houses: 2);
        Put(f, 1, 0, 59, SettlementViewFlags.Own, houses: 6);
        Put(f, 2, 0, 60, SettlementViewFlags.Own, houses: 6);
        Put(f, 3, 0, 120, SettlementViewFlags.Own, houses: 6);
        Assert.Equal(PlateTier.Village, SettlementPlate.Facts(f, 0).Tier);
        Assert.Equal(PlateTier.Village, SettlementPlate.Facts(f, 1).Tier);
        Assert.Equal(PlateTier.Town, SettlementPlate.Facts(f, 2).Tier);
        Assert.Equal(PlateTier.Town, SettlementPlate.Facts(f, 3).Tier);
        f.SettlementTemples[3] = 1;
        Assert.Equal(PlateTier.City, SettlementPlate.Facts(f, 3).Tier);
        f.SettlementTemples[3] = 0; f.SettlementZigguratPermille[3] = 200;
        Assert.Equal(PlateTier.City, SettlementPlate.Facts(f, 3).Tier);
        f.SettlementZigguratPermille[3] = 0; f.SettlementPopulation[3] = 89; f.SettlementZiggurats[3] = 1;
        Assert.Equal(PlateTier.Town, SettlementPlate.Facts(f, 3).Tier);
    }

    [Fact]
    public void A_hall_counts_as_a_permanent_dwelling()
    {
        MatchFrame f = Frame();
        Put(f, 0, 0, 12, SettlementViewFlags.Own, halls: 1);
        Assert.Equal(PlateTier.Village, SettlementPlate.Facts(f, 0).Tier);
    }

    [Fact]
    public void A_remembered_settlement_is_sized_by_its_head_count_alone()
    {
        MatchFrame f = Frame();
        Put(f, 0, 1, 15, SettlementViewFlags.Remembered);
        Put(f, 1, 1, 25, SettlementViewFlags.Remembered);
        Put(f, 2, 1, 70, SettlementViewFlags.Remembered);
        Assert.Equal(PlateTier.Band, SettlementPlate.Facts(f, 0).Tier);
        Assert.Equal(PlateTier.Village, SettlementPlate.Facts(f, 1).Tier);
        Assert.Equal(PlateTier.Town, SettlementPlate.Facts(f, 2).Tier);
        Assert.True(SettlementPlate.Facts(f, 2).Remembered);
    }

    [Fact]
    public void The_rim_relation_follows_the_civilization_flags()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)(CivViewFlags.Contacted | CivViewFlags.AtWar);
        f.CivFlags[2] = (ushort)(CivViewFlags.Contacted | CivViewFlags.Allied);
        f.CivFlags[3] = (ushort)(CivViewFlags.Aware | CivViewFlags.Contacted | CivViewFlags.Pact);
        Put(f, 0, 0, 30, SettlementViewFlags.Own, houses: 3);
        Put(f, 1, 1, 30, SettlementViewFlags.None, houses: 3);
        Put(f, 2, 2, 30, SettlementViewFlags.None, houses: 3);
        Put(f, 3, 3, 30, SettlementViewFlags.None, houses: 3);
        Assert.Equal(PlateRelation.Own, SettlementPlate.Facts(f, 0).Relation);
        Assert.Equal(PlateRelation.AtWar, SettlementPlate.Facts(f, 1).Relation);
        Assert.Equal(PlateRelation.Allied, SettlementPlate.Facts(f, 2).Relation);
        Assert.Equal(PlateRelation.Contacted, SettlementPlate.Facts(f, 3).Relation);
        f.CivFlags[3] = (ushort)CivViewFlags.Aware;
        Assert.Equal(PlateRelation.Unmet, SettlementPlate.Facts(f, 3).Relation);
    }

    [Fact]
    public void Free_folk_and_tin_folk_belong_to_nobody()
    {
        MatchFrame f = Frame();
        f.CivFlags[1] = (ushort)CivViewFlags.AtWar;
        Put(f, 0, 1, 20, SettlementViewFlags.FreeFolk, houses: 2);
        Put(f, 1, 1, 20, SettlementViewFlags.TinFolk | SettlementViewFlags.Palisade, houses: 2);
        Assert.Equal(PlateRelation.Wild, SettlementPlate.Facts(f, 0).Relation);
        PlateFacts tin = SettlementPlate.Facts(f, 1);
        Assert.Equal(PlateRelation.Wild, tin.Relation);
        Assert.True(tin.TinFolk);
    }

    [Fact]
    public void A_remembered_capital_is_not_marked_and_empty_sites_say_so()
    {
        MatchFrame f = Frame();
        f.CivFlags[1] = (ushort)CivViewFlags.Contacted;
        Put(f, 0, 1, 80, SettlementViewFlags.Capital, houses: 5);
        Put(f, 1, 1, 80, SettlementViewFlags.Capital | SettlementViewFlags.Remembered);
        Put(f, 2, 1, 0, SettlementViewFlags.Empty | SettlementViewFlags.Remembered);
        Assert.True(SettlementPlate.Facts(f, 0).Capital);
        Assert.False(SettlementPlate.Facts(f, 1).Capital);
        Assert.True(SettlementPlate.Facts(f, 2).Empty);
    }

    [Fact]
    public void Facts_compare_by_value_so_a_redraw_happens_only_on_change()
    {
        MatchFrame f = Frame();
        Put(f, 0, 0, 30, SettlementViewFlags.Own, houses: 3);
        PlateFacts a = SettlementPlate.Facts(f, 0), b = SettlementPlate.Facts(f, 0);
        Assert.Equal(a, b);
        f.SettlementPopulation[0] = 31;
        Assert.NotEqual(a, SettlementPlate.Facts(f, 0));
    }

    [Fact]
    public void Relevance_ranks_own_capital_over_levy_target_over_war_over_the_rest()
    {
        MatchFrame f = Frame(8);
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)(CivViewFlags.Contacted | CivViewFlags.AtWar);
        f.CivFlags[2] = (ushort)(CivViewFlags.Contacted | CivViewFlags.Allied);
        Put(f, 0, 0, 40, SettlementViewFlags.Own | SettlementViewFlags.Capital, houses: 4);
        Put(f, 1, 1, 40, SettlementViewFlags.LevyTarget, houses: 4);
        Put(f, 2, 0, 40, SettlementViewFlags.Own, houses: 4);
        Put(f, 3, 1, 40, SettlementViewFlags.None, houses: 4);
        Put(f, 4, 2, 40, SettlementViewFlags.None, houses: 4);
        Put(f, 5, 1, 40, SettlementViewFlags.FreeFolk, houses: 4);
        Put(f, 6, 1, 40, SettlementViewFlags.Remembered);
        Put(f, 7, 1, 0, SettlementViewFlags.Empty);
        int[] scores = new int[8];
        for (int i = 0; i < 8; i++) scores[i] = SettlementPlate.Relevance(SettlementPlate.Facts(f, i));
        Assert.Equal(new[] { 70, 60, 50, 40, 30, 20, 10, 0 }, scores);
    }

    private static void Siege(MatchFrame f, int settlement, int integrity)
    {
        f.Iron.Active = true; f.Iron.WorkKinds = LivingEconomyModel.IronWorkKindCount;
        f.Iron.Works = new byte[f.SettlementCount * f.Iron.WorkKinds];
        f.Iron.SiegeCount = 1;
        f.Iron.SiegeSettlement = new[] { settlement };
        f.Iron.SiegeIntegrityPermille = new[] { (short)integrity };
    }

    private static void Raid(MatchFrame f, PartyKind kind, int to, int targetCiv, int arrive)
    {
        f.PartyCount = 1;
        f.PartyKind = new[] { (byte)kind }; f.PartyTo = new[] { to }; f.PartyTargetCiv = new[] { (sbyte)targetCiv };
        f.PartyArriveSeason = new[] { arrive };
    }

    [Fact]
    public void A_siege_beats_every_other_alert_and_turns_the_ring_into_wall_integrity()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)(CivViewFlags.Contacted | CivViewFlags.AtWar);
        Put(f, 0, 0, 40, SettlementViewFlags.Own | SettlementViewFlags.Occupied, houses: 4);
        Put(f, 1, 1, 40, SettlementViewFlags.None, houses: 4);
        f.SettlementFood[0] = (byte)FoodBand.Famine;
        f.SettlementBuildKind[0] = 2; f.SettlementBuildPermille[0] = 500;
        Siege(f, 0, 620);
        PlateFacts own = SettlementPlate.Facts(f, 0);
        Assert.Equal(PlateAlert.Besieged, own.Alert);
        Assert.Equal(62, own.AlertNumber);
        Assert.Equal(PlateMeter.Siege, own.Meter);
        Assert.Equal(620, own.MeterPermille);
        Assert.True(own.Urgent);
        Assert.Equal(80, SettlementPlate.Relevance(own));
        Siege(f, 1, 900);
        PlateFacts foreign = SettlementPlate.Facts(f, 1);
        Assert.Equal(PlateAlert.Besieged, foreign.Alert);
        Assert.Equal(PlateMeter.Siege, foreign.Meter);
        Assert.Equal(60, SettlementPlate.Relevance(foreign));
    }

    [Fact]
    public void An_incoming_raid_counts_seasons_and_only_alerts_the_target()
    {
        MatchFrame f = Frame();
        f.Season = 10; f.FocusMandateUnlocked = true;
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)(CivViewFlags.Contacted | CivViewFlags.AtWar);
        Put(f, 0, 0, 40, SettlementViewFlags.Own, houses: 4);
        Put(f, 1, 1, 40, SettlementViewFlags.None, houses: 4);
        Raid(f, PartyKind.Raid, to: 0, targetCiv: 0, arrive: 13);
        PlateFacts own = SettlementPlate.Facts(f, 0);
        Assert.Equal(PlateAlert.Raid, own.Alert);
        Assert.Equal(3, own.AlertNumber);
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 1).Alert);
        Raid(f, PartyKind.Caravan, to: 0, targetCiv: 0, arrive: 13);
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 0).Alert);
        Raid(f, PartyKind.Raid, to: 0, targetCiv: 1, arrive: 13);
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 0).Alert);
    }

    [Fact]
    public void Private_alerts_are_shown_for_own_towns_only_and_in_priority_order()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)CivViewFlags.Contacted;
        f.FocusMandateUnlocked = true;
        Put(f, 0, 0, 40, SettlementViewFlags.Own | SettlementViewFlags.FireLit, houses: 4);
        Put(f, 1, 1, 40, SettlementViewFlags.FireLit, houses: 4);
        f.SettlementFood[0] = (byte)FoodBand.Short; f.SettlementCohesion[0] = (byte)CohesionBand.Fraying;
        f.SettlementFood[1] = (byte)FoodBand.Famine; f.SettlementCohesion[1] = (byte)CohesionBand.Fraying;
        Assert.Equal(PlateAlert.Short, SettlementPlate.Facts(f, 0).Alert);
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 1).Alert);
        f.SettlementFood[0] = (byte)FoodBand.Famine;
        Assert.Equal(PlateAlert.Famine, SettlementPlate.Facts(f, 0).Alert);
        f.SettlementFood[0] = 0;
        Assert.Equal(PlateAlert.Fraying, SettlementPlate.Facts(f, 0).Alert);
        f.SettlementCohesion[0] = 0; f.SettlementWinterForecast[0] = (byte)FoodBand.Short;
        Assert.Equal(PlateAlert.HungryWinter, SettlementPlate.Facts(f, 0).Alert);
        f.SettlementWinterForecast[0] = 0;
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 0).Alert);
        f.SettlementFlags[0] = (ushort)(SettlementViewFlags.Own | SettlementViewFlags.FissionPending | SettlementViewFlags.FireLit);
        Assert.Equal(PlateAlert.Splitting, SettlementPlate.Facts(f, 0).Alert);
    }

    [Fact]
    public void A_capital_does_not_fray_and_a_cold_hearth_only_matters_before_the_mandate()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        Put(f, 0, 0, 40, SettlementViewFlags.Own | SettlementViewFlags.Capital | SettlementViewFlags.FireLit, houses: 4);
        f.SettlementCohesion[0] = (byte)CohesionBand.Fraying;
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 0).Alert);
        f.SettlementFlags[0] = (ushort)(SettlementViewFlags.Own | SettlementViewFlags.Capital);
        f.SettlementCohesion[0] = 0;
        Assert.Equal(PlateAlert.FireOut, SettlementPlate.Facts(f, 0).Alert);
        f.FocusMandateUnlocked = true;
        Assert.Equal(PlateAlert.None, SettlementPlate.Facts(f, 0).Alert);
    }

    [Fact]
    public void Occupation_is_public_and_the_levy_target_is_foreign_only()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)(CivViewFlags.Contacted | CivViewFlags.AtWar);
        Put(f, 0, 1, 40, SettlementViewFlags.Occupied, houses: 4);
        Put(f, 1, 1, 40, SettlementViewFlags.LevyTarget, houses: 4);
        Assert.Equal(PlateAlert.Occupied, SettlementPlate.Facts(f, 0).Alert);
        Assert.Equal(PlateAlert.LevyTarget, SettlementPlate.Facts(f, 1).Alert);
    }

    [Fact]
    public void Remembered_and_empty_plates_show_no_alert_meter_or_defence()
    {
        MatchFrame f = Frame();
        f.CivFlags[1] = (ushort)CivViewFlags.Contacted;
        Put(f, 0, 1, 40, SettlementViewFlags.Remembered | SettlementViewFlags.Occupied | SettlementViewFlags.Palisade);
        Put(f, 1, 1, 0, SettlementViewFlags.Empty | SettlementViewFlags.Palisade);
        Siege(f, 0, 500);
        foreach (int row in new[] { 0, 1 })
        {
            PlateFacts facts = SettlementPlate.Facts(f, row);
            Assert.Equal(PlateAlert.None, facts.Alert);
            Assert.Equal(PlateMeter.None, facts.Meter);
            Assert.Equal(PlateDefence.None, facts.Defence);
        }
    }

    [Fact]
    public void Build_progress_is_the_own_ring_and_idle_or_foreign_towns_have_none()
    {
        MatchFrame f = Frame();
        f.CivFlags[0] = (ushort)CivViewFlags.Self;
        f.CivFlags[1] = (ushort)CivViewFlags.Contacted;
        Put(f, 0, 0, 40, SettlementViewFlags.Own, houses: 4);
        Put(f, 1, 0, 40, SettlementViewFlags.Own, houses: 4);
        Put(f, 2, 1, 40, SettlementViewFlags.None, houses: 4);
        for (int i = 0; i < f.SettlementCount; i++) f.SettlementBuildKind[i] = -1;
        f.SettlementBuildKind[0] = 3; f.SettlementBuildPermille[0] = 625;
        f.SettlementBuildKind[2] = 3; f.SettlementBuildPermille[2] = 625;
        PlateFacts building = SettlementPlate.Facts(f, 0);
        Assert.Equal(PlateMeter.Build, building.Meter);
        Assert.Equal(625, building.MeterPermille);
        Assert.Equal(PlateMeter.None, SettlementPlate.Facts(f, 1).Meter);
        Assert.Equal(PlateMeter.None, SettlementPlate.Facts(f, 2).Meter);
    }

    [Fact]
    public void The_defence_silhouette_is_the_strongest_work_standing()
    {
        MatchFrame f = Frame();
        f.CivFlags[1] = (ushort)CivViewFlags.Contacted;
        int walls = LivingEconomyModel.StructureIndex("stone_walls");
        int castle = -1;
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++) if (LivingEconomyModel.IronWorkId(k) == "castle") castle = k;
        Put(f, 0, 1, 40, SettlementViewFlags.ThornBarrier, houses: 4);
        Assert.Equal(PlateDefence.Thorn, SettlementPlate.Facts(f, 0).Defence);
        f.SettlementFlags[0] = (ushort)(SettlementViewFlags.ThornBarrier | SettlementViewFlags.Palisade);
        Assert.Equal(PlateDefence.Palisade, SettlementPlate.Facts(f, 0).Defence);
        f.SettlementStructures[walls] = 1;
        Assert.Equal(PlateDefence.Walls, SettlementPlate.Facts(f, 0).Defence);
        f.Iron.Active = true; f.Iron.WorkKinds = LivingEconomyModel.IronWorkKindCount;
        f.Iron.Works = new byte[f.SettlementCount * f.Iron.WorkKinds];
        f.Iron.Works[castle] = 1;
        Assert.Equal(PlateDefence.Castle, SettlementPlate.Facts(f, 0).Defence);
    }

    [Fact]
    public void The_frame_builder_marks_each_capital_once_and_never_a_free_folk_village()
    {
        MatchModel match = ViewKit.NewMatch();
        var runner = new SimRunner(match);
        var builder = new FrameBuilder(match, -1);
        var frame = new MatchFrame();
        for (int step = 0; step < 6; step++)
        {
            runner.Advance(1200);
            builder.Build(match, frame);
            var perCiv = new int[8];
            for (int i = 0; i < frame.SettlementCount; i++)
            {
                var flags = (SettlementViewFlags)frame.SettlementFlags[i];
                if ((flags & SettlementViewFlags.Capital) == 0) continue;
                Assert.Equal(0, (int)(flags & SettlementViewFlags.FreeFolk));
                perCiv[frame.SettlementCiv[i]]++;
            }
            foreach (int count in perCiv) Assert.InRange(count, 0, 1);
        }
    }
}
