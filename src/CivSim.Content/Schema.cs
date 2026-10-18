using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Numerics;

namespace CivSim.Content;

/// <summary>Where a value sits, for error messages.</summary>
public readonly record struct Ctx(string File, Report Report)
{
    public void Error(ContentValue v, string path, string message)
        => Report.Error("V1", $"{File}:{v.Line}", $"{path}: {message}");
}

/// <summary>A small schema language for content records (check V1). Unknown fields are errors unless a type opts in.</summary>
public abstract class FieldType
{
    public abstract void Check(ContentValue v, string path, Ctx ctx);
}

public sealed class StrType : FieldType
{
    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Text || string.IsNullOrWhiteSpace(v.TextValue)) ctx.Error(v, path, $"expected non-empty text, got {Describe(v)}");
    }

    internal static string Describe(ContentValue v) => v.Kind switch
    {
        ValueKind.Null => "nothing",
        ValueKind.Text => $"text '{v.TextValue}'",
        _ => v.Kind.ToString().ToLowerInvariant() + " " + v,
    };
}

public sealed class IntType : FieldType
{
    private readonly long _min, _max;
    public IntType(long min = long.MinValue, long max = long.MaxValue) { _min = min; _max = max; }

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Int) { ctx.Error(v, path, $"expected an integer, got {StrType.Describe(v)}"); return; }
        if (v.IntValue < _min || v.IntValue > _max) ctx.Error(v, path, $"{v.IntValue} is outside {_min}..{_max}");
    }
}

public sealed class NumType : FieldType
{
    private readonly Fix64? _min, _max;
    private readonly bool _minExclusive;

    public NumType(string? min = null, string? max = null, bool minExclusive = false)
    {
        _min = min is null ? null : Fix64.Parse(min);
        _max = max is null ? null : Fix64.Parse(max);
        _minExclusive = minExclusive;
    }

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (!v.IsNumber) { ctx.Error(v, path, $"expected a number, got {StrType.Describe(v)}"); return; }
        Fix64 x = v.Kind == ValueKind.Int ? Fix64.FromInt(v.IntValue) : v.FixValue;
        if (_min is { } lo && (_minExclusive ? x <= lo : x < lo)) ctx.Error(v, path, $"{x} is below the allowed minimum {lo}");
        if (_max is { } hi && x > hi) ctx.Error(v, path, $"{x} is above the allowed maximum {hi}");
    }
}

public sealed class BoolType : FieldType
{
    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Bool) ctx.Error(v, path, $"expected true or false, got {StrType.Describe(v)}");
    }
}

public sealed class EnumType : FieldType
{
    private readonly string[] _values;
    public EnumType(params string[] values) => _values = values;

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Text || Array.IndexOf(_values, v.TextValue) < 0)
            ctx.Error(v, path, $"expected one of [{string.Join(", ", _values)}], got {StrType.Describe(v)}");
    }
}

public sealed class ListType : FieldType
{
    private readonly FieldType _item;
    public ListType(FieldType item) => _item = item;

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.List) { ctx.Error(v, path, $"expected a list, got {StrType.Describe(v)}"); return; }
        for (int i = 0; i < v.Items.Count; i++) _item.Check(v.Items[i], $"{path}[{i}]", ctx);
    }
}

/// <summary>A map from any key to values of one type, for example lineage code to percent.</summary>
public sealed class MapType : FieldType
{
    private readonly FieldType _value;
    public MapType(FieldType value) => _value = value;

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Map) { ctx.Error(v, path, $"expected a map, got {StrType.Describe(v)}"); return; }
        foreach (KeyValuePair<string, ContentValue> e in v.Entries) _value.Check(e.Value, $"{path}.{e.Key}", ctx);
    }
}

public sealed class AnyType : FieldType
{
    public override void Check(ContentValue v, string path, Ctx ctx) { }
}

public sealed record Field(string Name, FieldType Type, bool Required);

public sealed class ObjType : FieldType
{
    private readonly List<Field> _fields = new();
    private readonly bool _allowExtra;

    public ObjType(bool allowExtra = false) => _allowExtra = allowExtra;

    public ObjType Req(string name, FieldType type) { _fields.Add(new Field(name, type, true)); return this; }
    public ObjType Opt(string name, FieldType type) { _fields.Add(new Field(name, type, false)); return this; }

    public override void Check(ContentValue v, string path, Ctx ctx)
    {
        if (v.Kind != ValueKind.Map) { ctx.Error(v, path, $"expected a record, got {StrType.Describe(v)}"); return; }
        foreach (Field f in _fields)
        {
            ContentValue? value = v.Get(f.Name);
            if (value is null || value.IsNull)
            {
                if (f.Required) ctx.Error(v, path, $"missing required field '{f.Name}'");
                continue;
            }
            f.Type.Check(value, path.Length == 0 ? f.Name : $"{path}.{f.Name}", ctx);
        }
        if (_allowExtra) return;
        foreach (KeyValuePair<string, ContentValue> e in v.Entries)
        {
            bool known = false;
            foreach (Field f in _fields) if (f.Name == e.Key) { known = true; break; }
            if (!known) ctx.Error(e.Value, path, $"unknown field '{e.Key}'");
        }
    }
}

/// <summary>Builders for the schema language.</summary>
public static class S
{
    public static FieldType Str() => new StrType();
    public static FieldType Int(long min = long.MinValue, long max = long.MaxValue) => new IntType(min, max);
    public static FieldType Num(string? min = null, string? max = null, bool minExclusive = false) => new NumType(min, max, minExclusive);
    public static FieldType Bool() => new BoolType();
    public static FieldType Enum(params string[] values) => new EnumType(values);
    public static FieldType List(FieldType item) => new ListType(item);
    public static FieldType Map(FieldType value) => new MapType(value);
    public static FieldType Any() => new AnyType();
    public static ObjType Obj(bool allowExtra = false) => new(allowExtra);
}
