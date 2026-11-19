using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Politics;

/// <summary>One person in a fight: who, and how much they count (docs/MVP/13 §7.1, before group multipliers).</summary>
public readonly record struct Fighter(long Id, Fix64 Strength)
{
    /// <summary>Individual multiplier for shield and chariot crew casualty reduction.</summary>
    public Fix64 CasualtiesTaken { get; init; } = Fix64.One;
    /// <summary>The shield part of <see cref="CasualtiesTaken"/> (0.7 for a shielded fighter), kept apart so bolts can pierce it (WAR-11).</summary>
    public Fix64 ShieldFactor { get; init; } = Fix64.One;
    /// <summary>A crossbow or longbow fighter (WAR-11): shields give the other side no help against these.</summary>
    public bool Bolt { get; init; }
}

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
    int MaxRounds, bool AttackerRangedFirst)
{
    /// <summary>Casualty factors from shields and chariot crews (docs/MVP/22 §7.1).</summary>
    public Fix64 AttackerCasualtiesTaken { get; init; } = Fix64.One;
    public Fix64 DefenderCasualtiesTaken { get; init; } = Fix64.One;
}

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

    /// <summary>Weapon and formation values from docs/MVP/22 §7.1.</summary>
    public static Fix64 E3WeaponStrength(string weapon) => weapon switch
    {
        "bare" => Fix64.FromRatio(6, 10), "club" => Fix64.One, "wood" => Fix64.FromRatio(13, 10),
        "stone" => Fix64.FromRatio(16, 10), "copper" => Fix64.FromRatio(18, 10),
        "arsenical" => Fix64.FromRatio(21, 10), "bronze" => Fix64.FromRatio(22, 10),
        "tin" => Fix64.FromRatio(23, 10), _ => throw new ArgumentOutOfRangeException(nameof(weapon))
    };

    /// <summary>Iron weapon strength (docs/MVP/26 §4, WAR-08): 2.4, and 3.0 for a steel edge, which is the 2.4 times 1.25 applied exactly once.</summary>
    public static Fix64 IronWeaponStrength(bool steelEdge) => steelEdge ? Fix64.FromRatio(30, 10) : Fix64.FromRatio(24, 10);

    /// <summary>A professional soldier's training (WAR-07): x1.5, replacing the warrior's x1.3 rather than stacking with it.</summary>
    public static readonly Fix64 ProfessionalTraining = Fix64.FromRatio(15, 10);

    public static Fix64 TrainedWarriorStrength(string weapon, bool chariotOnOpenTerrain = false)
    {
        Fix64 strength = E3WeaponStrength(weapon) * Fix64.FromRatio(13, 10);
        return chariotOnOpenTerrain ? strength * Fix64.FromInt(2) : strength;
    }

    /// <summary>WAR-11 crossbow and longbow weapon strength (docs/MVP/balance/tools/e5_combat_oracle.py): 2.0, used by a levy with no drilling (training 1.0).</summary>
    public static readonly Fix64 BoltStrength = Fix64.FromInt(2);

    public static Fix64 MilitiaStrength(string weapon) => E3WeaponStrength(weapon) * Fix64.FromRatio(6, 10);
    public static Fix64 ShieldCasualtyFactor(Fix64 shieldedShare)
        => Fix64.One - Fix64.FromRatio(3, 10) * Fix64.Clamp(shieldedShare, Fix64.Zero, Fix64.One);

    /// <summary>
    /// Whether a kit improves the expected casualty exchange against an equally armed, non-bolt foe.
    /// In one combat round, own losses are proportional to enemyStrength² / ownStrength and inflicted
    /// losses to ownStrength² / enemyStrength. Therefore a fighter at candidate strength C with own
    /// casualty factor q improves on baseline B exactly when C³ &gt; q·B³. Equal scores keep the baseline.
    /// </summary>
    public static bool ImprovesCombatExchange(Fix64 candidateStrength, Fix64 ownCasualtyFactor, Fix64 baselineStrength)
        => ImprovesCombatExchange(candidateStrength, ownCasualtyFactor, baselineStrength, Fix64.One);

    /// <summary>Compares two choices by their expected exchange score, strength³ divided by own casualty factor.</summary>
    public static bool ImprovesCombatExchange(Fix64 candidateStrength, Fix64 ownCasualtyFactor,
        Fix64 baselineStrength, Fix64 baselineCasualtyFactor)
    {
        if (candidateStrength <= Fix64.Zero || baselineStrength <= Fix64.Zero ||
            ownCasualtyFactor <= Fix64.Zero || baselineCasualtyFactor <= Fix64.Zero)
            return false;

        Fix64 candidateCube = candidateStrength * candidateStrength * candidateStrength;
        Fix64 baselineCube = baselineStrength * baselineStrength * baselineStrength;
        return candidateCube * baselineCasualtyFactor > baselineCube * ownCasualtyFactor;
    }

    public static readonly Fix64 ChariotCasualtyFactor = Fix64.Half;
    /// <summary>Factors multiply, so shielded chariot crew take 0.7 × 0.5 casualties (07d §6, F5).</summary>
    public static Fix64 E3CasualtyFactor(Fix64 shieldedShare, Fix64 chariotCrewShare = default)
        => ShieldCasualtyFactor(shieldedShare) * (chariotCrewShare.Raw > 0
            ? Fix64.One - Fix64.FromRatio(1, 2) * Fix64.Clamp(chariotCrewShare, Fix64.Zero, Fix64.One)
            : Fix64.One);

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
            Fix64 shot = Casualties(a.Strength * setup.AttackerMult, d.Strength * setup.DefenderMult * Fix64.Half)
                * d.CasualtiesTakenFrom(a) * setup.DefenderCasualtiesTaken;
            Hit(d, shot, ref rng, dead.DefenderDead, dead.DefenderWounded);
        }

        for (rounds = 1; rounds <= setup.MaxRounds; rounds++)
        {
            if (a.Empty || d.Empty)
            {
                // A ranged opening volley (or a defense with nobody mobilized) can end the fight before
                // the first exchange. The side still standing controls the field; only mutual absence
                // is a stand-off.
                if (a.Empty != d.Empty) winner = a.Empty ? BattleWinner.Defender : BattleWinner.Attacker;
                break;
            }
            Fix64 attack = a.Strength * setup.AttackerMult;
            Fix64 defend = d.Strength * setup.DefenderMult;
            if (setup.DefendersSurprised && rounds == 1) defend = defend * Fix64.Half;
            // Both sides take their casualties from the strengths at the start of the round.
            Fix64 onDefenders = Casualties(attack, defend) * d.CasualtiesTakenFrom(a) * setup.DefenderCasualtiesTaken;
            Fix64 onAttackers = Casualties(defend, attack) * a.CasualtiesTakenFrom(d) * setup.AttackerCasualtiesTaken;
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
        private Fix64 _bolt;

        public Side(IReadOnlyList<Fighter> fighters)
        {
            _fighters = fighters;
            _down = new bool[fighters.Count];
            Alive = fighters.Count;
            foreach (Fighter f in fighters) { _start += f.Strength; if (f.Bolt) _bolt += f.Strength; }
            Strength = _start;
        }

        public int Alive { get; private set; }
        public bool Empty => Alive == 0;
        public Fix64 Strength { get; private set; }
        public Fix64 CasualtiesTaken
        {
            get
            {
                if (Alive == 0) return Fix64.One;
                Fix64 factor = Fix64.Zero;
                for (int i = 0; i < _fighters.Count; i++) if (!_down[i]) factor += _fighters[i].CasualtiesTaken;
                return factor / Alive;
            }
        }

        /// <summary>
        /// Casualty factor against a foe: shields help less the larger the foe's share of strength that is crossbows and longbows (WAR-11).
        /// With no bolt fighters on the other side this is exactly <see cref="CasualtiesTaken"/>.
        /// </summary>
        public Fix64 CasualtiesTakenFrom(Side foe)
        {
            if (foe._bolt.Raw == 0 || foe.Strength.Raw <= 0 || Alive == 0) return CasualtiesTaken;
            Fix64 share = Fix64.Clamp(foe._bolt / foe.Strength, Fix64.Zero, Fix64.One);
            Fix64 factor = Fix64.Zero;
            for (int i = 0; i < _fighters.Count; i++)
            {
                if (_down[i]) continue;
                Fix64 taken = _fighters[i].CasualtiesTaken;
                Fix64 shield = _fighters[i].ShieldFactor;
                if (shield < Fix64.One && shield.Raw > 0) taken += (taken / shield - taken) * share;
                factor += taken;
            }
            return factor / Alive;
        }

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
            if (_fighters[index].Bolt) _bolt -= _fighters[index].Strength;
            return _fighters[index].Id;
        }
    }
}
