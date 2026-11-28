using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One barter at the Trading Post being put together (Docs/TradeResearch_2026-11-28.md §3): the Haven's goods offered (from the bag
/// and the stash) and the trader's taken (from their pack), weighed in worth. Coin only fills the balance: taken beyond given is paid
/// in coin, given beyond is paid back in coin, and what is left under a coin's worth (all of it with a trader who deals in no coin) is
/// a gift that raises standing. Make() checks everything again against what is there now and changes nothing when it can't be done.
/// </summary>
public sealed class TradeDeal
{
    /// <summary>Something of the Haven's the trader would take: a kind of stackable thing (bag and stash together), or one piece.</summary>
    public sealed class Good
    {
        public ItemStack Sample;
        public int Count;
        /// <summary>A piece of its own (gear): where it lies, to take that very one.</summary>
        public ItemContainer Container;
        public int Slot = -1;
        public float Price;

        public bool IsPiece => Slot >= 0;
    }

    public readonly TraderDefinition Trader;
    public readonly TradeState.Trader Record;
    private readonly TradeState state;
    private readonly TradeSettings settings;
    private readonly Dictionary<Good, int> given = new Dictionary<Good, int>();
    private readonly Dictionary<string, int> taken = new Dictionary<string, int>();

    public TradeDeal(TraderDefinition trader, TradeState state, TradeSettings settings)
    {
        Trader = trader;
        this.state = state;
        Record = state.Of(trader.Id);
        this.settings = settings;
    }

    public IReadOnlyDictionary<Good, int> Given => given;
    public IReadOnlyDictionary<string, int> Taken => taken;
    public bool IsEmpty => given.Count == 0 && taken.Count == 0;

    public int GivenOf(Good good) => good != null && given.TryGetValue(good, out int count) ? count : 0;
    public int TakenOf(string item) => item != null && taken.TryGetValue(item, out int count) ? count : 0;

    public void Clear()
    {
        given.Clear();
        taken.Clear();
    }

    /// <summary>Offers <paramref name="count"/> more of a good (negative: takes them back), within what there is.</summary>
    public void Give(Good good, int count)
    {
        int now = Mathf.Clamp(GivenOf(good) + count, 0, good.Count);
        if (now == 0)
        {
            given.Remove(good);
        }
        else
        {
            given[good] = now;
        }
    }

    /// <summary>Takes <paramref name="count"/> more from their pack (negative: puts them back), within what is left.</summary>
    public void Take(string item, int count)
    {
        TradeState.StockLine line = Record.StockOf(item);
        int now = Mathf.Clamp(TakenOf(item) + count, 0, line != null ? line.Left : 0);
        if (now == 0)
        {
            taken.Remove(item);
        }
        else
        {
            taken[item] = now;
        }
    }

    /// <summary>Worth of what is offered.</summary>
    public float GivenWorth
    {
        get
        {
            float worth = 0f;
            foreach (KeyValuePair<Good, int> line in given)
            {
                // Each unit after the last one offered of the same thing: one big sale floods their price as many small ones would.
                string id = line.Key.Sample.Item.Id;
                float ahead = offeredAhead.TryGetValue(id, out float had) ? had : 0f;
                worth += TradeRules.SellWorth(line.Key.Sample, line.Value, Trader, Record, settings, ahead);
                offeredAhead[id] = ahead + line.Value;
            }

            offeredAhead.Clear();
            return worth;
        }
    }

    private readonly Dictionary<string, float> offeredAhead = new Dictionary<string, float>();

    /// <summary>How many of this item the deal offers already (all its lines).</summary>
    public int OfferedOf(ItemDefinition item)
    {
        int count = 0;
        foreach (KeyValuePair<Good, int> line in given)
        {
            count += line.Key.Sample.Item == item ? line.Value : 0;
        }

        return count;
    }

    /// <summary>What they pay for the next one of a good, after what the deal already offers of it.</summary>
    public float NextPrice(Good good) => TradeRules.SellPrice(good.Sample.WithCount(1), Trader, Record, settings, OfferedOf(good.Sample.Item));

    /// <summary>Worth of what is taken from their pack.</summary>
    public float TakenWorth(IContentLookup content)
    {
        float worth = 0f;
        foreach (KeyValuePair<string, int> line in taken)
        {
            ItemDefinition item = content != null ? content.FindItem(line.Key) : null;
            worth += TradeRules.BuyPrice(item, Trader, Record, settings) * line.Value;
        }

        return worth;
    }

