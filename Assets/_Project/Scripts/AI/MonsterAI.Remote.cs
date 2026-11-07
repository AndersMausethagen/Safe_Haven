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
    /// <summary>Seconds behind the host a puppet is drawn: enough to cover two lost updates at ten a second (snapshot interpolation).</summary>
    private const double RemoteDelay = 0.15;
    private const float RemoteSnapDistance = 6f;

    /// <summary>Seconds without a pose after which the creature is taken to have stood still meanwhile.</summary>
    private const double RemoteQuietGap = 0.25;

    /// <summary>Whom the host's creature is after, as told to this player: nobody, them, or someone else on their side.</summary>
    public enum RemoteHunt : byte
    {
        None = 0,
        You = 1,
        Friend = 2,
    }

    private readonly PoseBuffer remotePoses = new PoseBuffer();
    private RemoteHunt remoteHunt;
    private float remoteSpeed;
    private bool suppressed;

    /// <summary>The session's clock (co-op sets it): puppets are drawn on it, a little behind.</summary>
    public static System.Func<double> RemoteClock { get; set; }

    /// <summary>
    /// True while co-op builds a puppet. A friend's game on the host's run makes no creatures of its own: any other made
    /// now is removed before it can think (<see cref="CoopCreatures.FromHost"/>).
    /// </summary>
    public static bool SpawningRemote { get; set; }

    /// <summary>True for a creature this game was about to make while the host's game sends them: it goes at once.</summary>
    private bool SuppressedByHost()
    {
        if (!CoopCreatures.FromHost || SpawningRemote)
        {
            return false;
        }

        suppressed = true;
        Destroy(gameObject);
        return true;
    }

    /// <summary>True for a creature shown in a friend's game and run by the host's.</summary>
    public bool IsRemote { get; private set; }

    /// <summary>The attack action last begun (-1: the definition's plain attack), so a host can tell friends which.</summary>
    public int LastAction { get; private set; } = -1;

    /// <summary>Hands this creature to the host's game: no more thinking or path finding here.</summary>
    public void MakeRemote()
    {
        IsRemote = true;
        remotePoses.Clear();
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

    /// <summary>
    /// Where the host's creature was at <paramref name="time"/> (the session's clock) and what it was doing. A strike or death
    /// in progress here finishes first.
    /// </summary>
    public void ApplyRemotePose(double time, Vector3 position, float yaw, float speed, State state, RemoteHunt hunting = RemoteHunt.None)
    {
        remoteHunt = hunting;
        // Nothing was sent while it stood still: it starts moving from where it stood, a moment before this, not slides from
        // the last pose a while ago.
        if (remotePoses.TryNewest(out PoseSnapshot newest) && time - newest.Time > RemoteQuietGap)
        {
            remotePoses.Add(new PoseSnapshot { Time = time - 0.1, Position = newest.Position, Yaw = newest.Yaw });
        }

        remotePoses.Add(new PoseSnapshot { Time = time, Position = position, Yaw = yaw });
        remoteSpeed = speed;

        if (CurrentState != State.Attacking && CurrentState != State.Dead && state != State.Attacking && state != State.Dead)
        {
            SetState(state);
        }
    }

    /// <summary>
    /// The host's creature strikes from <paramref name="from"/>: this one steps there (it is drawn a little behind) and the
    /// same action plays here, at <paramref name="at"/> (this player) or straight ahead.
    /// </summary>
    public void PlayRemoteStrike(int action, IDamageable at, Vector3 from, float yaw)
    {
        if (!IsRemote || CurrentState == State.Dead || definition == null)
        {
            return;
        }

        transform.SetPositionAndRotation(from, Quaternion.Euler(0f, yaw, 0f));

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

        if (RemoteClock == null || !remotePoses.Sample(RemoteClock() - RemoteDelay, RemoteSnapDistance, out Vector3 position, out float yaw, out _))
        {
            return;
        }

        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRemoteForNewPlaySession()
    {
        RemoteClock = null;
        SpawningRemote = false;
    }
}
