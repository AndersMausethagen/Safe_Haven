using UnityEngine;

/// <summary>Authored stamina tuning for the player: pool size, regeneration and what each action costs.</summary>
[CreateAssetMenu(fileName = "PlayerStaminaSettings", menuName = "Safe Haven/Player Stamina Settings")]
public sealed class PlayerStaminaSettings : ScriptableObject
{
    [Tooltip("Stamina when full.")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [Tooltip("Stamina regained per second once regeneration starts.")]
    [SerializeField, Min(0f)] private float regenPerSecond = 28f;
    [Tooltip("Seconds after spending stamina before it starts to regenerate.")]
    [SerializeField, Min(0f)] private float regenDelay = 0.8f;

    [Header("Costs")]
    [SerializeField, Min(0f)] private float attackCost = 15f;
    [SerializeField, Min(0f)] private float shieldBashCost = 20f;
    [SerializeField, Min(0f)] private float dodgeCost = 25f;

    public float MaxStamina => maxStamina;
    public float RegenPerSecond => regenPerSecond;
    public float RegenDelay => regenDelay;
    public float AttackCost => attackCost;
    public float ShieldBashCost => shieldBashCost;
    public float DodgeCost => dodgeCost;
}
