using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

/// <summary>
/// The event stream (docs/MVP/18 §6): research and politics events read from the systems' own lists (never cleared
/// here), plus events the builder derives by comparing economy state between frames (seasons, births, deaths, new
/// settlements, structures, fires, famine, parties coming), filtered for the viewer and logged in a bounded ring.
/// Each frame carries the newest <see cref="DefaultEventWindow"/> of them.
/// </summary>
public sealed partial class FrameBuilder
{
    private readonly ViewEvent[] _ring;
    private readonly int _window;
    private int _ringStart, _ringCount;
    private long _lastSequence;
    private int _researchCursor, _politicsCursor;

    // Baselines for derived events (saved by WriteState).
    private bool _eventsPrimed;
    private int _lastSeason = -1;
    private long _maxFigureId = -1;
    private int _prevSettlements;
    private byte[] _prevStructures = Array.Empty<byte>();
    private bool[] _prevFire = Array.Empty<bool>();
    private byte[] _prevFood = Array.Empty<byte>(), _prevForecast = Array.Empty<byte>();
    private const int MaxAnnounced = 32;
    private readonly int[] _announcedKey = new int[MaxAnnounced];
    private readonly int[] _announcedArrive = new int[MaxAnnounced];
    private int _announcedCount;

    // Scratch.
    private int[] _birthsByCiv = Array.Empty<int>();
    private int[] _deathsByCivCause = Array.Empty<int>();
    private ViewEvent[] _fromResearch = new ViewEvent[64], _fromPolitics = new ViewEvent[64];
    private int _researchNew, _politicsNew;
    private readonly bool[] _announcedSeen = new bool[MaxAnnounced];

    /// <summary>Events logged so far and still in the ring, oldest first.</summary>
    public int EventCount => _ringCount;

    /// <summary>The <paramref name="index"/>-th event in the ring, oldest first.</summary>
    public ViewEvent Event(int index)
    {
        if ((index < 0 || index >= _ringCount)) throw new ArgumentOutOfRangeException(nameof(index));
        return _ring[(_ringStart + index) % _ring.Length];
    }

    /// <summary>The sequence number of the newest event logged (0 before any).</summary>
    public long LatestEventSequence => _lastSequence;

    private void InitEventScratch()
    {
        _birthsByCiv = new int[_civs];
        _deathsByCivCause = new int[_civs * 8];
        _prevFire = new bool[128]; _prevFood = new byte[128]; _prevForecast = new byte[128];
        _prevStructures = new byte[128 * MatchFrame.StructureKinds];
        _fromResearch = new ViewEvent[256];
        _fromPolitics = new ViewEvent[128];
    }

    private void ResetEvents()
    {
        _ringStart = 0;
        _ringCount = 0;
        _researchCursor = 0;
        _politicsCursor = 0;
        _eventsPrimed = false;
        _announcedCount = 0;
    }

    private void CollectEvents(MatchModel match, MatchFrame target, int season)
    {
        ConvertResearch(match);
        ConvertPolitics(match);
        // Merge the two sources by tick; on a tie research comes first (it steps first within a tick).
        int r = 0, p = 0;
        while (r < _researchNew || p < _politicsNew)
        {
            bool takeResearch = p >= _politicsNew || r < _researchNew && _fromResearch[r].Tick <= _fromPolitics[p].Tick;
            Append(takeResearch ? _fromResearch[r++] : _fromPolitics[p++]);
        }
        DeriveEconomyEvents(match, target, season);
        _eventsPrimed = true;
        _lastSeason = season;

        int window = Math.Min(_window, _ringCount);
        MatchFrame.Ensure(ref target.Events, window);
        for (int i = 0; i < window; i++) target.Events[i] = _ring[(_ringStart + _ringCount - window + i) % _ring.Length];
        target.EventCount = window;
        target.LatestEventSequence = _lastSequence;
    }

    private void Append(ViewEvent e)
    {
        e.Sequence = ++_lastSequence;
        if (_ringCount < _ring.Length) _ring[(_ringStart + _ringCount++) % _ring.Length] = e;
        else
        {
            _ring[_ringStart] = e;
            _ringStart = (_ringStart + 1) % _ring.Length;
        }
    }

    private int TileOf(int settlement) => (settlement >= 0 && settlement < _settlementCount) ? _sTile[settlement] : -1;

