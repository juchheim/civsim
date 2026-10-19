using System;
using CivSim.Sim.Ai;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The belief state's pure parts (docs/MVP/14 §3.1, §5): sight, noise, confidence, tech closure, posterior, threat.</summary>
public class BeliefRulesTests
{
    private static readonly Lazy<ResearchCatalog> Catalog = new(() => new ResearchCatalog(TestPack.Pack));

    private static TechEstimate Tech()
    {
        ResearchCatalog c = Catalog.Value;
        var ladder = new[]
        {
            new Sim.Politics.WeaponRung(c.IndexOf("HNT-06"), Fix64.FromRatio(18, 10), true),
            new Sim.Politics.WeaponRung(c.IndexOf("HNT-05"), Fix64.FromRatio(17, 10), true),
            new Sim.Politics.WeaponRung(c.IndexOf("HNT-03"), Fix64.FromRatio(16, 10), false),
            new Sim.Politics.WeaponRung(c.IndexOf("HNT-02"), Fix64.FromRatio(13, 10), false),
            new Sim.Politics.WeaponRung(c.IndexOf("WAR-01"), Fix64.One, false),
        };
        return new TechEstimate(c, ladder, c.IndexOf("WAR-03"));
    }

    [Fact]
    public void Sight_ranges_follow_reach_scouts_and_the_awareness_rule()
    {
        Assert.Equal(15, BeliefRules.RoutineSight(12));
        Assert.Equal(12, BeliefRules.ScoutRange(12, false, false));
        Assert.Equal(18, BeliefRules.ScoutRange(12, true, false));
        Assert.Equal(24, BeliefRules.ScoutRange(12, true, true));
        // Two E1 bands (reach 9) see each other's figures at 22 tiles, the distance politics makes them Aware at.
        Assert.Equal(22, BeliefRules.FigureSight(9, 9));
        Assert.Equal(26, BeliefRules.FigureSight(9, 12));
    }

    [Fact]
    public void Head_count_noise_is_deterministic_and_within_a_quarter()
    {
        Fix64 low = Fix64.FromRatio(3, 4), high = Fix64.FromRatio(5, 4);
        bool varies = false;
        Fix64 first = BeliefRules.NoiseFactor(7, 0, 3, 10);
        for (int season = 0; season < 200; season++)
            for (int s = 0; s < 10; s++)
            {
                Fix64 f = BeliefRules.NoiseFactor(7, 0, s, season);
                Assert.Equal(f, BeliefRules.NoiseFactor(7, 0, s, season));
                InRangeFix(f, low, high);
                varies |= f != first;
                int truth = 10 + s * 7;
                int estimate = BeliefRules.Estimate(truth, f);
                Assert.InRange(estimate, truth * 3 / 4 - 1, truth * 5 / 4 + 1);
            }
        Assert.True(varies);
        // A different observer draws a different stream.
        Assert.NotEqual(BeliefRules.NoiseFactor(7, 0, 3, 10).Raw ^ BeliefRules.NoiseFactor(7, 0, 4, 10).Raw,
            BeliefRules.NoiseFactor(7, 1, 3, 10).Raw ^ BeliefRules.NoiseFactor(7, 1, 4, 10).Raw);
        Assert.Equal(0, BeliefRules.Estimate(0, high));
        Assert.Equal(1, BeliefRules.Estimate(1, low));
    }

    [Fact]
    public void Confidence_is_one_when_seen_and_decays_to_a_floor()
    {
        Assert.Equal(Fix64.Zero, BeliefRules.Confidence(-1));
        Assert.Equal(Fix64.One, BeliefRules.Confidence(0));
        Fix64 previous = Fix64.One;
        for (int n = 1; n < 60; n++)
        {
            Fix64 c = BeliefRules.Confidence(n);
            Assert.True(c <= previous);
            Assert.True(c >= BeliefRules.ConfidenceFloor);
            previous = c;
        }
        InRangeFix(BeliefRules.Confidence(4), Fix64.FromRatio(50, 100), Fix64.FromRatio(53, 100));
        Assert.Equal(BeliefRules.ConfidenceFloor, BeliefRules.Confidence(59));
    }

    [Fact]
    public void A_project_proves_its_prerequisites_transitively()
    {
        ResearchCatalog c = Catalog.Value;
        TechEstimate tech = Tech();
        foreach (ProjectDef p in c.Projects)
        {
            bool[] closure = tech.ClosureOf(p.Index);
            Assert.True(closure[p.Index]);
            foreach (int r in p.Requires)
            {
                Assert.True(closure[r], $"{p.Id} needs {c.Projects[r].Id}");
                bool[] inner = tech.ClosureOf(r);
                for (int i = 0; i < inner.Length; i++) if (inner[i]) Assert.True(closure[i]);
            }
            // A one-of prerequisite proves only what all the alternatives share.
            if (p.RequiresAny.Length > 1)
                foreach (int alt in p.RequiresAny)
                {
                    bool everyAlternativeHasIt = true;
                    foreach (int other in p.RequiresAny) everyAlternativeHasIt &= tech.ClosureOf(other)[alt];
                    if (!everyAlternativeHasIt && !Array.Exists(p.Requires, r => tech.ClosureOf(r)[alt]))
                        Assert.False(closure[alt], $"{p.Id} cannot prove its alternative {c.Projects[alt].Id}");
                }
        }
    }

