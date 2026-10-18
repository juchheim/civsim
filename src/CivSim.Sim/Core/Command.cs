namespace CivSim.Sim.Core;

/// <summary>
/// The player and AI actions that reach the simulation. Presentation code builds <see cref="Command"/>s and
/// never writes simulation state (docs/MVP/15 §5.1, rule D7). Numeric values are part of the replay
/// format: never renumber, only append.
/// </summary>
public enum CommandKind : ushort
{
    None = 0,
    /// <summary>A = slot (0 primary, 1 secondary, 2 tertiary), B = project id.</summary>
    SetSlot = 1,
    /// <summary>A = project id appended to the civilization's queue.</summary>
    QueueProject = 2,
    /// <summary>A = queue index to remove.</summary>
    DequeueProject = 3,
    /// <summary>A = theme (0 craft, 1 kin, 2 reach), B = weight in percent.</summary>
    SetLeaning = 4,
    /// <summary>A = tile x, B = tile y.</summary>
    PlaceBeacon = 5,
    /// <summary>A = beacon index.</summary>
    RemoveBeacon = 6,
    /// <summary>A = posture (see <see cref="Posture"/>).</summary>
    SetPosture = 7,
}

/// <summary>Civilization-wide stance (docs/MVP/01 §5.4).</summary>
public enum Posture : byte
{
    Balanced = 0,
    Expand = 1,
    Prosper = 2,
    Fortify = 3,
    Raid = 4,
}

/// <summary>One scheduled command. <see cref="Tick"/> is the simulation tick at which it takes effect.</summary>
public readonly record struct Command(long Tick, byte Civ, CommandKind Kind, int A, int B, int C);
