using System;

namespace CivSim.Sim.View;

/// <summary>
/// One snapshot of the match as a viewer may see it (docs/MVP/18 §3): plain structure-of-arrays buffers of integers that
/// <see cref="FrameBuilder.Build"/> refills in place. Arrays only grow, so after the first frames no allocation happens.
/// Each block has a count; entries past the count are stale. A frame is written by one thread and, once handed over
/// through <see cref="FrameStore"/>, only read.
/// <para>Units: positions are map units (<see cref="HexLayout.Unit"/> per tile column), shares are permille, Fix64 values are
/// raw Q32.32 longs (divide by 2^32 to display), bands are the enums in ViewTypes.cs. Ids: figures by economy figure id,
/// settlements by economy settlement index (stable for the match), herds by herd id, projects by dense pack index.</para>
/// </summary>
public sealed class MatchFrame
{
    /// <summary>Kinds of structure the economy builds; the stride of <see cref="SettlementStructures"/>.</summary>
    public static int StructureKinds => Economy.LivingEconomyModel.StructureKindCount;

    // ---- header -------------------------------------------------------------------------------
    /// <summary>Build number from the builder that wrote it (1, 2, 3, ...).</summary>
    public long Sequence;
    /// <summary>Ticks simulated: the frame shows the state after ticks 0 .. Tick-1 ran. Interpolate by it (10 ticks = 1 s at 1×).</summary>
    public long Tick;
    /// <summary>Seasons completed since the start (150 ticks each).</summary>
    public int Season;
    /// <summary>0 spring, 1 summer, 2 autumn, 3 winter.</summary>
    public int SeasonOfYear;
    public int Year;
    /// <summary>0 to 149: how far into the season (the palette can blend toward the next season with it).</summary>
    public int TickInSeason;
    public bool LastWinterHarsh;
    /// <summary>The civilization this frame is filtered for; -1 omniscient.</summary>
    public int ViewerCiv;
    public int Width, Height;
    /// <summary>All figures alive, seen or not (a debug and stats number; the HUD must not show it for rivals).</summary>
    public int TotalFigures;

    // ---- tiles (Width × Height) ---------------------------------------------------------------
    /// <summary><see cref="TileVisibility"/> per tile.</summary>
    public byte[] Visibility = Array.Empty<byte>();
    /// <summary>Owning civilization (-1 none). Visible tiles: now; explored: as last seen; unknown: -1.</summary>
    public sbyte[] Owner = Array.Empty<sbyte>();
    /// <summary>The strongest civilization claim on a visible tile in hundredths (70 is the ownership threshold), saturating at 255.</summary>
    public byte[] Claim = Array.Empty<byte>();
    /// <summary>The civilization holding that strongest claim (-1 none).</summary>
    public sbyte[] Claimant = Array.Empty<sbyte>();
    /// <summary>Second strongest claim and its civilization; visible tiles only, ties favor the lower civilization id.</summary>
    public byte[] Claim2 = Array.Empty<byte>();
    public sbyte[] Claimant2 = Array.Empty<sbyte>();
    /// <summary>Third strongest claim and its civilization; visible tiles only.</summary>
    public byte[] Claim3 = Array.Empty<byte>();
    public sbyte[] Claimant3 = Array.Empty<sbyte>();
    /// <summary><see cref="TileViewFlags"/> per tile.</summary>
    public byte[] TileFlags = Array.Empty<byte>();
    /// <summary>1 when the viewer has discovered a deposit on this tile; 0 otherwise. Read WorldView.Deposit only when set.</summary>
    public byte[] KnownDeposits = Array.Empty<byte>();
    /// <summary>Standing forest stock band (0–3) on visible tiles; zero elsewhere. See <see cref="Economy.LivingEconomyModel.ForestBand"/>.</summary>
    public byte[] ForestBand = Array.Empty<byte>();
    /// <summary>1 when ore from the tile has been mined; current sight is required to reveal it.</summary>
    public byte[] DepositWorked = Array.Empty<byte>();
    /// <summary>1 when a known tile is within the viewer's current mining reach (or any civilization in omniscient mode).</summary>
    public byte[] MiningReach = Array.Empty<byte>();

