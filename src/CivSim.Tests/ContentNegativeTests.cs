using System;
using System.Collections.Generic;
using System.Linq;
using CivSim.Content;

namespace CivSim.Tests;

/// <summary>
/// One injected defect per check (docs/MVP/16 §10): each mutation changes a single spot in the real content and
/// must produce the matching finding, proving the check can actually fail.
/// </summary>
public class ContentNegativeTests
{
    private const string Stn = "catalog/stn-stoneworking.yaml";
    private const string Keys = "catalog/keystones.yaml";

    public static IEnumerable<object[]> ErrorCases() => new List<object[]>
    {
        // V1 schema and YAML strictness
        new object[] { "V1 unknown field", Stn, "    status: numeric\n    requires: []\n", "    status: numeric\n    bogus: 1\n    requires: []\n", "V1", "unknown field 'bogus'" },
        new object[] { "V1 bad enum", Stn, "    type: M\n    status: numeric\n    requires: []\n", "    type: Z\n    status: numeric\n    requires: []\n", "V1", "expected one of" },
        new object[] { "V1 missing name", Stn, "    name: Pebble Choppers\n", "", "V1", "missing required field 'name'" },
        new object[] { "V1 bad era", Stn, "    era: 0\n    type: M\n    status: numeric\n    requires: []\n", "    era: 11\n    type: M\n    status: numeric\n    requires: []\n", "V1", "outside" },
        new object[] { "V1 yaml syntax", Stn, "projects:\n", "projects: [\n", "V1", "YAML syntax" },
        new object[] { "V1 duplicate key", Stn, "    era: 0\n    type: M\n    status: numeric\n    requires: []\n", "    era: 0\n    era: 0\n    type: M\n    status: numeric\n    requires: []\n", "V1", "duplicate key 'era'" },
        new object[] { "V1 anchor", Stn, "name: Pebble Choppers", "name: &a Pebble Choppers", "V1", "anchors and aliases" },
        // V2 references, V3 cycles
        new object[] { "V2 dangling requirement", Stn, "requires: [STN-01]", "requires: [STN-99]", "V2", "unknown project STN-99" },
        new object[] { "V2 duplicate id", Stn, "id: STN-02", "id: STN-01", "V2", "duplicate project id STN-01" },
        new object[] { "V3 cycle", Stn, "requires: []", "requires: [STN-02]", "V3", "prerequisite cycle" },
        // V4 and V5 era order
        new object[] { "V4 later-era requirement", Stn, "requires: []", "requires: [STN-08]", "V4", "from later era" },
        new object[] { "V5 keystone prerequisite era", Keys, "requires: [COM-02, FIR-02, STN-04, SOC-01]", "requires: [COM-02, FIR-02, STN-05, SOC-01]", "V5", "prerequisite STN-05" },
        new object[] { "V5 enters mismatch", Keys, "    enters: 1\n", "    enters: 2\n", "V5", "enters era 2" },
        // V8 gates
        new object[] { "V8 unguaranteed gate", "registries/guarantees.yaml", "  - {id: wolves, scope: start, radius: 20, gate: Wolves}\n", "", "V8", "[Seen: wolves]" },
        // V10 effects
        new object[] { "V10 hazard out of range", "catalog/fir-fire-heat.yaml", "{kind: hazard, cause: exposure, mult: 0.92}", "{kind: hazard, cause: exposure, mult: 5}", "V10", "above the allowed maximum" },
        new object[] { "V10 unknown kind", Keys, "{kind: era_advance, era: 1}", "{kind: era_advancex, era: 1}", "V10", "unknown effect kind" },
        new object[] { "V10 method mult and rate", Stn, "{kind: method, action: chop, rung: 1, name: Pebble chopper, mult: 1.5}", "{kind: method, action: chop, rung: 1, name: Pebble chopper, mult: 1.5, rate: 2, unit: R/ws}", "V10", "exactly one of 'mult' or 'rate'" },
        new object[] { "V10 prose on a numeric project", Stn, "{kind: good, id: G-TOOL1}", "{kind: prose, text: x}", "V10", "only allowed on draft projects" },
        // V11 ladders
        new object[] { "V11 duplicate rung", "catalog/sea-watercraft-seafaring.yaml", "rung: 2,", "rung: 1,", "V11", "rung 1 is defined by both" },
        new object[] { "V11 skipped rung", "catalog/sea-watercraft-seafaring.yaml", "rung: 3,", "rung: 5,", "V11", "skips rung" },
        // V12 registries
        new object[] { "V12 unknown good", Stn, "{kind: good, id: G-TOOL1}", "{kind: good, id: G-TOOLX}", "V12", "G-TOOLX" },
        // V13 cost
        new object[] { "V13 cost override without reason", Stn, "    status: numeric\n    requires: []\n", "    status: numeric\n    cost: 5\n    requires: []\n", "V13", "gives no cost_reason" },
        new object[] { "V13 ack without reason", "catalog/hnt-hunting-fishing.yaml", "    ack_reason:", "    note_x:", "V1", "unknown field 'note_x'" },
        // V14 practice
        new object[] { "V14 practice shares", Stn, "practice: [{action: knap, share: 50}, {action: chop, share: 50}]\n    flavor: The stone remembered how to cut.", "practice: [{action: knap, share: 50}, {action: chop, share: 40}]\n    flavor: The stone remembered how to cut.", "V14", "sum to 90" },
        new object[] { "V14 unknown practice action", Stn, "practice: [{action: knap, share: 50}, {action: chop, share: 50}]\n    flavor: The stone remembered how to cut.", "practice: [{action: kna, share: 50}, {action: chop, share: 50}]\n    flavor: The stone remembered how to cut.", "V14", "'kna'" },
        // V17 text
        new object[] { "V17 missing flavor", Stn, "    flavor: The stone remembered how to cut.\n", "", "V17", "no flavor line" },
        // V18 openings
        new object[] { "V18 illegal opening", "registries/openings.yaml", "KEY-1, SOC-03", "SOC-03", "V18", "opening" },
    };

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public void Each_check_catches_its_injected_defect(string name, string file, string oldText, string newText, string code, string messagePart)
    {
        Report report = ContentFixture.Mutate(file, oldText, newText);
        Assert.True(ContentFixture.Has(report, code, Severity.Error, messagePart),
            $"{name}: expected an error {code} containing '{messagePart}', got:\n{ContentFixture.Describe(report)}");
    }

