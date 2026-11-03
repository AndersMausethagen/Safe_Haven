using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a recipe is made. Values are serialized as numbers, so add new stations at the end and never renumber. The
/// identifiers are kept from earlier builds; the names players and designers see come from InspectorName and DisplayName
/// (GDD §14.2: Construction Table, Furnace, Forge, Cauldron...).
/// </summary>
public enum CraftingStationType
{
    /// <summary>Crafted anywhere, by hand.</summary>
    [InspectorName("By Hand")] None = 0,
    /// <summary>Basic gear, utilities and stone work; repairs gear.</summary>
    [InspectorName("Workshop")] Workbench = 1,
    /// <summary>The old Furnace. Nothing is made here now: ingots are smelted at the Smithy.</summary>
    [InspectorName("Furnace (old)")] Smelter = 2,
    /// <summary>Smelts ore into ingots and works metal into tools, weapons and armor.</summary>
    [InspectorName("Smithy")] Anvil = 3,
    /// <summary>Potions and brews (GDD Kitchen / Cauldron).</summary>
    [InspectorName("Cauldron")] AlchemyTable = 4,
    /// <summary>The Tailor's Shop: cloth, leather goods and packs. The identifier is kept from the old loom.</summary>
    [InspectorName("Tailor's Shop")] Loom = 5,
    /// <summary>Simple cooking: berries and water into meals (GDD Campfire).</summary>
    [InspectorName("Campfire")] Campfire = 6,
    /// <summary>Wood into planks, shields and bows.</summary>
    [InspectorName("Woodcutter's Lodge")] Sawbench = 7,
    /// <summary>The repaired portal: Portal Sparks from Life Orbs.</summary>
    [InspectorName("Portal")] Portal = 8,
    /// <summary>Proper cooking: meals with lasting bonuses (GDD Kitchen / Cauldron).</summary>
    [InspectorName("Kitchen")] Kitchen = 9,
    /// <summary>The Hunter's Lodge: hides into leather, bows and arrows. The identifier is kept from the old tanning rack.</summary>
    [InspectorName("Hunter's Lodge")] Tannery = 10,
    /// <summary>Crops: a seed crop goes in, grows on its own, even during raids, and waits to be harvested (GDD Farm Plot).</summary>
    [InspectorName("Farm Plot")] Farm = 11,
    /// <summary>Bees turn flowers and berries into honey.</summary>
    [InspectorName("Beehive")] Beehive = 12,
    [InspectorName("Wainwright's Yard")] Wainwright = 13,
}

public static class CraftingStationTypes
{
    public static string DisplayName(this CraftingStationType type)
    {
        switch (type)
        {
            case CraftingStationType.None:
                return "By hand";
            case CraftingStationType.Workbench:
                return "Workshop";
            case CraftingStationType.Smelter:
                return "Smithy";
            case CraftingStationType.Anvil:
                return "Smithy";
            case CraftingStationType.AlchemyTable:
                return "Cauldron";
            case CraftingStationType.Tannery:
                return "Hunter's Lodge";
            case CraftingStationType.Loom:
                return "Tailor's Shop";
            case CraftingStationType.Sawbench:
                return "Woodcutter's Lodge";
            case CraftingStationType.Farm:
                return "Farm Plot";
            case CraftingStationType.Wainwright:
                return "Wainwright's Yard";
            default:
                return type.ToString();
        }
    }

    /// <summary>The name with "a" or "an" in front, e.g. "an Anvil", for sentences shown to the player.</summary>
    public static string WithArticle(this CraftingStationType type)
    {
        string name = type.DisplayName();
        return ("AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an " : "a ") + name;
    }
}

/// <summary>
/// Adds actions to a crafting station's menu, above its recipes: the Construction Table's "Repair gear", the portal's
/// "Plan a raid". Put the component on the same object as the CraftingStation.
/// </summary>
public interface IStationMenuExtension
{
    void AddActions(MenuPage page, GameObject user);
}