    // ---- figures (ascending id) ---------------------------------------------------------------
    public int FigureCount;
    public long[] FigureId = Array.Empty<long>();
    public sbyte[] FigureCiv = Array.Empty<sbyte>();
    public int[] FigureSettlement = Array.Empty<int>();
    /// <summary>The tile the figure stands on (nearest centre).</summary>
    public int[] FigureTile = Array.Empty<int>();
    public int[] FigureX = Array.Empty<int>();
    public int[] FigureY = Array.Empty<int>();
    /// <summary><see cref="FigureActivity"/>.</summary>
    public byte[] FigureActivity = Array.Empty<byte>();
    /// <summary>Current specialist role: 1 Priest, 2 Scribe, 0 none.</summary>
    public byte[] FigureRole = Array.Empty<byte>();
    /// <summary><see cref="AgeBand"/>.</summary>
    public byte[] FigureAge = Array.Empty<byte>();
    /// <summary><see cref="HealthBand"/>.</summary>
    public byte[] FigureHealth = Array.Empty<byte>();
    /// <summary><see cref="FigureFlags"/>.</summary>
    public ushort[] FigureFlags = Array.Empty<ushort>();
    /// <summary>Best method rung for the figure's job at its settlement (-1 none): the held-tool sprite and tint.</summary>
    public sbyte[] FigureToolRung = Array.Empty<sbyte>();
    /// <summary>Highest researched method rung for its job, even when a tool or crew requirement blocks it.</summary>
    public sbyte[] FigureKnownToolRung = Array.Empty<sbyte>();
    /// <summary>Effective method output as permille of the highest researched method (1000 means no fallback).</summary>
    public short[] FigureMethodEfficiencyPermille = Array.Empty<short>();
    /// <summary><see cref="Economy.MethodFallbackReason"/> for a better researched method that is currently blocked.</summary>
    public byte[] FigureMethodFallback = Array.Empty<byte>();
    /// <summary><see cref="Economy.MethodToolId"/> required by the blocked method, or None.</summary>
    public byte[] FigureMethodFallbackTool = Array.Empty<byte>();
    /// <summary><see cref="CarryKind"/>.</summary>
    public byte[] FigureCarry = Array.Empty<byte>();

    // ---- wild herds (ascending id) ------------------------------------------------------------
    public int HerdCount;
    public int[] HerdId = Array.Empty<int>();
    /// <summary><see cref="World.HerdSpecies"/>.</summary>
    public byte[] HerdSpecies = Array.Empty<byte>();
    public int[] HerdTile = Array.Empty<int>();
    public int[] HerdX = Array.Empty<int>();
    public int[] HerdY = Array.Empty<int>();
    /// <summary>Animals in the herd (whole).</summary>
    public int[] HerdAnimals = Array.Empty<int>();
    /// <summary>Fear of people, 0 to 255 (skittish herds keep their distance).</summary>
    public byte[] HerdFear = Array.Empty<byte>();
    /// <summary>1 when the herd is away from its home range (seasonal migration).</summary>
    public byte[] HerdAway = Array.Empty<byte>();

    // ---- predators (static packs from world generation) ---------------------------------------
    public int PredatorCount;
    public int[] PredatorTile = Array.Empty<int>();
    /// <summary><see cref="World.PredatorSpecies"/>.</summary>
    public byte[] PredatorSpecies = Array.Empty<byte>();
    public byte[] PredatorSize = Array.Empty<byte>();

