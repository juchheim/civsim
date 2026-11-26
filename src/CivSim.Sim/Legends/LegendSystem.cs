using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;

namespace CivSim.Sim.Legends;

/// <summary>What a person did that counts toward becoming a Legend (docs/MVP/40 §3). Append only; never renumber.</summary>
public enum LegendDeed : byte
{
    /// <summary>Completed a project first practised by this figure (the research system's innovator).</summary>
    Innovator = 0,
    /// <summary>Innovator of a project no other civilization had finished: a First.</summary>
    First = 1,
    /// <summary>Led the people who founded a daughter settlement.</summary>
    Founder = 2,
    /// <summary>Lived past sixty.</summary>
    LongLife = 3,
    /// <summary>Survived a battle the figure's side won (the strongest survivor scores more).</summary>
    Battle = 4,
    /// <summary>Was the settlement's leading elder when it came through a famine.</summary>
    Famine = 5,
    /// <summary>The most skilled adult of the settlement that raised a Wonder.</summary>
    Wonder = 6,
}

/// <summary>A person who became a Legend. <see cref="DiedSeason"/> is -1 while they live.</summary>
public sealed class LegendRecord
{
    public long FigureId;
    public int Civilization;
    public int Settlement;
    public int BornSeason;
    public int DiedSeason = -1;
    public LegendDeed Deed;
    /// <summary>The deed's detail: the project index for an Innovator or a First, the daughter settlement for a Founder, the age for a long life.</summary>
    public int Detail;
    /// <summary>The generated name, as a code (<see cref="LegendNames"/>).</summary>
    public int NameCode;
}

/// <summary>
/// Legends (docs/MVP/40, phase L1): every figure collects deed points; at <see cref="Threshold"/> a figure of a civilization past KEY-1 rolls each season to become a Legend
/// (base chance <see cref="BaseChance"/>, doubled while ART-10's patron stands). From phase L2 a living Legend changes the match (docs/MVP/40 §4): their settlement's Cohesion target, its adults' skill practice, a Legend's own training and their group's
/// break point, and a death is mourned. The log is hashed into the match checksum. Everything is integer or Fix64; rolls come from their own <see cref="Pcg32"/> stream; lists are kept sorted by figure id.
/// </summary>
public sealed class LegendSystem : ILegendEffects
{
    /// <summary>The score at which a figure may become a Legend (P, docs/MVP/40 §3).</summary>
    public const int Threshold = 150;
    /// <summary>The most living Legends a settlement holds (docs/MVP/40 §4); a settlement at the cap rolls for no more.</summary>
    public const int PerSettlementCap = 3;
    /// <summary>The most living Legends a civilization holds.</summary>
    public const int PerCivilizationCap = 12;
    /// <summary>The age past which each year of life scores a point.</summary>
    public const int LongLifeAge = 60;

    public const int InnovatorPoints = 10, FirstPoints = 40, FounderPoints = 60, LongLifePoints = 1;
    /// <summary>A survivor of a won battle scores <see cref="BattlePoints"/>; the strongest survivor scores <see cref="BattleBestBonus"/> more.</summary>
    public const int BattlePoints = 30, BattleBestBonus = 20, FamineHeldPoints = 25, WonderPoints = 60;
    /// <summary>A famine is at least this many seasons under the line.</summary>
    public const int FamineMinSeasons = 3;
    /// <summary>A settlement is in famine while its food security is under this; it has come through when it is back at <see cref="FamineRecovered"/>.</summary>
    public static readonly Fix64 FamineBelow = Fix64.Half, FamineRecovered = Fix64.FromRatio(8, 10);

    /// <summary>The chance a figure at the threshold becomes a Legend in a season: 35% (P).</summary>
    public static readonly Fix64 BaseChance = Fix64.FromRatio(35, 100);

    private static readonly ulong RollStream = Hash64.OfString("legend.roll");
    private static readonly ulong NameStream = Hash64.OfString("legend.name");

    private struct Score
    {
        public long Id;
        public int Total;
        public byte BestDeed;
        public int BestPoints;
        public int Detail;
    }

