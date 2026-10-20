using System;
using Godot;

namespace CivSim.Game;

/// <summary>A research node or focus slot button that carries a project during UI drag-and-drop.</summary>
public partial class ResearchProjectButton : Button
{
    public int ProjectIndex { get; set; } = -1;
    public int DropSlot { get; set; } = -1;
    public Func<int, bool>? CanAssignProject { get; set; }
    public Action<int, int>? ProjectDropped { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (ProjectIndex < 0) return default;
        var preview = new Label { Text = Text, CustomMinimumSize = new Vector2(100, 28) };
        preview.HorizontalAlignment = HorizontalAlignment.Center;
        preview.VerticalAlignment = VerticalAlignment.Center;
        SetDragPreview(preview);
        return ProjectIndex;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => DropSlot >= 0 && data.VariantType == Variant.Type.Int && CanAssignProject?.Invoke(data.AsInt32()) == true;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (DropSlot >= 0 && data.VariantType == Variant.Type.Int)
            ProjectDropped?.Invoke(data.AsInt32(), DropSlot);
    }
}
