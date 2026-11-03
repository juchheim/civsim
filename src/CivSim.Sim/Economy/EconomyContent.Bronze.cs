using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Economy;

/// <summary>One recipe input: the goods that can fill it, first choice first (a YAML key "A|B"), and the quantity per batch.</summary>
public readonly record struct EconomyRecipeInput(string[] Goods, Fix64 Quantity);

/// <summary>One recipe output and its quantity per batch.</summary>
public readonly record struct EconomyRecipeOutput(string Good, Fix64 Quantity);

/// <summary>
/// A recipe of the craft step (docs/MVP/23 §5): run in ascending <see cref="Order"/>, then id, each limited by its
/// scarcest input. <see cref="UnlockedBy"/> is any-of project indices, <see cref="Structures"/> any-of structure ids
/// (empty when none is needed).
/// </summary>
public sealed record EconomyRecipe(
    string Id, int Order, int[] UnlockedBy, string[] Structures, EconomyRecipeInput[] Inputs, EconomyRecipeOutput[] Outputs,
    Fix64 Labor, string Practice, bool Bellows);

/// <summary>
/// A metal tool tier (docs/MVP/23 §4.1): the kit's metal and its amount, the work-seasons of tool work from a new kit to
/// its replacement, and the per-figure multipliers it gives the figure carrying it (typed <c>mods</c>, applied once).
/// </summary>
public sealed class EconomyToolTier
{
    private readonly string[] _modKeys;
    private readonly Fix64[] _modValues;

    public EconomyToolTier(string id, int tier, int[] unlockedBy, string metal, Fix64 metalPerKit, Fix64 serviceWs,
        string[] modKeys, Fix64[] modValues)
    {
        Id = id; Tier = tier; UnlockedBy = unlockedBy; Metal = metal; MetalPerKit = metalPerKit; ServiceWs = serviceWs;
        _modKeys = modKeys; _modValues = modValues;
    }

    public string Id { get; }
    /// <summary>8, 9 or 10.</summary>
    public int Tier { get; }
    public int[] UnlockedBy { get; }
    public string Metal { get; }
    public Fix64 MetalPerKit { get; }
    public Fix64 ServiceWs { get; }
    public int ModCount => _modKeys.Length;
    public string ModKey(int i) => _modKeys[i];
    public Fix64 ModValue(int i) => _modValues[i];

    /// <summary>The multiplier for a stat (1 when the tier has none for it).</summary>
    public Fix64 Mod(string stat)
    {
        int i = Array.BinarySearch(_modKeys, stat, StringComparer.Ordinal);
        return i >= 0 ? _modValues[i] : Fix64.One;
    }
}

/// <summary>A good's trade facts (docs/MVP/23 §3): category, barter value per unit and, for goods that wear by age, service seasons.</summary>
public readonly record struct EconomyTradeGood(string Id, string Category, Fix64 Barter, int ServiceSeasons, int IntroducedEra = 0);

public sealed partial class EconomyContent
{
    private EconomyRecipe[] _recipes = Array.Empty<EconomyRecipe>();
    private EconomyToolTier[] _toolTiers = Array.Empty<EconomyToolTier>();
    private EconomyTradeGood[] _tradeGoods = Array.Empty<EconomyTradeGood>();
    private string[] _constantIds = Array.Empty<string>();
    private Fix64[] _constantValues = Array.Empty<Fix64>();

    /// <summary>Recipes in the order the craft step runs them: ascending order, then id (ordinal).</summary>
    public ReadOnlySpan<EconomyRecipe> Recipes => _recipes;
    /// <summary>Metal tool tiers in ascending tier.</summary>
    public ReadOnlySpan<EconomyToolTier> ToolTiers => _toolTiers;
    /// <summary>Every good of the registry with its barter value, in id order.</summary>
    public ReadOnlySpan<EconomyTradeGood> TradeGoods => _tradeGoods;

    /// <summary>
    /// Fingerprint of the Bronze definitions (recipes, tool tiers, trade goods and constants). The economy hashes it only
    /// once the Bronze Age has begun, so earlier checksums do not depend on these registries.
    /// </summary>
    public ulong BronzeFingerprint { get; private set; }

    /// <summary>A balance constant from the pack (<c>balance.constants</c>), or <paramref name="fallback"/> when the pack lacks it.</summary>
    public Fix64 Constant(string id, Fix64 fallback)
    {
        int i = Array.BinarySearch(_constantIds, id, StringComparer.Ordinal);
        return i >= 0 ? _constantValues[i] : fallback;
    }

    /// <summary>The barter value of a good, 0 when unknown.</summary>
    public Fix64 BarterValue(string goodId)
    {
        int i = TradeGoodIndex(goodId);
        return i >= 0 ? _tradeGoods[i].Barter : Fix64.Zero;
    }

