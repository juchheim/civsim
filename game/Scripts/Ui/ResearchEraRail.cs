using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// The age strip down the left edge of the research Map: one chip per drawn age, Dawn at the bottom (the map reads inside to outside),
/// each with a short name, a progress arc (known of placed) and a dot on the current age; the next unreached age shows a lock. A thumb
/// shows the map's continuous lens focus. A click on a chip opens that age; dragging scrubs the focus and snaps on release.
/// It only draws and reports: the map owns the lens. See docs/MVP/research-tree-era-focus-design.md §3.
/// </summary>
public partial class ResearchEraRail : Control
{
    public const float RailWidth = 56f, MaxChip = 52f, MinChip = 34f;

    public Action<int>? AgePicked;
    public Action<float>? Scrubbed;
    public Action? ScrubEnded;
    public Func<int, string>? TooltipFor;

    private int[] _eras = Array.Empty<int>(), _known = Array.Empty<int>(), _total = Array.Empty<int>();
    private int _current = -1, _hover = -1;
    private float _f;
    private bool _pressed, _dragging;
    private Vector2 _pressAt;
    private float _pressF;

    private static readonly StyleBoxFlat Back = UiTheme.Box(UiTokens.SurfaceMap, 12, UiTokens.Border, 1);
    private static readonly StyleBoxFlat Thumb = UiTheme.Box(UiTokens.WithAlpha(UiTokens.Accent, 0.16f), 9, UiTokens.WithAlpha(UiTokens.Accent, 0.7f), 1);

    public ResearchEraRail()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(RailWidth, 0);
    }

    /// <summary>The ages to show, ring by ring (inner first), with their counts; <paramref name="current"/> is the player's age.</summary>
    public void SetAges(ReadOnlySpan<int> eras, ReadOnlySpan<int> known, ReadOnlySpan<int> total, int current)
    {
        bool same = eras.Length == _eras.Length && current == _current;
        for (int i = 0; same && i < eras.Length; i++) same = eras[i] == _eras[i] && known[i] == _known[i] && total[i] == _total[i];
        if (same) return;
        _eras = eras.ToArray();
        _known = known.ToArray();
        _total = total.ToArray();
        _current = current;
        QueueRedraw();
    }

    public void SetLens(float f)
    {
        if (Mathf.IsEqualApprox(f, _f)) return;
        _f = f;
        QueueRedraw();
    }

    /// <summary>The height the rail wants for its chips.</summary>
    public float WantedHeight => _eras.Length * MaxChip + 12;

    private float Chip => _eras.Length == 0 ? MaxChip : Math.Clamp((Size.Y - 12) / _eras.Length, MinChip, MaxChip);
    private float Bottom => Size.Y - 6 - (Size.Y - 12 - Chip * _eras.Length) * 0.5f;
    private float CentreOf(float f) => Bottom - (f + 0.5f) * Chip;
    private float FocusAt(float y) => (Bottom - y) / Chip - 0.5f;

    private int ChipAt(Vector2 at)
    {
        if (at.X < 0 || at.X > Size.X) return -1;
        int i = (int)MathF.Floor((Bottom - at.Y) / Chip);
        return i >= 0 && i < _eras.Length ? i : -1;
    }

    public override void _GuiInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                if (button.Pressed)
                {
                    _pressed = true;
                    _dragging = false;
                    _pressAt = button.Position;
                    _pressF = _f;
                }
                else if (_pressed)
                {
                    _pressed = false;
                    if (_dragging) ScrubEnded?.Invoke();
                    else if (ChipAt(button.Position) is int chip and >= 0) AgePicked?.Invoke(_eras[chip]);
                    _dragging = false;
                }
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                if (_pressed && (motion.ButtonMask & MouseButtonMask.Left) != 0)
                {
                    if (!_dragging && motion.Position.DistanceTo(_pressAt) > 4) _dragging = true;
                    // rail pixels map linearly to the focus
                    if (_dragging) Scrubbed?.Invoke(_pressF + (_pressAt.Y - motion.Position.Y) / Chip);
                }
                int hover = ChipAt(motion.Position);
                if (hover != _hover) { _hover = hover; QueueRedraw(); }
                MouseDefaultCursorShape = hover >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
                AcceptEvent();
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover >= 0) { _hover = -1; QueueRedraw(); }
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        int chip = ChipAt(atPosition);
        return chip >= 0 && TooltipFor is not null ? TooltipFor(_eras[chip]) : "";
    }

    public override void _Draw()
    {
        if (_eras.Length == 0) return;
        float chip = Chip;
        var back = new Rect2(0, CentreOf(_eras.Length - 0.5f) - 6, Size.X, chip * _eras.Length + 12);
        DrawStyleBox(Back, back);

        // the thumb: where the lens is, continuously
        float thumbY = CentreOf(Math.Clamp(_f, 0, _eras.Length - 1));
        DrawStyleBox(Thumb, new Rect2(4, thumbY - chip * 0.5f + 2, Size.X - 8, chip - 4));

        Font caps = UiTheme.Caps;
        const int font = 12;
        for (int i = 0; i < _eras.Length; i++)
        {
            int era = _eras[i];
            float cy = CentreOf(i);
            bool locked = era > _current;
            bool focused = Math.Abs(_f - i) < 0.5f;
            if (i == _hover && !focused) DrawRect(new Rect2(4, cy - chip * 0.5f + 2, Size.X - 8, chip - 4), UiTokens.WithAlpha(UiTokens.TextPrimary, 0.06f));

            // progress arc around a small disc
            var at = new Vector2(Size.X * 0.5f, cy - 6);
            float r = Math.Min(9f, chip * 0.2f);
            DrawCircle(at, r, UiTokens.WithAlpha(UiTokens.Surface2, 0.95f));
            DrawArc(at, r, 0, Mathf.Tau, 24, UiTokens.WithAlpha(UiTokens.TextTertiary, 0.35f), 2f, true);
            float share = _total[i] > 0 ? Math.Clamp(_known[i] / (float)_total[i], 0, 1) : 0;
            if (share > 0.001f)
                DrawArc(at, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * share, 24, era == _current ? UiTokens.Accent : UiTokens.TextSecondary, 2.5f, true);
            if (locked)
                UiDraw.Glyph(this, UiGlyph.Lock, new Rect2(at - new Vector2(5, 5), new Vector2(10, 10)), UiTokens.TextDisabled, 1.3f);
            else if (era == _current) DrawCircle(at, 3f, UiTokens.Accent);

            string name = UiText.AgeName(era);
            name = (name.Length > 3 ? name[..3] : name).ToUpperInvariant();
            Vector2 extent = caps.GetStringSize(name, HorizontalAlignment.Left, -1, font);
            Color color = focused ? UiTokens.TextPrimary : locked ? UiTokens.TextDisabled : era == _current ? UiTokens.Accent : UiTokens.TextTertiary;
            DrawString(caps, new Vector2((Size.X - extent.X) * 0.5f, cy + r + 4 + caps.GetAscent(font) - 2), name, HorizontalAlignment.Left, -1, font, color);
        }
    }
}
