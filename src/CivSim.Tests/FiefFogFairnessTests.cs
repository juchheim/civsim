using System;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class FiefFogFairnessTests
{
    private const int Observer = 0;
    private const int Margin = 8;
    private const ulong Seed = 20261054;

    [Fact]
    public void Unseen_Fief_deposits_powder_fleets_and_horses_do_not_change_observer_decisions()
    {
        CivControl[] controls = FogFairnessHarness.DefaultControls();
        MatchModel match = FogFairnessHarness.NewMatch(controls, Seed, fief: true);
        var runner = new SimRunner(match);
        runner.Advance(ResearchSystem.SeasonTicks - 1);
        long tick = runner.Tick;

        (MatchModel baseline, MatchModel perturbed) = FogFairnessHarness.RunPair(match, tick, controls,
            (copy, _) => Assert.True(FogFairnessHarness.PerturbUnseenFief(copy, Observer, Margin, randomSeed: 8091) > 0),
            fief: true, independentWorld: true, seed: Seed);

        Assert.NotEqual(baseline.ComputeChecksum(), perturbed.ComputeChecksum());
        Assert.Empty(FogFairnessHarness.CompareMinds(baseline, perturbed, Observer, tick));
    }

    [Fact]
    public void Unseen_Fief_horse_perturbation_stays_beyond_sight_and_does_not_reveal_a_source()
    {
        MatchModel match = FogFairnessHarness.NewMatch(FogFairnessHarness.DefaultControls(), Seed, fief: true);
        AiSeat seat = Assert.IsType<AiSeat>(match.Ai!.SeatOf(Observer));
        MapKnowledge map = seat.Belief.Map;

        Assert.Equal(HorseSourceKnowledge.Unknown,
            BeliefBuilder.ObserveHorseSourceKnowledge(map, match.Economy, HorseSourceKnowledge.Unknown));

        Assert.True(FogFairnessHarness.PerturbUnseenFief(match, Observer, Margin, randomSeed: 8091) > 0);

        int sightLimit = BeliefRules.RoutineSight(match.Politics.ReachOf(Observer)) + Margin;
        int horseHerds = 0;
        for (int i = 0; i < match.Economy.WildHerdCount; i++)
        {
            WildHerdView herd = match.Economy.WildHerd(i);
            if (herd.Species != HerdSpecies.Horse) continue;
            horseHerds++;
            Assert.False(map.IsExplored(herd.CurrentTile));

            int nearestSettlement = int.MaxValue;
            for (int s = 0; s < match.Politics.SettlementTotal; s++)
                if (match.Politics.SettlementCivOf(s) == Observer && !match.Politics.IsFreeFolk(s))
                    nearestSettlement = Math.Min(nearestSettlement, HexGrid.Distance(
                        match.Politics.SettlementTileOf(s), herd.CurrentTile, match.Ai.World.Generated.Width));
            Assert.True(nearestSettlement > sightLimit);
        }

        Assert.True(horseHerds > 0);
        Assert.Equal(HorseSourceKnowledge.Unknown,
            BeliefBuilder.ObserveHorseSourceKnowledge(map, match.Economy, HorseSourceKnowledge.Unknown));
    }

    [Fact]
    public void Visible_Fief_deposit_powder_fleet_and_horse_inputs_are_observable()
    {
        CivControl[] controls = { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council };
        MatchModel match = FogFairnessHarness.NewMatch(controls, Seed, fief: true);
        PoliticsSystem politics = match.Politics;
        LivingEconomyModel economy = match.Economy;
        AiSeat seat = Assert.IsType<AiSeat>(match.Ai!.SeatOf(Observer));
        int home = -1;
        for (int s = 0; s < politics.SettlementTotal; s++)
            if (politics.SettlementCivOf(s) == Observer && !politics.IsFreeFolk(s)) { home = s; break; }
        Assert.True(home >= 0, "observer needs a home settlement");
        int homeTile = politics.SettlementTileOf(home);

        seat.Belief.Map.MarkExplored(homeTile);
        FogFairnessHarness.FixtureSetVisibleDeposit(match, Observer, homeTile, DepositKind.Saltpeter);
        Assert.True(seat.Belief.Map.TryTile(homeTile, out WorldTile known));
        Assert.Equal(DepositKind.Saltpeter, known.Deposit);

        economy.FixtureStock(home, "G-CANNON", Fix64.FromInt(2));
        economy.FixtureStock(home, "G-POWDER", Fix64.FromInt(7));
        economy.FixtureBuildIronStructure(home, "harbor");
        economy.FixtureBuildIronStructure(home, "galley");
        economy.FixtureSetSoldiersSupplied(home, 8);
        OwnViewBuilder.Build(seat.Own, Observer, 0, economy, match.Research, politics);
        OwnSettlement own = Assert.Single(seat.Own.Settlements);
        Assert.Equal(Fix64.FromInt(2), own.CannonStock);
        Assert.Equal(Fix64.FromInt(7), own.PowderStock);
        Assert.Contains(home, seat.Own.CrewedGalleyHomes);

        int liveHorses = 0;
        for (int i = 0; i < economy.WildHerdCount; i++)
        {
            WildHerdView herd = economy.WildHerd(i);
            if (herd.Species != HerdSpecies.Horse) continue;
            if (herd.AnimalCount > Fix64.Zero) liveHorses++;
            economy.FixtureSetWildHerd(i, herd.CurrentTile, Fix64.Zero);
        }
        Assert.True(liveHorses > 0, "fixture world needs a live horse herd");
        int horseHerd = -1;
        for (int i = 0; i < economy.WildHerdCount; i++)
            if (economy.WildHerd(i).Species == HerdSpecies.Horse) { horseHerd = i; break; }
        Assert.True(horseHerd >= 0);
        economy.FixtureSetWildHerd(horseHerd, homeTile, Fix64.FromInt(5));
        Assert.Equal(HorseSourceKnowledge.Seen,
            BeliefBuilder.ObserveHorseSourceKnowledge(seat.Belief.Map, economy, HorseSourceKnowledge.Unknown));
    }
}
