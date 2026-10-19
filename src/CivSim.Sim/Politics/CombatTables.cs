using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>One scenario of the raid table (docs/MVP/13 §5.5): a warband of eight against a band of forty.</summary>
public readonly record struct RaidScenario(string Name, Defenses Defenses, int Warriors);

/// <summary>Statistics of many raids of one scenario.</summary>
public readonly record struct RaidStats(string Name, int Trials, Fix64 SuccessPercent, Fix64 RaiderCasualtyPercent,
    Fix64 DefenderCasualtyPercent, Fix64 RaiderDeathPercent, Fix64 DefenderDeathPercent, Fix64 MeanNetValue);

/// <summary>
/// Monte Carlo of the raid and capture tables (docs/MVP/13 §5.5, §8.4, tuning checks F3 to F5). The rows use the
/// reference armament of E1 to E2: raiders carry stone-tipped spears (1.6), the band's adults wooden spears (1.3),
/// warriors (WAR-04) train at ×1.3 and fight at full base strength (docs/MVP/13 §7.1).
/// </summary>
public static class CombatTables
{
    public const int RaiderCount = 8;
    public const int DefenderAdults = 22;
    public static readonly Fix64 RaiderStrength = Fix64.FromRatio(16, 10);
    public static readonly Fix64 DefenderStrength = Fix64.FromRatio(6, 10) * Fix64.FromRatio(13, 10);
    public static readonly Fix64 WarriorStrength = Fix64.One * Fix64.FromRatio(13, 10) * Fix64.FromRatio(13, 10);
    public static readonly Fix64 LifeValue = Fix64.FromInt(8);
    public static readonly Fix64 RelationsCost = Fix64.FromInt(6);

    public static readonly RaidScenario[] Scenarios =
    {
        new("none, surprised", Defenses.None, 0),
        new("thorn barrier + watchers + dogs", new Defenses(true, false, true, true, false, false, false, false, false, false), 0),
        new("palisade + watchers", new Defenses(false, true, true, false, false, false, false, false, false, false), 0),
        new("palisade + watchers + warriors", new Defenses(false, true, true, false, false, false, false, false, false, false), 8),
    };

    public static List<Fighter> Raiders()
    {
        var list = new List<Fighter>(RaiderCount);
        for (int i = 0; i < RaiderCount; i++) list.Add(new Fighter(1000 + i, RaiderStrength));
        return list;
    }

    public static List<Fighter> Band(int warriors, int adults = DefenderAdults)
    {
        var list = new List<Fighter>(adults);
        for (int i = 0; i < adults; i++) list.Add(new Fighter(i, i < warriors ? WarriorStrength : DefenderStrength));
        return list;
    }

    private static Fix64 Percent(long part, long whole) => whole == 0 ? Fix64.Zero : Fix64.FromRatio(100 * part, whole);

    public static RaidStats Run(in RaidScenario scenario, int trials, ulong seed)
    {
        int successes = 0;
        long raiderDeaths = 0, defenderDeaths = 0, raiderHurt = 0, defenderHurt = 0;
        Fix64 net = Fix64.Zero;
        List<Fighter> raiders = Raiders();
        List<Fighter> band = Band(scenario.Warriors);
        for (int t = 0; t < trials; t++)
        {
            RaidOutcome o = Raids.Resolve(raiders, band, scenario.Defenses, raiderRanged: false, seed, (ulong)t);
            if (o.Success) successes++;
            raiderDeaths += o.Battle.AttackerDead.Count;
            defenderDeaths += o.Battle.DefenderDead.Count;
            raiderHurt += o.Battle.AttackerDead.Count + o.Battle.AttackerWounded.Count;
            defenderHurt += o.Battle.DefenderDead.Count + o.Battle.DefenderWounded.Count;
            net += Raids.NetValue(o.Success ? Fix64.One : Fix64.Zero, o.LootCapacity, Fix64.FromInt(o.Battle.AttackerDead.Count), LifeValue, RelationsCost);
        }
        return new RaidStats(scenario.Name, trials, Percent(successes, trials),
            Percent(raiderHurt, (long)trials * RaiderCount), Percent(defenderHurt, (long)trials * DefenderAdults),
            Percent(raiderDeaths, (long)trials * RaiderCount), Percent(defenderDeaths, (long)trials * DefenderAdults),
            net / trials);
    }

    /// <summary>
    /// F5: how often an attacker with <paramref name="ratio"/> times the defenders' field strength takes a village
    /// within one season of campaigning (13 §11). The village has 40 adult defenders behind <paramref name="defenses"/>;
    /// field strength is Σ strength × defense (docs/MVP/05 §12.3), and every adult comes out to defend the village.
    /// </summary>
    public static Fix64 CaptureSuccessPercent(Fix64 ratio, in Defenses defenses, int trials, ulong seed)
    {
        List<Fighter> village = Band(0, 40);
        Fix64 fieldStrength = Fix64.Zero;
        foreach (Fighter f in village) fieldStrength += f.Strength;
        // Attackers fight at spear strength; their number is whatever gives the stated ratio of field strength.
        Fix64 want = fieldStrength * defenses.Multiplier() * ratio;
        int attackers = (want / RaiderStrength).RoundToInt();
        var force = new List<Fighter>(attackers);
        for (int i = 0; i < attackers; i++) force.Add(new Fighter(5000 + i, RaiderStrength));
        int taken = 0;
        for (int t = 0; t < trials; t++)
        {
            BattleResult r = Captures.Assault(force, village, defenses, childrenInside: true, seed, (ulong)t);
            if (Captures.CanHold(r)) taken++;
        }
        return Percent(taken, trials);
    }
}
