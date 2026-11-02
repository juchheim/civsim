using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Randomness;

namespace CivSim.Sim.Ai;

/// <summary>Warlord-difficulty policy based on recent attacks and visible next-season ordinary raids (docs/MVP/07d, A3).</summary>
internal static class RaidWithdrawalPolicy
{
    private const int EvidenceSeasons = 8;
    private const int ForecastTrials = 8;

    internal static bool ShouldWithdraw(int attackerWins, bool incoming) =>
        incoming ? attackerWins >= ForecastTrials / 2 : attackerWins > ForecastTrials / 2;

    internal static bool HasRecentEvidence(int season, int observedSeason, int attackers, int byCiv, int rivalCount) =>
        season - observedSeason is >= 0 and <= EvidenceSeasons && attackers > 0 && byCiv >= 0 && byCiv < rivalCount;

    public static void Decide(AiSeat seat, PoliticsSystem politics, ulong matchSeed, int season)
    {
        if (seat.Control.Kind != ControlKind.Ai || seat.Control.Spec.Level != Difficulty.Warlord) return;
        foreach (OwnSettlement settlement in seat.Own.Settlements)
        {
            var incomingRaids = new List<IncomingParty>();
            foreach (IncomingParty incoming in seat.Belief.IncomingParties)
                if (incoming.Kind == ExpeditionKind.Raid && incoming.TargetSettlement == settlement.Index &&
                    incoming.ArriveSeason == season + 1 && incoming.PartySize > 0 &&
                    incoming.Civ >= 0 && incoming.Civ < seat.Belief.Rivals.Length)
                    incomingRaids.Add(incoming);

            bool recentEvidence = HasRecentEvidence(season, settlement.LastRaidObservedSeason,
                settlement.LastRaidObservedAttackers, settlement.LastRaidObservedByCiv, seat.Belief.Rivals.Length);
            bool withdraw = false;
            string evidence = "none";
            int forecastWins = -1;
            if (recentEvidence)
            {
                withdraw = ForecastLoses(settlement, settlement.LastRaidObservedAttackers,
                    seat.Belief.Rivals[settlement.LastRaidObservedByCiv], seat.Civ, season, matchSeed,
                    (ulong)(uint)settlement.LastRaidObservedSeason, incoming: false, out forecastWins);
                evidence = "historical";
            }
            if (!withdraw)
            {
                if (incomingRaids.Count == 1)
                {
                    IncomingParty incoming = incomingRaids[0];
                    withdraw = ForecastLoses(settlement, incoming.PartySize, seat.Belief.Rivals[incoming.Civ],
                        seat.Civ, incoming.ArriveSeason, matchSeed, (ulong)(uint)incoming.ArriveSeason,
                        incoming: true, out forecastWins);
                    evidence = "single-incoming";
                }
                else if (incomingRaids.Count > 1)
                {
                    withdraw = ForecastIncomingGroupLoses(settlement, incomingRaids, seat.Belief.Rivals,
                        matchSeed, out forecastWins);
                    evidence = "group";
                }
            }
            RaidResponse desired = withdraw ? RaidResponse.Withdraw : RaidResponse.Stand;
            RaidResponse current = politics.RaidResponseOf(settlement.Index);
            int incomingPartySize = 0;
            var incomingCivs = new SortedSet<int>();
            foreach (IncomingParty incoming in incomingRaids)
            {
                incomingPartySize += incoming.PartySize;
                incomingCivs.Add(incoming.Civ);
            }
            if (ShouldTraceDecision(politics.RaidWithdrawalTraceEnabledForDiagnostics, recentEvidence,
                incomingRaids.Count, current, desired))
                politics.AddRaidWithdrawalDecisionTraceForDiagnostics(FormatDecisionTrace(season, settlement.Index,
                    evidence, incomingRaids.Count, incomingPartySize,
                    string.Join(",", incomingCivs.Select(c => $"c{c}")), forecastWins, current, desired, season + 1));
            bool withdrawalReady = current == RaidResponse.Withdraw;
            if (current != desired)
                withdrawalReady = seat.Context.Orders.SetRaidResponse(settlement.Index, desired) &&
                    desired == RaidResponse.Withdraw;
            if (desired == RaidResponse.Withdraw && withdrawalReady)
                foreach (IncomingParty incoming in incomingRaids)
                    politics.RecordRaidWarning(incoming.SourceExpedition, seat.Civ, settlement.Index, season);
        }
    }

    private static bool ForecastLoses(OwnSettlement settlement, int count, RivalBelief rival, int civ,
        int arrivalSeason, ulong matchSeed, ulong eventSeason, bool incoming, out int attackerWins)
    {
        attackerWins = 0;
        if (count <= 0) return false;
        var attackers = new List<Fighter>(count);
        Fix64 weapon = rival.WeaponStrength > Fix64.Zero ? rival.WeaponStrength : Fix64.FromRatio(6, 10);
        for (int i = 0; i < count; i++) attackers.Add(new Fighter(i + 1, weapon));
        ulong root = Hash64.Combine(Hash64.Combine(matchSeed, (ulong)(uint)civ),
            Hash64.Combine((ulong)(uint)settlement.Index, eventSeason));
        for (int trial = 0; trial < ForecastTrials; trial++)
        {
            ulong stream = incoming
                ? Hash64.Combine(root, Hash64.Combine((ulong)(uint)arrivalSeason, (ulong)(uint)trial)) ^ 0x5241494457495448UL
                : Hash64.Combine(root, (ulong)(uint)trial) ^ 0x5241494457495448UL;
            RaidOutcome outcome = Raids.Resolve(attackers, settlement.Defenders, settlement.Defenses,
                rival.RangedSeen, matchSeed, stream);
            if (outcome.Success) attackerWins++;
        }
        return ShouldWithdraw(attackerWins, incoming);
    }

