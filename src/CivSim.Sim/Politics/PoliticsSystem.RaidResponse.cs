using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

public sealed partial class PoliticsSystem
{
    private const int RaidWithdrawalTraceLimit = 512;
    private const int RaidWithdrawalDecisionTraceLimit = 128;
    private readonly List<RaidResponse> _raidResponses = new();
    private readonly List<RaidResponse> _activeRaidResponses = new();
    private readonly List<int> _raidResponseEffectiveSeasons = new();
    private readonly List<int> _lastRaidObservedSeason = new();
    private readonly List<int> _lastRaidObservedAttackers = new();
    private readonly List<int> _lastRaidObservedByCiv = new();
    private readonly List<string> _raidWithdrawalTracesForDiagnostics = new();
    private int _raidWithdrawalDecisionTraceCount;

    public bool RaidWithdrawalTraceEnabledForDiagnostics { get; set; }
    public bool[] RaidWithdrawalTraceCivsForDiagnostics { get; set; } = Array.Empty<bool>();
    public IReadOnlyList<string> RaidWithdrawalTracesForDiagnostics => _raidWithdrawalTracesForDiagnostics;

    internal bool IsRaidWithdrawalTraceTargetForDiagnostics(int civ) =>
        RaidWithdrawalTraceEnabledForDiagnostics && (uint)civ < (uint)RaidWithdrawalTraceCivsForDiagnostics.Length &&
        RaidWithdrawalTraceCivsForDiagnostics[civ];

    internal void AddRaidWithdrawalDecisionTraceForDiagnostics(string trace)
    {
        if (!RaidWithdrawalTraceEnabledForDiagnostics ||
            _raidWithdrawalDecisionTraceCount >= RaidWithdrawalDecisionTraceLimit ||
            _raidWithdrawalTracesForDiagnostics.Count >= RaidWithdrawalTraceLimit) return;
        _raidWithdrawalTracesForDiagnostics.Add(trace);
        _raidWithdrawalDecisionTraceCount++;
    }

    internal void AddRaidWithdrawalArrivalTraceForDiagnostics(string trace)
    {
        if (!RaidWithdrawalTraceEnabledForDiagnostics) return;
        if (_raidWithdrawalTracesForDiagnostics.Count >= RaidWithdrawalTraceLimit)
        {
            int decisionIndex = _raidWithdrawalTracesForDiagnostics.FindIndex(row =>
                row.StartsWith("RAID_WITHDRAWAL_DECISION ", StringComparison.Ordinal));
            if (decisionIndex < 0) return;
            _raidWithdrawalTracesForDiagnostics.RemoveAt(decisionIndex);
            _raidWithdrawalDecisionTraceCount--;
        }
        _raidWithdrawalTracesForDiagnostics.Add(trace);
    }

    /// <summary>Current response mode; changes become effective at their recorded season.</summary>
    public RaidResponse RaidResponseOf(int settlement)
        => (uint)settlement < (uint)_activeRaidResponses.Count ? _activeRaidResponses[settlement] : RaidResponse.Stand;

    private void ApplyPendingRaidResponses()
    {
        EnsureRaidResponseCapacity(_sCount);
        for (int i = 0; i < _sCount; i++)
            if (_season >= _raidResponseEffectiveSeasons[i]) _activeRaidResponses[i] = _raidResponses[i];
    }

    public int LastRaidObservedSeasonOf(int settlement)
        => (uint)settlement < (uint)_lastRaidObservedSeason.Count ? _lastRaidObservedSeason[settlement] : -1000;

    public int LastRaidObservedAttackersOf(int settlement)
        => (uint)settlement < (uint)_lastRaidObservedAttackers.Count ? _lastRaidObservedAttackers[settlement] : 0;

    public int LastRaidObservedByCivOf(int settlement)
        => (uint)settlement < (uint)_lastRaidObservedByCiv.Count ? _lastRaidObservedByCiv[settlement] : -1;

    /// <summary>Exact defenders for a settlement owned by <paramref name="civ"/>; no rival roster is exposed.</summary>
    public IReadOnlyList<Fighter> OwnDefendersOf(int civ, int settlement)
    {
        if ((uint)civ >= (uint)_civs || (uint)settlement >= (uint)_sCount || _sCiv[settlement] != civ || _free[settlement])
            return Array.Empty<Fighter>();
        return Fighters(settlement, attacking: false, out _);
    }

