using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Economy;
using CivSim.Sim.View;
using CivSim.Sim.World;
using Godot;

namespace CivSim.Game;

/// <summary>The hover card: five quiet lines, with more on Shift or when pinned by a click (spec §3.8).</summary>
public partial class Game
{
    private PanelContainer? _tileCard;
    private Label? _tileTitle, _tileFeatures, _tileWorkers, _tileFallback, _tileHint, _tileExtra;
    private UiIcon? _tileWarn;
    private Control? _tileFallbackRow;
    private UiButton? _tileClose;
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
        _tileFeatures = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        column.AddChild(_tileFeatures);
        _tileWorkers = UiKit.Text("", "Caption", wrap: true, minWidth: 296);
        column.AddChild(_tileWorkers);
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
        _tileClose!.Visible = pinned;
        _tileCard.MouseFilter = pinned ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;

        if (visibility == TileVisibility.Unknown)
        {
            UiKit.SetText(_tileTitle!, "Unexplored");
            UiKit.SetText(_tileFeatures!, "Your people haven't been here yet.");
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
        UiKit.SetText(_tileTitle!, $"{terrain} · {ownerText}");

        string water = world.HasRiver(tile) ? "River" : world.HasLake(tile) ? "Lake" : "";
        string deposit = frame.KnownDeposits[tile] != 0 && world.Deposit[tile] != (byte)DepositKind.None
            ? $"{SpacedName(((DepositKind)world.Deposit[tile]).ToString())} deposit" : "No known deposit";
        UiKit.SetText(_tileFeatures!, water.Length > 0 ? $"{water} · {deposit}" : deposit);

        _tileWorkers!.Visible = true;
        TileWork work = ScanTileWork(frame, tile, visibility == TileVisibility.Visible);
        UiKit.SetText(_tileWorkers, work.Summary);
        _tileFallbackRow!.Visible = work.Fallbacks > 0;
        if (work.Fallbacks > 0) UiKit.SetText(_tileFallback!, $"{work.Fallbacks} use older tools — {work.FirstRequirement}");

        _tileExtra!.Visible = shift;
        if (shift)
        {
            string claims = visibility == TileVisibility.Visible
                ? $"{ClaimText(frame.Claimant[tile], frame.Claim[tile], frame)} · {ClaimText(frame.Claimant2[tile], frame.Claim2[tile], frame)} · {ClaimText(frame.Claimant3[tile], frame.Claim3[tile], frame)}"
                : visibility == TileVisibility.Explored ? "last seen only" : "beyond sight";
            string extra = $"Claims: {claims}\nElevation {world.Elevation[tile]}";
            if (work.Fallbacks > 0 && work.FirstDetail.Length > 0) extra += $"\n{work.FirstDetail}";
            UiKit.SetText(_tileExtra, extra);
        }
        bool showHint = !pinned && !shift && _tileHintsShown < 12;
        _tileHint!.Visible = showHint;
        if (showHint) { UiKit.SetText(_tileHint, "Hold Shift for more · Click to pin"); if (_cardTile != tile) _tileHintsShown++; }
        PositionTileCard(screen);
    }

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
        Span<int> jobs = stackalloc int[13];
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureTile[i] != tile || frame.FigureActivity[i] == (byte)FigureActivity.Idle || frame.FigureActivity[i] > (byte)FigureActivity.Herd) continue;
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
        FigureActivity.Rest => "Rest", FigureActivity.TendFire => "Fire", FigureActivity.Herd => "Herd", _ => activity.ToString(),
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
