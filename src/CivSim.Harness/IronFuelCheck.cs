using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Harness;

internal static class IT6FuelGate
{
    public static readonly Fix64 MaxShortRate = Fix64.FromRatio(5, 100);
    public static readonly Fix64 MaxUnmetPlanShare = Fix64.FromRatio(1, 100);
    public const int MinDemandSeasons = 12;
}

/// <summary>
/// The fuel check of docs/MVP/25 I-T6 on the live economy: "sustainable metal is not more than 15% of civilian work in steady state, and
/// no mandatory clear-cut", measured on one-settlement fixtures in the Iron chapter against the same settlement in the Bronze chapter,
/// with a low-wood start, a start without Coppicing, and the loss of the iron supply (the import-loss stress).
/// <para>
/// Read-only tooling: it builds fixtures and runs the economy, changes nothing in the simulation, and its output is a pure function of its
/// arguments. A fixture is one fixed-population settlement with the live E3 age mix, a forge, kiln, smelter and an iron camp site four tiles
/// out on own land, topped up with grain each season so food never binds. It is therefore a test of fuel and labor, not of a whole match.
/// </para>
/// </summary>
internal static class IronFuelCheck
{
    /// <summary>I-T6's ceiling on the share of civilian work the metal chain may take.</summary>
    private static readonly Fix64 ShareCeiling = Fix64.FromRatio(15, 100);
    private sealed record Scenario(string Name, bool Iron, bool Coppice, int ForestTiles, bool LoseIronAtMidpoint,
        bool Carts = false, bool IronHubs = false, bool Gov04Sharing = false, bool T12 = false,
        FuelForestPlacement? SpatialPlacement = null, bool MixedT12 = false, bool Fief = false, bool Furnace = false);

    private static readonly Scenario[] Scenarios =
    {
        new("bronze-reference", false, true, 13, false),
        new("iron", true, true, 13, false),
        new("iron-low-wood", true, true, 4, false),
        new("iron-hub-low-wood", true, true, 4, false, IronHubs: true),
        new("iron-hub-low-wood-no-coppice", true, false, 4, false, IronHubs: true),
        new("iron-hub-gov04-low-wood", true, true, 4, false, IronHubs: true, Gov04Sharing: true),
        new("iron-hub-import-loss", true, true, 13, true, IronHubs: true),
        new("iron-hub-low-wood-t12", true, true, 4, false, IronHubs: true, T12: true),
        new("iron-hub-low-wood-mixed-t12", true, true, 4, false, IronHubs: true, MixedT12: true),
        new("iron-low-wood-carts", true, true, 4, false, Carts: true),
        new("iron-low-wood-no-coppice-carts", true, false, 4, false, Carts: true),
        new("bronze-low-wood-carts", false, true, 4, false, Carts: true),
        new("bronze-low-wood-no-coppice-carts", false, false, 4, false, Carts: true),
        new("iron-no-coppice", true, false, 13, false),
        new("bronze-no-coppice", false, false, 13, false),
        new("iron-import-loss", true, true, 13, true),
        new("bronze-low-wood", false, true, 4, false),
        new("iron-low-wood-no-coppice", true, false, 4, false),
        new("bronze-low-wood-no-coppice", false, false, 4, false),
        new("iron-wood-rich", true, true, 30, false),
        new("bronze-wood-rich", false, true, 30, false),
        new("iron-near-8", true, false, 4, false, SpatialPlacement: FuelForestPlacement.Nearby),
        new("iron-haul-8", true, false, 4, false, SpatialPlacement: FuelForestPlacement.HaulOnly),
        new("iron-far-8", true, false, 4, false, SpatialPlacement: FuelForestPlacement.CartOnly),
        new("iron-near-12", true, false, 4, false, Carts: true, SpatialPlacement: FuelForestPlacement.Nearby),
        new("iron-haul-12", true, false, 4, false, Carts: true, SpatialPlacement: FuelForestPlacement.HaulOnly),
        new("iron-far-12", true, false, 4, false, Carts: true, SpatialPlacement: FuelForestPlacement.CartOnly),
    };

    private static readonly Scenario[] FiefScenarios =
    {
        new("fief-low-wood-furnace", true, true, 4, false, Fief: true, Furnace: true),
        new("fief-low-wood-no-furnace", true, true, 4, false, Fief: true, Furnace: false),
    };

    private sealed class Row
    {
        public Fix64 Share, CharcoalWoodPerSeason, WoodStart, WoodMid, WoodEnd, IronKitShareAt12, IronKitShareAfter12Route,
            IronRouteWoodRetention, IronKitShare14To44AtRoute, IronKitShare45PlusAtRoute, IronKitShare, FuelShortShare, Bloom, Wrought;
        public Fix64 Kit11Issued, Kit12Issued, Kit11ToolWork, Kit12ToolWork;
        public Fix64 MaxTwelveSeasonShare;
        public Fix64 MaxTwelveSeasonLabor, MaxTwelveSeasonMetalLabor;
        public int MaxTwelveSeasonEnd;
        public int TwelveSeasonWindows;
        public int MatureTwelveSeasonCandidates;
        public int TwelveSeasonNoExposureWindows;
        public Fix64 FirstTwelveSeasonShare, FirstTwelveSeasonLabor, FirstTwelveSeasonMetalLabor;
        public int FirstTwelveSeasonEnd;
        public Fix64 MaxLateTwelveSeasonShare, MaxLateTwelveSeasonLabor, MaxLateTwelveSeasonMetalLabor;
        public int MaxLateTwelveSeasonEnd, LateTwelveSeasonWindows;
        public int Kit11Breaks, Kit12Breaks;
        public int IronEraFuelDemandSeasons, IronEraFuelShortSeasons;
        public Fix64 IronEraCharcoalPlan, IronEraCharcoalUnmet;
        public Fix64 ForestRegrowth, MetalCharcoalWood, MetalStandingRemoval, TotalStandingRemoval, WoodBalanceResidual;
        public Fix64 FiefBloomeryIronProduced, FiefWroughtProduced, FiefOreMined;
        public int IronCampBuiltSeason, IronToolsUnlockedSeason, IronRouteActivatedSeason, IronFirstKitSeason;
        public int ForestClearThresholdCrossings;
        public int T12UnlockSeason;
        public int T11ToT12Upgrades;
        public IT3WearDistribution? WearAtRoute12, WearAfterT12Unlock, WearAtEnd;
        public readonly List<string> FiefSeasonTrace = new();
        public readonly List<string> FiefT3Windows = new();
        public readonly List<bool> FiefNoFuelBySeason = new();
        public Fix64 FurnaceCharcoalFactor;
        public ulong Seed;
    }

    private sealed record ForestTrace(Fix64 Standing, Fix64 Regrowth, Fix64 MetalRemoved,
        Fix64 ChopRemoved, Fix64 CivicRemoved, Fix64 OtherRemoved, int ClearThresholdCrossings)
    {
        public Fix64 TotalRemoved => MetalRemoved + ChopRemoved + CivicRemoved + OtherRemoved;
    }