    private readonly ulong _seed;
    private readonly LivingEconomyModel _economy;
    private readonly ResearchSystem _research;
    private readonly int _key1, _art06, _met06;
    private readonly List<Score> _scores = new();           // sorted by figure id
    private readonly List<LegendRecord> _records = new();   // in order of becoming a Legend
    /// <summary>Settlements in famine: (settlement index, seasons under the line, people when it began), sorted by settlement; sparse.</summary>
    private readonly List<(int Settlement, int Seasons, int People)> _famine = new();
    private readonly List<FigureView> _residentsScratch = new();
    // Derived from the records after every change (not saved or hashed): who is a living Legend, and per settlement how many.
    private readonly List<long> _livingIds = new();   // ascending
    private int[] _livingBySettlement = Array.Empty<int>();
    private int[] _keptBySettlement = Array.Empty<int>();    // dead Legends a relief or a statue still remembers there (docs/MVP/40 §5)

    public LegendSystem(ulong seed, LivingEconomyModel economy, ResearchSystem research)
    {
        _seed = seed;
        _economy = economy;
        _research = research;
        _key1 = research.Catalog.IndexOf("KEY-1");
        _art06 = research.Catalog.IndexOf("ART-06");
        _met06 = research.Catalog.IndexOf("MET-06");
        research.EventObserver = OnResearchEvent;
        economy.LegendEffects = this;
    }

    /// <summary>Every Legend ever, oldest first.</summary>
    public IReadOnlyList<LegendRecord> Records => _records;

    public int ScoreOf(long figureId)
    {
        int at = Find(figureId);
        return at >= 0 ? _scores[at].Total : 0;
    }

    public bool IsLegend(long figureId)
    {
        foreach (LegendRecord r in _records) if (r.FigureId == figureId) return true;
        return false;
    }

    public int LivingCount(int civ)
    {
        int n = 0;
        foreach (LegendRecord r in _records) if (r.Civilization == civ && r.DiedSeason < 0) n++;
        return n;
    }

    public int LivingInSettlement(int settlement)
    {
        int n = 0;
        foreach (LegendRecord r in _records) if (r.Settlement == settlement && r.DiedSeason < 0) n++;
        return n;
    }

    /// <summary>Adds points to a figure (a deed observed by a system). <paramref name="detail"/> names the deed's subject.</summary>
    public void NoteDeed(long figureId, LegendDeed deed, int points, int detail = 0)
    {
        if (figureId < 0 || points <= 0) return;
        int at = Find(figureId);
        if (at < 0)
        {
            at = ~at;
            _scores.Insert(at, new Score { Id = figureId, BestDeed = (byte)deed, BestPoints = 0, Detail = detail });
        }
        Score s = _scores[at];
        s.Total += points;
        if (points > s.BestPoints) { s.BestPoints = points; s.BestDeed = (byte)deed; s.Detail = detail; }
        _scores[at] = s;
    }

    /// <summary>Research reports an innovator (<see cref="ResearchEventKind.Completed"/>) and a First (<see cref="ResearchEventKind.FirstClaimed"/>).</summary>
    private void OnResearchEvent(ResearchEvent e)
    {
        if (e.Kind == ResearchEventKind.Completed && e.A > 0)
        {
            NoteDeed(e.A, LegendDeed.Innovator, InnovatorPoints, e.Project);
            _innovatorsThisSeason.Add((e.Civ, e.Project, e.A));
        }
        else if (e.Kind == ResearchEventKind.FirstClaimed) _firstClaimedThisSeason.Add((e.Civ, e.Project));
    }

    private readonly List<(int Civ, int Project)> _firstClaimedThisSeason = new();
    private readonly List<(int Civ, int Project, long Figure)> _innovatorsThisSeason = new();

    /// <summary>
    /// The season's bookkeeping, called after research and politics have closed the season: Firsts, founders, long life, deaths, then the rolls.
    /// <paramref name="season"/> is the season that just ended.
    /// </summary>
    public void OnSeasonEnd(int season)
    {
        // A First: the innovator of the project already earned the Innovator points; the First adds more to the same figure.
        foreach ((int civ, int project) in _firstClaimedThisSeason)
        {
            foreach ((int c, int p, long figure) in _innovatorsThisSeason)
                if (c == civ && p == project) { NoteDeed(figure, LegendDeed.First, FirstPoints, project); break; }
        }
        _firstClaimedThisSeason.Clear();
        _innovatorsThisSeason.Clear();

        TrackFamine();

        // Long life: one point a year to everyone past sixty, once a year.
        if (season % 4 == 3)
            for (int i = 0; i < _economy.FigureCount; i++)
            {
                FigureView f = _economy.Figure(i);
                if (f.Age > LongLifeAge) NoteDeed(f.Id, LegendDeed.LongLife, LongLifePoints, f.Age);
            }

        MarkDeaths(season);
        Roll(season);
        Prune();
        RebuildEffects();
    }

