using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The raid planner (GDD §7.5), opened at the repaired portal: pick a destination (locked ones show what is to come), read
/// its briefing (known dangers, likely finds), buy extra time with Portal Sparks (GDD §7.3, up to the maximum, paid once on
/// entering), pick raid modifier items from the bag (each kind at most once; one is used up), preview the resulting rules
/// and enter. It states plainly what can be lost (GDD §18.6: before the first raid, not on the death screen). It closes
/// when the player walks away from the portal.
/// </summary>
public sealed class RaidPreparationWindow : MonoBehaviour
{
    private const string Changed = "#8BE08B";
    private const string Warning = "#FFB35A";

    [SerializeField] private UIWindow window;
    [SerializeField] private TMP_Text raidLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [Tooltip("Parent for the modifier rows. Should have a vertical layout group.")]
    [SerializeField] private RectTransform modifierList;
    [Tooltip("A hidden row that is copied for each kind of modifier item in the bag.")]
    [SerializeField] private RaidModifierRowView rowTemplate;
    [Tooltip("Shown when the bag holds no raid modifiers.")]
    [SerializeField] private GameObject noModifiersLabel;
    [Tooltip("Shows the resulting rules with rich text.")]
    [SerializeField] private TMP_Text rulesLabel;
    [Tooltip("Warns about what is at risk, or says why the raid can't start.")]
    [SerializeField] private TMP_Text riskLabel;
    [SerializeField] private Button enterButton;
    [SerializeField] private Button cancelButton;

    [Header("Destinations")]
    [Tooltip("Steps back through the portal's destinations. Optional.")]
    [SerializeField] private Button previousDestinationButton;
    [Tooltip("Steps forward through the portal's destinations. Optional.")]
    [SerializeField] private Button nextDestinationButton;

    [Header("Portal Sparks")]
    [Tooltip("Shows how many Portal Sparks are used and what they add. Optional; without it no time can be bought.")]
    [SerializeField] private TMP_Text sparksLabel;
    [SerializeField] private Button fewerSparksButton;
    [SerializeField] private Button moreSparksButton;
    [Tooltip("The loss rules, as stated to the player before every raid (GDD §21.3).")]
    [SerializeField, TextArea] private string lossRules = "If you fall or time runs out, you wake at the Heart Tree. What you carry is lost: materials, food and anything found in the raid. The tools, weapons and packs you bring return damaged and can be repaired at home. Your home remains safe.";

    private readonly List<RaidModifierRowView> rows = new List<RaidModifierRowView>();
    private readonly HashSet<ItemDefinition> picked = new HashSet<ItemDefinition>();
    private RaidPortal portal;
    private GameObject player;
    private ItemInventory inventory;
    private bool dirty;
    private int destinationIndex;
    private int sparks;
    private int pendingSeed;

    private void Awake()
    {
        rowTemplate.gameObject.SetActive(false);
        enterButton.onClick.AddListener(Enter);
        cancelButton.onClick.AddListener(Close);
        Listen(previousDestinationButton, () => StepDestination(-1));
        Listen(nextDestinationButton, () => StepDestination(1));
        Listen(fewerSparksButton, () => StepSparks(-1));
        Listen(moreSparksButton, () => StepSparks(1));
        window.OpenChanged += OnOpenChanged;
    }

    // Optional buttons may be left empty in older window prefabs.
    private static void Listen(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private void OnDestroy()
    {
        window.OpenChanged -= OnOpenChanged;
        Watch(null);
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (portal == null || player == null || !portal.IsInReach(player.transform.position))
        {
            Close();
            return;
        }

        if (dirty)
        {
            Rebuild();
        }
    }

    public void Open(RaidPortal source, GameObject user)
    {
        portal = source;
        player = user;
        picked.Clear();
        destinationIndex = 0;
        sparks = 0;
        // Rolled now, so the briefing shows the hour (and layout) the raid will really have.
        pendingSeed = RaidSession.NewSeed();
        Watch(user != null ? user.GetComponentInParent<ItemInventory>() : null);
        Rebuild();
        window.Open();
    }

    public void Close()
    {
        window.Close();
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            Watch(null);
            portal = null;
            player = null;
        }
    }

    // The bag can change while the window is open (crafting a modifier, say), so the list follows it.
    private void Watch(ItemInventory bag)
    {
        if (inventory != null)
        {
            inventory.Container.SlotChanged -= OnBagChanged;
        }

        inventory = bag;
        if (inventory != null)
        {
            inventory.Container.SlotChanged += OnBagChanged;
        }
    }

    private void OnBagChanged(int slot)
    {
        dirty = true;
    }

    private void Rebuild()
    {
        dirty = false;
        foreach (RaidModifierRowView row in rows)
        {
            Destroy(row.gameObject);
        }

        rows.Clear();
        var counts = new Dictionary<ItemDefinition, int>();
        var order = new List<ItemDefinition>();
        ItemContainer bag = inventory != null ? inventory.Container : null;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.IsEmpty || !stack.Item.IsRaidModifier)
            {
                continue;
            }

