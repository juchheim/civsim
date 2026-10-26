using System;
using System.Collections.Generic;
using System.IO;
using CivSim.Sim.Core;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.Politics;
using CivSim.Sim.Research;
using CivSim.Sim.World;

namespace CivSim.Sim.Ai;

/// <summary>
/// The belief builder (docs/MVP/14 §3.1, §5, §10.4): the only AI code that reads true state, and it reads another
/// civilization's state only through the sight rules below. Called once a season, after politics, for each AI
/// civilization. Everything the Spark and Strategist know about others comes out of the <see cref="Belief"/> it fills.
/// <para><b>What counts as seen.</b> Distances are hex tiles (terrain slows walking, not the eye, and hex distance is
/// cheap); r is the observer's routine reach of its era (13 §2.2), s = 3 a figure's sight (13 §2.1).</para>
/// <list type="number">
/// <item><b>Routine sight:</b> every tile within r + s of one of the observer's own settlements is explored and its
/// owner recorded this season (MapKnowledge). A rival settlement on such a tile is seen.</item>
/// <item><b>Scouts</b> (13 §2.3), once the observer is Aware of a rival: settlements within S + s are seen, where S is
/// the scout range (r; ×1.5 with WAY-01; ×2 with WAY-03). The tiles within s of a settlement seen this way are explored.</item>
/// <item><b>Visits:</b> a party of the observer's own (gift, barter, raid, assault) that reached a settlement this
/// season saw it. The builder remembers its own parties on the road for this, its only state.</item>
/// <item><b>Figures in the field:</b> two settlements whose people range into each other's sight, (r + r') × 1.1 + s
/// apart (the awareness rule of 13 §12.2), let the observer see the rival's figures: the best weapon they carry,
/// warriors' paint (WAR-04) and warpaint outfits (Raid posture, 14 §3.1).</item>
/// <item>Before the observer is Aware of a civilization nothing of it is seen. Tin Folk are remembered only after direct sight.</item>
/// </list>
/// <para><b>What a seen settlement shows:</b> its tile; head count and fighters times a noise factor uniform in
/// [0.75, 1.25] drawn from a stream keyed by match seed, observer, settlement and season; warriors (exactly: painted);
/// defenders' strength (same factor); thorn barrier, palisade, watchers; distress when its last food security was under
/// 0.9; and the structures standing there. A settlement out of sight keeps its last record (LastSeenSeason goes stale); a
/// settlement seen under a new owner, as Free Folk, or empty moves or leaves the list.</para>
/// <para><b>Public, no sight needed:</b> Aware, contact, Relations, reputation, war, alliance, truce (mutual knowledge);
/// a rival's era and Keystones (announced); its Firsts (the Chronicle); raids it made on us and gifts it sent us. Parties
/// heading for our settlements are seen once within r + s of the target or when they left from a settlement in sight;
/// a rival's raid or assault parties leaving a settlement in sight count as its warriors away.</para>
/// <para><b>Estimates:</b> the tech estimate (14 §5.2, <see cref="TechEstimate"/>), the archetype posterior (14 §5.3,
/// <see cref="ArchetypeModel"/>), threat and opportunity (14 §5.4, <see cref="ThreatModel"/>), confidence (decays 15% a
/// season unseen, <see cref="BeliefRules"/>).</para>
/// <para><b>Difficulty</b> (<see cref="DifficultyProfile.Opponent"/>): None keeps diplomacy, the public record and the map
/// only, with a uniform posterior and a diplomacy-only threat; ArchetypePosterior adds settlements (place, head count,
/// distress), the posterior and the public tech closure but no weapons, warriors, fighters, defenses or sighted tech;
/// Full and FullCoalition fill everything.</para>
/// </summary>
public sealed class BeliefBuilder : IBeliefBuilder
{
    private TechEstimate? _tech;
    private int _way01 = -1, _way03 = -1, _war02 = -1;

    /// <summary>Own parties on the road at the last update: (target settlement, arrival season). Saved and hashed.</summary>
    private readonly List<(int Settlement, int ArriveSeason)> _visits = new();

    // Scratch, rebuilt every update (never state).
    private readonly List<int> _own = new();
    private bool[] _seenNow = Array.Empty<bool>(), _visited = Array.Empty<bool>();
    private int[] _stamp = Array.Empty<int>();
    private int _stampValue;
    private int[] _claims = Array.Empty<int>(), _warriorsNow = Array.Empty<int>(), _firsts = Array.Empty<int>();
    private bool[] _figures = Array.Empty<bool>(), _rangedNow = Array.Empty<bool>(), _postureNow = Array.Empty<bool>();
    private Fix64[] _weaponNow = Array.Empty<Fix64>();
    private Fix64[] _ownDefense = Array.Empty<Fix64>(), _ownParty = Array.Empty<Fix64>();

