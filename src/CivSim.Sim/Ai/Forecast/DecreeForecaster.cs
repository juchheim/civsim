using System;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// The forecast card before a Decree is confirmed (docs/MVP/22 §4.4): "Elders expect: ...". Fog-fair (14 §3.1): it reads the issuing
/// civilization's own state exactly (Mandate, donors, crews, valuations, own tiles) and everyone else only through the
/// <see cref="Belief"/> given (an AI seat's), or, with none, only what lies on tiles the civilization knows (its Reach and its
/// expeditions' reports). It never reads a rival's stores, people or defenders directly, so it can be wrong, and the Chronicle line
/// after resolution says how it went. The view layer calls it for the player; the AI work package's Strategist may score Decrees with it.
/// </summary>
public static class DecreeForecaster
{
    public static DecreeForecast Forecast(PoliticsSystem pol, int civ, DecreeKind kind, int target, int extra, Belief? belief = null)
        => Forecast(pol, civ, kind, target, extra, belief, knownOwnedWeight: null, donorProtectionCache: null);

    internal static DecreeForecast Forecast(PoliticsSystem pol, int civ, DecreeKind kind, int target, int extra,
        Belief? belief, int? knownOwnedWeight)
        => Forecast(pol, civ, kind, target, extra, belief, knownOwnedWeight, donorProtectionCache: null);

    // Strategist batches evaluate every candidate against the same unchanged belief snapshot, so Found forecasts can share
    // the map-wide owned-land denominator and lazily cache donor protection results. Direct callers retain the uncached public path above.
    internal static DecreeForecast Forecast(PoliticsSystem pol, int civ, DecreeKind kind, int target, int extra,
        Belief? belief, int? knownOwnedWeight, byte[]? donorProtectionCache)
    {
        ResearchViewForDecrees r = pol.DecreeResearch;
        int cost = pol.DecreeCost(civ, kind);
        int mandate = r.Mandate(civ);
        OrderRefusalReason why = 0;
        if (!r.MandateUnlocked(civ) || mandate < cost) why = OrderRefusalReason.NoMandate;
        int success = 1000, people = 0, tiles = 0, relations = 0, seasons = 1;
        Fix64 goods = Fix64.Zero, landWeight = Fix64.Zero, dominion = Fix64.Zero, information = Fix64.Zero;
        Fix64 metalValue = Fix64.Zero, wallEta = Fix64.Zero, threatMargin = Fix64.Zero;
        int threatEta = -1;
        if (why == 0)
        {
            switch (kind)
            {
                case DecreeKind.Found:
                    why = FoundOwnCheck(pol, civ, target, belief, donorProtectionCache, out people);
                    int radius = ClaimRadius(pol, civ);
                    tiles = 3 * radius * (radius + 1) + 1;
                    (tiles, landWeight, dominion) = knownOwnedWeight is int cachedWeight
                        ? FoundLandValue(pol, civ, target, radius, belief, cachedWeight)
                        : FoundLandValue(pol, civ, target, radius, belief);
                    break;
                case DecreeKind.Mine:
                    why = belief is null ? MineKnownTileCheck(pol, civ, target) : MineBeliefCheck(pol, civ, target, belief);
                    if (KnownDeposit(pol, civ, target, belief, out WorldTile deposit))
                    {
                        (metalValue, goods) = EstimateMineValue(pol, civ, target, deposit);
                    }
                    break;
                case DecreeKind.Expedition:
                    if (unchecked((uint)target) >= unchecked((uint)pol.World.TileCount)) { why = OrderRefusalReason.NoSite; break; }
                    int nearest = NearestOwnTile(pol, civ, target, out int d);
                    if (nearest < 0) { why = OrderRefusalReason.NoDonor; break; }
                    // Route feasibility and shipwreck risk depend on unseen passability and open water. Use only
                    // geometric range/time plus a fixed 90% route-success prior; resolution determines the actual route.
                    if (d > pol.ExpeditionRange(civ)) { why = OrderRefusalReason.OutOfReach; break; }
                    tiles = UnknownTilesNear(pol, civ, target, 5, belief);
                    information = ExpeditionInformation(pol, civ, target, 5, belief);
                    seasons = 2 * Math.Max(1, (d + 9) / 10);
                    success = 900;
                    break;
                case DecreeKind.Levy:
                    why = LevyCheck(pol, civ, target, belief, out people, out success);
                    seasons = 2;
                    break;
                case DecreeKind.Corvee:
                    bool own = unchecked((uint)target) < unchecked((uint)pol.SettlementTotal) && pol.SettlementCivOf(target) == civ && !pol.IsFreeFolk(target);
                    if (!own || !pol.Economy.HasCivicWork(target, (byte)Math.Clamp(extra, 0, 7))) why = OrderRefusalReason.NoSite;
                    if (extra == LivingEconomyModel.CorveeTarget.Walls)
                    {
                        (wallEta, threatEta, threatMargin) = EstimateWallThreatTiming(pol, civ, target, belief);
                        int floorWallEta = wallEta.FloorToInt();
                        seasons = Math.Max(1, floorWallEta + (wallEta > Fix64.FromInt(floorWallEta) ? 1 : 0));
                    }
                    else seasons = 2;
                    break;
                case DecreeKind.Compact:
                    success = CompactAcceptance(pol, civ, target, extra, belief, out goods, out why);
                    relations = success >= 500 ? 1 : 0;
                    break;
                default:
                    why = OrderRefusalReason.InvalidTarget;
                    break;
            }
        }
        bool allowed = why == 0;
        return new DecreeForecast(kind, allowed, why, cost, allowed ? mandate - cost : mandate, allowed ? success : 0, people, tiles, goods,
            relations, seasons, landWeight, dominion, information, metalValue, wallEta, threatEta, threatMargin);
    }

