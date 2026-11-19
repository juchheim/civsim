using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

public sealed partial class LivingEconomyModel
{
    private const uint KnowHowRetentionMagic = 0x48574F4B; // "KOWH"
    private readonly List<Fix64[]?> _fiefKnowHowBuckets = new();
    private Fix64[][]? _fiefKnowHowReserve;
    private bool _fiefKnowHowNeedsMigration;

    /// <summary>Raised synchronously when an unprotected local Know-how bucket is discarded.</summary>
    internal Action<int, int, Fix64>? FiefKnowHowDiscarded { get; set; }

    internal void EnsureFiefKnowHowState(int lineages)
    {
        if (!FiefChain) return;
        if (lineages <= 0) throw new ArgumentOutOfRangeException(nameof(lineages));
        while (_fiefKnowHowBuckets.Count < _settlements.Count) _fiefKnowHowBuckets.Add(null);
        if (_fiefKnowHowReserve is null)
        {
            _fiefKnowHowReserve = new Fix64[CivilizationCount][];
            for (int c = 0; c < _fiefKnowHowReserve.Length; c++) _fiefKnowHowReserve[c] = new Fix64[lineages];
        }
        else
        {
            for (int c = 0; c < _fiefKnowHowReserve.Length; c++)
                if (_fiefKnowHowReserve[c].Length != lineages) throw new InvalidOperationException("Know-how lineage count changed.");
        }
        foreach (Fix64[]? bucket in _fiefKnowHowBuckets)
            if (bucket is not null && bucket.Length != lineages) throw new InvalidOperationException("Know-how lineage count changed.");
    }

    internal void AddFiefKnowHow(int settlementId, int lineage, Fix64 amount)
    {
        if (amount <= Fix64.Zero) return;
        if ((uint)settlementId >= (uint)_settlements.Count) throw new ArgumentOutOfRangeException(nameof(settlementId));
        while (_fiefKnowHowBuckets.Count <= settlementId) _fiefKnowHowBuckets.Add(null);
        Fix64[]? bucket = _fiefKnowHowBuckets[settlementId];
        if (bucket is null) _fiefKnowHowBuckets[settlementId] = bucket = new Fix64[_fiefKnowHowReserve![0].Length];
        bucket[lineage] += amount;
    }

    internal Fix64 FiefKnowHowBucket(int settlementId, int lineage)
        => (uint)settlementId < (uint)_fiefKnowHowBuckets.Count && _fiefKnowHowBuckets[settlementId] is { } bucket
            ? bucket[lineage] : Fix64.Zero;

    internal Fix64 FiefKnowHowReserve(int civ, int lineage)
        => _fiefKnowHowReserve is { } reserve ? reserve[civ][lineage] : Fix64.Zero;

    internal void AddFiefKnowHowReserve(int civ, int lineage, Fix64 amount)
    {
        if (amount > Fix64.Zero) _fiefKnowHowReserve![civ][lineage] += amount;
    }

    internal void DebitFiefKnowHow(int civ, int lineage, Fix64 amount)
    {
        if (!FiefChain || amount <= Fix64.Zero) return;
        Fix64 remaining = amount;
        for (int settlementId = 0; settlementId < _fiefKnowHowBuckets.Count; settlementId++)
        {
            Fix64[]? bucket = _fiefKnowHowBuckets[settlementId];
            if (bucket is null || _settlements[settlementId].Civilization != civ) continue;
            Fix64 debit = Fix64.Min(remaining, bucket[lineage]);
            bucket[lineage] -= debit;
            remaining -= debit;
            if (remaining <= Fix64.Zero) return;
        }
        Fix64 reserveDebit = Fix64.Min(remaining, _fiefKnowHowReserve![civ][lineage]);
        _fiefKnowHowReserve[civ][lineage] -= reserveDebit;
    }

    private void RemoveFiefKnowHowOnOwnerExit(SettlementState settlement)
    {
        if (!FiefChain || (uint)settlement.Id >= (uint)_fiefKnowHowBuckets.Count || _fiefKnowHowBuckets[settlement.Id] is not { } bucket) return;
        _fiefKnowHowBuckets[settlement.Id] = null;
        int formerCiv = settlement.Civilization;
        bool staffed = IronWorksCount(settlement, IronStructure.Monastery) > 0 && MonksAt(settlement.Id) >= 4;
        for (int lineage = 0; lineage < bucket.Length; lineage++)
        {
            Fix64 amount = bucket[lineage];
            if (amount <= Fix64.Zero) continue;
            if (staffed) _fiefKnowHowReserve![formerCiv][lineage] += amount;
            else FiefKnowHowDiscarded?.Invoke(formerCiv, lineage, amount);
        }
    }

    internal bool FiefKnowHowNeedsMigration => _fiefKnowHowNeedsMigration;

