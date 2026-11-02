using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Going straight to a building or person from the Haven window: the player steps out beside them, on the side they are coming
/// from, facing them. Runs the clearing without walking it, and never during a breach, when where the player stands matters.
/// </summary>
public static class HavenTravel
{
    private const float StandOff = 1.4f;
    private const float GroundSearch = 6f;
    private const float RayHeight = 30f;

    /// <summary>True while going somewhere is allowed.</summary>
    public static bool IsOpen => WardBreachDirector.Current == null || !WardBreachDirector.Current.IsActive;

    public static void AddGoTo(HavenEntry entry, Transform target)
    {
        bool open = IsOpen;
        entry.Actions.Add(new HavenAction(open ? "Go there" : "Not during a breach", () => GoTo(target), open, primary: false));
    }

    public static void GoTo(Transform target)
    {
        Player player = Player.Current;
        if (target == null || player == null || !IsOpen || !player.TryGetComponent(out PlayerPersistence persistence))
        {
            return;
        }

        Vector3 spot = SpotBeside(target, player.transform.position);
        Vector3 facing = target.position - spot;
        facing.y = 0f;
        float yaw = facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing).eulerAngles.y : player.transform.eulerAngles.y;
        persistence.MoveTo(new PlayerPose(spot, yaw));
    }

    /// <summary>The spot just outside what stands at the target, towards the player, on walkable ground when there is some.</summary>
    public static Vector3 SpotBeside(Transform target, Vector3 from)
    {
        Vector3 away = from - target.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -target.forward;
        float radius = StandOff;
        if (target.TryGetComponent(out BuildingObject building))
        {
            Bounds bounds = building.LocalBounds;
            radius += Mathf.Max(bounds.extents.x, bounds.extents.z);
        }

        Vector3 spot = target.position + away * radius;
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, GroundSearch, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return Physics.Raycast(spot + Vector3.up * RayHeight, Vector3.down, out RaycastHit ground, RayHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? ground.point : spot;
    }
}
