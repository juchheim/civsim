using CivSim.Harness;
using System.Linq;

namespace CivSim.Tests;

public sealed class CombatE3TableTests
{
    [Fact]
    public void LiveE3ReportCoversDocumentedRaidAndFieldRows()
    {
        Assert.Equal(11, CombatE3Tables.Raids.Length);
        Assert.Equal(6, CombatE3Tables.Fields.Length);
        Assert.Contains(CombatE3Tables.Raids, r => r.Name.StartsWith("12 stone spearmen"));
        Assert.Contains(CombatE3Tables.Fields, r => r.WinTarget is null);
        Assert.Equal(4, CombatE3Tables.Fields.Count(r => r.WinTarget is not null));
    }

    [Fact]
    public void FixedSeedE3RowsStayWithinDocumentedOracleBands()
    {
        foreach (E3RaidRow row in CombatE3Tables.Raids)
            Assert.True(CombatE3Tables.Pass(CombatE3Tables.Run(row, CombatE3Tables.DefaultTrials), row), row.Name);
        foreach (E3FieldRow row in CombatE3Tables.Fields)
            Assert.True(CombatE3Tables.Pass(CombatE3Tables.Run(row, CombatE3Tables.DefaultTrials)), row.Name);
    }
}
