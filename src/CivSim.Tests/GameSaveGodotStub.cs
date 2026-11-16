using System.IO;
using System;

namespace Godot;

/// <summary>Only the save path helpers need Godot OS; persistence tests use their own temporary paths.</summary>
internal static class OS
{
    public static string GetUserDataDir() => Path.GetTempPath();
    public static string[] GetCmdlineUserArgs() => Array.Empty<string>();
}

internal static class GD
{
    public static void Print(string message) { }
}
