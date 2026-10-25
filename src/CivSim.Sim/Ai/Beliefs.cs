using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// What one civilization believes about a rival settlement, from what its figures have seen (docs/MVP/14 §3.1).
/// Numbers are estimates with the season they were last true.
/// </summary>
public sealed class BeliefSettlement
{
    /// <summary>The settlement's index in the economy's list (a stable id).</summary>
    public int Settlement;
    public int Tile;
    public int LastSeenSeason;
    /// <summary>The season this settlement was first seen.</summary>
    public int FirstSeenSeason;
    /// <summary>Believed population, within about ±25% of the truth when seen.</summary>
    public int PopulationEstimate;
    /// <summary>Believed fighting-age people.</summary>
    public int FightersEstimate;
    public int WarriorsSeen;
    /// <summary>Believed strength of its defenders, before structures, in the units of docs/MVP/13 §7.1.</summary>
    public Fix64 DefendingStrengthEstimate;
    public bool ThornBarrierSeen, PalisadeSeen, WatchersSeen, HillfortSeen, StoneWallsSeen;
    /// <summary>Hungry or sick people were visible on the last sighting.</summary>
    public bool DistressSeen;
}

/// <summary>A party of another civilization seen on its way to one of our settlements (docs/MVP/13 §10 "raid incoming").</summary>
public sealed class IncomingParty
{
    public int Civ;
    public ExpeditionKind Kind;
    public int FromSettlement, TargetSettlement;
    public int ArriveSeason;
    /// <summary>Heads counted in the party.</summary>
    public int PartySize;
}

/// <summary>A rival caravan load last observed on a tile in sight.</summary>
public sealed class BeliefCaravan
{
    public int Civ, Id, Tile, LastSeenSeason, CompactId = -1, ArrivalSeason, CrewEstimate;
    public TradeGood Good;
    public Fix64 Cargo, CargoBarterValue;
}

/// <summary>A Tin Folk village identity and last sighting; it carries no hidden population or defense state.</summary>
public sealed class BeliefTinFolk
{
    public int Settlement, Tile, LastSeenSeason;
}

/// <summary>Bits of <see cref="RivalBelief.SeenFlags"/>: things ever seen of a rival.</summary>
public static class SeenFlag
{
    public const int ThornBarrier = 1, Palisade = 2, Watchers = 4, RangedWeapon = 8, Warriors = 16;
    /// <summary>Warpaint (Raid posture) on the latest sighting of its figures; cleared when seen without.</summary>
    public const int RaidPostureLast = 32;
    public const int Hillfort = 64, StoneWalls = 128;
    public const int CopperKit = 256, ArsenicalKit = 512, TinBronzeKit = 1024;
    public const int CopperMineCamp = 2048, ArsenicalMineCamp = 4096, TinMineCamp = 8192;
    public const int Scribes = 16384, Priests = 32768;
}

/// <summary>What one civilization believes about one rival (docs/MVP/14 §3.1, §5).</summary>
public sealed class RivalBelief
{
    public int Civ;
    public bool Aware, Contacted;
    public int FirstContactSeason = -1;
    /// <summary>The last season any of its people or structures were seen, -1 if never.</summary>
    public int LastSeenSeason = -1;
    /// <summary>The rival's era: public, announced with each Keystone.</summary>
    public int Era;
    public int PopulationLow, PopulationHigh;
    public readonly List<BeliefSettlement> Settlements = new();
    /// <summary>The minimum set of projects consistent with what was seen, plus public announcements (docs/MVP/14 §5.2).</summary>
    public bool[] ProjectBelieved = Array.Empty<bool>();
    /// <summary>Best weapon seen, in the units of docs/MVP/13 §7.1 (0.6 bare hands).</summary>
    public Fix64 WeaponStrength = Fix64.FromRatio(6, 10);
    public bool RangedSeen, WarriorsSeen, RaidPostureSeen;
    public int WarriorsEstimate;
    /// <summary>A Bayesian estimate over Builder, Grower, Wanderer, Warlord, Sage in that order (docs/MVP/14 §5.3); sums to one.</summary>
    public Fix64[] ArchetypePosterior = new Fix64[5];
    /// <summary>Public record: Firsts and Keystones announced for this civilization.</summary>
    public int KeystonesAnnounced, FirstsAnnounced;
    // Diplomacy is mutual knowledge, so these are exact.
    public int Relations, Reputation;
    public bool AtWar, Allied, InTruce;
    /// <summary>How sure the whole entry is: one right after a sighting, decaying with the seasons since (docs/MVP/14 §3.1).</summary>
    public Fix64 Confidence;
    /// <summary>Probability that this rival attacks within the planning horizon, and the loss if it does (docs/MVP/14 §5.4).</summary>
    public Fix64 ThreatProbability, ThreatLoss;
    /// <summary>How inviting this rival is to strike right now (14 §5.4): famine seen, warriors away, a gap closing.</summary>
    public Fix64 Opportunity;

