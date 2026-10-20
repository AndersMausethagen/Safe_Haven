using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode tests for station crafting: the queue, timing, output slot, storage-first delivery and saving.</summary>
public sealed class StationQueueTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    [Test]
    public void Tick_DeliversItemsToOutputSlotAfterCraftTime()
    {
        ItemDefinition axe = Create<ItemDefinition>("axe");
        RecipeDefinition recipe = Recipe("axe_recipe", axe, null, 1, 10f);
        var queue = new StationQueue("forge", CraftingStationType.Anvil, 10, 5);
        Assert.AreEqual(2, queue.Enqueue(recipe, 2));

        Assert.AreEqual(0, queue.Tick(9f, null));
        Assert.AreEqual(1, queue.Tick(2f, null));
        Assert.AreEqual(axe, queue.Output);
        Assert.AreEqual(1, queue.OutputAmount);
        Assert.AreEqual(1f, queue.Progress, 0.001f);
        Assert.AreEqual(1, queue.Tick(9f, null));
        Assert.AreEqual(2, queue.OutputAmount);
        Assert.IsTrue(queue.IsIdle);
        Assert.AreEqual(0f, queue.Progress);
    }

    [Test]
    public void Tick_LongAbsenceCatchesUpSeveralCrafts()
    {
        ResourceDefinition plank = Create<ResourceDefinition>("plank");
        RecipeDefinition recipe = Recipe("plank_recipe", null, plank, 2, 4f);
        var queue = new StationQueue("saw", CraftingStationType.Sawbench, 10, 5);
        queue.Enqueue(recipe, 5);
        var storage = new ResourceStorage(100);

        Assert.AreEqual(5, queue.Tick(600f, storage));
        Assert.AreEqual(10, storage.GetAmount(plank));
        Assert.IsNull(queue.Output);
    }

    [Test]
    public void Tick_ResourcesFillStorageFirstThenOutputSlot()
    {
        ResourceDefinition ingot = Create<ResourceDefinition>("ingot");
        RecipeDefinition recipe = Recipe("ingot_recipe", null, ingot, 3, 1f);
        var queue = new StationQueue("furnace", CraftingStationType.Smelter, 10, 5);
        queue.Enqueue(recipe);
        var storage = new ResourceStorage(5);
        storage.Add(ingot, 4);

        Assert.AreEqual(1, queue.Tick(1f, storage));
        Assert.AreEqual(5, storage.GetAmount(ingot));
        Assert.AreEqual(ingot, queue.Output);
        Assert.AreEqual(2, queue.OutputAmount);
    }

    [Test]
    public void Tick_FullOrDifferentOutputBlocksUntilCollected()
    {
        ItemDefinition axe = Create<ItemDefinition>("axe");
        ItemDefinition sword = Create<ItemDefinition>("sword");
        RecipeDefinition axes = Recipe("axe_recipe", axe, null, 1, 1f);
        RecipeDefinition swords = Recipe("sword_recipe", sword, null, 1, 1f);
        var queue = new StationQueue("forge", CraftingStationType.Anvil, 2, 5);
        queue.Enqueue(axes, 3);
        queue.Enqueue(swords);

        Assert.AreEqual(2, queue.Tick(10f, null));
        Assert.IsTrue(queue.Blocked);
        Assert.AreEqual(2, queue.OutputAmount);
        Assert.AreEqual(0, queue.Tick(10f, null), "nothing more fits");

        Assert.AreEqual(2, queue.TakeOutput(99));
        Assert.IsFalse(queue.Blocked);
        Assert.IsNull(queue.Output);
        Assert.AreEqual(1, queue.Tick(0.01f, null), "the waiting axe is delivered at once");
        Assert.AreEqual(axe, queue.Output);
        Assert.AreEqual(0, queue.Tick(1f, null), "a sword can't share the slot with an axe");
        Assert.IsTrue(queue.Blocked);
    }

    [Test]
    public void Enqueue_RespectsQueueLength()
    {
        RecipeDefinition recipe = Recipe("axe_recipe", Create<ItemDefinition>("axe"), null, 1, 1f);
        var queue = new StationQueue("table", CraftingStationType.Workbench, 10, 3);

        Assert.AreEqual(3, queue.Enqueue(recipe, 5));
        Assert.AreEqual(0, queue.Enqueue(recipe));
        Assert.AreEqual(0, queue.FreeQueueSlots);
    }

    [Test]
    public void CancelLast_OnlyRemovesUnstartedCrafts()
    {
        RecipeDefinition axes = Recipe("axe_recipe", Create<ItemDefinition>("axe"), null, 1, 10f);
        RecipeDefinition swords = Recipe("sword_recipe", Create<ItemDefinition>("sword"), null, 1, 10f);
        var queue = new StationQueue("forge", CraftingStationType.Anvil, 10, 5);
        queue.Enqueue(axes);
        queue.Enqueue(swords);
        queue.Tick(5f, null);

        Assert.AreEqual(swords, queue.CancelLast());
        Assert.IsFalse(queue.CanCancelLast, "the axe in progress has started");
        Assert.IsNull(queue.CancelLast());
        Assert.AreEqual(1, queue.QueuedCount);
    }

    [Test]
    public void Workshops_RoundTripThroughSave()
    {
        ItemDefinition axe = Create<ItemDefinition>("axe");
        ResourceDefinition ingot = Create<ResourceDefinition>("ingot");
        RecipeDefinition axes = Recipe("axe_recipe", axe, null, 1, 10f);
        var settlement = new SettlementState();
        StationQueue forge = settlement.Workshops.GetOrCreate("forge-1", CraftingStationType.Anvil, 10, 5);
        forge.Enqueue(axes, 3);
        forge.Tick(14f, null);
        settlement.Workshops.GetOrCreate("idle", CraftingStationType.Loom, 10, 5);

        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(new ItemContainer(4, 2), null, null), null, "SafeHaven", null,
            System.DateTime.UtcNow, settlement: settlement);
        var lookup = new TestLookup();
        lookup.Items[axe.Id] = axe;
        lookup.Resources[ingot.Id] = ingot;
        lookup.Recipes[axes.Id] = axes;
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);

        Assert.IsEmpty(loaded.MissingContent);
        StationQueue restored = loaded.Settlement.Workshops.Find("forge-1");
        Assert.IsNotNull(restored);
        Assert.AreEqual(CraftingStationType.Anvil, restored.Type);
        Assert.AreEqual(2, restored.QueuedCount);
        Assert.AreEqual(4f, restored.Progress, 0.001f);
        Assert.AreEqual(axe, restored.Output);
        Assert.AreEqual(1, restored.OutputAmount);
        Assert.IsNull(loaded.Settlement.Workshops.Find("idle"), "empty stations aren't saved");
    }

    [Test]
    public void OlderSavesWithoutStationsLoad()
    {
        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(new ItemContainer(4, 2), null, null), null, "SafeHaven", null, System.DateTime.UtcNow);
        data.settlement.stations = null;

        LoadedGame loaded = SaveMapper.FromSaveData(data, new TestLookup());

        CollectionAssert.IsEmpty(loaded.Settlement.Workshops.Queues);
    }

    private sealed class TestLookup : IContentLookup
    {
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
        public readonly Dictionary<string, ResourceDefinition> Resources = new Dictionary<string, ResourceDefinition>();
        public readonly Dictionary<string, RecipeDefinition> Recipes = new Dictionary<string, RecipeDefinition>();
        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => id != null && Resources.TryGetValue(id, out ResourceDefinition r) ? r : null;
        public RecipeDefinition FindRecipe(string id) => id != null && Recipes.TryGetValue(id, out RecipeDefinition r) ? r : null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }

    private RecipeDefinition Recipe(string id, ItemDefinition item, ResourceDefinition resource, int amount, float seconds)
    {
        RecipeDefinition recipe = Create<RecipeDefinition>(id);
        var data = new SerializedObject(recipe);
        data.FindProperty("output").objectReferenceValue = item;
        data.FindProperty("outputResource").objectReferenceValue = resource;
        data.FindProperty("outputAmount").intValue = amount;
        data.FindProperty("craftSeconds").floatValue = seconds;
        data.FindProperty("station").intValue = (int)CraftingStationType.Anvil;
        data.ApplyModifiedPropertiesWithoutUndo();
        return recipe;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
