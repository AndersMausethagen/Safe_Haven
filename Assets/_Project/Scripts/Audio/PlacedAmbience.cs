using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sound that tells the player where something is, from where it is: wind over the nearest ridge above them, creaking
/// from a stand of dead trees, a howl from the way the creatures live, a song over still water at night (the Nøkk). Each
/// is played now and then from that place, so the world can be read by ear as well as from the map. Nothing near: nothing
/// is played. Hours are a curve, as AmbientOneShots'. Cliffs (scree) and any trees (creaks) too, since 2026-11-18 (the biome beds).
/// <para>Where it belongs: one per kind of sound, in a scene (set up for the Haven world by BuildLivingWorld).</para>
/// </summary>
public sealed class PlacedAmbience : MonoBehaviour
{
    public enum Source
    {
        [Tooltip("The highest ground round the player, when it stands well above them.")]
        Ridge,
        [Tooltip("The nearest dead tree (a painted terrain tree whose name has Dead in it).")]
        DeadTrees,
        [Tooltip("The way to the nearest creature spawn zone, at a distance (howls).")]
        SpawnZone,
        [Tooltip("The nearest still water (a pond, the lake).")]
        StillWater,
        [Tooltip("The nearest steep face of ground (a cliff, a valley wall, a ravine's side): scree trickling, a stone falling.")]
        Cliff,
        [Tooltip("The nearest tree of any kind (a painted terrain tree): a trunk creaking, drips from the canopy.")]
        Trees,
    }

    [SerializeField] private Source source;
    [SerializeField] private SoundDefinition sound;
    [SerializeField] private Vector2 every = new Vector2(20f, 45f);
    [Tooltip("How likely at each hour (0-24 across, 0-1 up). Empty: always.")]
    [SerializeField] private AnimationCurve byHour = new AnimationCurve();
    [Tooltip("Metres it looks for its source within.")]
    [SerializeField, Min(5f)] private float searchRadius = 40f;

    private const float CellSize = 32f;
    // A face this steep (degrees) is a cliff; looked for this many metres apart round the player.
    private const float CliffSlope = 45f;
    private const float CliffStep = 4f;
    private readonly Dictionary<Vector2Int, List<Vector3>> deadTrees = new Dictionary<Vector2Int, List<Vector3>>();
    // Generated raid maps paint their trees during loading, after Start: the index is made when first needed, and again if the trees change.
    private int indexedTrees = -1;
    private MobSpawnZone[] zones;
    private float due;

    public void Configure(Source from, SoundDefinition played, Vector2 seconds, AnimationCurve hours, float radius)
    {
        source = from;
        sound = played;
        every = seconds;
        byHour = hours ?? new AnimationCurve();
        searchRadius = radius;
    }

    private void Start()
    {
        due = Time.time + Random.Range(every.x, every.y);
        if (source == Source.SpawnZone)
        {
            zones = FindObjectsByType<MobSpawnZone>();
        }
    }

    private void Update()
    {
        if (sound == null || Time.time < due)
        {
            return;
        }

        due = Time.time + Random.Range(every.x, every.y);
        Player player = Player.Current;
        if (player == null || (byHour != null && byHour.length > 0 && Random.value > byHour.Evaluate(AtmosphereConditions.Hour)))
        {
            return;
        }

        if (Find(player.transform.position, out Vector3 from))
        {
            AudioManager.Play(sound, from);
        }
    }

    private bool Find(Vector3 at, out Vector3 from)
    {
        switch (source)
        {
            case Source.Ridge:
                return HighGround(at, out from);
            case Source.DeadTrees:
            case Source.Trees:
                return NearestTree(at, out from);
            case Source.SpawnZone:
                return TowardsCreatures(at, out from);
            case Source.Cliff:
                return NearestCliff(at, out from);
            default:
                return StillWater(at, out from);
        }
    }

    private bool HighGround(Vector3 at, out Vector3 from)
    {
        from = default;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            return false;
        }

