using System;
using CivSim.Game.Ui;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>Camera control and the input router (docs/MVP/20 §4.1). Left-drag pans, right-drag turns, wheel and pinch zoom to the cursor.</summary>
public partial class Game
{
    private const float ZoomMin = 2.7f, ZoomMax = 120.0f;
    private static readonly float[] ZoomPresets = { 5.0f, 12.0f, 40.0f, 90.0f };

    private float _zoomTarget = 20.0f;
    private bool _zoomAnchored;
    private Vector2 _zoomAnchorScreen;
    private Vector3 _zoomAnchorWorld;
    private float _yawTarget = -0.6f;
    private bool _leftDown, _rightDown, _middleDown, _dragMoved, _orbitWithLeft;
    private Vector2 _leftDownPosition;
    private Vector3 _grabWorld;
    private bool _moveAnimating;
    private Vector3 _moveFrom, _moveTo;
    private float _moveZoomFrom, _moveZoomTo, _moveT;
    private int _pinnedTile = -1;
    private bool _panGestureSeen;

    private int ZoomLevel => _zoom switch { < 7 => 0, < 22 => 1, < 60 => 2, _ => 3 };

    // ---- zoom -------------------------------------------------------------------------------

    /// <summary>Immediate zoom (benchmarks, presets). The visual change is applied now.</summary>
    private void SetZoom(float value)
    {
        _zoom = _zoomTarget = Mathf.Clamp(value, ZoomMin, ZoomMax);
        _zoomAnchored = false;
        UpdateCamera();
    }

    /// <summary>Eased zoom; with an anchor the ground point under the cursor stays put.</summary>
    private void ZoomTo(float value, Vector2? anchor = null)
    {
        float target = Mathf.Clamp(value, ZoomMin, ZoomMax);
        _moveAnimating = false;
        if (_uiSettings.ReduceMotion)
        {
            if (anchor is Vector2 a && TryGroundHit(a, out Vector3 before))
            {
                _zoom = _zoomTarget = target;
                UpdateCamera();
                if (TryGroundHit(a, out Vector3 after)) { _target += before - after; ClampTarget(); UpdateCamera(); }
            }
            else SetZoom(target);
            return;
        }
        _zoomTarget = target;
        if (anchor is Vector2 screen && TryGroundHit(screen, out Vector3 world))
        {
            _zoomAnchored = true;
            _zoomAnchorScreen = screen;
            _zoomAnchorWorld = world;
        }
        else _zoomAnchored = false;
        _uiSettings.HintsZoomed = true;
    }

    private void ZoomStep(float factor, Vector2? anchor = null) => ZoomTo(_zoomTarget * factor, anchor);

    private void ZoomIn() => ZoomStep(1.0f / 1.3f);
    private void ZoomOut() => ZoomStep(1.3f);

    // ---- placement ---------------------------------------------------------------------------

    private void ClampTarget()
    {
        _target.X = Mathf.Clamp(_target.X, -5, _width + 5);
        _target.Z = Mathf.Clamp(_target.Z, -5, _height * RowSpacing + 5);
    }

    private void UpdateCamera()
    {
        if (_visualRenderer is not null) _visualRenderer.Call("set_camera", _target, _yaw, _pitch, _zoom);
        RefreshCompass();
    }

    private void FrameCapital()
    {
        if (_current is null || _camera is null) return;
        int selected = -1;
        int population = -1;
        for (int i = 0; i < _current.SettlementCount; i++)
            if (_current.SettlementCiv[i] == Math.Max(0, _current.ViewerCiv) && (_current.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) == 0 &&
                _current.SettlementPopulation[i] > population)
            {
                selected = i;
                population = _current.SettlementPopulation[i];
            }
        if (selected < 0 && _current.SettlementCount > 0) selected = 0;
        if (selected < 0) return;
        int tile = _current.SettlementTile[selected];
        Vector3 center = TileCenter(tile % _current.Width, tile / _current.Width);
        var target = new Vector3(center.X + 0.3f, _terrainHeights[tile], center.Z + 0.2f);
        _yaw = _yawTarget = Mathf.DegToRad(30.0f);
        _pitch = Mathf.DegToRad(40.0f);
        MoveCameraTo(target, 5.0f, instant: !_hudReady);
    }

