using System;
using System.Collections.Generic;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game.Ui;

/// <summary>
/// Every settlement's name plate on the map, drawn as one 2D overlay (docs/MVP/33). The game feeds it a <see cref="PlateFacts"/> per settlement when a
/// new frame arrives; it projects each one through the camera, picks a level of detail by zoom and relevance, keeps plates from overprinting
/// each other, and answers "which plate is under the pointer". It never touches the simulation.
/// </summary>
public partial class UiSettlementPlates : Control
{
    // Zoom bands (camera size; docs/MVP/33 §6.3): close up to 16, region to 55, continent beyond. Switching has 10% hysteresis.
    public const float RegionFrom = 16.0f, ContinentFrom = 55.0f;
    private const float Hysteresis = 0.10f;
    private const float CullMargin = 160.0f;
    private const float MinRise = 14.0f, MaxRise = 84.0f;

    /// <summary>Height of the pointer tip above the ground by tier, in world units: high enough to clear the tallest building of the tier.</summary>
    private static readonly float[] LiftByTier = { 0.9f, 1.2f, 1.5f, 1.9f };

    private sealed class Plate
    {
        public int Settlement, Tile, Seen;
        public Vector3 World;
        public string Name = "";
        public PlateFacts Facts;
        public string? CountFull, CountCompact;
        /// <summary>Pip, Compact, Full, Names (the "Names only" setting), and Full without its alert chip (slot 4: used when the chip is not worth the room).</summary>
        public readonly PlateMetrics[] Metrics = new PlateMetrics[5];
        public PlateMotion Motion = PlateMotion.Rest;
        /// <summary>The level drawn at the last layout, or -1 when it was not drawn (culled, or settings off): a plate grows only when it gains detail while on screen.</summary>
        public int LastLevel = -1;
        public string? Caption;
        // Layout, rebuilt when the camera or the facts change.
        public bool Shown;
        public Vector2 Anchor, Ground;
        public PlateLevel Level;
        public Vector2 Used;
        public float Alpha = 1.0f;
        public double Score;
        public bool Focus;
    }

    private readonly List<Plate> _plates = new();
    private readonly Dictionary<int, Plate> _bySettlement = new();
    private readonly List<Plate> _order = new();
    private readonly List<Plate> _drawn = new();
    private readonly List<Rect2> _placed = new();
    private int _frame;
    private bool _layoutDirty = true;
    private int _band;
    private float _zoom = 12.0f;
    private int _selected = -1, _hovered = -1;
    private bool _showAll;
    private Transform3D _lastCamera;
    private float _lastZoom = -1.0f;
    private Vector2 _lastViewport;
    private bool _galleryMode;
    private bool _hasUrgent;
    private double _pulseTime, _redrawAcc;
    private Func<int, string?>? _captionFor;
    private bool _primed;
    private PlateMode _mode = PlateMode.Full;

    private const float GrowS = 0.18f, ChipS = 0.26f, SweepS = 0.55f, FlipS = 0.26f, RiseS = 0.26f, FlashS = 0.7f, MeterPermillePerS = 1100.0f;

    public Camera3D? Camera { get; set; }

    /// <summary>Gives the hover line of a settlement (what it is building, whose it is, when it was last seen). Asked only for the selected and hovered plates.</summary>
    public Func<int, string?>? CaptionFor { get => _captionFor; set { _captionFor = value; _layoutDirty = true; } }

    /// <summary>Plates are drawn when true; flipped by the settings (Q8) and by capture scenarios.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The player's setting (docs/MVP/33 Q8): full plates, ribbons only, or none. Holding Alt shows every plate in full whatever it says.</summary>
    public PlateMode Mode
    {
        get => _mode;
        set { if (_mode == value) return; _mode = value; _layoutDirty = true; }
    }

    private bool Active => Enabled && (_mode != PlateMode.Off || _showAll);

    public UiSettlementPlates()
    {
        Name = "SettlementPlates";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        ClipContents = false;
    }

    // ---- feeding -----------------------------------------------------------------------------

    public void BeginFrame() => _frame++;

    /// <summary>Gives the overlay the plate of one settlement. <paramref name="world"/> is the ground point of its hearth tile.</summary>
    public void Set(int settlement, int tile, Vector3 world, string name, in PlateFacts facts)
    {
        bool created = false;
        if (!_bySettlement.TryGetValue(settlement, out Plate? plate))
        {
            plate = new Plate { Settlement = settlement };
            _bySettlement[settlement] = plate;
            _plates.Add(plate);
            _layoutDirty = true;
            created = true;
            if (_primed) plate.Motion.Grow = 0.0f; // a settlement that appears after the first frame grows in
        }
        plate.Seen = _frame;
        if (plate.Tile != tile || plate.World != world || plate.Facts != facts || plate.Name != name || plate.Metrics[2].DomeW == 0.0f)
        {
            if (!created && _primed && plate.Facts != facts) Notice(plate, plate.Facts, facts);
            plate.Tile = tile;
            plate.World = world;
            plate.Facts = facts;
            Refresh(plate, name);
            _layoutDirty = true;
        }
    }

