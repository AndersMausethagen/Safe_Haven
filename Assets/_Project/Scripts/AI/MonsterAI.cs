using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives a monster in a fight: it notices hostile targets nearby, chases them on the NavMesh and attacks with its
/// definition's attack through AttackResolver, the same pipeline as the player's weapon. Who it fights follows
/// FactionRelations, so Enemy monsters hunt the Player faction, Player-faction monsters hunt Enemies and Neutral
/// monsters never start or return a fight. Led too far from where it spawned, it gives up and walks home.
/// Tuning lives in the MonsterDefinition; the current target, timers and home position are this instance's own.
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

    private static readonly Collider[] Overlaps = new Collider[64];

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

    private bool roaming;
    private PoiseMeter poise;
    private float staggerUntil;

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
        }

        if (definition == null)
        {
            Debug.LogWarning($"{name} has a MonsterAI without a MonsterDefinition, so it stays idle.", this);
            return;
        }

        agent.speed = definition.MoveSpeed;
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
        if (!roaming)
        {
            home = transform.position;
        }
        EnsureOnNavMesh();
    }

    private void OnDestroy()
    {
        all.Remove(this);
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
                    ScanForTarget();
                }
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
            if (definition.HasTemperament(MonsterTemperament.Aggressive)
                || definition.HasTemperament(MonsterTemperament.Defensive))
            {
                ScanForTarget();
            }
        }

        if (!CanTarget(target) || (!Relentless && DistanceToTarget() > definition.ChaseRange))
        {
            ClearTarget();
            GoHomeOrIdle();
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

    private void LateUpdate()
    {
        if (visual == null || definition == null || definition.HoverHeight <= 0f)
        {
            return;
        }
        float height = IsExposed || CurrentState == State.Dead ? 0f
            : definition.HoverHeight + Mathf.Sin(Time.time * 3f) * 0.08f;
        visual.localPosition = Vector3.Lerp(visual.localPosition, visualHome + Vector3.up * height, Time.deltaTime * 10f);
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
        SetState(State.Returning);
    }

    private bool IsBeyondLeash()
    {
        return definition.LeashRange > 0f && HorizontalDistance(home) > definition.LeashRange;
    }

    private void ScanForTarget()
    {
        Vector3 center = transform.position + Vector3.up * CenterHeight;
        int count = Physics.OverlapSphereNonAlloc(center, definition.DetectionRange, Overlaps, targetLayers, QueryTriggerInteraction.Ignore);

        IDamageable best = null;
        Collider bestCollider = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            IDamageable candidate = Overlaps[i].GetComponentInParent<IDamageable>();
            if (!CanTarget(candidate))
            {
                continue;
            }

            float distance = HorizontalDistance(HitDetector.NearestPoint(Overlaps[i], center));
            if (!Relentless && definition.HasTemperament(MonsterTemperament.Defensive)
                && Vector3.Distance(home, Overlaps[i].transform.position) > definition.DefensiveRadius)
            {
                continue;
            }
            if (distance < bestDistance)
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
        StateChanged?.Invoke(previous, next);
    }

    private void OnDrawGizmosSelected()
    {
        if (definition == null)
        {
            return;
        }

        Vector3 origin = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, definition.DetectionRange);
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, definition.ChaseRange);

        if (definition.LeashRange > 0f)
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(origin, definition.LeashRange);
        }
    }
}
