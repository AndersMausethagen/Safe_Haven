using System;
using UnityEngine;

/// <summary>What kind of swing an attack is.</summary>
public enum SwingKind
{
    None,
    Light,
    Heavy,
    ShieldBash,
}

/// <summary>Everything about a swing as it starts, for animation, sounds and the combo display.</summary>
public readonly struct SwingInfo
{
    public readonly SwingKind Kind;
    public readonly AttackDefinition Attack;
    /// <summary>Chain step of a light swing (0 = first).</summary>
    public readonly int Step;
    public readonly int ChainLength;
    public readonly int Rhythm;
    /// <summary>Playback speed: above 1 with rhythm.</summary>
    public readonly float Speed;
    public readonly HitTechnique Technique;
    public readonly Vector3 Direction;

    public SwingInfo(SwingKind kind, AttackDefinition attack, int step, int chainLength, int rhythm, float speed, HitTechnique technique, Vector3 direction)
    {
        Kind = kind;
        Attack = attack;
        Step = step;
        ChainLength = chainLength;
        Rhythm = rhythm;
        Speed = speed;
        Technique = technique;
        Direction = direction;
    }

    public bool IsFinisher => (Technique & HitTechnique.Finisher) != 0;
    public bool IsCounter => (Technique & HitTechnique.Counter) != 0;
}

/// <summary>
/// Runs the player's attacks. Light swings walk through the weapon's combo chain when pressed in time (see
/// <see cref="ComboRules"/>: a press right as a swing recovers builds rhythm, mashing fumbles the chain back to the
/// first swing); holding the button is the heavy attack; attacking while guarding with a shield is the shield bash. A
/// parry or a perfect dodge readies a counter: the next swing that lands soon after is a critical one. Swings snap
/// toward a target near the aim and step into it. One press is buffered while a swing or a dodge is still going. It also
/// keeps the global mode in step: attacking enters GameState.Combat, a stretch without fighting returns to Exploration.
/// <para>
/// Animation listens to <see cref="SwingStarted"/>; feedback to <see cref="SwingLanded"/>, <see cref="ChainJudged"/>,
/// <see cref="CounterReady"/> and <see cref="PerfectDodge"/>; damage numbers, audio and loot listen to the targets and
/// the Combatant, so none of them are known here.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Combatant))]
public sealed class PlayerCombat : MonoBehaviour
{
    // Recovery for an attack that has no definition, so unconfigured attacks still animate and can't be spammed.
    private const float FallbackRecoveryTime = 0.6f;
    private const float LungeSeconds = 0.12f;
    // How close a lunge stops before the target's edge.
    private const float LungeStandOff = 0.8f;

    [Header("Mode")]
    [Tooltip("Seconds in Combat without fighting before returning to Exploration. 0 stays in Combat.")]
    [SerializeField, Min(0f)] private float combatTimeout = 5f;

    [Header("Equipment")]
    [Tooltip("Sets the animation stance, the damage and the attacks. Empty means no damage.")]
    [SerializeField] private WeaponDefinition weapon;
    [Tooltip("Shield bash (attack while guarding) only works while a shield is equipped; a shield also blocks better.")]
    [SerializeField] private bool hasShield;
    [Tooltip("The attack the shield bash performs.")]
    [SerializeField] private AttackDefinition shieldBash;

    [Header("Hits")]
    [Tooltip("Layers that can be hit. Targets still have to be damageable and hostile.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Aim assist")]
    [Tooltip("A swing turns toward a hostile target within this many degrees of the aim.")]
    [SerializeField, Range(0f, 90f)] private float aimAssistAngle = 25f;
    [Tooltip("How far beyond the attack's reach aim assist looks, in metres (the lunge closes the gap).")]
    [SerializeField, Min(0f)] private float aimAssistReach = 0.6f;

    private static readonly Collider[] Nearby = new Collider[32];

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private Damageable health;
    private PlayerDodge dodge;
    private PlayerStamina stamina;
    private PlayerGuard guard;
    private PlayerEating eating;
    private GameStateManager gameState;
    private float lastActivityTime;
    private WeaponDefinition announcedWeapon;

    // The swing in progress.
    private SwingKind activeKind;
    private AttackDefinition activeAttack;
    private float activeWeaponDamage;
    private float activeStartTime;
    private float activeSpeed = 1f;
    private bool hitPending;
    private Vector3 activeDirection;
    private float activeDamageMultiplier = 1f;
    private float activePoiseMultiplier = 1f;
    private HitTechnique activeTechnique;
    private bool activeForceCritical;

