using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// The "Building" tab of the Your people sheet (docs/MVP/30): what a town is building, what it is waiting on and why, what the player can do about it, and a
/// list of other things the town could build first. Every sentence comes from <see cref="BuildText"/> (tested headless); this file only lays it out and
/// sends <see cref="PlayerCommands.SetBuildFocus"/> orders. Nothing here writes simulation state.
/// </summary>
public partial class Game
{
    private readonly List<int> _buildSettlementIds = new();
    private OptionButton? _buildPicker;
    private bool _rebuildingBuildPicker;
    private int _buildSelected = -1;
    private Label? _buildHeadline, _buildFocusLabel, _buildAltNote, _buildNothing;
    private VBoxContainer? _buildNowBox, _buildExistingBox, _buildWaitingBox, _buildBlockedBox, _buildAltBox, _buildNotBuiltBox;
    private IReadOnlyList<BuildText.NotBuilt> _buildNotBuilt = Array.Empty<BuildText.NotBuilt>();
    private Control? _buildFocusRow;
    private UiButton? _buildClear;
    private BuildOutlook? _buildShown;
    private bool _buildShownCanIssue;
    private (int Settlement, int Code, bool Skip) _buildPending = (-1, -1, false);
    private int _buildShownPendingCode = -2;
    private int _buildTownsKey;
    private int _buildShownExistingKey = int.MinValue;

    private BuildNames? _buildNamesCache;

    private BuildNames BuildNamesForUi => _buildNamesCache ??= new BuildNames(
        UiText.StructureName, UiText.GoodName,
        id => _catalog is { } catalog && catalog.IndexOf(id) is var at and >= 0 ? catalog.Projects[at].Name : id);

