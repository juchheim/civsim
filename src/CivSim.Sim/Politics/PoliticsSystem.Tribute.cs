using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A weaker civilization paying tribute to a stronger one under terms (DIP-06, docs/MVP/25 §8).</summary>
public sealed class Tribute
{
    public int Overlord { get; internal set; }
    public int Subject { get; internal set; }
    public int SinceSeason { get; internal set; }
}

/// <summary>
/// Tributary states (DIP-06). Terms end a war the overlord is winning: the subject hands over a tenth of its actual food surplus each season and a quarter of its yearly
/// Dominion points go to the overlord (the points move, they are never copied, and the land never counts for the overlord, 25 Q5). No chains and no cycles: a
/// civilization is a subject of one overlord, and a subject has no subjects. The terms run a treaty term, end if either side goes to war again, and fall away if the
/// subject comes to outmatch the overlord.
/// </summary>
public sealed partial class PoliticsSystem
{
    /// <summary>Share of each subject settlement's food surplus handed over a season (provisional).</summary>
    private static readonly Fix64 TributeSurplusShare = Fix64.FromRatio(1, 10);
    /// <summary>Share of the subject's yearly Dominion flow that moves to the overlord (DIP-06).</summary>
    private static readonly Fix64 TributeDominionShare = Fix64.FromRatio(1, 4);
    /// <summary>The overlord must field this many times the subject's strength to impose terms (the odds a war of conquest asks, docs/MVP/13 §8.4).</summary>
    private static readonly Fix64 TributeOdds = Fix64.FromRatio(15, 10);

    private readonly List<Tribute> _tributes = new();

    public IReadOnlyList<Tribute> Tributes => _tributes;

    public int OverlordOf(int subject)
    {
        foreach (Tribute t in _tributes) if (t.Subject == subject) return t.Overlord;
        return -1;
    }

    public bool IsSubject(int civ) => OverlordOf(civ) >= 0;

    public bool IsOverlord(int civ)
    {
        foreach (Tribute t in _tributes) if (t.Overlord == civ) return true;
        return false;
    }

