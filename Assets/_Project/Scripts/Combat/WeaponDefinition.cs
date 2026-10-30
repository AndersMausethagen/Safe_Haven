using System.Collections.Generic;
using UnityEngine;

public enum WeaponKind { Melee = 0, Torch = 1, Bow = 2, FishingRod = 3 }

/// <summary>
/// A weapon the player can hold. It sets the animation stance, how much damage it carries and its attacks: the combo
/// chain of light swings the attack button walks through (pressed in time; see <see cref="ComboRules"/>), the heavy
/// attack a held press performs, and how well it guards. The player's power and crit stats then scale the attack; the
/// weapon itself only holds its own numbers.
/// <para>
/// A bow (Kind Bow) uses none of the swings: holding attack draws it (Draw Seconds to full), letting go looses an arrow
/// from the bag, and the next one is nocked in Reload Seconds (see PlayerArchery). The arrow sets the damage
/// (ArrowDefinition); the bow sets how fast it flies, how quickly it is drawn and reloaded, and how steady it is.
/// Letting go just as the draw is full is a perfect release; holding guard aims (steadier, slower).
/// </para>
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

    [Header("Bow")]
    [Tooltip("Seconds from nocking to full draw. A shot loosed earlier flies slower, hits softer and strays more.")]
    [SerializeField, Min(0.1f)] private float drawSeconds = 1f;
    [Tooltip("Seconds to nock the next arrow after a shot.")]
    [SerializeField, Min(0f)] private float reloadSeconds = 0.6f;
    [Tooltip("How fast an arrow leaves the bow at full draw, in metres per second (the arrow can change it).")]
    [SerializeField, Min(1f)] private float arrowSpeed = 32f;
    [Tooltip("How far a fully drawn shot can stray from the aim, in degrees. Half-drawn shots stray up to three times more.")]
    [SerializeField, Range(0f, 15f)] private float spread = 1.5f;
    [Tooltip("Share of a full draw needed before letting go shoots; earlier, the arrow stays on the string.")]
    [SerializeField, Range(0f, 1f)] private float minimumDraw = 0.25f;
    [Tooltip("Movement speed while drawing, as a share of normal.")]
    [SerializeField, Range(0f, 1f)] private float drawMoveSpeed = 0.55f;
    [Tooltip("Seconds after reaching full draw in which letting go is a perfect release: a critical hit.")]
    [SerializeField, Range(0f, 1f)] private float perfectReleaseSeconds = 0.2f;
    [Tooltip("Damage multiplier of a perfect release, on top of the critical hit.")]
    [SerializeField, Range(1f, 3f)] private float perfectReleaseDamage = 1.25f;
    [Tooltip("Spread while aiming (holding guard) as a share of normal.")]
    [SerializeField, Range(0.1f, 1f)] private float aimSpread = 0.5f;
    [Tooltip("Share of a normal shot's damage the instant shot after a perfect dodge deals.")]
    [SerializeField, Range(0f, 2f)] private float counterShotDamage = 0.6f;
    [Tooltip("Damage type, power scaling, crit and poise of a shot. Its damage comes from the arrow.")]
    [SerializeField] private AttackDefinition shotAttack;
    [SerializeField] private SoundDefinition drawSound;

    [Header("Sound and Effects")]
    [SerializeField] private SoundDefinition swingSound;
    [SerializeField] private SoundDefinition hitSound;
    [Tooltip("Spawned where this weapon's ordinary hits land (fists: a puff, not a blade's sparks). Strong hits keep " +
             "Combat Feel's critical effect. Empty uses Combat Feel's hit effect.")]
    [SerializeField] private GameObject hitEffect;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public WeaponStance Stance => stance;
    public WeaponKind Kind => kind;
    public bool IsBow => kind == WeaponKind.Bow;
    public bool IsFishingRod => kind == WeaponKind.FishingRod;
    /// <summary>The attack and guard buttons belong to the weapon's own use (PlayerArchery, PlayerFishing), not to swings.</summary>
    public bool TakesAttackInput => kind == WeaponKind.Bow || kind == WeaponKind.FishingRod;
    public float DrawSeconds => Mathf.Max(0.1f, drawSeconds);
    public float ReloadSeconds => Mathf.Max(0f, reloadSeconds);
    public float ArrowSpeed => Mathf.Max(1f, arrowSpeed);
    public float Spread => spread;
    public float MinimumDraw => minimumDraw;
    public float DrawMoveSpeed => drawMoveSpeed;
    public float PerfectReleaseSeconds => perfectReleaseSeconds;
    public float PerfectReleaseDamage => perfectReleaseDamage;
    public float AimSpread => aimSpread;
    public float CounterShotDamage => counterShotDamage;
    public AttackDefinition ShotAttack => shotAttack;
    public SoundDefinition DrawSound => drawSound;
    public bool RequiresBothHands => stance == WeaponStance.TwoHanded || stance == WeaponStance.Polearm || stance == WeaponStance.Bow;
    public bool CanUseOffHand => kind == WeaponKind.Torch && !RequiresBothHands;
    public float Damage => damage;
    public AttackDefinition BasicAttack => basicAttack;
    public AttackDefinition HeavyAttack => heavyAttack;
    public AttackDefinition CounterAttack => counterAttack;
    public float GuardLetThrough => guardLetThrough;
    public SoundDefinition SwingSound => swingSound;
    public SoundDefinition HitSound => hitSound;
    public GameObject HitEffect => hitEffect;

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
