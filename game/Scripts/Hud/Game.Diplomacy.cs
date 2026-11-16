using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Core;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The diplomacy drawer: pick a people, see where you stand, give orders with their reasons shown (spec §3.9).</summary>
public partial class Game
{
    private sealed class OrderRow
    {
        public HBoxContainer Root = null!;
        public UiButton Button = null!;
        public Label Text = null!;
        public UiIcon Lock = null!;
        public Control? Extra;
        public Action Act = null!;
        public string Label = "", ConfirmLabel = "";
        public bool NeedsConfirm, Confirming, Enabled;
        public float Timer;
    }

    private PanelContainer? _diplomacyDrawer;
    private bool _diplomacyOpen;
    private UiLayer? _diplomacyLayer;
    private readonly List<UiCivRow> _civRows = new();
    private VBoxContainer? _civList;
    private Control? _civListColumn, _civListDivider, _diplomacyDetail;
    private Label? _diploEmptyLabel, _diploName, _diploMeta, _diploAi, _diploNoParties, _diploStanding, _diploPartyCount;
    private UiEmblem? _diploEmblem;
    private OptionButton? _diploCompactGood;
    private OptionButton? _raidPartySize;
    private CheckButton? _diploCompactSell;
    private UiButton? _diploCompactAction, _diploEmbargoAction;
    private PanelContainer? _diploStatusChip, _diploRelationChip, _diploAgeChip;
    private OptionButton? _destinationSelect;
    private readonly List<(int Settlement, string Text)> _destinationOptions = new();
    private int _destinationOptionsCiv = -1;
    private bool _syncingDestinationSelect;
    private OrderRow? _giftRow, _barterRow, _allianceRow, _peaceRow, _raidRow, _warRow, _assaultRow;
    // Iron chapter orders (docs/MVP/25 §7, §8): shown only when the match plays the Iron chapter.
    private OrderRow? _leagueRow, _tributeRow, _blockadeRow;
    private VBoxContainer? _partyList;
    private readonly List<(HBoxContainer Root, Label Text, UiProgressBar Bar, UiButton Recall)> _partyRows = new();
    private readonly List<Label> _pendingPartyRows = new();
    private int _selectedPoliticsCiv = -1, _selectedTargetSettlement = -1;
    private int _war02 = -1, _trd01 = -1, _trd02 = -1, _dip03 = -1, _dip06 = -1, _dip07 = -1, _sea09 = -1;
    private readonly List<int> _civOrder = new();
    private readonly record struct ProjectedRelation(bool AtWar, bool Allied, bool InTruce, bool WarQueued, bool PeaceQueued, bool AllianceQueued, bool AssaultQueued);

    // The detail pane's three pages: Relations (gifts, alliance, league), Trade (barter, compacts, embargo), War (war and peace, raids, assault,
    // with the forces readiness right below the assault it informs). One page shows at a time so no order list grows into a long scroll.
    private const int DiploTabRelations = 0, DiploTabTrade = 1, DiploTabWar = 2;
    private static readonly string[] DiploTabNames = { "Relations", "Trade", "War" };
    private UiSegmented? _diploTabs;
    private readonly Control?[] _diploPages = new Control?[3];
    private ScrollContainer? _diploPageScroll;
    private int _diploTab = DiploTabRelations;

