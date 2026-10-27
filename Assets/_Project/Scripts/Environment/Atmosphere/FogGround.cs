using System.Collections;
using UnityEngine;

/// <summary>
/// Lets the volumetric fog follow the ground (Atmospheric Fog › Follow Terrain): bakes the height of every terrain tile
/// in the scene into a small texture the fog shader reads, so the fog lies as a blanket at the same depth over hills
/// and hollows and spills down slopes. Baked once during scene setup, after a raid map has been turned, and again with
/// <see cref="Bake"/> when the ground changes. One per scene, on the "Atmosphere" object.
/// </summary>
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
    }

    private void Start()
    {
        // Scenes without a loading-screen pass (an editor test scene) still get a ground.
        if (texture == null)
        {
            Bake();
        }
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
            Destroy(texture);
        }
    }

    /// <summary>Samples every terrain tile into the height texture.</summary>
    public void Bake()
    {
        Rect area = TerrainArea(out float minY, out float maxY);
        if (area.width <= 0f)
        {
            return;
        }

        int width = Mathf.Clamp(Mathf.CeilToInt(area.width / cellSize), 2, 1024);
        int height = Mathf.Clamp(Mathf.CeilToInt(area.height / cellSize), 2, 1024);
        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null)
            {
                Destroy(texture);
            }

            texture = new Texture2D(width, height, TextureFormat.RHalf, false, true)
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
                heights[y * width + x] = new Color((GroundAt(new Vector3(wx, 0f, z), minY) - minY) / span, 0f, 0f, 0f);
            }
        }

        texture.SetPixels(heights);
        texture.Apply(false, false);
        Area = area;
        HeightRange = new Vector2(minY, span);
    }

    private static float GroundAt(Vector3 position, float fallback)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
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

    private static Rect TerrainArea(out float minY, out float maxY)
    {
        bool any = false;
        float xMin = 0f, zMin = 0f, xMax = 0f, zMax = 0f;
        minY = 0f;
        maxY = 0f;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
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
