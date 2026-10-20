using CivSim.Sim.Core;

namespace CivSim.Sim.View;

/// <summary>
/// Builds the <see cref="Command"/>s a front end sends for a human player (docs/MVP/18 §7). The front end never writes
/// simulation state: it submits these to the <see cref="SimRunner"/> on the sim thread for a tick not yet simulated
/// (usually <c>runner.Tick</c>), and they become part of the replay. The rules decide whether an order takes effect; a
/// refused order does nothing, so the front end should grey out what the frame shows is impossible.
/// </summary>
public static class PlayerCommands
{
    // ---- research (ResearchSystem.Apply) ----
    public static Command SetSlot(long tick, int civ, int slot, int project) => new(tick, (byte)civ, CommandKind.SetSlot, slot, project, 0);
    public static Command ClearSlot(long tick, int civ, int slot) => new(tick, (byte)civ, CommandKind.SetSlot, slot, -1, 0);
    public static Command Enqueue(long tick, int civ, int project) => new(tick, (byte)civ, CommandKind.QueueProject, project, 0, 0);
    public static Command Dequeue(long tick, int civ, int queueIndex) => new(tick, (byte)civ, CommandKind.DequeueProject, queueIndex, 0, 0);
    /// <summary>Theme 0 craft, 1 kin, 2 reach; weight in percent.</summary>
    public static Command SetLeaning(long tick, int civ, int theme, int percent) => new(tick, (byte)civ, CommandKind.SetLeaning, theme, percent, 0);
    public static Command SetCouncil(long tick, int civ, bool on) => new(tick, (byte)civ, CommandKind.SetCouncil, on ? 1 : 0, 0, 0);

    // ---- levers (PoliticsSystem.Step) ----
    public static Command SetPosture(long tick, int civ, Posture posture) => new(tick, (byte)civ, CommandKind.SetPosture, (int)posture, 0, 0);
    /// <summary>A Beacon on a tile (the command carries offset x and y).</summary>
    public static Command PlaceBeacon(long tick, int civ, int tile, int width) => new(tick, (byte)civ, CommandKind.PlaceBeacon, tile % width, tile / width, 0);
    public static Command RemoveBeacon(long tick, int civ, int beaconIndex) => new(tick, (byte)civ, CommandKind.RemoveBeacon, beaconIndex, 0, 0);

    // ---- politics orders (PoliticsSystem.Step, CommandKind 10 to 16) ----
    public static Command SendGift(long tick, int civ, int toCiv) => new(tick, (byte)civ, CommandKind.SendGift, toCiv, 0, 0);
    public static Command SendBarter(long tick, int civ, int withCiv) => new(tick, (byte)civ, CommandKind.SendBarter, withCiv, 0, 0);
    public static Command LaunchRaid(long tick, int civ, int targetSettlement, int partySize) => new(tick, (byte)civ, CommandKind.LaunchRaid, targetSettlement, partySize, 0);
    public static Command LaunchAssault(long tick, int civ, int targetSettlement) => new(tick, (byte)civ, CommandKind.LaunchAssault, targetSettlement, 0, 0);
    public static Command DeclareWar(long tick, int civ, int onCiv) => new(tick, (byte)civ, CommandKind.DeclareWar, onCiv, 0, 0);
    public static Command MakePeace(long tick, int civ, int withCiv) => new(tick, (byte)civ, CommandKind.MakePeace, withCiv, 0, 0);
    public static Command FormAlliance(long tick, int civ, int withCiv) => new(tick, (byte)civ, CommandKind.FormAlliance, withCiv, 0, 0);
}
