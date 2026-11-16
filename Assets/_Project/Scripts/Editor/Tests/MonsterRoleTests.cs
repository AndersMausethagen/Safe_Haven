using NUnit.Framework;
using UnityEngine;

/// <summary>The actions a monster keeps for when it is hurt (Monster Attack › Below Health).</summary>
public sealed class MonsterRoleTests
{
    [Test]
    public void BelowHealth_OnlyOnceHurtEnough()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var hardens = new MonsterAttack { attack = attack, kind = MonsterAttackKind.Howl, maximumRange = 30f, belowHealth = 0.5f };
        Assert.IsFalse(hardens.IsAvailable(2f, 10f, 0f, false, 0, 0, 0.8f), "Not while it is still mostly whole.");
        Assert.IsTrue(hardens.IsAvailable(2f, 10f, 0f, false, 0, 0, 0.4f), "Once it is down to half.");
        Object.DestroyImmediate(attack);
    }

    [Test]
    public void BelowHealth_SavedBeforeItExisted_IsAlways()
    {
        Assert.IsTrue(new MonsterAttack { belowHealth = 0f }.WhenHurt(1f), "An entry saved before the field reads 0: always.");
        Assert.IsTrue(new MonsterAttack { belowHealth = 1f }.WhenHurt(1f));
    }

    [Test]
    public void SelectAttack_SkipsAHurtOnlyActionWhileWhole()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var actions = new[]
        {
            new MonsterAttack { attack = attack, kind = MonsterAttackKind.Melee, maximumRange = 3f, priority = 1 },
            new MonsterAttack { attack = attack, kind = MonsterAttackKind.Howl, maximumRange = 30f, priority = 30, belowHealth = 0.5f },
        };
        var cooldowns = new float[2];
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(actions, cooldowns, 2f, 1f, false, 0, 0, 0.9f));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(actions, cooldowns, 2f, 1f, false, 0, 0, 0.3f));
        Object.DestroyImmediate(attack);
    }

    // Review 2026-11-16: the Fossegrim and the Trollkjerring fought with the Moss Shaman's stats and the Berg-jutul with the Old
    // Moss Troll's, so their own assets (and the journal, which shows them) changed nothing in play.
    [Test]
    public void EveryMonster_FightsWithItsOwnStats()
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (monster.Prefab == null || monster.CombatStats == null || !monster.Prefab.TryGetComponent(out Combatant combatant))
            {
                continue;
            }

            Object used = new UnityEditor.SerializedObject(combatant).FindProperty("baseStats").objectReferenceValue;
            Assert.AreSame(monster.CombatStats, used, $"{monster.name}'s prefab fights with {(used != null ? used.name : "nothing")}");
        }
    }
}