    private static ViewEvent New(long tick, ViewEventKind kind, ChronicleKey text, ViewEventTier tier, int civ, int other)
        => new()
        {
            Tick = tick, Kind = kind, Text = text, Tier = tier, Civ = (sbyte)civ, Other = (sbyte)other,
            Figure = -1, Project = -1, Settlement = -1, Tile = -1,
        };

    private void AddResearch(in ViewEvent e)
    {
        MatchFrame.Ensure(ref _fromResearch, _researchNew + 1);
        _fromResearch[_researchNew++] = e;
    }

    private void AddPolitics(in ViewEvent e)
    {
        MatchFrame.Ensure(ref _fromPolitics, _politicsNew + 1);
        _fromPolitics[_politicsNew++] = e;
    }

    // ---- research ------------------------------------------------------------------------------

    private void ConvertResearch(MatchModel match)
    {
        _researchNew = 0;
        IReadOnlyList<ResearchEvent> events = match.Research.Events;
        if (events.Count < _researchCursor) _researchCursor = 0; // the list was cleared (a snapshot was loaded)
        long lastFigure = -1;
        int lastSettlement = -1, lastProject = -1;
        for (; _researchCursor < events.Count; _researchCursor++)
        {
            ResearchEvent r = events[_researchCursor];
            bool mine = _omniscient || r.Civ == _viewer;
            ViewEvent e;
            switch (r.Kind)
            {
                case ResearchEventKind.Glimpsed:
                case ResearchEventKind.ContactMade: // politics reports contact, with both stances
                    continue;
                case ResearchEventKind.FirstClaimed:
                    e = New(r.Tick, ViewEventKind.FirstClaimed, ChronicleKey.FirstClaimed, ViewEventTier.Milestone, r.Civ, -1);
                    e.A = r.A;
                    break;
                case ResearchEventKind.EraAdvanced:
                    e = New(r.Tick, ViewEventKind.EraAdvanced, ChronicleKey.EraAdvanced, ViewEventTier.Milestone, r.Civ, -1);
                    e.A = r.A; e.B = r.B;
                    break;
                default:
                    if (!mine) continue;
                    e = ConvertOwnResearch(match, r, ref lastFigure, ref lastSettlement, ref lastProject);
                    break;
            }
            e.Project = r.Project;
            AddResearch(e);
        }
    }

