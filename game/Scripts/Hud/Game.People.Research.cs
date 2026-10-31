using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// The Research tab: what is being researched, and for every learned project whether it is doing anything in the
/// settlements. Status is read from what exists (buildings standing, goods in store, workers on a method), so it says
/// "in use" or "not used yet", never what the project was worth.
/// </summary>
public partial class Game
{
    private enum ProjectUse { NotMeasured, Unused, InUse, PartlyBlocked, Blocked }

    // Learned-projects table columns; the header and every row use the same widths so they line up.
    private const int LearnedDateWidth = 120, LearnedStatusWidth = 160, LearnedValueWidth = 150;

    /// <summary>One learned project: name, date, status and the two gains on the first line; what it is doing on the second.</summary>
    private sealed class LearnedRow
    {
        public PanelContainer Root = null!;
        public Label Name = null!, Learned = null!, Last = null!, LastExtra = null!, Total = null!, TotalExtra = null!, Detail = null!;
        public UiPill Status = null!;
        public int Project = -1;
    }

    /// <summary>One research slot as a card: slot, name, progress, what finishing it does and what it would add.</summary>
    private sealed class ProgressRow
    {
        public Control Root = null!;
        public Label Name = null!, Eta = null!, Percent = null!, EffectsHead = null!, Effects = null!, Projection = null!;
        public UiProgressBar Bar = null!;
    }

    private readonly ProgressRow[] _progressRows = new ProgressRow[3];
    private readonly List<LearnedRow> _learnedRows = new();
    private readonly List<int> _learnedOrder = new();
    private readonly int[] _peopleBlockedByActivity = new int[23];
    private readonly PeopleBarRow[] _peopleKnowHowRows = new PeopleBarRow[3];
    private VBoxContainer? _learnedList;
    private UiSegmented? _learnedSort;
    private bool _learnedByWorth;
    private double[] _learnedWorth = Array.Empty<double>();
    private Label? _queueLine, _learnedEmpty, _researchSpeedValue, _peopleNoKnowHow;
    private UiPill? _learnedCount;
    private Control? _learnedTableHead;
    private string[]? _structureIds;

