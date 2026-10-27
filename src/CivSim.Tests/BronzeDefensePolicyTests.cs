using CivSim.Sim.Ai;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Tests;

public sealed class BronzeDefensePolicyTests
{
    [Fact]
    public void Only_believed_bronze_and_a_supported_threat_prioritize_an_exposed_settlement()
    {
        (OwnView own, RivalBelief[] rivals) = Scene();
        int met04 = TestPack.Catalog.IndexOf("MET-04"), met05 = TestPack.Catalog.IndexOf("MET-05");

        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));

        rivals[1].ProjectBelieved[met05] = true;
        rivals[1].ThreatProbability = Fix64.Zero;
        Assert.Equal(-1, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));

        rivals[1].ThreatProbability = Fix64.Half;
        Assert.Equal(0, BronzeDefensePolicy.SelectSettlement(0, 20, 16, 18, own.Settlements, rivals, met04, met05));
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
