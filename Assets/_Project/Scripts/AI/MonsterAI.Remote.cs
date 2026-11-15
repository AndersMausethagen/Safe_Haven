using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Co-op: a creature in a friend's game that is the host's (<see cref="CoopCreatures"/>). It makes no decisions of its
/// own: it goes where the host's goes, in its state, and strikes when the host's strikes. The strike itself plays out here,
/// against what stands here, so a blow lands or misses on the player who sees it (no network delay to dodge around).
/// Damage it takes here shows at once but can't kill it (<see cref="Damageable.HealthFloor"/>): the host's game decides
/// when it dies, and each player's own copy then drops that player's loot.
/// <para>
/// What it is told and what it works out: where it is, its state and whom it is after come from the host; whether it is
/// unaware (an ambush) is read from those (<see cref="IsRemoteUnaware"/>), as its own senses never look. A stagger is the
/// host's too, shown here when its state says so; one begun here (this player's parry) is told to the host
/// (<see cref="RemoteStaggered"/>) and stands meanwhile. A charge travels here by itself (<see cref="MonsterMovement.Detached"/>).
/// </para>
/// </summary>
public sealed partial class MonsterAI
{
    /// <summary>Seconds behind the host a puppet is drawn: enough to cover two lost updates at ten a second (snapshot interpolation).</summary>
    private const double RemoteDelay = 0.15;
    private const float RemoteSnapDistance = 6f;

    /// <summary>Seconds without a pose after which the creature is taken to have stood still meanwhile.</summary>
    private const double RemoteQuietGap = 0.25;

    /// <summary>Seconds a stagger begun here stands whatever the host's poses say: time for the host's game to hear of it and answer.</summary>
    private const float RemoteStaggerHoldSeconds = 0.6f;

    /// <summary>How far ahead a strike is taken to be aimed when the host's game named no place for it.</summary>
    private const float RemoteAimDistance = 4f;

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
    private float remoteStaggerHold = float.NegativeInfinity;

    /// <summary>
    /// Raised in a friend's game when something there staggers one of the host's creatures (this player's parry, a finishing
    /// blow holding it), with the seconds: co-op tells the host's game, which staggers the real one for everybody.
    /// </summary>
    public static event System.Action<MonsterAI, float> RemoteStaggered;

    // The host's creature has noticed nobody: it idles or walks its rounds and is after no one. Its awareness itself isn't
    // sent, and this copy's own senses never look, so by them it would count as unaware for good: every blow an ambush.
    private bool IsRemoteUnaware => remoteHunt == RemoteHunt.None && (CurrentState == State.Idle || CurrentState == State.Returning);

    // Staggered by something in this game: it stands for a moment whatever the poses say, and the host's game is told.
    private void NoteStaggeredHere(float seconds)
    {
        remoteStaggerHold = Mathf.Max(remoteStaggerHold, Time.time + Mathf.Max(RemoteStaggerHoldSeconds, seconds - RemoteStaggerHoldSeconds));
        RemoteStaggered?.Invoke(this, seconds);
    }

    /// <summary>The session's clock (co-op sets it): puppets are drawn on it, a little behind.</summary>
    public static System.Func<double> RemoteClock { get; set; }

    /// <summary>
    /// True while co-op builds a puppet. A friend's game on the host's run makes no creatures of its own: any other made
    /// now is removed before it can think (<see cref="CreatureSource.Remote"/>, set by co-op).
    /// </summary>
    public static bool SpawningRemote { get; set; }

    /// <summary>True for a creature this game was about to make while the host's game sends them: it goes at once.</summary>
    private bool SuppressedByHost()
    {
        if (CreatureSource.Remote == null || SpawningRemote)
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
        remoteHunt = RemoteHunt.None;
        remoteStaggerHold = float.NegativeInfinity;
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = false;
        }

        if (movement != null)
        {
            movement.Detached = true;
        }

