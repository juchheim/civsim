using Godot;

namespace CivSim.Game.Ui;

/// <summary>A civilization in the diplomacy list: emblem, name, one status line. Toggled when selected.</summary>
public partial class UiCivRow : Button
{
    private readonly UiEmblem _emblem = new(0, 26);
    private readonly Label _name = UiKit.Text("", "BodyStrong", ellipsis: true);
    private readonly Label _status = UiKit.Text("", "Caption", ellipsis: true);
    public int Civ { get; private set; } = -1;

    public UiCivRow()
    {
        ThemeTypeVariation = "RowButton";
        ToggleMode = true;
        FocusMode = FocusModeEnum.All;
        CustomMinimumSize = new Vector2(0, 55);
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", UiTokens.Sp2);
        margin.AddThemeConstantOverride("margin_right", UiTokens.Sp2);
        var row = UiKit.Row(UiTokens.Sp2);
        row.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(_emblem);
        var column = UiKit.Column(0);
        column.MouseFilter = MouseFilterEnum.Ignore;
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        column.AddChild(_name);
        column.AddChild(_status);
        row.AddChild(column);
        margin.AddChild(row);
        AddChild(margin);
    }

    public void Set(int civ, string name, string status)
    {
        Civ = civ;
        _emblem.Civ = civ;
        UiKit.SetText(_name, name);
        UiKit.SetText(_status, status);
    }
}
