using UnityEngine;

/// <summary>
/// A lasting harm a hit can leave behind (GDD §10.6): Poison that eats at health, Chill that slows, Roots that hold the
/// feet. It says how long it lasts, the damage it deals each second and how it slows the player. Attacks apply it
/// (AttackDefinition › Applies Status), StatusEffects carries it out and food or brews can cure it (ItemDefinition ›
/// Cures). Never changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "Status", menuName = "Safe Haven/Combat/Status Effect")]
public sealed class StatusEffectDefinition : ScriptableObject, IDisplayName
{
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [Tooltip("Shown on the HUD and in messages.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [Tooltip("Colour of its name on the HUD.")]
    [SerializeField] private Color color = Color.white;
    [Tooltip("Its icon beside the vitals (LocalSpawn: Tools/LocalSpawn/condition_icons.json). Empty: the name alone.")]
    [SerializeField] private Sprite icon;

    [Header("Effect")]
    [Tooltip("Seconds it lasts. A new hit restarts the time; effects don't stack.")]
    [SerializeField, Min(0.1f)] private float seconds = 6f;
    [Tooltip("Damage each second. Armor and resistances of this Damage Type reduce it. 0: none.")]
    [SerializeField, Min(0f)] private float damagePerSecond;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Share of normal speed the player moves at while it lasts: 1 is unhindered, 0 holds them in place.")]
    [SerializeField, Range(0f, 1f)] private float moveSpeed = 1f;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Color Color => color;
    public Sprite Icon => icon;
    public float Seconds => seconds;
    public float DamagePerSecond => damagePerSecond;
    public DamageType DamageType => damageType;
    public float MoveSpeed => moveSpeed;
}
