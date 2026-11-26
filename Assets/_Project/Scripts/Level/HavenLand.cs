using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's lands as the story has them (Docs/HavenWorldStory_2026-11-26.md): the mist over the outer Haven is the forest's
/// forgetting. Forgotten land lies grey under thick mist; a region whose boundary stone is lit (Territory) is remembered, and the
/// mist draws back from it over a few seconds as its own colour comes back. Two maps of the terrain, remade as that changes: the
/// land's look for the atmosphere's composite (each region's colour filter, and how forgotten it is) and the mist's region map
/// (how thick, and each region's mist colour), used by the terrain's TerrainMist in place of the painted one. Numbers in
/// HavenRegionSettings. Made at home by ExperienceTracker (as the boundary stones are), so the scene file never changes for it.
/// </summary>
public sealed class HavenLand : MonoBehaviour
{
    private const int MapSize = 256;
    // How often the maps are remade while the mist draws back (seconds).
    private const float RemakeEvery = 0.1f;

    public static HavenLand Current { get; private set; }

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private HavenRegionSettings settings;
    private WorldAtlas atlas;
    private TerrainMist mist;
    private Rect area;
    private Texture2D lookTexture;
    private Texture2D mistTexture;
    private Color32[] lookPixels;
    private Color32[] mistPixels;
    private float[] authored;
    // For each texel, the regions it belongs to (at most PerTexel, -1 past the last) and how much of it each is, blurred at the
    // borders, so remaking the maps reads only the few regions each texel has.
    private const int PerTexel = 4;
    private int[] texelRegions;
    private float[] texelShares;
    private readonly List<string> names = new List<string>();
    private readonly List<HavenRegionSettings.RegionLook> looks = new List<HavenRegionSettings.RegionLook>();
    // How remembered each region is now (0 forgotten, 1 remembered), easing toward its stone's state.
    private float[] remembered;
    private float nextRemake;
    private bool dirty;
    // Worked places in the land (a relit clamp: HavenPlaceScenes' smoking stages) keep it in mind round them, before their region is
    // remembered: x, z and radius of each, and for each texel how much it is kept (0 to 1).
    private readonly Dictionary<Object, Vector3> keepers = new Dictionary<Object, Vector3>();
    private float[] kept;

    /// <summary>A worked place keeps the land round it in mind: thinner mist and some colour, out to <paramref name="radius"/> metres.</summary>
    public void Keep(Object keeper, Vector2 at, float radius)
    {
        keepers[keeper] = new Vector3(at.x, at.y, Mathf.Max(1f, radius));
        RemakeKept();
    }

    /// <summary>The place is no longer worked (or gone).</summary>
    public void Release(Object keeper)
    {
        if (keepers.Remove(keeper))
        {
            RemakeKept();
        }
    }

