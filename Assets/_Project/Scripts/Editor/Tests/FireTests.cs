using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Fire: the flames are the Fire shader, the lights warm yellow and flickering.</summary>
public sealed class FireTests
{
    [Test]
    public void Fires_AreDrawnByTheFireShader()
    {
        Shader fire = Shader.Find("Safe Haven/Fire");
        Assert.IsNotNull(fire);
        Assert.IsFalse(ShaderUtil.ShaderHasError(fire));
        var flames = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationFire.mat");
        Assert.AreEqual(fire, flames.shader, "Torches, campfires, the furnace and fire arrows burn with it.");
        Assert.IsFalse(flames.IsKeywordEnabled("_FIRE_UV"), "On their sphere meshes.");
        var particles = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Effects/Bonfire Flame.mat");
        Assert.AreEqual(fire, particles.shader);
        Assert.IsTrue(particles.IsKeywordEnabled("_FIRE_UV"), "Particles shape the flame in their quads.");
        Assert.IsNotNull(Resources.Load<Material>("Fire Sparks"));
    }

    [Test]
    public void FireLights_AreWarmYellow_AndFlicker()
    {
        foreach (string path in new[] { "Assets/_Project/Prefabs/Structures/Campfire.prefab", "Assets/_Project/Prefabs/Structures/Torch.prefab",
                     "Assets/_Project/Prefabs/Items/Models/HandTorchModel.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            LightFlicker flicker = prefab.GetComponentInChildren<LightFlicker>(true);
            Assert.IsNotNull(flicker, path);
            Color colour = flicker.GetComponent<Light>().color;
            Assert.Greater(colour.g, 0.7f, $"{path}: yellow, not orange");
            Assert.Less(colour.b, 0.6f, $"{path}: warm");
        }

        float low = 1f, high = -1f;
        for (float t = 0f; t < 50f; t += 0.05f)
        {
            float wave = LightFlicker.Wave(t, 3f);
            low = Mathf.Min(low, wave);
            high = Mathf.Max(high, wave);
        }

        Assert.Less(low, -0.2f, "It dims...");
        Assert.Greater(high, 0.2f, "...and flares.");
    }
}
