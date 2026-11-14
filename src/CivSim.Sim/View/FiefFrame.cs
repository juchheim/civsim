using System;

namespace CivSim.Sim.View;

/// <summary>Read-only Faith &amp; Fief presentation data. Per-settlement values are aligned with the MatchFrame settlement block and private to the viewer's own towns.</summary>
public sealed class FiefFrame
{
    public bool Active;
    /// <summary>Own towns' mill-source counts plus available power and grain coverage in raw Q32.32; zero for other towns.</summary>
    public int[] WaterMillSources = Array.Empty<int>(), WindmillSources = Array.Empty<int>();
    public long[] AvailablePowerHours = Array.Empty<long>(), GrainCoverage = Array.Empty<long>();
    /// <summary>Own Fief towns' monk count and counts of monastery, university and hospital buildings, plus concentric-castle presence.</summary>
    public byte[] Monks = Array.Empty<byte>(), Monasteries = Array.Empty<byte>(), Universities = Array.Empty<byte>(), Hospitals = Array.Empty<byte>(), ConcentricCastle = Array.Empty<byte>();

    internal void EnsureSettlements(int n)
    {
        if (WaterMillSources.Length >= n) return;
        int size = Math.Max(n, Math.Max(4, WaterMillSources.Length * 2));
        WaterMillSources = new int[size]; WindmillSources = new int[size];
        AvailablePowerHours = new long[size]; GrainCoverage = new long[size];
        Monks = new byte[size]; Monasteries = new byte[size]; Universities = new byte[size]; Hospitals = new byte[size]; ConcentricCastle = new byte[size];
    }
}
