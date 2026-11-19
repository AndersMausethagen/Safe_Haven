using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pack as last time (Docs/SystemsGapReview_2026-11-19.md §3): the kit a raid set out with is its consumables and quickbar items, never
/// its gear or materials; packing tops the bag up from the stash to that kit and says what the stash was short of.
/// </summary>
public sealed class PackForRaidTests
{
    private static ItemDefinition Item(string path) => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/" + path + ".asset");
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    [Test]
    public void TheKit_IsWhatIsUsed_NotGearOrMaterials()
    {
        ItemDefinition bandage = Item("Bandage"), gauntlets = Item("Armor/CopperScaleGauntlets"), stone = Item("Materials/StoneBlock");
        Assert.AreEqual(ItemCategory.Consumable, bandage.Category);
        var bag = new ItemContainer(6, 2);
        bag.Add(bandage, 3);
        bag.Add(gauntlets, 1);
        bag.Add(stone, 5);
        Dictionary<string, int> kit = PackForRaid.KitOf(bag, new QuickItems());
        Assert.AreEqual(1, kit.Count, string.Join(", ", kit.Keys));
        Assert.AreEqual(3, kit[bandage.Id]);
    }

    [Test]
    public void Packing_TopsUpFromTheStash_AndSaysWhatWasShort()
    {
        ItemDefinition bandage = Item("Bandage");
        var kit = new Dictionary<string, int> { [bandage.Id] = 6, ["no.such.item.at.all"] = 2 };
        var bag = new ItemContainer(6, 2);
        bag.Add(bandage, 1);
        var stash = new ItemContainer(10, 2);
        stash.Add(bandage, 3);
        var shortfall = new List<string>();
        int moved = PackForRaid.Fill(kit, bag, stash, Content, new QuickItems(), shortfall);
        Assert.AreEqual(3, moved, "all the stash had");
        Assert.AreEqual(4, bag.Count(bandage));
        Assert.AreEqual(0, stash.Count(bandage));
        Assert.AreEqual(1, shortfall.Count, "two short of six; an item no longer in the game is skipped");
        StringAssert.Contains("×2", shortfall[0]);

        stash.Add(bandage, 10);
        shortfall.Clear();
        PackForRaid.Fill(kit, bag, stash, Content, new QuickItems(), shortfall);
        Assert.AreEqual(6, bag.Count(bandage), "to the kit, no more");
        Assert.AreEqual(8, stash.Count(bandage));
        Assert.IsEmpty(shortfall);
    }
}
