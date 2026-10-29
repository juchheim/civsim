using System;
using System.Collections.Generic;
using System.Globalization;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The Overview tab: what is slowing the people down, what limits their growth, how each settlement is doing.</summary>
public partial class Game
{
    private sealed class OverviewCard
    {
        public Label Title = null!, Value = null!, Detail = null!;
    }

    private sealed class DiagRow
    {
        public Control Root = null!;
        public UiIcon Icon = null!;
        public Label Headline = null!, Detail = null!, Where = null!;
        public readonly Button[] Chips = new Button[2];
        public readonly int[] ChipProject = { -1, -1 };
        public Control ChipRow = null!;
    }

    private sealed class GrowthBarRow
    {
        public Label Name = null!, Value = null!;
        public UiProgressBar Bar = null!;
    }

    private readonly OverviewCard[] _overviewCards = new OverviewCard[4];
    private readonly DiagRow[] _diagRows = new DiagRow[8];
    private readonly GrowthBarRow[] _growthRows = new GrowthBarRow[3];
    private Label? _diagEmpty, _growthNote;
    private Label? _wellFoodShort, _wellWinterRisk, _wellHungry, _wellCold, _wellIll, _wellWounded, _wellStrained;
    private Control? _wellWinterChip, _settlementsPanel;
    private readonly List<Label[]> _settlementRows = new();
    private readonly List<UiButton> _settlementShow = new();
    private readonly List<int> _settlementShowTarget = new();
    private GridContainer? _settlementGrid;
    private readonly float[] _seriesScratch = new float[MatchFrame.HistoryLength + 1];

    private Control BuildPeopleOverviewPane()
    {
        var content = UiKit.Column(UiTokens.Sp3);

        var cards = UiKit.Row(UiTokens.Sp2);
        string[] titles = { "YOUR PEOPLE", "FOOD", "GROWTH LIMIT", "RESEARCH SPEED" };
        for (int i = 0; i < titles.Length; i++)
        {
            var card = UiKit.Panel("InnerCard");
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var stack = UiKit.Column(UiTokens.Sp1);
            var view = new OverviewCard
            {
                Title = UiKit.Text(titles[i], "EyebrowTertiary"),
                Value = UiKit.Text("—", "NumTitle"),
                Detail = UiKit.Text("", "CaptionTertiary", wrap: true),
            };
            stack.AddChild(view.Title);
            stack.AddChild(view.Value);
            stack.AddChild(view.Detail);
            card.AddChild(stack);
            cards.AddChild(card);
            _overviewCards[i] = view;
        }
        content.AddChild(cards);

        var body = UiKit.Row(UiTokens.Sp3);
        PanelContainer slowing = UiKit.Panel("WellPanel", "WhatSlowsYouDown");
        slowing.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        slowing.SizeFlagsStretchRatio = 0.618f;
        var slowingContent = UiKit.Column(UiTokens.Sp2);
        slowingContent.AddChild(UiKit.Text("WHAT'S SLOWING YOU DOWN", "Eyebrow"));
        _diagEmpty = UiKit.Text("", "Caption", wrap: true);
        slowingContent.AddChild(_diagEmpty);
        for (int i = 0; i < _diagRows.Length; i++)
        {
            _diagRows[i] = BuildDiagRow();
            slowingContent.AddChild(_diagRows[i].Root);
        }
        slowing.AddChild(slowingContent);
        body.AddChild(slowing);

        PanelContainer limits = UiKit.Panel("WellPanel", "GrowthLimits");
        limits.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        limits.SizeFlagsStretchRatio = 0.382f;
        var limitContent = UiKit.Column(UiTokens.Sp2);
        limitContent.AddChild(UiKit.Text("WHAT LIMITS GROWTH", "Eyebrow"));
        string[] limitNames = { "Food", "Housing", "Group size" };
        for (int i = 0; i < _growthRows.Length; i++)
        {
            var row = UiKit.Row(UiTokens.Sp2);
            var view = new GrowthBarRow
            {
                Name = UiKit.Text(limitNames[i], "Caption", minWidth: 78),
                Bar = new UiProgressBar(6) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill },
                Value = UiKit.Text("", "Num", minWidth: 70, right: true),
            };
            row.AddChild(view.Name);
            row.AddChild(view.Bar);
            row.AddChild(view.Value);
            limitContent.AddChild(row);
            _growthRows[i] = view;
        }
        _growthNote = UiKit.Text("", "CaptionTertiary", wrap: true);
        limitContent.AddChild(_growthNote);
        limitContent.AddChild(UiKit.Divider());
        limitContent.AddChild(UiKit.Text("NEEDS & WELLBEING", "EyebrowTertiary"));
        var needs = new HFlowContainer();
        needs.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        needs.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _wellFoodShort = AddPeopleCountChip(needs, "Food short");
        _wellWinterRisk = AddPeopleCountChip(needs, "Winter risk");
        _wellWinterChip = _wellWinterRisk.GetParent<Control>()?.GetParent<Control>();
        _wellHungry = AddPeopleCountChip(needs, "Hungry");
        _wellCold = AddPeopleCountChip(needs, "Cold");
        _wellIll = AddPeopleCountChip(needs, "Ill");
        _wellWounded = AddPeopleCountChip(needs, "Wounded");
        _wellStrained = AddPeopleCountChip(needs, "Strained settlements");
        limitContent.AddChild(needs);
        limits.AddChild(limitContent);
        body.AddChild(limits);
        content.AddChild(body);

