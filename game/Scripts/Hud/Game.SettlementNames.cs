using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
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
    private CanvasLayer? _plateLayer;
    private UiSettlementPlates? _plates;

    /// <summary>The player's name for the settlement, or a generated one until a name is given (docs/MVP/33 §8).</summary>
    private string SettlementLabel(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : DefaultSettlementName(settlementIndex);

    // Cosmetic default names: two word parts per civilization, themed on its emblem (sun, wave, leaf, star, mountain, flame, moon, antler), plus
    // one set for Free Folk. Only the game ever sees them, so determinism and replays are untouched; the first name drawn for a settlement sticks.
    private static readonly string[][] NameStarts =
    {
        new[] { "Sun", "Dawn", "Gold", "Bright", "Amber", "Noon", "Ray", "High" },
        new[] { "Tide", "Reed", "Brook", "Mere", "Salt", "Foam", "Ripple", "Marsh" },
        new[] { "Green", "Oak", "Fern", "Willow", "Elm", "Moss", "Briar", "Linden" },
        new[] { "Star", "Night", "Silver", "Clear", "Far", "Vesper", "Cold", "Lode" },
        new[] { "Stone", "Iron", "High", "Crag", "Gran", "Slate", "Cairn", "Ridge" },
        new[] { "Ember", "Ash", "Cinder", "Red", "Hearth", "Spark", "Char", "Kiln" },
        new[] { "Moon", "Pale", "Mist", "Dusk", "Shade", "Wan", "Haze", "Hollow" },
        new[] { "Elk", "Hart", "Thorn", "Wolf", "Horn", "Stag", "Bracken", "Bear" },
        new[] { "Old", "Lone", "Grey", "Wild", "Hidden", "Rough", "Low", "Far" },
    };

    private static readonly string[][] NameEnds =
    {
        new[] { "hold", "field", "stead", "rise", "gate", "ford", "crest", "mark" },
        new[] { "haven", "ford", "mouth", "wick", "wharf", "pool", "bank", "strand" },
        new[] { "mere", "wood", "glen", "dell", "grove", "hollow", "wick", "brook" },
        new[] { "watch", "reach", "fall", "light", "hold", "point", "gaze", "mark" },
        new[] { "keep", "hold", "fell", "peak", "scar", "rock", "holm", "crown" },
        new[] { "fall", "forge", "hearth", "brand", "glow", "heath", "stack", "fire" },
        new[] { "rest", "vale", "mere", "drift", "hollow", "wake", "well", "ward" },
        new[] { "wood", "run", "moor", "den", "wald", "track", "fell", "hurst" },
        new[] { "camp", "fire", "stead", "post", "barrow", "ring", "hall", "wick" },
    };

    private readonly Dictionary<int, string> _defaultNames = new();
    private readonly HashSet<string> _defaultNamesTaken = new();

    private string DefaultSettlementName(int settlementIndex)
    {
        if (_defaultNames.TryGetValue(settlementIndex, out string? known)) return known;
        string plain = $"Settlement {settlementIndex + 1}";
        MatchFrame? frame = _current;
        int row = frame is null ? -1 : frame.IndexOfSettlement(settlementIndex);
        if (frame is null || row < 0) return plain;
        bool wild = (frame.SettlementFlags[row] & (ushort)(SettlementViewFlags.FreeFolk | SettlementViewFlags.TinFolk)) != 0 || frame.SettlementCiv[row] < 0;
        int set = wild ? NameStarts.Length - 1 : Math.Clamp((int)frame.SettlementCiv[row], 0, NameStarts.Length - 2);
        string[] starts = NameStarts[set], ends = NameEnds[set];
        string name = plain;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            uint h = unchecked((uint)(settlementIndex + 1) * 2654435761u + (uint)set * 40503u + (uint)attempt * 2246822519u);
            h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13;
            string candidate = starts[(int)(h % (uint)starts.Length)] + ends[(int)((h >> 8) % (uint)ends.Length)];
            if (_defaultNamesTaken.Contains(candidate)) continue;
            name = candidate;
            break;
        }
        if (name == plain) return plain;
        _defaultNamesTaken.Add(name);
        _defaultNames[settlementIndex] = name;
        return name;
    }

    /// <summary>The name a player gave, or empty when the settlement still has its default label.</summary>
    private string CustomSettlementName(int settlementIndex)
        => _settlementNames.TryGetValue(settlementIndex, out string? name) ? name : "";

    /// <summary>The map overlay that draws every settlement's name plate (docs/MVP/33). Created on first use, under the HUD and over the world.</summary>
    private UiSettlementPlates EnsurePlates()
    {
        if (_plates is not null && GodotObject.IsInstanceValid(_plates)) return _plates;
        _plateLayer = new CanvasLayer { Layer = 5, Name = "SettlementPlateLayer" };
        AddChild(_plateLayer);
        _plates = new UiSettlementPlates { CaptionFor = PlateCaption };
        _plateLayer.AddChild(_plates);
        return _plates;
    }

    /// <summary>Hands every settlement the current view knows (including remembered enemy sites) to the plate overlay.</summary>
    private void UpdateSettlementMapLabels(MatchFrame frame)
    {
        UiSettlementPlates plates = EnsurePlates();
        plates.Camera = _camera;
        plates.BeginFrame();
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            int settlement = frame.SettlementIndex[i];
            int tile = frame.SettlementTile[i];
            if (settlement < 0 || (uint)tile >= (uint)_terrainHeights.Length) continue;
            Vector3 center = TileCenter(tile % frame.Width, tile / frame.Width);
            plates.Set(settlement, tile, new Vector3(center.X, _terrainHeights[tile], center.Z), SettlementLabel(settlement), SettlementPlate.Facts(frame, i));
        }
        plates.EndFrame();
    }

    /// <summary>The hover line under a selected or hovered plate (docs/MVP/33 §5): what the town is building, whose it is, or when it was last seen.</summary>
    private string? PlateCaption(int settlement)
    {
        MatchFrame? frame = _current;
        int row = frame is null ? -1 : frame.IndexOfSettlement(settlement);
        if (frame is null || row < 0) return null;
        PlateFacts f = SettlementPlate.Facts(frame, row);
        if (f.Empty) return "Abandoned site";
        if (f.Remembered)
        {
            int seasons = Math.Max(0, frame.Season - f.SeenSeason);
            if (seasons < 4) return seasons <= 1 ? "Last seen last season" : $"Last seen {seasons} seasons ago";
            int years = seasons / 4;
            return years == 1 ? "Last seen 1 year ago" : $"Last seen {years} years ago";
        }
        if (f.Relation == PlateRelation.Own)
        {
            string? what = null;
            int kind = frame.SettlementBuildKind[row];
            if (kind >= 0) what = UiText.StructureName(LivingEconomyModel.StructureName(kind));
            else if (frame.Iron.Active && row < frame.Iron.BuildKind.Length && frame.Iron.BuildKind[row] >= 0)
                what = UiText.StructureName(LivingEconomyModel.IronWorkId(frame.Iron.BuildKind[row]));
            if (what is null) return "Nothing under way";
            int done = kind >= 0 ? frame.SettlementBuildPermille[row] : frame.Iron.BuildPermille[row];
            return $"Building {what} · {done / 10}%";
        }
        if (f.Relation == PlateRelation.Wild) return f.TinFolk ? "Tin Folk village" : "Free Folk village";
        string relation = f.Relation switch
        {
            PlateRelation.AtWar => "at war",
            PlateRelation.Allied => "allied",
            PlateRelation.Contacted => "known",
            _ => "not met",
        };
        return $"{CivLabel(frame, f.Civ)} · {relation}";
    }

    /// <summary>Keeps the plate overlay in step with the camera and the pointer; called every rendered frame.</summary>
    private void TickSettlementPlates()
    {
        if (_plates is null) return;
        _plates.Camera = _camera;
        _plates.Mode = _uiSettings.SettlementPlates;
        _plates.SetZoom(_zoom);
        _plates.SetSelectedTile(_pinnedTile);
        bool pointerFree = _hoverActive && !_leftDown && !_rightDown && !_middleDown && !_decreeMapMode && !_beaconMode;
        if (pointerFree)
        {
            Control? over = GetViewport().GuiGetHoveredControl();
            pointerFree = over is null || over == _hudRoot;
        }
        _plates.SetPointer(pointerFree ? _hoverScreen : null);
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
        if (name.Length == 0 || name == $"Settlement {settlementIndex + 1}" || _defaultNames.TryGetValue(settlementIndex, out string? generated) && name == generated) _settlementNames.Remove(settlementIndex);
        else _settlementNames[settlementIndex] = name;
        _plates?.SetName(settlementIndex, SettlementLabel(settlementIndex));
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
