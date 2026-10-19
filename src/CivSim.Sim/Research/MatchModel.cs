using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
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

    public MatchModel(LivingEconomyModel economy, ResearchSystem research, LivingEconomySource source)
    {
        Economy = economy;
        Research = research;
        Source = source;
    }

    public LivingEconomyModel Economy { get; }
    public ResearchSystem Research { get; }
    public LivingEconomySource Source { get; }

    /// <summary>
    /// A standard four-civilization match on the world generated from <paramref name="seed"/>. Civilization 0 is the
    /// player (the Council fills an empty slot after one season); the others are AI with their archetype's Leaning.
    /// </summary>
    public static MatchModel Create(ulong seed, ContentPack pack, ReadOnlySpan<EconomyArchetype> archetypes = default)
    {
        GeneratedWorld generated = WorldGenerator.Generate(seed);
        return Create(seed, pack, generated, archetypes);
    }

    public static MatchModel Create(ulong seed, ContentPack pack, GeneratedWorld generated,
        ReadOnlySpan<EconomyArchetype> archetypes = default)
    {
        var catalog = new ResearchCatalog(pack);
        WorldEconomyInput world = WorldEconomyInput.FromGeneratedWorld(generated);
        var profiles = new EconomyArchetype[world.CivilizationCount];
        if (!archetypes.IsEmpty) archetypes.CopyTo(profiles);
        var economy = new LivingEconomyModel(seed, EconomyContent.FromPack(pack), world, profiles);
        var source = new LivingEconomySource(economy, world, generated, catalog, profiles);
        var setups = new ResearchCivSetup[profiles.Length];
        for (int c = 0; c < setups.Length; c++)
        {
            (int craft, int kin, int reach) = Leaning(pack, profiles[c]);
            setups[c] = c == 0 ? ResearchCivSetup.Player(craft, kin, reach) : ResearchCivSetup.Ai(craft, kin, reach);
        }
        var research = new ResearchSystem(catalog, source, seed, setups);
        return new MatchModel(economy, research, source);
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

    public void Step(long tick, ReadOnlySpan<Command> commands)
    {
        Research.Step(tick, commands);
        _economyCommands.Clear();
        foreach (Command command in commands)
            if (command.Kind == CommandKind.CompleteProject) _economyCommands.Add(command);
        _completions.Clear();
        Research.TakeCompletions(_completions);
        foreach ((int civ, int project) in _completions)
            _economyCommands.Add(new Command(tick, (byte)civ, CommandKind.CompleteProject, project, 0, 0));
        Economy.Step(tick, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_economyCommands));
        if ((tick + 1) % ResearchSystem.SeasonTicks == 0)
        {
            Source.BeginSeason(tick);
            Research.OnSeasonEnd(tick);
        }
    }

    public ulong ComputeChecksum()
    {
        var hasher = new StateHasher();
        hasher.Add(Economy.ComputeChecksum());
        Research.Hash(ref hasher);
        return hasher.Finish();
    }

    public void WriteSnapshot(BinaryWriter writer)
    {
        Economy.WriteSnapshot(writer);
        Research.WriteSnapshot(writer);
    }

    public void ReadSnapshot(BinaryReader reader)
    {
        Economy.ReadSnapshot(reader);
        Research.ReadSnapshot(reader);
    }
}
