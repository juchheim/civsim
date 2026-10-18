using System;
using System.IO;

namespace CivSim.Tests;

internal static class RepoPaths
{
    /// <summary>The repository root: the nearest parent directory containing CivSim.slnx.</summary>
    public static string Root { get; } = FindRoot();

    public static string SimSources => Path.Combine(Root, "src", "CivSim.Sim");
    public static string Golden(string name) => Path.Combine(Root, "tests", "golden", name);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivSim.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find CivSim.slnx above " + AppContext.BaseDirectory);
    }
}
