using System;
using System.Collections.Generic;
using CivSim.Game.Ui;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Toasts, the Moment card, the age banner, the Chronicle drawer and coach tips (spec §3.6, §3.7, §3.11).</summary>
public partial class Game
{
    private enum ToastKind { Info, Milestone, Critical, Warning }
    private enum ChronicleClass { Research, People, Season, Danger, Rivals, World, Age }

    private sealed class ToastView
    {
        public Control Root = null!;
        public ColorRect Bar = null!;
        public UiIcon Icon = null!;
        public Label Title = null!, Body = null!;
        public UiButton Action = null!;
        public bool Active, Sticky;
        public float Timer;
        public string Key = "";
        public int Tile = -1;
        public Action? Callback;
        public ToastKind Kind;
        public float Age;
    }

    private sealed class ChronicleEntry
    {
        public ViewEventKind Kind;
        public int Civ, Other, Year, Season, Count, Sum, Tile;
        public ChronicleClass Class;
        public string Text = "";
        public List<string> Names = new();
        public bool Yours;
    }

    private readonly ToastView[] _toasts = new ToastView[3];
    private VBoxContainer? _toastStack;
    private Control? _momentCard;
    private PanelContainer? _momentPanel;
    private PanelContainer? _ageBanner, _coachCard, _hintPill;
    private Label? _momentEyebrow, _momentName, _momentText, _momentChange, _ageBannerTitle, _ageBannerText, _coachTitle, _coachBody, _hintLabel;
    private UiIcon? _momentIcon;
    private UiButton? _momentShow, _momentPick, _momentClose;
    private ColorRect? _momentBar;
    private readonly List<(int Project, int Tile)> _momentQueue = new();
    private int _momentProject = -1, _momentTile = -1;
    private bool _momentShowing;
    private float _momentTimer, _momentGap, _bannerTimer, _coachTimer, _coachGap = 20;
    private bool _momentHover;
    private readonly List<int> _coachQueue = new();
    private int _coachShown = -1;

    // chronicle
    private PanelContainer? _chronicleDrawer;
    private bool _chronicleOpen;
    private UiLayer? _chronicleLayer;
    private readonly List<ChronicleEntry> _chronicle = new(64);
    private readonly List<UiRowButton> _chronicleRows = new();
    private VBoxContainer? _chronicleList;
    private Label? _chronicleEmpty;
    private int _chronicleFilter; // 0 all, 1 yours, 2 world
    private readonly UiButton[] _chronicleChips = new UiButton[3];
    private int _chronicleUnread;
    private bool _chronicleUnseenCritical, _chronicleDirty;
    private long _structureKindsSeen;
    private readonly Dictionary<string, float> _toastRecent = new();
    private float _clock;

    private long _lastEventSequence;
    private long _lastOnboardingEventSequence;
    private ulong _onboardingSeen;
    private float _hintTimer;
    private bool _hintMouseVariant = true;
    private bool _beaconMode;

    // ---- build -------------------------------------------------------------------------------

    private void CreateNotifications()
    {
        // Toast stack (bottom-right, grows upward)
        _toastStack = UiKit.Column(UiTokens.Sp2);
        _toastStack.MouseFilter = Control.MouseFilterEnum.Ignore;
        _toastStack.Alignment = BoxContainer.AlignmentMode.End;
        for (int i = 0; i < 3; i++) _toastStack.AddChild(BuildToast(_toasts[i] = new ToastView()).Root);
        _hudRoot!.AddChild(_toastStack);

        // Moment card (bottom-left)
        var momentPanel = UiKit.Panel("MomentCard", "MomentCard");
        _momentPanel = momentPanel;
        _momentCard = UiKit.WrapOverlay(momentPanel, new Vector2(320, 128));
        _momentCard.Visible = false;
        _momentCard.MouseEntered += () => _momentHover = true;
        _momentCard.MouseExited += () => _momentHover = false;
        var column = UiKit.Column(3);
        var top = UiKit.Row(UiTokens.Sp2);
        _momentEyebrow = UiKit.Text("NEW KNOWLEDGE", "MomentEyebrow", ellipsis: true);
        top.AddChild(_momentEyebrow);
        _momentClose = UiKit.IconButton(UiGlyph.Close, "Dismiss", CloseMoment, 24, 14);
        top.AddChild(_momentClose);
        column.AddChild(top);
        var nameRow = UiKit.Row(UiTokens.Sp2);
        _momentIcon = new UiIcon(UiGlyph.Hammer, 16, UiTokens.TextPrimary);
        nameRow.AddChild(_momentIcon);
        _momentName = UiKit.Text("", "MomentHeading", ellipsis: true);
        nameRow.AddChild(_momentName);
        column.AddChild(nameRow);
        _momentText = UiKit.Text("", "MomentBody", wrap: true);
        _momentText.MaxLinesVisible = 1;
        _momentText.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_momentText);
        _momentChange = UiKit.Text("", "MomentCaption", ellipsis: true);
        column.AddChild(_momentChange);
        var buttons = UiKit.Row(UiTokens.Sp2);
        _momentShow = UiKit.Secondary("Show me", ShowMomentOnMap, "Jump to where it happened", UiGlyph.None, 30);
        _momentShow.ThemeTypeVariation = "MomentSecondary";
        _momentPick = UiKit.Primary("Pick next", () => { CloseMoment(); OpenResearchWeb(FirstOpenSlot()); }, "Choose what your people learn next", UiGlyph.None, 30);
        _momentPick.ThemeTypeVariation = "MomentPrimary";
        buttons.AddChild(_momentShow);
        buttons.AddChild(_momentPick);
        column.AddChild(buttons);
        momentPanel.AddChild(column);
        _momentBar = new ColorRect { Color = UiTokens.Accent, MouseFilter = Control.MouseFilterEnum.Ignore };
        _momentBar.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        _momentBar.OffsetRight = 3;
        _momentCard.AddChild(_momentBar);
        _hudRoot.AddChild(_momentCard);

        // Age banner and coach tip (share the top-centre slot)
        _ageBanner = UiKit.Panel("SheetPanel", "AgeBanner");
        _ageBanner.Visible = false;
        var bannerColumn = UiKit.Column(UiTokens.Sp1);
        bannerColumn.AddChild(UiKit.Text("A NEW AGE", "EyebrowAccent"));
        _ageBannerTitle = UiKit.Text("", "Display", wrap: false);
        _ageBannerTitle.HorizontalAlignment = HorizontalAlignment.Left;
        bannerColumn.AddChild(_ageBannerTitle);
        _ageBannerText = UiKit.Text("", "Caption");
        bannerColumn.AddChild(_ageBannerText);
        _ageBanner.AddChild(bannerColumn);
        _hudRoot.AddChild(_ageBanner);

