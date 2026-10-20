using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;
using Godot;

namespace CivSim.Game;

public partial class Game
{
    private void CreateM7cHud(CanvasLayer layer)
    {
        var overlays = new PanelContainer
        {
            Name = "MapOverlays",
            Position = new Vector2(14, 122),
            CustomMinimumSize = new Vector2(142, 0),
        };
        var overlayColumn = new VBoxContainer();
        overlayColumn.AddThemeConstantOverride("separation", 4);
        overlayColumn.AddChild(new Label { Text = "MAP LAYERS" });
        _claimsOverlayButton = OverlayButton("Claims", () => SetMapOverlay(1));
        _foodOverlayButton = OverlayButton("Food", () => SetMapOverlay(2));
        _methodsOverlayButton = OverlayButton("Methods", () => SetMapOverlay(3));
        overlayColumn.AddChild(_claimsOverlayButton);
        overlayColumn.AddChild(_foodOverlayButton);
        overlayColumn.AddChild(_methodsOverlayButton);
        _resourcesOverlayButton = new Button { Text = "Resources  [R]", ToggleMode = true, ButtonPressed = _resourcesVisible };
        _resourcesOverlayButton.Pressed += () =>
        {
            _resourcesVisible = !_resourcesVisible;
            _resourcesOverlayButton.ButtonPressed = _resourcesVisible;
            if (_current is not null) UpdateKnownResources(_current);
            RefreshOverlayButtons();
        };
        overlayColumn.AddChild(_resourcesOverlayButton);
        _overlayLegendLabel = new Label
        {
            Text = "Territory tint is always visible. Methods: purple rung; amber means a better rung is blocked.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 48),
        };
        _overlayLegendLabel.AddThemeFontSizeOverride("font_size", 11);
        _overlayLegendLabel.AddThemeColorOverride("font_color", new Color("#d2cfbf"));
        overlayColumn.AddChild(_overlayLegendLabel);
        overlays.AddChild(overlayColumn);
        layer.AddChild(overlays);

        var hoverPanel = new PanelContainer
        {
            Name = "TileHoverCard",
            CustomMinimumSize = new Vector2(340, 0),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var hoverMargin = new MarginContainer();
        hoverMargin.AddThemeConstantOverride("margin_left", 10);
        hoverMargin.AddThemeConstantOverride("margin_right", 10);
        hoverMargin.AddThemeConstantOverride("margin_top", 7);
        hoverMargin.AddThemeConstantOverride("margin_bottom", 7);
        var hoverColumn = new VBoxContainer();
        hoverColumn.AddThemeConstantOverride("separation", 3);
        _tileHoverTitle = new Label { Name = "TileTitle", Text = "Tile", MouseFilter = Control.MouseFilterEnum.Ignore };
        _tileHoverTitle.AddThemeFontSizeOverride("font_size", 15);
        _tileHoverLabel = new RichTextLabel
        {
            Name = "TileDetails",
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(320, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _tileHoverLabel.AddThemeFontSizeOverride("normal_font_size", 12);
        _tileHoverLabel.AddThemeColorOverride("default_color", new Color("#e8e4d5"));
        hoverColumn.AddChild(_tileHoverTitle);
        hoverColumn.AddChild(_tileHoverLabel);
        hoverMargin.AddChild(hoverColumn);
        hoverPanel.AddChild(hoverMargin);
        layer.AddChild(hoverPanel);
        _tileHoverPanel = hoverPanel;

        _chroniclePanel = new PanelContainer
        {
            Name = "Chronicle",
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -340,
            OffsetRight = -14,
            OffsetTop = 370,
            OffsetBottom = 690,
            Visible = _chronicleVisible,
        };
        var chronicleColumn = new VBoxContainer();
        chronicleColumn.AddThemeConstantOverride("separation", 5);
        var header = new HBoxContainer();
        var title = new Label { Text = "Chronicle", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 17);
        header.AddChild(title);
        _chronicleToggle = new Button { Text = "Hide  [C]", FocusMode = Control.FocusModeEnum.None };
        _chronicleToggle.Pressed += ToggleChronicle;
        header.AddChild(_chronicleToggle);
        chronicleColumn.AddChild(header);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _chronicleList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _chronicleList.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_chronicleList);
        chronicleColumn.AddChild(scroll);
        _chroniclePanel.AddChild(chronicleColumn);
        layer.AddChild(_chroniclePanel);

        var momentPanel = new PanelContainer
        {
            Name = "MomentBanner",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -260,
            OffsetRight = 260,
            OffsetTop = 112,
            OffsetBottom = 238,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var momentMargin = new MarginContainer();
        momentMargin.AddThemeConstantOverride("margin_left", 14);
        momentMargin.AddThemeConstantOverride("margin_right", 14);
        momentMargin.AddThemeConstantOverride("margin_top", 10);
        momentMargin.AddThemeConstantOverride("margin_bottom", 10);
        _momentLabel = new Label
        {
            Name = "MomentText",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _momentLabel.AddThemeFontSizeOverride("font_size", 16);
        _momentLabel.AddThemeColorOverride("font_color", new Color("#fff1c9"));
        momentMargin.AddChild(_momentLabel);
        momentPanel.AddChild(momentMargin);
        layer.AddChild(momentPanel);
        _momentPanel = momentPanel;

        _visualRenderer?.Call("set_overlay", _mapOverlay);
        RefreshOverlayButtons();
        if (_current is not null) ProcessViewEvents(_current);
    }

    private PanelContainer? _momentPanel;

    private static Button OverlayButton(string name, Action activate)
    {
        var button = new Button
        {
            Text = name,
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += activate;
        return button;
    }

    private void SetMapOverlay(int mode)
    {
        _mapOverlay = _mapOverlay == mode ? 0 : mode;
        _visualRenderer?.Call("set_overlay", _mapOverlay);
        RefreshOverlayButtons();
    }

    private void RefreshOverlayButtons()
    {
        if (_claimsOverlayButton is not null) _claimsOverlayButton.ButtonPressed = _mapOverlay == 1;
        if (_foodOverlayButton is not null) _foodOverlayButton.ButtonPressed = _mapOverlay == 2;
        if (_methodsOverlayButton is not null) _methodsOverlayButton.ButtonPressed = _mapOverlay == 3;
        if (_overlayLegendLabel is not null)
            _overlayLegendLabel.Text = _resourcesVisible
                ? "Known resource deposits on explored tiles."
                : _mapOverlay switch
                {
                    1 => "Visible claim strength · richer tint means a stronger claim.",
                    2 => "Settlement food band · autumn and winter show the winter forecast.",
                    3 => "Purple: usable method rung · amber: a better rung is blocked.",
                    _ => "Territory tint is always visible.",
                };
    }

    private void ToggleChronicle()
    {
        _chronicleVisible = !_chronicleVisible;
        if (_chroniclePanel is not null) _chroniclePanel.Visible = _chronicleVisible;
        if (_chronicleToggle is not null) _chronicleToggle.Text = _chronicleVisible ? "Hide  [C]" : "Show  [C]";
    }

    private bool HandleM7cInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return false;
        if (key.Keycode != Key.C) return false;
        ToggleChronicle();
        return true;
    }

    private void UpdateM7cHud(MatchFrame frame)
    {
        if (_momentPanel is not null) _momentPanel.Visible = _momentTimer > 0 && _momentLabel is not null;
        if (_chronicleList is not null && _chronicleEntries.Count == 0 && _chronicleList.GetChildCount() == 0)
            _chronicleList.AddChild(new Label { Text = $"Year {frame.Year} · {SeasonName(frame.SeasonOfYear)}\nYour people's story will appear here." });
    }

    private void ProcessViewEvents(MatchFrame frame)
    {
        int viewer = Math.Max(0, frame.ViewerCiv);
        for (int i = 0; i < frame.EventCount; i++)
        {
            ViewEvent item = frame.Events[i];
            if (item.Sequence <= _lastEventSequence) continue;
            _lastEventSequence = item.Sequence;
            bool publicBanner = item.Kind is ViewEventKind.FirstClaimed or ViewEventKind.EraAdvanced;
            if (item.Civ >= 0 && item.Civ != viewer && !publicBanner) continue;

            string line = DescribeEvent(item, viewer);
            if (line.Length > 0) AddChronicleEntry(item, line);
            if (item.Kind == ViewEventKind.Moment && item.Civ == viewer && item.A == 1)
                ShowMoment(item);

            bool alert = item.Tier == ViewEventTier.Critical || item.Kind == ViewEventKind.OrderRefused;
            if (alert && line.Length > 0)
            {
                _alertMessage = line;
                _alertTimer = 6.0f;
            }
        }
        RefreshChronicleText();
    }

    private void AddChronicleEntry(ViewEvent item, string text)
    {
        int year = item.Kind == ViewEventKind.SeasonBegan ? Math.Max(1, item.B) : Math.Max(1, (int)(item.Tick / 600) + 1);
        string date = $"Y{year} · ";
        _chronicleEntries.Insert(0, date + text);
        _chronicleSubjects.Insert(0, item);
        if (_chronicleEntries.Count > 8) _chronicleEntries.RemoveAt(_chronicleEntries.Count - 1);
        if (_chronicleSubjects.Count > 8) _chronicleSubjects.RemoveAt(_chronicleSubjects.Count - 1);
    }

    private void RefreshChronicleText()
    {
        if (_chronicleList is null) return;
        foreach (Node child in _chronicleList.GetChildren())
        {
            _chronicleList.RemoveChild(child);
            child.QueueFree();
        }
        if (_chronicleEntries.Count == 0)
        {
            _chronicleList.AddChild(new Label { Text = "The story of your people will appear here." });
            return;
        }
        for (int i = 0; i < _chronicleEntries.Count; i++)
        {
            ViewEvent item = _chronicleSubjects[i];
            if (item.Tile < 0)
            {
                var label = new Label
                {
                    Text = _chronicleEntries[i],
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(0, 30),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                label.AddThemeFontSizeOverride("font_size", 13);
                _chronicleList.AddChild(label);
                continue;
            }
            var row = new Button
            {
                Text = _chronicleEntries[i],
                Alignment = HorizontalAlignment.Left,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Flat = true,
                FocusMode = Control.FocusModeEnum.None,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 38),
                TooltipText = "Go to this event's location",
            };
            row.Pressed += () => FocusChronicleTile(item.Tile);
            _chronicleList.AddChild(row);
        }
    }

    private void FocusChronicleTile(int tile)
    {
        if (_camera is null || _current is null || tile < 0 || tile >= _current.Width * _current.Height) return;
        Vector3 center = TileCenter(tile % _current.Width, tile / _current.Width);
        _target = new Vector3(center.X, _terrainHeights[tile], center.Z);
        SetZoom(Mathf.Min(_zoom, 10.0f));
        UpdateCamera();
    }

    private string DescribeEvent(ViewEvent item, int viewer)
    {
        string project = ProjectName(item.Project);
        string other = item.Other >= 0 ? "a neighboring people" : "another people";
        return item.Kind switch
        {
            ViewEventKind.SeasonBegan => $"{SeasonName(item.A)} begins{(item.C != 0 ? " after a harsh winter" : "")}.",
            ViewEventKind.Births => $"{item.A} {(item.A == 1 ? "child was" : "children were")} born.",
            ViewEventKind.Deaths => $"{item.B} {(item.B == 1 ? "person died" : "people died")} this season.",
            ViewEventKind.SettlementFounded => item.A == 1 ? "Free Folk founded a camp." : "A new settlement takes root.",
            ViewEventKind.StructureBuilt => $"{StructureName(item.A)} completed.",
            ViewEventKind.StructureLost => $"A {StructureName(item.A)} was lost.",
            ViewEventKind.FireOut => "The hearth has gone cold.",
            ViewEventKind.FamineRisk => item.B == 1 ? "Winter stores are at risk of famine." : "A settlement has entered famine.",
            ViewEventKind.PartyIncoming => item.A == (int)PartyKind.Assault ? "An assault party is approaching." : "A hostile party is approaching.",
            ViewEventKind.SlotFilled => $"{project} entered a focus slot.",
            ViewEventKind.CouncilPick => $"The council chose {project}.",
            ViewEventKind.ProjectCompleted => $"{project} is now known.",
            ViewEventKind.Moment => $"{project}: {ProjectMoment(item.Project)}",
            ViewEventKind.VisibleChange => $"{project} changes the world: {VisibleChangeName(item.A)}.",
            ViewEventKind.FirstClaimed => item.Civ == viewer ? $"Your people were first to {project}." : "A people claimed a world first.",
            ViewEventKind.EraAdvanced => item.Civ == viewer ? $"Your people entered a new era through {project}." : "A people entered a new era.",
            ViewEventKind.Adopted => $"{project} is known by {item.A}% of the people.",
            ViewEventKind.DormantChanged => $"{project} {(item.A == 1 ? "has gone dormant" : "is active again")}.",
            ViewEventKind.ResourceSeen => "A new resource has been discovered.",
            ViewEventKind.ProjectUnlocked => $"A new discovery is available: {project}.",
            ViewEventKind.Aware => $"You became aware of {other}.",
            ViewEventKind.Contact => $"First contact with {other}.",
            ViewEventKind.Skirmish => "A border skirmish left people hurt.",
            ViewEventKind.GiftArrived => "A gift arrived from a neighboring people.",
            ViewEventKind.BarterDone => "The barter party returned.",
            ViewEventKind.RaidLaunched => "A raid party set out.",
            ViewEventKind.RaidResolved => item.A switch { 1 => "The raid returned with loot.", 2 => "A raid struck one of your settlements.", 3 => "Your people repelled a raid.", _ => "The raid did not succeed." },
            ViewEventKind.WarDeclared => item.A == 1 ? "A neighboring people declared war." : "Your people declared war.",
            ViewEventKind.PeaceMade => "Peace was made with a neighboring people.",
            ViewEventKind.AssaultResolved => item.A switch { 2 => "A settlement fell in an assault.", 3 => "Your people held their settlement.", 1 => "Your assault succeeded.", _ => "Your assault was repelled." },
            ViewEventKind.Occupied => "A settlement is under occupation.",
            ViewEventKind.OccupationBroken => "An occupation was broken.",
            ViewEventKind.Captured => item.A < 0 ? "Your people lost a settlement." : "Your people captured a settlement.",
            ViewEventKind.FreeFolkFounded => "Refugees founded a Free Folk camp.",
            ViewEventKind.AllianceFormed => "An alliance was formed.",
            ViewEventKind.BeaconPlaced => "A Beacon was placed.",
            ViewEventKind.OrderRefused => RefusalText(item),
            _ => "",
        };
    }

    private void ShowMoment(ViewEvent item)
    {
        if (_catalog is null || _momentLabel is null) return;
        if (item.Project < 0 || item.Project >= _catalog.Projects.Count) return;
        ProjectDef def = _catalog.Projects[item.Project];
        string heading = def.IsKeystone ? $"ERA MOMENT · {def.Name}" : $"NEW KNOWLEDGE · {def.Name}";
        _momentLabel.Text = $"{heading}\n\n{def.Moment}\n\n{def.Flavor}";
        _momentTimer = def.IsKeystone ? 11.0f : 8.0f;
        if (_momentPanel is not null) _momentPanel.Visible = true;
    }

    private string ProjectName(int project)
    {
        if (_catalog is null || project < 0 || project >= _catalog.Projects.Count) return "A discovery";
        return _catalog.Projects[project].Name;
    }

    private string ProjectMoment(int project)
    {
        if (_catalog is null || project < 0 || project >= _catalog.Projects.Count) return "a new way of living takes hold";
        return _catalog.Projects[project].Moment;
    }

    private static string StructureName(int kind)
    {
        try { return LivingEconomyModel.StructureName(kind); }
        catch (ArgumentOutOfRangeException) { return "A structure"; }
    }

    private static string VisibleChangeName(int raw)
        => raw >= (int)VisibleChange.NewAction && raw <= (int)VisibleChange.None ? ((VisibleChange)raw) switch
        {
            VisibleChange.NewAction => "a new action",
            VisibleChange.ToolOrMethod => "a better tool or method",
            VisibleChange.Good => "a new good or tool",
            VisibleChange.Structure => "a new structure",
            VisibleChange.Role => "a new role",
            VisibleChange.Tier => "a settlement tier",
            VisibleChange.Overlay => "a new map layer or lever",
            VisibleChange.Behavior => "a change in people's work",
            VisibleChange.EraCelebration => "a new era",
            _ => "a new possibility",
        }
        : "a visible change";

    private void UpdateTileHover(Vector2 screenPosition)
    {
        if (_camera is null || _current is null || _session is null || _tileHoverPanel is null || _tileHoverLabel is null || _dragging)
        {
            HideTileHover();
            return;
        }
        Vector3 origin = _camera.ProjectRayOrigin(screenPosition);
        Vector3 ray = _camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(ray.Y) < 0.0001f)
        {
            HideTileHover();
            return;
        }
        float distance = -origin.Y / ray.Y;
        if (distance < 0)
        {
            HideTileHover();
            return;
        }
        Vector3 hit = origin + ray * distance;
        int tile = -1;
        for (int pass = 0; pass < 3; pass++)
        {
            tile = HexLayout.NearestTile((int)(hit.X * MapUnit), (int)(hit.Z / RowSpacing * MapUnit), _current.Width, _current.Height);
            if (tile < 0 || tile >= _terrainHeights.Length) break;
            distance = (_terrainHeights[tile] - origin.Y) / ray.Y;
            if (distance < 0) { tile = -1; break; }
            hit = origin + ray * distance;
        }
        if (tile < 0 || tile >= _current.Width * _current.Height)
        {
            HideTileHover();
            return;
        }
        _hoveredTile = tile;
        RefreshTileHover();
        _tileHoverPanel.Visible = true;
        Vector2 size = _tileHoverPanel.Size.X > 0 ? _tileHoverPanel.Size : new Vector2(340, 132);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 position = screenPosition + new Vector2(18, 20);
        if (position.X + size.X > viewport.X - 8) position.X = screenPosition.X - size.X - 18;
        if (position.Y + size.Y > viewport.Y - 8) position.Y = screenPosition.Y - size.Y - 20;
        _tileHoverPanel.Position = new Vector2(Mathf.Clamp(position.X, 8, Mathf.Max(8, viewport.X - size.X - 8)),
            Mathf.Clamp(position.Y, 8, Mathf.Max(8, viewport.Y - size.Y - 8)));
    }

    private void HideTileHover()
    {
        _hoveredTile = -1;
        if (_tileHoverPanel is not null) _tileHoverPanel.Visible = false;
    }

    private void RefreshTileHover()
    {
        if (_current is null || _session is null || _hoveredTile < 0 || _tileHoverLabel is null) return;
        int tile = _hoveredTile;
        MatchFrame frame = _current;
        WorldView world = _session.WorldView;
        TileVisibility visibility = (TileVisibility)frame.Visibility[tile];
        string terrain = TerrainName((Biome)world.Biome[tile]);
        string owner = visibility == TileVisibility.Unknown
            ? "Unknown"
            : frame.Owner[tile] < 0
                ? visibility == TileVisibility.Explored ? "Unclaimed when last seen" : "Unclaimed"
                : CivName(frame.Owner[tile], frame.ViewerCiv);
        string claims = visibility == TileVisibility.Visible
            ? $"{ClaimText(frame.Claimant[tile], frame.Claim[tile], frame.ViewerCiv)} · " +
              $"{ClaimText(frame.Claimant2[tile], frame.Claim2[tile], frame.ViewerCiv)} · " +
              ClaimText(frame.Claimant3[tile], frame.Claim3[tile], frame.ViewerCiv)
            : visibility == TileVisibility.Explored ? "last seen only" : "beyond sight";
        string water = visibility != TileVisibility.Unknown
            ? world.HasRiver(tile) ? " · river" : world.HasLake(tile) ? " · lake" : ""
            : "";
        string resource = frame.KnownDeposits[tile] != 0 && world.Deposit[tile] != (byte)DepositKind.None
            ? $"{((DepositKind)world.Deposit[tile])} deposit · size {world.DepositAmount[tile]}"
            : visibility == TileVisibility.Unknown ? "Resources undiscovered" : "No known deposit";
        string workers = visibility == TileVisibility.Visible ? DescribeTileWorkers(frame, tile) : "Current workers: out of sight";
        if (_tileHoverTitle is not null)
            _tileHoverTitle.Text = visibility == TileVisibility.Unknown
                ? "Unexplored tile"
                : $"{terrain} · elevation {world.Elevation[tile]} · {owner}";
        _tileHoverLabel.Text = $"Claims: {claims}{water}\nResources: {resource}\n{workers}";
    }

    private static string DescribeTileWorkers(MatchFrame frame, int tile)
    {
        Span<int> jobs = stackalloc int[13];
        int workers = 0, fallbacks = 0;
        string firstFallback = "";
        for (int i = 0; i < frame.FigureCount; i++)
        {
            if (frame.FigureTile[i] != tile || frame.FigureActivity[i] == (byte)FigureActivity.Idle ||
                frame.FigureActivity[i] > (byte)FigureActivity.Herd) continue;
            int job = frame.FigureActivity[i];
            jobs[job]++;
            workers++;
            MethodFallbackReason reason = (MethodFallbackReason)frame.FigureMethodFallback[i];
            if (reason == MethodFallbackReason.None) continue;
            fallbacks++;
            if (firstFallback.Length != 0) continue;
            string requirement = reason switch
            {
                MethodFallbackReason.MissingTool => $"needs {ToolName((MethodToolId)frame.FigureMethodFallbackTool[i])}",
                MethodFallbackReason.HuntParty => HuntPartyRequirement(frame.FigureKnownToolRung[i]),
                _ => "blocked",
            };
            firstFallback = $"{ActivityName((FigureActivity)job)} R{frame.FigureToolRung[i]} → R{frame.FigureKnownToolRung[i]} · {requirement} · {frame.FigureMethodEfficiencyPermille[i] / 10}%";
        }
        if (workers == 0) return "Workers: none on this tile";
        var rows = new List<string>(3);
        for (int i = 1; i < jobs.Length; i++)
            if (jobs[i] > 0) rows.Add($"{ActivityName((FigureActivity)i)} {jobs[i]}");
        string result = $"Workers: {workers} · {string.Join(", ", rows)}";
        if (fallbacks > 0) result += $"\n[color=#ffbd52]FALLBACK ×{fallbacks} · {firstFallback}[/color]";
        return result;
    }

    private static string ClaimText(int civ, byte strength, int viewer)
        => civ < 0 || strength == 0 ? "—" : $"{CivName(civ, viewer)} {strength}%";

    private static string CivName(int civ, int viewer) => civ == viewer ? "You" : $"Civ {civ + 1}";

    private static string TerrainName(Biome biome) => biome switch
    {
        Biome.GrasslandSteppe => "Grassland / steppe",
        Biome.OpenWoodland => "Open woodland",
        Biome.TemperateForest => "Temperate forest",
        Biome.TropicalForest => "Tropical forest",
        _ => biome.ToString(),
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
        3 => "needs a 6+ person hunting party",
        5 => "needs a 1–2 person hunting party",
        _ => "needs a compatible hunting party",
    };

    private static string RefusalText(ViewEvent item)
    {
        string command = item.A >= (int)CommandKind.None && item.A <= (int)CommandKind.RecallParty
            ? ((CommandKind)item.A).ToString() : "Order";
        string reason = item.B switch
        {
            1 => "invalid target",
            2 => "requirements are not met",
            3 => "not available now",
            _ => "the order was refused",
        };
        return $"{command} refused: {reason}.";
    }

    private static string SeasonName(int season) => (season & 3) switch
    {
        0 => "Spring", 1 => "Summer", 2 => "Autumn", _ => "Winter",
    };

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
