using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Material maps exported by the Local Generator into Assets/_Project/Art/Generated/Materials/&lt;name&gt;/:
/// sets the import type per map suffix (normal map, linear data, repeat wrap) and builds &lt;name&gt;.mat (URP Lit)
/// once the base colour exists. Optional emission and detail maps are wired up when present; the extra masks
/// (_Curvature, _EdgeWear, _Cavity, _Coverage, _BlendMask, _Splat) are imported as linear data for custom shaders.
/// Re-exporting (or deleting a map) rebuilds the existing material.
/// </summary>
public sealed class GeneratedTexturePostprocessor : AssetPostprocessor
{
    internal const string MaterialsRoot = "Assets/_Project/Art/Generated/Materials/";
    private const string LitShader = "Universal Render Pipeline/Lit";

    internal enum MapKind
    {
        BaseColor, Normal, MetallicSmoothness, Occlusion, Height, Roughness, Emission,
        DetailAlbedo, DetailNormal, DetailMask, Mask, Sidecar,
    }

    private static readonly (string Suffix, MapKind Kind)[] Suffixes =
    {
        ("_BaseColor", MapKind.BaseColor), ("_DetailNormal", MapKind.DetailNormal), ("_Normal", MapKind.Normal),
        ("_MetallicSmoothness", MapKind.MetallicSmoothness), ("_Occlusion", MapKind.Occlusion),
        ("_Height", MapKind.Height), ("_Roughness", MapKind.Roughness), ("_Emission", MapKind.Emission),
        ("_DetailAlbedo", MapKind.DetailAlbedo), ("_DetailMask", MapKind.DetailMask),
        ("_Curvature", MapKind.Mask), ("_EdgeWear", MapKind.Mask), ("_Cavity", MapKind.Mask),
        ("_Coverage", MapKind.Mask), ("_BlendMask", MapKind.Mask), ("_Splat", MapKind.Mask),
        ("_material", MapKind.Sidecar),
    };

    [System.Serializable]
    private sealed class Sidecar
    {
        public float parallax;
        public float emission_intensity = 1f;
        public float detail_tiling = 8f;
        public float detail_normal_scale = 1f;
    }

    internal static bool TryParse(string assetPath, out string folder, out string materialName, out MapKind kind)
    {
        folder = materialName = null;
        kind = default;
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith(MaterialsRoot, System.StringComparison.Ordinal))
        {
            return false;
        }

        string stem = Path.GetFileNameWithoutExtension(path);
        foreach ((string suffix, MapKind mapKind) in Suffixes)
        {
            if (stem.Length > suffix.Length && stem.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                folder = Path.GetDirectoryName(path).Replace('\\', '/');
                materialName = stem.Substring(0, stem.Length - suffix.Length);
                kind = mapKind;
                return true;
            }
        }

        return false;
    }

    private void OnPreprocessTexture()
    {
        if (!TryParse(assetPath, out _, out _, out MapKind kind) || kind == MapKind.Sidecar)
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.textureType = kind == MapKind.Normal || kind == MapKind.DetailNormal
            ? TextureImporterType.NormalMap
            : TextureImporterType.Default;
        // Detail albedo stays linear: URP multiplies it by 2, so mid-grey (128) must sample as 0.5 to be neutral.
        importer.sRGBTexture = kind == MapKind.BaseColor || kind == MapKind.Emission;
        if (kind == MapKind.MetallicSmoothness || kind == MapKind.DetailMask || kind == MapKind.Mask)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
        }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        var pending = new HashSet<(string, string)>();
        foreach (string[] paths in new[] { imported, deleted })
        {
            foreach (string path in paths)
            {
                if (TryParse(path, out string folder, out string name, out _))
                {
                    pending.Add((folder, name));
                }
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        // Creating assets during an import callback is unsafe; build once the import batch has finished.
        EditorApplication.delayCall += () =>
        {
            foreach ((string folder, string name) in pending)
            {
                BuildMaterial(folder, name);
            }
        };
    }

    internal static Material BuildMaterial(string folder, string name)
    {
        string prefix = $"{folder}/{name}";
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BaseColor.png");
        if (baseColor == null)
        {
            return null;
        }

        string materialPath = prefix + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find(LitShader);
            if (shader == null)
            {
                Debug.LogWarning($"Generated material '{name}': shader '{LitShader}' not found.");
                return null;
            }

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.SetTexture("_BaseMap", baseColor);
        material.SetColor("_BaseColor", Color.white);
        AssignMap(material, "_BumpMap", "_NORMALMAP", prefix + "_Normal.png");
        material.SetFloat("_BumpScale", 1f);
        if (AssignMap(material, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP", prefix + "_MetallicSmoothness.png"))
        {
            material.SetFloat("_Smoothness", 1f); // multiplies the map's alpha
            material.SetFloat("_SmoothnessTextureChannel", 0f);
        }

        if (AssignMap(material, "_OcclusionMap", "_OCCLUSIONMAP", prefix + "_Occlusion.png"))
        {
            material.SetFloat("_OcclusionStrength", 1f);
        }

        Sidecar sidecar = ReadSidecar(prefix + "_material.json");
        float parallax = Mathf.Clamp(sidecar.parallax, 0f, 0.08f);
        if (parallax > 0f && AssignMap(material, "_ParallaxMap", "_PARALLAXMAP", prefix + "_Height.png"))
        {
            material.SetFloat("_Parallax", parallax);
        }
        else
        {
            material.SetTexture("_ParallaxMap", null);
            material.DisableKeyword("_PARALLAXMAP");
        }

        AssignEmission(material, prefix, sidecar);
        AssignDetail(material, prefix, sidecar);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static bool AssignMap(Material material, string property, string keyword, string texturePath)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture(property, texture);
        if (texture != null)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }

        return texture != null;
    }

    private static void AssignEmission(Material material, string prefix, Sidecar sidecar)
    {
        if (AssignMap(material, "_EmissionMap", "_EMISSION", prefix + "_Emission.png"))
        {
            float intensity = Mathf.Clamp(sidecar.emission_intensity, 0f, 50f);
            material.SetColor("_EmissionColor", new Color(intensity, intensity, intensity, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    private static void AssignDetail(Material material, string prefix, Sidecar sidecar)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_DetailAlbedo.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_DetailNormal.png");
        bool on = albedo != null || normal != null;
        material.SetTexture("_DetailAlbedoMap", albedo);
        material.SetTexture("_DetailNormalMap", normal);
        material.SetTexture("_DetailMask", on ? AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_DetailMask.png") : null);
        if (on)
        {
            float tiling = Mathf.Max(0.01f, sidecar.detail_tiling);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling)); // URP uses this for both maps
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.SetFloat("_DetailNormalMapScale", sidecar.detail_normal_scale);
            material.EnableKeyword("_DETAIL_MULX2");
        }
        else
        {
            material.DisableKeyword("_DETAIL_MULX2");
        }

        material.DisableKeyword("_DETAIL_SCALED");
    }

    private static Sidecar ReadSidecar(string sidecarPath)
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(sidecarPath);
        return text == null ? new Sidecar() : JsonUtility.FromJson<Sidecar>(text.text) ?? new Sidecar();
    }
}
