using System;
using System.Collections.Generic;
using CivSim.Content;
using CivSim.Sim.Content;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public class BronzeFingerprintCompatibilityTests
{
    [Fact]
    public void E4_effects_and_E4_only_registry_rows_do_not_change_Bronze_checksum()
    {
        ContentPack baseline = BuildPack(ContentFixture.Files());
        var changedFiles = ContentFixture.Files();
        Replace(changedFiles, "catalog/agr-agriculture.yaml",
            "rung: 4, name: iron_scythe, mult: 2.0, tool: t11, stage: harvest",
            "rung: 4, name: iron_scythe, mult: 2.1, tool: t11, stage: harvest");
        Replace(changedFiles, "registries/goods.yaml",
            "id: G-GRAPE, name: Grapes, category: food, first_from: [FOR-08], spoilage: 0.2",
            "id: G-GRAPE, name: Grapes, category: food, first_from: [FOR-08], spoilage: 0.3");
        Replace(changedFiles, "registries/actions.yaml",
            "id: till, name: Till, class: family, introduced_era: 4",
            "id: till, name: Till, class: family, base_rate: 1, unit: L/ws, introduced_era: 4");
        ContentPack changed = BuildPack(changedFiles);

        Assert.NotEqual(baseline.HashHex, changed.HashHex); // the full content pack still sees all E4 edits
        Assert.Equal(BronzeChecksum(baseline), BronzeChecksum(changed));
        Assert.True(TestPack.Catalog.Get("AGR-09").Numeric); // promoted with the rest of E4; a Bronze match still never reaches it (ChapterRules)
    }

    [Fact]
    public void E0_to_E3_effect_content_remains_in_the_Bronze_checksum()
    {
        ContentPack baseline = BuildPack(ContentFixture.Files());
        var changedFiles = ContentFixture.Files();
        Replace(changedFiles, "catalog/stn-stoneworking.yaml",
            "rung: 1, name: Pebble chopper, mult: 1.5",
            "rung: 1, name: Pebble chopper, mult: 1.6");
        ContentPack changed = BuildPack(changedFiles);

        Assert.NotEqual(BronzeChecksum(baseline), BronzeChecksum(changed));
    }

    private static ulong BronzeChecksum(ContentPack pack)
        => MatchModel.Create(ViewKit.Seed, pack, ViewKit.World.Value, ViewKit.Balanced).ComputeChecksum();

    private static ContentPack BuildPack(List<(string Path, string Text)> files)
    {
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Validator.Run(set, report);
        Assert.Equal(0, report.ErrorCount);
        ContentPack? pack = PackBuilder.Build(set, report);
        Assert.NotNull(pack);
        return pack!;
    }

    private static void Replace(List<(string Path, string Text)> files, string path, string oldText, string newText)
    {
        int index = files.FindIndex(f => f.Path == path);
        Assert.True(index >= 0, $"missing fixture {path}");
        string text = files[index].Text;
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        files[index] = (path, text.Replace(oldText, newText, StringComparison.Ordinal));
    }
}
