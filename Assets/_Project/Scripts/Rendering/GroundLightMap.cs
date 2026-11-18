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
    // Slice 1's height range (x lowest, y range, z 1 while a field with heights is bound, w the water's reach): GroundLight.hlsl reads the
    // ground and the water.
    private static readonly int HeightsId = Shader.PropertyToID("_GroundLightHeights");
    // The terrain's own heightmap (16-bit, a texel in 0.4 m) and where it lies: what a rock's foot blends into (GroundBlend.hlsl); the
    // field's ground is a byte over the map's heights in coarser cells, which on a bank reads a metre off.
    private static readonly int GroundHeightmapId = Shader.PropertyToID("_GroundBlendHeightmap");
    private static readonly int GroundPlaceId = Shader.PropertyToID("_GroundBlendPlace");
    private static readonly int GroundScaleId = Shader.PropertyToID("_GroundBlendScale");
    // A heightmap texture holds the terrain's 0-1 heights as 0 to this (Unity's kMaxHeight).
    private const float HeightmapTop = 32766f / 65535f;
    /// <summary>Metres from a cell to the water's edge within which its shore is wet (RaidMap's water level lookup).</summary>
    public const float ShoreReach = 5f;
    private static readonly int BounceId = Shader.PropertyToID("_GroundLightBounce");

    /// <summary>Metres round a felled or regrown tree drawn again: its crown.</summary>
    private const float FelledReach = 12f;
    /// <summary>Metres round a gathered or regrown resource drawn again: the rock.</summary>
    private const float NodeReach = 5f;
    /// <summary>Metres a field cell's cover test reaches from its centre: GroundLightField.CoverAt's taps, a metre out.</summary>
    public const float CoverTapReach = 1f;
    /// <summary>Metres inside a drawn block of the cover map a cell's centre must lie for its cover test to read only what was drawn:
    /// the taps' reach and the blend between texels.</summary>
    public const float CellInset = 1.5f;
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

    // A patch of ground to draw again once it is due: a felled tree's a while after the cut, a gathered rock's once it has broken apart.
    private struct Waiting
    {
        public Rect Area;
        public float Due;
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
    private float[] fieldWater;
    private float[] fieldNear;
    private float fieldLowest, fieldHighest = 1f;
    private float coverStrength;
    // The first build's recipe (its ground, colours and land's shade), kept until the late setup step has worked the field out again
    // from the finished map's cover.
    private GroundLightField.Recipe rebuild;
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
        fieldWater = null;
        fieldNear = null;
        rebuild = null;
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
    public IEnumerator Build(Terrain mapTerrain, Color32[] groundColours, Func<Vector2, bool> water, Func<Vector2, Vector2> shoreAt = null)
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
            IEnumerator readBack = ReadBack(drawn, ok => read = ok);
            while (Advance(readBack))
            {
                yield return null;
            }

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
                if (shoreAt != null)
                {
                    recipe.Water = WaterLevels(shoreAt, size, cell, origin.y, out recipe.WaterNear);
                }
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
            fieldArea = map;
            fieldSize = size;
            fieldGround = recipe.Ground;
            fieldWater = recipe.Water;
            fieldNear = recipe.WaterNear;
            fieldLowest = result.LowestHeight;
            fieldHighest = result.HighestHeight;
            coverStrength = recipe.CoverStrength;
            Color tint = look.BounceTint.linear * look.Bounce;
            Shader.SetGlobalVector(BounceId, new Vector4(tint.r, tint.g, tint.b, look.Sunflecks));
            Show(result);
            // Worked out again by the late setup step from the finished map's cover (Setup), the land's shade kept.
            recipe.KnownRelief = result.Relief;
            recipe.Cover = null;
            rebuild = drawnAgainLater ? recipe : null;
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
    /// creatures), the whole map's cover is drawn again behind the loading screen, with what moves left out, and with a field bound the
    /// whole field is worked out again from it: the ground's cover, and the bounce's sun test and the night's lift, which read the
    /// cover too. From then on only what changes. Driven step by step, so a fault in it is logged and the loading screen still goes.
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
        bool whole = rebuild != null && field != null;
        IEnumerator redraw = whole ? Rebuild() : Redraw(cover.PinnedArea, true);
        while (Advance(redraw))
        {
            yield return redraw.Current;
        }

        rebuild = null;
        if (mine == generation && cover.Pinned)
        {
            enabled = true;
            Debug.Log($"Raid ground light: the cover drawn again over the whole map once it was set up{(whole ? ", and the field worked out again from it" : "")}, " +
                      $"in {clock.ElapsedMilliseconds} ms.", this);
        }
    }

    // The whole field worked out again from the whole cover drawn anew (the late setup step), the land's shade kept from the first build:
    // what was placed since (caches, resource nodes) shades the ground, keeps the sun off it and counts in the night's lift.
    private IEnumerator Rebuild()
    {
        redrawing = true;
        int mine = generation;
        try
        {
            int resolution = cover.PinnedResolution;
            RenderTexture drawn = cover.Redraw(new RectInt(0, 0, resolution, resolution), true);
            if (drawn == null)
            {
                yield break;
            }

            reading = drawn;
            bool read = false;
            IEnumerator readBack = ReadBack(drawn, ok => read = ok);
            while (Advance(readBack))
            {
                yield return null;
            }

            Release(drawn);
            GroundLightField.Recipe recipe = rebuild;
            if (mine != generation || !read || field == null || recipe == null)
            {
                yield break;
            }

            Rect area = cover.PinnedArea;
            recipe.Cover = heights;
            recipe.CoverSize = resolution;
            recipe.CoverSide = area.width;
            recipe.CoverCorner = new Vector2(area.xMin - fieldArea.xMin, area.yMin - fieldArea.yMin);
            ushort[] raw = halves;
            float[] coverHeights = heights;
            bool convert = readHalves;
            int texels = resolution * resolution;
            Task<GroundLightField.Result> work = Task.Run(() =>
            {
                if (convert)
                {
                    GroundLightField.HalvesToFloats(raw, coverHeights, texels);
                }

                return GroundLightField.Build(recipe);
            });
            while (!work.IsCompleted)
            {
                yield return null;
            }

            recipe.Cover = null;
            if (mine != generation || field == null)
            {
                yield break;
            }

            if (work.IsFaulted || work.Result == null)
            {
                Debug.LogError($"Raid ground light: the field could not be worked out again ({work.Exception?.GetBaseException().Message}); the first stands.", this);
                yield break;
            }

            Show(work.Result);
        }
        finally
        {
            if (mine == generation)
            {
                redrawing = false;
            }
        }
    }

    // Binds a built field: its two slices in a new texture (the bound one is unreadable once uploaded), and its night's lift.
    private void Show(GroundLightField.Result result)
    {
        var made = new Texture2DArray(fieldSize, fieldSize, 2, TextureFormat.RGBA32, false, true)
        {
            name = "Ground Light", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
        };
        made.SetPixels32(result.Light, 0);
        made.SetPixels32(result.Ground, 1);
        made.Apply(false, true);
        if (field != null)
        {
            Destroy(field);
        }

        field = made;
        Shader.SetGlobalTexture(FieldId, field);
        Shader.SetGlobalVector(RectId, new Vector4(fieldArea.xMin, fieldArea.yMin, 1f / fieldArea.width, result.Lift));
        Shader.SetGlobalVector(HeightsId, new Vector4(result.LowestHeight, result.HighestHeight - result.LowestHeight, 1f, GroundLightField.WaterReach));
        BindHeightmap();
    }

    // The terrain's heightmap for GroundBlend.hlsl: uv = (xz - corner) * (resolution - 1) / (resolution * size) + half a texel, as the
    // terrain's samples sit on its corners.
    private void BindHeightmap()
    {
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null || data.heightmapTexture == null)
        {
            Shader.SetGlobalVector(GroundScaleId, Vector4.zero);
            return;
        }

        Vector3 corner = terrain.GetPosition();
        Vector3 size = data.size;
        float resolution = data.heightmapResolution;
        Shader.SetGlobalTexture(GroundHeightmapId, data.heightmapTexture);
        Shader.SetGlobalVector(GroundPlaceId, new Vector4(corner.x, corner.z, (resolution - 1f) / (resolution * size.x), (resolution - 1f) / (resolution * size.z)));
        Shader.SetGlobalVector(GroundScaleId, new Vector4(corner.y, size.y / HeightmapTop, 0.5f / resolution, 1f));
    }

    /// <summary>The water surface nearest each cell centre (world metres; NaN where none lies within <see cref="ShoreReach"/>) and how
    /// near its edge is (1 at it or in the water, 0 at Shore Reach; 1 where the edge is unknown, as the fen's), from the map's own lookup:
    /// <paramref name="shoreAt"/> takes metres from the terrain's corner and gives (the surface in metres above its lowest point, metres
    /// to its edge), x NaN for none. Plain arithmetic, for the worker thread.</summary>
    public static float[] WaterLevels(Func<Vector2, Vector2> shoreAt, int size, float cell, float baseHeight, out float[] near)
    {
        var levels = new float[size * size];
        near = new float[size * size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 shore = shoreAt(new Vector2((x + 0.5f) * cell, (z + 0.5f) * cell));
                int index = z * size + x;
                levels[index] = float.IsNaN(shore.x) ? float.NaN : baseHeight + shore.x;
                near[index] = float.IsNaN(shore.y) ? 1f : Mathf.Clamp01(1f - shore.y / ShoreReach);
            }
        }

        return levels;
    }

    // One step of a routine driven by hand rather than nested: false once it ends or throws (logged), so whoever drives it goes on, its
    // finally blocks run, and a fault never leaves the loading screen up or the redraws stopped for the run.
    private bool Advance(IEnumerator routine)
    {
        try
        {
            return routine.MoveNext();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
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

    // A felled tree: its patch is drawn again a while after the cut (Rendering Settings › Felled Redraw Delay), without its crown. The
    // falling or lying tree itself is never drawn (it has a body: SkyOcclusionMap leaves it out), so there is nothing to wait for.
    private void OnTreeDown(int index)
    {
        if (TreeAt(index, out Vector3 at))
        {
            waiting.Add(new Waiting { Area = Square(at, FelledReach), Due = Time.time + settings.FelledRedrawDelay });
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
    private void OnNodeChanged(Vector3 at) => waiting.Add(new Waiting { Area = Square(at, NodeReach), Due = Time.time + NodeDelay });

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

    /// <summary>
    /// The square of cover map to draw again when what stands over <paramref name="changed"/> (metres, x and z) changed: grown by the
    /// reach of a cell's cover test and a texel, so every cell whose test can see the change is worked out again, and by the inset those
    /// cells need inside what is drawn. Drawn only over the change itself, a ring of cells round it kept the old cover. Pure, for tests.
    /// </summary>
    public static Rect DrawnFor(Rect changed, float metresPerTexel)
    {
        float grow = CellInset + CoverTapReach + Mathf.Max(0f, metresPerTexel);
        return Rect.MinMaxRect(changed.xMin - grow, changed.yMin - grow, changed.xMax + grow, changed.yMax + grow);
    }

    /// <summary>
    /// The field cells (first and last, both included) whose centres lie <see cref="CellInset"/> inside a drawn block of the cover map,
    /// its corner and size in metres from the field's corner, among <paramref name="fieldSize"/> a side of <paramref name="cell"/> metres.
    /// False when none does. Pure, for tests.
    /// </summary>
    public static bool CellsInside(Vector2 corner, Vector2 size, float cell, int fieldSize, out int x0, out int z0, out int x1, out int z1)
    {
        x0 = Mathf.Max(0, Mathf.CeilToInt((corner.x + CellInset) / cell - 0.5f));
        z0 = Mathf.Max(0, Mathf.CeilToInt((corner.y + CellInset) / cell - 0.5f));
        x1 = Mathf.Min(fieldSize - 1, Mathf.FloorToInt((corner.x + size.x - CellInset) / cell - 0.5f));
        z1 = Mathf.Min(fieldSize - 1, Mathf.FloorToInt((corner.y + size.y - CellInset) / cell - 0.5f));
        return x1 >= x0 && z1 >= z0;
    }

    // Draws a patch of the cover map again (grown: DrawnFor) and, with a field bound, works the ground's cover out again for the cells
    // well inside it, on a worker thread. rescan: what moves is looked for anew (SkyOcclusionMap.Redraw).
    private IEnumerator Redraw(Rect ground, bool rescan = false)
    {
        redrawing = true;
        int mine = generation;
        try
        {
            RectInt texels = cover.TexelsOver(DrawnFor(ground, cover.PinnedArea.width / Mathf.Max(1, cover.PinnedResolution)));
            RenderTexture drawn = cover.Redraw(texels, rescan);
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
            IEnumerator readBack = ReadBack(drawn, ok => read = ok);
            while (Advance(readBack))
            {
                yield return null;
            }

            Release(drawn);
            if (mine != generation || !read || field == null)
            {
                yield break;
            }

            Rect area = cover.PinnedArea;
            float texelsPerMetre = cover.PinnedResolution / area.width;
            float cell = fieldArea.width / fieldSize;
            // The block's corner in metres from the field's corner, and the cells whose centres lie far enough inside it.
            var corner = new Vector2(area.xMin + texels.x / texelsPerMetre - fieldArea.xMin, area.yMin + texels.y / texelsPerMetre - fieldArea.yMin);
            if (!CellsInside(corner, new Vector2(texels.width / texelsPerMetre, texels.height / texelsPerMetre), cell, fieldSize,
                    out int x0, out int z0, out int x1, out int z1))
            {
                yield break;
            }

            int width = x1 - x0 + 1, height = z1 - z0 + 1;
            float[] groundHeights = fieldGround;
            float[] waterLevels = fieldWater;
            float[] waterNear = fieldNear;
            float lowest = fieldLowest, highest = fieldHighest;
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
                        // Slice 1's height, water and its nearness (green, blue, alpha) as the whole field wrote them: only the cover's shade is redrawn.
                        int index = cz * size + cx;
                        pixels[z * width + x] = GroundLightField.GroundTexel(GroundLightField.Byte(factor), groundHeights[index], waterLevels,
                            waterNear, index, lowest, highest);
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
    // thread turns them into heights), else at once as floats. done(false): nothing came back, or a new scene (Clear) came first. The
    // buffers are the ones current when it was asked for: Clear swaps in empty ones meanwhile, and copying into those threw.
    private IEnumerator ReadBack(RenderTexture texture, Action<bool> done)
    {
        int mine = generation;
        int width = texture.width, height = texture.height;
        int count = width * height;
        if (heights.Length < count)
        {
            heights = new float[count];
        }

        float[] intoHeights = heights;
        if (SystemInfo.supportsAsyncGPUReadback)
        {
            if (halves.Length < count)
            {
                halves = new ushort[count];
            }

            ushort[] intoHalves = halves;
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(texture, 0);
            while (!request.done)
            {
                yield return null;
            }

            if (mine != generation)
            {
                done(false);
                yield break;
            }

            if (!request.hasError)
            {
                NativeArray<ushort> data = request.GetData<ushort>();
                if (data.Length >= count)
                {
                    NativeArray<ushort>.Copy(data, 0, intoHalves, 0, count);
                    readHalves = true;
                    done(true);
                    yield break;
                }
            }
        }

        if (texture == null || !texture.IsCreated() || mine != generation)
        {
            done(false);
            yield break;
        }

        var copy = new Texture2D(width, height, TextureFormat.RFloat, false, true);
        try
        {
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = texture;
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            RenderTexture.active = was;
            NativeArray<float>.Copy(copy.GetRawTextureData<float>(), 0, intoHeights, 0, count);
        }
        finally
        {
            Destroy(copy);
        }

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
        Shader.SetGlobalVector(HeightsId, Vector4.zero);
        Shader.SetGlobalVector(GroundScaleId, Vector4.zero);
        Shader.SetGlobalTexture(GroundHeightmapId, Texture2D.blackTexture);
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
