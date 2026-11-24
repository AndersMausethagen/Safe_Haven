using UnityEngine;
using UnityEngine.Serialization;

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
    [Tooltip("Seconds a swing waits on its last pose once it has recovered, while nothing is pressed and the player stands still: the " +
             "next press still chains, and the chain's grace runs from the end of the wait (the user, 2026-11-23: \"hold the last part of " +
             "a swing for a bit ... to easier follow up with a combo\"). Moving, running, a roll or the guard ends it at once. 0: no wait.")]
    [SerializeField, Min(0f)] private float swingHoldSeconds = 0.4f;
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
    [Tooltip("What breaks a light swing off before its blow, for attacks that don't set their own (AttackDefinition › Cancels). A guard " +
             "pressed in its wind-up raises at once (God of War: block and roll cancel a quick blow before it lands).")]
    [SerializeField] private SwingCancels lightWindupCancels = SwingCancels.Dodge | SwingCancels.Guard;
    [Tooltip("The same for a heavy swing: a roll gets out, a block doesn't (a heavy still held breaks off into the guard either way).")]
    [SerializeField] private SwingCancels heavyWindupCancels = SwingCancels.Dodge;
    [Tooltip("The same for the rest: arts, rushes, shield bashes and counters out of the guard.")]
    [SerializeField] private SwingCancels otherWindupCancels = SwingCancels.Dodge;
    [Tooltip("What cuts a swing's follow-through short once its blow has landed, for attacks that don't set their own.")]
    [SerializeField] private SwingCancels recoveryCancels = SwingCancels.Dodge | SwingCancels.Guard;
    [Tooltip("Movement speed from the start of a light swing until its blow lands, as a share of normal, for attacks that don't set " +
             "their own (AttackDefinition › Movement). 0 plants the feet, so the whole body swings; above 0 a swing walked through plays " +
             "on the arms over the walking legs. Heavy swings, rushes and arts stay planted unless the attack says otherwise.")]
    [SerializeField, Range(0f, 1f)] private float windupMoveSpeed;
    [Tooltip("Share of walking speed kept while a heavy is held charging (the user, 2026-11-19: let me move slowly while charging); the " +
             "charge plays on the arms over walking legs. 0 plants the feet, as before.")]
    [SerializeField, Range(0f, 1f)] private float chargeMoveSpeed = 0.3f;
    [Tooltip("Movement speed from Move After Contact to the end of the swing, and the same after a heavy swing, as a share of normal, " +
             "for attacks that don't set their own. 0 keeps the player planted and facing the blow until the swing ends. Above 0, walking " +
             "off lets the arms finish the swing over the legs; the next attack still waits for the swing's recovery.")]
    [SerializeField, Range(0f, 1f)] private float swingMoveSpeed;
    [SerializeField, Range(0f, 1f)] private float heavyMoveSpeed;
    [Tooltip("Degrees per second a swing turns toward where the player aims early in its wind-up, when no enemy is in focus (No Rest " +
             "for the Wicked's \"change targets during attack windups\"). An attack can set its own (Movement › Windup Turn Rate). 0: aimed only as it starts.")]
    [SerializeField, Min(0f)] private float windupTurnRate = 300f;
    [Tooltip("Share of the wind-up, from its start, in which the blow may still turn toward the aim. It never turns once it steps in.")]
    [SerializeField, Range(0f, 1f)] private float windupTurnShare = 0.6f;
    [Tooltip("Seconds after a blow lands before the player walks and turns again (the arms finish the swing). An attack can set its own.")]
    [SerializeField, Min(0f)] private float moveAfterContact = 0.1f;
    [Tooltip("Share of a swing's wind-up, counted back from the blow, the step into it (the attack's Lunge) takes, at least 0.12 s. " +
             "An attack can set its own. Rushes step all through their wind-up.")]
    [SerializeField, Range(0f, 1f)] private float lungeShare = 0.33f;
    [Tooltip("The bounded buffer: a press during a swing is kept for the next one only this many seconds before the swing's chain point " +
             "(its Recovery Time), or in the last moments of a dodge, a flinch or a broken guard. Earlier presses are dropped, unless the " +
             "button is still held when the window opens. 0 keeps every press (mashing then commits to the whole chain).")]
    [SerializeField, Min(0f)] private float bufferWindow = 0.25f;
    [Tooltip("Seconds after a dodge that cut a swing's wind-up short in which no new attack starts, so cancelling into a dodge over and " +
             "over can't fish for its invulnerability (Path of Exile 2's lockout).")]
    [SerializeField, Min(0f)] private float cancelLockout = 0.35f;
    [Tooltip("A hit that gets through (not blocked) worth at least this share of the player's max health makes them flinch: a swing " +
             "still winding up is lost and they can't act for a moment. 0 = every hit.")]
    [SerializeField, Range(0f, 1f)] private float flinchShare = 0.05f;
    [SerializeField, Min(0f)] private float flinchSeconds = 0.45f;
    [Tooltip("Seconds after a flinch or a stumble ends before another can start (the damage still lands), so two attackers " +
             "can't keep the player flinching (the user, 2026-11-14: anti-overwhelm 1).")]
    [SerializeField, Min(0f)] private float flinchGrace = 1f;
    [Tooltip("A hit worth at least this share of the player's max health floors them for Knockdown Seconds (the whole body goes down), " +
             "untouchable while down so the blow isn't followed by a free combo. 1: never.")]
    [SerializeField, Range(0.05f, 1f)] private float knockdownShare = 0.35f;
    [SerializeField, Min(0f)] private float knockdownSeconds = 1f;
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
    [Tooltip("Held on past that, the heavy swing waits at the top of its wind-up and charges; after this long it is full (the blink) " +
             "and letting go strikes the weapon's charged attack. 0 turns charging off.")]
    [SerializeField, Min(0f)] private float heavyChargeSeconds = 0.9f;
    [Tooltip("Held on past full, a charge can be kept for as long as the player likes, at this much stamina a second (the user, " +
             "2026-11-20: \"hold a charging attack for as long as he wants, it could slowly start draining stamina\"). Run dry, it strikes.")]
    [SerializeField, Min(0f)] private float heldChargeDrain = 6f;
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
    [Tooltip("A dodge at least this many degrees away from where the player aims is a backstep: attacking out of it is the weapon's " +
             "backstep attack (when it has one) instead of the rush.")]
    [SerializeField, Range(60f, 180f)] private float backstepAngle = 120f;

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
    [Header("Fair turns (Docs/EnemyAIReview_2026-11-19.md §2)")]
    [Tooltip("Of those ready to strike the player, the one ranked highest goes: seconds waited, plus Front Bonus inside the player's " +
             "guard arc (half of Front Arc either side of where they face), plus On Screen Bonus in view, less Last Striker Penalty for " +
             "the one that struck last.")]
    [SerializeField, Min(0f)] private float frontBonus = 0.6f;
    [SerializeField, Range(10f, 180f)] private float frontHalfArc = 70f;
    [SerializeField, Min(0f)] private float onScreenBonus = 0.3f;
    [SerializeField, Min(0f)] private float lastStrikerPenalty = 1f;
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
    [Tooltip("Guard points (Monster Hunter's lance and charge blade): with a shield on the arm, a blow from the front that lands while " +
             "a swing winds up or a heavy charges is blocked by the shield, and the swing goes on (the user, 2026-11-19). Off: only " +
             "the raised guard blocks.")]
    [SerializeField] private bool shieldGuardPoints = true;
    [Tooltip("Times the shield's share let through a guard point lets through (a guard point is a glancing block, not a braced one).")]
    [SerializeField, Range(1f, 4f)] private float guardPointLetThrough = 1.6f;
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

    [Header("Parry economy (the user, 2026-11-15: parrying was the whole loop)")]
    [Tooltip("A great beast, boss or elite (Monster Definition › Parry Response: Auto or Poise Damage) isn't floored by a parry: it loses " +
             "this share of its poise instead, and a parry that breaks what is left staggers it. Two parries, or one and some blows, open it " +
             "(No Rest for the Wicked: \"Bosses are no longer fully staggered from parry\").")]
    [SerializeField, Range(0f, 1f)] private float parryPoiseShare = 0.4f;
    [Tooltip("Seconds a parried attacker that doesn't stagger reels: its strike and what would follow are broken off, but it isn't open " +
             "to a backstab and takes no extra damage.")]
    [SerializeField, Min(0f)] private float parryRecoilSeconds = 0.45f;
    [Tooltip("A guard raised and lowered again within this many seconds with no blow caught on it is a missed parry: it can't go up " +
             "again for Whiff Relock instead of Guard Relock. Tapping the guard to fish for parries costs, holding it doesn't.")]
    [SerializeField, Min(0f)] private float whiffTapSeconds = 0.5f;
    [SerializeField, Range(0f, 2f)] private float whiffRelock = 0.6f;
    [Tooltip("Seconds after a blow is blocked in which attacking out of the guard is the weapon's guard counter (Elden Ring): a hard, " +
             "poise-heavy answer, no critical. After a parry the press is the counter itself.")]
    [SerializeField, Min(0f)] private float guardCounterSeconds = 0.6f;
    [Tooltip("Poise damage of a guard counter, as a multiplier on its attack's.")]
    [SerializeField, Min(1f)] private float guardCounterPoise = 1.75f;

    [Header("Interrupts (hitting a wind-up)")]
    [Tooltip("A blow in a monster's wind-up breaks it off even when the strike isn't Interruptible, if it is a heavy swing, a counter, a " +
             "finisher or a weapon art, or its poise damage is at least this share of the monster's poise. Great beasts, bosses and elites " +
             "are never broken off this way. 0 turns it off.")]
    [SerializeField, Range(0f, 1f)] private float breakThroughPoiseShare = 0.25f;
    [Tooltip("Seconds an interrupted monster waits before it strikes again.")]
    [SerializeField, Min(0f)] private float interruptDelay = 0.5f;

    [Header("Counters (after a parry or a perfect dodge)")]
    [Tooltip("Seconds the counter stays ready: the next swing that lands in time is a critical counter.")]
    [SerializeField, Min(0f)] private float counterSeconds = 1f;
    [SerializeField, Min(1f)] private float counterDamageMultiplier = 1.3f;
    [Tooltip("The hitch after a perfect dodge or a parry, as a time scale, and how long it lasts in real seconds. Keep it " +
             "short: a beat of weight, not slow motion.")]
    [SerializeField, Range(0.05f, 1f)] private float slowMotionScale = 0.4f;
    [SerializeField, Min(0f)] private float slowMotionSeconds = 0.09f;
    [Tooltip("The player's parry holds this long, at the hit stop's speed: the hardest thing in the game landed lighter than a jab " +
             "(Docs/CombatFeelReview_2026-11-19.md §7). A monster's parry of the player keeps the hitch above.")]
    [SerializeField, Min(0f)] private float parryStop = 0.12f;
    [Tooltip("A perfect dodge's stop, on top of the hitch.")]
    [SerializeField, Min(0f)] private float perfectDodgeStop = 0.08f;

    [Header("Flinch fairness (Docs/CombatFeelReview_2026-11-19.md §5)")]
    [Tooltip("When the player flinches, no strike at them starts until the flinch is over and this long after: a second bite landed " +
             "inside it, when nothing could answer it.")]
    [SerializeField, Min(0f)] private float flinchTurnGrace = 0.2f;
    [Tooltip("A dodge cuts a flinch short from this far into it (not a knockdown, nor a parry thrown back).")]
    [SerializeField, Min(0f)] private float flinchDodgeAfter = 0.25f;

    [Header("Focus (earned by fighting well, spent on skills)")]
    [Tooltip("Focus is kept in circles (the user, 2026-11-24): a full one lights above the skill bar and stays lit until spent; every " +
             "ability, spell and weapon art takes one. This is the Focus one circle takes to fill.")]
    [FormerlySerializedAs("focusMax"), SerializeField, Min(1f)] private float focusPerCircle = 100f;
    [Tooltip("How many circles can be kept.")]
    [SerializeField, Range(1, 5)] private int focusCircles = 3;
    [Tooltip("Focus per target a swing hits (at most two count). Finishers count double, heavy swings 1.5 times, counters 1.5 times more.")]
    [SerializeField, Min(0f)] private float focusPerHit = 6f;
    [SerializeField, Min(0f)] private float focusOnParry = 35f;
    [SerializeField, Min(0f)] private float focusOnPerfectDodge = 25f;
    [SerializeField, Min(0f)] private float focusOnBlock = 2f;
    [Tooltip("Seconds the circle still filling holds after the last gain, then how fast it drains away (per second). Full circles never drain.")]
    [SerializeField, Min(0f)] private float focusHoldSeconds = 10f;
    [SerializeField, Min(0f)] private float focusDrainPerSecond = 4f;

    [Header("The player's damage")]
    [Tooltip("Everything the player deals is multiplied by this: every blow however it is delivered (a blade, an arrow, a staff's " +
             "cast, a thrown weapon) and the burning, poison and hazards it leaves; not what is dealt to structures. 0.7 since " +
             "2026-11-16: the combat rework had made the player far too strong, and this took the place of twice the health on " +
             "every enemy (the user: the change belongs on the player).")]
    [SerializeField, Range(0.1f, 2f)] private float playerDamage = 0.7f;
    [Tooltip("Weapon arts, on top of Player Damage: 0.9 is another tenth less. Arts are paid in Focus alone.")]
    [SerializeField, Range(0.1f, 2f)] private float artDamage = 0.9f;

    [Header("Regain (strike back to win back health)")]
    [Tooltip("Share of a blow that got through which striking back can win back (PlayerRegain). 0 turns Regain off.")]
    [SerializeField, Range(0f, 1f)] private float regainShare = 0.4f;
    [Tooltip("Seconds it can be won back; it drains evenly meanwhile.")]
    [SerializeField, Min(0.1f)] private float regainSeconds = 3f;
    [Tooltip("Health won back per point of melee damage dealt, up to what is left.")]
    [SerializeField, Min(0f)] private float regainPerDamage = 0.5f;

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
    [Tooltip("Below this share of its poise a creature reels: it sways, embers show at its chest and its poise bar pulses, so the player " +
             "sees a stagger is near (the hammer's Crush line). Only a look: it fights on as before.")]
    [SerializeField, Range(0f, 1f)] private float reelingShare = 0.3f;

    [Header("Creatures answering back (Docs/EnemyCombatResearch.md, phase C)")]
    [Tooltip("Seconds an ordinary creature is down when felled: a heavy, charged, art or guard-counter blow while it reels, a charged blow " +
             "that breaks its poise, or a blow worth three quarters of its poise. It falls, lies, and gets up as it ends.")]
    [SerializeField, Min(0f)] private float felledSeconds = 1.8f;
    [Tooltip("A stagger that breaks through a raised shield lasts this many times as long (Guard Broken); two of them splinter the shield.")]
    [SerializeField, Min(1f)] private float guardBrokenScale = 1.5f;
    [SerializeField, Min(1)] private int guardBreaksToSplinter = 2;
    [Tooltip("Seconds between a nimble creature's side steps from a bow held past its perfect release (Monster Definition › Arrow Dodge).")]
    [SerializeField, Min(0f)] private float arrowDodgeCooldown = 5f;
    [Tooltip("Seconds after a weapon art before another can start (the user, 2026-11-15: a spear toss pressed again mid-flight " +
             "struck again).")]
    [SerializeField, Min(0f)] private float artCooldown = 2f;
    [Tooltip("Seconds a creature stays marked by a perfect release from a bow with Hunter's Mark, and how many times the poise it takes from every blow meanwhile.")]
    [SerializeField, Min(0f)] private float huntersMarkSeconds = 6f;
    [SerializeField, Min(1f)] private float huntersMarkPoise = 1.5f;

    [Header("Fire on swings (a weapon coating: Ember Pitch)")]
    [Tooltip("A creature a coated swing sets alight (Stat › Melee Ignite Chance) burns for this share of the weapon's damage each " +
             "second, as a fire arrow's burn does: before the player's damage scale and bonus, which Burning adds.")]
    [SerializeField, Min(0f)] private float meleeBurnShare = 0.35f;
    [Tooltip("Seconds a creature set alight by a coated swing burns.")]
    [SerializeField, Min(0f)] private float meleeBurnSeconds = 4f;

    public float ChainGrace => chainGrace;
    public float SwingHoldSeconds => swingHoldSeconds;
    public float RhythmEarly => rhythmEarly;
    public float RhythmLate => rhythmLate;
    public int MaxRhythm => maxRhythm;
    public float RhythmDamagePerStack => rhythmDamagePerStack;
    public float RhythmSpeedPerStack => rhythmSpeedPerStack;
    public bool EarlyPressFumbles => earlyPressFumbles;
    public bool DodgeCancelsWindup => dodgeCancelsWindup;
    public SwingCancels LightWindupCancels => lightWindupCancels;
    public SwingCancels HeavyWindupCancels => heavyWindupCancels;
    public SwingCancels OtherWindupCancels => otherWindupCancels;
    public SwingCancels RecoveryCancels => recoveryCancels;
    public float WindupMoveSpeed => windupMoveSpeed;
    public float ChargeMoveSpeed => chargeMoveSpeed;
    public float SwingMoveSpeed => swingMoveSpeed;
    public float HeavyMoveSpeed => heavyMoveSpeed;
    public float WindupTurnRate => windupTurnRate;
    public float WindupTurnShare => windupTurnShare;
    public float MoveAfterContact => moveAfterContact;
    public float LungeShare => lungeShare;
    public float BufferWindow => bufferWindow;
    public float CancelLockout => cancelLockout;
    public float FlinchShare => flinchShare;
    public float KnockdownShare => knockdownShare;
    public float KnockdownSeconds => knockdownSeconds;
    public float FlinchSeconds => flinchSeconds;
    public float FlinchGrace => flinchGrace;
    public float HeavyHitShare => heavyHitShare;
    public float HeavyHitInvulnerable => heavyHitInvulnerable;
    public bool ShowComboHud => showComboHud;
    public float HeavyHoldSeconds => heavyHoldSeconds;
    public float HeavyChargeSeconds => heavyChargeSeconds;
    public float HeavyChargeDamage => heavyChargeDamage;
    public float HeavyChargePoise => heavyChargePoise;
    public float HeldChargeDrain => heldChargeDrain;
    public float RushAfterDodgeSeconds => rushAfterDodgeSeconds;
    public float RushRunShare => rushRunShare;
    public float RushRunSeconds => rushRunSeconds;
    public float RushRunAngle => rushRunAngle;
    public float BackstepAngle => backstepAngle;
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
    public float FrontBonus => frontBonus;
    public float FrontHalfArc => frontHalfArc;
    public float OnScreenBonus => onScreenBonus;
    public float LastStrikerPenalty => lastStrikerPenalty;
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
    public bool ShieldGuardPoints => shieldGuardPoints;
    public float GuardPointLetThrough => guardPointLetThrough;
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
    public float ParryPoiseShare => parryPoiseShare;
    public float ParryRecoilSeconds => parryRecoilSeconds;
    public float WhiffTapSeconds => whiffTapSeconds;
    public float WhiffRelock => whiffRelock;
    public float GuardCounterSeconds => guardCounterSeconds;
    public float GuardCounterPoise => guardCounterPoise;
    public float BreakThroughPoiseShare => breakThroughPoiseShare;
    public float InterruptDelay => interruptDelay;
    public float CounterSeconds => counterSeconds;
    public float CounterDamageMultiplier => counterDamageMultiplier;
    public float SlowMotionScale => slowMotionScale;
    public float SlowMotionSeconds => slowMotionSeconds;
    public float ParryStop => parryStop;
    public float PerfectDodgeStop => perfectDodgeStop;
    public float FlinchTurnGrace => flinchTurnGrace;
    public float FlinchDodgeAfter => flinchDodgeAfter;
    public float FocusPerCircle => focusPerCircle;
    public int FocusCircles => focusCircles;
    public float FocusMax => focusPerCircle * focusCircles;
    public float FocusPerHit => focusPerHit;
    public float FocusOnParry => focusOnParry;
    public float FocusOnPerfectDodge => focusOnPerfectDodge;
    public float FocusOnBlock => focusOnBlock;
    public float FocusHoldSeconds => focusHoldSeconds;
    public float FocusDrainPerSecond => focusDrainPerSecond;
    public float PlayerDamage => playerDamage;
    public float ArtDamage => artDamage;
    public float RegainShare => regainShare;
    public float RegainSeconds => regainSeconds;
    public float RegainPerDamage => regainPerDamage;
    public float EatSeconds => eatSeconds;
    public float EatMoveSpeed => eatMoveSpeed;
    public float HealOverSeconds => healOverSeconds;
    public float DefaultPoise => defaultPoise;
    public float PoiseRecoveryDelay => poiseRecoveryDelay;
    public float PoiseRecoveryPerSecond => poiseRecoveryPerSecond;
    public float StaggerSeconds => staggerSeconds;
    public float ReelingShare => reelingShare;
    public float FelledSeconds => felledSeconds;
    public float GuardBrokenScale => guardBrokenScale;
    public int GuardBreaksToSplinter => guardBreaksToSplinter;
    public float ArrowDodgeCooldown => arrowDodgeCooldown;
    public float ArtCooldown => artCooldown;
    public float HuntersMarkSeconds => huntersMarkSeconds;
    public float HuntersMarkPoise => huntersMarkPoise;
    public float MeleeBurnShare => meleeBurnShare;
    public float MeleeBurnSeconds => meleeBurnSeconds;
    public float StaggeredDamageTaken => staggeredDamageTaken;
}
