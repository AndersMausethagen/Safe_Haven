using System.Collections;
using UnityEngine;

/// <summary>
/// Lets the volumetric fog follow the ground (Atmospheric Fog › Follow Terrain): bakes the height of every terrain tile
/// in the scene into a small texture the fog shader reads, so the fog lies as a blanket at the same depth over hills
/// and hollows and spills down slopes. Baked once during scene setup, after a raid map has been turned, and again with
/// <see cref="Bake"/> when the ground or water changes. Extra channels record water-surface height, water footprints
/// and local depressions for TerrainMist; global fog still samples only terrain height.
/// One per scene, on the "Atmosphere" object.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FogGround : MonoBehaviour, ISceneSetup
{
    [Tooltip("Metres per texel. The fog blanket is soft, so a coarse grid is enough.")]
    [SerializeField, Min(0.5f)] private float cellSize = 2f;

    private Texture2D texture;

    /// <summary>The ground height field in the loaded scene, or null.</summary>
    public static FogGround Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    public Texture2D Texture => texture;
    /// <summary>World XZ rectangle covered.</summary>
    public Rect Area { get; private set; }
    /// <summary>Lowest height and the span to the highest (heights are stored 0–1 between them).</summary>
    public Vector2 HeightRange { get; private set; }

    // After RaidMap (-100) has turned the terrain.
    public int SetupOrder => 20;

    public IEnumerator Setup()
    {
        Bake();
        yield break;
    }

    private void OnEnable()
    {
        Current = this;
        // In play, Start bakes once every water has registered: baking here as well cost a second full pass.
        if (!Application.isPlaying)
        {
            Bake();
        }
    }

    private void Start()
    {
        // All water surfaces have registered by Start, unlike during OnEnable on scene load.
        Bake();
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDestroy()
    {
        if (texture != null)
        {
            ReleaseTexture();
        }
    }

    /// <summary>Samples this scene's terrain, local depressions and water footprints into the height texture.</summary>
    public void Bake()
    {
        Rect area = TerrainArea(out float minY, out float maxY);
        if (area.width <= 0f)
        {
            return;
        }

        int width = Mathf.Clamp(Mathf.CeilToInt(area.width / cellSize), 2, 1024);
        int height = Mathf.Clamp(Mathf.CeilToInt(area.height / cellSize), 2, 1024);
        if (texture == null || texture.width != width || texture.height != height || texture.format != TextureFormat.RGBAHalf)
        {
            if (texture != null)
            {
                ReleaseTexture();
            }

            texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            {
                name = "Fog Ground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        float span = Mathf.Max(0.01f, maxY - minY);
        var terrains = new System.Collections.Generic.List<Terrain>();
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain != null && terrain.terrainData != null && terrain.gameObject.scene == gameObject.scene)
            {
                terrains.Add(terrain);
            }
        }

        // The ground once per cell; the depressions read it back 12 m each way instead of sampling the terrain four more times.
        var ground = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            float z = area.yMin + (y + 0.5f) / height * area.height;
            for (int x = 0; x < width; x++)
            {
                ground[y * width + x] = GroundAt(terrains, new Vector3(area.xMin + (x + 0.5f) / width * area.width, 0f, z), minY);
            }
        }

        var heights = new Color[width * height];
        int stepX = Mathf.Max(1, Mathf.RoundToInt(12f / (area.width / width))), stepY = Mathf.Max(1, Mathf.RoundToInt(12f / (area.height / height)));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float here = ground[y * width + x];
                float surrounding = (ground[y * width + Mathf.Min(width - 1, x + stepX)] + ground[y * width + Mathf.Max(0, x - stepX)]
                                   + ground[Mathf.Min(height - 1, y + stepY) * width + x] + ground[Mathf.Max(0, y - stepY) * width + x]) * 0.25f;
                // R: ground, G: mist support surface (raised by water below), B: water footprint, A: local depression in metres.
                heights[y * width + x] = new Color((here - minY) / span, (here - minY) / span, 0f, Mathf.Max(0f, surrounding - here));
            }
        }

        // Each water only over the cells its outline can cover: a river's test walks its whole centreline, and over every cell of a
        // 1 km world that took minutes on entering play.
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.TakesRipples || water.gameObject.scene != gameObject.scene || !water.TryGetComponent(out MeshFilter filter)
                || filter.sharedMesh == null)
            {
                continue;
            }

            if (water.HasCentreline)
            {
                StampRiver(water.WorldCentreline(), area, width, height, ground, heights, minY, span);
                continue;
            }

            Bounds bounds = TransformBounds(water.transform, filter.sharedMesh.bounds);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.x - area.xMin) / area.width * width));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((bounds.max.x - area.xMin) / area.width * width));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.z - area.yMin) / area.height * height));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((bounds.max.z - area.yMin) / area.height * height));
            for (int y = y0; y <= y1; y++)
            {
                float z = area.yMin + (y + 0.5f) / height * area.height;
                for (int x = x0; x <= x1; x++)
                {
                    float wx = area.xMin + (x + 0.5f) / width * area.width;
                    int cell = y * width + x;
                    float here = ground[cell];
                    // From above, with the reach to come down to any surface: inside the outline is all that is asked.
                    if (water.Contains(new Vector3(wx, maxY + 1f, z), maxY - minY + 10f, out float waterHeight) && waterHeight > here + 0.02f)
                    {
                        Color c = heights[cell];
                        c.g = Mathf.Max(c.g, (waterHeight - minY) / span);
                        c.b = 1f;
                        heights[cell] = c;
                    }
                }
            }
        }

        texture.SetPixels(heights);
        texture.Apply(false, false);
        Area = area;
        HeightRange = new Vector2(minY, span);
    }

    private void ReleaseTexture()
    {
        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }

        texture = null;
    }

    // A river segment by segment, over only the cells beside each: within its half width, the water's height there.
    private static void StampRiver(Vector4[] line, Rect area, int width, int height, float[] ground, Color[] heights, float minY, float span)
    {
        float cellX = area.width / width, cellY = area.height / height;
        for (int i = 0; i + 1 < line.Length; i++)
        {
            Vector4 a = line[i], b = line[i + 1];
            float reach = Mathf.Max(a.w, b.w);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - area.xMin) / cellX));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - area.xMin) / cellX));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - reach - area.yMin) / cellY));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + reach - area.yMin) / cellY));
            var along = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSq = Mathf.Max(1e-8f, along.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var point = new Vector2(area.xMin + (x + 0.5f) * cellX, area.yMin + (y + 0.5f) * cellY);
                    float t = Mathf.Clamp01(Vector2.Dot(point - new Vector2(a.x, a.z), along) / lengthSq);
                    Vector4 at = Vector4.Lerp(a, b, t);
                    int cell = y * width + x;
                    if (Vector2.Distance(point, new Vector2(at.x, at.z)) > at.w || at.y <= ground[cell] + 0.02f)
                    {
                        continue;
                    }

                    Color c = heights[cell];
                    c.g = Mathf.Max(c.g, (at.y - minY) / span);
                    c.b = 1f;
                    heights[cell] = c;
                }
            }
        }
    }

    private static Bounds TransformBounds(Transform transform, Bounds local)
    {
        var world = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            world.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
        }

        return world;
    }

    private static float GroundAt(System.Collections.Generic.List<Terrain> terrains, Vector3 position, float fallback)
    {
        foreach (Terrain terrain in terrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (position.x >= origin.x && position.z >= origin.z && position.x <= origin.x + size.x && position.z <= origin.z + size.z)
            {
                return origin.y + terrain.SampleHeight(position);
            }
        }

        return fallback;
    }

    private Rect TerrainArea(out float minY, out float maxY)
    {
        bool any = false;
        float xMin = 0f, zMin = 0f, xMax = 0f, zMax = 0f;
        minY = 0f;
        maxY = 0f;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null || terrain.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            xMin = any ? Mathf.Min(xMin, origin.x) : origin.x;
            zMin = any ? Mathf.Min(zMin, origin.z) : origin.z;
            xMax = any ? Mathf.Max(xMax, origin.x + size.x) : origin.x + size.x;
            zMax = any ? Mathf.Max(zMax, origin.z + size.z) : origin.z + size.z;
            minY = any ? Mathf.Min(minY, origin.y) : origin.y;
            maxY = any ? Mathf.Max(maxY, origin.y + size.y) : origin.y + size.y;
            any = true;
        }

        return any ? Rect.MinMaxRect(xMin, zMin, xMax, zMax) : Rect.zero;
    }
}
