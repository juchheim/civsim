using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class BeliefBronzeEvidenceTests
{
    private static (TechEstimate Tech, RivalBelief Rival) NewEvidence()
    {
        MatchModel match = MatchModel.Create(20261019, TestPack.Pack);
        var tech = new TechEstimate(TestPack.Catalog, match.Politics.WeaponLadder,
            TestPack.Catalog.IndexOf("WAR-03"));
        var rival = new RivalBelief { Civ = 1, ProjectBelieved = new bool[TestPack.Catalog.Count] };
        rival.SeenFlags = SeenFlag.TinBronzeKit | SeenFlag.Hillfort | SeenFlag.StoneWalls |
            SeenFlag.TinMineCamp | SeenFlag.Scribes;
        return (tech, rival);
    }

    [Fact]
    public void Sighted_Bronze_evidence_adds_only_projects_proved_by_the_signal()
    {
        (TechEstimate tech, RivalBelief rival) = NewEvidence();

        BeliefBuilder.AddVisibleBronzeEvidence(tech, rival, visible: true);

        Assert.True(rival.ProjectBelieved[TestPack.Catalog.IndexOf("MET-05")]);
        Assert.True(rival.ProjectBelieved[TestPack.Catalog.IndexOf("DEF-04")]);
        Assert.True(rival.ProjectBelieved[TestPack.Catalog.IndexOf("DEF-05")]);
        Assert.True(rival.ProjectBelieved[TestPack.Catalog.IndexOf("COM-05")]);
        Assert.False(rival.ProjectBelieved[TestPack.Catalog.IndexOf("MET-04")]);
        Assert.False(rival.ProjectBelieved[TestPack.Catalog.IndexOf("WAR-06")]);
    }

    [Fact]
    public void The_same_E3_evidence_does_not_update_beliefs_without_sight()
    {
        (TechEstimate tech, RivalBelief rival) = NewEvidence();

        BeliefBuilder.AddVisibleBronzeEvidence(tech, rival, visible: false);

        Assert.DoesNotContain(true, rival.ProjectBelieved);
    }

    [Fact]
    public void Caravan_cargo_and_compact_are_recorded_only_when_the_party_tile_is_in_sight()
    {
        var rival = new RivalBelief { Civ = 1 };
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 1, Id = 42, Good = TradeGood.Tin,
            Cargo = Fix64.FromInt(12), CompactId = 9,
        };

        Assert.False(BeliefBuilder.RecordVisibleCaravan(rival, caravan, tile: 7, season: 3, visible: false));
        Assert.Empty(rival.CaravansSeen);
        Assert.False(rival.TinAccessSeen);

        Assert.True(BeliefBuilder.RecordVisibleCaravan(rival, caravan, tile: 7, season: 3, visible: true));
        BeliefCaravan seen = Assert.Single(rival.CaravansSeen);
        Assert.Equal(42, seen.Id);
        Assert.Equal(7, seen.Tile);
        Assert.Equal(9, seen.CompactId);
        Assert.Equal(TradeGood.Tin, seen.Good);
        Assert.Equal(Fix64.FromInt(12), seen.Cargo);
        Assert.Equal(3, seen.LastSeenSeason);
        Assert.True(rival.TinAccessSeen);
    }

    [Fact]
    public void A_later_unseen_caravan_position_does_not_replace_the_last_sighting()
    {
        var rival = new RivalBelief { Civ = 1 };
        var caravan = new Expedition { Kind = ExpeditionKind.Caravan, Civ = 1, Id = 42, Good = TradeGood.Tin, Cargo = Fix64.FromInt(12), CompactId = 9 };
        BeliefBuilder.RecordVisibleCaravan(rival, caravan, tile: 7, season: 3, visible: true);
        caravan.Good = TradeGood.Copper;
        caravan.Cargo = Fix64.FromInt(25);

        Assert.False(BeliefBuilder.RecordVisibleCaravan(rival, caravan, tile: 10, season: 4, visible: false));
        BeliefCaravan seen = Assert.Single(rival.CaravansSeen);
        Assert.Equal(7, seen.Tile);
        Assert.Equal(3, seen.LastSeenSeason);
        Assert.Equal(TradeGood.Tin, seen.Good);
        Assert.Equal(Fix64.FromInt(12), seen.Cargo);
        Assert.True(rival.TinAccessSeen);
    }
}
