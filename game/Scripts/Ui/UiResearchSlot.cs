using System;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>A research slot card that can drag its project onto another slot card.</summary>
public partial class UiResearchSlot : Control
{
    public int Slot { get; }
    public Func<int>? ProjectAtSlot { get; set; }
    public Func<bool>? CanDragProject { get; set; }
    public Func<int, string>? ProjectName { get; set; }
    public Func<int, int, bool>? CanDropResearchSlot { get; set; }
    public Action<int, int>? ResearchSlotDropped { get; set; }

    public UiResearchSlot(int slot)
    {
        Slot = slot;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (CanDragProject?.Invoke() != true || ProjectAtSlot is null) return default;
        int project = ProjectAtSlot();
        if (project < 0) return default;

        string name = ProjectName?.Invoke(project) ?? "research project";
        var preview = new PanelContainer
        {
            ThemeTypeVariation = "InnerCard",
            CustomMinimumSize = new Vector2(240, 44),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        preview.AddChild(UiKit.Margin(UiKit.Text($"Move {name}", "BodyStrong", ellipsis: true), 12, 6, 12, 6));
        SetDragPreview(preview);
        return Slot;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.Int) return false;
        int from = data.AsInt32();
        return from != Slot && CanDropResearchSlot?.Invoke(from, Slot) == true;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.VariantType != Variant.Type.Int) return;
        int from = data.AsInt32();
        if (from != Slot && CanDropResearchSlot?.Invoke(from, Slot) == true)
            ResearchSlotDropped?.Invoke(from, Slot);
    }
}
