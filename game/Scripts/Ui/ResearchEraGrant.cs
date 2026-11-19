using CivSim.Sim.Research;

namespace CivSim.Game.Ui;

/// <summary>Which projects a developer inspection launch completes before entering a requested research era.</summary>
public static class ResearchEraGrant
{
    /// <summary>
    /// A keystone belongs to the era it enters, not the era it leaves. In particular, a Fief inspection at era 6 must leave KEY-6 available; completing it ends the
    /// inspection match before the player can inspect its late-age systems.
    /// </summary>
    public static bool ShouldGrant(ProjectDef project, int targetEra)
    {
        int effectiveEra = project.IsKeystone ? project.Enters : project.Era;
        return project.Numeric && effectiveEra >= 0 && effectiveEra < targetEra;
    }
}
