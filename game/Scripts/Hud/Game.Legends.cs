using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Legends;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// Legends in the HUD (docs/MVP/40 §6, phase L5): a Chronicle line and a toast when one of the viewer's people becomes a Legend, a Chronicle line when one dies (and whether a relief or a
/// statue keeps their memory), and a "Legends" section on the people sheet. Everything is read from <see cref="MatchFrame"/>'s Legend arrays; the HUD finds what is new by comparing
/// with the figure ids it has already told the player about, so the simulation's event system is untouched.
/// </summary>
public partial class Game
{
    private readonly HashSet<long> _legendsAnnounced = new(), _legendDeathsAnnounced = new();
    private bool _legendsPrimed;

    private VBoxContainer? _legendsList;
    private Label? _legendsEmpty;
    private int _legendsSignature = int.MinValue;

    private static string LegendDeedWord(LegendDeed deed) => deed switch
    {
        LegendDeed.Innovator => "Innovator", LegendDeed.First => "First discoverer", LegendDeed.Founder => "Founder", LegendDeed.Battle => "Hero of the field",
        LegendDeed.Famine => "Kept faith through famine", LegendDeed.Wonder => "Builder of a Wonder", LegendDeed.LongLife => "Elder of the folk", _ => "",
    };

    /// <summary>What the Legend did, in a clause that follows their name ("Ama first practised Cooking").</summary>
    private string LegendDeedPhrase(LegendDeed deed, int detail, int settlement) => deed switch
    {
        LegendDeed.Innovator => $"was the first of your people to practise {ProjectName(detail)}",
        LegendDeed.First => $"was the first in the world to learn {ProjectName(detail)}",
        LegendDeed.Founder => $"led the people who founded {SettlementLabel(detail)}",
        LegendDeed.Battle => $"held the field at {SettlementLabel(detail)}",
        LegendDeed.Famine => $"held {SettlementLabel(settlement)} together through {detail} hungry seasons",
        LegendDeed.Wonder => $"raised a Wonder at {SettlementLabel(detail)}",
        LegendDeed.LongLife => $"lived to a great age ({detail})",
        _ => "did something that will be remembered",
    };

    private string LegendFullName(MatchFrame frame, int k) => $"{LegendNames.Compose(frame.LegendName[k])} of the {UiText.CivName(Math.Max(0, frame.ViewerCiv))}";

    private static int SettlementTileOf(MatchFrame frame, int settlement)
    {
        for (int w = 0; w < frame.SettlementCount; w++)
            if (frame.SettlementIndex[w] == settlement) return frame.SettlementTile[w];
        return -1;
    }

    private int _legendDebugCalls;

    /// <summary><c>--legend-debug</c>: after a few frames, make three Legends appear in the frame (a founder, a First and a hero), and later let the hero die with a relief to remember them.</summary>
    private void FakeDebugLegends(MatchFrame frame, bool advance = true)
    {
        if (advance) _legendDebugCalls++;
        if (_legendDebugCalls < 4 || frame.SettlementCount == 0) return;
        int settlement = frame.SettlementIndex[0];
        if (frame.LegendFigure.Length < 3)
        {
            frame.LegendFigure = new long[3]; frame.LegendSettlement = new int[3]; frame.LegendBorn = new int[3]; frame.LegendDied = new int[3];
            frame.LegendDeed = new byte[3]; frame.LegendDetail = new int[3]; frame.LegendName = new int[3]; frame.LegendRemembered = new byte[3];
        }
        bool heroDied = _legendDebugCalls >= 12;
        for (int k = 0; k < 3; k++)
        {
            frame.LegendFigure[k] = 900_000_001L + k;
            frame.LegendSettlement[k] = settlement;
            frame.LegendBorn[k] = 10 + k;
            frame.LegendDied[k] = k == 2 && heroDied ? 40 : -1;
            frame.LegendDeed[k] = (byte)(k == 0 ? LegendDeed.Founder : k == 1 ? LegendDeed.First : LegendDeed.Battle);
            frame.LegendDetail[k] = k == 1 ? Math.Max(0, _catalog?.IndexOf("FIR-01") ?? 0) : settlement;
            frame.LegendName[k] = 37 + 211 * k;
            frame.LegendRemembered[k] = (byte)(k == 2 && heroDied ? 1 : 0);
        }
        frame.LegendCount = 3;
    }

    /// <summary>Scrolls the people sheet so the Legends section is on screen (for captures).</summary>
    private void ShowLegendsSection()
    {
        Node? node = _legendsSectionPanel;
        while (node is not null && node is not ScrollContainer) node = node.GetParent();
        if (node is ScrollContainer scroll && _legendsSectionPanel is not null) scroll.EnsureControlVisible(_legendsSectionPanel);
    }

