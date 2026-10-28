using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// Development-only scripted UI walk-throughs for screenshots: <c>--scenario=name --shots=/abs/dir</c>.
/// Steps call the same methods the controls call; they never touch simulation state directly.
/// </summary>
public partial class Game
{
    private readonly List<(float At, Action Run)> _scenario = new();
    private float _scenarioClock;
    private int _scenarioIndex;
    private string? _scenarioDir;
    private bool _scenarioActive;

    private void StartScenarioIfRequested()
    {
        string? name = ArgumentValue("--scenario=");
        if (name is null) return;
        _scenarioDir = ArgumentValue("--shots=") ?? Path.GetTempPath();
        Directory.CreateDirectory(_scenarioDir);
        float t = 1.2f;
        void Step(Action run, float wait = 0.7f) { _scenario.Add((t, run)); t += wait; }
        void Shot(string shot) => Step(() => TakeScenarioShot(shot), 0.4f);
        switch (name)
        {
            case "assign":
                Step(() => OpenResearchWeb(-1));
                Shot("assign-1-web");
                Step(OnCtaPressed);
                Shot("assign-2-after-start");
                Step(() => { if (_selectedResearchProject >= 0) SelectResearchProject(FindDefaultResearchProject()); });
                Step(OnCtaPressed);
                Shot("assign-3-two-slots");
                Step(CloseResearchWeb);
                Shot("assign-4-closed");
                Step(TogglePause);
                Shot("assign-5-paused");
                Step(TogglePause);
                break;
            case "paused-assign":
                Step(() => _session?.SetPaused(true));
                Step(() => OpenResearchWeb(-1));
                Step(OnCtaPressed);
                Shot("pa-1-pending-web");
                Step(CloseResearchWeb);
                Shot("pa-2-pending-bar");
                break;
            case "replace":
                Step(() => OpenResearchWeb(-1));
                Step(OnCtaPressed);
                Step(() => SelectResearchProject(FindDefaultResearchProject()));
                Step(OnCtaPressed);
                Step(() => SelectResearchProject(FindDefaultResearchProject()));
                Step(OnCtaPressed);
                Step(() => SelectResearchProject(FindDefaultResearchProject()));
                Shot("replace-1-all-full");
                Step(OnCtaPressed);
                Shot("replace-2-choose-row");
                Step(() => AssignSelectedToSlot(0));
                Shot("replace-3-replaced");
                break;
            case "almost":
                Step(() => OpenResearchWeb(-1));
                Step(SelectFirstAlmostReady);
                Shot("almost-1-inspector");
                Step(OnCtaPressed);
                Shot("almost-2-queued");
                break;
            case "map":
                Step(() => OpenResearchWeb(-1));
                Step(() => SetResearchView(1));
                Shot("map-1");
                break;
            case "overlays":
                Step(() => SetMapOverlay(1));
                Shot("ov-claims");
                Step(() => SetMapOverlay(2));
                Shot("ov-food");
                Step(() => SetMapOverlay(3));
                Shot("ov-methods");
                Step(() => SetResourcesVisible(true));
                Shot("ov-resources");
                break;
            case "moments":
                Step(() => OpenResearchWeb(-1));
                Step(OnCtaPressed);
                Step(CloseResearchWeb);
                Step(() => GrantProject(FindProjectByName("Fire Keeping")));
                Shot("moment-1");
                Step(() => GrantProject(FindProjectByName("Plant Lore")), 1.2f);
                Shot("moment-2");
                Step(() => ToggleChronicle(true));
                Shot("moment-3-chronicle");
                break;
            case "toasts":
                Step(() => ShowToast("Food warning", ToastKind.Critical, "A settlement may go hungry this winter.", 10, key: "t1"));
                Step(() => ShowToast("A world first", ToastKind.Milestone, "Your people were first to learn Fire Keeping.", key: "t2"), 0.3f);
                Step(() => ShowToast("The elders chose Plant Lore", ToastKind.Info, "It fits your leaning.", key: "t3"), 0.3f);
                Shot("toasts");
                break;
            case "menu":
                Step(() => ToggleGameMenu(true));
                Shot("menu");
                Step(() => ToggleGameMenu(false));
                Step(() => ToggleHelp(true));
                Shot("help-controls");
                Step(() => _helpTabs?.Select(1, true));
                Shot("help-research");
                Step(() => ToggleHelp(false));
                Step(OpenSettings);
                Shot("settings");
                break;
            case "diplomacy":
                Step(() => ToggleDiplomacy(true));
                Shot("diplomacy");
                break;
            case "tile":
                Step(() => { _hoverScreen = new Vector2(800, 450); _hoverActive = true; _hoverMoved = true; _hoverRest = 1; });
                Shot("tile-hover");
                Step(() => { _pinnedTile = _hoverTile; RefreshTileCardNow(_hoverScreen); });
                Shot("tile-pinned");
                Step(() => ZoomTo(ZoomPresets[2]), 1.2f);
                Shot("tile-pinned-z2");
                break;
            case "end":
                Step(ShowEndScreen);
                Shot("end");
                break;
            case "zoom":
                Step(() => ZoomTo(ZoomPresets[1]), 1.0f);
                Shot("zoom-z1");
                Step(() => ZoomTo(ZoomPresets[2]), 1.0f);
                Shot("zoom-z2");
                Step(() => ZoomTo(ZoomPresets[3]), 1.0f);
                Shot("zoom-z3");
                break;
            case "input":
                Step(() => GD.Print($"SCN start paused={_session!.Paused} zoom={_zoom:0.00} target={_target} yaw={_yaw:0.00}"));
                Step(() => InjectKey(Key.Space));
                Step(() => GD.Print($"SCN after Space paused={_session!.Paused}"));
                Step(() => InjectKey(Key.T));
                Step(() => GD.Print($"SCN after T web={_researchWebVisible}"));
                Step(() => { GD.Print($"SCN lock before zoomTarget={_zoomTarget:0.00}"); InjectWheel(new Vector2(150, 500), true); InjectWheel(new Vector2(150, 500), true); Input.ParseInputEvent(new InputEventMagnifyGesture { Position = new Vector2(150, 500), Factor = 1.2f }); Input.ParseInputEvent(new InputEventPanGesture { Position = new Vector2(150, 500), Delta = new Vector2(0, -3) }); });
                Step(() => GD.Print($"SCN lock after wheel/pinch/scroll over web zoomTarget={_zoomTarget:0.00}"));
                Step(() => InjectKey(Key.Space));
                Step(() => GD.Print($"SCN Space inside web paused={_session!.Paused}"));
                Step(() => InjectKey(Key.Key1));
                Step(() => GD.Print($"SCN key 1 in web slot0={_current!.FocusSlotProject[0]} pending0={_pendingProject[0]}"));
                Shot("input-web-after-1");
                Step(() => InjectKey(Key.Escape));
                Step(() => GD.Print($"SCN Esc closes web={!_researchWebVisible} menuOpen={_gameMenuOpen}"));
                Step(() => InjectKey(Key.Escape));
                Step(() => GD.Print($"SCN Esc with nothing open: menuOpen={_gameMenuOpen}"));
                Step(() => InjectKey(Key.Escape));
                Step(() => GD.Print($"SCN Esc closes menu: menuOpen={_gameMenuOpen}"));
                Step(() => InjectKey(Key.O));
                Step(() => GD.Print($"SCN O diplomacy={_diplomacyOpen}"));
                Step(() => InjectKey(Key.Escape));
                Step(() => GD.Print($"SCN Esc closes diplomacy={!_diplomacyOpen}"));
                Step(() => InjectWheel(new Vector2(800, 450), true));
                Step(() => InjectWheel(new Vector2(800, 450), true), 0.9f);
                Step(() => GD.Print($"SCN wheel up x2 zoomTarget={_zoomTarget:0.00} zoom={_zoom:0.00}"));
                Step(() => InjectDrag(new Vector2(700, 400), new Vector2(800, 400)), 0.6f);
                Step(() => GD.Print($"SCN left-drag target={_target}"));
                Step(() => InjectRightDrag(new Vector2(700, 400), new Vector2(760, 420)), 0.6f);
                Step(() => GD.Print($"SCN right-drag yaw={_yaw:0.00} pitch={_pitch:0.00}"));
                Step(() => InjectKey(Key.B));
                Step(() => GD.Print($"SCN B beaconMode={_beaconMode}"));
                Step(() => InjectClick(new Vector2(800, 450), MouseButton.Right));
                Step(() => GD.Print($"SCN right-click placed nothing; beaconMode={_beaconMode} queueEvents ok"));
                Step(() => InjectKey(Key.Key3));
                Step(() => GD.Print($"SCN key 3 (zoom Z3) zoomTarget={_zoomTarget:0.00}"), 1.0f);
                Step(() => InjectKey(Key.H));
                Step(() => GD.Print($"SCN H help={_helpOpen} modal={_ui.AnyModal}"));
                Step(() => InjectKey(Key.Space));
                Step(() => GD.Print($"SCN Space in help paused={_session!.Paused}"));
                Step(() => InjectKey(Key.Escape));
                Step(() => GD.Print($"SCN Esc closes help={!_helpOpen}"));
                break;
            case "popovers":
                Step(() => OpenResearchWeb(-1));
                Step(OnCtaPressed);
                Step(SelectFirstAlmostReady);
                Step(OnCtaPressed);
                Step(ToggleQueuePopover);
                Shot("pop-queue");
                Step(CloseQueuePopover);
                Step(ToggleLeaningPopover);
                Shot("pop-leaning");
                Step(CloseLeaningPopover);
                Step(CloseResearchWeb);
                Step(() => OpenSlotMenu(0, new Vector2(300, 800)));
                Shot("pop-slotmenu");
                break;
            case "session":
                Step(SaveQuickMatch);
                Shot("session-saved");
                Step(LoadQuickMatch, 1.5f);
                Shot("session-loaded");
                Step(() => StartNewMatch(unchecked(_matchSeed + 1)), 1.5f);
                Shot("session-newmatch");
                break;
            case "beacon":
                Step(() => GrantProject(FindProjectByName("Songlines")), 1.5f);
                Step(() => GD.Print($"SCN beacons allowed={_current!.FocusBeaconsAllowed} placed={_current.FocusBeaconsPlaced}"));
                Step(() => InjectKey(Key.B));
                Step(() => GD.Print($"SCN beaconMode={_beaconMode}"));
                Step(() => InjectClick(new Vector2(900, 380), MouseButton.Left), 2.0f);
                Step(() => { TryPickTile(new Vector2(900, 380), out int pt); GD.Print($"SCN after click placed={_current!.FocusBeaconsPlaced} count={_current.BeaconCount} beaconTile={(_current.BeaconCount > 0 ? _current.BeaconTile[0] : -1)} pickedTile={pt} settlementTile={_current.SettlementTile[0]} w={_width}"); });
                Shot("beacon-placed");
                Step(() => ZoomTo(ZoomPresets[1]), 1.0f);
                Shot("beacon-z1");
                break;
            case "glide":
                Step(() => { FrameCapital(); _target += new Vector3(1.5f, 0, 1.0f); ZoomTo(9.0f); }, 1.6f);
                for (int i = 0; i < 6; i++)
                {
                    int n = i;
                    Step(() => GD.Print($"SCN glide {n} alpha={FrameAlpha(_renderTick):0.00} tick={_renderTick:0.0}"), 0.0f);
                    Step(() => TakeScenarioShot($"glide-{n}"), 0.2f);
                }
                break;
            case "tools":
                Step(() => { FrameCapital(); ZoomTo(2.0f); }, 2.0f);
                Step(() => _session!.SetPaused(true), 0.3f);
                Shot("tools-a");
                break;
            case "decor":
                Step(() =>
                {
                    foreach (string id in new[] { "BEL-02", "TER-03", "STN-10", "FIB-02", "CER-02", "FIB-07", "BEL-05", "COM-04", "FIR-08", "MET-02",
                        "SEA-03", "MEC-02", "DEF-03", "HNT-05", "BEL-01", "GOV-01", "MIN-03", "INQ-02", "BLD-05", "FIB-05", "WOD-03" })
                        GrantProject(_catalog!.IndexOf(id));
                }, 4.0f);
                Step(() => { FrameCapital(); ZoomTo(4.5f); }, 1.5f);
                Shot("decor-a");
                Step(() => { ZoomTo(3.0f); }, 1.2f);
                Shot("decor-b");
                break;
            case "megalith":
                Step(() => { GrantProject(FindProjectByName("Megalith Raising")); _session!.SetSpeedIndex(2); }, 1.2f);
                Step(() => { _target = (Vector3)_visualRenderer!.Call("monument_focus"); ZoomTo(4.2f); }, 0.2f);
                for (int i = 0; i < 7; i++)
                {
                    int n = i;
                    Shot($"mega-haul-{n}");
                    Step(() => { }, 0.9f);
                }
                Step(() => _session!.SetSpeedIndex(3), 20.0f);
                Shot("mega-mid");
                Step(() => { }, 14.0f);
                Shot("mega-lintel");
                Step(() => { }, 16.0f);
                Shot("mega-final");
                Step(() => ZoomTo(7.0f), 0.8f);
                Shot("mega-wide");
                break;
            case "trails":
                Step(() => { _session!.SetSpeedIndex(0); _target = (Vector3)_visualRenderer!.Call("trail_focus"); ZoomTo(7.0f); }, 1.5f);
                Shot("trail-a");
                Step(() => { ZoomTo(14.0f); }, 1.0f);
                Shot("trail-b");
                Step(() => { ZoomTo(4.0f); }, 1.0f);
                Shot("trail-close");
                Step(() =>
                {
                    GrantProject(FindProjectByName("Landmarks & Trails"));
                    GrantProject(FindProjectByName("Exchange Networks"));
                    ZoomTo(7.0f);
                }, 2.5f);
                Shot("trail-worn");
                Step(() => ZoomTo(26.0f), 1.5f);
                Shot("trail-wide");
                break;
            case "boats":
                Step(() => { FrameCapital(); ZoomTo(3.8f); _session!.SetSpeedIndex(1); }, 1.0f);
                foreach (string boat in new[] { "Floats & Rafts", "Bark & Hide Canoes", "Dugout Canoes", "Outriggers" })
                {
                    string boatName = boat;
                    Step(() => GrantProject(FindProjectByName(boatName)), 2.2f);
                    Shot("boats-" + boatName.Split(' ')[0].ToLowerInvariant());
                }
                Step(() => ZoomTo(2.4f), 1.0f);
                Shot("boats-close");
                break;
            case "voyage":
                // needs: --start-minute=15 --dev-island-colony (debug builds): civ 0's fighters now live on an island
                Step(() =>
                {
                    _session!.Queue(PlayerCommands.LaunchRaidAgainst(_session.Tick, 0, 1, 8));
                    _session.SetSpeedIndex(2);
                }, 1.0f);
                for (int i = 0; i < 30; i++)
                    Step(() =>
                    {
                        if (_voyageFound || _current is null) return;
                        for (int f = 0; f < _current.FigureCount; f++)
                        {
                            int tile = _current.FigureTile[f];
                            if (_current.FigureActivity[f] != (byte)FigureActivity.OnRaid || _session!.World.Tiles[tile].IsLand) continue;
                            _voyageFound = true;
                            int width = _current.Width;
                            Vector3 c = TileCenter(tile % width, tile / width);
                            MoveCameraTo(new Vector3(c.X, 0, c.Z), 3.4f, instant: true);
                            _session.SetPaused(true);
                            break;
                        }
                    }, 0.45f);
                Step(() => { }, 0.6f);
                Shot("voyage-a");
                Step(() => { _session!.SetPaused(false); _session.SetSpeedIndex(0); }, 1.5f);
                Shot("voyage-b");
                break;
            case "signals":
                Step(() => { _session!.SetSpeedIndex(0); GrantProject(FindProjectByName("Signal Fires & Drums")); }, 2.0f);
                Step(() => { GD.Print($"SCN signals posts={(int)_visualRenderer!.Call("signal_count")}"); _target = (Vector3)_visualRenderer!.Call("signal_focus"); ZoomTo(7.0f); }, 1.0f);
                for (int i = 0; i < 4; i++)
                {
                    int n = i;
                    Shot($"signals-{n}");
                    Step(() => { }, 1.7f);
                }
                Step(() => ZoomTo(2.8f), 1.0f);
                Shot("signals-close");
                Step(() => ZoomTo(26.0f), 1.2f);
                Shot("signals-wide");
                break;
            case "dogs":
                // --start-minute=14: learn Wolf Companions and Travois & Sledges, then follow a village dog and a dog hauling along a trail
                Step(() => { FrameCapital(); ZoomTo(9.0f); GrantProject(FindProjectByName("Wolf Companions")); GrantProject(FindProjectByName("Travois & Sledges")); _session!.SetPaused(false); _session.SetSpeedIndex(1); }, 3.0f);
                Step(() => { _target = (Vector3)_visualRenderer!.Call("dog_focus"); ZoomTo(1.7f); _session!.SetPaused(false); _session.SetSpeedIndex(1); }, 0.5f);
                for (int i = 0; i < 5; i++)
                {
                    int n = i;
                    Step(() => _target = (Vector3)_visualRenderer!.Call("dog_focus"), 1.2f);
                    Shot($"dogs-village-{n}");
                    Step(() => { }, 2.5f);
                }
                Step(() => { _target = (Vector3)_visualRenderer!.Call("haul_focus"); ZoomTo(1.7f); }, 0.5f);
                for (int i = 0; i < 5; i++)
                {
                    int n = i;
                    Step(() => { _target = (Vector3)_visualRenderer!.Call("haul_focus"); }, 1.2f);
                    Shot($"dogs-haul-{n}");
                    Step(() => { }, 2.5f);
                }
                break;
            case "looks":
                // --start-minute=30 --viewer=-1 --looks-grant: give every civilization the projects of the first three ages (but the
                // keystones, which end the match), let them settle in at speed, then tour what the looks leave on the map: a village with
                // its temple and chief's hall, the people, an ard team and the resting fields, a cart on its trail, the ships, a chariot.
                // Without --looks-grant the tour shows whatever the civilizations have learned by themselves; --looks-debug forces every
                // look on instead. The sim ends the match at year 64, so start before that.
                if (HasArgument("--looks-grant"))
                {
                    Step(() =>
                    {
                        for (int civ = 0; civ < 4; civ++)
                            foreach (var def in _catalog!.Projects)
                                if (def.Numeric && def.Era <= 3 && !def.Id.StartsWith("KEY-", StringComparison.Ordinal))
                                    _session!.Queue(new Command(_session.Tick, (byte)civ, CommandKind.CompleteProject, def.Index, 0, 0));
                        _session!.SetPaused(false);
                        _session.SetSpeedIndex(3);
                    }, 100.0f);
                }
                void Look(string focus, float zoom, string? kind = null)
                {
                    Vector3 at = kind is null ? (Vector3)_visualRenderer!.Call(focus) : (Vector3)_visualRenderer!.Call(focus, kind);
                    if (at != Vector3.Zero) { _target = at; ZoomTo(zoom); UpdateCamera(); }
                }
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(false);
                    _session.SetSpeedIndex(1);
                    FrameCapital();
                    Look("landmark_focus", 5.0f, "temple");
                }, 3.0f);
                Shot("looks-village");
                Step(() => ZoomTo(3.0f), 1.2f);
                Shot("looks-village-close");
                Step(() => Look("landmark_focus", 2.0f, "chiefhall"), 1.2f);
                Shot("looks-hall");
                for (int i = 0; i < 4; i++)
                {
                    int n = i;
                    Step(() => Look("team_focus", 1.7f), 1.0f);
                    Shot($"looks-ard-{n}");
                    Step(() => { }, 3.0f);
                }
                Step(() => Look("graze_focus", 2.4f), 1.0f);
                Shot("looks-fallow");
                for (int i = 0; i < 3; i++)
                {
                    int n = i;
                    Step(() => Look("cart_focus", 1.7f), 1.0f);
                    Shot($"looks-cart-{n}");
                    Step(() => { }, 3.0f);
                }
                Step(() => Look("boat_focus", 1.8f), 1.0f);
                Shot("looks-boat");
                Step(() => Look("chariot_focus", 1.7f), 1.0f);
                Shot("looks-chariot");
                Step(() => Look("landmark_focus", 12.0f, "temple"), 1.2f);
                Shot("looks-wide");
                break;
            case "ziggurat":
                // --start-minute=30 --viewer=-1 --dev-ziggurat=0..4 (debug builds; 1 to 3 = the stages of a ziggurat going up, 4 = built):
                // the BLD-08 ziggurat in its city, from afar, close up, then a few moments of the procession or the carriers.
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(false);
                    _session.SetSpeedIndex(1);
                    FrameCapital();
                }, 3.0f);
                void ZigLook(float zoom)
                {
                    Vector3 at = (Vector3)_visualRenderer!.Call("landmark_focus", "ziggurat");
                    if (at != Vector3.Zero) { _target = at; ZoomTo(zoom); UpdateCamera(); }
                }
                Step(() => ZigLook(8.0f), 1.5f);
                Shot("ziggurat-wide");
                Step(() => ZigLook(3.6f), 1.2f);
                Shot("ziggurat-village");
                for (int i = 0; i < 6; i++)
                {
                    int n = i;
                    Step(() => ZigLook(2.2f), 1.0f);
                    Shot($"ziggurat-close-{n}");
                    Step(() => { }, 2.2f);
                }
                break;
            case "leanto-village":
                // --start-minute=30 --viewer=-1 --dev-leanto=N (debug builds): the capital as a camp of lean-tos, from afar and close up
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(true);
                    FrameCapital();
                }, 2.0f);
                foreach (var (leantoShot, leantoZoom) in new[] { ("wide", 7.0f), ("village", 4.0f), ("close", 2.7f) })
                {
                    float zoom = leantoZoom;
                    string shot = leantoShot;
                    Step(() => { FrameCapital(); ZoomTo(zoom); UpdateCamera(); }, 1.2f);
                    Shot($"leanto-{shot}");
                }
                break;
            case "buildings-gallery":
                // every building and village prop at village scale, one row per shot (rows are laid out in LowPoly3DView.show_buildings_gallery)
                Vector3 buildingsOrigin = Vector3.Zero;
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(true);
                    FrameCapital();
                }, 1.5f);
                Step(() => buildingsOrigin = (Vector3)_visualRenderer!.Call("show_buildings_gallery"), 1.0f);
                // --rows=dwellings,yard limits the tour to those rows (names below)
                string[]? onlyRows = ArgumentValue("--rows=")?.Split(',');
                foreach (var (rowName, row) in new[]
                {
                    ("dwellings", 0), ("yard", 1), ("defense-land", 2), ("decor-1", 3), ("decor-2", 4), ("decor-3", 5), ("landmarks", 6),
                })
                {
                    if (onlyRows is not null && Array.IndexOf(onlyRows, rowName) < 0) continue;
                    string label = rowName;
                    float z = 4.2f - 1.4f * row;
                    foreach (var (half, dx) in new[] { ("a", -1.5f), ("b", 0.0f), ("c", 1.5f) })
                    {
                        string side = half;
                        float x = dx;
                        Step(() =>
                        {
                            _yaw = _yawTarget = Mathf.DegToRad(0.0f);
                            _pitch = Mathf.DegToRad(40.0f);
                            _target = new Vector3(buildingsOrigin.X + x, buildingsOrigin.Y, buildingsOrigin.Z + z - 0.05f);
                            ZoomTo(2.7f);
                            UpdateCamera();
                        }, 1.0f);
                        Shot($"buildings-{label}-{side}");
                    }
                }
                break;
            case "looks-gallery":
                // lays the meshes of the looks projects out in rows on a stage above the map and photographs them row by row
                Vector3 galleryOrigin = Vector3.Zero;
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(true);
                    FrameCapital();
                }, 1.5f);
                Step(() =>
                {
                    Vector3 at = (Vector3)_visualRenderer!.Call("show_gallery");
                    galleryOrigin = at;
                    _target = at;
                    _yaw = _yawTarget = Mathf.DegToRad(0.0f);
                    _pitch = Mathf.DegToRad(52.0f);
                    ZoomTo(9.0f);
                    UpdateCamera();
                }, 1.5f);
                Shot("gallery-all");
                // (name, x, z across the stage, zoom): the rows are laid out in LowPoly3DView.show_gallery
                foreach (var (item, dx, dz, zoom) in new[]
                {
                    ("people", 0.0f, 2.8f, 2.7f), ("temple", -1.25f, 1.5f, 2.7f), ("chiefhall", -0.45f, 1.5f, 2.7f), ("wool-chariot", 0.5f, 1.5f, 2.7f),
                    ("ships", 0.25f, 0.3f, 2.7f), ("raider", 1.1f, 0.3f, 2.7f), ("ard", -0.65f, -0.9f, 2.7f), ("cart", 0.6f, -0.9f, 2.7f),
                    ("fields", 0.0f, -2.3f, 3.6f),
                })
                {
                    string itemName = item;
                    float x = dx, z = dz, zm = zoom;
                    Step(() =>
                    {
                        _pitch = Mathf.DegToRad(itemName == "fields" ? 55.0f : 38.0f);
                        _target = new Vector3(galleryOrigin.X + x, galleryOrigin.Y, galleryOrigin.Z + z);
                        ZoomTo(zm);
                        UpdateCamera();
                    }, 1.4f);
                    Shot($"gallery-{itemName}");
                }
                break;
            case "burn":
                // --start-minute=14: learn both projects, run fast until the first fire, then watch it at half speed and the years after
                Step(() => { FrameCapital(); ZoomTo(6.0f); GrantProject(FindProjectByName("Landscape Burning")); GrantProject(FindProjectByName("Slash-and-Burn")); _session!.SetSpeedIndex(3); }, 0.5f);
                for (int i = 0; i < 260; i++)
                    Step(() =>
                    {
                        if (_burnSeen) return;
                        var f = (Vector3)_visualRenderer!.Call("burn_focus");
                        if (f != Vector3.Zero) { _burnSeen = true; _target = f; ZoomTo(3.4f); _session!.SetSpeedIndex(0); }
                    }, 0.05f);
                for (int i = 0; i < 7; i++)
                {
                    int n = i;
                    Shot($"burn-{n}");
                    Step(() => { }, 2.2f);
                }
                Step(() => { _session!.SetSpeedIndex(3); ZoomTo(4.6f); }, 6.0f);
                Shot("burn-after");
                Step(() => { }, 20.0f);
                Shot("burn-later");
                Step(() => { }, 40.0f);
                Shot("burn-regrown");
                break;
            case "feast":
                // --start-minute=14: learn Feasting, run fast until the first feast, then watch it at half speed
                Step(() => { FrameCapital(); ZoomTo(6.0f); GrantProject(FindProjectByName("Feasting")); _session!.SetSpeedIndex(3); }, 0.5f);
                for (int i = 0; i < 260; i++)
                    Step(() =>
                    {
                        if (_feastSeen) return;
                        var f = (Vector3)_visualRenderer!.Call("feast_focus");
                        if (f != Vector3.Zero) { _feastSeen = true; _target = f; ZoomTo(3.3f); _session!.SetSpeedIndex(0); }
                    }, 0.05f);
                for (int i = 0; i < 8; i++)
                {
                    int n = i;
                    Shot($"feast-{n}");
                    Step(() => { }, 3.0f);
                }
                break;
            case "glide-fixed":
                Step(() => { FrameCapital(); _target += new Vector3(1.5f, 0, 1.0f); ZoomTo(5.0f); _session!.SetPaused(true); }, 1.6f);
                Step(() => _lerpOverride = 0.0f, 0.4f);
                Shot("fixed-0");
                Step(() => _lerpOverride = 0.5f, 0.4f);
                Shot("fixed-5");
                Step(() => _lerpOverride = 1.0f, 0.4f);
                Shot("fixed-10");
                break;
            case "skirmish":
                // --start-minute=30 --viewer=-1 [--reduce-motion]: civ 1 raids civ 0's largest village three times (raiders win, defenders
                // win, a raiders' assault) and each fight is photographed through its stages; onDone is logged with its time
                int skTile = -1;
                Step(() =>
                {
                    if (_hudLayer is not null) _hudLayer.Visible = false;
                    if (_scrimLayer is not null) _scrimLayer.Visible = false;
                    _visualRenderer!.Call("lock_season", 0);
                    _session!.SetPaused(true);
                    int best = -1;
                    for (int i = 0; i < _current!.SettlementCount; i++)
                        if (_current.SettlementCiv[i] == 0 && (best < 0 || _current.SettlementPopulation[i] > _current.SettlementPopulation[best])) best = i;
                    skTile = best >= 0 ? _current.SettlementTile[best] : -1;
                    Vector3 c = TileCenter(skTile % _current.Width, skTile / _current.Width);
                    MoveCameraTo(new Vector3(c.X, _terrainHeights[skTile], c.Z), 3.0f, instant: true);
                    UpdateCamera();
                }, 1.5f);
                foreach (var (label, spec) in new[]
                {
                    ("win", new SkirmishSpec(0, 1, 0, true, 8, 6, 1, 4)),
                    ("hold", new SkirmishSpec(0, 1, 0, false, 6, 14, 4, 2)),
                    ("assault", new SkirmishSpec(0, 1, 0, true, 22, 18, 5, 12, Assault: true)),
                })
                {
                    string skLabel = label;
                    SkirmishSpec skSpec = spec;
                    float started = 0, startAt = t;
                    Step(() =>
                    {
                        started = _scenarioClock;
                        bool ok = StartSkirmish(skSpec with { Tile = skTile }, () => GD.Print($"SCN skirmish {skLabel} done after {_scenarioClock - started:0.00}s"));
                        Vector3 f = (Vector3)_visualRenderer!.Call("skirmish_focus");
                        GD.Print($"SCN skirmish {skLabel} started={ok} tile={skTile} focus={f}");
                        if (f != Vector3.Zero) { _target = f; UpdateCamera(); }
                    }, 0.35f);
                    foreach (float at in new[] { 0.35f, 0.8f, 1.3f, 1.8f, 2.3f, 2.8f, 3.3f, 3.9f, 4.4f })
                    {
                        string shotName = $"skirmish-{skLabel}-{at:0.00}s";
                        _scenario.Add((startAt + at, () => TakeScenarioShot(shotName)));
                    }
                    t = startAt + 4.8f + 1.2f;
                }
                Step(() => ZoomTo(9.0f), 0.2f);
                Step(() => StartSkirmish(new SkirmishSpec(skTile, 1, 0, true, 8, 6, 1, 4), () => GD.Print("SCN skirmish wide done")), 1.6f);
                Shot("skirmish-wide");
                Step(() => { }, 3.0f);
                break;
            case "dev":
                Step(ToggleDevOverlay);
                Step(() => OpenResearchWeb(-1), 3.0f);
                Step(() => GD.Print($"SCN ui_ms_with_web={_uiUpdateMs:0.000} refresh_ms={_hudRefreshMs:0.000} fps={Engine.GetFramesPerSecond()} figures={_current!.FigureCount}"), 0.2f);
                Shot("dev");
                Step(CloseResearchWeb, 3.0f);
                Step(() => GD.Print($"SCN ui_ms_idle={_uiUpdateMs:0.000} refresh_ms={_hudRefreshMs:0.000} fps={Engine.GetFramesPerSecond()}"), 0.2f);
                break;
            default:
                GD.PushWarning($"Unknown scenario '{name}'.");
                return;
        }
        Step(() => GetTree().Quit(), 0.3f);
        _scenarioActive = true;
    }

    private static void InjectKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private static void InjectWheel(Vector2 position, bool up)
    {
        var press = new InputEventMouseButton { ButtonIndex = up ? MouseButton.WheelUp : MouseButton.WheelDown, Pressed = true, Position = position, GlobalPosition = position };
        Input.ParseInputEvent(press);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = up ? MouseButton.WheelUp : MouseButton.WheelDown, Pressed = false, Position = position, GlobalPosition = position });
    }

    private static void InjectClick(Vector2 position, MouseButton button)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = position, GlobalPosition = position });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = position, GlobalPosition = position });
    }

    private static void InjectDrag(Vector2 from, Vector2 to, MouseButton button = MouseButton.Left)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = from, GlobalPosition = from });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = from, GlobalPosition = from });
        const int steps = 6;
        Vector2 last = from;
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = from.Lerp(to, i / (float)steps);
            Input.ParseInputEvent(new InputEventMouseMotion
            {
                Position = p, GlobalPosition = p, Relative = p - last,
                ButtonMask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right,
            });
            last = p;
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = to, GlobalPosition = to });
    }

    private static void InjectRightDrag(Vector2 from, Vector2 to) => InjectDrag(from, to, MouseButton.Right);

    private void TakeScenarioShot(string name)
    {
        Error error = SaveScreenshot(Path.Combine(_scenarioDir!, name + ".png"));
        GD.Print($"Scenario shot {name}: {error}");
    }

    private void TickScenario(float delta)
    {
        if (!_scenarioActive) return;
        _scenarioClock += delta;
        while (_scenarioIndex < _scenario.Count && _scenarioClock >= _scenario[_scenarioIndex].At)
        {
            Action run = _scenario[_scenarioIndex].Run;
            _scenarioIndex++;
            run();
        }
    }

    private int FindProjectByName(string name)
    {
        if (_catalog is null) return -1;
        foreach (var def in _catalog.Projects) if (def.Name == name) return def.Index;
        return -1;
    }

    /// <summary>Test grant through the normal command path (CommandKind.CompleteProject).</summary>
    private bool _voyageFound, _burnSeen, _feastSeen;

    private void GrantProject(int project)
    {
        if (_session is null || project < 0) return;
        _session.Queue(new Command(_session.Tick, 0, CommandKind.CompleteProject, project, 0, 0));
    }
}
