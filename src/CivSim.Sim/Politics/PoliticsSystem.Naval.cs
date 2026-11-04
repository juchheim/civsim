using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>A galley holding station off an enemy harbor (SEA-09, docs/MVP/25 §7).</summary>
public sealed class Blockade
{
    public int Civ { get; internal set; }
    public int TargetCiv { get; internal set; }
    /// <summary>The blockaded harbor's settlement.</summary>
    public int Harbor { get; internal set; }
    /// <summary>The blockader's own harbor, where the galley and its crew belong.</summary>
    public int Origin { get; internal set; }
    public int BeganSeason { get; internal set; }
}

/// <summary>
/// The bounded harbor blockade (docs/MVP/25 §7): it needs a surviving galley with a crew of four supplied soldiers, a declared war, a known harbor within sailing
/// reach and the galley's continuous presence; it shuts that harbor's sea routes and nothing else. A ship cannot hold two harbors, and the harbor's owner may
/// contest it with a galley of its own. The galley can be wrecked on station. There is no tactical naval control.
/// </summary>
public sealed partial class PoliticsSystem
{
    private static readonly ulong NavalStream = Hash64.OfString("politics.naval");
    private readonly List<Blockade> _blockades = new();

    /// <summary>Tiles a blockading galley may sail from its home harbor to its station: far enough to cross to another continent (the start towns are 50 to 90 sailing tiles apart; provisional).</summary>
    private const int BlockadeSailReach = 100;
    /// <summary>A relief galley must lie within this many tiles of the blockaded harbor to contest it (provisional).</summary>
    private const int BlockadeReliefTiles = 8;

    public IReadOnlyList<Blockade> Blockades => _blockades;

    public bool IsBlockaded(int harbor)
    {
        foreach (Blockade b in _blockades) if (b.Harbor == harbor) return true;
        return false;
    }

    private bool BlockadingFrom(int origin)
    {
        foreach (Blockade b in _blockades) if (b.Origin == origin) return true;
        return false;
    }

    /// <summary>A route through a blockaded harbor that goes by sea is shut; an overland route to the same town is not.</summary>
    private bool BlockadeCutsRoute(int from, int to)
        => _blockades.Count > 0 && (IsBlockaded(from) || IsBlockaded(to)) && WalkFrom(_sTile[from])[_sTile[to]] == int.MaxValue;

