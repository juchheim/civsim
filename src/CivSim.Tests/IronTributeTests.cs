using System;
using System.IO;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Tributary states in the Iron chapter (DIP-06): terms that end a lost war, a surplus and a quarter of Dominion flow that move once, no chains.</summary>
public sealed class IronTributeTests
{
    private const ulong Seed = 20261024;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> Pack = new(() => TestPack.WithNumeric("KEY-4", "DIP-06"));
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(Pack.Value));
    private static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    private static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    private static (MatchModel Match, SimRunner Runner, int Strong, int Weak) Rig(ChapterRules rules, bool dip06 = true, bool strong = true)
    {
        MatchModel match = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: rules);
        var runner = new SimRunner(match);
        void Grant(int civ, params string[] projects)
        {
            foreach (string project in projects)
                runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, Catalog.Value.IndexOf(project), 0, 0));
            runner.Advance(1);
        }
        Grant(0, dip06 ? new[] { "KEY-3", "KEY-4", "DIP-06" } : new[] { "KEY-3", "KEY-4" });
        runner.Advance(150);
        int home = Settlement(match, 0), foreign = Settlement(match, 1);
        if (strong) match.Economy.FixtureAddPeople(home, 240, 24);
        match.Economy.FixtureStock(foreign, "G-GRAIN", Fix64.FromInt(2000));
        match.Politics.SetExternalMind(0, true);
        match.Politics.SetExternalMind(1, true);
        match.Politics.ForceContact(0, 1);
        match.Politics.SetWar(0, 1, true);
        return (match, runner, 0, 1);
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount).First(s => match.Economy.SettlementLite(s).Civilization == civ && !match.Politics.IsFreeFolk(s));

    [Fact]
    public void Terms_need_DIP_06_a_war_a_clear_edge_and_no_chain_and_end_the_war()
    {
        (MatchModel noTech, _, _, _) = Rig(ChapterRules.Iron, dip06: false);
        Assert.False(noTech.Politics.OrderTribute(0, 1));
        (MatchModel even, _, _, _) = Rig(ChapterRules.Iron, strong: false);
        Assert.False(even.Politics.OrderTribute(0, 1), "no clear edge: the field strengths are within 1.5x");
        (MatchModel peace, _, _, _) = Rig(ChapterRules.Iron);
        peace.Politics.SetWar(0, 1, false);
        Assert.False(peace.Politics.OrderTribute(0, 1), "terms end a war; there is none");

        (MatchModel match, _, _, _) = Rig(ChapterRules.Iron);
        Assert.True(match.Politics.FixtureFieldStrength(0) >= match.Politics.FixtureFieldStrength(1) * Fix64.FromRatio(15, 10));
        Assert.True(match.Politics.OrderTribute(0, 1));
        Tribute t = Assert.Single(match.Politics.Tributes);
        Assert.Equal((0, 1), (t.Overlord, t.Subject));
        Assert.Equal(0, match.Politics.OverlordOf(1));
        Assert.True(match.Politics.IsOverlord(0) && match.Politics.IsSubject(1));
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.TributeAgreed && e.Civ == 0);
        // The war is over, and no chain forms: the subject cannot take a subject, the overlord cannot become one.
        Assert.False(match.Politics.OrderTribute(0, 1));
        Assert.False(match.Politics.OrderTribute(1, 2));
        Assert.False(match.Politics.OrderTribute(2, 0));

        (MatchModel bronze, _, _, _) = Rig(ChapterRules.Bronze);
        Assert.False(bronze.Politics.OrderTribute(0, 1));
    }

    [Fact]
    public void The_subject_hands_over_a_tenth_of_its_food_surplus_each_season()
    {
        (MatchModel match, _, _, _) = Rig(ChapterRules.Iron);
        Assert.True(match.Politics.OrderTribute(0, 1));
        int subject = Settlement(match, 1), overlord = Settlement(match, 0);
        Fix64 surplus = match.Politics.Surplus(subject, TradeGood.Food);
        Assert.True(surplus > Fix64.Zero);
        Fix64 subjectBefore = match.Economy.FoodStores(subject), overlordBefore = match.Economy.FoodStores(overlord);
        match.Politics.FixtureAdvanceTribute();
        Fix64 paid = subjectBefore - match.Economy.FoodStores(subject), received = match.Economy.FoodStores(overlord) - overlordBefore;
        Near(surplus / 10, paid);
        Near(paid, received); // the food moves once
    }

    [Fact]
    public void A_quarter_of_the_subjects_yearly_dominion_moves_to_the_overlord_and_the_total_is_conserved()
    {
        (MatchModel tribute, SimRunner tributeRunner, _, _) = Rig(ChapterRules.Iron);
        (MatchModel control, SimRunner controlRunner, _, _) = Rig(ChapterRules.Iron);
        Assert.True(tribute.Politics.OrderTribute(0, 1));
        control.Politics.SetWar(0, 1, false);
        tributeRunner.Advance(150 * 3);
        controlRunner.Advance(150 * 3);
        // Both matches now stand on identical territory; one accrual each, the first with terms.
        tribute.Politics.FixtureAccrueDominion();
        control.Politics.FixtureAccrueDominion();
        Fix64 gain1 = control.Politics.FixtureDominion(1);
        Assert.True(gain1 > Fix64.Zero, "the subject earns Dominion from its land");
        Near(gain1 * Fix64.FromRatio(3, 4), tribute.Politics.FixtureDominion(1));
        Near(control.Politics.FixtureDominion(0) + gain1 / 4, tribute.Politics.FixtureDominion(0));
        Near(control.Politics.FixtureDominion(0) + control.Politics.FixtureDominion(1), tribute.Politics.FixtureDominion(0) + tribute.Politics.FixtureDominion(1));
    }

    [Fact]
    public void The_terms_end_with_a_new_war_and_survive_a_snapshot()
    {
        (MatchModel match, SimRunner runner, _, _) = Rig(ChapterRules.Iron);
        Assert.True(match.Politics.OrderTribute(0, 1));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        stream.Position = 0;
        MatchModel resumed = MatchModel.Create(Seed, Pack.Value, World.Value, Balanced, default, rules: ChapterRules.Iron);
        using (var reader = new BinaryReader(stream)) resumed.ReadSnapshot(reader);
        Assert.Equal(match.ComputeChecksum(), resumed.ComputeChecksum());
        Assert.Single(resumed.Politics.Tributes);

        match.Politics.SetWar(0, 1, true);
        runner.Advance(150);
        Assert.Empty(match.Politics.Tributes);
        Assert.Contains(match.Politics.Events, e => e.Kind == PoliticsEventKind.TributeEnded && e.Civ == 0 && e.B == 0);
    }

    private static void Near(Fix64 expected, Fix64 actual)
        => Assert.True(Fix64.Abs(expected - actual) <= Fix64.FromRaw(43950), $"expected {expected}, got {actual}");
}
