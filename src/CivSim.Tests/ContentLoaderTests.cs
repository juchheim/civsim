using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Tests;

public class ContentLoaderTests
{
    private static ContentValue? Parse(string yaml, out Report report)
    {
        report = new Report();
        return YamlLoader.Parse("test.yaml", yaml, report);
    }

    [Fact]
    public void Scalars_are_typed_by_the_strict_rules()
    {
        ContentValue? v = Parse("a: 5\nb: -3\nc: 1.5\nd: true\ne: null\nf: \"1.5\"\ng: yes\nh: 1e5\ni: STN-01\nj: 'x'\n", out Report report);
        Assert.NotNull(v);
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(ValueKind.Int, v!.Get("a")!.Kind);
        Assert.Equal(-3L, v.Get("b")!.IntValue);
        Assert.Equal(ValueKind.Fix, v.Get("c")!.Kind);
        Assert.Equal(Fix64.FromRatio(3, 2), v.Get("c")!.FixValue);
        Assert.True(v.Get("d")!.BoolValue);
        Assert.True(v.Get("e")!.IsNull);
        Assert.Equal(ValueKind.Text, v.Get("f")!.Kind);   // quoted stays text
        Assert.Equal("yes", v.GetString("g"));              // no YAML 1.1 boolean guessing
        Assert.Equal("1e5", v.GetString("h"));              // no exponent numbers
        Assert.Equal("STN-01", v.GetString("i"));
        Assert.Equal("x", v.GetString("j"));
    }

    [Fact]
    public void Flow_and_block_collections_load_with_line_numbers()
    {
        ContentValue? v = Parse("list: [1, 2, 3]\nmap: {k: v, n: 2}\nblock:\n  - x\n  - y\n", out _);
        Assert.Equal(3, v!.Get("list")!.Items.Count);
        Assert.Equal("v", v.Get("map")!.GetString("k"));
        Assert.Equal(new[] { "x", "y" }, v.GetStringList("block"));
        Assert.Equal(4, v.Get("block")!.Items[0].Line);
    }

    [Theory]
    [InlineData("a: &x 1\nb: *x\n", "anchors and aliases")]
    [InlineData("base: &b {k: 1}\nother:\n  <<: *b\n", "merge keys")]
    [InlineData("a: 1\na: 2\n", "duplicate key")]
    [InlineData("a: 1\n---\nb: 2\n", "exactly one YAML document")]
    [InlineData("a: [1, 2\n", "YAML syntax")]
    [InlineData("a: 99999999999999999999\n", "integer out of range")]
    [InlineData("a: 3000000000.5\n", "out of range")]
    public void Strict_subset_violations_are_errors(string yaml, string expected)
    {
        ContentValue? v = Parse(yaml, out Report report);
        Assert.Null(v);
        Assert.True(report.Issues.Any(i => i.Message.Contains(expected)), string.Join("\n", report.Issues));
    }

    [Fact]
    public void ContentValue_helpers_promote_integers_and_ignore_wrong_types()
    {
        ContentValue? v = Parse("i: 4\nf: 0.25\ns: text\nl: [a, b, 3]\n", out _);
        Assert.Equal(Fix64.FromInt(4), v!.GetFix("i"));
        Assert.Equal(Fix64.FromRatio(1, 4), v.GetFix("f"));
        Assert.Null(v.GetInt("s"));
        Assert.Null(v.GetFix("s"));
        Assert.Equal(new[] { "a", "b" }, v.GetStringList("l")); // the integer is skipped
        Assert.Null(v.Get("missing"));
        Assert.False(v.Has("missing"));
    }

    [Fact]
    public void Canonical_json_sorts_keys_and_round_trips_decimals()
    {
        var tree = ContentValue.MapOf(new[]
        {
            new System.Collections.Generic.KeyValuePair<string, ContentValue>("b", ContentValue.Of(Fix64.FromRatio(1, 3))),
            new System.Collections.Generic.KeyValuePair<string, ContentValue>("a", ContentValue.ListOf(new[] { ContentValue.Of(1L), ContentValue.Of("x"), ContentValue.Null, ContentValue.Of(true) })),
        });
        byte[] bytes = CanonicalJson.Write(tree);
        Assert.Equal("{\"a\":[1,\"x\",null,true],\"b\":{\"$fix\":1431655765}}", System.Text.Encoding.UTF8.GetString(bytes));
        ContentValue back = CanonicalJson.Read(bytes);
        Assert.Equal(Fix64.FromRatio(1, 3), back.GetFix("b"));
        Assert.Equal(bytes, CanonicalJson.Write(back));
    }
}
