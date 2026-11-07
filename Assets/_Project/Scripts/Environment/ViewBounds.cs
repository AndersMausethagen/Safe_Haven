using UnityEngine;

/// <summary>
/// Keeps the top-down camera from looking past the edge of the scene's terrain: near the border it stops following and lets
/// the player walk on towards the wall, so nothing outside the world comes into view. It reads the scene's terrains, so it
/// follows them when the map grows. One per scene, anywhere; without it the camera follows as it always did.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/World/View Bounds")]
public sealed class ViewBounds : MonoBehaviour
{
    // A ray above the horizon never meets the ground; it counts as reaching this far.
    private const float FarGround = 90f;

    [Tooltip("How far inside the terrain's edge the view stops, in metres (hills at the edge can hide less than flat ground).")]
    [SerializeField, Min(0f)] private float margin = 2f;

    private Rect area;
    private bool hasArea;

    /// <summary>The bounds of the loaded scene, or null.</summary>
    public static ViewBounds Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private void OnEnable()
    {
        Current = this;
        hasArea = false;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>Where the camera may look so its view stays on the terrain, for a camera <paramref name="offset"/> from what it
    /// looks at.</summary>
    public Vector3 Keep(Vector3 focus, Vector3 offset, float fieldOfView, float aspect)
    {
        if (!hasArea)
        {
            hasArea = MinimapMap.TryGetArea(out area);
            if (!hasArea)
            {
                return focus;
            }
        }

        var inner = new Rect(area.xMin + margin, area.yMin + margin, area.width - margin * 2f, area.height - margin * 2f);
        return KeepInside(inner, focus, offset, fieldOfView, aspect);
    }

    /// <summary>Moves <paramref name="focus"/> as little as it takes for the view's corners, on the ground at the focus's height, to
    /// stay inside <paramref name="area"/>. A view wider than the area is centred on it.</summary>
    public static Vector3 KeepInside(Rect area, Vector3 focus, Vector3 offset, float fieldOfView, float aspect)
    {
        if (offset.sqrMagnitude < 1e-4f)
        {
            return focus;
        }

        Vector3 forward = -offset.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
        Vector3 up = Vector3.Cross(forward, right);
        float tall = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
        float wide = tall * aspect;
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 ray = forward + right * ((corner & 1) == 0 ? -wide : wide) + up * ((corner & 2) == 0 ? -tall : tall);
            Vector3 onGround;
            if (ray.y < -1e-3f)
            {
                onGround = offset + ray * (offset.y / -ray.y);
                onGround.y = 0f;
                onGround = Vector3.ClampMagnitude(onGround, FarGround);
            }
            else
            {
                onGround = offset + new Vector3(ray.x, 0f, ray.z).normalized * FarGround;
            }

            minX = Mathf.Min(minX, onGround.x);
            maxX = Mathf.Max(maxX, onGround.x);
            minZ = Mathf.Min(minZ, onGround.z);
            maxZ = Mathf.Max(maxZ, onGround.z);
        }

        focus.x = KeepAxis(focus.x, minX, maxX, area.xMin, area.xMax);
        focus.z = KeepAxis(focus.z, minZ, maxZ, area.yMin, area.yMax);
        return focus;
    }

    private static float KeepAxis(float at, float low, float high, float areaLow, float areaHigh)
    {
        if (high - low >= areaHigh - areaLow)
        {
            return (areaLow + areaHigh) * 0.5f - (low + high) * 0.5f;
        }

        if (at + low < areaLow)
        {
            return areaLow - low;
        }

        return at + high > areaHigh ? areaHigh - high : at;
    }
}
