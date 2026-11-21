using System.Collections.Generic;
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

    [Test]
    public void StoreInChests_PutsMaterialsAwayEvenWhenNoChestHoldsThemYet()
    {
        small = Chest("Small", 12);
        StashChest smallChest = small.GetComponent<StashChest>();
        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        var stew = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StewedBerries.asset");
        Assert.AreEqual(ItemCategory.Material, sticks.Category);
        Assert.AreNotEqual(ItemCategory.Material, stew.Category);
        var bag = new ItemContainer(4, 1);
        bag.Add(sticks, 3);
        bag.Add(stew, 2);
        Assert.IsTrue(GameSession.Stash.HasSomethingToStore(bag, null), "a material is something to store");

        int moved = GameSession.Stash.StackOnStored(bag, null) + GameSession.Stash.StoreMaterials(bag, null, null);
        Assert.AreEqual(3, moved);
        Assert.AreEqual(3, Held(smallChest, sticks), "the material went into the chest with room");
        Assert.AreEqual(2, bag.Count(stew), "food no chest holds stays with the player");
        Assert.IsFalse(GameSession.Stash.HasSomethingToStore(bag, null), "nothing left the prompt would offer");
    }

    // The systems review, 2026-11-21: a chest set to be passed over keeps what it has; the Store key fills the others.
    [Test]
    public void AChestSetToBePassedOver_TakesNothingFromTheStoreKey()
    {
        small = Chest("Small", 12);
        big = Chest("Shed", 0);
        StashChest kept = small.GetComponent<StashChest>(), shed = big.GetComponent<StashChest>();
        var berries = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Berries.asset");
        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        GameSession.Stash.Items.AddWithin(berries, 2, 0f, kept.FirstSlot, StashStorage.ChestSlots);
        kept.SkipsStore = true;
        Assert.IsTrue(StashChest.BlockSkipsStore(kept.Block));
        Assert.IsFalse(StashChest.BlockSkipsStore(shed.Block));
        var bag = new ItemContainer(4, 1);
        bag.Add(berries, 5);
        bag.Add(sticks, 3);

        int moved = GameSession.Stash.StackOnStored(bag, null, null, StashChest.BlockSkipsStore)
                    + GameSession.Stash.StoreMaterials(bag, null, null, -1, StashChest.BlockSkipsStore);
        Assert.AreEqual(8, moved);
        Assert.AreEqual(2, Held(kept, berries), "the passed-over chest keeps only what it had");
        Assert.AreEqual(0, Held(kept, sticks));
        Assert.AreEqual(5, Held(shed, berries), "its berries went to the other chest instead");
        Assert.AreEqual(3, Held(shed, sticks));

        // Without the skip, the same chest takes them (the canary: the rule above is the skip's, not the chests' order).
        bag.Add(berries, 4);
        GameSession.Stash.StackOnStored(bag, null);
        Assert.Greater(Held(kept, berries), 2, "unskipped, the chest that holds berries takes more");
    }

    [Test]
    public void AChestSetToBePassedOver_IsSaved()
    {
        small = Chest("Small", 12);
        StashChest chest = small.GetComponent<StashChest>();
        chest.SkipsStore = true;
        string saved = chest.CaptureState();
        chest.SkipsStore = false;
        chest.RestoreState(saved);
        Assert.IsTrue(chest.SkipsStore);
        chest.RestoreState("{\"block\":0}");
        Assert.IsFalse(chest.SkipsStore, "a save from before the switch fills it as before");
    }

    // Taking a building down (BuildingDemolition) never loses what a chest holds: as in Palworld, empty it first.
    [Test]
    public void AChestWithThingsInIt_IsNotTakenDown()
    {
        small = Chest("Small", 12);
        StashChest chest = small.GetComponent<StashChest>();
        BuildingObject building = small.AddComponent<BuildingObject>();
        var data = new SerializedObject(building);
        data.FindProperty("definition").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/SmallChest.asset");
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsNull(BuildingDemolition.WhyNot(building), "an empty chest can go");

        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        GameSession.Stash.Items.AddWithin(sticks, 3, 0f, chest.FirstSlot, StashStorage.ChestSlots);
        Assert.IsNotNull(BuildingDemolition.WhyNot(building), "with things in it, it stays");
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
        // Cheap: built from what the first tools gather, whatever the amounts are tuned to.
        Assert.IsNotEmpty(chest.ConstructionItems);
        Assert.IsTrue(chest.ConstructionItems.All(c => c.Item != null && c.Item.Tier <= 1),
            "built from tier 0 and 1 materials: " + string.Join(", ", chest.ConstructionItems.Select(c => c.Item != null ? c.Item.name : "nothing")));
        Assert.Greater(chest.Prefab.GetComponent<StashChest>().StashSlots, 0);
        Assert.IsNotNull(chest.Icon);
    }

    [Test]
    public void TheBarkPackIsMadeByHandFromRawMaterials()
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        RecipeDefinition recipe = book.Recipes.FirstOrDefault(r => r != null && r.Output != null && r.Output.name == "BarkPack");
        Assert.IsNotNull(recipe);
        Assert.IsTrue(recipe.IsHandcrafted && recipe.KnownFromStart);
        // Raw: every ingredient is picked or cut from a gathering node, nothing that has to be made first.
        var gathered = new HashSet<ItemDefinition>(AssetDatabase.FindAssets("t:GatheringNodeDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(node => node != null)
            .SelectMany(node => node.Yields.Entries.Select(entry => entry.Item)));
        Assert.IsNotEmpty(recipe.ItemCost);
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            Assert.IsTrue(cost.Item != null && gathered.Contains(cost.Item), $"{(cost.Item != null ? cost.Item.name : "nothing")} isn't gathered raw");
        }

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

    [Test]
    public void TheStorageShed_GrowsWithItsTiers_UpToAChestsMost()
    {
        // The shed began at a chest's most (60), so its tiers' room was capped away (the user chose a smaller start, 2026-11-21).
        var shed = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/StorageShed.asset");
        var chest = shed.Prefab.GetComponent<StashChest>();
        int room = chest.StashSlots;
        Assert.Less(room, StashStorage.ChestSlots, "room to grow");
        for (int level = 2; level <= shed.MaxLevel; level++)
        {
            int more = shed.UpgradeTo(level).extraStorage;
            Assert.Greater(more, 0, $"Tier {level} adds room");
            room += more;
            Assert.LessOrEqual(room, StashStorage.ChestSlots, $"Tier {level} stays within a chest's most");
        }

        Assert.AreEqual(StashStorage.ChestSlots, room, "the top tier holds a chest's most");
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
