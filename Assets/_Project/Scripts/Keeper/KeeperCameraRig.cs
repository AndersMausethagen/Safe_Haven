using UnityEngine;

/// <summary>
/// Where the Keeper's view looks (Docs/ColonyModeResearch_2026-11-21.md §4.3): the ground point in the middle of the screen,
/// which way the view faces and how far out it is zoomed. It pans along the ground faster the further out it is, turns
/// about the middle, keeps within a circle round the portal and steepens as it rises. Plain C# (KeeperViewTests); KeeperView
/// feeds it input and the camera reads it.
/// </summary>
public sealed class KeeperCameraRig
{
    /// <summary>The ground point the view is sent to (the middle of the screen once it settles).</summary>
    public Vector3 Target;
    /// <summary>Degrees about up the view faces.</summary>
    public float Yaw;
    /// <summary>0 zoomed fully in, 1 fully out.</summary>
    public float Zoom;

    public KeeperCameraRig(Vector3 target, float yaw, float zoom)
    {
        Target = target;
        Yaw = yaw;
        Zoom = Mathf.Clamp01(zoom);
    }

    public float Distance(KeeperViewSettings settings) => Mathf.Lerp(settings.MinDistance, settings.MaxDistance, Zoom);

    public float Pitch(KeeperViewSettings settings) => Mathf.Lerp(settings.PitchNear, settings.PitchFar, Zoom);

    /// <summary>Metres a second a full push pans at this zoom: in step with the distance, so the ground slides past the screen at
    /// the same pace near and far.</summary>
    public float PanSpeed(KeeperViewSettings settings) => settings.PanSpeed * Distance(settings) / Mathf.Max(1f, settings.MinDistance);

    /// <summary>Moves the target along the ground: <paramref name="push"/>.y away from the camera, .x to the right of the screen.</summary>
    public void Pan(Vector2 push, float deltaTime, KeeperViewSettings settings)
    {
        Target += PanDelta(Yaw, Vector2.ClampMagnitude(push, 1f), PanSpeed(settings) * deltaTime);
    }

    public void Turn(float degrees) => Yaw = Mathf.Repeat(Yaw + degrees, 360f);

    public void ZoomBy(float amount) => Zoom = Mathf.Clamp01(Zoom + amount);

    /// <summary>Keeps the target within <paramref name="radius"/> of <paramref name="centre"/> across the ground.</summary>
    public void KeepWithin(Vector3 centre, float radius) => Target = ClampToCircle(Target, centre, radius);

    /// <summary>The ground step for a push in screen terms, with the view facing <paramref name="yaw"/>.</summary>
    public static Vector3 PanDelta(float yaw, Vector2 push, float metres)
    {
        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        return (facing * Vector3.right * push.x + facing * Vector3.forward * push.y) * metres;
    }

    /// <summary><paramref name="point"/> pulled across the ground to within <paramref name="radius"/> of <paramref name="centre"/>; its height kept.</summary>
    public static Vector3 ClampToCircle(Vector3 point, Vector3 centre, float radius)
    {
        Vector3 across = point - centre;
        across.y = 0f;
        if (across.sqrMagnitude <= radius * radius)
        {
            return point;
        }

        Vector3 inside = centre + across.normalized * radius;
        inside.y = point.y;
        return inside;
    }
}
