using System;
using CivSim.Game.Ui;
using Godot;

namespace CivSim.Game;

/// <summary>The marker drawn on the map for a pinned tile: a gold hex outline and a floating pin that stays readable at every zoom.</summary>
public partial class Game
{
    private MeshInstance3D? _pinMarker;

    private void EnsurePinMarker()
    {
        if (_pinMarker is not null) return;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Color gold = new(1.0f, 0.62f, 0.05f, 1f);
        Color dark = new(0.08f, 0.06f, 0.03f, 1f);

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            foreach (Vector3 v in new[] { a, b, c, a, c, d }) { st.SetColor(color); st.AddVertex(v); }
        }
        void Edge(Vector2 p, Vector2 q, float width, Color color, float y)
        {
            Vector2 dir = (q - p).Normalized();
            Vector2 side = new Vector2(-dir.Y, dir.X) * width * 0.5f;
            Quad(new Vector3(p.X + side.X, y, p.Y + side.Y), new Vector3(p.X - side.X, y, p.Y - side.Y),
                 new Vector3(q.X - side.X, y, q.Y - side.Y), new Vector3(q.X + side.X, y, q.Y + side.Y), color);
        }
        for (int i = 0; i < 6; i++)
        {
            Vector2 a = HexCorner(i) * HexRadius, b = HexCorner((i + 1) % 6) * HexRadius;
            Edge(a, b, 0.12f, dark, 0.04f);
            Edge(a, b, 0.07f, gold, 0.045f);
        }
        // Floating pin: a diamond on a stem, drawn as two crossed double-sided quads.
        void Diamond(float angle, Color color, float scale)
        {
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 top = new(0, 0.95f * scale + (1 - scale) * 0.42f, 0), bottom = new(0, 0.5f, 0);
            Vector3 mid = new(0, 0.7f, 0);
            float w = 0.13f * scale;
            Quad(bottom, mid + dir * w, top, mid - dir * w, color);
            Quad(bottom, mid - dir * w, top, mid + dir * w, color);
        }
        Diamond(0, dark, 1.18f);
        Diamond(Mathf.Pi / 2, dark, 1.18f);
        Diamond(0, gold, 1.0f);
        Diamond(Mathf.Pi / 2, gold, 1.0f);
        Quad(new Vector3(-0.025f, 0.04f, 0), new Vector3(0.025f, 0.04f, 0), new Vector3(0.025f, 0.5f, 0), new Vector3(-0.025f, 0.5f, 0), gold);
        Quad(new Vector3(0, 0.04f, -0.025f), new Vector3(0, 0.04f, 0.025f), new Vector3(0, 0.5f, 0.025f), new Vector3(0, 0.5f, -0.025f), gold);

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true,
            RenderPriority = 10,
        };
        _pinMarker = new MeshInstance3D { Name = "PinnedTile", Mesh = st.Commit(), MaterialOverride = material, Visible = false };
        _pinMarker.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_pinMarker);
    }

    private void UpdatePinMarker()
    {
        if (_current is null) return;
        if (_pinnedTile < 0 || _pinnedTile >= _terrainHeights.Length)
        {
            if (_pinMarker is not null) _pinMarker.Visible = false;
            return;
        }
        EnsurePinMarker();
        Vector3 center = TileCenter(_pinnedTile % _current.Width, _pinnedTile / _current.Width);
        float scale = Mathf.Clamp(_zoom / 12.0f, 0.6f, 7.0f);
        _pinMarker!.Position = new Vector3(center.X, _terrainHeights[_pinnedTile], center.Z);
        _pinMarker.Scale = new Vector3(scale, scale, scale);
        _pinMarker.Visible = true;
    }
}
