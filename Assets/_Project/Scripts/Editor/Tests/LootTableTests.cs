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
        // Expeditions: a boss encounter's loot is its boss's (Brukam's Bridge), with no caches, elites or regional finds.
        foreach (RaidDefinition raid in All<RaidDefinition>("t:RaidDefinition").Where(each => !each.IsEncounter))
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

    [Test]
    public void NoCreature_DropsGreyStoneOrARuneBook()
    {
        // The user's call on the loot research (Q4): the troll-folk's Grey Stone and the four creatures' Rune Book go; each has a part of its own.
        foreach (MonsterDefinition monster in All<MonsterDefinition>("t:MonsterDefinition"))
        {
            foreach (ItemDropEntry entry in monster.Drops.Entries)
            {
                Assert.IsFalse(entry.Item != null && (entry.Item.name == "GreyStone" || entry.Item.name == "RuneBook"), $"{monster.name} drops {entry.DropName}");
            }
        }
    }

    [Test]
    public void EveryRaid_HasCacheKindsOfItsOwn()
    {
        var kindsByRaid = new Dictionary<RaidDefinition, HashSet<LootTable>>();
        foreach (RaidDefinition raid in All<RaidDefinition>("t:RaidDefinition").Where(each => !each.IsEncounter))
        {
            kindsByRaid[raid] = new HashSet<LootTable>(raid.SpawnLists
                .SelectMany(list => list.entries)
                .Where(entry => entry.prefab != null)
                .SelectMany(entry => entry.prefab.GetComponentsInChildren<LootCache>(true))
                .Select(cache => cache.Table)
                .Where(table => table != null));
            Assert.Greater(kindsByRaid[raid].Count, 2, $"{raid.name} places several kinds of cache");
        }

        foreach (KeyValuePair<RaidDefinition, HashSet<LootTable>> raid in kindsByRaid)
        {
            IEnumerable<LootTable> others = kindsByRaid.Where(other => other.Key != raid.Key).SelectMany(other => other.Value);
            Assert.IsTrue(raid.Value.Except(others).Any() || raid.Key.RegionalLoot.Drops.Entries.Count > 0, $"{raid.Key.name} holds something no other raid does");
            Assert.IsNotEmpty(raid.Key.RegionalLoot.Drops.Entries, $"{raid.Key.name}'s regional finds");
        }
    }

    [Test]
    public void HigherTiers_GiveMore()
    {
        foreach (RaidDefinition raid in All<RaidDefinition>("t:RaidDefinition"))
        {
            for (int i = 1; i < raid.Difficulties.Count; i++)
            {
                Assert.Greater(raid.Difficulties[i].loot, raid.Difficulties[i - 1].loot, $"{raid.name} tier {raid.Difficulties[i].tier} gives more than the tier below");
            }
        }
    }

    [Test]
    public void Trophies_AlwaysDrop_OneAtATime_AndOpenAGreatTier()
    {
        string[] trophies = { "MossTrollTusk", "StoneTrollKnuckle", "JutulHeartstone", "HeartwoodOfTheFirstGuardian" };
        var buildings = All<BuildingDefinition>("t:BuildingDefinition").ToList();
        foreach (string name in trophies)
        {
            var trophy = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Trophies/{name}.asset");
            Assert.IsNotNull(trophy, name);
            Assert.AreEqual(ItemCategory.Valuable, trophy.Category, $"{name}: no tier cap keeps a trophy back");
            ItemDropEntry row = All<LootTable>("t:LootTable", "Assets/_Project/Data/Loot/Creatures")
                .SelectMany(table => table.Drops.Entries).FirstOrDefault(entry => entry.Item == trophy);
            Assert.IsNotNull(row, $"{name} drops from its creature");
            Assert.AreEqual(1f, row.Probability, name);
            Assert.IsTrue(row.FixedAmount, $"{name}: one, at every tier");
            bool opens = buildings.Any(building => new SerializedObject(building).FindProperty("upgrades") is SerializedProperty upgrades
                && Enumerable.Range(0, upgrades.arraySize).Any(i => Uses(upgrades.GetArrayElementAtIndex(i).FindPropertyRelative("cost"), trophy)));
            Assert.IsTrue(opens, $"{name} opens a building tier");
        }
    }

    [Test]
    public void NewParts_AreRaidOnly_WhereTheyComeFromCreatures()
    {
        foreach (string name in new[] { "ShamansBurl", "HagsKnot", "BogTrollHide", "FiddleString", "WidowSilk", "MistwingFeathers", "NykksMane" })
        {
            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Parts/{name}.asset").RaidOnly, name);
        }

        foreach (string name in new[] { "WolfFang", "BogVenom", "WispEmber", "GraveIron", "RootlingBark", "BoarTusk" })
        {
            Assert.IsTrue(AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Materials/{name}.asset").RaidOnly, name);
        }

        Assert.IsFalse(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/RoughHide.asset").RaidOnly, "hides come from home's hunting too");
    }

    [Test]
    public void EveryNewDrop_HasAPurpose()
    {
        // "everything should have some purpose": a recipe or a building level uses it, or it breaks down into something. Brukam's tusk,
        // the Lindorm's scale and the Nykk's mane wait for their creatures (Troll Valley's boss, the Moss Marsh) and their uses with them.
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        string[] waiting = { "BrukamsTusk", "LindormScale", "NykksMane" };
        foreach (string folder in new[] { "Parts", "Trophies", "Salvage" })
        {
            foreach (ItemDefinition item in All<ItemDefinition>("t:ItemDefinition", $"Assets/_Project/Data/Items/{folder}"))
            {
                if (waiting.Contains(item.name))
                {
                    continue;
                }

                bool used = ItemUses.Of(item, book.Recipes, book.Buildings).Count > 0 || item.BreaksInto.Count > 0;
                Assert.IsTrue(used, $"{item.DisplayName} has no use");
            }
        }

        StringAssert.Contains("Hunter's Lodge tier II", ItemUses.Describe(ItemUses.Of(
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/WolfFang.asset"), book.Recipes, book.Buildings)));
    }

    [Test]
    public void ABarrowChest_GivesItsBarrowGoodsAndGraveIron_WithTheStartingSmithy()
    {
        // The 11-02 Smithy cap holds back gear only (review 2026-11-17, R6): a cache's finds and junk answer to the raid tier's drop cap.
        var chest = AssetDatabase.LoadAssetAtPath<LootTable>("Assets/_Project/Data/Loot/Caches/Barrow Chest.asset");
        int noSmithy = new SettlementState().SmithyTier;
        foreach (string wanted in new[] { "BarrowGoods", "GraveIron" })
        {
            ItemDefinition item = chest.Drops.Entries.Select(entry => entry.Item).FirstOrDefault(each => each != null && each.name == wanted);
            Assert.IsNotNull(item, $"the Barrow Chest holds {wanted}");
            Assert.Greater(item.Tier, 2, $"{wanted} is above the old cap, or this test proves nothing");
            Assert.IsFalse(LootCache.IsAboveThePlayer(item, noSmithy), $"{wanted} before the Smithy stands");
            Assert.IsFalse(LootCache.IsAboveThePlayer(item, 1), $"{wanted} with the first Smithy");
        }
    }

    [Test]
    public void ACache_StillHoldsBackGear_TheSmithyIsTiersFromMaking()
    {
        var made = new List<Object>();
        try
        {
            foreach (ItemCategory category in new[] { ItemCategory.Equipment, ItemCategory.Tool, ItemCategory.Material, ItemCategory.Valuable })
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>();
                made.Add(item);
                var so = new SerializedObject(item);
                so.FindProperty("category").intValue = (int)category;
                so.FindProperty("tier").intValue = 4;
                so.ApplyModifiedPropertiesWithoutUndo();
                bool gear = category == ItemCategory.Equipment || category == ItemCategory.Tool;
                Assert.AreEqual(gear, LootCache.IsAboveThePlayer(item, 1), $"tier 4 {category} with the first Smithy");
                Assert.IsFalse(LootCache.IsAboveThePlayer(item, 3), $"tier 4 {category}, a tier ahead of the Smithy");
            }
        }
        finally
        {
            made.ForEach(each => Object.DestroyImmediate(each));
        }
    }

    private static bool Uses(SerializedProperty cost, ItemDefinition item) =>
        Enumerable.Range(0, cost.arraySize).Any(i => cost.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == item);

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
