using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent drifting, terrain-following cloud layer rendered by AtmosphereFeature.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Terrain))]
public sealed class TerrainMist : MonoBehaviour
{
    private static readonly List<TerrainMist> active = new List<TerrainMist>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();
    private Terrain terrain;

    [Tooltip("Extinction per metre inside the mist clouds. Zero disables this layer without changing the scene fog.")]
    [SerializeField, Range(0f, 0.3f)] private float density = 0.2f;
    [Tooltip("Cloud scattering colour, independent of global fog colour.")]
    [SerializeField] private Color tint = new Color(0.8f, 0.87f, 0.94f);
    [Tooltip("Soft sky fill inside the clouds. Lower at night; higher makes the mist readable under dense trees.")]
    [SerializeField, Range(0f, 1f)] private float skyLight = 0.2f;
    [Tooltip("Height of the bottom of the cloud layer above the terrain, in metres.")]
    [SerializeField, Min(0f)] private float bottom = 0.03f;
    [Tooltip("Maximum layer depth in metres. The thick core stays near the ground and thins rapidly upward.")]
    [SerializeField, Min(0.25f)] private float thickness = 3f;
    [Tooltip("Horizontal size of the drifting clouds, in metres.")]
    [SerializeField, Min(1f)] private float cloudSize = 18f;
    [Tooltip("Share covered by clouds. Lower values leave larger completely clear gaps.")]
    [SerializeField, Range(0.05f, 1f)] private float coverage = 0.78f;
    [Tooltip("Breaks up cloud edges with smaller billows.")]
    [SerializeField, Range(0f, 1f)] private float erosion = 0.35f;
    [Tooltip("Stretches clouds along their drift into terrain-hugging veils. 1 keeps round cloud pockets.")]
    [SerializeField, Range(1f, 6f)] private float flowStretch = 3.5f;
    [Tooltip("World X/Z drift speed in metres per second before Weather Wind Influence.")]
    [SerializeField] private Vector2 wind = new Vector2(0.7f, 0.25f);
    [Tooltip("Multiplies drift and internal billowing. Raise this for more visible movement without changing direction.")]
    [SerializeField, Min(0f)] private float driftSpeed = 3.5f;
    [Header("Rolling waves and pooling")]
    [Tooltip("Height of calm rolling crests in metres. Zero disables the additional wave motion.")]
    [SerializeField, Min(0f)] private float waveAmplitude = 0.8f;
    [Tooltip("Distance between broad mist waves in metres.")]
    [SerializeField, Min(1f)] private float waveLength = 12f;
    [Tooltip("Wave travel in metres per second before weather influence.")]
    [SerializeField, Min(0f)] private float waveSpeed = 0.9f;
    [Tooltip("Extra mist in hollows relative to surrounding terrain within 12 metres. Zero disables pooling.")]
    [SerializeField, Range(0f, 4f)] private float lowlandBoost = 1.5f;
    [Tooltip("Depression depth in metres that receives the full lowland boost.")]
    [SerializeField, Min(0.1f)] private float poolingDepth = 2f;
    [Tooltip("Extra density over actual Water Surface footprints, above the water rather than the lake bed.")]
    [SerializeField, Range(0f, 4f)] private float waterBoost = 2f;
    [Tooltip("Soft fade at the terrain tile's perimeter, in metres.")]
    [SerializeField, Min(0.1f)] private float edgeFade = 5f;
    [Header("Clumps")]
    [Tooltip("0: the drifting sheet. 1: a blanket of mist on the ground, a thin veil swelling into soft billows, creeping slowly round what stands in its way " +
             "(the user's reference, 2026-11-22: \"more like clumpy clouds instead of a thin, varying shimmer\"; \"it should slowly creep " +
             "along the terrain\"). In between, a blend.")]
    [SerializeField, Range(0f, 1f)] private float clumps = 1f;
    [Tooltip("Metres across a billow.")]
    [SerializeField, Min(0.5f)] private float clumpSize = 4.5f;
    [Tooltip("How high the tallest billows rise, in metres (about waist high on a person; the veil between them lies over the ground plants: the user, 2026-11-23, \"so most of the ground plants gets mostly covered\").")]
    [SerializeField, Min(0.1f)] private float clumpHeight = 1.5f;
    [Tooltip("How much darker a billow is at its foot than at its top (its own shade; 0: lit evenly).")]
    [SerializeField, Range(0f, 0.9f)] private float underShade = 0.5f;
    [Tooltip("How fast the billows creep, as a share of the drift (Wind × Drift Speed): slowly along the ground, not sailing like clouds. " +
             "The mist's wake field flows at the same speed, round trees and rocks.")]
    [SerializeField, Range(0f, 0.5f)] private float creep = 0.1f;
    [Tooltip("How thick a billow is, times Density: billows are white and nearly opaque where the sheet is a veil.")]
    [SerializeField, Min(0f)] private float billowDensity = 12f;
    [Tooltip("The thin veil that lies between the billows and joins them over most of the ground, as a share of a billow's thickness " +
             "(the user, 2026-11-22: \"less clumpy and more connected through thin veils. most of the ground should be filled with mist\"). " +
             "0: billows with clear ground between.")]
    [SerializeField, Range(0f, 1f)] private float veil = 0.45f;
    [Header("Weather and time of day")]
    [Tooltip("Minimum mist amount regardless of weather or time. Zero allows mist only during misty weather or golden hour. One keeps the original always-on layer.")]
    [SerializeField, Range(0f, 1f)] private float baseAmount = 0f;
    [Tooltip("How much the weather preset's independent Mist amount contributes.")]
    [SerializeField, Range(0f, 1f)] private float weatherAmount = 1f;
    [Tooltip("Mist amount at dawn/dusk golden hour, reduced by cloud cover. Independent of global fog.")]
    [SerializeField, Range(0f, 1f)] private float goldenHourAmount = 1f;
    [Tooltip("Mist amount in the morning: it pools in the hollows before dawn and burns off by mid-morning (AtmosphereConditions.Morning).")]
    [SerializeField, Range(0f, 1f)] private float morningAmount = 0f;
    [Tooltip("0 keeps authored drift speed. 1 follows the blended weather's Wind multiplier (storm moves faster).")]
    [SerializeField, Range(0f, 1f)] private float weatherWindInfluence = 1f;
    [Header("Where it always lies")]
    [Tooltip("A map of the terrain, north up: white where mist always lies whatever the weather (misty woods, a bog), black where only the " +
             "weather's, the golden hour's and the morning's mist comes. Empty: no such mist. The Haven world's generator bakes one; it can " +
             "be painted over in any picture editor.")]
    [SerializeField] private Texture2D regionMap;
    [Tooltip("How much mist lies where the map is white, 0 to 1 of this layer's density.")]
    [SerializeField, Range(0f, 1f)] private float regionAmount = 0.6f;