    /// <summary>Live rival mining camps encountered by the most recent update.</summary>
    public int LastUpdateLiveRivalCamps { get; private set; }
    /// <summary>Live rival camps whose civilization was already known to the observer.</summary>
    public int LastUpdateAwareMiningCamps { get; private set; }
    /// <summary>Aware rival camps inside routine or scout sight.</summary>
    public int LastUpdateRangeMiningCamps { get; private set; }
    /// <summary>Aware rival camps whose tile owner was recorded during the current season.</summary>
    public int LastUpdateOwnerSeenMiningCamps { get; private set; }
    /// <summary>Rival mining camps newly recorded or refreshed during the most recent update.</summary>
    public int LastUpdateRecordedMiningCamps { get; private set; }

    /// <summary>The tech estimate built from the catalog (null until the first update).</summary>
    public TechEstimate? Tech => _tech;

    private void EnsureInit(TrueWorld world)
    {
        if (_tech is not null) return;
        ResearchCatalog catalog = world.Research.Catalog;
        _tech = new TechEstimate(catalog, world.Politics.WeaponLadder, world.Politics.ArcheryProject);
        _way01 = catalog.IndexOf("WAY-01");
        _way03 = catalog.IndexOf("WAY-03");
        _war02 = catalog.IndexOf("WAR-02");
    }

    private void EnsureBuffers(int settlements, int tiles, int civs)
    {
        if (_seenNow.Length < settlements)
        {
            int size = Math.Max(settlements, _seenNow.Length * 2);
            _seenNow = new bool[size];
            _visited = new bool[size];
        }
        if (_stamp.Length != tiles) _stamp = new int[tiles];
        if (_claims.Length != civs)
        {
            _claims = new int[civs]; _warriorsNow = new int[civs]; _firsts = new int[civs];
            _figures = new bool[civs]; _rangedNow = new bool[civs]; _postureNow = new bool[civs]; _weaponNow = new Fix64[civs];
        }
    }

