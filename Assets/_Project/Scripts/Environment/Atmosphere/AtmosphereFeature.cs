using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The renderer feature behind AtmosphericFog and FogOfWar (on PC_Renderer). Two passes before post-processing: a ray
/// march at half resolution gathers the fog's light and transmittance plus separate colour-preserving shafts along each view ray (sun or moon through its
/// shadow cascades, the nearest AtmosphereLights, thinned around VisibilitySources), then a full-resolution composite
/// lays the fog over the scene and adds the fog of war: unexplored ground (ExplorationMap) and, at night, the grey
/// veil beyond every visibility source. Game cameras only, so the Scene view stays clear for editing, and not cameras
/// that render into a texture (portraits, icons).
/// Shader: Shaders/Atmosphere.shader ("Hidden/Safe Haven/Atmosphere").
/// </summary>
public sealed class AtmosphereFeature : ScriptableRendererFeature
{
    /// <summary>Fog lights shaded per frame, nearest to the camera first.</summary>
    public const int MaxLights = 8;
    /// <summary>Visibility sources (sight, torches, the ward) per frame, nearest to the camera first.</summary>
    public const int MaxSources = 16;
    /// <summary>Fog areas per frame, nearest to the camera first.</summary>
    public const int MaxAreas = 8;
    public const int MaxMists = 4;
    public const int MaxMistVolumes = 4;

    [SerializeField] private Shader shader;
    [Tooltip("Gathers the fog at this share of the screen's resolution.")]
    [SerializeField, Range(0.25f, 1f)] private float fogResolution = 0.5f;

    /// <summary>Share of Fog Resolution the fog is drawn at: the player's Volumetric Fog setting. 1 is as authored.</summary>
    public static float QualityScale { get; set; } = 1f;