    public static int Run(string[] args)
    {
        string? packPath = null;
        var seeds = new List<ulong> { 20261019, 20261020, 20261021, 20261022, 20261023, 20261024, 20261025, 20261026 };
        int seasons = 40, people = 60;
        bool seasonsSpecified = false;
        bool peopleSpecified = false;
        bool spatialOnly = false;
        bool it3Wear = false;
        bool fief = false;
        bool traceFiefSeasons = false;
        var furnaceCharcoalCandidates = new List<Fix64>();
        string? scenarioName = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--spatial-only": spatialOnly = true; break;
                case "--it3-wear": it3Wear = true; break;
                case "--fief": fief = true; break;
                case "--trace-fief-seasons": traceFiefSeasons = true; break;
                case "--furnace-charcoal-candidates":
                    foreach (string value in args[++i].Split(','))
                    {
                        Fix64 factor = Fix64.Parse(value);
                        if (factor < Fix64.One || factor >= Fix64.FromInt(2))
                            throw new ArgumentException("Furnace charcoal candidate factors must be at least 1 and less than 2.");
                        furnaceCharcoalCandidates.Add(factor);
                    }
                    break;
                case "--scenario": scenarioName = args[++i]; break;
                case "--seeds":
                    seeds.Clear();
                    foreach (string part in args[++i].Split(',')) seeds.Add(ulong.Parse(part, CultureInfo.InvariantCulture));
                    break;
                case "--seasons": seasons = int.Parse(args[++i], CultureInfo.InvariantCulture); seasonsSpecified = true; break;
                case "--people": people = int.Parse(args[++i], CultureInfo.InvariantCulture); peopleSpecified = true; break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("iron-fuel needs --pack FILE (civsim build-pack --out FILE).");
        if (traceFiefSeasons && !fief) throw new ArgumentException("--trace-fief-seasons requires --fief.");
        if (furnaceCharcoalCandidates.Count > 0 && !fief)
            throw new ArgumentException("--furnace-charcoal-candidates requires --fief.");
        if (fief && !peopleSpecified) people = 200; // A mature Fief smelting town avoids startup-only camp overhead dominating the work-share gate.
        if (it3Wear && !seasonsSpecified) seasons = 240;
        else if (fief && !seasonsSpecified) seasons = 64;
        if (seasons < 8 || seasons % 2 != 0) throw new ArgumentException("--seasons must be an even number of at least 8.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        if (furnaceCharcoalCandidates.Count == 0)
            Console.Write(Report(pack, seeds, seasons, people, spatialOnly: spatialOnly, scenarioName: scenarioName,
                it3Wear: it3Wear, fief: fief, traceFiefSeasons: traceFiefSeasons));
        else
            foreach (Fix64 factor in furnaceCharcoalCandidates)
                Console.Write(Report(pack, seeds, seasons, people, spatialOnly: spatialOnly, scenarioName: scenarioName,
                    it3Wear: it3Wear, fief: fief, traceFiefSeasons: traceFiefSeasons, furnaceCharcoalFactor: factor));
        return 0;
    }

    internal static string Report(ContentPack pack, IReadOnlyList<ulong> seeds, int seasons, int people,
        int scenarioLimit = int.MaxValue, bool spatialOnly = false, string? scenarioName = null, bool it3Wear = false,
        bool fief = false, bool traceFiefSeasons = false, Fix64? furnaceCharcoalFactor = null)
    {
        Scenario[] selectedScenarios = (fief ? FiefScenarios : Scenarios)
            .Where(sc => scenarioName is null || string.Equals(sc.Name, scenarioName, StringComparison.OrdinalIgnoreCase))
            .Where(sc => !sc.MixedT12 || it3Wear)
            .Where(sc => !it3Wear || sc.Name is "iron-hub-low-wood-t12" or "iron-hub-low-wood-mixed-t12")
            .ToArray();
        var sb = new StringBuilder();
        void L(string text = "") => sb.Append(text).Append('\n');
        int window = seasons / 2;
        L($"iron-fuel{(fief ? " --fief" : spatialOnly ? " --spatial-only" : "")}: {seeds.Count} seeds, {seasons} seasons ({window}-season steady-state window), fixed {people}-person fixture settlement");
        if (fief)
            L($"Fief furnace charcoal factor at full powered coverage: {(furnaceCharcoalFactor?.ToDoubleForDisplay() ?? 1d):F2}; batch output, ore input, bloomery labor multiplier, power allocation, and denominator formulas unchanged. Actual cumulative production may respond to fuel availability.");
        L(fief
            ? $"F-T3: Fief furnace versus matched no-effect control on low wood; 12-season work share <= {Pct(ShareCeiling)}, fuel short <= 5%, no furnace-attributed clearing."
            : spatialOnly
            ? $"I-T6 spatial supply/reach counterfactual; apply the approved per-seed fuel cutoff (work-share ceiling {Pct(ShareCeiling)})."
            : $"I-T6: metal chain <= {Pct(ShareCeiling)} of civilian work; compare forest stock and fuel flows with the same-seed Bronze control");
        L("spatial controls: four matched 30 L forest tiles (120 L opening stock; no Coppicing; up to 2 L/season base regrowth below the per-tile cap) placed at radii 1–3 (near), 4–8 (haul), or 9–12 (far); other wood within radius 12 is removed. Reach 8 is default; TRN-05 gives reach 12 and its lower haul cost.");
        L("Iron hub controls: KEY-4 enables catchment-aware hubs; existing SOC-07 sharing reaches 15 tiles, GOV-04 reaches 20, and Iron physical charcoal hauling extends 8 tiles beyond that domestic reach.");
        L();
        L("scenario            seeds  metal-share(mean/max)  char+fire/season  regrowth/season  all-cuts/season  mass-resid  metal-char/season  metal-cut/season  reachable wood end/start(mean/min)  end/mid  NoFuel fallback(mean/max)  T11+ at fixture12/12 after route/end  bloom+wrought");
        var workVerdicts = new List<string>();
        var rowsByScenario = new Dictionary<string, Dictionary<ulong, Row>>(StringComparer.Ordinal);
        int shown = 0;
        foreach (Scenario sc in selectedScenarios)
        {
            if (spatialOnly && sc.SpatialPlacement is null) continue;
            if (shown++ >= scenarioLimit) break;
            var rows = new List<Row>();
            int skipped = 0;
            foreach (ulong seed in seeds)
            {
                try { rows.Add(RunOne(pack, seed, sc, seasons, people, traceFiefSeasons, furnaceCharcoalFactor)); }
                catch (InvalidOperationException) { skipped++; } // the fixture needs 16 land tiles in range and land four tiles out
            }
            if (rows.Count == 0) { L($"{sc.Name,-19} no usable seeds"); continue; }
            var rowsForSeed = new Dictionary<ulong, Row>();
            foreach (Row row in rows) rowsForSeed[row.Seed] = row;
            rowsByScenario[sc.Name] = rowsForSeed;
            Fix64 meanShare = Mean(rows, r => r.Share), maxShare = Max(rows, r => r.Share);
            Fix64 meanWood = Mean(rows, r => r.CharcoalWoodPerSeason);
            Fix64 meanRegrowth = Mean(rows, r => r.ForestRegrowth);
            Fix64 meanAllCuts = Mean(rows, r => r.TotalStandingRemoval);
            Fix64 meanResidual = Mean(rows, r => r.WoodBalanceResidual);
            Fix64 meanMetalCharcoal = Mean(rows, r => r.MetalCharcoalWood);
            Fix64 meanMetalCut = Mean(rows, r => r.MetalStandingRemoval);
            Fix64 meanRatio = Mean(rows, r => Ratio(r.WoodEnd, r.WoodStart)), minRatio = Min(rows, r => Ratio(r.WoodEnd, r.WoodStart));
            Fix64 meanSteady = Mean(rows, r => Ratio(r.WoodEnd, r.WoodMid));
            Fix64 meanShort = Mean(rows, r => r.FuelShortShare), maxShort = Max(rows, r => r.FuelShortShare);
            Fix64 meanKitsAt12 = Mean(rows, r => r.IronKitShareAt12);
            Fix64 meanKitsAfter12Route = Mean(rows, r => r.IronKitShareAfter12Route);
            Fix64 meanKits = Mean(rows, r => r.IronKitShare);
            Fix64 meanBloom = Mean(rows, r => r.Bloom + r.Wrought);
            L($"{sc.Name,-19} {rows.Count + (skipped > 0 ? "/" + seeds.Count : ""),5}  {Pct(meanShare),7} / {Pct(maxShare),-7}       {meanWood.ToDoubleForDisplay(),5:F2} L       {meanRegrowth.ToDoubleForDisplay(),5:F2} L       {meanAllCuts.ToDoubleForDisplay(),5:F2} L      {meanResidual.ToDoubleForDisplay(),6:F3} L       {meanMetalCharcoal.ToDoubleForDisplay(),5:F2} L         {meanMetalCut.ToDoubleForDisplay(),5:F2} L          {meanRatio.ToDoubleForDisplay(),5:F2} / {minRatio.ToDoubleForDisplay(),-5:F2}        {meanSteady.ToDoubleForDisplay(),5:F2}     {Pct(meanShort),5}/{Pct(maxShort),-5}      {Pct(meanKitsAt12),4}/{Pct(meanKitsAfter12Route),4}/{Pct(meanKits),4}     {meanBloom.ToDoubleForDisplay():F2}");
            if (sc.Fief && sc.Furnace)
                foreach (Row row in rows.OrderBy(r => r.Seed))
                {
                    int first = Math.Max(1, row.MaxTwelveSeasonEnd - 11);
                    int noFuel = row.FiefNoFuelBySeason.Skip(first - 1).Take(12).Count(value => value);
                    L($"  T3 candidate seed={row.Seed} factor={row.FurnaceCharcoalFactor.ToDoubleForDisplay():F2}; " +
                      $"max-window s{first}–{row.MaxTwelveSeasonEnd} work={Pct(row.MaxTwelveSeasonShare)} " +
                      $"({row.MaxTwelveSeasonMetalLabor.ToDoubleForDisplay():F2}/{row.MaxTwelveSeasonLabor.ToDoubleForDisplay():F2}); " +
                      $"NoFuel={noFuel}/12 ({Pct(Fix64.FromRatio(noFuel, 12))}); full-run forest crossings={row.ForestClearThresholdCrossings}; " +
                      $"late-window material residual={row.WoodBalanceResidual.ToDoubleForDisplay():F3} L; " +
                      $"cumulative bloomery iron/wrought output={row.FiefBloomeryIronProduced.ToDoubleForDisplay():F2}/{row.FiefWroughtProduced.ToDoubleForDisplay():F2}; " +
                      $"ore mined={row.FiefOreMined.ToDoubleForDisplay():F2} L; " +
                      $"final iron/wrought stock={row.Bloom.ToDoubleForDisplay():F2}/{row.Wrought.ToDoubleForDisplay():F2}");
                }
            bool shareOk = maxShare <= ShareCeiling;
            workVerdicts.Add($"{sc.Name}: work share {(shareOk ? "within" : "ABOVE")} {Pct(ShareCeiling)} on every seed (max {Pct(maxShare)})");
            if (traceFiefSeasons && sc.Fief)
            {
                L();
                foreach (Row traced in rows.OrderBy(row => row.Seed))
                {
                    foreach (string windowLine in traced.FiefT3Windows) L(windowLine);
                    L($"F_T3_ARM scenario={sc.Name} seed={traced.Seed} first_kit_season={traced.IronFirstKitSeason} " +
                      $"mature_candidates={traced.MatureTwelveSeasonCandidates} measured_windows={traced.TwelveSeasonWindows} " +
                      $"no_exposure_windows={traced.TwelveSeasonNoExposureWindows} fuel_demand_seasons={traced.IronEraFuelDemandSeasons} " +
                      $"fuel_short_seasons={traced.IronEraFuelShortSeasons} fuel_short_rate_raw={FuelShortRate(traced).Raw} " +
                      $"planned_charcoal_raw={traced.IronEraCharcoalPlan.Raw} unmet_charcoal_raw={traced.IronEraCharcoalUnmet.Raw} " +
                      $"fuel_unmet_share_raw={FuelUnmetShare(traced).Raw} no_fuel_share_raw={traced.FuelShortShare.Raw} " +
                      $"forest_clear_crossings={traced.ForestClearThresholdCrossings} wood_balance_residual_raw={traced.WoodBalanceResidual.Raw}");
                }
                L();
                L($"Fief seasonal causal trace ({sc.Name}; full-coverage charcoal factor={rows[0].FurnaceCharcoalFactor.ToDoubleForDisplay():F2}): seed season labor=total/metal; assigned mine/fuel/smelt/camp; idle subtraction; actual bloomery labor; explicit fuel/ore haul; furnace-active craft-coverage; bloomery/wrought outputs; stocks start->end (ore,charcoal,iron,wrought,steel,T11,T12)");
                foreach (Row traced in rows.OrderBy(row => row.Seed))
                    foreach (string traceLine in traced.FiefSeasonTrace) L($"  {traceLine}");
            }
        }
        L();
        L("I-T6 planned-charcoal shortfall (final half; demand-seasons count only seasons with a positive charcoal-production plan after existing charcoal stock):");
        L($"approved per-seed cutoff: shortfall rate <= {Pct(IT6FuelGate.MaxShortRate)} and unmet / planned charcoal <= {Pct(IT6FuelGate.MaxUnmetPlanShare)}; fewer than {IT6FuelGate.MinDemandSeasons} demand-seasons is censored");
        L("scenario                  plan demand seasons mean/max  shortfall rate mean/max  planned charcoal mean L/seed  unmet mean L/seed  unmet share mean/max");
        foreach (Scenario sc in selectedScenarios)
        {
            if (!sc.Iron || !rowsByScenario.TryGetValue(sc.Name, out Dictionary<ulong, Row>? fuelRows)) continue;
            var rows = new List<Row>(fuelRows.Values);
            Fix64 meanShortRate = Mean(rows, FuelShortRate), maxShortRate = Max(rows, FuelShortRate);
            Fix64 meanUnmetShare = Mean(rows, FuelUnmetShare), maxUnmetShare = Max(rows, FuelUnmetShare);
            L($"{sc.Name,-25} {Mean(rows, r => Fix64.FromInt(r.IronEraFuelDemandSeasons)).ToDoubleForDisplay(),6:F1}/{Max(rows, r => Fix64.FromInt(r.IronEraFuelDemandSeasons)).ToDoubleForDisplay(),-6:F0}         " +
              $"{Pct(meanShortRate),6}/{Pct(maxShortRate),-6}        {Mean(rows, r => r.IronEraCharcoalPlan).ToDoubleForDisplay(),8:F2}            " +
              $"{Mean(rows, r => r.IronEraCharcoalUnmet).ToDoubleForDisplay(),8:F2}       {Pct(meanUnmetShare),6}/{Pct(maxUnmetShare),-6}");
            int fuelPass = 0, fuelCensored = 0;
            foreach (Row row in rows)
            {
                if (row.IronEraFuelDemandSeasons < IT6FuelGate.MinDemandSeasons) fuelCensored++;
                else if (FuelShortRate(row) <= IT6FuelGate.MaxShortRate && FuelUnmetShare(row) <= IT6FuelGate.MaxUnmetPlanShare) fuelPass++;
                else L($"  cutoff miss seed {row.Seed}: short {row.IronEraFuelShortSeasons}/{row.IronEraFuelDemandSeasons} ({Pct(FuelShortRate(row))}); " +
                      $"unmet {row.IronEraCharcoalUnmet.ToDoubleForDisplay():F4}/{row.IronEraCharcoalPlan.ToDoubleForDisplay():F4} L ({Pct(FuelUnmetShare(row))})");
            }
            L($"  per-seed cutoff: {fuelPass}/{rows.Count - fuelCensored} eligible pass; {fuelCensored} censored");
        }
        L();
        L("I-T6 tool service and replacement activity (full fixed-population fixture run):");
        L("scenario                         T11 issued mean  tool-ws/kit  breaks (seeds, total, max)   T12 issued mean  tool-ws/kit  breaks (seeds, total, max)");
        foreach (Scenario sc in selectedScenarios)
        {
            if (!sc.Iron || !rowsByScenario.TryGetValue(sc.Name, out Dictionary<ulong, Row>? toolRows)) continue;
            var rows = new List<Row>(toolRows.Values);
            Fix64 issued11 = Fix64.Zero, issued12 = Fix64.Zero, toolWork11 = Fix64.Zero, toolWork12 = Fix64.Zero;
            int breakSeeds11 = 0, breakSeeds12 = 0, totalBreaks11 = 0, totalBreaks12 = 0, maxBreaks11 = 0, maxBreaks12 = 0;
            foreach (Row row in rows)
            {
                issued11 += row.Kit11Issued; issued12 += row.Kit12Issued;
                toolWork11 += row.Kit11ToolWork; toolWork12 += row.Kit12ToolWork;
                if (row.Kit11Breaks > 0) breakSeeds11++;
                if (row.Kit12Breaks > 0) breakSeeds12++;
                totalBreaks11 += row.Kit11Breaks; totalBreaks12 += row.Kit12Breaks;
                maxBreaks11 = Math.Max(maxBreaks11, row.Kit11Breaks);
                maxBreaks12 = Math.Max(maxBreaks12, row.Kit12Breaks);
            }
            Fix64 toolRate11 = issued11 > Fix64.Zero ? toolWork11 / issued11 : Fix64.Zero;
            Fix64 toolRate12 = issued12 > Fix64.Zero ? toolWork12 / issued12 : Fix64.Zero;
            L($"{sc.Name,-32} {Mean(rows, r => r.Kit11Issued).ToDoubleForDisplay(),7:F1}       {toolRate11.ToDoubleForDisplay(),6:F2}    ({breakSeeds11}/{rows.Count}, {totalBreaks11}, {maxBreaks11})              {Mean(rows, r => r.Kit12Issued).ToDoubleForDisplay(),7:F1}       {toolRate12.ToDoubleForDisplay(),6:F2}    ({breakSeeds12}/{rows.Count}, {totalBreaks12}, {maxBreaks12})");
        }
        L();
        L(fief ? "aggregate whole-run metal work share (context only; not the rolling F-T3 gate):" : "work-share screen:");
        foreach (string v in workVerdicts) L("  " + v);
        L();
        L("Forest clearing diagnostic (tile crossings below the simulation's 5 L cleared-woodland threshold, full run):");
        L("scenario                       seeds with crossings  total tile crossings  max in one seed");
        foreach (Scenario sc in selectedScenarios)
        {
            if (!rowsByScenario.TryGetValue(sc.Name, out Dictionary<ulong, Row>? clearRows)) continue;
            int seedsWithCrossings = 0, totalCrossings = 0, maxCrossings = 0;
            foreach (Row row in clearRows.Values)
            {
                if (row.ForestClearThresholdCrossings > 0) seedsWithCrossings++;
                totalCrossings += row.ForestClearThresholdCrossings;
                maxCrossings = Math.Max(maxCrossings, row.ForestClearThresholdCrossings);
            }
            L($"{sc.Name,-30} {seedsWithCrossings,3}/{clearRows.Count,-14} {totalCrossings,8} {maxCrossings,19}");
        }
        if (fief && rowsByScenario.TryGetValue("fief-low-wood-furnace", out Dictionary<ulong, Row>? furnaceRows) &&
            rowsByScenario.TryGetValue("fief-low-wood-no-furnace", out Dictionary<ulong, Row>? controlRows))
        {
            int paired = 0, furnaceAttributedCrossings = 0, workShareMisses = 0, fuelShortMisses = 0, matureWindowMisses = 0;
            int lateWindowMisses = 0, lateWorkShareMisses = 0;
            Fix64 maxTwelveSeasonShare = Fix64.Zero, maxFuelShort = Fix64.Zero;
            Fix64 maxLateTwelveSeasonShare = Fix64.Zero;
            Row? worstWorkShare = null;
            var immatureRows = new List<Row>();
            foreach (KeyValuePair<ulong, Row> pair in furnaceRows)
            {
                if (!controlRows.TryGetValue(pair.Key, out Row? control)) continue;
                Row furnace = pair.Value;
                paired++;
                if (furnace.TwelveSeasonWindows == 0)
                {
                    matureWindowMisses++;
                    immatureRows.Add(furnace);
                }
                if (furnace.LateTwelveSeasonWindows == 0) lateWindowMisses++;
                else
                {
                    maxLateTwelveSeasonShare = Fix64.Max(maxLateTwelveSeasonShare, furnace.MaxLateTwelveSeasonShare);
                    if (furnace.MaxLateTwelveSeasonShare > ShareCeiling) lateWorkShareMisses++;
                }
                int extraCrossings = Math.Max(0, furnace.ForestClearThresholdCrossings - control.ForestClearThresholdCrossings);
                furnaceAttributedCrossings += extraCrossings;
                maxTwelveSeasonShare = Fix64.Max(maxTwelveSeasonShare, furnace.MaxTwelveSeasonShare);
                if (worstWorkShare is null || furnace.MaxTwelveSeasonShare > worstWorkShare.MaxTwelveSeasonShare) worstWorkShare = furnace;
                maxFuelShort = Fix64.Max(maxFuelShort, furnace.FuelShortShare);
                if (furnace.MaxTwelveSeasonShare > ShareCeiling) workShareMisses++;
                if (furnace.FuelShortShare > Fix64.FromRatio(5, 100)) fuelShortMisses++;
            }
            L();
            L("F-T3 low-wood furnace gate (paired by seed; work share is the maximum of every rolling 12-season window after first kit output plus 12 seasons):");
            int matureEligible = paired - matureWindowMisses;
            int maturePasses = matureEligible - workShareMisses;
            int lateEligible = paired - lateWindowMisses;
            int latePasses = lateEligible - lateWorkShareMisses;
            L($"  mature-window result at unchanged {Pct(ShareCeiling)} ceiling: {maturePasses}/{matureEligible} eligible seed pairs pass; {workShareMisses} fail; {matureWindowMisses} censored (no mature window, not counted as a pass)");
            L($"  maximum mature 12-season work share: {Pct(maxTwelveSeasonShare)} ({(maxTwelveSeasonShare > ShareCeiling ? (maxTwelveSeasonShare - ShareCeiling).ToDoubleForDisplay() * 100 : 0):F1} percentage points above ceiling)");
            L($"  wholly late windows (end season >= {seasons / 2 + 12}): {latePasses}/{lateEligible} eligible seed pairs pass; {lateWorkShareMisses} fail; {lateWindowMisses} censored; maximum {Pct(maxLateTwelveSeasonShare)}");
            foreach (Row immature in immatureRows)
                L($"  censored seed {immature.Seed}: route season {immature.IronRouteActivatedSeason}, first kit season {immature.IronFirstKitSeason}, charcoal-demand seasons {immature.IronEraFuelDemandSeasons}");
            foreach (Row row in furnaceRows.Values.OrderBy(row => row.Seed))
                L($"  window phases seed {row.Seed}: first eligible=" +
                  $"{(row.TwelveSeasonWindows > 0 ? Pct(row.FirstTwelveSeasonShare) : "n/a")} end={row.FirstTwelveSeasonEnd}; " +
                  $"late max={(row.LateTwelveSeasonWindows > 0 ? Pct(row.MaxLateTwelveSeasonShare) : "n/a")} " +
                  $"end={row.MaxLateTwelveSeasonEnd} ({row.LateTwelveSeasonWindows} windows)");
            foreach (Row miss in furnaceRows.Values
                         .Where(row => row.TwelveSeasonWindows > 0 && row.MaxTwelveSeasonShare > ShareCeiling)
                         .OrderBy(row => row.Seed))
                L($"  work-share miss seed {miss.Seed}: {Pct(miss.MaxTwelveSeasonShare)} in seasons " +
                  $"{miss.MaxTwelveSeasonEnd - 11}–{miss.MaxTwelveSeasonEnd}; " +
                  $"{miss.MaxTwelveSeasonMetalLabor.ToDoubleForDisplay():F2}/{miss.MaxTwelveSeasonLabor.ToDoubleForDisplay():F2} metal/civilian work");
            if (worstWorkShare is { } worst)
            {
                Fix64 extraNonMetalLaborToCeiling = Fix64.Max(Fix64.Zero,
                    worst.MaxTwelveSeasonMetalLabor / ShareCeiling - worst.MaxTwelveSeasonLabor);
                L($"  worst window: seed {worst.Seed}, seasons {worst.MaxTwelveSeasonEnd - 11}–{worst.MaxTwelveSeasonEnd}; {worst.MaxTwelveSeasonMetalLabor.ToDoubleForDisplay():F2}/{worst.MaxTwelveSeasonLabor.ToDoubleForDisplay():F2} metal/civilian work; route season {worst.IronRouteActivatedSeason}, first kit season {worst.IronFirstKitSeason}");
                if (worst.MaxTwelveSeasonShare > ShareCeiling)
                    L($"  worst-window gap: at least {extraNonMetalLaborToCeiling.ToDoubleForDisplay():F2} additional non-metal work in that 12-season window would be needed to reach {Pct(ShareCeiling)} with metal work unchanged");
            }
            L($"  maximum final-half NoFuel share: {Pct(maxFuelShort)}; seeds over 5%: {fuelShortMisses}");
            L($"  furnace-attributed clearing crossings: {furnaceAttributedCrossings} (extra crossings with furnace effect on versus off)");
        }
        L();
        if (!spatialOnly)
        {
            L("I-T2 sustainable-route checkpoint (12 complete seasons after camp and MET-09 are both available):");
            L("Coverage counts all adult kit holders as a conservative proxy; the proposed I-T2 denominator is tool workers.");
            L("scenario                    MET-09/camp/first-kit seasons   all-adult T11+ mean/min/max  seeds >=70%  T11+ age14-44/45+  route-wood mean/min");
        }
        foreach (string name in new[] { "iron", "iron-low-wood", "iron-low-wood-carts", "iron-low-wood-no-coppice-carts", "iron-no-coppice", "iron-low-wood-no-coppice", "iron-import-loss", "iron-wood-rich" })
        {
            if (!rowsByScenario.TryGetValue(name, out Dictionary<ulong, Row>? routeRows)) continue;
            var rows = new List<Row>(routeRows.Values);
            int passes = 0;
            foreach (Row row in rows) if (row.IronKitShareAfter12Route >= Fix64.FromRatio(70, 100)) passes++;
            L($"{name,-27} " +
              $"{SeasonRange(rows, r => r.IronToolsUnlockedSeason),-5}/{SeasonRange(rows, r => r.IronCampBuiltSeason),-5}/{SeasonRange(rows, r => r.IronFirstKitSeason),-5} " +
              $"{Pct(Mean(rows, r => r.IronKitShareAfter12Route)),5}/{Pct(Min(rows, r => r.IronKitShareAfter12Route)),5}/{Pct(Max(rows, r => r.IronKitShareAfter12Route)),5}             {passes}/{rows.Count}         " +
              $"{Pct(Mean(rows, r => r.IronKitShare14To44AtRoute)),5}/{Pct(Mean(rows, r => r.IronKitShare45PlusAtRoute)),5}           " +
              $"{Mean(rows, r => r.IronRouteWoodRetention).ToDoubleForDisplay(),4:F2}/{Min(rows, r => r.IronRouteWoodRetention).ToDoubleForDisplay(),4:F2}");
            foreach (Row row in rows)
                if (row.IronKitShareAfter12Route < Fix64.FromRatio(70, 100))
                    L($"  miss seed {row.Seed}: MET-09 s{row.IronToolsUnlockedSeason}, camp s{row.IronCampBuiltSeason}, first T11 held s{row.IronFirstKitSeason}; " +
                      $"T11+ {Pct(row.IronKitShareAfter12Route)}, age14-44 {Pct(row.IronKitShare14To44AtRoute)}, age45+ {Pct(row.IronKitShare45PlusAtRoute)}, " +
                      $"reachable forest retention {row.IronRouteWoodRetention.ToDoubleForDisplay():F2}");
        }
        if (it3Wear)
        {
            L();
            L("I-T3 live wear distribution (read-only; fraction of tier service remaining; bins are <0, [0,20), [20,40), [40,60), [60,80), >=80%; rows are per seed)");
            L("scenario / seed / checkpoint           T11 bins <0,0-20,20-40,40-60,60-80,80+       T12 bins <0,0-20,20-40,40-60,60-80,80+");
            foreach (Scenario sc in selectedScenarios.Where(x => x.Name is "iron-hub-low-wood-t12" or "iron-hub-low-wood-mixed-t12"))
            {
                if (!rowsByScenario.TryGetValue(sc.Name, out Dictionary<ulong, Row>? wearRows)) continue;
                foreach (Row row in wearRows.Values.OrderBy(x => x.Seed))
                {
                    if (row.WearAtRoute12 is { } routeWear) L($"{sc.Name} / {row.Seed} / route+12                 {FormatWear(routeWear)}");
                    if (row.WearAfterT12Unlock is { } unlockWear) L($"{sc.Name} / {row.Seed} / MET-10+12 seasons (s{row.T12UnlockSeason + 12}) {FormatWear(unlockWear)}");
                    if (row.WearAtEnd is { } endWear) L($"{sc.Name} / {row.Seed} / end (T11-to-T12 upgrades={row.T11ToT12Upgrades}) {FormatWear(endWear)}");
                }
            }
            L("Mixed case preloads 40 T11 kits, grants MET-10 after 120 seasons, and adds 10 T12 kits so the planner exercises upgrades; this is a controlled wear fixture, not a production-supply result.");
        }
        L();
        if (!spatialOnly) L("paired Iron-minus-Bronze flows over each seed's final half (litres/season; same map and population fixture):");
        foreach ((string iron, string bronze) in new[]
                 {
                     ("iron", "bronze-reference"), ("iron-low-wood", "bronze-low-wood"),
                     ("iron-low-wood-carts", "bronze-low-wood-carts"),
                     ("iron-low-wood-no-coppice-carts", "bronze-low-wood-no-coppice-carts"),
                     ("iron-no-coppice", "bronze-no-coppice"),
                     ("iron-low-wood-no-coppice", "bronze-low-wood-no-coppice"),
                     ("iron-wood-rich", "bronze-wood-rich"),
                 })
        {
            if (!rowsByScenario.TryGetValue(iron, out Dictionary<ulong, Row>? ironRows) ||
                !rowsByScenario.TryGetValue(bronze, out Dictionary<ulong, Row>? bronzeRows)) continue;
            Fix64 deltaFuel = Fix64.Zero, deltaRemoval = Fix64.Zero, deltaAllCuts = Fix64.Zero, deltaRegrowth = Fix64.Zero, deltaWood = Fix64.Zero;
            Fix64 deltaForestClearings = Fix64.Zero;
            int paired = 0;
            foreach ((ulong seed, Row ir) in ironRows)
            {
                if (!bronzeRows.TryGetValue(seed, out Row? br)) continue;
                deltaFuel += ir.MetalCharcoalWood - br.MetalCharcoalWood;
                deltaRemoval += ir.MetalStandingRemoval - br.MetalStandingRemoval;
                deltaAllCuts += ir.TotalStandingRemoval - br.TotalStandingRemoval;
                deltaRegrowth += ir.ForestRegrowth - br.ForestRegrowth;
                deltaWood += Ratio(ir.WoodEnd, ir.WoodStart) - Ratio(br.WoodEnd, br.WoodStart);
                deltaForestClearings += Fix64.FromInt(ir.ForestClearThresholdCrossings - br.ForestClearThresholdCrossings);
                paired++;
            }
            if (paired == 0) continue;
            L($"  {iron,-19} vs {bronze,-19} n={paired}: metal-charcoal wood {(deltaFuel / paired).ToDoubleForDisplay():F2} L/season; " +
              $"standing metal removal {(deltaRemoval / paired).ToDoubleForDisplay():F2}, all standing cuts {(deltaAllCuts / paired).ToDoubleForDisplay():F2}; " +
              $"regrowth {(deltaRegrowth / paired).ToDoubleForDisplay():F2}; " +
              $"clear-threshold crossings {Signed(deltaForestClearings / paired)} /seed; " +
              $"end/start wood delta {(deltaWood / paired).ToDoubleForDisplay():F2}");
        }
        L();
        if (!spatialOnly) L("paired Coppicing effect (Coppicing minus no-Coppicing, same seeds):");
        foreach ((string coppiced, string uncoppiced) in new[]
                 {
                     ("iron", "iron-no-coppice"), ("iron-low-wood", "iron-low-wood-no-coppice"),
                     ("iron-low-wood-carts", "iron-low-wood-no-coppice-carts"),
                     ("bronze-reference", "bronze-no-coppice"), ("bronze-low-wood", "bronze-low-wood-no-coppice"),
                     ("bronze-low-wood-carts", "bronze-low-wood-no-coppice-carts"),
                 })
        {
            if (!rowsByScenario.TryGetValue(coppiced, out Dictionary<ulong, Row>? coppicedRows) ||
                !rowsByScenario.TryGetValue(uncoppiced, out Dictionary<ulong, Row>? uncoppicedRows)) continue;
            Fix64 deltaRegrowth = Fix64.Zero, deltaCuts = Fix64.Zero, deltaRetention = Fix64.Zero, deltaClearings = Fix64.Zero;
            Fix64 deltaFuelShort = Fix64.Zero, deltaKits = Fix64.Zero;
            int paired = 0;
            foreach ((ulong seed, Row coppicedRow) in coppicedRows)
            {
                if (!uncoppicedRows.TryGetValue(seed, out Row? uncoppicedRow)) continue;
                deltaRegrowth += coppicedRow.ForestRegrowth - uncoppicedRow.ForestRegrowth;
                deltaCuts += coppicedRow.TotalStandingRemoval - uncoppicedRow.TotalStandingRemoval;
                deltaRetention += Ratio(coppicedRow.WoodEnd, coppicedRow.WoodStart) - Ratio(uncoppicedRow.WoodEnd, uncoppicedRow.WoodStart);
                deltaClearings += Fix64.FromInt(coppicedRow.ForestClearThresholdCrossings - uncoppicedRow.ForestClearThresholdCrossings);
                deltaFuelShort += uncoppicedRow.FuelShortShare - coppicedRow.FuelShortShare;
                deltaKits += coppicedRow.IronKitShare - uncoppicedRow.IronKitShare;
                paired++;
            }
            if (paired == 0) continue;
            L($"  {coppiced,-28} vs {uncoppiced,-32} n={paired}: regrowth {Signed(deltaRegrowth / paired)} L/season; " +
              $"all standing cuts {Signed(deltaCuts / paired)} L/season; " +
              $"end/start stock {Signed(deltaRetention / paired)}; " +
              $"clear-threshold crossings {Signed(deltaClearings / paired)} /seed; " +
              $"NoFuel fallback reduction {Signed(deltaFuelShort / paired * Fix64.FromInt(100))} pp; " +
              $"T11+ holder change {Signed(deltaKits / paired * Fix64.FromInt(100))} pp");
        }
        L();
        L("NoFuel is the settlement's reported metal-method fallback, not a raw count of charcoal shortfalls. all-cuts counts standing-wood removals attributed to metal, chopping, civic and other use across the charcoal haul radius. mass-resid = Δstock − regrowth + all cuts; 0.000 means the traced forest stock balances over the window.");
        L("end/mid is the steady-state slope of standing wood over the window (1.00 = flat; below 1 it is still being drawn down). end/start includes the one-time kit build-out.");
        L("Reading: population and age mix are fixed; grain is topped up each season, and forge/kiln/smelter start built. This isolates fuel and labor, not a whole match.");
        if (!spatialOnly) L("The Bronze reference has the same iron site but makes no iron (the chain is Iron-chapter only), so the difference between it and 'iron' is the Iron chain's cost.");
        return sb.ToString();
    }

    private static Row RunOne(ContentPack pack, ulong seed, Scenario sc, int seasons, int people, bool traceFiefSeasons,
        Fix64? furnaceCharcoalFactor)
    {
        var projects = new List<string> { "MET-08", "MET-09" };
        if (sc.Carts) projects.Add("TRN-05");
        if (sc.IronHubs)
        {
            projects.Add("KEY-4");
            if (sc.Gov04Sharing) projects.Add("GOV-04");
        }
        if (sc.T12) projects.Add("MET-10");
        if (sc.Fief) projects.AddRange(new[] { "KEY-5", "NRG-03", "MET-12" });
        BronzeFixture f = sc.Fief
            ? BronzeFixture.CreateFixedPopulationFiefFuelScenario(pack, seed, people, sc.Coppice, sc.ForestTiles, projects.ToArray())
            : sc.SpatialPlacement is { } placement
            ? BronzeFixture.CreateFixedPopulationSpatialFuelScenario(pack, seed, people, sc.Iron, sc.Coppice, sc.ForestTiles, placement, projects.ToArray())
            : BronzeFixture.CreateFixedPopulationFuelScenario(pack, seed, people, sc.Iron, sc.Coppice, sc.ForestTiles, projects.ToArray());
        LivingEconomyModel e = f.Economy;
        e.B13WoodTraceEnabled = true;
        e.IT6AccountingTraceEnabled = sc.Iron;
        e.IT6FiefSeasonTraceEnabled = traceFiefSeasons && sc.Fief;
        if (sc.Fief)
        {
            e.FixtureSetIronWork(BronzeFixture.Settlement, "water_mill", 1);
            e.FixtureSetIronWork(BronzeFixture.Settlement, "blast_furnace", 1);
            e.FurnaceEffectEnabled = sc.Furnace;
            if (sc.Furnace && furnaceCharcoalFactor is { } factor)
                e.FurnaceCharcoalFactorOverride = factor;
        }
        if (sc.MixedT12) e.FixtureStock(BronzeFixture.Settlement, "G-TOOL11", Fix64.FromInt(40));
        int window = seasons / 2;
        int charcoalReach = e.FixtureIronFuelCharcoalReach(BronzeFixture.Settlement);
        HashSet<int> outerForestTiles = new();
        ForestTrace woodStart = CaptureReachableWood(e, -1, outerForestTiles, charcoalReach);
        var row = new Row { Seed = seed, WoodStart = woodStart.Standing };
        row.FurnaceCharcoalFactor = furnaceCharcoalFactor ?? Fix64.One;
        MetalStatistics mid = default;
        IT6AccountingSnapshot fuelMid = default;
        ForestTrace woodMid = default!, woodEnd = default!;
        int fuelShort = 0;
        int ironCampBuiltSeason = 0, ironToolsUnlockedSeason = 0, ironRouteActivatedSeason = 0, ironFirstKitSeason = 0;
        MetalStatistics precedingStats = e.MetalStats(0);
        IT6AccountingSnapshot precedingTrace = traceFiefSeasons ? e.CaptureIT6Accounting(0) : default;
        IT6FiefMaterialStocks precedingStocks = traceFiefSeasons ? e.CaptureIT6FiefMaterialStocks(BronzeFixture.Settlement) : default;
        Fix64 twelveSeasonLabor = Fix64.Zero, twelveSeasonMetalLabor = Fix64.Zero;
        Fix64[] seasonLabor = new Fix64[12], seasonMetalLabor = new Fix64[12];
        for (int s = 0; s < seasons; s++)
        {
            if (sc.LoseIronAtMidpoint && s == window && f.IronTile >= 0) e.FixtureExhaust(f.IronTile);
            f.RunSeasons(1);
            MetalStatistics currentStats = e.MetalStats(0);
            if (sc.Fief) row.FiefNoFuelBySeason.Add(e.Metal(BronzeFixture.Settlement).Reason == MethodFallbackReason.NoFuel);
            IT6AccountingSnapshot currentTrace = traceFiefSeasons ? e.CaptureIT6Accounting(0) : default;
            IT6FiefMaterialStocks currentStocks = traceFiefSeasons ? e.CaptureIT6FiefMaterialStocks(BronzeFixture.Settlement) : default;
            if (traceFiefSeasons && sc.Fief)
            {
                row.FiefBloomeryIronProduced += currentTrace.BloomeryIronOutput - precedingTrace.BloomeryIronOutput;
                row.FiefWroughtProduced += currentTrace.ForgeWroughtOutput - precedingTrace.ForgeWroughtOutput;
                row.FiefOreMined += currentTrace.IronOreMined - precedingTrace.IronOreMined;
                (bool furnaceActive, Fix64 craftCoverage) = e.CaptureFiefFurnaceDiagnostic(BronzeFixture.Settlement);
                row.FiefSeasonTrace.Add(FormatFiefSeasonTrace(seed, s + 1, currentStats.Labor - precedingStats.Labor,
                    currentStats.MetalLabor - precedingStats.MetalLabor, currentTrace, precedingTrace,
                    currentStocks, precedingStocks, furnaceActive, craftCoverage, row.FurnaceCharcoalFactor));
            }
            int slot = s % seasonLabor.Length;
            if (s >= seasonLabor.Length)
            {
                twelveSeasonLabor -= seasonLabor[slot];
                twelveSeasonMetalLabor -= seasonMetalLabor[slot];
            }
            seasonLabor[slot] = currentStats.Labor - precedingStats.Labor;
            seasonMetalLabor[slot] = currentStats.MetalLabor - precedingStats.MetalLabor;
            twelveSeasonLabor += seasonLabor[slot];
            twelveSeasonMetalLabor += seasonMetalLabor[slot];
            precedingStats = currentStats;
            precedingTrace = currentTrace;
            precedingStocks = currentStocks;
            if (sc.MixedT12 && s == seasons / 2 - 1)
            {
                e.FixtureComplete(0, "MET-10");
                e.FixtureStock(BronzeFixture.Settlement, "G-TOOL12", Fix64.FromInt(10));
                row.T12UnlockSeason = s + 1;
            }
            if (sc.Iron && ironCampBuiltSeason == 0)
                for (int c = 0; c < e.MiningCampCount; c++)
                {
                    MiningCampView view = e.MiningCamp(c);
                    if (view.Deposit == DepositKind.Iron && view.Built) { ironCampBuiltSeason = s + 1; break; }
                }
            if (sc.Iron && ironToolsUnlockedSeason == 0 && e.HasCompletedProject(0, "MET-09")) ironToolsUnlockedSeason = s + 1;
            if (ironRouteActivatedSeason == 0 && ironCampBuiltSeason > 0 && ironToolsUnlockedSeason > 0)
                ironRouteActivatedSeason = Math.Max(ironCampBuiltSeason, ironToolsUnlockedSeason);
            if (sc.Iron && ironFirstKitSeason == 0 && IronKitCoverage(e) > Fix64.Zero) ironFirstKitSeason = s + 1;
            row.IronCampBuiltSeason = ironCampBuiltSeason;
            row.IronToolsUnlockedSeason = ironToolsUnlockedSeason;
            row.IronRouteActivatedSeason = ironRouteActivatedSeason;
            if (sc.Fief && s >= seasonLabor.Length - 1 && ironFirstKitSeason > 0 && s + 1 >= ironFirstKitSeason + 12)
            {
                row.MatureTwelveSeasonCandidates++;
                string windowStatus = twelveSeasonLabor > Fix64.Zero ? "measured" : "no_exposure";
                int noFuelSeasons = row.FiefNoFuelBySeason.Skip(s + 1 - seasonLabor.Length).Take(seasonLabor.Length).Count(x => x);
                row.FiefT3Windows.Add($"F_T3_WINDOW scenario={sc.Name} seed={seed} start={s + 1 - seasonLabor.Length + 1} end={s + 1} " +
                    $"status={windowStatus} labor_raw={twelveSeasonLabor.Raw} metal_raw={twelveSeasonMetalLabor.Raw} no_fuel_seasons={noFuelSeasons}");
                if (twelveSeasonLabor <= Fix64.Zero)
                {
                    row.TwelveSeasonNoExposureWindows++;
                }
                else
                {
                    Fix64 share = twelveSeasonMetalLabor / twelveSeasonLabor;
                    if (row.TwelveSeasonWindows == 0)
                    {
                        row.FirstTwelveSeasonShare = share;
                        row.FirstTwelveSeasonLabor = twelveSeasonLabor;
                        row.FirstTwelveSeasonMetalLabor = twelveSeasonMetalLabor;
                        row.FirstTwelveSeasonEnd = s + 1;
                    }
                    if (row.TwelveSeasonWindows == 0 || share > row.MaxTwelveSeasonShare)
                    {
                        row.MaxTwelveSeasonShare = share;
                        row.MaxTwelveSeasonLabor = twelveSeasonLabor;
                        row.MaxTwelveSeasonMetalLabor = twelveSeasonMetalLabor;
                        row.MaxTwelveSeasonEnd = s + 1;
                    }
                    if (s + 1 >= window + 12)
                    {
                        if (row.LateTwelveSeasonWindows == 0 || share > row.MaxLateTwelveSeasonShare)
                        {
                            row.MaxLateTwelveSeasonShare = share;
                            row.MaxLateTwelveSeasonLabor = twelveSeasonLabor;
                            row.MaxLateTwelveSeasonMetalLabor = twelveSeasonMetalLabor;
                            row.MaxLateTwelveSeasonEnd = s + 1;
                        }
                        row.LateTwelveSeasonWindows++;
                    }
                    row.TwelveSeasonWindows++;
                }
            }
            if (s == window - 1)
            {
                mid = e.MetalStats(0);
                fuelMid = e.CaptureIT6Accounting(0);
                woodMid = CaptureReachableWood(e, s, outerForestTiles, charcoalReach);
                row.WoodMid = woodMid.Standing;
            }
            if (s == seasons - 1) woodEnd = CaptureReachableWood(e, s, outerForestTiles, charcoalReach);
            if (s == 11) row.IronKitShareAt12 = IronKitCoverage(e);
            if (ironRouteActivatedSeason > 0 && s + 1 == ironRouteActivatedSeason + 12)
            {
                row.IronKitShareAfter12Route = IronKitCoverage(e);
                (row.IronKitShare14To44AtRoute, row.IronKitShare45PlusAtRoute) = IronKitAgeCoverage(e);
                ForestTrace routeWood = CaptureReachableWood(e, s, outerForestTiles, charcoalReach);
                row.IronRouteWoodRetention = Ratio(routeWood.Standing, row.WoodStart);
                if (sc.T12 || sc.MixedT12) row.WearAtRoute12 = e.CaptureIT3WearDistribution(0);
            }
            if (sc.MixedT12 && row.T12UnlockSeason > 0 && s + 1 == row.T12UnlockSeason + 12)
                row.WearAfterT12Unlock = e.CaptureIT3WearDistribution(0);
            if (s >= window && e.Metal(BronzeFixture.Settlement).Reason == MethodFallbackReason.NoFuel) fuelShort++;
        }
        MetalStatistics end = e.MetalStats(0);
        IT6AccountingSnapshot fuelEnd = e.CaptureIT6Accounting(0);
        Fix64 labor = end.Labor - mid.Labor;
        row.Share = labor > Fix64.Zero ? (end.MetalLabor - mid.MetalLabor) / labor : Fix64.Zero;
        row.CharcoalWoodPerSeason = (end.CharcoalWood - mid.CharcoalWood + end.FireSettingWood - mid.FireSettingWood) / window;
        row.WoodEnd = woodEnd.Standing;
        row.ForestClearThresholdCrossings = woodEnd.ClearThresholdCrossings;
        row.FuelShortShare = Fix64.FromRatio(fuelShort, window);
        row.IronEraFuelDemandSeasons = fuelEnd.IronEraFuelDemandSeasons - fuelMid.IronEraFuelDemandSeasons;
        row.IronEraFuelShortSeasons = fuelEnd.IronEraFuelShortSeasons - fuelMid.IronEraFuelShortSeasons;
        row.IronEraCharcoalPlan = fuelEnd.IronEraCharcoalPlan - fuelMid.IronEraCharcoalPlan;
        row.IronEraCharcoalUnmet = fuelEnd.IronEraCharcoalUnmet - fuelMid.IronEraCharcoalUnmet;
        row.MetalCharcoalWood = (end.CharcoalWood - mid.CharcoalWood) / window;
        row.ForestRegrowth = (woodEnd.Regrowth - woodMid.Regrowth) / window;
        row.MetalStandingRemoval = (woodEnd.MetalRemoved - woodMid.MetalRemoved) / window;
        row.TotalStandingRemoval = (woodEnd.TotalRemoved - woodMid.TotalRemoved) / window;
        row.WoodBalanceResidual = ((woodEnd.Standing - woodMid.Standing) - (woodEnd.Regrowth - woodMid.Regrowth) +
            (woodEnd.TotalRemoved - woodMid.TotalRemoved)) / window;
        row.IronKitShare = IronKitCoverage(e);
        row.IronFirstKitSeason = ironFirstKitSeason;
        row.Kit11Issued = fuelEnd.Kit11Issued;
        row.Kit12Issued = fuelEnd.Kit12Issued;
        row.Kit11ToolWork = fuelEnd.Kit11ToolWork;
        row.Kit12ToolWork = fuelEnd.Kit12ToolWork;
        row.Kit11Breaks = fuelEnd.Kit11Breaks;
        row.Kit12Breaks = fuelEnd.Kit12Breaks;
        row.T11ToT12Upgrades = fuelEnd.Kit11Upgrades;
        if (sc.T12 || sc.MixedT12) row.WearAtEnd = e.CaptureIT3WearDistribution(0);
        row.Bloom = e.GoodStock(BronzeFixture.Settlement, "G-IRON");
        row.Wrought = e.GoodStock(BronzeFixture.Settlement, "G-WROUGHT");
        return row;
    }

    private static string FormatFiefSeasonTrace(ulong seed, int season, Fix64 totalLabor, Fix64 metalLabor,
        IT6AccountingSnapshot current, IT6AccountingSnapshot previous,
        IT6FiefMaterialStocks stocks, IT6FiefMaterialStocks priorStocks, bool furnaceActive, Fix64 craftCoverage, Fix64 charcoalFactor)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F3", CultureInfo.InvariantCulture);
        Fix64 dMine = current.MineLabor - previous.MineLabor;
        Fix64 dFuel = current.CharcoalLabor - previous.CharcoalLabor;
        Fix64 dSmelt = current.SmeltLabor - previous.SmeltLabor;
        Fix64 dCamp = current.CampLabor - previous.CampLabor;
        Fix64 dIdle = current.IdleMetalLabor - previous.IdleMetalLabor;
        Fix64 dBloomeryLabor = current.BloomeryLabor - previous.BloomeryLabor;
        Fix64 dCharcoalHaul = current.CharcoalHaulLabor - previous.CharcoalHaulLabor;
        Fix64 dOreHaul = current.IronOreHaulLabor - previous.IronOreHaulLabor;
        Fix64 dIronOreMined = current.IronOreMined - previous.IronOreMined;
        Fix64 dRecipeCharcoal = current.IronRecipeCharcoal - previous.IronRecipeCharcoal;
        Fix64 dBloomeryIron = current.BloomeryIronOutput - previous.BloomeryIronOutput;
        Fix64 dWrought = current.ForgeWroughtOutput - previous.ForgeWroughtOutput;
        return $"seed={seed} s{season} factor={F(charcoalFactor)} labor={F(totalLabor)}/{F(metalLabor)} " +
               $"assigned(mine/fuel/smelt/camp)={F(dMine)}/{F(dFuel)}/{F(dSmelt)}/{F(dCamp)} idle-sub={F(dIdle)} " +
               $"bloomery-labor={F(dBloomeryLabor)} explicit-haul(fuel/ore)={F(dCharcoalHaul)}/{F(dOreHaul)} " +
               $"ore-mined={F(dIronOreMined)} recipe-charcoal={F(dRecipeCharcoal)} " +
               $"furnace={furnaceActive} craft-coverage={F(craftCoverage)} output(iron/wrought)={F(dBloomeryIron)}/{F(dWrought)} " +
               $"stock(ore,charcoal,iron,wrought,steel,T11,T12)=" +
               $"{F(priorStocks.IronOre)}->{F(stocks.IronOre)},{F(priorStocks.Charcoal)}->{F(stocks.Charcoal)}," +
               $"{F(priorStocks.Iron)}->{F(stocks.Iron)},{F(priorStocks.Wrought)}->{F(stocks.Wrought)}," +
               $"{F(priorStocks.Steel)}->{F(stocks.Steel)},{F(priorStocks.Kit11)}->{F(stocks.Kit11)}," +
               $"{F(priorStocks.Kit12)}->{F(stocks.Kit12)}";
    }