    private static bool KnownDeposit(PoliticsSystem pol, int civ, int tile, Belief? belief, out WorldTile known)
    {
        if (unchecked((uint)tile) >= unchecked((uint)pol.World.TileCount)) { known = default; return false; }
        if (belief is not null) return belief.Map.TryTile(tile, out known);
        if (!pol.KnowsTile(civ, tile)) { known = default; return false; }
        known = pol.World.TileAt(tile);
        return true;
    }

    /// <summary>Expected marginal annual alloy supply from a known deposit, discounted for own sources and current output.</summary>
    internal static (Fix64 Value, Fix64 AnnualOutput) EstimateMineValue(PoliticsSystem pol, int civ, int tile, WorldTile deposit)
    {
        LivingEconomyModel economy = pol.Economy;
        MetalOutputRates output = economy.MetalOutputRate(civ);
        int population = 0;
        for (int s = 0; s < pol.SettlementTotal; s++)
            if (pol.SettlementCivOf(s) == civ && !pol.IsFreeFolk(s)) population += pol.SettlementPopulationOf(s);

        Fix64 demand, current, routeValue, conversion;
        DepositKind sourceKind;
        switch (deposit.Deposit)
        {
            case DepositKind.Tin:
                demand = Fix64.FromRatio((long)population * 85, 100_000); // 0.085 L per 100 people/year (§5.6)
                current = output.Tin; routeValue = Fix64.FromInt(40); conversion = Fix64.Half; sourceKind = DepositKind.Tin;
                break;
            case DepositKind.ArsenicalCopper:
                demand = Fix64.FromRatio((long)population * 58, 10_000); // arsenical route is a complete bronze substitute
                current = output.ArsenicalBronze; routeValue = Fix64.FromInt(24); conversion = Fix64.FromRatio(1, 4); sourceKind = DepositKind.ArsenicalCopper;
                break;
            case DepositKind.CopperOre:
                demand = Fix64.FromRatio((long)population * 77, 10_000); // 0.77 L copper per 100 people/year (§5.6)
                current = output.Copper; routeValue = Fix64.FromInt(12); conversion = Fix64.FromRatio(1, 4); sourceKind = DepositKind.CopperOre;
                break;
            default:
                // Non-alloy materials retain a small generic mine value; their route can still be useful in later eras.
                return (Fix64.FromRatio(deposit.DepositAmount, 20), Fix64.FromRatio(deposit.DepositAmount, 20));
        }

        bool source = economy.CivHasOreSource(civ, sourceKind);
        MetalAccessKind accessKind = sourceKind switch
        {
            DepositKind.Tin => MetalAccessKind.Tin,
            DepositKind.ArsenicalCopper => MetalAccessKind.Arsenic,
            _ => MetalAccessKind.Copper,
        };
        bool access = economy.CivHasMetalAccess(civ, accessKind);
        int crew = pol.DecreeResearch.IsDone(civ, "MIN-04") ? 6 : 4;
        Fix64 orePerWorkSeason = deposit.Deposit == DepositKind.Tin ? Fix64.Half : Fix64.One;
        if (pol.DecreeResearch.IsDone(civ, "MIN-04")) orePerWorkSeason *= Fix64.FromRatio(3, 2);
        if (pol.DecreeResearch.IsDone(civ, "MET-04") || pol.DecreeResearch.IsDone(civ, "MET-05"))
            orePerWorkSeason *= Fix64.FromRatio(13, 10); // bronze picks improve mine labor (§5.4)
        Fix64 campAnnualCapacity = Fix64.FromInt(Math.Min(crew, Math.Max(1, population))) * orePerWorkSeason * Fix64.FromInt(2);
        Fix64 availableMetal = campAnnualCapacity * conversion;
        Fix64 deficit = Fix64.Max(Fix64.Zero, demand - current);
        Fix64 marginal = Fix64.Min(availableMetal, deficit);
        Fix64 security = !source && !access ? Fix64.Min(availableMetal, demand) * Fix64.FromRatio(1, 4) : Fix64.Zero;
        if (source) marginal *= Fix64.FromRatio(1, 4);
        if (access) marginal *= Fix64.FromRatio(1, 4);
        Fix64 annual = marginal + security;
        return (annual * routeValue, annual);
    }