        _settlementsPanel = UiKit.Panel("WellPanel", "SettlementsTable");
        var tableContent = UiKit.Column(UiTokens.Sp2);
        tableContent.AddChild(UiKit.Text("YOUR SETTLEMENTS", "Eyebrow"));
        _settlementGrid = new GridContainer { Columns = 8 };
        _settlementGrid.AddThemeConstantOverride("h_separation", UiTokens.Sp3);
        _settlementGrid.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        string[] heads = { "SETTLEMENT", "PEOPLE", "FED", "STORES", "LASTS", "LIMIT", "COHESION", "" };
        foreach (string head in heads) _settlementGrid.AddChild(UiKit.Text(head, "EyebrowTertiary"));
        tableContent.AddChild(_settlementGrid);
        _settlementsPanel.AddChild(tableContent);
        content.AddChild(_settlementsPanel);
        return UiKit.Scroll(content);
    }

    private DiagRow BuildDiagRow()
    {
        var row = new DiagRow();
        var root = UiKit.Row(UiTokens.Sp2);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.Icon = new UiIcon(UiGlyph.Info, 18, UiTokens.Info) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        root.AddChild(row.Icon);
        var text = UiKit.Column(UiTokens.Sp1);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.Headline = UiKit.Text("", "BodyStrong", wrap: true);
        row.Detail = UiKit.Text("", "Caption", wrap: true);
        row.Where = UiKit.Text("", "CaptionTertiary", wrap: true);
        text.AddChild(row.Headline);
        text.AddChild(row.Detail);
        text.AddChild(row.Where);
        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        chips.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        chips.AddChild(UiKit.Text("Research that could help", "CaptionTertiary"));
        for (int i = 0; i < row.Chips.Length; i++)
        {
            int slot = i;
            row.Chips[i] = UiKit.InteractiveChip("", UiGlyph.None, () => OpenPeopleLever(row.ChipProject[slot]), "Open this project in the research web.");
            row.Chips[i].ToggleMode = false;
            chips.AddChild(row.Chips[i]);
        }
        row.ChipRow = chips;
        text.AddChild(chips);
        root.AddChild(text);
        row.Root = root;
        root.Visible = false;
        return row;
    }

    private void OpenPeopleLever(int project)
    {
        if (project < 0) return;
        ClosePeoplePanel();
        OpenResearchWeb(-1, project);
    }

    private void RefreshPeopleOverview(MatchFrame frame, int civ)
    {
        PeopleScopeSummary sum = _peopleSummary;
        PeopleCensus census = _peopleCensus;
        int population = Math.Max(census.Population, sum.Population);

        // PEOPLE
        int n = ScopeSeries(frame, HistorySeries.Population, _seriesScratch);
        OverviewCard people = _overviewCards[0];
        UiKit.SetText(people.Value, population.ToString("N0"));
        string growth = "";
        if (n >= 2)
        {
            int back = Math.Min(4, n - 1);
            int delta = population - (int)Math.Round(_seriesScratch[n - 1 - back]);
            growth = $"{(delta > 0 ? "+" : delta < 0 ? "−" : "±")}{Math.Abs(delta)} in {back} {(back == 1 ? "season" : "seasons")} · ";
        }
        UiKit.SetText(people.Detail, $"{growth}{census.Adults:N0} adults · {census.Elders:N0} elders · {census.Children:N0} children");

        // FOOD
        OverviewCard food = _overviewCards[1];
        double stores = Units(sum.Stores), need = Units(sum.Need), capacity = Units(sum.Capacity);
        if (sum.Rows == 0 || need <= 0)
        {
            UiKit.SetText(food.Value, "—");
            UiKit.SetText(food.Detail, sum.Rows == 0 ? "No settlement to report on yet." : "Nobody to feed.");
            SetTone(food.Value, UiTokens.TextPrimary);
        }
        else
        {
            double runway = stores / need;
            UiKit.SetText(food.Value, FormatSeasons(runway));
            SetTone(food.Value, ToneForRunway(runway));
            double fed = sum.Population > 0 ? sum.FoodSecurityWeighted / (double)sum.Population / 10.0 : 100;
            UiKit.SetText(food.Detail,
                $"{FormatUnits(stores)} of {FormatUnits(capacity)} R stored · need {FormatUnits(need)} R this season · {Math.Min(100, fed):0}% fed last season");
        }

        // GROWTH LIMIT
        OverviewCard limit = _overviewCards[2];
        int foodCap = DiagnosisEngine.Whole(sum.FoodCap), housingCap = DiagnosisEngine.Whole(sum.HousingCap);
        int cap = Math.Min(foodCap, housingCap);
        if (sum.Rows == 0 || cap <= 0)
        {
            UiKit.SetText(limit.Value, "—");
            UiKit.SetText(limit.Detail, "");
        }
        else
        {
            bool byFood = foodCap <= housingCap;
            double births = sum.Population > 0 ? sum.BirthWeighted / (double)sum.Population / 10.0 : 100;
            UiKit.SetText(limit.Value, $"{sum.Population:N0} / {cap:N0}");
            SetTone(limit.Value, sum.Population * 100 >= cap * 85 ? UiTokens.Warning : UiTokens.TextPrimary);
            UiKit.SetText(limit.Detail, $"{(byFood ? "The land" : "Housing")} sets the limit · births at {Math.Clamp(births, 0, 100):0}% of normal");
        }

        // RESEARCH SPEED
        OverviewCard research = _overviewCards[3];
        UiKit.SetText(research.Value, UiText.FormatInsight(frame.CivInsightPerYear[civ]) + " /yr");
        int slots = 0;
        for (int s = 0; s < 3; s++) if (frame.FocusSlotProject[s] >= 0) slots++;
        string trend = "";
        int count = frame.FocusHistoryCount;
        if (count >= 2)
        {
            int back = Math.Min(4, count - 1);
            int then = frame.FocusHistory[(count - 1 - back) * HistorySeries.CivSeries + HistorySeries.CivInsight];
            int now = frame.FocusHistory[(count - 1) * HistorySeries.CivSeries + HistorySeries.CivInsight];
            if (then > 0)
            {
                double change = (now - then) * 100.0 / then;
                trend = $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0}% in {back} {(back == 1 ? "season" : "seasons")} · ";
            }
        }
        UiKit.SetText(research.Detail, $"{trend}{slots} of 3 research slots active");

        RefreshDiagnosis(frame);
        RefreshGrowthBars(frame, foodCap, housingCap);
        RefreshWellbeing(frame);
        RefreshSettlementsTable(frame);
    }

    // ---- bottlenecks ---------------------------------------------------------------------------

    private void RefreshDiagnosis(MatchFrame frame)
    {
        _peopleDiagnosisCount = _peopleDiagnosis.Evaluate(frame, _peopleDiagnosisEntries);
        ulong shownCodes = 0;
        int shown = 0;
        Span<int> projects = stackalloc int[2];
        for (int i = 0; i < _peopleDiagnosisCount && shown < _diagRows.Length; i++)
        {
            DiagnosisEntry entry = _peopleDiagnosisEntries[i];
            bool civWide = entry.Settlement < 0;
            if (!civWide && _peopleScopeSelected >= 0 && entry.Settlement != _peopleScopeSelected) continue;
            ulong bit = 1UL << ((int)entry.Code & 63);
            if (!civWide && _peopleScopeSelected < 0 && (shownCodes & bit) != 0) continue;
            shownCodes |= bit;
            DiagRow row = _diagRows[shown++];
            (string headline, string detail) = DescribeDiagnosis(entry);
            UiKit.SetVisible(row.Root, true);
            UiKit.SetText(row.Headline, headline);
            UiKit.SetText(row.Detail, detail);
            (UiGlyph glyph, Color tone) = entry.Severity switch
            {
                DiagSeverity.Critical => (UiGlyph.Danger, UiTokens.Danger),
                DiagSeverity.Warning => (UiGlyph.Warning, UiTokens.Warning),
                _ => (UiGlyph.Info, UiTokens.Info),
            };
            row.Icon.Glyph = glyph;
            row.Icon.Tint = tone;
            UiKit.SetText(row.Where, DiagnosisWhere(frame, entry, i));
            int found = FindLeverProjects(frame, entry.Code, projects);
            for (int c = 0; c < row.Chips.Length; c++)
            {
                bool has = c < found;
                row.ChipProject[c] = has ? projects[c] : -1;
                UiKit.SetVisible(row.Chips[c], has);
                if (has) UiKit.SetText(row.Chips[c], LeverText(frame, projects[c]));
            }
            UiKit.SetVisible(row.ChipRow, found > 0);
        }
        for (int i = shown; i < _diagRows.Length; i++) UiKit.SetVisible(_diagRows[i].Root, false);
        UiKit.SetVisible(_diagEmpty!, shown == 0);
        if (shown == 0)
        {
            bool settled = _peopleSummary.Rows > 0 && frame.Season > 0;
            UiKit.SetText(_diagEmpty!, settled
                ? "Nothing is holding your people back right now. Growth is limited by the land and the housing you have."
                : "Numbers appear once the first season has ended.");
            SetTone(_diagEmpty!, settled ? UiTokens.Success : UiTokens.TextSecondary);
        }
    }

    /// <summary>Which settlements share this finding (only when the whole civilization is shown).</summary>
    private string DiagnosisWhere(MatchFrame frame, DiagnosisEntry first, int firstIndex)
    {
        if (first.Settlement < 0) return "";
        if (_peopleScopeSelected >= 0) return "";
        int others = 0;
        var names = new System.Text.StringBuilder();
        for (int i = firstIndex; i < _peopleDiagnosisCount; i++)
        {
            DiagnosisEntry e = _peopleDiagnosisEntries[i];
            if (e.Code != first.Code || e.Settlement < 0) continue;
            if (others < 3)
            {
                if (names.Length > 0) names.Append(", ");
                names.Append(e.Settlement + 1);
            }
            others++;
        }
        if (_peopleSummary.Rows <= 1) return "";
        string list = others > 3 ? names + $" and {others - 3} more" : names.ToString();
        return $"{(others == 1 ? "Settlement" : "Settlements")} {list}";
    }

    private static string R(int hundredths) => FormatHundredths(hundredths);

    private (string Headline, string Detail) DescribeDiagnosis(in DiagnosisEntry e)
    {
        switch (e.Code)
        {
            case DiagCode.Starving:
                return ("People are starving", $"Only {e.A / 10}% of food needs were met last season. {e.B:N0} people are affected.");
            case DiagCode.FoodShort:
                return ("Food is running short", $"{e.A / 10}% of food needs were met last season.");
            case DiagCode.WinterRisk:
                return ("Winter looks hard", $"Autumn's forecast covers only {e.A / 10}% of late-winter needs.");
            case DiagCode.StoresRunningOut:
                return ("Stores are running out", $"{R(e.A)} R in store against {R(e.B)} R needed this season. Stores fell {R(-e.C)} R last season.");
            case DiagCode.StoresFull:
                return ("Storage is full", (e.A > 0 ? $"{R(e.A)} R was lost to full stores last season. " : "") + $"Stores hold {R(e.B)} of {R(e.C)} R; a storage pit or granary raises the limit.");
            case DiagCode.Spoilage:
                return ("Food is spoiling", $"{R(e.A)} R, {e.B}% of what was gathered, spoiled last season. Drying, pits and granaries slow it.");
            case DiagCode.CrowdingFood:
                return ("The land is nearly full", $"{e.A:N0} people live here and the land feeds about {e.B:N0}. Births are at {e.C}% of normal.");
            case DiagCode.CrowdingHousing:
                return ("Housing is nearly full", $"{e.A:N0} people and room for about {e.B:N0}. Births are at {e.C}% of normal.");
            case DiagCode.SplitComing:
                return ("A new band will split off", $"{e.A:N0} people; bands tend to split near {e.B:N0}.");
            case DiagCode.FuelLow:
                return ("Firewood is low", $"{R(e.A)} wood in store; the fires burn about {R(e.B)} a season.");
            case DiagCode.FireOut:
                return ("The fire is out", "Cooked food is worth less and people feel the cold until a fire is lit again.");
            case DiagCode.ToolsMissing:
                return ($"{Sentence(PeopleActivityWord((FigureActivity)e.A, 2))} lack tools",
                    $"{e.B} of {e.C} are missing a tool for the best method they know, so they work the older way.");
            case DiagCode.HuntParty:
                return ("Too few hunters for the party method", $"{e.A} hunters are held back until the hunting party is big enough.");
            case DiagCode.MetalAccess:
                return (Sentence(PeopleFallbackWords((MethodFallbackReason)e.A)), $"{e.B} workers cannot use their metal tools.");
            case DiagCode.Cold:
                return ("People are cold", $"{e.A} of {e.B} were cold last season: shelter or clothing is short.");
            case DiagCode.Illness:
                return ("Sickness and injury", $"{e.A} ill and {e.B} wounded.");
            case DiagCode.CohesionLow:
                return (e.C >= (int)CohesionBand.Fraying ? "The settlement is fraying" : "The settlement is straining",
                    $"Distance from the capital costs {e.A} Cohesion; settlements beyond your Admin reach cost {e.B}.");
            case DiagCode.ResearchIdle:
                return ("Nothing is being researched", "Open the research web and pick a project.");
            case DiagCode.ScoutsWhileHungry:
                return ("Scouts are away while food is short", $"{e.A} scouts could be gathering food. Set scouting to Auto in the Work tab.");
            case DiagCode.MandateFull:
                return ("Your Mandate is full", "Spend it on a Decree before new Mandate goes to waste.");
            case DiagCode.BuildWaiting:
            {
                int kind = e.A & 0xFF, slot = (e.A >> 8) - 1;
                string structure = UiText.StructureName(LivingEconomyModel.StructureName(kind)).ToLowerInvariant();
                string good = slot < 0 ? "food" : UiText.GoodName(LivingEconomyModel.StoreSlotId(slot)).ToLowerInvariant();
                return ("Builders are waiting for materials", $"A {structure} needs {R(e.C)} {good}; {R(e.B)} is in store.");
            }
            case DiagCode.FoodLaborSqueeze:
                return ("Most hands are busy finding food", $"{e.B}% of the work went to food last season, so little was left for building and crafts.");
            case DiagCode.ToolLimitedWork:
                return ($"{ActionName(e.A)} work went unused", $"{R(e.B)} of {R(e.C)} work-seasons of {ActionName(e.A).ToLowerInvariant()} had no tool to use. More tools put it to work.");
            case DiagCode.LandLimited:
                return ($"{ActionName(e.A)} found less than hoped", $"It asked for {R(e.C)} and found {R(e.B)}: the land within reach is thin. Better methods, a new village or a Beacon help.");
            default:
                return ("Something needs attention", "");
        }
    }

    private static string ActionName(int kind) => kind switch
    {
        LivingEconomyModel.ActionForage => "Foraging", LivingEconomyModel.ActionHunt => "Hunting", LivingEconomyModel.ActionFish => "Fishing",
        LivingEconomyModel.ActionFarm => "Farming", _ => "Woodcutting",
    };

    private static string Sentence(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // ---- research that could help ----------------------------------------------------------------

    private static bool LeverMatches(DiagCode code, EffectDef e)
    {
        switch (code)
        {
            case DiagCode.StoresFull:
                return e.Kind == EffectKind.Build && e.Id is "storage_pit" or "granary";
            case DiagCode.Spoilage:
                return e.Kind == EffectKind.Build && e.Id is "drying_rack" or "storage_pit" or "granary" ||
                    e.Kind == EffectKind.Mod && (e.Stat?.Contains("spoilage", StringComparison.Ordinal) ?? false);
            case DiagCode.Starving or DiagCode.FoodShort or DiagCode.CrowdingFood or DiagCode.StoresRunningOut
                or DiagCode.FoodLaborSqueeze or DiagCode.LandLimited:
                return e.Kind == EffectKind.Method && e.Action is "forage" or "hunt" or "fish" or "farm" ||
                    e.Kind == EffectKind.Cap && e.Cap == CapKind.Food ||
                    e.Kind == EffectKind.Mod && (e.Stat is not null && (e.Stat.Contains("yield", StringComparison.Ordinal) ||
                        e.Stat.Contains("food", StringComparison.Ordinal) || e.Stat.Contains("forage", StringComparison.Ordinal)));
            case DiagCode.WinterRisk:
                return e.Kind == EffectKind.Build && e.Id is "drying_rack" or "storage_pit" or "granary" ||
                    e.Kind == EffectKind.Mod && (e.Stat?.Contains("spoilage", StringComparison.Ordinal) ?? false);
            case DiagCode.CrowdingHousing:
                return e.Kind == EffectKind.Cap && e.Cap == CapKind.Housing ||
                    e.Kind == EffectKind.Build && e.Id is "pit_house" or "wattle_daub_house" or "mudbrick_house" or "longhouse";
            case DiagCode.Cold:
                return e.Kind == EffectKind.Build && e.Id is "lean_to" or "hide_tent" or "pit_house" or "hearth" ||
                    e.Kind == EffectKind.Good && e.Id is "G-WRAP" or "G-TAILORED" or "G-GARMENT";
            case DiagCode.Illness:
                return e.Kind == EffectKind.Hazard;
            case DiagCode.CohesionLow:
                return e.Kind is EffectKind.Cohesion or EffectKind.Admin;
            case DiagCode.BuildWaiting:
                return e.Kind == EffectKind.Method && e.Action == "chop" ||
                    e.Kind == EffectKind.Mod && (e.Stat?.Contains("wood", StringComparison.Ordinal) ?? false);
            case DiagCode.FuelLow:
                return e.Kind == EffectKind.Mod && (e.Stat?.Contains("wood", StringComparison.Ordinal) ?? false);
            case DiagCode.ToolsMissing or DiagCode.ToolLimitedWork:
                return e.Kind == EffectKind.Good;
            default:
                return false;
        }
    }

    /// <summary>Up to two projects that could help: ones in a slot first, then available ones in catalog order.</summary>
    private int FindLeverProjects(MatchFrame frame, DiagCode code, Span<int> into)
    {
        if (_catalog is null) return 0;
        int found = 0;
        for (int pass = 0; pass < 2 && found < into.Length; pass++)
        {
            ProjectState wanted = pass == 0 ? ProjectState.Slotted : ProjectState.Available;
            for (int p = 0; p < _catalog.Count && p < frame.FocusProjectCount && found < into.Length; p++)
            {
                if ((ProjectState)frame.FocusProjectState[p] != wanted) continue;
                ProjectDef def = _catalog.Projects[p];
                if (!def.Numeric) continue;
                bool match = false;
                for (int i = 0; i < def.Effects.Length && !match; i++) match = LeverMatches(code, def.Effects[i]);
                if (match) into[found++] = p;
            }
        }
        return found;
    }

    private string LeverText(MatchFrame frame, int project)
    {
        string name = _catalog!.Projects[project].Name;
        return (ProjectState)frame.FocusProjectState[project] == ProjectState.Slotted ? $"{name} · researching" : name;
    }

    // ---- growth, wellbeing, settlements ----------------------------------------------------------

    private void RefreshGrowthBars(MatchFrame frame, int foodCap, int housingCap)
    {
        PeopleScopeSummary sum = _peopleSummary;
        int population = sum.Population;
        int groupCap = DiagnosisEngine.Whole(sum.GroupCap);
        int groupPop = population;
        if (_peopleScopeSelected < 0 && sum.Rows > 1)
        {
            // Bands split one by one, so for the whole civilization show the fullest one.
            double best = -1;
            for (int w = 0; w < frame.SettlementCount; w++)
            {
                if (!PeopleRowHasEconomy(frame, w)) continue;
                int g = DiagnosisEngine.Whole(frame.SettlementEcon[w * EconField.Stride + EconField.GroupCap]);
                if (g <= 0) continue;
                double ratio = frame.SettlementPopulation[w] / (double)g;
                if (ratio > best) { best = ratio; groupPop = frame.SettlementPopulation[w]; groupCap = g; }
            }
        }
        int[] caps = { foodCap, housingCap, groupCap };
        int[] pops = { population, population, groupPop };
        int binding = foodCap <= housingCap ? 0 : 1;
        for (int i = 0; i < _growthRows.Length; i++)
        {
            GrowthBarRow row = _growthRows[i];
            bool known = sum.Rows > 0 && caps[i] > 0;
            double ratio = known ? pops[i] / (double)caps[i] : 0;
            row.Bar.SetValue((float)Math.Min(1, ratio), false);
            row.Bar.Fill = ratio >= 1 ? UiTokens.Warning : i == binding ? UiTokens.Accent : UiTokens.Info;
            UiKit.SetText(row.Value, known ? $"{pops[i]:N0} / {caps[i]:N0}" : "—");
            UiKit.SetText(row.Name, i == 2 && _peopleScopeSelected < 0 && sum.Rows > 1 ? "Fullest band" : i == 2 ? "Group size" : i == 0 ? "Food" : "Housing");
        }
        if (sum.Rows == 0) { UiKit.SetText(_growthNote!, ""); return; }
        string name = binding == 0 ? "food" : "housing";
        double births = sum.Population > 0 ? sum.BirthWeighted / (double)sum.Population / 10.0 : 100;
        UiKit.SetText(_growthNote!, births < 99.5
            ? $"The tightest limit is {name} (highlighted). Births are slowed to {births:0}% of normal."
            : $"The tightest limit is {name} (highlighted). It is not slowing births yet.");
    }

    private void RefreshWellbeing(MatchFrame frame)
    {
        int foodShort = 0, winterRisk = 0, strained = 0;
        bool winterForecast = frame.SeasonOfYear is 2 or 3;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (!PeopleInScope(frame, i)) continue;
            if (frame.SettlementFood[i] >= (byte)FoodBand.Short) foodShort++;
            if (winterForecast && frame.SettlementWinterForecast[i] >= (byte)FoodBand.Short) winterRisk++;
            if (frame.SettlementCohesion[i] >= (byte)CohesionBand.Strained) strained++;
        }
        UiKit.SetText(_wellFoodShort!, foodShort.ToString("N0"));
        if (_wellWinterChip is not null) UiKit.SetVisible(_wellWinterChip, winterForecast);
        UiKit.SetText(_wellWinterRisk!, winterRisk.ToString("N0"));
        UiKit.SetText(_wellHungry!, _peopleCensus.Hungry.ToString("N0"));
        UiKit.SetText(_wellCold!, _peopleCensus.Cold.ToString("N0"));
        UiKit.SetText(_wellIll!, _peopleCensus.Ill.ToString("N0"));
        UiKit.SetText(_wellWounded!, _peopleCensus.Wounded.ToString("N0"));
        UiKit.SetText(_wellStrained!, strained.ToString("N0"));
    }

    private static readonly string[] CohesionWords = { "Strong", "Steady", "Strained", "Fraying" };

    private void RefreshSettlementsTable(MatchFrame frame)
    {
        int own = 0;
        for (int w = 0; w < frame.SettlementCount; w++) if (IsOwnPeopleSettlement(frame, w)) own++;
        UiKit.SetVisible(_settlementsPanel!, own >= 2);
        if (own < 2) return;
        int shown = 0;
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!IsOwnPeopleSettlement(frame, w)) continue;
            while (_settlementRows.Count <= shown) AddSettlementRow();
            Label[] cells = _settlementRows[shown];
            int econ = w * EconField.Stride;
            bool has = frame.SettlementEcon[econ + EconField.Valid] != 0;
            double need = Units(frame.SettlementEcon[econ + EconField.FoodNeed]);
            double stores = Units(frame.SettlementFoodStores[w]);
            double runway = need > 0 ? stores / need : 8;
            int pop = frame.SettlementPopulation[w];
            int foodCap = DiagnosisEngine.Whole(frame.SettlementEcon[econ + EconField.FoodCap]);
            int housingCap = DiagnosisEngine.Whole(frame.SettlementEcon[econ + EconField.HousingCap]);
            int cap = Math.Min(foodCap, housingCap);
            int fs = (int)frame.SettlementEcon[econ + EconField.FoodSecurityPermille];
            UiKit.SetText(cells[0], $"Settlement {frame.SettlementIndex[w] + 1}");
            UiKit.SetText(cells[1], pop.ToString("N0"));
            UiKit.SetText(cells[2], has ? $"{Math.Min(100, fs / 10)}%" : "—");
            SetTone(cells[2], fs < 700 ? UiTokens.Danger : fs < 900 ? UiTokens.Warning : UiTokens.TextSecondary);
            UiKit.SetText(cells[3], has ? $"{FormatUnits(stores)} R" : "—");
            UiKit.SetText(cells[4], has ? FormatSeasons(runway) : "—");
            SetTone(cells[4], has ? ToneForRunway(runway) : UiTokens.TextSecondary);
            UiKit.SetText(cells[5], has && cap > 0 ? $"{(foodCap <= housingCap ? "food" : "housing")} {pop}/{cap}" : "—");
            SetTone(cells[5], has && cap > 0 && pop * 100 >= cap * 85 ? UiTokens.Warning : UiTokens.TextSecondary);
            UiKit.SetText(cells[6], CohesionWords[Math.Min(3, (int)frame.SettlementCohesion[w])]);
            _settlementShowTarget[shown] = frame.SettlementIndex[w];
            for (int c = 0; c < cells.Length; c++) UiKit.SetVisible(cells[c], true);
            UiKit.SetVisible(_settlementShow[shown], true);
            shown++;
        }
        for (int i = shown; i < _settlementRows.Count; i++)
        {
            foreach (Label cell in _settlementRows[i]) UiKit.SetVisible(cell, false);
            UiKit.SetVisible(_settlementShow[i], false);
        }
    }

    private void AddSettlementRow()
    {
        var cells = new Label[7];
        for (int c = 0; c < cells.Length; c++)
        {
            cells[c] = UiKit.Text("", c == 0 ? "BodyStrong" : "Num", minWidth: c == 0 ? 110 : 64);
            _settlementGrid!.AddChild(cells[c]);
        }
        int index = _settlementRows.Count;
        _settlementShowTarget.Add(-1);
        var show = UiKit.Ghost("Show", () => SetPeopleScope(_settlementShowTarget[index]), "Look at just this settlement on every tab.");
        _settlementGrid!.AddChild(show);
        _settlementShow.Add(show);
        _settlementRows.Add(cells);
    }

    /// <summary>The scope's series (people or hundredths of a unit) with the newest value last; returns how many samples.</summary>
    private int ScopeSeries(MatchFrame frame, int series, float[] into)
    {
        int length = MatchFrame.HistoryLength, seriesCount = MatchFrame.HistorySeriesCount;
        int most = 0;
        for (int w = 0; w < frame.SettlementCount; w++)
            if (PeopleRowHasEconomy(frame, w)) most = Math.Max(most, frame.SettlementHistoryCount[w]);
        most = Math.Min(most, into.Length);
        Array.Clear(into, 0, most);
        for (int w = 0; w < frame.SettlementCount; w++)
        {
            if (!PeopleRowHasEconomy(frame, w)) continue;
            int count = Math.Min(frame.SettlementHistoryCount[w], most);
            int firstSample = frame.SettlementHistoryCount[w] - count;
            for (int k = 0; k < count; k++)
            {
                int sample = firstSample + k;
                into[most - count + k] += frame.SettlementHistory[(w * length + sample) * seriesCount + series];
            }
        }
        return most;
    }
}
