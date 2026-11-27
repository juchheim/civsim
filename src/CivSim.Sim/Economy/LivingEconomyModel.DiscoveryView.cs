using System;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>Whether a Discovery work is doing its job (docs/MVP/38 D-T8, docs/MVP/39 §8.1): none built, working, queued (a site the town has opened), dormant (nobody staffs it) or blocked (staffed but starved or cut off).</summary>
public enum DiscoveryWorkState : byte { None = 0, Working = 1, Queued = 2, Dormant = 3, Blocked = 4 }

/// <summary>Why a work is dormant or blocked, for the hover line and the mark's glyph.</summary>
public enum DiscoveryWorkReason : byte { None = 0, NoEngineer, NoCoal, NoMiners, Flooded, NoOperatives, NoPower, NoBotanist, NoScholar, NoBroker, NoCrew, NoHarbor }

/// <summary>One work's state and reason.</summary>
public readonly record struct DiscoveryWork(DiscoveryWorkState State, DiscoveryWorkReason Reason);

/// <summary>The states of a town's Discovery works. The mine is the town's coal pit; <see cref="Flooded"/> is a deep seam with no pumping engine, <see cref="Pumping"/> an engine draining one.</summary>
public readonly record struct DiscoveryWorks(
    DiscoveryWork Mine, DiscoveryWork Engine, DiscoveryWork Oven, DiscoveryWork Factory, DiscoveryWork Garden, DiscoveryWork Society, DiscoveryWork Exchange,
    DiscoveryWork Galleon, bool Flooded, bool Pumping);

/// <summary>A read-only snapshot of a Discovery town for the view: what stands, what works and what it gives (raw Q32.32 where a quantity).</summary>
public readonly record struct DiscoveryTownReadout(
    long CoalLitres, int Engines, int Ovens, bool Canal, bool Factory, long FactoryCoverage, int Operatives, int Engineers, long Soot,
    bool Colony, bool GardenStaffed, bool SocietyHere, bool Bastion, bool GalleonCrewed, bool ExchangeHere, bool Fleet = false, DiscoveryWorks Works = default);

public sealed partial class LivingEconomyModel
{
    /// <summary>
    /// The Discovery readout of a town (docs/MVP/38 §8; M13 WP-E6-11): pure reads of existing state, nothing stored, empty outside a Discovery match.
    /// The view frame calls it for the viewer's own towns only.
    /// </summary>
    public DiscoveryTownReadout DiscoveryTown(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return default;
        SettlementState s = _settlements[settlementIndex];
        return new DiscoveryTownReadout(
            CoalStock(settlementIndex).Raw, EngineCount(s), IronWorksCount(s, IronStructure.CokeOven), IronWorksCount(s, IronStructure.NavigationCanal) > 0,
            FactoryCount(s) > 0, FactoryCoverage(s).Raw, OperativesHeld(s), EngineersHeld(s), SootAt(s.SiteTile).Raw,
            IsColony(s), GardenStaffed(s), IronWorksCount(s, IronStructure.LearnedSociety) > 0, BastionFortBuilt(settlementIndex),
            CrewedGalleon(settlementIndex), IronWorksCount(s, IronStructure.ExchangeHouse) > 0, OffshoreFleet(s), WorksOf(s, settlementIndex));
    }

    /// <summary>The site tile of a civilization's capital (where its colonies' tethers lead), or -1 with none; Discovery only.</summary>
    public int CapitalTile(int civ)
    {
        if (!DiscoveryChain || civ < 0) return -1;
        int capital = CapitalOf(civ);
        return capital < 0 ? -1 : _settlements[capital].SiteTile;
    }

    private static DiscoveryWork W(DiscoveryWorkState state, DiscoveryWorkReason reason = DiscoveryWorkReason.None) => new(state, reason);

