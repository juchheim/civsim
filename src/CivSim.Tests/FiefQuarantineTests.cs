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

/// <summary>Bounded WP-E5-6 quarantine tests for inter-settlement spread and imported-outbreak gates.</summary>
public sealed class FiefQuarantineTests
{
    private const ulong FirstSeed = 20261140;
    private static readonly Lazy<CivSim.Sim.Content.ContentPack> MatchPack =
        new(() => TestPack.WithNumeric("KEY-5", "MED-08"));
    private static readonly Lazy<GeneratedWorld> MatchWorld =
        new(() => WorldGenerator.Generate(FirstSeed));

    private static (MatchModel Match, SimRunner Runner) StartMatch()
    {
        EconomyArchetype[] balanced =
            { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };
        MatchModel match = MatchModel.Create(FirstSeed, MatchPack.Value, MatchWorld.Value, balanced, default,
            rules: ChapterRules.Fief);
        return (match, new SimRunner(match));
    }

    private static void Grant(SimRunner runner, MatchModel match, int civ, string project)
    {
        int index = match.Research.Catalog.IndexOf(project);
        Assert.True(index >= 0, $"{project} must exist in the test pack");
        runner.Submit(new Command(runner.Tick, (byte)civ, CommandKind.CompleteProject, index, 0, 0));
        runner.Advance(2);
    }

    private static int Settlement(MatchModel match, int civ)
        => Enumerable.Range(0, match.Economy.SettlementCount)
            .First(s => match.Economy.SettlementLite(s).Civilization == civ);

    private static void DeliverPlagueCaravan(MatchModel match, Compact compact, int seller, int buyer, int eventId)
    {
        match.Politics.FixtureArriveCaravan(new Expedition
        {
            Kind = ExpeditionKind.Caravan,
            Civ = 0,
            TargetCiv = 1,
            FromSettlement = seller,
            ToSettlement = buyer,
            Good = TradeGood.Salt,
            Cargo = Fix64.One,
            Value = Fix64.One,
            Id = eventId,
            CompactId = compact.Id,
        });
    }

    [Fact]
    public void Quarantine_gates_imports_and_reduces_imported_outbreak_rolls_only_in_Fief()
    {
        BronzeFixture quarantined = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, FirstSeed, 100,
            "KEY-5", "MED-08");
        int accepted = 0;
        int importGatePasses = 0, outbreakRollPasses = 0;
        int firstImportOnly = -1, firstBothPass = -1;
        for (int i = 0; i < 256; i++)
        {
            (bool importGate, bool outbreakRoll) = quarantined.Economy.FixtureFiefImportedOutbreakRolls(0, i);
            if (importGate) importGatePasses++;
            if (outbreakRoll) outbreakRollPasses++;
            bool imported = quarantined.Economy.ImportOutbreak(0, OutbreakClass.Plague, importEvent: i);
            accepted += imported ? 1 : 0;
            if (importGate && !outbreakRoll)
            {
                Assert.False(imported, "the separate 0.75 outbreak roll rejects this keyed arrival");
                if (firstImportOnly < 0) firstImportOnly = i;
            }
            if (importGate && outbreakRoll)
            {
                Assert.True(imported, "both keyed quarantine gates allow this arrival");
                if (firstBothPass < 0) firstBothPass = i;
            }
            if (!importGate) Assert.False(imported, "the 1-in-2 quarantine gate rejects this arrival");
        }

        Assert.InRange(importGatePasses, 96, 160); // Deterministic event keys exercise the 1-in-2 gate.
        Assert.InRange(outbreakRollPasses, 180, 220); // And the independent 3-in-4 outbreak modifier.
        Assert.InRange(accepted, 72, 120); // Combined route acceptance is approximately 3/8; both gates are checked above.
        Assert.True(firstImportOnly >= 0 && firstBothPass >= 0);
        Assert.Equal(Fix64.FromRatio(25, 1000), quarantined.Economy.FixtureInterSettlementOutbreakChance(0));

