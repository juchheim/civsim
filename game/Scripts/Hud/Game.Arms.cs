using System;
using System.Collections.Generic;
using System.Text;
using CivSim.Game.Ui;
using CivSim.Sim.Politics;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// "Your forces": what the player's fighters carry right now, so an assault can be timed. Sits on the Diplomacy drawer's War page, right below the Assault order.
/// The base weapon (bow, spear-thrower, spears) comes from research alone and applies at once; metal kits, chariots and siege engines are goods a forge
/// has to make, and only count while a fighter is there to carry them (docs/MVP/13 §7.1, 22 §7.1, 26 §4).
/// </summary>
public partial class Game
{
    private sealed class ArmsKitRow
    {
        public PanelContainer Root = null!;
        public Label Name = null!, Strength = null!, Carried = null!, Made = null!;
    }

    private sealed class ArmsTile
    {
        public PanelContainer Root = null!;
        public Label Value = null!, Caption = null!;
    }

    private sealed class ArmsSettlementRow
    {
        public PanelContainer Root = null!;
        public Label Name = null!, Note = null!, Fighters = null!, Kits = null!, Strength = null!;
    }

    private Control? _armsBlock;
    private Label? _armsStrength, _armsBase, _armsAdvice, _armsLocked;
    private GridContainer? _armsTiles;
    private Control? _armsKitTable, _armsSettlementTable, _armsKitsHeadCell;
    private readonly ArmsKitRow[] _armsKitRows = new ArmsKitRow[5];
    private ArmsTile? _tileReady, _tileWounded, _tileWarriors, _tileSoldiers, _tileChariots, _tileEngines;
    private VBoxContainer? _armsSettlementList;
    private readonly List<ArmsSettlementRow> _armsSettlementRows = new();
    private const int ArmsCell = 64, ArmsTileWidth = 66;

    // Kit rows, strongest first (the order Fighters hands them out). Strength is the weapon multiplier of docs/MVP/22 §7.1 and 26 §4.
    private static readonly (string Good, string Strength, string Who)[] ArmsKitKinds =
    {
        ("G-ARMS4", "×3.0", "Soldiers only"),
        ("G-ARMS3", "×2.4", "Soldiers only"),
        ("G-ARMS2T", "×2.3", "Anyone, with a shield"),
        ("G-ARMS2A", "×2.1", "Anyone, with a shield"),
        ("G-ARMS1", "×1.8", "Anyone"),
    };

    private static readonly string[] ArmsBaseNames =
    {
        "Bare hands", "Clubs and thrown stones", "Wooden spears", "Stone-tipped spears",
        "Spear-thrower (ranged)", "Bow and arrow (ranged)", "Bow and arrow with Archery Warfare (ranged)",
    };

