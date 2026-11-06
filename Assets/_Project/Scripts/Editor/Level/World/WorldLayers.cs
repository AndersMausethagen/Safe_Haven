using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>One terrain's heights inside the rectangle a build works on, in world metres: as read, the ground before
/// any feature (hand edits taken in), and the working values the features shape in order.</summary>
public sealed class HeightLayer
{
    public readonly Terrain Terrain;
    public readonly TerrainGrid Grid;
    public readonly RectInt Rect;
    public readonly float[] Original;
    public readonly float[] Base;
    public readonly float[] Values;
    public readonly bool[] Touched;

    public HeightLayer(Terrain terrain, RectInt rect)
    {
        Terrain = terrain;
        Grid = TerrainGrid.Heights(terrain);
        Rect = rect;
        int count = rect.width * rect.height;
        Original = new float[count];
        Base = new float[count];
        Values = new float[count];
        Touched = new bool[count];
    }

    public int IndexOf(int x, int y) => (y - Rect.y) * Rect.width + (x - Rect.x);

    public float Get(int x, int y) => Values[IndexOf(x, y)];

    public void Set(int x, int y, float metres)
    {
        int i = IndexOf(x, y);
        Values[i] = metres;
        Touched[i] = true;
    }

    /// <summary>Bilinear height (world metres) of the working values at a world point; false outside the rectangle.
    /// The last row and column count as inside: a point on a terrain's far edge (where two terrains meet) must read the
    /// working values, not fall back to the built terrain, or a feature ending there feeds on its own last build and
    /// digs deeper every rebuild.</summary>
    public bool TrySample(Vector2 world, out float height)
    {
        Vector2 g = Grid.GridPosition(world);
        if (Rect.width < 2 || Rect.height < 2 || g.x < Rect.xMin || g.y < Rect.yMin || g.x > Rect.xMax - 1 || g.y > Rect.yMax - 1)
        {
            height = 0f;
            return false;
        }

        int x0 = Mathf.Min(Mathf.FloorToInt(g.x), Rect.xMax - 2), y0 = Mathf.Min(Mathf.FloorToInt(g.y), Rect.yMax - 2);
        float fx = g.x - x0, fy = g.y - y0;
        float a = Get(x0, y0), b = Get(x0 + 1, y0), c = Get(x0, y0 + 1), d = Get(x0 + 1, y0 + 1);
        height = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        return true;
    }
}

/// <summary>One terrain's paint (layer weights per alphamap cell) inside the rectangle a build works on.</summary>
public sealed class PaintLayer
{
    public readonly Terrain Terrain;
    public readonly TerrainGrid Grid;
    public readonly RectInt Rect;
    public readonly int Layers;
    public readonly float[] Values; // cell * Layers + layer
    public readonly bool[] Touched;
    private readonly TerrainLayer[] assets;

    public PaintLayer(Terrain terrain, RectInt rect)
    {
        Terrain = terrain;
        Grid = TerrainGrid.Alphas(terrain);
        Rect = rect;
        assets = terrain.terrainData.terrainLayers;
        Layers = assets.Length;
        Values = new float[rect.width * rect.height * Layers];
        Touched = new bool[rect.width * rect.height];
    }

    public int IndexOf(int x, int y) => (y - Rect.y) * Rect.width + (x - Rect.x);

    /// <summary>The terrain's index of a layer (-1 when the terrain does not have it).</summary>
    public int LayerIndex(TerrainLayer layer)
    {
        if (layer == null)
        {
            return -1;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] == layer)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Paints <paramref name="layer"/> over the cell with <paramref name="weight"/> (0-1); the other layers
    /// give way in proportion, so the weights keep summing to one.</summary>
    public void Paint(int x, int y, int layer, float weight)
    {
        if (layer < 0 || weight <= 0f)
        {
            return;
        }

        weight = Mathf.Clamp01(weight);
        int cell = IndexOf(x, y);
        int offset = cell * Layers;
        for (int l = 0; l < Layers; l++)
        {
            Values[offset + l] = Values[offset + l] * (1f - weight) + (l == layer ? weight : 0f);
        }

        Touched[cell] = true;
    }
}

