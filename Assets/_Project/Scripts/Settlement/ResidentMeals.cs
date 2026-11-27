using System.Collections.Generic;
using System.Text;

/// <summary>What a resident ate this morning. Saved; 0 (Fed) is what an older save reads.</summary>
public enum ResidentMeal
{
    Fed = 0,
    WellFed = 1,
    Hungry = 2,
}

/// <summary>
/// The Haven's people eat (the user, 2026-11-22: "We should have the NPC's require food ... This will make farming useful and have a
/// sink", and then: "NPC's should eat anything that's a basic food source. Like bread, berry, mushroom etc. prefering cooked and prepared
/// meals before raw materials"). Once HomeLifeSettings › Meals From stands (the Kitchen), each dawn every resident eats from the chests
/// until they have had Daily Hunger: cooked and prepared food first (bread, a baked turnip, grilled fish, a stew), the cheapest first,
/// then raw food (berries, mushrooms, turnips). Never a dish above Most Meal Tier (the player's feasts and pies are kept) nor what is
/// listed Uneaten (raw meat and fish wait for the cook). Filled on cooked food alone is well fed (+Well Fed Work), filled with raw food
/// in it fed, not filled hungry (−Hungry Work): shown in their window and on their card in the Keeper's view. Nobody starves or
/// leaves; a hungry Haven only works slower (the positive rule, Docs/EconomyAndPhases_2026-11-22.md §4.3). Before the Kitchen they feed
/// themselves. Bread is ordinary food: the player eats and carries it too. Run by <see cref="HavenDawn"/>.
/// </summary>
public static class ResidentMeals
{
    /// <summary>True once the Haven's people expect their meals from its chests (Meals From is built).</summary>
    public static bool Expected(IHavenView settlement, HomeLifeSettings settings) =>
        settlement != null && settings != null && settings.DailyHunger > 0f
        && (settings.MealsFrom == null || settlement.BuiltTier(settings.MealsFrom) > 0);

    /// <summary>Cooked or prepared food (a dish someone made), as against a raw thing to eat (berries, a turnip).</summary>
    public static bool IsPrepared(ItemDefinition item) => item != null && item.Category == ItemCategory.Consumable;

