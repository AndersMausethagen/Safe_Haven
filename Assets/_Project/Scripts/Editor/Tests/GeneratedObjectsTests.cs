using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>GeneratedObjects: a World Builder rebuild keeps the objects that came out the same, with their file and save ids.</summary>
public sealed class GeneratedObjectsTests
{
    private const string NodePath = "Assets/_Project/Prefabs/Resources/BrambleThicketNode.prefab";

    private Scene scene;
    private GameObject prefab;

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodePath);
    }

    [TearDown]
    public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

    private GameObject Root(string name)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static Transform Container(Transform parent, string name)
    {
        var container = new GameObject(name).transform;
        container.SetParent(parent, false);
        return container;
    }

    private GameObject Node(Transform parent, Vector3 at, bool withSaveId)
    {
        var node = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        node.transform.localPosition = at;
        if (withSaveId)
        {
            node.GetComponent<SaveableEntity>().AssignNewId();
        }

        return node;
    }

    private static GameObject Made(Transform parent)
    {
        var mesh = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
        mesh.transform.SetParent(parent, false);
        return mesh;
    }

    [Test]
    public void ARebuild_KeepsWhatCameOutTheSame()
    {
        // An Assert, not an Assume: a moved fixture must turn the test red, not quietly skip it.
        Assert.IsTrue(prefab != null && prefab.GetComponent<SaveableEntity>() != null, $"A saveable prefab to build with at {NodePath}");
        Transform old = Root("Generated").transform;
        Transform oldBushes = Container(old, "Bushes");
        GameObject same = Node(oldBushes, new Vector3(1f, 0f, 1f), true);
        GameObject moved = Node(oldBushes, new Vector3(2f, 0f, 2f), true);
        GameObject gone = Node(oldBushes, new Vector3(3f, 0f, 3f), true);
        GameObject oldWater = Made(old);
        string saveId = same.GetComponent<SaveableEntity>().Id;

        // The rebuild: the same bush, one moved, one gone, one new; the water made again (as procedural things are).
        Transform fresh = Root("Generated (fresh)").transform;
        Transform freshBushes = Container(fresh, "Bushes");
        Node(freshBushes, new Vector3(1f, 0f, 1f), false);
        GameObject movedNow = Node(freshBushes, new Vector3(2f, 0f, 2.5f), false);
        GameObject added = Node(freshBushes, new Vector3(4f, 0f, 4f), false);
        GameObject freshWater = Made(fresh);

        Assert.IsTrue(GeneratedObjects.CanReconcile(old, fresh));
        GeneratedObjects.Result result = GeneratedObjects.Reconcile(old, fresh);

        Assert.IsTrue(fresh == null, "The fresh root goes");
        Assert.IsTrue(same != null && same.transform.parent == oldBushes, "The unchanged bush is the old object, in the old container");
        Assert.AreEqual(saveId, same.GetComponent<SaveableEntity>().Id, "with its save id");
        Assert.IsTrue(moved == null && gone == null && oldWater == null, "What changed or went is removed");
        Assert.AreSame(oldBushes, movedNow.transform.parent, "The moved one comes in fresh");
        Assert.AreSame(oldBushes, added.transform.parent);
        Assert.AreSame(old, freshWater.transform.parent, "Made-again things are always replaced");
        CollectionAssert.AreEqual(new[] { same.transform, movedNow.transform, added.transform }, new[] { oldBushes.GetChild(0), oldBushes.GetChild(1), oldBushes.GetChild(2) },
            "In the rebuild's order");
        Assert.AreEqual(1, result.Kept);
        Assert.AreEqual(3, result.Added);
        Assert.AreEqual(3, result.Removed);
    }

    [Test]
    public void AnOverrideMakesItAnotherObject()
    {
        Assert.IsNotNull(prefab, $"A prefab to build with at {NodePath}");
        Transform root = Root("Generated").transform;
        GameObject a = Node(root, Vector3.one, false);
        GameObject b = Node(root, Vector3.one, false);
        Assert.AreEqual(GeneratedObjects.Signature(a.transform), GeneratedObjects.Signature(b.transform));
        b.transform.localScale = Vector3.one * 1.2f;
        Assert.AreNotEqual(GeneratedObjects.Signature(a.transform), GeneratedObjects.Signature(b.transform), "A different size is a different object");
        GameObject withId = Node(root, Vector3.one, true);
        Assert.AreEqual(GeneratedObjects.Signature(a.transform), GeneratedObjects.Signature(withId.transform), "The save id alone doesn't make it different");
    }
}
