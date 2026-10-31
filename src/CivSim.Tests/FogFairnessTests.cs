using System.Collections.Generic;
using System.Reflection;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using Xunit.Abstractions;

namespace CivSim.Tests;

/// <summary>
/// Acceptance A8 (docs/MVP/14 §10.4 "Cheating check", 15 rule D10): change what an AI cannot see and nothing it received
/// or did changes; change what it can see and its belief does.
/// </summary>
public class FogFairnessTests
{
    private const int Observer = 0;
    /// <summary>Tiles kept clear of every sight rule, enough for an era's reach and WAY-03's scouts to grow within the step.</summary>
    private const int Margin = 8;

    private readonly ITestOutputHelper _out;
    public FogFairnessTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Unseen_changes_leave_the_observers_belief_view_and_choices_unchanged_and_seen_ones_do_not()
    {
        CivControl[] controls = FogFairnessHarness.DefaultControls();
        MatchModel match = FogFairnessHarness.NewMatch(controls);
        var runner = new SimRunner(match);
        int contactSeason = -1, unseenChecks = 0, seenSettlementChecks = 0, seenWeaponChecks = 0;
        bool beforeContact = false, afterContact = false, midE2 = false;
        for (int season = 1; season <= 124; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks - 1);
            long tick = runner.Tick;
            bool contacted = false;
            for (int o = 1; o < 4; o++) contacted |= match.Politics.Diplomacy.Contacted(Observer, o);
            if (contacted && contactSeason < 0) contactSeason = season;

            string? label = null;
            if (season == 40) { label = "before contact"; beforeContact = true; }
            else if (contactSeason > 0 && season == contactSeason + 3) { label = "after contact"; afterContact = true; }
            else if (season == 120 && match.Research.Era(Observer) >= 2) { label = "mid E2"; midE2 = true; }
            if (label is null) { runner.Advance(1); continue; }

            // Seen: the belief must notice (copies of the match as it stands, before the pair below advances it).
            if (season > 40)
            {
                if (TrySeenSettlement(match, controls, tick)) seenSettlementChecks++;
                if (TrySeenWeapon(match, controls, tick)) seenWeaponChecks++;
            }

            // Unseen: identical minds.
            int changes = 0;
            (MatchModel a, MatchModel b) = FogFairnessHarness.RunPair(match, tick, controls,
                (copy, copyRunner) => changes = FogFairnessHarness.PerturbUnseen(copy, copyRunner, Observer, Margin));
            Assert.True(changes > 0, $"{label}: nothing out of sight to change");
            Assert.NotEqual(a.ComputeChecksum(), b.ComputeChecksum()); // the world did change
            List<string> diffs = FogFairnessHarness.CompareMinds(a, b, Observer, tick);
            Assert.True(diffs.Count == 0, $"{label} (season {season}, {changes} unseen changes): " + string.Join("; ", diffs));
            unseenChecks++;
            _out.WriteLine($"{label}: season {season}, {changes} unseen changes, belief hash {FogFairnessHarness.HashOf(a.Ai!.SeatOf(Observer)!.Belief):X}");

            runner = new SimRunner(match, SimRunner.DefaultChecksumInterval, tick + 1);
        }
        Assert.True(beforeContact && afterContact && midE2, $"checkpoints: {beforeContact} {afterContact} {midE2}, contact season {contactSeason}");
        Assert.Equal(3, unseenChecks);
        Assert.True(seenSettlementChecks + seenWeaponChecks >= 1, "no sanity check could be made");
        _out.WriteLine($"seen-settlement checks {seenSettlementChecks}, seen-weapon checks {seenWeaponChecks}");
    }

    [Fact]
    public void Bronze_unseen_compact_caravan_and_mandate_changes_do_not_change_the_observers_choices()
    {
        CivControl[] controls = FogFairnessHarness.DefaultControls();
        MatchModel match = FogFairnessHarness.NewMatch(controls);
        var runner = new SimRunner(match);

        // Wait for the observer and a rival to enter E3. Stop one tick before the season boundary,
        // just like the existing A8 checkpoints, so both copies make their next decisions from the perturbed state.
        long checkpointTick = -1;
        for (int season = 1; season <= 600; season++)
        {
            runner.Advance(ResearchSystem.SeasonTicks - 1);
            if (match.Research.MandateUnlocked(Observer) && match.Research.MandateUnlocked(1) && match.Research.Mandate(1) > 0)
            {
                checkpointTick = runner.Tick;
                break;
            }
            runner.Advance(1);
        }
        Assert.True(checkpointTick >= 0, "the observer and rival did not reach the Bronze checkpoint within 600 seasons");
        int met05 = match.Research.Catalog.IndexOf("MET-05");
        if (!match.Research.IsDone(Observer, met05))
        {
            runner.Submit(new Command(runner.Tick, Observer, CommandKind.CompleteProject, met05, 0, 0));
            runner.Advance(1);
            checkpointTick = runner.Tick;
        }
        Assert.True(match.Research.IsDone(Observer, met05), "the observer did not complete MET-05 for the Compact candidate check");
        Assert.False(match.Economy.CivHasMetalAccess(Observer, MetalAccessKind.Tin), "observer already had Tin access, so a buy Compact was unavailable");

        // Perturb only hidden route ownership, rival-to-rival Relations, and a hidden rival's actual fighter force,
        // alongside an unseen compact/caravan and Mandate change. Compare the planner output before advancing too,
        // so a candidate filter cannot silently consult those live values.
        MatchModel a = FogFairnessHarness.Clone(match, controls);
        MatchModel b = FogFairnessHarness.Clone(match, controls);
        EnsureCompactMandate(a, Observer);
        EnsureCompactMandate(b, Observer);
        ClearTinBuyCompacts(a, Observer);
        ClearTinBuyCompacts(b, Observer);
        PrepareTinCompactBelief(a, Observer, sellerCiv: 1);
        PrepareTinCompactBelief(b, Observer, sellerCiv: 1);
        PerturbUnseenBronzeObjects(b, observer: Observer, sellerCiv: 1, buyerCiv: 2);
        Assert.Contains(KnownTinCompactCandidates(a, Observer), IsTinCompactForSeller);
        Assert.Contains(KnownTinCompactCandidates(b, Observer), IsTinCompactForSeller);
        DecreeRequest[] beforeCandidates = ProposedDecrees(a, Observer);
        DecreeRequest[] afterCandidates = ProposedDecrees(b, Observer);
        Assert.Equal(beforeCandidates, afterCandidates);
        Assert.Contains(DecreeCandidateDecisions(a, Observer), IsTinCompactCandidateDecision);
        Assert.Contains(DecreeCandidateDecisions(b, Observer), IsTinCompactCandidateDecision);
        Assert.Equal(DecreeDecisions(a, Observer), DecreeDecisions(b, Observer));

        Assert.NotEqual(a.ComputeChecksum(), b.ComputeChecksum());
        RivalBelief before = a.Ai!.SeatOf(Observer)!.Belief.Rivals[1];
        RivalBelief after = b.Ai!.SeatOf(Observer)!.Belief.Rivals[1];
        Assert.Empty(before.CaravansSeen);
        Assert.Empty(after.CaravansSeen);
        // Sanity check the positive side of the boundary: this same kind of caravan evidence changes belief when seen.
        var visibleBelief = new RivalBelief { Civ = 1 };
        var visibleCaravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = 1, Id = 9001, Good = TradeGood.Tin,
            Cargo = Fix64.FromInt(3), CompactId = 9001,
        };
        Assert.False(BeliefBuilder.RecordVisibleCaravan(visibleBelief, visibleCaravan, tile: 7, season: 500, visible: false));
        Assert.Empty(visibleBelief.CaravansSeen);
        Assert.True(BeliefBuilder.RecordVisibleCaravan(visibleBelief, visibleCaravan, tile: 7, season: 500, visible: true));
        BeliefCaravan sighting = Assert.Single(visibleBelief.CaravansSeen);
        Assert.Equal((TradeGood.Tin, 9001), (sighting.Good, sighting.CompactId));
        Assert.True(visibleBelief.TinAccessSeen);
        _out.WriteLine($"Bronze checkpoint tick {checkpointTick}; hidden route ownership, rival Relations, actual force, compact, caravan, and Mandate perturbations left decree candidates and decisions unchanged.");
    }

    private static void PerturbUnseenBronzeObjects(MatchModel match, int observer, int sellerCiv, int buyerCiv)
    {
        TrueWorld world = match.Ai!.World;
        PoliticsSystem politics = match.Politics;
        int unseenTile = -1;
        int routeId = 9001;
        for (int tile = 0; tile < world.Generated.TileCount; tile++)
            if (!politics.KnowsTile(observer, tile)) { unseenTile = tile; break; }
        Assert.True(unseenTile >= 0, "no tile remained outside the observer's Bronze map knowledge");
        Assert.True(match.Research.TrySpendMandate(sellerCiv, 1), "the selected rival must have spare Mandate");

        // Change route ownership only on tiles the observer has never explored, and change relations only
        // between rival civilizations. Neither item is part of the observer's current diplomatic evidence.
        sbyte[] owners = (sbyte[])typeof(Territory).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(politics.Territory)!;
        int routeTilesChanged = 0;
        for (int tile = 0; tile < owners.Length; tile++)
            if (!politics.KnowsTile(observer, tile) && owners[tile] != 3)
            {
                owners[tile] = 3;
                routeTilesChanged++;
            }
        Assert.True(routeTilesChanged > 0, "no unseen route territory changed");
        politics.Diplomacy.Shift(sellerCiv, 3, -200);
        politics.Diplomacy.Shift(buyerCiv, 3, -200);

        int hiddenForceCiv = -1;
        for (int rival = 1; rival < politics.CivCount; rival++)
            if (BeliefBuilder.TilesOutOfFigureSight(world, observer, rival) > 0)
            { hiddenForceCiv = rival; break; }
        Assert.True(hiddenForceCiv >= 0, "no rival force was out of the observer's figure sight");
        int hiddenSettlement = -1;
        for (int s = 0; s < politics.SettlementTotal; s++)
            if (politics.SettlementCivOf(s) == hiddenForceCiv && !politics.IsFreeFolk(s)) { hiddenSettlement = s; break; }
        Assert.True(hiddenSettlement >= 0, "the hidden rival had no settlement to perturb");
        var hiddenFigures = new List<FigureView>();
        match.Economy.ListFigures(hiddenSettlement, hiddenFigures);
        Assert.NotEmpty(hiddenFigures);
        Assert.True(match.Economy.KillFigure(hiddenFigures[0].Id), "could not perturb hidden rival force");

        // This is a test-only world perturbation: add an active compact and caravan on a tile confirmed unknown to the
        // observer. There is no public API for injecting a compact into a copied match, so reflection adds the fixture
        // to the normal simulation-owned expedition list. The caravan has no origin/destination, so it remains at that
        // tile for the one decision step tested; its matching compact id prevents another dispatch. This checks the
        // fog boundary and decision independence, not compact acceptance, route generation, or delivery lifecycle.
        ((List<Compact>)politics.Compacts).Add(new Compact
        {
            Id = routeId, SellerCiv = sellerCiv, BuyerCiv = buyerCiv, Proposer = sellerCiv,
            Good = TradeGood.Tin, SinceSeason = politics.Season, Active = true,
        });
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = sellerCiv, TargetCiv = buyerCiv,
            FromSettlement = -1, ToSettlement = -1, TargetTile = unseenTile, ArriveSeason = politics.Season + 1000,
            Id = routeId, Good = TradeGood.Tin, Cargo = Fix64.FromInt(3), CompactId = routeId,
        };
        FieldInfo expeditionsField = typeof(PoliticsSystem).GetField("_expeditions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((List<Expedition>)expeditionsField.GetValue(politics)!).Add(caravan);
    }

    private static DecreeRequest[] ProposedDecrees(MatchModel match, int civ)
    {
        var candidates = new List<DecreeRequest>();
        match.Politics.DecreePolicyOf(civ)?.Propose(match.Politics, civ, candidates);
        return candidates.ToArray();
    }

    private static void PrepareTinCompactBelief(MatchModel match, int observer, int sellerCiv)
    {
        Belief belief = match.Ai!.SeatOf(observer)!.Belief;
        belief.Season = ((belief.Season + 3) / 4) * 4;
        belief.Rivals[sellerCiv].Contacted = true;
        belief.Rivals[sellerCiv].TinAccessSeen = true;
    }

    private static void ClearTinBuyCompacts(MatchModel match, int civ)
    {
        List<Compact> compacts = (List<Compact>)typeof(PoliticsSystem).GetField("_compacts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(match.Politics)!;
        compacts.RemoveAll(compact => compact.Active && compact.BuyerCiv == civ && compact.Good == TradeGood.Tin);
    }

    private static void EnsureCompactMandate(MatchModel match, int civ)
    {
        int cost = match.Politics.DecreeCost(civ, DecreeKind.Compact);
        if (match.Research.Mandate(civ) >= cost) return;
        // This counterfactual exercises whether the candidate uses only belief-visible evidence. Give the
        // observer enough of its own visible resource to make the candidate feasible in both cloned worlds.
        var research = (CivResearch[])typeof(ResearchSystem).GetField("_civs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(match.Research)!;
        research[civ].Mandate = cost;
    }

    private static DecreeRequest[] KnownTinCompactCandidates(MatchModel match, int civ)
    {
        var candidates = new List<DecreeRequest>();
        StrategistDecreePolicy.AddBeliefKnownTinCompactAlternatives(match.Politics, civ,
            match.Ai!.SeatOf(civ)!.Belief, candidates);
        return candidates.ToArray();
    }

    private static bool IsTinCompactForSeller(DecreeRequest candidate)
        => candidate.Kind == DecreeKind.Compact && candidate.Target == 1 &&
           DecreeArgs.Good(candidate.Extra) == TradeGood.Tin && !DecreeArgs.Sell(candidate.Extra);

    private static bool IsTinCompactCandidateDecision(AiDecision decision)
        => decision.Kind == "DecreeCandidate" && decision.Subject == (int)DecreeKind.Compact && decision.Other == 1;

    private static AiDecision[] DecreeCandidateDecisions(MatchModel match, int civ)
    {
        var entries = new List<AiDecision>();
        foreach (AiDecision decision in match.Ai!.Log.Entries)
            if (decision.Civ == civ && decision.Kind == "DecreeCandidate") entries.Add(decision);
        return entries.ToArray();
    }

    private static string[] DecreeDecisions(MatchModel match, int civ)
    {
        var entries = new List<string>();
        foreach (AiDecision decision in match.Ai!.Log.Entries)
            if (decision.Civ == civ && decision.Kind is "DecreeCandidate" or "Decree") entries.Add(decision.ToString());
        return entries.ToArray();
    }

    /// <summary>Kills half the people of a rival settlement in plain sight in a copy: the head count believed must change.</summary>
    private static bool TrySeenSettlement(MatchModel match, CivControl[] controls, long tick)
    {
        TrueWorld world = match.Ai!.World;
        int target = -1;
        for (int s = 0; s < match.Politics.SettlementTotal && target < 0; s++)
        {
            int owner = match.Politics.SettlementCivOf(s);
            if (owner == Observer || match.Politics.IsFreeFolk(s) || !match.Politics.Diplomacy.Aware(Observer, owner)) continue;
            if (match.Politics.SettlementPopulationOf(s) >= 8 && BeliefBuilder.TilesOutOfRoutineSight(world, Observer, s) <= -2) target = s;
        }
        if (target < 0) return false;
        MatchModel a = FogFairnessHarness.Clone(match, controls), b = FogFairnessHarness.Clone(match, controls);
        var figures = new List<FigureView>();
        b.Economy.ListFigures(target, figures);
        for (int i = 0; i < figures.Count; i += 2) b.Economy.KillFigure(figures[i].Id);
        new SimRunner(a, SimRunner.DefaultChecksumInterval, tick).Advance(1);
        new SimRunner(b, SimRunner.DefaultChecksumInterval, tick).Advance(1);
        BeliefSettlement? x = Find(a.Ai!.SeatOf(Observer)!.Belief, target), y = Find(b.Ai!.SeatOf(Observer)!.Belief, target);
        Assert.NotNull(x);
        Assert.NotNull(y);
        Assert.NotEqual(x!.PopulationEstimate, y!.PopulationEstimate);
        Assert.NotEqual(FogFairnessHarness.HashOf(a.Ai.SeatOf(Observer)!.Belief), FogFairnessHarness.HashOf(b.Ai.SeatOf(Observer)!.Belief));
        return true;
    }

    /// <summary>Hands a rival whose figures are in sight a better weapon in a copy: the weapon believed must change.</summary>
    private static bool TrySeenWeapon(MatchModel match, CivControl[] controls, long tick)
    {
        TrueWorld world = match.Ai!.World;
        ResearchCatalog c = match.Research.Catalog;
        for (int o = 1; o < match.Research.CivCount; o++)
        {
            if (!match.Politics.Diplomacy.Aware(Observer, o) || BeliefBuilder.TilesOutOfFigureSight(world, Observer, o) > -2) continue;
            int bow = c.IndexOf("HNT-06"), archery = c.IndexOf("WAR-03");
            int grant = !match.Research.IsDone(o, bow) ? bow : !match.Research.IsDone(o, archery) ? archery : -1;
            if (grant < 0) continue;
            MatchModel a = FogFairnessHarness.Clone(match, controls), b = FogFairnessHarness.Clone(match, controls);
            var rb = new SimRunner(b, SimRunner.DefaultChecksumInterval, tick);
            rb.Submit(new Command(tick, (byte)o, CommandKind.CompleteProject, grant, 0, 0));
            new SimRunner(a, SimRunner.DefaultChecksumInterval, tick).Advance(1);
            rb.Advance(1);
            RivalBelief x = a.Ai!.SeatOf(Observer)!.Belief.Rivals[o], y = b.Ai!.SeatOf(Observer)!.Belief.Rivals[o];
            Assert.NotEqual(x.WeaponStrength, y.WeaponStrength);
            Assert.True(y.ProjectBelieved[grant], "the weapon seen proves its project");
            return true;
        }
        return false;
    }

    private static BeliefSettlement? Find(Belief belief, int settlement)
    {
        foreach (RivalBelief r in belief.Rivals)
            foreach (BeliefSettlement s in r.Settlements)
                if (s.Settlement == settlement) return s;
        return null;
    }
}
