using System;
using UnityEngine;

/// <summary>
/// The player character's front door. Systems that need "the player" (quests handing out rewards, the raid director
/// watching for death, the audio manager detecting combat, saving) ask <see cref="Current"/> instead of searching the
/// scene, and reach the player's parts through it (bag, resources, crafting, health) instead of calling GetComponent.
/// <para>
/// Where it belongs: on the root of the PlayerArmature prefab, next to the components it exposes. It wakes up early
/// (execution order -95) so it is registered before other systems' Awake and OnEnable look for it.
/// </para>
/// <para>
/// Extending: when the player gets a new part other systems need (equipment, skills), add a property here rather than
/// having callers GetComponent it, so there is one place that knows how the player is built.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-95)]
[RequireComponent(typeof(ItemInventory), typeof(ResourceInventory), typeof(Damageable))]
public sealed class Player : MonoBehaviour
{
    /// <summary>The player in the loaded scene, or null where there is none (the start menu).</summary>
    public static Player Current { get; private set; }

    /// <summary>Raised when a player appears, after every scene change that has one.</summary>
    public static event Action<Player> Spawned;

    public ItemInventory Bag { get; private set; }
    public ResourceInventory Resources { get; private set; }
    public PlayerCrafting Crafting { get; private set; }
    public Damageable Health { get; private set; }
    public PlayerStamina Stamina { get; private set; }
    /// <summary>Hunger and thirst.</summary>
    public PlayerNeeds Needs { get; private set; }
    /// <summary>The equipped weapon.</summary>
    public PlayerEquipment Equipment { get; private set; }
    public PlayerSkills Skills { get; private set; }
    public Combatant Combatant { get; private set; }
    public PlayerPersistence Persistence { get; private set; }
    public PlayerStealth Stealth { get; private set; }
    /// <summary>The sounds the player makes, and how far they can be heard right now.</summary>
    public PlayerNoise Noise { get; private set; }
    /// <summary>Timed bonuses: meals, tonics, gifts.</summary>
    public PlayerBoons Boons { get; private set; }

    /// <summary>True when the object is the player or part of it, such as a collider on a child bone.</summary>
    public static bool IsPlayer(GameObject candidate)
    {
        return candidate != null && candidate.GetComponentInParent<Player>() != null;
    }

    private void Awake()
    {
        PlayerInterrupts.For(gameObject); // first, so the activities' registrations find it
        ColdBreath.AddTo(gameObject);
        PlayerTraining.AddTo(gameObject);
        PlayerTechniques.AddTo(gameObject);
        PlayerWarmth.AddTo(gameObject);
        CarriedGrime.AddTo(gameObject); // blood and mud from a fight, washed off by rain and water
        PlayerGlow.AddTo(gameObject); // a soft light round them in the dark
        OffScreenWarnings.AddTo(gameObject); // a strike winding up at them from out of view, at the screen's edge
        if (Current != null && Current != this)
        {
            Debug.LogWarning($"A second player ({name}) appeared; the newest one is used.", this);
        }

        Current = this;
        Bag = GetComponent<ItemInventory>();
        Resources = GetComponent<ResourceInventory>();
        Crafting = GetComponent<PlayerCrafting>();
        Health = GetComponent<Damageable>();
        Stamina = GetComponent<PlayerStamina>();
        Needs = GetComponent<PlayerNeeds>();
        Equipment = GetComponent<PlayerEquipment>();
        Skills = GetComponent<PlayerSkills>();
        Combatant = GetComponent<Combatant>();
        // Every blow the player lands, and what it leaves burning or poisoned (Combat Technique › Player Damage).
        if (Combatant != null)
        {
            Combatant.DamageDealtScale = CombatTechniqueSettings.Current.PlayerDamage;
        }
        Persistence = GetComponent<PlayerPersistence>();
        Stealth = GetComponent<PlayerStealth>();
        Noise = GetComponent<PlayerNoise>();
        Boons = GetComponent<PlayerBoons>();
    }

    private void Start()
    {
        Spawned?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        Spawned = null;
    }
}
