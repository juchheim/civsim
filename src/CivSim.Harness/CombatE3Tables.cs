using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Harness;

internal readonly record struct E3RaidRow(string Name, int Raiders, int Adults, string DefenderWeapon,
    int DefenderWarriors, bool RaiderShields, bool DefenderShields, Defenses Defenses,
    double SuccessTarget, double RaiderHurtTarget, double DefenderHurtTarget, double NetTarget);

internal readonly record struct E3RaidResult(string Name, double Success, double RaiderHurt, double DefenderHurt, double Net);
internal readonly record struct E3FieldRow(string Name, int Attackers, string AttackerWeapon, bool Chariots,
    bool AttackerShields, int Defenders, string DefenderWeapon, bool DefenderShields, double? WinTarget);
internal readonly record struct E3FieldResult(string Name, double WinPercent, double? WinTarget);

/// <summary>Live C# port of the E3 raid and field tables in docs/MVP/22 §7.5 and 07d §6.</summary>
internal static class CombatE3Tables
{
    internal const ulong Seed = 20261019;
    internal const int DefaultTrials = 4000;
    private const double Band = 10;
    private static Defenses D(bool thorn = false, bool palisade = false, bool watchers = false,
        bool hillfort = false, bool walls = false) => new(thorn, palisade, watchers, false, false, false,
            false, false, false, false, hillfort, walls, false, true);

    internal static readonly E3RaidRow[] Raids =
    {
        new("none, stone-armed militia", 12, 42, "stone", 0, true, false, D(), 94.5, 9, 24, 33),
        new("palisade + watchers, stone militia", 12, 42, "stone", 0, true, false, D(palisade:true, watchers:true), 16.5, 39, 8, -16),
        new("palisade + watchers, bronze militia + shields", 12, 42, "bronze", 0, true, true, D(palisade:true, watchers:true), 2.8, 45, 3, -23),
        new("palisade + watchers + 8 bronze warriors", 12, 42, "bronze", 8, true, true, D(palisade:true, watchers:true), 1.3, 60, 3, -29),
        new("hillfort + watchers + 8 warriors", 12, 42, "bronze", 8, true, true, D(watchers:true, hillfort:true), .3, 83, 2, -38),
        new("walls + watchers + 8 warriors", 12, 42, "bronze", 8, true, true, D(watchers:true, walls:true), .1, 93, 2, -42),
        new("12 stone spearmen, no shields vs none", 12, 42, "stone", 0, false, false, D(), 63.2, 18, 16.6, 14.8),
        new("mining camp, 6 stone miners", 8, 6, "stone", 0, true, false, D(), 96.5, 1, 45, 25),
        new("mining camp, 6 + 2 bronze guards + watch", 8, 8, "stone", 2, true, true, D(watchers:true), 99.5, 4, 42, 24),
        new("hamlet of 15 adults, stone, no palisade", 8, 15, "stone", 0, true, false, D(), 100, 1, 37, 25),
        new("hamlet of 15, bronze, thorn + watch", 8, 15, "bronze", 0, true, true, D(thorn:true, watchers:true), 89.2, 11, 26, 17),
    };

    internal static readonly E3FieldRow[] Fields =
    {
        new("20 bronze+shield vs 20 stone warriors",20,"bronze",false,true,20,"stone",false,100),
        new("20 bronze+shield vs 28 stone warriors",20,"bronze",false,true,28,"stone",false,18),
        new("12 chariot crew vs 24 bronze+shield infantry",12,"bronze",true,true,24,"bronze",true,37),
        new("12 chariot crew vs 18 bronze+shield infantry",12,"bronze",true,true,18,"bronze",true,100),
        new("12 chariot crew vs 20 bronze+shield infantry",12,"bronze",true,true,20,"bronze",true,null),
        new("12 chariot crew vs 22 bronze+shield infantry",12,"bronze",true,true,22,"bronze",true,null),
    };

    internal static bool WithinBand(double value, double target) => Math.Abs(value - target) <= Band;
    internal static bool Pass(E3RaidResult r, E3RaidRow s) => WithinBand(r.Success,s.SuccessTarget)
        && WithinBand(r.RaiderHurt,s.RaiderHurtTarget) && WithinBand(r.DefenderHurt,s.DefenderHurtTarget)
        && WithinBand(r.Net,s.NetTarget);
    internal static bool Pass(E3FieldResult r) => r.WinTarget is null || WithinBand(r.WinPercent,r.WinTarget.Value);

    internal static E3RaidResult Run(E3RaidRow s, int trials, ulong seed = Seed)
    {
        var raiders = Warriors(s.Raiders, s.Name.StartsWith("12 stone", StringComparison.Ordinal) ? "stone" : "bronze", 1000, false, s.RaiderShields);
        var defenders = new List<Fighter>(s.Adults);
        Fix64 bronzeWarrior = Combat.TrainedWarriorStrength("bronze");
        Fix64 militia = Combat.MilitiaStrength(s.DefenderWeapon);
        for (int i=0;i<s.Adults;i++)
            defenders.Add(new Fighter(i, i < s.DefenderWarriors ? bronzeWarrior : militia)
                { CasualtiesTaken = s.DefenderShields ? Fix64.FromRatio(7,10) : Fix64.One });
        long success=0, ah=0, dh=0; Fix64 net=Fix64.Zero;
        for (int t=0;t<trials;t++)
        {
            RaidOutcome o=CivSim.Sim.Politics.Raids.Resolve(raiders,defenders,s.Defenses,false,seed,(ulong)t);
            if(o.Success) success++;
            ah+=o.Battle.AttackerDead.Count+o.Battle.AttackerWounded.Count;
            dh+=o.Battle.DefenderDead.Count+o.Battle.DefenderWounded.Count;
            net+=CivSim.Sim.Politics.Raids.NetValue(o.Success?Fix64.One:Fix64.Zero,o.LootCapacity,
                Fix64.FromInt(o.Battle.AttackerDead.Count),Fix64.FromInt(8),Fix64.FromInt(6));
        }
        return new(s.Name,100.0*success/trials,100.0*ah/(trials*s.Raiders),100.0*dh/(trials*s.Adults),net.ToDoubleForDisplay()/trials);
    }

    internal static E3FieldResult Run(E3FieldRow s, int trials, ulong seed = Seed)
    {
        var a=Warriors(s.Attackers,s.AttackerWeapon,1000,s.Chariots,s.AttackerShields);
        var d=Warriors(s.Defenders,s.DefenderWeapon,5000,false,s.DefenderShields);
        var setup=new BattleSetup(Fix64.One,Fix64.One,false,Fix64.FromRatio(4,10),Fix64.FromRatio(4,10),15,false);
        int wins=0;
        for(int t=0;t<trials;t++) if(Combat.Fight(a,d,setup,seed,(ulong)t).Winner==BattleWinner.Attacker) wins++;
        return new(s.Name,100.0*wins/trials,s.WinTarget);
    }

    private static List<Fighter> Warriors(int n,string weapon,long first,bool chariots,bool shields)
    {
        var list=new List<Fighter>(n); Fix64 strength=Combat.TrainedWarriorStrength(weapon,chariots);
        for(int i=0;i<n;i++) list.Add(new Fighter(first+i,strength){CasualtiesTaken=chariots
            ? Combat.E3CasualtyFactor(shields?Fix64.One:Fix64.Zero,Fix64.One)
            : shields?Fix64.FromRatio(7,10):Fix64.One});
        return list;
    }
}
