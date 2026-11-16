using System;
using System.IO;
using CivSim.Sim.Economy;

namespace CivSim.Sim.Politics;

/// <summary>Fief-only leadership identity and DIP-08 alliance continuity across succession.</summary>
public sealed partial class PoliticsSystem
{
    private long[] _fiefLeaderIds = Array.Empty<long>();

    private void InitializeFiefLeadership()
    {
        if (!_economy.FiefChain) return;
        _fiefLeaderIds = new long[_civs];
        Array.Fill(_fiefLeaderIds, -1L);
        for (int civ = 0; civ < _civs; civ++) _fiefLeaderIds[civ] = ElectFiefLeader(civ);
    }

    /// <summary>Neutral seniority: oldest living adult, then lowest stable FigureId.</summary>
    private long ElectFiefLeader(int civ)
    {
        long bestId = -1;
        int bestAge = -1;
        for (int i = 0; i < _economy.FigureCount; i++)
        {
            FigureLook figure = _economy.LookFigure(i);
            if (figure.Civilization != civ || figure.Age < 14) continue;
            if (figure.Age > bestAge || figure.Age == bestAge && (bestId < 0 || figure.Id < bestId))
            {
                bestAge = figure.Age;
                bestId = figure.Id;
            }
        }
        return bestId;
    }

    /// <summary>Consumes the economy's just-completed-season mortality events exactly once at the politics season boundary.</summary>
    private void AdvanceFiefLeadership()
    {
        if (!_economy.FiefChain) return;
        if (_fiefLeaderIds.Length != _civs) InitializeFiefLeadership();
        int dip08 = _catalog.IndexOf("DIP-08");
        for (int i = 0; i < _economy.LastDeathEventCount; i++)
        {
            EconomyDeathEvent death = _economy.LastDeathEvent(i);
            int civ = death.Civilization;
            if ((uint)civ >= (uint)_civs || _fiefLeaderIds[civ] != death.FigureId) continue;

            _fiefLeaderIds[civ] = -1;
            bool successionKeepsAlliances = dip08 >= 0 && _research.IsDone(civ, dip08);
            int preserved = 0;
            for (int other = 0; other < _civs; other++) if (other != civ && _dip.Allied(civ, other)) preserved++;
            FiefWp7TraceObserverForDiagnostics?.Invoke($"wp7-dip08 seed={_seed} season={_season} civ={civ} leader={death.FigureId} successorPending=true dip08={successionKeepsAlliances.ToString().ToLowerInvariant()} alliancesBefore={preserved} event=leader_succession");
            if (!successionKeepsAlliances)
            {
                for (int other = 0; other < _civs; other++)
                {
                    if (other == civ || !_dip.Allied(civ, other)) continue;
                    // Leave Relations at the alliance floor so the Civ Mind cannot reform the lapsed pact immediately.
                    _dip.Set(civ, other, Diplomacy.AllianceFloor);
                    _dip.BreakAlliance(civ, other);
                }
            }
            _fiefLeaderIds[civ] = ElectFiefLeader(civ);
        }
    }

    private void HashFiefLeadership(ref Core.StateHasher h)
    {
        if (!_economy.FiefChain) return;
        h.Add(0x44495030384C4452UL);
        h.Add(_fiefLeaderIds.Length);
        foreach (long id in _fiefLeaderIds) h.Add(id);
    }

    private void WriteFiefLeadership(BinaryWriter writer)
    {
        if (!_economy.FiefChain) return;
        writer.Write(_fiefLeaderIds.Length);
        foreach (long id in _fiefLeaderIds) writer.Write(id);
    }

    private void ReadFiefLeadership(BinaryReader reader)
    {
        if (!_economy.FiefChain) return;
        int count = reader.ReadInt32();
        if (count != _civs) throw new InvalidDataException("Fief leader table has the wrong civilization count.");
        _fiefLeaderIds = new long[count];
        for (int civ = 0; civ < count; civ++)
        {
            long id = reader.ReadInt64();
            if (id < -1 || id >= _economy.NextFigureId) throw new InvalidDataException("Fief leader is not a real FigureId.");
            _fiefLeaderIds[civ] = id;
        }
    }

    internal long FixtureFiefLeaderId(int civ)
        => _economy.FiefChain && (uint)civ < (uint)_fiefLeaderIds.Length ? _fiefLeaderIds[civ] : -1;

    internal void FixtureAdvanceFiefLeadership() => AdvanceFiefLeadership();
}
