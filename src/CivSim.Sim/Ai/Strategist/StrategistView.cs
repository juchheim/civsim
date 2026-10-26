using System;
using CivSim.Sim.Core;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// The orders the Strategist gives (docs/MVP/14 §2). In play they go to <see cref="AiOrders"/>, which keeps the hard
/// rules of docs/MVP/13; in unit tests a recorder stands in. The Strategist never infers anything from a refusal.
/// </summary>
public interface IStrategistActions
{
    bool Gift(int toCiv);
    bool Gift(int toCiv, int targetSettlement) => Gift(toCiv);
    bool Barter(int withCiv);
    bool Barter(int withCiv, int targetSettlement) => Barter(withCiv);
    bool Raid(int targetSettlement, int partySize);
    bool Assault(int targetSettlement);
    bool DeclareWar(int civ);
    bool MakePeace(int civ);
    bool Ally(int civ);
    bool SetPosture(Posture posture);
    bool PlaceBeacon(int tile);
    void RemoveBeacon(int index);

    // ---- the Bronze levers (docs/MVP/22 §4.6, §13.3; WP8a). Defaults keep test recorders compiling: they refuse. ----
    /// <summary>Mandate in hand and its cap (the civilization's own research state).</summary>
    int Mandate => 0;
    int MandateCap => 0;
    int DecreeCost(DecreeKind kind) => int.MaxValue;
    /// <summary>
    /// Queues a Decree through the same path as a player's command 18: it resolves at the end of the season, Mandate is spent only
    /// on success, and a refusal is a <c>DecreeRefused</c> event. The AI is never given Mandate it did not earn.
    /// </summary>
    bool IssueDecree(DecreeKind kind, int target, int extra) => false;
    /// <summary>The Embargo toggle (command 19's rule).</summary>
    bool SetEmbargo(int civ, TradeGood good, bool on) => false;
    /// <summary>A raid on a caravan on the road (command 12 with a caravan target).</summary>
    bool RaidCaravan(int caravanId, int partySize) => false;
    /// <summary>A raid on a mining camp that was directly seen by the Strategist.</summary>
    bool RaidMiningCamp(int campId, int partySize) => false;
    /// <summary>
    /// Whether the Council's Decree policy keeps issuing for this civilization (the default until the AI plans Decrees itself; the AI
    /// work package turns it off when its Strategist takes over).
    /// </summary>
    void UseCouncilDecrees(bool on) { }
}

/// <summary>
/// Everything the Strategist may read, and nothing else (docs/MVP/14 §1, §10.4 cheating check): its own civilization
/// exactly, the fog-limited belief about everyone else, its own research state, the catalog and its profile. Built from
/// an <see cref="AiContext"/> in play; built by hand in unit tests, which is how they show that decisions depend on the
/// belief alone.
/// </summary>
public sealed class StrategistView
{
    public int Civ { get; init; }
    public int Season { get; init; }
    public long Tick { get; init; }
    public ulong MatchSeed { get; init; }
    public AiSpec Spec { get; init; }
    public DifficultyProfile Profile { get; init; } = null!;
    public OwnView Own { get; init; } = null!;
    public Belief Belief { get; init; } = null!;
    public StrategicIntent Intent { get; init; } = null!;
    public ResearchCatalog Catalog { get; init; } = null!;
    /// <summary>Whether this civilization has completed a project (its own research, exact).</summary>
    public Func<int, bool> OwnDone { get; init; } = _ => false;
    /// <summary>Where the reasons go (docs/MVP/14 §11); may be null in tests that do not look.</summary>
    public AiLog? Log { get; init; }

    public void Note(string kind, int subject, int other, Numerics.Fix64 score, string reason)
        => Log?.Add(new AiDecision(Tick, Season, Civ, Strategist.Layer, kind, subject, other, score, reason));

    public static StrategistView From(AiContext ctx) => new()
    {
        Civ = ctx.Civ, Season = ctx.Season, Tick = ctx.Tick, MatchSeed = ctx.MatchSeed, Spec = ctx.Spec, Profile = ctx.Profile,
        Own = ctx.Own, Belief = ctx.Belief, Intent = ctx.Intent, Catalog = ctx.Catalog, OwnDone = ctx.Research.IsDone, Log = ctx.Log,
    };
}

/// <summary>Sends the Strategist's orders through the civilization's own <see cref="AiOrders"/>.</summary>
public sealed class OrdersActions : IStrategistActions
{
    private readonly AiOrders _orders;
    public OrdersActions(AiOrders orders) { _orders = orders; }
    public bool Gift(int toCiv) => _orders.Gift(toCiv);
    public bool Gift(int toCiv, int targetSettlement) => _orders.Gift(toCiv, targetSettlement);
    public bool Barter(int withCiv) => _orders.Barter(withCiv);
    public bool Barter(int withCiv, int targetSettlement) => _orders.Barter(withCiv, targetSettlement);
    public bool Raid(int targetSettlement, int partySize) => _orders.Raid(targetSettlement, partySize);
    public bool Assault(int targetSettlement) => _orders.Assault(targetSettlement);
    public bool DeclareWar(int civ) => _orders.DeclareWar(civ);
    public bool MakePeace(int civ) => _orders.MakePeace(civ);
    public bool Ally(int civ) => _orders.Ally(civ);
    public bool SetPosture(Posture posture) => _orders.SetPosture(posture);
    public bool PlaceBeacon(int tile) => _orders.PlaceBeacon(tile);
    public void RemoveBeacon(int index) => _orders.RemoveBeacon(index);
    public int Mandate => _orders.Mandate;
    public int MandateCap => _orders.MandateCap;
    public int DecreeCost(DecreeKind kind) => _orders.DecreeCost(kind);
    public bool IssueDecree(DecreeKind kind, int target, int extra) => _orders.IssueDecree(kind, target, extra);
    public bool SetEmbargo(int civ, TradeGood good, bool on) => _orders.SetEmbargo(civ, good, on);
    public bool RaidCaravan(int caravanId, int partySize) => _orders.RaidCaravan(caravanId, partySize);
    public bool RaidMiningCamp(int campId, int partySize) => _orders.RaidMiningCamp(campId, partySize);
    public void UseCouncilDecrees(bool on) => _orders.UseCouncilDecrees(on);
}
