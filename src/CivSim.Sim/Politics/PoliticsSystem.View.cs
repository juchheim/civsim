using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>Read-only accessors for the view layer (docs/MVP/18): nothing here changes state.</summary>
public sealed partial class PoliticsSystem
{
    /// <summary>Seasonal camps placed so far (WAY-02, docs/MVP/08 §4.1), in placement order.</summary>
    public int CampCount => _camps.Count;

    /// <summary>A camp's tile, the settlement whose band uses it, and its civilization.</summary>
    public (int Tile, int Settlement, int Civ) Camp(int index) => _camps[index];

    /// <summary>The generated world the match is played on (terrain, starts, herds and predators as generated).</summary>
    public GeneratedWorld World => _generated;
}
