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
    [Tooltip("A looping effect shown on whatever bears it while it lasts (StatusBearerLook): flames, rime, a bleed. Empty: nothing shows.")]
    [SerializeField] private GameObject look;

    [Header("Effect")]
    [Tooltip("Seconds it lasts. A new hit restarts the time; effects don't stack.")]
    [SerializeField, Min(0.1f)] private float seconds = 6f;
    [Tooltip("Damage each second. Armor and resistances of this Damage Type reduce it. 0: none.")]
    [SerializeField, Min(0f)] private float damagePerSecond;
    [SerializeField] private DamageType damageType = DamageType.Physical;
    [Tooltip("Share of normal speed its bearer (the player or a creature) moves at while it lasts: 1 is unhindered, 0 holds it in place.")]
    [SerializeField, Range(0f, 1f)] private float moveSpeed = 1f;
    [Tooltip("Share of damage its bearer takes while it lasts, after armour: 1 is normal, 1.15 is 15% more (Sundered: armour broken).")]
    [SerializeField, Range(1f, 2f)] private float damageTaken = 1f;
    [Tooltip("A creature under it strikes no blow while it lasts (Frozen): the strike under way is broken off, as a recoil does.")]
    [SerializeField] private bool stopsStrikes;
    [Tooltip("Share of its bearer's own damage its blows deal while it lasts: 1 is full, 0.8 is a fifth less (Cowed by a war cry).")]
    [SerializeField, Range(0.3f, 1f)] private float damageDealt = 1f;

    [Header("Consumed by a blow (Docs/CombatSkillsResearch_2026-11-24.md 3.2: Shatter)")]
    [Tooltip("Blows of these kinds end it at once and burst it: a heavy, a finisher or a weapon art on a frozen creature shatters it. None: nothing does.")]
    [SerializeField] private HitTechnique consumedBy;
    [Tooltip("The burst's damage, as a share of the blow that set it off, of this status's Damage Type.")]
    [SerializeField, Min(0f)] private float consumeDamage;
    [Tooltip("What shows where it bursts (StatusBearerLook), once.")]
    [SerializeField] private GameObject consumeLook;
    [Tooltip("Heard where it bursts (ice shattering). Empty: nothing.")]
    [SerializeField] private SoundDefinition consumeSound;
    [Tooltip("What the player is told when it bursts (\"Shattered!\"). Empty: nothing.")]
    [SerializeField] private string consumeLine;

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
    public bool StopsStrikes => stopsStrikes;
    public float DamageDealt => damageDealt;
    public HitTechnique ConsumedBy => consumedBy;
    public float ConsumeDamage => consumeDamage;
    public GameObject ConsumeLook => consumeLook;
    public SoundDefinition ConsumeSound => consumeSound;
    public string ConsumeLine => consumeLine;
    public GameObject Look => look;
    public bool Refreshes => refreshes;
    public bool EndsOnDodge => endsOnDodge;
    public float RestSeconds => restSeconds;
    public string TakesHoldLine => string.IsNullOrWhiteSpace(takesHoldLine) ? $"{DisplayName}!" : takesHoldLine;
}