    // ---- settlements (ascending index) --------------------------------------------------------
    public int SettlementCount;
    /// <summary>Economy settlement index: the id used by events, figures, parties and occupations.</summary>
    public int[] SettlementIndex = Array.Empty<int>();
    public sbyte[] SettlementCiv = Array.Empty<sbyte>();
    public int[] SettlementTile = Array.Empty<int>();
    public int[] SettlementPopulation = Array.Empty<int>();
    /// <summary><see cref="FoodBand"/> of last season's food security.</summary>
    public byte[] SettlementFood = Array.Empty<byte>();
    /// <summary><see cref="FoodBand"/> of the late-winter forecast (made each autumn).</summary>
    public byte[] SettlementWinterForecast = Array.Empty<byte>();
    /// <summary><see cref="CohesionBand"/>.</summary>
    public byte[] SettlementCohesion = Array.Empty<byte>();
    /// <summary>Current target penalty from distance to the capital, in Cohesion points; own settlements only.</summary>
    public short[] SettlementOverreach = Array.Empty<short>();
    /// <summary>Current target penalty from settlements beyond Admin span, in Cohesion points; own settlements only.</summary>
    public short[] SettlementSpanPenalty = Array.Empty<short>();
    /// <summary><see cref="SettlementViewFlags"/>.</summary>
    public ushort[] SettlementFlags = Array.Empty<ushort>();
    /// <summary>Structure kind under construction, -1 none.</summary>
    public sbyte[] SettlementBuildKind = Array.Empty<sbyte>();
    /// <summary>Its labor done, permille.</summary>
    public short[] SettlementBuildPermille = Array.Empty<short>();
    /// <summary>Domestic small stock, cattle and pigs (whole animals).</summary>
    public int[] SettlementSmallStock = Array.Empty<int>(), SettlementCattle = Array.Empty<int>(), SettlementPigs = Array.Empty<int>();
    /// <summary>The season the viewer last saw it (the current season when visible).</summary>
    public int[] SettlementSeenSeason = Array.Empty<int>();
    /// <summary>Structure counts, <see cref="StructureKinds"/> per settlement: [i × StructureKinds + kind] (kind names: LivingEconomyModel.StructureName). Zero for remembered settlements.</summary>
    public byte[] SettlementStructures = Array.Empty<byte>();
    /// <summary>Halls standing at each settlement (a civic dwelling outside <see cref="SettlementStructures"/>). Zero for remembered settlements.</summary>
    public byte[] SettlementHalls = Array.Empty<byte>();
    /// <summary>Ziggurats standing at each settlement (BLD-08 Wonder), and the permille of the next one raised while it is under way. Zero for remembered settlements.</summary>
    public byte[] SettlementZiggurats = Array.Empty<byte>();
    public short[] SettlementZigguratPermille = Array.Empty<short>();

    // ---- fields and orchards ------------------------------------------------------------------
    public int PlotCount;
    public int[] PlotTile = Array.Empty<int>();
    public int[] PlotSettlement = Array.Empty<int>();
    /// <summary>0 field, 1 orchard.</summary>
    public byte[] PlotKind = Array.Empty<byte>();

    // ---- camps and Beacons --------------------------------------------------------------------
    public int CampCount;
    public int[] CampTile = Array.Empty<int>();
    /// <summary>Stable mining camp id, or -1 for seasonal camps.</summary>
    public int[] CampId = Array.Empty<int>();
    public sbyte[] CampCiv = Array.Empty<sbyte>();
    /// <summary><see cref="CampKind"/>; seasonal politics camps are 0, mining camps are 1.</summary>
    public byte[] CampKind = Array.Empty<byte>();
    /// <summary>Parent settlement index, or -1 when none.</summary>
    public int[] CampParent = Array.Empty<int>();
    /// <summary><see cref="World.DepositKind"/> at the camp, or None for seasonal camps.</summary>
    public byte[] CampDeposit = Array.Empty<byte>();
    public short[] CampCrew = Array.Empty<short>();
    public byte[] CampBuilt = Array.Empty<byte>(), CampMineHead = Array.Empty<byte>();
    // ---- monuments: the megalith a civilization raised (docs/MVP/02, STN-10); the front end plays the raising from the tick ----
    public int MonumentCount;
    public int[] MonumentTile = Array.Empty<int>();
    public sbyte[] MonumentCiv = Array.Empty<sbyte>();
    /// <summary>The tick the project was learned; the raising runs from here.</summary>
    public long[] MonumentTick = Array.Empty<long>();
    /// <summary>Population of the village that raises it (the size of the hauling crew).</summary>
    public int[] MonumentPopulation = Array.Empty<int>();
    // ---- seafaring: per civilization, the best boat its people know (0 none, 1 raft SEA-01, 2 bark canoe, 3 dugout, 4 outrigger, 5 plank boat SEA-05, 6 sail SEA-06) ----
    public byte[] CivSeaTier = Array.Empty<byte>();