    /// <summary>True when the Haven's people would eat <paramref name="item"/>: it feeds, it is basic (no higher than Most Meal Tier),
    /// and it isn't one left for the cook (Uneaten).</summary>
    public static bool IsFood(ItemDefinition item, HomeLifeSettings settings)
    {
        if (item == null || item.HungerRestore <= 0f || item.Tier > settings.MostMealTier
            || (item.Category != ItemCategory.Consumable && item.Category != ItemCategory.Material))
        {
            return false;
        }

        foreach (ItemDefinition kept in settings.Uneaten)
        {
            if (kept == item)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The foods in <paramref name="chests"/> in the order they are eaten: prepared before raw, then the cheapest, then the most filling.
    /// </summary>
    public static List<ItemDefinition> FoodsIn(ItemContainer chests, HomeLifeSettings settings)
    {
        var foods = new List<ItemDefinition>();
        for (int i = 0; i < chests.SlotCount; i++)
        {
            ItemStack stack = chests.GetSlot(i);
            if (!stack.IsEmpty && !foods.Contains(stack.Item) && IsFood(stack.Item, settings))
            {
                foods.Add(stack.Item);
            }
        }

        foods.Sort((a, b) =>
        {
            int prepared = IsPrepared(b).CompareTo(IsPrepared(a));
            if (prepared != 0)
            {
                return prepared;
            }

            int value = a.Value.CompareTo(b.Value);
            return value != 0 ? value : b.HungerRestore.CompareTo(a.HungerRestore);
        });
        return foods;
    }

    /// <summary>
    /// Breakfast at dawn: each resident eats from <paramref name="chests"/> and is marked well fed, fed or hungry. Returns the line told
    /// (what was eaten, who went short), or null when meals aren't expected yet or there is nobody.
    /// </summary>
    public static string Eat(SettlementState settlement, ItemContainer chests, HomeLifeSettings settings)
    {
        if (!Expected(settlement, settings) || chests == null || settlement.Residents.Count == 0)
        {
            return null;
        }

        List<ItemDefinition> foods = FoodsIn(chests, settings);
        var eaten = new Dictionary<ItemDefinition, int>();
        var order = new List<ItemDefinition>();
        var hungry = new List<string>();
        var treated = new List<string>();
        int wellFed = 0;
        foreach (ResidentRecord record in settlement.Residents)
        {
            // Away on a foray, they eat what they took for the road; their meal stands as it was.
            if (record == null || record.Resident == null || record.IsAway)
            {
                continue;
            }

            float had = 0f;
            bool raw = false;
            // Their favourite dish first, when the chests hold it and it is food they may eat (Docs/PeopleSystemReview_2026-11-23.md §2.6).
            // A treat: once in Favourite Every Days, and never the last Favourite Keeps of it, which stay the player's.
            ItemDefinition favourite = record.Resident.FavouriteDish;
            int lastHad = record.DeedOf(FavouriteDeed) - 1;
            bool due = lastHad < 0 || settlement.DaysPassed - lastHad >= settings.FavouriteEveryDays;
            if (favourite != null && due && foods.Contains(favourite) && chests.Count(favourite) > settings.FavouriteKeeps && chests.Remove(favourite, 1) > 0)
            {
                record.ForgetDeed(FavouriteDeed);
                record.AddDeed(FavouriteDeed, settlement.DaysPassed + 1);
                treated.Add(record.Resident.DisplayName);
                had += favourite.HungerRestore;
                raw |= !IsPrepared(favourite);
                if (!eaten.ContainsKey(favourite))
                {
                    order.Add(favourite);
                }

                eaten.TryGetValue(favourite, out int ate);
                eaten[favourite] = ate + 1;
                record.Feel(FavouriteFeeling, settlement.DaysPassed);
            }

            while (had < settings.DailyHunger)
            {
                ItemDefinition food = TakeFirst(chests, foods);
                if (food == null)
                {
                    break;
                }

                had += food.HungerRestore;
                raw |= !IsPrepared(food);
                if (!eaten.ContainsKey(food))
                {
                    order.Add(food);
                }

                eaten.TryGetValue(food, out int n);
                eaten[food] = n + 1;
            }

            if (had < settings.DailyHunger)
            {
                record.Meal = ResidentMeal.Hungry;
                hungry.Add(record.Resident.DisplayName);
            }
            else
            {
                record.Meal = raw ? ResidentMeal.Fed : ResidentMeal.WellFed;
                wellFed += raw ? 0 : 1;
            }
        }

        foreach (ItemDefinition food in order)
        {
            PlaytestLogger.Note(PlaytestLog.Eaten, food.Id, eaten[food], $"hungry {hungry.Count}");
        }

        var said = new StringBuilder();
        if (order.Count > 0)
        {
            var parts = new List<string>();
            foreach (ItemDefinition food in order)
            {
                parts.Add($"{eaten[food]} {food.DisplayName}");
            }

            said.Append("Breakfast: ").Append(string.Join(", ", parts)).Append(" eaten");
            said.Append(wellFed > 0 ? $"; {wellFed} well fed on cooked food (+{settings.WellFedWork * 100f:0}% work)." : ".");
        }

        if (treated.Count > 0)
        {
            said.Append(said.Length > 0 ? " " : "").Append($"A favourite for {Join(treated)}.");
        }

        if (hungry.Count > 0)
        {
            said.Append(said.Length > 0 ? " " : "").Append($"Not enough to eat for {Join(hungry)}: hungry today ({-settings.HungryWork * 100f:0}% work). ")
                .Append("Cooked food goes furthest: bread is baked from flour at the Kitchen.");
        }

        return said.ToString();
    }

    /// <summary>The feeling their favourite dish at breakfast leaves, for the day (ResidentSpirits).</summary>
    public const string FavouriteFeeling = "dish";

    // The day they last had their favourite, plus one (a deed, saved): favourites are a treat every few days.
    private const string FavouriteDeed = "favourite-day";

    /// <summary>How much faster (or slower) this resident works for what they ate this morning; 0 before meals are expected.</summary>
    public static float WorkBonus(ResidentRecord record) => WorkBonus(record, GameSession.Here);

    /// <summary>The same, in <paramref name="here"/> (tests).</summary>
    public static float WorkBonus(ResidentRecord record, IHavenView here)
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        if (record == null || !Expected(here, settings))
        {
            return 0f;
        }

        return record.Meal == ResidentMeal.WellFed ? settings.WellFedWork * (1f + FedWell(here, settings)) : record.Meal == ResidentMeal.Hungry ? -settings.HungryWork : 0f;
    }

    /// <summary>What a cook who feeds people well adds to everyone's Well Fed (a Proud Cook at the Kitchen: ResidentTraits, FeedsWell), as a
    /// share. Asked every frame by station speeds: indexed, no garbage.</summary>
    public static float FedWell(IHavenView here, HomeLifeSettings settings)
    {
        if (here == null || settings.MealsFrom == null)
        {
            return 0f;
        }

        float more = 0f;
        IReadOnlyList<ResidentRecord> people = here.Residents;
        ResidentDefinition along = ResidentsOut.Along;
        for (int i = 0; i < people.Count; i++)
        {
            // A cook at work now (ResidentsOut.AtWork): away alone was asked here, so a paused cook still fed everyone well.
            ResidentRecord cook = people[i];
            if (cook.Job != null && cook.Job.Workplace == settings.MealsFrom && ResidentsOut.AtWork(here, cook, along))
            {
                more += ResidentTraits.Sum(cook, TraitEffectKind.FeedsWell);
            }
        }

        return UnityEngine.Mathf.Max(0f, more);
    }

    /// <summary>Their meal in words, for their window and their card: empty before meals are expected.</summary>
    public static string Describe(ResidentRecord record)
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        if (record == null || !Expected(GameSession.Here, settings))
        {
            return "";
        }

        return record.Meal == ResidentMeal.WellFed ? $"Well fed: cooked food this morning (+{settings.WellFedWork * 100f:0}% work)."
            : record.Meal == ResidentMeal.Hungry ? $"Hungry: not enough in the chests to eat this morning ({-settings.HungryWork * 100f:0}% work)."
            : "Fed. Cooked food alone (bread, a stew, grilled fish) would make them well fed.";
    }

