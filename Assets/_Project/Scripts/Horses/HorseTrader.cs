using UnityEngine;

/// <summary>
/// Horses at the Trading Post: the caravan buys the player's horses standing near it (a price from its numbers, what it
/// has learned and a rare coat) and brings one horse of its own each time it comes round (after each raid survived),
/// broken in and the player's to buy. Coin goes to and comes from the stash, as the post's other trade does.
/// </summary>
public static class HorseTrader
{
    private const float Reach = 25f;

    /// <summary>The coin a horse fetches: its quality, its traits and a rare coat; a foal half.</summary>
    public static int PriceOf(HorseStats stats, HorseDefinition kind)
    {
        HorseSettings settings = HorseSettings.Current;
        float price = settings.TradeBase + settings.TradePerQuality * stats.Quality(kind);
        foreach (HorseTrait trait in new[] { HorseTrait.SureFooted, HorseTrait.Brave, HorseTrait.Swimmer, HorseTrait.Enduring })
        {
            price += stats.Has(trait) ? settings.TradePerTrait : 0f;
        }

        price += stats.rareCoat > 0 ? settings.TradeRareCoat : 0f;
        price *= kind != null && kind.IsSpecial ? 2f : 1f;
        price *= stats.IsGrown ? 1f : 0.5f;
        return Mathf.Max(1, Mathf.RoundToInt(price));
    }

    /// <summary>The horse offers and sales on the Trading Post's menu.</summary>
    public static void AddTo(MenuPage page, GameObject user, Transform post)
    {
        TradeSettings trade = TradeSettings.Current;
        HorseSettings settings = HorseSettings.Current;
        if (trade.Coin == null || settings.WildHorse == null)
        {
            return;
        }

        string coin = trade.Coin.DisplayName;
        foreach (Horse horse in Horse.All)
        {
            if (horse == null || !horse.IsAlive || !horse.IsTamed || horse.Rider != null || horse.IsStabled
                || (horse.transform.position - post.position).sqrMagnitude > Reach * Reach)
            {
                continue;
            }

            Horse chosen = horse;
            int price = Mathf.RoundToInt(PriceOf(horse.Stats, horse.Definition) * settings.TradeSellShare);
            page.Add(new MenuAction($"Sell {horse.HorseName}", $"{price} {coin}. It goes with the caravan for good", !HavenVisit.IsVisiting,
                () => Sell(chosen, price)));
        }

        string key = OfferKey();
        if (GameSession.Settlement.BoughtSinceLastRaid(key) == 0)
        {
            HorseStats offer = Offer(settings);
            int price = PriceOf(offer, settings.WildHorse);
            RareCoat? rare = settings.WildHorse.RareCoatOf(offer.rareCoat);
            string what = $"a {(rare.HasValue ? rare.Value.name.ToLowerInvariant() + " " : "")}{offer.SexName}, {HorseTemperamentRules.NameOf(offer.temperament)}";
            int purse = MaterialPool.For(user).Have(trade.Coin);
            page.Add(new MenuAction($"Buy {what}", $"{price} {coin}. Broken in, gallops {offer.gallopSpeed:0.0} m/s, carries {offer.carryWeight:0}",
                !HavenVisit.IsVisiting && purse >= price, () => Buy(user, post, offer, price, key)));
        }
    }

    // One horse each time the caravan comes: the same one until bought or the caravan moves on.
    private static string OfferKey() => "horse:" + GameSession.Settlement.SuccessfulExtractions;

    private static HorseStats Offer(HorseSettings settings)
    {
        var random = new System.Random(GameSession.Settlement.SuccessfulExtractions * 7919 + 131);
        HorseStats stats = HorseStats.Roll(settings.WildHorse, random);
        // The caravan picks its horses: never a poor one.
        HorseStats other = HorseStats.Roll(settings.WildHorse, random);
        return other.Quality(settings.WildHorse) > stats.Quality(settings.WildHorse) ? other : stats;
    }

    private static void Sell(Horse horse, int price)
    {
        if (horse == null || HavenVisit.LeftToHost())
        {
            return;
        }

        TradeSettings trade = TradeSettings.Current;
        string name = horse.HorseName;
        HorseHitch.Unhitch(horse);
        Object.Destroy(horse.gameObject);
        int added = GameSession.Stash.Items.Add(trade.Coin, price);
        GameMessages.Post($"Sold {name} to the caravan for {added} {trade.Coin.DisplayName}, in the stash.", MessageKind.Gain);
    }

    private static void Buy(GameObject user, Transform post, HorseStats stats, int price, string key)
    {
        HorseSettings settings = HorseSettings.Current;
        TradeSettings trade = TradeSettings.Current;
        if (HavenVisit.LeftToHost() || settings.WildHorse.Prefab == null || !MaterialPool.For(user).TrySpend(null, new[] { new ItemAmount(trade.Coin, price) }))
        {
            GameMessages.Post("You can't make that trade: check your coin.", MessageKind.Warning);
            return;
        }

        Vector3 at = post.position + post.forward * 4f;
        if (UnityEngine.AI.NavMesh.SamplePosition(at, out UnityEngine.AI.NavMeshHit hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
        {
            at = hit.position;
        }

        SaveableEntity made = SaveableEntity.Spawn(settings.WildHorse.Prefab.GetComponent<SaveableEntity>(), at, post.rotation);
        Horse horse = made.GetComponent<Horse>();
        horse.BecomeTamed(stats, 1f);
        GameSession.Settlement.NoteBought(key);
        GameMessages.Post($"The caravan's horse is yours: a {stats.SexName}, waiting by the post. Give it a name.", MessageKind.Gain);
        HorseNameWindow.Ask(horse);
    }
}