    private Control BuildPeopleBuildingPane()
    {
        PeopleSection now = NewPeopleSection("What this town is building", "BuildingNow",
            "Three things can be under way at once: homes, stores and defenses; Iron works; and civic buildings.");
        var pickRow = UiKit.Row(UiTokens.Sp2);
        var townLabel = UiKit.Cell("Town", "BodySecondary", 60);
        townLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        pickRow.AddChild(townLabel);
        _buildPicker = new OptionButton { CustomMinimumSize = new Vector2(220, UiTokens.ControlS), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _buildPicker.ItemSelected += selected =>
        {
            if (_rebuildingBuildPicker) return;
            int index = (int)selected;
            if ((uint)index < (uint)_buildSettlementIds.Count)
            {
                _buildSelected = _buildSettlementIds[index];
                _buildShown = null;
                if (_current is not null) RefreshPeopleBuilding(_current);
            }
        };
        pickRow.AddChild(_buildPicker);
        now.Body.AddChild(pickRow);
        _buildHeadline = UiKit.Text("", "Body", wrap: true);
        now.Body.AddChild(_buildHeadline);
        _buildNowBox = UiKit.Column(UiTokens.Sp2);
        now.Body.AddChild(_buildNowBox);
        _buildNothing = UiKit.Text("Nothing is under way in this town.", "CaptionTertiary", wrap: true);
        now.Body.AddChild(_buildNothing);
        var focus = UiKit.Row(UiTokens.Sp2);
        _buildFocusLabel = UiKit.Text("", "BodySecondary", wrap: true);
        focus.AddChild(_buildFocusLabel);
        _buildClear = UiKit.Secondary("Clear", () => SendBuildFocus(-1, false), "Give the choice back to the town.", UiGlyph.Close, UiTokens.ControlS);
        focus.AddChild(_buildClear);
        _buildFocusRow = focus;
        now.Body.AddChild(focus);

        PeopleSection existing = NewPeopleSection("Already built in this town", "BuildingExisting",
            "Researched buildings and works standing here. Choose another town above to inspect it.");
        _buildExistingBox = UiKit.Column(UiTokens.Sp1);
        existing.Body.AddChild(_buildExistingBox);

        PeopleSection waiting = NewPeopleSection("What it is waiting on", "BuildingWaiting",
            "Things the town wants and has learned how to build but cannot pay for yet. Each says what is short, where it would come from and what you can do.");
        _buildWaitingBox = UiKit.Column(UiTokens.Sp3);
        waiting.Body.AddChild(_buildWaitingBox);

        PeopleSection blocked = NewPeopleSection("Learned, but cannot be built here", "BuildingBlocked",
            "Your people know how to build these, but a rule rules them out in this town. Where that can change, it says how.");
        _buildBlockedBox = UiKit.Column(UiTokens.Sp2);
        blocked.Body.AddChild(_buildBlockedBox);

        PeopleSection alternatives = NewPeopleSection("Build something else first", "BuildingAlternatives",
            "Ask this town to build one of these next. It holds no materials and no workers back for it, and it ends when the building stands, after six years, or when you clear it.");
        _buildAltNote = UiKit.Text("Shelter, warmth, food stores and the first fields always come first, so a request can never leave the town cold, unhoused or hungry.",
            "CaptionTertiary", wrap: true);
        alternatives.Body.AddChild(_buildAltNote);
        _buildAltBox = UiKit.Column(UiTokens.Sp2);
        alternatives.Body.AddChild(_buildAltBox);

        PeopleSection unbuilt = NewPeopleSection("Learned, but built in none of your towns", "BuildingNotBuilt",
            "The structures your people can build that no town of yours has raised yet, and what holds them back most often.");
        _buildNotBuiltBox = UiKit.Column(UiTokens.Sp2);
        unbuilt.Body.AddChild(_buildNotBuiltBox);

        var body = UiKit.Row(UiTokens.Sp3);
        body.AddChild(PeopleColumn(0.56f, now, existing, waiting, blocked));
        body.AddChild(PeopleColumn(0.44f, alternatives, unbuilt));
        ScrollContainer scroll = PeopleScroll(body);
        _buildScroll = scroll;
        return scroll;
    }

    private ScrollContainer? _buildScroll;
    private readonly HashSet<string> _buildCausesShown = new();

    // ---- refresh ------------------------------------------------------------------------------------

    private void RefreshPeopleBuilding(MatchFrame frame)
    {
        if (_buildPicker is null) return;
        // The towns that can be asked: the viewer's own, in frame order. A single town picked in the sheet's scope selector wins.
        var ids = new List<int>();
        var rows = new Dictionary<int, int>();
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!IsOwnPeopleSettlement(frame, w)) continue;
            ids.Add(frame.SettlementIndex[w]);
            rows[frame.SettlementIndex[w]] = w;
        }
        if (_peopleScopeSelected >= 0 && ids.Contains(_peopleScopeSelected)) _buildSelected = _peopleScopeSelected;
        if (!ids.Contains(_buildSelected)) _buildSelected = ids.Count > 0 ? ids[0] : -1;
        if (!SameList(ids, _buildSettlementIds))
        {
            _buildSettlementIds.Clear();
            _buildSettlementIds.AddRange(ids);
            _rebuildingBuildPicker = true;
            _buildPicker.Clear();
            foreach (int id in ids)
                _buildPicker.AddItem($"{SettlementLabel(id)} · {frame.SettlementPopulation[rows[id]]:N0} people", id);
            _rebuildingBuildPicker = false;
        }
        int at = _buildSettlementIds.IndexOf(_buildSelected);
        _rebuildingBuildPicker = true;
        if (at >= 0 && _buildPicker.Selected != at) _buildPicker.Select(at);
        _rebuildingBuildPicker = false;
        _buildPicker.Disabled = _buildSettlementIds.Count <= 1;

