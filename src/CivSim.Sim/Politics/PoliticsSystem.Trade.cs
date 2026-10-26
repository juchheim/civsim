using System;
using System.Collections.Generic;
using CivSim.Sim.Economy;
using CivSim.Sim.Numerics;
using CivSim.Sim.World;

namespace CivSim.Sim.Politics;

/// <summary>
/// Tin trade without a diplomacy UI (docs/MVP/22 §6; WP8a): goods barter by scarcity value, Compacts as standing routes served by
/// visible, raidable caravans, the Embargo toggle, non-aggression pacts (DIP-04) and border treaties (DIP-05), Free Folk relations
/// and the Invite, plague carried along routes, and the Tin Folk who appear at the world's first KEY-3. Everything here is idle until
/// the Bronze Age begins, so E0–E2 matches never touch it.
/// </summary>
public sealed partial class PoliticsSystem : ITradeRouteSource
{
    private sealed class TradeRules
    {
        public Fix64 Gain, GainNum04, ClampLow, ClampHigh, FolkDefense;
        public int Horizon, MinRelations, MinRelationsDip05, EmbargoRelations, InviteSeasons, InviteRelations, InviteGift;
        public int CaravanRaidRelations, CaravanRaidReputation, PactSeasons, PactBreakRelations, PactBreakReputation, TreatySeasons;
        public int FolkAdults, FolkElders, FolkChildren, FolkRadius, FolkMinDist;

        public TradeRules(Research.ResearchCatalog c)
        {
            int I(string id) => c.Constant(id).FloorToInt();
            Gain = c.Constant("BarterGain"); GainNum04 = Gain / 2; ClampLow = c.Constant("ScarcityClampLow"); ClampHigh = c.Constant("ScarcityClampHigh");
            Horizon = I("CompactHorizonSeasons"); MinRelations = I("CompactMinRelations"); MinRelationsDip05 = -30;
            EmbargoRelations = I("EmbargoRelations"); InviteSeasons = I("InviteSeasons"); InviteRelations = I("InviteRelations");
            InviteGift = I("InviteGift"); CaravanRaidRelations = I("CaravanRaidRelations"); CaravanRaidReputation = I("CaravanRaidReputation");
            PactSeasons = I("PactYears") * 4; PactBreakRelations = I("PactBreakRelations"); PactBreakReputation = I("PactBreakReputation");
            TreatySeasons = I("TreatyYears") * 4; FolkAdults = I("TinFolkAdults"); FolkElders = I("TinFolkElders"); FolkChildren = I("TinFolkChildren");
            FolkRadius = I("TinFolkRadius"); FolkMinDist = I("TinFolkMinDist"); FolkDefense = c.Constant("FreeFolkDefense");
        }
    }

    /// <summary>Tin ore a Tin Folk village digs from its placer a season: two miners at 0.5 L a work-season (docs/MVP/22 §5.2), smelted at half.</summary>
    private static readonly Fix64 FolkOrePerSeason = Fix64.One;
    /// <summary>Folk relations: the first compact opens at 10, each delivery adds 3 (so an Invite is possible after about 8 seasons of trade).</summary>
    private const int FolkStartRelations = 10, FolkDeliveryRelations = 3;
    private static readonly Fix64 MinLoad = Fix64.FromRatio(1, 100);

    private TradeRules _trade = null!;
    private readonly List<Compact> _compacts = new();
    private readonly List<Pact> _pacts = new();
    private readonly List<FolkTie> _folkTies = new();
    private readonly List<TinFolkVillage> _tinFolk = new();
    private bool _tinFolkSpawned;
    private int _nextCompactId;
    /// <summary>Embargo bits per ordered pair (seller × civs + buyer): bit g for <see cref="TradeGood"/> g.</summary>
    private byte[] _embargo = Array.Empty<byte>();

    private void InitTrade()
    {
        _trade = new TradeRules(_catalog);
        _economy.TradeRoutes = this;
    }

    // ---- public surface --------------------------------------------------------------------------------

    public IReadOnlyList<Compact> Compacts => _compacts;
    public IReadOnlyList<Pact> Pacts => _pacts;
    public IReadOnlyList<TinFolkVillage> TinFolk => _tinFolk;
    public IReadOnlyList<FolkTie> FolkTies => _folkTies;
    public bool TinFolkSpawned => _tinFolkSpawned;

    /// <summary>Whether <paramref name="seller"/> embargoes <paramref name="good"/> to <paramref name="buyer"/> (docs/MVP/22 §4.3).</summary>
    public bool Embargoes(int seller, int buyer, TradeGood good)
        => unchecked((uint)seller) < unchecked((uint)_civs) && unchecked((uint)buyer) < unchecked((uint)_civs) && good is > TradeGood.None and < TradeGood.Count &&
           (_embargo[seller * _civs + buyer] & (1 << (int)good)) != 0;

    /// <summary>The goods a civilization embargoes to another, as a bit mask over <see cref="TradeGood"/>.</summary>
    public int EmbargoMask(int seller, int buyer) => _embargo[seller * _civs + buyer];

    private bool AnyEmbargo()
    {
        foreach (byte b in _embargo) if (b != 0) return true;
        return false;
    }

    /// <summary>A non-aggression pact stands between the two (docs/MVP/22 §6.5, DIP-04).</summary>
    public bool HasPact(int a, int b) => FindPact(a, b, CompactVariant.Pact) is not null;
    /// <summary>A border treaty stands between the two (DIP-05).</summary>
    public bool HasTreaty(int a, int b) => FindPact(a, b, CompactVariant.Treaty) is not null;

    private Pact? FindPact(int a, int b, CompactVariant kind)
    {
        foreach (Pact p in _pacts)
            if (p.Kind == kind && p.UntilSeason > _season && (p.A == a && p.B == b || p.A == b && p.B == a)) return p;
        return null;
    }

    /// <summary>Whether a settlement is a Tin Folk village that is still free.</summary>
    public bool IsTinFolk(int settlement)
    {
        foreach (TinFolkVillage v in _tinFolk) if (v.Settlement == settlement && v.Joined < 0) return true;
        return false;
    }

    /// <summary>Whether a sighting record identifies a still-free Tin Folk settlement close enough for a Levy.</summary>
    public bool CanLevyKnownTinFolk(int civ, int settlement, int lastSeenSeason)
    {
        if (unchecked((uint)civ) >= (uint)_civs || lastSeenSeason > _season || _season - lastSeenSeason > 2 ||
            !IsTinFolk(settlement) || !KnowsFolk(civ, settlement))
            return false;
        int home = NearestOwnSettlement(civ, _sTile[settlement]);
        return home >= 0 && TravelSeasons(civ, _sTile[home], _sTile[settlement]) <= 2;
    }

    /// <summary>Relations between a civilization and a Free Folk village (0 until they trade).</summary>
    public int FolkRelations(int civ, int folk) => FolkTieOf(civ, folk, create: false)?.Relations ?? 0;

