using System;
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
    {
        ResearchViewForDecrees r = pol.DecreeResearch;
        int cost = pol.DecreeCost(civ, kind);
        int mandate = r.Mandate(civ);
        OrderRefusalReason why = 0;
        if (!r.MandateUnlocked(civ) || mandate < cost) why = OrderRefusalReason.NoMandate;
        int success = 1000, people = 0, tiles = 0, relations = 0, seasons = 1;
        Fix64 goods = Fix64.Zero;
        if (why == 0)
        {
            switch (kind)
            {
                case DecreeKind.Found:
                    why = FoundOwnCheck(pol, civ, target, belief, out people);
                    int radius = ClaimRadius(pol, civ);
                    tiles = 3 * radius * (radius + 1) + 1;
                    break;
                case DecreeKind.Mine:
                    why = pol.MineCheck(civ, target);
                    // A crew of 4 (6 with MIN-04) digs about a litre a work-season (1.5 with a shaft and bronze picks).
                    int crew = r.IsDone(civ, "MIN-04") ? 6 : 4;
                    goods = Fix64.FromInt(crew) * (r.IsDone(civ, "MIN-04") ? Fix64.FromRatio(3, 2) : Fix64.One);
                    break;
                case DecreeKind.Expedition:
                    if (unchecked((uint)target) >= unchecked((uint)pol.World.TileCount)) { why = OrderRefusalReason.NoSite; break; }
                    int nearest = NearestOwnTile(pol, civ, target, out int d);
                    if (nearest < 0) { why = OrderRefusalReason.NoDonor; break; }
                    if (d > pol.ExpeditionRange(civ) || !pol.CanReach(civ, nearest, target)) { why = OrderRefusalReason.OutOfReach; break; }
                    tiles = UnknownTilesNear(pol, civ, target, 5, belief);
                    seasons = 2 * Math.Max(1, pol.TravelSeasons(civ, nearest, target));
                    success = 1000 - pol.VoyageRiskPermille(civ, nearest, target);
                    break;
                case DecreeKind.Levy:
                    why = LevyCheck(pol, civ, target, belief, out people, out success);
                    seasons = 2;
                    break;
                case DecreeKind.Corvee:
                    bool own = unchecked((uint)target) < unchecked((uint)pol.SettlementTotal) && pol.SettlementCivOf(target) == civ && !pol.IsFreeFolk(target);
                    if (!own || !pol.Economy.HasCivicWork(target, (byte)Math.Clamp(extra, 0, 7))) why = OrderRefusalReason.NoSite;
                    seasons = 2;
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
            relations, seasons);
    }

    private static int ClaimRadius(PoliticsSystem pol, int civ) => pol.ClaimRadiusOfEra(pol.DecreeResearch.Era(civ));

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
    private static OrderRefusalReason FoundOwnCheck(PoliticsSystem pol, int civ, int tile, Belief? belief, out int people)
    {
        people = 0;
        int width = pol.World.Width;
        if (unchecked((uint)tile) >= unchecked((uint)pol.World.TileCount) || !pol.Economy.IsHabitableSite(tile)) return OrderRefusalReason.NoSite;
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
                Fix64 fill = pol.Economy.PopulationFill(s);
                if (fill >= Fix64.FromRatio(6, 10) && fill > bestFill) { bestFill = fill; bestDonor = s; }
            }
            else if (belief is null && pol.KnowsTile(civ, at) && d < 9) return OrderRefusalReason.NoSite;
        }
        if (belief is not null)
            foreach (RivalBelief rival in belief.Rivals)
            {
                if (rival.Civ == civ) continue;
                foreach (BeliefSettlement b in rival.Settlements)
                    if (HexGrid.Distance(b.Tile, tile, width) < 9) return OrderRefusalReason.NoSite;
            }
        if (!inReach) return OrderRefusalReason.OutOfReach;
        if (bestDonor < 0) return OrderRefusalReason.NoDonor;
        people = (pol.SettlementPopulationOf(bestDonor) * 2 + 2) / 5;
        return people < 8 ? OrderRefusalReason.NoDonor : 0;
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

    /// <summary>The host the civilization would raise (its own, exact) against the defenders it believes are there.</summary>
    private static OrderRefusalReason LevyCheck(PoliticsSystem pol, int civ, int target, Belief? belief, out int host, out int success)
    {
        host = 0;
        success = 0;
        if (unchecked((uint)target) >= unchecked((uint)pol.SettlementTotal) || pol.SettlementPopulationOf(target) <= 0) return OrderRefusalReason.NoSite;
        if (!pol.IsFreeFolk(target) && pol.SettlementCivOf(target) == civ) return OrderRefusalReason.NoSite;
        if (!pol.SightedRecently(civ, target)) return OrderRefusalReason.OutOfReach;
        Fix64 strength = Fix64.Zero;
        for (int s = 0; s < pol.SettlementTotal; s++)
        {
            if (pol.SettlementCivOf(s) != civ || pol.IsFreeFolk(s) || pol.SettlementPopulationOf(s) <= 0) continue;
            int seasons = pol.TravelSeasons(civ, pol.SettlementTileOf(s), pol.SettlementTileOf(target));
            if (seasons > 2) continue;
            SettlementMilitary m = pol.MilitaryOf(s);
            int take = Math.Max(0, Math.Min(m.Fighters - 4, m.Warriors + (m.Fighters - m.Warriors) / 2));
            host += take;
            if (m.Fighters > 0) strength += m.AttackingStrength * take / m.Fighters;
        }
        if (host < 4) return OrderRefusalReason.NoDonor;
        // Believed defenders: the belief's estimate, else a prior of a third of the people at stone-spear strength behind a palisade.
        Fix64 defence = Fix64.Zero;
        if (belief is not null)
            foreach (RivalBelief rival in belief.Rivals)
                foreach (BeliefSettlement b in rival.Settlements)
                    if (b.Settlement == target) defence = b.DefendingStrengthEstimate * (b.PalisadeSeen ? Fix64.FromInt(2) : b.ThornBarrierSeen ? Fix64.FromRatio(13, 10) : Fix64.One);
        if (defence <= Fix64.Zero) defence = Fix64.FromInt(Math.Max(4, pol.SettlementPopulationOf(target) / 3)) * Fix64.FromInt(2);
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
            if (!pol.KnowsFolk(civ, f)) { why = OrderRefusalReason.OutOfReach; return 0; }
            if (variant == CompactVariant.Invite)
                return pol.FolkRelations(civ, f) > 30 ? 800 : 50;
        }
        else
        {
            if (unchecked((uint)target) >= unchecked((uint)pol.CivCount) || target == civ) { why = OrderRefusalReason.InvalidTarget; return 0; }
            int relations = belief is not null ? belief.Rivals[target].Relations : pol.Diplomacy.Relations(civ, target);
            if (!pol.Diplomacy.Contacted(civ, target)) { why = OrderRefusalReason.OutOfReach; return 0; }
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
        if (!weSell && good == TradeGood.Tin && (folk || SeenWorkingTin(pol, civ, target))) theirs = basis / 2;
        Fix64 buyer = weSell ? theirs : ours, seller = weSell ? ours : theirs;
        load = weSell ? pol.Surplus(home, good) : Fix64.Zero;
        Fix64 g = Fix64.FromRatio(11, 10);
        if (seller <= Fix64.Zero) return 0;
        Fix64 ratio = buyer / seller;
        if (ratio >= g * g * Fix64.FromInt(2)) return 900;
        if (ratio >= g * g) return 600;
        return 150;
    }

    private static bool SeenWorkingTin(PoliticsSystem pol, int civ, int other)
    {
        for (int i = 0; i < pol.Economy.MiningCampCount; i++)
        {
            Economy.MiningCampView camp = pol.Economy.MiningCamp(i);
            if (camp.Civilization == other && camp.Deposit == DepositKind.Tin && pol.KnowsTile(civ, camp.Tile)) return true;
        }
        return false;
    }
}
