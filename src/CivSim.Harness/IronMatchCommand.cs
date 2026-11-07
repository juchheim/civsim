using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Harness;

/// <summary>
/// <c>iron-match</c>: one whole Iron-chapter match with four Warlord AI civilizations, played to the chapter's end, with a report of what the Iron systems actually did
/// (docs/MVP/25 §13). It is for reading, not a gate: it prints when each civilization reached KEY-4 and KEY-5, how the chapter ended, and for each civilization the iron
/// kits, works, staff, Wonders, wars and the events that the new systems raised.
/// <code>civsim-harness iron-match --pack FILE [--seed N] [--seeds N] [--minutes N]</code>
/// </summary>
internal static class IronMatchCommand
{
    private static readonly EconomyArchetype[] Archetypes = { EconomyArchetype.Builder, EconomyArchetype.Grower, EconomyArchetype.Warlord, EconomyArchetype.Sage };

    private readonly record struct ForestLedger(int Tiles, Fix64 Initial, Fix64 Standing, Fix64 Regrowth, Fix64 IronRemoved,
        Fix64 ChopRemoved, Fix64 MetalRemoved, Fix64 CivicRemoved, Fix64 OtherRemoved,
        int Clearings, int ChopClearings, int MetalClearings, int CivicClearings, int OtherClearings,
        int IronClearings, int ClearedAtEnd, int GrasslandAtEnd);

    private readonly record struct LocalWoodLedger(Fix64 Stock, Fix64 ChopIn, Fix64 CivicIn, Fix64 OtherIn,
        Fix64 HearthUse, Fix64 FuelUse, Fix64 MetalUse, Fix64 GoodsUse, Fix64 CivicUse, Fix64 StructureUse, Fix64 OtherUse);

    private sealed class IT2RouteRecord
    {
        public required int SettlementId;
        public required int StartSeason;
        public required int UnlockSeason;
        public required int HubId;
        public required IT6FuelSiteSnapshot Start;
        public required Dictionary<int, IT6FuelSiteSnapshot> FuelStartsBySite;
        public Dictionary<int, IT6FuelSiteSnapshot>? FuelEndsBySite;
        public IT6FuelSiteSnapshot? End;
        public IT6FuelSiteSnapshot? FuelEnd;
        public bool HubChanged;
        public int EndSeason = -1;
    }

    private sealed record IT6WoodImportSupplier(IT6FuelSiteSnapshot Site, int RouteSeasons);
    private sealed record IT6WoodImportOpportunity(int Season, int ConsumerId, int ConsumerCivilization,
        Fix64 UnmetCharcoal, IT6FuelSiteSnapshot Consumer, IT6WoodImportSupplier[] Suppliers);
    private readonly record struct IT6TimedWoodShipment(int ArrivalSeason, int SellerId, int BuyerId,
        int BuyerCivilization, Fix64 Quantity);

