using System;
using System.Collections.Generic;
using CivSim.Sim.View;

namespace CivSim.Game;

/// <summary>
/// Development-only (debug builds) staging for the settlement-core prototype (docs/MVP/31 WP1, WP2): <c>--dev-core-gallery</c> dresses six live
/// settlements as one of each stage (Band, Village, Town, City, a walled Iron city with castle and Wonder, a coastal town) so the scenario
/// <c>core-gallery</c> can photograph them with and without <c>--core-layout</c>. It edits only the renderer's copy of the frame (the payload
/// dictionaries), never the simulation, like the other <c>--dev-*</c> flags in <see cref="BuildFramePayload"/>.
/// </summary>
public partial class Game
{
#if DEBUG
    // Structure kinds by index (M7WorldData.Kind in world_data.gd, Structure in LivingEconomyModel.cs).
    private const int SHearth = 0, SLeanTo = 1, SHideTent = 2, SPitHouse = 3, SWattle = 4, SMudbrick = 5, SLonghouse = 6, SStoragePit = 7,
        SDryingRack = 8, SGranary = 9, SKiln = 10, SQuern = 11, SThorn = 12, SLookout = 13, SWeir = 14, SPasture = 16, SStoneWalls = 19,
        SPalisade = 20, STotem = 21, SMarker = 22, SShrine = 23, SSignalPost = 24, SPottersWorkshop = 25;