    private static ForestTrace CaptureReachableWood(LivingEconomyModel economy, int season, HashSet<int> outerForestTiles, int reach)
    {
        B13WoodSnapshot local = default;
        foreach (B13WoodSnapshot snapshot in economy.CaptureB13WoodSnapshots(season))
            if (snapshot.SettlementId == BronzeFixture.Settlement) { local = snapshot; break; }

        Fix64 outerStanding = Fix64.Zero;
        foreach (B13OuterWoodSnapshot snapshot in economy.CaptureB13OuterWoodSnapshots(0, season, reach))
        {
            if (snapshot.SettlementId != BronzeFixture.Settlement || !snapshot.IsForest) continue;
            outerForestTiles.Add(snapshot.Tile);
            outerStanding += snapshot.Standing;
        }

        Fix64 outerRegrowth = Fix64.Zero, outerMetalRemoved = Fix64.Zero;
        Fix64 outerChopRemoved = Fix64.Zero, outerCivicRemoved = Fix64.Zero, outerOtherRemoved = Fix64.Zero;
        int outerClearThresholdCrossings = 0;
        foreach (B13TileWoodSnapshot tile in economy.CaptureB13TileWoodSnapshots(0))
        {
            if (!outerForestTiles.Contains(tile.Tile)) continue;
            outerRegrowth += tile.Regrowth;
            outerMetalRemoved += tile.MetalRemoved;
            outerChopRemoved += tile.ChopRemoved;
            outerCivicRemoved += tile.CivicRemoved;
            outerOtherRemoved += tile.OtherRemoved;
            outerClearThresholdCrossings += tile.ClearThresholdCrossings;
        }
        return new ForestTrace(local.StandingForestWood + outerStanding,
            local.Regrowth + outerRegrowth, local.MetalRemoved + outerMetalRemoved,
            local.ChopRemoved + outerChopRemoved, local.CivicRemoved + outerCivicRemoved, local.OtherRemoved + outerOtherRemoved,
            local.ClearThresholdCrossings + outerClearThresholdCrossings);
    }

