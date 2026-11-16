using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode tests for seeded raid generation: spawn groups fill some of their points from the raid's seed.</summary>
public sealed class RaidGenerationTests
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

    [Test]
    public void SeedFor_IsStableAndDiffersPerGroup()
    {
        Assert.AreEqual(RaidLayout.SeedFor(1234, "Raid Layout/Wolves"), RaidLayout.SeedFor(1234, "Raid Layout/Wolves"));
        Assert.AreNotEqual(RaidLayout.SeedFor(1234, "Raid Layout/Wolves"), RaidLayout.SeedFor(1234, "Raid Layout/Caches"));
        Assert.AreNotEqual(RaidLayout.SeedFor(1234, "Raid Layout/Wolves"), RaidLayout.SeedFor(4321, "Raid Layout/Wolves"));
        Assert.GreaterOrEqual(RaidLayout.SeedFor(-99, "x"), 0);
    }

    [Test]
    public void PickIndices_PicksDifferentPointsWithinRange()
    {
        var random = new System.Random(7);
        for (int run = 0; run < 200; run++)
        {
            List<int> picked = RaidLayout.PickIndices(random, 8, 5);

            Assert.AreEqual(5, picked.Count);
            Assert.AreEqual(5, new HashSet<int>(picked).Count, "A point is never used twice.");
            Assert.IsTrue(picked.TrueForAll(i => i >= 0 && i < 8));
        }

        Assert.AreEqual(3, RaidLayout.PickIndices(random, 3, 10).Count, "Never more than the points available.");
        Assert.AreEqual(0, RaidLayout.PickIndices(random, 0, 2).Count);
    }

    [Test]
    public void PickIndices_EveryPointGetsUsedOverManyRaids()
    {
        var random = new System.Random(11);
        var used = new HashSet<int>();
        for (int run = 0; run < 100; run++)
        {
            used.UnionWith(RaidLayout.PickIndices(random, 10, 3));
        }

        Assert.AreEqual(10, used.Count);
    }

    [Test]
    public void RollCount_StaysBetweenMinAndMaxAndThePointsAvailable()
    {
        var random = new System.Random(3);
        var seen = new HashSet<int>();
        for (int run = 0; run < 300; run++)
        {
            int count = RaidLayout.RollCount(random, 3, 5, 10);
            Assert.That(count, Is.InRange(3, 5));
            seen.Add(count);
        }

        Assert.AreEqual(3, seen.Count, "Every count from min to max comes up.");
        Assert.AreEqual(2, RaidLayout.RollCount(random, 3, 5, 2));
        Assert.AreEqual(4, RaidLayout.RollCount(random, 4, 4, 9));
        Assert.AreEqual(0, RaidLayout.RollCount(random, 1, 2, 0));
    }

    [Test]
    public void PickWeighted_FollowsTheWeightsAndSkipsZero()
    {
        var random = new System.Random(5);
        var weights = new List<float> { 3f, 0f, 1f };
        var hits = new int[3];
        for (int run = 0; run < 4000; run++)
        {
            hits[RaidLayout.PickWeighted(random, weights)]++;
        }

        Assert.AreEqual(0, hits[1]);
        Assert.AreEqual(0.75f, hits[0] / 4000f, 0.04f);
        Assert.AreEqual(-1, RaidLayout.PickWeighted(random, new List<float> { 0f, 0f }));
    }

    [Test]
    public void SpawnTable_RespectsThePerGroupCap()
    {
        var common = Track(new GameObject("Common"));
        var rare = Track(new GameObject("Rare"));
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[]
        {
            new SpawnTable.Entry { prefab = common, weight = 1f },
            new SpawnTable.Entry { prefab = rare, weight = 100f, maxPerGroup = 1 },
        });

        var counts = new Dictionary<GameObject, int>();
        var random = new System.Random(9);
        for (int i = 0; i < 20; i++)
        {
            GameObject pick = table.Pick(random, counts);
            counts.TryGetValue(pick, out int placed);
            counts[pick] = placed + 1;
        }

        Assert.AreEqual(1, counts[rare]);
        Assert.AreEqual(19, counts[common]);
    }

    [Test]
    public void SpawnTable_Roll_PlacesTheGuaranteedOnesFirst()
    {
        var common = Track(new GameObject("Common"));
        var quest = Track(new GameObject("Quest"));
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[]
        {
            new SpawnTable.Entry { prefab = common, weight = 1f },
            new SpawnTable.Entry { prefab = quest, weight = 0f, minPerGroup = 1 },
        });

        for (int seed = 0; seed < 50; seed++)
        {
            List<GameObject> picks = table.Roll(new System.Random(seed), 4);
            Assert.AreEqual(4, picks.Count);
            Assert.AreEqual(1, picks.FindAll(p => p == quest).Count, "The guaranteed entry is always there, and only once at weight 0.");
        }

        Assert.AreEqual(new[] { quest }, table.Roll(new System.Random(1), 1).ToArray(), "Guarantees come before weighted picks.");
    }

    [Test]
    public void SpawnGroup_SameSeedGivesTheSameLayout()
    {
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[]
        {
            new SpawnTable.Entry { prefab = Track(new GameObject("A")), weight = 1f },
            new SpawnTable.Entry { prefab = Track(new GameObject("B")), weight = 1f },
        });

        string first = Layout(table, 42);
        Assert.AreEqual(first, Layout(table, 42), "The same seed always gives the same layout.");

        var layouts = new HashSet<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            layouts.Add(Layout(table, seed));
        }

        Assert.Greater(layouts.Count, 5, "Different seeds give different layouts.");
    }

    [Test]
    public void RaidSession_KeepsItsSeed()
    {
        RaidDefinition raid = Track(ScriptableObject.CreateInstance<RaidDefinition>());
        var none = new RaidModifierDefinition[0];
        var session = new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven", 0, 777);

        Assert.AreEqual(777, session.Seed);
        Assert.GreaterOrEqual(new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven").Seed, 0);
    }

    [TestCase(0, 0)]
    [TestCase(4, 2)]
    public void SpawnGroup_SeparatesPacksAndHonorsZeroCount(int requested, int expected)
    {
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[] { new SpawnTable.Entry { prefab = Track(new GameObject("Pack")), weight = 1f } });
        var root = Track(new GameObject("Pack Sites"));
        for (int i = 0; i < 6; i++)
        {
            var point = new GameObject("Site " + i);
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = new Vector3(i < 3 ? i : 20f + i, 0f, 0f);
        }
        SpawnGroup group = root.AddComponent<SpawnGroup>();
        var data = new UnityEditor.SerializedObject(group);
        data.FindProperty("table").objectReferenceValue = table;
        data.FindProperty("count").vector2IntValue = new Vector2Int(requested, requested);
        data.FindProperty("scatterRadius").floatValue = 0f;
        data.FindProperty("minimumSpawnSeparation").floatValue = 10f;
        data.ApplyModifiedPropertiesWithoutUndo();

        IReadOnlyList<GameObject> spawned = group.Spawn(new System.Random(42));
        Assert.AreEqual(expected, spawned.Count);
        for (int i = 0; i < spawned.Count; i++)
        {
            for (int j = i + 1; j < spawned.Count; j++)
            {
                Assert.GreaterOrEqual(Vector3.Distance(spawned[i].transform.position, spawned[j].transform.position), 10f);
            }
        }
    }

    [Test]
    public void SpawnGroup_DensityScalesOnlyEnemyGroups()
    {
        var wolf = Track(new GameObject("Wolf"));
        wolf.AddComponent<MonsterAI>();
        SpawnTable enemies = Track(ScriptableObject.CreateInstance<SpawnTable>());
        enemies.SetEntries(new[] { new SpawnTable.Entry { prefab = wolf, weight = 1f } });
        SpawnTable crates = Track(ScriptableObject.CreateInstance<SpawnTable>());
        crates.SetEntries(new[] { new SpawnTable.Entry { prefab = Track(new GameObject("Crate")), weight = 1f } });
        var dense = new SpawnRules(2f, null, 60f, 110f);

        SpawnGroup enemyGroup = Group(enemies, 2, 2, Vector3.zero, 5f);
        SpawnGroup crateGroup = Group(crates, 2, 2, Vector3.zero, 5f);
        Assert.IsTrue(enemyGroup.SpawnsEnemies);
        Assert.IsFalse(crateGroup.SpawnsEnemies);
        Assert.AreEqual(new Vector2Int(4, 4), enemyGroup.CountFor(dense));
        Assert.AreEqual(new Vector2Int(2, 2), crateGroup.CountFor(dense));
        Assert.AreEqual(4, enemyGroup.Spawn(new System.Random(3), dense).Count);
        Assert.AreEqual(2, crateGroup.Spawn(new System.Random(3), dense).Count);
        Assert.AreEqual(1, new SpawnRules(0.5f, null, 60f, 110f).Scale(2));
        Assert.AreEqual(0, new SpawnRules(0f, null, 60f, 110f).Scale(3));
    }

    [TestCase(EntryBand.Near, 0f, 50f)]
    [TestCase(EntryBand.Middle, 60f, 110f)]
    [TestCase(EntryBand.Far, 110f, 1000f)]
    public void SpawnGroup_EntryBandOnlyFillsPointsAtThatDistance(EntryBand band, float min, float max)
    {
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[] { new SpawnTable.Entry { prefab = Track(new GameObject("Thing")), weight = 1f } });
        SpawnGroup group = Group(table, 20, 20, Vector3.zero, 10f);
        var data = new UnityEditor.SerializedObject(group);
        data.FindProperty("entryBand").enumValueIndex = (int)band;
        data.ApplyModifiedPropertiesWithoutUndo();

        var rules = new SpawnRules(1f, new Vector3(0f, 30f, 0f), 60f, 110f);
        IReadOnlyList<GameObject> spawned = group.Spawn(new System.Random(7), rules);
        Assert.Greater(spawned.Count, 0);
        foreach (GameObject thing in spawned)
        {
            float x = thing.transform.position.x;
            Assert.That(x >= min && x <= max, $"{band} placed something at {x} m.");
        }

        Assert.AreEqual(20, group.PointsFor(SpawnRules.Default).Count, "Without an entry, bands are ignored.");
    }

    [Test]
    public void SpawnGroup_DefaultRulesKeepTheOriginalLayout()
    {
        SpawnTable table = Track(ScriptableObject.CreateInstance<SpawnTable>());
        table.SetEntries(new[]
        {
            new SpawnTable.Entry { prefab = Track(new GameObject("A")), weight = 1f },
            new SpawnTable.Entry { prefab = Track(new GameObject("B")), weight = 2f },
        });
        SpawnGroup first = Group(table, 3, 6, Vector3.zero, 10f);
        SpawnGroup second = Group(table, 3, 6, Vector3.zero, 10f);

        var plain = new List<string>();
        foreach (GameObject spawned in first.Spawn(new System.Random(99)))
        {
            plain.Add($"{spawned.name}@{spawned.transform.position}");
        }

        var ruled = new List<string>();
        foreach (GameObject spawned in second.Spawn(new System.Random(99), new SpawnRules(1f, Vector3.zero, 60f, 110f)))
        {
            ruled.Add($"{spawned.name}@{spawned.transform.position}");
        }

        CollectionAssert.AreEqual(plain, ruled, "Density 1 and the Any band roll exactly as before.");
    }

    // A group of points in a row along x, spacing metres apart, filling exactly min to max.
    [Test]
    public void SpawnGroup_FriendOnTheHostsRunMakesNoCreaturesButRollsAlike()
    {
        // Co-op: the host's game makes the raid's creatures; a friend's game rolls the same layout and places the rest.
        var wolf = Track(new GameObject("Wolf"));
        wolf.AddComponent<MonsterAI>();
        var crate = Track(new GameObject("Crate"));
        SpawnTable mixed = Track(ScriptableObject.CreateInstance<SpawnTable>());
        mixed.SetEntries(new[]
        {
            new SpawnTable.Entry { prefab = wolf, weight = 1f },
            new SpawnTable.Entry { prefab = crate, weight = 1f },
        });
        SpawnGroup host = Group(mixed, 8, 8, Vector3.zero, 5f);
        SpawnGroup friend = Group(mixed, 8, 8, Vector3.zero, 5f);
        var hostRandom = new System.Random(9);
        var friendRandom = new System.Random(9);
        host.Spawn(hostRandom, SpawnRules.Default);
        CreatureSource.Remote = new CreaturesElsewhere();
        try
        {
            friend.Spawn(friendRandom, SpawnRules.Default);
        }
        finally
        {
            CreatureSource.Remote = null;
        }

        var hostCrates = new List<Vector3>();
        int wolves = 0;
        foreach (GameObject made in host.Spawned)
        {
            if (made.name == "Wolf") wolves++;
            else hostCrates.Add(made.transform.position);
        }

        var friendCrates = new List<Vector3>();
        foreach (GameObject made in friend.Spawned)
        {
            Assert.AreEqual("Crate", made.name, "no creature is made in the friend's game");
            friendCrates.Add(made.transform.position);
        }

        Assert.Greater(wolves, 0, "the host's game made some");
        CollectionAssert.AreEqual(hostCrates, friendCrates, "the rest lies where it does in the host's game");
        Assert.AreEqual(hostRandom.Next(), friendRandom.Next(), "both games rolled alike");
    }

    private sealed class CreaturesElsewhere : IRaidCreatureSource
    {
        public bool Wake(SpawnGroup group) => true;

        public void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt)
        {
        }
    }

    private SpawnGroup Group(SpawnTable table, int min, int max, Vector3 origin, float spacing)
    {
        var root = Track(new GameObject("Group"));
        for (int i = 0; i < 20; i++)
        {
            var point = new GameObject("Point " + i);
            point.transform.SetParent(root.transform, false);
            point.transform.position = origin + new Vector3(i * spacing, 0f, 0f);
        }

        SpawnGroup group = root.AddComponent<SpawnGroup>();
        var data = new UnityEditor.SerializedObject(group);
        data.FindProperty("table").objectReferenceValue = table;
        data.FindProperty("count").vector2IntValue = new Vector2Int(min, max);
        data.FindProperty("scatterRadius").floatValue = 0f;
        data.ApplyModifiedPropertiesWithoutUndo();
        return group;
    }

    // Builds a group of 8 points, fills 3 to 5 of them and describes which point got what.
    private string Layout(SpawnTable table, int seed)
    {
        var groupObject = Track(new GameObject("Group"));
        for (int i = 0; i < 8; i++)
        {
            new GameObject($"Point {i}").transform.SetParent(groupObject.transform, false);
            groupObject.transform.GetChild(i).localPosition = new Vector3(i * 10f, 0f, 0f);
        }

        var group = groupObject.AddComponent<SpawnGroup>();
        var serialized = new UnityEditor.SerializedObject(group);
        serialized.FindProperty("table").objectReferenceValue = table;
        serialized.FindProperty("count").vector2IntValue = new Vector2Int(3, 5);
        serialized.FindProperty("scatterRadius").floatValue = 0f;
        serialized.FindProperty("randomFacing").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var parts = new List<string>();
        foreach (GameObject spawned in group.Spawn(new System.Random(RaidLayout.SeedFor(seed, "Group"))))
        {
            parts.Add($"{spawned.name}@{spawned.transform.position.x:0}");
        }

        return string.Join(",", parts);
    }

    private T Track<T>(T asset) where T : Object
    {
        created.Add(asset);
        return asset;
    }

    private sealed class SomeoneElseMakesThem : IRaidCreatureSource
    {
        public bool Wake(SpawnGroup group) => true;
        public void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt) { }
    }

    [Test]
    public void AFollowersSkippedCreatures_AreMadeWhenTheHostLeaves_BeyondTheHandedBackOnes()
    {
        // The user, 2026-11-16: the host gone, the follower's map is filled again (it was empty but for what stood near them).
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/BlackWidow.prefab");
        Assert.IsNotNull(prefab);
        var root = new GameObject("Spawn Group");
        try
        {
            SpawnGroup group = root.AddComponent<SpawnGroup>();
            Transform near = new GameObject("Near").transform;
            near.SetParent(root.transform, false);
            Transform far = new GameObject("Far").transform;
            far.SetParent(root.transform, false);
            far.position = new Vector3(200f, 0f, 0f);

            CreatureSource.Remote = new SomeoneElseMakesThem();
            Assert.IsNull(group.PlaceAt(near, prefab, new System.Random(1)), "the host's game makes them");
            Assert.IsNull(group.PlaceAt(far, prefab, new System.Random(2)));
            CreatureSource.Remote = null;

            List<GameObject> made = group.PlaceSkipped(Vector3.zero, 90f);
            Assert.AreEqual(1, made.Count, "only the far one: the near one is the host's, handed back");
            Assert.Greater(made[0].transform.position.x, 150f);
            Assert.AreEqual(0, group.PlaceSkipped(Vector3.zero, 90f).Count, "made once");
        }
        finally
        {
            CreatureSource.Remote = null;
            Object.DestroyImmediate(root);
        }
    }
}
