using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A generated raid map's ground light (Docs/LightingBakeResearch_2026-11-17.md §2.1), worked out once at the end of the map's
/// load (RaidMap) while the loading screen is up, since a run's hour never changes:
/// <list type="bullet">
/// <item>the sky-occlusion map drawn once over the whole map (SkyOcclusionMap.Pin) instead of round the player every 12 m or
/// 10 s, with whatever moves left out, and drawn again as a late setup step once everything else is placed (caches, resource
/// nodes); afterwards only where something changed: a felled tree's patch once it has come down, a gathered or regrown
/// resource's, a tree growing back, a building's ground where it went up, came down or was moved;</item>
/// <item>where the raid's Look › Ground Light is on, a two-slice texture the shaders read through GroundLight.hlsl: the canopy's
/// shade on the ground (for the terrain, water and mist, which don't read the sky map), the land's own shade (relief), and light
/// thrown back from sunlit ground (bounce). The arithmetic is GroundLightField's, on a worker thread.</item>
/// </list>
/// The Haven, the prologue and painted maps keep the sky map round the player and never bind a field. Made by WorldRendering;
/// numbers in RenderingSettings, each raid's in RaidDefinition › Look › Ground Light.
/// </summary>
public sealed class GroundLightMap : MonoBehaviour, ISceneSetup
{
    private static readonly int FieldId = Shader.PropertyToID("_GroundLightField");
    private static readonly int RectId = Shader.PropertyToID("_GroundLightRect");
    private static readonly int BounceId = Shader.PropertyToID("_GroundLightBounce");

    /// <summary>Metres round a felled or regrown tree drawn again: its crown, and the reach of the cover test round it.</summary>
    private const float FelledReach = 12f;
    /// <summary>Seconds after the cut by which a felled tree's patch is drawn even if it hasn't come to rest (FelledTree gives up
    /// falling after 10 s and settling after 3).</summary>
    private const float FelledLongest = 15f;
    /// <summary>Metres from where a tree stood that its falling copy is looked for.</summary>
    private const float FelledFind = 6f;
    /// <summary>Seconds between looks at a tree still coming down.</summary>
    private const float FelledRecheck = 0.5f;
    /// <summary>Metres round a gathered or regrown resource drawn again: the rock, and the cover test's reach round it.</summary>
    private const float NodeReach = 5f;
    /// <summary>Seconds after a resource is gathered before its patch is drawn again (it breaks apart first).</summary>
    private const float NodeDelay = 1f;
    /// <summary>Metres round a building's bounds drawn again.</summary>
    private const float BuildingMargin = 2f;
    /// <summary>Metres of cover map round the field: SkyOcclusion.hlsl fades the map out over its outer twelfth, which then lies
    /// outside the map.</summary>
    private const float MarginShare = 0.1f;
    /// <summary>What ground under water throws back, against dry ground (the Marsh: banks darker, hummocks lighter).</summary>
    private const float WaterAlbedo = 0.12f;

    private static readonly float[] Linear = SrgbToLinear();

    public static GroundLightMap Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    // A patch of ground to draw again once it is due: a felled tree's waits until the tree has come to rest.
    private struct Waiting
    {
        public Rect Area;
        public Vector3 At;
        public float Due;
        public float GiveUp;
        public bool Falls;
    }

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
    private TerrainNodeHarvesting harvesting;
    private bool redrawing;
    // The block of the cover map being read back now, released with the readback or by Clear.
    private RenderTexture reading;
    // Readbacks land here and are reused: the map's own half floats, and the heights a worker thread turns them into (or the
    // fallback reads straight into).
    private ushort[] halves = Array.Empty<ushort>();
    private float[] heights = Array.Empty<float>();
    private bool readHalves;
    private readonly List<Rect> dirty = new List<Rect>();
    private readonly List<Waiting> waiting = new List<Waiting>();
    // Each building's ground as the cover map last saw it.
    private readonly List<(BuildingObject building, Rect area)> standing = new List<(BuildingObject, Rect)>();
    private bool buildingsChanged;

    /// <summary>True while a field is bound (a generated raid with Look › Ground Light on).</summary>
    public bool HasField => field != null;

    /// <summary>The late setup step <see cref="Build"/> adds: after every other.</summary>
    public int SetupOrder => int.MaxValue;

