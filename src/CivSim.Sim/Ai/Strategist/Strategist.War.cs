using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    // ---- war options (14 §6.2, §6.3) --------------------------------------------------------

    private static readonly ulong OccupationReliefStream = Hash64.OfString("strategist.assault.occupation-relief");

    /// <summary>
    /// For every contacted rival, the best settlement to take: projected force ratio at arrival against its believed
    /// defenders and walls (≥ 1.5, ≥ 2.0 against a palisade), a Monte Carlo of the assault and occupation hold against
    /// relief, and the war's value in Legacy over the horizon, less its casualties and reputation cost.
    /// </summary>
    private void ReadWarOptions(StrategistView v, SeasonRead s)
    {
        int width = v.Belief.Map.Width;
        int trials = StrategistTuning.Trials(v.Profile);
        int ownCount = Math.Max(1, v.Own.Settlements.Count);
        int ownAvgPop = Math.Max(1, v.Own.Population / ownCount);
        // A settlement's worth of land: own Dominion a year ÷ own settlements (a fair share of the 100 a year if unknown yet).
        Fix64 perYear = _dominionRate * 4;
        if (perYear <= Fix64.Zero) perYear = StrategistTuning.DominionPerYear / Math.Max(2, _civs);
        Fix64 landPerSettlement = perYear / ownCount;

        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            RivalRead read = s.Rivals[o];
            PairView p = v.Own.Others[o];
            RivalMemory m = _memory[o];
            if (!read.Contacted || p.Allied) continue;
            WarOption? best = null;
            int considered = 0;
            foreach (Site site in read.Sites)
            {
                if (site.Stale > StrategistTuning.StaleSeasons) continue;
                if (++considered > StrategistTuning.RaidTargetsConsidered) break;
                WarOption option = EvaluateAssault(v, s, read, m, site, width, trials, landPerSettlement, ownAvgPop);
                option.TacticallyViable = option.Feasible;
                ApplyLaunchBlockers(v, s, read, option, width);
                if (best is null || (option.Feasible && !best.Feasible) ||
                    (option.Feasible == best.Feasible && option.TacticallyViable && !best.TacticallyViable) ||
                    (option.Feasible == best.Feasible && option.TacticallyViable == best.TacticallyViable && option.Value > best.Value))
                    best = option;
            }
            if (best is null) continue;
            // Windows: strike before the rival's walls arrive, or while it is in distress (14 §6.2).
            RivalBelief r = v.Belief.Rivals[o];
            Fix64 window = Fix64.Zero;
            if (Believed(r, _def02) && !Believed(r, _def03) && r.Era >= 2) window += Fix64.FromRatio(15, 100);
            if (m.FieldTrend > Fix64.Zero && read.Field > Fix64.Zero && m.FieldTrend * 8 > read.Field / 10) window += Fix64.FromRatio(10, 100);
            foreach (Site site in read.Sites) if (site.Distress) { window += Fix64.FromRatio(10, 100); break; }
            best.Window = window;
            read.War = best;
            if (s.BestWar is null || (best.Feasible && !s.BestWar.Feasible) ||
                (best.Feasible == s.BestWar.Feasible && best.Value > s.BestWar.Value)) s.BestWar = best;
        }
    }

    private void ApplyLaunchBlockers(StrategistView v, SeasonRead s, RivalRead read, WarOption option, int width)
    {
        if (!option.Feasible) return;
        PairView pair = v.Own.Others[read.Civ];
        // Truces are kept unless breaking pays by a wide margin (14 §7.2).
        if (pair.InTruce && !pair.AtWar && option.Value < StrategistTuning.PactBreakMargin * StrategistTuning.TruceBreakLegacy)
        {
            option.Feasible = false;
            option.Why = "a truce holds and breaking it would not pay";
            return;
        }
        // Never leave a settlement next to another known raider needlessly weak (14 §6.4).
        for (int x = 0; x < _civs; x++)
            if (x != v.Civ && x != read.Civ && s.Rivals[x].EffectiveThreat >= StrategistTuning.ThreatNoticeAt * 2)
            {
                option.Feasible = false;
                option.Why = $"civ {x} threatens us (threat {F2(s.Rivals[x].EffectiveThreat)})";
                return;
            }
        // Evaluate the counterattack against the home army left by this exact target's proposed campaign.
        IReadOnlyDictionary<int, int> departures = CampaignDepartures(v, option.Tile, width);
        (Fix64 reply, OwnSettlement? exposed) = CounterThreat(v, read, width, departures);
        if (exposed is not null)
        {
            option.Feasible = false;
            option.Why = $"civ {read.Civ}'s army ({F1(reply)}) could take our settlement {exposed.Index} after the campaign leaves";
            return;
        }
        // Declaring needs hostile Relations or the Raid posture (13 §8.1): without either, and without the Posture lever, no war.
        bool declarable = pair.AtWar || pair.Relations < Diplomacy.HostileBelow || v.Own.Posture == Posture.Raid || Done(v, _gov02);
        if (!declarable)
        {
            option.Feasible = false;
            option.Why = $"war would pay ({option.Why}) but Relations {pair.Relations} are not hostile and there is no Raid posture";
        }
    }

    /// <summary>
    /// The rival's believed field army (up to 60% of each settlement's fighters, never fewer than four left at home) and
    /// the weakest post-departure own settlement in its reach that it could storm at 1.5 ratio after nearby relief, or null.
    /// </summary>
    private static Dictionary<int, int> CampaignDepartures(StrategistView v, int targetTile, int width)
    {
        var departures = new Dictionary<int, int>();
        OwnSettlement? origin = BestOrigin(v, targetTile, width);
        if (origin is null) return departures;
        foreach (OwnSettlement settlement in v.Own.Settlements)
        {
            if (HexGrid.Distance(settlement.Tile, origin.Tile, width) > StrategistTuning.ArmyGatherRange) continue;
            int allowed = Math.Max(0, Math.Min(settlement.Fighters - 4, settlement.Fighters * 6 / 10));
            if (allowed > 0) departures[settlement.Index] = allowed;
        }
        return departures;
    }

    private static Dictionary<int, int> RaidDepartures(OwnSettlement origin, int partySize)
        => new() { [origin.Index] = Math.Min(Math.Max(0, origin.Fighters - 4), Math.Max(0, partySize)) };

    private static Fix64 ResidualDefense(OwnSettlement settlement, IReadOnlyDictionary<int, int> departures)
    {
        departures.TryGetValue(settlement.Index, out int leaving);
        if (settlement.Fighters <= 0 || leaving <= 0) return settlement.DefendingStrength;
        // OwnView exposes aggregate strength rather than individual fighter strengths. The proportional estimate is
        // deliberately simple and deterministic; using attacking strength keeps the estimate cautious when the
        // strongest campaign fighters leave first.
        Fix64 leavingStrength = settlement.AttackingStrength * Fix64.FromRatio(leaving, settlement.Fighters)
            * StrategistTuning.PartyPickBonus;
        return Fix64.Max(Fix64.Zero, settlement.DefendingStrength - leavingStrength);
    }

    private (Fix64 Army, OwnSettlement? Exposed) CounterThreat(StrategistView v, RivalRead read, int width,
        IReadOnlyDictionary<int, int> departures)
    {
        Fix64 army = Fix64.Zero;
        foreach (Site site in read.Sites)
            army += site.PerAttack * Math.Max(0, Math.Min(site.Fighters - 4, site.Fighters * 6 / 10));
        if (army <= Fix64.Zero) return (army, null);
        int range = StrategistTuning.RaidRangeReaches * Math.Max(6, read.Reach);
        OwnSettlement? weakest = null;
        Fix64 weakestDefense = Fix64.MaxValue;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            bool near = read.Sites.Count == 0;
            foreach (Site site in read.Sites) if (HexGrid.Distance(o.Tile, site.Tile, width) <= range) near = true;
            if (!near) continue;
            Fix64 residual = ResidualDefense(o, departures);
            Fix64 defense = residual * o.Defenses.Multiplier();
            if (army < defense * StrategistTuning.WarRatio * StrategistTuning.CounterMargin || defense >= weakestDefense) continue;
            // Sister settlements within relief range break an occupation (13 §8.2): they bring 60% of their fighters.
            Fix64 relief = Fix64.Zero;
            foreach (OwnSettlement sister in v.Own.Settlements)
            {
                if (ReferenceEquals(sister, o) || sister.Fighters <= 0 || HexGrid.Distance(sister.Tile, o.Tile, width) > StrategistTuning.ReliefRange) continue;
                departures.TryGetValue(sister.Index, out int sent);
                int remaining = Math.Max(0, sister.Fighters - sent);
                int available = Math.Max(0, Math.Min(remaining - 2, remaining * 6 / 10));
                relief += sister.AttackingStrength * available / sister.Fighters * StrategistTuning.PartyPickBonus;
            }
            if (relief >= army * StrategistTuning.ReliefCover) continue;
            weakestDefense = defense;
            weakest = o;
        }
        return (army, weakest);
    }

    private WarOption EvaluateAssault(StrategistView v, SeasonRead s, RivalRead read, RivalMemory m, Site site, int width, int trials,
        Fix64 landPerSettlement, int ownAvgPop)
    {
        var option = new WarOption { Civ = read.Civ, Settlement = site.Settlement, Tile = site.Tile, Palisade = site.Defenses.Palisade };
        option.Required = option.Palisade ? StrategistTuning.WarRatioPalisade : StrategistTuning.WarRatio;
        OwnSettlement? origin = BestOrigin(v, site.Tile, width);
        if (origin is null) { option.Why = "no settlement to march from"; return option; }
        option.Travel = HexGrid.Distance(origin.Tile, site.Tile, width);

        // The field army: up to 60% of the fighters of every own settlement within ten tiles of the origin, strongest first.
        _a.Clear();
        Fix64 force = Fix64.Zero;
        foreach (OwnSettlement o in v.Own.Settlements)
        {
            if (HexGrid.Distance(o.Tile, origin.Tile, width) > StrategistTuning.ArmyGatherRange || o.Fighters <= 0) continue;
            int allowed = Math.Max(0, Math.Min(o.Fighters - 4, o.Fighters * 6 / 10));
            Fix64 each = o.AttackingStrength / o.Fighters * StrategistTuning.PartyPickBonus;
            for (int i = 0; i < allowed; i++) { _a.Add(new Fighter(2_000_000 + _a.Count, each)); force += each; }
        }
        option.Attackers = _a.Count;
        if (_a.Count < 6) { option.Why = $"army of {_a.Count} too small"; return option; }

        // Nearby settlements do not join the initial assault defense; their relief forces fight during occupation below.
        Fix64 defense = site.DefendStrength * site.Defenses.Multiplier();
        // Projected at arrival: the rival's believed growth over the march (14 §6.2).
        int seasons = Math.Max(1, (option.Travel + PoliticsSystem.TilesPerSeason - 1) / PoliticsSystem.TilesPerSeason);
        Fix64 growth = Fix64.One;
        if (m.FieldTrend > Fix64.Zero && read.Field > Fix64.Zero) growth += m.FieldTrend * seasons / read.Field;
        option.Ratio = defense > Fix64.Zero ? force / (defense * growth) : Fix64.FromInt(10);
        if (option.Ratio < option.Required * Fix64.FromRatio(9, 10))
        {
            option.Why = $"force ratio {F2(option.Ratio)} < {F2(option.Required)}{(option.Palisade ? " (palisade)" : "")}";
            return option;
        }

        Uniform(_b, site.Fighters, site.PerDefend, 0);
        int taken = 0, deaths = 0, wounded = 0;
        ulong stream = Hash64.Combine(Hash64.Combine(AssaultStream, (ulong)(v.Civ * 64 + read.Civ)), (ulong)site.Settlement);
        stream = Hash64.Combine(stream, (ulong)v.Season);
        List<Fighter> reliefTemplate = BuildOccupationRelief(read.Sites, site, width);
        int occupationSeasons = Captures.OccupationSeasons(site.Population);
        for (int t = 0; t < trials; t++)
        {
            ulong assaultTrialStream = Hash64.Combine(stream, (ulong)t);
            BattleResult b = Captures.Assault(_a, _b, site.Defenses, childrenInside: true, v.MatchSeed, assaultTrialStream);
            deaths += b.AttackerDead.Count;
            wounded += b.AttackerWounded.Count;
            if (!Captures.CanHold(b)) continue;

            List<Fighter> holders = UnhurtFighters(_a, b.AttackerDead, b.AttackerWounded);
            List<Fighter> reliefForce = new(reliefTemplate);
            bool held = true;
            for (int step = 0; step < occupationSeasons; step++)
            {
                // AdvanceOccupations checks the surviving, unwounded holders before each season's relief fight.
                if (holders.Count < Captures.MinHolders) { held = false; break; }
                if (reliefForce.Count == 0) continue;

                ulong reliefStream = OccupationTrialStream(v.Civ, read.Civ, site.Settlement, v.Season, t, step);
                BattleResult reliefBattle = Captures.Assault(reliefForce, holders, Defenses.None, childrenInside: false,
                    v.MatchSeed, reliefStream);
                deaths += reliefBattle.DefenderDead.Count;
                wounded += reliefBattle.DefenderWounded.Count;
                if (reliefBattle.Winner == BattleWinner.Attacker) { held = false; break; }

                // The simulation excludes wounded fighters from the next season's occupation force. Carry forward
                // only unhurt survivors on both sides rather than granting a fresh relief army at each step.
                holders = UnhurtFighters(holders, reliefBattle.DefenderDead, reliefBattle.DefenderWounded);
                reliefForce = UnhurtFighters(reliefForce, reliefBattle.AttackerDead, reliefBattle.AttackerWounded);
            }
            if (held) taken++;
        }
        option.CaptureChance = Fix64.FromRatio(taken, trials);
        option.Deaths = Fix64.FromRatio(deaths, trials);
        option.Wounded = Fix64.FromRatio(wounded, trials);
        option.Casualties = option.Deaths + option.Wounded;

        Fix64 popFactor = Fix64.Clamp(Fix64.FromRatio(Math.Max(1, site.Population), ownAvgPop), Fix64.Half, Fix64.FromInt(2));
        Fix64 gain = option.CaptureChance * landPerSettlement * popFactor * s.HorizonYears;
        Fix64 lifeCost = option.Deaths + option.Wounded * StrategistTuning.WoundedLifeShare;
        Fix64 cost = lifeCost * s.LifeLegacy + (v.Own.Others[read.Civ].AtWar ? Fix64.Zero : StrategistTuning.WarReputationLegacy);
        if (v.Own.Others[read.Civ].InTruce) cost += StrategistTuning.TruceBreakLegacy;
        Fix64 closeness = Fix64.Max(Fix64.Half, Fix64.One - Fix64.FromRatio(option.Travel, 50));
        option.Value = (gain - cost) * closeness;

        // Supply (14 §6.3): no campaign into a winter the marching settlement cannot feed.
        int lands = (v.Season + seasons - 1) % 4;
        bool hungryWinter = lands >= 2 && origin.LateWinterForecast < StrategistTuning.WinterFoodFloor;
        option.OriginWinter = origin.LateWinterForecast;
        option.LandsSeason = lands;
        option.Feasible = option.Ratio >= option.Required && option.CaptureChance >= StrategistTuning.MinCaptureChance &&
                          option.Value > Fix64.Zero && !hungryWinter;
        option.Why = $"ratio {F2(option.Ratio)} (need {F2(option.Required)}{(option.Palisade ? ", palisade" : "")}), capture {F2(option.CaptureChance)}, " +
                     $"value {F1(option.Value)} Legacy (gain {F1(gain)}, cost {F1(cost)}, expected deaths {F2(option.Deaths)}, wounded {F2(option.Wounded)}){(hungryWinter ? ", winter food short" : "")}";
        return option;
    }

    private static List<Fighter> BuildOccupationRelief(IReadOnlyList<Site> sites, Site target, int width)
    {
        var relief = new List<Fighter>();
        foreach (Site other in sites)
        {
            if (ReferenceEquals(other, target) || HexGrid.Distance(other.Tile, target.Tile, width) > StrategistTuning.ReliefRange) continue;
            int allowed = Math.Max(0, Math.Min(other.Fighters - 2, other.Fighters * 6 / 10));
            long idBase = 3_000_000L + (long)other.Settlement * 10_000L;
            for (int i = 0; i < allowed; i++) relief.Add(new Fighter(idBase + i, other.PerAttack));
        }
        return relief;
    }

    private static List<Fighter> UnhurtFighters(IReadOnlyList<Fighter> fighters, List<long> dead, List<long> wounded)
    {
        var unhurt = new List<Fighter>(fighters.Count);
        foreach (Fighter fighter in fighters)
            if (!dead.Contains(fighter.Id) && !wounded.Contains(fighter.Id)) unhurt.Add(fighter);
        return unhurt;
    }

    private static ulong OccupationTrialStream(int attacker, int defender, int target, int season, int trial, int step)
    {
        ulong stream = Hash64.Combine(OccupationReliefStream, (ulong)(attacker * 64 + defender));
        stream = Hash64.Combine(stream, (ulong)target);
        stream = Hash64.Combine(stream, (ulong)season);
        stream = Hash64.Combine(stream, (ulong)trial);
        return Hash64.Combine(stream, (ulong)step);
    }

    // ---- peace (14 §6.3 stop rules, §7.2 symmetric acceptance) ------------------------------

    private void DecidePeace(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            RivalMemory m = _memory[o];
            if (!p.AtWar)
            {
                if (m.WarSince >= 0) ResetWar(m);
                continue;
            }
            if (m.WarSince < 0)
            {
                m.WarSince = v.Season;
                m.WarStartAdults = v.Own.Adults;
                m.WeDeclared = m.WarDeclaredSeason == v.Season;
            }
            // A taken settlement shows up among our own.
            if (m.LastAssaultTarget >= 0 && v.Own.SettlementAt(m.LastAssaultTarget) is not null)
            {
                m.WarCaptures++;
                m.LastCaptureSeason = v.Season;
                v.Note("Captured", m.LastAssaultTarget, o, Fix64.One, $"settlement {m.LastAssaultTarget} of civ {o} is ours");
                m.LastAssaultTarget = -1;
            }
            // Never quit in the middle of taking a settlement (the order would refuse anyway).
            if (v.Own.AssaultsOnTheRoad > 0 || v.Own.Occupations > 0) continue;

            WarOption? option = s.Rivals[o].War;
            bool winnable = option is { TacticallyViable: true } && option.Value > Fix64.Zero;
            string? why = null;
            bool lossesMount = m.WarStartAdults > 0 &&
                               Fix64.FromInt(v.Own.Adults) < Fix64.FromInt(m.WarStartAdults) * (Fix64.One - StrategistTuning.LossesMount);
            if (lossesMount) why = $"losses mount: adults {v.Own.Adults} from {m.WarStartAdults}";
            else if (m.WarCaptures > 0 && !winnable) why = $"objectives met: {m.WarCaptures} settlement(s) taken, no further target";
            else if (!winnable && !m.WeDeclared) why = $"civ {o} declared war and we cannot win it ({option?.Why ?? "no target seen"}); peace and a truce protect us";
            else if (!winnable) why = $"force ratio gone ({option?.Why ?? "no target seen"})";
            else if (v.Season - m.WarSince >= StrategistTuning.FruitlessWarSeasons && m.WarCaptures == 0 &&
                     option!.Ratio < StrategistTuning.PeaceRatio * option.Required)
                why = $"{v.Season - m.WarSince} seasons of war without a capture";
            if (why is null) continue;
            if (act.MakePeace(o))
            {
                v.Note("Peace", o, -1, option?.Ratio ?? Fix64.Zero, why);
                s.Respond(o, "peace made");
                ResetWar(m);
            }
        }
    }

    private static void ResetWar(RivalMemory m)
    {
        m.WarSince = -1;
        m.WeDeclared = false;
        m.WarCaptures = 0;
        m.LastAssaultTarget = -1;
    }

    // ---- declaring war and campaigning (14 §6.2, §6.3) --------------------------------------

    private void DecideWar(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            RivalMemory m = _memory[o];
            WarOption? option = s.Rivals[o].War;
            bool feasible = option is { Feasible: true } && option.Value > Fix64.Zero;

            if (p.AtWar)
            {
                if (!feasible || s.OrderedLaunch || v.Own.AssaultsOnTheRoad > 0 || v.Own.Occupations > 0) continue;
                if (v.Season - m.LastCaptureSeason < StrategistTuning.PacifySeasons) continue;
                Launch(v, s, act, m, option!);
                continue;
            }

            bool wants = _plan == GrandPlan.Conquer && _planTarget == o;
            if (!wants)
            {
                // Hostile neighbours get their war considered and the refusal explained (14 §11).
                if (p.Contacted && p.Relations < Diplomacy.HostileBelow && option is not null && v.Season - m.LastWarNote >= 4)
                {
                    m.LastWarNote = v.Season;
                    v.Note("WarRejected", option.Settlement, o, option.Ratio, feasible ? $"feasible but not worth the plan ({option.Why})" : option.Why);
                }
                continue;
            }
            if (!feasible)
            {
                if (option is not null && v.Season - m.LastWarNote >= 4)
                {
                    m.LastWarNote = v.Season;
                    v.Note("WarRejected", option.Settlement, o, option.Ratio, option.Why);
                }
                continue;
            }
            s.WarPostureWanted = true;
            // Declaring is allowed when Relations are hostile, under Raid posture, or for an ally (13 §8.1).
            bool allowed = p.Relations < Diplomacy.HostileBelow || v.Own.Posture == Posture.Raid;
            if (!allowed && Done(v, _gov02) && act.SetPosture(Posture.Raid))
            {
                v.Note("Posture", (int)Posture.Raid, (int)v.Own.Posture, option!.Ratio, $"{v.Own.Posture} -> Raid: to declare war on civ {o}");
                _lastPostureChange = v.Season;
                allowed = true;
            }
            if (!allowed)
            {
                if (v.Season - m.LastWarNote >= 4)
                {
                    m.LastWarNote = v.Season;
                    v.Note("WarRejected", option!.Settlement, o, option.Ratio, $"war would pay ({option.Why}) but Relations {p.Relations} are not hostile and there is no Raid posture");
                }
                continue;
            }
            if (!act.DeclareWar(o)) continue;
            m.WarDeclaredSeason = v.Season;
            m.WarSince = v.Season;
            m.WeDeclared = true;
            m.WarStartAdults = v.Own.Adults;
            m.WarCaptures = 0;
            v.Note("War", option!.Settlement, o, option.Ratio, $"war on civ {o}: {option.Why}");
            if (!s.OrderedLaunch) Launch(v, s, act, m, option);
        }
    }

    private static void Launch(StrategistView v, SeasonRead s, IStrategistActions act, RivalMemory m, WarOption option)
    {
        if (!act.Assault(option.Settlement)) return;
        s.OrderedLaunch = true;
        m.LastAssaultSeason = v.Season;
        m.LastAssaultTarget = option.Settlement;
        v.Note("Assault", option.Settlement, option.Civ, option.CaptureChance,
            $"p={F2(option.CaptureChance)} lw={F2(option.OriginWinter)} cold={(option.LandsSeason >= 2 ? 1 : 0)} assault on settlement {option.Settlement} of civ {option.Civ} with {option.Attackers}: {option.Why}");
    }
}
