using System.Collections.Generic;
using CivSim.Sim.Politics;

namespace CivSim.Harness;

internal static class M10B30Rules
{
    public static bool IsNakedCaravan(TradeGood good, bool hostileRoute, int escortCount)
        => good == TradeGood.Tin && hostileRoute && escortCount < 2;

    public static bool IsMandateOverflowSeason(bool positiveDecreeCandidate, int mandate, int cap)
        => positiveDecreeCandidate && cap > 0 && mandate >= cap;

    /// <summary>Advances the consecutive B30 overflow streak, resetting it as soon as any season fails the gate.</summary>
    public static int AdvanceMandateOverflowStreak(int currentStreak, bool positiveDecreeCandidate, int mandate, int cap)
        => IsMandateOverflowSeason(positiveDecreeCandidate, mandate, cap) ? currentStreak + 1 : 0;

    public static bool IsPositivePaidDecreeCandidate(bool positiveEv, int mandateCost)
        => positiveEv && mandateCost > 0;
}

/// <summary>Counts a caravan once when it is first observed on a hostile Tin route with fewer than two escorts.</summary>
internal sealed class NakedTinCaravanTracker
{
    private readonly HashSet<int> _counted = new();

    public bool Observe(int caravanId, TradeGood good, bool hostileRoute, int escortCount)
    {
        return M10B30Rules.IsNakedCaravan(good, hostileRoute, escortCount) && _counted.Add(caravanId);
    }
}

/// <summary>Keeps desertion baselines distinct for separate investment instances at the same settlement.</summary>
internal sealed class SiegeBlunderTracker
{
    private sealed class Episode(int initialMembers)
    {
        public int InitialMembers { get; } = initialMembers;
        public bool Reported;
    }

    private readonly Dictionary<(object Instance, int BeganSeason), Episode> _episodes = new();

    private readonly Dictionary<(int Civ, int Settlement), Episode> _eventEpisodes = new();

    public void BeginFromEvent(int civ, int settlement, int initialMembers)
        => _eventEpisodes[(civ, settlement)] = new Episode(initialMembers);

    public bool ObserveDesertionEvent(int civ, int settlement, int remainingMembers)
    {
        if (!_eventEpisodes.TryGetValue((civ, settlement), out Episode? episode)) return false;
        if (episode.Reported || !M10TelemetryRules.IsUnsuppliedSiegeBlunder(
                episode.InitialMembers, remainingMembers, foodSeasons: 0)) return false;
        episode.Reported = true;
        return true;
    }

    public bool Observe(object instance, int beganSeason, int remainingMembers, int foodSeasons)
    {
        var key = (instance, beganSeason);
        if (!_episodes.TryGetValue(key, out Episode? episode))
        {
            episode = new Episode(remainingMembers);
            _episodes.Add(key, episode);
        }
        if (episode.Reported || !M10TelemetryRules.IsUnsuppliedSiegeBlunder(
                episode.InitialMembers, remainingMembers, foodSeasons)) return false;
        episode.Reported = true;
        return true;
    }
}