/// <summary>Detail (grass) cells inside the rectangle a build works on: those features want cleared (paths, rivers),
/// and how much of a plant features want grown (biome areas). Clearing wins over growing.</summary>
public sealed class ClearLayer
{
    public readonly Terrain Terrain;
    public readonly TerrainGrid Grid;
    public readonly RectInt Rect;
    public readonly bool[] Clear;
    public readonly int Prototypes;
    private readonly DetailPrototype[] prototypes;
    private int[] grow; // cell * Prototypes + prototype: the count wanted there, -1 where no feature grows it

    public ClearLayer(Terrain terrain, RectInt rect)
    {
        Terrain = terrain;
        Grid = TerrainGrid.Details(terrain);
        Rect = rect;
        Clear = new bool[rect.width * rect.height];
        prototypes = terrain.terrainData.detailPrototypes;
        Prototypes = prototypes.Length;
    }

    public int IndexOf(int x, int y) => (y - Rect.y) * Rect.width + (x - Rect.x);

    /// <summary>The terrain's detail index of a plant prefab (-1 when the terrain does not have it).</summary>
    public int PrototypeIndex(GameObject prefab)
    {
        for (int i = 0; prefab != null && i < prototypes.Length; i++)
        {
            if (prototypes[i].prototype == prefab)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Asks for at least <paramref name="count"/> of a plant in a cell (the most asked for wins).</summary>
    public void Grow(int x, int y, int prototype, int count)
    {
        if (prototype < 0 || prototype >= Prototypes || count <= 0)
        {
            return;
        }

        if (grow == null)
        {
            grow = new int[Clear.Length * Prototypes];
            System.Array.Fill(grow, -1);
        }

        int i = IndexOf(x, y) * Prototypes + prototype;
        grow[i] = Mathf.Max(grow[i], count);
    }

    /// <summary>What features want of a plant in a cell (by its index in this rectangle): -1 when they have no say.</summary>
    public int Wanted(int cell, int prototype) => grow == null ? -1 : grow[cell * Prototypes + prototype];

    /// <summary>
    /// What one detail cell becomes in a build, and what the stash keeps for it. <paramref name="kept"/>,
    /// <paramref name="baseValue"/> and <paramref name="last"/> come from the stash; <paramref name="current"/> is on the
    /// terrain now. Hand changes since the last build win: the cell is then left as it is (last = HandKept) until no
    /// feature wants it any more. Otherwise a clear leaves nothing, a grow at least its count over the ground, and a cell
    /// back at its ground is forgotten (release).
    /// </summary>
    public static int Decide(bool kept, int baseValue, int last, int current, bool clear, int wanted,
                             out int keepBase, out int keepLast, out bool release)
    {
        bool featureHere = clear || wanted >= 0;
        keepBase = kept ? baseValue : current;
        keepLast = 0;
        release = false;
        if (kept && (last == WorldTerrainStash.HandKept || current != last))
        {
            // Changed by hand since the last build (or kept from before): it stays as it is.
            keepBase = current;
            keepLast = WorldTerrainStash.HandKept;
            release = !featureHere;
            return current;
        }

        int value = clear ? 0 : wanted >= 0 ? Mathf.Max(keepBase, wanted) : keepBase;
        keepLast = value;
        release = kept && value == keepBase;
        return value;
    }
}

/// <summary>Ground heights across every terrain of a build: the working (shaped so far) heights inside the build
/// rectangles, the terrain itself elsewhere.</summary>
public sealed class WorldSampler
{
    private readonly List<HeightLayer> layers;

    public WorldSampler(List<HeightLayer> layers)
    {
        this.layers = layers;
    }

    public float Height(Vector2 xz)
    {
        foreach (HeightLayer layer in layers)
        {
            if (layer.TrySample(xz, out float height))
            {
                return height;
            }
        }

        return WorldGround.TryGround(xz, out Vector3 point, out _) ? point.y : 0f;
    }
}

/// <summary>The actual terrain under a point after a build (height and normal).</summary>
public static class WorldGround
{
    /// <summary>The scene whose terrains count while a build runs (the World Builder sets it). Scenes can lie on the
    /// same coordinates (the Haven and the prologue do); without this a prologue build placed its trees and stones on the
    /// Haven's ground when both were open. Outside a build the active scene's terrain is preferred.</summary>
    public static UnityEngine.SceneManagement.Scene Scope;

    public static Terrain TerrainAt(Vector2 xz)
    {
        UnityEngine.SceneManagement.Scene preferred = Scope.IsValid() ? Scope : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Terrain other = null;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null)
            {
                continue;
            }

            Vector3 local = new Vector3(xz.x, 0f, xz.y) - terrain.transform.position;
            if (local.x >= 0f && local.z >= 0f && local.x <= data.size.x && local.z <= data.size.z)
            {
                if (terrain.gameObject.scene == preferred)
                {
                    return terrain;
                }

                other ??= terrain;
            }
        }

        // Another scene's terrain only when not building (a build never reaches into another scene).
        return Scope.IsValid() ? null : other;
    }

