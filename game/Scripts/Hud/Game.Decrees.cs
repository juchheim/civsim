using System;
using CivSim.Game.Ui;
using CivSim.Sim.Politics;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Mandate controls and a fog-fair forecast card for the six Bronze Decrees.</summary>
public partial class Game
{
    private PanelContainer? _decreePanel;
    private Label? _decreeMandate, _decreeHint, _decreeForecast;
    private UiButton? _decreeConfirm, _decreeFound, _decreeExplore, _decreeMine, _decreeCorvee, _decreeLevy;
    private OptionButton? _decreePartner, _decreeGood;
    private CheckButton? _decreeSell;
    private UiLayer? _decreeLayer;
    private bool _decreeOpen, _decreeMapMode;
    private DecreeKind _pendingDecree;
    private int _pendingTarget, _pendingExtra;

    private void CreateDecreePanel()
    {
        _decreePanel = UiKit.Panel("HudPanel", "DecreePanel");
        _decreePanel.Visible = false;
        var col = UiKit.Column(UiTokens.Sp2);
        var header = UiKit.Row(UiTokens.Sp2);
        header.AddChild(UiKit.Text("Decrees", "Heading"));
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close", () => ToggleDecrees(false), 36, 16));
        col.AddChild(header);
        _decreeMandate = UiKit.Text("Mandate · locked", "Body");
        col.AddChild(_decreeMandate);
        _decreeHint = UiKit.Text("Research KEY-3 to issue a Decree.", "Caption", wrap: true, minWidth: 280);
        col.AddChild(_decreeHint);
        col.AddChild(UiKit.Text("CHOOSE A PLACE", "EyebrowTertiary"));
        _decreeFound = UiKit.Secondary("Found a village", () => ArmDecree(DecreeKind.Found), "Choose a tile on the map.");
        _decreeExplore = UiKit.Secondary("Send an expedition", () => ArmDecree(DecreeKind.Expedition), "Choose a tile to explore.");
        _decreeMine = UiKit.Secondary("Open a mine", () => ArmDecree(DecreeKind.Mine), "Choose a known deposit.");
        col.AddChild(_decreeFound); col.AddChild(_decreeExplore); col.AddChild(_decreeMine);
        col.AddChild(UiKit.Text("DIPLOMACY", "EyebrowTertiary"));
        var selectors = UiKit.Row(UiTokens.Sp2);
        _decreePartner = new OptionButton { CustomMinimumSize = new Vector2(150, UiTokens.ControlM), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _decreeGood = new OptionButton { CustomMinimumSize = new Vector2(112, UiTokens.ControlM) };
        foreach (TradeGood good in new[] { TradeGood.Tin, TradeGood.Copper, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Food, TradeGood.Luxury })
            _decreeGood.AddItem(good.ToString(), (int)good);
        _decreeSell = new CheckButton { Text = "Sell", ButtonPressed = false, TooltipText = "Off: buy this good · On: sell this good" };
        selectors.AddChild(_decreePartner); selectors.AddChild(_decreeGood); selectors.AddChild(_decreeSell);
        col.AddChild(selectors);
        var actions = UiKit.Row(UiTokens.Sp2);
        _decreeLevy = UiKit.Secondary("Raise levy", QueueLevy, "Gather your host against the selected neighbour.");
        actions.AddChild(_decreeLevy);
        var compact = UiKit.Secondary("Propose compact", QueueCompact, "Offer a standing route for the selected good.");
        actions.AddChild(compact);
        col.AddChild(actions);
        _decreeCorvee = UiKit.Secondary("Corvée · capital", QueueCorvee, "Speed building at your first settlement.");
        col.AddChild(_decreeCorvee);
        _decreeForecast = UiKit.Text("", "Caption", wrap: true, minWidth: 280);
        col.AddChild(_decreeForecast);
        _decreeConfirm = UiKit.Primary("Confirm Decree", ConfirmDecree, "Issue the forecasted Decree.");
        _decreeConfirm.Visible = false;
        col.AddChild(_decreeConfirm);
        _decreePanel.AddChild(col);
        _hudRoot!.AddChild(_decreePanel);
        _decreeLayer = RegisterLayer("decrees", false, () => { ToggleDecrees(false); return true; });
    }

    private void LayoutDecreePanel()
    {
        if (_decreePanel is null) return;
        float m = Margin;
        float width = _compact ? 360 : 470;
        float top = m + TopBandHeight + 12;
        AnchorTo(_decreePanel, 1, 0, 1, 0, -m - width, top, -m, top + (_compact ? 460 : 500), Control.GrowDirection.Begin);
        _decreePanel.CustomMinimumSize = new Vector2(width, 0);
    }

    private void ToggleDecrees(bool? force = null)
    {
        if (_decreePanel is null) return;
        bool open = force ?? !_decreeOpen;
        if (open == _decreeOpen) return;
        _decreeOpen = open;
        _decreePanel.Visible = open;
        if (open) { _ui.Push(_decreeLayer!); if (_current is not null) RefreshDecrees(_current); }
        else { _ui.Remove(_decreeLayer!); _decreeMapMode = false; }
    }

    private void ArmDecree(DecreeKind kind)
    {
        if (_session is null || _current is null || !_session.CanIssueCommands) return;
        _pendingDecree = kind;
        _decreeMapMode = true;
        UiKit.SetText(_decreeHint!, $"Click a tile for {kind switch { DecreeKind.Found => "a village", DecreeKind.Expedition => "an expedition", _ => "a mine" }}. Right-click to cancel.");
        _decreeForecast!.Text = ""; _decreeConfirm!.Visible = false;
        Input.SetDefaultCursorShape(Input.CursorShape.Cross);
    }

    private void SelectDecreeTile(int tile)
    {
        _decreeMapMode = false;
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        _pendingTarget = tile; _pendingExtra = 0;
        ShowDecreeForecast();
    }

    private void ShowDecreeForecast()
    {
        if (_session is null || _current is null || !_decreeOpen) return;
        int civ = Math.Max(0, _current.ViewerCiv);
        DecreeForecast f = _session.PreviewDecree(civ, _pendingDecree, _pendingTarget, _pendingExtra);
        string result = f.Allowed
            ? $"Elders expect {f.SuccessPermille / 10.0:0.#}% success · {f.People} people · about {f.Tiles} tiles · {f.Seasons} seasons. Mandate {f.MandateAfter} after."
            : $"Elders expect refusal: {f.LikelyRefusal}. Nothing will be spent.";
        UiKit.SetText(_decreeForecast!, result);
        UiKit.SetVisible(_decreeConfirm!, f.Allowed);
        UiKit.SetText(_decreeConfirm!, $"Confirm · {f.Cost} Mandate");
    }

    private void ConfirmDecree()
    {
        if (_session is null || !_session.CanIssueCommands) return;
        int civ = Math.Max(0, _current?.ViewerCiv ?? 0);
        _session.Queue(PlayerCommands.Decree(_session.Tick, civ, _pendingDecree, _pendingTarget, _pendingExtra));
        _decreeConfirm!.Visible = false; _decreeForecast!.Text = "Decree queued for the end of this season.";
    }

    private void RefreshDecrees(MatchFrame frame)
    {
        if (_decreeMandate is null) return;
        bool available = frame.FocusMandateUnlocked;
        UiKit.SetText(_decreeMandate, available
            ? $"Mandate · {frame.FocusMandate}/{frame.FocusMandateCap} · next in {frame.FocusMandateNextSeason} seasons"
            : "Mandate · locked until KEY-3");
        UiKit.SetText(_decreeHint!, available ? $"{frame.FocusPendingDecrees} Decrees resolve at season's end." : "Research KEY-3 to issue a Decree.");
        foreach (UiButton? button in new[] { _decreeFound, _decreeExplore, _decreeMine, _decreeLevy, _decreeCorvee })
            if (button is not null) button.Disabled = !available || !(_session?.CanIssueCommands ?? false);
        if (_decreePartner is null) return;
        int selected = _decreePartner.Selected > 0 ? _decreePartner.GetItemId(_decreePartner.Selected) : -1;
        _decreePartner.Clear();
        int viewer = Math.Max(0, frame.ViewerCiv);
        for (int c = 0; c < frame.CivCount; c++)
            if (c != viewer && ((CivViewFlags)frame.CivFlags[c] & CivViewFlags.Contacted) != 0)
                _decreePartner.AddItem(UiText.CivName(c), c);
        int index = _decreePartner.GetItemIndex(selected);
        if (index >= 0) _decreePartner.Select(index);
        _decreeLevy!.Disabled = !available || selected < 0 || !(_session?.CanIssueCommands ?? false);
    }

    private void QueueLevy()
    {
        if (_current is null || _session is null || _decreePartner!.Selected < 0) return;
        int civ = _decreePartner.GetItemId(_decreePartner.Selected);
        int target = -1;
        for (int i = 0; i < _current.SettlementCount; i++)
            if (_current.SettlementCiv[i] == civ && (_current.SettlementFlags[i] & (ushort)(SettlementViewFlags.Remembered | SettlementViewFlags.Empty)) == 0)
            { target = _current.SettlementIndex[i]; break; }
        if (target >= 0) PreviewAndQueue(DecreeKind.Levy, target, 0);
    }

    private void QueueCorvee()
    {
        if (_current is null) return;
        int target = -1;
        for (int i = 0; i < _current.SettlementCount; i++)
            if (_current.SettlementCiv[i] == _current.ViewerCiv && (_current.SettlementFlags[i] & (ushort)SettlementViewFlags.Empty) == 0)
            { target = _current.SettlementIndex[i]; break; }
        if (target >= 0) PreviewAndQueue(DecreeKind.Corvee, target, 0);
    }

    private void QueueCompact()
    {
        if (_decreePartner!.Selected < 0) return;
        int civ = _decreePartner.GetItemId(_decreePartner.Selected);
        TradeGood good = (TradeGood)_decreeGood!.GetItemId(_decreeGood.Selected);
        PreviewAndQueue(DecreeKind.Compact, civ, DecreeArgs.PackCompact(good, _decreeSell!.ButtonPressed));
    }

    private void QueueSelectedCompact()
    {
        if (_current is null || _selectedPoliticsCiv < 0 || _diploCompactGood is null || _diploCompactSell is null) return;
        ToggleDecrees(true);
        TradeGood good = (TradeGood)_diploCompactGood.GetItemId(_diploCompactGood.Selected);
        PreviewAndQueue(DecreeKind.Compact, _selectedPoliticsCiv, DecreeArgs.PackCompact(good, _diploCompactSell.ButtonPressed));
    }

    private string CompactAcceptanceHint(int civ, int partner)
    {
        if (_session is null || _diploCompactGood is null || _diploCompactSell is null || _diploCompactGood.Selected < 0)
            return "Likely to accept: unknown (no safe estimate available).";
        TradeGood good = (TradeGood)_diploCompactGood.GetItemId(_diploCompactGood.Selected);
        int extra = DecreeArgs.PackCompact(good, _diploCompactSell.ButtonPressed);
        DecreeForecast forecast = _session.PreviewDecree(civ, DecreeKind.Compact, partner, extra);
        if (!forecast.Allowed)
            return $"Likely to accept: unknown ({forecast.LikelyRefusal}).";
        string likelihood = forecast.SuccessPermille >= 800 ? "likely" : forecast.SuccessPermille >= 500 ? "possible" : "unlikely";
        return $"Likely to accept: {likelihood} · about {forecast.SuccessPermille / 10.0:0.#}% from known information.";
    }

    private void ToggleSelectedEmbargo()
    {
        if (_current is null || _session is null || !_session.CanIssueCommands || _selectedPoliticsCiv < 0 || _diploCompactGood is null) return;
        TradeGood good = (TradeGood)_diploCompactGood.GetItemId(_diploCompactGood.Selected);
        byte mask = _current.CivEmbargoByViewer[_selectedPoliticsCiv];
        bool on = (mask & (1 << (int)good)) == 0;
        _session.Queue(PlayerCommands.SetEmbargo(_session.Tick, Math.Max(0, _current.ViewerCiv), _selectedPoliticsCiv, good, on));
        ShowToast(on ? "Embargo set" : "Embargo lifted", ToastKind.Info, $"{good} trade with {UiText.CivName(_selectedPoliticsCiv)}.", key: "embargo");
    }

    private void PreviewAndQueue(DecreeKind kind, int target, int extra)
    {
        _pendingDecree = kind; _pendingTarget = target; _pendingExtra = extra;
        ShowDecreeForecast();
    }
}
