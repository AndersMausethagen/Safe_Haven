using UnityEngine;

/// <summary>
/// Walks a monster around a route while it has nothing better to do: from waypoint to waypoint (the route's children, in
/// order, looping), pausing at each. It uses MonsterAI.Roam, so the monster notices and hunts on the way like any
/// roamer, and goes back to its round once it has searched and given up. Put the route under the raid's Map Content so
/// it turns with the map.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterAI))]
public sealed class MonsterPatrol : MonoBehaviour
{
    [Tooltip("Its children are the waypoints, walked in order and back to the first.")]
    [SerializeField] private Transform route;
    [Tooltip("Seconds it stands at each waypoint, listening, before walking on.")]
    [SerializeField, Min(0f)] private float pauseSeconds = 4f;
    [Tooltip("Walking speed on the round, as a share of its full speed.")]
    [SerializeField, Range(0.1f, 1f)] private float walkSpeedScale = 0.55f;

    private MonsterAI monster;
    private int next;
    private float walkOnAt;
    private bool waiting;

    public Transform Route => route;

    /// <summary>Gives it a route (scene setup tools).</summary>
    public void SetRoute(Transform newRoute)
    {
        route = newRoute;
        next = 0;
    }

    private void Awake()
    {
        monster = GetComponent<MonsterAI>();
    }

    private void Start()
    {
        monster.RoamSpeedScale = walkSpeedScale;
        // A round only matters if it is walked while the player is elsewhere: that is what makes the woods feel inhabited.
        monster.KeepAwake = true;
        next = NearestWaypoint();
    }

    private void Update()
    {
        if (route == null || route.childCount == 0 || !monster.enabled || monster.CurrentState != MonsterAI.State.Idle)
        {
            waiting = false;
            return;
        }

        // Idle again: it has just reached a waypoint, or come back from a hunt or a search.
        if (!waiting)
        {
            waiting = true;
            walkOnAt = Time.time + pauseSeconds;
            return;
        }

        if (Time.time < walkOnAt)
        {
            return;
        }

        waiting = false;
        monster.Roam(route.GetChild(next).position);
        next = (next + 1) % route.childCount;
    }

    private int NearestWaypoint()
    {
        if (route == null || route.childCount == 0)
        {
            return 0;
        }

        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < route.childCount; i++)
        {
            float distance = (route.GetChild(i).position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void OnDrawGizmosSelected()
    {
        if (route == null || route.childCount < 2)
        {
            return;
        }

        Gizmos.color = new Color(0.4f, 1f, 1f, 0.8f);
        for (int i = 0; i < route.childCount; i++)
        {
            Vector3 from = route.GetChild(i).position + Vector3.up * 0.3f;
            Vector3 to = route.GetChild((i + 1) % route.childCount).position + Vector3.up * 0.3f;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawWireSphere(from, 0.6f);
        }
    }
}
