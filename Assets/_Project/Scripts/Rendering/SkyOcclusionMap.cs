using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The sky-occlusion map (Shaders/SkyOcclusion.hlsl): every couple of seconds a top-down orthographic camera over the
/// ground round the player draws the world height of whatever stands highest at each spot (roofs, canopies, bushes,
/// people) through the Sky Occlusion Renderer, whose Render Objects pass draws everything with the Sky Occlusion Height
/// material. Shaders then dim the sky's ambient light and reflections under cover and keep rain, puddles and snow out
/// from under roofs, with buildings put up in play and the day's light as it is now (a bake could have neither). At home
/// only, so the raids keep their own look. Made by WorldRendering; numbers in RenderingSettings.
/// </summary>
public sealed class SkyOcclusionMap : MonoBehaviour
{
    private static readonly int MapId = Shader.PropertyToID("_SkyOcclusionMap");
    private static readonly int RectId = Shader.PropertyToID("_SkyOcclusionRect");

    private RenderingSettings settings;
    private Camera above;
    private RenderTexture map;
    private bool active;
    private float nextRender;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        int size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.SkyOcclusionResolution, 128, 2048));
        map = new RenderTexture(size, size, GraphicsFormat.R16_SFloat, GraphicsFormat.D24_UNorm_S8_UInt)
        {
            name = "Sky Occlusion", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
        };
        var go = new GameObject("Sky Occlusion Camera") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(transform, false);
        above = go.AddComponent<Camera>();
        above.enabled = false;
        above.orthographic = true;
        above.orthographicSize = settings.SkyOcclusionArea * 0.5f;
        above.aspect = 1f;
        above.nearClipPlane = 1f;
        above.farClipPlane = 800f;
        above.clearFlags = CameraClearFlags.SolidColor;
        above.backgroundColor = new Color(-1000f, 0f, 0f, 1f);
        above.cullingMask = settings.SkyOcclusionLayers;
        above.targetTexture = map;
        above.allowHDR = true;
        above.allowMSAA = false;
        above.useOcclusionCulling = false;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.SetRenderer(settings.SkyOcclusionRendererIndex);
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.antialiasing = AntialiasingMode.None;
        Shader.SetGlobalTexture(MapId, map);
        Off();
    }

    /// <summary>A new scene: how much cover dims the indirect light here (0 off, 1 fully; rain stays out from under
    /// cover whenever it is on).</summary>
    public void Bind(float dimming)
    {
        strength = Mathf.Clamp01(dimming);
        active = strength > 0f && settings.SkyOcclusion && settings.SkyOcclusionRendererIndex >= 0;
        nextRender = 0f;
        if (!active)
        {
            Off();
        }
    }

    private void LateUpdate()
    {
        if (!active || Time.unscaledTime < nextRender)
        {
            return;
        }

        Transform centre = Player.Current != null ? Player.Current.transform : Camera.main != null ? Camera.main.transform : null;
        if (centre == null)
        {
            return;
        }

        nextRender = Time.unscaledTime + settings.SkyOcclusionInterval;
        // Snapped to whole texels, so the map doesn't shimmer as it follows.
        float area = settings.SkyOcclusionArea;
        float texel = area / map.width;
        Vector3 at = centre.position;
        at.x = Mathf.Round(at.x / texel) * texel;
        at.z = Mathf.Round(at.z / texel) * texel;
        above.transform.SetPositionAndRotation(new Vector3(at.x, at.y + 400f, at.z), Quaternion.LookRotation(Vector3.down, Vector3.forward));
        above.Render();
        Shader.SetGlobalTexture(MapId, map);
        Shader.SetGlobalVector(RectId, new Vector4(at.x - area * 0.5f, at.z - area * 0.5f, 1f / area, strength));
    }

    private float strength = 1f;

    private static void Off() => Shader.SetGlobalVector(RectId, Vector4.zero);

    private void OnDestroy()
    {
        Off();
        if (map != null)
        {
            map.Release();
            Destroy(map);
        }
    }
}
