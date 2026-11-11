using UnityEngine;

/// <summary>
/// A kind of horse: its body, the ranges its stats are rolled from (so one wild horse is worth more taming than another),
/// how it behaves wild, and the taming: the lasso, the struggle against the rope and the breaking ride fed with barley.
/// Shared and read-only in play; each horse keeps its own rolled <see cref="HorseStats"/> and state on <see cref="Horse"/>.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Horses/Horse", fileName = "Horse")]
public sealed class HorseDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Wild Horse";
    [SerializeField, TextArea] private string description = "Fast, wary and strong. A rope and patience make it yours.";
    [Tooltip("The horse: Horse, HorseTaming, NavMeshAgent, Damageable, Combatant, Interactable and a SaveableEntity.")]
    [SerializeField] private Horse prefab;
    [Tooltip("Coats a horse can have (each tints the body's Main materials). Empty: the model's own.")]
    [SerializeField] private Color[] coats = System.Array.Empty<Color>();

    [Header("Stats (each horse rolls its own)")]
    [SerializeField] private Vector2 health = new Vector2(140f, 200f);
    [SerializeField] private Vector2 stamina = new Vector2(80f, 120f);
    [Tooltip("Metres a second at a gallop.")]
    [SerializeField] private Vector2 gallopSpeed = new Vector2(10f, 12.5f);
    [Tooltip("Kilograms its saddle bags may hold.")]
    [SerializeField] private Vector2 carryWeight = new Vector2(55f, 85f);

    [Header("Gaits")]
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.6f;
    [Tooltip("Ridden without a gallop.")]
    [SerializeField, Min(0.1f)] private float trotSpeed = 6.5f;
    [Tooltip("Stamina a second spent at a gallop; it comes back at Stamina Regen while it doesn't gallop.")]
    [SerializeField, Min(0f)] private float gallopCost = 9f;
    [SerializeField, Min(0f)] private float staminaRegen = 6f;
    [Tooltip("Degrees a second it turns while ridden.")]
    [SerializeField, Min(10f)] private float turnSpeed = 220f;

    [Header("Wild")]
    [Tooltip("How near a quiet player may come; noise adds to it (metres of startle per metre the player is heard). Walking (5 m footsteps) it bolts at about 10 m, sneaking at about 6 m, inside the lasso's reach.")]
    [SerializeField, Min(0f)] private float shyDistance = 4f;
    [SerializeField, Min(0f)] private float perHeardMetre = 1.3f;
    [SerializeField, Min(1f)] private float wanderRadius = 14f;
    [Tooltip("Seconds it runs from what frightened or hurt it.")]
    [SerializeField, Min(1f)] private float fleeSeconds = 7f;
    [SerializeField] private SoundDefinition whinny;

    [Header("Lasso and struggle")]
    [SerializeField] private ItemDefinition lasso;
    [Tooltip("Metres the lasso reaches.")]
    [SerializeField, Min(1f)] private float lassoRange = 9f;
    [Tooltip("The horse's struggle stamina: every tap the player holds on with takes Tap Drain from it, and it is broken at 0.")]
    [SerializeField, Min(1f)] private float struggleStamina = 100f;
    [SerializeField, Min(0.1f)] private float tapDrain = 3f;
    [Tooltip("Strain on the rope a second while the horse pulls (1 slips it); every tap takes Tap Hold from it.")]
    [SerializeField, Min(0.01f)] private float pullStrain = 0.35f;
    [SerializeField, Min(0.01f)] private float tapHold = 0.1f;
    [Tooltip("Seconds between the horse's changes of direction while it fights the rope.")]
    [SerializeField] private Vector2 lungeSeconds = new Vector2(1.2f, 2.6f);
    [Tooltip("Seconds a worn-out horse stands for the player to mount before it fights again.")]
    [SerializeField, Min(1f)] private float mountSeconds = 8f;

    [Header("Breaking ride")]
    [SerializeField] private ItemDefinition barley;
    [Tooltip("Feeds it takes, at least and at most (each horse rolls its own).")]
    [SerializeField] private Vector2Int feedsNeeded = new Vector2Int(3, 5);
    [Tooltip("Seconds between its asking for barley.")]
    [SerializeField] private Vector2 feedInterval = new Vector2(10f, 30f);
    [Tooltip("Seconds it waits for barley once it has asked; then it throws the rider.")]
    [SerializeField, Min(1f)] private float feedWindow = 6f;

    [Header("In the world")]
    [Tooltip("Chance an in-game hour brings a wild horse to the land round the Haven (while there is none).")]
    [SerializeField, Range(0f, 1f)] private float spawnChancePerHour = 0.06f;
    [SerializeField, Min(0)] private int mostWild = 1;
    [Tooltip("Metres from the Haven's heart a wild horse grazes, nearest and farthest.")]
    [SerializeField] private Vector2 spawnRing = new Vector2(70f, 150f);

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Horse Prefab => prefab;
    public Color[] Coats => coats;
    public Vector2 Health => health;
    public Vector2 Stamina => stamina;
    public Vector2 GallopSpeed => gallopSpeed;
    public Vector2 CarryWeight => carryWeight;
    public float WalkSpeed => walkSpeed;
    public float TrotSpeed => trotSpeed;
    public float GallopCost => gallopCost;
    public float StaminaRegen => staminaRegen;
    public float TurnSpeed => turnSpeed;
    public float ShyDistance => shyDistance;
    public float PerHeardMetre => perHeardMetre;
    public float WanderRadius => wanderRadius;
    public float FleeSeconds => fleeSeconds;
    public SoundDefinition Whinny => whinny;
    public ItemDefinition Lasso => lasso;
    public float LassoRange => lassoRange;
    public float StruggleStamina => struggleStamina;
    public float TapDrain => tapDrain;
    public float PullStrain => pullStrain;
    public float TapHold => tapHold;
    public Vector2 LungeSeconds => lungeSeconds;
    public float MountSeconds => mountSeconds;
    public ItemDefinition Barley => barley;
    public Vector2Int FeedsNeeded => feedsNeeded;
    public Vector2 FeedInterval => feedInterval;
    public float FeedWindow => feedWindow;
    public float SpawnChancePerHour => spawnChancePerHour;
    public int MostWild => mostWild;
    public Vector2 SpawnRing => spawnRing;
}
