using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CivSim.Content;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Tests;

/// <summary>The real content, compiled once.</summary>
internal static class TestPack
{
    private static readonly Lazy<ContentPack> Cached = new(() =>
    {
        (ContentSet set, Report report) = ContentFixture.Load();
        ContentPack? pack = PackBuilder.Build(set, report);
        Assert.True(pack is not null, ContentFixture.Describe(report));
        return pack!;
    });

    public static ContentPack Pack => Cached.Value;
    public static ResearchCatalog Catalog { get; } = new(Cached.Value);
}

/// <summary>A scripted stand-in for the economy: fixed people, practice and exposure, so research rules can be tested alone.</summary>
internal sealed class FixedSource : IResearchSource
{
    private readonly ResearchCatalog _catalog;
    public Fix64 Adults = Fix64.FromInt(8), Elders = Fix64.FromInt(2);
    public Fix64 PeoplePerSettlement = Fix64.FromInt(12);
    public int Settlements = 1;
    public Fix64 InsightScale = Fix64.One;
    public ulong Seen;
    public UrgencySignals Signals { get; set; }
    public readonly Dictionary<string, Fix64> Work = new();
    public Fix64 PractisingShare { get; set; }
    public readonly List<(int Parent, int Daughter)> Fissions = new();
    public int ClimateGate = -1;

    public FixedSource(ResearchCatalog catalog, int civs = 1)
    {
        _catalog = catalog;
        CivCount = civs;
    }

    public int CivCount { get; }

    /// <summary>A source whose first civilization makes exactly this much Insight a year from the starting band.</summary>
    public static FixedSource WithInsightPerYear(ResearchCatalog catalog, int civs, double perYear)
    {
        Fix64 baseline = catalog.Constant("CommonsCoef") * FixMath.Pow(Fix64.FromInt(12), catalog.Constant("CommonsExp")) * catalog.Constant("InsightCalibration");
        return new FixedSource(catalog, civs) { InsightScale = Fix64.Parse(perYear.ToString(System.Globalization.CultureInfo.InvariantCulture)) / baseline };
    }

    public void BeginSeason(long tick) { }

    public InsightInputs GetInsightInputs(int civ) => new()
    {
        Adults = Adults, Elders = Elders, Settlements = Settlements,
        RoleHeads = new Fix64[_catalog.Roles.Count].Select(_ => Fix64.One).ToArray(),
        StructureOutput = Fix64.Zero, HearthShare = Fix64.One,
        InsightScale = InsightScale, RolesScale = Fix64.One, KnowHowScale = Fix64.One,
    };

    public void GetPractice(int civ, IResearchView view, Span<Fix64> work, out Fix64 practisingShare)
    {
        foreach (KeyValuePair<string, Fix64> kv in Work) work[_catalog.PracticeIndex(kv.Key)] += kv.Value;
        practisingShare = PractisingShare;
    }

    public UrgencySignals GetSignals(int civ) => Signals;
    public int SettlementCount(int civ) => Settlements;
    public Fix64 SettlementPeople(int civ, int settlement) => PeoplePerSettlement;
    public int AdoptionLagSeasons(int civ, int settlement) => settlement;
    public long ChooseInnovator(int civ, int project, int primaryPractice) => 1000 + civ;
    public ulong SeenMask(int civ, IResearchView view) => Seen;
    public int ClimateTarget(int civ) => ClimateGate;

    public void DrainFissions(int civ, List<(int Parent, int Daughter)> into)
    {
        into.AddRange(Fissions);
        Fissions.Clear();
    }

    public void Hash(ref StateHasher hasher) { hasher.Add(Settlements); hasher.Add(Seen); }
    public void WriteSnapshot(BinaryWriter writer) { writer.Write(Settlements); writer.Write(Seen); }
    public void ReadSnapshot(BinaryReader reader) { Settlements = reader.ReadInt32(); Seen = reader.ReadUInt64(); }
}

internal sealed class ResearchRig
{
    public readonly ResearchCatalog Catalog;
    public readonly FixedSource Source;
    public readonly ResearchSystem Research;
    public long Tick;

    public ResearchRig(int civs = 1, double insightPerYear = 0, bool council = false, int councilDelay = 0, Action<FixedSource>? configure = null,
        (int Craft, int Kin, int Reach)? leaning = null, ResearchCatalog? catalog = null)
    {
        Catalog = catalog ?? TestPack.Catalog;
        Source = insightPerYear > 0 ? FixedSource.WithInsightPerYear(Catalog, civs, insightPerYear) : new FixedSource(Catalog, civs);
        configure?.Invoke(Source);
        var (craft, kin, reach) = leaning ?? (34, 33, 33);
        var setups = new ResearchCivSetup[civs];
        Array.Fill(setups, new ResearchCivSetup(craft, kin, reach, council, councilDelay));
        Research = new ResearchSystem(Catalog, Source, 1, setups);
    }

    public int P(string id) => Catalog.IndexOf(id);

    /// <summary>Advances ticks, doing the season bookkeeping at season ends, as the match loop does.</summary>
    public void Run(long ticks, params Command[] first)
    {
        for (long i = 0; i < ticks; i++)
        {
            ReadOnlySpan<Command> commands = i == 0 ? first : ReadOnlySpan<Command>.Empty;
            Research.Step(Tick, commands);
            if ((Tick + 1) % ResearchSystem.SeasonTicks == 0)
            {
                Source.BeginSeason(Tick);
                Research.OnSeasonEnd(Tick);
            }
            Tick++;
        }
    }

    public void Do(params Command[] commands)
    {
        Research.Step(Tick, commands);
        if ((Tick + 1) % ResearchSystem.SeasonTicks == 0) Research.OnSeasonEnd(Tick);
        Tick++;
    }

    public Command Slot(int civ, int slot, string id) => new(Tick, (byte)civ, CommandKind.SetSlot, slot, id.Length == 0 ? -1 : P(id), 0);
    public Command Grant(int civ, string id) => new(Tick, (byte)civ, CommandKind.CompleteProject, P(id), 0, 0);
    public void GrantAll(int civ, params string[] ids) { foreach (string id in ids) Do(Grant(civ, id)); }

    public IEnumerable<ResearchEvent> Events(ResearchEventKind kind, string? project = null)
        => Research.Events.Where(e => e.Kind == kind && (project is null || e.Project == P(project)));
}
