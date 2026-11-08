using NUnit.Framework;
using UnityEngine;

/// <summary>Characters soak in the rain under open sky, stay dry under a roof, and dry slowly (fast by a fire); the wet
/// look goes on copies of their materials, never the shared assets.</summary>
public sealed class CharacterWetnessTests
{
    [Test]
    public void Wetness_SoaksInTheOpen_DriesUnderCoverAndByAFire()
    {
        float soaked = CharacterWetness.Advance(0f, 1f, false, false, 30f, 150f, 20f, 15f);
        Assert.AreEqual(0.5f, soaked, 1e-4f, "half soaked after 15 s of downpour");
        Assert.AreEqual(0.25f, CharacterWetness.Advance(0f, 0.5f, false, false, 30f, 150f, 20f, 15f), 1e-4f, "lighter rain soaks slower");
        Assert.Less(CharacterWetness.Advance(soaked, 1f, true, false, 30f, 150f, 20f, 15f), soaked, "under a roof it dries");
        Assert.AreEqual(0f, CharacterWetness.Advance(soaked, 0f, false, true, 30f, 150f, 20f, 10f), 1e-4f, "a fire dries fast");
        Assert.AreEqual(1f, CharacterWetness.Advance(0.95f, 1f, false, false, 30f, 150f, 20f, 60f), 1e-4f, "never past soaked");
    }

    [Test]
    public void Settings_AreOn()
    {
        var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        Assert.IsTrue(settings.WetCharacters);
        Assert.Greater(settings.DrySeconds, settings.SoakSeconds, "drying takes longer than soaking");
        Assert.Less(settings.FireDrySeconds, settings.DrySeconds);
    }
}
