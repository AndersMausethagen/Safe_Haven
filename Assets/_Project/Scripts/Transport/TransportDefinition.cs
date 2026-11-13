using UnityEngine;

/// <summary>
/// One kind of transport (GDD §8.5, §12.4): a wheelbarrow, a handcart, a rowboat. What it holds, how its load slows whoever
/// moves it and how heavy it is to the physics. Cargo and where it stands are runtime state on the Transport in the world.
/// The speed is three points on a line: empty, at its rated capacity and at the most it can be piled with. Past the rated
/// capacity it is overfilled, and the penalty is steep on purpose.
/// </summary>
[CreateAssetMenu(fileName = "NewTransport", menuName = "Safe Haven/Transport/Transport")]
public sealed class TransportDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 3)] private string description;
    [Tooltip("The Transport in the world, with a SaveableEntity so it stays where it was left.")]
    [SerializeField] private SaveableEntity prefab;
    [Tooltip("The item it packs into. Using the item sets it down.")]
    [SerializeField] private ItemDefinition kit;
    [Tooltip("The painting across the top of its cargo window: a name in Resources/WindowArt (LocalSpawn: " +
             "Tools/LocalSpawn/window_art.json). Empty: none.")]
    [SerializeField] private string windowArt;

    [Header("Cargo")]
    [SerializeField, Min(1)] private int columns = 6;
    [SerializeField, Min(1)] private int rows = 4;
    [Tooltip("Weight it carries as designed. Above this it is overfilled.")]
    [SerializeField, Min(1f)] private float capacity = 200f;
    [Tooltip("The most that can be piled on. Nothing more goes in.")]
    [SerializeField, Min(1f)] private float overloadLimit = 320f;

    [Header("Speed (share of normal)")]
    [SerializeField, Range(0.1f, 1f)] private float speedEmpty = 0.95f;
    [Tooltip("At its rated capacity.")]
    [SerializeField, Range(0.1f, 1f)] private float speedFull = 0.82f;
    [Tooltip("At the overload limit.")]
    [SerializeField, Range(0.1f, 1f)] private float speedOverfilled = 0.35f;

    [Header("Physics")]
    [SerializeField, Min(1f)] private float emptyMass = 60f;
    [Tooltip("Kilograms for each point of cargo weight.")]
    [SerializeField, Min(0f)] private float massPerWeight = 1.5f;
    [Tooltip("Newtons the player can pull with. A heavy load on a steep hill can be more than this.")]
    [SerializeField, Min(1f)] private float pullForce = 1800f;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public SaveableEntity Prefab => prefab;
    public ItemDefinition Kit => kit;
    public string WindowArt => windowArt;
    public int Columns => columns;
    public int Rows => rows;
    public float Capacity => capacity;
    public float OverloadLimit => Mathf.Max(capacity, overloadLimit);
    public float EmptyMass => emptyMass;
    public float MassPerWeight => massPerWeight;
    public float PullForce => pullForce;

    public bool IsOverfilled(float weight) => weight > capacity + 0.001f;

    /// <summary>The share of normal speed with this much cargo.</summary>
    public float SpeedScale(float weight)
    {
        return IsOverfilled(weight)
            ? Mathf.Lerp(speedFull, speedOverfilled, Mathf.InverseLerp(capacity, OverloadLimit, weight))
            : Mathf.Lerp(speedEmpty, speedFull, Mathf.Clamp01(weight / capacity));
    }
}
