using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;

namespace CivSim.Sim.Ai;

/// <summary>What the Strategist remembers about one rival between seasons. Hashed and saved with the seat.</summary>
internal sealed class RivalMemory
{
    public const int HistoryLength = 4;

    // Last season's facts, to notice hard events (14 §3.3).
    public int PrevRaidOnUs = -1000;
    public bool PrevAtWar, PrevAllied, PrevContacted;
    public int PrevAnnounced;
    // Threat episodes and our answers (14 §10.4 undefended loss).
    public int ThreatSince = -1;
    public int ThreatLast = -1000;
    public int LastDefenseSeason = -1000;
    // Threat history, for the Settler's late reactions (14 §9.2).
    public readonly int[] ThreatSeason = { -1000, -1000, -1000, -1000 };
    public readonly Fix64[] ThreatValue = new Fix64[HistoryLength];
    // The rival's believed field strength and its trend per season (windows, 14 §6.2).
    public Fix64 LastField, FieldTrend;
    // Threat rises at once and fades by a quarter a season, so one quiet season does not undo a defense.
    public Fix64 SmoothedThreat;
    // Raids, wars, campaigns.
    public int LastRaidOrder = -1000;
    public int LastRejectNote = -1000;
    public int WarDeclaredSeason = -1000;
    public int WarSince = -1;
    public bool WeDeclared;
    public int WarStartAdults;
    public int WarCaptures;
    public int LastAssaultSeason = -1000;
    public int LastAssaultTarget = -1;
    public int LastCaptureSeason = -1000;
    public int LastWarNote = -1000;
    public int LastGiftOrder = -1000;

    public void PushThreat(int season, Fix64 value)
    {
        Array.Copy(ThreatSeason, 1, ThreatSeason, 0, HistoryLength - 1);
        Array.Copy(ThreatValue, 1, ThreatValue, 0, HistoryLength - 1);
        ThreatSeason[HistoryLength - 1] = season;
        ThreatValue[HistoryLength - 1] = value;
    }

    /// <summary>The newest threat reading at least <paramref name="delay"/> seasons old, zero if none.</summary>
    public Fix64 ThreatAsOf(int season, int delay)
    {
        for (int i = HistoryLength - 1; i >= 0; i--)
            if (ThreatSeason[i] > -1000 && season - ThreatSeason[i] >= delay) return ThreatValue[i];
        return Fix64.Zero;
    }

    public void Hash(ref StateHasher h)
    {
        h.Add(PrevRaidOnUs); h.Add(PrevAtWar); h.Add(PrevAllied); h.Add(PrevContacted); h.Add(PrevAnnounced);
        h.Add(ThreatSince); h.Add(ThreatLast); h.Add(LastDefenseSeason);
        for (int i = 0; i < HistoryLength; i++) { h.Add(ThreatSeason[i]); h.Add(ThreatValue[i]); }
        h.Add(LastField); h.Add(FieldTrend); h.Add(SmoothedThreat); h.Add(LastRaidOrder); h.Add(LastRejectNote); h.Add(WarDeclaredSeason); h.Add(WarSince);
        h.Add(WeDeclared); h.Add(WarStartAdults); h.Add(WarCaptures); h.Add(LastAssaultSeason); h.Add(LastAssaultTarget);
        h.Add(LastCaptureSeason); h.Add(LastWarNote); h.Add(LastGiftOrder);
    }

    public void Write(BinaryWriter w)
    {
        w.Write(PrevRaidOnUs); w.Write(PrevAtWar); w.Write(PrevAllied); w.Write(PrevContacted); w.Write(PrevAnnounced);
        w.Write(ThreatSince); w.Write(ThreatLast); w.Write(LastDefenseSeason);
        for (int i = 0; i < HistoryLength; i++) { w.Write(ThreatSeason[i]); w.Write(ThreatValue[i].Raw); }
        w.Write(LastField.Raw); w.Write(FieldTrend.Raw); w.Write(SmoothedThreat.Raw); w.Write(LastRaidOrder); w.Write(LastRejectNote); w.Write(WarDeclaredSeason);
        w.Write(WarSince); w.Write(WeDeclared); w.Write(WarStartAdults); w.Write(WarCaptures); w.Write(LastAssaultSeason);
        w.Write(LastAssaultTarget); w.Write(LastCaptureSeason); w.Write(LastWarNote); w.Write(LastGiftOrder);
    }

