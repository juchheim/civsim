using CivSim.Sim.Economy;
using CivSim.Sim.Core;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.View;

public sealed partial class FrameBuilder
{
    private void UpdateDepositKnowledge(int tile, bool ownTerritory, LivingEconomyModel economy, ResearchSystem research)
    {
        DepositKind deposit = _tiles[tile].Deposit;
        if (_omniscient || ProjectDone(research, _min16))
        {
            _knownDeposits[tile] = deposit == DepositKind.None ? (byte)0 : (byte)1;
            _depositClues[tile] = 0;
            return;
        }

        bool visible = Visible(tile);
        bool explored = _explored[tile] != 0;
        if (visible && _knownDeposits[tile] == 0)
            _depositClues[tile] = (byte)_tiles[tile].SurfaceEvidence;

        // Evidence is a world feature, not a property of the hidden deposit. Clue-only tiles must survive.
        if (deposit == DepositKind.None) return;

        if (_knownDeposits[tile] == 0 && explored && IsDepositIdentified(tile, deposit, visible, ownTerritory, research))
            _knownDeposits[tile] = 1;

        // Working a deposit is itself proof that people found it.
        if (_knownDeposits[tile] == 0 && visible && economy.DepositWorked(tile))
            _knownDeposits[tile] = 1;

        if (_knownDeposits[tile] != 0) _depositClues[tile] = 0;
    }

    private bool IsDepositIdentified(int tile, DepositKind deposit, bool visible, bool ownTerritory, ResearchSystem research)
    {
        if (IsObviousSurfaceDeposit(deposit) && _tiles[tile].DepositDepth <= 1) return true;

        // Flint & Ochre Seeking turns nearby outcrops into mapped, named deposits.
        if (ProjectDone(research, _min01) && (deposit is DepositKind.Flint or DepositKind.Ochre) && NearOwnSettlement(tile, 25))
            return true;

        // Stone & Ore Lore recognizes exposed copper, tin, gold and silver where people can inspect the ground.
        if (visible && ProjectDone(research, _inq03) && _tiles[tile].DepositDepth <= 1 &&
            deposit is DepositKind.CopperOre or DepositKind.Tin or DepositKind.Gold or DepositKind.Silver)
            return true;

        // A discovered outcrop supports local shaft prospecting. The clue may be on a barren tile in the same province.
        if (ownTerritory && ProjectDone(research, _min04) && _tiles[tile].DepositDepth >= 2 && IsOreVein(deposit) &&
            HasNearbyMineralOutcrop(tile)) return true;

        // Surveying maps ore veins across owned land. Bloomery Iron gives a nearer, earlier survey for iron.
        if (ownTerritory && ProjectDone(research, _min08) && IsOreVein(deposit)) return true;
        if (deposit == DepositKind.Iron && ProjectDone(research, _met08) && NearOwnSettlement(tile, 20)) return true;

        // Geology and the dedicated materials projects identify modern fuel and specialty minerals locally.
        if (ownTerritory && ProjectDone(research, _inq13) && (deposit is DepositKind.Coal or DepositKind.Oil)) return true;
        if (deposit == DepositKind.Coal && ProjectDone(research, _min09) && HasNearbyEvidence(tile, SurfaceEvidence.DarkSeam, 10)) return true;
        if (deposit == DepositKind.Oil && ProjectDone(research, _min13) && HasNearbyEvidence(tile, SurfaceEvidence.OilSeep, 10)) return true;
        if (ownTerritory && ProjectDone(research, _min15) && (deposit is DepositKind.RareEarths or DepositKind.Lithium)) return true;
        if (ownTerritory && ProjectDone(research, _met16) && deposit == DepositKind.Bauxite) return true;

        return false;
    }

    private bool HasNearbyMineralOutcrop(int tile)
    {
        GeologyProvince province = _tiles[tile].Geology;
        if (province == GeologyProvince.None) return false;
        for (int clue = 0; clue < _tileCount; clue++)
            if (_depositClues[clue] == (byte)SurfaceEvidence.MineralOutcrop && _tiles[clue].Geology == province &&
                HexGrid.Distance(tile, clue, _width) <= 10) return true;
        return false;
    }

    private bool HasNearbyEvidence(int tile, SurfaceEvidence evidence, int radius)
    {
        for (int clue = 0; clue < _tileCount; clue++)
            if (_depositClues[clue] == (byte)evidence && HexGrid.Distance(tile, clue, _width) <= radius) return true;
        return false;
    }

    private bool NearOwnSettlement(int tile, int radius)
    {
        for (int settlement = 0; settlement < _settlementCount; settlement++)
        {
            if (_sPop[settlement] == 0 || _sFree[settlement] || _sCiv[settlement] != _viewer) continue;
            if (HexGrid.Distance(_sTile[settlement], tile, _width) <= radius) return true;
        }
        return false;
    }

    private bool ProjectDone(ResearchSystem research, int project)
        => project >= 0 && _viewer >= 0 && research.IsDone(_viewer, project);

    private static bool IsObviousSurfaceDeposit(DepositKind deposit)
        => deposit is DepositKind.Stone or DepositKind.Flint or DepositKind.Clay or DepositKind.Ochre or
            DepositKind.NativeCopper or DepositKind.Salt or DepositKind.Saltpeter or DepositKind.Sulfur or
            DepositKind.SilkMoths or DepositKind.Sugarcane;

    private static bool IsOreVein(DepositKind deposit)
        => deposit is DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold or
            DepositKind.Silver or DepositKind.Iron;
}
