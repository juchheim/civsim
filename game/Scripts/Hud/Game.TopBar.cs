using System;
using CivSim.Game.Ui;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Top band: civ card, time cluster with the dominant Pause control, and the top-right buttons (spec §3.1).</summary>
public partial class Game
{
    private PanelContainer? _civCard, _timeCluster, _topRight, _pausedPill, _replayChip;
    private Label? _civNameLabel, _civPeopleLabel, _civSettlementsLabel, _ageLabel, _ageCountLabel, _yearLabel, _seasonLabel, _pausedPillLabel;
    private UiEmblem? _civEmblem;
    private UiButton? _peopleTopButton;
    private UiProgressBar? _ageBar;
    private UiSeasonDial? _seasonDial;
    private UiButton? _pauseButton;
    private UiSegmented? _speedControl;
    private UiButton? _btnDecrees, _btnDiplomacy, _btnChronicle, _btnMenu;
    private PanelContainer? _chronicleBadge;
    private Label? _chronicleBadgeLabel;
    private Control? _civDivider, _civAgeBlock, _civStatsBlock;
    private int _lastSeasonOfYear = -1;

    private static void AnchorTo(Control c, float ax0, float ay0, float ax1, float ay1, float l, float t, float r, float b,
        Control.GrowDirection growH = Control.GrowDirection.End, Control.GrowDirection growV = Control.GrowDirection.End)
    {
        c.AnchorLeft = ax0; c.AnchorTop = ay0; c.AnchorRight = ax1; c.AnchorBottom = ay1;
        c.OffsetLeft = l; c.OffsetTop = t; c.OffsetRight = r; c.OffsetBottom = b;
        c.GrowHorizontal = growH;
        c.GrowVertical = growV;
    }

