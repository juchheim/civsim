using System;
using System.Diagnostics;
using CivSim.Game.Ui;
using CivSim.Sim.Research;
using ResearchTheme = CivSim.Sim.Research.Theme;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The three research slots, always on screen: φ-weighted cards, live share, progress and ETA (spec §3.3).</summary>
public partial class Game
{
    private enum SlotMode { Open, Working, Pending, Done }

    private sealed class SlotCardView
    {
        public Control Root = null!;
        public PanelContainer Panel = null!;
        public UiRankBadge Badge = null!;
        public Label Eyebrow = null!, Eta = null!, Name = null!, Cta = null!, Hint = null!, Percent = null!;
        public UiIcon ThemeIcon = null!, DoneIcon = null!;
        public UiProgressBar Bar = null!;
        public UiDashedBorder Dashed = null!;
        public ColorRect ThemeBar = null!;
        public Control Content = null!;
        public Control BarRow = null!;
        public SlotMode Mode = SlotMode.Open;
        public int Project = -2;
        public float DoneTimer;
        public int DoneProject = -1;
        public float OpenedAgo = 99;
    }

    private readonly SlotCardView[] _slotCards = new SlotCardView[3];
    private PanelContainer? _focusBar;
    private Control? _focusSummary;
    private UiButton? _researchButton;
    private UiToggle? _autoFillToggle;
    private Label? _insightLabel;
    private PanelContainer? _newBadge;
    private Label? _newBadgeLabel;
    private int _newProjectCount;
    private HBoxContainer? _slotRow;
    private UiPopover? _slotMenu;
    private int _slotMenuSlot = -1;
    private readonly int[] _pendingProject = { -2, -2, -2 };
    private readonly long[] _pendingTick = new long[3];
    private readonly int[] _lastSlotProject = { -2, -2, -2 };

    private readonly double[] _focusRatePermillePerSecond = new double[3];
    private readonly double[] _focusObservedAt = new double[3];
    private readonly int[] _focusObservedProject = { -1, -1, -1 };
    private readonly short[] _focusObservedPermille = new short[3];

