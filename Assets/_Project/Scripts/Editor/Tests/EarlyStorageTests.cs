using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EarlyStorageTests
{
    private GameObject small;
    private GameObject big;

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject chest in new[] { big, small })
        {
            if (chest != null)
            {
                Invoke(chest.GetComponent<StashChest>(), "OnDisable");
                Object.DestroyImmediate(chest);
            }
        }

        GameSession.Stash.Items.Clear();
        GameSession.Stash.Items.SetUnlockedSlots(GameSession.Stash.Items.SlotCount);
    }

    // The user, 2026-11-15: two chests shared one inventory; each has its own now (a block of the stash).
    [Test]
    public void EachChestKeepsItsOwnItems()
    {
        // Loaded together, as a save's chests are: the biggest takes the first block.
        small = Chest("Small", 12, start: false);
        big = Chest("Shed", 0, start: false);
        StashChest smallChest = small.GetComponent<StashChest>(), shed = big.GetComponent<StashChest>();
        Invoke(smallChest, "Start");
        Invoke(shed, "Start");
        Invoke(smallChest, "LateUpdate");
        Assert.AreEqual(0, shed.Block, "the biggest chest takes the first block (an old save's whole stash)");
        Assert.AreEqual(1, smallChest.Block);
        Assert.AreEqual(12 + StashStorage.ChestSlots, GameSession.Stash.Items.UnlockedSlots, "the chests' room adds up");

        var berries = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Berries.asset");
        var bag = new ItemContainer(4, 1);
        bag.Add(berries, 7);
        Assert.AreEqual(7, GameSession.Stash.Store(bag, 0, null, smallChest.Block));
        Assert.AreEqual(7, Held(smallChest, berries), "stored in the chest it was put in");
        Assert.AreEqual(0, Held(shed, berries), "not in the other one");
        Assert.AreEqual(7, GameSession.Stash.Items.Count(berries), "crafting and workers still see all chests together");

        Invoke(smallChest, "OnDisable");
        Assert.AreEqual(StashStorage.ChestSlots, GameSession.Stash.Items.UnlockedSlots, "a chest that is gone closes its block");
        Assert.AreEqual(7, GameSession.Stash.Items.Count(berries), "and keeps what was in it");
        Invoke(smallChest, "OnEnable");
        Assert.AreEqual(1, smallChest.Block, "a chest keeps its block");
    }

    [Test]
    public void QuickStackSendsWhatChestsHoldToThem()
    {
        small = Chest("Small", 12);
        big = Chest("Shed", 0);
        StashChest smallChest = small.GetComponent<StashChest>(), shed = big.GetComponent<StashChest>();
        var berries = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Berries.asset");
        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        var stew = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StewedBerries.asset");
        GameSession.Stash.Items.AddWithin(berries, 2, 0f, smallChest.FirstSlot, StashStorage.ChestSlots);
        GameSession.Stash.Items.AddWithin(stew, 1, 0f, shed.FirstSlot, StashStorage.ChestSlots);
        var bag = new ItemContainer(4, 1);
        bag.Add(berries, 5);
        bag.Add(sticks, 3);
        bag.Add(stew, 2);

        int moved = GameSession.Stash.StackOnStored(bag, stack => stack.Item == stew);
        Assert.AreEqual(5, moved);
        Assert.AreEqual(7, Held(smallChest, berries), "onto the chest that already holds berries");
        Assert.AreEqual(3, bag.Count(sticks), "no chest holds sticks, so they stay");
        Assert.AreEqual(2, bag.Count(stew), "what the bars hold stays with the player");
    }

    private static int Held(StashChest chest, ItemDefinition item)
    {
        int count = 0;
        for (int i = chest.FirstSlot; i < chest.FirstSlot + StashStorage.ChestSlots; i++)
        {
            ItemStack stack = GameSession.Stash.Items.GetSlot(i);
            count += stack.Item == item ? stack.Count : 0;
        }

        return count;
    }

    [Test]
    public void TheSmallChestIsCheapAndKnownFromTheStart()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        BuildingDefinition chest = book.Buildings.FirstOrDefault(b => b != null && b.name == "SmallChest");
        Assert.IsNotNull(chest, "in the build menu");
        Assert.IsTrue(chest.KnownFromStart);
        CollectionAssert.AreEquivalent(new[] { "OakWood", "Sticks" }, chest.ConstructionItems.Select(c => c.Item.name));
        Assert.AreEqual(12, chest.Prefab.GetComponent<StashChest>().StashSlots);
        Assert.IsNotNull(chest.Icon);
    }

    [Test]
    public void TheBarkPackIsMadeByHandFromRawMaterials()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        RecipeDefinition recipe = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.name == "BarkPack");
        Assert.IsNotNull(recipe);
        Assert.IsTrue(recipe.IsHandcrafted && recipe.KnownFromStart);
        CollectionAssert.AreEquivalent(new[] { "Sticks", "NettleFiber" }, recipe.ItemCost.Select(c => c.Item.name));
        Assert.IsTrue(recipe.Output.IsBackpack);
        var satchel = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/WovenSatchel.asset");
        Assert.Less(recipe.Output.CarryCapacityBonus, satchel.CarryCapacityBonus, "the satchel is still the better pack");
    }

    private static GameObject Chest(string name, int slots, bool start = true)
    {
        var go = new GameObject(name);
        go.AddComponent<Interactable>();
        var chest = go.AddComponent<StashChest>();
        var data = new SerializedObject(chest);
        data.FindProperty("stashSlots").intValue = slots;
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(chest, "Awake");
        Invoke(chest, "OnEnable");
        if (start)
        {
            Invoke(chest, "Start");
            Invoke(chest, "LateUpdate");
        }

        return go;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
