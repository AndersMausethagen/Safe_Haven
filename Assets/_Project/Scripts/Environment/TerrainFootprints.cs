using UnityEngine;
using System.Collections.Generic;

/// <summary>A bounded, recovering GPU footprint field owned by one terrain. Authored assets are never changed.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(TerrainSurface))]
public sealed class TerrainFootprints : MonoBehaviour
{
    private const int MaxGrassFields = 4;
    private static readonly List<TerrainFootprints> ActiveFields = new List<TerrainFootprints>();
    private static readonly Vector4[] GrassAreas = new Vector4[MaxGrassFields];
    private static readonly int[] GrassFieldIds = {
        Shader.PropertyToID("_GrassFootprint0"), Shader.PropertyToID("_GrassFootprint1"),
        Shader.PropertyToID("_GrassFootprint2"), Shader.PropertyToID("_GrassFootprint3")
    };
    [SerializeField] private Shader stampShader;
    private Terrain terrain;
    private TerrainSurface surface;
    private Material stampMaterial;
    private RenderTexture field;
    private RenderTexture scratch;
    private MaterialPropertyBlock block;
    private Vector2 origin;
    private float area;
    private float remaining;

    public RenderTexture Field => field;
    public Vector2 Origin => origin;
    public float Area => area;
    private TerrainSurfaceProfile Profile => surface != null ? surface.Profile : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        ActiveFields.Clear();
        Shader.SetGlobalFloat("_GrassFootprintCount", 0);
        Shader.SetGlobalVector("_GrassFootprintDepths", Vector4.zero);
        for (int i = 0; i < MaxGrassFields; i++)
            Shader.SetGlobalTexture(GrassFieldIds[i], Texture2D.blackTexture);
    }

    private void Awake()
    {
        terrain = GetComponent<Terrain>();
        surface = GetComponent<TerrainSurface>();
    }

    private void Update() => Advance(Time.deltaTime);

    public void Advance(float seconds)
    {
        if (seconds < 0 || !float.IsFinite(seconds))
            throw new System.ArgumentOutOfRangeException(nameof(seconds));
        if (field == null) return;
        if (Profile == null || !Profile.footprints)
        {
            Release();
            return;
        }
        if (remaining > 0 && seconds > 0)
        {
            WeatherSystem weather = WeatherSystem.Active;
            float lifetime = Profile.footprintLifetime * SnowLifetime(weather != null ? weather.Snow : 0f, weather != null && weather.Snowing ? weather.Current.Rain : 0f);
            Render(Vector2.zero, seconds / Mathf.Max(0.1f, lifetime), false, Vector2.zero, Vector2.up);
            remaining = Mathf.Max(0, remaining - seconds / Mathf.Max(0.1f, lifetime));
        }
        Bind();
    }

    /// <summary>
    /// How much longer prints last than usual: in lying snow they stay (up to five times as long, so tracks can be
    /// followed), and falling snow fills them in again (a heavy fall fills them in twice as fast as usual).
    /// </summary>
    public static float SnowLifetime(float snow, float snowfall)
    {
        float kept = Mathf.Lerp(1f, 5f, Mathf.Clamp01((snow - 0.2f) / 0.5f));
        return kept / (1f + Mathf.Clamp01(snowfall) * (kept * 2f - 1f));
    }

    public void Stamp(Vector3 worldPosition, Vector3 forward) => Stamp(worldPosition, forward, FootprintShape.Boot);

    public void Stamp(Vector3 worldPosition, Vector3 forward, FootprintShape shape)
    {
        if (!isActiveAndEnabled) return;
        if (terrain == null) Awake();
        if (Profile == null || !Profile.footprints || terrain.terrainData == null) return;
        Vector3 local = worldPosition - terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) return;
        if (!EnsureResources()) return;
        Vector2 centre = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 uv = (centre - origin) / area;
        Vector2 shift = Vector2.zero;
        if (uv.x < 0.25f || uv.y < 0.25f || uv.x > 0.75f || uv.y > 0.75f)
        {
            Vector2 nextOrigin = centre - Vector2.one * (area * 0.5f);
            // Integer texel shifts avoid blurring existing prints whenever the window moves.
            float texel = area / field.width;
            nextOrigin = new Vector2(Mathf.Round(nextOrigin.x / texel), Mathf.Round(nextOrigin.y / texel)) * texel;
            shift = (nextOrigin - origin) / area;
            origin = nextOrigin;
            uv = (centre - origin) / area;
        }
        Vector2 direction = new Vector2(forward.x, forward.z);
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        Render(shift, 0, true, uv, direction.normalized, shape);
        remaining = 1;
        Bind();
    }

    /// <summary>
    /// A print from someone other than the player (an NPC): stamped only inside the window the player's prints already
    /// keep, and never moving it, so a walker far off cannot drag the window away and wipe the player's trail. Prints
    /// outside it are not kept; nobody is near enough to see them.
    /// </summary>
    public void StampInWindow(Vector3 worldPosition, Vector3 forward) => StampInWindow(worldPosition, forward, FootprintShape.Boot);

    public void StampInWindow(Vector3 worldPosition, Vector3 forward, FootprintShape shape)
    {
        if (!isActiveAndEnabled || field == null || Profile == null || !Profile.footprints)
        {
            return;
        }

        Vector2 uv = (new Vector2(worldPosition.x, worldPosition.z) - origin) / area;
        if (uv.x < 0.02f || uv.y < 0.02f || uv.x > 0.98f || uv.y > 0.98f)
        {
            return;
        }

        Vector2 direction = new Vector2(forward.x, forward.z);
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
        Render(Vector2.zero, 0, true, uv, direction.normalized, shape);
        remaining = 1;
        Bind();
    }

    private bool EnsureResources()
    {
        int resolution = (int)Profile.footprintResolution;
        float desiredArea = Mathf.Max(8, Profile.footprintArea);
        if (field != null && (field.width != resolution || !Mathf.Approximately(area, desiredArea)))
            Release();
        if (field != null) return true;
        if (stampShader == null)
        {
            Debug.LogError("TerrainFootprints requires its footprint stamp shader. Rebuild Terrain Surface resources.", this);
            enabled = false;
            return false;
        }
        if (!stampShader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat))
        {
            Debug.LogError("Terrain footprint rendering is not supported on this device.", this);
            enabled = false;
            return false;
        }
        area = desiredArea;
        stampMaterial = new Material(stampShader) { hideFlags = HideFlags.HideAndDontSave };
        field = CreateField(resolution);
        scratch = CreateField(resolution);
        ActiveFields.Add(this);
        origin = new Vector2(terrain.transform.position.x, terrain.transform.position.z);
        return true;
    }

    private RenderTexture CreateField(int resolution)
    {
        var target = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RGFloat, RenderTextureReadWrite.Linear)
        {
            name = "Runtime Terrain Footprints",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        target.Create();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;
        return target;
    }

    private void Render(Vector2 shift, float decay, bool stamp, Vector2 uv, Vector2 direction) =>
        Render(shift, decay, stamp, uv, direction, FootprintShape.Boot);

    private void Render(Vector2 shift, float decay, bool stamp, Vector2 uv, Vector2 direction, FootprintShape shape)
    {
        bool hoof = shape.Hoof;
        float width = shape.Width > 0f ? shape.Width : Profile.bootWidth;
        float length = shape.Length > 0f ? shape.Length : Profile.bootLength;
        stampMaterial.SetVector("_Shift", new Vector4(shift.x, shift.y, 0, 0));
        stampMaterial.SetFloat("_Decay", decay);
        stampMaterial.SetFloat("_Stamp", stamp ? 1 : 0);
        stampMaterial.SetVector("_Boot", new Vector4(uv.x, uv.y,
            area / Mathf.Max(0.05f, width), area / Mathf.Max(0.05f, length)));
        stampMaterial.SetFloat("_Hoof", hoof ? 1 : 0);
        stampMaterial.SetVector("_Direction", new Vector4(direction.x, direction.y, 0, 0));
        stampMaterial.SetFloat("_UseSoleTexture", Profile.bootMask != null ? 1 : 0);
        stampMaterial.SetTexture("_SoleTex", Profile.bootMask != null ? Profile.bootMask : Texture2D.whiteTexture);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(field, scratch, stampMaterial);
        RenderTexture.active = previous;
        (field, scratch) = (scratch, field);
    }

    /// <summary>
    /// Makes sure the field exists and covers <paramref name="worldPosition"/> (a heavy blow's mark, GroundImpacts): made if
    /// no print has been pressed yet, and moved only when the point lies outside it, so the player's own trail stays.
    /// </summary>
    public void Include(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled) return;
        if (terrain == null) Awake();
        if (Profile == null || !Profile.footprints || terrain.terrainData == null || !EnsureResources()) return;
        Vector2 centre = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 uv = (centre - origin) / area;
        if (uv.x >= 0.1f && uv.y >= 0.1f && uv.x <= 0.9f && uv.y <= 0.9f)
        {
            Bind();
            return;
        }
        float texel = area / field.width;
        Vector2 nextOrigin = centre - Vector2.one * (area * 0.5f);
        nextOrigin = new Vector2(Mathf.Round(nextOrigin.x / texel), Mathf.Round(nextOrigin.y / texel)) * texel;
        Vector2 shift = (nextOrigin - origin) / area;
        origin = nextOrigin;
        Render(shift, 0, false, Vector2.zero, Vector2.up);
        Bind();
    }

    /// <summary>The fields in use: GroundImpacts mirrors heavy blows' marks into their second channel.</summary>
    public static IReadOnlyList<TerrainFootprints> Fields => ActiveFields;

    /// <summary>Runs <paramref name="material"/>'s <paramref name="pass"/> over the field (its _MainTex), keeping the
    /// result: GroundImpacts writes the craters of heavy blows into G (R, the prints, it leaves as they are).</summary>
    public void Overlay(Material material, int pass)
    {
        if (field == null || material == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(field, scratch, material, pass);
        RenderTexture.active = previous;
        (field, scratch) = (scratch, field);
        Bind();
    }

    private void Bind()
    {
        block ??= new MaterialPropertyBlock();
        terrain.GetSplatMaterialPropertyBlock(block);
        block.SetTexture("_FootprintField", field != null ? field : Texture2D.blackTexture);
        block.SetVector("_FootprintArea", new Vector4(origin.x, origin.y, 1 / Mathf.Max(area, 1), 0));
        bool active = field != null && Profile != null && Profile.footprints;
        block.SetVector("_FootprintSettings", active
            ? new Vector4(Profile.footprintDepth, Profile.footprintFlattening, Profile.footprintNormalFlattening, 1)
            : Vector4.zero);
        block.SetFloat("_FootprintDarkening", active ? Profile.footprintDarkening : 0);
        terrain.SetSplatMaterialPropertyBlock(block);
        UploadGrass();
    }

    private static void UploadGrass()
    {
        ActiveFields.RemoveAll(item => item == null || item.field == null);
        if (ActiveFields.Count > MaxGrassFields)
        {
            Vector3 eye = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector2 eyeXZ = new Vector2(eye.x, eye.z);
            ActiveFields.Sort((a, b) => (a.origin - eyeXZ).sqrMagnitude.CompareTo((b.origin - eyeXZ).sqrMagnitude));
        }
        int count = Mathf.Min(ActiveFields.Count, MaxGrassFields);
        Vector4 darkening = Vector4.zero;
        Vector4 depths = Vector4.zero;
        for (int i = 0; i < MaxGrassFields; i++)
        {
            TerrainFootprints item = i < count ? ActiveFields[i] : null;
            Shader.SetGlobalTexture(GrassFieldIds[i], item != null ? item.field : Texture2D.blackTexture);
            GrassAreas[i] = item != null && item.Profile != null && item.Profile.footprints
                ? new Vector4(item.origin.x, item.origin.y, 1 / item.area,
                    item.Profile.footprintGrassFlattening * TerrainSurface.QualityParameters(item.surface.Quality).x)
                : Vector4.zero;
            darkening[i] = item != null && item.Profile != null ? item.Profile.footprintDarkening : 0;
            depths[i] = item != null && item.Profile != null ? item.GrassDepression() : 0;
        }
        Shader.SetGlobalVectorArray("_GrassFootprintAreas", GrassAreas);
        Shader.SetGlobalVector("_GrassFootprintDarkening", darkening);
        Shader.SetGlobalVector("_GrassFootprintDepths", depths);
        Shader.SetGlobalFloat("_GrassFootprintCount", count);
    }

    private float GrassDepression()
    {
        float minimum = 0;
        foreach (TerrainSurfaceProfile.Layer layer in Profile.layers)
        {
            if (layer == null || layer.terrainLayer == null) continue;
            float low = layer.heightMap != null ? 0 : layer.terrainLayer.maskMapRemapMin.z;
            float high = layer.heightMap != null ? 1 : layer.terrainLayer.maskMapRemapMax.z;
            minimum = Mathf.Min(minimum, Mathf.Min((low - layer.midpoint) * layer.amplitude,
                (high - layer.midpoint) * layer.amplitude));
        }
        Material material = terrain.materialTemplate;
        float strength = material != null && material.HasProperty("_DisplacementStrength")
            ? Mathf.Max(0, material.GetFloat("_DisplacementStrength")) : 0;
        // Keep flattened plants below the deepest compressed layer, not floating over its tread.
        return Profile.footprintDepth - minimum * strength * Profile.footprintFlattening;
    }

    private void OnDisable() => Release();
    private void OnDestroy() => Release();

    private void Release()
    {
        if (field != null) { field.Release(); DestroyResource(field); }
        if (scratch != null) { scratch.Release(); DestroyResource(scratch); }
        if (stampMaterial != null) DestroyResource(stampMaterial);
        field = null;
        scratch = null;
        stampMaterial = null;
        remaining = 0;
        ActiveFields.Remove(this);
        if (terrain != null) Bind();
        else UploadGrass();
    }

    private static void DestroyResource(Object resource)
    {
        if (Application.isPlaying) Destroy(resource);
        else DestroyImmediate(resource);
    }
}

/// <summary>What presses a print: a boot (the terrain profile's size and sole) or a hoof of a given size.</summary>
public readonly struct FootprintShape
{
    public static readonly FootprintShape Boot = default;

    public readonly float Width;
    public readonly float Length;
    public readonly bool Hoof;

    private FootprintShape(float width, float length, bool hoof)
    {
        Width = width;
        Length = length;
        Hoof = hoof;
    }

    /// <summary>A shod hoof <paramref name="size"/> metres across.</summary>
    public static FootprintShape HoofOf(float size) => new FootprintShape(size, size * 1.05f, true);
}
