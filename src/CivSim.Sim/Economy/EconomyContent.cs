using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Economy;

/// <summary>A project's stable pack index and its contiguous range in the flattened effect array.</summary>
public readonly record struct EconomyProject(int Index, string Id, int Era, int EnterEra, int EffectStart, int EffectCount);

/// <summary>
/// A typed mechanical effect. Missing fields stay absent: for example a method has either Rate or Mult,
/// while a group cap has Value. Text notes are deliberately not interpreted by this adapter.
/// </summary>
public readonly record struct EconomyEffect(
    int ProjectIndex,
    string Kind,
    string? Action,
    string? Stat,
    string? Operation,
    string? Cap,
    string? Good,
    string? Structure,
    string? Cause,
    string? Scope,
    int? Rung,
    Fix64? Value,
    Fix64? Mult,
    Fix64? Take,
    Fix64? Rate,
    string? Unit);

/// <summary>An action's optional base rate; many activities acquire a rate from a method instead.</summary>
public readonly record struct EconomyAction(string Id, Fix64? BaseRate, string? Unit);

/// <summary>A good's per-season spoilage fraction. Durable goods have zero spoilage.</summary>
public readonly record struct EconomyGood(string Id, Fix64 Spoilage);

/// <summary>
/// Parses the compiled pack once into ordered economy definitions. Project, effect and registry order
/// is retained exactly, including definitions beyond E2; callers decide which eras and effects to use.
/// This object holds definitions only and never reads source content or evaluates prose rules.
/// </summary>
public sealed class EconomyContent
{
    private readonly EconomyProject[] _projects;
    private readonly EconomyEffect[] _effects;
    private readonly EconomyAction[] _actions;
    private readonly EconomyGood[] _goods;
    public ReadOnlySpan<EconomyProject> Projects => _projects;
    public ReadOnlySpan<EconomyEffect> Effects => _effects;
    public ReadOnlySpan<EconomyAction> Actions => _actions;
    public ReadOnlySpan<EconomyGood> Goods => _goods;

    private EconomyContent(EconomyProject[] projects, EconomyEffect[] effects,
        EconomyAction[] actions, EconomyGood[] goods)
    {
        _projects = projects;
        _effects = effects;
        _actions = actions;
        _goods = goods;
    }

    public static EconomyContent FromPack(ContentPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        IReadOnlyList<ContentValue> sourceProjects = pack.List("projects");
        var projects = new EconomyProject[sourceProjects.Count];
        var effects = new List<EconomyEffect>();

        for (int i = 0; i < sourceProjects.Count; i++)
        {
            ContentValue project = sourceProjects[i];
            int index = RequiredInt(project, "idx");
            string id = RequiredString(project, "id");
            int era = RequiredInt(project, "era");
            int start = effects.Count;
            foreach (ContentValue effect in List(project, "effects"))
            {
                string kind = RequiredString(effect, "kind");
                effects.Add(new EconomyEffect(
                    index,
                    kind,
                    effect.GetString("action"),
                    effect.GetString("stat"),
                    effect.GetString("op"),
                    effect.GetString("cap"),
                    kind == "good" ? effect.GetString("id") : null,
                    effect.GetString("structure"),
                    effect.GetString("cause"),
                    effect.GetString("scope"),
                    OptionalInt(effect, "rung"),
                    effect.GetFix("value"),
                    effect.GetFix("mult"),
                    effect.GetFix("take"),
                    effect.GetFix("rate"),
                    effect.GetString("unit")));
            }
            projects[i] = new EconomyProject(index, id, era, OptionalInt(project, "enters") ?? era,
                start, effects.Count - start);
        }

        ContentValue? registries = pack.Root.Get("registries");
        IReadOnlyList<ContentValue> sourceActions = List(registries, "actions");
        var actions = new EconomyAction[sourceActions.Count];
        for (int i = 0; i < sourceActions.Count; i++)
        {
            ContentValue action = sourceActions[i];
            actions[i] = new EconomyAction(RequiredString(action, "id"),
                action.GetFix("base_rate"), action.GetString("unit"));
        }

        IReadOnlyList<ContentValue> sourceGoods = List(registries, "goods");
        var goods = new EconomyGood[sourceGoods.Count];
        for (int i = 0; i < sourceGoods.Count; i++)
        {
            ContentValue good = sourceGoods[i];
            goods[i] = new EconomyGood(RequiredString(good, "id"), good.GetFix("spoilage") ?? Fix64.Zero);
        }

        return new EconomyContent(projects, effects.ToArray(), actions, goods);
    }

    private static IReadOnlyList<ContentValue> List(ContentValue? parent, string field)
    {
        ContentValue? value = parent?.Get(field);
        if (value is null || value.IsNull) return Array.Empty<ContentValue>();
        if (value.Kind != ValueKind.List)
            throw new ArgumentException($"Economy content field '{field}' must be a list.");
        return value.Items;
    }

    private static string RequiredString(ContentValue value, string field)
        => value.GetString(field) ?? throw new ArgumentException($"Economy content requires text field '{field}'.");

    private static int RequiredInt(ContentValue value, string field)
        => OptionalInt(value, field) ?? throw new ArgumentException($"Economy content requires integer field '{field}'.");

    private static int? OptionalInt(ContentValue value, string field)
    {
        long? number = value.GetInt(field);
        return number is null ? null : checked((int)number.Value);
    }
}
