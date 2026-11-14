#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
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
        var report = new List<string> { Settings(), Shadows(), Feature(), Volume(), Haze(), SkyOcclusion(), Sky(), AmbientOcclusion(), Variants() };
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

    // Fewer shader variants (measured with ShaderVariantReport: 43,296 after URP's own stripping). Light layers were on 76%
    // of all variants for one user, the character fill light, which the character shader now draws itself; probe blending
    // (70%) and box projection (50%) do nothing with the one follow-the-player probe and no scene probes. The test scenes
    // leave the build (disabled in the list, not removed).
    private static readonly string[] TestScenes =
    {
        "Assets/_Project/Scenes/SampleScene.unity", "Assets/_Project/Scenes/GameplayTestScene.unity", "Assets/_Project/Scenes/Demo/StoryDemo.unity",
    };

    private static string Variants()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        var so = new SerializedObject(pipeline);
        so.FindProperty("m_SupportsLightLayers").boolValue = false;
        so.FindProperty("m_ReflectionProbeBlending").boolValue = false;
        so.FindProperty("m_ReflectionProbeBoxProjection").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int off = 0;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (System.Array.IndexOf(TestScenes, scene.path) >= 0 && scene.enabled)
            {
                scene.enabled = false;
                off++;
            }
        }

        EditorBuildSettings.scenes = scenes;
        return $"light layers, probe blending and box projection off; {off} test scenes out of the build";
    }

    // SSAO seen from the top-down camera, 20-30 m up: at 0.4 m it was a few pixels wide. A wider reach and twelve samples
    // ground logs, stones and feet in a firmer contact shadow; it stays local, so it doesn't stack with the broad dimming
    // under cover (SkyOcclusion.hlsl).
    private static string AmbientOcclusion()
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer);
        ScriptableRendererFeature ssao = data.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
        if (ssao == null)
        {
            return "no SSAO";
        }

        var so = new SerializedObject(ssao);
        so.FindProperty("m_Settings.Radius").floatValue = 0.8f;
        so.FindProperty("m_Settings.Intensity").floatValue = 0.8f;
        so.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.3f;
        so.FindProperty("m_Settings.Samples").enumValueIndex = 0; // High: 12
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return "SSAO wider";
    }

    // The sky every scene with a clock gets (SkyDome): Shaders/Sky.shader with its default colours.
    private static string Sky()
    {
        const string path = "Assets/_Project/Settings/Rendering/Sky.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Sky"));
            AssetDatabase.CreateAsset(material, path);
        }

        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        var so = new SerializedObject(settings);
        so.FindProperty("skyMaterial").objectReferenceValue = material;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return "sky";
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

    // The Sky Occlusion Renderer: a copy of the plain Mobile renderer that draws nothing itself and has one Render Objects
    // pass drawing every opaque thing with the height material, listed on the PC pipeline for SkyOcclusionMap's camera.
    private static string SkyOcclusion()
    {
        const string path = "Assets/_Project/Settings/Rendering/Sky Occlusion Renderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (renderer == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Settings/Rendering/Mobile_Renderer.asset", path);
            renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        }

        const string materialPath = "Assets/_Project/Settings/Rendering/Sky Occlusion Height.mat";
        var height = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (height == null)
        {
            height = new Material(Shader.Find("Hidden/Safe Haven/Sky Occlusion Height"));
            AssetDatabase.CreateAsset(height, materialPath);
        }

        height.enableInstancing = true;
        var so = new SerializedObject(renderer);
        // Forward+ like PC_Renderer: one renderer on the pipeline's list on plain Forward switches the GPU Resident Drawer
        // (and its occlusion culling) off for every camera.
        so.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.ForwardPlus;
        so.FindProperty("m_OpaqueLayerMask").intValue = 0;
        so.FindProperty("m_TransparentLayerMask").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();

        RenderObjects draw = renderer.rendererFeatures.OfType<RenderObjects>().FirstOrDefault();
        if (draw == null)
        {
            draw = ScriptableObject.CreateInstance<RenderObjects>();
            draw.name = "Heights";
            AssetDatabase.AddObjectToAsset(draw, renderer);
            renderer.rendererFeatures.Add(draw);
            var rendererSo = new SerializedObject(renderer);
            SerializedProperty map = rendererSo.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }

            rendererSo.ApplyModifiedPropertiesWithoutUndo();
        }

        draw.settings.Event = RenderPassEvent.AfterRenderingOpaques;
        draw.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
        draw.settings.filterSettings.LayerMask = ~(1 << 5);
        draw.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
        draw.settings.overrideMaterial = height;
        draw.settings.overrideMaterialPassIndex = 0;
        EditorUtility.SetDirty(draw);
        renderer.SetDirty();
        EditorUtility.SetDirty(renderer);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Pipeline);
        var pipelineSo = new SerializedObject(pipeline);
        SerializedProperty list = pipelineSo.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            pipelineSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        var settingsSo = new SerializedObject(settings);
        settingsSo.FindProperty("skyOcclusionRendererIndex").intValue = index;
        settingsSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return $"sky occlusion renderer #{index}";
    }
}
