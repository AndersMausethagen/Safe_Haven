using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode tests for the crafting UI's pure parts: recipe tabs, output cells and recipe tooltips.</summary>
public sealed class CraftingUITests
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
    public void Build_GroupsByOutputAndPutsAllFirst()
    {
        RecipeDefinition axe = Recipe("axe", Item("Axe", ItemCategory.Tool), null);
        RecipeDefinition stew = Recipe("stew", Item("Stew", ItemCategory.Consumable), null);
        RecipeDefinition pick = Recipe("pick", Item("Pick", ItemCategory.Tool), null);
        RecipeDefinition spark = Recipe("spark", null, Create<ResourceDefinition>("Spark"));

        List<RecipeTabs.Tab> tabs = RecipeTabs.Build(new[] { stew, axe, spark, pick });

        CollectionAssert.AreEqual(new[] { RecipeTabs.All, "Tools", "Consumables", RecipeTabs.Resources }, tabs.ConvertAll(t => t.Label));
        CollectionAssert.AreEqual(new[] { stew, axe, spark, pick }, tabs[0].Recipes);
        CollectionAssert.AreEqual(new[] { axe, pick }, tabs[1].Recipes);
    }

    [Test]
    public void Build_OneKindGetsOneTabAndNothingGetsNone()
    {
        RecipeDefinition plank = Recipe("plank", Item("Plank", ItemCategory.Material), null);

        List<RecipeTabs.Tab> tabs = RecipeTabs.Build(new[] { plank, null });

        Assert.AreEqual(1, tabs.Count);
        Assert.AreEqual("Materials", tabs[0].Label);
        Assert.AreEqual(0, RecipeTabs.Build(new RecipeDefinition[0]).Count);
    }

    [Test]
    public void SplitIntoCells_FillsFullStacksThenPutsOverflowInLastCell()
    {
        CollectionAssert.AreEqual(new[] { 20, 5, 0, 0 }, RecipeTabs.SplitIntoCells(25, 20, 4));
        CollectionAssert.AreEqual(new[] { 1, 1, 3 }, RecipeTabs.SplitIntoCells(5, 1, 3));
        CollectionAssert.AreEqual(new[] { 0, 0 }, RecipeTabs.SplitIntoCells(0, 10, 2));
        CollectionAssert.AreEqual(new[] { 7 }, RecipeTabs.SplitIntoCells(7, 0, 1));
    }

    [Test]
    public void DescribeRecipe_ShowsOutputStatsCostsStationTimeAndProblem()
    {
        ItemDefinition axe = Item("Axe", ItemCategory.Tool);
        var itemData = new SerializedObject(axe);
        itemData.FindProperty("description").stringValue = "Chops trees.";
        itemData.ApplyModifiedPropertiesWithoutUndo();
        ResourceDefinition wood = Create<ResourceDefinition>("Wood");
        RecipeDefinition recipe = Recipe("axe", axe, null);
        var data = new SerializedObject(recipe);
        SerializedProperty costs = data.FindProperty("resourceCost");
        costs.arraySize = 1;
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("resource").objectReferenceValue = wood;
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 3;
        data.FindProperty("craftSeconds").floatValue = 90f;
        data.FindProperty("station").intValue = (int)CraftingStationType.Anvil;
        data.ApplyModifiedPropertiesWithoutUndo();

        string text = ItemTooltipView.DescribeRecipe(recipe, null, "The queue is full.");

        StringAssert.Contains("Tool", text);
        StringAssert.Contains("Chops trees.", text);
        StringAssert.Contains("3 Wood", text);
        StringAssert.Contains("Forge  ·  1 m 30 s", text);
        StringAssert.Contains("The queue is full.", text);
        StringAssert.DoesNotContain("Right-click", text);
    }

    private ItemDefinition Item(string name, ItemCategory category)
    {
        ItemDefinition item = Create<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("category").intValue = (int)category;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private RecipeDefinition Recipe(string name, ItemDefinition item, ResourceDefinition resource)
    {
        RecipeDefinition recipe = Create<RecipeDefinition>(name);
        var data = new SerializedObject(recipe);
        data.FindProperty("output").objectReferenceValue = item;
        data.FindProperty("outputResource").objectReferenceValue = resource;
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
