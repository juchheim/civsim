using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>
/// What anyone standing near a settlement can see of it: the buildings that stand there (docs/MVP/13 §2.5). Read by
/// the AI belief builder only for settlements its figures can see (docs/MVP/14 §3.1).
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>How many kinds of structure the economy builds.</summary>
    public static int StructureKindCount => (int)Structure.Count;

    /// <summary>The catalog's structure id of a kind (the <c>structure</c> of a project's <c>build</c> effect), "" if none.</summary>
    public static string StructureName(int kind) => StructureId((Structure)kind);

    /// <summary>Find a structure kind by its registry id, or −1 when the economy does not implement it.</summary>
    public static int StructureIndex(string id)
    {
        for (int i = 0; i < (int)Structure.Count; i++)
            if (StructureId((Structure)i) == id) return i;
        return -1;
    }

    /// <summary>How many structures of a kind stand at a settlement.</summary>
    public int StructureCount(int settlementIndex, int kind) => _settlements[settlementIndex].Structures[kind];

    /// <summary>How many Halls (a Bronze-age civic dwelling, housing 30) stand at a settlement.</summary>
    public int HallCount(int settlementIndex) => _settlements[settlementIndex].Civic?.Halls ?? 0;

    /// <summary>Ziggurats (the BLD-08 Wonder) standing at a settlement, and how much of the next one is raised, in permille (0 when none is being built).</summary>
    public (int Standing, int BuildPermille) ZigguratView(int settlementIndex)
    {
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { } c) return (0, 0);
        if (c.BuildKind != CivicBuild.Ziggurat || c.BuildProgress <= Fix64.Zero) return (c.Ziggurats, 0);
        Fix64 labor = CivicLabor(s, CivicBuild.Ziggurat);
        Fix64 share = labor <= Fix64.Zero ? Fix64.One : Fix64.Min(Fix64.One, c.BuildProgress / labor);
        return (c.Ziggurats, (share * 1000).FloorToInt());
    }
}
