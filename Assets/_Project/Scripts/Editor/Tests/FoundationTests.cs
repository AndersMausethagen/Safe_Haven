using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Guards the foundation of tiers I and II: what can be made at home, what a raid holds, and people working on their own.</summary>
public sealed class FoundationTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
    }

    private static IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static RaidDefinition MistyWoods => All<RaidDefinition>().First(raid => raid.name == "MistyWoods");

    [Test]
    public void TheWorkshop_HasThingsToMakeFromWhatLiesAtHome()
    {
        var atHome = new HashSet<string> { "OakWood", "GreyStone", "Sticks", "SmallRocks", "NettleFiber", "NettleCord" };
        List<RecipeDefinition> day1 = All<RecipeDefinition>().Where(recipe => recipe.Station == CraftingStationType.Workbench && recipe.KnownFromStart
            && recipe.RequiredStationTier == 1 && recipe.ItemCost.All(cost => atHome.Contains(cost.Item.name)) && recipe.ResourceCost.Count == 0).ToList();
        Assert.GreaterOrEqual(day1.Count(recipe => recipe.Output.Category == ItemCategory.Equipment), 3, "gear to make before the first raid");
        Assert.IsTrue(day1.Any(recipe => recipe.Output.Category == ItemCategory.Consumable), "something to heal with");
    }

    [TestCase("IronOreNode")]
    [TestCase("HardwoodNode")]
    [TestCase("FlaxNode")]
    [TestCase("GraniteNode")]
    [TestCase("MoonsilverOreNode")]
    [TestCase("CopperOreNode")]
    public void ARaid_HoldsTheMaterialsTheLadderNeeds(string prefab)
    {
        RaidSpawnList resources = MistyWoods.SpawnLists.First(list => list.name == "Resources");
        Assert.IsTrue(resources.entries.Any(entry => entry.prefab != null && entry.prefab.name == prefab), prefab);
    }

    [Test]
    public void Bjarne_CanBeFound_OnceTheSmithyStands()
    {
        RaidRumourDefinition rumour = All<RaidRumourDefinition>().First(one => one.name == "Bjarne");
        Assert.IsTrue(MistyWoods.GetSystem<RaidRumoursSystem>().Pool.Contains(rumour));
        Assert.IsTrue(rumour.Urgent);
        var settlement = new SettlementState();
        for (int i = 0; i < 3; i++)
        {
            settlement.RecordExtraction();
        }

        Assert.IsFalse(rumour.CanComeUp(null, settlement, null), "nowhere to work yet");
        settlement.NoteBuilt(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Forge.asset"), 1);
        Assert.IsTrue(rumour.CanComeUp(null, settlement, null));
    }

    [Test]
    public void ACraftsmanWithAWorkshop_IsSoughtBeforeAnyRumourByChance()
    {
        var settlement = new SettlementState();
        settlement.RecordExtraction();
        settlement.NoteBuilt(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Sawbench.asset"), 1);
        for (int seed = 1; seed <= 12; seed++)
        {
            List<RaidRumourDefinition> picked = RaidRumours.For(MistyWoods, seed, settlement, null);
            Assert.IsTrue(picked.Any(rumour => rumour.name == "Tor"), $"seed {seed}");
        }
    }

    [Test]
    public void AWorker_MakesWhatIsOrdered_FromTheStash_AndCarriesItBack()
    {
        (JobDefinition job, RecipeDefinition recipe, ItemDefinition log, ItemDefinition plank) = Carpenter(stock: 10, keep: 2);
        var settlement = new SettlementState();
        settlement.AddResident(Person("Tor", job), job);
        StationQueue queue = settlement.Workshops.GetOrCreate("lodge", CraftingStationType.Sawbench, 10, 5);
        var stash = new StashStorage();
        stash.Items.Add(log, 6);

        WorkerProduction.Work(settlement, stash);

        Assert.AreEqual(recipe, queue.Current, "started on its own");
        Assert.AreEqual(2, stash.Items.Count(log), "paid from the stash");

        queue.Tick(recipe.CraftSeconds + 0.1f, stash.Resources);
        WorkerProduction.Work(settlement, stash);

        Assert.AreEqual(2, stash.Items.Count(plank), "carried to the stash");
        Assert.IsNull(queue.Output);
    }

    [Test]
    public void AWorker_LeavesWhatIsKept_AndStopsAtPlenty()
    {
        (JobDefinition job, RecipeDefinition _, ItemDefinition log, ItemDefinition plank) = Carpenter(stock: 3, keep: 4);
        var settlement = new SettlementState();
        settlement.AddResident(Person("Tor", job), job);
        StationQueue queue = settlement.Workshops.GetOrCreate("lodge", CraftingStationType.Sawbench, 10, 5);
        var stash = new StashStorage();
        stash.Items.Add(log, 7);

        WorkerProduction.Work(settlement, stash);
        Assert.IsTrue(queue.IsIdle, "7 logs leave less than the 4 to keep after a craft of 4");

        stash.Items.Add(log, 20);
        stash.Items.Add(plank, 3);
        WorkerProduction.Work(settlement, stash);
        Assert.IsTrue(queue.IsIdle, "there is plenty");
    }

    [Test]
    public void AWorker_KeepsToTheStationsTier_AndStopsWhenPaused()
    {
        (JobDefinition job, RecipeDefinition _, ItemDefinition log, ItemDefinition _) = Carpenter(stock: 10, keep: 0);
        var settlement = new SettlementState();
        ResidentDefinition tor = Person("Tor", job);
        settlement.AddResident(tor, job);
        StationQueue queue = settlement.Workshops.GetOrCreate("lodge", CraftingStationType.Sawbench, 10, 5);
        var stash = new StashStorage();
        stash.Items.Add(log, 20);

        settlement.Find(tor).WorkPaused = true;
        WorkerProduction.Work(settlement, stash);
        Assert.IsTrue(queue.IsIdle, "paused");

        settlement.Find(tor).WorkPaused = false;
        queue.Tier = 0;
        WorkerProduction.Work(settlement, stash);
        Assert.IsTrue(queue.IsIdle, "the station's tier is too low");
    }

    [Test]
    public void WithoutAWorkplace_NobodyWorks()
    {
        (JobDefinition job, RecipeDefinition _, ItemDefinition log, ItemDefinition _) = Carpenter(stock: 10, keep: 0);
        var settlement = new SettlementState();
        settlement.AddResident(Person("Tor", job), job);
        var stash = new StashStorage();
        stash.Items.Add(log, 20);

        WorkerProduction.Work(settlement, stash);

        Assert.AreEqual(20, stash.Items.Count(log));
    }

    [Test]
    public void TheCarpenterAndSmith_HaveOrders()
    {
        Assert.IsNotEmpty(All<JobDefinition>().First(job => job.name == "Carpenter").WorkOrders);
        Assert.IsNotEmpty(All<JobDefinition>().First(job => job.name == "Smith").WorkOrders);
    }

    private (JobDefinition job, RecipeDefinition recipe, ItemDefinition log, ItemDefinition plank) Carpenter(int stock, int keep)
    {
        ItemDefinition log = Item("Log");
        ItemDefinition plank = Item("Plank");
        var recipe = Make<RecipeDefinition>("Plank Recipe");
        var recipeData = new SerializedObject(recipe);
        recipeData.FindProperty("output").objectReferenceValue = plank;
        recipeData.FindProperty("outputAmount").intValue = 2;
        recipeData.FindProperty("station").intValue = (int)CraftingStationType.Sawbench;
        recipeData.FindProperty("craftSeconds").floatValue = 1f;
        SerializedProperty cost = recipeData.FindProperty("itemCost");
        cost.arraySize = 1;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = log;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
        recipeData.ApplyModifiedPropertiesWithoutUndo();

        var lodge = Make<BuildingDefinition>("Lodge");
        var lodgeData = new SerializedObject(lodge);
        lodgeData.FindProperty("station").intValue = (int)CraftingStationType.Sawbench;
        lodgeData.ApplyModifiedPropertiesWithoutUndo();

        var job = Make<JobDefinition>("Carpenter Job");
        var jobData = new SerializedObject(job);
        jobData.FindProperty("workplace").objectReferenceValue = lodge;
        SerializedProperty orders = jobData.FindProperty("workOrders");
        orders.arraySize = 1;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("recipe").objectReferenceValue = recipe;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("stockTarget").intValue = stock;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("keepInput").intValue = keep;
        jobData.ApplyModifiedPropertiesWithoutUndo();
        return (job, recipe, log, plank);
    }

    private ItemDefinition Item(string name)
    {
        var item = Make<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("category").enumValueIndex = (int)ItemCategory.Material;
        data.FindProperty("tier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private ResidentDefinition Person(string name, JobDefinition job)
    {
        var person = Make<ResidentDefinition>(name);
        var data = new SerializedObject(person);
        SerializedProperty jobs = data.FindProperty("jobs");
        jobs.arraySize = 1;
        jobs.GetArrayElementAtIndex(0).objectReferenceValue = job;
        data.ApplyModifiedPropertiesWithoutUndo();
        return person;
    }

    private T Make<T>(string name) where T : ScriptableObject
    {
        var thing = ScriptableObject.CreateInstance<T>();
        thing.name = name;
        created.Add(thing);
        return thing;
    }
}
