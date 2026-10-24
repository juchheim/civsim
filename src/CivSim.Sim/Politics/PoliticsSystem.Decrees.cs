using System;
using System.Collections.Generic;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Randomness;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>
/// Mandate and the six Decrees (docs/MVP/22 §4; WP8a). A Decree is queued when it is issued (command 18, the Strategist's
/// <c>IStrategistActions</c>, or the Council's policy) and resolved in the politics step at the end of the season: civilizations in
/// ascending order, each civilization's Decrees in issue order, then its policy's. Mandate is spent only on success; a refusal
/// emits <see cref="PoliticsEventKind.DecreeRefused"/> (and <see cref="PoliticsEventKind.OrderRefused"/> for a player command) with
/// one of the reason codes NoMandate, NoDonor, NoSite, OutOfReach, Rejected. Donor and site choices use the fission scoring with
/// ascending-id tie-breaks; expedition contact rolls use <c>Pcg32.ForStream(seed, "politics.expedition", civ, season)</c>.
/// </summary>
public sealed partial class PoliticsSystem
{
    private static readonly ulong ExpeditionStream = Hash64.OfString("politics.expedition");
    private static readonly ulong BronzePoliticsTag = 0x504F4C4252303331UL; // "POLBR031"

    /// <summary>The Decree constants of docs/MVP/23 §12 (content/constants/balance.yaml), read once.</summary>
    private sealed class DecreeRules
    {
        public int DonorMinPop, MinDaughter, SettleReach, CartBonus, SeaReach, OwnMinDistBeyondRadius, RivalMinDist;
        public int BoostSeasons, StartCohesion, ExpeditionSize, ExpeditionRangeMult, Sight, RevealRadius, ContactRadius;
        public int LevySeasons, LevyTravelSeasons, CorveeSeasons, CorveeCohesion;
        public Fix64 DonorMinFill, FoundShare, ClaimBoost, RidingSpeed, RidingRange, LevyOthers;

        public DecreeRules(ResearchCatalog c)
        {
            int I(string id) => c.Constant(id).FloorToInt();
            DonorMinPop = I("FoundDonorMinPop"); DonorMinFill = c.Constant("FoundDonorMinFill"); FoundShare = c.Constant("FoundShare");
            MinDaughter = I("FoundMinDaughter"); SettleReach = I("SettleReach"); CartBonus = I("SettleReachCartBonus"); SeaReach = I("SettleReachSea");
            OwnMinDistBeyondRadius = I("FoundOwnMinDist"); RivalMinDist = I("FoundRivalMinDist"); ClaimBoost = c.Constant("FoundClaimBoost");
            BoostSeasons = I("FoundClaimBoostYears") * 4; StartCohesion = I("FoundStartCohesion");
            ExpeditionSize = I("ExpeditionSize"); ExpeditionRangeMult = I("ExpeditionRangeMult"); Sight = I("ExpeditionSight");
            RevealRadius = I("ExpeditionRevealRadius"); ContactRadius = I("ExpeditionContactRadius");
            RidingSpeed = c.Constant("RidingSpeedMult"); RidingRange = c.Constant("RidingRangeMult");
            LevySeasons = I("LevySeasons"); LevyOthers = c.Constant("LevyOtherAdultsShare"); LevyTravelSeasons = I("LevyTravelSeasons");
            CorveeSeasons = I("CorveeSeasons"); CorveeCohesion = I("CorveeCohesion");
        }
    }

    /// <summary>Projects the Bronze politics reads, resolved once.</summary>
    private readonly struct BronzeProjects
    {
        public readonly int Key3, Ter05, Trn05, Trn06, Inq03, Way04, War04, Dip04, Dip05, Num04, Met02, Met03, Met04, Met05, Min04;

        public BronzeProjects(ResearchCatalog c)
        {
            Key3 = c.IndexOf("KEY-3"); Ter05 = c.IndexOf("TER-05"); Trn05 = c.IndexOf("TRN-05"); Trn06 = c.IndexOf("TRN-06");
            Inq03 = c.IndexOf("INQ-03"); Way04 = c.IndexOf("WAY-04"); War04 = c.IndexOf("WAR-04"); Dip04 = c.IndexOf("DIP-04");
            Dip05 = c.IndexOf("DIP-05"); Num04 = c.IndexOf("NUM-04"); Met02 = c.IndexOf("MET-02"); Met03 = c.IndexOf("MET-03");
            Met04 = c.IndexOf("MET-04"); Met05 = c.IndexOf("MET-05"); Min04 = c.IndexOf("MIN-04");
        }
    }

    private DecreeRules _rules = null!;
    private BronzeProjects _bp;
    private readonly List<PendingDecree> _decreeQueue = new();
    private readonly List<DecreeRequest> _policyScratch = new();
    private IDecreePolicy?[] _policy = Array.Empty<IDecreePolicy?>();
    private ICompactJudge?[] _judge = Array.Empty<ICompactJudge?>();
    private readonly List<Levy> _levies = new();
    /// <summary>Settlements founded by Decree with their claim boost (TER-05: ×1.5 for 2 years): (settlement, first season without it).</summary>
    private readonly List<(int Settlement, int Until)> _foundBoost = new();
    /// <summary>Per civilization and Decree kind (index civ × 7 + kind): Decrees carried out, and refused.</summary>
    private int[] _decreesDone = Array.Empty<int>(), _decreesRefused = Array.Empty<int>();
    /// <summary>Per civilization: seasons with Mandate unlocked, and of those at the cap (docs/MVP/22 §15 T27).</summary>
    private int[] _mandateSeasons = Array.Empty<int>(), _mandateAtCap = Array.Empty<int>();
    /// <summary>Tiles each civilization's expeditions have revealed (null until the first returns).</summary>
    private bool[]?[] _revealed = Array.Empty<bool[]?>();
    private int _nextPartyId;
    /// <summary>Per civilization: settlements founded since its KEY-3 (fission and Decree), for T16's share; and how many settlements were tallied.</summary>
    private int[] _e3Foundings = Array.Empty<int>();
    private int _foundingsSeen;