    public bool OrderRaidResponse(int civ, int settlement, RaidResponse response)
    {
        if ((uint)civ >= (uint)_civs || (uint)settlement >= (uint)_sCount || _sCiv[settlement] != civ ||
            _free[settlement] || _sPop[settlement] <= 0 || response is not (RaidResponse.Stand or RaidResponse.Withdraw))
            return false;
        EnsureRaidResponseCapacity(_sCount);
        _raidResponses[settlement] = response;
        _raidResponseEffectiveSeasons[settlement] = _season + 1;
        if (response == RaidResponse.Withdraw)
            RecordVisibleRaidWarnings(civ, settlement, _season);
        return true;
    }

    /// <summary>Records the exact expedition seen by a belief update or a visible pending raid response.</summary>
    internal void RecordRaidWarning(Expedition? expedition, int civ, int settlement, int season)
    {
        EnsureRaidResponseCapacity(_sCount);
        if (expedition is null || expedition.Kind != ExpeditionKind.Raid || expedition.TargetParty >= 0 ||
            expedition.TargetCampId >= 0 || expedition.ToSettlement != settlement || expedition.TargetCiv != civ ||
            (uint)settlement >= (uint)_sCount || _sCiv[settlement] != civ || _free[settlement] ||
            expedition.ArriveSeason <= season || !WillWithdrawBeforeArrival(settlement, expedition.ArriveSeason)) return;

        if (expedition.WarnedTargetCiv == civ && expedition.WarnedTargetSettlement == settlement &&
            expedition.WarningSeason >= 0 && expedition.WarningSeason <= season) return;
        expedition.WarnedTargetCiv = civ;
        expedition.WarnedTargetSettlement = settlement;
        expedition.WarningSeason = season;
    }

    private bool WillWithdrawBeforeArrival(int settlement, int arrivalSeason)
    {
        if ((uint)settlement >= (uint)_activeRaidResponses.Count) return false;
        if (_raidResponseEffectiveSeasons[settlement] <= arrivalSeason)
            return _raidResponses[settlement] == RaidResponse.Withdraw;
        return _activeRaidResponses[settlement] == RaidResponse.Withdraw;
    }

    private void RecordVisibleRaidWarningsForActiveWithdrawals()
    {
        for (int i = 0; i < _expeditions.Count; i++)
        {
            Expedition e = _expeditions[i];
            int settlement = e.ToSettlement;
            if (e.Kind != ExpeditionKind.Raid || e.TargetParty >= 0 || e.TargetCampId >= 0 ||
                (uint)settlement >= (uint)_sCount || _activeRaidResponses[settlement] != RaidResponse.Withdraw ||
                !IsVisibleIncomingRaid(e, settlement)) continue;
            RecordRaidWarning(e, _sCiv[settlement], settlement, _season);
        }
    }

    /// <summary>
    /// The player-command sight check shares the belief's routine range, current scout sightings, and arrival window.
    /// The AI belief path calls <see cref="RecordRaidWarning"/> with its exact observed expedition when it also used
    /// its prior-season expedition-visit memory.
    /// </summary>
    private bool IsVisibleIncomingRaid(Expedition e, int settlement)
    {
        int civ = _sCiv[settlement];
        if (e.Civ == civ || e.Civ < 0 || e.Civ >= _civs || !_dip.Aware(civ, e.Civ) ||
            (uint)e.FromSettlement >= (uint)_sCount) return false;
        int routine = BeliefRules.RoutineSight(ReachOf(civ));
        int remaining = (e.ArriveSeason - _season) * TilesPerSeason;
        if (remaining <= routine) return true;

        int originTile = _sTile[e.FromSettlement];
        for (int own = 0; own < _sCount; own++)
            if (_sCiv[own] == civ && !_free[own] && HexGrid.Distance(_sTile[own], originTile, _width) <= routine)
                return true;
        return ScoutMapOwnerSeen(civ, originTile).Season >= _season;
    }

    private void RecordVisibleRaidWarnings(int civ, int settlement, int season)
    {
        for (int i = 0; i < _expeditions.Count; i++)
        {
            Expedition e = _expeditions[i];
            if (e.ToSettlement == settlement && IsVisibleIncomingRaid(e, settlement))
                RecordRaidWarning(e, civ, settlement, season);
        }
    }

