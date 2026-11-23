using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Every dish can be made from things the world gives (the user, 2026-11-23: "add a bunch more ingredients and recipes ... make new
/// ingredients drop as well if needed. or new plants or animals"): each ingredient a food or tonic recipe asks for is dropped by a
/// creature or a cache (a LootTable), grows on a gathering node, or is made by another recipe.
/// </summary>
public sealed class FoodSourcesTests
{
    private static List<T> All<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(id => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(asset => asset != null).ToList();

    [Test]
    public void EveryIngredientOfADish_ComesFromSomewhere()
    {
        var recipes = All<RecipeDefinition>();
        var sources = new HashSet<ItemDefinition>();
        foreach (RecipeDefinition recipe in recipes)
        {
            sources.Add(recipe.Output);
        }

        foreach (LootTable table in All<LootTable>())
        {
            sources.UnionWith(table.Drops.Entries.Where(entry => entry != null).Select(entry => entry.Item));
        }

        foreach (GatheringNodeDefinition node in All<GatheringNodeDefinition>())
        {
            sources.UnionWith(node.Yields.Entries.Concat(node.BurstYields.Entries).Where(entry => entry != null).Select(entry => entry.Item));
        }

        // Water is drawn at a well or a stream (WaterSource), not dropped or grown.
        var missing = new List<string>();
        foreach (RecipeDefinition recipe in recipes.Where(recipe => recipe.Output != null && (recipe.Output.HasBoon || recipe.Output.HungerRestore > 0f)))
        {
            foreach (ItemAmount cost in recipe.ItemCost)
            {
                if (cost.Item != null && !sources.Contains(cost.Item) && !cost.Item.IsWaterMeasure)
                {
                    missing.Add($"{recipe.name}: {cost.Item.name}");
                }
            }
        }

        Assert.IsEmpty(missing, "ingredients nothing gives");
        Assert.IsTrue(sources.Any(item => item != null && item.name == "Venison"), "canary: deer and stags give venison");
    }

    [Test]
    public void EveryNewForagePlant_GrowsInARaid()
    {
        var plants = new[] { "RamsonsNode", "AngelicaNode", "LingonberriesNode", "BogMyrtleNode" };
        var raids = All<RaidDefinition>();
        foreach (string plant in plants)
        {
            bool spawns = raids.Any(raid =>
            {
                var it = new SerializedObject(raid).GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null && it.objectReferenceValue.name == plant)
                    {
                        return true;
                    }
                }

                return false;
            });
            Assert.IsTrue(spawns, $"{plant} grows in some raid");
        }
    }
}
