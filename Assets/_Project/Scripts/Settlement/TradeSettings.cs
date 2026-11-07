using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the caravan at the Trading Post deals in: valuables brought home from raids sell for coin, and coin buys a little of the
/// materials luck can leave a player short of. Stock comes back after each raid survived, so it steadies progress without
/// replacing raids. Lives at Resources/TradeSettings; edit it in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Trade Settings", fileName = "TradeSettings")]
public sealed class TradeSettings : GameSettings<TradeSettings>
{
    [Tooltip("Coin a Fine piece of gear sells for, per point of its item Value.")]
    [SerializeField, Min(0f)] private float coinsPerValueFine = 0.1f;
    [Tooltip("Coin a Masterwork piece sells for, per point of its item Value.")]
    [SerializeField, Min(0f)] private float coinsPerValueMasterwork = 0.25f;

    /// <summary>What a better make fetches from the caravan: nothing for plain gear, which it doesn't buy.</summary>
    public int CoinsFor(ItemStack stack) =>
        stack.IsEmpty || stack.Quality == ItemQuality.Plain ? 0
        : Mathf.Max(1, Mathf.RoundToInt(stack.Item.Value * (stack.Quality == ItemQuality.Masterwork ? coinsPerValueMasterwork : coinsPerValueFine)));

    [Serializable]
    public sealed class Sale
    {
        public ItemDefinition item;
        [Min(1)] public int coins = 1;
    }

    [Serializable]
    public sealed class Offer
    {
        public ItemDefinition item;
        [Min(1)] public int amount = 1;
        [Min(1)] public int price = 1;
        [Tooltip("How many times it can be bought before the caravan has to come round again.")]
        [Min(1)] public int stock = 1;
        [Tooltip("Not offered below this Smithy tier, so nothing here climbs ahead of the ladder.")]
        [Min(0)] public int smithyTier;

        public string Key => item != null ? item.Id : "";
    }

    [Tooltip("What the caravan pays and charges in.")]
    [SerializeField] private ItemDefinition coin;
    [SerializeField] private List<Sale> sales = new List<Sale>();
    [SerializeField] private List<Offer> offers = new List<Offer>();

    public ItemDefinition Coin => coin;
    public IReadOnlyList<Sale> Sales => sales;
    public IReadOnlyList<Offer> Offers => offers;
}
