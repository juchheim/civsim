using System;
using System.IO;
using CivSim.Content;
using CivSim.Sim.Content;

namespace CivSim.Tests;

public class CliTests
{
    private static (int Code, string Output) Run(params string[] args)
    {
        TextWriter oldOut = Console.Out, oldErr = Console.Error;
        var sw = new StringWriter();
        Console.SetOut(sw);
        Console.SetError(sw);
        try { return (CivSim.Cli.Program.Main(args), sw.ToString()); }
        finally { Console.SetOut(oldOut); Console.SetError(oldErr); }
    }

    private static string CopyContent(Action<string>? tamper = null)
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-cli-" + Guid.NewGuid().ToString("N"));
        foreach (string file in Directory.EnumerateFiles(ContentFixture.ContentDir, "*.yaml", SearchOption.AllDirectories))
        {
            string target = Path.Combine(dir, Path.GetRelativePath(ContentFixture.ContentDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        tamper?.Invoke(dir);
        return dir;
    }

    [Fact]
    public void Validate_succeeds_on_the_real_content_even_in_strict_mode()
    {
        (int code, string output) = Run("validate", "--content", ContentFixture.ContentDir, "--strict");
        Assert.Equal(0, code);
        Assert.Contains("0 error(s), 0 warning(s)", output);
        Assert.Contains("509 projects + 10 keystones in 33 lineages", output);
    }

    [Fact]
    public void Validate_fails_on_broken_content()
    {
        string dir = CopyContent(d =>
        {
            string path = Path.Combine(d, "catalog", "stn-stoneworking.yaml");
            File.WriteAllText(path, File.ReadAllText(path).Replace("requires: [STN-01]", "requires: [STN-99]"));
        });
        try
        {
            (int code, string output) = Run("validate", "--content", dir);
            Assert.Equal(1, code);
            Assert.Contains("unknown project STN-99", output);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Build_pack_writes_a_loadable_pack_and_checks_the_golden_summary()
    {
        string dir = Path.Combine(Path.GetTempPath(), "civsim-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string pack = Path.Combine(dir, "test.pack");
            (int code, string output) = Run("build-pack", "--content", ContentFixture.ContentDir, "--out", pack, "--check-golden", RepoPaths.Golden("content-pack.txt"));
            Assert.True(code == 0, output);
            Assert.Contains("content pack check passed", output);
            Assert.Equal(CivSim.Sim.Content.ContentPack.Load(File.ReadAllBytes(pack)).HashHex, ContentPack.Load(File.ReadAllBytes(pack)).HashHex);

            (int infoCode, string info) = Run("pack-info", pack);
            Assert.Equal(0, infoCode);
            Assert.Contains("519 projects/keystones", info);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Build_pack_refuses_invalid_content()
    {
        string dir = CopyContent(d => File.Delete(Path.Combine(d, "registries", "goods.yaml")));
        try
        {
            string pack = Path.Combine(dir, "x.pack");
            (int code, _) = Run("build-pack", "--content", dir, "--out", pack);
            Assert.Equal(1, code);
            Assert.False(File.Exists(pack));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Report_commands_print_the_golden_tables()
    {
        (int c1, string stats) = Run("stats", "--content", ContentFixture.ContentDir);
        Assert.Equal(0, c1);
        Assert.Contains("craft 170 · kin 161 · reach 178", stats);
        Assert.Contains("E0 34 · E1 59 · E2 52", stats);

        (int c2, string paths) = Run("paths", "--content", ContentFixture.ContentDir);
        Assert.Equal(0, c2);
        Assert.Contains("KEY-10", paths);
        Assert.Contains("338", paths);

        (int c3, string gates) = Run("gates", "--content", ContentFixture.ContentDir);
        Assert.Equal(0, c3);
        Assert.Contains("clay", gates);
        Assert.Contains("KEY-3", gates);
    }

    [Fact]
    public void Unknown_commands_and_missing_directories_are_usage_errors()
    {
        Assert.Equal(2, Run().Code);
        Assert.Equal(2, Run("frobnicate").Code);
        Assert.Equal(2, Run("validate", "--content", Path.Combine(Path.GetTempPath(), "does-not-exist-civsim")).Code);
    }
}
