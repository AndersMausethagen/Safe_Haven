using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The trade at the Trading Post (Docs/TradeResearch_2026-11-28.md): no money loop on the shipped traders, prices that flood and come
/// back, wear and make, the Hidden Folk's refusals, the barter's balance and a deal made, haggling, asks, visits and a save.
/// </summary>
public sealed class TradeTests
{
    private static TradeSettings Settings => TradeSettings.Current;
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
    private static TraderDefinition Trader(string id) => Settings.FindTrader(id) ?? throw new AssertionException($"no trader {id}");
    private static ItemDefinition Item(string id) => Content.FindItem(id) ?? throw new AssertionException($"no item {id}");

    [Test]
    public void NothingBought_SellsBackAtAGain_ToAnyone_AtAnyStanding()
    {
        // The best case for selling (Friend, wanted, a won haggle, never sold to) against the cheapest buying (Friend, a won haggle).
        int checkedPairs = 0;
        foreach (TraderDefinition seller in Settings.Traders)
        {
            var cheapest = new TradeState.Trader { Id = seller.Id, Standing = 100f, Haggle = HaggleResult.Won };
            foreach (TraderDefinition.StockEntry entry in seller.Stock)
            {
                float buy = TradeRules.BuyPrice(entry.item, seller, cheapest, Settings);
                foreach (TraderDefinition buyer in Settings.Traders)
                {
                    var best = new TradeState.Trader { Id = buyer.Id, Standing = 100f, Haggle = HaggleResult.Won };
                    // Wanted whenever they can want it (a visit's wants come from their list).
                    if (buyer.Wants.Contains(entry.item))
                    {
                        best.Wanted.Add(entry.item.Id);
                    }
                    float sell = TradeRules.SellPrice(new ItemStack(entry.item, 1), buyer, best, Settings);
                    Assert.Less(sell, buy, $"{entry.item.name}: bought from {seller.name} for {buy:0.00}, sold to {buyer.name} for {sell:0.00}");
                    checkedPairs++;
                }
            }
        }

        Assert.Greater(checkedPairs, 30);
    }

    [Test]
    public void Standing_LowersWhatTheyAsk_ForWhatNobodyWants()
    {
        TraderDefinition pedlar = Trader("pedlar");
        ItemDefinition ore = Item("copper_ore");
        float stranger = TradeRules.BuyPrice(ore, pedlar, new TradeState.Trader { Standing = 0f }, Settings);
        float friend = TradeRules.BuyPrice(ore, pedlar, new TradeState.Trader { Standing = 100f }, Settings);
        Assert.Less(friend, stranger);
        Assert.Greater(TradeRules.SellPrice(new ItemStack(ore, 1), pedlar, new TradeState.Trader { Standing = 100f }, Settings),
            TradeRules.SellPrice(new ItemStack(ore, 1), pedlar, new TradeState.Trader { Standing = 0f }, Settings));
    }

    [Test]
    public void SellingTheSameThing_FloodsTheirPrice_AndARaidBringsItBack()
    {
        TraderDefinition pedlar = Trader("pedlar");
        var state = new TradeState();
        TradeState.Trader record = state.Of(pedlar.Id);
        var ore = new ItemStack(Item("iron_ore"), 1);
        float fresh = TradeRules.SellPrice(ore, pedlar, record, Settings);
        record.Sold["iron_ore"] = 15f;
        float flooded = TradeRules.SellPrice(ore, pedlar, record, Settings);
        Assert.Less(flooded, fresh * 0.75f);
        record.Sold["iron_ore"] = 1000f;
        Assert.GreaterOrEqual(TradeRules.SellPrice(ore, pedlar, record, Settings), fresh * Settings.FloodFloor - 0.0001f, "never below the floor");
        record.Sold["iron_ore"] = 15f;
        state.AfterRaid(Settings.ForgetPerRaid);
        state.AfterRaid(Settings.ForgetPerRaid);
        Assert.Greater(TradeRules.SellPrice(ore, pedlar, record, Settings), flooded);
    }

    [Test]
    public void OneBigSale_FloodsTheirPrice_AsManySmallOnesWould()
    {
        TraderDefinition pedlar = Trader("pedlar");
        var state = new TradeState();
        TradeState.Trader record = state.Of(pedlar.Id);
        var stash = new ItemContainer(6, 5);
        stash.Add(Item("iron_ore"), 30);
        var deal = new TradeDeal(pedlar, state, Settings);
        deal.Give(TradeDeal.GoodsOf(null, stash, pedlar, record, Settings).Find(good => good.Sample.Item.Id == "iron_ore"), 30);

        var oneByOne = new TradeState.Trader { Id = pedlar.Id };
        float singles = 0f;
        for (int i = 0; i < 30; i++)
        {
            singles += TradeRules.SellPrice(new ItemStack(Item("iron_ore"), 1), pedlar, oneByOne, Settings);
            oneByOne.Sold["iron_ore"] = i + 1;
        }

        Assert.AreEqual(singles, deal.GivenWorth, 0.01f);
        Assert.Less(deal.GivenWorth, 30 * TradeRules.SellPrice(new ItemStack(Item("iron_ore"), 1), pedlar, record, Settings) * 0.8f);
    }

