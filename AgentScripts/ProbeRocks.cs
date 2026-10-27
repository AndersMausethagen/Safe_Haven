using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Reports how the Poly Haven rocks imported: shader, maps, LOD Group, collider and size
/// (`unity command run_script --file AgentScripts/ProbeRocks.cs --entry ProbeRocks.Report`).</summary>
public static class ProbeRocks
{
    public static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Models/PolyHaven" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            var lod = model.GetComponent<LODGroup>();
            var collider = model.GetComponentInChildren<MeshCollider>();
            Bounds bounds = renderer != null ? renderer.bounds : default;
            sb.Append($"{System.IO.Path.GetFileNameWithoutExtension(path)}: shader={(material != null ? material.shader.name : "none")} " +
                      $"base={(material != null && material.GetTexture("_BaseMap") != null)} normal={(material != null && material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null)} " +
                      $"orm={(material != null && material.HasProperty("_ORMMap") && material.GetTexture("_ORMMap") != null)} moss={(material != null && material.HasProperty("_L1Amount") ? material.GetFloat("_L1Amount") : -1):0.00} " +
                      $"lods={(lod != null ? lod.lodCount : 0)} collider={(collider != null ? collider.sharedMesh.triangles.Length / 3 + (collider.convex ? " convex" : "") : "none")} " +
                      $"size={bounds.size} tris={model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh.triangles.Length / 3).FirstOrDefault()}\n");
        }

        return sb.ToString();
    }
}
