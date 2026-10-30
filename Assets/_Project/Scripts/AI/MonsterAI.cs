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
/// <para>
/// Its parts: MonsterSenses (what it notices), MonsterTargeting (whom it may hunt), MonsterMovement (the NavMesh),
/// MonsterAttacks (carrying out a strike) and MonsterPose (body language). This class is the state machine that decides
/// between them: idle, investigating, chasing, attacking, staggered, returning, dead.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Combatant))]
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class MonsterAI : MonoBehaviour, IEventSubject, IStaggerable
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
    internal const float CenterHeight = 1f;
    // Attacks start a little inside their range, so a target backing away is still hit.
    private const float AttackReachFactor = 0.85f;
    private const float HomeTolerance = 0.6f;
    // Close enough to the spot it went to check to start searching there.
    private const float ArriveDistance = 1.5f;
    // When a whole pack spots the player at once, the spotted sound plays once.
    private const float SpottedSoundSpacing = 0.5f;

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
    private MonsterMovement movement;
    private MonsterAttacks attacks;
    private MonsterPose pose;

    private IDamageable target;
    private Component targetComponent;
    private Collider targetCollider;

    private Vector3 home;
    private float nextScanTime;
    private bool fleeing;
    private MonsterPack pack;

    private bool roaming;
    private PoiseMeter poise;
    private float staggerUntil;

    private MonsterSenses senses;
    private readonly MonsterTargeting targeting = new MonsterTargeting();
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

    /// <summary>True while any monster chases or attacks the player or someone on their side: a fight is on.</summary>
    public static bool AnyHuntingPlayerSide()
    {
        foreach (MonsterAI monster in active)
        {
            if (monster.targetComponent != null && monster.IsEngaging(monster.target) && monster.target.Faction.IsPlayerSide())
            {
                return true;
            }
        }

        return false;
    }

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

        attacks.Cancel();
        movement.Stop();
        staggerUntil = Time.time + seconds;
        pose?.Stagger(Time.time, seconds);
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
        attacks.Delay(0.35f);
        SetState(State.Chasing);
    }

    public State CurrentState { get; private set; } = State.Idle;
    public MonsterDefinition Definition => definition;
    UnityEngine.Object IEventSubject.EventSubject => definition;
    public IDamageable Target => target;
    public Vector3 Home => home;

    /// <summary>The attack being performed, or null when not attacking. Its HitDelay is the wind-up before the hit.</summary>
    public AttackDefinition CurrentAttack => CurrentState == State.Attacking ? attacks.Attack : null;
    public MonsterAttack ActiveAction => CurrentState == State.Attacking ? attacks.Action : null;
    public bool IsExposed => CurrentState == State.Attacking && attacks.IsExposed;
    public bool IsHowler => pack != null && pack.IsLeader(this);

    /// <summary>The direction the current attack was aimed when it started; it doesn't follow the target.</summary>
    public Vector3 AttackDirection => attacks != null ? attacks.Direction : Vector3.zero;

    /// <summary>Horizontal movement speed in meters per second, for animation.</summary>
    public float Speed => movement != null ? movement.Speed : 0f;

    /// <summary>Raised when the state changes. Arguments: previous state, new state.</summary>
    public event Action<State, State> StateChanged;

    /// <summary>Raised when an attack starts, before its hit lands. Animation plays here.</summary>
    public event Action AttackStarted;

    private void Awake()
    {
        all.Add(this);
        combatant = GetComponent<Combatant>();
        damageable = GetComponent<Damageable>();
        var agent = GetComponent<NavMeshAgent>();
        movement = new MonsterMovement(transform, agent, repathInterval, turnSpeed);
        pack = GetComponentInParent<MonsterPack>();
        targeting.Bind(transform, definition, combatant, damageable, agent, targetLayers);
        Transform visual = transform.Find("Model");
        pose = visual != null ? new MonsterPose(visual) : null;

        if (definition == null)
        {
            Debug.LogWarning($"{name} has a MonsterAI without a MonsterDefinition, so it stays idle.", this);
            return;
        }

        movement.SetSpeed(definition.MoveSpeed);
        senses = new MonsterSenses(transform, definition, targetLayers,
            (candidate, collider) => targeting.IsSenseCandidate(candidate, collider, home, Relentless));
        attacks = new MonsterAttacks(transform, definition, combatant, movement, targetLayers);
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
        movement.EnsureOnNavMesh();
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
        movement.RepathNow();
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
            if (HorizontalDistance(spot) > ArriveDistance && Time.time < investigateGiveUpAt && movement.MoveTo(spot))
            {
                return;
            }

            movement.Stop();
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
        AwarenessIndicator spotted = EnsureIndicator();
        if (spotted != null)
        {
            spotted.ShowSpotted();
        }
        if (global::Player.IsPlayer(targetComponent.gameObject) && Time.time >= nextSpottedSoundAt)
        {
            nextSpottedSoundAt = Time.time + SpottedSoundSpacing;
            AudioManager.Play(rules.SpottedSound, transform.position);
        }

        WorldNoise.Emit(transform.position, rules.SpottedCallRadius);
    }

    // Body language (MonsterPose) after movement, so it shows this frame's attack and stagger.
    private void LateUpdate()
    {
        if (pose == null || definition == null)
        {
            return;
        }

        bool dead = CurrentState == State.Dead;
        float hoverHeight = IsExposed || dead ? 0f : definition.HoverHeight;
        MonsterAttack current = attacks.Action;
        pose.Update(hoverHeight, dead, current != null && current.kind == MonsterAttackKind.Charge);
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
            int chosen = attacks.ChooseAction(distance, IsHowler);
            if (chosen >= 0 && attacks.IsReady)
            {
                movement.Stop();
                SetState(State.Chasing);
                if (movement.TurnTowards(toTarget) && (pack == null || pack.TryAttack()))
                {
                    BeginAttack(chosen, toTarget);
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
                    movement.MoveTo(targetComponent.transform.position);
                }
                else
                {
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }
            }
            else
            {
                if (attacks.AnyStrikeReaches(distance))
                {
                    movement.Stop();
                    movement.TurnTowards(toTarget);
                }
                else
                {
                    movement.MoveTo(targetComponent.transform.position);
                }
            }
            return;
        }
        AttackDefinition attack = definition.Attack;

        if (attack != null && toTarget.magnitude <= attack.Range * AttackReachFactor)
        {
            movement.Stop();
            SetState(State.Chasing);
            bool facing = movement.TurnTowards(toTarget);
            if (facing && attacks.IsReady && (pack == null || pack.TryAttack()))
            {
                BeginAttack(-1, toTarget);
            }

            return;
        }

        SetState(State.Chasing);
        movement.MoveTo(targetComponent.transform.position);
    }

    // Action index, or -1 for the definition's plain attack.
    private void BeginAttack(int action, Vector3 toTarget)
    {
        attacks.Begin(action, toTarget);
        SetState(State.Attacking);
        pose?.Attack(CurrentAttack, Time.time);
        AttackStarted?.Invoke();
    }

    private void UpdateAttack()
    {
        if (!attacks.Update(CanTarget(target) ? targetComponent.transform.position : (Vector3?)null))
        {
            SetState(State.Chasing);
        }
    }

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        Vector3 destination = transform.position + away * Mathf.Max(2f, distance);
        if (movement.TryFindGround(destination, 2f, out Vector3 ground)
            && (Relentless || definition.LeashRange <= 0f || Vector3.Distance(home, ground) <= definition.LeashRange))
        {
            movement.MoveTo(ground);
        }
        else
        {
            movement.Stop();
        }
    }

    private void UpdateReturn()
    {
        if (HorizontalDistance(home) <= HomeTolerance || !movement.MoveTo(home))
        {
            roaming = false;
            movement.Stop();
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

        movement.Stop();
        SetState(State.Idle);
    }

    private void BeginReturn()
    {
        movement.RepathNow();
        senses?.Awareness.Calm();
        SetState(State.Returning);
        RefreshAmbush();
    }

    private bool IsBeyondLeash()
    {
        return definition.LeashRange > 0f && HorizontalDistance(home) > definition.LeashRange;
    }

    // While hunting one target, looks for a better one it can see (MonsterTargeting: the player first while reachable).
    private void ScanForNearerTarget()
    {
        if (targeting.TryFindBetter(target, DistanceToTarget(), senses, home, Relentless, out IDamageable better, out Collider betterCollider))
        {
            SetTarget(better, betterCollider);
        }
    }

    private bool IsAvoided(Vector3 position) => targeting.IsAvoided(position);

    private bool CanTarget(IDamageable candidate) => targeting.CanTarget(candidate);

    /// <summary>
    /// When set, the monster only targets the player, leaving buildings and residents alone (ward-breach attackers,
    /// design decision for the first breach).
    /// </summary>
    public bool PlayerOnly
    {
        get => targeting.PlayerOnly;
        set => targeting.PlayerOnly = value;
    }

    /// <summary>When set, the monster never gives up a chase or returns home (ward-breach attackers crossing the clearing).</summary>
    public bool Relentless { get; set; }

    /// <summary>
    /// Ground it will not enter (night prowlers: light). It neither notices targets standing there nor follows them in:
    /// a hunt that leads into it is lost at the edge, and anything heard there is searched for from where it stands.
    /// </summary>
    public System.Predicate<Vector3> AvoidArea
    {
        get => targeting.AvoidArea;
        set => targeting.AvoidArea = value;
    }

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
        movement.RepathNow();
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

    // Being hit by something it would fight makes the monster fight back, even from outside its detection range.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (CurrentState == State.Attacking && attacks.TryInterrupt(result.HealthLost))
        {
            pose?.CancelAttack();
            SetState(State.Chasing);
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
        ClearTarget();
        movement.Halt();
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
        if (definition == null || movement == null)
        {
            return;
        }

        float scale = CurrentState == State.Investigating ? PerceptionSettings.Current.InvestigateSpeedScale
            : CurrentState == State.Returning && roaming ? roamSpeedScale
            : 1f;
        movement.SetSpeed(definition.MoveSpeed * scale);
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