    [Test]
    public void AnAsk_TakesThePlainestMake_AndNeverAWorkedPiece()
    {
        TraderDefinition tinker = Trader("tinker");
        TraderDefinition.Ask ask = tinker.Asks.First(a => a.wanted != null && a.wanted.HasDurability && a.count == 1);
        var state = new TradeState();
        state.Of(tinker.Id).Ask = ask.id;
        var stash = new ItemContainer(6, 5);
        stash.AddStack(new ItemStack(ask.wanted, 1).WithEmpower(1));
        Assert.AreEqual(0, TradeFavours.AtHome(ask, null, stash), "an empowered piece doesn't count");
        Assert.IsNotNull(TradeFavours.Answer(tinker, state, null, stash, Settings));
        stash.AddStack(new ItemStack(ask.wanted, 1).WithQuality(ItemQuality.Fine).WithDurability(0.4f));
        Assert.AreEqual(1, TradeFavours.AtHome(ask, null, stash));
        Assert.IsNull(TradeFavours.Answer(tinker, state, null, stash, Settings));
        Assert.AreEqual(1, stash.Count(ask.wanted), "the empowered piece stays");
        int empowered = 0;
        for (int i = 0; i < stash.SlotCount; i++)
        {
            empowered += stash.GetSlot(i).Item == ask.wanted && stash.GetSlot(i).Empower == 1 ? 1 : 0;
        }

        Assert.AreEqual(1, empowered);
    }

    [Test]
    public void WornGear_FetchesLess_AndBetterWork_More()
    {
        TraderDefinition tinker = Trader("tinker");
        var sword = new ItemStack(Item("iron_sword"), 1);
        float plain = TradeRules.SellPrice(sword, tinker, null, Settings);
        Assert.Greater(plain, 0f, "the Tinker takes plain work for scrap");
        Assert.AreEqual(plain, TradeRules.SellPrice(sword.WithDurability(0.95f), tinker, null, Settings), 0.0001f, "a scratch costs nothing");
        Assert.Less(TradeRules.SellPrice(sword.WithDurability(0.5f), tinker, null, Settings), plain * 0.6f);
        Assert.Greater(TradeRules.SellPrice(sword.WithDurability(0f), tinker, null, Settings), 0f, "broken is still metal");
        Assert.Greater(TradeRules.SellPrice(sword.WithQuality(ItemQuality.Masterwork), tinker, null, Settings),
            TradeRules.SellPrice(sword.WithQuality(ItemQuality.Fine), tinker, null, Settings));
    }

    [Test]
    public void TheHiddenFolk_RefuseIron_AndTakeNoCoin()
    {
        TraderDefinition folk = Trader("hidden_folk");
        Assert.IsFalse(TradeRules.Buys(folk, new ItemStack(Item("iron_ingot"), 1), Settings, out string why));
        Assert.IsNotEmpty(why);
        Assert.IsTrue(TradeRules.Buys(folk, new ItemStack(Item("honey"), 1), Settings, out _));
        Assert.IsFalse(TradeRules.Buys(folk, new ItemStack(Settings.Coin, 1), Settings, out _));

        var state = new TradeState();
        TradeState.Trader record = state.Of(folk.Id);
        record.Stock.Add(new TradeState.StockLine { Item = folk.Stock[0].item.Id, Left = 1 });
        var deal = new TradeDeal(folk, state, Settings);
        deal.Take(folk.Stock[0].item.Id, 1);
        TradeDeal.Balance balance = deal.Weigh(Content, 999);
        Assert.IsFalse(balance.CanMake, "coin can't make up the balance with them");
        Assert.AreEqual(0, balance.CoinFromYou);
    }