    private static Fix64 Ratio(Fix64 end, Fix64 start) => start <= Fix64.Zero ? Fix64.One : end / start;
    private static Fix64 FuelShortRate(Row row) => row.IronEraFuelDemandSeasons > 0
        ? Fix64.FromRatio(row.IronEraFuelShortSeasons, row.IronEraFuelDemandSeasons)
        : Fix64.Zero;
    private static Fix64 FuelUnmetShare(Row row) => row.IronEraCharcoalPlan > Fix64.Zero
        ? row.IronEraCharcoalUnmet / row.IronEraCharcoalPlan
        : Fix64.Zero;
    private static string SeasonRange(List<Row> rows, Func<Row, int> season)
    {
        int min = season(rows[0]), max = min;
        foreach (Row row in rows)
        {
            int value = season(row);
            if (value < min) min = value;
            if (value > max) max = value;
        }
        return $"{min}-{max}";
    }

    private static Fix64 IronKitCoverage(LivingEconomyModel economy)
    {
        int adults = 0, iron = 0;
        foreach (int tier in economy.FixtureAdultKitTiers(BronzeFixture.Settlement)) { adults++; if (tier >= 11) iron++; }
        return adults == 0 ? Fix64.Zero : Fix64.FromRatio(iron, adults);
    }