    private Control BuildArmsBlock()
    {
        var block = UiKit.Column(UiTokens.Sp2);
        var head = UiKit.Row(UiTokens.Sp2);
        head.AddChild(UiKit.Text("YOUR FORCES", "EyebrowTertiary"));
        head.AddChild(UiKit.Spacer(expand: true));
        _armsStrength = UiKit.Text("", "Num");
        _armsStrength.MouseFilter = Control.MouseFilterEnum.Stop;
        _armsStrength.TooltipText = "All your settlements' fighters together: weapon, training and kits. An assault takes at most 60% of the fighters near the town it marches from.";
        head.AddChild(_armsStrength);
        block.AddChild(UiKit.Margin(head, 0, UiTokens.Sp2, 0, 0));

        // Readiness at a glance: one small tile per number that matters when timing an assault.
        // A grid whose column count follows the pane's width and the number of tiles shown, so the rows stay even (6 tiles: one row of 6, or 3 + 3).
        var tiles = new GridContainer { Columns = 6 };
        tiles.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        tiles.AddThemeConstantOverride("v_separation", UiTokens.Sp2);
        _armsTiles = tiles;
        _tileReady = NewArmsTile(tiles, "ready", "Fighters fit to march now.");
        _tileWounded = NewArmsTile(tiles, "laid up", "Wounded or sick fighters. They rejoin once they heal.");
        _tileWarriors = NewArmsTile(tiles, "warriors", "Warrior band members: trained fighters who still work in peacetime.");
        _tileSoldiers = NewArmsTile(tiles, "soldiers", "Standing soldiers: they take iron and steel kits first and crew siege engines.");
        _tileChariots = NewArmsTile(tiles, "chariots", "");
        _tileEngines = NewArmsTile(tiles, "engines", "");
        block.AddChild(tiles);

        var baseRow = UiKit.Row(UiTokens.Sp2);
        baseRow.AddChild(UiKit.Text("Base weapon", "CaptionTertiary", minWidth: 84));
        _armsBase = UiKit.Text("", "Caption", wrap: true);
        _armsBase.MouseFilter = Control.MouseFilterEnum.Stop;
        baseRow.AddChild(_armsBase);
        block.AddChild(baseRow);
        _armsAdvice = UiKit.Text("", "CaptionAccent", wrap: true);
        block.AddChild(_armsAdvice);

        // Metal kits: made by forges, counted only in a fighter's hands.
        var kitTable = UiKit.Column(0);
        kitTable.AddChild(ArmsHead("METAL KIT", ("STRENGTH", "Weapon strength while carried. Bare hands is ×0.6."),
            ("CARRIED", "In a fighter's hands now."), ("MADE", "All that your settlements hold. A kit with no fighter free to carry it sits idle.")));
        for (int i = 0; i < _armsKitRows.Length; i++)
        {
            var root = UiKit.Panel(i % 2 == 0 ? "TableRowAlt" : "TableRow", null, UiKit.MouseFilter.Pass);
            var row = UiKit.Row(UiTokens.Sp2);
            var view = new ArmsKitRow
            {
                Root = root,
                Name = UiKit.Text("", "Body", wrap: true),
                Strength = UiKit.Cell("", "NumBody", ArmsCell, right: true),
                Carried = UiKit.Cell("", "NumBody", ArmsCell, right: true),
                Made = UiKit.Cell("", "NumBody", ArmsCell, right: true),
            };
            row.AddChild(view.Name);
            row.AddChild(view.Strength);
            row.AddChild(view.Carried);
            row.AddChild(view.Made);
            root.AddChild(row);
            kitTable.AddChild(root);
            _armsKitRows[i] = view;
        }
        _armsKitTable = kitTable;
        block.AddChild(kitTable);

        // Where the fighters are.
        var table = UiKit.Column(0);
        var settlementHead = ArmsHead("SETTLEMENT", ("FIGHTERS", "Fighters ready in this settlement."),
            ("KITS", "Metal kits in fighters' hands here."), ("STRENGTH", "This settlement's whole fighting force if it marched out: weapon, training and kits together."));
        table.AddChild(settlementHead);
        _armsKitsHeadCell = settlementHead.FindChild("KITS", true, false) as Control;
        _armsSettlementList = UiKit.Column(0);
        table.AddChild(_armsSettlementList);
        _armsSettlementTable = table;
        block.AddChild(table);

        _armsLocked = UiKit.Text("", "CaptionTertiary", wrap: true);
        block.AddChild(_armsLocked);
        _armsBlock = block;
        return block;
    }

    private static ArmsTile NewArmsTile(Container parent, string caption, string tooltip)
    {
        var root = UiKit.Panel("WellPanel", null, UiKit.MouseFilter.Stop);
        root.CustomMinimumSize = new Vector2(ArmsTileWidth, 0);
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.TooltipText = tooltip;
        var column = UiKit.Column(0);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        var tile = new ArmsTile { Root = root, Value = UiKit.Text("0", "NumStrong"), Caption = UiKit.Text(caption, "CaptionTertiary") };
        column.AddChild(tile.Value);
        column.AddChild(tile.Caption);
        root.AddChild(column);
        parent.AddChild(root);
        return tile;
    }

