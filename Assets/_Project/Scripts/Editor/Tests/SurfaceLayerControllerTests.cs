using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SurfaceLayerControllerTests
{
    private GameObject root;
    private Material material;
    private MeshRenderer child;

    [SetUp]
    public void SetUp()
    {
        material = new Material(Shader.Find("Safe Haven/Layered Lit"));
        material.SetFloat("_L1Amount", 0.25f);
        root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.GetComponent<MeshRenderer>().sharedMaterial = material;
        GameObject other = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        other.transform.SetParent(root.transform);
        child = other.GetComponent<MeshRenderer>();
        child.sharedMaterial = material;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(material);
    }

    private static float BlockValue(Renderer renderer, string property)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        return block.GetFloat(property);
    }

    [Test]
    public void SetAmount_ByName_ReachesEveryRendererWithoutTouchingTheSharedMaterial()
    {
        var controller = root.AddComponent<SurfaceLayerController>();
        Assert.AreEqual(0.25f, controller.GetAmount("moss"), 1e-5f, "Reset reads the material's starting amount");

        float reported = -1f;
        controller.AmountChanged += (index, amount) => { if (index == 0) reported = amount; };
        controller.SetAmount("Moss", 0.8f);
        controller.SetAmount("Rust", 2f);

        Assert.AreEqual(0.8f, reported, 1e-5f);
        Assert.AreEqual(0.8f, BlockValue(root.GetComponent<Renderer>(), "_L1Amount"), 1e-5f);
        Assert.AreEqual(0.8f, BlockValue(child, "_L1Amount"), 1e-5f);
        Assert.AreEqual(1f, BlockValue(child, "_L2Amount"), 1e-5f, "amounts are clamped to 0-1");
        Assert.AreEqual(0.25f, material.GetFloat("_L1Amount"), 1e-5f);
    }

    [Test]
    public void FadeTo_StartsAFadeAndSetAmountCancelsIt()
    {
        var controller = root.AddComponent<SurfaceLayerController>();
        controller.FadeTo("Moss", 1f, 2f);
        Assert.IsTrue(controller.IsFading("Moss"));
        Assert.AreEqual(0.25f, controller.GetAmount(0), 1e-5f);

        controller.SetAmount(0, 0f);
        Assert.IsFalse(controller.IsFading(0));
        controller.FadeTo(0, 0.5f, 0f);
        Assert.AreEqual(0.5f, controller.GetAmount(0), 1e-5f, "zero duration applies immediately");
    }

    [Test]
    public void UnknownLayer_Throws()
    {
        var controller = root.AddComponent<SurfaceLayerController>();
        Assert.AreEqual(-1, controller.IndexOf("Snow"));
        Assert.Throws<ArgumentException>(() => controller.SetAmount("Snow", 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.SetAmount(2, 1f));
    }
}
