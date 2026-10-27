using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using GLTFast.Schema;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Material = UnityEngine.Material;
using Texture = UnityEngine.Texture;

/// <summary>
/// Models exported by LocalSpawn (GLBs under Assets/_Project/Models/Generated/) import with the project's own shader
/// instead of glTFast's "Shader Graphs/glTF-pbrMetallicRoughness": "Safe Haven/Layered Lit" by default (live moss /
/// rust layers driven by SurfaceLayerController), or the shader named in the model's sidecar
/// (&lt;model&gt;.material.json, written by LocalSpawn), which also carries the shader values (emission colour and
/// strength, layer textures, blend mask...). The glTF maps (base colour, normal, ORM, emission) carry over.
/// Trees in Models/Generated/Vegetation keep the Tree Wind flow (TreeWindMaterials) unless they have a sidecar.
///
/// glTFast's editor importer has no setting for this, so the project replaces its default material generator with
/// one that wraps it (reinstalled before every GLB import; glTFast resets it when entering Play mode).
/// </summary>
[InitializeOnLoad]
public static class LocalSpawnMaterials
{
    public const string ModelsRoot = "Assets/_Project/Models/Generated/";
    /// <summary>Downloaded CC0 models (Poly Haven) that import the same way: FBX with a sidecar naming their maps.</summary>
    public const string DownloadedModelsRoot = "Assets/_Project/Models/PolyHaven/";
    public const string LayerTexturesRoot = "Assets/_Project/Art/Generated/SurfaceLayers/";
    public const string DefaultShader = "Safe Haven/Layered Lit";
    public const string SidecarSuffix = ".material.json";
    public const string LayerTagPrefix = "SurfaceLayer";

    [Serializable]
    public sealed class Sidecar
    {
        public int version = 1;
        public string shader;
        [Tooltip("Names of Layered Lit's layers (Layer 1, Layer 2), stored as material tags for SurfaceLayerController.")]
        public string[] layers = Array.Empty<string>();
        public Property[] properties = Array.Empty<Property>();
        [Tooltip("FBX / OBJ with several materials (a tree's bark and needles): per material, by a part of its name, " +
                 "its own shader, layers and values instead of the ones above.")]
        public MaterialSection[] materials = Array.Empty<MaterialSection>();

        /// <summary>The settings for one of the model's materials: its section, or the sidecar itself.</summary>
        public Sidecar For(string materialName)
        {
            foreach (MaterialSection section in materials ?? Array.Empty<MaterialSection>())
            {
                if (section != null && !string.IsNullOrEmpty(section.match) && materialName.Contains(section.match))
                {
                    return new Sidecar { version = version, shader = section.shader, layers = section.layers, properties = section.properties };
                }
            }

            return this;
        }
    }

    [Serializable]
    public sealed class MaterialSection
    {
        public string match;
        public string shader;
        public string[] layers = Array.Empty<string>();
        public Property[] properties = Array.Empty<Property>();
    }

    /// <summary>type: float, color (linear values, as the shader sees them), vector, texture (project path, or
    /// relative to the model's folder) or keyword (value 1 enables it).</summary>
    [Serializable]
    public sealed class Property
    {
        public string name;
        public string type;
        public float[] value = Array.Empty<float>();
        public string texture;
    }

    private static readonly FieldInfo DefaultGeneratorField = typeof(MaterialGenerator).GetField(
        "s_DefaultMaterialGenerator", BindingFlags.NonPublic | BindingFlags.Static);

    // The GLB being imported and its sidecar (set in LocalSpawnModelImport.OnPreprocessAsset).
    internal static string CurrentImport;
    internal static Sidecar CurrentSidecar;

    static LocalSpawnMaterials()
    {
        Install();
        EditorApplication.playModeStateChanged += _ => Install();
    }

    /// <summary>Wraps glTFast's default material generator. False when glTFast changed its internals; GLBs then
    /// import with glTFast's shader and a warning is logged.</summary>
    public static bool Install()
    {
        if (DefaultGeneratorField == null)
        {
            Debug.LogWarning("LocalSpawn materials: this glTFast version has no default material generator to " +
                             "extend; generated models keep glTFast's shader.");
            return false;
        }

        if (DefaultGeneratorField.GetValue(null) is Generator)
        {
            return true;
        }

        DefaultGeneratorField.SetValue(null, null); // let glTFast create its pipeline-specific generator first
        DefaultGeneratorField.SetValue(null, new Generator(MaterialGenerator.GetDefaultMaterialGenerator()));
        return true;
    }

    public static string SidecarPath(string glbPath) =>
        glbPath.Substring(0, glbPath.Length - Path.GetExtension(glbPath).Length) + SidecarSuffix;

