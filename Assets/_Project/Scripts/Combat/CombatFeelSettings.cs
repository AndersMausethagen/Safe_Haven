using UnityEngine;

/// <summary>
/// How combat feels, in one asset designers can tune without code: hit stop, camera shake, hit flash, the squash punch
/// and knockback on targets, attack telegraphs and the dodge trail. Lives at Resources/CombatFeel and is read by
/// CombatFeedback, HitReaction, AttackTelegraph and PlayerDodge. Set a value to 0 to turn that effect off.
///
/// Where it belongs: Combat. Dodge distance, timing and cooldown are movement, so they live in PlayerMovementSettings.
/// </summary>
[CreateAssetMenu(fileName = "CombatFeel", menuName = "Safe Haven/Combat/Combat Feel")]
public sealed class CombatFeelSettings : ScriptableObject
{
    public const string ResourceName = "CombatFeel";

    [Header("Hit stop (seconds the game nearly freezes)")]
    [Tooltip("When the player's hit lands.")]
    [SerializeField, Min(0f)] private float hitStopOnHit = 0.04f;
    [Tooltip("When the player's hit is a critical hit.")]
    [SerializeField, Min(0f)] private float hitStopOnCrit = 0.08f;
    [Tooltip("When the player's hit kills.")]
    [SerializeField, Min(0f)] private float hitStopOnKill = 0.12f;
    [Tooltip("When the player is hurt.")]
    [SerializeField, Min(0f)] private float hitStopWhenHurt = 0.06f;
    [Tooltip("Game speed during a hit stop. 0 freezes completely.")]
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;
    [Header("One stop per swing (Docs/CombatFeelReview_2026-11-19.md §1)")]
    [Tooltip("Each further body one swing reaches lengthens its stop by this share of the first, instead of starting a stop of its own: " +
             "a sweep through a pack strobed, one freeze per body.")]
    [SerializeField, Range(0f, 1f)] private float extraTargetShare = 0.15f;
    [Tooltip("The most a swing's stop grows with more bodies, as a multiple of its first.")]
    [SerializeField, Min(1f)] private float mostPerSwing = 1.45f;
    [Tooltip("No blow of the player's holds longer than this (Street Fighter V's heaviest is about 0.25 s). Not a boss's fall or topple.")]
    [SerializeField, Min(0f)] private float stopCeiling = 0.25f;
    [Tooltip("No killing blow holds longer than this.")]
    [SerializeField, Min(0f)] private float killStopCeiling = 0.30f;
    [Tooltip("The blow that fells a boss holds this long (\"boss stop\", Docs/LookAndFeelResearch_2026-11-18.md V6), at Boss Stop Speed: " +
             "the moment the fight was for. Under Options › Hit Stop.")]
    [SerializeField, Min(0f)] private float bossStopSeconds = 0.45f;
    [SerializeField, Range(0f, 1f)] private float bossStopTimeScale = 0.03f;
    [Tooltip("The picture streaks out from a boss's killing blow (a share of the screen at its strongest; ImpactBlurFeature), easing out " +
             "over Impact Blur Seconds. Half as much on an elite felled by a heavy blow or a finisher. Under Options › Screen Flashes. 0: none.")]
    [SerializeField, Range(0f, 0.2f)] private float impactBlurStrength = 0.06f;
    [SerializeField, Min(0f)] private float impactBlurSeconds = 0.4f;
    [Tooltip("Metres the struck body shudders side to side for the hit stop (times the blow's weight), shrinking as it ends. Hosting " +
             "co-op, where time can't stop for everyone, the struck body's animation holds still instead and the attacker's slows. 0: no shudder.")]
    [SerializeField, Range(0f, 0.2f)] private float victimShake = 0.045f;

    [Header("Camera shake (0-1 strength)")]
    [SerializeField, Range(0f, 1f)] private float shakeOnHit = 0.15f;
    [SerializeField, Range(0f, 1f)] private float shakeOnCrit = 0.3f;
    [SerializeField, Range(0f, 1f)] private float shakeOnKill = 0.45f;
    [SerializeField, Range(0f, 1f)] private float shakeWhenHurt = 0.55f;
    [Tooltip("How far the camera moves at full strength, in meters.")]
    [SerializeField, Min(0f)] private float shakeMaxOffset = 0.35f;
    [Tooltip("How far the camera rolls at full strength, in degrees.")]
    [SerializeField, Min(0f)] private float shakeMaxRoll = 2.5f;
    [Tooltip("How fast the shake wobbles.")]
    [SerializeField, Min(0.1f)] private float shakeFrequency = 22f;
    [Tooltip("Strength lost per second, so shakes die out.")]
    [SerializeField, Min(0.1f)] private float shakeRecovery = 1.8f;