        _coachCard = UiKit.Panel("CardPanel", "CoachTip");
        _coachCard.Visible = false;
        var coachColumn = UiKit.Column(UiTokens.Sp2);
        var coachTop = UiKit.Row(UiTokens.Sp2);
        coachTop.AddChild(new UiIcon(UiGlyph.Info, 16, UiTokens.Info));
        _coachTitle = UiKit.Text("", "BodyStrong", ellipsis: true);
        coachTop.AddChild(_coachTitle);
        coachColumn.AddChild(coachTop);
        _coachBody = UiKit.Text("", "Body", wrap: true);
        coachColumn.AddChild(_coachBody);
        var coachButtons = UiKit.Row(UiTokens.Sp2);
        coachButtons.AddChild(UiKit.Primary("Got it", HideCoachTip, null, UiGlyph.None, UiTokens.ControlS));
        coachButtons.AddChild(UiKit.Ghost("Turn off tips", () => { _uiSettings.ShowTips = false; _uiSettings.Save(); HideCoachTip(); }, "You can turn tips back on in Settings or Help", UiGlyph.None, UiTokens.ControlS));
        coachColumn.AddChild(coachButtons);
        _coachCard.AddChild(coachColumn);
        _hudRoot.AddChild(_coachCard);

        // Hint pill
        _hintPill = UiKit.Panel("PillPanel", "HintPill", UiKit.MouseFilter.Ignore);
        _hintLabel = UiKit.Text("", "Caption");
        _hintPill.AddChild(_hintLabel);
        _hintPill.Visible = false;
        _hudRoot.AddChild(_hintPill);

