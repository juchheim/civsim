using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The Stores tab: every good held, what changed last season, how long food lasts and where it went.</summary>
public partial class Game
{
    private sealed class StoreRow
    {
        public PanelContainer Root = null!;
        public Label Name = null!, Now = null!, Change = null!;
        public UiSparkline Spark = null!;
        public int Slot = -1;
    }

    private sealed class FlowRow
    {
        public Label Name = null!, Value = null!;
        public UiProgressBar Bar = null!;
    }

    private static readonly string[] StoreSectionNames = { "FOOD", "MATERIALS", "CLOTHING", "TOOLS", "ADVANCED GOODS" };

    // Store table columns: the name takes the rest of the row.
    private const int StoreNowWidth = 90, StoreChangeWidth = 116, StoreTrendWidth = 96;

    private readonly StoreRow[] _foodRows = new StoreRow[LivingEconomyModel.FoodLedgerLineCount - LivingEconomyModel.FoodLedgerSummaryLines];
    private readonly List<StoreRow> _goodRows = new();
    private readonly Control?[] _storeSectionHeads = new Control?[StoreSectionNames.Length];
    private readonly FlowRow[] _flowRows = new FlowRow[6];
    private StoreRow? _foodTotalRow;
    private Label? _foodStoredValue, _foodStoredDetail, _foodLastsValue, _foodNeedValue;
    private Label? _foodNeedLine, _foodLandLine, _fuelLine, _flowEmpty;
    private readonly float[] _storeSeries = new float[MatchFrame.HistoryLength + 1];
    private static readonly int WoodStoreSlot = StoreSlotById("G-WOOD");

    private static int StoreSlotById(string id)
    {
        for (int slot = 0; slot < LivingEconomyModel.StoreSlotCount; slot++)
            if (LivingEconomyModel.StoreSlotId(slot) == id) return slot;
        return -1;
    }

    private Control BuildPeopleStoresPane()
    {
        PeopleSection held = NewPeopleSection("What you hold", "StoreTable");

        // Food at a glance: stored, how long it lasts, this season's need.
        var stats = UiKit.Row(UiTokens.Sp5);
        _foodStoredValue = AddStoreStat(stats, "FOOD IN STORE", out _foodStoredDetail);
        _foodLastsValue = AddStoreStat(stats, "LASTS", out _);
        _foodNeedValue = AddStoreStat(stats, "NEEDED THIS SEASON", out _);
        held.Body.AddChild(stats);
        held.Body.AddChild(UiKit.Spacer(height: UiTokens.Sp1));

        var food = UiKit.Column(2);
        food.AddChild(StoreHeader("FOOD", "In store", "Gathered (R)", "Rations gathered last season, by kind of food."));
        for (int g = 0; g < _foodRows.Length; g++)
        {
            string id = LivingEconomyModel.StoreSlotId(LivingEconomyModel.FoodGoodSlot(g));
            _foodRows[g] = BuildStoreRow(food, UiText.GoodName(id), false);
            _foodRows[g].Slot = LivingEconomyModel.FoodGoodSlot(g);
        }
        food.AddChild(UiKit.Divider());
        _foodTotalRow = BuildStoreRow(food, "All food (rations)", true);
        held.Body.AddChild(food);

        for (int category = 1; category < StoreSectionNames.Length; category++)
        {
            var section = UiKit.Column(2);
            section.AddChild(UiKit.Spacer(height: UiTokens.Sp2));
            section.AddChild(StoreHeader(StoreSectionNames[category], "In store", "Last season", "How much the stock rose or fell over the last full season."));
            held.Body.AddChild(section);
            _storeSectionHeads[category] = section;
            for (int slot = 0; slot < LivingEconomyModel.StoreSlotCount; slot++)
            {
                if ((int)LivingEconomyModel.StoreSlotCategory(slot) != category) continue;
                StoreRow row = BuildStoreRow(section, UiText.GoodName(LivingEconomyModel.StoreSlotId(slot)), false);
                row.Slot = slot;
                row.Root.Visible = false;
                _goodRows.Add(row);
            }
        }

        PeopleSection flows = NewPeopleSection("Where food went last season", "FoodFlows");
        _flowEmpty = UiKit.Text("Flows appear after the first season ends.", "Caption", wrap: true);
        flows.Body.AddChild(_flowEmpty);
        string[] flowNames = { "Held at the start", "+ Gathered", "− Eaten", "− Spoiled", "− Lost: stores full", "= Held at the end" };
        var flowList = UiKit.Column(UiTokens.Sp1);
        for (int i = 0; i < _flowRows.Length; i++)
        {
            var row = UiKit.Row(UiTokens.Sp3);
            row.CustomMinimumSize = new Vector2(0, 26);
            var view = new FlowRow
            {
                Name = UiKit.Cell(flowNames[i], i is 0 or 5 ? "BodyStrongSecondary" : "BodySecondary", 150, wrap: true),
                Bar = new UiProgressBar(6),
                Value = UiKit.Cell("", "NumBody", 64, right: true),
            };
            view.Name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(view.Name);
            row.AddChild(view.Bar);
            row.AddChild(view.Value);
            flowList.AddChild(row);
            _flowRows[i] = view;
        }
        flows.Body.AddChild(flowList);
        flows.Body.AddChild(UiKit.Spacer(height: UiTokens.Sp1));
        _foodNeedLine = UiKit.Text("", "Caption", wrap: true);
        _foodLandLine = UiKit.Text("", "Caption", wrap: true);
        flows.Body.AddChild(_foodNeedLine);
        flows.Body.AddChild(_foodLandLine);

        PeopleSection fuel = NewPeopleSection("Fuel", "Fuel");
        _fuelLine = UiKit.Text("", "Body", wrap: true);
        fuel.Body.AddChild(_fuelLine);

        var body = UiKit.Row(UiTokens.Sp3);
        body.AddChild(PeopleColumn(0.58f, held));
        body.AddChild(PeopleColumn(0.42f, flows, fuel));
        return PeopleScroll(body);
    }

