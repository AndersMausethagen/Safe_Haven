using NUnit.Framework;
using UnityEngine;

public sealed class TerrainPuddlesTests
{
    private static Vector3 WetPoint()
    {
        for (float x = 0f; x < 16f; x += 0.2f)
        {
            for (float z = 0f; z < 16f; z += 0.2f)
            {
                var p = new Vector3(x, 1000f, z);
                if (TerrainPuddles.Coverage(p, 1f, 1f) > 0.8f)
                    return p;
            }
        }
        Assert.Fail("Expected a full puddle in the shader's world-space pattern.");
        return Vector3.zero;
    }

    [Test]
    public void DryGroundAndSteepSlopes_NeverContainPuddles()
    {
        Vector3 p = WetPoint();
        Assert.AreEqual(0f, TerrainPuddles.Coverage(p, 1f, 0f));
        Assert.AreEqual(0f, TerrainPuddles.Coverage(p, 0.6f, 1f));
        Assert.Greater(TerrainPuddles.Coverage(p, 1f, 1f), TerrainPuddles.ContactCoverage);
    }

    [Test]
    public void PuddlesGrowWithWetness_AndDoNotCoverAllWetGround()
    {
        bool foundDry = false;
        for (float x = -5f; x < 5f; x += 0.15f)
        {
            var p = new Vector3(x, 0f, x * 0.7f);
            float previous = 0f;
            for (float wetness = 0f; wetness <= 1f; wetness += 0.1f)
            {
                float coverage = TerrainPuddles.Coverage(p, 1f, wetness);
                Assert.That(coverage, Is.InRange(previous, 1f));
                previous = coverage;
            }
            foundDry |= TerrainPuddles.Coverage(p, 1f, 1f) == 0f;
        }
        Assert.IsTrue(foundDry, "wet ground outside the visible mask stays a normal footstep");
    }

    [Test]
    public void RainPuddle_SelectsWaterSound_AndFloorsBlockIt()
    {
        float oldWetness = Shader.GetGlobalFloat("_SafeHavenWetness");
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(16f, 2f, 16f) };
        GameObject ground = Terrain.CreateTerrainGameObject(data);
        Terrain terrain = ground.GetComponent<Terrain>();
        var material = new Material(Shader.Find("Safe Haven/Terrain (World)"));
        GameObject floor = null;
        var audioObject = new GameObject("Puddle test audio");
        try
        {
            audioObject.AddComponent<AudioManager>().Initialise(
                Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName));
            ground.transform.position = Vector3.up * 1000f;
            terrain.materialTemplate = material;
            Shader.SetGlobalFloat("_SafeHavenWetness", 1f);
            Vector3 p = WetPoint();
            Physics.SyncTransforms();
            Assert.IsTrue(TerrainPuddles.TryFind(p, out float height));
            Assert.That(height, Is.EqualTo(p.y + 0.03f).Within(0.001f));
            Assert.AreSame(AudioManager.Sounds.FootstepWater, PlayerAudio.SurfaceSound(p));
            Assert.IsTrue(AudioManager.Sounds.FootstepWater.HasClips);

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = p + Vector3.up * 0.1f;
            floor.transform.localScale = new Vector3(1f, 0.1f, 1f);
            Physics.SyncTransforms();
            Assert.IsFalse(TerrainPuddles.TryFind(p, out _));
            Assert.AreNotSame(AudioManager.Sounds.FootstepWater, PlayerAudio.SurfaceSound(p));
            Object.DestroyImmediate(floor);
            floor = null;

            Shader.SetGlobalFloat("_SafeHavenWetness", 0f);
            Assert.IsFalse(TerrainPuddles.TryFind(p, out _));
            Shader.SetGlobalFloat("_SafeHavenWetness", 1f);
            terrain.materialTemplate = null;
            Assert.IsFalse(TerrainPuddles.Contains(terrain, p), "ordinary terrain shaders have no rain puddles");
        }
        finally
        {
            Shader.SetGlobalFloat("_SafeHavenWetness", oldWetness);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(audioObject);
        }
    }
}
