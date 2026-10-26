using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

/// <summary>
/// One civilization's research state (docs/MVP/05 §16): completed set, progress, focus slots, queue, Leaning,
/// Know-how pools, adoption and the Legacy it earned. Plain data; <see cref="ResearchModel"/> holds the rules.
/// </summary>
public sealed class CivResearch
{
    public const int SlotCount = 3;
    public const int MaxSettlements = 16;

    public readonly int Civ;
    public int Era;
    public readonly bool[] Done;
    public readonly long[] DoneTick;
    public readonly Fix64[] Progress;
    /// <summary>Know-how already folded into each project's progress (capped at half its cost, docs/MVP/02 §9).</summary>
    public readonly Fix64[] KnowHowApplied;
    /// <summary>The civilization learned of the project through a contacted civilization that completed it.</summary>
    public readonly bool[] KnownByContact;
    public readonly bool[] Pioneer;
    public readonly ProjectState[] LastState;
    public readonly int[] DoneByEra = new int[ResearchCatalog.EraCount];
    public ulong SeenMask;
    /// <summary>Bit i set when access gate target i is available (own territory or trade); unset bits make projects with that [Has] gate Dormant.</summary>
    public ulong HasMask = ulong.MaxValue;
    public readonly int[] Slots = { -1, -1, -1 };
    /// <summary>Tick at which each slot last became empty (-1 while full), for the Council's one-season grace.</summary>
    public readonly long[] SlotEmptySince = { 0, 0, 0 };
    /// <summary>Insight that overflowed from a completion while the next project was not yet chosen, per slot.</summary>
    public readonly Fix64[] SlotCarry = new Fix64[SlotCount];
    public readonly List<int> Queue = new();
    /// <summary>Leaning weights for Craft, Kin, Reach as percentages.</summary>
    public readonly int[] Leaning = { 34, 33, 33 };
    public readonly Fix64[] KnowHow;
    public int ClimateTarget = -1;
    /// <summary>The Council fills empty slots on its own (always for AI; the player's Council toggle).</summary>
    public bool Council = true;
    /// <summary>An AI Spark (docs/MVP/14 §2) sets this civilization's slots and queue; the Council then only fills a slot the plan leaves empty and never re-ranks.</summary>
    public bool AiControlled;
    /// <summary>Ticks an empty slot waits before a fallback pick (30 for AI, 150 for the player's Council: docs/MVP/08 §7).</summary>
    public int CouncilDelay;
    public Fix64 EraBoonInsight = Fix64.One;
    public int BeaconBoons;
    public Fix64 MasteryLegacy;
    public Fix64 FirstsLegacy;
    /// <summary>Legacy for Wonders that stand built (docs/MVP/22 §3.3), and the Wonder projects already paid for.</summary>
    public Fix64 WondersLegacy;
    public readonly List<int> WondersBuilt = new();

    // Mandate (docs/MVP/22 §4.2): the stored state is the current Mandate and the recharge counter; cap, recharge and the
    // Admin figures below are derived from the completed projects (RebuildDerived) and need no snapshot of their own.
    /// <summary>Mandate in hand, 0 until KEY-3 grants it.</summary>
    public int Mandate;
    /// <summary>Seasons counted toward the next point of Mandate; never counts while the Mandate is full.</summary>
    public int MandateCounter;
    /// <summary>Mandate earned by recharge so far (statistics for T27 and the Mandate-overflow detector).</summary>
    public int MandateEarned;
    /// <summary>Mandate spent on Decrees so far.</summary>
    public int MandateSpent;
    /// <summary>True once any completed project carries a `mandate` effect (KEY-3).</summary>
    public bool MandateUnlocked;
    public int MandateCap;
    /// <summary>Seasons per point of Mandate, floored at MandateRechargeMin; meaningful only while <see cref="MandateUnlocked"/>.</summary>
    public int MandateRecharge;
    /// <summary>Admin span: settlements held without strain (docs/MVP/22 §8.2).</summary>
    public int AdminSpan;
    /// <summary>Product of (1 - r) over every completed `admin` effect; Overreach reduction is 1 - this (docs/MVP/22 §8.3).</summary>
    public Fix64 AdminKeep = Fix64.One;

    // Adoption: per project, the fraction of each settlement's people who know it.
    public readonly Fix64[]?[] Adoption;
    public readonly int[] AdoptionAge;
    public readonly byte[] AdoptionMilestone;

