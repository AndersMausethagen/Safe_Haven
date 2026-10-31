using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Moving a stack onto a slot holding the same item: gear never stacks, so two copies change places.</summary>
public sealed class ItemMoveTests
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

    private ItemDefinition Item(string name)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.name = name;
        created.Add(item);
        return item;
    }

    private ItemDefinition Gear(string name)
    {
        ItemDefinition item = Item(name);
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)ToolType.Axe;
        data.FindProperty("toolTier").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private ItemDefinition Stacking(string name, int size)
    {
        ItemDefinition item = Item(name);
        var data = new SerializedObject(item);
        data.FindProperty("stackable").boolValue = true;
        data.FindProperty("maximumStack").intValue = size;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    [Test]
    public void TwoCopiesOfTheSameGear_ChangePlaces()
    {
        ItemDefinition axe = Gear("axe");
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(axe, 1, 0.2f));
        bag.SetSlot(1, new ItemStack(axe, 1, 1f));

        Assert.IsTrue(bag.MoveTo(0, bag, 1));

        Assert.AreEqual(1f, bag.GetSlot(0).Durability, 0.001f);
        Assert.AreEqual(0.2f, bag.GetSlot(1).Durability, 0.001f);
        Assert.AreEqual(2, bag.CountItems());
    }

    [Test]
    public void AFreshCopy_ReplacesAWornOne_FromAFullBag()
    {
        ItemDefinition axe = Gear("axe");
        var bag = new ItemContainer(1, 1);
        var worn = new ItemContainer(1, 1);
        bag.SetSlot(0, new ItemStack(axe, 1, 1f));
        worn.SetSlot(0, new ItemStack(axe, 1, 0.1f));

        Assert.IsTrue(bag.MoveTo(0, worn, 0));

        Assert.AreEqual(1f, worn.GetSlot(0).Durability, 0.001f);
        Assert.AreEqual(0.1f, bag.GetSlot(0).Durability, 0.001f);
    }

    [Test]
    public void StacksOfTheSameItem_StillMerge_AndWhatDoesNotFitStaysBehind()
    {
        ItemDefinition wood = Stacking("wood", 10);
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(wood, 6));
        bag.SetSlot(1, new ItemStack(wood, 7));

        Assert.IsTrue(bag.MoveTo(0, bag, 1));

        Assert.AreEqual(3, bag.GetSlot(0).Count);
        Assert.AreEqual(10, bag.GetSlot(1).Count);
    }

    [Test]
    public void AFullStack_TakesNoMoreOfItsItem()
    {
        ItemDefinition wood = Stacking("wood", 10);
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(wood, 4));
        bag.SetSlot(1, new ItemStack(wood, 10));

        Assert.IsFalse(bag.MoveTo(0, bag, 1));

        Assert.AreEqual(4, bag.GetSlot(0).Count);
        Assert.AreEqual(10, bag.GetSlot(1).Count);
    }
}
