using System;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;

namespace CivSim.Tests;

public class DiplomacyTests
{
    private static (double Friendly, double Wary, double Hostile) Distribution(ContactModifiers m, int rolls = 20000)
    {
        int f = 0, w = 0, h = 0;
        Pcg32 rng = Pcg32.Seed(11, 3);
        for (int i = 0; i < rolls; i++)
            switch (FirstContact.Roll(m, ref rng)) { case Stance.Friendly: f++; break; case Stance.Wary: w++; break; default: h++; break; }
        return (100.0 * f / rolls, 100.0 * w / rolls, 100.0 * h / rolls);
    }

    private static readonly ContactModifiers Plain = new(false, false, false, false, false, false, false, 0);

    /// <summary>13 §11 F2: with no diplomacy projects about 25% of meetings are friendly, 45% wary and 30% hostile.</summary>
    [Fact]
    public void F2_first_contact_without_projects_is_25_45_30()
    {
        (double f, double w, double h) = Distribution(Plain);
        Assert.InRange(f, 23.5, 26.5);
        Assert.InRange(w, 43.0, 47.0);
        Assert.InRange(h, 28.0, 32.0);
        Assert.Equal((25, 30), FirstContact.Odds(Plain));
    }

    [Fact]
    public void Projects_and_posture_shift_the_odds_as_in_13_section_3_1()
    {
        Assert.Equal((55, 15), FirstContact.Odds(Plain with { StrangerGreetings = true }));   // +30 friendly, hostile halved
        Assert.Equal((45, 30), FirstContact.Odds(Plain with { GiftExchange = true }));         // +20
        Assert.Equal((40, 30), FirstContact.Odds(Plain with { SpokenLanguage = true }));       // +15
        Assert.Equal((40, 30), FirstContact.Odds(Plain with { PostureProsper = true }));       // +15
        Assert.Equal((25, 50), FirstContact.Odds(Plain with { PostureRaid = true }));          // +20 hostile
        Assert.Equal((25, 45), FirstContact.Odds(Plain with { PartyHungry = true }));          // +15
        Assert.Equal((25, 40), FirstContact.Odds(Plain with { OtherMuchWeaker = true }));      // +10
        Assert.Equal((27, 28), FirstContact.Odds(Plain with { OtherReputation = 8 }));         // +/-1% per 4 points of reputation
        (int friendly, int hostile) = FirstContact.Odds(new ContactModifiers(true, true, true, true, true, true, true, 100));
        Assert.True(friendly + hostile <= 100);
    }

    [Fact]
    public void The_more_hostile_side_decides_violence_and_the_friendlier_decides_relations()
    {
        Assert.Equal((true, -20), FirstContact.Combine(Stance.Friendly, Stance.Hostile));
        Assert.Equal((true, -20), FirstContact.Combine(Stance.Wary, Stance.Hostile));
        Assert.Equal((false, 20), FirstContact.Combine(Stance.Friendly, Stance.Wary));
        Assert.Equal((false, 20), FirstContact.Combine(Stance.Friendly, Stance.Friendly));
        Assert.Equal((false, 0), FirstContact.Combine(Stance.Wary, Stance.Wary));
    }

    [Fact]
    public void Relations_clamp_hold_an_alliance_floor_and_drift_toward_zero_each_year()
    {
        var d = new Diplomacy(4);
        d.Shift(0, 1, 500);
        Assert.Equal(100, d.Relations(0, 1));
        Assert.Equal(100, d.Relations(1, 0));
        d.Shift(0, 1, -500);
        Assert.Equal(-100, d.Relations(1, 0));
        d.Set(2, 3, 60);
        d.Ally(2, 3);
        d.Shift(2, 3, -90);
        Assert.Equal(Diplomacy.AllianceFloor, d.Relations(2, 3));
        d.Set(0, 1, 12);
        d.YearlyDrift();
        Assert.Equal(7, d.Relations(0, 1));
        d.YearlyDrift(); d.YearlyDrift();
        Assert.Equal(0, d.Relations(0, 1));
        d.Set(0, 1, -12);
        d.YearlyDrift();
        Assert.Equal(-7, d.Relations(1, 0));
        Assert.Equal(Diplomacy.AllianceFloor, d.Relations(2, 3));
    }

    [Fact]
    public void War_peace_truce_and_reputation_bookkeeping()
    {
        var d = new Diplomacy(4);
        d.MarkAware(0, 1);
        Assert.True(d.Aware(1, 0));
        Assert.False(d.Contacted(0, 1));
        d.MarkContacted(0, 1);
        Assert.True(d.Contacted(1, 0));
        d.DeclareWar(0, 1);
        Assert.True(d.AtWar(1, 0));
        d.MakePeace(0, 1, season: 40);
        Assert.False(d.AtWar(0, 1));
        Assert.Equal(-10, d.Relations(0, 1));
        Assert.True(d.InTruce(0, 1, 40 + Diplomacy.TruceSeasons - 1));
        Assert.False(d.InTruce(0, 1, 40 + Diplomacy.TruceSeasons));
        d.AdjustReputation(2, -300);
        Assert.Equal(-100, d.Reputation(2));
        d.RecordRaid(0, 1, 33);
        Assert.Equal(33, d.LastRaidBy(0, 1));
        Assert.Equal(33, d.LastRaidOnAny(1));
        d.RecordGift(0, 1, 30);
        d.RecordGift(0, 1, 31);
        Assert.Equal(2, d.GiftsThisYear(0, 1));
        d.YearlyDrift();
        Assert.Equal(0, d.GiftsThisYear(0, 1));
    }
}