    // ---- signal fires (COM-04): per civilization, 1 when its people keep signal posts on the hills between their villages ----
    public byte[] CivSignals = Array.Empty<byte>();

    // ---- burning (FIR-05 Landscape Burning, AGR-04 Slash-and-Burn): per civilization, the tick each was learned, or -1; two per civilization ----
    public long[] CivBurnLearned = Array.Empty<long>();

    // ---- feasting (SOC-05): per civilization, the tick it was learned, or -1 ----
    public long[] CivFeastLearned = Array.Empty<long>();

    // ---- dogs (HUS-01 Wolf Companions, TRN-02 Travois & Sledges): per civilization, 0 none, 1 dogs about the villages, 2 dogs also haul travois on the trails ----
    public byte[] CivDogs = Array.Empty<byte>();

    // ---- looks (CivLook bits): per civilization, which of the projects that change fields, flocks, carts, chariots, temples and clothes it knows ----
    public ushort[] CivLooks = Array.Empty<ushort>();

    // ---- worn trails: routes between villages as runs of tiles (-1 = not explored or open water) ----------------------------
    public int TrailCount;
    /// <summary>1 footpath, 2 marked trail, 3 beaten road.</summary>
    public byte[] TrailLevel = Array.Empty<byte>();
    public sbyte[] TrailCiv = Array.Empty<sbyte>();
    public bool[] TrailTrade = Array.Empty<bool>();
    public int[] TrailStart = Array.Empty<int>(), TrailLength = Array.Empty<int>();
    public int TrailTileCount;
    public int[] TrailTiles = Array.Empty<int>();
    public int BeaconCount;
    public int[] BeaconTile = Array.Empty<int>();
    public sbyte[] BeaconCiv = Array.Empty<sbyte>();

    // ---- parties on the road ------------------------------------------------------------------
    public int PartyCount;
    /// <summary><see cref="PartyKind"/>.</summary>
    public byte[] PartyKind = Array.Empty<byte>();
    public sbyte[] PartyCiv = Array.Empty<sbyte>();
    public sbyte[] PartyTargetCiv = Array.Empty<sbyte>();
    /// <summary>Settlement indices of the leg being walked (a returning barter trip walks target to home).</summary>
    public int[] PartyFrom = Array.Empty<int>(), PartyTo = Array.Empty<int>();
    public int[] PartyFromTile = Array.Empty<int>(), PartyToTile = Array.Empty<int>();
    /// <summary>Where the party is now, in map units, on the straight line between the two settlements.</summary>
    public int[] PartyX = Array.Empty<int>(), PartyY = Array.Empty<int>();
    /// <summary>Share of the leg walked, permille.</summary>
    public short[] PartyProgress = Array.Empty<short>();
    public int[] PartyMembers = Array.Empty<int>();
    public int[] PartyArriveSeason = Array.Empty<int>();
    /// <summary>0 outbound, 1 returning (barter).</summary>
    public byte[] PartyLeg = Array.Empty<byte>();
    /// <summary>Caravans and goods-barter loads: the <see cref="Politics.TradeGood"/> carried and the load in thousandths of its unit; 0 for other parties.</summary>
    public byte[] PartyGood = Array.Empty<byte>();
    public int[] PartyCargoPermille = Array.Empty<int>();
    /// <summary>A caravan's or explorer's stable id (-1 for other parties): what a raid on a caravan targets (command 12, A = −(id + 1)).</summary>
    public int[] PartyId = Array.Empty<int>();

