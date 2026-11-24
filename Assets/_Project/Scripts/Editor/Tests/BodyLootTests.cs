using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loot on the body (BodyLootSettings, the user 2026-11-24): a creature's roll waits in LootableRemains, is taken a stack at a time or
/// all at once, keeps what didn't fit, and goes when it is empty.
/// </summary>
public sealed class BodyLootTests
{
    private readonly List<GameObject> made = new List<GameObject>();

    [TearDown]
    public void Clean()
    {
        foreach (GameObject thing in made)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        foreach (LootableRemains left in Object.FindObjectsByType<LootableRemains>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(left.gameObject);
        }

        made.Clear();
    }

    private GameObject Made(string name)
    {
        var thing = new GameObject(name);
        made.Add(thing);
        return thing;
    }

    private static ItemDefinition Hide => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/RoughHide.asset");

    [Test]
    public void TheBodysItems_AreTakenAStackAtATime_AndTheBodyGoesWhenEmpty()
    {
        Assert.IsNotNull(Hide);
        Damageable wolf = Made("A test's wolf").AddComponent<Damageable>();
        GameObject looter = Made("A test's looter");
        var bag = looter.AddComponent<ItemInventory>();

        LootableRemains remains = LootableRemains.Leave(wolf, "Wolf", new List<ItemAmount> { new ItemAmount(Hide, 3) },
            new List<ResourceAmount>(), false, 0, null);
        Assert.IsNotNull(remains);
        Assert.AreEqual("Wolf", remains.Title);
        Assert.AreEqual(3, remains.Items.Count(Hide), "the roll waits on the body");
        Assert.AreEqual(0, bag.Container.Count(Hide), "not in the bag");

        int slot = -1;
        for (int i = 0; i < remains.Items.SlotCount && slot < 0; i++)
        {
            slot = remains.Items.GetSlot(i).IsEmpty ? -1 : i;
        }

        Assert.IsTrue(remains.Take(slot, looter));
        Assert.AreEqual(3, bag.Container.Count(Hide));
        Assert.IsTrue(remains == null, "emptied, the body's loot is gone");
    }

    [Test]
    public void WhatDoesntFit_StaysOnTheBody()
    {
        ResourceDefinition essence = SettlementSettings.Current.CursedEssence;
        Assert.IsNotNull(essence);
        Damageable wolf = Made("A test's wolf").AddComponent<Damageable>();
        GameObject looter = Made("A test's looter, with no essence pouch");
        var bag = looter.AddComponent<ItemInventory>();

        LootableRemains remains = LootableRemains.Leave(wolf, "Wolf", new List<ItemAmount> { new ItemAmount(Hide, 2) },
            new List<ResourceAmount> { new ResourceAmount(essence, 5) }, false, 0, null);
        Assert.AreEqual(1, remains.TakeAll(looter), "the essence had nowhere to go");
        Assert.AreEqual(2, bag.Container.Count(Hide), "the hides went");
        Assert.IsTrue(remains != null && !remains.IsEmpty);
        Assert.AreEqual(5, remains.Resources[0].Amount, "the essence waits, whole");
    }

    [Test]
    public void AnElitesOwnRoll_JoinsItsCreaturesBody_UnderItsTitle()
    {
        ResourceDefinition essence = SettlementSettings.Current.CursedEssence;
        Damageable wolf = Made("A test's wolf").AddComponent<Damageable>();
        LootableRemains first = LootableRemains.Leave(wolf, "Wolf", new List<ItemAmount> { new ItemAmount(Hide, 2) },
            new List<ResourceAmount> { new ResourceAmount(essence, 3) }, false, 0, null);
        LootableRemains again = LootableRemains.Leave(wolf, "Frenzied Wolf", new List<ItemAmount> { new ItemAmount(Hide, 5) },
            new List<ResourceAmount> { new ResourceAmount(essence, 4) }, false, 0, null);
        again.Rename("Frenzied Wolf");

        Assert.AreSame(first, again, "one body, one loot");
        Assert.AreEqual(7, again.Items.Count(Hide));
        Assert.AreEqual(1, again.Resources.Count, "the same essence in one chip");
        Assert.AreEqual(7, again.Resources[0].Amount);
        Assert.AreEqual("Frenzied Wolf", again.Title);
    }

    [Test]
    public void ARollPastItsStackSize_KeepsEveryPiece()
    {
        Damageable wolf = Made("A test's wolf").AddComponent<Damageable>();
        int many = Hide.MaximumStack * 7 + 3;
        LootableRemains remains = LootableRemains.Leave(wolf, "Wolf", new List<ItemAmount> { new ItemAmount(Hide, many) },
            new List<ResourceAmount>(), false, 0, null);
        Assert.AreEqual(many, remains.Items.Count(Hide), "eight stacks, none dropped");
    }

    [Test]
    public void TheCorpseLimit_ClearsTheOldest_NeverOneWithLoot_NorTheNewest()
    {
        // Oldest first; true holds loot.
        CollectionAssert.AreEqual(new[] { 0, 2 }, DeathRagdoll.ToClear(new[] { false, true, false, false, false }, 3));
        CollectionAssert.AreEqual(new int[0], DeathRagdoll.ToClear(new[] { false, false }, 3), "under the limit");
        CollectionAssert.AreEqual(new int[0], DeathRagdoll.ToClear(new[] { true, true, true, false }, 2),
            "the newest among bodies with loot: none cleared, not even it");
        CollectionAssert.AreEqual(new[] { 1 }, DeathRagdoll.ToClear(new[] { true, false, true, false }, 3));
    }

    [Test]
    public void ACreatureWithNothingOnIt_LeavesNothingToLoot()
    {
        Damageable wolf = Made("A test's wolf").AddComponent<Damageable>();
        Assert.IsNull(LootableRemains.Leave(wolf, "Wolf", new List<ItemAmount>(), new List<ResourceAmount>(), false, 0, null));
    }
}
