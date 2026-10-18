using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace CivSim.Content;

/// <summary>
/// Reads YAML into a <see cref="ContentValue"/> tree under the strict subset of docs/MVP/16 §5: one document,
/// no anchors, aliases or merge keys, no duplicate keys, and no implicit type guessing beyond plain
/// integers, plain decimals, <c>true</c>/<c>false</c> and <c>null</c>. Quoted scalars are always text.
/// </summary>
public static class YamlLoader
{
    private static readonly Regex IntPattern = new(@"^[-+]?\d+$", RegexOptions.CultureInvariant);
    private static readonly Regex DecimalPattern = new(@"^[-+]?(\d+\.\d+|\.\d+)$", RegexOptions.CultureInvariant);

    public static ContentValue? Parse(string file, string text, Report report)
    {
        int before = report.ErrorCount;
        try
        {
            // The event parser (not the representation model) is used so duplicate keys, anchors and aliases are
            // still visible: the model silently keeps the last of two equal keys.
            var parser = new Parser(new StringReader(text));
            parser.Consume<StreamStart>();
            if (!parser.TryConsume<DocumentStart>(out _))
            {
                report.Error("V1", file, "expected exactly one YAML document, found none");
                return null;
            }
            ContentValue root = ReadNode(parser, file, report);
            parser.Consume<DocumentEnd>();
            if (parser.Accept<DocumentStart>(out _))
            {
                report.Error("V1", file, "expected exactly one YAML document, found more");
                return null;
            }
            return report.ErrorCount == before ? root : null;
        }
        catch (YamlException e)
        {
            report.Error("V1", $"{file}:{(int)e.Start.Line}", "YAML syntax: " + e.Message);
            return null;
        }
    }

    private static ContentValue ReadNode(IParser parser, string file, Report report)
    {
        ParsingEvent ev = parser.Current ?? throw new YamlException("unexpected end of YAML");
        int line = (int)ev.Start.Line;
        switch (ev)
        {
            case AnchorAlias:
                parser.MoveNext();
                report.Error("V1", $"{file}:{line}", "anchors and aliases are not allowed");
                return ContentValue.Null;
            case Scalar scalar:
                parser.MoveNext();
                if (!scalar.Anchor.IsEmpty) report.Error("V1", $"{file}:{line}", "anchors and aliases are not allowed");
                return ConvertScalar(scalar, file, line, report).WithLine(line);
            case SequenceStart seq:
                parser.MoveNext();
                if (!seq.Anchor.IsEmpty) report.Error("V1", $"{file}:{line}", "anchors and aliases are not allowed");
                var items = new List<ContentValue>();
                while (!parser.TryConsume<SequenceEnd>(out _)) items.Add(ReadNode(parser, file, report));
                return ContentValue.ListOf(items).WithLine(line);
            case MappingStart map:
                parser.MoveNext();
                if (!map.Anchor.IsEmpty) report.Error("V1", $"{file}:{line}", "anchors and aliases are not allowed");
                var entries = new List<KeyValuePair<string, ContentValue>>();
                var seen = new HashSet<string>(System.StringComparer.Ordinal);
                while (!parser.TryConsume<MappingEnd>(out _))
                {
                    if (parser.Current is not Scalar keyEvent)
                    {
                        report.Error("V1", $"{file}:{(int)(parser.Current?.Start.Line ?? line)}", "map keys must be plain text");
                        ReadNode(parser, file, report); // skip the key
                        ReadNode(parser, file, report); // and its value
                        continue;
                    }
                    parser.MoveNext();
                    string key = keyEvent.Value;
                    int keyLine = (int)keyEvent.Start.Line;
                    if (key == "<<") report.Error("V1", $"{file}:{keyLine}", "merge keys (<<) are not allowed");
                    if (!seen.Add(key)) report.Error("V1", $"{file}:{keyLine}", $"duplicate key '{key}'");
                    entries.Add(new(key, ReadNode(parser, file, report)));
                }
                return ContentValue.MapOf(entries).WithLine(line);
            default:
                parser.MoveNext();
                report.Error("V1", $"{file}:{line}", "unsupported YAML construct");
                return ContentValue.Null;
        }
    }

    private static ContentValue ConvertScalar(Scalar s, string file, int line, Report report)
    {
        string v = s.Value;
        if (s.Style != ScalarStyle.Plain) return ContentValue.Of(v);
        if (v.Length == 0 || v == "null" || v == "~") return ContentValue.Null;
        if (v == "true") return ContentValue.Of(true);
        if (v == "false") return ContentValue.Of(false);
        if (IntPattern.IsMatch(v))
        {
            if (long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i)) return ContentValue.Of(i);
            report.Error("V1", $"{file}:{line}", $"integer out of range: {v}");
            return ContentValue.Null;
        }
        if (DecimalPattern.IsMatch(v))
        {
            try { return ContentValue.Of(Fix64.Parse(v)); }
            catch (System.Exception e) when (e is System.OverflowException or System.FormatException)
            {
                report.Error("V1", $"{file}:{line}", $"decimal out of range or too precise: {v}");
                return ContentValue.Null;
            }
        }
        return ContentValue.Of(v);
    }
}