    private void CreateFocusBar()
    {
        _focusBar = UiKit.Panel("HudPanel", "FocusBar");
        _focusBar.CustomMinimumSize = new Vector2(0, FocusBarHeight);
        var root = UiKit.Row(UiTokens.Sp3);

        // Summary column
        var summary = new VBoxContainer { CustomMinimumSize = new Vector2(233, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        summary.AddThemeConstantOverride("separation", UiTokens.Sp2);
        _focusSummary = summary;
        _researchButton = UiKit.Secondary("Research web", () => OpenResearchWeb(-1), "See every project and choose what to learn (T)", UiGlyph.Spark);
        _researchButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        summary.AddChild(_researchButton);
        _newBadge = UiKit.Panel("BadgePanel", null, UiKit.MouseFilter.Ignore);
        _newBadge.Visible = false;
        _newBadgeLabel = UiKit.Text("1 new", "Num", UiTokens.TextOnAccent);
        _newBadgeLabel.AddThemeFontSizeOverride("font_size", 12);
        _newBadge.AddChild(_newBadgeLabel);
        _newBadge.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _newBadge.GrowHorizontal = Control.GrowDirection.Begin;
        _newBadge.OffsetTop = -8;
        _newBadge.OffsetRight = 4;
        _researchButton.AddChild(_newBadge);
        var lower = UiKit.Row(UiTokens.Sp2);
        lower.MouseFilter = Control.MouseFilterEnum.Ignore;
        _autoFillToggle = new UiToggle("Auto-fill", false, enabled =>
        {
            if (!_updatingFocusBar) SetAutoFill(enabled);
        })
        {
            TooltipText = "When a slot opens, the elders pick a project after one season. Your own choices stay.",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _autoFillToggle.ThemeTypeVariation = "ToggleRowCompact";
        lower.AddChild(_autoFillToggle);
        _insightLabel = UiKit.Text("— Insight/yr", "Num", right: true);
        _insightLabel.MouseFilter = Control.MouseFilterEnum.Stop;
        _insightLabel.TooltipText = "Insight is your research speed. More people, elders and learning raise it.";
        lower.AddChild(_insightLabel);
        summary.AddChild(lower);
        root.AddChild(summary);

        _slotRow = UiKit.Row(UiTokens.Sp3);
        for (int slot = 0; slot < 3; slot++) _slotRow.AddChild(BuildSlotCard(slot).Root);
        root.AddChild(_slotRow);
        _focusBar.AddChild(root);
        _hudRoot!.AddChild(_focusBar);

        _slotMenu = new UiPopover("CardPanel");
        _slotMenu.Dismissed = CloseSlotMenu;
        _hudRoot.AddChild(_slotMenu);
    }

    private bool _updatingFocusBar;

    private SlotCardView BuildSlotCard(int slot)
    {
        var view = new SlotCardView();
        var card = new PanelContainer { ThemeTypeVariation = "SlotCard" };
        view.Panel = card;
        Control wrapper = UiKit.WrapOverlay(card, new Vector2(200, 89));
        wrapper.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        wrapper.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true } button) return;
            if (button.ButtonIndex == MouseButton.Left) OnSlotClicked(slot);
            else if (button.ButtonIndex == MouseButton.Right) OpenSlotMenu(slot, button.GlobalPosition);
        };
        view.Root = wrapper;

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        view.Content = column;

        var row1 = UiKit.Row(UiTokens.Sp2);
        row1.MouseFilter = Control.MouseFilterEnum.Ignore;
        view.Badge = new UiRankBadge(slot + 1);
        view.DoneIcon = new UiIcon(UiGlyph.CheckBadge, 20, UiTokens.Success) { Visible = false };
        row1.AddChild(view.Badge);
        row1.AddChild(view.DoneIcon);
        view.Eyebrow = UiKit.Text(UiText.SlotNames[slot].ToUpperInvariant(), "Eyebrow", ellipsis: true);
        row1.AddChild(view.Eyebrow);
        view.Eta = UiKit.Text("", "Num", minWidth: 60, right: true);
        row1.AddChild(view.Eta);
        column.AddChild(row1);

        var row2 = UiKit.Row(UiTokens.Sp2);
        row2.MouseFilter = Control.MouseFilterEnum.Ignore;
        view.ThemeIcon = new UiIcon(UiGlyph.Hammer, 16, UiTokens.ThemeColor(ResearchThemeIdx(0)));
        row2.AddChild(view.ThemeIcon);
        view.Name = UiKit.Text("", "Heading", ellipsis: true);
        row2.AddChild(view.Name);
        view.Cta = UiKit.Text("+ Choose a project", "BodyStrong", UiTokens.Accent, ellipsis: true);
        row2.AddChild(view.Cta);
        column.AddChild(row2);

        var row3 = UiKit.Row(UiTokens.Sp2);
        row3.MouseFilter = Control.MouseFilterEnum.Ignore;
        view.BarRow = row3;
        view.Bar = new UiProgressBar(6);
        row3.AddChild(view.Bar);
        view.Percent = UiKit.Text("0%", "Num", minWidth: 36, right: true);
        row3.AddChild(view.Percent);
        view.Hint = UiKit.Text("", "CaptionTertiary", ellipsis: true);
        row3.AddChild(view.Hint);
        column.AddChild(row3);
        card.AddChild(column);

        view.ThemeBar = new ColorRect { Color = UiTokens.ThemeColor(ResearchThemeIdx(0)), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        view.ThemeBar.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        view.ThemeBar.OffsetRight = 3;
        view.ThemeBar.OffsetTop = 6;
        view.ThemeBar.OffsetBottom = -6;
        wrapper.AddChild(view.ThemeBar);
        view.Dashed = new UiDashedBorder(UiTokens.Accent) { Visible = false };
        wrapper.AddChild(view.Dashed);
        _slotCards[slot] = view;
        return view;
    }

    private static ResearchTheme ResearchThemeIdx(int index) => (ResearchTheme)index;

    private void LayoutFocusBar()
    {
        if (_focusBar is null || _slotRow is null || _focusSummary is null) return;
        float m = Margin;
        float width = _compact ? _logicalSize.X - 2 * m : Math.Min(1508, _logicalSize.X - 2 * m);
        AnchorTo(_focusBar, 0.5f, 1, 0.5f, 1, -width / 2, -(m + FocusBarHeight), width / 2, -m, Control.GrowDirection.Both, Control.GrowDirection.Begin);
        float summary = _compact ? 199 : 233;
        _focusSummary.CustomMinimumSize = new Vector2(summary, 0);
        float inner = width - 2 * UiTokens.Sp3 - summary - UiTokens.Sp3 - 2 * UiTokens.Sp3;
        float tertiary = Math.Max(200, inner * 0.191f);
        float secondary = Math.Max(233, inner * 0.309f);
        float primary = Math.Max(233, inner - tertiary - secondary);
        float[] widths = { primary, secondary, tertiary };
        for (int i = 0; i < 3; i++)
        {
            _slotCards[i].Root.CustomMinimumSize = new Vector2(widths[i], 89);
            _slotCards[i].Root.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        }
    }

    private void RefreshFocusBar(MatchFrame frame)
    {
        if (_catalog is null || _session is null) return;
        _updatingFocusBar = true;
        ObserveFocusProgress(frame);

        long insightRaw = frame.FocusCiv >= 0 && frame.FocusCiv < frame.CivCount ? frame.CivInsightPerYear[frame.FocusCiv] : 0;
        string insight = UiText.FormatInsight(insightRaw);
        UiKit.SetText(_insightLabel!, $"{insight} Insight/yr");
        _insightLabel!.TooltipText = "Insight is your research speed. More people, elders and learning raise it." + (frame.FocusQueueCount > 0 ? $" {frame.FocusQueueCount} waiting in the queue." : "");
        _autoFillToggle!.SetPressedNoSignal(frame.FocusCouncil);
        _autoFillToggle.Disabled = frame.FocusCiv != 0 || _session.IsReplay;

        int weightSum = 0;
        for (int slot = 0; slot < 3; slot++) if (frame.FocusSlotProject[slot] >= 0) weightSum += UiText.SlotWeight[slot];
        for (int slot = 0; slot < 3; slot++)
        {
            int project = frame.FocusSlotProject[slot];
            if (_pendingProject[slot] != -2 && frame.Tick > _pendingTick[slot]) _pendingProject[slot] = -2;
            if (project != _lastSlotProject[slot])
            {
                if (_lastSlotProject[slot] != -2 && !_slotDoneFlashActive(slot)) PulseCard(slot);
                if (project < 0 && _lastSlotProject[slot] >= 0) _slotCards[slot].OpenedAgo = 0;
                _lastSlotProject[slot] = project;
            }
            UpdateSlotCard(slot, frame, project, weightSum);
        }
        _updatingFocusBar = false;
    }

    private bool _slotDoneFlashActive(int slot) => _slotCards[slot].DoneTimer > 0;

    private void PulseCard(int slot)
    {
        SlotCardView view = _slotCards[slot];
        if (UiSettings.Current.ReduceMotion || !view.Content.IsInsideTree()) return;
        view.Content.Modulate = new Color(1, 1, 1, 0.3f);
        Tween tween = view.Content.CreateTween();
        tween.TweenProperty(view.Content, "modulate:a", 1.0f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void UpdateSlotCard(int slot, MatchFrame frame, int project, int weightSum)
    {
        SlotCardView view = _slotCards[slot];
        bool pending = _pendingProject[slot] != -2;
        int shownProject = pending && _pendingProject[slot] >= 0 ? _pendingProject[slot] : project;
        SlotMode mode;
        if (view.DoneTimer > 0) mode = SlotMode.Done;
        else if (pending && _pendingProject[slot] >= 0) mode = SlotMode.Pending;
        else if (project >= 0) mode = SlotMode.Working;
        else mode = SlotMode.Open;
        if (pending && _pendingProject[slot] == -1) mode = SlotMode.Open;
        if (mode == SlotMode.Done) shownProject = view.DoneProject;

        string eyebrow = UiText.SlotNames[slot].ToUpperInvariant();
        bool hasProject = shownProject >= 0 && shownProject < _catalog!.Count;
        ProjectDef? def = hasProject ? _catalog!.Projects[shownProject] : null;

        if (mode != view.Mode || shownProject != view.Project)
        {
            view.Mode = mode;
            view.Project = shownProject;
            string variation = mode switch { SlotMode.Open => "SlotCardOpen", SlotMode.Pending => "SlotCardPending", SlotMode.Done => "SlotCardDone", _ => "SlotCard" };
            UiKit.SetVariation(view.Panel, variation);
            UiKit.SetVisible(view.Dashed, mode == SlotMode.Open);
            UiKit.SetVisible(view.ThemeBar, mode == SlotMode.Working && def is not null);
            UiKit.SetVisible(view.DoneIcon, mode == SlotMode.Done);
            UiKit.SetVisible(view.Badge, mode != SlotMode.Done);
            UiKit.SetVisible(view.ThemeIcon, def is not null);
            UiKit.SetVisible(view.Name, def is not null);
            UiKit.SetVisible(view.Cta, def is null);
            if (def is not null)
            {
                view.ThemeIcon.Glyph = UiDraw.ThemeGlyph(def.Theme);
                view.ThemeIcon.Tint = UiTokens.ThemeText(def.Theme);
                view.ThemeBar.Color = UiTokens.ThemeColor(def.Theme);
                view.Bar.Fill = UiTokens.ThemeColor(def.Theme);
                UiKit.SetText(view.Name, def.Name);
            }
            else view.Bar.Fill = UiTokens.TextTertiary;
            view.Dashed.Color = frame.FocusCouncil ? UiTokens.TextTertiary : UiTokens.Accent;
        }

        bool showBar = mode is SlotMode.Working or SlotMode.Done;
        UiKit.SetVisible(view.Bar, showBar);
        UiKit.SetVisible(view.Percent, showBar);
        UiKit.SetVisible(view.Hint, !showBar);

        switch (mode)
        {
            case SlotMode.Working:
            case SlotMode.Done:
                {
                    int permille = mode == SlotMode.Done ? 1000 : frame.FocusSlotPermille[slot];
                    int share = weightSum > 0 ? (int)Math.Round(100.0 * UiText.SlotWeight[slot] / weightSum) : 0;
                    UiKit.SetText(view.Eyebrow, mode == SlotMode.Done ? $"{eyebrow} · DONE" : $"{eyebrow} · {share}%");
                    view.Bar.SetValue(permille / 1000f);
                    UiKit.SetText(view.Percent, $"{permille / 10}%");
                    view.Root.TooltipText = $"{UiText.SlotNames[slot]} gets {UiText.SlotShare[slot]} research points ({share}% now). Empty slots pass their share to the others. Click to change it, right-click for more.";
                    if (mode == SlotMode.Done) UiKit.SetText(view.Eta, "");
                    break;
                }
            case SlotMode.Pending:
                UiKit.SetText(view.Eyebrow, eyebrow);
                UiKit.SetText(view.Hint, "Starts when you resume");
                view.Hint.AddThemeColorOverride("font_color", UiTokens.Accent);
                UiKit.SetText(view.Eta, "");
                view.Root.TooltipText = "You chose this while paused. It starts when you resume.";
                break;
            default:
                UiKit.SetText(view.Eyebrow, eyebrow);
                if (frame.FocusCouncil)
                {
                    UiKit.SetText(view.Cta, "The elders will choose soon");
                    view.Cta.AddThemeColorOverride("font_color", UiTokens.TextSecondary);
                    UiKit.SetText(view.Hint, "Or click to pick one yourself");
                }
                else
                {
                    UiKit.SetText(view.Cta, "+ Choose a project");
                    view.Cta.AddThemeColorOverride("font_color", UiTokens.Accent);
                    UiKit.SetText(view.Hint, "Its share goes to the others until you choose");
                }
                UiKit.SetText(view.Eta, "");
                view.Root.TooltipText = "This slot is open. Its share goes to the other slots until you choose.";
                break;
        }
        view.Badge.SetFill(mode == SlotMode.Open ? UiTokens.TextTertiary : new Color("#E2D3A6"));
    }

    private void TickFocusBar(float delta)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            SlotCardView view = _slotCards[slot];
            if (view is null) continue;
            if (view.DoneTimer > 0)
            {
                view.DoneTimer -= delta;
                if (view.DoneTimer <= 0 && _current is not null) { view.DoneTimer = 0; RefreshFocusBar(_current); }
            }
            if (view.OpenedAgo < 99) view.OpenedAgo += delta;
            bool pulse = view.Mode == SlotMode.Open && (!_uiSettings.PlayedBefore || view.OpenedAgo < 3.0f) && _current is { FocusCouncil: false };
            view.Dashed.SetPulse(pulse);
        }
    }

    private void FlashSlotDone(int project)
    {
        if (_current is null) return;
        for (int slot = 0; slot < 3; slot++)
            if (_lastSlotProject[slot] == project)
            {
                _slotCards[slot].DoneProject = project;
                _slotCards[slot].DoneTimer = 0.9f;
                _slotCards[slot].OpenedAgo = 0;
            }
    }

    private void RefreshEtas()
    {
        if (_current is null || _session is null) return;
        for (int slot = 0; slot < 3; slot++)
        {
            SlotCardView view = _slotCards[slot];
            if (view.Mode != SlotMode.Working) continue;
            UiKit.SetText(view.Eta, FormatEta(slot, _current.FocusSlotPermille[slot]));
        }
    }

    private void ObserveFocusProgress(MatchFrame frame)
    {
        double now = Stopwatch.GetTimestamp();
        for (int slot = 0; slot < 3; slot++)
        {
            int project = frame.FocusSlotProject[slot];
            short progress = frame.FocusSlotPermille[slot];
            if (project == _focusObservedProject[slot] && project >= 0 && progress > _focusObservedPermille[slot])
            {
                double elapsed = (now - _focusObservedAt[slot]) / Stopwatch.Frequency;
                if (elapsed > 0.05) _focusRatePermillePerSecond[slot] = (progress - _focusObservedPermille[slot]) / elapsed;
            }
            else if (project != _focusObservedProject[slot]) _focusRatePermillePerSecond[slot] = 0;
            if (project != _focusObservedProject[slot] || progress > _focusObservedPermille[slot])
            {
                _focusObservedProject[slot] = project;
                _focusObservedPermille[slot] = progress;
                _focusObservedAt[slot] = now;
            }
        }
    }

    private string FormatEta(int slot, short progress)
    {
        if (_session?.Paused == true) return "Paused";
        double rate = _focusRatePermillePerSecond[slot];
        if (rate <= 0.01) return "—";
        return "ETA " + UiText.Minutes((1000 - progress) / rate);
    }

    private void OnSlotClicked(int slot)
    {
        OpenResearchWeb(slot);
    }

    private void SetAutoFill(bool enabled)
    {
        if (_session is null || _current is null || !_session.CanIssueCommands) return;
        _session.Queue(PlayerCommands.SetCouncil(_session.Tick, _current.FocusCiv, enabled));
        ShowFooterNote(enabled
            ? "Auto-fill is on. The elders fill empty slots after one season; your choices stay."
            : "Auto-fill is off. Empty slots wait for you.");
    }

    // ---- right-click menu --------------------------------------------------------------------

    private UiLayer? _slotMenuLayer;

    private void OpenSlotMenu(int slot, Vector2 globalPosition)
    {
        if (_slotMenu is null || _current is null || _session is null || _catalog is null) return;
        _slotMenuSlot = slot;
        VBoxContainer column = UiKit.Column(2);
        void Row(string text, Action action, bool enabled = true, string? tip = null)
        {
            UiButton row = UiKit.Ghost(text, () => { CloseSlotMenu(); action(); }, tip);
            row.ThemeTypeVariation = "RowButton";
            row.Alignment = HorizontalAlignment.Left;
            row.Disabled = !enabled;
            row.CustomMinimumSize = new Vector2(233, UiTokens.ControlS);
            column.AddChild(row);
        }
        int project = _current.FocusSlotProject[slot];
        Row("Show in research", () => OpenResearchWeb(slot, project));
        Row("Move up", () => MoveSlot(slot, slot - 1), slot > 0 && project >= 0, project < 0 ? "This slot is open." : null);
        Row("Move down", () => MoveSlot(slot, slot + 1), slot < 2 && project >= 0, project < 0 ? "This slot is open." : null);
        Row("Stop · keeps progress", () => ClearSlot(slot), project >= 0, project < 0 ? "This slot is open." : null);
        foreach (Node child in _slotMenu.Body.GetChildren()) { _slotMenu.Body.RemoveChild(child); child.QueueFree(); }
        _slotMenu.Body.AddChild(column);
        float y = globalPosition.Y - 200;
        _slotMenu.Open(new Vector2(globalPosition.X - 20, Math.Max(8, _slotCards[slot].Root.GlobalPosition.Y - 4 * UiTokens.ControlS - 30)), 233);
        _slotMenuLayer = RegisterLayer("slotmenu", false, () => { CloseSlotMenu(); return true; });
        _ui.Push(_slotMenuLayer);
        if (y < 0) { }
    }

    private void CloseSlotMenu()
    {
        _slotMenu?.Close();
        if (_slotMenuLayer is not null) _ui.Remove(_slotMenuLayer);
        _slotMenuLayer = null;
    }

    private void MoveSlot(int from, int to)
    {
        if (_session is null || _current is null || (uint)to >= 3u) return;
        int mine = _current.FocusSlotProject[from];
        int theirs = _current.FocusSlotProject[to];
        if (mine < 0) return;
        // The research system swaps ranks when the project is already slotted, so one command moves both.
        _session.Queue(PlayerCommands.SetSlot(_session.Tick, _current.FocusCiv, to, mine));
        MarkPending(to, mine);
        MarkPending(from, theirs >= 0 ? theirs : -1);
        ShowFooterNote($"{_catalog!.Projects[mine].Name} moved to {UiText.SlotNames[to]}.");
    }

    private void ClearSlot(int slot)
    {
        if (_session is null || _current is null) return;
        int project = _current.FocusSlotProject[slot];
        if (project < 0) return;
        _session.Queue(PlayerCommands.ClearSlot(_session.Tick, _current.FocusCiv, slot));
        MarkPending(slot, -1);
        ShowFooterNote($"{_catalog!.Projects[project].Name} keeps its {_current.FocusSlotPermille[slot] / 10}% and waits.");
    }

    /// <summary>While paused the frame cannot change; remember the order so the slot can show it as pending.</summary>
    private void MarkPending(int slot, int project)
    {
        if (_session is null || !_session.Paused) return;
        _pendingProject[slot] = project;
        _pendingTick[slot] = _session.Tick;
        if (_current is not null) RefreshFocusBar(_current);
    }
}
