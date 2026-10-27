using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Content;
using CivSim.Sim.Core;
using CivSim.Sim.Numerics;
using CivSim.Sim.Research;

namespace CivSim.Sim.Ai;

/// <summary>
/// The Strategist (docs/MVP/14 §2, §3.3, §5.4, §6, §7, §8.4): holds a grand plan and decides raids, wars, peace,
/// alliances, gifts, barter, Posture and Beacons for its civilization, by expected value on what it believes. It reads
/// only <see cref="StrategistView"/> (own state, the fog-limited belief, its own research) and acts only through
/// <see cref="IStrategistActions"/>. Every decision is logged with its reasons (14 §11).
/// <para>Season order: read own state and hard events, read every rival (forces, threat, war options), score the plan,
/// then peace, war and campaigns, raids, gifts and trade and alliances, Posture, Beacons.</para>
/// </summary>
public sealed partial class Strategist : IStrategist
{
    public const string Layer = "Strategist";

    private readonly AiSpec _spec;
    private readonly DifficultyProfile _profile;

    // ---- derived once from the catalog (not state) --------------------------------------
    private bool _resolved;
    private int _war02, _war03, _gov02, _trd01, _trd02, _dip03, _def02, _def03, _hus01, _com01, _com04, _met03, _met05;
    private int[] _eraReach = Array.Empty<int>();
    private Fix64 _minRaidSuccess, _minRaidValue;

    // ---- state: hashed and saved ----------------------------------------------------------
    private int _civs;
    private bool _started;
    private GrandPlan _plan = GrandPlan.FrontierRace;
    private int _planSince;
    private int _planTarget = -1;
    private readonly int[] _switchSeasons = { -1000, -1000, -1000, -1000 };
    private int _lastPostureChange = -1000;
    private int _raidStreak;
    internal int? NonConquerRaidPostureStreakForDiagnostics { get; set; }
    internal Fix64? FrontierEarlyBonusForDiagnostics { get; set; }
    internal int ThreeSeasonNonConquerRaidPostureOpportunitiesForDiagnostics { get; private set; }
    internal bool UseMaxCampaignOriginForDiagnostics { get; set; }
    private Fix64 _prevLegacy, _legacyRate, _prevDominion, _dominionRate;
    private readonly List<int> _prevSettlements = new();
    private readonly List<int> _beaconSeasons = new();
    private RivalMemory[] _memory = Array.Empty<RivalMemory>();

    public Strategist(AiSpec spec, DifficultyProfile profile)
    {
        _spec = spec;
        _profile = profile;
    }

    public bool ActsOnPolitics => true;
    public GrandPlan Plan => _plan;

    public void Decide(AiContext context) => Decide(StrategistView.From(context), new OrdersActions(context.Orders));

    /// <summary>One season's decisions on a view; the unit tests call this with hand-built views and a recorder.</summary>
    public void Decide(StrategistView v, IStrategistActions act)
    {
        Resolve(v.Catalog);
        EnsureState(v.Belief.Rivals.Length, v.Season);
        var s = new SeasonRead(v, _civs);

        ReadOwn(v, s);
        DetectHardEvents(v, s);
        ReadRivals(v, s);
        s.Frontier = FrontierValue(v);
        ReadLeader(v, s);
        ReadWarOptions(v, s);
        s.SecureTinValue = SecureTinPlanValue(v);
        ChoosePlan(v, s);
        PrioritizeBronzeDefense(v, act);

        DecidePeace(v, s, act);
        DecideWar(v, s, act);
        DecideRaids(v, s, act);
        DecideDiplomacy(v, s, act);
        DecidePosture(v, s, act);
        DecideBeacons(v, s, act);
        NoteDefense(v, s);

        Remember(v, s);
    }

