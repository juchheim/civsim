using System;
using System.Collections.Generic;
using System.IO;

namespace CivSim.Content;

/// <summary>Where content files come from: a directory on disk, or in-memory text (used by tests).</summary>
public interface IContentSource
{
    /// <summary>All YAML files as (relative path with forward slashes, text).</summary>
    IEnumerable<(string Path, string Text)> Files();
}

public sealed class DirectorySource : IContentSource
{
    private readonly string _root;

    public DirectorySource(string root) => _root = root;

    public IEnumerable<(string Path, string Text)> Files()
    {
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException($"Content directory not found: {_root}");
        var paths = new List<string>(Directory.EnumerateFiles(_root, "*.yaml", SearchOption.AllDirectories));
        paths.Sort(StringComparer.Ordinal); // load order never affects results, but keep it stable anyway
        foreach (string full in paths)
            yield return (Path.GetRelativePath(_root, full).Replace('\\', '/'), File.ReadAllText(full));
    }
}

public sealed class MemorySource : IContentSource
{
    private readonly List<(string Path, string Text)> _files = new();

    public MemorySource Add(string path, string text)
    {
        _files.Add((path, text));
        return this;
    }

    public IEnumerable<(string Path, string Text)> Files() => _files;
}
