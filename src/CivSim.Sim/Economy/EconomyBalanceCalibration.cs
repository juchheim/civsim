using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>A calibrated mean-field cap effect from params.CAPS in the balance model.</summary>
internal readonly record struct EconomyCapCalibration(string ProjectId, Fix64 FoodMultiplier, Fix64 LeanMultiplier);

/// <summary>
/// E0–E2 Food/Lean cap steps used by C4. Optional-project effects are probability-weighted
/// as mult^probability in balance_sim.caps(); the values below are those weighted multipliers.
/// Live fission applies lean-season project effects to the settlement's world-derived FoodCap,
/// then scales that LeanFood threshold by observed winter security.
/// </summary>
internal static class EconomyBalanceCalibration
{
    internal static readonly EconomyCapCalibration[] E0ToE2FoodAndLeanCaps =
    {
        new("FOR-01", Fix64.Parse("1.10"), Fix64.One),
        new("HNT-01", Fix64.Parse("1.12"), Fix64.One),
        new("FOD-01", Fix64.Parse("1.10"), Fix64.One),
        new("FOR-02", Fix64.One, Fix64.Parse("1.10")),
        new("HNT-02", Fix64.Parse("1.04"), Fix64.One),
        new("FOR-03", Fix64.Parse("1.08"), Fix64.Parse("1.25")),
        new("FOD-02", Fix64.One, Fix64.Parse("1.15")),
        new("HNT-03", Fix64.Parse("1.04"), Fix64.One),
        new("WAY-02", Fix64.Parse("1.05"), Fix64.One),
        new("HNT-07", Fix64.Parse("1.058852852922"), Fix64.One), // 1.10^0.6
        new("FOR-04", Fix64.Parse("1.10"), Fix64.One),
        new("FOD-03", Fix64.One, Fix64.Parse("1.30")),
        new("HNT-04", Fix64.Parse("1.068993044916"), Fix64.One), // 1.10^0.7
        new("FOD-04", Fix64.Parse("1.05"), Fix64.One),
        new("FOR-05", Fix64.Parse("1.04"), Fix64.One),
        new("FOR-06", Fix64.Parse("1.06"), Fix64.One),
        new("AGR-01", Fix64.Parse("1.30"), Fix64.One),
        new("AGR-02", Fix64.Parse("1.22"), Fix64.One),
        new("FOD-05", Fix64.Parse("1.05"), Fix64.One),
        new("AGR-03", Fix64.Parse("1.039230484541"), Fix64.One), // 1.08^0.5
        new("HNT-09", Fix64.Parse("1.031263156552"), Fix64.One), // 1.08^0.4
        new("FOR-07", Fix64.Parse("1.031263156552"), Fix64.One), // 1.08^0.4
    };
}