    private ViewEvent ConvertOwnResearch(MatchModel match, in ResearchEvent r, ref long lastFigure, ref int lastSettlement, ref int lastProject)
    {
        ViewEvent e;
        switch (r.Kind)
        {
            case ResearchEventKind.Unlocked:
                return New(r.Tick, ViewEventKind.ProjectUnlocked, ChronicleKey.ProjectUnlocked, ViewEventTier.Ambient, r.Civ, -1);
            case ResearchEventKind.SlotFilled:
                e = New(r.Tick, ViewEventKind.SlotFilled,
                    r.B == 2 ? ChronicleKey.SlotFilledByCouncil : r.B == 1 ? ChronicleKey.SlotFilledFromQueue : ChronicleKey.SlotFilledByPlayer,
                    r.B == 0 ? ViewEventTier.Ambient : ViewEventTier.Info, r.Civ, -1);
                e.A = r.A; e.B = r.B;
                return e;
            case ResearchEventKind.CouncilPick:
                e = New(r.Tick, ViewEventKind.CouncilPick, ChronicleKey.CouncilPick, ViewEventTier.Ambient, r.Civ, -1);
                e.A = r.A; e.B = r.B;
                return e;
            case ResearchEventKind.Completed:
            {
                e = New(r.Tick, ViewEventKind.ProjectCompleted, ChronicleKey.ProjectCompleted, ViewEventTier.Info, r.Civ, -1);
                long innovator = r.A;
                e.Figure = innovator;
                if (match.Economy.TryFigure(innovator, out FigureView figure))
                {
                    e.Settlement = figure.SettlementId;
                    e.Tile = TileOf(figure.SettlementId);
                }
                else
                {
                    // The innovator died the same tick: point at the civilization's first settlement instead.
                    for (int s = 0; s < _settlementCount; s++)
                        if (_sCiv[s] == r.Civ && !_sFree[s] && _sPop[s] > 0) { e.Settlement = s; e.Tile = _sTile[s]; break; }
                }
                ProjectDef def = _catalog.Projects[r.Project];
                e.A = (int)VisibleChange.None;
                for (int i = 0; i < def.Effects.Length; i++)
                    if (def.Effects[i].Visual != VisibleChange.None) { e.A = (int)def.Effects[i].Visual; break; }
                lastFigure = e.Figure; lastSettlement = e.Settlement; lastProject = r.Project;
                return e;
            }
            case ResearchEventKind.Moment:
                e = New(r.Tick, ViewEventKind.Moment, r.A == 1 ? ChronicleKey.MomentPlays : ChronicleKey.MomentBackfill,
                    r.A == 1 ? ViewEventTier.Moment : ViewEventTier.Ambient, r.Civ, -1);
                e.A = r.A;
                if (lastProject == r.Project) { e.Figure = lastFigure; e.Settlement = lastSettlement; e.Tile = TileOf(lastSettlement); }
                return e;
            case ResearchEventKind.VisibleChange:
                e = New(r.Tick, ViewEventKind.VisibleChange, ChronicleKey.VisibleChange, ViewEventTier.Ambient, r.Civ, -1);
                e.A = r.A; e.B = r.B;
                return e;
            case ResearchEventKind.Adopted:
                e = New(r.Tick, ViewEventKind.Adopted, ChronicleKey.Adopted, ViewEventTier.Ambient, r.Civ, -1);
                e.A = r.A; e.B = r.B;
                return e;
            case ResearchEventKind.SeenResource:
                e = New(r.Tick, ViewEventKind.ResourceSeen, ChronicleKey.ResourceSeen, ViewEventTier.Info, r.Civ, -1);
                e.A = r.A;
                return e;
            case ResearchEventKind.DormantChanged:
                e = New(r.Tick, ViewEventKind.DormantChanged, r.A == 1 ? ChronicleKey.ProjectDormant : ChronicleKey.ProjectRevived,
                    ViewEventTier.Info, r.Civ, -1);
                e.A = r.A;
                return e;
            default:
                return New(r.Tick, ViewEventKind.None, ChronicleKey.None, ViewEventTier.Ambient, r.Civ, -1);
        }
    }

    // ---- politics ------------------------------------------------------------------------------

    /// <summary>Kinds recorded only for the acting side: the other side still sees them happen to it.</summary>
    private static bool OneSided(PoliticsEventKind kind)
        => kind is PoliticsEventKind.Gift or PoliticsEventKind.Barter or PoliticsEventKind.Occupied or PoliticsEventKind.OccupationBroken;

