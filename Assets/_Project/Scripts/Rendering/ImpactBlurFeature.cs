using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A brief radial blur out from a great blow (Docs/LookAndFeelResearch_2026-11-18.md V6, "boss stop"): the picture streaks away from the
/// point the blow landed and settles over a moment. Played by <see cref="ImpactBlur.Play"/> (CombatFeedback on a boss's death);
/// the renderer feature (on PC_Renderer) enqueues its one full-screen pass only while a blur plays, so it costs nothing otherwise.
/// Game cameras only, before post-processing. Shader: Shaders/ImpactBlur.shader ("Hidden/Safe Haven/Impact Blur").
/// </summary>
public sealed class ImpactBlurFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material material;
    private BlurPass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Safe Haven/Impact Blur");
        }

        if (shader == null)
        {
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new BlurPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || !ImpactBlur.TryGet(out Vector2 centre, out float strength))
        {
            return;
        }

        if (renderingData.cameraData.cameraType != CameraType.Game || renderingData.cameraData.camera.targetTexture != null)
        {
            return;
        }

        pass.Setup(centre, strength);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class BlurPass : ScriptableRenderPass
    {
        private static readonly int BlurId = Shader.PropertyToID("_ImpactBlur");
        private readonly Material material;

        private class PassData
        {
            public Material material;
            public TextureHandle source;
        }

        public BlurPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("Impact Blur");
        }

        public void Setup(Vector2 centre, float strength) => material.SetVector(BlurId, new Vector4(centre.x, centre.y, strength, 0f));

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle color = resources.activeColorTexture;
            TextureDesc desc = renderGraph.GetTextureDesc(color);
            desc.name = "_CameraColorImpactBlur";
            desc.clearBuffer = false;
            TextureHandle target = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Impact Blur", out var data, profilingSampler))
            {
                data.material = material;
                data.source = color;
                builder.UseTexture(color);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
            }

            resources.cameraColor = target;
        }
    }
}

/// <summary>
/// The impact blur's state (<see cref="ImpactBlurFeature"/>): where on the screen it streaks from and how strongly, easing out over
/// its seconds. Real time, so a hit stop doesn't hold it. Scaled by Options › Screen Flashes (0 turns it off).
/// </summary>
public static class ImpactBlur
{
    private static Vector2 centre;
    private static float strength;
    private static float startedAt;
    private static float seconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        strength = 0f;
        seconds = 0f;
    }

    /// <summary>Streaks the picture away from <paramref name="worldPoint"/> by <paramref name="amount"/> (a share of the screen at its
    /// strongest, about 0.05) for <paramref name="duration"/> real seconds.</summary>
    public static void Play(Vector3 worldPoint, float amount, float duration)
    {
        Camera view = Camera.main;
        float scale = Mathf.Clamp01(GameOptions.ScreenFlash.Value);
        if (view == null || amount <= 0f || duration <= 0f || scale <= 0f)
        {
            return;
        }

        Vector3 point = view.WorldToViewportPoint(worldPoint);
        centre = point.z > 0f ? new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y)) : new Vector2(0.5f, 0.5f);
        strength = amount * scale;
        startedAt = Time.unscaledTime;
        seconds = duration;
    }

    /// <summary>The blur now: false once it has eased out.</summary>
    public static bool TryGet(out Vector2 from, out float now)
    {
        from = centre;
        now = Strength(strength, Time.unscaledTime - startedAt, seconds);
        return now > 0.0005f;
    }

    /// <summary>How strong a blur of <paramref name="peak"/> is <paramref name="age"/> seconds into <paramref name="duration"/>: full at
    /// once, easing out (squared). Pure, for tests.</summary>
    public static float Strength(float peak, float age, float duration)
    {
        if (duration <= 0f || age < 0f || age >= duration)
        {
            return 0f;
        }

        float left = 1f - age / duration;
        return peak * left * left;
    }
}
