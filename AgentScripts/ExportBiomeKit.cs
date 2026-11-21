using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes game prefabs out for Blender (the 3D cinematic shots, Tools/Blender/Cinematics): each prefab's most detailed LOD as an OBJ,
/// in the prefab's own space (Unity's left-handed Y-up turned to OBJ's right-handed Y-up: x flipped, triangles turned round), one
/// group per material, and kit.json listing every material's textures (base colour, normal, mask/ORM), colour, alpha cut-off and
/// tiling, with the prefab's bounds. Run with no domain reload:
///   unity command script run --file AgentScripts/ExportBiomeKit.cs --entry ExportBiomeKit.Run --args '["out folder", "prefab;prefab;..."]'
/// </summary>
public static class ExportBiomeKit
{
    private static readonly string[] BaseNames = { "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo", "_BaseColor_Map" };
    private static readonly string[] NormalNames = { "_BumpMap", "_NormalMap", "_Normal" };
    private static readonly string[] MaskNames = { "_ORMMap", "_MaskMap", "_MetallicGlossMap", "_OcclusionMap" };

    public static string Run(string outFolder, string prefabList)
    {
        Directory.CreateDirectory(outFolder);
        var kit = new StringBuilder("{\n  \"prefabs\": [\n");
        var materials = new Dictionary<string, Material>();
        bool firstPrefab = true;
        int written = 0;
        foreach (string path in prefabList.Split(';'))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Trim());
            if (prefab == null)
            {
                Debug.LogWarning("ExportBiomeKit: no prefab at " + path);
                continue;
            }

            List<Renderer> renderers = Lod0(prefab);
            string file = Safe(prefab.name) + ".obj";
            Bounds bounds = WriteObj(Path.Combine(outFolder, file), prefab.transform, renderers, materials);
            kit.Append(firstPrefab ? "" : ",\n").Append("    {\"name\": ").Append(Q(prefab.name)).Append(", \"obj\": ").Append(Q(file))
               .Append(", \"size\": ").Append(V(bounds.size)).Append(", \"min\": ").Append(V(bounds.min)).Append("}");
            firstPrefab = false;
            written++;
        }

        kit.Append("\n  ],\n  \"materials\": {\n");
        bool firstMat = true;
        foreach (KeyValuePair<string, Material> pair in materials)
        {
            Material m = pair.Value;
            kit.Append(firstMat ? "" : ",\n").Append("    ").Append(Q(pair.Key)).Append(": {")
               .Append("\"shader\": ").Append(Q(m.shader != null ? m.shader.name : ""))
               .Append(", \"base\": ").Append(Q(Texture(m, BaseNames)))
               .Append(", \"normal\": ").Append(Q(Texture(m, NormalNames)))
               .Append(", \"mask\": ").Append(Q(Texture(m, MaskNames)))
               .Append(", \"color\": ").Append(C(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white))
               .Append(", \"cutoff\": ").Append(F(Cutoff(m)))
               .Append(", \"tiling\": ").Append(Tiling(m))
               .Append("}");
            firstMat = false;
        }

        kit.Append("\n  }\n}\n");
        File.WriteAllText(Path.Combine(outFolder, "kit.json"), kit.ToString());
        return $"{written} prefabs, {materials.Count} materials -> {outFolder}";
    }

    // The renderers of the most detailed LOD (a LOD Group's first), or every mesh renderer under it.
    private static List<Renderer> Lod0(GameObject prefab)
    {
        var lods = prefab.GetComponentInChildren<LODGroup>(true);
        if (lods != null && lods.GetLODs().Length > 0)
        {
            var found = new List<Renderer>();
            foreach (Renderer r in lods.GetLODs()[0].renderers)
            {
                if (r != null)
                {
                    found.Add(r);
                }
            }

            if (found.Count > 0)
            {
                return found;
            }
        }

        return new List<Renderer>(prefab.GetComponentsInChildren<MeshRenderer>(true));
    }

    private static Bounds WriteObj(string path, Transform root, List<Renderer> renderers, Dictionary<string, Material> materials)
    {
        var obj = new StringBuilder();
        int offset = 1;
        bool any = false;
        Bounds bounds = default;
        foreach (Renderer r in renderers)
        {
            var filter = r.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                continue;
            }

            Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            foreach (Vector3 v in vertices)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(v);
                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }

                obj.Append("v ").Append(F(-p.x)).Append(' ').Append(F(p.y)).Append(' ').Append(F(p.z)).Append('\n');
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 uv = uvs != null && i < uvs.Length ? uvs[i] : Vector2.zero;
                obj.Append("vt ").Append(F(uv.x)).Append(' ').Append(F(uv.y)).Append('\n');
            }

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals != null && i < normals.Length ? toRoot.MultiplyVector(normals[i]).normalized : Vector3.up;
                obj.Append("vn ").Append(F(-n.x)).Append(' ').Append(F(n.y)).Append(' ').Append(F(n.z)).Append('\n');
            }

            Material[] shared = r.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                Material mat = sub < shared.Length ? shared[sub] : null;
                string name = mat != null ? Safe(mat.name) : "None";
                if (mat != null)
                {
                    materials[name] = mat;
                }

                obj.Append("usemtl ").Append(name).Append('\n');
                int[] tris = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    // Flipping x turns the winding: written c, b, a.
                    int a = tris[t] + offset, b = tris[t + 1] + offset, c = tris[t + 2] + offset;
                    obj.Append("f ").Append(c).Append('/').Append(c).Append('/').Append(c).Append(' ')
                       .Append(b).Append('/').Append(b).Append('/').Append(b).Append(' ')
                       .Append(a).Append('/').Append(a).Append('/').Append(a).Append('\n');
                }
            }

            offset += vertices.Length;
        }

        File.WriteAllText(path, obj.ToString());
        return bounds;
    }

    private static string Texture(Material m, string[] names)
    {
        foreach (string name in names)
        {
            if (m.HasProperty(name) && m.GetTexture(name) is Texture t && t != null)
            {
                string asset = AssetDatabase.GetAssetPath(t);
                if (!string.IsNullOrEmpty(asset))
                {
                    return Path.GetFullPath(asset).Replace('\\', '/');
                }
            }
        }

        return "";
    }

    private static float Cutoff(Material m)
    {
        bool clips = m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f)
                     || (m.HasProperty("_AlphaCutoffEnable") && m.GetFloat("_AlphaCutoffEnable") > 0.5f);
        if (!clips)
        {
            return 0f;
        }

        return m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : m.HasProperty("_AlphaCutoff") ? m.GetFloat("_AlphaCutoff") : 0.5f;
    }

    private static string Tiling(Material m)
    {
        foreach (string name in BaseNames)
        {
            if (m.HasProperty(name))
            {
                Vector2 s = m.GetTextureScale(name), o = m.GetTextureOffset(name);
                return $"[{F(s.x)}, {F(s.y)}, {F(o.x)}, {F(o.y)}]";
            }
        }

        return "[1, 1, 0, 0]";
    }

    private static string Safe(string name)
    {
        var s = new StringBuilder();
        foreach (char ch in name)
        {
            s.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_');
        }

        return s.ToString();
    }

    private static string F(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);
    private static string V(Vector3 v) => $"[{F(v.x)}, {F(v.y)}, {F(v.z)}]";
    private static string C(Color c) => $"[{F(c.r)}, {F(c.g)}, {F(c.b)}, {F(c.a)}]";
    private static string Q(string s) => "\"" + (s ?? "").Replace("\\", "/").Replace("\"", "'") + "\"";
}
