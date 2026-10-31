using UnityEngine;

/// <summary>
/// The rules that make combat slow, deliberate and dangerous, about timing rather than clicking (Resources/CombatTechniqueSettings, Systems Editor ›
/// Player › Movement &amp; Combat): combo chains and their rhythm, the heavy attack, guard and parry, counters after a
/// parry or perfect dodge, and poise and stagger. How hits look and feel is in <see cref="CombatFeelSettings"/>.
/// </summary>
[CreateAssetMenu(fileName = "CombatTechniqueSettings", menuName = "Safe Haven/Combat/Combat Technique")]
public sealed class CombatTechniqueSettings : GameSettings<CombatTechniqueSettings>
{
    [Header("Combo chain")]
    [Tooltip("Seconds after a swing's recovery the next press still continues the chain. Later starts it over.")]
    [SerializeField, Min(0f)] private float chainGrace = 0.45f;
    [Tooltip("A press this many seconds before the swing recovers, or up to the late margin after, is on the rhythm.")]
    [SerializeField, Min(0f)] private float rhythmEarly = 0.16f;
    [SerializeField, Min(0f)] private float rhythmLate = 0.1f;
    [Tooltip("Rhythm stacks: each swing on the rhythm adds one, up to this many. A swing off it clears them.")]
    [SerializeField, Range(0, 5)] private int maxRhythm = 2;
    [Tooltip("Extra damage per rhythm stack (0.05 = +5%). Small: timing is about reaching the finisher, not a power-up.")]
    [SerializeField, Range(0f, 0.5f)] private float rhythmDamagePerStack = 0.05f;
    [Tooltip("Faster swings per rhythm stack. 0 keeps every swing deliberate, however well timed.")]
    [SerializeField, Range(0f, 0.3f)] private float rhythmSpeedPerStack;
    [Tooltip("Mashing: a press before a swing has even landed fumbles the chain, so the next swing starts it over. Off, " +
             "an early press is buffered and the chain goes on; it only loses the rhythm bonus.")]
    [SerializeField] private bool earlyPressFumbles;

    [Header("Commitment and danger")]
    [Tooltip("On: a dodge can cut a swing short while it winds up (it then deals no damage; its stamina is spent). Off: " +
             "a swing can only be dodged out of once it has landed, so every swing is a commitment.")]
    [SerializeField] private bool dodgeCancelsWindup = true;
    [Tooltip("Movement speed from the start of a swing until its blow lands, as a share of normal. 0 plants the feet, so the whole " +
             "body swings and a chain looks the same every time; only the attack's own lunge moves the player. Above 0 a swing started " +
             "on the run plays on the arms over the running legs.")]
    [SerializeField, Range(0f, 1f)] private float windupMoveSpeed;
    [Tooltip("Movement speed once the blow has landed, for the rest of the swing, and the same after a heavy swing, as a share of normal. " +
             "Walking off lets the arms finish the swing over the legs; the next attack still waits for the swing's recovery.")]
    [SerializeField, Range(0f, 1f)] private float swingMoveSpeed = 0.4f;
    [SerializeField, Range(0f, 1f)] private float heavyMoveSpeed = 0.15f;
    [Tooltip("A hit that gets through (not blocked) worth at least this share of the player's max health makes them flinch: a swing " +
             "still winding up is lost and they can't act for a moment. 0 = every hit.")]
    [SerializeField, Range(0f, 1f)] private float flinchShare = 0.05f;
    [SerializeField, Min(0f)] private float flinchSeconds = 0.45f;
    [Tooltip("The chain pips and rhythm readout under the player. Off keeps the screen clean; the feel is in the sound and motion.")]
    [SerializeField] private bool showComboHud;
    [Header("Heavy attack (hold the attack button)")]
    [Tooltip("Holding attack this long turns the light swing the press started into the heavy attack. Keep it shorter " +
             "than the quickest light swing's Hit Delay (about 0.28 s), or holding gives a light swing and then a heavy one.")]
    [SerializeField, Range(0.1f, 1f)] private float heavyHoldSeconds = 0.22f;

    [Header("Guard and parry (hold the guard button)")]
    [Tooltip("Width in degrees in front of the player that the guard covers.")]
    [SerializeField, Range(30f, 360f)] private float blockArc = 140f;
    [Tooltip("A hit within this many seconds of raising the guard is parried.")]
    [SerializeField, Range(0f, 0.5f)] private float parryWindow = 0.15f;
    [Tooltip("After lowering the guard it can't go up again for this long, so tapping it isn't a free parry.")]
    [SerializeField, Range(0f, 1f)] private float guardRelock = 0.3f;
    [Tooltip("Stamina a blocked hit costs per point of damage it would have done.")]
    [SerializeField, Min(0f)] private float blockStaminaPerDamage = 0.8f;
    [Tooltip("Movement speed while guarding, as a share of normal.")]
    [SerializeField, Range(0.1f, 1f)] private float guardMoveSpeed = 0.5f;
    [Tooltip("Share of a hit that gets through a guard with no shield and no weapon guard of its own.")]
    [SerializeField, Range(0f, 1f)] private float unarmedLetThrough = 0.7f;
    [Tooltip("Seconds the player reels after the guard breaks.")]
    [SerializeField, Min(0f)] private float guardBreakStun = 1.1f;
    [Tooltip("Seconds an attacker staggers when its hit is parried.")]
    [SerializeField, Min(0f)] private float parryStagger = 1.0f;

