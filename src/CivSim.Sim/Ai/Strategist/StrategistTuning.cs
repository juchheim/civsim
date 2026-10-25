using System;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Ai;

/// <summary>
/// Every number the Strategist decides by (docs/MVP/14 §14: AI parameters are data). Each is named for what it means
/// and says where it comes from; tuning changes one of these and the document that justifies it.
/// </summary>
public static class StrategistTuning
{
    // ---- grand plan (14 §3.3) --------------------------------------------------------------

    /// <summary>Switching plans must beat the current plan's value by 10% of it (14 §3.3); hard events waive it.</summary>
    public static readonly Fix64 CommitmentPenalty = Fix64.FromRatio(10, 100);
    /// <summary>Without a hard event the plan changes at most twice in this many seasons (14 §10.4 "dithering": > 2 in 3 years).</summary>
    public const int DitherWindowSeasons = 12;
    public const int MaxSwitchesInWindow = 2;
    /// <summary>A plan is held at least this long before it may change, unless a hard event calls for defense.</summary>
    public const int PlanDwellSeasons = 4;
    /// <summary>Weight of open land in the Frontier race plan (E0 to E3 default, 14 §3.3).</summary>
    public static readonly Fix64 FrontierWeight = Fix64.One;
    /// <summary>The frontier race keeps this much value through E3 even when the map is unread, so it stays the default.</summary>
    public static readonly Fix64 FrontierEarlyBonus = Fix64.FromRatio(15, 100);
    public static readonly Fix64 ProsperWeight = Fix64.FromRatio(80, 100);
    public static readonly Fix64 DefendWeight = Fix64.FromRatio(130, 100);
    public static readonly Fix64 ConquerBase = Fix64.FromRatio(45, 100);
    /// <summary>Margin by which an already feasible conquest outranks SecureTin (plan ranking only; tactical gates remain).</summary>
    public static readonly Fix64 FeasibleConquestPriorityMargin = Fix64.FromRatio(15, 100);
    /// <summary>Conquer X = (base + war value in Legacy ÷ 25, at most +0.8, + window) × archetype aggression.</summary>
    public const int ConquerValueScale = 25;
    public static readonly Fix64 ConquerValueCap = Fix64.FromRatio(80, 100);
    public static readonly Fix64 CoalitionBase = Fix64.FromRatio(55, 100);
    /// <summary>After KEY-3, an unmet tin route beats open-land growth when MET-03 has made the MET-05 fork available (22 §13.3).</summary>
    public static readonly Fix64 SecureTinBase = Fix64.FromRatio(120, 100);
    /// <summary>Added to forecast gross value only for legal tin mine/compact or arsenical fallback candidates while SecureTin is active.</summary>
    public static readonly Fix64 SecureTinMineDecreeBonus = Fix64.FromInt(50);
    public static readonly Fix64 SecureTinCompactDecreeBonus = Fix64.FromInt(50);
    public static readonly Fix64 SecureTinArsenicalDecreeBonus = Fix64.FromInt(25);
    public static readonly Fix64 SecureTinLevyDecreeBonus = Fix64.FromInt(50);
    /// <summary>A rival is "the leader" when its believed land leads everyone else's by 25% (14 §7.3)...</summary>
    public static readonly Fix64 LeaderLead = Fix64.FromRatio(125, 100);
    /// <summary>...or it holds a third of the land anyone holds, on the way to the 40% Dominion warning.</summary>
    public static readonly Fix64 LeaderShare = Fix64.FromRatio(33, 100);

    // ---- threat and defense (14 §5.4, §6.4) ----------------------------------------------

