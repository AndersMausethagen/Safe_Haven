using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Screen-space contact shadows (Docs/LookAndFeelResearch_2026-11-18.md 3.2): the small shadows the sun's shadow map can't see from the
/// top-down camera (pebbles, grass clumps, a foot, a log's underside), from a short march of the depth buffer toward the sun, multiplied
/// onto the lit scene after opaques. On PC_Renderer; Options › Graphics › Contact Shadows turns it off. Game cameras only.
/// Shader: Shaders/ContactShadows.shader ("Hidden/Safe Haven/Contact Shadows").
/// </summary>
public sealed class ContactShadowsFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [Tooltip("How much a contact shadow darkens what the sun lights (0-1): the sky still lights it.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.55f;
    [Tooltip("Metres marched toward the sun: the longest shadow it draws.")]
    [SerializeField, Range(0.1f, 2f)] private float length = 0.6f;
    [Tooltip("Metres behind a surface still counted as solid (thin things: a blade of grass, a branch).")]
    [SerializeField, Range(0.05f, 2f)] private float thickness = 0.35f;
    [Tooltip("Metres from the camera beyond which it fades out (the play area is 15-35 m off).")]
    [SerializeField, Min(5f)] private float reach = 60f;

    private Material material;
    private ContactPass pass;

    public override void Create()
    {
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Safe Haven/Contact Shadows");
        }

        if (shader == null)
        {
            return;
        }

        // Create runs again on every change in the Editor: one material, not one more each time.
        if (material == null || material.shader != shader)
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
        }
        pass = new ContactPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || !GameOptions.ContactShadows.On)
        {
            return;
        }

        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game || renderingData.cameraData.camera.targetTexture != null)
        {
            return;
        }

        // The terrain under the view, so only what lies on the ground takes a contact shadow (cheap when it is the one bound).
        GroundMarks.BindGround(RainEffect.GroundInView(renderingData.cameraData.camera));

        pass.Setup(strength, length, thickness, reach);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class ContactPass : ScriptableRenderPass
    {
        private static readonly int ParamsId = Shader.PropertyToID("_ContactShadowParams");
        private readonly Material material;

        private class PassData
        {
            public Material material;
        }

        public ContactPass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("Contact Shadows");
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void Setup(float strength, float length, float thickness, float reach) =>
            material.SetVector(ParamsId, new Vector4(strength, length, thickness, reach));

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraDepthTexture.IsValid())
            {
                return;
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Contact Shadows", out var data, profilingSampler))
            {
                data.material = material;
                builder.UseTexture(resources.cameraDepthTexture);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), d.material, 0));
            }
        }
    }
}
