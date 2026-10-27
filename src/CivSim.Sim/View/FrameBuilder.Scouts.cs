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
    private const int MaxScoutJourneys = CivResearch.MaxSettlements * 2;

    private sealed class ScoutJourney
    {
        public long FigureId;
        public int HomeTile, TargetTile;
        public long StartTick, LegTicks;
        public int[] Path = Array.Empty<int>();
        public int PathLength;
    }

    private readonly record struct ScoutPosition(int Tile, int X, int Y);
    private readonly SortedList<long, ScoutJourney> _scoutJourneys = new();
    private readonly SortedList<long, ScoutPosition> _scoutPositions = new();
    private readonly SortedList<long, int> _scoutNoTargetSeason = new();
    private readonly List<long> _liveScouts = new();
    private readonly List<int> _scoutTargets = new();
    private readonly Stack<ScoutJourney> _scoutJourneyPool = new(MaxScoutJourneys);

    private void InitializeScoutJourneys()
    {
        for (int i = 0; i < MaxScoutJourneys; i++)
            _scoutJourneyPool.Push(new ScoutJourney { Path = new int[_tileCount] });
    }

    private void PrepareScouts(LivingEconomyModel eco, PoliticsSystem politics, long tick)
    {
        _scoutPositions.Clear();
        _liveScouts.Clear();
        _scoutTargets.Clear();
        int season = checked((int)(tick / LivingEconomyModel.SeasonTicks));
        long seasonStart = (long)season * LivingEconomyModel.SeasonTicks;

        if (_viewer >= 0)
        {
            for (int i = 0; i < _scoutJourneys.Count; i++)
            {
                ScoutJourney journey = _scoutJourneys.Values[i];
                if (tick < journey.StartTick + 2 * journey.LegTicks) _scoutTargets.Add(journey.TargetTile);
            }

            for (int i = 0; i < eco.FigureCount; i++)
            {
                FigureView figure = eco.Figure(i);
                if (figure.Civilization != _viewer || figure.Job != EconomyJob.Scout ||
                    (uint)figure.SettlementId >= (uint)_settlementCount) continue;
                _liveScouts.Add(figure.Id);
                int home = _sTile[figure.SettlementId];
                bool hasJourney = _scoutJourneys.TryGetValue(figure.Id, out ScoutJourney? journey);
                if (!hasJourney || journey!.HomeTile != home || tick >= journey.StartTick + 2 * journey.LegTicks)
                {
                    if (_scoutNoTargetSeason.TryGetValue(figure.Id, out int lastAttempt) && lastAttempt == season) continue;
                    _scoutNoTargetSeason[figure.Id] = season;
                    int target = ChooseScoutTarget(politics, figure.Civilization, home);
                    if (target < 0)
                    {
                        if (hasJourney) ReleaseScoutJourney(figure.Id);
                        continue;
                    }
                    journey ??= RentScoutJourney();
                    int pathLength = HexGrid.WalkingPathFrom(_tiles, _width, _height, _scoutCosts, home, target, journey.Path);
                    if (pathLength < 2)
                    {
                        if (hasJourney) _scoutJourneys.Remove(figure.Id);
                        ReturnScoutJourney(journey);
                        continue;
                    }
                    int steps = pathLength - 1;
                    int seasons = Math.Max(1, (steps + ScoutTilesPerSeason - 1) / ScoutTilesPerSeason);
                    journey.FigureId = figure.Id;
                    journey.HomeTile = home;
                    journey.TargetTile = target;
                    journey.StartTick = seasonStart;
                    journey.LegTicks = (long)seasons * LivingEconomyModel.SeasonTicks;
                    journey.PathLength = pathLength;
                    _scoutJourneys[figure.Id] = journey;
                    _scoutTargets.Add(target);
                }
                else if (journey!.PathLength < 2)
                {
                    journey.PathLength = BuildScoutPath(journey.HomeTile, journey.TargetTile, journey.Path);
                    if (journey.PathLength < 2) { ReleaseScoutJourney(figure.Id); continue; }
                }

                _scoutPositions[figure.Id] = PositionOnScoutPath(journey!, tick);
            }
        }

        for (int i = _scoutJourneys.Count - 1; i >= 0; i--)
            if (!_liveScouts.Contains(_scoutJourneys.Keys[i]))
            {
                ReturnScoutJourney(_scoutJourneys.Values[i]);
                _scoutJourneys.RemoveAt(i);
            }
        for (int i = _scoutNoTargetSeason.Count - 1; i >= 0; i--)
            if (!_liveScouts.Contains(_scoutNoTargetSeason.Keys[i])) _scoutNoTargetSeason.RemoveAt(i);
    }

    private int ChooseScoutTarget(PoliticsSystem politics, int civ, int home)
    {
        int reach = BeliefRules.ScoutMapRange(politics.ReachOf(civ),
            politics.DecreeResearch.IsDone(civ, "WAY-01"), politics.DecreeResearch.IsDone(civ, "WAY-03"));
        int maxCost = Math.Max(100, reach * 40);
        HexGrid.WalkingCostsFrom(_tiles, _width, _height, home, _scoutCosts, _scoutCostQueue, maxCost);
        IReadOnlyList<int> beacons = politics.BeaconsOf(civ);
        int best = -1, bestScore = int.MinValue;
        for (int tile = 0; tile < _tileCount; tile++)
        {
            if (_explored[tile] != 0 || !_tiles[tile].IsLand || _tiles[tile].Biome == Biome.Peak ||
                _scoutCosts[tile] == int.MaxValue || HexGrid.Distance(home, tile, _width) > reach || _scoutTargets.Contains(tile)) continue;
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
            int score = unknownLand * 1000 - Math.Min(_scoutCosts[tile], 999) + beaconPull * 250;
            if (score > bestScore || score == bestScore && tile < best)
            {
                best = tile;
                bestScore = score;
            }
        }
        return best;
    }

    private int BuildScoutPath(int home, int target, int[] path)
    {
        HexGrid.WalkingCostsFrom(_tiles, _width, _height, home, _scoutCosts, _scoutCostQueue);
        return HexGrid.WalkingPathFrom(_tiles, _width, _height, _scoutCosts, home, target, path);
    }

    private ScoutJourney RentScoutJourney()
        => _scoutJourneyPool.Count > 0 ? _scoutJourneyPool.Pop() : new ScoutJourney { Path = new int[_tileCount] };

    private void ReturnScoutJourney(ScoutJourney journey)
    {
        journey.PathLength = 0;
        if (_scoutJourneyPool.Count < MaxScoutJourneys) _scoutJourneyPool.Push(journey);
    }

    private void ReleaseScoutJourney(long figureId)
    {
        if (!_scoutJourneys.TryGetValue(figureId, out ScoutJourney? journey)) return;
        _scoutJourneys.Remove(figureId);
        ReturnScoutJourney(journey);
    }

    private ScoutPosition PositionOnScoutPath(ScoutJourney journey, long tick)
    {
        long elapsed = Math.Clamp(tick - journey.StartTick, 0, journey.LegTicks * 2);
        bool returning = elapsed >= journey.LegTicks;
        long legElapsed = returning ? elapsed - journey.LegTicks : elapsed;
        int steps = journey.PathLength - 1;
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
        for (int i = 0; i < _scoutJourneys.Count; i++) ReturnScoutJourney(_scoutJourneys.Values[i]);
        _scoutJourneys.Clear();
        _scoutPositions.Clear();
        _scoutNoTargetSeason.Clear();
        _liveScouts.Clear();
        _scoutTargets.Clear();
    }

    private void MarkScoutVisibility()
    {
        for (int i = 0; i < _scoutPositions.Count; i++) MarkDisk(_scoutPositions.Values[i].Tile, ScoutSight);
    }
}
