using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;

namespace CivSim.Tests;

/// <summary>The content hash must depend on content only: not file order, field order or comments.</summary>
public class ContentPackTests
{
    private static string HashOf(System.Collections.Generic.List<(string Path, string Text)> files)
    {
        (ContentSet set, Report report) = ContentFixture.Load(files);
        ContentPack? pack = PackBuilder.Build(set, report);
        Assert.True(pack is not null, ContentFixture.Describe(report));
        return pack!.HashHex;
    }

    [Fact]
    public void File_order_does_not_change_the_hash()
    {
        var files = ContentFixture.Files();
        string forward = HashOf(files);
        files.Reverse();
        Assert.Equal(forward, HashOf(files));
    }

    [Fact]
    public void Comments_and_blank_lines_do_not_change_the_hash()
    {
        var files = ContentFixture.Files();
        string baseline = HashOf(files);
        int i = files.FindIndex(f => f.Path == "catalog/stn-stoneworking.yaml");
        files[i] = (files[i].Path, "# a new comment\n\n" + files[i].Text.Replace("projects:\n", "projects:\n\n# another\n"));
        Assert.Equal(baseline, HashOf(files));
    }

    [Fact]
    public void Field_order_inside_a_record_does_not_change_the_hash()
    {
        var files = ContentFixture.Files();
        string baseline = HashOf(files);
        int i = files.FindIndex(f => f.Path == "catalog/stn-stoneworking.yaml");
        string swapped = files[i].Text.Replace("    era: 0\n    type: M\n    status: numeric\n    requires: []\n",
                                               "    type: M\n    era: 0\n    requires: []\n    status: numeric\n");
        Assert.NotEqual(files[i].Text, swapped);
        files[i] = (files[i].Path, swapped);
        Assert.Equal(baseline, HashOf(files));
    }

    [Fact]
    public void Changing_a_value_changes_the_hash()
    {
        var files = ContentFixture.Files();
        string baseline = HashOf(files);
        int i = files.FindIndex(f => f.Path == "catalog/fir-fire-heat.yaml");
        files[i] = (files[i].Path, files[i].Text.Replace("{kind: hazard, cause: exposure, mult: 0.92}", "{kind: hazard, cause: exposure, mult: 0.91}"));
        Assert.NotEqual(baseline, HashOf(files));
    }

    [Fact]
    public void A_pack_is_not_built_from_invalid_content()
    {
        var files = ContentFixture.Files();
        int i = files.FindIndex(f => f.Path == "catalog/stn-stoneworking.yaml");
        files[i] = (files[i].Path, files[i].Text.Replace("requires: [STN-01]", "requires: [STN-99]"));
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Assert.Null(PackBuilder.Build(set, report));
        Assert.True(report.HasErrors);
    }

    [Fact]
    public void Compiled_projects_carry_dense_sorted_indices()
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        ContentPack pack = PackBuilder.Build(set, report)!;
        var ids = pack.List("projects").Select(p => p.GetString("id")!).ToList();
        Assert.Equal(ids.OrderBy(x => x, System.StringComparer.Ordinal).ToList(), ids);
        Assert.Equal(Enumerable.Range(0, ids.Count).Select(n => (long)n), pack.List("projects").Select(p => p.GetInt("idx")!.Value));
        Assert.Contains(pack.Root.Get("ladders")!.Entries, e => e.Key == "chop");
        Assert.Equal(10, pack.Root.Get("registries")!.Get("eras")!.Items.Count);
    }

    [Fact]
    public void A_pack_with_the_wrong_format_is_rejected()
    {
        byte[] wrong = System.Text.Encoding.UTF8.GetBytes("{\"format\":\"nope\",\"version\":1}");
        Assert.Throws<System.IO.InvalidDataException>(() => ContentPack.Load(wrong));
        byte[] oldVersion = System.Text.Encoding.UTF8.GetBytes("{\"format\":\"civsim-content-pack\",\"version\":99}");
        Assert.Throws<System.IO.InvalidDataException>(() => ContentPack.Load(oldVersion));
    }
}
