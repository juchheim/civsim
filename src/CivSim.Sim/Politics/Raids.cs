using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Politics;

/// <summary>What stands between a raider and the stores (docs/MVP/13 §5.3, §6.2).</summary>
public readonly record struct Defenses(
    bool ThornBarrier, bool Palisade, bool Watchers, bool Dogs, bool SignalFires, bool AlarmCalls, bool HillSite,
    bool Archery, bool Night, bool RaidedRecently, bool Hillfort = false, bool StoneWalls = false, bool HillfortRefuge = false,
    bool BronzeRules = false)
{
    public static readonly Defenses None = default;

    /// <summary>Product of the defense multipliers on defenders' strength (13 §6.2; Bronze barriers replace each other).</summary>
    public Fix64 Multiplier()
    {
        Fix64 m = Fix64.FromRatio(11, 10);
        bool modernBarrier = BronzeRules || Hillfort || StoneWalls;
        Fix64 barrier = Fix64.One;
        if (modernBarrier)
        {
            if (ThornBarrier) barrier = Fix64.FromRatio(13, 10);
            if (Palisade) barrier = Fix64.Max(barrier, Fix64.FromInt(2));
            if (Hillfort) barrier = Fix64.Max(barrier, Fix64.FromRatio(5, 2));
            if (StoneWalls) barrier = Fix64.Max(barrier, Fix64.FromInt(3));
        }
        else
        {
            // Preserve the E0–E2 combat model; Bronze barriers replace each other from KEY-3 onward.
            if (ThornBarrier) m = m * Fix64.FromRatio(13, 10);
            if (Palisade) m = m * Fix64.FromInt(2);
        }
        if (modernBarrier) m = m * barrier;
        if (HillSite) m = m * Fix64.FromRatio(12, 10);
        if (Archery && (modernBarrier ? barrier > Fix64.One : ThornBarrier || Palisade)) m = m * Fix64.FromRatio(5, 4);
        return m;
    }

    /// <summary>Hillforts and walls shelter raided stores; walls cannot be assaulted in E3.</summary>
    public Fix64 LootMultiplier() => Hillfort || StoneWalls || HillfortRefuge ? Fix64.FromRatio(1, 4) : Fix64.One;
    public bool CanBeAssaulted() => !StoneWalls;
}

/// <summary>How a raid went.</summary>
public sealed class RaidOutcome
{
    public bool Success { get; init; }
    public bool Surprised { get; init; }
    public BattleResult Battle { get; init; } = new();
    /// <summary>Rations of goods the surviving, unwounded raiders can carry away (zero on failure).</summary>
    public Fix64 LootCapacity { get; init; }
    /// <summary>Flammable structures burned (docs/MVP/13 §5.4).</summary>
    public int StructuresBurned { get; init; }
}

/// <summary>
/// Raid resolution (docs/MVP/13 §5): a surprise check inside three tiles, then rounds of combat. A raid succeeds when
/// the defenders break before the raiders do; raiders break at 30% losses before any loot (40% after), defenders
/// at 60% (children inside). No captives are taken.
/// </summary>
public static class Raids
{
    public static readonly Fix64 BaseSurprise = Fix64.FromRatio(60, 100);
    public static readonly Fix64 RaiderBreak = Fix64.FromRatio(30, 100);
    public static readonly Fix64 DefenderBreak = Fix64.FromRatio(40, 100);
    public static readonly Fix64 DefenderBreakWithChildren = Fix64.FromRatio(60, 100);
    public static readonly Fix64 CarryPerRaider = Fix64.FromInt(4);
    public const int MaxRounds = 15;

