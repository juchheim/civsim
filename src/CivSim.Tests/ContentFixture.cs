using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Content;

namespace CivSim.Tests;

/// <summary>Loads the real content into memory so tests can mutate one file's text and revalidate.</summary>
internal static class ContentFixture
{
    public static string ContentDir => Path.Combine(RepoPaths.Root, "content");

    public static List<(string Path, string Text)> Files()
        => new DirectorySource(ContentDir).Files().ToList();

    public static (ContentSet Set, Report Report) Load(IEnumerable<(string Path, string Text)>? files = null)
    {
        var source = new MemorySource();
        foreach ((string path, string text) in files ?? Files()) source.Add(path, text);
        var report = new Report();
        ContentSet set = ContentSet.Load(source, report);
        return (set, report);
    }

    /// <summary>Applies one text replacement to one file (which must contain the old text exactly once) and validates.</summary>
    public static Report Mutate(string path, string oldText, string newText, int expectedCount = 1)
    {
        var files = Files();
        int index = files.FindIndex(f => f.Path == path);
        Assert.True(index >= 0, $"no content file {path}");
        string text = files[index].Text;
        int count = 0, at = 0;
        while ((at = text.IndexOf(oldText, at, StringComparison.Ordinal)) >= 0) { count++; at += oldText.Length; }
        Assert.True(count == expectedCount, $"expected {expectedCount} occurrence(s) of '{oldText}' in {path}, found {count}");
        files[index] = (path, text.Replace(oldText, newText, StringComparison.Ordinal));
        (ContentSet set, Report report) = Load(files);
        return Validator.Run(set, report);
    }

    public static bool Has(Report report, string code, Severity severity = Severity.Error, string? containing = null)
        => report.Issues.Any(i => i.Code == code && i.Severity == severity && (containing is null || i.Message.Contains(containing, StringComparison.Ordinal)));

    public static string Describe(Report report) => string.Join("\n", report.Issues.Select(i => i.ToString()));
}