    // ---- the observation record the estimates above are built from (docs/MVP/14 §5.3) ----------------
    /// <summary>Distinct settlements of this rival ever seen.</summary>
    public int SettlementsSeenEver;
    /// <summary>Tiles this rival owned among the tiles in sight this season, and the most ever counted.</summary>
    public int ClaimTilesSeen, ClaimTilesSeenMax;
    /// <summary>Bit k set when a structure of economy kind k was seen at one of its settlements.</summary>
    public ulong StructureKindsSeen;
    /// <summary>What was ever seen of them, as <see cref="SeenFlag"/> bits: defenses, weapons, tools, roles and mining camps.</summary>
    public int SeenFlags;
    /// <summary>The most warriors ever seen at once.</summary>
    public int WarriorsSeenMax;
    /// <summary>Raids it made on our settlements (mutual knowledge) and the season of the last one (-1000 none).</summary>
    public int RaidsOnUs, LastRaidOnUsSeason = -1000;
    /// <summary>The season its last gift reached us (-1000 none).</summary>
    public int LastGiftFromSeason = -1000;
    /// <summary>Its people seen away on raids or assaults this season (warriors away, 14 §5.4).</summary>
    public int PartiesAwaySeen;
    /// <summary>Observed caravans, keyed by stable party id; cargo is only recorded when the party tile was in sight.</summary>
    public readonly List<BeliefCaravan> CaravansSeen = new();
    /// <summary>A rival was observed carrying tin in a caravan.</summary>
    public bool TinAccessSeen;
}

/// <summary>
/// The map as one civilization knows it. Terrain and ownership are only readable where the civilization has been:
/// <see cref="TryTile"/> refuses unexplored tiles, which is the fog.
/// </summary>
public sealed class MapKnowledge
{
    private readonly WorldTile[] _tiles;
    private readonly bool[] _explored;
    private readonly sbyte[] _ownerSeen;
    private readonly short[] _ownerSeenSeason;

    public MapKnowledge(WorldTile[] tiles, int width, int height)
    {
        _tiles = tiles;
        Width = width;
        Height = height;
        _explored = new bool[tiles.Length];
        _ownerSeen = new sbyte[tiles.Length];
        Array.Fill(_ownerSeen, (sbyte)-1);
        _ownerSeenSeason = new short[tiles.Length];
        Array.Fill(_ownerSeenSeason, (short)-1);
    }

    public int Width { get; }
    public int Height { get; }
    public int TileCount => _tiles.Length;
    public bool IsExplored(int tile) => _explored[tile];

    /// <summary>The tile's terrain and deposit, only if the civilization has seen it.</summary>
    public bool TryTile(int tile, out WorldTile value)
    {
        if (_explored[tile]) { value = _tiles[tile]; return true; }
        value = default;
        return false;
    }

    /// <summary>Who owned the tile the last time it was seen (-1 unowned or never seen) and in which season (-1 never).</summary>
    public (int Owner, int Season) OwnerSeen(int tile) => (_ownerSeen[tile], _ownerSeenSeason[tile]);

    // Written by the belief builder only.
    public void MarkExplored(int tile) => _explored[tile] = true;
    public void RecordOwner(int tile, int owner, int season) { _ownerSeen[tile] = (sbyte)owner; _ownerSeenSeason[tile] = (short)season; }

    public void Hash(ref StateHasher h)
    {
        for (int i = 0; i < _explored.Length; i++)
            if (_explored[i]) { h.Add(i); h.Add((int)_ownerSeen[i]); h.Add((int)_ownerSeenSeason[i]); }
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_explored.Length);
        for (int i = 0; i < _explored.Length; i++) { w.Write(_explored[i]); w.Write(_ownerSeen[i]); w.Write(_ownerSeenSeason[i]); }
    }

    public void Read(BinaryReader r)
    {
        if (r.ReadInt32() != _explored.Length) throw new InvalidDataException("Map knowledge does not match this world.");
        for (int i = 0; i < _explored.Length; i++) { _explored[i] = r.ReadBoolean(); _ownerSeen[i] = r.ReadSByte(); _ownerSeenSeason[i] = r.ReadInt16(); }
    }
}

