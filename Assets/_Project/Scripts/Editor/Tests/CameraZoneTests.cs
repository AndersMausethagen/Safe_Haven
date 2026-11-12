using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Camera Zone: how deep in its box a point is (the soft edge), turned with the object.</summary>
public sealed class CameraZoneTests
{
    [Test]
    public void TheZone_FadesInOverItsSoftEdge()
    {
        GameObject go = EditorUtility.CreateGameObjectWithHideFlags("Zone", HideFlags.HideAndDontSave, typeof(CameraZone));
        try
        {
            var zone = go.GetComponent<CameraZone>();
            zone.Size = new Vector3(20f, 10f, 20f);
            zone.SoftEdge = 4f;
            Assert.AreEqual(1f, zone.Inside(Vector3.zero), 0.001f, "deep inside");
            Assert.AreEqual(0.5f, zone.Inside(new Vector3(8f, 0f, 0f)), 0.001f, "2 m in from the edge, half way");
            Assert.AreEqual(0f, zone.Inside(new Vector3(11f, 0f, 0f)), 0.001f, "outside");
            Assert.AreEqual(0f, zone.Inside(new Vector3(0f, 6f, 0f)), 0.001f, "above it");

            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            zone.Size = new Vector3(40f, 10f, 10f);
            Assert.Greater(zone.Inside(new Vector3(0f, 0f, 15f)), 0f, "turned with the object: its long side now runs north");
            Assert.AreEqual(0f, zone.Inside(new Vector3(15f, 0f, 0f)), 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