    /// <summary>
    /// Working, queued, dormant or blocked for each Discovery work of a town (D-T8), derived from the state the sim already keeps: nothing is stored. A work is
    /// <see cref="DiscoveryWorkState.None"/> where the town has none. "Queued" is a coal pit the town has opened and not yet dug.
    /// </summary>
    private DiscoveryWorks WorksOf(SettlementState s, int index)
    {
        // the coal pit: the town's first coal camp (a deep seam floods until an engine pumps it)
        DiscoveryWork mine = default; bool flooded = false, pumping = false;
        foreach (MiningCampState camp in _camps)
        {
            if (camp.Parent != s.Id) continue;
            WorldDeposit d = _world.Deposit(camp.Tile);
            if (d.Kind != DepositKind.Coal) continue;
            bool deep = d.Depth >= FloodDepth;
            if (!camp.Built) { mine = W(DiscoveryWorkState.Queued); break; }
            flooded = deep && !s.EnginePumping; pumping = deep && s.EnginePumping;
            if (flooded) mine = W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.Flooded);
            else
            {
                int crew = 0;
                foreach (FigureState f in Residents(s.Id)) if (f.Camp == camp.Id) crew++;
                mine = crew == 0 ? W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoMiners) : W(DiscoveryWorkState.Working);
            }
            break;
        }

        DiscoveryWork engine = default;
        int engines = EngineCount(s);
        if (engines > 0)
        {
            int staffed = Math.Min(engines, EngineersHeld(s) / EngineersPerEngine);
            engine = staffed == 0 ? W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoEngineer)
                : s.EngineUnits > Fix64.Zero || s.EnginePumping ? W(DiscoveryWorkState.Working) : W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCoal);
        }

        DiscoveryWork oven = default;
        if (IronWorksCount(s, IronStructure.CokeOven) > 0)
            oven = DiscoveryStock(s, DiscoveryGood.Coal) > Fix64.Zero ? W(DiscoveryWorkState.Working) : W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCoal);

        DiscoveryWork factory = default;
        if (FactoryCount(s) > 0)
            factory = OperativesHeld(s) == 0 ? W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoOperatives)
                : FactoryCoverage(s) <= Fix64.Zero ? W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoPower) : W(DiscoveryWorkState.Working);

        DiscoveryWork garden = default;
        if (IronWorksCount(s, IronStructure.BotanicalGarden) > 0)
            garden = GardenStaffed(s) ? W(DiscoveryWorkState.Working) : W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoBotanist);

        DiscoveryWork society = default;
        if (IronWorksCount(s, IronStructure.LearnedSociety) > 0)
        {
            bool scholar = false;
            foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Scholar) { scholar = true; break; }
            society = scholar && Done(s.Civilization, "LRN-09") ? W(DiscoveryWorkState.Working) : W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoScholar);
        }

        bool harbor = IronWorksCount(s, IronStructure.Harbor) > 0;
        DiscoveryWork exchange = default;
        if (IronWorksCount(s, IronStructure.ExchangeHouse) > 0)
            exchange = !harbor ? W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoHarbor)
                : BrokersHeld(s) < BrokersPerExchange || !Done(s.Civilization, "TRD-11") ? W(DiscoveryWorkState.Dormant, DiscoveryWorkReason.NoBroker) : W(DiscoveryWorkState.Working);

        DiscoveryWork galleon = default;
        if (IronWorksCount(s, IronStructure.Galleon) > 0)
            galleon = !harbor ? W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoHarbor)
                : CrewedGalleon(index) ? W(DiscoveryWorkState.Working) : W(DiscoveryWorkState.Blocked, DiscoveryWorkReason.NoCrew);

        return new DiscoveryWorks(mine, engine, oven, factory, garden, society, exchange, galleon, flooded, pumping);
    }

    /// <summary>
    /// Whether this settlement is a former overseas colony of <paramref name="civ"/> that has gone independent (TER-11, docs/MVP/38 §5.2): it seceded, belonged to
    /// that civilization, and lies across water from its capital. Derived from the secession flag and the colony definition; Discovery only.
    /// </summary>
    public bool IsBreakawayColony(int settlementIndex, int civ)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return false;
        SettlementState s = _settlements[settlementIndex];
        if (s.Civic is not { Seceded: true } || s.Civilization != civ || !Done(civ, "TER-11")) return false;
        int capital = CapitalOf(civ);
        if (capital < 0 || capital == s.Id) return false;
        int site = _settlements[capital].SiteTile;
        int[] cost = _walkFrom[site] ??= WalkingCosts(site);
        return cost[s.SiteTile] == int.MaxValue;
    }

    /// <summary>
    /// The deep coal pits of a civilization's towns that are worked (docs/MVP/39 §4.1): the tile of each, and 1 where the seam is flooded (no engine pumps it) or 2 where
    /// a fired engine is draining it. Fills at most <paramref name="tiles"/>.Length entries and returns how many; empty outside Discovery. Reads state only.
    /// </summary>
    public int DeepCoalPits(int civ, int[] tiles, byte[] states)
    {
        int n = 0;
        if (!DiscoveryChain) return 0;
        foreach (MiningCampState camp in _camps)
        {
            if (n >= tiles.Length || !camp.Built) continue;
            SettlementState s = _settlements[camp.Parent];
            if (s.Civilization != civ) continue;
            WorldDeposit d = _world.Deposit(camp.Tile);
            if (d.Kind != DepositKind.Coal || d.Depth < FloodDepth) continue;
            tiles[n] = camp.Tile; states[n++] = s.EnginePumping ? (byte)2 : (byte)1;
        }
        return n;
    }

    /// <summary>
    /// Which E6 goods a town holds a stock of, as a mask of <c>DiscoveryFrame.Cargo*</c> bits (docs/MVP/39 §7.4): coal, coke, plate glass, precision parts, acid, alkali,
    /// pamphlets, a chronometer or telescope, sugar and salt cod. A bit is set only for a stock above zero, so a crate is never drawn for a good the town lacks.
    /// </summary>
    public int CargoMask(int settlementIndex)
    {
        if (!DiscoveryChain || (uint)settlementIndex >= (uint)_settlements.Count) return 0;
        SettlementState s = _settlements[settlementIndex];
        int mask = 0;
        if (DiscoveryStock(s, DiscoveryGood.Coal) > Fix64.Zero) mask |= 1;
        if (DiscoveryStock(s, DiscoveryGood.Coke) > Fix64.Zero) mask |= 2;
        if (DiscoveryStock(s, DiscoveryGood.PlateGlass) > Fix64.Zero) mask |= 4;
        if (DiscoveryStock(s, DiscoveryGood.Parts) > Fix64.Zero) mask |= 8;
        if (DiscoveryStock(s, DiscoveryGood.Acid) > Fix64.Zero) mask |= 16;
        if (DiscoveryStock(s, DiscoveryGood.Alkali) > Fix64.Zero) mask |= 32;
        if (FiefStockPublic(settlementIndex, "G-PAMPHLET") > Fix64.Zero) mask |= 64;
        if (DiscoveryStock(s, DiscoveryGood.Chronometer) > Fix64.Zero || DiscoveryStock(s, DiscoveryGood.Telescope) > Fix64.Zero) mask |= 128;
        if (FiefStockPublic(settlementIndex, "G-SUGAR") > Fix64.Zero) mask |= 256;
        if (FiefStockPublic(settlementIndex, "G-COD") > Fix64.Zero) mask |= 512;
        return mask;
    }
}
