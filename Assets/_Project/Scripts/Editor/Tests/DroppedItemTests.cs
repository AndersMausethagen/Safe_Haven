using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Dropping from the bag: the stack lies in the world, whole, until it is picked up again.</summary>
public sealed class DroppedItemTests
{
    private static ItemDefinition Find(System.Func<ItemDefinition, bool> wanted) => AssetDatabase.FindAssets("t:ItemDefinition")
        .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(wanted);

    private static ItemDefinition Gear => Find(i => i.HasDurability && i.MaximumStack == 1);
    private static ItemDefinition Material => Find(i => !i.HasDurability && i.MaximumStack >= 10 && i.Prefab == null && i.HeldModel == null);

    private static DroppedItem NewDrop(ItemStack stack)
    {
        var drop = Object.Instantiate(DroppedItemSettings.Current.Prefab).GetComponent<DroppedItem>();
        drop.Hold(stack);
        return drop;
    }

    [Test]
    public void ThePrefab_IsWiredAndCanBeRecreatedFromASave()
    {
        SaveableEntity prefab = DroppedItemSettings.Current.Prefab;
        Assert.IsNotNull(prefab);
        Assert.IsNotEmpty(prefab.PrefabId);
        Assert.AreEqual(prefab, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).FindPrefab(prefab.PrefabId));
        var interactable = prefab.GetComponent<Interactable>();
        Assert.AreEqual(InteractionKind.Use, interactable.Kind);
        Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "nothing should trip on it");
        var saved = new SerializedObject(interactable);
        Assert.IsFalse(saved.FindProperty("deactivateOnComplete").boolValue, "what doesn't fit has to stay there");
    }

    [Test]
    public void DroppingFromTheBag_PutsTheWholeStackOnTheGround()
    {
        var player = new GameObject("Dropper");
        ItemDefinition material = Material;
        DroppedItem drop = null;
        try
        {
            var bag = player.AddComponent<ItemInventory>();
            bag.Container.SetSlot(0, new ItemStack(material, 7));
            Assert.IsTrue(bag.Drop(bag.Container, 0));
            Assert.IsTrue(bag.Container.GetSlot(0).IsEmpty);
            drop = Object.FindAnyObjectByType<DroppedItem>();
            Assert.IsNotNull(drop, "it has to land somewhere");
            Assert.AreEqual(material, drop.Stack.Item);
            Assert.AreEqual(7, drop.Stack.Count);
            StringAssert.Contains(material.DisplayName, drop.InteractionName);
            StringAssert.Contains("7", drop.InteractionName);
        }
        finally
        {
            Object.DestroyImmediate(player);
            if (drop != null)
            {
                Object.DestroyImmediate(drop.gameObject);
            }
        }
    }

    [Test]
    public void ASavedDrop_KeepsWearAndEssenceWork()
    {
        EnchantmentDefinition enchantment = EssenceSettings.Current.Enchantments[0];
        DroppedItem drop = NewDrop(new ItemStack(Gear, 1, 0.4f, 2, enchantment));
        DroppedItem loaded = Object.Instantiate(DroppedItemSettings.Current.Prefab).GetComponent<DroppedItem>();
        try
        {
            loaded.RestoreState(drop.CaptureState());
            Assert.AreEqual(Gear, loaded.Stack.Item);
            Assert.AreEqual(0.4f, loaded.Stack.Durability, 0.001f);
            Assert.AreEqual(2, loaded.Stack.Empower);
            Assert.AreEqual(enchantment, loaded.Stack.Enchantment);
        }
        finally
        {
            Object.DestroyImmediate(drop.gameObject);
            Object.DestroyImmediate(loaded.gameObject);
        }
    }

    [Test]
    public void PickingUp_ReturnsTheSameStack()
    {
        EnchantmentDefinition enchantment = EssenceSettings.Current.Enchantments[0];
        DroppedItem drop = NewDrop(new ItemStack(Gear, 1, 0.4f, 2, enchantment));
        try
        {
            var bag = new ItemContainer(2, 1);
            Assert.AreEqual(1, drop.TakeInto(bag));
            Assert.IsTrue(drop.Stack.IsEmpty);
            ItemStack back = bag.GetSlot(0);
            Assert.AreEqual(0.4f, back.Durability, 0.001f);
            Assert.AreEqual(2, back.Empower);
            Assert.AreEqual(enchantment, back.Enchantment);
        }
        finally
        {
            Object.DestroyImmediate(drop.gameObject);
        }
    }

    [Test]
    public void WhatDoesntFit_StaysOnTheGround()
    {
        ItemDefinition material = Material;
        DroppedItem drop = NewDrop(new ItemStack(material, material.MaximumStack));
        try
        {
            var bag = new ItemContainer(1, 1);
            bag.SetSlot(0, new ItemStack(material, material.MaximumStack - 3));
            Assert.AreEqual(3, drop.TakeInto(bag));
            Assert.AreEqual(material.MaximumStack - 3, drop.Stack.Count);
            Assert.AreEqual(0, drop.TakeInto(bag));
            Assert.AreEqual(material.MaximumStack - 3, drop.Stack.Count, "a full bag loses nothing");
        }
        finally
        {
            Object.DestroyImmediate(drop.gameObject);
        }
    }

    [Test]
    public void PickingUp_IsNotANewFind()
    {
        int found = 0;
        void Count(GameEvent happened)
        {
            if (happened.Kind == GameEventKind.ItemObtained)
            {
                found++;
            }
        }

        DroppedItem drop = NewDrop(new ItemStack(Material, 5));
        GameEvents.Raised += Count;
        try
        {
            drop.TakeInto(new ItemContainer(2, 1));
            Assert.AreEqual(0, found, "dropping and picking up again would fill any collect quest");
        }
        finally
        {
            GameEvents.Raised -= Count;
            Object.DestroyImmediate(drop.gameObject);
        }
    }

    [Test]
    public void AnItemWithoutAModel_ShowsTheSack_AndOneWithAModelLiesOnTheGround()
    {
        DroppedItem plain = NewDrop(new ItemStack(Material, 1));
        DroppedItem spear = NewDrop(new ItemStack(Find(i => i.HeldModel != null && i.HeldModel.GetComponentsInChildren<MeshFilter>(true).Length > 0 && i.name == "IronSpear"), 1));
        spear.transform.SetPositionAndRotation(new Vector3(3f, 2f, 3f), Quaternion.identity);
        try
        {
            Assert.IsFalse(plain.ShowsOwnModel);
            Assert.IsTrue(plain.transform.Find("Visual/Sack").gameObject.activeSelf);
            Assert.IsTrue(spear.ShowsOwnModel);
            Assert.IsFalse(spear.transform.Find("Visual/Sack").gameObject.activeSelf);
            Renderer[] look = spear.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(look);
            Assert.IsEmpty(spear.GetComponentsInChildren<Light>(true));
            float height = look.Max(r => r.bounds.max.y) - look.Min(r => r.bounds.min.y);
            float length = look.Max(r => r.bounds.size.magnitude);
            Assert.Less(height, length * 0.5f, "a spear lies down");
        }
        finally
        {
            Object.DestroyImmediate(plain.gameObject);
            Object.DestroyImmediate(spear.gameObject);
        }
    }
}
