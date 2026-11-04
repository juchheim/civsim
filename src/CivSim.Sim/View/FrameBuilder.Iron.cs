using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;

namespace CivSim.Sim.View;

/// <summary>
/// The Iron chapter's block of the frame (docs/MVP/25 §12). Everything is derived from the same state the rules use and filtered by what the viewer may see:
/// works counts for settlements seen now, staff and build detail for the viewer's own settlements, and the sieges, blockades, tributes and leagues the viewer is
/// party to or can see. Nothing is written back to the simulation.
/// </summary>
public sealed partial class FrameBuilder
{
    private static readonly string[] WonderStructures = { "great_library", "great_lighthouse", "great_dome" };

    private void FillIron(MatchModel match, MatchFrame target)
    {
        IronFrame f = target.Iron;
        LivingEconomyModel eco = match.Economy;
        PoliticsSystem pol = match.Politics;
        f.Active = eco.IronChain;
        f.SiegeCount = f.BlockadeCount = f.TributeCount = f.LeagueCount = 0;
        f.RoadCount = f.RoadTileCount = 0;
        for (int k = 0; k < IronFrame.WonderKinds; k++) f.WonderSettlement[k] = -1;
        if (!f.Active) return;

        f.WorkKinds = LivingEconomyModel.IronWorkKindCount;
        int kinds = f.WorkKinds, n = target.SettlementCount;
        f.EnsureSettlements(n);
        for (int w = 0; w < n; w++)
        {
            int s = target.SettlementIndex[w];
            SettlementViewFlags flags = (SettlementViewFlags)target.SettlementFlags[w];
            bool remembered = (flags & SettlementViewFlags.Remembered) != 0;
            bool own = (flags & (SettlementViewFlags.Own | SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) == SettlementViewFlags.Own;
            for (int k = 0; k < kinds; k++)
                f.Works[w * kinds + k] = remembered || (uint)s >= (uint)_settlementCount ? (byte)0 : (byte)Math.Min(255, eco.IronWorkAt(s, k));
            f.OurFaithPermille[w] = 0; f.ForeignFaithPermille[w] = 0; f.ForeignFaithCiv[w] = -1;
            if (!own || (uint)s >= (uint)_settlementCount)
            {
                f.Soldiers[w] = f.Scholars[w] = f.Missionaries[w] = 0;
                f.BuildKind[w] = -1; f.BuildPermille[w] = 0; f.PledgeTo[w] = -1;
                if (!remembered && !_omniscient && (uint)s < (uint)_settlementCount && (flags & SettlementViewFlags.FreeFolk) == 0)
                    f.OurFaithPermille[w] = (short)(eco.FaithShare(s, _viewer) * 1000).FloorToInt();
                continue;
            }
            f.Soldiers[w] = (byte)Math.Min(255, eco.SoldiersAt(s));
            f.Scholars[w] = (byte)Math.Min(255, eco.ScholarsAt(s));
            f.Missionaries[w] = (byte)Math.Min(255, eco.Missionaries(s));
            for (int civ = 0; civ < _civs; civ++)
            {
                short share = (short)(eco.FaithShare(s, civ) * 1000).FloorToInt();
                if (share > f.ForeignFaithPermille[w]) { f.ForeignFaithPermille[w] = share; f.ForeignFaithCiv[w] = (sbyte)civ; }
            }
            (int buildKind, int permille) = eco.IronBuildAt(s);
            f.BuildKind[w] = (sbyte)buildKind;
            f.BuildPermille[w] = (short)permille;
            f.PledgeTo[w] = (short)eco.PledgeOf(s);
        }

        // Paved roads: each linked settlement's road to its capital. The viewer's own come whole; another civilization's only where the viewer sees the tiles.
        int roadKind = -1;
        for (int k = 0; k < kinds; k++) if (LivingEconomyModel.IronWorkId(k) == "road") roadKind = k;
        if (roadKind >= 0)
        {
            var line = new List<int>();
            for (int w = 0; w < n; w++)
            {
                int s = target.SettlementIndex[w];
                SettlementViewFlags flags = (SettlementViewFlags)target.SettlementFlags[w];
                if ((flags & (SettlementViewFlags.Remembered | SettlementViewFlags.FreeFolk)) != 0 || (uint)s >= (uint)_settlementCount) continue;
                if (f.Works[w * kinds + roadKind] == 0) continue;
                line.Clear();
                if (!eco.RoadTilesForView(s, line, out bool bridged)) continue;
                bool own = _omniscient || (flags & SettlementViewFlags.Own) != 0;
                f.EnsureRoads(f.RoadCount + 1);
                f.EnsureRoadTiles(f.RoadTileCount + line.Count);
                int r = f.RoadCount++;
                f.RoadSettlement[r] = w; f.RoadStart[r] = f.RoadTileCount; f.RoadLength[r] = line.Count; f.RoadBridged[r] = (byte)(bridged ? 1 : 0);
                foreach (int tile in line) f.RoadTiles[f.RoadTileCount++] = own || Visible(tile) ? tile : -1;
            }
        }

        // Sieges: the viewer's own, or any whose town it can see.
        f.EnsureSieges(pol.Investments.Count);
        foreach (Investment inv in pol.Investments)
        {
            if (!_omniscient && inv.Civ != _viewer && inv.TargetCiv != _viewer && !Visible(_sTile[inv.Settlement])) continue;
            int i = f.SiegeCount++;
            f.SiegeSettlement[i] = inv.Settlement; f.SiegeBesieger[i] = inv.Civ; f.SiegeEngines[i] = inv.Engines;
            f.SiegeSeasons[i] = Math.Max(0, target.Season - inv.BeganSeason);
            f.SiegeIntegrityPermille[i] = (short)Math.Clamp((inv.Integrity * 1000).FloorToInt(), 0, 1000);
        }

        f.EnsureBlockades(pol.Blockades.Count);
        foreach (Blockade b in pol.Blockades)
        {
            if (!_omniscient && b.Civ != _viewer && b.TargetCiv != _viewer && !Visible(_sTile[b.Harbor])) continue;
            int i = f.BlockadeCount++;
            f.BlockadeHarbor[i] = b.Harbor; f.BlockadeCiv[i] = b.Civ; f.BlockadeOrigin[i] = b.Origin;
        }

        f.EnsureTributes(pol.Tributes.Count);
        foreach (Tribute t in pol.Tributes)
        {
            if (!_omniscient && t.Overlord != _viewer && t.Subject != _viewer) continue;
            int i = f.TributeCount++;
            f.TributeOverlord[i] = t.Overlord; f.TributeSubject[i] = t.Subject;
        }

        f.EnsureLeagues(pol.Leagues.Count);
        foreach (League league in pol.Leagues)
        {
            int mask = 0;
            foreach (int member in league.Members) mask |= 1 << member;
            if (!_omniscient && (mask & (1 << _viewer)) == 0) continue;
            f.LeagueMembers[f.LeagueCount++] = mask;
        }

        // A Wonder is public once built, but where it stands is shown only for a town the viewer knows.
        for (int k = 0; k < IronFrame.WonderKinds; k++)
        {
            int holder = eco.WonderSettlement(WonderStructures[k]);
            if (holder >= 0 && (_omniscient || target.IndexOfSettlement(holder) >= 0)) f.WonderSettlement[k] = holder;
        }
    }
}
