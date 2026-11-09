using System;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

/// <summary>Research preference weights derived from stable lineage and effect data.</summary>
internal static class CivilizationDirectionResearch
{
    private static readonly Fix64 StrongWeight = Fix64.FromRatio(175, 100);
    private static readonly Fix64 SupportingWeight = Fix64.FromRatio(125, 100);

    public static Fix64 Weight(CivilizationDirection direction, ProjectDef project, string lineage)
    {
        if (direction == CivilizationDirection.Balanced || project.IsKeystone) return Fix64.One;
        bool strong = direction switch
        {
            CivilizationDirection.Defensive => lineage == "DEF",
            CivilizationDirection.Offensive => lineage == "WAR",
            CivilizationDirection.Growth => lineage is "AGR" or "FOD" or "HUS",
            CivilizationDirection.Prosperity => lineage is "TRD" or "BLD" or "MEC" or "NRG" or "MIN" or "ART",
            CivilizationDirection.Discovery => lineage is "WAY" or "INQ",
            _ => false,
        };
        bool supporting = direction switch
        {
            CivilizationDirection.Defensive => lineage is "WAR" or "MED" or "HUS" or "GOV" or "SOC" or "FOD",
            CivilizationDirection.Offensive => lineage is "DEF" or "MET" or "MIN" or "TRN" or "SEA",
            CivilizationDirection.Growth => lineage is "FOR" or "HNT" or "MED" or "SOC" or "GOV",
            CivilizationDirection.Prosperity => lineage is "GOV" or "LRN" or "WOD" or "CER" or "STN",
            CivilizationDirection.Discovery => lineage is "SEA" or "TRN" or "LRN" or "COM" or "NUM" or "TRD",
            _ => false,
        };

        if (direction == CivilizationDirection.Discovery && project.Theme == Theme.Reach) strong = true;
        if (project.Theme == Theme.Kin && direction is CivilizationDirection.Growth or CivilizationDirection.Prosperity)
            supporting = true;
        if (project.Theme == Theme.Craft && direction == CivilizationDirection.Prosperity)
            supporting = true;

        foreach (EffectDef effect in project.Effects)
        {
            if (direction == CivilizationDirection.Defensive && effect.Kind == EffectKind.Defense) strong = true;
            if (direction == CivilizationDirection.Offensive && effect.Kind == EffectKind.Combat) strong = true;
            if (direction == CivilizationDirection.Growth && effect.Kind == EffectKind.Cap &&
                effect.Cap is CapKind.Food or CapKind.Housing or CapKind.Population) supporting = true;
            if (direction == CivilizationDirection.Discovery && effect.Kind is EffectKind.Reveal or EffectKind.Reach or EffectKind.KnowHow)
                strong = true;
            if (direction == CivilizationDirection.Prosperity &&
                (effect.Kind == EffectKind.Role || effect.Kind == EffectKind.Mod && effect.Stat is { } stat &&
                    (stat.Contains("trade", StringComparison.Ordinal) || stat.Contains("build", StringComparison.Ordinal) ||
                     stat.Contains("production", StringComparison.Ordinal))))
                supporting = true;
        }

        return strong ? StrongWeight : supporting ? SupportingWeight : Fix64.One;
    }
}
