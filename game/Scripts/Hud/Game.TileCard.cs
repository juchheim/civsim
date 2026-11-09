using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.View;
using CivSim.Sim.World;
using CivSim.Sim.Politics;
using Godot;

namespace CivSim.Game;

/// <summary>The hover card: five quiet lines, with more on Shift or when pinned by a click (spec §3.8).</summary>
public partial class Game
{
    private PanelContainer? _tileCard;
    private Label? _tileTitle, _tileFeatures, _tileBeaconPreview, _tileWorkers, _tileFallback, _tileHint, _tileExtra;
    private UiIcon? _tileWarn;
    private Control? _tileFallbackRow, _tileScoutRow;
    private Label? _tileScoutStatus;
    private readonly Button[] _tileScoutButtons = new Button[4];
    private UiButton? _tileClose;
    private Control? _tileRenameRow;
    private LineEdit? _tileRename;
    private int _tileRenameBound = -1;
    private Vector2 _hoverScreen;
    private int _hoverTile = -1;
    private float _hoverRest, _tileRefreshClock;
    private bool _hoverMoved;
    private int _tileHintsShown;
    private int _cardTile = -2;
    private bool _cardPinnedShown;

    private void CreateTileCard()
    {
        _tileCard = UiKit.Panel("CardPanel", "TileCard", UiKit.MouseFilter.Ignore);
        _tileCard.CustomMinimumSize = new Vector2(320, 0);
        _tileCard.Visible = false;
        var column = UiKit.Column(UiTokens.Sp1);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        var top = UiKit.Row(UiTokens.Sp2);
        top.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tileTitle = UiKit.Text("", "BodyStrong", ellipsis: true);
        top.AddChild(_tileTitle);
        _tileClose = UiKit.IconButton(UiGlyph.Close, "Unpin (Esc)", () => { _pinnedTile = -1; UpdatePinLayer(); HideTileCard(); }, 26, 14);
        _tileClose.Visible = false;
        top.AddChild(_tileClose);
        column.AddChild(top);
        var rename = UiKit.Row(UiTokens.Sp1);
        _tileRename = new LineEdit
        {
            PlaceholderText = "Name this settlement",
            MaxLength = MaxSettlementNameLength,
            SelectAllOnFocus = true,
            FocusMode = Control.FocusModeEnum.All,
            TooltipText = "Rename this settlement (Enter to confirm, empty to restore the default name)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, UiTokens.ControlS),
        };
        _tileRename.TextSubmitted += _ => CommitSettlementRename();
        rename.AddChild(_tileRename);
        rename.AddChild(UiKit.Secondary("Rename", CommitSettlementRename, "Save this settlement's name", UiGlyph.None, UiTokens.ControlS));
        _tileRenameRow = rename;
        _tileRenameRow.Visible = false;
        column.AddChild(rename);
        _tileFeatures = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        column.AddChild(_tileFeatures);
        _tileBeaconPreview = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        _tileBeaconPreview.Visible = false;
        column.AddChild(_tileBeaconPreview);
        _tileWorkers = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        column.AddChild(_tileWorkers);
        var scout = UiKit.Column(UiTokens.Sp1);
        _tileScoutStatus = UiKit.Text("Scouting", "Caption");
        scout.AddChild(_tileScoutStatus);
        var scoutOptions = UiKit.Row(UiTokens.Sp1);
        string[] scoutLabels = { "Auto", "0", "1", "2" };
        ScoutPolicy[] scoutPolicies = { ScoutPolicy.Auto, ScoutPolicy.Zero, ScoutPolicy.One, ScoutPolicy.Two };
        string[] scoutTips =
        {
            "Use normal scouting, yielding scouts automatically when food is short.",
            "Stop scouting at this settlement; released workers return to other jobs next season.",
            "Allow up to one scout at this settlement next season.",
            "Allow up to two scouts at this settlement next season. Requires WAY-01.",
        };
        for (int i = 0; i < _tileScoutButtons.Length; i++)
        {
            int slot = i;
            ScoutPolicy policy = scoutPolicies[i];
            Button button = UiKit.InteractiveChip(scoutLabels[i], UiGlyph.None, () => SetScoutPolicyAtPinnedSettlement(policy), scoutTips[i]);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _tileScoutButtons[slot] = button;
            scoutOptions.AddChild(button);
        }
        scout.AddChild(scoutOptions);
        _tileScoutRow = scout;
        column.AddChild(scout);
        var warn = UiKit.Row(UiTokens.Sp1);
        warn.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tileWarn = new UiIcon(UiGlyph.Warning, 16, UiTokens.Warning) { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        warn.AddChild(_tileWarn);
        _tileFallback = UiKit.Text("", "CaptionWarning", wrap: true, minWidth: 276);
        warn.AddChild(_tileFallback);
        _tileFallbackRow = warn;
        column.AddChild(warn);
        _tileExtra = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        column.AddChild(_tileExtra);
        _tileHint = UiKit.Text("", "CaptionTertiary");
        column.AddChild(_tileHint);
        _tileCard.AddChild(column);
        _hudRoot!.AddChild(_tileCard);
    }

    private bool _hoverActive;

    private void NoteHover(Vector2 screen)
    {
        if (screen.DistanceTo(_hoverScreen) < 2f && _hoverActive) return;
        _hoverActive = true;
        _hoverScreen = screen;
        _hoverRest = 0;
        _hoverMoved = true;
        if (_pinnedTile < 0 && _tileCard is { Visible: true }) HideTileCard();
    }

    private void HideTileCardWhileDragging()
    {
        if (_leftDown || _rightDown || _middleDown) { _hoverRest = 0; if (_pinnedTile < 0) HideTileCard(); }
    }

    private void HideTileCard()
    {
        if (_tileCard is null) return;
        _tileCard.Visible = false;
        _cardTile = -2;
    }

    private void TickTileCard(float delta)
    {
        UpdatePinMarker();
        if (_tileCard is null || _current is null) return;
        if (_pinnedTile >= 0)
        {
            _tileRefreshClock += delta;
            if (_cardTile != _pinnedTile || !_cardPinnedShown || _tileRefreshClock > 1.0f) RefreshTileCardNow(_hoverScreen);
            return;
        }
        _cardPinnedShown = false;
        if (!_hoverActive || _leftDown || _rightDown || _middleDown) return;
        Control? hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is not null && hovered != _hudRoot) { HideTileCard(); return; }
        _hoverRest += delta;
        if (_hoverRest < 0.25f) return;
        if (_hoverMoved || _cardTile == -2)
        {
            _hoverMoved = false;
            if (!TryPickTile(_hoverScreen, out int tile)) { HideTileCard(); return; }
            _hoverTile = tile;
        }
        if (_hoverTile < 0) return;
        _tileRefreshClock += delta;
        bool shift = Input.IsKeyPressed(Key.Shift);
        if (_cardTile != _hoverTile || _tileRefreshClock > 1.0f || shift != _cardShift) RefreshTileCardNow(_hoverScreen);
    }