    [Fact]
    public void Sightings_map_to_their_projects_through_the_catalog()
    {
        ResearchCatalog c = Catalog.Value;
        TechEstimate tech = Tech();
        var b = new bool[c.Count];
        tech.AddPalisade(b);
        Assert.True(b[c.IndexOf("DEF-03")]);
        foreach (int r in c.Get("DEF-03").Requires) Assert.True(b[r]);
        b = new bool[c.Count];
        tech.AddWarriors(b);
        Assert.True(b[c.IndexOf("WAR-04")] && b[c.IndexOf("WAR-03")] && b[c.IndexOf("WAR-02")]);
        b = new bool[c.Count];
        tech.AddWatchers(b);
        Assert.True(b[c.IndexOf("DEF-02")]);
        b = new bool[c.Count];
        tech.AddWeapon(Fix64.FromRatio(27, 10), true, b);
        Assert.True(b[c.IndexOf("HNT-06")] && b[c.IndexOf("WAR-03")]);
        b = new bool[c.Count];
        tech.AddWeapon(Fix64.FromRatio(13, 10), false, b);
        Assert.True(b[c.IndexOf("HNT-02")]);
        Assert.False(b[c.IndexOf("HNT-03")]);
        // Structures come from the economy's kinds, matched to the catalog's build effects.
        b = new bool[c.Count];
        for (int k = 0; k < Sim.Economy.LivingEconomyModel.StructureKindCount; k++)
            if (Sim.Economy.LivingEconomyModel.StructureName(k) == "thorn_barrier") tech.AddStructure(k, b);
        Assert.True(b[c.IndexOf("DEF-01")]);
    }

    [Fact]
    public void A_public_era_proves_the_earlier_keystones_closures()
    {
        ResearchCatalog c = Catalog.Value;
        TechEstimate tech = Tech();
        var b = new bool[c.Count];
        tech.AddEra(0, b);
        Assert.DoesNotContain(true, b);
        tech.AddEra(2, b);
        Assert.True(b[c.KeystoneLeaving(0)] && b[c.KeystoneLeaving(1)]);
        Assert.False(b[c.KeystoneLeaving(2)]);
        foreach (int r in c.Projects[c.KeystoneLeaving(1)].Requires) Assert.True(b[r]);
    }

    [Fact]
    public void The_posterior_reads_scripted_behaviour_and_keeps_a_floor()
    {
        var post = new Fix64[ArchetypeModel.Count];
        ArchetypeModel.Reset(post);
        AssertSumsToOne(post);
        for (int i = 0; i < 4; i++) ArchetypeModel.Update(post, BeliefEvidence.RaidOnUs);
        ArchetypeModel.Update(post, BeliefEvidence.WarriorsSeen);
        Assert.Equal(ArchetypeModel.Warlord, ArchetypeModel.Mode(post));
        AssertSumsToOne(post);

        ArchetypeModel.Reset(post);
        for (int i = 0; i < 4; i++) ArchetypeModel.Update(post, BeliefEvidence.NewSettlement);
        Assert.Equal(ArchetypeModel.Grower, ArchetypeModel.Mode(post));
        ArchetypeModel.Reset(post);
        for (int i = 0; i < 4; i++) ArchetypeModel.Update(post, BeliefEvidence.ClaimsAdvance);
        ArchetypeModel.Update(post, BeliefEvidence.GiftFromThem);
        Assert.Equal(ArchetypeModel.Wanderer, ArchetypeModel.Mode(post));
        ArchetypeModel.Reset(post);
        for (int i = 0; i < 4; i++) ArchetypeModel.Update(post, BeliefEvidence.NewCraftStructure);
        Assert.Equal(ArchetypeModel.Builder, ArchetypeModel.Mode(post));
        ArchetypeModel.Reset(post);
        for (int i = 0; i < 6; i++) ArchetypeModel.Update(post, BeliefEvidence.FirstAnnounced);
        ArchetypeModel.Update(post, BeliefEvidence.EraAhead);
        Assert.Equal(ArchetypeModel.Sage, ArchetypeModel.Mode(post));

        // A hundred raids never rule out a peaceful reading, and a change of behaviour is read within a few seasons.
        ArchetypeModel.Reset(post);
        for (int i = 0; i < 100; i++) ArchetypeModel.Update(post, BeliefEvidence.RaidOnUs);
        foreach (Fix64 p in post) Assert.True(p >= Fix64.FromRatio(18, 1000));
        for (int i = 0; i < 12; i++) ArchetypeModel.Update(post, BeliefEvidence.NewSettlement);
        Assert.Equal(ArchetypeModel.Grower, ArchetypeModel.Mode(post));
        AssertSumsToOne(post);
    }