    private Material material;
    private AtmospherePass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Safe Haven/Atmosphere");
        }

        if (shader == null)
        {
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new AtmospherePass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || material == null)
        {
            return;
        }

        CameraType type = renderingData.cameraData.cameraType;
        // Cameras that render into a texture (the dialogue portrait, icons) are close-ups without fog; the march's
        // buffers are sized for the screen, so on them it would leave a dark band.
        if (type != CameraType.Game || renderingData.cameraData.camera.targetTexture != null)
        {
            return;
        }

        VolumeStack stack = VolumeManager.instance.stack;
        var fog = stack.GetComponent<AtmosphericFog>();
        var fogOfWar = stack.GetComponent<FogOfWar>();
        // Fog areas show even in a scene without base fog.
        bool fogOn = fog != null && fog.active && (fog.IsActive() || FogArea.Active.Count > 0
            || TerrainMist.Active.Count > 0 || MistVolume.Active.Count > 0);
        bool warOn = fogOfWar != null && fogOfWar.IsActive();
        if (!fogOn && !warOn)
        {
            AtmosphereConditions.FogDensity = 0f;
            return;
        }

        pass.Setup(fog, fogOn, fogOfWar, warOn, Mathf.Max(0.25f, fogResolution * QualityScale));
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class AtmospherePass : ScriptableRenderPass
    {
        private static readonly int FogTextureId = Shader.PropertyToID("_AtmosphereFog");
        private static readonly int ShaftTextureId = Shader.PropertyToID("_AtmosphereShafts");
        private static readonly int FogParams0 = Shader.PropertyToID("_FogParams0");
        private static readonly int FogParams1 = Shader.PropertyToID("_FogParams1");
        private static readonly int FogParams2 = Shader.PropertyToID("_FogParams2");
        private static readonly int FogAlbedo = Shader.PropertyToID("_FogAlbedo");
        private static readonly int FogAmbient = Shader.PropertyToID("_FogAmbient");
        private static readonly int FogWind = Shader.PropertyToID("_FogWind");
        private static readonly int LightCount = Shader.PropertyToID("_FogLightCount");
        private static readonly int LightPositions = Shader.PropertyToID("_FogLightPositions");
        private static readonly int LightColors = Shader.PropertyToID("_FogLightColors");
        private static readonly int LightClearings = Shader.PropertyToID("_FogLightClearings");
        private static readonly int SourceCount = Shader.PropertyToID("_SourceCount");
        private static readonly int Sources = Shader.PropertyToID("_Sources");
        private static readonly int WarParams = Shader.PropertyToID("_WarParams");
        private static readonly int UnexploredColor = Shader.PropertyToID("_UnexploredColor");
        private static readonly int VeilColor = Shader.PropertyToID("_VeilColor");
        private static readonly int ExploreTexture = Shader.PropertyToID("_ExploreTex");
        private static readonly int ExploreRect = Shader.PropertyToID("_ExploreRect");
        private static readonly int WarGround = Shader.PropertyToID("_WarGround");
        private static readonly int FogParams3 = Shader.PropertyToID("_FogParams3");
        private static readonly int GroundTexture = Shader.PropertyToID("_GroundTex");
        private static readonly int GroundRect = Shader.PropertyToID("_GroundRect");
        private static readonly int GroundRange = Shader.PropertyToID("_GroundRange");
        private static readonly int AreaCount = Shader.PropertyToID("_FogAreaCount");
        private static readonly int Areas = Shader.PropertyToID("_FogAreas");
        private static readonly int AreaShapes = Shader.PropertyToID("_FogAreaShapes");
        private static readonly int ShaftParams = Shader.PropertyToID("_ShaftParams");
        private static readonly int ShaftColor = Shader.PropertyToID("_ShaftColor");
        private static readonly int MistCount = Shader.PropertyToID("_TerrainMistCount");
        private static readonly int MistRects = Shader.PropertyToID("_TerrainMistRects");
        private static readonly int MistShapes = Shader.PropertyToID("_TerrainMistShapes");
        private static readonly int MistClouds = Shader.PropertyToID("_TerrainMistClouds");
        private static readonly int MistWinds = Shader.PropertyToID("_TerrainMistWinds");
        private static readonly int MistTints = Shader.PropertyToID("_TerrainMistTints");
        private static readonly int ShadowCascades = Shader.PropertyToID("_AtmosphereShadowCascades");

        private readonly Material material;
        private readonly Vector4[] lightPositions = new Vector4[MaxLights];
        private readonly Vector4[] lightColors = new Vector4[MaxLights];
        private readonly Vector4[] lightClearings = new Vector4[MaxLights];
        private readonly Vector4[] sources = new Vector4[MaxSources];
        private readonly Vector4[] areas = new Vector4[MaxAreas];
        private readonly Vector4[] areaShapes = new Vector4[MaxAreas];
        private readonly List<FogArea> areaOrder = new List<FogArea>();
        private readonly List<AtmosphereLight> lightOrder = new List<AtmosphereLight>();
        private readonly List<VisibilitySource> sourceOrder = new List<VisibilitySource>();
        private readonly List<TerrainMist> mistOrder = new List<TerrainMist>();
        private readonly Vector4[] mistRects = new Vector4[MaxMists];
        private readonly Vector4[] mistShapes = new Vector4[MaxMists];
        private readonly Vector4[] mistClouds = new Vector4[MaxMists];
        private readonly Vector4[] mistWinds = new Vector4[MaxMists];
        private readonly Vector4[] mistTints = new Vector4[MaxMists];
        private readonly Vector4[] mistWaves = new Vector4[MaxMists];
        private readonly Vector4[] mistPooling = new Vector4[MaxMists];
        private readonly List<MistVolume> volumeOrder = new List<MistVolume>();
        private readonly Matrix4x4[] volumeTransforms = new Matrix4x4[MaxMistVolumes];
        private readonly Vector4[] volumeSizes = new Vector4[MaxMistVolumes];
        private readonly Vector4[] volumeShapes = new Vector4[MaxMistVolumes];
        private readonly Vector4[] volumeClouds = new Vector4[MaxMistVolumes];
        private readonly Vector4[] volumeWinds = new Vector4[MaxMistVolumes];
        private readonly Vector4[] volumeTints = new Vector4[MaxMistVolumes];
        private readonly Vector4[] volumeWaves = new Vector4[MaxMistVolumes];
        private AtmosphericFog fog;
        private FogOfWar fogOfWar;
        private bool fogOn;
        private bool warOn;
        private float resolution;
        private float extraWindTime;
        private float lastWindTime;

        public AtmospherePass(Material material)
        {
            this.material = material;
            lastWindTime = Time.time;
            profilingSampler = new ProfilingSampler("Safe Haven Atmosphere");
        }

        public void Setup(AtmosphericFog fogSettings, bool fogActive, FogOfWar war, bool warActive, float fogResolution)
        {
            fog = fogSettings;
            fogOn = fogActive;
            fogOfWar = war;
            warOn = warActive;
            resolution = fogResolution;
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public int pass;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            UploadParameters(camera.camera.transform.position);
            var pipeline = UniversalRenderPipeline.asset;
            material.SetFloat(ShadowCascades, pipeline != null ? pipeline.shadowCascadeCount : 1);

            TextureHandle color = resources.activeColorTexture;
            TextureDesc fogDesc = renderGraph.GetTextureDesc(color);
            fogDesc.name = "_AtmosphereFog";
            fogDesc.width = Mathf.Max(1, Mathf.RoundToInt(fogDesc.width * resolution));
            fogDesc.height = Mathf.Max(1, Mathf.RoundToInt(fogDesc.height * resolution));
            fogDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            fogDesc.depthBufferBits = DepthBits.None;
            fogDesc.msaaSamples = MSAASamples.None;
            fogDesc.clearBuffer = true;
            fogDesc.clearColor = new Color(0f, 0f, 0f, 1f);
            fogDesc.filterMode = FilterMode.Bilinear;
            TextureHandle fogTexture = renderGraph.CreateTexture(fogDesc);
            TextureDesc shaftDesc = fogDesc;
            shaftDesc.name = "_AtmosphereShafts";
            shaftDesc.clearColor = Color.clear;
            TextureHandle shaftTexture = renderGraph.CreateTexture(shaftDesc);

            // Without fog the composite still needs a transmittance of 1: the cleared texture is it.
            if (fogOn)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Atmosphere March", out var data, profilingSampler))
                {
                    data.material = material;
                    data.pass = 0;
                    builder.UseTexture(resources.cameraDepthTexture);
                    if (resources.mainShadowsTexture.IsValid())
                    {
                        builder.UseTexture(resources.mainShadowsTexture);
                    }
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(shaftTexture, 1, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(fogTexture, FogTextureId);
                    builder.SetGlobalTextureAfterPass(shaftTexture, ShaftTextureId);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
                }
            }
            else
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Atmosphere Clear", out var data, profilingSampler))
                {
                    builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachment(shaftTexture, 1, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(fogTexture, FogTextureId);
                    builder.SetGlobalTextureAfterPass(shaftTexture, ShaftTextureId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) => { });
                }
            }

            TextureDesc targetDesc = renderGraph.GetTextureDesc(color);
            targetDesc.name = "_CameraColorAtmosphere";
            targetDesc.clearBuffer = false;
            TextureHandle target = renderGraph.CreateTexture(targetDesc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Atmosphere Composite", out var data, profilingSampler))
            {
                data.material = material;
                data.source = color;
                data.pass = 1;
                builder.UseTexture(color);
                builder.UseTexture(fogTexture);
                builder.UseTexture(shaftTexture);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }

            resources.cameraColor = target;
        }

        private void UploadParameters(Vector3 cameraPosition)
        {
            float now = Time.time;
            if (now < lastWindTime)
                extraWindTime = 0f;
            extraWindTime += Mathf.Max(0f, now - lastWindTime) * (AtmosphereConditions.WeatherWind - 1f);
            lastWindTime = now;
            float weather = fogOn ? AtmosphereConditions.FogMultiplier(fog.weatherInfluence.value) : 1f;
            AtmosphereConditions.FogDensity = fogOn ? fog.density.value * weather : 0f;
            if (fogOn)
            {
                material.SetVector(FogParams0, new Vector4(fog.density.value * weather, fog.baseHeight.value, fog.heightFalloff.value, fog.anisotropy.value));
                material.SetVector(FogParams1, new Vector4(fog.mainLight.value, fog.localLights.value, 1f / fog.noiseSize.value, fog.noiseStrength.value));
                material.SetVector(FogParams2, new Vector4(fog.maxDistance.value, fog.steps.value, fog.clearing.value, 0f));
                material.SetColor(FogAlbedo, fog.albedo.value.linear);
                material.SetColor(FogAmbient, fog.ambient.value.linear);
                Vector3 wind = fog.wind.value;
                material.SetVector(FogWind, new Vector4(wind.x, wind.y, wind.z, extraWindTime * fog.weatherWindInfluence.value));
                material.SetFloat("_FogDriftSpeed", fog.driftSpeed.value);
                material.SetVector(ShaftParams, new Vector4(fog.shaftDensity.value, fog.shaftIntensity.value, fog.shaftHeight.value, fog.shaftGlow.value));
                material.SetColor(ShaftColor, fog.shaftColor.value.linear);

                FogGround ground = FogGround.Current;
                bool hasGround = ground != null && ground.Texture != null;
                material.SetVector(FogParams3, new Vector4(hasGround ? fog.followTerrain.value : 0f, 1f / fog.bankSize.value,
                    fog.bankStrength.value, fog.bankSpeed.value));
                material.SetFloat("_HasFogGround", hasGround ? 1f : 0f);
                material.SetFloat("_HasMistHabitat", hasGround ? 1f : 0f);
                if (hasGround)
                {
                    Rect area = ground.Area;
                    material.SetTexture(GroundTexture, ground.Texture);
                    material.SetVector(GroundRect, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
                    material.SetVector(GroundRange, ground.HeightRange);
                }

                mistOrder.Clear();
                foreach (TerrainMist mist in TerrainMist.Active)
                {
                    if (mist != null && mist.Density > 0f && mist.Terrain != null && mist.Terrain.terrainData != null)
                    {
                        mistOrder.Add(mist);
                    }
                }

                mistOrder.Sort((a, b) => a.DistanceSquared(cameraPosition).CompareTo(b.DistanceSquared(cameraPosition)));
                int mists = hasGround ? Mathf.Min(MaxMists, mistOrder.Count) : 0;
                for (int i = 0; i < mists; i++)
                {
                    TerrainMist mist = mistOrder[i];
                    Vector3 origin = mist.Terrain.transform.position;
                    Vector3 size = mist.Terrain.terrainData.size;
                    mistRects[i] = new Vector4(origin.x, origin.z, size.x, size.z);
                    mistShapes[i] = new Vector4(mist.EffectiveDensity, mist.Bottom, mist.Thickness, mist.EdgeFade);
                    mistClouds[i] = new Vector4(1f / mist.CloudSize, mist.Coverage, mist.Erosion, mist.FlowStretch);
                    mistWinds[i] = new Vector4(mist.Wind.x, mist.Wind.y, extraWindTime * mist.WeatherWindInfluence,
                        Mathf.Max(0f, AtmosphereConditions.WeatherWind - 0.4f) * mist.WeatherWindInfluence);
                    Color tint = mist.Tint;
                    mistTints[i] = new Vector4(tint.r, tint.g, tint.b, mist.SkyLight);
                    mistWaves[i] = mist.Waves;
                    mistPooling[i] = mist.Pooling;
                }

                material.SetInt(MistCount, mists);
                material.SetVectorArray(MistRects, mistRects);
                material.SetVectorArray(MistShapes, mistShapes);
                material.SetVectorArray(MistClouds, mistClouds);
                material.SetVectorArray(MistWinds, mistWinds);
                material.SetVectorArray(MistTints, mistTints);
                material.SetVectorArray("_TerrainMistWaves", mistWaves);
                material.SetVectorArray("_TerrainMistPooling", mistPooling);

                volumeOrder.Clear();
                foreach (MistVolume volume in MistVolume.Active)
                {
                    if (volume == null)
                        continue;
                    Vector3 scale = volume.transform.lossyScale;
                    if (volume.EffectiveDensity > 0f && Mathf.Abs(scale.x * scale.y * scale.z) > 1e-6f)
                        volumeOrder.Add(volume);
                }
                volumeOrder.Sort((a, b) => a.DistanceSquared(cameraPosition).CompareTo(b.DistanceSquared(cameraPosition)));
                int volumes = Mathf.Min(MaxMistVolumes, volumeOrder.Count);
                for (int i = 0; i < volumes; i++)
                {
                    MistVolume volume = volumeOrder[i];
                    Vector3 size = volume.Size;
                    volumeTransforms[i] = volume.transform.worldToLocalMatrix;
                    volumeSizes[i] = new Vector4(size.x, size.y, size.z, 0f);
                    volumeShapes[i] = new Vector4(volume.EffectiveDensity, 0f, size.y, volume.EdgeFade);
                    volumeClouds[i] = new Vector4(1f / volume.CloudSize, volume.Coverage, volume.Erosion, volume.FlowStretch);
                    volumeWinds[i] = new Vector4(volume.Wind.x, volume.Wind.y,
                        extraWindTime * volume.WeatherWindInfluence,
                        Mathf.Max(0f, AtmosphereConditions.WeatherWind - 0.4f) * volume.WeatherWindInfluence);
                    Color tint = volume.Tint;
                    volumeTints[i] = new Vector4(tint.r, tint.g, tint.b, volume.SkyLight);
                    volumeWaves[i] = volume.Waves;
                }
                material.SetInt("_MistVolumeCount", volumes);
                material.SetMatrixArray("_MistVolumeWorldToLocal", volumeTransforms);
                material.SetVectorArray("_MistVolumeSizes", volumeSizes);
                material.SetVectorArray("_MistVolumeShapes", volumeShapes);
                material.SetVectorArray("_MistVolumeClouds", volumeClouds);
                material.SetVectorArray("_MistVolumeWinds", volumeWinds);
                material.SetVectorArray("_MistVolumeTints", volumeTints);
                material.SetVectorArray("_MistVolumeWaves", volumeWaves);

                int areaCount = Nearest(FogArea.Active, areaOrder, cameraPosition, MaxAreas);
                for (int i = 0; i < areaCount; i++)
                {
                    FogArea fogArea = areaOrder[i];
                    Vector3 p = fogArea.transform.position;
                    float areaWeather = fogArea.FollowsWeather ? weather : 1f;
                    areas[i] = new Vector4(p.x, p.y, p.z, fogArea.Density * areaWeather);
                    areaShapes[i] = new Vector4(1f / fogArea.Radius, 1f / fogArea.Height, fogArea.Softness, 0f);
                }

                material.SetFloat(AreaCount, areaCount);
                material.SetVectorArray(Areas, areas);
                material.SetVectorArray(AreaShapes, areaShapes);
            }

            int lights = Nearest(AtmosphereLight.Active, lightOrder, cameraPosition, MaxLights);
            for (int i = 0; i < lights; i++)
            {
                AtmosphereLight light = lightOrder[i];
                Vector3 p = light.transform.position;
                lightPositions[i] = new Vector4(p.x, p.y, p.z, Mathf.Max(0.01f, light.Range));
                Color c = light.FogColor;
                lightColors[i] = new Vector4(c.r, c.g, c.b, 0f);
                lightClearings[i] = new Vector4(light.ClearingRadius, light.ClearingStrength, light.ClearingSoftness, 0f);
            }

            material.SetFloat(LightCount, lights);
            material.SetVectorArray(LightPositions, lightPositions);
            material.SetVectorArray(LightColors, lightColors);
            material.SetVectorArray(LightClearings, lightClearings);

            int count = Nearest(VisibilitySource.Active, sourceOrder, cameraPosition, MaxSources);
            for (int i = 0; i < count; i++)
            {
                VisibilitySource source = sourceOrder[i];
                Vector3 p = source.transform.position;
                sources[i] = new Vector4(p.x, p.z, source.Radius, source.Softness);
            }

            material.SetFloat(SourceCount, count);
            material.SetVectorArray(Sources, sources);

            ExplorationMap map = ExplorationMap.Current;
            bool explore = warOn && map != null && map.Texture != null;
            if (explore)
            {
                Rect area = map.Grid.Area;
                material.SetTexture(ExploreTexture, map.Texture);
                material.SetVector(ExploreRect, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
            }

            // The fog of war is laid on the ground the player stands on (or well below the camera without a player).
            material.SetFloat(WarGround, Player.Current != null ? Player.Current.transform.position.y : cameraPosition.y - 20f);
            float unexplored = explore ? fogOfWar.unexplored.value : 0f;
            float veil = warOn ? fogOfWar.nightVeil.value : 0f;
            material.SetVector(WarParams, new Vector4(unexplored, veil,
                warOn ? fogOfWar.veilDesaturation.value : 0f, warOn ? fogOfWar.veilDarkening.value : 0f));
            if (warOn)
            {
                material.SetColor(UnexploredColor, fogOfWar.unexploredColor.value.linear);
                Color veilColor = fogOfWar.veilColor.value.linear;
                veilColor.a = fogOfWar.veilHaze.value;
                material.SetColor(VeilColor, veilColor);
            }
        }

        // Fills `order` with the nearest `max` items to the camera and returns how many.
        private static int Nearest<T>(IReadOnlyList<T> items, List<T> order, Vector3 from, int max) where T : Component
        {
            order.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    order.Add(items[i]);
                }
            }

            if (order.Count > max)
            {
                order.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
            }

            return Mathf.Min(max, order.Count);
        }
    }
}
