using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.View;
using CivSim.Sim.World;

namespace CivSim.Tests;

/// <summary>Shared rigs for the view layer tests (docs/MVP/18): the politics golden's match, with frames built along the way.</summary>
internal static class ViewKit
{
    public const ulong Seed = 20261019;
    public static readonly Lazy<GeneratedWorld> World = new(() => WorldGenerator.Generate(Seed));
    public static readonly EconomyArchetype[] Balanced =
        { EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced, EconomyArchetype.Balanced };

    public static MatchModel NewMatch() => MatchModel.Create(Seed, TestPack.Pack, World.Value, Balanced);

    /// <summary>A 30-minute match watched by civilization 0 every 10 ticks, checked frame by frame as it went. Read-only once built.</summary>
    public static readonly Lazy<WatchedMatch> Watched = new(() => new WatchedMatch(30 * 600));

    public static byte[] Snapshot(MatchModel match)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true)) match.WriteSnapshot(writer);
        return stream.ToArray();
    }

    public static MatchModel Restore(byte[] snapshot)
    {
        MatchModel match = NewMatch();
        using var reader = new BinaryReader(new MemoryStream(snapshot));
        match.ReadSnapshot(reader);
        return match;
    }

    /// <summary>A fingerprint of everything a frame shows (all blocks up to their counts, events included).</summary>
    public static ulong Hash(MatchFrame f, bool includeTotals = true)
    {
        var h = new StateHasher();
        h.Add(f.Sequence); h.Add(f.Tick); h.Add(f.Season); h.Add(f.SeasonOfYear); h.Add(f.Year); h.Add(f.TickInSeason);
        h.Add(f.LastWinterHarsh); h.Add(f.ViewerCiv); h.Add(f.Width); h.Add(f.Height);
        if (includeTotals) h.Add(f.TotalFigures);
        for (int t = 0; t < f.Visibility.Length; t++) { h.Add(f.Visibility[t]); h.Add(f.Owner[t]); h.Add(f.Claim[t]); h.Add(f.Claimant[t]); h.Add(f.TileFlags[t]); h.Add(f.KnownDeposits[t]); }
        h.Add(f.FigureCount);
        for (int i = 0; i < f.FigureCount; i++)
        {
            h.Add(f.FigureId[i]); h.Add(f.FigureCiv[i]); h.Add(f.FigureSettlement[i]); h.Add(f.FigureTile[i]); h.Add(f.FigureX[i]); h.Add(f.FigureY[i]);
            h.Add(f.FigureActivity[i]); h.Add(f.FigureAge[i]); h.Add(f.FigureHealth[i]); h.Add(f.FigureFlags[i]); h.Add(f.FigureToolRung[i]); h.Add(f.FigureCarry[i]);
        }
        h.Add(f.HerdCount);
        for (int i = 0; i < f.HerdCount; i++)
        {
            h.Add(f.HerdId[i]); h.Add(f.HerdSpecies[i]); h.Add(f.HerdTile[i]); h.Add(f.HerdX[i]); h.Add(f.HerdY[i]); h.Add(f.HerdAnimals[i]);
            h.Add(f.HerdFear[i]); h.Add(f.HerdAway[i]);
        }
        h.Add(f.PredatorCount);
        for (int i = 0; i < f.PredatorCount; i++) { h.Add(f.PredatorTile[i]); h.Add(f.PredatorSpecies[i]); h.Add(f.PredatorSize[i]); }
        h.Add(f.SettlementCount);
        int kinds = MatchFrame.StructureKinds;
        for (int i = 0; i < f.SettlementCount; i++)
        {
            h.Add(f.SettlementIndex[i]); h.Add(f.SettlementCiv[i]); h.Add(f.SettlementTile[i]); h.Add(f.SettlementPopulation[i]);
            h.Add(f.SettlementFood[i]); h.Add(f.SettlementWinterForecast[i]); h.Add(f.SettlementCohesion[i]); h.Add(f.SettlementFlags[i]);
            h.Add(f.SettlementBuildKind[i]); h.Add(f.SettlementBuildPermille[i]); h.Add(f.SettlementSmallStock[i]); h.Add(f.SettlementCattle[i]);
            h.Add(f.SettlementPigs[i]); h.Add(f.SettlementSeenSeason[i]);
            for (int k = 0; k < kinds; k++) h.Add(f.SettlementStructures[i * kinds + k]);
        }
        h.Add(f.PlotCount);
        for (int i = 0; i < f.PlotCount; i++) { h.Add(f.PlotTile[i]); h.Add(f.PlotSettlement[i]); h.Add(f.PlotKind[i]); }
        h.Add(f.CampCount);
        for (int i = 0; i < f.CampCount; i++) { h.Add(f.CampTile[i]); h.Add(f.CampCiv[i]); }
        h.Add(f.BeaconCount);
        for (int i = 0; i < f.BeaconCount; i++) { h.Add(f.BeaconTile[i]); h.Add(f.BeaconCiv[i]); }
        h.Add(f.PartyCount);
        for (int i = 0; i < f.PartyCount; i++)
        {
            h.Add(f.PartyKind[i]); h.Add(f.PartyCiv[i]); h.Add(f.PartyTargetCiv[i]); h.Add(f.PartyFrom[i]); h.Add(f.PartyTo[i]);
            h.Add(f.PartyX[i]); h.Add(f.PartyY[i]); h.Add(f.PartyProgress[i]); h.Add(f.PartyMembers[i]); h.Add(f.PartyArriveSeason[i]); h.Add(f.PartyLeg[i]);
        }
        h.Add(f.OccupationCount);
        for (int i = 0; i < f.OccupationCount; i++)
        {
            h.Add(f.OccupationCiv[i]); h.Add(f.OccupationSettlement[i]); h.Add(f.OccupationHolders[i]);
            h.Add(f.OccupationSinceSeason[i]); h.Add(f.OccupationRequiredSeasons[i]);
        }
        h.Add(f.CivCount);
        for (int c = 0; c < f.CivCount; c++)
        {
            h.Add(f.CivFlags[c]); h.Add(f.CivEra[c]); h.Add(f.CivBreadthDone[c]); h.Add(f.CivBreadthNeeded[c]); h.Add(f.CivLegacy[c]);
            h.Add(f.CivFirstsLegacy[c]); h.Add(f.CivDominion[c]); h.Add(f.CivLandBasisPoints[c]); h.Add(f.CivPosture[c]); h.Add(f.CivRelations[c]);
            h.Add(f.CivWeaponTier[c]); h.Add(f.CivPopulation[c]); h.Add(f.CivSettlements[c]); h.Add(f.CivProjectsDone[c]); h.Add(f.CivInsightPerYear[c]);
        }
        h.Add(f.FocusCiv);
        for (int i = 0; i < 3; i++) { h.Add(f.FocusSlotProject[i]); h.Add(f.FocusSlotPermille[i]); h.Add(f.FocusLeaning[i]); }
        h.Add(f.FocusQueueCount);
        for (int i = 0; i < f.FocusQueueCount; i++) h.Add(f.FocusQueue[i]);
        h.Add(f.FocusCouncil); h.Add(f.FocusBeaconsAllowed); h.Add(f.FocusBeaconsPlaced); h.Add(f.FocusEra); h.Add(f.FocusProjectCount);
        for (int i = 0; i < f.FocusProjectCount; i++) h.Add(f.FocusProjectState[i]);
        h.Add(f.EventCount); h.Add(f.LatestEventSequence);
        for (int i = 0; i < f.EventCount; i++) h.Add(HashEvent(f.Events[i]));
        return h.Finish();
    }

    public static ulong HashEvent(in ViewEvent e)
    {
        var h = new StateHasher();
        h.Add(e.Sequence); h.Add(e.Tick); h.Add(e.Figure); h.Add(e.Project); h.Add(e.Settlement); h.Add(e.Tile);
        h.Add(e.A); h.Add(e.B); h.Add(e.C); h.Add((int)e.Kind); h.Add((int)e.Text); h.Add((int)e.Tier); h.Add(e.Civ); h.Add(e.Other);
        return h.Finish();
    }
}

