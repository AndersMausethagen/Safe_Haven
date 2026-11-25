using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The user, 2026-11-15: three basic swings for every weapon. Docs/CombatResearch.md §3.5: each family has its own chain (no two the
/// same, the spear no longer swinging the same clip twice), context attacks, a signature and an art that moves. Damage per second is no
/// longer held here: since 2026-11-25 the swings play at their clips' own pace and the user retunes damage after (CombatClipPace).
/// </summary>
public sealed class WeaponIdentityTests
{
    private static WeaponDefinition Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{CombatAnimationSetup.WeaponFolder}/{name}.asset");

    private static readonly (string weapon, WeaponSignature signature)[] Families =
    {
        ("IronSword", WeaponSignature.CounterChain), ("FangDagger", WeaponSignature.Assassin),
        ("CopperHandAxe", WeaponSignature.GuardBreaker), ("Greataxe", WeaponSignature.Momentum),
        ("IronGreatsword", WeaponSignature.HyperArmour), ("IronWarhammer", WeaponSignature.Crush),
        ("Spear", WeaponSignature.AntiCharge),
    };

    [Test]
    public void EachFamilyHasItsOwnSignature_AChain_AChargeAndAnArtThatMoves()
    {
        foreach ((string name, WeaponSignature signature) in Families)
        {
            WeaponDefinition weapon = Weapon(name);
            Assert.AreEqual(signature, weapon.Signature, name);
            Assert.GreaterOrEqual(weapon.ComboLength, 3, $"{name} has a chain of three or more");
            Assert.IsNotNull(weapon.ChargedAttack, $"{name} has a full-charge release");
            Assert.IsTrue(weapon.WeaponArt.Motion != ArtMotion.None || (weapon.WeaponArt.OwnWindupMove ?? 0f) > 0f, $"{name}'s art moves the player");
        }

        Assert.AreEqual(WeaponSignature.Flurry, Weapon("Fists").Signature);
    }

    [Test]
    public void NoTwoFamiliesSwingTheSameChain_AndTheSpearNoLongerRepeats()
    {
        var chains = new HashSet<string>();
        foreach ((string name, _) in Families)
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
