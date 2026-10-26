using System;
using System.Linq;
using CivSim.Game.Ui;
using Godot;

namespace CivSim.Game;

/// <summary>First-run video and title screen, before the simulation scene is loaded.</summary>
public partial class Startup : Control
{
    private const double FadeStartsAtSeconds = 8.0;
    private const double FadeDurationSeconds = 2.0;
    private const string VideoPath = "res://assets/ui/loading/intro-pan-sunset.ogv";
    private const string GameScenePath = "res://Main.tscn";

    [Export] public Texture2D? LogoTexture { get; set; }

    private static readonly Color BackgroundTan = new("#D6C19B");

    private VideoStreamPlayer? _video;
    private Control? _titleScreen;
    private Button? _startButton;
    private double _elapsed;
    private bool _transitionStarted;
    private bool _launching;

    public override void _Ready()
    {
        if (IsDevelopmentPresentationRun())
        {
            ChangeToGameScene();
            return;
        }

        MouseFilter = MouseFilterEnum.Ignore;

        var background = new ColorRect
        {
            Name = "TanBackground",
            Color = BackgroundTan,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _titleScreen = BuildTitleScreen();
        AddChild(_titleScreen);

        _video = new VideoStreamPlayer
        {
            Name = "IntroVideo",
            Expand = true,
            Loop = false,
            AudioTrack = 0,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _video.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_video);

        VideoStream? intro = GD.Load<VideoStream>(VideoPath);
        if (intro is null)
        {
            _transitionStarted = true;
            GD.PushError($"Could not load startup video: {VideoPath}");
            RevealTitleScreen();
            return;
        }

        _video.Stream = intro;
        _video.Play();
    }

    public override void _Process(double delta)
    {
        if (_transitionStarted || _video is null) return;

        _elapsed += delta;
        if (_elapsed >= FadeStartsAtSeconds) BeginFadeToTitleScreen();
    }

    private Control BuildTitleScreen()
    {
        var screen = new Control
        {
            Name = "TitleScreen",
            Modulate = new Color(1, 1, 1, 0),
            MouseFilter = MouseFilterEnum.Pass,
        };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);

        var logo = new TextureRect
        {
            Name = "Logo",
            Texture = LogoTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        logo.AnchorLeft = 0.17f;
        logo.AnchorTop = 0.035f;
        logo.AnchorRight = 0.83f;
        logo.AnchorBottom = 0.61f;
        screen.AddChild(logo);

        _startButton = new Button
        {
            Name = "StartGame",
            Text = "Start Game",
            Theme = UiTheme.Build(),
            ThemeTypeVariation = "Secondary",
            CustomMinimumSize = new Vector2(340, 64),
            FocusMode = FocusModeEnum.All,
            Disabled = true,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _startButton.AddThemeFontSizeOverride("font_size", 28);
        _startButton.AnchorLeft = 0.5f;
        _startButton.AnchorTop = 0.68f;
        _startButton.AnchorRight = 0.5f;
        _startButton.AnchorBottom = 0.68f;
        _startButton.OffsetLeft = -170;
        _startButton.OffsetTop = -32;
        _startButton.OffsetRight = 170;
        _startButton.OffsetBottom = 32;
        _startButton.Pressed += StartGame;
        screen.AddChild(_startButton);

        return screen;
    }

    private void BeginFadeToTitleScreen()
    {
        if (_transitionStarted || _video is null || _titleScreen is null) return;
        _transitionStarted = true;

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(_video, "modulate:a", 0.0f, FadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_video, "volume_db", -60.0f, FadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_titleScreen, "modulate:a", 1.0f, FadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Chain().TweenCallback(Callable.From(RevealTitleScreen));
    }

    private void RevealTitleScreen()
    {
        if (_titleScreen is not null) _titleScreen.Modulate = Colors.White;
        if (_video is not null) _video.Visible = false;
        if (_startButton is null) return;

        _startButton.Disabled = false;
        _startButton.GrabFocus();
    }

    private void StartGame()
    {
        if (_launching) return;
        _launching = true;
        if (_startButton is not null) _startButton.Disabled = true;
        ChangeToGameScene();
    }

    private void ChangeToGameScene()
    {
        Error result = GetTree().ChangeSceneToFile(GameScenePath);
        if (result != Error.Ok)
        {
            _launching = false;
            if (_startButton is not null) _startButton.Disabled = false;
            GD.PushError($"Could not open game scene {GameScenePath}: {result}");
        }
    }

    private static bool IsDevelopmentPresentationRun()
    {
        return OS.GetCmdlineUserArgs().Any(arg =>
            arg.StartsWith("--benchmark-", StringComparison.Ordinal) ||
            arg.StartsWith("--start-minute=", StringComparison.Ordinal) ||
            arg.StartsWith("--screenshot=", StringComparison.Ordinal) ||
            arg.StartsWith("--scenario=", StringComparison.Ordinal) ||
            arg.StartsWith("--shots=", StringComparison.Ordinal));
    }
}