    private void FocusTile(int tile)
    {
        if (_current is null || tile < 0 || tile >= _current.Width * _current.Height) return;
        Vector3 center = TileCenter(tile % _current.Width, tile / _current.Width);
        MoveCameraTo(new Vector3(center.X, _terrainHeights[tile], center.Z), Mathf.Min(_zoomTarget, 10.0f));
    }

    private void MoveCameraTo(Vector3 target, float zoom, bool instant = false)
    {
        zoom = Mathf.Clamp(zoom, ZoomMin, ZoomMax);
        if (instant || _uiSettings.ReduceMotion)
        {
            _target = target;
            SetZoom(zoom);
            return;
        }
        _moveAnimating = true;
        _moveFrom = _target;
        _moveTo = target;
        _moveZoomFrom = _zoom;
        _moveZoomTo = zoom;
        _moveT = 0;
        _zoomAnchored = false;
    }

    // ---- per-frame ----------------------------------------------------------------------------

    private void TickCamera(float delta)
    {
        bool dirty = false;
        if (_moveAnimating)
        {
            _moveT = Math.Min(1, _moveT + delta / 0.45f);
            float t = _moveT < 0.5f ? 4 * _moveT * _moveT * _moveT : 1 - Mathf.Pow(-2 * _moveT + 2, 3) / 2;
            _target = _moveFrom.Lerp(_moveTo, t);
            _zoom = _zoomTarget = Mathf.Exp(Mathf.Lerp(Mathf.Log(_moveZoomFrom), Mathf.Log(_moveZoomTo), t));
            if (_moveT >= 1) _moveAnimating = false;
            dirty = true;
        }
        else if (!Mathf.IsEqualApprox(_zoom, _zoomTarget))
        {
            float k = 1.0f - Mathf.Exp(-delta / 0.09f);
            float next = Mathf.Exp(Mathf.Lerp(Mathf.Log(_zoom), Mathf.Log(_zoomTarget), k));
            if (Mathf.Abs(next - _zoomTarget) < _zoomTarget * 0.002f) next = _zoomTarget;
            _zoom = next;
            if (_zoomAnchored)
            {
                UpdateCameraTransformOnly();
                if (TryGroundHit(_zoomAnchorScreen, out Vector3 hit))
                {
                    _target += _zoomAnchorWorld - hit;
                    ClampTarget();
                }
            }
            dirty = true;
        }
        if (!Mathf.IsEqualApprox(_yaw, _yawTarget))
        {
            float k = 1.0f - Mathf.Exp(-delta / 0.07f);
            _yaw = Mathf.Lerp(_yaw, _yawTarget, k);
            if (Mathf.Abs(_yaw - _yawTarget) < 0.002f) _yaw = _yawTarget;
            dirty = true;
        }

        // Keyboard pan: only when the map owns the keyboard.
        Vector2 move = Vector2.Zero;
        if (MapOwnsKeyboard())
        {
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;
        }
        if (move != Vector2.Zero)
        {
            float speed = Input.IsKeyPressed(Key.Shift) ? 2.5f : 1.0f;
            Vector3 right = new(Mathf.Cos(_yaw), 0, -Mathf.Sin(_yaw));
            Vector3 forward = new(-Mathf.Sin(_yaw), 0, -Mathf.Cos(_yaw));
            _target += (right * move.X - forward * move.Y) * _zoom * delta * 0.85f * speed;
            ClampTarget();
            _moveAnimating = false;
            _uiSettings.HintsPanned = true;
            dirty = true;
        }
        if (dirty)
        {
            UpdateCamera();
        }
    }

    private void UpdateCameraTransformOnly()
    {
        if (_camera is null) return;
        _visualRenderer?.Call("set_camera", _target, _yaw, _pitch, _zoom);
    }

    private bool MapOwnsKeyboard()
        => _session is not null && !_researchWebVisible && !_ui.AnyModal && GetViewport().GuiGetFocusOwner() is not LineEdit && !_gameMenuOpen;

    // ---- picking -----------------------------------------------------------------------------

