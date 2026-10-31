using System;
using CivSim.Sim.Ai;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Tests;

public sealed class BronzeDefensePolicyTests
{
    [Fact]
    public void Direct_bronze_belief_starts_preparation_without_the_threat_score_floor()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));

        rivals[1].ProjectBelieved[met05] = true;
        rivals[1].ThreatProbability = Fix64.Zero;
        Assert.Equal(0, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));

        rivals[1].Settlements[0].LastSeenSeason = 11;
        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));
    }

    [Fact]
    public void Preparation_uses_one_extra_reach_as_a_warning_buffer_and_stops_at_one_queued_defense()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements[0].Tile = 100;
        rivals[1].Settlements[0].Tile = 140;
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        rivals[1].ThreatProbability = Fix64.FromRatio(18, 1000);

        // For reach 12, the raid selector spans 36 tiles and the defense planner looks one reach farther.
        Assert.Equal(0, BronzeDefensePolicy.SelectSettlement(0, 20, 160, 12,
            own.Settlements, rivals, TestPack.Catalog.IndexOf("MET-04"), TestPack.Catalog.IndexOf("MET-05")));

        own.Settlements[0].PhysicalDefenseQueued = true;
        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 160, 12,
            own.Settlements, rivals, TestPack.Catalog.IndexOf("MET-04"), TestPack.Catalog.IndexOf("MET-05")));
    }

    [Fact]
    public void High_confidence_front_prepares_all_exposed_settlements_before_early_era_hedges()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        Array.Resize(ref rivals, 4);
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 103 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 105 });
        rivals[1].Contacted = true;
        rivals[1].Era = 1;
        rivals[2] = new RivalBelief { Civ = 2, ProjectBelieved = new bool[TestPack.Catalog.Count] };
        rivals[2].Contacted = true;
        rivals[2].Era = 2;
        rivals[2].Settlements.Add(new BeliefSettlement { Settlement = 2, Tile = 100, LastSeenSeason = 20 });
        rivals[3] = new RivalBelief { Civ = 3, ProjectBelieved = new bool[TestPack.Catalog.Count] };
        rivals[3].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        rivals[3].Settlements.Add(new BeliefSettlement { Settlement = 3, Tile = 105, LastSeenSeason = 20 });
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        BronzeDefensePolicy.PreparationTarget[] targets = BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(2, true),
            new BronzeDefensePolicy.PreparationTarget(1, true),
            new BronzeDefensePolicy.PreparationTarget(0, true),
        }, targets);
    }

    [Fact]
    public void One_high_confidence_rival_covers_all_exposed_settlements()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 104 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 107 });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        BronzeDefensePolicy.PreparationTarget[] targets = BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(0, true),
            new BronzeDefensePolicy.PreparationTarget(1, true),
            new BronzeDefensePolicy.PreparationTarget(2, true),
        }, targets);
    }

    [Fact]
    public void High_confidence_rivals_each_get_one_target_before_a_second_target_is_assigned()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        Array.Resize(ref rivals, 3);
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 102 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 104 });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        rivals[2] = new RivalBelief { Civ = 2, ProjectBelieved = new bool[TestPack.Catalog.Count] };
        rivals[2].ProjectBelieved[TestPack.Catalog.IndexOf("MET-05")] = true;
        rivals[2].Settlements.Add(new BeliefSettlement { Settlement = 2, Tile = 104, LastSeenSeason = 20 });

        BronzeDefensePolicy.PreparationTarget[] targets = BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, TestPack.Catalog.IndexOf("MET-04"), TestPack.Catalog.IndexOf("MET-05"));
        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(1, true),
            new BronzeDefensePolicy.PreparationTarget(2, true),
            new BronzeDefensePolicy.PreparationTarget(0, true),
        }, targets);
    }

    [Fact]
    public void Believed_bronze_without_a_fresh_enemy_site_falls_back_to_every_unfortified_settlement()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 104 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 108 });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        rivals[1].Settlements.Clear();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(0, true),
            new BronzeDefensePolicy.PreparationTarget(1, true),
            new BronzeDefensePolicy.PreparationTarget(2, true),
        }, BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));

        own.Settlements[1].PhysicalDefenseQueued = true;
        own.Settlements[2].Defenses = new Defenses(false, true, false, false, false, false, false,
            false, false, false, false, false, false, false);
        Assert.Equal(new[] { new BronzeDefensePolicy.PreparationTarget(0, true) },
            BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
                own.Settlements, rivals, met04, met05));
    }

    [Fact]
    public void Only_queued_defenses_among_the_two_selected_high_confidence_targets_consume_slots()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 104 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 107, PhysicalDefenseQueued = true });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        BronzeDefensePolicy.PreparationTarget[] targets = BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(0, true),
            new BronzeDefensePolicy.PreparationTarget(1, true),
        }, targets);

        own.Settlements[0].PhysicalDefenseQueued = true;
        targets = BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        Assert.Equal(new[] { new BronzeDefensePolicy.PreparationTarget(1, true) }, targets);
    }

    [Fact]
    public void Early_era_hedge_prepares_each_exposed_settlement_but_requires_contact_and_fresh_site()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 104 });
        RivalBelief rival = rivals[1];
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");
        rival.Contacted = true;
        rival.Era = 1;

        Assert.Equal(new[]
        {
            new BronzeDefensePolicy.PreparationTarget(0, false),
            new BronzeDefensePolicy.PreparationTarget(1, false),
        },
            BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
                own.Settlements, rivals, met04, met05));

        rival.Contacted = false;
        Assert.Empty(BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));

        rival.Contacted = true;
        rival.Era = 0;
        Assert.Empty(BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));

        rival.Era = 1;
        rival.Settlements[0].LastSeenSeason = 11;
        Assert.Empty(BronzeDefensePolicy.SelectPreparationTargets(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
    }

    [Fact]
    public void Stale_sites_are_ignored_and_only_palisades_hillforts_or_walls_count_as_defense()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");
        rivals[1].ProjectBelieved[met04] = true;
        rivals[1].ThreatProbability = Fix64.Half;
        own.Settlements[0].Defenses = new Defenses(true, false, true, false, false, false, false,
            false, false, false, false, false, false, false);

        Assert.Equal(0, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));
        rivals[1].Settlements[0].LastSeenSeason = 11;
        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));
    }

    [Fact]
    public void Proactive_diagnostic_uses_public_E3_and_ranks_nearest_then_weaker_exposure()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 103,
            Defenses = new Defenses(true, false, true, true, false, false, false, false, false, false) });
        rivals[1].Era = 3;
        rivals[1].ThreatProbability = Fix64.FromRatio(23, 1000);

        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
        Assert.Equal(1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05, proactivePublicEra: true));

        rivals[1].ProjectBelieved[met04] = true;
        rivals[1].ThreatProbability = Fix64.Half;
        Assert.Equal(1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
        Assert.Equal(1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05, proactivePublicEra: true));
    }

    [Fact]
    public void Proactive_diagnostic_breaks_equal_distance_by_weaker_exposure_then_settlement_index()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");
        own.Settlements.Add(new OwnSettlement { Index = 3, Tile = 101 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 101 });
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 103,
            Defenses = new Defenses(true, false, true, true, false, false, false, false, false, false) });
        rivals[1].Era = 3;

        Assert.Equal(2, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05, proactivePublicEra: true));
    }

    [Fact]
    public void Public_era_shadow_requires_contact_and_selects_nearest_fresh_site()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 1, Tile = 103 });
        rivals[1].Era = 3;
        rivals[1].ThreatProbability = Fix64.FromRatio(23, 1000);

        Assert.False(BronzeDefensePolicy.TrySelectPublicEraShadowTarget(0, 20, 16, 18,
            own.Settlements, rivals, out _));

        rivals[1].Contacted = true;
        Assert.True(BronzeDefensePolicy.TrySelectPublicEraShadowTarget(0, 20, 16, 18,
            own.Settlements, rivals, out var selected));
        Assert.Equal(1, selected.Settlement);
        Assert.Equal(1, selected.Rival);
        Assert.Equal(1, selected.Site);
        Assert.Equal(0, selected.SiteAge);
        Assert.Equal(1, selected.Distance);
        Assert.Equal(Fix64.FromRatio(23, 1000), selected.ThreatProbability);

        rivals[1].Settlements[0].LastSeenSeason = 11;
        Assert.False(BronzeDefensePolicy.TrySelectPublicEraShadowTarget(0, 20, 16, 18,
            own.Settlements, rivals, out _));
    }

    [Fact]
    public void Belief_only_hedge_falls_back_to_capital_then_frontier_without_fresh_sites()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 4, Tile = 104 });
        own.Settlements.Add(new OwnSettlement { Index = 2, Tile = 101 });
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");
        rivals[1].ProjectBelieved[met04] = true;
        rivals[1].ThreatProbability = Fix64.FromRatio(1, 100);
        rivals[1].Settlements.Clear();

        int[] targets = BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);

        Assert.Equal(new[] { 0, 4 }, targets);
        Assert.Empty(BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, Scene().Rivals, met04, met05));
    }

    [Fact]
    public void Hedge_orders_frontier_first_when_fresh_site_is_nearer_and_capital_first_on_tie()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 4, Tile = 102 });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-05")] = true;
        rivals[1].ThreatProbability = Fix64.Zero;
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        Assert.Equal(new[] { 4, 0 }, BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));

        rivals[1].Settlements[0].Tile = 101;
        Assert.Equal(new[] { 0, 4 }, BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
    }

    [Fact]
    public void Hedge_filters_queued_and_built_targets_and_keeps_pair_order_stable()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        own.Settlements.Add(new OwnSettlement { Index = 4, Tile = 104 });
        rivals[1].ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")] = true;
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        int[] first = BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        int[] second = BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05);
        Assert.Equal(new[] { 0, 4 }, first);
        Assert.Equal(first, second);

        own.Settlements[0].PhysicalDefenseQueued = true;
        Assert.Equal(new[] { 4 }, BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
        own.Settlements[1].Defenses = new Defenses(false, true, false, false, false, false, false, false, false, false,
            false, false, false, false);
        Assert.Empty(BronzeDefensePolicy.SelectHedgeSettlements(0, 20, 16, 18,
            own.Settlements, rivals, met04, met05));
    }

    private static (OwnView Own, RivalBelief[] Rivals) Scene()
    {
        var own = new OwnView { Civ = 0, ReachTiles = 18 };
        own.Settlements.Add(new OwnSettlement { Index = 0, Tile = 100 });
        var rivals = new[]
        {
            new RivalBelief { Civ = 0, ProjectBelieved = new bool[TestPack.Catalog.Count] },
            new RivalBelief { Civ = 1, ProjectBelieved = new bool[TestPack.Catalog.Count] },
        };
        rivals[1].Settlements.Add(new BeliefSettlement { Settlement = 1, Tile = 102, LastSeenSeason = 20 });
        return (own, rivals);
    }
}