    // Caches rebuilt from the completed set (see ResearchModel.RebuildDerived).
    public Fix64[] ModMult;
    public Fix64[] ModFlat;
    public Fix64[] HazardMult = new Fix64[ResearchCatalog.CauseCount];
    public Fix64 AdoptMult = Fix64.One;
    public Fix64 KnowHowGain = Fix64.One;
    public Fix64[] KnowHowLineageGain;
    public Fix64[] CapMult = new Fix64[3];
    public Fix64 GroupCapValue;
    public bool DerivedDirty = true;

    // Insight cache, refreshed each season and whenever a completion changes it.
    public Fix64 InsightPerYear;
    public Fix64 CommonsPerYear;
    public Fix64 KnowHowPerYear;
    public bool InsightDirty = true;

    public CivResearch(int civ, int projectCount, int lineageCount, int statCount)
    {
        Civ = civ;
        Done = new bool[projectCount];
        DoneTick = new long[projectCount];
        Progress = new Fix64[projectCount];
        KnowHowApplied = new Fix64[projectCount];
        KnownByContact = new bool[projectCount];
        Pioneer = new bool[projectCount];
        LastState = new ProjectState[projectCount];
        KnowHow = new Fix64[lineageCount];
        KnowHowLineageGain = new Fix64[lineageCount];
        Adoption = new Fix64[]?[projectCount];
        AdoptionAge = new int[projectCount];
        AdoptionMilestone = new byte[projectCount];
        ModMult = new Fix64[statCount];
        ModFlat = new Fix64[statCount];
    }

    public int SlotOf(int project)
    {
        for (int i = 0; i < SlotCount; i++) if (Slots[i] == project) return i;
        return -1;
    }

    public void Hash(ref StateHasher h)
    {
        h.Add(Civ);
        h.Add(Era);
        h.Add(SeenMask);
        h.Add(HasMask);
        for (int i = 0; i < Done.Length; i++)
        {
            if (Done[i]) { h.Add(i); h.Add(DoneTick[i]); h.Add(Pioneer[i]); }
            else if (Progress[i].Raw != 0) { h.Add(-i - 1); h.Add(Progress[i]); h.Add(KnowHowApplied[i]); }
            if (KnownByContact[i]) h.Add(1_000_000 + i);
        }
        foreach (int s in Slots) h.Add(s);
        foreach (long t in SlotEmptySince) h.Add(t);
        foreach (Fix64 c in SlotCarry) h.Add(c);
        h.Add(Queue.Count);
        foreach (int q in Queue) h.Add(q);
        foreach (int l in Leaning) h.Add(l);
        h.Add(KnowHow);
        h.Add(Council);
        if (AiControlled) h.Add(0xA1C0A1C0A1C0A1C0UL); // only when set, so matches without an AI keep their checksums
        h.Add(CouncilDelay);
        h.Add(EraBoonInsight);
        h.Add(BeaconBoons);
        h.Add(MasteryLegacy);
        h.Add(FirstsLegacy);
        // Bronze state only once it exists, so matches that never reach KEY-3 or build a Wonder keep their checksums.
        if (WondersBuilt.Count > 0) { h.Add(WondersLegacy); h.Add(WondersBuilt.Count); foreach (int w in WondersBuilt) h.Add(w); }
        if (MandateUnlocked) { h.Add(0x4D414E44415445UL); h.Add(Mandate); h.Add(MandateCounter); h.Add(MandateEarned); h.Add(MandateSpent); }
        for (int p = 0; p < Adoption.Length; p++)
        {
            if (Adoption[p] is not { } a) continue;
            h.Add(p);
            h.Add(AdoptionAge[p]);
            h.Add(AdoptionMilestone[p]);
            h.Add(a);
        }
    }