    /// <summary>Called with every new frame: tells the player of each new Legend and each Legend who has died, once.</summary>
    private void ProcessLegends(MatchFrame frame)
    {
        if (LegendDebug) FakeDebugLegends(frame);
        int viewer = Math.Max(0, frame.ViewerCiv);
        for (int k = 0; k < frame.LegendCount; k++)
        {
            long id = frame.LegendFigure[k];
            var deed = (LegendDeed)frame.LegendDeed[k];
            int settlement = frame.LegendSettlement[k], tile = SettlementTileOf(frame, settlement);
            string name = LegendFullName(frame, k);
            if (_legendsAnnounced.Add(id) && _legendsPrimed)
            {
                string line = $"{name} {LegendDeedPhrase(deed, frame.LegendDetail[k], settlement)}, and is now told of as a Legend.";
                AddChronicle(LegendEvent(frame, viewer, settlement, tile, id), line, ChronicleClass.People, true);
                ShowToast("A Legend is told of", ToastKind.Milestone, $"{name} {LegendDeedPhrase(deed, frame.LegendDetail[k], settlement)}.", tile,
                    tile >= 0 ? "Show" : "", tile >= 0 ? () => FocusTile(tile) : null, key: $"legend-{id}");
            }
            if (frame.LegendDied[k] >= 0 && _legendDeathsAnnounced.Add(id) && _legendsPrimed)
            {
                bool kept = frame.LegendRemembered[k] != 0;
                string line = kept ? $"{name} has died. A carved memory keeps their name in {SettlementLabel(settlement)}."
                                   : $"{name} has died. {UiText.Sentence(SettlementLabel(settlement))} mourns.";
                AddChronicle(LegendEvent(frame, viewer, settlement, tile, id), line, ChronicleClass.People, true);
            }
            else if (frame.LegendDied[k] >= 0) _legendDeathsAnnounced.Add(id);
        }
        _legendsPrimed = true; // the Legends already there when the game opened (or a save loaded) are known without being announced
    }

    private static ViewEvent LegendEvent(MatchFrame frame, int viewer, int settlement, int tile, long figure) => new()
    {
        Tick = frame.Tick, Kind = ViewEventKind.None, Civ = (sbyte)viewer, Other = -1, Figure = figure, Project = -1, Settlement = settlement, Tile = tile, Tier = ViewEventTier.Milestone,
    };

    // ---- the people sheet's Legends section --------------------------------------------------------------------------------------------------

    private PeopleSection BuildLegendsSection()
    {
        PeopleSection section = NewPeopleSection("Legends", "LegendsSection", "People your folk tell stories about. Living Legends steady a town and its fighters; a carved relief keeps one in memory.");
        _legendsEmpty = UiKit.Text("No one has become a Legend yet. Founding towns, learning something first and holding the field make a person's name.", "Body", wrap: true);
        _legendsList = UiKit.Column(UiTokens.Sp2);
        section.Body.AddChild(_legendsEmpty);
        section.Body.AddChild(_legendsList);
        _legendsSectionPanel = section.Panel;
        return section;
    }

    private Control? _legendsSectionPanel;

    private void RefreshLegendsSection(MatchFrame frame)
    {
        if (_legendsList is null || _legendsEmpty is null || _legendsSectionPanel is null) return;
        if (LegendDebug) FakeDebugLegends(frame, advance: false);
        int signature = frame.LegendCount;
        for (int k = 0; k < frame.LegendCount; k++) signature = signature * 31 + (frame.LegendDied[k] >= 0 ? 7 : 3) + frame.LegendRemembered[k] * 11 + frame.LegendSettlement[k];
        if (signature == _legendsSignature) return;
        _legendsSignature = signature;
        foreach (Node child in _legendsList.GetChildren())
        {
            _legendsList.RemoveChild(child);
            child.QueueFree();
        }
        UiKit.SetVisible(_legendsEmpty, frame.LegendCount == 0);
        // Living first, then the remembered, newest at the top.
        for (int pass = 0; pass < 2; pass++)
            for (int k = frame.LegendCount - 1; k >= 0; k--)
            {
                bool living = frame.LegendDied[k] < 0;
                if (living != (pass == 0)) continue;
                var deed = (LegendDeed)frame.LegendDeed[k];
                int settlement = frame.LegendSettlement[k], tile = SettlementTileOf(frame, settlement);
                var row = UiKit.Row(UiTokens.Sp3);
                var texts = UiKit.Column(UiTokens.Sp1);
                texts.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                texts.AddChild(UiKit.Text(LegendNames.Compose(frame.LegendName[k]) + (living ? "" : "  ·  died"), "BodyStrong"));
                string where = SettlementLabel(settlement);
                string status = living ? $"{LegendDeedWord(deed)} · lives in {where}"
                    : frame.LegendRemembered[k] != 0 ? $"{LegendDeedWord(deed)} · remembered in stone at {where}" : $"{LegendDeedWord(deed)} · mourned in {where}";
                texts.AddChild(UiKit.Text(status, "Caption", wrap: true));
                texts.AddChild(UiKit.Text(UiText.Sentence($"{LegendDeedPhrase(deed, frame.LegendDetail[k], settlement)}."), "BodySecondary", wrap: true));
                row.AddChild(texts);
                if (tile >= 0 && living) row.AddChild(UiKit.Ghost("Show", () => FocusTile(tile), "Look at where they live."));
                _legendsList.AddChild(row);
            }
    }
}
