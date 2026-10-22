using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// One civilization's own research, as the AI Spark sees and steers it (docs/MVP/14 §4). It can read only its own
/// civilization and change it only with the commands a player has: slots, queue, Leaning.
/// </summary>
public sealed class AiResearchPort
{
    private readonly ResearchSystem _research;
    private readonly int _civ;

    internal AiResearchPort(ResearchSystem research, int civ)
    {
        _research = research;
        _civ = civ;
    }

    public ResearchCatalog Catalog => _research.Catalog;
    public int Era => _research.Era(_civ);
    public bool IsDone(int project) => _research.IsDone(_civ, project);
    public ProjectState State(int project) => _research.State(_civ, project);
    public Fix64 Progress(int project) => _research.Progress(_civ, project);
    public Fix64 EffectiveCost(int project) => _research.EffectiveCost(_civ, project);
    public int Slot(int slot) => _research.Slot(_civ, slot);
    public System.Collections.Generic.IReadOnlyList<int> Queue => _research.Queue(_civ);
    public int DoneInEra(int era) => _research.DoneInEra(_civ, era);
    public Fix64 KnowHow(int lineage) => _research.KnowHow(_civ, lineage);
    public Fix64 KnowHowApplied(int project) => _research.KnowHowApplied(_civ, project);
    public Fix64 InsightPerYear => _research.InsightPerYear(_civ);
    public Fix64 CommonsPerYear => _research.CommonsPerYear(_civ);
    public int Leaning(int theme) => _research.Leaning(_civ, theme);
    public ulong SeenMask => _research.SeenMask(_civ);
    /// <summary>Public record: the first civilization to complete a project (docs/MVP/02 §14); -1 if none yet.</summary>
    public int FirstBy(int project) => _research.FirstBy(project);
    public Fix64 AdoptionFraction(int project) => _research.AdoptionFraction(_civ, project);
    public Fix64 ModifierMultiplier(string stat) => _research.ModifierMultiplier(_civ, stat);
    public Fix64 HazardMultiplier(string cause) => _research.HazardMultiplier(_civ, cause);

    /// <summary>The Council's score for every project: the prior the search starts from (14 §4.2).</summary>
    public void CouncilPrior(Span<Fix64> scores) => _research.CouncilPrior(_civ, scores);

    public void SetSlot(int slot, int project) => Order(CommandKind.SetSlot, slot, project);
    public void ClearSlot(int slot) => Order(CommandKind.SetSlot, slot, -1);
    public void Enqueue(int project) => Order(CommandKind.QueueProject, project, 0);
    public void Dequeue(int index) => Order(CommandKind.DequeueProject, index, 0);
    public void SetLeaning(int theme, int percent) => Order(CommandKind.SetLeaning, theme, percent);

    /// <summary>Empties the queue (the AI re-plans from scratch each time).</summary>
    public void ClearQueue()
    {
        while (_research.Queue(_civ).Count > 0) Dequeue(0);
    }

    private void Order(CommandKind kind, int a, int b) => _research.ApplyOrder(new Command(_research.Tick, (byte)_civ, kind, a, b, 0));
}

/// <summary>
/// One civilization's orders to its people: gifts, barter, raids, assaults, war, peace, alliances, Posture and Beacons.
/// Every order keeps the hard rules of docs/MVP/13 (projects known, contact, travel, food to spare) and returns whether
/// it was carried out; deciding whether it is wise is the Strategist's job.
/// </summary>
public sealed class AiOrders
{
    private readonly PoliticsSystem _politics;
    private readonly int _civ;

    internal AiOrders(PoliticsSystem politics, int civ)
    {
        _politics = politics;
        _civ = civ;
    }

    public bool Gift(int toCiv) => _politics.OrderGift(_civ, toCiv);
    public bool Gift(int toCiv, int targetSettlement) => _politics.OrderGift(_civ, toCiv, targetSettlement);
    public bool Barter(int withCiv) => _politics.OrderBarter(_civ, withCiv);
    public bool Barter(int withCiv, int targetSettlement) => _politics.OrderBarter(_civ, withCiv, targetSettlement);
    public bool Raid(int targetSettlement, int partySize) => _politics.OrderRaid(_civ, targetSettlement, partySize);
    public bool Assault(int targetSettlement) => _politics.OrderAssault(_civ, targetSettlement);
    public bool DeclareWar(int civ) => _politics.OrderWar(_civ, civ);
    public bool MakePeace(int civ) => _politics.OrderPeace(_civ, civ);
    public bool Ally(int civ) => _politics.OrderAlliance(_civ, civ);
    public bool SetPosture(Posture posture) => _politics.OrderPosture(_civ, posture);
    public bool PlaceBeacon(int tile) => _politics.OrderBeacon(_civ, tile);
    public void RemoveBeacon(int index) => _politics.OrderRemoveBeacon(_civ, index);
}
