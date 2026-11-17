using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A generated raid map's ground light (Docs/LightingBakeResearch_2026-11-17.md §2.1), worked out once at the end of the map's
/// load (RaidMap) while the loading screen is up, since a run's hour never changes:
/// <list type="bullet">
/// <item>the sky-occlusion map drawn once over the whole map (SkyOcclusionMap.Pin) instead of round the player every 12 m or
/// 10 s; afterwards only a felled tree's patch, or the map when a building goes up or comes down, is drawn again;</item>
/// <item>where the raid's Look › Ground Light is on, a two-slice texture the shaders read through GroundLight.hlsl: the canopy's
/// shade on the ground (for the terrain, water and mist, which don't read the sky map), the land's own shade (relief), and light
/// thrown back from sunlit ground (bounce). The arithmetic is GroundLightField's, on a worker thread.</item>
/// </list>
/// The Haven, the prologue and painted maps keep the sky map round the player and never bind a field. Made by WorldRendering;
/// numbers in RenderingSettings, each raid's in RaidDefinition › Look › Ground Light.
/// </summary>
public sealed class GroundLightMap : MonoBehaviour
{
    private static readonly int FieldId = Shader.PropertyToID("_GroundLightField");
    private static readonly int RectId = Shader.PropertyToID("_GroundLightRect");
    private static readonly int BounceId = Shader.PropertyToID("_GroundLightBounce");

    /// <summary>Metres round a felled tree drawn again: its crown, and the reach of the cover test round it.</summary>
    private const float FelledReach = 12f;
    /// <summary>Metres of cover map round the field: SkyOcclusion.hlsl fades the map out over its outer twelfth, which then lies
    /// outside the map.</summary>
    private const float MarginShare = 0.1f;
    /// <summary>What ground under water throws back, against dry ground (the Marsh: banks darker, hummocks lighter).</summary>
    private const float WaterAlbedo = 0.12f;

    private static readonly float[] Linear = SrgbToLinear();

    public static GroundLightMap Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private RenderingSettings settings;
    private SkyOcclusionMap cover;
    private Texture2DArray field;
    private Texture2DArray placeholder;
    private int generation;
    // Kept for patches drawn again later: the field's square, its cells' ground heights, the cover's strength.
    private Rect fieldArea;
    private int fieldSize;
    private float[] fieldGround;
    private float coverStrength;
    private Terrain terrain;
    private TerrainTreeFelling felling;
    private int buildings;
    private bool redrawing;
    private Action redrawWhenReady;
    private readonly List<(Vector3 at, float due)> felled = new List<(Vector3 at, float due)>();

    /// <summary>True while a field is bound (a generated raid with Look › Ground Light on).</summary>
    public bool HasField => field != null;

    public void Initialise(RenderingSettings rules, SkyOcclusionMap occlusion)
    {
        settings = rules;
        cover = occlusion;
        Current = this;
        enabled = false;
        redrawWhenReady = () =>
        {
            if (cover != null && cover.Pinned && !redrawing)
            {
                StartCoroutine(Redraw(cover.PinnedArea));
            }
        };
        // Bound whenever no field is, so the shaders' texture is never a missing or destroyed one (they don't read it then anyway).
        placeholder = new Texture2DArray(1, 1, 2, TextureFormat.RGBA32, false, true)
        {
            name = "Ground Light (none)", hideFlags = HideFlags.HideAndDontSave,
        };
        placeholder.SetPixels32(new[] { new Color32(0, 0, 0, 255) }, 0);
        placeholder.SetPixels32(new[] { new Color32(255, 255, 255, 255) }, 1);
        placeholder.Apply(false, true);
        Unbind();
    }

    /// <summary>A new scene: no field until a generated raid map builds one, and the cover map follows the player again (SkyOcclusionMap.Bind).</summary>
    public void Clear()
    {
        generation++;
        SceneTransition.CancelWhenReady(redrawWhenReady);
        StopAllCoroutines();
        redrawing = false;
        felled.Clear();
        if (felling != null)
        {
            felling.TreeDown -= OnTreeDown;
            felling = null;
        }

        terrain = null;
        fieldGround = null;
        Unbind();
        if (field != null)
        {
            Destroy(field);
            field = null;
        }

        enabled = false;
    }