    /// <summary>A table header: the first column's title, then right-aligned numeric column titles (each named after its text, with a tooltip).</summary>
    private static Control ArmsHead(string first, params (string Title, string Tip)[] columns)
    {
        var head = UiKit.Panel("TableHead", null, UiKit.MouseFilter.Pass);
        var row = UiKit.Row(UiTokens.Sp2);
        var name = UiKit.Text(first, "EyebrowTertiary");
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(name);
        foreach ((string title, string tip) in columns)
        {
            Label cell = UiKit.Cell(title, "EyebrowTertiary", ArmsCell, right: true);
            cell.Name = title;
            cell.MouseFilter = Control.MouseFilterEnum.Stop;
            cell.TooltipText = tip;
            row.AddChild(cell);
        }
        head.AddChild(row);
        return head;
    }

    private static void SetTile(ArmsTile tile, bool visible, int value, string? tooltip = null)
    {
        UiKit.SetVisible(tile.Root, visible);
        if (!visible) return;
        UiKit.SetText(tile.Value, value.ToString());
        if (tooltip is not null && tile.Root.TooltipText != tooltip) tile.Root.TooltipText = tooltip;
    }

    private static string Hundredths(int value) => $"{value / 100}.{Math.Abs(value % 100) / 10}";

    private void RefreshArms(MatchFrame frame)
    {
        if (_armsBlock is null) return;
        ArmsFrame arms = frame.Arms;
        SettlementArms t = arms.Total;
        bool bronze = (arms.Research & ArmsFrame.HasBronzeArms) != 0, ironArms = (arms.Research & ArmsFrame.HasIronArms) != 0;
        bool chariots = (arms.Research & ArmsFrame.HasChariots) != 0, engines = (arms.Research & ArmsFrame.HasEngines) != 0;
        bool professionals = (arms.Research & ArmsFrame.HasProfessionals) != 0, warriors = (arms.Research & ArmsFrame.HasWarriors) != 0;

        UiKit.SetText(_armsStrength!, $"total strength {Hundredths(t.AttackStrength)}");
        SetTile(_tileReady!, true, t.Fighters);
        SetTile(_tileWounded!, t.Wounded > 0, t.Wounded);
        SetTile(_tileWarriors!, warriors, t.Warriors);
        SetTile(_tileSoldiers!, professionals, t.Soldiers);
        SetTile(_tileChariots!, chariots, t.Chariots,
            $"Chariots made: {t.Chariots}, with {t.ChariotCrew} fighters riding. Double strength on open grass, savanna or desert; no help against hillforts or stone walls. Crews take half the casualties.");
        SetTile(_tileEngines!, engines, t.Engines,
            "Siege engines ready. An assault on a walled town takes them from the settlement it marches from (up to 3, four crew each, burning 0.2 charcoal a season). They last 12 seasons in the field.");

        int tileCount = 0;
        foreach (ArmsTile? tile in new[] { _tileReady, _tileWounded, _tileWarriors, _tileSoldiers, _tileChariots, _tileEngines })
            if (tile is not null && tile.Root.Visible) tileCount++;
        // The page's width from the dock's (the grid's own size follows its column count, so it can't be the measure): dock − panel padding −
        // people list − the gaps around the divider − the scroll bar and its gap.
        float width = DiplomacyDockWidth - 2 * UiTokens.Sp3 - (_civListColumn?.CustomMinimumSize.X ?? 196) - (2 * UiTokens.Sp3 + 1) - UiTokens.Sp3 - 12;
        int fit = Math.Max(1, (int)((width + UiTokens.Sp2) / (ArmsTileWidth + UiTokens.Sp2)));
        int tileRows = (Math.Max(1, tileCount) + fit - 1) / fit;
        int columns = Math.Max(1, (tileCount + tileRows - 1) / tileRows);
        if (_armsTiles!.Columns != columns) _armsTiles.Columns = columns;

        int tier = Math.Clamp(arms.BaseTier, 0, ArmsBaseNames.Length - 1);
        bool ranged = tier >= 4;
        UiKit.SetText(_armsBase!, $"{ArmsBaseNames[tier]} · ×{Hundredths(arms.BaseHundredths)}");
        _armsBase!.TooltipText = "Every fighter carries this. Research gives it to all your fighters at once; no stock is needed."
            + (ranged ? " Ranged fighters fire one free volley before the melee." : "") + " A metal kit replaces it for whoever carries one.";

        int armedTotal = 0, idle = 0;
        int[] armed = { t.Steel, t.Iron, t.Tin, t.Arsenical, t.Copper };
        int[] stored = { t.StockSteel, t.StockIron, t.StockTin, t.StockArsenical, t.StockCopper };
        bool[] unlocked = { (arms.Research & ArmsFrame.HasSteel) != 0, ironArms, bronze, bronze, bronze };
        int kitRows = 0;
        for (int i = 0; i < _armsKitRows.Length; i++)
        {
            ArmsKitRow row = _armsKitRows[i];
            bool show = unlocked[i] || stored[i] > 0;
            UiKit.SetVisible(row.Root, show);
            if (!show) continue;
            UiKit.SetVariation(row.Root, (kitRows++ & 1) == 0 ? "TableRowAlt" : "TableRow");
            armedTotal += armed[i]; idle += stored[i] - armed[i];
            UiKit.SetText(row.Name, UiText.GoodName(ArmsKitKinds[i].Good));
            row.Root.TooltipText = $"{ArmsKitKinds[i].Who}. Weapon strength {ArmsKitKinds[i].Strength}. Forges make kits slowly; soldiers take the best iron and steel first, then everyone takes bronze and copper kits in order.";
            UiKit.SetText(row.Strength, ArmsKitKinds[i].Strength);
            UiKit.SetText(row.Carried, armed[i].ToString());
            UiKit.SetText(row.Made, stored[i].ToString());
        }
        bool anyKitRow = kitRows > 0;
        UiKit.SetVisible(_armsKitTable!, anyKitRow);

        var advice = new StringBuilder();
        if (t.Fighters == 0) advice.Append("You have no fighters ready.");
        else
        {
            int bare = Math.Max(0, t.Fighters - armedTotal);
            if (anyKitRow && armedTotal == 0) advice.Append("No metal kits are in anyone's hands yet; your fighters use the base weapon.");
            else if (anyKitRow && bare > 0) advice.Append($"{bare} of {t.Fighters} fighters still carry only the base weapon.");
            else if (anyKitRow) advice.Append("Every ready fighter is armed with a metal kit.");
            if (idle > 0) advice.Append($"{(advice.Length > 0 ? " " : "")}{idle} kits have no free fighter to carry them.");
            if (engines && t.Engines == 0) advice.Append($"{(advice.Length > 0 ? " " : "")}No siege engines yet: a forge builds them once you have standing soldiers (one engine per four).");
            else if (engines && t.Soldiers / 4 < Math.Min(3, t.Engines)) advice.Append($"{(advice.Length > 0 ? " " : "")}Not enough soldiers to crew every engine (four each).");
        }
        UiKit.SetText(_armsAdvice!, advice.ToString());
        UiKit.SetVisible(_armsAdvice!, advice.Length > 0);

        // Per settlement: where the fighters and kits are. The assault gathers up to 60% of the fighters in each settlement near the one it marches from.
        bool kitsColumn = bronze || ironArms;
        if (_armsKitsHeadCell is not null) UiKit.SetVisible(_armsKitsHeadCell, kitsColumn);
        int shown = 0;
        for (int w = 0; w < arms.RowCount; w++)
        {
            SettlementArms a = arms.Rows[w];
            if (a.Fighters == 0 && a.Wounded == 0 && a.StockCopper + a.StockArsenical + a.StockTin + a.StockIron + a.StockSteel + a.Engines + a.Chariots == 0) continue;
            if (shown >= _armsSettlementRows.Count) _armsSettlementRows.Add(NewArmsSettlementRow());
            ArmsSettlementRow row = _armsSettlementRows[shown];
            UiKit.SetVisible(row.Root, true);
            UiKit.SetVariation(row.Root, (shown & 1) == 0 ? "TableRowAlt" : "TableRow");
            shown++;
            int kits = a.Steel + a.Iron + a.Tin + a.Arsenical + a.Copper;
            UiKit.SetText(row.Name, SettlementLabel(frame.SettlementIndex[w]));
            var note = new StringBuilder();
            if (a.Wounded > 0) note.Append($"{a.Wounded} laid up");
            if (a.Soldiers > 0) note.Append($"{(note.Length > 0 ? " · " : "")}{a.Soldiers} {(a.Soldiers == 1 ? "soldier" : "soldiers")}");
            if (chariots && a.Chariots > 0) note.Append($"{(note.Length > 0 ? " · " : "")}{a.Chariots} {(a.Chariots == 1 ? "chariot" : "chariots")}");
            if (engines && a.Engines > 0) note.Append($"{(note.Length > 0 ? " · " : "")}{a.Engines} {(a.Engines == 1 ? "engine" : "engines")}");
            UiKit.SetText(row.Note, note.ToString());
            UiKit.SetVisible(row.Note, note.Length > 0);
            UiKit.SetText(row.Fighters, a.Fighters.ToString());
            UiKit.SetVisible(row.Kits, kitsColumn);
            UiKit.SetText(row.Kits, kits.ToString());
            UiKit.SetText(row.Strength, Hundredths(a.AttackStrength));
        }
        for (int i = shown; i < _armsSettlementRows.Count; i++) UiKit.SetVisible(_armsSettlementRows[i].Root, false);
        UiKit.SetVisible(_armsSettlementTable!, shown > 0);

        var locked = new List<string>();
        if (!warriors) locked.Add("Warrior Bands");
        if ((arms.Research & ArmsFrame.HasBow) == 0) locked.Add("Bow & Arrow");
        if (!bronze) locked.Add("Bronze Arms & Shields");
        if (!chariots) locked.Add("Chariots");
        if (!professionals) locked.Add("Professional Armies");
        if (!ironArms) locked.Add("Iron Weapons");
        if (!engines) locked.Add("Siege Engines");
        UiKit.SetText(_armsLocked!, locked.Count == 0 ? "" : "Still to research for war: " + string.Join(", ", locked) + ".");
        UiKit.SetVisible(_armsLocked!, locked.Count > 0);
    }

