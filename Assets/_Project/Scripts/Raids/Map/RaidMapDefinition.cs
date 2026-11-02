using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One hand-made raid map and the rules for turning it into a Unity Terrain, edited in the Scene view on the raid
/// scene's RaidMap (its inspector's Map Painter):
/// <list type="bullet">
/// <item>Heights: sculpted straight on the scene's terrain with Unity's terrain tools; the terrain asset is the source.</item>
/// <item>Zones: painted layers (<see cref="RaidMapZone"/>): paths, forest density, clearings, and spawn zones whose
/// candidate points go to the Spawn Groups that name them.</item>
/// <item>Markers: draggable points for the player start, exits, camps and sites (<see cref="RaidMapMarker"/>).</item>
/// </list>
/// Bake turns zones and markers into paint, trees, rocks and spawn points. A raid playing this map as painted turns the
/// baked terrain (RaidMap) and never reads zones or markers; a raid with Layout Rules generates its zones on a copy of
/// this asset (UseGenerated). The older images (height, mask, marker PNGs) are kept only to import from once.
/// </summary>
[CreateAssetMenu(fileName = "RaidMap", menuName = "Safe Haven/Raids/Raid Map")]
public sealed class RaidMapDefinition : ScriptableObject
{
    [Serializable]
    public sealed class Scatter
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
        [Tooltip("Smallest and largest size, picked at random for each one placed.")]
        public Vector2 scale = new Vector2(0.85f, 1.2f);
        [Tooltip("The lowest raid tier it grows at, on a generated map. 0: any.")]
        [Range(0, Tiers.Max)] public int fromTier;
        [Tooltip("The highest raid tier it grows at, on a generated map. 0: any.")]
        [Range(0, Tiers.Max)] public int toTier;