    /// <summary>
    /// Works out a generated raid map's light, once its ground, trees and set pieces are in place (RaidMap, behind the loading screen).
    /// <paramref name="groundColours"/> is the paint as colour (TerrainGrowth.GroundColours); <paramref name="water"/> says whether a
    /// point (metres from the terrain's corner) lies under water. Logs what each step took.
    /// </summary>
    public IEnumerator Build(Terrain mapTerrain, Color32[] groundColours, Func<Vector2, bool> water)
    {
        Clear();
        if (settings == null || !settings.GroundLight || cover == null || !cover.CanDraw || mapTerrain == null || mapTerrain.terrainData == null)
        {
            yield break;
        }

        int mine = generation;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var step = System.Diagnostics.Stopwatch.StartNew();
        var times = new System.Text.StringBuilder();
        terrain = mapTerrain;
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 dataSize = data.size;
        float side = Mathf.Max(dataSize.x, dataSize.z);
        var map = new Rect(origin.x, origin.z, side, side);
        float margin = side * MarginShare;
        var area = new Rect(map.xMin - margin, map.yMin - margin, side + 2f * margin, side + 2f * margin);
        int coverSize = Resolution(area.width, settings.GroundCoverTexel, 2048);
        Bounds bounds = data.bounds;
        RenderTexture drawn = cover.Pin(area, coverSize, origin.y + bounds.min.y - 10f, origin.y + bounds.max.y + 150f);
        if (drawn == null)
        {
            yield break;
        }

        times.Append($"cover drawn once {step.ElapsedMilliseconds} ms ({coverSize}² over {area.width:0} m), ");
        buildings = BuildingObject.Active.Count;
        if (terrain.TryGetComponent(out felling))
        {
            felling.TreeDown += OnTreeDown;
        }

        enabled = true;
        // Caches and resource nodes are placed after the map (RaidGenerator): once every setup step is done, the map is drawn
        // again, still behind the loading screen.
        SceneTransition.WhenReady(redrawWhenReady);
        RaidGroundLight look = GameSession.ActiveRaid?.Definition != null ? GameSession.ActiveRaid.Definition.GroundLight : null;
        if (look == null || !look.Enabled)
        {
            RenderTexture.ReleaseTemporary(drawn);
            Debug.Log($"Raid ground light in {clock.ElapsedMilliseconds} ms: {times}no field (Look › Ground Light off).", this);
            yield break;
        }

        step.Restart();
        float[] coverHeights = null;
        yield return ReadBack(drawn, heights => coverHeights = heights);
        RenderTexture.ReleaseTemporary(drawn);
        if (mine != generation || coverHeights == null)
        {
            yield break;
        }

        times.Append($"read back {step.ElapsedMilliseconds} ms, ");
        step.Restart();
        int size = Resolution(side, settings.GroundLightCell, 1024);
        float cell = side / size;
        int heightResolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, heightResolution, heightResolution);
        int colourWidth = data.alphamapWidth, colourHeight = data.alphamapHeight;
        Vector3 toward = TowardLight(out float hour, out bool dark);
        var recipe = new GroundLightField.Recipe
        {
            Size = size,
            Cell = cell,
            Cover = coverHeights,
            CoverSize = coverSize,
            CoverCorner = new Vector2(-margin, -margin),
            CoverSide = area.width,
            TowardLight = toward,
            CoverStrength = cover.Strength,
            Relief = look.Relief,
            Bounce = look.Bounce > 0f && groundColours != null && groundColours.Length == colourWidth * colourHeight,
            Dark = dark,
            NightLiftMost = settings.NightLiftMost,
            ReliefDirections = settings.ReliefDirections,
            ReliefNearest = settings.ReliefReach.x,
            ReliefFarthest = settings.ReliefReach.y,
            BounceSpread = settings.BounceSpread,
        };
        times.Append($"terrain read {step.ElapsedMilliseconds} ms, ");