    /// <summary>
    /// Impose tributary terms on a civilization the caller is at war with and clearly outmatches. Hard rules: DIP-06 for the overlord, no occupation or siege between
    /// them, no chain (neither side already in a tribute relation) and one and a half times the subject's strength.
    /// </summary>
    public bool OrderTribute(int overlord, int subject)
    {
        if (!_economy.IronChain || (uint)overlord >= (uint)_civs || (uint)subject >= (uint)_civs || overlord == subject) return false;
        if (_p.Dip06 < 0 || !_research.IsDone(overlord, _p.Dip06) || !_dip.AtWar(overlord, subject)) return false;
        if (IsSubject(overlord) || IsOverlord(subject) || IsSubject(subject)) return false;
        foreach (Occupation occ in _occupations)
            if (occ.Civ == overlord && _sCiv[occ.Settlement] == subject || occ.Civ == subject && _sCiv[occ.Settlement] == overlord) return false;
        foreach (Investment investment in _investments)
            if (investment.Civ == overlord && investment.TargetCiv == subject || investment.Civ == subject && investment.TargetCiv == overlord) return false;
        if (FieldStrength(overlord) < FieldStrength(subject) * TributeOdds) return false;
        _dip.MakePeace(overlord, subject, _season);
        _tributes.Add(new Tribute { Overlord = overlord, Subject = subject, SinceSeason = _season });
        _tributes.Sort((a, b) => a.Subject.CompareTo(b.Subject));
        _events.Add(new PoliticsEvent(_tick, (byte)overlord, PoliticsEventKind.TributeAgreed, subject, 0, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)subject, PoliticsEventKind.TributeAgreed, overlord, 1, 0));
        return true;
    }

    private void EndTribute(int index, int reason)
    {
        Tribute t = _tributes[index];
        _events.Add(new PoliticsEvent(_tick, (byte)t.Overlord, PoliticsEventKind.TributeEnded, t.Subject, 0, reason));
        _events.Add(new PoliticsEvent(_tick, (byte)t.Subject, PoliticsEventKind.TributeEnded, t.Overlord, 1, reason));
        _tributes.RemoveAt(index);
    }

    /// <summary>One season: the terms lapse with war, the treaty term or the subject's recovery; otherwise each subject town hands over its share of surplus food.</summary>
    private void AdvanceTribute()
    {
        for (int i = _tributes.Count - 1; i >= 0; i--)
        {
            Tribute t = _tributes[i];
            if (_dip.AtWar(t.Overlord, t.Subject)) { EndTribute(i, 0); continue; }                       // 0: war again
            if (_season - t.SinceSeason >= _trade.TreatySeasons) { EndTribute(i, 1); continue; }          // 1: the term ran out
            if (FieldStrength(t.Subject) > FieldStrength(t.Overlord)) { EndTribute(i, 2); continue; }     // 2: the subject outgrew its terms
            for (int s = 0; s < _sCount; s++)
            {
                if (_sCiv[s] != t.Subject || _free[s]) continue;
                Fix64 due = Surplus(s, TradeGood.Food) * TributeSurplusShare;
                if (due <= Fix64.Zero) continue;
                int dest = NearestSettlementOf(t.Overlord, _sTile[s]);
                if (dest < 0) continue;
                Fix64 taken = _economy.TakeGoods(s, due, LootPreference.FoodOnly);
                if (taken > Fix64.Zero) _economy.GiveGoods(dest, taken);
            }
        }
    }

    private int NearestSettlementOf(int civ, int tile)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (d < bestDistance) { best = s; bestDistance = d; }
        }
        return best;
    }

    /// <summary>The yearly Dominion accrual with tribute applied: the subject's gain this year is cut by a quarter and that quarter goes to the overlord.</summary>
    private void AccrueDominionWithTribute()
    {
        if (!_economy.IronChain || _tributes.Count == 0) { _territory.AccrueDominion(); return; }
        var before = new Fix64[_civs];
        for (int c = 0; c < _civs; c++) before[c] = _territory.Dominion(c);
        _territory.AccrueDominion();
        foreach (Tribute t in _tributes)
        {
            Fix64 gain = _territory.Dominion(t.Subject) - before[t.Subject];
            if (gain > Fix64.Zero) _territory.MoveDominion(t.Subject, t.Overlord, gain * TributeDominionShare);
        }
    }

    private void HashTributes(ref StateHasher h)
    {
        h.Add(_tributes.Count);
        foreach (Tribute t in _tributes) { h.Add(t.Overlord); h.Add(t.Subject); h.Add(t.SinceSeason); }
    }

    private void WriteTributes(BinaryWriter w)
    {
        w.Write(_tributes.Count);
        foreach (Tribute t in _tributes) { w.Write(t.Overlord); w.Write(t.Subject); w.Write(t.SinceSeason); }
    }

    private void ReadTributes(BinaryReader r)
    {
        _tributes.Clear();
        int count = r.ReadInt32();
        if (count < 0 || count > _civs) throw new InvalidDataException("Snapshot tribute count is invalid.");
        for (int i = 0; i < count; i++)
        {
            var t = new Tribute { Overlord = r.ReadInt32(), Subject = r.ReadInt32(), SinceSeason = r.ReadInt32() };
            if ((uint)t.Overlord >= (uint)_civs || (uint)t.Subject >= (uint)_civs || t.Overlord == t.Subject || i > 0 && _tributes[i - 1].Subject >= t.Subject)
                throw new InvalidDataException("Snapshot tribute fields or order are invalid.");
            _tributes.Add(t);
        }
    }

    internal Fix64 FixtureFieldStrength(int civ) => FieldStrength(civ);
    internal void FixtureAdvanceTribute() => AdvanceTribute();
    internal void FixtureMakePeace(int a, int b) => _dip.MakePeace(a, b, _season);
    internal void FixtureAccrueDominion() => AccrueDominionWithTribute();
    internal Fix64 FixtureDominion(int civ) => _territory.Dominion(civ);
}
