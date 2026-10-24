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
}
