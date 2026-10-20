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
    [Tooltip("The most targets one hit can reach, nearest first. 0 means no limit.")]
    [SerializeField, Min(0)] private int maxTargets = 3;

    [Header("Timing")]
    [Tooltip("Seconds after the attack starts when the damage lands. Match it to the swing of the animation.")]
    [SerializeField, Min(0f)] private float hitDelay = 0.3f;
    [Tooltip("Seconds after the attack starts before the next one can begin. Match it to where the animation can chain.")]
    [SerializeField, Min(0f)] private float recoveryTime = 0.6f;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public DamageType DamageType => damageType;
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

    private void OnValidate()
    {
        // The attack can't be ready to chain before the damage has landed.
        recoveryTime = Mathf.Max(recoveryTime, hitDelay);
    }
}
