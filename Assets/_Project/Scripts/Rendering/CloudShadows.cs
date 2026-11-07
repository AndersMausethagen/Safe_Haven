using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Soft shadows of passing clouds: a tiling cloud pattern drawn into a light cookie on the sun, drifting with the wind.
/// The share of sky under cloud follows the weather; a sky that is all cloud casts none (the light is already soft and
/// even). At home only, so the raids keep their own look. URP takes only a Texture2D as a sun cookie, so the pattern drawn
/// into a render texture is copied on the GPU into one. Made by WorldRendering; numbers in RenderingSettings.
/// Shader: Shaders/CloudCookie.shader.
/// </summary>
public sealed class CloudShadows : MonoBehaviour
{
    private static readonly int CoverId = Shader.PropertyToID("_CloudCover");
    private static readonly int SoftnessId = Shader.PropertyToID("_CloudSoftness");
    private static readonly int DarknessId = Shader.PropertyToID("_CloudDarkness");
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");

    private RenderingSettings settings;
    private Material material;
    private RenderTexture drawn;
    private Texture2D cookie;
    private Light sun;
    private UniversalAdditionalLightData sunData;
    private Vector2 offset;
    private float drawnCover = -1f;
    private float drawnDarkness = -1f;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
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
        if (sun == null || sunData == null)
        {
            return;
        }

        float clouds = WeatherSystem.Active != null ? WeatherSystem.Active.Current.Clouds : 0f;
        Vector2 range = settings.CloudCover;
        float cover = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(clouds * 2f));
        // Under a sky of solid cloud the light is even: no shapes to cast.
        float darkness = settings.CloudDarkness * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.9f, clouds)));
        if (Mathf.Abs(cover - drawnCover) > 0.005f || Mathf.Abs(darkness - drawnDarkness) > 0.005f)
        {
            Draw(cover, darkness);
        }

        Vector4 wind = Shader.GetGlobalVector(WindId);
        var direction = new Vector2(wind.x, wind.y);
        if (direction.sqrMagnitude < 1e-4f)
        {
            direction = new Vector2(0.8f, 0.6f);
        }

        float strength = Mathf.Max(0.3f, AtmosphereConditions.WeatherWind);
        offset += direction.normalized * (settings.CloudSpeed * strength * Time.deltaTime);
        offset.x = Mathf.Repeat(offset.x, settings.CloudSize);
        offset.y = Mathf.Repeat(offset.y, settings.CloudSize);
        sunData.lightCookieSize = new Vector2(settings.CloudSize, settings.CloudSize);
        sunData.lightCookieOffset = offset;
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