    /// <summary>
    /// Held through famine (docs/MVP/40 §3): a settlement whose food security stays under 0.5 for three or more seasons and then recovers to 0.8 with at least four fifths of
    /// its people still alive scores the famine deed for its leading elder (the oldest resident of seventy or under), who held it together.
    /// </summary>
    private void TrackFamine()
    {
        for (int i = 0; i < _economy.SettlementCount; i++)
        {
            if (_economy.IsFreeFolkSettlement(i)) continue;
            var lite = _economy.SettlementLite(i);
            if (lite.Civilization < 0) continue;
            int people = _economy.Population(lite.Id);
            if (people <= 0) { RemoveFamine(i); continue; }
            StepFamine(i, people, _economy.Settlement(i).LastFoodSecurity);
        }
    }

    /// <summary>One season of one settlement's famine bookkeeping (see <see cref="TrackFamine"/>).</summary>
    internal void StepFamine(int settlement, int people, Fix64 security)
    {
        int at = FindFamine(settlement);
        if (security < FamineBelow)
        {
            if (at < 0) _famine.Insert(~at, (settlement, 1, people));
            else _famine[at] = (settlement, _famine[at].Seasons + 1, _famine[at].People);
        }
        else if (security >= FamineRecovered && at >= 0)
        {
            (int _, int seasons, int before) = _famine[at];
            _famine.RemoveAt(at);
            if (seasons >= FamineMinSeasons && people * 5 >= before * 4) NoteLeadingElder(settlement, LegendDeed.Famine, FamineHeldPoints, seasons);
        }
    }

    private void NoteLeadingElder(int settlement, LegendDeed deed, int points, int detail)
    {
        _residentsScratch.Clear();
        _economy.ListFigures(settlement, _residentsScratch);
        long best = -1;
        int bestAge = -1;
        foreach (FigureView f in _residentsScratch)
            if (f.Age is >= 14 and <= 70 && f.Age > bestAge) { best = f.Id; bestAge = f.Age; }
        if (best >= 0) NoteDeed(best, deed, points, detail);
    }

