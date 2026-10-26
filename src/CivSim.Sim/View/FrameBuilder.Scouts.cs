using System;
using System.Collections.Generic;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>Scouts use the economy's job assignment and the view's ordinary figure animation, with a round trip to the frontier.</summary>
public sealed partial class FrameBuilder
{
    private const int ScoutSight = 3, ScoutRevealRadius = 3, ScoutTilesPerSeason = 10;

    private sealed class ScoutJourney
    {
        public long FigureId;
        public int HomeTile, TargetTile;
        public long StartTick, LegTicks;
        public int[] Path = Array.Empty<int>();
    }

    private readonly record struct ScoutPosition(int Tile, int X, int Y);
    private readonly SortedList<long, ScoutJourney> _scoutJourneys = new();
    private readonly SortedList<long, ScoutPosition> _scoutPositions = new();
    private readonly SortedList<long, int> _scoutNoTargetSeason = new();
    private readonly SortedSet<long> _liveScouts = new();
    private readonly SortedSet<int> _scoutTargets = new();

    private void PrepareScouts(LivingEconomyModel eco, PoliticsSystem politics, long tick)
    {
        _scoutPositions.Clear();
        _liveScouts.Clear();
        _scoutTargets.Clear();
        int season = checked((int)(tick / LivingEconomyModel.SeasonTicks));
        long seasonStart = (long)season * LivingEconomyModel.SeasonTicks;

        if (_viewer >= 0)
        {
            foreach (ScoutJourney journey in _scoutJourneys.Values)
                if (tick < journey.StartTick + 2 * journey.LegTicks) _scoutTargets.Add(journey.TargetTile);

            for (int i = 0; i < eco.FigureCount; i++)
            {
                FigureView figure = eco.Figure(i);
                if (figure.Civilization != _viewer || figure.Job != EconomyJob.Scout ||
                    (uint)figure.SettlementId >= (uint)_settlementCount) continue;
                _liveScouts.Add(figure.Id);
                int home = _sTile[figure.SettlementId];
                if (!_scoutJourneys.TryGetValue(figure.Id, out ScoutJourney? journey) || journey.HomeTile != home ||
                    tick >= journey.StartTick + 2 * journey.LegTicks)
                {
                    _scoutJourneys.Remove(figure.Id);
                    journey = null;
                    if (_scoutNoTargetSeason.TryGetValue(figure.Id, out int lastAttempt) && lastAttempt == season) continue;
                    _scoutNoTargetSeason[figure.Id] = season;
                    int target = ChooseScoutTarget(politics, figure.Civilization, home);
                    if (target < 0) continue;
                    int[] path = BuildScoutPath(home, target);
                    if (path.Length < 2) continue;
                    int steps = path.Length - 1;
                    int seasons = Math.Max(1, (steps + ScoutTilesPerSeason - 1) / ScoutTilesPerSeason);
                    journey = new ScoutJourney
                    {
                        FigureId = figure.Id, HomeTile = home, TargetTile = target,
                        StartTick = seasonStart, LegTicks = (long)seasons * LivingEconomyModel.SeasonTicks,
                        Path = path,
                    };
                    _scoutJourneys[figure.Id] = journey;
                    _scoutTargets.Add(target);
                }
                else if (journey.Path.Length < 2)
                {
                    journey.Path = BuildScoutPath(journey.HomeTile, journey.TargetTile);
                    if (journey.Path.Length < 2) { _scoutJourneys.Remove(figure.Id); continue; }
                }

                _scoutPositions[figure.Id] = PositionOnScoutPath(journey, tick);
            }
        }

        foreach (long id in new List<long>(_scoutJourneys.Keys))
            if (!_liveScouts.Contains(id)) _scoutJourneys.Remove(id);
        foreach (long id in new List<long>(_scoutNoTargetSeason.Keys))
            if (!_liveScouts.Contains(id)) _scoutNoTargetSeason.Remove(id);
    }

    private int ChooseScoutTarget(PoliticsSystem politics, int civ, int home)
    {
        int reach = BeliefRules.ScoutMapRange(politics.ReachOf(civ),
            politics.DecreeResearch.IsDone(civ, "WAY-01"), politics.DecreeResearch.IsDone(civ, "WAY-03"));
        int maxCost = Math.Max(100, reach * 40);
        int[] costs = HexGrid.WalkingCostsFrom(_tiles, _width, _height, home, maxCost);
        IReadOnlyList<int> beacons = politics.BeaconsOf(civ);
        int best = -1, bestScore = int.MinValue;
        for (int tile = 0; tile < _tileCount; tile++)
        {
            if (_explored[tile] != 0 || !_tiles[tile].IsLand || _tiles[tile].Biome == Biome.Peak ||
                costs[tile] == int.MaxValue || HexGrid.Distance(home, tile, _width) > reach || _scoutTargets.Contains(tile)) continue;
            int unknownLand = 0;
            HexGrid.Coordinates(tile, _width, out int cx, out int cy);
            for (int y = Math.Max(0, cy - ScoutRevealRadius); y <= Math.Min(_height - 1, cy + ScoutRevealRadius); y++)
                for (int x = Math.Max(0, cx - ScoutRevealRadius - 1); x <= Math.Min(_width - 1, cx + ScoutRevealRadius + 1); x++)
                {
                    int nearby = y * _width + x;
                    if (_explored[nearby] == 0 && _tiles[nearby].IsLand && _tiles[nearby].Biome != Biome.Peak &&
                        HexGrid.Distance(tile, nearby, _width) <= ScoutRevealRadius) unknownLand++;
                }
            int beaconPull = 0;
            for (int b = 0; b < beacons.Count; b++)
            {
                int distance = HexGrid.Distance(tile, beacons[b], _width);
                if (distance <= 5) beaconPull = Math.Max(beaconPull, 5 - distance);
            }
            int score = unknownLand * 1000 - Math.Min(costs[tile], 999) + beaconPull * 250;
            if (score > bestScore || score == bestScore && tile < best)
            {
                best = tile;
                bestScore = score;
            }
        }
        return best;
    }

    private int[] BuildScoutPath(int home, int target)
        => HexGrid.WalkingPathFrom(_tiles, _width, _height, home, target);

    private ScoutPosition PositionOnScoutPath(ScoutJourney journey, long tick)
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
        int from = journey.Path[fromIndex], to = journey.Path[toIndex];
        int fx = HexLayout.CenterX(from, _width), fy = HexLayout.CenterY(from, _width);
        int tx = HexLayout.CenterX(to, _width), ty = HexLayout.CenterY(to, _width);
        int x = fx + (int)((long)(tx - fx) * fraction / 1000);
        int y = fy + (int)((long)(ty - fy) * fraction / 1000);
        return new ScoutPosition(HexLayout.NearestTile(x, y, _width, _height), x, y);
    }

    private bool TryScoutPosition(long figureId, out ScoutPosition position)
        => _scoutPositions.TryGetValue(figureId, out position);

    private void ClearScoutViewState()
    {
        _scoutJourneys.Clear();
        _scoutPositions.Clear();
        _scoutNoTargetSeason.Clear();
        _liveScouts.Clear();
        _scoutTargets.Clear();
    }

    private void MarkScoutVisibility()
    {
        foreach (ScoutPosition position in _scoutPositions.Values) MarkDisk(position.Tile, ScoutSight);
    }
}
