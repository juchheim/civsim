using System;
using System.IO;

namespace CivSim.Sim.Core;

/// <summary>
/// A simulation the <see cref="SimRunner"/> can drive. The real game world implements this in later
/// milestones; the toy model in <c>CivSim.Sim.Toy</c> is a permanent determinism canary.
/// </summary>
public interface ISimModel
{
    /// <summary>
    /// Simulates exactly one tick. <paramref name="commands"/> are the commands scheduled for this tick, in
    /// submission order. The model must be a pure function of its state, the tick and the commands.
    /// </summary>
    void Step(long tick, ReadOnlySpan<Command> commands);

    /// <summary>A fingerprint of all state that affects future behavior.</summary>
    ulong ComputeChecksum();

    /// <summary>Writes all state needed to resume (little-endian, no platform-dependent types).</summary>
    void WriteSnapshot(BinaryWriter writer);

    /// <summary>Restores state written by <see cref="WriteSnapshot"/> from the same build.</summary>
    void ReadSnapshot(BinaryReader reader);
}