    public void Initialise(RenderingSettings rules, SkyOcclusionMap occlusion)
    {
        settings = rules;
        cover = occlusion;
        Current = this;
        enabled = false;
        BuildingObject.ActiveChanged += OnBuildingsChanged;
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
        StopAllCoroutines();
        redrawing = false;
        if (reading != null)
        {
            RenderTexture.ReleaseTemporary(reading);
            reading = null;
        }

        dirty.Clear();
        waiting.Clear();
        standing.Clear();
        buildingsChanged = false;
        if (felling != null)
        {
            felling.TreeDown -= OnTreeDown;
            felling.TreeBack -= OnTreeBack;
            felling = null;
        }

        if (harvesting != null)
        {
            harvesting.NodeChanged -= OnNodeChanged;
            harvesting = null;
        }

        terrain = null;
        fieldGround = null;
        halves = Array.Empty<ushort>();
        heights = Array.Empty<float>();
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
        bool drawnAgainLater = false;
        try
        {
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

            reading = drawn;
            times.Append($"cover drawn once {step.ElapsedMilliseconds} ms ({coverSize}² over {area.width:0} m), ");
            SeeBuildings();
            if (terrain.TryGetComponent(out felling))
            {
                felling.TreeDown += OnTreeDown;
                felling.TreeBack += OnTreeBack;
            }

            if (terrain.TryGetComponent(out harvesting))
            {
                harvesting.NodeChanged += OnNodeChanged;
            }

            // Caches and resource nodes are placed after the map (RaidGenerator): once every setup step is done, the map is drawn
            // again, still behind the loading screen (Setup). Outside a scene's setup nothing is still to come.
            drawnAgainLater = SceneTransition.AddSetup(this);
            RaidGroundLight look = GameSession.ActiveRaid?.Definition != null ? GameSession.ActiveRaid.Definition.GroundLight : null;
            if (look == null || !look.Enabled)
            {
                Release(drawn);
                Debug.Log($"Raid ground light in {clock.ElapsedMilliseconds} ms: {times}no field (Look › Ground Light off).", this);
                yield break;
            }

            step.Restart();
            bool read = false;
            yield return ReadBack(drawn, ok => read = ok);
            Release(drawn);
            if (mine != generation || !read)
            {
                yield break;
            }

            times.Append($"read back {step.ElapsedMilliseconds} ms, ");
            step.Restart();
            int size = Resolution(side, settings.GroundLightCell, 1024);
            float cell = side / size;
            int heightResolution = data.heightmapResolution;
            float[,] terrainHeights = data.GetHeights(0, 0, heightResolution, heightResolution);
            int colourWidth = data.alphamapWidth, colourHeight = data.alphamapHeight;
            Vector3 toward = TowardLight(out float hour, out float dim);
            ushort[] raw = halves;
            float[] coverHeights = heights;
            bool convert = readHalves;
            int texels = coverSize * coverSize;
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
                Dim = dim,
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
                if (convert)
                {
                    GroundLightField.HalvesToFloats(raw, coverHeights, texels);
                }

                recipe.Ground = GroundHeights(terrainHeights, heightResolution, size, cell, dataSize, origin.y);
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
            Debug.Log($"Raid ground light built in {clock.ElapsedMilliseconds} ms ({size}² cells of {cell:0.00} m; hour {hour:0.0}, " +
                      $"{dim:0.00} of the night's lift: {result.Lift:0.00}; relief {look.Relief:0.00}, ground's mean light " +
                      $"{result.MeanGround:0.00}; bounce {look.Bounce:0.00}): {times}.", this);
        }
        finally
        {
            // From now on what changes is drawn again; with a late setup step to come, once that has drawn the whole map.
            if (mine == generation && cover.Pinned && !drawnAgainLater)
            {
                enabled = true;
            }
        }
    }

    /// <summary>
    /// The late setup step <see cref="Build"/> adds: once every other step has placed what it places (caches, resource nodes,
    /// creatures), the whole map's cover is drawn again behind the loading screen, with what moves left out; from then on only
    /// what changes.
    /// </summary>
    public IEnumerator Setup()
    {
        int mine = generation;
        if (cover == null || !cover.Pinned)
        {
            yield break;
        }

        while (redrawing)
        {
            yield return null;
        }

        if (mine != generation)
        {
            yield break;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        dirty.Clear();
        SeeBuildings();
        buildingsChanged = false;
        yield return Redraw(cover.PinnedArea);
        if (mine == generation && cover.Pinned)
        {
            enabled = true;
            Debug.Log($"Raid ground light: the cover drawn again over the whole map once it was set up, in {clock.ElapsedMilliseconds} ms.", this);
        }
    }

    private void LateUpdate()
    {
        if (cover == null || !cover.Pinned)
        {
            enabled = false;
            return;
        }

        if (buildingsChanged)
        {
            buildingsChanged = false;
            BuildingsMoved();
        }

        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            Waiting patch = waiting[i];
            if (Time.time < patch.Due)
            {
                continue;
            }

            if (patch.Falls && StillFalling(patch.At, ref patch.Area) && Time.time < patch.GiveUp)
            {
                patch.Due = Time.time + FelledRecheck;
                waiting[i] = patch;
                continue;
            }

            waiting.RemoveAt(i);
            MarkDirty(patch.Area);
        }

        if (!redrawing && dirty.Count > 0)
        {
            Rect next = dirty[0];
            dirty.RemoveAt(0);
            StartCoroutine(Redraw(next));
        }
    }