    /// <summary>
    /// Starts the one-off motions a change of facts deserves (docs/MVP/33 §4.5): the alert chip and its pulse, the rim recolouring and emblem flip when the
    /// settlement changes hands, stakes rising for a new defence, and the ring's catch-up or finished-build flash. Nothing else moves.
    /// </summary>
    private static void Notice(Plate plate, in PlateFacts old, in PlateFacts now)
    {
        PlateMotion mo = plate.Motion;
        if (now.Alert != old.Alert && now.Alert != PlateAlert.None)
        {
            mo.Chip = 0.0f;
            mo.Sweep = 0.0f;
        }
        bool newOwner = now.Civ != old.Civ || now.Occupied != old.Occupied;
        if (newOwner || now.Relation != old.Relation)
        {
            mo.Flip = 0.0f;
            mo.OldRim = UiDome.RelationColor(old);
            mo.FlipsEmblem = newOwner && !old.Empty && !now.Empty;
            mo.OldFacts = old;
        }
        if (now.Defence > old.Defence) mo.Rise = 0.0f;
        if (now.Meter == old.Meter && now.Meter == PlateMeter.Build)
        {
            if (old.MeterPermille >= 600 && now.MeterPermille <= old.MeterPermille - 300)
            {
                mo.Flash = 1.0f;          // the job is done: the whole ring lights once, then empties for the next one
                mo.MeterShown = 1000.0f;
            }
            else if (mo.MeterShown < 0.0f) mo.MeterShown = old.MeterPermille;
        }
        else if (now.Meter == old.Meter && now.Meter == PlateMeter.Siege && mo.MeterShown < 0.0f) mo.MeterShown = old.MeterPermille;
        plate.Motion = mo;
    }

    /// <summary>Drops plates of settlements the latest frame no longer lists.</summary>
    public void EndFrame()
    {
        _primed = true;
        for (int i = _plates.Count - 1; i >= 0; i--)
        {
            if (_plates[i].Seen == _frame) continue;
            _bySettlement.Remove(_plates[i].Settlement);
            _plates.RemoveAt(i);
            _layoutDirty = true;
        }
    }

    public void SetName(int settlement, string name)
    {
        if (!_bySettlement.TryGetValue(settlement, out Plate? plate) || plate.Name == name) return;
        Refresh(plate, name);
        _layoutDirty = true;
    }

    private static void Refresh(Plate plate, string name)
    {
        plate.Name = name;
        PlateFacts f = plate.Facts;
        string? full = null, compact = null;
        if (!f.Empty)
        {
            string count = f.Remembered ? "~" + f.Population : f.Population.ToString();
            full = count;
            compact = f.Relation == PlateRelation.Own ? count : null;
        }
        plate.CountFull = full;
        plate.CountCompact = compact;
        plate.Metrics[(int)PlateLevel.Pip] = UiDome.Measure(PlateLevel.Pip, f, "", null);
        plate.Metrics[(int)PlateLevel.Names] = UiDome.Measure(PlateLevel.Names, f, name, null);
        plate.Metrics[(int)PlateLevel.Compact] = UiDome.Measure(PlateLevel.Compact, f, name, compact);
        plate.Metrics[(int)PlateLevel.Full] = UiDome.Measure(PlateLevel.Full, f, name, full);
        plate.Metrics[NoChip] = plate.Metrics[(int)PlateLevel.Full].Chip is null ? plate.Metrics[(int)PlateLevel.Full] : UiDome.Measure(PlateLevel.Full, f, name, full, withChip: false);
    }

    /// <summary>
    /// The metrics a plate is laid out and drawn with. The chip shows at close zoom, on a focused plate, and for the alerts that matter at region zoom;
    /// the quiet ones (hungry winter, fire out, splitting) are left off a region-zoom plate (docs/MVP/33 Q10).
    /// </summary>
    private PlateMetrics MetricsFor(Plate p, PlateLevel level)
    {
        if (level != PlateLevel.Full) return p.Metrics[(int)level];
        bool chip = _band == 0 || p.Focus || !UiDome.QuietAlert(p.Facts.Alert);
        return p.Metrics[chip ? (int)PlateLevel.Full : NoChip];
    }

    private const int NoChip = 4;

    public void SetZoom(float zoom)
    {
        if (Mathf.IsEqualApprox(zoom, _zoom)) return;
        _zoom = zoom;
        _layoutDirty = true;
    }

    /// <summary>The settlement on this tile is the selected (pinned) one, or -1.</summary>
    public void SetSelectedTile(int tile)
    {
        int settlement = -1;
        if (tile >= 0)
            foreach (Plate p in _plates)
                if (p.Tile == tile) { settlement = p.Settlement; break; }
        if (settlement == _selected) return;
        _selected = settlement;
        _layoutDirty = true;
    }

    /// <summary>Tells the overlay where a free pointer is (null when it is over the HUD, dragging, or away).</summary>
    public void SetPointer(Vector2? screen)
    {
        int hovered = screen is Vector2 s && Active ? PickSettlement(s) : -1;
        if (hovered == _hovered) return;
        _hovered = hovered;
        _layoutDirty = true;
    }