    // Decor kinds (M7MeshKit.DECOR_NAMES order): totem, cairn, hideframe, pots, loom, shrine, signal, charcoal, furnace, sledge, palisade,
    // spearrack, grave, council, quarry, skypole, bricks, baskets, woodwork.
    private static readonly int[] GalleryDecorBand = { 2, 3, 12 };
    private static readonly int[] GalleryDecorVillage = { 0, 1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 15, 17 };
    private static readonly int[] GalleryDecorAll = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 };

    private sealed record GalleryStage(string Name, string Stage, int Population, (int Kind, int Count)[] Structures, int Halls, int Temples,
        int Ziggurats, string[] Works, int Soldiers, int Forge, int[] Decor);

    private static readonly GalleryStage[] GalleryStages =
    {
        new("iron", "city", 140, new[] { (SMudbrick, 12), (SLonghouse, 3), (SGranary, 3), (SStoragePit, 2), (SDryingRack, 4), (SQuern, 2), (SKiln, 1),
                (SPottersWorkshop, 1), (STotem, 1), (SShrine, 1), (SLookout, 1), (SSignalPost, 1), (SThorn, 1), (SPalisade, 1), (SStoneWalls, 3),
                (SPasture, 1), (SMarker, 1) },
            2, 1, 1, new[] { "gatehouse", "castle", "great_dome", "academy", "theatre_arena", "bathhouse", "aqueduct", "press", "crane", "windlass_well" },
            6, 2, GalleryDecorAll),
        new("city", "city", 110, new[] { (SMudbrick, 10), (SLonghouse, 2), (SGranary, 3), (SDryingRack, 3), (SQuern, 2), (SKiln, 1), (SPottersWorkshop, 1),
                (STotem, 1), (SShrine, 1), (SLookout, 1), (SSignalPost, 1), (SPalisade, 1), (SStoneWalls, 2), (SPasture, 1) },
            1, 1, 0, new[] { "academy", "windlass_well" }, 3, 1, GalleryDecorAll),
        new("town", "town", 75, new[] { (SMudbrick, 6), (SWattle, 2), (SGranary, 2), (SStoragePit, 2), (SDryingRack, 4), (SQuern, 2), (SKiln, 1),
                (SPottersWorkshop, 1), (STotem, 1), (SShrine, 1), (SLookout, 1), (SSignalPost, 1), (SThorn, 1), (SPalisade, 1), (SStoneWalls, 1),
                (SPasture, 1), (SMarker, 1) },
            0, 0, 0, Array.Empty<string>(), 0, 0, GalleryDecorAll),
        new("village", "village", 45, new[] { (SWattle, 4), (SStoragePit, 2), (SDryingRack, 2), (SQuern, 1), (SKiln, 1), (STotem, 1), (SLookout, 1),
                (SThorn, 1), (SPalisade, 1), (SPasture, 1) },
            0, 0, 0, Array.Empty<string>(), 0, 0, GalleryDecorVillage),
        new("band", "band", 14, new[] { (SLeanTo, 2), (SHideTent, 2), (SStoragePit, 1), (SDryingRack, 1), (SThorn, 1) },
            0, 0, 0, Array.Empty<string>(), 0, 0, GalleryDecorBand),
    };

    private static readonly GalleryStage GalleryCoastal = new("coastal", "town", 70, new[] { (SWattle, 5), (SGranary, 1), (SStoragePit, 1),
            (SDryingRack, 2), (SQuern, 1), (SKiln, 1), (SWeir, 1), (SLookout, 1), (SPalisade, 1), (SShrine, 1), (STotem, 1) },
        0, 0, 0, new[] { "harbor", "galley" }, 0, 0, GalleryDecorVillage);

    /// <summary>Picks the coastal town (the biggest live settlement beside the sea) and five more (biggest first: Iron city, city, town, village, band)
    /// and overwrites their renderer dictionaries with the gallery's contents.</summary>
    private static void ApplyCoreGallery(MatchFrame frame, WorldView world, Godot.Collections.Array settlements)
    {
        var order = new List<int>();
        for (int i = 0; i < frame.SettlementCount && i < settlements.Count; i++)
            if ((frame.SettlementFlags[i] & (ushort)(SettlementViewFlags.Remembered | SettlementViewFlags.Empty)) == 0 && frame.SettlementPopulation[i] > 0)
                order.Add(i);
        order.Sort((a, b) => frame.SettlementPopulation[b] != frame.SettlementPopulation[a]
            ? frame.SettlementPopulation[b].CompareTo(frame.SettlementPopulation[a]) : a.CompareTo(b));
        Span<int> neighbours = stackalloc int[6];
        int coastal = -1;
        foreach (int i in order)
        {
            int count = CivSim.Sim.World.HexGrid.Neighbors(frame.SettlementTile[i], frame.Width, frame.Height, neighbours);
            for (int k = 0; k < count && coastal < 0; k++) if (world.Biome[neighbours[k]] <= 1) coastal = i;
            if (coastal >= 0) break;
        }
        int next = 0;
        foreach (GalleryStage stage in GalleryStages)
        {
            while (next < order.Count && order[next] == coastal) next++;
            if (next >= order.Count) break;
            DressGallery((Godot.Collections.Dictionary)settlements[order[next]], stage);
            next++;
        }
        if (coastal >= 0) DressGallery((Godot.Collections.Dictionary)settlements[coastal], GalleryCoastal);
    }

    private static void DressGallery(Godot.Collections.Dictionary settlement, GalleryStage stage)
    {
        var structures = new int[MatchFrame.StructureKinds];
        structures[SHearth] = 1;
        foreach (var (kind, count) in stage.Structures) if (kind < structures.Length) structures[kind] = count;
        var works = new int[IronWorkKindCount()];
        foreach (string id in stage.Works) if (IronKindIndex(id) is >= 0 and int at) works[at] = 1;
        settlement["structures"] = structures;
        settlement["population"] = stage.Population;
        settlement["halls"] = stage.Halls;
        settlement["temples"] = stage.Temples;
        settlement["ziggurats"] = stage.Ziggurats;
        settlement["zigguratPermille"] = 0;
        settlement["fireLit"] = true;
        settlement["ironWorks"] = works;
        settlement["soldiers"] = stage.Soldiers;
        settlement["forge"] = stage.Forge;
        settlement["faithOur"] = 0;
        settlement["faithForeign"] = 0;
        settlement["faithCiv"] = -1;
        settlement["provinceSeat"] = -1;
        settlement["contract"] = 0;
        settlement["facing"] = 0;
        settlement["ironBuildKind"] = -1;
        settlement["ironBuildPermille"] = 0;
        settlement["decor"] = stage.Decor;
        settlement["coreStage"] = stage.Stage;
        settlement["coreGallery"] = stage.Name;
    }
#endif
}