    /// <summary>A headline number with an all-caps label above and an optional quiet line below; returns the number label.</summary>
    private static Label AddStoreStat(HBoxContainer parent, string title, out Label detail)
    {
        var column = UiKit.Column(2);
        column.AddChild(UiKit.Text(title, "EyebrowTertiary"));
        Label value = UiKit.Text("—", "Heading");
        column.AddChild(value);
        detail = UiKit.Text("", "CaptionTertiary");
        column.AddChild(detail);
        parent.AddChild(column);
        return value;
    }

    /// <summary>A store group's header: the group name over the name column, then the column titles, aligned with the rows.</summary>
    private static Control StoreHeader(string group, string now, string change, string changeTip)
    {
        var head = UiKit.Panel("TableHead", null, UiKit.MouseFilter.Ignore);
        var row = UiKit.Row(UiTokens.Sp3);
        var name = UiKit.Text(group, "Eyebrow");
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(name);
        row.AddChild(UiKit.Cell(now.ToUpperInvariant(), "EyebrowTertiary", StoreNowWidth, right: true));
        Label changeHead = UiKit.Cell(change.ToUpperInvariant(), "EyebrowTertiary", StoreChangeWidth, right: true);
        changeHead.MouseFilter = Control.MouseFilterEnum.Stop;
        changeHead.TooltipText = changeTip;
        row.AddChild(changeHead);
        row.AddChild(UiKit.Cell("TREND", "EyebrowTertiary", StoreTrendWidth, right: true));
        head.AddChild(row);
        return head;
    }

    private static StoreRow BuildStoreRow(VBoxContainer parent, string name, bool strong)
    {
        var root = UiKit.Panel("TableRow", null, UiKit.MouseFilter.Ignore);
        var row = UiKit.Row(UiTokens.Sp3);
        var view = new StoreRow
        {
            Root = root,
            Name = UiKit.Text(name, strong ? "BodyStrong" : "Body", wrap: true),
            Now = UiKit.Cell("", strong ? "NumStrong" : "NumBody", StoreNowWidth, right: true),
            Change = UiKit.Cell("", strong ? "NumStrong" : "NumBody", StoreChangeWidth, right: true),
            Spark = new UiSparkline(StoreTrendWidth, 20),
        };
        view.Now.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        view.Change.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(view.Name);
        row.AddChild(view.Now);
        row.AddChild(view.Change);
        row.AddChild(view.Spark);
        root.AddChild(row);
        parent.AddChild(root);
        return view;
    }