    // ---- trade routes: the viewer's compacts (every compact in omniscient mode), docs/MVP/22 §6.2, §14 ----
    public int RouteCount;
    public int[] RouteCompact = Array.Empty<int>();
    /// <summary>Seller and buyer civilizations (-1 for a Free Folk village, whose settlement is in RouteSellerSettlement / RouteBuyerSettlement).</summary>
    public sbyte[] RouteSellerCiv = Array.Empty<sbyte>(), RouteBuyerCiv = Array.Empty<sbyte>();
    public int[] RouteSellerSettlement = Array.Empty<int>(), RouteBuyerSettlement = Array.Empty<int>();
    public int[] RouteFromTile = Array.Empty<int>(), RouteToTile = Array.Empty<int>();
    /// <summary><see cref="Politics.TradeGood"/>.</summary>
    public byte[] RouteGood = Array.Empty<byte>();
    /// <summary>The last delivery, in thousandths of the good's unit (food in thousandths of a ration).</summary>
    public int[] RouteVolumePermille = Array.Empty<int>();
    public int[] RouteDeliveries = Array.Empty<int>(), RouteSinceSeason = Array.Empty<int>();

    // ---- occupations --------------------------------------------------------------------------
    public int OccupationCount;
    public sbyte[] OccupationCiv = Array.Empty<sbyte>();
    public int[] OccupationSettlement = Array.Empty<int>();
    public int[] OccupationTile = Array.Empty<int>();
    public int[] OccupationHolders = Array.Empty<int>();
    public int[] OccupationSinceSeason = Array.Empty<int>();
    public int[] OccupationRequiredSeasons = Array.Empty<int>();

    // ---- civilizations ------------------------------------------------------------------------
    public int CivCount;
    /// <summary><see cref="CivViewFlags"/> relative to the viewer.</summary>
    public ushort[] CivFlags = Array.Empty<ushort>();
    /// <summary>Public: era, keystone breadth, Legacy, Firsts, Dominion and land share, posture and Relations with the viewer.</summary>
    public byte[] CivEra = Array.Empty<byte>();
    public short[] CivBreadthDone = Array.Empty<short>(), CivBreadthNeeded = Array.Empty<short>();
    public long[] CivLegacy = Array.Empty<long>(), CivFirstsLegacy = Array.Empty<long>(), CivDominion = Array.Empty<long>();
    public int[] CivLandBasisPoints = Array.Empty<int>();
    /// <summary><see cref="Core.Posture"/>; Raid posture shows as warpaint, so it is public.</summary>
    public byte[] CivPosture = Array.Empty<byte>();
    public short[] CivRelations = Array.Empty<short>();
    /// <summary>0 bare hands, 1 club, 2 spear, 3 fire-hardened spear, 4 atlatl, 5 bow (13 §7.1): weapon sprites.</summary>
    public byte[] CivWeaponTier = Array.Empty<byte>();
    /// <summary>Private (zero unless <see cref="CivViewFlags.Detailed"/>): people, settlements, Insight per year (Fix64 raw), projects done.</summary>
    public int[] CivPopulation = Array.Empty<int>(), CivSettlements = Array.Empty<int>(), CivProjectsDone = Array.Empty<int>();
    public long[] CivInsightPerYear = Array.Empty<long>();
    /// <summary>
    /// Private (zero unless <see cref="CivViewFlags.Detailed"/>): Mandate in hand, its cap and the seasons until the next point
    /// (0 when full or not yet unlocked), docs/MVP/22 §4.2. The top bar draws pips from these.
    /// </summary>
    public byte[] CivMandate = Array.Empty<byte>(), CivMandateCap = Array.Empty<byte>(), CivMandateNext = Array.Empty<byte>();
    /// <summary>Embargo masks over <see cref="Politics.TradeGood"/> bits: what the viewer stops selling to each civilization, and what each stops selling to the viewer.</summary>
    public byte[] CivEmbargoByViewer = Array.Empty<byte>(), CivEmbargoOnViewer = Array.Empty<byte>();

