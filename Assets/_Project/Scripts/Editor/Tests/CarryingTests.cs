using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode tests for carrying (GDD §12.4): the bag's weight budget, and the MaterialPool rule that costs are paid from
/// what is carried first and, at home, from storage.
/// </summary>
public sealed class CarryingTests
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

    // ----- Carry weight -----

    [Test]
    public void Weight_LimitsHowManyItemsFitInTheBag()
    {
        ItemDefinition wood = Item("wood", 1f);
        ItemDefinition stone = Item("stone", 2f);
        var bag = new ItemContainer(10, 1);
        bag.SetWeightCapacity(150f);

        Assert.AreEqual(100, bag.Add(wood, 100));
        Assert.AreEqual(100f, bag.CurrentWeight, 0.001f);
        Assert.AreEqual(50f, bag.FreeWeight, 0.001f);
        Assert.AreEqual(25, bag.SpaceFor(stone), "Slots have room for more; weight is what stops the stone.");
        Assert.AreEqual(25, bag.Add(stone, 40));
        Assert.AreEqual(150f, bag.CurrentWeight, 0.001f);
    }

    [Test]
    public void Weight_BonusesRaiseTheBudgetUntilRemoved()
    {
        var bag = new ItemContainer(10, 1);
        bag.SetWeightCapacity(150f);
        object backpack = new object();

        bag.SetWeightBonus(backpack, 50f);
        Assert.AreEqual(200f, bag.WeightCapacity, 0.001f);

        bag.RemoveWeightBonus(backpack);
        Assert.AreEqual(150f, bag.WeightCapacity, 0.001f);
    }

    [Test]
    public void Weight_ContainerWithoutABudgetIgnoresWeight()
    {
        ItemDefinition stone = Item("stone", 2f);
        var stash = new ItemContainer(20, 1);

        Assert.IsFalse(stash.HasWeightLimit);
        Assert.IsTrue(float.IsPositiveInfinity(stash.FreeWeight));
        Assert.AreEqual(500, stash.Add(stone, 500));
    }

    [Test]
    public void Weight_MovingIntoAFullBagIsRefusedButMovingOutIsNot()
    {
        ItemDefinition stone = Item("stone", 2f);
        var bag = new ItemContainer(10, 1);
        bag.SetWeightCapacity(10f);
        var stash = new ItemContainer(10, 1);
        stash.Add(stone, 20);
        bag.Add(stone, 5);

        Assert.IsFalse(stash.MoveTo(0, bag, 1), "Twenty stones weigh more than the bag's free budget.");
        Assert.IsTrue(bag.MoveTo(0, stash, 5));
        Assert.AreEqual(0f, bag.CurrentWeight, 0.001f);
    }

    // ----- Material pool -----

    [Test]
    public void Pool_SpendsCarriedMaterialsBeforeStorage()
    {
        ResourceDefinition wood = Resource("wood");
        var carried = new ResourceStorage(100);
        var stored = new ResourceStorage(1000);
        carried.Add(wood, 4);
        stored.Add(wood, 10);
        var pool = new MaterialPool(carried, null, stored);

        Assert.AreEqual(14, pool.Have(wood));
        Assert.IsTrue(pool.TrySpend(new[] { new ResourceAmount(wood, 6) }));

        Assert.AreEqual(0, carried.GetAmount(wood));
        Assert.AreEqual(8, stored.GetAmount(wood));
    }

    [Test]
    public void Pool_NeverPaysHalfACost()
    {
        ResourceDefinition wood = Resource("wood");
        ResourceDefinition stone = Resource("stone");
        var carried = new ResourceStorage(100);
        carried.Add(wood, 4);
        carried.Add(stone, 1);
        var pool = new MaterialPool(carried, null);

        Assert.IsFalse(pool.TrySpend(new[] { new ResourceAmount(wood, 3), new ResourceAmount(stone, 2) }));

        Assert.AreEqual(4, carried.GetAmount(wood));
        Assert.AreEqual(1, carried.GetAmount(stone));
    }

    [Test]
    public void Pool_InARaidCountsOnlyWhatIsCarried()
    {
        ResourceDefinition wood = Resource("wood");
        var carried = new ResourceStorage(100);
        carried.Add(wood, 4);
        var pool = new MaterialPool(carried, null);

        Assert.IsFalse(pool.IncludesStorage);
        Assert.IsFalse(pool.CanAfford(new[] { new ResourceAmount(wood, 6) }, null, out string missing));
        StringAssert.Contains("4/6", missing);
    }

    [Test]
    public void Pool_TakesItemsFromTheBagBeforeStorage()
    {
        ItemDefinition rune = Create<ItemDefinition>("rune");
        var bag = new ItemContainer(2, 1);
        var storedItems = new ItemContainer(4, 1);
        bag.Add(rune, 1);
        storedItems.Add(rune, 2);
        var pool = new MaterialPool(null, bag, null, storedItems);

        Assert.IsTrue(pool.TrySpend(null, new[] { new ItemAmount(rune, 2) }));

        Assert.AreEqual(0, bag.Count(rune));
        Assert.AreEqual(1, storedItems.Count(rune));
    }

    [Test]
    public void Pool_AddPutsWhatDoesNotFitIntoStorage()
    {
        ResourceDefinition wood = Resource("wood");
        var carried = new ResourceStorage(10);
        var stored = new ResourceStorage(1000);
        var pool = new MaterialPool(carried, null, stored);

        Assert.AreEqual(25, pool.Add(wood, 25));

        Assert.AreEqual(10, carried.GetAmount(wood));
        Assert.AreEqual(15, stored.GetAmount(wood));
    }

    // ----- Helpers -----

    private ResourceDefinition Resource(string name)
    {
        return Create<ResourceDefinition>(name);
    }

    private ItemDefinition Item(string name, float weight)
    {
        ItemDefinition item = Create<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("weight").floatValue = weight;
        data.FindProperty("stackable").boolValue = true;
        data.FindProperty("maximumStack").intValue = 50;
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
