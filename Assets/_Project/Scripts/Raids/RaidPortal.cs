using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The way into raids from Safe Haven (GDD §6.1, §21.3). It starts as a ruin: using it offers a repair that costs Life
/// Orbs (SettlementSettings.PortalRepairCost). Once repaired (saved in SettlementState) it glows and works as the Portal
/// crafting station (Portal Sparks are queued here); using it opens the station menu with "Plan a raid" first, which
/// opens the raid planner (RaidPreparationWindow) for its destinations. Locked destinations (Troll Valley) are shown but
/// can't be entered yet. Without a CraftingStation, using the repaired portal opens the planner directly.
/// <para>
/// Where it belongs: on the portal in Safe Haven, next to an Interactable that doesn't deactivate on completion and a
/// CraftingStation of type Portal. The dormant and active looks are child objects switched on and off.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
public sealed class RaidPortal : MonoBehaviour, IStationMenuExtension, IInteractionName
{
    [Tooltip("The first destination offered.")]
    [SerializeField] private RaidDefinition raid;
    [Tooltip("More destinations, in the order the planner lists them. Locked ones show what is to come.")]
    [SerializeField] private List<RaidDefinition> otherDestinations = new List<RaidDefinition>();
    [Tooltip("Left empty, the first RaidPreparationWindow in the scene is used.")]
    [SerializeField] private RaidPreparationWindow preparationWindow;
    [Tooltip("The window closes when the player walks further than this from the portal, in meters.")]
    [SerializeField, Min(1f)] private float windowRange = 5f;

    [Header("Repair")]
    [Tooltip("Starts as a ruin that must be repaired. Turn off for portals that work from the start (test scenes).")]
    [SerializeField] private bool needsRepair = true;
    [Tooltip("Shown while the portal is a ruin. Optional.")]
    [SerializeField] private GameObject dormantVisual;
    [Tooltip("Shown once the portal works. Optional.")]
    [SerializeField] private GameObject activeVisual;
    [Tooltip("Raised when the portal is repaired, for quests.")]
    [SerializeField, RaisedByGame] private GameEventDefinition repairedEvent;
    [Tooltip("Optional progression requirement for repairs only. Inspecting the dormant portal remains possible.")]
    [SerializeField] private QuestCondition repairRequires;
    [SerializeField] private string repairLockedText = "Free the Heart Tree's light first.";
    [SerializeField, TextArea] private string ruinText = "Ancient standing stones, cracked and cold. A faint hum lingers in them, as if they remember being a door. Life Orbs might wake them.";

    private Interactable interactable;
    private CraftingStation station;

    /// <summary>The first destination.</summary>
    public RaidDefinition Raid => raid;

    /// <summary>Every destination, the first one first.</summary>
    public IReadOnlyList<RaidDefinition> Destinations
    {
        get
        {
            var all = new List<RaidDefinition>();
            if (raid != null)
            {
                all.Add(raid);
            }

            foreach (RaidDefinition other in otherDestinations)
            {
                if (other != null && !all.Contains(other))
                {
                    all.Add(other);
                }
            }

            return all;
        }
    }

    public bool IsRepaired => !needsRepair || GameSession.Settlement.PortalRepaired;
    public string InteractionName => IsRepaired ? "Portal" : "Ruined Portal";

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        station = GetComponent<CraftingStation>();
    }

    private void OnEnable()
    {
        interactable.Completed.AddListener(OnUsed);
        GameSession.Settlement.Changed += ShowState;
        ShowState();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        GameSession.Settlement.Changed -= ShowState;
    }

    /// <summary>True while a position is close enough to keep the preparation window open.</summary>
    public bool IsInReach(Vector3 position)
    {
        return interactable.DistanceFrom(position) <= windowRange;
    }

    private void ShowState()
    {
        bool repaired = IsRepaired;
        if (dormantVisual != null)
        {
            dormantVisual.SetActive(!repaired);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(repaired);
        }

        if (station != null)
        {
            station.Usable = repaired;
        }
    }

    private void OnUsed()
    {
        GameObject user = interactable.Interactor;
        if (!IsRepaired)
        {
            if (ActionMenuWindow.Current != null)
            {
                ActionMenuWindow.Current.Open(() => RuinPage(user), user, transform, windowRange + 2f);
            }

            return;
        }

        // A repaired portal with a CraftingStation opens the station menu itself, with "Plan a raid" at the top.
        if (station == null)
        {
            OpenPlanner(user);
        }
    }

    public void AddActions(MenuPage page, GameObject user)
    {
        if (IsRepaired)
        {
            page.Add(new MenuAction("Plan a raid", "Choose a destination, supplies and modifiers", true, () => OpenPlanner(user), closesMenu: true));
        }
    }

    private void OpenPlanner(GameObject user)
    {
        if (preparationWindow == null && UIStateManager.Instance != null)
        {
            preparationWindow = UIStateManager.Instance.Find<RaidPreparationWindow>(UIWindowId.RaidPreparation);
        }

        if (preparationWindow == null)
        {
            Debug.LogError("RaidPortal found no RaidPreparationWindow in the scene.", this);
            return;
        }

        preparationWindow.Open(this, user);
    }

    private MenuPage RuinPage(GameObject user)
    {
        IReadOnlyList<ResourceAmount> cost = SettlementSettings.Current.PortalRepairCost;
        bool affordable = MaterialPool.For(user).CanAfford(cost, null, out string missing);
        bool unlocked = repairRequires.IsMet;
        var page = new MenuPage { Title = "Ruined Portal", Body = ruinText };
        page.Add(new MenuAction("Repair the portal", !unlocked ? repairLockedText
            : AmountText.Describe(cost) + (affordable ? "" : $"   <color=#FF7B6B>{missing}</color>"), unlocked && affordable,
            () => Repair(user), closesMenu: true));
        return page;
    }

    private void Repair(GameObject user)
    {
        if (!repairRequires.IsMet)
        {
            GameMessages.Post(repairLockedText, MessageKind.Warning);
            return;
        }
        if (IsRepaired || !MaterialPool.For(user).TrySpend(SettlementSettings.Current.PortalRepairCost))
        {
            return;
        }

        GameSession.Settlement.RepairPortal();
        GameMessages.Post("The stones drink the Life Orbs' light. The portal wakes, humming, and the mist beyond it parts.", MessageKind.Gain);
        if (repairedEvent != null)
        {
            GameEvents.Raise(repairedEvent, user);
        }
    }
}