    /// <summary>Chance a rival attacks within the horizon, by Relations, before capability, archetype and reach.</summary>
    public static readonly Fix64 IntentAtWar = Fix64.FromRatio(85, 100);
    public static readonly Fix64 IntentHostile = Fix64.FromRatio(55, 100);   // Relations < -30
    public static readonly Fix64 IntentUneasy = Fix64.FromRatio(30, 100);    // Relations < 0
    public static readonly Fix64 IntentNeutral = Fix64.FromRatio(12, 100);   // Relations < 20
    public static readonly Fix64 IntentFriendly = Fix64.FromRatio(5, 100);
    /// <summary>A truce or an alliance bars raids by rule; only a pact-breaker would come.</summary>
    public static readonly Fix64 PactFactor = Fix64.FromRatio(10, 100);
    /// <summary>They raided us recently (fact, every level sees it).</summary>
    public static readonly Fix64 RecentRaidBonus = Fix64.FromRatio(30, 100);
    public const int RecentRaidSeasons = 8;
    /// <summary>Full opponent model only: warpaint (Raid posture) seen, hungry or sick people seen, warriors seen.</summary>
    public static readonly Fix64 RaidPostureBonus = Fix64.FromRatio(25, 100);
    public static readonly Fix64 DistressBonus = Fix64.FromRatio(10, 100);
    public static readonly Fix64 WarriorsBonus = Fix64.FromRatio(5, 100);
    /// <summary>Capability: Raiding (WAR-02) believed; not seen but in E2+; neither.</summary>
    public static readonly Fix64 CapabilityKnown = Fix64.One;
    public static readonly Fix64 CapabilityLikely = Fix64.FromRatio(50, 100);
    public static readonly Fix64 CapabilityUnlikely = Fix64.FromRatio(15, 100);
    /// <summary>Archetype factor = base + slope × P(Warlord): 1.0 for the uniform prior.</summary>
    public static readonly Fix64 ArchetypeBase = Fix64.FromRatio(70, 100);
    public static readonly Fix64 ArchetypeWarlordSlope = Fix64.FromRatio(150, 100);
    /// <summary>Raids go no further than three reaches (the rule parties travel by); beyond, a rival is a distant worry.</summary>
    public const int RaidRangeReaches = 3;
    public static readonly Fix64 OutOfRangeFactor = Fix64.FromRatio(20, 100);
    /// <summary>A raid party is about 30% of a settlement's fighters (13 §5.2: 15-30%, Raid posture 30%).</summary>
    public static readonly Fix64 ThreatPartyShare = Fix64.FromRatio(30, 100);
    /// <summary>Defense urgency = threat score × this, clamped to one: a one-in-two chance of a successful raid on us is full urgency.</summary>
    public static readonly Fix64 UrgencyScale = Fix64.FromInt(2);
    /// <summary>Urgency at which the AI takes Posture Fortify (14 §4.5 "Fortify when §6.4's threat is high").</summary>
    public static readonly Fix64 FortifyAt = Fix64.FromRatio(45, 100);
    /// <summary>Threat score at which a rival counts as an observed threat (logged; the undefended-loss detector keys on it).</summary>
    public static readonly Fix64 ThreatNoticeAt = Fix64.FromRatio(15, 100);
    /// <summary>A threat reading fades to this share a season unless renewed (rises at once), so defenses do not flicker.</summary>
    public static readonly Fix64 ThreatFade = Fix64.FromRatio(75, 100);
    /// <summary>Estimates are pessimistic by this much: populations are known to ±25% (14 §3.1).</summary>
    public static readonly Fix64 BeliefPessimism = Fix64.FromRatio(110, 100);
    /// <summary>Stale sightings grow by this share of the estimate at zero confidence.</summary>
    public static readonly Fix64 StaleGrowth = Fix64.FromRatio(25, 100);
    /// <summary>Confidence lost per season since a settlement was seen (one when just seen).</summary>
    public static readonly Fix64 FreshnessDecay = Fix64.FromRatio(125, 1000);

    // ---- raids (14 §6.1, 13 §5, §11 F4) ---------------------------------------------------

    /// <summary>
    /// Calibrates the simulated raid win probability to the prior 8-seed, 64-minute baseline (66/84 resolved:
    /// 75% predicted, 56% observed; observed/predicted ≈ 0.75). Recheck on the final 8-seed run.
    /// </summary>
    public static readonly Fix64 RaidSuccessCalibration = Fix64.FromRatio(3, 4);
    public static Fix64 CalibrateRaidSuccess(Fix64 simulatedProbability)
        => Fix64.Clamp(simulatedProbability, Fix64.Zero, Fix64.One) * RaidSuccessCalibration;

    public static bool MeetsRaidSuccessThreshold(Fix64 calibratedProbability, Fix64 threshold)
        => calibratedProbability >= threshold;