    private static void AssertSumsToOne(Fix64[] post)
    {
        Fix64 sum = Fix64.Zero;
        foreach (Fix64 p in post) sum += p;
        Assert.Equal(Fix64.One, sum);
    }

    private static readonly Fix64[] Uniform = { Fix64.FromRatio(1, 5), Fix64.FromRatio(1, 5), Fix64.FromRatio(1, 5), Fix64.FromRatio(1, 5), Fix64.FromRatio(1, 5) };

    [Fact]
    public void Threat_rises_with_hostility_warriors_paint_hunger_history_and_nearness()
    {
        var baseInputs = new ThreatInputs(0, false, false, false, 2, 0, false, false, false, 20, 12);
        Fix64 p0 = ThreatModel.Probability(baseInputs, Uniform);
        Assert.True(ThreatModel.Probability(baseInputs with { Relations = -50 }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { Relations = 50 }, Uniform) < p0);
        Assert.True(ThreatModel.Probability(baseInputs with { AtWar = true }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { Allied = true }, Uniform) < p0);
        Assert.True(ThreatModel.Probability(baseInputs with { WarriorsSeen = 5 }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { WarriorsSeen = 10 }, Uniform) > ThreatModel.Probability(baseInputs with { WarriorsSeen = 5 }, Uniform));
        Assert.True(ThreatModel.Probability(baseInputs with { RaidPostureSeen = true }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { DistressSeen = true }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { RaidedUsRecently = true }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { DistanceTiles = 10 }, Uniform) > p0);
        Assert.True(ThreatModel.Probability(baseInputs with { DistanceTiles = 30 }, Uniform) < p0);
        Assert.Equal(Fix64.Zero, ThreatModel.Probability(baseInputs with { DistanceTiles = 40 }, Uniform));
        Assert.True(ThreatModel.Probability(baseInputs with { RaidCapability = 0 }, Uniform) < p0);
        var warlord = new[] { Fix64.FromRatio(5, 100), Fix64.FromRatio(5, 100), Fix64.FromRatio(5, 100), Fix64.FromRatio(80, 100), Fix64.FromRatio(5, 100) };
        Assert.True(ThreatModel.Probability(baseInputs, warlord) > p0);
        var all = new ThreatInputs(-100, true, false, false, 2, 40, true, true, true, 1, 12);
        Assert.Equal(ThreatModel.MaxProbability, ThreatModel.Probability(all, warlord));

        Assert.True(ThreatModel.Loss(Fix64.FromInt(10), Fix64.FromInt(5)) > ThreatModel.Loss(Fix64.FromInt(5), Fix64.FromInt(5)));
        Assert.True(ThreatModel.Loss(Fix64.FromInt(5), Fix64.FromInt(10)) < ThreatModel.Loss(Fix64.FromInt(5), Fix64.FromInt(5)));
        Assert.Equal(Fix64.Half, ThreatModel.Loss(Fix64.FromInt(5), Fix64.FromInt(5)));
        Assert.True(ThreatModel.PartyStrength(40, 4, Fix64.FromRatio(16, 10)) > ThreatModel.PartyStrength(40, 0, Fix64.FromRatio(16, 10)));
        Assert.True(ThreatModel.PartyStrength(40, 0, Fix64.FromRatio(16, 10)) > ThreatModel.PartyStrength(20, 0, Fix64.FromRatio(16, 10)));
    }

    [Fact]
    public void Opportunity_rises_with_weakness_famine_absence_and_a_closing_gap()
    {
        var o = new OpportunityInputs(true, false, false, false, Fix64.FromInt(8), Fix64.FromInt(8), false, false, false, Fix64.One);
        Fix64 v = ThreatModel.Opportunity(o);
        Assert.Equal(Fix64.Half, v);
        Assert.True(ThreatModel.Opportunity(o with { TargetDefense = Fix64.FromInt(4) }) > v);
        Assert.True(ThreatModel.Opportunity(o with { TargetDefense = Fix64.FromInt(16) }) < v);
        Assert.True(ThreatModel.Opportunity(o with { OwnParty = Fix64.FromInt(16) }) > v);
        Assert.True(ThreatModel.Opportunity(o with { DistressSeen = true }) > v);
        Assert.True(ThreatModel.Opportunity(o with { WarriorsAwaySeen = true }) > v);
        Assert.True(ThreatModel.Opportunity(o with { GapClosing = true }) > v);
        Assert.True(ThreatModel.Opportunity(o with { InTruce = true }) < v);
        Assert.True(ThreatModel.Opportunity(o with { Confidence = Fix64.FromRatio(2, 10) }) < v);
        Assert.Equal(Fix64.Zero, ThreatModel.Opportunity(o with { Contacted = false }));
        Assert.Equal(Fix64.Zero, ThreatModel.Opportunity(o with { Allied = true }));
    }

    private static void InRangeFix(Sim.Numerics.Fix64 value, Sim.Numerics.Fix64 low, Sim.Numerics.Fix64 high)
        => Assert.True(value >= low && value <= high, $"{value} not in [{low}, {high}]");
}
