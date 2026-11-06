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

    private VBoxContainer? _decreeBody;
    private Control? _decreeLockedBlock, _decreeControls;
    private Label? _decreeLockedLead;
    private bool _decreesUnlocked;

    private void CreateDecreePanel()
    {
        _decreePanel = UiKit.Panel("HudPanel", "DecreePanel");
        _decreePanel.Visible = false;
        var root = UiKit.Column(UiTokens.Sp2);
        var header = UiKit.Row(UiTokens.Sp2);
        header.AddChild(UiKit.Text("Decrees", "Heading"));
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close", () => ToggleDecrees(false), 36, 16));
        root.AddChild(header);
        var col = UiKit.Column(UiTokens.Sp2);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _decreeBody = col;
        _decreeMandate = UiKit.Text("Mandate · locked", "Body", wrap: true);
        col.AddChild(_decreeMandate);
        _decreeHint = UiKit.Text($"Reach {DecreeUnlockProjectName()} to issue a Decree.", "Caption", wrap: true);
        col.AddChild(_decreeHint);

        // Locked: say what Decrees will do instead of showing a column of disabled buttons and an empty list (ui-ux-pass2 §5).
        var locked = UiKit.Column(UiTokens.Sp2);
        _decreeLockedBlock = locked;
        locked.AddChild(UiKit.Text("WHAT DECREES WILL LET YOU DO", "EyebrowTertiary"));
        _decreeLockedLead = UiKit.Text("", "BodySecondary", wrap: true);
        foreach (string line in new[]
                 {
                     "Found a village, send an expedition or open a mine on a tile you pick.",
                     "Raise a levy against a neighbour, or propose a trade compact.",
                     "Call a corvée to speed building at your capital.",
                 })
        {
            var row = UiKit.Row(UiTokens.Sp2);
            row.AddChild(new UiIcon(UiGlyph.Seal, 16, UiTokens.TextTertiary) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin });
            Label text = UiKit.Text(line, "BodySecondary", wrap: true);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(text);
            locked.AddChild(row);
        }
        locked.AddChild(UiKit.Text("Each Decree spends Mandate, which builds up over the seasons.", "CaptionTertiary", wrap: true));
        col.AddChild(locked);

        var controls = UiKit.Column(UiTokens.Sp2);
        _decreeControls = controls;
        controls.AddChild(UiKit.Text("CHOOSE A PLACE", "EyebrowTertiary"));
        _decreeFound = UiKit.Secondary("Found a village", () => ArmDecree(DecreeKind.Found), "Choose a tile on the map.");
        _decreeExplore = UiKit.Secondary("Send an expedition", () => ArmDecree(DecreeKind.Expedition), "Choose a tile to explore.");
        _decreeMine = UiKit.Secondary("Open a mine", () => ArmDecree(DecreeKind.Mine), "Choose a known deposit.");
        controls.AddChild(_decreeFound); controls.AddChild(_decreeExplore); controls.AddChild(_decreeMine);
        controls.AddChild(UiKit.Text("DIPLOMACY", "EyebrowTertiary"));
        // Neighbour on its own line, then good and direction: the three side by side did not fit the narrow dock.
        _decreePartner = new OptionButton { CustomMinimumSize = new Vector2(0, UiTokens.ControlM), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = "The neighbour for a levy or a compact." };
        controls.AddChild(_decreePartner);
        var selectors = UiKit.Row(UiTokens.Sp2);
        _decreeGood = new OptionButton { CustomMinimumSize = new Vector2(112, UiTokens.ControlM), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (TradeGood good in new[] { TradeGood.Tin, TradeGood.Copper, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Food, TradeGood.Luxury })
            _decreeGood.AddItem(good.ToString(), (int)good);
        _decreeSell = new CheckButton { Text = "Sell", ButtonPressed = false, TooltipText = "Off: buy this good · On: sell this good" };
        selectors.AddChild(_decreeGood); selectors.AddChild(_decreeSell);
        controls.AddChild(selectors);
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", UiTokens.Sp2);
        actions.AddThemeConstantOverride("v_separation", UiTokens.Sp2);
        _decreeLevy = UiKit.Secondary("Raise levy", QueueLevy, "Gather your host against the selected neighbour.");
        actions.AddChild(_decreeLevy);
        _decreeCompact = UiKit.Secondary("Propose compact", QueueCompact, "Offer a standing route for the selected good.");
        actions.AddChild(_decreeCompact);
        controls.AddChild(actions);
        controls.AddChild(UiKit.Text("AT HOME", "EyebrowTertiary"));
        _decreeCorvee = UiKit.Secondary("Corvée · capital", QueueCorvee, "Speed building at your first settlement.");
        controls.AddChild(_decreeCorvee);
        col.AddChild(controls);
        _decreeForecast = UiKit.Text("", "Caption", wrap: true);
        col.AddChild(_decreeForecast);
        _decreeConfirm = UiKit.Primary("Confirm Decree", ConfirmDecree, "Issue the forecasted Decree.");
        _decreeConfirm.Visible = false;
        col.AddChild(_decreeConfirm);
        ScrollContainer scroll = UiKit.Scroll(col);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(scroll);
        _decreePanel.AddChild(root);
        _hudRoot!.AddChild(_decreePanel);
        _decreeLayer = RegisterLayer("decrees", false, () => { ToggleDecrees(false); return true; });
    }

    private UiButton? _decreeCompact;

    private void LayoutDecreePanel()
    {
        if (_decreePanel is null) return;
        FitDecreePanel();
    }

    /// <summary>The Decrees panel is as tall as what it holds, up to the dock's full height (then it scrolls).</summary>
    private void FitDecreePanel()
    {
        if (_decreePanel is null || _decreeBody is null || _decreeBody.GetParent() is not Control scroll) return;
        // Measured from the laid-out sizes (wrapped labels only know their height once they know their width): the panel's
        // chrome (header, padding) plus the body's height, capped at the dock. Refreshed while open, so it settles in a frame.
        float chrome = _decreePanel.Size.Y - scroll.Size.Y;
        float content = chrome + _decreeBody.GetCombinedMinimumSize().Y;
        float height = MathF.Ceiling(Math.Min(content, DockMaxHeight));
        if (Math.Abs(_decreePanel.CustomMinimumSize.Y - height) > 0.5f || Math.Abs(_decreePanel.CustomMinimumSize.X - DockNarrowWidth) > 0.5f)
        {
            PlaceDock(_decreePanel, DockNarrowWidth, height);
            _decreePanel.ResetSize();
        }
    }

    private void ToggleDecrees(bool? force = null)
    {
        if (_decreePanel is null) return;
        bool open = force ?? !_decreeOpen;
        if (open == _decreeOpen) return;
        if (open) CloseOtherDocks("decrees");
        _decreeOpen = open;
        _decreePanel.Visible = open;
        if (open) { _ui.Push(_decreeLayer!); if (_current is not null) RefreshDecrees(_current); CallDeferred(nameof(FitDecreePanel)); }
        else { _ui.Remove(_decreeLayer!); _decreeMapMode = false; }
        LayoutNotifications();
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
            : $"The elders advise against it. {MineRefusalDetail(_pendingDecree, (int)f.LikelyRefusal, _pendingTarget) ?? UiText.DecreeRefusal(_pendingDecree, (int)f.LikelyRefusal)}{FoundRefusalDetail(_pendingDecree, f.LikelyRefusal, civ, _pendingTarget)} Nothing will be spent.";
        UiKit.SetText(_decreeForecast!, result);
        UiKit.SetVisible(_decreeConfirm!, f.Allowed);
        UiKit.SetText(_decreeConfirm!, $"Confirm · {f.Cost} Mandate");
        CallDeferred(nameof(FitDecreePanel));
    }

    /// <summary>For a founding refused because no settlement can spare people: what each settlement in reach is short of, as lines after the rule.</summary>
    private string FoundRefusalDetail(DecreeKind kind, OrderRefusalReason reason, int civ, int tile)
    {
        if (kind != DecreeKind.Found || reason != OrderRefusalReason.NoDonor || _session is null) return "";
        string detail = UiText.FoundDonorShortfalls(_session.FoundDonorStatuses(civ, tile));
        return detail.Length == 0 ? "" : "\n" + detail;
    }

    /// <summary>The usual reasons <c>Open a mine</c> is refused as "no site", named for the tile picked; null when none of them is the cause.</summary>
    private string? MineRefusalDetail(DecreeKind kind, int reason, int tile)
    {
        if (kind != DecreeKind.Mine || reason != (int)OrderRefusalReason.NoSite || _current is null || _catalog is null || _session is null) return null;
        WorldView world = _session.WorldView;
        if ((uint)tile >= (uint)world.TileCount) return null;
        if (_current.KnownDeposits[tile] == 0)
            return _current.DepositClues[tile] != 0
                ? "Your people have noticed a geological clue here, but have not identified a mineable deposit."
                : "Your people have not identified a deposit here.";
        var deposit = (CivSim.Sim.World.DepositKind)world.Deposit[tile];
        string? unlock = deposit switch
        {
            CivSim.Sim.World.DepositKind.ArsenicalCopper => "MET-04",
            CivSim.Sim.World.DepositKind.Tin => "MET-05",
            CivSim.Sim.World.DepositKind.Iron => "MET-08",
            CivSim.Sim.World.DepositKind.Gold or CivSim.Sim.World.DepositKind.Silver => "MET-07",
            CivSim.Sim.World.DepositKind.Salt => "MIN-06",
            _ => null,
        };
        if (unlock is not null && !ProjectKnown(unlock))
            return $"Your people don't know how to work {SpacedName(deposit.ToString()).ToLowerInvariant()} yet: learn {_catalog.Projects[_catalog.IndexOf(unlock)].Name} first.";
        if (world.DepositDepth[tile] >= 2 && !ProjectKnown("MIN-04"))
            return $"That {SpacedName(deposit.ToString()).ToLowerInvariant()} deposit is deep: only a shaft reaches it, and your people haven't learned Shaft Mining. Research it, then try again.";
        int civ = Math.Max(0, _current.ViewerCiv);
        for (int s = 0; s < _current.SettlementCount; s++)
            if (_current.SettlementCiv[s] == civ && CivSim.Sim.World.HexGrid.Distance(_current.SettlementTile[s], tile, world.Width) <= 3)
                return "That deposit is within three tiles of a settlement, so its people already work it themselves: no separate mine is needed.";
        for (int c = 0; c < _current.CampCount; c++)
            if (_current.CampTile[c] == tile && _current.CampKind[c] == 1)
                return "A mining camp is already working that deposit.";
        return null;
    }

    private bool ProjectKnown(string id)
    {
        int index = _catalog!.IndexOf(id);
        return index >= 0 && StateOf(index) is CivSim.Sim.Research.ProjectState.Completed or CivSim.Sim.Research.ProjectState.Dormant;
    }

    private string DecreeUnlockProjectName()
    {
        if (_catalog is null) return "Age of Metal";
        int index = _catalog.IndexOf("KEY-3");
        return index >= 0 ? _catalog.Projects[index].Name : "Age of Metal";
    }

    private void ConfirmDecree()
    {
        if (_session is null || !_session.CanIssueCommands) return;
        int civ = Math.Max(0, _current?.ViewerCiv ?? 0);
        _session.Queue(PlayerCommands.Decree(_session.Tick, civ, _pendingDecree, _pendingTarget, _pendingExtra));
        _decreeConfirm!.Visible = false; _decreeForecast!.Text = "Decree queued for the end of this season.";
        CallDeferred(nameof(FitDecreePanel));
    }

    private void RefreshDecrees(MatchFrame frame)
    {
        if (_decreeMandate is null) return;
        bool available = frame.FocusMandateUnlocked;
        string unlockProject = DecreeUnlockProjectName();
        UiKit.SetText(_decreeMandate, available
            ? $"Mandate · {frame.FocusMandate}/{frame.FocusMandateCap} · next in {frame.FocusMandateNextSeason} seasons"
            : $"Mandate · locked until {unlockProject}");
        UiKit.SetText(_decreeHint!, available ? $"{frame.FocusPendingDecrees} Decrees resolve at season's end." : $"Reach {unlockProject} to issue a Decree.");
        bool layoutChanged = UiKit.SetVisible2(_decreeLockedBlock!, !available) | UiKit.SetVisible2(_decreeControls!, available);
        if (_decreesUnlocked != available)
        {
            _decreesUnlocked = available;
            UpdateDecreesButton();
        }
        if (_decreeOpen) CallDeferred(nameof(FitDecreePanel));
        _ = layoutChanged;
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
        _decreeCompact!.Disabled = !available || !(_session?.CanIssueCommands ?? false);
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
            return $"Likely to accept: unknown. {UiText.DecreeRefusal(DecreeKind.Compact, (int)forecast.LikelyRefusal)}";
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