    public void Update(Belief belief, TrueWorld world, DifficultyProfile profile)
    {
        EnsureInit(world);
        TechEstimate tech = _tech!;
        PoliticsSystem pol = world.Politics;
        ResearchSystem res = world.Research;
        LivingEconomyModel eco = world.Economy;
        Diplomacy dip = pol.Diplomacy;
        int civ = belief.Civ, season = pol.Season, civs = belief.Rivals.Length, total = pol.SettlementTotal;
        int width = belief.Map.Width;
        OpponentModel model = profile.Opponent;
        bool rivals = model != OpponentModel.None;
        bool details = model >= OpponentModel.Full;
        LastUpdateLiveRivalCamps = 0;
        LastUpdateAwareMiningCamps = 0;
        LastUpdateRangeMiningCamps = 0;
        LastUpdateOwnerSeenMiningCamps = 0;
        LastUpdateRecordedMiningCamps = 0;
        belief.Season = season;
        EnsureBuffers(total, belief.Map.TileCount, civs);
        // An expedition's report is sight (docs/MVP/22 §4.3): the tiles it revealed are explored.
        if (pol.HasRevealed(civ))
            for (int t = 0; t < belief.Map.TileCount; t++)
                if (!belief.Map.IsExplored(t) && pol.IsRevealed(civ, t)) belief.Map.MarkExplored(t);

        // ---- own settlements, sight ranges, own parties that arrived --------------------------------------
        _own.Clear();
        for (int s = 0; s < total; s++)
            if (pol.SettlementCivOf(s) == civ && !pol.IsFreeFolk(s)) _own.Add(s);
        int reach = pol.ReachOf(civ);
        int routine = BeliefRules.RoutineSight(reach);
        int scout = BeliefRules.ScoutRange(reach, IsDone(res, civ, _way01), IsDone(res, civ, _way03)) + BeliefRules.SightRadius;
        Array.Clear(_visited, 0, _visited.Length);
        foreach ((int target, int arrive) in _visits)
            if (arrive <= season && (target >= 0 && target < total)) _visited[target] = true;
        _visits.Clear();
        foreach (Expedition e in pol.Expeditions)
            if (e.Civ == civ) _visits.Add((e.ToSettlement, e.ArriveSeason));

        // ---- the map: routine sight around own settlements ---------------------------------------------------
        _stampValue = _stampValue == int.MaxValue ? 1 : _stampValue + 1;
        Array.Clear(_claims);
        foreach (int s in _own) SeeDisk(belief.Map, pol.Territory, pol.SettlementTileOf(s), routine, season, countClaims: true);

        // ---- public record and diplomacy ---------------------------------------------------------------------
        Array.Clear(_firsts);
        for (int p = 0; p < res.Catalog.Count; p++)
        {
            int first = res.FirstBy(p);
            if ((first >= 0 && first < civs)) _firsts[first]++;
        }
        Span<bool> wasContacted = stackalloc bool[civs], wasAtWar = stackalloc bool[civs], wasRanged = stackalloc bool[civs];
        Span<bool> wasPosture = stackalloc bool[civs];
        Span<int> wasEra = stackalloc int[civs], wasFirsts = stackalloc int[civs], wasEver = stackalloc int[civs];
        Span<int> wasFlags = stackalloc int[civs];
        Span<ulong> wasKinds = stackalloc ulong[civs];
        for (int o = 0; o < civs; o++)
        {
            if (o == civ) continue;
            RivalBelief r = belief.Rivals[o];
            wasContacted[o] = r.Contacted; wasAtWar[o] = r.AtWar; wasEra[o] = r.Era; wasFirsts[o] = r.FirstsAnnounced;
            wasEver[o] = r.SettlementsSeenEver; wasFlags[o] = r.SeenFlags; wasKinds[o] = r.StructureKindsSeen;
            wasRanged[o] = (r.SeenFlags & SeenFlag.RangedWeapon) != 0; wasPosture[o] = (r.SeenFlags & SeenFlag.RaidPostureLast) != 0;
            r.Aware = dip.Aware(civ, o);
            r.Contacted = dip.Contacted(civ, o);
            if (r.Contacted && !wasContacted[o]) r.FirstContactSeason = season;
            r.Relations = dip.Relations(civ, o);
            r.Reputation = dip.Reputation(o);
            r.AtWar = dip.AtWar(civ, o);
            r.Allied = dip.Allied(civ, o);
            r.InTruce = dip.InTruce(civ, o, season);
            r.Era = res.Era(o);
            int keystones = 0;
            for (int e = 0; e < ResearchCatalog.EraCount; e++)
            {
                int k = res.Catalog.KeystoneLeaving(e);
                if (k >= 0 && res.IsDone(o, k)) keystones++;
            }
            r.KeystonesAnnounced = keystones;
            r.FirstsAnnounced = _firsts[o];
            r.ClaimTilesSeen = _claims[o];
            r.PartiesAwaySeen = 0;
            _figures[o] = false; _warriorsNow[o] = 0; _rangedNow[o] = false; _postureNow[o] = false; _weaponNow[o] = Fix64.Zero;
        }

        // ---- settlements and figures in sight ---------------------------------------------------------------
        Array.Clear(_seenNow, 0, _seenNow.Length);
        if (rivals)
        {
            for (int s = 0; s < total; s++)
            {
                int owner = pol.SettlementCivOf(s);
                if (owner == civ && !pol.IsFreeFolk(s)) { Forget(belief, s, keep: -1); continue; }
                int d = NearestOwn(pol, pol.SettlementTileOf(s), width);
                bool free = pol.IsFreeFolk(s);
                bool aware = !free && (owner >= 0 && owner < civs) && belief.Rivals[owner].Aware;
                bool inSight = _visited[s] || d <= routine || (AnyAware(belief) && d <= scout);
                if (free || pol.SettlementPopulationOf(s) <= 0)
                {
                    if (inSight) Forget(belief, s, keep: -1); // seen empty or gone over to the Free Folk
                    continue;
                }
                if (!aware) continue;
                bool settlementSeen = _visited[s] || d <= routine || d <= scout;
                bool figuresSeen = settlementSeen || d <= BeliefRules.FigureSight(reach, pol.ReachOf(owner));
                if (!figuresSeen) continue;
                RivalBelief r = belief.Rivals[owner];
                SettlementMilitary m = pol.MilitaryOf(s);
                _figures[owner] = true;
                _warriorsNow[owner] = Math.Max(_warriorsNow[owner], m.Warriors);
                if (m.Warriors > 0) r.SeenFlags |= SeenFlag.Warriors;
                if (!settlementSeen) continue;
                _seenNow[s] = true;
                if (d > routine && !_visited[s])
                    SeeDisk(belief.Map, pol.Territory, pol.SettlementTileOf(s), BeliefRules.SightRadius, season, countClaims: false);
                Observe(belief, r, s, m, pol, eco, season, details);
            }
            for (int o = 0; o < civs; o++)
            {
                if (o == civ || !_figures[o]) continue;
                _weaponNow[o] = pol.WeaponOf(o, out bool ranged);
                _rangedNow[o] = ranged;
                _postureNow[o] = pol.PostureOf(o) == Posture.Raid;
                RivalBelief r = belief.Rivals[o];
                r.LastSeenSeason = season;
                if (ranged) r.SeenFlags |= SeenFlag.RangedWeapon;
                r.RaidPostureSeen = details && _postureNow[o];
                if (details)
                {
                    if (_weaponNow[o] > r.WeaponStrength) r.WeaponStrength = _weaponNow[o];
                    r.RangedSeen |= ranged;
                }
            }
        }

        // Tin Folk are not civilizations and never enter a RivalBelief. Remember only villages whose settlement tile
        // is visible now; their identity/location may persist, but no population or defense facts are stored.
        for (int s = 0; s < total; s++)
        {
            if (!pol.IsTinFolk(s)) continue;
            int tile = pol.SettlementTileOf(s);
            int d = NearestOwn(pol, tile, width);
            bool inSight = _visited[s] || d <= routine || (AnyAware(belief) && d <= scout);
            if (!inSight) continue;
            BeliefTinFolk? known = null;
            foreach (BeliefTinFolk folk in belief.TinFolkSeen)
                if (folk.Settlement == s) { known = folk; break; }
            if (known is null) belief.TinFolkSeen.Add(new BeliefTinFolk { Settlement = s, Tile = tile, LastSeenSeason = season });
            else { known.Tile = tile; known.LastSeenSeason = season; }
        }

        // ---- E3 mining camps in sight ------------------------------------------------------------------------
        // A camp is a visible rival fact for every opponent model that observes rivals. It must still pass awareness
        // and a local sight test; do not infer camps from remote economic state.
        for (int i = 0; i < eco.MiningCampCount; i++)
        {
            MiningCampView camp = eco.MiningCamp(i);
            if (camp.Civilization == civ || (uint)camp.Civilization >= (uint)civs) continue;
            LastUpdateLiveRivalCamps++;
            if (!rivals || !belief.Rivals[camp.Civilization].Aware) continue;
            LastUpdateAwareMiningCamps++;
            int d = NearestOwn(pol, camp.Tile, width);
            bool inLocalRange = d <= routine || d <= scout;
            if (inLocalRange) LastUpdateRangeMiningCamps++;
            bool tileSeenThisSeason = belief.Map.OwnerSeen(camp.Tile).Season == season;
            if (tileSeenThisSeason) LastUpdateOwnerSeenMiningCamps++;
            if (!inLocalRange && !tileSeenThisSeason) continue;

            RivalBelief rival = belief.Rivals[camp.Civilization];
            int flag = camp.Deposit switch
            {
                DepositKind.CopperOre => SeenFlag.CopperMineCamp,
                DepositKind.ArsenicalCopper => SeenFlag.ArsenicalMineCamp,
                DepositKind.Tin => SeenFlag.TinMineCamp,
                _ => 0,
            };
            if (flag != 0) rival.SeenFlags |= flag;
            rival.LastSeenSeason = season;
            int knownIndex = -1;
            for (int k = 0; k < rival.MiningCampsSeen.Count; k++)
                if (rival.MiningCampsSeen[k].Id == camp.Id) { knownIndex = k; break; }
            var sighting = new BeliefMiningCamp
            {
                Id = camp.Id, Tile = camp.Tile, LastSeenSeason = season, CrewEstimate = camp.Crew, Deposit = camp.Deposit,
            };
            if (knownIndex >= 0) rival.MiningCampsSeen[knownIndex] = sighting;
            else rival.MiningCampsSeen.Add(sighting);
            LastUpdateRecordedMiningCamps++;
        }

        // ---- parties on the road ----------------------------------------------------------------------------
        belief.IncomingParties.Clear();
        foreach (Expedition e in pol.Expeditions)
        {
            if (e.Civ == civ || e.Civ < 0 || e.Civ >= civs || !dip.Aware(civ, e.Civ)) continue;
            if (rivals && e.Kind == ExpeditionKind.Caravan)
            {
                int tile = pol.PartyTile(e);
                Fix64 barterValue = e.Good == TradeGood.Food ? Fix64.One : eco.GoodBarterValue(DecreeArgs.GoodId(e.Good));
                RecordVisibleCaravan(belief.Rivals[e.Civ], e, tile, season, pol.KnowsTile(civ, tile), barterValue);
            }
            bool originSeen = (e.FromSettlement >= 0 && e.FromSettlement < total) && _seenNow[e.FromSettlement];
            bool targetOwn = (e.ToSettlement >= 0 && e.ToSettlement < total) && pol.SettlementCivOf(e.ToSettlement) == civ && !pol.IsFreeFolk(e.ToSettlement);
            if (targetOwn)
            {
                int remaining = (e.ArriveSeason - season) * PoliticsSystem.TilesPerSeason;
                if (originSeen || remaining <= routine)
                    belief.IncomingParties.Add(new IncomingParty
                    {
                        Civ = e.Civ, Kind = e.Kind, FromSettlement = e.FromSettlement, TargetSettlement = e.ToSettlement,
                        ArriveSeason = e.ArriveSeason, PartySize = e.Members.Count,
                    });
            }
            else if (rivals && originSeen && e.Kind is ExpeditionKind.Raid or ExpeditionKind.Assault)
                belief.Rivals[e.Civ].PartiesAwaySeen += e.Members.Count;
        }

        // ---- estimates --------------------------------------------------------------------------------------
        bool ownComputed = false;
        Fix64 ownWeapon = Fix64.Zero;
        int ownEra = res.Era(civ);
        for (int o = 0; o < civs; o++)
        {
            if (o == civ) continue;
            RivalBelief r = belief.Rivals[o];
            int raidBy = dip.LastRaidBy(o, civ);
            bool raidedUs = raidBy > r.LastRaidOnUsSeason && raidBy > -1000;
            if (raidedUs) { r.RaidsOnUs++; r.LastRaidOnUsSeason = raidBy; }
            int giftBy = dip.LastGift(o, civ);
            bool gift = giftBy > r.LastGiftFromSeason && giftBy > -1000;
            if (gift) r.LastGiftFromSeason = giftBy;

            if (!rivals)
            {
                ArchetypeModel.Reset(r.ArchetypePosterior);
                r.Confidence = Fix64.Zero;
            }
            else
            {
                if (ArchetypeModel.IsUnset(r.ArchetypePosterior)) ArchetypeModel.Reset(r.ArchetypePosterior);
                if (r.Aware)
                {
                    Fix64[] post = r.ArchetypePosterior;
                    if (r.SettlementsSeenEver > wasEver[o] && wasEver[o] > 0) ArchetypeModel.Update(post, BeliefEvidence.NewSettlement);
                    if (r.ClaimTilesSeen >= r.ClaimTilesSeenMax + 5) ArchetypeModel.Update(post, BeliefEvidence.ClaimsAdvance);
                    ulong newKinds = r.StructureKindsSeen & ~wasKinds[o];
                    for (int k = 0; k < LivingEconomyModel.StructureKindCount; k++)
                        if ((newKinds & (1UL << k)) != 0 && tech.IsAdvancedStructure(k)) { ArchetypeModel.Update(post, BeliefEvidence.NewCraftStructure); break; }
                    if (_warriorsNow[o] > r.WarriorsSeenMax) ArchetypeModel.Update(post, BeliefEvidence.WarriorsSeen);
                    if (_postureNow[o] && !wasPosture[o]) ArchetypeModel.Update(post, BeliefEvidence.RaidPosture);
                    if (raidedUs) ArchetypeModel.Update(post, BeliefEvidence.RaidOnUs);
                    if (r.AtWar && !wasAtWar[o]) ArchetypeModel.Update(post, BeliefEvidence.WarOnUs);
                    if (r.Contacted && !wasContacted[o])
                    {
                        if (r.Relations < 0) ArchetypeModel.Update(post, BeliefEvidence.HostileContact);
                        else if (r.Relations > 0) ArchetypeModel.Update(post, BeliefEvidence.FriendlyContact);
                    }
                    if (gift) ArchetypeModel.Update(post, BeliefEvidence.GiftFromThem);
                    if (r.FirstsAnnounced > wasFirsts[o]) ArchetypeModel.Update(post, BeliefEvidence.FirstAnnounced);
                    if (r.Era > wasEra[o] && r.Era > ownEra) ArchetypeModel.Update(post, BeliefEvidence.EraAhead);
                    int newDefense = r.SeenFlags & ~wasFlags[o] & (SeenFlag.ThornBarrier | SeenFlag.Palisade | SeenFlag.Hillfort | SeenFlag.StoneWalls);
                    if (newDefense != 0) ArchetypeModel.Update(post, BeliefEvidence.DefenseSeen);
                    if (_rangedNow[o] && !wasRanged[o]) ArchetypeModel.Update(post, BeliefEvidence.RangedWeapon);
                }
                // Warpaint on the latest sighting, kept below Full too so the next season can tell a new signal from an old one.
                if (_figures[o]) r.SeenFlags = _postureNow[o] ? r.SeenFlags | SeenFlag.RaidPostureLast : r.SeenFlags & ~SeenFlag.RaidPostureLast;
                r.ClaimTilesSeenMax = Math.Max(r.ClaimTilesSeenMax, r.ClaimTilesSeen);
                r.WarriorsSeenMax = Math.Max(r.WarriorsSeenMax, _warriorsNow[o]);

                // Tech estimate: the public record always, sightings at Full.
                tech.AddEra(r.Era, r.ProjectBelieved);
                for (int p = 0; p < res.Catalog.Count; p++) if (res.FirstBy(p) == o) tech.AddProject(p, r.ProjectBelieved);
                if (details)
                {
                    if (r.WeaponStrength > Fix64.FromRatio(6, 10)) tech.AddWeapon(r.WeaponStrength, r.RangedSeen, r.ProjectBelieved);
                    if ((r.SeenFlags & SeenFlag.Warriors) != 0) tech.AddWarriors(r.ProjectBelieved);
                    if ((r.SeenFlags & SeenFlag.Watchers) != 0) tech.AddWatchers(r.ProjectBelieved);
                    if ((r.SeenFlags & SeenFlag.Palisade) != 0) tech.AddPalisade(r.ProjectBelieved);
                    AddVisibleBronzeEvidence(tech, r, visible: true);
                    for (int k = 0; k < LivingEconomyModel.StructureKindCount; k++)
                        if ((r.StructureKindsSeen & (1UL << k)) != 0) tech.AddStructure(k, r.ProjectBelieved);
                }

                int sum = 0, warriors = 0;
                foreach (BeliefSettlement b in r.Settlements) { sum += b.PopulationEstimate; warriors += b.WarriorsSeen; }
                r.PopulationLow = sum * 4 / 5;
                r.PopulationHigh = (sum * 4 + 2) / 3;
                r.WarriorsEstimate = details ? warriors : 0;
                r.WarriorsSeen = details && (r.SeenFlags & SeenFlag.Warriors) != 0;
                r.Confidence = BeliefRules.Confidence(r.LastSeenSeason < 0 ? -1 : season - r.LastSeenSeason);
            }

            if (!r.Aware) { r.ThreatProbability = r.ThreatLoss = r.Opportunity = Fix64.Zero; continue; }
            if (!ownComputed) { ownWeapon = ComputeOwn(pol, civ); ownComputed = true; }
            Estimate(r, pol, season, details, rivals, ownWeapon, ownEra, width);
        }
    }

