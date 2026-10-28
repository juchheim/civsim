using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Player-facing view of work, wellbeing and research inputs for the player's people.</summary>
public partial class Game
{
    private sealed class PeopleMetric
    {
        public Label Value = null!, Detail = null!;
    }

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

    private readonly PeopleMetric?[] _peopleMetrics = new PeopleMetric?[4];
    private readonly PeopleBarRow[] _peopleWorkRows = new PeopleBarRow[PeopleWork.Length];
    private readonly PeopleBarRow[] _peopleKnowHowRows = new PeopleBarRow[3];
    private readonly int[] _peopleActivityCounts = new int[23];
    private readonly List<int> _peopleScoutSettlementIds = new();
    private readonly Button[] _peopleScoutButtons = new Button[4];
    private int _peopleSelectedScoutSettlement = -1;
    private bool _rebuildingPeopleScoutOptions;
    private Control? _peopleRoot;
    private ColorRect? _peopleScrim;
    private PanelContainer? _peoplePanel;
    private UiLayer? _peopleLayer;
    private bool _peopleOpen;
    private UiButton? _peopleWebButton, _peopleReturnButton;
    private Label? _peopleDate, _peopleResearchSummary, _peopleWorkNote, _peopleNoKnowHow;
    private Label? _peopleScoutStatus;
    private OptionButton? _peopleScoutSettlement;
    private Label? _peopleChildren, _peopleRecovering, _peopleResting, _peopleTraveling;
    private Label? _peoplePriests, _peopleScribes, _peopleWarriors;
    private Label? _peopleFoodShort, _peopleWinterRisk, _peopleHungry, _peopleCold, _peopleIll, _peopleWounded, _peopleBlockedMethods, _peopleStrained;

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
        var titleColumn = UiKit.Column(UiTokens.Sp1);
        titleColumn.AddChild(UiKit.Text("Your people", "Title"));
        _peopleDate = UiKit.Text("Year 1 · Spring", "CaptionTertiary");
        titleColumn.AddChild(_peopleDate);
        header.AddChild(titleColumn);
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (Esc)", ClosePeoplePanel));
        content.AddChild(header);

        var metricRow = UiKit.Row(UiTokens.Sp2);
        string[] metricNames = { "YOUR PEOPLE", "SETTLEMENTS", "RESEARCH SPEED", "AGE PROGRESS" };
        for (int i = 0; i < metricNames.Length; i++)
        {
            var card = UiKit.Panel("InnerCard");
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var stack = UiKit.Column(UiTokens.Sp1);
            stack.AddChild(UiKit.Text(metricNames[i], "EyebrowTertiary"));
            var metric = new PeopleMetric
            {
                Value = UiKit.Text("—", "NumTitle"),
                Detail = UiKit.Text("", "CaptionTertiary", ellipsis: true),
            };
            stack.AddChild(metric.Value);
            stack.AddChild(metric.Detail);
            card.AddChild(stack);
            metricRow.AddChild(card);
            _peopleMetrics[i] = metric;
        }
        content.AddChild(metricRow);

