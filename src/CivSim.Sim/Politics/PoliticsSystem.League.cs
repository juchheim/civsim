using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>A voluntary league of allied civilizations (DIP-07).</summary>
public sealed class League
{
    public List<int> Members { get; } = new();
}

/// <summary>
/// Leagues and confederations (DIP-07, docs/MVP/25 §8). A league is a set of mutually allied civilizations. It shares nothing by itself: when a member is building
/// a Wonder, each partner may pledge the Iron build labor of its most populous town to it for the season. That is delivered labor, never multiplied. The Wonder is
/// the builder's alone, and so is its Legacy; a partner earns only goodwill. A member that goes to war with a partner, or loses the alliance, leaves.
/// </summary>
public sealed partial class PoliticsSystem
{
    private const int MaxLeagueMembers = 4;
    private readonly List<League> _leagues = new();

    public IReadOnlyList<League> Leagues => _leagues;

    public int LeagueOf(int civ)
    {
        for (int i = 0; i < _leagues.Count; i++) if (_leagues[i].Members.Contains(civ)) return i;
        return -1;
    }

    /// <summary>
    /// Found a league or bring a partner into one. Hard rules: DIP-07 for the proposer, an alliance with the partner (and with every member if joining a league), no war,
    /// the partner not already in another league, at most four members.
    /// </summary>
    public bool OrderLeague(int civ, int partner)
    {
        if (!_economy.IronChain || (uint)civ >= (uint)_civs || (uint)partner >= (uint)_civs || civ == partner) return false;
        if (_p.Dip07 < 0 || !_research.IsDone(civ, _p.Dip07) || !_dip.Allied(civ, partner) || _dip.AtWar(civ, partner)) return false;
        int mine = LeagueOf(civ), theirs = LeagueOf(partner);
        if (mine >= 0 && mine == theirs) return false;
        if (mine >= 0 && theirs >= 0) return false;
        if (mine < 0 && theirs < 0)
        {
            var league = new League();
            league.Members.Add(Math.Min(civ, partner));
            league.Members.Add(Math.Max(civ, partner));
            _leagues.Add(league);
        }
        else
        {
            League into = _leagues[mine >= 0 ? mine : theirs];
            int joiner = mine >= 0 ? partner : civ;
            if (into.Members.Count >= MaxLeagueMembers) return false;
            foreach (int member in into.Members) if (!_dip.Allied(joiner, member) || _dip.AtWar(joiner, member)) return false;
            into.Members.Add(joiner);
            into.Members.Sort();
        }
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.LeagueJoined, partner, _leagues[LeagueOf(civ)].Members.Count, 0));
        _events.Add(new PoliticsEvent(_tick, (byte)partner, PoliticsEventKind.LeagueJoined, civ, _leagues[LeagueOf(civ)].Members.Count, 1));
        return true;
    }

    /// <summary>A season of league life: members who have fallen out of the alliance leave; partners pledge labor to a member's Wonder in progress.</summary>
    private void AdvanceLeagues()
    {
        if (!_economy.IronChain) return;
        for (int i = _leagues.Count - 1; i >= 0; i--)
        {
            League league = _leagues[i];
            for (int a = league.Members.Count - 1; a >= 0; a--)
            {
                bool fallenOut = false;
                foreach (int other in league.Members)
                    if (other != league.Members[a] && (!_dip.Allied(league.Members[a], other) || _dip.AtWar(league.Members[a], other))) { fallenOut = true; break; }
                if (!fallenOut) continue;
                _events.Add(new PoliticsEvent(_tick, (byte)league.Members[a], PoliticsEventKind.LeagueEnded, -1, league.Members.Count, 0));
                league.Members.RemoveAt(a);
            }
            if (league.Members.Count < 2) _leagues.RemoveAt(i);
        }
        // Pledges are decided fresh each season: first none, then each partner of a builder.
        for (int s = 0; s < _sCount; s++) _economy.SetWonderPledge(s, -1);
        foreach (League league in _leagues)
        {
            foreach (int builder in league.Members)
            {
                int site = _economy.WonderUnderConstruction(builder);
                if (site < 0) continue;
                foreach (int partner in league.Members)
                {
                    if (partner == builder) continue;
                    int contributor = _economy.LeagueContributor(partner);
                    if (contributor < 0 || _economy.WonderUnderConstruction(partner) >= 0) continue; // a partner building its own Wonder keeps its labor for it
                    _economy.SetWonderPledge(contributor, site);
                    _dip.Shift(partner, builder, 1);
                }
            }
        }
    }

    private void HashLeagues(ref StateHasher h)
    {
        h.Add(_leagues.Count);
        foreach (League league in _leagues) { h.Add(league.Members.Count); foreach (int m in league.Members) h.Add(m); }
    }

    private void WriteLeagues(BinaryWriter w)
    {
        w.Write(_leagues.Count);
        foreach (League league in _leagues) { w.Write(league.Members.Count); foreach (int m in league.Members) w.Write(m); }
    }

    private void ReadLeagues(BinaryReader r)
    {
        _leagues.Clear();
        int count = r.ReadInt32();
        if (count < 0 || count > _civs) throw new InvalidDataException("Snapshot league count is invalid.");
        for (int i = 0; i < count; i++)
        {
            var league = new League();
            int members = r.ReadInt32();
            if (members < 2 || members > MaxLeagueMembers) throw new InvalidDataException("Snapshot league size is invalid.");
            for (int m = 0; m < members; m++)
            {
                int civ = r.ReadInt32();
                if ((uint)civ >= (uint)_civs || m > 0 && league.Members[m - 1] >= civ) throw new InvalidDataException("Snapshot league member or order is invalid.");
                league.Members.Add(civ);
            }
            _leagues.Add(league);
        }
    }

    internal void FixtureAdvanceLeagues() => AdvanceLeagues();
    internal void FixtureAlly(int a, int b) => _dip.Ally(a, b);
}
