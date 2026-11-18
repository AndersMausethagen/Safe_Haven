using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gathering nodes stay inside the model budgets (2026-11-09: generated models arrived at ~10k triangles whatever their
/// size, with twice as many vertices as triangles from fragmented UVs, LODs that barely reduced, 2,000-triangle
/// colliders and 2048 textures on half-metre objects; together 22% of the Haven's triangles on screen). Every node
/// prefab with an LODGroup is measured, so a new or regenerated model that breaks a budget fails here, not in a
/// playtest. The numbers follow best practice measured in the game's own view (ScreenBudget: triangles no smaller
/// than the 2x2 pixel quads GPUs shade, ~1-2 texels a screen pixel, convex hulls PhysX can cook, compressed
/// non-readable data) and the scanned rocks the Haven already had (BoulderNode: 6k triangles, 0.6 vertices a
/// triangle, 32-50 pixels a triangle at the default view).
/// </summary>
public sealed class ModelBudgetTests
{
    private const string Nodes = "Assets/_Project/Prefabs/Resources";

    private struct Level
    {
        public int Vertices, Triangles;
    }

    /// <summary>Looks whose budget the import had to raise above the view's because decimating further broke the shape
    /// (measured: surface deviation and silhouette, Tools/Blender/ingest_generated.py), from the
    /// Art/Gathering/*/*_Budget.json it writes. Their triangles are what the shape needs; they are reported, not failed.</summary>
    private static HashSet<string> ShapeLimitedLooks() => LooksWhere(@"""shape_limited"":\s*true");

    /// <summary>Looks whose region unwrap ran away and took smart project (more UV islands, so more vertices).</summary>
    private static HashSet<string> SmartUnwrappedLooks() => LooksWhere(@"""unwrap"":\s*""smart""");

    // Looks in the import's *_Budget.json files whose entry matches pattern (an entry has no nested braces).
    private static HashSet<string> LooksWhere(string pattern)
    {
        var looks = new HashSet<string>();
        foreach (string path in System.IO.Directory.GetFiles("Assets/_Project/Art/Gathering", "*_Budget.json", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(path);
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                         text, @"""([^""{}]+)"":\s*\{([^{}]*)\}"))
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(match.Groups[2].Value, pattern))
                {
                    looks.Add(match.Groups[1].Value);
                }
            }
        }

        return looks;
    }

    /// <summary>The Haven's older scanned-pack rocks: LOD0 (6-8k triangles) is denser than the top-down view needs now
    /// that LOD0 draws throughout it (2026-11-09). Known debt, listed so it isn't forgotten: optimise them with the same
    /// shape check, then take them off this list.</summary>
    private static readonly HashSet<string> KnownDense = new HashSet<string> { "FallenLogNode", "GreatBoulderNode", "OldStumpNode" };

    // Every budget check below runs over these: if the folder moved or the nodes lost their LOD groups, each would pass on
    // nothing, so finding none is a failure of its own.
    private static List<GameObject> NodesWithLods()
    {
        List<GameObject> nodes = AssetDatabase.FindAssets("t:Prefab", new[] { Nodes })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(go => go != null && go.GetComponentInChildren<LODGroup>(true) != null)
            .ToList();
        Assert.Greater(nodes.Count, 0, $"no node prefab with a LOD group under {Nodes}");
        return nodes;
    }

    private static Level Measure(LOD lod)
    {
        var level = new Level();
        foreach (Renderer renderer in lod.renderers)
        {
            if (renderer == null || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                continue;
            }

            Mesh mesh = filter.sharedMesh;
            level.Vertices += mesh.vertexCount;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                level.Triangles += (int)mesh.GetIndexCount(s) / 3;
            }
        }

        return level;
    }

    private static bool IsFoliage(LODGroup group) =>
        group.GetLODs()[0].renderers.Any(r => r != null && r.sharedMaterials.Any(m => m != null && m.shader.name.Contains("Tree Wind")));

    /// <summary>LOD0 triangles allowed by how big the thing is: small gatherables, boulders, logs; leaf cards a bit more.</summary>
    private static int TriangleBudget(LODGroup group)
    {
        float size = group.size;
        int budget = size < 1f ? 2500 : size < 3f ? 6000 : 8000;
        return IsFoliage(group) ? budget * 3 / 2 : budget;
    }

    [Test]
    public void LevelZero_StaysInsideTheTriangleBudgetForItsSize()
    {
        HashSet<string> shapeLimited = ShapeLimitedLooks();
        var over = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                Level lod0 = Measure(group.GetLODs()[0]);
                int budget = TriangleBudget(group);
                if (lod0.Triangles > budget && !shapeLimited.Contains(group.name))
                {
                    over.Add($"{node.name}/{group.name}: {lod0.Triangles} triangles (size {group.size:0.0} m, budget {budget})");
                }
            }
        }

        Assert.That(over, Is.Empty, string.Join("\n", over));
    }

    [Test]
    public void AtTheDefaultView_TrianglesAreNotTooSmallOnScreen()
    {
        // The level Unity picks at the default top-down view (ScreenBudget: 20 m, 147 pixels a metre at 2160p, lodBias 2)
        // draws at least 25 pixels per visible triangle at 2160p. The builder aims at 40 (10 at 1080p, the 2x2 quad
        // line); the scanned rocks measure 32-50. Before the LOD rework the mushroom patch drew 1.7 and the turnips 6.8.
        const float floor = 25f;
        HashSet<string> shapeLimited = ShapeLimitedLooks();
        var small = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                Renderer first = lods[0].renderers.FirstOrDefault(r => r != null);
                if (first == null || !first.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 scale = first.transform.lossyScale;
                bounds.size = Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float relative = ScreenBudget.DefaultRelativeHeight(bounds);
                int level = System.Array.FindIndex(lods, l => relative >= l.screenRelativeTransitionHeight);
                if (level < 0)
                {
                    small.Add($"{node.name}/{group.name}: culled at the default view (relative height {relative:0.000})");
                    continue;
                }

                int triangles = Measure(lods[level]).Triangles;
                float pixels = ScreenBudget.PixelsPerTriangle(bounds, triangles, relative);
                // Leaf cards are big triangles whose cost is overdraw (cropped to the leaf), not size: not measured here.
                if (pixels < floor && !shapeLimited.Contains(group.name) && !IsFoliage(group) && !KnownDense.Contains(node.name))
                {
                    small.Add($"{node.name}/{group.name}: LOD{level} {triangles} triangles, {pixels:0.0} pixels each at 2160p");
                }
            }
        }

        Assert.That(small, Is.Empty, string.Join("\n", small));
    }

    [Test]
    public void InTheTopDownView_EveryNodeDrawsLevelZero()
    {
        // Nothing in the top-down view shows a lighter level (the user: "nothing in the top down camera view should have
        // a lower LOD"): LOD0 holds down to the farthest ground at full zoom-out.
        var early = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                Renderer first = lods[0].renderers.FirstOrDefault(r => r != null);
                if (first == null || !first.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }

                // All of LOD0's parts (a tree is a trunk and a crown), as the builders size it.
                Bounds bounds = default;
                bool any = false;
                foreach (Renderer part in lods[0].renderers.Where(r => r != null))
                {
                    if (!part.TryGetComponent(out MeshFilter partFilter) || partFilter.sharedMesh == null)
                    {
                        continue;
                    }

                    Bounds b = partFilter.sharedMesh.bounds;
                    Vector3 s = part.transform.lossyScale;
                    b.size = Vector3.Scale(b.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                    if (any)
                    {
                        bounds.Encapsulate(b);
                    }
                    else
                    {
                        bounds = b;
                        any = true;
                    }
                }

                float farthest = ScreenBudget.FarthestTopDownRelativeHeight(bounds);
                if (lods[0].screenRelativeTransitionHeight > farthest * 1.02f)
                {
                    early.Add($"{node.name}/{group.name}: LOD0 ends at {lods[0].screenRelativeTransitionHeight:0.000}, the top-down view reaches {farthest:0.000}");
                }
            }
        }

        Assert.That(early, Is.Empty, string.Join(System.Environment.NewLine, early));
    }

    [Test]
    public void SolidModels_HaveFewerVerticesThanTriangles()
    {
        HashSet<string> smart = SmartUnwrappedLooks();
        // Vertices split at every UV seam: fragmented UVs (a thousand islands from smart-project on a noisy generated
        // mesh) made Copper Ore 2 vertices a triangle. Scanned rocks are 0.6; leaf cards are allowed more (each quad
        // has its own corners).
        var over = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                Level lod0 = Measure(group.GetLODs()[0]);
                // Small clusters of separate pieces (five stones, a patch of mushrooms) need at least an island a piece,
                // so under a metre 1.35x; at 1-2k vertices that is cheap.
                float limit = IsFoliage(group) ? 1.6f : smart.Contains(group.name) ? 2.2f : group.size < 1f ? 1.35f : 1.1f;
                if (lod0.Triangles > 0 && lod0.Vertices > lod0.Triangles * limit)
                {
                    over.Add($"{node.name}/{group.name}: {lod0.Vertices} vertices for {lod0.Triangles} triangles (limit {limit}x)");
                }
            }
        }

        Assert.That(over, Is.Empty, string.Join("\n", over));
    }

    [Test]
    public void LevelsOfDetail_ReallyReduce()
    {
        // Each level no heavier than the one before, and the last at most 60% of LOD0 (thinning only the leaf cards
        // left the Berry Bush's LOD2 at 86%).
        var flat = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                Level[] levels = group.GetLODs().Select(Measure).ToArray();
                if (levels.Length < 2)
                {
                    flat.Add($"{node.name}/{group.name}: only one level");
                    continue;
                }

                for (int i = 1; i < levels.Length; i++)
                {
                    if (levels[i].Triangles > levels[i - 1].Triangles)
                    {
                        flat.Add($"{node.name}/{group.name}: LOD{i} {levels[i].Triangles} > LOD{i - 1} {levels[i - 1].Triangles}");
                    }
                }

                if (levels[levels.Length - 1].Triangles > levels[0].Triangles * 0.6f)
                {
                    flat.Add($"{node.name}/{group.name}: last level {levels[levels.Length - 1].Triangles} of {levels[0].Triangles}");
                }
            }
        }

        Assert.That(flat, Is.Empty, string.Join("\n", flat));
    }

    [Test]
    public void Colliders_AreSmallConvexHullsOrPrimitives()
    {
        // PhysX cooks a convex hull to at most 255 polygons, so a bigger source mesh is only load time and memory
        // (the generated nodes' hulls were 2,000 triangles). Counted in Unity vertices (flat faces split them).
        var heavy = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (MeshCollider collider in node.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == null)
                {
                    continue;
                }

                if (!collider.convex || collider.sharedMesh.vertexCount > 600)
                {
                    heavy.Add($"{node.name}/{collider.name}: convex {collider.convex}, {collider.sharedMesh.vertexCount} vertices");
                }
            }
        }

        Assert.That(heavy, Is.Empty, string.Join("\n", heavy));
    }

    [Test]
    public void Meshes_AreNotReadable_AndUse16BitIndices()
    {
        // A readable mesh keeps a CPU copy (twice the memory); 32-bit indices are only for meshes over 65k vertices.
        var wrong = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (MeshFilter filter in node.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && (mesh.isReadable || mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32))
                {
                    wrong.Add($"{node.name}/{filter.name}: {mesh.name} readable {mesh.isReadable}, {mesh.indexFormat}");
                }
            }
        }

        Assert.That(wrong.Distinct().ToList(), Is.Empty, string.Join("\n", wrong.Distinct()));
    }

    [Test]
    public void Textures_FitTheObjectsSize()
    {
        // About 700-1500 texels a metre: 1024 for things under a metre, 2048 at most above; the slow-changing maps
        // (ORM, height, blend mask, emission) at most the colour map's size.
        var big = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                int limit = group.size < 1f ? 1024 : 2048;
                foreach (Material material in group.GetLODs()[0].renderers.Where(r => r != null).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
                {
                    Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    if (baseMap == null)
                    {
                        continue;
                    }

                    if (baseMap.width > limit)
                    {
                        big.Add($"{node.name}: {material.name} colour map {baseMap.width} (size {group.size:0.0} m, limit {limit})");
                    }

                    foreach (string map in new[] { "_ORMMap", "_ParallaxMap", "_BlendMask", "_EmissionMap" })
                    {
                        Texture texture = material.HasProperty(map) ? material.GetTexture(map) : null;
                        if (texture != null && texture.width > baseMap.width)
                        {
                            big.Add($"{node.name}: {material.name} {map} {texture.width} > colour map {baseMap.width}");
                        }
                    }
                }
            }
        }

        Assert.That(big.Distinct().ToList(), Is.Empty, string.Join("\n", big.Distinct()));
    }
}