    // ---- the focus bar of one civilization (the viewer; civilization 0 in omniscient mode) ---
    public int FocusCiv;
    /// <summary>Project in each focus slot (-1 empty) and its progress, permille of the effective cost.</summary>
    public readonly int[] FocusSlotProject = new int[3];
    public readonly short[] FocusSlotPermille = new short[3];
    public int FocusQueueCount;
    public readonly int[] FocusQueue = new int[8];
    public bool FocusCouncil;
    /// <summary>Leaning percent: craft, kin, reach.</summary>
    public readonly byte[] FocusLeaning = new byte[3];
    public int FocusBeaconsAllowed, FocusBeaconsPlaced;
    /// <summary>The focus civilization's era.</summary>
    public int FocusEra;
    /// <summary><see cref="Research.ProjectState"/> of every project (dense pack index) for the focus civilization: the research web's node states (08 §8.2).</summary>
    public int FocusProjectCount;
    public byte[] FocusProjectState = Array.Empty<byte>();
    /// <summary>Current Know-how stock by research field (lineage), fixed-point raw values.</summary>
    public long[] FocusKnowHow = Array.Empty<long>();
    public int FocusKnowHowCount;
    /// <summary>Mandate pips (docs/MVP/22 §4.2): in hand, the cap, seasons to the next point (0 when full or locked), and whether Decrees are unlocked.</summary>
    public int FocusMandate, FocusMandateCap, FocusMandateNextSeason;
    public bool FocusMandateUnlocked;
    /// <summary>What each Decree costs now, indexed by <see cref="Politics.DecreeKind"/> (0 unused).</summary>
    public readonly byte[] FocusDecreeCost = new byte[7];
    /// <summary>Decrees issued this season that resolve at its end.</summary>
    public int FocusPendingDecrees;
    /// <summary>The standing levy host: its target settlement (-1 none), size and last season.</summary>
    public int FocusLevyTarget = -1, FocusLevyHost, FocusLevyUntilSeason;
    /// <summary>Decrees carried out and seasons at the Mandate cap so far (the end screen; 22 §15 T27).</summary>
    public int FocusDecreesDone, FocusSeasonsAtCap;

    // ---- events -------------------------------------------------------------------------------
    /// <summary>The newest events (up to the builder's window), oldest first. Show those with a Sequence above the last one shown.</summary>
    public int EventCount;
    public ViewEvent[] Events = Array.Empty<ViewEvent>();
    /// <summary>Sequence of the newest event logged so far (0 none); a gap before Events[0] means the reader fell behind.</summary>
    public long LatestEventSequence;

    /// <summary>Recent AI explanations for the post-match "why" panel; presentation data, not simulation state.</summary>
    public int AiWhyCount;
    public AiWhyEntry[] AiWhyEntries = Array.Empty<AiWhyEntry>();

    /// <summary>
    /// Grows the buffers to hold this much up front, so a long match never allocates while frames are built (a front end
    /// calls it once with the MVP's budgets: about 2,400 figures, 600 per civilization).
    /// </summary>
    public void Reserve(int figures, int settlements, int herds, int parties = 64, int plots = 4096,
        int events = FrameBuilder.DefaultEventWindow, int aiWhy = 32)
    {
        EnsureFigures(figures);
        EnsureSettlements(settlements);
        EnsureHerds(herds);
        EnsurePredators(herds);
        EnsureParties(parties);
        EnsureRoutes(parties);
        EnsureOccupations(parties);
        EnsurePlots(plots);
        EnsureCamps(settlements * 4);
        EnsureBeacons(64);
        Ensure(ref Events, events);
        Ensure(ref AiWhyEntries, aiWhy);
        Ensure(ref FocusProjectState, 1024);
    }

    internal static void Ensure<T>(ref T[] array, int needed)
    {
        if (array.Length >= needed) return;
        int size = Math.Max(needed, Math.Max(16, array.Length * 2));
        Array.Resize(ref array, size);
    }