    private bool _cardShift;

    private void RefreshTileCardNow(Vector2 screen)
    {
        if (_tileCard is null || _current is null || _session is null) return;
        _tileRefreshClock = 0;
        int tile = _pinnedTile >= 0 ? _pinnedTile : _hoverTile;
        if (_pinnedTile < 0 && !TryPickTile(screen, out tile)) { HideTileCard(); return; }
        if (tile < 0) { HideTileCard(); return; }
        bool pinned = _pinnedTile >= 0;
        bool shift = Input.IsKeyPressed(Key.Shift) || pinned;
        _cardShift = Input.IsKeyPressed(Key.Shift);
        _cardTile = tile;
        _cardPinnedShown = pinned;
        MatchFrame frame = _current;
        WorldView world = _session.WorldView;
        TileVisibility visibility = (TileVisibility)frame.Visibility[tile];
        _tileScoutRow!.Visible = false;
        _tileBeaconPreview!.Visible = _beaconMode;
        if (_beaconMode && TryGetOwnBeaconIndex(tile, out _))
            UiKit.SetText(_tileBeaconPreview, "Your Beacon is here · click to remove it.");
        _tileClose!.Visible = pinned;
        _tileCard.MouseFilter = pinned ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;

        if (visibility == TileVisibility.Unknown)
        {
            _tileRenameRow!.Visible = false;
            _tileRenameBound = -1;
            UiKit.SetText(_tileTitle!, "Unexplored");
            UiKit.SetText(_tileFeatures!, "Your people haven't been here yet.");
            if (_beaconMode && !TryGetOwnBeaconIndex(tile, out _))
                UiKit.SetText(_tileBeaconPreview!, "Settlement likelihood is unknown until this land is explored.");
            _tileWorkers!.Visible = false;
            _tileFallbackRow!.Visible = false;
            _tileExtra!.Visible = false;
            _tileHint!.Visible = false;
            PositionTileCard(screen);
            return;
        }

        string terrain = TerrainName((Biome)world.Biome[tile]);
        int owner = frame.Owner[tile];
        string ownerText = owner < 0
            ? (visibility == TileVisibility.Explored ? "unclaimed when last seen" : "unclaimed")
            : owner == frame.ViewerCiv ? "Yours" : CivLabel(frame, owner);
        int ownSettlement = FindOwnSettlementAt(frame, tile);
        UiKit.SetText(_tileTitle!, ownSettlement >= 0
            ? $"{SettlementLabel(ownSettlement)} · {terrain} · {ownerText}"
            : $"{terrain} · {ownerText}");
        RefreshRenameRow(ownSettlement, pinned);
        if (_beaconMode && !TryGetOwnBeaconIndex(tile, out _))
            UiKit.SetText(_tileBeaconPreview!, BeaconPreviewText(frame, tile));

        string water = world.HasRiver(tile) ? "River" : world.HasLake(tile) ? "Lake" : "";
        bool depositKnown = frame.KnownDeposits[tile] != 0;
        DepositKind tileDeposit = depositKnown ? (DepositKind)world.Deposit[tile] : DepositKind.None;
        string deposit = depositKnown && tileDeposit != DepositKind.None
            ? $"{SpacedName(tileDeposit.ToString())} deposit{(world.DepositDepth[tile] >= 2 ? " (deep: needs Shaft Mining)" : "")}" :
            frame.DepositClues[tile] != 0 ? DepositClueText(frame.DepositClues[tile]) : "No known deposit";
        string liveTile = visibility == TileVisibility.Visible
            ? $"Forest band {frame.ForestBand[tile]}" : "";
        if (frame.KnownDeposits[tile] != 0 && frame.DepositWorked[tile] != 0) liveTile = JoinTileDetail(liveTile, "worked");
        else if (frame.KnownDeposits[tile] != 0 && frame.MiningReach[tile] != 0)
            liveTile = JoinTileDetail(liveTile, world.DepositDepth[tile] >= 2 && !ProjectKnown("MIN-04") ? "within mining reach, but too deep without Shaft Mining" : "within mining reach");
        UiKit.SetText(_tileFeatures!, string.Join(" · ", new[] { water, deposit, liveTile }.Where(s => s.Length > 0)));

        _tileWorkers!.Visible = true;
        TileWork work = ScanTileWork(frame, tile, visibility == TileVisibility.Visible);
        UiKit.SetText(_tileWorkers, work.Summary);
        RefreshScoutControls(frame, tile, pinned);
        _tileFallbackRow!.Visible = work.Fallbacks > 0;
        if (work.Fallbacks > 0) UiKit.SetText(_tileFallback!, $"{work.Fallbacks} use older tools — {work.FirstRequirement}");

        int supplySettlement = _mapOverlay == 8 && visibility == TileVisibility.Visible ? SettlementAtTile(frame, tile) : -1;
        bool showSupply = supplySettlement >= 0;
        _tileExtra!.Visible = shift || showSupply;
        if (shift || showSupply)
        {
            string extra = "";
            if (shift)
            {
                string claims = visibility == TileVisibility.Visible
                    ? $"{ClaimText(frame.Claimant[tile], frame.Claim[tile], frame)} · {ClaimText(frame.Claimant2[tile], frame.Claim2[tile], frame)} · {ClaimText(frame.Claimant3[tile], frame.Claim3[tile], frame)}"
                    : visibility == TileVisibility.Explored ? "last seen only" : "beyond sight";
                extra = $"Claims: {claims}\nElevation {world.Elevation[tile]}";
                if (work.Fallbacks > 0 && work.FirstDetail.Length > 0) extra += $"\n{work.FirstDetail}";
                if (visibility == TileVisibility.Visible) extra += $"\nForest stock band {frame.ForestBand[tile]}";
                if (visibility == TileVisibility.Visible)
                {
                    for (int camp = 0; camp < frame.CampCount; camp++)
                    {
                        if (frame.CampTile[camp] != tile || frame.CampKind[camp] != (byte)CampKind.Mining) continue;
                        string mineral = frame.CampDeposit[camp] == 0 ? "unknown deposit" : SpacedName(((DepositKind)frame.CampDeposit[camp]).ToString());
                        extra += $"\nMining camp: {mineral} · crew {frame.CampCrew[camp]} · {(frame.CampBuilt[camp] != 0 ? "built" : "work camp")}{(frame.CampMineHead[camp] != 0 ? " · minehead" : "")}";
                    }
                }
                for (int s = 0; s < frame.SettlementCount; s++)
                {
                    if (frame.SettlementTile[s] != tile || (frame.SettlementFlags[s] & (ushort)SettlementViewFlags.Remembered) != 0) continue;
                    bool own = frame.SettlementCiv[s] == frame.ViewerCiv;
                    if (!own && visibility != TileVisibility.Visible) continue;
                    extra += $"\nCohesion: {CohesionName(frame.SettlementCohesion[s])}";
                    if (own && (frame.SettlementOverreach[s] != 0 || frame.SettlementSpanPenalty[s] != 0))
                        extra += $" · overreach {frame.SettlementOverreach[s]} · span strain {frame.SettlementSpanPenalty[s]}";
                }
            }
            if (showSupply) extra += (extra.Length > 0 ? "\n\n" : "") + SupplyNetworkDetails(frame, supplySettlement);
            UiKit.SetText(_tileExtra, extra);
        }
        bool showHint = !pinned && !shift && _tileHintsShown < 12;
        _tileHint!.Visible = showHint;
        if (showHint) { UiKit.SetText(_tileHint, ownSettlement >= 0 ? "Hold Shift for more · Click to pin and rename" : "Hold Shift for more · Click to pin"); if (_cardTile != tile) _tileHintsShown++; }
        PositionTileCard(screen);
    }