    private sealed class IT6TimedImportStats
    {
        public int DemandSeasons, BaselineShortSeasons, CounterfactualShortSeasons;
        public Fix64 Planned, BaselineUnmet, CounterfactualUnmet, Shipped, Arrived, UsedAgainstFuel, EndingWoodStock, InTransit;
    }

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 1, minutes = 110, capYears = ChapterRules.IronCapYear, countdown = ChapterRules.IronCountdownSeasons;
        bool grantE4 = false;
        bool it6Accounting = false;
        bool it6SupplyDiagnostic = false;
        bool ironHubFuelSharing = false;
        bool ironFissionCoveragePreserving = false;
        bool ironKitCoveragePriority = false;
        bool bronzeControl = false;
        int ironFuelReachBufferOverride = -1;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pack": packPath = args[++i]; break;
                case "--seed": first = ulong.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--seeds": seeds = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--minutes": minutes = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--cap-years": capYears = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--countdown": countdown = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--grant-e4": grantE4 = true; break;
                case "--it6-accounting": it6Accounting = true; break;
                case "--it6-supply-diagnostic": it6SupplyDiagnostic = true; it6Accounting = true; break;
                case "--iron-hub-fuel-sharing": ironHubFuelSharing = true; break;
                case "--iron-fission-coverage-preserving": ironFissionCoveragePreserving = true; break;
                case "--iron-kit-coverage-priority": ironKitCoveragePriority = true; break;
                case "--iron-fuel-reach-buffer": ironFuelReachBufferOverride = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                case "--bronze-control": bronzeControl = true; break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness iron-match --pack FILE [--seed N] [--seeds N] [--minutes N] [--cap-years N] [--countdown SEASONS] [--grant-e4] [--it6-accounting] [--it6-supply-diagnostic] [--iron-hub-fuel-sharing] [--iron-fission-coverage-preserving] [--iron-kit-coverage-priority] [--iron-fuel-reach-buffer N] [--bronze-control]");
                    return 2;
            }
        }
        if (ironFuelReachBufferOverride is < -1 or > 100) throw new ArgumentOutOfRangeException(nameof(ironFuelReachBufferOverride));
        if (packPath is null) throw new ArgumentException("iron-match needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        for (int n = 0; n < seeds; n++) Console.Write(Play(first + (ulong)n, pack, minutes, capYears, countdown, grantE4, it6Accounting, it6SupplyDiagnostic, ironHubFuelSharing, ironFissionCoveragePreserving, ironKitCoveragePriority, bronzeControl, ironFuelReachBufferOverride));
        return 0;
    }

    private static string Play(ulong seed, ContentPack pack, int minutes, int capYears, int countdown, bool grantE4, bool it6Accounting, bool it6SupplyDiagnostic, bool ironHubFuelSharing, bool ironFissionCoveragePreserving, bool ironKitCoveragePriority, bool bronzeControl, int ironFuelReachBufferOverride)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        ChapterRules rules = bronzeControl ? ChapterRules.Bronze :
            capYears == ChapterRules.IronCapYear && countdown == ChapterRules.IronCountdownSeasons ? ChapterRules.Iron : ChapterRules.IronWithLimits(capYears, countdown);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
        match.Economy.B13WoodTraceEnabled = it6Accounting;
        match.Economy.IT6AccountingTraceEnabled = it6Accounting;
        match.Economy.IronHubFuelSharingEnabled = ironHubFuelSharing;
        match.Economy.IronFissionCoveragePreservingForDiagnostics = ironFissionCoveragePreserving;
        match.Economy.IronKitCoveragePriorityForDiagnostics = ironKitCoveragePriority;
        match.Economy.IronFuelReachBufferOverride = ironFuelReachBufferOverride;
        FissionSiteDiagnostics? fissionDiagnostics = ironFissionCoveragePreserving ? new FissionSiteDiagnostics() : null;
        if (fissionDiagnostics is not null) match.Economy.FissionSiteDiagnosticsForDiagnostics = fissionDiagnostics;
        var runner = new SimRunner(match);
        int civs = Archetypes.Length;
        ResearchSystem research = match.Research;
        int met09 = research.Catalog.IndexOf("MET-09");
        ForestLedger startForest = it6Accounting ? CaptureForest(match.Economy) : default;
        LocalWoodLedger startWood = it6Accounting ? CaptureLocalWood(match.Economy, 0) : default;
        var routeSeason = new int[civs];
        Array.Fill(routeSeason, -1);
        var steadySeason = new int[civs];
        Array.Fill(steadySeason, -1);
        var routeStats = new MetalStatistics[civs];
        var routeTrace = new IT6AccountingSnapshot[civs];
        var steadyStats = new MetalStatistics[civs];
        var steadyTrace = new IT6AccountingSnapshot[civs];
        var statsHistory = new List<MetalStatistics>[civs];
        var traceHistory = new List<IT6AccountingSnapshot>[civs];
        var it2Routes = new Dictionary<int, IT2RouteRecord>();
        var fuelHistory = it6SupplyDiagnostic ? new List<(int Season, IT6FuelSiteSnapshot[] Sites)>() : null;
        var catchmentHistory = it6SupplyDiagnostic ? new List<(int Season, int Civilization, IT6FuelSiteSnapshot[] Sites, B13OuterWoodSnapshot[] OuterRows)>() : null;
        var importHistory = it6SupplyDiagnostic ? new List<IT6WoodImportOpportunity>() : null;
        IT6FuelSiteSnapshot[]? previousFuelSites = null;
        var lastCatchmentCaptureSeason = new Dictionary<int, int>();
        for (int c = 0; c < civs; c++)
        {
            statsHistory[c] = new List<MetalStatistics> { match.Economy.MetalStats(c) };
            traceHistory[c] = new List<IT6AccountingSnapshot> { match.Economy.CaptureIT6Accounting(c) };
        }
        int key4 = research.Catalog.IndexOf("KEY-4"), key5 = research.Catalog.IndexOf("KEY-5");
        var granted = new bool[civs];
        var idleSeasons = new int[civs];
        var peakSoldiers = new int[civs];
        long limit = minutes * 600L;
        MatchEnd end = match.EndOfMatch();
        while (runner.Tick < limit && !end.IsOver(runner.Tick))
        {
            // Diagnostic: take research out of the picture. Each civilization is given every E4 project except the keystone the moment it has KEY-4.
            if (grantE4)
                for (int c = 0; c < civs; c++)
                {
                    if (granted[c] || !research.IsDone(c, key4)) continue;
                    granted[c] = true;
                    foreach (ProjectDef p in research.Catalog.Projects)
                        if (p.Era == 4 && p.Index != key5) runner.Submit(new Command(runner.Tick, (byte)c, CommandKind.CompleteProject, p.Index, 0, 0));
                }
            runner.Advance(150);
            end = match.EndOfMatch();
            if (it6Accounting)
            {
                int season = (int)(runner.Tick / 150L);
                IT6FuelSiteSnapshot[] seasonSites = match.Economy.CaptureIT6FuelSites().ToArray();
                fuelHistory?.Add((season, seasonSites));
                if (catchmentHistory is not null)
                {
                    var previous = (previousFuelSites ?? Array.Empty<IT6FuelSiteSnapshot>()).ToDictionary(site => site.SettlementId);
                    IT6FuelSiteSnapshot[] shortSites = seasonSites.Where(site => site.ShortSeasons > previous.GetValueOrDefault(site.SettlementId).ShortSeasons).ToArray();
                    foreach (IT6FuelSiteSnapshot consumer in shortSites)
                    {
                        Fix64 unmet = consumer.Unmet - previous.GetValueOrDefault(consumer.SettlementId).Unmet;
                        if (unmet <= Fix64.Zero) continue;
                        IT6WoodImportSupplier[] suppliers = seasonSites
                            .Where(site => site.Civilization != consumer.Civilization && site.Population > 0 && site.WoodExportableNow > Fix64.Zero)
                            .Where(site => match.Politics.Diplomacy.Contacted(site.Civilization, consumer.Civilization) &&
                                !match.Politics.Diplomacy.AtWar(site.Civilization, consumer.Civilization) &&
                                match.Politics.Diplomacy.Relations(site.Civilization, consumer.Civilization) >= 0)
                            .Select(site => (Site: site, Reachable: match.Politics.CanReach(site.Civilization, site.SiteTile, consumer.SiteTile)))
                            .Where(candidate => candidate.Reachable)
                            .Select(candidate => new IT6WoodImportSupplier(candidate.Site,
                                match.Politics.TravelSeasons(candidate.Site.Civilization, candidate.Site.SiteTile, consumer.SiteTile)))
                            .OrderBy(candidate => candidate.RouteSeasons)
                            .ThenByDescending(candidate => candidate.Site.WoodExportableNow)
                            .ThenBy(candidate => candidate.Site.SettlementId)
                            .ToArray();
                        importHistory?.Add(new IT6WoodImportOpportunity(season, consumer.SettlementId, consumer.Civilization,
                            unmet, consumer, suppliers));
                    }

                    foreach (int civ in shortSites.Select(site => site.Civilization).Distinct().OrderBy(civ => civ))
                    {
                        if (lastCatchmentCaptureSeason.TryGetValue(civ, out int capturedSeason) && season - capturedSeason < 12) continue;
                        IT6FuelSiteSnapshot[] candidates = seasonSites.Where(site => site.Civilization == civ && site.Forges > 0 && site.Smelters > 0).ToArray();
                        if (candidates.Length == 0) continue;
                        int reach = candidates.Max(site => site.FuelReach);
                        B13OuterWoodSnapshot[] outerRows = match.Economy.CaptureB13OuterWoodSnapshots(civ, season, reach).ToArray();
                        catchmentHistory.Add((season, civ, candidates, outerRows));
                        lastCatchmentCaptureSeason[civ] = season;
                    }
                    previousFuelSites = seasonSites;
                }
                var sitesById = seasonSites.ToDictionary(site => site.SettlementId);
                foreach (IT6FuelSiteSnapshot site in seasonSites)
                {
                    if (!it2Routes.TryGetValue(site.SettlementId, out IT2RouteRecord? route) &&
                        site.Kit11Issued + site.Kit12Issued + site.Kit11Received + site.Kit12Received > Fix64.Zero)
                    {
                        int unlockSeason = research.IsDone(site.Civilization, met09)
                            ? checked((int)(research.CompletedTick(site.Civilization, met09) / 150L))
                            : -1;
                        route = new IT2RouteRecord
                        {
                            SettlementId = site.SettlementId,
                            StartSeason = season,
                            UnlockSeason = unlockSeason,
                            HubId = site.IronFuelHubId,
                            Start = site,
                            FuelStartsBySite = seasonSites.Where(candidate => candidate.Civilization == site.Civilization)
                                .ToDictionary(candidate => candidate.SettlementId),
                        };
                        it2Routes.Add(site.SettlementId, route);
                    }
                    if (route is not null && route.End is null)
                    {
                        if (site.IronFuelHubId != route.HubId) route.HubChanged = true;
                        if (season >= route.StartSeason + 12)
                        {
                            route.End = site;
                            route.EndSeason = season;
                            if (sitesById.TryGetValue(route.HubId, out IT6FuelSiteSnapshot fuelEnd)) route.FuelEnd = fuelEnd;
                            route.FuelEndsBySite = seasonSites.Where(candidate => candidate.Civilization == route.Start.Civilization)
                                .ToDictionary(candidate => candidate.SettlementId);
                        }
                    }
                }
                for (int c = 0; c < civs; c++)
                {
                    IT6AccountingSnapshot trace = match.Economy.CaptureIT6Accounting(c);
                    statsHistory[c].Add(match.Economy.MetalStats(c));
                    traceHistory[c].Add(trace);
                    if (routeSeason[c] < 0 && research.IsDone(c, met09) && trace.Kit11Issued + trace.Kit12Issued > Fix64.Zero)
                    {
                        routeSeason[c] = season;
                        routeStats[c] = match.Economy.MetalStats(c);
                        routeTrace[c] = trace;
                    }
                    else if (routeSeason[c] >= 0 && steadySeason[c] < 0 && season >= routeSeason[c] + 12)
                    {
                        steadySeason[c] = season;
                        steadyStats[c] = match.Economy.MetalStats(c);
                        steadyTrace[c] = trace;
                    }
                }
            }
            for (int c = 0; c < civs; c++)
            {
                if (match.Economy.IronIdleInstitutions(c) > 0) idleSeasons[c]++;
                int soldiers = 0;
                for (int s = 0; s < match.Economy.SettlementCount; s++) if (match.Economy.SettlementLite(s).Civilization == c) soldiers += match.Economy.SoldiersAt(s);
                peakSoldiers[c] = Math.Max(peakSoldiers[c], soldiers);
            }
        }

        var o = new StringBuilder();
        void L(string text = "") => o.Append(text).Append('\n');
        string Min(long tick) => tick < 0 ? "-" : (tick / 600.0).ToString("F1", CultureInfo.InvariantCulture);
        L($"== {(bronzeControl ? "bronze-control" : "iron-match")} seed {seed}: {runner.Tick / 600.0:F1} minutes simulated, chapter end {end.Reason} at season {end.EndSeason} (ended: {end.IsOver(runner.Tick)})");
        L($"{"civ",-4}{"arch",-9}{"KEY-4",7}{"KEY-5",7}{"E4 done",8}{"settl",7}{"people",8}{"T11",6}{"T12",6}{"adults",8}{"soldiers",9}{"scholars",9}{"missions",9}{"idleS",7}");
        for (int c = 0; c < civs; c++)
        {
            int settlements = 0, people = 0, scholars = 0, missionaries = 0, soldiers = 0;
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                if (match.Economy.SettlementLite(s).Civilization != c || match.Politics.IsFreeFolk(s)) continue;
                settlements++; people += match.Economy.Population(match.Economy.SettlementLite(s).Id);
                scholars += match.Economy.ScholarsAt(s); missionaries += match.Economy.Missionaries(s); soldiers += match.Economy.SoldiersAt(s);
            }
            (int t11, int t12, int adults) = match.Economy.IronKitHolders(c);
            int e4 = research.Catalog.Projects.Count(p => p.Era == 4 && research.IsDone(c, p.Index));
            long k4 = research.IsDone(c, key4) ? research.CompletedTick(c, key4) : -1, k5 = research.IsDone(c, key5) ? research.CompletedTick(c, key5) : -1;
            L($"c{c,-3}{Archetypes[c],-9}{Min(k4),7}{Min(k5),7}{e4,8}{settlements,7}{people,8}{t11,6}{t12,6}{adults,8}{soldiers,9}{scholars,9}{missionaries,9}{idleSeasons[c],7}");
        }

        // Iron works by kind, per civilization.
        L("iron works built (all settlements):");
        for (int k = 0; k < LivingEconomyModel.IronWorkKindCount; k++)
        {
            var row = new int[civs];
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                int civ = match.Economy.SettlementLite(s).Civilization;
                if ((uint)civ < (uint)civs) row[civ] += match.Economy.IronWorkAt(s, k);
            }
            if (row.Sum() > 0) L($"  {LivingEconomyModel.IronWorkId(k),-24}{string.Join("  ", row.Select(v => v.ToString().PadLeft(3)))}");
        }
        L("iron build in progress (town count by structure, with the share of its labor done):");
        for (int c = 0; c < civs; c++)
        {
            var byKind = new SortedDictionary<string, (int Towns, int Permille)>();
            int villages = 0;
            for (int s = 0; s < match.Economy.SettlementCount; s++)
            {
                if (match.Economy.SettlementLite(s).Civilization != c || match.Politics.IsFreeFolk(s)) continue;
                if (match.Economy.Tier(s) == SettlementTier.Village) { villages++; continue; }
                (int kind, int permille) = match.Economy.IronBuildAt(s);
                string name = kind < 0 ? "(nothing wanted)" : LivingEconomyModel.IronWorkId(kind);
                byKind.TryGetValue(name, out (int Towns, int Permille) cur);
                byKind[name] = (cur.Towns + 1, cur.Permille + permille);
            }
            L($"  c{c}: villages {villages}; " + string.Join(", ", byKind.Select(kv => $"{kv.Key} x{kv.Value.Towns} ({kv.Value.Permille / Math.Max(1, kv.Value.Towns) / 10}%)")));
        }
        L("what the stuck towns lack (settlements whose structure is fully worked):");
        var lacks = new SortedDictionary<string, int>();
        for (int s = 0; s < match.Economy.SettlementCount; s++)
        {
            (int kind, int permille) = match.Economy.IronBuildAt(s);
            if (kind < 0 || permille < 1000) continue;
            string shortage = match.Economy.IronBuildShortages(s);
            foreach (string part in shortage.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where((_, i) => i % 2 == 0))
            {
                string key = $"{LivingEconomyModel.IronWorkId(kind)} lacks {part}";
                lacks[key] = lacks.GetValueOrDefault(key) + 1;
            }
            if (shortage.Length == 0) lacks[$"{LivingEconomyModel.IronWorkId(kind)} fully supplied"] = lacks.GetValueOrDefault($"{LivingEconomyModel.IronWorkId(kind)} fully supplied") + 1;
        }
        foreach (var kv in lacks) L($"  {kv.Key}: {kv.Value} towns");
        L("wonders held: " + string.Join(", ", new[] { "great_library", "great_lighthouse", "great_dome" }.Select(w =>
        {
            for (int c = 0; c < civs; c++) if (match.Economy.HoldsWonder(c, w)) return $"{w} c{c}";
            return $"{w} none";
        })));

        // What the politics events say the new systems did.
        var counts = match.Politics.Events.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        int Count(PoliticsEventKind k) => counts.GetValueOrDefault(k);
        L($"events: wars {Count(PoliticsEventKind.WarDeclared)}  raids {Count(PoliticsEventKind.RaidResolved)}  assaults {Count(PoliticsEventKind.AssaultResolved)}  captured {Count(PoliticsEventKind.Captured)}  " +
          $"investments {Count(PoliticsEventKind.InvestmentBegan)}  blockades {Count(PoliticsEventKind.BlockadeBegan)}/{Count(PoliticsEventKind.BlockadeEnded)}  " +
          $"tributes {Count(PoliticsEventKind.TributeAgreed)}/{Count(PoliticsEventKind.TributeEnded)}  leagues {Count(PoliticsEventKind.LeagueJoined)}/{Count(PoliticsEventKind.LeagueEnded)}  compacts {Count(PoliticsEventKind.CompactSigned)}");
        L($"now: tributes {match.Politics.Tributes.Count}, leagues {match.Politics.Leagues.Count}, blockades {match.Politics.Blockades.Count}, investments {match.Politics.Investments.Count} " +
          $"(with engines {match.Politics.Investments.Count(i => i.Engines > 0)})");
        L($"peak soldiers per civ: {string.Join(", ", peakSoldiers.Select((v, c) => $"c{c} {v}"))}");
        L($"final legacy per civ: {string.Join(", ", Enumerable.Range(0, civs).Select(c => $"c{c} {(research.Legacy(c) + match.Politics.Territory.Dominion(c)).ToDoubleForDisplay():F0}"))}");
        if (fissionDiagnostics is not null)
        {
            L("fission cohort coverage diagnostics (last completed season's actual toolworkers; result reports whether the sample was adjusted):");
            foreach (FissionResolutionAttempt attempt in fissionDiagnostics.Resolutions)
                L($"  {attempt.Trace}");
        }
        if (ironFuelReachBufferOverride >= 0)
            L($"controlled diagnostic Iron fuel-haul buffer override: {ironFuelReachBufferOverride} tiles (current default {8})");
        if (ironFissionCoveragePreserving)
            L("controlled diagnostic fission cohort: deficit-reducing current tool-kit coverage swaps (unchanged 70% screen)");
        if (it6Accounting)
        {
            AppendIT6Accounting(o, (int)(runner.Tick / 150L), match.Economy, startForest, startWood,
                routeSeason, steadySeason, routeStats, routeTrace, steadyStats, steadyTrace, statsHistory, traceHistory,
                it2Routes.Values.OrderBy(x => x.SettlementId).ToArray(), fuelHistory);
            if (fuelHistory is not null && catchmentHistory is not null)
                AppendIT6SupplyDiagnostic(o, world, fuelHistory, catchmentHistory,
                    importHistory is null ? Array.Empty<IT6WoodImportOpportunity>() : importHistory);
        }
        L();
        return o.ToString();
    }

    private static ForestLedger CaptureForest(LivingEconomyModel economy)
    {
        Fix64 initial = Fix64.Zero, standing = Fix64.Zero, regrowth = Fix64.Zero;
        Fix64 iron = Fix64.Zero;
        Fix64 chop = Fix64.Zero, metal = Fix64.Zero, civic = Fix64.Zero, other = Fix64.Zero;
        int crossings = 0, chopCrossings = 0, metalCrossings = 0, civicCrossings = 0, otherCrossings = 0;
        int ironCrossings = 0;
        int cleared = 0, grassland = 0;
        IReadOnlyList<B13TileWoodSnapshot> tiles = economy.CaptureB13TileWoodSnapshots(0);
        foreach (B13TileWoodSnapshot tile in tiles)
        {
            initial += tile.Initial; standing += tile.Standing; regrowth += tile.Regrowth;
            iron += tile.IronRemoved;
            chop += tile.ChopRemoved; metal += tile.MetalRemoved; civic += tile.CivicRemoved; other += tile.OtherRemoved;
            crossings += tile.ClearThresholdCrossings;
            ironCrossings += tile.IronClearThresholdCrossings;
            chopCrossings += tile.ChopClearThresholdCrossings; metalCrossings += tile.MetalClearThresholdCrossings;
            civicCrossings += tile.CivicClearThresholdCrossings; otherCrossings += tile.OtherClearThresholdCrossings;
            if (tile.ForestState > 0) cleared++;
            if (tile.ForestState == 2) grassland++;
        }
        return new ForestLedger(tiles.Count, initial, standing, regrowth, iron, chop, metal, civic, other,
            crossings, chopCrossings, metalCrossings, civicCrossings, otherCrossings, ironCrossings, cleared, grassland);
    }

    private static LocalWoodLedger CaptureLocalWood(LivingEconomyModel economy, int season)
    {
        var total = new LocalWoodLedger();
        foreach (B13WoodSnapshot local in economy.CaptureB13WoodSnapshots(season))
            total = new LocalWoodLedger(total.Stock + local.LocalWood,
                total.ChopIn + local.ChopInflows, total.CivicIn + local.CivicInflows, total.OtherIn + local.OtherInflows,
                total.HearthUse + local.HearthUse, total.FuelUse + local.E3FuelUse, total.MetalUse + local.MetalUse,
                total.GoodsUse + local.GoodsUse, total.CivicUse + local.CivicUse, total.StructureUse + local.StructureUse,
                total.OtherUse + local.OtherUse);
        return total;
    }

    private static void AppendIT6Accounting(StringBuilder output, int endSeason, LivingEconomyModel economy,
        ForestLedger startForest, LocalWoodLedger startWood, int[] routeSeason, int[] steadySeason,
        MetalStatistics[] routeStats, IT6AccountingSnapshot[] routeTrace,
        MetalStatistics[] steadyStats, IT6AccountingSnapshot[] steadyTrace,
        List<MetalStatistics>[] statsHistory, List<IT6AccountingSnapshot>[] traceHistory, IT2RouteRecord[] it2Routes,
        IReadOnlyList<(int Season, IT6FuelSiteSnapshot[] Sites)>? fuelHistory)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        static string Pct(Fix64 numerator, Fix64 denominator) => denominator <= Fix64.Zero
            ? "n/a"
            : (100 * numerator.ToDoubleForDisplay() / denominator.ToDoubleForDisplay()).ToString("F2", CultureInfo.InvariantCulture) + "%";
        static string FuelSummary(IT6AccountingSnapshot from, IT6AccountingSnapshot to)
        {
            int shortSeasons = to.IronEraFuelShortSeasons - from.IronEraFuelShortSeasons;
            int demandSeasons = to.IronEraFuelDemandSeasons - from.IronEraFuelDemandSeasons;
            Fix64 planned = to.IronEraCharcoalPlan - from.IronEraCharcoalPlan;
            Fix64 unmet = to.IronEraCharcoalUnmet - from.IronEraCharcoalUnmet;
            return $"short {shortSeasons}/{demandSeasons} demand-seasons ({Pct(Fix64.FromInt(shortSeasons), Fix64.FromInt(demandSeasons))}); unmet {F(unmet)}/{F(planned)} L planned ({Pct(unmet, planned)})";
        }
        static string FuelVerdict(IT6AccountingSnapshot from, IT6AccountingSnapshot to)
        {
            int demandSeasons = to.IronEraFuelDemandSeasons - from.IronEraFuelDemandSeasons;
            if (demandSeasons < IT6FuelGate.MinDemandSeasons) return $"CENSORED (<{IT6FuelGate.MinDemandSeasons} demand-seasons)";
            Fix64 shortRate = Fix64.FromRatio(to.IronEraFuelShortSeasons - from.IronEraFuelShortSeasons, demandSeasons);
            Fix64 plan = to.IronEraCharcoalPlan - from.IronEraCharcoalPlan;
            Fix64 unmetShare = plan > Fix64.Zero ? (to.IronEraCharcoalUnmet - from.IronEraCharcoalUnmet) / plan : Fix64.Zero;
            return shortRate <= IT6FuelGate.MaxShortRate && unmetShare <= IT6FuelGate.MaxUnmetPlanShare ? "PASS" : "FAIL";
        }
        static Fix64 LocalUse(LocalWoodLedger x) => x.HearthUse + x.FuelUse + x.MetalUse + x.GoodsUse + x.CivicUse + x.StructureUse + x.OtherUse;
        static Fix64 LocalIn(LocalWoodLedger x) => x.ChopIn + x.CivicIn + x.OtherIn;
        ForestLedger endForest = CaptureForest(economy);
        LocalWoodLedger endWood = CaptureLocalWood(economy, endSeason);
        Fix64 forestRegrowth = endForest.Regrowth - startForest.Regrowth;
        Fix64 forestRemoved = (endForest.ChopRemoved - startForest.ChopRemoved) + (endForest.MetalRemoved - startForest.MetalRemoved) +
            (endForest.CivicRemoved - startForest.CivicRemoved) + (endForest.OtherRemoved - startForest.OtherRemoved);
        Fix64 forestDelta = endForest.Standing - startForest.Standing;
        Fix64 forestResidual = forestDelta - (forestRegrowth - forestRemoved);
        Fix64 localStockDelta = endWood.Stock - startWood.Stock;
        Fix64 localFlow = (LocalIn(endWood) - LocalIn(startWood)) - (LocalUse(endWood) - LocalUse(startWood));
        Fix64 localResidual = localStockDelta - localFlow;
        Fix64 woodResidual = forestDelta + localStockDelta - (forestRegrowth - forestRemoved + localFlow);
        int crossings = endForest.Clearings - startForest.Clearings;
        int chopCrossings = endForest.ChopClearings - startForest.ChopClearings;
        int metalCrossings = endForest.MetalClearings - startForest.MetalClearings;
        int civicCrossings = endForest.CivicClearings - startForest.CivicClearings;
        int otherCrossings = endForest.OtherClearings - startForest.OtherClearings;
        int ironCrossings = endForest.IronClearings - startForest.IronClearings;

        output.AppendLine("I-T6 whole-match accounting (read-only trace; world-wide forest and stock ledger):");
        output.AppendLine($"  forest tiles={endForest.Tiles} stock={F(startForest.Standing)}->{F(endForest.Standing)} L; regrowth={F(forestRegrowth)} L; removed chop/metal/civic/other={F(endForest.ChopRemoved - startForest.ChopRemoved)}/{F(endForest.MetalRemoved - startForest.MetalRemoved)}/{F(endForest.CivicRemoved - startForest.CivicRemoved)}/{F(endForest.OtherRemoved - startForest.OtherRemoved)} L (Iron-era subset={F(endForest.IronRemoved - startForest.IronRemoved)} L); residual={F(forestResidual)} L");
        output.AppendLine($"  local wood stores={F(startWood.Stock)}->{F(endWood.Stock)} L; inflow={F(LocalIn(endWood) - LocalIn(startWood))} L; use={F(LocalUse(endWood) - LocalUse(startWood))} L; local residual={F(localResidual)} L; combined residual={F(woodResidual)} L");
        output.AppendLine($"  game clearing rule: each tile crossing from >=5 L to <5 L; crossings={crossings} by final removal category chop/metal-before-MET-09/Iron-era-metal/civic/other={chopCrossings}/{metalCrossings - ironCrossings}/{ironCrossings}/{civicCrossings}/{otherCrossings}; currently cleared tiles={endForest.ClearedAtEnd} (grassland={endForest.GrasslandAtEnd})");

        Fix64 produced11 = Fix64.Zero, produced12 = Fix64.Zero, issued11 = Fix64.Zero, issued12 = Fix64.Zero;
        Fix64 kit11Received = Fix64.Zero, kit12Received = Fix64.Zero, kit11Sent = Fix64.Zero, kit12Sent = Fix64.Zero;
        Fix64 kit11Retired = Fix64.Zero, kit12Retired = Fix64.Zero;
        Fix64 kit11Stock = Fix64.Zero, kit12Stock = Fix64.Zero;
        int kit11Holders = 0, kit12Holders = 0;
        int kit11Breaks = 0, kit12Breaks = 0, kit11Upgrades = 0;
        IT6FuelSiteSnapshot[] kitSites = economy.CaptureIT6FuelSites().ToArray();
        foreach (IT6FuelSiteSnapshot site in kitSites)
        {
            produced11 += site.Kit11Produced; produced12 += site.Kit12Produced;
            issued11 += site.Kit11Issued; issued12 += site.Kit12Issued;
            kit11Received += site.Kit11Received; kit12Received += site.Kit12Received;
            kit11Sent += site.Kit11Sent; kit12Sent += site.Kit12Sent;
            kit11Breaks += site.Kit11Breaks; kit12Breaks += site.Kit12Breaks; kit11Upgrades += site.Kit11Upgrades;
            kit11Retired += site.Kit11Retired; kit12Retired += site.Kit12Retired;
            kit11Holders += site.Kit11Holders; kit12Holders += site.Kit12Holders;
            kit11Stock += site.Kit11Stock; kit12Stock += site.Kit12Stock;
        }
        Fix64 kit11BalanceResidual = produced11 + kit11Received - kit11Sent - Fix64.FromInt(kit11Breaks) - kit11Retired - Fix64.FromInt(kit11Holders) - kit11Stock;
        Fix64 kit12BalanceResidual = produced12 + kit12Received - kit12Sent - Fix64.FromInt(kit12Breaks) - kit12Retired - Fix64.FromInt(kit12Holders) - kit12Stock;
        output.AppendLine($"  kits world: T11 made/received/sent/issued/broken/early-upgrades/retired/held/stored={F(produced11)}/{F(kit11Received)}/{F(kit11Sent)}/{F(issued11)}/{kit11Breaks}/{kit11Upgrades}/{F(kit11Retired)}/{kit11Holders}/{F(kit11Stock)}; residual={F(kit11BalanceResidual)}; T12={F(produced12)}/{F(kit12Received)}/{F(kit12Sent)}/{F(issued12)}/{kit12Breaks}/0/{F(kit12Retired)}/{kit12Holders}/{F(kit12Stock)}; residual={F(kit12BalanceResidual)} kits");
        var siteResiduals = kitSites.Select(site => (Site: site,
            R11: site.Kit11Produced + site.Kit11Received - site.Kit11Sent - Fix64.FromInt(site.Kit11Breaks) - site.Kit11Retired - Fix64.FromInt(site.Kit11Holders) - site.Kit11Stock,
            R12: site.Kit12Produced + site.Kit12Received - site.Kit12Sent - Fix64.FromInt(site.Kit12Breaks) - site.Kit12Retired - Fix64.FromInt(site.Kit12Holders) - site.Kit12Stock))
            .Where(x => x.R11 != Fix64.Zero || x.R12 != Fix64.Zero).ToArray();
        output.AppendLine($"  settlement kit ledgers: {kitSites.Length - siteResiduals.Length}/{kitSites.Length} exact; nonzero residual sites={siteResiduals.Length}");
        foreach (var site in siteResiduals)
            output.AppendLine($"    c{site.Site.Civilization} s{site.Site.SettlementId} T11={F(site.R11)} (made/received/sent/broken/retired/held/stored={F(site.Site.Kit11Produced)}/{F(site.Site.Kit11Received)}/{F(site.Site.Kit11Sent)}/{site.Site.Kit11Breaks}/{F(site.Site.Kit11Retired)}/{site.Site.Kit11Holders}/{F(site.Site.Kit11Stock)}); T12={F(site.R12)}");
        output.AppendLine($"  approved Iron-era fuel cutoff (mature interval, per civ): shortfall rate <= {Pct(IT6FuelGate.MaxShortRate, Fix64.One)}, unmet / planned charcoal <= {Pct(IT6FuelGate.MaxUnmetPlanShare, Fix64.One)}, minimum {IT6FuelGate.MinDemandSeasons} demand-seasons; shortage rate denominator counts positive planned charcoal-production demand after stored stock");
        output.AppendLine("  I-T2 settlement route screen (first kit issue or sister delivery, then 12 complete seasons; fuel follows per-tier kit transfers to sites that produced the supplied tier during the window; relay-only or stock-only sources are censored): qualify over summed producer-site demand-seasons, with at least 12");
        output.AppendLine("    settlement start-end owner MET-09-season workers T11/T12 coverage T11/T12 workers age14-44 age45+ holders11/12 stock11/12 population hub start/end hub F/S site F/S hub-wood-factor hub-reach/baseline site-wood-factor site-reach/baseline made11/12 received11/12 issued11/12 upgrades fuel-sites short/demand rate unmet/plan result");
        int it2Pass = 0, it2Fail = 0, it2Censored = 0;
        foreach (IT2RouteRecord route in it2Routes)
        {
            if (route.End is not { } finish)
            {
                output.AppendLine($"    s{route.SettlementId} {route.StartSeason}–<+12 owner=c{route.Start.Civilization} MET-09={route.UnlockSeason} no 12-season endpoint; CENSORED");
                it2Censored++;
                continue;
            }
            IT6FuelSiteSnapshot start = route.Start;
            IT6FuelSiteSnapshot? fuelEnd = route.FuelEnd;
            var fuelProducerTiers = new HashSet<(int SettlementId, int Tier)>();
            var fuelProducerIds = new HashSet<int>();
            var unprovenSource = false;
            var transfers = economy.CaptureIT6KitTransfers();
            foreach (int tier in new[] { 11, 12 })
            {
                Fix64 startProduced = tier == 11 ? route.Start.Kit11Produced : route.Start.Kit12Produced;
                Fix64 finishProduced = tier == 11 ? finish.Kit11Produced : finish.Kit12Produced;
                if (finishProduced > startProduced) fuelProducerTiers.Add((route.SettlementId, tier));

                var supplyNodes = new HashSet<int> { route.SettlementId };
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (var transfer in transfers)
                    {
                        if (transfer.Tier != tier || transfer.Season < route.StartSeason || transfer.Season > route.EndSeason ||
                            !supplyNodes.Contains(transfer.ToSettlementId)) continue;
                        if (supplyNodes.Add(transfer.FromSettlementId)) expanded = true;
                        fuelProducerTiers.Add((transfer.FromSettlementId, tier));
                    }
                } while (expanded);
            }
            int demand = 0, shortSeasons = 0;
            Fix64 planned = Fix64.Zero, unmet = Fix64.Zero;
            var sourceLabels = new List<string>();
            Dictionary<int, IT6FuelSiteSnapshot>? fuelEndsBySite = route.FuelEndsBySite;
            foreach ((int producerId, int tier) in fuelProducerTiers.OrderBy(x => x.SettlementId).ThenBy(x => x.Tier))
            {
                if (!route.FuelStartsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerStart) ||
                    fuelEndsBySite is null || !fuelEndsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerEnd))
                {
                    unprovenSource = true;
                    continue;
                }
                Fix64 sourceStartProduced = tier == 11 ? producerStart.Kit11Produced : producerStart.Kit12Produced;
                Fix64 sourceEndProduced = tier == 11 ? producerEnd.Kit11Produced : producerEnd.Kit12Produced;
                if (producerStart.Civilization != start.Civilization || producerEnd.Civilization != start.Civilization ||
                    producerStart.Population <= 0 || producerEnd.Population <= 0 || sourceEndProduced <= sourceStartProduced)
                {
                    unprovenSource = true;
                    continue;
                }
                fuelProducerIds.Add(producerId);
            }
            foreach (int producerId in fuelProducerIds.OrderBy(id => id))
            {
                if (!route.FuelStartsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerStart) ||
                    fuelEndsBySite is null || !fuelEndsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerEnd))
                {
                    unprovenSource = true;
                    continue;
                }
                demand += producerEnd.DemandSeasons - producerStart.DemandSeasons;
                shortSeasons += producerEnd.ShortSeasons - producerStart.ShortSeasons;
                planned += producerEnd.Planned - producerStart.Planned;
                unmet += producerEnd.Unmet - producerStart.Unmet;
            }
            sourceLabels.AddRange(fuelProducerIds.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));
            if (fuelProducerIds.Count == 0) unprovenSource = true;
            Fix64 shortRate = demand > 0 ? Fix64.FromRatio(shortSeasons, demand) : Fix64.Zero;
            Fix64 unmetShare = planned > Fix64.Zero ? unmet / planned : Fix64.Zero;
            int workers = finish.ToolWorkers;
            int ironWorkers = finish.T11ToolWorkers + finish.T12ToolWorkers;
            Fix64 coverage = workers > 0 ? Fix64.FromRatio(ironWorkers, workers) : Fix64.Zero;
            string result;
            if (finish.Civilization != start.Civilization) result = "CENSORED captured/owner changed";
            else if (route.UnlockSeason < 0) result = "CENSORED MET-09 not recorded";
            else if (route.HubChanged) result = "CENSORED assigned hub changed";
            else if (fuelEnd is null) result = "CENSORED fuel producer missing";
            else if (unprovenSource) result = "CENSORED kit source not produced in window";
            else if (demand < IT6FuelGate.MinDemandSeasons) result = $"CENSORED fuel demand {demand}/12";
            else if (shortRate > IT6FuelGate.MaxShortRate || unmetShare > IT6FuelGate.MaxUnmetPlanShare) result = "FAIL fuel cutoff";
            else if (workers <= 0) result = "CENSORED no tool workers at endpoint";
            else if (coverage >= Fix64.FromRatio(70, 100)) result = "PASS screen";
            else result = "FAIL coverage";
            if (result.StartsWith("PASS", StringComparison.Ordinal)) it2Pass++;
            else if (result.StartsWith("FAIL", StringComparison.Ordinal)) it2Fail++;
            else it2Censored++;
            output.AppendLine($"    s{route.SettlementId} {route.StartSeason}–{route.EndSeason} c{start.Civilization}->c{finish.Civilization} MET-09={route.UnlockSeason} " +
                $"{workers} {finish.T11ToolWorkers}/{finish.T12ToolWorkers} {Pct(Fix64.FromInt(ironWorkers), Fix64.FromInt(workers))} " +
                $"{finish.T11ToolWorkers14To44}/{finish.T12ToolWorkers14To44} {finish.T11ToolWorkers45Plus}/{finish.T12ToolWorkers45Plus} " +
                $"{finish.Kit11Holders}/{finish.Kit12Holders} {F(finish.Kit11Stock)}/{F(finish.Kit12Stock)} {finish.Population} {route.HubId}/{finish.IronFuelHubId} " +
                $"{fuelEnd?.Forges ?? 0}/{fuelEnd?.Smelters ?? 0} {finish.Forges}/{finish.Smelters} " +
                $"{Pct(fuelEnd?.IronProductionWoodFactor ?? Fix64.Zero, Fix64.One)} {F(fuelEnd?.ReachWoodNow ?? Fix64.Zero)}/{F(fuelEnd?.ForestBaseline ?? Fix64.Zero)} " +
                $"{Pct(finish.IronProductionWoodFactor, Fix64.One)} {F(finish.ReachWoodNow)}/{F(finish.ForestBaseline)} " +
                $"{F(finish.Kit11Produced - start.Kit11Produced)}/{F(finish.Kit12Produced - start.Kit12Produced)} " +
                $"{F(finish.Kit11Received - start.Kit11Received)}/{F(finish.Kit12Received - start.Kit12Received)} " +
                $"{F(finish.Kit11Issued - start.Kit11Issued)}/{F(finish.Kit12Issued - start.Kit12Issued)} {finish.Kit11Upgrades - start.Kit11Upgrades} " +
                $"[{string.Join(',', sourceLabels)}] {shortSeasons}/{demand} {Pct(shortRate, Fix64.One)} {F(unmet)}/{F(planned)} {Pct(unmetShare, Fix64.One)} {result}");
            if (result.StartsWith("FAIL", StringComparison.Ordinal))
            {
                output.AppendLine($"      route kit/workers start={start.T11ToolWorkers + start.T12ToolWorkers}/{start.ToolWorkers} end={ironWorkers}/{workers}; source accounting is T11/T12 made,received,sent,issued,broken,retired,holder-change,stock-change");
                foreach (int producerId in fuelProducerIds.Append(route.SettlementId).Distinct().OrderBy(id => id))
                {
                    if (!route.FuelStartsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerStart) ||
                        fuelEndsBySite is null || !fuelEndsBySite.TryGetValue(producerId, out IT6FuelSiteSnapshot producerEnd)) continue;
                    string role = producerId == route.SettlementId ? "receiver/source" : "producer";
                    output.AppendLine($"        {role} s{producerId}: " +
                        $"T11={F(producerEnd.Kit11Produced - producerStart.Kit11Produced)},{F(producerEnd.Kit11Received - producerStart.Kit11Received)},{F(producerEnd.Kit11Sent - producerStart.Kit11Sent)},{F(producerEnd.Kit11Issued - producerStart.Kit11Issued)},{producerEnd.Kit11Breaks - producerStart.Kit11Breaks},{F(producerEnd.Kit11Retired - producerStart.Kit11Retired)},{producerEnd.Kit11Holders - producerStart.Kit11Holders},{F(producerEnd.Kit11Stock - producerStart.Kit11Stock)}; " +
                        $"T12={F(producerEnd.Kit12Produced - producerStart.Kit12Produced)},{F(producerEnd.Kit12Received - producerStart.Kit12Received)},{F(producerEnd.Kit12Sent - producerStart.Kit12Sent)},{F(producerEnd.Kit12Issued - producerStart.Kit12Issued)},{producerEnd.Kit12Breaks - producerStart.Kit12Breaks},{F(producerEnd.Kit12Retired - producerStart.Kit12Retired)},{producerEnd.Kit12Holders - producerStart.Kit12Holders},{F(producerEnd.Kit12Stock - producerStart.Kit12Stock)}");
                }
                if (fuelHistory is not null && fuelProducerIds.Count > 0)
                {
                    output.AppendLine("      failing-window seasonal kit/worker chronology (per-season deltas; stock and holders are endpoints):");
                    output.AppendLine("      same-civilization relocation scan (new site, population change, or kit transfer/break; helps distinguish household movement from local supply):");
                    var beforeWindow = fuelHistory.LastOrDefault(sample => sample.Season < route.StartSeason).Sites;
                    var previousById = (beforeWindow ?? Array.Empty<IT6FuelSiteSnapshot>())
                        .ToDictionary(site => site.SettlementId);
                    int[] chronologyIds = fuelProducerIds.Append(route.SettlementId).Distinct().OrderBy(id => id).ToArray();
                    foreach ((int sampleSeason, IT6FuelSiteSnapshot[] samples) in fuelHistory)
                    {
                        if (sampleSeason < route.StartSeason || sampleSeason > route.EndSeason) continue;
                        var currentById = samples.ToDictionary(site => site.SettlementId);
                        foreach (int id in chronologyIds)
                        {
                            if (!currentById.TryGetValue(id, out IT6FuelSiteSnapshot now) ||
                                !previousById.TryGetValue(id, out IT6FuelSiteSnapshot before)) continue;
                            output.AppendLine($"        season {sampleSeason} s{id}: workers={now.T11ToolWorkers + now.T12ToolWorkers}/{now.ToolWorkers}, holders={now.Kit11Holders}/{now.Kit12Holders}, stock={F(now.Kit11Stock)}/{F(now.Kit12Stock)}; " +
                                $"T11 made/received/sent/issued/broken/retired={F(now.Kit11Produced - before.Kit11Produced)}/{F(now.Kit11Received - before.Kit11Received)}/{F(now.Kit11Sent - before.Kit11Sent)}/{F(now.Kit11Issued - before.Kit11Issued)}/{now.Kit11Breaks - before.Kit11Breaks}/{F(now.Kit11Retired - before.Kit11Retired)}; " +
                                $"T12 made/received/sent/issued/broken/retired={F(now.Kit12Produced - before.Kit12Produced)}/{F(now.Kit12Received - before.Kit12Received)}/{F(now.Kit12Sent - before.Kit12Sent)}/{F(now.Kit12Issued - before.Kit12Issued)}/{now.Kit12Breaks - before.Kit12Breaks}/{F(now.Kit12Retired - before.Kit12Retired)}");
                        }
                        foreach (IT6FuelSiteSnapshot now in samples.Where(site => site.Civilization == start.Civilization)
                                     .OrderBy(site => site.SettlementId))
                        {
                            bool existed = previousById.TryGetValue(now.SettlementId, out IT6FuelSiteSnapshot before);
                            int previousPopulation = existed ? before.Population : 0;
                            Fix64 received = now.Kit11Received + now.Kit12Received -
                                (existed ? before.Kit11Received + before.Kit12Received : Fix64.Zero);
                            Fix64 sent = now.Kit11Sent + now.Kit12Sent -
                                (existed ? before.Kit11Sent + before.Kit12Sent : Fix64.Zero);
                            int breaks = now.Kit11Breaks + now.Kit12Breaks -
                                (existed ? before.Kit11Breaks + before.Kit12Breaks : 0);
                            bool newLiveSite = !existed && now.Population > 0;
                            bool noteworthy = newLiveSite || now.Population != previousPopulation ||
                                received != Fix64.Zero || sent != Fix64.Zero || breaks != 0;
                            if (noteworthy)
                                output.AppendLine($"        season {sampleSeason} s{now.SettlementId} pop={previousPopulation}->{now.Population}, holders={ (existed ? before.Kit11Holders : 0) + (existed ? before.Kit12Holders : 0)}->{now.Kit11Holders + now.Kit12Holders}, received={F(received)}, sent={F(sent)}, breaks={breaks}, stocks={F(now.Kit11Stock + now.Kit12Stock)}");
                        }
                        previousById = currentById;
                    }
                }
            }
        }
        output.AppendLine($"    I-T2 diagnostic totals: {it2Pass} screen pass, {it2Fail} fail, {it2Censored} censored; endpoint tool-worker coverage uses actual positive toolWork, split by ages 14–44 and 45+");
        output.AppendLine("  civ chain-work share = (mine/charcoal/smelt/camp work + T11/T12 kit crafting + metal-facility construction) / civilian work recorded by the metal ledger; haul is included in chain work and also broken out below:");
        for (int c = 0; c < routeSeason.Length; c++)
        {
            MetalStatistics end = economy.MetalStats(c);
            IT6AccountingSnapshot trace = economy.CaptureIT6Accounting(c);
            Fix64 wholeChainLabor = end.MetalLabor + trace.KitRecipeLabor + trace.MetalFacilityBuildLabor;
            output.AppendLine($"  c{c} {Archetypes[c]} all-match metal/work={F(end.MetalLabor)}/{F(end.Labor)}; full-chain share={Pct(wholeChainLabor, end.Labor)}; kits made/issued T11={F(trace.Kit11Produced)}/{F(trace.Kit11Issued)} T12={F(trace.Kit12Produced)}/{F(trace.Kit12Issued)}; tool-ws used T11/T12={F(trace.Kit11ToolWork)}/{F(trace.Kit12ToolWork)}; breaks T11/T12={trace.Kit11Breaks}/{trace.Kit12Breaks}; T11 upgrades={trace.Kit11Upgrades}; fuel charcoal-made/wood-cut/fire-setting-wood/iron-recipe-charcoal={F(end.Charcoal)}/{F(end.CharcoalWood)}/{F(end.FireSettingWood)}/{F(trace.IronRecipeCharcoal)}; ore mined all/Iron={F(end.OreMined)}/{F(trace.IronOreMined)} L; Iron fire-setting wood={F(trace.IronFireSettingWood)} L; haul-ws charcoal/all-ore/Iron-ore={F(trace.CharcoalHaulLabor)}/{F(trace.OreHaulLabor)}/{F(trace.IronOreHaulLabor)}; metal-facility build-ws={F(trace.MetalFacilityBuildLabor)}; fuel-short settlement-seasons all/Iron-era={trace.MetalFuelShortSeasons}/{trace.IronEraFuelShortSeasons}");
            output.AppendLine($"    post-MET-09 charcoal plan: {FuelSummary(default, trace)}");
            if (routeSeason[c] < 0)
            {
                output.AppendLine("    route window: no MET-09 + issued-Iron-kit exposure");
                continue;
            }
            if (steadySeason[c] < 0)
            {
                output.AppendLine($"    route window: began season {routeSeason[c]}, only {endSeason - routeSeason[c]} seasons after first issue; did not reach the 12-season maturity screen");
                continue;
            }
            Fix64 routeWork = steadyStats[c].Labor - routeStats[c].Labor;
            Fix64 routeChain = (steadyStats[c].MetalLabor - routeStats[c].MetalLabor) +
                (steadyTrace[c].KitRecipeLabor - routeTrace[c].KitRecipeLabor) +
                (steadyTrace[c].MetalFacilityBuildLabor - routeTrace[c].MetalFacilityBuildLabor);
            Fix64 matureWork = end.Labor - steadyStats[c].Labor;
            Fix64 matureChain = (end.MetalLabor - steadyStats[c].MetalLabor) +
                (trace.KitRecipeLabor - steadyTrace[c].KitRecipeLabor) +
                (trace.MetalFacilityBuildLabor - steadyTrace[c].MetalFacilityBuildLabor);
            output.AppendLine($"    route ramp: seasons {routeSeason[c]}–{steadySeason[c]} (12 complete after first issue), chain/work={F(routeChain)}/{F(routeWork)} ({Pct(routeChain, routeWork)}), fuel {FuelSummary(routeTrace[c], steadyTrace[c])}; post-ramp seasons={endSeason - steadySeason[c]}, chain/work={F(matureChain)}/{F(matureWork)} ({Pct(matureChain, matureWork)}), fuel {FuelSummary(steadyTrace[c], trace)}; fuel-cutoff={FuelVerdict(steadyTrace[c], trace)}");
            Fix64 maxRollingShare = Fix64.Zero;
            int rollingWindows = 0;
            int maxWindowStart = -1, maxWindowEnd = -1;
            Fix64 maxWindowChain = Fix64.Zero, maxWindowWork = Fix64.Zero;
            Fix64 maxWindowMetal = Fix64.Zero, maxWindowKitCraft = Fix64.Zero, maxWindowFacility = Fix64.Zero;
            for (int windowEnd = routeSeason[c] + 24; windowEnd <= endSeason; windowEnd++)
            {
                int windowStart = windowEnd - 12;
                MetalStatistics a = statsHistory[c][windowStart], b = statsHistory[c][windowEnd];
                IT6AccountingSnapshot ta = traceHistory[c][windowStart], tb = traceHistory[c][windowEnd];
                Fix64 work = b.Labor - a.Labor;
                if (work <= Fix64.Zero) continue;
                Fix64 metalLabor = b.MetalLabor - a.MetalLabor;
                Fix64 kitCraftLabor = tb.KitRecipeLabor - ta.KitRecipeLabor;
                Fix64 facilityLabor = tb.MetalFacilityBuildLabor - ta.MetalFacilityBuildLabor;
                Fix64 chain = metalLabor + kitCraftLabor + facilityLabor;
                Fix64 share = chain / work;
                if (maxWindowStart < 0 || share > maxRollingShare)
                {
                    maxRollingShare = share;
                    maxWindowStart = windowStart;
                    maxWindowEnd = windowEnd;
                    maxWindowChain = chain;
                    maxWindowWork = work;
                    maxWindowMetal = metalLabor;
                    maxWindowKitCraft = kitCraftLabor;
                    maxWindowFacility = facilityLabor;
                }
                rollingWindows++;
            }
            output.AppendLine($"    mature rolling windows: {rollingWindows} 12-season windows after 12-season route maturity; max share={(rollingWindows == 0 ? "n/a" : Pct(maxRollingShare, Fix64.One))}" +
                (rollingWindows == 0 ? "" : $" at seasons {maxWindowStart}–{maxWindowEnd}, chain/work={F(maxWindowChain)}/{F(maxWindowWork)} (metal/craft/facility={F(maxWindowMetal)}/{F(maxWindowKitCraft)}/{F(maxWindowFacility)} ws)"));
        }
        IT6FuelSiteSnapshot[] fuelSites = economy.CaptureIT6FuelSites().Where(x => x.ShortSeasons > 0).OrderByDescending(x => x.Unmet).Take(12).ToArray();
        if (fuelSites.Length > 0)
        {
            output.AppendLine("  fuel-short settlement hotspots (top 12 by unmet charcoal; wood is measured before each short burn, end-state supply is current at match end):");
            output.AppendLine("    civ site pop short/demand unmet/plan-L mean-reach/usable/in-range/outer/local-wood-at-short-L remaining-work mean-factor baseline/end-reach/end-usable/tiles next-10-reach/raw/usable/tiles nearest-next-forest local-charcoal own/other-settlements-in-reach");
            foreach (IT6FuelSiteSnapshot site in fuelSites)
            {
                Fix64 shortRate = Fix64.FromRatio(site.ShortSeasons, site.DemandSeasons);
                Fix64 unmetShare = site.Planned > Fix64.Zero ? site.Unmet / site.Planned : Fix64.Zero;
                Fix64 count = Fix64.FromInt(site.ShortSeasons);
                output.AppendLine($"    c{site.Civilization} s{site.SettlementId} pop={site.Population} {site.ShortSeasons}/{site.DemandSeasons} ({Pct(shortRate, Fix64.One)}) " +
                    $"{F(site.Unmet)}/{F(site.Planned)} L ({Pct(unmetShare, Fix64.One)}) " +
                    $"{F(site.ShortfallReachWood / count)}/{F(site.ShortfallHarvestableWood / count)}/" +
                    $"{F(site.ShortfallInRangeWood / count)}/{F(site.ShortfallOuterReachWood / count)}/{F(site.ShortfallLocalWood / count)} " +
                    $"work={F(site.ShortfallRemainingWork / count)} " +
                    $"factor={Pct(site.ShortfallWoodFactor / count, Fix64.One)} baseline={F(site.ForestBaseline)} end-reach={F(site.ReachWoodNow)}/usable={F(site.HarvestableWoodNow)} on {site.HarvestableForestTiles} tiles " +
                    $"next-10={F(site.BeyondReachRawWood)}/{F(site.BeyondReachHarvestableWood)} on {site.BeyondReachForestTiles} tiles, nearest={site.NearestBeyondReachForestDistance} " +
                    $"local-charcoal={F(site.CharcoalStock)} own/other-neighbors={site.OwnSettlementsInReach}/{site.OtherSettlementsInReach}");
            }
        }
    }

    private static void AppendIT6SupplyDiagnostic(StringBuilder output, GeneratedWorld world,
        IReadOnlyList<(int Season, IT6FuelSiteSnapshot[] Sites)> history,
        IReadOnlyList<(int Season, int Civilization, IT6FuelSiteSnapshot[] Sites, B13OuterWoodSnapshot[] OuterRows)> catchmentHistory,
        IReadOnlyList<IT6WoodImportOpportunity> importHistory)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        static string Pct(Fix64 numerator, Fix64 denominator) => denominator <= Fix64.Zero
            ? "n/a"
            : (100 * numerator.ToDoubleForDisplay() / denominator.ToDoubleForDisplay()).ToString("F2", CultureInfo.InvariantCulture) + "%";
        WorldEconomyInput economyWorld = WorldEconomyInput.FromGeneratedWorld(world);
        output.AppendLine("  per-season fuel-supply diagnosis (post-season stock plus exact pre-burn deltas recorded on short events; candidate catchments are sampled at first shortage and every 12 seasons):");
        output.AppendLine("  cross-civ import feasibility is inventory-only: event-time known/peaceful/path-reachable sellers' stored wood after the 12 L reserve and four-season generic wood need; third-party route blocks, standing-forest harvest, shipment capacity/labor and a Wood Compact (none exists) are not modelled:");
        var previousBySite = new Dictionary<int, IT6FuelSiteSnapshot>();
        int shortEventCount = 0;
        foreach ((int season, IT6FuelSiteSnapshot[] sites) in history)
        {
            var currentBySite = sites.ToDictionary(site => site.SettlementId);
            IT6FuelSiteSnapshot[] shortEvents = sites.Where(site =>
            {
                IT6FuelSiteSnapshot before = previousBySite.GetValueOrDefault(site.SettlementId);
                return site.ShortSeasons > before.ShortSeasons;
            }).ToArray();
            if (shortEvents.Length == 0)
            {
                previousBySite = currentBySite;
                continue;
            }

            shortEventCount += shortEvents.Length;
            foreach (IGrouping<int, IT6FuelSiteSnapshot> civGroup in shortEvents.GroupBy(site => site.Civilization).OrderBy(group => group.Key))
            {
                int civ = civGroup.Key;
                IT6FuelSiteSnapshot[] civSites = sites.Where(site => site.Civilization == civ).ToArray();
                IT6WoodImportOpportunity[] opportunities = importHistory.Where(item => item.Season == season && item.ConsumerCivilization == civ).ToArray();
                Fix64 woodNeededThisSeason = opportunities.Aggregate(Fix64.Zero, (sum, item) => sum + item.UnmetCharcoal * Fix64.FromInt(6));
                IT6WoodImportSupplier[] reachableWoodSellers = opportunities.SelectMany(item => item.Suppliers)
                    .GroupBy(candidate => candidate.Site.SettlementId)
                    .Select(group => group.OrderBy(candidate => candidate.RouteSeasons).First()).ToArray();
                Fix64 grossForeignWood = reachableWoodSellers.Aggregate(Fix64.Zero, (sum, candidate) => sum + candidate.Site.WoodExportableNow);
                int shortestSupplierRoute = reachableWoodSellers.Select(candidate => candidate.RouteSeasons).DefaultIfEmpty(int.MaxValue).Min();
                Fix64 recipientOreSurplus = opportunities.Aggregate(Fix64.Zero, (sum, item) => sum + item.Consumer.IronOreExportableNow);
                output.AppendLine($"      import upper bound c{civ}: {opportunities.Length} short sites need {F(woodNeededThisSeason)} L wood-equivalent this season; {reachableWoodSellers.Length} unique reachable foreign inventories offer at most {F(grossForeignWood)} L stored wood after reserves ({Pct(Fix64.Min(grossForeignWood, woodNeededThisSeason), woodNeededThisSeason)} of this one-season deficit), shortest route={ (shortestSupplierRoute == int.MaxValue ? "none" : shortestSupplierRoute.ToString(CultureInfo.InvariantCulture) + " seasons") }, recipient estimated spare ore={F(recipientOreSurplus)} L. Optimistic stock ceiling only; supplier stocks may compete across recipients and deliveries arrive after travel.");
                output.AppendLine($"    season {season} c{civ}: {civGroup.Count()} site short-burn event(s)");
                foreach (IGrouping<int, IT6FuelSiteSnapshot> hubGroup in civGroup.GroupBy(site => site.IronFuelHubId).OrderBy(group => group.Key))
                {
                    int hubId = hubGroup.Key;
                    IT6FuelSiteSnapshot hub = currentBySite.GetValueOrDefault(hubId);
                    int assignedCount = civSites.Count(site => site.Population > 0 && site.IronFuelHubId == hubId);
                    int assignedPopulation = civSites.Where(site => site.Population > 0 && site.IronFuelHubId == hubId).Sum(site => site.Population);
                    Fix64 assignedPlan = Fix64.Zero, assignedUnmet = Fix64.Zero;
                    foreach (IT6FuelSiteSnapshot member in civSites.Where(site => site.IronFuelHubId == hubId))
                    {
                        IT6FuelSiteSnapshot before = previousBySite.GetValueOrDefault(member.SettlementId);
                        assignedPlan += member.Planned - before.Planned;
                        assignedUnmet += member.Unmet - before.Unmet;
                    }
                    Fix64 hubKitMade = hub.Kit11Produced + hub.Kit12Produced -
                        previousBySite.GetValueOrDefault(hubId).Kit11Produced - previousBySite.GetValueOrDefault(hubId).Kit12Produced;
                    output.AppendLine($"      hub s{hubId}: assigned={assignedCount} settlements/{assignedPopulation} people, plan/unmet={F(assignedPlan)}/{F(assignedUnmet)} L, kit-made={F(hubKitMade)}, facilities={hub.Forges}/{hub.Smelters}, reach={hub.FuelReach}, post-season wood={F(hub.HarvestableWoodNow)} usable/{hub.ReachWoodNow} raw on {hub.HarvestableForestTiles} tiles, factor={Pct(hub.IronProductionWoodFactor, Fix64.One)}, local wood/charcoal={F(hub.LocalWoodNow)}/{F(hub.CharcoalStock)}");
                    foreach (IT6FuelSiteSnapshot site in hubGroup.OrderBy(item => item.SettlementId))
                    {
                        IT6FuelSiteSnapshot before = previousBySite.GetValueOrDefault(site.SettlementId);
                        Fix64 made = site.Kit11Produced + site.Kit12Produced - before.Kit11Produced - before.Kit12Produced;
                        output.AppendLine($"        s{site.SettlementId}: planned/unmet +{F(site.Planned - before.Planned)}/+{F(site.Unmet - before.Unmet)} L; pre-burn wood reach/usable/in-range/outer/local={F(site.ShortfallReachWood - before.ShortfallReachWood)}/{F(site.ShortfallHarvestableWood - before.ShortfallHarvestableWood)}/{F(site.ShortfallInRangeWood - before.ShortfallInRangeWood)}/{F(site.ShortfallOuterReachWood - before.ShortfallOuterReachWood)}/{F(site.ShortfallLocalWood - before.ShortfallLocalWood)} L; remaining work={F(site.ShortfallRemainingWork - before.ShortfallRemainingWork)}, factor={Pct(site.ShortfallWoodFactor - before.ShortfallWoodFactor, Fix64.One)}; post-season reach usable={F(site.HarvestableWoodNow)} on {site.HarvestableForestTiles} tiles, next forest={site.NearestBeyondReachForestDistance} tiles/{F(site.BeyondReachHarvestableWood)} usable; local wood/charcoal={F(site.LocalWoodNow)}/{F(site.CharcoalStock)}; facilities={site.Forges}/{site.Smelters}, kit-made={F(made)}");
                    }
                }

                foreach (IT6FuelSiteSnapshot consumer in civGroup.OrderBy(site => site.SettlementId))
                {
                    IT6FuelSiteSnapshot before = previousBySite.GetValueOrDefault(consumer.SettlementId);
                    Fix64 unmetThisSeason = consumer.Unmet - before.Unmet;
                    if (unmetThisSeason <= Fix64.Zero) continue;
                    Fix64 woodNeeded = unmetThisSeason * Fix64.FromInt(6);
                    IT6WoodImportOpportunity? opportunity = importHistory.FirstOrDefault(item => item.Season == season && item.ConsumerId == consumer.SettlementId);
                    if (opportunity is null || opportunity.Suppliers.Length == 0)
                    {
                        output.AppendLine($"      import feasibility s{consumer.SettlementId}: unmet={F(unmetThisSeason)} L charcoal needs {F(woodNeeded)} L wood for this season; no event-time known, peaceful, path-reachable foreign settlement has stored wood surplus");
                        continue;
                    }
                    output.AppendLine($"      import feasibility s{consumer.SettlementId}: unmet={F(unmetThisSeason)} L charcoal needs {F(woodNeeded)} L wood for this season; recipient estimated spare ore={F(consumer.IronOreExportableNow)} L; event-time known/peaceful/path-reachable stored-wood sellers:");
                    foreach (IT6WoodImportSupplier candidate in opportunity.Suppliers.Take(3))
                    {
                        IT6FuelSiteSnapshot seller = candidate.Site;
                        output.AppendLine($"        c{seller.Civilization}/s{seller.SettlementId} route={candidate.RouteSeasons} seasons, wood cargo ceiling={F(seller.WoodExportableNow)} L ({Pct(Fix64.Min(seller.WoodExportableNow, woodNeeded), woodNeeded)} of this one-season fuel gap), seller estimated spare ore={F(seller.IronOreExportableNow)} L");
                    }
                }

                var sample = catchmentHistory.FirstOrDefault(item => item.Season == season && item.Civilization == civ);
                if (sample.Sites is null || sample.OuterRows is null) continue;
                IT6FuelSiteSnapshot[] candidates = sample.Sites;
                if (candidates.Length == 0) continue;
                var outerRows = sample.OuterRows.Where(row => row.IsForest && candidates.Any(candidate => candidate.SettlementId == row.SettlementId)).ToArray();
                var perCandidate = new List<(IT6FuelSiteSnapshot Site, Fix64 Usable, Fix64 Growth)>();
                Fix64 duplicatedUsable = Fix64.Zero, duplicatedGrowth = Fix64.Zero;
                foreach (IT6FuelSiteSnapshot candidate in candidates)
                {
                    Fix64 usable = Fix64.Zero, growth = Fix64.Zero;
                    foreach (B13OuterWoodSnapshot row in outerRows.Where(row => row.SettlementId == candidate.SettlementId))
                    {
                        usable += Fix64.Max(Fix64.Zero, row.Standing - Fix64.FromInt(5));
                        growth += economyWorld.Tile(row.Tile).WoodGrowthPerYear;
                    }
                    perCandidate.Add((candidate, usable, growth));
                    duplicatedUsable += usable;
                    duplicatedGrowth += growth;
                }
                var uniqueRows = outerRows.GroupBy(row => row.Tile).Select(group => group.First()).ToArray();
                Fix64 uniqueUsable = Fix64.Zero, uniqueGrowth = Fix64.Zero;
                foreach (B13OuterWoodSnapshot row in uniqueRows)
                {
                    uniqueUsable += Fix64.Max(Fix64.Zero, row.Standing - Fix64.FromInt(5));
                    uniqueGrowth += economyWorld.Tile(row.Tile).WoodGrowthPerYear;
                }
                output.AppendLine($"      eligible forge+smelter catchments: {candidates.Length}; outer-forest usable summed/unique={F(duplicatedUsable)}/{F(uniqueUsable)} L, annual growth summed/unique={F(duplicatedGrowth)}/{F(uniqueGrowth)} L/year ({uniqueRows.Length} unique tiles; local radius-three wood excluded)");
                foreach ((IT6FuelSiteSnapshot candidate, Fix64 usable, Fix64 growth) in perCandidate.OrderByDescending(item => item.Usable).ThenBy(item => item.Site.SettlementId))
                {
                    IT6FuelSiteSnapshot[] members = civSites.Where(site => site.IronFuelHubId == candidate.SettlementId && site.Population > 0).ToArray();
                    Fix64 plan = Fix64.Zero, kitMade = Fix64.Zero;
                    foreach (IT6FuelSiteSnapshot member in members)
                    {
                        IT6FuelSiteSnapshot before = previousBySite.GetValueOrDefault(member.SettlementId);
                        plan += member.Planned - before.Planned;
                        kitMade += member.Kit11Produced - before.Kit11Produced + member.Kit12Produced - before.Kit12Produced;
                    }
                    output.AppendLine($"        s{candidate.SettlementId} serves={members.Length} plan={F(plan)} L kit-made={F(kitMade)} local={F(usable)} usable/{F(growth)} L/year; total reach={F(candidate.HarvestableWoodNow)} usable, factor={Pct(candidate.IronProductionWoodFactor, Fix64.One)}, reach={candidate.FuelReach}");
                }
            }
            previousBySite = currentBySite;
        }
        output.AppendLine($"    sampled {history.Count} seasons; observed {shortEventCount} site-season short events");
        AppendIT6TimedImportFeasibility(output, history, importHistory);
        AppendIT6PhysicalSupplierFeasibility(output, history, importHistory);
    }

    private static void AppendIT6PhysicalSupplierFeasibility(StringBuilder output,
        IReadOnlyList<(int Season, IT6FuelSiteSnapshot[] Sites)> history,
        IReadOnlyList<IT6WoodImportOpportunity> opportunities)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        static string Pct(Fix64 numerator, Fix64 denominator) => denominator <= Fix64.Zero
            ? "n/a"
            : (100 * numerator.ToDoubleForDisplay() / denominator.ToDoubleForDisplay()).ToString("F2", CultureInfo.InvariantCulture) + "%";

        output.AppendLine("  bounded physical supplier check: event-time seller Iron-haul catchment where traced; non-Iron sellers contribute stored inventory only. Reachable standing stock and annual growth exclude rival-held forest but are ceilings, not deliverable surplus (labor, domestic draw and overlapping Iron catchments are not subtracted):");
        if (history.Count == 0)
        {
            output.AppendLine("    no seller catchment snapshots");
            return;
        }

        int reported = 0;
        foreach (IT6WoodImportOpportunity opportunity in opportunities.OrderBy(item => item.Season).ThenBy(item => item.ConsumerId))
        {
            if (opportunity.Season < 280) continue;
            if (reported++ >= 12) break;
            Fix64 needed = opportunity.UnmetCharcoal * Fix64.FromInt(6);
            output.AppendLine($"    shortage s{opportunity.ConsumerId} c{opportunity.ConsumerCivilization} season={opportunity.Season} wood-equivalent-needed={F(needed)} L; route-feasible suppliers at event:");
            if (opportunity.Suppliers.Length == 0)
            {
                output.AppendLine("      none");
                continue;
            }
            foreach (IT6WoodImportSupplier supplier in opportunity.Suppliers.Take(3))
            {
                int departBy = Math.Max(280, opportunity.Season - supplier.RouteSeasons);
                var departureSample = history.Where(sample => sample.Season <= departBy)
                    .OrderByDescending(sample => sample.Season).FirstOrDefault();
                IT6FuelSiteSnapshot? sellerAtDeparture = departureSample.Sites is null
                    ? null
                    : departureSample.Sites.Where(site => site.SettlementId == supplier.Site.SettlementId)
                        .Select(site => (IT6FuelSiteSnapshot?)site).FirstOrDefault();
                if (sellerAtDeparture is null)
                {
                    output.AppendLine($"      c{supplier.Site.Civilization}/s{supplier.Site.SettlementId}: route={supplier.RouteSeasons}, target departure s{departBy}; no seller snapshot");
                    continue;
                }
                Fix64 storedExportable = sellerAtDeparture.Value.WoodExportableNow;
                Fix64 routeStanding = sellerAtDeparture.Value.HarvestableWoodNow;
                int sellerHubId = sellerAtDeparture.Value.IronFuelHubId;
                IT6FuelSiteSnapshot? sellerHub = (departureSample.Sites ?? Array.Empty<IT6FuelSiteSnapshot>())
                    .Where(site => site.SettlementId == sellerHubId)
                    .Select(site => (IT6FuelSiteSnapshot?)site).FirstOrDefault();
                string sharingSummary = sellerHubId == sellerAtDeparture.Value.SettlementId
                    ? "local catchment"
                    : $"assigned hub s{sellerHubId} IronReach={sellerHub?.FuelReach ?? 0}";
                string reachSummary = sellerAtDeparture.Value.FuelReach > 0
                    ? $"site IronReach={sellerAtDeparture.Value.FuelReach}, {sharingSummary}, reachable forest={F(sellerAtDeparture.Value.ReachWoodNow)} L raw/{F(routeStanding)} L above tile reserve, {sellerAtDeparture.Value.HarvestableForestTiles} tiles, standing growth={F(sellerAtDeparture.Value.ReachWoodGrowthPerYear)} L/year"
                    : "no Iron-haul catchment trace for this seller";
                output.AppendLine($"      c{supplier.Site.Civilization}/s{supplier.Site.SettlementId}: {reachSummary}, route={supplier.RouteSeasons}, target departure s{departBy}, seller sample s{departureSample.Season}; stored wood above reserve/need={F(storedExportable)} L; stock+traced standing could cover {Pct(Fix64.Min(storedExportable + routeStanding, needed), needed)} of this event's gap before labor, domestic draw, harvest cap, route-haul work, and overlapping Iron users");
            }
        }
        if (opportunities.Count == 0)
            output.AppendLine("    no charcoal shortfall events to compare against physical supplier catchments");
    }

    private static void AppendIT6TimedImportFeasibility(StringBuilder output,
        IReadOnlyList<(int Season, IT6FuelSiteSnapshot[] Sites)> history,
        IReadOnlyList<IT6WoodImportOpportunity> opportunities)
    {
        static string F(Fix64 value) => value.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture);
        static string Pct(Fix64 numerator, Fix64 denominator) => denominator <= Fix64.Zero
            ? "n/a"
            : (100 * numerator.ToDoubleForDisplay() / denominator.ToDoubleForDisplay()).ToString("F2", CultureInfo.InvariantCulture) + "%";

        output.AppendLine("  optimistic stored-inventory counterfactual (not a formal maximum or implemented trade): greedy shipments respond only after observed unmet charcoal, debit unique seller inventory, preserve the stored-wood reserve, and arrive after the captured route duration. It assumes delivered wood immediately covers observed unmet charcoal; it omits cargo cap, trader labor, payment, third-party blockade, loss, standing-forest harvest, and compact hub constraints:");
        if (history.Count == 0)
        {
            output.AppendLine("    no seasonal snapshots; counterfactual unavailable");
            return;
        }

        var stats = new Dictionary<int, IT6TimedImportStats>();
        var previousSites = new Dictionary<int, IT6FuelSiteSnapshot>();
        var sellerBaselineSurplus = new Dictionary<int, Fix64>();
        var sellerCounterfactualStock = new Dictionary<int, Fix64>();
        var importedWoodStock = new Dictionary<int, Fix64>();
        var residualWoodDemandBySite = new Dictionary<int, Fix64>();
        var inTransit = new List<IT6TimedWoodShipment>();
        var currentSites = new Dictionary<int, IT6FuelSiteSnapshot>();
        long minLoadRaw = Fix64.FromRatio(1, 100).Raw;

        IT6TimedImportStats GetStats(int civilization)
        {
            if (!stats.TryGetValue(civilization, out IT6TimedImportStats? value))
            {
                value = new IT6TimedImportStats();
                stats[civilization] = value;
            }
            return value;
        }

        foreach ((int season, IT6FuelSiteSnapshot[] sites) in history)
        {
            currentSites.Clear();
            foreach (IT6FuelSiteSnapshot site in sites) currentSites[site.SettlementId] = site;

            foreach (IT6FuelSiteSnapshot site in sites)
            {
                Fix64 baseSurplus = site.Population > 0 ? site.WoodExportableNow : Fix64.Zero;
                if (sellerBaselineSurplus.TryGetValue(site.SettlementId, out Fix64 priorBase))
                {
                    Fix64 available = sellerCounterfactualStock.GetValueOrDefault(site.SettlementId);
                    sellerCounterfactualStock[site.SettlementId] = Fix64.Max(Fix64.Zero, available + baseSurplus - priorBase);
                }
                else sellerCounterfactualStock[site.SettlementId] = baseSurplus;
                sellerBaselineSurplus[site.SettlementId] = baseSurplus;
            }

            for (int i = inTransit.Count - 1; i >= 0; i--)
            {
                IT6TimedWoodShipment shipment = inTransit[i];
                if (shipment.ArrivalSeason > season) continue;
                inTransit.RemoveAt(i);
                if (!currentSites.TryGetValue(shipment.BuyerId, out IT6FuelSiteSnapshot receiver) || receiver.Population <= 0) continue;
                importedWoodStock[shipment.BuyerId] = importedWoodStock.GetValueOrDefault(shipment.BuyerId) + shipment.Quantity;
                IT6TimedImportStats buyerStats = GetStats(shipment.BuyerCivilization);
                buyerStats.Arrived += shipment.Quantity;
            }

            residualWoodDemandBySite.Clear();
            foreach (IT6FuelSiteSnapshot site in sites.OrderBy(item => item.SettlementId))
            {
                IT6FuelSiteSnapshot before = previousSites.GetValueOrDefault(site.SettlementId);
                int demandDelta = Math.Max(0, site.DemandSeasons - before.DemandSeasons);
                if (demandDelta == 0) continue;
                Fix64 plannedDelta = Fix64.Max(Fix64.Zero, site.Planned - before.Planned);
                Fix64 unmetDelta = Fix64.Min(plannedDelta, Fix64.Max(Fix64.Zero, site.Unmet - before.Unmet));
                int shortDelta = Math.Max(0, site.ShortSeasons - before.ShortSeasons);
                Fix64 woodNeed = unmetDelta * Fix64.FromInt(6);
                Fix64 held = importedWoodStock.GetValueOrDefault(site.SettlementId);
                Fix64 used = Fix64.Min(held, woodNeed);
                importedWoodStock[site.SettlementId] = held - used;
                Fix64 remainingUnmet = Fix64.Max(Fix64.Zero, unmetDelta - used / Fix64.FromInt(6));

                IT6TimedImportStats civStats = GetStats(site.Civilization);
                civStats.DemandSeasons += demandDelta;
                civStats.Planned += plannedDelta;
                civStats.BaselineUnmet += unmetDelta;
                civStats.CounterfactualUnmet += remainingUnmet;
                civStats.BaselineShortSeasons += shortDelta;
                if (shortDelta > 0 && (unmetDelta <= Fix64.Zero || remainingUnmet > Fix64.Zero))
                    civStats.CounterfactualShortSeasons += shortDelta;
                civStats.UsedAgainstFuel += used;
                residualWoodDemandBySite[site.SettlementId] = remainingUnmet * Fix64.FromInt(6);
            }

            foreach (IT6WoodImportOpportunity opportunity in opportunities.Where(item => item.Season == season)
                         .OrderBy(item => item.ConsumerCivilization).ThenBy(item => item.ConsumerId))
            {
                Fix64 need = residualWoodDemandBySite.GetValueOrDefault(opportunity.ConsumerId);
                if (need <= Fix64.Zero) continue;
                IT6TimedImportStats civStats = GetStats(opportunity.ConsumerCivilization);
                foreach (IT6WoodImportSupplier supplier in opportunity.Suppliers)
                {
                    if (need.Raw < minLoadRaw) break;
                    Fix64 stock = sellerCounterfactualStock.GetValueOrDefault(supplier.Site.SettlementId);
                    Fix64 quantity = Fix64.Min(need, stock);
                    if (quantity.Raw < minLoadRaw) continue;
                    sellerCounterfactualStock[supplier.Site.SettlementId] = stock - quantity;
                    civStats.Shipped += quantity;
                    inTransit.Add(new IT6TimedWoodShipment(season + Math.Max(1, supplier.RouteSeasons),
                        supplier.Site.SettlementId, opportunity.ConsumerId, opportunity.ConsumerCivilization, quantity));
                    need -= quantity;
                }
            }

            foreach (IT6FuelSiteSnapshot site in sites) previousSites[site.SettlementId] = site;
        }

        foreach (IT6TimedWoodShipment shipment in inTransit)
        {
            IT6TimedImportStats civStats = GetStats(shipment.BuyerCivilization);
            civStats.InTransit += shipment.Quantity;
        }
        foreach ((int settlementId, Fix64 wood) in importedWoodStock)
            if (currentSites.TryGetValue(settlementId, out IT6FuelSiteSnapshot site) && site.Population > 0)
            {
                IT6TimedImportStats civStats = GetStats(site.Civilization);
                civStats.EndingWoodStock += wood;
            }

        foreach ((int civ, IT6TimedImportStats result) in stats.OrderBy(pair => pair.Key))
            output.AppendLine($"    c{civ}: baseline={result.BaselineShortSeasons}/{result.DemandSeasons} short, {F(result.BaselineUnmet)}/{F(result.Planned)} L unmet ({Pct(result.BaselineUnmet, result.Planned)}); optimistic stored-inventory scenario={result.CounterfactualShortSeasons}/{result.DemandSeasons} short, {F(result.CounterfactualUnmet)}/{F(result.Planned)} L unmet ({Pct(result.CounterfactualUnmet, result.Planned)}); shipped/arrived/used/stored/in-transit={F(result.Shipped)}/{F(result.Arrived)}/{F(result.UsedAgainstFuel)}/{F(result.EndingWoodStock)}/{F(result.InTransit)} L");
    }
}
