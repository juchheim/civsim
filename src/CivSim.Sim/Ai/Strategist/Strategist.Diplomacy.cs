using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    private readonly record struct TradeRoute(OwnSettlement Origin, int TargetSettlement, int Distance);

    /// <summary>
    /// Gifts, barter and alliances (14 §7): gift a stronger neighbour at first contact and keep Relations comfortable
    /// with anyone we do not mean to fight (appease a threatening one sooner); barter when both sides gain and Relations
    /// allow; ally when a third civilization threatens both of us, or against the leader. Never with the civilization
    /// the plan is about (Conquer X, Coalition against X).
    /// </summary>
    private void DecideDiplomacy(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        if (v.Own.Settlements.Count == 0) return;
        // The coalition's economic pressure is limited to tin and only targets the leader we currently
        // identify from explored territory. No foreign stock or trade inventory is consulted.
        int key3 = v.Catalog.IndexOf("KEY-3");
        bool leaderTinRouteKnown = _planTarget >= 0 && _planTarget < v.Belief.Rivals.Length &&
            (v.Belief.Rivals[_planTarget].TinAccessSeen || (v.Belief.Rivals[_planTarget].SeenFlags & SeenFlag.TinMineCamp) != 0);
        if (_plan == GrandPlan.CoalitionAgainstLeader && _planTarget >= 0 && _planTarget == s.Leader && leaderTinRouteKnown &&
            Done(v, key3) && v.Own.Others[_planTarget].Contacted && !v.Own.Others[_planTarget].TinEmbargoed &&
            act.SetEmbargo(_planTarget, TradeGood.Tin, on: true))
        {
            v.Note("Embargo", _planTarget, (int)TradeGood.Tin, Fix64.One,
                $"coalition against the forecast leader; tin embargo after KEY-3");
        }
        var gifts = new List<(int Civ, TradeRoute Route, string Reason, Fix64 Score)>();
        if (Done(v, _trd01))
        {
            for (int o = 0; o < _civs; o++)
            {
                if (o == v.Civ) continue;
                PairView p = v.Own.Others[o];
                RivalRead read = s.Rivals[o];
                RivalMemory m = _memory[o];
                if (!p.Contacted || p.AtWar) continue;
                bool hostileIntent = ((_plan is GrandPlan.Conquer or GrandPlan.CoalitionAgainstLeader) && _planTarget == o)
                                     || v.Season - m.LastRaidOrder < 8;
                bool embargo = s.Leader == o && _plan == GrandPlan.CoalitionAgainstLeader;
                if (hostileIntent || embargo) continue;

                TradeRoute? route = FindTradeRoute(v, read, StrategistTuning.SpareFood);
                if (route is null) continue;
                int since = v.Season - Math.Max(p.LastGiftSeason, m.LastGiftOrder);
                bool never = p.LastGiftSeason < 0 && m.LastGiftOrder < 0;
                bool stronger = read.Field > s.OwnField * StrategistTuning.StrongerBy;
                bool threatened = read.EffectiveThreat >= StrategistTuning.ThreatNoticeAt || m.ThreatSince >= 0;
                string? reason = null;
                Fix64 score = Fix64.Zero;

                if (threatened && p.Relations < 20 && since >= StrategistTuning.AppeaseIntervalSeasons)
                {
                    score = Fix64.FromInt(100) + read.EffectiveThreat * Fix64.FromInt(20) + Fix64.FromInt(20 - p.Relations);
                    reason = $"appease threat (threat {F2(read.EffectiveThreat)}, Relations {p.Relations})";
                }
                else if (AllianceReason(v, s, o) is not null && Done(v, _dip03) && Believed(v.Belief.Rivals[o], _dip03) && !p.Allied &&
                         p.Relations <= Diplomacy.AllianceAbove && since >= StrategistTuning.AppeaseIntervalSeasons)
                {
                    score = Fix64.FromInt(80 + Diplomacy.AllianceAbove - p.Relations);
                    reason = $"build toward alliance (Relations {p.Relations})";
                }
                else if (never && (stronger || _spec.Archetype != EconomyArchetype.Warlord))
                {
                    Fix64 imbalance = s.OwnField > Fix64.Zero ? Fix64.Max(Fix64.Zero, read.Field / s.OwnField - Fix64.One) : Fix64.Zero;
                    score = Fix64.FromInt(60) + Fix64.Min(Fix64.FromInt(20), imbalance * Fix64.FromInt(20));
                    reason = stronger ? $"first contact with stronger rival ({F1(read.Field)} vs {F1(s.OwnField)})" : "first contact with a peaceful neighbour";
                }
                else if (p.Relations < StrategistTuning.GiftKeepAbove && since >= StrategistTuning.GiftIntervalSeasons)
                {
                    score = Fix64.FromInt(40 + StrategistTuning.GiftKeepAbove - p.Relations);
                    reason = $"maintain relations (Relations {p.Relations})";
                }

                if (reason is null) continue;
                Fix64 distancePenalty = Fix64.Min(Fix64.FromInt(10), Fix64.FromRatio(route.Value.Distance, 2));
                score = Fix64.Max(Fix64.Zero, score - distancePenalty);
                gifts.Add((o, route.Value, reason, score));
            }
        }

        gifts.Sort((a, b) =>
        {
            int scoreOrder = b.Score.CompareTo(a.Score);
            return scoreOrder != 0 ? scoreOrder : a.Civ.CompareTo(b.Civ);
        });
        OwnSettlement? giftOrigin = null;
        foreach (var candidate in gifts)
        {
            if (!act.Gift(candidate.Civ, candidate.Route.TargetSettlement)) continue;
            giftOrigin = candidate.Route.Origin;
            RivalMemory m = _memory[candidate.Civ];
            m.LastGiftOrder = v.Season;
            RivalRead read = s.Rivals[candidate.Civ];
            bool threatened = read.EffectiveThreat >= StrategistTuning.ThreatNoticeAt || m.ThreatSince >= 0;
            v.Note("Gift", candidate.Civ, -1, candidate.Score,
                $"{candidate.Reason}; origin {candidate.Route.Origin.Index} has food {F1(candidate.Route.Origin.FoodStores)}, " +
                $"target {candidate.Route.TargetSettlement}, distance {candidate.Route.Distance}");
            if (threatened) s.Respond(candidate.Civ, "appeasement gift");
            break;
        }

        bool bartered = false;
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            RivalRead read = s.Rivals[o];
            RivalBelief r = v.Belief.Rivals[o];
            RivalMemory m = _memory[o];
            if (!p.Contacted || p.AtWar) continue;
            bool hostileIntent = ((_plan is GrandPlan.Conquer or GrandPlan.CoalitionAgainstLeader) && _planTarget == o)
                                 || v.Season - m.LastRaidOrder < 8;
            bool embargo = s.Leader == o && _plan == GrandPlan.CoalitionAgainstLeader;

            // Alliance (14 §7.2): a shared threat, or the leader, or a flank before a war elsewhere.
            if (!p.Allied && p.Relations > Diplomacy.AllianceAbove && Done(v, _dip03) && Believed(r, _dip03) && !hostileIntent && !embargo)
            {
                string? reason = AllianceReason(v, s, o);
                if (reason is not null && act.Ally(o))
                {
                    v.Note("Alliance", o, -1, Fix64.FromInt(p.Relations), reason);
                    for (int x = 0; x < _civs; x++) if (x != v.Civ && x != o && s.Rivals[x].EffectiveThreat >= StrategistTuning.SharedThreatAt) s.Respond(x, $"alliance with civ {o}");
                }
            }

            // Barter only when the nearby origin can spare a full cargo and the conservative trade estimate is positive.
            // A barter cargo is funded from the spare-food reserve. Keep the extra five-food
            // cushion below for a simultaneous gift, but do not add a second cargo margin here.
            Fix64 reserve = StrategistTuning.SpareFood;
            TradeRoute? tradeRoute = FindTradeRoute(v, s.Rivals[o], reserve);
            if (giftOrigin is not null && tradeRoute is { } currentRoute && currentRoute.Origin.Index == giftOrigin.Index &&
                currentRoute.Origin.FoodStores < reserve + Fix64.FromInt(5))
            {
                reserve += Fix64.FromInt(5);
                tradeRoute = FindTradeRoute(v, s.Rivals[o], reserve);
            }
            if (giftOrigin is not null && tradeRoute is { } finalRoute && finalRoute.Origin.Index == giftOrigin.Index) reserve += Fix64.FromInt(5);
            Fix64 barterScore = tradeRoute is null ? Fix64.Zero : EstimateBarterBenefit(p, tradeRoute.Value);
            if (!bartered && tradeRoute is not null && tradeRoute.Value.Origin.FoodStores >= reserve && barterScore > Fix64.Zero &&
                Done(v, _trd02) && Believed(r, _trd02) && p.Relations > 0 && !hostileIntent && !embargo &&
                v.Season - p.LastBarterSeason >= StrategistTuning.BarterIntervalSeasons)
            {
                if (act.Barter(o, tradeRoute.Value.TargetSettlement))
                {
                    bartered = true;
                    v.Note("Barter", o, -1, barterScore,
                        $"estimated benefit {F2(barterScore)}; origin {tradeRoute.Value.Origin.Index} has food " +
                        $"{F1(tradeRoute.Value.Origin.FoodStores)}, target {tradeRoute.Value.TargetSettlement}, Relations {p.Relations}");
                }
            }
        }
    }

    private static TradeRoute? FindTradeRoute(StrategistView v, RivalRead rival, Fix64 minFood)
    {
        TradeRoute? best = null;
        int bestDistance = int.MaxValue;
        int width = v.Belief.Map.Width;
        foreach (Site site in rival.Sites)
        {
            if (site.Stale > StrategistTuning.StaleSeasons) continue;
            OwnSettlement? origin = null;
            int originDistance = int.MaxValue;
            foreach (OwnSettlement own in v.Own.Settlements)
            {
                int distance = HexGrid.Distance(own.Tile, site.Tile, width);
                if (distance < originDistance || (distance == originDistance && (origin is null || own.Index < origin.Index)))
                {
                    origin = own;
                    originDistance = distance;
                }
            }
            // Gift and barter parties leave four fighters behind and need a four-cargo food reserve.
            if (origin is null || origin.Fighters < 6 || origin.FoodStores < minFood) continue;
            if (best is null || originDistance < bestDistance ||
                (originDistance == bestDistance && site.Settlement < best.Value.TargetSettlement))
            {
                best = new TradeRoute(origin, site.Settlement, originDistance);
                bestDistance = originDistance;
            }
        }
        return best;
    }

    private static Fix64 EstimateBarterBenefit(PairView pair, TradeRoute route)
    {
        // Nominal economic uplift assumes a full return cargo: both sides gain 10%. Add modest diplomatic value,
        // then charge for distance as time and party commitment; unseen knowledge is deliberately not assumed.
        Fix64 tradeUplift = Fix64.FromRatio(12, 10);
        Fix64 relationshipValue = pair.Relations < StrategistTuning.GiftKeepAbove ? Fix64.FromRatio(1, 2) : Fix64.FromRatio(1, 10);
        Fix64 travelCost = Fix64.FromRatio(route.Distance, 40);
        return tradeUplift + relationshipValue - travelCost;
    }

    private string? AllianceReason(StrategistView v, SeasonRead s, int partner)
    {
        int width = v.Belief.Map.Width;
        RivalRead mate = s.Rivals[partner];
        for (int x = 0; x < _civs; x++)
        {
            if (x == v.Civ || x == partner) continue;
            RivalRead threat = s.Rivals[x];
            if (s.Leader == x && _plan == GrandPlan.CoalitionAgainstLeader) return $"coalition against the leader, civ {x}";
            if (_plan == GrandPlan.Conquer && _planTarget == x) return $"secure the flank before the war on civ {x}";
            if (threat.EffectiveThreat < StrategistTuning.SharedThreatAt) continue;
            // Shared: the threat's settlements are within its raiding range of the partner's too (or we cannot tell).
            bool shared = mate.Sites.Count == 0 || threat.Sites.Count == 0;
            int range = StrategistTuning.RaidRangeReaches * Math.Max(6, threat.Reach);
            foreach (Site a in mate.Sites)
                foreach (Site b in threat.Sites)
                    if (HexGrid.Distance(a.Tile, b.Tile, width) <= range) shared = true;
            if (shared) return $"shared threat: civ {x} (threat {F2(threat.EffectiveThreat)}) menaces both";
        }
        return null;
    }

    // ---- Beacons (14 §4.5, §8.4) --------------------------------------------------------------

    /// <summary>
    /// Beacons go on the best unclaimed explored land 6-18 tiles out: good ground, strategic deposits, and the midline
    /// toward the believed rival (07b R9), but never where daughter bands may not settle (20 tiles from a rival settlement).
    /// A Beacon whose tile has been claimed is taken down; one older than 16 seasons is moved if a clearly better tile exists.
    /// </summary>
    private void DecideBeacons(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        int allowance = v.Own.BeaconAllowance;
        var beacons = v.Own.Beacons;
        while (_beaconSeasons.Count < beacons.Count) _beaconSeasons.Add(v.Season);
        while (_beaconSeasons.Count > beacons.Count) _beaconSeasons.RemoveAt(_beaconSeasons.Count - 1);
        if (allowance <= 0 && beacons.Count == 0) return;
        MapKnowledge map = v.Belief.Map;

        int bestTile = BestBeaconTile(v, s, -1, out Fix64 bestScore);
        // Take down stale ones, from the back so the indices hold.
        int count = beacons.Count;
        for (int i = count - 1; i >= 0; i--)
        {
            int tile = beacons[i];
            (int owner, int seen) = map.OwnerSeen(tile);
            string? why = null;
            if (owner >= 0 && seen >= _beaconSeasons[i]) why = owner == v.Civ ? "its land is ours now" : $"civ {owner} claimed it";
            else if (v.Season - _beaconSeasons[i] >= StrategistTuning.BeaconStaleSeasons && bestTile >= 0 &&
                     bestScore > BeaconScore(v, s, tile) * Fix64.FromRatio(12, 10)) why = "a clearly better site is known";
            else if (i >= allowance) why = "allowance shrank";
            if (why is null) continue;
            act.RemoveBeacon(i);
            v.Note("BeaconRemoved", tile, -1, Fix64.Zero, $"Beacon at {tile % map.Width},{tile / map.Width} taken down: {why}");
            _beaconSeasons.RemoveAt(i);
            count--;
        }
        while (count < allowance)
        {
            int tile = BestBeaconTile(v, s, count, out Fix64 score);
            if (tile < 0) break;
            if (!act.PlaceBeacon(tile)) break;
            _beaconSeasons.Add(v.Season);
            _placedThisSeason.Add(tile);
            count++;
            v.Note("Beacon", tile, -1, score, $"Beacon at {tile % map.Width},{tile / map.Width}: score {F2(score)}{_beaconWhy}");
        }
        _placedThisSeason.Clear();
    }

    private readonly System.Collections.Generic.List<int> _placedThisSeason = new();
    private string _beaconWhy = "";

    private int BestBeaconTile(StrategistView v, SeasonRead s, int placed, out Fix64 bestScore)
    {
        MapKnowledge map = v.Belief.Map;
        int best = -1;
        bestScore = Fix64.Zero;
        int r = StrategistTuning.BeaconMaxDistance;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            int ox = o.Tile % map.Width, oy = o.Tile / map.Width;
            for (int y = Math.Max(0, oy - r); y <= Math.Min(map.Height - 1, oy + r); y++)
                for (int x = Math.Max(0, ox - r - 1); x <= Math.Min(map.Width - 1, ox + r + 1); x++)
                {
                    int tile = y * map.Width + x;
                    Fix64 score = BeaconScore(v, s, tile);
                    if (score <= bestScore) continue;
                    bool taken = false;
                    foreach (int b in v.Own.Beacons) if (HexGrid.Distance(b, tile, map.Width) < 6) taken = true;
                    foreach (int b in _placedThisSeason) if (HexGrid.Distance(b, tile, map.Width) < 6) taken = true;
                    if (taken) continue;
                    bestScore = score;
                    best = tile;
                }
        }
        if (best >= 0) BeaconScore(v, s, best, explain: true);
        return best;
    }

    private Fix64 BeaconScore(StrategistView v, SeasonRead s, int tile, bool explain = false)
    {
        MapKnowledge map = v.Belief.Map;
        if (!map.TryTile(tile, out WorldTile t) || t.HabitableWeight == 0 || t.Biome is Biome.Mountain or Biome.Peak) return Fix64.Zero;
        if (map.OwnerSeen(tile).Owner >= 0) return Fix64.Zero;
        int width = map.Width;
        int ownD = int.MaxValue, ownTile = -1;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            int d = HexGrid.Distance(o.Tile, tile, width);
            if (d < ownD) { ownD = d; ownTile = o.Tile; }
        }
        if (ownD < StrategistTuning.BeaconMinDistance || ownD > StrategistTuning.BeaconMaxDistance) return Fix64.Zero;
        int rivalD = int.MaxValue, rivalTile = -1;
        for (int c = 0; c < _civs; c++)
        {
            if (c == v.Civ) continue;
            foreach (Site site in s.Rivals[c].Sites)
            {
                int d = HexGrid.Distance(site.Tile, tile, width);
                if (d < rivalD) { rivalD = d; rivalTile = site.Tile; }
            }
        }
        if (rivalD < StrategistTuning.BeaconRivalKeepOff) return Fix64.Zero;
        Fix64 quality = Fix64.FromRatio(t.ForageScore, 100) * t.HabitableWeight / 100;
        if ((t.Flags & (TileFlags.River | TileFlags.Lake)) != 0) quality += Fix64.FromRatio(30, 100);
        quality += Fix64.FromRatio(t.SoilFertility, 255) * Fix64.FromRatio(3, 10);
        // Prefer a comfortable fission distance (10-14 tiles) over the edges of the band.
        int off = Math.Abs(ownD - 12);
        Fix64 fit = Fix64.One - Fix64.FromRatio(off, 24);
        Fix64 frontier = Fix64.Zero;
        if (rivalTile >= 0 && ownTile >= 0)
        {
            int span = HexGrid.Distance(ownTile, rivalTile, width);
            if (span > 0 && rivalD < span)
                frontier = StrategistTuning.BeaconMidlineBonus * Fix64.Clamp(Fix64.FromRatio(2 * (span - rivalD), span), Fix64.Zero, Fix64.One);
        }
        Fix64 deposit = t.Deposit is World.DepositKind.Tin or World.DepositKind.NativeCopper or World.DepositKind.CopperOre
            or World.DepositKind.Coal or World.DepositKind.Salt or World.DepositKind.Flint
            ? StrategistTuning.BeaconDepositBonus : Fix64.Zero;
        // The E3 Beacon bonus is about securing a source, not owning the MET-05 project while it is still dormant.
        Fix64 tinRoute = BronzeTinBeaconBonus(Done(v, _met03), v.Own.TinAccess, t.Deposit);
        deposit += tinRoute;
        Fix64 score = quality * fit * (Fix64.One + frontier) + deposit;
        if (explain)
            _beaconWhy = $" (ground {F2(quality)}, {ownD} tiles out{(frontier > Fix64.Zero ? $", toward civ's frontier +{F2(frontier)}" : "")}{(deposit > Fix64.Zero ? $", {t.Deposit} deposit" : "")}{(tinRoute > Fix64.Zero ? ", securing tin" : "")})";
        return score;
    }

    internal static Fix64 BronzeTinBeaconBonus(bool hasBronzeTools, bool hasTinBronze, World.DepositKind deposit)
        => hasBronzeTools && !hasTinBronze && deposit == World.DepositKind.Tin
            ? StrategistTuning.BeaconTinRouteBonus : Fix64.Zero;
}
