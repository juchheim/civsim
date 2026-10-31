using CivSim.Game.Ui;
using Godot;

namespace CivSim.Game;

/// <summary>
/// Short words that rise off a map tile and fade ("+24 rations" where a raid was won). They are billboards at a fixed size on
/// screen, so they read at every zoom. Nothing here touches the simulation.
/// </summary>
public partial class Game
{
    private const float FloaterSeconds = 3.2f;

    private void SpawnMapFloater(int tile, string text, Color color)
    {
        if (_current is null || tile < 0 || tile >= _terrainHeights.Length || text.Length == 0) return;
        Vector3 center = TileCenter(tile % _current.Width, tile / _current.Width);
        // Offsets scale with the zoom so the rise reads the same on screen near and far.
        float rise = 0.12f * _zoom;
        var start = new Vector3(center.X, _terrainHeights[tile] + 0.07f * _zoom, center.Z);
        var label = new Label3D
        {
            Text = text,
            FontSize = 64,
            OutlineSize = 18,
            PixelSize = 0.00055f,
            Modulate = color,
            OutlineModulate = new Color(0.08f, 0.06f, 0.03f, 0.95f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            NoDepthTest = true,
            Shaded = false,
            DoubleSided = true,
            RenderPriority = 12,
            Position = start,
        };
        AddChild(label);
        Tween tween = label.CreateTween();
        if (UiSettings.Current.ReduceMotion)
        {
            tween.TweenInterval(FloaterSeconds * 0.7f);
        }
        else
        {
            tween.SetParallel(true);
            tween.TweenProperty(label, "position:y", start.Y + rise, FloaterSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        tween.Chain().TweenProperty(label, "modulate:a", 0.0f, FloaterSeconds * 0.3f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