    public static bool TryGround(Vector2 xz, out Vector3 point, out Vector3 normal)
    {
        Terrain terrain = TerrainAt(xz);
        if (terrain == null)
        {
            point = new Vector3(xz.x, 0f, xz.y);
            normal = Vector3.up;
            return false;
        }

        TerrainData data = terrain.terrainData;
        Vector3 world = new Vector3(xz.x, 0f, xz.y);
        Vector3 local = world - terrain.transform.position;
        point = new Vector3(xz.x, terrain.transform.position.y + terrain.SampleHeight(world), xz.y);
        normal = data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
        return true;
    }

    /// <summary>Steepness in degrees (0 flat, 90 a wall).</summary>
    public static float Slope(Vector3 normal) => Vector3.Angle(normal, Vector3.up);
}

/// <summary>Places one palette object the way the Scatter Brush does: random size and turn, tilted partly with the slope,
/// set down by the bottom of its meshes and sunk a little.</summary>
public static class WorldScatter
{
    public static GameObject Place(ScatterPalette.Entry entry, Vector3 ground, Vector3 normal, Transform parent,
                                   System.Random random, float extraSink = 0f)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, parent);
        float yaw = entry.randomYaw ? (float)random.NextDouble() * 360f : 0f;
        Quaternion tilt = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), entry.alignToSlope);
        float scale = Mathf.Lerp(entry.scale.x, entry.scale.y, (float)random.NextDouble());
        instance.transform.SetPositionAndRotation(ground, tilt * Quaternion.Euler(0f, yaw, 0f));
        instance.transform.localScale = entry.prefab.transform.localScale * scale;
        instance.transform.position = ground + Vector3.up * (GroundPlacement.BottomOffset(instance) - entry.sink - extraSink);
        return instance;
    }

    /// <summary>Points kept apart: a coarse grid of what was placed, for spacing checks.</summary>
    public sealed class Spacing
    {
        private readonly Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();
        private const float Cell = 4f;

        /// <summary>Adds a point with its keep-clear radius (stored in y of the list entry).</summary>
        public void Add(Vector2 xz, float radius)
        {
            var key = new Vector2Int(Mathf.FloorToInt(xz.x / Cell), Mathf.FloorToInt(xz.y / Cell));
            if (!cells.TryGetValue(key, out List<Vector3> list))
            {
                cells[key] = list = new List<Vector3>();
            }

            list.Add(new Vector3(xz.x, radius, xz.y));
        }

        /// <summary>Whether a point with this radius keeps clear of everything placed (radii add up).</summary>
        public bool IsFree(Vector2 xz, float radius)
        {
            int reach = Mathf.CeilToInt((radius + 4f) / Cell);
            int cx = Mathf.FloorToInt(xz.x / Cell), cy = Mathf.FloorToInt(xz.y / Cell);
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!cells.TryGetValue(new Vector2Int(x, y), out List<Vector3> list))
                    {
                        continue;
                    }

                    foreach (Vector3 p in list)
                    {
                        float min = radius + p.y;
                        if ((new Vector2(p.x, p.z) - xz).sqrMagnitude < min * min)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
