using NUnit.Framework;
using UnityEngine;

/// <summary>Drips: found at the bottom edge of a sloping roof, landing on the ground below, and going on (thinner)
/// while the world is wet after the rain, never in snow.</summary>
public sealed class RainDripsTests
{
    [Test]
    public void ARoofDrips_FromItsLowerEdge()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            roof.transform.position = new Vector3(0f, 4f, 0f);
            roof.transform.localScale = new Vector3(4f, 0.1f, 4f);
            roof.transform.rotation = Quaternion.Euler(0f, 0f, 30f); // falls away toward +x... or -x: either edge is fine
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(new Vector3(0f, 20f, 0f), Vector3.down, out RaycastHit hit, 40f));
            Assert.AreEqual(roof.GetComponent<Collider>(), hit.collider);
            Assert.IsTrue(RainDrips.TryEdge(hit, out RainDrips.Spot spot), "walks down to the edge");
            Assert.Less(spot.from.y, hit.point.y, "the edge is lower than where it landed on the roof");
            Assert.Greater(Mathf.Abs(spot.from.x), 1.4f, "at the roof's lower edge");
            Assert.AreEqual(0f, spot.landing.y, 0.05f, "lands on the ground");
        }
        finally
        {
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(roof);
        }
    }

    [Test]
    public void Dripping_FollowsTheRain_ThenTheWet()
    {
        Assert.AreEqual(1f, RainDrips.Dripping(1f, 1f, false));
        Assert.AreEqual(0.35f, RainDrips.Dripping(0f, 1f, false), 1e-4f, "a third after the rain while still wet");
        Assert.AreEqual(0f, RainDrips.Dripping(0f, 0f, false));
        Assert.AreEqual(0f, RainDrips.Dripping(1f, 1f, true), "snow doesn't drip");
    }
}