    // A felled tree: its patch is drawn again once the tree has come down and lies still (never before: the cover map is kept
    // all run), covering where it lies as well as where it stood.
    private void OnTreeDown(int index)
    {
        if (TreeAt(index, out Vector3 at))
        {
            waiting.Add(new Waiting
            {
                Area = Square(at, FelledReach), At = at, Due = Time.time + settings.FelledRedrawDelay, GiveUp = Time.time + FelledLongest, Falls = true,
            });
        }
    }

    // A tree growing back where it was felled: its crown shades the ground again.
    private void OnTreeBack(int index)
    {
        if (TreeAt(index, out Vector3 at))
        {
            MarkDirty(Square(at, FelledReach));
        }
    }

    // A painted rock gathered (gone until it grows back) or grown back.
    private void OnNodeChanged(Vector3 at) =>
        waiting.Add(new Waiting { Area = Square(at, NodeReach), At = at, Due = Time.time + NodeDelay, GiveUp = 0f });

    private void OnBuildingsChanged() => buildingsChanged = true;

    private bool TreeAt(int index, out Vector3 at)
    {
        at = default;
        if (terrain == null || terrain.terrainData == null || index < 0 || index >= terrain.terrainData.treeInstanceCount)
        {
            return false;
        }

        at = terrain.transform.position + Vector3.Scale(terrain.terrainData.GetTreeInstance(index).position, terrain.terrainData.size);
        return true;
    }

    // Whether the felled copy of a tree that stood at a spot is still coming down or settling; the patch grows to take in where
    // it lies, trunk and crown.
    private static bool StillFalling(Vector3 at, ref Rect patch)
    {
        bool moving = false;
        foreach (FelledTree tree in FindObjectsByType<FelledTree>())
        {
            Vector3 offset = tree.transform.position - at;
            if (offset.x * offset.x + offset.z * offset.z > FelledFind * FelledFind)
            {
                continue;
            }

            moving |= !tree.IsLying;
            foreach (Renderer part in tree.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = part.bounds;
                patch = Rect.MinMaxRect(Mathf.Min(patch.xMin, bounds.min.x), Mathf.Min(patch.yMin, bounds.min.z),
                                        Mathf.Max(patch.xMax, bounds.max.x), Mathf.Max(patch.yMax, bounds.max.z));
            }
        }

        return moving;
    }

    // Buildings that went up, came down or were set down elsewhere since the cover map last saw them: their ground then and now.
    private void BuildingsMoved()
    {
        IReadOnlyList<BuildingObject> now = BuildingObject.Active;
        for (int i = standing.Count - 1; i >= 0; i--)
        {
            (BuildingObject building, Rect area) was = standing[i];
            bool still = was.building != null && Contains(now, was.building);
            if (still)
            {
                Rect here = GroundUnder(was.building);
                if (here == was.area)
                {
                    continue;
                }

                standing[i] = (was.building, here);
                MarkDirty(here);
            }
            else
            {
                standing.RemoveAt(i);
            }

            MarkDirty(was.area);
        }

        foreach (BuildingObject building in now)
        {
            if (building != null && !Seen(building))
            {
                Rect area = GroundUnder(building);
                standing.Add((building, area));
                MarkDirty(area);
            }
        }
    }

