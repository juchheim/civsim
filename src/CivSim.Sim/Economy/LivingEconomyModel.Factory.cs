using System;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Economy;

/// <summary>
/// The Factory (MEC-10, docs/MVP/38 §5.7, M13 WP-E6-8 slice 1): a staffed building that draws power from the town's shared ledger and, as it is powered, cuts the labor of
/// every recipe the town runs, with the first local pollution: soot on its tile and the tiles around it, which wears on the health of the town beside it. Everything
/// here is inert unless the match plays the Discovery chapter (<see cref="DiscoveryChain"/>); the soot lives in one per-tile array that is hashed and snapshotted only
/// in a Discovery match, so Bronze, Iron and Fief bytes and hashes are unchanged. All numbers are provisional.
/// </summary>
public sealed partial class LivingEconomyModel
{
    /// <summary>Operatives a factory needs: adults taken off the fields and other work (the row's urban migration is this draw, shown honestly as people leaving other work).</summary>
    private const int FactoryOperatives = 12;
    /// <summary>A town this small cannot spare a factory's hands (and keeps four other adults, see <c>StaffRoles</c>).</summary>
    private const int FactoryMinPopulation = 120;
    /// <summary>Power hours one operative's machine draws a season, from the shared ledger after grinding, sawing and powered craft.</summary>
    private static readonly Fix64 FactoryPowerPerOperative = Fix64.One;

    private static readonly Fix64 SootAtSite = Fix64.One, SootAtNeighbor = Fix64.Half, SootCap = Fix64.FromInt(10);
    /// <summary>Endemic death hazard of a town: one fiftieth more per point of soot on its own tile, capped at a fifth more (provisional).</summary>
    private static readonly Fix64 SootFeverCap = Fix64.One + Fix64.One / 5;

    private const uint SootSnapshotMagic = 0x544F_4F53; // "SOOT" little-endian
    private const ulong SootTag = 0x534F_4F54_5441_4753; // "SOOTTAGS"

    private Fix64[]? _soot;

    private int FactoryCount(SettlementState s) => DiscoveryChain ? IronWorksCount(s, IronStructure.Factory) : 0;

    /// <summary>Operatives a town should hold: a full crew for a standing factory once MEC-10 is learned and the town has a power source (a factory with none idles and takes nobody).</summary>
    private int OperativesWanted(SettlementState s)
        => DiscoveryChain && FactoryCount(s) > 0 && Done(s.Civilization, "MEC-10") && MillPower(s).Available > Fix64.Zero ? FactoryOperatives : 0;

    private int OperativesHeld(SettlementState s)
    {
        int n = 0;
        foreach (FigureState f in Residents(s.Id)) if (f.Role == Role.Operative) n++;
        return n;
    }

    /// <summary>
    /// How much of the factory's full effect it gets this season, from 0 to 1: the share of its operatives in post times the share of their machines' power hours that the
    /// ledger has left after grinding, sawing and powered craft.
    /// </summary>
    private Fix64 FactoryCoverage(SettlementState s)
    {
        if (FactoryCount(s) == 0 || !Done(s.Civilization, "MEC-10")) return Fix64.Zero;
        Fix64 crew = Fix64.Min(Fix64.One, Fix64.FromInt(OperativesHeld(s)) / FactoryOperatives);
        if (crew <= Fix64.Zero) return Fix64.Zero;
        Fix64 demand = Fix64.FromInt(FactoryOperatives) * FactoryPowerPerOperative;
        Fix64 power = Fix64.Min(Fix64.One, MillPower(s).Unused / demand);
        return crew * power;
    }

    /// <summary>The labor multiplier of the town's recipes: the factory's x2 output per worker, scaled by the power it receives, is <c>1 / (1 + coverage)</c> of the labor.</summary>
    private Fix64 FactoryLaborFactor(SettlementState s)
    {
        if (!DiscoveryChain || FactoryCount(s) == 0) return Fix64.One;
        Fix64 coverage = FactoryCoverage(s);
        return coverage <= Fix64.Zero ? Fix64.One : Fix64.One / (Fix64.One + coverage);
    }

    /// <summary>
    /// FIB-12 (docs/MVP/38 §5.7, M13 WP-E6-8 slice 2): the multi-spindle jenny spins three times as much for the labor, by hand; the flying shuttle weaves up to three times as
    /// much, but only as far as the shared power ledger lends it hours (a hand shuttle gains nothing over the treadle loom, a fully powered one triples it). Applied on top
    /// of the wheel (FIB-11) and the treadle loom (FIB-10). Other recipes are untouched.
    /// </summary>
    private Fix64 ShuttleAndJennyLaborFactor(SettlementState s, string recipeId)
    {
        if (recipeId == "spin_yarn") return Fix64.One / 3;
        if (recipeId == "weave_cloth") return Fix64.One / (Fix64.One + Fix64.FromInt(2) * MillPower(s).Coverage(MillPowerJob.Craft));
        return Fix64.One;
    }

    // ---- soot -------------------------------------------------------------------------------------------

    internal Fix64 SootAt(int tile) => _soot is { } soot && (uint)tile < (uint)soot.Length ? soot[tile] : Fix64.Zero;