        float best = at.y + 5f;
        bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            for (float distance = 12f; distance <= searchRadius; distance += 12f)
            {
                var point = new Vector3(at.x + Mathf.Cos(angle) * distance, 0f, at.z + Mathf.Sin(angle) * distance);
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                if (point.y > best)
                {
                    best = point.y;
                    from = point + Vector3.up * 2f;
                    found = true;
                }
            }
        }

        return found;
    }

    private void IndexTrees()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        TerrainData data = terrain.terrainData;
        if (data.treeInstanceCount == indexedTrees)
        {
            return;
        }

        indexedTrees = data.treeInstanceCount;
        deadTrees.Clear();
        TreePrototype[] prototypes = data.treePrototypes;
        var dead = new bool[prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            dead[i] = prototypes[i].prefab != null && (source == Source.Trees || prototypes[i].prefab.name.Contains("Dead"));
        }

        Vector3 origin = terrain.transform.position, size = data.size;
        foreach (TreeInstance tree in data.treeInstances)
        {
            if (tree.prototypeIndex < 0 || tree.prototypeIndex >= dead.Length || !dead[tree.prototypeIndex])
            {
                continue;
            }

            var point = new Vector3(origin.x + tree.position.x * size.x, origin.y + tree.position.y * size.y + 4f, origin.z + tree.position.z * size.z);
            var cell = new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.z / CellSize));
            if (!deadTrees.TryGetValue(cell, out List<Vector3> list))
            {
                deadTrees[cell] = list = new List<Vector3>();
            }

            list.Add(point);
        }
    }

    private bool NearestTree(Vector3 at, out Vector3 from)
    {
        from = default;
        IndexTrees();
        float best = searchRadius * searchRadius;
        int reach = Mathf.CeilToInt(searchRadius / CellSize);
        var centre = new Vector2Int(Mathf.FloorToInt(at.x / CellSize), Mathf.FloorToInt(at.z / CellSize));
        bool found = false;
        for (int x = -reach; x <= reach; x++)
        {
            for (int y = -reach; y <= reach; y++)
            {
                if (!deadTrees.TryGetValue(centre + new Vector2Int(x, y), out List<Vector3> list))
                {
                    continue;
                }

                foreach (Vector3 tree in list)
                {
                    float squared = (tree - at).sqrMagnitude;
                    if (squared < best && squared > 16f)
                    {
                        best = squared;
                        from = tree;
                        found = true;
                    }
                }
            }
        }

        return found;
    }

    // A howl comes from the way the creatures are, from far enough off to sound like it.
    private bool TowardsCreatures(Vector3 at, out Vector3 from)
    {
        from = default;
        float best = searchRadius * searchRadius;
        bool found = false;
        if (zones == null)
        {
            return false;
        }

        foreach (MobSpawnZone zone in zones)
        {
            if (zone == null)
            {
                continue;
            }

            float squared = (zone.transform.position - at).sqrMagnitude;
            if (squared < best && squared > 25f * 25f)
            {
                best = squared;
                Vector3 way = (zone.transform.position - at).normalized;
                from = at + way * 30f + Vector3.up * 2f;
                found = true;
            }
        }

        return found;
    }

    // The nearest steep face round the player, near its foot, where scree runs down: looked for along 16 ways out to the search radius.
    private bool NearestCliff(Vector3 at, out Vector3 from)
    {
        from = default;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position, size = data.size;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            for (float distance = 6f; distance <= searchRadius; distance += CliffStep)
            {
                float x = at.x + Mathf.Cos(angle) * distance, z = at.z + Mathf.Sin(angle) * distance;
                float u = (x - origin.x) / size.x, v = (z - origin.z) / size.z;
                if (u < 0f || v < 0f || u > 1f || v > 1f)
                {
                    break;
                }

                if (data.GetSteepness(u, v) >= CliffSlope)
                {
                    if (distance < best)
                    {
                        best = distance;
                        var point = new Vector3(x, 0f, z);
                        point.y = terrain.SampleHeight(point) + origin.y + 1f;
                        from = point;
                        found = true;
                    }

                    break;
                }
            }
        }

        return found;
    }

    private bool StillWater(Vector3 at, out Vector3 from)
    {
        from = default;
        float best = searchRadius * searchRadius;
        bool found = false;
        Terrain terrain = Terrain.activeTerrain;
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || !water.IsStill || water.IsFrozen)
            {
                continue;
            }

            Vector3 point = water.NearestPoint(at);
            if (Buried(terrain, point) && !OpenWaterNear(water, terrain, at, out point))
            {
                continue;
            }

            float squared = (point - at).sqrMagnitude;
            if (squared < best)
            {
                best = squared;
                from = point + Vector3.up * 0.5f;
                found = true;
            }
        }

        return found;
    }

    // A sheet laid under the whole map (the Moss Marsh's fen) lies under the ground in most places, so its nearest point is often below
    // the listener's feet: the sound comes from the nearest place where its water shows instead, along 16 directions.
    private bool OpenWaterNear(WaterSurface water, Terrain terrain, Vector3 at, out Vector3 open)
    {
        open = default;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            for (float distance = CliffStep; distance <= searchRadius; distance += CliffStep)
            {
                Vector3 point = water.NearestPoint(new Vector3(at.x + Mathf.Cos(angle) * distance, at.y, at.z + Mathf.Sin(angle) * distance));
                if (Buried(terrain, point))
                {
                    continue;
                }

                float squared = (point - at).sqrMagnitude;
                if (squared < best)
                {
                    best = squared;
                    open = point;
                    found = true;
                }

                break;
            }
        }

        return found;
    }

    /// <summary>Whether the ground stands over a water surface's point (the water doesn't show there). Pure, for tests.</summary>
    public static bool Buried(Terrain terrain, Vector3 point) =>
        terrain != null && terrain.SampleHeight(point) + terrain.transform.position.y > point.y + 0.05f;
}