            if (!counts.ContainsKey(stack.Item))
            {
                order.Add(stack.Item);
                counts[stack.Item] = 0;
            }

            counts[stack.Item] += stack.Count;
        }

        picked.RemoveWhere(item => !counts.ContainsKey(item));
        foreach (ItemDefinition item in order)
        {
            RaidModifierRowView row = Instantiate(rowTemplate, modifierList);
            row.name = item.DisplayName;
            row.gameObject.SetActive(true);
            row.Bind(item, counts[item], Toggle);
            row.SetPicked(picked.Contains(item));
            rows.Add(row);
        }

        noModifiersLabel.SetActive(order.Count == 0);
        RefreshRules();
    }

    private void Toggle(ItemDefinition item)
    {
        if (!picked.Remove(item))
        {
            picked.Add(item);
        }

        foreach (RaidModifierRowView row in rows)
        {
            row.SetPicked(picked.Contains(row.Item));
        }

        RefreshRules();
    }

    private RaidDefinition Destination
    {
        get
        {
            if (portal == null)
            {
                return null;
            }

            IReadOnlyList<RaidDefinition> destinations = portal.Destinations;
            return destinations.Count > 0 ? destinations[Mathf.Clamp(destinationIndex, 0, destinations.Count - 1)] : null;
        }
    }

    private void StepDestination(int step)
    {
        int count = portal != null ? portal.Destinations.Count : 0;
        if (count > 0)
        {
            destinationIndex = (destinationIndex + step + count) % count;
            RefreshRules();
        }
    }

    private void StepSparks(int step)
    {
        sparks = Mathf.Clamp(sparks + step, 0, SettlementSettings.Current.MaxSparksPerRaid);
        RefreshRules();
    }

    private void RefreshRules()
    {
        RaidDefinition raid = Destination;
        string problem = RaidLauncher.CheckLaunch(raid);
        int destinations = portal != null ? portal.Destinations.Count : 0;
        if (previousDestinationButton != null && nextDestinationButton != null)
        {
            previousDestinationButton.gameObject.SetActive(destinations > 1);
            nextDestinationButton.gameObject.SetActive(destinations > 1);
        }
        RefreshSparks(raid);
        problem ??= RaidLauncher.CheckSparks(player, sparks);
        enterButton.interactable = problem == null;
        if (raid == null)
        {
            raidLabel.text = "The portal is dormant";
            descriptionLabel.text = "";
            rulesLabel.text = "";
            riskLabel.text = $"<color={Warning}>{problem}</color>";
            return;
        }

        raidLabel.text = $"{raid.DisplayName}  <size=16>{Tiers.RichLabel(raid.Tier)}{(raid.IsLocked ? "  <color=#AAB0BC>locked</color>" : "")}</size>";
        RaidTimeOfDay timeOfDay = RaidTimeOfDay.For(raid, pendingSeed);
        descriptionLabel.text = raid.Description + DescribeRumours(raid) + DescribeTimeOfDay(timeOfDay) + DescribeLayout(raid) + DescribeBriefing(raid);

        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in picked)
        {
            modifiers.Add(item.RaidModifier);
        }

        RaidParameters rules = RaidParameters.Calculate(raid, modifiers, RaidLauncher.ExtraSeconds(sparks), timeOfDay);
        var builder = new StringBuilder();
        AppendRule(builder, "Time limit", RaidStats.FormatTime(rules.Duration), raid, rules, RaidStat.Duration);
        AppendRule(builder, "Extraction", $"{rules.ExtractionTime:0.#}s", raid, rules, RaidStat.ExtractionTime);
        AppendRule(builder, "Cache loot", $"x{rules.LootMultiplier:0.##}", raid, rules, RaidStat.LootQuantity);
        AppendRule(builder, "Enemy health", $"x{rules.EnemyHealthMultiplier:0.##}", raid, rules, RaidStat.EnemyHealth);
        AppendRule(builder, "Enemy power", $"x{rules.EnemyPowerMultiplier:0.##}", raid, rules, RaidStat.EnemyPower);
        rulesLabel.text = builder.ToString();

        riskLabel.text = problem != null ? $"<color={Warning}>{problem}</color>" : DescribeRisk();
    }

    private static string DescribeTimeOfDay(RaidTimeOfDay timeOfDay)
    {
        if (timeOfDay == null)
        {
            return "";
        }

        string reward = timeOfDay.lootMultiplier > 1.001f ? $"  <color=#8BE08B>+{timeOfDay.lootMultiplier - 1f:P0} cache loot</color>" : "";
        string line = string.IsNullOrWhiteSpace(timeOfDay.briefing) ? "" : $" {timeOfDay.briefing}";
        int hours = Mathf.FloorToInt(timeOfDay.hour) % 24;
        int minutes = Mathf.FloorToInt((timeOfDay.hour - Mathf.Floor(timeOfDay.hour)) * 60f);
        return $"\n<color=#C9B8FF>Time of day:</color> {timeOfDay.DisplayName} ({hours:00}:{minutes:00}){reward}.{line}";
    }

    // A new forest every run: the briefing says which, as its seed is already rolled.
    private string DescribeLayout(RaidDefinition raid)
    {
        if (raid.LayoutRules == null)
        {
            return "";
        }

        (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, pendingSeed);
        return $"\n<color=#9FD3C7>This run:</color> {mood.name}. {mood.briefing} {shape.briefing}";
    }

    private string DescribeRumours(RaidDefinition raid)
    {
        List<RaidRumourDefinition> rumours = RaidRumours.For(raid, pendingSeed, GameSession.Settlement, player != null ? player.GetComponent<PlayerCrafting>() : null);
        var builder = new StringBuilder();
        foreach (RaidRumourDefinition rumour in rumours)
        {
            builder.Append("\n<color=#FFD27A>Rumour:</color> ").Append(rumour.Briefing);
        }

        return builder.ToString();
    }

    private static string DescribeBriefing(RaidDefinition raid)
    {
        var builder = new StringBuilder();
        var enemies = new List<string>();
        foreach (MonsterDefinition monster in raid.KnownEnemies)
        {
            if (monster != null)
            {
                enemies.Add(monster.DisplayName);
            }
        }

        var finds = new List<string>();
        foreach (ItemDefinition item in raid.LikelyItems)
        {
            if (item != null)
            {
                finds.Add(item.DisplayName);
            }
        }

        foreach (ResourceDefinition resource in raid.LikelyResources)
        {
            if (resource != null)
            {
                finds.Add(resource.DisplayName);
            }
        }

        if (enemies.Count > 0)
        {
            builder.Append("\n<color=#FF9A6B>Known dangers:</color> ").Append(string.Join(", ", enemies));
        }

        if (finds.Count > 0)
        {
            builder.Append("\n<color=#8BE08B>Likely finds:</color> ").Append(string.Join(", ", finds));
        }

        if (raid.IsLocked)
        {
            builder.Append("\n\n<color=#AAB0BC>").Append(raid.LockedText).Append("</color>");
        }

        return builder.ToString();
    }

    private void RefreshSparks(RaidDefinition raid)
    {
        SettlementSettings settings = SettlementSettings.Current;
        bool usable = sparksLabel != null && fewerSparksButton != null && moreSparksButton != null && settings.PortalSpark != null && settings.MaxSparksPerRaid > 0 && raid != null && !raid.IsLocked;
        if (sparksLabel != null)
        {
            sparksLabel.transform.parent.gameObject.SetActive(usable);
        }

        if (!usable)
        {
            sparks = 0;
            return;
        }

        int owned = MaterialPool.For(player).Have(settings.PortalSpark);
        string added = sparks > 0 ? $"  <color={Changed}>+{RaidStats.FormatTime(sparks * settings.SecondsPerSpark)}</color>" : "";
        sparksLabel.text = $"{settings.PortalSpark.DisplayName}: {sparks}/{settings.MaxSparksPerRaid}{added}   <color=#AAB0BC>(you have {owned})</color>";
        fewerSparksButton.interactable = sparks > 0;
        moreSparksButton.interactable = sparks < settings.MaxSparksPerRaid && sparks < owned;
    }

    // Rules that the picked modifiers change are highlighted.
    private static void AppendRule(StringBuilder builder, string label, string value, RaidDefinition raid, RaidParameters rules, RaidStat stat)
    {
        if (builder.Length > 0)
        {
            builder.Append("     ");
        }

        bool changed = !Mathf.Approximately(raid.GetBase(stat), rules.Get(stat));
        builder.Append($"<color=#AAB0BC>{label}</color> ").Append(changed ? $"<color={Changed}>{value}</color>" : value);
    }

    private string DescribeRisk()
    {
        int items = 0;
        int resources = 0;
        if (player != null)
        {
            PlayerSnapshot carried = PlayerSnapshot.Capture(player);
            foreach (KeyValuePair<ItemDefinition, int> entry in carried.CountItems())
            {
                items += entry.Value;
            }

            foreach (KeyValuePair<ResourceDefinition, int> entry in carried.CountResources())
            {
                resources += entry.Value;
            }
        }

        string carrying = items == 0 && resources == 0
            ? "You carry nothing that can be lost."
            : $"You carry {items} item{(items == 1 ? "" : "s")} and {resources} resource{(resources == 1 ? "" : "s")}. Leave what you can't risk in storage.";
        string cart = PlayerTransport.Current != null ? PlayerTransport.Current.RaidWarning() : "";
        return $"<color={Warning}>{lossRules}</color>\n{carrying}" + (cart.Length > 0 ? $"\n<color={Warning}>{cart}</color>" : "");
    }

    private void Enter()
    {
        if (portal == null)
        {
            return;
        }

        if (RaidLauncher.TryLaunch(Destination, player, picked, out string problem, sparks, pendingSeed))
        {
            Close();
        }
        else
        {
            GameMessages.Post(problem, MessageKind.Warning);
            RefreshRules();
        }
    }
}
