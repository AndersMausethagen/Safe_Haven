using UnityEngine;

/// <summary>
/// One way of dealing damage: a basic attack, a shield bash, and later an ability. It says what damage is dealt, how it
/// scales, what area it covers and when it lands. The player's weapon attacks and V Rising style abilities are the same
/// kind of thing, so an ability is just another AttackDefinition (for example Fire damage, no weapon ratio, scaled by
/// spell power) that its caster hands to AttackResolver.
///
/// Damage before the target's defenses = (Base Damage + weapon damage x Weapon Damage Ratio)
///                                       x (1 + power x damage per power x Power Scaling).
/// </summary>
[CreateAssetMenu(fileName = "Attack", menuName = "Safe Haven/Combat/Attack")]
public sealed class AttackDefinition : ScriptableObject
{
    [SerializeField] private string displayName;

    [Header("Damage")]
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("How a physical blow lands: Blunt (crushing), Slash (cutting) or Stab (piercing). Targets resist or are weak " +
             "to each kind (Blunt/Slash/Stab Resistance). None: neither (a spell, a shove that only pushes).")]
    [SerializeField] private StrikeKind strike;
    [Tooltip("Flat damage that doesn't depend on a weapon. Abilities mostly use this.")]
    [SerializeField, Min(0f)] private float baseDamage;
    [Tooltip("How much of the equipped weapon's damage this attack uses. 1 is 100%, 0 ignores the weapon.")]
    [SerializeField, Min(0f)] private float weaponDamageRatio = 1f;
    [Tooltip("How strongly the attacker's power scales this attack. 1 is normal, 0 means power does nothing.")]
    [SerializeField, Min(0f)] private float powerScaling = 1f;
    [Tooltip("Multiplier on damage dealt to structures. Lower it for attacks that are poor at breaking buildings.")]
    [SerializeField, Min(0f)] private float structureDamageMultiplier = 1f;
    [SerializeField] private bool canCrit = true;

    [Header("Hit area")]
    [Tooltip("How far in front of the attacker the hit reaches, measured to the nearest point of the target.")]
    [SerializeField, Min(0.1f)] private float range = 2f;
    [Tooltip("Width of the hit cone in degrees. 360 hits everything around the attacker.")]
    [SerializeField, Range(1f, 360f)] private float arc = 110f;
    [Tooltip("The most targets one hit can reach, nearest first. 0 means no limit. 1 by default: an attack only hits " +
             "several targets (a sweep, a slam) when it is authored to.")]
    [SerializeField, Min(0)] private int maxTargets = 1;

    [Header("Timing")]
    [Tooltip("Seconds after the attack starts when the damage lands. Match it to the swing of the animation.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.3f;
    [Tooltip("Seconds after the attack starts before the next one can begin. Match it to where the animation can chain.")]
    [SerializeField, Min(0f)] private float recoveryTime = 0.6f;

    [Header("Weight")]
    [Tooltip("How much a hit wears down the target's poise. Enough of it in a short time staggers the target.")]
    [SerializeField, Min(0f)] private float poiseDamage = 10f;
    [Tooltip("How much the blow weighs: multiplies its hit stop and camera shake, the blood it draws and how far a killed target " +
             "flies. 1 is a sword, below 1 a dagger, above 1 an axe or a hammer. It changes no damage.")]
    [SerializeField, Range(0.25f, 3f)] private float impact = 1f;
    [Tooltip("Metres the attacker steps forward into the blow, ending as it lands (toward a target within reach).")]
    [SerializeField, Min(0f)] private float lunge;
    [Tooltip("Metres the body leaves the ground over the wind-up, landing just after the blow (a leaping spin). Only the model rises: " +
             "the feet's collider stays, so nothing is jumped over. 0: on the ground (the user, 2026-11-19: the sword's last swing " +
             "barely left the ground).")]
    [SerializeField, Min(0f)] private float hop;
    [Tooltip("Stamina the player pays for it. Below 0 uses the player's normal attack cost.")]
    [SerializeField] private float staminaCost = -1f;

    [Header("Status")]
    [Tooltip("Left behind by a hit that gets through (not blocked or parried): poison, chill, roots. Empty: none.")]
    [SerializeField] private StatusEffectDefinition appliesStatus;
    [Tooltip("Chance from 0 to 1 that a hit leaves the status.")]
    [SerializeField, Range(0f, 1f)] private float statusChance = 1f;

    [Header("The world")]
    [Tooltip("What the blow does to the ground and the grass round it when it lands (dust, wind, a crater). Empty: nothing, " +
             "except the player's heavy swings, which use Ground Impact Settings by the kind of blow.")]
    [SerializeField] private GroundImpactDefinition groundImpact;

    [Header("Grabs and rushes")]
    [Tooltip("Can't be blocked or parried, only dodged: a grab, a shield rush, a quake. Its warning on the ground shows in " +
             "Combat Feel's Unblockable colour.")]
    [SerializeField] private bool unblockable;
    [Tooltip("Metres a hit drags its target toward the attacker (a tongue, a hand from the bog). Negative throws it away.")]
    [SerializeField] private float pull;

    [Header("Movement (player)")]
    [Tooltip("Share of walking speed the player keeps while this swing winds up (on the arms, over walking legs). Below 0 uses Combat " +
             "Technique Settings' Windup Move Speed for a light swing or a shield bash, and plants a heavy, a rush or an art. 0 plants the feet.")]
    [SerializeField, Range(-1f, 1f)] private float windupMove = -1f;
    [Tooltip("Share of walking speed from Move At to the end of the swing: the arms finish it while the legs walk off. Below 0 uses the " +
             "settings' Swing Move Speed (Heavy Move Speed for a heavy). It never brings the next swing sooner.")]
    [SerializeField, Range(-1f, 1f)] private float afterContactMove = -1f;
    [Tooltip("Degrees per second the blow turns toward where the player aims early in its wind-up (the focused enemy is followed at " +
             "Swing Tracking instead). Below 0 uses the settings' Windup Turn Rate. 0 aims it only as it starts.")]
    [SerializeField, Min(-1f)] private float windupTurnRate = -1f;
    [Tooltip("Seconds after the blow lands before the player walks and turns again. Below 0 uses the settings' Move After Contact.")]
    [SerializeField, Min(-1f)] private float moveAt = -1f;
    [Tooltip("Share of the wind-up, counted back from the blow, that the step into it (Lunge) takes: 0.33 is the last third. Below 0 " +
             "uses the settings' Lunge Share. A rush steps all through its wind-up.")]
    [SerializeField, Range(-1f, 1f)] private float lungeShare = -1f;

    [Header("How a weapon art moves (player)")]
    [Tooltip("None: an ordinary swing. Dash Through: the player dashes Motion Distance through enemies, striking all along the way, and " +
             "ends past them (behind for a backstab). Blink Behind: steps at once to the back of the enemy in focus within Motion Distance, " +
             "then strikes. Leap To: closes up to Motion Distance on the enemy in focus over the whole wind-up. Throw: the weapon flies " +
             "Motion Distance, striking what it meets (up to Max Targets), and comes back to the hand.")]
    [SerializeField] private ArtMotion motion;
    [SerializeField, Min(0f)] private float motionDistance = 3.5f;
    [Tooltip("Always a critical hit (a dagger's focused stab).")]
    [SerializeField] private bool alwaysCritical;

    [Header("Cast (player: a staff's bolts)")]
    [Tooltip("The swing casts this instead of striking in melee: a projectile prefab with a MonsterProjectile (a rune bolt), fired as the " +
             "blow would land. It strikes as this attack would, at the weapon's damage. Empty: an ordinary blow.")]
    [SerializeField] private GameObject castProjectile;
    [SerializeField, Min(1)] private int castCount = 1;
    [Tooltip("Degrees between bolts when it casts several (a fan).")]
    [SerializeField, Range(0f, 45f)] private float castSpread = 12f;
    [SerializeField, Min(1f)] private float castSpeed = 18f;
    [SerializeField, Min(0.05f)] private float castRadius = 0.3f;
    [SerializeField, Min(1f)] private float castRange = 16f;
    [Tooltip("Left where a bolt comes down (a GroundHazard prefab: binding runes, a web). Empty: none.")]
    [SerializeField] private GameObject castHazard;

    [Header("Animation (player)")]
    [Tooltip("Animator state the swing plays, by name (Safe Haven › Setup › Characters › Set Up Combat Animations makes them). " +
             "Empty uses the weapon stance's old attack animation.")]
    [SerializeField] private string animationState;
    [Tooltip("Playback speed of the build-up, until the strike takes over. Below 1 it reads more clearly: you see the blow coming. " +
             "Set Up Combat Animations works it out so the swing still lands at its Hit Delay.")]
    [SerializeField, Range(0.05f, 3f)] private float animationSpeed = 1f;
    [Tooltip("Playback speed of the strike, the last moment before the blow lands. Faster than the build-up is what makes it snap.")]
    [SerializeField, Range(0.25f, 4f)] private float strikeSpeed = 1f;
    [Tooltip("Seconds before the blow lands that the strike takes over. 0 plays the whole wind-up at Animation Speed. A held heavy " +
             "swing waits here while it charges.")]
    [SerializeField, Min(0f)] private float strikeLead;
    [Tooltip("Playback speed from the hit on: the follow-through. 1 keeps the clip's own snap.")]
    [SerializeField, Range(0.25f, 3f)] private float followThroughSpeed = 1f;
    [Tooltip("A heavy swing a held press grows into takes up the light swing's pose, as far into its own wind-up as the light one " +
             "had got, when it is the same motion going on. Off for a heavy that is another motion altogether (the fists' high kick " +
             "after a punch): it starts from its first frame at its own pace, instead of crawling through the last of its wind-up.")]
    [SerializeField] private bool growsFromLight = true;
    [Tooltip("Seconds the swing blends in over, from whatever pose comes before it (the stance's ready pose, the chain's swing before " +
             "it, the guard). Measured and written by Safe Haven › Tools › Fit Attack Blends (AttackFlow): long enough that no " +
             "way into it moves the body faster than the swing itself does, and over before its strike. 0: the usual short blend.")]
    [SerializeField, Min(0f)] private float blendIn;
    [Tooltip("The same from the ready pose, the guard, the exploring or walking pose: the swing that starts a fight or a chain. " +
             "Measured apart (the review, 2026-11-20: the worst way in, often a chain's wrap, slowed the commonest).")]
    [SerializeField, Min(0f)] private float blendInFromReady;

    [Header("Sound and Effects")]
    [Tooltip("Plays as the swing starts. Empty uses the weapon's, then the game-wide swing sound.")]
    [SerializeField] private SoundDefinition swingSound;
    [Tooltip("Plays where it hits. Empty uses the weapon's, then the game-wide hit sound.")]
    [SerializeField] private SoundDefinition hitSound;
    [Tooltip("The slash arc drawn along the swing. Clear (alpha 0) draws none.")]
    [SerializeField] private Color slashColor = new Color(1f, 0.95f, 0.85f, 0.8f);
    [Tooltip("Which way the slash arc sweeps, as seen from behind the attacker: the way the weapon moves in the " +
             "animation. Set from the clip by Set Up Combat Animations.")]
    [SerializeField] private SlashSweep slashSweep = SlashSweep.RightToLeft;
    [Tooltip("Roll of the slash arc in degrees: 0 is a flat sweep, towards ±80 a steep chop; positive raises the right " +
             "end. Set from the clip by Set Up Combat Animations.")]
    [SerializeField, Range(-80f, 80f)] private float slashTilt;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public DamageType DamageType => damageType;
    public StrikeKind Strike => damageType.IsPhysical() ? strike : StrikeKind.None;
    public float BaseDamage => baseDamage;
    public float WeaponDamageRatio => weaponDamageRatio;
    public float PowerScaling => powerScaling;
    public float StructureDamageMultiplier => structureDamageMultiplier;
    public bool CanCrit => canCrit;
    public float Range => range;
    public float Arc => arc;
    public int MaxTargets => maxTargets;
    public float HitDelay => hitDelay;
    public float RecoveryTime => recoveryTime;
    public float PoiseDamage => poiseDamage;
    public float Impact => impact;
    public float Lunge => lunge;
    public float Hop => hop;
    /// <summary>Its own stamina cost, or null for the player's normal attack cost (PlayerCombat.StaminaCostOf resolves it). A placeholder
    /// is never handed out as a number (Docs/BugPatterns.md, guard 4: "-1, the default" was read as a cost of -1).</summary>
    public float? OwnStaminaCost => staminaCost >= 0f ? staminaCost : (float?)null;
    public StatusEffectDefinition AppliesStatus => appliesStatus;
    public float StatusChance => statusChance;
    /// <summary>Only a dodge gets out of it: no block or parry stops it.</summary>
    public bool Unblockable => unblockable;
    /// <summary>Metres a hit drags its target toward the attacker (negative: away).</summary>
    public float Pull => pull;
    /// <summary>What the blow does to the ground round it (GroundImpacts).</summary>
    public GroundImpactDefinition GroundImpact => groundImpact;
    /// <summary>Movement (player): its own value, or null for the settings' (SwingMotion resolves each).</summary>
    public float? OwnWindupMove => windupMove >= 0f ? windupMove : (float?)null;
    public float? OwnAfterContactMove => afterContactMove >= 0f ? afterContactMove : (float?)null;
    public float? OwnWindupTurnRate => windupTurnRate >= 0f ? windupTurnRate : (float?)null;
    public float? OwnMoveAt => moveAt >= 0f ? moveAt : (float?)null;
    public float? OwnLungeShare => lungeShare >= 0f ? lungeShare : (float?)null;
    public ArtMotion Motion => motion;
    public float MotionDistance => motionDistance;
    public bool AlwaysCritical => alwaysCritical;
    public GameObject CastProjectile => castProjectile;
    public int CastCount => castCount;
    public float CastSpread => castSpread;
    public float CastSpeed => castSpeed;
    public float CastRadius => castRadius;
    public float CastRange => castRange;
    public GameObject CastHazard => castHazard;
    public string AnimationState => animationState;
    public float AnimationSpeed => animationSpeed;
    public float StrikeSpeed => strikeSpeed;
    public float StrikeLead => strikeLead;
    public float FollowThroughSpeed => followThroughSpeed;
    public bool GrowsFromLight => growsFromLight;
    public float BlendIn => blendIn;
    public float BlendInFromReady => blendInFromReady;
    public SoundDefinition SwingSound => swingSound;
    public SoundDefinition HitSound => hitSound;
    public Color SlashColor => slashColor;
    public SlashSweep SlashSweep => slashSweep;
    public float SlashTilt => slashTilt;

    /// <summary>Sets the player-facing extras in one go (editor setup and tests).</summary>
    public void ConfigureTechnique(string state, float speed, float poise, float lungeDistance, Color slash)
    {
        animationState = state;
        animationSpeed = speed;
        poiseDamage = poise;
        lunge = lungeDistance;
        slashColor = slash;
    }

    /// <summary>Sets how the player moves through the swing (editor setup and tests); below 0 leaves a value to the settings.</summary>
    public void ConfigureMovement(float windup, float afterContact, float turnRate = -1f, float moveAfter = -1f, float lungeLeadShare = -1f)
    {
        windupMove = windup;
        afterContactMove = afterContact;
        windupTurnRate = turnRate;
        moveAt = moveAfter;
        lungeShare = lungeLeadShare;
    }

    /// <summary>Sets how the wind-up plays (editor setup and tests).</summary>
    public void ConfigureStrike(float buildUpSpeed, float speed, float lead)
    {
        animationSpeed = buildUpSpeed;
        strikeSpeed = speed;
        strikeLead = lead;
    }

    /// <summary>Sets the damage, reach and timing (editor setup and tests).</summary>
    public void ConfigureHit(float weaponRatio, float reach, float width, int targets, float landsAfter, float recoversAfter)
    {
        weaponDamageRatio = weaponRatio;
        range = reach;
        arc = width;
        maxTargets = targets;
        hitDelay = landsAfter;
        recoveryTime = Mathf.Max(recoversAfter, landsAfter);
    }

    private void OnValidate()
    {
        // The attack can't be ready to chain before the damage has landed.
        recoveryTime = Mathf.Max(recoveryTime, hitDelay);
    }
}

/// <summary>How a weapon art moves the player (AttackDefinition › How a weapon art moves).</summary>
public enum ArtMotion
{
    None,
    DashThrough,
    BlinkBehind,
    LeapTo,
    Throw,
}

/// <summary>Which way a slash arc sweeps, as seen from behind the attacker.</summary>
public enum SlashSweep
{
    LeftToRight,
    RightToLeft,
}
