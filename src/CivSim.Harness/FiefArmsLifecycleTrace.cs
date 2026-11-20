using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Harness;

/// <summary>Opt-in, read-only inventory and combat-roster observations for the metal-arms lifecycle.</summary>
internal sealed class FiefArmsLifecycleTrace
{
    private readonly HashSet<(int Civ, string Project)> _researchMilestones = new();
    private readonly HashSet<(int Settlement, string Tier)> _stockMilestones = new();
    private readonly HashSet<(int Settlement, string Tier)> _wholeStockMilestones = new();
    private readonly HashSet<(int Settlement, string Tier)> _carrierMilestones = new();
    private readonly HashSet<(int Settlement, string Recipe)> _producerMilestones = new();
    private readonly HashSet<(int Settlement, string Good)> _intermediateStockMilestones = new();
    private readonly HashSet<(int Settlement, string Good)> _intermediateDeliveryMilestones = new();
    private readonly HashSet<int> _chariotStockMilestones = new();
    private readonly HashSet<int> _chariotCrewMilestones = new();
    private readonly HashSet<int> _engineStockMilestones = new();
    private readonly HashSet<int> _cannonStockMilestones = new();
    private readonly HashSet<int> _powderStockMilestones = new();
    private readonly HashSet<int> _engineSiegeMilestones = new();
    private readonly HashSet<int> _cannonSiegeMilestones = new();
    private readonly HashSet<int> _forgeMilestones = new();
    private readonly HashSet<int> _withoutWeaponMilestones = new();
    private readonly HashSet<int> _levyBowReadyMilestones = new();
    private readonly Dictionary<int, string> _forgeQueueState = new();
    private readonly Dictionary<(int Settlement, string Producer), string> _producerQueueState = new();
    private bool _coverageWritten;
    private Fix64[] _arsenicalBronzeDeliveredByCiv = Array.Empty<Fix64>();
    private Fix64[] _crucibleSteelDeliveredByCiv = Array.Empty<Fix64>();

    private static readonly (string Tier, string Good, Func<SettlementArms, int> Carried)[] Tiers =
    [
        ("steel", "G-ARMS4", a => a.Steel),
        ("crucible", "G-ARMS5", a => a.Crucible),
        ("iron", "G-ARMS3", a => a.Iron),
        ("tin", "G-ARMS2T", a => a.Tin),
        ("arsenical", "G-ARMS2A", a => a.Arsenical),
        ("copper", "G-ARMS1", a => a.Copper),
        ("crossbow", "G-CROSSBOW", a => a.Crossbow),
        ("longbow", "G-LONGBOW", a => a.Longbow),
        ("warbow", "G-BOW", a => a.WarBow),
    ];

    private static readonly string[] ResearchProjects = ["MIN-03", "MIN-04", "HNT-06", "WAR-03", "MET-03", "MET-04", "WAR-05", "WAR-06", "WAR-08", "MET-10", "WAR-09", "WAR-11", "CHM-07", "WAR-12", "MET-11"];
    private static readonly string[] MaterialGoods =
    [
        "G-COPPER", "G-ASORE", "G-ABRONZE", "G-BRONZE", "G-IRONORE", "G-WROUGHT", "G-STEEL", "G-CSTEEL", "G-CHAR",
        "G-WOOD", "G-CLAY", "G-LEATHER", "G-HIDE", "G-WHEEL", "G-HORSE", "G-CHARIOT", "G-ENGINE",
        "G-IRON", "G-CORD", "G-BLOCK", "G-CAST", "G-SALTPETER", "G-SULFUR", "G-POWDER", "G-CANNON", "G-CSTEEL",
    ];

