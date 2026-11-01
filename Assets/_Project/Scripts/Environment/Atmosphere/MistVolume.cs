using System.Collections.Generic;
using UnityEngine;

/// <summary>A movable cloud volume using the same flowing mist as terrain, without requiring terrain underneath.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Environment/Mist Volume")]
public sealed class MistVolume : MonoBehaviour
{
    private static readonly List<MistVolume> active = new List<MistVolume>();

    [Header("Volume")]
    [Tooltip("Local width, height and depth in metres. The pivot is the bottom centre: place it on the ground or water surface and the mist rises Size.y above it. Rotation and scale are supported.")]
    [SerializeField] private Vector3 size = new Vector3(20f, 3f, 14f);
    [Tooltip("Extinction per metre inside the clouds. Unlike light shafts, mist intentionally obscures what is behind it.")]
    [SerializeField, Range(0f, 0.5f)] private float density = 0.22f;
    [Tooltip("Horizontal edge fade in local metres.")]
    [SerializeField, Min(0.1f)] private float edgeFade = 2f;
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
    public float EffectiveDensity => Mathf.Max(0f, density) * (followsWeather ? AtmosphereConditions.WeatherMist : 1f);
    public float EdgeFade => Mathf.Clamp(edgeFade, 0.01f, Mathf.Min(Size.x, Size.z) * 0.5f);
    public Color Tint => tint.linear;
    public float SkyLight => skyLight;
    public float CloudSize => Mathf.Max(1f, cloudSize);
    public float Coverage => coverage;
    public float Erosion => erosion;
    public float FlowStretch => Mathf.Max(1f, flowStretch);
    public Vector2 Wind => wind * Mathf.Max(0f, driftSpeed);
    public Vector4 Waves => new Vector4(Mathf.Max(0f, waveAmplitude), Mathf.Max(1f, waveLength),
        Mathf.Max(0f, waveSpeed) * Mathf.Max(0f, driftSpeed), 0f);
    public float WeatherWindInfluence => weatherWindInfluence;

    private void OnEnable()
    {
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
