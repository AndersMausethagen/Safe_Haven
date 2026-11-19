using UnityEngine;

/// <summary>
/// A place that changes how the camera frames the player while they are in it: a box (its Size, turned with the object)
/// with a soft edge, in which the view comes closer (or goes further out) and tilts, easing in over Blend Seconds as the
/// player walks in and back out as they leave. For a moment that should feel near: the morning fire in the prologue
/// brings the party close. Prefabs/Camera/Camera Zone; scale the box with Size in the Inspector (the gizmo shows it).
/// The player's own zoom and camera style are kept; only the top-down view tilts.
/// </summary>
[AddComponentMenu("Safe Haven/Camera/Camera Zone")]
public sealed class CameraZone : MonoBehaviour
{
    [Tooltip("The box the zone fills, in metres (centred on this object and turned with it).")]
    [SerializeField] private Vector3 size = new Vector3(20f, 10f, 20f);
    [Tooltip("Metres inside the edge over which it fades in: walking in, the camera changes gradually.")]
    [SerializeField, Min(0f)] private float softEdge = 4f;
    [Tooltip("The camera's distance as a share of the usual: 0.7 comes 30% closer, 1.2 goes out.")]
    [SerializeField, Range(0.3f, 2f)] private float distance = 0.75f;
    [Tooltip("Degrees added to the camera's pitch: negative looks flatter across the ground (faces show), positive more from above.")]
    [SerializeField, Range(-30f, 30f)] private float tilt = -8f;
    [Tooltip("Seconds the change takes as the player walks in or out.")]
    [SerializeField, Min(0.05f)] private float blendSeconds = 2.5f;

    private float weight;

    public Vector3 Size
    {
        get => size;
        set => size = new Vector3(Mathf.Max(0.1f, value.x), Mathf.Max(0.1f, value.y), Mathf.Max(0.1f, value.z));
    }

    public float Distance
    {
        get => distance;
        set => distance = Mathf.Clamp(value, 0.3f, 2f);
    }

    public float Tilt
    {
        get => tilt;
        set => tilt = Mathf.Clamp(value, -30f, 30f);
    }

    public float SoftEdge
    {
        get => softEdge;
        set => softEdge = Mathf.Max(0f, value);
    }

    public float BlendSeconds
    {
        get => blendSeconds;
        set => blendSeconds = Mathf.Max(0.05f, value);
    }

    /// <summary>How far the zone has the camera now, 0..1.</summary>
    public float Weight => weight;

    /// <summary>How deep in the zone a point is: 1 inside the soft edge, 0 outside, between on the edge.</summary>
    public float Inside(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 half = size * 0.5f;
        // Metres to the nearest face, inside (positive) or out.
        float depth = Mathf.Min(half.x - Mathf.Abs(local.x), Mathf.Min(half.y - Mathf.Abs(local.y), half.z - Mathf.Abs(local.z)));
        if (depth <= 0f)
        {
            return 0f;
        }

        return softEdge <= 0f ? 1f : Mathf.Clamp01(depth / softEdge);
    }

    private void Update()
    {
        Player player = Player.Current;
        float wanted = player != null ? Inside(player.transform.position) : 0f;
        weight = Mathf.MoveTowards(weight, wanted, Time.unscaledDeltaTime / blendSeconds);
        float eased = weight * weight * (3f - 2f * weight);
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null)
        {
            view.SetCloseness(this, Mathf.Lerp(1f, distance, eased));
            view.SetTilt(this, tilt * eased);
        }
    }

    private void OnDisable()
    {
        weight = 0f;
        PlayerCameraController view = PlayerCameraController.Current;
        if (view != null)
        {
            view.SetCloseness(this, 1f);
            view.SetTilt(this, 0f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(Vector3.zero, size);
        if (softEdge > 0f)
        {
            Vector3 inner = new Vector3(Mathf.Max(0f, size.x - softEdge * 2f), Mathf.Max(0f, size.y - softEdge * 2f), Mathf.Max(0f, size.z - softEdge * 2f));
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, inner);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.08f);
        Gizmos.DrawCube(Vector3.zero, size);
    }
}
