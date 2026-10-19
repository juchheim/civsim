using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
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
    /// <summary>Believed population, within about ±25% of the truth when seen.</summary>
    public int PopulationEstimate;
    /// <summary>Believed fighting-age people.</summary>
    public int FightersEstimate;
    public int WarriorsSeen;
    /// <summary>Believed strength of its defenders, before structures, in the units of docs/MVP/13 §7.1.</summary>
    public Fix64 DefendingStrengthEstimate;
    public bool ThornBarrierSeen, PalisadeSeen, WatchersSeen;
    /// <summary>Hungry or sick people were visible on the last sighting.</summary>
    public bool DistressSeen;
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
            foreach (Fix64 p in r.ArchetypePosterior) h.Add(p);
            foreach (bool b in r.ProjectBelieved) h.Add(b);
            h.Add(r.Settlements.Count);
            foreach (BeliefSettlement s in r.Settlements)
            {
                h.Add(s.Settlement); h.Add(s.Tile); h.Add(s.LastSeenSeason); h.Add(s.PopulationEstimate); h.Add(s.FightersEstimate);
                h.Add(s.WarriorsSeen); h.Add(s.DefendingStrengthEstimate); h.Add(s.ThornBarrierSeen); h.Add(s.PalisadeSeen);
                h.Add(s.WatchersSeen); h.Add(s.DistressSeen);
            }
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
            w.Write(r.ArchetypePosterior.Length);
            foreach (Fix64 p in r.ArchetypePosterior) w.Write(p.Raw);
            w.Write(r.ProjectBelieved.Length);
            foreach (bool b in r.ProjectBelieved) w.Write(b);
            w.Write(r.Settlements.Count);
            foreach (BeliefSettlement s in r.Settlements)
            {
                w.Write(s.Settlement); w.Write(s.Tile); w.Write(s.LastSeenSeason); w.Write(s.PopulationEstimate);
                w.Write(s.FightersEstimate); w.Write(s.WarriorsSeen); w.Write(s.DefendingStrengthEstimate.Raw);
                w.Write(s.ThornBarrierSeen); w.Write(s.PalisadeSeen); w.Write(s.WatchersSeen); w.Write(s.DistressSeen);
            }
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
                    Settlement = r0.ReadInt32(), Tile = r0.ReadInt32(), LastSeenSeason = r0.ReadInt32(), PopulationEstimate = r0.ReadInt32(),
                    FightersEstimate = r0.ReadInt32(), WarriorsSeen = r0.ReadInt32(), DefendingStrengthEstimate = Fix64.FromRaw(r0.ReadInt64()),
                    ThornBarrierSeen = r0.ReadBoolean(), PalisadeSeen = r0.ReadBoolean(), WatchersSeen = r0.ReadBoolean(), DistressSeen = r0.ReadBoolean(),
                });
        }
    }
}