    private long ScopeStock(MatchFrame frame, int slot)
    {
        long total = 0;
        int slots = MatchFrame.StoreSlots;
        for (int w = 0; w < frame.SettlementCount; w++)
            if (PeopleRowHasEconomy(frame, w)) total += frame.SettlementStores[w * slots + slot];
        return total;
    }

    /// <summary>Fills a store row from a history series (hundredths) and the live value; returns the last season's change in units.</summary>
    private double FillSpark(MatchFrame frame, StoreRow row, int series, double live, Color tone)
    {
        int n = ScopeSeries(frame, series, _storeSeries);
        Span<float> values = stackalloc float[n + 1];
        for (int i = 0; i < n; i++) values[i] = _storeSeries[i] / 100f;
        values[n] = (float)live;
        row.Spark.SetSeries(values, tone);
        return n >= 2 ? (_storeSeries[n - 1] - _storeSeries[n - 2]) / 100.0 : double.NaN;
    }

    private static string FormatChange(double change)
        => double.IsNaN(change) ? "—" : Math.Abs(change) < 0.05 ? "±0" : (change > 0 ? "+" : "−") + FormatUnits(Math.Abs(change));

    private void RefreshPeopleStores(MatchFrame frame)
    {
        PeopleScopeSummary sum = _peopleSummary;
        long[] ledger = sum.Ledger;
        bool none = sum.Rows == 0;

        double stores = Units(sum.Stores), need = Units(sum.Need), capacity = Units(sum.Capacity);
        if (none)
        {
            UiKit.SetText(_foodStoredValue!, "—");
            UiKit.SetText(_foodStoredDetail!, "No settlement to report on yet.");
            UiKit.SetText(_foodLastsValue!, "—");
            SetTone(_foodLastsValue!, UiTokens.TextPrimary);
            UiKit.SetText(_foodNeedValue!, "—");
        }
        else
        {
            double runway = need > 0 ? stores / need : 8;
            UiKit.SetText(_foodStoredValue!, $"{FormatUnits(stores)} R");
            UiKit.SetText(_foodStoredDetail!, $"of {FormatUnits(capacity)} R storage");
            UiKit.SetText(_foodLastsValue!, need > 0 ? FormatSeasons(runway) : "—");
            SetTone(_foodLastsValue!, need > 0 ? ToneForRunway(runway) : UiTokens.TextPrimary);
            UiKit.SetText(_foodNeedValue!, $"{FormatUnits(need)} R");
        }

        // Food goods: stock in units, gathered last season in rations.
        int foodShown = 0;
        for (int g = 0; g < _foodRows.Length; g++)
        {
            StoreRow row = _foodRows[g];
            double stock = Units(ScopeStock(frame, row.Slot));
            double change = FillSpark(frame, row, HistorySeries.StockBase + row.Slot, stock, UiTokens.Accent);
            UiKit.SetText(row.Now, FormatUnits(stock));
            double gathered = sum.LedgerValid ? Units(ledger[LivingEconomyModel.FoodLedgerSummaryLines + g]) : double.NaN;
            UiKit.SetText(row.Change, sum.LedgerValid ? FormatChange(gathered) : "—");
            SetTone(row.Change, !double.IsNaN(gathered) && gathered < -0.05 ? UiTokens.Warning : UiTokens.TextPrimary);
            _ = change;
            bool visible = stock > 0.005 || sum.LedgerValid && Math.Abs(gathered) > 0.005 || none;
            UiKit.SetVisible(row.Root, visible);
            if (visible) UiKit.SetVariation(row.Root, (foodShown++ & 1) == 0 ? "TableRowAlt" : "TableRow");
        }
        StoreRow total = _foodTotalRow!;
        double totalChange = FillSpark(frame, total, HistorySeries.FoodStores, stores, UiTokens.Success);
        UiKit.SetText(total.Now, FormatUnits(stores));
        UiKit.SetText(total.Change, sum.LedgerValid ? FormatChange(Units(ledger[EconField.LedgerGathered])) : "—");
        _ = totalChange;

        // The other goods: stock now and last season's change.
        int[] visibleBySection = new int[StoreSectionNames.Length];
        foreach (StoreRow row in _goodRows)
        {
            double stock = Units(ScopeStock(frame, row.Slot));
            int n = ScopeSeries(frame, HistorySeries.StockBase + row.Slot, _storeSeries);
            bool everHeld = stock > 0.005;
            for (int i = 0; i < n && !everHeld; i++) everHeld = _storeSeries[i] > 0.5f;
            UiKit.SetVisible(row.Root, everHeld);
            if (!everHeld) continue;
            int shownInSection = visibleBySection[(int)LivingEconomyModel.StoreSlotCategory(row.Slot)]++;
            UiKit.SetVariation(row.Root, (shownInSection & 1) == 0 ? "TableRowAlt" : "TableRow");
            double change = FillSpark(frame, row, HistorySeries.StockBase + row.Slot, stock, UiTokens.Info);
            UiKit.SetText(row.Now, FormatUnits(stock));
            UiKit.SetText(row.Change, FormatChange(change));
            SetTone(row.Change, !double.IsNaN(change) && change < -0.05 ? UiTokens.Warning : UiTokens.TextPrimary);
        }
        for (int category = 1; category < StoreSectionNames.Length; category++)
            if (_storeSectionHeads[category] is { } head) UiKit.SetVisible(head, visibleBySection[category] > 0);

        RefreshFoodFlows(frame, sum);
    }