    /// <summary>The scales for this deal: what the Haven pays in coin, what it is paid back, and what is left over as a gift.</summary>
    public readonly struct Balance
    {
        public readonly float Give;
        public readonly float Take;
        public readonly int CoinFromYou;
        public readonly int CoinToYou;
        public readonly float Gift;
        public readonly float Standing;
        /// <summary>Why it can't be made as it stands, or null.</summary>
        public readonly string Problem;

        public Balance(float give, float take, int fromYou, int toYou, float gift, float standing, string problem)
        {
            Give = give;
            Take = take;
            CoinFromYou = fromYou;
            CoinToYou = toYou;
            Gift = gift;
            Standing = standing;
            Problem = problem;
        }

        public bool CanMake => Problem == null;
    }

    /// <summary>Weighs the deal against the coin the Haven holds (<paramref name="coinHeld"/>).</summary>
    public Balance Weigh(IContentLookup content, int coinHeld)
    {
        float give = GivenWorth;
        float take = TakenWorth(content);
        float coinWorth = settings.CoinWorth;
        int fromYou = 0;
        int toYou = 0;
        float gift = 0f;
        string problem = null;
        if (IsEmpty)
        {
            problem = "Choose something of theirs, or something of yours to offer.";
        }
        else if (give > TradeRules.PackLeft(Trader, Record, settings) + 0.001f)
        {
            problem = $"Their pack has room for {Mathf.FloorToInt(TradeRules.PackLeft(Trader, Record, settings))} worth more this visit.";
        }
        else if (take > give)
        {
            if (!Trader.DealsInCoin || settings.Coin == null)
            {
                problem = "They take no coin: offer more of your goods.";
            }
            else
            {
                fromYou = Mathf.CeilToInt((take - give) / coinWorth - 0.0001f);
                gift = fromYou * coinWorth - (take - give);
                if (fromYou > coinHeld)
                {
                    problem = $"You need {fromYou} coin to make up the rest, and have {coinHeld}.";
                }
            }
        }
        else
        {
            float over = give - take;
            if (Trader.DealsInCoin && settings.Coin != null)
            {
                toYou = Mathf.FloorToInt(over / coinWorth + 0.0001f);
                over -= toYou * coinWorth;
            }

            gift = over;
        }

        // Traded is what changed hands as a deal; what was given beyond it is the gift, counted once, as a gift.
        float traded = take + Mathf.Max(0f, give - gift);
        float standing = traded / settings.WorthPerStanding + TradeRules.GiftStanding(gift, Trader, Record, settings);
        return new Balance(give, take, fromYou, toYou, gift, standing, problem);
    }

    /// <summary>
    /// The Haven's goods the trader would take: every kind of stackable thing in the bag and the stash (together, bag first), and every
    /// piece of gear on its own, each with what they pay for one now. Only what they buy.
    /// </summary>
    public static List<Good> GoodsOf(ItemContainer bag, ItemContainer stash, TraderDefinition trader, TradeState.Trader record, TradeSettings settings)
    {
        var goods = new List<Good>();
        var byKind = new Dictionary<ItemDefinition, Good>();
        foreach (ItemContainer container in new[] { bag, stash })
        {
            for (int i = 0; container != null && i < container.SlotCount; i++)
            {
                ItemStack stack = container.GetSlot(i);
                if (stack.IsEmpty || !TradeRules.Buys(trader, stack, settings, out _))
                {
                    continue;
                }

                // A piece of its own (gear, an inscribed stack): offered from its slot, as many as the slot holds.
                if (stack.IsUnique)
                {
                    goods.Add(new Good
                    {
                        Sample = stack, Count = stack.Count, Container = container, Slot = i,
                        Price = TradeRules.SellPrice(stack.WithCount(1), trader, record, settings),
                    });
                    continue;
                }

                if (byKind.TryGetValue(stack.Item, out Good kind))
                {
                    kind.Count += stack.Count;
                    continue;
                }

                kind = new Good { Sample = stack.WithCount(1), Count = stack.Count, Price = TradeRules.SellPrice(stack.WithCount(1), trader, record, settings) };
                byKind[stack.Item] = kind;
                goods.Add(kind);
            }
        }

        goods.RemoveAll(good => good.Price <= 0f);
        return goods;
    }

