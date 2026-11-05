using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// <summary>Default charcoal haul reach in balance.yaml; the fuel fixture does not grant TRN-05 carts.</summary>
    private const int FuelFixtureCharcoalReach = 8;
    private const int FuelFixtureCartCharcoalReach = 12;

    private sealed record Scenario(string Name, bool Iron, bool Coppice, int ForestTiles, bool LoseIronAtMidpoint, bool Carts = false);

    private static readonly Scenario[] Scenarios =
    {
        new("bronze-reference", false, true, 13, false),
        new("iron", true, true, 13, false),
        new("iron-low-wood", true, true, 4, false),
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
    };

    private sealed class Row
    {
        public Fix64 Share, CharcoalWoodPerSeason, WoodStart, WoodMid, WoodEnd, IronKitShareAt12, IronKitShareAfter12Route,
            IronRouteWoodRetention, IronKitShare14To44AtRoute, IronKitShare45PlusAtRoute, IronKitShare, FuelShortShare, Bloom, Wrought;
        public int IronEraFuelDemandSeasons, IronEraFuelShortSeasons;
        public Fix64 IronEraCharcoalPlan, IronEraCharcoalUnmet;
        public Fix64 ForestRegrowth, MetalCharcoalWood, MetalStandingRemoval, TotalStandingRemoval, WoodBalanceResidual;
        public int IronCampBuiltSeason, IronToolsUnlockedSeason, IronRouteActivatedSeason, IronFirstKitSeason;
        public int ForestClearThresholdCrossings;
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
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seeds":
                    seeds.Clear();
                    foreach (string part in args[++i].Split(',')) seeds.Add(ulong.Parse(part, CultureInfo.InvariantCulture));
                    break;
                case "--seasons": seasons = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--people": people = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option {args[i]}.");
            }
        }
        if (packPath is null) throw new ArgumentException("iron-fuel needs --pack FILE (civsim build-pack --out FILE).");
        if (seasons < 8 || seasons % 2 != 0) throw new ArgumentException("--seasons must be an even number of at least 8.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        Console.Write(Report(pack, seeds, seasons, people));
        return 0;
    }

    internal static string Report(ContentPack pack, IReadOnlyList<ulong> seeds, int seasons, int people, int scenarioLimit = int.MaxValue)
    {
        var sb = new StringBuilder();
        void L(string text = "") => sb.Append(text).Append('\n');
        int window = seasons / 2;
        L($"iron-fuel: {seeds.Count} seeds, {seasons} seasons ({window}-season steady-state window), fixed {people}-person fixture settlement");
        L($"I-T6: metal chain <= {Pct(ShareCeiling)} of civilian work; compare forest stock and fuel flows with the same-seed Bronze control");
        L();
        L("scenario            seeds  metal-share(mean/max)  char+fire/season  regrowth/season  all-cuts/season  mass-resid  metal-char/season  metal-cut/season  reachable wood end/start(mean/min)  end/mid  NoFuel fallback(mean/max)  T11+ at fixture12/12 after route/end  bloom+wrought");
        var workVerdicts = new List<string>();
        var rowsByScenario = new Dictionary<string, Dictionary<ulong, Row>>(StringComparer.Ordinal);
        int shown = 0;
        foreach (Scenario sc in Scenarios)
        {
            if (shown++ >= scenarioLimit) break;
            var rows = new List<Row>();
            int skipped = 0;
            foreach (ulong seed in seeds)
            {
                try { rows.Add(RunOne(pack, seed, sc, seasons, people)); }
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
            bool shareOk = maxShare <= ShareCeiling;
            workVerdicts.Add($"{sc.Name}: work share {(shareOk ? "within" : "ABOVE")} {Pct(ShareCeiling)} on every seed (max {Pct(maxShare)})");
        }
        L();
        L("I-T6 planned-charcoal shortfall (final half; demand-seasons count only seasons with a positive charcoal-production plan after existing charcoal stock):");
        L($"approved per-seed cutoff: shortfall rate <= {Pct(IT6FuelGate.MaxShortRate)} and unmet / planned charcoal <= {Pct(IT6FuelGate.MaxUnmetPlanShare)}; fewer than {IT6FuelGate.MinDemandSeasons} demand-seasons is censored");
        L("scenario                  plan demand seasons mean/max  shortfall rate mean/max  planned charcoal mean L/seed  unmet mean L/seed  unmet share mean/max");
        foreach (Scenario sc in Scenarios)
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
        L("work-share screen:");
        foreach (string v in workVerdicts) L("  " + v);
        L();
        L("Forest clearing diagnostic (tile crossings below the simulation's 5 L cleared-woodland threshold, full run):");
        L("scenario                       seeds with crossings  total tile crossings  max in one seed");
        foreach (Scenario sc in Scenarios)
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
        L();
        L("I-T2 sustainable-route checkpoint (12 complete seasons after camp and MET-09 are both available):");
        L("Coverage counts all adult kit holders as a conservative proxy; the proposed I-T2 denominator is tool workers.");
        L("scenario                    MET-09/camp/first-kit seasons   all-adult T11+ mean/min/max  seeds >=70%  T11+ age14-44/45+  route-wood mean/min");
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
        L();
        L("paired Iron-minus-Bronze flows over each seed's final half (litres/season; same map and population fixture):");
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
        L("paired Coppicing effect (Coppicing minus no-Coppicing, same seeds):");
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
        L("The Bronze reference has the same iron site but makes no iron (the chain is Iron-chapter only), so the difference between it and 'iron' is the Iron chain's cost.");
        return sb.ToString();
    }

    private static Row RunOne(ContentPack pack, ulong seed, Scenario sc, int seasons, int people)
    {
        BronzeFixture f = BronzeFixture.CreateFixedPopulationFuelScenario(pack, seed, people, sc.Iron, sc.Coppice, sc.ForestTiles,
            sc.Carts ? new[] { "MET-08", "MET-09", "TRN-05" } : new[] { "MET-08", "MET-09" });
        LivingEconomyModel e = f.Economy;
        e.B13WoodTraceEnabled = true;
        e.IT6AccountingTraceEnabled = sc.Iron;
        int window = seasons / 2;
        int charcoalReach = sc.Carts ? FuelFixtureCartCharcoalReach : FuelFixtureCharcoalReach;
        HashSet<int> outerForestTiles = new();
        ForestTrace woodStart = CaptureReachableWood(e, -1, outerForestTiles, charcoalReach);
        var row = new Row { Seed = seed, WoodStart = woodStart.Standing };
        MetalStatistics mid = default;
        IT6AccountingSnapshot fuelMid = default;
        ForestTrace woodMid = default!, woodEnd = default!;
        int fuelShort = 0;
        int ironCampBuiltSeason = 0, ironToolsUnlockedSeason = 0, ironRouteActivatedSeason = 0, ironFirstKitSeason = 0;
        for (int s = 0; s < seasons; s++)
        {
            if (sc.LoseIronAtMidpoint && s == window && f.IronTile >= 0) e.FixtureExhaust(f.IronTile);
            f.RunSeasons(1);
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
            }
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
        row.Bloom = e.GoodStock(BronzeFixture.Settlement, "G-IRON");
        row.Wrought = e.GoodStock(BronzeFixture.Settlement, "G-WROUGHT");
        return row;
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
