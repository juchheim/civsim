using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A simulation-owned scout route, exposed read-only to views.</summary>
public readonly record struct ScoutJourneyView(long FigureId, int Civilization, int HomeTile, int TargetTile,
    long StartTick, long LegTicks, ReadOnlyMemory<int> Path);

public sealed partial class PoliticsSystem
{
    private const int ScoutTilesPerSeason = 10;
    private const int ScoutRevealRadius = BeliefRules.SightRadius;

    private sealed class ScoutJourney
    {
        public long FigureId;
        public int Civilization, HomeTile, TargetTile;
        public long StartTick, LegTicks;
        public int[] Path = Array.Empty<int>();
    }

    private SortedList<long, ScoutJourney> _scoutJourneys = new();
    private SortedList<long, int> _scoutAttemptSeason = new();
    private byte[][] _scoutExplored = Array.Empty<byte[]>();
    private sbyte[][] _scoutOwnerSeen = Array.Empty<sbyte[]>();
    private int[][] _scoutOwnerSeenSeason = Array.Empty<int[]>();
    private int[] _scoutCosts = Array.Empty<int>();
    private int[] _scoutPathScratch = Array.Empty<int>();
    private PriorityQueue<int, int> _scoutCostQueue = new();
    private readonly List<int> _scoutTargets = new();
    private readonly HashSet<long> _liveScoutSet = new();

    private void InitializeScoutSimulation()
    {
        int tileCount = _tileArray.Length;
        _scoutExplored = new byte[_civs][];
        _scoutOwnerSeen = new sbyte[_civs][];
        _scoutOwnerSeenSeason = new int[_civs][];
        for (int civ = 0; civ < _civs; civ++)
        {
            _scoutExplored[civ] = new byte[tileCount];
            _scoutOwnerSeen[civ] = new sbyte[tileCount];
            Array.Fill(_scoutOwnerSeen[civ], (sbyte)-1);
            _scoutOwnerSeenSeason[civ] = new int[tileCount];
            Array.Fill(_scoutOwnerSeenSeason[civ], -1);
        }
        _scoutCosts = new int[tileCount];
        _scoutPathScratch = new int[tileCount];
        _scoutCostQueue = new PriorityQueue<int, int>(checked(tileCount * 6));
    }

    /// <summary>Gets the current authoritative route for a scout figure.</summary>
    public bool TryGetScoutJourney(long figureId, out ScoutJourneyView journey)
    {
        if (_scoutJourneys.TryGetValue(figureId, out ScoutJourney? value))
        {
            journey = new ScoutJourneyView(value.FigureId, value.Civilization, value.HomeTile, value.TargetTile,
                value.StartTick, value.LegTicks, value.Path);
            return true;
        }
        journey = default;
        return false;
    }

    /// <summary>Whether a civilization's simulated scouts have explored this tile.</summary>
    public bool ScoutMapExplored(int civ, int tile) => _scoutExplored[civ][tile] != 0;

    /// <summary>Owner last observed by this civilization's simulated scouts, and the season of that sighting.</summary>
    public (int Owner, int Season) ScoutMapOwnerSeen(int civ, int tile)
        => (_scoutOwnerSeen[civ][tile], _scoutOwnerSeenSeason[civ][tile]);