        var body = UiKit.Row(UiTokens.Sp3);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        PanelContainer workPanel = UiKit.Panel("WellPanel", "WorkAssignments");
        workPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        workPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        workPanel.SizeFlagsStretchRatio = 0.618f;
        var workContent = UiKit.Column(UiTokens.Sp2);
        workContent.AddChild(UiKit.Text("WORK ASSIGNMENTS · THIS SEASON", "Eyebrow"));
        _peopleWorkNote = UiKit.Text("Counts show each person's current job assignment.", "CaptionTertiary", wrap: true);
        workContent.AddChild(_peopleWorkNote);
        var workList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < PeopleWork.Length; i++)
        {
            _peopleWorkRows[i] = BuildPeopleBarRow(workList, PeopleWork[i].Name);
        }
        workContent.AddChild(workList);
        workContent.AddChild(UiKit.Divider());
        workContent.AddChild(UiKit.Text("SETTLEMENT SCOUTING LIMITS", "Eyebrow"));
        var scoutTargetRow = UiKit.Row(UiTokens.Sp2);
        scoutTargetRow.AddChild(UiKit.Text("Settlement", "Caption"));
        _peopleScoutSettlement = new OptionButton
        {
            CustomMinimumSize = new Vector2(185, UiTokens.ControlM),
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
        scoutTargetRow.AddChild(UiKit.Text("Scouts", "Caption"));
        workContent.AddChild(scoutTargetRow);
        var scoutPolicyRow = UiKit.Row(UiTokens.Sp1);
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
        workContent.AddChild(scoutPolicyRow);
        _peopleScoutStatus = UiKit.Text("Auto scouting yields workers when food is short. Changes apply next season.", "CaptionTertiary", wrap: true);
        workContent.AddChild(_peopleScoutStatus);
        workContent.AddChild(UiKit.Divider());
        workContent.AddChild(UiKit.Text("OTHER ACTIVITIES", "EyebrowTertiary"));
        var other = new HFlowContainer();
        other.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        other.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _peopleChildren = AddPeopleCountChip(other, "Children");
        _peopleRecovering = AddPeopleCountChip(other, "Recovering");
        _peopleResting = AddPeopleCountChip(other, "Resting or idle");
        _peopleTraveling = AddPeopleCountChip(other, "Away with a party");
        workContent.AddChild(other);
        workPanel.AddChild(workContent);
        body.AddChild(workPanel);

        PanelContainer contextPanel = UiKit.Panel("WellPanel", "ResearchAndNeeds");
        contextPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contextPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        contextPanel.SizeFlagsStretchRatio = 0.382f;
        var context = UiKit.Column(UiTokens.Sp2);
        context.AddChild(UiKit.Text("RESEARCH CONTEXT", "Eyebrow"));
        _peopleResearchSummary = UiKit.Text("Adults and elders add Commons Insight; elders contribute twice as much. Roles and institutions add to the total.", "CaptionTertiary", wrap: true);
        context.AddChild(_peopleResearchSummary);
        var roleRow = new HFlowContainer();
        roleRow.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        roleRow.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _peoplePriests = AddPeopleCountChip(roleRow, "Priests");
        _peopleScribes = AddPeopleCountChip(roleRow, "Scribes");
        _peopleWarriors = AddPeopleCountChip(roleRow, "Warriors");
        context.AddChild(roleRow);
        context.AddChild(UiKit.Text("Warriors and specialist roles may also appear in the work counts.", "CaptionTertiary", wrap: true));
        context.AddChild(UiKit.Divider());
        context.AddChild(UiKit.Text("STORED KNOW-HOW", "EyebrowTertiary"));
        context.AddChild(UiKit.Text("Practice builds Know-how in related fields. Matching projects can spend it automatically, up to half their cost.", "CaptionTertiary", wrap: true));
        var knowHowList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < _peopleKnowHowRows.Length; i++) _peopleKnowHowRows[i] = BuildPeopleBarRow(knowHowList, "—");
        context.AddChild(knowHowList);
        _peopleNoKnowHow = UiKit.Text("No stored Know-how yet. Current work assignments change with each season.", "CaptionTertiary", wrap: true);
        _peopleNoKnowHow.Visible = false;
        context.AddChild(_peopleNoKnowHow);
        context.AddChild(UiKit.Divider());
        context.AddChild(UiKit.Text("NEEDS & WELLBEING", "EyebrowTertiary"));
        var needs = new HFlowContainer();
        needs.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        needs.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _peopleFoodShort = AddPeopleCountChip(needs, "Food short");
        _peopleWinterRisk = AddPeopleCountChip(needs, "Winter risk");
        _peopleHungry = AddPeopleCountChip(needs, "Hungry");
        _peopleCold = AddPeopleCountChip(needs, "Cold");
        _peopleIll = AddPeopleCountChip(needs, "Ill");
        _peopleWounded = AddPeopleCountChip(needs, "Wounded");
        _peopleBlockedMethods = AddPeopleCountChip(needs, "Workers on older methods");
        _peopleStrained = AddPeopleCountChip(needs, "Strained settlements");
        context.AddChild(needs);
        contextPanel.AddChild(context);
        body.AddChild(contextPanel);
        content.AddChild(body);

        var footer = UiKit.Row(UiTokens.Sp2);
        footer.AddChild(UiKit.Text("Food reflects the latest season; winter risk appears in autumn and winter.", "CaptionTertiary", wrap: true));
        footer.AddChild(UiKit.Spacer(expand: true));
        _peopleReturnButton = UiKit.Secondary("Close", ClosePeoplePanel, null, UiGlyph.None, UiTokens.ControlS, true);
        footer.AddChild(_peopleReturnButton);
        content.AddChild(footer);

        _peopleLayer = RegisterLayer("people", true, () => { ClosePeoplePanel(); return true; });
        _hudRoot!.AddChild(_peopleRoot);
    }

    private static PeopleBarRow BuildPeopleBarRow(VBoxContainer parent, string name)
    {
        var row = UiKit.Row(UiTokens.Sp2);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        var result = new PeopleBarRow
        {
            Root = row,
            Name = UiKit.Text(name, "Caption", minWidth: 117, ellipsis: true),
            Bar = new UiProgressBar(4) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill },
            Count = UiKit.Text("0", "Num", minWidth: 36, right: true),
        };
        result.Root.AddChild(result.Name);
        result.Root.AddChild(result.Bar);
        result.Root.AddChild(result.Count);
        parent.AddChild(result.Root);
        return result;
    }

    private static Label AddPeopleCountChip(HFlowContainer parent, string name)
    {
        var chip = UiKit.Panel("ChipPanel", null, UiKit.MouseFilter.Ignore);
        var row = UiKit.Row(UiTokens.Sp1);
        row.AddChild(UiKit.Text(name, "CaptionTertiary"));
        Label count = UiKit.Text("0", "NumStrong");
        row.AddChild(count);
        chip.AddChild(row);
        parent.AddChild(chip);
        return count;
    }

    private void LayoutPeoplePanel()
    {
        if (_peoplePanel is null) return;
        float width = Math.Min(1100, _logicalSize.X - 2 * Margin);
        float height = Math.Min(705, _logicalSize.Y - 2 * Margin);
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

    private void OpenPeoplePanel()
    {
        if (_peopleOpen || _peopleRoot is null || _current is null) return;
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

    private void RefreshPeoplePanel(MatchFrame frame)
    {
        if (_peopleRoot is null || _catalog is null) return;
        int civ = frame.FocusCiv;
        if ((uint)civ >= (uint)frame.CivCount) return;
        RefreshPeopleScoutControls(frame);
        Array.Clear(_peopleActivityCounts);
        int children = 0, adults = 0, elders = 0, recovering = 0, resting = 0, traveling = 0;
        int priests = 0, scribes = 0, warriors = 0, hungry = 0, cold = 0, ill = 0, wounded = 0, blocked = 0;
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureCiv[i] != civ) continue;
            FigureFlags flags = (FigureFlags)frame.FigureFlags[i];
            if ((flags & FigureFlags.FreeFolk) != 0) continue;
            FigureActivity activity = (FigureActivity)frame.FigureActivity[i];
            if ((uint)activity < (uint)_peopleActivityCounts.Length) _peopleActivityCounts[(int)activity]++;
            switch ((AgeBand)frame.FigureAge[i])
            {
                case AgeBand.Child: children++; break;
                case AgeBand.Adult: adults++; break;
                case AgeBand.Elder: elders++; break;
            }
            if (activity == FigureActivity.Convalescing) recovering++;
            if (activity is FigureActivity.Rest or FigureActivity.Idle) resting++;
            if ((flags & FigureFlags.Away) != 0) traveling++;
            if ((byte)frame.FigureRole[i] == 1) priests++;
            if ((byte)frame.FigureRole[i] == 2) scribes++;
            if ((flags & FigureFlags.Warrior) != 0) warriors++;
            if ((flags & FigureFlags.Hungry) != 0) hungry++;
            if ((flags & FigureFlags.Cold) != 0) cold++;
            if ((flags & FigureFlags.Ill) != 0) ill++;
            if ((flags & FigureFlags.Wounded) != 0) wounded++;
            if (frame.FigureMethodFallback[i] != (byte)CivSim.Sim.Economy.MethodFallbackReason.None) blocked++;
        }

        int population = frame.CivPopulation[civ];
        for (int i = 0; i < _peopleWorkRows.Length; i++)
        {
            PeopleBarRow row = _peopleWorkRows[i];
            int count = _peopleActivityCounts[(int)PeopleWork[i].Activity];
            UiKit.SetVisible(row.Root, true);
            UiKit.SetText(row.Count, count.ToString("N0"));
            row.Bar.SetValue(population > 0 ? count / (float)population : 0, false);
        }

        UiKit.SetText(_peopleDate!, $"Year {frame.Year + 1} · {UiText.SeasonNames[frame.SeasonOfYear & 3]} · assignments change with the season");
        UiKit.SetText(_peopleMetrics[0]!.Value, population.ToString("N0"));
        UiKit.SetText(_peopleMetrics[0]!.Detail, $"{adults:N0} adults · {elders:N0} elders");
        UiKit.SetText(_peopleMetrics[1]!.Value, frame.CivSettlements[civ].ToString("N0"));
        UiKit.SetText(_peopleMetrics[1]!.Detail, "settlements with your people");
        UiKit.SetText(_peopleMetrics[2]!.Value, UiText.FormatInsight(frame.CivInsightPerYear[civ]) + " /yr");
        UiKit.SetText(_peopleMetrics[2]!.Detail, "total research speed");
        int needed = Math.Max(1, (int)frame.CivBreadthNeeded[civ]);
        UiKit.SetText(_peopleMetrics[3]!.Value, $"{Math.Min((int)frame.CivBreadthDone[civ], needed)}/{needed}");
        UiKit.SetText(_peopleMetrics[3]!.Detail, $"{UiText.AgeName(frame.CivEra[civ])} age projects known");

        UiKit.SetText(_peopleChildren!, children.ToString("N0"));
        UiKit.SetText(_peopleRecovering!, recovering.ToString("N0"));
        UiKit.SetText(_peopleResting!, resting.ToString("N0"));
        UiKit.SetText(_peopleTraveling!, traveling.ToString("N0"));
        UiKit.SetText(_peoplePriests!, priests.ToString("N0"));
        UiKit.SetText(_peopleScribes!, scribes.ToString("N0"));
        UiKit.SetText(_peopleWarriors!, warriors.ToString("N0"));

        int foodShort = 0, winterRisk = 0, strained = 0;
        bool winterForecastAvailable = frame.SeasonOfYear is 2 or 3;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (frame.SettlementCiv[i] != civ) continue;
            SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[i];
            if ((flags & (SettlementViewFlags.FreeFolk | SettlementViewFlags.Remembered)) != 0) continue;
            if ((flags & SettlementViewFlags.Own) == 0) continue;
            if (frame.SettlementFood[i] >= (byte)FoodBand.Short) foodShort++;
            if (winterForecastAvailable && frame.SettlementWinterForecast[i] >= (byte)FoodBand.Short) winterRisk++;
            if (frame.SettlementCohesion[i] >= (byte)CohesionBand.Strained) strained++;
        }
        UiKit.SetText(_peopleFoodShort!, foodShort.ToString("N0"));
        Control? winterChip = _peopleWinterRisk!.GetParent<Control>()?.GetParent<Control>();
        if (winterChip is not null) UiKit.SetVisible(winterChip, winterForecastAvailable);
        UiKit.SetText(_peopleWinterRisk, winterRisk.ToString("N0"));
        UiKit.SetText(_peopleHungry!, hungry.ToString("N0"));
        UiKit.SetText(_peopleCold!, cold.ToString("N0"));
        UiKit.SetText(_peopleIll!, ill.ToString("N0"));
        UiKit.SetText(_peopleWounded!, wounded.ToString("N0"));
        UiKit.SetText(_peopleBlockedMethods!, blocked.ToString("N0"));
        UiKit.SetText(_peopleStrained!, strained.ToString("N0"));

        Span<int> top = stackalloc int[3] { -1, -1, -1 };
        for (int lineage = 0; lineage < Math.Min(frame.FocusKnowHowCount, _catalog.LineageCodes.Count); lineage++)
        {
            if (frame.FocusKnowHow[lineage] <= 0) continue;
            for (int place = 0; place < top.Length; place++)
            {
                if (top[place] >= 0 && frame.FocusKnowHow[lineage] <= frame.FocusKnowHow[top[place]]) continue;
                for (int move = top.Length - 1; move > place; move--) top[move] = top[move - 1];
                top[place] = lineage;
                break;
            }
        }
        long maxKnowHow = top[0] >= 0 ? frame.FocusKnowHow[top[0]] : 0;
        for (int i = 0; i < _peopleKnowHowRows.Length; i++)
        {
            PeopleBarRow row = _peopleKnowHowRows[i];
            int lineage = top[i];
            bool visible = lineage >= 0;
            UiKit.SetVisible(row.Root, visible);
            if (!visible) continue;
            UiKit.SetText(row.Name, UiText.FieldName(_catalog, lineage, false));
            UiKit.SetText(row.Count, FormatFixed(frame.FocusKnowHow[lineage]));
            row.Bar.SetValue(maxKnowHow > 0 ? frame.FocusKnowHow[lineage] / (float)maxKnowHow : 0, false);
        }
        UiKit.SetVisible(_peopleNoKnowHow!, maxKnowHow == 0);
        UiKit.SetText(_peopleReturnButton!, _researchWebVisible ? "Back to Research" : "Close");
    }

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
            $"Settlement {_peopleSelectedScoutSettlement + 1} · {activeScouts} active this season · changes apply next season");
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
