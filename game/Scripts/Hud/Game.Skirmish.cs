using System;
using System.Collections.Generic;
using Godot;

namespace CivSim.Game;

/// <summary>
/// A raid (or assault) that reaches its target plays as a short skirmish on the map before its result is shown: the raiders run
/// in from the side they came from, the lines clash in dust, the losers fall or run and the winners stand under their banner and
/// a ring in their colour. The drawing lives in the renderer (Renderer/LowPoly3D/Scripts/skirmish_rig.gd, the same people meshes
/// and civ tints as the walking figures); this side only starts it, times it in real seconds and calls back when it is over.
/// Nothing here touches the simulation.
/// </summary>
/// <remarks>
/// At most <see cref="MaxActiveSkirmishes"/> play at once. A skirmish asked for beyond that is not drawn: StartSkirmish still
/// returns true and its onDone runs on the next frame, so a burst of raids never delays or loses a result. A new match, load or
/// replay (<see cref="ClearSkirmishes"/>, called first thing in ReplaceSession) ends every running skirmish and runs its onDone
/// at once, before the old match's UI is reset.
/// </remarks>
public partial class Game
{
    private readonly record struct SkirmishSpec(int Tile, int AttackerCiv, int DefenderCiv, bool AttackersWon,
        int Attackers, int Defenders, int AttackerLosses, int DefenderLosses, bool Assault = false);

    private sealed class Skirmish
    {
        public int Id;
        public Node3D Renderer = null!;
        public float Age, Duration;
        public Action OnDone = null!;
    }

    private const int MaxActiveSkirmishes = 3;
    private readonly List<Skirmish> _skirmishes = new();
    private readonly List<Action> _skirmishDeferred = new();

    /// Starts the skirmish at the tile. Returns false (and never calls onDone) if it cannot play (bad tile etc.).
    /// Otherwise calls onDone exactly once when the show is finished (about 3.5 to 4.8 s of real time), or immediately if the
    /// effect is torn down early (new match / load) so the caller's result is never lost.
    private bool StartSkirmish(in SkirmishSpec spec, Action onDone)
    {
        if (_visualRenderer is null || _current is null || spec.Tile < 0 || spec.Tile >= _terrainHeights.Length) return false;
        if (_skirmishes.Count >= MaxActiveSkirmishes)
        {
            _skirmishDeferred.Add(onDone);
            return true;
        }
        var payload = new Godot.Collections.Dictionary
        {
            ["tile"] = spec.Tile,
            ["att_civ"] = spec.AttackerCiv,
            ["def_civ"] = spec.DefenderCiv,
            ["att_won"] = spec.AttackersWon,
            ["att_n"] = spec.Attackers,
            ["def_n"] = spec.Defenders,
            ["att_lost"] = spec.AttackerLosses,
            ["def_lost"] = spec.DefenderLosses,
            ["angle"] = SkirmishApproachAngle(spec.Tile, spec.AttackerCiv),
            ["calm"] = _uiSettings.ReduceMotion,
            ["assault"] = spec.Assault,
        };
        Godot.Collections.Array result = _visualRenderer.Call("skirmish_start", payload).AsGodotArray();
        int id = result.Count > 0 ? result[0].AsInt32() : -1;
        float seconds = result.Count > 1 ? result[1].AsSingle() : 0;
        if (id < 0 || seconds <= 0) return false;
        _skirmishes.Add(new Skirmish { Id = id, Renderer = _visualRenderer, Duration = seconds, OnDone = onDone });
        return true;
    }

    /// <summary>The direction (radians in the map's XZ plane, from the target) the raiders come from: toward the attacker's
    /// nearest settlement, or a fixed angle from the tile when the viewer knows none.</summary>
    private float SkirmishApproachAngle(int tile, int attackerCiv)
    {
        int width = _current?.Width ?? _width;
        Vector3 target = TileCenter(tile % width, tile / width);
        float best = float.MaxValue;
        Vector3 from = Vector3.Zero;
        if (_current is not null)
            for (int i = 0; i < _current.SettlementCount; i++)
            {
                if (_current.SettlementCiv[i] != attackerCiv || _current.SettlementTile[i] == tile) continue;
                int t = _current.SettlementTile[i];
                Vector3 c = TileCenter(t % width, t / width);
                float d = c.DistanceSquaredTo(target);
                if (d < best) { best = d; from = c; }
            }
        if (best < float.MaxValue) return Mathf.Atan2(from.Z - target.Z, from.X - target.X);
        uint h = unchecked((uint)tile * 2654435761u ^ (uint)(attackerCiv + 1) * 40503u);
        return (h % 360) * Mathf.Pi / 180.0f;
    }

    private void TickSkirmishes(float delta)
    {
        if (_skirmishDeferred.Count > 0)
        {
            Action[] deferred = _skirmishDeferred.ToArray();
            _skirmishDeferred.Clear();
            foreach (Action done in deferred) done();
        }
        for (int i = _skirmishes.Count - 1; i >= 0; i--)
        {
            if (i >= _skirmishes.Count) continue;
            Skirmish s = _skirmishes[i];
            s.Age += delta;
            bool gone = s.Renderer != _visualRenderer || !GodotObject.IsInstanceValid(s.Renderer);
            if (!gone && s.Age < s.Duration) continue;
            _skirmishes.RemoveAt(i);
            s.OnDone();
        }
    }

    /// <summary>Ends every skirmish now (new match, load, replay) and hands each its result.</summary>
    private void ClearSkirmishes()
    {
        if (_visualRenderer is not null && GodotObject.IsInstanceValid(_visualRenderer)) _visualRenderer.Call("skirmish_clear");
        var pending = new List<Action>(_skirmishDeferred);
        foreach (Skirmish s in _skirmishes) pending.Add(s.OnDone);
        _skirmishDeferred.Clear();
        _skirmishes.Clear();
        foreach (Action done in pending) done();
    }
}
