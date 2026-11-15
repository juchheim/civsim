using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>
/// What a settlement can put in the field right now, read the way <c>Fighters</c> arms them (docs/MVP/13 §7.1, 22 §7.1, 26 §4): the people able to fight, the metal kits in
/// store, and how many of those kits are actually in someone's hands. A kit is only worn by a fighter, so a stock larger than the fighters present is idle metal.
/// </summary>
/// <param name="Fighters">Adults and elders who are not laid up with wounds.</param>
/// <param name="Wounded">Fighting-age people out of the fight this season.</param>
/// <param name="Warriors">Warrior-band members (WAR-04): one adult in five, men only, never a standing soldier.</param>
/// <param name="Soldiers">Standing soldiers in post and fed (WAR-07).</param>
/// <param name="StockSteel">Kits in store, strongest first: tempered-edge, iron, tin bronze, arsenical bronze, copper.</param>
/// <param name="Steel">Kits in someone's hands: strongest first, soldiers take steel then iron, then everyone takes bronze and copper kits in order.</param>
/// <param name="Chariots">Chariots in store (WAR-06); each carries a crew of two.</param>
/// <param name="ChariotCrew">Fighters riding chariots now.</param>
/// <param name="Engines">Siege engines in store (WAR-09).</param>
/// <param name="AttackStrength">The settlement's whole fighting strength if it marched out now (13 §7.1), in hundredths.</param>
public readonly record struct SettlementArms(
    int Fighters, int Wounded, int Warriors, int Soldiers,
    int StockSteel, int StockIron, int StockTin, int StockArsenical, int StockCopper,
    int Steel, int Iron, int Tin, int Arsenical, int Copper,
    int Chariots, int ChariotCrew, int Engines, int AttackStrength);

public sealed partial class PoliticsSystem
{
    // Scratch for ArmsOf, which runs on every frame build and may not allocate.
    private readonly List<LivingEconomyModel.FighterRecord> _armsRecords = new();
    private readonly List<Fighter> _armsFighters = new();

    /// <summary>
    /// The base weapon of a civilization's ordinary fighters, from research alone (13 §7.1): 0 bare hands, 1 club or thrown stones, 2 wooden spear, 3 stone-tipped
    /// spear, 4 atlatl (ranged), 5 bow (ranged), 6 bow with Archery Warfare (ranged, x1.5). Metal kits replace it for whoever wears one.
    /// </summary>
    public int BaseWeaponTier(int civ)
    {
        if (_research.IsDone(civ, _p.Hnt06)) return _research.IsDone(civ, _p.War03) ? 6 : 5;
        if (_research.IsDone(civ, _p.Hnt05)) return 4;
        if (_research.IsDone(civ, _p.Hnt03)) return 3;
        if (_research.IsDone(civ, _p.Hnt02)) return 2;
        if (_research.IsDone(civ, _p.War01)) return 1;
        return 0;
    }

    /// <summary>The weapon multiplier a civilization's base weapon gives its fighters, in hundredths (60 bare hands, up to 270 for a bow with Archery Warfare).</summary>
    public int BaseWeaponHundredths(int civ) => (Weapon(civ, out _) * Fix64.FromInt(100) + Fix64.Half).FloorToInt();

    /// <summary>A settlement's fighters and arms as the next march would find them. Read-only; nothing here changes the simulation.</summary>
    public SettlementArms ArmsOf(int settlement)
    {
        if ((uint)settlement >= (uint)_sCount) return default;
        int civ = _sCiv[settlement];
        List<LivingEconomyModel.FighterRecord> records = _armsRecords;
        records.Clear();
        _economy.ListFightingAge(settlement, records);
        int fighters = 0, wounded = 0, soldiers = 0, adults = 0;
        foreach (var r in records)
        {
            if (r.Age < 45) adults++;
            if (r.Wounded) { wounded++; continue; }
            fighters++;
            if (r.Soldier) soldiers++;
        }
        int warriors = _research.IsDone(civ, _p.War04) ? System.Math.Min(adults / 5, fighters - soldiers) : 0;

        int stockSteel = _economy.IronChain ? _economy.GoodStock(settlement, "G-ARMS4").FloorToInt() : 0;
        int stockIron = _economy.IronChain ? _economy.GoodStock(settlement, "G-ARMS3").FloorToInt() : 0;
        int stockTin = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS2T").FloorToInt() : 0;
        int stockArsenical = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS2A").FloorToInt() : 0;
        int stockCopper = _economy.BronzeActive ? _economy.GoodStock(settlement, "G-ARMS1").FloorToInt() : 0;

        // The same order Fighters uses: soldiers take steel, then iron; whoever is left takes tin bronze, arsenical bronze, copper.
        int steel = System.Math.Min(stockSteel, soldiers);
        int iron = System.Math.Min(stockIron, soldiers - steel);
        int open = fighters - steel - iron;
        int tin = System.Math.Min(stockTin, open); open -= tin;
        int arsenical = System.Math.Min(stockArsenical, open); open -= arsenical;
        int copper = System.Math.Min(stockCopper, open);

        int chariots = _economy.BronzeActive && _research.IsDone(civ, _p.War06) ? _economy.GoodStock(settlement, "G-CHARIOT").FloorToInt() : 0;
        int engines = _economy.IronChain && _research.IsDone(civ, _p.War09) ? _economy.GoodStock(settlement, "G-ENGINE").FloorToInt() : 0;

        Fix64 strength = Fix64.Zero;
        FightersInto(settlement, attacking: true, -1, _armsRecords, _armsFighters, null);
        foreach (Fighter f in _armsFighters) strength += f.Strength;
        return new SettlementArms(fighters, wounded, warriors, soldiers,
            stockSteel, stockIron, stockTin, stockArsenical, stockCopper, steel, iron, tin, arsenical, copper,
            chariots, System.Math.Min(chariots * 2, fighters), engines, (strength * Fix64.FromInt(100) + Fix64.Half).FloorToInt());
    }
}
