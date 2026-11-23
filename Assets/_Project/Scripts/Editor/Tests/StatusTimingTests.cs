using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Getting free of a status (Docs/HeartrootPlaytest_2026-11-23.md P5): the user's first Heartroot try ended in three Root Eruption rings
/// in a second, each ring restarting Roots. A status that doesn't refresh keeps its first end, one that rests can't take hold again at
/// once, and Roots is set up that way and ends on a roll.
/// </summary>
public sealed class StatusTimingTests
{
    private static StatusEffectDefinition Effect(bool refreshes, float seconds, float rest)
    {
        var effect = ScriptableObject.CreateInstance<StatusEffectDefinition>();
        var so = new SerializedObject(effect);
        so.FindProperty("refreshes").boolValue = refreshes;
        so.FindProperty("seconds").floatValue = seconds;
        so.FindProperty("restSeconds").floatValue = rest;
        so.ApplyModifiedPropertiesWithoutUndo();
        return effect;
    }

    [Test]
    public void AHitWhileAtWork_RestartsOnlyAnEffectThatRefreshes()
    {
        StatusEffectDefinition poison = Effect(true, 6f, 0f);
        StatusEffectDefinition roots = Effect(false, 1.6f, 1.5f);
        try
        {
            // Laid at 0, hit again at 0.55 (Root Eruption's next ring).
            float poisonEnd = StatusTiming.EndsAt(poison, false, 0f, 0f);
            float rootsEnd = StatusTiming.EndsAt(roots, false, 0f, 0f);
            Assert.AreEqual(6.55f, StatusTiming.EndsAt(poison, true, poisonEnd, 0.55f), 1e-4f, "poison restarts its time");
            Assert.AreEqual(1.6f, StatusTiming.EndsAt(roots, true, rootsEnd, 0.55f), 1e-4f, "roots keep their first end");
        }
        finally
        {
            Object.DestroyImmediate(poison);
            Object.DestroyImmediate(roots);
        }
    }

    [Test]
    public void AfterItEnds_ItRestsBeforeTakingHoldAgain()
    {
        StatusEffectDefinition roots = Effect(false, 1.6f, 1.5f);
        try
        {
            float restUntil = StatusTiming.RestUntil(roots, 2f);
            Assert.IsFalse(StatusTiming.MayTakeHold(2.5f, restUntil), "the next ring of the volley can't root again");
            Assert.IsTrue(StatusTiming.MayTakeHold(3.5f, restUntil), "a later attack can");
        }
        finally
        {
            Object.DestroyImmediate(roots);
        }
    }

    [Test]
    public void Roots_DontRefresh_RestAfter_AndARollTearsFree()
    {
        var roots = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Roots.asset");
        Assert.IsNotNull(roots);
        Assert.IsFalse(roots.Refreshes, "a volley's later rings mustn't hold the player longer than the first");
        Assert.IsTrue(roots.EndsOnDodge, "a roll is the way out");
        Assert.Greater(roots.RestSeconds, 0f, "it can't take hold again the moment the player rolls clear");
        StringAssert.Contains("Roll", roots.TakesHoldLine, "the player is told the way out");
    }

    [Test]
    public void ARoll_EndsWhatARollEnds_AndNothingElse()
    {
        var player = new GameObject("Rooted");
        player.AddComponent<Combatant>();
        Damageable body = player.AddComponent<Damageable>();
        StatusEffectDefinition roots = Effect(false, 5f, 1f);
        StatusEffectDefinition poison = Effect(true, 5f, 0f);
        var so = new SerializedObject(roots);
        so.FindProperty("endsOnDodge").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            StatusEffects effects = StatusEffects.Apply(body, roots, null);
            StatusEffects.Apply(body, poison, null);
            Assert.IsNotNull(effects);
            Assert.IsTrue(effects.Has(roots) && effects.Has(poison), "both took hold");
            Assert.AreEqual(1, effects.EndOnDodge(), "one effect ends on a roll");
            Assert.IsFalse(effects.Has(roots), "the roots are torn free");
            Assert.IsTrue(effects.Has(poison), "the poison stays");
            StatusEffects.Apply(body, roots, null);
            Assert.IsFalse(effects.Has(roots), "torn free, it rests before it can take hold again");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(roots);
            Object.DestroyImmediate(poison);
        }
    }
}