    /// <summary>Never launch a raid the belief gives less than this chance (14 §10.4 "suicide raid").</summary>
    public static readonly Fix64 MinRaidSuccess = Fix64.FromRatio(30, 100);
    /// <summary>Do not raid on a sighting older than two seasons: scout first (14 §6.1).</summary>
    public const int StaleSeasons = 2;
    /// <summary>A life is worth 8 rations and a raid's Relations cost 6 (the ledger of 13 §11 F4).</summary>
    public static readonly Fix64 LifeValue = Fix64.FromInt(8);
    public static readonly Fix64 RaidRelationsCost = Fix64.FromInt(6);
    /// <summary>Raiding a Peaceful neighbour also costs reputation 10 (13 §5.4), valued like the Relations cost.</summary>
    public static readonly Fix64 RaidReputationCost = Fix64.FromInt(6);
    /// <summary>The raiders carry 4 rations each (Raids.CarryPerRaider); 80% of it reaches home.</summary>
    public static readonly Fix64 LootDelivered = Fix64.FromRatio(80, 100);
    /// <summary>Believed stores per head of the target (observed E1-E2 bands hold about 0.8 rations a person).</summary>
    public static readonly Fix64 StoresPerHead = Fix64.FromRatio(80, 100);
    /// <summary>Stores by the season a raid lands (0 spring .. 3 winter): they peak after the autumn harvest (14 §6.1).</summary>
    public static readonly Fix64[] SeasonalStores =
        { Fix64.FromRatio(60, 100), Fix64.FromRatio(70, 100), Fix64.One, Fix64.FromRatio(90, 100) };
    /// <summary>
    /// Below the Full opponent model a rival village's fighters and defending strength are not seen, only its head count
    /// (14 §16.3), so a third of its people are assumed to fight (0.33 fighters a head in the accurate beliefs of Warlord, and the
    /// rule the threat estimate already uses for a rival whose villages are unplaced).
    /// </summary>
    public static readonly Fix64 UnseenFighterShare = Fix64.FromRatio(1, 3);
    /// <summary>
    /// ...and each is believed as strong as one of our own fighters times this. Measured against what a raid meets, a defender
    /// is about 0.6 of our raiders once the defence multiplier is counted separately (the old 1.3 doubled it, and with the
    /// defences unseen a blind Chieftain won about a quarter of the raids it rated at three in four, 14 §16.3a).
    /// </summary>
    public static readonly Fix64 UnseenDefenderStrength = Fix64.FromRatio(6, 10);
    /// <summary>
    /// A blind observer assumes a rival has the thorn hedge, lookouts and dogs from this era of the rival's (public)...
    /// </summary>
    public const int UnseenWorksEra = 1;
    /// <summary>...and the palisade and signal fires from this one (measured: 34 of 39 raided villages had a palisade).</summary>
    public const int UnseenPalisadeEra = 2;
    /// <summary>Raiders are the strongest fighters: party strength per raider vs the settlement's average.</summary>
    public static readonly Fix64 PartyPickBonus = Fix64.FromRatio(115, 100);
    /// <summary>Party sizes tried, as shares of the origin's fighters (13 §5.2: 15-30%, more with warriors).</summary>
    public static readonly Fix64[] RaidPartyShares = { Fix64.FromRatio(30, 100), Fix64.FromRatio(45, 100) };
    public const int RaidCooldownSeasons = 6;
    /// <summary>At most this many target settlements get a Monte Carlo each season (nearest first).</summary>
    public const int RaidTargetsConsidered = 3;
    /// <summary>A raid that lands in autumn or winter is not sent from a settlement whose late-winter food forecast is below this (14 §6.1).</summary>
    public static readonly Fix64 WinterFoodFloor = Fix64.FromRatio(80, 100);
    /// <summary>Seasons of positive expected value before the AI takes Posture Raid ("sustained", 14 §4.5).</summary>
    public const int RaidPostureStreak = 3;
    /// <summary>Retaliation: chance a raided rival answers, if hostile afterwards or not.</summary>
    public static readonly Fix64 RetaliationHostile = Fix64.FromRatio(60, 100);
    public static readonly Fix64 RetaliationOther = Fix64.FromRatio(20, 100);
    /// <summary>Retaliation from a rival not believed to know Raiding (WAR-02 is a hard rule for raids) is this likely.</summary>
    public static readonly Fix64 RetaliationUnarmed = Fix64.FromRatio(25, 100);
    /// <summary>The goods a successful raid on us carries off, in rations, before our stores cap it.</summary>
    public static readonly Fix64 ExposedLoot = Fix64.FromInt(16);

