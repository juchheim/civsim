using CivSim.Harness;

namespace CivSim.Tests;

public sealed class ResearchE3AccountingTests
{
    [Theory]
    [InlineData(0, -1)] // a newborn was not exposed to the season just measured
    [InlineData(1, 0)]
    [InlineData(14, 13)] // the child-to-adult boundary belongs to the pre-aging child cohort
    [InlineData(15, 14)]
    [InlineData(45, 44)] // the adult-to-elder boundary belongs to the pre-aging adult cohort
    [InlineData(46, 45)]
    [InlineData(99, 98)]
    public void Survivor_age_is_rewound_to_the_pre_mortality_cohort(int postSeasonAge, int expectedPreMortalityAge)
    {
        Assert.Equal(expectedPreMortalityAge, ResearchE3.PreMortalityAgeOfSurvivor(postSeasonAge));
    }
}
