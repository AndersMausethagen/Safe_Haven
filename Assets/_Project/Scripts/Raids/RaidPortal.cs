using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The way into raids from Safe Haven (GDD §6.1, §21.3). It starts as a ruin: using it offers a repair that costs Life
/// Orbs (SettlementSettings.PortalRepairCost). Once repaired (saved in SettlementState) it glows and works as the Portal
/// crafting station (Portal Sparks are queued here), and "Plan a raid" opens the raid planner (RaidPreparationWindow) for its
/// destinations. Locked destinations (Troll Valley) are shown but can't be entered yet. On the portal at the heart of Safe Haven a
/// HomePortal beside it owns using it, and shows these actions in its one menu; a portal without one acts for itself: its station
/// menu, or the planner directly when it has no CraftingStation.
/// <para>
/// Where it belongs: on the portal in Safe Haven, next to an Interactable that doesn't deactivate on completion and a
/// CraftingStation of type Portal. The dormant and active looks are child objects switched on and off.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[InAFriendsHaven(HavenUse.ItsChoicesDecide, "setting out is the host's; a visitor follows when they go")]
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
    [SerializeField] private string repairLockedText = "Cold, silent stones. They need their light back first.";
    [SerializeField, TextArea] private string ruinText = "Ancient standing stones, cracked and cold. A faint hum lingers in them, as if they remember being a door. Life Orbs might wake them.";

    private Interactable interactable;
    private CraftingStation station;
    private bool hasHomeMenu;

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

    public bool IsRepaired => !needsRepair || GameSession.Here.PortalRepaired;
    public string InteractionName => IsRepaired ? "Portal" : "Sleeping Portal";
    public string RuinText => ruinText;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        station = GetComponent<CraftingStation>();
        hasHomeMenu = TryGetComponent(out HomePortal _);
    }

    private void OnEnable()
    {
        if (!hasHomeMenu)
        {
            interactable.Completed.AddListener(OnUsed);
        }

        GameSession.Here.Changed += ShowState;
        ShowState();
    }

    private void OnDisable()
    {
        interactable.Completed.RemoveListener(OnUsed);
        GameSession.Here.Changed -= ShowState;
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
        // Co-op: the host went through; friends follow into the same raid.
        float secondsLeft = 0f;
        RaidDefinition following = CoopSession.Current != null ? CoopSession.Current.Raids.RaidToFollow(Destinations, out secondsLeft) : null;
        if (following != null)
        {
            page.Add(new MenuAction($"Follow {HavenVisit.HostName} to {following.DisplayName}", $"{Mathf.FloorToInt(secondsLeft / 60f)}:{Mathf.FloorToInt(secondsLeft % 60f):00} left of their raid. You bring only what you carry.",
                true, () => CoopSession.Current.Raids.FollowIntoRaid(following, user), closesMenu: true));
        }

        if (IsRepaired)
        {
            // A friend visiting goes on raids by following the host: there is nothing for them to plan.
            page.Add(HavenVisit.IsVisiting
                ? new MenuAction("Plan a raid", HavenVisit.RaidRefusal, false, null)
                : new MenuAction("Plan a raid", "Choose a destination, supplies and modifiers", true, () => OpenPlanner(user), closesMenu: true));
            AddImprovement(page, user);
        }
        else if (hasHomeMenu)
        {
            AddRepair(page, user);
        }
    }

    private static void AddImprovement(MenuPage page, GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Here.PortalLevel;
        if (level >= settings.PortalLevels.Count)
        {
            return;
        }

        SettlementSettings.PortalLevel next = settings.PortalLevels[level];
        bool affordable = MaterialPool.For(user).CanAfford(next.cost, next.items, out string missing);
        string cost = AmountText.Describe(next.items) + (next.items.Count > 0 && next.cost.Count > 0 ? ", " : "") + AmountText.Describe(next.cost);
        page.Add(new MenuAction($"Improve the portal: {next.title}", $"{next.description}\n{cost}" + (affordable ? "" : $"   <color={UIStyle.Current.DangerHex}>{missing}</color>"), affordable, () => Improve(user)));
    }

    // Paid from what the player has at hand: a visitor helps improve their friend's portal (HavenDecisions).
    private static void Improve(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Here.PortalLevel;
        if (level >= settings.PortalLevels.Count || !MaterialPool.For(user).TrySpend(settings.PortalLevels[level].cost, settings.PortalLevels[level].items))
        {
            return;
        }

        HavenDecisions.Do("portal.improve", level.ToString(System.Globalization.CultureInfo.InvariantCulture), settings.PortalLevels[level].cost, settings.PortalLevels[level].items);
    }

    // The portal improved from the level the player saw (another may have improved it meanwhile).
    [HavenDecision("portal.improve")]
    private static HavenDecisions.Outcome ImprovePortal(string fromLevel)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Here.PortalLevel;
        if (!int.TryParse(fromLevel, out int seen) || seen != level || level >= settings.PortalLevels.Count)
        {
            return HavenDecisions.Outcome.Refused("The portal was improved meanwhile: what you gave is back in your bag.");
        }

        GameSession.Home.ImprovePortal();
        // Its own kind: a quest's "Raise the portal to Tier II" is the ward's tier (PortalTiers), raised at the portal's menu. Counted
        // from this, the pocket's improvement finished Steady the Door while the tier it asked for was never raised.
        GameEvents.Raise(new GameEvent(GameEventKind.PortalImproved, null, GameSession.Home.PortalLevel));
        // The safe pocket grows with the portal (the Haven's own player's).
        if (Player.Current != null && Player.Current.TryGetComponent(out ItemInventory inventory))
        {
            inventory.RefreshPocket();
        }

        return HavenDecisions.Outcome.Made($"The stones settle: {settings.PortalLevels[level].description}");
    }

    private void OpenPlanner(GameObject user)
    {
        // Nor lay out a run they can't enter.
        if (HavenVisit.IsVisiting)
        {
            GameMessages.Post(HavenVisit.RaidRefusal);
            return;
        }

        if (preparationWindow == null && UIStateManager.Current != null)
        {
            preparationWindow = UIStateManager.Current.Find<RaidPreparationWindow>(UIWindowId.RaidPreparation);
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
        var page = new MenuPage { Title = "The Sleeping Portal", Body = ruinText };
        AddRepair(page, user);
        return page;
    }

    private void AddRepair(MenuPage page, GameObject user)
    {
        IReadOnlyList<ResourceAmount> cost = SettlementSettings.Current.PortalRepairCost;
        bool affordable = MaterialPool.For(user).CanAfford(cost, null, out string missing);
        bool unlocked = repairRequires.IsMet;
        page.Add(new MenuAction("Wake the portal", !unlocked ? repairLockedText
            : AmountText.Describe(cost) + (affordable ? "" : $"   <color={UIStyle.Current.DangerHex}>{missing}</color>"), unlocked && affordable,
            () => Repair(user), closesMenu: true) { HostOnly = true });
    }

    private void Repair(GameObject user)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        if (!repairRequires.IsMet)
        {
            GameMessages.Post(repairLockedText, MessageKind.Warning);
            return;
        }
        if (IsRepaired || !MaterialPool.For(user).TrySpend(SettlementSettings.Current.PortalRepairCost))
        {
            return;
        }

        GameSession.Home.RepairPortal();
        GameMessages.Post("The stones drink the Life Orbs' light. The portal wakes, humming, and the mist beyond it parts.", MessageKind.Gain);
        if (repairedEvent != null)
        {
            GameEvents.Raise(repairedEvent, user);
        }
    }
}