using UnityEngine;

/// <summary>
/// The global numbers behind damage calculation, kept in one asset so the whole game can be rebalanced without
/// touching code. Combatants without a rules asset use the defaults below.
/// </summary>
[CreateAssetMenu(fileName = "CombatRules", menuName = "Safe Haven/Combat/Combat Rules")]
public sealed class CombatRules : ScriptableObject
{
    private static CombatRules fallback;

    [Tooltip("Damage added per point of power, as a fraction. 0.01 means each point of power is +1% damage.")]
    [SerializeField, Min(0f)] private float damagePerPower = 0.01f;
    [Tooltip("Physical power each point of Strength gives. 1 means Strength counts the same as physical power.")]
    [SerializeField, Min(0f)] private float powerPerStrength = 1f;
    [Tooltip("The armor value that halves physical damage. Armor has diminishing returns: armor / (armor + this).")]
    [SerializeField, Min(1f)] private float armorForHalfReduction = 100f;
    [Tooltip("The most damage resistance can remove. 0.9 means resistances stop at 90%.")]
    [SerializeField, Range(0f, 1f)] private float maxResistance = 0.9f;
    [Tooltip("How negative a resistance can go. -1 means a weakness can double the damage taken.")]
    [SerializeField, Range(-5f, 0f)] private float minResistance = -1f;
    [Tooltip("A hit that does damage never does less than this, so heavy armor can't make something immune.")]
    [SerializeField, Min(0f)] private float minimumDamage = 1f;

    /// <summary>Used by combatants that have no rules asset assigned.</summary>
    public static CombatRules Default
    {
        get
        {
            if (fallback == null)
            {
                fallback = CreateInstance<CombatRules>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return fallback;
        }
    }

    public float DamagePerPower => damagePerPower;
    public float PowerPerStrength => powerPerStrength;

    /// <summary>The power a hit of this type gets: physical power plus Strength for physical hits, spell power otherwise.</summary>
    public float PowerFor(StatSheet stats, DamageType type)
    {
        float power = stats.Get(type.PowerStat());
        return type.IsPhysical() ? power + stats.Get(StatType.Strength) * powerPerStrength : power;
    }
    public float ArmorForHalfReduction => armorForHalfReduction;
    public float MinimumDamage => minimumDamage;

    /// <summary>The share of physical damage removed by an armor value, from 0 up to (but never reaching) 1.</summary>
    public float GetArmorReduction(float armor)
    {
        return armor <= 0f ? 0f : armor / (armor + armorForHalfReduction);
    }

    public float ClampResistance(float resistance)
    {
        return Mathf.Clamp(resistance, minResistance, maxResistance);
    }
}
