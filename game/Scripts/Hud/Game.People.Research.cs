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

    private sealed class LearnedRow
    {
        public Control Root = null!;
        public Label Name = null!, Learned = null!, Status = null!, Last = null!, Total = null!, Detail = null!;
        public int Project = -1;
    }

    private sealed class ProgressRow
    {
        public Control Root = null!;
        public Label Name = null!, Eta = null!, Effects = null!;
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
    private readonly string[] _projectionText = new string[3];
    private Label? _queueLine, _learnedHeader, _learnedEmpty, _researchSpeedLine, _peopleNoKnowHow;
    private string[]? _structureIds;

    private Control BuildPeopleResearchPane()
    {
        var content = UiKit.Column(UiTokens.Sp3);

        PanelContainer progress = UiKit.Panel("WellPanel", "InProgress");
        var progressContent = UiKit.Column(UiTokens.Sp2);
        progressContent.AddChild(UiKit.Text("IN PROGRESS", "Eyebrow"));
        string[] slotNames = { "PRIMARY", "SECOND", "THIRD" };
        for (int i = 0; i < _progressRows.Length; i++)
        {
            var root = UiKit.Column(UiTokens.Sp1);
            var line = UiKit.Row(UiTokens.Sp2);
            var view = new ProgressRow
            {
                Root = root,
                Name = UiKit.Text("", "BodyStrong", ellipsis: true),
                Eta = UiKit.Text("", "Num", minWidth: 90, right: true),
                Bar = new UiProgressBar(5),
                Effects = UiKit.Text("", "CaptionTertiary", wrap: true),
            };
            line.AddChild(UiKit.Text(slotNames[i], "EyebrowTertiary", minWidth: 70));
            line.AddChild(view.Name);
            line.AddChild(view.Eta);
            root.AddChild(line);
            root.AddChild(view.Bar);
            root.AddChild(view.Effects);
            progressContent.AddChild(root);
            _progressRows[i] = view;
        }
        _queueLine = UiKit.Text("", "CaptionTertiary", wrap: true);
        progressContent.AddChild(_queueLine);
        progress.AddChild(progressContent);
        content.AddChild(progress);

        PanelContainer learned = UiKit.Panel("WellPanel", "LearnedProjects");
        var learnedContent = UiKit.Column(UiTokens.Sp2);
        var learnedHead = UiKit.Row(UiTokens.Sp3);
        _learnedHeader = UiKit.Text("LEARNED PROJECTS", "Eyebrow");
        learnedHead.AddChild(_learnedHeader);
        learnedHead.AddChild(UiKit.Spacer(expand: true));
        learnedHead.AddChild(UiKit.Text("Sort", "CaptionTertiary"));
        _learnedSort = new UiSegmented(new[] { "Newest", "Worth most" }, 0, i => { _learnedByWorth = i == 1; if (_current is not null) RefreshPeoplePanel(_current); }, 96);
        _learnedSort.SetTooltip(1, "Worth most orders by what each project adds a season: rations, wood and lives together (a life counts as 25 rations).");
        learnedHead.AddChild(_learnedSort);
        learnedContent.AddChild(learnedHead);
        learnedContent.AddChild(UiKit.Text("Values are what last season would have lost without that project alone, so shares of several projects do not add up to the total change. ≈ marks a counted estimate.", "CaptionTertiary", wrap: true));
        _learnedEmpty = UiKit.Text("Nothing learned yet. What each project does shows up here once you have some.", "CaptionTertiary", wrap: true);
        learnedContent.AddChild(_learnedEmpty);
        var head = UiKit.Row(UiTokens.Sp2);
        head.AddChild(FixedWidthText("PROJECT", "EyebrowTertiary", 170));
        head.AddChild(FixedWidthText("LEARNED", "EyebrowTertiary", 108));
        head.AddChild(FixedWidthText("STATUS", "EyebrowTertiary", 128));
        head.AddChild(FixedWidthText("LAST SEASON", "EyebrowTertiary", 128, right: true));
        head.AddChild(FixedWidthText("SINCE LEARNED", "EyebrowTertiary", 128, right: true));
        head.AddChild(UiKit.Text("WHAT IT IS DOING", "EyebrowTertiary"));
        learnedContent.AddChild(head);
        _learnedList = UiKit.Column(UiTokens.Sp1);
        learnedContent.AddChild(_learnedList);
        learned.AddChild(learnedContent);
        content.AddChild(learned);

        PanelContainer speed = UiKit.Panel("WellPanel", "ResearchSpeed");
        var speedContent = UiKit.Column(UiTokens.Sp2);
        speedContent.AddChild(UiKit.Text("RESEARCH SPEED & KNOW-HOW", "Eyebrow"));
        _researchSpeedLine = UiKit.Text("", "Caption", wrap: true);
        speedContent.AddChild(_researchSpeedLine);
        speedContent.AddChild(UiKit.Text("Practice builds Know-how in related fields. Matching projects can spend it automatically, up to half their cost.", "CaptionTertiary", wrap: true));
        var knowHowList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < _peopleKnowHowRows.Length; i++) _peopleKnowHowRows[i] = BuildPeopleBarRow(knowHowList, "—");
        speedContent.AddChild(knowHowList);
        _peopleNoKnowHow = UiKit.Text("No stored Know-how yet.", "CaptionTertiary", wrap: true);
        speedContent.AddChild(_peopleNoKnowHow);
        speed.AddChild(speedContent);
        content.AddChild(speed);
        return UiKit.Scroll(content);
    }

    private static Label FixedWidthText(string text, string style, int width, bool right = false)
    {
        Label label = UiKit.Text(text, style, minWidth: width, ellipsis: true, right: right);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        return label;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

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
                row.Bar.SetValue(0, false);
                UiKit.SetText(row.Effects, "Choose a project in the research web (T).");
                continue;
            }
            ProjectDef def = _catalog.Projects[project];
            SetTone(row.Name, UiTokens.TextPrimary);
            UiKit.SetText(row.Name, def.Name);
            UiKit.SetText(row.Eta, $"{frame.FocusSlotPermille[slot] / 10}% · {FormatEta(slot, frame.FocusSlotPermille[slot])}");
            row.Bar.SetValue(frame.FocusSlotPermille[slot] / 1000f, false);
            List<string> effects = UiText.DescribeEffects(def, 3);
            for (int i = 0; i < effects.Count; i++) effects[i] = Shorten(effects[i], 90);
            string projection = ProjectionText(frame, slot);
            UiKit.SetText(row.Effects, (effects.Count > 0 ? "When done: " + string.Join(" · ", effects) : def.Flavor) +
                (projection.Length > 0 ? "\n" + projection : ""));
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

        UiKit.SetText(_learnedHeader!, $"LEARNED PROJECTS · {_learnedOrder.Count}");
        UiKit.SetVisible(_learnedEmpty!, _learnedOrder.Count == 0);
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
            (ProjectUse use, string detail) = AssessProject(frame, def, (ProjectState)frame.FocusProjectState[project]);
            PayoffSum payoff = SumPayoff(frame, project);
            bool tooEarly = season >= frame.Season - 1;
            string label, last = "", total = "";
            Color tone;
            if (payoff.Measured)
            {
                (label, tone, string payoffDetail) = PayoffStatusText(payoff, tooEarly);
                if (!tooEarly)
                {
                    last = PayoffAmount(payoff.Last, perYear: true);
                    total = PayoffAmount(payoff.Total, perYear: false);
                }
                bool usageIsEmpty = detail.StartsWith("Its effects", StringComparison.Ordinal);
                detail = usageIsEmpty ? payoffDetail : payoffDetail + " · " + detail;
            }
            else
            {
                (label, tone) = use switch
                {
                    ProjectUse.InUse => ("In use", UiTokens.Success),
                    ProjectUse.PartlyBlocked => ("Partly blocked", UiTokens.Warning),
                    ProjectUse.Blocked => ("Blocked", UiTokens.Danger),
                    ProjectUse.Unused => ("Not used yet", UiTokens.Info),
                    _ => ("No direct measure", UiTokens.TextTertiary),
                };
            }
            UiKit.SetText(row.Status, label);
            SetTone(row.Status, tone);
            UiKit.SetText(row.Last, last);
            UiKit.SetText(row.Total, total);
            UiKit.SetText(row.Detail, detail);
        }
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

    private static string PayoffAmount(PayoffAmounts a, bool perYear)
    {
        var parts = new List<string>(3);
        if (a.Rations >= 0.05) parts.Add($"≈ +{FormatUnits(a.Rations)} R");
        if (a.Wood >= 0.05) parts.Add($"≈ +{FormatUnits(a.Wood)} wood");
        double lives = perYear ? a.Lives * 4 : a.Lives;
        if (lives >= 0.0005) parts.Add($"≈ {lives:0.###} lives{(perYear ? "/yr" : "")}");
        return parts.Count == 0 ? "—" : string.Join(" · ", parts);
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

    private static (string Label, Color Tone, string Detail) PayoffStatusText(in PayoffSum p, bool tooEarly)
    {
        if (tooEarly) return ("Too early", UiTokens.Info, "Learned within the last season; numbers start with the first full season.");
        if (p.Paying > 0 && p.Blocked > 0)
            return ("Partly blocked", UiTokens.Warning, $"{p.Blocked} of {p.Paying + p.Blocked} settlements are held back: {PeopleBlockedWhy(p)}.");
        if (p.Paying > 0) return ("Paying off", UiTokens.Success, "It added to last season's output.");
        if (p.Blocked > 0) return ("Blocked", UiTokens.Danger, PeopleSentence(PeopleBlockedWhy(p)) + ".");
        if (p.Superseded > 0 && p.Idle == 0) return ("Superseded", UiTokens.Info, "A better method took its place.");
        return ("Idle", UiTokens.TextSecondary, "There was nothing for it to act on last season.");
    }

    private static string PeopleBlockedWhy(in PayoffSum p)
    {
        var reason = (MethodFallbackReason)p.Reason;
        string tool = p.Tool != 0 ? $" ({PeopleToolName(p.Tool)})" : "";
        return reason == MethodFallbackReason.MissingTool ? $"missing a tool{tool}" : PeopleFallbackWords(reason);
    }

    private static string PeopleSentence(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>What a project in a research slot would add at today's flows, or why it cannot yet.</summary>
    private string ProjectionText(MatchFrame frame, int slot)
    {
        var status = (LivingEconomyModel.PayoffStatus)frame.FocusSlotProjectionInfo[slot * 3];
        if (status == LivingEconomyModel.PayoffStatus.NotMeasured) return "";
        var amounts = new PayoffAmounts
        {
            Rations = Units(frame.FocusSlotProjection[slot * 3]), Wood = Units(frame.FocusSlotProjection[slot * 3 + 1]),
            Lives = Units(frame.FocusSlotProjection[slot * 3 + 2]),
        };
        string tool = frame.FocusSlotProjectionInfo[slot * 3 + 2] != 0 ? PeopleToolName(frame.FocusSlotProjectionInfo[slot * 3 + 2]) : "";
        var reason = (MethodFallbackReason)frame.FocusSlotProjectionInfo[slot * 3 + 1];
        string gain = PayoffAmount(amounts, perYear: true);
        switch (status)
        {
            case LivingEconomyModel.PayoffStatus.Paying:
                return $"At last season's work it would add {gain}, across all settlements.";
            case LivingEconomyModel.PayoffStatus.Blocked:
                return reason == MethodFallbackReason.HuntParty ? "Its method needs a bigger hunting party before it does anything."
                    : tool.Length > 0 ? $"Its method needs {tool} before it does anything." : "Its method is blocked until something it needs exists.";
            default:
                return "No measurable gain at today's flows: there is no work for it to speed up yet.";
        }
    }

    private void AddLearnedRow()
    {
        var root = UiKit.Row(UiTokens.Sp2);
        root.MouseFilter = Control.MouseFilterEnum.Pass;
        var row = new LearnedRow
        {
            Root = root,
            Name = FixedWidthText("", "BodyStrong", 170),
            Learned = FixedWidthText("", "CaptionTertiary", 108),
            Status = FixedWidthText("", "Caption", 128),
            Last = FixedWidthText("", "Num", 128, right: true),
            Total = FixedWidthText("", "Num", 128, right: true),
            Detail = UiKit.Text("", "Caption", wrap: true),
        };
        root.AddChild(row.Name);
        root.AddChild(row.Learned);
        root.AddChild(row.Status);
        root.AddChild(row.Last);
        root.AddChild(row.Total);
        root.AddChild(row.Detail);
        _learnedList!.AddChild(root);
        _learnedRows.Add(row);
    }

    private static FigureActivity? ActivityOfAction(string? action) => action switch
    {
        "forage" => FigureActivity.Forage, "hunt" => FigureActivity.Hunt, "fish" => FigureActivity.Fish, "farm" => FigureActivity.Farm,
        "chop" => FigureActivity.Chop, "gather" => FigureActivity.Gather, "craft" => FigureActivity.Craft, "herd" => FigureActivity.Herd,
        "preserve" => FigureActivity.Preserve, _ => null,
    };

    /// <summary>What the project's measurable effects look like in the settlements right now.</summary>
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
                    if (count > 0) { anyInUse = true; parts.Add($"{count} {name} standing"); }
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
                    else { anyInUse = true; parts.Add($"{workers} {word} at work"); }
                    break;
                }
            }
        }
        string detail = parts.Count > 0 ? string.Join(" · ", parts) : "Its effects are not tracked as numbers yet.";
        if (state == ProjectState.Dormant) return (ProjectUse.Blocked, "A tool or input it needs is missing, so the old method is used. " + detail);
        if (partlyBlocked) return (ProjectUse.PartlyBlocked, detail);
        if (anyInUse) return (ProjectUse.InUse, detail);
        if (anyUnused) return (ProjectUse.Unused, detail);
        return (ProjectUse.NotMeasured, detail);
    }

    private void RefreshResearchSpeed(MatchFrame frame, int civ)
    {
        UiKit.SetText(_researchSpeedLine!,
            $"{UiText.FormatInsight(frame.CivInsightPerYear[civ])} Insight a year. Adults and elders add Commons Insight (elders twice as much); roles and institutions add more.");
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