    /// <summary>The hunger the basic food in <paramref name="chests"/> would still the Haven's people with, all told.</summary>
    public static float HungerIn(ItemContainer chests, HomeLifeSettings settings)
    {
        float hunger = 0f;
        foreach (ItemDefinition food in FoodsIn(chests, settings))
        {
            hunger += food.HungerRestore * chests.Count(food);
        }

        return hunger;
    }

    /// <summary>Takes <paramref name="hunger"/>'s worth of food from <paramref name="chests"/> in the order they eat it (cooked first):
    /// food for the road (Forays). Returns the hunger taken.</summary>
    public static float TakeFood(ItemContainer chests, float hunger, HomeLifeSettings settings)
    {
        List<ItemDefinition> foods = FoodsIn(chests, settings);
        float taken = 0f;
        while (taken < hunger)
        {
            ItemDefinition food = TakeFirst(chests, foods);
            if (food == null)
            {
                break;
            }

            taken += food.HungerRestore;
        }

        return taken;
    }

    private static ItemDefinition TakeFirst(ItemContainer chests, List<ItemDefinition> foods)
    {
        while (foods.Count > 0)
        {
            ItemDefinition food = foods[0];
            if (chests.Count(food) > 0 && chests.Remove(food, 1) > 0)
            {
                return food;
            }

            foods.RemoveAt(0);
        }

        return null;
    }

    private static string Join(List<string> names) =>
        names.Count <= 1 ? string.Join("", names) : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
}

/// <summary>A dawn in one's own Haven, at home or passed while away: the day counts, the people eat (ResidentMeals), the lands turn.</summary>
public static class HavenDawn
{
    public static void Pass()
    {
        SettlementState home = GameSession.Home;
        home.PassDay();
        // A foray's bruises heal overnight (Forays).
        foreach (ResidentRecord record in home.Residents)
        {
            record.Bruised = false;
        }

        string breakfast = ResidentMeals.Eat(home, GameSession.Stash != null ? GameSession.Stash.Items : null, HomeLifeSettings.Current);
        if (!string.IsNullOrEmpty(breakfast))
        {
            GameMessages.Post(breakfast, breakfast.Contains("Not enough to eat") ? MessageKind.Warning : MessageKind.Info);
        }

        global::Territory.Turn(true);
        // The people's own day: feelings, friendships, inspired mornings (PeopleDay).
        PeopleDay.Dawn(home);
    }
}
