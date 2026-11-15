using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The user, 2026-11-15, approving Docs/CombatResearch.md §3.4.3-3.4.4: the loop was "attack once or twice, wait to parry, parry then
/// attack" because a parry floored anything and paid more Focus than a whole chain, while monsters traded through the player's swings.
/// Now a parry pays less and doesn't floor great beasts, a parry fished for costs, attacking pays more, light strikes and heavy blows
/// break wind-ups, and a block can be answered with a guard counter.
/// </summary>
public sealed class ParryEconomyTests
{
    [Test]
    public void AParryFloorsTheOrdinary_ButOnlyWearsDownTheGreat()
    {
        Assert.AreEqual(ParryResponse.Stagger, GuardRules.ParryResponseOf(ParryResponse.Auto, false));
        Assert.AreEqual(ParryResponse.PoiseDamage, GuardRules.ParryResponseOf(ParryResponse.Auto, true), "a great beast, boss or elite");
        Assert.AreEqual(ParryResponse.Recoil, GuardRules.ParryResponseOf(ParryResponse.Recoil, false), "the definition can say");

        // Two parries (or one and some blows) break a great beast's poise at 40% a parry.
        var poise = new PoiseMeter(450f, 2f, 15f);
        Assert.IsFalse(poise.Hit(450f * 0.4f, 0f));
        Assert.IsFalse(poise.Hit(450f * 0.4f, 0.5f));
        Assert.IsTrue(poise.Hit(450f * 0.4f, 1f), "the third breaks it; with a few blows between, the second does");
    }

    [Test]
    public void HeavyBlowsBreakThroughWindups_NeverAGreatBeasts()
    {
        Assert.IsTrue(GuardRules.BreaksThrough(HitTechnique.Heavy, 10f, 140f, 0.25f, false));
        Assert.IsTrue(GuardRules.BreaksThrough(HitTechnique.GuardCounter, 10f, 140f, 0.25f, false));
        Assert.IsTrue(GuardRules.BreaksThrough(HitTechnique.None, 40f, 140f, 0.25f, false), "a blow worth a quarter of its poise");
        Assert.IsFalse(GuardRules.BreaksThrough(HitTechnique.None, 12f, 140f, 0.25f, false), "a plain light swing doesn't");
        Assert.IsFalse(GuardRules.BreaksThrough(HitTechnique.Heavy, 200f, 450f, 0.25f, true), "a great beast keeps its strike");
        Assert.IsFalse(GuardRules.BreaksThrough(HitTechnique.Heavy, 10f, 140f, 0f, false), "0 turns it off");
    }

    [Test]
    public void ATappedGuardThatCaughtNothing_StaysDownLonger()
    {
        Assert.AreEqual(0.6f, GuardRules.RelockAfter(false, 0.2f, 0.5f, 0.3f, 0.6f), 1e-5f, "a parry fished for");
        Assert.AreEqual(0.3f, GuardRules.RelockAfter(true, 0.2f, 0.5f, 0.3f, 0.6f), 1e-5f, "it caught a blow");
        Assert.AreEqual(0.3f, GuardRules.RelockAfter(false, 1.2f, 0.5f, 0.3f, 0.6f), 1e-5f, "held up, not tapped");
    }

    // The settings as tuned: a parry is worth about two landed hits, not six, and an art costs about four hits or a parry and two.
    [Test]
    public void AttackingEarnsFocus_AsWellAsParrying()
    {
        CombatTechniqueSettings rules = Resources.Load<CombatTechniqueSettings>(CombatTechniqueSettings.ResourceName);
        Assert.LessOrEqual(rules.FocusOnParry, rules.FocusPerHit * 2.5f, "a parry is worth a couple of hits");
        Assert.Less(rules.FocusOnPerfectDodge, 25f);
        var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/IronSword.asset");
        float chain = 0f;
        for (int step = 0; step < sword.ComboLength; step++)
        {
            HitTechnique technique = step == sword.ComboLength - 1 ? HitTechnique.Finisher : HitTechnique.None;
            chain += PlayerFocus.ForSwing(new SwingInfo(SwingKind.Light, sword.ComboStep(step), step, sword.ComboLength, 0, 1f, technique, Vector3.forward), 1, rules);
        }

        Assert.Greater(chain, rules.FocusOnParry * 1.5f, "a whole chain landed earns more than a parry");
        Assert.GreaterOrEqual(chain + rules.FocusOnParry, sword.WeaponArtFocus, "a parry and a chain pay for the art");
        Assert.Greater(rules.BreakThroughPoiseShare, 0f);
        Assert.Greater(rules.WhiffRelock, rules.GuardRelock);
    }

    // The light strikes of ordinary creatures can be interrupted; trolls, knights and bosses keep theirs.
    [Test]
    public void LightStrikesCanBeInterrupted_GreatOnesCant()
    {
        int light = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterDefinition"))
        {
            var monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (MonsterAttack entry in monster.Attacks)
            {
                if (entry.attack == null || entry.kind != MonsterAttackKind.Melee || !entry.interruptible)
                {
                    continue;
                }

                light++;
                Assert.IsTrue(monster.OpenWhileStriking, $"{monster.name}'s {entry.attack.name}: a great beast's strike can't be broken off");
                Assert.LessOrEqual(entry.attack.HitDelay, 0.6f, $"{monster.name}'s {entry.attack.name} is no light strike");
            }
        }

        Assert.GreaterOrEqual(light, 10, "the bites, gores, shoves and grasps");
        var knight = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("UndeadKnightFire t:MonsterDefinition")[0]));
        Assert.AreEqual(ParryResponse.PoiseDamage, knight.ParryResponse, "an armoured knight isn't floored by a parry");
    }

    [Test]
    public void ShieldsKnockBackWhatTheyBlock_BetterShieldsMore()
    {
        WeaponDefinition Shield(string name) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{name}.asset");
        Assert.Greater(Shield("WickerShield").BlockPoise, 0f);
        Assert.Greater(Shield("IronKiteShield").BlockPoise, Shield("WickerShield").BlockPoise);
    }
}
