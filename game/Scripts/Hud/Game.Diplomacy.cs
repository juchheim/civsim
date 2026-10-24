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
    private Control? _diplomacyDetail, _diplomacyEmpty, _friendlyBlock, _hostileBlock, _partiesBlock;
    private Label? _diploEmptyLabel, _diploName, _diploMeta, _diploAi, _diploNoParties;
    private UiEmblem? _diploEmblem;
    private OptionButton? _diploCompactGood;
    private CheckButton? _diploCompactSell;
    private UiButton? _diploCompactAction, _diploEmbargoAction;
    private PanelContainer? _diploStatusChip, _diploRelationChip, _diploAgeChip;
    private HFlowContainer? _destinationChips;
    private readonly List<UiButton> _destinationPool = new();
    private OrderRow? _giftRow, _barterRow, _allianceRow, _peaceRow, _raidRow, _warRow, _assaultRow;
    private VBoxContainer? _partyList;
    private readonly List<(HBoxContainer Root, Label Text, UiProgressBar Bar, UiButton Recall)> _partyRows = new();
    private int _selectedPoliticsCiv = -1, _selectedTargetSettlement = -1;
    private int _war02 = -1, _trd01 = -1, _trd02 = -1, _dip03 = -1;
    private readonly List<int> _civOrder = new();

    private void CreateDiplomacyDrawer()
    {
        if (_catalog is not null)
        {
            _war02 = _catalog.IndexOf("WAR-02");
            _trd01 = _catalog.IndexOf("TRD-01");
            _trd02 = _catalog.IndexOf("TRD-02");
            _dip03 = _catalog.IndexOf("DIP-03");
        }
        _diplomacyDrawer = UiKit.Panel("HudPanel", "DiplomacyDrawer");
        _diplomacyDrawer.Visible = false;
        var root = UiKit.Column(UiTokens.Sp2);
        var header = UiKit.Row(UiTokens.Sp2);
        header.AddChild(UiKit.Text("Diplomacy", "Heading"));
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (O)", () => ToggleDiplomacy(false), 36, 16));
        root.AddChild(header);

        _diploEmptyLabel = UiKit.Text("", "BodySecondary", wrap: true);
        root.AddChild(_diploEmptyLabel);

        var body = UiKit.Row(UiTokens.Sp3);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _civList = UiKit.Column(UiTokens.Sp1);
        var listScroll = UiKit.Scroll(_civList);
        listScroll.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        listScroll.CustomMinimumSize = new Vector2(199, 0);
        body.AddChild(listScroll);

        var detail = UiKit.Column(UiTokens.Sp2);
        _diplomacyDetail = detail;
        var detailScroll = UiKit.Scroll(detail);
        detailScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(detailScroll);
        root.AddChild(body);
        _diplomacyEmpty = _diploEmptyLabel;

        var title = UiKit.Row(UiTokens.Sp2);
        _diploEmblem = new UiEmblem(0, 34);
        title.AddChild(_diploEmblem);
        _diploName = UiKit.Text("", "Title", ellipsis: true);
        title.AddChild(_diploName);
        detail.AddChild(title);
        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        chips.AddThemeConstantOverride("v_separation", UiTokens.Sp1);
        _diploStatusChip = UiKit.Chip("CONTACTED");
        _diploRelationChip = UiKit.Chip("NEUTRAL");
        _diploAgeChip = UiKit.Chip("DAWN AGE");
        chips.AddChild(_diploStatusChip);
        chips.AddChild(_diploRelationChip);
        chips.AddChild(_diploAgeChip);
        detail.AddChild(chips);
        _diploMeta = UiKit.Text("", "Caption");
        detail.AddChild(_diploMeta);
        _diploAi = UiKit.Text("", "CaptionTertiary", wrap: true);
        detail.AddChild(_diploAi);

        detail.AddChild(UiKit.Text("DESTINATION", "EyebrowTertiary"));
        _destinationChips = new HFlowContainer();
        _destinationChips.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        _destinationChips.AddThemeConstantOverride("v_separation", UiTokens.Sp2);
        detail.AddChild(_destinationChips);

        var friendly = UiKit.Column(UiTokens.Sp2);
        friendly.AddChild(UiKit.Text("FRIENDLY", "EyebrowTertiary"));
        _giftRow = NewOrderRow("Send gift", QueueGift, false, "");
        _barterRow = NewOrderRow("Barter", QueueBarter, false, "");
        _allianceRow = NewOrderRow("Form alliance", () => QueueDiplomacyOrder(PlayerCommands.FormAlliance), false, "");
        _peaceRow = NewOrderRow("Make peace", () => QueueDiplomacyOrder(PlayerCommands.MakePeace), false, "");
        foreach (OrderRow row in new[] { _giftRow, _barterRow, _allianceRow, _peaceRow }) friendly.AddChild(row.Root);
        var compactControls = UiKit.Row(UiTokens.Sp2);
        _diploCompactGood = new OptionButton { CustomMinimumSize = new Vector2(130, UiTokens.ControlM) };
        foreach (TradeGood good in new[] { TradeGood.Tin, TradeGood.Copper, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Food, TradeGood.Luxury })
            _diploCompactGood.AddItem(good.ToString(), (int)good);
        _diploCompactSell = new CheckButton { Text = "Sell", TooltipText = "Off: buy this good · On: sell this good" };
        _diploCompactAction = UiKit.Secondary("Compact", QueueSelectedCompact, "Offer a standing goods route to this people.");
        compactControls.AddChild(_diploCompactGood); compactControls.AddChild(_diploCompactSell); compactControls.AddChild(_diploCompactAction);
        friendly.AddChild(compactControls);
        var embargoControls = UiKit.Row(UiTokens.Sp2);
        _diploEmbargoAction = UiKit.Secondary("Toggle embargo", ToggleSelectedEmbargo, "Stop or resume selling the selected good to this people.");
        embargoControls.AddChild(_diploEmbargoAction);
        friendly.AddChild(embargoControls);
        detail.AddChild(friendly);
        _friendlyBlock = friendly;

        var hostile = UiKit.Column(UiTokens.Sp2);
        hostile.AddChild(UiKit.Text("HOSTILE", "EyebrowTertiary"));
        _raidRow = NewOrderRow("Raid", QueueRaid, true, "Confirm raid");
        _warRow = NewOrderRow("Declare war", () => QueueDiplomacyOrder(PlayerCommands.DeclareWar), true, "Confirm war");
        _assaultRow = NewOrderRow("Assault", QueueAssault, true, "Confirm assault");
        foreach (OrderRow row in new[] { _raidRow, _warRow, _assaultRow }) hostile.AddChild(row.Root);
        detail.AddChild(hostile);
        _hostileBlock = hostile;

        var parties = UiKit.Column(UiTokens.Sp2);
        parties.AddChild(UiKit.Text("YOUR PARTIES", "EyebrowTertiary"));
        _partyList = UiKit.Column(UiTokens.Sp1);
        parties.AddChild(_partyList);
        _diploNoParties = UiKit.Text("No parties on the road.", "CaptionTertiary");
        parties.AddChild(_diploNoParties);
        detail.AddChild(parties);
        _partiesBlock = parties;

        _diplomacyDrawer.AddChild(root);
        _hudRoot!.AddChild(_diplomacyDrawer);
        _diplomacyLayer = RegisterLayer("diplomacy", false, () => { ToggleDiplomacy(false); return true; });
    }

    private OrderRow NewOrderRow(string label, Action act, bool confirm, string confirmLabel)
    {
        var row = new OrderRow { Label = label, Act = act, NeedsConfirm = confirm, ConfirmLabel = confirmLabel };
        row.Root = UiKit.Row(UiTokens.Sp3);
        row.Button = UiKit.Secondary(label, null, null, UiGlyph.None, UiTokens.ControlM, true);
        row.Button.CustomMinimumSize = new Vector2(144, UiTokens.ControlM);
        row.Button.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.Button.Pressed += () => PressOrder(row);
        row.Root.AddChild(row.Button);
        row.Lock = new UiIcon(UiGlyph.Lock, 16, UiTokens.TextTertiary) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        row.Root.AddChild(row.Lock);
        row.Text = UiKit.Text("", "Caption", wrap: true);
        row.Text.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.Root.AddChild(row.Text);
        return row;
    }

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
        foreach (OrderRow? row in new[] { _raidRow, _warRow, _assaultRow })
        {
            if (row is null || !row.Confirming) continue;
            row.Timer -= delta;
            if (row.Timer <= 0) CancelConfirm(row);
        }
    }

    private void LayoutDiplomacyDrawer(float top)
    {
        if (_diplomacyDrawer is null) return;
        float m = Margin;
        float width = _compact ? 520 : 610;
        float height = Math.Min(_compact ? 560 : 664, _logicalSize.Y - top - m - 60);
        AnchorTo(_diplomacyDrawer, 1, 0, 1, 0, -m - width, top, -m, top + height, Control.GrowDirection.Begin);
        _drawerFullHeight = height;
        _diplomacyDrawer.CustomMinimumSize = new Vector2(width, _civOrder.Count > 0 ? height : 0);
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
            if (_chronicleOpen) ToggleChronicle(false);
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
        _diplomacyDrawer!.CustomMinimumSize = new Vector2(_diplomacyDrawer.CustomMinimumSize.X, _civOrder.Count > 0 ? _drawerFullHeight : 0);
        if (_civOrder.Count == 0) _diplomacyDrawer.ResetSize();
        _civList.GetParent<Control>().Visible = _civOrder.Count > 0;
        _diplomacyDetail!.GetParent<Control>().Visible = _civOrder.Count > 0 && own;
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
        bool contacted = (flags & CivViewFlags.Contacted) != 0, atWar = (flags & CivViewFlags.AtWar) != 0;
        bool allied = (flags & CivViewFlags.Allied) != 0, truce = (flags & CivViewFlags.InTruce) != 0;
        if (_diploCompactAction is not null)
            _diploCompactAction.Disabled = !(_session?.CanIssueCommands ?? false) || !frame.FocusMandateUnlocked || atWar || !contacted;
        if (_diploEmbargoAction is not null)
            _diploEmbargoAction.Disabled = !(_session?.CanIssueCommands ?? false) || !contacted;
        _diploEmblem!.Civ = target;
        UiKit.SetText(_diploName!, UiText.CivName(target));
        (string status, string variation) = atWar ? ("AT WAR", "ChipPanelDanger") : allied ? ("ALLIED", "ChipPanelSuccess") : truce ? ("IN TRUCE", "ChipPanelInfo") : contacted ? ("CONTACTED", "ChipPanel") : ("KNOWN ONLY", "ChipPanel");
        UiKit.SetChipText(_diploStatusChip!, status);
        UiKit.SetVariation(_diploStatusChip!, variation);
        int relations = frame.CivRelations[target];
        UiKit.SetChipText(_diploRelationChip!, contacted ? $"{RelationWord(relations).ToUpperInvariant()} {relations:+0;−0;0}" : "NO CONTACT YET");
        UiKit.SetChipText(_diploAgeChip!, UiText.AgeName(frame.CivEra[target]).ToUpperInvariant() + " AGE");
        int seen = 0;
        for (int i = 0; i < frame.SettlementCount; i++)
            if (frame.SettlementCiv[i] == target && (frame.SettlementFlags[i] & (ushort)(SettlementViewFlags.FreeFolk | SettlementViewFlags.Empty)) == 0) seen++;
        UiKit.SetText(_diploMeta!, $"{seen} {(seen == 1 ? "settlement" : "settlements")} seen");
        string ai = "";
        if (contacted)
            for (int i = frame.AiWhyCount - 1; i >= 0; i--)
                if (frame.AiWhyEntries[i].Civ == target) { ai = $"Their elders: “{frame.AiWhyEntries[i].Reason}”"; break; }
        UiKit.SetText(_diploAi!, ai);
        UiKit.SetVisible(_diploAi!, ai.Length > 0);

        RefreshDestinations(frame, target);
        RefreshOrders(frame, target, contacted, atWar, allied, truce, relations);
        RefreshParties(frame);
    }

    private static int RelationRank(MatchFrame frame, int civ)
    {
        CivViewFlags f = (CivViewFlags)frame.CivFlags[civ];
        return (f & CivViewFlags.AtWar) != 0 ? 0 : (f & CivViewFlags.Allied) != 0 ? 1 : (f & CivViewFlags.Contacted) != 0 ? 2 : 3;
    }

    private string CivStatusText(MatchFrame frame, int civ)
    {
        CivViewFlags f = (CivViewFlags)frame.CivFlags[civ];
        string state = (f & CivViewFlags.AtWar) != 0 ? "At war" : (f & CivViewFlags.Allied) != 0 ? "Allied" : (f & CivViewFlags.InTruce) != 0 ? "Truce"
            : (f & CivViewFlags.Contacted) != 0 ? RelationWord(frame.CivRelations[civ]) : "Seen from afar";
        return $"{state} · {UiText.AgeName(frame.CivEra[civ])}";
    }

    private void RefreshDestinations(MatchFrame frame, int civ)
    {
        var options = new List<(int Settlement, string Text)> { (-1, "Nearest") };
        for (int i = 0; i < frame.SettlementCount && options.Count < 9; i++)
        {
            if (frame.SettlementCiv[i] != civ) continue;
            if ((frame.SettlementFlags[i] & (ushort)(SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk | SettlementViewFlags.Empty)) != 0) continue;
            options.Add((frame.SettlementIndex[i], $"Settlement {frame.SettlementIndex[i] + 1} · {frame.SettlementPopulation[i]}"));
        }
        bool selectedStillThere = _selectedTargetSettlement < 0;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Settlement == _selectedTargetSettlement) selectedStillThere = true;
            UiButton chip;
            if (i >= _destinationPool.Count)
            {
                int captured = i;
                chip = (UiButton)UiKit.InteractiveChip("", UiGlyph.None, () => { });
                chip.Pressed += () =>
                {
                    if (chip.GetMeta("settlement").AsInt32() is int s) _selectedTargetSettlement = s;
                    if (_current is not null) RefreshDiplomacy(_current);
                };
                _destinationPool.Add(chip);
                _destinationChips!.AddChild(chip);
                _ = captured;
            }
            chip = _destinationPool[i];
            chip.Visible = true;
            chip.Text = options[i].Text;
            chip.SetMeta("settlement", options[i].Settlement);
            chip.TooltipText = options[i].Settlement < 0 ? "The closest pair of settlements" : "";
            chip.SetPressedNoSignal(options[i].Settlement == _selectedTargetSettlement);
        }
        for (int i = options.Count; i < _destinationPool.Count; i++) _destinationPool[i].Visible = false;
        if (!selectedStillThere) _selectedTargetSettlement = -1;
    }

    private bool HasProjectDone(MatchFrame frame, int project)
    {
        if (project < 0 || project >= frame.FocusProjectCount) return false;
        return (ProjectState)frame.FocusProjectState[project] is ProjectState.Completed or ProjectState.Dormant;
    }

    private string NeedsName(int project) => project >= 0 && _catalog is not null ? _catalog.Projects[project].Name : "a research project";

    /// <summary>The visible settlement of <paramref name="civ"/> to raid or assault: the chosen one, or the one nearest to your own.</summary>
    private int ResolveDestination(MatchFrame frame, int civ)
    {
        if (_selectedTargetSettlement >= 0) return _selectedTargetSettlement;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if (frame.SettlementCiv[i] != civ) continue;
            if ((frame.SettlementFlags[i] & (ushort)(SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk | SettlementViewFlags.Empty)) != 0) continue;
            int tile = frame.SettlementTile[i];
            var a = new Vector2(tile % frame.Width, tile / frame.Width);
            for (int j = 0; j < frame.SettlementCount; j++)
            {
                if (frame.SettlementCiv[j] != Math.Max(0, frame.ViewerCiv)) continue;
                int own = frame.SettlementTile[j];
                float d = a.DistanceTo(new Vector2(own % frame.Width, own / frame.Width));
                if (d < bestDistance) { bestDistance = d; best = frame.SettlementIndex[i]; }
            }
        }
        return best;
    }

    private void SetOrder(OrderRow row, bool visible, bool enabled, string text)
    {
        UiKit.SetVisible(row.Root, visible);
        row.Enabled = enabled;
        row.Button.Disabled = !enabled;
        row.Lock.Visible = !enabled;
        UiKit.SetText(row.Text, text);
        row.Text.ThemeTypeVariation = enabled ? "Caption" : "CaptionTertiary";
        if (!enabled) CancelConfirm(row);
    }

    private void RefreshOrders(MatchFrame frame, int civ, bool contacted, bool atWar, bool allied, bool truce, int relations)
    {
        bool trd01 = HasProjectDone(frame, _trd01), trd02 = HasProjectDone(frame, _trd02), war02 = HasProjectDone(frame, _war02), dip03 = HasProjectDone(frame, _dip03);
        bool barterInFlight = false;
        for (int i = 0; i < frame.PartyCount; i++)
            if (frame.PartyCiv[i] == frame.ViewerCiv && frame.PartyTargetCiv[i] == civ && frame.PartyKind[i] == (byte)PartyKind.Barter) barterInFlight = true;
        int destination = ResolveDestination(frame, civ);
        bool hasDestination = destination >= 0;
        bool can = _session?.CanIssueCommands ?? false;
        if (_diploCompactAction is not null) _diploCompactAction.TooltipText = CompactAcceptanceHint(frame.ViewerCiv, civ);

        SetOrder(_giftRow!, true, can && contacted && !atWar && trd01,
            !trd01 ? $"Needs {NeedsName(_trd01)} (research)." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." : "Carries food to them. Improves relations.");
        SetOrder(_barterRow!, true, can && contacted && !atWar && trd02 && relations > 0 && !barterInFlight,
            !trd02 ? $"Needs {NeedsName(_trd02)} (research)." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." :
            relations <= 0 ? $"Needs warmer relations (above 0; now {relations})." : barterInFlight ? "A barter trip is already on the way." : "Trades your surplus for theirs.");
        SetOrder(_allianceRow!, !allied, can && contacted && !atWar && !allied && relations > 40 && dip03,
            !dip03 ? $"Needs {NeedsName(_dip03)}: both of you must know it." : !contacted ? "Meet them first." : atWar ? "Not while you're at war." :
            relations <= 40 ? $"Needs warmer relations (above 40; now {relations})." : "You defend each other.");
        bool occupation = false;
        for (int i = 0; i < frame.OccupationCount; i++)
            for (int s = 0; s < frame.SettlementCount; s++)
                if (frame.SettlementIndex[s] == frame.OccupationSettlement[i] &&
                    (frame.OccupationCiv[i] == frame.ViewerCiv && frame.SettlementCiv[s] == civ || frame.OccupationCiv[i] == civ && frame.SettlementCiv[s] == frame.ViewerCiv))
                    occupation = true;
        SetOrder(_peaceRow!, atWar, can && atWar && !occupation, occupation ? "Not while you hold one of their settlements." : "Ends the war. A truce follows.");

        SetOrder(_raidRow!, true, can && contacted && war02 && !allied && !truce && hasDestination,
            !war02 ? $"Needs {NeedsName(_war02)} (research)." : !contacted ? "Meet them first." : allied ? "You can't raid an ally." : truce ? "Not during a truce." :
            !hasDestination ? "Pick one of their settlements you can see." : "Sends 4 raiders to take stores. Hurts relations.");
        SetOrder(_warRow!, !atWar, can && contacted && !atWar && !allied,
            !contacted ? "Meet them first." : allied ? "End the alliance first." : "Breaks any truce and harms your reputation. Lets you assault.");
        SetOrder(_assaultRow!, true, can && atWar && hasDestination,
            !atWar ? "Declare war first." : !hasDestination ? "Pick one of their settlements you can see." : "Tries to take the settlement.");
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
                var bar = new UiProgressBar(6) { CustomMinimumSize = new Vector2(89, 6), SizeFlagsHorizontal = Control.SizeFlags.Fill };
                var recall = UiKit.Ghost("Recall", null, "Survivors head home.", UiGlyph.None, 30);
                int captured = ordinal;
                recall.Pressed += () => { if (_session is not null && _current is not null) _session.Queue(PlayerCommands.RecallParty(_session.Tick, 0, captured)); };
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
        UiKit.SetVisible(_diploNoParties!, ordinal == 0);
    }

    // ---- orders ------------------------------------------------------------------------------

    private void QueueDiplomacyOrder(Func<long, int, int, Command> create)
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        _session.Queue(create(_session.Tick, 0, _selectedPoliticsCiv));
    }

    private void QueueGift()
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        _session.Queue(PlayerCommands.SendGift(_session.Tick, 0, _selectedPoliticsCiv, _selectedTargetSettlement));
    }

    private void QueueBarter()
    {
        if (_session is null || _selectedPoliticsCiv < 0) return;
        _session.Queue(PlayerCommands.SendBarter(_session.Tick, 0, _selectedPoliticsCiv, _selectedTargetSettlement));
    }

    private void QueueRaid()
    {
        if (_session is null || _current is null) return;
        int destination = ResolveDestination(_current, _selectedPoliticsCiv);
        if (destination >= 0) _session.Queue(PlayerCommands.LaunchRaid(_session.Tick, 0, destination, 4));
    }

    private void QueueAssault()
    {
        if (_session is null || _current is null) return;
        int destination = ResolveDestination(_current, _selectedPoliticsCiv);
        if (destination >= 0) _session.Queue(PlayerCommands.LaunchAssault(_session.Tick, 0, destination));
    }
}
