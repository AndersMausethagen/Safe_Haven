using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The quickbar (hold F): favourites picked in the bag, kept in their places, saved with the player.</summary>
public sealed class QuickItemsTests
{
    private static ItemDefinition Stew => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StewedBerries.asset");
    private static ItemDefinition Lantern => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/PrimitiveFireflyLantern.asset");

    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object made in created)
        {
            if (made != null)
            {
                UnityEngine.Object.DestroyImmediate(made);
            }
        }

        created.Clear();
    }

    [Test]
    public void Toggling_AddsAndRemoves_InOrder_UpToEight()
    {
        Assert.IsTrue(Stew.IsUsable && Lantern.IsUsable, "the test's items can be used from the bag");
        var quick = new QuickItems();
        int changes = 0;
        quick.Changed += () => changes++;

        Assert.IsTrue(quick.Toggle(Stew));
        Assert.IsTrue(quick.Toggle(Lantern));
        CollectionAssert.AreEqual(new[] { Stew, Lantern }, quick.Items, "each keeps the place it was given");
        Assert.IsFalse(quick.Toggle(Stew), "a second press takes it off");
        CollectionAssert.AreEqual(new[] { Lantern }, quick.Items);
        Assert.AreEqual(3, changes);

        var plain = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(plain);
        Assert.IsFalse(plain.IsUsable);
        Assert.IsFalse(quick.Toggle(plain), "only things the bag can use");
        Assert.AreEqual(1, quick.Items.Count);

        // Up to its capacity: one usable item more than it holds is refused, and nothing is pushed off for it.
        var full = new QuickItems();
        List<ItemDefinition> usable = new List<ItemDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && item.IsUsable && !usable.Contains(item) && usable.Count <= QuickItems.Capacity)
            {
                usable.Add(item);
            }
        }

        Assert.AreEqual(QuickItems.Capacity + 1, usable.Count, "enough usable items to overfill the quickbar");
        for (int i = 0; i < QuickItems.Capacity; i++)
        {
            Assert.IsTrue(full.Toggle(usable[i]), usable[i].name);
        }

        Assert.IsFalse(full.Toggle(usable[QuickItems.Capacity]), "the one past the capacity is refused");
        Assert.AreEqual(QuickItems.Capacity, full.Items.Count);
        CollectionAssert.AreEqual(usable.GetRange(0, QuickItems.Capacity), full.Items, "the first ones keep their places");
    }

    // The user, 2026-11-15: with keys 1-5 full, new consumables went nowhere; they take the next empty slot of 1-9, however
    // they reach the bag, and putting a saved bag back doesn't count as new.
    [Test]
    public void NewConsumablesTakeTheNextEmptySkillSlot()
    {
        var player = new GameObject("Bar Player");
        created.Add(player);
        player.AddComponent<PlayerStateMachine>();
        var items = player.AddComponent<ItemInventory>();
        var skills = player.AddComponent<PlayerSkills>();
        Invoke(items, "Awake");
        Invoke(skills, "Awake");
        Invoke(skills, "OnEnable");
        for (int i = 0; i < 5; i++)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            created.Add(skill);
            skills.Loadout.Equip(i, skill);
        }

        Assert.AreEqual(5, skills.Loadout.FirstEmptySlot(), "1-5 are full");
        items.Container.Add(Stew, 2);
        Invoke(items, "LateUpdate");
        Assert.AreEqual(5, skills.Loadout.IndexOf(Stew), "it goes on key 6");

        items.Container.Remove(Stew, 2);
        Invoke(items, "LateUpdate");
        skills.Loadout.Clear(5);
        items.BeginRestore();
        items.Container.Add(Stew, 1);
        items.EndRestore();
        Invoke(items, "LateUpdate");
        Assert.AreEqual(-1, skills.Loadout.IndexOf(Stew), "a bag put back from a save is not new");
        Invoke(skills, "OnDisable");
    }

    // The user, 2026-11-15: Stewed Berries take 10 of the water carried, not a whole waterskin.
    [Test]
    public void WaterIsMeasuredFromTheWaterskins()
    {
        var water = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Water.asset");
        var skin = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Waterskin.asset");
        Assert.IsTrue(water.IsWaterMeasure);
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/StewedBerries.asset");
        CollectionAssert.Contains(new List<ItemAmount>(recipe.ItemCost).ConvertAll(cost => cost.Item.name + " " + cost.Amount), "Water 10");

        var bag = new ItemContainer(4, 1);
        bag.AddStack(new ItemStack(skin, 1).WithWater(25f));
        Assert.AreEqual(25, bag.Count(water));
        Assert.AreEqual(10, bag.Remove(water, 10));
        Assert.AreEqual(15f, bag.GetSlot(0).Water, 0.01f, "the waterskin keeps the rest");
        Assert.AreEqual(1, bag.Count(skin), "and stays in the bag");
        Assert.AreEqual(5, bag.Add(water, 5), "a refund pours back in");
        Assert.AreEqual(20f, bag.GetSlot(0).Water, 0.01f);
        Assert.AreEqual(20, bag.Remove(water, 30), "only what is there");
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);
    }

    [Test]
    public void Restore_KeepsOrder_AndSkipsRepeatsAndUnusables()
    {
        var plain = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(plain);
        var quick = new QuickItems();
        quick.Restore(new[] { Lantern, Stew, Lantern, plain, null });
        CollectionAssert.AreEqual(new[] { Lantern, Stew }, quick.Items);
        quick.Restore(null);
        Assert.AreEqual(0, quick.Items.Count);
    }

    [Test]
    public void TheQuickbar_IsSavedAndLoaded_AndOutlivesAFailedRaid()
    {
        var lookup = new ItemLookup(Stew, Lantern);
        PlayerSnapshot player = PlayerSnapshot.Capture(new ItemContainer(8, 5), null, null).WithQuickItems(new[] { Lantern, Stew });
        SaveData data = SaveMapper.ToSaveData(player, null, "SafeHaven", null, DateTime.UtcNow);
        CollectionAssert.AreEqual(new[] { Lantern.Id, Stew.Id }, data.player.quickItems);

        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);
        CollectionAssert.AreEqual(new[] { Lantern, Stew }, loaded.Player.QuickBar);
        CollectionAssert.AreEqual(new[] { Lantern, Stew }, loaded.Player.WithoutBelongings().QuickBar,
            "a preference, not belongings: a failed raid keeps it");
    }

    // The systems review, 2026-11-21: what is marked to keep stays in the bag when storing, and the marks are saved.
    [Test]
    public void KeptItems_AreSavedAndLoaded_AndOutliveAFailedRaid()
    {
        var lookup = new ItemLookup(Stew, Lantern);
        PlayerSnapshot player = PlayerSnapshot.Capture(new ItemContainer(8, 5), null, null).WithKeptItems(new[] { Stew });
        SaveData data = SaveMapper.ToSaveData(player, null, "SafeHaven", null, DateTime.UtcNow);
        CollectionAssert.AreEqual(new[] { Stew.Id }, data.player.keptItems);

        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);
        CollectionAssert.AreEqual(new[] { Stew }, loaded.Player.KeptItems);
        CollectionAssert.AreEqual(new[] { Stew }, loaded.Player.WithoutBelongings().KeptItems);

        var marks = new KeptItems();
        Assert.IsTrue(marks.Toggle(Lantern));
        Assert.IsTrue(marks.Contains(Lantern));
        Assert.IsFalse(marks.Toggle(Lantern), "pressed again, the mark comes off");
        Assert.IsFalse(marks.Contains(Lantern));
    }

    // Finds only the two items; everything else is missing (the test saves nothing else).
    private sealed class ItemLookup : IContentLookup
    {
        private readonly Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();

        public ItemLookup(params ItemDefinition[] known)
        {
            foreach (ItemDefinition item in known)
            {
                items[item.Id] = item;
            }
        }

        public ItemDefinition FindItem(string id) => id != null && items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }
}
