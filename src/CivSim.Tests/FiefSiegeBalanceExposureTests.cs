using System;
using System.Linq;
using System.IO;
using System.Text;
using CivSim.Harness;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>Controlled live Fief siege exposure; this is a mechanic diagnostic, not whole-match acceptance.</summary>
public sealed class FiefSiegeBalanceExposureTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() =>
        TestPack.WithNumeric("KEY-5", "WAR-09", "WAR-12", "CHM-07", "DEF-05", "DEF-08", "DEF-09"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(BuildNearbyStartWorld);
    private static readonly EconomyArchetype[] Archetypes =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private readonly ITestOutputHelper _output;
    public FiefSiegeBalanceExposureTests(ITestOutputHelper output) => _output = output;

    private sealed class Rig
    {
        public readonly MatchModel Match;
        public readonly SimRunner Runner;
        public Rig()
        {
            Match = MatchModel.Create(Seed, Pack.Value, World.Value, Archetypes, default, rules: ChapterRules.Fief);
            Runner = new SimRunner(Match);
        }

        public void Grant(int civ, params string[] projects)
        {
            foreach (string id in projects)
            {
                int index = Catalog.Value.IndexOf(id);
                Assert.True(index >= 0, $"{id} must exist in the fixture pack");
                Runner.Submit(new Command(Runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
                Runner.Advance(1);
            }
        }

        public int SettlementOf(int civ) => Enumerable.Range(0, Match.Economy.SettlementCount)
            .First(s => Match.Economy.SettlementLite(s).Civilization == civ && !Match.Politics.IsFreeFolk(s));
        public void Season() => Runner.Advance(150);
    }

    private static GeneratedWorld BuildNearbyStartWorld()
    {
        GeneratedWorld source = WorldGenerator.Generate(Seed);
        WorldTile[] tiles = source.Tiles.ToArray();
        WorldStart[] starts = source.Starts.ToArray();
        bool[] passable = HexGrid.BuildWalkingPassability(tiles, source.Width, source.Height);
        int targetStart = starts.Single(s => s.Civilization == 1).Tile;
        int[] distance = HexGrid.WalkingDistancesFrom(source.Width, source.Height, passable, targetStart);
        var occupied = starts.Select(s => s.Tile).ToHashSet();
        int nearby = Enumerable.Range(0, tiles.Length)
            .Where(t => !occupied.Contains(t) && tiles[t].IsLand && tiles[t].Biome != Biome.Peak && distance[t] is >= 5 and <= 7)
            .OrderBy(t => distance[t]).ThenBy(t => t).First();
        int attacker = Array.FindIndex(starts, s => s.Civilization == 0);
        starts[attacker] = starts[attacker] with { Tile = nearby };
        return new GeneratedWorld(source.Seed, source.Settings, tiles, starts, source.Herds.ToArray(), source.Predators.ToArray(), source.Report);
    }

    private static (Rig Rig, int Home, int Target) Prepared(bool cannon, bool engine, bool concentric, int cannonCount = 2)
    {
        var rig = new Rig();
        rig.Grant(0, "KEY-5", "WAR-09", "WAR-12", "CHM-07");
        rig.Grant(1, "KEY-5", "DEF-05", "DEF-08", "DEF-09");
        rig.Season();
        int home = rig.SettlementOf(0), target = rig.SettlementOf(1);
        LivingEconomyModel economy = rig.Match.Economy;
        economy.FixtureAddPeople(home, 240, 24);
        economy.FixtureSetGoodStock(home, "G-GRAIN", Fix64.FromInt(1000));
        economy.FixtureSetGoodStock(target, "G-GRAIN", Fix64.FromInt(1000));
        economy.FixtureSetGoodStock(home, "G-CHAR", Fix64.FromInt(30));
        if (cannon) economy.FixtureSetGoodStock(home, "G-CANNON", Fix64.FromInt(cannonCount));
        if (engine) economy.FixtureSetGoodStock(home, "G-ENGINE", Fix64.FromInt(2));
        if (cannon) economy.FixtureSetGoodStock(home, "G-POWDER", cannonCount == 1 ? Fix64.FromRatio(1, 4) : Fix64.One);
        economy.FixtureBuildStructure(target, "stone_walls");
        Assert.True(economy.FixtureHasStoneWalls(target));
        if (concentric)
        {
            economy.FixtureBuildIronStructure(target, "castle");
            economy.FixtureBuildIronStructure(target, "concentric_castle");
            economy.FixtureSetSoldiersSupplied(target, 8);
        }
        rig.Match.Economy.FixtureSetGoodStock(home, "G-GRAIN", Fix64.FromInt(1000));
        rig.Match.Politics.SetExternalMind(0, true);
        rig.Match.Politics.SetExternalMind(1, true);
        rig.Match.Politics.ForceContact(0, 1);
        rig.Match.Politics.SetWar(0, 1, true);
        Assert.True(rig.Match.Politics.OrderAssault(0, target));
        int arrival = rig.Match.Politics.Expeditions.Single(e => e.Kind == ExpeditionKind.Assault).ArriveSeason;
        rig.Runner.Advance(150 * (arrival - rig.Match.Politics.Season));
        Assert.True(rig.Match.Politics.InvestmentAt(target) is not null,
            $"controlled siege did not begin; season={rig.Match.Politics.Season}; events=" +
            string.Join(" | ", rig.Match.Politics.Events.Select(e => $"{e.Kind}:c{e.Civ}:other{e.Other}:a{e.A}:b{e.B}")));
        return (rig, home, target);
    }

    [Fact]
    public void One_powdered_cannon_fires_one_shot_spends_exactly_one_quarter_and_assigns_four_adults_once()
    {
        (Rig rig, int home, int target) = Prepared(cannon: true, engine: false, concentric: true, cannonCount: 1);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        Assert.Equal(1, siege.Cannons);
        Assert.Equal(Fix64.Zero, rig.Match.Economy.FixtureGoodStock(home, "G-CANNON"));
        Defenses garrisoned = rig.Match.Politics.FixtureDefensesOf(target);
        Assert.True(garrisoned.Castle && garrisoned.ConcentricCastle);
        Assert.Equal((garrisoned with { ConcentricCastle = false }).Multiplier() * Defenses.ConcentricCastleMultiplier,
            garrisoned.Multiplier());

        var observations = new System.Collections.Generic.List<SiegeTraceObservation>();
        var crewDeaths = new System.Collections.Generic.List<long>();
        rig.Match.Politics.SiegeTraceObserverForDiagnostics = observations.Add;
        rig.Match.Politics.CombatCasualtyObserverForDiagnostics = (figure, kind, dead, _) =>
        {
            if (kind == "siegeCrewAttrition" && dead) crewDeaths.Add(figure.Id);
        };

        rig.Season();

        SiegeTraceObservation observation = observations[0];
        Assert.Equal(1, observation.CannonShots);
        Assert.Equal(1, observation.CannonCrews);
        Assert.Equal(4, observation.AttritionAssignedCrews);
        Assert.True(observation.Def09Active);
        Assert.Equal(Fix64.FromRatio(1, 4), observation.PowderPaid);
        Assert.Equal(Fix64.Zero, rig.Match.Economy.FixtureGoodStock(home, "G-POWDER"));
        Assert.Equal(Fix64.Zero, siege.Integrity);
        Assert.True(siege.PowderBreach);
        Assert.True((rig.Match.Politics.FixtureDefensesOf(target) with { PowderBreach = siege.PowderBreach }).CanBeAssaulted());

        long[] assigned = siege.Members.Take(4).ToArray();
        long[] expectedDeaths = assigned.Where(id => Sieges.RollCrewAttrition(Seed, target, observation.Season, id, def09Concentric: true)).ToArray();
        Assert.Equal(expectedDeaths.OrderBy(id => id), crewDeaths.OrderBy(id => id));
        Assert.Equal(crewDeaths.Count, crewDeaths.Distinct().Count());
    }

    [Fact]
    public void Cannon_without_powder_does_not_damage_or_breach_a_wall()
    {
        (Rig rig, int home, int target) = Prepared(cannon: true, engine: false, concentric: false, cannonCount: 1);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        rig.Match.Economy.FixtureSetGoodStock(home, "G-POWDER", Fix64.Zero);
        var observations = new System.Collections.Generic.List<SiegeTraceObservation>();
        rig.Match.Politics.SiegeTraceObserverForDiagnostics = observations.Add;

        rig.Season();

        Assert.Equal(Fix64.One, siege.Integrity);
        Assert.False(siege.PowderBreach);
        Assert.Equal(0, observations[0].CannonShots);
        Assert.Equal(Fix64.Zero, observations[0].PowderPaid);
        Assert.Equal(Fix64.Zero, rig.Match.Economy.FixtureGoodStock(home, "G-POWDER"));
        Assert.DoesNotContain(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
    }

    [Fact]
    public void Powder_backed_cannon_breaches_with_paid_shot_and_captures_after_storm()
    {
        (Rig rig, int home, int target) = Prepared(cannon: true, engine: false, concentric: false);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        Assert.Equal(2, siege.Cannons);
        Assert.Equal(Fix64.Zero, rig.Match.Economy.FixtureGoodStock(home, "G-CANNON"));
        Assert.Equal(Fix64.One, siege.Integrity);

        var casualties = new System.Collections.Generic.List<string>();
        rig.Match.Politics.CombatCasualtyObserverForDiagnostics = (figure, kind, dead, season) =>
            casualties.Add($"{kind}:figure={figure.Id}:dead={dead}:season={season}");
        rig.Season();
        Fix64 remainingPowder = rig.Match.Economy.FixtureGoodStock(home, "G-POWDER");
        Assert.Equal(Fix64.FromRatio(1, 2), remainingPowder);
        Assert.Equal(Fix64.Zero, siege.Integrity);
        Assert.True(siege.PowderBreach);
        PoliticsEvent storm = Assert.Single(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.Captured && e.Civ == 0 && e.A == target);
        int attackersDown = (storm.Fight >> 16) & 0xFF, defendersDown = (storm.Fight >> 24) & 0x7F;
        _output.WriteLine($"cannon siege: siege-started=1, cannons-carried=2, assigned-crews=8, crew deaths={casualties.Count(c => c.StartsWith("siegeCrewAttrition:", StringComparison.Ordinal) && c.Contains(":dead=True:", StringComparison.Ordinal))}, cannon-stock-after-deployment=0, powder 1.00→{remainingPowder.ToDoubleForDisplay():F2} L, wall storm won={storm.A == 1}, attackers-down={attackersDown}, defenders-down={defendersDown}, captured=1");
    }

    [Fact]
    public void Siege_trace_observes_exact_powder_fire_and_does_not_change_the_checksum()
    {
        (Rig traced, int tracedHome, int tracedTarget) = Prepared(cannon: true, engine: false, concentric: false);
        (Rig plain, _, _) = Prepared(cannon: true, engine: false, concentric: false);
        var observations = new System.Collections.Generic.List<SiegeTraceObservation>();
        traced.Match.Politics.SiegeTraceObserverForDiagnostics = observations.Add;
        int observedCrewDeaths = 0;
        traced.Match.Politics.CombatCasualtyObserverForDiagnostics = (_, kind, dead, _) =>
        {
            if (kind == "siegeCrewAttrition" && dead) observedCrewDeaths++;
        };

        traced.Season();
        plain.Season();

        Assert.Equal(2, observations.Count); // first after crew attrition, then the same row updated after wall fire
        SiegeTraceObservation observation = observations[0];
        Assert.Equal(observation, observations[1]); // both callback values share the same mutable diagnostic payload
        Assert.Equal(traced.Match.Politics.Season, observation.Season);
        Assert.Equal(0, observation.Attacker);
        Assert.Equal(1, observation.Defender);
        Assert.Equal(tracedTarget, observation.Settlement);
        Assert.Equal(2, observation.Cannons);
        Assert.Equal(0, observation.Engines);
        Assert.Equal(2, observation.CannonCrews);
        Assert.Equal(2, observation.CannonShots);
        Assert.Equal(observation.Cannons * Sieges.CrewPerEngine, observation.AttritionAssignedCrews);
        Assert.False(observation.Def09Active);
        Assert.Equal(Fix64.Half, observation.PowderPaid);
        Assert.Equal(8, observation.AttritionAssignedCrews);
        Assert.Equal(observedCrewDeaths, observation.AttritionDeaths);
        Assert.True(observation.FireAttempted);
        Assert.Equal(Fix64.One, observation.IntegrityBefore);
        Assert.Equal(Fix64.Zero, observation.IntegrityAfter);
        Assert.True(observation.PowderBreach);
        Assert.True(observation.Supplied);
        Assert.Equal(Fix64.FromRatio(1, 2), traced.Match.Economy.FixtureGoodStock(tracedHome, "G-POWDER"));
        Assert.Equal(plain.Match.ComputeChecksum(), traced.Match.ComputeChecksum());
    }

    [Fact]
    public void Fief_snapshot_round_trips_cannon_inventory_and_breach_state()
    {
        (Rig rig, _, int target) = Prepared(cannon: true, engine: false, concentric: false);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        using var snapshot = new MemoryStream();
        using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) rig.Match.WriteSnapshot(writer);
        snapshot.Position = 0;
        (Rig resumed, _, _) = Prepared(cannon: true, engine: false, concentric: false);
        using (var reader = new BinaryReader(snapshot)) resumed.Match.ReadSnapshot(reader);
        Assert.Equal(rig.Match.ComputeChecksum(), resumed.Match.ComputeChecksum());
        Assert.Equal(siege.Cannons, Assert.IsType<Investment>(resumed.Match.Politics.InvestmentAt(target)).Cannons);
        Assert.Equal(siege.PowderBreach, resumed.Match.Politics.InvestmentAt(target)!.PowderBreach);
    }

    [Fact]
    public void Engine_only_wall_breach_does_not_spend_powder_and_garrisoned_DEF09_preserves_the_stronger_storm_defense()
    {
        (Rig rig, int home, int target) = Prepared(cannon: false, engine: true, concentric: true);
        Investment siege = Assert.Single(rig.Match.Politics.Investments);
        int carriedEngines = siege.Engines;
        Assert.InRange(siege.Engines, 1, Sieges.MaxEngines);
        Assert.Equal(0, siege.Cannons);
        Assert.True(rig.Match.Politics.FixtureDefensesOf(target).ConcentricCastle);
        Fix64 powderBefore = rig.Match.Economy.FixtureGoodStock(home, "G-POWDER");
        Fix64 defended = rig.Match.Politics.FixtureDefensesOf(target).Multiplier();
        Fix64 castleOnly = (rig.Match.Politics.FixtureDefensesOf(target) with { ConcentricCastle = false }).Multiplier();
        Assert.Equal(castleOnly * Defenses.ConcentricCastleMultiplier, defended);

        var casualties = new System.Collections.Generic.List<string>();
        var observations = new System.Collections.Generic.List<SiegeTraceObservation>();
        rig.Match.Politics.SiegeTraceObserverForDiagnostics = observations.Add;
        rig.Match.Politics.CombatCasualtyObserverForDiagnostics = (figure, kind, dead, season) =>
            casualties.Add($"{kind}:figure={figure.Id}:dead={dead}:season={season}");
        rig.Season();
        Assert.Equal(powderBefore, rig.Match.Economy.FixtureGoodStock(home, "G-POWDER"));
        Assert.Equal(Fix64.Zero, siege.Integrity);
        Assert.False(siege.PowderBreach);
        Assert.Equal(2, observations.Count);
        SiegeTraceObservation observation = observations[0];
        Assert.Equal(observation, observations[1]);
        int observedCrewDeaths = casualties.Count(c => c.StartsWith("siegeCrewAttrition:", StringComparison.Ordinal) &&
            c.Contains(":dead=True:", StringComparison.Ordinal));
        Assert.True(observation.Def09Active);
        Assert.Equal(carriedEngines * Sieges.CrewPerEngine, observation.AttritionAssignedCrews);
        Assert.Equal(carriedEngines, observation.EngineCrews);
        Assert.Equal(0, observation.CannonCrews);
        Assert.Equal(0, observation.CannonShots);
        Assert.Equal(observedCrewDeaths, observation.AttritionDeaths);
        PoliticsEvent storm = Assert.Single(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.AssaultResolved && e.Civ == 0 && e.B == target);
        _output.WriteLine($"engine siege: siege-started=1, engines-carried={carriedEngines}, assigned-crews={carriedEngines * Sieges.CrewPerEngine}, crew deaths={casualties.Count(c => c.StartsWith("siegeCrewAttrition:", StringComparison.Ordinal) && c.Contains(":dead=True:", StringComparison.Ordinal))}, cannon shots=0, powder unchanged={powderBefore.ToDoubleForDisplay():F2} L, garrisoned DEF-09 multiplier={defended.ToDoubleForDisplay():F3}, attackers-down={(storm.Fight >> 16) & 0xFF}, defenders-down={(storm.Fight >> 24) & 0x7F}, attacker-won={storm.A == 1}, captured={rig.Match.Politics.Events.Any(e => e.Kind == PoliticsEventKind.Captured && e.Civ == 0 && e.A == target)}");
    }

    [Fact]
    public void Relief_terminates_a_Fief_investment_and_returns_unspent_cannons_without_spending_powder()
    {
        (Rig rig, _, int target) = Prepared(cannon: true, engine: false, concentric: false);
        Investment active = Assert.Single(rig.Match.Politics.Investments);
        int home = active.HomeSettlement;
        rig.Match.Economy.FixtureAddPeople(target, 240, 24);
        rig.Match.Economy.FixtureSetGoodStock(target, "G-GRAIN", Fix64.FromInt(1000));
        var relief = new System.Collections.Generic.List<LivingEconomyModel.FighterRecord>();
        rig.Match.Economy.ListFightingAge(target, relief);
        var casualties = new System.Collections.Generic.List<string>();
        rig.Match.Politics.CombatCasualtyObserverForDiagnostics = (figure, kind, dead, season) =>
            casualties.Add($"{kind}:civ={figure.Civilization}:dead={dead}:season={season}");
        Assert.True(rig.Match.Politics.ResolveInvestmentRelief(1, target, relief.ConvertAll(f => f.Id).ToArray()));
        Assert.Null(rig.Match.Politics.InvestmentAt(target));
        Assert.Equal(Fix64.FromInt(2), rig.Match.Economy.FixtureGoodStock(home, "G-CANNON"));
        Assert.Contains(rig.Match.Politics.Events, e => e.Kind == PoliticsEventKind.InvestmentRelieved && e.Civ == 1 && e.A == target);
        Assert.Equal(Fix64.One, rig.Match.Economy.FixtureGoodStock(home, "G-POWDER"));
        _output.WriteLine($"relief siege: prior cannons=2 returned=2, powder unchanged=1.00 L, relief won=True, relief-side casualties={casualties.Count(c => c.StartsWith("siegeRelief:civ=1:", StringComparison.Ordinal))}, besieger casualties={casualties.Count(c => c.StartsWith("siegeRelief:civ=0:", StringComparison.Ordinal))}");
    }

    [Fact]
    public void Terminal_siege_trace_reports_final_civilization_stocks_and_attacked_settlement_defenses()
    {
        (Rig rig, int home, int target) = Prepared(cannon: true, engine: false, concentric: true);
        StringBuilder output = new();
        FiefSiegeTerminalTrace.Append(output, Seed, rig.Match.Politics.Season, rig.Match.Economy,
            new[] { new FiefSiegeTerminalExposure(target, 0, 1, Fix64.FromRatio(1, 2), true,
                "right_censored", "match_horizon_active_investment") });

        Assert.Contains("fief-siege-terminal seed=20261024 season=", output.ToString());
        Assert.Contains("exposure=observed settlements=1", output.ToString());
        Assert.Contains("fief-siege-terminal-inventory seed=20261024 civ=0 cannonStock=0.00 powderStock=1.00", output.ToString());
        Assert.Contains($"settlement={target} attacker=0 defender=1 stoneWalls=true", output.ToString());
        Assert.Contains("castle=true castleGarrisoned=true concentricCastle=true integrity=0.50 powderBreach=true", output.ToString());
        Assert.Contains("outcome=right_censored censorReason=match_horizon_active_investment", output.ToString());
        Assert.Equal(Fix64.One, rig.Match.Economy.FixtureGoodStock(home, "G-POWDER"));
    }

    [Fact]
    public void Terminal_siege_trace_states_no_exposure_without_inventing_inventory_rows()
    {
        var rig = new Rig();
        StringBuilder output = new();
        FiefSiegeTerminalTrace.Append(output, Seed, rig.Match.Politics.Season, rig.Match.Economy,
            Array.Empty<FiefSiegeTerminalExposure>());

        Assert.Equal($"fief-siege-terminal seed={Seed} season={rig.Match.Politics.Season} exposure=none reason=no_siege_investment_observed{Environment.NewLine}",
            output.ToString());
    }

    [Theory]
    [InlineData(ChapterMode.Bronze)]
    [InlineData(ChapterMode.Iron)]
    public void Non_Fief_economies_ignore_cannon_resource_operations(ChapterMode mode)
    {
        ChapterRules rules = mode == ChapterMode.Bronze ? ChapterRules.Bronze : ChapterRules.Iron;
        MatchModel match = MatchModel.Create(Seed, TestPack.Pack, World.Value, Archetypes, default, rules: rules);
        ulong before = match.ComputeChecksum();
        Assert.Equal(0, match.Economy.TakeCannons(0, 3));
        Assert.Equal(0, match.Economy.PayCannonPowder(0, 3));
        Assert.Equal(before, match.ComputeChecksum());
    }
}
