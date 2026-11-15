using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Carries out the graphics settings. The project's own render assets are never changed: the settings go on a copy of the
/// pipeline asset made when the game starts, and what is left (ambient occlusion on the shared renderer) is put back when
/// the game ends, so playing in the Editor leaves nothing behind. The authored values are the top of each setting, so the
/// default is the game as it was made. Terrains and grass are per scene: <see cref="ApplyToScene"/> runs on every scene
/// change. Look settings (bloom and the like) are an override volume on top of the scene's own, built by OptionsSystem.
/// </summary>
public static class GraphicsOptions
{
    // Anti-Aliasing: Off, 2x, 4x, 8x MSAA, TAA (temporal, which needs MSAA off).
    private static readonly int[] MsaaSamples = { 1, 2, 4, 8, 1 };
    private const int TemporalChoice = 4;
    // Ultra goes past the pipeline asset's own 4096: the sun's 8192 map (URP's largest), for the ground's small relief shadows.
    private static readonly int[] ShadowResolution = { 1024, 2048, 4096, 8192 };
    // The atlas the fires' and lamps' shadows share (LightShadowBudget fits their resolutions to it). High and Ultra's 4096
    // holds the nearest point light at the High tier and two more at Medium; the pipeline asset's 2048 couldn't hold even one
    // at High, and URP halved them all with a warning every frame (the user, 2026-11-15). Low and Medium keep the asset's 2048.
    private static readonly int[] AdditionalShadowAtlas = { 2048, 2048, 4096, 4096 };
    private const int UltraShadows = 3;
    private static readonly float[] ShadowDistanceShare = { 0.5f, 0.7f, 1f, 1f };
    // High and Ultra keep the pipeline's own four (sharper near the player over the same distance).
    private static readonly int[] ShadowCascades = { 1, 2, 4, 4 };
    private static readonly float[] FogShare = { 0.5f, 0.75f, 1f };
    private static readonly float[] LodShare = { 0.6f, 0.8f, 1f };
    private static readonly int[] MipLimit = { 2, 1, 0 };
    private static readonly UpscalingFilterSelection[] Upscalers =
        { UpscalingFilterSelection.Linear, UpscalingFilterSelection.FSR, UpscalingFilterSelection.STP, UpscalingFilterSelection.STP, UpscalingFilterSelection.STP };
    public const int DlssChoice = 3;
    public const int Fsr2Choice = 4;
#if ENABLE_UPSCALER_FRAMEWORK
    // URP's upscalers by name (the Upscaling choices in order); DLSS 4 and FSR 2 come with the NVIDIA and AMD modules.
    private static readonly string[] UpscalerNames =
        { "Bilinear", "FidelityFX Super Resolution 1.0", "Spatial-Temporal Post-Processing", "Deep Learning Super Sampling 4", "FidelityFX Super Resolution 2" };
    private const string StpName = "Spatial-Temporal Post-Processing";
    private static DLSSOptions dlssOptions;
    private static FSR2Options fsr2Options;
    private static bool? dlssAvailable;
    private static bool? fsr2Available;
#endif

    /// <summary>The upscaler in use after the last Apply (Options shows when DLSS or FSR 2 can't run here).</summary>
    public static string ActiveUpscaler { get; private set; } = "";

    private static readonly Dictionary<Camera, AntialiasingMode> AuthoredCameras = new Dictionary<Camera, AntialiasingMode>();
    private static readonly Dictionary<Terrain, (float density, float distance)> AuthoredTerrains = new Dictionary<Terrain, (float, float)>();

    private static RenderPipelineAsset authoredForQuality;
    private static UniversalRenderPipelineAsset authored;
    private static UniversalRenderPipelineAsset runtime;
    private static ScriptableRendererFeature occlusion;
    private static bool occlusionWasOn;
    private static float authoredLodBias;
    private static int authoredMipLimit;
    private static bool started;

    /// <summary>Makes the working copy of the pipeline asset, once. False without URP.</summary>
    private static bool Start()
    {
        if (started)
        {
            return runtime != null;
        }

        started = true;
        authoredForQuality = QualitySettings.renderPipeline;
        authored = (authoredForQuality != null ? authoredForQuality : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (authored == null)
        {
            Debug.LogWarning("Graphics options need a Universal Render Pipeline asset; they are off.");
            return false;
        }

        authoredLodBias = QualitySettings.lodBias;
        authoredMipLimit = QualitySettings.globalTextureMipmapLimit;
        runtime = Object.Instantiate(authored);
        runtime.name = authored.name + " (Options)";
        runtime.hideFlags = HideFlags.HideAndDontSave;
        QualitySettings.renderPipeline = runtime;
        foreach (ScriptableRendererData renderer in authored.rendererDataList)
        {
            if (renderer == null)
            {
                continue;
            }

            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                // Its class is internal to URP.
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    occlusion = feature;
                    occlusionWasOn = feature.isActive;
                }
            }
        }