    private void CreateTopBar()
    {
        // Civ card
        _civCard = UiKit.Panel("HudPanel", "CivCard");
        _civCard.CustomMinimumSize = new Vector2(0, TopBandHeight);
        var cardRow = UiKit.Row(UiTokens.Sp3);
        _civEmblem = new UiEmblem(0, 34);
        cardRow.AddChild(_civEmblem);
        _civStatsBlock = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        _civStatsBlock.AddThemeConstantOverride("separation", 0);
        var nameRow = UiKit.Row(UiTokens.Sp1);
        _civNameLabel = UiKit.Text("YOUR PEOPLE", "Eyebrow", ellipsis: true);
        _civNameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameRow.AddChild(_civNameLabel);
        _peopleTopButton = UiKit.IconButton(UiGlyph.People, "People, work and research context", OpenPeoplePanel, 30, 16);
        _peopleTopButton.FocusMode = Control.FocusModeEnum.All;
        _peopleTopButton.CustomMinimumSize = new Vector2(30, 26);
        nameRow.AddChild(_peopleTopButton);
        _civStatsBlock.AddChild(nameRow);
        var stats = UiKit.Row(UiTokens.Sp1);
        stats.MouseFilter = Control.MouseFilterEnum.Ignore;
        _civPeopleLabel = UiKit.Text("12", "NumStrong");
        _civSettlementsLabel = UiKit.Text("people · 1 settlement", "Caption");
        stats.AddChild(_civPeopleLabel);
        stats.AddChild(_civSettlementsLabel);
        _civStatsBlock.AddChild(stats);
        cardRow.AddChild(_civStatsBlock);
        _civDivider = UiKit.Divider(true);
        cardRow.AddChild(_civDivider);
        _civAgeBlock = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _civAgeBlock.AddThemeConstantOverride("separation", 2);
        _ageLabel = UiKit.Text("DAWN AGE", "Eyebrow");
        _civAgeBlock.AddChild(_ageLabel);
        var ageRow = UiKit.Row(UiTokens.Sp2);
        ageRow.MouseFilter = Control.MouseFilterEnum.Ignore;
        _ageBar = new UiProgressBar(4) { CustomMinimumSize = new Vector2(89, 4), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        ageRow.AddChild(_ageBar);
        _ageCountLabel = UiKit.Text("0/13", "Num", minWidth: 40);
        ageRow.AddChild(_ageCountLabel);
        _civAgeBlock.AddChild(ageRow);
        cardRow.AddChild(_civAgeBlock);
        _civCard.AddChild(cardRow);
        _hudRoot!.AddChild(_civCard);

        // Time cluster
        _timeCluster = UiKit.Panel("HudPanel", "TimeCluster");
        _timeCluster.CustomMinimumSize = new Vector2(0, TopBandHeight);
        var timeRow = UiKit.Row(UiTokens.Sp3);
        _replayChip = UiKit.Chip("REPLAY", UiGlyph.Play, UiTokens.Info, "ChipPanelInfo");
        _replayChip.Visible = false;
        timeRow.AddChild(_replayChip);
        _seasonDial = new UiSeasonDial();
        timeRow.AddChild(_seasonDial);
        var dateColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(96, 0) };
        dateColumn.AddThemeConstantOverride("separation", -2);
        _yearLabel = UiKit.Text("Year 1", "NumTitle");
        _seasonLabel = UiKit.Text("SPRING", "Eyebrow");
        dateColumn.AddChild(_yearLabel);
        dateColumn.AddChild(_seasonLabel);
        timeRow.AddChild(dateColumn);
        _pauseButton = UiKit.Secondary("Pause", TogglePause, "Pause (Space). You can still choose research and give orders.", UiGlyph.Pause);
        _pauseButton.CustomMinimumSize = new Vector2(128, UiTokens.ControlM);
        _pauseButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        timeRow.AddChild(_pauseButton);
        _speedControl = new UiSegmented(new[] { "½×", "1×", "2×", "4×" }, 1, index => _session?.SetSpeedIndex(index), 40);
        _speedControl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        for (int i = 0; i < 4; i++) _speedControl.SetTooltip(i, "Game speed ([ and ])");
        timeRow.AddChild(_speedControl);
        _timeCluster.AddChild(timeRow);
        _hudRoot.AddChild(_timeCluster);

        // Paused pill
        _pausedPill = UiKit.Panel("PausedPill", "PausedPill");
        _pausedPillLabel = UiKit.Text("PAUSED · Space to resume", "EyebrowAccent");
        _pausedPill.AddChild(_pausedPillLabel);
        _pausedPill.Visible = false;
        _pausedPill.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _pausedPill.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) TogglePause();
        };
        _hudRoot.AddChild(_pausedPill);

        // Top-right cluster
        _topRight = UiKit.Panel("HudPanel", "TopRight");
        _topRight.CustomMinimumSize = new Vector2(0, TopBandHeight);
        var rightRow = UiKit.Row(UiTokens.Sp1);
        _btnDecrees = UiKit.Ghost("Decrees", () => ToggleDecrees(), "Mandate and civic orders", UiGlyph.People, UiTokens.ControlM);
        _btnDiplomacy = UiKit.Ghost("Diplomacy", () => ToggleDiplomacy(), "Your neighbours and your orders (O)", UiGlyph.Flags, UiTokens.ControlM);
        _btnChronicle = UiKit.Ghost("Chronicle", () => ToggleChronicle(), "Your people's story (C)", UiGlyph.Scroll, UiTokens.ControlM);
        _btnMenu = UiKit.Ghost("Menu", () => ToggleGameMenu(), "Save, load, replays and settings (Esc)", UiGlyph.Menu, UiTokens.ControlM);
        _chronicleBadge = UiKit.Panel("BadgePanel", null, UiKit.MouseFilter.Ignore);
        _chronicleBadge.Visible = false;
        _chronicleBadgeLabel = UiKit.Text("1", "Num", UiTokens.TextOnAccent);
        _chronicleBadgeLabel.AddThemeFontSizeOverride("font_size", 12);
        _chronicleBadge.AddChild(_chronicleBadgeLabel);
        _chronicleBadge.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _chronicleBadge.GrowHorizontal = Control.GrowDirection.Begin;
        _chronicleBadge.OffsetTop = 1;
        _chronicleBadge.OffsetRight = -1;
        _btnChronicle.AddChild(_chronicleBadge);
        rightRow.AddChild(_btnDecrees);
        rightRow.AddChild(_btnDiplomacy);
        rightRow.AddChild(_btnChronicle);
        rightRow.AddChild(_btnMenu);
        _topRight.AddChild(rightRow);
        _hudRoot.AddChild(_topRight);
    }

    private void LayoutTopBar()
    {
        if (_civCard is null || _timeCluster is null || _topRight is null || _pausedPill is null) return;
        float m = Margin;
        float civWidth = _compact ? 233 : 377;
        AnchorTo(_civCard, 0, 0, 0, 0, m, m, m + civWidth, m + TopBandHeight);
        _civCard.CustomMinimumSize = new Vector2(civWidth, TopBandHeight);
        if (_civAgeBlock is not null) _civAgeBlock.Visible = true;
        if (_civDivider is not null) _civDivider.Visible = !_compact;
        if (_ageBar is not null) _ageBar.Visible = !_compact;
        if (_ageCountLabel is not null) _ageCountLabel.Visible = !_compact;
        if (_civSettlementsLabel is not null) _civSettlementsLabel.Visible = !_compact;

        AnchorTo(_timeCluster, 0.5f, 0, 0.5f, 0, 0, m, 0, m + TopBandHeight, Control.GrowDirection.Both);
        AnchorTo(_pausedPill, 0.5f, 0, 0.5f, 0, 0, m + TopBandHeight + 8, 0, m + TopBandHeight + 44, Control.GrowDirection.Both);
        AnchorTo(_topRight, 1, 0, 1, 0, 0, m, -m, m + TopBandHeight, Control.GrowDirection.Begin);

        foreach (UiButton? button in new[] { _btnDecrees, _btnDiplomacy, _btnChronicle, _btnMenu })
        {
            if (button is null) continue;
            string label = button == _btnDecrees ? "Decrees" : button == _btnDiplomacy ? "Diplomacy" : button == _btnChronicle ? "Chronicle" : "Menu";
            UiGlyph glyph = button == _btnDecrees ? UiGlyph.People : button == _btnDiplomacy ? UiGlyph.Flags : button == _btnChronicle ? UiGlyph.Scroll : UiGlyph.Menu;
            if (_compact)
            {
                button.Text = "";
                button.ThemeTypeVariation = "IconButton";
                button.GlyphSize = 26;
                button.CustomMinimumSize = new Vector2(UiTokens.ControlM, UiTokens.ControlM);
            }
            else
            {
                button.Text = label;
                button.ThemeTypeVariation = "GhostIcon";
                button.GlyphSize = 16;
                button.CustomMinimumSize = new Vector2(0, UiTokens.ControlM);
            }
            button.Glyph = glyph;
        }
        UpdatePausedTreatment();
    }

    private void RefreshTopBar(MatchFrame frame)
    {
        if (_civNameLabel is null || _session is null) return;
        int civ = Math.Clamp(frame.ViewerCiv, 0, Math.Max(0, frame.CivCount - 1));
        _civEmblem!.Civ = civ;
        UiKit.SetText(_civNameLabel, UiText.CivName(civ).ToUpperInvariant());
        int people = frame.CivPopulation[civ];
        int settlements = frame.CivSettlements[civ];
        UiKit.SetText(_civPeopleLabel!, people.ToString("N0"));
        UiKit.SetText(_civSettlementsLabel!, $"people · {settlements} {(settlements == 1 ? "settlement" : "settlements")}");
        int era = frame.CivEra[civ];
        UiKit.SetText(_ageLabel!, UiText.AgeName(era).ToUpperInvariant() + " AGE");
        int done = frame.CivBreadthDone[civ], needed = Math.Max(1, (int)frame.CivBreadthNeeded[civ]);
        UiKit.SetText(_ageCountLabel!, $"{Math.Min(done, needed)}/{needed}");
        _ageBar!.SetValue(Math.Min(1.0f, done / (float)needed), false);
        UiKit.SetText(_yearLabel!, YearText(frame));
        int seasonsLeft = _session.ChapterSeasonsLeft;
        if (seasonsLeft >= 0)
        {
            int yearsLeft = Math.Max(1, (seasonsLeft + 3) / 4);
            UiKit.SetText(_seasonLabel!, $"IRON AGE · {yearsLeft} {(yearsLeft == 1 ? "YEAR" : "YEARS")} LEFT");
            _seasonLabel!.AddThemeColorOverride("font_color", UiTokens.Warning);
            _seasonLabel.TooltipText = "The match ends when your people reach the Iron Age, in this countdown, or at the Year 64 cap.";
        }
        else
        {
            UiKit.SetText(_seasonLabel!, UiText.SeasonNames[frame.SeasonOfYear & 3].ToUpperInvariant());
            _seasonLabel!.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
            _seasonLabel.TooltipText = "The current season.";
        }
        _lastSeasonOfYear = frame.SeasonOfYear;
        _seasonDial!.Set(frame.SeasonOfYear & 3, frame.TickInSeason / 150.0f);

        bool replay = _session.IsReplay;
        UiKit.SetVisible(_replayChip!, replay);
        if (_speedControl is not null)
        {
            double speed = _session.Speed;
            int index = speed < 0.75 ? 0 : speed < 1.5 ? 1 : speed < 3 ? 2 : 3;
            _speedControl.Select(index, false);
        }
        string rank = LegacyRankText(frame, civ);
        string tip = $"Your people. Next age: a turning point moves you on once enough {UiText.AgeName(era)} projects are known and its own needs are met.{rank}";
        _civCard!.TooltipText = tip;
        _civCard.MouseFilter = Control.MouseFilterEnum.Pass;
    }

    private static string LegacyRankText(MatchFrame frame, int civ)
    {
        long mine = frame.CivLegacy[civ];
        int rank = 1;
        for (int i = 0; i < frame.CivCount; i++) if (i != civ && frame.CivLegacy[i] > mine) rank++;
        string[] ordinal = { "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th" };
        return $" Legacy {FormatFixed(mine)} · {ordinal[Math.Min(rank - 1, 7)]} of {frame.CivCount}.";
    }

    private void RefreshSeasonDial()
    {
        if (_current is null || _seasonDial is null || _session is null) return;
        // The frame is published every 10 ticks; interpolate the arc between frames so it reads as continuous.
        double renderTick = Math.Min(_renderTick, _current.Tick);
        double inSeason = _current.TickInSeason + (renderTick - _current.Tick);
        _seasonDial.Set(_current.SeasonOfYear & 3, (float)Math.Clamp(inSeason / 150.0, 0, 1));
    }

    private void TogglePause()
    {
        if (_session is null || _session.MatchEnded || _session.ReplayFinished) return;
        _session.TogglePaused();
        _openingHold = false;
        _openingHoldAssignmentPending = false;
        UpdatePausedTreatment();
    }

    private void UpdatePauseButton()
    {
        if (_pauseButton is null || _session is null) return;
        bool finished = _session.MatchEnded || _session.ReplayFinished;
        bool paused = _session.Paused;
        UiKit.SetText(_pauseButton, finished ? "Match over" : paused ? "Resume" : "Pause");
        _pauseButton.Glyph = paused && !finished ? UiGlyph.Play : UiGlyph.Pause;
        _pauseButton.Disabled = finished;
        UiKit.SetVariation(_pauseButton, paused && !finished ? "PrimaryIcon" : "SecondaryIcon");
        _pauseButton.TooltipText = finished
            ? "This match has ended."
            : paused ? "Resume (Space)." : "Pause (Space). You can still choose research and give orders.";
    }

    private void UpdatePausedPill(bool paused)
    {
        if (_pausedPill is null || _pausedPillLabel is null) return;
        bool hide = _researchWebVisible || _ui.AnyModal;
        UiKit.SetText(_pausedPillLabel, _openingHold
            ? "PAUSED · choose a first project, or press Space to start"
            : "PAUSED · Space to resume");
        bool show = paused && !hide;
        if (_pausedPill.Visible != show) UiKit.Fade(_pausedPill, show);
    }
}
