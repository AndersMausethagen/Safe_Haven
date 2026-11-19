using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The weight of a blow (Docs/CombatFeelReview_2026-11-19.md): how far it shoves a body (HitReaction.KnockbackFor) goes by its Impact
/// and technique, not by whether it was a critical. A dagger's sure critical shoved a troll further than a warhammer's heavy shoved a
/// rootling.
/// </summary>
public sealed class CombatWeightTests
{
    private static AttackDefinition WithImpact(float impact)
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        var data = new SerializedObject(attack);
        data.FindProperty("impact").floatValue = impact;
        data.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    private static float Shove(float impact, HitTechnique technique, bool critical = false)
    {
        AttackDefinition attack = WithImpact(impact);
        try
        {
            var blow = new DamageInfo(null, attack, 10f, DamageType.Physical, critical, Vector3.zero, Vector3.forward, 0f, technique);
            var result = new DamageResult(10f, 10f, 10f, false, critical, DamageType.Physical);
            return HitReaction.KnockbackFor(blow, result, null);
        }
        finally
        {
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void AHeavierBlow_ShovesFurther_AndACriticalBarelyMore()
    {
        float dagger = Shove(0.65f, HitTechnique.Melee);
        float sword = Shove(1f, HitTechnique.Melee);
        float hammerHeavy = Shove(1.8f, HitTechnique.Melee | HitTechnique.Heavy);
        Assert.Less(dagger, sword, "a dagger's stab moves a body less than a sword's slash");
        Assert.Less(sword, hammerHeavy, "a sword's slash less than a hammer's heavy");
        Assert.AreEqual(0.35f, sword, 0.01f, "a plain slash of Impact 1: the knockback distance");
        Assert.LessOrEqual(hammerHeavy, 1f + 1e-4f, "a heavy that isn't charged leaves its follow-up in reach");
        Assert.Less(Shove(0.65f, HitTechnique.Melee, critical: true), sword, "a dagger's critical still moves a body less than a slash");
    }

    [Test]
    public void NoBlow_ShovesFurtherThanTheMost()
    {
        float most = CombatFeelSettings.Current.KnockbackMost;
        Assert.LessOrEqual(Shove(3f, HitTechnique.WeaponArt, critical: true), most + 1e-4f);
    }

    // The parry cue (Docs/CombatFeelReview_2026-11-19.md §2): nobody reacts inside a 0.15 s window, so the cue comes before it.
    [Test]
    public void TheParryCue_ComesBeforeTheWindow_ButNeverBeforeAHold()
    {
        const float window = 0.15f, lead = 0.12f;
        // A Draug's cleave at the player: 0.27 s before contact, room to react and press inside the window.
        Assert.AreEqual(0.75f - 0.27f, AttackTelegraph.ParryCueAt(0.75f, window, lead, atMe: true, mayHold: false), 1e-4f);
        // At someone else: as the window opens, as before.
        Assert.AreEqual(0.75f - window, AttackTelegraph.ParryCueAt(0.75f, window, lead, atMe: false, mayHold: false), 1e-4f);
        // One that may hold its wind-up: not before its hold point, or the hold would bait nothing.
        float held = AttackTelegraph.ParryCueAt(0.75f, window, lead, atMe: true, mayHold: true);
        Assert.Greater(held, 0.75f * MonsterAttacks.HoldAtShare);
        Assert.Less(held, 0.75f - window + 1e-4f, "still before the window opens");
        // Too short for a lead: the window's own cue; shorter than the window: none.
        Assert.AreEqual(0.25f - window, AttackTelegraph.ParryCueAt(0.25f, window, lead, atMe: true, mayHold: false), 1e-4f);
        Assert.Less(AttackTelegraph.ParryCueAt(0.1f, window, lead, atMe: true, mayHold: false), 0f);
        // The line sits where the fill stands mid-window: 0.9 of the reach for the cleave.
        Assert.AreEqual(0.9f, AttackTelegraph.ParryLineAt(0.75f, window), 1e-4f);
    }

    // A charged heavy (its own flag) shoves to the most; an uncharged one keeps the body within its follow-up's reach.
    [Test]
    public void AChargedHeavy_ShovesFurtherThanAnUnchargedOne()
    {
        float uncharged = Shove(1.8f, HitTechnique.Melee | HitTechnique.Heavy);
        float charged = Shove(1.8f, HitTechnique.Melee | HitTechnique.Heavy | HitTechnique.Charged);
        Assert.LessOrEqual(uncharged, 1f + 1e-4f);
        Assert.Greater(charged, uncharged, "a sword's charged heavy (Impact 1.8) was capped at 1 m");
        Assert.LessOrEqual(charged, CombatFeelSettings.Current.KnockbackMost + 1e-4f);
    }
}