    internal void EnsureTiles(int count)
    {
        if (Visibility.Length != count) Visibility = new byte[count];
        if (Owner.Length != count) Owner = new sbyte[count];
        if (Claim.Length != count) Claim = new byte[count];
        if (Claimant.Length != count) Claimant = new sbyte[count];
        if (Claim2.Length != count) Claim2 = new byte[count];
        if (Claimant2.Length != count) Claimant2 = new sbyte[count];
        if (Claim3.Length != count) Claim3 = new byte[count];
        if (Claimant3.Length != count) Claimant3 = new sbyte[count];
        if (TileFlags.Length != count) TileFlags = new byte[count];
        if (KnownDeposits.Length != count) KnownDeposits = new byte[count];
        if (ForestBand.Length != count) ForestBand = new byte[count];
        if (DepositWorked.Length != count) DepositWorked = new byte[count];
        if (MiningReach.Length != count) MiningReach = new byte[count];
    }

    internal void EnsureFigures(int n)
    {
        if (FigureId.Length >= n) return;
        Ensure(ref FigureId, n); Ensure(ref FigureCiv, n); Ensure(ref FigureSettlement, n); Ensure(ref FigureTile, n);
        Ensure(ref FigureX, n); Ensure(ref FigureY, n); Ensure(ref FigureActivity, n); Ensure(ref FigureRole, n); Ensure(ref FigureAge, n);
        Ensure(ref FigureHealth, n); Ensure(ref FigureFlags, n); Ensure(ref FigureToolRung, n); Ensure(ref FigureCarry, n);
        Ensure(ref FigureKnownToolRung, n); Ensure(ref FigureMethodEfficiencyPermille, n);
        Ensure(ref FigureMethodFallback, n); Ensure(ref FigureMethodFallbackTool, n);
    }

    internal void EnsureHerds(int n)
    {
        if (HerdId.Length >= n) return;
        Ensure(ref HerdId, n); Ensure(ref HerdSpecies, n); Ensure(ref HerdTile, n); Ensure(ref HerdX, n); Ensure(ref HerdY, n);
        Ensure(ref HerdAnimals, n); Ensure(ref HerdFear, n); Ensure(ref HerdAway, n);
    }

    internal void EnsurePredators(int n)
    {
        if (PredatorTile.Length >= n) return;
        Ensure(ref PredatorTile, n); Ensure(ref PredatorSpecies, n); Ensure(ref PredatorSize, n);
    }

    internal void EnsureSettlements(int n)
    {
        if (SettlementIndex.Length < n)
        {
            Ensure(ref SettlementIndex, n); Ensure(ref SettlementCiv, n); Ensure(ref SettlementTile, n); Ensure(ref SettlementPopulation, n);
            Ensure(ref SettlementFood, n); Ensure(ref SettlementWinterForecast, n); Ensure(ref SettlementCohesion, n);
            Ensure(ref SettlementOverreach, n); Ensure(ref SettlementSpanPenalty, n);
            Ensure(ref SettlementFlags, n); Ensure(ref SettlementBuildKind, n); Ensure(ref SettlementBuildPermille, n);
            Ensure(ref SettlementSmallStock, n); Ensure(ref SettlementCattle, n); Ensure(ref SettlementPigs, n);
            Ensure(ref SettlementSeenSeason, n); Ensure(ref SettlementHalls, n);
            Ensure(ref SettlementZiggurats, n); Ensure(ref SettlementZigguratPermille, n);
        }
        Ensure(ref SettlementStructures, SettlementIndex.Length * StructureKinds);
    }

    internal void EnsurePlots(int n)
    {
        if (PlotTile.Length >= n) return;
        Ensure(ref PlotTile, n); Ensure(ref PlotSettlement, n); Ensure(ref PlotKind, n);
    }

    internal void EnsureCamps(int n)
    {
        if (CampTile.Length >= n) return;
        Ensure(ref CampTile, n); Ensure(ref CampId, n); Ensure(ref CampCiv, n); Ensure(ref CampKind, n); Ensure(ref CampParent, n);
        Ensure(ref CampDeposit, n); Ensure(ref CampCrew, n); Ensure(ref CampBuilt, n); Ensure(ref CampMineHead, n);
    }

