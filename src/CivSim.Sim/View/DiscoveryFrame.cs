using System;

namespace CivSim.Sim.View;

/// <summary>
/// Read-only Discovery-chapter presentation data (docs/MVP/38 §8; M13 WP-E6-11 slice 1). Per-settlement values are aligned with the MatchFrame
/// settlement block and filled only for the viewer's own towns; the civilization block is the viewer's own. Inactive outside a Discovery match.
/// Quantities are raw Q32.32.
/// </summary>
public sealed class DiscoveryFrame
{
    public bool Active;
    public long[] CoalLitres = Array.Empty<long>(), FactoryCoverage = Array.Empty<long>(), Soot = Array.Empty<long>();
    public byte[] Engines = Array.Empty<byte>(), Ovens = Array.Empty<byte>(), Operatives = Array.Empty<byte>(), Engineers = Array.Empty<byte>();
    /// <summary>1 where the thing stands or works: canal, factory, colony (Colony is a state instead: 0 none, 1 content, 2 strained, 3 blockaded), staffed garden, learned society, bastion fort, crewed galleon, exchange house.</summary>
    public byte[] Canal = Array.Empty<byte>(), Factory = Array.Empty<byte>(), Colony = Array.Empty<byte>(), GardenStaffed = Array.Empty<byte>(),
        Society = Array.Empty<byte>(), Bastion = Array.Empty<byte>(), GalleonCrewed = Array.Empty<byte>(), ExchangeHouse = Array.Empty<byte>();
    /// <summary>1 where the town sends an offshore fishing fleet to sea (HNT-12, a harbor): a lugger lies at its quay and a bank shows at sea (docs/MVP/39 §5, §7.1).</summary>
    public byte[] Fleet = Array.Empty<byte>();

    /// <summary>The viewer's own civilization: ocean reach, a staffed exchange, joint-stock and insurance open, a staffed society, colonies held, KEY-7 progress in permille.</summary>
    public bool OceanReach, ExchangeStaffed, JointStockOpen, InsuranceOpen, SocietyStaffed;
    /// <summary>The viewer's realm holds a marine chronometer (a navigator stands on its quays) and a telescope (a brass dome beside its learned society).</summary>
    public bool ChronometerHeld, TelescopeHeld;
    /// <summary>Bit <c>c</c> set when the viewer has a resident embassy (DIP-09) with civilization <c>c</c>: a flag in its colour flies over the viewer's capital.</summary>
    public int EmbassyMask;
    /// <summary>
    /// Ocean lanes (docs/MVP/39 §7.2): the visible trade routes whose trip crosses deep ocean, which only a SEA-12 ship can sail. Tiles of the two harbor towns, the seller's
    /// civilization and the route's volume in permille. Taken from the frame's own route list, so a lane is listed only where the route is, and never reveals land.
    /// </summary>
    public int LaneCount;
    public int[] LaneFromTile = Array.Empty<int>(), LaneToTile = Array.Empty<int>(), LaneVolume = Array.Empty<int>();
    public sbyte[] LaneCiv = Array.Empty<sbyte>();
    /// <summary>The viewer's capital's tile (where a colony's tether leads), -1 with none.</summary>
    public int CapitalTile = -1;
    public int Colonies;
    public short Key7Permille;

    internal void EnsureLanes(int n)
    {
        if (LaneFromTile.Length >= n) return;
        int size = Math.Max(n, Math.Max(4, LaneFromTile.Length * 2));
        LaneFromTile = new int[size]; LaneToTile = new int[size]; LaneVolume = new int[size]; LaneCiv = new sbyte[size];
    }

    internal void EnsureSettlements(int n)
    {
        if (CoalLitres.Length >= n) return;
        int size = Math.Max(n, Math.Max(4, CoalLitres.Length * 2));
        CoalLitres = new long[size]; FactoryCoverage = new long[size]; Soot = new long[size];
        Engines = new byte[size]; Ovens = new byte[size]; Operatives = new byte[size]; Engineers = new byte[size];
        Canal = new byte[size]; Factory = new byte[size]; Colony = new byte[size]; GardenStaffed = new byte[size];
        Society = new byte[size]; Bastion = new byte[size]; GalleonCrewed = new byte[size]; ExchangeHouse = new byte[size]; Fleet = new byte[size];
    }
}
