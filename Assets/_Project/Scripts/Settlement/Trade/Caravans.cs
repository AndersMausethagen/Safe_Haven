using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who comes to the Trading Post, and with what (Docs/TradeResearch_2026-11-28.md §3): after each raid survived every trader may
/// come (by chance, every so many raids, or always the first time the post stands), with a pack rolled from their stock, a few
/// things they want most, and an ask. A visit lasts until the next raid survived. The roll is seeded by the homecoming, so the same
/// homecoming always brings the same traders, whenever it is first looked at (the Trading Post rolls it, TradingPost.Update).
/// </summary>
public static class Caravans
{
    /// <summary>
    /// Rolls the visits for this homecoming if they haven't been; returns the traders who came with this roll (empty when it was rolled
    /// before). Only for one's own Haven (the host's at home).
    /// </summary>
    public static List<TraderDefinition> EnsureRolled(TradeState state, IHavenView here, TradeSettings settings)
    {
        var arrived = new List<TraderDefinition>();
        int homecoming = here.SuccessfulExtractions;
        if (state.RolledAt == homecoming)
        {
            return arrived;
        }

        bool first = state.RolledAt < 0;
        foreach (TraderDefinition trader in settings.Traders)
        {
            if (trader == null)
            {
                continue;
            }

            TradeState.Trader record = state.Of(trader.Id);
            // An ask left standing too long comes down, whether or not they come this time.
            if (record.Ask != null && (trader.FindAsk(record.Ask) == null || homecoming - record.AskPostedAt >= settings.AskLasts))
            {
                record.Ask = null;
            }

            var random = new System.Random(homecoming * 7919 + StableHash(trader.Id));
            if (Comes(trader, record, here, settings, homecoming, first, random))
            {
                Arrive(trader, record, here, settings, homecoming, random);
                arrived.Add(trader);
            }
        }

        state.NoteRolled(homecoming);
        return arrived;
    }

    /// <summary>Whether a trader is at the post now (this homecoming's visit).</summary>
    public static bool IsHere(TraderDefinition trader, TradeState state, IHavenView here) =>
        trader != null && state.Knows(trader.Id) && state.Of(trader.Id).IsHereAt(here.SuccessfulExtractions);

    /// <summary>The traders at the post now, in the settings' order.</summary>
    public static List<TraderDefinition> HereNow(TradeState state, IHavenView here, TradeSettings settings)
    {
        var found = new List<TraderDefinition>();
        foreach (TraderDefinition trader in settings.Traders)
        {
            if (IsHere(trader, state, here))
            {
                found.Add(trader);
            }
        }

        return found;
    }

    /// <summary>Whether they will deal now (the Hidden Folk only after dark); <paramref name="why"/> says why not.</summary>
    public static bool DealsNow(TraderDefinition trader, bool dark, out string why)
    {
        why = trader.OnlyAfterDark && !dark ? "They keep out of sight by day. Come back after dark." : null;
        return why == null;
    }

    private static bool Comes(TraderDefinition trader, TradeState.Trader record, IHavenView here, TradeSettings settings, int homecoming, bool first,
        System.Random random)
    {
        if (homecoming < trader.FromRaid || here.SmithyTier < trader.SmithyTier)
        {
            return false;
        }

        if (first && trader.HereFirst)
        {
            return true;
        }

        if (trader.EveryNthRaid > 0)
        {
            return homecoming > 0 && homecoming % trader.EveryNthRaid == 0;
        }

        float chance = TradeRules.Tier(record.Standing, settings) >= 2 ? Mathf.Max(trader.VisitChance, trader.TrustedVisitChance) : trader.VisitChance;
        return random.NextDouble() < chance;
    }

    private static void Arrive(TraderDefinition trader, TradeState.Trader record, IHavenView here, TradeSettings settings, int homecoming, System.Random random)
    {
        record.VisitAt = homecoming;
        record.Visits++;
        record.Haggle = HaggleResult.NotTried;
        record.Taken = 0f;
        record.Stock.Clear();
        record.Wanted.Clear();
        int tier = TradeRules.Tier(record.Standing, settings);
        foreach (TraderDefinition.StockEntry entry in trader.Stock)
        {
            if (entry == null || entry.item == null || entry.smithyTier > here.SmithyTier || entry.standingTier > tier || random.NextDouble() >= entry.chance)
            {
                continue;
            }

            int count = random.Next(Mathf.Min(entry.least, entry.most), Mathf.Max(entry.least, entry.most) + 1);
            TradeState.StockLine line = record.StockOf(entry.item.Id);
            if (line != null)
            {
                line.Left += count;
            }
            else
            {
                record.Stock.Add(new TradeState.StockLine { Item = entry.item.Id, Left = count });
            }
        }

        var wants = new List<ItemDefinition>();
        foreach (ItemDefinition want in trader.Wants)
        {
            if (want != null && !wants.Contains(want))
            {
                wants.Add(want);
            }
        }

        for (int i = 0; i < trader.WantsPerVisit && wants.Count > 0; i++)
        {
            int pick = random.Next(wants.Count);
            record.Wanted.Add(wants[pick].Id);
            wants.RemoveAt(pick);
        }

        if (record.Ask == null)
        {
            PostAsk(trader, record, here, homecoming, random);
        }
    }

    // A new ask, not the one just answered or taken down if another will do.
    private static void PostAsk(TraderDefinition trader, TradeState.Trader record, IHavenView here, int homecoming, System.Random random)
    {
        var open = new List<TraderDefinition.Ask>();
        foreach (TraderDefinition.Ask ask in trader.Asks)
        {
            if (ask != null && ask.wanted != null && !string.IsNullOrEmpty(ask.id) && ask.smithyTier <= here.SmithyTier)
            {
                open.Add(ask);
            }
        }

        if (open.Count == 0)
        {
            return;
        }

        TraderDefinition.Ask chosen = open[random.Next(open.Count)];
        record.Ask = chosen.id;
        record.AskPostedAt = homecoming;
    }

    // The same number for the same id in every session (string.GetHashCode isn't kept between runs).
    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in text ?? "")
            {
                hash = hash * 31 + c;
            }

            return hash & 0x7fffffff;
        }
    }
}
