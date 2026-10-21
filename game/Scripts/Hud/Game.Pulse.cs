using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using Godot;

namespace CivSim.Game;

/// <summary>
/// A completed project plays on the map: rings spread from the settlement where it was learned and a pale column of light rises
/// from the hearth. Every project gets one, so "something happened here" never depends on what the project does.
/// </summary>
public partial class Game
{
    private sealed class MapPulse
    {
        public Node3D Root = null!;
        public MeshInstance3D RingA = null!, RingB = null!, Beam = null!;
        public StandardMaterial3D RingMatA = null!, RingMatB = null!, BeamMat = null!;
        public float Age, Duration;
        public bool Keystone;
    }

    private readonly List<MapPulse> _pulses = new();
    private readonly List<(int Tile, bool Keystone)> _pulseQueue = new();
    private float _pulseGap;
    private ArrayMesh? _ringMesh, _beamMesh;
    private const int MaxActivePulses = 4;

    private static readonly Color PulseColor = new(1.0f, 0.86f, 0.52f);

    private void StartPulse(int tile, bool keystone)
    {
        if (tile < 0 || _terrainHeights.Length == 0 || tile >= _terrainHeights.Length) return;
        if (_pulseQueue.Count >= 6) _pulseQueue.RemoveAt(0);
        _pulseQueue.Add((tile, keystone));
    }

    private void EnsurePulseMeshes()
    {
        if (_ringMesh is not null) return;
        // A flat annulus of outer radius 1 (the node scales it).
        var ring = new SurfaceTool();
        ring.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 48;
        const float inner = 0.9f;
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Tau * i / segments, a1 = Mathf.Tau * (i + 1) / segments;
            var o0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var o1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 i0 = o0 * inner, i1 = o1 * inner;
            foreach (Vector3 v in new[] { o0, i0, o1, o1, i0, i1 }) { ring.SetColor(Colors.White); ring.AddVertex(v); }
        }
        _ringMesh = ring.Commit();

        // An open cylinder, opaque at the foot and clear at the top.
        var beam = new SurfaceTool();
        beam.Begin(Mesh.PrimitiveType.Triangles);
        const int sides = 16;
        for (int i = 0; i < sides; i++)
        {
            float a0 = Mathf.Tau * i / sides, a1 = Mathf.Tau * (i + 1) / sides;
            var b0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var b1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 t0 = b0 * 0.45f + Vector3.Up, t1 = b1 * 0.45f + Vector3.Up;
            var foot = new Color(1, 1, 1, 0.5f); var tip = new Color(1, 1, 1, 0.0f);
            void V(Vector3 v, Color c) { beam.SetColor(c); beam.AddVertex(v); }
            V(b0, foot); V(b1, foot); V(t1, tip);
            V(b0, foot); V(t1, tip); V(t0, tip);
        }
        _beamMesh = beam.Commit();
    }

    private StandardMaterial3D PulseMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        NoDepthTest = false,
        RenderPriority = 12,
    };

    private void LaunchPulse(int tile, bool keystone)
    {
        EnsurePulseMeshes();
        var pulse = new MapPulse { Duration = keystone ? 5.0f : 3.2f, Keystone = keystone };
        pulse.Root = new Node3D { Name = "ProjectPulse" };
        MeshInstance3D Make(Mesh mesh, out StandardMaterial3D material)
        {
            material = PulseMaterial();
            var instance = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            pulse.Root.AddChild(instance);
            return instance;
        }
        pulse.RingA = Make(_ringMesh!, out pulse.RingMatA);
        pulse.RingB = Make(_ringMesh!, out pulse.RingMatB);
        pulse.Beam = Make(_beamMesh!, out pulse.BeamMat);
        int width = _current?.Width ?? _width;
        Vector3 center = TileCenter(tile % width, tile / width);
        pulse.Root.Position = new Vector3(center.X, _terrainHeights[tile] + 0.03f, center.Z);
        AddChild(pulse.Root);
        _pulses.Add(pulse);
    }

    private void TickPulses(float delta)
    {
        _pulseGap -= delta;
        if (_pulseQueue.Count > 0 && _pulses.Count < MaxActivePulses && _pulseGap <= 0)
        {
            (int tile, bool keystone) = _pulseQueue[0];
            _pulseQueue.RemoveAt(0);
            LaunchPulse(tile, keystone);
            _pulseGap = 0.7f;
        }
        float zoomScale = Mathf.Clamp(_zoom / 12.0f, 1.0f, 6.0f);
        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            MapPulse pulse = _pulses[i];
            pulse.Age += delta;
            float t = pulse.Age / pulse.Duration;
            if (t >= 1.0f)
            {
                pulse.Root.QueueFree();
                _pulses.RemoveAt(i);
                continue;
            }
            float size = (pulse.Keystone ? 1.5f : 1.0f) * zoomScale;
            // Two rings, the second half a second behind; each spreads and fades.
            void Ring(MeshInstance3D ring, StandardMaterial3D material, float local)
            {
                if (local < 0) { ring.Visible = false; return; }
                ring.Visible = true;
                float e = 1.0f - Mathf.Pow(1.0f - Mathf.Clamp(local, 0, 1), 3.0f);
                float radius = Mathf.Lerp(0.12f, 1.25f, e) * size;
                ring.Scale = new Vector3(radius, 1, radius);
                float alpha = (1.0f - e) * 0.9f;
                material.AlbedoColor = new Color(PulseColor.R, PulseColor.G, PulseColor.B, alpha);
            }
            float seconds = pulse.Duration;
            Ring(pulse.RingA, pulse.RingMatA, pulse.Age / (seconds * 0.6f));
            Ring(pulse.RingB, pulse.RingMatB, (pulse.Age - 0.5f) / (seconds * 0.6f));
            // The column swells in, hangs, and thins out.
            float envelope = Mathf.Min(1.0f, pulse.Age / 0.35f) * Mathf.Clamp((1.0f - t) / 0.45f, 0, 1);
            float height = (pulse.Keystone ? 1.5f : 1.0f) * 1.4f * zoomScale * Mathf.Min(1.0f, 0.3f + pulse.Age / 0.6f);
            float width = 0.055f * (pulse.Keystone ? 1.6f : 1.0f) * zoomScale;
            pulse.Beam.Scale = new Vector3(width, height, width);
            pulse.BeamMat.AlbedoColor = new Color(PulseColor.R, PulseColor.G, PulseColor.B, envelope);
        }
    }
}