    internal static (Fix64 WallEta, int ThreatEta, Fix64 Margin) EstimateWallThreatTiming(PoliticsSystem pol, int civ, int settlement, Belief? belief)
    {
        if (unchecked((uint)settlement) >= unchecked((uint)pol.SettlementTotal) || belief is null) return (Fix64.Zero, -1, Fix64.Zero);
        int population = pol.SettlementPopulationOf(settlement);
        if (population <= 0) return (Fix64.Zero, -1, Fix64.Zero);
        int wallKind = LivingEconomyModel.StructureIndex("stone_walls");
        int totalRings = (int)pol.Economy.Tier(settlement) + 1;
        int current = pol.Economy.StructureCount(settlement, wallKind);
        int remainingRings = Math.Max(1, totalRings - current);
        Fix64 effort = Fix64.FromInt(90 * remainingRings); // 30 wall work-seasons + ~60 quarry/dress work (§7.2)
        (int buildKind, Fix64 share) = pol.Economy.SettlementBuild(settlement);
        if (buildKind == wallKind) effort = Fix64.Max(Fix64.Zero, effort - Fix64.FromInt(90) * share);
        Fix64 workPerSeason = Fix64.Max(Fix64.One, Fix64.FromRatio((long)population * 45, 100)); // calibrated to the §7.2 village, ~2 years
        Fix64 corveeRate = workPerSeason * Fix64.FromRatio(3, 2);
        Fix64 acceleratedEffort = corveeRate * Fix64.FromInt(2);
        Fix64 wallEta = effort <= acceleratedEffort ? effort / corveeRate : Fix64.FromInt(2) + (effort - acceleratedEffort) / workPerSeason;

        int nearestEta = int.MaxValue;
        int ownTile = pol.SettlementTileOf(settlement);
        foreach (RivalBelief rival in belief.Rivals)
        {
            bool supported = rival.AtWar || rival.ThreatProbability >= Fix64.FromRatio(1, 4) ||
                belief.Season - rival.LastRaidOnUsSeason is >= 0 and <= 4;
            if (!supported) continue;
            foreach (BeliefSettlement site in rival.Settlements)
            {
                if (belief.Season - site.LastSeenSeason is < 0 or > 8) continue;
                int distance = HexGrid.Distance(site.Tile, ownTile, pol.World.Width);
                nearestEta = Math.Min(nearestEta, Math.Max(1, (distance + PoliticsSystem.TilesPerSeason - 1) / PoliticsSystem.TilesPerSeason));
            }
        }
        if (nearestEta == int.MaxValue) return (wallEta, -1, Fix64.FromInt(-1));
        return (wallEta, nearestEta, Fix64.FromInt(nearestEta) - wallEta);
    }

