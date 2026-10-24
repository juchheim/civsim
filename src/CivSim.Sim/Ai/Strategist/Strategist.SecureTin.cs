using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    /// <summary>
    /// Secure tin is an E3 objective only after KEY-3 and the MET-05 fork exists. It reads the exact own era/research and
    /// own Tin access, while the decree policy separately ranks only forecast-legal recovery orders from known evidence.
    /// </summary>
    private Fix64 SecureTinPlanValue(StrategistView v)
    {
        int met03 = v.Catalog.IndexOf("MET-03");
        if (v.Own.Era < 3 || !Done(v, met03) || v.Own.TinAccess) return Fix64.Zero;
        return StrategistTuning.SecureTinBase;
    }
}
