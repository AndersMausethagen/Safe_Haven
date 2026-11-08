using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The sky (SkyDome, Sky.shader): it is set up, the sun and moon stand where the hour puts them, and the moon
/// goes through its phases.</summary>
public sealed class SkyTests
{
    [Test]
    public void TheSky_IsSetUpAndCompiles()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        Assert.IsNotNull(settings.SkyMaterial, "RenderingSettings › Sky Material");
        Assert.AreEqual("Safe Haven/Sky", settings.SkyMaterial.shader.name);
        Assert.IsFalse(ShaderUtil.ShaderHasError(settings.SkyMaterial.shader));
        Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Hidden/Safe Haven/Cloud Cookie")), "the cookie shares the sky's cloud noise");
    }

    [Test]
    public void SunAndMoon_FollowTheHour()
    {
        var profile = ScriptableObject.CreateInstance<DayNightProfile>();
        try
        {
            profile.SkyAt(13f, out Vector3 sun, out Vector3 moon, out _);
            Assert.Greater(sun.y, 0.5f, "the sun is high at midday");
            Assert.Less(moon.y, 0f, "the moon is down by day");
            profile.SkyAt(1f, out sun, out moon, out _);
            Assert.Less(sun.y, 0f, "the sun is down at night");
            Assert.Greater(moon.y, 0.3f, "the moon is up at night");
            profile.SkyAt(profile.Sunrise + 0.05f, out sun, out _, out _);
            Assert.AreEqual(0f, sun.y, 0.1f, "the sun rises from the horizon");
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void TheMoon_GoesThroughItsPhases()
    {
        Assert.AreEqual(0.5f, SkyDome.MoonPhase(6f, 12f, 0f), 1e-4f, "full halfway through");
        Assert.AreEqual(0f, SkyDome.MoonPhase(12f, 12f, 0f), 1e-4f, "new again after a whole cycle");
        Assert.AreEqual(0.25f, SkyDome.MoonPhase(0f, 12f, 0.25f), 1e-4f, "starting phase");
    }
}
