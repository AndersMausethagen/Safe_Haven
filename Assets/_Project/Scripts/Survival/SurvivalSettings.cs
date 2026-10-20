using UnityEngine;

/// <summary>
/// Tuning for hunger and thirst (GDD §11). They drain all the time, slowly at home and faster in raids, and never kill:
/// running out only weakens stamina. Hunger at zero lowers maximum stamina and slows its regeneration; thirst at zero
/// slows regeneration while in combat. Read by PlayerNeeds.
/// </summary>
[CreateAssetMenu(fileName = "SurvivalSettings", menuName = "Safe Haven/Settings/Survival Settings")]
public sealed class SurvivalSettings : GameSettings<SurvivalSettings>
{
    [Header("Meters")]
    [SerializeField, Min(1f)] private float maxHunger = 100f;
    [SerializeField, Min(1f)] private float maxThirst = 100f;
    [Tooltip("Hunger a new game starts with, as a fraction of the maximum. Below full so eating is taught early.")]
    [SerializeField, Range(0f, 1f)] private float startingHunger = 0.7f;
    [Tooltip("Thirst a new game starts with, as a fraction of the maximum.")]
    [SerializeField, Range(0f, 1f)] private float startingThirst = 0.6f;

    [Header("Drain (points per minute, at home)")]
    [SerializeField, Min(0f)] private float hungerPerMinute = 2.5f;
    [SerializeField, Min(0f)] private float thirstPerMinute = 3.5f;
    [Tooltip("Drain is multiplied by this during raids.")]
    [SerializeField, Min(0f)] private float raidMultiplier = 2.5f;

    [Header("Warnings")]
    [Tooltip("The player is warned when a meter falls to this fraction.")]
    [SerializeField, Range(0f, 1f)] private float lowWarning = 0.25f;

    [Header("When starving (hunger at 0)")]
    [Tooltip("Maximum stamina is multiplied by this.")]
    [SerializeField, Range(0.1f, 1f)] private float starvingMaxStamina = 0.75f;
    [Tooltip("Stamina regeneration is multiplied by this.")]
    [SerializeField, Range(0f, 1f)] private float starvingRegeneration = 0.6f;

    [Header("When parched (thirst at 0)")]
    [Tooltip("Stamina regeneration in combat is multiplied by this.")]
    [SerializeField, Range(0f, 1f)] private float parchedCombatRegeneration = 0.5f;

    public float MaxHunger => maxHunger;
    public float MaxThirst => maxThirst;
    public float StartingHunger => startingHunger;
    public float StartingThirst => startingThirst;
    public float HungerPerMinute => hungerPerMinute;
    public float ThirstPerMinute => thirstPerMinute;
    public float RaidMultiplier => raidMultiplier;
    public float LowWarning => lowWarning;
    public float StarvingMaxStamina => starvingMaxStamina;
    public float StarvingRegeneration => starvingRegeneration;
    public float ParchedCombatRegeneration => parchedCombatRegeneration;

    /// <summary>Minutes a full meter lasts at the given drain, for the Systems Editor. Infinity when it doesn't drain.</summary>
    public static float MinutesToEmpty(float max, float perMinute, float multiplier = 1f)
    {
        float rate = perMinute * multiplier;
        return rate > 0f ? max / rate : float.PositiveInfinity;
    }
}
