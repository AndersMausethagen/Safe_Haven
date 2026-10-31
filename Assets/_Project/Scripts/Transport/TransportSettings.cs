using UnityEngine;

/// <summary>
/// What all transport shares (GDD §8.5): how it reaches the player and how much noise it makes. Each kind's capacity and
/// speed are on its TransportDefinition. Lives at Resources/TransportSettings, edited on the Transport page of the Systems
/// Editor.
/// </summary>
[CreateAssetMenu(fileName = "TransportSettings", menuName = "Safe Haven/Settings/Transport Settings")]
public sealed class TransportSettings : GameSettings<TransportSettings>
{
    [Tooltip("The slot the cargo window is made of.")]
    [SerializeField] private ItemSlotView slotPrefab;
    [Tooltip("A cart this close to the player when they extract comes home with them, attached or not. Metres.")]
    [SerializeField, Min(0f)] private float extractionRange = 12f;
    [Tooltip("At home, what is in a cart or boat this close to the player can be built and crafted with. Metres.")]
    [SerializeField, Min(0f)] private float materialReach = 12f;
    [Tooltip("At home, a cart this close to a chest can send its cargo straight to storage. Metres.")]
    [SerializeField, Min(0f)] private float storageReach = 8f;
    [Tooltip("Metres monsters hear a rolling cart from, empty.")]
    [SerializeField, Min(0f)] private float rumbleRadius = 5f;
    [Tooltip("More metres when full. A loaded cart is a loud cart.")]
    [SerializeField, Min(0f)] private float rumbleWhenFull = 8f;
    [Tooltip("Seconds between rumbles while it rolls.")]
    [SerializeField, Min(0.1f)] private float rumbleSeconds = 1.2f;
    [Tooltip("The share of normal speed left while the cart is at the end of the player's reach, stuck or too heavy for the hill.")]
    [SerializeField, Range(0f, 1f)] private float strainedSpeed = 0.2f;
    [Tooltip("Seconds stuck at full strain before the player lets go.")]
    [SerializeField, Min(0f)] private float stuckSeconds = 1.5f;

    public ItemSlotView SlotPrefab => slotPrefab;
    public float ExtractionRange => extractionRange;
    public float MaterialReach => materialReach;
    public float StorageReach => storageReach;
    public float RumbleRadius => rumbleRadius;
    public float RumbleWhenFull => rumbleWhenFull;
    public float RumbleSeconds => rumbleSeconds;
    public float StrainedSpeed => strainedSpeed;
    public float StuckSeconds => stuckSeconds;
}
