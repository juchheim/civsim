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

    /// <summary>How many structures of a kind stand at a settlement.</summary>
    public int StructureCount(int settlementIndex, int kind) => _settlements[settlementIndex].Structures[kind];
}
