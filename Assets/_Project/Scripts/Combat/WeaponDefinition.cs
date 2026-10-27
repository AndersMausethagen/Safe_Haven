using System.Collections.Generic;
using UnityEngine;

public enum WeaponKind { Melee = 0, Torch = 1 }

/// <summary>
/// A weapon the player can hold. It sets the animation stance, how much damage it carries and its attacks: the combo
/// chain of light swings the attack button walks through (pressed in time; see <see cref="ComboRules"/>), the heavy
/// attack a held press performs, and how well it guards. The player's power and crit stats then scale the attack; the
/// weapon itself only holds its own numbers.
/// </summary>
[CreateAssetMenu(fileName = "Weapon", menuName = "Safe Haven/Combat/Weapon")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("Selects the combat idle, attack animation and arm pose.")]
    [SerializeField] private WeaponStance stance = WeaponStance.OneHanded;
    [SerializeField] private WeaponKind kind;
    [Tooltip("The weapon's base damage, before the player's power and the attack's ratio.")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [Tooltip("The first swing, and the only one when the combo chain is empty.")]
    [SerializeField] private AttackDefinition basicAttack;

    [Header("Combo and heavy attack")]
    [Tooltip("Light swings in order. Pressing attack in time after one moves to the next; the last is the finisher.")]
    [SerializeField] private List<AttackDefinition> combo = new List<AttackDefinition>();
    [Tooltip("Hold the attack button: a slow, hard swing that breaks poise. Empty: holding does a light swing.")]
    [SerializeField] private AttackDefinition heavyAttack;
    [Tooltip("The swing a ready counter (after a parry or perfect dodge) performs. Empty: the next chain swing, as a counter.")]
    [SerializeField] private AttackDefinition counterAttack;

    [Header("Guard")]
    [Tooltip("Share of a blocked hit that still gets through with this weapon (a shield blocks better).")]
    [SerializeField, Range(0f, 1f)] private float guardLetThrough = 0.35f;

    [Header("Sound")]
    [SerializeField] private SoundDefinition swingSound;
    [SerializeField] private SoundDefinition hitSound;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public WeaponStance Stance => stance;
    public WeaponKind Kind => kind;
    public bool RequiresBothHands => stance == WeaponStance.TwoHanded || stance == WeaponStance.Polearm;
    public bool CanUseOffHand => kind == WeaponKind.Torch && !RequiresBothHands;
    public float Damage => damage;
    public AttackDefinition BasicAttack => basicAttack;
    public AttackDefinition HeavyAttack => heavyAttack;
    public AttackDefinition CounterAttack => counterAttack;
    public float GuardLetThrough => guardLetThrough;
    public SoundDefinition SwingSound => swingSound;
    public SoundDefinition HitSound => hitSound;

    /// <summary>Number of steps in the light chain (at least 1 when there is a basic attack).</summary>
    public int ComboLength => combo.Count > 0 ? combo.Count : basicAttack != null ? 1 : 0;

    /// <summary>The light swing at chain step <paramref name="step"/> (the basic attack when there is no chain).</summary>
    public AttackDefinition ComboStep(int step)
    {
        if (combo.Count == 0)
        {
            return basicAttack;
        }

        AttackDefinition attack = combo[Mathf.Clamp(step, 0, combo.Count - 1)];
        return attack != null ? attack : basicAttack;
    }

    public IReadOnlyList<AttackDefinition> Combo => combo;

    /// <summary>Sets the chain and heavy attack (editor setup and tests).</summary>
    public void ConfigureAttacks(List<AttackDefinition> chain, AttackDefinition heavy, AttackDefinition counter)
    {
        combo = chain;
        heavyAttack = heavy;
        counterAttack = counter;
        if (basicAttack == null && chain.Count > 0)
        {
            basicAttack = chain[0];
        }
    }
}
