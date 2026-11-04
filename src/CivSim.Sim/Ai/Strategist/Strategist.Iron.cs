using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>
/// The Strategist's Iron-chapter levers (docs/MVP/25 §11): ending a war it is clearly winning on tributary terms, putting a galley off an enemy harbor it has
/// seen, and founding a league with an ally. It reads only its own exact state and its belief, and every order is checked again by the rules, so a refusal
/// costs nothing. There is no stored memory: each lever is tried on a fixed rhythm of seasons (staggered by rival), so a declined offer is not pressed again at
/// once and the Bronze snapshot is untouched.
/// </summary>
public sealed partial class Strategist
{
    /// <summary>Seasons of war before the Strategist offers terms, and how often it tries (tribute, blockade, league), staggered by the rival's number.</summary>
    internal const int IronTributeAfterSeasons = 4, IronTributeEvery = 8, IronBlockadeEvery = 4, IronLeagueEvery = 12;
    /// <summary>The believed strength lead it wants before imposing terms: a margin above the 1.5 the rules require, so belief error does not waste the try.</summary>
    internal static readonly Fix64 IronTributeLead = Fix64.FromRatio(17, 10);

    private void DecideIron(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        if (!v.IronChapter) return;
        int dip06 = v.Catalog.IndexOf("DIP-06"), dip07 = v.Catalog.IndexOf("DIP-07"), sea09 = v.Catalog.IndexOf("SEA-09");
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            PairView p = v.Own.Others[o];
            if (!p.Contacted) continue;
            RivalRead read = s.Rivals[o];
            RivalMemory m = _memory[o];
            if (p.AtWar)
            {
                // A clear lead ends the war on our terms; the rules decide whether the lead is real.
                if (dip06 >= 0 && v.OwnDone(dip06) && !v.Own.IsSubject && m.WarSince >= 0 && v.Season - m.WarSince >= IronTributeAfterSeasons &&
                    (v.Season + o) % IronTributeEvery == 0 && s.OwnField > Fix64.Zero && read.Field * IronTributeLead <= s.OwnField &&
                    v.Own.AssaultsOnTheRoad == 0 && v.Own.Occupations == 0 && act.ImposeTribute(o))
                {
                    v.Note("Tribute", o, -1, s.OwnField / Fix64.Max(read.Field, Fix64.One), $"civ {o} outmatched ({read.Field.ToDoubleForDisplay():F1} against our {s.OwnField.ToDoubleForDisplay():F1}): terms end the war");
                    s.Respond(o, "tribute imposed");
                    ResetWar(m);
                    continue;
                }
                // A galley off a harbor we have seen shuts its sea routes.
                if (sea09 >= 0 && v.OwnDone(sea09) && v.Own.CrewedGalleyHomes.Count > 0 && (v.Belief.Rivals[o].SeenFlags & SeenFlag.Harbor) != 0 &&
                    (v.Season + o) % IronBlockadeEvery == 0)
                {
                    int harbor = NearestRivalSettlement(v, o, v.Own.CrewedGalleyHomes[0]);
                    if (harbor >= 0 && act.Blockade(harbor))
                        v.Note("Blockade", harbor, o, Fix64.One, $"a galley from settlement {v.Own.CrewedGalleyHomes[0]} stands off the seen harbor at settlement {harbor}");
                }
            }
            else if (p.Allied && dip07 >= 0 && v.OwnDone(dip07) && (v.Season + o) % IronLeagueEvery == 0 && act.FormLeague(o))
                v.Note("League", o, -1, Fix64.One, $"allied with civ {o} and in a league with it for shared Wonder work");
        }
    }

    /// <summary>
    /// The believed settlement of a rival nearest to a home port, for a rival whose harbors have been seen (the flag is per rival, so the order is tried on the nearest
    /// town and the rules say whether it has one); -1 when none is believed.
    /// </summary>
    private static int NearestRivalSettlement(StrategistView v, int rival, int homeSettlement)
    {
        OwnSettlement? home = v.Own.SettlementAt(homeSettlement);
        if (home is null) return -1;
        int best = -1, bestDistance = int.MaxValue, width = v.Belief.Map.Width;
        foreach (BeliefSettlement b in v.Belief.Rivals[rival].Settlements)
        {
            int d = World.HexGrid.Distance(home.Tile, b.Tile, width);
            if (d < bestDistance || d == bestDistance && b.Settlement < best) { best = b.Settlement; bestDistance = d; }
        }
        return best;
    }
}
