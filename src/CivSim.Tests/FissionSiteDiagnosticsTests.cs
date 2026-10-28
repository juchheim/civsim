using System.Linq;
using CivSim.Sim.Economy;
using Xunit;

namespace CivSim.Tests;

public sealed class FissionSiteDiagnosticsTests
{
    [Fact]
    public void Fission_site_attempt_accounts_ordered_rejections_and_later_recovery()
    {
        var diagnostics = new FissionSiteDiagnostics();
        int[] rejected = { 1, 2, 3, 4, 5, 6 };
        diagnostics.RecordAttempt(0, 7, 10, scannedTiles: 23, legalSites: 2, selectedTile: -1, rejections: rejected);
        diagnostics.RecordAttempt(0, 7, 11, scannedTiles: 23, legalSites: 1, selectedTile: 44, rejections: new int[6]);

        FissionSiteSelectionAttempt blocked = diagnostics.Attempts[0];
        FissionSiteSelectionAttempt recovered = diagnostics.Attempts[1];
        Assert.Equal(blocked.ScannedTiles, blocked.LegalSites + blocked.Rejections.Sum());
        Assert.Equal(23, blocked.ScannedTiles);
        Assert.Equal(-1, blocked.SelectedTile);
        Assert.True(recovered.RecoveredAfterBlocked);
        Assert.Contains("parentDistanceBelowMin=2", FissionSiteDiagnostics.Format(blocked, "warlord"));
        Assert.Contains("disallowedByAdvisor=6", FissionSiteDiagnostics.Format(blocked, "warlord"));
        Assert.Contains("noSite=true", FissionSiteDiagnostics.Format(blocked, "warlord"));
        Assert.Contains("recoveredAfterBlocked=true", FissionSiteDiagnostics.Format(recovered, "warlord"));
    }
}