    private static bool IsDone(ResearchSystem res, int civ, int project) => project >= 0 && res.IsDone(civ, project);

    /// <summary>Applies E3 tech evidence only after a sighted observation; shared by live update and fog tests.</summary>
    internal static void AddVisibleBronzeEvidence(TechEstimate tech, RivalBelief rival, bool visible)
    {
        if (!visible) return;
        tech.AddBronzeDefense((rival.SeenFlags & SeenFlag.Hillfort) != 0,
            (rival.SeenFlags & SeenFlag.StoneWalls) != 0, rival.ProjectBelieved);
        tech.AddMetalKits((rival.SeenFlags & SeenFlag.CopperKit) != 0,
            (rival.SeenFlags & SeenFlag.ArsenicalKit) != 0, (rival.SeenFlags & SeenFlag.TinBronzeKit) != 0, rival.ProjectBelieved);
        if ((rival.SeenFlags & SeenFlag.CopperMineCamp) != 0) tech.AddMiningCamp(DepositKind.CopperOre, rival.ProjectBelieved);
        if ((rival.SeenFlags & SeenFlag.ArsenicalMineCamp) != 0) tech.AddMiningCamp(DepositKind.ArsenicalCopper, rival.ProjectBelieved);
        if ((rival.SeenFlags & SeenFlag.TinMineCamp) != 0) tech.AddMiningCamp(DepositKind.Tin, rival.ProjectBelieved);
        tech.AddCivicRoles((rival.SeenFlags & SeenFlag.Scribes) != 0, (rival.SeenFlags & SeenFlag.Priests) != 0, rival.ProjectBelieved);
    }

