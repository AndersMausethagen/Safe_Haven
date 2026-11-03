using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A resident's day (GDD §15): they walk to their work and get on with it, eat, sit at the fire of an evening, sleep at their own spot,
/// and run for the portal when the haven is attacked. They stop to talk when spoken to and carry on after. What they do when is
/// ResidentRoutine and Settlement Settings › Daily Life; the gesture for a job is on its JobDefinition. Added by ResidentNpc. Where
/// the ground has no NavMesh they go straight to their place, as there is no way to walk there.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ResidentNpc))]
public sealed class ResidentLife : MonoBehaviour
{
    private const float ThinkSeconds = 0.5f;
    private const float ArriveDistance = 0.7f;
    private const float StuckSeconds = 4f;
    private const float TalkTurnSpeed = 240f;
    private const float RingRadius = 2.4f;
    private const float WorkmateGap = 1.2f;
    private const float GuardRadius = 6f;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private ResidentNpc npc;
    private Talkable talkable;
    private NavMeshAgent agent;
    private Animator animator;
    private CharacterEmotes emotes;
    private DayNightCycle clock;
    private Vector3 home;
    private Quaternion homeFacing;
    private Vector3 place;
    private Quaternion facing;
    private ResidentActivity activity;
    private bool started;
    private bool arrived;
    private float nextThinkAt;
    private float stuckSince = -1f;
    private float nextGestureAt;
    private float ringAngle;
    private float workShift;
    private GameObject tool;
    private ItemDefinition toolItem;
    private float workSoundAt = float.PositiveInfinity;
    private ResidentDefender defender;

    /// <summary>What they are about right now.</summary>
    public ResidentActivity Activity => activity;

    /// <summary>True once they are at the place for it and getting on with it.</summary>
    public bool HasArrived => arrived;

    private void Awake()
    {
        npc = GetComponent<ResidentNpc>();
        talkable = GetComponent<Talkable>();
    }

    /// <summary>Their own spot, where they sleep, and which side of the fire is theirs.</summary>
    public void Settle(Vector3 homePosition, Quaternion homeRotation, int seat)
    {
        home = homePosition;
        homeFacing = homeRotation;
        // The golden angle, so any number of them sit apart.
        ringAngle = seat * 137.5f;
        // Those who share a workplace stand side by side along it.
        workShift = (seat % 3 - 1) * WorkmateGap;
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null && !animator.TryGetComponent(out emotes))
        {
            emotes = animator.gameObject.AddComponent<CharacterEmotes>();
        }