    /// <summary>Forecasts whether at least one party in a same-settlement, same-season raid group succeeds.</summary>
    private static bool ForecastIncomingGroupLoses(OwnSettlement settlement, List<IncomingParty> parties,
        RivalBelief[] rivals, ulong matchSeed, out int attackerWins)
    {
        var attackers = new List<Fighter>[parties.Count];
        for (int partyIndex = 0; partyIndex < parties.Count; partyIndex++)
        {
            IncomingParty party = parties[partyIndex];
            var partyAttackers = new List<Fighter>(party.PartySize);
            Fix64 weapon = rivals[party.Civ].WeaponStrength > Fix64.Zero
                ? rivals[party.Civ].WeaponStrength : Fix64.FromRatio(6, 10);
            for (int i = 0; i < party.PartySize; i++) partyAttackers.Add(new Fighter(i + 1, weapon));
            attackers[partyIndex] = partyAttackers;
        }

        attackerWins = 0;
        int arrivalSeason = parties[0].ArriveSeason;
        for (int trial = 0; trial < ForecastTrials; trial++)
        {
            var remainingDefenders = new List<Fighter>(settlement.Defenders);
            bool anySuccess = false;
            for (int partyIndex = 0; partyIndex < parties.Count && !anySuccess; partyIndex++)
            {
                IncomingParty party = parties[partyIndex];
                RivalBelief rival = rivals[party.Civ];
                ulong root = Hash64.Combine(Hash64.Combine(matchSeed, (ulong)(uint)party.Civ),
                    Hash64.Combine((ulong)(uint)settlement.Index, (ulong)(uint)arrivalSeason));
                ulong partyKey = Hash64.Combine((ulong)(uint)party.FromSettlement,
                    Hash64.Combine((ulong)(uint)party.PartySize, (ulong)(uint)partyIndex));
                ulong stream = Hash64.Combine(root,
                    Hash64.Combine((ulong)(uint)arrivalSeason,
                        Hash64.Combine((ulong)(uint)trial, partyKey))) ^ 0x5241494457495448UL;
                Defenses defenses = DefensesForIncomingParty(settlement.Defenses, partyIndex);
                RaidOutcome outcome = Raids.Resolve(attackers[partyIndex], remainingDefenders, defenses,
                    rival.RangedSeen, matchSeed, stream);
                anySuccess = outcome.Success;
                if (!anySuccess) RemoveDefenderCasualties(remainingDefenders, outcome.Battle);
            }
            if (anySuccess) attackerWins++;
        }
        return ShouldWithdraw(attackerWins, incoming: true);
    }

    internal static bool ShouldTraceDecision(bool enabled, bool recentEvidence, int incomingCount,
        RaidResponse before, RaidResponse desired) =>
        enabled && (recentEvidence || incomingCount > 0 || before != desired);

    internal static string FormatDecisionTrace(int season, int settlement, string evidence, int incomingCount,
        int incomingPartySize, string incomingCivs, int forecastWins, RaidResponse before, RaidResponse desired,
        int effectiveSeason) =>
        $"RAID_WITHDRAWAL_DECISION season={season} settlement=s{settlement} evidence={evidence} incoming={incomingCount} " +
        $"partySize={incomingPartySize} civs={(incomingCivs.Length == 0 ? "none" : incomingCivs)} " +
        $"forecast={(forecastWins < 0 ? "none" : $"{forecastWins}/8")} responseBefore={before} " +
        $"responseDesired={desired} effectiveSeason={effectiveSeason}";

    internal static string FormatArrivalTrace(int season, int target, int attacker, int partySize,
        RaidResponse response, bool surprised, bool success, bool withdrawn, int loot,
        int defenderDead, int defenderWounded, RaidOutcome? shadowStand = null, bool preparedWarning = false,
        bool surpriseRoll = false)
    {
        string shadow = shadowStand is null ? string.Empty :
            $" shadowStandSuccess={(shadowStand.Success ? "yes" : "no")} " +
            $"shadowStandDefenderDead={shadowStand.Battle.DefenderDead.Count} " +
            $"shadowStandDefenderWounded={shadowStand.Battle.DefenderWounded.Count} " +
            $"shadowStandLootCapacity={shadowStand.LootCapacity.ToDoubleForDisplay().ToString("F2", CultureInfo.InvariantCulture)}";
        return $"RAID_WITHDRAWAL_ARRIVAL season={season} target=s{target} attacker=c{attacker} partySize={partySize} " +
            $"effectiveResponse={response} surprise={(surprised ? "yes" : "no")} surpriseRoll={(surpriseRoll ? "yes" : "no")} " +
            $"success={(success ? "yes" : "no")} " +
            $"withdrawn={(withdrawn ? "yes" : "no")} loot={loot} defenderDead={defenderDead} " +
            $"defenderWounded={defenderWounded} preparedWarning={(preparedWarning ? "yes" : "no")}{shadow}";
    }

    internal static void RemoveDefenderCasualties(List<Fighter> defenders, BattleResult battle)
    {
        if (battle.DefenderDead.Count == 0 && battle.DefenderWounded.Count == 0) return;
        var casualties = new SortedSet<long>(battle.DefenderDead);
        casualties.UnionWith(battle.DefenderWounded);
        defenders.RemoveAll(defender => casualties.Contains(defender.Id));
    }

    internal static Defenses DefensesForIncomingParty(Defenses original, int partyIndex) =>
        partyIndex == 0 ? original : original with { RaidedRecently = true };
}
