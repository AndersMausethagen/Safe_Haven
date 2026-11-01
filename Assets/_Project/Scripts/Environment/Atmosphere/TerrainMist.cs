using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent drifting, terrain-following cloud layer rendered by AtmosphereFeature.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Terrain))]
public sealed class TerrainMist : MonoBehaviour
{
    private static readonly List<TerrainMist> active = new List<TerrainMist>();
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
    [Header("Weather and time of day")]
    [Tooltip("Minimum mist amount regardless of weather or time. Zero allows mist only during misty weather or golden hour. One keeps the original always-on layer.")]
    [SerializeField, Range(0f, 1f)] private float baseAmount = 0f;
    [Tooltip("How much the weather preset's independent Mist amount contributes.")]
    [SerializeField, Range(0f, 1f)] private float weatherAmount = 1f;
    [Tooltip("Mist amount at dawn/dusk golden hour, reduced by cloud cover. Independent of global fog.")]
    [SerializeField, Range(0f, 1f)] private float goldenHourAmount = 1f;
    [Tooltip("0 keeps authored drift speed. 1 follows the blended weather's Wind multiplier (storm moves faster).")]
    [SerializeField, Range(0f, 1f)] private float weatherWindInfluence = 1f;

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
    public float EdgeFade => Mathf.Max(0.1f, edgeFade);
    public float WeatherWindInfluence => weatherWindInfluence;
    public float EffectiveDensity => density * EvaluateAmount(
        AtmosphereConditions.WeatherOnlyAtmosphere ? 0f : baseAmount, weatherAmount,
        AtmosphereConditions.WeatherOnlyAtmosphere ? 0f : goldenHourAmount,
        AtmosphereConditions.WeatherMist, AtmosphereConditions.GoldenHour);

    public static float EvaluateAmount(float baseline, float weatherContribution, float goldenContribution,
        float weatherMist, float goldenHour) =>
        Mathf.Clamp01(Mathf.Max(baseline, Mathf.Max(weatherContribution * weatherMist, goldenContribution * goldenHour)));

    private void OnEnable()
    {
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
