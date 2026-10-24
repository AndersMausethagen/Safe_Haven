using NUnit.Framework;
using UnityEngine;

public sealed class TreeWindTests
{
    private GameObject zoneObject;

    [TearDown]
    public void TearDown()
    {
        if (zoneObject != null) Object.DestroyImmediate(zoneObject);
    }

    private WindZone CreateZone(float yaw, WindZoneMode mode)
    {
        zoneObject = new GameObject("__TreeWindZone");
        zoneObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var zone = zoneObject.AddComponent<WindZone>();
        zone.mode = mode;
        zone.windMain = 2f;
        zone.windPulseMagnitude = 0.6f;
        return zone;
    }

    [Test]
    public void WithoutWindZone_UsesMossWindSettings()
    {
        var (wind, gusts, tree) = MossWind.Evaluate(90f, 0.05f, 1.2f, 0.3f, null);

        Assert.That(wind.x, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(wind.y, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(wind.z, Is.EqualTo(0.05f).Within(1e-6f));
        Assert.That(wind.w, Is.EqualTo(1.2f));
        Assert.That(gusts, Is.EqualTo(0.3f));
        Assert.That(tree, Is.EqualTo(1f));
    }

    [Test]
    public void DirectionalWindZone_DrivesDirectionStrengthAndGusts()
    {
        WindZone zone = CreateZone(180f, WindZoneMode.Directional);

        var (wind, gusts, tree) = MossWind.Evaluate(90f, 0.05f, 1.2f, 0.3f, zone);

        Assert.That(wind.x, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(wind.y, Is.EqualTo(-1f).Within(1e-4f));
        Assert.That(wind.z, Is.EqualTo(0.1f).Within(1e-6f), "moss displacement scales with Main");
        Assert.That(wind.w, Is.EqualTo(1.2f), "speed stays a Moss Wind setting");
        Assert.That(gusts, Is.EqualTo(0.6f).Within(1e-6f));
        Assert.That(tree, Is.EqualTo(2f));
    }

    [Test]
    public void SphericalWindZone_IsIgnored()
    {
        WindZone zone = CreateZone(180f, WindZoneMode.Spherical);

        var (wind, _, tree) = MossWind.Evaluate(90f, 0.05f, 1.2f, 0.3f, zone);

        Assert.That(wind.x, Is.EqualTo(1f).Within(1e-4f));
        Assert.That(tree, Is.EqualTo(1f));
    }

    [Test]
    public void GltfLeafMaterial_CopiesToClippedTwoSidedWindMaterial()
    {
        Shader gltf = Shader.Find("Shader Graphs/glTF-pbrMetallicRoughness");
        Shader wind = Shader.Find(TreeWindMaterials.ShaderName);
        Assert.That(gltf, Is.Not.Null);
        Assert.That(wind, Is.Not.Null);

        var texture = new Texture2D(4, 4);
        var source = new Material(gltf);
        var target = new Material(wind);
        try
        {
            source.SetTexture("baseColorTexture", texture);
            source.SetColor("baseColorFactor", new Color(0.5f, 0.6f, 0.7f, 1f));
            source.SetFloat("roughnessFactor", 0.6f);
            source.SetFloat("metallicFactor", 0f);
            source.SetFloat("alphaCutoff", 0.4f);
            source.SetFloat("_Cull", 0f);
            source.EnableKeyword("_ALPHATEST_ON");

            TreeWindMaterials.CopyGltf(source, target);

            Assert.That(target.GetTexture("_BaseMap"), Is.SameAs(texture));
            Assert.That(target.GetColor("_BaseColor"), Is.EqualTo(new Color(0.5f, 0.6f, 0.7f, 1f)));
            Assert.That(target.GetFloat("_Roughness"), Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(target.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
            Assert.That(target.GetFloat("_Cutoff"), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(target.GetFloat("_Cull"), Is.EqualTo(0f));
            Assert.That(target.GetFloat("_OcclusionStrength"), Is.EqualTo(0f), "no ORM map, no occlusion");
            Assert.That(target.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }

    [TestCase("Assets/_Project/Models/Generated/Vegetation/oak/oak_Leaves.glb", ExpectedResult = true)]
    [TestCase("Assets/_Project/Models/Generated/props/crate.glb", ExpectedResult = false)]
    [TestCase("Assets/_Project/Models/Generated/Vegetation/oak/oak_BarkHeight.png", ExpectedResult = false)]
    public bool OnlyVegetationGlbsAreConverted(string path) => TreeWindMaterials.IsTreeGlb(path);

    [Test]
    public void VariantSitsNextToTheGlb()
    {
        Assert.That(TreeWindMaterials.VariantPath("Assets/Trees/oak/oak_Trunk.glb"),
            Is.EqualTo("Assets/Trees/oak/oak_Trunk_Wind.prefab"));
    }
}