        // The arithmetic off the main thread, so the loading screen keeps moving.
        step.Restart();
        Task<GroundLightField.Result> work = Task.Run(() =>
        {
            recipe.Ground = GroundHeights(heights, heightResolution, size, cell, dataSize, origin.y);
            if (recipe.Bounce)
            {
                recipe.Albedo = GroundAlbedo(groundColours, colourWidth, colourHeight, size, cell, dataSize, water);
            }

            return GroundLightField.Build(recipe);
        });
        while (!work.IsCompleted)
        {
            yield return null;
        }

        if (mine != generation)
        {
            yield break;
        }

        if (work.IsFaulted || work.Result == null)
        {
            Debug.LogError($"Raid ground light: the field could not be worked out ({work.Exception?.GetBaseException().Message}); the cover map alone stands.", this);
            yield break;
        }

        GroundLightField.Result result = work.Result;
        times.Append($"field worked out {step.ElapsedMilliseconds} ms ({result.Timings}), ");
        step.Restart();
        field = new Texture2DArray(size, size, 2, TextureFormat.RGBA32, false, true)
        {
            name = "Ground Light", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
        };
        field.SetPixels32(result.Light, 0);
        field.SetPixels32(result.Ground, 1);
        field.Apply(false, true);
        fieldArea = map;
        fieldSize = size;
        fieldGround = recipe.Ground;
        coverStrength = recipe.CoverStrength;
        Color tint = look.BounceTint.linear * look.Bounce;
        Shader.SetGlobalTexture(FieldId, field);
        Shader.SetGlobalVector(BounceId, new Vector4(tint.r, tint.g, tint.b, 0f));
        Shader.SetGlobalVector(RectId, new Vector4(map.xMin, map.yMin, 1f / side, result.Lift));
        times.Append($"uploaded {step.ElapsedMilliseconds} ms");
        Debug.Log($"Raid ground light built in {clock.ElapsedMilliseconds} ms ({size}² cells of {cell:0.00} m; hour {hour:0.0}" +
                  $"{(dark ? $", dark: night lift {result.Lift:0.00}" : "")}; relief {look.Relief:0.00}, ground's mean light {result.MeanGround:0.00}; " +
                  $"bounce {look.Bounce:0.00}): {times}.", this);
    }

    private void LateUpdate()
    {
        if (cover == null || !cover.Pinned)
        {
            enabled = false;
            return;
        }

        if (redrawing)
        {
            return;
        }

        // A building going up or coming down: rare in a raid, so the whole map is drawn again.
        if (BuildingObject.Active.Count != buildings)
        {
            buildings = BuildingObject.Active.Count;
            StartCoroutine(Redraw(cover.PinnedArea));
            return;
        }

        if (felled.Count > 0 && Time.time >= felled[0].due)
        {
            Vector3 at = felled[0].at;
            felled.RemoveAt(0);
            StartCoroutine(Redraw(new Rect(at.x - FelledReach, at.z - FelledReach, FelledReach * 2f, FelledReach * 2f)));
        }
    }

    // A felled tree: once it has landed, its patch is drawn again. (A tree growing back in a raid isn't: no event for it.)
    private void OnTreeDown(int index)
    {
        if (terrain == null || terrain.terrainData == null || index < 0 || index >= terrain.terrainData.treeInstanceCount)
        {
            return;
        }

        Vector3 at = terrain.transform.position + Vector3.Scale(terrain.terrainData.GetTreeInstance(index).position, terrain.terrainData.size);
        felled.Add((at, Time.time + settings.FelledRedrawDelay));
    }

    // Draws a patch of the cover map again and, with a field bound, works the ground's cover out again for the cells well inside
    // it (the cover test reaches a metre out), on a worker thread.
    private IEnumerator Redraw(Rect ground)
    {
        redrawing = true;
        int mine = generation;
        RectInt texels = cover.TexelsOver(ground);
        RenderTexture drawn = cover.Redraw(texels);
        if (drawn == null || field == null || fieldGround == null)
        {
            if (drawn != null)
            {
                RenderTexture.ReleaseTemporary(drawn);
            }

            redrawing = false;
            yield break;
        }

        float[] heights = null;
        yield return ReadBack(drawn, read => heights = read);
        RenderTexture.ReleaseTemporary(drawn);
        if (mine != generation || heights == null || field == null)
        {
            redrawing = false;
            yield break;
        }

        Rect area = cover.PinnedArea;
        float texelsPerMetre = cover.PinnedResolution / area.width;
        float cell = fieldArea.width / fieldSize;
        // The patch's corner in metres from the field's corner, and the cells whose centres lie 1.5 m inside it.
        var corner = new Vector2(area.xMin + texels.x / texelsPerMetre - fieldArea.xMin, area.yMin + texels.y / texelsPerMetre - fieldArea.yMin);
        int x0 = Mathf.Max(0, Mathf.CeilToInt((corner.x + 1.5f) / cell - 0.5f));
        int z0 = Mathf.Max(0, Mathf.CeilToInt((corner.y + 1.5f) / cell - 0.5f));
        int x1 = Mathf.Min(fieldSize - 1, Mathf.FloorToInt((corner.x + texels.width / texelsPerMetre - 1.5f) / cell - 0.5f));
        int z1 = Mathf.Min(fieldSize - 1, Mathf.FloorToInt((corner.y + texels.height / texelsPerMetre - 1.5f) / cell - 0.5f));
        if (x1 < x0 || z1 < z0)
        {
            redrawing = false;
            yield break;
        }

        int width = x1 - x0 + 1, height = z1 - z0 + 1;
        float[] groundHeights = fieldGround;
        int size = fieldSize;
        float strength = coverStrength;
        int blockWidth = texels.width, blockHeight = texels.height;
        Task<Color32[]> work = Task.Run(() =>
        {
            var pixels = new Color32[width * height];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cx = x0 + x, cz = z0 + z;
                    float factor = GroundLightField.CoverAt(heights, blockWidth, blockHeight, corner, texelsPerMetre,
                        (cx + 0.5f) * cell, (cz + 0.5f) * cell, groundHeights[cz * size + cx], strength);
                    pixels[z * width + x] = new Color32(GroundLightField.Byte(factor), 255, 255, 255);
                }
            }

            return pixels;
        });
        while (!work.IsCompleted)
        {
            yield return null;
        }

        if (mine == generation && field != null && !work.IsFaulted)
        {
            var patch = new Texture2D(width, height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            patch.SetPixels32(work.Result);
            patch.Apply(false);
            Graphics.CopyTexture(patch, 0, 0, 0, 0, width, height, field, 1, 0, x0, z0);
            Destroy(patch);
        }

        redrawing = false;
    }

    // What the GPU drew, as heights: asynchronously where the platform can, else at once.
    private static IEnumerator ReadBack(RenderTexture texture, Action<float[]> done)
    {
        if (SystemInfo.supportsAsyncGPUReadback)
        {
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RFloat);
            while (!request.done)
            {
                yield return null;
            }

            if (!request.hasError)
            {
                done(request.GetData<float>().ToArray());
                yield break;
            }
        }

        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RFloat, false, true);
        RenderTexture was = RenderTexture.active;
        RenderTexture.active = texture;
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
        RenderTexture.active = was;
        done(copy.GetRawTextureData<float>().ToArray());
        Destroy(copy);
    }

    // Towards the sun (or the moon) at the run's hour, as the scene's day/night profile will set it (RaidSky sets the hour after
    // the map is made), and whether the hour is dark.
    private static Vector3 TowardLight(out float hour, out bool dark)
    {
        RaidTimeOfDay time = GameSession.ActiveRaid?.TimeOfDay;
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        hour = time != null ? time.hour : cycle != null ? cycle.Hour : 12f;
        if (cycle != null && cycle.Profile != null)
        {
            DayNightState state = cycle.Profile.Evaluate(hour);
            dark = time != null ? time.IsDark : state.IsMoon;
            return -(state.LightRotation * Vector3.forward);
        }

        dark = time != null && time.IsDark;
        Light sun = RenderSettings.sun;
        return sun != null ? -sun.transform.forward : Vector3.up;
    }

    /// <summary>The ground's height in world metres at each field cell's centre, from the terrain's heights (normalised, [z, x]).</summary>
    public static float[] GroundHeights(float[,] heights, int resolution, int size, float cell, Vector3 terrainSize, float baseHeight)
    {
        var ground = new float[size * size];
        for (int z = 0; z < size; z++)
        {
            float v = Mathf.Clamp((z + 0.5f) * cell / terrainSize.z * (resolution - 1), 0f, resolution - 1);
            int v0 = (int)v, v1 = Math.Min(v0 + 1, resolution - 1);
            float fv = v - v0;
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Clamp((x + 0.5f) * cell / terrainSize.x * (resolution - 1), 0f, resolution - 1);
                int u0 = (int)u, u1 = Math.Min(u0 + 1, resolution - 1);
                float fu = u - u0;
                float south = heights[v0, u0] + (heights[v0, u1] - heights[v0, u0]) * fu;
                float north = heights[v1, u0] + (heights[v1, u1] - heights[v1, u0]) * fu;
                ground[z * size + x] = baseHeight + (south + (north - south) * fv) * terrainSize.y;
            }
        }

        return ground;
    }

    /// <summary>The ground's colour (linear) at each field cell's centre, from the paint's colours; much darker under water.</summary>
    public static Vector3[] GroundAlbedo(Color32[] colours, int width, int height, int size, float cell, Vector3 terrainSize, Func<Vector2, bool> water)
    {
        var albedo = new Vector3[size * size];
        for (int z = 0; z < size; z++)
        {
            float metresZ = (z + 0.5f) * cell;
            float v = Mathf.Clamp(metresZ / terrainSize.z * height - 0.5f, 0f, height - 1);
            int v0 = (int)v, v1 = Math.Min(v0 + 1, height - 1);
            float fv = v - v0;
            for (int x = 0; x < size; x++)
            {
                float metresX = (x + 0.5f) * cell;
                float u = Mathf.Clamp(metresX / terrainSize.x * width - 0.5f, 0f, width - 1);
                int u0 = (int)u, u1 = Math.Min(u0 + 1, width - 1);
                float fu = u - u0;
                Vector3 south = Vector3.Lerp(ToLinear(colours[v0 * width + u0]), ToLinear(colours[v0 * width + u1]), fu);
                Vector3 north = Vector3.Lerp(ToLinear(colours[v1 * width + u0]), ToLinear(colours[v1 * width + u1]), fu);
                Vector3 colour = Vector3.Lerp(south, north, fv);
                if (water != null && water(new Vector2(metresX, metresZ)))
                {
                    colour *= WaterAlbedo;
                }

                albedo[z * size + x] = colour;
            }
        }

        return albedo;
    }

    private static Vector3 ToLinear(Color32 colour) => new Vector3(Linear[colour.r], Linear[colour.g], Linear[colour.b]);

    private static float[] SrgbToLinear()
    {
        var table = new float[256];
        for (int i = 0; i < 256; i++)
        {
            float c = i / 255f;
            table[i] = c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
        }

        return table;
    }

    private static int Resolution(float metres, float perTexel, int most) =>
        Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(metres / Mathf.Max(0.01f, perTexel))), 64, most);

    private void Unbind()
    {
        Shader.SetGlobalVector(RectId, Vector4.zero);
        Shader.SetGlobalVector(BounceId, Vector4.zero);
        if (placeholder != null)
        {
            Shader.SetGlobalTexture(FieldId, placeholder);
        }
    }

    private void OnDestroy()
    {
        Clear();
        if (placeholder != null)
        {
            Destroy(placeholder);
        }

        if (Current == this)
        {
            Current = null;
        }
    }
}