    private void ConvertPolitics(MatchModel match)
    {
        _politicsNew = 0;
        IReadOnlyList<PoliticsEvent> events = match.Politics.Events;
        if (events.Count < _politicsCursor) _politicsCursor = 0;
        for (; _politicsCursor < events.Count; _politicsCursor++)
        {
            PoliticsEvent p = events[_politicsCursor];
            if (!_omniscient && p.Civ != _viewer && !(OneSided(p.Kind) && p.Other == _viewer)) continue;
            bool defender = !_omniscient && p.Civ != _viewer;
            ViewEvent e = New(p.Tick, ViewEventKind.None, ChronicleKey.None, ViewEventTier.Info, p.Civ, p.Other);
            e.A = p.A;
            e.B = p.B;
            switch (p.Kind)
            {
                case PoliticsEventKind.Aware:
                    e.Kind = ViewEventKind.Aware; e.Text = ChronicleKey.Aware;
                    break;
                case PoliticsEventKind.Contact:
                    e.Kind = ViewEventKind.Contact; e.Tier = ViewEventTier.Milestone;
                    e.Text = (Stance)p.A switch { Stance.Friendly => ChronicleKey.ContactFriendly, Stance.Wary => ChronicleKey.ContactWary, _ => ChronicleKey.ContactHostile };
                    break;
                case PoliticsEventKind.Skirmish:
                    e.Kind = ViewEventKind.Skirmish; e.Text = ChronicleKey.Skirmish; e.Tier = ViewEventTier.Milestone;
                    break;
                case PoliticsEventKind.Gift:
                    e.Kind = ViewEventKind.GiftArrived; e.Text = ChronicleKey.GiftSent;
                    break;
                case PoliticsEventKind.Barter:
                    e.Kind = ViewEventKind.BarterDone; e.Text = ChronicleKey.BarterDone;
                    break;
                case PoliticsEventKind.RaidLaunched:
                    e.Kind = ViewEventKind.RaidLaunched; e.Text = ChronicleKey.RaidLaunched;
                    e.Settlement = p.B; e.Tile = TileOf(p.B);
                    break;
                case PoliticsEventKind.RaidResolved:
                    e.Kind = ViewEventKind.RaidResolved;
                    (e.Text, e.Tier) = p.A switch
                    {
                        1 => (ChronicleKey.RaidSucceeded, ViewEventTier.Milestone),
                        0 => (ChronicleKey.RaidFailed, ViewEventTier.Milestone),
                        2 => (ChronicleKey.RaidSuffered, ViewEventTier.Critical),
                        _ => (ChronicleKey.RaidRepelled, ViewEventTier.Milestone),
                    };
                    break;
                case PoliticsEventKind.WarDeclared:
                    e.Kind = ViewEventKind.WarDeclared;
                    (e.Text, e.Tier) = p.A == 1 ? (ChronicleKey.WarDeclaredOnUs, ViewEventTier.Critical) : (ChronicleKey.WarDeclaredByUs, ViewEventTier.Milestone);
                    break;
                case PoliticsEventKind.PeaceMade:
                    e.Kind = ViewEventKind.PeaceMade; e.Text = ChronicleKey.PeaceMade; e.Tier = ViewEventTier.Milestone;
                    break;
                case PoliticsEventKind.AssaultResolved:
                    e.Kind = ViewEventKind.AssaultResolved; e.Settlement = p.B; e.Tile = TileOf(p.B);
                    (e.Text, e.Tier) = p.A switch
                    {
                        1 => (ChronicleKey.AssaultWon, ViewEventTier.Milestone),
                        0 => (ChronicleKey.AssaultLost, ViewEventTier.Milestone),
                        2 => (ChronicleKey.SettlementFell, ViewEventTier.Critical),
                        _ => (ChronicleKey.SettlementHeld, ViewEventTier.Critical),
                    };
                    break;
                case PoliticsEventKind.Occupied:
                    e.Kind = ViewEventKind.Occupied; e.Text = ChronicleKey.Occupied; e.Settlement = p.B; e.Tile = TileOf(p.B);
                    e.Tier = defender ? ViewEventTier.Critical : ViewEventTier.Milestone;
                    break;
                case PoliticsEventKind.OccupationBroken:
                    e.Kind = ViewEventKind.OccupationBroken; e.Settlement = p.A; e.Tile = TileOf(p.A);
                    e.Text = p.B == 1 ? ChronicleKey.OccupationAbandoned : ChronicleKey.OccupationBroken;
                    e.Tier = ViewEventTier.Milestone;
                    break;
                case PoliticsEventKind.Captured:
                    e.Kind = ViewEventKind.Captured; e.Settlement = p.A; e.Tile = TileOf(p.A);
                    (e.Text, e.Tier) = p.B >= 0 ? (ChronicleKey.SettlementCaptured, ViewEventTier.Milestone) : (ChronicleKey.SettlementLost, ViewEventTier.Critical);
                    e.A = p.B; e.B = 0;
                    break;
                case PoliticsEventKind.FreeFolkFounded:
                    e.Kind = ViewEventKind.FreeFolkFounded; e.Text = ChronicleKey.FreeFolkFounded; e.Settlement = p.A; e.Tile = TileOf(p.A);
                    e.A = p.B; e.B = 0;
                    break;
                case PoliticsEventKind.AllianceFormed:
                    e.Kind = ViewEventKind.AllianceFormed; e.Text = ChronicleKey.AllianceFormed; e.Tier = ViewEventTier.Milestone;
                    break;
                case PoliticsEventKind.BeaconPlaced:
                    e.Kind = ViewEventKind.BeaconPlaced; e.Text = ChronicleKey.BeaconPlaced;
                    e.Tile = HexLayout.TileAt(p.A, p.B, _width, _height);
                    break;
                case PoliticsEventKind.VoyageLost:
                    e.Kind = ViewEventKind.VoyageLost; e.Text = ChronicleKey.VoyageLost;
                    break;
                case PoliticsEventKind.OrderRefused:
                    e.Kind = ViewEventKind.OrderRefused; e.Text = ChronicleKey.OrderRefused;
                    e.Tier = ViewEventTier.Info; e.A = p.A; e.B = p.B;
                    break;
                default:
                    continue;
            }
            AddPolitics(e);
        }
    }