    /// <summary>Stores a caravan observation only when its current tile is in the observer's known sight.</summary>
    internal static bool RecordVisibleCaravan(RivalBelief rival, Expedition expedition, int tile, int season, bool visible,
        Fix64 cargoUnitValue = default)
    {
        if (!visible || expedition.Kind != ExpeditionKind.Caravan || expedition.Civ != rival.Civ) return false;
        int index = -1;
        for (int i = 0; i < rival.CaravansSeen.Count; i++)
            if (rival.CaravansSeen[i].Id == expedition.Id) { index = i; break; }
        var sighting = new BeliefCaravan
        {
            Civ = expedition.Civ, Id = expedition.Id, Tile = tile, LastSeenSeason = season,
            CompactId = expedition.CompactId, Good = expedition.Good, Cargo = expedition.Cargo,
            ArrivalSeason = expedition.ArriveSeason, CrewEstimate = expedition.Members.Count,
            CargoBarterValue = expedition.Cargo * cargoUnitValue,
        };
        if (index >= 0) rival.CaravansSeen[index] = sighting;
        else rival.CaravansSeen.Add(sighting);
        if (expedition.Good == TradeGood.Tin) rival.TinAccessSeen = true;
        return true;
    }

    private static bool AnyAware(Belief belief)
    {
        foreach (RivalBelief r in belief.Rivals) if (r.Civ != belief.Civ && r.Aware) return true;
        return false;
    }

