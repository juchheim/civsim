using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Research;

/// <summary>
/// The Bronze-age research state that is not a project: Mandate (docs/MVP/22 §4.2), Admin span and Overreach reduction
/// (22 §8.2-8.3) and the Legacy for built Wonders (22 §3.3). Mandate is stored (the current value and the recharge
/// counter, checksummed and snapshotted in <see cref="CivResearch"/>); cap, recharge and the Admin figures are derived
/// from the completed `mandate` and `admin` effects. Nothing here changes what the economy does: the Decrees that spend
/// Mandate and the Cohesion that reads Admin arrive with the systems that own them.
/// </summary>
public sealed partial class ResearchSystem
{
    /// <summary>True once a completed project (KEY-3) has given the civilization a Mandate.</summary>
    public bool MandateUnlocked(int civ) => _civs[civ].MandateUnlocked;
    public int Mandate(int civ) => _civs[civ].Mandate;
    public int MandateCap(int civ) => _civs[civ].MandateCap;
    /// <summary>Seasons per point of Mandate: 8 less the Kin projects' reductions, floored at MandateRechargeMin.</summary>
    public int MandateRechargeSeasons(int civ) => _civs[civ].MandateRecharge;
    public int MandateEarned(int civ) => _civs[civ].MandateEarned;
    public int MandateSpent(int civ) => _civs[civ].MandateSpent;

    /// <summary>Seasons until the next point of Mandate; 0 when the Mandate is full (it does not accumulate) or not unlocked.</summary>
    public int MandateSeasonsToNext(int civ)
    {
        CivResearch c = _civs[civ];
        if (!c.MandateUnlocked || c.Mandate >= c.MandateCap) return 0;
        return Math.Max(1, c.MandateRecharge - c.MandateCounter);
    }

    /// <summary>
    /// Spends Mandate on a Decree (docs/MVP/22 §4.5: only on success). False, and nothing spent, when the civilization
    /// holds less than <paramref name="cost"/>. A cost of 0 always succeeds.
    /// </summary>
    public bool TrySpendMandate(int civ, int cost)
    {
        CivResearch c = _civs[civ];
        if (cost < 0 || cost > c.Mandate) return false;
        c.Mandate -= cost;
        c.MandateSpent += cost;
        return true;
    }

    /// <summary>
    /// The Mandate a Decree costs this civilization (docs/MVP/23 §12): the DecreeCost constant, or the lowest cost any
    /// completed `mandate` effect sets for it (DIP-04 sets Compact to 0). <paramref name="decree"/> is found, expedition, mine,
    /// levy, corvee or compact.
    /// </summary>
    public int DecreeCost(int civ, string decree)
    {
        string constant = decree switch
        {
            "found" => "DecreeCostFound",
            "expedition" => "DecreeCostExpedition",
            "mine" => "DecreeCostMine",
            "levy" => "DecreeCostLevy",
            "corvee" => "DecreeCostCorvee",
            "compact" => "DecreeCostCompact",
            _ => throw new ArgumentException($"Unknown Decree '{decree}'.", nameof(decree)),
        };
        int cost = _catalog.Constant(constant).FloorToInt();
        CivResearch c = _civs[civ];
        for (int p = 0; p < _catalog.Count; p++)
        {
            if (!c.Done[p]) continue;
            foreach (EffectDef e in _catalog.Projects[p].Effects)
                if (e.Kind == EffectKind.Mandate && e.Decree == decree && e.DecreeCost < cost) cost = e.DecreeCost;
        }
        return cost;
    }

    /// <summary>Admin span: settlements the polity holds without strain (4 at KEY-3, up to 15 with every Kin project).</summary>
    public int AdminSpan(int civ) => _civs[civ].AdminSpan;

    /// <summary>The share of Overreach the civilization's institutions remove: <c>1 - Π(1 - r)</c> over completed `admin` effects.</summary>
    public Fix64 AdminReduction(int civ) => Fix64.One - _civs[civ].AdminKeep;

    /// <summary>
    /// Credits a built Wonder with its Legacy, <c>25 × (era + 1)</c> (docs/MVP/22 §3.3). The civilization must have researched
    /// the Wonder project; each civilization is credited once per Wonder. Returns false when nothing was awarded.
    /// </summary>
    public bool AwardWonderBuilt(int civ, int project)
    {
        CivResearch c = _civs[civ];
        if ((uint)project >= (uint)_catalog.Count || !c.Done[project]) return false;
        ProjectDef def = _catalog.Projects[project];
        if (def.Type != ProjectType.Wonder) return false;
        int at = c.WondersBuilt.BinarySearch(project);
        if (at >= 0) return false;
        c.WondersBuilt.Insert(~at, project);
        c.WondersLegacy += _catalog.WonderBuiltUnits(def);
        return true;
    }

    public bool WonderBuilt(int civ, int project) => _civs[civ].WondersBuilt.BinarySearch(project) >= 0;
}