/// <summary>
/// The 30-minute rig: the politics-golden match with a viewer-0 builder (default ring), a viewer-0 builder with a small
/// ring and an omniscient builder, all every 10 ticks, and the per-frame invariants checked as it runs.
/// </summary>
internal sealed class WatchedMatch
{
    public readonly MatchModel Match = ViewKit.NewMatch();
    public readonly SimRunner Runner;
    public readonly FrameBuilder Viewer;
    public readonly FrameBuilder SmallRing;
    public readonly FrameBuilder Omniscient;
    public readonly MatchFrame ViewerFrame = new(), SmallFrame = new(), OmniFrame = new();
    public readonly List<string> Violations = new();
    public int ExploredObservations, RememberedSettlementObservations, PartiesSeen, FramesBuilt;
    public readonly List<ViewEvent> AllViewerEvents = new();

    public WatchedMatch(long ticks)
    {
        Runner = new SimRunner(Match);
        Viewer = new FrameBuilder(Match, 0);
        SmallRing = new FrameBuilder(Match, 0, eventCapacity: 64, eventWindow: 16);
        Omniscient = new FrameBuilder(Match, -1);
        var world = ViewKit.World.Value;
        var lastSeenOwner = new sbyte[world.TileCount];
        var everSeen = new bool[world.TileCount];
        long shown = 0;
        while (Runner.Tick < ticks)
        {
            Runner.Advance(10);
            Viewer.Build(Match, ViewerFrame);
            SmallRing.Build(Match, SmallFrame);
            Omniscient.Build(Match, OmniFrame);
            FramesBuilt++;
            MatchFrame f = ViewerFrame;
            for (int t = 0; t < f.Visibility.Length; t++)
            {
                var v = (TileVisibility)f.Visibility[t];
                if (v == TileVisibility.Visible) { everSeen[t] = true; lastSeenOwner[t] = f.Owner[t]; }
                else if (v == TileVisibility.Explored)
                {
                    ExploredObservations++;
                    if (!everSeen[t]) Violations.Add($"tick {f.Tick}: tile {t} explored but never seen");
                    else if (f.Owner[t] != lastSeenOwner[t]) Violations.Add($"tick {f.Tick}: tile {t} remembered owner {f.Owner[t]}, last seen {lastSeenOwner[t]}");
                    if (f.Claim[t] != 0) Violations.Add($"tick {f.Tick}: claim strength shown on unseen tile {t}");
                }
                else if (everSeen[t]) Violations.Add($"tick {f.Tick}: tile {t} forgotten");
            }
            for (int i = 0; i < f.SettlementCount; i++)
                if ((f.SettlementFlags[i] & (ushort)SettlementViewFlags.Remembered) != 0) RememberedSettlementObservations++;
            PartiesSeen += f.PartyCount;
            for (int i = 0; i < f.EventCount; i++)
            {
                ViewEvent e = f.Events[i];
                if (i > 0 && e.Sequence != f.Events[i - 1].Sequence + 1) Violations.Add($"tick {f.Tick}: event sequence gap");
                if (i > 0 && e.Tick < f.Events[i - 1].Tick) Violations.Add($"tick {f.Tick}: event ticks go back ({e.Kind} at {e.Tick})");
                if (e.Tick >= f.Tick) Violations.Add($"tick {f.Tick}: event from the future ({e.Kind} at {e.Tick})");
                if (e.Sequence <= shown) continue;
                if (shown != 0 && e.Sequence != shown + 1) Violations.Add($"tick {f.Tick}: the window skipped events {shown + 1} to {e.Sequence - 1}");
                shown = e.Sequence;
                AllViewerEvents.Add(e);
            }
        }
    }
}