    private static readonly Recipe[] Recipes =
    [
        new("cosmelt_arsenical", ["MET-04"], [["G-ASORE"], ["G-CHAR"]], [2, 2], "smelter"),
        new("crucible_steel", ["MET-11"], [["G-STEEL"], ["G-CLAY"], ["G-CHAR"]], [0.4, 0.3, 0.4], "forge"),
        new("cast_arms1", ["MET-03"], [["G-COPPER"], ["G-WOOD"]], [0.2, 0.5], "forge"),
        new("cast_arms2a", ["WAR-05"], [["G-ABRONZE"], ["G-WOOD"], ["G-LEATHER", "G-HIDE"]], [0.3, 1, 1], "forge"),
        new("cast_arms2t", ["WAR-05"], [["G-BRONZE"], ["G-WOOD"], ["G-LEATHER", "G-HIDE"]], [0.3, 1, 1], "forge"),
        new("make_iron_arms", ["WAR-08"], [["G-WROUGHT"], ["G-WOOD"], ["G-LEATHER", "G-HIDE"]], [0.25, 0.4, 0.3], "forge"),
        new("make_steel_arms", ["WAR-08", "MET-10"], [["G-STEEL"], ["G-WOOD"], ["G-LEATHER", "G-HIDE"]], [0.25, 0.4, 0.3], "forge"),
        new("make_crucible_arms", ["MET-11"], [["G-CSTEEL"], ["G-WOOD"], ["G-LEATHER", "G-HIDE"]], [0.2, 0.4, 0.3], "forge"),
        new("build_chariot", ["WAR-06"], [["G-WHEEL"], ["G-WOOD"], ["G-BRONZE", "G-ABRONZE"], ["G-LEATHER", "G-HIDE"]], [2, 2, 0.2, 1], "forge"),
        new("make_siege_engine", ["WAR-09"], [["G-WOOD"], ["G-BLOCK"], ["G-WROUGHT"]], [8, 4, 0.4], "forge"),
        new("make_crossbow", ["WAR-11"], [["G-WOOD"], ["G-WROUGHT", "G-IRON"], ["G-CORD"]], [1, 0.1, 0.2], "forge"),
        new("make_longbow", ["WAR-11"], [["G-WOOD"], ["G-CORD"]], [1, 0.1], "none"),
        new("hunt_bow_supply", ["HNT-06"], [], [], "hunt"),
        new("cast_cannon", ["WAR-12"], [["G-CAST"], ["G-WOOD"]], [2, 6], "arsenal"),
        new("make_powder", ["CHM-07"], [["G-SALTPETER"], ["G-SULFUR"], ["G-CHAR"]], [0.75, 0.1, 0.15], "arsenal"),
    ];

    private sealed record Recipe(string Id, string[] Projects, string[][] Inputs, double[] Quantities, string Producer);

    internal static bool IsRequested(string[] args) => Array.IndexOf(args, "--fief-arms-lifecycle-trace") >= 0;