    [Test]
    public void ADeal_TakesWhatIsGiven_PaysTheRestInCoin_AndRemembersIt()
    {
        TraderDefinition pedlar = Trader("pedlar");
        var state = new TradeState();
        TradeState.Trader record = state.Of(pedlar.Id);
        record.Stock.Add(new TradeState.StockLine { Item = "copper_ore", Left = 10 });
        var stash = new ItemContainer(6, 5);
        stash.Add(Item("flax"), 20);
        stash.Add(Settings.Coin, 50);
        var pool = new MaterialPool(null, null, null, stash);

        var deal = new TradeDeal(pedlar, state, Settings);
        List<TradeDeal.Good> goods = TradeDeal.GoodsOf(null, stash, pedlar, record, Settings);
        TradeDeal.Good flax = goods.Find(good => good.Sample.Item.Id == "flax");
        Assert.NotNull(flax, "the pedlar takes flax");
        Assert.IsNull(goods.Find(good => good.Sample.Item == Settings.Coin), "coin is the balance, not a good");
        deal.Give(flax, 5);
        deal.Take("copper_ore", 10);
        TradeDeal.Balance balance = deal.Weigh(Content, 50);
        Assert.IsTrue(balance.CanMake, balance.Problem);
        Assert.Greater(balance.CoinFromYou, 0, "ten ore are worth more than five flax");
        Assert.GreaterOrEqual(balance.Give + balance.CoinFromYou * Settings.CoinWorth, balance.Take - 0.001f, "the coin covers the gap");

        Assert.IsNull(deal.Make(null, stash, pool, Content));
        Assert.AreEqual(15, stash.Count(Item("flax")));
        Assert.AreEqual(10, stash.Count(Item("copper_ore")));
        Assert.AreEqual(50 - balance.CoinFromYou, stash.Count(Settings.Coin));
        Assert.IsNull(record.StockOf("copper_ore"), "their pack is empty of it");
        Assert.AreEqual(5f, record.SoldOf("flax"), 0.001f);
        Assert.Greater(record.Standing, 0f);
        Assert.Greater(record.Taken, 0f);
    }

    [Test]
    public void ADealTheirPackCantHold_OrTheCoinCantCover_ChangesNothing()
    {
        TraderDefinition pedlar = Trader("pedlar");
        var state = new TradeState();
        TradeState.Trader record = state.Of(pedlar.Id);
        record.Stock.Add(new TradeState.StockLine { Item = "iron_ore", Left = 18 });
        var stash = new ItemContainer(6, 5);
        stash.Add(Settings.Coin, 1);
        var deal = new TradeDeal(pedlar, state, Settings);
        deal.Take("iron_ore", 18);
        Assert.IsNotNull(deal.Make(null, stash, new MaterialPool(null, null, null, stash), Content));
        Assert.AreEqual(1, stash.Count(Settings.Coin));
        Assert.AreEqual(18, record.StockOf("iron_ore").Left);

        stash.Add(Item("heart_of_the_wilds"), 1);
        stash.Add(Item("gilded_idol"), 1);
        deal = new TradeDeal(pedlar, state, Settings);
        foreach (TradeDeal.Good good in TradeDeal.GoodsOf(null, stash, pedlar, record, Settings))
        {
            deal.Give(good, good.Count);
        }

        Assert.IsFalse(deal.Weigh(Content, 1).CanMake, "a stranger's pack takes 400 worth a visit");
        Assert.IsNotNull(deal.Make(null, stash, new MaterialPool(null, null, null, stash), Content));
        Assert.AreEqual(1, stash.Count(Item("heart_of_the_wilds")));
    }

    [Test]
    public void AHaggle_IsTriedOnce_AndALostOneCostsStanding()
    {
        var state = new TradeState();
        TradeState.Trader record = state.Of("pedlar");
        record.Standing = 10f;
        Assert.AreEqual(HaggleResult.Lost, TradeFavours.Haggle(state, record, Settings, 0.99));
        Assert.Less(record.Standing, 10f);
        float after = record.Standing;
        Assert.AreEqual(HaggleResult.Lost, TradeFavours.Haggle(state, record, Settings, 0.0), "once a visit");
        Assert.AreEqual(after, record.Standing);

        TradeState.Trader other = state.Of("tinker");
        Assert.AreEqual(HaggleResult.Won, TradeFavours.Haggle(state, other, Settings, 0.0));
        TraderDefinition tinker = Trader("tinker");
        var plain = new TradeState.Trader();
        Assert.Less(TradeRules.BuyPrice(Item("copper_axe"), tinker, other, Settings) - 0.0001f, TradeRules.BuyPrice(Item("copper_axe"), tinker, plain, Settings));
    }

