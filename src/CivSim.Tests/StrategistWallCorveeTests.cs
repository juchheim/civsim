using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;
using Xunit;

namespace CivSim.Tests;

public sealed class StrategistWallCorveeTests
{
    private const ulong Seed = 20261023;
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));

    [Fact]
    public void Strategist_adds_legal_wall_corvee_and_values_it_with_believed_defense_urgency()
    {
        var rig = new StrategistRig(Seed, World.Value,
            new[] { CivControl.Ai(Difficulty.Warlord, EconomyArchetype.Balanced), CivControl.Council, CivControl.Council, CivControl.Council });
        rig.Grant(0, "KEY-3", "DEF-04", "DEF-05");
        rig.Seasons(12);
        int settlement = Enumerable.Range(0, rig.Politics.SettlementTotal)
            .First(s => rig.Politics.SettlementCivOf(s) == 0 && !rig.Politics.IsFreeFolk(s));

        object states = typeof(LivingEconomyModel).GetField("_settlements", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Match.Economy)!;
        object state = ((IList)states)[settlement]!;
        Type type = state.GetType();
        int wallKind = LivingEconomyModel.StructureIndex("stone_walls");
        type.GetField("BuildTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(state, Enum.ToObject(type.GetField("BuildTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.FieldType, wallKind));
        type.GetField("BuildProgress", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(state, Fix64.One);
        Assert.True(rig.Politics.Economy.HasCivicWork(settlement, LivingEconomyModel.CorveeTarget.Walls));
        object[] civs = (object[])typeof(ResearchSystem).GetField("_civs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(rig.Match.Research)!;
        Type civType = civs[0].GetType();
        int cap = (int)civType.GetField("MandateCap")!.GetValue(civs[0])!;
        civType.GetField("Mandate")!.SetValue(civs[0], cap);

        var candidates = new System.Collections.Generic.List<DecreeRequest>();
        StrategistDecreePolicy.AddWallCorveeAlternatives(rig.Politics, 0, candidates);

        DecreeRequest request = Assert.Single(candidates);
        Assert.Equal(DecreeKind.Corvee, request.Kind);
        Assert.Equal(settlement, request.Target);
        Assert.Equal(LivingEconomyModel.CorveeTarget.Walls, request.Extra);
        var forecast = new DecreeForecast(DecreeKind.Corvee, true, 0, 1, 2, 1000, 0, 0, Fix64.Zero, 0, 2,
            WallEtaSeasons: Fix64.FromInt(2), ThreatEtaSeasons: 5, ThreatMarginSeasons: Fix64.FromInt(3));
        Assert.Equal(Fix64.Zero, StrategistDecreePolicy.DefenseCorveeBonus(rig.Politics, request, forecast, Fix64.Zero));
        Assert.True(StrategistDecreePolicy.DefenseCorveeBonus(rig.Politics, request, forecast, Fix64.One) > Fix64.Zero);
    }
}
