using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Guards what Set Up Combat Animations made: a swing whose state isn't on the animator plays nothing, and a swing whose
/// clip was fitted must still reach it in time.</summary>
public sealed class CombatMovesetTests
{
    private static IEnumerable<WeaponDefinition> MeleeWeapons() => AssetDatabase.FindAssets("t:WeaponDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(weapon => weapon != null && weapon.Kind == WeaponKind.Melee);

    private static IEnumerable<AttackDefinition> Attacks(WeaponDefinition weapon)
    {
        foreach (AttackDefinition attack in weapon.Combo)
        {
            yield return attack;
        }

        if (weapon.HeavyAttack != null) yield return weapon.HeavyAttack;
        if (weapon.CounterAttack != null) yield return weapon.CounterAttack;
        if (weapon.WeaponArt != null) yield return weapon.WeaponArt;
    }

    private static HashSet<string> States(string controller, string layer)
    {
        var loaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(controller);
        AnimatorControllerLayer found = layer == null ? loaded.layers[0] : loaded.layers.First(l => l.name == layer);
        return new HashSet<string>(found.stateMachine.states.Select(state => state.state.name));
    }

    [Test]
    public void EverySwingPlaysAStateThePlayerHas()
    {
        HashSet<string> states = States(CombatAnimationSetup.PlayerControllerPath, CombatAnimationSetup.FullBodyLayer);
        foreach (WeaponDefinition weapon in MeleeWeapons())
        {
            foreach (AttackDefinition attack in Attacks(weapon))
            {
                Assert.IsNotNull(attack, $"{weapon.name} has an empty swing");
                Assert.IsTrue(states.Contains(attack.AnimationState), $"{weapon.name}: {attack.name} plays '{attack.AnimationState}', which isn't on the animator");
            }
        }
    }

    [Test]
    public void EveryArmedFamilyHasAWeaponArt()
    {
        foreach (WeaponDefinition weapon in MeleeWeapons().Where(weapon => weapon.Stance != WeaponStance.Unarmed))
        {
            Assert.IsNotNull(weapon.WeaponArt, $"{weapon.name} has no weapon art");
            Assert.Greater(weapon.WeaponArtFocus, 0f, $"{weapon.name}'s art costs nothing");
        }
    }

    [Test]
    public void EverySwingsStrikeFallsWithinItsWindup()
    {
        foreach (WeaponDefinition weapon in MeleeWeapons())
        {
            foreach (AttackDefinition attack in Attacks(weapon))
            {
                Assert.Less(attack.StrikeLead, attack.HitDelay, $"{attack.name}: the strike takes over before the swing starts");
                Assert.GreaterOrEqual(attack.RecoveryTime, attack.HitDelay, $"{attack.name} recovers before it lands");
                Assert.GreaterOrEqual(attack.StrikeSpeed, attack.AnimationSpeed, $"{attack.name}: the strike should not be slower than its build-up");
                Assert.Greater(attack.AnimationSpeed, 0.2f, $"{attack.name}: a build-up this slow reads as a frozen pose");
            }
        }
    }

    // A heavy swing waits with the legs planted, then the legs join in: a clip already in the air would pop up from the ground.
    [Test]
    public void NoHeavySwingLeavesTheGround()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CombatAnimationSetup.PlayerControllerPath);
        AnimatorControllerLayer layer = controller.layers.First(l => l.name == CombatAnimationSetup.FullBodyLayer);
        foreach (WeaponDefinition weapon in MeleeWeapons().Where(weapon => weapon.HeavyAttack != null))
        {
            var clip = layer.stateMachine.states.First(s => s.state.name == weapon.HeavyAttack.AnimationState).state.motion as AnimationClip;
            Assert.IsFalse(CombatAnimationSetup.Leaps(clip, out _), $"{weapon.name}'s heavy swing {weapon.HeavyAttack.name} plays a leap");
        }
    }

    [Test]
    public void EveryWeaponHasARushAttack()
    {
        HashSet<string> states = States(CombatAnimationSetup.PlayerControllerPath, CombatAnimationSetup.FullBodyLayer);
        foreach (WeaponDefinition weapon in MeleeWeapons())
        {
            Assert.IsNotNull(weapon.RushAttack, $"{weapon.name} has no rush attack");
            Assert.IsTrue(states.Contains(weapon.RushAttack.AnimationState), $"{weapon.RushAttack.name} plays a state the player doesn't have");
            Assert.Greater(weapon.RushAttack.Lunge, 0f, $"{weapon.RushAttack.name} doesn't lunge");
            Assert.Less(weapon.RushAttack.HitDelay, 0.45f, $"{weapon.RushAttack.name} isn't quick");
        }
    }

    [Test]
    public void MonsterStrikesPlayStatesTheControllerHas()
    {
        HashSet<string> states = States(CombatAnimationSetup.MonsterControllerPath, null);
        foreach (string guid in AssetDatabase.FindAssets("t:AttackDefinition"))
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            string path = AssetDatabase.GetAssetPath(attack);
            bool isPlayers = path.Contains("/Combos/") || attack.AnimationState == null || attack.AnimationState.Length == 0
                             || States(CombatAnimationSetup.PlayerControllerPath, CombatAnimationSetup.FullBodyLayer).Contains(attack.AnimationState);
            if (!isPlayers)
            {
                Assert.IsTrue(states.Contains(attack.AnimationState), $"{attack.name} plays '{attack.AnimationState}', which is on neither controller");
            }
        }
    }

    [Test]
    public void BloodAndMarkersAreSetUp()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Assert.IsNotNull(feel.BloodEffects, "Combat Feel has no blood prefab: run Safe Haven › Combat › Build Blood and Markers");
        Assert.IsTrue(feel.BloodEffects.HasPools, "the blood prefab has no pools: run Safe Haven › Combat › Build Blood and Markers");
        Assert.Greater(feel.BloodPoolSeconds, feel.CorpseSeconds, "a pool should outlast the body lying in it");
        Assert.IsNotNull(feel.FocusMarkerMaterial);
        Assert.Greater(feel.CorpseSeconds, feel.CorpseSettleSeconds, "a corpse should come to rest before it sinks");
    }

    [Test]
    public void ThePlayerCarriesTheNewParts()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        Assert.IsNotNull(player.GetComponent<PlayerFocusTarget>());
        Assert.IsNotNull(player.GetComponent<WeaponTrail>());
        Assert.IsNotNull(player.GetComponent<TwoHandGrip>());
    }
}