    [Header("Hit flash")]
    [Tooltip("Color a target flashes when hit. Values above 1 make it glow.")]
    [SerializeField, ColorUsage(false, true)] private Color flashColor = new Color(2.2f, 2.2f, 2.2f);
    [SerializeField, Min(0f)] private float flashSeconds = 0.08f;

    [Header("Hit punch (the target squashes and springs back)")]
    [Tooltip("How much the target squashes. 0.1 is 10%.")]
    [SerializeField, Range(0f, 0.5f)] private float punchAmount = 0.12f;
    [SerializeField, Min(0f)] private float punchSeconds = 0.16f;

    [Header("Hit recoil (monsters: the body snaps away from the blow)")]
    [Tooltip("Degrees a monster's body tips away from a strong blow (crits, heavy swings, finishers, counters, kills); " +
             "ordinary hits tip it 60% of that. It springs back with a small overshoot.")]
    [SerializeField, Range(0f, 40f)] private float hitRecoilDegrees = 22f;
    [Tooltip("Metres the body is shoved back by a strong blow, on top of the knockback.")]
    [SerializeField, Range(0f, 0.5f)] private float hitRecoilShove = 0.14f;
    [SerializeField, Min(0.05f)] private float hitRecoilSeconds = 0.28f;

    [Header("Knockback (monsters on the NavMesh)")]
    [Tooltip("Meters a monster is pushed by a blow of Impact 1 with no technique (a sword's slash). Every blow is pushed by its weight: " +
             "this × Impact^1.5 × its technique (HitReaction.KnockbackFor; Docs/CombatFeelReview_2026-11-19.md §4).")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.35f;
    [Tooltip("A critical's push, as a multiple.")]
    [SerializeField, Min(1f)] private float knockbackCritShare = 1.2f;
    [Tooltip("A heavy's or a finisher's push, as a multiple.")]
    [SerializeField, Min(1f)] private float knockbackHeavy = 1.5f;
    [Tooltip("A weapon art's push, as a multiple.")]
    [SerializeField, Min(1f)] private float knockbackArt = 2f;
    [Tooltip("A counter's or a guard counter's push, as a multiple.")]
    [SerializeField, Min(1f)] private float knockbackCounter = 1.3f;
    [Tooltip("What a heavyweight (a great beast, an elite, anything in a boss fight) takes of it. A boss itself is never pushed.")]
    [SerializeField, Range(0f, 1f)] private float knockbackHeavyweightShare = 0.35f;
    [Tooltip("No blow pushes further (a heavy that isn't charged at most 1 m, so its follow-up still reaches).")]
    [SerializeField, Min(0f)] private float knockbackMost = 1.6f;

    [Header("High ground (arrows and thrown missiles, for the player and creatures alike)")]
    [Tooltip("Metres the shooter must stand above the target before a missile hits harder.")]
    [SerializeField, Min(0f)] private float highGroundFrom = 1.5f;
    [Tooltip("Extra damage for every metre above that, as a share (0.03 = 3 %).")]
    [SerializeField, Min(0f)] private float highGroundPerMetre = 0.03f;
    [Tooltip("The most extra damage high ground can give, as a share.")]
    [SerializeField, Min(0f)] private float highGroundMost = 0.25f;

    [Header("Attack telegraphs")]
    [Tooltip("Transparent unlit material for the ground markers. Its color is replaced by the colors below.")]
    [SerializeField] private Material telegraphMaterial;
    [Tooltip("The warnings drawn on the ground itself (Shaders/GroundTelegraph.shader, GroundMarks): a strike's area and fill, a lob's " +
             "landing, a hazard, the opening ring, the Hunter's Mark and the backstab zone lie over slopes and steps, with a bright rim, a " +
             "gradient and a pattern per kind. Empty: the flat fans drawn with Telegraph Material.")]
    [SerializeField] private Material groundMarkMaterial;
    [Tooltip("The full area the attack will hit.")]
    [SerializeField] private Color telegraphAreaColor = new Color(1f, 0.25f, 0.15f, 0.18f);
    [Tooltip("Grows over the area as the attack winds up; when it fills the area, the hit lands.")]
    [SerializeField] private Color telegraphFillColor = new Color(1f, 0.3f, 0.1f, 0.45f);
    [Tooltip("Seconds the marker lingers, bright, after the hit lands.")]
    [SerializeField, Min(0f)] private float telegraphLinger = 0.1f;
    [Tooltip("The fill of a blow no guard stops (a grab, a shield rush, a quake): it can only be dodged.")]
    [SerializeField] private Color unblockableColor = new Color(0.72f, 0.12f, 0.95f, 0.55f);
    [Tooltip("The fill of a strike any blow in its wind-up breaks off (Interruptible: a call, a cast, a quick shove), so the player " +
             "learns which ones to hit rather than dodge.")]
    [SerializeField] private Color interruptibleColor = new Color(0.3f, 0.78f, 1f, 0.5f);
    [Tooltip("The edge drawn round a heavy strike's area: a creature's great blow (Monster Attack › Heavy) or one with Impact at least Heavy Impact.")]
    [SerializeField] private Color heavyEdgeColor = new Color(0.75f, 0.04f, 0.02f, 0.8f);
    [Tooltip("Width of a heavy strike's edge, as a share of its reach.")]
    [SerializeField, Range(0.02f, 0.3f)] private float heavyEdgeWidth = 0.09f;
    [SerializeField, Range(0.25f, 3f)] private float heavyImpact = 1.5f;
    [Tooltip("Plays as a heavy strike starts its wind-up: a low swell, heard before it is seen.")]
    [SerializeField] private SoundDefinition heavyWindupSound;
    [Tooltip("The ring at a creature's feet after its blow lands, shrinking until it can act again: the opening to punish it in.")]
    [SerializeField] private Color openingColor = new Color(0.45f, 1f, 0.5f, 0.45f);
    [Tooltip("Metres the opening ring reaches past the body when it opens (it shrinks in to the body as it closes), and the ring's width.")]
    [SerializeField, Min(0f)] private float openingReach = 0.45f;
    [SerializeField, Min(0.02f)] private float openingWidth = 0.12f;

    [Header("Reeling (a creature's poise worn low: Combat Technique Settings › Reeling Share)")]
    [Tooltip("Looping effect at a reeling creature's chest (embers through the cracks).")]
    [SerializeField] private GameObject reelingEffect;
    [Tooltip("Degrees a reeling body sways side to side, and how many times a second.")]
    [SerializeField, Min(0f)] private float reelingSway = 4f;
    [SerializeField, Min(0.1f)] private float reelingSwayRate = 0.9f;
    [Tooltip("The poise bar pulses towards this colour while the creature reels.")]
    [SerializeField] private Color reelingBarColor = new Color(1f, 0.55f, 0.15f, 1f);
    [Tooltip("Looping breath or steam over a creature that has turned in fury, badly hurt (Monster Definition > When Hurt).")]
    [SerializeField] private GameObject furyEffect;
    [Tooltip("The thin ring at the feet of a creature marked by Hunter's Mark (a bow's perfect release).")]
    [SerializeField] private Color huntersMarkColor = new Color(0.55f, 1f, 0.75f, 0.55f);

    [Header("A coated blade (a weapon coating: Ember Pitch)")]
    [Tooltip("Looping flames on the weapon in hand while a coating's fire is on it (CoatedBladeView): its shape is set to the weapon's " +
             "own mesh, it smoulders at rest and roars through a swing.")]
    [SerializeField] private GameObject coatedFlames;
    [Tooltip("Flames a second at rest, and through a swing.")]
    [SerializeField, Min(0f)] private float coatedFlamesAtRest = 14f;
    [SerializeField, Min(0f)] private float coatedFlamesSwinging = 90f;

    [Header("Dodge")]
    [Tooltip("Trail left behind while dodging. Uses the telegraph material.")]
    [SerializeField] private Gradient dodgeTrailColor = DefaultTrail();
    [SerializeField, Min(0f)] private float dodgeTrailWidth = 0.6f;
    [Tooltip("Text shown over the player when a dodge avoids a hit. Empty shows nothing.")]
    [SerializeField] private string dodgedText = "Dodged";
    [SerializeField] private Color dodgedTextColor = new Color(0.6f, 0.9f, 1f);

    [Header("Technique callouts (text over the player; empty shows nothing, the default: sound and motion say it)")]
    [SerializeField] private string perfectDodgeText = "";
    [SerializeField] private string parryText = "";
    [SerializeField] private string guardBrokenText = "";
    [SerializeField] private string staggerText = "";
    [SerializeField] private string counterText = "";
    [Tooltip("Over a creature whose wind-up a blow broke off (it also sparks and clangs).")]
    [SerializeField] private string interruptText = "";
    [SerializeField] private Color techniqueTextColor = new Color(1f, 0.85f, 0.4f);
    [Tooltip("The telegraph fill turns this color for the last moment before the hit: the time to raise the guard and parry.")]
    [SerializeField] private Color parryCueColor = new Color(1f, 0.92f, 0.8f, 0.55f);

    [Header("Effects (pooled particle prefabs; empty plays none)")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] private GameObject criticalHitEffect;
    [SerializeField] private GameObject structureHitEffect;
    [SerializeField] private GameObject blockEffect;
    [SerializeField] private GameObject parryEffect;
    [SerializeField] private GameObject staggerEffect;
    [SerializeField] private GameObject perfectDodgeEffect;
    [Tooltip("Small glint on an attacker at the parry moment.")]
    [SerializeField] private GameObject parryCueEffect;
    [Header("A parry cue to act on (Docs/CombatFeelReview_2026-11-19.md §2)")]
    [Tooltip("The glint and its sound come this long before the parry window opens, on a strike at the player: the window (0.15 s) is " +
             "shorter than anyone reacts to a light (180-250 ms), a little longer than to a sound. A strike that may hold never cues " +
             "before its hold point. The window itself is unchanged.")]
    [SerializeField, Range(0f, 0.3f)] private float parryCueLead = 0.12f;
    [Tooltip("Heard with the lead cue: a short bright ting, quieter than a parry.")]
    [SerializeField] private SoundDefinition parryCueSound;
    [Tooltip("A thin line on a strike's warning where its fill stands in the middle of the parry window: press as the front crosses it " +
             "(timing a moving thing to a mark is a far better skill than reacting). Its alpha and width (a share of the radius).")]
    [SerializeField, Range(0f, 1f)] private float parryLineAlpha = 0.35f;
    [SerializeField, Range(0.005f, 0.2f)] private float parryLineWidth = 0.04f;
    [Header("The blow that ends a fight (Docs/CombatFeelReview_2026-11-19.md §8)")]
    [Tooltip("The player's kill of the last hostile after the player's side within Fight Hold Range, in a fight of at least Fight Won " +
             "Foes foes or Fight Won Seconds: after its stop, the game slows to this for Last Kill Slow Seconds (real time), the sting " +
             "plays and the fight is over at once instead of after the quiet stretch. Not for a boss (its own stop) or wildlife.")]
    [SerializeField, Range(0.05f, 1f)] private float lastKillSlowScale = 0.3f;
    [SerializeField, Min(0f)] private float lastKillSlowSeconds = 0.35f;
    [SerializeField, Min(1)] private int fightWonFoes = 2;
    [SerializeField, Min(0f)] private float fightWonSeconds = 4f;
    [Tooltip("Heard as the last foe falls: one low drum stroke and a breath of wind.")]
    [SerializeField] private SoundDefinition fightWonSound;
    [Header("A charge you can feel building (§11)")]
    [Tooltip("Heard while a heavy is held to charge, from the player, stopped when it is let go or broken off.")]
    [SerializeField] private SoundDefinition chargeRiseSound;
    [Tooltip("The weapon's light while charging: from Charge Light Low at the least charge to Charge Light High at full, its range " +
             "from Charge Light Range Low to High; it flickers at full.")]
    [SerializeField] private Color chargeLightLow = new Color(1f, 0.62f, 0.3f);
    [SerializeField] private Color chargeLightHigh = new Color(1f, 0.9f, 0.7f);
    [SerializeField, Min(0f)] private float chargeLightRangeLow = 1.1f;
    [SerializeField, Min(0f)] private float chargeLightRangeHigh = 1.8f;
    [SerializeField, Min(0f)] private float chargeLightIntensityLow = 0.3f;
    [SerializeField, Min(0f)] private float chargeLightIntensityHigh = 2f;
    [Header("A camera kick along big blows (§15)")]
    [Tooltip("The player's heavy, finisher and art landings push the frame along the blow by this many metres per Impact (a hammer's " +
             "heavy 0.09 m), once a swing, and shake this share as hard as before (the push carries them, not a rattle). A heavy " +
             "strike taken (Impact from Heavy Impact) pushes it the way the blow went.")]
    [SerializeField, Range(0f, 0.2f)] private float kickPerImpact = 0.05f;
    [SerializeField, Range(0f, 1f)] private float kickShakeShare = 0.6f;
    [Tooltip("Where a lobbed boulder or a burst of roots lands. Empty: the structure-hit dust.")]
    [SerializeField] private GameObject landingEffect;

    [Header("Slash arcs")]
    [Tooltip("Material of the arc drawn along a swing (Safe Haven/Slash). Empty draws none.")]
    [SerializeField] private Material slashMaterial;
    [Tooltip("Seconds the arc takes to sweep and fade.")]
    [SerializeField, Min(0.02f)] private float slashSeconds = 0.2f;
    [Tooltip("Height of the arc above the attacker's feet.")]
    [SerializeField] private float slashHeight = 1.05f;
    [Tooltip("Multiplies the attacks' Slash Color (HDR), so the arc glows through bloom instead of vanishing against " +
             "bright ground seen from the high camera.")]
    [SerializeField, Min(0f)] private float slashBrightness = 2.5f;
    [Tooltip("Seconds before a swing lands that its whoosh plays: as the weapon moves, not as the wind-up starts (an " +
             "axe's whoosh used to come over half a second early).")]
    [SerializeField, Min(0f)] private float swingSoundLead = 0.12f;
    [Tooltip("Draws the flat arc as well as the blade's trail. Off, the trail alone shows the swing.")]
    [SerializeField] private bool slashArcs;

    [Header("Weapon trail (the ribbon following the blade)")]
    [Tooltip("Seconds the ribbon takes to fade behind the blade. 0 draws none.")]
    [SerializeField, Min(0f)] private float trailSeconds = 0.16f;
    [Tooltip("Multiplies the attacks' Slash Color, as Slash Brightness does for the arc.")]
    [SerializeField, Min(0f)] private float trailBrightness = 0.8f;
    [Tooltip("How much of the weapon's length the ribbon covers, from the tip back. 1 goes all the way to the end in the hand.")]
    [SerializeField, Range(0.1f, 1f)] private float trailBladeShare = 0.72f;

    [Header("Arrows")]
    [Tooltip("Seconds the trail behind an arrow lasts. It is what shows a shot from the high camera. 0 draws none.")]
    [SerializeField, Min(0f)] private float arrowTrailSeconds = 0.16f;
    [SerializeField, Min(0f)] private float arrowTrailWidth = 0.055f;
    [SerializeField] private Color arrowTrailColor = new Color(0.95f, 0.95f, 0.88f, 0.55f);
    [Tooltip("The trail of a perfect release.")]
    [SerializeField] private Color arrowTrailPerfectColor = new Color(1f, 0.78f, 0.32f, 0.95f);
    [Tooltip("How hard the camera kicks as a bow is loosed, from a bare draw to a full one.")]
    [SerializeField, Range(0f, 1f)] private float shotKickMin = 0.02f;
    [SerializeField, Range(0f, 1f)] private float shotKickMax = 0.1f;
    [Tooltip("A blow further from the player than this is heard as if from here, in metres: an arrow's hit still has to reach the shooter.")]
    [SerializeField, Min(0f)] private float hitHeardWithin = 12f;

    [Header("Markers")]
    [Tooltip("Ring on the ground under the enemy in focus. Empty draws none.")]
    [SerializeField] private Material focusMarkerMaterial;
    [SerializeField] private Color focusMarkerColor = new Color(1f, 0.72f, 0.3f, 0.75f);
    [Tooltip("The wedge behind a staggering enemy: where a backstab lands.")]
    [SerializeField] private Color backstabMarkerColor = new Color(1f, 0.92f, 0.75f, 0.3f);
    [Tooltip("What that wedge warms toward as the enemy learns to see backstabs coming.")]
    [SerializeField] private Color backstabMarkerWaryColor = new Color(1f, 0.32f, 0.12f, 0.42f);
    [Tooltip("Plays with a backstab, over the weapon's own hit.")]
    [SerializeField] private SoundDefinition backstabSound;
    [Tooltip("Seconds the game slows for a backstab, on top of its hit stop.")]
    [SerializeField, Min(0f)] private float backstabSlowSeconds = 0.16f;

    [Header("Blood")]
    [Tooltip("One copy per scene sprays it (BloodEffects). Empty: no blood.")]
    [SerializeField] private BloodEffects bloodEffects;
    [Tooltip("Plays with a killing blow on what bleeds.")]
    [SerializeField] private SoundDefinition bloodKillSound;
    [Tooltip("Metres per second of the fastest droplets from the weakest blow, and from the strongest.")]
    [SerializeField, Min(0f)] private float bloodSpeedMin = 2.5f;
    [SerializeField, Min(0f)] private float bloodSpeedMax = 10f;
    [Tooltip("Droplets from the weakest blow, and from the strongest.")]
    [SerializeField, Min(0)] private int bloodDropletsMin = 14;
    [SerializeField, Min(0)] private int bloodDropletsMax = 60;
    [Tooltip("Stains on the ground from the weakest blow, and from the strongest.")]
    [SerializeField, Min(0)] private int bloodStainsMin = 2;
    [SerializeField, Min(0)] private int bloodStainsMax = 12;
    [Tooltip("Metres from the blow the furthest stain lands, for the strongest blow.")]
    [SerializeField, Min(0f)] private float bloodReach = 3.2f;
    [Tooltip("Seconds a stain stays on the ground. Set the stain system's Max Particles in the prefab with this in mind.")]
    [SerializeField, Min(1f)] private float bloodStainSeconds = 30f;
    [Tooltip("Metres per second of the strongest jet from a corpse's wound, and how far it reaches on the ground.")]
    [SerializeField, Min(0f)] private float bloodSpurtSpeed = 6f;
    [SerializeField, Min(0f)] private float bloodSpurtReach = 2.6f;
    [Tooltip("Seconds a killed body goes on pumping blood from its wound. 0: it doesn't.")]
    [SerializeField, Min(0f)] private float corpseBleedSeconds = 5f;
    [Tooltip("Seconds between pulses, as the bleeding starts and as it runs out.")]
    [SerializeField, Min(0.05f)] private float corpseSpurtFirst = 0.16f;
    [SerializeField, Min(0.05f)] private float corpseSpurtLast = 0.7f;
    [Tooltip("Seconds a pool of blood stays on the ground. It spreads through the first part of that, as the pool system's Size Over Lifetime says.")]
    [SerializeField, Min(1f)] private float bloodPoolSeconds = 45f;

    [Header("Corpses")]
    [Tooltip("Metres per second a killed body is thrown by the weakest blow, and by the strongest.")]
    [SerializeField, Min(0f)] private float corpseThrowMin = 1.5f;
    [SerializeField, Min(0f)] private float corpseThrowMax = 5.5f;
    [Tooltip("How much of the throw goes upward: 0 is flat along the ground.")]
    [SerializeField, Range(0f, 2f)] private float corpseLift = 0.45f;
    [Tooltip("Seconds of physics before a corpse is left lying still, which costs nothing.")]
    [SerializeField, Min(0.5f)] private float corpseSettleSeconds = 4f;
    [Tooltip("Seconds a corpse lies before it sinks into the ground and is gone.")]
    [SerializeField, Min(1f)] private float corpseSeconds = 14f;
    [Tooltip("The most corpses at once; beyond it the oldest goes.")]
    [SerializeField, Min(1)] private int corpseLimit = 14;

    [Header("Gore (killing blows; none while the Blood option is off)")]
    [Tooltip("Chance a two-handed blunt weapon's killing blow bursts a small creature (Monster › Can Be Smashed) into pieces.")]
    [SerializeField, Range(0f, 1f)] private float smashChance = 0.35f;
    [Tooltip("Chance a slashing killing blow takes the head or a limb (Monster › Can Lose Limbs).")]
    [SerializeField, Range(0f, 1f)] private float severChance = 0.3f;
    [Tooltip("Of those, the share that take the head.")]
    [SerializeField, Range(0f, 1f)] private float severHeadShare = 0.4f;
    [Tooltip("A cut-off stump pumps this many times as long and as hard as an ordinary wound.")]
    [SerializeField, Range(1f, 4f)] private float severBleed = 1.8f;
    [Tooltip("Metres per second the pieces of a smashed body are flung, slowest and fastest.")]
    [SerializeField, Min(0f)] private float smashSpeedMin = 3.5f;
    [SerializeField, Min(0f)] private float smashSpeedMax = 9f;
    [Tooltip("Gobbets of gore thrown out besides the body's own pieces.")]
    [SerializeField, Range(0, 24)] private int smashGobbets = 9;
    [Tooltip("Metres round a smashed body that its blood splatters the ground.")]
    [SerializeField, Min(0f)] private float smashSplatterReach = 3.5f;

    [Header("Hurt vignette")]
    [Tooltip("Screen edges flash this color when the player is hurt. Alpha is the strength.")]
    [SerializeField] private Color hurtColor = new Color(0.65f, 0.05f, 0.02f, 0.55f);
    [SerializeField, Min(0f)] private float hurtSeconds = 0.45f;
    [Tooltip("Below this share of health the edges pulse slowly.")]
    [SerializeField, Range(0f, 1f)] private float lowHealthBelow = 0.3f;
    [SerializeField, Range(0f, 1f)] private float lowHealthStrength = 0.35f;
    [Tooltip("Edges flash this color on a parry or perfect dodge.")]
    [SerializeField] private Color techniqueFlashColor = new Color(0.8f, 0.85f, 0.9f, 0.12f);

    private static CombatFeelSettings current;

    /// <summary>The settings from Resources/CombatFeel, or built-in defaults when the asset is missing.</summary>
    public static CombatFeelSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<CombatFeelSettings>(ResourceName);
                if (current == null)
                {
                    current = CreateInstance<CombatFeelSettings>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return current;
        }
    }

