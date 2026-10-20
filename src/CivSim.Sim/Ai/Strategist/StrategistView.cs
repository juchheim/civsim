using System;
using CivSim.Sim.Core;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// The orders the Strategist gives (docs/MVP/14 §2). In play they go to <see cref="AiOrders"/>, which keeps the hard
/// rules of docs/MVP/13; in unit tests a recorder stands in. The Strategist never infers anything from a refusal.
/// </summary>
public interface IStrategistActions
{
    bool Gift(int toCiv);
    bool Barter(int withCiv);
    bool Raid(int targetSettlement, int partySize);
    bool Assault(int targetSettlement);
    bool DeclareWar(int civ);
    bool MakePeace(int civ);
    bool Ally(int civ);
    bool SetPosture(Posture posture);
    bool PlaceBeacon(int tile);
    void RemoveBeacon(int index);
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
    public bool Barter(int withCiv) => _orders.Barter(withCiv);
    public bool Raid(int targetSettlement, int partySize) => _orders.Raid(targetSettlement, partySize);
    public bool Assault(int targetSettlement) => _orders.Assault(targetSettlement);
    public bool DeclareWar(int civ) => _orders.DeclareWar(civ);
    public bool MakePeace(int civ) => _orders.MakePeace(civ);
    public bool Ally(int civ) => _orders.Ally(civ);
    public bool SetPosture(Posture posture) => _orders.SetPosture(posture);
    public bool PlaceBeacon(int tile) => _orders.PlaceBeacon(tile);
    public void RemoveBeacon(int index) => _orders.RemoveBeacon(index);
}
