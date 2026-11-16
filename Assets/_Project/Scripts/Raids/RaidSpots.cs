using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finding places in a running raid: on the ground, walkable, out of the player's sight and with a way to the player.
/// Shared by what appears during a raid (RoamingSpawner, RaidEventDirector).
/// </summary>
public static class RaidSpots
{
    private const float GroundProbeHeight = 60f;
    private const float NavMeshSnap = 4f;

    private static readonly RaycastHit[] SightHits = new RaycastHit[16];

    /// <summary>In front of the camera, inside its view and with nothing solid in between.</summary>
    public static bool IsVisible(Vector3 point) => IsVisible(point, null);

    /// <summary>
    /// The same for something standing at <paramref name="point"/>: its own colliders (under <paramref name="self"/>) don't hide
    /// it. A troll's capsule reaches past chest height, so the plain check always found it hidden by its own body, and roamers
    /// were taken away in plain sight.
    /// </summary>
    public static bool IsVisible(Vector3 point, Transform self)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Vector3 chest = point + Vector3.up * 1.2f;
        Vector3 viewport = view.WorldToViewportPoint(chest);
        if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f)
        {
            return false;
        }

        Vector3 from = view.transform.position;
        if (self == null)
        {
            return !Physics.Linecast(from, chest, ~0, QueryTriggerInteraction.Ignore);
        }

        Vector3 way = chest - from;
        int count = Physics.RaycastNonAlloc(from, way.normalized, SightHits, way.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!SightHits[i].transform.IsChildOf(self))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Points are picked flat at the player's height; this drops one onto the ground below or above it so the
    /// NavMesh lookup works on hilly terrain.</summary>
    public static Vector3 Grounded(Vector3 point)
    {
        return Physics.Raycast(point + Vector3.up * GroundProbeHeight, Vector3.down, out RaycastHit ground, GroundProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore)
            ? ground.point
            : point;
    }

    /// <summary>
    /// A walkable spot between the two distances from <paramref name="from"/>, out of sight and with a complete path back
    /// to it. False when none turned up in <paramref name="attempts"/> tries.
    /// </summary>
    public static bool TryFindSpot(Vector3 from, float minDistance, float maxDistance, System.Random random, NavMeshPath path, out Vector3 spot, int attempts = 16)
    {
        spot = from;
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, NavMeshSnap, NavMesh.AllAreas))
        {
            return false;
        }

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(minDistance, maxDistance, (float)random.NextDouble());
            Vector3 candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(Grounded(candidate), out NavMeshHit hit, NavMeshSnap, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 flat = hit.position - from;
            flat.y = 0f;
            if (flat.magnitude < minDistance || IsVisible(hit.position)
                || !NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            spot = hit.position;
            return true;
        }

        return false;
    }
}
