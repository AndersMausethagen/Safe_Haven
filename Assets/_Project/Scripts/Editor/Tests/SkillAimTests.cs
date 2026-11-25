using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Skills that go somewhere are held to aim; the rings their effects draw lie flat for the top-down camera (the user, 2026-11-25).</summary>
public sealed class SkillAimTests
{
    private static SkillDefinition Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{name}.asset");

    [TestCase("WolfsLunge")]
    [TestCase("MountainLeap")]
    [TestCase("FrostRune")]
    [TestCase("AshladsEmber")]
    [TestCase("HeartrootsGrasp")]
    [TestCase("TrollsHurl")]
    public void ASkillThatGoesSomewhere_IsHeldToAim(string name)
    {
        Assert.IsTrue(Skill(name).IsAimed, name);
    }

    [TestCase("WhirlingSteel")]
    [TestCase("TrollsbaneWrath")]
    [TestCase("WarCry")]
    [TestCase("HuldrasBlessing")]
    [TestCase("WeaponArt")]
    [TestCase("WintersBreath")]
    public void ARingRoundThePlayer_ABlessingOrAChannel_CastsOnThePress(string name)
    {
        Assert.IsFalse(Skill(name).IsAimed, name);
    }

    [Test]
    public void TheSkillRing_LiesFlatOnTheGround()
    {
        Vector3? facing = VfxGraphAuthoring.DirectionInput("Assets/_Project/Art/Effects/Skills/Graphs/Skill Ring.vfx", "AxisZ");
        Assert.IsTrue(facing.HasValue, "the ring's Orient block");
        Assert.Greater(Mathf.Abs(facing.Value.normalized.y), 0.99f, "the ring's quad faces up or down, not along the ground: " + facing.Value);
    }

    // The user, 2026-11-25: "shorter jumps and lunge if the player wants it, but not longer than maximum".
    [Test]
    public void ALungeGoesAsFarAsAimed_NeverPastItsOwnDistance()
    {
        float most = Skill("MountainLeap").LungeDistance;
        Assert.AreEqual(3f, PlayerSkills.LungeReach(most, 3f), 1e-4f, "aimed nearer: that far");
        Assert.AreEqual(most, PlayerSkills.LungeReach(most, most + 10f), 1e-4f, "aimed past it: its own distance");
        Assert.AreEqual(most, PlayerSkills.LungeReach(most, float.PositiveInfinity), 1e-4f, "no point aimed at (a stick): all of it");
        Assert.Greater(PlayerSkills.LungeReach(most, 0f), 0f, "aimed at the feet: still a step");
    }

    // The user, 2026-11-25: "Weapon art should change description based on which weapon we are holding": every art has its own words.
    [Test]
    public void EveryWeaponArtAndRuneArt_HasItsOwnWords()
    {
        var missing = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition"))
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (weapon.WeaponArt != null && string.IsNullOrWhiteSpace(weapon.WeaponArt.Description))
            {
                missing.Add($"{weapon.name}'s {weapon.WeaponArt.name}");
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.RuneArt != null && string.IsNullOrWhiteSpace(item.RuneArt.Description))
            {
                missing.Add($"{item.name}'s {item.RuneArt.name}");
            }
        }

        Assert.IsEmpty(missing, "these arts would show the generic Weapon's Art text");
    }
}
