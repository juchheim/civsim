using System;
using System.Collections.Generic;
using System.Globalization;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// "Your people": one sheet, four tabs (Overview, Stores, Work, Research) and a scope selector shared by all of them
/// (every settlement, or one). Overview names what slows the civilization, Stores shows what it holds and where food goes,
/// Work shows who does what, Research shows what learned projects are doing.
/// </summary>
public partial class Game
{
    private enum PeopleTab { Overview, Stores, Work, Research }

    private sealed class PeopleBarRow
    {
        public Control Root = null!;
        public Label Name = null!, Count = null!;
        public UiProgressBar Bar = null!;
    }

    private static readonly (FigureActivity Activity, string Name)[] PeopleWork =
    {
        (FigureActivity.Forage, "Foragers"), (FigureActivity.Hunt, "Hunters"), (FigureActivity.Fish, "Fishers"),
        (FigureActivity.Farm, "Farmers"), (FigureActivity.Chop, "Woodcutters"), (FigureActivity.Gather, "Gatherers"),
        (FigureActivity.Craft, "Crafters"), (FigureActivity.Build, "Builders"), (FigureActivity.Preserve, "Food preservers"),
        (FigureActivity.TendFire, "Fire tenders"), (FigureActivity.Herd, "Herders"), (FigureActivity.Scout, "Scouts"),
    };

    /// <summary>Head counts of the people in the chosen scope, read once per refresh.</summary>
    private struct PeopleCensus
    {
        public int Population, Children, Adults, Elders, Recovering, Resting, Traveling;
        public int Priests, Scribes, Warriors, Hungry, Cold, Ill, Wounded, Blocked;
    }

    /// <summary>The economy block of the chosen scope: sums over its settlements, ratios recomputed from the sums.</summary>
    private sealed class PeopleScopeSummary
    {
        public int Rows, Population;
        public long Stores, Need, Capacity, Fuel, FoodCap, HousingCap, GroupCap, LeanCap;
        public long FoodSecurityWeighted, BirthWeighted;
        public bool LedgerValid;
        public readonly long[] Ledger = new long[LivingEconomyModel.FoodLedgerLineCount];
        public int MinForecast = 1000;
    }

    private readonly PeopleBarRow[] _peopleWorkRows = new PeopleBarRow[PeopleWork.Length];
    private readonly int[] _peopleActivityCounts = new int[23];
    private readonly List<int> _peopleScoutSettlementIds = new();
    private readonly Button[] _peopleScoutButtons = new Button[4];
    private readonly Control?[] _peoplePanes = new Control?[4];
    private readonly List<int> _peopleScopeIds = new();
    private readonly PeopleScopeSummary _peopleSummary = new();
    private PeopleCensus _peopleCensus;
    private PeopleTab _peopleTab = PeopleTab.Overview;
    private UiSegmented? _peopleTabs;
    private OptionButton? _peopleScope;
    private int _peopleScopeSelected = -1;
    private bool _rebuildingPeopleScope;
    private int _peopleSelectedScoutSettlement = -1;
    private bool _rebuildingPeopleScoutOptions;
    private Control? _peopleRoot;
    private ColorRect? _peopleScrim;
    private PanelContainer? _peoplePanel;
    private UiLayer? _peopleLayer;
    private bool _peopleOpen;
    private UiButton? _peopleReturnButton, _peopleWebButton;
    private Label? _peopleDate, _peopleFootnote;
    private Label? _peopleScoutStatus;
    private OptionButton? _peopleScoutSettlement;
    private Label? _peopleChildren, _peopleRecovering, _peopleResting, _peopleTraveling;
    private Label? _peoplePriests, _peopleScribes, _peopleWarriors;
    private readonly DiagnosisEngine _peopleDiagnosis = new();
    private readonly DiagnosisEntry[] _peopleDiagnosisEntries = new DiagnosisEntry[48];
    private int _peopleDiagnosisCount;
    private int _peopleDiagnosisSequence = -1;
    private readonly Control[] _peopleMethodRows = new Control[6];
    private readonly Label[] _peopleMethodLabels = new Label[6];
    private Label? _peopleMethodNone, _laborNote, _lossNone;
    private readonly FlowRow[] _laborRows = new FlowRow[6];
    private readonly Label[] _lossLabels = new Label[6];

    private static readonly string[] PeopleTabNames = { "Overview", "Stores", "Work", "Research" };

    private void CreatePeoplePanel()
    {
        _peopleRoot = new Control { Name = "PeopleSheet", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _peopleRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _peopleScrim = new ColorRect { Color = UiTokens.ScrimModal, MouseFilter = Control.MouseFilterEnum.Stop };
        _peopleScrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _peopleScrim.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) ClosePeoplePanel();
        };
        _peopleRoot.AddChild(_peopleScrim);

        _peoplePanel = UiKit.Panel("SheetPanel", "PeoplePanel");
        _peopleRoot.AddChild(_peoplePanel);
        var content = UiKit.Column(UiTokens.Sp3);
        _peoplePanel.AddChild(content);

        var header = UiKit.Row(UiTokens.Sp3);
        var title = UiKit.Text("People & Stores", "Title");
        title.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(title);
        _peopleDate = UiKit.Text("Year 1 · Spring", "BodySecondary");
        _peopleDate.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(_peopleDate);
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (Esc)", ClosePeoplePanel));
        content.AddChild(header);