    // ---- derived from the economy --------------------------------------------------------------

    private bool Reports(int civ, bool free) => _omniscient || civ == _viewer && !free;

    private void DeriveEconomyEvents(MatchModel match, MatchFrame target, int season)
    {
        LivingEconomyModel eco = match.Economy;
        long tick = Math.Max(0, eco.Tick - 1);
        int n = _settlementCount, kinds = MatchFrame.StructureKinds;
        if (_prevFire.Length < n)
        {
            int size = Math.Max(n, Math.Max(16, _prevFire.Length * 2));
            Array.Resize(ref _prevFire, size); Array.Resize(ref _prevFood, size); Array.Resize(ref _prevForecast, size);
            Array.Resize(ref _prevStructures, size * kinds);
        }
        bool primed = _eventsPrimed;
        if (primed && season != _lastSeason)
        {
            int self = _omniscient ? -1 : _viewer;
            ViewEvent e = New(tick, ViewEventKind.SeasonBegan, SeasonKey(season, eco.LastWinterWasHarsh), ViewEventTier.Ambient, self, -1);
            e.A = season % 4; e.B = season / 4; e.C = eco.LastWinterWasHarsh ? 1 : 0;
            Append(e);
            Array.Clear(_deathsByCivCause);
            for (int i = 0; i < eco.LastDeathEventCount; i++)
            {
                EconomyDeathEvent d = eco.LastDeathEvent(i);
                if ((d.Civilization >= 0 && d.Civilization < _civs)) _deathsByCivCause[d.Civilization * 8 + (int)d.Cause]++;
            }
            for (int c = 0; c < _civs; c++)
            {
                if (!_omniscient && c != _viewer) continue;
                for (int cause = 0; cause < 8; cause++)
                {
                    int count = _deathsByCivCause[c * 8 + cause];
                    if (count == 0) continue;
                    ViewEvent death = New(tick, ViewEventKind.Deaths, ChronicleKey.Deaths, ViewEventTier.Ambient, c, -1);
                    death.A = cause; death.B = count;
                    Append(death);
                }
            }
        }
        if (primed)
            for (int c = 0; c < _civs; c++)
            {
                if (_birthsByCiv[c] == 0 || !_omniscient && c != _viewer) continue;
                ViewEvent birth = New(tick, ViewEventKind.Births, ChronicleKey.Births, ViewEventTier.Ambient, c, -1);
                birth.A = _birthsByCiv[c];
                Append(birth);
            }

        for (int s = 0; s < n; s++)
        {
            var lite = eco.SettlementLite(s);
            bool fire = eco.SettlementFireLit(s);
            byte food = (byte)FoodBandOf(lite.LastFoodSecurity), forecast = (byte)FoodBandOf(lite.LateWinterFoodForecast);
            bool fresh = s >= _prevSettlements;
            bool reports = primed && Reports(_sCiv[s], _sFree[s]);
            if (fresh)
            {
                if (primed && (_omniscient || _sCiv[s] == _viewer))
                {
                    bool island = !_sFree[s] && match.Politics.IsIsland(_sTile[s]);
                    ViewEvent founded = New(tick, ViewEventKind.SettlementFounded,
                        _sFree[s] ? ChronicleKey.FreeFolkCampFounded : island ? ChronicleKey.IslandSettled : ChronicleKey.SettlementFounded, ViewEventTier.Info, _sCiv[s], -1);
                    founded.Settlement = s; founded.Tile = _sTile[s]; founded.A = _sFree[s] ? 1 : 0; founded.C = island ? 1 : 0;
                    Append(founded);
                }
            }
            else if (reports)
            {
                for (int k = 0; k < kinds; k++)
                {
                    int now = Math.Min(255, eco.StructureCount(s, k)), before = _prevStructures[s * kinds + k];
                    if (now == before) continue;
                    bool built = now > before;
                    ViewEvent change = New(tick, built ? ViewEventKind.StructureBuilt : ViewEventKind.StructureLost,
                        built ? ChronicleKey.StructureBuilt : ChronicleKey.StructureBurned,
                        built && now > 1 ? ViewEventTier.Ambient : ViewEventTier.Info, _sCiv[s], -1);
                    change.Settlement = s; change.Tile = _sTile[s]; change.A = k; change.B = now;
                    Append(change);
                }
                if (_prevFire[s] && !fire && _sPop[s] > 0)
                {
                    ViewEvent fireOut = New(tick, ViewEventKind.FireOut, ChronicleKey.FireOut, ViewEventTier.Info, _sCiv[s], -1);
                    fireOut.Settlement = s; fireOut.Tile = _sTile[s];
                    Append(fireOut);
                }
                if (_sPop[s] > 0 && food == (byte)FoodBand.Famine && _prevFood[s] != (byte)FoodBand.Famine)
                    Append(Famine(tick, s, food, 0));
                if (_sPop[s] > 0 && forecast == (byte)FoodBand.Famine && _prevForecast[s] != (byte)FoodBand.Famine)
                    Append(Famine(tick, s, forecast, 1));
            }
            for (int k = 0; k < kinds; k++) _prevStructures[s * kinds + k] = (byte)Math.Min(255, eco.StructureCount(s, k));
            _prevFire[s] = fire;
            _prevFood[s] = food;
            _prevForecast[s] = forecast;
        }
        _prevSettlements = n;
        AnnounceIncoming(tick, primed);
    }