/// <summary>Everything one civilization believes about the world beyond its own people (docs/MVP/14 §3.1).</summary>
public sealed class Belief
{
    public Belief(int civ, int civCount, int projectCount, MapKnowledge map)
    {
        Civ = civ;
        Map = map;
        Rivals = new RivalBelief[civCount];
        for (int c = 0; c < civCount; c++)
            Rivals[c] = new RivalBelief { Civ = c, ProjectBelieved = new bool[projectCount] };
    }

    public int Civ { get; }
    public int Season { get; set; }
    public RivalBelief[] Rivals { get; }
    public MapKnowledge Map { get; }
    /// <summary>Tin Folk villages that have been directly sighted, retained as stale knowledge.</summary>
    public List<BeliefTinFolk> TinFolkSeen { get; } = new();
    /// <summary>Parties of other civilizations seen on their way to our settlements this season.</summary>
    public List<IncomingParty> IncomingParties { get; } = new();

    /// <summary>
    /// Hashes, writes and reads every field above. Whoever adds a field to the belief types adds it here too, or a resumed
    /// match will diverge (the snapshot tests catch it).
    /// </summary>
    public void Hash(ref StateHasher h)
    {
        h.Add(Season);
        Map.Hash(ref h);
        foreach (RivalBelief r in Rivals)
        {
            h.Add(r.Civ); h.Add(r.Aware); h.Add(r.Contacted); h.Add(r.FirstContactSeason); h.Add(r.LastSeenSeason); h.Add(r.Era);
            h.Add(r.PopulationLow); h.Add(r.PopulationHigh); h.Add(r.WeaponStrength); h.Add(r.RangedSeen); h.Add(r.WarriorsSeen);
            h.Add(r.RaidPostureSeen); h.Add(r.WarriorsEstimate); h.Add(r.KeystonesAnnounced); h.Add(r.FirstsAnnounced);
            h.Add(r.Relations); h.Add(r.Reputation); h.Add(r.AtWar); h.Add(r.Allied); h.Add(r.InTruce);
            h.Add(r.Confidence); h.Add(r.ThreatProbability); h.Add(r.ThreatLoss); h.Add(r.Opportunity);
            h.Add(r.SettlementsSeenEver); h.Add(r.ClaimTilesSeen); h.Add(r.ClaimTilesSeenMax); h.Add(r.StructureKindsSeen);
            h.Add(r.SeenFlags); h.Add(r.WarriorsSeenMax); h.Add(r.RaidsOnUs); h.Add(r.LastRaidOnUsSeason);
            h.Add(r.LastGiftFromSeason); h.Add(r.PartiesAwaySeen);
            h.Add(r.TinAccessSeen); h.Add(r.CaravansSeen.Count);
            foreach (BeliefCaravan c in r.CaravansSeen)
            { h.Add(c.Civ); h.Add(c.Id); h.Add(c.Tile); h.Add(c.LastSeenSeason); h.Add(c.CompactId); h.Add((int)c.Good); h.Add(c.Cargo); h.Add(c.ArrivalSeason); h.Add(c.CrewEstimate); h.Add(c.CargoBarterValue); }
            foreach (Fix64 p in r.ArchetypePosterior) h.Add(p);
            foreach (bool b in r.ProjectBelieved) h.Add(b);
            h.Add(r.Settlements.Count);
            foreach (BeliefSettlement s in r.Settlements)
            {
                h.Add(s.Settlement); h.Add(s.Tile); h.Add(s.LastSeenSeason); h.Add(s.FirstSeenSeason); h.Add(s.PopulationEstimate);
                h.Add(s.FightersEstimate); h.Add(s.WarriorsSeen); h.Add(s.DefendingStrengthEstimate); h.Add(s.ThornBarrierSeen);
                h.Add(s.PalisadeSeen); h.Add(s.WatchersSeen); h.Add(s.HillfortSeen); h.Add(s.StoneWallsSeen); h.Add(s.DistressSeen);
            }
        }
        h.Add(TinFolkSeen.Count);
        foreach (BeliefTinFolk f in TinFolkSeen) { h.Add(f.Settlement); h.Add(f.Tile); h.Add(f.LastSeenSeason); }
        h.Add(IncomingParties.Count);
        foreach (IncomingParty p in IncomingParties)
        {
            h.Add(p.Civ); h.Add((int)p.Kind); h.Add(p.FromSettlement); h.Add(p.TargetSettlement); h.Add(p.ArriveSeason); h.Add(p.PartySize);
        }
    }

