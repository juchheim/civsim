using System;
using CivSim.Sim.Economy;

namespace CivSim.Sim.View;

/// <summary>
/// The Iron chapter's block of the frame (docs/MVP/25 §12): the works, staff and pledges of the settlements the viewer may see, and the sieges, blockades,
/// tributes, leagues and Wonders in play. Empty and <see cref="Active"/> false in any other chapter. Per-settlement arrays are indexed like the frame's
/// settlement block (<see cref="MatchFrame.SettlementIndex"/>); detail beyond the works counts is the viewer's own settlements only. Arrays only grow.
/// </summary>
public sealed class IronFrame
{
    public bool Active;
    /// <summary>Iron works kinds: the stride of <see cref="Works"/>.</summary>
    public int WorkKinds;

    // ---- per civilization: small-prop looks of the Iron Age that the 16 bits of MatchFrame.CivLooks have no room for ----
    public const int LookCamels = 1, LookRelays = 2, LookBarrels = 4, LookPlanks = 8, LookGlass = 16;
    /// <summary>One int per civilization: bits of <c>Look*</c> for the projects it has completed (HUS-09, COM-08, WOD-07, WOD-08, CER-07). Zero outside the Iron chapter.</summary>
    public int[] CivLooks = Array.Empty<int>();

    // ---- per settlement: actual stock behind settlement props, separate from the project looks above ----
    public const byte PropBarrelStock = 1, PropPlankStock = 2, PropBlownGlassStock = 4;
    /// <summary>Own settlements only: bit mask for local barrel, plank, and blown-glass stock; zero elsewhere.</summary>
    public byte[] PropGoods = Array.Empty<byte>();

    // ---- per settlement (frame index) ----
    /// <summary>Count of each Iron works kind in a settlement the viewer sees now (zero for one it only remembers).</summary>
    public byte[] Works = Array.Empty<byte>();
    /// <summary>Own settlements only: soldiers in post and fed, scholars, and missionaries.</summary>
    public byte[] Soldiers = Array.Empty<byte>(), Scholars = Array.Empty<byte>(), Missionaries = Array.Empty<byte>();
    /// <summary>Own settlements only: the frame index of the province seat that governs it (itself for a seat), -1 none; and 1 when it is under the feudal contract (GOV-09).</summary>
    public short[] ProvinceSeat = Array.Empty<short>();
    public byte[] Contract = Array.Empty<byte>();
    /// <summary>A settlement the viewer sees now: its forge, 0 none, 1 idle, 2 at work (<see cref="LivingEconomyModel.ForgeStateAt"/>).</summary>
    public byte[] Forge = Array.Empty<byte>();
    /// <summary>A town the viewer sees that is not its own: the share of its people who follow the viewer's faith, in permille (0 for own towns and in an omniscient frame).</summary>
    public short[] OurFaithPermille = Array.Empty<short>();
    /// <summary>Own settlements only: the strongest foreign faith among its people, in permille, and the civilization it belongs to (-1 none).</summary>
    public short[] ForeignFaithPermille = Array.Empty<short>();
    public sbyte[] ForeignFaithCiv = Array.Empty<sbyte>();
    /// <summary>Own settlements only: the Iron structure under construction (kind, -1 none) and its progress in permille.</summary>
    public sbyte[] BuildKind = Array.Empty<sbyte>();
    public short[] BuildPermille = Array.Empty<short>();
    /// <summary>Own settlements only: the settlement (frame-independent index) this town's build labor is pledged to this season, -1 none.</summary>
    public short[] PledgeTo = Array.Empty<short>();

    /// <summary>A settlement the viewer sees now: the civilizations its frontier wall segments face, one bit per civilization.</summary>
    public byte[] FacingMask = Array.Empty<byte>();

    // ---- terraces (AGR-08): the tiles of every terrace the viewer may see ----
    public int TerraceTileCount;
    public int[] TerraceTiles = Array.Empty<int>(), TerraceSettlement = Array.Empty<int>();

    // ---- paved roads (TRN-07) and their bridges (TRN-08): one road per settlement linked to its capital ----
    public int RoadCount;
    /// <summary>Frame index of the settlement the road runs to, and where its tiles start in <see cref="RoadTiles"/> and how many there are.</summary>
    public int[] RoadSettlement = Array.Empty<int>(), RoadStart = Array.Empty<int>(), RoadLength = Array.Empty<int>();
    /// <summary>1 when a bridge stands, so the road crosses a river tile on its line; 0 when the road stops at the river.</summary>
    public byte[] RoadBridged = Array.Empty<byte>();
    /// <summary>The road tiles from the capital to the settlement; -1 for a tile of another civilization's road that the viewer cannot see now.</summary>
    public int[] RoadTiles = Array.Empty<int>();
    public int RoadTileCount;

