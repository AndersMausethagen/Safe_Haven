using UnityEngine;

/// <summary>
/// The Trading Post (GDD §17.3): a caravan stops here once Safe Haven is known. Valuables from the bag or the stash sell for coin,
/// and coin buys what TradeSettings offers. Everything bought goes to the stash. How much is left is in SettlementState.
/// <para>
/// Where it belongs: on the Trading Post building prefab, next to an Interactable that doesn't deactivate.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class TradingPost : MonoBehaviour
{
    [SerializeField, Min(1f)] private float menuRange = 6f;

    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable() => interactable.Completed.AddListener(OnUsed);

    private void OnDisable() => interactable.Completed.RemoveListener(OnUsed);

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => BuildPage(user), user, transform, menuRange);
        }
    }

    private static MenuPage BuildPage(GameObject user)
    {
        TradeSettings settings = TradeSettings.Current;
        MaterialPool pool = MaterialPool.For(user);
        string coin = settings.Coin != null ? settings.Coin.DisplayName : "coin";
        int purse = settings.Coin != null ? pool.Have(settings.Coin) : 0;
        var page = new MenuPage
        {
            Title = "The Trading Post",
            Body = $"You have {purse} {coin}. The caravan comes round again after each raid you survive.",
        };
        foreach (TradeSettings.Sale sale in settings.Sales)
        {
            int held = sale.item != null ? pool.Have(sale.item) : 0;
            if (held > 0)
            {
                TradeSettings.Sale chosen = sale;
                page.Add(new MenuAction($"Sell {sale.item.DisplayName}", $"You have {held}. {sale.coins} {coin} each", true, () => Sell(user, chosen)));
            }
        }

        foreach (TradeSettings.Offer offer in settings.Offers)
        {
            if (offer.item == null || GameSession.Settlement.SmithyTier < offer.smithyTier)
            {
                continue;
            }

            int left = offer.stock - GameSession.Settlement.BoughtSinceLastRaid(offer.Key);
            TradeSettings.Offer chosen = offer;
            string stock = left > 0 ? $"{offer.price} {coin}, {left} left" : "Sold out until the caravan returns";
            page.Add(new MenuAction($"Buy {offer.amount} {offer.item.DisplayName}", stock, left > 0 && purse >= offer.price, () => Buy(user, chosen)));
        }

        return page;
    }

    private static void Sell(GameObject user, TradeSettings.Sale sale)
    {
        TradeSettings settings = TradeSettings.Current;
        if (settings.Coin != null && MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(sale.item, 1) }))
        {
            int added = GameSession.Stash.Items.Add(settings.Coin, sale.coins);
            GameMessages.Post($"Sold {sale.item.DisplayName} for {added} {settings.Coin.DisplayName}, in the stash.", MessageKind.Gain);
        }
    }

    private static void Buy(GameObject user, TradeSettings.Offer offer)
    {
        TradeSettings settings = TradeSettings.Current;
        if (settings.Coin == null || GameSession.Settlement.BoughtSinceLastRaid(offer.Key) >= offer.stock
            || GameSession.Stash.Items.SpaceFor(offer.item) < offer.amount
            || !MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(settings.Coin, offer.price) }))
        {
            GameMessages.Post("You can't make that trade: check your coin and the room in the stash.", MessageKind.Warning);
            return;
        }

        GameSession.Stash.Items.Add(offer.item, offer.amount);
        GameSession.Settlement.NoteBought(offer.Key);
        GameMessages.Post($"Bought {offer.amount} {offer.item.DisplayName}, in the stash.", MessageKind.Gain);
    }
}
