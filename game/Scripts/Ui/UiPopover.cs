using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>A small panel over the HUD that closes when the player clicks anywhere outside it.</summary>
public partial class UiPopover : Control
{
    public PanelContainer Body { get; }
    public Action? Dismissed { get; set; }
    private Vector2 _desired;

    public UiPopover(string variation = "SheetPanel")
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Body = new PanelContainer { ThemeTypeVariation = variation, MouseFilter = MouseFilterEnum.Stop };
        AddChild(Body);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true })
        {
            AcceptEvent();
            Dismissed?.Invoke();
        }
    }

    /// <summary>Shows the popover with its top-left at <paramref name="position"/>, kept inside the viewport.</summary>
    public void Open(Vector2 position, float minWidth = 0)
    {
        _desired = position;
        Body.CustomMinimumSize = new Vector2(minWidth, 0);
        Body.Size = Body.GetCombinedMinimumSize();
        Visible = true;
        Body.Position = position;
        CallDeferred(nameof(Clamp));
    }

    /// <summary>Opens with the popover's right edge at <paramref name="right"/> (for buttons on the right side).</summary>
    public void OpenRightAligned(float right, float top, float minWidth = 0)
    {
        Body.CustomMinimumSize = new Vector2(minWidth, 0);
        Body.Size = Body.GetCombinedMinimumSize();
        _desired = new Vector2(right - Math.Max(minWidth, Body.Size.X), top);
        Visible = true;
        Body.Position = _desired;
        CallDeferred(nameof(Clamp));
    }

    private void Clamp()
    {
        Body.ResetSize();
        Vector2 view = GetViewportRect().Size;
        Vector2 size = Body.Size;
        Body.Position = new Vector2(Mathf.Clamp(_desired.X, 8, Mathf.Max(8, view.X - size.X - 8)), Mathf.Clamp(_desired.Y, 8, Mathf.Max(8, view.Y - size.Y - 8)));
    }

    public void Close() => Visible = false;
}
