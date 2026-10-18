using System;
using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Content;

public enum ValueKind : byte { Null, Bool, Int, Fix, Text, List, Map }

/// <summary>
/// A loosely typed, immutable value tree: the in-memory form of content (YAML on disk, canonical JSON in a
/// content pack). Numbers are exact: integers are <see cref="long"/> and decimals are <see cref="Fix64"/>.
/// Maps keep their entries in a list (never a hash table), so iteration order is always defined.
/// </summary>
public sealed class ContentValue
{
    public static readonly ContentValue Null = new(ValueKind.Null);

    public ValueKind Kind { get; }
    public bool BoolValue { get; }
    public long IntValue { get; }
    public Fix64 FixValue { get; }
    public string? TextValue { get; }
    public IReadOnlyList<ContentValue> Items { get; }
    public IReadOnlyList<KeyValuePair<string, ContentValue>> Entries { get; }
    /// <summary>1-based source line for error messages; 0 when unknown. Not part of the canonical form.</summary>
    public int Line { get; init; }

    private static readonly ContentValue[] NoItems = Array.Empty<ContentValue>();
    private static readonly KeyValuePair<string, ContentValue>[] NoEntries = Array.Empty<KeyValuePair<string, ContentValue>>();

    private ContentValue(ValueKind kind, bool b = false, long i = 0, Fix64 f = default, string? t = null,
        IReadOnlyList<ContentValue>? items = null, IReadOnlyList<KeyValuePair<string, ContentValue>>? entries = null)
    {
        Kind = kind;
        BoolValue = b;
        IntValue = i;
        FixValue = f;
        TextValue = t;
        Items = items ?? NoItems;
        Entries = entries ?? NoEntries;
    }

    public static ContentValue Of(bool value) => new(ValueKind.Bool, b: value);
    public static ContentValue Of(long value) => new(ValueKind.Int, i: value);
    public static ContentValue Of(Fix64 value) => new(ValueKind.Fix, f: value);
    public static ContentValue Of(string value) => new(ValueKind.Text, t: value);
    public static ContentValue ListOf(IEnumerable<ContentValue> items) => new(ValueKind.List, items: new List<ContentValue>(items));
    public static ContentValue MapOf(IEnumerable<KeyValuePair<string, ContentValue>> entries)
        => new(ValueKind.Map, entries: new List<KeyValuePair<string, ContentValue>>(entries));

    public ContentValue WithLine(int line)
    {
        // A value is immutable, so attach the line by rebuilding with the same payload.
        return new ContentValue(Kind, BoolValue, IntValue, FixValue, TextValue,
            Kind == ValueKind.List ? Items : null, Kind == ValueKind.Map ? Entries : null) { Line = line };
    }

    public bool IsNull => Kind == ValueKind.Null;
    public bool IsNumber => Kind is ValueKind.Int or ValueKind.Fix;

    /// <summary>The value of a map entry, or null when this is not a map or the key is absent.</summary>
    public ContentValue? Get(string key)
    {
        if (Kind != ValueKind.Map) return null;
        for (int i = 0; i < Entries.Count; i++)
            if (string.Equals(Entries[i].Key, key, StringComparison.Ordinal)) return Entries[i].Value;
        return null;
    }

    public bool Has(string key)
    {
        ContentValue? v = Get(key);
        return v is not null && !v.IsNull;
    }

    public string? GetString(string key) => Get(key) is { Kind: ValueKind.Text } v ? v.TextValue : null;
    public long? GetInt(string key) => Get(key) is { Kind: ValueKind.Int } v ? v.IntValue : null;
    public bool? GetBool(string key) => Get(key) is { Kind: ValueKind.Bool } v ? v.BoolValue : null;

    /// <summary>A number as a Fix64; integers are promoted exactly.</summary>
    public Fix64? GetFix(string key)
    {
        ContentValue? v = Get(key);
        if (v is null) return null;
        return v.Kind switch
        {
            ValueKind.Fix => v.FixValue,
            ValueKind.Int => Fix64.FromInt(v.IntValue),
            _ => null,
        };
    }

    public IReadOnlyList<string> GetStringList(string key)
    {
        var result = new List<string>();
        ContentValue? v = Get(key);
        if (v is { Kind: ValueKind.List })
            foreach (ContentValue item in v.Items)
                if (item.Kind == ValueKind.Text && item.TextValue is not null) result.Add(item.TextValue);
        return result;
    }

    public override string ToString() => Kind switch
    {
        ValueKind.Null => "null",
        ValueKind.Bool => BoolValue ? "true" : "false",
        ValueKind.Int => IntValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ValueKind.Fix => FixValue.ToString(),
        ValueKind.Text => TextValue ?? "",
        ValueKind.List => "[" + Items.Count + " items]",
        _ => "{" + Entries.Count + " entries}",
    };
}
