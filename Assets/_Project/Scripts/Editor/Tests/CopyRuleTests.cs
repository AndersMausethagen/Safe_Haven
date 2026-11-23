using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Guard 5 of Docs/BugPatterns.md: the host's creatures in a friend's game. Every component a creature carries says what it does on
/// a friend's copy (OnAFriendsCopy); what the host decides is told once that it is a copy (ICopyAware) instead of asking IsRemote;
/// a friend's blow reaches the host whole (CreatureHitMessage.Of and Blow); and the same blow does the same there as here.
/// </summary>
public sealed class CopyRuleTests
{
    // Added to creatures while the game runs, so not on their prefabs: list one here when it is new.
    private static readonly Type[] AddedAtRuntime =
    {
        typeof(StatusEffects), typeof(Burning), typeof(ForcedMove), typeof(EliteCreature), typeof(LostOne), typeof(BoundSummons), typeof(BossTryLog),
    };

    private readonly List<GameObject> made = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject body in made)
        {
            if (body != null)
            {
                Object.DestroyImmediate(body);
            }
        }

        made.Clear();
    }

    private static IEnumerable<Type> CreatureComponents()
    {
        var types = new HashSet<Type>(AddedAtRuntime);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null || prefab.GetComponentInChildren<MonsterAI>(true) == null)
            {
                continue;
            }

            foreach (MonoBehaviour part in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (part != null && part.GetType().Assembly == typeof(MonsterAI).Assembly)
                {
                    types.Add(part.GetType());
                }
            }
        }

        return types;
    }

    [Test]
    public void EveryCreatureComponent_SaysWhatItDoesOnAFriendsCopy()
    {
        List<Type> found = CreatureComponents().ToList();
        Assert.Greater(found.Count, 15, "the creature prefabs' components were found");
        CollectionAssert.IsEmpty(found.Where(type => type.GetCustomAttribute<OnAFriendsCopyAttribute>() == null).Select(type => type.Name),
            "say what it does on a friend's copy of the host's creature: [OnAFriendsCopy(CopyRule..., \"how\")]");
    }

    [Test]
    public void WhatTheHostDecides_IsToldItIsACopy()
    {
        IEnumerable<string> untold = CreatureComponents()
            .Where(type => type.GetCustomAttribute<OnAFriendsCopyAttribute>()?.Rule == CopyRule.HostOnly && !typeof(ICopyAware).IsAssignableFrom(type))
            .Select(type => type.Name);
        CollectionAssert.IsEmpty(untold, "a HostOnly part is an ICopyAware: MonsterAI tells it when its creature becomes a copy");
    }

    [Test]
    public void AFriendsHit_ReachesTheHostWhole()
    {
        var blow = new DamageInfo(null, null, 12.5f, DamageType.Physical, true, new Vector3(1f, 2f, 3f), Vector3.forward, 4f,
            HitTechnique.Heavy | HitTechnique.Counter, StrikeKind.Blunt);
        var result = new DamageResult(12.5f, 10f, 10f, false, true, DamageType.Physical, GuardOutcome.Blocked);
        CreatureHitMessage hit = CreatureHitMessage.Of(7, blow, result);
        DamageInfo back = hit.Blow(null);

        // Who struck it is the host's to say, and the attack travels as its parts; everything else must come back as it was.
        var lost = new List<string>();
        foreach (FieldInfo field in typeof(DamageInfo).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.Name == nameof(DamageInfo.Source) || field.Name == nameof(DamageInfo.Attack))
            {
                continue;
            }

            if (!Equals(field.GetValue(blow), field.GetValue(back)))
            {
                lost.Add(field.Name);
            }
        }

        CollectionAssert.IsEmpty(lost, "a part of the blow doesn't reach the host: carry it in CreatureHitMessage (Of, Blow)");
        Assert.AreEqual(GuardOutcome.Blocked, hit.JudgedGuard, "and how the copy's shield took it");
        Assert.AreEqual(7, hit.Id);
    }

    private Damageable Body(string name, float bluntResistance)
    {
        var body = new GameObject(name);
        made.Add(body);
        var combatant = body.AddComponent<Combatant>();
        combatant.SetFaction(Faction.Enemy);
        combatant.Stats.SetBase(StatType.MaxHealth, 200f);
        combatant.Stats.SetBase(StatType.BluntResistance, bluntResistance);
        var health = body.AddComponent<Damageable>();
        var data = new SerializedObject(health);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(Damageable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(health, null);
        return health;
    }

    // The strike kind was lost on the way: a friend's hammer did a plain blow's damage to a creature weak to blunt.
    [Test]
    public void TheSameBlow_DoesTheSameHereAndThroughAFriend()
    {
        Damageable here = Body("Here", -0.5f);
        Damageable host = Body("The host's", -0.5f);
        var blow = new DamageInfo(null, null, 20f, DamageType.Physical, false, Vector3.zero, Vector3.forward, 5f, HitTechnique.Melee, StrikeKind.Blunt);

        DamageResult struck = here.ApplyDamage(blow);
        CreatureHitMessage sent = CreatureHitMessage.Of(1, blow, struck);
        DamageResult there = host.ApplyJudged(sent.Blow(null), sent.JudgedGuard);

        Assert.Greater(struck.HealthLost, 20f, "the weakness to blunt counts");
        Assert.AreEqual(struck.HealthLost, there.HealthLost, 1e-4f, "and counts the same in the host's game");
    }
}
