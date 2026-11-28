using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One kind of trader who comes to the Trading Post (Docs/TradeResearch_2026-11-28.md): when they come, what they buy and refuse,
/// what their pack holds each visit, what they want most, and what they ask of the Haven. Authored data, read by TradeRules (prices),
/// Caravans (visits and stock) and the TradeWindow; what changes in play lives in TradeState.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Trade/Trader Definition", fileName = "Trader")]
public sealed class TraderDefinition : ScriptableObject
{
    [Serializable]
    public sealed class StockEntry
    {
        public ItemDefinition item;
        [Min(1)] public int least = 1;
        [Min(1)] public int most = 1;
        [Tooltip("Chance it is in the pack on a visit (1: always).")]
        [Range(0f, 1f)] public float chance = 1f;
        [Tooltip("Not brought below this Smithy tier, so nothing climbs ahead of the ladder.")]
        [Min(0)] public int smithyTier;
        [Tooltip("Only brought for those with at least this standing tier (0 Stranger, 1 Known, 2 Trusted, 3 Friend).")]
        [Range(0, 3)] public int standingTier;
    }

    [Serializable]
    public sealed class Ask
    {
        [Tooltip("Stable id, saved while the ask stands.")]
        public string id;
        public ItemDefinition wanted;
        [Min(1)] public int count = 1;
        public ItemDefinition reward;
        [Min(0)] public int rewardCount = 1;
        [Tooltip("Standing it gives when brought.")]
        [Min(0)] public int standing = 15;
        [Tooltip("Not asked below this Smithy tier.")]
        [Min(0)] public int smithyTier;
        [Tooltip("What they say when it is brought.")]
        public string thanks;
    }

    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("A few words under the name in the trade window.")]
    [SerializeField] private string greeting;
    [Tooltip("The kit icon beside the name (Wheel, Hammer, Moon...).")]
    [SerializeField] private string icon = "Wheel";

    [Header("Visits")]
    [Tooltip("Chance they come with a homecoming (a raid survived).")]
    [SerializeField, Range(0f, 1f)] private float visitChance = 1f;
    [Tooltip("Chance from the Trusted tier on (standing tier 2).")]
    [SerializeField, Range(0f, 1f)] private float trustedVisitChance = 1f;
    [Tooltip("They come every this many raids survived instead of by chance. 0: by chance.")]
    [SerializeField, Min(0)] private int everyNthRaid;
    [Tooltip("Not before this many raids have been survived.")]
    [SerializeField, Min(0)] private int fromRaid;
    [Tooltip("Not before the Smithy reaches this tier (0: whatever the Smithy).")]
    [SerializeField, Min(0)] private int smithyTier;
    [Tooltip("Always here the first time the Trading Post stands.")]
    [SerializeField] private bool hereFirst;
    [Tooltip("Trade only while it is dark (the Hidden Folk).")]
    [SerializeField] private bool onlyAfterDark;

    [Header("Buying")]
    [Tooltip("Kinds of item they buy. Quest items are never bought, nor coin (it is the balance).")]
    [SerializeField] private List<ItemCategory> buysKinds = new List<ItemCategory>();
    [Tooltip("Items they buy besides those kinds.")]
    [SerializeField] private List<ItemDefinition> alsoBuys = new List<ItemDefinition>();
    [Tooltip("Items they never buy, whatever their kind.")]
    [SerializeField] private List<ItemDefinition> refuses = new List<ItemDefinition>();
    [Tooltip("Items whose id holds this word are refused (the Hidden Folk: \"iron\"). Empty: none.")]
    [SerializeField] private string refusesWord;
    [Tooltip("Why they refuse what they refuse, for the window.")]
    [SerializeField] private string refusalLine;
    [Tooltip("What plain gear and tools fetch, as a share of a good piece (0: they don't buy plain work; the Tinker takes it for scrap).")]
    [SerializeField, Range(0f, 1f)] private float plainGearShare;
    [Tooltip("They pay and take coin. The Hidden Folk don't: theirs is barter only, and what is given beyond is a gift.")]
    [SerializeField] private bool dealsInCoin = true;

    [Header("Pack")]
    [Tooltip("Worth of your goods their pack takes a visit, as a Stranger.")]
    [SerializeField, Min(0)] private int pack = 400;
    [Tooltip("More room for each standing tier.")]
    [SerializeField, Min(0)] private int packPerTier = 150;
    [SerializeField] private List<StockEntry> stock = new List<StockEntry>();
    [Tooltip("What they may want most on a visit (x Want Multiplier in TradeSettings).")]
    [SerializeField] private List<ItemDefinition> wants = new List<ItemDefinition>();
    [SerializeField, Min(0)] private int wantsPerVisit = 2;

    [Header("Asks and standing")]
    [SerializeField] private List<Ask> asks = new List<Ask>();
    [Tooltip("Gifts (what is given beyond the deal) count this many times toward standing.")]
    [SerializeField, Min(0f)] private float giftWeight = 1f;
    [Tooltip("What haggling is called with them.")]
    [SerializeField] private string haggleWord = "Haggle";

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Greeting => greeting;
    public string Icon => icon;
    public float VisitChance => visitChance;
    public float TrustedVisitChance => trustedVisitChance;
    public int EveryNthRaid => everyNthRaid;
    public int FromRaid => fromRaid;
    public int SmithyTier => smithyTier;
    public bool HereFirst => hereFirst;
    public bool OnlyAfterDark => onlyAfterDark;
    public IReadOnlyList<ItemCategory> BuysKinds => buysKinds;
    public IReadOnlyList<ItemDefinition> AlsoBuys => alsoBuys;
    public IReadOnlyList<ItemDefinition> Refuses => refuses;
    public string RefusesWord => refusesWord;
    public string RefusalLine => refusalLine;
    public float PlainGearShare => plainGearShare;
    public bool DealsInCoin => dealsInCoin;
    public int PackFor(int tier) => pack + packPerTier * Mathf.Max(0, tier);
    public IReadOnlyList<StockEntry> Stock => stock;
    public IReadOnlyList<ItemDefinition> Wants => wants;
    public int WantsPerVisit => wantsPerVisit;
    public IReadOnlyList<Ask> Asks => asks;
    public float GiftWeight => giftWeight;
    public string HaggleWord => string.IsNullOrWhiteSpace(haggleWord) ? "Haggle" : haggleWord;

    public Ask FindAsk(string askId)
    {
        foreach (Ask ask in asks)
        {
            if (ask != null && ask.id == askId)
            {
                return ask;
            }
        }

        return null;
    }
}