    private Control BuildPeopleResearchPane()
    {
        var content = UiKit.Column(UiTokens.Sp3);

        // In progress: one card per slot, equal heights.
        PeopleSection progress = NewPeopleSection("In progress", "InProgress");
        var cards = UiKit.Row(UiTokens.Sp3);
        for (int i = 0; i < _progressRows.Length; i++)
        {
            _progressRows[i] = BuildProgressCard(UiText.SlotNames[i].ToUpperInvariant());
            cards.AddChild(_progressRows[i].Root);
        }
        progress.Body.AddChild(cards);
        _queueLine = UiKit.Text("", "Caption", wrap: true);
        progress.Body.AddChild(_queueLine);
        content.AddChild(progress.Panel);

        // Learned projects: a two-line table.
        PeopleSection learned = NewPeopleSection("Learned projects", "LearnedProjects");
        _learnedCount = new UiPill("0");
        learned.Head.AddChild(_learnedCount);
        learned.Head.AddChild(UiKit.Spacer(expand: true));
        var sortLabel = UiKit.Text("Sort", "Caption");
        sortLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        learned.Head.AddChild(sortLabel);
        _learnedSort = new UiSegmented(new[] { "Newest", "Worth most" }, 0, i => { _learnedByWorth = i == 1; if (_current is not null) RefreshPeoplePanel(_current); }, 110);
        _learnedSort.SetTooltip(1, "Worth most orders by what each project added last season: rations, wood and lives together (a life counts as 25 rations).");
        learned.Head.AddChild(_learnedSort);
        _learnedEmpty = UiKit.Text("Nothing learned yet. What each project does shows up here once you have some.", "Caption", wrap: true);
        learned.Body.AddChild(_learnedEmpty);

        const string gainTip = "What last season would have lost without this project alone. Several projects' shares do not add up to the total change; all values are estimates.";
        var head = UiKit.Panel("TableHead", null, UiKit.MouseFilter.Ignore);
        var headRow = UiKit.Row(UiTokens.Sp3);
        var projectHead = UiKit.Text("PROJECT", "EyebrowTertiary");
        projectHead.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headRow.AddChild(projectHead);
        headRow.AddChild(UiKit.Cell("LEARNED", "EyebrowTertiary", LearnedDateWidth));
        headRow.AddChild(UiKit.Cell("STATUS", "EyebrowTertiary", LearnedStatusWidth));
        headRow.AddChild(InfoHeader("LAST SEASON", LearnedValueWidth, gainTip));
        headRow.AddChild(InfoHeader("SINCE LEARNED", LearnedValueWidth, gainTip + " Since learned adds every season up to now."));
        head.AddChild(headRow);
        _learnedTableHead = head;
        learned.Body.AddChild(head);
        _learnedList = UiKit.Column(2);
        learned.Body.AddChild(_learnedList);
        content.AddChild(learned.Panel);

        // Research speed and Know-how, side by side.
        PeopleSection speed = NewPeopleSection("Research speed & know-how", "ResearchSpeed");
        var speedRow = UiKit.Row(UiTokens.Sp5);
        var speedLeft = UiKit.Column(UiTokens.Sp1);
        speedLeft.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        speedLeft.SizeFlagsStretchRatio = 0.42f;
        speedLeft.AddChild(PeopleSubhead("INSIGHT"));
        var speedValueRow = UiKit.Row(UiTokens.Sp2);
        _researchSpeedValue = UiKit.Text("—", "NumTitle");
        speedValueRow.AddChild(_researchSpeedValue);
        var perYear = UiKit.Text("a year", "BodySecondary");
        perYear.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        speedValueRow.AddChild(perYear);
        speedLeft.AddChild(speedValueRow);
        speedLeft.AddChild(UiKit.Text("Adults and elders add Commons Insight (elders twice as much). Roles and institutions add more.", "Caption", wrap: true));
        speedRow.AddChild(speedLeft);
        var speedRight = UiKit.Column(UiTokens.Sp1);
        speedRight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        speedRight.SizeFlagsStretchRatio = 0.58f;
        speedRight.AddChild(PeopleSubhead("KNOW-HOW · TOP FIELDS"));
        var knowHowList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < _peopleKnowHowRows.Length; i++) _peopleKnowHowRows[i] = BuildPeopleBarRow(knowHowList, "—");
        speedRight.AddChild(knowHowList);
        _peopleNoKnowHow = UiKit.Text("No stored Know-how yet.", "Caption", wrap: true);
        speedRight.AddChild(_peopleNoKnowHow);
        speedRight.AddChild(UiKit.Text("Practice builds Know-how in related fields. Matching projects spend it automatically, up to half their cost.", "CaptionTertiary", wrap: true));
        speedRow.AddChild(speedRight);
        speed.Body.AddChild(speedRow);
        content.AddChild(speed.Panel);
        return PeopleScroll(content);
    }

    private static ProgressRow BuildProgressCard(string slotName)
    {
        PanelContainer card = UiKit.Panel("InnerCard", null, UiKit.MouseFilter.Ignore);
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var column = UiKit.Column(UiTokens.Sp2);
        var view = new ProgressRow
        {
            Root = card,
            Name = UiKit.Text("", "BodyStrong", wrap: true),
            Eta = UiKit.Text("", "Num", right: true),
            Percent = UiKit.Cell("", "NumStrong", 48, right: true),
            Bar = new UiProgressBar(6),
            EffectsHead = PeopleSubhead("WHEN DONE"),
            Effects = UiKit.Text("", "Caption", wrap: true),
            Projection = UiKit.Text("", "Caption", wrap: true),
        };
        var top = UiKit.Row(UiTokens.Sp2);
        var slot = UiKit.Text(slotName, "EyebrowTertiary");
        slot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(slot);
        top.AddChild(view.Eta);
        column.AddChild(top);
        column.AddChild(view.Name);
        var bar = UiKit.Row(UiTokens.Sp2);
        bar.AddChild(view.Bar);
        bar.AddChild(view.Percent);
        column.AddChild(bar);
        column.AddChild(UiKit.Spacer(height: 2));
        column.AddChild(view.EffectsHead);
        column.AddChild(view.Effects);
        column.AddChild(view.Projection);
        card.AddChild(column);
        return view;
    }

    /// <summary>A right-aligned column header with an info glyph; hovering it explains the column.</summary>
    private static Control InfoHeader(string text, int width, string tooltip)
    {
        var cell = UiKit.Row(UiTokens.Sp1);
        cell.CustomMinimumSize = new Vector2(width, 0);
        cell.MouseFilter = Control.MouseFilterEnum.Stop;
        cell.TooltipText = tooltip;
        cell.Alignment = BoxContainer.AlignmentMode.End;
        cell.AddChild(new UiIcon(UiGlyph.Info, 14, UiTokens.TextTertiary));
        cell.AddChild(UiKit.Text(text, "EyebrowTertiary"));
        return cell;
    }

    private void RefreshPeopleResearch(MatchFrame frame, int civ)
    {
        RefreshProgressRows(frame);
        RefreshLearnedRows(frame);
        RefreshResearchSpeed(frame, civ);
    }

    private void RefreshProgressRows(MatchFrame frame)
    {
        for (int slot = 0; slot < _progressRows.Length; slot++)
        {
            ProgressRow row = _progressRows[slot];
            int project = frame.FocusSlotProject[slot];
            if (project < 0 || project >= _catalog!.Count)
            {
                UiKit.SetText(row.Name, "Empty slot");
                SetTone(row.Name, UiTokens.TextTertiary);
                UiKit.SetText(row.Eta, "");
                UiKit.SetText(row.Percent, "");
                row.Bar.SetValue(0, false);
                UiKit.SetVisible(row.Bar.GetParent<Control>(), false);
                UiKit.SetVisible(row.EffectsHead, false);
                UiKit.SetText(row.Effects, "Choose a project in the research web (T).");
                UiKit.SetVisible(row.Projection, false);
                continue;
            }
            ProjectDef def = _catalog.Projects[project];
            SetTone(row.Name, UiTokens.TextPrimary);
            UiKit.SetText(row.Name, def.Name);
            UiKit.SetText(row.Percent, $"{frame.FocusSlotPermille[slot] / 10}%");
            UiKit.SetText(row.Eta, FormatEta(slot, frame.FocusSlotPermille[slot]));
            row.Bar.SetValue(frame.FocusSlotPermille[slot] / 1000f, false);
            UiKit.SetVisible(row.Bar.GetParent<Control>(), true);
            List<string> effects = UiText.DescribeEffects(def, 3);
            UiKit.SetVisible(row.EffectsHead, effects.Count > 0);
            UiKit.SetText(row.Effects, effects.Count > 0 ? string.Join("\n", effects) : def.Flavor);
            (string projection, Color tone) = ProjectionText(frame, slot);
            UiKit.SetVisible(row.Projection, projection.Length > 0);
            UiKit.SetText(row.Projection, projection);
            SetTone(row.Projection, tone);
        }
        if (frame.FocusQueueCount > 0)
        {
            var names = new List<string>(frame.FocusQueueCount);
            for (int i = 0; i < frame.FocusQueueCount; i++)
                if ((uint)frame.FocusQueue[i] < (uint)_catalog!.Count) names.Add(_catalog.Projects[frame.FocusQueue[i]].Name);
            UiKit.SetText(_queueLine!, "Up next: " + string.Join(", ", names));
        }
        else UiKit.SetText(_queueLine!, "Nothing is queued behind these.");
    }

    private void EnsureStructureIds()
    {
        if (_structureIds is not null) return;
        _structureIds = new string[LivingEconomyModel.StructureKindCount];
        for (int k = 0; k < _structureIds.Length; k++) _structureIds[k] = LivingEconomyModel.StructureName(k);
    }

    private void RefreshLearnedRows(MatchFrame frame)
    {
        EnsureStructureIds();
        Array.Clear(_peopleBlockedByActivity);
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureCiv[i] != frame.FocusCiv || frame.FigureMethodFallback[i] == 0) continue;
            if (_peopleScopeSelected >= 0 && frame.FigureSettlement[i] != _peopleScopeSelected) continue;
            int activity = frame.FigureActivity[i];
            if ((uint)activity < (uint)_peopleBlockedByActivity.Length) _peopleBlockedByActivity[activity]++;
        }

        _learnedOrder.Clear();
        for (int p = 0; p < _catalog!.Count && p < frame.FocusProjectCount; p++)
        {
            ProjectState state = (ProjectState)frame.FocusProjectState[p];
            if (state is ProjectState.Completed or ProjectState.Dormant) _learnedOrder.Add(p);
        }
        int[] done = frame.FocusProjectDoneSeason;
        if (_learnedWorth.Length < _catalog.Count) _learnedWorth = new double[_catalog.Count];
        foreach (int project in _learnedOrder)
        {
            PayoffSum sum = SumPayoff(frame, project);
            _learnedWorth[project] = sum.Measured ? sum.Last.Rations + sum.Last.Wood * 0.5 + sum.Last.Lives * 25 : -1;
        }
        bool byWorth = _learnedByWorth;
        double[] worth = _learnedWorth;
        _learnedOrder.Sort((a, b) =>
        {
            int c = byWorth ? worth[b].CompareTo(worth[a]) : 0;
            if (c == 0) c = done[b].CompareTo(done[a]);
            return c != 0 ? c : a.CompareTo(b);
        });

        _learnedCount!.Set(_learnedOrder.Count.ToString("N0"), UiPill.Tone.Neutral);
        UiKit.SetVisible(_learnedEmpty!, _learnedOrder.Count == 0);
        UiKit.SetVisible(_learnedTableHead!, _learnedOrder.Count > 0);
        while (_learnedRows.Count < _learnedOrder.Count) AddLearnedRow();
        for (int i = 0; i < _learnedRows.Count; i++)
        {
            LearnedRow row = _learnedRows[i];
            if (i >= _learnedOrder.Count) { UiKit.SetVisible(row.Root, false); continue; }
            int project = _learnedOrder[i];
            ProjectDef def = _catalog.Projects[project];
            UiKit.SetVisible(row.Root, true);
            if (row.Project != project)
            {
                row.Project = project;
                UiKit.SetText(row.Name, def.Name);
                row.Root.TooltipText = string.Join("\n", UiText.DescribeEffects(def, 5));
            }
            int season = done[project];
            UiKit.SetText(row.Learned, season >= 0 ? $"Year {season / 4 + 1} {UiText.SeasonNames[season & 3]}" : "—");
            (ProjectUse use, string usage) = AssessProject(frame, def, (ProjectState)frame.FocusProjectState[project]);
            PayoffSum payoff = SumPayoff(frame, project);
            bool tooEarly = season >= frame.Season - 1;
            string label, detail;
            UiPill.Tone tone;
            (string Main, string Extra) last = ("—", ""), total = ("—", "");
            if (payoff.Measured)
            {
                (label, tone, string payoffDetail) = PayoffStatusText(payoff, tooEarly);
                if (!tooEarly)
                {
                    last = PayoffCell(payoff.Last, perYear: true);
                    total = PayoffCell(payoff.Total, perYear: false);
                }
                bool usageIsEmpty = usage.Length == 0;
                detail = usageIsEmpty ? payoffDetail : payoffDetail + " " + usage;
            }
            else
            {
                (label, tone) = use switch
                {
                    ProjectUse.InUse => ("In use", UiPill.Tone.Success),
                    ProjectUse.PartlyBlocked => ("Partly blocked", UiPill.Tone.Warning),
                    ProjectUse.Blocked => ("Blocked", UiPill.Tone.Danger),
                    ProjectUse.Unused => ("Not used yet", UiPill.Tone.Info),
                    _ => ("No direct measure", UiPill.Tone.Neutral),
                };
                detail = usage.Length > 0 ? usage : "Its effects are not tracked as numbers yet.";
            }
            row.Status.Set(label, tone);
            SetPayoffCell(row.Last, row.LastExtra, last);
            SetPayoffCell(row.Total, row.TotalExtra, total);
            UiKit.SetText(row.Detail, detail);
            SetTone(row.Detail, !payoff.Measured && use == ProjectUse.NotMeasured ? UiTokens.TextTertiary : UiTokens.TextSecondary);
            UiKit.SetVariation(row.Root, (i & 1) == 0 ? "TableRowAlt" : "TableRow");
        }
    }

    private static void SetPayoffCell(Label main, Label extra, (string Main, string Extra) value)
    {
        UiKit.SetText(main, value.Main);
        SetTone(main, value.Main == "—" ? UiTokens.TextTertiary : UiTokens.TextPrimary);
        UiKit.SetText(extra, value.Extra);
        UiKit.SetVisible(extra, value.Extra.Length > 0);
    }

    private struct PayoffAmounts { public double Rations, Wood, Lives; }

    private struct PayoffSum
    {
        public bool Measured;
        public int Paying, Blocked, Superseded, Idle;
        public PayoffAmounts Last, Total;
        public byte Reason, Tool;
    }

    /// <summary>Adds a project's payoff over the settlements in scope.</summary>
    private PayoffSum SumPayoff(MatchFrame frame, int project)
    {
        var sum = new PayoffSum();
        int m = Array.IndexOf(frame.PayoffProject, project, 0, frame.PayoffProjectCount);
        if (m < 0) return sum;
        sum.Measured = true;
        int count = frame.PayoffProjectCount;
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!PeopleRowHasEconomy(frame, w)) continue;
            int o = (w * count + m) * 3;
            sum.Last.Rations += Units(frame.SettlementPayoff[o]); sum.Last.Wood += Units(frame.SettlementPayoff[o + 1]); sum.Last.Lives += Units(frame.SettlementPayoff[o + 2]);
            sum.Total.Rations += Units(frame.SettlementPayoffTotal[o]); sum.Total.Wood += Units(frame.SettlementPayoffTotal[o + 1]); sum.Total.Lives += Units(frame.SettlementPayoffTotal[o + 2]);
            switch ((LivingEconomyModel.PayoffStatus)frame.SettlementPayoffInfo[o])
            {
                case LivingEconomyModel.PayoffStatus.Paying: sum.Paying++; break;
                case LivingEconomyModel.PayoffStatus.Blocked:
                    sum.Blocked++;
                    if (sum.Reason == 0) { sum.Reason = frame.SettlementPayoffInfo[o + 1]; sum.Tool = frame.SettlementPayoffInfo[o + 2]; }
                    break;
                case LivingEconomyModel.PayoffStatus.Superseded: sum.Superseded++; break;
                default: sum.Idle++; break;
            }
        }
        return sum;
    }

    /// <summary>The gains worth showing, largest kind first: "+3.7 R", "+1.2 wood", "0.04 lives a year". Near-zero parts are left out.</summary>
    private static List<string> PayoffParts(PayoffAmounts a, bool perYear)
    {
        var parts = new List<string>(3);
        if (a.Rations >= 0.05) parts.Add($"+{FormatUnits(a.Rations)} R");
        if (a.Wood >= 0.05) parts.Add($"+{FormatUnits(a.Wood)} wood");
        double lives = perYear ? a.Lives * 4 : a.Lives;
        if (lives >= 0.01)
        {
            string count = lives >= 10 ? lives.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                : lives.ToString(lives >= 1 ? "0.#" : "0.##", System.Globalization.CultureInfo.InvariantCulture);
            parts.Add($"{count} {(count == "1" ? "life" : "lives")}{(perYear ? " a year" : "")}");
        }
        return parts;
    }

    /// <summary>A table cell: the first gain as the main value, any others on a quieter second line.</summary>
    private static (string Main, string Extra) PayoffCell(PayoffAmounts a, bool perYear)
    {
        List<string> parts = PayoffParts(a, perYear);
        if (parts.Count == 0) return ("—", "");
        return (parts[0], string.Join("\n", parts.GetRange(1, parts.Count - 1)));
    }

    private static string PeopleToolName(byte tool)
    {
        string name = ((MethodToolId)tool).ToString();
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) text.Append(' ');
            text.Append(char.ToLowerInvariant(name[i]));
        }
        return text.ToString();
    }

    private static (string Label, UiPill.Tone Tone, string Detail) PayoffStatusText(in PayoffSum p, bool tooEarly)
    {
        if (tooEarly) return ("Too early", UiPill.Tone.Info, "Learned within the last season; numbers start with the first full season.");
        if (p.Paying > 0 && p.Blocked > 0)
            return ("Partly blocked", UiPill.Tone.Warning, $"{p.Blocked} of {p.Paying + p.Blocked} settlements are held back: {PeopleBlockedWhy(p)}.");
        if (p.Paying > 0) return ("Paying off", UiPill.Tone.Success, "It added to last season's output.");
        if (p.Blocked > 0) return ("Blocked", UiPill.Tone.Danger, PeopleSentence(PeopleBlockedWhy(p)) + ".");
        if (p.Superseded > 0 && p.Idle == 0) return ("Superseded", UiPill.Tone.Info, "A better method took its place.");
        return ("Idle", UiPill.Tone.Neutral, "There was nothing for it to act on last season.");
    }

    private static string PeopleBlockedWhy(in PayoffSum p)
    {
        var reason = (MethodFallbackReason)p.Reason;
        string tool = p.Tool != 0 ? $" ({PeopleToolName(p.Tool)})" : "";
        return reason == MethodFallbackReason.MissingTool ? $"missing a tool{tool}" : PeopleFallbackWords(reason);
    }

    private static string PeopleSentence(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>What a project in a research slot would add at today's flows, or why it cannot yet, with the tone to show it in.</summary>
    private (string Text, Color Tone) ProjectionText(MatchFrame frame, int slot)
    {
        var status = (LivingEconomyModel.PayoffStatus)frame.FocusSlotProjectionInfo[slot * 3];
        if (status == LivingEconomyModel.PayoffStatus.NotMeasured) return ("", UiTokens.TextSecondary);
        var amounts = new PayoffAmounts
        {
            Rations = Units(frame.FocusSlotProjection[slot * 3]), Wood = Units(frame.FocusSlotProjection[slot * 3 + 1]),
            Lives = Units(frame.FocusSlotProjection[slot * 3 + 2]),
        };
        string tool = frame.FocusSlotProjectionInfo[slot * 3 + 2] != 0 ? PeopleToolName(frame.FocusSlotProjectionInfo[slot * 3 + 2]) : "";
        var reason = (MethodFallbackReason)frame.FocusSlotProjectionInfo[slot * 3 + 1];
        switch (status)
        {
            case LivingEconomyModel.PayoffStatus.Paying:
            {
                var pieces = new List<string>(2);
                List<string> seasonal = PayoffParts(new PayoffAmounts { Rations = amounts.Rations, Wood = amounts.Wood }, perYear: false);
                if (seasonal.Count > 0) pieces.Add(string.Join(" and ", seasonal) + " a season");
                pieces.AddRange(PayoffParts(new PayoffAmounts { Lives = amounts.Lives }, perYear: true));
                return pieces.Count == 0
                    ? ("No measurable gain at last season's work.", UiTokens.TextTertiary)
                    : ($"Would add {string.Join(", plus ", pieces)} at last season's work, across all settlements.", UiTokens.Success);
            }
            case LivingEconomyModel.PayoffStatus.Blocked:
                return (reason == MethodFallbackReason.HuntParty ? "Needs a bigger hunting party before it does anything."
                    : tool.Length > 0 ? $"Needs {tool} before it does anything." : "Blocked until something it needs exists.", UiTokens.Warning);
            default:
                return ("No measurable gain yet: there is no work for it to speed up.", UiTokens.TextTertiary);
        }
    }

    private void AddLearnedRow()
    {
        var root = UiKit.Panel("TableRow", null, UiKit.MouseFilter.Pass);
        var column = UiKit.Column(2);
        var line = UiKit.Row(UiTokens.Sp3);
        var row = new LearnedRow
        {
            Root = root,
            Name = UiKit.Text("", "BodyStrong", wrap: true),
            Learned = UiKit.Cell("", "Caption", LearnedDateWidth),
            Status = new UiPill(),
            Last = UiKit.Text("", "NumBody", right: true),
            LastExtra = UiKit.Text("", "Num", right: true),
            Total = UiKit.Text("", "NumBody", right: true),
            TotalExtra = UiKit.Text("", "Num", right: true),
            Detail = UiKit.Text("", "Caption", wrap: true),
        };
        row.Learned.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        line.AddChild(row.Name);
        line.AddChild(row.Learned);
        var status = UiKit.Row(0);
        status.CustomMinimumSize = new Vector2(LearnedStatusWidth, 0);
        status.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.Status.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        status.AddChild(row.Status);
        line.AddChild(status);
        line.AddChild(ValueCell(row.Last, row.LastExtra));
        line.AddChild(ValueCell(row.Total, row.TotalExtra));
        column.AddChild(line);
        column.AddChild(row.Detail);
        root.AddChild(column);
        _learnedList!.AddChild(root);
        _learnedRows.Add(row);
    }

    private static Control ValueCell(Label main, Label extra)
    {
        var cell = UiKit.Column(0);
        cell.CustomMinimumSize = new Vector2(LearnedValueWidth, 0);
        cell.MouseFilter = Control.MouseFilterEnum.Ignore;
        cell.AddChild(main);
        cell.AddChild(extra);
        return cell;
    }

    private static FigureActivity? ActivityOfAction(string? action) => action switch
    {
        "forage" => FigureActivity.Forage, "hunt" => FigureActivity.Hunt, "fish" => FigureActivity.Fish, "farm" => FigureActivity.Farm,
        "chop" => FigureActivity.Chop, "gather" => FigureActivity.Gather, "craft" => FigureActivity.Craft, "herd" => FigureActivity.Herd,
        "preserve" => FigureActivity.Preserve, _ => null,
    };

    /// <summary>"pit house" → "pit houses", "granary" → "granaries".</summary>
    private static string PeoplePlural(string noun, int count)
    {
        if (count == 1 || noun.Length == 0) return noun;
        if (noun.EndsWith("y", StringComparison.Ordinal) && noun.Length > 1 && "aeiou".IndexOf(noun[^2]) < 0) return noun[..^1] + "ies";
        if (noun.EndsWith("s", StringComparison.Ordinal) || noun.EndsWith("sh", StringComparison.Ordinal) ||
            noun.EndsWith("ch", StringComparison.Ordinal) || noun.EndsWith("x", StringComparison.Ordinal)) return noun + "es";
        return noun + "s";
    }

    /// <summary>
    /// What the project's measurable effects look like in the settlements right now, as one sentence ("2 granaries standing;
    /// no farmers right now."), or "" when none of its effects is counted.
    /// </summary>
    private (ProjectUse Use, string Detail) AssessProject(MatchFrame frame, ProjectDef def, ProjectState state)
    {
        bool anyInUse = false, anyUnused = false, partlyBlocked = false;
        var parts = new List<string>(3);
        int kinds = MatchFrame.StructureKinds;
        for (int i = 0; i < def.Effects.Length; i++)
        {
            EffectDef e = def.Effects[i];
            switch (e.Kind)
            {
                case EffectKind.Build when e.Id is not null:
                {
                    int kind = Array.IndexOf(_structureIds!, e.Id);
                    if (kind < 0) break;
                    int count = 0;
                    for (int w = 0; w < frame.SettlementCount; w++)
                        if (PeopleInScope(frame, w)) count += frame.SettlementStructures[w * kinds + kind];
                    string name = UiText.StructureName(e.Id).ToLowerInvariant();
                    if (count > 0) { anyInUse = true; parts.Add($"{count} {PeoplePlural(name, count)} standing"); }
                    else { anyUnused = true; parts.Add($"no {name} built yet"); }
                    break;
                }
                case EffectKind.Good when e.Id is not null:
                {
                    int slot = StoreSlotById(e.Id);
                    if (slot < 0) break;
                    double stock = Units(ScopeStock(frame, slot));
                    string name = UiText.GoodName(e.Id).ToLowerInvariant();
                    if (stock > 0.05) { anyInUse = true; parts.Add($"{FormatUnits(stock)} {name} in store"); }
                    else { anyUnused = true; parts.Add($"no {name} in store"); }
                    break;
                }
                case EffectKind.Method or EffectKind.UnlockAction:
                {
                    if (ActivityOfAction(e.Action) is not { } activity) break;
                    int workers = _peopleActivityCounts[(int)activity], blocked = _peopleBlockedByActivity[(int)activity];
                    string word = PeopleActivityWord(activity, 2);
                    if (workers == 0) { anyUnused = true; parts.Add($"no {word} right now"); }
                    else if (e.Kind == EffectKind.Method && blocked > 0) { partlyBlocked = true; parts.Add($"{blocked} of {workers} {word} still on an older method"); }
                    else { anyInUse = true; parts.Add($"{workers} {PeopleActivityWord(activity, workers)} at work"); }
                    break;
                }
            }
        }
        for (int i = parts.Count - 1; i > 0; i--)
            if (parts.IndexOf(parts[i]) < i) parts.RemoveAt(i);
        string detail = parts.Count > 0 ? PeopleSentence(string.Join("; ", parts)) + "." : "";
        if (state == ProjectState.Dormant) return (ProjectUse.Blocked, ("A tool or input it needs is missing, so the old method is used. " + detail).TrimEnd());
        if (partlyBlocked) return (ProjectUse.PartlyBlocked, detail);
        if (anyInUse) return (ProjectUse.InUse, detail);
        if (anyUnused) return (ProjectUse.Unused, detail);
        return (ProjectUse.NotMeasured, detail);
    }

    private void RefreshResearchSpeed(MatchFrame frame, int civ)
    {
        UiKit.SetText(_researchSpeedValue!, UiText.FormatInsight(frame.CivInsightPerYear[civ]));
        Span<int> top = stackalloc int[3] { -1, -1, -1 };
        for (int lineage = 0; lineage < Math.Min(frame.FocusKnowHowCount, _catalog!.LineageCodes.Count); lineage++)
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
    }
}
