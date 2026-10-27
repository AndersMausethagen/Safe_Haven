using UnityEngine;

/// <summary>
/// What an arrow does once loosed: its damage (before the shooter's power and the draw), how it changes the bow's speed,
/// how strongly it drops, and what flies. An arrow item points here (ItemDefinition › Arrow); a bow shoots the first
/// arrow stack in the bag. Nothing here changes while playing.
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

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float Damage => damage;
    public float SpeedMultiplier => speedMultiplier;
    public float GravityScale => gravityScale;
    public ArrowProjectile Projectile => projectile;
    public SoundDefinition HitSound => hitSound;
    public float ImpactNoise => impactNoise;
}
