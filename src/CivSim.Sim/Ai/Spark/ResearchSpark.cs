using System.IO;
using CivSim.Sim.Core;

namespace CivSim.Sim.Ai;

/// <summary>
/// PLACEHOLDER (replaced by the forecast and planner work): does nothing, so the Council fills any slot the AI leaves
/// empty. The real Spark values projects with the forecast model and beam-searches plans (docs/MVP/14 §4).
/// </summary>
public sealed class ResearchSpark : IResearchSpark
{
    public ResearchSpark(AiSpec spec, DifficultyProfile profile) { }
    public void Decide(AiContext context) { }
    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}
