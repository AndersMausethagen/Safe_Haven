using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>Gentle relief for a flat terrain (Safe Haven > World > Terrain > Add Gentle Relief): soft rises and
/// dips (noise, a few tens of centimetres) so sunlight and shadow vary across the ground instead of lying flat. Only
/// level ground changes: sculpted or carved areas (stream beds) keep their shape, and the ground stays level within a
/// clearance of anything standing on it (buildings, rocks, trees, water), so nothing floats or sinks. Optionally keeps
/// the Heart Tree's ward (where the player builds) level. Undoable; rebake the NavMesh afterwards.</summary>
public static class TerrainRelief
{
    [MenuItem("Safe Haven/World/Terrain/Add Gentle Relief...")]
    private static void Menu() => ScriptableWizard.DisplayWizard<ReliefWizard>("Add Gentle Relief", "Add Relief");

    private sealed class ReliefWizard : ScriptableWizard
    {
        [Tooltip("Highest rise / deepest dip (metres).")]
        [Range(0.05f, 1.5f)] public float amplitude = 0.3f;
        [Tooltip("Size of the rises (metres).")]
        [Min(2f)] public float scale = 14f;
        [Tooltip("The ground stays level within this distance of objects and water (metres).")]
        [Min(0f)] public float clearance = 1.5f;
        [Tooltip("Keep the ground inside the Heart Tree's ward level, where buildings are placed.")]
        public bool keepWardLevel = true;
        public int seed = 1;
        [Tooltip("Rebake the scene's NavMesh Surfaces so monsters and NPCs walk on the new ground.")]
        public bool rebakeNavMesh = true;

        private void OnWizardCreate()
        {
            foreach (Terrain terrain in Terrain.activeTerrains.Where(t => t.terrainData != null))
            {
                int changed = Apply(terrain, amplitude, scale, clearance, keepWardLevel, seed);
                Debug.Log($"Relief: {changed} height samples of {terrain.name} changed.");
            }

            if (rebakeNavMesh)
            {
                RebakeNavMeshes();
            }
        }
    }

    /// <summary>Adds the relief to the terrain (undoable); returns the number of height samples changed.</summary>
    public static int Apply(Terrain terrain, float amplitude, float scale, float clearance, bool keepWardLevel, int seed)
    {
        TerrainData data = terrain.terrainData;
        float[,] heights = Compute(terrain, amplitude, scale, clearance, keepWardLevel, seed, out int changed);
        Undo.RegisterCompleteObjectUndo(data, "Add Gentle Relief");
        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        return changed;
    }

    /// <summary>The terrain's heights with the relief added (the terrain itself is not changed).</summary>
    public static float[,] Compute(Terrain terrain, float amplitude, float scale, float clearance, bool keepWardLevel, int seed,
        out int changed)
    {
        TerrainData data = terrain.terrainData;
        int n = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, n, n);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float cell = size.x / (n - 1);

        // Level ground is the most common height; anything else was sculpted or carved and is left alone.
        float level = heights.Cast<float>().GroupBy(h => Mathf.Round(h * size.y * 50f)).OrderByDescending(g => g.Count()).First().Key / 50f / size.y;
        bool[,] blocked = Blocked(terrain, heights, level, keepWardLevel);
        float[,] distance = DistanceToBlocked(blocked, cell);

