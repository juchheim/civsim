using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Faith &amp; Fief chapter's switch on the economy (docs/MVP/27, M12 WP-E5-4). Like <see cref="IronChain"/> it is set once from the chapter
/// rules; off, the economy is byte-for-byte the Iron or Bronze economy. It contains the Iron chain: turning it on turns the Iron chain on.
/// <para>
/// **Plumbing only.** No E5 system reads it yet, so a Fief economy plays exactly like an Iron one except that E5 project completions are
/// recorded (<see cref="ProjectCeiling"/>). Each E5 system added later (power ledger, blast furnace, monasteries, powder and the rest) gates on
/// this flag and keeps its state in its own snapshot and hash section, written only when the flag is on, as the Iron state is. The raw-era
/// lookups that index the registry by research era (reach, claim radius, mortality rows read through <c>LivingEconomySource</c> and
/// <c>PoliticsSystem</c>) will read the E5 row in a Fief match; that is intended and is audited in docs/MVP/balance/m12-wp6-mode-plumbing.md.
/// </para>
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool _fiefChain;

    /// <summary>True when the match plays the Faith &amp; Fief chapter. Setting it also turns the Iron chain on.</summary>
    public bool FiefChain
    {
        get => _fiefChain;
        init
        {
            _fiefChain = value;
            if (value)
            {
                IronChain = true;
                _compiledRecipes = _allRecipes;
            }
            ConfigureMetalRecipeExecutionOrder();
        }
    }

    private static readonly Fix64 PowderPerCannonShot = Fix64.FromRatio(1, 4);
    private static readonly Fix64 BaseFiefRecipeWaste = Fix64.FromRatio(1, 20);

    /// <summary>
    /// The SOC-11 craft-loss allowance is a loss of "non-kit crafted goods" (catalog note). Tool kits keep their service-life rule, and ore reduction and metal refining
    /// (smelting, alloying, bloomery, wrought iron, steel) make intermediate metal, not crafted goods, so they carry no craft loss: their fuel and ore are already
    /// accounted by the I-T6 fuel ledger, and a flat 5% on them put the Fief ramp-up window over the F-T3 15% work-share rule (docs/MVP/balance/m12-e5-ft3-fit-2026-11-16.md).
    /// </summary>
    private static bool FiefCraftLossApplies(CompiledRecipe r)
    {
        string id = r.Def.Id;
        if (id.StartsWith("make_tool", StringComparison.Ordinal) || id.StartsWith("cast_t", StringComparison.Ordinal)) return false;
        if (id.StartsWith("smelt_", StringComparison.Ordinal) || id.StartsWith("cosmelt_", StringComparison.Ordinal)) return false;
        return id is not ("alloy_bronze" or "bloomery" or "forge_wrought" or "carburize_steel");
    }

    private Fix64 FiefRecipeWasteRate(int civilization)
    {
        Fix64 quality = Modifier(civilization, "craft_quality");
        return quality > Fix64.One ? BaseFiefRecipeWaste / quality : BaseFiefRecipeWaste;
    }

    /// <summary>Diagnostic craft-role practice exposure; the physical apprenticeship effect reduces eligible recipe labor in <c>FiefRecipeLabor</c>.</summary>
    internal Fix64 FiefCraftRoleTraining(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.CraftRoleTraining : Fix64.Zero;

    /// <summary>Cumulative modeled recipe-input loss, including the guild reduction, for focused diagnostics.</summary>
    internal Fix64 FiefRecipeWaste(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.GuildRecipeWaste : Fix64.Zero;

    /// <summary>Accumulated output-independent craft quality units (one per batch; 1.2 with SOC-11).</summary>
    internal Fix64 FiefCraftQuality(int settlementIndex)
        => FiefChain && _settlements[settlementIndex].Civic is { } civic ? civic.CraftQualityUnits : Fix64.Zero;

    /// <summary>Read-only Fief weapon stock for the owning civilization's AI view.</summary>
    internal Fix64 FiefStock(int settlementIndex, string goodId)
    {
        if (!FiefChain) return Fix64.Zero;
        int slot = SlotOf(goodId);
        return slot < 0 ? Fix64.Zero : Stock(_settlements[settlementIndex], slot);
    }

    /// <summary>Draws Fief cannons from a settlement's existing stock for an active siege.</summary>
    public int TakeCannons(int settlementIndex, int max)
    {
        if (!FiefChain || max <= 0) return 0;
        SettlementState settlement = _settlements[settlementIndex];
        int count = Math.Min(max, Stock(settlement, FiefSlot(FiefGood.Cannon)).FloorToInt());
        if (count > 0) AddStock(settlement, FiefSlot(FiefGood.Cannon), Fix64.FromInt(-count));
        return count;
    }

    /// <summary>Returns unused siege cannons to their home settlement.</summary>
    public void ReturnCannons(int settlementIndex, int count)
    {
        if (!FiefChain || count <= 0) return;
        AddStock(_settlements[settlementIndex], FiefSlot(FiefGood.Cannon), Fix64.FromInt(count));
    }

    /// <summary>Consumes a quarter unit of existing powder for each cannon that fires this season; returns full shots paid.</summary>
    public int PayCannonPowder(int settlementIndex, int requestedShots)
    {
        if (!FiefChain || requestedShots <= 0) return 0;
        SettlementState settlement = _settlements[settlementIndex];
        int shots = Math.Min(requestedShots, (Stock(settlement, FiefSlot(FiefGood.Powder)) / PowderPerCannonShot).FloorToInt());
        if (shots > 0) AddStock(settlement, FiefSlot(FiefGood.Powder), -PowderPerCannonShot * shots);
        return shots;
    }

    // ---- the Arsenal (docs/MVP/37-arsenal.md) -------------------------------------------------------

    /// <summary>
    /// Answers "does this civilization have a conquest prospect": a hostile walled rival within siege supply reach, a force that beats it with margin and
    /// no relief veto. Set by the match wiring; null (no appetite) when the economy runs without politics. A pure function of true state, so it
    /// needs no saved state of its own.
    /// </summary>
    public Func<int, bool>? ConquestProspectProbe { get; set; }
    /// <summary>
    /// The AI prepares an Arsenal after cannon and powder research, or earlier when a conquest prospect creates urgency.
    /// A target is a priority signal, not a prerequisite that deadlocks the train behind a siege plan.
    /// </summary>
    internal bool ArsenalConquestPriorityFor(int civ) => FiefChain && Done(civ, "WAR-12") &&
        ConquestProspectProbe is { } probe && probe(civ);
    internal bool ArsenalWantedFor(int civ) => FiefChain && Done(civ, "WAR-12") &&
        (Done(civ, "CHM-07") || ArsenalConquestPriorityFor(civ));
    private bool HasArsenal(SettlementState s) => FiefChain && IronWorksCount(s, IronStructure.Arsenal) > 0;
    /// <summary>The Arsenal's own supply reach in tiles: 8 at WAR-12, +4 carts (TRN-05), +4 paved roads (TRN-07), +2 horseshoe (TRN-09). Provisional.</summary>
    internal int ArsenalReach(int civ) => 8 + (Done(civ, "TRN-05") ? 4 : 0) + (Done(civ, "TRN-07") ? 4 : 0) + (Done(civ, "TRN-09") ? 2 : 0);
    private const int ArsenalStoneQuarryRadius = 8;

    /// <summary>
    /// Construction stone can come from this town's stores, a sister town's transferable block/ashlar stock, or raw
    /// quarry stone in the eight-tile catchment of any populated settlement connected to the Arsenal site.
    /// </summary>
    private (Fix64 Blocks, Fix64 Ashlar, Fix64 RawStone) ArsenalConstructionStonePotential(SettlementState site)
    {
        int civ = site.Civilization;
        int[] distances = ArsenalLogisticsDistances(site.Id);
        int shareReach = IronSharingEnabled ? Math.Max(0, ShareReach(civ)) : -1;
        var sources = new List<SettlementState>();
        foreach (SettlementState source in _settlements)
            if (distances[source.Id] != int.MaxValue) sources.Add(source);
        sources.Sort((a, b) =>
        {
            int distance = distances[a.Id].CompareTo(distances[b.Id]);
            return distance != 0 ? distance : a.Id.CompareTo(b.Id);
        });

        Fix64 blocks = Fix64.Zero, ashlar = Fix64.Zero, rawStone = Fix64.Zero;
        var seenTiles = new bool[_world.TileCount]; // tiles whose stone was already counted (no hash-ordered set, D4/D5)
        Fix64 carts = ArsenalCartStock(site, site);
        foreach (SettlementState source in sources)
        {
            if (source.Civilization != civ || IsFreeFolk(source) || Population(source.Id) <= 0) continue;
            if (source.Metal is { } metal)
            {
                bool canShare = source.Id == site.Id || shareReach >= 0 &&
                    HexGrid.Distance(source.SiteTile, site.SiteTile, _world.Width) <= shareReach;
                if (canShare)
                {
                    Fix64 reservedBlocks = Fix64.Zero, reservedAshlar = Fix64.Zero;
                    if (metal.IronBuildKind >= 0 &&
                        (source.Id != site.Id || metal.IronBuildKind != (int)IronStructure.Arsenal))
                    {
                        IronSpec committed = IronSpecs[metal.IronBuildKind];
                        reservedBlocks = Fix64.FromInt(committed.Block);
                        reservedAshlar = Fix64.FromInt(committed.Ashlar);
                    }
                    blocks += Fix64.Max(Fix64.Zero, metal.Goods[(int)MetalGood.Block] - reservedBlocks);
                    ashlar += Fix64.Max(Fix64.Zero, metal.Goods[(int)MetalGood.Ashlar] - reservedAshlar);
                }
            }

            foreach (int tile in ReachTiles(source.SiteTile, ArsenalStoneQuarryRadius))
            {
                if (seenTiles[tile]) continue;
                seenTiles[tile] = true;
                if (_stoneStock[tile] <= Fix64.Zero || IsRivalTile(civ, tile)) continue;
                Fix64 take = _stoneStock[tile];
                Fix64 wear = ArsenalCartWearPerUnit(site, source, distances[source.Id], cannon: false);
                if (wear > Fix64.Zero)
                {
                    take = Fix64.Min(take, carts / wear);
                    carts -= take * wear;
                }
                rawStone += take;
            }
        }
        return (blocks, ashlar, rawStone);
    }

    /// <summary>Whether the Arsenal's block and ashlar cost can be supplied by its connected stone catchments.</summary>
    private bool ArsenalConstructionStoneCanFinish(SettlementState site)
    {
        if (!FiefChain || site.Metal is null) return false;
        IronSpec spec = IronSpecs[(int)IronStructure.Arsenal];
        var potential = ArsenalConstructionStonePotential(site);
        return potential.Blocks + potential.Ashlar + potential.RawStone >= Fix64.FromInt(spec.Block + spec.Ashlar);
    }

    /// <summary>Quarries raw stone from connected sister catchments for an Arsenal under construction, paying long-haul cart wear.</summary>
    internal Fix64 TakeArsenalNetworkStone(int settlementIndex, Fix64 amount)
    {
        if (!FiefChain || amount <= Fix64.Zero || unchecked((uint)settlementIndex) >= unchecked((uint)_settlements.Count)) return Fix64.Zero;
        SettlementState site = _settlements[settlementIndex];
        if (site.Metal?.IronBuildKind != (int)IronStructure.Arsenal) return Fix64.Zero;
        int[] distances = ArsenalLogisticsDistances(site.Id);
        var sources = new List<SettlementState>();
        foreach (SettlementState source in _settlements)
            if (distances[source.Id] != int.MaxValue) sources.Add(source);
        sources.Sort((a, b) =>
        {
            int distance = distances[a.Id].CompareTo(distances[b.Id]);
            return distance != 0 ? distance : a.Id.CompareTo(b.Id);
        });

        var seenTiles = new bool[_world.TileCount]; // tiles whose stone was already counted (no hash-ordered set, D4/D5)
        foreach (int tile in ReachTiles(site.SiteTile, ArsenalStoneQuarryRadius)) seenTiles[tile] = true;
        Fix64 left = amount;
        foreach (SettlementState source in sources)
        {
            if (left <= Fix64.Zero) break;
            if (source.Id == site.Id || source.Civilization != site.Civilization || IsFreeFolk(source) || Population(source.Id) <= 0) continue;
            int distance = distances[source.Id];
            Fix64 wear = ArsenalCartWearPerUnit(site, source, distance, cannon: false);
            Fix64 carts = wear > Fix64.Zero ? ArsenalCartStock(site, source) : Fix64.MaxValue;
            foreach (int tile in ReachTiles(source.SiteTile, ArsenalStoneQuarryRadius))
            {
                if (left <= Fix64.Zero) break;
                if (seenTiles[tile]) continue;
                seenTiles[tile] = true;
                if (_stoneStock[tile] <= Fix64.Zero || IsRivalTile(site.Civilization, tile)) continue;
                Fix64 take = Fix64.Min(left, _stoneStock[tile]);
                if (wear > Fix64.Zero) take = Fix64.Min(take, carts / wear);
                if (take <= Fix64.Zero) continue;
                if (wear > Fix64.Zero)
                {
                    Fix64 cost = take * wear;
                    PayArsenalCartWear(site, source, cost);
                    carts -= cost;
                }
                _stoneStock[tile] -= take;
                left -= take;
                RecordSupplyTransfer(source.Id, site.Id, "G-STONE", take);
            }
        }
        return amount - left;
    }
    private int FiefOreReach(SettlementState s)
        => HasArsenal(s) ? Math.Max(MiningReach(s.Civilization, s.SiteTile), ArsenalReach(s.Civilization)) : MiningReach(s.Civilization, s.SiteTile);
    private static readonly Fix64 ArsenalSaltpeterTarget = Fix64.FromRatio(3, 2), ArsenalSulfurTarget = Fix64.FromRatio(1, 5);
    /// <summary>Iron the Arsenal keeps for casting: two cannons' worth at two iron each.</summary>
    private const int ArsenalIronTarget = 4;

    /// <summary>
    /// Shortest haul through populated sister settlements. Each leg must be land-connected and at most the current
    /// Arsenal reach; another settlement can therefore relay goods without every source being beside the Arsenal.
    /// Equal routes resolve in settlement order. This is derived state and needs no snapshot fields.
    /// </summary>
    internal int ArsenalLogisticsDistance(int fromSettlement, int toSettlement)
    {
        if (!FiefChain || fromSettlement < 0 || toSettlement < 0 ||
            fromSettlement >= _settlements.Count || toSettlement >= _settlements.Count) return -1;
        int distance = ArsenalLogisticsDistances(fromSettlement)[toSettlement];
        return distance == int.MaxValue ? -1 : distance;
    }

    private int[] ArsenalLogisticsDistances(int fromSettlement)
    {
        SettlementState from = _settlements[fromSettlement];
        int civ = from.Civilization;
        bool Member(SettlementState s) => s.Civilization == civ && !IsFreeFolk(s) && Population(s.Id) > 0;
        int count = _settlements.Count, reach = ArsenalReach(civ);
        var distance = new int[count];
        var visited = new bool[count];
        Array.Fill(distance, int.MaxValue);
        if (!Member(from)) return distance;
        distance[fromSettlement] = 0;
        for (int pass = 0; pass < count; pass++)
        {
            int next = -1;
            for (int i = 0; i < count; i++)
                if (!visited[i] && distance[i] != int.MaxValue &&
                    (next < 0 || distance[i] < distance[next])) next = i;
            if (next < 0) return distance;
            visited[next] = true;
            SettlementState a = _settlements[next];
            for (int i = 0; i < count; i++)
            {
                SettlementState b = _settlements[i];
                if (visited[i] || !Member(b) || HexGrid.Distance(a.SiteTile, b.SiteTile, _world.Width) > reach ||
                    !LandConnected(a.SiteTile, b.SiteTile)) continue;
                int leg = WalkingTiles(a.SiteTile, b.SiteTile);
                if (RoadLinked(a) && RoadLinked(b)) leg = Math.Max(1, (leg * 2 + 2) / 3);
                int candidate = distance[next] + leg;
                if (candidate < distance[i]) distance[i] = candidate;
            }
        }
        return distance;
    }

    /// <summary>
    /// A local haul retains the existing hand-carried service. Longer relay hauls wear existing carts: one cart per
    /// 4,000 additional unit-tiles, twenty times as much for a cannon. Connected cart stock pays the service;
    /// insufficient carts limit the quantity moved. Roads shorten the route and transport research extends each leg.
    /// </summary>
    private Fix64 ArsenalCartWearPerUnit(SettlementState from, SettlementState to, int distance, bool cannon)
    {
        int extra = Math.Max(0, distance - ArsenalReach(from.Civilization));
        return Fix64.FromRatio(extra * (cannon ? 20 : 1), 4000);
    }

    private Fix64 ArsenalCartStock(SettlementState from, SettlementState to)
    {
        int[] distances = ArsenalLogisticsDistances(from.Id);
        Fix64 stock = Fix64.Zero;
        foreach (SettlementState s in _settlements)
            if (distances[s.Id] != int.MaxValue) stock += Stock(s, MetalSlot(MetalGood.Cart));
        return stock;
    }

    private void PayArsenalCartWear(SettlementState from, SettlementState to, Fix64 wear)
    {
        int slot = MetalSlot(MetalGood.Cart);
        void Pay(SettlementState s)
        {
            Fix64 part = Fix64.Min(wear, Stock(s, slot));
            if (part <= Fix64.Zero) return;
            AddStock(s, slot, -part);
            wear -= part;
        }
        Pay(from);
        Pay(to);
        if (wear <= Fix64.Zero) return;
        int[] distances = ArsenalLogisticsDistances(from.Id);
        foreach (SettlementState s in _settlements)
        {
            if (wear <= Fix64.Zero) break;
            if (s != from && s != to && distances[s.Id] != int.MaxValue) Pay(s);
        }
    }

    private Fix64 MoveArsenalCargo(SettlementState from, SettlementState to, int slot, Fix64 requested, int distance, bool cannon = false)
    {
        if (distance < 0 || requested <= Fix64.Zero) return Fix64.Zero;
        Fix64 amount = Fix64.Min(requested, Stock(from, slot));
        Fix64 wearPerUnit = ArsenalCartWearPerUnit(from, to, distance, cannon);
        if (wearPerUnit > Fix64.Zero) amount = Fix64.Min(amount, ArsenalCartStock(from, to) / wearPerUnit);
        if (amount <= Epsilon) return Fix64.Zero;
        PayArsenalCartWear(from, to, amount * wearPerUnit);
        AddStock(from, slot, -amount);
        AddStock(to, slot, amount);
        string goodId = slot >= FiefSlotBase ? FiefGoodId((FiefGood)(slot - FiefSlotBase)) : MetalGoodId((MetalGood)(slot - (int)Good.Count));
        RecordSupplyTransfer(from.Id, to.Id, goodId, amount);
        return amount;
    }

    /// <summary>Whether a connected own settlement (the site included) holds, camps or can dig the ore.</summary>
    private bool OreSupplyable(SettlementState site, FiefGood ore)
    {
        int civ = site.Civilization;
        int[] distances = ArsenalLogisticsDistances(site.Id);
        foreach (SettlementState t in _settlements)
        {
            if (t.Civilization != civ || IsFreeFolk(t)) continue;
            if (distances[t.Id] == int.MaxValue) continue;
            if (Stock(t, FiefSlot(ore)) > Fix64.Zero || HasFiefOreCamp(t, ore) || HasFiefDepositInReach(t, ore)) return true;
        }
        return false;
    }

    /// <summary>Whether the connected logistics network can provide both powder ores; a readiness fact, never a construction prerequisite.</summary>
    private bool ArsenalCanBeSupplied(SettlementState site) => OreSupplyable(site, FiefGood.Saltpeter) && OreSupplyable(site, FiefGood.Sulfur);

    /// <summary>Connected ore sources, weighted toward shorter hauls, for an automatic siting preference.</summary>
    private int ArsenalSupplyScore(SettlementState site, int[] distances)
    {
        int civ = site.Civilization, score = 0;
        foreach (SettlementState t in _settlements)
        {
            if (t.Civilization != civ || IsFreeFolk(t)) continue;
            if (distances[t.Id] == int.MaxValue) continue;
            int weight = Math.Max(1, 1000 / (1 + distances[t.Id]));
            if (Stock(t, FiefSlot(FiefGood.Saltpeter)) > Fix64.Zero || HasFiefDepositInReach(t, FiefGood.Saltpeter) || HasFiefOreCamp(t, FiefGood.Saltpeter)) score += weight;
            if (Stock(t, FiefSlot(FiefGood.Sulfur)) > Fix64.Zero || HasFiefDepositInReach(t, FiefGood.Sulfur) || HasFiefOreCamp(t, FiefGood.Sulfur)) score += weight;
        }
        return score;
    }

    /// <summary>
    /// A national works prefers a defended home with access to our own frontier rally towns. These are owned facts:
    /// no unseen rival settlement, army, relation or future conquest target is read to choose the site.
    /// </summary>
    private int ArsenalStrategicSiteScore(SettlementState site, int[] distances)
    {
        // Recent raids are a warning, so the planner prefers quieter homes while a civilization with no quiet forge
        // town can still prepare its Arsenal. Active investment is compared separately before these scores.
        int score = 0;
        if (site.Civic is { } civic && checked((int)_seasonsCompleted) - civic.RaidedSeason < 4 * _civic.RaidYears)
            score -= 2000;
        if (HasPhysicalDefense(site)) score += 100;
        if (HasStoneWalls(site)) score += 150;
        if (IronWorksCount(site, IronStructure.Castle) > 0) score += 250;
        if (IronWorksCount(site, IronStructure.ConcentricCastle) > 0) score += 250;
        if (site.Metal is { } metal) score += Math.Min(CastleGarrison, metal.IronSoldiersSupplied) * 25;

        int nearestFront = int.MaxValue;
        foreach (SettlementState rally in _settlements)
        {
            if (rally.Civilization != site.Civilization || IsFreeFolk(rally) || Population(rally.Id) <= 0 ||
                distances[rally.Id] == int.MaxValue) continue;
            if (!rally.UnderInvestment && IronWorksCount(rally, IronStructure.FrontierWall) == 0 &&
                IronWorksCount(rally, IronStructure.Castle) == 0 && IronWorksCount(rally, IronStructure.ConcentricCastle) == 0) continue;
            nearestFront = Math.Min(nearestFront, distances[rally.Id]);
        }
        if (nearestFront != int.MaxValue) score += 750 / (1 + nearestFront);
        return score;
    }

    // Presentation and per-town planner queries often ask for the same national recommendation many times in a
    // season. Cache only this derived answer, with exact owned-input comparison so query order, mid-season changes,
    // restoration and a cold cache all produce the same choice. No cache field is saved or hashed.
    private ArsenalRecommendationCacheEntry?[] _arsenalRecommendationCache = Array.Empty<ArsenalRecommendationCacheEntry?>();
    private sealed class ArsenalRecommendationCacheEntry
    {
        public long Season;
        public int Reach;
        public int Site;
        public ArsenalRecommendationInput[] Inputs = Array.Empty<ArsenalRecommendationInput>();
    }

    private readonly record struct ArsenalRecommendationInput(int Id, int Tile, int Population, bool FreeFolk,
        bool Town, bool Forge, bool SaltpeterSource, bool SulfurSource, bool RoadLinked, bool Invested,
        bool SkipArsenal, int IronBuildKind, long BlockStock, long AshlarStock, long QuarryStone, long CartStock,
        int RaidedSeason, bool PhysicalDefense, bool StoneWalls, bool FrontierWall, bool Castle, bool ConcentricCastle, int Soldiers);

    private ArsenalRecommendationInput[] ArsenalRecommendationInputs(int civ)
    {
        var inputs = new System.Collections.Generic.List<ArsenalRecommendationInput>();
        foreach (SettlementState s in _settlements)
        {
            if (s.Civilization != civ) continue;
            inputs.Add(new ArsenalRecommendationInput(s.Id, s.SiteTile, Population(s.Id), IsFreeFolk(s),
                s.Civic is { Tier: >= 1 }, s.Metal is { Forges: > 0 },
                Stock(s, FiefSlot(FiefGood.Saltpeter)) > Fix64.Zero || HasFiefDepositInReach(s, FiefGood.Saltpeter) || HasFiefOreCamp(s, FiefGood.Saltpeter),
                Stock(s, FiefSlot(FiefGood.Sulfur)) > Fix64.Zero || HasFiefDepositInReach(s, FiefGood.Sulfur) || HasFiefOreCamp(s, FiefGood.Sulfur),
                RoadLinked(s), s.UnderInvestment, BuildFocusSkips(s, BuildTrack.Iron, (int)IronStructure.Arsenal),
                s.Metal?.IronBuildKind ?? -1, s.Metal?.Goods[(int)MetalGood.Block].Raw ?? 0,
                s.Metal?.Goods[(int)MetalGood.Ashlar].Raw ?? 0, ReachStone(s).Raw,
                s.Metal?.Goods[(int)MetalGood.Cart].Raw ?? 0,
                s.Civic?.RaidedSeason ?? -1000, HasPhysicalDefense(s), HasStoneWalls(s),
                IronWorksCount(s, IronStructure.FrontierWall) > 0, IronWorksCount(s, IronStructure.Castle) > 0, IronWorksCount(s, IronStructure.ConcentricCastle) > 0,
                s.Metal?.IronSoldiersSupplied ?? 0));
        }
        return inputs.ToArray();
    }

    private static bool SameArsenalRecommendationInputs(ArsenalRecommendationInput[] a, ArsenalRecommendationInput[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    /// <summary>The automatic planner's recommended forge town; player requests can choose any eligible town.</summary>
    internal int ArsenalRecommendedSiteFor(int civ)
    {
        int built = ArsenalSettlement(civ);
        if (built >= 0) return built;
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && s.Metal?.IronBuildKind == (int)IronStructure.Arsenal &&
                !BuildFocusSkips(s, BuildTrack.Iron, (int)IronStructure.Arsenal) && ArsenalConstructionStoneCanFinish(s)) return s.Id;

        int reach = ArsenalReach(civ);
        ArsenalRecommendationInput[] inputs = ArsenalRecommendationInputs(civ);
        if (_arsenalRecommendationCache.Length <= civ) Array.Resize(ref _arsenalRecommendationCache, Math.Max(civ + 1, _completedProjects.Length));
        if (_arsenalRecommendationCache[civ] is { } cached &&
            cached.Season == _seasonsCompleted && cached.Reach == reach && SameArsenalRecommendationInputs(cached.Inputs, inputs))
            return cached.Site;

        int best = -1, bestScore = int.MinValue, bestPop = -1;
        bool bestInvested = true;
        foreach (SettlementState other in _settlements)
        {
            if (other.Civilization != civ || IsFreeFolk(other) || other.Civic is not { Tier: >= 1 } || other.Metal is not { Forges: > 0 } ||
                BuildFocusSkips(other, BuildTrack.Iron, (int)IronStructure.Arsenal) || !ArsenalConstructionStoneCanFinish(other)) continue;
            int p = Population(other.Id);
            if (p < _civic.TownMinPop) continue;
            bool invested = other.UnderInvestment;
            int[] distances = ArsenalLogisticsDistances(other.Id);
            int score = ArsenalSupplyScore(other, distances) + ArsenalStrategicSiteScore(other, distances);
            if (best >= 0 && invested != bestInvested && invested) continue;
            if (best >= 0 && invested == bestInvested &&
                (score < bestScore || score == bestScore && (p < bestPop || p == bestPop && other.Id > best))) continue;
            best = other.Id;
            bestScore = score;
            bestPop = p;
            bestInvested = invested;
        }
        _arsenalRecommendationCache[civ] = new ArsenalRecommendationCacheEntry
        {
            Season = _seasonsCompleted, Reach = reach, Site = best, Inputs = inputs,
        };
        return best;
    }

    private bool IsBestArsenalSite(SettlementState s) => ArsenalRecommendedSiteFor(s.Civilization) == s.Id;
    private bool IsPreferredArsenalSite(SettlementState s) => IsBestArsenalSite(s);

    /// <summary>Built or currently commissioned site, falling back to the town the player is inspecting.</summary>
    internal int ArsenalReadinessSiteFor(int civ, int fallback)
    {
        int built = ArsenalSettlement(civ);
        if (built >= 0) return built;
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && s.Metal?.IronBuildKind == (int)IronStructure.Arsenal) return s.Id;
        return fallback;
    }

    /// <summary>Facts for showing supply and an actionable next step before the player commissions an Arsenal.</summary>
    internal (int[] SaltpeterSources, int[] SulfurSources, int[] DisconnectedOreSources, int ConnectedSettlements,
        Fix64 CartStock, bool CannonResearchReady, bool PowderResearchReady, Fix64 Iron, Fix64 CastIron, Fix64 Charcoal,
        Fix64 Saltpeter, Fix64 Sulfur, Fix64 Clay, Fix64 Wood)
        ArsenalReadinessFor(int settlementIndex)
    {
        SettlementState site = _settlements[settlementIndex];
        var saltpeter = new System.Collections.Generic.List<int>();
        var sulfur = new System.Collections.Generic.List<int>();
        var disconnected = new System.Collections.Generic.List<int>();
        int connected = 0;
        int[] distances = ArsenalLogisticsDistances(site.Id);
        foreach (SettlementState other in _settlements)
        {
            if (other.Civilization != site.Civilization || IsFreeFolk(other) || Population(other.Id) <= 0) continue;
            bool salt = Stock(other, FiefSlot(FiefGood.Saltpeter)) > Fix64.Zero || HasFiefOreCamp(other, FiefGood.Saltpeter) || HasFiefDepositInReach(other, FiefGood.Saltpeter);
            bool sulf = Stock(other, FiefSlot(FiefGood.Sulfur)) > Fix64.Zero || HasFiefOreCamp(other, FiefGood.Sulfur) || HasFiefDepositInReach(other, FiefGood.Sulfur);
            if (distances[other.Id] == int.MaxValue)
            {
                if (salt || sulf) disconnected.Add(other.Id);
                continue;
            }
            connected++;
            if (salt) saltpeter.Add(other.Id);
            if (sulf) sulfur.Add(other.Id);
        }
        return (saltpeter.ToArray(), sulfur.ToArray(), disconnected.ToArray(), connected, ArsenalCartStock(site, site),
            Done(site.Civilization, "WAR-12") && Done(site.Civilization, "MET-12"), Done(site.Civilization, "CHM-07"),
            Stock(site, MetalSlot(MetalGood.Iron)), Stock(site, FiefSlot(FiefGood.CastIron)), Stock(site, MetalSlot(MetalGood.Charcoal)),
            Stock(site, FiefSlot(FiefGood.Saltpeter)), Stock(site, FiefSlot(FiefGood.Sulfur)),
            Stock(site, (int)Good.Clay), Stock(site, (int)Good.Wood));
    }

    /// <summary>
    /// Connected towns that can dig saltpeter or sulfur dig it for the Arsenal (their want rides on the ordinary ore planner), and the same towns
    /// are asked for nothing else. Called per town from the weapon wants.
    /// </summary>
    private void FiefArsenalSupplyWants(SettlementState s, MetalPlan plan)
    {
        if (!FiefChain || !Done(s.Civilization, "WAR-12") || !Done(s.Civilization, "CHM-07")) return;
        int a = ArsenalSettlement(s.Civilization);
        if (a < 0 || a == s.Id) return;
        SettlementState arsenal = _settlements[a];
        if (ArsenalLogisticsDistance(s.Id, arsenal.Id) < 0) return;
        foreach (FiefGood ore in new[] { FiefGood.Saltpeter, FiefGood.Sulfur })
        {
            if (BestRangeFiefDeposit(s, ore, out _) < 0 && !HasFiefOreCamp(s, ore) && !HasFiefDepositInReach(s, ore)) continue;
            Fix64 target = ore == FiefGood.Saltpeter ? ArsenalSaltpeterTarget : ArsenalSulfurTarget;
            Fix64 have = Stock(arsenal, FiefSlot(ore)) + Stock(s, FiefSlot(ore));
            if (have < target) plan.Terminal[FiefSlot(ore)] += target - have;
        }
    }

    /// <summary>
    /// Season step: connected towns carry saltpeter and sulfur to the Arsenal and send iron above their own bloom need.
    /// Remote shipments wear existing carts. Each move debits the sender before crediting the Arsenal, in town order.
    /// </summary>
    private void ShipArsenalSupplies()
    {
        if (!FiefChain) return;
        for (int civ = 0; civ < _completedProjects.Length; civ++)
        {
            int a = ArsenalSettlement(civ);
            if (a < 0) continue;
            SettlementState arsenal = _settlements[a];
            int[] distances = ArsenalLogisticsDistances(a);
            foreach (SettlementState t in _settlements)
            {
                if (t == arsenal || t.Civilization != civ || IsFreeFolk(t) || Population(t.Id) == 0) continue;
                int distance = distances[t.Id];
                if (distance == int.MaxValue) continue;
                foreach (FiefGood ore in new[] { FiefGood.Saltpeter, FiefGood.Sulfur })
                {
                    Fix64 amount = Stock(t, FiefSlot(ore));
                    if (amount <= Fix64.Zero) continue;
                    MoveArsenalCargo(t, arsenal, FiefSlot(ore), amount, distance);
                }
                if (t.Metal is { } tm && arsenal.Metal is { } am)
                {
                    Fix64 need = Fix64.FromInt(ArsenalIronTarget) - am.Goods[(int)MetalGood.Iron];
                    Fix64 surplus = tm.Goods[(int)MetalGood.Iron] - IronBloomNeed(t);
                    Fix64 move = Fix64.Min(need, surplus);
                    if (move > Epsilon)
                    {
                        MoveArsenalCargo(t, arsenal, MetalSlot(MetalGood.Iron), move, distance);
                    }
                }
            }
        }
    }

    /// <summary>The settlement holding the civilization's Arsenal, or -1.</summary>
    private int ArsenalSettlement(int civ)
    {
        if (!FiefChain) return -1;
        foreach (SettlementState s in _settlements)
            if (s.Civilization == civ && !IsFreeFolk(s) && IronWorksCount(s, IronStructure.Arsenal) > 0) return s.Id;
        return -1;
    }

    /// <summary>
    /// Cannons and powder a host can draw on: its own stock plus the connected Arsenal's deliverable stock, limited by
    /// exactly the cart service that staging will pay when the host marches.
    /// </summary>
    internal (Fix64 Cannons, Fix64 Powder) FiefTrainStockAt(int settlementIndex)
    {
        if (!FiefChain) return (Fix64.Zero, Fix64.Zero);
        SettlementState s = _settlements[settlementIndex];
        Fix64 cannons = Stock(s, FiefSlot(FiefGood.Cannon)), powder = Stock(s, FiefSlot(FiefGood.Powder));
        int arsenal = ArsenalSettlement(s.Civilization);
        if (arsenal >= 0 && arsenal != settlementIndex && ArsenalLogisticsDistance(arsenal, settlementIndex) is int distance and >= 0)
        {
            SettlementState from = _settlements[arsenal];
            Fix64 carts = ArsenalCartStock(from, s);
            Fix64 cannonMove = Stock(from, FiefSlot(FiefGood.Cannon));
            Fix64 cannonWear = ArsenalCartWearPerUnit(from, s, distance, cannon: true);
            if (cannonWear > Fix64.Zero) cannonMove = Fix64.Min(cannonMove, carts / cannonWear);
            carts = Fix64.Max(Fix64.Zero, carts - cannonMove * cannonWear);
            Fix64 powderMove = Stock(from, FiefSlot(FiefGood.Powder));
            Fix64 powderWear = ArsenalCartWearPerUnit(from, s, distance, cannon: false);
            if (powderWear > Fix64.Zero) powderMove = Fix64.Min(powderMove, carts / powderWear);
            cannons += cannonMove;
            powder += powderMove;
        }
        return (cannons, powder);
    }

    /// <summary>Hauls the Arsenal's cannons and powder to the host's home settlement so the ordinary train draw finds them there.</summary>
    internal void StageFiefTrain(int homeSettlement)
    {
        if (!FiefChain) return;
        SettlementState home = _settlements[homeSettlement];
        int arsenal = ArsenalSettlement(home.Civilization);
        if (arsenal < 0 || arsenal == homeSettlement) return;
        int distance = ArsenalLogisticsDistance(arsenal, homeSettlement);
        if (distance < 0) return;
        SettlementState from = _settlements[arsenal];
        foreach (FiefGood good in new[] { FiefGood.Cannon, FiefGood.Powder })
        {
            Fix64 amount = Stock(from, FiefSlot(good));
            if (amount <= Fix64.Zero) continue;
            MoveArsenalCargo(from, home, FiefSlot(good), amount, distance, good == FiefGood.Cannon);
        }
    }

    /// <summary>Report-only: Arsenals standing for a civilization (0 or 1) and whether the AI currently wants one.</summary>
    internal (int Standing, bool Wanted) ArsenalStateForDiagnostics(int civ)
    {
        int n = 0;
        foreach (SettlementState s in _settlements) if (s.Civilization == civ && !IsFreeFolk(s) && FiefChain && IronWorksCount(s, IronStructure.Arsenal) > 0) n++;
        return (n, ArsenalWantedFor(civ));
    }
    private void FiefWeaponWants(SettlementState settlement, MetalPlan plan)
    {
        FiefCommunicationWants(settlement, plan);
        FiefArsenalSupplyWants(settlement, plan);
        if (!FiefChain || !Done(settlement.Civilization, "WAR-12") || !HasArsenal(settlement)) return;
        // The Arsenal keeps a small train: two cannons and two shots of powder.
        plan.Terminal[FiefSlot(FiefGood.Cannon)] = Fix64.FromInt(2);
        if (Done(settlement.Civilization, "CHM-07"))
            plan.Terminal[FiefSlot(FiefGood.Powder)] = PowderPerCannonShot * 2;
    }

    internal void FixtureSetGoodStock(int settlementIndex, string goodId, Fix64 amount)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot) throw new ArgumentException($"Unknown good '{goodId}'.", nameof(goodId));
        AddStock(_settlements[settlementIndex], slot, amount - Stock(_settlements[settlementIndex], slot));
    }

    internal Fix64 FixtureGoodStock(int settlementIndex, string goodId)
    {
        int slot = SlotOf(goodId);
        if (slot == NoSlot) throw new ArgumentException($"Unknown good '{goodId}'.", nameof(goodId));
        return Stock(_settlements[settlementIndex], slot);
    }
}
