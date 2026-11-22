using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The economy's rules (Docs/EconomyAndPhases_2026-11-22.md, the user's answers of 2026-11-22): home makes plain goods, raids make the
/// tier materials; Elderwood is a raid wood; fields grow with the Haven's rank; the Haven's people eat.
/// </summary>
public sealed class EconomyRulesTests
{
    // Tier II commons (hardwood, flax, herbs, honey, copper ore from a tithe) may come from home; tier III and up only from raids.
    private const int MostTierFromHome = 2;

    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    [Test]
    public void NoResidentOrLandSource_GivesTierThreeOrAbove()
    {
        var bad = new List<string>();
        int seen = 0;
        void Check(string source, ItemDefinition item)
        {
            if (item == null)
            {
                return;
            }

            seen++;
            if (item.Tier > MostTierFromHome)
            {
                bad.Add($"{source}: {item.name} (tier {item.Tier})");
            }
        }

        foreach (TerritorySettings.RegionInfo region in TerritorySettings.Current.Regions)
        {
            foreach (ItemAmount tithe in region.tithe)
            {
                Check($"{region.region}'s tithe", tithe.Item);
            }
        }

        foreach (TerritorySettings.PartyInfo party in TerritorySettings.Current.Parties)
        {
            foreach (TerritorySettings.PartyYield yield in party.yields)
            {
                Check($"the {party.kind} party", yield.item);
            }
        }

        foreach (ItemAmount rock in WorkerSettings.Current.AwayRockYield)
        {
            Check("the quarry ring away", rock.Item);
        }

        foreach (HomecomingSettings.Gift gift in HomecomingSettings.Current.Gifts)
        {
            Check($"a homecoming gift ({(gift.job != null ? gift.job.name : "anyone")})", gift.item);
        }

        // What grows or is caught at home: a farm, dock or hive recipe that gives more than it takes of its own kind.
        foreach (RecipeDefinition recipe in Content.Recipes.Where(r => r != null && r.Output != null
                     && (r.Station == CraftingStationType.Farm || r.Station == CraftingStationType.Dock || r.Station == CraftingStationType.Beehive)))
        {
            int sown = recipe.ItemCost.Where(c => c.Item == recipe.Output).Sum(c => c.Amount);
            if (recipe.OutputAmount > sown)
            {
                Check($"{recipe.name} at the {recipe.Station}", recipe.Output);
            }
        }

        Assert.Greater(seen, 20, "the home sources were found");
        Assert.IsEmpty(bad, "home makes plain, raids make tier:\n" + string.Join("\n", bad));
    }

    [Test]
    public void AtHome_LogsComeNoBetterThanHardwood_ElderwoodIsARaidWood()
    {
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        Assert.AreEqual(2, rules.HomeMaxWoodTier);
        Assert.IsNull(RaidDirector.Current, "no raid in the Editor: this is home");

        // Every roll a hit, an iron axe on a big tree: Elderwood without the cap, Hardwood with it.
        Assert.Greater(rules.RollLog(4, 2.5f, () => 0f).Tier, MostTierFromHome, "the canary: the wood list does hold a raid wood");
        var logs = new List<ItemAmount>();
        ChoppableTree.Roll(6, 4, 2.5f, () => 0f, logs);
        Assert.AreEqual(6, logs.Sum(l => l.Amount));
        Assert.IsTrue(logs.All(l => l.Item.Tier <= MostTierFromHome), string.Join(", ", logs.Select(l => l.Item.name)));
        Assert.IsTrue(logs.Any(l => l.Item.Tier == MostTierFromHome), "still the best home wood");
    }

