using System;
using System.Linq;
using System.Text.RegularExpressions;
using CivSim.Content;
using CivSim.Sim.Content;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public class E4DraftContentTests
{
    [Fact]
    public void E4_drafts_are_typed_complete_and_remain_inert()
    {
        (ContentSet set, Report report) = Validate();
        Project[] e4 = set.Projects.Where(p => p.Era == 4).ToArray();

        Assert.NotEmpty(e4);
        Assert.All(e4, p =>
        {
            Assert.Equal("draft", p.Status);
            Assert.NotNull(set.CostOf(p));
            Assert.NotEmpty(p.Practice);
            Assert.All(p.Practice, share => Assert.True(share.Share > 0, $"{p.Id} has a non-positive practice share"));
            Assert.Equal(100, p.Practice.Sum(share => share.Share));
        });
        Assert.DoesNotContain(report.Issues, i => i.Code == "V21");
        Assert.DoesNotContain(report.Issues, i => i.Code == "V14" && e4.Any(p => i.Message.Contains(p.Id, StringComparison.Ordinal)));
        Assert.DoesNotContain(report.Issues, i => i.Code == "V17" && e4.Any(p => i.Message.Contains(p.Id, StringComparison.Ordinal)));
        Assert.NotEmpty(set.ById["KEY-5"].Practice);
        Assert.Equal(100, set.ById["KEY-5"].Practice.Sum(x => x.Share));

        ContentPack? pack = PackBuilder.Build(set, report);
        Assert.NotNull(pack);
        var catalog = new ResearchCatalog(pack!);
        Assert.All(e4, p => Assert.False(catalog.Get(p.Id).Numeric, $"{p.Id} compiled numeric before WP5"));
        Assert.Contains(catalog.Get("FOD-09").Effects, effect => effect.Kind == EffectKind.Cap && effect.Cap == CapKind.Population);
        EffectDef scythe = Assert.Single(catalog.Get("AGR-09").Effects, effect => effect.Kind == EffectKind.Method);
        Assert.Equal("t11", scythe.Tool);
        Assert.Equal("harvest", scythe.Stage);
    }

    [Fact]
    public void E4_cannot_be_promoted_before_WP5()
    {
        Report report = MutateProject("COM-07", block => block.Replace("status: draft", "status: numeric", StringComparison.Ordinal));
        Assert.Contains(report.Issues, i => i.Code == "V21" && i.Message.Contains("COM-07", StringComparison.Ordinal) && i.Message.Contains("remain status: draft", StringComparison.Ordinal));
    }

    [Fact]
    public void E4_effects_cannot_fall_back_to_prose()
    {
        Report report = MutateProject("COM-07", block =>
        {
            int effects = block.IndexOf("    effects:", StringComparison.Ordinal);
            Assert.True(effects >= 0, "COM-07 has an effects field");
            string head = block[..effects], tail = block[effects..];
            string changed = new Regex(@"kind: [A-Za-z_]+").Replace(tail, "kind: prose", 1);
            Assert.NotEqual(tail, changed);
            return head + changed;
        });
        Assert.Contains(report.Issues, i => i.Code == "V21" && i.Message.Contains("COM-07", StringComparison.Ordinal) && i.Message.Contains("must be typed", StringComparison.Ordinal));
    }

    [Fact]
    public void Method_tool_qualifier_must_resolve_to_a_tool_registry_entry()
    {
        Report report = MutateProject("AGR-09", block => block.Replace("tool: t11", "tool: missing_tool", StringComparison.Ordinal));
        Assert.Contains(report.Issues, i => i.Code == "V12" && i.Message.Contains("missing_tool", StringComparison.Ordinal));
    }

    [Fact]
    public void E4_practice_requires_a_positive_complete_mapping()
    {
        Report report = MutateProject("COM-07", block =>
        {
            string withoutPractice = Regex.Replace(block, @"(?ms)^    practice:.*?(?=^    [A-Za-z_]+:|\z)", "");
            Assert.Contains("    effects:", withoutPractice, StringComparison.Ordinal);
            return withoutPractice.Replace("    effects:", "    practice: [{action: council, share: 50}]\n    effects:", StringComparison.Ordinal);
        });
        Assert.Contains(report.Issues, i => i.Code == "V14" && i.Message.Contains("COM-07", StringComparison.Ordinal) && i.Message.Contains("sum to 50", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("flavor", "flavor text")]
    [InlineData("moment", "Moment text")]
    public void E4_drafts_require_flavor_and_moment(string field, string diagnostic)
    {
        Report report = MutateProject("COM-07", block =>
        {
            string changed = new Regex($@"(?m)^    {field}:.*(?:\r?\n|$)").Replace(block, "", 1);
            Assert.NotEqual(block, changed);
            return changed;
        });
        Assert.Contains(report.Issues, i => i.Code == "V21" && i.Message.Contains("COM-07", StringComparison.Ordinal) && i.Message.Contains(diagnostic, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void E4_requirements_still_use_normal_reference_validation()
    {
        Report report = MutateProject("COM-07", block => new Regex(@"(?m)^    requires:.*$").Replace(block, "    requires: [MISSING-E4-REQ]", 1));
        Assert.Contains(report.Issues, i => i.Code == "V2" && i.Message.Contains("MISSING-E4-REQ", StringComparison.Ordinal));
    }

    [Fact]
    public void E4_gates_still_use_normal_deposit_validation()
    {
        Report report = MutateProject("COM-07", block =>
        {
            string withoutGates = Regex.Replace(block, @"(?ms)^    gates:.*?(?=^    [A-Za-z_]+:|\z)", "");
            Assert.Contains("    requires:", withoutGates, StringComparison.Ordinal);
            return withoutGates.Replace("    requires:", "    gates: [{kind: has, target: Unregistered E4 deposit}]\n    requires:", StringComparison.Ordinal);
        });
        Assert.Contains(report.Issues, i => i.Code == "V19" && i.Message.Contains("COM-07", StringComparison.Ordinal));
    }

    private static (ContentSet Set, Report Report) Validate()
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        Validator.Run(set, report);
        return (set, report);
    }

    private static Report MutateProject(string id, Func<string, string> mutate)
    {
        var files = ContentFixture.Files();
        int fileIndex = files.FindIndex(f => f.Text.Contains($"  - id: {id}\n", StringComparison.Ordinal));
        Assert.True(fileIndex >= 0, $"no project {id}");
        string path = files[fileIndex].Path;
        string text = files[fileIndex].Text;
        int start = text.IndexOf($"  - id: {id}\n", StringComparison.Ordinal);
        int next = text.IndexOf("\n  - id: ", start + 1, StringComparison.Ordinal);
        if (next < 0) next = text.Length;
        string block = text[start..next];
        string changed = mutate(block);
        Assert.NotEqual(block, changed);
        files[fileIndex] = (path, text[..start] + changed + text[next..]);
        (ContentSet set, Report report) = ContentFixture.Load(files);
        return Validator.Run(set, report);
    }
}
