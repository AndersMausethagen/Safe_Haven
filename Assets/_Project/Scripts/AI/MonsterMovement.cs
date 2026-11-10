using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// How a monster gets about on the NavMesh: walking to a point (asking for a new path at most every repath interval),
/// stopping, turning on the spot, rushing straight ahead until a wall, and stepping back onto the NavMesh when it was put
/// beside it. Plain C#, owned by a MonsterAI, which decides where to go.
/// </summary>
public sealed class MonsterMovement
{
    private const float FacingTolerance = 20f;
    private const float NavMeshSnapDistance = 2f;
    private const float CutOffGap = 1.5f;

    private readonly Transform self;
    private readonly NavMeshAgent agent;
    private readonly float repathInterval;
    private readonly float turnSpeed;

    private float nextRepathTime;
    private bool warnedOffNavMesh;
    private Vector3 asked;
    private bool hasAsked;

    /// <summary>
    /// Co-op: a friend's copy of the host's creature. Its agent is off (it goes where the host's goes), so the one move it
    /// makes by itself, a charge, travels over the NavMesh without it. Left as it was, a charge found no agent, ended where
    /// it began and never reached anyone.
    /// </summary>
    public bool Detached { get; set; }

    public MonsterMovement(Transform self, NavMeshAgent agent, float repathInterval, float turnSpeed)
    {
        this.self = self;
        this.agent = agent;
        this.repathInterval = repathInterval;
        this.turnSpeed = turnSpeed;
    }

    /// <summary>Horizontal movement speed in metres per second, for animation.</summary>
    public float Speed
    {
        get
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                return 0f;
            }

            Vector3 velocity = agent.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    /// <summary>True when it has gone as far as the NavMesh lets it, and that isn't where it was going: its target stands somewhere it can't reach.</summary>
    public bool IsCutOff => hasAsked && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.remainingDistance < 0.8f
        // Not the path status: at the end of a path it can't finish, that reads complete with no distance left, and the search for
        // a way that isn't there is pending nearly all the time. It is simply still far off.
        && (self.position - asked).sqrMagnitude > CutOffGap * CutOffGap;

    public void SetSpeed(float metresPerSecond)
    {
        agent.speed = metresPerSecond;
    }

    /// <summary>The next <see cref="MoveTo"/> asks for a path at once (it has somewhere new to go).</summary>
    public void RepathNow()
    {
        nextRepathTime = 0f;
    }

    /// <summary>Walks toward <paramref name="destination"/>. False when it can't (off the NavMesh, no path).</summary>
    public bool MoveTo(Vector3 destination)
    {
        if (Time.time < nextRepathTime)
        {
            return true;
        }

        nextRepathTime = Time.time + repathInterval;
        if (!EnsureOnNavMesh())
        {
            return false;
        }

        agent.updateRotation = true;
        asked = destination;
        hasAsked = true;
        // Re-requesting the same spot while the last search is still running would restart it and, on a long path,
        // keep it from ever finishing.
        if (agent.pathPending && (agent.destination - destination).sqrMagnitude < 0.25f)
        {
            return true;
        }

        return agent.SetDestination(destination);
    }

    /// <summary>Walks toward <paramref name="destination"/> looking along <paramref name="facing"/> instead of where it goes.</summary>
    public void Strafe(Vector3 destination, Vector3 facing)
    {
        MoveTo(destination);
        agent.updateRotation = false;
        TurnTowards(facing);
    }

    public void Stop()
    {
        agent.updateRotation = false;
        nextRepathTime = 0f;
        hasAsked = false;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    /// <summary>Stops for good (it died): the agent no longer moves or blocks others' paths.</summary>
    public void Halt()
    {
        Stop();
        agent.enabled = false;
    }

    /// <summary>Turns toward <paramref name="direction"/> at its turn speed. True once it faces it.</summary>
    public bool TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Quaternion desired = Quaternion.LookRotation(direction);
        self.rotation = Quaternion.RotateTowards(self.rotation, desired, turnSpeed * Time.deltaTime);
        return Quaternion.Angle(self.rotation, desired) <= FacingTolerance;
    }

    /// <summary>Moves up to <paramref name="distance"/> along <paramref name="direction"/> (a charge). True when a wall
    /// stopped it.</summary>
    public bool Rush(Vector3 direction, float distance)
    {
        if (Detached)
        {
            return RushDetached(direction, distance);
        }

        Vector3 before = self.position;
        bool blocked = agent.Raycast(before + direction * distance, out NavMeshHit wall);
        agent.Move(blocked ? wall.position - before : direction * distance);
        return blocked;
    }

    // The same move asked of the NavMesh itself: stopped by its edges, kept on its ground (the agent's own height above it).
    private bool RushDetached(Vector3 direction, float distance)
    {
        Vector3 before = self.position;
        Vector3 step = direction * distance;
        if (!NavMesh.SamplePosition(before, out NavMeshHit from, NavMeshSnapDistance, agent.areaMask))
        {
            // Off the NavMesh altogether (the host's stands where this game has none): straight on, as the host's went.
            self.position = before + step;
            return false;
        }

        Vector3 to = from.position + step;
        bool blocked = NavMesh.Raycast(from.position, to, out NavMeshHit wall, agent.areaMask) && wall.hit;
        if (blocked)
        {
            to = wall.position;
        }
        else if (NavMesh.SamplePosition(to, out NavMeshHit ground, NavMeshSnapDistance, agent.areaMask))
        {
            to = ground.position;
        }

        self.position = to + Vector3.up * agent.baseOffset;
        return blocked;
    }

    /// <summary>The nearest NavMesh point within <paramref name="radius"/> of <paramref name="near"/> it can walk on.</summary>
    public bool TryFindGround(Vector3 near, float radius, out Vector3 point)
    {
        bool found = NavMesh.SamplePosition(near, out NavMeshHit hit, radius, agent.areaMask);
        point = found ? hit.position : near;
        return found;
    }

    /// <summary>True when it can move: on the NavMesh, or stepped onto it from close by.</summary>
    public bool EnsureOnNavMesh()
    {
        if (Detached)
        {
            return true;
        }

        if (!agent.isActiveAndEnabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (NavMesh.SamplePosition(self.position, out NavMeshHit hit, NavMeshSnapDistance, agent.areaMask)
            && agent.Warp(hit.position))
        {
            return true;
        }

        if (!warnedOffNavMesh)
        {
            warnedOffNavMesh = true;
            Debug.LogWarning($"{self.name} is not on a NavMesh, so it can't move. Add a NavMeshSurface to the scene and bake it.", self);
        }

        return false;
    }
}