    private void CreateDiplomacyDrawer()
    {
        if (_catalog is not null)
        {
            _war02 = _catalog.IndexOf("WAR-02");
            _trd01 = _catalog.IndexOf("TRD-01");
            _trd02 = _catalog.IndexOf("TRD-02");
            _dip03 = _catalog.IndexOf("DIP-03");
            _dip06 = _catalog.IndexOf("DIP-06");
            _dip07 = _catalog.IndexOf("DIP-07");
            _sea09 = _catalog.IndexOf("SEA-09");
        }
        _diplomacyDrawer = UiKit.Panel("HudPanel", "DiplomacyDrawer");
        _diplomacyDrawer.Visible = false;
        var root = UiKit.Column(UiTokens.Sp2);
        var header = UiKit.Row(UiTokens.Sp2);
        var heading = UiKit.Text("Diplomacy", "Heading");
        heading.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(heading);
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (O)", () => ToggleDiplomacy(false), 36, 16));
        root.AddChild(header);

        _diploEmptyLabel = UiKit.Text("", "BodySecondary", wrap: true);
        root.AddChild(_diploEmptyLabel);

        // Body: the peoples you know on the left, the selected people on the right.
        var body = UiKit.Row(UiTokens.Sp3);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var listColumn = UiKit.Column(UiTokens.Sp2);
        listColumn.AddChild(UiKit.Text("PEOPLES YOU KNOW", "EyebrowTertiary"));
        _civList = UiKit.Column(UiTokens.Sp1);
        listColumn.AddChild(UiKit.Scroll(_civList));
        _civListColumn = listColumn;
        body.AddChild(listColumn);
        _civListDivider = UiKit.Divider(vertical: true);
        body.AddChild(_civListDivider);

        var detail = UiKit.Column(UiTokens.Sp2);
        detail.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        detail.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _diplomacyDetail = detail;
        body.AddChild(detail);
        root.AddChild(body);

        // 1. Who they are and where you stand.
        var title = UiKit.Row(UiTokens.Sp3);
        _diploEmblem = new UiEmblem(0, 40) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        title.AddChild(_diploEmblem);
        var identity = UiKit.Column(UiTokens.Sp1);
        identity.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _diploName = UiKit.Text("", "Title", ellipsis: true);
        identity.AddChild(_diploName);
        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", UiTokens.Sp1 + 2);
        chips.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _diploStatusChip = UiKit.Chip("CONTACTED");
        _diploRelationChip = UiKit.Chip("NEUTRAL");
        _diploAgeChip = UiKit.Chip("DAWN AGE");
        chips.AddChild(_diploStatusChip);
        chips.AddChild(_diploRelationChip);
        chips.AddChild(_diploAgeChip);
        identity.AddChild(chips);
        title.AddChild(identity);
        detail.AddChild(title);

        // 2. The settlement the next gift, barter, raid, assault or blockade goes to.
        var targetRow = UiKit.Row(UiTokens.Sp3);
        var targetLabel = UiKit.Text("TARGET", "EyebrowTertiary");
        targetLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        targetRow.AddChild(targetLabel);
        _destinationSelect = new OptionButton
        {
            CustomMinimumSize = new Vector2(0, UiTokens.ControlS),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
            TooltipText = "Used for gifts and barter. Raids and assaults also use this target. Choose Civilization decides to let the simulation pick.",
        };
        _destinationSelect.ItemSelected += selected =>
        {
            if (_syncingDestinationSelect) return;
            _selectedTargetSettlement = _destinationSelect.GetItemId((int)selected);
            if (_current is not null) RefreshDiplomacy(_current);
        };
        targetRow.AddChild(_destinationSelect);
        detail.AddChild(targetRow);

        // 3. The grouped orders, one page at a time.
        _diploTabs = new UiSegmented(DiploTabNames, _diploTab, SelectDiplomacyTab, 96) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        _diploTabs.SetTooltip(DiploTabRelations, "Gifts, alliances and leagues.");
        _diploTabs.SetTooltip(DiploTabTrade, "Barter trips, standing trade compacts and embargoes.");
        _diploTabs.SetTooltip(DiploTabWar, "War and peace, raids and assaults, and how ready your fighters are.");
        detail.AddChild(_diploTabs);

        var pages = UiKit.Column(0);
        // Breathing room between the page and its scroll bar.
        var scrollPad = new MarginContainer();
        scrollPad.AddThemeConstantOverride("margin_right", UiTokens.Sp3);
        scrollPad.AddChild(pages);
        _diploPageScroll = UiKit.Scroll(scrollPad);
        detail.AddChild(_diploPageScroll);

        // Relations
        var relations = UiKit.Column(UiTokens.Sp2);
        _diploStanding = UiKit.Text("", "Caption", wrap: true);
        relations.AddChild(_diploStanding);
        _diploMeta = UiKit.Text("", "CaptionTertiary", wrap: true);
        relations.AddChild(_diploMeta);
        _diploAi = UiKit.Text("", "CaptionTertiary", wrap: true);
        relations.AddChild(_diploAi);
        relations.AddChild(SectionLabel("ORDERS"));
        _giftRow = NewOrderRow("Send gift", QueueGift, false, "");
        _allianceRow = NewOrderRow("Form alliance", () => QueueDiplomacyOrder(PlayerCommands.FormAlliance), false, "");
        _leagueRow = NewOrderRow("Form league", () => QueueDiplomacyOrder(PlayerCommands.FormLeague), false, "");
        foreach (OrderRow row in new[] { _giftRow, _allianceRow, _leagueRow }) relations.AddChild(row.Root);
        pages.AddChild(relations);
        _diploPages[DiploTabRelations] = relations;

        // Trade
        var trade = UiKit.Column(UiTokens.Sp2);
        trade.AddChild(SectionLabel("TRIPS"));
        _barterRow = NewOrderRow("Barter", QueueBarter, false, "");
        trade.AddChild(_barterRow.Root);
        trade.AddChild(SectionLabel("STANDING TRADE"));
        trade.AddChild(UiKit.Text("A compact is a lasting goods route with this people. An embargo stops your traders selling them one good.", "CaptionTertiary", wrap: true));
        var goodRow = UiKit.Row(UiTokens.Sp2);
        var goodLabel = UiKit.Text("Good", "Caption", minWidth: 44);
        goodLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        goodRow.AddChild(goodLabel);
        _diploCompactGood = new OptionButton { CustomMinimumSize = new Vector2(120, UiTokens.ControlS), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (TradeGood good in new[] { TradeGood.Tin, TradeGood.Copper, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Food, TradeGood.Luxury })
            _diploCompactGood.AddItem(good.ToString(), (int)good);
        _diploCompactGood.ItemSelected += _ => { if (_current is not null) RefreshDiplomacy(_current); };
        goodRow.AddChild(_diploCompactGood);
        var buyLabel = UiKit.Text("Buy", "Caption");
        buyLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        goodRow.AddChild(buyLabel);
        _diploCompactSell = new CheckButton { Text = "", TooltipText = "Off: you buy this good from them · On: you sell it to them", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        goodRow.AddChild(_diploCompactSell);
        var sellLabel = UiKit.Text("Sell", "Caption");
        sellLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        goodRow.AddChild(sellLabel);
        trade.AddChild(goodRow);
        var tradeActions = UiKit.Row(UiTokens.Sp2);
        _diploCompactAction = UiKit.Secondary("Offer compact", QueueSelectedCompact, "Offer a standing goods route to this people.");
        _diploCompactAction.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _diploEmbargoAction = UiKit.Secondary("Set embargo", ToggleSelectedEmbargo, "Stop or resume selling the selected good to this people.");
        _diploEmbargoAction.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tradeActions.AddChild(_diploCompactAction);
        tradeActions.AddChild(_diploEmbargoAction);
        trade.AddChild(tradeActions);
        pages.AddChild(trade);
        _diploPages[DiploTabTrade] = trade;

        // War: the orders, then the forces they would send.
        var war = UiKit.Column(UiTokens.Sp2);
        war.AddChild(SectionLabel("ORDERS"));
        _warRow = NewOrderRow("Declare war", () => QueueDiplomacyOrder(PlayerCommands.DeclareWar), true, "Confirm war");
        _peaceRow = NewOrderRow("Make peace", () => QueueDiplomacyOrder(PlayerCommands.MakePeace), false, "");
        _tributeRow = NewOrderRow("Impose tribute", () => QueueDiplomacyOrder(PlayerCommands.ImposeTribute), true, "Confirm terms");
        var raidSizeRow = UiKit.Row(UiTokens.Sp2);
        var raidSizeLabel = UiKit.Text("Party size", "Caption");
        raidSizeLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        raidSizeRow.AddChild(raidSizeLabel);
        _raidPartySize = new OptionButton { CustomMinimumSize = new Vector2(118, 32) };
        _raidPartySize.AddItem("4 raiders", 4);
        _raidPartySize.AddItem("Up to 8", 8);
        _raidPartySize.AddItem("Up to 12", 12);
        _raidPartySize.Select(0);
        raidSizeRow.AddChild(_raidPartySize);
        _raidRow = NewOrderRow("Raid", QueueRaid, true, "Confirm raid", raidSizeRow);
        _blockadeRow = NewOrderRow("Blockade harbor", QueueBlockade, true, "Confirm blockade");
        _assaultRow = NewOrderRow("Assault", QueueAssault, true, "Confirm assault");
        foreach (OrderRow row in new[] { _warRow, _peaceRow, _tributeRow, _raidRow, _blockadeRow, _assaultRow }) war.AddChild(row.Root);
        war.AddChild(BuildArmsBlock());
        pages.AddChild(war);
        _diploPages[DiploTabWar] = war;

        // 4. Parties on the road, under every page.
        detail.AddChild(UiKit.Divider());
        var partiesHead = UiKit.Row(UiTokens.Sp2);
        partiesHead.AddChild(UiKit.Text("ON THE ROAD", "EyebrowTertiary"));
        _diploPartyCount = UiKit.Text("", "Num");
        partiesHead.AddChild(_diploPartyCount);
        partiesHead.AddChild(UiKit.Spacer(expand: true));
        _diploNoParties = UiKit.Text("No parties out.", "CaptionTertiary");
        partiesHead.AddChild(_diploNoParties);
        detail.AddChild(partiesHead);
        _partyList = UiKit.Column(UiTokens.Sp1);
        detail.AddChild(_partyList);

        SelectDiplomacyTab(_diploTab);
        _diplomacyDrawer.AddChild(root);
        _hudRoot!.AddChild(_diplomacyDrawer);
        _diplomacyLayer = RegisterLayer("diplomacy", false, () => { ToggleDiplomacy(false); return true; });
    }

    private static Control SectionLabel(string text)
    {
        var label = UiKit.Text(text, "EyebrowTertiary");
        return UiKit.Margin(label, 0, UiTokens.Sp1, 0, 0);
    }

    private void SelectDiplomacyTab(int tab)
    {
        _diploTab = Math.Clamp(tab, 0, _diploPages.Length - 1);
        for (int i = 0; i < _diploPages.Length; i++)
            if (_diploPages[i] is { } page) UiKit.SetVisible(page, i == _diploTab);
        _diploTabs?.Select(_diploTab, false);
        if (_diploPageScroll is not null) _diploPageScroll.ScrollVertical = 0;
        foreach (OrderRow? row in new[] { _raidRow, _warRow, _assaultRow, _tributeRow, _blockadeRow })
            if (row is not null) CancelConfirm(row);
    }

    /// <summary>One order: a fixed-width button, then what it does, or a lock and the reason it can't be given yet (quiet, never louder than the button).</summary>
    private OrderRow NewOrderRow(string label, Action act, bool confirm, string confirmLabel, Control? extra = null)
    {
        var row = new OrderRow { Label = label, Act = act, NeedsConfirm = confirm, ConfirmLabel = confirmLabel, Extra = extra };
        row.Root = UiKit.Row(UiTokens.Sp3);
        row.Root.CustomMinimumSize = new Vector2(0, UiTokens.ControlS);
        row.Button = UiKit.Secondary(label, null, null, UiGlyph.None, UiTokens.ControlS, true);
        row.Button.CustomMinimumSize = new Vector2(OrderButtonWidth, UiTokens.ControlS);
        row.Button.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.Button.Pressed += () => PressOrder(row);
        row.Root.AddChild(row.Button);
        var column = UiKit.Column(UiTokens.Sp1);
        column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var line = UiKit.Row(6);
        row.Lock = new UiIcon(UiGlyph.Lock, 14, UiTokens.TextTertiary) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        line.AddChild(row.Lock);
        row.Text = UiKit.Text("", "Caption", wrap: true);
        row.Text.MouseFilter = Control.MouseFilterEnum.Stop;
        line.AddChild(row.Text);
        column.AddChild(line);
        if (extra is not null) column.AddChild(extra);
        row.Root.AddChild(column);
        return row;
    }

    private const int OrderButtonWidth = 136;

    private void PressOrder(OrderRow row)
    {
        if (!row.Enabled) return;
        if (row.NeedsConfirm && !row.Confirming)
        {
            row.Confirming = true;
            row.Timer = 4.0f;
            row.Button.Text = row.ConfirmLabel;
            UiKit.SetVariation(row.Button, "Danger");
            return;
        }
        row.Confirming = false;
        row.Button.Text = row.Label;
        UiKit.SetVariation(row.Button, "Secondary");
        row.Act();
        if (_session?.Paused == true) ShowToast("Order sent", ToastKind.Info, "It starts when you resume.", key: "order-paused");
    }

    private void CancelConfirm(OrderRow row)
    {
        if (!row.Confirming) return;
        row.Confirming = false;
        row.Button.Text = row.Label;
        UiKit.SetVariation(row.Button, "Secondary");
    }

    private void TickDiplomacy(float delta)
    {
        foreach (OrderRow? row in new[] { _raidRow, _warRow, _assaultRow, _tributeRow, _blockadeRow })
        {
            if (row is null || !row.Confirming) continue;
            row.Timer -= delta;
            if (row.Timer <= 0) CancelConfirm(row);
        }
    }

    private void LayoutDiplomacyDrawer(float top)
    {
        if (_diplomacyDrawer is null) return;
        // Its own, wider dock width (list + detail). Full dock height once someone is met; before that, only as tall as its message.
        _drawerFullHeight = DockMaxHeight;
        if (_civListColumn is not null) _civListColumn.CustomMinimumSize = new Vector2(_compact ? 164 : 196, 0);
        PlaceDock(_diplomacyDrawer, DiplomacyDockWidth, _civOrder.Count > 0 ? _drawerFullHeight : 0);
    }

    private float _drawerFullHeight;

    private void ToggleDiplomacy(bool? force = null)
    {
        if (_diplomacyDrawer is null) return;
        bool open = force ?? !_diplomacyOpen;
        if (open == _diplomacyOpen) return;
        _diplomacyOpen = open;
        if (open)
        {
            CloseOtherDocks("diplomacy");
            if (_researchWebVisible) CloseResearchWeb();
            if (_current is not null) RefreshDiplomacy(_current);
            _diplomacyDrawer.Modulate = new Color(1, 1, 1, 0);
            _diplomacyDrawer.Visible = true;
            UiKit.Fade(_diplomacyDrawer, true, UiTokens.BaseS);
            _ui.Push(_diplomacyLayer!);
        }
        else
        {
            UiKit.Fade(_diplomacyDrawer, false, UiTokens.BaseCloseS);
            _ui.Remove(_diplomacyLayer!);
        }
        LayoutNotifications();
    }

    private string RelationWord(int relations) => relations <= -40 ? "Hostile" : relations < 0 ? "Cold" : relations <= 40 ? "Neutral" : "Warm";

    private void RefreshDiplomacy(MatchFrame frame)
    {
        if (!_diplomacyOpen || _civList is null || _catalog is null) return;
        int viewer = Math.Max(0, frame.ViewerCiv);
        bool own = frame.ViewerCiv >= 0;

        _civOrder.Clear();
        for (int civ = 0; civ < frame.CivCount; civ++)
            if (civ != viewer && (frame.CivFlags[civ] & (ushort)CivViewFlags.Aware) != 0) _civOrder.Add(civ);
        _civOrder.Sort((a, b) => RelationRank(frame, a).CompareTo(RelationRank(frame, b)));

        UiKit.SetVisible(_diploEmptyLabel!, _civOrder.Count == 0 || !own);
        UiKit.SetText(_diploEmptyLabel!, !own ? "Orders are available only when you play your own civilization."
            : _civOrder.Count == 0 ? "You haven't met anyone yet. Scouts and growing borders will find your neighbours." : "");
        bool full = _civOrder.Count > 0;
        if (full != _diplomacyDrawer!.CustomMinimumSize.Y > 0) PlaceDock(_diplomacyDrawer, DiplomacyDockWidth, full ? _drawerFullHeight : 0);
        if (!full) _diplomacyDrawer.ResetSize();
        UiKit.SetVisible(_civListColumn!, _civOrder.Count > 0);
        UiKit.SetVisible(_diplomacyDetail!, _civOrder.Count > 0 && own);
        UiKit.SetVisible(_civListDivider!, _civOrder.Count > 0 && own);
        if (_civOrder.Count == 0) return;
        if (!_civOrder.Contains(_selectedPoliticsCiv)) { _selectedPoliticsCiv = _civOrder[0]; _selectedTargetSettlement = -1; }

        for (int i = 0; i < _civOrder.Count; i++)
        {
            int civ = _civOrder[i];
            UiCivRow row;
            if (i >= _civRows.Count)
            {
                row = new UiCivRow();
                row.Pressed += () => { if (row.Civ >= 0) { _selectedPoliticsCiv = row.Civ; _selectedTargetSettlement = -1; if (_current is not null) RefreshDiplomacy(_current); } };
                _civRows.Add(row);
                _civList.AddChild(row);
            }
            row = _civRows[i];
            row.Visible = true;
            row.Set(civ, UiText.CivName(civ), CivStatusText(frame, civ));
            row.SetPressedNoSignal(civ == _selectedPoliticsCiv);
        }
        for (int i = _civOrder.Count; i < _civRows.Count; i++) _civRows[i].Visible = false;
        if (!own) return;

        int target = _selectedPoliticsCiv;
        CivViewFlags flags = (CivViewFlags)frame.CivFlags[target];
        ProjectedRelation relation = RelationAfterQueuedOrders(frame, target);
        bool contacted = (flags & CivViewFlags.Contacted) != 0, atWar = relation.AtWar;
        bool allied = relation.Allied, truce = relation.InTruce;
        if (_diploCompactAction is not null)
            _diploCompactAction.Disabled = !(_session?.CanIssueCommands ?? false) || !frame.FocusMandateUnlocked || atWar || !contacted;
        if (_diploEmbargoAction is not null)
        {
            _diploEmbargoAction.Disabled = !(_session?.CanIssueCommands ?? false) || !contacted;
            if (_diploCompactGood is not null && _diploCompactGood.Selected >= 0)
            {
                TradeGood good = (TradeGood)_diploCompactGood.GetItemId(_diploCompactGood.Selected);
                bool embargoed = EmbargoAfterQueuedOrders(frame, target, good);
                bool queued = false;
                if (_session is not null)
                    foreach (Command command in _session.PendingCommands)
                        if (command.Civ == frame.ViewerCiv && command.Kind == CommandKind.SetEmbargo && command.A == target && command.B == (int)good)
                            queued = true;
                _diploEmbargoAction.Text = queued ? (embargoed ? "Embargo queued" : "Lift queued") : (embargoed ? "Lift embargo" : "Set embargo");
            }
        }
        _diploEmblem!.Civ = target;
        UiKit.SetText(_diploName!, UiText.CivName(target));
        (string status, string variation) = atWar
            ? (relation.WarQueued && (flags & CivViewFlags.AtWar) == 0 ? "WAR QUEUED" : "AT WAR", "ChipPanelDanger")
            : allied
                ? (relation.AllianceQueued && (flags & CivViewFlags.Allied) == 0 ? "ALLIANCE QUEUED" : "ALLIED", "ChipPanelSuccess")
                : truce
                    ? (relation.PeaceQueued ? "PEACE QUEUED" : "IN TRUCE", "ChipPanelInfo")
                    : contacted ? ("CONTACTED", "ChipPanel") : ("KNOWN ONLY", "ChipPanel");
        UiKit.SetChipText(_diploStatusChip!, status);
        UiKit.SetVariation(_diploStatusChip!, variation);
        int relations = frame.CivRelations[target];
        UiKit.SetChipText(_diploRelationChip!, contacted ? $"{RelationWord(relations).ToUpperInvariant()} {relations:+0;−0;0}" : "NO CONTACT YET");
        UiKit.SetChipText(_diploAgeChip!, UiText.AgeName(frame.CivEra[target]).ToUpperInvariant() + " AGE");
        int seen = 0;
        for (int i = 0; i < frame.SettlementCount; i++)
            if (frame.SettlementCiv[i] == target && (frame.SettlementFlags[i] & (ushort)(SettlementViewFlags.FreeFolk | SettlementViewFlags.Empty)) == 0) seen++;
        UiKit.SetText(_diploMeta!, seen == 0 ? "None of their settlements seen yet." : $"{seen} of their {(seen == 1 ? "settlement" : "settlements")} seen.");
        UiKit.SetText(_diploStanding!, StandingText(contacted, atWar, allied, truce, relations));
        string ai = "";
        if (contacted)
            for (int i = frame.AiWhyCount - 1; i >= 0; i--)
                if (frame.AiWhyEntries[i].Civ == target) { ai = $"Their elders say: “{frame.AiWhyEntries[i].Reason}”"; break; }
        UiKit.SetText(_diploAi!, ai);
        UiKit.SetVisible(_diploAi!, ai.Length > 0);

        RefreshDestinations(frame, target);
        RefreshOrders(frame, target, contacted, atWar, allied, truce, relations, relation.AssaultQueued);
        RefreshParties(frame);
        RefreshArms(frame);
    }

    /// <summary>One plain sentence on where the two peoples stand and what moves it, at the top of the Relations page.</summary>
    private string StandingText(bool contacted, bool atWar, bool allied, bool truce, int relations)
    {
        if (!contacted) return "You have only seen them from afar. Meet them (scouts, borders or trade) before you can send anything.";
        if (atWar) return $"You are at war (relations {relations:+0;−0;0}). The War page has the orders that end it.";
        if (allied) return $"You are allies (relations {relations:+0;−0;0}) and defend each other.";
        string mood = RelationWord(relations).ToLowerInvariant();
        string next = relations <= 0 ? "Gifts warm them; barter needs relations above 0."
            : relations <= 40 ? "Gifts and barter warm them; above 40 they will hear an alliance."
            : "Warm enough for an alliance.";
        return (truce ? "A truce holds. " : "") + $"Relations are {mood} ({relations:+0;−0;0}). {next} Raids and war cool them.";
    }

    private ProjectedRelation RelationAfterQueuedOrders(MatchFrame frame, int civ)
    {
        CivViewFlags flags = (CivViewFlags)frame.CivFlags[civ];
        bool atWar = (flags & CivViewFlags.AtWar) != 0;
        bool allied = (flags & CivViewFlags.Allied) != 0;
        bool truce = (flags & CivViewFlags.InTruce) != 0;
        bool warQueued = false, peaceQueued = false, allianceQueued = false, assaultQueued = false;
        if (_session is not null)
        {
            foreach (Command command in _session.PendingCommands)
            {
                if (command.Civ != frame.ViewerCiv) continue;
                switch (command.Kind)
                {
                    case CommandKind.DeclareWar when command.A == civ:
                        atWar = true;
                        allied = false;
                        warQueued = true;
                        break;
                    case CommandKind.MakePeace when command.A == civ:
                        atWar = false;
                        allied = false;
                        truce = true;
                        peaceQueued = true;
                        break;
                    case CommandKind.FormAlliance when command.A == civ:
                        atWar = false;
                        allied = true;
                        truce = false;
                        allianceQueued = true;
                        break;
                    case CommandKind.LaunchAssault:
                        int targetCiv = command.A == -1 ? command.B - 1 : SettlementOwner(frame, command.A);
                        if (targetCiv == civ) assaultQueued = true;
                        break;
                }
            }
        }
        return new ProjectedRelation(atWar, allied, truce, warQueued, peaceQueued, allianceQueued, assaultQueued);
    }

    private static int SettlementOwner(MatchFrame frame, int settlement)
    {
        for (int i = 0; i < frame.SettlementCount; i++)
            if (frame.SettlementIndex[i] == settlement) return frame.SettlementCiv[i];
        return -1;
    }

    private bool HasPendingDiplomaticOrder(MatchFrame frame, int civ, CommandKind kind)
    {
        if (_session is null) return false;
        foreach (Command command in _session.PendingCommands)
        {
            if (command.Civ != frame.ViewerCiv || command.Kind != kind) continue;
            int target = kind switch
            {
                CommandKind.SendGift or CommandKind.SendBarter or CommandKind.DeclareWar or CommandKind.MakePeace or
                    CommandKind.FormAlliance or CommandKind.ImposeTribute or CommandKind.FormLeague => command.A,
                CommandKind.LaunchRaidAgainst => command.A,
                CommandKind.LaunchRaid or CommandKind.Blockade => SettlementOwner(frame, command.A),
                _ => -1,
            };
            if (target == civ) return true;
        }
        return false;
    }

    private int RelationRank(MatchFrame frame, int civ)
    {
        ProjectedRelation relation = RelationAfterQueuedOrders(frame, civ);
        return relation.AtWar ? 0 : relation.Allied ? 1 : (frame.CivFlags[civ] & (ushort)CivViewFlags.Contacted) != 0 ? 2 : 3;
    }

    private string CivStatusText(MatchFrame frame, int civ)
    {
        CivViewFlags f = (CivViewFlags)frame.CivFlags[civ];
        ProjectedRelation relation = RelationAfterQueuedOrders(frame, civ);
        string state = relation.AtWar ? (relation.WarQueued && (f & CivViewFlags.AtWar) == 0 ? "War queued" : "At war")
            : relation.Allied ? (relation.AllianceQueued && (f & CivViewFlags.Allied) == 0 ? "Alliance queued" : "Allied")
            : relation.InTruce ? (relation.PeaceQueued ? "Peace queued" : "Truce")
            : (f & CivViewFlags.Contacted) != 0 ? RelationWord(frame.CivRelations[civ]) : "Seen from afar";
        return $"{state} · {UiText.AgeName(frame.CivEra[civ])}";
    }

    private void RefreshDestinations(MatchFrame frame, int civ)
    {
        var options = new List<(int Settlement, string Text)> { (-1, "Civilization decides") };
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (frame.SettlementCiv[i] != civ) continue;
            SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[i];
            if ((flags & (SettlementViewFlags.FreeFolk | SettlementViewFlags.Empty)) != 0) continue;
            bool remembered = (flags & SettlementViewFlags.Remembered) != 0;
            string label = remembered
                ? $"{SettlementLabel(frame.SettlementIndex[i])} · last seen"
                : $"{SettlementLabel(frame.SettlementIndex[i])} · {frame.SettlementPopulation[i]} people";
            options.Add((frame.SettlementIndex[i], label));
        }
        bool selectedStillThere = false;
        foreach ((int settlement, string _) in options)
            if (settlement == _selectedTargetSettlement) { selectedStillThere = true; break; }
        if (!selectedStillThere) _selectedTargetSettlement = -1;

        bool changed = _destinationOptionsCiv != civ || _destinationOptions.Count != options.Count;
        if (!changed)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (_destinationOptions[i] != options[i]) { changed = true; break; }
            }
        }
        if (changed)
        {
            _syncingDestinationSelect = true;
            _destinationSelect!.Clear();
            _destinationOptions.Clear();
            foreach ((int settlement, string text) in options)
            {
                _destinationSelect.AddItem(text, settlement);
                _destinationOptions.Add((settlement, text));
            }
            _destinationOptionsCiv = civ;
            _syncingDestinationSelect = false;
        }

