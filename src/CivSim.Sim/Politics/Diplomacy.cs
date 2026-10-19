using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Politics;

/// <summary>How a meeting party resolves a first contact (docs/MVP/13 §3.1).</summary>
public enum Stance : byte { Friendly, Wary, Hostile }

/// <summary>The modifiers on one side's first-contact roll. Traits are not simulated yet, so nurturing and brave counts are zero.</summary>
public readonly record struct ContactModifiers(
    bool StrangerGreetings, bool GiftExchange, bool SpokenLanguage, bool PostureProsper, bool PostureRaid,
    bool PartyHungry, bool OtherMuchWeaker, int OtherReputation);

/// <summary>First-contact roll (docs/MVP/13 §3.1).</summary>
public static class FirstContact
{
    /// <summary>The probabilities, in percent, of Friendly and Hostile; Wary takes the remainder.</summary>
    public static (int Friendly, int Hostile) Odds(in ContactModifiers m)
    {
        int friendly = 25;
        if (m.StrangerGreetings) friendly += 30;
        if (m.GiftExchange) friendly += 20;
        if (m.SpokenLanguage) friendly += 15;
        if (m.PostureProsper) friendly += 15;
        int hostile = 30;
        if (m.StrangerGreetings) hostile = hostile / 2; // -50% relative
        if (m.PostureRaid) hostile += 20;
        if (m.PartyHungry) hostile += 15;
        if (m.OtherMuchWeaker) hostile += 10;
        // Reputation: +/-1% per 4 points (13 §3.3).
        friendly += m.OtherReputation / 4;
        hostile -= m.OtherReputation / 4;
        friendly = Math.Clamp(friendly, 0, 95);
        hostile = Math.Clamp(hostile, 0, 95);
        if (friendly + hostile > 100) { friendly = friendly * 100 / (friendly + hostile); hostile = 100 - friendly; }
        return (friendly, hostile);
    }

    public static Stance Roll(in ContactModifiers m, ref Pcg32 rng)
    {
        (int friendly, int hostile) = Odds(m);
        uint roll = rng.NextBelow(100);
        if (roll < friendly) return Stance.Friendly;
        if (roll < friendly + hostile) return Stance.Hostile;
        return Stance.Wary;
    }

    /// <summary>The two sides' stances combine: the more hostile decides violence, the friendlier decides Relations (13 §3.1).</summary>
    public static (bool Violence, int RelationsDelta) Combine(Stance a, Stance b)
    {
        if (a == Stance.Hostile || b == Stance.Hostile) return (true, -20);
        if (a == Stance.Friendly || b == Stance.Friendly) return (false, 20);
        return (false, 0);
    }
}

/// <summary>
/// Who knows whom and how they feel (docs/MVP/05 §12, 13 §3): awareness, contact, a Relations score from -100 to
/// +100 per pair, public reputation, war, truce and alliance. Everything is indexed by civilization id, with
/// symmetric pair data stored on both orderings.
/// </summary>
public sealed class Diplomacy
{
    public const int Min = -100, Max = 100;
    public const int HostileBelow = -30, AllianceAbove = 40;
    public const int AllianceFloor = 40;
    /// <summary>A truce after a peace lasts four years (16 seasons, 13 §8.1).</summary>
    public const int TruceSeasons = 16;

    private readonly int _n;
    private readonly int[] _relations;
    private readonly bool[] _aware, _contacted, _war, _allied;
    private readonly int[] _truceUntil;
    private readonly int[] _reputation;
    private readonly int[] _giftsThisYear;
    private readonly int[] _lastGift, _lastBarter, _lastRaidOn, _lastRaidBy;