    private void RemakeKept()
    {
        kept ??= new float[MapSize * MapSize];
        System.Array.Clear(kept, 0, kept.Length);
        float texel = area.width / MapSize;
        foreach (Vector3 keeper in keepers.Values)
        {
            int cx = Mathf.FloorToInt((keeper.x - area.xMin) / texel), cy = Mathf.FloorToInt((keeper.y - area.yMin) / (area.height / MapSize));
            int reach = Mathf.CeilToInt(keeper.z / texel) + 1;
            for (int y = Mathf.Max(0, cy - reach); y <= Mathf.Min(MapSize - 1, cy + reach); y++)
            {
                for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(MapSize - 1, cx + reach); x++)
                {
                    float wx = area.xMin + (x + 0.5f) * texel, wz = area.yMin + (y + 0.5f) * (area.height / MapSize);
                    float d = Mathf.Sqrt((wx - keeper.x) * (wx - keeper.x) + (wz - keeper.y) * (wz - keeper.y)) / keeper.z;
                    // Whole within half the radius, fading to none at it.
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, d));
                    int i = y * MapSize + x;
                    kept[i] = Mathf.Max(kept[i], k);
                }
            }
        }

        dirty = true;
    }

    /// <summary>The land's look for the atmosphere: RGB each region's colour filter (halved), A how forgotten.</summary>
    public Texture2D LookTexture => lookTexture;
    /// <summary>x min, z min, 1 / width, 1 / depth of the maps.</summary>
    public Vector4 LookRect => new Vector4(area.xMin, area.yMin, 1f / area.width, 1f / area.height);
    /// <summary>Strength, forgotten desaturation, forgotten darkening.</summary>
    public Vector4 LookParams => new Vector4(1f, settings.ForgottenDesaturation, settings.ForgottenDarkening, 0f);
    public Color ForgottenColor => settings.ForgottenColor.linear;

    /// <summary>Stands it in the Haven (ExperienceTracker, beside TerritorySpawner); anywhere else, or without an atlas, nothing.</summary>
    public static void Begin(string sceneName)
    {
        if (sceneName != Territory.HomeScene || Territory.Atlas == null || Current != null)
        {
            return;
        }

        new GameObject("Haven Land").AddComponent<HavenLand>();
    }

    private void Awake()
    {
        Current = this;
        settings = HavenRegionSettings.Current;
        atlas = Territory.Atlas;
        foreach (TerrainMist terrainMist in TerrainMist.Active)
        {
            if (terrainMist != null && terrainMist.AuthoredRegionMap != null)
            {
                mist = terrainMist;
                break;
            }
        }

        Terrain terrain = mist != null ? mist.Terrain : Terrain.activeTerrain;
        area = terrain != null && terrain.terrainData != null
            ? new Rect(terrain.transform.position.x, terrain.transform.position.z, terrain.terrainData.size.x, terrain.terrainData.size.z)
            : atlas.Area;
        Build();
        Territory.Changed += OnLandsChanged;
    }

    private void OnDestroy()
    {
        Territory.Changed -= OnLandsChanged;
        if (mist != null)
        {
            mist.UseRegionMap(null, false);
        }

        if (lookTexture != null) Destroy(lookTexture);
        if (mistTexture != null) Destroy(mistTexture);
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>True when the region's stone is lit: remembered.</summary>
    public static bool IsRemembered(string region) =>
        region != null && (region == Territory.HomeRegion || GameSession.Here.Territory.IsClaimed(region));

    private void Build()
    {
        foreach (WorldAtlas.Region region in atlas.Regions)
        {
            names.Add(region.name);
            looks.Add(settings.LookOf(region.name));
        }

        int count = names.Count;
        var shares = new float[count][];
        for (int r = 0; r < count; r++)
        {
            shares[r] = new float[MapSize * MapSize];
        }

        // Which region each texel is in, then blurred so one region's look fades into the next.
        for (int y = 0; y < MapSize; y++)
        {
            for (int x = 0; x < MapSize; x++)
            {
                var world = new Vector3(area.xMin + (x + 0.5f) / MapSize * area.width, 0f, area.yMin + (y + 0.5f) / MapSize * area.height);
                WorldAtlas.Region region = atlas.RegionAt(world);
                int index = region != null ? names.IndexOf(region.name) : -1;
                if (index >= 0)
                {
                    shares[index][y * MapSize + x] = 1f;
                }
            }
        }

        int radius = Mathf.Max(0, Mathf.RoundToInt(settings.BorderSoftness / (area.width / MapSize)));
        var scratch = new float[MapSize * MapSize];
        foreach (float[] share in shares)
        {
            Blur(share, scratch, MapSize, radius);
        }

        texelRegions = new int[MapSize * MapSize * PerTexel];
        texelShares = new float[MapSize * MapSize * PerTexel];
        for (int i = 0; i < MapSize * MapSize; i++)
        {
            int k = 0;
            for (int r = 0; r < count && k < PerTexel; r++)
            {
                if (shares[r][i] > 0.001f)
                {
                    texelRegions[i * PerTexel + k] = r;
                    texelShares[i * PerTexel + k] = shares[r][i];
                    k++;
                }
            }

            for (; k < PerTexel; k++)
            {
                texelRegions[i * PerTexel + k] = -1;
            }
        }

        authored = ReadAuthored(mist != null ? mist.AuthoredRegionMap : null, MapSize);
        remembered = new float[count];
        for (int r = 0; r < count; r++)
        {
            remembered[r] = Target(r);
        }

        lookPixels = new Color32[MapSize * MapSize];
        mistPixels = new Color32[MapSize * MapSize];
        lookTexture = NewTexture("Haven Land Look");
        mistTexture = NewTexture("Haven Land Mist");
        Remake();
        if (mist != null)
        {
            mist.UseRegionMap(mistTexture, true);
        }
    }

    private static Texture2D NewTexture(string name) =>
        new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false, true)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };

    // 1 for a region remembered (and home, and a region with no look: as painted), 0 for one forgotten.
    private float Target(int region) => looks[region] == null || IsRemembered(names[region]) ? 1f : 0f;

    private void OnLandsChanged()
    {
        for (int r = 0; r < names.Count; r++)
        {
            if (Target(r) > remembered[r] + 0.001f && looks[r] != null)
            {
                Remembered(names[r], looks[r]);
            }
        }

        dirty = true;
    }

    // The moment the mist draws back: said once, as it starts (the land itself shows the rest).
    private void Remembered(string region, HavenRegionSettings.RegionLook look)
    {
        string line = string.IsNullOrWhiteSpace(look.rememberedLine) ? "" : $" {look.rememberedLine}";
        GameMessages.Post($"{region} is remembered. The mist draws back.{line}", MessageKind.Gain);
        if (AudioManager.Sounds != null && Player.Current != null)
        {
            AudioManager.Play(AudioManager.Sounds.QuestCompleted, Player.Current.transform.position);
        }
    }

    private void Update()
    {
        float step = Time.deltaTime / Mathf.Max(0.5f, settings.DrawBackSeconds);
        for (int r = 0; r < names.Count; r++)
        {
            float target = Target(r);
            if (!Mathf.Approximately(remembered[r], target))
            {
                remembered[r] = Mathf.MoveTowards(remembered[r], target, step);
                dirty = true;
            }
        }

        if (dirty && Time.time >= nextRemake)
        {
            nextRemake = Time.time + RemakeEvery;
            dirty = false;
            Remake();
        }
    }

    // Both maps from the regions' shares, looks and how remembered each is now.
    private void Remake()
    {
        float rememberedMist = settings.RememberedMist;
        float keptRemembered = settings.KeptRemembered;
        for (int i = 0; i < MapSize * MapSize; i++)
        {
            float a = authored[i];
            float keptHere = kept != null ? kept[i] * keptRemembered : 0f;
            float density = 0f, forgotten = 0f, weight = 0f;
            Vector3 filter = Vector3.zero, tint = Vector3.zero;
            for (int k = 0; k < PerTexel; k++)
            {
                int r = texelRegions[i * PerTexel + k];
                if (r < 0)
                {
                    break;
                }

                float share = texelShares[i * PerTexel + k];

                weight += share;
                HavenRegionSettings.RegionLook look = looks[r];
                if (look == null)
                {
                    // Home, or a region with no look: as painted, never grey.
                    density += share * a;
                    filter += share * Vector3.one;
                    tint += share * Vector3.one;
                    continue;
                }

                float m = Mathf.Max(remembered[r], keptHere);
                float thick = Mathf.Max(a * look.forgottenMist, look.forgottenFloor);
                density += share * Mathf.Lerp(thick, a * rememberedMist, m);
                forgotten += share * (1f - m);
                // The region's own colour comes back as it is remembered.
                filter += share * Vector3.Lerp(Vector3.one, new Vector3(look.filter.r, look.filter.g, look.filter.b), m);
                tint += share * new Vector3(look.mistTint.r, look.mistTint.g, look.mistTint.b);
            }

            if (weight > 0f)
            {
                // Outside every region (the map's rim) keeps the painted map and its look.
                float rest = Mathf.Max(0f, 1f - weight);
                density += rest * a;
                filter += rest * Vector3.one;
                tint += rest * Vector3.one;
            }
            else
            {
                density = a;
                filter = Vector3.one;
                tint = Vector3.one;
            }

            lookPixels[i] = new Color32(Byte(filter.x * 0.5f), Byte(filter.y * 0.5f), Byte(filter.z * 0.5f), Byte(forgotten));
            mistPixels[i] = new Color32(Byte(density), Byte(tint.x), Byte(tint.y), Byte(tint.z));
        }

        lookTexture.SetPixels32(lookPixels);
        lookTexture.Apply(false);
        mistTexture.SetPixels32(mistPixels);
        mistTexture.Apply(false);
    }

    private static byte Byte(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);

    /// <summary>A box blur in two passes over a square map (radius in texels). Public for tests.</summary>
    public static void Blur(float[] values, float[] scratch, int size, int radius)
    {
        if (radius <= 0)
        {
            return;
        }

        float norm = 1f / (radius * 2 + 1);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += values[y * size + Mathf.Clamp(x + k, 0, size - 1)];
                }

                scratch[y * size + x] = sum * norm;
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    sum += scratch[Mathf.Clamp(y + k, 0, size - 1) * size + x];
                }

                values[y * size + x] = sum * norm;
            }
        }
    }

    // The painted map at this size, read through a render target so it needn't be readable.
    private static float[] ReadAuthored(Texture2D map, int size)
    {
        var values = new float[size * size];
        if (map == null)
        {
            return values;
        }

        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(map, target);
        RenderTexture.active = target;
        var read = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        read.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Color32[] pixels = read.GetPixels32();
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = pixels[i].r / 255f;
        }

        Destroy(read);
        return values;
    }
}
