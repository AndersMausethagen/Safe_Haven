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
    [Tooltip("Stamina per second while swimming forward (treading water costs nothing). Out of stamina, the player " +
             "swims slower (PlayerMovementSettings › Exhausted Swim Speed).")]
    [SerializeField, Min(0f)] private float swimCostPerSecond = 7f;
    [Tooltip("Every stamina cost, times this: swings, blocks, dodges, abilities, swimming, hauling. 0.85 = 15% cheaper. " +
             "One dial for the whole game's stamina feel, on top of each action's own cost.")]
    [SerializeField, Range(0.1f, 2f)] private float actionCostScale = 1f;

    [Header("Rewards")]
    [Tooltip("Share of the base stamina pool (Max Stamina) given back at once by a parry, so timing a guard pays for itself.")]
    [SerializeField, Range(0f, 1f)] private float parryReturn = 0f;

    public float MaxStamina => maxStamina;
    public float ActionCostScale => actionCostScale;
    public float ParryReturn => parryReturn;
    public float RegenPerSecond => regenPerSecond;
    public float RegenDelay => regenDelay;
    public float AttackCost => attackCost;
    public float ShieldBashCost => shieldBashCost;
    public float DodgeCost => dodgeCost;
    public float SwimCostPerSecond => swimCostPerSecond;
}