    // Where every building stands now, as the whole map is drawn.
    private void SeeBuildings()
    {
        standing.Clear();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building != null)
            {
                standing.Add((building, GroundUnder(building)));
            }
        }
    }

    private bool Seen(BuildingObject building)
    {
        foreach (var seen in standing)
        {
            if (ReferenceEquals(seen.building, building))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(IReadOnlyList<BuildingObject> list, BuildingObject building)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], building))
            {
                return true;
            }
        }

        return false;
    }

    private static Rect GroundUnder(BuildingObject building)
    {
        Bounds world = BuildPlacement.TransformBounds(building.LocalBounds, building.transform.localToWorldMatrix);
        return Rect.MinMaxRect(world.min.x - BuildingMargin, world.min.z - BuildingMargin, world.max.x + BuildingMargin, world.max.z + BuildingMargin);
    }

    private static Rect Square(Vector3 at, float reach) => new Rect(at.x - reach, at.z - reach, reach * 2f, reach * 2f);

    // A patch to draw again, merged with any waiting that it meets.
    private void MarkDirty(Rect area)
    {
        for (int i = 0; i < dirty.Count; i++)
        {
            if (dirty[i].Overlaps(area))
            {
                Rect other = dirty[i];
                area = Rect.MinMaxRect(Mathf.Min(area.xMin, other.xMin), Mathf.Min(area.yMin, other.yMin),
                                       Mathf.Max(area.xMax, other.xMax), Mathf.Max(area.yMax, other.yMax));
                dirty.RemoveAt(i);
                i = -1;
            }
        }

        dirty.Add(area);
    }

    // Draws a patch of the cover map again and, with a field bound, works the ground's cover out again for the cells well inside
    // it (the cover test reaches a metre out), on a worker thread.
    private IEnumerator Redraw(Rect ground)
    {
        redrawing = true;
        int mine = generation;
        try
        {
            RectInt texels = cover.TexelsOver(ground);
            RenderTexture drawn = cover.Redraw(texels);
            if (drawn == null)
            {
                yield break;
            }

            reading = drawn;
            if (field == null || fieldGround == null)
            {
                Release(drawn);
                yield break;
            }

            bool read = false;
            yield return ReadBack(drawn, ok => read = ok);
            Release(drawn);
            if (mine != generation || !read || field == null)
            {
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
                yield break;
            }

            int width = x1 - x0 + 1, height = z1 - z0 + 1;
            float[] groundHeights = fieldGround;
            int size = fieldSize;
            float strength = coverStrength;
            int blockWidth = texels.width, blockHeight = texels.height;
            ushort[] raw = halves;
            float[] coverHeights = heights;
            bool convert = readHalves;
            Task<Color32[]> work = Task.Run(() =>
            {
                if (convert)
                {
                    GroundLightField.HalvesToFloats(raw, coverHeights, blockWidth * blockHeight);
                }

                var pixels = new Color32[width * height];
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int cx = x0 + x, cz = z0 + z;
                        float factor = GroundLightField.CoverAt(coverHeights, blockWidth, blockHeight, corner, texelsPerMetre,
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
        }
        finally
        {
            if (mine == generation)
            {
                redrawing = false;
            }
        }
    }

    // Gives a drawn block back, unless Clear already has.
    private void Release(RenderTexture drawn)
    {
        if (drawn != null && reading == drawn)
        {
            RenderTexture.ReleaseTemporary(drawn);
            reading = null;
        }
    }

    // What the GPU drew, into the reused buffers: asynchronously as the map's own half floats where the platform can (a worker
    // thread turns them into heights), else at once as floats. done(false): nothing came back.
    private IEnumerator ReadBack(RenderTexture texture, Action<bool> done)
    {
        int count = texture.width * texture.height;
        if (heights.Length < count)
        {
            heights = new float[count];
        }

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            if (halves.Length < count)
            {
                halves = new ushort[count];
            }

            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0);
            while (!request.done)
            {
                yield return null;
            }

            if (!request.hasError)
            {
                NativeArray<ushort> data = request.GetData<ushort>();
                if (data.Length >= count)
                {
                    NativeArray<ushort>.Copy(data, 0, halves, 0, count);
                    readHalves = true;
                    done(true);
                    yield break;
                }
            }
        }

        if (texture == null || !texture.IsCreated())
        {
            done(false);
            yield break;
        }

        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RFloat, false, true);
        RenderTexture was = RenderTexture.active;
        RenderTexture.active = texture;
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
        RenderTexture.active = was;
        NativeArray<float>.Copy(copy.GetRawTextureData<float>(), 0, heights, 0, count);
        Destroy(copy);
        readHalves = false;
        done(true);
    }

    // Towards the sun (or the moon) at the run's hour, as the scene's day/night profile will set it (RaidSky sets the hour after
    // the map is made), and how much of the night's lift the hour gets: by how dark it is (GroundLightField.LiftShare).
    private Vector3 TowardLight(out float hour, out float dim)
    {
        RaidTimeOfDay time = GameSession.ActiveRaid?.TimeOfDay;
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        hour = time != null ? time.hour : cycle != null ? cycle.Hour : 12f;
        if (cycle != null && cycle.Profile != null)
        {
            DayNightState state = cycle.Profile.Evaluate(hour);
            dim = GroundLightField.LiftShare(state.Night, state.IsMoon ? 0f : state.Daylight, settings.LiftBelowSun);
            return -(state.LightRotation * Vector3.forward);
        }

        dim = time != null && time.IsDark ? 1f : 0f;
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
        BuildingObject.ActiveChanged -= OnBuildingsChanged;
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
