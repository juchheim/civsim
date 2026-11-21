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
    public byte[] Engines = Array.Empty<byte>(), Ovens = Array.Empty<byte>(), Operatives = Array.Empty<byte>();
    /// <summary>1 where the thing stands or works: canal, factory, colony, staffed garden, learned society, bastion fort, crewed galleon, exchange house.</summary>
    public byte[] Canal = Array.Empty<byte>(), Factory = Array.Empty<byte>(), Colony = Array.Empty<byte>(), GardenStaffed = Array.Empty<byte>(),
        Society = Array.Empty<byte>(), Bastion = Array.Empty<byte>(), GalleonCrewed = Array.Empty<byte>(), ExchangeHouse = Array.Empty<byte>();

    /// <summary>The viewer's own civilization: ocean reach, a staffed exchange, joint-stock and insurance open, a staffed society, colonies held, KEY-7 progress in permille.</summary>
    public bool OceanReach, ExchangeStaffed, JointStockOpen, InsuranceOpen, SocietyStaffed;
    public int Colonies;
    public short Key7Permille;

    internal void EnsureSettlements(int n)
    {
        if (CoalLitres.Length >= n) return;
        int size = Math.Max(n, Math.Max(4, CoalLitres.Length * 2));
        CoalLitres = new long[size]; FactoryCoverage = new long[size]; Soot = new long[size];
        Engines = new byte[size]; Ovens = new byte[size]; Operatives = new byte[size];
        Canal = new byte[size]; Factory = new byte[size]; Colony = new byte[size]; GardenStaffed = new byte[size];
        Society = new byte[size]; Bastion = new byte[size]; GalleonCrewed = new byte[size]; ExchangeHouse = new byte[size];
    }
}
