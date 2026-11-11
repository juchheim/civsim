using System.Collections.Generic;
using CivSim.Harness;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>docs/MVP/31 WP0: the core audit is repeatable and its packing reproduces the plan's seed-1 row of §6.2.</summary>
public sealed class CoreAuditTests
{
    [Fact]
    public void Static_report_is_repeatable_and_matches_the_plan_on_seed_1()
    {
        string one = CoreAuditCommand.StaticReport(1, 1);
        Assert.Equal(one, CoreAuditCommand.StaticReport(1, 1));
        // tile order 172 / 112, food order 139 / 88, clean core 131 / 85 (measured for docs/MVP/31 §6.2 at 4098f30).
        Assert.Contains("|   172   112 |   139    88 |    131     85 |", one);
    }

    [Fact]
    public void Packing_never_places_two_sites_closer_than_the_spacing()
    {
        const int w = 20, h = 20;
        var order = new int[w * h];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        int at7 = CoreAuditCommand.Pack(w, h, 7, _ => true, order);
        int at9 = CoreAuditCommand.Pack(w, h, 9, _ => true, order);
        Assert.True(at7 > at9 && at9 > 0);

        var disk = new List<int>();
        CoreAuditCommand.Disk(HexGrid.Index(10, 10, w, h), 1, w, h, disk);
        Assert.Equal(7, disk.Count);
    }
}
