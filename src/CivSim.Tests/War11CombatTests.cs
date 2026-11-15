using System.Collections.Generic;
using System.Linq;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Tests;

/// <summary>WAR-11 crossbows and longbows in the combat core: bolt strength 2.0 and shields that do not help against them (docs/MVP/27 §5.6, e5_combat_oracle.py).</summary>
public class War11CombatTests
{
    private static readonly Fix64 Shield = Combat.ShieldCasualtyFactor(Fix64.One);

    private static List<Fighter> Levy(int n, bool bolt, long firstId = 0)
        => Enumerable.Range(0, n).Select(i => new Fighter(firstId + i, bolt ? Combat.BoltStrength : Fix64.One) { Bolt = bolt }).ToList();

    private static List<Fighter> Infantry(int n, bool shielded, long firstId = 1000)
        => Enumerable.Range(0, n).Select(i => new Fighter(firstId + i, Fix64.FromInt(3))
        {
            CasualtiesTaken = shielded ? Shield : Fix64.One,
            ShieldFactor = shielded ? Shield : Fix64.One,
        }).ToList();

    private static BattleSetup Open() => new(Fix64.One, Fix64.One, false, Fix64.FromRatio(4, 10), Fix64.FromRatio(4, 10), 15, false);

    private static int Lost(BattleResult r) => r.DefenderDead.Count + r.DefenderWounded.Count;

    [Fact]
    public void A_bolt_weapon_is_strength_two()
    {
        Assert.Equal(Fix64.FromInt(2), Combat.BoltStrength);
    }

    [Fact]
    public void Shields_give_no_help_against_a_bolt_line()
    {
        for (ulong stream = 1; stream <= 20; stream++)
        {
            BattleResult shielded = Combat.Fight(Levy(30, bolt: true), Infantry(20, shielded: true), Open(), 11, stream);
            BattleResult bare = Combat.Fight(Levy(30, bolt: true), Infantry(20, shielded: false), Open(), 11, stream);
            Assert.Equal(bare.Winner, shielded.Winner);
            Assert.Equal(bare.DefenderDead, shielded.DefenderDead);
            Assert.Equal(bare.DefenderWounded, shielded.DefenderWounded);
        }
    }

    [Fact]
    public void Shields_still_help_against_ordinary_weapons()
    {
        int shieldedLost = 0, bareLost = 0;
        for (ulong stream = 1; stream <= 40; stream++)
        {
            shieldedLost += Lost(Combat.Fight(Levy(30, bolt: false), Infantry(20, shielded: true), Open(), 11, stream));
            bareLost += Lost(Combat.Fight(Levy(30, bolt: false), Infantry(20, shielded: false), Open(), 11, stream));
        }
        Assert.True(shieldedLost < bareLost, $"shielded {shieldedLost} should lose fewer than bare {bareLost}");
    }

    [Fact]
    public void A_mixed_line_pierces_in_proportion()
    {
        // Half the attacking strength is bolts: the shield is worth half what it is against plain weapons, so the loss lies between the two.
        var mixed = Levy(10, bolt: true).Concat(Levy(20, bolt: false, 500)).ToList();
        int plain = 0, pierced = 0, halfPierced = 0;
        for (ulong stream = 1; stream <= 60; stream++)
        {
            plain += Lost(Combat.Fight(Levy(30, bolt: false), Infantry(20, shielded: true), Open(), 5, stream));
            pierced += Lost(Combat.Fight(Levy(30, bolt: true), Infantry(20, shielded: true), Open(), 5, stream));
            halfPierced += Lost(Combat.Fight(mixed, Infantry(20, shielded: true), Open(), 5, stream));
        }
        Assert.True(plain < halfPierced);
        Assert.True(halfPierced < pierced);
    }

    [Fact]
    public void Bolt_fights_are_deterministic()
    {
        BattleResult a = Combat.Fight(Levy(25, true), Infantry(20, true), Open(), 3, 9);
        BattleResult b = Combat.Fight(Levy(25, true), Infantry(20, true), Open(), 3, 9);
        Assert.Equal(a.DefenderDead, b.DefenderDead);
        Assert.Equal(a.AttackerWounded, b.AttackerWounded);
    }
}
