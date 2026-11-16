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
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "trades take the host's stores: the host's (HavenVisit.LeftToHost)")]
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

        // Fine and Masterwork pieces in the bag, not worn: the caravan pays for good work.
        if (settings.Coin != null && user.TryGetComponent(out ItemInventory bag))
        {
            ItemContainer slots = bag.Container;
            for (int i = 0; i < slots.SlotCount; i++)
            {
                ItemStack piece = slots.GetSlot(i);
                int coins = settings.CoinsFor(piece);
                if (coins > 0)
                {
                    int index = i;
                    page.Add(new MenuAction($"Sell your {piece.DisplayName}", $"{coins} {coin}: good work fetches a good price", true, () => SellMade(user, index)));
                }
            }
        }

        foreach (TradeSettings.Offer offer in settings.Offers)
        {
            if (offer.item == null || GameSession.Here.SmithyTier < offer.smithyTier)
            {
                continue;
            }

            int left = offer.stock - GameSession.Here.BoughtSinceLastRaid(offer.Key);
            TradeSettings.Offer chosen = offer;
            string stock = left > 0 ? $"{offer.price} {coin}, {left} left" : "Sold out until the caravan returns";
            page.Add(new MenuAction($"Buy {offer.amount} {offer.item.DisplayName}", stock, left > 0 && purse >= offer.price, () => Buy(user, chosen)));
        }

        // The caravan's horse trade: the player's horses standing near the post, and one of its own each time it comes.
        HorseTrader.AddTo(page, user, user != null ? FindPost(user.transform.position) : null);
        return page;
    }

    private static Transform FindPost(Vector3 near)
    {
        TradingPost best = null;
        float bestDistance = float.MaxValue;
        foreach (TradingPost post in FindObjectsByType<TradingPost>(FindObjectsInactive.Exclude))
        {
            float distance = (post.transform.position - near).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = post;
                bestDistance = distance;
            }
        }

        return best != null ? best.transform : null;
    }

    private static void Sell(GameObject user, TradeSettings.Sale sale)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        TradeSettings settings = TradeSettings.Current;
        if (!StashTakesCoin(settings, sale.coins))
        {
            return;
        }

        if (settings.Coin != null && MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(sale.item, 1) }))
        {
            int added = GameSession.Stash.Items.Add(settings.Coin, sale.coins);
            GameMessages.Post($"Sold {sale.item.DisplayName} for {added} {settings.Coin.DisplayName}, in the stash.", MessageKind.Gain);
        }
    }

    private static void SellMade(GameObject user, int index)
    {
        if (HavenVisit.LeftToHost() || !user.TryGetComponent(out ItemInventory bag))
        {
            return;
        }

        TradeSettings settings = TradeSettings.Current;
        ItemStack piece = bag.Container.GetSlot(index);
        int coins = settings.CoinsFor(piece);
        if (settings.Coin == null || coins <= 0 || !StashTakesCoin(settings, coins))
        {
            return;
        }

        bag.Container.SetSlot(index, ItemStack.Empty);
        int added = GameSession.Stash.Items.Add(settings.Coin, coins);
        GameMessages.Post($"Sold your {piece.DisplayName} for {added} {settings.Coin.DisplayName}, in the stash.", MessageKind.Gain);
    }

    private static void Buy(GameObject user, TradeSettings.Offer offer)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        TradeSettings settings = TradeSettings.Current;
        if (settings.Coin == null || GameSession.Here.BoughtSinceLastRaid(offer.Key) >= offer.stock
            || GameSession.Stash.Items.SpaceFor(offer.item) < offer.amount
            || !MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(settings.Coin, offer.price) }))
        {
            GameMessages.Post("You can't make that trade: check your coin and the room in the stash.", MessageKind.Warning);
            return;
        }

        GameSession.Stash.Items.Add(offer.item, offer.amount);
        GameSession.Home.NoteBought(offer.Key);
        GameMessages.Post($"Bought {offer.amount} {offer.item.DisplayName}, in the stash.", MessageKind.Gain);
    }

    // A sale pays into the stash, so it waits until the stash has room for the coin: a full stash took the goods for nothing.
    private static bool StashTakesCoin(TradeSettings settings, int coins)
    {
        if (settings.Coin == null || GameSession.Stash.Items.SpaceFor(settings.Coin) >= coins)
        {
            return true;
        }

        GameMessages.Post("No room in the stash for the coin: make room in a chest first.", MessageKind.Warning);
        return false;
    }
}
