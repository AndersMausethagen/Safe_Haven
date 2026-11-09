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
/// playtest. The numbers come from the scanned rocks the Haven already had (BoulderNode: 6k triangles, 0.6 vertices a
/// triangle) and from Docs-free rules: see the assets' notes in AgentScripts/BuildGatheringModels.cs.
/// </summary>
public sealed class ModelBudgetTests
{
    private const string Nodes = "Assets/_Project/Prefabs/Resources";

    private struct Level
    {
        public int Vertices, Triangles;
    }

    private static IEnumerable<GameObject> NodesWithLods() =>
        AssetDatabase.FindAssets("t:Prefab", new[] { Nodes })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(go => go != null && go.GetComponentInChildren<LODGroup>(true) != null);

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
        var over = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                Level lod0 = Measure(group.GetLODs()[0]);
                int budget = TriangleBudget(group);
                if (lod0.Triangles > budget)
                {
                    over.Add($"{node.name}/{group.name}: {lod0.Triangles} triangles (size {group.size:0.0} m, budget {budget})");
                }
            }
        }

        Assert.That(over, Is.Empty, string.Join("\n", over));
    }

    [Test]
    public void SolidModels_HaveFewerVerticesThanTriangles()
    {
        // Vertices split at every UV seam: fragmented UVs (a thousand islands from smart-project on a noisy generated
        // mesh) made Copper Ore 2 vertices a triangle. Scanned rocks are 0.6; leaf cards are allowed more (each quad
        // has its own corners).
        var over = new List<string>();
        foreach (GameObject node in NodesWithLods())
        {
            foreach (LODGroup group in node.GetComponentsInChildren<LODGroup>(true))
            {
                Level lod0 = Measure(group.GetLODs()[0]);
                float limit = IsFoliage(group) ? 1.6f : 1.1f;
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