    [Header("Counters (after a parry or a perfect dodge)")]
    [Tooltip("Seconds the counter stays ready: the next swing that lands in time is a critical counter.")]
    [SerializeField, Min(0f)] private float counterSeconds = 1f;
    [SerializeField, Min(1f)] private float counterDamageMultiplier = 1.3f;
    [Tooltip("The hitch after a perfect dodge or a parry, as a time scale, and how long it lasts in real seconds. Keep it " +
             "short: a beat of weight, not slow motion.")]
    [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.4f;
    [SerializeField, Min(0f)] private float slowMotionSeconds = 0.09f;

    [Header("Focus (earned by fighting well, spent on skills)")]
    [SerializeField, Min(1f)] private float focusMax = 100f;
    [Tooltip("Focus per target a swing hits (at most two count). Finishers count double, heavy swings 1.5 times, counters 1.5 times more.")]
    [SerializeField, Min(0f)] private float focusPerHit = 6f;
    [SerializeField, Min(0f)] private float focusOnParry = 35f;
    [SerializeField, Min(0f)] private float focusOnPerfectDodge = 25f;
    [SerializeField, Min(0f)] private float focusOnBlock = 2f;
    [Tooltip("Seconds Focus holds after the last gain, then how fast it drains away (per second).")]
    [SerializeField, Min(0f)] private float focusHoldSeconds = 10f;
    [SerializeField, Min(0f)] private float focusDrainPerSecond = 4f;

    [Header("Eating and drinking (committed healing)")]
    [Tooltip("Seconds eating or drinking takes. Getting hit or dodging stops it, and the food is kept.")]
    [SerializeField, Min(0.1f)] private float eatSeconds = 1.3f;
    [Tooltip("Movement speed while eating, as a share of normal.")]
    [SerializeField, Range(0f, 1f)] private float eatMoveSpeed = 0.35f;
    [Tooltip("Seconds over which a meal's healing arrives, once eaten.")]
    [SerializeField, Min(0f)] private float healOverSeconds = 6f;

    [Header("Poise and stagger (monsters)")]
    [Tooltip("Poise of monsters whose definition sets none. Hits wear it down; at zero the monster staggers.")]
    [SerializeField, Min(1f)] private float defaultPoise = 30f;
    [Tooltip("Seconds without being hit before poise starts coming back, and how much per second.")]
    [SerializeField, Min(0f)] private float poiseRecoveryDelay = 2f;
    [SerializeField, Min(0f)] private float poiseRecoveryPerSecond = 15f;
    [Tooltip("Seconds a monster staggers when its poise breaks.")]
    [SerializeField, Min(0f)] private float staggerSeconds = 1f;
    [Tooltip("Damage a staggered monster takes, as a multiplier.")]
    [SerializeField, Min(1f)] private float staggeredDamageTaken = 1.2f;

    public float ChainGrace => chainGrace;
    public float RhythmEarly => rhythmEarly;
    public float RhythmLate => rhythmLate;
    public int MaxRhythm => maxRhythm;
    public float RhythmDamagePerStack => rhythmDamagePerStack;
    public float RhythmSpeedPerStack => rhythmSpeedPerStack;
    public bool EarlyPressFumbles => earlyPressFumbles;
    public bool DodgeCancelsWindup => dodgeCancelsWindup;
    public float WindupMoveSpeed => windupMoveSpeed;
    public float SwingMoveSpeed => swingMoveSpeed;
    public float HeavyMoveSpeed => heavyMoveSpeed;
    public float FlinchShare => flinchShare;
    public float FlinchSeconds => flinchSeconds;
    public bool ShowComboHud => showComboHud;
    public float HeavyHoldSeconds => heavyHoldSeconds;
    public float BlockArc => blockArc;
    public float ParryWindow => parryWindow;
    public float GuardRelock => guardRelock;
    public float BlockStaminaPerDamage => blockStaminaPerDamage;
    public float GuardMoveSpeed => guardMoveSpeed;
    public float UnarmedLetThrough => unarmedLetThrough;
    public float GuardBreakStun => guardBreakStun;
    public float ParryStagger => parryStagger;
    public float CounterSeconds => counterSeconds;
    public float CounterDamageMultiplier => counterDamageMultiplier;
    public float SlowMotionScale => slowMotionScale;
    public float SlowMotionSeconds => slowMotionSeconds;
    public float FocusMax => focusMax;
    public float FocusPerHit => focusPerHit;
    public float FocusOnParry => focusOnParry;
    public float FocusOnPerfectDodge => focusOnPerfectDodge;
    public float FocusOnBlock => focusOnBlock;
    public float FocusHoldSeconds => focusHoldSeconds;
    public float FocusDrainPerSecond => focusDrainPerSecond;
    public float EatSeconds => eatSeconds;
    public float EatMoveSpeed => eatMoveSpeed;
    public float HealOverSeconds => healOverSeconds;
    public float DefaultPoise => defaultPoise;
    public float PoiseRecoveryDelay => poiseRecoveryDelay;
    public float PoiseRecoveryPerSecond => poiseRecoveryPerSecond;
    public float StaggerSeconds => staggerSeconds;
    public float StaggeredDamageTaken => staggeredDamageTaken;
}