    // ---- war (14 §6.2, §6.3, 13 §8) -------------------------------------------------------

    /// <summary>Projected force ratio at arrival needed to declare and to assault; double against a palisade (14 §6.2).</summary>
    public static readonly Fix64 WarRatio = Fix64.FromRatio(15, 10);
    public static readonly Fix64 WarRatioPalisade = Fix64.FromInt(2);
    /// <summary>
    /// No war while the rival's believed army reaches this share of the 1.5 ratio against our weakest settlement in its
    /// reach (it would answer by taking it; below one for the uncertainty of the belief).
    /// </summary>
    public static readonly Fix64 CounterMargin = Fix64.FromRatio(80, 100);
    /// <summary>A settlement whose sisters within relief range can field this share of the rival's army is covered.</summary>
    public static readonly Fix64 ReliefCover = Fix64.FromRatio(75, 100);
    /// <summary>The Monte Carlo of the assault must take and hold the settlement at least this often (13 §11 F5 is ≥ 60% at 1.5×).</summary>
    public static readonly Fix64 MinCaptureChance = Fix64.FromRatio(60, 100);
    /// <summary>Relief from the defender's settlements within 8 tiles counts at this share (they bring 60% of their fighters).</summary>
    public static readonly Fix64 ReliefShare = Fix64.FromRatio(60, 100);
    public const int ReliefRange = 8;
    /// <summary>The field army is drawn from settlements within this many tiles of the origin.</summary>
    public const int ArmyGatherRange = 10;
    /// <summary>Peace: when the force ratio falls below one, or own adults fall by this share since the war began (14 §6.3).</summary>
    public static readonly Fix64 PeaceRatio = Fix64.One;
    public static readonly Fix64 LossesMount = Fix64.FromRatio(25, 100);
    /// <summary>After a capture, hold and pacify this long before the next target (14 §6.3 step 4).</summary>
    public const int PacifySeasons = 4;
    /// <summary>A war with no capture and no feasible target this long is not paying.</summary>
    public const int FruitlessWarSeasons = 8;
    /// <summary>Breaking a truce must pay this many times its reputation cost (14 §7.2 "by a wide margin").</summary>
    public static readonly Fix64 PactBreakMargin = Fix64.FromInt(3);
    /// <summary>Legacy a captured settlement adds a year, as a share of 100 Dominion points × own share ÷ own settlements.</summary>
    public static readonly Fix64 DominionPerYear = Fix64.FromInt(100);
    /// <summary>Reputation cost of a war, in Legacy points (third parties learn of it).</summary>
    public static readonly Fix64 WarReputationLegacy = Fix64.FromInt(4);
    /// <summary>Breaking a truce costs reputation 40 (13 §8.1), in Legacy points.</summary>
    public static readonly Fix64 TruceBreakLegacy = Fix64.FromInt(20);
    /// <summary>Floor of the value of a life in Legacy points.</summary>
    public static readonly Fix64 MinLifeLegacy = Fix64.FromRatio(1, 2);
    /// <summary>A fighter wounded for two seasons costs less than a life lost in the war valuation.</summary>
    public static readonly Fix64 WoundedLifeShare = Fix64.FromRatio(1, 4);

    // ---- diplomacy (14 §7) ----------------------------------------------------------------

    /// <summary>Gifts keep Relations above this with anyone we do not mean to fight.</summary>
    public const int GiftKeepAbove = 25;
    public const int GiftIntervalSeasons = 6;
    /// <summary>An appeasement gift to a threatening rival may follow sooner.</summary>
    public const int AppeaseIntervalSeasons = 2;
    public const int BarterIntervalSeasons = 4;
    /// <summary>A gift or barter trip leaves only from stores above this (the order itself needs 20).</summary>
    public static readonly Fix64 SpareFood = Fix64.FromInt(22);
    /// <summary>A rival is "stronger" (worth an early gift, 14 §7.1) when its believed field strength exceeds ours by this.</summary>
    public static readonly Fix64 StrongerBy = Fix64.FromRatio(90, 100);
    /// <summary>A shared threat for an alliance: a third civilization threatening us at least this much.</summary>
    public static readonly Fix64 SharedThreatAt = Fix64.FromRatio(15, 100);

