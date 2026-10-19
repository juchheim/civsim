using System;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>PLACEHOLDER: the scripted opponents arrive with the harness work. Until then every bot is the plain belief builder with a passive Spark and Strategist.</summary>
internal static class BotFactory
{
    public static (IBeliefBuilder, IResearchSpark, IStrategist) Create(BotKind kind, ResearchCatalog catalog)
        => (new BeliefBuilder(), new ResearchSpark(new AiSpec(Difficulty.Warlord, Economy.EconomyArchetype.Balanced), DifficultyProfile.From(catalog.Pack, Difficulty.Warlord)), new Strategist(new AiSpec(Difficulty.Warlord, Economy.EconomyArchetype.Balanced), DifficultyProfile.From(catalog.Pack, Difficulty.Warlord)));
}
