using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rules of trade at the Trading Post (Docs/TradeResearch_2026-11-28.md): which traders come (TraderDefinition), the coin that
/// balances a barter, and the numbers TradeRules prices with: the shares by standing, what quality and wear do to a piece, how
/// selling the same thing floods a trader, haggling and standing. Lives at Resources/TradeSettings; edit it in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Trade Settings", fileName = "TradeSettings")]
public sealed class TradeSettings : GameSettings<TradeSettings>
{
    [Tooltip("What balances a barter: paid for what is taken beyond what is given, and paid back for the rest. Worth its own Value.")]
    [SerializeField] private ItemDefinition coin;
    [Tooltip("Every trader who may come to the post.")]
    [SerializeField] private List<TraderDefinition> traders = new List<TraderDefinition>();

    [Header("Standing")]
    [Tooltip("Names of the standing tiers, from 0 standing up (Stranger, Known, Trusted, Friend).")]
    [SerializeField] private string[] tierNames = { "Stranger", "Known", "Trusted", "Friend" };
    [Tooltip("Standing each tier starts at (as many as the names).")]
    [SerializeField] private float[] tierFrom = { 0f, 25f, 50f, 75f };
    [Tooltip("What a trader pays for a thing, as a share of its worth, by tier (RimWorld: 60%).")]
    [SerializeField] private float[] sellShare = { 0.55f, 0.6f, 0.67f, 0.75f };
    [Tooltip("What a trader asks for a thing, as a share of its worth, by tier (RimWorld: 140%).")]
    [SerializeField] private float[] buyShare = { 1.45f, 1.35f, 1.25f, 1.15f };
    [Tooltip("Worth traded (both ways) for one standing.")]
    [SerializeField, Min(1f)] private float worthPerStanding = 120f;
    [Tooltip("Worth given as a gift for one standing, below Gift Slows At.")]
    [SerializeField, Min(1f)] private float giftWorthPerStanding = 40f;
    [Tooltip("Worth given as a gift for one standing from Gift Slows At on (RimWorld: gifts raise goodwill less the higher it is).")]
    [SerializeField, Min(1f)] private float giftWorthPerStandingHigh = 100f;
    [SerializeField, Range(0f, 100f)] private float giftSlowsAt = 50f;

    [Header("What a piece is worth")]
    [Tooltip("A Fine piece's worth against a plain one's (Value × this). 1.8 pays what the old caravan did.")]
    [SerializeField, Min(1f)] private float fineWorth = 1.8f;
    [SerializeField, Min(1f)] private float masterworkWorth = 4.5f;
    [Tooltip("Valuables (relics, idols) fetch this much more: they were made to be sold.")]
    [SerializeField, Min(1f)] private float valuableMultiplier = 1.8f;
    [Tooltip("Durability from which a piece keeps its whole worth.")]
    [SerializeField, Range(0f, 1f)] private float fullWorthFrom = 0.9f;
    [Tooltip("Share of worth a broken piece keeps.")]
    [SerializeField, Range(0f, 1f)] private float brokenWorth = 0.25f;

    [Header("Supply and want")]
    [Tooltip("Each unit of a thing sold to a trader makes their next one worth this much of the last (Slime Rancher-style).")]
    [SerializeField, Range(0.5f, 1f)] private float perUnitSold = 0.96f;
    [Tooltip("Never below this share, however much they have been sold.")]
    [SerializeField, Range(0.05f, 1f)] private float floodFloor = 0.4f;
    [Tooltip("Share of what they have been sold that they forget after each raid survived.")]
    [SerializeField, Range(0f, 1f)] private float forgetPerRaid = 0.34f;
    [Tooltip("What a thing they want this visit fetches more.")]
    [SerializeField, Min(1f)] private float wantMultiplier = 1.5f;
    [Tooltip("What they ask for a thing is never below this times the most anyone would pay for it: no buying low to sell high.")]
    [SerializeField, Min(1f)] private float arbitrageMargin = 1.05f;

    [Header("Haggling")]
    [SerializeField, Range(0f, 1f)] private float haggleChance = 0.5f;
    [Tooltip("More chance per point of standing.")]
    [SerializeField, Range(0f, 0.05f)] private float haggleChancePerStanding = 0.004f;
    [SerializeField, Range(0f, 1f)] private float haggleChanceMost = 0.9f;
    [Tooltip("A won haggle: their prices this much better for the visit (both ways).")]
    [SerializeField, Range(0f, 0.5f)] private float haggleGain = 0.1f;
    [Tooltip("A lost haggle: standing lost.")]
    [SerializeField, Min(0f)] private float haggleCost = 3f;

    [Header("Asks")]
    [Tooltip("Homecomings an ask stands before it is taken down.")]
    [SerializeField, Min(1)] private int askLasts = 3;

    public ItemDefinition Coin => coin;
    /// <summary>What a coin is worth in a barter (its Value, at least 1).</summary>
    public float CoinWorth => coin != null ? Mathf.Max(1, coin.Value) : 10f;
    public IReadOnlyList<TraderDefinition> Traders => traders;
    public int TierCount => Mathf.Min(tierNames.Length, Mathf.Min(tierFrom.Length, Mathf.Min(sellShare.Length, buyShare.Length)));
    public string TierName(int tier) => tierNames[Mathf.Clamp(tier, 0, TierCount - 1)];
    public float TierFrom(int tier) => tierFrom[Mathf.Clamp(tier, 0, TierCount - 1)];
    public float SellShare(int tier) => sellShare[Mathf.Clamp(tier, 0, TierCount - 1)];
    public float BuyShare(int tier) => buyShare[Mathf.Clamp(tier, 0, TierCount - 1)];
    public float WorthPerStanding => worthPerStanding;
    public float GiftWorthPerStanding(float standing) => standing < giftSlowsAt ? giftWorthPerStanding : giftWorthPerStandingHigh;
    public float FineWorth => fineWorth;
    public float MasterworkWorth => masterworkWorth;
    public float ValuableMultiplier => valuableMultiplier;
    public float FullWorthFrom => fullWorthFrom;
    public float BrokenWorth => brokenWorth;
    public float PerUnitSold => perUnitSold;
    public float FloodFloor => floodFloor;
    public float ForgetPerRaid => forgetPerRaid;
    public float WantMultiplier => wantMultiplier;
    public float ArbitrageMargin => arbitrageMargin;
    public float HaggleGain => haggleGain;
    public float HaggleCost => haggleCost;
    public int AskLasts => askLasts;

    public float HaggleChanceAt(float standing) => Mathf.Min(haggleChanceMost, haggleChance + haggleChancePerStanding * Mathf.Max(0f, standing));

    public TraderDefinition FindTrader(string id)
    {
        foreach (TraderDefinition trader in traders)
        {
            if (trader != null && trader.Id == id)
            {
                return trader;
            }
        }

        return null;
    }
}
