using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edit Mode tests for worn gear: slot rules, equipping, stat bonuses, saves and failed raids.</summary>
public sealed class GearTests
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

    // ----- Slot rules -----

    [Test]
    public void SlotRules_PutEachKindOfGearInItsOwnSlot()
    {
        ItemDefinition helmet = Armor("helmet", ArmorSlot.Head);
        ItemDefinition ring = Armor("ring", ArmorSlot.Finger);
        ItemDefinition pickaxe = Tool("pickaxe", ToolType.Pickaxe);

        Assert.IsTrue(GearSlots.Accepts(GearSlot.Head, helmet));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.Chest, helmet));
        Assert.IsTrue(GearSlots.Accepts(GearSlot.Finger1, ring));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.Finger2, ring), "one ring: the second finger is retired");
        Assert.IsFalse(GearSlots.Accepts(GearSlot.Legs, Armor("leggings", ArmorSlot.Legs)), "the body covers the legs");
        Assert.IsTrue(GearSlots.Accepts(GearSlot.PickaxeTool, pickaxe));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.AxeTool, pickaxe));
        Assert.IsFalse(GearSlots.Accepts(GearSlot.Head, Create<ItemDefinition>("berry")));
    }

    [Test]
    public void Gear_RefusesItemsDroppedOnTheWrongSlot()
    {
        var gear = new GearSet();
        var bag = new ItemContainer(4, 4);
        bag.SetSlot(0, new ItemStack(Armor("helmet", ArmorSlot.Head), 1));

        Assert.IsFalse(bag.MoveTo(0, gear.Slots, (int)GearSlot.Feet));
        Assert.IsTrue(gear.Get(GearSlot.Feet).IsEmpty);
        Assert.IsFalse(bag.GetSlot(0).IsEmpty);
    }

    // ----- Equipping -----

    [Test]
    public void Equip_MovesTheItemOutOfTheBagAndSwapsWithWhatWasWorn()
    {
        ItemDefinition oldCap = Armor("old_cap", ArmorSlot.Head);
        ItemDefinition newCap = Armor("new_cap", ArmorSlot.Head);
        var gear = new GearSet();
        var bag = new ItemContainer(4, 4);
        bag.SetSlot(0, new ItemStack(oldCap, 1));
        bag.SetSlot(1, new ItemStack(newCap, 1));

        Assert.IsTrue(gear.Equip(bag, 0, out _));
        Assert.AreEqual(oldCap, gear.Get(GearSlot.Head).Item);
        Assert.IsTrue(bag.GetSlot(0).IsEmpty);

        Assert.IsTrue(gear.Equip(bag, 1, out _));
        Assert.AreEqual(newCap, gear.Get(GearSlot.Head).Item);
        Assert.AreEqual(1, bag.Count(oldCap), "The old cap goes back into the bag.");
    }

    [Test]
    public void Equip_RefusesBrokenGearAndThingsThatArentGear()
    {
        var gear = new GearSet();
        var bag = new ItemContainer(4, 4);
        bag.SetSlot(0, new ItemStack(Armor("cap", ArmorSlot.Head), 1, 0f));
        bag.SetSlot(1, new ItemStack(Create<ItemDefinition>("berry"), 3));

        Assert.IsFalse(gear.Equip(bag, 0, out string broken));
        Assert.IsNotEmpty(broken);
        Assert.IsFalse(gear.Equip(bag, 1, out string notGear));
        Assert.IsNotEmpty(notGear);
        Assert.IsTrue(gear.Get(GearSlot.Head).IsEmpty);
    }

    [Test]
    public void Equip_ASecondRingSwapsWithTheFirst()
    {
        ItemDefinition ring = Armor("ring", ArmorSlot.Finger);
        ItemDefinition other = Armor("other ring", ArmorSlot.Finger);
        var gear = new GearSet();
        var bag = new ItemContainer(4, 4);
        bag.SetSlot(0, new ItemStack(ring, 1));
        bag.SetSlot(1, new ItemStack(other, 1));

        Assert.IsTrue(gear.Equip(bag, 0, out _));
        Assert.IsTrue(gear.Equip(bag, 1, out _));

        Assert.AreEqual(other, gear.Get(GearSlot.Finger1).Item);
        Assert.IsTrue(gear.Get(GearSlot.Finger2).IsEmpty);
        Assert.AreEqual(1, bag.Count(ring), "the first ring went back to the bag");
    }

    [Test]
    public void SevenArmorSlotsAreInUse()
    {
        int worn = 0;
        foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
        {
            bool armor = slot < GearSlot.MainHand;
            if (armor && !GearSlots.IsRetired(slot))
            {
                worn++;
            }
        }

        Assert.AreEqual(7, worn);
    }

    [Test]
    public void Unequip_PutsTheItemBackInTheBag_OrRefusesWhenItIsFull()
    {
        ItemDefinition cap = Armor("cap", ArmorSlot.Head);
        var gear = new GearSet();
        gear.Slots.SetSlot((int)GearSlot.Head, new ItemStack(cap, 1));
        var full = new ItemContainer(1, 1);
        full.SetSlot(0, new ItemStack(Create<ItemDefinition>("rock"), 1));

        Assert.IsFalse(gear.Unequip(GearSlot.Head, full, out string reason));
        Assert.IsNotEmpty(reason);
        Assert.AreEqual(cap, gear.Get(GearSlot.Head).Item);

        var bag = new ItemContainer(2, 2);
        Assert.IsTrue(gear.Unequip(GearSlot.Head, bag, out _));
        Assert.IsTrue(gear.Get(GearSlot.Head).IsEmpty);
        Assert.AreEqual(1, bag.Count(cap));
    }

    [Test]
    public void AutoEquip_FillsOnlyEmptySlots()
    {
        ItemDefinition worn = Armor("worn", ArmorSlot.Head);
        ItemDefinition spare = Armor("spare", ArmorSlot.Head);
        ItemDefinition boots = Armor("boots", ArmorSlot.Feet);
        var gear = new GearSet();
        gear.Slots.SetSlot((int)GearSlot.Head, new ItemStack(worn, 1));
        var bag = new ItemContainer(4, 4);
        bag.SetSlot(0, new ItemStack(spare, 1));
        bag.SetSlot(1, new ItemStack(boots, 1));

        Assert.AreEqual(1, gear.AutoEquipFrom(bag));

        Assert.AreEqual(worn, gear.Get(GearSlot.Head).Item);
        Assert.AreEqual(boots, gear.Get(GearSlot.Feet).Item);
        Assert.AreEqual(1, bag.Count(spare));
    }

    [Test]
    public void ToolTier_ComesFromTheToolBelt()
    {
        var gear = new GearSet();
        gear.Slots.SetSlot((int)GearSlot.PickaxeTool, new ItemStack(Tool("pickaxe", ToolType.Pickaxe, 2), 1));

        Assert.AreEqual(2, gear.ToolTier(ToolType.Pickaxe));
        Assert.AreEqual(0, gear.ToolTier(ToolType.Axe));
    }

    // ----- Stats -----

    [Test]
    public void Stats_ApplyWhileWorn_AndGoAwayWhenRemovedOrBroken()
    {
        ItemDefinition jerkin = Armor("jerkin", ArmorSlot.Chest, new StatBonus(StatType.Armor, 10f), new StatBonus(StatType.Strength, 2f));
        var stats = new StatSheet();
        var gear = new GearSet();
        gear.Bind(stats);

        gear.Slots.SetSlot((int)GearSlot.Chest, new ItemStack(jerkin, 1));
        Assert.AreEqual(10f, stats.Get(StatType.Armor), 0.001f);
        Assert.AreEqual(2f, stats.Get(StatType.Strength), 0.001f);

        gear.Slots.SetSlot((int)GearSlot.Chest, new ItemStack(jerkin, 1, 0f));
        Assert.AreEqual(0f, stats.Get(StatType.Armor), 0.001f, "Broken gear gives nothing.");

        gear.Slots.SetSlot((int)GearSlot.Chest, new ItemStack(jerkin, 1));
        gear.Slots.SetSlot((int)GearSlot.Chest, ItemStack.Empty);
        Assert.AreEqual(0f, stats.Get(StatType.Armor), 0.001f);
        Assert.AreEqual(0f, stats.Get(StatType.Strength), 0.001f);
    }

    [Test]
    public void Stats_UnbindingRemovesEveryBonus()
    {
        var stats = new StatSheet();
        var gear = new GearSet();
        gear.Slots.SetSlot((int)GearSlot.Head, new ItemStack(Armor("cap", ArmorSlot.Head, new StatBonus(StatType.MaxHealth, 5f)), 1));

        gear.Bind(stats);
        Assert.AreEqual(5f, stats.Get(StatType.MaxHealth), 0.001f, "Binding applies gear that was already worn.");

        gear.Bind(null);
        Assert.AreEqual(0f, stats.Get(StatType.MaxHealth), 0.001f);
    }

    [Test]
    public void Strength_AddsPhysicalPowerOnly()
    {
        var rules = Create<CombatRules>("rules");
        var stats = new StatSheet();
        stats.SetBase(StatType.PhysicalPower, 5f);
        stats.SetBase(StatType.SpellPower, 3f);
        stats.SetBase(StatType.Strength, 4f);

        Assert.AreEqual(5f + 4f * rules.PowerPerStrength, rules.PowerFor(stats, DamageType.Physical), 0.001f);
        Assert.AreEqual(3f, rules.PowerFor(stats, DamageType.Fire), 0.001f);
    }

    [Test]
    public void DamageBonus_ScalesEveryHit()
    {
        var rules = Create<CombatRules>("rules");
        var attack = Create<AttackDefinition>("strike");
        var stats = new StatSheet();
        float plain = DamageCalculator.CalculateOutgoing(stats, rules, attack, 10f, TargetCategory.Creature, 1f, out _);

        stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, 0.5f, this));
        float boosted = DamageCalculator.CalculateOutgoing(stats, rules, attack, 10f, TargetCategory.Creature, 1f, out _);

        Assert.AreEqual(plain * 1.5f, boosted, 0.001f);
    }

    // ----- Snapshots and saves -----

    [Test]
    public void Save_RoundTripsWornGear()
    {
        var lookup = new Lookup();
        ItemDefinition cap = Armor("cap", ArmorSlot.Head);
        ItemDefinition pickaxe = Tool("pickaxe", ToolType.Pickaxe);
        ItemDefinition torch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HandTorch.asset");
        lookup.Items[cap.Id] = cap;
        lookup.Items[pickaxe.Id] = pickaxe;
        lookup.Items[torch.Id] = torch;
        var gear = new ItemContainer(GearSlots.Count, 1);
        gear.SetSlot((int)GearSlot.Head, new ItemStack(cap, 1, 0.25f));
        gear.SetSlot((int)GearSlot.PickaxeTool, new ItemStack(pickaxe, 1));
        gear.SetSlot((int)GearSlot.OffHand, new ItemStack(torch, 1, 0.6f));
        PlayerSnapshot player = PlayerSnapshot.Capture(new ItemContainer(4, 4), null, null, null, null, PlayerSnapshot.Condition.None, gear);

        SaveData data = SaveMapper.ToSaveData(player, null, "SafeHaven", null, DateTime.UtcNow);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);

        Assert.IsTrue(loaded.Player.HasGear);
        Assert.AreEqual(3, loaded.Player.Gear.Count);
        PlayerSnapshot.SlotRecord head = Find(loaded.Player.Gear, (int)GearSlot.Head);
        Assert.AreEqual(cap, head.Stack.Item);
        Assert.AreEqual(0.25f, head.Stack.Durability, 0.001f);
        Assert.AreEqual(pickaxe, Find(loaded.Player.Gear, (int)GearSlot.PickaxeTool).Stack.Item);
        Assert.AreEqual(torch, Find(loaded.Player.Gear, (int)GearSlot.OffHand).Stack.Item);
        Assert.AreEqual(0.6f, Find(loaded.Player.Gear, (int)GearSlot.OffHand).Stack.Durability, 0.001f);
    }

    [Test]
    public void Save_FromBeforeGearSlots_LoadsWithoutGear()
    {
        var data = new SaveData { version = SaveMapper.CurrentVersion, scene = "SafeHaven" };
        data.player.equippedSlot = 2;

        LoadedGame loaded = SaveMapper.FromSaveData(data, new Lookup());

        Assert.IsFalse(loaded.Player.HasGear, "Old saves have no gear, so the player equips it from the bag on load.");
        Assert.AreEqual(2, loaded.Player.State.EquippedSlot);
    }

    [Test]
    public void FailedRaid_KeepsGearCarriedIn_WornDown_AndLosesGearLootedInTheRaid()
    {
        ItemDefinition cap = Armor("cap", ArmorSlot.Head);
        ItemDefinition lootedBoots = Armor("boots", ArmorSlot.Feet);
        var bag = new ItemContainer(4, 4);
        var gear = new ItemContainer(GearSlots.Count, 1);
        gear.SetSlot((int)GearSlot.Head, new ItemStack(cap, 1));
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, null, null, null, null, PlayerSnapshot.Condition.None, gear);

        gear.SetSlot((int)GearSlot.Feet, new ItemStack(lootedBoots, 1));
        PlayerSnapshot atDeath = PlayerSnapshot.Capture(bag, null, null, null, null, PlayerSnapshot.Condition.None, gear);

        PlayerSnapshot kept = atDeath.AfterFailedRaid(loadout, 0.25f);

        Assert.AreEqual(1, kept.Gear.Count);
        Assert.AreEqual(cap, kept.Gear[0].Stack.Item);
        Assert.AreEqual(0.75f, kept.Gear[0].Stack.Durability, 0.001f);
    }

    // ----- Helpers -----

    private sealed class Lookup : IContentLookup
    {
        public readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
        public ItemDefinition FindItem(string id) => id != null && Items.TryGetValue(id, out ItemDefinition item) ? item : null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }

    private static PlayerSnapshot.SlotRecord Find(IReadOnlyList<PlayerSnapshot.SlotRecord> records, int index)
    {
        foreach (PlayerSnapshot.SlotRecord record in records)
        {
            if (record.Index == index)
            {
                return record;
            }
        }

        Assert.Fail($"No record for slot {index}.");
        return default;
    }

    private ItemDefinition Armor(string name, ArmorSlot slot, params StatBonus[] bonuses)
    {
        ItemDefinition item = Create<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("armorSlot").intValue = (int)slot;
        SerializedProperty list = data.FindProperty("statBonuses");
        list.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            element.FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            element.FindPropertyRelative("value").floatValue = bonuses[i].value;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private ItemDefinition Tool(string name, ToolType type, int tier = 1)
    {
        ItemDefinition item = Create<ItemDefinition>(name);
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)type;
        data.FindProperty("toolTier").intValue = tier;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    // Definitions without an explicit id use their asset name as id, so naming the instance sets the id.
    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }

    [Test]
    public void TorchKey_SwapsATwoHandedWeaponAndTheTorch()
    {
        var torch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HandTorch.asset");
        var bow = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeWoodenBow.asset");
        var user = new GameObject("Torch user");
        try
        {
            var equipment = user.AddComponent<PlayerEquipment>();
            ItemContainer bag = user.GetComponent<ItemInventory>().Container;
            bag.Add(torch, 1);
            bag.Add(bow, 1);
            Assert.IsTrue(equipment.EquipFromBag(torch));
            Assert.AreSame(torch, equipment.OffHand.Item);

            Assert.IsTrue(equipment.EquipFromBag(bow), "a handed-over bow puts the torch away");
            Assert.AreSame(bow, equipment.MainHand.Item);
            Assert.IsTrue(equipment.OffHand.IsEmpty);

            Assert.IsTrue(equipment.ToggleTorch());
            Assert.IsTrue(equipment.MainHand.IsEmpty, "the bow goes to the bag for the torch");
            Assert.AreSame(torch, equipment.OffHand.Item);

            Assert.IsTrue(equipment.ToggleTorch());
            Assert.AreSame(bow, equipment.MainHand.Item, "and comes back when the torch is put away");
            Assert.IsTrue(equipment.OffHand.IsEmpty);
            Assert.AreEqual(1, bag.Count(torch));
        }
        finally
        {
            Object.DestroyImmediate(user);
        }
    }
}
