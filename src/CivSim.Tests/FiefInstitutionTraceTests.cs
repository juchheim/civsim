using CivSim.Harness;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public sealed class FiefInstitutionTraceTests
{
    [Theory]
    [InlineData(BuildStatus.Current, true)]
    [InlineData(BuildStatus.Short, true)]
    [InlineData(BuildStatus.Waiting, true)]
    [InlineData(BuildStatus.Done, false)]
    [InlineData(BuildStatus.Blocked, false)]
    [InlineData(BuildStatus.NotNeeded, false)]
    public void Build_outlook_queue_statuses_are_classified_without_guessing(BuildStatus buildStatus, bool queued)
        => Assert.Equal(queued, FiefInstitutionTrace.IsQueued(buildStatus));

    [Fact]
    public void Staffing_classifiers_allocate_scholars_once_in_stable_institution_priority_order()
    {
        Assert.Equal("dormant", FiefInstitutionTrace.ClassifyMonastery(0).State);
        Assert.Equal("working", FiefInstitutionTrace.ClassifyMonastery(2).State);
        Assert.Equal("dormant", FiefInstitutionTrace.ClassifyHospital(false, 1, 1).State);
        Assert.Equal("working", FiefInstitutionTrace.ClassifyHospital(true, 1, 2).State);

        int assigned = FiefInstitutionTrace.AllocateUniversityScholars(residentScholars: 3, universityCount: 2, otherScholarInstitutions: 2);
        Assert.Equal(1, assigned);
        var status = FiefInstitutionTrace.ClassifyUniversity(assigned, universityCount: 2, residentScholars: 3, otherScholarInstitutions: 2);
        Assert.Equal("working", status.State);
        Assert.Contains("scholars_assigned=1/2", status.Reason);
        Assert.Equal(0, FiefInstitutionTrace.AllocateUniversityScholars(1, 1, 1));
        Assert.Equal("dormant", FiefInstitutionTrace.ClassifyUniversity(0, 1, 1, 1).State);
        Assert.Equal(0, FiefInstitutionTrace.AllocateUniversityScholars(0, 1, 0));
        Assert.Equal("dormant", FiefInstitutionTrace.ClassifyUniversity(0, 1, 0, 0).State);
    }

    [Fact]
    public void Mill_classifier_names_the_missing_wind_unlock_or_power_source()
    {
        Assert.Equal("requires_NRG-05_for_wind_power", FiefInstitutionTrace.ClassifyMill(
            sourceActive: false, hasPower: false, windmill: true, hasWindResearch: false, Fix64.Zero).Reason);
        Assert.Equal("no_active_power_source", FiefInstitutionTrace.ClassifyMill(
            sourceActive: false, hasPower: false, windmill: false, hasWindResearch: true, Fix64.Zero).Reason);
        Assert.Equal("working", FiefInstitutionTrace.ClassifyMill(
            sourceActive: true, hasPower: true, windmill: false, hasWindResearch: false, Fix64.FromInt(5)).State);
    }
}