    /// <summary>Season step: a working factory (some power and some crew) soots its tile and the tiles beside it, up to a cap.</summary>
    private void EmitSoot(SettlementState s)
    {
        if (!DiscoveryChain || FactoryCoverage(s) <= Fix64.Zero) return;
        _soot ??= new Fix64[_world.TileCount];
        _soot[s.SiteTile] = Fix64.Min(SootCap, _soot[s.SiteTile] + SootAtSite * FactoryCount(s));
        foreach (int tile in RangeTiles(s.SiteTile))
        {
            if (tile == s.SiteTile || HexGrid.Distance(s.SiteTile, tile, _world.Width) != 1) continue;
            _soot[tile] = Fix64.Min(SootCap, _soot[tile] + SootAtNeighbor * FactoryCount(s));
        }
    }

    /// <summary>The soot of the whole map fades by a tenth each season (called from the per-tile season step).</summary>
    private void DecaySoot(int tile)
    {
        if (_soot is not { } soot || soot[tile] == Fix64.Zero) return;
        Fix64 left = soot[tile] - soot[tile] / 10;
        soot[tile] = left < Fix64.FromRatio(1, 100) ? Fix64.Zero : left;
    }

    /// <summary>The endemic death hazard factor of a settlement's people for the soot on their own tile (exactly 1 outside Discovery and in clean air).</summary>
    private Fix64 SootFeverFactor(int settlementId)
    {
        if (!DiscoveryChain || _soot is null || (uint)settlementId >= (uint)_settlements.Count) return Fix64.One;
        Fix64 soot = _soot[_settlements[settlementId].SiteTile];
        return soot <= Fix64.Zero ? Fix64.One : Fix64.Min(SootFeverCap, Fix64.One + soot / 50);
    }

    // ---- snapshot and hash ------------------------------------------------------------------------------

    private void HashSoot(ref StateHasher hash)
    {
        if (!DiscoveryChain || _soot is not { } soot) return;
        bool any = false;
        for (int t = 0; t < soot.Length; t++)
        {
            if (soot[t] == Fix64.Zero) continue;
            if (!any) { hash.Add(SootTag); any = true; }
            hash.Add(t);
            hash.Add(soot[t]);
        }
    }

    private void WriteSoot(BinaryWriter writer)
    {
        if (!DiscoveryChain) return;
        writer.Write(SootSnapshotMagic);
        int count = 0;
        if (_soot is { } soot) for (int t = 0; t < soot.Length; t++) if (soot[t] != Fix64.Zero) count++;
        writer.Write(count);
        if (_soot is not { } soot2) return;
        for (int t = 0; t < soot2.Length; t++)
        {
            if (soot2[t] == Fix64.Zero) continue;
            writer.Write(t);
            writer.Write(soot2[t].Raw);
        }
    }

    private void ReadSoot(BinaryReader reader)
    {
        if (!DiscoveryChain) return;
        if (reader.ReadUInt32() != SootSnapshotMagic) throw new InvalidDataException("The snapshot has no Discovery soot section.");
        int count = reader.ReadInt32();
        if (count < 0 || count > _world.TileCount) throw new InvalidDataException("Invalid Discovery soot tile count.");
        _soot = count == 0 ? null : new Fix64[_world.TileCount];
        int previous = -1;
        for (int n = 0; n < count; n++)
        {
            int tile = reader.ReadInt32();
            if (tile <= previous || tile >= _world.TileCount) throw new InvalidDataException("Invalid Discovery soot tile index.");
            previous = tile;
            Fix64 value = Fix64.FromRaw(reader.ReadInt64());
            if (value < Fix64.Zero || value > SootCap) throw new InvalidDataException("Invalid Discovery soot amount.");
            _soot![tile] = value;
        }
    }

    // ---- fixtures ---------------------------------------------------------------------------------------

    internal Fix64 FixtureFactoryCoverage(int settlementIndex) => FactoryCoverage(_settlements[settlementIndex]);

    internal Fix64 FixtureFactoryLaborFactor(int settlementIndex) => FactoryLaborFactor(_settlements[settlementIndex]);

    internal Fix64 FixtureSootAt(int tile) => SootAt(tile);

    internal Fix64 FixtureSootFeverFactor(int settlementIndex) => SootFeverFactor(settlementIndex);

    internal void FixtureEmitSoot(int settlementIndex) => EmitSoot(_settlements[settlementIndex]);

    internal void FixtureDecaySootEverywhere() { for (int t = 0; t < _world.TileCount; t++) DecaySoot(t); }

    /// <summary>Fixture: gives the town's first adults the operative role directly, as <c>FixtureStaffAndBurnEngines</c> does for engineers.</summary>
    internal void FixtureStaffOperatives(int settlementIndex, int count)
    {
        int given = 0;
        foreach (FigureState f in Residents(_settlements[settlementIndex].Id))
        {
            if (f.Age is < 14 or >= 60 || f.Camp >= 0) continue;
            f.Role = given < count ? Role.Operative : Role.None;
            if (given < count) given++;
        }
    }
}
