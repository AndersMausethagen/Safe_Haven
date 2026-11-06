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
/// between them: idle, investigating, chasing, attacking, staggered, hopping, returning, dead.
/// </para>
/// <para>
/// With nothing going on it lives a little (MonsterIdleLife). In a fight it spreads out around its target with others (AttackTurns.FlankSpot),
/// won't loose a shot it has no clear line for, walks off once its target stands where it can't be reached, and flees when its pack breaks.
/// </para>
/// <para>
/// Backstabs: it answers one with a strike or a hop, and remembers it (BackstabMemory), as do others of its side who saw. One that
/// remembers may see the next swing coming from behind while it strikes, and break off to hop aside or parry. It is also the guard
/// on its own Damageable, for that parry and the shield its definition may give it.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Combatant))]
[RequireComponent(typeof(Damageable))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed partial class MonsterAI : MonoBehaviour, IEventSubject, IStaggerable, IHitGuard
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
        /// <summary>Hopping out of reach of a backstab, or after one. Blows don't touch it meanwhile.</summary>
        Hopping,
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
    private float speedScale = 1f;
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

    private readonly BackstabMemory backstabs = new BackstabMemory();
    // When it answers the last backstab; below 0: it has nothing to answer.
    private float answerAt = -1f;
    // Its parry of a swing it saw coming stays up until then.
    private float parryUntil = -1f;
    private float lastSwingWatched = -1f;
    private float hopEndsAt;
    private Vector3 hopDirection;

    // How high a hop lifts the body at its middle, in metres.
    private const float HopHeight = 0.3f;

    // Waiting its turn at the player: until when, and which way round it goes.
    private bool circling;
    private float circlingUntil;
    private float circleSide = 1f;
    // Close strikes at the player don't start together: shared by every monster.
    private static float nextTurnAt;

    private MonsterIdleLife idleLife;
    private MonsterIdleDriver idleDriver;
    private MonsterPositioning positioning;
    // Fleeing from a broken pack until then.
    private float routedUntil = float.NegativeInfinity;
    // Its target stands where it can't be reached: since when, and until when it doesn't try again.
    private float cutOffSince = -1f;
    private float ignoresTargetsUntil = float.NegativeInfinity;


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
            if (monster.IsHuntingPlayerSide)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True while it is coming at its target with a close strike: what a companion steps out of the way of.</summary>
    public bool IsClosingIn => CurrentState == State.Attacking && attacks.IsClosingIn;

    /// <summary>True while this monster chases or attacks the player or someone on their side.</summary>
    public bool IsHuntingPlayerSide => targetComponent != null && IsEngaging(target) && target.Faction.IsPlayerSide();

    /// <summary>True while chasing or attacking the given target.</summary>
    public bool IsEngaging(IDamageable candidate)
    {
        return candidate != null && target == candidate
            && (CurrentState == State.Chasing || CurrentState == State.Attacking || CurrentState == State.Staggered || CurrentState == State.Hopping);
    }

    /// <summary>Its poise right now (null before it has woken).</summary>
    public PoiseMeter Poise => poise;

    /// <summary>What it has noticed: its awareness and what it saw last (null without a definition).</summary>
    public MonsterSenses Senses => senses;

    /// <summary>True while it searches for a target it lost sight of, rather than checking a glimpse or a sound.</summary>
    public bool IsSearchingLostTarget => CurrentState == State.Investigating && searchingLostTarget;

    public bool IsStaggered => CurrentState == State.Staggered;

    /// <summary>True while a blow from behind is a backstab: it staggers, or is committed to a strike and can't turn to face it. Great beasts
    /// and elites aren't caught out by their own strikes, so only a stagger opens them.</summary>
    public bool IsOpenToBackstab => CurrentState == State.Staggered || (CurrentState == State.Attacking && OpenWhileStriking);

    private bool OpenWhileStriking => definition != null && definition.OpenWhileStriking && !IsElite;

    /// <summary>Set when a raid makes it an elite.</summary>
    public bool IsElite { get; set; }

    /// <summary>True while its shield is up: hunting, not striking, staggering or hopping.</summary>
    public bool ShieldIsUp => definition != null && definition.ShieldArc > 0f && CurrentState == State.Chasing;

    /// <summary>True while it holds a wind-up before letting the blow go: the same clock stops for its body and its warning.</summary>
    public bool IsHoldingStrike => CurrentState == State.Attacking && attacks.IsHolding;

    /// <summary>The chance it sees the next backstab coming, from what it remembers (0 until it has learned).</summary>
    public float BackstabReadChance
    {
        get
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            return BackstabMemory.ReadChance(backstabs.Remembered(Time.time, rules.BackstabForgotAfter), rules.ReadChancePerBackstab, rules.ReadChanceMost);
        }
    }

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

    /// <summary>A finishing blow has it: it reels on, facing away, until the blow is done whatever its stagger had left.</summary>
    public void HoldForFinish(float seconds, float bladeIn)
    {
        if (CurrentState != State.Staggered)
        {
            return;
        }

        staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);
        pose?.Stagger(Time.time, seconds);
        HeldForFinish?.Invoke(bladeIn);
    }

    /// <summary>A finishing blow has it, and lands in this many seconds. Whatever animates it stands it up to take it.</summary>
    public event Action<float> HeldForFinish;

    public bool CanBeFinished => definition != null && definition.CanBeFinished;

    /// <summary>The fight has reached this stage, and it knows the attacks that go with it (BossEncounter).</summary>
    public void SetPhase(int phase) => attacks.Phase = phase;

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
    public LayerMask TargetLayers => targetLayers;

    /// <summary>Multiplies how fast it moves (a frenzied elite).</summary>
    public float SpeedScale
    {
        get => speedScale;
        set
        {
            speedScale = Mathf.Max(0.1f, value);
            ApplySpeed();
        }
    }

    /// <summary>Multiplies how long it waits between strikes.</summary>
    public float CooldownScale
    {
        get => attacks != null ? attacks.CooldownScale : 1f;
        set
        {
            if (attacks != null)
            {
                attacks.CooldownScale = Mathf.Max(0.1f, value);
            }
        }
    }
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
        CharacterFillLight.Mark(gameObject); // lit by the player's fill light, so creatures stand out in the dark woods
        WalkerFootprints.AddTo(gameObject, 0.75f, 0.18f); // tracks, which last in snow
        combatant = GetComponent<Combatant>();
        damageable = GetComponent<Damageable>();
        var agent = GetComponent<NavMeshAgent>();
        movement = new MonsterMovement(transform, agent, repathInterval, turnSpeed);
        pack = GetComponentInParent<MonsterPack>();
        targeting.Bind(transform, definition, combatant, damageable, agent, targetLayers);
        Transform visual = transform.Find("Model");
        CreatureLook.Vary(visual, definition);
        pose = visual != null ? new MonsterPose(visual) { RestsByAnimation = HasRestClip(visual) } : null;
        if (pose != null && damageable != null)
        {
            // The pose recoils from blows and owns the model's scale, so the generic squash would fight it.
            HitReaction.For(damageable).Punch = false;
        }

        if (definition == null)
        {
            Debug.LogWarning($"{name} has a MonsterAI without a MonsterDefinition, so it stays idle.", this);
            return;
        }

        movement.SetSpeed(definition.MoveSpeed);
        idleLife = new MonsterIdleLife(definition);
        idleLife.Reset();
        senses = new MonsterSenses(transform, definition, targetLayers,
            (candidate, collider) => targeting.IsSenseCandidate(candidate, collider, home, Relentless));
        idleDriver = new MonsterIdleDriver(transform, definition, idleLife, movement, senses, pose, ApplySpeed);
        positioning = new MonsterPositioning(transform, movement, senses, definition);
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
        damageable.RemoveInvulnerability(this);
    }

    private void Update()
    {
        if (CurrentState == State.Dead || definition == null)
        {
            return;
        }

        poise?.Tick(Time.time, Time.deltaTime);
        TrackTargetMovement();
        if (CurrentState == State.Staggered)
        {
            UpdateStagger();
            return;
        }

        if (CurrentState == State.Hopping)
        {
            UpdateHop();
            return;
        }

        if (CurrentState == State.Attacking)
        {
            if (!SeesBackstabComing())
            {
                UpdateAttack();
            }

            return;
        }

        if (Time.time < parryUntil && CanTarget(target))
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(FlatToTarget()),
                CombatTechniqueSettings.Current.ReadTurnSpeed * Time.deltaTime);
            return;
        }

        if (answerAt >= 0f && Time.time >= answerAt)
        {
            answerAt = -1f;
            if (AnswerBackstab())
            {
                return;
            }
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

            if (UpdateIdleLife())
            {
                return;
            }

            GoHomeOrIdle();
            if (CurrentState == State.Idle)
            {
                UpdateIdleGaze();
            }
            return;
        }

        if (!Relentless && ((!IsProvoked && DistanceToTarget() > definition.ChaseRange) || IsAvoided(targetComponent.transform.position)))
        {
            LoseTrack();
            return;
        }

        if (!Relentless && !IsProvoked && IsBeyondLeash())
        {
            ClearTarget();
            BeginReturn();
            return;
        }

        if (WalksOffCutOff())
        {
            return;
        }

        Engage();
    }

    // ----- Idle life -----

    private bool UpdateIdleLife()
    {
        if (CurrentState != State.Idle || idleLife == null || !idleLife.IsOn || KeepAwake || Relentless || roaming)
        {
            return false;
        }

        idleDriver.Tick(home);
        if (idleLife.Now == MonsterIdleLife.Doing.Standing)
        {
            UpdateIdleGaze();
        }

        return true;
    }

    // ----- In a fight -----

    private void TrackTargetMovement()
    {
        if (targetComponent == null || attacks == null || Time.deltaTime <= 0f)
        {
            return;
        }

        attacks.TargetVelocity = positioning.TrackTarget(targetComponent.transform.position, Time.deltaTime);
    }

    // It can do nothing here and knows it: it gives up for a while, unless it can shoot from where it stands.
    private bool WalksOffCutOff()
    {
        if (Relentless || !movement.IsCutOff)
        {
            cutOffSince = -1f;
            return false;
        }

        if (cutOffSince < 0f)
        {
            cutOffSince = Time.time;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        if (Time.time - cutOffSince < rules.CutOffSeconds || CanShoot)
        {
            return false;
        }

        // The spot it was walking to may be what's out of reach (a flank in a wall), not the target.
        cutOffSince = -1f;
        if (targeting.CanReach(targetComponent.transform.position))
        {
            return false;
        }

        ignoresTargetsUntil = Time.time + rules.CutOffForgetSeconds;
        ClearTarget();
        BeginReturn();
        return true;
    }

    private bool CanShoot
    {
        get
        {
            foreach (MonsterAttack action in definition.Attacks)
            {
                if (action != null && action.kind == MonsterAttackKind.Projectile)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A broken pack: it runs from its target for this long, and doesn't strike. Elites and those sent on a mission stand.</summary>
    public void Rout(float seconds)
    {
        if (seconds <= 0f || IsElite || Relentless || CurrentState == State.Dead)
        {
            return;
        }

        routedUntil = Time.time + seconds;
        circling = false;
        if (CurrentState == State.Attacking)
        {
            attacks.Cancel();
            pose?.CancelAttack();
            SetState(State.Chasing);
        }
    }

    private bool IsRouted => Time.time < routedUntil;

    private bool ShotIsClear() => positioning.ShotIsClear(targetComponent.transform.position);

    private void FindALine(Vector3 toTarget, float distance)
    {
        SetState(State.Chasing);
        positioning.WalkToALine(targetComponent.transform.position, toTarget, distance);
    }

    private Vector3 ChaseSpot(float distance) => positioning.ChaseSpot(this, target, targetComponent.transform.position, distance, IsAtThePlayer);

    /// <summary>True while it is chasing or striking this target, for those choosing a side of it.</summary>
    public bool IsClosingOn(IDamageable who) => target == who && (CurrentState == State.Chasing || CurrentState == State.Attacking);

    // Monsters that start fights look and listen for targets; retaliation-only ones only react to being hit.
    private bool IsWatchful()
    {
        return definition.HasTemperament(MonsterTemperament.Aggressive)
            || definition.HasTemperament(MonsterTemperament.Defensive);
    }

    // Looks for targets: while hunting one, keeps track of it; otherwise fills awareness and, once suspicious, goes to look.
    private void Sense()
    {
        float elapsed = Mathf.Min(Time.time - lastSenseTime, 1f);
        lastSenseTime = Time.time;
        if (Time.time < ignoresTargetsUntil && !CanTarget(target))
        {
            return;
        }

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
        // Whoever just hit it can't hide by standing far off: it knows where the blow came from.
        if (IsProvoked || senses.CanSee(targetComponent.transform, definition.ChaseRange))
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
        if (global::Player.IsPlayer(targetComponent.gameObject))
        {
            CreatureRecords.NoteMet(definition);
        }

        if (global::Player.IsPlayer(targetComponent.gameObject) && Time.time >= nextSpottedSoundAt)
        {
            nextSpottedSoundAt = Time.time + SpottedSoundSpacing;
            AudioManager.Play(rules.SpottedSound, transform.position);
        }

        WorldNoise.Emit(transform.position, rules.SpottedCallRadius);
    }

    // A body with a clip to rest in uses it, rather than being tipped and pressed down whole.
    private static bool HasRestClip(Transform visual)
    {
        if (!visual.TryGetComponent(out Animator animator) || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == MonsterAnimationDriver.RestingParameter)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True while it grazes or sleeps.</summary>
    public bool IsResting => pose != null && pose.Rest > 0.25f;

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
        if (IsHoldingStrike)
        {
            pose.HoldAttack(Time.deltaTime);
        }

        pose.Update(hoverHeight, dead, current != null && current.kind == MonsterAttackKind.Charge, CurrentState == State.Hopping ? HopLift : attacks.LeapHeight);
    }

    // It rises and falls over the length of the hop.
    private float HopLift
    {
        get
        {
            float through = 1f - Mathf.Clamp01((hopEndsAt - Time.time) / CombatTechniqueSettings.Current.HopSeconds);
            return HopHeight * 4f * through * (1f - through);
        }
    }

    private void Engage()
    {
        Vector3 toTarget = TargetPoint() - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (IsRouted)
        {
            SetState(State.Chasing);
            Retreat(toTarget, definition.FleeDistance);
            return;
        }

        if (circling)
        {
            if (Time.time < circlingUntil)
            {
                Circle(toTarget, distance);
                return;
            }

            circling = false;
            ApplySpeed();
        }

        // Defending its ground ends where its ground does, but not while it answers a blow.
        if (!Relentless && !IsProvoked && definition.HasTemperament(MonsterTemperament.Defensive)
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
                if (HoldsBack(definition.Attacks[chosen].kind, toTarget, distance))
                {
                    return;
                }

                if (definition.Attacks[chosen].kind == MonsterAttackKind.Projectile && !ShotIsClear())
                {
                    FindALine(toTarget, distance);
                    return;
                }

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
                    movement.MoveTo(ChaseSpot(distance));
                }
            }
            return;
        }
        AttackDefinition attack = definition.Attack;

        if (attack != null && toTarget.magnitude <= attack.Range * AttackReachFactor)
        {
            if (attacks.IsReady && HoldsBack(MonsterAttackKind.Melee, toTarget, distance))
            {
                return;
            }

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
        movement.MoveTo(ChaseSpot(distance));
    }

    // Action index, or -1 for the definition's plain attack.
    private void BeginAttack(int action, Vector3 toTarget)
    {
        circling = false;
        attacks.Begin(action, toTarget);
        if (IsAtThePlayer)
        {
            nextTurnAt = Time.time + CombatTechniqueSettings.Current.AttackTurnSpacing;
        }

        SetState(State.Attacking);
        AnnounceStrike();
    }

    // A strike's warning, to the eye and the ear, and the body going into it.
    private void AnnounceStrike()
    {
        pose?.Attack(CurrentAttack, Time.time);
        MonsterAttack action = attacks.Action;
        AudioManager.Play(action != null && action.sound != null ? action.sound : Definition.AttackSound, transform.position);
        AttackStarted?.Invoke();
    }

    private void UpdateAttack()
    {
        int blow = attacks.Blow;
        if (!attacks.Update(CanTarget(target) ? targetComponent.transform.position : (Vector3?)null))
        {
            SetState(State.Chasing);
            return;
        }

        // The next blow of a combo: a body and a warning of its own, as a strike has.
        if (attacks.Blow != blow)
        {
            AnnounceStrike();
        }
    }

    private bool IsAtThePlayer => targetComponent != null && global::Player.IsPlayer(targetComponent.gameObject);

    // Only so many close in on the player at once, so a crowd can be read: one crowded out circles until its next try, and one that only
    // has to let another's blow start first stands ready. Ranged and calling actions don't wait, nor do great beasts and elites, though
    // they are counted. True when it held back.
    private bool HoldsBack(MonsterAttackKind kind, Vector3 toTarget, float distance)
    {
        if ((kind != MonsterAttackKind.Melee && kind != MonsterAttackKind.Charge) || !OpenWhileStriking || !IsAtThePlayer)
        {
            return false;
        }

        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        int closingIn = 0;
        foreach (MonsterAI other in active)
        {
            if (other != this && other.target == target && other.CurrentState == State.Attacking && other.attacks.IsClosingIn)
            {
                closingIn++;
            }
        }

        switch (AttackTurns.Decide(closingIn, rules.AttackersAtOnce, Time.time, nextTurnAt))
        {
            case AttackTurn.Circle:
                circling = true;
                circlingUntil = Time.time + UnityEngine.Random.Range(rules.CircleSeconds.x, Mathf.Max(rules.CircleSeconds.x, rules.CircleSeconds.y));
                circleSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                ApplySpeed();
                Circle(toTarget, distance);
                return true;
            case AttackTurn.StandReady:
                movement.Stop();
                SetState(State.Chasing);
                movement.TurnTowards(toTarget);
                return true;
            default:
                return false;
        }
    }

    // Sideways around the target, eyes on it, drifting to the distance it waits at.
    private void Circle(Vector3 toTarget, float distance)
    {
        SetState(State.Chasing);
        Vector3 spot = AttackTurns.CircleSpot(transform.position, toTarget, circleSide, CombatTechniqueSettings.Current.CircleDistance);
        if (movement.TryFindGround(spot, 1.5f, out Vector3 ground))
        {
            movement.Strafe(ground, toTarget);
        }
        else
        {
            movement.Stop();
            movement.TurnTowards(toTarget);
        }
    }

    private void Retreat(Vector3 toTarget, float distance)
    {
        Vector3 away = toTarget.sqrMagnitude > 0.001f ? -toTarget.normalized : -transform.forward;
        Vector3 destination = transform.position + away * Mathf.Max(2f, distance);
        if (movement.TryFindGround(destination, 2f, out Vector3 ground)
            && (Relentless || IsRouted || definition.LeashRange <= 0f || Vector3.Distance(home, ground) <= definition.LeashRange))
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

    private float provokedUntil = float.NegativeInfinity;

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

    private bool IsProvoked => Time.time < provokedUntil;

    // Being hit by something it would fight makes the monster fight back, even from outside its detection range: without
    // this, an arrow from beyond its chase range was forgotten the next moment.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        // Burning ticks don't jolt the body; strong blows rock it hardest.
        if (pose != null && result.Damage > 0f && !result.Blocked && (damage.Technique & HitTechnique.OverTime) == 0)
        {
            bool strong = result.IsCritical || result.Killed
                || (damage.Technique & (HitTechnique.Heavy | HitTechnique.Finisher | HitTechnique.Counter)) != 0;
            pose.Hit(damage.Direction, strong ? 1f : 0.6f, Time.time);
        }

        if (CurrentState == State.Attacking && attacks.TryInterrupt(result.HealthLost))
        {
            pose?.CancelAttack();
            SetState(State.Chasing);
        }

        if ((damage.Technique & HitTechnique.Backstab) != 0)
        {
            OnBackstabbed(result.Killed);
        }

        if (poise != null && CurrentState != State.Staggered && CurrentState != State.Dead && !result.Killed
            && poise.Hit(damage.Poise, Time.time))
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }

        Combatant attacker = damage.Source;
        if (definition == null || CurrentState == State.Dead || attacker == null)
        {
            return;
        }

        IDamageable attackerTarget = attacker.GetComponent<IDamageable>();
        if (!CanTarget(attackerTarget))
        {
            return;
        }

        if (!CanTarget(target))
        {
            SetTarget(attackerTarget, null);
        }

        if (target == attackerTarget)
        {
            provokedUntil = Time.time + PerceptionSettings.Current.ProvokedSeconds;
        }
    }

    /// <summary>Damageable asks before every hit: a parry of a swing it saw coming, or its shield while it hunts.</summary>
    public GuardOutcome Guard(in DamageInfo damage, out float letThrough)
    {
        letThrough = 1f;
        if (definition == null || damage.IsOverTime)
        {
            return GuardOutcome.None;
        }

        if (Time.time < parryUntil)
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            parryUntil = -1f;
            answerAt = Time.time + rules.AnswerDelay;
            if (damage.Source != null && damage.Source.TryGetComponent(out PlayerCombat swinger) && HorizontalDistance(swinger.transform.position) <= rules.ReadRange)
            {
                swinger.Rebuff(rules.ParriedFlinchSeconds);
            }

            letThrough = 0f;
            return GuardOutcome.Parried;
        }

        if (CurrentState != State.Chasing || GuardRules.ResolveShield(definition.ShieldArc, transform.forward, damage.Direction, damage.Technique) != GuardOutcome.Blocked)
        {
            return GuardOutcome.None;
        }

        letThrough = definition.ShieldLetsThrough;
        return GuardOutcome.Blocked;
    }

    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        damageable.DamageTakenMultiplier = 1f;
        ClearTarget();
        movement.Halt();
        SetState(State.Dead);
        pack?.MemberFell(this);
    }

    private void SetState(State next)
    {
        if (CurrentState == next)
        {
            return;
        }

        State previous = CurrentState;
        CurrentState = next;
        if (previous == State.Idle && idleLife != null)
        {
            idleLife.Reset();
            idleDriver.Show();
        }

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

        float scale = CurrentState == State.Idle && idleLife != null && idleLife.Now == MonsterIdleLife.Doing.Walking ? definition.IdleWalkSpeed
            : CurrentState == State.Investigating ? PerceptionSettings.Current.InvestigateSpeedScale
            : CurrentState == State.Returning && roaming ? roamSpeedScale
            : circling ? CombatTechniqueSettings.Current.CircleSpeed
            : 1f;
        movement.SetSpeed(definition.MoveSpeed * scale * speedScale);
    }

    // Entering play mode doesn't reload scripts in this project, so the shared sound timer is reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        nextSpottedSoundAt = 0f;
        nextTurnAt = 0f;
    }
}