    // The swing before, for judging the next press.
    private float lastStartTime = float.NegativeInfinity;
    private float lastHitAt;
    private float lastRecoverAt;
    private int lastStep = -1;

    // Input.
    private float pressTime = -1f;
    private ChainPress pressJudge = ChainPress.Late;
    private bool heavyStarted;
    private SwingKind queuedKind;
    private ChainPress queuedJudge;

    // Technique.
    private bool fumbled;
    private int rhythm;
    private float counterUntil = float.NegativeInfinity;
    private float flinchUntil = float.NegativeInfinity;

    public WeaponDefinition Weapon
    {
        get => weapon;
        set
        {
            weapon = value;
            lastStep = -1;
            AnnounceWeapon();
        }
    }

    public bool HasShield
    {
        get => hasShield;
        set => hasShield = value;
    }

    /// <summary>True while a swing is going.</summary>
    public bool IsAttacking => activeKind != SwingKind.None;

    /// <summary>The guard can go up when no swing is going or the current one has already landed, and not while flinching.</summary>
    public bool CanGuard => !IsFlinching && (activeKind == SwingKind.None || !hitPending);

    /// <summary>A dodge can't cut a swing short before it lands (unless the settings allow it): swings are commitments.</summary>
    public bool CanDodge => !IsFlinching && (activeKind == SwingKind.None || !hitPending || Rules.DodgeCancelsWindup);

    /// <summary>True for a moment after a hit got through: the player reels and can't act.</summary>
    public bool IsFlinching => Time.time < flinchUntil;

    /// <summary>Raised when a hit makes the player flinch, with how long; true when it cost them a swing.</summary>
    public event Action<float, bool> Flinched;

    /// <summary>Rhythm stacks right now (0 to the settings' maximum).</summary>
    public int Rhythm => rhythm;

    /// <summary>True while a counter is ready (after a parry or perfect dodge).</summary>
    public bool CounterIsReady => Time.time <= counterUntil;

    /// <summary>Raised when a weapon swing starts (light or heavy). Kept for listeners that don't need the details.</summary>
    public event Action AttackPerformed;

    /// <summary>Raised when a shield bash starts.</summary>
    public event Action ShieldBashPerformed;

    /// <summary>Raised when any swing starts, with everything about it. Animation plays here.</summary>
    public event Action<SwingInfo> SwingStarted;

    /// <summary>Raised when a swing's hit moment comes, with how many targets it hit (0 = a whiff).</summary>
    public event Action<SwingInfo, int> SwingLanded;

    /// <summary>Raised when an attack press is judged against the swing before it.</summary>
    public event Action<ChainPress> ChainJudged;

    /// <summary>Raised when a counter becomes ready.</summary>
    public event Action CounterReady;

    /// <summary>Raised when a hit is dodged through at the last moment (during the dodge's invulnerability).</summary>
    public event Action<DamageInfo> PerfectDodge;

    /// <summary>Raised when the equipped weapon changes, including being cleared.</summary>
    public event Action<WeaponDefinition> WeaponChanged;

    private SwingInfo current;