    [Test]
    public void AnAsk_TakesWhatItWants_GivesItsReward_AndStanding()
    {
        TraderDefinition pedlar = Trader("pedlar");
        TraderDefinition.Ask ask = pedlar.Asks[0];
        var state = new TradeState();
        TradeState.Trader record = state.Of(pedlar.Id);
        record.Ask = ask.id;
        var stash = new ItemContainer(6, 5);
        stash.Add(ask.wanted, ask.count - 1);
        Assert.IsNotNull(TradeFavours.Answer(pedlar, state, null, stash, Settings), "one short");
        Assert.AreEqual(ask.count - 1, stash.Count(ask.wanted), "nothing taken when short");
        stash.Add(ask.wanted, 1);
        int rewardsBefore = stash.Count(ask.reward);
        Assert.IsNull(TradeFavours.Answer(pedlar, state, null, stash, Settings));
        Assert.AreEqual(0, stash.Count(ask.wanted));
        Assert.AreEqual(rewardsBefore + ask.rewardCount, stash.Count(ask.reward));
        Assert.AreEqual(ask.standing, record.Standing, 0.001f);
        Assert.IsNull(record.Ask);
    }

    [Test]
    public void Visits_AreTheSameForAHomecoming_ThePedlarComesFirst_AndTheFolkWait()
    {
        var settlement = new SettlementState();
        Assert.IsTrue(Caravans.EnsureRolled(settlement.Trade, settlement, Settings).Exists(t => t.Id == "pedlar"), "the pedlar is there when the post first stands");
        Assert.IsEmpty(Caravans.EnsureRolled(settlement.Trade, settlement, Settings), "rolled once a homecoming");
        Assert.IsNotNull(settlement.Trade.Of("pedlar").Stock.Find(line => line.Item == "copper_ore"), "the old caravan's copper is in the pack");
        Assert.IsNotNull(settlement.Trade.Of("pedlar").Ask, "an ask is posted");

        int folkCame = 0;
        int tinkerCame = 0;
        var first = new List<string>();
        for (int raid = 1; raid <= 60; raid++)
        {
            settlement.RecordExtraction();
            List<TraderDefinition> came = Caravans.EnsureRolled(settlement.Trade, settlement, Settings);
            folkCame += came.Exists(t => t.Id == "hidden_folk") ? 1 : 0;
            tinkerCame += came.Exists(t => t.Id == "tinker") ? 1 : 0;
            Assert.IsFalse(raid < 3 && came.Exists(t => t.Id == "hidden_folk"), "the Hidden Folk wait for the third raid");
            if (raid <= 5)
            {
                came.ForEach(t => first.Add($"{raid}:{t.Id}"));
            }
        }

        Assert.Greater(folkCame, 0);
        Assert.Less(folkCame, 30);
        Assert.AreEqual(0, tinkerCame, "no Smithy, no Tinker");

        // The same homecoming rolled again (a reload before the post is looked at) brings the same traders.
        var again = new SettlementState();
        Caravans.EnsureRolled(again.Trade, again, Settings);
        var second = new List<string>();
        for (int raid = 1; raid <= 5; raid++)
        {
            again.RecordExtraction();
            Caravans.EnsureRolled(again.Trade, again, Settings).ForEach(t => second.Add($"{raid}:{t.Id}"));
        }

        CollectionAssert.AreEqual(first, second);
    }

    [Test]
    public void TheTraders_SurviveASave()
    {
        var settlement = new SettlementState();
        Caravans.EnsureRolled(settlement.Trade, settlement, Settings);
        TradeState.Trader pedlar = settlement.Trade.Of("pedlar");
        pedlar.Standing = 42.5f;
        pedlar.Sold["flax"] = 3.5f;
        pedlar.Haggle = HaggleResult.Won;
        pedlar.Taken = 120f;
        pedlar.Wanted.Add("linen");
        int stockLines = pedlar.Stock.Count;

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, System.DateTime.UtcNow, null, new QuestLog(), settlement, null, new CharacterProgress());
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        TradeState back = SaveMapper.FromSaveData(data, Content).Settlement.Trade;
        TradeState.Trader loaded = back.Of("pedlar");
        Assert.AreEqual(settlement.Trade.RolledAt, back.RolledAt);
        Assert.AreEqual(42.5f, loaded.Standing, 0.001f);
        Assert.AreEqual(3.5f, loaded.SoldOf("flax"), 0.001f);
        Assert.AreEqual(HaggleResult.Won, loaded.Haggle);
        Assert.AreEqual(120f, loaded.Taken, 0.001f);
        Assert.AreEqual(pedlar.Ask, loaded.Ask);
        Assert.AreEqual(pedlar.VisitAt, loaded.VisitAt);
        Assert.AreEqual(stockLines, loaded.Stock.Count);
        CollectionAssert.AreEqual(pedlar.Wanted, loaded.Wanted);

        // An old save has no traders: nothing rolled yet.
        data.settlement.traders = null;
        data.settlement.tradeRolled = false;
        Assert.AreEqual(-1, SaveMapper.FromSaveData(data, Content).Settlement.Trade.RolledAt);
    }
}