    private ArmsSettlementRow NewArmsSettlementRow()
    {
        var root = UiKit.Panel("TableRow", null, UiKit.MouseFilter.Pass);
        root.TooltipText = "Strength is this settlement's whole fighting force if it marched out. An assault takes at most 60% of its fighters and always leaves some at home.";
        var row = UiKit.Row(UiTokens.Sp2);
        var names = UiKit.Column(0);
        names.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        names.MouseFilter = Control.MouseFilterEnum.Ignore;
        var view = new ArmsSettlementRow
        {
            Root = root,
            Name = UiKit.Text("", "Body", ellipsis: true),
            Note = UiKit.Text("", "CaptionTertiary", wrap: true),
            Fighters = UiKit.Cell("", "NumBody", ArmsCell, right: true),
            Kits = UiKit.Cell("", "NumBody", ArmsCell, right: true),
            Strength = UiKit.Cell("", "NumStrong", ArmsCell, right: true),
        };
        names.AddChild(view.Name);
        names.AddChild(view.Note);
        row.AddChild(names);
        foreach (Label cell in new[] { view.Fighters, view.Kits, view.Strength })
        {
            cell.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            row.AddChild(cell);
        }
        root.AddChild(row);
        _armsSettlementList!.AddChild(root);
        return view;
    }
}