        public bool AppearsAt(int tier) => tier <= 0 || ((fromTier <= 0 || tier >= fromTier) && (toTier <= 0 || tier <= toTier));
    }

    [Serializable]
    public sealed class SiteRule
    {
        public RaidSiteKind kind;
        [Tooltip("Spawn points made around each marker of this kind; more than 1 makes a cluster (a camp, a resource patch).")]
        [Min(1)] public int pointsPerSite = 1;
        [Tooltip("How far from the marker the extra points of a cluster are placed, in metres.")]
        [Min(0f)] public float spread = 3f;
    }

    [Header("Painted layout")]
    [Tooltip("Cells across each painted zone. Higher is finer but heavier; changing it resamples what is painted.")]
    [SerializeField] private int zoneResolution = 256;
    [Tooltip("The painted layers. Paint them in the Scene view (RaidMap › Map Painter); settings can be edited here.")]
    [SerializeField] private List<RaidMapZone> zones = new List<RaidMapZone>();
    [Tooltip("Points of interest. Drag them in the Scene view (RaidMap › Map Painter › Markers).")]
    [SerializeField] private List<RaidMapMarker> markers = new List<RaidMapMarker>();

    [Header("Legacy images (import only)")]
    [Tooltip("Greyscale, black = lowest, white = Height metres. Read only by 'Import from Images'.")]
    [SerializeField] private Texture2D heightMap;
    [Tooltip("Red = paths, green = forest density, blue = clearings. Read only by 'Import from Images'.")]
    [SerializeField] private Texture2D maskMap;
    [Tooltip("Coloured dots on black (RaidMapMarkers.Legend). Read only by 'Import from Images'.")]
    [SerializeField] private Texture2D markerMap;

    [Header("Size")]
    [Tooltip("Width and length of the square map in metres.")]
    [SerializeField, Min(16f)] private float size = 200f;
    [Tooltip("Metres between the darkest and the brightest pixel of the height map.")]
    [SerializeField, Min(1f)] private float height = 30f;
    [Tooltip("Terrain heightmap resolution (2^n + 1).")]
    [SerializeField] private int heightmapResolution = 513;
    [Tooltip("Resolution of the terrain's paint (splat) map.")]
    [SerializeField] private int paintResolution = 512;
    [Tooltip("Import from Images: blur passes over the imported heights, to hide pixel steps.")]
    [SerializeField, Range(0, 4)] private int smoothing = 1;

    [Header("Paint")]
    [SerializeField] private Material terrainMaterial;
    [Tooltip("Forest floor; also the layer TerrainMoss grows on.")]
    [SerializeField] private TerrainLayer forestLayer;
    [Tooltip("Paths and clearings.")]
    [SerializeField] private TerrainLayer pathLayer;
    [Tooltip("Cliffs: slopes steeper than Cliff Slope.")]
    [SerializeField] private TerrainLayer cliffLayer;
    [Tooltip("Slopes (degrees) where rock starts to show, and where it covers everything.")]
    [SerializeField] private Vector2 cliffSlope = new Vector2(30f, 42f);

    [Header("Growth")]
    [Tooltip("Grass, bushes and plants on each painted layer, grown on every map when a raid loads.")]
    [SerializeField] private List<TerrainGrowth.Ground> growth = new List<TerrainGrowth.Ground>();
    [Tooltip("The plants grown in patches over that ground: ferns, berries, flowers, glowing mushrooms. Empty: the standard forest floor.")]
    [SerializeField] private List<TerrainGrowth.Plant> plants = new List<TerrainGrowth.Plant>();

    [Header("Trees")]
    [SerializeField] private List<Scatter> trees = new List<Scatter>();
    [Tooltip("Metres between trees where the forest is densest (green = white).")]
    [SerializeField, Min(1f)] private float treeSpacing = 3.4f;
    [Tooltip("No trees on slopes steeper than this (degrees).")]
    [SerializeField, Range(0f, 60f)] private float maxTreeSlope = 32f;
    [Tooltip("Trees keep this far (metres) from every marker, so sites are never inside a trunk.")]
    [SerializeField, Min(0f)] private float clearAroundSites = 5f;

    [Header("Rocks and logs")]
    [SerializeField] private List<Scatter> rocks = new List<Scatter>();
    [SerializeField, Min(1f)] private float rockSpacing = 7f;
    [Tooltip("Chance of a rock in each spot at the foot of a cliff.")]
    [SerializeField, Range(0f, 1f)] private float rocksAtCliffs = 0.5f;
    [Tooltip("Chance of a rock or log in each spot of open forest.")]
    [SerializeField, Range(0f, 1f)] private float rocksInForest = 0.05f;

    [Header("Sites")]
    [SerializeField] private List<SiteRule> siteRules = new List<SiteRule>();
    [Tooltip("Seed for scattering trees and rocks. Change it for a different forest from the same images.")]
    [SerializeField] private int scatterSeed = 1;

    public int ZoneResolution => zoneResolution;
    public List<RaidMapZone> Zones => zones;
    public List<RaidMapMarker> Markers => markers;
    /// <summary>True once the map has painted zones (made in the painter or imported from the images).</summary>
    public bool HasPaintedLayout => zones.Count > 0;
    public bool HasLegacyImages => heightMap != null || maskMap != null || markerMap != null;

    public Texture2D HeightMap => heightMap;
    public Texture2D MaskMap => maskMap;
    public Texture2D MarkerMap => markerMap;
    public float Size => size;
    public float Height => height;
    public int HeightmapResolution => heightmapResolution;
    public int PaintResolution => paintResolution;
    public int Smoothing => smoothing;
    public Material TerrainMaterial => terrainMaterial;
    public TerrainLayer ForestLayer => forestLayer;
    public TerrainLayer PathLayer => pathLayer;
    public TerrainLayer CliffLayer => cliffLayer;
    public Vector2 CliffSlope => cliffSlope;
    public IReadOnlyList<TerrainGrowth.Ground> Growth => growth;
    public IReadOnlyList<TerrainGrowth.Plant> Plants => plants.Count > 0 ? plants : TerrainGrowth.DefaultPlants;
    public IReadOnlyList<Scatter> Trees => trees;
    public float TreeSpacing => treeSpacing;
    public float MaxTreeSlope => maxTreeSlope;
    public float ClearAroundSites => clearAroundSites;
    public IReadOnlyList<Scatter> Rocks => rocks;
    public float RockSpacing => rockSpacing;
    public float RocksAtCliffs => rocksAtCliffs;
    public float RocksInForest => rocksInForest;
    public int ScatterSeed => scatterSeed;

    /// <summary>The rule for a kind of site; one point exactly on the marker when there is none.</summary>
    public SiteRule RuleFor(RaidSiteKind kind)
    {
        foreach (SiteRule rule in siteRules)
        {
            if (rule != null && rule.kind == kind)
            {
                return rule;
            }
        }

        return new SiteRule { kind = kind, pointsPerSite = 1, spread = 0f };
    }

    /// <summary>The strongest paint of all zones with this role at (u, v); 0 when there are none.</summary>
    public float Strength(RaidZoneRole role, float u, float v)
    {
        float strongest = 0f;
        foreach (RaidMapZone zone in zones)
        {
            if (zone != null && zone.role == role)
            {
                strongest = Mathf.Max(strongest, zone.Sample(u, v));
            }
        }

        return strongest;
    }

    /// <summary>The spawn zone with this name, or null.</summary>
    public RaidMapZone SpawnZone(string zoneName)
    {
        foreach (RaidMapZone zone in zones)
        {
            if (zone != null && zone.IsSpawnZone && zone.name == zoneName)
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>The markers as sites, sorted like RaidMapMarkers.Decode (kind, then south to north, then west to east).</summary>
    public List<RaidSite> Sites()
    {
        var sites = new List<RaidSite>(markers.Count);
        foreach (RaidMapMarker marker in markers)
        {
            if (marker != null && marker.kind != RaidSiteKind.None)
            {
                sites.Add(marker.ToSite());
            }
        }

        RaidMapMarkers.Sort(sites);
        return sites;
    }

    /// <summary>Adds the Path, Forest and Clearing zones the terrain paint needs, if missing, and sizes every zone.</summary>
    public void EnsureZones()
    {
        AddIfMissing(RaidZoneRole.Path, "Path", new Color(0.85f, 0.6f, 0.3f));
        AddIfMissing(RaidZoneRole.Forest, "Forest", new Color(0.2f, 0.85f, 0.25f));
        AddIfMissing(RaidZoneRole.Clearing, "Clearing", new Color(0.35f, 0.6f, 1f));
        foreach (RaidMapZone zone in zones)
        {
            zone?.EnsureResolution(zoneResolution);
        }
    }

    private void AddIfMissing(RaidZoneRole role, string zoneName, Color color)
    {
        if (!zones.Exists(zone => zone != null && zone.role == role))
        {
            zones.Add(new RaidMapZone { name = zoneName, role = role, color = color });
        }
    }

    /// <summary>
    /// Makes this copy of a map paint from generated zones, in the forest of the given mood and raid tier. Only ever for a copy
    /// made at runtime (Instantiate): the asset itself never changes while playing.
    /// </summary>
    public void UseGenerated(int resolution, byte[] path, byte[] forest, byte[] clearing, RaidMapMood mood, int seed, int tier = 0)
    {
        zoneResolution = resolution;
        zones.Clear();
        AddGenerated("Path", RaidZoneRole.Path, path, resolution);
        AddGenerated("Forest", RaidZoneRole.Forest, forest, resolution);
        AddGenerated("Clearing", RaidZoneRole.Clearing, clearing, resolution);
        markers.Clear();
        scatterSeed = seed;
        treeSpacing *= mood.treeSpacing;
        rocksInForest = Mathf.Clamp01(rocksInForest * mood.rocks);
        for (int i = 0; i < trees.Count && i < mood.treeWeights.Count; i++)
        {
            if (trees[i] != null)
            {
                trees[i].weight = mood.treeWeights[i];
            }
        }

        // After the mood, which can favour a tree this tier has none of.
        foreach (Scatter scatter in trees)
        {
            if (scatter != null && !scatter.AppearsAt(tier))
            {
                scatter.weight = 0f;
            }
        }

        foreach (Scatter scatter in rocks)
        {
            if (scatter != null && !scatter.AppearsAt(tier))
            {
                scatter.weight = 0f;
            }
        }
    }

    private void AddGenerated(string zoneName, RaidZoneRole role, byte[] weights, int resolution)
    {
        var zone = new RaidMapZone { name = zoneName, role = role };
        zone.SetWeights(weights, resolution);
        zones.Add(zone);
    }

    /// <summary>For the image import: replaces the zone resolution (the zones are resampled by EnsureZones).</summary>
    public void SetZoneResolution(int cells) => zoneResolution = Mathf.Clamp(cells, 16, 2048);

    private void OnValidate()
    {
        heightmapResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(heightmapResolution - 1, 32, 4096)) + 1;
        paintResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(paintResolution, 16, 2048));
        cliffSlope.y = Mathf.Max(cliffSlope.x + 1f, cliffSlope.y);
        zoneResolution = Mathf.Clamp(zoneResolution, 16, 2048);
    }
}
