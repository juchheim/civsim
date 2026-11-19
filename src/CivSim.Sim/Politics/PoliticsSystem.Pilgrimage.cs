using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>Same-season, deterministic BEL-11 pilgrimages over completed connected Fief roads.</summary>
public sealed partial class PoliticsSystem
{
    // A resolved trip carries a route marker through this turn's claim refresh and trade dispatch only.
    // The draft party size is four; it eats one ration per eight road tiles for each traveler.
    private const int PilgrimPartySize = 4, PilgrimageTilesPerSeason = 8;
    private static readonly Fix64 PilgrimageClaimBonus = Fix64.FromRatio(5, 100);

    private sealed class PilgrimageRoute
    {
        public int Civ, FromSettlement, ToSettlement, UseSeason;
        public readonly List<int> Settlements = new();
        public readonly List<int> Tiles = new();
    }

    private readonly List<PilgrimageRoute> _pilgrimageRoutes = new();

    /// <summary>Orders and immediately resolves a four-person pilgrimage over complete connected roads; returns false without spending food on any invalid order.</summary>
    public bool OrderPilgrimage(int civ, int fromSettlement, int toSettlement)
    {
        if (!_economy.FiefChain || (uint)civ >= (uint)_civs ||
            (uint)fromSettlement >= (uint)_sCount || (uint)toSettlement >= (uint)_sCount) return false;
        var route = new PilgrimageRoute { Civ = civ, FromSettlement = fromSettlement, ToSettlement = toSettlement, UseSeason = _season + 1 };
        if (!_economy.TryPilgrimageRoadPath(civ, fromSettlement, toSettlement, route.Settlements, route.Tiles)) return false;
        int traveledTiles = route.Tiles.Count - 1;
        int travelSeasons = Math.Max(1, (traveledTiles + PilgrimageTilesPerSeason - 1) / PilgrimageTilesPerSeason);
        Fix64 foodCost = Fix64.FromInt(PilgrimPartySize * travelSeasons);
        if (_economy.FoodStores(fromSettlement) < foodCost) return false;
        Fix64 eaten = _economy.TakeGoods(fromSettlement, foodCost, LootPreference.FoodOnly);
        if (eaten < foodCost)
        {
            if (eaten > Fix64.Zero) _economy.GiveGoods(fromSettlement, eaten);
            return false;
        }
        _pilgrimageRoutes.Add(route);
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.PilgrimageCompleted, toSettlement, fromSettlement, foodCost.FloorToInt()));
        return true;
    }

    /// <summary>Active only during the season in which a route's completed pilgrimage is applied.</summary>
    private bool PilgrimageRouteActive(PilgrimageRoute route) => _economy.FiefChain && route.UseSeason >= _season && route.UseSeason <= _season + 1;

    /// <summary>Extra claim emission scoped to the exact road tiles actually traversed this season.</summary>
    private Fix64 PilgrimageClaimBonusAt(int civ, int tile)
    {
        foreach (PilgrimageRoute route in _pilgrimageRoutes)
            if (route.Civ == civ && PilgrimageRouteActive(route) && route.Tiles.Contains(tile)) return PilgrimageClaimBonus;
        return Fix64.Zero;
    }

    /// <summary>Route-trade factor for an exact settlement pair that carried pilgrims this season, with the cathedral factor only when that trip ended here.</summary>
    public Fix64 PilgrimageTradeMultiplier(int fromSettlement, int toSettlement)
    {
        if (!_economy.FiefChain) return Fix64.One;
        bool carriedPilgrims = false, pilgrimageEndedHere = false;
        foreach (PilgrimageRoute route in _pilgrimageRoutes)
        {
            if (!PilgrimageRouteActive(route) ||
                !(route.FromSettlement == fromSettlement && route.ToSettlement == toSettlement ||
                  route.FromSettlement == toSettlement && route.ToSettlement == fromSettlement)) continue;
            carriedPilgrims = true;
            pilgrimageEndedHere |= route.ToSettlement == toSettlement;
        }
        if (!carriedPilgrims) return Fix64.One;
        int civ = _economy.SettlementLite(fromSettlement).Civilization;
        Fix64 mult = _economy.PilgrimageRouteTradeFactor(civ);
        if (pilgrimageEndedHere) mult *= _economy.PilgrimageTradeMultiplier(toSettlement);
        return mult;
    }

    private void ClearCompletedPilgrimageRoutes() => _pilgrimageRoutes.Clear();

    internal void HashPilgrimageState(ref StateHasher h)
    {
        h.Add(_pilgrimageRoutes.Count);
        foreach (PilgrimageRoute route in _pilgrimageRoutes)
        {
            h.Add(route.Civ); h.Add(route.FromSettlement); h.Add(route.ToSettlement); h.Add(route.UseSeason);
            h.Add(route.Settlements.Count); foreach (int settlement in route.Settlements) h.Add(settlement);
            h.Add(route.Tiles.Count); foreach (int tile in route.Tiles) h.Add(tile);
        }
    }

    internal void WritePilgrimageState(System.IO.BinaryWriter writer)
    {
        writer.Write(_pilgrimageRoutes.Count);
        foreach (PilgrimageRoute route in _pilgrimageRoutes)
        {
            writer.Write(route.Civ); writer.Write(route.FromSettlement); writer.Write(route.ToSettlement); writer.Write(route.UseSeason);
            writer.Write(route.Settlements.Count); foreach (int settlement in route.Settlements) writer.Write(settlement);
            writer.Write(route.Tiles.Count); foreach (int tile in route.Tiles) writer.Write(tile);
        }
    }

    internal void ReadPilgrimageState(System.IO.BinaryReader reader)
    {
        _pilgrimageRoutes.Clear();
        int count = reader.ReadInt32();
        if (count is < 0 or > 10_000) throw new System.IO.InvalidDataException("Invalid pilgrimage route count in snapshot.");
        for (int i = 0; i < count; i++)
        {
            var route = new PilgrimageRoute
            {
                Civ = reader.ReadInt32(), FromSettlement = reader.ReadInt32(), ToSettlement = reader.ReadInt32(), UseSeason = reader.ReadInt32(),
            };
            int settlements = reader.ReadInt32();
            if (settlements is < 2 or > 10_000) throw new System.IO.InvalidDataException("Invalid pilgrimage settlement path in snapshot.");
            for (int j = 0; j < settlements; j++) route.Settlements.Add(reader.ReadInt32());
            int tiles = reader.ReadInt32();
            if (tiles is < 2 or > 100_000) throw new System.IO.InvalidDataException("Invalid pilgrimage tile path in snapshot.");
            for (int j = 0; j < tiles; j++) route.Tiles.Add(reader.ReadInt32());
            if ((uint)route.Civ >= (uint)_civs || (uint)route.FromSettlement >= (uint)_economy.SettlementCount ||
                (uint)route.ToSettlement >= (uint)_economy.SettlementCount || route.FromSettlement == route.ToSettlement ||
                route.Settlements[0] != route.FromSettlement || route.Settlements[^1] != route.ToSettlement)
                throw new System.IO.InvalidDataException("Invalid pilgrimage route in snapshot.");
            foreach (int settlement in route.Settlements)
                if ((uint)settlement >= (uint)_economy.SettlementCount) throw new System.IO.InvalidDataException("Pilgrimage path leaves the settlement list.");
            foreach (int tile in route.Tiles)
                if ((uint)tile >= (uint)_tileArray.Length) throw new System.IO.InvalidDataException("Pilgrimage path leaves the map.");
            _pilgrimageRoutes.Add(route);
        }
    }

    internal void FixtureRefreshPilgrimageSettlements() => RefreshSettlements();
    internal List<ClaimSource> FixturePilgrimageClaimSources()
    {
        var sources = new List<ClaimSource>();
        BuildSettlementClaimSources(sources);
        return sources;
    }
}