    /// <summary>Probability that the target is caught unready (13 §5.3): alarm calls ×0.8, dogs ×0.5, watchers ×0.3, night +15 points, a raid within a year −20.</summary>
    public static Fix64 SurpriseChance(in Defenses d)
    {
        Fix64 p = BaseSurprise;
        if (d.AlarmCalls) p = p * Fix64.FromRatio(8, 10);
        if (d.Dogs) p = p * Fix64.Half;
        if (d.Watchers) p = p * Fix64.FromRatio(3, 10);
        if (d.Night) p += Fix64.FromRatio(15, 100);
        if (d.RaidedRecently) p -= Fix64.FromRatio(20, 100);
        return Fix64.Clamp(p, Fix64.Zero, Fix64.FromRatio(95, 100));
    }

    /// <summary>
    /// Fraction of the defending adults who reach the fight (13 §6.1: adults within two tiles fight, the others run
    /// home): few when the raid catches the camp unready, most when it was warned. Fitted to the table of 13 §5.5.
    /// </summary>
    public static Fix64 MobilizedShare(bool surprised) => surprised ? SurprisedShare : AlertShare;

    public static readonly Fix64 SurprisedShare = Fix64.FromRatio(40, 100);
    public static readonly Fix64 AlertShare = Fix64.FromRatio(52, 100);
    /// <summary>Share of carried goods that reaches the home settlement (the other 20% is lost on the return journey).</summary>
    public static readonly Fix64 DeliveredLootShare = Fix64.FromRatio(8, 10);

    public static RaidOutcome Resolve(IReadOnlyList<Fighter> raiders, IReadOnlyList<Fighter> defenders, in Defenses defenses,
        bool raiderRanged, ulong seed, ulong stream)
    {
        Pcg32 rng = Pcg32.ForStream(seed, stream, 1, 0);
        bool surprised = rng.NextFix01() < SurpriseChance(defenses);

        // Only the defenders who reach the fight count; picked by lot so no one is always the one at home.
        Fix64 share = MobilizedShare(surprised);
        var fighting = new List<Fighter>(defenders.Count);
        foreach (Fighter f in defenders)
            if (share >= Fix64.One || rng.NextFix01() < share) fighting.Add(f);

        var setup = new BattleSetup(Fix64.One, defenses.Multiplier(), surprised, RaiderBreak, DefenderBreak,
            MaxRounds, raiderRanged);
        BattleResult battle = Combat.Fight(raiders, fighting, setup, seed, stream ^ 0x5241494455UL);
        bool success = battle.Winner == BattleWinner.Attacker;

        int burned = 0;
        if (success)
            // Each round of the raid the raiders burn one flammable structure with even odds (13 §5.4).
            for (int round = 0; round < battle.Rounds; round++)
                if (rng.NextBelow(2) == 0) burned++;
        int carriers = battle.AttackerSurvivors;
        return new RaidOutcome
        {
            Success = success,
            Surprised = surprised,
            Battle = battle,
            LootCapacity = success ? CarryPerRaider * carriers * defenses.LootMultiplier() : Fix64.Zero,
            StructuresBurned = Math.Min(burned, 3),
        };
    }

    /// <summary>
    /// Net value to the raider of an unprovoked raid, in rations (13 §11 F4): the loot, less a life at
    /// <paramref name="lifeValue"/> per raider dead, less the Relations and reputation cost of the raid.
    /// </summary>
    public static Fix64 NetValue(Fix64 successShare, Fix64 expectedLoot, Fix64 expectedRaiderDeaths, Fix64 lifeValue, Fix64 relationsCost)
        => successShare * expectedLoot - expectedRaiderDeaths * lifeValue - relationsCost;
}

/// <summary>
/// Capturing a settlement (docs/MVP/13 §8.2): a battle at the settlement; if its defenders break or die and the attacker
/// still has at least four fighters, the attacker holds it for one season (a camp or hamlet) or two (a village or
/// larger) and the settlement changes hands.
/// </summary>
public static class Captures
{
    public const int MinHolders = 4;