    public float HitStopOnHit => hitStopOnHit;
    public float HitStopOnCrit => hitStopOnCrit;
    public float VictimShake => victimShake;
    public string InterruptText => interruptText;
    public float HitStopOnKill => hitStopOnKill;
    public float HitStopWhenHurt => hitStopWhenHurt;
    public float HitStopTimeScale => hitStopTimeScale;
    public float ExtraTargetShare => extraTargetShare;
    public float MostPerSwing => mostPerSwing;
    public float StopCeiling => stopCeiling;
    public float KillStopCeiling => killStopCeiling;
    public float BossStopSeconds => bossStopSeconds;
    public float BossStopTimeScale => bossStopTimeScale;
    public float ImpactBlurStrength => impactBlurStrength;
    public float ImpactBlurSeconds => impactBlurSeconds;
    public float ShakeOnHit => shakeOnHit;
    public float ShakeOnCrit => shakeOnCrit;
    public float ShakeOnKill => shakeOnKill;
    public float ShakeWhenHurt => shakeWhenHurt;
    public float ShakeMaxOffset => shakeMaxOffset;
    public float ShakeMaxRoll => shakeMaxRoll;
    public float ShakeFrequency => shakeFrequency;
    public float ShakeRecovery => shakeRecovery;
    public Color FlashColor => flashColor;
    public float FlashSeconds => flashSeconds;
    public float PunchAmount => punchAmount;
    public float PunchSeconds => punchSeconds;
    public float HitRecoilDegrees => hitRecoilDegrees;
    public float HitRecoilShove => hitRecoilShove;
    public float HitRecoilSeconds => hitRecoilSeconds;
    public float KnockbackDistance => knockbackDistance;
    public float KnockbackCritShare => knockbackCritShare;
    public float KnockbackHeavy => knockbackHeavy;
    public float KnockbackArt => knockbackArt;
    public float KnockbackCounter => knockbackCounter;
    public float KnockbackHeavyweightShare => knockbackHeavyweightShare;
    public float KnockbackMost => knockbackMost;