    /// <summary>The viewer's own live settlement on <paramref name="tile"/> (its sim index), or -1.</summary>
    private static int FindOwnSettlementAt(MatchFrame frame, int tile)
    {
        if (frame.ViewerCiv < 0) return -1;
        for (int s = 0; s < frame.SettlementCount; s++)
        {
            SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[s];
            if (frame.SettlementTile[s] == tile && frame.SettlementCiv[s] == frame.ViewerCiv &&
                (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own)
                return frame.SettlementIndex[s];
        }
        return -1;
    }

    private static int SettlementAtTile(MatchFrame frame, int tile)
    {
        for (int i = 0; i < frame.SettlementCount; i++)
            if (frame.SettlementTile[i] == tile && (frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) == 0)
                return frame.SettlementIndex[i];
        return -1;
    }

    private string SupplyNetworkDetails(MatchFrame frame, int settlement)
    {
        var sends = new List<string>();
        var receives = new List<string>();
        int moreSends = 0, moreReceives = 0;

        void AddLine(bool outgoing, string line)
        {
            List<string> list = outgoing ? sends : receives;
            if (list.Count < 4) list.Add(line);
            else if (outgoing) moreSends++;
            else moreReceives++;
        }

        for (int i = 0; i < frame.SupplyTransferCount; i++)
        {
            int from = frame.SupplyTransferFrom[i], to = frame.SupplyTransferTo[i];
            string kind = frame.SupplyTransferKind[i] == 1 ? "Tribute" : "Shared";
            string cargo = $"{kind} · {SupplyGoodLabel(frame.SupplyTransferGood[i])} · {FormatSupplyAmount(frame.SupplyTransferAmount[i] / 4294967296.0)}";
            if (from == settlement) AddLine(true, $"{SettlementLabel(to)}: {cargo}");
            if (to == settlement) AddLine(false, $"{SettlementLabel(from)}: {cargo}");
        }
        for (int i = 0; i < frame.RouteCount; i++)
        {
            int from = frame.RouteSellerSettlement[i], to = frame.RouteBuyerSettlement[i];
            string good = Enum.GetName(typeof(TradeGood), frame.RouteGood[i]) ?? "Goods";
            string cargo = $"Trade · {good} · {FormatSupplyAmount(frame.RouteVolumePermille[i] / 1000.0)}";
            if (from == settlement) AddLine(true, $"{SettlementLabel(to)}: {cargo}");
            if (to == settlement) AddLine(false, $"{SettlementLabel(from)}: {cargo}");
        }
        for (int i = 0; i < frame.PartyCount; i++)
        {
            PartyKind kind = (PartyKind)frame.PartyKind[i];
            if (kind is not (PartyKind.Gift or PartyKind.Barter or PartyKind.Caravan)) continue;
            int from = frame.PartyFrom[i], to = frame.PartyTo[i];
            string item = frame.PartyGood[i] == 0 ? "Food" : Enum.GetName(typeof(TradeGood), frame.PartyGood[i]) ?? "Goods";
            string label = kind switch { PartyKind.Gift => "Gift", PartyKind.Barter => "Barter", _ => "Caravan" };
            string cargo = $"{label} · {item} · {FormatSupplyAmount(frame.PartyCargoPermille[i] / 1000.0)} · en route";
            if (from == settlement) AddLine(true, $"{SettlementLabel(to)}: {cargo}");
            if (to == settlement) AddLine(false, $"{SettlementLabel(from)}: {cargo}");
        }

        int row = frame.IndexOfSettlement(settlement);
        var result = new System.Text.StringBuilder("SUPPLY NETWORKS");
        if (row >= 0 && frame.SettlementFood[row] >= 2)
            result.Append(frame.SettlementFood[row] >= 3 ? "\nFood shortage: famine" : "\nFood shortage: short");
        if (row >= 0 && (frame.SettlementFlags[row] & (ushort)SettlementViewFlags.Own) != 0)
        {
            int econ = row * EconField.Stride;
            if (frame.SettlementEcon[econ + EconField.Valid] != 0 &&
                frame.SettlementEcon[econ + EconField.BlockerNeed] > frame.SettlementEcon[econ + EconField.BlockerHave])
                result.Append("\nBuilding materials short");
        }
        AppendGroup(result, "Sends", sends, moreSends);
        AppendGroup(result, "Receives", receives, moreReceives);
        if (sends.Count == 0 && receives.Count == 0 && moreSends == 0 && moreReceives == 0)
            result.Append("\nNo goods moving to or from this settlement this season.");
        return result.ToString();
    }

    private static void AppendGroup(System.Text.StringBuilder text, string heading, List<string> lines, int more)
    {
        if (lines.Count == 0 && more == 0) return;
        text.Append($"\n{heading}:");
        foreach (string line in lines) text.Append($"\n· {line}");
        if (more > 0) text.Append($"\n· and {more} more");
    }

    private static string FormatSupplyAmount(double value)
        => value <= 0 ? "amount unavailable" : value.ToString("N1", System.Globalization.CultureInfo.InvariantCulture);

    private static string SupplyGoodLabel(string id)
    {
        string code = id.StartsWith("G-", StringComparison.Ordinal) ? id[2..] : id;
        return code switch
        {
            "FOOD" => "Food", "CUORE" => "Copper ore", "ASORE" => "Arsenical ore", "TINORE" => "Tin ore",
            "IRONORE" => "Iron ore", "COPPER" => "Copper", "BRONZE" => "Bronze", "ABRONZE" => "Arsenical bronze",
            "WOOD" => "Wood", "GRAIN" => "Grain", "STONE" => "Stone", "CLAY" => "Clay", "TIN" => "Tin",
            "IRON" => "Iron", "SALT" => "Salt", "GOLD" => "Gold", "TOOL11" => "Iron kit", "TOOL12" => "Iron kit",
            _ => code.Replace("_", " ").ToLowerInvariant(),
        };
    }

    private void RefreshRenameRow(int settlement, bool pinned)
    {
        bool show = pinned && settlement >= 0;
        _tileRenameRow!.Visible = show;
        if (!show) { _tileRenameBound = -1; return; }
        if (_tileRenameBound == settlement) return;
        // Bind once per settlement so the one-second refresh never overwrites what the player is typing.
        _tileRenameBound = settlement;
        _tileRename!.Text = CustomSettlementName(settlement);
        _tileRename.PlaceholderText = $"Name this settlement (now {SettlementLabel(settlement)})";
        _tileRename.GrabFocus();
    }

    private void CommitSettlementRename()
    {
        if (_tileRename is null || _tileRenameBound < 0) return;
        int settlement = _tileRenameBound;
        string name = RenameSettlement(settlement, _tileRename.Text);
        _tileRename.Text = name;
        _tileRename.ReleaseFocus();
        RefreshTileCardNow(_hoverScreen);
        if (name.Length > 0) ShowToast("Settlement renamed", ToastKind.Info, $"Now called {name}.", key: "settlement-renamed");
    }

    private static string DepositClueText(byte clue) => clue switch
    {
        2 => "Dark mineral seam · not identified",
        3 => "Oil seep · not surveyed",
        _ => "Mineral outcrop · not identified",
    };

    private string BeaconPreviewText(MatchFrame frame, int tile)
    {
        if (_session is null) return "Settlement likelihood unavailable.";
        CivSim.Sim.Economy.BeaconSitePreview preview = GetBeaconPlacementPreview(frame, tile);
        if (!preview.CanFound)
            return "Estimated likelihood: none · no current settlement can legally found here.";

        int fit = Math.Clamp((preview.RelativeSiteFit * CivSim.Sim.Numerics.Fix64.FromInt(100)).RoundToInt(), 0, 100);
        string likelihood = fit >= 85 ? "High" : fit >= 60 ? "Possible" : "Low";
        string ready = preview.PendingSplits > 0
            ? $"Ready splits choosing this exact tile: {preview.PendingSplitsChoosingTile}/{preview.PendingSplits}."
            : "No split is pending right now; timing depends on population growth and conditions.";
        return $"Estimated likelihood: {likelihood} · this tile scores {fit}% of the best reachable site in this field. {ready}";
    }

    private void RefreshScoutControls(MatchFrame frame, int tile, bool pinned)
    {
        if (!pinned || frame.ViewerCiv < 0) return;
        for (int s = 0; s < frame.SettlementCount; s++)
        {
            SettlementViewFlags flags = (SettlementViewFlags)frame.SettlementFlags[s];
            if (frame.SettlementTile[s] != tile || frame.SettlementCiv[s] != frame.ViewerCiv ||
                (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) != SettlementViewFlags.Own)
                continue;

            int settlement = frame.SettlementIndex[s];
            int activeScouts = 0;
            for (int i = 0; i < frame.FigureCount; i++)
                if (frame.FigureSettlement[i] == settlement && frame.FigureActivity[i] == (byte)FigureActivity.Scout)
                    activeScouts++;
            ScoutPolicy policy = EffectiveScoutPolicy(frame.ViewerCiv, settlement,
                (ScoutPolicy)frame.SettlementScoutPolicy[s], out bool policyQueued);
            UiKit.SetText(_tileScoutStatus!, $"Scouting · {activeScouts} active this season" +
                (policyQueued ? $" · {policy} queued for next season after resume" : " · changes next season"));
            for (int i = 0; i < _tileScoutButtons.Length; i++)
            {
                _tileScoutButtons[i].SetPressedNoSignal(i == (int)policy);
                _tileScoutButtons[i].Disabled = !(_session?.CanIssueCommands ?? false) ||
                    i == (int)ScoutPolicy.Two && !HasWayfindingOne(frame);
            }
            _tileScoutRow!.Visible = true;
            return;
        }
    }

    private void SetScoutPolicyAtPinnedSettlement(ScoutPolicy policy)
    {
        if (_session is null || _current is null || _pinnedTile < 0) return;
        if (policy == ScoutPolicy.Two && !HasWayfindingOne(_current)) return;
        for (int s = 0; s < _current.SettlementCount; s++)
        {
            SettlementViewFlags flags = (SettlementViewFlags)_current.SettlementFlags[s];
            if (_current.SettlementTile[s] != _pinnedTile || _current.SettlementCiv[s] != _current.ViewerCiv ||
                (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) != SettlementViewFlags.Own)
                continue;
            QueueCommand(PlayerCommands.SetScoutPolicy(_session.Tick, _current.ViewerCiv,
                _current.SettlementIndex[s], policy));
            return;
        }
    }

    private static string JoinTileDetail(string current, string detail) => current.Length == 0 ? detail : $"{current} · {detail}";

    private static string CohesionName(byte band) => band switch { 0 => "strong", 1 => "steady", 2 => "strained", _ => "fraying" };

    private void PositionTileCard(Vector2 screen)
    {
        if (_tileCard is null) return;
        _tileCard.Visible = true;
        _tileCard.ResetSize();
        Vector2 size = _tileCard.GetCombinedMinimumSize();
        Vector2 view = GetViewport().GetVisibleRect().Size;
        Vector2 position = screen + new Vector2(21, 21);
        if (position.X + size.X > view.X - 8) position.X = screen.X - size.X - 21;
        if (position.Y + size.Y > view.Y - 8) position.Y = screen.Y - size.Y - 21;
        _tileCard.Position = new Vector2(Mathf.Clamp(position.X, 8, Math.Max(8, view.X - size.X - 8)), Mathf.Clamp(position.Y, 8, Math.Max(8, view.Y - size.Y - 8)));
    }

    private struct TileWork
    {
        public string Summary, FirstRequirement, FirstDetail;
        public int Workers, Fallbacks;
    }

    private TileWork ScanTileWork(MatchFrame frame, int tile, bool visible)
    {
        var result = new TileWork { Summary = "Out of sight", FirstRequirement = "", FirstDetail = "" };
        if (!visible) return result;
        Span<int> jobs = stackalloc int[23];
        for (int i = 0; i < frame.FigureCount; i++)
        {
            FigureActivity activity = (FigureActivity)frame.FigureActivity[i];
            if (frame.FigureTile[i] != tile || activity == FigureActivity.Idle ||
                activity > FigureActivity.Herd && activity != FigureActivity.Scout) continue;
            int job = frame.FigureActivity[i];
            jobs[job]++;
            result.Workers++;
            MethodFallbackReason reason = (MethodFallbackReason)frame.FigureMethodFallback[i];
            if (reason == MethodFallbackReason.None) continue;
            result.Fallbacks++;
            if (result.FirstRequirement.Length != 0) continue;
            result.FirstRequirement = reason switch
            {
                MethodFallbackReason.MissingTool => $"need {ToolName((MethodToolId)frame.FigureMethodFallbackTool[i])}",
                MethodFallbackReason.HuntParty => HuntPartyRequirement(frame.FigureKnownToolRung[i]),
                _ => "blocked",
            };
            result.FirstDetail = $"{ActivityName((FigureActivity)job)}: {frame.FigureMethodEfficiencyPermille[i] / 10}% of the best method ({result.FirstRequirement})";
        }
        if (result.Workers == 0) { result.Summary = "Nobody working here"; return result; }
        var rows = new List<string>(3);
        for (int i = 1; i < jobs.Length; i++) if (jobs[i] > 0) rows.Add($"{ActivityName((FigureActivity)i)} {jobs[i]}");
        result.Summary = $"Working here: {result.Workers} — {string.Join(", ", rows)}";
        return result;
    }

    private static string CivLabel(MatchFrame frame, int civ)
        => civ >= 0 && civ < frame.CivCount && (frame.CivFlags[civ] & (ushort)CivViewFlags.Aware) != 0 ? UiText.CivName(civ) : "A distant people";

    private static string ClaimText(int civ, byte strength, MatchFrame frame)
        => civ < 0 || strength == 0 ? "—" : $"{(civ == frame.ViewerCiv ? "You" : CivLabel(frame, civ))} {strength}%";

    private static string SpacedName(string pascal)
    {
        var text = new System.Text.StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i])) text.Append(' ').Append(char.ToLowerInvariant(pascal[i]));
            else text.Append(i == 0 ? pascal[i] : pascal[i]);
        }
        return text.ToString();
    }

    private static string TerrainName(Biome biome) => biome switch
    {
        Biome.GrasslandSteppe => "Grassland",
        Biome.OpenWoodland => "Open woodland",
        Biome.TemperateForest => "Temperate forest",
        Biome.TropicalForest => "Tropical forest",
        _ => UiText.Sentence(SpacedName(biome.ToString())),
    };

    private static string ActivityName(FigureActivity activity) => activity switch
    {
        FigureActivity.Forage => "Forage", FigureActivity.Hunt => "Hunt", FigureActivity.Fish => "Fish",
        FigureActivity.Farm => "Farm", FigureActivity.Chop => "Chop", FigureActivity.Gather => "Gather",
        FigureActivity.Craft => "Craft", FigureActivity.Build => "Build", FigureActivity.Preserve => "Preserve",
        FigureActivity.Rest => "Rest", FigureActivity.TendFire => "Fire", FigureActivity.Herd => "Herd",
        FigureActivity.Scout => "Scout", _ => activity.ToString(),
    };

    private static string ToolName(MethodToolId tool) => tool switch
    {
        MethodToolId.Tool1 => "a stone chopper", MethodToolId.Tool2 => "a hand axe", MethodToolId.Tool4 => "a hafted axe",
        MethodToolId.Tool7 => "a polished axe", MethodToolId.SpearThrower => "a spear-thrower",
        MethodToolId.FishingKit => "a fishing kit", _ => tool.ToString().ToLowerInvariant(),
    };

    private static string HuntPartyRequirement(sbyte rung) => rung switch
    {
        3 => "need a hunting party of 6 or more",
        5 => "need a hunting party of 1–2",
        _ => "need a compatible hunting party",
    };

    // ---- overlay byte builders (renderer payload) ----------------------------------------------

    private static byte[] BuildFoodOverlay(MatchFrame frame)
    {
        int tiles = frame.Width * frame.Height;
        var result = new byte[tiles];
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if ((frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) != 0) continue;
            int tile = frame.SettlementTile[i];
            if ((uint)tile < (uint)tiles) result[tile] = (byte)(frame.SettlementFood[i] + 1);
        }
        return result;
    }

    private static byte[] BuildFoodForecastOverlay(MatchFrame frame)
    {
        int tiles = frame.Width * frame.Height;
        var result = new byte[tiles];
        for (int i = 0; i < frame.SettlementCount; i++)
        {
            if ((frame.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) != 0) continue;
            int tile = frame.SettlementTile[i];
            if ((uint)tile < (uint)tiles) result[tile] = (byte)(frame.SettlementWinterForecast[i] + 1);
        }
        return result;
    }

    private static byte[] BuildMethodOverlay(MatchFrame frame)
    {
        int tiles = frame.Width * frame.Height;
        var result = new byte[tiles];
        for (int i = 0; i < frame.FigureCount; i++)
        {
            int rung = frame.FigureToolRung[i];
            int tile = frame.FigureTile[i];
            bool fallback = frame.FigureMethodFallback[i] != (byte)MethodFallbackReason.None;
            if ((rung < 0 && !fallback) || (uint)tile >= (uint)tiles) continue;
            result[tile] = (byte)Math.Max(result[tile], Math.Clamp(rung + 1, 1, 255));
        }
        return result;
    }

    private static byte[] BuildMethodFallbackOverlay(MatchFrame frame)
    {
        int tiles = frame.Width * frame.Height;
        var result = new byte[tiles];
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureMethodFallback[i] == (byte)MethodFallbackReason.None) continue;
            int tile = frame.FigureTile[i];
            if ((uint)tile < (uint)tiles && frame.Visibility[tile] == (byte)TileVisibility.Visible) result[tile] = 255;
        }
        return result;
    }
}