    /// <summary>Station a galley off an enemy harbor. Hard rules: war with its owner, a known harbor, a crewed free galley within sailing reach.</summary>
    public bool OrderBlockade(int civ, int harbor)
    {
        string? why = BlockadeRefusal(civ, harbor, out int origin);
        if (why is not null) return false;
        int other = _sCiv[harbor];
        _blockades.Add(new Blockade { Civ = civ, TargetCiv = other, Harbor = harbor, Origin = origin, BeganSeason = _season });
        _blockades.Sort((a, b) => a.Harbor.CompareTo(b.Harbor));
        CutSeaCaravans(harbor);
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.BlockadeBegan, other, harbor, origin));
        _events.Add(new PoliticsEvent(_tick, (byte)other, PoliticsEventKind.BlockadeBegan, civ, harbor, origin));
        return true;
    }

    /// <summary>Why a blockade order cannot be carried out (null when it can), and the galley's home harbor when it can.</summary>
    internal string? BlockadeRefusal(int civ, int harbor, out int origin)
    {
        origin = -1;
        if (!_economy.IronChain) return "chapter";
        if ((uint)civ >= (uint)_civs || (uint)harbor >= (uint)_sCount || _free[harbor]) return "target";
        if (_p.Sea09 < 0 || !_research.IsDone(civ, _p.Sea09)) return "SEA-09";
        if (!CanSail(civ)) return "SEA-04";
        int other = _sCiv[harbor];
        if (other == civ) return "own harbor";
        if (!_dip.AtWar(civ, other)) return "not at war";
        if (!_economy.HasHarbor(harbor)) return "no harbor";
        if (IsBlockaded(harbor)) return "already blockaded";
        if (!KnowsTile(civ, _sTile[harbor])) return "unknown harbor";
        for (int s = 0; s < _sCount && origin < 0; s++)
        {
            if (_sCiv[s] != civ || _free[s] || !_economy.CrewedGalley(s) || BlockadingFrom(s)) continue;
            int cost = SailFrom(_sTile[s])[_sTile[harbor]];
            if (cost != int.MaxValue && (cost + 9) / 10 <= BlockadeSailReach) origin = s;
        }
        return origin < 0 ? "no galley in reach" : null;
    }

    /// <summary>Caravans bound to or from a harbor that go by sea turn back when the blockade begins; their cargo goes home.</summary>
    private void CutSeaCaravans(int harbor)
    {
        for (int i = _expeditions.Count - 1; i >= 0; i--)
        {
            Expedition caravan = _expeditions[i];
            if (caravan.Kind != ExpeditionKind.Caravan || caravan.FromSettlement != harbor && caravan.ToSettlement != harbor) continue;
            if ((uint)caravan.FromSettlement >= (uint)_sCount || (uint)caravan.ToSettlement >= (uint)_sCount) continue;
            if (WalkFrom(_sTile[caravan.FromSettlement])[_sTile[caravan.ToSettlement]] != int.MaxValue) continue;
            Unload(caravan.FromSettlement, caravan.Good, caravan.Cargo);
            _expeditions.RemoveAt(i);
        }
    }

    private Fix64 CrewStrength(int settlement)
    {
        List<Fighter> fighters = Fighters(settlement, attacking: false, out _, _sTile[settlement]);
        fighters.Sort((x, y) => x.Strength != y.Strength ? y.Strength.CompareTo(x.Strength) : x.Id.CompareTo(y.Id));
        Fix64 total = Fix64.Zero;
        for (int i = 0; i < Math.Min(Sieges.CrewPerEngine, fighters.Count); i++) total += fighters[i].Strength;
        return total;
    }

    private void EndBlockade(int index, int outcome)
    {
        Blockade b = _blockades[index];
        _events.Add(new PoliticsEvent(_tick, (byte)b.Civ, PoliticsEventKind.BlockadeEnded, b.TargetCiv, b.Harbor, outcome));
        _events.Add(new PoliticsEvent(_tick, (byte)b.TargetCiv, PoliticsEventKind.BlockadeEnded, b.Civ, b.Harbor, outcome));
        _blockades.RemoveAt(index);
    }

    /// <summary>A season on station: the blockade lapses with the war, the harbor, the galley or its crew; a wreck or a relief galley can end it.</summary>
    private void AdvanceBlockades()
    {
        for (int i = _blockades.Count - 1; i >= 0; i--)
        {
            Blockade b = _blockades[i];
            bool standing = !_free[b.Harbor] && _sCiv[b.Harbor] == b.TargetCiv && _dip.AtWar(b.Civ, b.TargetCiv) && _economy.HasHarbor(b.Harbor) &&
                            !_free[b.Origin] && _sCiv[b.Origin] == b.Civ && _economy.CrewedGalley(b.Origin);
            if (!standing) { EndBlockade(i, 0); continue; }
            if (b.BeganSeason >= _season) continue;

            // Weather and rocks off the harbor: the base shipwreck odds of any voyage for each season on station (the passage out is not rolled; provisional).
            if (RollVoyageLost(VoyageBaseRiskPermille, (ulong)(b.Origin * 8191L + b.Harbor) * 37UL + 5UL, _season, b.Civ))
            {
                _economy.LoseGalley(b.Origin);
                EndBlockade(i, 2);
                continue;
            }

            // The harbor's owner may meet the galley with one of its own, from the harbor itself or another of its ports close by.
            int relief = -1;
            for (int s = 0; s < _sCount && relief < 0; s++)
            {
                if (_sCiv[s] != b.TargetCiv || _free[s] || !_economy.CrewedGalley(s)) continue;
                if (HexGrid.Distance(_sTile[s], _sTile[b.Harbor], _width) <= BlockadeReliefTiles) relief = s;
            }
            if (relief < 0) continue;
            Fix64 attacker = CrewStrength(b.Origin), defender = CrewStrength(relief) * Fix64.FromRatio(11, 10); // home waters
            Pcg32 rng = Pcg32.ForStream(_seed, NavalStream ^ 0x52454C494546UL, (ulong)b.Harbor, _season);
            bool attackerWins = attacker + defender > Fix64.Zero && rng.NextFix01() < attacker / (attacker + defender);
            if (attackerWins) _economy.LoseGalley(relief);
            else { _economy.LoseGalley(b.Origin); EndBlockade(i, 1); }
        }
    }

    private void HashBlockades(ref StateHasher h)
    {
        h.Add(_blockades.Count);
        foreach (Blockade b in _blockades) { h.Add(b.Civ); h.Add(b.TargetCiv); h.Add(b.Harbor); h.Add(b.Origin); h.Add(b.BeganSeason); }
    }

    private void WriteBlockades(BinaryWriter w)
    {
        w.Write(_blockades.Count);
        foreach (Blockade b in _blockades) { w.Write(b.Civ); w.Write(b.TargetCiv); w.Write(b.Harbor); w.Write(b.Origin); w.Write(b.BeganSeason); }
    }

    private void ReadBlockades(BinaryReader r)
    {
        _blockades.Clear();
        int count = r.ReadInt32();
        if (count < 0 || count > _sCount) throw new InvalidDataException("Snapshot blockade count is invalid.");
        for (int i = 0; i < count; i++)
        {
            var b = new Blockade { Civ = r.ReadInt32(), TargetCiv = r.ReadInt32(), Harbor = r.ReadInt32(), Origin = r.ReadInt32(), BeganSeason = r.ReadInt32() };
            if ((uint)b.Civ >= (uint)_civs || (uint)b.TargetCiv >= (uint)_civs || b.Civ == b.TargetCiv || (uint)b.Harbor >= (uint)_sCount ||
                (uint)b.Origin >= (uint)_sCount || i > 0 && _blockades[i - 1].Harbor >= b.Harbor)
                throw new InvalidDataException("Snapshot blockade fields or order are invalid.");
            _blockades.Add(b);
        }
    }

    /// <summary>Marks a tile as known to a civilization (a scout or voyage has seen it), for fixtures.</summary>
    internal void FixtureReveal(int civ, int tile) => (_revealed[civ] ??= new bool[_tileArray.Length])[tile] = true;

    internal int FixtureSailTiles(int from, int to) { int c = SailFrom(_sTile[from])[_sTile[to]]; return c == int.MaxValue ? -1 : (c + 9) / 10; }

    internal bool FixtureBlockadeCutsRoute(int from, int to) => BlockadeCutsRoute(from, to);
}
