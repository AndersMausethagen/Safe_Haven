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
    /// <summary>A quick lunge out of a dodge or on the run (out of a dodge away from the aim: the weapon's backstep attack).</summary>
    Rush,
    /// <summary>Attacking out of the guard just after a blow was blocked on it.</summary>
    GuardCounter,
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
/// lunge that carries the movement into the blow. One press is buffered while a swing or a dodge is still going. Fighting keeps
/// the global mode in step (<see cref="PlayerCombatMode"/>); where a swing aims and what it can finish is
/// <see cref="MeleeTargeting"/>'s.
/// <para>
/// The state is here; its behaviour is in parts by concern: PlayerCombat.Input (presses), .Swings (a swing from start to
/// recovery), .Finishers (the finishing blow) and .Reactions (dodges, flinches, counters).
/// </para>
/// <para>
/// Animation listens to <see cref="SwingStarted"/>; feedback to <see cref="SwingLanded"/>, <see cref="ChainJudged"/>,
/// <see cref="CounterReady"/> and <see cref="PerfectDodge"/>; damage numbers, audio and loot listen to the targets and
/// the Combatant, so none of them are known here.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Combatant))]
public sealed partial class PlayerCombat : MonoBehaviour
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
    private PlayerCombatMode mode;
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
    // How the swing lets the feet move (SwingMotion): its share of speed while winding up, how long before the blow it steps in,
    // and whether the player has their legs back after it landed.
    private float activeWindupMove;
    private float activeLungeLead;
    private bool feetReleased;
    // The bounded buffer: a press dropped for coming too early, which counts once the window opens if the button is still held.
    private bool heldEarlyPress;
    // No new attack before this: a dodge cut a wind-up short (Cancel Lockout).
    private float attackLockedUntil = float.NegativeInfinity;
    // Context attacks: which way the last dodge went (a backstep away from the aim), when a heavy last landed and whether it was the
    // follow-up, and an attack the next Begin must use (a full charge's release).
    private Vector3 lastDodgeDirection;
    private float lastHeavyEndedAt = float.NegativeInfinity;
    private bool lastHeavyWasFollowUp;
    private AttackDefinition forcedAttack;
    private bool activeIsFollowUp;
    // How far the swing in progress steps into its blow (its attack's Lunge, or the rush's for a Riposte counter).
    private float activeLunge;
    private bool counterLunges;
    // How long a heavy swing has waited at the top of its wind-up; the swing's clock stands still meanwhile.
    private float chargeHeld;
    private bool charging;
    private bool attackHeld;
    // How long the player has been running, for the running attack.
    private float runningFor;

    // The swing before, for judging the next press.
    private float lastStartTime = float.NegativeInfinity;
    private float lastHitAt;
    private float lastRecoverAt;
    private int lastStep = -1;

    // Input.
    private float pressTime = -1f;
    private ChainPress pressJudge = ChainPress.Late;
    private bool heavyStarted;
    // The art now starting is paid in stamina, Focus being short.
    private bool artOnStamina;
    // Stamina already paid for that light swing, taken off the heavy one's cost.
    private float heavyCredit;
    private SwingKind queuedKind;
    private ChainPress queuedJudge;

    // Technique.
    private bool fumbled;
    private int rhythm;
    private float counterUntil = float.NegativeInfinity;
    private float flinchUntil = float.NegativeInfinity;
    // No new flinch or stumble before this (Flinch Grace), and invulnerable until this after a heavy hit.
    private float flinchGraceUntil = float.NegativeInfinity;
    private float heavyHitSafeUntil = float.NegativeInfinity;
    private bool heavyHitSafe;
    // Key for the flinch's slowdown, separate from the swing's own.
    private static readonly object Flinch = new object();

    public WeaponDefinition Weapon
    {
        get => weapon;
        set
        {
            weapon = value;
            lastStep = -1;
            momentum = 0;
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

    /// <summary>
    /// While a held press turns its light swing into the heavy one: how far through its wind-up the light swing had got (0-0.8), so the
    /// heavy one's animation takes up from there instead of starting over (PlayerAnimationController). 0 otherwise.
    /// </summary>
    public float HeavyGrownFrom { get; private set; }

    /// <summary>Share of walking speed the swing in progress keeps while it winds up (0: planted).</summary>
    public float WindupMoveShare => IsWindingUp ? activeWindupMove : 0f;

    /// <summary>True while the swing steps into its blow (a rush all through its wind-up): the legs are the clip's own then.</summary>
    public bool IsSteppingIn => IsWindingUp && activeLunge > 0f && !stepPending;

    /// <summary>True while a heavy swing waits at the top of its wind-up, charging.</summary>
    public bool IsCharging => charging;

    /// <summary>How far a charging or charged heavy swing has got, from 0 to 1.</summary>
    public float Charge => Rules.HeavyChargeSeconds > 0f ? Mathf.Clamp01(chargeHeld / Rules.HeavyChargeSeconds) : 0f;

    /// <summary>Raised once when a held heavy swing is fully charged.</summary>
    public event Action ChargeFull;

    /// <summary>Raised when a blow lands from behind on a staggering target.</summary>
    public event Action<IDamageable> Backstabbed;

    /// <summary>True while the two bodies are locked together.</summary>
    public bool IsFinishing => finishing != null;

    /// <summary>A finishing blow has begun on this creature.</summary>
    public event Action<MonsterAI> FinishStarted;

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
    public bool IsPushingThrough => hitPending && Weapon != null && Weapon.HeavySwingArmor > 0f
        && (activeKind == SwingKind.Heavy
            // A greatsword's Hyper Armour: its finisher and charged blows push through as its heavy does.
            || (Weapon.Signature == WeaponSignature.HyperArmour && (activeTechnique & (HitTechnique.Finisher | HitTechnique.Charged)) != 0));

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

    /// <summary>The next blow that would make the player flinch doesn't (Unyielding, a great enchantment); spent by that blow.</summary>
    public bool FlinchWarded { get; set; }

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
        mode = new PlayerCombatMode(transform);
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

        mode.Attach(this);
    }

    private void OnDisable()
    {
        EndFinish();
        if (heavyHitSafe && health != null)
        {
            heavyHitSafe = false;
            health.RemoveInvulnerability(this);
        }

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

        mode.Detach();
        EndSwing();
        pendingHits.Clear();
        queuedKind = SwingKind.None;
        pressTime = -1f;
        attackHeld = false;
        heldEarlyPress = false;
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
            // Written every frame, so it has to carry everything that scales what the player takes: the Damage Taken assist
            // (Options) as well as a heavy weapon pushing through. Setting only the latter put the assist back to 1 each frame.
            health.DamageTakenMultiplier = DamageTakenScale(GameOptions.DamageTakenShare, IsPushingThrough ? Weapon.HeavySwingArmor : 0f);
        }

        if (flinchUntil > 0f && !IsFlinching)
        {
            flinchUntil = 0f;
            movement.SetSpeedScale(Flinch, 1f);
        }

        if (heavyHitSafe && Time.time >= heavyHitSafeUntil && health != null)
        {
            heavyHitSafe = false;
            health.RemoveInvulnerability(this);
        }

        UpdateRunning();
        UpdateEarlyPress();
        UpdateHeldPress();
        UpdateSwing();
        ApplyPendingHits();
        mode.Tick(combatTimeout);
    }

    /// <summary>What multiplies the damage the player takes: the assist's share, less what a heavy swing shrugs off.</summary>
    public static float DamageTakenScale(float assistShare, float heavySwingArmor) =>
        Mathf.Max(0f, assistShare) * (1f - Mathf.Clamp01(heavySwingArmor));

    private void UpdateRunning()
    {
        PlayerMovementSettings settings = movement.Settings;
        bool running = activeKind == SwingKind.None && !movement.IsDashing && settings != null
            && movement.HorizontalSpeed >= settings.MoveSpeed * Rules.RushRunShare;
        runningFor = running ? runningFor + Time.deltaTime : 0f;
    }

    private void AnnounceWeapon()
    {
        announcedWeapon = weapon;
        WeaponChanged?.Invoke(weapon);
    }
}