    /// <summary>Whether the civilization has traded with the village (the capture and raid penalties of docs/MVP/22 §6.4 read it).</summary>
    public bool TradedWithFolk(int civ, int folk) => FolkTieOf(civ, folk, create: false) is { Deliveries: > 0 };

    private FolkTie? FolkTieOf(int civ, int folk, bool create)
    {
        foreach (FolkTie t in _folkTies) if (t.Civ == civ && t.Folk == folk) return t;
        if (!create) return null;
        var tie = new FolkTie { Civ = civ, Folk = folk, Relations = FolkStartRelations };
        _folkTies.Add(tie);
        return tie;
    }

    /// <summary>Tiles within a free Tin Folk village's claim radius (3) are theirs: no founding or camp there (docs/MVP/22 §6.4, §11).</summary>
    private bool FreeFolkOwns(int tile)
    {
        foreach (TinFolkVillage v in _tinFolk)
            if (v.Joined < 0 && unchecked((uint)v.Settlement) < unchecked((uint)_sCount) && _free[v.Settlement] &&
                HexGrid.Distance(_sTile[v.Settlement], tile, _width) <= _trade.FolkRadius) return true;
        return false;
    }

    // ---- ITradeRouteSource (the economy's market towns and epidemic inflow) -----------------------------

    int ITradeRouteSource.RoutesAt(int settlementIndex)
    {
        int n = 0;
        foreach (Compact c in _compacts)
            if (c.Active && (c.LastSeller == settlementIndex || c.LastBuyer == settlementIndex)) n++;
        return n;
    }

    Fix64 ITradeRouteSource.InflowAt(int settlementIndex)
    {
        int arrivals = 0;
        foreach (Expedition e in _expeditions)
            if (e.Kind == ExpeditionKind.Caravan && e.ToSettlement == settlementIndex && e.ArriveSeason <= _season + 1) arrivals += Math.Max(1, e.Members.Count);
        if (arrivals == 0) return Fix64.Zero;
        int pop = Math.Max(1, _economy.Population(settlementIndex));
        // Caravan crews arriving as a share of the people, at most a tenth (docs/MVP/09 §10.1 inflow).
        return Fix64.Min(Fix64.FromRatio(1, 10), Fix64.FromRatio(arrivals, pop));
    }

    // ---- valuation (docs/MVP/22 §6.1: base × clamp(need ÷ stock, 0.5, 3)) ------------------------------------

    /// <summary>What one unit of a good is worth to a settlement now, in rations.</summary>
    public Fix64 UnitValue(int settlement, TradeGood good)
    {
        if (unchecked((uint)settlement) >= unchecked((uint)_sCount) || good is TradeGood.None or >= TradeGood.Count) return Fix64.Zero;
        string id = DecreeArgs.GoodId(good);
        Fix64 basis = good == TradeGood.Food ? Fix64.One : _economy.GoodBarterValue(id);
        if (basis <= Fix64.Zero) return Fix64.Zero;
        Fix64 need = _economy.TradeNeed(settlement, id, _trade.Horizon);
        Fix64 stock = Stock(settlement, good);
        Fix64 ratio;
        if (need <= Fix64.Zero) ratio = _trade.ClampLow;
        else if (stock <= Fix64.Zero) ratio = _trade.ClampHigh;
        else if (stock < need / _trade.ClampHigh) ratio = _trade.ClampHigh;
        else if (stock > need / _trade.ClampLow) ratio = _trade.ClampLow;
        else ratio = Fix64.Clamp(need / stock, _trade.ClampLow, _trade.ClampHigh);
        return basis * ratio;
    }

    private Fix64 Stock(int settlement, TradeGood good)
        => good == TradeGood.Food ? _economy.FoodStores(settlement) : _economy.GoodStock(settlement, DecreeArgs.GoodId(good));

    /// <summary>What a settlement can spare of a good: its stock less its own next four seasons of need.</summary>
    public Fix64 Surplus(int settlement, TradeGood good)
        => Fix64.Max(Fix64.Zero, Stock(settlement, good) - _economy.TradeNeed(settlement, DecreeArgs.GoodId(good), _trade.Horizon));

    private Fix64 Shortfall(int settlement, TradeGood good)
        => Fix64.Max(Fix64.Zero, _economy.TradeNeed(settlement, DecreeArgs.GoodId(good), _trade.Horizon) - Stock(settlement, good));

    /// <summary>The gain a side wants before it trades: 10%, 5% with NUM-04 (docs/MVP/13 §4.2).</summary>
    private Fix64 GainFor(int civ) => civ >= 0 && Done(civ, _bp.Num04) ? _trade.GainNum04 : _trade.Gain;

    /// <summary>Both sides gain at the geometric-mean price when the buyer's value is at least (1 + g)² of the seller's.</summary>
    private bool BothGain(Fix64 sellerValue, Fix64 buyerValue, int sellerCiv, int buyerCiv)
    {
        if (sellerValue <= Fix64.Zero || buyerValue <= Fix64.Zero) return false;
        Fix64 g = Fix64.One + Fix64.Max(GainFor(sellerCiv), GainFor(buyerCiv));
        return buyerValue >= sellerValue * g * g;
    }