    public void Write(BinaryWriter w)
    {
        w.Write(Era);
        w.Write(SeenMask);
        w.Write(HasMask);
        w.Write(ClimateTarget);
        w.Write(Council);
        w.Write(AiControlled);
        w.Write(CouncilDelay);
        w.Write(EraBoonInsight.Raw);
        w.Write(BeaconBoons);
        w.Write(MasteryLegacy.Raw);
        w.Write(FirstsLegacy.Raw);
        w.Write(WondersLegacy.Raw);
        w.Write(WondersBuilt.Count);
        foreach (int wonder in WondersBuilt) w.Write(wonder);
        w.Write(Mandate);
        w.Write(MandateCounter);
        w.Write(MandateEarned);
        w.Write(MandateSpent);
        for (int i = 0; i < Done.Length; i++)
        {
            w.Write(Done[i]);
            w.Write(DoneTick[i]);
            w.Write(Progress[i].Raw);
            w.Write(KnowHowApplied[i].Raw);
            w.Write(KnownByContact[i]);
            w.Write(Pioneer[i]);
            w.Write((byte)LastState[i]);
            w.Write(AdoptionAge[i]);
            w.Write(AdoptionMilestone[i]);
            if (Adoption[i] is { } a)
            {
                w.Write(true);
                for (int s = 0; s < MaxSettlements; s++) w.Write(a[s].Raw);
            }
            else w.Write(false);
        }
        foreach (int c in DoneByEra) w.Write(c);
        foreach (int s in Slots) w.Write(s);
        foreach (long t in SlotEmptySince) w.Write(t);
        foreach (Fix64 c in SlotCarry) w.Write(c.Raw);
        w.Write(Queue.Count);
        foreach (int q in Queue) w.Write(q);
        foreach (int l in Leaning) w.Write(l);
        w.Write(KnowHow.Length);
        foreach (Fix64 k in KnowHow) w.Write(k.Raw);
    }

    public void Read(BinaryReader r)
    {
        Era = r.ReadInt32();
        SeenMask = r.ReadUInt64();
        HasMask = r.ReadUInt64();
        ClimateTarget = r.ReadInt32();
        Council = r.ReadBoolean();
        AiControlled = r.ReadBoolean();
        CouncilDelay = r.ReadInt32();
        EraBoonInsight = Fix64.FromRaw(r.ReadInt64());
        BeaconBoons = r.ReadInt32();
        MasteryLegacy = Fix64.FromRaw(r.ReadInt64());
        FirstsLegacy = Fix64.FromRaw(r.ReadInt64());
        WondersLegacy = Fix64.FromRaw(r.ReadInt64());
        int wonders = r.ReadInt32();
        WondersBuilt.Clear();
        for (int i = 0; i < wonders; i++) WondersBuilt.Add(r.ReadInt32());
        Mandate = r.ReadInt32();
        MandateCounter = r.ReadInt32();
        MandateEarned = r.ReadInt32();
        MandateSpent = r.ReadInt32();
        for (int i = 0; i < Done.Length; i++)
        {
            Done[i] = r.ReadBoolean();
            DoneTick[i] = r.ReadInt64();
            Progress[i] = Fix64.FromRaw(r.ReadInt64());
            KnowHowApplied[i] = Fix64.FromRaw(r.ReadInt64());
            KnownByContact[i] = r.ReadBoolean();
            Pioneer[i] = r.ReadBoolean();
            LastState[i] = (ProjectState)r.ReadByte();
            AdoptionAge[i] = r.ReadInt32();
            AdoptionMilestone[i] = r.ReadByte();
            if (r.ReadBoolean())
            {
                var a = new Fix64[MaxSettlements];
                for (int s = 0; s < MaxSettlements; s++) a[s] = Fix64.FromRaw(r.ReadInt64());
                Adoption[i] = a;
            }
            else Adoption[i] = null;
        }
        for (int i = 0; i < DoneByEra.Length; i++) DoneByEra[i] = r.ReadInt32();
        for (int i = 0; i < SlotCount; i++) Slots[i] = r.ReadInt32();
        for (int i = 0; i < SlotCount; i++) SlotEmptySince[i] = r.ReadInt64();
        for (int i = 0; i < SlotCount; i++) SlotCarry[i] = Fix64.FromRaw(r.ReadInt64());
        int queued = r.ReadInt32();
        Queue.Clear();
        for (int i = 0; i < queued; i++) Queue.Add(r.ReadInt32());
        for (int i = 0; i < Leaning.Length; i++) Leaning[i] = r.ReadInt32();
        int lineages = r.ReadInt32();
        if (lineages != KnowHow.Length) throw new InvalidDataException("Snapshot lineage count differs from the content pack.");
        for (int i = 0; i < lineages; i++) KnowHow[i] = Fix64.FromRaw(r.ReadInt64());
        DerivedDirty = true;
        InsightDirty = true;
    }
}
