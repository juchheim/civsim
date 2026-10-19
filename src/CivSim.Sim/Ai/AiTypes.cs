using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>Difficulty changes how well the AI thinks, never what it gets (docs/MVP/14 §9.2).</summary>
public enum Difficulty : byte { Settler, Chieftain, Warlord, Legend }

public enum OpponentModel : byte { None, ArchetypePosterior, Full, FullCoalition }

public enum ReplanCadence : byte { Yearly, Season, SeasonAndEvents }

/// <summary>A level's thinking budget, read from the difficulty registry. Counted in fixed iterations, never in wall-clock time (14 §12).</summary>
public sealed class DifficultyProfile
{
    public Difficulty Level { get; init; }
    /// <summary>How many eras of research the planner looks ahead.</summary>
    public int HorizonEras { get; init; }
    /// <summary>How many partial research plans the beam search keeps.</summary>
    public int BeamWidth { get; init; }
    /// <summary>Percent of project picks taken from the Council's prior instead of the search (Settler 20, Chieftain 5).</summary>
    public int CouncilPriorPercent { get; init; }
    public OpponentModel Opponent { get; init; }
    public ReplanCadence Replan { get; init; }
    /// <summary>Seasons late the AI reacts to a threat (the Settler's 2); zero elsewhere.</summary>
    public int ReactionDelaySeasons { get; init; }

    public static DifficultyProfile From(ContentPack pack, Difficulty level)
    {
        string id = level.ToString().ToLowerInvariant();
        foreach (ContentValue d in pack.Root.Get("registries")!.Get("difficulty")!.Items)
        {
            if (d.GetString("id") != id) continue;
            return new DifficultyProfile
            {
                Level = level,
                HorizonEras = (int)d.GetInt("horizon_eras")!,
                BeamWidth = (int)d.GetInt("beam_width")!,
                CouncilPriorPercent = (int)d.GetInt("council_prior_pct")!,
                Opponent = d.GetString("opponent_model") switch
                {
                    "none" => OpponentModel.None,
                    "archetype_posterior" => OpponentModel.ArchetypePosterior,
                    "full_coalition" => OpponentModel.FullCoalition,
                    _ => OpponentModel.Full,
                },
                Replan = d.GetString("replan") switch
                {
                    "yearly" => ReplanCadence.Yearly,
                    "season" => ReplanCadence.Season,
                    _ => ReplanCadence.SeasonAndEvents,
                },
                ReactionDelaySeasons = level == Difficulty.Settler ? 2 : 0,
            };
        }
        throw new ArgumentException($"No difficulty '{id}' in the registry.", nameof(level));
    }
}

/// <summary>An AI civilization's personality (the archetype) and level.</summary>
public readonly record struct AiSpec(Difficulty Level, EconomyArchetype Archetype);

/// <summary>The scripted opponents that stand in for players in the harness (docs/MVP/14 §10.2).</summary>
public enum BotKind : byte { Random, OpeningGrower, OpeningBuilder, OpeningWanderer, Rusher, Turtle, Policy }

/// <summary>
/// A fixed policy for the exploit searcher (docs/MVP/14 §10.2): the lineages to research first (by lineage code), the
/// season Raid posture starts (or -1), the season Fortify starts (or -1), the biggest raid party and whether it gifts.
/// The searcher mutates these looking for a cheap plan that beats the smart AI; anything it finds is a bug to fix.
/// </summary>
public sealed record BotPolicy(string[] Lineages, int RaidFromSeason, int FortifyFromSeason, int PartySize, bool Gifts)
{
    public override string ToString() => $"lineages={string.Join('>', Lineages)} raid@{RaidFromSeason} fortify@{FortifyFromSeason} party={PartySize} gifts={Gifts}";
}

public enum ControlKind : byte
{
    /// <summary>The Council of Elders and the v0 rules: the naive baseline and the player's auto-pick.</summary>
    Council,
    /// <summary>The smart AI: belief state, forecast, planner, Strategist.</summary>
    Ai,
    /// <summary>A scripted bot.</summary>
    Bot,
}

/// <summary>Who runs a civilization.</summary>
public readonly record struct CivControl(ControlKind Kind, AiSpec Spec = default, BotKind Bot = BotKind.Random, BotPolicy? Policy = null)
{
    public static CivControl Council => new(ControlKind.Council);
    public static CivControl Ai(Difficulty level, EconomyArchetype archetype) => new(ControlKind.Ai, new AiSpec(level, archetype));
    public static CivControl Scripted(BotKind bot) => new(ControlKind.Bot, default, bot);
    public static CivControl Fixed(BotPolicy policy) => new(ControlKind.Bot, default, BotKind.Policy, policy);
}

/// <summary>One logged strategic decision with the reasons that decided it (docs/MVP/14 §11).</summary>
public readonly record struct AiDecision(long Tick, int Season, int Civ, string Layer, string Kind, int Subject, int Other, Fix64 Score, string Reason);

/// <summary>The explanation log. Output only: nothing in the simulation reads it back.</summary>
public sealed class AiLog
{
    private readonly List<AiDecision> _entries = new();
    public IReadOnlyList<AiDecision> Entries => _entries;
    public void Add(in AiDecision decision) => _entries.Add(decision);
    public void Clear() => _entries.Clear();
}

/// <summary>The Strategist's grand plans (docs/MVP/14 §3.3). E0 to E2 use the first five.</summary>
public enum GrandPlan : byte { FrontierRace, Prosper, Defend, Conquer, CoalitionAgainstLeader, ApexRace }

/// <summary>
/// What the Strategist tells the Spark (docs/MVP/14 §6.4: "if a rival's forecast bronze or warrior ETA falls before its own
/// palisade's ETA, DEF and WAR projects jump in priority"). Written by the Strategist each season, read by the Spark in the
/// same pass; saved and hashed with the seat.
/// </summary>
public sealed class StrategicIntent
{
    public GrandPlan Plan;
    public int PlanSinceSeason;
    /// <summary>The civilization the plan is about (Conquer X, Coalition against X), -1 if none.</summary>
    public int TargetCiv = -1;
    /// <summary>0 to 1: how soon defense research and walls are needed (the threat belief, 14 §5.4).</summary>
    public Numerics.Fix64 DefenseUrgency;
    /// <summary>0 to 1: how much a coming war wants weapons and warriors researched first.</summary>
    public Numerics.Fix64 AttackReadiness;
    /// <summary>0 to 1: how much open land is still worth racing for, so Kin and Reach projects keep their priority.</summary>
    public Numerics.Fix64 FrontierValue;

    public void Hash(ref Core.StateHasher h)
    {
        h.Add((int)Plan); h.Add(PlanSinceSeason); h.Add(TargetCiv); h.Add(DefenseUrgency); h.Add(AttackReadiness); h.Add(FrontierValue);
    }

    public void Write(System.IO.BinaryWriter w)
    {
        w.Write((byte)Plan); w.Write(PlanSinceSeason); w.Write(TargetCiv);
        w.Write(DefenseUrgency.Raw); w.Write(AttackReadiness.Raw); w.Write(FrontierValue.Raw);
    }

    public void Read(System.IO.BinaryReader r)
    {
        Plan = (GrandPlan)r.ReadByte(); PlanSinceSeason = r.ReadInt32(); TargetCiv = r.ReadInt32();
        DefenseUrgency = Numerics.Fix64.FromRaw(r.ReadInt64()); AttackReadiness = Numerics.Fix64.FromRaw(r.ReadInt64());
        FrontierValue = Numerics.Fix64.FromRaw(r.ReadInt64());
    }
}
