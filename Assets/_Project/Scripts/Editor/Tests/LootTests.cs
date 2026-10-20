using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Edit Mode tests for the loot rules: rarity and value, locked bag slots (carry space) and the safe pocket that survives
/// a failed raid.
/// </summary>
public sealed class LootTests
{
    private sealed class Lookup : IContentLookup
    {
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();

        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
    }

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

    // ----- Locked slots -----

    [Test]
    public void LockedSlots_TakeNoNewItems()
    {
        ItemDefinition ingot = Item("ingot", stack: 10);
        var bag = new ItemContainer(4, 1);
        bag.SetUnlockedSlots(2);

        int added = bag.Add(ingot, 35);

        Assert.AreEqual(20, added);
        Assert.AreEqual(20, bag.SpaceFor(ingot) + bag.Count(ingot));
        Assert.IsTrue(bag.GetSlot(2).IsEmpty);
        Assert.AreEqual(-1, bag.FirstEmptySlot());
    }

    [Test]
    public void LockingSlots_KeepsWhatTheyHold()
    {
        ItemDefinition axe = Item("axe", stack: 1);
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(3, new ItemStack(axe, 1));
        bag.SetUnlockedSlots(1);

        Assert.AreEqual(axe, bag.GetSlot(3).Item);
        Assert.IsFalse(bag.IsUnlocked(3));
        bag.Move(3, 0);
        Assert.AreEqual(axe, bag.GetSlot(0).Item, "Items can still be moved out of a locked slot.");
    }

    [Test]
    public void Move_IntoALockedSlot_DoesNothing()
    {
        ItemDefinition axe = Item("axe", stack: 1);
        ItemDefinition pick = Item("pick", stack: 1);
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(0, new ItemStack(axe, 1));
        bag.SetSlot(3, new ItemStack(pick, 1));
        bag.SetUnlockedSlots(2);

        Assert.IsFalse(bag.MoveTo(0, bag, 3));
        Assert.IsFalse(bag.MoveTo(3, bag, 0), "Swapping would push the other stack into the locked slot.");
        Assert.AreEqual(axe, bag.GetSlot(0).Item);
        Assert.AreEqual(pick, bag.GetSlot(3).Item);
    }

    [Test]
    public void LayoutChanged_IsRaisedOnlyWhenTheCountChanges()
    {
        var bag = new ItemContainer(4, 1);
        int raised = 0;
        bag.LayoutChanged += () => raised++;

        bag.SetUnlockedSlots(4);
        bag.SetUnlockedSlots(2);
        bag.SetUnlockedSlots(2);
        bag.SetUnlockedSlots(99);

        Assert.AreEqual(2, raised);
        Assert.AreEqual(4, bag.UnlockedSlots);
    }

    // ----- Safe pocket -----

    [Test]
    public void MoveTo_AnotherContainer_MovesMergesAndSwaps()
    {
        ItemDefinition relic = Item("relic", stack: 5);
        ItemDefinition idol = Item("idol", stack: 1);
        var bag = new ItemContainer(4, 1);
        var pocket = new ItemContainer(1, 1);
        bag.SetSlot(0, new ItemStack(relic, 2));
        bag.SetSlot(1, new ItemStack(relic, 4));
        bag.SetSlot(2, new ItemStack(idol, 1));

        Assert.IsTrue(bag.MoveTo(0, pocket, 0));
        Assert.IsTrue(bag.MoveTo(1, pocket, 0));
        Assert.AreEqual(5, pocket.GetSlot(0).Count);
        Assert.AreEqual(1, bag.GetSlot(1).Count, "What doesn't fit the stack stays behind.");

        Assert.IsTrue(bag.MoveTo(2, pocket, 0));
        Assert.AreEqual(idol, pocket.GetSlot(0).Item);
        Assert.AreEqual(relic, bag.GetSlot(2).Item);
        Assert.AreEqual(5, bag.GetSlot(2).Count);
    }

    [Test]
    public void FailedRaid_KeepsTheSafePocketOnly()
    {
        ItemDefinition relic = Item("relic", stack: 5);
        ItemDefinition axe = Item("axe", stack: 1);
        var bag = new ItemContainer(4, 1);
        var pocket = new ItemContainer(1, 1);
        bag.Add(axe, 1);
        pocket.Add(relic, 2);
        PlayerSnapshot carried = PlayerSnapshot.Capture(bag, null, null, pocket);

        PlayerSnapshot kept = carried.WithoutBelongings();
        var newBag = new ItemContainer(4, 1);
        var newPocket = new ItemContainer(1, 1);
        kept.ApplyTo(newBag, null, null, newPocket);

        Assert.AreEqual(0, newBag.Count(axe));
        Assert.AreEqual(2, newPocket.Count(relic));
    }

    [Test]
    public void Report_DoesNotCountTheSafePocketAsLost()
    {
        ItemDefinition relic = Item("relic", stack: 5);
        ItemDefinition axe = Item("axe", stack: 1, value: 10);
        var bag = new ItemContainer(4, 1);
        var pocket = new ItemContainer(1, 1);
        bag.Add(axe, 1);
        pocket.Add(relic, 1);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, null, null, pocket);
        RaidSession session = Session();
        session.End(RaidOutcome.Died);

        RaidReport report = RaidReport.Create(session, loadout, loadout.WithoutBelongings());

