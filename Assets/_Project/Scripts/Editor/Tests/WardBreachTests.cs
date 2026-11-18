using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Ward breaches after the first: their pacing, their cost and what they can never take away.</summary>
public sealed class WardBreachTests
{
    [Test]
    public void TheBreaches_ComeInOrder_AndTheLastComesBackStronger()
    {
        WardBreachSettings settings = WardBreachSettings.Current;
        Assert.GreaterOrEqual(settings.Breaches.Count, 2);
        int last = settings.Breaches.Count - 1;
        Assert.AreEqual(settings.Breaches[0], settings.For(0));
        Assert.IsFalse(settings.For(0).ThreatensHome, "the first stays scripted and forgiving (GDD §6.6)");
        Assert.AreEqual(settings.Breaches[last], settings.For(last + 5));
        Assert.AreEqual(1f, settings.Strength(last), 0.001f);
        Assert.Greater(settings.Strength(last + 1), settings.Strength(last));
    }

    [Test]
    public void EveryLaterBreach_HasWavesLanesAndAWayToLose()
    {
        Assert.IsNotNull(WardBreachSettings.Current.WardAnchor);
        var health = new SerializedObject(WardBreachSettings.Current.WardAnchor);
        Assert.IsFalse(health.FindProperty("destroyOnDeath").boolValue);
        foreach (WardBreachDefinition breach in WardBreachSettings.Current.Breaches.Skip(1))
        {
            Assert.IsTrue(breach.ThreatensHome, breach.name);
            Assert.Greater(breach.LaneCount, 1, breach.name);
            Assert.Greater(breach.WaveCount, 1, breach.name);
            Assert.IsNotEmpty(breach.Reward, breach.name);
            for (int wave = 0; wave < breach.WaveCount; wave++)
            {
                Assert.IsNotEmpty(breach.Roster(wave), $"{breach.name} wave {wave}");
                Assert.IsTrue(breach.Roster(wave).All(enemy => enemy != null && enemy.GetComponent<MonsterAI>() != null), $"{breach.name} wave {wave}");
            }
        }
    }

    [Test]
    public void AGentleBreach_HasOneWaveOfItsOneEnemy()
    {
        WardBreachDefinition first = WardBreachSettings.Current.For(0);
        Assert.AreEqual(1, first.WaveCount);
        Assert.AreEqual(first.EnemyCount, first.Roster(0).Count);
        Assert.IsTrue(first.Roster(0).All(enemy => enemy == first.EnemyPrefab));
    }

    [Test]
    public void WinningABreach_SetsTheNextOneRaidsAway()
    {
        var settlement = new SettlementState();
        settlement.RecordExtraction();
        settlement.WarnOfBreach();
        Assert.AreEqual(1, settlement.BreachPendingSince);
        settlement.WinBreach(3);
        Assert.AreEqual(BreachStatus.Won, settlement.Breach);
        Assert.AreEqual(1, settlement.BreachesWon);
        Assert.AreEqual(4, settlement.NextBreachAt);
    }

    [Test]
    public void LosingABreach_StrainsTheWardForAWhile_AndLeavesItToTryAgain()
    {
        var settlement = new SettlementState();
        settlement.WarnOfBreach();
        settlement.LoseBreach(2);
        Assert.AreEqual(BreachStatus.Pending, settlement.Breach);
        Assert.AreEqual(0, settlement.BreachesWon);
        Assert.AreEqual(2, settlement.WardStrain);
        settlement.RecordExtraction();
        Assert.AreEqual(1, settlement.WardStrain);
        settlement.RecordExtraction();
        settlement.RecordExtraction();
        Assert.AreEqual(0, settlement.WardStrain, "no downward spiral: it mends by itself (GDD §6.9)");
    }

    [Test]
    public void ASaveFromBeforeThereWereSeveral_CountsTheFirstBreachAsWon()
    {
        var settlement = new SettlementState();
        settlement.Restore(true, 0, 5, BreachStatus.Won, 0, 0f);
        settlement.RestoreBreaches(0, 8, 0, 0);
        Assert.AreEqual(1, settlement.BreachesWon);
        Assert.AreEqual(8, settlement.NextBreachAt);
    }

    [Test]
    public void Repairs_CostAShareOfTheDamage_AndNeverNothing()
    {
        Assert.AreEqual(6, BuildingRepairs.Share(20, 1f, 0.3f));
        Assert.AreEqual(3, BuildingRepairs.Share(20, 0.5f, 0.3f));
        Assert.AreEqual(1, BuildingRepairs.Share(20, 0.01f, 0.3f), "any damage costs something");
        Assert.AreEqual(0, BuildingRepairs.Share(0, 1f, 0.3f));
        Assert.AreEqual(0, BuildingRepairs.Share(20, 0f, 0.3f));
    }

    [TestCase(0f, 10f, "north")]
    [TestCase(10f, 0f, "east")]
    [TestCase(0f, -10f, "south")]
    [TestCase(-10f, 0f, "west")]
    [TestCase(10f, 10f, "north-east")]
    [TestCase(-10f, -10f, "south-west")]
    public void Lanes_AreNamedByTheCompass(float x, float z, string expected)
    {
        Assert.AreEqual(expected, BreachLanes.Compass(Vector3.zero, new Vector3(x, 0f, z)));
    }

    [Test]
    public void Lanes_ReadAsOnePhrase_WithoutRepeating()
    {
        BreachLane Lane(string direction) => new BreachLane(Vector3.zero, new Vector3[0], direction);
        Assert.AreEqual("", BreachLanes.Describe(new BreachLane[0]));
        Assert.AreEqual("the north", BreachLanes.Describe(new[] { Lane("north") }));
        Assert.AreEqual("the north and the east", BreachLanes.Describe(new[] { Lane("north"), Lane("east"), Lane("north") }));
        Assert.AreEqual("the north, the east and the south", BreachLanes.Describe(new[] { Lane("north"), Lane("east"), Lane("south") }));
    }

    [Test]
    public void ABuilding_BreaksButIsNeverLost()
    {
        var palisade = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Palisade.prefab");
        GameObject wall = Object.Instantiate(palisade);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        BuildingObject building = wall.GetComponent<BuildingObject>();
        Assert.IsNotNull(building, "the palisade is a building");
        try
        {
            var health = wall.GetComponent<Damageable>();
            typeof(Damageable).GetMethod("Awake", flags)?.Invoke(health, null);
            health.Heal(health.MaxHealth);
            // The floor comes from the building itself as it is enabled in a scene (BuildingObject), as in play: not set here.
            typeof(BuildingObject).GetMethod("OnEnable", flags).Invoke(building, null);
            health.ApplyDamage(new DamageInfo(null, null, 1000000f, DamageType.Physical, false, Vector3.zero, Vector3.up));
            Assert.IsTrue(health.IsAlive);
            Assert.IsTrue(health.IsBroken);
            Assert.AreEqual(1f, health.CurrentHealth, 0.001f);
            health.ApplyDamage(new DamageInfo(null, null, 1000000f, DamageType.Physical, false, Vector3.zero, Vector3.up));
            Assert.IsTrue(health.IsAlive, "a second hit changes nothing");
            health.Heal(health.MaxHealth);
            Assert.IsFalse(health.IsBroken);
        }
        finally
        {
            typeof(BuildingObject).GetMethod("OnDisable", flags).Invoke(building, null);
            Object.DestroyImmediate(wall);
        }
    }
}