    private bool TryGroundHit(Vector2 screen, out Vector3 hit)
    {
        hit = _target;
        if (_camera is null) return false;
        Vector3 origin = _camera.ProjectRayOrigin(screen);
        Vector3 ray = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(ray.Y) < 0.0001f) return false;
        float distance = (_target.Y - origin.Y) / ray.Y;
        if (distance < 0) return false;
        hit = origin + ray * distance;
        return true;
    }

    private bool TryPickTile(Vector2 screen, out int tile)
    {
        tile = -1;
        if (_camera is null || _current is null) return false;
        Vector3 origin = _camera.ProjectRayOrigin(screen);
        Vector3 ray = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(ray.Y) < 0.0001f) return false;
        float distance = -origin.Y / ray.Y;
        if (distance < 0) return false;
        Vector3 hit = origin + ray * distance;
        for (int pass = 0; pass < 3; pass++)
        {
            tile = HexLayout.NearestTile((int)(hit.X * MapUnit), (int)(hit.Z / RowSpacing * MapUnit), _current.Width, _current.Height);
            if (tile < 0 || tile >= _terrainHeights.Length) { tile = -1; return false; }
            distance = (_terrainHeights[tile] - origin.Y) / ray.Y;
            if (distance < 0) { tile = -1; return false; }
            hit = origin + ray * distance;
        }
        return tile >= 0 && tile < _current.Width * _current.Height;
    }

    // ---- input router -------------------------------------------------------------------------

    public override void _Input(InputEvent input)
    {
        if (_session is null || input is not InputEventKey { Pressed: true } key) return;
        if (key.Echo)
        {
            // Holding a pan key must not re-trigger toggles; nothing else repeats.
            return;
        }
        bool typing = GetViewport().GuiGetFocusOwner() is LineEdit;
        Key code = key.Keycode;

        // Always available, even while typing
        if (code == Key.Escape)
        {
            if (HandleEscape()) GetViewport().SetInputAsHandled();
            return;
        }
        if (code == Key.F3) { ToggleDevOverlay(); GetViewport().SetInputAsHandled(); return; }
        if (typing) return;

        if (code is Key.Space or Key.P)
        {
            if (_ui.TopIsModal && _introPanelVisible) return;
            TogglePause();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_introPanelVisible)
        {
            if (code is Key.Enter or Key.KpEnter) { DismissIntro(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (_ui.TopIsModal)
        {
            if (code is Key.H or Key.F1 && _helpOpen) { ToggleHelp(false); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (code == Key.T) { if (_researchWebVisible) CloseResearchWeb(); else OpenResearchWeb(-1); GetViewport().SetInputAsHandled(); return; }
        if (code is Key.H or Key.F1) { ToggleHelp(); GetViewport().SetInputAsHandled(); return; }
        if (_researchWebVisible)
        {
            if (HandleWebKey(key)) GetViewport().SetInputAsHandled();
            return;
        }
        switch (code)
        {
            case Key.O: ToggleDiplomacy(); break;
            case Key.C: ToggleChronicle(); break;
            case Key.R: SetResourcesVisible(!_resourcesVisible); break;
            case Key.B: ToggleBeaconMode(); break;
            case Key.F or Key.Home: FrameCapital(); break;
            case Key.Q: TurnCamera(-1); break;
            case Key.E: TurnCamera(1); break;
            case Key.Bracketleft: _session.ChangeSpeed(-1); break;
            case Key.Bracketright: _session.ChangeSpeed(1); break;
            case Key.Minus or Key.KpSubtract: ZoomOut(); break;
            case Key.Equal or Key.KpAdd: ZoomIn(); break;
            case Key.Key0 or Key.Kp0: ZoomTo(ZoomPresets[0]); break;
            case Key.Key1 or Key.Kp1: ZoomTo(ZoomPresets[1]); break;
            case Key.Key2 or Key.Kp2: ZoomTo(ZoomPresets[2]); break;
            case Key.Key3 or Key.Kp3: ZoomTo(ZoomPresets[3]); break;
            case Key.F5: SaveQuickMatch(); break;
            case Key.F9: AskLoadQuickMatch(); break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void TurnCamera(int direction)
    {
        _yawTarget += direction * Mathf.Pi * 0.5f;
        _uiSettings.HintsTurned = true;
        if (_uiSettings.ReduceMotion) { _yaw = _yawTarget; UpdateCamera(); }
    }

    private bool HandleEscape()
    {
        if (_ui.CloseTop()) { UpdatePausedTreatment(); return true; }
        if (_coachShown >= 0) { HideCoachTip(); return true; }
        if (_ui.Count == 0 && !_endScreenShown) { ToggleGameMenu(true); return true; }
        return false;
    }

    /// <summary>The map is frozen while the research web, a modal, the menu or a popover is open: no zoom, pan, turn or pick.</summary>
    private bool MapInputLocked
        => _researchWebVisible || _ui.AnyModal || _gameMenuOpen || _queueLayer is not null || _leaningLayer is not null || _slotMenuLayer is not null;

    public override void _UnhandledInput(InputEvent input)
    {
        if (_session is null) return;
        if (MapInputLocked)
        {
            // Scroll and pinch gestures are not consumed by every control; swallow them here so they never reach the map.
            if (input is InputEventPanGesture or InputEventMagnifyGesture or InputEventMouseButton or InputEventMouseMotion)
            {
                _leftDown = _rightDown = _middleDown = false;
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        switch (input)
        {
            case InputEventMouseButton button:
                HandleMouseButton(button);
                break;
            case InputEventPanGesture pan:
                // macOS reports two-finger scrolling as a pan gesture, not as wheel buttons.
                if (Mathf.Abs(pan.Delta.Y) > 0.0001f)
                {
                    _uiSettings.TrackpadSeen = true;
                    ZoomStep(Mathf.Pow(1.15f, pan.Delta.Y * 0.35f), GetViewport().GetMousePosition());
                }
                break;
            case InputEventMagnifyGesture magnify:
                if (magnify.Factor > 0 && Mathf.Abs(magnify.Factor - 1.0f) > 0.0001f)
                {
                    _uiSettings.TrackpadSeen = true;
                    ZoomStep(1.0f / Mathf.Pow(Mathf.Clamp(magnify.Factor, 0.8f, 1.25f), 0.6f), magnify.Position);
                }
                break;
            case InputEventMouseMotion motion:
                HandleMouseMotion(motion);
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton button)
    {
        Vector2 position = button.Position;
        if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed) { ZoomStep(1.0f / 1.1f, position); return; }
        if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed) { ZoomStep(1.1f, position); return; }
        switch (button.ButtonIndex)
        {
            case MouseButton.Left:
                if (button.Pressed)
                {
                    _leftDown = true;
                    _dragMoved = false;
                    _leftDownPosition = position;
                    _orbitWithLeft = button.AltPressed;
                    _moveAnimating = false;
                    if (!TryGroundHit(position, out _grabWorld)) _grabWorld = _target;
                }
                else
                {
                    bool click = _leftDown && !_dragMoved;
                    _leftDown = false;
                    if (click) OnMapClicked(position);
                }
                break;
            case MouseButton.Right:
                if (button.Pressed) { _rightDown = true; _dragMoved = false; _leftDownPosition = position; }
                else
                {
                    bool click = _rightDown && !_dragMoved;
                    _rightDown = false;
                    if (click) OnMapRightClicked();
                }
                break;
            case MouseButton.Middle:
                _middleDown = button.Pressed;
                break;
        }
        HideTileCardWhileDragging();
    }

    private void HandleMouseMotion(InputEventMouseMotion motion)
    {
        if (_leftDown)
        {
            if (!_dragMoved && (motion.Position - _leftDownPosition).Length() > 6) _dragMoved = true;
            if (_dragMoved)
            {
                if (_orbitWithLeft) Orbit(motion.Relative);
                else if (TryGroundHit(motion.Position, out Vector3 hit))
                {
                    _target += _grabWorld - hit;
                    ClampTarget();
                    UpdateCamera();
                    _uiSettings.HintsPanned = true;
                }
                HideTileCardWhileDragging();
            }
            return;
        }
        if (_rightDown || _middleDown)
        {
            if (!_dragMoved && (motion.Position - _leftDownPosition).Length() > 4) _dragMoved = true;
            Orbit(motion.Relative);
            HideTileCardWhileDragging();
            return;
        }
        NoteHover(motion.Position);
    }

    private void Orbit(Vector2 relative)
    {
        _yaw -= relative.X * 0.006f;
        _yawTarget = _yaw;
        _pitch = Mathf.Clamp(_pitch + relative.Y * 0.004f, Mathf.DegToRad(25), Mathf.DegToRad(80));
        _uiSettings.HintsTurned = true;
        UpdateCamera();
    }

    private void OnMapClicked(Vector2 position)
    {
        if (_decreeMapMode)
        {
            if (TryPickTile(position, out int decreeTile)) SelectDecreeTile(decreeTile);
            return;
        }
        if (_beaconMode)
        {
            ManageBeaconAt(position);
            return;
        }
        if (TryPickTile(position, out int tile))
        {
            _pinnedTile = _pinnedTile == tile ? -1 : tile;
            RefreshTileCardNow(position);
            UpdatePinLayer();
        }
        else { _pinnedTile = -1; UpdatePinLayer(); }
    }

    private void OnMapRightClicked()
    {
        if (_decreeMapMode)
        {
            _decreeMapMode = false;
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            if (_decreeHint is not null) UiKit.SetText(_decreeHint, "Map target cancelled.");
        }
        else if (_beaconMode) SetBeaconMode(false);
        else if (_pinnedTile >= 0) { _pinnedTile = -1; UpdatePinLayer(); RefreshTileCardNow(GetViewport().GetMousePosition()); }
    }

    // ---- Beacon mode -------------------------------------------------------------------------

    private UiLayer? _beaconLayer;

    private void ToggleBeaconMode() => SetBeaconMode(!_beaconMode);

    private void SetBeaconMode(bool on)
    {
        if (on && !(_current is { FocusBeaconsAllowed: > 0 }))
        {
            ShowToast("No Beacons yet", ToastKind.Info, "A Beacon is a suggestion that draws scouts and settlers to a spot. Learn Songlines to unlock them.", key: "beacon-locked");
            return;
        }
        _beaconMode = on;
        if (on)
        {
            _beaconLayer ??= RegisterLayer("beacon", false, () => { SetBeaconMode(false); return true; });
            _ui.Push(_beaconLayer);
        }
        else if (_beaconLayer is not null) _ui.Remove(_beaconLayer);
        RefreshRail(_current!);
        Input.SetDefaultCursorShape(on ? Input.CursorShape.Cross : Input.CursorShape.Arrow);
    }

    private void ManageBeaconAt(Vector2 screenPosition)
    {
        if (_session is null || _camera is null || _current is null || !_session.CanIssueCommands)
        {
            SetBeaconMode(false);
            return;
        }
        if (!TryPickTile(screenPosition, out int tile))
        {
            SetBeaconMode(false);
            return;
        }

        if (TryGetOwnBeaconIndex(tile, out int beaconIndex))
        {
            _session.Queue(PlayerCommands.RemoveBeacon(_session.Tick, 0, beaconIndex));
            SetBeaconMode(false);
            string nextStep = _session.Paused
                ? "The removal starts when you resume. Press B after it takes effect to place a replacement."
                : "Its slot is free. Press B to place a replacement.";
            ShowToast("Beacon removed", ToastKind.Info, nextStep, tile, key: "beacon-removed");
            return;
        }

        _session.Queue(PlayerCommands.PlaceBeacon(_session.Tick, 0, tile, _width, replaceIfFull: true));
        SetBeaconMode(false);
        ShowToast("Beacon placed", ToastKind.Info, "If all Beacon slots were used, your nearest Beacon moved here.", tile, key: $"beacon-placed-{tile}");
    }

    private bool TryGetOwnBeaconIndex(int tile, out int beaconIndex)
    {
        beaconIndex = 0;
        if (_current is null) return false;
        int ownIndex = 0;
        for (int i = 0; i < _current.BeaconCount; i++)
        {
            if (_current.BeaconCiv[i] != _current.FocusCiv) continue;
            if (_current.BeaconTile[i] == tile)
            {
                beaconIndex = ownIndex;
                return true;
            }
            ownIndex++;
        }
        return false;
    }

    private UiLayer? _pinLayer;

    private void UpdatePinLayer()
    {
        if (_pinnedTile >= 0)
        {
            _pinLayer ??= RegisterLayer("pin", false, () => { _pinnedTile = -1; RefreshTileCardNow(GetViewport().GetMousePosition()); return true; });
            _ui.Push(_pinLayer);
        }
        else if (_pinLayer is not null) _ui.Remove(_pinLayer);
    }
}
