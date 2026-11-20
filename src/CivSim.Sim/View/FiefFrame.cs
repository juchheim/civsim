using System;

namespace CivSim.Sim.View;

/// <summary>Read-only Faith &amp; Fief presentation data. Per-settlement values are aligned with the MatchFrame settlement block and private to the viewer's own towns.</summary>
public sealed class FiefFrame
{
    public bool Active;
    /// <summary>Own towns' mill-source counts plus available power and grain coverage in raw Q32.32; zero for other towns.</summary>
    public int[] WaterMillSources = Array.Empty<int>(), WindmillSources = Array.Empty<int>();
    public long[] AvailablePowerHours = Array.Empty<long>(), GrainCoverage = Array.Empty<long>();
    /// <summary>Per-season power budget by job, raw Q32.32.</summary>
    public long[] GrindingDemand = Array.Empty<long>(), GrindingUsed = Array.Empty<long>(), SawingDemand = Array.Empty<long>(), SawingUsed = Array.Empty<long>(), CraftDemand = Array.Empty<long>(), CraftUsed = Array.Empty<long>();
    /// <summary>Own town staffing, treatment, scholar contribution, and KEY-6 research progress.</summary>
    public byte[] Physicians = Array.Empty<byte>(), Attendants = Array.Empty<byte>(), IllPatients = Array.Empty<byte>(), WoundedPatients = Array.Empty<byte>(), TreatedPatients = Array.Empty<byte>(), Scholars = Array.Empty<byte>(), HospitalStaffed = Array.Empty<byte>();
    public long[] ScholarInsightHeads = Array.Empty<long>();
    public short[] Key6Permille = Array.Empty<short>();
    public string[] BuildGuidance = Array.Empty<string>();
    /// <summary>1 while the town's Arsenal holds cannons or powder (it works), else 0; the renderer draws it lit or dark (docs/MVP/32 §4.13).</summary>
    public byte[] ArsenalStocked = Array.Empty<byte>();
    /// <summary>Own Fief towns' monk count and counts of monastery, university and hospital buildings, plus concentric-castle presence.</summary>
    public byte[] Monks = Array.Empty<byte>(), Monasteries = Array.Empty<byte>(), Universities = Array.Empty<byte>(), Hospitals = Array.Empty<byte>(), ConcentricCastle = Array.Empty<byte>();

    internal void EnsureSettlements(int n)
    {
        if (WaterMillSources.Length >= n) return;
        int size = Math.Max(n, Math.Max(4, WaterMillSources.Length * 2));
        WaterMillSources = new int[size]; WindmillSources = new int[size];
        AvailablePowerHours = new long[size]; GrainCoverage = new long[size];
        GrindingDemand = new long[size]; GrindingUsed = new long[size]; SawingDemand = new long[size]; SawingUsed = new long[size]; CraftDemand = new long[size]; CraftUsed = new long[size];
        Physicians = new byte[size]; Attendants = new byte[size]; IllPatients = new byte[size]; WoundedPatients = new byte[size]; TreatedPatients = new byte[size]; Scholars = new byte[size]; HospitalStaffed = new byte[size];
        ScholarInsightHeads = new long[size]; Key6Permille = new short[size]; BuildGuidance = new string[size];
        ArsenalStocked = new byte[size];
        Monks = new byte[size]; Monasteries = new byte[size]; Universities = new byte[size]; Hospitals = new byte[size]; ConcentricCastle = new byte[size];
    }
}
