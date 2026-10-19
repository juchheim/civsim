using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// The true state of the whole match. Only <see cref="IBeliefBuilder"/> receives it: the AI's observations are what
/// the builder lets through, so the fog-fairness test (docs/MVP/14 §10.4) has one place to look.
/// </summary>
public sealed class TrueWorld
{
    internal TrueWorld(LivingEconomyModel economy, ResearchSystem research, PoliticsSystem politics, GeneratedWorld generated)
    {
        Economy = economy;
        Research = research;
        Politics = politics;
        Generated = generated;
    }

    public LivingEconomyModel Economy { get; }
    public ResearchSystem Research { get; }
    public PoliticsSystem Politics { get; }
    public GeneratedWorld Generated { get; }
}