    /// <summary>
    /// The settlement a party trades through: a Free Folk village is itself; a civilization's seller hub holds the most to spare, its
    /// buyer hub lacks the most (nearest to <paramref name="near"/> on ties), ascending index last.
    /// </summary>
    private int HubOf(int civ, int folk, TradeGood good, bool selling, int near)
    {
        if (folk >= 0) return folk < _sCount && _sPop[folk] > 0 ? folk : -1;
        int best = -1;
        Fix64 bestScore = Fix64.MinValue;
        int bestDistance = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_sCiv[s] != civ || _free[s] || _sPop[s] <= 0) continue;
            Fix64 score = selling ? Surplus(s, good) : Shortfall(s, good);
            int d = near >= 0 ? HexGrid.Distance(_sTile[s], _sTile[near], _width) : 0;
            if (score > bestScore || score == bestScore && d < bestDistance)
            {
                best = s; bestScore = score; bestDistance = d;
            }
        }
        return best;
    }

    // ---- the Compact Decree ---------------------------------------------------------------------------

    private OrderRefusalReason ExecuteCompact(int civ, int target, int extra, out int subject, out int other)
    {
        subject = -1;
        other = -1;
        CompactVariant variant = DecreeArgs.Variant(extra);
        bool folk = DecreeArgs.IsFolkParty(target);
        int folkSettlement = folk ? DecreeArgs.FolkSettlement(target) : -1;
        if (!folk) other = target;
        if (folk)
        {
            if (unchecked((uint)folkSettlement) >= unchecked((uint)_sCount) || !_free[folkSettlement] || _sPop[folkSettlement] <= 0) return OrderRefusalReason.InvalidTarget;
            if (!KnowsFolk(civ, folkSettlement)) return OrderRefusalReason.OutOfReach;
        }
        else
        {
            if (unchecked((uint)target) >= unchecked((uint)_civs) || target == civ) return OrderRefusalReason.InvalidTarget;
            if (!_dip.Contacted(civ, target)) return OrderRefusalReason.OutOfReach;
            if (_dip.AtWar(civ, target) || _dip.Relations(civ, target) < 0) return OrderRefusalReason.Rejected;
        }
        switch (variant)
        {
            case CompactVariant.Invite: return folk ? ExecuteInvite(civ, folkSettlement, out subject) : OrderRefusalReason.InvalidTarget;
            case CompactVariant.Pact:
            case CompactVariant.Treaty: return folk ? OrderRefusalReason.InvalidTarget : ExecutePact(civ, target, variant);
        }
        TradeGood good = DecreeArgs.Good(extra);
        if (good is TradeGood.None or >= TradeGood.Count) return OrderRefusalReason.InvalidTarget;
        bool weSell = DecreeArgs.Sell(extra);
        int sellerCiv = weSell ? civ : folk ? -1 : target, buyerCiv = weSell ? (folk ? -1 : target) : civ;
        int sellerFolk = weSell ? -1 : folkSettlement, buyerFolk = weSell ? folkSettlement : -1;
        if (folk && FolkTieOf(civ, folkSettlement, create: false) is { Relations: < 0 }) return OrderRefusalReason.Rejected;
        if (sellerCiv >= 0 && buyerCiv >= 0 && Embargoes(sellerCiv, buyerCiv, good)) return OrderRefusalReason.Rejected;
        foreach (Compact c in _compacts)
            if (c.Active && c.Good == good && c.SellerCiv == sellerCiv && c.BuyerCiv == buyerCiv && c.SellerFolk == sellerFolk && c.BuyerFolk == buyerFolk)
                return OrderRefusalReason.Rejected;
        int sellerHub = HubOf(sellerCiv, sellerFolk, good, selling: true, -1);
        int buyerHub = HubOf(buyerCiv, buyerFolk, good, selling: false, sellerHub);
        if (sellerHub < 0 || buyerHub < 0) return OrderRefusalReason.NoSite;
        if (Travel(sellerCiv >= 0 ? sellerCiv : buyerCiv, _sTile[sellerHub], _sTile[buyerHub]) == int.MaxValue) return OrderRefusalReason.OutOfReach;
        if (RouteBlocked(sellerCiv, buyerCiv, sellerHub, buyerHub)) return OrderRefusalReason.Rejected;
        // The receiver decides by expected value (docs/MVP/22 §6.2): both sides gain at current scarcity and Relations ≥ 0.
        Fix64 vs = UnitValue(sellerHub, good), vb = UnitValue(buyerHub, good);
        int receiver = folk ? -1 : target;
        bool accepts = receiver >= 0 && _judge[receiver] is { } judge
            ? judge.Accepts(this, civ, receiver, CompactVariant.Goods, good, !weSell, vs, vb)
            : BothGain(vs, vb, sellerCiv, buyerCiv);
        if (!accepts) return OrderRefusalReason.Rejected;
        var compact = new Compact
        {
            Id = _nextCompactId++, SellerCiv = sellerCiv, SellerFolk = sellerFolk, BuyerCiv = buyerCiv, BuyerFolk = buyerFolk,
            Proposer = civ, Good = good, SinceSeason = _season,
        };
        _compacts.Add(compact);
        if (folk) FolkTieOf(civ, folkSettlement, create: true);
        subject = compact.Id;
        int code = (int)good;
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.CompactSigned, folk ? -1 : target, compact.Id, code + (weSell ? 100 : 0)));
        if (!folk) _events.Add(new PoliticsEvent(_tick, (byte)target, PoliticsEventKind.CompactSigned, civ, compact.Id, code + (weSell ? 0 : 100)));
        return 0;
    }

    /// <summary>A route is blocked when a third civilization at war with either side owns a tile on the straight line between the hubs.</summary>
    private bool RouteBlocked(int sellerCiv, int buyerCiv, int from, int to)
    {
        if (UnderInvestment(from) || UnderInvestment(to)) return true;
        bool anyWar = false;
        for (int x = 0; x < _civs && !anyWar; x++)
            if (x != sellerCiv && x != buyerCiv && (sellerCiv >= 0 && _dip.AtWar(x, sellerCiv) || buyerCiv >= 0 && _dip.AtWar(x, buyerCiv))) anyWar = true;
        if (!anyWar) return false;
        HexGrid.Coordinates(_sTile[from], _width, out int fx, out int fy);
        HexGrid.Coordinates(_sTile[to], _width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(_sTile[from], _sTile[to], _width));
        for (int k = 0; k < steps; k++)
        {
            int owner = _territory.OwnerOf(HexGrid.Index(fx + (tx - fx) * k / steps, fy + (ty - fy) * k / steps, _width, _height));
            if (owner < 0 || owner == sellerCiv || owner == buyerCiv) continue;
            if (sellerCiv >= 0 && _dip.AtWar(owner, sellerCiv) || buyerCiv >= 0 && _dip.AtWar(owner, buyerCiv)) return true;
        }
        return false;
    }

    /// <summary>The Invite (docs/MVP/22 §4.3): a Free Folk village with an active compact for 8 seasons and Relations above 30 joins as a daughter village.</summary>
    private OrderRefusalReason ExecuteInvite(int civ, int folk, out int subject)
    {
        subject = folk;
        FolkTie? tie = FolkTieOf(civ, folk, create: false);
        bool trading = false;
        foreach (Compact c in _compacts) if (c.Active && c.Involves(civ) && c.InvolvesFolk(folk)) trading = true;
        if (tie is null || !trading || tie.CompactSeasons < _trade.InviteSeasons || tie.Relations <= _trade.InviteRelations)
            return OrderRefusalReason.Rejected;
        int giver = NearestOwnSettlement(civ, _sTile[folk]);
        if (giver < 0) return OrderRefusalReason.NoDonor;
        Fix64 gift = _economy.TakeGoods(giver, Fix64.FromInt(_trade.InviteGift), LootPreference.FoodOnly);
        _economy.GiveGoods(folk, gift);
        _free[folk] = false;
        _economy.TransferSettlement(folk, civ);
        foreach (TinFolkVillage v in _tinFolk) if (v.Settlement == folk) v.Joined = civ;
        foreach (Compact c in _compacts)
            if (c.Active && c.InvolvesFolk(folk)) EndCompact(c, CompactEndReason.Joined);
        RefreshSettlements();
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.VillageJoined, -1, folk, _sPop[folk]));
        return 0;
    }

    /// <summary>A non-aggression pact (DIP-04 on both sides, 8 years) or a border treaty (DIP-05, 10 years), accepted at Relations ≥ 0.</summary>
    private OrderRefusalReason ExecutePact(int civ, int other, CompactVariant kind)
    {
        int project = kind == CompactVariant.Pact ? _bp.Dip04 : _bp.Dip05;
        if (!Done(civ, project) || !Done(other, project)) return OrderRefusalReason.Rejected;
        if (FindPact(civ, other, kind) is not null) return OrderRefusalReason.Rejected;
        bool accepts = _judge[other] is { } judge
            ? judge.Accepts(this, civ, other, kind, TradeGood.None, false, Fix64.Zero, Fix64.Zero)
            : _dip.Relations(civ, other) >= 0;
        if (!accepts) return OrderRefusalReason.Rejected;
        int seasons = kind == CompactVariant.Pact ? _trade.PactSeasons : _trade.TreatySeasons;
        _pacts.Add(new Pact { A = Math.Min(civ, other), B = Math.Max(civ, other), Kind = kind, SinceSeason = _season, UntilSeason = _season + seasons });
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.PactSigned, other, (int)kind, seasons));
        _events.Add(new PoliticsEvent(_tick, (byte)other, PoliticsEventKind.PactSigned, civ, (int)kind, seasons));
        return 0;
    }

    /// <summary>War breaks a pact between the two: Relations −60, the breaker's reputation −30 (docs/MVP/22 §12, DIP-04).</summary>
    private void BreakPacts(int breaker, int other)
    {
        for (int i = _pacts.Count - 1; i >= 0; i--)
        {
            Pact p = _pacts[i];
            if (p.UntilSeason <= _season || !(p.A == breaker && p.B == other || p.A == other && p.B == breaker)) continue;
            _pacts.RemoveAt(i);
            if (p.Kind != CompactVariant.Pact) continue;
            _dip.Shift(breaker, other, _trade.PactBreakRelations);
            _dip.AdjustReputation(breaker, _trade.PactBreakReputation);
            _events.Add(new PoliticsEvent(_tick, (byte)breaker, PoliticsEventKind.PactBroken, other, (int)p.Kind, 1));
            _events.Add(new PoliticsEvent(_tick, (byte)other, PoliticsEventKind.PactBroken, breaker, (int)p.Kind, 0));
        }
    }

    // ---- Embargo -----------------------------------------------------------------------------------------

    /// <summary>
    /// The Embargo toggle (docs/MVP/22 §4.3): stop selling a good to a civilization (free, from KEY-3). Existing compacts in that good
    /// end; Relations −10 when it is set. Lifting it costs nothing. False when the rules refuse.
    /// </summary>
    public bool OrderEmbargo(int civ, int target, TradeGood good, bool on)
    {
        if (unchecked((uint)civ) >= unchecked((uint)_civs) || unchecked((uint)target) >= unchecked((uint)_civs) || civ == target || good is TradeGood.None or >= TradeGood.Count) return false;
        if (!_research.MandateUnlocked(civ)) return false;
        int i = civ * _civs + target;
        byte bit = (byte)(1 << (int)good);
        bool was = (_embargo[i] & bit) != 0;
        if (was == on) return true;
        _embargo[i] = on ? (byte)(_embargo[i] | bit) : (byte)(_embargo[i] & ~bit);
        if (on)
        {
            if (_dip.Contacted(civ, target)) _dip.Shift(civ, target, _trade.EmbargoRelations);
            foreach (Compact c in _compacts)
                if (c.Active && c.Good == good && c.SellerCiv == civ && c.BuyerCiv == target) EndCompact(c, CompactEndReason.Embargo);
        }
        _events.Add(new PoliticsEvent(_tick, (byte)civ, PoliticsEventKind.EmbargoSet, target, (int)good, on ? 1 : 0));
        _events.Add(new PoliticsEvent(_tick, (byte)target, PoliticsEventKind.EmbargoSet, civ, (int)good, on ? 3 : 2));
        return true;
    }

    // ---- the season ------------------------------------------------------------------------------------

    /// <summary>Tin Folk work their placers; compacts check their terms, count their seasons and send a caravan when there is something to carry.</summary>
    private void RunTrade()
    {
        foreach (TinFolkVillage v in _tinFolk)
            if (v.Joined < 0 && unchecked((uint)v.Settlement) < unchecked((uint)_sCount) && _free[v.Settlement] && _sPop[v.Settlement] > 0)
                _economy.WorkTinPlacer(v.Settlement, v.Placer, FolkOrePerSeason);
        for (int p = _pacts.Count - 1; p >= 0; p--) if (_pacts[p].UntilSeason <= _season) _pacts.RemoveAt(p);
        foreach (Compact c in _compacts)
        {
            if (!c.Active) continue;
            CompactEndReason end = CompactBroken(c);
            if (end != CompactEndReason.None) { EndCompact(c, end); continue; }
            int folk = c.SellerFolk >= 0 ? c.SellerFolk : c.BuyerFolk;
            int civ = c.SellerCiv >= 0 ? c.SellerCiv : c.BuyerCiv;
            if (folk >= 0 && FolkTieOf(civ, folk, create: true) is { } tie) tie.CompactSeasons++;
            bool onRoad = false;
            foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan && e.CompactId == c.Id) { onRoad = true; break; }
            if (!onRoad) SendCaravan(c);
        }
    }

    private CompactEndReason CompactBroken(Compact c)
    {
        int seller = HubOf(c.SellerCiv, c.SellerFolk, c.Good, selling: true, -1);
        int buyer = HubOf(c.BuyerCiv, c.BuyerFolk, c.Good, selling: false, seller);
        if (seller < 0 || buyer < 0) return CompactEndReason.PartyGone;
        if (c.SellerFolk >= 0 && !_free[c.SellerFolk] || c.BuyerFolk >= 0 && !_free[c.BuyerFolk]) return CompactEndReason.PartyGone;
        if (c.SellerCiv >= 0 && c.BuyerCiv >= 0)
        {
            if (_dip.AtWar(c.SellerCiv, c.BuyerCiv)) return CompactEndReason.War;
            int min = Done(c.SellerCiv, _bp.Dip05) || Done(c.BuyerCiv, _bp.Dip05) ? _trade.MinRelationsDip05 : _trade.MinRelations;
            if (_dip.Relations(c.SellerCiv, c.BuyerCiv) < min) return CompactEndReason.Relations;
            if (Embargoes(c.SellerCiv, c.BuyerCiv, c.Good)) return CompactEndReason.Embargo;
        }
        else
        {
            int civ = c.SellerCiv >= 0 ? c.SellerCiv : c.BuyerCiv;
            int folk = c.SellerFolk >= 0 ? c.SellerFolk : c.BuyerFolk;
            if (FolkTieOf(civ, folk, create: false) is { Relations: < 0 }) return CompactEndReason.Relations;
        }
        if (RouteBlocked(c.SellerCiv, c.BuyerCiv, seller, buyer)) return CompactEndReason.Blocked;
        return CompactEndReason.None;
    }

    private void EndCompact(Compact c, CompactEndReason reason)
    {
        if (!c.Active) return;
        c.Active = false;
        c.EndedSeason = _season;
        c.EndReason = reason;
        int a = c.SellerCiv >= 0 ? c.SellerCiv : c.BuyerCiv;
        int b = c.SellerCiv >= 0 ? c.BuyerCiv : -1;
        if (a >= 0) _events.Add(new PoliticsEvent(_tick, (byte)a, PoliticsEventKind.CompactEnded, b, c.Id, (int)reason));
        if (b >= 0) _events.Add(new PoliticsEvent(_tick, (byte)b, PoliticsEventKind.CompactEnded, a, c.Id, (int)reason));
    }

    /// <summary>
    /// One delivery (docs/MVP/22 §6.2–6.3): the seller loads min(surplus, the buyer's next 4 seasons of need) if both still gain at the
    /// scarcity rate; two carriers walk it to the buyer (boats and carts as the route allows).
    /// </summary>
    private void SendCaravan(Compact c)
    {
        int seller = HubOf(c.SellerCiv, c.SellerFolk, c.Good, selling: true, -1);
        int buyer = HubOf(c.BuyerCiv, c.BuyerFolk, c.Good, selling: false, seller);
        if (seller < 0 || buyer < 0 || seller == buyer) return;
        Fix64 load = Fix64.Min(Surplus(seller, c.Good), Shortfall(buyer, c.Good));
        if (load < MinLoad) return;
        Fix64 vs = UnitValue(seller, c.Good), vb = UnitValue(buyer, c.Good);
        if (!BothGain(vs, vb, c.SellerCiv, c.BuyerCiv)) return;
        int mover = c.SellerCiv >= 0 ? c.SellerCiv : _sCiv[seller];
        int travel = Travel(c.SellerCiv >= 0 ? c.SellerCiv : c.BuyerCiv, _sTile[seller], _sTile[buyer]);
        if (travel == int.MaxValue) return;
        bool hostileRoute = c.Good == TradeGood.Tin && RoadCrossesHostileTerritory(mover, seller, buyer);
        bool canEscort = hostileRoute && CanProvideTwoWarriorEscorts(seller, mover);
        // Keep the Compact active and its goods at the seller until this route is safe or two escorts can be fielded.
        if (ShouldWaitForSafeCaravanRoute(hostileRoute, canEscort)) return;
        Fix64 taken = c.Good == TradeGood.Food
            ? _economy.TakeGoods(seller, load, LootPreference.FoodOnly)
            : _economy.TakeGood(seller, DecreeArgs.GoodId(c.Good), load);
        if (taken < MinLoad) { if (taken > Fix64.Zero) Unload(seller, c.Good, taken); return; }
        int partySize = canEscort ? 4 : 2;
        List<Fighter> crew = PickParty(seller, mover, partySize, attacking: false, out List<long> ids, stayHome: 4);
        var caravan = new Expedition
        {
            Kind = ExpeditionKind.Caravan, Civ = mover, TargetCiv = c.BuyerCiv, FromSettlement = seller, ToSettlement = buyer,
            ArriveSeason = _season + SeasonsFor(travel), Id = _nextPartyId++, Good = c.Good, Cargo = taken, CompactId = c.Id,
            FolkSeller = c.SellerFolk, Value = Fix64.Sqrt(vs * vb), EscortCount = canEscort ? Math.Min(2, Math.Max(0, ids.Count - 2)) : 0,
        };
        for (int i = 0; i < ids.Count; i++) { caravan.Members.Add(ids[i]); caravan.Strengths.Add(crew[i].Strength); }
        _expeditions.Add(caravan);
        c.LastSeller = seller;
        c.LastBuyer = buyer;
    }

    private void Unload(int settlement, TradeGood good, Fix64 amount)
    {
        if (good == TradeGood.Food) _economy.GiveGoods(settlement, amount);
        else _economy.PutGood(settlement, DecreeArgs.GoodId(good), amount);
    }

    private Compact? CompactById(int id)
    {
        foreach (Compact c in _compacts) if (c.Id == id) return c;
        return null;
    }

    /// <summary>A caravan arrives: the buyer takes the goods and pays at the agreed rate (food first); plague travels with it (docs/MVP/22 §10).</summary>
    private void ArriveCaravan(Expedition e)
    {
        Compact? c = CompactById(e.CompactId);
        int buyer = e.ToSettlement, seller = e.FromSettlement;
        bool buyerOk = unchecked((uint)buyer) < unchecked((uint)_sCount) && _sPop[buyer] > 0;
        if (!buyerOk || c is null)
        {
            if (unchecked((uint)seller) < unchecked((uint)_sCount)) Unload(seller, e.Good, e.Cargo);
            return;
        }
        Fix64 price = e.Value;
        Fix64 owed = e.Cargo * price;
        Fix64 paid = c.Good == TradeGood.Food ? PayInGoods(buyer, seller, owed) : _economy.TakeGoods(buyer, owed, LootPreference.FoodOnly);
        if (c.Good != TradeGood.Food && unchecked((uint)seller) < unchecked((uint)_sCount)) _economy.GiveGoods(seller, paid);
        Fix64 delivered = owed <= Fix64.Zero ? e.Cargo : Fix64.Min(e.Cargo, e.Cargo * paid / owed);
        Unload(buyer, e.Good, delivered);
        if (e.Cargo > delivered && unchecked((uint)seller) < unchecked((uint)_sCount)) Unload(seller, e.Good, e.Cargo - delivered);
        if (unchecked((uint)seller) < unchecked((uint)_sCount) && _economy.OutbreakAt(seller).Class == OutbreakClass.Plague)
            _economy.ImportOutbreak(buyer, OutbreakClass.Plague);
        c.Deliveries++;
        c.Delivered += delivered;
        c.LastVolume = delivered;
        if (c.SellerCiv >= 0 && c.BuyerCiv >= 0) _dip.Shift(c.SellerCiv, c.BuyerCiv, 1);
        else
        {
            int civ = c.SellerCiv >= 0 ? c.SellerCiv : c.BuyerCiv;
            int folk = c.SellerFolk >= 0 ? c.SellerFolk : c.BuyerFolk;
            if (FolkTieOf(civ, folk, create: true) is { } tie)
            {
                tie.Relations = Math.Min(Diplomacy.Max, tie.Relations + FolkDeliveryRelations);
                tie.Deliveries++;
            }
        }
    }

    /// <summary>A food compact is paid in the buyer's metals or luxuries (copper, tin, bronze, salt, gold, faience), the first it can spare.</summary>
    private Fix64 PayInGoods(int buyer, int seller, Fix64 owed)
    {
        Fix64 paid = Fix64.Zero;
        foreach (TradeGood g in new[] { TradeGood.Copper, TradeGood.Tin, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Luxury })
        {
            if (paid >= owed) break;
            Fix64 unit = _economy.GoodBarterValue(DecreeArgs.GoodId(g));
            if (unit <= Fix64.Zero) continue;
            Fix64 spare = Surplus(buyer, g);
            Fix64 want = Fix64.Min(spare, (owed - paid) / unit);
            if (want <= Fix64.Zero) continue;
            Fix64 moved = _economy.TakeGood(buyer, DecreeArgs.GoodId(g), want);
            if (unchecked((uint)seller) < unchecked((uint)_sCount)) _economy.PutGood(seller, DecreeArgs.GoodId(g), moved);
            paid += moved * unit;
        }
        return paid;
    }

    // ---- raids on caravans (docs/MVP/22 §6.3, §7.5) -------------------------------------------------------

    /// <summary>The caravans on the road, for raid targeting and the view.</summary>
    public IEnumerable<Expedition> Caravans()
    {
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan) yield return e;
    }

    /// <summary>Whether the caravan's straight road crosses a civilization's territory whose Relations with its mover are negative.</summary>
    public bool CaravanCrossesHostileTerritory(Expedition caravan)
    {
        if (caravan.Kind != ExpeditionKind.Caravan || unchecked((uint)caravan.Civ) >= unchecked((uint)_civs) ||
            unchecked((uint)caravan.FromSettlement) >= unchecked((uint)_sCount) ||
            unchecked((uint)caravan.ToSettlement) >= unchecked((uint)_sCount)) return false;
        int from = PartyTile(caravan);
        return from >= 0 && RoadSegmentCrossesHostileTerritory(caravan.Civ, from, _sTile[caravan.ToSettlement]);
    }

    private bool RoadCrossesHostileTerritory(int mover, int fromSettlement, int toSettlement)
        => RoadSegmentCrossesHostileTerritory(mover, _sTile[fromSettlement], _sTile[toSettlement]);

    private bool RoadSegmentCrossesHostileTerritory(int mover, int from, int to)
    {
        HexGrid.Coordinates(from, _width, out int fx, out int fy);
        HexGrid.Coordinates(to, _width, out int tx, out int ty);
        int steps = Math.Max(1, HexGrid.Distance(from, to, _width));
        // Include the party's current tile: it can become hostile while the caravan is en route.
        for (int k = 0; k < steps; k++)
        {
            int tile = HexGrid.Index(fx + (tx - fx) * k / steps, fy + (ty - fy) * k / steps, _width, _height);
            int owner = _territory.OwnerOf(tile);
            if (owner >= 0 && owner != mover && _dip.Relations(mover, owner) < 0) return true;
        }
        return false;
    }

    private bool CanProvideTwoWarriorEscorts(int settlement, int civ)
    {
        if (unchecked((uint)civ) >= unchecked((uint)_civs) || !Done(civ, _bp.War04)) return false;
        var records = new List<LivingEconomyModel.FighterRecord>();
        _economy.ListFightingAge(settlement, records);
        int adults = 0, available = 0, warriors = 0;
        foreach (var r in records) if (r.Age < 45 && !r.Wounded) adults++;
        int slots = adults / 5;
        foreach (var r in records)
        {
            if (r.Wounded) continue;
            available++;
            if (r.Age < 45 && !r.Female && slots > 0) { warriors++; slots--; }
        }
        return warriors >= 2 && available >= 8;
    }

    internal static bool ShouldWaitForSafeCaravanRoute(bool hostileRoute, bool canEscort)
        => hostileRoute && !canEscort;

    public bool CaravanIsNaked(Expedition caravan)
        => CaravanCrossesHostileTerritory(caravan) && caravan.EscortCount < 2;

    /// <summary>Where a party is now (its tile by progress along the straight road), for targeting.</summary>
    public int PartyTile(Expedition e)
    {
        int from = unchecked((uint)e.FromSettlement) < unchecked((uint)_sCount) ? _sTile[e.FromSettlement] : -1;
        int to = unchecked((uint)e.ToSettlement) < unchecked((uint)_sCount) ? _sTile[e.ToSettlement] : e.TargetTile;
        if (from < 0 || to < 0) return Math.Max(from, to);
        if (e.Kind == ExpeditionKind.Explore && e.Leg == 1) (from, to) = (to, from);
        int total = Math.Max(1, TravelSeasons(e.Civ, from, to) == int.MaxValue ? 1 : TravelSeasons(e.Civ, from, to));
        int left = Math.Clamp(e.ArriveSeason - _season, 0, total);
        HexGrid.Coordinates(from, _width, out int fx, out int fy);
        HexGrid.Coordinates(to, _width, out int tx, out int ty);
        int done = total - left;
        return HexGrid.Index(fx + (tx - fx) * done / total, fy + (ty - fy) * done / total, _width, _height);
    }

    /// <summary>
    /// A raid on a caravan (command 12 with A = −(caravan id + 1)): a party from the nearest own settlement that can spare it intercepts
    /// before the caravan arrives. Hard rules: WAR-02, the caravan is not ours, contact with its owner (if a civilization), no alliance,
    /// truce or pact with it.
    /// </summary>
    public bool OrderRaidCaravan(int c, int caravanId, int partySize)
    {
        if (!_research.IsDone(c, _p.War02)) return false;
        Expedition? target = null;
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan && e.Id == caravanId) { target = e; break; }
        if (target is null || target.Civ == c && target.FolkSeller < 0) return false;
        foreach (int o in new[] { target.FolkSeller < 0 ? target.Civ : -1, target.TargetCiv })
        {
            if (o < 0 || o == c) continue;
            if (!_dip.Contacted(c, o) || _dip.Allied(c, o) || _dip.InTruce(c, o, _season) || HasPact(c, o)) return false;
        }
        int at = PartyTile(target);
        int origin = BestOrigin(c, at);
        if (origin < 0) return false;
        int travel = Travel(c, _sTile[origin], at);
        if (travel == int.MaxValue || travel > TilesPerSeason * Math.Max(1, target.ArriveSeason - _season)) return false;
        List<Fighter> party = PickParty(origin, c, Math.Max(4, partySize), attacking: true, out List<long> ids, battleTile: at);
        if (party.Count < 4) return false;
        var raid = new Expedition
        {
            Kind = ExpeditionKind.Raid, Civ = c, TargetCiv = target.FolkSeller < 0 ? target.Civ : target.TargetCiv, FromSettlement = origin,
            ToSettlement = -1, TargetTile = at, TargetParty = caravanId, ArriveSeason = _season + 1,
        };
        for (int i = 0; i < ids.Count; i++) { raid.Members.Add(ids[i]); raid.Strengths.Add(party[i].Strength); }
        _expeditions.Add(raid);
        RaidCount++;
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.RaidLaunched, raid.TargetCiv, party.Count, -1));
        return true;
    }

    /// <summary>A raid on a directly sighted mining camp; the target is the camp's stable id, not its mutable list index.</summary>
    public bool OrderRaidMiningCamp(int c, int campId, int partySize)
    {
        if (!_economy.BronzeActive || !_research.IsDone(c, _p.War02) || !_economy.TryMiningCampById(campId, out MiningCampView camp) ||
            camp.Crew < 2 || camp.OreLeft <= Fix64.Zero) return false;
        int owner = camp.Civilization;
        if (owner == c || !_dip.Contacted(c, owner) || _dip.Allied(c, owner) || _dip.InTruce(c, owner, _season) || HasPact(c, owner)) return false;
        int origin = BestOrigin(c, camp.Tile);
        if (origin < 0) return false;
        int travel = Travel(c, _sTile[origin], camp.Tile);
        if (travel == int.MaxValue) return false;
        List<Fighter> party = PickParty(origin, c, Math.Max(4, partySize), attacking: true, out List<long> ids, battleTile: camp.Tile);
        if (party.Count < 4) return false;
        var raid = new Expedition
        {
            Kind = ExpeditionKind.Raid, Civ = c, TargetCiv = owner, FromSettlement = origin,
            ToSettlement = -1, TargetTile = camp.Tile, TargetCampId = camp.Id,
            ArriveSeason = _season + SeasonsFor(travel),
        };
        for (int i = 0; i < ids.Count; i++) { raid.Members.Add(ids[i]); raid.Strengths.Add(party[i].Strength); }
        _expeditions.Add(raid);
        RaidCount++;
        _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.MiningCampRaidLaunched, owner, party.Count, camp.Tile));
        return true;
    }

    /// <summary>The raiders meet the caravan on the road: its crew fights in the open; on success the cargo is theirs (docs/MVP/22 §6.3).</summary>
    private void ArriveCaravanRaid(Expedition raid)
    {
        Expedition? caravan = null;
        foreach (Expedition e in _expeditions) if (e.Kind == ExpeditionKind.Caravan && e.Id == raid.TargetParty) { caravan = e; break; }
        if (caravan is null) return;
        List<Fighter> raiders = AliveFighters(raid, out _);
        if (raiders.Count < 3) return;
        List<Fighter> crew = AliveFighters(caravan, out _);
        Weapon(raid.Civ, out bool ranged);
        RaidOutcome outcome = Raids.Resolve(raiders, crew, Defenses.None, ranged, _seed,
            RaidStream ^ 0x434152415641UL ^ (ulong)(raid.Civ * 4099 + caravan.Id * 97) ^ (ulong)_season * 104729UL);
        ApplyCasualties(outcome.Battle.AttackerDead, outcome.Battle.AttackerWounded);
        ApplyCasualties(outcome.Battle.DefenderDead, outcome.Battle.DefenderWounded);
        Fix64 loot = Fix64.Zero;
        if (outcome.Success || crew.Count == 0)
        {
            loot = caravan.Cargo;
            _expeditions.Remove(caravan);
            if (unchecked((uint)raid.FromSettlement) < unchecked((uint)_sCount)) Unload(raid.FromSettlement, caravan.Good, loot);
            RaidsSucceeded++;
        }
        Compact? c = CompactById(caravan.CompactId);
        int lootPermille = (loot * 1000).FloorToInt();
        // Seller and buyer lose Relations with the raider (−30) and the raider's reputation falls 10 (docs/MVP/22 §6.3).
        foreach (int victim in new[] { c?.SellerCiv ?? -1, c?.BuyerCiv ?? -1 })
        {
            if (victim < 0 || victim == raid.Civ) continue;
            _dip.Shift(raid.Civ, victim, _trade.CaravanRaidRelations);
            _dip.RecordRaid(raid.Civ, victim, _season);
            _events.Add(new PoliticsEvent(_tick, (byte)victim, PoliticsEventKind.CaravanRaided, raid.Civ, caravan.CompactId, lootPermille));
        }
        int folk = c is null ? -1 : c.SellerFolk >= 0 ? c.SellerFolk : c.BuyerFolk;
        if (folk >= 0 && FolkTieOf(raid.Civ, folk, create: true) is { } tie) tie.Relations += _trade.CaravanRaidRelations;
        _dip.AdjustReputation(raid.Civ, _trade.CaravanRaidReputation);
        _events.Add(new PoliticsEvent(_tick, (byte)raid.Civ, PoliticsEventKind.CaravanRaided, c?.SellerCiv ?? -1, caravan.CompactId, outcome.Success ? lootPermille : -1));
    }

    // ---- goods barter (docs/MVP/22 §6.1) ---------------------------------------------------------------

    private static readonly TradeGood[] BarterGoods = { TradeGood.Tin, TradeGood.Copper, TradeGood.Bronze, TradeGood.Salt, TradeGood.Gold, TradeGood.Luxury };

    /// <summary>
    /// In the Bronze Age a barter trip carries goods, not only food (docs/MVP/22 §6.1, 13 §4.2): the partner answers the visitors' food
    /// with the trade good the two value most differently, when both gain at the geometric-mean price. Returns the good and quantity
    /// loaded, or (None, 0) to fall back to food for food.
    /// </summary>
    private (TradeGood Good, Fix64 Quantity) ChooseBarterGood(int visitorCiv, int visitorHome, int partner, Fix64 foodValue)
    {
        if (!_economy.BronzeActive || foodValue <= Fix64.Zero) return (TradeGood.None, Fix64.Zero);
        TradeGood best = TradeGood.None;
        Fix64 bestRatio = Fix64.Zero, bestPrice = Fix64.Zero;
        int partnerCiv = _sCiv[partner];
        foreach (TradeGood g in BarterGoods)
        {
            if (!_free[partner] && Embargoes(partnerCiv, visitorCiv, g)) continue;
            if (Surplus(partner, g) <= MinLoad) continue;
            Fix64 vSeller = UnitValue(partner, g), vBuyer = UnitValue(visitorHome, g);
            if (!BothGain(vSeller, vBuyer, _free[partner] ? -1 : partnerCiv, visitorCiv)) continue;
            Fix64 ratio = vBuyer / vSeller;
            if (ratio > bestRatio) { best = g; bestRatio = ratio; bestPrice = Fix64.Sqrt(vSeller * vBuyer); }
        }
        if (best == TradeGood.None || bestPrice <= Fix64.Zero) return (TradeGood.None, Fix64.Zero);
        Fix64 quantity = Fix64.Min(Surplus(partner, best), foodValue / bestPrice);
        Fix64 taken = _economy.TakeGood(partner, DecreeArgs.GoodId(best), quantity);
        return taken > Fix64.Zero ? (best, taken) : (TradeGood.None, Fix64.Zero);
    }

    // ---- Tin Folk (docs/MVP/22 §6.4) ---------------------------------------------------------------

    /// <summary>
    /// At the first KEY-3 in the world every far or island tin placer (quantity tier 1) that no civilization owns and that lies at
    /// least 8 tiles from any settlement gets a Tin Folk village of 24 on the nearest open tile beside it. Once per world.
    /// </summary>
    private void SpawnTinFolk()
    {
        if (_tinFolkSpawned || !_economy.BronzeActive) return;
        _tinFolkSpawned = true;
        var sites = new List<int>();
        for (int tile = 0; tile < _tileArray.Length; tile++)
        {
            WorldTile t = _tileArray[tile];
            if (t.Deposit != DepositKind.Tin || t.DepositAmount != 1 || _territory.OwnerOf(tile) >= 0) continue;
            int site = FolkSite(tile);
            if (site < 0) continue;
            bool clear = true;
            for (int s = 0; s < _sCount && clear; s++)
                if (_sPop[s] > 0 && HexGrid.Distance(_sTile[s], tile, _width) < _trade.FolkMinDist) clear = false;
            foreach (int other in sites) if (HexGrid.Distance(other, site, _width) < _trade.FolkMinDist) clear = false;
            if (!clear) continue;
            sites.Add(site);
            int nominal = NearestCivTo(site);
            if (nominal < 0) continue;
            int village = _economy.SpawnVillage(nominal, site, _trade.FolkAdults, _trade.FolkElders, _trade.FolkChildren);
            RefreshSettlements();
            _free[village] = true;
            _tinFolk.Add(new TinFolkVillage { Settlement = village, Placer = tile, SinceSeason = _season });
            for (int c = 0; c < _civs; c++)
                _events.Add(new PoliticsEvent(_tick, (byte)c, PoliticsEventKind.TinFolkAppeared, -1, village, tile));
        }
    }

    /// <summary>The village stands beside its placer: the nearest habitable tile within 2 that holds no deposit, lowest index on ties; else the placer itself.</summary>
    private int FolkSite(int placer)
    {
        int best = -1, bestDistance = int.MaxValue;
        HexGrid.Coordinates(placer, _width, out int px, out int py);
        for (int y = Math.Max(0, py - 2); y <= Math.Min(_height - 1, py + 2); y++)
            for (int x = Math.Max(0, px - 3); x <= Math.Min(_width - 1, px + 3); x++)
            {
                int t = y * _width + x;
                int d = HexGrid.Distance(placer, t, _width);
                if (d == 0 || d > 2 || !_economy.IsHabitableSite(t) || _tileArray[t].Deposit != DepositKind.None) continue;
                if (d < bestDistance || d == bestDistance && t < best) { best = t; bestDistance = d; }
            }
        if (best >= 0) return best;
        return _economy.IsHabitableSite(placer) ? placer : -1;
    }

    private int NearestCivTo(int tile)
    {
        int best = -1, bestDistance = int.MaxValue;
        for (int s = 0; s < _sCount; s++)
        {
            if (_free[s] || _sPop[s] <= 0) continue;
            int d = HexGrid.Distance(_sTile[s], tile, _width);
            if (d < bestDistance) { bestDistance = d; best = _sCiv[s]; }
        }
        return best;
    }

    // ---- hashing and snapshots -------------------------------------------------------------------------

    private void HashTrade(ref Core.StateHasher h)
    {
        h.Add(_tinFolkSpawned); h.Add(_nextCompactId);
        h.Add(_compacts.Count);
        foreach (Compact c in _compacts)
        {
            h.Add(c.Id); h.Add(c.SellerCiv); h.Add(c.SellerFolk); h.Add(c.BuyerCiv); h.Add(c.BuyerFolk); h.Add(c.Proposer); h.Add((byte)c.Good);
            h.Add(c.SinceSeason); h.Add(c.Deliveries); h.Add(c.Delivered); h.Add(c.LastVolume); h.Add(c.Active); h.Add(c.EndedSeason); h.Add((byte)c.EndReason);
            h.Add(c.LastSeller); h.Add(c.LastBuyer);
        }
        h.Add(_pacts.Count);
        foreach (Pact p in _pacts) { h.Add(p.A); h.Add(p.B); h.Add((byte)p.Kind); h.Add(p.SinceSeason); h.Add(p.UntilSeason); }
        h.Add(_folkTies.Count);
        foreach (FolkTie t in _folkTies) { h.Add(t.Folk); h.Add(t.Civ); h.Add(t.Relations); h.Add(t.CompactSeasons); h.Add(t.Deliveries); }
        h.Add(_tinFolk.Count);
        foreach (TinFolkVillage v in _tinFolk) { h.Add(v.Settlement); h.Add(v.Placer); h.Add(v.SinceSeason); h.Add(v.Joined); }
        foreach (byte b in _embargo) h.Add(b);
    }

    private void WriteTrade(System.IO.BinaryWriter w)
    {
        w.Write(_tinFolkSpawned); w.Write(_nextCompactId);
        w.Write(_compacts.Count);
        foreach (Compact c in _compacts)
        {
            w.Write(c.Id); w.Write(c.SellerCiv); w.Write(c.SellerFolk); w.Write(c.BuyerCiv); w.Write(c.BuyerFolk); w.Write(c.Proposer); w.Write((byte)c.Good);
            w.Write(c.SinceSeason); w.Write(c.Deliveries); w.Write(c.Delivered.Raw); w.Write(c.LastVolume.Raw); w.Write(c.Active); w.Write(c.EndedSeason); w.Write((byte)c.EndReason);
            w.Write(c.LastSeller); w.Write(c.LastBuyer);
        }
        w.Write(_pacts.Count);
        foreach (Pact p in _pacts) { w.Write(p.A); w.Write(p.B); w.Write((byte)p.Kind); w.Write(p.SinceSeason); w.Write(p.UntilSeason); }
        w.Write(_folkTies.Count);
        foreach (FolkTie t in _folkTies) { w.Write(t.Folk); w.Write(t.Civ); w.Write(t.Relations); w.Write(t.CompactSeasons); w.Write(t.Deliveries); }
        w.Write(_tinFolk.Count);
        foreach (TinFolkVillage v in _tinFolk) { w.Write(v.Settlement); w.Write(v.Placer); w.Write(v.SinceSeason); w.Write(v.Joined); }
        w.Write(_embargo);
    }

    private void ReadTrade(System.IO.BinaryReader r)
    {
        _tinFolkSpawned = r.ReadBoolean(); _nextCompactId = r.ReadInt32();
        _compacts.Clear();
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++)
            _compacts.Add(new Compact
            {
                Id = r.ReadInt32(), SellerCiv = r.ReadInt32(), SellerFolk = r.ReadInt32(), BuyerCiv = r.ReadInt32(), BuyerFolk = r.ReadInt32(),
                Proposer = r.ReadInt32(), Good = (TradeGood)r.ReadByte(), SinceSeason = r.ReadInt32(), Deliveries = r.ReadInt32(),
                Delivered = Fix64.FromRaw(r.ReadInt64()), LastVolume = Fix64.FromRaw(r.ReadInt64()), Active = r.ReadBoolean(),
                EndedSeason = r.ReadInt32(), EndReason = (CompactEndReason)r.ReadByte(), LastSeller = r.ReadInt32(), LastBuyer = r.ReadInt32(),
            });
        _pacts.Clear();
        n = r.ReadInt32();
        for (int i = 0; i < n; i++)
            _pacts.Add(new Pact { A = r.ReadInt32(), B = r.ReadInt32(), Kind = (CompactVariant)r.ReadByte(), SinceSeason = r.ReadInt32(), UntilSeason = r.ReadInt32() });
        _folkTies.Clear();
        n = r.ReadInt32();
        for (int i = 0; i < n; i++)
            _folkTies.Add(new FolkTie { Folk = r.ReadInt32(), Civ = r.ReadInt32(), Relations = r.ReadInt32(), CompactSeasons = r.ReadInt32(), Deliveries = r.ReadInt32() });
        _tinFolk.Clear();
        n = r.ReadInt32();
        for (int i = 0; i < n; i++)
            _tinFolk.Add(new TinFolkVillage { Settlement = r.ReadInt32(), Placer = r.ReadInt32(), SinceSeason = r.ReadInt32(), Joined = r.ReadInt32() });
        byte[] embargo = r.ReadBytes(_embargo.Length);
        if (embargo.Length != _embargo.Length) throw new System.IO.InvalidDataException("Snapshot embargo table is truncated.");
        Array.Copy(embargo, _embargo, embargo.Length);
    }
}