    private CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        dodge = GetComponent<PlayerDodge>();
        stamina = GetComponent<PlayerStamina>();
        guard = GetComponent<PlayerGuard>();
        eating = GetComponent<PlayerEating>();
        announcedWeapon = weapon;
    }

    private void OnEnable()
    {
        stateMachine.AttackRequested += OnAttackPressed;
        stateMachine.AttackReleased += OnAttackReleased;
        stateMachine.ShieldBashRequested += OnShieldBashRequested;
        combatant.DamageDealt += OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (health != null)
        {
            health.Evaded += OnEvaded;
            health.Damaged += OnHurt;
        }

        gameState = GameStateManager.Instance;
        if (gameState == null)
        {
            Debug.LogWarning("PlayerCombat found no GameStateManager; attacking won't change the game mode.", this);
            return;
        }

        gameState.GameStateChanged += OnGameStateChanged;
        lastActivityTime = Time.time;
    }

    private void OnDisable()
    {
        stateMachine.AttackRequested -= OnAttackPressed;
        stateMachine.AttackReleased -= OnAttackReleased;
        stateMachine.ShieldBashRequested -= OnShieldBashRequested;
        combatant.DamageDealt -= OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (health != null)
        {
            health.Evaded -= OnEvaded;
            health.Damaged -= OnHurt;
        }

        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }

        EndSwing();
        queuedKind = SwingKind.None;
        pressTime = -1f;
        movement.SetSpeedScale(this, 1f);
    }

    private void OnValidate()
    {
        // Lets the weapon be swapped in the Inspector while playing.
        if (Application.isPlaying && announcedWeapon != weapon)
        {
            AnnounceWeapon();
        }
    }

    private void Update()
    {
        if (flinchUntil > 0f && !IsFlinching)
        {
            flinchUntil = 0f;
            movement.SetSpeedScale(Flinch, 1f);
        }

        UpdateHeldPress();
        UpdateSwing();

        if (gameState != null
            && combatTimeout > 0f
            && gameState.CurrentState == GameState.Combat
            && Time.time - lastActivityTime >= combatTimeout)
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    // ----- Input -----

    private void OnAttackPressed()
    {
        if (guard != null && guard.IsGuarding)
        {
            if (hasShield)
            {
                Request(SwingKind.ShieldBash, ChainPress.Late);
                return;
            }

            // Without a shield, attacking drops the guard and swings (a ready counter is spent here).
            guard.Lower();
        }

        pressJudge = JudgePress();
        if (pressJudge == ChainPress.Early && Rules.EarlyPressFumbles && activeKind == SwingKind.Light)
        {
            fumbled = true;
        }

        ChainJudged?.Invoke(pressJudge);

        // Without a heavy attack there is nothing to wait for: swing at once.
        if (weapon == null || weapon.HeavyAttack == null)
        {
            pressTime = -1f;
            Request(SwingKind.Light, pressJudge);
            return;
        }

        pressTime = Time.time;
        heavyStarted = false;
    }

    private void OnAttackReleased()
    {
        if (pressTime < 0f)
        {
            return;
        }

        pressTime = -1f;
        if (!heavyStarted)
        {
            Request(SwingKind.Light, pressJudge);
        }
    }

    private void UpdateHeldPress()
    {
        if (pressTime < 0f || heavyStarted)
        {
            return;
        }

        if (Time.time - pressTime >= Rules.HeavyHoldSeconds)
        {
            heavyStarted = true;
            Request(SwingKind.Heavy, ChainPress.Late);
        }
    }

    // Kept for gamepads and scripts that ask for a bash directly.
    private void OnShieldBashRequested()
    {
        if (hasShield)
        {
            Request(SwingKind.ShieldBash, ChainPress.Late);
        }
    }

    private ChainPress JudgePress()
    {
        if (activeKind != SwingKind.None)
        {
            return ComboRules.Judge(Time.time - activeStartTime, HitAt(activeAttack, activeSpeed), RecoverAt(activeAttack, activeSpeed), Rules);
        }

        return ComboRules.Judge(Time.time - lastStartTime, lastHitAt, lastRecoverAt, Rules);
    }

    private void Request(SwingKind kind, ChainPress judge)
    {
        if (eating != null && eating.IsEating)
        {
            return; // eating takes both hands
        }

        bool busy = activeKind != SwingKind.None || IsFlinching || (dodge != null && dodge.IsDodging) || (guard != null && guard.IsStunned);
        if (busy)
        {
            // Buffered: it starts as soon as the current swing has recovered or the dodge has ended.
            queuedKind = kind;
            queuedJudge = judge;
            return;
        }

        Begin(kind, judge);
    }

    // ----- Swings -----

    private void Begin(SwingKind kind, ChainPress judge)
    {
        queuedKind = SwingKind.None;
        int step = 0;
        int chain = weapon != null ? weapon.ComboLength : 1;
        AttackDefinition attack;
        HitTechnique technique = HitTechnique.None;
        switch (kind)
        {
            case SwingKind.Heavy:
                attack = weapon != null ? weapon.HeavyAttack : null;
                technique |= HitTechnique.Heavy;
                break;
            case SwingKind.ShieldBash:
                attack = shieldBash;
                technique |= HitTechnique.ShieldBash;
                break;
            default:
                step = ComboRules.NextStep(lastStep, chain, judge, fumbled);
                attack = weapon != null ? weapon.ComboStep(step) : null;
                if (CounterIsReady && weapon != null && weapon.CounterAttack != null)
                {
                    attack = weapon.CounterAttack;
                    step = 0;
                }

                break;
        }

        // Pay the full cost when the swing actually starts, rechecking affordability for buffered swings.
        if (stamina != null && !stamina.TrySpend(CostOf(kind, attack)))
        {
            return;
        }

        fumbled = false;
        rhythm = kind == SwingKind.Light && step > 0 ? ComboRules.NextRhythm(rhythm, judge, Rules) : 0;
        bool finisher = kind == SwingKind.Light && chain > 1 && step == chain - 1;
        lastStep = kind == SwingKind.Light && !finisher ? step : -1;

        activeKind = kind;
        activeAttack = attack;
        activeWeaponDamage = kind == SwingKind.ShieldBash || weapon == null ? 0f : weapon.Damage;
        activeSpeed = ComboRules.SpeedMultiplier(rhythm, Rules);
        activeDamageMultiplier = ComboRules.DamageMultiplier(rhythm, Rules);
        activePoiseMultiplier = 1f;
        activeForceCritical = false;
        if (rhythm > 0)
        {
            technique |= HitTechnique.Rhythm;
        }

        if (finisher)
        {
            technique |= HitTechnique.Finisher;
            activePoiseMultiplier *= 1.5f;
        }

        if (CounterIsReady && kind != SwingKind.ShieldBash)
        {
            technique |= HitTechnique.Counter;
            activeDamageMultiplier *= Rules.CounterDamageMultiplier;
            activePoiseMultiplier *= 2f;
            activeForceCritical = true;
            counterUntil = float.NegativeInfinity;
        }

        activeTechnique = technique;
        activeStartTime = Time.time;
        hitPending = attack != null;
        // The direction is locked in when the swing starts: toward the aim, or the target it snaps to.
        activeDirection = AimAssist(movement.AimDirection, attack, out float targetDistance);
        movement.SnapFacing(activeDirection);
        if (attack != null && attack.Lunge > 0f)
        {
            float distance = targetDistance >= 0f ? Mathf.Clamp(targetDistance - LungeStandOff, 0f, attack.Lunge) : attack.Lunge * 0.5f;
            movement.Lunge(activeDirection * (distance / LungeSeconds), LungeSeconds);
        }

        movement.SetSpeedScale(this, kind == SwingKind.Heavy ? Rules.HeavyMoveSpeed : Rules.SwingMoveSpeed);
        EnterCombat();

        current = new SwingInfo(kind, attack, step, chain, rhythm, activeSpeed, technique, activeDirection);
        SwingStarted?.Invoke(current);
        if (kind == SwingKind.ShieldBash)
        {
            ShieldBashPerformed?.Invoke();
        }
        else
        {
            AttackPerformed?.Invoke();
        }
    }

    private float CostOf(SwingKind kind, AttackDefinition attack)
    {
        if (stamina == null)
        {
            return 0f;
        }

        if (attack != null && attack.StaminaCost >= 0f)
        {
            return attack.StaminaCost;
        }

        return kind == SwingKind.ShieldBash ? stamina.ShieldBashCost : kind == SwingKind.Heavy ? stamina.AttackCost * 2f : stamina.AttackCost;
    }

    private void UpdateSwing()
    {
        if (activeKind == SwingKind.None)
        {
            if (queuedKind != SwingKind.None && !IsFlinching && (dodge == null || !dodge.IsDodging) && (guard == null || !guard.IsStunned))
            {
                Begin(queuedKind, queuedJudge);
            }

            return;
        }

        float elapsed = Time.time - activeStartTime;
        if (hitPending && elapsed >= HitAt(activeAttack, activeSpeed))
        {
            hitPending = false;
            int hits = AttackResolver.Execute(combatant, activeAttack, activeWeaponDamage, transform.position, activeDirection, hitLayers,
                activeDamageMultiplier, activePoiseMultiplier, activeTechnique, activeForceCritical);
            SwingLanded?.Invoke(current, hits);
        }

        if (elapsed >= RecoverAt(activeAttack, activeSpeed))
        {
            SwingKind next = queuedKind;
            ChainPress judge = queuedJudge;
            EndSwing();
            if (next != SwingKind.None)
            {
                Begin(next, judge);
            }
        }
    }

    private void EndSwing()
    {
        if (activeKind != SwingKind.None)
        {
            lastStartTime = activeStartTime;
            lastHitAt = HitAt(activeAttack, activeSpeed);
            lastRecoverAt = RecoverAt(activeAttack, activeSpeed);
        }

        activeKind = SwingKind.None;
        activeAttack = null;
        hitPending = false;
        movement.SetSpeedScale(this, 1f);
    }

    private static float HitAt(AttackDefinition attack, float speed) => attack != null ? attack.HitDelay / speed : 0f;

    private static float RecoverAt(AttackDefinition attack, float speed) => (attack != null ? attack.RecoveryTime : FallbackRecoveryTime) / speed;

    /// <summary>Ends the current swing if its hit has already landed (raising the guard cuts the follow-through short).</summary>
    public void CancelRecovery()
    {
        if (activeKind != SwingKind.None && !hitPending)
        {
            EndSwing();
            queuedKind = SwingKind.None;
        }
    }

    /// <summary>Readies a counter: the next swing within the settings' counter time is a critical counter.</summary>
    public void GrantCounter()
    {
        counterUntil = Time.time + Rules.CounterSeconds;
        CounterReady?.Invoke();
    }

    // The nearest hostile target within the assist cone and reach, else the aim as given.
    private Vector3 AimAssist(Vector3 aim, AttackDefinition attack, out float targetDistance)
    {
        targetDistance = -1f;
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : transform.forward;
        if (attack == null || aimAssistAngle <= 0f)
        {
            return aim;
        }

        float reach = attack.Range + attack.Lunge + aimAssistReach;
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, reach, Nearby, hitLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 chosen = aim;
        for (int i = 0; i < count; i++)
        {
            IDamageable target = Nearby[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || target.Faction == combatant.Faction || target.Category != TargetCategory.Creature)
            {
                continue;
            }

            Vector3 to = HitDetector.NearestPoint(Nearby[i], transform.position + Vector3.up) - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            float angle = distance > 0.05f ? Vector3.Angle(aim, to) : 0f;
            // Prefer what is near the aim, then what is near the player.
            float score = angle / Mathf.Max(aimAssistAngle, 1f) + distance / reach;
            if (angle <= aimAssistAngle && score < best)
            {
                best = score;
                chosen = distance > 0.05f ? to / distance : aim;
                targetDistance = distance;
            }
        }

        return chosen;
    }

    // ----- Reactions -----

    // Dodging is the way out of trouble, so it cuts a swing short; a swing that hadn't landed yet deals no damage. The
    // chain survives it, so dodge-cancelled combos still count if the next press is in time.
    private void OnDodged(Vector3 direction)
    {
        EndSwing();
        queuedKind = SwingKind.None;
        pressTime = -1f;
    }

    // The player is no super soldier: a real hit (not blocked) staggers them for a moment, and a swing still winding up
    // is lost. Trading blows loses; read the telegraph, guard or get out of the way.
    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (result.Blocked || result.Killed || health == null || result.Damage < health.MaxHealth * Rules.FlinchShare)
        {
            return;
        }

        bool lostSwing = activeKind != SwingKind.None && hitPending;
        if (lostSwing)
        {
            EndSwing();
        }

        queuedKind = SwingKind.None;
        pressTime = -1f;
        flinchUntil = Time.time + Rules.FlinchSeconds;
        movement.SetSpeedScale(Flinch, 0.25f);
        Flinched?.Invoke(Rules.FlinchSeconds, lostSwing);
    }

    // Key for the flinch's slowdown, separate from the swing's own.
    private static readonly object Flinch = new object();

    // A hit evaded during the dodge's invulnerability is a perfect dodge: it readies a counter.
    private void OnEvaded(DamageInfo damage)
    {
        if (dodge != null && dodge.IsDodging && damage.Source != null)
        {
            GrantCounter();
            PerfectDodge?.Invoke(damage);
        }
    }

    private void AnnounceWeapon()
    {
        announcedWeapon = weapon;
        WeaponChanged?.Invoke(weapon);
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        lastActivityTime = Time.time;
    }

    // The timeout counts from entering Combat as well, so a manual toggle also returns to Exploration when left idle.
    private void OnGameStateChanged(GameState previous, GameState state)
    {
        if (state == GameState.Combat)
        {
            lastActivityTime = Time.time;
        }
    }

    private void EnterCombat()
    {
        lastActivityTime = Time.time;
        if (gameState != null)
        {
            gameState.TrySetState(GameState.Combat);
        }
    }
}