    internal void EnsureTrails(int n)
    {
        if (TrailLevel.Length >= n) return;
        Ensure(ref TrailLevel, n); Ensure(ref TrailCiv, n); Ensure(ref TrailTrade, n); Ensure(ref TrailStart, n); Ensure(ref TrailLength, n);
    }

    internal void EnsureTrailTiles(int n) { if (TrailTiles.Length < n) Ensure(ref TrailTiles, Math.Max(n, TrailTiles.Length * 2)); }

    internal void EnsureMonuments(int n)
    {
        if (MonumentTile.Length >= n) return;
        Ensure(ref MonumentTile, n); Ensure(ref MonumentCiv, n); Ensure(ref MonumentTick, n); Ensure(ref MonumentPopulation, n);
    }

    internal void EnsureBeacons(int n) { if (BeaconTile.Length < n) { Ensure(ref BeaconTile, n); Ensure(ref BeaconCiv, n); } }

    internal void EnsureParties(int n)
    {
        if (PartyKind.Length >= n) return;
        Ensure(ref PartyKind, n); Ensure(ref PartyCiv, n); Ensure(ref PartyTargetCiv, n); Ensure(ref PartyFrom, n); Ensure(ref PartyTo, n);
        Ensure(ref PartyFromTile, n); Ensure(ref PartyToTile, n); Ensure(ref PartyX, n); Ensure(ref PartyY, n);
        Ensure(ref PartyProgress, n); Ensure(ref PartyMembers, n); Ensure(ref PartyArriveSeason, n); Ensure(ref PartyLeg, n);
        Ensure(ref PartyGood, n); Ensure(ref PartyCargoPermille, n); Ensure(ref PartyId, n);
    }

    internal void EnsureRoutes(int n)
    {
        if (RouteCompact.Length >= n) return;
        Ensure(ref RouteCompact, n); Ensure(ref RouteSellerCiv, n); Ensure(ref RouteBuyerCiv, n); Ensure(ref RouteSellerSettlement, n);
        Ensure(ref RouteBuyerSettlement, n); Ensure(ref RouteFromTile, n); Ensure(ref RouteToTile, n); Ensure(ref RouteGood, n);
        Ensure(ref RouteVolumePermille, n); Ensure(ref RouteDeliveries, n); Ensure(ref RouteSinceSeason, n);
    }

    internal void EnsureOccupations(int n)
    {
        if (OccupationCiv.Length >= n) return;
        Ensure(ref OccupationCiv, n); Ensure(ref OccupationSettlement, n); Ensure(ref OccupationTile, n); Ensure(ref OccupationHolders, n);
        Ensure(ref OccupationSinceSeason, n); Ensure(ref OccupationRequiredSeasons, n);
    }

    internal void EnsureCivs(int n)
    {
        if (CivFlags.Length >= n) return;
        CivFlags = new ushort[n]; CivEra = new byte[n]; CivBreadthDone = new short[n]; CivBreadthNeeded = new short[n];
        CivLegacy = new long[n]; CivFirstsLegacy = new long[n]; CivDominion = new long[n]; CivLandBasisPoints = new int[n];
        CivPosture = new byte[n]; CivRelations = new short[n]; CivWeaponTier = new byte[n]; CivPopulation = new int[n];
        CivSettlements = new int[n]; CivProjectsDone = new int[n]; CivInsightPerYear = new long[n];
        CivMandate = new byte[n]; CivMandateCap = new byte[n]; CivMandateNext = new byte[n];
        CivEmbargoByViewer = new byte[n]; CivEmbargoOnViewer = new byte[n];
    }

    /// <summary>The index of a figure id in this frame (binary search), or -1 when it is not in the frame.</summary>
    public int IndexOfFigure(long id)
    {
        int index = Array.BinarySearch(FigureId, 0, FigureCount, id);
        return index >= 0 ? index : -1;
    }

    /// <summary>The index of a settlement in this frame, or -1 when the viewer does not know it.</summary>
    public int IndexOfSettlement(int settlementIndex)
    {
        int index = Array.BinarySearch(SettlementIndex, 0, SettlementCount, settlementIndex);
        return index >= 0 ? index : -1;
    }
}