        Application.quitting += Restore;
        return true;
    }

    /// <summary>Carries out everything that isn't per scene.</summary>
    public static void Apply()
    {
        if (!Start())
        {
            return;
        }

        runtime.renderScale = Mathf.Min(GameOptions.RenderScale.Value, authored.renderScale);
        ApplyUpscaler();
        runtime.msaaSampleCount = MsaaSamples[GameOptions.AntiAliasing.Index];

        int shadows = GameOptions.Shadows.Index;
        runtime.mainLightShadowmapResolution = shadows >= UltraShadows
            ? ShadowResolution[UltraShadows]
            : Mathf.Min(ShadowResolution[shadows], authored.mainLightShadowmapResolution);
        runtime.shadowDistance = authored.shadowDistance * ShadowDistanceShare[shadows];
        runtime.shadowCascadeCount = Mathf.Min(ShadowCascades[shadows], authored.shadowCascadeCount);
        runtime.additionalLightsShadowmapResolution = AdditionalShadowAtlas[shadows];

        if (occlusion != null)
        {
            occlusion.SetActive(occlusionWasOn && GameOptions.AmbientOcclusion.On);
        }

        AtmosphereFeature.QualityScale = FogShare[GameOptions.VolumetricFog.Index];
        QualitySettings.lodBias = authoredLodBias * LodShare[GameOptions.ViewDistance.Index];
        QualitySettings.globalTextureMipmapLimit = Mathf.Max(authoredMipLimit, MipLimit[GameOptions.TextureQuality.Index]);
        ApplyToScene();
    }

    // Below 100% Render Scale the chosen upscaler brings the picture up. DLSS and FSR 2 rebuild it from several frames, and
    // the Render Scale picks their quality mode (they set the resolution themselves): 100% is DLAA for DLSS (native, only
    // its anti-aliasing) and Quality for FSR 2, 75% Quality, 60% Balanced, 50% Performance, below that Ultra Performance.
    // On a card or graphics API that can't run them they fall back to STP.
    private static void ApplyUpscaler()
    {
        int choice = Mathf.Clamp(GameOptions.Upscaling.Index, 0, Upscalers.Length - 1);
        bool scaled = GameOptions.RenderScale.Value < 0.999f;
#if ENABLE_UPSCALER_FRAMEWORK
        float scale = GameOptions.RenderScale.Value;
        if (choice == DlssChoice && DlssAvailable())
        {
            dlssOptions ??= NewOptions<DLSSOptions>(UpscalerNames[DlssChoice]);
            dlssOptions.dlssQualityMode = scale >= 0.999f ? UnityEngine.NVIDIA.DLSSQuality.DLAA
                : scale >= 0.7f ? UnityEngine.NVIDIA.DLSSQuality.MaximumQuality
                : scale >= 0.55f ? UnityEngine.NVIDIA.DLSSQuality.Balanced
                : scale >= 0.45f ? UnityEngine.NVIDIA.DLSSQuality.MaximumPerformance
                : UnityEngine.NVIDIA.DLSSQuality.UltraPerformance;
            Use(dlssOptions);
            runtime.renderScale = 1f;
            runtime.upscalerName = UpscalerNames[DlssChoice];
        }
        else if (choice == Fsr2Choice && Fsr2Available())
        {
            fsr2Options ??= NewOptions<FSR2Options>(UpscalerNames[Fsr2Choice]);
            fsr2Options.fsr2QualityMode = scale >= 0.7f ? UnityEngine.AMD.FSR2Quality.Quality
                : scale >= 0.55f ? UnityEngine.AMD.FSR2Quality.Balanced
                : scale >= 0.45f ? UnityEngine.AMD.FSR2Quality.Performance
                : UnityEngine.AMD.FSR2Quality.UltraPerformance;
            Use(fsr2Options);
            runtime.renderScale = 1f;
            runtime.upscalerName = UpscalerNames[Fsr2Choice];
        }
        else
        {
            string wanted = choice >= DlssChoice ? StpName : UpscalerNames[choice];
            runtime.upscalerName = scaled ? wanted : authored.upscalerName;
        }

        ActiveUpscaler = runtime.upscalerName;
#else
        runtime.upscalingFilter = scaled ? Upscalers[choice] : authored.upscalingFilter;
        ActiveUpscaler = runtime.upscalingFilter.ToString();
#endif
    }

