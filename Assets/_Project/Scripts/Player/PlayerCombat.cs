using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What kind of swing an attack is.</summary>
public enum SwingKind
{
    None,
    Light,
    Heavy,
    ShieldBash,
    /// <summary>A weapon art, paid for with Focus.</summary>
    Art,
    /// <summary>A quick lunge out of a dodge or on the run.</summary>
    Rush,
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
/// first swing); the light swing starts on the press, and holding the button on turns it into the heavy attack while it
/// is still winding up, which waits at the top of its wind-up and charges for as long as it is held; attacking while guarding
/// with a shield is the shield bash. Focus pays for the weapon's art (the middle button, or attacking while guarding without
/// a shield). A parry or a perfect dodge readies a counter: the next swing that lands soon after is a critical one. Swings
/// snap toward a target near the aim, follow the focused enemy (<see cref="PlayerFocusTarget"/>) while they wind up and step
/// into the blow as it lands; a wide swing strikes its targets in the order the blade reaches them, and a staggered target
/// struck from behind is backstabbed. A press during or just after a dodge, or on the run, is the weapon's rush attack, a quick
/// lunge that carries the movement into the blow. One press is buffered while a swing or a dodge is still going. It also keeps the
/// global mode in step: attacking or being attacked enters GameState.Combat, and only a stretch with no blows and no enemy after the
/// player returns to Exploration.
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

    // One target of a swing, waiting for the blade to reach it.
    private struct PendingHit
    {
        public HitDetector.Hit Hit;
        public float At;
        public AttackDefinition Attack;
        public float WeaponDamage;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Damage;
        public float Poise;
        public HitTechnique Technique;
        public bool Critical;
    }

    private readonly List<PendingHit> pendingHits = new List<PendingHit>(8);
    private readonly List<HitDetector.Hit> foundHits = new List<HitDetector.Hit>(8);

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private Damageable health;
    private PlayerDodge dodge;
    private PlayerStamina stamina;
    private PlayerGuard guard;
    private PlayerEating eating;
    private PlayerFocus focus;
    private PlayerFocusTarget focusTarget;
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
    // Still to step into the blow, a moment before it lands.
    private bool stepPending;
    // How long a heavy swing has waited at the top of its wind-up; the swing's clock stands still meanwhile.
    private float chargeHeld;
    private bool charging;
    private bool attackHeld;
    // How long the player has been running, for the running attack.
    private float runningFor;
    private float nextHuntCheck;
    private bool hunted;

    // The swing before, for judging the next press.
    private float lastStartTime = float.NegativeInfinity;
    private float lastHitAt;
    private float lastRecoverAt;
    private int lastStep = -1;

