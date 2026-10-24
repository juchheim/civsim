using System;
using System.Collections.Generic;
using CivSim.Sim.Content;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// Turns sightings into the projects they prove (docs/MVP/14 §5.2). Every sighting maps to the projects whose catalog
/// effects produce it (a <c>build</c> of the structure, a <c>role</c> of the outfit, the weapon ladder of 13 §7.1), and a
/// project proves its prerequisites. When several projects could explain a sighting, only what all of them share is
/// proved. Built once from the catalog; holds no match state.
/// </summary>
public sealed class TechEstimate
{
    private readonly ResearchCatalog _catalog;
    private readonly int _count, _words;
    /// <summary>Per project, the project and everything it needs (one-of prerequisites: what all alternatives share).</summary>
    private readonly ulong[] _closure;
    private readonly ulong[]?[] _structure;
    private readonly bool[] _advancedStructure;
    private readonly ulong[]?[] _era;
    private readonly ulong[]? _warrior, _watchers, _palisade;
    private readonly (Fix64 Strength, bool Ranged, ulong[] Proves)[] _weapons;
    private readonly ulong[]? _archeryBow;
    private readonly Fix64 _bowStrength;
    private readonly int _met03, _met04, _met05, _def04, _def05, _com05, _bel06;

    public TechEstimate(ResearchCatalog catalog, IReadOnlyList<WeaponRung> weaponLadder, int archeryProject)
    {
        _catalog = catalog;
        _count = catalog.Count;
        _met03 = catalog.IndexOf("MET-03"); _met04 = catalog.IndexOf("MET-04"); _met05 = catalog.IndexOf("MET-05");
        _def04 = catalog.IndexOf("DEF-04"); _def05 = catalog.IndexOf("DEF-05");
        _com05 = catalog.IndexOf("COM-05"); _bel06 = catalog.IndexOf("BEL-06");
        _words = (_count + 63) / 64;
        _closure = new ulong[_count * _words];
        var done = new bool[_count];
        for (int p = 0; p < _count; p++) Compute(p, done);

        // What the catalog says each project puts on the map: structures built and roles worn.
        var builds = new List<(string Name, List<int> Projects)>();
        var roles = new List<(string Name, List<int> Projects)>();
        IReadOnlyList<ContentValue> raw = catalog.Pack.List("projects");
        for (int i = 0; i < raw.Count && i < _count; i++)
        {
            if (!catalog.Projects[i].Numeric || raw[i].Get("effects") is not { Kind: ValueKind.List } effects) continue;
            foreach (ContentValue e in effects.Items)
            {
                string? kind = e.GetString("kind");
                if (kind == "build" && e.GetString("structure") is { } s) Add(builds, s, i);
                else if (kind == "role" && e.GetString("id") is { } r) Add(roles, r, i);
            }
        }

        _structure = new ulong[]?[LivingEconomyModel.StructureKindCount];
        _advancedStructure = new bool[_structure.Length];
        for (int k = 0; k < _structure.Length; k++)
        {
            List<int>? candidates = Find(builds, LivingEconomyModel.StructureName(k));
            _structure[k] = Shared(candidates);
            if (candidates is not null)
                foreach (int p in candidates) if (catalog.Projects[p].Era >= 1) _advancedStructure[k] = true;
        }
        _warrior = Shared(Find(roles, "warrior"));
        var watcherCandidates = new List<int>();
        if (Find(roles, "watcher") is { } w) watcherCandidates.AddRange(w);
        if (Find(builds, "lookout_post") is { } l) watcherCandidates.AddRange(l);
        _watchers = Shared(watcherCandidates.Count > 0 ? watcherCandidates : null);
        _palisade = Shared(Find(builds, "palisade"));

        _weapons = new (Fix64, bool, ulong[])[weaponLadder.Count];
        _bowStrength = Fix64.Zero;
        for (int i = 0; i < weaponLadder.Count; i++)
        {
            WeaponRung rung = weaponLadder[i];
            _weapons[i] = (rung.Strength, rung.Ranged, Row(rung.Project).ToArray());
            if (rung.Ranged && rung.Strength > _bowStrength) _bowStrength = rung.Strength;
        }
        if (weaponLadder.Count > 0 && archeryProject >= 0)
        {
            _archeryBow = Row(weaponLadder[0].Project).ToArray();
            Or(_archeryBow, Row(archeryProject));
        }

        // A public era E proves every keystone that left an earlier era, with their closures.
        _era = new ulong[]?[ResearchCatalog.EraCount + 1];
        var acc = new ulong[_words];
        for (int e = 0; e <= ResearchCatalog.EraCount; e++)
        {
            _era[e] = (ulong[])acc.Clone();
            int key = e < ResearchCatalog.EraCount ? catalog.KeystoneLeaving(e) : -1;
            if (key >= 0) Or(acc, Row(key));
        }
    }

    public int ProjectCount => _count;

    private static void Add(List<(string Name, List<int> Projects)> list, string name, int project)
    {
        foreach ((string n, List<int> ps) in list)
            if (string.Equals(n, name, StringComparison.Ordinal)) { ps.Add(project); return; }
        list.Add((name, new List<int> { project }));
    }

    private static List<int>? Find(List<(string Name, List<int> Projects)> list, string name)
    {
        foreach ((string n, List<int> ps) in list)
            if (string.Equals(n, name, StringComparison.Ordinal)) return ps;
        return null;
    }

