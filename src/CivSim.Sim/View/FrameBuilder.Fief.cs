using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

/// <summary>Builds the Fief-only, privacy-filtered read model from existing simulation state.</summary>
public sealed partial class FrameBuilder
{
    private static int IronWorkIndex(string id)
    {
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
            if (LivingEconomyModel.IronWorkId(k) == id) return k;
        return -1;
    }

    private static readonly int MonasteryKind = IronWorkIndex("monastery");
    private static readonly int UniversityKind = IronWorkIndex("university");
    private static readonly int HospitalKind = IronWorkIndex("hospital");
    private static readonly int ConcentricCastleKind = IronWorkIndex("concentric_castle");

    private void FillFief(MatchModel match, MatchFrame target)
    {
        FiefFrame f = target.Fief;
        LivingEconomyModel eco = match.Economy;
        f.Active = eco.FiefChain;
        int n = target.SettlementCount;
        f.EnsureSettlements(n);
        System.Array.Clear(f.WaterMillSources, 0, n);
        System.Array.Clear(f.WindmillSources, 0, n);
        System.Array.Clear(f.AvailablePowerHours, 0, n);
        System.Array.Clear(f.GrainCoverage, 0, n);
        System.Array.Clear(f.Monks, 0, n);
        System.Array.Clear(f.Monasteries, 0, n);
        System.Array.Clear(f.Universities, 0, n);
        System.Array.Clear(f.Hospitals, 0, n);
        System.Array.Clear(f.ConcentricCastle, 0, n);
        if (!f.Active) return;

        for (int w = 0; w < n; w++)
        {
            int settlement = target.SettlementIndex[w];
            SettlementViewFlags flags = (SettlementViewFlags)target.SettlementFlags[w];
            bool own = (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            if (!own || (uint)settlement >= (uint)_settlementCount) continue;
            f.Monks[w] = (byte)System.Math.Min(255, eco.MonksAt(settlement));
            f.Monasteries[w] = (byte)System.Math.Min(255, eco.IronWorkAt(settlement, MonasteryKind));
            f.Universities[w] = (byte)System.Math.Min(255, eco.IronWorkAt(settlement, UniversityKind));
            f.Hospitals[w] = (byte)System.Math.Min(255, eco.IronWorkAt(settlement, HospitalKind));
            f.ConcentricCastle[w] = (byte)(eco.ConcentricCastleBuilt(settlement) ? 1 : 0);
            if (!eco.TryCaptureMillPowerSnapshot(settlement, target.Season, out MillPowerLedgerSnapshot snapshot)) continue;
            f.WaterMillSources[w] = snapshot.WaterMillSources;
            f.WindmillSources[w] = snapshot.WindmillSources;
            f.AvailablePowerHours[w] = snapshot.AvailablePowerHours.Raw;
            f.GrainCoverage[w] = snapshot.GrainCoverage.Raw;
        }
    }
}
