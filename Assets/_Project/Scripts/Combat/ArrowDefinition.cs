using UnityEngine;

/// <summary>
/// What an arrow does once loosed: its damage (before the shooter's power and the draw), how it changes the bow's speed,
/// how strongly it drops, what flies, whether it can be picked up again and whether it sets creatures burning. An arrow
/// item points here (ItemDefinition › Arrow); a bow shoots the chosen arrows (Z cycles), else the first in the bag.
/// Nothing here changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "Arrow", menuName = "Safe Haven/Combat/Arrow")]
public sealed class ArrowDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("Damage of a fully drawn hit, before the shooter's power and crit and the target's armor.")]
    [SerializeField, Min(0f)] private float damage = 12f;
    [Tooltip("Multiplies the bow's arrow speed: heavy heads fly slower.")]
    [SerializeField, Range(0.25f, 2f)] private float speedMultiplier = 1f;
    [Tooltip("Share of normal gravity the arrow falls with. Lower flies flatter.")]
    [SerializeField, Range(0f, 2f)] private float gravityScale = 0.6f;
    [Tooltip("What flies and sticks where it lands (an ArrowProjectile). Its +Z points along the shaft, tip first.")]
    [SerializeField] private ArrowProjectile projectile;
    [Tooltip("Played where the arrow strikes.")]
    [SerializeField] private SoundDefinition hitSound;
    [Tooltip("How far the strike is heard by monsters, in metres. A shot into the brush lures them away from the archer.")]
    [SerializeField, Min(0f)] private float impactNoise = 6f;
    [Tooltip("Chance an arrow stuck in the ground or a tree can be picked up again (walk over it).")]
    [SerializeField, Range(0f, 1f)] private float recoverChance = 0.5f;

    [Header("Fire")]
    [Tooltip("Fire damage per second a hit creature burns for. 0: the arrow doesn't set things alight.")]
    [SerializeField, Min(0f)] private float burnDamagePerSecond;
    [Tooltip("Seconds a hit creature burns.")]
    [SerializeField, Min(0f)] private float burnSeconds = 4f;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float Damage => damage;
    public float SpeedMultiplier => speedMultiplier;
    public float GravityScale => gravityScale;
    public ArrowProjectile Projectile => projectile;
    public SoundDefinition HitSound => hitSound;
    public float ImpactNoise => impactNoise;
    public float RecoverChance => recoverChance;
    public float BurnDamagePerSecond => burnDamagePerSecond;
    public float BurnSeconds => burnSeconds;
    public bool Burns => burnDamagePerSecond > 0f && burnSeconds > 0f;
}
