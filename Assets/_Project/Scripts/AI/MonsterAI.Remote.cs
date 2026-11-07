using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Co-op: a creature in a friend's game that is the host's (<see cref="CoopCreatures"/>). It makes no decisions of its
/// own: it goes where the host's goes, in its state, and strikes when the host's strikes. The strike itself plays out here,
/// against what stands here, so a blow lands or misses on the player who sees it (no network delay to dodge around).
/// Damage it takes here shows at once but can't kill it (<see cref="Damageable.HealthFloor"/>): the host's game decides
/// when it dies, and each player's own copy then drops that player's loot.
/// </summary>
public sealed partial class MonsterAI
{
    private const float RemoteFollowRate = 10f;
    private const float RemoteSnapDistance = 6f;

    private Vector3 remoteAt;
    private float remoteYaw;
    private float remoteSpeed;

    /// <summary>True for a creature shown in a friend's game and run by the host's.</summary>
    public bool IsRemote { get; private set; }

    /// <summary>The attack action last begun (-1: the definition's plain attack), so a host can tell friends which.</summary>
    public int LastAction { get; private set; } = -1;

    /// <summary>Hands this creature to the host's game: no more thinking or path finding here.</summary>
    public void MakeRemote()
    {
        IsRemote = true;
        remoteAt = transform.position;
        remoteYaw = transform.eulerAngles.y;
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = false;
        }
    }

    /// <summary>Takes this creature back into this game (the host left the run): it thinks and finds its way again.</summary>
    public void MakeLocal()
    {
        if (!IsRemote)
        {
            return;
        }

        IsRemote = false;
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = true;
            agent.Warp(transform.position);
        }
    }

    /// <summary>Where the host's creature is and what it is doing. A strike or death in progress here finishes first.</summary>
    public void ApplyRemotePose(Vector3 position, float yaw, float speed, State state)
    {
        remoteAt = position;
        remoteYaw = yaw;
        remoteSpeed = speed;
        if ((transform.position - position).sqrMagnitude > RemoteSnapDistance * RemoteSnapDistance)
        {
            transform.position = position;
        }

        if (CurrentState != State.Attacking && CurrentState != State.Dead && state != State.Attacking && state != State.Dead)
        {
            SetState(state);
        }
    }

    /// <summary>The host's creature strikes: the same action plays here, at <paramref name="at"/> (this player) or straight ahead.</summary>
    public void PlayRemoteStrike(int action, IDamageable at)
    {
        if (!IsRemote || CurrentState == State.Dead || definition == null)
        {
            return;
        }

        Vector3 toTarget = transform.forward;
        if (at is Component body && CanTarget(at))
        {
            SetTarget(at, null);
            toTarget = body.transform.position - transform.position;
            toTarget.y = 0f;
        }

        BeginAttack(action, toTarget.sqrMagnitude > 0.0001f ? toTarget : transform.forward);
    }

    /// <summary>
    /// Co-op, in the host's game: a friend's blow landed. The creature turns on them and stays on them a while, as it would
    /// on anyone who hit it (a blow from afar draws it out of its ground).
    /// </summary>
    public void ProvokedBy(IDamageable friend)
    {
        if (definition == null || CurrentState == State.Dead || !CanTarget(friend))
        {
            return;
        }

        if (!CanTarget(target))
        {
            SetTarget(friend, null);
        }

        if (target == friend)
        {
            provokedUntil = Time.time + PerceptionSettings.Current.ProvokedSeconds;
        }
    }

    private void UpdateRemote()
    {
        if (CurrentState == State.Attacking)
        {
            UpdateAttack();
            return;
        }

        float follow = 1f - Mathf.Exp(-RemoteFollowRate * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, remoteAt, follow);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, remoteYaw, 0f), follow);
    }
}
