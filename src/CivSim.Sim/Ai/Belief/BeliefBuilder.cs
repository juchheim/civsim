using System.IO;
using CivSim.Sim.Core;

namespace CivSim.Sim.Ai;

/// <summary>
/// PLACEHOLDER (replaced by the belief-state work): fills only what both sides know for certain, the diplomacy.
/// The real builder turns sightings into beliefs (docs/MVP/14 §3.1, §5).
/// </summary>
public sealed class BeliefBuilder : IBeliefBuilder
{
    public void Update(Belief belief, TrueWorld world, DifficultyProfile profile)
    {
        belief.Season = world.Politics.Season;
        for (int o = 0; o < belief.Rivals.Length; o++)
        {
            if (o == belief.Civ) continue;
            RivalBelief r = belief.Rivals[o];
            r.Aware = world.Politics.Diplomacy.Aware(belief.Civ, o);
            r.Contacted = world.Politics.Diplomacy.Contacted(belief.Civ, o);
            r.Relations = world.Politics.Diplomacy.Relations(belief.Civ, o);
            r.Reputation = world.Politics.Diplomacy.Reputation(o);
            r.AtWar = world.Politics.Diplomacy.AtWar(belief.Civ, o);
            r.Allied = world.Politics.Diplomacy.Allied(belief.Civ, o);
            r.Era = world.Research.Era(o);
        }
    }

    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}
