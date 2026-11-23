using System.Collections.Generic;
using UnityEngine;

/// <summary>A movable cloud volume using the same flowing mist as terrain, without requiring terrain underneath.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Environment/Mist Volume")]
public sealed class MistVolume : MonoBehaviour
{
    private static readonly List<MistVolume> active = new List<MistVolume>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [Header("Volume")]
    [Tooltip("Local width, height and depth in metres. The pivot is the bottom centre: place it on the ground or water surface and the mist rises Size.y above it. Rotation and scale are supported.")]
    [SerializeField] private Vector3 size = new Vector3(20f, 3f, 14f);
    [Tooltip("Extinction per metre inside the clouds. Unlike light shafts, mist intentionally obscures what is behind it.")]
    [SerializeField, Range(0f, 0.5f)] private float density = 0.22f;
    [Tooltip("Horizontal edge fade in local metres.")]
    [SerializeField, Min(0.1f)] private float edgeFade = 2f;
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
    [SerializeField] private Color tint = new Color(0.8f, 0.87f, 0.94f);
    [SerializeField, Range(0f, 1f)] private float skyLight = 0.2f;

    [Header("Clouds and flow")]
    [SerializeField, Min(1f)] private float cloudSize = 12f;
    [SerializeField, Range(0.05f, 1f)] private float coverage = 0.8f;
    [SerializeField, Range(0f, 1f)] private float erosion = 0.3f;
    [SerializeField, Range(1f, 6f)] private float flowStretch = 3.5f;
    [Tooltip("Drift in the volume's local X/Z plane, metres per second before Drift Speed.")]
    [SerializeField] private Vector2 wind = new Vector2(0.7f, 0.25f);
    [Tooltip("Multiplier for drift, rolling crests and internal billowing. Zero freezes the volume.")]
    [SerializeField, Min(0f)] private float driftSpeed = 3.5f;
    [SerializeField, Min(0f)] private float waveAmplitude = 0.8f;
    [SerializeField, Min(1f)] private float waveLength = 12f;
    [SerializeField, Min(0f)] private float waveSpeed = 0.3f;

    [Header("Weather")]
    [Tooltip("Off keeps this placed mist visible even in Clear weather. On follows the weather's Mist amount.")]
    [SerializeField] private bool followsWeather;
    [Tooltip("Zero keeps authored movement; one follows weather wind, even when mist itself is always visible.")]
    [SerializeField, Range(0f, 1f)] private float weatherWindInfluence;

    public static IReadOnlyList<MistVolume> Active => active;
    public Vector3 Size => Vector3.Max(size, Vector3.one * 0.1f);
    /// <summary>How much of its density shows right now, 0..1: for mist that rolls in and thins out (ArrivalMist). Not saved.</summary>
    public float DensityScale { get; set; } = 1f;
    public float EffectiveDensity => Mathf.Max(0f, density) * Mathf.Clamp01(DensityScale) * (followsWeather ? AtmosphereConditions.WeatherMist : 1f);
    public float EdgeFade => Mathf.Clamp(edgeFade, 0.01f, Mathf.Min(Size.x, Size.z) * 0.5f);
    public Color Tint => tint.linear;
    public float SkyLight => skyLight;
    public float CloudSize => Mathf.Max(1f, cloudSize);
    public float Coverage => coverage;
    public float Erosion => erosion;
    public float FlowStretch => Mathf.Max(1f, flowStretch);
    public Vector2 Wind => wind * Mathf.Max(0f, driftSpeed);
    /// <summary>The clumps' shape for the march: amount, 1 / billow size, height (m), under-shade.</summary>
    public Vector4 Clumps => new Vector4(clumps, 1f / Mathf.Max(0.5f, clumpSize), Mathf.Max(0.1f, clumpHeight), underShade);
    /// <summary>The billows' creep (share of the drift), thickness (times Density) and the veil between them (share), for the march.</summary>
    public Vector4 Billows => new Vector4(creep, billowDensity, veil, 0f);
    /// <summary>The billows' creep, as a share of <see cref="Wind"/>.</summary>
    public float Creep => creep;
    public Vector4 Waves => new Vector4(Mathf.Max(0f, waveAmplitude), Mathf.Max(1f, waveLength),
        Mathf.Max(0f, waveSpeed) * Mathf.Max(0f, driftSpeed), 0f);
    public float WeatherWindInfluence => weatherWindInfluence;

    private void OnEnable()
    {
        MistWakes.Ensure();
        if (!active.Contains(this))
            active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    public float DistanceSquared(Vector3 point)
    {
        Vector3 size = Size;
        Vector3 min = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
        Vector3 max = new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 nearest = Vector3.Min(max, Vector3.Max(min, local));
        return (point - transform.TransformPoint(nearest)).sqrMagnitude;
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireCube(new Vector3(0f, Size.y * 0.5f, 0f), Size);
        Gizmos.matrix = previous;
    }
}
