using System;
using System.Globalization;
using System.Linq;
using System.Text;
using CivSim.Sim.Economy;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Opt-in read-only classification of the F-T6 institution slice.</summary>
internal static class FiefInstitutionTrace
{
    public static void Append(StringBuilder output, LivingEconomyModel economy, ResearchSystem research, int season)
    {
        var powerBySettlement = economy.CaptureMillPowerSnapshots(season).ToDictionary(row => row.SettlementId);
        int[] working = new int[economy.CivilizationCount], dormant = new int[economy.CivilizationCount], unresolved = new int[economy.CivilizationCount];
        int nrg05 = research.Catalog.IndexOf("NRG-05");
        string[] ids = ["monastery", "university", "hospital", "gothic_cathedral", "clock_tower", "watermill", "windmill"];

        int Kind(string id)
        {
            for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
                if (LivingEconomyModel.IronWorkId(k) == id) return k;
            return -1;
        }

        for (int settlement = 0; settlement < economy.SettlementCount; settlement++)
        {
            (int id, int civ, _, _, _, _) = economy.SettlementLite(settlement);
            if ((uint)civ >= (uint)economy.CivilizationCount) continue;
            BuildOutlook outlook = economy.BuildOutlookFor(settlement);
            int universityKind = Kind("university");
            int universityCount = universityKind >= 0 ? economy.IronWorkAt(settlement, universityKind) : 0;
            int scholars = economy.ScholarsAt(settlement);
            int competingScholarInstitutions = economy.IronWorkAt(settlement, FindKind("academy"))
                + economy.IronWorkAt(settlement, FindKind("cathedral_school"));
            if (research.IsDone(civ, research.Catalog.IndexOf("LRN-07")))
                competingScholarInstitutions += economy.IronWorkAt(settlement, FindKind("monastery"));
            int universityScholars = AllocateUniversityScholars(scholars, universityCount, competingScholarInstitutions);
            foreach (string structure in ids)
            {
                int kind = Kind(structure);
                int built = kind >= 0 ? economy.IronWorkAt(settlement, kind) : 0;
                BuildItem? item = Array.Find(outlook.Items, candidate => candidate.Track == BuildTrack.Iron && candidate.Id == structure);
                if (built > 0)
                {
                    (string State, string Reason, bool Staffed) status = structure == "university"
                        ? ClassifyUniversity(universityScholars, universityCount, scholars, competingScholarInstitutions)
                        : ClassifyBuilt(economy, research, powerBySettlement, settlement, id, civ, structure, nrg05);
                    int staffedCount = structure == "university" ? universityScholars : status.Staffed && status.State == "working" ? built : 0;
                    int dormantCount = structure == "university" ? built - universityScholars : status.Staffed && status.State == "dormant" ? built : 0;
                    output.AppendLine($"fief-institution-trace season={season} civ={civ} settlement={id} institution={structure} state={status.State} builtCount={built} staffedCount={staffedCount} dormantCount={dormantCount} reason={status.Reason}");
                    if (status.Staffed)
                    {
                        working[civ] += staffedCount;
                        dormant[civ] += dormantCount;
                        if (status.State == "unresolved") unresolved[civ] += built;
                    }
                }
                if (built == 0 && item is not null && IsQueued(item.Status))
                {
                    string project = string.IsNullOrEmpty(item.Gate.Project) ? "none" : item.Gate.Project;
                    string shortages = string.Join(',', item.Needs.Select((need, index) => (need, index))
                        .Where(pair => pair.need.Short)
                        .Select(pair => $"{LivingEconomyModel.StoreSlotId(pair.need.Slot)}:{pair.need.Have.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture)}/{pair.need.Need.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture)}:{item.Supplies[pair.index].Kind}"));
                    output.AppendLine($"fief-institution-trace season={season} civ={civ} settlement={id} institution={structure} state=queued builtCount=0 reason=buildStatus_{item.Status};gate={item.Gate.Cause}/{item.Gate.Why};project={project};shortages={(shortages.Length == 0 ? "none" : shortages)}");
                }
            }
        }

        for (int civ = 0; civ < economy.CivilizationCount; civ++)
        {
            int denominator = working[civ] + dormant[civ];
            string rate = denominator == 0 ? "na" : (100.0 * dormant[civ] / denominator).ToString("F2", CultureInfo.InvariantCulture);
            output.AppendLine($"fief-institution-summary season={season} civ={civ} workingStaffed={working[civ]} dormantStaffed={dormant[civ]} unresolvedStaffing={unresolved[civ]} dormantRatePercent={rate} denominator={denominator}");
        }
    }

