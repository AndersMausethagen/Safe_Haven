using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored data for a kind of monster: identity, combat stats, faction, loot and how it behaves in a fight. Monsters
/// drop items, not raw resources, and a boss can teach the recipes for the next tier. Every instance reads this; runtime
/// values such as current health and current target live on its components.
/// </summary>
[CreateAssetMenu(fileName = "NewMonster", menuName = "Safe Haven/Monsters/Monster Definition")]
public sealed class MonsterDefinition : ScriptableObject, ICombatSounds
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
    [Tooltip("Never made an elite: a raid's great beast is strong enough, and a summoned swarm isn't worth it.")]
    [SerializeField] private bool neverElite;

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
    [Tooltip("It hunts horses first (wolves): a horse is noticed this many times as fast as anyone else. 1 = no preference.")]
    [SerializeField, Min(1f)] private float huntsHorses = 1f;
    [Tooltip("Horses shy from it (draugr, trolls): within this many metres, a horse that isn't brave, very fond of its rider or " +
             "barded panics. 0 = horses don't mind it.")]
    [SerializeField, Min(0f)] private float frightensHorses;

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

    [Header("Idle Life")]
    [Tooltip("How far around home it wanders while nothing is going on, in metres. 0: it stays where it is.")]
    [SerializeField, Min(0f)] private float idleWanderRadius;
    [Tooltip("Seconds it stays put between walks, least and most.")]
    [SerializeField] private Vector2 idlePauseSeconds = new Vector2(3f, 8f);
    [Tooltip("Its speed while wandering, as a share of its full speed.")]
    [SerializeField, Range(0.1f, 1f)] private float idleWalkSpeed = 0.4f;
    [Tooltip("Chance it settles down to graze (head down) after a walk.")]
    [SerializeField, Range(0f, 1f)] private float idleGrazeChance;
    [Tooltip("Chance it settles down to sleep after a walk.")]
    [SerializeField, Range(0f, 1f)] private float idleSleepChance;
    [Tooltip("How much of what it sees a sleeper notices. Low enough to creep past.")]
    [SerializeField, Range(0f, 1f)] private float idleSleepAttention = 0.3f;

    [Header("Game")]
    [Tooltip("It runs instead of fighting: when struck, and when the player comes into its sight within its Detection Range (a sneaking " +
             "hunter gets closer) or steps inside its Close Sense Range. For game to hunt, set Faction to Neutral as well.")]
    [SerializeField] private bool timid;
    [Tooltip("Seconds it runs before it settles again, wherever it has got to.")]
    [SerializeField, Min(1f)] private float boltSeconds = 7f;
    [Tooltip("A thief (the tusse): it sneaks up to the player, or a cart near them, snatches one stack and runs. It fights only " +
             "when cornered. Killed, it drops what it took; once it gets far enough away, it is gone with it (MonsterAI.Thief).")]
    [SerializeField] private bool thief;
    [Tooltip("Metres from the player (or a cart) at which it grabs.")]
    [SerializeField, Min(0.5f)] private float stealReach = 1.5f;
    [Tooltip("Metres from the player at which a thief with something is away, and gone.")]
    [SerializeField, Min(5f)] private float escapeDistance = 40f;
    [Tooltip("Seconds it fights back when cornered (the player beside it and nowhere to run), before it tries to run again.")]
    [SerializeField, Min(0.5f)] private float corneredSeconds = 4f;

    [Header("In a Fight")]
    [Tooltip("A person-shaped creature, whom a finishing blow can line up with: a press close behind it while it staggers locks the two together for the stab.")]
    [SerializeField] private bool canBeFinished;
    [Tooltip("How much it spreads out around its target with others of its side, so a group surrounds rather than clumps. 0: straight at it. 1: the widest flank.")]
    [SerializeField, Range(0f, 1f)] private float flankSpread;
    [Tooltip("When it attacks the Haven (ward breaches, corruption nights) it goes for walls and buildings, walls first, and " +
             "pays people no mind until one hurts it. For the big trolls, which come to tear the place down.")]
    [SerializeField] private bool breaksWalls;

    [Header("Poise")]
    [Tooltip("How much it takes before it staggers (attacks' Poise Damage wears it down). 0 uses the game-wide default; " +
             "a very high value makes it unstoppable.")]
    [SerializeField, Min(0f)] private float poise;
    [Tooltip("What a parry of its strike does to it. Auto: an ordinary creature staggers, a great beast, boss or elite (one that isn't " +
             "Open While Striking) loses part of its poise instead (Combat Technique Settings › Parry Poise Share) and staggers only " +
             "when that breaks it. Recoil: it only reels back a moment, its strike broken off.")]
    [SerializeField] private ParryResponse parryResponse = ParryResponse.Auto;

    [Header("Backstabs and guarding")]
    [Tooltip("A blow from behind in the middle of its strike is a backstab. Off for great beasts and bosses: they have to be staggered first. Elites never are, whatever this says.")]
    [SerializeField] private bool openWhileStriking = true;
    [Tooltip("Once it has learned, it can parry a backstab it sees coming, not only hop aside. For what has the hands for it.")]
    [SerializeField] private bool parriesBackstabs;
    [Tooltip("Width in degrees of the shield it holds in front of it while it hunts. 0: none. Heavy blows and arts go through; its strike lets it drop.")]
    [SerializeField, Range(0f, 240f)] private float shieldArc;
    [Tooltip("Share of a blow that gets past the shield.")]
    [SerializeField, Range(0f, 1f)] private float shieldLetsThrough = 0.25f;

    [Header("Body")]
    [Tooltip("Blows draw blood. Off for what has none in it (a Marsh Light).")]
    [SerializeField] private bool bleeds = true;
    [Tooltip("What flies from a blow and stains the ground: red for the living, sap for what grows, black for the dead.")]
    [SerializeField] private Color bloodColor = new Color(0.32f, 0.015f, 0.02f, 0.95f);
    [Tooltip("Its body drops and is thrown by the killing blow. Off for what has no body to speak of.")]
    [SerializeField] private bool ragdollOnDeath = true;
    [Tooltip("With Ragdoll On Death: it falls by its Death animation instead of going loose, as a tree should. Off, the body is thrown by the blow.")]
    [SerializeField] private bool fallsByAnimation;
    [Tooltip("Small enough that a killing blow from a two-handed blunt weapon can burst it, flinging pieces and gore " +
             "(Combat Feel › Gore › Smash Chance). For the small ones: rootlings, wolves, boars, little trolls.")]
    [SerializeField] private bool canBeSmashed;
    [Tooltip("A slashing killing blow can take its head or a limb, which then bleeds hard (Combat Feel › Gore › Sever Chance). " +
             "Off for what has nothing to cut off.")]
    [SerializeField] private bool canLoseLimbs = true;
    [Tooltip("What it is made of: what a blow on it throws off and sounds like (HitMaterials). Flesh bleeds as usual.")]
    [SerializeField] private HitMaterial hitMaterial = HitMaterial.Flesh;

    [Header("Wounds")]
    [Tooltip("Below this share of its health it limps: slower, so a hurt creature can be run down. 0: never.")]
    [SerializeField, Range(0f, 1f)] private float limpBelow = 0.3f;
    [Tooltip("How fast it moves while limping, as a share.")]
    [SerializeField, Range(0.3f, 1f)] private float limpSpeed = 0.75f;
    [Tooltip("Below this share of its health it leaves a trail of blood as it goes (if it bleeds), which can be followed. 0: never.")]
    [SerializeField, Range(0f, 1f)] private float bleedTrailBelow = 0.5f;
    [Tooltip("Pieces of armour a heavy blow can break off it (knights, the armoured dead). Each one costs it some of its armour.")]
    [SerializeField, Min(0)] private int armourChips;
    [Tooltip("Share of its armour each broken piece takes with it.")]
    [SerializeField, Range(0f, 0.5f)] private float armourPerChip = 0.12f;
    [Tooltip("A blow taking at least this share of its health breaks a piece off (a blunt blow, half as much).")]
    [SerializeField, Range(0.01f, 0.5f)] private float chipFromShare = 0.07f;

    [Tooltip("How much bigger or smaller each one is than the model, as a share. 0.08 is up to 8% either way. Leave a one-off at 0.")]
    [SerializeField, Range(0f, 0.3f)] private float sizeVariation;
    [Tooltip("How much lighter, darker, warmer or colder each one is, as a share, so a pack isn't one creature several times over.")]
    [SerializeField, Range(0f, 0.3f)] private float shadeVariation;

    [Header("Journal")]
    [Tooltip("Its picture in the journal.")]
    [SerializeField] private Sprite portrait;
    [Tooltip("What people tell of it: the story, not the fight.")]
    [SerializeField, TextArea(2, 8)] private string journalLore;
    [Tooltip("How to fight it, or to keep away from it.")]
    [SerializeField, TextArea(2, 8)] private string journalAdvice;
    [Tooltip("Where it is found, in a line.")]
    [SerializeField] private string journalHabitat;
    [Tooltip("How many have to fall before the journal knows how to fight it. Twice this and it knows what is told of it; at 1, the first is enough for both. Being killed by one also teaches the fight.")]
    [SerializeField, Min(1)] private int journalKillsToKnow = 3;

    [Header("Sound")]
    [Tooltip("Plays when it is killed. Empty uses the creature default from Audio Settings.")]
    [SerializeField] private SoundDefinition deathSound;
    [Tooltip("Plays when it is hurt (on top of the hit sound). Empty: none.")]
    [SerializeField] private SoundDefinition hurtSound;
    [Tooltip("Plays as it winds up an attack, so the player hears it coming. Empty: none.")]
    [SerializeField] private SoundDefinition attackSound;

    public bool NeverElite => neverElite;
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
    /// <summary>Game: it runs instead of fighting (MonsterAI.Timid).</summary>
    public bool Timid => timid;
    public float BoltSeconds => boltSeconds;
    /// <summary>A thief: snatches a stack from the player or a cart and runs (MonsterAI.Thief).</summary>
    public bool Thief => thief;
    public float StealReach => stealReach;
    public float EscapeDistance => escapeDistance;
    public float CorneredSeconds => corneredSeconds;
    public float DetectionRange => detectionRange;
    public float ChaseRange => Mathf.Max(chaseRange, detectionRange);
    public float LeashRange => leashRange;
    public float SightAngle => sightAngle;
    public float CloseSenseRange => closeSenseRange;
    public float Hearing => hearing;
    public float Alertness => alertness;
    public float HuntsHorses => huntsHorses;
    public float FrightensHorses => frightensHorses;
    public Sprite Portrait => portrait;
    public string JournalLore => journalLore;
    public string JournalAdvice => journalAdvice;
    public string JournalHabitat => journalHabitat;
    public int JournalKillsToKnow => Mathf.Max(1, journalKillsToKnow);
    public bool FallsByAnimation => fallsByAnimation;
    public float SizeVariation => sizeVariation;
    public float ShadeVariation => shadeVariation;
    public SoundDefinition DeathSound => deathSound;
    public SoundDefinition HurtSound => hurtSound;
    public SoundDefinition AttackSound => attackSound;
    /// <summary>Its poise, or 0 when it uses the game-wide default.</summary>
    public float Poise => poise;
    public ParryResponse ParryResponse => parryResponse;
    public bool OpenWhileStriking => openWhileStriking;
    public bool ParriesBackstabs => parriesBackstabs;
    public float ShieldArc => shieldArc;
    public float ShieldLetsThrough => shieldLetsThrough;
    public bool Bleeds => bleeds;
    public Color BloodColor => bloodColor;
    public bool RagdollOnDeath => ragdollOnDeath;
    public bool CanBeSmashed => canBeSmashed;
    public bool CanLoseLimbs => canLoseLimbs;
    public HitMaterial HitMaterial => hitMaterial;
    public float LimpBelow => limpBelow;
    public float LimpSpeed => limpSpeed;
    public float BleedTrailBelow => bleedTrailBelow;
    public int ArmourChips => armourChips;
    public float ArmourPerChip => armourPerChip;
    public float ChipFromShare => chipFromShare;
    public IReadOnlyList<MonsterAttack> Attacks => attacks;
    public MonsterMovementTactic MovementTactic => movementTactic;
    public MonsterTemperament Temperament => temperament;
    public float PreferredDistance => preferredDistance;
    public float DistanceTolerance => distanceTolerance;
    public float FleeHealthFraction => fleeHealthFraction;
    public float FleeDistance => fleeDistance;
    public float DefensiveRadius => defensiveRadius;
    public float IdleWanderRadius => idleWanderRadius;
    public Vector2 IdlePauseSeconds => idlePauseSeconds;
    public float IdleWalkSpeed => idleWalkSpeed;
    public float IdleGrazeChance => idleGrazeChance;
    public float IdleSleepChance => idleSleepChance;
    public float IdleSleepAttention => idleSleepAttention;
    public float FlankSpread => flankSpread;
    /// <summary>At the Haven it goes for walls and buildings before people (HomeAssault).</summary>
    public bool BreaksWalls => breaksWalls;
    public bool CanBeFinished => canBeFinished;
    public float HoverHeight => hoverHeight;
    public bool HasTemperament(MonsterTemperament value) => (temperament & value) != 0;

    private void OnValidate()
    {
        chaseRange = Mathf.Max(chaseRange, detectionRange);
        drops.Validate();
    }
}

/// <summary>What a parry of a monster's strike does to it (MonsterDefinition › Parry Response).</summary>
public enum ParryResponse
{
    /// <summary>Stagger for an ordinary creature, Poise Damage for a great beast, boss or elite.</summary>
    Auto,
    /// <summary>It staggers: open to blows and a backstab.</summary>
    Stagger,
    /// <summary>It loses part of its poise and reels a moment; it staggers only when that breaks it.</summary>
    PoiseDamage,
    /// <summary>It only reels back a moment, its strike broken off.</summary>
    Recoil,
}