        BuildChronicleDrawer();
        if (_current is not null) _lastEventSequence = 0;
    }

    private ToastView BuildToast(ToastView view)
    {
        var panel = UiKit.Panel("ToastPanel");
        view.Root = UiKit.WrapOverlay(panel, new Vector2(440, 72));
        view.Root.Visible = false;
        view.Root.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        var row = UiKit.Row(UiTokens.Sp3);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        view.Icon = new UiIcon(UiGlyph.Info, 20, UiTokens.Info);
        row.AddChild(view.Icon);
        var column = UiKit.Column(0);
        column.MouseFilter = Control.MouseFilterEnum.Ignore;
        column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        view.Title = UiKit.Text("", "BodyStrong", ellipsis: true);
        view.Body = UiKit.Text("", "Caption", wrap: true);
        view.Body.MaxLinesVisible = 3;
        view.Body.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(view.Title);
        column.AddChild(view.Body);
        row.AddChild(column);
        view.Action = UiKit.Ghost("Go there", null, null, UiGlyph.None, UiTokens.ControlS);
        view.Action.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        view.Action.Pressed += () => { view.Callback?.Invoke(); DismissToast(view); };
        row.AddChild(view.Action);
        row.AddChild(UiKit.IconButton(UiGlyph.Close, "Dismiss notification", () => DismissToast(view), 28, 14));
        panel.AddChild(row);
        view.Bar = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        view.Bar.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        view.Bar.OffsetRight = 3;
        view.Root.AddChild(view.Bar);
        view.Root.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                view.Callback?.Invoke();
                DismissToast(view);
            }
        };
        return view;
    }

    private void LayoutNotifications()
    {
        if (_toastStack is null || _momentCard is null || _ageBanner is null || _coachCard is null || _hintPill is null) return;
        float m = Margin;
        float bottom = m + FocusBarHeight + UiTokens.Sp1;
        float toastWidth = _compact ? 340 : 440;
        foreach (ToastView view in _toasts) view.Root.CustomMinimumSize = new Vector2(toastWidth, _compact ? 64 : 72);
        AnchorTo(_toastStack, 1, 1, 1, 1, -m - toastWidth, -bottom - 3 * 80, -m, -bottom, Control.GrowDirection.Begin, Control.GrowDirection.Begin);
        float momentWidth = _compact ? 280 : 320;
        float momentHeight = _compact ? 112 : 128;
        float momentLeft = m + (_compact ? 63 : 0);
        AnchorTo(_momentCard, 0, 1, 0, 1, momentLeft, -bottom - momentHeight, momentLeft + momentWidth, -bottom, Control.GrowDirection.End, Control.GrowDirection.Begin);
        _momentCard.CustomMinimumSize = new Vector2(momentWidth, momentHeight);
        float coachWidth = _compact ? 466 : 610;
        float slotTop = m + TopBandHeight + 8 + 44;
        AnchorTo(_coachCard, 0.5f, 0, 0.5f, 0, -coachWidth / 2, slotTop, coachWidth / 2, slotTop + 110, Control.GrowDirection.Both);
        _coachCard.CustomMinimumSize = new Vector2(coachWidth, 0);
        AnchorTo(_ageBanner, 0.5f, 0, 0.5f, 0, -coachWidth / 2, slotTop, coachWidth / 2, slotTop + 89, Control.GrowDirection.Both);
        _ageBanner.CustomMinimumSize = new Vector2(coachWidth, 89);
        AnchorTo(_hintPill, 0.5f, 1, 0.5f, 1, 0, -bottom - 34, 0, -bottom, Control.GrowDirection.Both, Control.GrowDirection.Begin);
    }

    private void RefreshNotifications(MatchFrame frame)
    {
        ProcessViewEvents(frame);
        ProcessCoachTriggers(frame);
        UpdateMomentButtons();
        if (_chronicleOpen && _chronicleDirty) RefreshChronicleRows();
        UpdateChronicleBadge();
    }

    private void TickNotifications(float delta)
    {
        _clock += delta;
        TickResearchWeb(delta);
        foreach (ToastView view in _toasts)
        {
            if (!view.Active) continue;
            view.Age += delta;
            if (view.Sticky) continue;
            view.Timer -= delta;
            if (view.Timer <= 0) DismissToast(view);
        }
        if (_momentShowing && !_momentHover)
        {
            _momentTimer -= delta;
            if (_momentTimer <= 0) CloseMoment();
        }
        else if (!_momentShowing && _momentQueue.Count > 0)
        {
            _momentGap -= delta;
            if (_momentGap <= 0) PopMoment();
        }
        if (_bannerTimer > 0)
        {
            _bannerTimer -= delta;
            if (_bannerTimer <= 0 && _ageBanner is not null) UiKit.Fade(_ageBanner, false);
        }
        if (_coachShown >= 0)
        {
            _coachTimer -= delta;
            if (_coachTimer <= 0) HideCoachTip();
        }
        else
        {
            _coachGap -= delta;
            if (_coachQueue.Count > 0 && _coachGap <= 0 && CanShowCoach()) ShowCoachTip(_coachQueue[0]);
        }
    }

    // ---- toasts ------------------------------------------------------------------------------

    private void ShowToast(string title, ToastKind kind, string body, int tile = -1, string actionLabel = "", Action? action = null, string key = "")
    {
        if (_toastStack is null) return;
        if (key.Length > 0)
        {
            if (_toastRecent.TryGetValue(key, out float last) && _clock - last < (kind == ToastKind.Critical ? 20f : 5f)) return;
            _toastRecent[key] = _clock;
        }
        ToastView? slot = null;
        foreach (ToastView view in _toasts) if (!view.Active) { slot = view; break; }
        if (slot is null)
        {
            if (kind == ToastKind.Info) return;
            float oldest = -1;
            foreach (ToastView view in _toasts)
                if (!view.Sticky && view.Age > oldest) { oldest = view.Age; slot = view; }
            if (slot is null) foreach (ToastView view in _toasts) if (view.Age > oldest) { oldest = view.Age; slot = view; }
        }
        if (slot is null) return;
        slot.Active = true;
        slot.Age = 0;
        slot.Kind = kind;
        slot.Sticky = kind == ToastKind.Critical;
        slot.Timer = kind switch { ToastKind.Milestone => 8f, ToastKind.Warning => 6f, _ => 5f };
        slot.Key = key;
        slot.Tile = tile;
        UiKit.SetText(slot.Title, title);
        UiKit.SetText(slot.Body, body);
        slot.Body.Visible = body.Length > 0;
        slot.Root.TooltipText = body.Length > 60 ? body : "";
        (UiGlyph glyph, Color color) = kind switch
        {
            ToastKind.Critical => (UiGlyph.Danger, UiTokens.Danger),
            ToastKind.Warning => (UiGlyph.Warning, UiTokens.Warning),
            ToastKind.Milestone => (UiGlyph.Spark, UiTokens.Accent),
            _ => (UiGlyph.Info, UiTokens.Info),
        };
        slot.Icon.Glyph = glyph;
        slot.Icon.Tint = color;
        slot.Bar.Color = color;
        if (tile >= 0 && actionLabel.Length == 0) { actionLabel = "Go there"; action = () => FocusTile(tile); }
        slot.Callback = action ?? (tile >= 0 ? () => FocusTile(tile) : null);
        slot.Action.Visible = actionLabel.Length > 0;
        slot.Action.Text = actionLabel;
        slot.Root.Modulate = new Color(1, 1, 1, 0);
        slot.Root.Visible = true;
        if (!UiSettings.Current.ReduceMotion)
        {
            Tween tween = slot.Root.CreateTween();
            tween.TweenProperty(slot.Root, "modulate:a", 1.0f, UiTokens.ToastInS).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else slot.Root.Modulate = Colors.White;
        if (kind == ToastKind.Critical && _uiSettings.PauseOnDanger && _session is { Paused: false }) _session.SetPaused(true);
    }

    private void DismissToast(ToastView view)
    {
        if (!view.Active) return;
        view.Active = false;
        view.Root.Visible = false;
        view.Callback = null;
    }

    // ---- Moment card and age banner ----------------------------------------------------------

    private void QueueMoment(int project, int tile)
    {
        _momentQueue.Add((project, tile));
        if (!_momentShowing && _momentGap <= 0) PopMoment();
    }

    private void PopMoment()
    {
        if (_momentQueue.Count == 0 || _catalog is null || _momentCard is null) return;
        if (_momentQueue.Count >= 3)
        {
            var names = new List<string>();
            foreach ((int project, int _) in _momentQueue) names.Add(_catalog.Projects[project].Name);
            int count = _momentQueue.Count;
            _momentQueue.Clear();
            _momentTile = -1;
            UiKit.SetText(_momentEyebrow!, "NEW KNOWLEDGE");
            UiKit.SetText(_momentName!, $"{count} projects finished");
            UiKit.SetText(_momentText!, string.Join(", ", names));
            _momentText!.TooltipText = string.Join(", ", names);
            UiKit.SetText(_momentChange!, "");
            _momentChange!.TooltipText = "";
            _momentIcon!.Glyph = UiGlyph.Spark;
            _momentIcon.Tint = UiTokens.Accent;
            _momentBar!.Color = UiTokens.Accent;
            _momentShow!.Visible = false;
            _momentPick!.Text = "Open research";
            _momentPick.Pressed -= OpenResearchFromMoment;
            _momentPick.Pressed += OpenResearchFromMoment;
            _momentProject = -2;
            ShowMomentCard();
            return;
        }
        (int p, int tile) = _momentQueue[0];
        _momentQueue.RemoveAt(0);
        ProjectDef def = _catalog.Projects[p];
        _momentProject = p;
        _momentTile = tile;
        UiKit.SetText(_momentEyebrow!, def.IsKeystone ? "A NEW AGE" : "NEW KNOWLEDGE");
        UiKit.SetText(_momentName!, def.Name);
        UiKit.SetText(_momentText!, def.Moment.Length > 0 ? def.Moment : def.Flavor);
        _momentText!.TooltipText = def.Moment.Length > 0 ? def.Moment : def.Flavor;
        string change = UiText.FirstChangeLine(def);
        UiKit.SetText(_momentChange!, change);
        _momentChange!.TooltipText = change;
        _momentIcon!.Glyph = UiDraw.ThemeGlyph(def.Theme);
        _momentIcon.Tint = UiTokens.ThemeText(def.Theme);
        _momentBar!.Color = UiTokens.ThemeColor(def.Theme);
        _momentShow!.Visible = tile >= 0;
        _momentPick!.Text = "Pick next";
        ShowMomentCard();
    }

    private void OpenResearchFromMoment() { CloseMoment(); OpenResearchWeb(FirstOpenSlot()); }

    private void FitMomentCard()
    {
        if (_momentCard is null || _momentPanel is null) return;
        float m = Margin;
        float bottom = m + FocusBarHeight + UiTokens.Sp1;
        float width = _compact ? 280 : 320;
        float height = Math.Max(_compact ? 112 : 128, _momentPanel.GetCombinedMinimumSize().Y);
        float left = m + (_compact ? 63 : 0);
        AnchorTo(_momentCard, 0, 1, 0, 1, left, -bottom - height, left + width, -bottom, Control.GrowDirection.End, Control.GrowDirection.Begin);
        _momentCard.CustomMinimumSize = new Vector2(width, height);
    }

    private void ShowMomentCard()
    {
        _momentShowing = true;
        CallDeferred(nameof(FitMomentCard));
        _momentTimer = 8.0f;
        _momentHover = false;
        UpdateMomentButtons();
        _momentCard!.Modulate = new Color(1, 1, 1, 0);
        _momentCard.Visible = true;
        UiKit.Fade(_momentCard, true, UiTokens.ToastInS);
    }

    private void UpdateMomentButtons()
    {
        if (_momentPick is null || _momentCard is null || !_momentCard.Visible) return;
        bool summary = _momentProject == -2;
        bool canPick = summary || (_current is { FocusCouncil: false } && FirstOpenSlot() >= 0);
        _momentPick.Visible = canPick;
    }

    private void CloseMoment()
    {
        if (_momentCard is null) return;
        UiKit.Fade(_momentCard, false, UiTokens.ToastOutS);
        _momentShowing = false;
        _momentProject = -1;
        _momentGap = 4.0f;
    }

    private void ShowMomentOnMap()
    {
        if (_momentTile >= 0) FocusTile(_momentTile);
    }

    private void ShowAgeBanner(int project, int era)
    {
        if (_ageBanner is null || _catalog is null) return;
        string age = UiText.AgeName(era);
        UiKit.SetText(_ageBannerTitle!, $"The {age} age");
        UiKit.SetText(_ageBannerText!, $"{_catalog.Projects[project].Name} turned the age.");
        _coachCard!.Visible = false;
        _ageBanner.Modulate = new Color(1, 1, 1, 0);
        _ageBanner.Visible = true;
        UiKit.Fade(_ageBanner, true, UiTokens.ToastInS);
        _bannerTimer = 4.0f;
    }

    // ---- coach tips --------------------------------------------------------------------------

    private static readonly string[] CoachTitles =
    {
        "Your research slots", "New knowledge", "Winter", "Tools and methods", "A growing people", "A new age", "First contact", "Food warning",
        "Look around", "An open slot",
        "Mandate and Decrees", "Tin bronze tools",
    };

    private static string[] CoachBodies => new[]
    {
        "Primary gets the biggest share of research, then Secondary, then Tertiary. Click a slot to change it.",
        "When a project finishes, someone tries it first and the idea spreads. Click Show me to watch.",
        "A season lasts 15 seconds at 1×. Winter is when stored food and shelter matter most.",
        "Turn on Methods to see who has the best tools. Orange tiles know a better method they can't use yet.",
        "Your people split and founded a new settlement. Turn on Claims to see the land you hold.",
        "A turning point moved your people into a new age. New projects are ready.",
        "You met another people. Open Diplomacy (O) to see how they feel about you.",
        "A settlement may go hungry this winter. Turn on Food to see where.",
        "Drag to move. Scroll or pinch to zoom. Right-drag (or Option-drag) to turn.",
        "A slot is open. Its share goes to the others until you choose — or turn on Auto-fill.",
        "KEY-3 unlocked Mandate. Decrees let you choose where to found, explore and mine; open Decrees to preview a call before issuing it.",
        "Tin bronze is stronger, but it needs tin. When access runs short, workers fall back to arsenical bronze or copper kits; use the Methods layer to spot the change.",
    };

    private bool TipSeen(int index) => (_onboardingSeen & (1UL << index)) != 0;

    private bool CanShowCoach() => _uiSettings.ShowTips && !_ui.AnyModal && !(_ageBanner?.Visible ?? false) && !_researchWebVisible;

    private void RequestCoachTip(int index)
    {
        if (!_uiSettings.ShowTips || TipSeen(index) || _coachQueue.Contains(index) || index == _coachShown) return;
        _onboardingSeen |= 1UL << index;
        _coachQueue.Add(index);
        if (_coachQueue.Count > 2) _coachQueue.RemoveAt(0);
        if (_coachShown < 0 && CanShowCoach() && (_coachGap <= 0 || _tipsShownCount < 2)) ShowCoachTip(_coachQueue[0]);
    }

    private int _tipsShownCount;

    private void ShowCoachTip(int index)
    {
        if (_coachCard is null) return;
        _coachQueue.Remove(index);
        _coachShown = index;
        _tipsShownCount++;
        UiKit.SetText(_coachTitle!, CoachTitles[index]);
        UiKit.SetText(_coachBody!, CoachBodies[index]);
        _coachTimer = 30.0f;
        _coachCard.Modulate = new Color(1, 1, 1, 0);
        _coachCard.Visible = true;
        UiKit.Fade(_coachCard, true, UiTokens.ToastInS);
    }

    private void HideCoachTip()
    {
        if (_coachShown < 0 || _coachCard is null) return;
        _coachShown = -1;
        UiKit.Fade(_coachCard, false, UiTokens.ToastOutS);
        _coachGap = _tipsShownCount < 2 ? 6.0f : 45.0f;
    }

    private void MaybeShowLookAroundTip()
    {
        if (_uiSettings.PlayedBefore && !TipSeen(8)) RequestCoachTip(8);
    }

    private void ProcessCoachTriggers(MatchFrame frame)
    {
        if (_ui.AnyModal) return;
        int viewer = Math.Max(0, frame.ViewerCiv);
        int tip = -1;
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _lastOnboardingEventSequence) continue;
            _lastOnboardingEventSequence = item.Sequence;
            if (item.Civ != viewer) continue;
            if (item.Kind == ViewEventKind.SeasonBegan && item.A == 3 && tip < 0) tip = 2;
            else if (item.Kind == ViewEventKind.Moment && tip < 0) tip = 1;
            else if (item.Kind == ViewEventKind.VisibleChange && tip < 0) tip = 3;
            else if (item.Kind == ViewEventKind.Contact && tip < 0) tip = 6;
            else if (item.Kind == ViewEventKind.EraAdvanced && tip < 0) tip = 5;
            else if (item.Kind == ViewEventKind.FamineRisk && tip < 0) tip = 7;
        }
        if (tip < 0 && viewer < frame.CivSettlements.Length && frame.CivSettlements[viewer] > 1 && !TipSeen(4)) tip = 4;
        if (tip < 0 && frame.FocusCiv == viewer && frame.FocusMandateUnlocked && !TipSeen(10)) tip = 10;
        if (tip < 0 && frame.FocusCiv == viewer && viewer < frame.CivEra.Length && frame.CivEra[viewer] >= 3 && !TipSeen(11))
            for (int i = 0; i < frame.FigureCiv.Length; i++)
                if (frame.FigureCiv[i] == viewer && (MethodToolId)frame.FigureMethodFallbackTool[i] is MethodToolId.TinBronzeKit or MethodToolId.ArsenicalKit)
                { tip = 11; break; }
        if (tip < 0 && !_researchWebVisible && frame.FocusCiv == viewer && !TipSeen(0) &&
            (frame.FocusSlotProject[0] >= 0 || frame.FocusSlotProject[1] >= 0 || frame.FocusSlotProject[2] >= 0)) tip = 0;
        if (tip < 0 && !TipSeen(9) && TipSeen(0) && !frame.FocusCouncil && FirstOpenSlot() >= 0 && !_researchWebVisible) tip = 9;
        if (tip >= 0) RequestCoachTip(tip);
    }

    // ---- hint pill ---------------------------------------------------------------------------

    private void TickHint(float delta)
    {
        if (_hintPill is null || _hintLabel is null) return;
        _hintTimer += delta;
        if (_hintTimer < 0.25f) return;
        _hintTimer = 0;
        string text = "";
        if (_beaconMode)
        {
            int free = _current is null ? 0 : Math.Max(0, _current.FocusBeaconsAllowed - _current.FocusBeaconsPlaced);
            text = free > 0
                ? "Empty tile: place or replace · Your Beacon: remove · Right-click/Esc: cancel"
                : "Empty tile: replace nearest · Your Beacon: remove · Right-click/Esc: cancel";
        }
        else if (_pinnedTile >= 0) text = "Esc or right-click to unpin";
        else if (_uiSettings.ShowHints && !(_uiSettings.HintsPanned && _uiSettings.HintsZoomed && _uiSettings.HintsTurned))
        {
            bool trackpad = _uiSettings.TrackpadSeen;
            string alt = OS.GetName() == "macOS" ? "Option" : "Alt";
            text = trackpad
                ? $"Drag to move · Pinch or two-finger scroll to zoom · {alt}-drag to turn · Space to pause"
                : "Drag to move · Scroll to zoom · Right-drag to turn · Space to pause";
        }
        bool show = text.Length > 0 && !_researchWebVisible && !_ui.AnyModal;
        if (show && (_momentCard?.Visible ?? false) && _logicalSize.X < 1300) show = false;
        UiKit.SetText(_hintLabel, text);
        UiKit.SetVisible(_hintPill, show);
    }

    // ---- Chronicle ---------------------------------------------------------------------------

    private void BuildChronicleDrawer()
    {
        _chronicleDrawer = UiKit.Panel("HudPanel", "ChronicleDrawer");
        _chronicleDrawer.Visible = false;
        var column = UiKit.Column(UiTokens.Sp2);
        var header = UiKit.Row(UiTokens.Sp2);
        header.AddChild(UiKit.Text("Chronicle", "Heading"));
        header.AddChild(UiKit.Spacer(expand: true));
        header.AddChild(UiKit.IconButton(UiGlyph.Close, "Close (C)", () => ToggleChronicle(false), 36, 16));
        column.AddChild(header);
        var chips = UiKit.Row(UiTokens.Sp2);
        string[] names = { "All", "Yours", "World" };
        for (int i = 0; i < 3; i++)
        {
            int filter = i;
            UiButton chip = (UiButton)UiKit.InteractiveChip(names[i], UiGlyph.None, () => SetChronicleFilter(filter));
            chip.ButtonPressed = i == 0;
            _chronicleChips[i] = chip;
            chips.AddChild(chip);
        }
        column.AddChild(chips);
        _chronicleList = UiKit.Column(UiTokens.Sp1);
        _chronicleEmpty = UiKit.Text("Your people's story will appear here.", "BodySecondary", wrap: true);
        _chronicleList.AddChild(_chronicleEmpty);
        ScrollContainer scroll = UiKit.Scroll(_chronicleList);
        scroll.CustomMinimumSize = new Vector2(0, 160);
        column.AddChild(scroll);
        _chronicleDrawer.AddChild(column);
        _hudRoot!.AddChild(_chronicleDrawer);
        _chronicleLayer = RegisterLayer("chronicle", false, () => { ToggleChronicle(false); return true; });
    }

    private void LayoutDrawers()
    {
        if (_chronicleDrawer is null) return;
        float m = Margin;
        float top = m + TopBandHeight + 13;
        float width = _compact ? 320 : 377;
        float height = _compact ? Math.Min(233, _logicalSize.Y - top - 200) : Math.Min(377, _logicalSize.Y - top - 140);
        AnchorTo(_chronicleDrawer, 1, 0, 1, 0, -m - width, top, -m, top + height, Control.GrowDirection.Begin);
        _chronicleDrawer.CustomMinimumSize = new Vector2(width, height);
        LayoutDiplomacyDrawer(top);
    }

    private void ToggleChronicle(bool? force = null)
    {
        if (_chronicleDrawer is null) return;
        bool open = force ?? !_chronicleOpen;
        if (open == _chronicleOpen) return;
        _chronicleOpen = open;
        if (open)
        {
            if (_diplomacyOpen) ToggleDiplomacy(false);
            if (_researchWebVisible) CloseResearchWeb();
            _chronicleUnread = 0;
            _chronicleUnseenCritical = false;
            RefreshChronicleRows();
            _chronicleDrawer.Modulate = new Color(1, 1, 1, 0);
            _chronicleDrawer.Visible = true;
            UiKit.Fade(_chronicleDrawer, true, UiTokens.BaseS);
            _ui.Push(_chronicleLayer!);
        }
        else
        {
            UiKit.Fade(_chronicleDrawer, false, UiTokens.BaseCloseS);
            _ui.Remove(_chronicleLayer!);
        }
        UpdateChronicleBadge();
    }

    private void SetChronicleFilter(int filter)
    {
        _chronicleFilter = filter;
        for (int i = 0; i < 3; i++) _chronicleChips[i].SetPressedNoSignal(i == filter);
        RefreshChronicleRows();
    }

    private void UpdateChronicleBadge()
    {
        if (_chronicleBadge is null || _chronicleBadgeLabel is null) return;
        bool show = !_chronicleOpen && (_chronicleUnread > 0 || _chronicleUnseenCritical);
        UiKit.SetVisible(_chronicleBadge, show);
        if (!show) return;
        UiKit.SetText(_chronicleBadgeLabel, _chronicleUnseenCritical ? "!" : _chronicleUnread > 9 ? "9+" : _chronicleUnread.ToString());
        UiKit.SetVariation(_chronicleBadge, _chronicleUnseenCritical ? "BadgePanelDanger" : "BadgePanel");
    }

    private void RefreshChronicleRows()
    {
        _chronicleDirty = false;
        if (_chronicleList is null) return;
        int shown = 0;
        for (int i = 0; i < _chronicle.Count && shown < 30; i++)
        {
            ChronicleEntry entry = _chronicle[i];
            if (_chronicleFilter == 1 && !entry.Yours) continue;
            if (_chronicleFilter == 2 && entry.Yours) continue;
            UiRowButton row;
            if (shown >= _chronicleRows.Count)
            {
                row = new UiRowButton(56);
                row.Pressed += () => { if (row.Tile >= 0) FocusTile(row.Tile); };
                _chronicleRows.Add(row);
                _chronicleList.AddChild(row);
            }
            row = _chronicleRows[shown];
            (UiGlyph glyph, Color tint) = ChronicleIcon(entry);
            string meta = $"Year {entry.Year} · {UiText.SeasonNames[entry.Season & 3]}" + (entry.Count > 1 && entry.Names.Count == 0 && entry.Sum == 0 ? $" · ×{entry.Count}" : "");
            bool old = _current is not null && _current.Year + 1 - entry.Year > 3;
            row.Set(glyph, tint, entry.Text, meta, entry.Tile, old);
            row.Visible = true;
            shown++;
        }
        for (int i = shown; i < _chronicleRows.Count; i++) _chronicleRows[i].Visible = false;
        UiKit.SetVisible(_chronicleEmpty!, shown == 0);
    }

    private static (UiGlyph, Color) ChronicleIcon(ChronicleEntry entry) => entry.Class switch
    {
        ChronicleClass.Research => (UiGlyph.Spark, UiTokens.Accent),
        ChronicleClass.People => (UiGlyph.People, UiTokens.TextSecondary),
        ChronicleClass.Season => ((UiGlyph)((int)UiGlyph.Spring + (entry.Season & 3)), UiTokens.TextSecondary),
        ChronicleClass.Danger => (UiGlyph.Danger, UiTokens.Danger),
        ChronicleClass.Rivals => (UiGlyph.Flags, UiTokens.Info),
        ChronicleClass.Age => (UiGlyph.Age, UiTokens.Accent),
        _ => (UiGlyph.Globe, UiTokens.TextTertiary),
    };

    private void AddChronicle(ViewEvent item, string text, ChronicleClass cls, bool yours, string groupName = "")
    {
        int year = (int)(item.Tick / 600) + 1;
        int season = (int)(item.Tick / 150) & 3;
        ChronicleEntry? head = _chronicle.Count > 0 ? _chronicle[0] : null;
        bool merge = head is not null && head.Kind == item.Kind && head.Civ == item.Civ && head.Other == item.Other && head.Year == year &&
                     item.Kind is ViewEventKind.Births or ViewEventKind.Deaths or ViewEventKind.FirstClaimed or ViewEventKind.ProjectUnlocked
                         or ViewEventKind.StructureBuilt or ViewEventKind.Adopted or ViewEventKind.VisibleChange;
        if (merge && head is not null)
        {
            head.Count++;
            head.Sum += item.Kind is ViewEventKind.Births ? item.A : item.Kind is ViewEventKind.Deaths ? item.B : 0;
            if (groupName.Length > 0) head.Names.Add(groupName);
            head.Text = FormatGrouped(head, text);
            if (item.Tile >= 0) head.Tile = item.Tile;
        }
        else
        {
            var entry = new ChronicleEntry
            {
                Kind = item.Kind, Civ = item.Civ, Other = item.Other, Year = year, Season = season, Count = 1,
                Sum = item.Kind is ViewEventKind.Births ? item.A : item.Kind is ViewEventKind.Deaths ? item.B : 0,
                Tile = item.Tile, Class = cls, Text = text, Yours = yours,
            };
            if (groupName.Length > 0) entry.Names.Add(groupName);
            entry.Text = FormatGrouped(entry, text);
            _chronicle.Insert(0, entry);
            if (_chronicle.Count > 60) _chronicle.RemoveAt(_chronicle.Count - 1);
        }
        _chronicleDirty = true;
        if (!_chronicleOpen && item.Tier >= ViewEventTier.Info) _chronicleUnread++;
    }

    private string FormatGrouped(ChronicleEntry entry, string single)
    {
        if (entry.Count <= 1) return single;
        switch (entry.Kind)
        {
            case ViewEventKind.Births: return $"{entry.Sum} children were born this year.";
            case ViewEventKind.Deaths: return $"{entry.Sum} people died this year.";
            case ViewEventKind.FirstClaimed when entry.Civ != _current?.ViewerCiv: return $"The {UiText.CivName(entry.Civ)} claimed {entry.Count} world firsts.";
            case ViewEventKind.ProjectUnlocked:
                {
                    int shown = Math.Min(3, entry.Names.Count);
                    string list = string.Join(", ", entry.Names.GetRange(0, shown));
                    return entry.Names.Count > shown ? $"New projects ready: {list} +{entry.Names.Count - shown}" : $"New projects ready: {list}";
                }
            case ViewEventKind.StructureBuilt: return $"{entry.Count} structures built.";
            default: return single;
        }
    }

    // ---- events ------------------------------------------------------------------------------

    private void ProcessViewEvents(MatchFrame frame)
    {
        int viewer = Math.Max(0, frame.ViewerCiv);
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _lastEventSequence) continue;
            _lastEventSequence = item.Sequence;
            bool publicEvent = item.Kind is ViewEventKind.FirstClaimed or ViewEventKind.EraAdvanced;
            if (item.Civ >= 0 && item.Civ != viewer && !publicEvent) continue;
            HandleEvent(item, viewer, frame);
        }
    }

    private string OtherName(ViewEvent item) => UiText.CivName(item.Other);

    private static string Rations(int n) => n == 1 ? "1 ration" : $"{n} rations";

    /// <summary>
    /// What a raid came to, for the viewer: the toast title, the sentence for the toast and the chronicle (the loot and who was
    /// hurt), and the few words and colour that float up off the map where it was fought.
    /// </summary>
    private (string Title, string Text, string Floater, Color Color) RaidReport(ViewEvent item)
    {
        string who = OtherName(item);
        int fight = item.C, loot = Math.Max(0, item.B);
        int raiders = fight & 0xFF, defenders = (fight >> 8) & 0xFF, raidersDown = (fight >> 16) & 0xFF, defendersDown = (fight >> 24) & 0x7F;
        string Toll(int down, int of, string side) => down <= 0 || of <= 0 ? "" : $" {down} of {of} {side} hurt or lost.";
        switch (item.A)
        {
            case 1:
                return (loot > 0 ? $"Raid won: +{Rations(loot)}" : "Raid won",
                    (loot > 0 ? $"Your raiders came home with {Rations(loot)} of loot." : "Your raiders won but found little worth carrying home.")
                        + Toll(raidersDown, raiders, "raiders were"),
                    loot > 0 ? $"+{Rations(loot)}" : "Raid won", UiTokens.Accent);
            case 0:
                return ("Raid failed", "Your raiders were beaten back and came home with nothing." + Toll(raidersDown, raiders, "raiders were"), "Raid failed", UiTokens.TextSecondary);
            case 2:
                return (loot > 0 ? $"Raided: -{Rations(loot)}" : "You were raided",
                    loot > 0 ? $"The {who} raiders carried off {Rations(loot)} of your goods." + Toll(defendersDown, defenders, "defenders were")
                             : $"The {who} raiders broke through." + Toll(defendersDown, defenders, "defenders were"),
                    loot > 0 ? $"-{Rations(loot)}" : "Raided", UiTokens.Danger);
            default:
                return ("Raid driven off", $"Your people drove off the {who} raiders." + Toll(defendersDown, defenders, "defenders were"), "Raid driven off", UiTokens.Success);
        }
    }

    /// <summary>Moves the camera to the viewer's raiding party where it is now, or to the raid's target when none is on the road.</summary>
    private void FollowRaidParty(int viewer, int targetTile)
    {
        if (_current is not null)
            for (int i = 0; i < _current.FigureCount; i++)
            {
                if (_current.FigureCiv[i] != viewer) continue;
                if ((FigureActivity)_current.FigureActivity[i] is not (FigureActivity.OnRaid or FigureActivity.OnAssault)) continue;
                FocusTile(_current.FigureTile[i]);
                return;
            }
        FocusTile(targetTile);
    }

    /// <summary>
    /// Plays the fight of a resolved raid or assault on its tile, then runs <paramref name="report"/> (the chronicle line and the
    /// toast). With no fight to show (an investment ended, no tile, or the skirmish cannot play) it reports at once.
    /// </summary>
    private void ReportAfterSkirmish(ViewEvent item, bool assault, Action report)
    {
        int fight = item.C;
        if (item.Tile < 0 || fight == 0) { report(); return; }
        bool viewerAttacked = item.A is 0 or 1;
        bool attackersWon = item.A is 1 or 2;
        var spec = new SkirmishSpec(item.Tile, viewerAttacked ? item.Civ : item.Other, viewerAttacked ? item.Other : item.Civ, attackersWon,
            fight & 0xFF, (fight >> 8) & 0xFF, (fight >> 16) & 0xFF, (fight >> 24) & 0x7F, assault);
        if (!StartSkirmish(spec, report)) report();
    }

    private void HandleEvent(ViewEvent item, int viewer, MatchFrame frame)
    {
        bool mine = item.Civ == viewer || item.Civ < 0;
        string project = ProjectName(item.Project);
        string Slot(int s) => (uint)s < 3u ? UiText.SlotNames[s] : "a slot";
        switch (item.Kind)
        {
            case ViewEventKind.SeasonBegan:
                if (item.C != 0 && item.A == 0) AddChronicle(item, "A harsh winter has ended.", ChronicleClass.Season, true);
                break;
            case ViewEventKind.Births:
                AddChronicle(item, $"{item.A} {(item.A == 1 ? "child was" : "children were")} born this year.", ChronicleClass.People, true);
                break;
            case ViewEventKind.Deaths:
                AddChronicle(item, $"{item.B} {(item.B == 1 ? "person" : "people")} died this year.", ChronicleClass.People, true);
                break;
            case ViewEventKind.SettlementFounded:
                {
                    bool island = item.C == 1;
                    string text = item.A == 1 ? "Free Folk founded a camp." : island ? "Your people crossed the sea and settled an island." : "Your people founded a new settlement.";
                    AddChronicle(item, text, ChronicleClass.People, true);
                    if (island) ShowToast("A village on an island", ToastKind.Milestone, "Outriggers carried a band across the water.", item.Tile, key: "island-settled");
                    else if (item.A != 1) ShowToast("A new settlement", ToastKind.Info, "Your people have split and spread out.", item.Tile);
                    break;
                }
            case ViewEventKind.StructureBuilt:
                {
                    string name = StructureName(item.A);
                    AddChronicle(item, $"{UiText.Sentence(name)} built.", ChronicleClass.People, true);
                    if (item.A is >= 0 and < 60 && (_structureKindsSeen & (1L << item.A)) == 0)
                    {
                        _structureKindsSeen |= 1L << item.A;
                        ShowToast($"{UiText.Sentence(name)} built", ToastKind.Info, "A new kind of building stands.", item.Tile);
                    }
                    break;
                }
            case ViewEventKind.StructureLost:
                AddChronicle(item, $"A {StructureName(item.A).ToLowerInvariant()} was lost.", ChronicleClass.Danger, true);
                ShowToast("A building was lost", ToastKind.Critical, $"A {StructureName(item.A).ToLowerInvariant()} is gone.", item.Tile, key: "structure-lost");
                _chronicleUnseenCritical |= !_chronicleOpen;
                break;
            case ViewEventKind.FireOut:
                AddChronicle(item, "A hearth has gone cold.", ChronicleClass.People, true);
                ShowToast("A hearth went cold", ToastKind.Info, "Fire keeps people warm and fed.", item.Tile, key: "fire-out");
                break;
            case ViewEventKind.FamineRisk:
                {
                    string text = item.B == 1 ? "Food will run short this winter." : "A settlement is starving.";
                    AddChronicle(item, text, ChronicleClass.Danger, true);
                    ShowToast("Food warning", ToastKind.Critical, text, item.Tile, key: "famine");
                    _chronicleUnseenCritical |= !_chronicleOpen;
                    break;
                }
            case ViewEventKind.PartyIncoming:
                {
                    string text = item.A == (int)PartyKind.Assault ? "An assault party is coming." : "A raid party is coming.";
                    AddChronicle(item, text, ChronicleClass.Danger, true);
                    ShowToast(item.A == (int)PartyKind.Assault ? "An assault is coming" : "Raiders are coming", ToastKind.Critical, "A hostile party was sighted heading for your settlement.", item.Tile, key: "party-incoming");
                    _chronicleUnseenCritical |= !_chronicleOpen;
                    break;
                }
            case ViewEventKind.SlotFilled:
                if (item.B == 1) AddChronicle(item, $"{project} moved up from the queue into {Slot(item.A)}.", ChronicleClass.Research, true);
                else if (item.B == 2) AddChronicle(item, $"The elders put {project} into {Slot(item.A)}.", ChronicleClass.Research, true);
                break;
            case ViewEventKind.CouncilPick:
                AddChronicle(item, $"The elders chose {project} for {Slot(item.A)}: {UiText.CouncilReason(item.B)}.", ChronicleClass.Research, true);
                ShowToast($"The elders chose {project}", ToastKind.Info, UiText.Sentence(UiText.CouncilReason(item.B)) + ".", key: "council-pick");
                break;
            case ViewEventKind.ProjectCompleted:
                AddChronicle(item, $"Your people learned {project}.", ChronicleClass.Research, true);
                QueueMoment(item.Project, item.Tile);
                if (mine && item.Tile >= 0)
                    StartPulse(item.Tile, _catalog is not null && (uint)item.Project < (uint)_catalog.Projects.Count && _catalog.Projects[item.Project].IsKeystone);
                OnResearchProjectCompleted(item.Project);
                break;
            case ViewEventKind.FirstClaimed:
                if (item.Civ == viewer)
                {
                    AddChronicle(item, $"Your people were the first in the world to learn {project}.", ChronicleClass.World, true);
                    ShowToast("A world first", ToastKind.Milestone, $"Your people were first to learn {project}.", key: $"first-{item.Project}");
                }
                else AddChronicle(item, $"The {UiText.CivName(item.Civ)} were first to learn {project}.", ChronicleClass.World, false);
                break;
            case ViewEventKind.EraAdvanced:
                if (item.Civ == viewer)
                {
                    AddChronicle(item, $"Your people entered the {UiText.AgeName(item.A)} age through {project}.", ChronicleClass.Age, true);
                    ShowAgeBanner(item.Project, item.A);
                }
                else
                {
                    AddChronicle(item, $"The {UiText.CivName(item.Civ)} entered the {UiText.AgeName(item.A)} age.", ChronicleClass.World, false);
                    ShowToast($"{UiText.CivName(item.Civ)} entered a new age", ToastKind.Milestone, $"The {UiText.AgeName(item.A)} age.", key: $"rival-age-{item.Civ}-{item.A}");
                }
                break;
            case ViewEventKind.Adopted:
                if (item.A >= 90) AddChronicle(item, $"{project} is now known by {item.A}% of your people.", ChronicleClass.Research, true);
                break;
            case ViewEventKind.DormantChanged:
                {
                    string text = item.A == 1 ? $"{project} is blocked: its methods fall back." : $"{project} works again.";
                    AddChronicle(item, text, ChronicleClass.Research, true);
                    ShowToast(item.A == 1 ? "A method is blocked" : "A method works again", ToastKind.Info, text, key: $"dormant-{item.Project}");
                    break;
                }
            case ViewEventKind.ResourceSeen:
                {
                    string target = _catalog is not null && (uint)item.A < (uint)_catalog.GateTargets.Count ? _catalog.GateTargets[item.A].Replace('_', ' ') : "something new";
                    AddChronicle(item, $"Your people found {target}.", ChronicleClass.People, true);
                    ShowToast("A discovery in the land", ToastKind.Info, $"Your people found {target}.", key: $"seen-{item.A}");
                    break;
                }
            case ViewEventKind.ProjectUnlocked:
                AddChronicle(item, $"New project ready: {project}.", ChronicleClass.Research, true, project);
                if (!_researchWebVisible) { _newProjectCount++; UpdateNewBadge(); }
                break;
            case ViewEventKind.Aware:
                AddChronicle(item, $"You learned of the {OtherName(item)}.", ChronicleClass.Rivals, true);
                break;
            case ViewEventKind.Contact:
                AddChronicle(item, $"You met the {OtherName(item)}.", ChronicleClass.Rivals, true);
                ShowToast($"You met the {OtherName(item)}", ToastKind.Milestone, "They are your neighbours now.", -1, "Open Diplomacy", () => ToggleDiplomacy(true), "contact");
                break;
            case ViewEventKind.Skirmish:
                AddChronicle(item, $"A border skirmish with the {OtherName(item)} left people hurt.", ChronicleClass.Danger, true);
                ShowToast("A border skirmish", ToastKind.Critical, $"People were hurt in a clash with the {OtherName(item)}.", item.Tile, key: "skirmish");
                _chronicleUnseenCritical |= !_chronicleOpen;
                break;
            case ViewEventKind.GiftArrived:
                AddChronicle(item, $"A gift arrived from the {OtherName(item)}.", ChronicleClass.Rivals, true);
                ShowToast("A gift arrived", ToastKind.Info, $"From the {OtherName(item)}.", key: "gift");
                break;
            case ViewEventKind.BarterDone:
                AddChronicle(item, "Your barter party came home.", ChronicleClass.Rivals, true);
                ShowToast("Barter done", ToastKind.Info, "Your trade party came home.", key: "barter");
                break;
            case ViewEventKind.RaidLaunched:
                {
                    string who = OtherName(item);
                    string text = item.C switch
                    {
                        RaidTarget.Caravan => $"{item.A} raiders went out to ambush a {who} caravan.",
                        RaidTarget.MiningCamp => $"{item.A} raiders set out for a {who} mining camp.",
                        RaidTarget.Assault => $"{item.A} fighters marched on a {who} settlement.",
                        _ => $"{item.A} raiders set out for a {who} settlement.",
                    };
                    AddChronicle(item, text, ChronicleClass.Rivals, true);
                    int target = item.Tile;
                    ShowToast(item.C == RaidTarget.Assault ? "An assault set out" : "Raiders set out", ToastKind.Milestone, text, target, "Follow", () => FollowRaidParty(viewer, target), "raid-out");
                    break;
                }
            case ViewEventKind.RaidResolved:
                {
                    (string title, string text, string floater, Color color) = RaidReport(item);
                    // The fight plays on the map first; the result follows when it is over.
                    ReportAfterSkirmish(item, assault: false, () =>
                    {
                        AddChronicle(item, text, item.A == 2 ? ChronicleClass.Danger : ChronicleClass.Rivals, true);
                        ShowToast(title, item.A == 2 ? ToastKind.Critical : item.A == 1 ? ToastKind.Milestone : ToastKind.Info, text, item.Tile, key: $"raid-resolved-{item.A}-{item.Tile}");
                        SpawnMapFloater(item.Tile, floater, color);
                        if (item.A == 2) _chronicleUnseenCritical |= !_chronicleOpen;
                    });
                    break;
                }
            case ViewEventKind.WarDeclared:
                {
                    string text = item.A == 1 ? $"The {OtherName(item)} declared war on you." : $"You declared war on the {OtherName(item)}.";
                    AddChronicle(item, text, ChronicleClass.Danger, true);
                    ShowToast(item.A == 1 ? "War!" : "You declared war", item.A == 1 ? ToastKind.Critical : ToastKind.Info, text, key: "war");
                    if (item.A == 1) _chronicleUnseenCritical |= !_chronicleOpen;
                    break;
                }
            case ViewEventKind.PeaceMade:
                AddChronicle(item, $"Peace with the {OtherName(item)}.", ChronicleClass.Rivals, true);
                ShowToast("Peace", ToastKind.Milestone, $"The war with the {OtherName(item)} is over.", key: "peace");
                break;
            case ViewEventKind.AssaultResolved:
                {
                    string text = item.A switch { 1 => "Your assault succeeded.", 2 => "A settlement fell.", 3 => "Your people held their settlement.", _ => "Your assault was driven back." };
                    ReportAfterSkirmish(item, assault: true, () =>
                    {
                        AddChronicle(item, text, item.A == 2 ? ChronicleClass.Danger : ChronicleClass.Rivals, true);
                        ShowToast(item.A == 2 ? "A settlement fell" : "An assault ended", item.A == 2 ? ToastKind.Critical : ToastKind.Milestone, text, item.Tile, key: "assault");
                        if (item.A == 2) _chronicleUnseenCritical |= !_chronicleOpen;
                    });
                    break;
                }
            case ViewEventKind.Occupied:
                AddChronicle(item, "A settlement is occupied.", ChronicleClass.Danger, true);
                break;
            case ViewEventKind.OccupationBroken:
                AddChronicle(item, "An occupation ended.", ChronicleClass.Rivals, true);
                break;
            case ViewEventKind.Captured:
                {
                    string text = item.A < 0 ? "You lost a settlement." : "You captured a settlement.";
                    AddChronicle(item, text, item.A < 0 ? ChronicleClass.Danger : ChronicleClass.Rivals, true);
                    ShowToast(item.A < 0 ? "A settlement was lost" : "A settlement captured", item.A < 0 ? ToastKind.Critical : ToastKind.Milestone, text, item.Tile, key: "captured");
                    if (item.A < 0) _chronicleUnseenCritical |= !_chronicleOpen;
                    break;
                }
            case ViewEventKind.FreeFolkFounded:
                AddChronicle(item, "Refugees founded a Free Folk camp.", ChronicleClass.People, true);
                break;
            case ViewEventKind.AllianceFormed:
                AddChronicle(item, $"Alliance with the {OtherName(item)}.", ChronicleClass.Rivals, true);
                ShowToast("An alliance", ToastKind.Milestone, $"You and the {OtherName(item)} defend each other.", key: "alliance");
                break;
            case ViewEventKind.BeaconPlaced:
                AddChronicle(item, "You placed a Beacon.", ChronicleClass.People, true);
                break;
            case ViewEventKind.VoyageLost:
                {
                    string what = item.B switch { 0 => "gift party", 1 => "trade party", 2 => "raiding party", 3 => "war party", _ => "band of settlers" };
                    AddChronicle(item, $"Your {what} turned back at sea.", ChronicleClass.Danger, true);
                    ShowToast("A voyage turned back", ToastKind.Warning, $"The sea was too rough; your {what} came home.", key: "voyage-lost");
                    break;
                }
            case ViewEventKind.OrderRefused:
                {
                    string text = $"{UiText.OrderNoun(item.A)} didn't happen: {UiText.RefusalReason(item.B)}.";
                    AddChronicle(item, text, ChronicleClass.Danger, true);
                    if (item.A == (int)CommandKind.SetSlot) ClearPendingFor(-1);
                    ShowToast("That didn't work", ToastKind.Warning, text, key: "refused");
                    break;
                }
        }
        _ = mine;
    }

    private void ClearPendingFor(int slot)
    {
        for (int s = 0; s < 3; s++) if (slot < 0 || s == slot) _pendingProject[s] = -2;
    }

    private string ProjectName(int project)
    {
        if (_catalog is null || project < 0 || project >= _catalog.Projects.Count) return "a discovery";
        return _catalog.Projects[project].Name;
    }

    private static string StructureName(int kind)
    {
        try { return LivingEconomyModel.StructureName(kind); }
        catch (ArgumentOutOfRangeException) { return "structure"; }
    }

    private void CloseDrawersAndMenus()
    {
        if (_peopleOpen) ClosePeoplePanel();
        if (_chronicleOpen) ToggleChronicle(false);
        if (_diplomacyOpen) ToggleDiplomacy(false);
        ToggleGameMenu(false);
    }
}
