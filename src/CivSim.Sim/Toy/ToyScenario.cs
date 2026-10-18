using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Toy;

/// <summary>
/// The fixed scenario used by the golden check and the harness: a seed, a number of ticks, and a
/// command script derived deterministically from the seed.
/// </summary>
public static class ToyScenario
{
    public const ulong GoldenSeed = 20261018;
    public const long GoldenTicks = 3000;

    private static readonly ulong ScriptStream = Hash64.OfString("toy.script");

    /// <summary>A hand-written opening plus 24 seed-derived commands, in submission order.</summary>
    public static List<Command> Commands(ulong seed, long totalTicks)
    {
        var commands = new List<Command>
        {
            new(300, 0, CommandKind.PlaceBeacon, ToyModel.Width - 4, ToyModel.Height - 4, 0),
            new(600, 1, CommandKind.SetPosture, (int)Posture.Expand, 0, 0),
            new(900, 2, CommandKind.PlaceBeacon, 4, 4, 0),
            new(1200, 3, CommandKind.SetPosture, (int)Posture.Expand, 0, 0),
        };
        Pcg32 rng = Pcg32.ForStream(seed, ScriptStream, 0, 0);
        var random = new List<Command>();
        for (int i = 0; i < 24; i++)
        {
            long tick = 1 + rng.NextBelow((uint)(totalTicks - 1));
            byte civ = (byte)rng.NextBelow(ToyModel.Civs);
            random.Add(rng.NextBelow(3) switch
            {
                0 => new Command(tick, civ, CommandKind.PlaceBeacon,
                    rng.NextInt(0, ToyModel.Width), rng.NextInt(0, ToyModel.Height), 0),
                1 => new Command(tick, civ, CommandKind.RemoveBeacon, 0, 0, 0),
                _ => new Command(tick, civ, CommandKind.SetPosture, rng.NextInt(0, 5), 0, 0),
            });
        }
        commands.AddRange(random);
        return commands;
    }
}
