using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

public sealed partial class Strategist
{
    // ---- war options (14 §6.2, §6.3) --------------------------------------------------------

    /// <summary>
    /// For every contacted rival, the best settlement to take: projected force ratio at arrival against its believed
    /// defenders, walls and relief (≥ 1.5, ≥ 2.0 against a palisade), a Monte Carlo of the assault and hold, and the
    /// war's value in Legacy: the Dominion the settlement brings over the horizon, less the lives and reputation it costs.
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
                if (best is null || (option.Feasible && !best.Feasible) ||
                    (option.Feasible == best.Feasible && option.Value > best.Value)) best = option;
            }
            if (best is null) continue;
            // Truces are kept unless breaking pays by a wide margin (14 §7.2).
            if (p.InTruce && !p.AtWar && best.Value < StrategistTuning.PactBreakMargin * StrategistTuning.TruceBreakLegacy)
            {
                best.Feasible = false;
                best.Why = "a truce holds and breaking it would not pay";
            }
            // Never leave a settlement next to another known raider needlessly weak (14 §6.4).
            for (int x = 0; x < _civs; x++)
                if (x != v.Civ && x != o && s.Rivals[x].EffectiveThreat >= StrategistTuning.ThreatNoticeAt * 2 && best.Feasible)
                {
                    best.Feasible = false;
                    best.Why = $"civ {x} threatens us (threat {F2(s.Rivals[x].EffectiveThreat)})";
                }
            // A war invites the reply in kind: their field army against our weakest settlement within their reach (they move
            // out at 1.5 times its defense, 13 §8.4). Never open a war that hands them one of ours (14 §6.4).
            (Fix64 reply, OwnSettlement? exposed) = CounterThreat(v, read, width);
            if (best.Feasible && exposed is not null)
            {
                best.Feasible = false;
                best.Why = $"civ {o}'s army ({F1(reply)}) could take our settlement {exposed.Index} in reply";
            }
            // Declaring needs hostile Relations or the Raid posture (13 §8.1): without either, and without the Posture lever, no war.
            bool declarable = p.AtWar || p.Relations < Diplomacy.HostileBelow || v.Own.Posture == Posture.Raid || Done(v, _gov02);
            if (!declarable && best.Feasible)
            {
                best.Feasible = false;
                best.Why = $"war would pay ({best.Why}) but Relations {p.Relations} are not hostile and there is no Raid posture";
            }
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

    /// <summary>
    /// The rival's believed field army (up to 60% of each settlement's fighters, never fewer than four left at home) and
    /// the weakest own settlement within its reach that it could storm at the 1.5 ratio, or null if none.
    /// </summary>
    private (Fix64 Army, OwnSettlement? Exposed) CounterThreat(StrategistView v, RivalRead read, int width)
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
            Fix64 defense = o.DefendingStrength * o.Defenses.Multiplier();
            if (defense < weakestDefense) { weakestDefense = defense; weakest = o; }
        }
        if (weakest is null || army < weakestDefense * StrategistTuning.WarRatio * StrategistTuning.CounterMargin) return (army, null);
        return (army, weakest);
    }

    private WarOption EvaluateAssault(StrategistView v, SeasonRead s, RivalRead read, RivalMemory m, Site site, int width, int trials,
        Fix64 landPerSettlement, int ownAvgPop)
    {
        var option = new WarOption { Civ = read.Civ, Settlement = site.Settlement, Palisade = site.Defenses.Palisade };
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

        Fix64 relief = Fix64.Zero;
        foreach (Site other in read.Sites)
            if (!ReferenceEquals(other, site) && HexGrid.Distance(other.Tile, site.Tile, width) <= StrategistTuning.ReliefRange)
                relief += other.PerAttack * other.Fighters * StrategistTuning.ReliefShare;
        Fix64 defense = site.DefendStrength * site.Defenses.Multiplier() + relief;
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
        int taken = 0, deaths = 0;
        ulong stream = Hash64.Combine(Hash64.Combine(AssaultStream, (ulong)(v.Civ * 64 + read.Civ)), (ulong)site.Settlement);
        stream = Hash64.Combine(stream, (ulong)v.Season);
        for (int t = 0; t < trials; t++)
        {
            BattleResult b = Captures.Assault(_a, _b, site.Defenses, childrenInside: true, v.MatchSeed, unchecked(stream + (ulong)t));
            if (Captures.CanHold(b)) taken++;
            deaths += b.AttackerDead.Count;
        }
        option.CaptureChance = Fix64.FromRatio(taken, trials);
        option.Deaths = Fix64.FromRatio(deaths, trials);

        Fix64 popFactor = Fix64.Clamp(Fix64.FromRatio(Math.Max(1, site.Population), ownAvgPop), Fix64.Half, Fix64.FromInt(2));
        Fix64 gain = option.CaptureChance * landPerSettlement * popFactor * s.HorizonYears;
        Fix64 cost = option.Deaths * s.LifeLegacy + (v.Own.Others[read.Civ].AtWar ? Fix64.Zero : StrategistTuning.WarReputationLegacy);
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
                     $"value {F1(option.Value)} Legacy (gain {F1(gain)}, cost {F1(cost)}){(hungryWinter ? ", winter food short" : "")}";
        return option;
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
            bool winnable = option is { Feasible: true } && option.Value > Fix64.Zero;
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