    private void AdvanceScoutSimulation(long tick)
    {
        RefreshSettlements();
        int season = checked((int)(tick / LivingEconomyModel.SeasonTicks));
        int observationSeason = season + 1;
        if (tick % LivingEconomyModel.SeasonTicks == 0)
        {
            for (int civ = 0; civ < _civs; civ++)
            {
                int routineSight = BeliefRules.RoutineSight(ReachOf(civ));
                for (int s = 0; s < _sCount; s++)
                    if (_sCiv[s] == civ && !_free[s] && _sPop[s] > 0)
                        RecordScoutSight(civ, _sTile[s], routineSight, observationSeason);
            }
        }

        _liveScoutSet.Clear();
        for (int civ = 0; civ < _civs; civ++)
        {
            _scoutTargets.Clear();
            for (int i = 0; i < _scoutJourneys.Count; i++)
            {
                ScoutJourney active = _scoutJourneys.Values[i];
                if (active.Civilization == civ && tick < active.StartTick + 2 * active.LegTicks)
                    _scoutTargets.Add(active.TargetTile);
            }

            for (int i = 0; i < _economy.FigureCount; i++)
            {
                FigureView figure = _economy.Figure(i);
                if (figure.Civilization != civ || figure.Job != EconomyJob.Scout ||
                    (uint)figure.SettlementId >= (uint)_sCount) continue;
                _liveScoutSet.Add(figure.Id);
                int home = _sTile[figure.SettlementId];
                bool hasJourney = _scoutJourneys.TryGetValue(figure.Id, out ScoutJourney? journey);
                if (hasJourney && (journey!.Civilization != civ || journey.HomeTile != home ||
                    tick >= journey.StartTick + 2 * journey.LegTicks))
                {
                    _scoutJourneys.Remove(figure.Id);
                    hasJourney = false;
                    journey = null;
                }

                if (!hasJourney)
                {
                    if (_scoutAttemptSeason.TryGetValue(figure.Id, out int lastAttempt) && lastAttempt == season) continue;
                    _scoutAttemptSeason[figure.Id] = season;
                    int target = ChooseScoutTarget(civ, home);
                    if (target < 0) continue;
                    HexGrid.WalkingCostsFrom(_tileArray, _width, _height, home, _scoutCosts, _scoutCostQueue);
                    int pathLength = HexGrid.WalkingPathFrom(_tileArray, _width, _height, _scoutCosts, home, target, _scoutPathScratch);
                    if (pathLength < 2) continue;
                    int steps = pathLength - 1;
                    int seasons = Math.Max(1, (steps + ScoutTilesPerSeason - 1) / ScoutTilesPerSeason);
                    var path = new int[pathLength];
                    Array.Copy(_scoutPathScratch, path, pathLength);
                    journey = new ScoutJourney
                    {
                        FigureId = figure.Id,
                        Civilization = civ,
                        HomeTile = home,
                        TargetTile = target,
                        StartTick = (long)season * LivingEconomyModel.SeasonTicks,
                        LegTicks = (long)seasons * LivingEconomyModel.SeasonTicks,
                        Path = path,
                    };
                    _scoutJourneys.Add(figure.Id, journey);
                    _scoutTargets.Add(target);
                }

                int scoutTile = PositionOnScoutPath(journey!, tick);
                RecordScoutSight(civ, scoutTile, ScoutRevealRadius, observationSeason);
                DetectScoutContacts(civ, scoutTile);
            }
        }

        for (int i = _scoutJourneys.Count - 1; i >= 0; i--)
            if (!_liveScoutSet.Contains(_scoutJourneys.Keys[i])) _scoutJourneys.RemoveAt(i);
        for (int i = _scoutAttemptSeason.Count - 1; i >= 0; i--)
            if (!_liveScoutSet.Contains(_scoutAttemptSeason.Keys[i])) _scoutAttemptSeason.RemoveAt(i);
    }

    private int ChooseScoutTarget(int civ, int home)
    {
        int reach = BeliefRules.ScoutMapRange(ReachOf(civ), _research.IsDone(civ, _p.Way01), _research.IsDone(civ, _p.Way03));
        int maxCost = Math.Max(100, reach * 40);
        HexGrid.WalkingCostsFrom(_tileArray, _width, _height, home, _scoutCosts, _scoutCostQueue, maxCost);
        IReadOnlyList<int> beacons = _beacons[civ];
        int best = -1, bestScore = int.MinValue;
        for (int tile = 0; tile < _tileArray.Length; tile++)
        {
            if (_scoutExplored[civ][tile] != 0 || !_tileArray[tile].IsLand || _tileArray[tile].Biome == Biome.Peak ||
                _scoutCosts[tile] == int.MaxValue || HexGrid.Distance(home, tile, _width) > reach || _scoutTargets.Contains(tile)) continue;
            int unknownLand = 0;
            HexGrid.Coordinates(tile, _width, out int cx, out int cy);
            for (int y = Math.Max(0, cy - ScoutRevealRadius); y <= Math.Min(_height - 1, cy + ScoutRevealRadius); y++)
                for (int x = Math.Max(0, cx - ScoutRevealRadius - 1); x <= Math.Min(_width - 1, cx + ScoutRevealRadius + 1); x++)
                {
                    int nearby = y * _width + x;
                    if (_scoutExplored[civ][nearby] == 0 && _tileArray[nearby].IsLand && _tileArray[nearby].Biome != Biome.Peak &&
                        HexGrid.Distance(tile, nearby, _width) <= ScoutRevealRadius) unknownLand++;
                }
            int beaconPull = 0;
            for (int b = 0; b < beacons.Count; b++)
            {
                int distance = HexGrid.Distance(tile, beacons[b], _width);
                if (distance <= 5) beaconPull = Math.Max(beaconPull, 5 - distance);
            }
            int score = unknownLand * 1000 - Math.Min(_scoutCosts[tile], 999) + beaconPull * 250;
            if (score > bestScore || score == bestScore && tile < best)
            {
                best = tile;
                bestScore = score;
            }
        }
        return best;
    }

