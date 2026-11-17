using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Breaking things down at the Workshop (Salvage, SalvageBench; loot overhaul step 4): junk gives what it is made of, gear a share of its
/// recipe that shrinks as it wears, nothing else can be broken, and breaking takes the item and hands over exactly what was shown.
/// </summary>
public sealed class SalvageTests
{
    private static ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/" + path);

    private static IReadOnlyList<RecipeDefinition> Recipes =>
        AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset").Recipes;

    [Test]
    public void EveryJunkFind_BreaksIntoSomething()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data/Items/Salvage" }))
        {
            var junk = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            List<ItemAmount> yield = Salvage.YieldOf(new ItemStack(junk, 1), Recipes, DurabilitySettings.Current);
            Assert.IsNotEmpty(yield, junk.name);
            Assert.IsTrue(yield.All(part => part.Item != null && part.Amount > 0), junk.name);
        }
    }

    [Test]
    public void Gear_GivesAShareOfItsRecipe_LessWhenWorn()
    {
        ItemDefinition shield = Item("Weapons/IronKiteShield.asset") ?? AssetDatabase.FindAssets("t:ItemDefinition IronKiteShield")
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First();
        RecipeDefinition recipe = Repairs.RecipeFor(shield, Recipes);
        Assert.IsNotNull(recipe);
        int Total(IEnumerable<ItemAmount> parts) => parts.Sum(part => part.Amount);
        int full = Total(Salvage.YieldOf(new ItemStack(shield, 1, 1f), Recipes, DurabilitySettings.Current));
        int worn = Total(Salvage.YieldOf(new ItemStack(shield, 1, 0.3f), Recipes, DurabilitySettings.Current));
        Assert.Greater(full, 0);
        Assert.Less(full, Total(recipe.ItemCost), "breaking down loses some: making it again costs more");
        Assert.Less(worn, full, "a worn piece gives less");
    }

    [Test]
    public void MaterialsAndFood_CantBeBrokenDown()
    {
        Assert.IsEmpty(Salvage.YieldOf(new ItemStack(Item("Materials/IronIngot.asset"), 3), Recipes, DurabilitySettings.Current));
        Assert.IsEmpty(Salvage.YieldOf(new ItemStack(Item("Bandage.asset"), 3), Recipes, DurabilitySettings.Current));
    }

    [Test]
    public void BreakingDown_TakesTheItems_AndGivesWhatWasShown()
    {
        var player = new GameObject("Salvager");
        try
        {
            var bag = player.AddComponent<ItemInventory>();
            ItemDefinition sickle = Item("Salvage/BrokenSickle.asset");
            ItemDefinition iron = Item("Materials/IronIngot.asset");
            bag.Container.SetSlot(0, new ItemStack(sickle, 3));
            List<ItemAmount> shown = Salvage.YieldOf(bag.Container.GetSlot(0), Recipes, DurabilitySettings.Current);

            Assert.IsTrue(Salvage.TryBreakDown(player, bag.Container, 0, 2, Recipes));

            Assert.AreEqual(1, bag.Container.GetSlot(0).Count, "two of three broken");
            Assert.AreEqual(shown.Single(part => part.Item == iron).Amount * 2, bag.Container.Count(iron));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void TheWorkshop_OffersBreakingDown()
    {
        var table = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/ConstructionTable.prefab");
        Assert.IsNotNull(table.GetComponent<SalvageBench>());
        Assert.IsTrue(string.IsNullOrEmpty(table.GetComponentInChildren<SaveableEntity>(true)?.Id), "a prefab asset keeps no save id of its own");
    }
}
