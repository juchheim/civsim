using System.Collections.Generic;
using CivSim.Sim.Numerics;

namespace CivSim.Sim.Politics;

/// <summary>The six Decrees (docs/MVP/22 §4.3). Values are command 18's A argument and part of the replay format: append only.</summary>
public enum DecreeKind : byte { None = 0, Found = 1, Expedition = 2, Mine = 3, Levy = 4, Corvee = 5, Compact = 6 }

/// <summary>The goods a Compact can carry (docs/MVP/22 §4.3, §6.2). Values are packed into command arguments: append only.</summary>
public enum TradeGood : byte { None = 0, Tin = 1, Copper = 2, Bronze = 3, Salt = 4, Gold = 5, Food = 6, Luxury = 7, Count = 8 }

/// <summary>What a Compact Decree proposes (docs/MVP/22 §4.3, §6.5): a goods route, an Invite, a non-aggression pact (DIP-04) or a border treaty (DIP-05).</summary>
public enum CompactVariant : byte { Goods = 0, Invite = 1, Pact = 2, Treaty = 3 }

/// <summary>Who issued a Decree: a player command (kind 18), an AI Strategist through <c>IStrategistActions</c>, or the Council's policy.</summary>
public enum DecreeOrigin : byte { Command = 0, Strategist = 1, Council = 2 }

/// <summary>Why a compact ended (docs/MVP/22 §6.2).</summary>
public enum CompactEndReason : byte { None = 0, Relations = 1, War = 2, Embargo = 3, Blocked = 4, PartyGone = 5, Joined = 6 }

/// <summary>
/// Packing of the Decree arguments (docs/MVP/22 §4.5: B = target tile, settlement or civilization; C = packed extra). A Compact's
/// partner is a civilization (0..n-1) or a Free Folk village (−(settlement + 1)); its C carries the good in bits 0–7, the direction in
/// bit 8 (0 we buy, 1 we sell) and the variant in bits 9–11. A Corvée's C is the structure aimed at
/// (<see cref="Economy.LivingEconomyModel.CorveeTarget"/>).
/// </summary>
public static class DecreeArgs
{
    public static int FolkParty(int settlement) => -(settlement + 1);
    public static bool IsFolkParty(int target) => target < 0;
    public static int FolkSettlement(int target) => -target - 1;

    public static int PackCompact(TradeGood good, bool sell, CompactVariant variant = CompactVariant.Goods)
        => (int)good | (sell ? 1 << 8 : 0) | ((int)variant << 9);

    public static TradeGood Good(int extra) => (TradeGood)(extra & 0xFF);
    public static bool Sell(int extra) => (extra & (1 << 8)) != 0;
    public static CompactVariant Variant(int extra) => (CompactVariant)((extra >> 9) & 0x7);

    /// <summary>The economy's good id for a trade good (food moves as rations).</summary>
    public static string GoodId(TradeGood good) => good switch
    {
        TradeGood.Tin => "G-TIN", TradeGood.Copper => "G-COPPER", TradeGood.Bronze => "G-BRONZE", TradeGood.Salt => "G-SALT",
        TradeGood.Gold => "G-GOLD", TradeGood.Food => Economy.LivingEconomyModel.FoodGoodId, TradeGood.Luxury => "G-FAIENCE",
        _ => "",
    };

    /// <summary>The Decree id the content's <c>mandate</c> effects and DecreeCost constants use.</summary>
    public static string ContentId(DecreeKind kind) => kind switch
    {
        DecreeKind.Found => "found", DecreeKind.Expedition => "expedition", DecreeKind.Mine => "mine", DecreeKind.Levy => "levy",
        DecreeKind.Corvee => "corvee", DecreeKind.Compact => "compact", _ => "",
    };
}

/// <summary>A Decree waiting for the politics step at the end of the season (docs/MVP/22 §4.5).</summary>
public readonly record struct PendingDecree(int Civ, DecreeKind Kind, int Target, int Extra, long Tick, DecreeOrigin Origin);

/// <summary>What a Decree policy asks for: the same arguments as command 18.</summary>
public readonly record struct DecreeRequest(DecreeKind Kind, int Target, int Extra);

/// <summary>
/// Chooses Decrees for a civilization each season (docs/MVP/22 §4.6). The Council's hands-off policy is the default
/// (<see cref="CouncilDecreePolicy"/>); the AI work package plugs its Strategist in with <see cref="PoliticsSystem.SetDecreePolicy"/>
/// or issues through <c>IStrategistActions</c> and sets the policy to null. A policy must be a pure function of the state it reads:
/// it holds nothing that a snapshot would have to carry.
/// </summary>
public interface IDecreePolicy
{
    void Propose(PoliticsSystem politics, int civ, List<DecreeRequest> into);
}

