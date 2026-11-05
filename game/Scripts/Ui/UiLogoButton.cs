using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The animated game logo used as the pause control. It plays while game time runs and rests on the clean
/// emblem frame (with an optional accent ring) while paused. Frames come from a 4x4 sprite sheet because Godot
/// cannot play GIFs.
/// </summary>
public partial class UiLogoButton : BaseButton
{
    private const string SheetPath = "res://assets/ui/hud/logo-swirl-sheet.png";
    private const int Columns = 4;
    private const int Cell = 128;
    private const int FrameCount = 16;
    private const double FrameSeconds = 0.14; // the source GIF's 140 ms per frame; constant at every game speed
    private const int RestFrame = 0;
    private const double FadeSeconds = 0.12;

    /// <summary>The tan disc behind the emblem, so its cut-out spiral gaps read as stone rather than the dark bar.</summary>
    private static readonly Color Tan = new("#D9BD8F");

    /// <summary>Emblem diameter as a share of the tan disc; the rest is the always-on tan border.</summary>
    private const float EmblemFraction = 0.88f;

    private static Texture2D? _sheet;

    private int _frame = RestFrame;
    private int _fadeFrom = RestFrame;
    private double _fade; // 1 -> 0 while crossfading from _fadeFrom to the rest frame
    private double _clock;
    private bool _playing;
    private bool _highlight;

    public UiLogoButton(Action pressed, int size)
    {
        CustomMinimumSize = new Vector2(size, size);
        FocusMode = FocusModeEnum.None; // Space is handled globally; a focusable button would toggle twice
        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        ActionMode = ActionModeEnum.Release;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Pressed += pressed;
        SetProcess(false);
        _sheet ??= GD.Load<Texture2D>(SheetPath);
    }

    /// <summary>True while game time runs: the animation loops. False: crossfade to the rest frame and stop.</summary>
    public bool Playing
    {
        get => _playing;
        set
        {
            if (_playing == value) return;
            _playing = value;
            if (value)
            {
                _clock = 0;
                _fade = 0;
                SetProcess(!UiSettings.Current.ReduceMotion);
            }
            else if (_frame != RestFrame && !UiSettings.Current.ReduceMotion)
            {
                _fadeFrom = _frame;
                _fade = 1;
                _frame = RestFrame;
                SetProcess(true);
            }
            else
            {
                _frame = RestFrame;
                _fade = 0;
                SetProcess(false);
            }
            QueueRedraw();
        }
    }

    /// <summary>Draws an accent ring (the paused cue).</summary>
    public bool Highlight
    {
        get => _highlight;
        set
        {
            if (_highlight == value) return;
            _highlight = value;
            QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        if (_fade > 0)
        {
            _fade = Math.Max(0, _fade - delta / FadeSeconds);
            QueueRedraw();
        }
        if (_playing)
        {
            _clock += delta;
            if (_clock >= FrameSeconds)
            {
                _clock -= FrameSeconds;
                if (_clock >= FrameSeconds) _clock = 0; // long hitch: do not fast-forward
                _frame = (_frame + 1) % FrameCount;
                QueueRedraw();
            }
        }
        else if (_fade <= 0)
        {
            SetProcess(false);
        }
    }

    public override void _Draw()
    {
        if (_sheet is null) return;
        var drawMode = GetDrawMode();
        float scale = drawMode is DrawMode.Pressed or DrawMode.HoverPressed ? 0.95f : drawMode == DrawMode.Hover ? 1.05f : 1.0f;
        Color tint = Disabled ? new Color(1, 1, 1, 0.45f)
            : drawMode == DrawMode.Hover ? new Color(1.12f, 1.10f, 1.06f) : Colors.White;
        float outer = Math.Min(Size.X, Size.Y) * scale; // the tan disc, border included
        float side = outer * EmblemFraction;            // the emblem sits inside, leaving a tan border all round
        var dest = new Rect2((Size - new Vector2(side, side)) / 2, new Vector2(side, side));
        var tan = new Color(Tan, Tan.A * tint.A);
        DrawCircle(Size / 2, outer / 2 - 2, tan);
        DrawArc(Size / 2, outer / 2 - 1.5f, 0, Mathf.Tau, 64, tan, 3, true); // antialiased rim
        if (_fade > 0)
        {
            DrawFrame(_fadeFrom, dest, new Color(tint, tint.A * (float)_fade));
            DrawFrame(_frame, dest, new Color(tint, tint.A * (1 - (float)_fade)));
        }
        else
        {
            DrawFrame(_frame, dest, tint);
        }
        if (_highlight)
            DrawArc(Size / 2, Math.Min(Size.X, Size.Y) / 2 - 0.75f, 0, Mathf.Tau, 64, UiTokens.Accent, 1.5f, true);
    }

    private void DrawFrame(int frame, Rect2 dest, Color tint)
    {
        var src = new Rect2((frame % Columns) * Cell, (frame / Columns) * Cell, Cell, Cell);
        DrawTextureRectRegion(_sheet!, dest, src, tint);
    }
}
