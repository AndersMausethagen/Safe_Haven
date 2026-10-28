using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The renderer feature behind AtmosphericFog and FogOfWar (on PC_Renderer). Two passes before post-processing: a ray
/// march at half resolution gathers the fog's light and transmittance along each view ray (sun or moon through its
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

    [SerializeField] private Shader shader;
    [Tooltip("Gathers the fog at this share of the screen's resolution.")]
    [SerializeField, Range(0.25f, 1f)] private float fogResolution = 0.5f;

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
        bool fogOn = fog != null && fog.active && (fog.IsActive() || FogArea.Active.Count > 0);
        bool warOn = fogOfWar != null && fogOfWar.IsActive();
        if (!fogOn && !warOn)
        {
            AtmosphereConditions.FogDensity = 0f;
            return;
        }

        pass.Setup(fog, fogOn, fogOfWar, warOn, fogResolution);
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
        private static readonly int FogParams0 = Shader.PropertyToID("_FogParams0");
        private static readonly int FogParams1 = Shader.PropertyToID("_FogParams1");
        private static readonly int FogParams2 = Shader.PropertyToID("_FogParams2");
        private static readonly int FogAlbedo = Shader.PropertyToID("_FogAlbedo");
        private static readonly int FogAmbient = Shader.PropertyToID("_FogAmbient");
        private static readonly int FogWind = Shader.PropertyToID("_FogWind");
        private static readonly int LightCount = Shader.PropertyToID("_FogLightCount");
        private static readonly int LightPositions = Shader.PropertyToID("_FogLightPositions");
        private static readonly int LightColors = Shader.PropertyToID("_FogLightColors");
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

        private readonly Material material;
        private readonly Vector4[] lightPositions = new Vector4[MaxLights];
        private readonly Vector4[] lightColors = new Vector4[MaxLights];
        private readonly Vector4[] sources = new Vector4[MaxSources];
        private readonly Vector4[] areas = new Vector4[MaxAreas];
        private readonly Vector4[] areaShapes = new Vector4[MaxAreas];
        private readonly List<FogArea> areaOrder = new List<FogArea>();
        private readonly List<AtmosphereLight> lightOrder = new List<AtmosphereLight>();
        private readonly List<VisibilitySource> sourceOrder = new List<VisibilitySource>();
        private AtmosphericFog fog;
        private FogOfWar fogOfWar;
        private bool fogOn;
        private bool warOn;
        private float resolution;

        public AtmospherePass(Material material)
        {
            this.material = material;
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

            // Without fog the composite still needs a transmittance of 1: the cleared texture is it.
            if (fogOn)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Atmosphere March", out var data, profilingSampler))
                {
                    data.material = material;
                    data.pass = 0;
                    builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(fogTexture, FogTextureId);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
                }
            }
            else
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Atmosphere Clear", out var data, profilingSampler))
                {
                    builder.SetRenderAttachment(fogTexture, 0, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(fogTexture, FogTextureId);
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
            float weather = fogOn ? Mathf.Lerp(1f, AtmosphereConditions.WeatherFog, fog.weatherInfluence.value) : 1f;
            AtmosphereConditions.FogDensity = fogOn ? fog.density.value * weather : 0f;
            if (fogOn)
            {
                material.SetVector(FogParams0, new Vector4(fog.density.value * weather, fog.baseHeight.value, fog.heightFalloff.value, fog.anisotropy.value));
                material.SetVector(FogParams1, new Vector4(fog.mainLight.value, fog.localLights.value, 1f / fog.noiseSize.value, fog.noiseStrength.value));
                material.SetVector(FogParams2, new Vector4(fog.maxDistance.value, fog.steps.value, fog.clearing.value, 0f));
                material.SetColor(FogAlbedo, fog.albedo.value.linear);
                material.SetColor(FogAmbient, fog.ambient.value.linear);
                material.SetVector(FogWind, fog.wind.value);

                FogGround ground = FogGround.Current;
                bool follow = ground != null && ground.Texture != null && fog.followTerrain.value > 0f;
                material.SetVector(FogParams3, new Vector4(follow ? fog.followTerrain.value : 0f, 1f / fog.bankSize.value,
                    fog.bankStrength.value, fog.bankSpeed.value));
                if (follow)
                {
                    Rect area = ground.Area;
                    material.SetTexture(GroundTexture, ground.Texture);
                    material.SetVector(GroundRect, new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height));
                    material.SetVector(GroundRange, ground.HeightRange);
                }

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
            }

            material.SetFloat(LightCount, lights);
            material.SetVectorArray(LightPositions, lightPositions);
            material.SetVectorArray(LightColors, lightColors);

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
