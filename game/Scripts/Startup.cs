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
    private const double AfterVideoFadeDurationSeconds = 0.8;
    private const double StartButtonFadeDurationSeconds = 0.6;

    [Export] public Texture2D? AfterVideoTexture { get; set; }

    private static readonly Color BackgroundTan = new("#D6C19B");

    private VideoStreamPlayer? _video;
    private TextureRect? _afterVideoImage;
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

        AddChild(BuildTitleScreen());

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
            RevealAfterVideoScreen();
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
            MouseFilter = MouseFilterEnum.Pass,
        };
        screen.SetAnchorsPreset(LayoutPreset.FullRect);

        _afterVideoImage = new TextureRect
        {
            Name = "AfterVideoScreen",
            Texture = AfterVideoTexture,
            Modulate = new Color(1, 1, 1, 0),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _afterVideoImage.SetAnchorsPreset(LayoutPreset.FullRect);
        screen.AddChild(_afterVideoImage);

        _startButton = new Button
        {
            Name = "StartGame",
            Text = "Start Game",
            Theme = UiTheme.Build(),
            ThemeTypeVariation = "Secondary",
            CustomMinimumSize = new Vector2(340, 64),
            Modulate = new Color(1, 1, 1, 0),
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
        if (_transitionStarted || _video is null) return;
        _transitionStarted = true;

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(_video, "modulate:a", 0.0f, FadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_video, "volume_db", -60.0f, FadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(RevealAfterVideoScreen));
    }

    private void RevealAfterVideoScreen()
    {
        if (_video is not null) _video.Visible = false;
        if (_afterVideoImage is null)
        {
            GD.PushError("Could not display startup background: AfterVideoTexture is not assigned.");
            RevealStartButton();
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(_afterVideoImage, "modulate:a", 1.0f, AfterVideoFadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(RevealStartButton));
    }

    private void RevealStartButton()
    {
        if (_startButton is null) return;

        var tween = CreateTween();
        tween.TweenProperty(_startButton, "modulate:a", 1.0f, StartButtonFadeDurationSeconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(EnableStartButton));
    }

    private void EnableStartButton()
    {
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
