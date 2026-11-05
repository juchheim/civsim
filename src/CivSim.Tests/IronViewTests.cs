using System;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>The Iron chapter in the view layer (docs/MVP/25 §12): the frame block, its fog, the chronicle events and the three new commands.</summary>
public sealed class IronViewTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() =>
        TestPack.WithNumeric("KEY-4", "DIP-06", "DIP-07", "LRN-05", "LRN-06", "SEA-07", "SEA-09", "WAR-07"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner) Rig(ChapterRules rules)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, int civ, params string[] projects)
    {
        foreach (string project in projects)
            runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
        runner.Advance(1);
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    private static MatchFrame Frame(MatchModel match, int viewer)
    {
        var frame = new MatchFrame();
        new FrameBuilder(match, viewer).Build(match, frame);
        return frame;
    }

    [Fact]
    public void The_Iron_looks_and_the_kit_a_figure_holds_reach_the_frame()
    {
        var pack = TestPack.WithNumeric("KEY-4", "AGR-10", "CER-08");
        var catalog = new ResearchCatalog(pack);
        MatchModel match = MatchModel.Create(Seed, pack, World.Value, Balanced, default, rules: ChapterRules.Iron);
        var runner = new SimRunner(match);
        runner.Advance(5);
        ushort iron = (ushort)(CivLook.MouldboardPlough | CivLook.TileRoofs);
        Assert.Equal(0, Frame(match, -1).CivLooks[0] & iron);

        foreach (string project in new[] { "KEY-4", "AGR-10" })
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf(project), 0, 0));
        runner.Advance(2);
        MatchFrame mouldboard = Frame(match, -1);
        Assert.Equal((ushort)CivLook.MouldboardPlough, (ushort)(mouldboard.CivLooks[0] & iron));
        Assert.Equal(0, mouldboard.CivLooks[1] & iron);

        runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf("CER-08"), 0, 0));
        runner.Advance(2);
        Assert.Equal(iron, (ushort)(Frame(match, -1).CivLooks[0] & iron));

        // The kit a figure carries goes out with it: none, then a tempered steel kit (12) on the first adult.
        LivingEconomyModel eco = match.Economy;
        int adult = -1;
        for (int i = 0; i < eco.FigureCount && adult < 0; i++)
            if (eco.Figure(i).Civilization == 0 && eco.Figure(i).Age >= 14) adult = i;
        Assert.True(adult >= 0);
        eco.FixtureSetKit(adult, 12, Fix64.FromInt(20));
        MatchFrame kits = Frame(match, -1);
        int steel = 0;
        for (int i = 0; i < kits.FigureCount; i++) if (kits.FigureKit[i] == 12) steel++;
        Assert.Equal(1, steel);
    }

    [Fact]
    public void The_Iron_prop_looks_follow_each_civilizations_own_projects_and_Bronze_has_none()
    {
        var pack = TestPack.WithNumeric("KEY-4", "WOD-07", "COM-08");
        var catalog = new ResearchCatalog(pack);
        MatchModel match = MatchModel.Create(Seed, pack, World.Value, Balanced, default, rules: ChapterRules.Iron);
        var runner = new SimRunner(match);
        runner.Advance(5);
        Assert.All(Frame(match, -1).Iron.CivLooks, v => Assert.Equal(0, v));
        foreach (string project in new[] { "KEY-4", "WOD-07" })
            runner.Submit(new Command(runner.Tick, 0, CommandKind.CompleteProject, catalog.IndexOf(project), 0, 0));
        runner.Advance(2);
        MatchFrame frame = Frame(match, -1);
        Assert.Equal(IronFrame.LookBarrels, frame.Iron.CivLooks[0] & (IronFrame.LookBarrels | IronFrame.LookRelays));
        Assert.Equal(0, frame.Iron.CivLooks[1]);

        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        bronzeRunner.Advance(5);
        Assert.All(Frame(bronze, -1).Iron.CivLooks, v => Assert.Equal(0, v));
    }

    [Fact]
    public void An_Iron_frame_carries_works_staff_and_Wonders_and_a_Bronze_frame_carries_nothing()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, "KEY-3", "KEY-4", "LRN-05", "LRN-06");
        int home = Settlement(match, 0), foreign = Settlement(match, 1);
        match.Economy.FixtureBuildIronStructure(home, "academy");
        match.Economy.FixtureCompleteIron(home, "great_library");
        match.Economy.FixtureBuildIronStructure(foreign, "gatehouse");
        MatchFrame omniscient = Frame(match, -1);
        Assert.True(omniscient.Iron.Active);
        Assert.Equal(LivingEconomyModel.IronWorkKindCount, omniscient.Iron.WorkKinds);
        int w = omniscient.IndexOfSettlement(home);
        int academy = Enumerable.Range(0, LivingEconomyModel.IronWorkKindCount).First(k => LivingEconomyModel.IronWorkId(k) == "academy");
        int gatehouse = Enumerable.Range(0, LivingEconomyModel.IronWorkKindCount).First(k => LivingEconomyModel.IronWorkId(k) == "gatehouse");
        Assert.Equal(1, omniscient.Iron.Works[w * omniscient.Iron.WorkKinds + academy]);
        Assert.Equal(1, omniscient.Iron.Works[omniscient.IndexOfSettlement(foreign) * omniscient.Iron.WorkKinds + gatehouse]);
        Assert.Equal(home, omniscient.Iron.WonderSettlement[0]);
        Assert.Equal(-1, omniscient.Iron.WonderSettlement[1]);

        // The viewer's own frame: the Wonder is known (own town); the other civilization's works are shown only if its town is seen now.
        MatchFrame own = Frame(match, 0);
        Assert.Equal(home, own.Iron.WonderSettlement[0]);
        int f = own.IndexOfSettlement(foreign);
        if (f >= 0 && (own.SettlementFlags[f] & (ushort)SettlementViewFlags.Remembered) != 0)
            Assert.Equal(0, own.Iron.Works[f * own.Iron.WorkKinds + gatehouse]);
        // A civilization that has not met the Library's holder does not learn where it stands.
        Assert.Equal(-1, Frame(match, 2).Iron.WonderSettlement[0]);

        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        bronzeRunner.Advance(150);
        MatchFrame bronzeFrame = Frame(bronze, -1);
        Assert.False(bronzeFrame.Iron.Active);
        Assert.Equal(0, bronzeFrame.Iron.TributeCount + bronzeFrame.Iron.BlockadeCount + bronzeFrame.Iron.LeagueCount + bronzeFrame.Iron.SiegeCount);
    }

    [Fact]
    public void The_new_commands_reach_the_rules_and_a_Bronze_match_refuses_them()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, "KEY-3", "KEY-4", "DIP-06", "DIP-07");
        runner.Advance(150);
        int home = Settlement(match, 0);
        match.Economy.FixtureAddPeople(home, 240, 24);
        match.Politics.SetExternalMind(0, true);
        match.Politics.SetExternalMind(1, true);
        match.Politics.ForceContact(0, 1);
        match.Politics.SetWar(0, 1, true);
        runner.Submit(PlayerCommands.ImposeTribute(runner.Tick, 0, 1));
        runner.Advance(1);
        Tribute t = Assert.Single(match.Politics.Tributes);
        Assert.Equal((0, 1), (t.Overlord, t.Subject));

        // An alliance first, then the league.
        match.Politics.FixtureAlly(0, 2);
        runner.Submit(PlayerCommands.FormLeague(runner.Tick, 0, 2));
        runner.Advance(1);
        Assert.Equal(new[] { 0, 2 }, Assert.Single(match.Politics.Leagues).Members);

        // A blockade with nothing to blockade is refused, with a reason the player sees.
        runner.Submit(PlayerCommands.Blockade(runner.Tick, 0, Settlement(match, 3)));
        runner.Advance(1);
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.OrderRefused && e.Civ == 0 && e.A == (int)CommandKind.Blockade);

        (MatchModel bronze, SimRunner bronzeRunner) = Rig(ChapterRules.Bronze);
        bronzeRunner.Advance(150);
        bronzeRunner.Submit(PlayerCommands.ImposeTribute(bronzeRunner.Tick, 0, 1));
        bronzeRunner.Submit(PlayerCommands.FormLeague(bronzeRunner.Tick, 0, 2));
        bronzeRunner.Advance(1);
        Assert.Empty(bronze.Politics.Tributes);
        Assert.Empty(bronze.Politics.Leagues);
    }

    [Fact]
    public void Tribute_and_league_events_reach_the_chronicle_for_the_sides_that_took_part_only()
    {
        (MatchModel match, SimRunner runner) = Rig(ChapterRules.Iron);
        Grant(runner, 0, "KEY-3", "KEY-4", "DIP-06");
        runner.Advance(150);
        match.Economy.FixtureAddPeople(Settlement(match, 0), 240, 24);
        match.Politics.SetExternalMind(0, true);
        match.Politics.SetExternalMind(1, true);
        match.Politics.ForceContact(0, 1);
        match.Politics.SetWar(0, 1, true);
        var builder = new FrameBuilder(match, 0);
        var other = new FrameBuilder(match, 2);
        var frame = new MatchFrame();
        builder.Build(match, frame);
        Assert.True(match.Politics.OrderTribute(0, 1));
        runner.Advance(1);
        builder.Build(match, frame);
        Assert.Contains(Enumerable.Range(0, frame.EventCount).Select(i => frame.Events[i]), e => e.Kind == ViewEventKind.TributeAgreed && e.Civ == 0 && e.Other == 1);
        var outsider = new MatchFrame();
        other.Build(match, outsider);
        Assert.DoesNotContain(Enumerable.Range(0, outsider.EventCount).Select(i => outsider.Events[i]), e => e.Kind == ViewEventKind.TributeAgreed);
        Assert.Equal("chronicle.tribute.agreed", ViewText.Key(ChronicleKey.TributeAgreed));
        Assert.Equal("chronicle.blockade.began", ViewText.Key(ChronicleKey.BlockadeBegan));
        Assert.Equal("chronicle.league.joined", ViewText.Key(ChronicleKey.LeagueJoined));
        // The tribute is visible to its parties and omniscient viewers, and not to a third civilization.
        Assert.Equal(1, Frame(match, 0).Iron.TributeCount);
        Assert.Equal(1, Frame(match, 1).Iron.TributeCount);
        Assert.Equal(1, Frame(match, -1).Iron.TributeCount);
        Assert.Equal(0, Frame(match, 2).Iron.TributeCount);
    }
}