        Assert.AreEqual(1, report.ItemsLost.Count);
        Assert.AreEqual(axe, report.ItemsLost[0].Item);
        Assert.AreEqual(10, report.ValueLost);
    }

    [Test]
    public void FailedRaid_ListsLootFoundAndLost_AndPocketedLootAsSaved()
    {
        ItemDefinition potion = Item("potion", stack: 5, value: 5);
        ItemDefinition coin = Item("coin", stack: 50, value: 10);
        ItemDefinition idol = Item("idol", stack: 1, value: 150);
        var bag = new ItemContainer(4, 1);
        var pocket = new ItemContainer(1, 1);
        bag.Add(potion, 2);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, null, null, pocket);
        bag.Add(coin, 6);
        pocket.Add(idol, 1);
        PlayerSnapshot final = PlayerSnapshot.Capture(bag, null, null, pocket);
        RaidSession session = Session();
        session.End(RaidOutcome.TimeExpired);

        RaidReport report = RaidReport.Create(session, loadout, final.WithoutBelongings(), final);

        Assert.AreEqual(1, report.ItemsGained.Count);
        Assert.AreEqual(idol, report.ItemsGained[0].Item);
        Assert.AreEqual(2 * 5 + 6 * 10, report.ValueLost);
        var lost = new List<ItemDefinition>();
        foreach (ItemAmount entry in report.ItemsLost)
        {
            lost.Add(entry.Item);
        }

        CollectionAssert.AreEquivalent(new[] { potion, coin }, lost);
    }

    [Test]
    public void PocketThatNoLongerFits_GoesToTheBag()
    {
        ItemDefinition relic = Item("relic", stack: 5);
        ItemDefinition idol = Item("idol", stack: 1);
        var oldPocket = new ItemContainer(2, 1);
        oldPocket.SetSlot(0, new ItemStack(relic, 3));
        oldPocket.SetSlot(1, new ItemStack(idol, 1));
        PlayerSnapshot snapshot = PlayerSnapshot.Capture(null, null, null, oldPocket);

        var bag = new ItemContainer(4, 1);
        var pocket = new ItemContainer(1, 1);
        snapshot.ApplyTo(bag, null, null, pocket);

        Assert.AreEqual(3, pocket.Count(relic));
        Assert.AreEqual(1, bag.Count(idol));
    }

    [Test]
    public void Save_RoundTripsTheSafePocket()
    {
        var lookup = new Lookup();
        ItemDefinition relic = Item("relic", stack: 5);
        lookup.Items[relic.Id] = relic;
        var pocket = new ItemContainer(1, 1);
        pocket.Add(relic, 4);

        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null, pocket), null, "SampleScene", null, DateTime.UtcNow);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);
        var restored = new ItemContainer(1, 1);
        loaded.Player.ApplyTo(new ItemContainer(2, 1), null, null, restored);

        Assert.AreEqual(4, restored.Count(relic));
    }

    [Test]
    public void Load_SaveWithoutAPocket_StillWorks()
    {
        var lookup = new Lookup();
        var data = new SaveData { version = SaveMapper.CurrentVersion, scene = "SampleScene" };
        data.player.safePocket = null;

        LoadedGame loaded = SaveMapper.FromSaveData(data, lookup);

        Assert.IsEmpty(loaded.Player.Pocket);
    }

    // ----- Rarity and value -----

    [Test]
    public void Report_SumsValue_AndListsRarestFirst()
    {
        ItemDefinition coin = Item("coin", stack: 50, value: 10, rarity: ItemRarity.Uncommon);
        ItemDefinition idol = Item("idol", stack: 1, value: 150, rarity: ItemRarity.Epic);
        ItemDefinition bone = Item("bone", stack: 50, value: 2, rarity: ItemRarity.Common);
        var bag = new ItemContainer(4, 1);
        PlayerSnapshot before = PlayerSnapshot.Capture(bag, null, null);
        bag.Add(coin, 3);
        bag.Add(idol, 1);
        bag.Add(bone, 5);
        RaidSession session = Session();
        session.End(RaidOutcome.Extracted);

        RaidReport report = RaidReport.Create(session, before, PlayerSnapshot.Capture(bag, null, null));

        Assert.AreEqual(3 * 10 + 150 + 5 * 2, report.ValueGained);
        Assert.AreEqual(idol, report.ItemsGained[0].Item);
        Assert.AreEqual(coin, report.ItemsGained[1].Item);
        Assert.AreEqual(bone, report.ItemsGained[2].Item);
    }

    [Test]
    public void RarityStyles_ExistForEveryRarity()
    {
        foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
        {
            RarityStyle style = Rarity.StyleOf(rarity);
            Assert.AreEqual(rarity, style.Rarity);
            Assert.IsFalse(string.IsNullOrEmpty(style.Label));
        }
    }

    // ----- Helpers -----

    private ItemDefinition Item(string id, int stack, int value = 1, ItemRarity rarity = ItemRarity.Common)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.name = id;
        created.Add(item);
        var data = new SerializedObject(item);
        data.FindProperty("stackable").boolValue = stack > 1;
        data.FindProperty("maximumStack").intValue = stack;
        data.FindProperty("value").intValue = value;
        data.FindProperty("rarity").enumValueIndex = (int)rarity;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private RaidSession Session()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        created.Add(raid);
        RaidModifierDefinition[] none = new RaidModifierDefinition[0];
        return new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SampleScene");
    }
}
