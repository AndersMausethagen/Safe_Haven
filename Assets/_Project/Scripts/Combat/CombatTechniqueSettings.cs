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
             "0 keeps the player planted and facing the blow until the swing ends. Above 0, walking off lets the arms finish the swing " +
             "over the legs; the next attack still waits for the swing's recovery.")]
    [SerializeField, Range(0f, 1f)] private float swingMoveSpeed;
    [SerializeField, Range(0f, 1f)] private float heavyMoveSpeed;
    [Tooltip("A hit that gets through (not blocked) worth at least this share of the player's max health makes them flinch: a swing " +
             "still winding up is lost and they can't act for a moment. 0 = every hit.")]
    [SerializeField, Range(0f, 1f)] private float flinchShare = 0.05f;
    [SerializeField, Min(0f)] private float flinchSeconds = 0.45f;
    [Tooltip("Seconds after a flinch or a stumble ends before another can start (the damage still lands), so two attackers " +
             "can't keep the player flinching (the user, 2026-11-14: anti-overwhelm 1).")]
    [SerializeField, Min(0f)] private float flinchGrace = 1f;
    [Tooltip("A hit worth at least this share of the player's max health also gives a moment of invulnerability...")]
    [SerializeField, Range(0.05f, 1f)] private float heavyHitShare = 0.2f;
    [Tooltip("...this many seconds: one chance to answer before the next blow lands.")]
    [SerializeField, Min(0f)] private float heavyHitInvulnerable = 0.3f;
    [Tooltip("The chain pips and rhythm readout under the player. Off keeps the screen clean; the feel is in the sound and motion.")]
    [SerializeField] private bool showComboHud;
    [Header("Heavy attack (hold the attack button)")]
    [Tooltip("Holding attack this long turns the light swing the press started into the heavy attack. Keep it shorter " +
             "than the quickest light swing's Hit Delay (about 0.28 s), or holding gives a light swing and then a heavy one.")]
    [SerializeField, Range(0.1f, 1f)] private float heavyHoldSeconds = 0.22f;
    [Tooltip("Held on past that, the heavy swing waits at the top of its wind-up and charges, for at most this long; letting go " +
             "strikes. 0 turns charging off.")]
    [SerializeField, Min(0f)] private float heavyChargeSeconds = 0.9f;
    [Tooltip("Damage of a fully charged heavy swing, as a multiplier.")]
    [SerializeField, Min(1f)] private float heavyChargeDamage = 1.4f;
    [Tooltip("Poise damage of a fully charged heavy swing, as a multiplier.")]
    [SerializeField, Min(1f)] private float heavyChargePoise = 1.5f;

    [Header("Rush attack (out of a dodge, or on the run)")]
    [Tooltip("An attack pressed during a dodge, or this many seconds after it, is the weapon's quick rush attack: it cuts the end of " +
             "the dodge short once its invulnerability is over.")]
    [SerializeField, Min(0f)] private float rushAfterDodgeSeconds = 0.3f;
    [Tooltip("Share of full running speed that counts as running, for how long, and how far the aim may point from the way you run.")]
    [SerializeField, Range(0.1f, 1f)] private float rushRunShare = 0.7f;
    [SerializeField, Min(0f)] private float rushRunSeconds = 0.2f;
    [SerializeField, Range(10f, 180f)] private float rushRunAngle = 70f;

    [Header("Dodging around an enemy (while it is in focus)")]
    [Tooltip("Steering a dodge toward or past the enemy in focus, within this many metres, curves it around to the spot behind.")]
    [SerializeField, Min(0f)] private float dodgeAroundRange = 6f;
    [Tooltip("How much faster a dodge going around is: the way round is longer than the way straight.")]
    [SerializeField, Min(1f)] private float dodgeAroundSpeed = 1.3f;
    [Tooltip("At the longest, seconds a dodge going around lasts: a light quickstep needs longer than usual to get all the way round. It " +
             "ends as soon as it gets there.")]
    [SerializeField, Min(0.1f)] private float dodgeAroundSeconds = 0.62f;
    [Tooltip("How fast it can turn, in degrees per second.")]
    [SerializeField, Min(0f)] private float dodgeAroundTurn = 720f;
    [Tooltip("How far beyond the enemy's body the spot behind it is, and how wide of its body the curve keeps, in metres.")]
    [SerializeField, Min(0f)] private float dodgeBehindDistance = 1f;
    [SerializeField, Min(0f)] private float dodgeClearance = 0.55f;
    [Tooltip("A bow drawn out of a dodge starts this share of the way to a full draw: coming out of a roll with the arrow already half up.")]
    [SerializeField, Range(0f, 0.9f)] private float drawAfterDodge = 0.35f;

    [Header("Staying in the fight")]
    [Tooltip("While an enemy within this many metres is after the player, the game stays in Combat however long since a blow was traded.")]
    [SerializeField, Min(0f)] private float fightHoldRange = 14f;

    [Header("Landing a swing")]
    [Tooltip("Seconds a swing takes to sweep a half circle: targets are struck in the order the blade reaches them, not all on one " +
             "frame. Narrow swings take less. 0 strikes them together.")]
    [SerializeField, Min(0f)] private float sweepSeconds = 0.09f;
    [Tooltip("Degrees per second a swing still winding up turns to follow the focused enemy, until it steps into the blow. 0 aims " +
             "only as it starts.")]
    [SerializeField, Min(0f)] private float swingTracking = 240f;

    [Header("Focus on an enemy")]
    [Tooltip("An enemy can be taken up as the focus within this many metres.")]
    [SerializeField, Min(1f)] private float focusAcquireRange = 6f;
    [Tooltip("Width of the cone around the aim in which an enemy can be taken up, in degrees either side.")]
    [SerializeField, Range(10f, 180f)] private float focusAcquireCone = 70f;
    [Tooltip("The focus lets go of an enemy further away than this.")]
    [SerializeField, Min(1f)] private float focusKeepRange = 9f;
    [Tooltip("How much better another enemy's score must be before the focus moves to it (see TargetRules).")]
    [SerializeField, Min(0f)] private float focusSwitchMargin = 0.35f;
    [Tooltip("While the aim points further than this from the focused enemy, in degrees, the player looks and swings where the aim " +
             "points instead.")]
    [SerializeField, Range(30f, 180f)] private float focusFreeAimAngle = 100f;

    [Header("Backstab (struck from behind while staggering or striking)")]
    [Tooltip("Width of the area behind an enemy that counts, in degrees.")]
    [SerializeField, Range(30f, 240f)] private float backstabArc = 130f;
    [Tooltip("Damage of a backstab on a staggering enemy, as a multiplier on top of the staggered bonus. Always a critical hit.")]
    [SerializeField, Min(1f)] private float backstabDamage = 1.75f;
    [Tooltip("Damage of a backstab on an enemy in the middle of a strike. Less than the above, and not always critical: a parry and then a backstab should pay best.")]
    [SerializeField, Min(1f)] private float backstabOnStrikeDamage = 1.35f;
    [Tooltip("Share of a dodge's stamina a backstab straight out of it gives back.")]
    [SerializeField, Range(0f, 1f)] private float backstabRefund = 0.5f;
    [Tooltip("Seconds after a dodge ends that a backstab still gets the refund.")]
    [SerializeField, Min(0f)] private float backstabRefundWithin = 1.5f;

    [Header("Taking turns at the player")]
    [Tooltip("How many can be closing in on the player with a strike at once. Others circle. Great beasts and elites don't wait; ranged attacks have their own count.")]
    [SerializeField, Min(1)] private int attackersAtOnce = 2;
    [Tooltip("How many may shoot or throw at the player at once, besides those closing in (the user, 2026-11-14: \"at most 2 ranged\"). The rest hold their aim.")]
    [SerializeField, Min(1)] private int rangedAtOnce = 2;
    [Tooltip("Seconds a ranged attacker keeps its turn after it starts a shot or a throw, so a third waits for a quiet moment.")]
    [SerializeField, Min(0.1f)] private float rangedTurnSeconds = 2.5f;
    [Tooltip("A melee attacker further than this many degrees off the camera's view is off screen: it strikes only when no " +
             "other is closing in, and after Off Screen Wait; otherwise it circles back into view (anti-overwhelm 3). Noticing " +
             "and coming is unchanged: noise still draws everything in.")]
    [SerializeField, Range(30f, 180f)] private float offScreenAngle = 70f;
    [SerializeField, Min(0f)] private float offScreenWait = 0.6f;
    [Tooltip("Seconds between one close strike at the player starting and the next.")]
    [SerializeField, Min(0f)] private float attackTurnSpacing = 0.35f;
    [Tooltip("Seconds one circles before it tries again, least and most.")]
    [SerializeField] private Vector2 circleSeconds = new Vector2(0.8f, 1.6f);
    [Tooltip("Metres from the player it circles at.")]
    [SerializeField, Min(0f)] private float circleDistance = 3.5f;
    [Tooltip("Share of its speed it circles at.")]
    [SerializeField, Range(0.1f, 1f)] private float circleSpeed = 0.6f;

    [Header("How enemies answer a backstab, and learn from it")]
    [Tooltip("Seconds after a backstab before the enemy answers it, or after its stagger if that is longer.")]
    [SerializeField, Min(0f)] private float answerDelay = 0.3f;
    [Tooltip("Chance it answers by turning into a strike at once, wind-up as usual. Otherwise, or with no strike to use, it hops back.")]
    [SerializeField, Range(0f, 1f)] private float answerStrikeChance = 0.65f;
    [Tooltip("How far its hop goes, in metres.")]
    [SerializeField, Min(0f)] private float hopDistance = 3f;
    [Tooltip("Seconds its hop takes. Blows don't touch it meanwhile.")]
    [SerializeField, Min(0.05f)] private float hopSeconds = 0.3f;
    [Tooltip("Chance per backstab it remembers that it sees the next one coming, and breaks off its strike to meet it.")]
    [SerializeField, Range(0f, 1f)] private float readChancePerBackstab = 0.35f;
    [Tooltip("The chance never goes above this. A staggering enemy never sees it coming, so a parry stays the sure way in.")]
    [SerializeField, Range(0f, 1f)] private float readChanceMost = 0.9f;
    [Tooltip("Seconds until it forgets one backstab.")]
    [SerializeField, Min(1f)] private float backstabForgotAfter = 20f;
    [Tooltip("Others of its side this close saw it and learn as well, in metres. 0: only the one struck.")]
    [SerializeField, Min(0f)] private float backstabSeenWithin = 8f;
    [Tooltip("It only sees a swing coming from this close, in metres.")]
    [SerializeField, Min(0f)] private float readRange = 4f;
    [Tooltip("Chance that one which can parry (its definition) parries what it sees coming, rather than hopping aside.")]
    [SerializeField, Range(0f, 1f)] private float readParryChance = 0.5f;
    [Tooltip("Seconds its parry stays up.")]
    [SerializeField, Min(0f)] private float readParrySeconds = 0.6f;
    [Tooltip("Degrees per second it whips round to parry.")]
    [SerializeField, Min(0f)] private float readTurnSpeed = 1440f;
    [Tooltip("Seconds the player reels from a parried backstab.")]
    [SerializeField, Min(0f)] private float parriedFlinchSeconds = 0.55f;

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
    [Tooltip("Stamina a weapon art (guard then attack, or the middle button) costs instead when Focus is short of its price, " +
             "so it always comes out. Paid in Focus it costs no stamina: fighting well makes it free.")]
    [SerializeField, Min(0f)] private float artStaminaWithoutFocus = 30f;

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
    public float FlinchGrace => flinchGrace;
    public float HeavyHitShare => heavyHitShare;
    public float HeavyHitInvulnerable => heavyHitInvulnerable;
    public bool ShowComboHud => showComboHud;
    public float HeavyHoldSeconds => heavyHoldSeconds;
    public float HeavyChargeSeconds => heavyChargeSeconds;
    public float HeavyChargeDamage => heavyChargeDamage;
    public float HeavyChargePoise => heavyChargePoise;
    public float RushAfterDodgeSeconds => rushAfterDodgeSeconds;
    public float RushRunShare => rushRunShare;
    public float RushRunSeconds => rushRunSeconds;
    public float RushRunAngle => rushRunAngle;
    public float DodgeAroundRange => dodgeAroundRange;
    public float DodgeAroundSpeed => dodgeAroundSpeed;
    public float DodgeAroundSeconds => dodgeAroundSeconds;
    public float DodgeAroundTurn => dodgeAroundTurn;
    public float DodgeBehindDistance => dodgeBehindDistance;
    public float DodgeClearance => dodgeClearance;
    public float DrawAfterDodge => drawAfterDodge;
    public float FightHoldRange => fightHoldRange;
    public float SweepSeconds => sweepSeconds;
    public float SwingTracking => swingTracking;
    public float FocusAcquireRange => focusAcquireRange;
    public float FocusAcquireCone => focusAcquireCone;
    public float FocusKeepRange => focusKeepRange;
    public float FocusSwitchMargin => focusSwitchMargin;
    public float FocusFreeAimAngle => focusFreeAimAngle;
    public float BackstabArc => backstabArc;
    public float BackstabDamage => backstabDamage;
    public float BackstabOnStrikeDamage => backstabOnStrikeDamage;
    public float BackstabRefund => backstabRefund;
    public float BackstabRefundWithin => backstabRefundWithin;
    public int AttackersAtOnce => attackersAtOnce;
    public int RangedAtOnce => rangedAtOnce;
    public float RangedTurnSeconds => rangedTurnSeconds;
    public float OffScreenAngle => offScreenAngle;
    public float OffScreenWait => offScreenWait;
    public float AttackTurnSpacing => attackTurnSpacing;
    public Vector2 CircleSeconds => circleSeconds;
    public float CircleDistance => circleDistance;
    public float CircleSpeed => circleSpeed;
    public float AnswerDelay => answerDelay;
    public float AnswerStrikeChance => answerStrikeChance;
    public float HopDistance => hopDistance;
    public float HopSeconds => hopSeconds;
    public float ReadChancePerBackstab => readChancePerBackstab;
    public float ReadChanceMost => readChanceMost;
    public float BackstabForgotAfter => backstabForgotAfter;
    public float BackstabSeenWithin => backstabSeenWithin;
    public float ReadRange => readRange;
    public float ReadParryChance => readParryChance;
    public float ReadParrySeconds => readParrySeconds;
    public float ReadTurnSpeed => readTurnSpeed;
    public float ParriedFlinchSeconds => parriedFlinchSeconds;
    public float BlockArc => blockArc;
    [Header("Finishing blow")]
    [Tooltip("How the stab looks. Only its animation is used: the numbers and timing are the weapon's own rush, so a finishing blow doesn't change the balance.")]
    [SerializeField] private AttackDefinition finishStab;
    [Tooltip("A press this close behind a staggered creature that can take one is a finishing blow, in metres.")]
    [SerializeField, Min(0.5f)] private float finishReach = 2.6f;
    [Tooltip("How far behind the creature the two bodies lock together, in metres.")]
    [SerializeField, Min(0.2f)] private float finishGap = 0.95f;
    [Tooltip("Seconds to close that distance: the snap into the hold.")]
    [SerializeField, Min(0.02f)] private float finishCloseSeconds = 0.14f;
    [Tooltip("Seconds the two stay locked after the blade lands.")]
    [SerializeField, Min(0f)] private float finishHoldSeconds = 0.3f;

    public AttackDefinition FinishStab => finishStab;
    public float FinishReach => finishReach;
    public float FinishGap => finishGap;
    public float FinishCloseSeconds => finishCloseSeconds;
    public float FinishHoldSeconds => finishHoldSeconds;
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
    public float ArtStaminaWithoutFocus => artStaminaWithoutFocus;
    public float EatSeconds => eatSeconds;
    public float EatMoveSpeed => eatMoveSpeed;
    public float HealOverSeconds => healOverSeconds;
    public float DefaultPoise => defaultPoise;
    public float PoiseRecoveryDelay => poiseRecoveryDelay;
    public float PoiseRecoveryPerSecond => poiseRecoveryPerSecond;
    public float StaggerSeconds => staggerSeconds;
    public float StaggeredDamageTaken => staggeredDamageTaken;
}
