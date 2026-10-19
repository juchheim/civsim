using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// A scripted research player (docs/MVP/14 §10.2): an ordered script first (an opening, or lineages to favour), then the
/// Council's own ranking for whatever the script does not cover; or, for the sanity floor, random picks. It keeps every
/// slot full and the queue stocked, so it never idles between the once-a-season decisions. It holds no state.
/// </summary>
internal sealed class ScriptedSpark : IResearchSpark
{
    private readonly int[] _script;
    private readonly int[] _favoured;
    private readonly bool _random;

    private ScriptedSpark(int[] script, int[] favoured, bool random)
    {
        _script = script;
        _favoured = favoured;
        _random = random;
    }

    public static ScriptedSpark Opening(ResearchCatalog catalog, string openingId)
        => new(new List<int>(new OpeningBot(catalog, openingId).Steps).ToArray(), Array.Empty<int>(), false);

    public static ScriptedSpark Prefer(ResearchCatalog catalog, params string[] lineageCodes)
    {
        var lineages = new List<int>();
        foreach (string code in lineageCodes) lineages.Add(catalog.LineageIndex(code));
        return new ScriptedSpark(Array.Empty<int>(), lineages.ToArray(), false);
    }

    public static ScriptedSpark RandomPicks() => new(Array.Empty<int>(), Array.Empty<int>(), true);

    public void Decide(AiContext ctx)
    {
        AiResearchPort port = ctx.Research;
        ResearchCatalog catalog = ctx.Catalog;
        int count = catalog.Count;
        var prior = new Fix64[count];
        port.CouncilPrior(prior);

        // Candidates the people could take up now or soon, in this bot's order of preference.
        var order = new List<int>();
        var listed = new bool[count];
        foreach (int step in _script)
            if (Takeable(port, catalog, step) && !listed[step]) { order.Add(step); listed[step] = true; }
        var rest = new List<int>();
        for (int p = 0; p < count; p++)
            if (!listed[p] && Takeable(port, catalog, p)) rest.Add(p);
        if (_random)
        {
            Pcg32 rng = ctx.Stream(0xB07, 1);
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (rest[i], rest[j]) = (rest[j], rest[i]);
            }
        }
        else
        {
            // Favoured lineages first, then by the Council's score (stable: ties by project index).
            rest.Sort((a, b) =>
            {
                int fa = IsFavoured(catalog, a) ? 0 : 1, fb = IsFavoured(catalog, b) ? 0 : 1;
                if (fa != fb) return fa.CompareTo(fb);
                int c = prior[b].CompareTo(prior[a]);
                return c != 0 ? c : a.CompareTo(b);
            });
        }
        order.AddRange(rest);

        // Fill empty slots with the best Available project, then stock the queue behind them.
        int next = 0;
        for (int slot = 0; slot < CivResearch.SlotCount; slot++)
        {
            if (port.Slot(slot) >= 0) continue;
            while (next < order.Count && port.State(order[next]) != ProjectState.Available) next++;
            if (next >= order.Count) break;
            port.SetSlot(slot, order[next]);
            next++;
        }
        port.ClearQueue();
        int queued = 0;
        foreach (int p in order)
        {
            if (queued >= 8) break;
            if (port.State(p) == ProjectState.Slotted) continue;
            port.Enqueue(p);
            queued++;
        }
    }

    private bool IsFavoured(ResearchCatalog catalog, int project)
    {
        int lineage = catalog.Projects[project].Lineage;
        foreach (int l in _favoured) if (l == lineage) return true;
        return false;
    }

    private static bool Takeable(AiResearchPort port, ResearchCatalog catalog, int project)
    {
        if (!catalog.Projects[project].Numeric || port.IsDone(project)) return false;
        ProjectState s = port.State(project);
        return s == ProjectState.Available || s == ProjectState.Glimpsed || s == ProjectState.Slotted;
    }

    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}

/// <summary>Leaves the civilization on the v0 rules: the Council's politics with scripted research.</summary>
internal sealed class PassiveStrategist : IStrategist
{
    public bool ActsOnPolitics => false;
    public void Decide(AiContext context) { }
    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}

/// <summary>
/// Turtle (docs/MVP/14 §10.2): Fortify, makes peace whenever it can, gifts to keep relations warm, and never raids or
/// declares war.
/// </summary>
internal sealed class TurtleStrategist : IStrategist
{
    private const int GiftEverySeasons = 8;

    public bool ActsOnPolitics => true;

    public void Decide(AiContext ctx)
    {
        ctx.Orders.SetPosture(Posture.Fortify);
        foreach (PairView o in ctx.Own.Others)
        {
            if (o.Civ == ctx.Civ || !o.Contacted) continue;
            if (o.AtWar) ctx.Orders.MakePeace(o.Civ);
            else if (ctx.Season - o.LastGiftSeason >= GiftEverySeasons && ctx.Orders.Gift(o.Civ))
                ctx.Note("strategist", "gift", o.Civ, 0, Fix64.One, "turtle: keep the neighbours friendly");
        }
    }

    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}

/// <summary>
/// Rusher (docs/MVP/14 §10.2): Raid posture at GOV-02 and a raid on the nearest believed settlement of every contacted
/// rival, every season it has the people to spare, from first contact. It never makes peace and never gifts.
/// </summary>
internal sealed class RusherStrategist : IStrategist
{
    private static readonly int[] PartySizes = { 12, 8, 6, 4 };

    public RusherStrategist(ResearchCatalog catalog) { }

    public bool ActsOnPolitics => true;

    public void Decide(AiContext ctx)
    {
        ctx.Orders.SetPosture(Posture.Raid);
        if (ctx.Own.RaidsOnTheRoad > 0) return;
        int width = ctx.Belief.Map.Width;
        foreach (PairView o in ctx.Own.Others)
        {
            if (o.Civ == ctx.Civ || !o.Contacted || o.Allied) continue;
            RivalBelief rival = ctx.Belief.Rivals[o.Civ];
            // Aim at the believed settlement nearest to any of ours.
            int bestTarget = -1, bestDistance = int.MaxValue;
            foreach (BeliefSettlement t in rival.Settlements)
            {
                foreach (OwnSettlement mine in ctx.Own.Settlements)
                {
                    int d = HexGrid.Distance(mine.Tile, t.Tile, width);
                    if (d < bestDistance) { bestDistance = d; bestTarget = t.Settlement; }
                }
            }
            if (bestTarget < 0) continue;
            foreach (int party in PartySizes)
            {
                if (!ctx.Orders.Raid(bestTarget, party)) continue;
                ctx.Note("strategist", "raid", bestTarget, party, Fix64.One, "rusher: raid from contact");
                return;
            }
        }
    }

    public void Hash(ref StateHasher hasher) { }
    public void Write(BinaryWriter writer) { }
    public void Read(BinaryReader reader) { }
}
