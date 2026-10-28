using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored data for a kind of monster: identity, combat stats, faction, loot and how it behaves in a fight. Monsters
/// drop items, not raw resources, and a boss can teach the recipes for the next tier. Every instance reads this; runtime
/// values such as current health and current target live on its components.
/// </summary>
[CreateAssetMenu(fileName = "NewMonster", menuName = "Safe Haven/Monsters/Monster Definition")]
public sealed class MonsterDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;

    [Header("World")]
    [Tooltip("Health, power, armor and resistances.")]
    [SerializeField] private CombatStatsDefinition combatStats;
    [Tooltip("Enemy monsters attack the Player faction, Player monsters attack Enemies, Neutral monsters never attack.")]
    [SerializeField] private Faction faction = Faction.Enemy;
    [Tooltip("Items the player receives for killing it: hides, bones, trophies.")]
    [SerializeField] private ItemDropTable drops = new ItemDropTable();
    [Tooltip("Recipes the player learns for killing it. Use this on bosses to unlock the next tier of crafting.")]
    [SerializeField] private List<RecipeDefinition> unlocksRecipes = new List<RecipeDefinition>();
    [Tooltip("Controller for the monster's Animator. It should have a Speed float, an Attack trigger and a Dead bool.")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    [Tooltip("Generated monster prefab placed in the world.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Experience for killing it. 0: worked out from its health (LevelingSettings).")]
    [SerializeField, Min(0)] private int experience;

    [Header("Behaviour")]
    [Tooltip("What the monster does when it attacks.")]
    [SerializeField] private AttackDefinition attack;
    [Tooltip("Damage the attack's Weapon Damage Ratio applies to, like a weapon's damage.")]
    [SerializeField, Min(0f)] private float attackDamage = 8f;
    [Tooltip("Extra seconds after an attack has recovered before the next one can start.")]
    [SerializeField, Min(0f)] private float attackCooldown = 0.6f;
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [Tooltip("How far it sees a plainly visible target in front of it (inside its Sight Angle, with nothing in between). " +
             "Sneaking targets are seen from closer, fighting ones from further (Systems Editor › Senses & Stealth).")]
    [SerializeField, Min(0f)] private float detectionRange = 10f;
    [Tooltip("The monster loses a target that gets further away than this, and goes to look where it last saw it.")]
    [SerializeField, Min(0f)] private float chaseRange = 16f;
    [Tooltip("The monster stops chasing and walks home when it gets this far from where it spawned. 0 disables it.")]
    [SerializeField, Min(0f)] private float leashRange = 25f;

    [Header("Senses")]
    [Tooltip("Width of its sight cone in degrees. 360 sees all around.")]
    [SerializeField, Range(0f, 360f)] private float sightAngle = 120f;
    [Tooltip("It notices anything this close from any direction, even behind it or through cover.")]
    [SerializeField, Min(0f)] private float closeSenseRange = 2.5f;
    [Tooltip("Multiplies how far it hears noise (footsteps, fights, gathering). 0 = deaf.")]
    [SerializeField, Min(0f)] private float hearing = 1f;
    [Tooltip("Multiplies how quickly a glimpse turns into spotting you. Above 1 = watchful, below 1 = dull.")]
    [SerializeField, Min(0f)] private float alertness = 1f;

    [Header("Composable combat")]
    [Tooltip("Empty keeps the original single attack above. Otherwise these actions replace it.")]
    [SerializeField] private List<MonsterAttack> attacks = new List<MonsterAttack>();
    [SerializeField] private MonsterMovementTactic movementTactic;
    [SerializeField] private MonsterTemperament temperament = MonsterTemperament.Aggressive;
    [SerializeField, Min(1f)] private float preferredDistance = 7f;
    [SerializeField, Min(0.2f)] private float distanceTolerance = 1.5f;
    [SerializeField, Range(0f, 1f)] private float fleeHealthFraction = 0.25f;
    [SerializeField, Min(1f)] private float fleeDistance = 10f;
    [SerializeField, Min(1f)] private float defensiveRadius = 8f;
    [Tooltip("Offsets the visual only, never the collider or navigation agent. Charge recovery lands the visual.")]
    [SerializeField, Min(0f)] private float hoverHeight;

    [Header("Poise")]
    [Tooltip("How much it takes before it staggers (attacks' Poise Damage wears it down). 0 uses the game-wide default; " +
             "a very high value makes it unstoppable.")]
    [SerializeField, Min(0f)] private float poise;

    [Header("Sound")]
    [Tooltip("Plays when it is killed. Empty uses the creature default from Audio Settings.")]
    [SerializeField] private SoundDefinition deathSound;
    [Tooltip("Plays when it is hurt (on top of the hit sound). Empty: none.")]
    [SerializeField] private SoundDefinition hurtSound;
    [Tooltip("Plays as it winds up an attack, so the player hears it coming. Empty: none.")]
    [SerializeField] private SoundDefinition attackSound;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public GameObject Prefab => prefab;
    public CombatStatsDefinition CombatStats => combatStats;
    public Faction Faction => faction;
    public ItemDropTable Drops => drops;
    public int Experience => experience;
    public IReadOnlyList<RecipeDefinition> UnlocksRecipes => unlocksRecipes;
    public RuntimeAnimatorController AnimatorController => animatorController;
    public AttackDefinition Attack => attack;
    public float AttackDamage => attackDamage;
    public float AttackCooldown => attackCooldown;
    public float MoveSpeed => moveSpeed;
    public float DetectionRange => detectionRange;
    public float ChaseRange => Mathf.Max(chaseRange, detectionRange);
    public float LeashRange => leashRange;
    public float SightAngle => sightAngle;
    public float CloseSenseRange => closeSenseRange;
    public float Hearing => hearing;
    public float Alertness => alertness;
    public SoundDefinition DeathSound => deathSound;
    public SoundDefinition HurtSound => hurtSound;
    public SoundDefinition AttackSound => attackSound;
    /// <summary>Its poise, or 0 when it uses the game-wide default.</summary>
    public float Poise => poise;
    public IReadOnlyList<MonsterAttack> Attacks => attacks;
    public MonsterMovementTactic MovementTactic => movementTactic;
    public MonsterTemperament Temperament => temperament;
    public float PreferredDistance => preferredDistance;
    public float DistanceTolerance => distanceTolerance;
    public float FleeHealthFraction => fleeHealthFraction;
    public float FleeDistance => fleeDistance;
    public float DefensiveRadius => defensiveRadius;
    public float HoverHeight => hoverHeight;
    public bool HasTemperament(MonsterTemperament value) => (temperament & value) != 0;

    private void OnValidate()
    {
        chaseRange = Mathf.Max(chaseRange, detectionRange);
        drops.Validate();
    }
}