        clock = FindAnyObjectByType<DayNightCycle>();
        SettlementSettings settings = SettlementSettings.Current;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = settings.ResidentWalkSpeed;
            agent.angularSpeed = 300f;
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.Warp(hit.position);
        }

        started = true;
        // On arriving in the scene everyone is already where the hour has them: a whole haven doesn't set off at once on every load.
        Decide(true);
    }

    /// <summary>Thinks again now (a workplace was built, or a job given).</summary>
    public void Refresh()
    {
        if (started)
        {
            Decide(false, true);
        }
    }

    private void Update()
    {
        if (!started)
        {
            return;
        }

        if (KeepsGuard())
        {
            return;
        }

        if (talkable != null && talkable.IsTalking)
        {
            Listen();
            return;
        }

        if (agent != null && agent.isStopped)
        {
            agent.isStopped = false;
            arrived = false;
        }

        if (Time.time >= nextThinkAt)
        {
            nextThinkAt = Time.time + ThinkSeconds;
            Decide(false);
        }

        if (arrived)
        {
            GetOnWithIt();
        }
        else
        {
            GetThere();
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f, 0.15f, Time.deltaTime);
        }
    }

    // A breach is on and their job defends the Haven: they are on guard until it is over, and the guard is who the player sees.
    private bool KeepsGuard()
    {
        bool breach = WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive;
        CompanionDefinition how = breach ? npc.Job?.DefendsAs : null;
        defender ??= new ResidentDefender(transform, animator, agent);
        if (how != null && !defender.IsOnGuard)
        {
            if (emotes != null && emotes.IsPlaying)
            {
                emotes.Stop();
            }

            defender.Stand(npc.Resident, how, GuardPost());
        }
        else if (how == null && defender.IsOnGuard)
        {
            defender.StandDown();
            Decide(false, true);
        }

        return defender.IsOnGuard;
    }

    // Up on the Watch Post if that is where their job is; otherwise a ring out from the portal, between it and what comes.
    private Vector3 GuardPost()
    {
        BuildingDefinition workplace = npc.Job?.Workplace;
        BuildingObject post = workplace != null ? BuildingObject.Find(workplace) : null;
        if (post != null && post.GetComponent<WatchPostArcher>() != null && npc.TryGetWorkplace(out Vector3 spot, out _))
        {
            return spot;
        }

        Vector3 round = Quaternion.Euler(0f, ringAngle, 0f) * Vector3.forward;
        return (HomePortal.Current != null ? HomePortal.Current.transform.position : home) + round * GuardRadius;
    }

    /// <summary>The guard they have become in a breach, or null.</summary>
    public Companion Guard => defender?.Guard;

    private void Listen()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        if (emotes != null && emotes.IsPlaying)
        {
            emotes.Stop();
        }

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, 0f, 0.15f, Time.deltaTime);
        }

        Player player = Player.Current;
        if (player != null)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), TalkTurnSpeed * Time.deltaTime);
            }
        }
    }

    private void Decide(bool atOnce, bool again = false)
    {
        SettlementSettings settings = SettlementSettings.Current;
        bool hasWorkplace = npc.TryGetWorkplace(out Vector3 workSpot, out Quaternion workFacing);
        ResidentActivity next = ResidentRoutine.Decide(clock != null ? clock.Hour : 12f, settings.ResidentHours, hasWorkplace, InDanger(settings));
        if (next == activity && started && !atOnce && !again)
        {
            return;
        }

        activity = next;
        switch (next)
        {
            case ResidentActivity.Work:
                place = workSpot + workFacing * Vector3.right * workShift;
                facing = workFacing;
                break;
            case ResidentActivity.Sleep:
                place = home;
                facing = homeFacing;
                break;
            default:
                Vector3 centre = Gathering(next, settings);
                Vector3 round = Quaternion.Euler(0f, ringAngle, 0f) * Vector3.forward;
                place = centre + round * RingRadius;
                facing = Quaternion.LookRotation(-round);
                break;
        }

        if (agent != null)
        {
            agent.speed = next == ResidentActivity.Shelter ? settings.ResidentRunSpeed : settings.ResidentWalkSpeed;
        }

        if (emotes != null && emotes.IsPlaying)
        {
            emotes.Stop();
        }

        arrived = false;
        stuckSince = -1f;
        workSoundAt = float.PositiveInfinity;
        ShowTool(null, 1f);
        if (atOnce || agent == null || !agent.isOnNavMesh)
        {
            PutThere();
        }
        else if (NavMesh.SamplePosition(place, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
    }

    // Where they gather: the fire if there is one, else the portal. In danger, always the portal: it is the heart of the place.
    private Vector3 Gathering(ResidentActivity next, SettlementSettings settings)
    {
        BuildingObject fire = next != ResidentActivity.Shelter && settings.ResidentGatheringPlace != null ? BuildingObject.Find(settings.ResidentGatheringPlace) : null;
        if (fire != null)
        {
            return fire.transform.position;
        }

        return HomePortal.Current != null ? HomePortal.Current.transform.position : home;
    }

    private bool InDanger(SettlementSettings settings)
    {
        if (WardBreachDirector.Current != null && WardBreachDirector.Current.IsActive)
        {
            return true;
        }

        float reach = settings.ResidentDangerRadius * settings.ResidentDangerRadius;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && monster.CurrentState != MonsterAI.State.Dead && monster.IsHuntingPlayerSide
                && (monster.transform.position - transform.position).sqrMagnitude <= reach)
            {
                return true;
            }
        }

        return false;
    }

    private void GetThere()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            PutThere();
            return;
        }

        if (agent.pathPending)
        {
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= ArriveDistance)
        {
            Reached();
            return;
        }

        // Boxed in by something built since, or no way through: they find their own way there rather than stand against a wall all day.
        bool getting = agent.velocity.sqrMagnitude > 0.04f && agent.pathStatus == NavMeshPathStatus.PathComplete;
        if (getting)
        {
            stuckSince = -1f;
            return;
        }

        if (stuckSince < 0f)
        {
            stuckSince = Time.time;
        }
        else if (Time.time - stuckSince >= StuckSeconds)
        {
            PutThere();
        }
    }

    private void PutThere()
    {
        if (agent != null && agent.isOnNavMesh && NavMesh.SamplePosition(place, out NavMeshHit hit, 3f, agent.areaMask))
        {
            agent.Warp(hit.position);
        }
        else if (agent == null)
        {
            transform.position = place;
        }

        Reached();
    }

    private void Reached()
    {
        arrived = true;
        if (agent != null && agent.isOnNavMesh && agent.hasPath)
        {
            agent.ResetPath();
        }

        nextGestureAt = 0f;
        JobDefinition job = activity == ResidentActivity.Work ? npc.Job : null;
        if (job != null)
        {
            ShowTool(job.WorkTool, job.WorkToolScale);
        }
    }

    // Comes out when they get to work and goes away when they stop.
    private void ShowTool(ItemDefinition item, float scale)
    {
        if (item == toolItem)
        {
            return;
        }

        if (tool != null)
        {
            Destroy(tool);
        }

        toolItem = item;
        tool = null;
        Transform hand = item != null && animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null)
        {
            return;
        }

        tool = HeldItemView.CreateModel(item, hand);
        if (tool != null)
        {
            Pose grip = SettlementSettings.Current.ResidentToolGrip;
            tool.transform.SetLocalPositionAndRotation(grip.position + grip.rotation * item.HeldPose.position, grip.rotation * item.HeldPose.rotation);
            tool.transform.localScale *= scale;
        }
    }

    private void MakeWorkSound(JobDefinition job)
    {
        if (Time.time < workSoundAt)
        {
            return;
        }

        bool repeats = job.WorkGesture != null && job.WorkGesture.IsHeld;
        workSoundAt = repeats ? Time.time + Random.Range(job.WorkSoundEvery.x, job.WorkSoundEvery.y) : float.PositiveInfinity;
        AudioManager.Play(job.WorkSound, transform.position + Vector3.up);
    }

    private void GetOnWithIt()
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, TalkTurnSpeed * Time.deltaTime);
        JobDefinition job = activity == ResidentActivity.Work ? npc.Job : null;
        if (job != null && job.WorkSound != null)
        {
            MakeWorkSound(job);
        }

        if (emotes == null || Time.time < nextGestureAt)
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        EmoteDefinition gesture = activity switch
        {
            ResidentActivity.Work => npc.WorkGesture,
            ResidentActivity.Meal => settings.ResidentMealGesture,
            ResidentActivity.Sleep => settings.ResidentSleepGesture,
            ResidentActivity.Fire => Pick(settings.ResidentFireGestures),
            _ => null,
        };

        if (gesture == null)
        {
            nextGestureAt = float.PositiveInfinity;
            return;
        }

        // A pose lasts until something changes; a gesture comes round again.
        if (gesture.IsHeld)
        {
            if (emotes.Current != gesture)
            {
                emotes.Play(gesture);
                if (job != null && float.IsPositiveInfinity(workSoundAt))
                {
                    workSoundAt = Time.time + Random.Range(0.3f, job.WorkSoundEvery.y);
                }
            }

            nextGestureAt = activity == ResidentActivity.Fire ? Time.time + Random.Range(18f, 40f) : Time.time + 5f;
        }
        else if (!emotes.IsPlaying)
        {
            // Work keeps a rhythm, with a breath now and then; at the fire a gesture is only now and then.
            emotes.Play(gesture);
            if (job != null)
            {
                workSoundAt = Time.time + job.WorkSoundDelay;
            }

            nextGestureAt = activity != ResidentActivity.Work ? Time.time + Random.Range(18f, 40f)
                : Random.value < 0.2f ? Time.time + Random.Range(2f, 4f) : Time.time + Random.Range(0.1f, 0.5f);
        }
    }

    private static EmoteDefinition Pick(EmoteDefinition[] gestures) =>
        gestures != null && gestures.Length > 0 ? gestures[Random.Range(0, gestures.Length)] : null;
}
