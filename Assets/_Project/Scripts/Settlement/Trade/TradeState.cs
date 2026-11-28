using System;
using System.Collections.Generic;

/// <summary>How a haggle stands on a trader's visit.</summary>
public enum HaggleResult
{
    NotTried = 0,
    Won = 1,
    Lost = 2,
}

/// <summary>
/// What the traders remember of the Haven (Docs/TradeResearch_2026-11-28.md), as plain state: each one's standing, how much of each
/// thing they have been sold lately (it floods their price), the ask they have posted, and their visit: when it began (the
/// homecoming), what is left in their pack, what they want most this time, how a haggle went and how much their pack has taken.
/// Runtime state on SettlementState (saved in SettlementSaveData.traders by SaveMapper); Caravans rolls the visits, TradeRules prices.
/// </summary>
public sealed class TradeState
{
    public sealed class StockLine
    {
        public string Item;
        public int Left;
    }

    public sealed class Trader
    {
        public string Id;
        public float Standing;
        /// <summary>Units of each item (by id) sold to them lately, forgotten a share at a time (TradeSettings › Forget Per Raid).</summary>
        public readonly Dictionary<string, float> Sold = new Dictionary<string, float>();
        /// <summary>The ask they have posted (TraderDefinition.Ask.id), or null.</summary>
        public string Ask;
        /// <summary>The homecoming the ask was posted at.</summary>
        public int AskPostedAt;
        public int AsksAnswered;
        /// <summary>The homecoming (SuccessfulExtractions) of their visit; they are here while it is the current one. -1: never came.</summary>
        public int VisitAt = -1;
        public readonly List<StockLine> Stock = new List<StockLine>();
        public readonly List<string> Wanted = new List<string>();
        public HaggleResult Haggle;
        /// <summary>Worth of the Haven's goods their pack has taken this visit.</summary>
        public float Taken;
        public int Visits;

        public bool IsHereAt(int homecoming) => VisitAt == homecoming;

        public float SoldOf(string item) => item != null && Sold.TryGetValue(item, out float sold) ? sold : 0f;

        public StockLine StockOf(string item)
        {
            foreach (StockLine line in Stock)
            {
                if (line.Item == item)
                {
                    return line;
                }
            }

            return null;
        }
    }

    private readonly Dictionary<string, Trader> traders = new Dictionary<string, Trader>();

    /// <summary>The homecoming the visits were last rolled for; -1 before the first roll (Caravans.EnsureRolled).</summary>
    public int RolledAt { get; private set; } = -1;

    public IEnumerable<Trader> Traders => traders.Values;

    public Trader Of(string id)
    {
        if (!traders.TryGetValue(id, out Trader trader))
        {
            trader = new Trader { Id = id };
            traders[id] = trader;
        }

        return trader;
    }

    public bool Knows(string id) => id != null && traders.ContainsKey(id);

    public void NoteRolled(int homecoming) => RolledAt = homecoming;

    /// <summary>A raid survived: each trader forgets a share of what they were sold, so a flooded price comes back.</summary>
    public void AfterRaid(float forget)
    {
        foreach (Trader trader in traders.Values)
        {
            var keys = new List<string>(trader.Sold.Keys);
            foreach (string key in keys)
            {
                float left = trader.Sold[key] * (1f - forget);
                if (left < 0.05f)
                {
                    trader.Sold.Remove(key);
                }
                else
                {
                    trader.Sold[key] = left;
                }
            }
        }
    }

    public void ChangeStanding(Trader trader, float by) => trader.Standing = Math.Max(0f, Math.Min(100f, trader.Standing + by));

    /// <summary>Puts back what a save kept. Used by SaveMapper only.</summary>
    public void Restore(int rolledAt, IEnumerable<Trader> saved)
    {
        traders.Clear();
        RolledAt = rolledAt;
        if (saved == null)
        {
            return;
        }

        foreach (Trader trader in saved)
        {
            if (!string.IsNullOrEmpty(trader.Id))
            {
                traders[trader.Id] = trader;
            }
        }
    }
}