    /// <summary>How much harder a missile hits from this many metres above its target: a damage multiplier, 1 or more.</summary>
    public float HighGround(float above) => 1f + Mathf.Clamp((above - highGroundFrom) * highGroundPerMetre, 0f, highGroundMost);

    /// <summary>The high ground multiplier for a missile from <paramref name="shooter"/> striking <paramref name="target"/> (1 without settings).</summary>
    public static float HighGroundFor(Vector3 shooter, Vector3 target) => Current != null ? Current.HighGround(shooter.y - target.y) : 1f;
    public Material TelegraphMaterial => telegraphMaterial;
    public Material GroundMarkMaterial => groundMarkMaterial;
    public Color TelegraphAreaColor => telegraphAreaColor;
    public Color TelegraphFillColor => telegraphFillColor;
    public float TelegraphLinger => telegraphLinger;
    public Gradient DodgeTrailColor => dodgeTrailColor;
    public float DodgeTrailWidth => dodgeTrailWidth;
    public string DodgedText => dodgedText;
    public Color DodgedTextColor => dodgedTextColor;
    public string PerfectDodgeText => perfectDodgeText;
    public string ParryText => parryText;
    public string GuardBrokenText => guardBrokenText;
    public string StaggerText => staggerText;
    public string CounterText => counterText;
    public Color TechniqueTextColor => techniqueTextColor;
    public Color ParryCueColor => parryCueColor;
    public Color UnblockableColor => unblockableColor;
    public Color InterruptibleColor => interruptibleColor;
    public Color HeavyEdgeColor => heavyEdgeColor;
    public float HeavyEdgeWidth => heavyEdgeWidth;
    public float HeavyImpact => heavyImpact;
    public SoundDefinition HeavyWindupSound => heavyWindupSound;
    public Color OpeningColor => openingColor;
    public float OpeningReach => openingReach;
    public float OpeningWidth => openingWidth;
    public GameObject ReelingEffect => reelingEffect;
    public GameObject CoatedFlames => coatedFlames;
    public float CoatedFlamesAtRest => coatedFlamesAtRest;
    public float CoatedFlamesSwinging => coatedFlamesSwinging;
    public float ReelingSway => reelingSway;
    public float ReelingSwayRate => reelingSwayRate;
    public Color ReelingBarColor => reelingBarColor;
    public GameObject FuryEffect => furyEffect;
    public Color HuntersMarkColor => huntersMarkColor;
    public GameObject LandingEffect => landingEffect != null ? landingEffect : structureHitEffect;
    public GameObject HitEffect => hitEffect;
    public GameObject CriticalHitEffect => criticalHitEffect;
    public GameObject StructureHitEffect => structureHitEffect;
    public GameObject BlockEffect => blockEffect;
    public GameObject ParryEffect => parryEffect;
    public GameObject StaggerEffect => staggerEffect;
    public GameObject PerfectDodgeEffect => perfectDodgeEffect;
    public GameObject ParryCueEffect => parryCueEffect;
    public float ParryCueLead => parryCueLead;
    public SoundDefinition ParryCueSound => parryCueSound;
    public float ParryLineAlpha => parryLineAlpha;
    public float ParryLineWidth => parryLineWidth;
    public float LastKillSlowScale => lastKillSlowScale;
    public float LastKillSlowSeconds => lastKillSlowSeconds;
    public int FightWonFoes => fightWonFoes;
    public float FightWonSeconds => fightWonSeconds;
    public SoundDefinition FightWonSound => fightWonSound;
    public SoundDefinition ChargeRiseSound => chargeRiseSound;
    public Color ChargeLightLow => chargeLightLow;
    public Color ChargeLightHigh => chargeLightHigh;
    public float ChargeLightRangeLow => chargeLightRangeLow;
    public float ChargeLightRangeHigh => chargeLightRangeHigh;
    public float ChargeLightIntensityLow => chargeLightIntensityLow;
    public float ChargeLightIntensityHigh => chargeLightIntensityHigh;
    public float KickPerImpact => kickPerImpact;
    public float KickShakeShare => kickShakeShare;
    public Material SlashMaterial => slashMaterial;
    public float SlashSeconds => slashSeconds;
    public float SlashHeight => slashHeight;
    public float SlashBrightness => slashBrightness;
    public float SwingSoundLead => swingSoundLead;
    public bool SlashArcs => slashArcs;
    public float TrailSeconds => trailSeconds;
    public float TrailBrightness => trailBrightness;
    public float TrailBladeShare => trailBladeShare;
    public float ArrowTrailSeconds => arrowTrailSeconds;
    public float ArrowTrailWidth => arrowTrailWidth;
    public Color ArrowTrailColor => arrowTrailColor;
    public Color ArrowTrailPerfectColor => arrowTrailPerfectColor;
    public float ShotKickMin => shotKickMin;
    public float ShotKickMax => shotKickMax;
    public float HitHeardWithin => hitHeardWithin;
    public Material FocusMarkerMaterial => focusMarkerMaterial;
    public Color FocusMarkerColor => focusMarkerColor;
    public Color BackstabMarkerColor => backstabMarkerColor;
    public Color BackstabMarkerWaryColor => backstabMarkerWaryColor;
    public SoundDefinition BackstabSound => backstabSound;
    public float BackstabSlowSeconds => backstabSlowSeconds;
    public BloodEffects BloodEffects => bloodEffects;
    public SoundDefinition BloodKillSound => bloodKillSound;
    public float BloodSpeedMin => bloodSpeedMin;
    public float BloodSpeedMax => bloodSpeedMax;
    public int BloodDropletsMin => bloodDropletsMin;
    public int BloodDropletsMax => bloodDropletsMax;
    public int BloodStainsMin => bloodStainsMin;
    public int BloodStainsMax => bloodStainsMax;
    public float BloodReach => bloodReach;
    public float BloodStainSeconds => bloodStainSeconds;
    public float BloodSpurtSpeed => bloodSpurtSpeed;
    public float BloodSpurtReach => bloodSpurtReach;
    public float CorpseBleedSeconds => corpseBleedSeconds;
    public float CorpseSpurtFirst => corpseSpurtFirst;
    public float CorpseSpurtLast => corpseSpurtLast;
    public float BloodPoolSeconds => bloodPoolSeconds;
    public float CorpseThrowMin => corpseThrowMin;
    public float CorpseThrowMax => corpseThrowMax;
    public float CorpseLift => corpseLift;
    public float CorpseSettleSeconds => corpseSettleSeconds;
    public float CorpseSeconds => corpseSeconds;
    public int CorpseLimit => corpseLimit;
    public float SmashChance => smashChance;
    public float SeverChance => severChance;
    public float SeverHeadShare => severHeadShare;
    public float SeverBleed => severBleed;
    public float SmashSpeedMin => smashSpeedMin;
    public float SmashSpeedMax => smashSpeedMax;
    public int SmashGobbets => smashGobbets;
    public float SmashSplatterReach => smashSplatterReach;
    public Color HurtColor => hurtColor;
    public float HurtSeconds => hurtSeconds;
    public float LowHealthBelow => lowHealthBelow;
    public float LowHealthStrength => lowHealthStrength;
    public Color TechniqueFlashColor => techniqueFlashColor;

    private static Gradient DefaultTrail()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.5f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