    public Diplomacy(int civs)
    {
        _n = civs;
        _relations = new int[civs * civs];
        _aware = new bool[civs * civs];
        _contacted = new bool[civs * civs];
        _war = new bool[civs * civs];
        _allied = new bool[civs * civs];
        _truceUntil = new int[civs * civs];
        _reputation = new int[civs];
        _giftsThisYear = new int[civs * civs];
        _lastGift = new int[civs * civs];
        _lastBarter = new int[civs * civs];
        _lastRaidOn = new int[civs * civs];
        _lastRaidBy = new int[civs * civs];
        Array.Fill(_lastGift, -1000);
        Array.Fill(_lastBarter, -1000);
        Array.Fill(_lastRaidOn, -1000);
        Array.Fill(_lastRaidBy, -1000);
    }

    private int I(int a, int b) => a * _n + b;

    public int Relations(int a, int b) => _relations[I(a, b)];
    public bool Aware(int a, int b) => _aware[I(a, b)];
    public bool Contacted(int a, int b) => _contacted[I(a, b)];
    public bool AtWar(int a, int b) => _war[I(a, b)];
    public bool Allied(int a, int b) => _allied[I(a, b)];
    public int Reputation(int civ) => _reputation[civ];
    public bool InTruce(int a, int b, int season) => season < _truceUntil[I(a, b)];

    public void MarkAware(int a, int b) { _aware[I(a, b)] = true; _aware[I(b, a)] = true; }

    public void MarkContacted(int a, int b)
    {
        MarkAware(a, b);
        _contacted[I(a, b)] = true;
        _contacted[I(b, a)] = true;
    }

    /// <summary>Moves Relations by <paramref name="delta"/>, keeping an alliance's floor.</summary>
    public void Shift(int a, int b, int delta)
    {
        int value = Math.Clamp(_relations[I(a, b)] + delta, Min, Max);
        if (_allied[I(a, b)]) value = Math.Max(value, AllianceFloor);
        _relations[I(a, b)] = value;
        _relations[I(b, a)] = value;
    }

    public void Set(int a, int b, int value)
    {
        value = Math.Clamp(value, Min, Max);
        _relations[I(a, b)] = value;
        _relations[I(b, a)] = value;
    }

    public void AdjustReputation(int civ, int delta) => _reputation[civ] = Math.Clamp(_reputation[civ] + delta, Min, Max);

    public void DeclareWar(int a, int b) { _war[I(a, b)] = true; _war[I(b, a)] = true; _allied[I(a, b)] = false; _allied[I(b, a)] = false; }

    /// <summary>Peace sets Relations to -10 and starts a four-year truce; breaking it later costs reputation (13 §8.1).</summary>
    public void MakePeace(int a, int b, int season)
    {
        _war[I(a, b)] = false;
        _war[I(b, a)] = false;
        _truceUntil[I(a, b)] = season + TruceSeasons;
        _truceUntil[I(b, a)] = season + TruceSeasons;
        Set(a, b, -10);
    }

    public void Ally(int a, int b)
    {
        _allied[I(a, b)] = true;
        _allied[I(b, a)] = true;
        Shift(a, b, 0);
    }

    public void BreakAlliance(int a, int b) { _allied[I(a, b)] = false; _allied[I(b, a)] = false; }

    // ---- per-pair bookkeeping the Civ Mind uses -------------------------------------------
    public int GiftsThisYear(int from, int to) => _giftsThisYear[I(from, to)];
    public int LastGift(int from, int to) => _lastGift[I(from, to)];
    public int LastBarter(int a, int b) => _lastBarter[I(a, b)];
    public int LastRaidOn(int target, int by) => _lastRaidOn[I(target, by)];
    public int LastRaidBy(int by) { int m = -1000; for (int t = 0; t < _n; t++) m = Math.Max(m, _lastRaidBy[I(by, t)]); return m; }
    public int LastRaidBy(int by, int target) => _lastRaidBy[I(by, target)];
    public int LastRaidOnAny(int target) { int m = -1000; for (int b = 0; b < _n; b++) m = Math.Max(m, _lastRaidOn[I(target, b)]); return m; }

