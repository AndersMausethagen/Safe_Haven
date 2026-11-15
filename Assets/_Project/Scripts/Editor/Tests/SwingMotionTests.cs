using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The user, 2026-11-15: "3 basic animations which locks the player in place", so the loop was spam-and-die or wait-parry-attack.
/// Docs/CombatResearch.md Phase 0: swings let the player move and turn by weapon, the step into the blow is longer, mashing no
/// longer commits a whole chain (the bounded buffer), and none of it moves the chain point, so cadence holds.
/// </summary>
public sealed class SwingMotionTests
{
    private CombatTechniqueSettings rules;
    private readonly List<Object> made = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        rules = ScriptableObject.CreateInstance<CombatTechniqueSettings>();
        made.Add(rules);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private AttackDefinition Attack(float windup = -1f, float after = -1f, float turn = -1f, float moveAt = -1f, float lunge = -1f)
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        made.Add(attack);
        attack.ConfigureMovement(windup, after, turn, moveAt, lunge);
        return attack;
    }

    private void Set(string field, float value)
    {
        var data = new SerializedObject(rules);
        data.FindProperty(field).floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [Test]
    public void AnAttackSaysHowItMoves_OrTheSettingsDo()
    {
        Set("windupMoveSpeed", 0.25f);
        Set("swingMoveSpeed", 0.5f);
        Set("heavyMoveSpeed", 0.2f);
        AttackDefinition plain = Attack(), dagger = Attack(0.6f, 0.6f);
        Assert.AreEqual(0.25f, SwingMotion.WindupMove(plain, SwingKind.Light, rules));
        Assert.AreEqual(0.6f, SwingMotion.WindupMove(dagger, SwingKind.Light, rules), "its own share");
        Assert.AreEqual(0f, SwingMotion.WindupMove(plain, SwingKind.Heavy, rules), "a heavy winds up planted");
        Assert.AreEqual(0f, SwingMotion.WindupMove(plain, SwingKind.Rush, rules), "a rush lunges instead");
        Assert.AreEqual(0f, SwingMotion.WindupMove(plain, SwingKind.Art, rules), "an art's leap or spin is the whole body's");
        Assert.AreEqual(0.5f, SwingMotion.AfterContactMove(plain, SwingKind.Light, rules));
        Assert.AreEqual(0.2f, SwingMotion.AfterContactMove(plain, SwingKind.Heavy, rules));
        Assert.AreEqual(0.6f, SwingMotion.AfterContactMove(dagger, SwingKind.Light, rules));
    }

    [Test]
    public void TheLegsComeBackAMomentAfterTheBlow_NeverAfterTheChainPoint()
    {
        Set("moveAfterContact", 0.1f);
        Assert.AreEqual(0.38f, SwingMotion.MoveAt(Attack(), 0.28f, 0.46f, 1f, rules), 1e-5f);
        Assert.AreEqual(0.33f, SwingMotion.MoveAt(Attack(moveAt: 0.05f), 0.28f, 0.46f, 1f, rules), 1e-5f, "its own moment");
        Assert.AreEqual(0.3f, SwingMotion.MoveAt(Attack(moveAt: 0.5f), 0.28f, 0.3f, 1f, rules), 1e-5f, "never past recovery");
    }

    [Test]
    public void TheStepIntoTheBlowTakesTheLastPartOfTheWindup()
    {
        Set("lungeShare", 0.33f);
        Assert.AreEqual(0.12f, SwingMotion.LungeLead(Attack(), SwingKind.Light, 0.28f, 0.12f, 0f, rules), 1e-5f, "at least the shortest");
        Assert.AreEqual(0.79f * 0.33f, SwingMotion.LungeLead(Attack(), SwingKind.Light, 0.79f, 0.12f, 0f, rules), 1e-5f, "a third of a finisher's");
        Assert.AreEqual(0.4f, SwingMotion.LungeLead(Attack(), SwingKind.Rush, 0.4f, 0.12f, 0f, rules), 1e-5f, "a rush all the way");
        Assert.AreEqual(0.15f, SwingMotion.LungeLead(Attack(), SwingKind.Heavy, 0.9f, 0.12f, 0.15f, rules), 1e-5f, "a heavy only after its charge");
    }

    [Test]
    public void TheBlowTurnsTowardTheAimOnlyEarlyInTheWindup()
    {
        Set("windupTurnShare", 0.6f);
        Assert.IsTrue(SwingMotion.CanTurn(0.1f, 0.6f, 0.2f, rules));
        Assert.IsFalse(SwingMotion.CanTurn(0.37f, 0.6f, 0.1f, rules), "past the share");
        Assert.IsFalse(SwingMotion.CanTurn(0.3f, 0.6f, 0.35f, rules), "not once it steps in");
        Assert.AreEqual(90f, SwingMotion.TurnRate(Attack(turn: 90f), rules));
    }

    [Test]
    public void Mashing_IsDropped_OnlyPressesNearTheChainPointAreKept()
    {
        Set("bufferWindow", 0.25f);
        Assert.IsFalse(SwingMotion.Buffers(0.1f, 0.46f, rules), "a press as the swing starts is dropped");
        Assert.IsTrue(SwingMotion.Buffers(0.25f, 0.46f, rules), "kept in the last quarter second");
        Assert.IsTrue(SwingMotion.BuffersWithin(0.2f, rules));
        Assert.IsFalse(SwingMotion.BuffersWithin(0.4f, rules));
        Set("bufferWindow", 0f);
        Assert.IsTrue(SwingMotion.Buffers(0f, 1f, rules), "0 keeps every press, as before");
    }

    // The settings asset and the families as tuned: daggers and fists keep most of their speed, swords and spears some, two-handers
    // almost none in the wind-up, heavies stay planted.
    [Test]
    public void TheProjectsSwingsMoveByWeapon()
    {
        CombatTechniqueSettings project = Resources.Load<CombatTechniqueSettings>(CombatTechniqueSettings.ResourceName);
        Assert.Greater(project.SwingMoveSpeed, 0f, "the player walks out of a swing");
        Assert.Greater(project.BufferWindow, 0f, "the buffer is bounded");
        float Windup(string weapon, int step = 0)
        {
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{weapon}.asset");
            Assert.IsNotNull(definition, weapon);
            return SwingMotion.WindupMove(definition.ComboStep(step), SwingKind.Light, project);
        }

        Assert.Greater(Windup("FangDagger"), Windup("IronSword"), "a dagger moves more than a sword");
        Assert.Greater(Windup("IronSword"), Windup("IronGreatsword"), "a sword more than a greatsword");
        Assert.Greater(Windup("Fists"), Windup("IronSword"));
        var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/IronSword.asset");
        Assert.AreEqual(0f, SwingMotion.WindupMove(sword.HeavyAttack, SwingKind.Heavy, project), "heavies wind up planted");
        Assert.GreaterOrEqual(sword.Combo.Min(attack => attack.Lunge), 0.8f, "chain swings step in at least 0.8 m");
        Assert.GreaterOrEqual(sword.Combo.Last().Lunge, 1.4f, "the finisher steps further");
    }
}
