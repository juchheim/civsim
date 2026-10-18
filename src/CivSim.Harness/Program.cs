using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Toy;

namespace CivSim.Harness;

/// <summary>
/// Headless runner (docs/MVP/15 A9). At M0 it drives the toy determinism canary; later it will run full
/// AI tournaments and regression suites.
/// <code>
/// civsim-harness toy [--seed N] [--ticks N] [--checksum-every N]      run and print the result
/// civsim-harness toy --check-golden FILE                              compare against a golden file (CI)
/// civsim-harness toy --write-golden FILE                              (re)write the golden file
/// civsim-harness toy --record FILE                                    write a replay file
/// civsim-harness replay verify FILE                                   re-run a replay and verify checksums
/// </code>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            return args[0] switch
            {
                "toy" => RunToy(args[1..]),
                "replay" when args.Length >= 3 && args[1] == "verify" => VerifyReplay(args[2]),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: civsim-harness toy [--seed N] [--ticks N] [--checksum-every N] [--check-golden FILE | --write-golden FILE | --record FILE]");
        Console.Error.WriteLine("       civsim-harness replay verify FILE");
        return 2;
    }

    private static int RunToy(string[] args)
    {
        ulong seed = ToyScenario.GoldenSeed;
        long ticks = ToyScenario.GoldenTicks;
        int interval = SimRunner.DefaultChecksumInterval;
        string? checkGolden = null, writeGolden = null, record = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--ticks": ticks = long.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--checksum-every": interval = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                case "--check-golden": checkGolden = Next(args, ref i); break;
                case "--write-golden": writeGolden = Next(args, ref i); break;
                case "--record": record = Next(args, ref i); break;
                default: return Usage();
            }
        }

        var runner = new SimRunner(new ToyModel(seed), interval);
        foreach (Command c in ToyScenario.Commands(seed, ticks)) runner.Submit(c);
        runner.Advance(ticks);

        string result = Describe(seed, ticks, interval, runner);
        Console.WriteLine(result);

        if (record is not null)
        {
            Replay replay = Replay.Record(new ToyModel(seed), seed, ticks, ToyScenario.Commands(seed, ticks), interval);
            using FileStream stream = File.Create(record);
            replay.Write(stream);
            Console.WriteLine($"wrote replay {record}");
        }
        if (writeGolden is not null)
        {
            File.WriteAllText(writeGolden, result + "\n");
            Console.WriteLine($"wrote golden {writeGolden}");
        }
        if (checkGolden is not null)
        {
            string expected = File.ReadAllText(checkGolden).Replace("\r\n", "\n").Trim();
            if (expected != result.Trim())
            {
                Console.Error.WriteLine("DETERMINISM CHECK FAILED");
                Console.Error.WriteLine("expected:");
                Console.Error.WriteLine(expected);
                Console.Error.WriteLine("actual:");
                Console.Error.WriteLine(result);
                return 1;
            }
            Console.WriteLine("determinism check passed");
        }
        return 0;
    }

    private static string Describe(ulong seed, long ticks, int interval, SimRunner runner)
    {
        ulong digest = runner.Digest();
        return string.Join('\n',
            $"seed={seed} ticks={ticks} checksumInterval={interval} commands={runner.AppliedCommands.Count}",
            $"final={runner.CurrentChecksum:x16}",
            $"digest={digest:x16}");
    }

    private static int VerifyReplay(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Replay replay = Replay.Read(stream);
        ReplayVerification result = ReplayVerifier.Verify(replay, seed => new ToyModel(seed));
        Console.WriteLine(result.Ok ? $"replay OK ({replay.Checksums.Count} checksums, {replay.Commands.Count} commands)" : result.Message);
        return result.Ok ? 0 : 1;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
        return args[++i];
    }
}
