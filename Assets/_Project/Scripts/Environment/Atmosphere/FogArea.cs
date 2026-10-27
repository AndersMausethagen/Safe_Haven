using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pocket of extra fog placed in the world: a misty hollow, a marsh, the breath of a waterfall. An upright ellipsoid
/// (Radius wide, Height tall) whose fog fades out over its edge, seen from anywhere like the rest of the volumetric fog
/// and drifting with it. Add one with GameObject › Safe Haven › World › Fog Area; in a raid, keep it under Map Content so it
/// turns with the map. AtmosphereFeature draws the nearest few (MaxAreas) to the camera.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FogArea : MonoBehaviour
{
    private static readonly List<FogArea> active = new List<FogArea>();

    [Tooltip("Extra fog density at the heart of the area (extinction per metre), on top of the scene's fog.")]
    [SerializeField, Range(0f, 0.5f)] private float density = 0.06f;
    [Tooltip("Half its width, in metres.")]
    [SerializeField, Min(0.5f)] private float radius = 15f;
    [Tooltip("Half its height, in metres. Low and wide lies on the ground like a blanket.")]
    [SerializeField, Min(0.25f)] private float height = 3f;
    [Tooltip("Share of the area over which it fades out at the edge.")]
    [SerializeField, Range(0.05f, 1f)] private float softness = 0.6f;
    [Tooltip("Follows the weather's fog (thicker in mist) like the scene's fog does.")]
    [SerializeField] private bool followsWeather = true;

    public static IReadOnlyList<FogArea> Active => active;

    public float Density => density;
    public float Radius => radius;
    public float Height => height;
    public float Softness => softness;
    public bool FollowsWeather => followsWeather;

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.75f, 0.85f, 0.95f, 0.18f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(radius, height, radius));
        Gizmos.DrawSphere(Vector3.zero, 1f);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 0.85f, 0.95f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(radius, height, radius));
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
        Gizmos.DrawWireSphere(Vector3.zero, 1f - softness);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