    private int PositionOnScoutPath(ScoutJourney journey, long tick)
    {
        long elapsed = Math.Clamp(tick - journey.StartTick, 0, journey.LegTicks * 2);
        bool returning = elapsed >= journey.LegTicks;
        long legElapsed = returning ? elapsed - journey.LegTicks : elapsed;
        int steps = journey.Path.Length - 1;
        long progress = Math.Min((long)steps * 1000, legElapsed * steps * 1000 / journey.LegTicks);
        int completed = (int)(progress / 1000);
        int fraction = (int)(progress % 1000);
        int fromIndex = returning ? steps - completed : completed;
        int toIndex = returning ? Math.Max(0, fromIndex - 1) : Math.Min(steps, fromIndex + 1);
        return fraction >= 500 ? journey.Path[toIndex] : journey.Path[fromIndex];
    }

    private void RecordScoutSight(int civ, int center, int radius, int season)
    {
        HexGrid.Coordinates(center, _width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(_height - 1, cy + radius); y++)
            for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(_width - 1, cx + radius + 1); x++)
            {
                int tile = y * _width + x;
                if (HexGrid.Distance(center, tile, _width) > radius) continue;
                _scoutExplored[civ][tile] = 1;
                int owner = _territory.OwnerOf(tile);
                _scoutOwnerSeen[civ][tile] = (sbyte)owner;
                _scoutOwnerSeenSeason[civ][tile] = season;
                if (owner < 0 || owner >= _civs || owner == civ || _dip.Aware(civ, owner)) continue;
                _dip.MarkAware(civ, owner);
                int distance = HexGrid.Distance(center, tile, _width);
                _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.Aware, owner, distance, 0));
                _events.Add(new PoliticsEvent(_tick, (byte)owner, PoliticsEventKind.Aware, civ, distance, 0));
            }
    }

    private void DetectScoutContacts(int civ, int scoutTile)
    {
        for (int s = 0; s < _sCount; s++)
        {
            int other = _sCiv[s];
            if (other == civ || other < 0 || other >= _civs || _free[s] || _sPop[s] <= 0 ||
                _dip.Contacted(civ, other) || HexGrid.Distance(scoutTile, _sTile[s], _width) > ScoutRevealRadius) continue;
            if (!_dip.Aware(civ, other))
            {
                _dip.MarkAware(civ, other);
                int distance = HexGrid.Distance(scoutTile, _sTile[s], _width);
                _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.Aware, other, distance, 1));
                _events.Add(new PoliticsEvent(_tick, (byte)other, PoliticsEventKind.Aware, civ, distance, 1));
            }
            MakeContact(civ, other);
        }
    }

    private void HashScoutSimulation(ref StateHasher h)
    {
        h.Add(_scoutJourneys.Count);
        for (int i = 0; i < _scoutJourneys.Count; i++)
        {
            ScoutJourney j = _scoutJourneys.Values[i];
            h.Add(j.FigureId); h.Add(j.Civilization); h.Add(j.HomeTile); h.Add(j.TargetTile); h.Add(j.StartTick); h.Add(j.LegTicks);
            h.Add(j.Path.Length); foreach (int tile in j.Path) h.Add(tile);
        }
        h.Add(_scoutAttemptSeason.Count);
        for (int i = 0; i < _scoutAttemptSeason.Count; i++)
        { h.Add(_scoutAttemptSeason.Keys[i]); h.Add(_scoutAttemptSeason.Values[i]); }
        for (int civ = 0; civ < _civs; civ++)
            for (int tile = 0; tile < _tileArray.Length; tile++)
            {
                h.Add(_scoutExplored[civ][tile]);
                h.Add((int)_scoutOwnerSeen[civ][tile]);
                h.Add(_scoutOwnerSeenSeason[civ][tile]);
            }
    }

    private void WriteScoutSimulation(BinaryWriter w)
    {
        w.Write(_civs); w.Write(_tileArray.Length);
        for (int civ = 0; civ < _civs; civ++)
        {
            w.Write(_scoutExplored[civ]);
            for (int tile = 0; tile < _tileArray.Length; tile++)
            { w.Write(_scoutOwnerSeen[civ][tile]); w.Write(_scoutOwnerSeenSeason[civ][tile]); }
        }
        w.Write(_scoutJourneys.Count);
        for (int i = 0; i < _scoutJourneys.Count; i++)
        {
            ScoutJourney j = _scoutJourneys.Values[i];
            w.Write(j.FigureId); w.Write(j.Civilization); w.Write(j.HomeTile); w.Write(j.TargetTile); w.Write(j.StartTick); w.Write(j.LegTicks);
            w.Write(j.Path.Length); foreach (int tile in j.Path) w.Write(tile);
        }
        w.Write(_scoutAttemptSeason.Count);
        for (int i = 0; i < _scoutAttemptSeason.Count; i++)
        { w.Write(_scoutAttemptSeason.Keys[i]); w.Write(_scoutAttemptSeason.Values[i]); }
    }

    private void ReadScoutSimulation(BinaryReader r)
    {
        if (r.ReadInt32() != _civs || r.ReadInt32() != _tileArray.Length) throw new InvalidDataException("Scout state does not match this world.");
        for (int civ = 0; civ < _civs; civ++)
        {
            byte[] explored = r.ReadBytes(_tileArray.Length);
            if (explored.Length != _tileArray.Length) throw new InvalidDataException("Truncated scout map.");
            Buffer.BlockCopy(explored, 0, _scoutExplored[civ], 0, explored.Length);
            for (int tile = 0; tile < _tileArray.Length; tile++)
            { _scoutOwnerSeen[civ][tile] = r.ReadSByte(); _scoutOwnerSeenSeason[civ][tile] = r.ReadInt32(); }
        }
        _scoutJourneys.Clear();
        int journeys = r.ReadInt32();
        if (journeys < 0 || journeys > _economy.FigureCount) throw new InvalidDataException("Bad scout journey count.");
        for (int i = 0; i < journeys; i++)
        {
            long id = r.ReadInt64(); int civ = r.ReadInt32(), home = r.ReadInt32(), target = r.ReadInt32();
            long start = r.ReadInt64(), leg = r.ReadInt64(); int length = r.ReadInt32();
            if (id < 0 || (uint)civ >= (uint)_civs || (uint)home >= (uint)_tileArray.Length ||
                (uint)target >= (uint)_tileArray.Length || start < 0 || leg <= 0 || length < 2 || length > _tileArray.Length)
                throw new InvalidDataException("Bad scout journey.");
            var path = new int[length];
            for (int p = 0; p < length; p++)
            {
                path[p] = r.ReadInt32();
                if ((uint)path[p] >= (uint)_tileArray.Length) throw new InvalidDataException("Bad scout path.");
            }
            if (path[0] != home || path[^1] != target) throw new InvalidDataException("Bad scout path endpoints.");
            _scoutJourneys.Add(id, new ScoutJourney
            {
                FigureId = id, Civilization = civ, HomeTile = home, TargetTile = target,
                StartTick = start, LegTicks = leg, Path = path,
            });
        }
        _scoutAttemptSeason.Clear();
        int attempts = r.ReadInt32();
        if (attempts < 0 || attempts > _economy.FigureCount) throw new InvalidDataException("Bad scout attempt count.");
        for (int i = 0; i < attempts; i++)
        {
            long id = r.ReadInt64(); int season = r.ReadInt32();
            if (id < 0 || season < 0) throw new InvalidDataException("Bad scout attempt.");
            _scoutAttemptSeason.Add(id, season);
        }
    }
}
