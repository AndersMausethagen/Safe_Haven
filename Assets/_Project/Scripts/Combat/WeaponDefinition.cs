using UnityEngine;

/// <summary>
/// A weapon the player can hold. It sets the animation stance, how much damage it carries, and the basic attack the left
/// mouse button performs. Swapping the weapon changes all three. The player's power and crit stats then scale the
/// attack; the weapon itself only holds its own numbers.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Safe Haven/Combat/Weapon")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("Selects the combat idle, attack animation and arm pose.")]
    [SerializeField] private WeaponStance stance = WeaponStance.OneHanded;
    [Tooltip("The weapon's base damage, before the player's power and the attack's ratio.")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField] private AttackDefinition basicAttack;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public WeaponStance Stance => stance;
    public float Damage => damage;
    public AttackDefinition BasicAttack => basicAttack;
}
