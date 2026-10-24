using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;

namespace CivSim.Tests;

public class CombatTests
{
    private static void Same(Fix64 expected, Fix64 actual) => Assert.True(Math.Abs(expected.Raw - actual.Raw) < 1000, $"expected {expected}, got {actual}");

    private static List<Fighter> Group(int n, string strength, long firstId = 0)
        => Enumerable.Range(0, n).Select(i => new Fighter(firstId + i, Fix64.Parse(strength))).ToList();

    private static BattleSetup Open(int rounds = 15) => new(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10), rounds, false);

    [Fact]
    public void Casualties_follow_the_square_law_with_k_0_06()
    {
        // Strength 10 against 10: 0.06 x 100 / 10 = 0.6 figures a round each way.
        Same(Fix64.FromRatio(6, 10), Combat.Casualties(Fix64.FromInt(10), Fix64.FromInt(10)));
        // Doubling the enemy quadruples what you lose; doubling your own halves it.
        Same(Combat.Casualties(Fix64.FromInt(10), Fix64.FromInt(10)) * 4, Combat.Casualties(Fix64.FromInt(20), Fix64.FromInt(10)));
        Same(Combat.Casualties(Fix64.FromInt(10), Fix64.FromInt(10)) / 2, Combat.Casualties(Fix64.FromInt(10), Fix64.FromInt(20)));
    }

    [Fact]
    public void The_stronger_side_wins_and_a_fight_is_deterministic()
    {
        List<Fighter> strong = Group(12, "1.5"), weak = Group(12, "0.6", 100);
        BattleResult a = Combat.Fight(strong, weak, Open(), 7, 1), b = Combat.Fight(strong, weak, Open(), 7, 1);
        Assert.Equal(BattleWinner.Attacker, a.Winner);
        Assert.Equal(a.DefenderDead, b.DefenderDead);
        Assert.Equal(a.AttackerWounded, b.AttackerWounded);
        Assert.True(a.DefenderDead.Count + a.DefenderWounded.Count > a.AttackerDead.Count + a.AttackerWounded.Count);
        Assert.Equal(BattleWinner.Defender, Combat.Fight(weak, strong, Open(), 7, 1).Winner);
    }

    [Fact]
    public void Casualties_are_forty_percent_deaths_and_sixty_percent_wounds_and_fall_on_real_people()
    {
        int dead = 0, hurt = 0;
        for (ulong s = 0; s < 400; s++)
        {
            BattleResult r = Combat.Fight(Group(10, "1.0"), Group(10, "1.0", 100), Open(), 3, s);
            dead += r.AttackerDead.Count + r.DefenderDead.Count;
            hurt += r.AttackerWounded.Count + r.DefenderWounded.Count;
            var ids = r.AttackerDead.Concat(r.AttackerWounded).Concat(r.DefenderDead).Concat(r.DefenderWounded).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count()); // nobody falls twice
        }
        double share = (double)dead / (dead + hurt);
        Assert.InRange(share, 0.36, 0.44);
    }

    [Fact]
    public void A_side_breaks_at_its_threshold_and_a_surprised_defence_fights_at_half_strength_at_first()
    {
        // Attackers break at 30% lost: with equal numbers they are the ones to leave the field first against a 2x defence.
        var setup = new BattleSetup(Fix64.One, Fix64.FromInt(2), false, Fix64.FromRatio(3, 10), Fix64.FromRatio(6, 10), 15, false);
        Assert.Equal(BattleWinner.Defender, Combat.Fight(Group(10, "1.0"), Group(10, "1.0", 100), setup, 5, 5).Winner);

        int surprisedWins = 0, alertWins = 0;
        for (ulong s = 0; s < 300; s++)
        {
            var alert = new BattleSetup(Fix64.One, Fix64.One, false, Fix64.FromRatio(3, 10), Fix64.FromRatio(4, 10), 15, false);
            var surprised = alert with { DefendersSurprised = true };
            if (Combat.Fight(Group(8, "1.0"), Group(8, "1.0", 100), surprised, 9, s).Winner == BattleWinner.Attacker) surprisedWins++;
            if (Combat.Fight(Group(8, "1.0"), Group(8, "1.0", 100), alert, 9, s).Winner == BattleWinner.Attacker) alertWins++;
        }
        Assert.True(surprisedWins > alertWins + 30, $"surprised {surprisedWins} alert {alertWins}");
    }

    [Fact]
    public void Surprise_follows_the_modifiers_of_13_section_5_3()
    {
        Fix64 Chance(Defenses d) => Raids.SurpriseChance(d);
        Same(Fix64.FromRatio(60, 100), Chance(Defenses.None));
        Same(Fix64.FromRatio(48, 100), Chance(Defenses.None with { AlarmCalls = true }));      // x0.8
        Same(Fix64.FromRatio(30, 100), Chance(Defenses.None with { Dogs = true }));            // x0.5
        Same(Fix64.FromRatio(18, 100), Chance(Defenses.None with { Watchers = true }));        // x0.3
        Same(Fix64.FromRatio(75, 100), Chance(Defenses.None with { Night = true }));           // +15 points
        Same(Fix64.FromRatio(40, 100), Chance(Defenses.None with { RaidedRecently = true }));  // -20 points
        Same(Fix64.FromRatio(9, 100), Chance(Defenses.None with { Watchers = true, Dogs = true }));
    }

    [Fact]
    public void Defense_multipliers_stack_as_in_13_section_6_2()
    {
        Same(Fix64.FromRatio(11, 10), Defenses.None.Multiplier());                                     // home ground
        Assert.True(Math.Abs((Defenses.None with { ThornBarrier = true }).Multiplier().Raw - Fix64.FromRatio(143, 100).Raw) < 100);
        Assert.True(Math.Abs((Defenses.None with { Palisade = true }).Multiplier().Raw - Fix64.FromRatio(22, 10).Raw) < 100);
        Assert.True((Defenses.None with { Palisade = true, ThornBarrier = true }).Multiplier() > Fix64.FromInt(2) * Fix64.FromRatio(143, 100) - Fix64.One);
    }

    /// <summary>13 §11 F3: the raid table of §5.5 within ten points. The table's "deaths" are casualties (dead and wounded).</summary>
    [Fact]
    public void F3_the_raid_table_reproduces_within_ten_points()
    {
        (double success, double raiderLo, double raiderHi, double defenderLo, double defenderHi)[] targets =
        {
            (75, 5, 10, 8, 15), (40, 15, 25, 5, 10), (15, 25, 40, 2, 5), (5, 40, 100, 1, 3),
        };
        for (int i = 0; i < CombatTables.Scenarios.Length; i++)
        {
            RaidStats r = CombatTables.Run(CombatTables.Scenarios[i], 4000, 20261019);
            var t = targets[i];
            double success = r.SuccessPercent.ToDoubleForDisplay(), raiders = r.RaiderCasualtyPercent.ToDoubleForDisplay(), defenders = r.DefenderCasualtyPercent.ToDoubleForDisplay();
            string row = $"{r.Name}: success {success:F1} raiders {raiders:F1} defenders {defenders:F1}";
            Assert.True(Math.Abs(success - t.success) <= 10, row);
            Assert.InRange(raiders, t.raiderLo - 10, t.raiderHi + 10);
            Assert.InRange(defenders, t.defenderLo - 10, t.defenderHi + 10);
        }
        // More defense always means fewer successes and a worse day for the raiders.
        RaidStats[] rows = CombatTables.Scenarios.Select(s => CombatTables.Run(s, 3000, 11)).ToArray();
        for (int i = 1; i < rows.Length; i++)
        {
            Assert.True(rows[i].SuccessPercent < rows[i - 1].SuccessPercent);
            Assert.True(rows[i].RaiderCasualtyPercent > rows[i - 1].RaiderCasualtyPercent);
            Assert.True(rows[i].DefenderCasualtyPercent < rows[i - 1].DefenderCasualtyPercent);
        }
    }

    /// <summary>13 §11 F4: an unprovoked raid pays against an undefended band and loses against a palisaded one.</summary>
    [Fact]
    public void F4_raiding_pays_only_against_the_unprepared()
    {
        RaidStats[] rows = CombatTables.Scenarios.Select(s => CombatTables.Run(s, 4000, 20261019)).ToArray();
        Assert.True(rows[0].MeanNetValue > Fix64.Zero, $"undefended {rows[0].MeanNetValue}");
        Assert.True(rows[2].MeanNetValue < Fix64.Zero, $"palisade {rows[2].MeanNetValue}");
        Assert.True(rows[3].MeanNetValue < Fix64.Zero, $"palisade + warriors {rows[3].MeanNetValue}");
        Assert.True(rows[1].MeanNetValue < rows[0].MeanNetValue);
    }

    /// <summary>13 §11 F5: with 1.5 times the field strength, a village falls in one season at least 60% of the time.</summary>
    [Fact]
    public void F5_a_stronger_army_takes_a_defended_village_in_one_season()
    {
        var thorn = Defenses.None with { ThornBarrier = true };
        var palisade = Defenses.None with { Palisade = true, Watchers = true };
        Assert.True(CombatTables.CaptureSuccessPercent(Fix64.Parse("1.5"), thorn, 600, 3) >= Fix64.FromInt(60));
        Assert.True(CombatTables.CaptureSuccessPercent(Fix64.Parse("1.5"), palisade, 600, 3) >= Fix64.FromInt(60));
        // An equal army does not: capture is the strong's privilege (13 §8.4).
        Assert.True(CombatTables.CaptureSuccessPercent(Fix64.One, palisade, 600, 3) < Fix64.FromInt(30));
        Assert.True(CombatTables.CaptureSuccessPercent(Fix64.Parse("3.0"), palisade, 200, 3) >= Fix64.FromInt(95));
    }

    [Fact]
    public void A_capture_needs_four_holders_and_a_village_needs_two_seasons()
    {
        var win = new BattleResult { Winner = BattleWinner.Attacker, AttackerSurvivors = 3 };
        Assert.False(Captures.CanHold(win));
        win.AttackerSurvivors = 4;
        Assert.True(Captures.CanHold(win));
        Assert.False(Captures.CanHold(new BattleResult { Winner = BattleWinner.Defender, AttackerSurvivors = 20 }));
        Assert.Equal(1, Captures.OccupationSeasons(35));   // a camp
        Assert.Equal(1, Captures.OccupationSeasons(150));  // a hamlet
        Assert.Equal(2, Captures.OccupationSeasons(151));  // a village
    }

    [Fact]
    public void Bronze_weapons_shields_and_chariots_use_the_E3_values()
    {
        Same(Fix64.FromRatio(21, 10), Combat.E3WeaponStrength("arsenical"));
        Same(Fix64.FromRatio(23, 10), Combat.E3WeaponStrength("tin"));
        Same(Fix64.FromRatio(286, 100), Combat.TrainedWarriorStrength("bronze"));
        Same(Fix64.FromRatio(572, 100), Combat.TrainedWarriorStrength("bronze", chariotOnOpenTerrain: true));
        Same(Fix64.FromRatio(7, 10), Combat.ShieldCasualtyFactor(Fix64.One));
        Same(Fix64.FromRatio(85, 100), Combat.ShieldCasualtyFactor(Fix64.Half));
        Same(Fix64.Half, Combat.ChariotCasualtyFactor);
        Same(Fix64.FromRatio(35, 100), Combat.E3CasualtyFactor(Fix64.One, Fix64.One));
    }

    [Fact]
    public void Bronze_barriers_replace_each_other_and_walls_cannot_be_assaulted()
    {
        var hillfort = Defenses.None with { Hillfort = true };
        var walls = Defenses.None with { StoneWalls = true };
        Same(Fix64.FromRatio(275, 100), hillfort.Multiplier()); // ×1.1 home ×2.5 hillfort
        Same(Fix64.FromRatio(33, 10), walls.Multiplier()); // ×1.1 home ×3 walls
        Same(Fix64.FromRatio(1, 4), hillfort.LootMultiplier());
        Assert.False(walls.CanBeAssaulted());
        BattleResult result = Captures.Assault(Group(30, "2.0"), Group(20, "1.0", 100), walls, true, 1, 2);
        Assert.Equal(BattleWinner.StandOff, result.Winner);
        Assert.Empty(result.AttackerDead);
    }

    [Fact]
    public void Fighter_shields_and_chariot_crew_reduce_live_battle_casualties()
    {
        List<Fighter> plain = Group(30, "1.0", 100);
        List<Fighter> protectedSide = plain.Select(f => f with { CasualtiesTaken = Fix64.FromRatio(35, 100) }).ToList();
        var setup = Open();
        int plainLosses = 0, protectedLosses = 0;
        for (ulong seed = 0; seed < 100; seed++)
        {
            plainLosses += Combat.Fight(Group(30, "1.0"), plain, setup, 17, seed).DefenderDead.Count;
            protectedLosses += Combat.Fight(Group(30, "1.0"), protectedSide, setup, 17, seed).DefenderDead.Count;
        }
        Assert.True(protectedLosses < plainLosses, $"protected {protectedLosses}, plain {plainLosses}");
    }
}