/// <summary>
/// Makes a building a crafting station of one kind. Using it opens the StationWindow (or, in a scene without one, the
/// text menu StationMenu): the recipes known for this
/// station, its queue and its output slot. Crafts are queued and worked on over time, even while the player is away or in
/// a raid (StationQueue, kept in SettlementState). A station can be switched off (Usable = false), as the portal is
/// until it is repaired.
/// <para>
/// Where it belongs: on a station building prefab next to an Interactable that doesn't deactivate (BuildingPrefabBuilder
/// adds both). Its queue is found by the SaveableEntity id, or by Station Id when set (the portal has no SaveableEntity).
/// Output size and queue length come from the BuildingDefinition.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class CraftingStation : MonoBehaviour
{
    public const int DefaultOutputCapacity = 10;
    public const int DefaultQueueLength = 5;

    private static readonly List<CraftingStation> active = new List<CraftingStation>();

    [SerializeField] private CraftingStationType type = CraftingStationType.Workbench;
    [Tooltip("Fixed id for the station's queue in saves. Leave empty to use the SaveableEntity id.")]
    [SerializeField] private string stationId;
    [Tooltip("The menu closes when the player walks further than this from the station, in meters.")]
    [SerializeField, Min(1f)] private float menuRange = 6f;
    [Tooltip("Using it opens its window. Off when something beside it owns the interaction and calls Open Menu itself (the portal at home).")]
    [SerializeField] private bool opensWhenUsed = true;

    private Collider[] colliders;
    private Interactable interactable;
    private BuildingObject building;
    private SaveableEntity saveable;
    private Damageable health;
    private bool usable = true;

    public static IReadOnlyList<CraftingStation> Active => active;
    public CraftingStationType Type => type;

    /// <summary>False while the station can't be used yet, such as an unrepaired portal.</summary>
    public bool Usable
    {
        get => usable && !IsBroken;
        set => usable = value;
    }

    private bool IsBroken => health != null && health.IsBroken;

    /// <summary>The key of the station's queue in WorkshopState.</summary>
    public string Id
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(stationId))
            {
                return stationId;
            }

            return saveable != null && !string.IsNullOrEmpty(saveable.Id) ? saveable.Id : type + "@" + gameObject.name;
        }
    }

    public int OutputCapacity => (building != null && building.Definition != null ? building.Definition.OutputCapacity : DefaultOutputCapacity)
        + Mathf.RoundToInt(Upgrades(level => level.extraOutput));
    public int QueueLength => (building != null && building.Definition != null ? building.Definition.QueueLength : DefaultQueueLength)
        + Mathf.RoundToInt(Upgrades(level => level.extraQueue));

    /// <summary>How fast it works: upgrades, plus a resident whose job works here (WorkerSpeedBonus).</summary>
    public float Speed
    {
        get
        {
            ResidentRecord worker = Worker;
            float staffed = worker == null ? 0f : WorkerSpeedBonus + (GameSession.Settlement.HasBed(worker) ? BedSpeedBonus : 0f);
            return 1f + Upgrades(level => level.craftSpeed) + staffed;
        }
    }

    /// <summary>A resident working here makes the station this much faster.</summary>
    public const float WorkerSpeedBonus = 0.5f;

    /// <summary>And this much more when they have a bed of their own. They work without one.</summary>
    public const float BedSpeedBonus = 0.25f;

    /// <summary>True when a resident's job has this building as its workplace.</summary>
    public bool HasWorker => Worker != null;

    /// <summary>The resident whose job has this building as its workplace, or null.</summary>
    public ResidentRecord Worker
    {
        get
        {
            if (building == null || building.Definition == null)
            {
                return null;
            }

            foreach (ResidentRecord resident in GameSession.Settlement.Residents)
            {
                if (resident.Job != null && resident.Job.Workplace == building.Definition)
                {
                    return resident;
                }
            }

            return null;
        }
    }

    /// <summary>The station's queue and output slot, created on first use.</summary>
    public StationQueue Queue
    {
        get
        {
            StationQueue queue = GameSession.Settlement.Workshops.GetOrCreate(Id, type, OutputCapacity, QueueLength);
            if (queue != null)
            {
                queue.SpeedMultiplier = Speed;
                queue.Tier = Tier;
            }

            return queue;
        }
    }

    /// <summary>What it can make: its building's level, or no limit for a station that isn't one.</summary>
    public int Tier => TryGetComponent(out Construction construction) ? Mathf.Max(1, construction.Level) : Tiers.Max;

    private float Upgrades(System.Func<BuildingLevel, float> bonus) =>
        TryGetComponent(out Construction construction) ? construction.Sum(bonus) : 0f;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>();
        interactable = GetComponent<Interactable>();
        building = GetComponent<BuildingObject>();
        saveable = GetComponent<SaveableEntity>();
        health = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        active.Add(this);
        if (interactable != null && opensWhenUsed)
        {
            interactable.Completed.AddListener(OnUsed);
        }
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(OnUsed);
        }
    }

    private void OnUsed()
    {
        if (IsBroken)
        {
            GameMessages.Post("It's broken. Mend the clearing at the portal.", MessageKind.Warning);
        }
        else if (Usable)
        {
            OpenMenu(interactable.Interactor);
        }
    }

    public void OpenMenu(GameObject user)
    {
        if (user == null)
        {
            return;
        }

        // Cooking at a fire is done sitting by it; walking away stands them up.
        if (type == CraftingStationType.Campfire)
        {
            CharacterGestures.Play(user.transform, CharacterCatalog.Current.SitAtFire);
        }

        StationWindow stationWindow = UIStateManager.Instance != null ? UIStateManager.Instance.Find<StationWindow>(UIWindowId.Station) : null;
        if (stationWindow != null)
        {
            // Pages such as the repair list come back here with "Back to the ..."; the station window replaces them.
            if (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen)
            {
                ActionMenuWindow.Current.Close();
            }

            stationWindow.Open(this, user, menuRange);
        }
        else if (ActionMenuWindow.Current != null)
        {
            ActionMenuWindow.Current.Open(() => StationMenu.Build(this, user), user, transform, menuRange);
        }
    }

    /// <summary>True when a station of the kind is within range of the position. Hand crafting is possible anywhere.</summary>
    public static bool IsNear(Vector3 position, CraftingStationType type, float range)
    {
        if (type == CraftingStationType.None)
        {
            return true;
        }

        foreach (CraftingStation station in active)
        {
            if (station.type == type && station.Usable && station.HorizontalDistance(position) <= range)
            {
                return true;
            }
        }

        return false;
    }

    // Measured to the station's nearest edge, so a wide station is reachable from any side.
    private float HorizontalDistance(Vector3 position)
    {
        Vector3 nearest = transform.position;
        float best = float.MaxValue;
        foreach (Collider candidate in colliders)
        {
            if (candidate == null || !candidate.enabled)
            {
                continue;
            }

            Vector3 point = HitDetector.NearestPoint(candidate, position);
            float distance = (point - position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                nearest = point;
            }
        }

        Vector3 offset = nearest - position;
        offset.y = 0f;
        return offset.magnitude;
    }
}
