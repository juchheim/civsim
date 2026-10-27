using System;
using System.Collections.Generic;
using CivSim.Sim.Ai;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

/// <summary>Scout movement is simulated by PoliticsSystem; the frame builder only places those scouts on the map.</summary>
public sealed partial class FrameBuilder
{
    private readonly record struct ScoutPosition(int Tile, int X, int Y);
    private readonly SortedList<long, ScoutPosition> _scoutPositions = new();

    private void PrepareScouts(LivingEconomyModel eco, PoliticsSystem politics, long tick)
    {
        _scoutPositions.Clear();
        if (_viewer < 0) return;
        for (int i = 0; i < eco.FigureCount; i++)
        {
            FigureView figure = eco.Figure(i);
            if (figure.Civilization != _viewer || figure.Job != EconomyJob.Scout ||
                !politics.TryGetScoutJourney(figure.Id, out ScoutJourneyView journey) ||
                tick >= journey.StartTick + 2 * journey.LegTicks) continue;
            _scoutPositions[figure.Id] = PositionOnScoutPath(journey, tick);
        }
    }

    private ScoutPosition PositionOnScoutPath(ScoutJourneyView journey, long tick)
    {
        ReadOnlySpan<int> path = journey.Path.Span;
        long elapsed = Math.Clamp(tick - journey.StartTick, 0, journey.LegTicks * 2);
        bool returning = elapsed >= journey.LegTicks;
        long legElapsed = returning ? elapsed - journey.LegTicks : elapsed;
        int steps = path.Length - 1;
        long progress = Math.Min((long)steps * 1000, legElapsed * steps * 1000 / journey.LegTicks);
        int completed = (int)(progress / 1000);
        int fraction = (int)(progress % 1000);
        int fromIndex = returning ? steps - completed : completed;
        int toIndex = returning ? Math.Max(0, fromIndex - 1) : Math.Min(steps, fromIndex + 1);
        int from = path[fromIndex], to = path[toIndex];
        int fx = HexLayout.CenterX(from, _width), fy = HexLayout.CenterY(from, _width);
        int tx = HexLayout.CenterX(to, _width), ty = HexLayout.CenterY(to, _width);
        int x = fx + (int)((long)(tx - fx) * fraction / 1000);
        int y = fy + (int)((long)(ty - fy) * fraction / 1000);
        return new ScoutPosition(HexLayout.NearestTile(x, y, _width, _height), x, y);
    }

    private bool TryScoutPosition(long figureId, out ScoutPosition position)
        => _scoutPositions.TryGetValue(figureId, out position);

    private void ClearScoutViewState() => _scoutPositions.Clear();

    private void MarkScoutVisibility()
    {
        for (int i = 0; i < _scoutPositions.Count; i++) MarkDisk(_scoutPositions.Values[i].Tile, BeliefRules.SightRadius);
    }
}