    public static IReadOnlyList<TerrainMist> Active => active;
    public Terrain Terrain => terrain;
    public float Density => density;
    public Color Tint => tint.linear;
    public float SkyLight => skyLight;
    public float Bottom => bottom;
    public float Thickness => Mathf.Max(0.25f, thickness);
    public float CloudSize => Mathf.Max(1f, cloudSize);
    public float Coverage => coverage;
    public float Erosion => erosion;
    public float FlowStretch => Mathf.Max(1f, flowStretch);
    public Vector2 Wind => wind * Mathf.Max(0f, driftSpeed);
    public Vector4 Waves => new Vector4(Mathf.Max(0f, waveAmplitude), Mathf.Max(1f, waveLength), Mathf.Max(0f, waveSpeed), 0f);
    public Vector4 Pooling => new Vector4(lowlandBoost, Mathf.Max(0.1f, poolingDepth), waterBoost, 0f);
    /// <summary>The clumps' shape for the march: amount, 1 / billow size, height (m), under-shade.</summary>
    public Vector4 Clumps => new Vector4(clumps, 1f / Mathf.Max(0.5f, clumpSize), Mathf.Max(0.1f, clumpHeight), underShade);
    /// <summary>The billows' creep (share of the drift), thickness (times Density) and the veil between them (share), for the march.</summary>
    public Vector4 Billows => new Vector4(creep, billowDensity, veil, 0f);
    /// <summary>The billows' creep, as a share of <see cref="Wind"/>.</summary>
    public float Creep => creep;

    public float EdgeFade => Mathf.Max(0.1f, edgeFade);
    public float WeatherWindInfluence => weatherWindInfluence;

    // A map made at runtime in place of the authored one (HavenLand: thick over forgotten land, thin over remembered), with each
    // region's mist colour in its G, B and A channels when tinted.
    private Texture2D runtimeRegionMap;
    private bool runtimeTinted;

    /// <summary>The painted map of where mist always lies (HavenLand reads it to make its own).</summary>
    public Texture2D AuthoredRegionMap => regionMap;
    public Texture2D RegionMap => runtimeRegionMap != null ? runtimeRegionMap : regionMap;
    /// <summary>True when the map in use carries each place's mist colour in G, B and A (a multiplier over the tint).</summary>
    public bool RegionTinted => runtimeRegionMap != null && runtimeTinted;

    /// <summary>Uses <paramref name="map"/> in place of the authored map until called with null (HavenLand).</summary>
    public void UseRegionMap(Texture2D map, bool tinted)
    {
        runtimeRegionMap = map;
        runtimeTinted = tinted;
    }

    /// <summary>Extinction per metre where the region map is white (0 without a map). Whatever the weather, and whether or not
    /// the scene's atmosphere otherwise comes from the weather alone (the Haven's does): the map is where mist always lies.</summary>
    public float RegionDensity => RegionMap != null ? density * regionAmount : 0f;
    public float EffectiveDensity => density * Mathf.Max(EvaluateAmount(
        AtmosphereConditions.WeatherOnlyAtmosphere ? 0f : baseAmount, weatherAmount,
        AtmosphereConditions.WeatherOnlyAtmosphere ? 0f : goldenHourAmount,
        AtmosphereConditions.WeatherMist, AtmosphereConditions.GoldenHour),
        AtmosphereConditions.WeatherOnlyAtmosphere ? 0f : morningAmount * AtmosphereConditions.Morning);

    /// <summary>Sets how much morning mist this layer gathers (BuildLivingWorld).</summary>
    public void SetMorningAmount(float amount) => morningAmount = Mathf.Clamp01(amount);

    public static float EvaluateAmount(float baseline, float weatherContribution, float goldenContribution,
        float weatherMist, float goldenHour) =>
        Mathf.Clamp01(Mathf.Max(baseline, Mathf.Max(weatherContribution * weatherMist, goldenContribution * goldenHour)));

    private void OnEnable()
    {
        MistWakes.Ensure();
        terrain = GetComponent<Terrain>();
        if (!active.Contains(this))
        {
            active.Add(this);
        }
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public float DistanceSquared(Vector3 point)
    {
        Terrain terrain = Terrain;
        if (terrain == null || terrain.terrainData == null)
        {
            return float.PositiveInfinity;
        }

        Vector3 min = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float dx = Mathf.Max(Mathf.Max(min.x - point.x, 0f), point.x - min.x - size.x);
        float dz = Mathf.Max(Mathf.Max(min.z - point.z, 0f), point.z - min.z - size.z);
        return dx * dx + dz * dz;
    }
}
