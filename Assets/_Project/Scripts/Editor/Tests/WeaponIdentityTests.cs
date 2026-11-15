using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The user, 2026-11-15: three basic swings for every weapon. Docs/CombatResearch.md §3.5: each family has its own chain (no two the
/// same, the spear no longer swinging the same clip twice), context attacks, a signature and an art that moves, while its damage per
/// second (Σ weapon ratio over Σ recovery, §1.3) stays what raid tiers were tuned to, so the weapon types stay balanced.
/// </summary>
public sealed class WeaponIdentityTests
{
    private static WeaponDefinition Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{name}.asset");

    private static float RatioPerSecond(WeaponDefinition weapon) =>
        weapon.Combo.Sum(attack => attack.WeaponDamageRatio) / weapon.Combo.Sum(attack => attack.RecoveryTime);

    // Before 2026-11-15, from §1.3 (sword, dagger and fists gained a fourth swing, two-handers a third, at the same pace).
    private static readonly (string weapon, float ratioPerSecond, WeaponSignature signature)[] Families =
    {
        ("IronSword", 1.60f, WeaponSignature.CounterChain), ("FangDagger", 1.76f, WeaponSignature.Assassin),
        ("CopperHandAxe", 1.60f, WeaponSignature.GuardBreaker), ("Greataxe", 1.07f, WeaponSignature.Momentum),
        ("IronGreatsword", 1.07f, WeaponSignature.HyperArmour), ("IronWarhammer", 1.07f, WeaponSignature.Crush),
        ("Spear", 1.43f, WeaponSignature.AntiCharge),
    };

    [Test]
    public void EachFamilyKeepsItsPace_AndHasItsOwnSignature()
    {
        foreach ((string name, float pace, WeaponSignature signature) in Families)
        {
            WeaponDefinition weapon = Weapon(name);
            Assert.AreEqual(signature, weapon.Signature, name);
            Assert.AreEqual(pace, RatioPerSecond(weapon), pace * 0.06f, $"{name}'s damage per second moved");
            Assert.GreaterOrEqual(weapon.ComboLength, 3, $"{name} has a chain of three or more");
            Assert.IsNotNull(weapon.ChargedAttack, $"{name} has a full-charge release");
            Assert.IsTrue(weapon.WeaponArt.Motion != ArtMotion.None || weapon.WeaponArt.WindupMove > 0f, $"{name}'s art moves the player");
        }

        Assert.AreEqual(WeaponSignature.Flurry, Weapon("Fists").Signature);
    }

    [Test]
    public void NoTwoFamiliesSwingTheSameChain_AndTheSpearNoLongerRepeats()
    {
        var chains = new HashSet<string>();
        foreach ((string name, _, _) in Families)
        {
            WeaponDefinition weapon = Weapon(name);
            string chain = string.Join("|", weapon.Combo.Select(attack => attack.AnimationState));
            Assert.IsTrue(chains.Add(chain), $"{name} swings the same chain as another family: {chain}");
        }

        WeaponDefinition spear = Weapon("Spear");
        Assert.AreNotEqual(spear.ComboStep(0).AnimationState, spear.ComboStep(1).AnimationState, "the spear's first two swings differ");
        Assert.IsNotNull(spear.BackstepAttack, "the spear's butt strike");
        Assert.Less(spear.BackstepAttack.Pull, 0f, "it shoves away");
    }

    [Test]
    public void ShieldsHaveAGuardCounter()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (weapon.IsShield)
            {
                Assert.IsNotNull(weapon.OwnGuardCounter, weapon.name);
                Assert.Greater(weapon.BlockPoise, 0f, weapon.name);
            }
        }
    }
}
