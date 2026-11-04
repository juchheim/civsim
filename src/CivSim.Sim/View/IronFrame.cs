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

    // ---- per settlement (frame index) ----
    /// <summary>Count of each Iron works kind in a settlement the viewer sees now (zero for one it only remembers).</summary>
    public byte[] Works = Array.Empty<byte>();
    /// <summary>Own settlements only: soldiers in post and fed, scholars, and missionaries.</summary>
    public byte[] Soldiers = Array.Empty<byte>(), Scholars = Array.Empty<byte>(), Missionaries = Array.Empty<byte>();
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

    // ---- sieges the viewer is party to or sees ----
    public int SiegeCount;
    public int[] SiegeSettlement = Array.Empty<int>(), SiegeBesieger = Array.Empty<int>(), SiegeEngines = Array.Empty<int>(), SiegeSeasons = Array.Empty<int>();
    /// <summary>1000 intact, 0 breached.</summary>
    public short[] SiegeIntegrityPermille = Array.Empty<short>();

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
        if (Soldiers.Length >= n) return;
        int size = Math.Max(n, Soldiers.Length * 2);
        Soldiers = new byte[size]; Scholars = new byte[size]; Missionaries = new byte[size]; OurFaithPermille = new short[size];
        ForeignFaithPermille = new short[size]; ForeignFaithCiv = new sbyte[size];
        BuildKind = new sbyte[size]; BuildPermille = new short[size]; PledgeTo = new short[size];
    }

    internal void EnsureSieges(int n)
    {
        if (SiegeSettlement.Length >= n) return;
        SiegeSettlement = new int[n]; SiegeBesieger = new int[n]; SiegeEngines = new int[n]; SiegeSeasons = new int[n]; SiegeIntegrityPermille = new short[n];
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