    public static BattleResult Assault(IReadOnlyList<Fighter> attackers, IReadOnlyList<Fighter> defenders, in Defenses defenses,
        bool childrenInside, ulong seed, ulong stream)
    {
        // E3 stone walls cannot be assaulted; callers use an investment/siege instead.
        if (!defenses.CanBeAssaulted()) return new BattleResult { Winner = BattleWinner.StandOff };
        var setup = new BattleSetup(Fix64.One, defenses.Multiplier(), DefendersSurprised: false,
            AttackerBreak: Fix64.FromRatio(4, 10), DefenderBreak: childrenInside ? Raids.DefenderBreakWithChildren : Fix64.FromRatio(4, 10),
            MaxRounds: Raids.MaxRounds, AttackerRangedFirst: false);
        return Combat.Fight(attackers, defenders, setup, seed, stream);
    }

    public static bool CanHold(BattleResult assault) => assault.Winner == BattleWinner.Attacker && assault.AttackerSurvivors >= MinHolders;

    /// <summary>Seasons the attacker must hold the settlement tile: one for a camp or hamlet, two for a village or larger.</summary>
    public static int OccupationSeasons(int population) => population <= 150 ? 1 : 2;
}

/// <summary>State and season step for a Bronze investment (docs/MVP/22 §7.4, 07d R5).</summary>
public readonly record struct SiegeState(Fix64 BesiegerStrength, Fix64 UnwalledDefenderStrength,
    Fix64 DefenderStrength, Fix64 StoresSeasons, int BesiegerFoodSeasons, int Season);

public enum SiegeResult : byte { Ongoing, Capitulated, Lifted, Sally }

public static class Sieges
{
    public static readonly Fix64 MinimumInvestmentRatio = Fix64.FromRatio(12, 10);
    public static readonly Fix64 WallDefenseMultiplier = Fix64.FromInt(3);
    public static readonly Fix64 RationNeed = Fix64.FromRatio(75, 100);
    public static readonly Fix64 LowStoresCapitulationChance = Fix64.FromRatio(35, 100);
    public static readonly Fix64 EmptyStoresCapitulationChance = Fix64.FromRatio(70, 100);
    public static readonly Fix64 DesertionPerUnsuppliedSeason = Fix64.FromRatio(10, 100);
    public static readonly int SupplyReachTiles = 8;

    /// <summary>Investment requires a host at least 1.2× the settlement's unwalled field strength.</summary>
    public static bool CanInvest(Fix64 besieger, Fix64 unwalledDefenders)
        => besieger >= unwalledDefenders * MinimumInvestmentRatio;

    /// <summary>
    /// Advance one season. The settlement rations at 75% of need; supplied besiegers retain strength, while an unsupplied
    /// host has one season of food then loses 10% each season. Return values distinguish a relief, sally, or capitulation.
    /// </summary>
    public static SiegeResult Advance(ref SiegeState state, bool supplied, ulong seed, ulong stream, ulong settlement = 0)
    {
        state = state with { Season = state.Season + 1, StoresSeasons = Fix64.Max(Fix64.Zero, state.StoresSeasons - RationNeed) };
        Fix64 besieger = state.BesiegerStrength;
        int food = state.BesiegerFoodSeasons;
        if (!supplied && food <= 0) besieger *= Fix64.One - DesertionPerUnsuppliedSeason;
        else if (!supplied) food--;

        state = state with { BesiegerStrength = besieger, BesiegerFoodSeasons = food };
        if (besieger < state.UnwalledDefenderStrength * Fix64.FromRatio(11, 10) * MinimumInvestmentRatio) return SiegeResult.Lifted;
        if (Fix64.FromRatio(11, 10) * state.UnwalledDefenderStrength > Fix64.FromRatio(8, 10) * besieger)
            return SiegeResult.Sally;

        Fix64 chance = state.StoresSeasons.Raw == 0 ? EmptyStoresCapitulationChance
            : state.StoresSeasons < Fix64.One ? LowStoresCapitulationChance : Fix64.Zero;
        if (chance.Raw > 0)
        {
            Pcg32 rng = Pcg32.ForStream(seed, stream, settlement, state.Season);
            if (rng.NextFix01() < chance) return SiegeResult.Capitulated;
        }
        return SiegeResult.Ongoing;
    }
}