    private ViewEvent Famine(long tick, int settlement, byte band, int forecast)
    {
        ViewEvent e = New(tick, ViewEventKind.FamineRisk, forecast == 1 ? ChronicleKey.FamineForecast : ChronicleKey.FamineNow,
            ViewEventTier.Critical, _sCiv[settlement], -1);
        e.Settlement = settlement; e.Tile = _sTile[settlement]; e.A = band; e.B = forecast;
        return e;
    }

    private static ChronicleKey SeasonKey(int season, bool harsh) => (season % 4) switch
    {
        0 => harsh ? ChronicleKey.SeasonHarshWinter : ChronicleKey.SeasonSpring,
        1 => ChronicleKey.SeasonSummer,
        2 => ChronicleKey.SeasonAutumn,
        _ => ChronicleKey.SeasonWinter,
    };

    /// <summary>
    /// A raid or assault heading for one of the viewer's settlements is reported once, when it comes into sight (its
    /// position on a visible tile; the belief builder's "seen within r + s of the target" in spirit).
    /// </summary>
    private void AnnounceIncoming(long tick, bool primed)
    {
        Array.Clear(_announcedSeen);
        for (int p = 0; p < _partyCount; p++)
        {
            var kind = (ExpeditionKind)_pKind[p];
            if (kind is not (ExpeditionKind.Raid or ExpeditionKind.Assault)) continue;
            int to = _pTo[p];
            if (!(_omniscient ? _pTarget[p] >= 0 : IsOwn(to)) || _pCiv[p] == _viewer) continue;
            int key = (_pCiv[p] * 4 + (int)kind) * 65536 + to;
            int found = -1;
            for (int i = 0; i < _announcedCount; i++)
                if (_announcedKey[i] == key && _announcedArrive[i] == _pArrive[p]) { found = i; break; }
            if (found >= 0) { _announcedSeen[found] = true; continue; }
            if (!Visible(_pTile[p]) || _announcedCount >= MaxAnnounced) continue;
            _announcedKey[_announcedCount] = key;
            _announcedArrive[_announcedCount] = _pArrive[p];
            _announcedSeen[_announcedCount++] = true;
            if (!primed) continue;
            ViewEvent e = New(tick, ViewEventKind.PartyIncoming,
                kind == ExpeditionKind.Raid ? ChronicleKey.RaidIncoming : ChronicleKey.AssaultIncoming,
                ViewEventTier.Critical, _pTarget[p], _pCiv[p]);
            e.Settlement = to; e.Tile = _sTile[to];
            e.A = (int)kind; e.B = _pMembers[p]; e.C = _pArrive[p];
            Append(e);
        }
        // Forget parties that arrived or turned back.
        int w = 0;
        for (int i = 0; i < _announcedCount; i++)
        {
            if (!_announcedSeen[i]) continue;
            _announcedKey[w] = _announcedKey[i];
            _announcedArrive[w++] = _announcedArrive[i];
        }
        _announcedCount = w;
    }
}
