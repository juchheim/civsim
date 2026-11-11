using System;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>
/// Player-facing words for a village site refused because a valuable deposit lies in its core (docs/MVP/31 §5.1, §7.1). The rule uses
/// every deposit, found or not; the words name a deposit only when the viewer's frame already knows it, so a refusal never reveals a hidden
/// one beyond "poor ground".
/// </summary>
public static class SiteText
{
    /// <summary>
    /// The refusal for <c>Found a village</c> on <paramref name="site"/>: names the first deposit (lowest tile index) in the core that
    /// <paramref name="knownDeposits"/> marks as known, else the neutral "poor ground" sentence.
    /// </summary>
    public static string CoreDepositRefusal(WorldView world, ReadOnlySpan<byte> knownDeposits, int site)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if ((uint)site < (uint)world.TileCount)
        {
            int x = site % world.Width, y = site / world.Width, r = SettlementCoreRules.CoreRadius;
            for (int ny = Math.Max(0, y - r); ny <= Math.Min(world.Height - 1, y + r); ny++)
                for (int nx = Math.Max(0, x - r - 1); nx <= Math.Min(world.Width - 1, x + r + 1); nx++)
                {
                    int t = ny * world.Width + nx;
                    if (HexGrid.Distance(site, t, world.Width) > r || t >= knownDeposits.Length || knownDeposits[t] == 0) continue;
                    var kind = (DepositKind)world.Deposit[t];
                    if (!SettlementCoreRules.IsValuableCoreDeposit(kind)) continue;
                    return $"A village there would cover the known {DepositName(kind)} deposit. Pick a spot at least two tiles from it, so it can be mined.";
                }
        }
        return "The ground there is poor for a village. Try a spot nearby.";
    }

    private static string DepositName(DepositKind kind) => kind switch
    {
        DepositKind.NativeCopper => "native copper",
        DepositKind.CopperOre => "copper ore",
        DepositKind.ArsenicalCopper => "arsenical copper",
        _ => kind.ToString().ToLowerInvariant(),
    };
}
