using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The world's look, round one (`unity command script run --file AgentScripts/BuildRendering.cs --entry BuildRendering.Apply`):
/// Resources/RenderingSettings (follow-the-player reflections, cloud shadows), four shadow cascades and GPU occlusion culling
/// on the PC pipeline, the TonemapFeature on PC_Renderer after the Atmosphere feature, and the Haven's base volume moved
/// from ACES to the filmic curve with its grade re-balanced for it (the raids' volumes are left as they are).
/// Re-runnable. Touches no scene.
/// </summary>
public static class BuildRendering
{
    private const string Pipeline = "Assets/_Project/Settings/Rendering/PC_RPAsset.asset";
    private const string Renderer = "Assets/_Project/Settings/Rendering/PC_Renderer.asset";
    private const string HavenVolume = "Assets/_Project/Settings/Rendering/Volumes/SafeHaven_Volume.asset";

    // The Haven's grade under the new curve: ACES's own contrast and warmth were partly made up for by the grade.
    public static FilmicCurve Curve = FilmicCurve.Neutral;
    public static float Exposure = -0.1f;
    public static float Saturation = 1.05f;
    public static float Temperature = 0f;
    public static float Tint = 0f;
    public static float Contrast = 18f;
    public static float ColourSaturation = 6f;

    public static string Apply()
    {
        var report = new List<string> { Settings(), Shadows(), Feature(), Volume(), Haze() };
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static string Settings()
    {
        const string path = "Assets/_Project/Resources/RenderingSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RenderingSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("cloudShader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/Cloud Cookie");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return "rendering settings";
    }

    private static string Shadows()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        var so = new SerializedObject(asset);
        // Same 50 m and the same map: four nearer splits give the ground near the player sharper shadows.
        so.FindProperty("m_ShadowCascadeCount").intValue = 4;
        so.FindProperty("m_Cascade4Split").vector3Value = new Vector3(0.08f, 0.22f, 0.48f);
        SerializedProperty occlusion = so.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras");
        if (occlusion != null)
        {
            occlusion.boolValue = true;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return $"4 cascades{(occlusion != null ? ", GPU occlusion culling" : ", no occlusion setting")}";
    }

    private static string Feature()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer);
        if (data.rendererFeatures.OfType<TonemapFeature>().Any())
        {
            return "tonemap feature had it";
        }

        var feature = ScriptableObject.CreateInstance<TonemapFeature>();
        feature.name = "Filmic Tonemap";
        var featureSo = new SerializedObject(feature);
        featureSo.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/Filmic Tonemap");
        featureSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.AddObjectToAsset(feature, data);

        // After the Atmosphere feature, so the fog is mapped too; the editor keeps a map of the features' file ids.
        int at = data.rendererFeatures.FindIndex(f => f is AtmosphereFeature);
        data.rendererFeatures.Insert(at >= 0 ? at + 1 : data.rendererFeatures.Count, feature);
        var so = new SerializedObject(data);
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize = data.rendererFeatures.Count;
        for (int i = 0; i < data.rendererFeatures.Count; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty();
        EditorUtility.SetDirty(data);
        return "tonemap feature added";
    }

    private static string Volume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HavenVolume);
        if (!profile.TryGet(out FilmicTonemapping filmic))
        {
            filmic = profile.Add<FilmicTonemapping>();
            AssetDatabase.AddObjectToAsset(filmic, profile);
        }

        filmic.curve.Override(Curve);
        filmic.exposure.Override(Exposure);
        filmic.saturation.Override(Saturation);
        if (profile.TryGet(out Tonemapping tonemapping))
        {
            tonemapping.mode.Override(TonemappingMode.None);
        }

        if (profile.TryGet(out WhiteBalance white))
        {
            white.temperature.Override(Temperature);
            white.tint.Override(Tint);
        }

        if (profile.TryGet(out ColorAdjustments adjust))
        {
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(ColourSaturation);
        }

        EditorUtility.SetDirty(filmic);
        EditorUtility.SetDirty(profile);
        return $"Haven volume: {Curve}";
    }

    // The heat haze LightFlicker puts over fires (Resources/Heat Haze).
    private static string Haze()
    {
        const string path = "Assets/_Project/Resources/Heat Haze.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Heat Haze"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return "heat haze";
    }
}
