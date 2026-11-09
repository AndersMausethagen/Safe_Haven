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
        Assert.AreEqual(8, QuickItems.Capacity);
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