    private int NearestOwn(PoliticsSystem pol, int tile, int width)
    {
        int best = int.MaxValue;
        foreach (int s in _own) best = Math.Min(best, HexGrid.Distance(pol.SettlementTileOf(s), tile, width));
        return best;
    }

    /// <summary>Explores the tiles within <paramref name="radius"/> of <paramref name="center"/> and records who owns them now.</summary>
    private void SeeDisk(MapKnowledge map, Territory territory, int center, int radius, int season, bool countClaims)
    {
        int width = map.Width, height = map.Height;
        int x = center % width, y = center / width;
        for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
            for (int nx = Math.Max(0, x - radius - 1); nx <= Math.Min(width - 1, x + radius + 1); nx++)
            {
                int tile = ny * width + nx;
                if (_stamp[tile] == _stampValue || HexGrid.Distance(center, tile, width) > radius) continue;
                _stamp[tile] = _stampValue;
                int owner = territory.OwnerOf(tile);
                map.MarkExplored(tile);
                map.RecordOwner(tile, owner, season);
                if (countClaims && (owner >= 0 && owner < _claims.Length)) _claims[owner]++;
            }
    }

    /// <summary>Removes a settlement from every rival's list except <paramref name="keep"/>'s.</summary>
    private static void Forget(Belief belief, int settlement, int keep)
    {
        foreach (RivalBelief r in belief.Rivals)
        {
            if (r.Civ == keep) continue;
            for (int i = r.Settlements.Count - 1; i >= 0; i--)
                if (r.Settlements[i].Settlement == settlement) r.Settlements.RemoveAt(i);
        }
    }