    public void Write(BinaryWriter w)
    {
        w.Write(Season);
        Map.Write(w);
        foreach (RivalBelief r in Rivals)
        {
            w.Write(r.Aware); w.Write(r.Contacted); w.Write(r.FirstContactSeason); w.Write(r.LastSeenSeason); w.Write(r.Era);
            w.Write(r.PopulationLow); w.Write(r.PopulationHigh); w.Write(r.WeaponStrength.Raw); w.Write(r.RangedSeen);
            w.Write(r.WarriorsSeen); w.Write(r.RaidPostureSeen); w.Write(r.WarriorsEstimate); w.Write(r.KeystonesAnnounced);
            w.Write(r.FirstsAnnounced); w.Write(r.Relations); w.Write(r.Reputation); w.Write(r.AtWar); w.Write(r.Allied);
            w.Write(r.InTruce); w.Write(r.Confidence.Raw); w.Write(r.ThreatProbability.Raw); w.Write(r.ThreatLoss.Raw);
            w.Write(r.Opportunity.Raw);
            w.Write(r.SettlementsSeenEver); w.Write(r.ClaimTilesSeen); w.Write(r.ClaimTilesSeenMax); w.Write(r.StructureKindsSeen);
            w.Write(r.SeenFlags); w.Write(r.WarriorsSeenMax); w.Write(r.RaidsOnUs); w.Write(r.LastRaidOnUsSeason);
            w.Write(r.LastGiftFromSeason); w.Write(r.PartiesAwaySeen);
            w.Write(r.TinAccessSeen); w.Write(r.CaravansSeen.Count);
            foreach (BeliefCaravan c in r.CaravansSeen)
            { w.Write(c.Civ); w.Write(c.Id); w.Write(c.Tile); w.Write(c.LastSeenSeason); w.Write(c.CompactId); w.Write((byte)c.Good); w.Write(c.Cargo.Raw); w.Write(c.ArrivalSeason); w.Write(c.CrewEstimate); w.Write(c.CargoBarterValue.Raw); }
            w.Write(r.ArchetypePosterior.Length);
            foreach (Fix64 p in r.ArchetypePosterior) w.Write(p.Raw);
            w.Write(r.ProjectBelieved.Length);
            foreach (bool b in r.ProjectBelieved) w.Write(b);
            w.Write(r.Settlements.Count);
            foreach (BeliefSettlement s in r.Settlements)
            {
                w.Write(s.Settlement); w.Write(s.Tile); w.Write(s.LastSeenSeason); w.Write(s.FirstSeenSeason); w.Write(s.PopulationEstimate);
                w.Write(s.FightersEstimate); w.Write(s.WarriorsSeen); w.Write(s.DefendingStrengthEstimate.Raw);
                w.Write(s.ThornBarrierSeen); w.Write(s.PalisadeSeen); w.Write(s.WatchersSeen);
                w.Write(s.HillfortSeen); w.Write(s.StoneWallsSeen); w.Write(s.DistressSeen);
            }
        }
        w.Write(TinFolkSeen.Count);
        foreach (BeliefTinFolk f in TinFolkSeen) { w.Write(f.Settlement); w.Write(f.Tile); w.Write(f.LastSeenSeason); }
        w.Write(IncomingParties.Count);
        foreach (IncomingParty p in IncomingParties)
        {
            w.Write(p.Civ); w.Write((byte)p.Kind); w.Write(p.FromSettlement); w.Write(p.TargetSettlement); w.Write(p.ArriveSeason); w.Write(p.PartySize);
        }
    }