    private static void PrioritizeBronzeDefense(StrategistView v, IStrategistActions act)
    {
        int settlement = BronzeDefensePolicy.SelectSettlement(v.Civ, v.Season, v.Belief.Map.Width,
            v.Own.ReachTiles, v.Own.Settlements, v.Belief.Rivals,
            v.Catalog.IndexOf("MET-04"), v.Catalog.IndexOf("MET-05"));
        if (settlement >= 0 && act.PrioritizePhysicalDefenseBuild(settlement))
            v.Note("DefenseBuild", settlement, -1, Fix64.One, "physical defense priority from believed bronze threat");
    }

    // ---- set-up ---------------------------------------------------------------------------

    private void Resolve(ResearchCatalog c)
    {
        if (_resolved) return;
        _war02 = c.IndexOf("WAR-02"); _war03 = c.IndexOf("WAR-03"); _gov02 = c.IndexOf("GOV-02"); _trd01 = c.IndexOf("TRD-01");
        _trd02 = c.IndexOf("TRD-02"); _dip03 = c.IndexOf("DIP-03"); _def02 = c.IndexOf("DEF-02"); _def03 = c.IndexOf("DEF-03");
        _hus01 = c.IndexOf("HUS-01"); _com01 = c.IndexOf("COM-01"); _com04 = c.IndexOf("COM-04");
        _met03 = c.IndexOf("MET-03"); _met05 = c.IndexOf("MET-05");
        var reach = new List<int>();
        foreach (ContentValue era in c.Pack.Root.Get("registries")!.Get("eras")!.Items) reach.Add((int)era.GetInt("reach")!);
        _eraReach = reach.ToArray();
        string margin = "medium";
        string id = _spec.Archetype.ToString().ToLowerInvariant();
        foreach (ContentValue a in c.Pack.Root.Get("registries")!.Get("archetypes")!.Items)
            if (a.GetString("id") == id) margin = a.GetString("raid_margin") ?? "medium";
        (_minRaidSuccess, _minRaidValue) = StrategistTuning.RaidMargin(margin);
        if (_minRaidSuccess < StrategistTuning.MinRaidSuccess) _minRaidSuccess = StrategistTuning.MinRaidSuccess;
        _resolved = true;
    }

    private void EnsureState(int civs, int season)
    {
        if (_memory.Length == civs) return;
        _civs = civs;
        _memory = new RivalMemory[civs];
        for (int c = 0; c < civs; c++) _memory[c] = new RivalMemory();
        _planSince = season;
    }

    private static bool Done(StrategistView v, int project) => project >= 0 && v.OwnDone(project);
    private static bool Believed(RivalBelief r, int project) => project >= 0 && project < r.ProjectBelieved.Length && r.ProjectBelieved[project];
    private int ReachOfEra(int era) => _eraReach.Length == 0 ? 12 : _eraReach[Math.Clamp(era, 0, _eraReach.Length - 1)];

    // ---- own state and hard events --------------------------------------------------------

    private void ReadOwn(StrategistView v, SeasonRead s)
    {
        OwnView own = v.Own;
        foreach (OwnSettlement o in own.Settlements)
        {
            s.OwnField += o.AttackingStrength;
            s.OwnDefense += o.DefendingStrength * o.Defenses.Multiplier();
        }
        // Legacy and Dominion rates per season, smoothed: they price a life and a captured settlement (14 §6.2).
        if (_started)
        {
            Fix64 dl = own.Legacy - _prevLegacy, dd = own.Dominion - _prevDominion;
            _legacyRate = _legacyRate + (dl - _legacyRate) / 4;
            _dominionRate = _dominionRate + (dd - _dominionRate) / 4;
        }
        _prevLegacy = own.Legacy;
        _prevDominion = own.Dominion;
        s.HorizonYears = Math.Max(2, v.Profile.HorizonEras * 2);
        Fix64 perYear = _legacyRate * 4;
        s.LifeLegacy = Fix64.Max(StrategistTuning.MinLifeLegacy,
            own.Adults > 0 ? perYear * s.HorizonYears / own.Adults : StrategistTuning.MinLifeLegacy);
    }

