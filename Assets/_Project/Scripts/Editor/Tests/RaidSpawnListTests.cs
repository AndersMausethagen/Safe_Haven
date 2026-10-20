using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for raid spawn lists: % shares, planning over several groups' points, and enemy density.</summary>
public sealed class RaidSpawnListTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
    }

    [Test]
    public void SetShare_RescalesTheOthersProportionallyToTotal100()
    {
        List<SpawnTable.Entry> entries = Entries(50f, 30f, 20f);
        RaidSpawnList.SetShare(entries, 0, 80f);

        Assert.AreEqual(80f, entries[0].weight, 0.001f);
        Assert.AreEqual(12f, entries[1].weight, 0.001f);
        Assert.AreEqual(8f, entries[2].weight, 0.001f);
        Assert.AreEqual(100f, Total(entries), 0.001f);
    }

    [Test]
    public void SetShare_SplitsTheRestEvenlyWhenOthersAreZero_AndClamps()
    {
        List<SpawnTable.Entry> entries = Entries(100f, 0f, 0f);
        RaidSpawnList.SetShare(entries, 0, 40f);
        Assert.AreEqual(30f, entries[1].weight, 0.001f);
        Assert.AreEqual(30f, entries[2].weight, 0.001f);

        RaidSpawnList.SetShare(entries, 1, 150f);
        Assert.AreEqual(100f, entries[1].weight, 0.001f);
        Assert.AreEqual(100f, Total(entries), 0.001f);

        List<SpawnTable.Entry> single = Entries(3f);
        RaidSpawnList.SetShare(single, 0, 10f);
        Assert.AreEqual(100f, single[0].weight, 0.001f, "A lone entry is always 100%.");
    }

    [Test]
    public void Normalize_KeepsRatiosAndHandlesAllZero()
    {
        List<SpawnTable.Entry> entries = Entries(1f, 3f);
        RaidSpawnList.Normalize(entries);
        Assert.AreEqual(25f, entries[0].weight, 0.001f);
        Assert.AreEqual(75f, entries[1].weight, 0.001f);

        List<SpawnTable.Entry> zeros = Entries(0f, 0f, 0f, 0f);
        RaidSpawnList.Normalize(zeros);
        Assert.AreEqual(25f, zeros[3].weight, 0.001f);
    }

    [Test]
    public void Plan_IsDeterministicPerSeed_AndHonoursMinAndMax()
    {
        List<SpawnTable.Entry> entries = Entries(90f, 10f);
        entries[0].maxPerGroup = 3;
        entries[1].minPerGroup = 2;
        List<Transform> points = Points(30, 4f);

        List<(int point, GameObject prefab)> first = SpawnGroup.Plan(new System.Random(5), new Vector2Int(8, 8), points, 0f, entries);
        List<(int point, GameObject prefab)> again = SpawnGroup.Plan(new System.Random(5), new Vector2Int(8, 8), points, 0f, entries);
        CollectionAssert.AreEqual(first, again, "The same seed lays the list out the same way.");

        int a = 0;
        int b = 0;
        var used = new HashSet<int>();
        foreach ((int point, GameObject prefab) in first)
        {
            Assert.IsTrue(used.Add(point), "A point is filled once.");
            if (prefab == entries[0].prefab)
            {
                a++;
            }
            else
            {
                b++;
            }
        }

        Assert.LessOrEqual(a, 3, "Max caps an entry.");
        Assert.GreaterOrEqual(b, 2, "Min guarantees an entry.");
    }

    [Test]
    public void Plan_NeverFillsMorePointsThanThereAre()
    {
        List<(int point, GameObject prefab)> plan =
            SpawnGroup.Plan(new System.Random(1), new Vector2Int(10, 12), Points(4, 2f), 0f, Entries(100f));
        Assert.AreEqual(4, plan.Count);
    }

    [Test]
    public void CountFor_ScalesOnlyCreatureLists()
    {
        var wolf = Track(new GameObject("Wolf"));
        wolf.AddComponent<MonsterAI>();
        var dense = new SpawnRules(2f, null, 60f, 110f);
        var enemies = new RaidSpawnList { count = new Vector2Int(3, 5) };
        enemies.entries.Add(new SpawnTable.Entry { prefab = wolf, weight = 100f });
        var resources = new RaidSpawnList { count = new Vector2Int(3, 5), entries = Entries(100f) };

        Assert.IsTrue(enemies.SpawnsEnemies);
        Assert.AreEqual(new Vector2Int(6, 10), enemies.CountFor(dense));
        Assert.AreEqual(new Vector2Int(3, 5), resources.CountFor(dense));
    }

    private List<SpawnTable.Entry> Entries(params float[] weights)
    {
        var entries = new List<SpawnTable.Entry>();
        foreach (float weight in weights)
        {
            entries.Add(new SpawnTable.Entry { prefab = Track(new GameObject("Prefab " + entries.Count)), weight = weight });
        }

        return entries;
    }

    private List<Transform> Points(int count, float spacing)
    {
        var points = new List<Transform>();
        for (int i = 0; i < count; i++)
        {
            points.Add(Track(new GameObject("Point " + i)).transform);
            points[i].position = new Vector3(i * spacing, 0f, 0f);
        }

        return points;
    }

    private static float Total(List<SpawnTable.Entry> entries)
    {
        float total = 0f;
        foreach (SpawnTable.Entry entry in entries)
        {
            total += entry.weight;
        }

        return total;
    }

    private T Track<T>(T thing) where T : Object
    {
        created.Add(thing);
        return thing;
    }
}