/// <summary>
/// How the receiver of a Compact, pact or treaty decides (docs/MVP/22 §4.3, §6.2: the receiver's Strategist by expected value).
/// The default is the rule of 22 §4.3 (both sides gain ≥ 10%, 5% with NUM-04, and Relations ≥ 0); the AI work package may plug in
/// its forecast for a civilization it runs.
/// </summary>
public interface ICompactJudge
{
    /// <summary>Whether <paramref name="receiver"/> accepts. <paramref name="sellerValue"/> and <paramref name="buyerValue"/> are each side's value of one unit.</summary>
    bool Accepts(PoliticsSystem politics, int proposer, int receiver, CompactVariant variant, TradeGood good, bool receiverSells,
        Fix64 sellerValue, Fix64 buyerValue);
}

/// <summary>
/// A levy host (docs/MVP/22 §4.3 <c>Raise the levy</c>): for two seasons all warriors and half the other adults of the settlements
/// within two seasons' travel stand ready against one target. The next raid, assault or investment on that target uses it
/// (<see cref="PoliticsSystem.TryTakeLevy"/>); the host does no work while it stands.
/// </summary>
public sealed class Levy
{
    public int Civ;
    public int TargetSettlement;
    public int SinceSeason;
    /// <summary>The first season the host has dispersed.</summary>
    public int UntilSeason;
    public readonly List<long> Members = new();
    public readonly List<Fix64> Strengths = new();
}

/// <summary>
/// A standing goods route (docs/MVP/22 §6.2). Each party is a civilization or a Free Folk village: <see cref="SellerCiv"/> is the
/// civilization or -1 with <see cref="SellerFolk"/> the village's settlement index (-1 for a civilization); the same for the buyer.
/// Each season the seller sends a caravan with min(surplus, the buyer's next 4 seasons of need) and the buyer pays at the scarcity rate.
/// </summary>
public sealed class Compact
{
    public int Id;
    public int SellerCiv = -1, SellerFolk = -1, BuyerCiv = -1, BuyerFolk = -1;
    /// <summary>The civilization that proposed it (the Decree's issuer).</summary>
    public int Proposer;
    public TradeGood Good;
    public int SinceSeason;
    public int Deliveries;
    /// <summary>Goods delivered so far, and by the last caravan (in the good's unit; food in rations).</summary>
    public Fix64 Delivered, LastVolume;
    public bool Active = true;
    public int EndedSeason = -1;
    public CompactEndReason EndReason;
    /// <summary>The hubs the last caravan ran between (-1 before the first): the route the economy's market towns and epidemic inflow count.</summary>
    public int LastSeller = -1, LastBuyer = -1;

    public bool Involves(int civ) => SellerCiv == civ || BuyerCiv == civ;
    public bool InvolvesFolk(int settlement) => SellerFolk == settlement || BuyerFolk == settlement;
}

/// <summary>A non-aggression pact (DIP-04, 8 years) or a border treaty (DIP-05, 10 years) between two civilizations (docs/MVP/22 §6.5).</summary>
public sealed class Pact
{
    public int A, B;
    public CompactVariant Kind;
    public int SinceSeason, UntilSeason;
}

/// <summary>Relations between a civilization and a Free Folk village (docs/MVP/22 §4.3 Invite): they start at 10 with the first compact and grow 3 a delivery.</summary>
public sealed class FolkTie
{
    public int Folk, Civ;
    public int Relations;
    /// <summary>Seasons with an active compact between the two (the Invite needs 8).</summary>
    public int CompactSeasons;
    public int Deliveries;
}

/// <summary>
/// A Tin Folk village (docs/MVP/22 §6.4): a Free Folk settlement that appeared at the world's first KEY-3 next to an unclaimed far or
/// island tin placer. It works the placer, trades and can be invited or taken. <see cref="Joined"/> is the civilization it joined by
/// an Invite (-1 while free). Kept simple on purpose: the claim and capture rules of 22 §11 read these fields.
/// </summary>
public sealed class TinFolkVillage
{
    public int Settlement;
    public int Placer;
    public int SinceSeason;
    public int Joined = -1;
}

/// <summary>
/// The forecast card of a Decree (docs/MVP/22 §4.4), computed from what the issuing civilization knows: its own state exactly and
/// everyone else's only as far as it has seen (fog-fair). Permille values are probabilities; the rest are expectations.
/// </summary>
public readonly record struct DecreeForecast(
    DecreeKind Kind, bool Allowed, OrderRefusalReason LikelyRefusal, int Cost, int MandateAfter, int SuccessPermille,
    int People, int Tiles, Fix64 Goods, int RelationsDelta, int Seasons,
    Fix64 LandWeight = default, Fix64 Dominion = default, Fix64 Information = default,
    Fix64 MetalValue = default, Fix64 WallEtaSeasons = default, int ThreatEtaSeasons = -1, Fix64 ThreatMarginSeasons = default);
