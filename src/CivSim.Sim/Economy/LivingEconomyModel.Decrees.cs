using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The economy's side of the Decrees and the trade objects of docs/MVP/22 §4 and §6 (WP8a). Politics decides and validates;
/// these hooks only carry the decision out: a placed founding (the fission machinery with the Decree's share), the levy host
/// that stands idle, a Corvée that drives civic construction, the goods a compact or caravan moves, and the Tin Folk villages
/// that appear at the world's first KEY-3. Every list here is empty until a Decree or compact uses it, and it is hashed only
/// when it is not, so E0–E2 checksums are those of the economy without it.
/// </summary>
public sealed partial class LivingEconomyModel
{
    private static readonly ulong FoundStream = Hash64.OfString("economy.found");
    private static readonly ulong DecreeTag = 0x4445435245455321UL; // "DECREES!"

    /// <summary>Figures standing with a levy host (docs/MVP/22 §4.3: "the host does no work"), sorted by figure id, with the first season they are free again.</summary>
    private readonly List<(long Figure, int Until)> _idle = new();

    /// <summary>Corvées: settlement, the first season it is over, and the work aimed at (<see cref="CorveeTarget"/>).</summary>
    private readonly List<(int Settlement, int Until, byte Target)> _corvee = new();

    /// <summary>The structure a Corvée is aimed at (docs/MVP/22 §4.3; command 18's C argument for the Corvée Decree).</summary>
    public static class CorveeTarget
    {
        public const byte TopPublicWork = 0, Walls = 1, Temple = 2, Palace = 3, Canal = 4, School = 5, Market = 6, Wonder = 7, Count = 8;
    }

    // ---- queries for the Decree rules -----------------------------------------------------------------

    /// <summary>Whether a settlement may stand on the tile (not water, ice or peak), the fission site rule.</summary>
    public bool IsHabitableSite(int tile) => unchecked((uint)tile) < unchecked((uint)_world.TileCount) && IsHabitable(tile);

    /// <summary>Seasons since the settlement last split (fission or a founding Decree); its 4-season cooldown reads this.</summary>
    public int SeasonsSinceFission(int settlementIndex) => checked((int)_seasonsCompleted) - _settlements[settlementIndex].LastFissionSeason;

    /// <summary>The settlement's people over its EffectivePopCap (min of FoodCap, HousingCap, GroupCap): the fission scoring a founding donor is chosen by.</summary>
    public Fix64 PopulationFill(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        Fix64 cap = Fix64.Min(FoodCap(s), Fix64.Min(HousingCap(s), GroupCap(s)));
        int population = Population(s.Id);
        if (cap <= Fix64.Zero) return population > 0 ? Fix64.FromInt(10) : Fix64.Zero;
        return Fix64.FromInt(population) / cap;
    }

    /// <summary>The food score a daughter band rates a site by (forage, herds and fish within range).</summary>
    public Fix64 SiteFood(int tile) => unchecked((uint)tile) < unchecked((uint)_siteFoodScores.Length) ? _siteFoodScores[tile] : Fix64.Zero;

    /// <summary>Whether <paramref name="civ"/> can dig the deposit on <paramref name="tile"/> at all (the ore is unlocked, depth 2 needs MIN-04, ore left).</summary>
    public bool CanDig(int civ, int tile) => _bronzeActive && CivInBronze(civ) && unchecked((uint)tile) < unchecked((uint)_world.TileCount) && DepositWorkable(civ, tile);

    /// <summary>
    /// Whether the civilization already gets an ore: a workable deposit of <paramref name="kind"/> in one of its settlements' ranges or
    /// a camp of its own on one (the Council asks before it spends Mandate on <c>Open a mine</c>).
    /// </summary>
    public bool CivHasOreSource(int civ, DepositKind kind)
    {
        foreach (MiningCampState camp in _camps)
            if (_settlements[camp.Parent].Civilization == civ && _world.Deposit(camp.Tile).Kind == kind) return true;
        foreach (SettlementState s in _settlements)
        {
            if (s.Civilization != civ || IsFreeFolk(s) || Population(s.Id) == 0) continue;
            foreach (int tile in RangeTiles(s.SiteTile))
                if (_world.Deposit(tile).Kind == kind && DepositWorkable(civ, tile) && !IsRivalTile(civ, tile)) return true;
        }
        return false;
    }

