using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>A death has to happen whatever a listener does: ending the raid and dropping the loot hang on it.</summary>
public sealed class DamageableFailureTests
{
    private GameObject body;

    private Damageable Victim()
    {
        body = new GameObject("Victim");
        var combatant = body.AddComponent<Combatant>();
        var victim = body.AddComponent<Damageable>();
        // Destroy is refused in edit mode, and reaching it is not what these tests are about.
        var data = new UnityEditor.SerializedObject(victim);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(combatant, "Awake");
        Invoke(victim, "Awake");
        return victim;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    }

    private static DamageInfo Lethal() => new DamageInfo(null, null, 1000000f, DamageType.Holy, false, Vector3.zero, Vector3.up);

    [TearDown]
    public void TearDown()
    {
        if (body != null)
        {
            Object.DestroyImmediate(body);
        }
    }

    [Test]
    public void ABrokenDamagedListener_DoesNotSwallowTheDeath()
    {
        Damageable victim = Victim();
        bool died = false;
        victim.Damaged += (damage, result) => throw new InvalidOperationException("listener broke");
        victim.Died += (who, blow) => died = true;

        LogAssert.Expect(LogType.Exception, new Regex("listener broke"));
        DamageResult outcome = victim.ApplyDamage(Lethal());

        Assert.IsTrue(outcome.Killed);
        Assert.IsTrue(died, "the raid's end and the loot wait on Died");
    }

    [Test]
    public void ABrokenHealthListener_DoesNotSwallowTheDeath()
    {
        Damageable victim = Victim();
        bool died = false;
        victim.HealthChanged += (current, maximum) => throw new InvalidOperationException("listener broke");
        victim.Died += (who, blow) => died = true;

        LogAssert.Expect(LogType.Exception, new Regex("listener broke"));
        victim.ApplyDamage(Lethal());

        Assert.IsTrue(died);
    }

    [Test]
    public void ABrokenDiedListener_DoesNotStopTheOthers()
    {
        Damageable victim = Victim();
        bool heard = false;
        victim.Died += (who, blow) => throw new InvalidOperationException("listener broke");
        victim.Died += (who, blow) => heard = true;

        LogAssert.Expect(LogType.Exception, new Regex("listener broke"));
        victim.ApplyDamage(Lethal());

        Assert.IsTrue(heard);
    }
}
