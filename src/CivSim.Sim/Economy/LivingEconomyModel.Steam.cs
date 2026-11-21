using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The atmospheric engine (NRG-06; docs/MVP/38 §5.1, M13 WP-E6-5 slice 2): a staffed engine that burns coal for power units in the E5 power ledger and drains
/// flooded coal seams. Everything here is inert unless the match plays the Discovery chapter (<see cref="DiscoveryChain"/>); the engine's state lives on the
/// settlement and is hashed and snapshotted only in a Discovery match, so Bronze, Iron and Fief bytes and hashes are unchanged. Numbers are the draft values of
/// <c>e6_power_oracle.py</c> (10 units on 1 L coal per unit-season, two engineers) and are provisional.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>The most engines one town may build.</summary>
    private const int MaxEnginesPerTown = 2;

    /// <summary>A town this small cannot spare the engineers (and keeps at least four other adults at home, see <c>StaffRoles</c>).</summary>
    private const int EngineMinPopulation = 80;

    /// <summary>Power units one fired engine delivers in a season. One unit is one power hour, the ledger's unit (water mill 5, windmill 4 at 0.8).</summary>
    private const int EngineUnitsPerEngine = 10;

    /// <summary>Litres of coal burned for each power unit delivered.</summary>
    private static readonly Fix64 EngineCoalPerUnit = Fix64.One;

    /// <summary>Adults taken off other work for each engine (registry role <c>engineer</c>).</summary>
    private const int EngineersPerEngine = 2;

    /// <summary>Power units a fired engine spends draining its town's flooded coal seams before the rest goes to the ledger (provisional; the oracle leaves the pumping draw open).</summary>
    private const int EnginePumpUnits = 2;

    /// <summary>A coal seam at this depth tier or deeper floods without a pumping engine.</summary>
    private const int FloodDepth = 3;

    /// <summary>A flooded seam yields this share of a dry one while nothing pumps it (<c>FLOODED_FACTOR</c> in the oracle).</summary>
    private static readonly Fix64 FloodedSeamYield = Fix64.Half;

    private const uint SteamSnapshotMagic = 0x4D41_4554; // "TEAM" little-endian
    private const ulong SteamTag = 0x5354_4541_4D45_4E47; // "STEAMENG"

    private int EngineCount(SettlementState s) => DiscoveryChain ? IronWorksCount(s, IronStructure.AtmosphericEngine) : 0;

    /// <summary>Engineers a town should hold: two per engine once NRG-06 is learned, and only while coal can be had (an engine with no coal idles and takes nobody off other work).</summary>
    private int EngineersWanted(SettlementState s)
        => DiscoveryChain && Done(s.Civilization, "NRG-06") && CoalObtainable(s) ? EngineCount(s) * EngineersPerEngine : 0;

    private int EngineersHeld(SettlementState s)
    {
        int n = 0;
        foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Engineer) n++;
        return n;
    }

    /// <summary>Power hours the town's engines put into the shared ledger this season, after pumping (zero outside Discovery).</summary>
    private Fix64 SteamPowerHours(SettlementState s) => DiscoveryChain ? s.EngineUnits : Fix64.Zero;

    private bool HasFloodedCoalCamp(SettlementState s)
    {
        foreach (MiningCampState camp in _camps)
        {
            if (camp.Parent != s.Id) continue;
            WorldDeposit d = _world.Deposit(camp.Tile);
            if (d.Kind == DepositKind.Coal && d.Depth >= FloodDepth) return true;
        }
        return false;
    }

    /// <summary>Yield factor of a coal seam: one while it is dry or pumped, the flooded share while a deep seam has no pumping engine.</summary>
    private Fix64 FloodedSeamFactor(SettlementState s, int tile)
        => !s.EnginePumping && _world.Deposit(tile).Depth >= FloodDepth ? FloodedSeamYield : Fix64.One;

    /// <summary>
    /// The first engine is wanted where a deep coal seam floods or where the town's power budget falls short of its work; a second only where the shortfall
    /// remains in a larger town. (The hard gate, with project, size and coal supply, is in <c>IronGate</c>.)
    /// </summary>
    private bool EngineWanted(SettlementState s)
    {
        int engines = EngineCount(s);
        MillPowerAllocation p = MillPower(s);
        bool shortfall = p.GrindingDemand + p.SawingDemand + p.CraftDemand > p.Available;
        if (engines == 0) return HasFloodedCoalCamp(s) || (shortfall && Population(s.Id) >= 150);
        return shortfall && Population(s.Id) >= 300;
    }

    /// <summary>
    /// Season step, run once per town after the season's roles are staffed and before its camps work: every staffed engine burns coal for its units, the engines
    /// drain the town's flooded seams first (a fixed draw), and what remains is the power the ledger sees. No coal or no engineer leaves the engine idle.
    /// </summary>
    private void BurnEngines(SettlementState s)
    {
        int engines = EngineCount(s);
        if (engines == 0 && s.EngineUnits == Fix64.Zero && !s.EnginePumping) return;
        Fix64 units = Fix64.Zero;
        bool pumping = false;
        if (engines > 0 && Done(s.Civilization, "NRG-06"))
        {
            int staffed = Math.Min(engines, EngineersHeld(s) / EngineersPerEngine);
            int slot = DiscoverySlot(DiscoveryGood.Coal);
            Fix64 burn = Fix64.Min(Fix64.FromInt(staffed * EngineUnitsPerEngine) * EngineCoalPerUnit, Stock(s, slot));
            if (burn > Fix64.Zero)
            {
                AddStock(s, slot, -burn);
                units = burn / EngineCoalPerUnit;
                if (units >= Fix64.FromInt(EnginePumpUnits) && HasFloodedCoalCamp(s))
                {
                    pumping = true;
                    units -= Fix64.FromInt(EnginePumpUnits);
                }
            }
        }
        s.EngineUnits = units;
        s.EnginePumping = pumping;
    }

    /// <summary>The engines' and the hearths' coal is a terminal want like any input: two seasons on hand, so the planner opens a camp for it.</summary>
    private void EngineCoalWants(SettlementState s, MetalPlan plan)
    {
        int engines = EngineCount(s);
        int slot = DiscoverySlot(DiscoveryGood.Coal);
        Fix64 target = Fix64.Zero;
        if (engines > 0 && Done(s.Civilization, "NRG-06")) target += Fix64.FromInt(engines * EngineUnitsPerEngine * 2) * EngineCoalPerUnit;
        target += CoalHeatNeed(s, winter: true) * 2;                // FIR-11 coal firing: two winter seasons of heat coal on hand
        Fix64 have = Stock(s, slot);
        if (target > have) plan.Terminal[slot] += target - have;
    }

    // ---- snapshot and hash ----------------------------------------------------------------------------

    private static bool HasSteamState(SettlementState s) => s.EngineUnits != Fix64.Zero || s.EnginePumping;

    private void HashSteam(ref StateHasher hash)
    {
        if (!DiscoveryChain) return;
        bool any = false;
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!HasSteamState(_settlements[i])) continue;
            if (!any) { hash.Add(SteamTag); any = true; }
            hash.Add(i);
            hash.Add(_settlements[i].EngineUnits);
            hash.Add(_settlements[i].EnginePumping ? 1 : 0);
        }
    }

    private void WriteSteam(BinaryWriter writer)
    {
        if (!DiscoveryChain) return;
        writer.Write(SteamSnapshotMagic);
        int count = 0;
        for (int i = 0; i < _settlements.Count; i++) if (HasSteamState(_settlements[i])) count++;
        writer.Write(count);
        for (int i = 0; i < _settlements.Count; i++)
        {
            if (!HasSteamState(_settlements[i])) continue;
            writer.Write(i);
            writer.Write(_settlements[i].EngineUnits.Raw);
            writer.Write(_settlements[i].EnginePumping);
        }
    }

    private void ReadSteam(BinaryReader reader)
    {
        if (!DiscoveryChain) return;
        if (reader.ReadUInt32() != SteamSnapshotMagic) throw new InvalidDataException("The snapshot has no Discovery steam section.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _settlements.Count) throw new InvalidDataException("Invalid Discovery steam settlement count.");
        int previous = -1;
        for (int n = 0; n < count; n++)
        {
            int index = reader.ReadInt32();
            if (index <= previous || index >= _settlements.Count) throw new InvalidDataException("Invalid Discovery steam settlement index.");
            previous = index;
            Fix64 units = Fix64.FromRaw(reader.ReadInt64());
            if (units < Fix64.Zero || units > Fix64.FromInt(EngineUnitsPerEngine * MaxEnginesPerTown)) throw new InvalidDataException("Invalid Discovery engine output.");
            _settlements[index].EngineUnits = units;
            _settlements[index].EnginePumping = reader.ReadBoolean();
        }
    }

    // ---- readouts and fixtures (tests and views) ------------------------------------------------------

    /// <summary>A town's engines as the cards show them: built, staffed, the units they delivered last season, whether they pumped, and the coal one full season burns.</summary>
    internal (int Engines, int Staffed, Fix64 Units, bool Pumping, Fix64 CoalPerSeason) SteamReadout(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return (0, 0, Fix64.Zero, false, Fix64.Zero);
        SettlementState s = _settlements[settlementIndex];
        int engines = EngineCount(s);
        int staffed = Math.Min(engines, EngineersHeld(s) / EngineersPerEngine);
        return (engines, staffed, s.EngineUnits, s.EnginePumping, Fix64.FromInt(staffed * EngineUnitsPerEngine) * EngineCoalPerUnit);
    }

    /// <summary>Tests only: give a settlement's adults the engineer role, then run the engine burn once.</summary>
    internal void FixtureStaffAndBurnEngines(int settlementIndex, int engineers)
    {
        SettlementState s = _settlements[settlementIndex];
        int given = 0;
        foreach (FigureState f in Residents(s.Id))
        {
            if (given >= engineers) break;
            if (f.Age is < 14 or >= 60 || f.Camp >= 0 || f.Role != Role.None) continue;
            f.Role = Role.Engineer;
            given++;
        }
        BurnEngines(s);
    }

    /// <summary>Tests only: the yield factor of a coal seam for a town this season.</summary>
    internal Fix64 FixtureFloodedSeamFactor(int settlementIndex, int tile) => FloodedSeamFactor(_settlements[settlementIndex], tile);

    internal bool FixtureEngineWanted(int settlementIndex) => EngineWanted(_settlements[settlementIndex]);
}