    [Fact]
    public void An_ack_without_a_reason_is_an_error()
    {
        string path = "catalog/hnt-hunting-fishing.yaml";
        var files = ContentFixture.Files();
        int i = files.FindIndex(f => f.Path == path);
        string text = files[i].Text;
        int start = text.IndexOf("    ack_reason:", StringComparison.Ordinal);
        int end = text.IndexOf('\n', start) + 1;
        files[i] = (path, text.Remove(start, end - start));
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Validator.Run(set, report);
        Assert.True(ContentFixture.Has(report, "V13", Severity.Error, "no ack_reason"), ContentFixture.Describe(report));
    }

    [Fact]
    public void Removing_an_ack_makes_the_accepted_warning_appear()
    {
        Report v11 = ContentFixture.Mutate("catalog/hnt-hunting-fishing.yaml", "    ack: [V11]\n", "");
        Assert.True(ContentFixture.Has(v11, "V11", Severity.Warning, "HNT-05"), ContentFixture.Describe(v11));
        Report v9 = ContentFixture.Mutate(Keys, "    ack: [V9]\n", "");
        Assert.True(ContentFixture.Has(v9, "V9", Severity.Warning, "KEY-1"), ContentFixture.Describe(v9));
    }

    [Fact]
    public void A_missing_registry_file_is_an_error()
    {
        var files = ContentFixture.Files().Where(f => f.Path != "registries/practice.yaml").ToList();
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Assert.True(ContentFixture.Has(report, "V1", Severity.Error, "required registry file is missing"), ContentFixture.Describe(report));
    }

    [Fact]
    public void A_missing_era_is_an_error()
    {
        string id9 = "  - {id: 9, name: Information,";
        var files = ContentFixture.Files();
        int i = files.FindIndex(f => f.Path == "registries/eras.yaml");
        var lines = files[i].Text.Split('\n').Where(l => !l.StartsWith(id9, StringComparison.Ordinal)).ToList();
        files[i] = (files[i].Path, string.Join('\n', lines));
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Validator.Run(set, report);
        Assert.True(ContentFixture.Has(report, "V2", Severity.Error, "era 9 is missing"), ContentFixture.Describe(report));
    }

    [Fact]
    public void A_file_outside_the_layout_is_ignored_with_a_warning()
    {
        var files = ContentFixture.Files();
        files.Add(("notes/extra.yaml", "schema: 1\n"));
        (ContentSet set, Report report) = ContentFixture.Load(files);
        Assert.True(ContentFixture.Has(report, "V1", Severity.Warning, "not part of the content layout"));
    }
}