    private static void Observe(Belief belief, RivalBelief r, int s, SettlementMilitary m, PoliticsSystem pol,
        LivingEconomyModel eco, int season, bool details)
    {
        Forget(belief, s, keep: r.Civ);
        BeliefSettlement? b = null;
        int insert = r.Settlements.Count;
        for (int i = 0; i < r.Settlements.Count; i++)
        {
            if (r.Settlements[i].Settlement == s) { b = r.Settlements[i]; break; }
            if (r.Settlements[i].Settlement > s) { insert = i; break; }
        }
        if (b is null)
        {
            b = new BeliefSettlement { Settlement = s, FirstSeenSeason = season };
            r.Settlements.Insert(insert, b);
            r.SettlementsSeenEver++;
        }
        Fix64 factor = BeliefRules.NoiseFactor(pol.MatchSeed, belief.Civ, s, season);
        b.Tile = pol.SettlementTileOf(s);
        b.LastSeenSeason = season;
        r.LastSeenSeason = season;
        b.PopulationEstimate = BeliefRules.Estimate(pol.SettlementPopulationOf(s), factor);
        b.DistressSeen = eco.SettlementLite(s).LastFoodSecurity < BeliefRules.DistressFoodSecurity;
        if (m.Defenses.ThornBarrier) r.SeenFlags |= SeenFlag.ThornBarrier;
        if (m.Defenses.Palisade) r.SeenFlags |= SeenFlag.Palisade;
        if (m.Defenses.Watchers) r.SeenFlags |= SeenFlag.Watchers;
        if (m.Defenses.Hillfort) r.SeenFlags |= SeenFlag.Hillfort;
        if (m.Defenses.StoneWalls) r.SeenFlags |= SeenFlag.StoneWalls;
        for (int k = 0; k < LivingEconomyModel.StructureKindCount; k++)
            if (eco.StructureCount(s, k) > 0) r.StructureKindsSeen |= 1UL << k;
        if (details)
        {
            MetalReadout metal = eco.Metal(s);
            if (metal.Kits8 > 0) r.SeenFlags |= SeenFlag.CopperKit;
            if (metal.Kits9 > 0) r.SeenFlags |= SeenFlag.ArsenicalKit;
            if (metal.Kits10 > 0) r.SeenFlags |= SeenFlag.TinBronzeKit;
            (int priests, int scribes) = eco.RoleHeads(s);
            if (scribes > 0) r.SeenFlags |= SeenFlag.Scribes;
            if (priests > 0) r.SeenFlags |= SeenFlag.Priests;
            b.FightersEstimate = BeliefRules.Estimate(m.Fighters, factor);
            b.WarriorsSeen = m.Warriors;
            b.DefendingStrengthEstimate = m.DefendingStrength * factor;
            b.ThornBarrierSeen = m.Defenses.ThornBarrier;
            b.PalisadeSeen = m.Defenses.Palisade;
            b.WatchersSeen = m.Defenses.Watchers;
            b.HillfortSeen = m.Defenses.Hillfort;
            b.StoneWallsSeen = m.Defenses.StoneWalls;
        }
        else
        {
            b.FightersEstimate = 0;
            b.WarriorsSeen = 0;
            b.DefendingStrengthEstimate = Fix64.Zero;
            b.ThornBarrierSeen = b.PalisadeSeen = b.WatchersSeen = b.HillfortSeen = b.StoneWallsSeen = false;
        }
    }

    /// <summary>Our own settlements' defense and raid party (own state is exact, 14 §3.1). Returns our best weapon.</summary>
    private Fix64 ComputeOwn(PoliticsSystem pol, int civ)
    {
        if (_ownDefense.Length < _own.Count) { _ownDefense = new Fix64[_own.Count * 2]; _ownParty = new Fix64[_own.Count * 2]; }
        for (int i = 0; i < _own.Count; i++)
        {
            SettlementMilitary m = pol.MilitaryOf(_own[i]);
            _ownDefense[i] = m.DefendingStrength * m.Defenses.Multiplier();
            _ownParty[i] = m.AttackingStrength * ThreatModel.RaidPartyShare;
        }
        return pol.WeaponOf(civ, out _);
    }

    private int NearestOwnIndex(PoliticsSystem pol, int tile, int width, out int distance)
    {
        distance = int.MaxValue;
        int best = -1;
        for (int i = 0; i < _own.Count; i++)
        {
            int d = HexGrid.Distance(pol.SettlementTileOf(_own[i]), tile, width);
            if (d < distance) { distance = d; best = i; }
        }
        return best;
    }

