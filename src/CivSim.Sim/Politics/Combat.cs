using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Politics;

/// <summary>One person in a fight: who, and how much they count (docs/MVP/13 §7.1, before group multipliers).</summary>
public readonly record struct Fighter(long Id, Fix64 Strength);

/// <summary>The conditions of one engagement.</summary>
/// <param name="AttackerMult">Weapon, training and terrain already folded into each <see cref="Fighter"/>; this is a group multiplier.</param>
/// <param name="DefenderMult">Defense multipliers stacked (thorn barrier, palisade, site, home ground: docs/MVP/13 §6.2).</param>
/// <param name="DefendersSurprised">Surprised defenders fight at half strength in the first round.</param>
/// <param name="AttackerBreak">Share of starting strength lost at which the attackers break and retreat.</param>
/// <param name="DefenderBreak">Same for defenders: 40%, or 60% with children inside.</param>
/// <param name="MaxRounds">Rounds before the fight is called a stand-off (a round is 10 ticks; a season has 15).</param>
/// <param name="AttackerRangedFirst">The attackers shoot first, unanswered, for one round.</param>
public readonly record struct BattleSetup(
    Fix64 AttackerMult, Fix64 DefenderMult, bool DefendersSurprised, Fix64 AttackerBreak, Fix64 DefenderBreak,
    int MaxRounds, bool AttackerRangedFirst);

public enum BattleWinner : byte { Attacker, Defender, StandOff }

/// <summary>The people who died or were wounded on each side, in the order they fell.</summary>
public sealed class BattleResult
{
    public BattleWinner Winner { get; init; }
    public int Rounds { get; init; }
    public List<long> AttackerDead { get; } = new();
    public List<long> AttackerWounded { get; } = new();
    public List<long> DefenderDead { get; } = new();
    public List<long> DefenderWounded { get; } = new();
    public int AttackerSurvivors { get; set; }
    public int DefenderSurvivors { get; set; }
}

/// <summary>
/// Combat resolution for E0 to E2 (docs/MVP/13 §7): every 10 ticks opposing groups fight a round in which each
/// side's casualties are <c>k × (enemy strength)² / (own strength)</c>, 40% of them deaths and 60% wounds, until
/// one side breaks. Casualties fall on random members, so the economy can remove real people.
/// </summary>
public static class Combat
{
    /// <summary>Casualties per round, k in 13 §7.2.</summary>
    public static readonly Fix64 K = Fix64.FromRatio(6, 100);
    public static readonly Fix64 DeathShare = Fix64.FromRatio(4, 10);

    public static BattleResult Fight(IReadOnlyList<Fighter> attackers, IReadOnlyList<Fighter> defenders, in BattleSetup setup, ulong seed, ulong stream)
    {
        var a = new Side(attackers);
        var d = new Side(defenders);
        Pcg32 rng = Pcg32.ForStream(seed, stream, 0, 0);
        var dead = new BattleResultBuilder();
        int rounds = 0;
        BattleWinner winner = BattleWinner.StandOff;

        if (setup.AttackerRangedFirst && !a.Empty && !d.Empty)
        {
            Fix64 shot = Casualties(a.Strength * setup.AttackerMult, d.Strength * setup.DefenderMult * Fix64.Half);
            Hit(d, shot, ref rng, dead.DefenderDead, dead.DefenderWounded);
        }

        for (rounds = 1; rounds <= setup.MaxRounds; rounds++)
        {
            if (a.Empty || d.Empty) break;
            Fix64 attack = a.Strength * setup.AttackerMult;
            Fix64 defend = d.Strength * setup.DefenderMult;
            if (setup.DefendersSurprised && rounds == 1) defend = defend * Fix64.Half;
            // Both sides take their casualties from the strengths at the start of the round.
            Fix64 onDefenders = Casualties(attack, defend);
            Fix64 onAttackers = Casualties(defend, attack);
            Hit(d, onDefenders, ref rng, dead.DefenderDead, dead.DefenderWounded);
            Hit(a, onAttackers, ref rng, dead.AttackerDead, dead.AttackerWounded);
            bool defendersBroke = d.Empty || d.Lost >= setup.DefenderBreak;
            bool attackersBroke = a.Empty || a.Lost >= setup.AttackerBreak;
            if (defendersBroke && !attackersBroke) { winner = BattleWinner.Attacker; break; }
            if (attackersBroke) { winner = BattleWinner.Defender; break; }
        }

        var result = new BattleResult { Winner = winner, Rounds = Math.Min(rounds, setup.MaxRounds) };
        result.AttackerDead.AddRange(dead.AttackerDead);
        result.AttackerWounded.AddRange(dead.AttackerWounded);
        result.DefenderDead.AddRange(dead.DefenderDead);
        result.DefenderWounded.AddRange(dead.DefenderWounded);
        result.AttackerSurvivors = a.Alive;
        result.DefenderSurvivors = d.Alive;
        return result;
    }

    /// <summary>Expected figures lost by a side of strength <paramref name="own"/> against <paramref name="enemy"/> in one round.</summary>
    public static Fix64 Casualties(Fix64 enemy, Fix64 own)
        => own.Raw <= 0 ? Fix64.FromInt(1000) : K * enemy * enemy / own;

    private static void Hit(Side side, Fix64 expected, ref Pcg32 rng, List<long> dead, List<long> wounded)
    {
        int whole = expected.FloorToInt();
        Fix64 fraction = expected - Fix64.FromInt(whole);
        int count = whole + (rng.NextFix01() < fraction ? 1 : 0);
        for (int i = 0; i < count && side.Alive > 0; i++)
        {
            int victim = side.PickAlive((int)rng.NextBelow((uint)side.Alive));
            long id = side.Fall(victim);
            if (rng.NextFix01() < DeathShare) dead.Add(id);
            else wounded.Add(id);
        }
    }

    private sealed class BattleResultBuilder
    {
        public readonly List<long> AttackerDead = new(), AttackerWounded = new(), DefenderDead = new(), DefenderWounded = new();
    }

    private sealed class Side
    {
        private readonly IReadOnlyList<Fighter> _fighters;
        private readonly bool[] _down;
        private readonly Fix64 _start;

        public Side(IReadOnlyList<Fighter> fighters)
        {
            _fighters = fighters;
            _down = new bool[fighters.Count];
            Alive = fighters.Count;
            foreach (Fighter f in fighters) _start += f.Strength;
            Strength = _start;
        }

        public int Alive { get; private set; }
        public bool Empty => Alive == 0;
        public Fix64 Strength { get; private set; }
        public Fix64 Lost => _start.Raw == 0 ? Fix64.One : (_start - Strength) / _start;

        /// <summary>The n-th fighter still standing, in the order they were listed.</summary>
        public int PickAlive(int n)
        {
            for (int i = 0; i < _fighters.Count; i++)
                if (!_down[i] && n-- == 0) return i;
            throw new InvalidOperationException("No such fighter.");
        }

        public long Fall(int index)
        {
            _down[index] = true;
            Alive--;
            Strength -= _fighters[index].Strength;
            return _fighters[index].Id;
        }
    }
}
