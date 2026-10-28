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

        GameSession.Stash.Items.SetUnlockedSlots(StashStorage.Columns * StashStorage.Rows);
    }

    [Test]
    public void TheBiggestChestStandingDecidesTheStashSize()
    {
        small = Chest("Small", 12);
        Assert.AreEqual(12, GameSession.Stash.Items.UnlockedSlots);
        big = Chest("Shed", 0);
        Assert.AreEqual(StashStorage.Columns * StashStorage.Rows, GameSession.Stash.Items.UnlockedSlots, "a shed opens all of it");
        Invoke(big.GetComponent<StashChest>(), "OnDisable");
        Object.DestroyImmediate(big);
        Assert.AreEqual(12, GameSession.Stash.Items.UnlockedSlots);
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

    private static GameObject Chest(string name, int slots)
    {
        var go = new GameObject(name);
        go.AddComponent<Interactable>();
        var chest = go.AddComponent<StashChest>();
        var data = new SerializedObject(chest);
        data.FindProperty("stashSlots").intValue = slots;
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(chest, "Awake");
        Invoke(chest, "OnEnable");
        return go;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
