using System.Collections.Generic;

namespace CivSim.Game;

/// <summary>
/// Player-chosen settlement names. They are cosmetic and live on the Godot side, keyed by the sim's settlement index; the
/// simulation never sees them, so they cannot touch determinism or replays. They travel with quick-saves.
/// </summary>
public partial class Game
{
    public const int MaxSettlementNameLength = 24;

    private readonly Dictionary<int, string> _settlementNames = new();

    /// <summary>The player's name for the settlement, or "Settlement N" until one is given.</summary>
    private string SettlementLabel(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : $"Settlement {settlementIndex + 1}";

    /// <summary>The name a player gave, or empty when the settlement still has its default label.</summary>
    private string CustomSettlementName(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : "";

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
    }
}