    internal void MigrateFiefKnowHow(int lineageCount, Func<int, int, Fix64> pool)
    {
        if (!FiefChain || !_fiefKnowHowNeedsMigration) return;
        EnsureFiefKnowHowState(lineageCount);
        for (int civ = 0; civ < CivilizationCount; civ++)
        {
            int destination = Capital(civ);
            if (destination < 0)
                for (int i = 0; i < _settlements.Count; i++)
                    if (_settlements[i].Civilization == civ) { destination = i; break; }
            for (int lineage = 0; lineage < lineageCount; lineage++)
            {
                Fix64 amount = pool(civ, lineage);
                if (amount <= Fix64.Zero) continue;
                if (destination >= 0)
                {
                    while (_fiefKnowHowBuckets.Count <= destination) _fiefKnowHowBuckets.Add(null);
                    Fix64[]? bucket = _fiefKnowHowBuckets[destination];
                    if (bucket is null) _fiefKnowHowBuckets[destination] = bucket = new Fix64[lineageCount];
                    bucket[lineage] += amount;
                }
                else _fiefKnowHowReserve![civ][lineage] += amount;
            }
        }
        _fiefKnowHowNeedsMigration = false;
    }

    private void HashFiefKnowHow(ref StateHasher hash)
    {
        if (!FiefChain) return;
        hash.Add(KnowHowRetentionMagic);
        int count = 0;
        for (int i = 0; i < _fiefKnowHowBuckets.Count; i++) if (_fiefKnowHowBuckets[i] is not null) count++;
        hash.Add(count);
        for (int i = 0; i < _fiefKnowHowBuckets.Count; i++)
        {
            if (_fiefKnowHowBuckets[i] is not { } bucket) continue;
            hash.Add(i); hash.Add(bucket.Length); hash.Add(bucket);
        }
        hash.Add(_fiefKnowHowReserve?.Length ?? 0);
        if (_fiefKnowHowReserve is not null)
            foreach (Fix64[] reserve in _fiefKnowHowReserve) { hash.Add(reserve.Length); hash.Add(reserve); }
    }

    private void WriteFiefKnowHow(BinaryWriter writer)
    {
        if (!FiefChain) return;
        writer.Write(KnowHowRetentionMagic);
        int count = 0;
        for (int i = 0; i < _fiefKnowHowBuckets.Count; i++) if (_fiefKnowHowBuckets[i] is not null) count++;
        writer.Write(count);
        for (int i = 0; i < _fiefKnowHowBuckets.Count; i++)
        {
            if (_fiefKnowHowBuckets[i] is not { } bucket) continue;
            writer.Write(i); writer.Write(bucket.Length);
            foreach (Fix64 value in bucket) writer.Write(value.Raw);
        }
        writer.Write(_fiefKnowHowReserve?.Length ?? 0);
        if (_fiefKnowHowReserve is not null)
            foreach (Fix64[] reserve in _fiefKnowHowReserve)
            {
                writer.Write(reserve.Length);
                foreach (Fix64 value in reserve) writer.Write(value.Raw);
            }
    }

    private void ReadFiefKnowHow(BinaryReader reader)
    {
        if (!FiefChain) return;
        _fiefKnowHowBuckets.Clear();
        for (int i = 0; i < _settlements.Count; i++) _fiefKnowHowBuckets.Add(null);
        if (reader.ReadUInt32() != KnowHowRetentionMagic) throw new InvalidDataException("Fief Know-how snapshot section is missing.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _settlements.Count) throw new InvalidDataException("Invalid Fief Know-how bucket count.");
        int previous = -1, lineageCount = 0;
        for (int n = 0; n < count; n++)
        {
            int id = reader.ReadInt32(), length = reader.ReadInt32();
            if (id <= previous || id >= _settlements.Count || length <= 0 || (lineageCount != 0 && length != lineageCount))
                throw new InvalidDataException("Invalid Fief Know-how bucket.");
            previous = id; lineageCount = length;
            var bucket = new Fix64[length];
            for (int i = 0; i < length; i++)
            {
                bucket[i] = Fix64.FromRaw(reader.ReadInt64());
                if (bucket[i] < Fix64.Zero) throw new InvalidDataException("Negative Fief Know-how balance.");
            }
            _fiefKnowHowBuckets[id] = bucket;
        }
        int civs = reader.ReadInt32();
        if (civs is not 0 && civs != CivilizationCount) throw new InvalidDataException("Invalid Fief Know-how reserve count.");
        if (civs == 0) { _fiefKnowHowReserve = null; return; }
        _fiefKnowHowReserve = new Fix64[civs][];
        for (int c = 0; c < civs; c++)
        {
            int length = reader.ReadInt32();
            if (length <= 0 || (lineageCount != 0 && length != lineageCount)) throw new InvalidDataException("Invalid Fief Know-how reserve.");
            lineageCount = length; _fiefKnowHowReserve[c] = new Fix64[length];
            for (int i = 0; i < length; i++)
            {
                _fiefKnowHowReserve[c][i] = Fix64.FromRaw(reader.ReadInt64());
                if (_fiefKnowHowReserve[c][i] < Fix64.Zero) throw new InvalidDataException("Negative Fief Know-how reserve.");
            }
        }
        _fiefKnowHowNeedsMigration = false;
    }
}