    // ---- Posture and Beacons (14 §4.5, §8.4) ----------------------------------------------

    /// <summary>Frontier value at which the race is open (Posture Expand) and below which land is closed (Prosper).</summary>
    public static readonly Fix64 FrontierOpen = Fix64.FromRatio(40, 100);
    public static readonly Fix64 FrontierClosed = Fix64.FromRatio(20, 100);
    /// <summary>Before enough of the map is read, the frontier is assumed this open in E0 to E3 (the race is the default).</summary>
    public static readonly Fix64 FrontierPrior = Fix64.FromRatio(70, 100);
    public const int FrontierMinSamples = 24;
    /// <summary>Posture changes no more often than this unless a threat forces Fortify.</summary>
    public const int PostureHoldSeasons = 2;
    /// <summary>Beacons sit this far from own settlements (fission sites cluster within 15 tiles of a Beacon).</summary>
    public const int BeaconMinDistance = 6;
    public const int BeaconMaxDistance = 18;
    /// <summary>Daughter bands refuse sites this close to a rival settlement (PoliticsSystem rule), so a Beacon there is wasted.</summary>
    public const int BeaconRivalKeepOff = 20;
    /// <summary>Score bonus of a tile on the midline toward the believed rival (07b R9), and of a strategic deposit.</summary>
    public static readonly Fix64 BeaconMidlineBonus = Fix64.FromRatio(60, 100);
    public static readonly Fix64 BeaconDepositBonus = Fix64.FromRatio(40, 100);
    /// <summary>Extra Beacon value for an unclaimed tin placer after copper tools, before the tin-bronze route is secured (22 §13.3).</summary>
    public static readonly Fix64 BeaconTinRouteBonus = Fix64.FromRatio(75, 100);
    /// <summary>A Beacon is re-placed after this long, or at once when its tile is taken.</summary>
    public const int BeaconStaleSeasons = 16;

    // ---- budget (14 §12) --------------------------------------------------------------------

    /// <summary>
    /// Monte Carlo trials per raid/war estimate. A 32-trial floor limits winner's-curse selection from comparing several
    /// noisy candidate probabilities; higher beam widths retain the original scaling above the floor.
    /// </summary>
    public static int Trials(DifficultyProfile profile) => Math.Max(32, 8 + 2 * profile.BeamWidth);
    /// <summary>Trials in the second look at a raid about to be ordered. The search uses 10 to 20 trials a candidate, every season, for
    /// every village; the best of that many noisy estimates reads high (a true 0.2 shows 0.7 now and then), so the chosen raid
    /// is played again with this many on a fresh stream and must still clear its margins (14 §16.3a).</summary>
    /// </summary>
    public const int RaidConfirmTrials = 64;

    /// <summary>Raid margins by the archetype registry's raid_margin (14 §9.1): minimum believed success and expected value in rations.</summary>
    public static (Fix64 MinSuccess, Fix64 MinValue) RaidMargin(string margin) => margin switch
    {
        "high" => (Fix64.FromRatio(60, 100), Fix64.FromInt(4)),
        "low" => (Fix64.FromRatio(35, 100), Fix64.FromRatio(1, 2)),
        _ => (Fix64.FromRatio(45, 100), Fix64.FromInt(2)),
    };

    /// <summary>How readily an archetype goes to war (multiplies the Conquer plan's value; 14 §9.1 style, not competence).</summary>
    public static Fix64 Aggression(Economy.EconomyArchetype archetype) => archetype switch
    {
        Economy.EconomyArchetype.Warlord => Fix64.FromRatio(130, 100),
        Economy.EconomyArchetype.Wanderer => Fix64.One,
        Economy.EconomyArchetype.Balanced => Fix64.One,
        _ => Fix64.FromRatio(80, 100),
    };
}