        var tabRow = UiKit.Row(UiTokens.Sp3);
        _peopleTabs = new UiSegmented(PeopleTabNames, (int)_peopleTab, SelectPeopleTab, 112);
        _peopleTabs.SetTooltip(0, "What is slowing your people down, and what limits their growth.");
        _peopleTabs.SetTooltip(1, "What you hold, how long it lasts and where the food goes.");
        _peopleTabs.SetTooltip(2, "Who does what, and where better methods are blocked.");
        _peopleTabs.SetTooltip(3, "What research is under way and what learned projects are doing.");
        tabRow.AddChild(_peopleTabs);
        tabRow.AddChild(UiKit.Spacer(expand: true));
        var showing = UiKit.Text("Showing", "Caption");
        showing.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        tabRow.AddChild(showing);
        _peopleScope = new OptionButton { CustomMinimumSize = new Vector2(260, UiTokens.ControlM) };
        _peopleScope.TooltipText = "Every number on these tabs covers all your settlements, or just the one you pick.";
        _peopleScope.ItemSelected += selected =>
        {
            if (_rebuildingPeopleScope) return;
            int index = (int)selected;
            if ((uint)index < (uint)_peopleScopeIds.Count) SetPeopleScope(_peopleScopeIds[index]);
        };
        tabRow.AddChild(_peopleScope);
        content.AddChild(tabRow);

        var host = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddChild(host);
        _peoplePanes[(int)PeopleTab.Overview] = BuildPeopleOverviewPane();
        _peoplePanes[(int)PeopleTab.Stores] = BuildPeopleStoresPane();
        _peoplePanes[(int)PeopleTab.Work] = BuildPeopleWorkPane();
        _peoplePanes[(int)PeopleTab.Research] = BuildPeopleResearchPane();
        for (int i = 0; i < _peoplePanes.Length; i++)
        {
            Control pane = _peoplePanes[i]!;
            pane.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            pane.Visible = i == (int)_peopleTab;
            host.AddChild(pane);
        }

        content.AddChild(UiKit.Divider());
        var footer = UiKit.Row(UiTokens.Sp3);
        _peopleFootnote = UiKit.Text("", "CaptionTertiary", wrap: true);
        _peopleFootnote.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        footer.AddChild(_peopleFootnote);
        _peopleReturnButton = UiKit.Secondary("Close", ClosePeoplePanel, null, UiGlyph.None, UiTokens.ControlS, true);
        footer.AddChild(_peopleReturnButton);
        content.AddChild(footer);

