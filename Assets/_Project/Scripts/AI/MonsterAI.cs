using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives a monster in a fight: it notices hostile targets with its senses (MonsterSenses: a sight cone blocked by
/// cover, a close sense all around, hearing through WorldNoise), goes to look at what it half-noticed, chases what it
/// spotted on the NavMesh and attacks with its definition's attack through AttackResolver, the same pipeline as the
/// player's weapon. A target it can't see for a while is lost: it searches where it last saw it, then walks home.
/// Who it fights follows FactionRelations, so Enemy monsters hunt the Player faction, Player-faction monsters hunt
/// Enemies and Neutral monsters never start or return a fight. Led too far from where it spawned, it gives up and
/// walks home. Tuning lives in the MonsterDefinition and PerceptionSettings; the current target, awareness, timers and
/// home position are this instance's own.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Combatant))]
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class MonsterAI : MonoBehaviour
{
    public enum State
    {
        Idle,
        Chasing,
        Attacking,
        Returning,
        Dead,
        /// <summary>Reeling from a broken poise or a parried attack: no moving or attacking, and it takes extra damage.</summary>
        Staggered,
        /// <summary>Going to look at something it half-noticed (a glimpse, a sound, a target it lost), then searching there.</summary>
        Investigating,
    }

    // Matches HitDetector, so the monster measures reach the same way its attack does.
    private const float CenterHeight = 1f;
    // Attacks start a little inside their range, so a target backing away is still hit.
    private const float AttackReachFactor = 0.85f;
    private const float FacingTolerance = 20f;
    // A new target has to be this much nearer before the monster switches, so it doesn't flip between two.
    private const float SwitchTargetMargin = 1.5f;
    private const float HomeTolerance = 0.6f;
    private const float NavMeshSnapDistance = 2f;
    // Close enough to the spot it went to check to start searching there.
    private const float ArriveDistance = 1.5f;
    // When a whole pack spots the player at once, the spotted sound plays once.
    private const float SpottedSoundSpacing = 0.5f;

    private static readonly Collider[] Overlaps = new Collider[64];
    private static float nextSpottedSoundAt;

    [SerializeField] private MonsterDefinition definition;
    [Tooltip("Layers searched for targets and hit by attacks. Targets still have to be damageable and hostile.")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [Tooltip("Seconds between searches for a nearer target.")]
    [SerializeField, Min(0.05f)] private float scanInterval = 0.25f;
    [Tooltip("Seconds between path updates while moving.")]
    [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
    [Tooltip("Degrees per second the monster turns toward its target while standing.")]
    [SerializeField, Min(0f)] private float turnSpeed = 540f;

    private Combatant combatant;
    private Damageable damageable;
    private NavMeshAgent agent;

    private IDamageable target;
    private Component targetComponent;
    private Collider targetCollider;

    private Vector3 home;
    private float nextScanTime;
    private float nextRepathTime;
    private float nextAttackTime;
    private float attackStartTime;
    private Vector3 attackDirection;
    private bool hitPending;
    private bool warnedOffNavMesh;
    private MonsterAttack action;
    private float[] readyAt;
    private int actionIndex = -1;
    private int shotsFired;
    private float chargeTravelled;
    private float chargeLength;
    private float exposedAt = -1f;
    private bool chargeHit;
    private bool fleeing;
    private MonsterPack pack;
    private Transform visual;
    private Vector3 visualHome;
    private Quaternion visualHomeRotation = Quaternion.identity;
    private Vector3 visualHomeScale = Vector3.one;
    private float hover;
    // The attack whose pose is playing (it keeps settling after the attack ends) and the stagger's timing.
    private AttackDefinition posedAttack;
    private float posedAttackStart;
    private float staggerStart = -1f;
    private float staggerLength;

    private bool roaming;
    private PoiseMeter poise;
    private float staggerUntil;

    private MonsterSenses senses;
    private AwarenessIndicator indicator;
    private float lastSenseTime;
    private float lastSeenTargetAt;
    private Vector3 lastKnownTargetPosition;
    private bool searchingLostTarget;
    // Investigation: walking to the spot until it gets there or gives up, then looking around until searchUntil.
    private float searchUntil = -1f;
    private float investigateGiveUpAt;
    private float lookDirection = 1f;
    // Idle glances around the way it first faced.
    private Vector3 homeForward = Vector3.forward;
    private Vector3 glanceHeading = Vector3.forward;
    private float nextGlanceAt;

    private static readonly System.Collections.Generic.List<MonsterAI> active = new System.Collections.Generic.List<MonsterAI>();
    private static readonly System.Collections.Generic.List<MonsterAI> all = new System.Collections.Generic.List<MonsterAI>();

    /// <summary>Every enabled monster, so systems (audio combat detection) don't have to search the scene.</summary>
    public static System.Collections.Generic.IReadOnlyList<MonsterAI> Active => active;

    /// <summary>Every monster that has woken at least once and still exists, including disabled (sleeping) ones.</summary>
    public static System.Collections.Generic.IReadOnlyList<MonsterAI> All => all;

    /// <summary>True while walking to a point given by <see cref="Roam"/>, watching for targets on the way.</summary>
    public bool IsRoaming => roaming && CurrentState == State.Returning;

    /// <summary>True while chasing or attacking the given target.</summary>
    public bool IsEngaging(IDamageable candidate)
    {
        return candidate != null && target == candidate
            && (CurrentState == State.Chasing || CurrentState == State.Attacking || CurrentState == State.Staggered);
    }

    /// <summary>Its poise right now (null before it has woken).</summary>
    public PoiseMeter Poise => poise;

    /// <summary>What it has noticed: its awareness and what it saw last (null without a definition).</summary>
    public MonsterSenses Senses => senses;

    /// <summary>True while it searches for a target it lost sight of, rather than checking a glimpse or a sound.</summary>
    public bool IsSearchingLostTarget => CurrentState == State.Investigating && searchingLostTarget;

    public bool IsStaggered => CurrentState == State.Staggered;

    /// <summary>Raised when it starts staggering, with how long it lasts.</summary>
    public event Action<float> Staggered;

    /// <summary>Raised when any monster starts staggering (game-wide feedback).</summary>
    public static event Action<MonsterAI, float> AnyStaggered;

    /// <summary>
    /// Makes it reel for <paramref name="seconds"/>: any attack it was winding up is cancelled (its cooldown still
    /// applies), it stands still and takes extra damage (<see cref="CombatTechniqueSettings.StaggeredDamageTaken"/>).
    /// Broken poise and parried attacks call this.
    /// </summary>
    public void Stagger(float seconds)
    {
        if (definition == null || CurrentState == State.Dead || seconds <= 0f)
        {
            return;
        }

        if (action != null && actionIndex >= 0)
        {
            readyAt[actionIndex] = Time.time + action.cooldown;
        }

        action = null;
        actionIndex = -1;
        hitPending = false;
        exposedAt = -1f;
        StopMoving();
        staggerUntil = Time.time + seconds;
        staggerStart = Time.time;
        staggerLength = seconds;
        posedAttack = null;
        damageable.DamageTakenMultiplier = CombatTechniqueSettings.Current.StaggeredDamageTaken;
        SetState(State.Staggered);
        Staggered?.Invoke(seconds);
        AnyStaggered?.Invoke(this, seconds);
    }

    private void UpdateStagger()
    {
        if (Time.time < staggerUntil)
        {
            return;
        }

        damageable.DamageTakenMultiplier = 1f;
        nextAttackTime = Time.time + 0.35f;
        SetState(State.Chasing);
    }

    public State CurrentState { get; private set; } = State.Idle;
    public MonsterDefinition Definition => definition;
    public IDamageable Target => target;
    public Vector3 Home => home;

    /// <summary>The attack being performed, or null when not attacking. Its HitDelay is the wind-up before the hit.</summary>
    public AttackDefinition CurrentAttack => CurrentState == State.Attacking && definition != null ? action?.attack ?? definition.Attack : null;
    public MonsterAttack ActiveAction => CurrentState == State.Attacking ? action : null;
    public bool IsExposed => CurrentState == State.Attacking && exposedAt >= 0f;
    public bool IsHowler => pack != null && pack.IsLeader(this);

    /// <summary>The direction the current attack was aimed when it started; it doesn't follow the target.</summary>
    public Vector3 AttackDirection => attackDirection;

    /// <summary>Horizontal movement speed in meters per second, for animation.</summary>
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

    /// <summary>Raised when the state changes. Arguments: previous state, new state.</summary>
    public event Action<State, State> StateChanged;

    /// <summary>Raised when an attack starts, before its hit lands. Animation plays here.</summary>
    public event Action AttackStarted;

    private void Awake()
    {
        all.Add(this);
        combatant = GetComponent<Combatant>();
        damageable = GetComponent<Damageable>();
        agent = GetComponent<NavMeshAgent>();
        pack = GetComponentInParent<MonsterPack>();
        visual = transform.Find("Model");
        if (visual != null)
        {
            visualHome = visual.localPosition;
            visualHomeRotation = visual.localRotation;
            visualHomeScale = visual.localScale;
        }

        if (definition == null)
        {
            Debug.LogWarning($"{name} has a MonsterAI without a MonsterDefinition, so it stays idle.", this);
            return;
        }

        agent.speed = definition.MoveSpeed;
        senses = new MonsterSenses(transform, definition, targetLayers, IsSenseCandidate);
        readyAt = new float[definition.Attacks.Count];
        CombatTechniqueSettings technique = CombatTechniqueSettings.Current;
        poise = new PoiseMeter(definition.Poise > 0f ? definition.Poise : technique.DefaultPoise,
            technique.PoiseRecoveryDelay, technique.PoiseRecoveryPerSecond);
        if (definition.Attack == null && definition.Attacks.Count == 0 && combatant.Faction != Faction.Neutral)
        {
            Debug.LogWarning($"{definition.DisplayName} has no attack, so it chases but never attacks.", this);
        }
    }

    private void Start()
    {
        ResetHome();
        lastSenseTime = Time.time;
        nextGlanceAt = Time.time + UnityEngine.Random.Range(0f, PerceptionSettings.Current.IdleGlanceSeconds.y);
        EnsureOnNavMesh();
        RefreshAmbush();
    }

    private void OnDestroy()
    {
        all.Remove(this);
    }

    /// <summary>
    /// Makes where it stands, facing the way it faces, its home (the raid map turned under it). A roaming monster keeps
    /// the place it walks to.
    /// </summary>
    public void ResetHome()
    {
        if (!roaming)
        {
            home = transform.position;
        }

        Vector3 facing = transform.forward;
        facing.y = 0f;
        homeForward = glanceHeading = facing.sqrMagnitude > 0.001f ? facing.normalized : Vector3.forward;
    }

    /// <summary>
    /// Walks to a point and makes it the new home, attacking anything it notices on the way (roaming spawns). Getting
    /// a target ends the roam: the place it was found becomes home, so the normal chase and leash rules apply.
    /// </summary>
    public void Roam(Vector3 destination)
    {
        if (definition == null || CurrentState == State.Dead || CurrentState == State.Attacking)
        {
            return;
        }

        ClearTarget();
        home = destination;
        roaming = true;
        BeginReturn();
        ApplySpeed();
    }

    private void OnEnable()
    {
        active.Add(this);
        damageable.Damaged += OnDamaged;
        damageable.Died += OnDied;
    }

    private void OnDisable()
    {
        active.Remove(this);
        damageable.Damaged -= OnDamaged;
        damageable.Died -= OnDied;
    }

    private void Update()
    {
        if (CurrentState == State.Dead || definition == null)
        {
            return;
        }

        poise?.Tick(Time.time, Time.deltaTime);
        if (CurrentState == State.Staggered)
        {
            UpdateStagger();
            return;
        }

        if (CurrentState == State.Attacking)
        {
            UpdateAttack();
            return;
        }

        if (CurrentState == State.Investigating)
        {
            UpdateInvestigation();
            return;
        }

        if (CurrentState == State.Returning)
        {
            if (!roaming)
            {
                UpdateReturn();
                return;
            }

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + scanInterval;
                if (definition.HasTemperament(MonsterTemperament.Aggressive))
                {
                    Sense();
                }
            }
            if (CurrentState == State.Investigating)
            {
                return;
            }
            if (!CanTarget(target))
            {
                UpdateReturn();
                return;
            }

            roaming = false;
            home = transform.position;
        }

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            if (IsWatchful())
            {
                Sense();
            }
        }

        if (CurrentState == State.Investigating)
        {
            return;
        }

        if (!CanTarget(target))
        {
            ClearTarget();
            if (senses.Awareness.IsAlerted)
            {
                senses.Awareness.Calm();
                RefreshAmbush();
            }
            GoHomeOrIdle();
            if (CurrentState == State.Idle)
            {
                UpdateIdleGaze();
            }
            return;
        }

        if (!Relentless && (DistanceToTarget() > definition.ChaseRange || IsAvoided(targetComponent.transform.position)))
        {
            LoseTrack();
            return;
        }

        if (!Relentless && IsBeyondLeash())
        {
            ClearTarget();
            BeginReturn();
            return;
        }

        Engage();
    }

    // Monsters that start fights look and listen for targets; retaliation-only ones only react to being hit.
    private bool IsWatchful()
    {
        return definition.HasTemperament(MonsterTemperament.Aggressive)
            || definition.HasTemperament(MonsterTemperament.Defensive);
    }

    /// <summary>
    /// A sound of <paramref name="radius"/> metres at <paramref name="position"/> (WorldNoise). An idle, roaming or
    /// investigating monster that hears it grows suspicious and comes to look; one that hunts, fights or walks home
    /// ignores it. A sound alone never makes it spot anyone.
    /// </summary>
    public void Hear(Vector3 position, float radius)
    {
        if (!CanInvestigate())
        {
            return;
        }

        float level = PerceptionRules.HeardLevel(HorizontalDistance(position), radius, definition.Hearing, PerceptionSettings.Current);
        if (level <= 0f)
        {
            return;
        }

        senses.Awareness.Hear(level, position);
        if (senses.Stage != AwarenessStage.Unaware)
        {
            BeginInvestigate(false);
        }
    }

    /// <summary>Sends it to look at a position as if it had heard something loud there (an angry forest).</summary>
    public void Investigate(Vector3 position)
    {
        if (!CanInvestigate())
        {
            return;
        }

        senses.Awareness.Hear(PerceptionSettings.Current.HeardAwarenessNear, position);
        BeginInvestigate(false);
    }

    private bool CanInvestigate()
    {
        if (definition == null || senses == null || !isActiveAndEnabled || !IsWatchful())
        {
            return false;
        }

        return CurrentState == State.Idle || CurrentState == State.Investigating
            || (CurrentState == State.Returning && roaming);
    }

    // Looks for targets: while hunting one, keeps track of it; otherwise fills awareness and, once suspicious, goes to look.
    private void Sense()
    {
        float elapsed = Mathf.Min(Time.time - lastSenseTime, 1f);
        lastSenseTime = Time.time;
        if (CanTarget(target))
        {
            TrackTarget();
            return;
        }

        if (senses.Look(elapsed))
        {
            SetTarget(senses.Spotted, senses.SpottedCollider);
            return;
        }

        if (senses.Stage == AwarenessStage.Suspicious && CurrentState != State.Investigating && CanInvestigate())
        {
            BeginInvestigate(false);
        }

        RefreshAmbush();
    }

    private void TrackTarget()
    {
        if (senses.CanSee(targetComponent.transform, definition.ChaseRange))
        {
            lastSeenTargetAt = Time.time;
            lastKnownTargetPosition = targetComponent.transform.position;
            senses.Awareness.Alert(lastKnownTargetPosition);
            ScanForNearerTarget();
            return;
        }

        if (!Relentless && Time.time - lastSeenTargetAt > PerceptionSettings.Current.LoseTrackSeconds)
        {
            LoseTrack();
        }
    }

    // Out of sight for too long or out of chase range: it goes to where it last saw the target and searches there.
    private void LoseTrack()
    {
        ClearTarget();
        senses.Awareness.LoseTrack(lastKnownTargetPosition, PerceptionSettings.Current.LostTrackAwareness);
        BeginInvestigate(true);
    }

    private void BeginInvestigate(bool lostTarget)
    {
        bool fresh = CurrentState != State.Investigating;
        searchingLostTarget = lostTarget || (!fresh && searchingLostTarget);
        searchUntil = -1f;
        investigateGiveUpAt = Time.time + PerceptionSettings.Current.MaxInvestigateSeconds;
        nextRepathTime = 0f;
        if (fresh && !lostTarget)
        {
            AudioManager.Play(PerceptionSettings.Current.SuspiciousSound, transform.position);
        }

        EnsureIndicator();
        SetState(State.Investigating);
        RefreshAmbush();
    }

    private void UpdateInvestigation()
    {
        if (CanTarget(target))
        {
            // Spotted it, was hit or was told by its pack: the hunt starts where it stands.
            if (roaming)
            {
                roaming = false;
                home = transform.position;
            }
            searchingLostTarget = false;
            SetState(State.Chasing);
            return;
        }

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            Sense();
            if (CurrentState != State.Investigating || CanTarget(target))
            {
                return;
            }
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        if (senses.IsSeeingSomething)
        {
            // Another glimpse: go there instead of searching here.
            searchUntil = -1f;
        }

        if (searchUntil < 0f)
        {
            Vector3 spot = InvestigationSpot(senses.Awareness.Stimulus);
            if (HorizontalDistance(spot) > ArriveDistance && Time.time < investigateGiveUpAt && MoveTo(spot))
            {
                return;
            }

            StopMoving();
            searchUntil = Time.time + rules.SearchSeconds;
            lookDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        transform.Rotate(0f, lookDirection * rules.LookAroundSpeed * Time.deltaTime, 0f);
        if (Time.time >= searchUntil)
        {
            EndInvestigation();
        }
    }

    // Where to look, kept inside its leash (and defensive radius), so checking a noise never drags it off for good.
    private Vector3 InvestigationSpot(Vector3 stimulus)
    {
        float limit = definition.LeashRange;
        if (definition.HasTemperament(MonsterTemperament.Defensive))
        {
            limit = limit > 0f ? Mathf.Min(limit, definition.DefensiveRadius) : definition.DefensiveRadius;
        }

        if (IsAvoided(stimulus))
        {
            return transform.position;
        }

        Vector3 offset = stimulus - home;
        offset.y = 0f;
        if (roaming || Relentless || limit <= 0f || offset.magnitude <= limit)
        {
            return stimulus;
        }

        return home + offset.normalized * (limit * 0.9f);
    }

    private void EndInvestigation()
    {
        searchingLostTarget = false;
        BeginReturn();
    }

    // Idle monsters glance around the way they first faced, so a sneaking player has to watch them.
    private void UpdateIdleGaze()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        if (Time.time >= nextGlanceAt)
        {
            Vector2 seconds = rules.IdleGlanceSeconds;
            nextGlanceAt = Time.time + UnityEngine.Random.Range(seconds.x, Mathf.Max(seconds.x, seconds.y));
            glanceHeading = Quaternion.Euler(0f, UnityEngine.Random.Range(-rules.IdleGlanceAngle, rules.IdleGlanceAngle), 0f) * homeForward;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(glanceHeading),
            rules.LookAroundSpeed * Time.deltaTime);
    }

    // Unaware monsters take extra damage, a hit from the shadows (PerceptionSettings › Ambush).
    private void RefreshAmbush()
    {
        if (senses == null || CurrentState == State.Staggered || CurrentState == State.Dead)
        {
            return;
        }

        bool unaware = !CanTarget(target) && senses.Stage == AwarenessStage.Unaware;
        damageable.DamageTakenMultiplier = unaware ? PerceptionSettings.Current.AmbushDamageTaken : 1f;
    }

    private AwarenessIndicator EnsureIndicator()
    {
        if (indicator == null && PerceptionSettings.Current.ShowIndicators)
        {
            indicator = AwarenessIndicator.CreateFor(this);
        }

        return indicator;
    }

    // It has just spotted (or been set on) a target: the "!", a sound when it's the player, and a call that sends idle
    // monsters nearby to look.
    private void OnSpotted()
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        EnsureIndicator()?.ShowSpotted();
        if (global::Player.IsPlayer(targetComponent.gameObject) && Time.time >= nextSpottedSoundAt)
        {
            nextSpottedSoundAt = Time.time + SpottedSoundSpacing;
            AudioManager.Play(rules.SpottedSound, transform.position);
        }

        WorldNoise.Emit(transform.position, rules.SpottedCallRadius);
    }

    // The body language that makes attacks readable from above (AttackPose): coiling through the wind-up, snapping into
    // the hit, swaying while staggered; plus the hover of flying monsters.
    private void LateUpdate()
    {
        if (visual == null || definition == null)
        {
            return;
        }

        float targetHover = definition.HoverHeight <= 0f || IsExposed || CurrentState == State.Dead ? 0f
            : definition.HoverHeight + Mathf.Sin(Time.time * 3f) * 0.08f;
        hover = Mathf.Lerp(hover, targetHover, Time.deltaTime * 10f);

        AttackPose.Pose pose = AttackPose.Pose.Rest;
        if (CurrentState == State.Dead)
        {
            posedAttack = null;
        }
        else if (staggerStart >= 0f && Time.time - staggerStart < staggerLength)
        {
            pose = AttackPose.Stagger(Time.time - staggerStart, staggerLength);
        }
        else if (posedAttack != null)
        {
            float elapsed = Time.time - posedAttackStart;
            if (elapsed > posedAttack.HitDelay + AttackPose.StrikeSeconds + AttackPose.SettleSeconds)
            {
                posedAttack = null;
            }
            else
            {
                // Charges move the whole body already; they only coil.
                bool charge = action != null && action.kind == MonsterAttackKind.Charge && elapsed >= posedAttack.HitDelay;
                pose = charge ? AttackPose.Pose.Rest : AttackPose.Attack(elapsed, posedAttack.HitDelay);
            }
        }

        float width = 1f / Mathf.Sqrt(Mathf.Max(0.5f, pose.Height));
        visual.localPosition = visualHome + Vector3.up * hover + Vector3.forward * pose.Shift;
        visual.localRotation = Quaternion.Euler(-pose.Lean, 0f, pose.Sway) * visualHomeRotation;
        visual.localScale = Vector3.Scale(visualHomeScale, new Vector3(width, pose.Height, width));
    }

    private void Engage()
    {
        Vector3 toTarget = TargetPoint() - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (!Relentless && definition.HasTemperament(MonsterTemperament.Defensive)
            && (HorizontalDistance(home) > definition.DefensiveRadius
                || Vector3.Distance(home, targetComponent.transform.position) > definition.DefensiveRadius))
        {
            ClearTarget();
            BeginReturn();
            return;
        }
        if (definition.HasTemperament(MonsterTemperament.Cowardly))
        {
            fleeing = damageable.HealthFraction <= definition.FleeHealthFraction
                && distance < definition.FleeDistance + (fleeing ? definition.DistanceTolerance : -definition.DistanceTolerance);
            if (fleeing)
            {
                SetState(State.Chasing);
                Retreat(toTarget, definition.FleeDistance - distance);
                return;
            }
        }

        if (definition.Attacks.Count > 0)
        {
            int chosen = SelectAttack(definition.Attacks, readyAt, distance, Time.time, IsHowler);
            if (chosen >= 0 && Time.time >= nextAttackTime)
            {
                StopMoving();
                SetState(State.Chasing);
                if (TurnTowards(toTarget) && (pack == null || pack.TryAttack()))
                {
                    actionIndex = chosen;
                    action = definition.Attacks[chosen];
                    BeginAttack(toTarget);
                }
                return;
            }
            SetState(State.Chasing);
            if (definition.MovementTactic == MonsterMovementTactic.KeepDistance)
            {
                if (distance < definition.PreferredDistance - definition.DistanceTolerance)
                {
                    Retreat(toTarget, definition.PreferredDistance - distance);
                }
                else if (distance > definition.PreferredDistance + definition.DistanceTolerance)
                {
                    MoveTo(targetComponent.transform.position);
                }
                else
                {
                    StopMoving();
                    TurnTowards(toTarget);
                }
            }
            else
            {
                bool inAttackRange = false;
                foreach (MonsterAttack candidate in definition.Attacks)
                {
                    if (candidate != null && candidate.attack != null && candidate.kind != MonsterAttackKind.Howl
                        && distance >= candidate.minimumRange && distance <= candidate.maximumRange)
                    {
                        inAttackRange = true;
                        break;
                    }
                }
                if (inAttackRange)
                {
                    StopMoving();
                    TurnTowards(toTarget);
                }
                else
                {
                    MoveTo(targetComponent.transform.position);
                }
            }
            return;
        }
        AttackDefinition attack = definition.Attack;

        if (attack != null && toTarget.magnitude <= attack.Range * AttackReachFactor)
        {
            StopMoving();
            SetState(State.Chasing);
            bool facing = TurnTowards(toTarget);
            if (facing && Time.time >= nextAttackTime && (pack == null || pack.TryAttack()))
            {
                BeginAttack(toTarget);
            }

            return;
        }

        SetState(State.Chasing);
        MoveTo(targetComponent.transform.position);
    }

    private void BeginAttack(Vector3 direction)
    {
        // The swing is aimed when it starts, so a target can still dodge out of it.
        attackDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        attackStartTime = Time.time;
        hitPending = true;
        shotsFired = 0;
        chargeTravelled = 0f;
        chargeLength = action != null ? Mathf.Min(action.chargeDistance, direction.magnitude + 0.7f) : 0f;
        exposedAt = -1f;
        chargeHit = false;
        SetState(State.Attacking);
        posedAttack = CurrentAttack;
        posedAttackStart = attackStartTime;
        AttackStarted?.Invoke();
    }

    private void UpdateAttack()
    {
        AttackDefinition attack = CurrentAttack;
        if (attack == null)
        {
            SetState(State.Chasing);
            return;
        }

        TurnTowards(attackDirection);
        float elapsed = Time.time - attackStartTime;

        if (action != null)
        {
            UpdateAction(attack, elapsed);
            return;
        }

        if (hitPending && elapsed >= attack.HitDelay)
        {
            hitPending = false;
            AttackResolver.Execute(combatant, attack, definition.AttackDamage, transform.position, attackDirection, targetLayers);
        }

        if (elapsed >= attack.RecoveryTime)
        {
            nextAttackTime = Time.time + definition.AttackCooldown;
            SetState(State.Chasing);
        }
    }

    public static int SelectAttack(System.Collections.Generic.IReadOnlyList<MonsterAttack> attacks,
        float[] cooldowns, float distance, float now, bool isLeader)
    {
        int chosen = -1;
        for (int i = 0; i < attacks.Count; i++)
        {
            MonsterAttack candidate = attacks[i];
            if (candidate != null && candidate.IsAvailable(distance, now, cooldowns[i], isLeader)
                && (chosen < 0 || candidate.priority > attacks[chosen].priority))
            {
                chosen = i;
            }
        }
        return chosen;
    }

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        Vector3 destination = transform.position + away * Mathf.Max(2f, distance);
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, agent.areaMask)
            && (Relentless || definition.LeashRange <= 0f || Vector3.Distance(home, hit.position) <= definition.LeashRange))
        {
            MoveTo(hit.position);
        }
        else
        {
            StopMoving();
        }
    }

    private void UpdateAction(AttackDefinition attack, float elapsed)
    {
        if (elapsed < attack.HitDelay)
        {
            return;
        }
        switch (action.kind)
        {
            case MonsterAttackKind.Projectile:
                if (shotsFired < action.burstCount && elapsed >= attack.HitDelay + shotsFired * action.burstInterval)
                {
                    FireProjectile();
                    shotsFired++;
                }
                if (shotsFired >= action.burstCount
                    && elapsed >= Mathf.Max(attack.RecoveryTime, attack.HitDelay + (action.burstCount - 1) * action.burstInterval + 0.5f))
                {
                    FinishAction();
                }
                break;
            case MonsterAttackKind.Charge:
                UpdateCharge(attack);
                break;
            case MonsterAttackKind.Howl:
                if (hitPending)
                {
                    hitPending = false;
                    ApplyHowl();
                }
                if (elapsed >= attack.RecoveryTime)
                {
                    FinishAction();
                }
                break;
            default:
                if (hitPending)
                {
                    hitPending = false;
                    AttackResolver.Execute(combatant, attack, action.damage, transform.position, attackDirection, targetLayers);
                }
                if (elapsed >= attack.RecoveryTime)
                {
                    FinishAction();
                }
                break;
        }
    }

    private void UpdateCharge(AttackDefinition attack)
    {
        if (exposedAt >= 0f)
        {
            if (Time.time - exposedAt >= action.exposedSeconds)
            {
                FinishAction();
            }
            return;
        }
        if (!EnsureOnNavMesh())
        {
            exposedAt = Time.time;
            return;
        }
        float step = Mathf.Min(action.chargeSpeed * Time.deltaTime, chargeLength - chargeTravelled);
        Vector3 before = transform.position;
        Vector3 destination = before + attackDirection * step;
        bool blocked = agent.Raycast(destination, out NavMeshHit wall);
        agent.Move(blocked ? wall.position - before : attackDirection * step);
        chargeTravelled += step;
        if (!chargeHit)
        {
            chargeHit = AttackResolver.Execute(combatant, attack, action.damage,
                transform.position, attackDirection, targetLayers) > 0;
        }
        if (blocked || chargeTravelled >= chargeLength || chargeHit)
        {
            exposedAt = Time.time;
            StopMoving();
        }
    }

    private void ApplyHowl()
    {
        foreach (MonsterAI other in active)
        {
            if (other.CurrentState == State.Dead || other.combatant.Faction != combatant.Faction
                || Vector3.Distance(transform.position, other.transform.position) > action.buffRadius)
            {
                continue;
            }
            MonsterHowlBuff buff = other.GetComponent<MonsterHowlBuff>();
            if (buff == null)
            {
                buff = other.gameObject.AddComponent<MonsterHowlBuff>();
            }
            buff.Refresh(action.damageBonus, action.buffSeconds);
        }
        GameMessages.Post($"{definition.DisplayName} howls! Nearby monsters gain +{action.damageBonus:P0} damage.", MessageKind.Warning);
    }

    private void FireProjectile()
    {
        if (action.projectilePrefab == null)
        {
            Debug.LogError($"{definition.DisplayName}: projectile action has no projectile prefab.", this);
            return;
        }
        Vector3 origin = transform.position + Vector3.up;
        Vector3 aim = CanTarget(target) ? targetComponent.transform.position + Vector3.up - origin : attackDirection;
        GameObject projectile = PrefabPool.Spawn(action.projectilePrefab, origin + aim.normalized * 0.7f, Quaternion.LookRotation(aim));
        MonsterProjectile flight = projectile.GetComponent<MonsterProjectile>();
        if (flight == null)
        {
            Debug.LogError($"{projectile.name} has no MonsterProjectile component.", projectile);
            PrefabPool.Release(projectile);
            return;
        }
        flight.Initialize(combatant, action, aim);
    }

    private void FinishAction()
    {
        readyAt[actionIndex] = Time.time + action.cooldown;
        nextAttackTime = Time.time + 0.15f;
        action = null;
        actionIndex = -1;
        exposedAt = -1f;
        SetState(State.Chasing);
    }

    private void UpdateReturn()
    {
        if (HorizontalDistance(home) <= HomeTolerance || !MoveTo(home))
        {
            roaming = false;
            StopMoving();
            SetState(State.Idle);
        }
    }

    private void GoHomeOrIdle()
    {
        if (definition.LeashRange > 0f && HorizontalDistance(home) > HomeTolerance)
        {
            BeginReturn();
            return;
        }

        StopMoving();
        SetState(State.Idle);
    }

    private void BeginReturn()
    {
        nextRepathTime = 0f;
        senses?.Awareness.Calm();
        SetState(State.Returning);
        RefreshAmbush();
    }

    private bool IsBeyondLeash()
    {
        return definition.LeashRange > 0f && HorizontalDistance(home) > definition.LeashRange;
    }

    // While hunting one target, switches to a clearly nearer one it can see (a traveller between it and the player).
    private void ScanForNearerTarget()
    {
        Vector3 center = transform.position + Vector3.up * CenterHeight;
        int count = Physics.OverlapSphereNonAlloc(center, definition.DetectionRange, Overlaps, targetLayers, QueryTriggerInteraction.Ignore);

        IDamageable best = null;
        Collider bestCollider = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            IDamageable candidate = Overlaps[i].GetComponentInParent<IDamageable>();
            if (ReferenceEquals(candidate, target) || !IsSenseCandidate(candidate, Overlaps[i]))
            {
                continue;
            }

            float distance = HorizontalDistance(HitDetector.NearestPoint(Overlaps[i], center));
            if (distance < bestDistance && senses.CanSee(((Component)candidate).transform, definition.DetectionRange))
            {
                best = candidate;
                bestCollider = Overlaps[i];
                bestDistance = distance;
            }
        }

        if (best == null || ReferenceEquals(best, target))
        {
            return;
        }

        if (CanTarget(target) && bestDistance > DistanceToTarget() - SwitchTargetMargin)
        {
            return;
        }

        SetTarget(best, bestCollider);
    }

    // A target its senses may notice: one it would fight, and for a defensive monster, one inside its ground.
    private bool IsSenseCandidate(IDamageable candidate, Collider candidateCollider)
    {
        if (!CanTarget(candidate))
        {
            return false;
        }

        if (IsAvoided(candidateCollider.transform.position))
        {
            return false;
        }

        return Relentless || !definition.HasTemperament(MonsterTemperament.Defensive)
            || Vector3.Distance(home, candidateCollider.transform.position) <= definition.DefensiveRadius;
    }

    private bool IsAvoided(Vector3 position) => AvoidArea != null && AvoidArea(position);

    private bool CanTarget(IDamageable candidate)
    {
        var component = candidate as Component;
        if (component == null || !component.gameObject.activeInHierarchy || ReferenceEquals(candidate, damageable))
        {
            return false;
        }

        if (PlayerOnly && !global::Player.IsPlayer(component.gameObject))
        {
            return false;
        }

        return candidate.IsAlive && combatant.Faction.WillAttack(candidate.Faction);
    }

    /// <summary>
    /// When set, the monster only targets the player, leaving buildings and residents alone (ward-breach attackers,
    /// design decision for the first breach).
    /// </summary>
    public bool PlayerOnly { get; set; }

    /// <summary>When set, the monster never gives up a chase or returns home (ward-breach attackers crossing the clearing).</summary>
    public bool Relentless { get; set; }

    /// <summary>
    /// Ground it will not enter (night prowlers: light). It neither notices targets standing there nor follows them in:
    /// a hunt that leads into it is lost at the edge, and anything heard there is searched for from where it stands.
    /// </summary>
    public System.Predicate<Vector3> AvoidArea { get; set; }

    /// <summary>When set, CreatureSleep never puts it to sleep, however far the player is (a patrolling apex).</summary>
    public bool KeepAwake { get; set; }

    /// <summary>Movement speed while roaming (<see cref="Roam"/>), as a share of its own: patrols walk, hunts run.</summary>
    public float RoamSpeedScale
    {
        get => roamSpeedScale;
        set
        {
            roamSpeedScale = Mathf.Max(0.05f, value);
            ApplySpeed();
        }
    }

    private float roamSpeedScale = 1f;

    /// <summary>Starts chasing a target right away, wherever it is. Returns false when it can't target it.</summary>
    public bool Engage(IDamageable newTarget)
    {
        if (definition == null || CurrentState == State.Dead || !CanTarget(newTarget))
        {
            return false;
        }

        SetTarget(newTarget, null);
        return true;
    }

    private void SetTarget(IDamageable newTarget, Collider hitCollider)
    {
        target = newTarget;
        targetComponent = (Component)newTarget;
        targetCollider = hitCollider != null ? hitCollider : targetComponent.GetComponentInChildren<Collider>();
        nextRepathTime = 0f;
        lastSeenTargetAt = Time.time;
        lastKnownTargetPosition = targetComponent.transform.position;
        searchingLostTarget = false;
        if (senses != null)
        {
            bool fresh = !senses.Awareness.IsAlerted;
            senses.Awareness.Alert(lastKnownTargetPosition);
            if (fresh)
            {
                OnSpotted();
            }
        }

        RefreshAmbush();
        if (pack != null)
        {
            pack.Alert(newTarget);
        }
    }

    private void ClearTarget()
    {
        target = null;
        targetComponent = null;
        targetCollider = null;
    }

    private Vector3 TargetPoint()
    {
        if (targetCollider != null && targetCollider.enabled)
        {
            return HitDetector.NearestPoint(targetCollider, transform.position + Vector3.up * CenterHeight);
        }

        return targetComponent.transform.position;
    }

    private float DistanceToTarget()
    {
        return targetComponent != null ? HorizontalDistance(TargetPoint()) : float.MaxValue;
    }

    private float HorizontalDistance(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private bool MoveTo(Vector3 destination)
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
        // Re-requesting the same spot while the last search is still running would restart it and, on a long path,
        // keep it from ever finishing.
        if (agent.pathPending && (agent.destination - destination).sqrMagnitude < 0.25f)
        {
            return true;
        }

        return agent.SetDestination(destination);
    }

    private void StopMoving()
    {
        agent.updateRotation = false;
        nextRepathTime = 0f;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh && (agent.hasPath || agent.pathPending))
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    private bool EnsureOnNavMesh()
    {
        if (!agent.isActiveAndEnabled)
        {
            return false;
        }

        if (agent.isOnNavMesh)
        {
            return true;
        }

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavMeshSnapDistance, agent.areaMask)
            && agent.Warp(hit.position))
        {
            return true;
        }

        if (!warnedOffNavMesh)
        {
            warnedOffNavMesh = true;
            Debug.LogWarning($"{name} is not on a NavMesh, so it can't move. Add a NavMeshSurface to the scene and bake it.", this);
        }

        return false;
    }

    private bool TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Quaternion desired = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, desired) <= FacingTolerance;
    }

    // Being hit by something it would fight makes the monster fight back, even from outside its detection range.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (CurrentState == State.Attacking && action != null && action.interruptible
            && Time.time - attackStartTime < action.attack.HitDelay && result.HealthLost > 0f)
        {
            posedAttack = null;
            FinishAction();
        }

        if (poise != null && CurrentState != State.Staggered && CurrentState != State.Dead && !result.Killed
            && poise.Hit(damage.Poise, Time.time))
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }

        if (definition == null || CurrentState == State.Dead || (CurrentState == State.Returning && !roaming) || CanTarget(target))
        {
            return;
        }

        Combatant attacker = damage.Source;
        if (attacker == null)
        {
            return;
        }

        IDamageable attackerTarget = attacker.GetComponent<IDamageable>();
        if (CanTarget(attackerTarget))
        {
            SetTarget(attackerTarget, null);
        }
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        damageable.DamageTakenMultiplier = 1f;
        StopMoving();
        ClearTarget();
        agent.enabled = false;
        SetState(State.Dead);
    }

    private void SetState(State next)
    {
        if (CurrentState == next)
        {
            return;
        }

        State previous = CurrentState;
        CurrentState = next;
        ApplySpeed();
        StateChanged?.Invoke(previous, next);
    }

    // Going to look is a cautious walk and a patrol a stroll; everything else moves at full speed.
    private void ApplySpeed()
    {
        if (definition == null || agent == null)
        {
            return;
        }

        float scale = CurrentState == State.Investigating ? PerceptionSettings.Current.InvestigateSpeedScale
            : CurrentState == State.Returning && roaming ? roamSpeedScale
            : 1f;
        agent.speed = definition.MoveSpeed * scale;
    }

    // Entering play mode doesn't reload scripts in this project, so the shared sound timer is reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        nextSpottedSoundAt = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (definition == null)
        {
            return;
        }

        Vector3 origin = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        DrawSightCone(definition.DetectionRange, definition.SightAngle);
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, definition.CloseSenseRange);
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, definition.ChaseRange);

        if (definition.LeashRange > 0f)
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(origin, definition.LeashRange);
        }
    }

    private void DrawSightCone(float range, float angle)
    {
        const int segments = 24;
        Vector3 center = transform.position + Vector3.up * 0.1f;
        float half = Mathf.Clamp(angle, 0f, 360f) * 0.5f;
        Vector3 previous = center + Quaternion.Euler(0f, -half, 0f) * transform.forward * range;
        if (half < 180f)
        {
            Gizmos.DrawLine(center, previous);
        }

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = center + Quaternion.Euler(0f, Mathf.Lerp(-half, half, i / (float)segments), 0f) * transform.forward * range;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        if (half < 180f)
        {
            Gizmos.DrawLine(center, previous);
        }
    }
}
