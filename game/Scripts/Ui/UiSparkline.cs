using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>A tiny trend line: the series scaled to its own range, a faint baseline and a dot on the newest sample.</summary>
public partial class UiSparkline : Control
{
    private const int MaxPoints = 24;
    private readonly float[] _values = new float[MaxPoints];
    private int _count;
    private Color _color = UiTokens.Accent;

    public UiSparkline(int width = 84, int height = 22)
    {
        CustomMinimumSize = new Vector2(width, height);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>Sets the series (oldest first); redraws only when something changed.</summary>
    public void SetSeries(ReadOnlySpan<float> values, Color color)
    {
        int count = Math.Min(values.Length, MaxPoints);
        int skip = values.Length - count;
        bool changed = count != _count || color != _color;
        for (int i = 0; i < count && !changed; i++) if (!Mathf.IsEqualApprox(_values[i], values[skip + i])) changed = true;
        if (!changed) return;
        for (int i = 0; i < count; i++) _values[i] = values[skip + i];
        _count = count;
        _color = color;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        DrawLine(new Vector2(0, h - 0.5f), new Vector2(w, h - 0.5f), UiTokens.Border, 1);
        if (_count < 2) return;
        float min = _values[0], max = _values[0];
        for (int i = 1; i < _count; i++) { min = Mathf.Min(min, _values[i]); max = Mathf.Max(max, _values[i]); }
        min = Mathf.Min(min, 0);
        float range = Mathf.Max(max - min, 0.0001f);
        var points = new Vector2[_count];
        for (int i = 0; i < _count; i++)
            points[i] = new Vector2(2 + (w - 4) * i / (_count - 1), 2 + (h - 4) * (1 - (_values[i] - min) / range));
        DrawPolyline(points, _color, 1.5f, true);
        DrawCircle(points[^1], 2.2f, _color);
    }
}
