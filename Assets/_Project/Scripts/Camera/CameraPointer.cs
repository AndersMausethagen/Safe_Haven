using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Where the player is pointing. In the top-down style that is the mouse cursor; in the action style the cursor is
/// locked and hidden, so it is the crosshair in the middle of the screen. Systems that aim or pick things in the world
/// (attacks, building placement) ask here instead of reading the mouse position, so they work in both styles.
/// </summary>
public static class CameraPointer
{
    /// <summary>True while the action camera is active; set by PlayerCameraController.</summary>
    public static bool UsesScreenCenter { get; set; }

    /// <summary>How far along the view a crosshair pick reaches; set by PlayerCameraController from its settings.</summary>
    public static float Reach { get; set; } = 14f;

    public static bool TryGetPointOnPlane(Camera camera, Plane plane, out Vector3 point)
    {
        return TryGetPointOnPlane(camera, plane, Reach, out point);
    }

    public static bool TryGetRay(Camera camera, out Ray ray)
    {
        if (camera != null && UsesScreenCenter)
        {
            ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            return true;
        }

        if (camera == null || Mouse.current == null)
        {
            ray = default;
            return false;
        }

        ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        return true;
    }

    /// <summary>
    /// The pointed-at spot on a flat plane. With the crosshair, looking far away or above the horizon would leave the
    /// spot out of reach, so it is held at most <paramref name="maxDistance"/> along the view and dropped onto the plane.
    /// </summary>
    public static bool TryGetPointOnPlane(Camera camera, Plane plane, float maxDistance, out Vector3 point)
    {
        point = default;
        if (!TryGetRay(camera, out Ray ray))
        {
            return false;
        }

        if (plane.Raycast(ray, out float distance) && (!UsesScreenCenter || distance <= maxDistance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        if (!UsesScreenCenter)
        {
            return false;
        }

        point = plane.ClosestPointOnPlane(ray.GetPoint(maxDistance));
        return true;
    }
}
