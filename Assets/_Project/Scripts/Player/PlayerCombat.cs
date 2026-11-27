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
    /// <summary>The least time a charged attack let go from the hold (ChargedFromHold) takes to land: the animation's blend into it
    /// (PlayerAnimationController) is this long, so the blow never lands mid-blend.</summary>
    /// (0.04 s snapped from the held heavy's pose to the charged one's backswing, 2026-11-17: now long enough to read as one motion.)
    internal const float ChargedReleaseSeconds = 0.12f;
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
    // The moment the swing really began (its clock, activeStartTime, may start earlier: a head start into its wind-up).
    private float activeBeganAt;
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
    private float heldPastFull;

    // A charge held on past full (the blink): it is kept, at a stamina cost, but no longer guards or shrugs off blows (review 2026-11-20:
    // held without end, the heavy's armour and a shield's guard point made a stance that lasted as long as the stamina did).
    private bool HeldPastFull => charging && heldPastFull > 0f;
    // Seconds the light swing a press began has waited at the top of its swing for the hold to decide (WaitsForHold).
    private float holdWaited;
    private bool waitingForHold;
    private bool charging;
    private float attackPressedAt = float.NegativeInfinity;
    // The end of a recovery the guard cut short (GiveWayToGuard): no new swing before it, but for a counter.
    private float recoveryOwedUntil = float.NegativeInfinity;

    /// <summary>True while a recovery the guard cut short is still owed: attacks wait (a ready counter doesn't).</summary>
    public bool OwesRecovery => Time.time < recoveryOwedUntil && !CounterIsReady;
    private bool attackHeld;
    // How long the player has been running, for the running attack.
    private float runningFor;

    // The swing before, for judging the next press.
    private float lastStartTime = float.NegativeInfinity;
    private float lastHitAt;
    private float lastRecoverAt;
    // The swing waiting on its last pose after its recovery (Swing Hold Seconds), and a lunge whose follow-up has already chained.
    private bool holdingPose;
    private float lungeLedFrom = float.NegativeInfinity;
    private PlayerSkills skills;
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
    // No new flinch or stumble before this (Flinch Grace), and invulnerable until this after a heavy hit.
    private float flinchGraceUntil = float.NegativeInfinity;
    private float heavyHitSafeUntil = float.NegativeInfinity;
    private bool heavyHitSafe;
    // Key for the flinch's slowdown, separate from the swing's own.
    private static readonly LeaseKey Flinch = new LeaseKey("a flinch");

    public WeaponDefinition Weapon
    {
        get => weapon;
        set
        {
            if (value != weapon)
            {
                DropSwingForSwap();
            }

            weapon = value;
            lastStep = -1;
            momentum = 0;
            // A sword's Counter Chain readies its extra counters for the sword: a ready counter stays one for the weapon taken up.
            counterCharges = Mathf.Min(counterCharges, 1);
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

    /// <summary>A weapon art under way, its blow still to land (ArtBladeView lights the blade through it).</summary>
    public bool ArtBlowPending => activeKind == SwingKind.Art && hitPending;

    /// <summary>When the swing in progress started (Time.time); meaningful while <see cref="IsAttacking"/>.</summary>
    public float SwingStartTime => activeStartTime;

    /// <summary>True while the swing in progress hasn't landed yet (it can still be lost or turned into another).</summary>
    public bool IsWindingUp => activeKind != SwingKind.None && hitPending;

    /// <summary>
    /// A shield on the arm guards the swing (guard points, CombatTechniqueSettings › Shield Guard Points): while a melee swing winds
    /// up to its hit or a heavy is held charging, the shield takes a blow from the front. Not an art, a thrown weapon or a bow.
    /// </summary>
    public bool InGuardPoint => Rules.ShieldGuardPoints && Shield != null && hitPending && !HeldPastFull && weapon != null && !weapon.TakesAttackInput
                                && (activeKind == SwingKind.Light || activeKind == SwingKind.Heavy || activeKind == SwingKind.Rush
                                    || activeKind == SwingKind.ShieldBash || activeKind == SwingKind.GuardCounter);

    /// <summary>The attack of the swing in progress, or null.</summary>
    public AttackDefinition ActiveAttack => activeAttack;

    /// <summary>Seconds until the swing in progress lands; meaningful while <see cref="IsWindingUp"/>.</summary>
    public float TimeToHit => HitAt(activeAttack, activeSpeed) - ActiveElapsed;

    /// <summary>
    /// While a held press turns its light swing into the heavy one: how far through its wind-up the light swing had got (0-0.8), so the
    /// heavy one's animation takes up from there instead of starting over (PlayerAnimationController). 0 otherwise.
    /// </summary>
    public float HeavyGrownFrom { get; private set; }

    /// <summary>
    /// Asked as a swing begins, before its clock starts (PlayerAnimationController): the share of its wind-up its clip picks up from
    /// because the pose before it is already that far in (out of the guard, a heavy chain), 0-0.8. Null or 0: from its first frame.
    /// </summary>
    public Func<SwingKind, AttackDefinition, float> WindupTakenUp;

    /// <summary>
    /// The share of the swing's wind-up it began past (a heavy grown from the light, out of the guard, a heavy chain): its clock starts
    /// that far in, so the blow comes that much sooner and the clip plays on at its own pace (the user, 2026-11-25: no slowed
    /// wind-ups to hold the timing). 0 for a swing from its first frame and for a charged release (ChargedFromHold has its own).
    /// </summary>
    public float WindupTaken { get; private set; }

    /// <summary>
    /// While a full charge hands over to the weapon's charged attack: the hold was its wind-up, so it goes straight into the strike
    /// from the top of its backswing (PlayerAnimationController starts its clip there). False otherwise.
    /// </summary>
    public bool ChargedFromHold { get; private set; }

    /// <summary>Share of walking speed the swing in progress keeps while it winds up (0: planted).</summary>
    public float WindupMoveShare => !IsWindingUp ? 0f : charging ? Rules.ChargeMoveSpeed : activeWindupMove;

    /// <summary>True while the swing steps into its blow (a rush all through its wind-up): the legs are the clip's own then.</summary>
    public bool IsSteppingIn => IsWindingUp && activeLunge > 0f && !stepPending;

    /// <summary>True while a heavy swing waits at the top of its wind-up, charging.</summary>
    public bool IsCharging => charging;

    /// <summary>True while the swing stands still at the top of its wind-up: a heavy charging, or a light one waiting to see whether
    /// its press becomes a heavy (the animation holds its pose).</summary>
    public bool IsHoldingSwing => charging || waitingForHold;

    /// <summary>How far a charging or charged heavy swing has got, from 0 to 1.</summary>
    public float Charge => Rules.HeavyChargeSeconds > 0f ? Mathf.Clamp01(chargeHeld / Rules.HeavyChargeSeconds) : 0f;

    /// <summary>Raised once when a held heavy swing is fully charged.</summary>
    public event Action ChargeFull;

    /// <summary>Raised when a blow lands from behind on a staggering target.</summary>
    public event Action<IDamageable> Backstabbed;

    /// <summary>True while the two bodies are locked together.</summary>
    public bool IsFinishing => finishing != null;

    /// <summary>The stamina the last swing begun took from the bar (0 for one already paid for, or an art paid in Focus).</summary>
    public float LastSwingStaminaPaid { get; private set; }

    /// <summary>Weapon arts begun so far, so what answers an art's landing can tell one art from the next.</summary>
    public int ArtsBegun { get; private set; }

    /// <summary>The Focus the last art begun was paid with (the rune's or the weapon's).</summary>
    public float ArtFocusPaid { get; private set; }

    /// <summary>A finishing blow has begun on this creature.</summary>
    public event Action<MonsterAI> FinishStarted;

    private MonsterAI finishing;
    private Vector3 finishFrom;
    private float finishEndsAt;
    private Collider finishingCollider;
    private CharacterController controller;

    // The swing's own clock: stopped while it charges (chargeHeld, then heldPastFull once full) or waits for the hold to be told.
    private float ActiveElapsed => Time.time - activeStartTime - chargeHeld - heldPastFull - holdWaited;

    /// <summary>
    /// The guard can go up for a press made at <paramref name="pressedAt"/>, not while flinching: with no swing going; in a swing's
    /// follow-through when its recovery cancels allow it; and in its wind-up when its wind-up cancels allow it (a light swing by
    /// default, the per-move cancel table, the user 2026-11-20) or to break off a heavy swing being held (charging, or winding up to
    /// it; the user, 2026-11-20: blocking out of a charge played the held blow out at speed and then jumped to the block). A wind-up is
    /// only broken off by a press made after the attack's: a guard held since before it (attacking out of the guard, both buttons held)
    /// leaves it alone.
    /// </summary>
    public bool CanGuardFrom(float pressedAt) => !IsFlinching && (activeKind == SwingKind.None
        || (hitPending ? (BreaksOffIntoGuard || CancelsNow(SwingCancels.Guard)) && pressedAt > attackPressedAt : CancelsNow(SwingCancels.Guard)));

    // A heavy swing still held before its blow: charging at the top of its wind-up, or on its way there.
    private bool BreaksOffIntoGuard => charging || (activeKind == SwingKind.Heavy && hitPending && attackHeld && (activeTechnique & HitTechnique.Charged) == 0);

    // Whether the swing going now gives way to this: its wind-up's cancels before the blow, its recovery's after (the per-move cancel
    // table, SwingMotion).
    private bool CancelsNow(SwingCancels what) => activeKind == SwingKind.None || holdingPose
        || ((hitPending ? SwingMotion.WindupCancels(activeAttack, activeKind, Rules) : SwingMotion.RecoveryCancels(activeAttack, Rules)) & what) != 0;

    /// <summary>A dodge gets out of a swing when its cancels allow it (by default all through it: Dodge Cancels Windup).</summary>
    public bool CanDodge => (!IsFlinching || InCuttableFlinchTail) && CancelsNow(SwingCancels.Dodge);

    /// <summary>True for a moment after a hit got through: the player reels and can't act.</summary>
    public bool IsFlinching => Time.time < flinchUntil;

    /// <summary>True in the wind-up of a heavy swing with a weapon heavy enough to trade blows: hits hurt less and don't knock it out.</summary>
    public bool IsPushingThrough => hitPending && !HeldPastFull && Weapon != null && Weapon.HeavySwingArmor > 0f
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

    /// <summary>The swing began (true) or stopped (false) waiting on its last pose after its recovery (Swing Hold Seconds).</summary>
    public event Action<bool> SwingHeld;

    /// <summary>True while a recovered swing waits on its last pose for the next press.</summary>
    public bool IsHoldingPose => holdingPose;

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
    // How many the swing under way struck: -1 before its blow lands.
    private int landedThisSwing = -1;

    /// <summary>In the recovery of a heavy, charged or art swing that struck nothing (Docs/EnemyAIReview_2026-11-19.md, a committed
    /// whiff): from tier II, a creature standing ready takes the opening at once.</summary>
    public bool IsSpent => IsAttacking && !hitPending && landedThisSwing == 0
        && (current.Technique & (HitTechnique.Heavy | HitTechnique.Charged | HitTechnique.WeaponArt)) != 0;

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
        skills = GetComponent<PlayerSkills>();
        mode = new PlayerCombatMode(transform);
        announcedWeapon = weapon;
    }

    private void OnEnable()
    {
        stateMachine.AttackRequested += OnAttackPressed;
        stateMachine.AttackReleased += OnAttackReleased;
        stateMachine.AttackKeyTaken += OnAttackKeyTaken;
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
            health.Died += OnDied;
        }

        mode.Attach(this);
    }

    // Fallen: the swing under way, its blows still to land and any press waiting are all gone. A blow landing after death dealt
    // damage, and one that felled a boss counted its win on a raid already lost.
    private void OnDied(Damageable victim, DamageInfo killingBlow)
    {
        EndSwing();
        EndFinish();
        pendingHits.Clear();
        queuedKind = SwingKind.None;
        pressTime = -1f;
        heldEarlyPress = false;
        attackHeld = false;
        forcedAttack = null;
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
        stateMachine.AttackKeyTaken -= OnAttackKeyTaken;
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
            health.Died -= OnDied;
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

    // A weapon put away or swapped mid-swing: the press held for the old one is let go (it would grow into the new weapon's heavy, or
    // read a weapon no longer there), nothing buffered carries over, and a wind-up not yet landed is lost. A blow already landed
    // recovers as it was.
    private void DropSwingForSwap()
    {
        pressTime = -1f;
        heldEarlyPress = false;
        queuedKind = SwingKind.None;
        forcedAttack = null;
        if (activeKind != SwingKind.None && hitPending)
        {
            EndSwing();
        }
    }

    private void AnnounceWeapon()
    {
        announcedWeapon = weapon;
        WeaponChanged?.Invoke(weapon);
    }
}
