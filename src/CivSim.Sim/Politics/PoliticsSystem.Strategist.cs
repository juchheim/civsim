using CivSim.Sim.Core;

namespace CivSim.Sim.Politics;

public sealed partial class PoliticsSystem
{
    /// <summary>
    /// Posture Fortify on the defenses of a civilization's settlements (docs/MVP/05 §6.2, 13 §6: "guards ×2"). A modest
    /// stand-in until structures are posted by the planner: a fortifying civilization keeps watch, so its settlements count
    /// as watched (raid surprise ×0.3, 13 §5.3) and give the alarm (×0.8). Nothing changes under any other posture, so
    /// matches that never fortify keep their checksums.
    /// <para>Wiring (for the integrator): <c>DefensesOf</c> in PoliticsSystem.cs should return
    /// <c>WithPostureDefenses(civ, new Defenses(...))</c>. Until then only the Strategist's own Fortify rules apply
    /// (no raids, no expeditions under threat).</para>
    /// </summary>
    public Defenses WithPostureDefenses(int civ, in Defenses defenses)
    {
        if ((Posture)_posture[civ] != Posture.Fortify) return defenses;
        return defenses with { Watchers = true, AlarmCalls = true };
    }
}