    private void InitDecrees()
    {
        _rules = new DecreeRules(_catalog);
        _bp = new BronzeProjects(_catalog);
        _policy = new IDecreePolicy?[_civs];
        var council = new CouncilDecreePolicy();
        for (int c = 0; c < _civs; c++) _policy[c] = council;
        _judge = new ICompactJudge?[_civs];
        _decreesDone = new int[_civs * 7];
        _decreesRefused = new int[_civs * 7];
        _mandateSeasons = new int[_civs];
        _mandateAtCap = new int[_civs];
        _revealed = new bool[]?[_civs];
        _e3Foundings = new int[_civs];
        _embargo = new byte[_civs * _civs];
        InitTrade();
    }

    // ---- public surface ------------------------------------------------------------------------------

    /// <summary>
    /// Replaces a civilization's Decree policy (docs/MVP/22 §4.6). The default is the Council's (<see cref="CouncilDecreePolicy"/>);
    /// the AI work package passes its Strategist's policy, or null when its Strategist issues through <c>IStrategistActions</c>.
    /// The policy runs only while the civilization's Council is on (a player who turns the Council off issues every Decree).
    /// </summary>
    public void SetDecreePolicy(int civ, IDecreePolicy? policy) => _policy[civ] = policy;
    public IDecreePolicy? DecreePolicyOf(int civ) => _policy[civ];

    /// <summary>Replaces how a civilization judges compact, pact and treaty proposals (null: the rule of docs/MVP/22 §4.3).</summary>
    public void SetCompactJudge(int civ, ICompactJudge? judge) => _judge[civ] = judge;

    public IReadOnlyList<PendingDecree> PendingDecrees => _decreeQueue;
    public IReadOnlyList<Levy> Levies => _levies;
    public int DecreesDone(int civ, DecreeKind kind) => _decreesDone[civ * 7 + (int)kind];
    public int DecreesRefused(int civ, DecreeKind kind) => _decreesRefused[civ * 7 + (int)kind];
    public int DecreesDone(int civ) { int n = 0; for (int k = 1; k < 7; k++) n += _decreesDone[civ * 7 + k]; return n; }
    public int MandateSeasons(int civ) => _mandateSeasons[civ];
    /// <summary>Settlements the civilization founded since its KEY-3, by fission or Decree (docs/MVP/22 §15 T16: 25–50% by Decree).</summary>
    public int E3Foundings(int civ) => _e3Foundings[civ];
    public int MandateSeasonsAtCap(int civ) => _mandateAtCap[civ];
    public ResearchViewForDecrees DecreeResearch => new(_research);

    /// <summary>The Mandate a Decree costs the civilization now (docs/MVP/23 §12; DIP-04 makes the Compact free).</summary>
    public int DecreeCost(int civ, DecreeKind kind) => kind is >= DecreeKind.Found and <= DecreeKind.Compact
        ? _research.DecreeCost(civ, DecreeArgs.ContentId(kind)) : 0;

    /// <summary>Queues a Decree for the end of this season (the Strategist's path; command 18 comes through <see cref="Step"/>). False when the kind is unknown.</summary>
    public bool IssueDecree(int civ, DecreeKind kind, int target, int extra, DecreeOrigin origin = DecreeOrigin.Strategist)
    {
        if (unchecked((uint)civ) >= unchecked((uint)_civs) || kind is < DecreeKind.Found or > DecreeKind.Compact) return false;
        _decreeQueue.Add(new PendingDecree(civ, kind, target, extra, _tick, origin));
        return true;
    }

    private void QueueDecreeCommand(Command command)
    {
        if (command.A is < (int)DecreeKind.Found or > (int)DecreeKind.Compact)
        {
            Refused(command, command.B, OrderRefusalReason.InvalidTarget);
            return;
        }
        _decreeQueue.Add(new PendingDecree(command.Civ, (DecreeKind)command.A, command.B, command.C, command.Tick, DecreeOrigin.Command));
    }

    /// <summary>Whether a tile has been revealed to the civilization by one of its expeditions.</summary>
    public bool HasRevealed(int civ) => _revealed[civ] is not null;
    public bool IsRevealed(int civ, int tile) => _revealed[civ] is { } r && unchecked((uint)tile) < unchecked((uint)r.Length) && r[tile];

