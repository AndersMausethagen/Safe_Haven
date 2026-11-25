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
}