    private static (string State, string Reason, bool Staffed) ClassifyBuilt(LivingEconomyModel economy, ResearchSystem research,
        System.Collections.Generic.Dictionary<int, MillPowerLedgerSnapshot> powerBySettlement,
        int settlement, int settlementId, int civ, string structure, int nrg05)
    {
        switch (structure)
        {
            case "monastery":
                int monks = economy.MonksAt(settlement);
                return ClassifyMonastery(monks);
            case "university":
            {
                int scholars = economy.ScholarsAt(settlement);
                int academy = economy.IronWorkAt(settlement, FindKind("academy"));
                int school = economy.IronWorkAt(settlement, FindKind("cathedral_school"));
                int monastery = research.IsDone(civ, research.Catalog.IndexOf("LRN-07"))
                    ? economy.IronWorkAt(settlement, FindKind("monastery")) : 0;
                int universities = economy.IronWorkAt(settlement, FindKind("university"));
                int competing = academy + school + monastery;
                int assigned = AllocateUniversityScholars(scholars, universities, competing);
                return ClassifyUniversity(assigned, universities, scholars, competing);
            }
            case "hospital":
            {
                var care = economy.FiefCareAndScholarCounts(settlement);
                return ClassifyHospital(care.HospitalStaffed, care.Physicians, care.Attendants);
            }
            case "gothic_cathedral":
                return ("working", "built_passive_wonder_no_postbuild_staff_role", false);
            case "clock_tower":
                return ("working", "built_tower_includes_its_own_settlement_in_reach", false);
            case "watermill":
            case "windmill":
            {
                powerBySettlement.TryGetValue(settlementId, out MillPowerLedgerSnapshot power);
                bool sourceActive = structure == "watermill" ? power.WaterMillSources > 0 : power.WindmillSources > 0;
                return ClassifyMill(sourceActive, power.AvailablePowerHours > CivSim.Sim.Numerics.Fix64.Zero,
                    structure == "windmill", research.IsDone(civ, nrg05), power.AvailablePowerHours);
            }
            default:
                return ("unresolved", "unsupported_institution_type", false);
        }
    }

    private static int FindKind(string id)
    {
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
            if (LivingEconomyModel.IronWorkId(k) == id) return k;
        return -1;
    }

    internal static bool IsQueued(BuildStatus status) => status is BuildStatus.Current or BuildStatus.Short or BuildStatus.Waiting;

    internal static (string State, string Reason, bool Staffed) ClassifyMonastery(int residentMonks)
        => residentMonks > 0 ? ("working", $"monks_present={residentMonks}", true) : ("dormant", "no_resident_monks", true);

    /// <summary>Deterministically reserve one scholar per competing institution before assigning remaining scholars to universities.</summary>
    internal static int AllocateUniversityScholars(int residentScholars, int universityCount, int otherScholarInstitutions)
    {
        int available = Math.Max(0, residentScholars - Math.Max(0, otherScholarInstitutions));
        return Math.Min(Math.Max(0, universityCount), available);
    }

    internal static (string State, string Reason, bool Staffed) ClassifyUniversity(int assignedScholars, int universityCount,
        int residentScholars, int otherScholarInstitutions)
        => assignedScholars > 0
            ? ("working", $"scholars_assigned={assignedScholars}/{universityCount};resident_scholars={residentScholars};priority_reserved_for_other_institutions={otherScholarInstitutions}", true)
            : ("dormant", residentScholars == 0 ? "no_resident_scholars" : $"scholars_reserved_for_other_institutions={otherScholarInstitutions};resident_scholars={residentScholars}", true);

    internal static (string State, string Reason, bool Staffed) ClassifyHospital(bool staffed, int physicians, int attendants)
        => staffed ? ("working", $"physicians={physicians};attendants={attendants}", true) :
            ("dormant", $"requires_1_physician_2_attendants;physicians={physicians};attendants={attendants}", true);

    internal static (string State, string Reason, bool Staffed) ClassifyMill(bool sourceActive, bool hasPower,
        bool windmill, bool hasWindResearch, CivSim.Sim.Numerics.Fix64 availablePower)
    {
        if (sourceActive && hasPower)
            return ("working", $"available_power_hours={availablePower.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture)}", false);
        string reason = windmill && !hasWindResearch ? "requires_NRG-05_for_wind_power" : "no_active_power_source";
        return ("dormant", reason, false);
    }
}
