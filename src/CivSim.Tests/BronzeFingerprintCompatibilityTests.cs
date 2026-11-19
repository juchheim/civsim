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

    [Fact]
    public void Later_chapters_fingerprint_the_content_their_mode_can_use_without_changing_legacy_streams()
    {
        ContentPack baseline = BuildPack(ContentFixture.Files());

        var changedE5Files = ContentFixture.Files();
        Replace(changedE5Files, "catalog/agr-agriculture.yaml",
            "value: 1.5, note: \"rotated fields: cropped share 1/2 to 2/3 (x1.333) with a legume yield x1.125 (P, e5_economy_oracle)\"",
            "value: 1.6, note: \"rotated fields: cropped share 1/2 to 2/3 (x1.333) with a legume yield x1.125 (P, e5_economy_oracle)\"");
        ContentPack changedE5 = BuildPack(changedE5Files);

        Assert.Equal(ModeChecksum(baseline, ChapterRules.Bronze), ModeChecksum(changedE5, ChapterRules.Bronze));
        Assert.Equal(ModeChecksum(baseline, ChapterRules.Iron), ModeChecksum(changedE5, ChapterRules.Iron));
        Assert.NotEqual(ModeChecksum(baseline, ChapterRules.Fief), ModeChecksum(changedE5, ChapterRules.Fief));

        var changedE4Files = ContentFixture.Files();
        Replace(changedE4Files, "catalog/bel-belief-ritual.yaml",
            "stat: scholar_insight, op: pct, value: 0.10",
            "stat: scholar_insight, op: pct, value: 0.11");
        ContentPack changedE4 = BuildPack(changedE4Files);

        Assert.Equal(ModeChecksum(baseline, ChapterRules.Iron), ModeChecksum(changedE4, ChapterRules.Iron));
        Assert.NotEqual(ModeChecksum(baseline, ChapterRules.Fief), ModeChecksum(changedE4, ChapterRules.Fief));
    }

    private static ulong BronzeChecksum(ContentPack pack)
        => MatchModel.Create(ViewKit.Seed, pack, ViewKit.World.Value, ViewKit.Balanced).ComputeChecksum();

    private static ulong ModeChecksum(ContentPack pack, ChapterRules rules)
        => MatchModel.Create(ViewKit.Seed, pack, ViewKit.World.Value, ViewKit.Balanced, default, rules: rules).ComputeChecksum();

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