        int selectedIndex = 0;
        for (int i = 0; i < _destinationOptions.Count; i++)
            if (_destinationOptions[i].Settlement == _selectedTargetSettlement) { selectedIndex = i; break; }
        _syncingDestinationSelect = true;
        _destinationSelect!.Select(selectedIndex);
        _syncingDestinationSelect = false;
        _destinationSelect.TooltipText = _selectedTargetSettlement < 0
            ? "Used for gifts and barter. Raids and assaults use the selected settlement too. The simulation picks a target by default."
            : DestinationTooltip(frame, civ, _selectedTargetSettlement);
    }

    private bool HasProjectDone(MatchFrame frame, int project)
    {
        if (project < 0 || project >= frame.FocusProjectCount) return false;
        return (ProjectState)frame.FocusProjectState[project] is ProjectState.Completed or ProjectState.Dormant;
    }

    private string NeedsName(int project) => project >= 0 && _catalog is not null ? _catalog.Projects[project].Name : "a research project";

    private static string DestinationTooltip(MatchFrame frame, int civ, int settlement)
    {
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (frame.SettlementIndex[i] != settlement || frame.SettlementCiv[i] != civ) continue;
            if ((frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) == 0) return "Currently visible settlement.";
            int seen = frame.SettlementSeenSeason[i];
            string when = seen >= 0
                ? $"{UiText.SeasonNames[seen & 3]}, Year {seen / 4 + 1}"
                : "an earlier season";
            return $"Last seen in {when} with {frame.SettlementPopulation[i]} people. If control has changed, the assault will not launch.";
        }
        return "";
    }

    private static bool IsRememberedDestination(MatchFrame frame, int civ, int settlement)
    {
        for (int i = 0; i < frame.SettlementCount; i++)
            if (frame.SettlementIndex[i] == settlement && frame.SettlementCiv[i] == civ)
                return (frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) != 0;
        return false;
    }

    private void SetOrder(OrderRow row, bool visible, bool enabled, string text)
    {
        UiKit.SetVisible(row.Root, visible);
        row.Enabled = enabled;
        row.Button.Disabled = !enabled;
        row.Lock.Visible = !enabled;
        UiKit.SetText(row.Text, text);
        UiKit.SetVariation(row.Text, enabled ? "Caption" : "CaptionTertiary");
        if (row.Text.TooltipText != text) row.Text.TooltipText = text;
        if (row.Button.TooltipText != text) row.Button.TooltipText = text;
        if (row.Extra is not null) UiKit.SetVisible(row.Extra, enabled);
        if (!enabled) CancelConfirm(row);
    }

    private void RefreshOrders(MatchFrame frame, int civ, bool contacted, bool atWar, bool allied, bool truce, int relations, bool assaultQueued)
    {
        bool trd01 = HasProjectDone(frame, _trd01), trd02 = HasProjectDone(frame, _trd02), war02 = HasProjectDone(frame, _war02), dip03 = HasProjectDone(frame, _dip03);
        bool giftQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.SendGift);
        bool barterQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.SendBarter);
        bool raidQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.LaunchRaid) || HasPendingDiplomaticOrder(frame, civ, CommandKind.LaunchRaidAgainst);
        bool barterInFlight = false;
        for (int i = 0; i < frame.PartyCount; i++)
            if (frame.PartyCiv[i] == frame.ViewerCiv && frame.PartyTargetCiv[i] == civ && frame.PartyKind[i] == (byte)PartyKind.Barter) barterInFlight = true;
        bool can = _session?.CanIssueCommands ?? false;
        if (_diploCompactAction is not null) _diploCompactAction.TooltipText = CompactAcceptanceHint(frame.ViewerCiv, civ);

        SetOrder(_giftRow!, true, can && contacted && !atWar && trd01 && !giftQueued,
            giftQueued ? "Gift queued; it starts when you resume." : !trd01 ? $"Needs {NeedsName(_trd01)} (research)." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." : "Carries food to them. Improves relations.");
        SetOrder(_barterRow!, true, can && contacted && !atWar && trd02 && relations > 0 && !barterInFlight && !barterQueued,
            barterQueued ? "Barter queued; it starts when you resume." : !trd02 ? $"Needs {NeedsName(_trd02)} (research)." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." :
            relations <= 0 ? $"Needs warmer relations (above 0; now {relations})." : barterInFlight ? "A barter trip is already on the way." : "Trades your surplus for theirs.");
        SetOrder(_allianceRow!, !allied, can && contacted && !atWar && !allied && relations > 40 && dip03,
            !dip03 ? $"Needs {NeedsName(_dip03)}: both of you must know it." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." :
            relations <= 40 ? $"Needs warmer relations (above 40; now {relations})." : "You defend each other.");
        bool occupation = false, ownOccupation = false, assaultInFlight = false;
        for (int i = 0; i < frame.OccupationCount; i++)
            for (int s = 0; s < frame.SettlementCount; s++)
            {
                if (frame.SettlementIndex[s] != frame.OccupationSettlement[i]) continue;
                bool usHolding = frame.OccupationCiv[i] == frame.ViewerCiv && frame.SettlementCiv[s] == civ;
                bool theyHolding = frame.OccupationCiv[i] == civ && frame.SettlementCiv[s] == frame.ViewerCiv;
                occupation |= usHolding || theyHolding;
                ownOccupation |= usHolding;
            }
        for (int i = 0; i < frame.PartyCount; i++)
            if (frame.PartyCiv[i] == frame.ViewerCiv && frame.PartyTargetCiv[i] == civ && frame.PartyKind[i] == (byte)PartyKind.Assault)
                assaultInFlight = true;
        SetOrder(_peaceRow!, atWar, can && atWar && !occupation, occupation ? "Not while you hold one of their settlements." : "Ends the war. A truce follows.");

        RefreshIronOrders(frame, civ, can, atWar, allied, contacted);
        bool chosenDestination = _selectedTargetSettlement >= 0;
        string raidHint = chosenDestination
            ? "Sends raiders to this settlement. Hurts relations."
            : "Sends raiders to their closest settlement, even if it is out of sight. Hurts relations.";
        SetOrder(_raidRow!, true, can && contacted && war02 && !allied && !truce && !raidQueued,
            raidQueued ? "Raid queued; it starts when you resume." : !war02 ? $"Needs {NeedsName(_war02)} (research)." : !contacted ? "Meet them first." : allied ? "You can't raid an ally." : truce ? "Not during a truce." :
            raidHint);
        SetOrder(_warRow!, !atWar, can && contacted && !atWar && !allied,
            !contacted ? "Meet them first." : allied ? "End the alliance first." : "Breaks any truce and harms your reputation. Lets you assault.");
        bool rememberedDestination = chosenDestination && IsRememberedDestination(frame, civ, _selectedTargetSettlement);
        SetOrder(_assaultRow!, true, can && atWar && !assaultQueued && !assaultInFlight && !ownOccupation,
            assaultQueued ? "Assault queued; it starts when you resume." : !atWar ? "Declare war first." : assaultInFlight ? "An assault against them is already traveling." : ownOccupation ? "Your people are occupying one of their settlements. Another assault can launch once the occupation ends." : !chosenDestination ? "Your civilization chooses its closest campaign target. Choose a settlement above to direct it." :
            rememberedDestination ? "Marches to its last known site; it may be empty or its population may have changed." : "Tries to take the selected settlement.");
    }

    private void RefreshParties(MatchFrame frame)
    {
        int ordinal = 0;
        for (int i = 0; i < frame.PartyCount; i++)
        {
            if (frame.PartyCiv[i] != frame.ViewerCiv) continue;
            int slot = ordinal;
            if (ordinal >= _partyRows.Count)
            {
                var row = UiKit.Row(UiTokens.Sp2);
                var text = UiKit.Text("", "Body", ellipsis: true);
                text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                var bar = new UiProgressBar(6) { CustomMinimumSize = new Vector2(89, 6), SizeFlagsHorizontal = Control.SizeFlags.Fill };
                var recall = UiKit.Ghost("Recall", null, "Survivors head home.", UiGlyph.None, 30);
                int captured = ordinal;
                recall.Pressed += () => { if (_session is not null && _current is not null) QueueCommand(PlayerCommands.RecallParty(_session.Tick, 0, captured)); };
                row.AddChild(text);
                row.AddChild(bar);
                row.AddChild(recall);
                _partyList!.AddChild(row);
                _partyRows.Add((row, text, bar, recall));
            }
            (HBoxContainer root, Label label, UiProgressBar progress, UiButton _) = _partyRows[slot];
            root.Visible = true;
            string kind = ((PartyKind)frame.PartyKind[i]) switch { PartyKind.Gift => "Gift", PartyKind.Barter => "Barter", PartyKind.Raid => "Raid", _ => "Assault" };
            int permille = frame.PartyProgress[i];
            string phase = frame.PartyLeg[i] == 1 ? "on the way home" : permille < 340 ? "setting out" : permille < 670 ? "halfway" : "nearly there";
            UiKit.SetText(label, $"{kind} · {frame.PartyMembers[i]} people · {phase}");
            progress.SetValue(permille / 1000f, false);
            ordinal++;
        }
        for (int i = ordinal; i < _partyRows.Count; i++) _partyRows[i].Root.Visible = false;
        int pendingOrdinal = 0;
        void AddPending(string text)
        {
            if (pendingOrdinal >= _pendingPartyRows.Count)
            {
                Label pending = UiKit.Text("", "CaptionAccent", wrap: true);
                _partyList!.AddChild(pending);
                _pendingPartyRows.Add(pending);
            }
            Label label = _pendingPartyRows[pendingOrdinal++];
            label.Visible = true;
            UiKit.SetText(label, $"{text} · queued until you resume");
        }

        if (_session is not null)
        {
            foreach (Command command in _session.PendingCommands)
            {
                if (command.Civ != frame.ViewerCiv) continue;
                switch (command.Kind)
                {
                    case CommandKind.SendGift:
                        AddPending($"Gift to {UiText.CivName(command.A)}");
                        break;
                    case CommandKind.SendBarter:
                        AddPending($"Barter with {UiText.CivName(command.A)}");
                        break;
                    case CommandKind.LaunchRaid:
                    {
                        int owner = command.A >= 0 ? SettlementOwner(frame, command.A) : -1;
                        AddPending(owner >= 0 ? $"Raid on {UiText.CivName(owner)}" : "Raid");
                        break;
                    }
                    case CommandKind.LaunchRaidAgainst:
                        AddPending($"Raid on {UiText.CivName(command.A)}");
                        break;
                    case CommandKind.LaunchAssault:
                    {
                        int owner = command.A == -1 ? command.B - 1 : SettlementOwner(frame, command.A);
                        AddPending(owner >= 0 ? $"Assault on {UiText.CivName(owner)}" : "Assault");
                        break;
                    }
                    case CommandKind.Blockade:
                    {
                        int owner = SettlementOwner(frame, command.A);
                        AddPending(owner >= 0 ? $"Blockade at {UiText.CivName(owner)}" : "Blockade");
                        break;
                    }
                    case CommandKind.RecallParty:
                        AddPending($"Recall order for party {command.A + 1}");
                        break;
                }
            }
        }
        for (int i = pendingOrdinal; i < _pendingPartyRows.Count; i++) _pendingPartyRows[i].Visible = false;
        UiKit.SetVisible(_diploNoParties!, ordinal == 0 && pendingOrdinal == 0);
        UiKit.SetText(_diploPartyCount!, ordinal + pendingOrdinal > 0 ? (ordinal + pendingOrdinal).ToString() : "");
        UiKit.SetVisible(_partyList!, ordinal + pendingOrdinal > 0);
    }

    // ---- orders ------------------------------------------------------------------------------

    private void QueueDiplomacyOrder(Func<long, int, int, Command> create)
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        QueueCommand(create(_session.Tick, 0, _selectedPoliticsCiv));
    }

    private void QueueGift()
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        QueueCommand(PlayerCommands.SendGift(_session.Tick, 0, _selectedPoliticsCiv, _selectedTargetSettlement));
    }

    private void QueueBarter()
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        QueueCommand(PlayerCommands.SendBarter(_session.Tick, 0, _selectedPoliticsCiv, _selectedTargetSettlement));
    }

    private void QueueRaid()
    {
        if (_session is null || _current is null || _selectedPoliticsCiv < 0) return;
        int partySize = _raidPartySize is null ? 4 : Math.Max(4, _raidPartySize.GetSelectedId());
        if (_selectedTargetSettlement >= 0)
            QueueCommand(PlayerCommands.LaunchRaid(_session.Tick, 0, _selectedTargetSettlement, partySize));
        else
            QueueCommand(PlayerCommands.LaunchRaidAgainst(_session.Tick, 0, _selectedPoliticsCiv, partySize));
    }

    /// <summary>The Iron chapter's three orders: a league with an ally, tributary terms in a war being won, a galley off a harbor chosen above.</summary>
    private void RefreshIronOrders(MatchFrame frame, int civ, bool can, bool atWar, bool allied, bool contacted)
    {
        bool iron = frame.Iron.Active;
        bool dip06 = HasProjectDone(frame, _dip06), dip07 = HasProjectDone(frame, _dip07), sea09 = HasProjectDone(frame, _sea09);
        bool leagueQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.FormLeague);
        bool tributeQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.ImposeTribute);
        bool blockadeQueued = HasPendingDiplomaticOrder(frame, civ, CommandKind.Blockade);
        SetOrder(_leagueRow!, iron && allied, can && contacted && allied && dip07 && !leagueQueued,
            leagueQueued ? "League proposal queued; it starts when you resume." : !dip07 ? $"Needs {NeedsName(_dip07)} (research)." : "Founds a league with this ally, or brings it into yours. Partners lend labor to each other's Wonders.");
        SetOrder(_tributeRow!, iron && atWar, can && atWar && dip06 && !tributeQueued,
            tributeQueued ? "Tribute terms queued; they are offered when you resume." : !dip06 ? $"Needs {NeedsName(_dip06)} (research)." : "Ends the war on tributary terms. Needs about one and a half times their strength; they hand over a tenth of their spare food each season and a quarter of their yearly Dominion.");
        bool galley = false;
        int galleyKind = -1;
        for (int k = 0; k < frame.Iron.WorkKinds; k++) if (LivingEconomyModelWorkId(k) == "galley") galleyKind = k;
        if (galleyKind >= 0)
            for (int w = 0; w < frame.SettlementCount; w++)
                if ((frame.SettlementFlags[w] & (ushort)SettlementViewFlags.Own) != 0 && frame.Iron.Works[w * frame.Iron.WorkKinds + galleyKind] > 0 && frame.Iron.Soldiers[w] >= 4) galley = true;
        bool chosen = _selectedTargetSettlement >= 0;
        SetOrder(_blockadeRow!, iron && atWar, can && atWar && sea09 && galley && chosen && !blockadeQueued,
            blockadeQueued ? "Blockade queued; it starts when you resume." : !sea09 ? $"Needs {NeedsName(_sea09)} (research)." : !galley ? "Needs a harbor town with a war galley and four soldiers to crew it." :
            !chosen ? "Choose one of their settlements above: a harbor you have seen." : "Stations a galley off that harbor and shuts its sea routes.");
    }

    private static string LivingEconomyModelWorkId(int kind) => CivSim.Sim.Economy.LivingEconomyModel.IronWorkId(kind);

    private void QueueBlockade()
    {
        if (_session is null || _selectedTargetSettlement < 0) return;
        QueueCommand(PlayerCommands.Blockade(_session.Tick, 0, _selectedTargetSettlement));
    }

    private void QueueAssault()
    {
        if (_session is null || _current is null) return;
        if (_selectedTargetSettlement >= 0)
            QueueCommand(PlayerCommands.LaunchAssault(_session.Tick, 0, _selectedTargetSettlement, _selectedPoliticsCiv));
        else
            QueueCommand(PlayerCommands.LaunchAssaultAgainst(_session.Tick, 0, _selectedPoliticsCiv));
    }
}
