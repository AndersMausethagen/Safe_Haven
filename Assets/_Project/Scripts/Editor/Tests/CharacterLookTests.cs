using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The character look: character materials wear Lit (See-Through) with a skin or cloth keyword (so a build keeps
/// the variant), eyes stay Lit, and skin is told from cloth by name.</summary>
public sealed class CharacterLookTests
{
    [Test]
    public void SkinIsToldFromCloth()
    {
        Assert.IsTrue(CharacterLook.IsSkin("M_HU_M_Body_04"));
        Assert.IsTrue(CharacterLook.IsSkin("M_HU_F_Head_02_A"));
        Assert.IsTrue(CharacterLook.IsSkin("Shaman goblin"));
        Assert.IsFalse(CharacterLook.IsSkin("M_HU_M_Body_Cloth_01"), "clothes on a body are cloth");
        Assert.IsFalse(CharacterLook.IsSkin("Wolf Main"));
        Assert.IsFalse(CharacterLook.IsSkin("M_HU_Hair_01_Br"));
    }

    [Test]
    public void CharacterMaterials_WearTheCharacterLook()
    {
        string[] checks =
        {
            "Assets/StylizedCharacter/Materials/Character/Human/Male/Body/M_HU_M_Body_04.mat",
            "Assets/_Project/Art/Enemies/Downloaded/Wolf/Wolf Main.mat",
        };
        foreach (string path in checks)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.AreEqual("Safe Haven/Lit (See-Through)", material.shader.name, path);
            Assert.IsTrue(material.IsKeywordEnabled("_CHARACTER_SKIN") ^ material.IsKeywordEnabled("_CHARACTER_CLOTH"), path);
        }

        Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Safe Haven/Lit (See-Through)")));
    }
}
