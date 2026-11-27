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
    /// <summary>1 where the thing stands or works: canal, factory, colony (Colony is a state instead: 0 none, 1 content, 2 strained, 3 blockaded, 4 independent), staffed garden, learned society, bastion fort, crewed galleon, exchange house.</summary>
    public byte[] Canal = Array.Empty<byte>(), Factory = Array.Empty<byte>(), Colony = Array.Empty<byte>(), GardenStaffed = Array.Empty<byte>(),
        Society = Array.Empty<byte>(), Bastion = Array.Empty<byte>(), GalleonCrewed = Array.Empty<byte>(), ExchangeHouse = Array.Empty<byte>();
    /// <summary>1 where the town sends an offshore fishing fleet to sea (HNT-12, a harbor): a lugger lies at its quay and a bank shows at sea (docs/MVP/39 §5, §7.1).</summary>
    public byte[] Fleet = Array.Empty<byte>();
    /// <summary>Per own town: which E6 goods it holds a stock of (a mask of <c>Cargo*</c>), drawn as crates at the works and the quay (docs/MVP/39 §7.4). Only what the town really holds.</summary>
    public ushort[] Cargo = Array.Empty<ushort>();
    /// <summary>Per own town: the imported staple its fields grow in the Columbian Exchange (FOR-10 with overseas contact): 0 none, 1 maize (warm and temperate lowlands), 2 potato (cool uplands).</summary>
    public byte[] Crop = Array.Empty<byte>();
    public const ushort CargoCoal = 1, CargoCoke = 2, CargoGlass = 4, CargoParts = 8, CargoAcid = 16, CargoAlkali = 32, CargoPamphlets = 64, CargoInstruments = 128, CargoSugar = 256, CargoCod = 512;

    /// <summary>
    /// The state of each Discovery work of the viewer's own towns (docs/MVP/38 D-T8, docs/MVP/39 §8.1), <see cref="WorkKinds"/> bytes per settlement row: row <c>w</c>, work <c>k</c>
    /// at <c>w * WorkKinds + k</c>. A byte packs the state in its low three bits (0 none, 1 working, 2 queued, 3 dormant, 4 blocked) and the reason above them
    /// (<see cref="Economy.DiscoveryWorkReason"/>). <see cref="Flooded"/> is 1 where the town's coal seam is deep and no engine pumps it; <see cref="Pumping"/> where one does.
    /// </summary>
    public const int WorkKinds = 8, WorkMine = 0, WorkEngine = 1, WorkOven = 2, WorkFactory = 3, WorkGarden = 4, WorkSociety = 5, WorkExchange = 6, WorkGalleon = 7;
    public byte[] Work = Array.Empty<byte>(), Flooded = Array.Empty<byte>(), Pumping = Array.Empty<byte>();
    public static byte PackWork(Economy.DiscoveryWork w) => (byte)((int)w.State | (int)w.Reason << 3);
    public static Economy.DiscoveryWorkState WorkStateOf(byte packed) => (Economy.DiscoveryWorkState)(packed & 7);
    public static Economy.DiscoveryWorkReason WorkReasonOf(byte packed) => (Economy.DiscoveryWorkReason)(packed >> 3);

    /// <summary>
    /// Wrecks (docs/MVP/39 §7.1): the viewer's voyages lost in the last season, a transient marker at the tile of the loss. The age is in ticks since the loss.
    /// </summary>
    public const int MaxWrecks = 16;
    public int WreckCount;
    public int[] WreckTile = new int[MaxWrecks], WreckAge = new int[MaxWrecks];
    /// <summary>The viewer's worked deep coal pits (docs/MVP/39 §4.1): the tile of each and its state, 1 flooded (no engine pumps it), 2 pumped.</summary>
    public const int MaxPits = 32;
    public int PitCount;
    public int[] PitTile = new int[MaxPits];
    public byte[] PitState = new byte[MaxPits];
    /// <summary>Former colonies of the viewer that went independent (their <see cref="Colony"/> state is 4).</summary>
    public int Independent;

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
        Society = new byte[size]; Bastion = new byte[size]; GalleonCrewed = new byte[size]; ExchangeHouse = new byte[size]; Fleet = new byte[size]; Cargo = new ushort[size]; Crop = new byte[size];
        Work = new byte[size * WorkKinds]; Flooded = new byte[size]; Pumping = new byte[size];
    }
}
