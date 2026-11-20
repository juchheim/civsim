using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
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
    private static readonly int ArsenalKind = IronWorkIndex("arsenal");

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
        System.Array.Clear(f.GrindingDemand, 0, n); System.Array.Clear(f.GrindingUsed, 0, n);
        System.Array.Clear(f.SawingDemand, 0, n); System.Array.Clear(f.SawingUsed, 0, n);
        System.Array.Clear(f.CraftDemand, 0, n); System.Array.Clear(f.CraftUsed, 0, n);
        System.Array.Clear(f.Monks, 0, n);
        System.Array.Clear(f.Monasteries, 0, n);
        System.Array.Clear(f.Universities, 0, n);
        System.Array.Clear(f.Hospitals, 0, n);
        System.Array.Clear(f.ConcentricCastle, 0, n);
        System.Array.Clear(f.Physicians, 0, n); System.Array.Clear(f.Attendants, 0, n);
        System.Array.Clear(f.IllPatients, 0, n); System.Array.Clear(f.WoundedPatients, 0, n); System.Array.Clear(f.TreatedPatients, 0, n);
        System.Array.Clear(f.Scholars, 0, n); System.Array.Clear(f.HospitalStaffed, 0, n); System.Array.Clear(f.ScholarInsightHeads, 0, n); System.Array.Clear(f.Key6Permille, 0, n);
        System.Array.Clear(f.BuildGuidance, 0, n);
        System.Array.Clear(f.ArsenalStocked, 0, n);
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
            if (ArsenalKind >= 0 && eco.IronWorkAt(settlement, ArsenalKind) > 0)
                f.ArsenalStocked[w] = (byte)(eco.FiefStock(settlement, "G-CANNON") > Fix64.Zero || eco.FiefStock(settlement, "G-POWDER") > Fix64.Zero ? 1 : 0);
            f.Hospitals[w] = (byte)System.Math.Min(255, eco.IronWorkAt(settlement, HospitalKind));
            f.ConcentricCastle[w] = (byte)(eco.ConcentricCastleBuilt(settlement) ? 1 : 0);
            var care = eco.FiefCareAndScholarCounts(settlement);
            f.Physicians[w] = (byte)System.Math.Min(255, care.Physicians);
            f.Attendants[w] = (byte)System.Math.Min(255, care.Attendants);
            f.IllPatients[w] = (byte)System.Math.Min(255, care.Ill);
            f.WoundedPatients[w] = (byte)System.Math.Min(255, care.Wounded);
            f.TreatedPatients[w] = (byte)System.Math.Min(255, care.Treated);
            f.Scholars[w] = (byte)System.Math.Min(255, care.Scholars);
            f.HospitalStaffed[w] = (byte)(care.HospitalStaffed ? 1 : 0);
            int lrn08 = match.Research.Catalog.IndexOf("LRN-08");
            bool universityBonus = f.Universities[w] > 0 && lrn08 >= 0 && match.Research.IsDone(target.SettlementCiv[w], lrn08);
            Fix64 scholarHeads = Fix64.FromInt(care.Scholars) * (universityBonus ? Fix64.FromRatio(3, 2) : Fix64.One);
            f.ScholarInsightHeads[w] = scholarHeads.Raw;
            int key6 = match.Research.Catalog.IndexOf("KEY-6");
            if (key6 >= 0 && !match.Research.IsDone(target.SettlementCiv[w], key6))
            {
                Fix64 cost = match.Research.EffectiveCost(target.SettlementCiv[w], key6);
                if (cost > Fix64.Zero) f.Key6Permille[w] = (short)System.Math.Clamp((match.Research.Progress(target.SettlementCiv[w], key6) * Fix64.FromInt(1000) / cost).FloorToInt(), 0, 1000);
            }
            BuildOutlook? outlook = target.SettlementBuild[w];
            if (outlook is not null) foreach (BuildItem item in outlook.Items)
            {
                if (item.Track != BuildTrack.Iron || item.Id is not ("hospital" or "university" or "water_mill" or "windmill" or "blast_furnace")) continue;
                if (item.Status is BuildStatus.Done or BuildStatus.NotNeeded) continue;
                string guidance = $"{item.Id}: {BuildText.Short(item, BuildNames.Ids)}";
                if (item.Levers.Length > 0) guidance += $" · {BuildText.Lever(item.Levers[0], BuildNames.Ids)}";
                f.BuildGuidance[w] = guidance;
                break;
            }
            if (!eco.TryCaptureMillPowerSnapshot(settlement, target.Season, out MillPowerLedgerSnapshot snapshot)) continue;
            f.WaterMillSources[w] = snapshot.WaterMillSources;
            f.WindmillSources[w] = snapshot.WindmillSources;
            f.AvailablePowerHours[w] = snapshot.AvailablePowerHours.Raw;
            f.GrainCoverage[w] = snapshot.GrainCoverage.Raw;
            f.GrindingDemand[w] = snapshot.GrindingDemand.Raw; f.GrindingUsed[w] = snapshot.GrindingUsed.Raw;
            f.SawingDemand[w] = snapshot.SawingDemand.Raw; f.SawingUsed[w] = snapshot.SawingUsed.Raw;
            f.CraftDemand[w] = snapshot.CraftDemand.Raw; f.CraftUsed[w] = snapshot.CraftUsed.Raw;
        }
    }
}
