using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    /// <summary>
    /// Gifts, barter and alliances (14 §7): gift a stronger neighbour at first contact and keep Relations comfortable
    /// with anyone we do not mean to fight (appease a threatening one sooner); barter when both sides gain and Relations
    /// allow; ally when a third civilization threatens both of us, or against the leader. Never with the civilization
    /// the plan is about (Conquer X, Coalition against X).
    /// </summary>
    private void DecideDiplomacy(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        if (v.Own.Settlements.Count == 0) return;
        Fix64 stores = Fix64.Zero;
        foreach (OwnSettlement o in v.Own.Settlements) stores = Fix64.Max(stores, o.FoodStores);
        bool spare = stores >= StrategistTuning.SpareFood;
        bool gifted = false;
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

            // Gifts (14 §7.1, 13 §4.1).
            if (!gifted && spare && Done(v, _trd01) && !hostileIntent && !embargo)
            {
                int since = v.Season - Math.Max(p.LastGiftSeason, m.LastGiftOrder);
                bool never = p.LastGiftSeason < 0 && m.LastGiftOrder < 0;
                bool stronger = read.Field > s.OwnField * StrategistTuning.StrongerBy;
                bool friendlyArchetype = _spec.Archetype != Economy.EconomyArchetype.Warlord;
                bool threatened = read.EffectiveThreat >= StrategistTuning.ThreatNoticeAt || m.ThreatSince >= 0;
                string? why = null;
                if (never && (stronger || friendlyArchetype))
                    why = stronger ? $"first contact: civ {o} is believed stronger ({F1(read.Field)} vs {F1(s.OwnField)})" : "first contact: a peaceful neighbour is worth more than prey";
                else if (threatened && p.Relations < 20 && since >= StrategistTuning.AppeaseIntervalSeasons)
                    why = $"appease a threat (threat {F2(read.EffectiveThreat)}, Relations {p.Relations})";
                else if (p.Relations < StrategistTuning.GiftKeepAbove && since >= StrategistTuning.GiftIntervalSeasons)
                    why = $"keep Relations up ({p.Relations})";
                else if (AllianceReason(v, s, o) is not null && Done(v, _dip03) && Believed(r, _dip03) && !p.Allied &&
                         p.Relations <= Diplomacy.AllianceAbove && since >= StrategistTuning.AppeaseIntervalSeasons)
                    why = $"raise Relations ({p.Relations}) toward an alliance";
                if (why is not null && act.Gift(o))
                {
                    gifted = true;
                    m.LastGiftOrder = v.Season;
                    v.Note("Gift", o, -1, Fix64.FromInt(p.Relations), why);
                    if (threatened) s.Respond(o, "appeasement gift");
                }
            }

            // Barter (13 §4.2): both sides gain 10%, and traded materials pass [Seen] knowledge both ways.
            if (spare && Done(v, _trd02) && Believed(r, _trd02) && p.Relations > 0 && !hostileIntent && !embargo &&
                v.Season - p.LastBarterSeason >= StrategistTuning.BarterIntervalSeasons)
            {
                if (act.Barter(o)) v.Note("Barter", o, -1, Fix64.FromInt(p.Relations), $"barter with civ {o}: both gain, Relations {p.Relations}");
            }
        }
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
        Fix64 score = quality * fit * (Fix64.One + frontier) + deposit;
        if (explain)
            _beaconWhy = $" (ground {F2(quality)}, {ownD} tiles out{(frontier > Fix64.Zero ? $", toward civ's frontier +{F2(frontier)}" : "")}{(deposit > Fix64.Zero ? $", {t.Deposit} deposit" : "")})";
        return score;
    }
}
