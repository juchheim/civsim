using CivSim.Game.Ui;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class ResearchEraGrantTests
{
    [Fact]
    public void Era_six_inspection_grants_Fief_projects_but_leaves_its_ending_keystone_available()
    {
        var catalog = new ResearchCatalog(TestPack.WithNumeric("KEY-5", "KEY-6", "AGR-11", "AGR-12"));

        ProjectDef key5 = catalog.Projects[catalog.IndexOf("KEY-5")];
        ProjectDef key6 = catalog.Projects[catalog.IndexOf("KEY-6")];
        ProjectDef fiefProject = catalog.Projects[catalog.IndexOf("AGR-11")];
        ProjectDef nextAgeProject = catalog.Projects[catalog.IndexOf("AGR-12")];

        Assert.True(ResearchEraGrant.ShouldGrant(key5, 6));
        Assert.True(ResearchEraGrant.ShouldGrant(fiefProject, 6));
        Assert.False(ResearchEraGrant.ShouldGrant(key6, 6));
        Assert.True(ResearchEraGrant.ShouldGrant(key6, 7));
        Assert.False(ResearchEraGrant.ShouldGrant(nextAgeProject, 6));
    }
}
