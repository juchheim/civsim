using System;

namespace CivSim.Sim.Economy;

/// <summary>Read-only Fief staffing/treatment facts for the presentation frame.</summary>
public sealed partial class LivingEconomyModel
{
    internal (int Physicians, int Attendants, int Ill, int Wounded, int Treated, int Scholars, bool HospitalStaffed) FiefCareAndScholarCounts(int settlementIndex)
    {
        if (!FiefChain || (uint)settlementIndex >= (uint)_settlements.Count) return default;
        int physicians = 0, attendants = 0, ill = 0, wounded = 0, treated = 0, scholars = 0;
        foreach (FigureState f in Residents(settlementIndex))
        {
            if (f.Role == Role.Physician) physicians++;
            if (f.Role == Role.Attendant) attendants++;
            if (f.Role == Role.Scholar) scholars++;
            bool treatedThisSeason = false;
            if (f.IllnessSeasons > 0) { ill++; treatedThisSeason |= f.DiseaseTreated; }
            if (f.WoundSeasons > 0) { wounded++; treatedThisSeason |= f.WoundTreated; }
            if (treatedThisSeason) treated++;
        }
        return (physicians, attendants, ill, wounded, treated, scholars, HospitalStaffed(_settlements[settlementIndex]));
    }
}