        changed = 0;
        float offset = seed * 37.13f;
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                float fade = clearance > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance[z, x] / clearance)) : (blocked[z, x] ? 0f : 1f);
                if (fade <= 0f)
                {
                    continue;
                }

                float wx = origin.x + x * cell, wz = origin.z + z * cell;
                float broad = Mathf.PerlinNoise(wx / scale + offset, wz / scale - offset) * 2f - 1f;
                float fine = Mathf.PerlinNoise(wx / (scale * 0.35f) - offset, wz / (scale * 0.35f) + offset) * 2f - 1f;
                float rise = (broad * 0.75f + fine * 0.25f) * amplitude * fade;
                heights[z, x] = Mathf.Clamp01(heights[z, x] + rise / size.y);
                changed++;
            }
        }

        return heights;
    }

    /// <summary>Samples that must stay as they are: not level ground (carved, sculpted), under or beside objects
    /// standing on the ground, under water, or (optionally) inside the Heart Tree's ward.</summary>
    private static bool[,] Blocked(Terrain terrain, float[,] heights, float level, bool keepWardLevel)
    {
        TerrainData data = terrain.terrainData;
        int n = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float cell = size.x / (n - 1);
        var blocked = new bool[n, n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                blocked[z, x] = Mathf.Abs(heights[z, x] - level) * size.y > 0.02f;
            }
        }

        // Footprints of objects that touch the ground (renderers and colliders), and water.
        float groundY = origin.y + level * size.y;
        IEnumerable<Bounds> footprints = Object.FindObjectsByType<Renderer>()
            .Where(r => r.enabled && !(r is ParticleSystemRenderer) && r.gameObject.scene == terrain.gameObject.scene)
            .Select(r => r.bounds)
            .Concat(Object.FindObjectsByType<Collider>().Where(c => c.enabled && !(c is TerrainCollider) && !c.isTrigger).Select(c => c.bounds))
            .Where(b => b.min.y < groundY + 0.6f && b.max.y > groundY - 2f && b.size.x < size.x * 0.5f && b.size.z < size.z * 0.5f);
        foreach (Bounds b in footprints)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min.x - origin.x) / cell), 0, n - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((b.max.x - origin.x) / cell), 0, n - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((b.min.z - origin.z) / cell), 0, n - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((b.max.z - origin.z) / cell), 0, n - 1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    blocked[z, x] = true;
                }
            }
        }

        if (keepWardLevel && HeartTree.Current != null)
        {
            Vector3 centre = HeartTree.Current.transform.position;
            float radius = HeartTree.Current.WardRadius + 2f;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (new Vector2(origin.x + x * cell - centre.x, origin.z + z * cell - centre.z).sqrMagnitude < radius * radius)
                    {
                        blocked[z, x] = true;
                    }
                }
            }
        }

        return blocked;
    }

    /// <summary>Distance (metres) from each sample to the nearest blocked one (two-pass chamfer transform).</summary>
    private static float[,] DistanceToBlocked(bool[,] blocked, float cell)
    {
        int n = blocked.GetLength(0);
        var d = new float[n, n];
        const float big = 1e6f;
        float diagonal = cell * 1.41421f;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
                d[z, x] = blocked[z, x] ? 0f : big;

        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                if (x > 0) d[z, x] = Mathf.Min(d[z, x], d[z, x - 1] + cell);
                if (z > 0) d[z, x] = Mathf.Min(d[z, x], d[z - 1, x] + cell);
                if (x > 0 && z > 0) d[z, x] = Mathf.Min(d[z, x], d[z - 1, x - 1] + diagonal);
                if (x < n - 1 && z > 0) d[z, x] = Mathf.Min(d[z, x], d[z - 1, x + 1] + diagonal);
            }

        for (int z = n - 1; z >= 0; z--)
            for (int x = n - 1; x >= 0; x--)
            {
                if (x < n - 1) d[z, x] = Mathf.Min(d[z, x], d[z, x + 1] + cell);
                if (z < n - 1) d[z, x] = Mathf.Min(d[z, x], d[z + 1, x] + cell);
                if (x < n - 1 && z < n - 1) d[z, x] = Mathf.Min(d[z, x], d[z + 1, x + 1] + diagonal);
                if (x > 0 && z < n - 1) d[z, x] = Mathf.Min(d[z, x], d[z + 1, x - 1] + diagonal);
            }

        return d;
    }

    /// <summary>Rebakes every NavMeshSurface in the open scenes the way their Bake button does (the new data is saved
    /// as an asset next to the scene; baking finishes over the next editor frames).</summary>
    public static int RebakeNavMeshes()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>();
        if (surfaces.Length > 0)
        {
            NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
            Debug.Log($"Relief: rebaking {surfaces.Length} NavMesh surface(s). Save the scene when the bake finishes.");
        }

        return surfaces.Length;
    }
}