    private static (Fix64 Age14To44, Fix64 Age45Plus) IronKitAgeCoverage(LivingEconomyModel economy)
    {
        int adult = 0, adultIron = 0, elder = 0, elderIron = 0;
        foreach ((int age, int tier) in economy.FixtureAdultKitCohort(BronzeFixture.Settlement))
        {
            if (age < 45) { adult++; if (tier >= 11) adultIron++; }
            else { elder++; if (tier >= 11) elderIron++; }
        }
        return (adult == 0 ? Fix64.Zero : Fix64.FromRatio(adultIron, adult),
                elder == 0 ? Fix64.Zero : Fix64.FromRatio(elderIron, elder));
    }

    private static string Pct(Fix64 v) => (v * 100).ToDoubleForDisplay().ToString("F1", CultureInfo.InvariantCulture) + "%";

    private static string FormatWear(IT3WearDistribution d)
        => $"{d.T11BelowZero},{d.T11Under20},{d.T11Under40},{d.T11Under60},{d.T11Under80},{d.T11AtLeast80} / " +
           $"{d.T12BelowZero},{d.T12Under20},{d.T12Under40},{d.T12Under60},{d.T12Under80},{d.T12AtLeast80} " +
           $"(n={d.T11Total}/{d.T12Total})";
    private static string Signed(Fix64 v)
    {
        double display = v.ToDoubleForDisplay();
        if (Math.Abs(display) < 0.005) display = 0;
        return display.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
    }
    private static Fix64 Mean(List<Row> rows, Func<Row, Fix64> f) { Fix64 t = Fix64.Zero; foreach (Row r in rows) t += f(r); return t / rows.Count; }
    private static Fix64 Max(List<Row> rows, Func<Row, Fix64> f) { Fix64 m = f(rows[0]); foreach (Row r in rows) if (f(r) > m) m = f(r); return m; }
    private static Fix64 Min(List<Row> rows, Func<Row, Fix64> f) { Fix64 m = f(rows[0]); foreach (Row r in rows) if (f(r) < m) m = f(r); return m; }
}
