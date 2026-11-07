using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The renderer feature behind FilmicTonemapping (on PC_Renderer, after the Atmosphere feature so the fog is in what it
/// maps): one full-screen pass before post-processing while a volume asks for a curve. Game and Scene view cameras;
/// not cameras rendering into a texture (portraits, icons), whose look is set by what they show.
/// Shader: Shaders/FilmicTonemap.shader ("Hidden/Safe Haven/Filmic Tonemap").
/// </summary>
public sealed class TonemapFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material material;
    private TonemapPass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Safe Haven/Filmic Tonemap");
        }

        if (shader == null)
        {
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new TonemapPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null)
        {
            return;
        }

        CameraType type = renderingData.cameraData.cameraType;
        if ((type != CameraType.Game && type != CameraType.SceneView) || renderingData.cameraData.camera.targetTexture != null
            || !renderingData.cameraData.postProcessEnabled)
        {
            return;
        }

        var settings = VolumeManager.instance.stack.GetComponent<FilmicTonemapping>();
        if (settings == null || !settings.IsActive())
        {
            return;
        }

        pass.Setup(settings);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class TonemapPass : ScriptableRenderPass
    {
        private static readonly int ExposureId = Shader.PropertyToID("_TonemapExposure");
        private static readonly int SaturationId = Shader.PropertyToID("_TonemapSaturation");

        private readonly Material material;
        private int curvePass;

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public int pass;
        }

        public TonemapPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("Filmic Tonemap");
        }

        public void Setup(FilmicTonemapping settings)
        {
            curvePass = settings.curve.value == FilmicCurve.Neutral ? 0 : settings.curve.value == FilmicCurve.AgX ? 1 : 2;
            material.SetFloat(ExposureId, Mathf.Pow(2f, settings.exposure.value));
            material.SetFloat(SaturationId, settings.saturation.value);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle color = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.name = "_CameraColorTonemapped";
            desc.clearBuffer = false;
            TextureHandle target = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Filmic Tonemap", out var data, profilingSampler))
            {
                data.material = material;
                data.source = color;
                data.pass = curvePass;
                builder.UseTexture(color);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }

            resources.cameraColor = target;
        }
    }
}