    private static OrderRefusalReason MineKnownTileCheck(PoliticsSystem pol, int civ, int tile)
    {
        if (unchecked((uint)tile) >= unchecked((uint)pol.World.TileCount) || !pol.KnowsTile(civ, tile)) return OrderRefusalReason.NoSite;
        WorldTile known = pol.World.TileAt(tile);
        if (known.Deposit is not (DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or
            DepositKind.Silver or DepositKind.Salt or DepositKind.Iron)) return OrderRefusalReason.NoSite;
        ResearchViewForDecrees research = pol.DecreeResearch;
        bool unlocked = known.Deposit switch
        {
            DepositKind.CopperOre => research.Era(civ) >= 3,
            DepositKind.ArsenicalCopper => research.IsDone(civ, "MET-04"),
            DepositKind.Tin => research.IsDone(civ, "MET-05"),
            DepositKind.Gold or DepositKind.Silver => research.IsDone(civ, "MET-07"),
            DepositKind.Salt => research.IsDone(civ, "MIN-06"),
            DepositKind.Iron => research.IsDone(civ, "MET-08"),
            _ => false,
        };
        if (!unlocked || known.DepositDepth >= 2 && !research.IsDone(civ, "MIN-04")) return OrderRefusalReason.NoSite;
        int nearest = -1, best = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            int distance = HexGrid.Distance(pol.SettlementTileOf(s), tile, pol.World.Width);
            if (distance < best) { best = distance; nearest = s; }
        }
        if (nearest < 0) return OrderRefusalReason.NoDonor;
        return best > pol.Economy.MiningReachFrom(nearest) ? OrderRefusalReason.OutOfReach : (OrderRefusalReason)0;
    }

    private static int ClaimRadius(PoliticsSystem pol, int civ) => pol.ClaimRadiusOfEra(pol.DecreeResearch.Era(civ));

    /// <summary>Mine feasibility from a recent tile sighting and exact own settlement reach, without consulting hidden live ownership or ore.</summary>
    private static OrderRefusalReason MineBeliefCheck(PoliticsSystem pol, int civ, int tile, Belief belief)
    {
        if (unchecked((uint)tile) >= unchecked((uint)belief.Map.TileCount) || !belief.Map.TryTile(tile, out WorldTile known) ||
            known.Deposit is not (DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or
                DepositKind.Silver or DepositKind.Salt or DepositKind.Iron)) return OrderRefusalReason.NoSite;

        (int owner, int seenSeason) = belief.Map.OwnerSeen(tile);
        if (belief.Season - seenSeason is < 0 or > 2 || owner != -1 && owner != civ) return OrderRefusalReason.NoSite;

        ResearchViewForDecrees research = pol.DecreeResearch;
        bool unlocked = known.Deposit switch
        {
            DepositKind.CopperOre => research.Era(civ) >= 3,
            DepositKind.ArsenicalCopper => research.IsDone(civ, "MET-04"),
            DepositKind.Tin => research.IsDone(civ, "MET-05"),
            DepositKind.Gold or DepositKind.Silver => research.IsDone(civ, "MET-07"),
            DepositKind.Salt => research.IsDone(civ, "MIN-06"),
            DepositKind.Iron => research.IsDone(civ, "MET-08"),
            _ => false,
        };
        if (!unlocked || known.DepositDepth >= 2 && !research.IsDone(civ, "MIN-04")) return OrderRefusalReason.NoSite;

        // Free Folk are not represented in territorial ownership. Reject their recently sighted village radius
        // from belief evidence rather than consulting the live Tin Folk table.
        foreach (BeliefTinFolk folk in belief.TinFolkSeen)
            if (belief.Season - folk.LastSeenSeason is >= 0 and <= 2 &&
                HexGrid.Distance(folk.Tile, tile, pol.World.Width) <= 3) return OrderRefusalReason.NoSite;

        int nearest = -1, best = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            int distance = HexGrid.Distance(pol.SettlementTileOf(s), tile, pol.World.Width);
            if (distance < best) { best = distance; nearest = s; }
        }
        if (nearest < 0) return OrderRefusalReason.NoDonor;
        return best > pol.Economy.MiningReachFrom(nearest) ? OrderRefusalReason.OutOfReach : (OrderRefusalReason)0;
    }

    private static int NearestOwnTile(PoliticsSystem pol, int civ, int tile, out int distance)
    {
        int best = -1;
        distance = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            int d = HexGrid.Distance(pol.SettlementTileOf(s), tile, pol.World.Width);
            if (d < distance) { distance = d; best = pol.SettlementTileOf(s); }
        }
        return best;
    }

    /// <summary>The founding rules on what the civilization knows: own distances and donor exactly, others' villages as seen.</summary>
    internal static OrderRefusalReason FoundOwnCheck(PoliticsSystem pol, int civ, int tile, Belief? belief, out int people)
        => FoundOwnCheck(pol, civ, tile, belief, donorProtectionCache: null, out people);

    internal static OrderRefusalReason FoundOwnCheck(PoliticsSystem pol, int civ, int tile, Belief? belief,
        byte[]? donorProtectionCache, out int people)
    {
        people = 0;
        int width = pol.World.Width;
        if (unchecked((uint)tile) >= unchecked((uint)pol.World.TileCount)) return OrderRefusalReason.NoSite;
        WorldTile knownTile;
        bool tileKnown;
        if (belief is not null) tileKnown = belief.Map.TryTile(tile, out knownTile);
        else
        {
            tileKnown = pol.KnowsTile(civ, tile);
            knownTile = tileKnown ? pol.World.TileAt(tile) : default;
        }
        if (!tileKnown) return OrderRefusalReason.NoSite;
        if (!knownTile.IsLand) return OrderRefusalReason.NoSite;
        int r = ClaimRadius(pol, civ) + 1;
        int bestDonor = -1;
        Fix64 bestFill = Fix64.MinValue;
        bool inReach = false;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            int at = pol.SettlementTileOf(s), d = HexGrid.Distance(at, tile, width);
            bool own = pol.SettlementCivOf(s) == civ && !pol.IsFreeFolk(s);
            if (own)
            {
                if (pol.SettlementPopulationOf(s) <= 0) continue;
                if (d < r) return OrderRefusalReason.NoSite;
                if (d > pol.FoundReach(civ, bySea: false)) continue;
                inReach = true;
                if (pol.SettlementPopulationOf(s) < 20 || pol.Economy.SeasonsSinceFission(s) < 4) continue;
                if (belief is not null && ShouldProtectFoundDonor(pol, s, donorProtectionCache)) continue;
                Fix64 fill = pol.Economy.PopulationFill(s);
                if (fill >= Fix64.FromRatio(6, 10) && fill > bestFill) { bestFill = fill; bestDonor = s; }
            }
            else if (belief is null && pol.KnowsTile(civ, at) && d < 9) return OrderRefusalReason.NoSite;
        }
        if (belief is not null)
        {
            foreach (RivalBelief rival in belief.Rivals)
            {
                if (rival.Civ == civ) continue;
                foreach (BeliefSettlement b in rival.Settlements)
                    if (HexGrid.Distance(b.Tile, tile, width) < 9) return OrderRefusalReason.NoSite;
            }
            foreach (BeliefTinFolk folk in belief.TinFolkSeen)
                if (belief.Season - folk.LastSeenSeason is >= 0 and <= 2 && HexGrid.Distance(folk.Tile, tile, width) < 9)
                    return OrderRefusalReason.NoSite;
        }
        if (!inReach) return OrderRefusalReason.OutOfReach;
        if (bestDonor < 0) return OrderRefusalReason.NoDonor;
        people = (pol.SettlementPopulationOf(bestDonor) * 2 + 2) / 5;
        return people < 8 ? OrderRefusalReason.NoDonor : 0;
    }

    private static bool ShouldProtectFoundDonor(PoliticsSystem pol, int settlement, byte[]? cache)
    {
        if (cache is null || (uint)settlement >= (uint)cache.Length)
            return pol.ShouldProtectStrategistFoundDonor(settlement);
        byte cached = cache[settlement];
        if (cached == 0)
        {
            bool protect = pol.ShouldProtectStrategistFoundDonor(settlement);
            cache[settlement] = protect ? (byte)2 : (byte)1;
            return protect;
        }
        return cached == 2;
    }

    private static int UnknownTilesNear(PoliticsSystem pol, int civ, int center, int radius, Belief? belief)
    {
        int width = pol.World.Width, height = pol.World.Height, n = 0;
        HexGrid.Coordinates(center, width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
            for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(width - 1, cx + radius + 1); x++)
            {
                int t = y * width + x;
                if (HexGrid.Distance(center, t, width) > radius) continue;
                bool known = belief is not null ? belief.Map.IsExplored(t) || pol.IsRevealed(civ, t) : pol.KnowsTile(civ, t);
                if (!known) n++;
            }
        return n;
    }

    internal static (int Tiles, Fix64 Weight, Fix64 Dominion) FoundLandValue(PoliticsSystem pol, int civ, int center, int radius, Belief? belief)
    {
        int knownOwnedWeight = KnownOwnedHabitableWeight(pol, civ, belief);
        return FoundLandValue(pol, civ, center, radius, belief, knownOwnedWeight);
    }

    internal static int KnownOwnedHabitableWeight(PoliticsSystem pol, int civ, Belief? belief)
    {
        int knownOwnedWeight = 0;
        // Dominion is an estimated annual share from the known map only. Unseen cells are omitted from both sides.
        for (int t = 0; t < pol.World.TileCount; t++)
        {
            bool known = belief is not null ? belief.Map.TryTile(t, out _) : pol.KnowsTile(civ, t);
            if (!known) continue;
            var (owner, _) = belief is not null ? belief.Map.OwnerSeen(t) : (pol.Territory.OwnerOf(t), 0);
            if (owner != -1) knownOwnedWeight += KnownHabitableWeight(pol, civ, belief, t);
        }
        return knownOwnedWeight;
    }

    internal static (int Tiles, Fix64 Weight, Fix64 Dominion) FoundLandValue(PoliticsSystem pol, int civ, int center,
        int radius, Belief? belief, int knownOwnedWeight)
    {
        int width = pol.World.Width, height = pol.World.Height, tiles = 0, weight = 0;
        HexGrid.Coordinates(center, width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
        for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(width - 1, cx + radius + 1); x++)
        {
            int tile = y * width + x;
            if (HexGrid.Distance(center, tile, width) > radius) continue;
            WorldTile data = default;
            bool known = belief is not null ? belief.Map.TryTile(tile, out data) : pol.KnowsTile(civ, tile);
            if (known && belief is null) data = pol.World.TileAt(tile);
            if (!known || !data.IsLand) continue;
            var (owner, seenSeason) = belief is not null ? belief.Map.OwnerSeen(tile) : (pol.Territory.OwnerOf(tile), pol.Season);
            // Existing own claims are unavailable; known rival or free-folk claims are unavailable too. Unknown ownership
            // is treated as uncertain and receives half credit rather than consulting hidden live ownership.
            if (owner == civ || owner == -2) continue;
            int habitable = data.HabitableWeight;
            int credit = owner >= 0 ? 0 : seenSeason < 0 ? habitable / 2 : habitable;
            if (credit <= 0) continue;
            tiles++;
            weight += credit;
        }
        Fix64 expectedWeight = Fix64.FromRatio(weight, 100);
        Fix64 dominion = weight <= 0 ? Fix64.Zero : Fix64.FromRatio(100L * weight, Math.Max(1, knownOwnedWeight + weight));
        return (tiles, expectedWeight, dominion);
    }

    private static int KnownHabitableWeight(PoliticsSystem pol, int civ, Belief? belief, int tile)
    {
        if (belief is not null) return belief.Map.TryTile(tile, out WorldTile known) ? known.HabitableWeight : 0;
        return pol.KnowsTile(civ, tile) ? pol.World.TileAt(tile).HabitableWeight : 0;
    }

    private static Fix64 ExpeditionInformation(PoliticsSystem pol, int civ, int center, int radius, Belief? belief)
    {
        int width = pol.World.Width, height = pol.World.Height;
        Fix64 score = Fix64.Zero;
        HexGrid.Coordinates(center, width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
        for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(width - 1, cx + radius + 1); x++)
        {
            int tile = y * width + x;
            if (HexGrid.Distance(center, tile, width) > radius || IsKnown(tile)) continue;
            // Frontier cells reveal more than interior cells. A nearby *known* metal deposit adds modest strategic
            // information value; unseen deposits are never inspected or assumed to exist.
            bool frontier = false, metalContext = false;
            for (int yy = Math.Max(0, y - 1); yy <= Math.Min(height - 1, y + 1); yy++)
            for (int xx = Math.Max(0, x - 2); xx <= Math.Min(width - 1, x + 2); xx++)
            {
                int neighbor = yy * width + xx;
                if (HexGrid.Distance(tile, neighbor, width) > 2 || !IsKnown(neighbor)) continue;
                frontier = true;
                bool knownMetal = false;
                if (belief is not null && belief.Map.TryTile(neighbor, out WorldTile seen))
                    knownMetal = seen.Deposit is DepositKind.Tin or DepositKind.ArsenicalCopper or DepositKind.CopperOre;
                else if (belief is null && pol.KnowsTile(civ, neighbor))
                    knownMetal = pol.World.TileAt(neighbor).Deposit is DepositKind.Tin or DepositKind.ArsenicalCopper or DepositKind.CopperOre;
                if (knownMetal) metalContext = true;
            }
            score += Fix64.One + (frontier ? Fix64.FromRatio(1, 2) : Fix64.Zero) + (metalContext ? Fix64.FromRatio(1, 2) : Fix64.Zero);
        }
        return score;

        bool IsKnown(int tile) => belief is not null ? belief.Map.IsExplored(tile) || pol.IsRevealed(civ, tile) : pol.KnowsTile(civ, tile);
    }

    /// <summary>The host the civilization would raise (its own, exact) against the defenders it believes are there.</summary>
    private static OrderRefusalReason LevyCheck(PoliticsSystem pol, int civ, int target, Belief? belief, out int host, out int success)
    {
        host = 0;
        success = 0;
        if (unchecked((uint)target) >= unchecked((uint)pol.SettlementTotal)) return OrderRefusalReason.NoSite;
        int targetTile;
        Fix64 defence = Fix64.Zero;
        if (belief is not null)
        {
            // With an AI belief, the target's existence, location, population, owner and defenses all come from
            // its recent sighting record. Stable ids only identify the order target; they do not reveal live state.
            targetTile = -1;
            int owner = -1;
            bool knownTarget = false, tinFolk = false;
            foreach (BeliefTinFolk folk in belief.TinFolkSeen)
                if (folk.Settlement == target && belief.Season - folk.LastSeenSeason is >= 0 and <= 2)
                { targetTile = folk.Tile; knownTarget = tinFolk = true; break; }
            foreach (RivalBelief rival in belief.Rivals)
                foreach (BeliefSettlement site in rival.Settlements)
                    if (site.Settlement == target && belief.Season - site.LastSeenSeason is >= 0 and <= 2 && site.PopulationEstimate > 0)
                    {
                        targetTile = site.Tile; owner = rival.Civ; knownTarget = true;
                        defence = site.DefendingStrengthEstimate * (site.PalisadeSeen ? Fix64.FromInt(2) : site.ThornBarrierSeen ? Fix64.FromRatio(13, 10) : Fix64.One);
                    }
            if (!knownTarget) return OrderRefusalReason.OutOfReach;
            if (owner == civ) return OrderRefusalReason.NoSite;
            if (owner >= 0)
            {
                if (pol.Diplomacy.Allied(civ, owner) || pol.Diplomacy.InTruce(civ, owner, pol.Season) || pol.HasPact(civ, owner))
                    return OrderRefusalReason.Rejected;
            }
            // Tin Folk are authored as small, palisaded villages. This fixed conservative prior is independent
            // of their live population and lets the forecast represent the documented neutral target class.
            if (tinFolk) defence = Fix64.FromInt(6);
        }
        else
        {
            // A player preview has no saved rival belief. Only accept a recently observed target and use a fixed
            // defender prior; never inspect its current population, ownership, or defenders after it leaves sight.
            if (!pol.SightedRecently(civ, target)) return OrderRefusalReason.OutOfReach;
            targetTile = pol.SettlementTileOf(target);
            defence = Fix64.FromInt(12);
        }
        if (pol.HasActiveLevy(civ)) return OrderRefusalReason.Rejected;
        Fix64 strength = Fix64.Zero;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            int geometricDistance = HexGrid.Distance(pol.SettlementTileOf(s), targetTile, pol.World.Width);
            int seasons = Math.Max(1, (geometricDistance + 9) / 10);
            if (seasons > 2) continue;
            SettlementMilitary m = pol.MilitaryOf(s);
            int take = Math.Max(0, Math.Min(m.Fighters - 4, m.Warriors + (m.Fighters - m.Warriors) / 2));
            host += take;
            if (m.Fighters > 0) strength += m.AttackingStrength * take / m.Fighters;
        }
        if (host < 4) return OrderRefusalReason.NoDonor;
        // Believed defenders: use a sighted estimate, or a fixed prior. Never derive a fallback from hidden population.
        if (defence <= Fix64.Zero) defence = Fix64.FromInt(16);
        // A force at 1.5× the defence takes it (13 §8.4, F5); the card shows a ramp from 0.8× (none) to 1.5× (all).
        Fix64 ratio = strength / defence;
        success = Math.Clamp(((ratio - Fix64.FromRatio(8, 10)) * 1000 / Fix64.FromRatio(7, 10)).FloorToInt(), 0, 1000);
        return 0;
    }

    /// <summary>
    /// Whether the partner is likely to accept a goods compact: our side's value of the good exactly; theirs assumed at the base value
    /// (×0.5 for a Tin Folk village or a civilization we have seen working tin, when we buy tin), so "they lack tin" is a belief, not a read.
    /// </summary>
    private static int CompactAcceptance(PoliticsSystem pol, int civ, int target, int extra, Belief? belief, out Fix64 load, out OrderRefusalReason why)
    {
        load = Fix64.Zero;
        why = 0;
        bool folk = DecreeArgs.IsFolkParty(target);
        CompactVariant variant = DecreeArgs.Variant(extra);
        if (folk)
        {
            int f = DecreeArgs.FolkSettlement(target);
            if (belief is null)
            {
                if (!pol.KnowsFolk(civ, f)) { why = OrderRefusalReason.OutOfReach; return 0; }
            }
            else
            {
                bool known = false;
                foreach (BeliefTinFolk seen in belief.TinFolkSeen)
                    if (seen.Settlement == f) { known = true; break; }
                if (!known) { why = OrderRefusalReason.OutOfReach; return 0; }
            }
            if (variant == CompactVariant.Invite)
                // There is no persisted Folk-relations observation in MapKnowledge/Belief. Use a fixed cautious
                // prior in previews rather than reading the live hidden relation score.
                return 50;
        }
        else
        {
            if (unchecked((uint)target) >= unchecked((uint)pol.CivCount) || target == civ) { why = OrderRefusalReason.InvalidTarget; return 0; }
            int relations = belief is not null ? belief.Rivals[target].Relations : pol.Diplomacy.Relations(civ, target);
            bool contacted = belief is not null ? belief.Rivals[target].Contacted : pol.Diplomacy.Contacted(civ, target);
            if (!contacted) { why = OrderRefusalReason.OutOfReach; return 0; }
            if (relations < 0) { why = OrderRefusalReason.Rejected; return 0; }
            if (variant is CompactVariant.Pact or CompactVariant.Treaty) return relations >= 10 ? 900 : 600;
        }
        TradeGood good = DecreeArgs.Good(extra);
        if (good is TradeGood.None or >= TradeGood.Count) { why = OrderRefusalReason.InvalidTarget; return 0; }
        bool weSell = DecreeArgs.Sell(extra);
        int home = -1;
        Fix64 bestOwn = Fix64.MinValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            Fix64 v = pol.UnitValue(s, good);
            Fix64 score = weSell ? -v : v;
            if (score > bestOwn) { bestOwn = score; home = s; }
        }
        if (home < 0) { why = OrderRefusalReason.NoSite; return 0; }
        Fix64 ours = pol.UnitValue(home, good);
        Fix64 basis = good == TradeGood.Food ? Fix64.One : pol.Economy.GoodBarterValue(DecreeArgs.GoodId(good));
        Fix64 theirs = basis;
        if (!weSell && good == TradeGood.Tin && (folk || SeenWorkingTin(pol, civ, target, belief))) theirs = basis / 2;
        Fix64 buyer = weSell ? theirs : ours, seller = weSell ? ours : theirs;
        load = weSell ? pol.Surplus(home, good) : Fix64.Zero;
        Fix64 g = Fix64.FromRatio(11, 10);
        if (seller <= Fix64.Zero) return 0;
        Fix64 ratio = buyer / seller;
        if (ratio >= g * g * Fix64.FromInt(2)) return 900;
        if (ratio >= g * g) return 600;
        return 150;
    }

    private static bool SeenWorkingTin(PoliticsSystem pol, int civ, int other, Belief? belief)
    {
        if (belief is not null)
        {
            if (unchecked((uint)other) >= unchecked((uint)belief.Rivals.Length)) return false;
            RivalBelief rival = belief.Rivals[other];
            return rival.TinAccessSeen || (rival.SeenFlags & SeenFlag.TinMineCamp) != 0;
        }
        // Without a persisted player belief record there is no reliable way to know whether the current camp table
        // contains a newly built but unseen rival mine. Use the public base-value prior.
        return false;
    }
}
