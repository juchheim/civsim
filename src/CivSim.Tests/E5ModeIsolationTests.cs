using System.Linq;
using CivSim.Sim.Ai.Forecast;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>Every era-five row must remain inert until the civilization's own KEY-5 and outside Fief.</summary>
public sealed class E5ModeIsolationTests
{
    private static readonly string[] E5Projects = TestPack.Catalog.Projects
        .Where(p => p.Era == ChapterRules.Fief.LastResearchEra)
        .Select(p => p.Id)
        .ToArray();

    [Fact]
    public void Numeric_E5_rows_stay_hidden_and_effect_free_before_KEY5_and_through_Bronze_and_Iron()
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric(E5Projects));
        int[] eraFive = E5Projects.Select(catalog.IndexOf).ToArray();
        int[] e5 = eraFive.Where(p => !catalog.Projects[p].IsKeystone).ToArray();
        Assert.NotEmpty(e5);
        Assert.All(eraFive, p => Assert.True(catalog.Projects[p].Numeric));

        // Fief has the E5 ceiling, but each civilization's own KEY-5 is still the gate.
        var fief = new ResearchRig(civs: 4, catalog: catalog, rules: ChapterRules.Fief);
        fief.Do(fief.Grant(0, "KEY-4"));
        Assert.Equal(4, fief.Research.Era(0));
        Assert.All(e5, p => Assert.Equal(ProjectState.Hidden, fief.Research.State(0, p)));
        Assert.All(e5, p => Assert.False(fief.Research.IsDone(0, p)));
        var draftFief = new ResearchRig(civs: 4, rules: ChapterRules.Fief);
        draftFief.Do(draftFief.Grant(0, "KEY-4"));
        foreach (int p in e5)
        {
            fief.Do(fief.Grant(0, catalog.Projects[p].Id));
            draftFief.Do(draftFief.Grant(0, catalog.Projects[p].Id));
            Assert.False(fief.Research.IsDone(0, p));
        }
        Assert.Equal(ResearchHash(draftFief.Research), ResearchHash(fief.Research));

        fief.Do(fief.Grant(0, "KEY-5"));
        Assert.Equal(5, fief.Research.Era(0));
        Assert.Contains(e5, p => fief.Research.State(0, p) != ProjectState.Hidden);
        Assert.All(e5, p => Assert.Equal(ProjectState.Hidden, fief.Research.State(1, p)));
        foreach (int p in e5) fief.Do(fief.Grant(0, catalog.Projects[p].Id));
        Assert.All(e5, p => Assert.True(fief.Research.IsDone(0, p)));

        // Bronze and Iron never expose or accept era-five rows, even after KEY-4.
        foreach (ChapterRules rules in new[] { ChapterRules.Bronze, ChapterRules.Iron })
        {
            var rig = new ResearchRig(civs: 4, catalog: catalog, rules: rules);
            rig.Do(rig.Grant(0, "KEY-4"));
            Assert.All(e5, p => Assert.Equal(ProjectState.Hidden, rig.Research.State(0, p)));
            foreach (int p in e5) rig.Do(rig.Grant(0, catalog.Projects[p].Id));
            Assert.All(e5, p => Assert.False(rig.Research.IsDone(0, p)));
            var draftRig = new ResearchRig(civs: 4, rules: rules);
            draftRig.Do(draftRig.Grant(0, "KEY-4"));
            foreach (int p in e5) draftRig.Do(draftRig.Grant(0, catalog.Projects[p].Id));
            Assert.Equal(ResearchHash(draftRig.Research), ResearchHash(rig.Research));

            var forecast = new ForecastCatalog(catalog, rules.LastResearchEra);
            Assert.All(eraFive, p => Assert.False(forecast.IsNumeric[p]));
            Assert.All(eraFive, p => Assert.DoesNotContain(p, forecast.Numeric));
        }
    }

    private static ulong ResearchHash(ResearchSystem research)
    {
        var hasher = new CivSim.Sim.Core.StateHasher();
        research.Hash(ref hasher);
        return hasher.Finish();
    }
}
