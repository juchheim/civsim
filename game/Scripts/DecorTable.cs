using System;
using System.Collections.Generic;
using CivSim.Sim.Research;
using CivSim.Sim.View;

namespace CivSim.Game;

/// <summary>
/// Cosmetic props a settlement grows when its people learn a project: a totem post, a cairn, a loom, a stone quarry. Boats are separate (a moored fleet by tier, from the frame's sea tier).
/// Purely a view of what the focus civilization knows (the same completed set the research web shows); nothing here feeds
/// back into the simulation. The numbers are the renderer's decor kinds (<c>M7MeshKit.DECOR_NAMES</c> in mesh_kit.gd). The last four
/// kinds there (temple, chief's hall, fleece bales, chariot) are not in this table: they follow any civilization's <c>CivLooks</c>.
/// </summary>
public static class DecorTable
{
    public const int Totem = 0, Cairn = 1, HideFrame = 2, Pots = 3, Loom = 4, Shrine = 5, Signal = 6, Charcoal = 7,
        Furnace = 8, Sledge = 9, Palisade = 10, SpearRack = 11, Grave = 12, Council = 13, Quarry = 14,
        SkyPole = 15, Bricks = 16, Baskets = 17, Woodwork = 18;

    /// <summary>Project id to the prop it leaves in the village. Several projects may share a prop.</summary>
    private static readonly (string Project, int Decor)[] Rows =
    {
        ("BEL-02", Totem), ("SOC-04", Totem),
        ("TER-03", Cairn), ("WAY-01", Cairn),
        ("FIB-02", HideFrame), ("FIB-03", HideFrame),
        ("CER-02", Pots), ("CER-03", Pots), ("FOD-05", Pots),
        ("FIB-06", Loom), ("FIB-07", Loom),
        ("BEL-05", Shrine), ("BEL-04", Shrine),
        ("COM-04", Signal),
        ("FIR-08", Charcoal),
        ("MET-01", Furnace), ("MET-02", Furnace),
        ("MEC-02", Sledge), ("TRN-02", Sledge), ("NRG-01", Sledge),
        ("DEF-03", Palisade),
        ("HNT-03", SpearRack), ("HNT-04", SpearRack), ("HNT-05", SpearRack), ("HNT-06", SpearRack),
        ("WAR-01", SpearRack), ("WAR-02", SpearRack), ("WAR-03", SpearRack), ("WAR-04", SpearRack),
        ("BEL-01", Grave),
        ("GOV-01", Council), ("GOV-02", Council), ("SOC-05", Council),
        ("MIN-02", Quarry), ("MIN-03", Quarry),
        ("INQ-02", SkyPole), ("NUM-02", SkyPole), ("WAY-03", SkyPole),
        ("BLD-04", Bricks), ("BLD-05", Bricks),
        ("FIB-05", Baskets), ("FOR-03", Baskets),
        ("WOD-02", Woodwork), ("WOD-03", Woodwork), ("WOD-04", Woodwork),
    };

    /// <summary>The decor kinds the focus civilization has earned, ascending and without repeats.</summary>
    public static int[] Known(MatchFrame frame, ResearchCatalog? catalog)
    {
        if (catalog is null || frame.FocusProjectCount == 0) return Array.Empty<int>();
        var seen = new bool[32];
        var kinds = new List<int>();
        foreach ((string id, int decor) in Rows)
        {
            if (seen[decor]) continue;
            int index = catalog.IndexOf(id);
            if ((uint)index >= (uint)frame.FocusProjectCount) continue;
            if ((ProjectState)frame.FocusProjectState[index] is not (ProjectState.Completed or ProjectState.Dormant)) continue;
            seen[decor] = true;
            kinds.Add(decor);
        }
        kinds.Sort();
        return kinds.ToArray();
    }
}
