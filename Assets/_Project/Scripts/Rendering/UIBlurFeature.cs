using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The blurred picture of the game that UIBackdrop lays behind full-screen windows (on PC_Renderer): while one is open, the
/// camera's finished image (after post-processing) is taken down to a quarter and blurred twice each way into a texture
/// that lasts from frame to frame (<see cref="Output"/>). Nothing runs while no window wants it.
/// Shader: Shaders/UIBlur.shader ("Hidden/Safe Haven/UI Blur").
/// </summary>
public sealed class UIBlurFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [Tooltip("The blur's size: a share of the screen's resolution it is drawn at.")]
    [SerializeField, Range(0.125f, 0.5f)] private float resolution = 0.25f;

    private Material material;
    private BlurPass pass;

    /// <summary>The latest blurred picture, or null before the first.</summary>
    public static RenderTexture Output { get; private set; }

    /// <summary>Set by UIBackdrop while a backdrop is showing.</summary>
    public static bool Wanted { get; set; }

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Safe Haven/UI Blur");
        }

        if (shader == null)
        {
            return;
        }

        // Create runs again on every change to the renderer (OnValidate): the material is made once, the old pass's copy and
        // targets let go (EngineHazardTests).
        if (material == null || material.shader != shader)
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }

        pass?.Dispose();
        pass = new BlurPass(material, resolution) { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Wanted || pass == null || renderingData.cameraData.cameraType != CameraType.Game
            || renderingData.cameraData.camera.targetTexture != null || renderingData.cameraData.camera != Camera.main)
        {
            return;
        }

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
        Output = null;
    }

    private sealed class BlurPass : ScriptableRenderPass
    {
        private static readonly int TexelId = Shader.PropertyToID("_BlurTexel");
        private readonly Material material;
        // A command buffer reads a material when it runs, not when it is recorded: one material for the step down from
        // the screen and one for the blur, each with its own texel size.
        private readonly Material blur;
        private readonly float share;
        private RTHandle result;
        private RTHandle scratch;

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public int pass;
        }

        public BlurPass(Material material, float share)
        {
            this.material = material;
            blur = new Material(material) { hideFlags = HideFlags.HideAndDontSave };
            this.share = share;
            profilingSampler = new ProfilingSampler("UI Backdrop Blur");
        }

        public void Dispose()
        {
            Release();
            CoreUtils.Destroy(blur);
        }

        public void Release()
        {
            result?.Release();
            scratch?.Release();
            result = null;
            scratch = null;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle color = resources.activeColorTexture;
            TextureDesc source = renderGraph.GetTextureDesc(color);
            int width = Mathf.Max(8, Mathf.RoundToInt(source.width * share));
            int height = Mathf.Max(8, Mathf.RoundToInt(source.height * share));
            if (result == null || result.rt.width != width || result.rt.height != height)
            {
                Release();
                result = RTHandles.Alloc(width, height, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, filterMode: FilterMode.Bilinear,
                    wrapMode: TextureWrapMode.Clamp, name: "_UIBackdrop");
                scratch = RTHandles.Alloc(width, height, colorFormat: GraphicsFormat.B10G11R11_UFloatPack32, filterMode: FilterMode.Bilinear,
                    wrapMode: TextureWrapMode.Clamp, name: "_UIBackdropScratch");
            }

            TextureHandle a = renderGraph.ImportTexture(result);
            TextureHandle b = renderGraph.ImportTexture(scratch);
            material.SetVector(TexelId, new Vector4(1f / source.width, 1f / source.height, 0f, 0f));
            blur.SetVector(TexelId, new Vector4(1f / width, 1f / height, 0f, 0f));
            Blit(renderGraph, material, color, a, 0);
            for (int i = 0; i < 2; i++)
            {
                Blit(renderGraph, blur, a, b, 1);
                Blit(renderGraph, blur, b, a, 2);
            }

            Output = result.rt;
        }

        private void Blit(RenderGraph renderGraph, Material with, TextureHandle from, TextureHandle to, int pass)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("UI Backdrop Blur", out var data, profilingSampler))
            {
                data.material = with;
                data.source = from;
                data.pass = pass;
                builder.UseTexture(from);
                builder.SetRenderAttachment(to, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }
        }
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Wanted = false;
    }
}
