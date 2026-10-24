using CivSim.Sim.Core;
using CivSim.Sim.Politics;

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
    /// <summary>A Beacon on a tile; replace the nearest existing Beacon if at capacity when requested.</summary>
    public static Command PlaceBeacon(long tick, int civ, int tile, int width, bool replaceIfFull = false)
        => new(tick, (byte)civ, CommandKind.PlaceBeacon, tile % width, tile / width, replaceIfFull ? 1 : 0);
    public static Command RemoveBeacon(long tick, int civ, int beaconIndex) => new(tick, (byte)civ, CommandKind.RemoveBeacon, beaconIndex, 0, 0);

    // ---- politics orders (PoliticsSystem.Step, CommandKind 10 to 17) ----
    /// <summary>Target settlement is optional; -1 keeps the rules' closest-pair destination.</summary>
    public static Command SendGift(long tick, int civ, int toCiv, int targetSettlement = -1)
        => new(tick, (byte)civ, CommandKind.SendGift, toCiv, targetSettlement + 1, 0);
    /// <summary>Target settlement is optional; -1 keeps the rules' closest-pair destination.</summary>
    public static Command SendBarter(long tick, int civ, int withCiv, int targetSettlement = -1)
        => new(tick, (byte)civ, CommandKind.SendBarter, withCiv, targetSettlement + 1, 0);
    public static Command LaunchRaid(long tick, int civ, int targetSettlement, int partySize) => new(tick, (byte)civ, CommandKind.LaunchRaid, targetSettlement, partySize, 0);
    public static Command LaunchAssault(long tick, int civ, int targetSettlement) => new(tick, (byte)civ, CommandKind.LaunchAssault, targetSettlement, 0, 0);
    public static Command DeclareWar(long tick, int civ, int onCiv) => new(tick, (byte)civ, CommandKind.DeclareWar, onCiv, 0, 0);
    public static Command MakePeace(long tick, int civ, int withCiv) => new(tick, (byte)civ, CommandKind.MakePeace, withCiv, 0, 0);
    public static Command FormAlliance(long tick, int civ, int withCiv) => new(tick, (byte)civ, CommandKind.FormAlliance, withCiv, 0, 0);
    public static Command RecallParty(long tick, int civ, int expeditionIndex) => new(tick, (byte)civ, CommandKind.RecallParty, expeditionIndex, 0, 0);
    /// <summary>A raid on a caravan on the road (the frame's PartyId of a Caravan party).</summary>
    public static Command RaidCaravan(long tick, int civ, int caravanId, int partySize) => new(tick, (byte)civ, CommandKind.LaunchRaid, -(caravanId + 1), partySize, 0);

    // ---- Decrees and the Embargo (docs/MVP/22 §4.5; CommandKind 18 and 19) ----
    public static Command FoundVillage(long tick, int civ, int tile) => Decree(tick, civ, DecreeKind.Found, tile, 0);
    public static Command SendExpedition(long tick, int civ, int tile) => Decree(tick, civ, DecreeKind.Expedition, tile, 0);
    public static Command OpenMine(long tick, int civ, int tile) => Decree(tick, civ, DecreeKind.Mine, tile, 0);
    public static Command RaiseLevy(long tick, int civ, int targetSettlement) => Decree(tick, civ, DecreeKind.Levy, targetSettlement, 0);
    /// <summary>Corvée at an own settlement; target is a <c>LivingEconomyModel.CorveeTarget</c> (0: the top public work).</summary>
    public static Command Corvee(long tick, int civ, int settlement, int target = 0) => Decree(tick, civ, DecreeKind.Corvee, settlement, target);
    /// <summary>A compact with a civilization; pass <c>DecreeArgs.FolkParty(settlement)</c> as the partner for a Free Folk village.</summary>
    public static Command ProposeCompact(long tick, int civ, int partner, TradeGood good, bool sell)
        => Decree(tick, civ, DecreeKind.Compact, partner, DecreeArgs.PackCompact(good, sell));
    public static Command InviteVillage(long tick, int civ, int folkSettlement)
        => Decree(tick, civ, DecreeKind.Compact, DecreeArgs.FolkParty(folkSettlement), DecreeArgs.PackCompact(TradeGood.None, false, CompactVariant.Invite));
    public static Command ProposePact(long tick, int civ, int partner, bool treaty = false)
        => Decree(tick, civ, DecreeKind.Compact, partner, DecreeArgs.PackCompact(TradeGood.None, false, treaty ? CompactVariant.Treaty : CompactVariant.Pact));
    public static Command SetEmbargo(long tick, int civ, int onCiv, TradeGood good, bool on)
        => new(tick, (byte)civ, CommandKind.SetEmbargo, onCiv, (int)good, on ? 1 : 0);

    public static Command Decree(long tick, int civ, DecreeKind kind, int target, int extra)
        => new(tick, (byte)civ, CommandKind.IssueDecree, (int)kind, target, extra);
}
