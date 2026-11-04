using System.Collections.Generic;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// Player-chosen settlement names. They are cosmetic and live on the Godot side, keyed by the sim's settlement index; the
/// simulation never sees them, so they cannot touch determinism or replays. They travel with quick-saves.
/// </summary>
public partial class Game
{
    public const int MaxSettlementNameLength = 24;

    private readonly Dictionary<int, string> _settlementNames = new();
    private readonly Dictionary<int, Label3D> _settlementMapLabels = new();

    /// <summary>The player's name for the settlement, or "Settlement N" until one is given.</summary>
    private string SettlementLabel(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : $"Settlement {settlementIndex + 1}";

    /// <summary>The name a player gave, or empty when the settlement still has its default label.</summary>
    private string CustomSettlementName(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : "";

    /// <summary>Names every settlement the current view knows, including remembered enemy sites.</summary>
    private void UpdateSettlementMapLabels(MatchFrame frame)
    {
        var present = new HashSet<int>();
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            int settlement = frame.SettlementIndex[i];
            int tile = frame.SettlementTile[i];
            if (settlement < 0 || (uint)tile >= (uint)_terrainHeights.Length) continue;
            present.Add(settlement);
            if (!_settlementMapLabels.TryGetValue(settlement, out Label3D? label) || !GodotObject.IsInstanceValid(label))
            {
                label = new Label3D
                {
                    Name = $"SettlementMapLabel_{settlement}",
                    FontSize = 48,
                    OutlineSize = 12,
                    PixelSize = 0.00055f,
                    Modulate = new Color("#fff1cf"),
                    OutlineModulate = new Color(0.055f, 0.045f, 0.03f, 0.98f),
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    FixedSize = true,
                    NoDepthTest = true,
                    Shaded = false,
                    DoubleSided = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    RenderPriority = 10,
                };
                AddChild(label);
                _settlementMapLabels[settlement] = label;
            }
            label.Text = SettlementLabel(settlement);
            Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
            label.Position = new Vector3(center.X, _terrainHeights[tile] + 1.35f, center.Z);
            label.Visible = true;
        }

        foreach ((int settlement, Label3D label) in _settlementMapLabels)
            if (!present.Contains(settlement) && GodotObject.IsInstanceValid(label)) label.Visible = false;
    }

    private static string CleanSettlementName(string? raw)
    {
        if (raw is null) return "";
        var buffer = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
            if (!char.IsControl(c)) buffer.Append(c);
        string trimmed = buffer.ToString().Trim();
        return trimmed.Length > MaxSettlementNameLength ? trimmed[..MaxSettlementNameLength].TrimEnd() : trimmed;
    }

    /// <summary>Sets or, with an empty name, clears a settlement's name; returns the name now in effect ("" when cleared).</summary>
    private string RenameSettlement(int settlementIndex, string? raw)
    {
        string name = CleanSettlementName(raw);
        if (name.Length == 0 || name == $"Settlement {settlementIndex + 1}") _settlementNames.Remove(settlementIndex);
        else _settlementNames[settlementIndex] = name;
        if (_settlementMapLabels.TryGetValue(settlementIndex, out Label3D? label) && GodotObject.IsInstanceValid(label))
            label.Text = SettlementLabel(settlementIndex);
        return name;
    }

    private List<KeyValuePair<int, string>> SettlementNamesForSave()
    {
        var names = new List<KeyValuePair<int, string>>(_settlementNames);
        names.Sort((a, b) => a.Key.CompareTo(b.Key));
        return names;
    }

    private void RestoreSettlementNames(IReadOnlyList<KeyValuePair<int, string>> names)
    {
        _settlementNames.Clear();
        foreach (KeyValuePair<int, string> pair in names)
        {
            string name = CleanSettlementName(pair.Value);
            if (pair.Key >= 0 && name.Length > 0) _settlementNames[pair.Key] = name;
        }
        if (_current is not null)
        {
            UpdateSettlementMapLabels(_current);
            if (_diplomacyOpen) RefreshDiplomacy(_current);
        }
    }
}