        BronzeFixture fiefWithoutQuarantine = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, FirstSeed, 100, "KEY-5");
        Assert.Equal(Fix64.FromRatio(5, 100), fiefWithoutQuarantine.Economy.FixtureInterSettlementOutbreakChance(0));
        Assert.True(fiefWithoutQuarantine.Economy.ImportOutbreak(0, OutbreakClass.Plague, importEvent: 77));

        BronzeFixture iron = BronzeFixture.CreateIron(TestPack.Pack, FirstSeed, 100, "KEY-5", "MED-08");
        Assert.Equal(Fix64.FromRatio(5, 100), iron.Economy.FixtureInterSettlementOutbreakChance(0));
        Assert.True(iron.Economy.ImportOutbreak(0, OutbreakClass.Plague, importEvent: 77));
    }

    [Fact]
    public void Accepted_quarantine_import_continues_exactly_after_snapshot_restore()
    {
        const ulong seed = FirstSeed;
        BronzeFixture live = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 100, "KEY-5", "MED-08");
        live.RunSeasons(1);
        bool imported = false;
        for (int importEvent = 0; importEvent < 32; importEvent++)
        {
            imported = live.Economy.ImportOutbreak(0, OutbreakClass.Plague, importEvent);
            if (imported) break;
        }

        Assert.True(imported, "The deterministic seed range should include an accepted quarantine import.");
        byte[] snapshot = Snapshot(live.Economy);

        BronzeFixture resumed = BronzeFixture.CreateFiefWithCamp(TestPack.Pack, seed, 100, "KEY-5", "MED-08");
        resumed.RunSeasons(1);
        Restore(resumed.Economy, snapshot);

        live.RunSeasons(1);
        resumed.RunSeasons(1);
        Assert.Equal((OutbreakClass.Plague, 1), live.Economy.OutbreakAt(0));
        Assert.Equal(Snapshot(live.Economy), Snapshot(resumed.Economy));
        Assert.Equal(live.Economy.ComputeChecksum(), resumed.Economy.ComputeChecksum());
    }

    [Fact]
    public void Plague_caravan_arrivals_record_quarantine_blocks_and_unquarantined_imports()
    {
        (MatchModel quarantined, SimRunner quarantinedRunner) = StartMatch();
        Grant(quarantinedRunner, quarantined, 0, "KEY-3");
        Grant(quarantinedRunner, quarantined, 0, "KEY-5");
        Grant(quarantinedRunner, quarantined, 1, "KEY-3");
        Grant(quarantinedRunner, quarantined, 1, "KEY-5");
        Grant(quarantinedRunner, quarantined, 1, "MED-08");
        int quarantinedSeller = Settlement(quarantined, 0);
        int quarantinedBuyer = Settlement(quarantined, 1);
        quarantined.Economy.GiveGoods(quarantinedBuyer, Fix64.FromInt(1000));
        quarantined.Economy.FixtureSetOutbreak(quarantinedSeller, OutbreakClass.Plague);
        quarantined.Economy.EnableFiefHealthTraceForDiagnostics();
        Compact quarantinedCompact = quarantined.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);

        // Every arrival crosses the actual ArriveCaravan route path. The event-keyed gate range must include
        // both rejected and accepted arrivals, and trace counts must reconcile to the results of those gates.
        for (int eventId = 0; eventId < 256; eventId++)
            DeliverPlagueCaravan(quarantined, quarantinedCompact, quarantinedSeller, quarantinedBuyer, eventId);

        FiefExpectedHazardSnapshot quarantineTrace =
            quarantined.Economy.CaptureFiefExpectedHazardsForDiagnostics(1);
        Assert.True(quarantineTrace.Quarantine);
        Assert.Equal(256, quarantineTrace.ImportedAttempts);
        Assert.InRange(quarantineTrace.ImportedBlocked, 1, 255);
        Assert.InRange(quarantineTrace.ImportedSuccesses, 1, 255);
        Assert.Equal(quarantineTrace.ImportedAttempts,
            quarantineTrace.ImportedBlocked + quarantineTrace.ImportedSuccesses);

        (MatchModel open, SimRunner openRunner) = StartMatch();
        Grant(openRunner, open, 0, "KEY-3");
        Grant(openRunner, open, 0, "KEY-5");
        Grant(openRunner, open, 1, "KEY-3");
        Grant(openRunner, open, 1, "KEY-5");
        int openSeller = Settlement(open, 0);
        int openBuyer = Settlement(open, 1);
        open.Economy.GiveGoods(openBuyer, Fix64.FromInt(1000));
        open.Economy.FixtureSetOutbreak(openSeller, OutbreakClass.Plague);
        open.Economy.EnableFiefHealthTraceForDiagnostics();
        Compact openCompact = open.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);

        for (int eventId = 0; eventId < 8; eventId++)
            DeliverPlagueCaravan(open, openCompact, openSeller, openBuyer, eventId);

        FiefExpectedHazardSnapshot openTrace = open.Economy.CaptureFiefExpectedHazardsForDiagnostics(1);
        Assert.False(openTrace.Quarantine);
        Assert.Equal(8, openTrace.ImportedAttempts);
        Assert.Equal(0, openTrace.ImportedBlocked);
        Assert.Equal(8, openTrace.ImportedSuccesses);
    }

    [Fact]
    public void Fief_caravan_carries_departure_plague_after_seller_outbreak_ends_during_travel()
    {
        (MatchModel match, SimRunner runner) = StartMatch();
        Grant(runner, match, 0, "KEY-3");
        Grant(runner, match, 0, "KEY-5");
        Grant(runner, match, 1, "KEY-3");
        Grant(runner, match, 1, "KEY-5");
        Grant(runner, match, 1, "MED-08");
        int seller = Settlement(match, 0), buyer = Settlement(match, 1);
        match.Economy.GiveGoods(buyer, Fix64.FromInt(1000));
        match.Economy.PutGood(seller, "G-SALT", Fix64.FromInt(1000));
        match.Economy.FixtureSetOutbreak(seller, OutbreakClass.Plague);
        match.Economy.EnableFiefHealthTraceForDiagnostics();
        Compact compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);

        match.Politics.FixtureSendCaravan(compact);
        Expedition caravan = Assert.Single(match.Politics.Caravans());
        Assert.True(caravan.ArriveSeason > 0);
        Assert.True(caravan.CarriesPlague);

        // Model the seller's two-season outbreak ending before this shipment reaches its destination.
        match.Economy.FixtureSetOutbreak(seller, OutbreakClass.None);
        match.Politics.FixtureArriveCaravan(caravan);

        FiefExpectedHazardSnapshot trace = match.Economy.CaptureFiefExpectedHazardsForDiagnostics(1);
        Assert.True(trace.Quarantine);
        Assert.Equal(1, trace.ImportedAttempts);
        Assert.Equal(trace.ImportedAttempts, trace.ImportedBlocked + trace.ImportedSuccesses);
    }

    [Fact]
    public void Carried_plague_payload_is_ignored_by_Bronze_and_Iron_hashes_and_snapshots()
    {
        foreach (ChapterRules rules in new[] { ChapterRules.Bronze, ChapterRules.Iron })
        {
            MatchModel match = MatchModel.Create(FirstSeed, TestPack.Pack, MatchWorld.Value,
                new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced },
                default, rules: rules);
            Compact compact = match.Politics.FixtureAddCompact(0, 1, TradeGood.Salt);
            var caravan = new Expedition
            {
                Kind = ExpeditionKind.Caravan, Id = 700, CompactId = compact.Id,
                FromSettlement = Settlement(match, 0), ToSettlement = Settlement(match, 1),
                Good = TradeGood.Salt, Cargo = Fix64.One,
            };
            match.Politics.FixtureAddCaravan(caravan);
            ulong before = match.ComputeChecksum();
            caravan.CarriesPlague = true;
            Assert.Equal(before, match.ComputeChecksum());

            using var snapshot = new MemoryStream();
            using (var writer = new BinaryWriter(snapshot, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
            snapshot.Position = 0;
            MatchModel restored = MatchModel.Create(FirstSeed, TestPack.Pack, MatchWorld.Value,
                new[] { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced },
                default, rules: rules);
            using (var reader = new BinaryReader(snapshot)) restored.ReadSnapshot(reader);
            Expedition restoredCaravan = Assert.Single(restored.Politics.Caravans());
            Assert.False(restoredCaravan.CarriesPlague);
            Assert.Equal(match.ComputeChecksum(), restored.ComputeChecksum());
        }
    }

    private static byte[] Snapshot(LivingEconomyModel economy)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) economy.WriteSnapshot(writer);
        return stream.ToArray();
    }

    private static void Restore(LivingEconomyModel economy, byte[] snapshot)
    {
        using var stream = new MemoryStream(snapshot);
        using var reader = new BinaryReader(stream);
        economy.ReadSnapshot(reader);
        Assert.Equal(stream.Length, stream.Position);
    }
}