    internal void AppendSeason(StringBuilder output, ulong seed, int season,
        LivingEconomyModel economy, PoliticsSystem politics, ResearchSystem research)
    {
        if (!_coverageWritten)
        {
            output.AppendLine($"fief-arms-coverage seed={seed} tier=crucible good=G-ARMS5 stockable=true roster_supported=true families=copper,bronze,iron,steel,crucible,bow,crossbow,longbow,chariot,engine,cannon,powder researchFlags=MIN-03,HNT-06,WAR-03,MET-03,WAR-05,WAR-06,WAR-08,MET-10,WAR-09,WAR-11,CHM-07,WAR-12,MET-11 reason=candidate_ArmsOf_exposes_crucible");
            _coverageWritten = true;
        }

        if (_arsenicalBronzeDeliveredByCiv.Length != economy.CivilizationCount)
        {
            _arsenicalBronzeDeliveredByCiv = new Fix64[economy.CivilizationCount];
            _crucibleSteelDeliveredByCiv = new Fix64[economy.CivilizationCount];
        }
        foreach (SettlementSupplyTransfer transfer in economy.SupplyTransfers)
        {
            string good = transfer.GoodId;
            if (good is not ("G-ABRONZE" or "G-CSTEEL")) continue;
            int receiverCiv = economy.SettlementLite(transfer.ToSettlement).Civilization;
            if ((uint)receiverCiv >= (uint)economy.CivilizationCount) continue;
            if (good == "G-ABRONZE") _arsenicalBronzeDeliveredByCiv[receiverCiv] += transfer.Quantity;
            else _crucibleSteelDeliveredByCiv[receiverCiv] += transfer.Quantity;
            if (_intermediateDeliveryMilestones.Add((transfer.ToSettlement, good)))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=intermediate-delivery civ={receiverCiv} settlement={transfer.ToSettlement} good={good} from={transfer.FromSettlement} season={season}");
        }

        foreach (int civ in Enumerable.Range(0, economy.CivilizationCount))
        foreach (string project in ResearchProjects)
        {
            int index = research.Catalog.IndexOf(project);
            if (index < 0 || !research.IsDone(civ, index) || !_researchMilestones.Add((civ, project))) continue;
            int completedSeason = checked((int)(research.CompletedTick(civ, index) / 150L));
            output.AppendLine($"fief-arms-milestone seed={seed} kind=research civ={civ} project={project} season={completedSeason}");
        }

        foreach (int civ in Enumerable.Range(0, economy.CivilizationCount))
            if (IsDone(research, civ, "HNT-06") && IsDone(research, civ, "WAR-03") && _levyBowReadyMilestones.Add(civ))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=levy-bow-ready civ={civ} season={season} requires=HNT-06+WAR-03");

        var civRows = new List<SiteRow>[economy.CivilizationCount];
        for (int civ = 0; civ < civRows.Length; civ++) civRows[civ] = new List<SiteRow>();

        for (int settlement = 0; settlement < economy.SettlementCount; settlement++)
        {
            var lite = economy.SettlementLite(settlement);
            int civ = lite.Civilization;
            if ((uint)civ >= (uint)civRows.Length || politics.IsFreeFolk(settlement)) continue;

            MetalReadout metal = economy.Metal(settlement);
            int forge = metal.Forges;
            foreach (string intermediate in new[] { "G-ABRONZE", "G-CSTEEL" })
                if (economy.GoodStock(settlement, intermediate) > Fix64.Zero &&
                    _intermediateStockMilestones.Add((lite.Id, intermediate)))
                    output.AppendLine($"fief-arms-milestone seed={seed} kind=intermediate-stock civ={civ} settlement={lite.Id} good={intermediate} season={season}");
            BuildOutlook buildOutlook = economy.BuildOutlookFor(settlement);
            BuildItem? forgeItem = buildOutlook.Items.FirstOrDefault(item => item.Id == "forge");
            string queueKey = ForgeQueueKey(forgeItem);
            if (forge == 0 && forgeItem is not null && _forgeQueueState.GetValueOrDefault(lite.Id) != queueKey)
            {
                _forgeQueueState[lite.Id] = queueKey;
                string queueShortages = string.Join(',', forgeItem.Needs.Where(need => need.Short)
                    .Select(need => $"{LivingEconomyModel.StoreSlotId(need.Slot)}:{F(need.Have)}/{F(need.Need)}"));
                output.AppendLine($"fief-arms-forge-queue seed={seed} season={season} civ={civ} settlement={lite.Id} status={forgeItem.Status} cause={forgeItem.Gate.Cause} why={forgeItem.Gate.Why} project={forgeItem.Gate.Project ?? "none"} shortages={(queueShortages.Length == 0 ? "none" : queueShortages)}");
            }
            if (forge > 0 && _forgeMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=forge civ={civ} settlement={lite.Id} season={season} count={forge}");
            foreach (BuildItem producerItem in buildOutlook.Items.Where(item => item.Id is "blast_furnace" or "arsenal"))
            {
                if (ProducerCount(economy, settlement, producerItem.Id, forge) > 0) continue;
                string producerQueueKey = ForgeQueueKey(producerItem);
                var key = (lite.Id, producerItem.Id);
                if (_producerQueueState.GetValueOrDefault(key) == producerQueueKey) continue;
                _producerQueueState[key] = producerQueueKey;
                string producerShortages = string.Join(',', producerItem.Needs.Where(need => need.Short)
                    .Select(need => $"{LivingEconomyModel.StoreSlotId(need.Slot)}:{F(need.Have)}/{F(need.Need)}"));
                output.AppendLine($"fief-arms-producer-queue seed={seed} season={season} civ={civ} settlement={lite.Id} producer={producerItem.Id} status={producerItem.Status} cause={producerItem.Gate.Cause} why={producerItem.Gate.Why} project={producerItem.Gate.Project ?? "none"} shortages={(producerShortages.Length == 0 ? "none" : producerShortages)}");
            }
            foreach (Recipe recipe in Recipes)
            {
                int producerCount = ProducerCount(economy, settlement, recipe.Producer, forge);
                if (recipe.Projects.All(project => IsDone(research, civ, project)) && producerCount > 0 && _producerMilestones.Add((lite.Id, recipe.Id)))
                    output.AppendLine($"fief-arms-milestone seed={seed} kind=producer-ready civ={civ} settlement={lite.Id} recipe={recipe.Id} producer={recipe.Producer} count={producerCount} season={season}");
            }

            SettlementArms arms = politics.ArmsOf(settlement);
            var stock = new double[Tiers.Length];
            var carried = new int[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                stock[i] = Math.Max(0, economy.GoodStock(settlement, Tiers[i].Good).ToDoubleForDisplay());
                carried[i] = Tiers[i].Carried(arms);
                if (stock[i] > 0 && _stockMilestones.Add((lite.Id, Tiers[i].Tier)))
                    output.AppendLine($"fief-arms-milestone seed={seed} kind=stock civ={civ} settlement={lite.Id} tier={Tiers[i].Tier} season={season}");
                if (stock[i] >= 1 && _wholeStockMilestones.Add((lite.Id, Tiers[i].Tier)))
                    output.AppendLine($"fief-arms-milestone seed={seed} kind=whole-stock civ={civ} settlement={lite.Id} tier={Tiers[i].Tier} season={season} count={Math.Floor(stock[i])}");
                if (carried[i] > 0 && _carrierMilestones.Add((lite.Id, Tiers[i].Tier)))
                    output.AppendLine($"fief-arms-milestone seed={seed} kind=carrier civ={civ} settlement={lite.Id} tier={Tiers[i].Tier} season={season} count={carried[i]}");
            }

            int armed = carried.Sum();
            int metalArmed = carried.Take(6).Sum();
            int bowArmed = carried.Skip(6).Sum();
            int withoutWeapon = Math.Max(0, arms.Fighters - armed);
            if (withoutWeapon > 0 && _withoutWeaponMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=without-weapon civ={civ} settlement={lite.Id} season={season} fighters={arms.Fighters} withoutWeapon={withoutWeapon}");
            if (arms.Chariots > 0 && _chariotStockMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=chariot-stock civ={civ} settlement={lite.Id} season={season} count={arms.Chariots}");
            if (arms.ChariotCrew > 0 && _chariotCrewMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=chariot-crew civ={civ} settlement={lite.Id} season={season} riders={arms.ChariotCrew} chariots={arms.Chariots}");

            string researchBits = string.Join(',', ResearchProjects.Select(project => $"{Key(project)}={Bit(IsDone(research, civ, project))}"));
            string tierStocks = string.Join(',', Tiers.Select((tier, i) => $"{tier.Tier}:{F(stock[i])}/{carried[i]}"));
            string materials = string.Join(',', MaterialGoods.Select(good => $"{Key(good)}:{F(economy.GoodStock(settlement, good))}"));
            string recipeBits = string.Concat(Recipes.Select(recipe => RecipeState(recipe, economy, research, settlement, civ, forge)));
            string recipeReadiness = string.Join(',', Recipes.Select((recipe, i) => $"{recipe.Id}:{(recipeBits[i] switch { '1' => "ready", '0' => "inputs_short", _ => "ineligible" })}"));
            string shortages = forgeItem is null ? "none" : string.Join(',', forgeItem.Needs.Where(need => need.Short)
                .Select(need => $"{LivingEconomyModel.StoreSlotId(need.Slot)}:{F(need.Have)}/{F(need.Need)}"));

            bool relevant = forge > 0 || forgeItem is not null || stock.Any(value => value > 0) || carried.Any(value => value > 0) ||
                arms.Chariots > 0 || arms.ChariotCrew > 0 || ResearchProjects.Any(project => IsDone(research, civ, project));

            Fix64 engineStock = economy.GoodStock(settlement, "G-ENGINE");
            Fix64 cannonStock = economy.GoodStock(settlement, "G-CANNON");
            Fix64 powderStock = economy.GoodStock(settlement, "G-POWDER");
            Investment[] homeInvestments = politics.Investments.Where(investment => investment.HomeSettlement == lite.Id).ToArray();
            int siegeEngines = homeInvestments.Sum(investment => investment.Engines);
            int siegeCannons = homeInvestments.Sum(investment => investment.Cannons);
            int activeSieges = homeInvestments.Length;
            if (engineStock > Fix64.Zero && _engineStockMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=stock civ={civ} settlement={lite.Id} tier=engine season={season}");
            if (cannonStock > Fix64.Zero && _cannonStockMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=stock civ={civ} settlement={lite.Id} tier=cannon season={season}");
            if (powderStock > Fix64.Zero && _powderStockMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=stock civ={civ} settlement={lite.Id} tier=powder season={season}");
            if (siegeEngines > 0 && _engineSiegeMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=siege-assigned civ={civ} settlement={lite.Id} tier=engine season={season} count={siegeEngines}");
            if (siegeCannons > 0 && _cannonSiegeMilestones.Add(lite.Id))
                output.AppendLine($"fief-arms-milestone seed={seed} kind=siege-assigned civ={civ} settlement={lite.Id} tier=cannon season={season} count={siegeCannons}");
            relevant |= engineStock > Fix64.Zero || cannonStock > Fix64.Zero || powderStock > Fix64.Zero || activeSieges > 0;
            if (relevant && season % 4 == 3)
            {
                double idleStock = stock.Select((value, i) => Math.Max(0, value - carried[i])).Sum();
                output.AppendLine($"fief-arms-state seed={seed} season={season} civ={civ} settlement={lite.Id} pop={economy.Population(lite.Id)} forge={forge} smelter={metal.Smelters} arsenicAccess={metal.ArsenicAccess.ToString().ToLowerInvariant()} blastFurnace={ProducerCount(economy, settlement, "blast_furnace", forge)} powderMill={ProducerCount(economy, settlement, "powder_mill", forge)} forgeQueue={(forgeItem is null ? "none" : forgeItem.Status.ToString())} forgeCause={(forgeItem is null ? "none" : forgeItem.Gate.Cause.ToString())} forgeShortages={(shortages.Length == 0 ? "none" : shortages)} research={researchBits} fighters={arms.Fighters} soldiers={arms.Soldiers} warriors={arms.Warriors} armed={armed} metalArmed={metalArmed} bowArmed={bowArmed} withoutWeapon={withoutWeapon} stockCarriedByTier={tierStocks} idleStock={F(idleStock)} chariots={arms.Chariots} chariotCrew={arms.ChariotCrew} engineStock={F(engineStock)} siegeEngines={siegeEngines} cannonStock={F(cannonStock)} siegeCannons={siegeCannons} powderStock={F(powderStock)} activeSieges={activeSieges} inputs={materials} recipeInputsReady={recipeReadiness}");
            }

            civRows[civ].Add(new SiteRow(arms, stock, carried, forge > 0, metal.Smelters > 0, forgeItem is not null, recipeBits,
                engineStock, cannonStock, powderStock, siegeEngines, siegeCannons));
        }

        if (season % 4 != 3) return;
        for (int civ = 0; civ < civRows.Length; civ++)
        {
            List<SiteRow> rows = civRows[civ];
            int fighters = rows.Sum(row => row.Arms.Fighters);
            int armed = rows.Sum(row => row.Carried.Sum());
            int metalArmed = rows.Sum(row => row.Carried.Take(6).Sum());
            int bowArmed = rows.Sum(row => row.Carried.Skip(6).Sum());
            int withoutWeapon = rows.Sum(row => Math.Max(0, row.Arms.Fighters - row.Carried.Sum()));
            int chariots = rows.Sum(row => row.Arms.Chariots);
            int chariotCrew = rows.Sum(row => row.Arms.ChariotCrew);
            int chariotStockSites = rows.Count(row => row.Arms.Chariots > 0);
            int chariotCrewSites = rows.Count(row => row.Arms.ChariotCrew > 0);
            int idleChariots = rows.Sum(row => Math.Max(0, row.Arms.Chariots - (row.Arms.ChariotCrew + 1) / 2));
            double engineStock = rows.Sum(row => row.EngineStock.ToDoubleForDisplay());
            double cannonStock = rows.Sum(row => row.CannonStock.ToDoubleForDisplay());
            double powderStock = rows.Sum(row => row.PowderStock.ToDoubleForDisplay());
            int siegeEngines = rows.Sum(row => row.SiegeEngines);
            int siegeCannons = rows.Sum(row => row.SiegeCannons);
            double idle = rows.Sum(row => row.Stock.Select((value, i) => Math.Max(0, value - row.Carried[i])).Sum());
            double stockTotal = rows.Sum(row => row.Stock.Sum());
            int idleSites = rows.Count(row => row.Stock.Select((value, i) => Math.Max(0, value - row.Carried[i])).Sum() > 0);
            int baseOnlySites = rows.Count(row => row.Arms.Fighters > row.Carried.Sum());
            int overlap = rows.Count(row => row.Arms.Fighters > row.Carried.Sum() && row.Stock.Select((value, i) => Math.Max(0, value - row.Carried[i])).Sum() > 0);
            int[] inputReady = new int[Recipes.Length], eligible = new int[Recipes.Length];
            foreach (SiteRow row in rows)
            for (int recipe = 0; recipe < Recipes.Length; recipe++)
            {
                if (row.RecipeBits[recipe] == '1' || row.RecipeBits[recipe] == '0')
                {
                    eligible[recipe]++;
                    if (row.RecipeBits[recipe] == '1') inputReady[recipe]++;
                }
            }
            string recipes = string.Join(',', Recipes.Select((recipe, i) => $"{recipe.Id}:{inputReady[i]}/{eligible[i]}-sites-inputs-ready"));
            Fix64 arsenicalBronzeDelivered = _arsenicalBronzeDeliveredByCiv[civ];
            Fix64 crucibleSteelDelivered = _crucibleSteelDeliveredByCiv[civ];
            output.AppendLine($"fief-arms-summary seed={seed} season={season} civ={civ} settlements={rows.Count} forgeSites={rows.Count(row => row.Forge)} smelterSites={rows.Count(row => row.Smelter)} queuedForgeSites={rows.Count(row => row.ForgeQueued)} fighters={fighters} armed={armed} metalArmed={metalArmed} bowArmed={bowArmed} withoutWeapon={withoutWeapon} stock={F(stockTotal)} idleStock={F(idle)} idleStockSites={idleSites} withoutWeaponSites={baseOnlySites} localOverlapSites={overlap} chariots={chariots} chariotCrew={chariotCrew} idleChariots={idleChariots} chariotStockSites={chariotStockSites} chariotCrewSites={chariotCrewSites} engineStock={F(engineStock)} siegeEngines={siegeEngines} cannonStock={F(cannonStock)} siegeCannons={siegeCannons} powderStock={F(powderStock)} intermediateDelivered=ABRONZE:{F(arsenicalBronzeDelivered)},CSTEEL:{F(crucibleSteelDelivered)} oneBatchInputReadiness={recipes}");
            _arsenicalBronzeDeliveredByCiv[civ] = Fix64.Zero;
            _crucibleSteelDeliveredByCiv[civ] = Fix64.Zero;
        }
    }

    private sealed record SiteRow(SettlementArms Arms, double[] Stock, int[] Carried, bool Forge, bool Smelter, bool ForgeQueued, string RecipeBits,
        Fix64 EngineStock, Fix64 CannonStock, Fix64 PowderStock, int SiegeEngines, int SiegeCannons);

    private static int ProducerCount(LivingEconomyModel economy, int settlement, string producer, int forge)
    {
        if (producer == "forge") return forge;
        if (producer == "smelter") return economy.Metal(settlement).Smelters;
        if (producer is "none" or "hunt") return 1;
        for (int kind = 0; kind < LivingEconomyModel.IronWorkKindCount; kind++)
            if (LivingEconomyModel.IronWorkId(kind) == producer) return economy.IronWorkAt(settlement, kind);
        return 0;
    }

    private static string RecipeState(Recipe recipe, LivingEconomyModel economy, ResearchSystem research, int settlement, int civ, int forge)
    {
        bool eligible = ProducerCount(economy, settlement, recipe.Producer, forge) > 0 && recipe.Projects.All(project => IsDone(research, civ, project));
        if (!eligible) return "-";
        bool ready = true;
        foreach ((string[] alternatives, double amount) in recipe.Inputs.Zip(recipe.Quantities))
            if (!alternatives.Any(good => economy.GoodStock(settlement, good).ToDoubleForDisplay() >= amount)) ready = false;
        return ready ? "1" : "0";
    }

    private static bool IsDone(ResearchSystem research, int civ, string project)
    {
        int index = research.Catalog.IndexOf(project);
        return index >= 0 && research.IsDone(civ, index);
    }

    private static string ForgeQueueKey(BuildItem? item)
    {
        if (item is null) return "none";
        string shortageKinds = string.Join(',', item.Needs.Where(need => need.Short)
            .Select(need => LivingEconomyModel.StoreSlotId(need.Slot)));
        return $"{item.Status}|{item.Gate.Cause}|{item.Gate.Why}|{item.Gate.Project}|{shortageKinds}";
    }

    private static string Key(string value) => value.Replace("G-", "", StringComparison.Ordinal).Replace('-', '_');
    private static string Bit(bool value) => value ? "1" : "0";
    private static string F(Fix64 value) => F(value.ToDoubleForDisplay());
    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