    /// <summary>Threat and opportunity for one rival the observer is Aware of (14 §5.4).</summary>
    private void Estimate(RivalBelief r, PoliticsSystem pol, int season, bool details, bool rivals, Fix64 ownWeapon, int ownEra, int width)
    {
        int capability = _war02 >= 0 && r.ProjectBelieved.Length > _war02 && r.ProjectBelieved[_war02] ? 2 : r.Era >= 1 ? 1 : 0;
        bool distress = false;
        foreach (BeliefSettlement b in r.Settlements) distress |= b.DistressSeen;

        // The nearest believed settlement is where an attack would come from.
        BeliefSettlement? near = null;
        int distance = int.MaxValue, ownIndex = -1;
        foreach (BeliefSettlement b in r.Settlements)
        {
            int i = NearestOwnIndex(pol, b.Tile, width, out int d);
            if (d < distance) { distance = d; near = b; ownIndex = i; }
        }
        var inputs = new ThreatInputs(r.Relations, r.AtWar, r.Allied, r.InTruce, capability, details ? r.WarriorsEstimate : 0,
            details && r.RaidPostureSeen, distress, season - r.LastRaidOnUsSeason <= ThreatModel.RecentRaidSeasons,
            distance, pol.ReachOfEra(r.Era));
        r.ThreatProbability = ThreatModel.Probability(inputs, r.ArchetypePosterior);

        Fix64 party = near is null
            ? ThreatModel.PartyStrength(0, 0, r.WeaponStrength)
            : ThreatModel.PartyStrength(near.FightersEstimate > 0 ? near.FightersEstimate : near.PopulationEstimate / 2, near.WarriorsSeen, r.WeaponStrength);
        Fix64 ownDefense = Fix64.Zero;
        if (ownIndex >= 0) ownDefense = _ownDefense[ownIndex];
        else
            for (int i = 0; i < _own.Count; i++)
                if (i == 0 || _ownDefense[i] < ownDefense) ownDefense = _ownDefense[i];
        r.ThreatLoss = ThreatModel.Loss(party, ownDefense);

        Fix64 best = Fix64.Zero;
        if (rivals)
            foreach (BeliefSettlement b in r.Settlements)
            {
                int i = NearestOwnIndex(pol, b.Tile, width, out _);
                if (i < 0) continue;
                Fix64 defense = details && b.DefendingStrengthEstimate > Fix64.Zero
                    ? b.DefendingStrengthEstimate * new Defenses(b.ThornBarrierSeen, b.PalisadeSeen, b.WatchersSeen, false, false, false,
                        false, false, false, false).Multiplier()
                    : Fix64.FromInt(b.PopulationEstimate) * ThreatModel.UnknownFighterShare * ThreatModel.DefenderBase * r.WeaponStrength
                      * Defenses.None.Multiplier();
                var o = new OpportunityInputs(r.Contacted, r.Allied, r.InTruce, r.AtWar, _ownParty[i], defense, b.DistressSeen,
                    r.PartiesAwaySeen > 0, details && ownWeapon > r.WeaponStrength && r.Era >= ownEra, r.Confidence);
                Fix64 v = ThreatModel.Opportunity(o);
                if (v > best) best = v;
            }
        r.Opportunity = best;
    }

    // ---- state ----------------------------------------------------------------------------------------------

    public void Hash(ref StateHasher hasher)
    {
        hasher.Add(_visits.Count);
        foreach ((int s, int a) in _visits) { hasher.Add(s); hasher.Add(a); }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(_visits.Count);
        foreach ((int s, int a) in _visits) { writer.Write(s); writer.Write(a); }
    }

    public void Read(BinaryReader reader)
    {
        _visits.Clear();
        int n = reader.ReadInt32();
        for (int i = 0; i < n; i++) _visits.Add((reader.ReadInt32(), reader.ReadInt32()));
    }

    // ---- sight queries for tests and tools ------------------------------------------------------------------

    /// <summary>
    /// How many tiles a settlement lies beyond the observer's routine and scout sight now (zero or less: in sight), ignoring
    /// awareness and visits. int.MaxValue when the observer has no settlement.
    /// </summary>
    public static int TilesOutOfSight(TrueWorld world, int observer, int settlement)
    {
        PoliticsSystem pol = world.Politics;
        ResearchCatalog c = world.Research.Catalog;
        int reach = pol.ReachOf(observer);
        int range = Math.Max(BeliefRules.RoutineSight(reach),
            BeliefRules.ScoutRange(reach, IsDone(world.Research, observer, c.IndexOf("WAY-01")), IsDone(world.Research, observer, c.IndexOf("WAY-03")))
            + BeliefRules.SightRadius);
        int d = MinDistance(pol, observer, pol.SettlementTileOf(settlement));
        return d == int.MaxValue ? d : d - range;
    }

    /// <summary>How many tiles the nearest settlements of observer and rival lie beyond figure sight (zero or less: figures seen).</summary>
    public static int TilesOutOfFigureSight(TrueWorld world, int observer, int rival)
    {
        PoliticsSystem pol = world.Politics;
        int best = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
            if (pol.SettlementCivOf(s) == rival && !pol.IsFreeFolk(s) && pol.SettlementPopulationOf(s) > 0)
                best = Math.Min(best, MinDistance(pol, observer, pol.SettlementTileOf(s)));
        return best == int.MaxValue ? best : best - BeliefRules.FigureSight(pol.ReachOf(observer), pol.ReachOf(rival));
    }

    private static int MinDistance(PoliticsSystem pol, int civ, int tile)
    {
        int best = int.MaxValue;
        for (int s = 0; s < pol.SettlementTotal; s++)
            if (pol.SettlementCivOf(s) == civ && !pol.IsFreeFolk(s))
                best = Math.Min(best, HexGrid.Distance(pol.SettlementTileOf(s), tile, pol.Territory.Width));
        return best;
    }
}