    [Test]
    public void FarmPlots_GrowWithTheHavensRank_AndTheLastHolds()
    {
        var plot = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/FarmPlot.asset");
        Assert.IsTrue(plot.IsLimited);
        int ranks = HavenSettings.Current.Ranks.Count;
        for (int rank = 1; rank < ranks; rank++)
        {
            Assert.GreaterOrEqual(plot.LimitAt(rank), plot.LimitAt(rank - 1), $"rank {rank}");
        }

        Assert.Greater(plot.LimitAt(ranks - 1), plot.LimitAt(0), "more fields as the Haven grows");
        Assert.AreEqual(plot.LimitAt(ranks - 1), plot.LimitAt(ranks + 3), "the last number holds");
        Assert.IsFalse(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Kitchen.asset").IsLimited, "most buildings have no limit");
    }

    // The user, 2026-11-22: "NPC's should eat anything that's a basic food source. Like bread, berry, mushroom etc. prefering cooked and
    // prepared meals before raw materials."
    [Test]
    public void Residents_EatAnyBasicFood_CookedFirst_ThenRaw_AndItIsSaved()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        Assert.IsNotNull(settings.MealsFrom, "meals start with the Kitchen");
        Assert.AreEqual(40f, settings.DailyHunger, "a loaf a day");
        ItemDefinition Food(string name) => Content.Items.First(i => i != null && i.name == name);
        ItemDefinition bread = Food("BarleyBread"), berries = Food("Berries"), turnip = Food("WildTurnip"), meat = Food("BoarMeat"), pie = Food("BoarPie");
        Assert.Less(berries.Value, bread.Value, "the canary: raw berries are cheaper, yet the bread goes first");
        Assert.AreEqual(40f, berries.HungerRestore * 4f, "four handfuls of berries fill one");
        Assert.Contains(meat, settings.Uneaten.ToList(), "raw meat waits for the cook");
        Assert.Greater(pie.Tier, settings.MostMealTier, "a pie is the player's");

        ResidentDefinition[] people = AssetDatabase.FindAssets("t:ResidentDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(one => one != null).Take(3).ToArray();
        var settlement = new SettlementState();
        foreach (ResidentDefinition one in people)
        {
            settlement.AddResident(one);
        }

        var chests = new ItemContainer(10, 6);
        chests.Add(bread, 1);
        chests.Add(berries, 4);
        chests.Add(turnip, 2);
        chests.Add(meat, 5);
        chests.Add(pie, 2);

        // No Kitchen yet: they feed themselves, and nothing is taken.
        Assert.IsNull(ResidentMeals.Eat(settlement, chests, settings));
        Assert.AreEqual(1, chests.Count(bread));
        Assert.AreEqual(0f, ResidentMeals.WorkBonus(settlement.Find(people[0]), settlement));

        settlement.NoteBuilt(settings.MealsFrom, 1);
        string said = ResidentMeals.Eat(settlement, chests, settings);
        StringAssert.Contains("Breakfast", said);
        StringAssert.Contains(people[2].DisplayName, said, "the one who went short is named");
        Assert.AreEqual(0, chests.Count(bread), "the cooked loaf first");
        Assert.AreEqual(0, chests.Count(berries), "then the raw food, the cheapest first");
        Assert.AreEqual(0, chests.Count(turnip), "the last of it, not enough");
        Assert.AreEqual(5, chests.Count(meat), "raw meat kept for the cook");
        Assert.AreEqual(2, chests.Count(pie), "the player's pies kept");
        Assert.AreEqual(ResidentMeal.WellFed, settlement.Find(people[0]).Meal, "cooked food: well fed");
        Assert.AreEqual(ResidentMeal.Fed, settlement.Find(people[1]).Meal, "raw food: fed");
        Assert.AreEqual(ResidentMeal.Hungry, settlement.Find(people[2]).Meal, "two turnips aren't a day's food");
        Assert.AreEqual(settings.WellFedWork, ResidentMeals.WorkBonus(settlement.Find(people[0]), settlement), 0.0001f);
        Assert.AreEqual(0f, ResidentMeals.WorkBonus(settlement.Find(people[1]), settlement), 0.0001f);
        Assert.AreEqual(-settings.HungryWork, ResidentMeals.WorkBonus(settlement.Find(people[2]), settlement), 0.0001f);
        Assert.Less(settings.HungryWork, 1f, "hungry is slower, never stopped");

        // Saved and read back.
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), Content);
        Assert.AreEqual(ResidentMeal.WellFed, back.Find(people[0]).Meal);
        Assert.AreEqual(ResidentMeal.Hungry, back.Find(people[2]).Meal);
    }

    [Test]
    public void BreadIsBakedFromFlour_AndFlourIsGroundFromBarley()
    {
        RecipeDefinition flour = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/Flour.asset");
        RecipeDefinition bread = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/BarleyBread.asset");
        Assert.AreEqual("Barley", flour.ItemCost.Single().Item.name);
        Assert.AreEqual(2, flour.ItemCost.Single().Amount);
        Assert.AreEqual(1, bread.ItemCost.Count(c => c.Item == flour.Output), "bread from flour");
        Assert.IsFalse(bread.ItemCost.Any(c => c.Item != null && c.Item.name == "Barley"), "not from barley straight");
        Assert.AreEqual(flour.Station, bread.Station, "both at the Kitchen");
        Assert.IsTrue(Content.Items.Contains(flour.Output));
        JobDefinition cook = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Cook.asset");
        int flourAt = cook.WorkOrders.ToList().FindIndex(o => o.recipe == flour);
        int breadAt = cook.WorkOrders.ToList().FindIndex(o => o.recipe == bread);
        Assert.GreaterOrEqual(flourAt, 0, "the Cook grinds flour");
        Assert.Less(flourAt, breadAt, "before the bread that needs it");
    }
}
