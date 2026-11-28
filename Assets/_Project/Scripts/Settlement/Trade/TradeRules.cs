using System.Linq;
using UnityEngine;

/// <summary>
/// How the traders price things (Docs/TradeResearch_2026-11-28.md §3), as plain rules: what a trader pays for one of the Haven's
/// goods (its worth by quality and wear, how much of it they have been sold, whether they want it, the standing's share) and what
/// they ask for one of theirs (never below what anyone would pay for it, so trade is no money loop). Worth is an item's Value; a
/// coin is worth its own (TradeSettings.CoinWorth).
/// </summary>
public static class TradeRules
{
    /// <summary>The standing tier of a standing (0 Stranger .. 3 Friend).</summary>
    public static int Tier(float standing, TradeSettings settings)
    {
        int tier = 0;
        for (int i = 1; i < settings.TierCount; i++)
        {
            if (standing >= settings.TierFrom(i))
            {
                tier = i;
            }
        }

        return tier;
    }

    /// <summary>Whether <paramref name="trader"/> takes <paramref name="stack"/> at all; <paramref name="why"/> says why not.</summary>
    public static bool Buys(TraderDefinition trader, ItemStack stack, TradeSettings settings, out string why)
    {
        why = null;
        if (stack.IsEmpty || trader == null)
        {
            return false;
        }

        ItemDefinition item = stack.Item;
        if (item == settings.Coin || item.Category == ItemCategory.Quest || item.IsWaterMeasure || item.Value <= 0)
        {
            return false;
        }

        if (IsRefused(trader, item))
        {
            why = trader.RefusalLine;
            return false;
        }

        bool kind = trader.BuysKinds.Contains(item.Category) || trader.AlsoBuys.Contains(item);
        if (!kind)
        {
            return false;
        }

        if (IsGear(item) && stack.Quality == ItemQuality.Plain && trader.PlainGearShare <= 0f)
        {
            why = "Plain work: they only buy a Fine or Masterwork piece.";
            return false;
        }

        return true;
    }