    public static bool IsGeneratedModel(string path)
    {
        path = path.Replace('\\', '/');
        return path.StartsWith(ModelsRoot, StringComparison.Ordinal) &&
               path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>FBX / OBJ exports (Unity's model importer): they get the project shader only with a sidecar, which
    /// names their maps (LocalSpawn writes them next to the model).</summary>
    public static bool IsGeneratedModelFile(string path)
    {
        path = path.Replace('\\', '/');
        return IsInModelRoot(path) &&
               (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether the path is under a folder whose models get the project shader (generated or downloaded).</summary>
    public static bool IsInModelRoot(string path) =>
        path.StartsWith(ModelsRoot, StringComparison.Ordinal) ||
        path.StartsWith(DownloadedModelsRoot, StringComparison.Ordinal);

    /// <summary>Whether this GLB gets the project shader: generated models, except trees without a sidecar.</summary>
    public static bool UsesProjectShader(string path, Sidecar sidecar) =>
        IsGeneratedModel(path) && (sidecar != null || !TreeWindMaterials.IsTreeGlb(path.Replace('\\', '/')));

    public static Sidecar ReadSidecar(string glbPath)
    {
        string path = SidecarPath(glbPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<Sidecar>(File.ReadAllText(path));
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning($"LocalSpawn materials: {path} is not a valid material sidecar ({exception.Message}).");
            return null;
        }
    }

    /// <summary>Project path of a sidecar texture: as given when it starts with Assets/, else relative to the model.</summary>
    public static string ResolveTexture(string glbPath, string texture)
    {
        if (string.IsNullOrEmpty(texture))
        {
            return null;
        }

        texture = texture.Replace('\\', '/');
        return texture.StartsWith("Assets/", StringComparison.Ordinal)
            ? texture
            : Path.GetDirectoryName(glbPath).Replace('\\', '/') + "/" + texture;
    }

    /// <summary>A material with the project shader built from a glTFast material (null when the shader is missing).</summary>
    public static Material Convert(Material source, Sidecar sidecar, string glbPath)
    {
        string shaderName = string.IsNullOrEmpty(sidecar?.shader) ? DefaultShader : sidecar.shader;
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"LocalSpawn materials: shader '{shaderName}' not found; {glbPath} keeps glTFast's shader.");
            return null;
        }

        var material = new Material(shader) { name = source.name, enableInstancing = true };
        CopyGltf(source, material);
        if (sidecar != null)
        {
            Apply(material, sidecar, glbPath);
        }

        return material;
    }

    /// <summary>Copies glTFast's glTF-pbrMetallicRoughness values to the URP Lit-style properties the project shaders
    /// share (only the ones the target shader has).</summary>
    public static void CopyGltf(Material source, Material target)
    {
        SetTexture(target, "_BaseMap", source.GetTexture("baseColorTexture"));
        if (source.HasProperty("baseColorTexture_ST") && target.HasProperty("_BaseMap"))
        {
            Vector4 st = source.GetVector("baseColorTexture_ST");
            target.SetTextureScale("_BaseMap", new Vector2(st.x, st.y));
            target.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
        }

        SetColor(target, "_BaseColor", source.GetColor("baseColorFactor"));
        SetTexture(target, "_BumpMap", source.GetTexture("normalTexture"));
        SetFloat(target, "_BumpScale", source.GetFloat("normalTexture_scale"));
        Texture orm = source.GetTexture("metallicRoughnessTexture");
        Texture occlusion = source.GetTexture("occlusionTexture");
        float roughness = source.GetFloat("roughnessFactor");
        SetFloat(target, "_Metallic", source.GetFloat("metallicFactor"));
        if (orm != null && target.HasProperty("_ORMMap"))
        {
            target.SetTexture("_ORMMap", orm);
            target.EnableKeyword("_ORMMAP");
            SetFloat(target, "_Roughness", roughness);
            // LocalSpawn packs ambient occlusion into the metallic-roughness image (ORM).
            SetFloat(target, "_OcclusionStrength", occlusion == orm ? source.GetFloat("occlusionTexture_strength") : 0f);
        }
        else
        {
            SetFloat(target, "_Smoothness", 1f - roughness);
        }

        Texture emission = source.GetTexture("emissiveTexture");
        Color emissive = source.GetColor("emissiveFactor");
        SetTexture(target, "_EmissionMap", emission);
        SetColor(target, "_EmissionColor", emission != null || emissive.maxColorComponent > 0f ? emissive : Color.black);
        UpdateEmission(target);
    }

    /// <summary>Applies the sidecar's shader values and layer names.</summary>
    public static void Apply(Material material, Sidecar sidecar, string glbPath)
    {
        foreach (Property property in sidecar.properties ?? Array.Empty<Property>())
        {
            if (property == null || string.IsNullOrEmpty(property.name))
            {
                continue;
            }

            float[] v = property.value ?? Array.Empty<float>();
            switch (property.type)
            {
                case "float" when v.Length > 0:
                    SetFloat(material, property.name, v[0]);
                    break;
                case "color" when v.Length >= 3:
                    // Values are linear (what the shader receives); SetColor expects gamma in a linear project.
                    var linear = new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f);
                    SetColor(material, property.name, QualitySettings.activeColorSpace == ColorSpace.Linear ? linear.gamma : linear);
                    break;
                case "vector" when v.Length > 0:
                    if (material.HasProperty(property.name))
                    {
                        material.SetVector(property.name, new Vector4(v[0], v.Length > 1 ? v[1] : 0f,
                                                                      v.Length > 2 ? v[2] : 0f, v.Length > 3 ? v[3] : 0f));
                    }

                    break;
                case "texture":
                    string path = ResolveTexture(glbPath, property.texture);
                    var texture = path != null ? AssetDatabase.LoadAssetAtPath<Texture>(path) : null;
                    if (path != null && texture == null)
                    {
                        Debug.LogWarning($"LocalSpawn materials: texture {path} for {property.name} of {glbPath} is missing.");
                    }

                    SetTexture(material, property.name, texture);
                    break;
                case "keyword" when v.Length > 0:
                    if (v[0] > 0.5f) material.EnableKeyword(property.name);
                    else material.DisableKeyword(property.name);
                    break;
            }
        }

        string[] layers = sidecar.layers ?? Array.Empty<string>();
        for (int i = 0; i < layers.Length; i++)
        {
            if (!string.IsNullOrEmpty(layers[i]))
            {
                material.SetOverrideTag(LayerTagPrefix + (i + 1), layers[i]);
            }
        }

        UpdateEmission(material);
    }

    private static void UpdateEmission(Material material)
    {
        bool on = material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0f;
        if (on) material.EnableKeyword("_EMISSION");
        else material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = on ? MaterialGlobalIlluminationFlags.BakedEmissive
                                              : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private static void SetTexture(Material material, string name, Texture texture)
    {
        if (material.HasProperty(name)) material.SetTexture(name, texture);
    }

    private static void SetColor(Material material, string name, Color color)
    {
        if (material.HasProperty(name)) material.SetColor(name, color);
    }

    private static void SetFloat(Material material, string name, float value)
    {
        if (material.HasProperty(name)) material.SetFloat(name, value);
    }

    /// <summary>glTFast's own generator, with LocalSpawn models converted to the project shader.</summary>
    private sealed class Generator : IMaterialGenerator
    {
        private readonly IMaterialGenerator inner;

        public Generator(IMaterialGenerator inner) => this.inner = inner;

        public Material GetDefaultMaterial(bool pointsSupport = false) => inner.GetDefaultMaterial(pointsSupport);

        public void SetLogger(ICodeLogger logger) => inner.SetLogger(logger);

        public Material GenerateMaterial(MaterialBase gltfMaterial, IGltfReadable gltf, bool pointsSupport = false)
        {
            Material source = inner.GenerateMaterial(gltfMaterial, gltf, pointsSupport);
            string path = CurrentImport;
            if (source == null || pointsSupport || path == null || !UsesProjectShader(path, CurrentSidecar))
            {
                return source;
            }

            // The sidecar may keep glTFast's own shader (it has everything a plain prop needs): only its values change.
            if (CurrentSidecar != null && CurrentSidecar.shader == source.shader.name)
            {
                Apply(source, CurrentSidecar, path);
                return source;
            }

            // Layered Lit is opaque: cut-out or transparent glTF materials (leaves, glass) keep glTFast's shader
            // unless the sidecar names another one.
            bool alpha = source.IsKeywordEnabled("_ALPHATEST_ON") || source.renderQueue > 2450;
            if (alpha && string.IsNullOrEmpty(CurrentSidecar?.shader))
            {
                return source;
            }

            Material converted = Convert(source, CurrentSidecar, path);
            if (converted == null)
            {
                return source;
            }

            UnityEngine.Object.DestroyImmediate(source);
            return converted;
        }
    }
}

/// <summary>Tells LocalSpawnMaterials which GLB is importing, declares its sidecar textures as dependencies (so they
/// import first and changes reimport the model), and sets up the shared layer textures and blend masks.</summary>
public sealed class LocalSpawnModelImport : AssetPostprocessor
{
    private void OnPreprocessAsset()
    {
        bool glb = assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
        if (!glb && !LocalSpawnMaterials.IsGeneratedModelFile(assetPath))
        {
            return;
        }

        LocalSpawnMaterials.Sidecar sidecar = LocalSpawnMaterials.ReadSidecar(assetPath);
        if (sidecar != null)
        {
            context.DependsOnSourceAsset(LocalSpawnMaterials.SidecarPath(assetPath));
            IEnumerable<LocalSpawnMaterials.Property> properties = (sidecar.properties ?? Array.Empty<LocalSpawnMaterials.Property>())
                .Concat((sidecar.materials ?? Array.Empty<LocalSpawnMaterials.MaterialSection>())
                        .SelectMany(m => m?.properties ?? Array.Empty<LocalSpawnMaterials.Property>()));
            foreach (LocalSpawnMaterials.Property property in properties)
            {
                string texture = property?.type == "texture"
                    ? LocalSpawnMaterials.ResolveTexture(assetPath, property.texture)
                    : null;
                if (texture != null && File.Exists(texture))
                {
                    context.DependsOnArtifact(texture);
                }
            }
        }

        if (!glb)
        {
            return; // FBX / OBJ: OnPreprocessMaterialDescription builds their materials
        }

        LocalSpawnMaterials.Install();
        LocalSpawnMaterials.CurrentImport = assetPath;
        LocalSpawnMaterials.CurrentSidecar = sidecar;
        EditorApplication.delayCall += () =>
        {
            LocalSpawnMaterials.CurrentImport = null;
            LocalSpawnMaterials.CurrentSidecar = null;
        };
    }

    // FBX / OBJ exported by LocalSpawn: the project shader with the maps and values the sidecar names (LocalSpawn
    // writes the maps next to the model, because Unity's importer cannot read the glTF channel packing).
    private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
    {
        if (!LocalSpawnMaterials.IsGeneratedModelFile(assetPath))
        {
            return;
        }

        LocalSpawnMaterials.Sidecar sidecar = LocalSpawnMaterials.ReadSidecar(assetPath)?.For(material.name);
        string shaderName = string.IsNullOrEmpty(sidecar?.shader) ? null : sidecar.shader;
        Shader shader = shaderName != null ? Shader.Find(shaderName) : null;
        if (shader == null || shaderName == "Shader Graphs/glTF-pbrMetallicRoughness")
        {
            return; // no sidecar, or one asking for glTFast's shader (it cannot read FBX materials): Unity's default
        }

        material.shader = shader;
        material.enableInstancing = true;
        LocalSpawnMaterials.Apply(material, sidecar, assetPath);
    }

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        bool layer = path.StartsWith(LocalSpawnMaterials.LayerTexturesRoot, StringComparison.Ordinal);
        bool model = LocalSpawnMaterials.IsInModelRoot(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        bool mask = model && path.EndsWith("_BlendMask.png", StringComparison.Ordinal);
        if (model && !mask)
        {
            // Maps written next to FBX / OBJ exports: normal map and linear ORM; the rest keep Unity's defaults.
            var mapImporter = (TextureImporter)assetImporter;
            if (stem.EndsWith("_Normal", StringComparison.Ordinal))
            {
                mapImporter.textureType = TextureImporterType.NormalMap;
            }
            else if (stem.Contains("_ORM_") || stem.Contains("_MetallicRoughness"))
            {
                mapImporter.sRGBTexture = false;
            }
            else if (stem.EndsWith("Needles_Color", StringComparison.Ordinal))
            {
                // Baked conifer needles (alpha-clipped cards): keep the needles' coverage in the smaller mipmaps, or
                // distant trees thin out to nothing or blur into clumps.
                mapImporter.mipMapsPreserveCoverage = true;
                mapImporter.alphaTestReferenceValue = 0.4f;
            }

            return;
        }

        if (!layer && !mask)
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        bool normal = layer && stem.EndsWith("_Normal", StringComparison.Ordinal);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = layer && stem.EndsWith("_Color", StringComparison.Ordinal);
        importer.wrapMode = layer ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        if (!normal)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
        }
    }

    // A sidecar written after its model (or changed alone) reimports the model.
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (!path.EndsWith(LocalSpawnMaterials.SidecarSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            string glb = path.Substring(0, path.Length - LocalSpawnMaterials.SidecarSuffix.Length) + ".glb";
            if (File.Exists(glb) && Array.IndexOf(imported, glb) < 0)
            {
                EditorApplication.delayCall += () => AssetDatabase.ImportAsset(glb, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
