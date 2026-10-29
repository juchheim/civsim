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
        public Control Root = null!;
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

    private readonly StoreRow[] _foodRows = new StoreRow[LivingEconomyModel.FoodLedgerLineCount - LivingEconomyModel.FoodLedgerSummaryLines];
    private readonly List<StoreRow> _goodRows = new();
    private readonly Control?[] _storeSectionHeads = new Control?[StoreSectionNames.Length];
    private readonly FlowRow[] _flowRows = new FlowRow[6];
    private StoreRow? _foodTotalRow;
    private Label? _foodSummary, _foodNeedLine, _foodLandLine, _fuelLine, _flowEmpty;
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
        var body = UiKit.Row(UiTokens.Sp3);

        PanelContainer left = UiKit.Panel("WellPanel", "StoreTable");
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.SizeFlagsStretchRatio = 0.618f;
        var leftContent = UiKit.Column(UiTokens.Sp1);
        leftContent.AddChild(UiKit.Text("FOOD", "Eyebrow"));
        _foodSummary = UiKit.Text("", "BodyStrong", wrap: true);
        leftContent.AddChild(_foodSummary);
        AddStoreHeader(leftContent, "In store", "Gathered (R)");
        for (int g = 0; g < _foodRows.Length; g++)
        {
            string id = LivingEconomyModel.StoreSlotId(LivingEconomyModel.FoodGoodSlot(g));
            _foodRows[g] = BuildStoreRow(leftContent, UiText.GoodName(id), false);
            _foodRows[g].Slot = LivingEconomyModel.FoodGoodSlot(g);
        }
        _foodTotalRow = BuildStoreRow(leftContent, "All food (rations)", true);

        for (int category = 1; category < StoreSectionNames.Length; category++)
        {
            var section = UiKit.Column(UiTokens.Sp1);
            section.AddChild(UiKit.Divider());
            section.AddChild(UiKit.Text(StoreSectionNames[category], "Eyebrow"));
            AddStoreHeader(section, "In store", "Last season");
            leftContent.AddChild(section);
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
        left.AddChild(leftContent);
        body.AddChild(left);

        PanelContainer right = UiKit.Panel("WellPanel", "FoodFlows");
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsStretchRatio = 0.382f;
        var rightContent = UiKit.Column(UiTokens.Sp2);
        rightContent.AddChild(UiKit.Text("WHERE FOOD WENT LAST SEASON", "Eyebrow"));
        _flowEmpty = UiKit.Text("Flows appear after the first season ends.", "CaptionTertiary", wrap: true);
        rightContent.AddChild(_flowEmpty);
        string[] flowNames = { "Held at the start", "+ Gathered", "− Eaten", "− Spoiled", "− Lost: stores full", "= Held at the end" };
        for (int i = 0; i < _flowRows.Length; i++)
        {
            var row = UiKit.Row(UiTokens.Sp2);
            var view = new FlowRow
            {
                Name = UiKit.Text(flowNames[i], "Caption", minWidth: 118),
                Bar = new UiProgressBar(6) { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill },
                Value = UiKit.Text("", "Num", minWidth: 52, right: true),
            };
            row.AddChild(view.Name);
            row.AddChild(view.Bar);
            row.AddChild(view.Value);
            rightContent.AddChild(row);
            _flowRows[i] = view;
        }
        rightContent.AddChild(UiKit.Divider());
        _foodNeedLine = UiKit.Text("", "Caption", wrap: true);
        _foodLandLine = UiKit.Text("", "Caption", wrap: true);
        _fuelLine = UiKit.Text("", "Caption", wrap: true);
        rightContent.AddChild(_foodNeedLine);
        rightContent.AddChild(_foodLandLine);
        rightContent.AddChild(UiKit.Divider());
        rightContent.AddChild(UiKit.Text("FUEL", "EyebrowTertiary"));
        rightContent.AddChild(_fuelLine);
        right.AddChild(rightContent);
        body.AddChild(right);
        return UiKit.Scroll(body);
    }

    private static void AddStoreHeader(VBoxContainer parent, string now, string change)
    {
        var row = UiKit.Row(UiTokens.Sp2);
        row.AddChild(UiKit.Text("", "EyebrowTertiary", minWidth: 150, ellipsis: true));
        row.AddChild(UiKit.Text(now.ToUpperInvariant(), "EyebrowTertiary", minWidth: 70, right: true));
        row.AddChild(UiKit.Text(change.ToUpperInvariant(), "EyebrowTertiary", minWidth: 92, right: true));
        row.AddChild(UiKit.Text("TREND", "EyebrowTertiary", minWidth: 84));
        parent.AddChild(row);
    }

    private static StoreRow BuildStoreRow(VBoxContainer parent, string name, bool strong)
    {
        var row = UiKit.Row(UiTokens.Sp2);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        var view = new StoreRow
        {
            Root = row,
            Name = UiKit.Text(name, strong ? "BodyStrong" : "Caption", minWidth: 150, ellipsis: true),
            Now = UiKit.Text("", strong ? "NumStrong" : "Num", minWidth: 70, right: true),
            Change = UiKit.Text("", "Num", minWidth: 92, right: true),
            Spark = new UiSparkline(84, 20),
        };
        row.AddChild(view.Name);
        row.AddChild(view.Now);
        row.AddChild(view.Change);
        row.AddChild(view.Spark);
        parent.AddChild(row);
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
        if (none) UiKit.SetText(_foodSummary!, "No settlement to report on yet.");
        else
        {
            double runway = need > 0 ? stores / need : 8;
            UiKit.SetText(_foodSummary!,
                $"{FormatUnits(stores)} R in store of {FormatUnits(capacity)} R storage · covers {FormatSeasons(runway)} of this season's need ({FormatUnits(need)} R)");
            SetTone(_foodSummary!, ToneForRunway(runway));
        }

        // Food goods: stock in units, gathered last season in rations.
        for (int g = 0; g < _foodRows.Length; g++)
        {
            StoreRow row = _foodRows[g];
            double stock = Units(ScopeStock(frame, row.Slot));
            double change = FillSpark(frame, row, HistorySeries.StockBase + row.Slot, stock, UiTokens.Accent);
            UiKit.SetText(row.Now, FormatUnits(stock));
            double gathered = sum.LedgerValid ? Units(ledger[LivingEconomyModel.FoodLedgerSummaryLines + g]) : double.NaN;
            UiKit.SetText(row.Change, sum.LedgerValid ? FormatChange(gathered) : "—");
            SetTone(row.Change, !double.IsNaN(gathered) && gathered < -0.05 ? UiTokens.Warning : UiTokens.TextSecondary);
            _ = change;
            UiKit.SetVisible(row.Root, stock > 0.005 || sum.LedgerValid && Math.Abs(gathered) > 0.005 || none);
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
            visibleBySection[(int)LivingEconomyModel.StoreSlotCategory(row.Slot)]++;
            double change = FillSpark(frame, row, HistorySeries.StockBase + row.Slot, stock, UiTokens.Info);
            UiKit.SetText(row.Now, FormatUnits(stock));
            UiKit.SetText(row.Change, FormatChange(change));
            SetTone(row.Change, !double.IsNaN(change) && change < -0.05 ? UiTokens.Warning : UiTokens.TextSecondary);
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
        if (fuel <= 0.005) UiKit.SetText(_fuelLine!, $"No hearth burns wood yet. {FormatUnits(wood)} wood in store.");
        else
        {
            double seasons = wood / fuel;
            UiKit.SetText(_fuelLine!, $"Fires burn {FormatUnits(fuel)} wood this season; {FormatUnits(wood)} is in store, enough for {FormatSeasons(seasons)}.");
            SetTone(_fuelLine!, seasons < 1 ? UiTokens.Warning : UiTokens.TextSecondary);
        }
    }
}