    /// <summary>The tile of the plate under <paramref name="screen"/>, if any.</summary>
    public bool TryPickTile(Vector2 screen, out int tile)
    {
        tile = -1;
        if (!Active || !Visible) return false;
        int settlement = PickSettlement(screen);
        if (settlement < 0 || !_bySettlement.TryGetValue(settlement, out Plate? plate)) return false;
        tile = plate.Tile;
        return true;
    }

    /// <summary>Where a settlement's plate is drawn now (its pointer tip), for captures and checks.</summary>
    public bool TryGetAnchor(int settlement, out Vector2 anchor, out PlateLevel level)
    {
        anchor = default; level = PlateLevel.Pip;
        if (!_bySettlement.TryGetValue(settlement, out Plate? plate) || !plate.Shown) return false;
        anchor = plate.Used; level = plate.Level;
        return true;
    }

    private int PickSettlement(Vector2 screen)
    {
        for (int i = _drawn.Count - 1; i >= 0; i--)
        {
            Plate p = _drawn[i];
            if (UiDome.Hits(MetricsFor(p, p.Level), screen - p.Used)) return p.Settlement;
        }
        return -1;
    }

    // ---- per-frame ---------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (_galleryMode || !Visible) return;
        bool alt = Input.IsKeyPressed(Key.Alt);
        if (alt != _showAll) { _showAll = alt; _layoutDirty = true; }
        if (Camera is { } camera && GodotObject.IsInstanceValid(camera))
        {
            Transform3D xform = camera.GlobalTransform;
            Vector2 viewport = GetViewportRect().Size;
            if (!xform.IsEqualApprox(_lastCamera) || viewport != _lastViewport || !Mathf.IsEqualApprox(_lastZoom, _zoom))
            {
                _lastCamera = xform; _lastViewport = viewport; _lastZoom = _zoom;
                _layoutDirty = true;
            }
        }
        bool animating = AdvanceMotion((float)delta);
        if (_layoutDirty)
        {
            Layout();
            QueueRedraw();
            _redrawAcc = 0.0;
        }
        else if (animating)
        {
            QueueRedraw();
            _redrawAcc = 0.0;
        }
        else if (_hasUrgent && !UiSettings.Current.ReduceMotion)
        {
            // A plate with an urgent alert breathes: redraw about 25 times a second, no more.
            _redrawAcc += delta;
            if (_redrawAcc >= 0.04) { _redrawAcc = 0.0; QueueRedraw(); }
        }
        if (_hasUrgent) _pulseTime += delta;
    }

    /// <summary>Runs every plate's one-off motion timers; true while any is still moving. With Reduce motion on, everything rests at once.</summary>
    private bool AdvanceMotion(float delta)
    {
        bool reduce = UiSettings.Current.ReduceMotion;
        bool any = false;
        foreach (Plate p in _plates)
        {
            if (!p.Motion.Active) continue;
            if (reduce) { p.Motion = PlateMotion.Rest; any = true; continue; }
            PlateMotion mo = p.Motion;
            mo.Grow = Math.Min(1.0f, mo.Grow + delta / GrowS);
            mo.Chip = Math.Min(1.0f, mo.Chip + delta / ChipS);
            mo.Sweep = Math.Min(1.0f, mo.Sweep + delta / SweepS);
            mo.Flip = Math.Min(1.0f, mo.Flip + delta / FlipS);
            mo.Rise = Math.Min(1.0f, mo.Rise + delta / RiseS);
            if (mo.Flash > 0.0f)
            {
                mo.Flash = Math.Max(0.0f, mo.Flash - delta / FlashS);
                mo.MeterShown = 1000.0f;
            }
            else if (mo.MeterShown >= 0.0f)
            {
                float target = p.Facts.MeterPermille, step = MeterPermillePerS * delta;
                mo.MeterShown = Math.Abs(mo.MeterShown - target) <= step ? -1.0f : mo.MeterShown + Math.Sign(target - mo.MeterShown) * step;
            }
            p.Motion = mo;
            any = true;
        }
        return any;
    }

    /// <summary>Rim alpha factor of an urgent plate: 0.55 to 1.0 over 1.6 seconds, steady when motion is reduced.</summary>
    private float Pulse => UiSettings.Current.ReduceMotion ? 1.0f : 0.775f + 0.225f * MathF.Cos((float)(_pulseTime * Math.Tau / 1.6));

    private int BandFor(float zoom)
    {
        // Hysteresis: moving up needs 10% past the edge, moving down 10% short of it.
        float toRegion = _band >= 1 ? RegionFrom * (1.0f - Hysteresis) : RegionFrom * (1.0f + Hysteresis);
        float toContinent = _band >= 2 ? ContinentFrom * (1.0f - Hysteresis) : ContinentFrom * (1.0f + Hysteresis);
        return zoom > toContinent ? 2 : zoom > toRegion ? 1 : 0;
    }

    private static bool Collides(List<Rect2> placed, Rect2 box)
    {
        if (box.Size.X <= 0.0f) return false;
        for (int i = 0; i < placed.Count; i++)
            if (placed[i].Intersects(box)) return true;
        return false;
    }

    private static Rect2 At(Rect2 box, Vector2 anchor, float dy = 0.0f)
        => new(box.Position + anchor + new Vector2(0, dy), box.Size);

    private void Layout()
    {
        _layoutDirty = false;
        _order.Clear();
        _drawn.Clear();
        _placed.Clear();
        if (Camera is not { } camera || !GodotObject.IsInstanceValid(camera) || !Active)
        {
            foreach (Plate p in _plates) { p.Shown = false; p.LastLevel = -1; }
            return;
        }
        _band = BandFor(_zoom);
        Vector2 viewport = GetViewportRect().Size;
        // At Hearth zoom the plates rise a little and thin out so the figures stay visible.
        float hearth = Mathf.Clamp((8.0f - _zoom) / 3.0f, 0.0f, 1.0f);
        foreach (Plate p in _plates)
        {
            p.Shown = false;
            p.Focus = _showAll || p.Settlement == _selected || p.Settlement == _hovered;
            if (p.Facts.Empty && _band > 0 && !p.Focus) continue;
            // The pointer tip stands a little above the village: as high as the tier's buildings would reach in world units, but never more
            // than a fixed number of pixels, so a close zoom does not fling the plate off the top of the screen.
            if (camera.IsPositionBehind(p.World)) continue;
            Vector2 ground = camera.UnprojectPosition(p.World);
            if (ground.X < -CullMargin || ground.Y < -CullMargin || ground.X > viewport.X + CullMargin || ground.Y > viewport.Y + CullMargin) continue;
            float lift = LiftByTier[(int)p.Facts.Tier] + (p.Facts.Capital ? 0.2f : 0.0f);
            float natural = ground.Y - camera.UnprojectPosition(p.World + new Vector3(0, lift, 0)).Y;
            float rise = Mathf.Clamp(natural, MinRise, MaxRise + (p.Facts.Capital ? 10.0f : 0.0f));
            p.Ground = ground.Round();
            p.Anchor = new Vector2(ground.X, ground.Y - rise).Round();
            p.Alpha = p.Focus ? 1.0f : Mathf.Lerp(1.0f, 0.72f, hearth);
            p.Score = (double)SettlementPlate.Relevance(p.Facts) * 100000.0 + Math.Min(p.Facts.Population, 9999) * 10.0 - p.Settlement * 0.001;
            if (p.Settlement == _selected) p.Score += 1.0e9;
            else if (p.Settlement == _hovered) p.Score += 9.0e8;
            else if (_showAll) p.Score += 1.0e8;
            p.Shown = true;
            _order.Add(p);
        }
        _order.Sort((x, y) => y.Score.CompareTo(x.Score));

        foreach (Plate p in _order)
        {
            PlateLevel level = Desired(p);
            string? pipName = PipNamed(p) ? p.Name : null;
            while (true)
            {
                PlateMetrics m = level == PlateLevel.Pip ? PipMetrics(p, pipName) : MetricsFor(p, level);
                Vector2 origin = level == PlateLevel.Pip ? p.Ground : p.Anchor;
                Rect2 dome = At(m.DomeBox, origin), ribbon = At(m.RibbonBox, origin);
                bool free = p.Focus || level == PlateLevel.Pip && pipName is null || !Collides(_placed, dome) && !Collides(_placed, ribbon);
                if (!free && level == PlateLevel.Full)
                {
                    // Try once a ribbon's height higher before giving up detail.
                    float dy = -m.RibbonH;
                    Rect2 d2 = At(m.DomeBox, origin, dy), r2 = At(m.RibbonBox, origin, dy);
                    if (!Collides(_placed, d2) && !Collides(_placed, r2))
                    {
                        origin += new Vector2(0, dy);
                        dome = d2; ribbon = r2; free = true;
                    }
                }
                if (free || level == PlateLevel.Pip && pipName is null)
                {
                    p.Level = level;
                    p.Used = origin;
                    if (level == PlateLevel.Pip) p.Metrics[(int)PlateLevel.Pip] = m;
                    _placed.Add(dome);
                    if (ribbon.Size.X > 0.0f) _placed.Add(ribbon);
                    break;
                }
                if (level == PlateLevel.Pip) pipName = null;
                else level = level == PlateLevel.Names ? PlateLevel.Pip : level - 1;
            }
            _drawn.Add(p);
        }
        _drawn.Reverse(); // highest score last, so the plates that matter sit on top
        _hasUrgent = false;
        foreach (Plate p in _plates)
            if (!p.Shown) p.LastLevel = -1;
        foreach (Plate p in _drawn)
        {
            // A plate that gains detail while it stays on screen grows (the dome rises from the ribbon); one that loses detail, or comes in at the edge, just changes.
            if (p.LastLevel >= 0 && Rank(p.Level) > Rank((PlateLevel)p.LastLevel) && p.Motion.Grow >= 1.0f) p.Motion.Grow = 0.0f;
            p.LastLevel = (int)p.Level;
            if (p.Facts.Urgent) _hasUrgent = true;
            p.Caption = _captionFor is not null && p.Level == PlateLevel.Full && (p.Settlement == _hovered || p.Settlement == _selected) ? _captionFor(p.Settlement) : null;
        }
    }

    private static int Rank(PlateLevel level) => level switch { PlateLevel.Pip => 0, PlateLevel.Names => 1, PlateLevel.Compact => 2, _ => 3 };

    private PlateLevel Desired(Plate p)
    {
        if (p.Focus) return PlateLevel.Full;
        if (_mode == PlateMode.NamesOnly) return _band >= 2 ? PlateLevel.Pip : PlateLevel.Names;
        if (_band == 0) return PlateLevel.Full;
        if (_band == 1) return p.Facts.Relation == PlateRelation.Own && p.Facts.Capital ? PlateLevel.Full : PlateLevel.Compact;
        return PlateLevel.Pip;
    }

    private bool PipNamed(Plate p) => _band >= 2 && (p.Facts.Capital || p.Facts.Tier == PlateTier.City) && !p.Facts.Empty;

    private PlateMetrics PipMetrics(Plate p, string? name)
        => UiDome.Measure(PlateLevel.Pip, p.Facts, name ?? "", null);

    // ---- drawing -----------------------------------------------------------------------------

    public override void _Draw()
    {
        if (_galleryMode) { DrawGallery(); return; }
        if (!Active) return;
        foreach (Plate p in _drawn)
        {
            PlateMetrics m = MetricsFor(p, p.Level);
            string name = p.Level == PlateLevel.Pip ? (m.NameW > 0 ? p.Name : "") : p.Name;
            string? count = p.Level == PlateLevel.Full ? p.CountFull : p.Level == PlateLevel.Compact ? p.CountCompact : null;
            UiDome.Draw(this, p.Used, m, p.Facts, name, count, p.Alpha, p.Focus, Pulse, p.Motion);
            if (p.Caption is { Length: > 0 }) UiDome.DrawCaption(this, p.Used, m, p.Caption, GetViewportRect().Size.X);
        }
    }

    // ---- gallery -----------------------------------------------------------------------------

    private readonly List<(PlateFacts Facts, string Name, PlateLevel Level, Vector2 At, bool Focus, bool NamedPip, PlateMotion Motion)> _gallery = new();
    private static readonly (string Name, Color Ground)[] Grounds =
    {
        ("sand", new Color("#CDBB8A")), ("grass", new Color("#6E8F4E")), ("snow", new Color("#E8EEF2")), ("forest", new Color("#2F4A2C")), ("water", new Color("#2C5A78")),
    };

    /// <summary>Lays out every plate variant on plain ground for review (the <c>plate-gallery</c> scenario).</summary>
    public void ShowGallery(int page = 0)
    {
        _galleryMode = true;
        _gallery.Clear();
        PlateFacts F(int civ, int pop, PlateTier tier, PlateRelation rel, bool capital = false, bool remembered = false, bool empty = false,
            bool occupied = false, bool tin = false, PlateDefence defence = PlateDefence.None, PlateMeter meter = PlateMeter.None, int permille = 0,
            PlateAlert alert = PlateAlert.None, int number = 0)
            => new(civ, pop, tier, rel, capital, remembered, empty, occupied, tin, false, 0, defence, meter, permille, alert, number);
        float x0 = 130.0f, dx = 215.0f;
        var rows = page switch
        {
            1 => new List<(string Label, PlateLevel Level, (PlateFacts F, string N)[] Plates)>
            {
                ("defence", PlateLevel.Full, new[]
                {
                    (F(0, 72, PlateTier.Town, PlateRelation.Own), "Open Town"), (F(0, 72, PlateTier.Town, PlateRelation.Own, defence: PlateDefence.Thorn), "Thornhold"),
                    (F(0, 72, PlateTier.Town, PlateRelation.Own, defence: PlateDefence.Palisade), "Stakeford"), (F(0, 72, PlateTier.Town, PlateRelation.Own, defence: PlateDefence.Walls), "Stonewall"),
                    (F(0, 100, PlateTier.City, PlateRelation.Own, capital: true, defence: PlateDefence.Castle), "Highcastle"), (F(1, 34, PlateTier.Village, PlateRelation.AtWar, defence: PlateDefence.Palisade), "Settlement 9"),
                    (F(2, 12, PlateTier.Band, PlateRelation.Allied, defence: PlateDefence.Thorn), "Thornlet"),
                }),
                ("alerts", PlateLevel.Full, new[]
                {
                    (F(0, 72, PlateTier.Town, PlateRelation.Own, defence: PlateDefence.Walls, meter: PlateMeter.Siege, permille: 620, alert: PlateAlert.Besieged, number: 62), "Stonewall"),
                    (F(0, 34, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Raid, number: 2), "Lowfield"), (F(0, 45, PlateTier.Village, PlateRelation.Own, occupied: true, alert: PlateAlert.Occupied), "Ironbridge"),
                    (F(0, 60, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Famine), "Amberford"), (F(0, 38, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Short), "Reedmoor"),
                    (F(0, 50, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Fraying), "Farwatch"), (F(0, 66, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.HungryWinter), "Coldharbor"),
                }),
                ("more alerts", PlateLevel.Full, new[]
                {
                    (F(0, 18, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.FireOut), "Ashfall"), (F(0, 88, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Splitting), "Broadmeadow"),
                    (F(1, 54, PlateTier.Village, PlateRelation.AtWar, alert: PlateAlert.LevyTarget), "Settlement 9"), (F(1, 40, PlateTier.Village, PlateRelation.AtWar, occupied: true, alert: PlateAlert.Occupied), "Settlement 4"),
                    (F(1, 66, PlateTier.Town, PlateRelation.AtWar, defence: PlateDefence.Walls, meter: PlateMeter.Siege, permille: 900, alert: PlateAlert.Besieged, number: 90), "Emberkeep"),
                    (F(0, 120, PlateTier.City, PlateRelation.Own, capital: true, defence: PlateDefence.Castle, meter: PlateMeter.Siege, permille: 250, alert: PlateAlert.Besieged, number: 25), "Sunhold Regnant"),
                    (F(0, 80, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Raid, number: 0), "Lastlight"),
                }),
                ("meters", PlateLevel.Full, new[]
                {
                    (F(0, 40, PlateTier.Village, PlateRelation.Own, meter: PlateMeter.Build, permille: 0), "Idle Town"), (F(0, 62, PlateTier.Town, PlateRelation.Own, meter: PlateMeter.Build, permille: 250), "Quarter"),
                    (F(0, 62, PlateTier.Town, PlateRelation.Own, meter: PlateMeter.Build, permille: 500), "Half"), (F(0, 62, PlateTier.Town, PlateRelation.Own, meter: PlateMeter.Build, permille: 875), "Nearly"),
                    (F(3, 62, PlateTier.Town, PlateRelation.Own, meter: PlateMeter.Build, permille: 500), "Goldcrown"), (F(5, 118, PlateTier.City, PlateRelation.Own, meter: PlateMeter.Build, permille: 625), "Greatharbor"),
                    (F(0, 74, PlateTier.Town, PlateRelation.Own, capital: true, meter: PlateMeter.Build, permille: 375), "Sunhold"),
                }),
            },
            3 => new List<(string Label, PlateLevel Level, (PlateFacts F, string N)[] Plates)>
            {
                ("names only", PlateLevel.Names, new[]
                {
                    (F(0, 12, PlateTier.Band, PlateRelation.Own), "Ashfall"), (F(0, 34, PlateTier.Village, PlateRelation.Own), "Lowfield"),
                    (F(0, 72, PlateTier.Town, PlateRelation.Own, capital: true), "Sunhold"), (F(1, 54, PlateTier.Village, PlateRelation.AtWar), "Goldhold"),
                    (F(2, 61, PlateTier.Town, PlateRelation.Allied), "Greenmere"), (F(3, 40, PlateTier.Village, PlateRelation.Contacted), "Yellowrun"),
                    (F(4, 60, PlateTier.Town, PlateRelation.Unmet, remembered: true), "Farhaven"),
                }),
                ("names only with alerts", PlateLevel.Names, new[]
                {
                    (F(0, 72, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Besieged, number: 62), "Stonewall"), (F(0, 34, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Raid, number: 2), "Lowfield"),
                    (F(0, 60, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Famine), "Amberford"), (F(0, 38, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Short), "Reedmoor"),
                    (F(1, 40, PlateTier.Village, PlateRelation.AtWar, occupied: true, alert: PlateAlert.Occupied), "Ironbridge"), (F(1, 54, PlateTier.Village, PlateRelation.AtWar, alert: PlateAlert.LevyTarget), "Settlement 9"),
                    (F(0, 66, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.HungryWinter), "Coldharbor"),
                }),
                ("wild folk", PlateLevel.Full, new[]
                {
                    (F(-1, 22, PlateTier.Village, PlateRelation.Wild), "Free Village"), (F(-1, 18, PlateTier.Band, PlateRelation.Wild, tin: true), "Tin Folk"),
                    (F(-1, 30, PlateTier.Village, PlateRelation.Wild, tin: true), "Tinwick"), (F(-1, 22, PlateTier.Village, PlateRelation.Wild), "Free Village"),
                }),
                ("wild folk compact and pip", PlateLevel.Compact, new[]
                {
                    (F(-1, 22, PlateTier.Village, PlateRelation.Wild), "Free Village"), (F(-1, 18, PlateTier.Band, PlateRelation.Wild, tin: true), "Tin Folk"),
                }),
            },
            2 => new List<(string Label, PlateLevel Level, (PlateFacts F, string N)[] Plates)>
            {
                ("compact alerts", PlateLevel.Compact, new[]
                {
                    (F(0, 34, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Short), "Lowfield"), (F(0, 74, PlateTier.Town, PlateRelation.Own, capital: true, alert: PlateAlert.Besieged, number: 40, meter: PlateMeter.Siege, permille: 400), "Sunhold"),
                    (F(0, 54, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Raid, number: 1), "Reedmoor"), (F(1, 54, PlateTier.Village, PlateRelation.AtWar, occupied: true, alert: PlateAlert.Occupied), "Settlement 9"),
                    (F(2, 61, PlateTier.Town, PlateRelation.Allied, defence: PlateDefence.Walls), "Greenmere"), (F(0, 34, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.HungryWinter), "Quiet One"),
                    (F(1, 54, PlateTier.Village, PlateRelation.AtWar, alert: PlateAlert.LevyTarget), "Settlement 5"),
                }),
                ("pips", PlateLevel.Pip, new[]
                {
                    (F(0, 34, PlateTier.Village, PlateRelation.Own, alert: PlateAlert.Besieged, number: 50), ""), (F(0, 118, PlateTier.City, PlateRelation.Own, capital: true, alert: PlateAlert.Raid, number: 2), "Sunhold"),
                    (F(1, 54, PlateTier.Village, PlateRelation.AtWar, occupied: true, alert: PlateAlert.Occupied), ""), (F(0, 61, PlateTier.Town, PlateRelation.Own, alert: PlateAlert.Famine), ""),
                    (F(0, 61, PlateTier.Town, PlateRelation.Own), ""), (F(1, 54, PlateTier.Village, PlateRelation.AtWar), ""), (F(2, 40, PlateTier.Village, PlateRelation.Allied), ""),
                }),
            },
            _ => new List<(string Label, PlateLevel Level, (PlateFacts F, string N)[] Plates)>
        {
            ("tiers", PlateLevel.Full, new[]
            {
                (F(0, 12, PlateTier.Band, PlateRelation.Own), "Ashfall"), (F(0, 34, PlateTier.Village, PlateRelation.Own), "Lowfield"),
                (F(0, 72, PlateTier.Town, PlateRelation.Own), "Amberford"), (F(0, 118, PlateTier.City, PlateRelation.Own), "Greatharbor"),
                (F(0, 74, PlateTier.Town, PlateRelation.Own, capital: true), "Sunhold"), (F(0, 131, PlateTier.City, PlateRelation.Own, capital: true), "Sunhold Regnant"),
                (F(0, 12, PlateTier.Band, PlateRelation.Own, capital: true), "Hearth"),
            }),
            ("relations", PlateLevel.Full, new[]
            {
                (F(1, 54, PlateTier.Village, PlateRelation.AtWar), "Settlement 9"), (F(2, 61, PlateTier.Town, PlateRelation.Allied), "Greenmere"),
                (F(3, 40, PlateTier.Village, PlateRelation.Contacted), "Yellowrun"), (F(4, 33, PlateTier.Village, PlateRelation.Unmet), "Settlement 12"),
                (F(5, 66, PlateTier.Town, PlateRelation.AtWar, capital: true), "Emberkeep"), (F(-1, 22, PlateTier.Village, PlateRelation.Wild), "Settlement 3"),
                (F(-1, 18, PlateTier.Band, PlateRelation.Wild, tin: true), "Tin Folk"),
            }),
            ("states", PlateLevel.Full, new[]
            {
                (F(1, 60, PlateTier.Town, PlateRelation.Contacted, remembered: true), "Settlement 4"), (F(2, 28, PlateTier.Village, PlateRelation.Allied, remembered: true), "Farhaven"),
                (F(1, 0, PlateTier.Band, PlateRelation.Unmet, empty: true), "Settlement 7"), (F(0, 45, PlateTier.Village, PlateRelation.Own, occupied: true), "Ironbridge"),
                (F(6, 88, PlateTier.Town, PlateRelation.Contacted), "A Very Long Settlement Nam"), (F(7, 9, PlateTier.Band, PlateRelation.Own), "Ox"),
                (F(3, 100, PlateTier.City, PlateRelation.Contacted, capital: true), "Goldcrown"),
            }),
            ("compact", PlateLevel.Compact, new[]
            {
                (F(0, 34, PlateTier.Village, PlateRelation.Own), "Lowfield"), (F(0, 74, PlateTier.Town, PlateRelation.Own, capital: true), "Sunhold"),
                (F(1, 54, PlateTier.Village, PlateRelation.AtWar), "Settlement 9"), (F(2, 61, PlateTier.Town, PlateRelation.Allied), "Greenmere"),
                (F(3, 66, PlateTier.Town, PlateRelation.Contacted, capital: true), "Goldcrown"), (F(-1, 22, PlateTier.Village, PlateRelation.Wild), "Settlement 3"),
                (F(4, 60, PlateTier.Town, PlateRelation.Unmet, remembered: true), "Settlement 4"),
            }),
            ("pips", PlateLevel.Pip, new[]
            {
                (F(0, 34, PlateTier.Village, PlateRelation.Own), ""), (F(0, 118, PlateTier.City, PlateRelation.Own, capital: true), "Sunhold"),
                (F(1, 54, PlateTier.Village, PlateRelation.AtWar), ""), (F(2, 61, PlateTier.Town, PlateRelation.Allied), ""),
                (F(5, 130, PlateTier.City, PlateRelation.Contacted), "Emberkeep"), (F(-1, 22, PlateTier.Village, PlateRelation.Wild), ""),
                (F(4, 60, PlateTier.Town, PlateRelation.Unmet, remembered: true), ""),
            }),
        },
        };
        if (page == 4) { ShowMotionGallery(); return; }
        float y = 60.0f;
        foreach (var row in rows)
        {
            for (int i = 0; i < row.Plates.Length; i++)
            {
                (PlateFacts facts, string name) = row.Plates[i];
                var at = new Vector2(x0 + i * dx, row.Level == PlateLevel.Pip ? y + 40.0f : y + 100.0f);
                _gallery.Add((facts, name, row.Level, at, i == 2 && row.Level == PlateLevel.Full && row.Label == "relations", row.Level == PlateLevel.Pip && name.Length > 0, PlateMotion.Rest));
            }
            y += row.Level == PlateLevel.Pip ? 100.0f : 160.0f;
        }
        QueueRedraw();
    }

    /// <summary>Frozen frames of each motion, half way through, beside the finished state (the <c>plate-gallery</c> scenario, page 4).</summary>
    private void ShowMotionGallery()
    {
        PlateFacts Own(PlateTier tier, int pop, PlateAlert alert = PlateAlert.None, int number = 0, PlateDefence defence = PlateDefence.None, PlateMeter meter = PlateMeter.None, int permille = 0, int civ = 0, PlateRelation rel = PlateRelation.Own, bool capital = false)
            => new(civ, pop, tier, rel, capital, false, false, false, false, false, 0, defence, meter, permille, alert, number);
        PlateMotion Rest = PlateMotion.Rest;
        var items = new List<(PlateFacts F, string Name, PlateMotion M)>();
        PlateMotion grow = Rest; grow.Grow = 0.45f;
        items.Add((Own(PlateTier.Town, 72), "Growing 45%", grow));
        PlateMotion grow2 = Rest; grow2.Grow = 0.8f;
        items.Add((Own(PlateTier.Town, 72), "Growing 80%", grow2));
        PlateMotion chip = Rest; chip.Chip = 0.4f; chip.Sweep = 0.45f;
        items.Add((Own(PlateTier.Town, 60, PlateAlert.Famine), "Chip + pulse", chip));
        PlateMotion sweep = Rest; sweep.Sweep = 0.7f;
        items.Add((Own(PlateTier.Town, 72, PlateAlert.Besieged, 62, PlateDefence.Walls, PlateMeter.Siege, 620), "Pulse climbing", sweep));
        PlateMotion flip = Rest; flip.Flip = 0.3f; flip.FlipsEmblem = true; flip.OldRim = UiDome.RelationColor(Own(PlateTier.Village, 40, civ: 1, rel: PlateRelation.AtWar)); flip.OldFacts = Own(PlateTier.Village, 40, civ: 1, rel: PlateRelation.AtWar);
        items.Add((Own(PlateTier.Village, 40), "Flip 30%", flip));
        PlateMotion flip2 = flip; flip2.Flip = 0.7f;
        items.Add((Own(PlateTier.Village, 40), "Flip 70%", flip2));
        PlateMotion rise = Rest; rise.Rise = 0.5f;
        items.Add((Own(PlateTier.Town, 72, defence: PlateDefence.Palisade), "Stakes rising", rise));
        PlateMotion flash = Rest; flash.Flash = 0.8f; flash.MeterShown = 1000;
        items.Add((Own(PlateTier.Town, 62, meter: PlateMeter.Build, permille: 80), "Build flash", flash));
        PlateMotion fill = Rest; fill.MeterShown = 400;
        items.Add((Own(PlateTier.Town, 62, meter: PlateMeter.Build, permille: 750), "Ring filling", fill));
        PlateMotion namesGrow = Rest; namesGrow.Grow = 0.5f;
        items.Add((Own(PlateTier.Village, 34), "Names grow", namesGrow));
        for (int i = 0; i < items.Count; i++)
        {
            int col = i % 5, row = i / 5;
            PlateLevel level = items[i].Name == "Names grow" ? PlateLevel.Names : PlateLevel.Full;
            _gallery.Add((items[i].F, items[i].Name, level, new Vector2(150.0f + col * 245.0f, 170.0f + row * 230.0f), false, false, items[i].M));
        }
        QueueRedraw();
    }

    private void DrawGallery()
    {
        Vector2 size = GetViewportRect().Size;
        float strip = size.X / Grounds.Length;
        for (int i = 0; i < Grounds.Length; i++) DrawRect(new Rect2(i * strip, 0, strip + 1, size.Y), Grounds[i].Ground);
        foreach (var item in _gallery)
        {
            PlateMetrics m = UiDome.Measure(item.Level, item.Facts, item.NamedPip || item.Level != PlateLevel.Pip ? item.Name : "",
                item.Level == PlateLevel.Full ? (item.Facts.Empty ? null : (item.Facts.Remembered ? "~" : "") + item.Facts.Population)
                : item.Level == PlateLevel.Compact && item.Facts.Relation == PlateRelation.Own ? item.Facts.Population.ToString() : null);
            string? count = m.HasCount ? (item.Facts.Remembered ? "~" : "") + item.Facts.Population : null;
            UiDome.Draw(this, item.At, m, item.Facts, item.Level == PlateLevel.Pip && !item.NamedPip ? "" : item.Name, count, 1.0f, item.Focus, 1.0f, item.Motion);
        }
    }
}
