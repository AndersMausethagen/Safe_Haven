using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Checks the baked conifers and the palettes that use them
/// (`unity command run_script --file AgentScripts/ProbeTrees.cs --entry ProbeTrees.Report`).</summary>
public static class ProbeTrees
{
    public static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (string name in new[] { "Norway Spruce A", "Norway Spruce B", "Norway Spruce C", "Scots Pine A", "Scots Pine B", "Scots Pine C" })
        {
            GameObject prefab = WorldAssets.Prefab(name);
            if (prefab == null)
            {
                sb.Append(name + ": MISSING\n");
                continue;
            }

            var lod = prefab.GetComponentInChildren<LODGroup>();
            var capsule = prefab.GetComponent<CapsuleCollider>();
            MeshRenderer lod0 = prefab.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.EndsWith("_LOD0"));
            Mesh mesh = lod0 != null ? lod0.GetComponent<MeshFilter>().sharedMesh : null;
            string materials = lod0 == null ? "none" : string.Join(", ", lod0.sharedMaterials.Select(m =>
                m == null ? "null" : $"{m.shader.name.Replace("Safe Haven/", "")}{(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null ? " NO-TEX" : "")}"));
            sb.Append($"{name}: lods={(lod != null ? lod.lodCount : 0)} tris={(mesh != null ? mesh.triangles.Length / 3 : 0)} " +
                      $"height={(mesh != null ? mesh.bounds.size.y : 0):0.0} wind uv={(mesh != null && mesh.uv2.Length > 0 && mesh.uv3.Length > 0)} " +
                      $"collider={(capsule != null ? $"r{capsule.radius:0.00}" : "none")} [{materials}]\n");
        }

        foreach (string palette in new[] { "Dark Forest Trees", "Haven Trees" })
        {
            ScatterPalette p = WorldAssets.Palette(palette);
            sb.Append($"{palette}: " + (p == null ? "MISSING" : string.Join(", ", p.entries.Select(e => e.prefab != null ? e.prefab.name : "null"))) + "\n");
        }

        return sb.ToString();
    }
}
