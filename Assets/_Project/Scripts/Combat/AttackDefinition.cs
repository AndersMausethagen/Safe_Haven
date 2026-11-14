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
    [Tooltip("Stamina the player pays for it. Below 0 uses the player's normal attack cost.")]
    [SerializeField] private float staminaCost = -1f;

    [Header("Status")]
    [Tooltip("Left behind by a hit that gets through (not blocked or parried): poison, chill, roots. Empty: none.")]
    [SerializeField] private StatusEffectDefinition appliesStatus;
    [Tooltip("Chance from 0 to 1 that a hit leaves the status.")]
    [SerializeField, Range(0f, 1f)] private float statusChance = 1f;

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
    public float StaminaCost => staminaCost;
    public StatusEffectDefinition AppliesStatus => appliesStatus;
    public float StatusChance => statusChance;
    public string AnimationState => animationState;
    public float AnimationSpeed => animationSpeed;
    public float StrikeSpeed => strikeSpeed;
    public float StrikeLead => strikeLead;
    public float FollowThroughSpeed => followThroughSpeed;
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

/// <summary>Which way a slash arc sweeps, as seen from behind the attacker.</summary>
public enum SlashSweep
{
    LeftToRight,
    RightToLeft,
}