    private void RefreshFoodFlows(MatchFrame frame, PeopleScopeSummary sum)
    {
        long[] ledger = sum.Ledger;
        bool valid = sum.LedgerValid;
        UiKit.SetVisible(_flowEmpty!, !valid);
        int[] lines =
        {
            EconField.LedgerStart, EconField.LedgerGathered, EconField.LedgerEaten,
            EconField.LedgerSpoiled, EconField.LedgerLostToCap, EconField.LedgerEnd,
        };
        Color[] tones = { UiTokens.Info, UiTokens.Success, UiTokens.Accent, UiTokens.Warning, UiTokens.Danger, UiTokens.Info };
        double largest = 0.01;
        for (int i = 0; i < lines.Length; i++) largest = Math.Max(largest, Units(ledger[lines[i]]));
        for (int i = 0; i < _flowRows.Length; i++)
        {
            FlowRow row = _flowRows[i];
            double value = valid ? Units(ledger[lines[i]]) : 0;
            row.Bar.SetValue((float)(value / largest), false);
            row.Bar.Fill = value > 0.05 || i is 0 or 1 or 5 ? tones[i] : UiTokens.SurfaceSunken;
            UiKit.SetText(row.Value, valid ? FormatUnits(value) : "");
            UiKit.SetVisible(row.Name.GetParent<Control>(), valid);
        }

        if (sum.Rows == 0) { UiKit.SetText(_foodNeedLine!, ""); UiKit.SetText(_foodLandLine!, ""); UiKit.SetText(_fuelLine!, ""); return; }
        double need = Units(sum.Need), stores = Units(sum.Stores);
        string spoilage = "";
        if (valid)
        {
            double gathered = Units(ledger[EconField.LedgerGathered]), spoiled = Units(ledger[EconField.LedgerSpoiled]);
            if (gathered > 0.5 && spoiled > 0.05) spoilage = $" Spoilage took {spoiled * 100 / gathered:0}% of what was gathered.";
        }
        UiKit.SetText(_foodNeedLine!, $"This season's need is {FormatUnits(need)} R; {FormatUnits(stores)} R is in store.{spoilage}");
        int foodCap = DiagnosisEngine.Whole(sum.FoodCap);
        UiKit.SetText(_foodLandLine!, foodCap > 0
            ? $"The land can feed about {foodCap:N0} people; you have {sum.Population:N0}. Research that raises yields raises that number."
            : "");

        double fuel = Units(sum.Fuel);
        double wood = WoodStoreSlot >= 0 ? Units(ScopeStock(frame, WoodStoreSlot)) : 0;
        if (fuel <= 0.005)
        {
            UiKit.SetText(_fuelLine!, $"No hearth burns wood yet. {FormatUnits(wood)} wood in store.");
            SetTone(_fuelLine!, UiTokens.TextPrimary);
        }
        else
        {
            double seasons = wood / fuel;
            UiKit.SetText(_fuelLine!, $"Fires burn {FormatUnits(fuel)} wood this season; {FormatUnits(wood)} is in store, enough for {FormatSeasons(seasons)}.");
            SetTone(_fuelLine!, seasons < 1 ? UiTokens.Warning : UiTokens.TextPrimary);
        }
    }
}