#if ENABLE_UPSCALER_FRAMEWORK
    // The working copy's own options: the pipeline asset's sub-assets are shared with the project and must not change.
    private static T NewOptions<T>(string upscaler) where T : UpscalerOptions
    {
        var options = ScriptableObject.CreateInstance<T>();
        options.hideFlags = HideFlags.HideAndDontSave;
        options.upscalerName = upscaler;
        return options;
    }

    private static void Use(UpscalerOptions options)
    {
        List<UpscalerOptions> list = runtime.upscalerOptions;
        list.RemoveAll(o => o == null || o.upscalerName == options.upscalerName);
        list.Add(options);
    }

    /// <summary>True when this card and graphics API can run DLSS (an NVIDIA RTX card).</summary>
    public static bool DlssAvailable() => dlssAvailable ??= Check(typeof(DLSSIUpscaler), "CheckDLSSFeatureAvailable");

    /// <summary>True when the AMD plugin runs here: most cards, but only on DirectX 12 or Vulkan. On DirectX 11 URP's own
    /// check says yes, and the plugin then throws inside ffxFsr2GetRenderResolutionFromQualityMode and takes the whole
    /// game down (it crashed the Editor, 2026-11-12).</summary>
    public static bool Fsr2Available() => fsr2Available ??=
        (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
        && Check(typeof(FSR2IUpscaler), "CheckFSR2FeatureAvailable");

    // URP's own check for each, which is private to it.
    private static bool Check(System.Type upscaler, string method)
    {
        System.Reflection.MethodInfo check = upscaler.GetMethod(method, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        try
        {
            return check != null && (bool)check.Invoke(null, null);
        }
        catch (System.Exception)
        {
            return false;
        }
    }
#else
    public static bool DlssAvailable() => false;
    public static bool Fsr2Available() => false;
#endif

    /// <summary>The terrains of the loaded scene: their surface detail, grass and small plants.</summary>
    public static void ApplyToScene()
    {
        bool temporal = GameOptions.AntiAliasing.Index == TemporalChoice;
        var goneCameras = new List<Camera>();
        foreach (Camera camera in AuthoredCameras.Keys)
        {
            if (camera == null)
            {
                goneCameras.Add(camera);
            }
        }

        foreach (Camera camera in goneCameras)
        {
            AuthoredCameras.Remove(camera);
        }

        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!camera.CompareTag("MainCamera") || !camera.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                continue;
            }

            if (!AuthoredCameras.ContainsKey(camera))
            {
                AuthoredCameras[camera] = data.antialiasing;
            }

            data.antialiasing = temporal ? AntialiasingMode.TemporalAntiAliasing : AuthoredCameras[camera];
            if (temporal)
            {
                data.taaSettings.quality = TemporalAAQuality.High;
            }
        }

        var detail = (TerrainSurface.DetailQuality)GameOptions.TerrainDetail.Index;
        foreach (TerrainSurface surface in Object.FindObjectsByType<TerrainSurface>(FindObjectsInactive.Include))
        {
            surface.SetQuality(detail);
        }

        float grass = GameOptions.GrassDensity.Value;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (!AuthoredTerrains.TryGetValue(terrain, out (float density, float distance) authoredGrass))
            {
                authoredGrass = (terrain.detailObjectDensity, terrain.detailObjectDistance);
                AuthoredTerrains[terrain] = authoredGrass;
            }

            terrain.detailObjectDensity = authoredGrass.density * grass;
            terrain.detailObjectDistance = authoredGrass.distance * Mathf.Lerp(0.6f, 1f, grass);
        }
    }

    /// <summary>Terrains of a scene that has gone are forgotten.</summary>
    public static void ForgetUnloadedTerrains()
    {
        var gone = new List<Terrain>();
        foreach (Terrain terrain in AuthoredTerrains.Keys)
        {
            if (terrain == null)
            {
                gone.Add(terrain);
            }
        }

        foreach (Terrain terrain in gone)
        {
            AuthoredTerrains.Remove(terrain);
        }
    }

    // Playing in the Editor must leave the project as it found it.
    private static void Restore()
    {
        Application.quitting -= Restore;
        if (occlusion != null)
        {
            occlusion.SetActive(occlusionWasOn);
        }

        if (runtime != null)
        {
            QualitySettings.renderPipeline = authoredForQuality;
            Object.Destroy(runtime);
        }

        QualitySettings.lodBias = authoredLodBias;
        QualitySettings.globalTextureMipmapLimit = authoredMipLimit;
        AtmosphereFeature.QualityScale = 1f;
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        started = false;
        runtime = null;
        authored = null;
        authoredForQuality = null;
        occlusion = null;
        AuthoredTerrains.Clear();
        AuthoredCameras.Clear();
    }
}