    /// <summary>Mining reach from a settlement in tiles: 8, 12 with carts (TRN-05), 16 from a river or coast with plank boats (SEA-05).</summary>
    public int MiningReachFrom(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        return MiningReach(s.Civilization, s.SiteTile);
    }

    /// <summary>Whether the civilization's metal tiers are known: 8 copper (MET-03), 9 arsenical (MET-04), 10 tin bronze (MET-05).</summary>
    public bool KnowsTier(int civ, int tier) => tier is >= 8 and <= 10 && TierKnown(civ, tier);

    // ---- the founding Decree ---------------------------------------------------------------------------

    /// <summary>
    /// <c>Found a village</c> (docs/MVP/22 §4.3): the donor sends <paramref name="share"/> of its people (at least
    /// <paramref name="minDaughter"/>) to <paramref name="siteTile"/> now, exactly as a fission (cross-section, tools, 40% of the
    /// stores and herds), and the daughter starts at Cohesion <paramref name="startCohesion"/>. It counts as the donor's fission for
    /// its 4-season cooldown. Returns the new settlement's index, or -1 when the donor cannot spare a viable band.
    /// </summary>
    public int FoundByDecree(int donorIndex, int siteTile, Fix64 share, int minDaughter, int startCohesion)
    {
        if (unchecked((uint)donorIndex) >= unchecked((uint)_settlements.Count) || !IsHabitableSite(siteTile)) return -1;
        SettlementState parent = _settlements[donorIndex];
        int season = checked((int)_seasonsCompleted);
        if (season - parent.LastFissionSeason < 4) return -1;
        int population = Population(parent.Id);
        int daughterCount = (Fix64.FromInt(population) * share).RoundToInt();
        if (daughterCount < minDaughter || daughterCount >= population) return -1;
        SettlementState? daughter = SplitSettlement(parent, siteTile, daughterCount, season, FoundStream);
        if (daughter is null) return -1;
        // C = (F − 0.6) ÷ 0.006 (docs/MVP/22 §8.3), so C 60 is F 0.96.
        daughter.Cohesion = Fix64.Clamp(Fix64.Parse("0.6") + Fix64.FromRatio(6, 1000) * startCohesion, Fix64.Parse("0.6"), Fix64.Parse("1.2"));
        return daughter.Id;
    }

    // ---- the levy host ----------------------------------------------------------------------------------

    /// <summary>The figures stand with a levy host and do no work until season <paramref name="until"/> (exclusive).</summary>
    public void SetIdle(IReadOnlyList<long> figureIds, int until)
    {
        foreach (long id in figureIds)
        {
            int at = IdleIndex(id);
            if (at >= 0) _idle[at] = (id, Math.Max(_idle[at].Until, until));
            else _idle.Insert(~at, (id, until));
        }
    }

    /// <summary>Releases figures from a host (the levy was used or dispersed).</summary>
    public void ClearIdle(IReadOnlyList<long> figureIds)
    {
        foreach (long id in figureIds)
        {
            int at = IdleIndex(id);
            if (at >= 0) _idle.RemoveAt(at);
        }
    }

    public bool IsIdle(long figureId)
    {
        int at = IdleIndex(figureId);
        return at >= 0 && _idle[at].Until > _seasonsCompleted;
    }

