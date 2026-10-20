namespace CivSim.Sim.Ai;

/// <summary>
/// A Spark that reacts between decisions, after each research step (docs/MVP/14 §9.2 "re-plan each season + events").
/// It acts only through the port, with the commands a player has.
/// </summary>
public interface IResearchStepListener
{
    void OnResearchStep(AiContext context);
}
