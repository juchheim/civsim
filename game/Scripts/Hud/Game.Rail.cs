using System;
using CivSim.Game.Ui;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>The left map-tools rail: zoom ladder, compass, map layers with a legend, and Beacon mode (spec §3.5).</summary>
public partial class Game
{
    private PanelContainer? _rail, _legendCard;
    private UiButton? _zoomInButton, _zoomOutButton, _compassButton, _claimsButton, _foodButton, _methodsButton, _resourcesButton, _beaconButton;
    private UiZoomLadder? _zoomLadder;
    private Label? _zoomNameLabel, _layersLabel;
    private Control? _legendClaims, _legendFood, _legendMethods, _legendResources;
    private Control? _railDivider;

    private void CreateRail()
    {
        _rail = UiKit.Panel("HudPanel", "MapTools");
        var column = UiKit.Column(UiTokens.Sp1);
        column.Alignment = BoxContainer.AlignmentMode.Begin;
        _zoomInButton = UiKit.IconButton(UiGlyph.Plus, "Zoom in · scroll up or =", ZoomIn, 36, 22);
        column.AddChild(_zoomInButton);
        _zoomLadder = new UiZoomLadder(level => ZoomTo(ZoomPresets[level]), 73, 100);
        _zoomLadder.TooltipText = "Zoom: Z0 Hearth · Z1 Settlement · Z2 Region · Z3 Continent (keys 0–3)";
        column.AddChild(_zoomLadder);
        _zoomOutButton = UiKit.IconButton(UiGlyph.Minus, "Zoom out · scroll down or −", ZoomOut, 36, 22);
        column.AddChild(_zoomOutButton);
        _zoomNameLabel = UiKit.Text("Hearth", "CaptionTertiary");
        _zoomNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_zoomNameLabel);
        _compassButton = UiKit.IconButton(UiGlyph.CompassRose, "Turn: Q / E · click for north", TurnToNorth, 36, 26);
        column.AddChild(_compassButton);
        _railDivider = UiKit.Divider();
        column.AddChild(_railDivider);
        _layersLabel = UiKit.Text("LAYERS", "EyebrowTertiary");
        _layersLabel.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_layersLabel);
        _claimsButton = RailToggle("Claims", UiGlyph.LayerClaims, "Who holds the land, and how firmly", () => SetMapOverlay(1));
        _foodButton = RailToggle("Food", UiGlyph.LayerFood, "Which settlements have enough food", () => SetMapOverlay(2));
        _methodsButton = RailToggle("Methods", UiGlyph.LayerMethods, "How good your people's tools are", () => SetMapOverlay(3));
        _resourcesButton = RailToggle("Resources", UiGlyph.LayerResources, "Deposits you have found (R)", () => SetResourcesVisible(!_resourcesVisible));
        column.AddChild(_claimsButton);
        column.AddChild(_foodButton);
        column.AddChild(_methodsButton);
        column.AddChild(_resourcesButton);
        _beaconButton = RailToggle("Beacon", UiGlyph.Beacon, "A Beacon draws scouts and settlers toward a spot. It's a suggestion, not an order. (B)", ToggleBeaconMode);
        _beaconButton.Visible = false;
        column.AddChild(_beaconButton);
        _rail.AddChild(column);
        _hudRoot!.AddChild(_rail);

        BuildLegendCard();
        RefreshOverlayState();
    }

    private UiButton RailToggle(string text, UiGlyph glyph, string tooltip, Action pressed)
    {
        var button = new UiButton
        {
            Text = text,
            Glyph = glyph,
            GlyphTop = true,
            GlyphSize = 22,
            ToggleMode = true,
            ThemeTypeVariation = "RailButtonTall",
            CustomMinimumSize = new Vector2(73, 54),
            TooltipText = tooltip,
        };
        button.AddThemeFontSizeOverride("font_size", 14);
        button.Pressed += pressed;
        return button;
    }

    private void BuildLegendCard()
    {
        _legendCard = UiKit.Panel("CardPanel", "LegendCard");
        _legendCard.Visible = false;
        _legendCard.CustomMinimumSize = new Vector2(233, 0);
        var host = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        VBoxContainer Block() { var v = UiKit.Column(UiTokens.Sp2); v.Visible = false; host.AddChild(v); return v; }
        VBoxContainer claims = Block(), food = Block(), methods = Block(), resources = Block();
        _legendClaims = claims; _legendFood = food; _legendMethods = methods; _legendResources = resources;

        claims.AddChild(UiKit.Text("CLAIMS", "EyebrowTertiary"));
        claims.AddChild(GradientBar(new Color(0.5f, 0.5f, 0.45f, 0.25f), UiTokens.Accent));
        var claimRow = UiKit.Row(0);
        claimRow.AddChild(UiKit.Text("Weak", "Caption"));
        claimRow.AddChild(UiKit.Spacer(expand: true));
        claimRow.AddChild(UiKit.Text("Strong", "Caption"));
        claims.AddChild(claimRow);
        claims.AddChild(UiKit.Text("70+ makes land yours. Hover a tile for the top three claims.", "CaptionTertiary", wrap: true, minWidth: 207));

        food.AddChild(UiKit.Text("FOOD", "EyebrowTertiary"));
        AddSwatch(food, new Color("#338C38"), "Well fed");
        AddSwatch(food, new Color("#C79E29"), "Tight");
        AddSwatch(food, new Color("#D4541F"), "Short");
        AddSwatch(food, new Color("#A6141A"), "Famine");
        food.AddChild(UiKit.Text("Autumn and winter show the forecast for late winter.", "CaptionTertiary", wrap: true, minWidth: 207));

        methods.AddChild(UiKit.Text("METHODS", "EyebrowTertiary"));
        methods.AddChild(GradientBar(new Color("#1F6B9C"), new Color("#9140B3")));
        var methodRow = UiKit.Row(0);
        methodRow.AddChild(UiKit.Text("Older tools", "Caption"));
        methodRow.AddChild(UiKit.Spacer(expand: true));
        methodRow.AddChild(UiKit.Text("Newer tools", "Caption"));
        methods.AddChild(methodRow);
        AddSwatch(methods, new Color("#FF8A1A"), "A better method is blocked — hover for why");

        resources.AddChild(UiKit.Text("RESOURCES", "EyebrowTertiary"));
        resources.AddChild(UiKit.Text("Coloured stones mark deposits you have found. Hover a tile for the type.", "Caption", wrap: true, minWidth: 207));

        _legendCard.AddChild(host);
        _hudRoot!.AddChild(_legendCard);
    }

    private static Control GradientBar(Color from, Color to)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, from);
        gradient.SetColor(1, to);
        var texture = new GradientTexture1D { Gradient = gradient, Width = 128 };
        return new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new Vector2(0, 12),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private static void AddSwatch(VBoxContainer parent, Color color, string text)
    {
        var row = UiKit.Row(UiTokens.Sp2);
        var swatch = new ColorRect { Color = color, CustomMinimumSize = new Vector2(13, 13), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(swatch);
        row.AddChild(UiKit.Text(text, "Caption", wrap: true, minWidth: 180));
        parent.AddChild(row);
    }

    private void LayoutRail()
    {
        if (_rail is null) return;
        float m = Margin;
        float top = m + TopBandHeight + 13;
        float width = _compact ? 55 : 89;
        AnchorTo(_rail, 0, 0, 0, 0, m, top + (_devVisible ? 110 : 0), m + width, top + (_devVisible ? 110 : 0));
        _rail.CustomMinimumSize = new Vector2(width, 0);
        _rail.Size = new Vector2(width, 0);
        foreach (UiButton? button in new[] { _claimsButton, _foodButton, _methodsButton, _resourcesButton, _beaconButton })
        {
            if (button is null) continue;
            if (_compact)
            {
                button.GlyphTop = false;
                button.GlyphSize = 22;
                button.Text = "";
                button.ThemeTypeVariation = "IconButton";
                button.CustomMinimumSize = new Vector2(36, 36);
            }
            else
            {
                button.GlyphTop = true;
                button.Text = button == _claimsButton ? "Claims" : button == _foodButton ? "Food" : button == _methodsButton ? "Methods" : button == _resourcesButton ? "Resources" : "Beacon";
                button.ThemeTypeVariation = "RailButtonTall";
                button.CustomMinimumSize = new Vector2(73, 54);
            }
        }
        if (_zoomLadder is not null) _zoomLadder.CustomMinimumSize = new Vector2(_compact ? 39 : 73, 100);
        if (_layersLabel is not null) _layersLabel.Visible = !_compact;
        if (_zoomNameLabel is not null) _zoomNameLabel.Visible = !_compact;
        if (_legendCard is not null)
        {
            _legendCard.Position = new Vector2(m + width + 8, top);
        }
        RefreshOverlayState();
    }

    private void RefreshRail(MatchFrame frame)
    {
        if (_beaconButton is null) return;
        bool available = frame.FocusBeaconsAllowed > 0;
        UiKit.SetVisible(_beaconButton, available);
        if (available)
        {
            int left = Math.Max(0, frame.FocusBeaconsAllowed - frame.FocusBeaconsPlaced);
            if (!_compact) UiKit.SetText(_beaconButton, $"Beacon · {left}");
            _beaconButton.SetPressedNoSignal(_beaconMode);
            _beaconButton.Disabled = left == 0 && !_beaconMode;
        }
        RefreshRailZoom();
    }

    private void RefreshRailZoom()
    {
        if (_zoomLadder is null) return;
        float t = (Mathf.Log(_zoom) - Mathf.Log(ZoomMin)) / (Mathf.Log(ZoomMax) - Mathf.Log(ZoomMin));
        _zoomLadder.Set(t, ZoomLevel);
        if (_zoomNameLabel is not null) UiKit.SetText(_zoomNameLabel, UiText.ZoomNames[ZoomLevel]);
        if (_zoomLadder is not null) _zoomLadder.TooltipText = UiText.ZoomTips[ZoomLevel] + " · keys 0–3";
    }

    private void RefreshCompass()
    {
        if (_compassButton is null) return;
        _compassButton.GlyphRotation = _yaw;
    }

    private void TurnToNorth()
    {
        float turns = Mathf.Round(_yaw / Mathf.Tau);
        _yawTarget = turns * Mathf.Tau;
        _uiSettings.HintsTurned = true;
        if (_uiSettings.ReduceMotion) { _yaw = _yawTarget; UpdateCamera(); }
    }

    private void SetMapOverlay(int mode)
    {
        _mapOverlay = _mapOverlay == mode ? 0 : mode;
        _visualRenderer?.Call("set_overlay", _mapOverlay);
        RefreshOverlayState();
    }

    private void SetResourcesVisible(bool visible)
    {
        _resourcesVisible = visible;
        if (_current is not null) UpdateKnownResources(_current);
        RefreshOverlayState();
    }

    private void RefreshOverlayState()
    {
        if (_claimsButton is null) return;
        _claimsButton.SetPressedNoSignal(_mapOverlay == 1);
        _foodButton!.SetPressedNoSignal(_mapOverlay == 2);
        _methodsButton!.SetPressedNoSignal(_mapOverlay == 3);
        _resourcesButton!.SetPressedNoSignal(_resourcesVisible);
        if (_legendCard is null) return;
        UiButton? anchor = _mapOverlay switch { 1 => _claimsButton, 2 => _foodButton, 3 => _methodsButton, _ => _resourcesVisible ? _resourcesButton : null };
        _legendClaims!.Visible = _mapOverlay == 1 && !_resourcesVisible;
        _legendFood!.Visible = _mapOverlay == 2 && !_resourcesVisible;
        _legendMethods!.Visible = _mapOverlay == 3 && !_resourcesVisible;
        _legendResources!.Visible = _resourcesVisible;
        _legendCard.Visible = anchor is not null;
        if (anchor is not null) CallDeferred(nameof(PositionLegend));
    }

    private void PositionLegend()
    {
        if (_legendCard is null || _rail is null) return;
        UiButton? anchor = _mapOverlay switch { 1 => _claimsButton, 2 => _foodButton, 3 => _methodsButton, _ => _resourcesVisible ? _resourcesButton : null };
        if (_resourcesVisible) anchor = _resourcesButton;
        if (anchor is null) return;
        _legendCard.Position = new Vector2(_rail.Position.X + _rail.Size.X + 8, anchor.GlobalPosition.Y);
    }

    private void ReadOverlayArgument()
    {
        string? overlay = ArgumentValue("--overlay=")?.ToLowerInvariant();
        if (overlay is null) return;
        _mapOverlay = overlay switch { "claims" => 1, "food" => 2, "methods" => 3, _ => 0 };
        _resourcesVisible = overlay == "resources";
        _visualRenderer?.Call("set_overlay", _mapOverlay);
    }
}
