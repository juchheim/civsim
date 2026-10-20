using System;
using System.IO;

namespace CivSim.Sim.View;

/// <summary>
/// Saving the builder's memory (docs/MVP/18 §4.3): what the viewer has explored, the owners and settlements it last saw,
/// the event log and the baselines events are derived from. It is presentation state, kept out of the simulation
/// snapshot so checksums and the replay format do not change; a save file stores it next to the snapshot.
/// </summary>
public sealed partial class FrameBuilder
{
    private const uint StateMagic = 0x57454956; // "VIEW"
    private const int StateVersion = 1;

    public void WriteState(BinaryWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.Write(StateMagic);
        w.Write(StateVersion);
        w.Write(_tileCount);
        w.Write(_viewer);
        w.Write(_frameSequence);
        w.Write(_explored);
        for (int t = 0; t < _tileCount; t++) w.Write(_rememberedOwner[t]);
        w.Write(_seenSettlements);
        for (int s = 0; s < _seenSettlements; s++)
        {
            w.Write(_seenSeason[s]); w.Write(_seenCiv[s]); w.Write(_seenPopulation[s]); w.Write(_seenFree[s]);
        }

        w.Write(_lastSequence);
        w.Write(_ringCount);
        for (int i = 0; i < _ringCount; i++) WriteEvent(w, _ring[(_ringStart + i) % _ring.Length]);
        w.Write(_eventsPrimed);
        w.Write(_lastSeason);
        w.Write(_maxFigureId);
        int kinds = MatchFrame.StructureKinds;
        w.Write(_prevSettlements);
        w.Write(kinds);
        for (int s = 0; s < _prevSettlements; s++)
        {
            w.Write(_prevFire[s]); w.Write(_prevFood[s]); w.Write(_prevForecast[s]);
            w.Write(_prevStructures, s * kinds, kinds);
        }
        w.Write(_announcedCount);
        for (int i = 0; i < _announcedCount; i++) { w.Write(_announcedKey[i]); w.Write(_announcedArrive[i]); }
        // The claim layer: the simulation snapshot keeps owners but not the claim field (it is rebuilt at each season
        // end), so a loaded match would show no claim strength until then without this copy.
        w.Write(_claimSeason);
        if (_claimSeason >= 0)
            for (int t = 0; t < _tileCount; t++) { w.Write(_topClaim[t]); w.Write(_topCiv[t]); w.Write(_contested[t]); }
    }

    /// <summary>
    /// Restores memory written by <see cref="WriteState"/> for the same world. Call it right after the match itself was
    /// restored from its snapshot: the systems' event lists start empty then, and the builder reads them from the start.
    /// </summary>
    public void ReadState(BinaryReader r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (r.ReadUInt32() != StateMagic || r.ReadInt32() != StateVersion) throw new InvalidDataException("Unsupported view state.");
        if (r.ReadInt32() != _tileCount) throw new InvalidDataException("View state is for another map.");
        int viewer = r.ReadInt32();
        if (viewer < -1 || viewer >= _civs) throw new InvalidDataException("View state viewer out of range.");
        _viewer = viewer;
        _frameSequence = r.ReadInt64();
        byte[] explored = r.ReadBytes(_tileCount);
        if (explored.Length != _tileCount) throw new InvalidDataException("Truncated view state.");
        Buffer.BlockCopy(explored, 0, _explored, 0, _tileCount);
        for (int t = 0; t < _tileCount; t++) _rememberedOwner[t] = r.ReadSByte();
        int seen = r.ReadInt32();
        if (seen < 0 || seen > 1_000_000) throw new InvalidDataException("Bad view state.");
        EnsureSeen(seen);
        _seenSettlements = seen;
        for (int s = 0; s < seen; s++)
        {
            _seenSeason[s] = r.ReadInt32(); _seenCiv[s] = r.ReadSByte(); _seenPopulation[s] = r.ReadInt32(); _seenFree[s] = r.ReadBoolean();
        }

        _lastSequence = r.ReadInt64();
        int count = r.ReadInt32();
        if (count < 0 || count > 1_000_000) throw new InvalidDataException("Bad view state.");
        _ringStart = 0;
        _ringCount = 0;
        for (int i = 0; i < count; i++)
        {
            ViewEvent e = ReadEvent(r);
            if (_ringCount < _ring.Length) _ring[_ringCount++] = e;
            else { _ring[_ringStart] = e; _ringStart = (_ringStart + 1) % _ring.Length; }
        }
        _eventsPrimed = r.ReadBoolean();
        _lastSeason = r.ReadInt32();
        _maxFigureId = r.ReadInt64();
        int settlements = r.ReadInt32();
        int kinds = r.ReadInt32();
        if (settlements < 0 || settlements > 1_000_000 || kinds != MatchFrame.StructureKinds) throw new InvalidDataException("Bad view state.");
        if (_prevFire.Length < settlements)
        {
            Array.Resize(ref _prevFire, settlements); Array.Resize(ref _prevFood, settlements); Array.Resize(ref _prevForecast, settlements);
            Array.Resize(ref _prevStructures, settlements * kinds);
        }
        for (int s = 0; s < settlements; s++)
        {
            _prevFire[s] = r.ReadBoolean(); _prevFood[s] = r.ReadByte(); _prevForecast[s] = r.ReadByte();
            byte[] structures = r.ReadBytes(kinds);
            if (structures.Length != kinds) throw new InvalidDataException("Truncated view state.");
            Buffer.BlockCopy(structures, 0, _prevStructures, s * kinds, kinds);
        }
        _prevSettlements = settlements;
        int announced = r.ReadInt32();
        if (announced < 0 || announced > MaxAnnounced) throw new InvalidDataException("Bad view state.");
        _announcedCount = announced;
        for (int i = 0; i < announced; i++) { _announcedKey[i] = r.ReadInt32(); _announcedArrive[i] = r.ReadInt32(); }
        _claimSeason = r.ReadInt32();
        if (_claimSeason >= 0)
            for (int t = 0; t < _tileCount; t++) { _topClaim[t] = r.ReadByte(); _topCiv[t] = r.ReadSByte(); _contested[t] = r.ReadBoolean(); }
        _researchCursor = 0;
        _politicsCursor = 0;
    }

    private static void WriteEvent(BinaryWriter w, in ViewEvent e)
    {
        w.Write(e.Sequence); w.Write(e.Tick); w.Write(e.Figure);
        w.Write(e.Project); w.Write(e.Settlement); w.Write(e.Tile); w.Write(e.A); w.Write(e.B); w.Write(e.C);
        w.Write((ushort)e.Kind); w.Write((ushort)e.Text); w.Write((byte)e.Tier); w.Write(e.Civ); w.Write(e.Other);
    }

    private static ViewEvent ReadEvent(BinaryReader r) => new()
    {
        Sequence = r.ReadInt64(), Tick = r.ReadInt64(), Figure = r.ReadInt64(),
        Project = r.ReadInt32(), Settlement = r.ReadInt32(), Tile = r.ReadInt32(), A = r.ReadInt32(), B = r.ReadInt32(), C = r.ReadInt32(),
        Kind = (ViewEventKind)r.ReadUInt16(), Text = (ChronicleKey)r.ReadUInt16(), Tier = (ViewEventTier)r.ReadByte(),
        Civ = r.ReadSByte(), Other = r.ReadSByte(),
    };
}
