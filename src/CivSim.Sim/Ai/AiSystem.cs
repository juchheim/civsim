using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>One civilization's seat at the table: its mind and what that mind knows.</summary>
public sealed class AiSeat
{
    internal AiSeat(int civ, CivControl control, DifficultyProfile profile, IBeliefBuilder builder, IResearchSpark spark,
        IStrategist strategist, Belief belief, AiContext context)
    {
        Civ = civ;
        Control = control;
        Profile = profile;
        Builder = builder;
        Spark = spark;
        Strategist = strategist;
        Belief = belief;
        Context = context;
    }

    public int Civ { get; }
    public CivControl Control { get; }
    public DifficultyProfile Profile { get; }
    public IBeliefBuilder Builder { get; }
    public IResearchSpark Spark { get; }
    public IStrategist Strategist { get; }
    public Belief Belief { get; }
    public AiContext Context { get; }
    public OwnView Own => Context.Own;
}

/// <summary>
/// Runs the AI civilizations' minds once a season, after the economy, research and politics have finished theirs
/// (docs/MVP/14 §2): own view, then belief, then the Spark's research plan, then the Strategist's orders. Everything it
/// does goes through <see cref="AiResearchPort"/> and <see cref="AiOrders"/>; the same rules as a player's.
/// </summary>
public sealed class AiSystem
{
    private readonly TrueWorld _world;
    private readonly AiSeat?[] _seats;
    private readonly AiLog _log = new();
    private readonly ulong _seed;
    private long _tick;

    public AiSystem(LivingEconomyModel economy, ResearchSystem research, PoliticsSystem politics, GeneratedWorld generated,
        ContentPack pack, ulong seed, ReadOnlySpan<CivControl> controls)
    {
        _world = new TrueWorld(economy, research, politics, generated);
        _seed = seed;
        _seats = new AiSeat?[controls.Length];
        for (int civ = 0; civ < controls.Length; civ++)
        {
            CivControl control = controls[civ];
            if (control.Kind == ControlKind.Council) continue;
            Difficulty level = control.Kind == ControlKind.Ai ? control.Spec.Level : Difficulty.Warlord;
            DifficultyProfile profile = DifficultyProfile.From(pack, level);
            (IBeliefBuilder builder, IResearchSpark spark, IStrategist strategist) = AiMinds.Create(control, profile, research.Catalog);
            var map = new MapKnowledge(generated.Tiles.ToArray(), generated.Width, generated.Height);
            var belief = new Belief(civ, controls.Length, research.Catalog.Count, map);
            var context = new AiContext
            {
                Civ = civ, MatchSeed = seed, Spec = control.Spec, Profile = profile, Own = new OwnView(), Belief = belief, Intent = new StrategicIntent(),
                Research = new AiResearchPort(research, civ), Orders = new AiOrders(politics, civ), Catalog = research.Catalog, Log = _log,
            };
            _seats[civ] = new AiSeat(civ, control, profile, builder, spark, strategist, belief, context);
            research.SetAiControlled(civ, true);
            if (strategist.ActsOnPolitics) politics.SetExternalMind(civ, true);
        }
    }

    public int CivCount => _seats.Length;
    public AiSeat? SeatOf(int civ) => _seats[civ];
    public AiLog Log => _log;
    public TrueWorld World => _world;

    /// <summary>Call after <see cref="PoliticsSystem.OnSeasonEnd"/>.</summary>
    public void OnSeasonEnd(long tick)
    {
        _tick = tick;
        for (int civ = 0; civ < _seats.Length; civ++)
        {
            AiSeat? seat = _seats[civ];
            if (seat is null) continue;
            AiContext ctx = seat.Context;
            ctx.Tick = tick;
            ctx.Season = _world.Politics.Season;
            OwnViewBuilder.Build(ctx.Own, civ, tick, _world.Economy, _world.Research, _world.Politics);
            seat.Builder.Update(seat.Belief, _world, seat.Profile);
            // Replan cadence (docs/MVP/14 §9.2): the Settler thinks once a year.
            if (seat.Profile.Replan == ReplanCadence.Yearly && ctx.Season % 4 != 0) continue;
            // The Strategist decides first: its threat and war readings tell the Spark what research is urgent.
            seat.Strategist.Decide(ctx);
            seat.Spark.Decide(ctx);
        }
    }

    public void Hash(ref StateHasher h)
    {
        foreach (AiSeat? seat in _seats)
        {
            if (seat is null) { h.Add(0); continue; }
            h.Add(1);
            seat.Belief.Hash(ref h);
            seat.Context.Intent.Hash(ref h);
            seat.Builder.Hash(ref h);
            seat.Spark.Hash(ref h);
            seat.Strategist.Hash(ref h);
        }
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_tick);
        foreach (AiSeat? seat in _seats)
        {
            if (seat is null) continue;
            seat.Belief.Write(w);
            seat.Context.Intent.Write(w);
            seat.Builder.Write(w);
            seat.Spark.Write(w);
            seat.Strategist.Write(w);
        }
    }

    public void Read(BinaryReader r)
    {
        _tick = r.ReadInt64();
        foreach (AiSeat? seat in _seats)
        {
            if (seat is null) continue;
            seat.Belief.Read(r);
            seat.Context.Intent.Read(r);
            seat.Builder.Read(r);
            seat.Spark.Read(r);
            seat.Strategist.Read(r);
        }
        _log.Clear();
    }
}

/// <summary>Creates the parts of a mind for a kind of control. The smart AI's parts are the real classes; the bots are scripts that use the same interfaces.</summary>
internal static class AiMinds
{
    public static (IBeliefBuilder, IResearchSpark, IStrategist) Create(CivControl control, DifficultyProfile profile, ResearchCatalog catalog)
    {
        if (control.Kind == ControlKind.Ai)
            return (new BeliefBuilder(), new ResearchSpark(control.Spec, profile), new Strategist(control.Spec, profile));
        return BotFactory.Create(control.Bot, catalog);
    }
}
