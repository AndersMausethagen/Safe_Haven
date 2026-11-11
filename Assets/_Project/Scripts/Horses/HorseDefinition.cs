using UnityEngine;

/// <summary>What a special breed can do that the common horse can't.</summary>
[System.Flags]
public enum HorseAbility
{
    None = 0,
    /// <summary>Mud and bog don't slow it.</summary>
    BogStrider = 1,
    /// <summary>Poison and rot don't hurt it.</summary>
    PoisonProof = 2,
    /// <summary>It glows in the dark: a light about it at night.</summary>
    NightGlow = 4,
    /// <summary>Frost doesn't slow or hurt it.</summary>
    ColdProof = 8,
}

/// <summary>A coat rarer than the common ones: its own look (a tint, and a pattern texture for dapples or patches) and mane.</summary>
[System.Serializable]
public struct RareCoat
{
    public string name;
    public Color tint;
    [Tooltip("The body's texture for this coat (dapples, patches); empty for the common coat's with the tint.")]
    public Texture2D pattern;
    [Tooltip("Index into Manes for its mane, or -1 for the horse's own roll.")]
    public int mane;
}

/// <summary>Where a kind of horse is met.</summary>
public enum HorseHome
{
    HavenRing = 0,
    Bog = 1,
}

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
    [Tooltip("Manes and tails a horse can have (materials for the Hair parts). Empty: the model's own.")]
    [SerializeField] private Material[] manes = System.Array.Empty<Material>();
    [Tooltip("For each coat, the manes it goes with (indices into Manes, comma-separated, e.g. \"0,2\"). Empty: any mane.")]
    [SerializeField] private string[] coatManes = System.Array.Empty<string>();
    [Tooltip("Rare coats (palomino, dapple, pinto...): rolled now and then for a wild horse, and passed on by breeding.")]
    [SerializeField] private RareCoat[] rareCoats = System.Array.Empty<RareCoat>();
    [SerializeField, Range(0f, 1f)] private float rareCoatChance = 0.06f;
    [Tooltip("What this breed can do that the common horse can't.")]
    [SerializeField] private HorseAbility abilities;

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
    [Tooltip("The canter a rider picks with a tap of Space: faster than a trot, and free (no stamina).")]
    [SerializeField, Min(0.1f)] private float canterSpeed = 8.5f;
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
    [Tooltip("A long neigh: answering a whistle, rearing on the rope, being tamed.")]
    [SerializeField] private SoundDefinition neigh;
    [Tooltip("A snort and a shake of the head, while it stands.")]
    [SerializeField] private SoundDefinition snort;
    [Tooltip("Hard breathing after a gallop.")]
    [SerializeField] private SoundDefinition breathing;
    [Tooltip("A hoofbeat, every beat of its gait: on earth and grass, and on stone, wood and in shallow water.")]
    [SerializeField] private SoundDefinition hooves;
    [SerializeField] private SoundDefinition hoovesStone;
    [SerializeField] private SoundDefinition hoovesWood;
    [SerializeField] private SoundDefinition hoovesWater;

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
    [Tooltip("Where it is met: the ring of land round the Haven, or the bog.")]
    [SerializeField] private HorseHome home = HorseHome.HavenRing;
    [Tooltip("A special horse: rare, never the first one promised; its foals are of its kind when the mare is.")]
    [SerializeField] private bool special;
    [Tooltip("It wears its own barding from birth (the bog's toxic horse); barding put on it only changes the look and the protection.")]
    [SerializeField] private bool alwaysArmoured;
    [Tooltip("Chance an in-game hour brings a wild horse to the land round the Haven (while there is none).")]
    [SerializeField, Range(0f, 1f)] private float spawnChancePerHour = 0.06f;
    [SerializeField, Min(0)] private int mostWild = 1;
    [Tooltip("Metres from the Haven's heart a wild horse grazes, nearest and farthest (Home: Haven Ring).")]
    [SerializeField] private Vector2 spawnRing = new Vector2(70f, 150f);
    [Tooltip("Home: Bog. Where the bog lies from the Haven's heart (degrees from north, metres) and how wide it is; BuildHorses copies it from the world's recipe.")]
    [SerializeField] private Vector2 homeAt = new Vector2(195f, 345f);
    [SerializeField, Min(1f)] private float homeRadius = 95f;
    [Tooltip("Hours of the day it is met (from, to; e.g. 20 to 5 for the night). Both 0: any hour.")]
    [SerializeField] private Vector2 activeHours = Vector2.zero;
    [Tooltip("Kick damage of its forelegs (a bold horse turns on what hurt it).")]
    [SerializeField, Min(0f)] private float strikeDamage = 16f;

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
    public float CanterSpeed => canterSpeed;
    public float GallopCost => gallopCost;
    public float StaminaRegen => staminaRegen;
    public float TurnSpeed => turnSpeed;
    public float ShyDistance => shyDistance;
    public float PerHeardMetre => perHeardMetre;
    public float WanderRadius => wanderRadius;
    public float FleeSeconds => fleeSeconds;
    public SoundDefinition Whinny => whinny;
    public SoundDefinition Neigh => neigh != null ? neigh : whinny;
    public SoundDefinition Snort => snort;
    public SoundDefinition Breathing => breathing;
    public SoundDefinition Hooves => hooves;

    /// <summary>The hoofbeat for the ground it is on (the earth's when there is none of its own).</summary>
    public SoundDefinition HoovesOn(HorseGround.Surface surface)
    {
        SoundDefinition own = surface == HorseGround.Surface.Stone ? hoovesStone : surface == HorseGround.Surface.Wood ? hoovesWood
            : surface == HorseGround.Surface.Water ? hoovesWater : null;
        return own != null ? own : hooves;
    }
    public Material[] Manes => manes;
    public RareCoat[] RareCoats => rareCoats;
    public HorseAbility Abilities => abilities;
    public bool Has(HorseAbility ability) => (abilities & ability) != 0;

    /// <summary>A rare coat now and then (Rare Coat Chance): 1 + its index, or 0 for a common one.</summary>
    public int RollRareCoat(System.Random random) =>
        rareCoats.Length > 0 && random.NextDouble() < rareCoatChance ? random.Next(rareCoats.Length) + 1 : 0;

    /// <summary>A horse's rare coat (1 + index), or null.</summary>
    public RareCoat? RareCoatOf(int rare) => rare > 0 && rare <= rareCoats.Length ? rareCoats[rare - 1] : (RareCoat?)null;
    public HorseHome Home => home;
    public bool IsSpecial => special;
    public bool AlwaysArmoured => alwaysArmoured;

    /// <summary>A mane for this coat: one of those listed for it, any when none are.</summary>
    public int ManeFor(int coat, System.Random random)
    {
        if (manes.Length == 0)
        {
            return -1;
        }

        string listed = coat >= 0 && coat < coatManes.Length ? coatManes[coat] : "";
        var choices = new System.Collections.Generic.List<int>();
        foreach (string part in listed.Split(','))
        {
            if (int.TryParse(part.Trim(), out int index) && index >= 0 && index < manes.Length)
            {
                choices.Add(index);
            }
        }

        return choices.Count > 0 ? choices[random.Next(choices.Count)] : random.Next(manes.Length);
    }
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
    public float StrikeDamage => strikeDamage;
    public float HomeRadius => homeRadius;

    /// <summary>Where its home lies from the Haven's heart, on the ground (x, z).</summary>
    public Vector3 HomeOffset => new Vector3(Mathf.Sin(homeAt.x * Mathf.Deg2Rad), 0f, Mathf.Cos(homeAt.x * Mathf.Deg2Rad)) * homeAt.y;

    /// <summary>Whether it may be met at this hour of the day.</summary>
    public bool IsAbroadAt(float hour)
    {
        if (Mathf.Approximately(activeHours.x, activeHours.y))
        {
            return true;
        }

        return activeHours.x < activeHours.y ? hour >= activeHours.x && hour < activeHours.y : hour >= activeHours.x || hour < activeHours.y;
    }
}
