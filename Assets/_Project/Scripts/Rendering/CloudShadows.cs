using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Soft shadows of passing clouds: the clouds' tiling pattern drawn into a light cookie on the sun, drifting with the wind.
/// Cover and drift come from the CloudLayer the sky draws too; a sky that is all cloud casts none (the light is already
/// soft and even). URP takes only a Texture2D as a sun cookie, so the pattern drawn into a render texture is copied on the
/// GPU into one. Made by WorldRendering; numbers in RenderingSettings.
/// Shader: Shaders/CloudCookie.shader.
/// </summary>
[DefaultExecutionOrder(91)]
public sealed class CloudShadows : MonoBehaviour
{
    private static readonly int CoverId = Shader.PropertyToID("_CloudCover");
    private static readonly int SoftnessId = Shader.PropertyToID("_CloudSoftness");
    private static readonly int DarknessId = Shader.PropertyToID("_CloudDarkness");

    private RenderingSettings settings;
    private CloudLayer layer;
    private Material material;
    private RenderTexture drawn;
    private Texture2D cookie;
    private Light sun;
    private UniversalAdditionalLightData sunData;
    private float drawnCover = -1f;
    private float drawnDarkness = -1f;

    public void Initialise(RenderingSettings rules, CloudLayer clouds)
    {
        settings = rules;
        layer = clouds;
        Shader shader = settings.CloudShader;
        if (shader == null)
        {
            enabled = false;
            return;
        }

        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        drawn = new RenderTexture(512, 512, GraphicsFormat.R8_UNorm, GraphicsFormat.None)
        {
            name = "Cloud Shadows (drawn)", useMipMap = false, hideFlags = HideFlags.HideAndDontSave,
        };
        cookie = new Texture2D(512, 512, GraphicsFormat.R8_UNorm, TextureCreationFlags.None)
        {
            name = "Cloud Shadows", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }

    /// <summary>Puts the clouds on this scene's sun, or takes them off it.</summary>
    public void Bind(Light light, bool show)
    {
        Unbind();
        if (!show || light == null || light.type != LightType.Directional || cookie == null)
        {
            return;
        }

        sun = light;
        sunData = light.GetComponent<UniversalAdditionalLightData>();
        drawnCover = -1f;
    }

    private void LateUpdate()
    {
        if (sun == null || sunData == null || layer == null)
        {
            return;
        }

        float cover = layer.Cover;
        // Under a sky of solid cloud the light is even: no shapes to cast.
        // At night the moon is all the light there is: clouds pass over it softly, or the ground (and the player) go black.
        float darkness = settings.CloudDarkness * (1f - layer.Overcast) * (1f - 0.65f * AtmosphereConditions.Night);
        if (Mathf.Abs(cover - drawnCover) > 0.005f || Mathf.Abs(darkness - drawnDarkness) > 0.005f)
        {
            Draw(cover, darkness);
        }

        sunData.lightCookieSize = new Vector2(settings.CloudSize, settings.CloudSize);
        sunData.lightCookieOffset = layer.Offset;
        if (sun.cookie != cookie)
        {
            sun.cookie = cookie;
        }
    }

    private void Draw(float cover, float darkness)
    {
        drawnCover = cover;
        drawnDarkness = darkness;
        material.SetFloat(CoverId, cover);
        material.SetFloat(SoftnessId, settings.CloudSoftness);
        material.SetFloat(DarknessId, darkness);
        Graphics.Blit(null, drawn, material);
        Graphics.CopyTexture(drawn, cookie);
        cookie.IncrementUpdateCount();
    }

    private void Unbind()
    {
        if (sun != null && sun.cookie == cookie)
        {
            sun.cookie = null;
        }

        sun = null;
        sunData = null;
    }

    private void OnDestroy()
    {
        Unbind();
        if (drawn != null)
        {
            drawn.Release();
            Destroy(drawn);
        }

        if (cookie != null)
        {
            Destroy(cookie);
        }

        if (material != null)
        {
            Destroy(material);
        }
    }
}