    public void Read(BinaryReader r)
    {
        PrevRaidOnUs = r.ReadInt32(); PrevAtWar = r.ReadBoolean(); PrevAllied = r.ReadBoolean(); PrevContacted = r.ReadBoolean();
        PrevAnnounced = r.ReadInt32(); ThreatSince = r.ReadInt32(); ThreatLast = r.ReadInt32(); LastDefenseSeason = r.ReadInt32();
        for (int i = 0; i < HistoryLength; i++) { ThreatSeason[i] = r.ReadInt32(); ThreatValue[i] = Fix64.FromRaw(r.ReadInt64()); }
        LastField = Fix64.FromRaw(r.ReadInt64()); FieldTrend = Fix64.FromRaw(r.ReadInt64()); SmoothedThreat = Fix64.FromRaw(r.ReadInt64()); LastRaidOrder = r.ReadInt32();
        LastRejectNote = r.ReadInt32(); WarDeclaredSeason = r.ReadInt32(); WarSince = r.ReadInt32(); WeDeclared = r.ReadBoolean();
        WarStartAdults = r.ReadInt32(); WarCaptures = r.ReadInt32(); LastAssaultSeason = r.ReadInt32(); LastAssaultTarget = r.ReadInt32();
        LastCaptureSeason = r.ReadInt32(); LastWarNote = r.ReadInt32(); LastGiftOrder = r.ReadInt32();
    }
}

/// <summary>One believed rival settlement with the numbers the Strategist plans on (pessimistic where unsure).</summary>
internal sealed class Site
{
    public int Settlement, Tile, Stale, Population, Fighters;
    public Fix64 Fresh, PerDefend, PerAttack;
    public Defenses Defenses;
    public bool Distress;
    public Fix64 DefendStrength => PerDefend * Fighters;
}

/// <summary>A possible war target with predicted tactical outcome and a separate safe-to-launch result.</summary>
internal sealed class WarOption
{
    public int Civ, Settlement, Tile, Travel, Attackers, LandsSeason;
    public Fix64 OriginWinter;
    public Fix64 Ratio, Required, CaptureChance, Deaths, Wounded, Casualties, Value, Window;
    /// <summary>Tactical, value, and supply checks pass before strategic launch blockers are applied.</summary>
    public bool TacticallyViable;
    /// <summary>Safe to launch now after tactical checks and current strategic/political blockers.</summary>
    public bool Feasible, Palisade;
    public string Why = "";
}

/// <summary>This season's reading of one rival. Not state: rebuilt every season from the belief.</summary>
internal sealed class RivalRead
{
    public int Civ;
    public bool Known, Contacted;
    public readonly List<Site> Sites = new();
    public Fix64 Field, PWarlord;
    public int Distance = int.MaxValue;
    public int Reach;
    public Fix64 ThreatP, PAgainst, ExpectedLoss, Threat, EffectiveThreat;
    public bool Capable;
    public int LandSeen;
    public WarOption? War;
}

/// <summary>Everything read this season, shared by the decision steps.</summary>
internal sealed class SeasonRead
{
    public SeasonRead(StrategistView v, int civs)
    {
        Rivals = new RivalRead[civs];
        for (int c = 0; c < civs; c++) Rivals[c] = new RivalRead { Civ = c };
        Responses = new string?[civs];
    }

    public readonly RivalRead[] Rivals;
    public readonly string?[] Responses;
    public Fix64 OwnField, OwnDefense, LifeLegacy, Frontier, Urgency, CoalitionValue;
    public int HorizonYears;
    public int TopThreatCiv = -1;
    public int Leader = -1;
    public WarOption? BestWar;
    public bool WarPostureWanted;
    public bool HardEvent;
    public string HardWhy = "";
    public bool OrderedLaunch;

    public void Hard(string why)
    {
        HardWhy = HardEvent ? HardWhy + "; " + why : why;
        HardEvent = true;
    }

    public void Respond(int civ, string what)
    {
        if (civ < 0 || civ >= Responses.Length) return;
        Responses[civ] = Responses[civ] is null ? what : Responses[civ] + "; " + what;
    }
}
