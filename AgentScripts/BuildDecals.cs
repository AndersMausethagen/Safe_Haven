#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Decals (`unity command script run --file AgentScripts/BuildDecals.cs --entry BuildDecals.Apply`, after
/// `python Tools/Decals/decals.py` made the textures in Art/Decals): the textures' import settings, a URP decal material
/// for each (Art/Decals/Materials), the URP Decal renderer feature on PC_Renderer (Screen Space, normals rebuilt from depth
/// with five samples: it works over every shader. DBuffer was tried first and turned the generated world terrain grey, its
/// decal variant sampling an empty DBuffer) and RenderingSettings' decal slots: blood, footprints, trampled ground, scorch
/// and wall grime.
/// Re-runnable.
/// </summary>
public static class BuildDecals
{
    private const string Art = "Assets/_Project/Art/Decals/";
    private const string Renderer = "Assets/_Project/Settings/Rendering/PC_Renderer.asset";

    public static string Apply()
    {
        // Draw order: worn ground first, then what lies on it.
        Material trampled = Decal("Trampled Ground", 0.5f, 0);
        Material grime = Decal("Wall Grime", 0.5f, 1);
        Material scorch = Decal("Scorch", 0.4f, 2);
        Material footprints = Decal("Footprints", 1f, 3);
        Material blood = Decal("Blood Atlas", 0.7f, 4, readable: true);
        string feature = Feature();

        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        var so = new SerializedObject(settings);
        so.FindProperty("bloodDecal").objectReferenceValue = blood;
        so.FindProperty("bloodAtlas").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Blood Atlas.png");
        so.FindProperty("footprintDecal").objectReferenceValue = footprints;
        so.FindProperty("trampledDecal").objectReferenceValue = trampled;
        so.FindProperty("scorchDecal").objectReferenceValue = scorch;
        so.FindProperty("grimeDecal").objectReferenceValue = grime;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"5 decal materials; {feature}";
    }

    private static Material Decal(string name, float normalBlend, int drawOrder, bool readable = false)
    {
        Texture2D baseMap = Import(Art + name + ".png", false, readable);
        Texture2D normalMap = Import(Art + name + " Normal.png", true, false);
        PrefabBuildUtility.EnsureFolder(Art + "Materials");
        string path = Art + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Shader Graphs/Decal"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("Base_Map", baseMap);
        material.SetTexture("Normal_Map", normalMap);
        material.SetFloat("Normal_Blend", normalBlend);
        material.SetFloat("_DrawOrder", drawOrder);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D Import(string path, bool normal, bool readable)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.alphaIsTransparency = !normal;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.isReadable = readable;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static string Feature()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer);
        DecalRendererFeature feature = data.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            var rendererSo = new SerializedObject(data);
            SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }

            rendererSo.ApplyModifiedPropertiesWithoutUndo();
        }

        var so = new SerializedObject(feature);
        so.FindProperty("m_Settings.technique").intValue = 2; // Screen Space
        so.FindProperty("m_Settings.maxDrawDistance").floatValue = 90f;
        so.FindProperty("m_Settings.screenSpaceSettings.normalBlend").intValue = 1; // Medium
        // No decal layers: URP fades decals by angle only with them, but the world terrain's depth pass doesn't write
        // rendering layers, so with them on nothing lands on the ground. GroundWear keeps its boxes thin instead.
        so.FindProperty("m_Settings.decalLayers").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty();
        EditorUtility.SetDirty(data);
        return "decal feature (Screen Space, medium normals)";
    }
}