    private void DetectHardEvents(StrategistView v, SeasonRead s)
    {
        if (!_started) return;
        foreach (int prev in _prevSettlements)
            if (v.Own.SettlementAt(prev) is null) s.Hard("lost settlement " + prev);
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            RivalMemory m = _memory[o];
            PairView p = v.Own.Others[o];
            RivalBelief r = v.Belief.Rivals[o];
            if (p.LastRaidOnUsSeason > m.PrevRaidOnUs && p.LastRaidOnUsSeason >= 0) s.Hard("raided by civ " + o);
            if (p.AtWar && !m.PrevAtWar && m.WarDeclaredSeason != v.Season) s.Hard("civ " + o + " declared war");
            if (m.PrevAllied && !p.Allied) s.Hard("alliance with civ " + o + " broken");
            if (p.Contacted && !m.PrevContacted) s.Hard("first contact with civ " + o);
            if (r.KeystonesAnnounced + r.FirstsAnnounced > m.PrevAnnounced) s.Hard("civ " + o + " announced a First or Keystone");
        }
    }

    // ---- grand plan (14 §3.3) -------------------------------------------------------------

    private void ChoosePlan(StrategistView v, SeasonRead s)
    {
        Span<Fix64> value = stackalloc Fix64[7];
        Span<int> target = stackalloc int[7];
        // ApexRace is reserved but not yet scored. Initialize every slot so an unassigned plan cannot inherit
        // stack contents and make the selected plan vary with call history or runtime layout.
        value.Clear();
        target.Fill(-1);
        Fix64 frontierEarlyBonus = FrontierEarlyBonusForDiagnostics is { } overrideBonus
            ? overrideBonus
            : StrategistTuning.FrontierEarlyBonus;
        value[(int)GrandPlan.FrontierRace] = StrategistTuning.FrontierWeight * s.Frontier
            + (v.Own.Era <= 3 ? frontierEarlyBonus : Fix64.Zero);
        target[(int)GrandPlan.FrontierRace] = -1;
        value[(int)GrandPlan.Prosper] = StrategistTuning.ProsperWeight * (Fix64.One - s.Frontier);
        target[(int)GrandPlan.Prosper] = -1;
        value[(int)GrandPlan.Defend] = StrategistTuning.DefendWeight * s.Urgency;
        target[(int)GrandPlan.Defend] = s.TopThreatCiv;
        WarOption? best = s.BestWar;
        value[(int)GrandPlan.Conquer] = Fix64.Zero;
        target[(int)GrandPlan.Conquer] = -1;
        if (best is { Feasible: true } && best.Value > Fix64.Zero)
        {
            Fix64 v0 = StrategistTuning.ConquerBase + Fix64.Clamp(best.Value / StrategistTuning.ConquerValueScale, Fix64.Zero, StrategistTuning.ConquerValueCap) + best.Window;
            value[(int)GrandPlan.Conquer] = v0 * StrategistTuning.Aggression(_spec.Archetype);
            // A tactically feasible, positive-value conquest should not be hidden by SecureTin's fixed strategic
            // priority. Keep this as a plan-ranking floor: it does not loosen force, capture, supply, or safety gates.
            value[(int)GrandPlan.Conquer] = Fix64.Max(value[(int)GrandPlan.Conquer],
                s.SecureTinValue + StrategistTuning.FeasibleConquestPriorityMargin);
            target[(int)GrandPlan.Conquer] = best.Civ;
        }
        value[(int)GrandPlan.CoalitionAgainstLeader] = s.CoalitionValue;
        target[(int)GrandPlan.CoalitionAgainstLeader] = s.Leader;
        value[(int)GrandPlan.SecureTin] = s.SecureTinValue;
        target[(int)GrandPlan.SecureTin] = -1;

        int bestPlan = 0;
        for (int i = 1; i < value.Length; i++) if (value[i] > value[bestPlan]) bestPlan = i;
        Fix64 current = value[(int)_plan];
        var why = new System.Text.StringBuilder();
        why.Append("frontier ").Append(F2(value[0])).Append(", prosper ").Append(F2(value[1])).Append(", defend ").Append(F2(value[2]))
           .Append(", conquer ").Append(F2(value[3])).Append(", coalition ").Append(F2(value[4]))
           .Append(", secure tin ").Append(F2(value[(int)GrandPlan.SecureTin]));

        if (!_started)
        {
            _plan = (GrandPlan)bestPlan;
            _planSince = v.Season;
            _planTarget = target[bestPlan];
            v.Note("Plan", bestPlan, -1, value[bestPlan], $"hard=1 initial plan {_plan}: {why}");
        }
        else if (bestPlan != (int)_plan)
        {
            bool hard = s.HardEvent;
            bool beats = value[bestPlan] > current + StrategistTuning.CommitmentPenalty * current;
            int recent = 0;
            foreach (int season in _switchSeasons) if (v.Season - season < StrategistTuning.DitherWindowSeasons) recent++;
            // A hard event that calls for defense is answered at once; every other change waits out the dwell and the cap,
            // so a band raided every few seasons does not flip between racing and defending (14 §10.4 dithering).
            bool urgent = hard && bestPlan == (int)GrandPlan.Defend;
            // Defense is not given up while raids keep coming.
            bool besieged = false;
            for (int o = 0; o < _civs; o++)
                if (o != v.Civ && v.Season - v.Own.Others[o].LastRaidOnUsSeason <= StrategistTuning.RecentRaidSeasons) besieged = true;
            if (_plan == GrandPlan.Defend && besieged) urgent = false;
            bool settled = v.Season - _planSince >= StrategistTuning.PlanDwellSeasons && recent < StrategistTuning.MaxSwitchesInWindow;
            bool holdDefense = _plan == GrandPlan.Defend && besieged;
            if (urgent || (settled && !holdDefense && (hard || beats)))
            {
                GrandPlan old = _plan;
                _plan = (GrandPlan)bestPlan;
                _planSince = v.Season;
                _planTarget = target[bestPlan];
                Array.Copy(_switchSeasons, 1, _switchSeasons, 0, _switchSeasons.Length - 1);
                _switchSeasons[^1] = v.Season;
                v.Note("Plan", bestPlan, (int)old, value[bestPlan],
                    $"hard={(hard ? 1 : 0)} {old} -> {_plan}{(hard ? " after " + s.HardWhy : "")}: {why}");
            }
        }
        else _planTarget = target[bestPlan];
        // A target that is no longer there (peace made, rival gone) leaves a plan about nobody.
        if ((_plan is GrandPlan.Conquer or GrandPlan.CoalitionAgainstLeader) && _planTarget < 0) _planTarget = target[(int)_plan];

        StrategicIntent intent = v.Intent;
        intent.Plan = _plan;
        intent.PlanSinceSeason = _planSince;
        intent.TargetCiv = _plan switch
        {
            GrandPlan.Conquer or GrandPlan.CoalitionAgainstLeader => _planTarget,
            GrandPlan.Defend => s.TopThreatCiv,
            _ => -1,
        };
        intent.DefenseUrgency = s.Urgency;
        intent.FrontierValue = s.Frontier;
        Fix64 readiness = Fix64.Zero;
        if (_plan == GrandPlan.Conquer) readiness = Fix64.FromRatio(6, 10) + (best?.Window ?? Fix64.Zero);
        else if (best is { } b && b.Ratio > Fix64.Zero && b.Window > Fix64.Zero) readiness = Fix64.FromRatio(4, 10);
        else if (_raidStreak > 0) readiness = Fix64.FromRatio(35, 100);
        intent.AttackReadiness = Fix64.Clamp(readiness, Fix64.Zero, Fix64.One);
    }

    // ---- Posture (14 §4.5) ------------------------------------------------------------------

    private void DecidePosture(StrategistView v, SeasonRead s, IStrategistActions act)
    {
        if (!Done(v, _gov02)) return;
        Posture current = v.Own.Posture;
        if (NonConquerRaidPostureStreakForDiagnostics is > StrategistTuning.RaidPostureStreak &&
            _plan != GrandPlan.Conquer && _raidStreak == StrategistTuning.RaidPostureStreak &&
            s.Urgency < StrategistTuning.FortifyAt &&
            !(_plan == GrandPlan.Defend && s.Urgency >= StrategistTuning.FortifyAt / 2))
            ThreeSeasonNonConquerRaidPostureOpportunitiesForDiagnostics++;
        Posture want;
        string why;
        if (s.Urgency >= StrategistTuning.FortifyAt || (_plan == GrandPlan.Defend && s.Urgency >= StrategistTuning.FortifyAt / 2))
        { want = Posture.Fortify; why = $"threat from civ {s.TopThreatCiv}, urgency {F2(s.Urgency)}"; }
        else if (_plan == GrandPlan.Conquer && s.WarPostureWanted)
        { want = Posture.Raid; why = $"a war of conquest on civ {_planTarget} needs the Raid posture (13 §8.1)"; }
        else if (_raidStreak >= (NonConquerRaidPostureStreakForDiagnostics ?? StrategistTuning.RaidPostureStreak))
        { want = Posture.Raid; why = $"raids have paid in expectation {_raidStreak} seasons running"; }
        else if (s.Frontier >= StrategistTuning.FrontierOpen)
        { want = Posture.Expand; why = $"frontier open ({F2(s.Frontier)})"; }
        else if (s.Frontier < StrategistTuning.FrontierClosed || _plan == GrandPlan.Prosper)
        { want = Posture.Prosper; why = $"land closed ({F2(s.Frontier)})"; }
        else { want = current == Posture.Balanced ? Posture.Expand : current; why = $"frontier middling ({F2(s.Frontier)})"; }

        if (want == current) return;
        bool urgent = want == Posture.Fortify || current == Posture.Balanced;
        if (!urgent && v.Season - _lastPostureChange < StrategistTuning.PostureHoldSeasons) return;
        if (act.SetPosture(want))
        {
            _lastPostureChange = v.Season;
            v.Note("Posture", (int)want, (int)current, s.Urgency, $"{current} -> {want}: {why}");
            if (want == Posture.Fortify) s.Respond(s.TopThreatCiv, "Fortify posture");
        }
    }

    // ---- the defensive-response record (14 §10.4 undefended loss) -------------------------

    private void NoteDefense(StrategistView v, SeasonRead s)
    {
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            RivalRead r = s.Rivals[o];
            RivalMemory m = _memory[o];
            if (r.EffectiveThreat >= StrategistTuning.ThreatNoticeAt)
            {
                m.ThreatLast = v.Season;
                if (m.ThreatSince < 0)
                {
                    m.ThreatSince = v.Season;
                    v.Note("Threat", o, -1, r.EffectiveThreat,
                        $"civ {o} threatens: attack chance {F2(r.ThreatP)}, a raid on us would succeed {F2(r.PAgainst)}, expected loss {F1(r.ExpectedLoss)} rations");
                }
                // Defense research is the response always available: the urgency tells the Spark to put DEF and WAR first.
                if (s.Urgency >= StrategistTuning.FortifyAt) s.Respond(o, $"defense urgency {F2(s.Urgency)} for DEF/WAR research");
            }
            else if (m.ThreatSince >= 0 && v.Season - m.ThreatLast >= 4) m.ThreatSince = -1;
            if (s.Responses[o] is { } response)
            {
                m.LastDefenseSeason = v.Season;
                v.Note("Defense", o, -1, r.EffectiveThreat, response);
            }
        }
    }

    // ---- end of season ----------------------------------------------------------------------

    private void Remember(StrategistView v, SeasonRead s)
    {
        _started = true;
        _prevSettlements.Clear();
        foreach (OwnSettlement o in v.Own.Settlements) _prevSettlements.Add(o.Index);
        for (int o = 0; o < _civs; o++)
        {
            if (o == v.Civ) continue;
            RivalMemory m = _memory[o];
            PairView p = v.Own.Others[o];
            RivalBelief r = v.Belief.Rivals[o];
            RivalRead read = s.Rivals[o];
            m.PrevRaidOnUs = p.LastRaidOnUsSeason;
            m.PrevAtWar = p.AtWar || m.WarDeclaredSeason == v.Season;
            m.SmoothedThreat = read.Threat;
            m.PrevAllied = p.Allied;
            m.PrevContacted = p.Contacted;
            m.PrevAnnounced = r.KeystonesAnnounced + r.FirstsAnnounced;
            if (read.Field > Fix64.Zero)
            {
                if (m.LastField > Fix64.Zero) m.FieldTrend = m.FieldTrend + (read.Field - m.LastField - m.FieldTrend) / 4;
                m.LastField = read.Field;
            }
            m.PushThreat(v.Season, read.Threat);
        }
    }

    internal static string F1(Fix64 x) => Fmt(x, 10);
    internal static string F2(Fix64 x) => Fmt(x, 100);

    private static string Fmt(Fix64 x, int scale)
    {
        long scaled = (x * scale).RoundToInt();
        bool negative = scaled < 0;
        long mag = Math.Abs(scaled);
        int digits = scale == 10 ? 1 : 2;
        string frac = (mag % scale).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0');
        return (negative ? "-" : "") + (mag / scale).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + frac;
    }

    // ---- hashing and saves ----------------------------------------------------------------

    public void Hash(ref StateHasher h)
    {
        h.Add(_civs); h.Add(_started); h.Add((int)_plan); h.Add(_planSince); h.Add(_planTarget);
        foreach (int x in _switchSeasons) h.Add(x);
        h.Add(_lastPostureChange); h.Add(_raidStreak);
        h.Add(_prevLegacy); h.Add(_legacyRate); h.Add(_prevDominion); h.Add(_dominionRate);
        h.Add(_prevSettlements.Count); foreach (int x in _prevSettlements) h.Add(x);
        h.Add(_beaconSeasons.Count); foreach (int x in _beaconSeasons) h.Add(x);
        foreach (RivalMemory m in _memory) m.Hash(ref h);
    }

    public void Write(BinaryWriter w)
    {
        w.Write(_civs); w.Write(_started); w.Write((byte)_plan); w.Write(_planSince); w.Write(_planTarget);
        foreach (int x in _switchSeasons) w.Write(x);
        w.Write(_lastPostureChange); w.Write(_raidStreak);
        w.Write(_prevLegacy.Raw); w.Write(_legacyRate.Raw); w.Write(_prevDominion.Raw); w.Write(_dominionRate.Raw);
        w.Write(_prevSettlements.Count); foreach (int x in _prevSettlements) w.Write(x);
        w.Write(_beaconSeasons.Count); foreach (int x in _beaconSeasons) w.Write(x);
        foreach (RivalMemory m in _memory) m.Write(w);
    }

    public void Read(BinaryReader r)
    {
        _civs = r.ReadInt32(); _started = r.ReadBoolean(); _plan = (GrandPlan)r.ReadByte(); _planSince = r.ReadInt32(); _planTarget = r.ReadInt32();
        for (int i = 0; i < _switchSeasons.Length; i++) _switchSeasons[i] = r.ReadInt32();
        _lastPostureChange = r.ReadInt32(); _raidStreak = r.ReadInt32();
        _prevLegacy = Fix64.FromRaw(r.ReadInt64()); _legacyRate = Fix64.FromRaw(r.ReadInt64());
        _prevDominion = Fix64.FromRaw(r.ReadInt64()); _dominionRate = Fix64.FromRaw(r.ReadInt64());
        _prevSettlements.Clear();
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++) _prevSettlements.Add(r.ReadInt32());
        _beaconSeasons.Clear();
        n = r.ReadInt32();
        for (int i = 0; i < n; i++) _beaconSeasons.Add(r.ReadInt32());
        _memory = new RivalMemory[_civs];
        for (int c = 0; c < _civs; c++) { _memory[c] = new RivalMemory(); _memory[c].Read(r); }
    }
}
