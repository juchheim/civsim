using System;
using CivSim.Game.Ui;
using CivSim.Sim.Core;
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
    private UiLogoButton? _pauseLogo;
    private UiSegmented? _speedControl;
    private UiButton? _btnDecrees, _btnDiplomacy, _btnDirection, _btnChronicle, _btnMenu;
    private PanelContainer? _chronicleBadge;
    private Label? _chronicleBadgeLabel;
    private Control? _civDivider, _civAgeBlock, _civStatsBlock, _dateColumn;
    private HBoxContainer? _timeLeftWing, _timeRightWing;
    private bool _topRightIconsOnly;

    /// <summary>Measured wing width (left = right) of the time cluster at the last layout, for the --ui-debug report.</summary>
    private float _timeWingWidth;
    private int _lastSeasonOfYear = -1;
    private const int LogoSize = 90, LogoSlotWidth = 72;

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
        _peopleTopButton = UiKit.IconButton(UiGlyph.People, "People & Stores (Y): what slows your people down, what they hold, who does what", OpenPeoplePanel, 30, 16);
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
        // The whole card opens People & Stores (Y): a 377 × 64 target instead of the 30 × 26 icon it used to be (ui-ux-pass2 §4).
        _civCard.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _civCard.MouseEntered += () => UiKit.SetVariation(_civCard, "HudPanelHover");
        _civCard.MouseExited += () => UiKit.SetVariation(_civCard, "HudPanel");
        _civCard.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { OpenPeoplePanel(); _civCard.AcceptEvent(); }
        };
        _hudRoot!.AddChild(_civCard);

        // Time cluster: two wings of equal width either side of the logo, so the logo sits on the screen's centre line.
        // The left wing holds the season dial and the date; the right wing the speed control (and the replay chip).
        _timeCluster = UiKit.Panel("TimeClusterPanel", "TimeCluster");
        _timeCluster.CustomMinimumSize = new Vector2(0, TopBandHeight);
        var timeRow = UiKit.Row(UiTokens.Sp3);
        _timeLeftWing = UiKit.Row(UiTokens.Sp3);
        _timeLeftWing.Name = "TimeLeftWing";
        _timeLeftWing.MouseFilter = Control.MouseFilterEnum.Ignore;
        _timeLeftWing.Alignment = BoxContainer.AlignmentMode.Center;
        _seasonDial = new UiSeasonDial();
        _timeLeftWing.AddChild(_seasonDial);
        _dateColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _dateColumn.AddThemeConstantOverride("separation", -3);
        _yearLabel = UiKit.Text("Year 1", "NumYear");
        _seasonLabel = UiKit.Text("SPRING", "Eyebrow", ellipsis: true);
        _dateColumn.AddChild(_yearLabel);
        _dateColumn.AddChild(_seasonLabel);
        _timeLeftWing.AddChild(_dateColumn);
        timeRow.AddChild(_timeLeftWing);
        // The logo is larger than the band: it hangs 13 px over the top and bottom edges of the cluster panel.
        _pauseLogo = new UiLogoButton(TogglePause, LogoSize) { Name = "PauseLogo", TooltipText = "Pause (Space). You can still choose research and give orders." };
        var logoSlot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(LogoSlotWidth, TopBandHeight - 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        logoSlot.AddChild(_pauseLogo);
        AnchorTo(_pauseLogo, 0.5f, 0.5f, 0.5f, 0.5f, -LogoSize / 2f, -LogoSize / 2f, LogoSize / 2f, LogoSize / 2f, Control.GrowDirection.Both, Control.GrowDirection.Both);
        timeRow.AddChild(logoSlot);
        _timeRightWing = UiKit.Row(UiTokens.Sp3);
        _timeRightWing.Name = "TimeRightWing";
        _timeRightWing.MouseFilter = Control.MouseFilterEnum.Ignore;
        _timeRightWing.Alignment = BoxContainer.AlignmentMode.Center;
        _speedControl = new UiSegmented(new[] { "½×", "1×", "2×", "4×" }, 1, index => _session?.SetSpeedIndex(index), 42);
        _speedControl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        for (int i = 0; i < 4; i++) _speedControl.SetTooltip(i, "Game speed ([ and ])");
        _timeRightWing.AddChild(_speedControl);
        _replayChip = UiKit.Chip("REPLAY", UiGlyph.Play, UiTokens.Info, "ChipPanelInfo");
        _replayChip.Visible = false;
        _replayChip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _timeRightWing.AddChild(_replayChip);
        timeRow.AddChild(_timeRightWing);
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
        _btnDecrees = UiKit.Ghost("Decrees", () => ToggleDecrees(), "Mandate and civic orders", UiGlyph.Lock, UiTokens.ControlM);
        _btnDiplomacy = UiKit.Ghost("Diplomacy", () => ToggleDiplomacy(), "Your neighbours and your orders (O)", UiGlyph.Flags, UiTokens.ControlM);
        _btnDirection = UiKit.Ghost("Direction", ToggleCivilizationDirection, "Set your civilization's direction", UiGlyph.CompassRose, UiTokens.ControlM);
        _btnChronicle = UiKit.Ghost("Chronicle", () => ToggleChronicle(), "Your people's story (C)", UiGlyph.Scroll, UiTokens.ControlM);
        _btnMenu = UiKit.Ghost("Menu", () => ToggleGameMenu(), "Save, load, replays and settings (Esc)", UiGlyph.Menu, UiTokens.ControlM);
        _chronicleBadge = UiKit.Panel("BadgePanel", null, UiKit.MouseFilter.Ignore);
        _chronicleBadge.Visible = false;
        _chronicleBadgeLabel = UiKit.Text("1", "NumBadge", UiTokens.TextOnAccent);
        _chronicleBadge.AddChild(_chronicleBadgeLabel);
        _chronicleBadge.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _chronicleBadge.GrowHorizontal = Control.GrowDirection.Begin;
        _chronicleBadge.OffsetTop = 1;
        _chronicleBadge.OffsetRight = -1;
        _btnChronicle.AddChild(_chronicleBadge);
        rightRow.AddChild(_btnDecrees);
        rightRow.AddChild(_btnDiplomacy);
        rightRow.AddChild(_btnDirection);
        rightRow.AddChild(_btnChronicle);
        rightRow.AddChild(_btnMenu);
        _topRight.AddChild(rightRow);
        _hudRoot.AddChild(_topRight);
        UpdateDecreesButton();
    }

    private void LayoutTopBar()
    {
        if (_civCard is null || _timeCluster is null || _topRight is null || _pausedPill is null) return;
        float m = Margin;
        if (_civAgeBlock is not null) _civAgeBlock.Visible = true;
        if (_civDivider is not null) _civDivider.Visible = !_compact;
        if (_ageBar is not null) _ageBar.Visible = !_compact;
        if (_ageCountLabel is not null) _ageCountLabel.Visible = !_compact;
        if (_civSettlementsLabel is not null) _civSettlementsLabel.Visible = !_compact;

        AnchorTo(_pausedPill, 0.5f, 0, 0.5f, 0, 0, m + TopBandHeight + 16, 0, m + TopBandHeight + 52, Control.GrowDirection.Both);
        AnchorTo(_topRight, 1, 0, 1, 0, 0, m, -m, m + TopBandHeight, Control.GrowDirection.Begin);

        // Wing width: what the longest date ("Year 888" over "16 YEARS LEFT") needs, and never less than the speed side.
        float dateWidth = Math.Max(
            UiTheme.DisplayNum.GetStringSize("Year 888", HorizontalAlignment.Left, -1, UiTokens.FontYear).X,
            UiTheme.Caps.GetStringSize("16 YEARS LEFT", HorizontalAlignment.Left, -1, UiTokens.FontSmall).X);
        dateWidth = MathF.Ceiling(dateWidth) + 2;
        float dialWidth = _seasonDial?.CustomMinimumSize.X ?? 34;
        float speedWidth = _speedControl?.GetCombinedMinimumSize().X ?? 170;
        // In a replay the REPLAY chip joins the speed control, and both wings widen together.
        float replayWidth = _replayChip is { Visible: true } chip ? UiTokens.Sp3 + chip.GetCombinedMinimumSize().X : 0;
        float wing = Math.Max(dialWidth + UiTokens.Sp3 + dateWidth, speedWidth + replayWidth);
        const float clusterChrome = UiTokens.Sp3 * 2 + UiTokens.Sp3 * 2 + LogoSlotWidth;              // panel padding + row gaps + logo slot

        // Room left between the civ card and the top-right cluster, measured from the centre line.
        // Compact windows give the civ card whatever the centred cluster leaves (233 to 296, √φ steps), so the people's name fits.
        float half = _logicalSize.X / 2f;
        float civWidth = _compact ? Math.Clamp(MathF.Floor(half - m - UiTokens.Sp3 - clusterChrome / 2f - wing), 233, 296) : 377;
        AnchorTo(_civCard, 0, 0, 0, 0, m, m, m + civWidth, m + TopBandHeight);
        _civCard.CustomMinimumSize = new Vector2(civWidth, TopBandHeight);
        float civActual = Math.Max(civWidth, _civCard.GetCombinedMinimumSize().X);
        float leftRoom = half - (m + civActual) - UiTokens.Sp3;
        float labelled = TopRightLabelledWidth();
        float iconOnly = 5 * UiTokens.ControlM + 4 * UiTokens.Sp1 + 2 * UiTokens.Sp3;
        _topRightIconsOnly = _compact || half - (m + labelled) - UiTokens.Sp3 < clusterChrome / 2f + wing;
        float rightRoom = half - (m + (_topRightIconsOnly ? iconOnly : labelled)) - UiTokens.Sp3;
        float maxWing = Math.Min(leftRoom, rightRoom) - clusterChrome / 2f;
        if (wing > maxWing)
        {
            // Too narrow for the full date: keep the wings equal and let the season line ellipsize.
            wing = Math.Max(Math.Max(dialWidth + UiTokens.Sp3 + 89, speedWidth + replayWidth), MathF.Floor(maxWing));
            dateWidth = wing - dialWidth - UiTokens.Sp3;
        }
        _timeWingWidth = wing;
        if (_dateColumn is not null) _dateColumn.CustomMinimumSize = new Vector2(dateWidth, 0);
        if (_timeLeftWing is not null) _timeLeftWing.CustomMinimumSize = new Vector2(wing, 0);
        if (_timeRightWing is not null) _timeRightWing.CustomMinimumSize = new Vector2(wing, 0);
        float clusterWidth = 2 * wing + clusterChrome;
        AnchorTo(_timeCluster, 0.5f, 0, 0.5f, 0, -clusterWidth / 2f, m, clusterWidth / 2f, m + TopBandHeight, Control.GrowDirection.Both);
        if (HasArgument("--ui-debug"))
            GD.Print($"UIDEBUG topbar logical={_logicalSize} wing={wing} cluster={clusterWidth} leftRoom={leftRoom} rightRoom={rightRoom} iconsOnly={_topRightIconsOnly}");

        foreach (UiButton? button in new[] { _btnDecrees, _btnDiplomacy, _btnDirection, _btnChronicle, _btnMenu })
        {
            if (button is null) continue;
            string label = button == _btnDecrees ? "Decrees" : button == _btnDiplomacy ? "Diplomacy" :
                button == _btnDirection ? "Direction" : button == _btnChronicle ? "Chronicle" : "Menu";
            UiGlyph glyph = button == _btnDecrees ? DecreesGlyph : button == _btnDiplomacy ? UiGlyph.Flags :
                button == _btnDirection ? UiGlyph.CompassRose : button == _btnChronicle ? UiGlyph.Scroll : UiGlyph.Menu;
            if (_topRightIconsOnly)
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
        UpdateDecreesButton();
        UpdatePausedTreatment();
    }

    /// <summary>Decrees wear a seal; until they unlock, a lock, so the button says "not yet" before it is pressed.</summary>
    /// <remarks>With labels the lock reads "Decrees, locked"; as a lone icon a lock says nothing, so there the seal stays and dims.</remarks>
    private UiGlyph DecreesGlyph => _decreesUnlocked || _topRightIconsOnly ? UiGlyph.Seal : UiGlyph.Lock;

    private void UpdateDecreesButton()
    {
        if (_btnDecrees is null) return;
        _btnDecrees.Glyph = DecreesGlyph;
        _btnDecrees.SelfModulate = !_decreesUnlocked && _topRightIconsOnly ? new Color(1, 1, 1, 0.6f) : Colors.White;
        _btnDecrees.TooltipText = _decreesUnlocked
            ? "Decrees: found villages, explore, mine, levy, compacts and corvée (spends Mandate)"
            : $"Decrees unlock with {DecreeUnlockProjectName()}. Click to see what they will do.";
    }

    /// <summary>Width of the top-right cluster with its text labels (Decrees, Diplomacy, Chronicle, Menu).</summary>
    private static float TopRightLabelledWidth()
    {
        float width = 2 * UiTokens.Sp3 + 4 * UiTokens.Sp1;
        foreach (string label in new[] { "Decrees", "Diplomacy", "Direction", "Chronicle", "Menu" })
            width += UiTheme.Medium.GetStringSize(label, HorizontalAlignment.Left, -1, UiTokens.FontBody).X + 2 * UiTokens.Sp3 + 16 + UiTokens.Sp1;
        return MathF.Ceiling(width);
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
        _ageCountLabel!.TooltipText = $"Only completed regular {UiText.AgeName(era)} Age projects add to the next-age count. Required older-age projects unlock the route but don't add to this count. Projects in progress don't count yet.";
        _ageBar!.SetValue(Math.Min(1.0f, done / (float)needed), false);
        UiKit.SetText(_yearLabel!, YearText(frame));
        int seasonsLeft = _session.ChapterSeasonsLeft;
        if (seasonsLeft >= 0)
        {
            int yearsLeft = Math.Max(1, (seasonsLeft + 3) / 4);
            UiKit.SetText(_seasonLabel!, $"{yearsLeft} {(yearsLeft == 1 ? "YEAR" : "YEARS")} LEFT");
            _seasonLabel!.AddThemeColorOverride("font_color", UiTokens.Warning);
            _seasonLabel.TooltipText = $"A rival has reached the {UiText.AgeName(_session.IsIron ? 5 : 4)} age. The match ends when your people get there too, or when this countdown runs out.";
        }
        else
        {
            UiKit.SetText(_seasonLabel!, UiText.SeasonNames[frame.SeasonOfYear & 3].ToUpperInvariant());
            _seasonLabel!.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
            _seasonLabel.TooltipText = "The current season.";
        }
        _lastSeasonOfYear = frame.SeasonOfYear;
        _seasonDial!.Set(frame.SeasonOfYear & 3, frame.TickInSeason / 150.0f);
        RefreshCivilizationDirectionUi(frame);

        bool replay = _session.IsReplay;
        if (_replayChip!.Visible != replay)
        {
            _replayChip.Visible = replay;
            LayoutTopBar();
        }
        if (_speedControl is not null)
        {
            double speed = _session.Speed;
            int index = speed < 0.75 ? 0 : speed < 1.5 ? 1 : speed < 3 ? 2 : 3;
            _speedControl.Select(index, false);
        }
        string rank = LegacyRankText(frame, civ);
        string tip = $"Your people. Next age: complete {needed} regular {UiText.AgeName(era)} Age projects and meet the turning point's prerequisites. A star adds to this count; a link marks a required prerequisite or one possible route, as shown in Needs.{rank}";
        _civCard!.TooltipText = "Click for People & Stores (Y). " + tip;
        _civCard.MouseFilter = Control.MouseFilterEnum.Stop;
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

    /// <summary>The animated logo is the pause control: it plays while time runs and rests (with a gold ring) while paused.</summary>
    private void UpdatePauseLogo()
    {
        if (_pauseLogo is null || _session is null) return;
        bool finished = _session.MatchEnded || _session.ReplayFinished;
        bool paused = _session.Paused;
        if (_pauseLogo.Disabled != finished) _pauseLogo.Disabled = finished;
        bool playing = !finished && !paused;
        if (_pauseLogo.Playing != playing) _pauseLogo.Playing = playing;
        bool ring = paused && !finished;
        if (_pauseLogo.Highlight != ring) _pauseLogo.Highlight = ring;
        string tip = finished
            ? "This match has ended."
            : paused ? "Resume (Space)." : "Pause (Space). You can still choose research and give orders.";
        if (_pauseLogo.TooltipText != tip) _pauseLogo.TooltipText = tip;
    }

    private void UpdatePausedPill(bool paused)
    {
        if (_pausedPill is null || _pausedPillLabel is null) return;
        bool hide = _researchWebVisible || _ui.AnyModal;
        int queued = _session?.PendingCommands.Length ?? 0;
        string pending = queued == 0 ? "" : queued == 1 ? " · 1 change queued" : $" · {queued} changes queued";
        UiKit.SetText(_pausedPillLabel, _openingHold
            ? $"PAUSED · choose a first project{pending}"
            : $"PAUSED{pending} · Space to resume");
        bool show = paused && !hide;
        if (_pausedPill.Visible != show) UiKit.Fade(_pausedPill, show);
    }
}
