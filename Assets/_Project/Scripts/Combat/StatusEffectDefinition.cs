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
    [Tooltip("Share of damage its bearer takes while it lasts, after armour: 1 is normal, 1.15 is 15% more (Sundered: armour broken).")]
    [SerializeField, Range(1f, 2f)] private float damageTaken = 1f;

    [Header("Getting free (Docs/HeartrootPlaytest_2026-11-23.md P5)")]
    [Tooltip("A new hit while it is at work restarts its time. Off: it runs out on its first time, so a volley's later blows (Root " +
             "Eruption's three rings) can't hold the player longer than the first did.")]
    [SerializeField] private bool refreshes = true;
    [Tooltip("A roll tears the player free of it at once (Roots).")]
    [SerializeField] private bool endsOnDodge;
    [Tooltip("After it ends (run out, torn free or cured), it can't take hold again for this long, in seconds: the next ring of a volley " +
             "can't root the player who has just rolled clear. 0: at once.")]
    [SerializeField, Min(0f)] private float restSeconds;
    [Tooltip("What the player is told when it takes hold, so they know the way out (\"Rooted! Roll to tear free.\"). Empty: its name.")]
    [SerializeField] private string takesHoldLine;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Color Color => color;
    public Sprite Icon => icon;
    public float Seconds => seconds;
    public float DamagePerSecond => damagePerSecond;
    public DamageType DamageType => damageType;
    public float MoveSpeed => moveSpeed;
    public float DamageTaken => damageTaken;
    public bool Refreshes => refreshes;
    public bool EndsOnDodge => endsOnDodge;
    public float RestSeconds => restSeconds;
    public string TakesHoldLine => string.IsNullOrWhiteSpace(takesHoldLine) ? $"{DisplayName}!" : takesHoldLine;
}