    /// <summary>
    /// Makes the deal: takes the coin and the goods offered (bag first, then the stash), gives what was taken from their pack and the
    /// coin paid back (bag, stash, then the feet: ItemDelivery), and notes it with the trader (stock, flooding, pack, standing). Checks
    /// everything first and changes nothing when it can't be done; returns the problem, or null.
    /// </summary>
    public string Make(ItemInventory bag, ItemContainer stash, MaterialPool pool, IContentLookup content)
    {
        ItemContainer carried = bag != null ? bag.Container : null;
        int coinHeld = settings.Coin != null ? pool.Have(settings.Coin) : 0;
        Balance balance = Weigh(content, coinHeld);
        if (!balance.CanMake)
        {
            return balance.Problem;
        }

        // Still there: a stackable kind counted again, a piece still in its slot as it was.
        foreach (KeyValuePair<Good, int> line in given)
        {
            Good good = line.Key;
            if (good.IsPiece)
            {
                ItemStack now = good.Container.GetSlot(good.Slot);
                if (!now.SameKind(good.Sample) || now.Count < line.Value || Mathf.Abs(now.Durability - good.Sample.Durability) > 0.0001f)
                {
                    return $"Your {good.Sample.DisplayName} isn't where it was.";
                }
            }
            else if (BareCount(carried, good.Sample.Item) + BareCount(stash, good.Sample.Item) < line.Value)
            {
                return $"There aren't {line.Value} {good.Sample.Item.DisplayName} at home any more.";
            }
        }

        foreach (KeyValuePair<string, int> line in taken)
        {
            TradeState.StockLine stock = Record.StockOf(line.Key);
            if (stock == null || stock.Left < line.Value || content == null || content.FindItem(line.Key) == null)
            {
                return "That isn't in their pack any more.";
            }
        }

        if (balance.CoinFromYou > 0 && !pool.TrySpend(null, new[] { new ItemAmount(settings.Coin, balance.CoinFromYou) }))
        {
            return $"You need {balance.CoinFromYou} coin.";
        }

        foreach (KeyValuePair<Good, int> line in given)
        {
            Good good = line.Key;
            if (good.IsPiece)
            {
                ItemStack now = good.Container.GetSlot(good.Slot);
                good.Container.SetSlot(good.Slot, now.Count > line.Value ? now.WithCount(now.Count - line.Value) : ItemStack.Empty);
            }
            else
            {
                // Bare stacks only (never an inscribed one of the same item), bag first.
                int left = line.Value;
                if (carried != null)
                {
                    left -= carried.Remove(good.Sample.Item, left, ItemQuality.Plain, false);
                }

                if (left > 0 && stash != null)
                {
                    stash.Remove(good.Sample.Item, left, ItemQuality.Plain, false);
                }
            }

            Record.Sold[good.Sample.Item.Id] = Record.SoldOf(good.Sample.Item.Id) + line.Value;
        }

        foreach (KeyValuePair<string, int> line in taken)
        {
            Record.StockOf(line.Key).Left -= line.Value;
            Deliver(bag, stash, content.FindItem(line.Key), line.Value);
        }

        Record.Stock.RemoveAll(stock => stock.Left <= 0);
        if (balance.CoinToYou > 0)
        {
            Deliver(bag, stash, settings.Coin, balance.CoinToYou);
        }

        Record.Taken += balance.Give;
        state.ChangeStanding(Record, balance.Standing);
        Clear();
        return null;
    }

    /// <summary>How many of a stackable item lie in plain stacks (not a piece of its own, such as an inscribed stack).</summary>
    public static int BareCount(ItemContainer container, ItemDefinition item)
    {
        int count = 0;
        for (int i = 0; container != null && i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            count += stack.Item == item && !stack.IsUnique && !stack.IsWorked ? stack.Count : 0;
        }

        return count;
    }

    /// <summary>Into the bag, the rest to the stash, the rest at the player's feet: nothing traded is lost for want of room.
    /// <paramref name="newFind"/>: a reward (an ask's), raised as a find for collect quests.</summary>
    public static void Deliver(ItemInventory bag, ItemContainer stash, ItemDefinition item, int count, bool newFind = false)
    {
        // Stackables come whole; pieces one at a time (gear never stacks).
        int each = item.Stackable ? count : 1;
        for (int given = 0; given < count; given += each)
        {
            var stack = new ItemStack(item, Mathf.Min(each, count - given));
            if (bag != null)
            {
                ItemDelivery.Give(bag, stack, newFind, toStash: true, cause: ArrivalCause.Rewarded);
            }
            else
            {
                ItemDelivery.Give((ItemContainer)null, stack, stash, null);
            }
        }
    }
}