    // Input.
    private float pressTime = -1f;
    private ChainPress pressJudge = ChainPress.Late;
    private bool heavyStarted;
    // Stamina already paid for that light swing, taken off the heavy one's cost.
    private float heavyCredit;
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
        get => hasShield || Shield != null;
        set => hasShield = value;
    }

    /// <summary>The shield in the off hand (PlayerEquipment sets it), or null. Its Guard Let Through decides blocks.</summary>
    public WeaponDefinition Shield { get; set; }

    /// <summary>True while a swing is going.</summary>
    public bool IsAttacking => activeKind != SwingKind.None;

    /// <summary>When the swing in progress started (Time.time); meaningful while <see cref="IsAttacking"/>.</summary>
    public float SwingStartTime => activeStartTime;

    /// <summary>True while the swing in progress hasn't landed yet (it can still be lost or turned into another).</summary>
    public bool IsWindingUp => activeKind != SwingKind.None && hitPending;

    /// <summary>The attack of the swing in progress, or null.</summary>
    public AttackDefinition ActiveAttack => activeAttack;

    /// <summary>Seconds until the swing in progress lands; meaningful while <see cref="IsWindingUp"/>.</summary>
    public float TimeToHit => HitAt(activeAttack, activeSpeed) - ActiveElapsed;

    /// <summary>True while a heavy swing waits at the top of its wind-up, charging.</summary>
    public bool IsCharging => charging;

    /// <summary>How far a charging or charged heavy swing has got, from 0 to 1.</summary>
    public float Charge => Rules.HeavyChargeSeconds > 0f ? Mathf.Clamp01(chargeHeld / Rules.HeavyChargeSeconds) : 0f;

    /// <summary>Raised once when a held heavy swing is fully charged.</summary>
    public event Action ChargeFull;

    /// <summary>Raised when a blow lands from behind on a staggering target.</summary>
    public event Action<IDamageable> Backstabbed;

    private MonsterAI finishing;
    private Vector3 finishFrom;
    private float finishEndsAt;
    private Collider finishingCollider;
    private CharacterController controller;

    private float ActiveElapsed => Time.time - activeStartTime - chargeHeld;

    /// <summary>The guard can go up when no swing is going or the current one has already landed, and not while flinching.</summary>
    public bool CanGuard => !IsFlinching && (activeKind == SwingKind.None || !hitPending);

    /// <summary>A dodge can't cut a swing short before it lands (unless the settings allow it): swings are commitments.</summary>
    public bool CanDodge => !IsFlinching && (activeKind == SwingKind.None || !hitPending || Rules.DodgeCancelsWindup);

    /// <summary>True for a moment after a hit got through: the player reels and can't act.</summary>
    public bool IsFlinching => Time.time < flinchUntil;

    /// <summary>True in the wind-up of a heavy swing with a weapon heavy enough to trade blows: hits hurt less and don't knock it out.</summary>
    public bool IsPushingThrough => activeKind == SwingKind.Heavy && hitPending && Weapon != null && Weapon.HeavySwingArmor > 0f;

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
        controller = GetComponent<CharacterController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        dodge = GetComponent<PlayerDodge>();
        stamina = GetComponent<PlayerStamina>();
        guard = GetComponent<PlayerGuard>();
        eating = GetComponent<PlayerEating>();
        focus = GetComponent<PlayerFocus>();
        focusTarget = GetComponent<PlayerFocusTarget>();
        announcedWeapon = weapon;
    }

    private void OnEnable()
    {
        stateMachine.AttackRequested += OnAttackPressed;
        stateMachine.AttackReleased += OnAttackReleased;
        stateMachine.ShieldBashRequested += OnShieldBashRequested;
        stateMachine.ArrowCycleRequested += OnArtRequested;
        combatant.DamageDealt += OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (health != null)
        {
            health.Evaded += OnEvaded;
            health.Damaged += OnHurt;
            health.Guarded += OnGuarded;
        }

        gameState = GameStateManager.Current;
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
        EndFinish();
        stateMachine.AttackRequested -= OnAttackPressed;
        stateMachine.AttackReleased -= OnAttackReleased;
        stateMachine.ShieldBashRequested -= OnShieldBashRequested;
        stateMachine.ArrowCycleRequested -= OnArtRequested;
        combatant.DamageDealt -= OnDamageDealt;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (health != null)
        {
            health.Evaded -= OnEvaded;
            health.Damaged -= OnHurt;
            health.Guarded -= OnGuarded;
        }

        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }

        EndSwing();
        pendingHits.Clear();
        queuedKind = SwingKind.None;
        pressTime = -1f;
        attackHeld = false;
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
        if (health != null)
        {
            health.DamageTakenMultiplier = IsPushingThrough ? 1f - Weapon.HeavySwingArmor : 1f;
        }

        if (flinchUntil > 0f && !IsFlinching)
        {
            flinchUntil = 0f;
            movement.SetSpeedScale(Flinch, 1f);
        }

        UpdateRunning();
        UpdateHeldPress();
        UpdateSwing();
        ApplyPendingHits();

        if (gameState != null && gameState.CurrentState == GameState.Combat
            && StrikeRules.FightIsOver(Time.time - lastActivityTime, combatTimeout, IsHunted()))
        {
            gameState.TrySetState(GameState.Exploration);
        }
    }

    private void UpdateRunning()
    {
        PlayerMovementSettings settings = movement.Settings;
        bool running = activeKind == SwingKind.None && !movement.IsDashing && settings != null
            && movement.HorizontalSpeed >= settings.MoveSpeed * Rules.RushRunShare;
        runningFor = running ? runningFor + Time.deltaTime : 0f;
    }

    // Looked at a few times a second, only while the fight's timeout could end it.
    private bool IsHunted()
    {
        if (Time.time < nextHuntCheck)
        {
            return hunted;
        }

        nextHuntCheck = Time.time + 0.25f;
        hunted = false;
        float range = Rules.FightHoldRange;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster != null && health != null && monster.IsEngaging(health)
                && (monster.transform.position - transform.position).sqrMagnitude <= range * range)
            {
                hunted = true;
                break;
            }
        }

        return hunted;
    }

    // ----- Input -----

    private void OnAttackPressed()
    {
        if (weapon != null && weapon.TakesAttackInput)
        {
            return; // PlayerArchery draws and shoots; PlayerFishing casts
        }

        if (guard != null && guard.IsGuarding)
        {
            if (HasShield)
            {
                Request(SwingKind.ShieldBash, ChainPress.Late);
                return;
            }

            // Without a shield, attacking drops the guard: into the weapon art if Focus pays for it, else into a swing (a
            // ready counter is spent here).
            guard.Lower();
            if (CanAffordArt)
            {
                Request(SwingKind.Art, ChainPress.Late);
                return;
            }
        }

        attackHeld = true;
        if (WantsRush())
        {
            pressTime = -1f;
            Request(SwingKind.Rush, ChainPress.Late);
            return;
        }

        pressJudge = JudgePress();
        if (pressJudge == ChainPress.Early && Rules.EarlyPressFumbles && activeKind == SwingKind.Light)
        {
            fumbled = true;
        }

        ChainJudged?.Invoke(pressJudge);

        // The light swing starts on the press, so a tap is answered at once (waiting for the release to tell a tap from a
        // hold made every light swing late). Held on, UpdateHeldPress turns it into the heavy one.
        Request(SwingKind.Light, pressJudge);
        bool hasHeavy = weapon != null && weapon.HeavyAttack != null;
        pressTime = hasHeavy ? Time.time : -1f;
        heavyStarted = false;
    }

    private void OnAttackReleased()
    {
        attackHeld = false;
        if (pressTime < 0f || weapon != null && weapon.TakesAttackInput)
        {
            return;
        }

        pressTime = -1f;
    }

    // An attack out of a dodge, or on the run toward where it aims, is the weapon's rush attack.
    private bool WantsRush()
    {
        if (weapon == null || weapon.RushAttack == null || activeKind != SwingKind.None)
        {
            return false;
        }

        if (dodge != null && (dodge.IsDodging || dodge.JustDodged(Rules.RushAfterDodgeSeconds)))
        {
            return true;
        }

        return StrikeRules.IsRunningAttack(runningFor, Rules.RushRunSeconds, Vector3.Angle(movement.HorizontalVelocity, movement.AimDirection), Rules.RushRunAngle);
    }

    private bool CanAffordArt => weapon != null && weapon.WeaponArt != null && !weapon.TakesAttackInput
        && focus != null && focus.CanSpend(weapon.WeaponArtFocus);

    // The middle button: the next arrow with a bow (PlayerArchery), the weapon's art with anything else.
    private void OnArtRequested()
    {
        if (weapon == null || weapon.TakesAttackInput || weapon.WeaponArt == null)
        {
            return;
        }

        if (focus != null && !focus.CanSpend(weapon.WeaponArtFocus))
        {
            focus.TrySpend(weapon.WeaponArtFocus); // refused: lets the HUD say so
            return;
        }

        if (guard != null && guard.IsGuarding)
        {
            guard.Lower();
        }

        Request(SwingKind.Art, ChainPress.Late);
    }

    private void UpdateHeldPress()
    {
        if (pressTime < 0f || heavyStarted)
        {
            return;
        }

        if (Time.time - pressTime < Rules.HeavyHoldSeconds)
        {
            return;
        }

        // Still winding up the light swing this press began (at once, or buffered behind the swing before): it becomes the
        // heavy one, paying only the difference. A counter stays a counter, and without the stamina for the difference the
        // light swing goes on.
        bool turn = activeKind == SwingKind.Light && activeStartTime >= pressTime && hitPending && (activeTechnique & HitTechnique.Counter) == 0;
        heavyStarted = true;
        pressTime = -1f;
        if (turn)
        {
            float credit = CostOf(SwingKind.Light, activeAttack);
            float extra = Mathf.Max(0f, CostOf(SwingKind.Heavy, weapon.HeavyAttack) - credit);
            if (stamina != null && stamina.Current < extra)
            {
                return;
            }

            EndSwing();
            heavyCredit = credit;
            Begin(SwingKind.Heavy, ChainPress.Late);
            heavyCredit = 0f;
            return;
        }

        // A light swing that has already landed (or a press buffered behind another swing): the heavy one follows.
        Request(SwingKind.Heavy, ChainPress.Late);
    }

    // Kept for gamepads and scripts that ask for a bash directly.
    private void OnShieldBashRequested()
    {
        if (HasShield)
        {
            Request(SwingKind.ShieldBash, ChainPress.Late);
        }
    }

    private ChainPress JudgePress()
    {
        if (activeKind != SwingKind.None)
        {
            return ComboRules.Judge(ActiveElapsed, HitAt(activeAttack, activeSpeed), RecoverAt(activeAttack, activeSpeed), Rules);
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
            case SwingKind.Art:
                attack = weapon != null ? weapon.WeaponArt : null;
                technique |= HitTechnique.WeaponArt;
                // Focus may have run out while it waited in the buffer.
                if (attack == null || (focus != null && !focus.TrySpend(weapon.WeaponArtFocus)))
                {
                    return;
                }

                break;
            case SwingKind.Rush:
                // Out of a perfect dodge the counter is the better blow; a rush leads on into the chain's second swing.
                attack = CounterIsReady && weapon != null && weapon.CounterAttack != null ? weapon.CounterAttack : weapon != null ? weapon.RushAttack : null;
                if (dodge != null)
                {
                    dodge.CutShort();
                }

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

        MonsterAI toFinish = kind == SwingKind.Light || kind == SwingKind.Rush ? FindFinishable() : null;
        if (toFinish != null)
        {
            // The weapon's own rush: same numbers and time as any, so a finishing blow changes the moment and not the balance.
            kind = SwingKind.Rush;
            attack = weapon.RushAttack;
            step = 0;
        }

        // Pay the full cost when the swing actually starts, rechecking affordability for buffered swings.
        if (stamina != null && !stamina.TrySpend(Mathf.Max(0f, CostOf(kind, attack) - heavyCredit)))
        {
            return;
        }

        fumbled = false;
        rhythm = kind == SwingKind.Light && step > 0 ? ComboRules.NextRhythm(rhythm, judge, Rules) : 0;
        bool finisher = kind == SwingKind.Light && chain > 1 && step == chain - 1;
        lastStep = kind == SwingKind.Rush ? 0 : kind == SwingKind.Light && !finisher ? step : -1;

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
        chargeHeld = 0f;
        charging = false;
        hitPending = attack != null;
        stepPending = attack != null && attack.Lunge > 0f;
        // Toward the aim, or the target it snaps to; while it winds up it may still follow the focused enemy.
        if (focusTarget != null)
        {
            focusTarget.Refresh();
        }

        activeDirection = toFinish != null ? toFinish.transform.forward : AimAssist(movement.AimDirection, attack, out _);
        movement.SnapFacing(activeDirection);
        if (toFinish != null)
        {
            stepPending = false;
            BeginFinish(toFinish, HitAt(attack, activeSpeed));
        }

        // Planted, facing the blow, until it lands: legs running under a swing and a body skating through one both look wrong.
        movement.FacingLocked = true;
        movement.SetSpeedScale(this, Rules.WindupMoveSpeed);
        if (!hitPending)
        {
            ReleaseFeet();
        }
        else if (kind == SwingKind.Rush && stepPending)
        {
            // A rush carries the movement it came with: the lunge runs for the whole wind-up instead of waiting for the strike.
            StepIntoBlow(HitAt(attack, activeSpeed));
        }

        EnterCombat();

        AttackDefinition looks = toFinish != null && Rules.FinishStab != null ? Rules.FinishStab : attack;
        current = new SwingInfo(kind, looks, step, chain, rhythm, activeSpeed, technique, activeDirection);
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
        if (stamina == null || kind == SwingKind.Art)
        {
            return 0f;
        }

        if (attack != null && attack.StaminaCost >= 0f)
        {
            return attack.StaminaCost;
        }

        return kind == SwingKind.ShieldBash ? stamina.ShieldBashCost : kind == SwingKind.Heavy ? stamina.AttackCost * 2f : stamina.AttackCost;
    }

    // ----- A finishing blow -----

    private MonsterAI FindFinishable()
    {
        if (weapon == null || weapon.RushAttack == null)
        {
            return null;
        }

        CombatTechniqueSettings rules = Rules;
        MonsterAI best = null;
        float bestDistance = float.MaxValue;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null)
            {
                continue;
            }

            Vector3 at = monster.transform.position;
            if (!StrikeRules.CanFinish(monster.IsStaggered, monster.CanBeFinished, at, monster.transform.forward, transform.position, rules.BackstabArc, rules.FinishReach))
            {
                continue;
            }

            float distance = (at - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monster;
            }
        }

        return best;
    }

    private void BeginFinish(MonsterAI target, float hitAt)
    {
        finishing = target;
        finishFrom = transform.position;
        finishEndsAt = Time.time + hitAt + Rules.FinishHoldSeconds;
        // Past each other's bodies for the stab, and not to be knocked out of it before the blade lands.
        finishingCollider = target.GetComponentInChildren<Collider>();
        if (finishingCollider != null && controller != null)
        {
            Physics.IgnoreCollision(controller, finishingCollider, true);
        }

        if (health != null)
        {
            health.AddInvulnerability(this);
        }

        target.HoldForFinish(hitAt + Rules.FinishHoldSeconds + Rules.FinishCloseSeconds, hitAt);
        FinishStarted?.Invoke(target);
    }

    private void UpdateFinish()
    {
        if (finishing == null)
        {
            return;
        }

        if (Time.time >= finishEndsAt)
        {
            EndFinish();
            return;
        }

        CombatTechniqueSettings rules = Rules;
        Vector3 spot = StrikeRules.FinishSpot(finishing.transform.position, finishing.transform.forward, rules.FinishGap);
        float share = Mathf.Clamp01((Time.time - activeStartTime) / rules.FinishCloseSeconds);
        Vector3 wanted = Vector3.Lerp(finishFrom, spot, share * share * (3f - 2f * share));
        Vector3 move = wanted - transform.position;
        move.y = 0f;
        if (controller != null && controller.enabled)
        {
            controller.Move(move);
        }

        movement.SnapFacing(finishing.transform.forward);
    }

    private void EndFinish()
    {
        if (finishingCollider != null && controller != null)
        {
            Physics.IgnoreCollision(controller, finishingCollider, false);
        }

        finishingCollider = null;
        finishing = null;
        if (health != null)
        {
            health.RemoveInvulnerability(this);
        }
    }

    /// <summary>True while the two bodies are locked together.</summary>
    public bool IsFinishing => finishing != null;

    /// <summary>A finishing blow has begun on this creature.</summary>
    public event Action<MonsterAI> FinishStarted;

    private void UpdateSwing()
    {
        UpdateFinish();
        if (activeKind == SwingKind.None)
        {
            bool dodgeInTheWay = dodge != null && dodge.IsDodging && !(queuedKind == SwingKind.Rush && dodge.CanBeCutShort);
            if (queuedKind != SwingKind.None && !IsFlinching && !dodgeInTheWay && (guard == null || !guard.IsStunned))
            {
                Begin(queuedKind, queuedJudge);
            }

            return;
        }

        float hitAt = HitAt(activeAttack, activeSpeed);
        if (UpdateCharge(hitAt))
        {
            return;
        }

        float elapsed = ActiveElapsed;
        if (hitPending && elapsed < hitAt - LungeSeconds)
        {
            TrackFocus();
        }
        else if (hitPending && stepPending)
        {
            StepIntoBlow(hitAt - elapsed);
        }

        if (hitPending && elapsed >= hitAt)
        {
            hitPending = false;
            int hits = LandSwing();
            ReleaseFeet();
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

    // A heavy swing held on waits at the top of its wind-up, where its strike would begin, and everything about it stands
    // still until the button is let go or the charge is full. True while it waits.
    private bool UpdateCharge(float hitAt)
    {
        float most = Rules.HeavyChargeSeconds;
        bool waits = activeKind == SwingKind.Heavy && hitPending && attackHeld && most > 0f && chargeHeld < most
            && ActiveElapsed >= hitAt - Mathf.Max(activeAttack.StrikeLead, LungeSeconds);
        if (!waits)
        {
            charging = false;
            return false;
        }

        charging = true;
        chargeHeld = Mathf.Min(most, chargeHeld + Time.deltaTime);
        TrackFocus();
        if (chargeHeld >= most)
        {
            charging = false;
            ChargeFull?.Invoke();
        }

        return charging;
    }

    // Until it steps into the blow, a swing keeps turning toward the enemy it is focused on.
    private void TrackFocus()
    {
        if (focusTarget == null || !focusTarget.HasFocus || Rules.SwingTracking <= 0f)
        {
            return;
        }

        Vector3 to = focusTarget.DirectionToTarget;
        activeDirection = Vector3.RotateTowards(activeDirection, to, Rules.SwingTracking * Mathf.Deg2Rad * Time.deltaTime, 0f);
        movement.SnapFacing(activeDirection);
    }

    // The last moment before the blow: the body travels with the strike, as far as the target (or half way with none), so the
    // step ends as it lands instead of sliding at the start and then standing still through the swing.
    private void StepIntoBlow(float secondsLeft)
    {
        stepPending = false;
        activeDirection = AimAssist(activeDirection, activeAttack, out float targetDistance);
        movement.SnapFacing(activeDirection);
        float distance = targetDistance >= 0f ? Mathf.Clamp(targetDistance - LungeStandOff, 0f, activeAttack.Lunge) : activeAttack.Lunge * 0.5f;
        float seconds = Mathf.Max(0.05f, secondsLeft);
        movement.Lunge(activeDirection * (distance / seconds), seconds);
    }

    private int LandSwing()
    {
        if (activeAttack == null)
        {
            return 0;
        }

        float charge = activeKind == SwingKind.Heavy ? Charge : 0f;
        float damage = activeDamageMultiplier * Mathf.Lerp(1f, Rules.HeavyChargeDamage, charge);
        float poise = activePoiseMultiplier * Mathf.Lerp(1f, Rules.HeavyChargePoise, charge);
        Vector3 origin = transform.position;
        HitDetector.FindTargets(origin, activeDirection, activeAttack, combatant.Faction, hitLayers, foundHits);

        float first = float.MaxValue;
        float sweep = StrikeRules.SweepSeconds(activeAttack.Arc, Rules.SweepSeconds);
        foreach (HitDetector.Hit hit in foundHits)
        {
            first = Mathf.Min(first, StrikeRules.SweepPosition(activeDirection, hit.Point - origin, activeAttack.Arc, activeAttack.SlashSweep));
        }

        foreach (HitDetector.Hit hit in foundHits)
        {
            float through = StrikeRules.SweepPosition(activeDirection, hit.Point - origin, activeAttack.Arc, activeAttack.SlashSweep);
            pendingHits.Add(new PendingHit
            {
                Hit = hit,
                At = Time.time + (through - first) * sweep,
                Attack = activeAttack,
                WeaponDamage = activeWeaponDamage,
                Origin = origin,
                Direction = activeDirection,
                Damage = damage,
                Poise = poise,
                Technique = activeTechnique,
                Critical = activeForceCritical,
            });
        }

        ApplyPendingHits();
        return foundHits.Count;
    }

    private void ApplyPendingHits()
    {
        for (int i = pendingHits.Count - 1; i >= 0; i--)
        {
            PendingHit pending = pendingHits[i];
            if (Time.time < pending.At)
            {
                continue;
            }

            pendingHits.RemoveAt(i);
            Strike(pending);
        }
    }

    private void Strike(in PendingHit pending)
    {
        IDamageable target = pending.Hit.Target;
        // It may have died, or gone, in the moment the blade took to reach it.
        if (target == null || (target is UnityEngine.Object living && living == null) || !target.IsAlive)
        {
            return;
        }

        float damage = pending.Damage;
        HitTechnique technique = pending.Technique;
        bool critical = pending.Critical;
        bool backstab = IsBackstab(target, out MonsterAI backstabbed);
        if (backstab)
        {
            // A stagger is earned, and pays most; catching it mid-strike pays less, and no sure critical.
            damage *= backstabbed.IsStaggered ? Rules.BackstabDamage : Rules.BackstabOnStrikeDamage;
            technique |= HitTechnique.Backstab;
            critical |= backstabbed.IsStaggered;
        }

        AttackResolver.Apply(combatant, pending.Attack, pending.WeaponDamage, pending.Hit, pending.Origin, pending.Direction, damage,
            pending.Poise, technique, critical);
        if (backstab)
        {
            Backstabbed?.Invoke(target);
        }
    }

    private bool IsBackstab(IDamageable target, out MonsterAI monster)
    {
        monster = null;
        return target is Component body && body.TryGetComponent(out monster) && monster.IsOpenToBackstab
            && StrikeRules.IsBehind(body.transform.position, body.transform.forward, transform.position, Rules.BackstabArc);
    }

    // The blow has landed: the rest is follow-through, walked out of at the rules' speed. At 0 the player stays planted
    // and facing the blow until the swing ends. The swing itself goes on, so walking off doesn't bring the next attack sooner.
    private void ReleaseFeet()
    {
        float speed = activeKind == SwingKind.Heavy ? Rules.HeavyMoveSpeed : Rules.SwingMoveSpeed;
        movement.FacingLocked = speed <= 0f;
        movement.SetSpeedScale(this, speed);
    }

    private void EndSwing()
    {
        EndFinish();
        if (activeKind != SwingKind.None)
        {
            lastStartTime = activeStartTime + chargeHeld;
            lastHitAt = HitAt(activeAttack, activeSpeed);
            lastRecoverAt = RecoverAt(activeAttack, activeSpeed);
        }

        activeKind = SwingKind.None;
        activeAttack = null;
        hitPending = false;
        stepPending = false;
        charging = false;
        chargeHeld = 0f;
        movement.FacingLocked = false;
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

    /// <summary>Uses up a ready counter without a swing (a bow's instant shot after a perfect dodge).</summary>
    public void SpendCounter() => counterUntil = float.NegativeInfinity;

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
            if (target == null || !target.IsAlive || target.Faction.AreAllies(combatant.Faction) || target.Category != TargetCategory.Creature)
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
        // Being attacked is a fight, whether or not the player hits back.
        if (!damage.IsOverTime && damage.Source != null)
        {
            EnterCombat();
        }

        if (result.Blocked || result.Killed || damage.IsOverTime || health == null || result.Damage < health.MaxHealth * Rules.FlinchShare || IsPushingThrough)
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

    /// <summary>Thrown back by a monster's parry of the blow that just landed. The swing has already been spent, so it plays out.</summary>
    public void Rebuff(float seconds)
    {
        queuedKind = SwingKind.None;
        pressTime = -1f;
        flinchUntil = Time.time + seconds;
        movement.SetSpeedScale(Flinch, 0.25f);
        Flinched?.Invoke(seconds, false);
    }

    // Key for the flinch's slowdown, separate from the swing's own.
    private static readonly object Flinch = new object();

    // A hit evaded during the dodge's invulnerability is a perfect dodge: it readies a counter.
    private void OnGuarded(DamageInfo damage, GuardOutcome outcome) => EnterCombat();

    private void OnEvaded(DamageInfo damage)
    {
        if (damage.Source != null)
        {
            EnterCombat();
        }

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

    /// <summary>Enters (or stays in) GameState.Combat as if attacking, e.g. while drawing a bow.</summary>
    public void KeepInCombat() => EnterCombat();

    private void EnterCombat()
    {
        lastActivityTime = Time.time;
        if (gameState != null)
        {
            gameState.TrySetState(GameState.Combat);
        }
    }
}
