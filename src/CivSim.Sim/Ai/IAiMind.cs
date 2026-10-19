using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>State every AI part must be able to hash and save, so a match resumes and replays exactly.</summary>
public interface IAiMind
{
    void Hash(ref StateHasher hasher);
    void Write(BinaryWriter writer);
    void Read(BinaryReader reader);
}

/// <summary>
/// What an AI part may read and do. Nothing here reaches another civilization's true state: the own view is exact, the
/// belief is the fog-limited picture of everyone else, and the ports act only for this civilization (docs/MVP/14 §1).
/// </summary>
public sealed class AiContext
{
    public int Civ { get; internal set; }
    public int Season { get; internal set; }
    public long Tick { get; internal set; }
    public ulong MatchSeed { get; internal set; }
    public AiSpec Spec { get; internal set; }
    public DifficultyProfile Profile { get; internal set; } = null!;
    public OwnView Own { get; internal set; } = null!;
    public Belief Belief { get; internal set; } = null!;
    /// <summary>Shared by the Strategist (writes) and the Spark (reads); the Strategist decides first each season.</summary>
    public StrategicIntent Intent { get; internal set; } = null!;
    public AiResearchPort Research { get; internal set; } = null!;
    public AiOrders Orders { get; internal set; } = null!;
    public ResearchCatalog Catalog { get; internal set; } = null!;
    public AiLog Log { get; internal set; } = null!;
    /// <summary>A random stream for this civilization, part and season, never shared (docs/MVP/15 rule D3).</summary>
    public Pcg32 Stream(ulong part, ulong entity = 0) => Pcg32.ForStream(MatchSeed, part ^ (ulong)Civ * 0x9E3779B97F4A7C15UL, entity, Season);
    public void Note(string layer, string kind, int subject, int other, Numerics.Fix64 score, string reason)
        => Log.Add(new AiDecision(Tick, Season, Civ, layer, kind, subject, other, score, reason));
}

/// <summary>Turns observations into the belief state (docs/MVP/14 §3.1, §5). The only AI part allowed to read true state, and only through sight.</summary>
public interface IBeliefBuilder : IAiMind
{
    void Update(Belief belief, TrueWorld world, DifficultyProfile profile);
}

/// <summary>The AI Spark: chooses research slots, queue and Leaning (docs/MVP/14 §4).</summary>
public interface IResearchSpark : IAiMind
{
    void Decide(AiContext context);
}

/// <summary>
/// The Strategist: grand plan, raids, war, peace, alliances, gifts, Posture, Beacons (docs/MVP/14 §3.3, §6, §7).
/// <see cref="ActsOnPolitics"/> says whether it replaces the v0 rules for its civilization.
/// </summary>
public interface IStrategist : IAiMind
{
    bool ActsOnPolitics { get; }
    void Decide(AiContext context);
}