        int selectedRow = _buildSelected >= 0 && rows.TryGetValue(_buildSelected, out int selectedAt) ? selectedAt : -1;
        BuildOutlook? outlook = selectedRow >= 0 && (uint)selectedRow < (uint)frame.SettlementBuild.Length
            ? frame.SettlementBuild[selectedRow] : null;
        bool canIssue = _session?.CanIssueCommands ?? false;
        if (_buildPending.Settlement == _buildSelected && outlook is not null &&
            outlook.FocusCode == _buildPending.Code && outlook.FocusIsSkip == _buildPending.Skip) _buildPending = (-1, -1, false);
        if (_buildPending.Settlement >= 0 && _buildPending.Settlement != _buildSelected) _buildPending = (-1, -1, false);
        int pendingCode = _buildPending.Settlement == _buildSelected ? _buildPending.Code : -2;
        var allTowns = new List<BuildOutlook?>();
        foreach (int id in ids) allTowns.Add(frame.SettlementBuild[rows[id]]);
        int townsKey = 0;
        foreach (BuildOutlook? town in allTowns) townsKey = unchecked(townsKey * 31 + (town?.Season ?? -1) + 17 * (town?.FocusCode ?? 0));
        bool townsChanged = townsKey != _buildTownsKey;
        int existingKey = ExistingItemsKey(frame, selectedRow);
        if (ReferenceEquals(outlook, _buildShown) && canIssue == _buildShownCanIssue && pendingCode == _buildShownPendingCode && !townsChanged && existingKey == _buildShownExistingKey) return;
        _buildTownsKey = townsKey;
        _buildShownExistingKey = existingKey;
        _buildNotBuilt = BuildText.NotBuiltAnywhere(allTowns, BuildNamesForUi);
        _buildShown = outlook;
        _buildShownCanIssue = canIssue;
        _buildShownPendingCode = pendingCode;
        RenderBuildOutlook(outlook, canIssue, frame, selectedRow);
    }

    private static bool SameList(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static void ClearChildren(Node box)
    {
        foreach (Node child in box.GetChildren())
        {
            box.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void RenderBuildOutlook(BuildOutlook? o, bool canIssue, MatchFrame frame, int settlementRow)
    {
        ClearChildren(_buildNowBox!); ClearChildren(_buildExistingBox!); ClearChildren(_buildWaitingBox!); ClearChildren(_buildBlockedBox!); ClearChildren(_buildAltBox!); ClearChildren(_buildNotBuiltBox!);
        RenderNotBuilt(BuildNamesForUi);
        RenderExistingHere(frame, settlementRow);
        BuildNames names = BuildNamesForUi;
        if (o is null || o.Items.Length == 0)
        {
            UiKit.SetText(_buildHeadline!, "Reading this town's building rules...");
            _buildNothing!.Visible = false;
            _buildFocusRow!.Visible = false;
            _buildWaitingBox!.AddChild(UiKit.Text("Nothing is waiting.", "CaptionTertiary"));
            _buildBlockedBox!.AddChild(UiKit.Text("Nothing is ruled out.", "CaptionTertiary"));
            _buildAltBox!.AddChild(UiKit.Text("Your people have not learned anything this town could build yet.", "CaptionTertiary", wrap: true));
            return;
        }

        _buildCausesShown.Clear();
        UiKit.SetText(_buildHeadline!, BuildText.Headline(o, names));
        Color tone = o.Headline switch { BuildHeadline.Building => UiTokens.Success, BuildHeadline.WaitingOnMaterials => UiTokens.Warning, _ => UiTokens.TextSecondary };
        if (_buildHeadline!.GetThemeColor("font_color") != tone) _buildHeadline.AddThemeColorOverride("font_color", tone);

        // What is under way, one row per slot.
        bool any = false;
        foreach (BuildTrack track in new[] { BuildTrack.Basic, BuildTrack.Iron, BuildTrack.Civic })
        {
            BuildItem? item = o.CurrentOf(track);
            if (item is null) continue;
            any = true;
            _buildNowBox!.AddChild(CurrentRow(track, item, names));
        }
        _buildNothing!.Visible = !any;

        // The player's choice for this town.
        string focusText = "";
        int shownCode = o.FocusCode;
        bool shownSkip = o.FocusIsSkip;
        bool pending = false;
        if (_buildPending.Settlement == _buildSelected && (_buildPending.Code != shownCode || _buildPending.Skip != shownSkip))
        {
            shownCode = _buildPending.Code; shownSkip = _buildPending.Skip; pending = true;
        }
        if (shownCode >= 0)
        {
            string name = BuildText.NameOfCode(shownCode, names);
            string until = pending ? "The order is sent and takes effect when time runs." : $"Lasts until Year {Math.Max(1, o.FocusUntil / 4 + 1)}, or until it is built.";
            focusText = shownSkip ? $"You set the {name.ToLowerInvariant()} aside for now. {until}" : $"You asked for the {name.ToLowerInvariant()} next. {until}";
        }
        else if (_buildPending.Settlement == _buildSelected && _buildPending.Code < 0)
            focusText = "The order is sent and takes effect when time runs.";
        _buildFocusRow!.Visible = focusText.Length > 0;
        UiKit.SetText(_buildFocusLabel!, focusText);
        _buildClear!.Visible = shownCode >= 0;
        _buildClear.Disabled = !canIssue;

        // Waiting on: the structures that are short of something, the current one first.
        var waiting = new List<BuildItem>();
        foreach (BuildItem item in o.Items)
            if (item.Status == BuildStatus.Current && item.AnyShort) waiting.Add(item);
        foreach (BuildItem item in o.Items)
            if (item.Status == BuildStatus.Short) waiting.Add(item);
        const int shown = 7;
        for (int i = 0; i < Math.Min(shown, waiting.Count); i++) _buildWaitingBox!.AddChild(WaitingCard(waiting[i], o, names, canIssue));
        if (waiting.Count > shown)
            _buildWaitingBox!.AddChild(UiKit.Text($"and {waiting.Count - shown} more, shorter on the same goods.", "CaptionTertiary"));
        if (waiting.Count == 0)
            _buildWaitingBox!.AddChild(UiKit.Text(o.Headline == BuildHeadline.NothingWanted
                ? "Nothing: the town wants nothing it cannot pay for." : "Nothing the town wants is short of anything.", "CaptionSuccess", wrap: true));

        // Ruled out here.
        int blocked = 0;
        foreach (BuildItem item in o.Items)
        {
            if (item.Status != BuildStatus.Blocked) continue;
            blocked++;
            _buildBlockedBox!.AddChild(BlockedRow(item, names, canIssue));
        }
        if (blocked == 0) _buildBlockedBox!.AddChild(UiKit.Text("Nothing your people have learned is ruled out in this town.", "CaptionSuccess", wrap: true));

        // Alternatives, by slot.
        bool anyAlt = false;
        foreach (BuildTrack track in new[] { BuildTrack.Basic, BuildTrack.Civic, BuildTrack.Iron })
        {
            var rows = new List<BuildItem>();
            foreach (BuildItem item in o.Items) if (item.Track == track && item.CanFocus) rows.Add(item);
            if (rows.Count == 0) continue;
            anyAlt = true;
            rows.Sort((a, b) => (a.AnyShort ? 1 : 0).CompareTo(b.AnyShort ? 1 : 0));
            _buildAltBox!.AddChild(PeopleSubhead(BuildText.Track(track).ToUpperInvariant()));
            foreach (BuildItem item in rows) _buildAltBox.AddChild(AlternativeRow(item, o, names, canIssue));
        }
        if (!anyAlt) _buildAltBox!.AddChild(UiKit.Text("Nothing else can be asked of this town right now.", "CaptionTertiary", wrap: true));
    }

    private void RenderExistingHere(MatchFrame frame, int settlementRow)
    {
        if ((uint)settlementRow >= (uint)frame.SettlementCount)
        {
            _buildExistingBox!.AddChild(UiKit.Text("No settlement selected.", "CaptionTertiary"));
            return;
        }

        bool any = false;
        int structureBase = settlementRow * MatchFrame.StructureKinds;
        var basic = UiKit.Column(UiTokens.Sp1 / 2);
        for (int kind = 0; kind < MatchFrame.StructureKinds; kind++)
        {
            int count = frame.SettlementStructures[structureBase + kind];
            if (count == 0) continue;
            if (!any)
            {
                _buildExistingBox!.AddChild(PeopleSubhead("HOMES AND PUBLIC WORKS"));
                any = true;
            }
            basic.AddChild(BuiltItemRow(UiText.StructureName(LivingEconomyModel.StructureName(kind)), count));
        }
        if (basic.GetChildCount() > 0) _buildExistingBox!.AddChild(basic);

        bool hasIron = false;
        var iron = UiKit.Column(UiTokens.Sp1 / 2);
        int ironBase = settlementRow * frame.Iron.WorkKinds;
        if (frame.Iron.Active && frame.Iron.WorkKinds > 0)
        {
            for (int kind = 0; kind < frame.Iron.WorkKinds; kind++)
            {
                int count = frame.Iron.Works[ironBase + kind];
                if (count == 0) continue;
                if (!hasIron)
                {
                    _buildExistingBox!.AddChild(PeopleSubhead("IRON WORKS"));
                    hasIron = true;
                    any = true;
                }
                iron.AddChild(BuiltItemRow(UiText.StructureName(LivingEconomyModel.IronWorkId(kind)), count));
            }
        }
        if (iron.GetChildCount() > 0) _buildExistingBox!.AddChild(iron);

        bool hasCivic = false;
        var civic = UiKit.Column(UiTokens.Sp1 / 2);
        int civicBase = settlementRow * MatchFrame.CivicStructureKinds;
        for (int kind = 0; kind < MatchFrame.CivicStructureKinds; kind++)
        {
            int count = frame.SettlementCivicStructures[civicBase + kind];
            if (count == 0) continue;
            if (!hasCivic)
            {
                _buildExistingBox!.AddChild(PeopleSubhead("CIVIC BUILDINGS"));
                hasCivic = true;
                any = true;
            }
            civic.AddChild(BuiltItemRow(UiText.StructureName(LivingEconomyModel.CivicStructureName(kind + 1)), count));
        }
        if (civic.GetChildCount() > 0) _buildExistingBox!.AddChild(civic);

        if (!any) _buildExistingBox!.AddChild(UiKit.Text("Nothing has been built here yet.", "CaptionTertiary", wrap: true));
    }

    private static int ExistingItemsKey(MatchFrame frame, int settlementRow)
    {
        if ((uint)settlementRow >= (uint)frame.SettlementCount) return -1;
        int key = 17;
        int structureBase = settlementRow * MatchFrame.StructureKinds;
        for (int kind = 0; kind < MatchFrame.StructureKinds; kind++) key = unchecked(key * 31 + frame.SettlementStructures[structureBase + kind]);
        int civicBase = settlementRow * MatchFrame.CivicStructureKinds;
        for (int kind = 0; kind < MatchFrame.CivicStructureKinds; kind++) key = unchecked(key * 31 + frame.SettlementCivicStructures[civicBase + kind]);
        if (frame.Iron.Active && frame.Iron.WorkKinds > 0)
        {
            int ironBase = settlementRow * frame.Iron.WorkKinds;
            for (int kind = 0; kind < frame.Iron.WorkKinds; kind++) key = unchecked(key * 31 + frame.Iron.Works[ironBase + kind]);
        }
        return key;
    }

    private static Control BuiltItemRow(string name, int count)
    {
        var row = UiKit.Row(UiTokens.Sp2);
        Label item = UiKit.Text(name, "Body", wrap: true);
        item.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(item);
        row.AddChild(UiKit.Cell($"×{count}", "NumBody", 44, right: true));
        return row;
    }

    private void RenderNotBuilt(BuildNames names)
    {
        const int most = 8;
        for (int i = 0; i < Math.Min(most, _buildNotBuilt.Count); i++)
        {
            BuildText.NotBuilt n = _buildNotBuilt[i];
            var line = UiKit.Column(UiTokens.Sp1 / 2);
            var head = UiKit.Row(UiTokens.Sp2);
            head.AddChild(UiKit.Text(names.Structure(n.Id), "BodyStrong"));
            head.AddChild(UiKit.Spacer(expand: true));
            head.AddChild(UiKit.Text(BuildText.Track(n.Track), "EyebrowTertiary"));
            line.AddChild(head);
            line.AddChild(UiKit.Text(n.Reason, "Caption", wrap: true));
            _buildNotBuiltBox!.AddChild(line);
        }
        if (_buildNotBuilt.Count > most) _buildNotBuiltBox!.AddChild(UiKit.Text($"and {_buildNotBuilt.Count - most} more.", "CaptionTertiary"));
        if (_buildNotBuilt.Count == 0) _buildNotBuiltBox!.AddChild(UiKit.Text("Every structure your people can build stands in at least one town.", "CaptionSuccess", wrap: true));
    }

    // ---- pieces -------------------------------------------------------------------------------------

    private Control CurrentRow(BuildTrack track, BuildItem item, BuildNames names)
    {
        var box = UiKit.Column(UiTokens.Sp1);
        var head = UiKit.Row(UiTokens.Sp3);
        head.AddChild(UiKit.Cell(BuildText.Track(track), "EyebrowTertiary", 170, wrap: true));
        head.AddChild(UiKit.Text(names.Structure(item.Id), "BodyStrong"));
        head.AddChild(UiKit.Spacer(expand: true));
        head.AddChild(UiKit.Text(BuildText.Short(item, names), item.AnyShort ? "CaptionWarning" : "Caption"));
        box.AddChild(head);
        var bar = new UiProgressBar(6);
        bar.SetValue(item.Labor > CivSim.Sim.Numerics.Fix64.Zero ? (float)(item.Progress.ToDoubleForDisplay() / item.Labor.ToDoubleForDisplay()) : 0, false);
        bar.Fill = item.AnyShort ? UiTokens.Warning : UiTokens.Accent;
        box.AddChild(bar);
        return box;
    }

    private Control WaitingCard(BuildItem item, BuildOutlook o, BuildNames names, bool canIssue)
    {
        var card = UiKit.Panel("InnerCard", null, UiKit.MouseFilter.Pass);
        var col = UiKit.Column(UiTokens.Sp1);
        card.AddChild(col);
        var head = UiKit.Row(UiTokens.Sp3);
        head.AddChild(UiKit.Text(names.Structure(item.Id), "BodyStrong"));
        head.AddChild(UiKit.Spacer(expand: true));
        head.AddChild(UiKit.Text(item.Status == BuildStatus.Current ? "Under way" : "Wanted", "EyebrowTertiary"));
        col.AddChild(head);
        // Every good that is short, with what the town holds, then where it would come from.
        for (int i = 0; i < item.Needs.Length; i++)
        {
            BuildNeed need = item.Needs[i];
            if (!need.Short) continue;
            var line = UiKit.Row(UiTokens.Sp2);
            line.AddChild(new UiIcon(UiGlyph.Dot, 14, UiTokens.Warning));
            line.AddChild(UiKit.Text(BuildText.GoodName(need.Slot, names), "BodySecondary"));
            line.AddChild(UiKit.Text($"{FormatUnits(need.Have.ToDoubleForDisplay())} of {FormatUnits(need.Need.ToDoubleForDisplay())}", "NumBody"));
            col.AddChild(line);
            string cause = BuildText.Supply(need, item.Supplies[i], names);
            col.AddChild(UiKit.Text(_buildCausesShown.Add(cause) ? cause : "Same cause as above.", "Caption", wrap: true));
        }
        string verdict = BuildText.VerdictAfterCause(item, names);
        if (verdict.Length > 0)
        {
            Color color = item.Verdict == BuildVerdict.Resolvable ? UiTokens.Success : item.Verdict == BuildVerdict.Unresolvable ? UiTokens.Warning : UiTokens.TextSecondary;
            col.AddChild(UiKit.Text(verdict, "Caption", color, wrap: true));
        }
        AddLevers(col, item, o, names, canIssue);
        return card;
    }

    private Control BlockedRow(BuildItem item, BuildNames names, bool canIssue)
    {
        var col = UiKit.Column(UiTokens.Sp1);
        var head = UiKit.Row(UiTokens.Sp3);
        head.AddChild(UiKit.Text(names.Structure(item.Id), "BodyStrong"));
        head.AddChild(UiKit.Spacer(expand: true));
        head.AddChild(UiKit.Text(BuildText.Track(item.Track), "EyebrowTertiary"));
        col.AddChild(head);
        col.AddChild(UiKit.Text(BuildText.Why(item.Gate, names), "BodySecondary", wrap: true));
        string verdict = BuildText.Verdict(item, names);
        if (item.Verdict == BuildVerdict.Unresolvable)
            col.AddChild(UiKit.Text("Nothing you can order changes that here.", "CaptionWarning", wrap: true));
        else if (item.Verdict == BuildVerdict.Resolvable)
            col.AddChild(UiKit.Text(verdict, "CaptionSuccess", wrap: true));
        AddLevers(col, item, _buildShown!, names, canIssue);
        col.AddChild(UiKit.Divider());
        return col;
    }

    private Control AlternativeRow(BuildItem item, BuildOutlook o, BuildNames names, bool canIssue)
    {
        bool chosen = item.IsFocus || (_buildPending.Settlement == _buildSelected && _buildPending.Code == item.Code && !_buildPending.Skip);
        var row = UiKit.Row(UiTokens.Sp3);
        row.CustomMinimumSize = new Vector2(0, 40);
        var text = UiKit.Column(UiTokens.Sp1 / 2);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(UiKit.Text(names.Structure(item.Id), chosen ? "BodyStrong" : "Body"));
        string cost = $"Takes {BuildText.Cost(item, names)}";
        string state = item.Status == BuildStatus.Current ? "under way now"
            : item.AnyShort ? $"short: {ShortList(item, names)}"
            : item.Status == BuildStatus.NotNeeded ? "can pay now, though the town does not need it yet"
            : "can pay now";
        text.AddChild(UiKit.Text($"{cost} · {state}", item.AnyShort ? "CaptionWarning" : item.Status == BuildStatus.NotNeeded ? "CaptionTertiary" : "CaptionSuccess", wrap: true));
        row.AddChild(text);
        if (chosen)
            row.AddChild(UiKit.Secondary("Asked for", null, "You asked for this next.", UiGlyph.Check, UiTokens.ControlS));
        else
        {
            UiButton go = UiKit.Primary("Build next", () => SendBuildFocus(item.Code, false),
                item.Track == BuildTrack.Civic && item.Status != BuildStatus.Current
                    ? "Switching the civic building drops the work already put into the current one."
                    : "Ask this town to build this next.", UiGlyph.None, UiTokens.ControlS);
            go.Disabled = !canIssue;
            row.AddChild(go);
        }
        return row;
    }

    private static string ShortList(BuildItem item, BuildNames names)
    {
        var sb = new StringBuilder();
        foreach (BuildNeed n in item.Needs)
        {
            if (!n.Short) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(BuildText.GoodName(n.Slot, names).ToLowerInvariant()).Append(' ').Append(FormatUnits(n.Have.ToDoubleForDisplay())).Append(" of ").Append(FormatUnits(n.Need.ToDoubleForDisplay()));
        }
        return sb.ToString();
    }

    /// <summary>One line for each lever: its sentence and, where the game lets the player act from here, a button.</summary>
    private void AddLevers(VBoxContainer col, BuildItem item, BuildOutlook o, BuildNames names, bool canIssue)
    {
        foreach (BuildLever lever in item.Levers)
        {
            var row = UiKit.Row(UiTokens.Sp2);
            row.AddChild(new UiIcon(UiGlyph.ChevronRight, 14, UiTokens.Accent));
            Label text = UiKit.Text(BuildText.Lever(lever, names), "Caption", wrap: true);
            row.AddChild(text);
            UiButton? button = null;
            switch (lever.Kind)
            {
                case LeverKind.FocusThis:
                    button = UiKit.Secondary("Build next", () => SendBuildFocus(item.Code, false), null, UiGlyph.None, UiTokens.ControlS);
                    break;
                case LeverKind.FocusPrerequisite:
                    button = UiKit.Secondary("Build it first", () => SendBuildFocus(lever.Arg, false), null, UiGlyph.None, UiTokens.ControlS);
                    break;
                case LeverKind.SkipThis:
                    button = UiKit.Secondary("Set aside", () => SendBuildFocus(item.Code, true), "Lets the slot go to the next work the town wants. Labor already put in is kept for later.", UiGlyph.None, UiTokens.ControlS);
                    break;
                case LeverKind.Corvee:
                {
                    int settlement = _buildSelected, target = lever.Arg;
                    button = UiKit.Secondary("Corvée…", () => { ToggleDecrees(true); PreviewAndQueue(DecreeKind.Corvee, settlement, target); }, "Opens the Decree with its forecast and cost.", UiGlyph.None, UiTokens.ControlS);
                    break;
                }
                case LeverKind.Research:
                {
                    string project = lever.Project ?? "";
                    button = UiKit.Secondary("Open research", () =>
                    {
                        ClosePeoplePanel();
                        OpenResearchWeb(-1, _catalog?.IndexOf(project) ?? -1);
                    }, null, UiGlyph.None, UiTokens.ControlS);
                    break;
                }
                case LeverKind.Compact: case LeverKind.FoundNearby: case LeverKind.OpenMine:
                    button = UiKit.Secondary("Decrees", () => { ClosePeoplePanel(); ToggleDecrees(true); }, null, UiGlyph.None, UiTokens.ControlS);
                    break;
            }
            if (button is not null)
            {
                button.Disabled = !canIssue && lever.Kind is not (LeverKind.Research);
                row.AddChild(button);
            }
            col.AddChild(row);
        }
    }

    // ---- orders -------------------------------------------------------------------------------------

    private void SendBuildFocus(int code, bool skip)
    {
        if (_session is null || _current is null || _buildSelected < 0 || !_session.CanIssueCommands) return;
        int civ = Math.Max(0, _current.ViewerCiv);
        _session.Queue(code < 0
            ? PlayerCommands.ClearBuildFocus(_session.Tick, civ, _buildSelected)
            : skip ? PlayerCommands.SkipBuild(_session.Tick, civ, _buildSelected, code)
            : PlayerCommands.SetBuildFocus(_session.Tick, civ, _buildSelected, code));
        _buildPending = (_buildSelected, code, skip);
        _buildShown = null; // draw the pending line at once
        if (_current is not null) RefreshPeopleBuilding(_current);
    }

    // ---- development walk-through (--scenario=build-panel) ----------------------------------------------

    private bool PickBuildTown(Func<BuildOutlook, bool> wanted, string label)
    {
        if (_current is null) return false;
        for (int w = 0; w < _current.SettlementCount; w++)
        {
            if (!IsOwnPeopleSettlement(_current, w) || _current.SettlementBuild[w] is not { } outlook || !wanted(outlook)) continue;
            _peopleScopeSelected = _current.SettlementIndex[w];
            _buildSelected = _current.SettlementIndex[w];
            _buildShown = null;
            RefreshPeoplePanel(_current);
            GD.Print($"SCN build-panel {label}: town {SettlementLabel(_buildSelected)} headline {outlook.Headline}");
            return true;
        }
        GD.Print($"SCN build-panel {label}: no town in this match shows it");
        return false;
    }

    private void BuildPanelScenario(Action<Action, float> step, Action<string> shot)
    {
        step(() =>
        {
            if (_current is null) return;
            var tally = new Dictionary<string, int>();
            for (int w = 0; w < _current.SettlementCount; w++)
                if (IsOwnPeopleSettlement(_current, w) && _current.SettlementBuild[w] is { } o)
                    foreach (BuildItem item in o.Items)
                        if (item.Status is BuildStatus.Short or BuildStatus.Blocked or BuildStatus.Current)
                        {
                            string key = $"{item.Status}/{item.Verdict}";
                            tally[key] = tally.GetValueOrDefault(key) + 1;
                        }
            foreach ((string k, int v) in tally) GD.Print($"SCN build-panel census {k} = {v}");
            OpenPeoplePanel(PeopleTab.Building, int.MinValue);
        }, 1.2f);
        step(() => PickBuildTown(o => o.Items.Any(i => i.Status is BuildStatus.Short or BuildStatus.Current && i.AnyShort && i.Verdict == BuildVerdict.Resolvable), "resolvable"), 1.0f);
        shot("build-1-resolvable");
        step(() =>
        {
            if (PickBuildTown(o => o.Items.Any(i => i.Verdict == BuildVerdict.Unresolvable && i.Status is BuildStatus.Short or BuildStatus.Blocked), "unresolvable")) return;
            // No town of this match is in such a state: draw a hand-made outlook through the same renderer (printed so a capture is never mistaken for live data).
            GD.Print("SCN build-panel unresolvable: SYNTHETIC outlook, not live data");
            if (_current is null) return;
            RenderBuildOutlook(SyntheticStuckOutlook(), true, _current, _current.IndexOfSettlement(_buildSelected));
            _buildShown = null;
        }, 1.0f);
        shot("build-2-unresolvable");
        step(() => { if (_buildScroll is not null) _buildScroll.ScrollVertical = 5000; }, 0.6f);
        shot("build-2b-ruled-out");
        step(() =>
        {
            if (_current is null || _buildSelected < 0) return;
            for (int w = 0; w < _current.SettlementCount; w++)
            {
                if (!IsOwnPeopleSettlement(_current, w) || _current.SettlementIndex[w] != _buildSelected || _current.SettlementBuild[w] is not { } o) continue;
                BuildItem? pick = o.Items.FirstOrDefault(i => i.CanFocus && !i.AnyShort && i.Status != BuildStatus.Current) ?? o.Items.FirstOrDefault(i => i.CanFocus);
                if (pick is not null) { GD.Print($"SCN build-panel asking for {pick.Id}"); SendBuildFocus(pick.Code, false); }
            }
        }, 2.0f);
        shot("build-3-asked");
    }

    /// <summary>A town short of stone with none in reach, a shrine it is too small for and a weir with no river: the stops nothing can fix.</summary>
    private static BuildOutlook SyntheticStuckOutlook()
    {
        int stone = LivingEconomyModel.StoreSlotCount > 0 ? Array.FindIndex(Enumerable.Range(0, LivingEconomyModel.StoreSlotCount).ToArray(), s => LivingEconomyModel.StoreSlotId(s) == "G-STONE") : 9;
        var one = CivSim.Sim.Numerics.Fix64.One;
        BuildItem Make(string id, int kind, BuildStatus status, BuildGate gate, BuildNeed[] needs, GoodSupply[] supplies, BuildVerdict verdict, BuildLever[] levers)
            => new(BuildTrack.Basic, kind, id, 0, status, gate, needs, supplies, one, CivSim.Sim.Numerics.Fix64.Zero, false, BuildWhy.None, false, false, verdict, levers);
        var items = new[]
        {
            Make("hearth", 0, BuildStatus.Short, new BuildGate(BuildCause.Materials, BuildWhy.None),
                new[] { new BuildNeed(stone, CivSim.Sim.Numerics.Fix64.Zero, CivSim.Sim.Numerics.Fix64.Half) },
                new[] { new GoodSupply(SupplyKind.NoSource, null, 3) }, BuildVerdict.Unresolvable, new[] { new BuildLever(LeverKind.FoundNearby) }),
            Make("shrine", 1, BuildStatus.Blocked, new BuildGate(BuildCause.Size, BuildWhy.NeedsPopulation, 20), Array.Empty<BuildNeed>(), Array.Empty<GoodSupply>(), BuildVerdict.Unresolvable, Array.Empty<BuildLever>()),
            Make("weir", 2, BuildStatus.Blocked, new BuildGate(BuildCause.Site, BuildWhy.NoRiver), Array.Empty<BuildNeed>(), Array.Empty<GoodSupply>(), BuildVerdict.Unresolvable, Array.Empty<BuildLever>()),
            Make("hillfort", 3, BuildStatus.Blocked, new BuildGate(BuildCause.Site, BuildWhy.NoHillSite), Array.Empty<BuildNeed>(), Array.Empty<GoodSupply>(), BuildVerdict.Unresolvable, Array.Empty<BuildLever>()),
        };
        return new BuildOutlook(0, 0, BuildHeadline.WaitingOnMaterials, items, -1, false, -1, -1, false, false, 1000, one, one, 14);
    }
}
