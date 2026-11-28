using UnityEngine;

/// <summary>
/// What passes between the Haven and a trader besides goods (Docs/TradeResearch_2026-11-28.md §3): a haggle once a visit, its odds and
/// its cost shown first (GDD §17.3), and the ask each trader posts, brought from the bag or the stash for a reward and standing
/// (RimWorld's trade requests, Tarkov's trader tasks).
/// </summary>
public static class TradeFavours
{
    /// <summary>Tries a haggle: won, prices this visit are Haggle Gain better; lost, Haggle Cost standing goes. Once a visit.</summary>
    public static HaggleResult Haggle(TradeState state, TradeState.Trader record, TradeSettings settings, double roll)
    {
        if (record.Haggle != HaggleResult.NotTried)
        {
            return record.Haggle;
        }

        bool won = roll < settings.HaggleChanceAt(record.Standing);
        record.Haggle = won ? HaggleResult.Won : HaggleResult.Lost;
        if (!won)
        {
            state.ChangeStanding(record, -settings.HaggleCost);
        }

        return record.Haggle;
    }

    /// <summary>The line for a haggle's odds and what it risks, for the button's hover.</summary>
    public static string HaggleOdds(TradeState.Trader record, TradeSettings settings)
    {
        if (record.Haggle == HaggleResult.Won)
        {
            return $"Their prices are {settings.HaggleGain:0%} better until they come again.";
        }

        if (record.Haggle == HaggleResult.Lost)
        {
            return "They won't be talked down again this visit.";
        }

        float chance = settings.HaggleChanceAt(record.Standing);
        return $"{chance:0%}: prices {settings.HaggleGain:0%} better this visit.\n{1f - chance:0%}: they take offence, −{settings.HaggleCost:0} standing.";
    }

    /// <summary>The ask a trader has posted, or null.</summary>
    public static TraderDefinition.Ask AskOf(TraderDefinition trader, TradeState.Trader record) =>
        trader != null && record != null && record.Ask != null ? trader.FindAsk(record.Ask) : null;

    /// <summary>How much of what an ask wants is home (bag and stash), of any make: pieces the player has empowered or enchanted
    /// are never taken, so they don't count.</summary>
    public static int AtHome(TraderDefinition.Ask ask, ItemContainer bag, ItemContainer stash)
    {
        if (ask == null || ask.wanted == null)
        {
            return 0;
        }

        int count = 0;
        foreach (ItemContainer container in new[] { bag, stash })
        {
            for (int i = 0; container != null && i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetSlot(i);
                count += stack.Item == ask.wanted && !stack.IsWorked ? stack.Count : 0;
            }
        }

        return count;
    }

    // Takes what an ask wants, the plainest make first, bag before stash, never a worked piece. Only after AtHome said there is enough.
    private static void TakeFor(TraderDefinition.Ask ask, ItemContainer bag, ItemContainer stash)
    {
        int left = ask.count;
        foreach (ItemQuality make in new[] { ItemQuality.Plain, ItemQuality.Fine, ItemQuality.Masterwork })
        {
            foreach (ItemContainer container in new[] { bag, stash })
            {
                for (int i = 0; container != null && i < container.SlotCount && left > 0; i++)
                {
                    ItemStack stack = container.GetSlot(i);
                    if (stack.Item != ask.wanted || stack.IsWorked || stack.Quality != make)
                    {
                        continue;
                    }

                    int took = Mathf.Min(left, stack.Count);
                    container.SetSlot(i, stack.Count > took ? stack.WithCount(stack.Count - took) : ItemStack.Empty);
                    left -= took;
                }
            }
        }
    }

    /// <summary>
    /// Brings what an ask wants (bag first, then the stash), gives its reward (bag, stash, then the feet) and standing, and takes the ask
    /// down. Returns the problem, or null.
    /// </summary>
    public static string Answer(TraderDefinition trader, TradeState state, ItemInventory bag, ItemContainer stash, TradeSettings settings)
    {
        TradeState.Trader record = state.Of(trader.Id);
        TraderDefinition.Ask ask = AskOf(trader, record);
        ItemContainer carried = bag != null ? bag.Container : null;
        if (ask == null)
        {
            return "They have nothing asked.";
        }

        if (AtHome(ask, carried, stash) < ask.count)
        {
            return $"You have {AtHome(ask, carried, stash)} of the {ask.count} {ask.wanted.DisplayName} at home.";
        }

        TakeFor(ask, carried, stash);
        if (ask.reward != null && ask.rewardCount > 0)
        {
            TradeDeal.Deliver(bag, stash, ask.reward, ask.rewardCount, newFind: true);
        }

        state.ChangeStanding(record, ask.standing);
        record.Ask = null;
        record.AsksAnswered++;
        return null;
    }
}
