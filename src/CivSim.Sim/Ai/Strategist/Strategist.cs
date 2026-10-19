using System.IO;
using CivSim.Sim.Core;

namespace CivSim.Sim.Ai;

/// <summary>
/// PLACEHOLDER (replaced by the Strategist work): does nothing and leaves the civilization on the v0 rules.
/// The real Strategist holds a grand plan and decides raids, wars, peace, alliances, Posture and Beacons by expected
/// value on its beliefs (docs/MVP/14 §3.3, §6, §7).
/// </summary>
public sealed class Strategist : IStrategist
{
    public Strategist(AiSpec spec, DifficultyProfile profile) { }
    public bool ActsOnPolitics => false;
    public void Decide(AiContext context) { }
    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}