    public void Read(BinaryReader r0)
    {
        Season = r0.ReadInt32();
        Map.Read(r0);
        foreach (RivalBelief r in Rivals)
        {
            r.Aware = r0.ReadBoolean(); r.Contacted = r0.ReadBoolean(); r.FirstContactSeason = r0.ReadInt32();
            r.LastSeenSeason = r0.ReadInt32(); r.Era = r0.ReadInt32(); r.PopulationLow = r0.ReadInt32();
            r.PopulationHigh = r0.ReadInt32(); r.WeaponStrength = Fix64.FromRaw(r0.ReadInt64()); r.RangedSeen = r0.ReadBoolean();
            r.WarriorsSeen = r0.ReadBoolean(); r.RaidPostureSeen = r0.ReadBoolean(); r.WarriorsEstimate = r0.ReadInt32();
            r.KeystonesAnnounced = r0.ReadInt32(); r.FirstsAnnounced = r0.ReadInt32(); r.Relations = r0.ReadInt32();
            r.Reputation = r0.ReadInt32(); r.AtWar = r0.ReadBoolean(); r.Allied = r0.ReadBoolean(); r.InTruce = r0.ReadBoolean();
            r.Confidence = Fix64.FromRaw(r0.ReadInt64()); r.ThreatProbability = Fix64.FromRaw(r0.ReadInt64());
            r.ThreatLoss = Fix64.FromRaw(r0.ReadInt64()); r.Opportunity = Fix64.FromRaw(r0.ReadInt64());
            r.SettlementsSeenEver = r0.ReadInt32(); r.ClaimTilesSeen = r0.ReadInt32(); r.ClaimTilesSeenMax = r0.ReadInt32();
            r.StructureKindsSeen = r0.ReadUInt64(); r.SeenFlags = r0.ReadInt32(); r.WarriorsSeenMax = r0.ReadInt32();
            r.RaidsOnUs = r0.ReadInt32(); r.LastRaidOnUsSeason = r0.ReadInt32(); r.LastGiftFromSeason = r0.ReadInt32();
            r.PartiesAwaySeen = r0.ReadInt32();
            r.TinAccessSeen = r0.ReadBoolean(); r.CaravansSeen.Clear();
            int caravans = r0.ReadInt32();
            for (int i = 0; i < caravans; i++) r.CaravansSeen.Add(new BeliefCaravan
            {
                Civ = r0.ReadInt32(), Id = r0.ReadInt32(), Tile = r0.ReadInt32(), LastSeenSeason = r0.ReadInt32(),
                CompactId = r0.ReadInt32(), Good = (TradeGood)r0.ReadByte(), Cargo = Fix64.FromRaw(r0.ReadInt64()),
                ArrivalSeason = r0.ReadInt32(), CrewEstimate = r0.ReadInt32(), CargoBarterValue = Fix64.FromRaw(r0.ReadInt64()),
            });
            int posterior = r0.ReadInt32();
            r.ArchetypePosterior = new Fix64[posterior];
            for (int i = 0; i < posterior; i++) r.ArchetypePosterior[i] = Fix64.FromRaw(r0.ReadInt64());
            int projects = r0.ReadInt32();
            r.ProjectBelieved = new bool[projects];
            for (int i = 0; i < projects; i++) r.ProjectBelieved[i] = r0.ReadBoolean();
            r.Settlements.Clear();
            int count = r0.ReadInt32();
            for (int i = 0; i < count; i++)
                r.Settlements.Add(new BeliefSettlement
                {
                    Settlement = r0.ReadInt32(), Tile = r0.ReadInt32(), LastSeenSeason = r0.ReadInt32(), FirstSeenSeason = r0.ReadInt32(),
                    PopulationEstimate = r0.ReadInt32(), FightersEstimate = r0.ReadInt32(), WarriorsSeen = r0.ReadInt32(),
                    DefendingStrengthEstimate = Fix64.FromRaw(r0.ReadInt64()), ThornBarrierSeen = r0.ReadBoolean(),
                    PalisadeSeen = r0.ReadBoolean(), WatchersSeen = r0.ReadBoolean(), HillfortSeen = r0.ReadBoolean(),
                    StoneWallsSeen = r0.ReadBoolean(), DistressSeen = r0.ReadBoolean(),
                });
        }
        TinFolkSeen.Clear();
        int folkCount = r0.ReadInt32();
        for (int i = 0; i < folkCount; i++) TinFolkSeen.Add(new BeliefTinFolk
        { Settlement = r0.ReadInt32(), Tile = r0.ReadInt32(), LastSeenSeason = r0.ReadInt32() });
        IncomingParties.Clear();
        int parties = r0.ReadInt32();
        for (int i = 0; i < parties; i++)
            IncomingParties.Add(new IncomingParty
            {
                Civ = r0.ReadInt32(), Kind = (ExpeditionKind)r0.ReadByte(), FromSettlement = r0.ReadInt32(),
                TargetSettlement = r0.ReadInt32(), ArriveSeason = r0.ReadInt32(), PartySize = r0.ReadInt32(),
            });
    }
}
