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
        [Tooltip("Where it belongs: slope, forest density, wet or dry ground, edges, closeness to trunks, groves and room.")]
        public RaidScatterHabitat habitat = new RaidScatterHabitat();

        public bool AppearsAt(int tier) => tier <= 0 || ((fromTier <= 0 || tier >= fromTier) && (toTier <= 0 || tier <= toTier));
    }

    /// <summary>
    /// Stones in and beside running and still water (Docs/WorldLookPlan_2026-11-17.md B6): on the shallow bed, standing a little out of
    /// the water so the water shader rings them with foam where the concept art has it, and in clumps along the banks. An empty list
    /// places none.
    /// </summary>
    [Serializable]
    public sealed class ShoreRule
    {
        [Tooltip("The stones (lake stones, small mossy rocks, pebbles), picked by weight. Empty: no stones by the water. Those with a " +
                 "capsule collider keep it, so creatures path round them.")]
        public List<Scatter> stones = new List<Scatter>();
        [Tooltip("Metres between the spots on a river's or pond's bed where a stone could lie.")]
        [Min(0.5f)] public float bedSpacing = 2f;
        [Tooltip("Chance of a stone in each spot of shallow bed; fewer where it is deeper.")]
        [Range(0f, 1f)] public float bedChance = 0.8f;
        [Tooltip("Stones lie on the bed only where the water is shallower than this, in metres.")]
        [Min(0.05f)] public float deepest = 0.8f;
        [Tooltip("How far a bed stone stands out of the water, least and most, in metres. Foam rings the stones that break the surface.")]
        public Vector2 proud = new Vector2(0.1f, 0.3f);
        [Tooltip("Metres between the spots along a bank where a stone could lie.")]
        [Min(0.5f)] public float bankSpacing = 2.2f;
        [Tooltip("How far up the bank from the water's edge stones lie, in metres.")]
        [Min(0f)] public float bankReach = 1.5f;
        [Tooltip("Chance of a stone in each bank spot inside a clump.")]
        [Range(0f, 1f)] public float bankChance = 0.6f;
        [Tooltip("Metres across a clump of bank stones; the bank between clumps stays bare.")]
        [Min(1f)] public float clumpSize = 6f;
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
    [Tooltip("Optional fourth layer: needles and leaves under the densest canopy, in patches, never on wet ground. Empty: three layers.")]
    [SerializeField] private TerrainLayer litterLayer;
    [Tooltip("How much of the deep forest's floor the litter covers (0 none, 1 all of it).")]
    [SerializeField, Range(0f, 1f)] private float litter = 0.6f;
    [Tooltip("Metres over which a path's soil fades into the forest floor (each of two blur passes). Wider is a softer, more gradual " +
             "edge (the user, 2026-11-17, on the Moss Marsh: \"make the transition between materials more gradient\").")]
    [SerializeField, Range(0.3f, 4f)] private float pathFeather = 0.9f;
    [Tooltip("How much noise frays the path's edge into tufts of the forest floor: 1 the old ragged edge, 0 a clean fade.")]
    [SerializeField, Range(0f, 1f)] private float pathFray = 1f;
    [Tooltip("How much of a path's soft border is painted with the Litter Layer, a step between the path and the floor (gravel, then " +
             "leaves and dirt, then meadow or moss) instead of the two meeting in a line. 0: none (as before).")]
    [SerializeField, Range(0f, 1f)] private float edgeBlend;
    [Tooltip("Ground at the water's edge: the Path Layer (mud, gravel) at the waterline fading up through the Litter Layer into the floor, " +
             "by how wet the ground is (the fen's and the ponds' wetness), so a shore is a band and not moss meeting water. 0: none (as before).")]
    [SerializeField, Range(0f, 1f)] private float shoreMud;
    [Tooltip("How much soil shows on open ground away from the trails, where the forest is thin (times how open it is). 0.35 the " +
             "Woods' gravelly openings; low keeps an open valley a meadow, soil only on trails and trodden clearings.")]
    [SerializeField, Range(0f, 1f)] private float openSoil = 0.35f;
    [Tooltip("The layer TerrainMoss's skin and fronds grow on, on a generated map. Empty: the Forest Layer. The Litter Layer keeps moss " +
             "to the densest trees. Must be one of this map's layers.")]
    [SerializeField] private TerrainLayer mossLayer;
    [Tooltip("No moss skin at all on a generated map (TerrainMoss switched off for the run).")]
    [SerializeField] private bool noMoss;

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
    [Tooltip("How much trees gather into copses with meadow between (and a few lone trees in it), 0 to 1. 0: spread as the forest's " +
             "density says. Moves trees rather than adding them; undergrowth follows at half strength.")]
    [SerializeField, Range(0f, 1f)] private float treeClumping;
    [Tooltip("Metres across a copse and the meadow beside it.")]
    [SerializeField, Min(2f)] private float clumpSize = 16f;

    [Header("Rocks and logs")]
    [SerializeField] private List<Scatter> rocks = new List<Scatter>();
    [SerializeField, Min(1f)] private float rockSpacing = 7f;
    [Tooltip("Chance of a rock in each spot at the foot of a cliff.")]
    [SerializeField, Range(0f, 1f)] private float rocksAtCliffs = 0.5f;
    [Tooltip("Chance of a rock or log in each spot of open forest.")]
    [SerializeField, Range(0f, 1f)] private float rocksInForest = 0.05f;
    [Tooltip("Chance of a rock in each spot of open ground (thin forest, off trails and clearings). 0: the open stays bare of them.")]
    [SerializeField, Range(0f, 1f)] private float rocksInOpen;
    [Tooltip("Chance that a rock with room of 1 m or more becomes a pile: 2-5 Pile Stones round it and a plant at its foot. Pile " +
             "stones cut no hole in the navmesh.")]
    [SerializeField, Range(0f, 1f)] private float pileChance;
    [Tooltip("The small stones a pile is made of (Mossy Rock Small, Grey Rock, Pebbles), by weight, each at its own size (0.3-0.6 suits).")]
    [SerializeField] private List<Scatter> pileStones = new List<Scatter>();

    [Header("Water's edge")]
    [Tooltip("Stones on the shallow beds and along the banks of rivers and ponds, on a generated map.")]
    [SerializeField] private ShoreRule shore = new ShoreRule();

    [Header("Undergrowth")]
    [Tooltip("Saplings, shrubs and forest-floor clutter, placed after the trees in the gaps and edges their habitat favours.")]
    [SerializeField] private List<Scatter> undergrowth = new List<Scatter>();
    [SerializeField, Min(0.5f)] private float undergrowthSpacing = 2.5f;
    [Tooltip("Chance of undergrowth in each spot that suits something.")]
    [SerializeField, Range(0f, 1f)] private float undergrowthChance = 0.5f;

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

    /// <summary>A copy for a map this many times wider, at the same detail per metre. Made at runtime; the asset never changes.</summary>
    public RaidMapDefinition ScaledBy(float width)
    {
        RaidMapDefinition copy = Instantiate(this);
        copy.size = size * width;
        return copy;
    }
    public float Height => height;
    public int HeightmapResolution => heightmapResolution;
    public int PaintResolution => paintResolution;
    public int Smoothing => smoothing;
    public Material TerrainMaterial => terrainMaterial;
    public TerrainLayer ForestLayer => forestLayer;
    public TerrainLayer PathLayer => pathLayer;
    public TerrainLayer CliffLayer => cliffLayer;
    public Vector2 CliffSlope => cliffSlope;
    public TerrainLayer LitterLayer => litterLayer;
    public float Litter => litter;
    public float PathFeather => pathFeather;
    public float PathFray => pathFray;
    public float EdgeBlend => edgeBlend;
    public float ShoreMud => shoreMud;
    public float OpenSoil => openSoil;
    public bool NoMoss => noMoss;

    /// <summary>
    /// The layer the moss grows on: Moss Layer when it is one of this map's layers, otherwise the Forest Layer (as before Moss Layer
    /// existed). Null with <see cref="NoMoss"/>.
    /// </summary>
    public TerrainLayer MossLayer
    {
        get
        {
            if (noMoss)
            {
                return null;
            }

            return mossLayer != null && Array.IndexOf(Layers, mossLayer) >= 0 ? mossLayer : forestLayer;
        }
    }

    /// <summary>The terrain layers in paint order: forest, path, cliff, then litter when the map has one.</summary>
    public TerrainLayer[] Layers => litterLayer != null
        ? new[] { forestLayer, pathLayer, cliffLayer, litterLayer }
        : new[] { forestLayer, pathLayer, cliffLayer };
    public IReadOnlyList<TerrainGrowth.Ground> Growth => growth;
    public IReadOnlyList<TerrainGrowth.Plant> Plants => plants.Count > 0 ? plants : TerrainGrowth.DefaultPlants;
    public IReadOnlyList<Scatter> Trees => trees;
    public float TreeSpacing => treeSpacing;
    public float MaxTreeSlope => maxTreeSlope;
    public float ClearAroundSites => clearAroundSites;
    public float TreeClumping => treeClumping;
    public float ClumpSize => clumpSize;
    public IReadOnlyList<Scatter> Rocks => rocks;
    public float RockSpacing => rockSpacing;
    public float RocksAtCliffs => rocksAtCliffs;
    public float RocksInForest => rocksInForest;
    public float RocksInOpen => rocksInOpen;
    public float PileChance => pileChance;
    public IReadOnlyList<Scatter> PileStones => pileStones;
    public ShoreRule Shore => shore ?? (shore = new ShoreRule());
    public IReadOnlyList<Scatter> Undergrowth => undergrowth;
    public float UndergrowthSpacing => undergrowthSpacing;
    public float UndergrowthChance => undergrowthChance;
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
        rocksInOpen = Mathf.Clamp01(rocksInOpen * mood.rocks);
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

        foreach (Scatter scatter in undergrowth)
        {
            if (scatter != null && !scatter.AppearsAt(tier))
            {
                scatter.weight = 0f;
            }
        }

        foreach (List<Scatter> stones in new[] { pileStones, Shore.stones })
        {
            foreach (Scatter scatter in stones)
            {
                if (scatter != null && !scatter.AppearsAt(tier))
                {
                    scatter.weight = 0f;
                }
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
