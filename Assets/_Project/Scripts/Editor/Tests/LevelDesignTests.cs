using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelDesignTests
{
    // Far from the real level so its ground and objects do not get in the way.
    private static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);
    private const float GroundHeight = 2f;
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown() => TestCleanup.Destroy(created);

    private Terrain FlatTerrain(int layers = 0)
    {
        var data = new TerrainData { heightmapResolution = 33, alphamapResolution = 64 };
        data.size = new Vector3(40f, 10f, 40f);
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
                heights[z, x] = GroundHeight / 10f;
        data.SetHeights(0, 0, heights);
        if (layers > 0)
        {
            data.terrainLayers = Enumerable.Range(0, layers).Select(i => new TerrainLayer { name = "layer " + i }).ToArray();
            created.AddRange(data.terrainLayers);
        }

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.transform.position = Origin;
        created.Add(go);
        created.Add(data);
        return go.GetComponent<Terrain>();
    }

    private GameObject Cube(Vector3 position)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = position;
        created.Add(cube);
        return cube;
    }

    [Test]
    public void Drop_SetsAnObjectDownByTheBottomOfItsMeshes()
    {
        FlatTerrain();
        GameObject cube = Cube(Origin + new Vector3(20f, 9f, 20f));
        cube.transform.localScale = new Vector3(1f, 3f, 1f); // pivot 1.5 m above its bottom
        Assert.IsTrue(GroundPlacement.Drop(cube.transform, false, 0f));
        Assert.AreEqual(GroundHeight, cube.GetComponent<Renderer>().bounds.min.y, 0.02f);
    }

    [Test]
    public void ScatterBrush_KeepsItsSpacingAndPutsThingsOnTheGround()
    {
        FlatTerrain();
        var rocks = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Forest/Prefabs/Boulder A.prefab");
        Assert.IsNotNull(rocks, "the forest kit's boulder");
        var palette = ScriptableObject.CreateInstance<ScatterPalette>();
        palette.name = "test rocks";
        palette.spacing = 3f;
        palette.entries.Add(new ScatterPalette.Entry { prefab = rocks, sink = 0f, alignToSlope = 0f });
        created.Add(palette);
        var folder = new GameObject("scatter test");
        created.Add(folder);

        List<GameObject> placed = ScatterBrush.Paint(palette, Origin + new Vector3(20f, 0f, 20f), 8f, 60, folder.transform, new System.Random(3));
        Assert.Greater(placed.Count, 3);
        Assert.IsTrue(placed.All(p => p.transform.parent == folder.transform));
        for (int i = 0; i < placed.Count; i++)
        {
            Assert.AreEqual(GroundHeight, placed[i].GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y), 0.05f, "set on the ground");
            for (int j = i + 1; j < placed.Count; j++)
            {
                Vector3 d = placed[i].transform.position - placed[j].transform.position;
                Assert.GreaterOrEqual(new Vector2(d.x, d.z).magnitude, palette.spacing - 1e-3f);
            }
        }

        int more = ScatterBrush.Paint(palette, Origin + new Vector3(20f, 0f, 20f), 8f, 60, folder.transform, new System.Random(4)).Count;
        Assert.Less(more, placed.Count, "painting the same place again only fills the gaps");
        Assert.AreEqual(folder.transform.childCount, ScatterBrush.Erase(folder.transform, palette, Origin + new Vector3(20f, 0f, 20f), 20f));
        Assert.AreEqual(0, folder.transform.childCount);
    }

    [Test]
    public void LevelCheck_FindsFloatingObjectsAndLeftoverTerrains_AndFixesThem()
    {
        FlatTerrain();
        GameObject floating = Cube(Origin + new Vector3(10f, GroundHeight + 3f, 10f));
        floating.AddComponent<Interactable>();
        GameObject grounded = Cube(Origin + new Vector3(30f, GroundHeight + 0.45f, 30f));
        grounded.AddComponent<Interactable>();
        var empty = new GameObject("empty terrain");
        empty.AddComponent<Terrain>();
        created.Add(empty);

        List<LevelIssue> issues = LevelCheck.Run(SceneManager.GetActiveScene());
        LevelIssue floats = issues.SingleOrDefault(i => i.Target == floating && i.Kind == "Floating");
        Assert.IsNotNull(floats);
        Assert.IsFalse(issues.Any(i => i.Target == grounded && (i.Kind == "Floating" || i.Kind == "Buried" || i.Kind == "Off the terrain")));
        Assert.IsTrue(issues.Any(i => i.Target == empty && i.Kind == "Leftover"));

        floats.Fix();
        Assert.IsFalse(LevelCheck.Run(SceneManager.GetActiveScene()).Any(i => i.Target == floating && i.Kind == "Floating"));
        Assert.Greater(LevelCheck.Census(SceneManager.GetActiveScene())["Interactables"], 1);
    }
}