    private bool ApplyRaidResponseCommand(Command command)
    {
        RaidResponse response = (RaidResponse)command.B;
        bool validTarget = (uint)command.A < (uint)_sCount && _sCiv[command.A] == command.Civ &&
            !_free[command.A] && _sPop[command.A] > 0;
        if (!validTarget || response is not (RaidResponse.Stand or RaidResponse.Withdraw))
            Refused(command, command.A, OrderRefusalReason.InvalidTarget);
        else OrderRaidResponse(command.Civ, command.A, response);
        return validTarget;
    }

    private void ObserveSettlementRaid(int settlement, int season, int attackers, int byCiv)
    {
        EnsureRaidResponseCapacity(_sCount);
        _lastRaidObservedSeason[settlement] = season;
        _lastRaidObservedAttackers[settlement] = attackers;
        _lastRaidObservedByCiv[settlement] = byCiv;
    }

    private void EnsureRaidResponseCapacity(int count)
    {
        while (_raidResponses.Count < count)
        {
            _raidResponses.Add(RaidResponse.Stand);
            _activeRaidResponses.Add(RaidResponse.Stand);
            _raidResponseEffectiveSeasons.Add(0);
            _lastRaidObservedSeason.Add(-1000);
            _lastRaidObservedAttackers.Add(0);
            _lastRaidObservedByCiv.Add(-1);
        }
    }

    private void HashRaidResponses(ref StateHasher h)
    {
        int count = Math.Max(_sCount, _economy.SettlementCount);
        EnsureRaidResponseCapacity(count);
        bool active = false;
        for (int i = 0; i < count; i++)
            if (_raidResponses[i] != RaidResponse.Stand || _activeRaidResponses[i] != RaidResponse.Stand ||
                _raidResponseEffectiveSeasons[i] != 0 || _lastRaidObservedSeason[i] != -1000 ||
                _lastRaidObservedAttackers[i] != 0 || _lastRaidObservedByCiv[i] != -1)
            { active = true; break; }
        if (!active) return;
        h.Add(0x524149445253504EUL);
        h.Add(count);
        for (int i = 0; i < count; i++)
        {
            h.Add((byte)_raidResponses[i]); h.Add(_raidResponseEffectiveSeasons[i]);
            h.Add((byte)_activeRaidResponses[i]);
            h.Add(_lastRaidObservedSeason[i]); h.Add(_lastRaidObservedAttackers[i]); h.Add(_lastRaidObservedByCiv[i]);
        }
    }

    private void WriteRaidResponses(BinaryWriter w)
    {
        int count = Math.Max(_sCount, _economy.SettlementCount);
        EnsureRaidResponseCapacity(count);
        w.Write(count);
        for (int i = 0; i < count; i++)
        {
            w.Write((byte)_raidResponses[i]); w.Write(_raidResponseEffectiveSeasons[i]);
            w.Write((byte)_activeRaidResponses[i]);
            w.Write(_lastRaidObservedSeason[i]); w.Write(_lastRaidObservedAttackers[i]); w.Write(_lastRaidObservedByCiv[i]);
        }
    }

    private void ReadRaidResponses(BinaryReader r)
    {
        _raidResponses.Clear(); _activeRaidResponses.Clear(); _raidResponseEffectiveSeasons.Clear();
        _lastRaidObservedSeason.Clear(); _lastRaidObservedAttackers.Clear(); _lastRaidObservedByCiv.Clear();
        int count = r.ReadInt32();
        if (count < 0 || count > 1_000_000) throw new InvalidDataException("Snapshot contains invalid raid-response state.");
        for (int i = 0; i < count; i++)
        {
            byte response = r.ReadByte(); int effective = r.ReadInt32(), active = r.ReadByte();
            int observed = r.ReadInt32(), attackers = r.ReadInt32(), byCiv = r.ReadInt32();
            if (response > (byte)RaidResponse.Withdraw || active > (int)RaidResponse.Withdraw || effective < 0 || observed < -1000 || attackers < 0 || byCiv < -1 || byCiv >= _civs)
                throw new InvalidDataException("Snapshot contains invalid raid-response state.");
            _raidResponses.Add((RaidResponse)response); _raidResponseEffectiveSeasons.Add(effective);
            _activeRaidResponses.Add((RaidResponse)active);
            _lastRaidObservedSeason.Add(observed); _lastRaidObservedAttackers.Add(attackers);
            _lastRaidObservedByCiv.Add(byCiv);
        }
        if (count < _economy.SettlementCount) throw new InvalidDataException("Snapshot omits settlement raid-response state.");
    }
}