        RefreshAmbush();
    }

    /// <summary>Takes this creature back into this game (the host left the run): it thinks and finds its way again.</summary>
    public void MakeLocal()
    {
        if (!IsRemote)
        {
            return;
        }

        IsRemote = false;
        remoteHunt = RemoteHunt.None;
        if (movement != null)
        {
            movement.Detached = false;
        }

        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = true;
            agent.Warp(transform.position);
        }

        RefreshAmbush();
    }

    /// <summary>
    /// Where the host's creature was at <paramref name="time"/> (the session's clock) and what it was doing. A strike or death
    /// in progress here finishes first.
    /// </summary>
    /// <param name="poiseShare">The host's creature's poise, 0-1: this copy wears none of its own, so its reeling and poise bar show the host's.</param>
    /// <param name="waiting">The host's creature is waiting its turn (a ready stance).</param>
    /// <param name="felled">The host's creature is down from a felling blow (a longer stagger, lying and getting up).</param>
    /// <param name="guarding">The host's creature has its weapon raised (light blows from the front are turned aside here too).</param>
    public void ApplyRemotePose(double time, Vector3 position, float yaw, float speed, State state, RemoteHunt hunting = RemoteHunt.None,
        float poiseShare = 1f, bool waiting = false, bool felled = false, bool guarding = false, float noticeLevel = 0f, RemoteHunt noticing = RemoteHunt.None,
        bool marked = false)
    {
        remoteMarked = marked;
        remoteHunt = hunting;
        remoteNoticeLevel = noticeLevel;
        remoteNoticing = noticing;
        if (noticing == RemoteHunt.You && noticeLevel > 0f)
        {
            EnsureIndicator();
        }

        remoteWaiting = waiting;
        remoteGuarding = guarding;
        poise?.Show(poiseShare);
        // Nothing was sent while it stood still: it starts moving from where it stood, a moment before this, not slides from
        // the last pose a while ago.
        if (remotePoses.TryNewest(out PoseSnapshot newest) && time - newest.Time > RemoteQuietGap)
        {
            remotePoses.Add(new PoseSnapshot { Time = time - 0.1, Position = newest.Position, Yaw = newest.Yaw });
        }

        remotePoses.Add(new PoseSnapshot { Time = time, Position = position, Yaw = yaw });
        remoteSpeed = speed;

        bool dead = CurrentState == State.Dead || state == State.Dead;
        // A stagger begun here stands until the host's game has had time to hear of it.
        bool holdsStagger = CurrentState == State.Staggered && state != State.Staggered && Time.time < remoteStaggerHold;
        if (!dead && !holdsStagger && state != CurrentState && definition != null)
        {
            if (state == State.Staggered)
            {
                // The host's creature staggers (its poise broke, someone parried it): shown here as a stagger is, open to the
                // same blows and out of any strike it was playing, but not told back to the host.
                felling = felled;
                BeginStagger(felled ? CombatTechniqueSettings.Current.FelledSeconds : CombatTechniqueSettings.Current.StaggerSeconds);
                felling = false;
            }
            else if (CurrentState != State.Attacking && state != State.Attacking)
            {
                // A strike in progress here finishes first; one in the host's game comes as a strike, not as a pose.
                SetState(state);
            }
        }

        RefreshAmbush();
    }

    /// <summary>
    /// The host's creature strikes from <paramref name="from"/>: this one steps there (it is drawn a little behind) and the
    /// same action plays here, at <paramref name="at"/> (this player, where they really stand) or, for a strike at someone
    /// else, at <paramref name="aimedAt"/>: where the host's creature aimed it.
    /// </summary>
    public void PlayRemoteStrike(int action, IDamageable at, Vector3 from, float yaw, Vector3? aimedAt = null)
    {
        if (!IsRemote || CurrentState == State.Dead || definition == null)
        {
            return;
        }

        // An action this game's creature doesn't have (it can't, from the same build), or nothing to strike with.
        if (action >= definition.Attacks.Count || (action < 0 && definition.Attack == null))
        {
            return;
        }

        // Staggered here a moment ago (a parry) and the host's game hasn't heard yet: what it sent meanwhile is void.
        if (CurrentState == State.Staggered && Time.time < remoteStaggerHold)
        {
            return;
        }

        transform.SetPositionAndRotation(from, Quaternion.Euler(0f, yaw, 0f));

        Vector3 toTarget;
        if (at is Component body && CanTarget(at))
        {
            SetTarget(at, null);
            toTarget = body.transform.position - transform.position;
        }
        else
        {
            // Not at this player: nothing here is its target. A target kept from an earlier strike (or from this player's
            // last blow on it) aimed its shots and follow-up blows at them, though the host's creature struck at someone else.
            ClearTarget();
            toTarget = aimedAt.HasValue ? aimedAt.Value - transform.position : transform.forward * RemoteAimDistance;
        }

        toTarget.y = 0f;
        BeginAttack(action, toTarget.sqrMagnitude > 0.0001f ? toTarget : transform.forward * RemoteAimDistance);
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