    // ---- sieges the viewer is party to or sees ----
    public int SiegeCount;
    public int[] SiegeSettlement = Array.Empty<int>(), SiegeBesieger = Array.Empty<int>(), SiegeEngines = Array.Empty<int>(), SiegeSeasons = Array.Empty<int>();
    /// <summary>1000 intact, 0 breached.</summary>
    public short[] SiegeIntegrityPermille = Array.Empty<short>();
    /// <summary>The tile of the besieger's home settlement when the viewer may know it (its own, or in sight), else -1: the direction the engine train comes from.</summary>
    public int[] SiegeOriginTile = Array.Empty<int>();

    // ---- blockades, tributes, leagues ----
    public int BlockadeCount;
    public int[] BlockadeHarbor = Array.Empty<int>(), BlockadeCiv = Array.Empty<int>(), BlockadeOrigin = Array.Empty<int>();
    public int TributeCount;
    public int[] TributeOverlord = Array.Empty<int>(), TributeSubject = Array.Empty<int>();
    public int LeagueCount;
    /// <summary>One bit per civilization for each league's members.</summary>
    public int[] LeagueMembers = Array.Empty<int>();

    // ---- Wonders (public once built): holder settlement, -1 none; in the order Great Library, Great Lighthouse, Great Dome ----
    public const int WonderKinds = 3;
    public readonly int[] WonderSettlement = { -1, -1, -1 };

    internal void EnsureSettlements(int n)
    {
        if (WorkKinds <= 0) return;
        if (Works.Length < n * WorkKinds) Works = new byte[Math.Max(n * WorkKinds, Works.Length * 2)];
        if (PropGoods.Length < n) Array.Resize(ref PropGoods, Math.Max(n, PropGoods.Length * 2));
        if (Soldiers.Length >= n) return;
        int size = Math.Max(n, Soldiers.Length * 2);
        Soldiers = new byte[size]; Scholars = new byte[size]; Missionaries = new byte[size]; OurFaithPermille = new short[size]; Forge = new byte[size]; FacingMask = new byte[size]; ProvinceSeat = new short[size]; Contract = new byte[size];
        ForeignFaithPermille = new short[size]; ForeignFaithCiv = new sbyte[size];
        BuildKind = new sbyte[size]; BuildPermille = new short[size]; PledgeTo = new short[size];
    }

    internal void EnsureTerraces(int n)
    {
        if (TerraceTiles.Length >= n) return;
        int size = Math.Max(n, TerraceTiles.Length * 2);
        Array.Resize(ref TerraceTiles, size); Array.Resize(ref TerraceSettlement, size);
    }

    internal void EnsureRoads(int n)
    {
        if (RoadSettlement.Length >= n) return;
        int size = Math.Max(n, RoadSettlement.Length * 2);
        RoadSettlement = new int[size]; RoadStart = new int[size]; RoadLength = new int[size]; RoadBridged = new byte[size];
    }

    internal void EnsureRoadTiles(int n)
    {
        if (RoadTiles.Length >= n) return;
        Array.Resize(ref RoadTiles, Math.Max(n, RoadTiles.Length * 2));
    }

    internal void EnsureSieges(int n)
    {
        if (SiegeSettlement.Length >= n) return;
        SiegeSettlement = new int[n]; SiegeBesieger = new int[n]; SiegeEngines = new int[n]; SiegeSeasons = new int[n]; SiegeIntegrityPermille = new short[n]; SiegeOriginTile = new int[n];
    }

    internal void EnsureBlockades(int n)
    {
        if (BlockadeHarbor.Length >= n) return;
        BlockadeHarbor = new int[n]; BlockadeCiv = new int[n]; BlockadeOrigin = new int[n];
    }

    internal void EnsureTributes(int n)
    {
        if (TributeOverlord.Length >= n) return;
        TributeOverlord = new int[n]; TributeSubject = new int[n];
    }

    internal void EnsureLeagues(int n)
    {
        if (LeagueMembers.Length < n) LeagueMembers = new int[n];
    }
}
