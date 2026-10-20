using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode tests for deployed equipment (GDD §8.3, §13.4): what a failed raid keeps and wears down, and what repairs
/// cost.
/// </summary>
public sealed class DurabilityTests
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

    // ----- Failed raids -----

    [Test]
    public void FailedRaid_KeepsOnlyCarriedInEquipment_WornDown()
    {
        ItemDefinition axe = Gear("axe");
        ItemDefinition potion = Create<ItemDefinition>("potion");
        ResourceDefinition wood = Create<ResourceDefinition>("wood");
        var bag = new ItemContainer(4, 2);
        bag.SetSlot(2, new ItemStack(axe, 1));
        bag.SetSlot(3, new ItemStack(potion, 2));
        var carried = new ResourceStorage(100);
        carried.Add(wood, 20);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, carried, null);

        // During the raid a sword was looted and more wood gathered.
        bag.SetSlot(5, new ItemStack(Gear("sword"), 1));
        carried.Add(wood, 30);
        PlayerSnapshot atDeath = PlayerSnapshot.Capture(bag, carried, null);
        var wear = new List<EquipmentWear>();

        PlayerSnapshot kept = atDeath.AfterFailedRaid(loadout, 0.25f, wear);

        Assert.AreEqual(1, kept.Slots.Count, "Only the axe carried in is kept: loot and consumables are lost.");
        Assert.AreEqual(2, kept.Slots[0].Index);
        Assert.AreEqual(axe, kept.Slots[0].Stack.Item);
        Assert.AreEqual(0.75f, kept.Slots[0].Stack.Durability, 0.001f);
        Assert.IsEmpty(kept.Resources, "Carried materials are lost.");
        Assert.AreEqual(1, wear.Count);
        Assert.AreEqual(1f, wear[0].Before, 0.001f);
        Assert.AreEqual(0.75f, wear[0].After, 0.001f);
    }

    [Test]
    public void FailedRaid_BreaksWornGearButKeepsIt()
    {
        ItemDefinition axe = Gear("axe");
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(axe, 1, 0.1f));
        PlayerSnapshot snapshot = PlayerSnapshot.Capture(bag, null, null);

        PlayerSnapshot kept = snapshot.AfterFailedRaid(snapshot, 0.25f);

        Assert.AreEqual(1, kept.Slots.Count);
        Assert.IsTrue(kept.Slots[0].Stack.IsBroken);
    }

    [Test]
    public void FailedRaid_KeepsConditionAndTheEquippedSlotOfKeptGear()
    {
        ItemDefinition axe = Gear("axe");
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(1, new ItemStack(axe, 1));
        PlayerSnapshot snapshot = PlayerSnapshot.Capture(bag, null, null, null, null, new PlayerSnapshot.Condition(50f, 40f, 1));

        PlayerSnapshot kept = snapshot.AfterFailedRaid(snapshot, 0.25f);

        Assert.AreEqual(1, kept.State.EquippedSlot);
        Assert.AreEqual(50f, kept.State.Hunger);
        Assert.AreEqual(40f, kept.State.Thirst);
    }

    [Test]
    public void FailedRaid_DoesNotKeepAnEquippedWeaponLootedInTheRaid()
    {
        var bag = new ItemContainer(2, 1);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, null, null);
        bag.SetSlot(0, new ItemStack(Gear("sword"), 1));
        PlayerSnapshot atDeath = PlayerSnapshot.Capture(bag, null, null, null, null, new PlayerSnapshot.Condition(50f, 40f, 0));

        PlayerSnapshot kept = atDeath.AfterFailedRaid(loadout, 0.25f);

        Assert.IsEmpty(kept.Slots);
        Assert.AreEqual(-1, kept.State.EquippedSlot);
    }

    [Test]
    public void Settings_CountFailedRaidsUntilNewGearBreaks()
    {
        DurabilitySettings settings = Create<DurabilitySettings>("durability");

        // Default: 25% per failed raid.
        Assert.AreEqual(4, settings.FailedRaidsUntilBroken);
    }

    // ----- Repairs -----

    [Test]
    public void RepairCost_ScalesWithMissingDurability_RoundedUp()
    {
        (ItemDefinition axe, RecipeDefinition recipe, ResourceDefinition wood, ResourceDefinition stone) = AxeRecipe();

        // Half worn: 50% of the recipe for a full repair x 50% missing = 25%: 10 wood -> 2.5 -> 3, 4 stone -> 1.
        List<ResourceAmount> cost = Repairs.Cost(new ItemStack(axe, 1, 0.5f), recipe, Create<DurabilitySettings>("rules")).Resources;

        Assert.AreEqual(2, cost.Count);
        Assert.AreEqual(wood, cost[0].Resource);
        Assert.AreEqual(3, cost[0].Amount);
        Assert.AreEqual(stone, cost[1].Resource);
        Assert.AreEqual(1, cost[1].Amount);
    }

    [Test]
    public void RepairCost_FromBrokenIsTheConfiguredShareOfTheRecipe()
    {
        (ItemDefinition axe, RecipeDefinition recipe, _, _) = AxeRecipe();

        List<ResourceAmount> cost = Repairs.Cost(new ItemStack(axe, 1, 0f), recipe, Create<DurabilitySettings>("rules")).Resources;

        Assert.AreEqual(5, cost[0].Amount);
        Assert.AreEqual(2, cost[1].Amount);
    }

    [Test]
    public void RepairCost_NeverDropsBelowTheMinimumPerMaterial()
    {
        (ItemDefinition axe, RecipeDefinition recipe, _, _) = AxeRecipe();

        List<ResourceAmount> cost = Repairs.Cost(new ItemStack(axe, 1, 0.99f), recipe, Create<DurabilitySettings>("rules")).Resources;

        Assert.AreEqual(1, cost[0].Amount);
        Assert.AreEqual(1, cost[1].Amount);
    }

    [Test]
    public void RepairCost_IsFreeForUndamagedGearOrGearWithoutARecipe()
    {
        (ItemDefinition axe, RecipeDefinition recipe, _, _) = AxeRecipe();
        DurabilitySettings settings = Create<DurabilitySettings>("rules");

        Assert.IsTrue(Repairs.Cost(new ItemStack(axe, 1), recipe, settings).IsFree);
        Assert.IsTrue(Repairs.Cost(new ItemStack(axe, 1, 0.2f), null, settings).IsFree);
    }

    [Test]
    public void RecipeFor_FindsTheRecipeThatMakesTheItem()
    {
        (ItemDefinition axe, RecipeDefinition recipe, _, _) = AxeRecipe();
        RecipeDefinition other = Create<RecipeDefinition>("other");

        Assert.AreEqual(recipe, Repairs.RecipeFor(axe, new[] { other, recipe }));
        Assert.IsNull(Repairs.RecipeFor(Gear("sword"), new[] { other, recipe }));
    }

    // ----- Helpers -----

    private (ItemDefinition, RecipeDefinition, ResourceDefinition, ResourceDefinition) AxeRecipe()
    {
        ItemDefinition axe = Gear("axe");
        ResourceDefinition wood = Create<ResourceDefinition>("wood");
        ResourceDefinition stone = Create<ResourceDefinition>("stone");
        RecipeDefinition recipe = Create<RecipeDefinition>("axe_recipe");
        var data = new SerializedObject(recipe);
        data.FindProperty("output").objectReferenceValue = axe;
        SerializedProperty cost = data.FindProperty("resourceCost");
        cost.arraySize = 2;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("resource").objectReferenceValue = wood;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 10;
        cost.GetArrayElementAtIndex(1).FindPropertyRelative("resource").objectReferenceValue = stone;
        cost.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 4;
        data.ApplyModifiedPropertiesWithoutUndo();
        return (axe, recipe, wood, stone);
    }

    // A tool, so it has durability.
    private ItemDefinition Gear(string name)
    {
        ItemDefinition item = Create<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)ToolType.Axe;
        data.FindProperty("toolTier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
