using System;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// The scripted opponents of docs/MVP/14 §10.2, built on the same three interfaces as the smart AI. They use the
/// real belief builder (so a Rusher aims at what it has seen, like a player would) and the same orders.
/// </summary>
internal static class BotFactory
{
    public static (IBeliefBuilder, IResearchSpark, IStrategist) Create(BotKind kind, ResearchCatalog catalog)
    {
        var builder = new BeliefBuilder();
        switch (kind)
        {
            case BotKind.OpeningGrower: return (builder, ScriptedSpark.Opening(catalog, "grower"), new PassiveStrategist());
            case BotKind.OpeningBuilder: return (builder, ScriptedSpark.Opening(catalog, "builder"), new PassiveStrategist());
            case BotKind.OpeningWanderer: return (builder, ScriptedSpark.Opening(catalog, "wanderer"), new PassiveStrategist());
            case BotKind.Rusher: return (builder, ScriptedSpark.Prefer(catalog, "WAR", "HNT"), new RusherStrategist(catalog));
            case BotKind.Turtle: return (builder, ScriptedSpark.Prefer(catalog, "DEF", "BLD"), new TurtleStrategist());
            default: return (builder, ScriptedSpark.RandomPicks(), new PassiveStrategist());
        }
    }
}