    private int IdleIndex(long id)
    {
        int low = 0, high = _idle.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            long v = _idle[mid].Figure;
            if (v == id) return mid;
            if (v < id) low = mid + 1; else high = mid - 1;
        }
        return ~low;
    }

    // ---- Corvée -----------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the settlement has public work a Corvée could drive (docs/MVP/22 §4.3: refused when nothing is buildable): the aimed
    /// structure (or, for <see cref="CorveeTarget.TopPublicWork"/>, any civic structure, canal or Market-town housing). Walls are
    /// built by the fortification work package; until then a Corvée on walls finds nothing to build.
    /// </summary>
    public bool HasCivicWork(int settlementIndex, byte target)
    {
        if (unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return false;
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { } c || !CohesionLive(s) || Population(s.Id) == 0) return false;
        switch (target)
        {
            case CorveeTarget.TopPublicWork:
                if (c.BuildKind != CivicBuild.None && CanStillBuild(s, c.BuildKind)) return true;
                if (ChooseCivic(s, c) != CivicBuild.None) return true;
                return CanalCandidates(s) > 0 || (IsMarketTown(s) && HousingCap(s) < HousingTarget(s));
            case CorveeTarget.Canal:
                return CanalCandidates(s) > 0;
            case CorveeTarget.Walls:
                return CanBuild(s.Civilization, Structure.StoneWalls) &&
                    s.Structures[(int)Structure.StoneWalls] < (int)Tier(s.Id) + 1;
            default:
                CivicBuild b = CorveeBuild(target);
                if (b == CivicBuild.None) return false;
                return (c.BuildKind == b && CanStillBuild(s, b)) || (CanStillBuild(s, b) && CivicUnlocked(s.Civilization, b));
        }
    }

    /// <summary>What the settlement is building now, as a <see cref="CorveeTarget"/> (0 when none or not a civic structure).</summary>
    public byte CivicBuildTarget(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { } c) return CorveeTarget.TopPublicWork;
        return c.BuildKind switch
        {
            CivicBuild.Temple => CorveeTarget.Temple, CivicBuild.Palace => CorveeTarget.Palace, CivicBuild.School => CorveeTarget.School,
            CivicBuild.Market => CorveeTarget.Market, CivicBuild.Ziggurat => CorveeTarget.Wonder, _ => CorveeTarget.TopPublicWork,
        };
    }

    private static CivicBuild CorveeBuild(byte target) => target switch
    {
        CorveeTarget.Temple => CivicBuild.Temple, CorveeTarget.Palace => CivicBuild.Palace, CorveeTarget.School => CivicBuild.School,
        CorveeTarget.Market => CivicBuild.Market, CorveeTarget.Wonder => CivicBuild.Ziggurat, _ => CivicBuild.None,
    };

    private bool CivicUnlocked(int civ, CivicBuild b) => b switch
    {
        CivicBuild.Market => Done(civ, "TRD-04"), CivicBuild.Temple => Done(civ, "BEL-06"), CivicBuild.Palace => Done(civ, "GOV-05"),
        CivicBuild.School => Done(civ, "LRN-04"), CivicBuild.Ziggurat => Done(civ, "BLD-08"), _ => false,
    };

    /// <summary>
    /// Starts a Corvée: for the seasons before <paramref name="until"/> the settlement's building labor counts ×<paramref name="mult"/>
    /// (docs/MVP/22 §4.3, 1.5), aimed at <paramref name="target"/>. Returns false (nothing changed) when there is nothing to build.
    /// </summary>
    public bool SetCorvee(int settlementIndex, int until, byte target)
    {
        if (!HasCivicWork(settlementIndex, target)) return false;
        for (int i = 0; i < _corvee.Count; i++)
            if (_corvee[i].Settlement == settlementIndex) { _corvee[i] = (settlementIndex, until, target); return true; }
        _corvee.Add((settlementIndex, until, target));
        SettlementState s = _settlements[settlementIndex];
        CivicBuild b = CorveeBuild(target);
        if (b != CivicBuild.None && s.Civic is { } c && c.BuildKind != b && CanStillBuild(s, b))
        {
            c.BuildKind = b;
            c.BuildProgress = Fix64.Zero;
        }
        if (target == CorveeTarget.Walls && s.BuildTarget != Structure.StoneWalls)
        {
            s.BuildTarget = Structure.StoneWalls;
            s.BuildProgress = Fix64.Zero;
        }
        return true;
    }

    /// <summary>The Corvée running at a settlement: its target, or -1 when none.</summary>
    public int CorveeAt(int settlementIndex)
    {
        foreach ((int settlement, int until, byte target) in _corvee)
            if (settlement == settlementIndex && until > _seasonsCompleted) return target;
        return -1;
    }

    private static readonly Fix64 CorveeMult = Fix64.FromRatio(3, 2);

    /// <summary>The Corvée's labor multiplier on a settlement's civic construction this season (1 without one).</summary>
    private Fix64 CorveeLaborMult(SettlementState s)
        => _corvee.Count > 0 && CorveeAt(s.Id) >= 0 && CorveeAt(s.Id) != CorveeTarget.Walls ? CorveeMult : Fix64.One;

    // ---- goods for compacts and caravans ---------------------------------------------------------------

    /// <summary>Removes up to <paramref name="quantity"/> of a stocked good from a settlement (a caravan loads it) and returns what was taken.</summary>
    public Fix64 TakeGood(int settlementIndex, string goodId, Fix64 quantity)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot || quantity <= Fix64.Zero) return Fix64.Zero;
        SettlementState s = _settlements[settlementIndex];
        if (slot < (int)Good.Count && ToolDurability(s, (Good)slot) > Fix64.Zero) return Fix64.Zero;
        Fix64 moved = Fix64.Min(quantity, Stock(s, slot));
        if (moved <= Fix64.Zero) return Fix64.Zero;
        AddStock(s, slot, -moved);
        return moved;
    }

    /// <summary>Adds a stocked good to a settlement's stores (a caravan unloads, loot comes home).</summary>
    public void PutGood(int settlementIndex, string goodId, Fix64 quantity)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot || quantity <= Fix64.Zero) return;
        AddStock(_settlements[settlementIndex], slot, quantity);
    }

    /// <summary>
    /// What a settlement needs of a trade good over <paramref name="seasons"/> seasons, in the good's unit (docs/MVP/22 §6.1: the
    /// "need" of the scarcity rate). Metals: the metal plan's want, or, where the tier is known but there is no access, the re-kit it
    /// would cast (0.015 L of tin, 0.12 L of copper, 0.15 L of bronze per adult). Food: the settlement's food need. Salt, gold and
    /// luxuries: one unit per 20 people a season.
    /// </summary>
    public Fix64 TradeNeed(int settlementIndex, string goodId, int seasons)
    {
        SettlementState s = _settlements[settlementIndex];
        int pop = Population(s.Id);
        if (pop == 0) return Fix64.Zero;
        Fix64 horizon = Fix64.FromInt(Math.Max(1, seasons));
        if (goodId == FoodGoodId) return FoodNeed(s, false) * horizon;
        int civ = s.Civilization;
        int adults = 0;
        foreach (FigureState f in _figures) if (f.SettlementId == s.Id && f.Age >= 14) adults++;
        MetalGood? metal = goodId switch { "G-TIN" => MetalGood.Tin, "G-COPPER" => MetalGood.Copper, "G-BRONZE" => MetalGood.Bronze, _ => null };
        if (metal is { } g)
        {
            if (!CivInBronze(civ)) return Fix64.Zero;
            Fix64 want = Fix64.Zero;
            if (s.Metal is { } m)
                for (int k = 0; k < SharedGoods.Length; k++)
                    if (SharedGoods[k] == g) want = m.ShareWant[k];
            bool access = s.Metal is { } mm && (mm.Access & (g == MetalGood.Tin ? AccessTin | SourceTin : g == MetalGood.Copper ? AccessCopper | SourceCopper : AccessTin | AccessArsenic)) != 0;
            Fix64 latent = Fix64.Zero;
            if (g == MetalGood.Tin && TierKnown(civ, 10) && !access) latent = Fix64.FromRatio(15, 1000) * adults;
            else if (g == MetalGood.Copper && TierKnown(civ, 8) && !access) latent = Fix64.FromRatio(12, 100) * adults;
            else if (g == MetalGood.Bronze && (TierKnown(civ, 10) || TierKnown(civ, 9)) && !access) latent = Fix64.FromRatio(15, 100) * adults;
            return Fix64.Max(want * horizon, latent);
        }
        if (goodId is "G-SALT" or "G-GOLD" || _content.BarterValue(goodId) > Fix64.Zero)
            return Fix64.FromRatio(pop, 20) * horizon;
        return Fix64.Zero;
    }

    /// <summary>The good id that stands for food in trade (food moves as rations through <see cref="TakeGoods"/> and <see cref="GiveGoods"/>).</summary>
    public const string FoodGoodId = "G-FOOD";

    // ---- Tin Folk (docs/MVP/22 §6.4) -----------------------------------------------------------------

    /// <summary>
    /// A neutral village of new people (docs/MVP/22 §6.4: 9 adults, 2 elders, 13 children; a hearth, a thorn barrier for its
    /// palisade and two seasons of food). It carries <paramref name="nominalCiv"/>'s id, as every settlement must; the caller marks it
    /// Free Folk. Returns its index.
    /// </summary>
    public int SpawnVillage(int nominalCiv, int siteTile, int adults, int elders, int children)
    {
        if (unchecked((uint)nominalCiv) >= unchecked((uint)_completedProjects.Length)) throw new ArgumentOutOfRangeException(nameof(nominalCiv));
        SettlementState s = NewSettlement(nominalCiv, siteTile);
        _settlements.Add(s);
        for (int i = 0; i < adults; i++) AddFigure(nominalCiv, s.Id, 16 + (i * 3) % 26, i % 2 == 0);
        for (int i = 0; i < elders; i++) AddFigure(nominalCiv, s.Id, 50 + i * 4, i % 2 == 1);
        for (int i = 0; i < children; i++) AddFigure(nominalCiv, s.Id, 1 + i % 13, i % 2 == 0);
        s.Structures[(int)Structure.Hearth] = 1;
        s.Structures[(int)Structure.ThornBarrier] = 1;
        s.FireLit = true;
        s.FireKeeperId = FindFireKeeper(s);
        int people = adults + elders + children;
        GiveGoods(s.Id, Fix64.FromInt(people * 2));
        RebuildWeirOwnership();
        return s.Id;
    }

    /// <summary>
    /// A Tin Folk village works its placer (docs/MVP/22 §6.4: "mine and smelt their placer"): up to <paramref name="oreLitres"/> of tin
    /// ore is dug from <paramref name="tile"/> and smelted at half (22 §5.3) into the village's tin. Returns the tin made.
    /// </summary>
    public Fix64 WorkTinPlacer(int settlementIndex, int tile, Fix64 oreLitres)
    {
        if (unchecked((uint)tile) >= unchecked((uint)_world.TileCount) || _world.Deposit(tile).Kind != DepositKind.Tin || oreLitres <= Fix64.Zero) return Fix64.Zero;
        Fix64 ore = Fix64.Min(oreLitres, _ore[tile]);
        if (ore <= Fix64.Zero) return Fix64.Zero;
        _ore[tile] -= ore;
        Fix64 tin = ore / 2;
        AddStock(_settlements[settlementIndex], MetalSlot(MetalGood.Tin), tin);
        return tin;
    }

    // ---- state ----------------------------------------------------------------------------------------

    /// <summary>Drops expired hosts and Corvées (politics calls it once a season).</summary>
    public void PruneDecreeState()
    {
        _idle.RemoveAll(e => e.Until <= _seasonsCompleted);
        _corvee.RemoveAll(e => e.Until <= _seasonsCompleted);
    }

    private void HashDecrees(ref StateHasher hash)
    {
        if (_idle.Count == 0 && _corvee.Count == 0) return;
        hash.Add(DecreeTag);
        hash.Add(_idle.Count);
        foreach ((long figure, int until) in _idle) { hash.Add(figure); hash.Add(until); }
        hash.Add(_corvee.Count);
        foreach ((int settlement, int until, byte target) in _corvee) { hash.Add(settlement); hash.Add(until); hash.Add(target); }
    }

    private void WriteDecrees(BinaryWriter w)
    {
        w.Write(_idle.Count);
        foreach ((long figure, int until) in _idle) { w.Write(figure); w.Write(until); }
        w.Write(_corvee.Count);
        foreach ((int settlement, int until, byte target) in _corvee) { w.Write(settlement); w.Write(until); w.Write(target); }
    }

    private void ReadDecrees(BinaryReader r)
    {
        _idle.Clear();
        int n = ReadCount(r, 1_000_000);
        long last = long.MinValue;
        for (int i = 0; i < n; i++)
        {
            long id = r.ReadInt64();
            if (id <= last) throw new InvalidDataException("Snapshot levy hosts are not in ascending id order.");
            last = id;
            _idle.Add((id, r.ReadInt32()));
        }
        _corvee.Clear();
        n = ReadCount(r, 100_000);
        for (int i = 0; i < n; i++)
        {
            int settlement = r.ReadInt32(), until = r.ReadInt32();
            byte target = r.ReadByte();
            if (unchecked((uint)settlement) >= unchecked((uint)_settlements.Count) || target >= CorveeTarget.Count)
                throw new InvalidDataException("Snapshot contains an invalid Corvée.");
            _corvee.Add((settlement, until, target));
        }
    }

    private void CopyDecreesFrom(LivingEconomyModel source)
    {
        _idle.Clear(); _idle.AddRange(source._idle);
        _corvee.Clear(); _corvee.AddRange(source._corvee);
    }
}