    /// <summary>
    /// Whether the civilization knows a tile (docs/MVP/22 §4.3 "a known deposit tile"): within its routine Reach of one of its
    /// settlements (where its figures range) or revealed by an expedition.
    /// </summary>
    public bool KnowsTile(int civ, int tile)
    {
        if (unchecked((uint)tile) >= unchecked((uint)_tileArray.Length)) return false;
        if (IsRevealed(civ, tile)) return true;
        int reach = ReachOf(civ);
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && HexGrid.Distance(_sTile[s], tile, _width) <= reach) return true;
        return false;
    }

    /// <summary>The levy host standing against <paramref name="targetSettlement"/>, or null (docs/MVP/22 §4.3; the combat consumer reads it).</summary>
    public Levy? LevyFor(int civ, int targetSettlement)
    {
        foreach (Levy l in _levies)
            if (l.Civ == civ && l.TargetSettlement == targetSettlement && l.UntilSeason > _season) return l;
        return null;
    }

    public bool HasActiveLevy(int civ)
    {
        foreach (Levy levy in _levies)
            if (levy.Civ == civ && levy.UntilSeason > _season) return true;
        return false;
    }

    /// <summary>
    /// The next raid, assault or investment on the target takes the host (docs/MVP/22 §4.3): returns its living, unwounded members
    /// and dissolves the levy, so they work again. False when no host stands against that target.
    /// </summary>
    public bool TryTakeLevy(int civ, int targetSettlement, out List<Fighter> host)
    {
        host = new List<Fighter>();
        Levy? levy = LevyFor(civ, targetSettlement);
        if (levy is null) return false;
        for (int i = 0; i < levy.Members.Count; i++)
            if (_economy.TryFigure(levy.Members[i], out _) && !_economy.IsWounded(levy.Members[i]))
                host.Add(new Fighter(levy.Members[i], levy.Strengths[i]));
        _economy.ClearIdle(levy.Members);
        _levies.Remove(levy);
        return true;
    }

    /// <summary>The claim radius a civilization's villages have (the era's), for the founding distance rule r + 1.</summary>
    private int EraRadiusOf(int civ) => _eraRadius[Math.Min(_research.Era(civ), _eraRadius.Length - 1)];

    /// <summary>
    /// SettleReach for a founding (docs/MVP/22 §4.3): 14 tiles over land, +6 with carts (TRN-05), 24 by sea with SEA-04 (21 §3).
    /// </summary>
    public int FoundReach(int civ, bool bySea)
    {
        if (bySea) return CanSail(civ) ? _rules.SeaReach : 0;
        return _rules.SettleReach + (Done(civ, _bp.Trn05) ? _rules.CartBonus : 0);
    }

    private bool Done(int civ, int project) => project >= 0 && _research.IsDone(civ, project);

    // ---- resolution ------------------------------------------------------------------------------------

    /// <summary>The politics step's Decree pass (docs/MVP/22 §4.5): civilizations ascending, queued Decrees in issue order, then the policy's.</summary>
    private void ResolveDecrees()
    {
        TallyFoundings();
        ExpireLevies();
        if (_economy.BronzeActive) _economy.PruneDecreeState();
        if (_decreeQueue.Count == 0 && !AnyMandate()) return;
        for (int civ = 0; civ < _civs; civ++)
        {
            for (int i = 0; i < _decreeQueue.Count; i++)
                if (_decreeQueue[i].Civ == civ) Resolve(_decreeQueue[i]);
            if (_research.MandateUnlocked(civ) && _policy[civ] is { } policy && _research.CouncilOn(civ))
            {
                _policyScratch.Clear();
                policy.Propose(this, civ, _policyScratch);
                foreach (DecreeRequest r in _policyScratch)
                    Resolve(new PendingDecree(civ, r.Kind, r.Target, r.Extra, _tick, DecreeOrigin.Council));
            }
            if (_research.MandateUnlocked(civ))
            {
                _mandateSeasons[civ]++;
                if (_research.Mandate(civ) >= _research.MandateCap(civ)) _mandateAtCap[civ]++;
            }
        }
        _decreeQueue.Clear();
        TallyFoundings();
    }

    /// <summary>Counts the settlements that appeared since the last tally as foundings of civilizations past KEY-3 (Free Folk are no one's).</summary>
    private void TallyFoundings()
    {
        for (int i = _foundingsSeen; i < _sCount; i++)
        {
            int c = _sCiv[i];
            if (!_free[i] && unchecked((uint)c) < unchecked((uint)_civs) && _research.MandateUnlocked(c)) _e3Foundings[c]++;
        }
        _foundingsSeen = Math.Max(_foundingsSeen, _sCount);
    }

    private bool AnyMandate()
    {
        for (int c = 0; c < _civs; c++) if (_research.MandateUnlocked(c)) return true;
        return false;
    }

    private void Resolve(PendingDecree d)
    {
        int civ = d.Civ;
        int other = -1;
        OrderRefusalReason reason;
        int subject = -1;
        int cost = DecreeCost(civ, d.Kind);
        if (!_research.MandateUnlocked(civ) || _research.Mandate(civ) < cost) reason = OrderRefusalReason.NoMandate;
        else
        {
            reason = d.Kind switch
            {
                DecreeKind.Found => ExecuteFound(civ, d.Target, out subject),
                DecreeKind.Expedition => ExecuteExpedition(civ, d.Target, out subject),
                DecreeKind.Mine => ExecuteMine(civ, d.Target, out subject),
                DecreeKind.Levy => ExecuteLevy(civ, d.Target, out subject, out other),
                DecreeKind.Corvee => ExecuteCorvee(civ, d.Target, d.Extra, out subject),
                DecreeKind.Compact => ExecuteCompact(civ, d.Target, d.Extra, out subject, out other),
                _ => OrderRefusalReason.InvalidTarget,
            };
        }
        if (reason == 0)
        {
            if (!_research.TrySpendMandate(civ, cost)) throw new InvalidOperationException("Mandate checked but not spendable.");
            _decreesDone[civ * 7 + (int)d.Kind]++;
            _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.DecreeResolved, other, (int)d.Kind, subject));
            return;
        }
        _decreesRefused[civ * 7 + (int)d.Kind]++;
        if (other < 0 && d.Kind == DecreeKind.Compact && !DecreeArgs.IsFolkParty(d.Target) && unchecked((uint)d.Target) < unchecked((uint)_civs)) other = d.Target;
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.DecreeRefused, other, (int)d.Kind, (int)reason));
        if (d.Origin == DecreeOrigin.Command)
            _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.OrderRefused, d.Target, (int)CommandKind.IssueDecree, (int)reason));
    }

    // ---- Found a village ------------------------------------------------------------------------------

    /// <summary>
    /// Why a founding on <paramref name="tile"/> would be refused, or 0 (docs/MVP/22 §4.3): the tile must be habitable, not rival- or
    /// Free-Folk-owned, at least r + 1 from an own settlement, 9 from a rival or Free Folk one, and within SettleReach of a donor.
    /// <paramref name="donor"/> is the chosen donor: the own settlement with the highest people ÷ EffectivePopCap among those with
    /// at least 20 people and 60% of their cap, within reach and off their fission cooldown (ascending index on ties).
    /// </summary>
    public OrderRefusalReason FoundCheck(int civ, int tile, out int donor)
    {
        donor = -1;
        if (unchecked((uint)tile) >= unchecked((uint)_tileArray.Length) || !_economy.IsHabitableSite(tile)) return OrderRefusalReason.NoSite;
        int owner = _territory.OwnerOf(tile);
        if (owner >= 0 && owner != civ) return OrderRefusalReason.NoSite;
        if (FreeFolkOwns(tile)) return OrderRefusalReason.NoSite;
        int ownMin = EraRadiusOf(civ) + _rules.OwnMinDistBeyondRadius;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (_sCiv[s] == civ && !_free[s]) { if (d < ownMin) return OrderRefusalReason.NoSite; }
            else if (d < _rules.RivalMinDist) return OrderRefusalReason.NoSite;
        }
        bool anyInReach = false;
        Fix64 bestFill = Fix64.MinValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            bool walk = WalkFrom(_sTile[s])[tile] != int.MaxValue;
            int reach = FoundReach(civ, bySea: !walk);
            if (reach == 0 || d > reach || (!walk && !CanReach(civ, _sTile[s], tile))) continue;
            anyInReach = true;
            if (_sPop[s] < _rules.DonorMinPop || _economy.SeasonsSinceFission(s) < 4) continue;
            Fix64 fill = _economy.PopulationFill(s);
            if (fill < _rules.DonorMinFill) continue;
            if (fill > bestFill) { bestFill = fill; donor = s; }
        }
        if (!anyInReach) return OrderRefusalReason.OutOfReach;
        return donor < 0 ? OrderRefusalReason.NoDonor : 0;
    }

    private OrderRefusalReason ExecuteFound(int civ, int tile, out int subject)
    {
        subject = -1;
        OrderRefusalReason why = FoundCheck(civ, tile, out int donor);
        if (why != 0) return why;
        int village = _economy.FoundByDecree(donor, tile, _rules.FoundShare, _rules.MinDaughter, _rules.StartCohesion);
        if (village < 0) return OrderRefusalReason.NoDonor;
        RefreshSettlements();
        if (Done(civ, _bp.Ter05)) _foundBoost.Add((village, _season + _rules.BoostSeasons));
        subject = village;
        return 0;
    }

    /// <summary>The claim boost of a village founded by Decree (TER-05: ×1.5 for 2 years), 1 otherwise.</summary>
    private Fix64 FoundBoost(int settlement)
    {
        foreach ((int s, int until) in _foundBoost)
            if (s == settlement && until > _season) return _rules.ClaimBoost;
        return Fix64.One;
    }

    // ---- Expedition ------------------------------------------------------------------------------------

    /// <summary>The farthest an expedition may go (docs/MVP/22 §4.3): 2 × Reach, ×1.5 riding (TRN-06).</summary>
    public int ExpeditionRange(int civ)
    {
        int range = ReachOf(civ) * _rules.ExpeditionRangeMult;
        return Done(civ, _bp.Trn06) ? (Fix64.FromInt(range) * _rules.RidingRange).FloorToInt() : range;
    }

    private OrderRefusalReason ExecuteExpedition(int civ, int tile, out int subject)
    {
        subject = tile;
        if (unchecked((uint)tile) >= unchecked((uint)_tileArray.Length)) return OrderRefusalReason.NoSite;
        int origin = -1, best = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (d < best && AdultCount(s) >= _rules.ExpeditionSize + 4) { best = d; origin = s; }
        }
        if (origin < 0) return OrderRefusalReason.NoDonor;
        if (best > ExpeditionRange(civ)) return OrderRefusalReason.OutOfReach;
        int travel = Travel(civ, _sTile[origin], tile);
        if (travel == int.MaxValue) return OrderRefusalReason.OutOfReach;
        List<Fighter> party = PickParty(origin, civ, _rules.ExpeditionSize, attacking: true, out List<long> ids);
        if (party.Count < _rules.ExpeditionSize) return OrderRefusalReason.NoDonor;
        Fix64 food = _economy.TakeGoods(origin, Fix64.FromInt(_rules.ExpeditionSize), LootPreference.FoodOnly);
        var e = new Expedition
        {
            Kind = ExpeditionKind.Explore, Civ = civ, TargetCiv = -1, FromSettlement = origin, ToSettlement = -1, TargetTile = tile,
            ArriveSeason = _season + ExploreSeasons(civ, travel), Value = food, Id = _nextPartyId++,
        };
        for (int i = 0; i < ids.Count; i++) { e.Members.Add(ids[i]); e.Strengths.Add(party[i].Strength); }
        _expeditions.Add(e);
        return 0;
    }

    private int ExploreSeasons(int civ, int tiles)
    {
        if (Done(civ, _bp.Trn06)) tiles = (Fix64.FromInt(tiles) / _rules.RidingSpeed).FloorToInt();
        return SeasonsFor(Math.Max(1, tiles));
    }

    /// <summary>The explorers reach their target: they see along the way and around it, become Aware of anyone seen, may meet them, and turn home.</summary>
    private void ArriveExplore(Expedition e)
    {
        if (e.Leg == 1) { ReportExpedition(e); return; }
        int civ = e.Civ;
        if (unchecked((uint)e.FromSettlement) >= unchecked((uint)_sCount) || AliveFighters(e, out _).Count == 0) return;
        int from = _sTile[e.FromSettlement], to = e.TargetTile;
        int sight = _rules.Sight + (Done(civ, _bp.Trn06) ? 1 : 0);
        var seen = new bool[_tileArray.Length];
        HexGrid.Coordinates(from, _width, out int fx, out int fy);
        HexGrid.Coordinates(to, _width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(from, to, _width));
        for (int k = 0; k <= steps; k++)
        {
            int x = fx + (tx - fx) * k / steps, y = fy + (ty - fy) * k / steps;
            MarkDisk(seen, HexGrid.Index(x, y, _width, _height), sight);
        }
        MarkDisk(seen, to, _rules.RevealRadius);
        e.Revealed.Clear();
        for (int t = 0; t < seen.Length; t++) if (seen[t]) e.Revealed.Add(t);
        // Anyone seen becomes known; within two tiles of the party's path or target they meet (a roll per civilization).
        Pcg32 roll = Pcg32.ForStream(_seed, ExpeditionStream, (ulong)civ, _season);
        var met = new bool[_civs];
        for (int s = 0; s < _sCount; s++)
        {
            int o = _sCiv[s];
            if (_free[s] || o == civ || !seen[_sTile[s]]) continue;
            if (!_dip.Aware(civ, o))
            {
                _dip.MarkAware(civ, o);
                _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.Aware, o, 0, 1));
                _events.Add(new PoliticsEvent(_tick, (byte)o, PoliticsEventKind.Aware, civ, 0, 1));
            }
            if (met[o] || _dip.Contacted(civ, o) || !NearPath(_sTile[s], from, to, _rules.ContactRadius)) continue;
            met[o] = true;
            // Three in four such meetings become first contact (docs/MVP/22 §4.3 "roll first contact within 2 tiles").
            if (roll.NextBelow(4) < 3) MakeContact(civ, o);
        }
        if (Done(civ, _bp.Way04)) CommitReveal(civ, e.Revealed);
        e.Leg = 1;
        int back = Travel(civ, to, from);
        e.ArriveSeason = _season + ExploreSeasons(civ, back == int.MaxValue ? steps : back);
        _expeditions.Add(e);
    }

    private bool NearPath(int tile, int from, int to, int radius)
    {
        if (HexGrid.Distance(tile, to, _width) <= radius) return true;
        HexGrid.Coordinates(from, _width, out int fx, out int fy);
        HexGrid.Coordinates(to, _width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(from, to, _width));
        for (int k = 0; k <= steps; k++)
            if (HexGrid.Distance(tile, HexGrid.Index(fx + (tx - fx) * k / steps, fy + (ty - fy) * k / steps, _width, _height), _width) <= radius) return true;
        return false;
    }

    private void MarkDisk(bool[] marked, int center, int radius)
    {
        HexGrid.Coordinates(center, _width, out int cx, out int cy);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(_height - 1, cy + radius); y++)
            for (int x = Math.Max(0, cx - radius - 1); x <= Math.Min(_width - 1, cx + radius + 1); x++)
            {
                int t = y * _width + x;
                if (HexGrid.Distance(center, t, _width) <= radius) marked[t] = true;
            }
    }

    private void ReportExpedition(Expedition e)
    {
        // Explorers who all fell report nothing.
        if (AliveFighters(e, out _).Count == 0 && e.Members.Count > 0) return;
        CommitReveal(e.Civ, e.Revealed);
        if (unchecked((uint)e.FromSettlement) < unchecked((uint)_sCount) && e.Value > Fix64.Zero) _economy.GiveGoods(e.FromSettlement, e.Value / 2);
        _events.Add(new PoliticsEvent(_tick, (byte)e.Civ, PoliticsEventKind.ExpeditionReturned, -1, e.Revealed.Count, e.TargetTile));
    }

    /// <summary>Makes the explorers' discoveries civilization-wide: the tiles, and the gate resources on them (deposits with INQ-03; herds).</summary>
    private void CommitReveal(int civ, List<int> tiles)
    {
        bool[] r = _revealed[civ] ??= new bool[_tileArray.Length];
        bool prospect = Done(civ, _bp.Inq03);
        ulong seen = 0;
        foreach (int t in tiles)
        {
            r[t] = true;
            string? gate = _tileArray[t].Deposit switch
            {
                DepositKind.Tin => "Tin", DepositKind.NativeCopper => "Native copper", DepositKind.Salt => "Salt",
                DepositKind.Gold or DepositKind.Silver => "Gold or Silver", DepositKind.Coal => "Coal", DepositKind.SilkMoths => "Silk moths",
                _ => null,
            };
            if (gate is null || (!prospect && gate is "Tin" or "Native copper" or "Gold or Silver")) continue;
            int index = _catalog.GateTargetIndex(gate);
            if (index >= 0) seen |= 1UL << index;
        }
        foreach (Herd herd in _generated.Herds)
        {
            if (unchecked((uint)herd.Tile) >= unchecked((uint)r.Length) || !r[herd.Tile]) continue;
            string? gate = herd.Species switch
            {
                HerdSpecies.Horse => "Wild horses", HerdSpecies.WildGoatSheep => "Wild goats or sheep", HerdSpecies.CamelLlama => "Camels or Llamas",
                _ => null,
            };
            int index = gate is null ? -1 : _catalog.GateTargetIndex(gate);
            if (index >= 0) seen |= 1UL << index;
        }
        if (seen != 0) _research.AddSeen(civ, seen);
    }

    // ---- Open a mine -----------------------------------------------------------------------------------

    /// <summary>Why <c>Open a mine</c> on <paramref name="tile"/> would be refused, or 0: a known deposit, not rival- or Free-Folk-owned, within mining reach.</summary>
    public OrderRefusalReason MineCheck(int civ, int tile)
    {
        if (unchecked((uint)tile) >= unchecked((uint)_tileArray.Length)) return OrderRefusalReason.NoSite;
        if (_tileArray[tile].Deposit is not (DepositKind.CopperOre or DepositKind.ArsenicalCopper or DepositKind.Tin or DepositKind.Gold
            or DepositKind.Silver or DepositKind.Salt or DepositKind.Iron)) return OrderRefusalReason.NoSite;
        if (!KnowsTile(civ, tile) || FreeFolkOwns(tile)) return OrderRefusalReason.NoSite;
        if (!_economy.CanDig(civ, tile)) return OrderRefusalReason.NoSite;
        int owner = _territory.OwnerOf(tile);
        if (owner >= 0 && owner != civ) return OrderRefusalReason.NoSite;
        int nearest = -1, best = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (d < best) { best = d; nearest = s; }
        }
        if (nearest < 0) return OrderRefusalReason.NoDonor;
        if (best > _economy.MiningReachFrom(nearest)) return OrderRefusalReason.OutOfReach;
        return 0;
    }

    private OrderRefusalReason ExecuteMine(int civ, int tile, out int subject)
    {
        subject = tile;
        OrderRefusalReason why = MineCheck(civ, tile);
        if (why != 0) return why;
        return _economy.OpenMiningCamp(civ, tile) switch
        {
            MiningCampResult.Opened => 0,
            MiningCampResult.OutOfReach => OrderRefusalReason.OutOfReach,
            MiningCampResult.NoParent or MiningCampResult.NoCrew => OrderRefusalReason.NoDonor,
            _ => OrderRefusalReason.NoSite,
        };
    }

    // ---- Raise the levy -------------------------------------------------------------------------------

    /// <summary>Whether the civilization has seen the settlement within the last two seasons: within its Reach of an own settlement, or on a tile its expeditions revealed.</summary>
    public bool SightedRecently(int civ, int settlement)
    {
        if (unchecked((uint)settlement) >= unchecked((uint)_sCount)) return false;
        int tile = _sTile[settlement];
        if (IsRevealed(civ, tile)) return true;
        int reach = ReachOf(civ);
        for (int s = 0; s < _sCount; s++)
            if (_sCiv[s] == civ && !_free[s] && _sPop[s] > 0 && HexGrid.Distance(_sTile[s], tile, _width) <= reach) return true;
        return false;
    }

    private OrderRefusalReason ExecuteLevy(int civ, int target, out int subject, out int other)
    {
        subject = target;
        other = -1;
        if (unchecked((uint)target) >= unchecked((uint)_sCount) || _sPop[target] <= 0) return OrderRefusalReason.NoSite;
        bool folk = _free[target];
        int owner = _sCiv[target];
        if (!folk && owner == civ) return OrderRefusalReason.NoSite;
        if (!folk) other = owner;
        if (!SightedRecently(civ, target)) return OrderRefusalReason.OutOfReach;
        if (!folk && (_dip.Allied(civ, owner) || _dip.InTruce(civ, owner, _season) || HasPact(civ, owner))) return OrderRefusalReason.Rejected;
        foreach (Levy l in _levies) if (l.Civ == civ && l.UntilSeason > _season) return OrderRefusalReason.Rejected;
        // All warriors and half the other adults of the own settlements within two seasons' travel (docs/MVP/22 §4.3).
        int reach = _rules.LevyTravelSeasons * TilesPerSeason;
        var levy = new Levy { Civ = civ, TargetSettlement = target, SinceSeason = _season, UntilSeason = _season + _rules.LevySeasons };
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            int travel = Travel(civ, _sTile[s], _sTile[target]);
            if (travel == int.MaxValue || travel > reach) continue;
            List<Fighter> fighters = Fighters(s, attacking: true, out _);
            var records = new List<LivingEconomyModel.FighterRecord>();
            _economy.ListFightingAge(s, records);
            int adults = 0;
            foreach (var r in records) if (r.Age < 45 && !r.Wounded) adults++;
            int warriors = Done(civ, _p.War04) ? adults / 5 : 0;
            var ordered = new List<Fighter>(fighters);
            ordered.Sort((x, y) => x.Strength != y.Strength ? y.Strength.CompareTo(x.Strength) : x.Id.CompareTo(y.Id));
            int others = Math.Max(0, ordered.Count - warriors);
            int take = Math.Min(ordered.Count, warriors + (Fix64.FromInt(others) * _rules.LevyOthers).FloorToInt());
            // Four stay home in every settlement.
            take = Math.Min(take, Math.Max(0, ordered.Count - 4));
            for (int i = 0; i < take; i++)
            {
                if (_economy.IsIdle(ordered[i].Id)) continue;
                levy.Members.Add(ordered[i].Id);
                levy.Strengths.Add(ordered[i].Strength);
            }
        }
        if (levy.Members.Count < 4) return OrderRefusalReason.NoDonor;
        _economy.SetIdle(levy.Members, levy.UntilSeason);
        _levies.Add(levy);
        return 0;
    }

    private void ExpireLevies()
    {
        for (int i = _levies.Count - 1; i >= 0; i--)
        {
            if (_levies[i].UntilSeason > _season) continue;
            _economy.ClearIdle(_levies[i].Members);
            _levies.RemoveAt(i);
        }
    }

    // ---- Corvée ---------------------------------------------------------------------------------------

    private OrderRefusalReason ExecuteCorvee(int civ, int settlement, int target, out int subject)
    {
        subject = settlement;
        if (unchecked((uint)settlement) >= unchecked((uint)_sCount) || _sCiv[settlement] != civ || _free[settlement] || _sPop[settlement] <= 0)
            return OrderRefusalReason.NoSite;
        byte kind = (byte)Math.Clamp(target, 0, LivingEconomyModel.CorveeTarget.Count - 1);
        if (target != kind) return OrderRefusalReason.NoSite;
        if (!_economy.SetCorvee(settlement, _season + _rules.CorveeSeasons, kind)) return OrderRefusalReason.NoSite;
        // Cohesion −3 there (C points; F = 0.6 + 0.006 C).
        _economy.AdjustCohesion(settlement, Fix64.FromRatio(6, 1000) * _rules.CorveeCohesion);
        return 0;
    }

    // ---- hashing and snapshots ----------------------------------------------------------------------

    private bool BronzeStateEmpty => _decreeQueue.Count == 0 && _levies.Count == 0 && _investments.Count == 0 && _foundBoost.Count == 0 && _nextPartyId == 0 &&
                                     _compacts.Count == 0 && _pacts.Count == 0 && _folkTies.Count == 0 && _tinFolk.Count == 0 &&
                                     !_tinFolkSpawned && !AnyRevealed() && !AnyEmbargo() && !AnyMandate();

    private bool AnyRevealed()
    {
        foreach (bool[]? r in _revealed) if (r is not null) return true;
        return false;
    }

    private void HashExpeditionBronze(ref StateHasher h, Expedition e)
    {
        h.Add(e.Id); h.Add(e.TargetTile); h.Add(e.TargetParty); h.Add((byte)e.Good); h.Add(e.Cargo); h.Add(e.CompactId); h.Add(e.FolkSeller);
        h.Add(e.EscortCount);
        h.Add(e.Revealed.Count); foreach (int t in e.Revealed) h.Add(t);
    }

    /// <summary>The Bronze politics state, hashed only once there is any, so E0–E2 checksums are those of politics without it.</summary>
    private void HashBronze(ref StateHasher h)
    {
        if (BronzeStateEmpty) return;
        h.Add(BronzePoliticsTag);
        h.Add(_decreeQueue.Count);
        foreach (PendingDecree d in _decreeQueue) { h.Add(d.Civ); h.Add((byte)d.Kind); h.Add(d.Target); h.Add(d.Extra); h.Add(d.Tick); h.Add((byte)d.Origin); }
        h.Add(_levies.Count);
        foreach (Levy l in _levies)
        {
            h.Add(l.Civ); h.Add(l.TargetSettlement); h.Add(l.SinceSeason); h.Add(l.UntilSeason);
            h.Add(l.Members.Count); for (int i = 0; i < l.Members.Count; i++) { h.Add(l.Members[i]); h.Add(l.Strengths[i]); }
        }
        h.Add(_foundBoost.Count); foreach ((int s, int u) in _foundBoost) { h.Add(s); h.Add(u); }
        foreach (int v in _decreesDone) h.Add(v);
        foreach (int v in _decreesRefused) h.Add(v);
        foreach (int v in _mandateSeasons) h.Add(v);
        foreach (int v in _mandateAtCap) h.Add(v);
        for (int c = 0; c < _civs; c++)
        {
            bool[]? r = _revealed[c];
            h.Add(r is not null);
            if (r is null) continue;
            int n = 0; foreach (bool b in r) if (b) n++;
            h.Add(n);
            for (int t = 0; t < r.Length; t++) if (r[t]) h.Add(t);
        }
        h.Add(_nextPartyId);
        foreach (int v in _e3Foundings) h.Add(v);
        h.Add(_foundingsSeen);
        HashTrade(ref h);
        HashInvestments(ref h);
    }

    private void WriteBronze(System.IO.BinaryWriter w)
    {
        // Expeditions' Bronze fields, by index into the list written above.
        int marked = 0;
        foreach (Expedition e in _expeditions) if (e.HasBronzeFields) marked++;
        w.Write(marked);
        for (int i = 0; i < _expeditions.Count; i++)
        {
            Expedition e = _expeditions[i];
            if (!e.HasBronzeFields) continue;
            w.Write(i); w.Write(e.Id); w.Write(e.TargetTile); w.Write(e.TargetParty); w.Write((byte)e.Good); w.Write(e.Cargo.Raw);
            w.Write(e.CompactId); w.Write(e.FolkSeller); w.Write(e.EscortCount);
            w.Write(e.Revealed.Count); foreach (int t in e.Revealed) w.Write(t);
        }
        w.Write(_decreeQueue.Count);
        foreach (PendingDecree d in _decreeQueue) { w.Write(d.Civ); w.Write((byte)d.Kind); w.Write(d.Target); w.Write(d.Extra); w.Write(d.Tick); w.Write((byte)d.Origin); }
        w.Write(_levies.Count);
        foreach (Levy l in _levies)
        {
            w.Write(l.Civ); w.Write(l.TargetSettlement); w.Write(l.SinceSeason); w.Write(l.UntilSeason);
            w.Write(l.Members.Count); for (int i = 0; i < l.Members.Count; i++) { w.Write(l.Members[i]); w.Write(l.Strengths[i].Raw); }
        }
        w.Write(_foundBoost.Count); foreach ((int s, int u) in _foundBoost) { w.Write(s); w.Write(u); }
        foreach (int v in _decreesDone) w.Write(v);
        foreach (int v in _decreesRefused) w.Write(v);
        foreach (int v in _mandateSeasons) w.Write(v);
        foreach (int v in _mandateAtCap) w.Write(v);
        for (int c = 0; c < _civs; c++)
        {
            bool[]? r = _revealed[c];
            w.Write(r is not null);
            if (r is null) continue;
            int n = 0; foreach (bool b in r) if (b) n++;
            w.Write(n);
            for (int t = 0; t < r.Length; t++) if (r[t]) w.Write(t);
        }
        w.Write(_nextPartyId);
        foreach (int v in _e3Foundings) w.Write(v);
        w.Write(_foundingsSeen);
        WriteTrade(w);
        WriteInvestments(w);
    }

    private void ReadBronze(System.IO.BinaryReader r)
    {
        int marked = r.ReadInt32();
        for (int k = 0; k < marked; k++)
        {
            int i = r.ReadInt32();
            if (unchecked((uint)i) >= unchecked((uint)_expeditions.Count)) throw new System.IO.InvalidDataException("Snapshot party fields point past the parties.");
            Expedition e = _expeditions[i];
            e.Id = r.ReadInt32(); e.TargetTile = r.ReadInt32(); e.TargetParty = r.ReadInt32(); e.Good = (TradeGood)r.ReadByte();
            e.Cargo = Fix64.FromRaw(r.ReadInt64()); e.CompactId = r.ReadInt32(); e.FolkSeller = r.ReadInt32();
            e.EscortCount = r.ReadInt32();
            e.Revealed.Clear();
            int n = r.ReadInt32();
            for (int t = 0; t < n; t++) e.Revealed.Add(r.ReadInt32());
        }
        _decreeQueue.Clear();
        int q = r.ReadInt32();
        for (int i = 0; i < q; i++)
            _decreeQueue.Add(new PendingDecree(r.ReadInt32(), (DecreeKind)r.ReadByte(), r.ReadInt32(), r.ReadInt32(), r.ReadInt64(), (DecreeOrigin)r.ReadByte()));
        _levies.Clear();
        int levies = r.ReadInt32();
        for (int i = 0; i < levies; i++)
        {
            var l = new Levy { Civ = r.ReadInt32(), TargetSettlement = r.ReadInt32(), SinceSeason = r.ReadInt32(), UntilSeason = r.ReadInt32() };
            int m = r.ReadInt32();
            for (int k = 0; k < m; k++) { l.Members.Add(r.ReadInt64()); l.Strengths.Add(Fix64.FromRaw(r.ReadInt64())); }
            _levies.Add(l);
        }
        _foundBoost.Clear();
        int boosts = r.ReadInt32();
        for (int i = 0; i < boosts; i++) _foundBoost.Add((r.ReadInt32(), r.ReadInt32()));
        for (int i = 0; i < _decreesDone.Length; i++) _decreesDone[i] = r.ReadInt32();
        for (int i = 0; i < _decreesRefused.Length; i++) _decreesRefused[i] = r.ReadInt32();
        for (int i = 0; i < _mandateSeasons.Length; i++) _mandateSeasons[i] = r.ReadInt32();
        for (int i = 0; i < _mandateAtCap.Length; i++) _mandateAtCap[i] = r.ReadInt32();
        for (int c = 0; c < _civs; c++)
        {
            _revealed[c] = null;
            if (!r.ReadBoolean()) continue;
            var map = new bool[_tileArray.Length];
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int t = r.ReadInt32();
                if (unchecked((uint)t) >= unchecked((uint)map.Length)) throw new System.IO.InvalidDataException("Snapshot reveals a tile off the map.");
                map[t] = true;
            }
            _revealed[c] = map;
        }
        _nextPartyId = r.ReadInt32();
        for (int i = 0; i < _e3Foundings.Length; i++) _e3Foundings[i] = r.ReadInt32();
        _foundingsSeen = r.ReadInt32();
        ReadTrade(r);
        ReadInvestments(r);
    }
}

/// <summary>A narrow read of research for Decree policies (Mandate, costs and what is done), so a policy needs no research reference of its own.</summary>
public readonly struct ResearchViewForDecrees
{
    private readonly Research.ResearchSystem _r;
    internal ResearchViewForDecrees(Research.ResearchSystem r) => _r = r;
    public int Mandate(int civ) => _r.Mandate(civ);
    public int MandateCap(int civ) => _r.MandateCap(civ);
    public bool MandateUnlocked(int civ) => _r.MandateUnlocked(civ);
    public bool IsDone(int civ, string projectId) { int p = _r.Catalog.IndexOf(projectId); return p >= 0 && _r.IsDone(civ, p); }
    public int Era(int civ) => _r.Era(civ);
    public long CompletedTick(int civ, string projectId) { int p = _r.Catalog.IndexOf(projectId); return p >= 0 && _r.IsDone(civ, p) ? _r.CompletedTick(civ, p) : -1; }
}