    /// <summary>Whether a trader turns this item away whatever it is (their list, or the word in its id: the Hidden Folk and iron).</summary>
    public static bool IsRefused(TraderDefinition trader, ItemDefinition item) =>
        item != null && (trader.Refuses.Contains(item)
            || !string.IsNullOrWhiteSpace(trader.RefusesWord) && item.Id.IndexOf(trader.RefusesWord, System.StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>Gear and tools: pieces whose make and wear count.</summary>
    public static bool IsGear(ItemDefinition item) => item != null && item.HasDurability && !item.HoldsWater && !item.IsLantern;

    /// <summary>Share of its worth a piece keeps for its wear: whole from Full Worth From, half at 50%, Broken Worth when broken.</summary>
    public static float Condition(float durability, TradeSettings settings)
    {
        float full = settings.FullWorthFrom;
        if (durability >= full)
        {
            return 1f;
        }

        if (durability >= 0.5f)
        {
            return Mathf.Lerp(0.5f, 1f, (durability - 0.5f) / Mathf.Max(0.0001f, full - 0.5f));
        }

        return Mathf.Lerp(settings.BrokenWorth, 0.5f, durability / 0.5f);
    }

    /// <summary>One piece's worth before any trader's share: Value, its make, its wear.</summary>
    public static float WorthOf(ItemStack unit, TradeSettings settings)
    {
        if (unit.IsEmpty)
        {
            return 0f;
        }

        float worth = unit.Item.Value;
        if (IsGear(unit.Item))
        {
            worth *= unit.Quality == ItemQuality.Masterwork ? settings.MasterworkWorth : unit.Quality == ItemQuality.Fine ? settings.FineWorth : 1f;
            worth *= Condition(unit.Durability, settings);
        }

        if (unit.Item.Category == ItemCategory.Valuable)
        {
            worth *= settings.ValuableMultiplier;
        }

        return worth;
    }

    /// <summary>How much of a thing they have been sold, as a share of its price: Per Unit Sold for each unit, never below Flood Floor.</summary>
    public static float Flooding(TradeState.Trader record, string item, TradeSettings settings) =>
        record == null ? 1f : Mathf.Max(settings.FloodFloor, Mathf.Pow(settings.PerUnitSold, record.SoldOf(item)));

    private static float HaggleFactor(TradeState.Trader record, TradeSettings settings) =>
        record != null && record.Haggle == HaggleResult.Won ? settings.HaggleGain : 0f;

    /// <summary>What they pay for <paramref name="count"/> of <paramref name="unit"/> in one go: each after the last, as flooded as if
    /// sold one at a time (<paramref name="ahead"/>: how many of it the deal already offers before these).</summary>
    public static float SellWorth(ItemStack unit, int count, TraderDefinition trader, TradeState.Trader record, TradeSettings settings, float ahead = 0f)
    {
        float worth = 0f;
        ItemStack one = unit.WithCount(1);
        for (int k = 0; k < count; k++)
        {
            worth += SellPrice(one, trader, record, settings, ahead + k);
        }

        return worth;
    }

    /// <summary>What <paramref name="trader"/> pays for one of <paramref name="unit"/> now (0 when they don't buy it), after
    /// <paramref name="ahead"/> more of it than they have been sold.</summary>
    public static float SellPrice(ItemStack unit, TraderDefinition trader, TradeState.Trader record, TradeSettings settings, float ahead = 0f)
    {
        if (!Buys(trader, unit, settings, out _))
        {
            return 0f;
        }

        float price = WorthOf(unit, settings) * settings.SellShare(Tier(record != null ? record.Standing : 0f, settings));
        if (IsGear(unit.Item) && unit.Quality == ItemQuality.Plain)
        {
            price *= trader.PlainGearShare;
        }

        if (record != null && record.Wanted.Contains(unit.Item.Id))
        {
            price *= settings.WantMultiplier;
        }

        price *= record == null ? Mathf.Max(settings.FloodFloor, Mathf.Pow(settings.PerUnitSold, ahead))
            : Mathf.Max(settings.FloodFloor, Mathf.Pow(settings.PerUnitSold, record.SoldOf(unit.Item.Id) + ahead));
        price *= 1f + HaggleFactor(record, settings);
        return Mathf.Max(0f, price);
    }

    /// <summary>
    /// The most any trader could ever pay for one new, plain <paramref name="item"/>: the best standing's share, wanted if they can want
    /// it, after a won haggle, never flooded. What a trader asks for it stays above this (Arbitrage Margin), so nothing bought can be
    /// sold back at a gain, to whoever buys it.
    /// </summary>
    public static float SellCeiling(ItemDefinition item, TradeSettings settings)
    {
        if (item == null)
        {
            return 0f;
        }

        float bestShare = 0f;
        for (int tier = 0; tier < settings.TierCount; tier++)
        {
            bestShare = Mathf.Max(bestShare, settings.SellShare(tier));
        }

        var unit = new ItemStack(item, 1);
        float best = 0f;
        foreach (TraderDefinition trader in settings.Traders)
        {
            if (trader == null || !Buys(trader, unit, settings, out _))
            {
                continue;
            }

            float price = WorthOf(unit, settings) * bestShare * (1f + settings.HaggleGain);
            price *= trader.Wants.Contains(item) ? settings.WantMultiplier : 1f;
            price *= IsGear(item) ? trader.PlainGearShare : 1f;
            best = Mathf.Max(best, price);
        }

        return best;
    }

    /// <summary>What <paramref name="trader"/> asks for one of <paramref name="item"/> now.</summary>
    public static float BuyPrice(ItemDefinition item, TraderDefinition trader, TradeState.Trader record, TradeSettings settings)
    {
        if (item == null)
        {
            return 0f;
        }

        float price = Mathf.Max(1, item.Value) * settings.BuyShare(Tier(record != null ? record.Standing : 0f, settings));
        price *= 1f - HaggleFactor(record, settings);
        return Mathf.Max(price, SellCeiling(item, settings) * settings.ArbitrageMargin);
    }

    /// <summary>Worth of the Haven's goods their pack can still take this visit.</summary>
    public static float PackLeft(TraderDefinition trader, TradeState.Trader record, TradeSettings settings) =>
        Mathf.Max(0f, trader.PackFor(Tier(record.Standing, settings)) - record.Taken);

    /// <summary>Standing a gift of <paramref name="worth"/> gives (falling returns as standing rises; the trader's Gift Weight).</summary>
    public static float GiftStanding(float worth, TraderDefinition trader, TradeState.Trader record, TradeSettings settings) =>
        worth <= 0f ? 0f : worth / settings.GiftWorthPerStanding(record.Standing) * trader.GiftWeight;

    /// <summary>The chance a haggle comes off now, 0 when it was tried this visit.</summary>
    public static float HaggleChance(TradeState.Trader record, TradeSettings settings) =>
        record.Haggle != HaggleResult.NotTried ? 0f : settings.HaggleChanceAt(record.Standing);
}
