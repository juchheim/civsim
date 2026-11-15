using System;
using CivSim.Sim.Politics;

namespace CivSim.Sim.View;

/// <summary>
/// What the viewer's people are armed with (docs/MVP/13 §7.1, 22 §7.1, 26 §4): the base weapon research gives every fighter, the research that unlocks metal arms,
/// chariots and siege engines, and for each own settlement the fighters it can field, the kits in store and how many are in someone's hands. Read from the same state
/// the combat rules use; nothing is written back. Per-settlement rows are indexed like the frame's settlement block and are zero for settlements that are not the viewer's.
/// </summary>
public sealed class ArmsFrame
{
    /// <summary>Base weapon tier of the viewer's fighters: see <see cref="PoliticsSystem.BaseWeaponTier"/>.</summary>
    public int BaseTier;
    /// <summary>The base weapon's strength multiplier in hundredths.</summary>
    public int BaseHundredths;
    /// <summary>Research flags: one bit per <c>Has*</c> constant.</summary>
    public int Research;
    public const int HasWarriors = 1, HasBronzeArms = 2, HasChariots = 4, HasProfessionals = 8, HasIronArms = 16, HasEngines = 32,
        HasAtlatl = 64, HasBow = 128, HasArchery = 256, HasSteel = 512;

    /// <summary>Own settlements, in frame order, and the per-settlement rows (zeroed for the rest).</summary>
    public SettlementArms[] Rows = Array.Empty<SettlementArms>();
    public int RowCount;

    /// <summary>Totals over every own settlement.</summary>
    public SettlementArms Total;

    internal void Ensure(int n)
    {
        if (Rows.Length < n) Array.Resize(ref Rows, Math.Max(n, Rows.Length * 2));
    }
}