    public int TradeGoodIndex(string goodId)
    {
        int low = 0, high = _tradeGoods.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            int order = string.CompareOrdinal(_tradeGoods[mid].Id, goodId);
            if (order == 0) return mid;
            if (order < 0) low = mid + 1; else high = mid - 1;
        }
        return -1;
    }

    private int ProjectIndexOf(string id)
    {
        foreach (EconomyProject p in _projects) if (p.Id == id) return p.Index;
        return -1;
    }

    private int[] ProjectIndices(IReadOnlyList<string> ids)
    {
        var list = new List<int>();
        foreach (string id in ids)
        {
            int index = ProjectIndexOf(id);
            if (index >= 0) list.Add(index);
        }
        return list.ToArray();
    }

    private static List<KeyValuePair<string, ContentValue>> SortedEntries(ContentValue? map)
    {
        var entries = new List<KeyValuePair<string, ContentValue>>();
        if (map is null || map.Kind != ValueKind.Map) return entries;
        entries.AddRange(map.Entries);
        entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return entries;
    }

    private static Fix64 Number(ContentValue value)
        => value.Kind == ValueKind.Int ? Fix64.FromInt(checked((int)value.IntValue)) : value.Kind == ValueKind.Fix ? value.FixValue : Fix64.Zero;

    private void LoadBronze(ContentPack pack)
    {
        ContentValue? registries = pack.Root.Get("registries");

        var recipes = new List<EconomyRecipe>();
        foreach (ContentValue r in List(registries, "recipes"))
        {
            var inputs = new List<EconomyRecipeInput>();
            foreach (KeyValuePair<string, ContentValue> e in SortedEntries(r.Get("inputs")))
                inputs.Add(new EconomyRecipeInput(e.Key.Split('|'), Number(e.Value)));
            var outputs = new List<EconomyRecipeOutput>();
            foreach (KeyValuePair<string, ContentValue> e in SortedEntries(r.Get("outputs")))
                outputs.Add(new EconomyRecipeOutput(e.Key, Number(e.Value)));
            var structures = new List<string>(r.GetStringList("structure"));
            recipes.Add(new EconomyRecipe(RequiredString(r, "id"), RequiredInt(r, "order"), ProjectIndices(r.GetStringList("unlocked_by")),
                structures.ToArray(), inputs.ToArray(), outputs.ToArray(), r.GetFix("labor") ?? Fix64.Zero,
                r.GetString("practice") ?? "craft", r.GetBool("bellows") ?? false));
        }
        recipes.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
        _recipes = recipes.ToArray();

        var tiers = new List<EconomyToolTier>();
        foreach (ContentValue t in List(registries, "tools"))
        {
            string id = RequiredString(t, "id");
            if (t.GetFix("service_ws") is not { } service || t.GetString("metal") is not { } metal) continue;
            if (id.Length < 2 || id[0] != 't' || !int.TryParse(id.AsSpan(1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int tier)) continue;
            var keys = new List<string>();
            var values = new List<Fix64>();
            foreach (KeyValuePair<string, ContentValue> m in SortedEntries(t.Get("mods")))
            {
                keys.Add(m.Key);
                values.Add(Number(m.Value));
            }
            tiers.Add(new EconomyToolTier(id, tier, ProjectIndices(t.GetStringList("unlocked_by")), metal,
                t.GetFix("metal_per_kit") ?? Fix64.Zero, service, keys.ToArray(), values.ToArray()));
        }
        tiers.Sort((a, b) => a.Tier.CompareTo(b.Tier));
        _toolTiers = tiers.ToArray();

        var trade = new List<EconomyTradeGood>();
        foreach (ContentValue g in List(registries, "goods"))
        {
            string id = RequiredString(g, "id");
            Fix64 barter = g.Get("barter") is { IsNumber: true } b ? Number(b) : Fix64.Zero;
            trade.Add(new EconomyTradeGood(id, g.GetString("category") ?? "", barter,
                ServiceSeasons(g.GetString("durability")), OptionalInt(g, "introduced_era") ?? 0));
        }
        trade.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        _tradeGoods = trade.ToArray();

        var constants = new List<(string Id, Fix64 Value)>();
        foreach (ContentValue c in List(pack.Root.Get("balance"), "constants"))
        {
            string? id = c.GetString("id");
            if (id is null || c.Get("value") is not { IsNumber: true } v) continue;
            constants.Add((id, Number(v)));
        }
        constants.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        _constantIds = new string[constants.Count];
        _constantValues = new Fix64[constants.Count];
        for (int i = 0; i < constants.Count; i++) { _constantIds[i] = constants[i].Id; _constantValues[i] = constants[i].Value; }

        BronzeFingerprint = ComputeBronzeFingerprint();
    }

    /// <summary>"24 seasons" → 24; anything else (durable, ws-based, breakage) → 0.</summary>
    private static int ServiceSeasons(string? durability)
    {
        if (durability is null) return 0;
        const string suffix = " seasons";
        if (!durability.EndsWith(suffix, StringComparison.Ordinal)) return 0;
        return int.TryParse(durability.AsSpan(0, durability.Length - suffix.Length), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int seasons) ? seasons : 0;
    }

    private ulong ComputeBronzeFingerprint()
    {
        var hash = new StateHasher();
        int bronzeRecipes = 0;
        foreach (EconomyRecipe recipe in _recipes) if (IsBronzeDefinition(recipe.UnlockedBy)) bronzeRecipes++;
        hash.Add(bronzeRecipes);
        foreach (EconomyRecipe r in _recipes)
        {
            if (!IsBronzeDefinition(r.UnlockedBy)) continue;
            hash.Add(Hash64.OfString(r.Id)); hash.Add(r.Order); hash.Add(r.Labor); hash.Add(Hash64.OfString(r.Practice)); hash.Add(r.Bellows);
            hash.Add(r.UnlockedBy.Length); foreach (int u in r.UnlockedBy) hash.Add(u);
            hash.Add(r.Structures.Length); foreach (string s in r.Structures) hash.Add(Hash64.OfString(s));
            hash.Add(r.Inputs.Length);
            foreach (EconomyRecipeInput input in r.Inputs)
            {
                hash.Add(input.Goods.Length); foreach (string g in input.Goods) hash.Add(Hash64.OfString(g));
                hash.Add(input.Quantity);
            }
            hash.Add(r.Outputs.Length);
            foreach (EconomyRecipeOutput output in r.Outputs) { hash.Add(Hash64.OfString(output.Good)); hash.Add(output.Quantity); }
        }
        int bronzeToolTiers = 0;
        foreach (EconomyToolTier tier in _toolTiers) if (IsBronzeDefinition(tier.UnlockedBy)) bronzeToolTiers++;
        hash.Add(bronzeToolTiers);
        foreach (EconomyToolTier t in _toolTiers)
        {
            if (!IsBronzeDefinition(t.UnlockedBy)) continue;
            hash.Add(Hash64.OfString(t.Id)); hash.Add(t.Tier); hash.Add(Hash64.OfString(t.Metal)); hash.Add(t.MetalPerKit); hash.Add(t.ServiceWs);
            hash.Add(t.UnlockedBy.Length); foreach (int u in t.UnlockedBy) hash.Add(u);
            hash.Add(t.ModCount);
            for (int i = 0; i < t.ModCount; i++) { hash.Add(Hash64.OfString(t.ModKey(i))); hash.Add(t.ModValue(i)); }
        }
        int bronzeTradeGoods = 0;
        foreach (EconomyTradeGood good in _tradeGoods) if (good.IntroducedEra <= 3) bronzeTradeGoods++;
        hash.Add(bronzeTradeGoods);
        foreach (EconomyTradeGood g in _tradeGoods)
        {
            if (g.IntroducedEra > 3) continue;
            hash.Add(Hash64.OfString(g.Id)); hash.Add(Hash64.OfString(g.Category)); hash.Add(g.Barter); hash.Add(g.ServiceSeasons);
        }
        // Preserve the legacy effect sequence used by Bronze snapshots. Before WP3 every E4
        // project compiled to one prose effect; E5+ effects were already present in the old
        // fingerprint and remain included here. Iron-mode activation will use a separate full
        // content fingerprint.
        int legacyEffects = 0;
        foreach (EconomyProject project in _projects)
            legacyEffects += project.Era == 4 ? 1 : project.EffectCount;
        hash.Add(legacyEffects);
        foreach (EconomyProject project in _projects)
        {
            if (project.Era == 4)
            {
                // Match the EconomyEffect values produced by the legacy prose-only row.
                hash.Add(-1); hash.Add(Fix64.FromInt(-1)); hash.Add(-1);
                hash.Add(Hash64.OfString("")); hash.Add(-1); hash.Add(Fix64.FromInt(-1));
                hash.Add(Hash64.OfString("")); hash.Add(-1);
                continue;
            }
            for (int i = project.EffectStart; i < project.EffectStart + project.EffectCount; i++)
            {
                EconomyEffect e = _effects[i];
                hash.Add(e.Span ?? -1); hash.Add(e.Overreach ?? Fix64.FromInt(-1)); hash.Add(e.Radius ?? -1);
                hash.Add(Hash64.OfString(e.Tier ?? "")); hash.Add(e.MinPop ?? -1); hash.Add(e.ClaimMult ?? Fix64.FromInt(-1));
                hash.Add(Hash64.OfString(e.RoleId ?? ""));
                hash.Add(e.Needs?.Length ?? -1);
                if (e.Needs is not null) foreach (string n in e.Needs) hash.Add(Hash64.OfString(n));
            }
        }
        hash.Add(_constantIds.Length);
        for (int i = 0; i < _constantIds.Length; i++) { hash.Add(Hash64.OfString(_constantIds[i])); hash.Add(_constantValues[i]); }
        return hash.Finish();
    }

    private bool IsBronzeDefinition(int[] unlockedBy)
    {
        foreach (int index in unlockedBy)
            if (IsBronzeProject(index)) return true;
        return false;
    }

    private bool IsBronzeProject(int projectIndex)
        => (uint)projectIndex < (uint)_projects.Length && _projects[projectIndex].Era <= 3;
}
