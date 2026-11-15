using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Art runes and the runic staff (Docs/CombatResearch.md Phase 3): a second art per kind of weapon, carved with Runecraft, attuned from
/// the bag and kept in the save; the arts the families had before 2026-11-15 come back as runes. The staff casts instead of striking.
/// </summary>
public sealed class ArtRuneTests
{
    private static ItemDefinition Item(string name) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/{name}.asset");

    [Test]
    public void EveryKindOfWeaponHasARune_CarvedWithRunecraft()
    {
        var kinds = new System.Collections.Generic.HashSet<WeaponSignature>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || !item.IsArtRune)
            {
                continue;
            }

            kinds.Add(item.RuneFor);
            Assert.IsTrue(item.IsUsable, item.name);
            Assert.IsNull(item.RaidModifier, $"{item.name} is no raid modifier");
            Assert.Greater(item.RuneFocus, 0f, item.name);
            Assert.IsNotEmpty(item.RuneArt.AnimationState, item.name);
        }

        foreach (WeaponSignature kind in new[] { WeaponSignature.CounterChain, WeaponSignature.Assassin, WeaponSignature.GuardBreaker, WeaponSignature.Momentum,
                     WeaponSignature.HyperArmour, WeaponSignature.Crush, WeaponSignature.AntiCharge, WeaponSignature.Flurry })
        {
            Assert.IsTrue(kinds.Contains(kind), $"no rune for {kind}");
        }

        Assert.AreEqual("1H Spin", Item("RuneoftheWhirl").RuneArt.AnimationState, "the sword's old spin comes back as its rune");
    }

    [Test]
    public void AnAttunedRune_IsKeptInTheProgress_AndItsSaveForm()
    {
        var progress = new CharacterProgress();
        Assert.IsNull(progress.RuneIdFor(WeaponSignature.CounterChain));
        Assert.IsTrue(progress.AttuneRune(WeaponSignature.CounterChain, "runeofthewhirl"));
        Assert.IsFalse(progress.AttuneRune(WeaponSignature.CounterChain, "runeofthewhirl"), "already attuned");
        var saved = new System.Collections.Generic.List<string>(progress.AttunedRunes);
        var loaded = new CharacterProgress();
        loaded.RestoreAttunedRunes(saved);
        Assert.AreEqual("runeofthewhirl", loaded.RuneIdFor(WeaponSignature.CounterChain));
        Assert.IsNull(loaded.RuneIdFor(WeaponSignature.Crush));
    }

    [Test]
    public void TheRunicStaff_CastsItsChain_AndFeedsFocus()
    {
        ItemDefinition staff = Item("RunicStaff");
        Assert.IsNotNull(staff, "the runic staff");
        WeaponDefinition weapon = staff.Weapon;
        Assert.AreEqual(WeaponSignature.Channel, weapon.Signature);
        Assert.IsNotNull(weapon.ComboStep(0).CastProjectile, "its light swing casts a bolt");
        Assert.Greater(weapon.HeavyAttack.CastCount, 1, "held, it fans bolts");
        Assert.IsNotNull(weapon.WeaponArt.CastHazard, "its art binds the ground");
        float pace = 0f, time = 0f;
        foreach (AttackDefinition attack in weapon.Combo)
        {
            pace += attack.WeaponDamageRatio;
            time += attack.RecoveryTime;
        }

        Assert.Less(pace / time, 1.43f, "from range it hits slower than the spear, the slowest one-handed reach");
    }
}
