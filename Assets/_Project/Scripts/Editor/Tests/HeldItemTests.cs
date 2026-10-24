using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HeldItemTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown() => TestCleanup.Destroy(created);

    [Test]
    public void HeldModel_FallsBackToTheWorldPrefab()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(item);
        var world = new GameObject("world");
        created.Add(world);
        var serialized = new SerializedObject(item);
        serialized.FindProperty("prefab").objectReferenceValue = world;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreSame(world, item.HeldModel);
        Assert.AreEqual(1f, item.HeldScale);
    }

    [Test]
    public void HeldModel_IsOnlyAVisual()
    {
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(model);
        model.AddComponent<Rigidbody>();
        model.AddComponent<GrassInteractor>();
        HeldItemView.StripToVisual(model);
        Assert.IsNull(model.GetComponent<Collider>());
        Assert.IsNull(model.GetComponent<Rigidbody>());
        Assert.IsNull(model.GetComponent<GrassInteractor>());
        Assert.IsNotNull(model.GetComponent<MeshRenderer>());
        Assert.IsNotNull(model.GetComponent<MeshFilter>());
    }

    [Test]
    public void PlayerGrip_SitsInThePalmWithTheHandleAcrossIt()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(HeldItemGrips.PlayerPrefab);
        Pose grip = HeldItemGrips.ComputeGrip(player);
        Assert.Less(grip.position.magnitude, 0.15f, "within the hand");
        Assert.Greater(grip.position.magnitude, 0.01f, "in the palm, not at the wrist");
        Assert.AreNotEqual(Quaternion.identity, grip.rotation);
        Assert.IsNotNull(player.GetComponent<HeldItemView>(), "the player shows what it holds");
    }

    [Test]
    public void WeaponsAndTools_HaveSomethingToHold()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && (item.IsWeapon || item.IsTool))
            {
                Assert.IsNotNull(item.HeldModel, $"{item.name} has no model to show in the hand");
            }
        }
    }
}
