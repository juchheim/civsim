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
        public required IT6FuelSiteSnapshot Start;
        public IT6FuelSiteSnapshot? End;
        public int EndSeason = -1;
    }

    public static int Run(string[] args)
    {
        string? packPath = null;
        ulong first = 20261019;
        int seeds = 1, minutes = 110, capYears = ChapterRules.IronCapYear, countdown = ChapterRules.IronCountdownSeasons;
        bool grantE4 = false;
        bool it6Accounting = false;
        bool bronzeControl = false;
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
                case "--bronze-control": bronzeControl = true; break;
                default:
                    Console.Error.WriteLine("usage: civsim-harness iron-match --pack FILE [--seed N] [--seeds N] [--minutes N] [--cap-years N] [--countdown SEASONS] [--grant-e4] [--it6-accounting] [--bronze-control]");
                    return 2;
            }
        }
        if (packPath is null) throw new ArgumentException("iron-match needs --pack FILE.");
        ContentPack pack = ContentPack.Load(File.ReadAllBytes(packPath));
        for (int n = 0; n < seeds; n++) Console.Write(Play(first + (ulong)n, pack, minutes, capYears, countdown, grantE4, it6Accounting, bronzeControl));
        return 0;
    }

    private static string Play(ulong seed, ContentPack pack, int minutes, int capYears, int countdown, bool grantE4, bool it6Accounting, bool bronzeControl)
    {
        GeneratedWorld world = WorldGenerator.Generate(seed);
        var controls = new CivControl[Archetypes.Length];
        for (int c = 0; c < controls.Length; c++) controls[c] = CivControl.Ai(Difficulty.Warlord, Archetypes[c]);
        ChapterRules rules = bronzeControl ? ChapterRules.Bronze :
            capYears == ChapterRules.IronCapYear && countdown == ChapterRules.IronCountdownSeasons ? ChapterRules.Iron : ChapterRules.IronWithLimits(capYears, countdown);
        MatchModel match = MatchModel.Create(seed, pack, world, Archetypes, controls, rules: rules);
        match.Economy.B13WoodTraceEnabled = it6Accounting;
        match.Economy.IT6AccountingTraceEnabled = it6Accounting;
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
                foreach (IT6FuelSiteSnapshot site in match.Economy.CaptureIT6FuelSites())
                {
                    if (!it2Routes.TryGetValue(site.SettlementId, out IT2RouteRecord? route) &&
                        site.Kit11Issued + site.Kit12Issued > Fix64.Zero)
                    {
                        int unlockSeason = research.IsDone(site.Civilization, met09)
                            ? checked((int)(research.CompletedTick(site.Civilization, met09) / 150L))
                            : -1;
                        route = new IT2RouteRecord
                        {
                            SettlementId = site.SettlementId,
                            StartSeason = season,
                            UnlockSeason = unlockSeason,
                            Start = site,
                        };
                        it2Routes.Add(site.SettlementId, route);
                    }
                    if (route is not null && route.End is null && season >= route.StartSeason + 12)
                    {
                        route.End = site;
                        route.EndSeason = season;
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
        if (it6Accounting) AppendIT6Accounting(o, (int)(runner.Tick / 150L), match.Economy, startForest, startWood,
            routeSeason, steadySeason, routeStats, routeTrace, steadyStats, steadyTrace, statsHistory, traceHistory,
            it2Routes.Values.OrderBy(x => x.SettlementId).ToArray());
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
        List<MetalStatistics>[] statsHistory, List<IT6AccountingSnapshot>[] traceHistory, IT2RouteRecord[] it2Routes)
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
        foreach (IT6FuelSiteSnapshot site in economy.CaptureIT6FuelSites())
        {
            kit11Received += site.Kit11Received; kit12Received += site.Kit12Received;
            kit11Sent += site.Kit11Sent; kit12Sent += site.Kit12Sent;
        }
        for (int c = 0; c < routeSeason.Length; c++)
        {
            IT6AccountingSnapshot t = economy.CaptureIT6Accounting(c);
            produced11 += t.Kit11Produced; produced12 += t.Kit12Produced;
            issued11 += t.Kit11Issued; issued12 += t.Kit12Issued;
            kit11Breaks += t.Kit11Breaks; kit12Breaks += t.Kit12Breaks; kit11Upgrades += t.Kit11Upgrades;
            kit11Retired += t.Kit11Retired; kit12Retired += t.Kit12Retired;
            (int t11, int t12, _) = economy.IronKitHolders(c);
            kit11Holders += t11; kit12Holders += t12;
            for (int s = 0; s < economy.SettlementCount; s++)
            {
                if (economy.SettlementLite(s).Civilization != c) continue;
                kit11Stock += economy.GoodStock(s, "G-TOOL11");
                kit12Stock += economy.GoodStock(s, "G-TOOL12");
            }
        }
        Fix64 kit11BalanceResidual = produced11 + kit11Received - kit11Sent - Fix64.FromInt(kit11Breaks + kit11Upgrades) - kit11Retired - Fix64.FromInt(kit11Holders) - kit11Stock;
        Fix64 kit12BalanceResidual = produced12 + kit12Received - kit12Sent - Fix64.FromInt(kit12Breaks) - kit12Retired - Fix64.FromInt(kit12Holders) - kit12Stock;
        output.AppendLine($"  kits world: T11 made/received/sent/issued/broken/upgraded/retired/held/stored={F(produced11)}/{F(kit11Received)}/{F(kit11Sent)}/{F(issued11)}/{kit11Breaks}/{kit11Upgrades}/{F(kit11Retired)}/{kit11Holders}/{F(kit11Stock)}; residual={F(kit11BalanceResidual)}; T12={F(produced12)}/{F(kit12Received)}/{F(kit12Sent)}/{F(issued12)}/{kit12Breaks}/0/{F(kit12Retired)}/{kit12Holders}/{F(kit12Stock)}; residual={F(kit12BalanceResidual)} kits");
        output.AppendLine($"  approved Iron-era fuel cutoff (mature interval, per civ): shortfall rate <= {Pct(IT6FuelGate.MaxShortRate, Fix64.One)}, unmet / planned charcoal <= {Pct(IT6FuelGate.MaxUnmetPlanShare, Fix64.One)}, minimum {IT6FuelGate.MinDemandSeasons} demand-seasons; shortage rate denominator counts positive planned charcoal-production demand after stored stock");
        output.AppendLine("  I-T2 settlement route screen (first local kit issue, then 12 complete seasons; this is a diagnostic run, not an approved I-T2 balance verdict): a qualified window must meet the approved fuel cutoff with 12/12 demand-seasons");
        output.AppendLine("    settlement start-end owner MET-09-season workers T11/T12 coverage T11/T12 workers age14-44 age45+ made11/12 received11/12 issued11/12 upgrades fuel-short/demand rate unmet/plan result");
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
            int demand = finish.DemandSeasons - start.DemandSeasons;
            int shortSeasons = finish.ShortSeasons - start.ShortSeasons;
            Fix64 planned = finish.Planned - start.Planned;
            Fix64 unmet = finish.Unmet - start.Unmet;
            Fix64 shortRate = demand > 0 ? Fix64.FromRatio(shortSeasons, demand) : Fix64.Zero;
            Fix64 unmetShare = planned > Fix64.Zero ? unmet / planned : Fix64.Zero;
            int workers = finish.ToolWorkers;
            int ironWorkers = finish.T11ToolWorkers + finish.T12ToolWorkers;
            Fix64 coverage = workers > 0 ? Fix64.FromRatio(ironWorkers, workers) : Fix64.Zero;
            string result;
            if (finish.Civilization != start.Civilization) result = "CENSORED captured/owner changed";
            else if (route.UnlockSeason < 0) result = "CENSORED MET-09 not recorded";
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
                $"{F(finish.Kit11Produced - start.Kit11Produced)}/{F(finish.Kit12Produced - start.Kit12Produced)} " +
                $"{F(finish.Kit11Received - start.Kit11Received)}/{F(finish.Kit12Received - start.Kit12Received)} " +
                $"{F(finish.Kit11Issued - start.Kit11Issued)}/{F(finish.Kit12Issued - start.Kit12Issued)} {finish.Kit11Upgrades - start.Kit11Upgrades} " +
                $"{shortSeasons}/{demand} {Pct(shortRate, Fix64.One)} {F(unmet)}/{F(planned)} {Pct(unmetShare, Fix64.One)} {result}");
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
}
