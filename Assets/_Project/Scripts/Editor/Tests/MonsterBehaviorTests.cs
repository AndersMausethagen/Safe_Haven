using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MonsterBehaviorTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object value in created)
        {
            Object.DestroyImmediate(value);
        }
        created.Clear();
    }

    [Test]
    public void Selection_UsesPriorityAndListOrder()
    {
        MonsterAttack first = Action(2), second = Action(5), third = Action(5);
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { first, second, third }, new float[3], 2f, 0f, false));
    }

    [Test]
    public void Selection_RespectsIndependentCooldownsAndRange()
    {
        MonsterAttack shove = Action(10), stones = Action(1);
        shove.maximumRange = 2f;
        stones.minimumRange = 3f;
        stones.maximumRange = 14f;
        var entries = new[] { shove, stones };
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(entries, new float[2], 6f, 0f, false));
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 6f, 4f, false));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 6f, 5f, false));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 1f, 4f, false));
    }

    [Test]
    public void Selection_HowlRequiresLeader()
    {
        MonsterAttack bite = Action(1), howl = Action(10);
        howl.packLeaderOnly = true;
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(new[] { bite, howl }, new float[2], 1f, 0f, false));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { bite, howl }, new float[2], 1f, 0f, true));
    }

    [Test]
    public void Selection_SkipsIncompleteEntries()
    {
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(new MonsterAttack[] { null, new MonsterAttack() }, new float[2], 1f, 0f, true));
    }

    [Test]
    public void Definition_DefaultsPreserveLegacyChaser()
    {
        MonsterDefinition monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        created.Add(monster);
        Assert.AreEqual(0, monster.Attacks.Count);
        Assert.AreEqual(MonsterMovementTactic.Chase, monster.MovementTactic);
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Aggressive));
        Assert.IsFalse(monster.HasTemperament(MonsterTemperament.Cowardly));
    }

    [Test]
    public void Definition_AllowsCombinedTemperaments()
    {
        MonsterDefinition monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        created.Add(monster);
        var data = new SerializedObject(monster);
        data.FindProperty("temperament").intValue = (int)(MonsterTemperament.Aggressive | MonsterTemperament.Cowardly);
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Aggressive));
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Cowardly));
        Assert.IsFalse(monster.HasTemperament(MonsterTemperament.Defensive));
    }

    private MonsterAttack Action(int priority)
    {
        AttackDefinition attack = ScriptableObject.CreateInstance<AttackDefinition>();
        created.Add(attack);
        return new MonsterAttack { attack = attack, priority = priority, maximumRange = 10f };
    }
}
