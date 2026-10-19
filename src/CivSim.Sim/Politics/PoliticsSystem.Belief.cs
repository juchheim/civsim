using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>A rung of the weapon ladder of docs/MVP/13 §7.1: the project that puts the weapon in figures' hands.</summary>
public readonly record struct WeaponRung(int Project, Fix64 Strength, bool Ranged);

/// <summary>
/// Public rules the AI belief builder needs to turn sightings into beliefs (docs/MVP/14 §3.1, §5.2). Nothing here
/// reads a civilization's state.
/// </summary>
public sealed partial class PoliticsSystem
{
    private WeaponRung[]? _weaponLadder;

    /// <summary>The match seed, for streams keyed to the match (docs/MVP/15 rule D3).</summary>
    public ulong MatchSeed => _seed;

    /// <summary>
    /// The weapons a figure can be seen carrying, best first, as <see cref="WeaponOf"/> ranks them (13 §7.1): bow, atlatl,
    /// stone-tipped spear, wooden spear, club. A sighting of a weapon's strength implies its project.
    /// </summary>
    public IReadOnlyList<WeaponRung> WeaponLadder => _weaponLadder ??= new[]
    {
        new WeaponRung(_p.Hnt06, Fix64.FromRatio(18, 10), true),
        new WeaponRung(_p.Hnt05, Fix64.FromRatio(17, 10), true),
        new WeaponRung(_p.Hnt03, Fix64.FromRatio(16, 10), false),
        new WeaponRung(_p.Hnt02, Fix64.FromRatio(13, 10), false),
        new WeaponRung(_p.War01, Fix64.One, false),
    };

    /// <summary>Archery Warfare (WAR-03): bows seen at ×1.5 strength imply it.</summary>
    public int ArcheryProject => _p.War03;

    /// <summary>Routine reach of an era in tiles (13 §2.2; the eras registry).</summary>
    public int ReachOfEra(int era) => _eraReach[System.Math.Clamp(era, 0, _eraReach.Length - 1)];
}