        _peopleLayer = RegisterLayer("people", true, () => { ClosePeoplePanel(); return true; });
        _hudRoot!.AddChild(_peopleRoot);
        UpdatePeopleFootnote();
    }

    // ---- shared layout pieces -------------------------------------------------------------------

    /// <summary>A titled section of the sheet: a sunken panel with a title row (title, then anything added to Head) and a body.</summary>
    private sealed class PeopleSection
    {
        public PanelContainer Panel = null!;
        public Label Title = null!;
        public HBoxContainer Head = null!;
        public VBoxContainer Body = null!;
    }

    private static PeopleSection NewPeopleSection(string title, string? name = null, string? caption = null)
    {
        var section = new PeopleSection { Panel = UiKit.Panel("SectionPanel", name) };
        section.Panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        section.Body = UiKit.Column(UiTokens.Sp2);
        var top = UiKit.Column(UiTokens.Sp1);
        section.Head = UiKit.Row(UiTokens.Sp3);
        section.Title = UiKit.Text(title, "SectionTitle");
        section.Title.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        section.Head.AddChild(section.Title);
        top.AddChild(section.Head);
        if (caption is not null) top.AddChild(UiKit.Text(caption, "CaptionTertiary", wrap: true));
        section.Body.AddChild(top);
        section.Body.AddChild(UiKit.Spacer(height: UiTokens.Sp1));
        section.Panel.AddChild(section.Body);
        return section;
    }

    /// <summary>A small all-caps label that opens a block inside a section.</summary>
    private static Label PeopleSubhead(string text) => UiKit.Text(text, "EyebrowTertiary");

    /// <summary>A tab's scrolling body, with a little room on the right for the scroll bar and below the last section.</summary>
    private static ScrollContainer PeopleScroll(Control content)
        => UiKit.Scroll(UiKit.Margin(content, 0, 0, UiTokens.Sp3, UiTokens.Sp2));

    /// <summary>A column of sections for a two-column tab, sharing the row's width by <paramref name="ratio"/>.</summary>
    private static VBoxContainer PeopleColumn(float ratio, params PeopleSection[] sections)
    {
        var column = UiKit.Column(UiTokens.Sp3);
        column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.SizeFlagsStretchRatio = ratio;
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        foreach (PeopleSection section in sections) column.AddChild(section.Panel);
        return column;
    }

    private static HFlowContainer PeopleFlow()
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        flow.AddThemeConstantOverride("v_separation", UiTokens.Sp2);
        return flow;
    }

    // ---- Work pane: who does what -----------------------------------------------------------------

    private Control BuildPeopleWorkPane()
    {
        // Left: who does what this season, and where last season's work went.
        PeopleSection work = NewPeopleSection("Work assignments", "WorkAssignments", "Each person's job right now. Assignments change with the season.");
        var workList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < PeopleWork.Length; i++) _peopleWorkRows[i] = BuildPeopleBarRow(workList, PeopleWork[i].Name);
        work.Body.AddChild(workList);
        work.Body.AddChild(UiKit.Spacer(height: UiTokens.Sp1));
        work.Body.AddChild(PeopleSubhead("NOT AT WORK"));
        var other = PeopleFlow();
        _peopleChildren = AddPeopleCountChip(other, "Children");
        _peopleRecovering = AddPeopleCountChip(other, "Recovering");
        _peopleResting = AddPeopleCountChip(other, "Resting or idle");
        _peopleTraveling = AddPeopleCountChip(other, "Away with a party");
        work.Body.AddChild(other);

        PeopleSection labor = NewPeopleSection("Where the work went last season", "LaborSplit");
        _laborNote = UiKit.Text("", "Caption", wrap: true);
        labor.Body.AddChild(_laborNote);
        string[] laborNames = { "Food", "Building", "Crafts & materials", "Herding", "Civic works", "Fire, care, roles" };
        var laborList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < _laborRows.Length; i++)
        {
            var row = UiKit.Row(UiTokens.Sp3);
            row.CustomMinimumSize = new Vector2(0, 26);
            var view = new FlowRow
            {
                Name = UiKit.Cell(laborNames[i], "BodySecondary", 160, wrap: true),
                Bar = new UiProgressBar(6),
                Value = UiKit.Cell("", "NumBody", 128, right: true),
            };
            view.Name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(view.Name);
            row.AddChild(view.Bar);
            row.AddChild(view.Value);
            laborList.AddChild(row);
            _laborRows[i] = view;
        }
        labor.Body.AddChild(laborList);

        // Right: scouting limits, methods held back, lost work, roles.
        PeopleSection scouting = NewPeopleSection("Scouting", "Scouting", "How many scouts a settlement may send out. Changes apply next season.");
        var scoutTargetRow = UiKit.Row(UiTokens.Sp2);
        var settlementLabel = UiKit.Cell("Settlement", "BodySecondary", 84);
        settlementLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        scoutTargetRow.AddChild(settlementLabel);
        _peopleScoutSettlement = new OptionButton
        {
            CustomMinimumSize = new Vector2(180, UiTokens.ControlS),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _peopleScoutSettlement.ItemSelected += selected =>
        {
            if (_rebuildingPeopleScoutOptions) return;
            int index = (int)selected;
            if ((uint)index < (uint)_peopleScoutSettlementIds.Count)
                _peopleSelectedScoutSettlement = _peopleScoutSettlementIds[index];
            if (_current is not null) RefreshPeopleScoutControls(_current);
        };
        scoutTargetRow.AddChild(_peopleScoutSettlement);
        scouting.Body.AddChild(scoutTargetRow);
        var scoutPolicyRow = UiKit.Row(UiTokens.Sp2);
        var scoutsLabel = UiKit.Cell("Scouts", "BodySecondary", 84);
        scoutsLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        scoutPolicyRow.AddChild(scoutsLabel);
        string[] scoutLabels = { "Auto", "0", "1", "2" };
        ScoutPolicy[] scoutPolicies = { ScoutPolicy.Auto, ScoutPolicy.Zero, ScoutPolicy.One, ScoutPolicy.Two };
        string[] scoutTips =
        {
            "Auto scouting yields workers when food is short.",
            "No scouts; released workers return to other jobs next season.",
            "Allow up to one scout next season.",
            "Allow up to two scouts next season. Requires WAY-01.",
        };
        for (int i = 0; i < _peopleScoutButtons.Length; i++)
        {
            ScoutPolicy policy = scoutPolicies[i];
            Button button = UiKit.InteractiveChip(scoutLabels[i], UiGlyph.None, () => SetPeopleScoutPolicy(policy), scoutTips[i]);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _peopleScoutButtons[i] = button;
            scoutPolicyRow.AddChild(button);
        }
        scouting.Body.AddChild(scoutPolicyRow);
        _peopleScoutStatus = UiKit.Text("Auto scouting yields workers when food is short.", "Caption", wrap: true);
        scouting.Body.AddChild(_peopleScoutStatus);

        PeopleSection methods = NewPeopleSection("Workers on older methods", "OlderMethods",
            "A better method is known, but these workers lack something it needs: a tool, a bigger hunting party or metal.");
        for (int i = 0; i < _peopleMethodRows.Length; i++)
        {
            _peopleMethodLabels[i] = UiKit.Text("", "BodySecondary", wrap: true);
            _peopleMethodRows[i] = _peopleMethodLabels[i];
            methods.Body.AddChild(_peopleMethodLabels[i]);
        }
        _peopleMethodNone = UiKit.Text("Everyone is using the best method they know.", "CaptionSuccess", wrap: true);
        methods.Body.AddChild(_peopleMethodNone);

        PeopleSection lost = NewPeopleSection("Where work was lost", "LostWork", "Work with no tool to use, and work that found little to take.");
        for (int i = 0; i < _lossLabels.Length; i++)
        {
            _lossLabels[i] = UiKit.Text("", "BodySecondary", wrap: true);
            lost.Body.AddChild(_lossLabels[i]);
        }
        _lossNone = UiKit.Text("No work went unused for want of tools, and nothing came up short on land.", "CaptionSuccess", wrap: true);
        lost.Body.AddChild(_lossNone);

        PeopleSection roles = NewPeopleSection("Roles", "Roles", "Warriors and specialists may also appear in the work counts.");
        var roleRow = PeopleFlow();
        _peoplePriests = AddPeopleCountChip(roleRow, "Priests");
        _peopleScribes = AddPeopleCountChip(roleRow, "Scribes");
        _peopleWarriors = AddPeopleCountChip(roleRow, "Warriors");
        roles.Body.AddChild(roleRow);

        var body = UiKit.Row(UiTokens.Sp3);
        body.AddChild(PeopleColumn(0.58f, work, labor));
        body.AddChild(PeopleColumn(0.42f, scouting, methods, lost, roles));
        return PeopleScroll(body);
    }

    private static PeopleBarRow BuildPeopleBarRow(VBoxContainer parent, string name)
    {
        var row = UiKit.Row(UiTokens.Sp3);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.CustomMinimumSize = new Vector2(0, 24);
        var result = new PeopleBarRow
        {
            Root = row,
            Name = UiKit.Cell(name, "BodySecondary", 170, wrap: true),
            Bar = new UiProgressBar(6),
            Count = UiKit.Cell("0", "NumBody", 48, right: true),
        };
        result.Name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        result.Root.AddChild(result.Name);
        result.Root.AddChild(result.Bar);
        result.Root.AddChild(result.Count);
        parent.AddChild(result.Root);
        return result;
    }

    private static Label AddPeopleCountChip(HFlowContainer parent, string name)
    {
        var chip = UiKit.Panel("ChipPanel", null, UiKit.MouseFilter.Ignore);
        var row = UiKit.Row(UiTokens.Sp2);
        row.AddChild(UiKit.Text(name, "Caption"));
        Label count = UiKit.Text("0", "NumStrong");
        row.AddChild(count);
        chip.AddChild(row);
        parent.AddChild(chip);
        return count;
    }

    // ---- shell ----------------------------------------------------------------------------------

    private void LayoutPeoplePanel()
    {
        if (_peoplePanel is null) return;
        float width = Math.Min(1280, _logicalSize.X - 2 * Margin);
        float height = Math.Min(780, _logicalSize.Y - 2 * Margin);
        _peoplePanel.AnchorLeft = _peoplePanel.AnchorRight = 0.5f;
        _peoplePanel.AnchorTop = _peoplePanel.AnchorBottom = 0.5f;
        _peoplePanel.OffsetLeft = -width / 2;
        _peoplePanel.OffsetRight = width / 2;
        _peoplePanel.OffsetTop = -height / 2;
        _peoplePanel.OffsetBottom = height / 2;
        _peoplePanel.GrowHorizontal = Control.GrowDirection.Both;
        _peoplePanel.GrowVertical = Control.GrowDirection.Both;
        _peoplePanel.CustomMinimumSize = new Vector2(width, height);
    }

    private void OpenPeoplePanel() => OpenPeoplePanel(null, int.MinValue);

    /// <summary>Opens the sheet; <paramref name="tab"/> and <paramref name="settlement"/> (economy index) are optional deep links.</summary>
    private void OpenPeoplePanel(PeopleTab? tab, int settlement)
    {
        if (_peopleRoot is null || _current is null) return;
        if (tab is { } wanted) ShowPeopleTab(wanted);
        if (settlement != int.MinValue) _peopleScopeSelected = settlement;
        if (_peopleOpen) { RefreshPeoplePanel(_current); return; }
        _peopleOpen = true;
        _hudRoot!.MoveChild(_peopleRoot, _hudRoot.GetChildCount() - 1);
        RefreshPeoplePanel(_current);
        LayoutPeoplePanel();
        _peoplePanel!.Modulate = new Color(1, 1, 1, 0);
        _peopleRoot.Visible = true;
        UiKit.Fade(_peoplePanel, true, UiTokens.SlowS);
        _ui.Push(_peopleLayer!);
        UiKit.SetText(_peopleReturnButton!, _researchWebVisible ? "Back to Research" : "Close");
        UpdatePausedTreatment();
        CallDeferred(nameof(FocusPeopleClose));
    }

    private void FocusPeopleClose()
    {
        if (_peopleOpen) _peopleReturnButton?.GrabFocus();
    }

    private void ClosePeoplePanel()
    {
        if (!_peopleOpen) return;
        _peopleOpen = false;
        if (_peopleRoot is not null) _peopleRoot.Visible = false;
        if (_peopleLayer is not null) _ui.Remove(_peopleLayer);
        UpdatePausedTreatment();
    }

    private void SelectPeopleTab(int index)
    {
        ShowPeopleTab((PeopleTab)Math.Clamp(index, 0, PeopleTabNames.Length - 1));
        if (_current is not null) RefreshPeoplePanel(_current);
    }

    private void ShowPeopleTab(PeopleTab tab)
    {
        _peopleTab = tab;
        for (int i = 0; i < _peoplePanes.Length; i++)
            if (_peoplePanes[i] is { } pane) pane.Visible = i == (int)tab;
        _peopleTabs?.Select((int)tab, false);
        UpdatePeopleFootnote();
    }

    private void UpdatePeopleFootnote()
    {
        if (_peopleFootnote is null) return;
        UiKit.SetText(_peopleFootnote, _peopleTab switch
        {
            PeopleTab.Overview => "Food numbers are for the last full season. Winter risk shows in autumn and winter.",
            PeopleTab.Stores => "R is a ration: one adult's food for a season. Stock is live; flows are for the last full season.",
            PeopleTab.Work => "Counts are live; the work split is for the last full season.",
            _ => "Gains are estimates: what last season would have lost without each project.",
        });
    }

    private void SetPeopleScope(int settlement)
    {
        _peopleScopeSelected = settlement;
        if (settlement >= 0 && _peopleScoutSettlementIds.Contains(settlement)) _peopleSelectedScoutSettlement = settlement;
        if (_current is not null) RefreshPeoplePanel(_current);
    }

    private void RefreshPeopleScope(MatchFrame frame)
    {
        if (_peopleScope is null) return;
        bool changed = false;
        int own = 0;
        for (int s = 0; s < frame.SettlementCount; s++)
        {
            if (!IsOwnPeopleSettlement(frame, s)) continue;
            own++;
            if (own >= _peopleScopeIds.Count || _peopleScopeIds[own] != frame.SettlementIndex[s]) changed = true;
        }
        if (_peopleScopeIds.Count != own + 1) changed = true;
        if (changed)
        {
            _peopleScopeIds.Clear();
            _peopleScopeIds.Add(-1);
            _rebuildingPeopleScope = true;
            _peopleScope.Clear();
            _peopleScope.AddItem("All settlements", 0);
            for (int s = 0; s < frame.SettlementCount; s++)
            {
                if (!IsOwnPeopleSettlement(frame, s)) continue;
                _peopleScopeIds.Add(frame.SettlementIndex[s]);
                _peopleScope.AddItem($"Settlement {frame.SettlementIndex[s] + 1} · {frame.SettlementPopulation[s]:N0} people", _peopleScopeIds.Count - 1);
            }
            if (!_peopleScopeIds.Contains(_peopleScopeSelected)) _peopleScopeSelected = -1;
            _rebuildingPeopleScope = false;
        }
        int selected = _peopleScopeIds.IndexOf(_peopleScopeSelected);
        _rebuildingPeopleScope = true;
        if (selected >= 0 && _peopleScope.Selected != selected) _peopleScope.Select(selected);
        _peopleScope.Disabled = _peopleScopeIds.Count <= 2;
        _rebuildingPeopleScope = false;
    }

    private bool PeopleInScope(MatchFrame frame, int row)
        => IsOwnPeopleSettlement(frame, row) && (_peopleScopeSelected < 0 || frame.SettlementIndex[row] == _peopleScopeSelected);

    private bool PeopleRowHasEconomy(MatchFrame frame, int row)
        => PeopleInScope(frame, row) && frame.SettlementEcon[row * EconField.Stride + EconField.Valid] != 0;

    private void RefreshPeoplePanel(MatchFrame frame)
    {
        if (!_peopleOpen || _peopleRoot is null || _catalog is null) return;
        int civ = frame.FocusCiv;
        if ((uint)civ >= (uint)frame.CivCount) return;
        RefreshPeopleScope(frame);
        CountPeople(frame, civ);
        SummarizePeopleScope(frame);
        UiKit.SetText(_peopleDate!, $"Year {frame.Year + 1} · {UiText.SeasonNames[frame.SeasonOfYear & 3]}");
        UiKit.SetText(_peopleReturnButton!, _researchWebVisible ? "Back to Research" : "Close");
        switch (_peopleTab)
        {
            case PeopleTab.Overview: RefreshPeopleOverview(frame, civ); break;
            case PeopleTab.Stores: RefreshPeopleStores(frame); break;
            case PeopleTab.Work: RefreshPeopleWork(frame, civ); break;
            case PeopleTab.Research: RefreshPeopleResearch(frame, civ); break;
        }
    }

    private void CountPeople(MatchFrame frame, int civ)
    {
        Array.Clear(_peopleActivityCounts);
        var c = new PeopleCensus();
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureCiv[i] != civ) continue;
            FigureFlags flags = (FigureFlags)frame.FigureFlags[i];
            if ((flags & FigureFlags.FreeFolk) != 0) continue;
            if (_peopleScopeSelected >= 0 && frame.FigureSettlement[i] != _peopleScopeSelected) continue;
            c.Population++;
            FigureActivity activity = (FigureActivity)frame.FigureActivity[i];
            if ((uint)activity < (uint)_peopleActivityCounts.Length) _peopleActivityCounts[(int)activity]++;
            switch ((AgeBand)frame.FigureAge[i])
            {
                case AgeBand.Child: c.Children++; break;
                case AgeBand.Adult: c.Adults++; break;
                case AgeBand.Elder: c.Elders++; break;
            }
            if (activity == FigureActivity.Convalescing) c.Recovering++;
            if (activity is FigureActivity.Rest or FigureActivity.Idle) c.Resting++;
            if ((flags & FigureFlags.Away) != 0) c.Traveling++;
            if (frame.FigureRole[i] == 1) c.Priests++;
            if (frame.FigureRole[i] == 2) c.Scribes++;
            if ((flags & FigureFlags.Warrior) != 0) c.Warriors++;
            if ((flags & FigureFlags.Hungry) != 0) c.Hungry++;
            if ((flags & FigureFlags.Cold) != 0) c.Cold++;
            if ((flags & FigureFlags.Ill) != 0) c.Ill++;
            if ((flags & FigureFlags.Wounded) != 0) c.Wounded++;
            if (frame.FigureMethodFallback[i] != (byte)MethodFallbackReason.None) c.Blocked++;
        }
        _peopleCensus = c;
    }

    private void SummarizePeopleScope(MatchFrame frame)
    {
        PeopleScopeSummary sum = _peopleSummary;
        sum.Rows = 0; sum.Population = 0;
        sum.Stores = sum.Need = sum.Capacity = sum.Fuel = sum.FoodCap = sum.HousingCap = sum.GroupCap = sum.LeanCap = 0;
        sum.FoodSecurityWeighted = sum.BirthWeighted = 0;
        sum.LedgerValid = false;
        sum.MinForecast = 1000;
        Array.Clear(sum.Ledger);
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!PeopleRowHasEconomy(frame, w)) continue;
            int econ = w * EconField.Stride;
            long[] e = frame.SettlementEcon;
            int pop = frame.SettlementPopulation[w];
            sum.Rows++;
            sum.Population += pop;
            sum.Stores += frame.SettlementFoodStores[w];
            sum.Need += e[econ + EconField.FoodNeed];
            sum.Capacity += e[econ + EconField.FoodCapacity];
            sum.Fuel += e[econ + EconField.FuelPerSeason];
            sum.FoodCap += e[econ + EconField.FoodCap];
            sum.HousingCap += e[econ + EconField.HousingCap];
            sum.GroupCap += e[econ + EconField.GroupCap];
            sum.LeanCap += e[econ + EconField.LeanCap];
            long fs = e[econ + EconField.FoodSecurityPermille];
            sum.FoodSecurityWeighted += fs * pop;
            sum.BirthWeighted += fs * e[econ + EconField.HealthPermille] / 1000 * e[econ + EconField.CrowdingFreePermille] / 1000 * pop;
            sum.MinForecast = Math.Min(sum.MinForecast, (int)e[econ + EconField.ForecastPermille]);
            if (e[econ + EconField.LedgerValid] != 0)
            {
                sum.LedgerValid = true;
                for (int i = 0; i < sum.Ledger.Length; i++) sum.Ledger[i] += e[econ + EconField.Ledger + i];
            }
        }
    }

    // ---- shared formatting ------------------------------------------------------------------------

    private static double Units(long raw) => raw / 4294967296.0;

    /// <summary>Rations or goods with one decimal below 100 and none above ("3.4", "41").</summary>
    private static string FormatUnits(double value)
        => Math.Abs(value) >= 100 ? value.ToString("N0", CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FormatUnits(long raw) => FormatUnits(Units(raw));

    private static string FormatHundredths(int hundredths) => FormatUnits(hundredths / 100.0);

    private static string FormatSeasons(double seasons)
        => seasons >= 8 ? "8+ seasons" : seasons < 0.05 ? "0 seasons" : seasons.ToString("0.#", CultureInfo.InvariantCulture) + (Math.Abs(seasons - 1) < 0.05 ? " season" : " seasons");

    private static void SetTone(Label label, Color color)
    {
        if (label.GetThemeColor("font_color") != color) label.AddThemeColorOverride("font_color", color);
    }

    private static Color ToneForRunway(double seasons)
        => seasons < 1 ? UiTokens.Danger : seasons < 2 ? UiTokens.Warning : UiTokens.Success;

    // ---- Work refresh -----------------------------------------------------------------------------

    private void RefreshPeopleWork(MatchFrame frame, int civ)
    {
        RefreshPeopleScoutControls(frame);
        int population = _peopleCensus.Population;
        for (int i = 0; i < _peopleWorkRows.Length; i++)
        {
            PeopleBarRow row = _peopleWorkRows[i];
            int count = _peopleActivityCounts[(int)PeopleWork[i].Activity];
            UiKit.SetVisible(row.Root, true);
            UiKit.SetText(row.Count, count.ToString("N0"));
            row.Bar.SetValue(population > 0 ? count / (float)population : 0, false);
            SetTone(row.Name, count > 0 ? UiTokens.TextPrimary : UiTokens.TextTertiary);
            SetTone(row.Count, count > 0 ? UiTokens.TextPrimary : UiTokens.TextTertiary);
        }
        UiKit.SetText(_peopleChildren!, _peopleCensus.Children.ToString("N0"));
        UiKit.SetText(_peopleRecovering!, _peopleCensus.Recovering.ToString("N0"));
        UiKit.SetText(_peopleResting!, _peopleCensus.Resting.ToString("N0"));
        UiKit.SetText(_peopleTraveling!, _peopleCensus.Traveling.ToString("N0"));
        UiKit.SetText(_peoplePriests!, _peopleCensus.Priests.ToString("N0"));
        UiKit.SetText(_peopleScribes!, _peopleCensus.Scribes.ToString("N0"));
        UiKit.SetText(_peopleWarriors!, _peopleCensus.Warriors.ToString("N0"));
        RefreshPeopleMethods(frame, civ);
        RefreshPeopleLabor(frame);
    }

    private static readonly string[] FoodShareCaptions =
    {
        "",
        "Hunger last season: almost all hands (90%) went to food.",
        "Stores were below one season's need: 85% of hands went to food.",
        "Stores were below two seasons' need: 70% of hands went to food.",
        "Stores were comfortable: about half the hands were free for building and crafts.",
        "The town keeps a quarter of its hands for civic work.",
    };

    private readonly long[] _laborSums = new long[LivingEconomyModel.LaborLineCount];
    private readonly long[] _actionSums = new long[LivingEconomyModel.ActionLedgerLineCount];

    private void RefreshPeopleLabor(MatchFrame frame)
    {
        Array.Clear(_laborSums);
        Array.Clear(_actionSums);
        int rule = 0;
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!PeopleRowHasEconomy(frame, w)) continue;
            int econ = w * EconField.Stride;
            for (int i = 0; i < LivingEconomyModel.LaborLineCount - 1; i++) _laborSums[i] += frame.SettlementEcon[econ + EconField.Labor + i];
            int ruleHere = (int)(frame.SettlementEcon[econ + EconField.Labor + LivingEconomyModel.LaborRule] >> 32);
            if (ruleHere > 0 && (rule == 0 || ruleHere < rule)) rule = ruleHere;
            for (int i = 0; i < _actionSums.Length; i++) _actionSums[i] += frame.SettlementEcon[econ + EconField.Actions + i];
        }
        double capacity = Units(_laborSums[LivingEconomyModel.LaborCapacity]);
        UiKit.SetText(_laborNote!, capacity <= 0 ? "Numbers appear once the first season has ended."
            : $"{FormatUnits(capacity)} work-seasons (ws) were available. {FoodShareCaptions[Math.Clamp(rule, 0, 5)]}");
        double reserved = Units(_laborSums[LivingEconomyModel.LaborReserved]);
        double[] parts =
        {
            Units(_laborSums[LivingEconomyModel.LaborFood]), Units(_laborSums[LivingEconomyModel.LaborBuild]),
            Units(_laborSums[LivingEconomyModel.LaborMaterial]), Units(_laborSums[LivingEconomyModel.LaborHerd]),
            Units(_laborSums[LivingEconomyModel.LaborCivic]), reserved,
        };
        Color[] tones = { UiTokens.Success, UiTokens.Accent, UiTokens.Info, UiTokens.Info, UiTokens.Info, UiTokens.TextTertiary };
        for (int i = 0; i < _laborRows.Length; i++)
        {
            FlowRow row = _laborRows[i];
            double share = capacity > 0 ? parts[i] / capacity : 0;
            row.Bar.SetValue((float)Math.Clamp(share, 0, 1), false);
            row.Bar.Fill = tones[i];
            UiKit.SetText(row.Value, capacity > 0 ? $"{FormatUnits(parts[i])} ws · {share * 100:0}%" : "");
            bool show = capacity > 0 && (i != 4 || parts[i] > 0.005);
            UiKit.SetVisible(row.Name.GetParent<Control>(), show);
        }

        int shown = 0;
        string[] names = { "Foraging", "Hunting", "Fishing", "Farming", "Woodcutting" };
        for (int k = 0; k < LivingEconomyModel.ActionLedgerKinds && shown < _lossLabels.Length; k++)
        {
            int line = k * LivingEconomyModel.ActionLedgerFields;
            double assigned = Units(_actionSums[line + LivingEconomyModel.ActionAssigned]), applied = Units(_actionSums[line + LivingEconomyModel.ActionApplied]);
            double requested = Units(_actionSums[line + LivingEconomyModel.ActionRequested]), harvested = Units(_actionSums[line + LivingEconomyModel.ActionHarvested]);
            if (assigned - applied >= 0.25 && shown < _lossLabels.Length)
            {
                UiKit.SetText(_lossLabels[shown], $"{names[k]}: {FormatUnits(assigned - applied)} of {FormatUnits(assigned)} work-seasons had no tool to use.");
                UiKit.SetVisible(_lossLabels[shown++], true);
            }
            if (requested - harvested >= 1 && harvested < requested * 0.9 && shown < _lossLabels.Length)
            {
                UiKit.SetText(_lossLabels[shown], $"{names[k]}: asked for {FormatUnits(requested)}, found {FormatUnits(harvested)}. The land within reach is thin.");
                UiKit.SetVisible(_lossLabels[shown++], true);
            }
        }
        for (int i = shown; i < _lossLabels.Length; i++) UiKit.SetVisible(_lossLabels[i], false);
        UiKit.SetVisible(_lossNone!, shown == 0);
    }

    private readonly int[] _peopleMethodCounts = new int[23 * 8];

    private void RefreshPeopleMethods(MatchFrame frame, int civ)
    {
        Array.Clear(_peopleMethodCounts);
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureCiv[i] != civ || (((FigureFlags)frame.FigureFlags[i]) & FigureFlags.FreeFolk) != 0) continue;
            if (_peopleScopeSelected >= 0 && frame.FigureSettlement[i] != _peopleScopeSelected) continue;
            byte reason = frame.FigureMethodFallback[i];
            int activity = frame.FigureActivity[i];
            if (reason == 0 || reason >= 8 || (uint)activity >= 23) continue;
            _peopleMethodCounts[activity * 8 + reason]++;
        }
        int shown = 0;
        for (int activity = 0; activity < 23 && shown < _peopleMethodLabels.Length; activity++)
            for (int reason = 1; reason < 8 && shown < _peopleMethodLabels.Length; reason++)
            {
                int count = _peopleMethodCounts[activity * 8 + reason];
                if (count == 0) continue;
                UiKit.SetText(_peopleMethodLabels[shown], $"{count} {PeopleActivityWord((FigureActivity)activity, count)}: {PeopleFallbackWords((MethodFallbackReason)reason)}");
                UiKit.SetVisible(_peopleMethodRows[shown], true);
                shown++;
            }
        for (int i = shown; i < _peopleMethodRows.Length; i++) UiKit.SetVisible(_peopleMethodRows[i], false);
        UiKit.SetVisible(_peopleMethodNone!, shown == 0);
    }

    private static string PeopleActivityWord(FigureActivity activity, int count) => activity switch
    {
        FigureActivity.Forage => count == 1 ? "forager" : "foragers",
        FigureActivity.Hunt => count == 1 ? "hunter" : "hunters",
        FigureActivity.Fish => count == 1 ? "fisher" : "fishers",
        FigureActivity.Farm => count == 1 ? "farmer" : "farmers",
        FigureActivity.Chop => count == 1 ? "woodcutter" : "woodcutters",
        FigureActivity.Gather => count == 1 ? "gatherer" : "gatherers",
        FigureActivity.Craft => count == 1 ? "crafter" : "crafters",
        FigureActivity.Herd => count == 1 ? "herder" : "herders",
        _ => count == 1 ? "worker" : "workers",
    };

    private static string PeopleFallbackWords(MethodFallbackReason reason) => reason switch
    {
        MethodFallbackReason.MissingTool => "missing a tool for the better method",
        MethodFallbackReason.HuntParty => "too few hunters for the party method",
        MethodFallbackReason.NoTin => "no tin for bronze tools",
        MethodFallbackReason.NoArsenic => "no arsenic ore for arsenical bronze",
        MethodFallbackReason.NoOre => "no copper ore for metal tools",
        MethodFallbackReason.NoFuel => "not enough charcoal fuel",
        _ => "blocked",
    };

    private void RefreshPeopleScoutControls(MatchFrame frame)
    {
        if (_peopleScoutSettlement is null || _peopleScoutStatus is null) return;
        bool changed = false;
        int ownCount = 0;
        for (int s = 0; s < frame.SettlementCount; s++)
        {
            if (!IsOwnPeopleSettlement(frame, s)) continue;
            if (ownCount >= _peopleScoutSettlementIds.Count || _peopleScoutSettlementIds[ownCount] != frame.SettlementIndex[s]) changed = true;
            ownCount++;
        }
        if (ownCount != _peopleScoutSettlementIds.Count) changed = true;
        if (changed)
        {
            int previousSelection = _peopleSelectedScoutSettlement;
            _peopleScoutSettlementIds.Clear();
            _peopleScoutSettlement.Clear();
            for (int s = 0; s < frame.SettlementCount; s++)
            {
                if (!IsOwnPeopleSettlement(frame, s)) continue;
                int settlement = frame.SettlementIndex[s];
                _peopleScoutSettlementIds.Add(settlement);
                _peopleScoutSettlement.AddItem($"Settlement {settlement + 1}", settlement);
            }
            int selectedIndex = _peopleScoutSettlementIds.IndexOf(previousSelection);
            if (selectedIndex < 0) selectedIndex = _peopleScoutSettlementIds.Count > 0 ? 0 : -1;
            _peopleSelectedScoutSettlement = selectedIndex >= 0 ? _peopleScoutSettlementIds[selectedIndex] : -1;
            _rebuildingPeopleScoutOptions = true;
            if (selectedIndex >= 0) _peopleScoutSettlement.Select(selectedIndex);
            _rebuildingPeopleScoutOptions = false;
        }
        if (_peopleScopeSelected >= 0 && _peopleScoutSettlementIds.IndexOf(_peopleScopeSelected) is var scoped and >= 0 &&
            _peopleSelectedScoutSettlement != _peopleScopeSelected)
        {
            _peopleSelectedScoutSettlement = _peopleScopeSelected;
            _rebuildingPeopleScoutOptions = true;
            _peopleScoutSettlement.Select(scoped);
            _rebuildingPeopleScoutOptions = false;
        }

        bool available = _peopleSelectedScoutSettlement >= 0 && frame.ViewerCiv >= 0;
        int selectedRow = -1;
        if (available)
            for (int s = 0; s < frame.SettlementCount; s++)
                if (frame.SettlementIndex[s] == _peopleSelectedScoutSettlement && IsOwnPeopleSettlement(frame, s))
                { selectedRow = s; break; }
        available &= selectedRow >= 0;
        _peopleScoutSettlement.Disabled = _peopleScoutSettlementIds.Count == 0;
        for (int i = 0; i < _peopleScoutButtons.Length; i++)
            _peopleScoutButtons[i].Disabled = !available || !(_session?.CanIssueCommands ?? false) ||
                i == (int)ScoutPolicy.Two && !HasWayfindingOne(frame);
        if (!available)
        {
            UiKit.SetText(_peopleScoutStatus, "No settlement is available to manage.");
            return;
        }

        int activeScouts = 0;
        for (int i = 0; i < frame.FigureCount; i++)
            if (frame.FigureSettlement[i] == _peopleSelectedScoutSettlement && frame.FigureActivity[i] == (byte)FigureActivity.Scout)
                activeScouts++;
        ScoutPolicy policy = (ScoutPolicy)frame.SettlementScoutPolicy[selectedRow];
        UiKit.SetText(_peopleScoutStatus,
            $"Settlement {_peopleSelectedScoutSettlement + 1} has {activeScouts} {(activeScouts == 1 ? "scout" : "scouts")} out this season.");
        for (int i = 0; i < _peopleScoutButtons.Length; i++)
            _peopleScoutButtons[i].SetPressedNoSignal(i == (int)policy);
    }

    private static bool IsOwnPeopleSettlement(MatchFrame frame, int index)
    {
        SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[index];
        return frame.SettlementCiv[index] == frame.ViewerCiv &&
            (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
    }

    private bool HasWayfindingOne(MatchFrame frame)
    {
        int project = _catalog?.IndexOf("WAY-01") ?? -1;
        if (project < 0 || project >= frame.FocusProjectCount) return false;
        ProjectState state = (ProjectState)frame.FocusProjectState[project];
        return state is ProjectState.Completed or ProjectState.Dormant;
    }

    private void SetPeopleScoutPolicy(ScoutPolicy policy)
    {
        if (_session is null || _current is null || _peopleSelectedScoutSettlement < 0) return;
        if (policy == ScoutPolicy.Two && !HasWayfindingOne(_current)) return;
        _session.Queue(PlayerCommands.SetScoutPolicy(_session.Tick, _current.ViewerCiv,
            _peopleSelectedScoutSettlement, policy));
    }
}