    public void RecordGift(int from, int to, int season)
    {
        _giftsThisYear[I(from, to)]++;
        _lastGift[I(from, to)] = season;
    }

    public void RecordBarter(int a, int b, int season) { _lastBarter[I(a, b)] = season; _lastBarter[I(b, a)] = season; }

    public void RecordRaid(int by, int target, int season)
    {
        _lastRaidBy[I(by, target)] = season;
        _lastRaidOn[I(target, by)] = season;
    }

    /// <summary>
    /// Once a year Relations drift 5 points toward zero, the gift tally resets, and a standing alliance holds its
    /// floor (13 §3.2).
    /// </summary>
    public void YearlyDrift()
    {
        for (int a = 0; a < _n; a++)
            for (int b = a + 1; b < _n; b++)
            {
                int v = _relations[I(a, b)];
                if (v > 0) v = Math.Max(0, v - 5);
                else if (v < 0) v = Math.Min(0, v + 5);
                if (_allied[I(a, b)]) v = Math.Max(v, AllianceFloor);
                _relations[I(a, b)] = v;
                _relations[I(b, a)] = v;
            }
        Array.Clear(_giftsThisYear);
    }

    public void Hash(ref StateHasher h)
    {
        foreach (int v in _relations) h.Add(v);
        foreach (bool v in _aware) h.Add(v);
        foreach (bool v in _contacted) h.Add(v);
        foreach (bool v in _war) h.Add(v);
        foreach (bool v in _allied) h.Add(v);
        foreach (int v in _truceUntil) h.Add(v);
        foreach (int v in _reputation) h.Add(v);
        foreach (int v in _giftsThisYear) h.Add(v);
        foreach (int v in _lastGift) h.Add(v);
        foreach (int v in _lastBarter) h.Add(v);
        foreach (int v in _lastRaidOn) h.Add(v);
        foreach (int v in _lastRaidBy) h.Add(v);
    }

    public void Write(BinaryWriter w)
    {
        foreach (int v in _relations) w.Write(v);
        foreach (bool v in _aware) w.Write(v);
        foreach (bool v in _contacted) w.Write(v);
        foreach (bool v in _war) w.Write(v);
        foreach (bool v in _allied) w.Write(v);
        foreach (int v in _truceUntil) w.Write(v);
        foreach (int v in _reputation) w.Write(v);
        foreach (int v in _giftsThisYear) w.Write(v);
        foreach (int v in _lastGift) w.Write(v);
        foreach (int v in _lastBarter) w.Write(v);
        foreach (int v in _lastRaidOn) w.Write(v);
        foreach (int v in _lastRaidBy) w.Write(v);
    }

    public void Read(BinaryReader r)
    {
        for (int i = 0; i < _relations.Length; i++) _relations[i] = r.ReadInt32();
        for (int i = 0; i < _aware.Length; i++) _aware[i] = r.ReadBoolean();
        for (int i = 0; i < _contacted.Length; i++) _contacted[i] = r.ReadBoolean();
        for (int i = 0; i < _war.Length; i++) _war[i] = r.ReadBoolean();
        for (int i = 0; i < _allied.Length; i++) _allied[i] = r.ReadBoolean();
        for (int i = 0; i < _truceUntil.Length; i++) _truceUntil[i] = r.ReadInt32();
        for (int i = 0; i < _reputation.Length; i++) _reputation[i] = r.ReadInt32();
        for (int i = 0; i < _giftsThisYear.Length; i++) _giftsThisYear[i] = r.ReadInt32();
        for (int i = 0; i < _lastGift.Length; i++) _lastGift[i] = r.ReadInt32();
        for (int i = 0; i < _lastBarter.Length; i++) _lastBarter[i] = r.ReadInt32();
        for (int i = 0; i < _lastRaidOn.Length; i++) _lastRaidOn[i] = r.ReadInt32();
        for (int i = 0; i < _lastRaidBy.Length; i++) _lastRaidBy[i] = r.ReadInt32();
    }
}
