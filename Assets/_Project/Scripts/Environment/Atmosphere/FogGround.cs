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
        Bake();
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
        var heights = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float z = area.yMin + (y + 0.5f) / height * area.height;
            for (int x = 0; x < width; x++)
            {
                float wx = area.xMin + (x + 0.5f) / width * area.width;
                var position = new Vector3(wx, 0f, z);
                float ground = GroundAt(position, minY);
                float surrounding = (GroundAt(position + Vector3.right * 12f, ground)
                    + GroundAt(position + Vector3.left * 12f, ground)
                    + GroundAt(position + Vector3.forward * 12f, ground)
                    + GroundAt(position + Vector3.back * 12f, ground)) * 0.25f;
                float surfaceHeight = ground;
                float waterMask = 0f;
                foreach (WaterSurface water in WaterSurface.Surfaces)
                {
                    if (water == null || !water.TakesRipples || water.gameObject.scene != gameObject.scene)
                        continue;
                    Vector3 nearest = water.NearestPoint(position);
                    var probe = new Vector3(wx, nearest.y, z);
                    if (water.Contains(probe, 0.1f, out float waterHeight) && waterHeight > ground + 0.02f)
                    {
                        surfaceHeight = Mathf.Max(surfaceHeight, waterHeight);
                        waterMask = 1f;
                    }
                }
                // R: ground, G: mist support surface, B: water footprint, A: local depression in metres.
                heights[y * width + x] = new Color((ground - minY) / span, (surfaceHeight - minY) / span,
                    waterMask, Mathf.Max(0f, surrounding - ground));
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

    private float GroundAt(Vector3 position, float fallback)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null || terrain.gameObject.scene != gameObject.scene)
            {
                continue;
            }

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
