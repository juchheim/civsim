using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Ai;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.World;

namespace CivSim.Sim.Research;

/// <summary>
/// The match so far: M3's living economy driven by M4's research. Research decides what completes and when; each
/// completion is applied to the economy as a <see cref="CommandKind.CompleteProject"/>, so the people change what
/// they do (docs/MVP/05 §16). Research reads the economy back for Insight, practice and exposure.
/// <para>Order within a tick: commands and the research step, then the economy tick, then (on a season boundary)
/// the economy's season totals are read and research does its season bookkeeping.</para>
/// </summary>
public sealed class MatchModel : ISimModel
{
    private readonly List<Command> _economyCommands = new();
    private readonly List<(int Civ, int Project)> _completions = new();
    private readonly List<(int Civ, string Project)> _wonders = new();

    public MatchModel(LivingEconomyModel economy, ResearchSystem research, LivingEconomySource source, PoliticsSystem politics,
        AiSystem? ai = null)
    {
        Ai = ai;
        Economy = economy;
        Research = research;
        Source = source;
        Politics = politics;
        source.FreeFolk = politics;
    }

    public LivingEconomyModel Economy { get; }
    public ResearchSystem Research { get; }
    public LivingEconomySource Source { get; }
    public PoliticsSystem Politics { get; }
    /// <summary>The AI civilizations' minds; null when every civilization is on the Council.</summary>
    public AiSystem? Ai { get; }

    /// <summary>
    /// A standard four-civilization match on the world generated from <paramref name="seed"/>. Civilization 0 is the
    /// player (the Council may fill an empty slot after one season); the others are AI with their archetype's Leaning.
    /// </summary>
    public static MatchModel Create(ulong seed, ContentPack pack, ReadOnlySpan<EconomyArchetype> archetypes = default)
    {
        GeneratedWorld generated = WorldGenerator.Generate(seed);
        return Create(seed, pack, generated, archetypes);
    }

    public static MatchModel Create(ulong seed, ContentPack pack, GeneratedWorld generated,
        ReadOnlySpan<EconomyArchetype> archetypes = default)
        => Create(seed, pack, generated, archetypes, ReadOnlySpan<CivControl>.Empty);

    /// <summary>
    /// A match in which each civilization has its own control: the Council, the smart AI at a level, or a scripted bot
    /// (docs/MVP/14 §10.2). An AI civilization's archetype is its personality and replaces the economy archetype given.
    /// </summary>
    public static MatchModel Create(ulong seed, ContentPack pack, GeneratedWorld generated,
        ReadOnlySpan<EconomyArchetype> archetypes, ReadOnlySpan<CivControl> controls, bool playerCouncil = true,
        bool gentleStart = false)
    {
        var catalog = new ResearchCatalog(pack);
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(generated);
        var profiles = new EconomyArchetype[world.CivilizationCount];
        if (!archetypes.IsEmpty) archetypes.CopyTo(profiles);
        if (!controls.IsEmpty && controls.Length != profiles.Length) throw new ArgumentException("One control per civilization.", nameof(controls));
        for (int c = 0; c < controls.Length; c++)
            if (controls[c].Kind == ControlKind.Ai) profiles[c] = controls[c].Spec.Archetype;
        var economy = new LivingEconomyModel(seed, EconomyContent.FromPack(pack), world, profiles, gentleStart);
        var source = new LivingEconomySource(economy, world, generated, catalog, profiles);
        var setups = new ResearchCivSetup[profiles.Length];
        for (int c = 0; c < setups.Length; c++)
        {
            (int craft, int kin, int reach) = Leaning(pack, profiles[c]);
            bool humanSeat = c == 0 && (controls.IsEmpty || controls[c].Kind == ControlKind.Council);
            setups[c] = humanSeat
                ? ResearchCivSetup.Player(craft, kin, reach, playerCouncil)
                : ResearchCivSetup.Ai(craft, kin, reach);
        }
        var research = new ResearchSystem(catalog, source, seed, setups);
        var politics = new PoliticsSystem(economy, research, generated, profiles, seed, gentleStart);
        AiSystem? ai = null;
        foreach (CivControl control in controls)
            if (control.Kind != ControlKind.Council) { ai = new AiSystem(economy, research, politics, generated, pack, seed, controls); break; }
        return new MatchModel(economy, research, source, politics, ai);
    }

    /// <summary>An archetype's starting Leaning in percent (docs/MVP/14 §8): from the archetypes registry, equal for Balanced.</summary>
    public static (int Craft, int Kin, int Reach) Leaning(ContentPack pack, EconomyArchetype archetype)
    {
        string id = archetype.ToString().ToLowerInvariant();
        foreach (ContentValue a in pack.Root.Get("registries")!.Get("archetypes")!.Items)
        {
            if (a.GetString("id") != id || a.Get("leaning") is not { } l) continue;
            return (ToPercent(l.GetFix("craft")), ToPercent(l.GetFix("kin")), ToPercent(l.GetFix("reach")));
        }
        return (34, 33, 33);
    }

    private static int ToPercent(Fix64? share) => share is { } s ? (s * 100).RoundToInt() : 33;

    /// <summary>
    /// When the Bronze chapter ends as things stand (docs/MVP/22 §3.1): at the end of the season in which the human completes
    /// KEY-4, 12 seasons after the first rival does, or at Year 64. Pass the human's civilization, or -1 for a headless match.
    /// A pure function of the checksummed research state, so every replay and restore agrees on it.
    /// </summary>
    public MatchEnd EndOfMatch(int humanSeat = -1) => MatchEnd.Evaluate(Research, humanSeat);

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        Research.Step(tick, commands);
        Ai?.AfterResearchStep(tick);
        Politics.Step(tick, commands);
        _economyCommands.Clear();
        foreach (Command command in commands)
            if (command.Kind is CommandKind.CompleteProject or CommandKind.SetScoutPolicy) _economyCommands.Add(command);
        _completions.Clear();
        Research.TakeCompletions(_completions);
        foreach ((int civ, int project) in _completions)
            _economyCommands.Add(new Command(tick, (byte)civ, CommandKind.CompleteProject, project, 0, 0));
        Economy.Step(tick, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_economyCommands));
        if ((tick + 1) % ResearchSystem.SeasonTicks == 0)
        {
            Source.BeginSeason(tick);
            // A Wonder the economy finished this season earns its Legacy (docs/MVP/22 §3.3).
            _wonders.Clear();
            Economy.DrainWondersBuilt(_wonders);
            foreach ((int civ, string project) in _wonders) Research.AwardWonderBuilt(civ, Research.Catalog.IndexOf(project));
            Research.OnSeasonEnd(tick);
            Politics.OnSeasonEnd(tick);
            Ai?.OnSeasonEnd(tick);
        }
    }

    public ulong ComputeChecksum()
    {
        var hasher = new StateHasher();
        hasher.Add(Economy.ComputeChecksum());
        Research.Hash(ref hasher);
        Politics.Hash(ref hasher);
        Ai?.Hash(ref hasher);
        return hasher.Finish();
    }

    public void WriteSnapshot(BinaryWriter writer)
    {
        // Politics goes before research: research's restore re-reads the economy through a source that asks politics which settlements are Free Folk.
        Economy.WriteSnapshot(writer);
        Politics.WriteSnapshot(writer);
        Research.WriteSnapshot(writer);
        Ai?.Write(writer);
    }

    public void ReadSnapshot(BinaryReader reader)
    {
        Economy.ReadSnapshot(reader);
        Politics.ReadSnapshot(reader);
        Research.ReadSnapshot(reader);
        Ai?.Read(reader);
    }
}