    private Span<ulong> Row(int project) => _closure.AsSpan(project * _words, _words);

    private static void Or(Span<ulong> into, ReadOnlySpan<ulong> from)
    {
        for (int i = 0; i < into.Length; i++) into[i] |= from[i];
    }

    private void Compute(int p, bool[] done)
    {
        if (done[p]) return;
        done[p] = true; // the requires graph is acyclic (the content validator checks it)
        ProjectDef def = _catalog.Projects[p];
        foreach (int r in def.Requires) Compute(r, done);
        foreach (int a in def.RequiresAny) Compute(a, done);
        Span<ulong> me = Row(p);
        me[p >> 6] |= 1UL << (p & 63);
        foreach (int r in def.Requires) Or(me, Row(r));
        if (def.RequiresAny.Length > 0)
        {
            Span<ulong> shared = stackalloc ulong[_words];
            Row(def.RequiresAny[0]).CopyTo(shared);
            for (int i = 1; i < def.RequiresAny.Length; i++)
            {
                ReadOnlySpan<ulong> alt = Row(def.RequiresAny[i]);
                for (int w = 0; w < _words; w++) shared[w] &= alt[w];
            }
            Or(me, shared);
        }
    }

    /// <summary>What every candidate's closure shares: all that a sighting explained by any one of them proves.</summary>
    private ulong[]? Shared(List<int>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return null;
        ulong[] set = Row(candidates[0]).ToArray();
        for (int i = 1; i < candidates.Count; i++)
        {
            ReadOnlySpan<ulong> other = Row(candidates[i]);
            for (int w = 0; w < _words; w++) set[w] &= other[w];
        }
        return set;
    }

    private static void OrInto(ReadOnlySpan<ulong> set, bool[] believed)
    {
        for (int p = 0; p < believed.Length; p++)
            if ((set[p >> 6] & (1UL << (p & 63))) != 0) believed[p] = true;
    }

    // ---- what a sighting proves --------------------------------------------------------------

    /// <summary>Marks a project and everything it needs.</summary>
    public void AddProject(int project, bool[] believed) => OrInto(Row(project), believed);

    /// <summary>Marks everything an announced era proves: the keystones that left the earlier eras, with their closures.</summary>
    public void AddEra(int era, bool[] believed)
    {
        if (_era[Math.Clamp(era, 0, ResearchCatalog.EraCount)] is { } set) OrInto(set, believed);
    }

    /// <summary>Marks what a structure of economy kind <paramref name="kind"/> standing at a settlement proves.</summary>
    public void AddStructure(int kind, bool[] believed)
    {
        if ((kind >= 0 && kind < _structure.Length) && _structure[kind] is { } set) OrInto(set, believed);
    }

    /// <summary>True when the structure kind comes from a project after the Dawn: craft, not the first shelters.</summary>
    public bool IsAdvancedStructure(int kind) => (kind >= 0 && kind < _advancedStructure.Length) && _advancedStructure[kind];

    public void AddWarriors(bool[] believed) { if (_warrior is { } s) OrInto(s, believed); }
    public void AddWatchers(bool[] believed) { if (_watchers is { } s) OrInto(s, believed); }
    public void AddPalisade(bool[] believed) { if (_palisade is { } s) OrInto(s, believed); }

    /// <summary>A visible E3 defense proves its building project.</summary>
    public void AddBronzeDefense(bool hillfort, bool stoneWalls, bool[] believed)
    {
        if (hillfort && _def04 >= 0) AddProject(_def04, believed);
        if (stoneWalls && _def05 >= 0) AddProject(_def05, believed);
    }

    /// <summary>A visible metal-tool tint proves only the tool tier's unlocking project.</summary>
    public void AddMetalKits(bool copper, bool arsenical, bool tinBronze, bool[] believed)
    {
        if (copper && _met03 >= 0) AddProject(_met03, believed);
        if (arsenical && _met04 >= 0) AddProject(_met04, believed);
        if (tinBronze && _met05 >= 0) AddProject(_met05, believed);
    }

    /// <summary>A visible mine camp on an ore deposit proves the corresponding accessible mining route.</summary>
    public void AddMiningCamp(DepositKind deposit, bool[] believed)
    {
        int project = deposit switch
        {
            DepositKind.CopperOre => _met03,
            DepositKind.ArsenicalCopper => _met04,
            DepositKind.Tin => _met05,
            _ => -1,
        };
        if (project >= 0) AddProject(project, believed);
    }

    /// <summary>Visible E3 specialists prove the project that unlocks their role.</summary>
    public void AddCivicRoles(bool scribes, bool priests, bool[] believed)
    {
        if (scribes && _com05 >= 0) AddProject(_com05, believed);
        if (priests && _bel06 >= 0) AddProject(_bel06, believed);
    }

    /// <summary>Marks what a weapon of this strength in figures' hands proves; bows above their own strength prove archery warfare too.</summary>
    public void AddWeapon(Fix64 strength, bool ranged, bool[] believed)
    {
        if (ranged && _archeryBow is not null && strength > _bowStrength) { OrInto(_archeryBow, believed); return; }
        foreach ((Fix64 s, bool r, ulong[] proves) in _weapons)
            if (s == strength && r == ranged) { OrInto(proves, believed); return; }
    }

    /// <summary>The closure of one project as a fresh array (tests and tools).</summary>
    public bool[] ClosureOf(int project)
    {
        var result = new bool[_count];
        AddProject(project, result);
        return result;
    }
}
