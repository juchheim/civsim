using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Content;

/// <summary>
/// A compiled content pack: the validated catalog and registries as one canonical value tree, plus the
/// SHA-256 of its canonical bytes (docs/MVP/16 §5). The simulation and the game load packs; they never read
/// YAML. Canonical means: UTF-8, no whitespace, map keys in ordinal order, decimals as exact Q32.32 raw
/// integers, so the same content always produces the same bytes and hash on every platform.
/// </summary>
public sealed class ContentPack
{
    public const string FormatName = "civsim-content-pack";
    public const int FormatVersion = 1;

    public ContentValue Root { get; }
    public byte[] Bytes { get; }
    public byte[] Hash { get; }

    private ContentPack(ContentValue root, byte[] bytes)
    {
        Root = root;
        Bytes = bytes;
        Hash = SHA256.HashData(bytes);
    }

    public string HashHex => Convert.ToHexString(Hash).ToLowerInvariant();

    /// <summary>Builds a pack from a value tree (what the content compiler produces).</summary>
    public static ContentPack FromTree(ContentValue root) => new(root, CanonicalJson.Write(root));

    /// <summary>Loads a pack from its canonical bytes.</summary>
    public static ContentPack Load(byte[] bytes)
    {
        ContentValue root = CanonicalJson.Read(bytes);
        if (root.GetString("format") != FormatName) throw new InvalidDataException("Not a CivSim content pack.");
        if (root.GetInt("version") != FormatVersion) throw new InvalidDataException("Unsupported content pack version.");
        return new ContentPack(root, bytes);
    }

    public IReadOnlyList<ContentValue> List(string name)
        => Root.Get(name) is { Kind: ValueKind.List } v ? v.Items : Array.Empty<ContentValue>();
}

/// <summary>Writes and reads the canonical JSON form of a <see cref="ContentValue"/> tree.</summary>
public static class CanonicalJson
{
    private const string FixKey = "$fix";

    public static byte[] Write(ContentValue root)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteValue(writer, root);
        return stream.ToArray();
    }

    private static void WriteValue(Utf8JsonWriter w, ContentValue v)
    {
        switch (v.Kind)
        {
            case ValueKind.Null: w.WriteNullValue(); break;
            case ValueKind.Bool: w.WriteBooleanValue(v.BoolValue); break;
            case ValueKind.Int: w.WriteNumberValue(v.IntValue); break;
            case ValueKind.Fix:
                w.WriteStartObject();
                w.WriteNumber(FixKey, v.FixValue.Raw);
                w.WriteEndObject();
                break;
            case ValueKind.Text: w.WriteStringValue(v.TextValue); break;
            case ValueKind.List:
                w.WriteStartArray();
                foreach (ContentValue item in v.Items) WriteValue(w, item);
                w.WriteEndArray();
                break;
            default:
                var entries = new List<KeyValuePair<string, ContentValue>>(v.Entries);
                entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                w.WriteStartObject();
                foreach (KeyValuePair<string, ContentValue> e in entries)
                {
                    w.WritePropertyName(e.Key);
                    WriteValue(w, e.Value);
                }
                w.WriteEndObject();
                break;
        }
    }

    public static ContentValue Read(ReadOnlySpan<byte> utf8)
    {
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow });
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        return Convert(doc.RootElement);
    }

    private static ContentValue Convert(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Null: return ContentValue.Null;
            case JsonValueKind.True: return ContentValue.Of(true);
            case JsonValueKind.False: return ContentValue.Of(false);
            case JsonValueKind.String: return ContentValue.Of(e.GetString() ?? "");
            case JsonValueKind.Number:
                if (!e.TryGetInt64(out long i)) throw new InvalidDataException("Content packs hold integers only.");
                return ContentValue.Of(i);
            case JsonValueKind.Array:
                var items = new List<ContentValue>();
                foreach (JsonElement item in e.EnumerateArray()) items.Add(Convert(item));
                return ContentValue.ListOf(items);
            case JsonValueKind.Object:
                var entries = new List<KeyValuePair<string, ContentValue>>();
                foreach (JsonProperty p in e.EnumerateObject()) entries.Add(new(p.Name, Convert(p.Value)));
                if (entries.Count == 1 && entries[0].Key == FixKey && entries[0].Value.Kind == ValueKind.Int)
                    return ContentValue.Of(Fix64.FromRaw(entries[0].Value.IntValue));
                return ContentValue.MapOf(entries);
            default:
                throw new InvalidDataException("Unsupported JSON value.");
        }
    }
}
