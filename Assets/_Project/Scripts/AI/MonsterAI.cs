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
/// The state machine keeps its state here and its behaviour in parts by concern: MonsterAI.Awareness (noticing, idle
/// life), .Investigation (going to look), .Targets (whom it fights), .Fighting (closing in and attacking), .Home (the
/// leash and walking back), .Reactions (stagger, hurt, guard, death), .Backstabs, .Remote (a friend's copy of the
/// host's creature) and .Gizmos.
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

    /// <summary>
    /// True while a monster within <paramref name="range"/> of <paramref name="point"/> chases or attacks the player or
    /// anyone on their side: a companion, a friend, or (in a friend's game) whoever the host's creature is after.
    /// </summary>
    public static bool AnyHuntingPlayerSideWithin(Vector3 point, float range)
    {
        foreach (MonsterAI monster in active)
        {
            if (monster != null && monster.IsHuntingPlayerSide && (monster.transform.position - point).sqrMagnitude <= range * range)
            {
                return true;
            }
        }

        return false;
    }

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
    public bool IsHuntingPlayerSide => IsRemote ? remoteHunt != RemoteHunt.None && IsEngagedState
        : targetComponent != null && IsEngaging(target) && target.Faction.IsPlayerSide();

    /// <summary>True while chasing or attacking the given target.</summary>
    public bool IsEngaging(IDamageable candidate)
    {
        // Co-op, the host's creature: after this player when the host says so.
        if (IsRemote)
        {
            return candidate != null && remoteHunt == RemoteHunt.You && IsEngagedState && candidate is Component body && global::Player.IsPlayer(body.gameObject);
        }

        return candidate != null && target == candidate && IsEngagedState;
    }

    private bool IsEngagedState =>
        CurrentState == State.Chasing || CurrentState == State.Attacking || CurrentState == State.Staggered || CurrentState == State.Hopping;

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
    public bool ShieldIsUp => ShieldArcNow > 0f && CurrentState == State.Chasing;

    /// <summary>True while it holds a wind-up before letting the blow go: the same clock stops for its body and its warning.</summary>
    public bool IsHoldingStrike => CurrentState == State.Attacking && attacks.IsHolding;

    /// <summary>The chance it sees the next backstab coming, from what it remembers (0 until it has learned).</summary>
    public float BackstabReadChance
    {
        get
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            int remembered = Mathf.Max(BackstabsForeknown, backstabs.Remembered(Time.time, rules.BackstabForgotAfter));
            return BackstabMemory.ReadChance(remembered, rules.ReadChancePerBackstab, rules.ReadChanceMost);
        }
    }

    /// <summary>Raised when it starts staggering, with how long it lasts.</summary>
    public event Action<float> Staggered;

    /// <summary>Raised when any monster starts staggering (game-wide feedback).</summary>
    public static event Action<MonsterAI, float> AnyStaggered;

    /// <summary>Raised when a blow breaks off any monster's wind-up, with who struck it (game-wide feedback: the spark, the tally).</summary>
    public static event Action<MonsterAI, Combatant> AnyInterrupted;

    /// <summary>A great beast, boss or elite: not Open While Striking, made an elite, or in a boss fight. A parry doesn't floor it and a
    /// heavy blow doesn't break off its strike.</summary>
    public bool IsHeavyweight
    {
        get
        {
            if (definition == null)
            {
                return false;
            }

            return !definition.OpenWhileStriking || IsElite || InBossFight;
        }
    }

    private bool? inBossFight;

    /// <summary>True in a boss fight (BossEncounter).</summary>
    public bool InBossFight
    {
        get
        {
            if (inBossFight == null)
            {
                inBossFight = GetComponentInParent<BossEncounter>() != null;
            }

            return inBossFight.Value;
        }
    }

    /// <summary>True while it charges in (a Charge action, before or during its rush): a spear set against it stops it (Anti Charge).</summary>
    public bool IsCharging => CurrentState == State.Attacking && attacks != null && attacks.Action != null && attacks.Action.kind == MonsterAttackKind.Charge;

    /// <summary>A spear set against its charge: the charge is broken off and, unless it is a boss, it staggers.</summary>
    public void StopCharge(Combatant by)
    {
        if (!IsCharging)
        {
            return;
        }

        attacks.Cancel();
        pose?.CancelAttack();
        SetState(State.Chasing);
        if (!InBossFight)
        {
            Stagger(CombatTechniqueSettings.Current.StaggerSeconds);
        }

        AnyInterrupted?.Invoke(this, by);
    }

    /// <summary>A finishing blow has it, and lands in this many seconds. Whatever animates it stands it up to take it.</summary>
    public event Action<float> HeldForFinish;

    public bool CanBeFinished => definition != null && definition.CanBeFinished;

    /// <summary>The fight has reached this stage, and it knows the attacks that go with it (BossEncounter).</summary>
    public void SetPhase(int phase) => attacks.Phase = phase;

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

    /// <summary>True while the strike under way breaks off for any blow that hurts (its Interruptible; never an Unyielding elite's): the
    /// one rule the telegraph's colour shows and OnDamaged keeps (with Break Through for blows heavy enough to break any).</summary>
    public bool AnyBlowBreaksStrike => CurrentState == State.Attacking && !IsUnyielding && attacks.Action != null && attacks.Action.interruptible;
    public bool IsExposed => CurrentState == State.Attacking && attacks.IsExposed;
    public bool IsHowler => pack != null && pack.IsLeader(this);

    /// <summary>The direction the current attack was aimed when it started; it doesn't follow the target.</summary>
    public Vector3 AttackDirection => attacks != null ? attacks.Direction : Vector3.zero;

    /// <summary>Horizontal movement speed in meters per second, for animation.</summary>
    public float Speed => IsRemote ? remoteSpeed : movement != null ? movement.Speed : 0f;

    /// <summary>Raised when the state changes. Arguments: previous state, new state.</summary>
    public event Action<State, State> StateChanged;

    /// <summary>Raised when an attack starts, before its hit lands. Animation plays here.</summary>
    public event Action AttackStarted;

    private void Awake()
    {
        all.Add(this);
        if (SuppressedByHost())
        {
            return;
        }

        CharacterFillLight.Mark(gameObject); // lit by the player's fill light, so creatures stand out in the dark woods
        WalkerFootprints.AddTo(gameObject, 0.75f, 0.18f); // tracks, which last in snow
        if (!TryGetComponent(out CreatureWounds _)) gameObject.AddComponent<CreatureWounds>(); // limping, blood trails, armour breaking
        combatant = GetComponent<Combatant>();
        damageable = GetComponent<Damageable>();
        var agent = GetComponent<NavMeshAgent>();
        movement = new MonsterMovement(transform, agent, repathInterval, turnSpeed);
        pack = GetComponentInParent<MonsterPack>();
        targeting.Bind(transform, definition, combatant, damageable, agent, targetLayers);
        Transform visual = transform.Find("Model");
        CreatureLook.Vary(visual, definition);
        pose = visual != null ? new MonsterPose(visual) { RestsByAnimation = HasRestClip(visual) } : null;
        reelingTell = new ReelingTell(transform);
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
        // Removed before it could wake (co-op: the host's game sends the creatures).
        if (suppressed)
        {
            return;
        }

        // Onto the NavMesh first (up to a few metres, out of a hill it spawned in), then home where it stands: home taken first
        // could lie off the mesh, a place it could never walk back to.
        movement.EnsureOnNavMesh();
        ResetHome();
        lastSenseTime = Time.time;
        nextGlanceAt = Time.time + UnityEngine.Random.Range(0f, PerceptionSettings.Current.IdleGlanceSeconds.y);
        RefreshAmbush();
    }

    private void OnDestroy()
    {
        all.Remove(this);
    }

    private void OnEnable()
    {
        // Removed before it could wake (co-op: the host's game sends the creatures).
        if (suppressed)
        {
            return;
        }

        active.Add(this);
        damageable.Damaged += OnDamaged;
        damageable.Died += OnDied;
    }

    private void OnDisable()
    {
        // Removed before it could wake (co-op: the host's game sends the creatures).
        if (suppressed)
        {
            return;
        }

        active.Remove(this);
        damageable.Damaged -= OnDamaged;
        damageable.Died -= OnDied;
        damageable.RemoveInvulnerability(this);
        reelingTell?.Clear();
        ClearAnswers();
    }

    private void Update()
    {
        if (CurrentState == State.Dead || definition == null || suppressed)
        {
            return;
        }

        // Co-op: the host's creature, shown here (MonsterAI.Remote).
        if (IsRemote)
        {
            UpdateRemote();
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

        // Nothing starts a move or a blow while an in-game cinematic has the camera (the user, 2026-11-14: "have enemies stop
        // attacking during cinematic"): a swing or stagger already under way finishes above, then it stands and waits.
        if (InGameCinematic.HoldsTheWorld)
        {
            movement.Stop();
            return;
        }

        // Game runs instead of fighting (MonsterAI.Timid).
        if (UpdateTimid())
        {
            return;
        }

        // A thief steals and runs, and fights only when cornered (MonsterAI.Thief).
        if (UpdateThief())
        {
            return;
        }

        if (UpdateWeaponGuard())
        {
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
            // Every way into a walk home clears the target, so one it has now came on the way: a blow (OnDamaged), its pack's
            // call or Engage. It turns and fights instead of taking blows in the back the whole way home.
            // Its home stays where it is: the chase's leash still brings it back.
            if (!roaming)
            {
                if (!CanTarget(target))
                {
                    UpdateReturn();
                    return;
                }
            }
            else
            {
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

                // Held to its destination: what it meets far from there is left alone, and it walks on.
                if (HoldsHome && IsBeyondLeash())
                {
                    ClearTarget();
                    UpdateReturn();
                    return;
                }

                roaming = false;
                if (!HoldsHome)
                {
                    home = transform.position;
                }
            }
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
        if (definition != null && !suppressed)
        {
            UpdateTells();
        }

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

    private void SetState(State next)
    {
        if (CurrentState == next)
        {
            return;
        }

        State previous = CurrentState;
        CurrentState = next;
        // However a stagger ends (in a friend's game the host's word ends it, or its next strike), what it added goes with it.
        if (previous == State.Staggered && damageable != null)
        {
            damageable.DamageTakenMultiplier = 1f;
        }

        // However a hop ends (a stagger, a felling tree, a friend's parry): it can be hurt again. Only UpdateHop gave it back before,
        // so one staggered mid-hop fought on with every blow evaded.
        if (previous == State.Hopping && damageable != null)
        {
            damageable.RemoveInvulnerability(this);
        }

        // A staggered creature never reads a swing: a parry it was readying is gone with the stagger.
        if (next == State.Staggered)
        {
            parryUntil = -1f;
        }

        // Circling is a way of chasing: walking home or looking about goes at its own pace.
        if (next != State.Chasing)
        {
            circling = false;
        }

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
