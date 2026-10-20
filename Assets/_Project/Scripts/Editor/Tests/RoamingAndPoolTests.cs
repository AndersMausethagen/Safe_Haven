using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class RoamingAndPoolTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        GameObject pool = GameObject.Find("Prefab Pool");
        if (pool != null)
        {
            Object.DestroyImmediate(pool);
        }
        foreach (Object value in created)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }
        created.Clear();
    }

    [Test]
    public void AliveCap_GrowsWithStagesAndAlarms_AndIsClamped()
    {
        RoamingSpawnSettings settings = RoamingSpawnSettings.Create(Table(), 2, 1, 2, 8, 12);
        Assert.AreEqual(2, settings.AliveCap(0, 0, 1f));
        Assert.AreEqual(6, settings.AliveCap(2, 1, 1f));
        Assert.AreEqual(8, settings.AliveCap(5, 3, 1f));
        Assert.AreEqual(3, settings.AliveCap(0, 0, 1.5f));
        Assert.AreEqual(0, settings.AliveCap(3, 2, 0f));
    }

    [Test]
    public void Enabled_NeedsATableAndLimits()
    {
        Assert.IsFalse(RoamingSpawnSettings.Create(null, 2, 1, 2, 8, 12).Enabled);
        Assert.IsFalse(RoamingSpawnSettings.Create(Table(), 2, 1, 2, 8, 0).Enabled);
        Assert.IsFalse(RoamingSpawnSettings.Create(Table(), 2, 1, 2, 0, 12).Enabled);
        Assert.IsTrue(RoamingSpawnSettings.Create(Table(), 2, 1, 2, 8, 12).Enabled);
    }

    [Test]
    public void Pool_ReusesReleasedCopies()
    {
        var prefab = new GameObject("Pooled Thing");
        created.Add(prefab);

        GameObject first = PrefabPool.Spawn(prefab, new Vector3(1f, 2f, 3f), Quaternion.identity);
        Assert.AreNotSame(prefab, first);
        Assert.IsTrue(first.activeSelf);
        Assert.AreEqual(new Vector3(1f, 2f, 3f), first.transform.position);

        PrefabPool.Release(first);
        Assert.IsFalse(first.activeSelf);
        Assert.AreEqual(1, PrefabPool.CountIdle(prefab));

        PrefabPool.Release(first);
        Assert.AreEqual(1, PrefabPool.CountIdle(prefab), "Releasing twice must not queue the copy twice.");

        GameObject second = PrefabPool.Spawn(prefab, Vector3.zero, Quaternion.identity);
        Assert.AreSame(first, second);
        Assert.AreEqual(0, PrefabPool.CountIdle(prefab));
    }

    private SpawnTable Table()
    {
        var table = ScriptableObject.CreateInstance<SpawnTable>();
        created.Add(table);
        return table;
    }
}
