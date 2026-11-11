namespace CivSim.Sim.World;

/// <summary>
/// What a settlement's reserved core may hold (docs/MVP/31 §3.1, §7.1): the site tile and ring 1 never cover a valuable deposit, so a
/// mine is never buried under a village. Shared by world generation (start repairs) and every way the economy founds a settlement.
/// </summary>
public static class SettlementCoreRules
{
    /// <summary>The core radius of the standard rules: the centre hex and ring 1 (content <c>SettlementCoreRadius</c>).</summary>
    public const int CoreRadius = 1;

    /// <summary>
    /// Metal, salt and coal: the deposits a new settlement's core must not hold, discovered or not. Stone, flint, clay and ochre are
    /// pooled surface stock and stay allowed; later kinds (saltpeter, sulfur) are appended here when their era arrives.
    /// </summary>
    public static bool IsValuableCoreDeposit(DepositKind kind) => kind is DepositKind.NativeCopper or DepositKind.CopperOre
        or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or DepositKind.Silver or DepositKind.Iron
        or DepositKind.Salt or DepositKind.Coal;
}
