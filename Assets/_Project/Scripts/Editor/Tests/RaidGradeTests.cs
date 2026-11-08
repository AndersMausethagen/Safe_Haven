using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>The raids' grade (RaidGrade, RaidGradeVolume): every raid has one darker than home, and the volume sets only
/// the tonemapping, so the raid scene's night and weather volumes keep blending underneath.</summary>
public sealed class RaidGradeTests
{
    [Test]
    public void EveryRaid_IsGradedDarkerThanHome()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsNotNull(raid.Grade, raid.name);
            Assert.AreEqual(FilmicCurve.Neutral, raid.Grade.Curve, raid.name);
            Assert.Less(raid.Grade.Exposure, 0f, $"{raid.name} is darker than the Haven");
            Assert.Greater(raid.Grade.Contrast, 1f, $"{raid.name} keeps the contrast its volumes were graded under");
        }

        Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Hidden/Safe Haven/Filmic Tonemap")));
    }

    [Test]
    public void TheGradeVolume_SetsOnlyTheTonemapping()
    {
        var go = new GameObject("Raid Grade Test");
        try
        {
            var grade = go.AddComponent<RaidGradeVolume>();
            grade.Initialise();
            var volume = go.GetComponent<Volume>();
            Assert.IsFalse(volume.enabled, "off until a raid");
            grade.Apply(new RaidGrade());
            Assert.IsTrue(volume.enabled);
            Assert.IsTrue(volume.isGlobal);
            Assert.AreEqual(RaidGradeVolume.Priority, volume.priority);
            VolumeProfile profile = volume.sharedProfile;
            Assert.AreEqual(2, profile.components.Count, "filmic and URP tonemapping only");
            Assert.IsTrue(profile.TryGet(out Tonemapping tonemapping));
            Assert.AreEqual(TonemappingMode.None, tonemapping.mode.value);
            grade.Apply(null);
            Assert.IsFalse(volume.enabled, "off at home");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
