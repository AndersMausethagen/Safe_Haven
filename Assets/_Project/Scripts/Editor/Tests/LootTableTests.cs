using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loot tables as assets (Data/Loot): one source of truth for what drops, so the journal and the prefab never disagree again (the
/// Blight Boar's bone); rows limited to raid tiers; trophies that a loot multiplier leaves alone; and every raid with its own
/// regional and elite tables.
/// </summary>
public sealed class LootTableTests
{
    private static IEnumerable<T> All<T>(string filter, params string[] folders) where T : Object =>
        AssetDatabase.FindAssets(filter, folders.Length > 0 ? folders : null)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);

    [Test]
    public void EveryMonster_AndItsPrefab_ShareOneTable()
    {
        foreach (MonsterDefinition monster in All<MonsterDefinition>("t:MonsterDefinition"))
        {
            Assert.IsNotNull(monster.LootTable, $"{monster.name} has a loot table");
            var prefab = new SerializedObject(monster).FindProperty("prefab").objectReferenceValue as GameObject;
            if (prefab == null)
            {
                continue;
            }

            foreach (LootOnDeath loot in prefab.GetComponentsInChildren<LootOnDeath>(true))
            {
                Assert.AreSame(monster.LootTable, loot.Table, $"{prefab.name} drops what {monster.name}'s journal page says");
            }
        }
    }

    [Test]
    public void EveryCreatureAndCache_InThePrefabs_HasATable()
    {
        foreach (GameObject prefab in All<GameObject>("t:Prefab", "Assets/_Project/Prefabs", "Assets/_Project/Data"))
        {
            foreach (LootOnDeath loot in prefab.GetComponentsInChildren<LootOnDeath>(true))
            {
                Assert.IsTrue(loot.Table != null || prefab.name == "ArmoredDummy", $"{prefab.name} drops from a Loot Table");
            }

            foreach (LootCache cache in prefab.GetComponentsInChildren<LootCache>(true))
            {
                Assert.IsNotNull(cache.Table, $"{prefab.name}'s cache holds a Loot Table");
            }
        }
    }

    [Test]
    public void TheBlightBoar_DropsTheBoneItsPageShows()
    {
        var boar = AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/BlightBoar.asset");
        Assert.IsTrue(boar.Drops.Entries.Any(entry => entry.Item != null && entry.Item.name == "Bone"));
    }

    [Test]
    public void EveryRaid_HasItsOwnRegionalAndEliteTables()
    {
        var regional = new HashSet<LootTable>();
        foreach (RaidDefinition raid in All<RaidDefinition>("t:RaidDefinition"))
        {
            Assert.IsNotNull(raid.RegionalLoot, $"{raid.name}: what only it holds");
            Assert.IsNotNull(raid.EliteLoot, $"{raid.name}: what its elites give");
            Assert.IsTrue(regional.Add(raid.RegionalLoot), $"{raid.name} shares its regional table with another raid");
            foreach (RaidDifficulty tier in raid.Difficulties)
            {
                Assert.IsTrue(raid.EliteLoot.Drops.Entries.Any(entry => entry.AppearsAt(tier.tier)), $"{raid.name} tier {tier.tier}: elites give something");
            }
        }
    }

    [Test]
    public void TierRows_DropOnlyAtTheirTiers_AndNeverOutsideRaids()
    {
        ItemDropEntry any = Row(0, 0);
        ItemDropEntry fromTwo = Row(2, 0);
        ItemDropEntry twoToThree = Row(2, 3);
        Assert.IsTrue(any.AppearsAt(0) && any.AppearsAt(1) && any.AppearsAt(5));
        Assert.IsFalse(fromTwo.AppearsAt(0), "a tier row never drops at home");
        Assert.IsFalse(fromTwo.AppearsAt(1));
        Assert.IsTrue(fromTwo.AppearsAt(2) && fromTwo.AppearsAt(5));
        Assert.IsFalse(twoToThree.AppearsAt(4));
        Assert.IsTrue(twoToThree.AppearsAt(3));
    }

    [Test]
    public void TheMultiplier_ScalesRows_ButNotFixedOnes()
    {
        ItemDropEntry scaled = Row(0, 0);
        ItemDropEntry trophy = Row(0, 0, fixedAmount: true);
        Assert.AreEqual(3, scaled.Scaled(2, 1.5f, 0f), "2 x 1.5 = 3");
        Assert.AreEqual(2, scaled.Scaled(1, 1.5f, 0.2f), "the half rounds up on a low roll");
        Assert.AreEqual(1, scaled.Scaled(1, 1.5f, 0.9f));
        Assert.AreEqual(1, trophy.Scaled(1, 2f, 0f), "a trophy stays one");
    }

    [Test]
    public void ATable_RollsOnlyTheRowsOfItsTier()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var other = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var table = new ItemDropTable();
            var so = new List<ItemDropEntry> { Row(0, 0, item: item), Row(3, 0, item: other) };
            typeof(ItemDropTable).GetField("entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(table, so);
            Assert.IsTrue(table.Roll(1, 1f).All(drop => drop.Item == item));
            Assert.IsTrue(table.Roll(3, 1f).Any(drop => drop.Item == other));
            Assert.IsTrue(table.Roll().All(drop => drop.Item == item), "outside a raid");
        }
        finally
        {
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(other);
        }
    }

    private static ItemDropEntry Row(int from, int to, bool fixedAmount = false, ItemDefinition item = null)
    {
        var entry = new ItemDropEntry();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(ItemDropEntry).GetField("fromTier", flags).SetValue(entry, from);
        typeof(ItemDropEntry).GetField("toTier", flags).SetValue(entry, to);
        typeof(ItemDropEntry).GetField("fixedAmount", flags).SetValue(entry, fixedAmount);
        typeof(ItemDropEntry).GetField("item", flags).SetValue(entry, item);
        return entry;
    }
}