    private int FindFamine(int settlement)
    {
        int low = 0, high = _famine.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int v = _famine[middle].Settlement;
            if (v == settlement) return middle;
            if (v < settlement) low = middle + 1; else high = middle - 1;
        }
        return ~low;
    }

    private void RemoveFamine(int settlement)
    {
        int at = FindFamine(settlement);
        if (at >= 0) _famine.RemoveAt(at);
    }

    /// <summary>Keeps each living Legend's settlement current and marks the dead; a death is mourned in the settlement the Legend last lived in (docs/MVP/40 §5).</summary>
    private void MarkDeaths(int season)
    {
        foreach (LegendRecord r in _records)
        {
            if (r.DiedSeason >= 0) continue;
            if (_economy.TryFigure(r.FigureId, out FigureView who)) { r.Settlement = who.SettlementId; continue; }
            // A settlement that keeps this Legend's memory (a free relief or statue slot, docs/MVP/40 §5) does not mourn; the bonus stays.
            bool kept = FreeKeepSlots(r.Settlement) > 0;
            r.DiedSeason = season;
            if (!kept) _economy.NoteMourning(r.Settlement, MourningPoints);
        }
    }

    /// <summary>
    /// How many dead Legends a settlement can still keep (docs/MVP/40 §5): ART-06's carved relief keeps one in every settlement of a civilization that has it, and MET-06's bronze statue a
    /// second in a City. Read from the records, the research and the tier as they stand, so nothing is stored and a relief learned after a Legend died remembers them too.
    /// </summary>
    private int KeepSlots(int settlement)
    {
        if ((uint)settlement >= (uint)_economy.SettlementCount) return 0;
        int civ = _economy.SettlementLite(settlement).Civilization;
        if (civ < 0) return 0;
        int slots = 0;
        if (_art06 >= 0 && _research.IsDone(civ, _art06)) slots++;
        if (_met06 >= 0 && _research.IsDone(civ, _met06) && _economy.Tier(settlement) == SettlementTier.City) slots++;
        return slots;
    }

    private int DeadAt(int settlement)
    {
        int n = 0;
        foreach (LegendRecord r in _records) if (r.DiedSeason >= 0 && r.Settlement == settlement) n++;
        return n;
    }

    private int FreeKeepSlots(int settlement) => KeepSlots(settlement) - DeadAt(settlement);

    /// <summary>
    /// Whether this dead Legend's memory is kept by a relief or a statue: the first to die at a settlement fills the relief, the second the statue (a death in the same season is
    /// ranked in the order the Legends were made, as <see cref="MarkDeaths"/> takes them).
    /// </summary>
    public bool IsRemembered(LegendRecord record)
    {
        if (record.DiedSeason < 0) return false;
        int rank = 0;
        foreach (LegendRecord r in _records)
        {
            if (r == record || r.DiedSeason < 0 || r.Settlement != record.Settlement) continue;
            if (r.DiedSeason < record.DiedSeason || r.DiedSeason == record.DiedSeason && _records.IndexOf(r) < _records.IndexOf(record)) rank++;
        }
        return rank < KeepSlots(record.Settlement);
    }

    /// <summary>The one-time Cohesion dip, in points, when a Legend dies.</summary>
    public const int MourningPoints = 3;
    /// <summary>The Cohesion points a living Legend adds to their settlement's target, and the most a settlement can gain.</summary>
    public const int CohesionPointsEach = 2, CohesionPointsCap = 4;

    /// <summary>Rebuilds the derived lookups from the records. Called whenever the records change and after a restore.</summary>
    private void RebuildEffects()
    {
        _livingIds.Clear();
        int top = 0;
        foreach (LegendRecord r in _records)
            if (r.Settlement + 1 > top) top = r.Settlement + 1;
        if (_livingBySettlement.Length < top) { _livingBySettlement = new int[top]; _keptBySettlement = new int[top]; }
        else { Array.Clear(_livingBySettlement); Array.Clear(_keptBySettlement); }
        foreach (LegendRecord r in _records)
        {
            if (r.DiedSeason >= 0) continue;
            _livingIds.Add(r.FigureId);
            if ((uint)r.Settlement < (uint)_livingBySettlement.Length) _livingBySettlement[r.Settlement]++;
        }
        _livingIds.Sort();
        // The memory a relief or a statue keeps: as many of a settlement's dead Legends as it has slots (docs/MVP/40 §5).
        for (int i = 0; i < _keptBySettlement.Length; i++)
        {
            if (_livingBySettlement[i] == 0 && !HasDeadAt(i)) continue;
            _keptBySettlement[i] = Math.Min(KeepSlots(i), DeadAt(i));
        }
    }

    private bool HasDeadAt(int settlement)
    {
        foreach (LegendRecord r in _records) if (r.DiedSeason >= 0 && r.Settlement == settlement) return true;
        return false;
    }

    public bool IsLivingLegend(long figureId) => _livingIds.BinarySearch(figureId) >= 0;

    public int CohesionPoints(int settlement)
        => (uint)settlement < (uint)_livingBySettlement.Length
            ? Math.Min(CohesionPointsCap, CohesionPointsEach * (_livingBySettlement[settlement] + _keptBySettlement[settlement])) : 0;

    public bool SpreadsSkill(int settlement) => (uint)settlement < (uint)_livingBySettlement.Length && _livingBySettlement[settlement] > 0;

    /// <summary>Whether the log holds anything: the match hashes it only once it does, so a match that never scored keeps an older checksum.</summary>
    public bool HasState => _scores.Count > 0 || _records.Count > 0 || _innovatorsThisSeason.Count > 0 || _firstClaimedThisSeason.Count > 0 || _famine.Count > 0;

    private void Roll(int season)
    {
        for (int i = 0; i < _scores.Count; i++)
        {
            Score s = _scores[i];
            if (s.Total < Threshold || IsLegend(s.Id) || !_economy.TryFigure(s.Id, out FigureView who)) continue;
            int civ = who.Civilization;
            if (!_research.IsDone(civ, _key1)) continue; // Legends begin to appear with the first keystone
            if (LivingInSettlement(who.SettlementId) >= PerSettlementCap || LivingCount(civ) >= PerCivilizationCap) continue;
            int permyriad = ChancePermyriad(civ);
            Pcg32 rng = Pcg32.ForStream(_seed, RollStream, (ulong)s.Id, season);
            if ((int)rng.NextBelow(10000u) >= permyriad) continue;
            _records.Add(new LegendRecord
            {
                FigureId = s.Id, Civilization = civ, Settlement = who.SettlementId, BornSeason = season,
                Deed = (LegendDeed)s.BestDeed, Detail = s.Detail, NameCode = NameCodeOf(s.Id),
            });
        }
    }

    /// <summary>A civilization's chance, in ten-thousandths, that a figure at the threshold becomes a Legend this season: 35%, doubled while ART-10's patron stands.</summary>
    internal int ChancePermyriad(int civ)
        => Math.Min(10000, (BaseChance * _economy.LegendChanceFactor(civ) * Fix64.FromInt(10000)).RoundToInt());

    /// <summary>Drops the scores of figures who are gone and were never Legends, so the list stays about as long as the living population's scorers.</summary>
    private void Prune()
    {
        int write = 0;
        for (int read = 0; read < _scores.Count; read++)
        {
            Score s = _scores[read];
            if (_economy.TryFigure(s.Id, out _) || IsLegend(s.Id)) _scores[write++] = s;
        }
        if (write < _scores.Count) _scores.RemoveRange(write, _scores.Count - write);
    }

    private int NameCodeOf(long figureId)
    {
        Pcg32 rng = Pcg32.ForStream(_seed, NameStream, (ulong)figureId, 0);
        return LegendNames.Code((int)rng.NextBelow((uint)LegendNames.Count));
    }

    private int Find(long id)
    {
        int low = 0, high = _scores.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) >> 1;
            long v = _scores[middle].Id;
            if (v == id) return middle;
            if (v < id) low = middle + 1; else high = middle - 1;
        }
        return ~low;
    }

    // ---- state (phase L2 wires these into the match snapshot and checksum) -------------------------------------------------

    public void Hash(ref StateHasher h)
    {
        h.Add(_scores.Count);
        foreach (Score s in _scores) { h.Add(s.Id); h.Add(s.Total); h.Add((int)s.BestDeed); h.Add(s.BestPoints); h.Add(s.Detail); }
        h.Add(_records.Count);
        foreach (LegendRecord r in _records)
        {
            h.Add(r.FigureId); h.Add(r.Civilization); h.Add(r.Settlement); h.Add(r.BornSeason); h.Add(r.DiedSeason); h.Add((int)r.Deed); h.Add(r.Detail); h.Add(r.NameCode);
        }
        h.Add(_innovatorsThisSeason.Count);
        foreach ((int civ, int project, long figure) in _innovatorsThisSeason) { h.Add(civ); h.Add(project); h.Add(figure); }
        h.Add(_firstClaimedThisSeason.Count);
        foreach ((int civ, int project) in _firstClaimedThisSeason) { h.Add(civ); h.Add(project); }
        h.Add(_famine.Count);
        foreach ((int settlement, int seasons, int people) in _famine) { h.Add(settlement); h.Add(seasons); h.Add(people); }
    }

    private const uint TrailerMagic = 0x444E4745; // "EGND" little-endian
    private const int TrailerVersion = 1;

    /// <summary>
    /// The Legends as an optional trailer of a match snapshot: written only once there is anything to keep, so a snapshot of a match with no scores is byte-identical to one written
    /// before Legends. It sits before the guidance trailer; a reader that finds another magic there puts the four bytes back.
    /// </summary>
    internal void WriteOptionalTrailer(BinaryWriter writer)
    {
        if (_scores.Count == 0 && _records.Count == 0 && _innovatorsThisSeason.Count == 0 && _firstClaimedThisSeason.Count == 0 && _famine.Count == 0) return;
        writer.Write(TrailerMagic);
        writer.Write(TrailerVersion);
        Write(writer);
    }

    internal void ReadOptionalTrailer(BinaryReader reader)
    {
        _scores.Clear();
        _records.Clear();
        _innovatorsThisSeason.Clear();
        _firstClaimedThisSeason.Clear();
        _famine.Clear();
        RebuildEffects();
        Stream stream = reader.BaseStream;
        if (!stream.CanSeek || stream.Length - stream.Position < 4) return;
        long start = stream.Position;
        if (reader.ReadUInt32() != TrailerMagic) { stream.Position = start; return; }
        if (reader.ReadInt32() != TrailerVersion) throw new InvalidDataException("The snapshot has an unsupported Legends trailer.");
        Read(reader);
        RebuildEffects();
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_scores.Count);
        foreach (Score s in _scores) { w.Write(s.Id); w.Write(s.Total); w.Write(s.BestDeed); w.Write(s.BestPoints); w.Write(s.Detail); }
        w.Write(_records.Count);
        foreach (LegendRecord r in _records)
        {
            w.Write(r.FigureId); w.Write(r.Civilization); w.Write(r.Settlement); w.Write(r.BornSeason); w.Write(r.DiedSeason); w.Write((byte)r.Deed); w.Write(r.Detail); w.Write(r.NameCode);
        }
        // Innovators and Firsts reported by research since the season began are scored at its end, so a save between the two must keep them.
        w.Write(_innovatorsThisSeason.Count);
        foreach ((int civ, int project, long figure) in _innovatorsThisSeason) { w.Write(civ); w.Write(project); w.Write(figure); }
        w.Write(_firstClaimedThisSeason.Count);
        foreach ((int civ, int project) in _firstClaimedThisSeason) { w.Write(civ); w.Write(project); }
        w.Write(_famine.Count);
        foreach ((int settlement, int seasons, int people) in _famine) { w.Write(settlement); w.Write(seasons); w.Write(people); }
    }

    public void Read(BinaryReader r)
    {
        _scores.Clear();
        _records.Clear();
        int n = r.ReadInt32();
        if (n < 0 || n > 1_000_000) throw new InvalidDataException("Invalid Legend score count.");
        long previous = long.MinValue;
        for (int i = 0; i < n; i++)
        {
            var s = new Score { Id = r.ReadInt64(), Total = r.ReadInt32(), BestDeed = r.ReadByte(), BestPoints = r.ReadInt32(), Detail = r.ReadInt32() };
            if (s.Id <= previous) throw new InvalidDataException("Legend scores are not in ascending figure order.");
            previous = s.Id;
            _scores.Add(s);
        }
        int m = r.ReadInt32();
        if (m < 0 || m > 1_000_000) throw new InvalidDataException("Invalid Legend record count.");
        for (int i = 0; i < m; i++)
            _records.Add(new LegendRecord
            {
                FigureId = r.ReadInt64(), Civilization = r.ReadInt32(), Settlement = r.ReadInt32(), BornSeason = r.ReadInt32(), DiedSeason = r.ReadInt32(),
                Deed = (LegendDeed)r.ReadByte(), Detail = r.ReadInt32(), NameCode = r.ReadInt32(),
            });
        _innovatorsThisSeason.Clear();
        _firstClaimedThisSeason.Clear();
        int innovators = r.ReadInt32();
        if (innovators < 0 || innovators > 1_000_000) throw new InvalidDataException("Invalid Legend innovator count.");
        for (int i = 0; i < innovators; i++) _innovatorsThisSeason.Add((r.ReadInt32(), r.ReadInt32(), r.ReadInt64()));
        int firsts = r.ReadInt32();
        if (firsts < 0 || firsts > 1_000_000) throw new InvalidDataException("Invalid Legend first count.");
        for (int i = 0; i < firsts; i++) _firstClaimedThisSeason.Add((r.ReadInt32(), r.ReadInt32()));
        _famine.Clear();
        int famine = r.ReadInt32();
        if (famine < 0 || famine > 1_000_000) throw new InvalidDataException("Invalid Legend famine count.");
        int previousSettlement = -1;
        for (int i = 0; i < famine; i++)
        {
            (int settlement, int seasons, int people) = (r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            if (settlement <= previousSettlement) throw new InvalidDataException("Legend famine entries are not in ascending settlement order.");
            previousSettlement = settlement;
            _famine.Add((settlement, seasons, people));
        }
    }
}
