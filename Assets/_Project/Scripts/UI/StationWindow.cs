using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The crafting window opened at a crafting station (Construction Table, Furnace, Forge...). It shows:
/// <list type="bullet">
/// <item>A RecipeBrowser over the station's known recipes, to choose and queue them.</item>
/// <item>The queue, with a progress bar for the craft being worked on.</item>
/// <item>The output slot, which holds one kind at a time. Clicking it collects.</item>
/// <item>The station's tier, who works here and what waits for a higher tier.</item>
/// <item>The station's extra actions (IStationMenuExtension: "Upgrade…", "Repair gear…", "Plan a raid") as buttons.</item>
/// </list>
/// Hovering a queued craft or the output shows its stats and description in the tooltip.
/// <para>
/// It only reads the station's StationQueue and asks StationCrafting to queue, cancel and collect; the rules live there.
/// It closes when the player walks away. Where it belongs: one StationWindow prefab (Prefabs/UI/StationWindow, built by
/// Safe Haven > UI > Build Station Window) under Screen Canvas in Safe Haven. Without one, stations fall back to the
/// text menu (StationMenu).
/// </para>
/// </summary>
public sealed class StationWindow : MonoBehaviour
{
    // One grid of ItemSlotViews. Reports clicks and hovers by cell index.
    private sealed class SlotGrid : IItemSlotHandler
    {
        private readonly RectTransform root;
        private readonly ItemSlotView prefab;
        private readonly Action<int, PointerEventData> clicked;
        private readonly Action<SlotGrid, int> hovered;
        private readonly Action<SlotGrid, int> unhovered;

        public SlotGrid(RectTransform root, ItemSlotView prefab, Action<int, PointerEventData> clicked,
            Action<SlotGrid, int> hovered, Action<SlotGrid, int> unhovered)
        {
            this.root = root;
            this.prefab = prefab;
            this.clicked = clicked;
            this.hovered = hovered;
            this.unhovered = unhovered;
        }

        public List<ItemSlotView> Views { get; } = new List<ItemSlotView>();

        public void Resize(int count)
        {
            while (Views.Count < count)
            {
                ItemSlotView view = Instantiate(prefab, root);
                view.name = "Cell " + Views.Count;
                view.Bind(this, Views.Count);
                Views.Add(view);
            }

            for (int i = 0; i < Views.Count; i++)
            {
                Views[i].gameObject.SetActive(i < count);
            }
        }

        public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
        {
            if (!eventData.dragging)
            {
                clicked?.Invoke(slot.Index, eventData);
            }
        }

        public void OnSlotHovered(ItemSlotView slot) => hovered?.Invoke(this, slot.Index);

        public void OnSlotUnhovered(ItemSlotView slot) => unhovered?.Invoke(this, slot.Index);

        public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
        {
        }

        public void OnSlotDragged(PointerEventData eventData)
        {
        }

        public void OnSlotDropped(ItemSlotView target)
        {
        }

        public void OnSlotDragEnded()
        {
        }
    }

    private const float OutputGap = 14f;

    private static readonly Color PlainTint = new Color(0.3f, 0.31f, 0.36f, 1f);

    [SerializeField] private UIWindow window;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private ItemTooltipView tooltip;
    [SerializeField] private ItemSlotView slotPrefab;
    [SerializeField] private RecipeBrowser recipes;

    [Header("Queue")]
    [Tooltip("The queue's heading: how full it is and when it will all be done.")]
    [SerializeField] private TMP_Text queueLabel;
    [Tooltip("Parent for the queued crafts. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform queueGrid;
    [Tooltip("The bar's fill; its right anchor follows the craft's progress.")]
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Button cancelButton;

    [Header("Output")]
    [Tooltip("Heading, cells and button together. Sits under the queue's last row, however many rows it has.")]
    [SerializeField] private RectTransform outputGroup;
    [Tooltip("Parent for the output cells. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform outputGrid;
    [SerializeField, Min(1)] private int outputCells = 1;
    [SerializeField] private TMP_Text outputLabel;
    [SerializeField] private Button collectButton;

    [Header("This station")]
    [Tooltip("The station's tier, who works here and what waits for a higher tier.")]
    [SerializeField] private TMP_Text stationLabel;

    [Header("Station actions")]
    [Tooltip("Parent for the station's extra actions (Repair gear, Plan a raid). Should have a horizontal layout group.")]
    [SerializeField] private RectTransform actionBar;
    [Tooltip("A hidden button with a TMP label, copied for each action.")]
    [SerializeField] private Button actionTemplate;

    [Tooltip("Seconds between refreshes of costs, queue and output while open. The progress bar moves every frame.")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private readonly List<Button> actionButtons = new List<Button>();
    private readonly List<MenuAction> actions = new List<MenuAction>();
    private SlotGrid queued;
    private SlotGrid output;
    private CraftingStation station;
    private GameObject user;
    private PlayerCrafting crafting;
    private float range;
    private int knownCount = -1;
    private float nextRefresh;
    private SlotGrid hoveredGrid;
    private int hoveredIndex = -1;
    private int[] outputAmounts = new int[0];
    private Image progressImage;
    private TMP_Text collectLabel;
    // Opened from the Keeper's view: no collecting into the bag from afar.
    private bool fromAfar;
    private UIRole collectRole;
    private bool anyoneCouldWorkHere;

    private void Awake()
    {
        queued = new SlotGrid(queueGrid, slotPrefab, null, OnHovered, OnUnhovered);
        output = new SlotGrid(outputGrid, slotPrefab, (index, data) => Collect(), OnHovered, OnUnhovered);
        progressImage = progressFill.GetComponent<Image>();
        collectLabel = collectButton.GetComponentInChildren<TMP_Text>();
        collectRole = collectButton.GetComponent<UIRole>();
        actionTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
        cancelButton.onClick.AddListener(CancelLast);
        collectButton.onClick.AddListener(Collect);
        recipes.Made += Refresh;
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        recipes.Made -= Refresh;
        window.OpenChanged -= OnOpenChanged;
        PadTabs.Unregister(this);
    }

    public bool IsOpen => window != null && window.IsOpen;

    /// <summary>Opens the window for a station. It closes when the user is further than maxDistance meters from it.</summary>
    public void Open(CraftingStation source, GameObject viewer, float maxDistance) => Open(source, viewer, maxDistance, false);

    /// <summary>
    /// Opens it from the Keeper's view (<paramref name="afar"/>): at any distance, to queue and watch. What is made stays in the
    /// station's output until someone collects it there: taken into the bag from across the Haven it would arrive by magic.
    /// </summary>
    public void Open(CraftingStation source, GameObject viewer, float maxDistance, bool afar)
    {
        station = source;
        user = viewer;
        fromAfar = afar;
        shownOutput = source.Queue.OutputAmount;
        crafting = viewer != null ? viewer.GetComponent<PlayerCrafting>() : null;
        range = maxDistance;
        titleLabel.text = source.Type.DisplayName();
        anyoneCouldWorkHere = HasAJob(source);
        knownCount = -1;
        hoveredGrid = null;
        tooltip.Hide();
        window.Open();
        ShowBanner(source.Type);
        BuildTabs();
        ShowUpgrade(false);
        Refresh();
    }

    // ----- The Upgrade tab (the user, 2026-11-21: "Add an Upgrade tab to the buildings. This is where we can upgrade to the next tier
    // or add modular improvements") -----

    private const float TabWidth = 150f;
    private const float TabHeight = 36f;

    private readonly List<GameObject> craftParts = new List<GameObject>();
    private RectTransform tabsRow;
    private Button craftTab;
    private Button upgradeTab;
    private TMP_Text craftTabLabel;
    private TMP_Text upgradeTabLabel;
    private Image craftTabShown;
    private Image upgradeTabShown;
    private RectTransform upgradePanel;
    private StationUpgradeTab upgradeView;
    private bool showingUpgrade;

    /// <summary>True while the Upgrade tab is shown.</summary>
    public bool ShowingUpgrade => showingUpgrade;

    // Made once, after the banner has moved the craft parts down: the tabs on the band's foot, the upgrade page where the craft parts are.
    private void BuildTabs()
    {
        var panel = titleLabel.transform.parent as RectTransform;
        if (panel == null)
        {
            return;
        }

        if (tabsRow == null)
        {
            UIStyle style = UIStyle.Current;
            KitBanner banner = panel.GetComponent<KitBanner>();
            float top = (banner != null ? banner.Growth : 0f) + 16f;
            foreach (Transform child in panel)
            {
                var rect = (RectTransform)child;
                bool overlay = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
                bool topAnchored = Mathf.Approximately(rect.anchorMin.y, 1f) && Mathf.Approximately(rect.anchorMax.y, 1f);
                bool own = child == titleLabel.transform || child == closeButton.transform || child == actionBar || child == tooltip.transform
                           || child.GetComponent<KitBanner>() != null || child.name == "Banner";
                if (!overlay && topAnchored && !own && -rect.anchoredPosition.y > top + TabHeight)
                {
                    craftParts.Add(child.gameObject);
                }
            }

            tabsRow = CodeUI.At("Station Tabs", panel, 24f, top, TabWidth * 2f + 8f, TabHeight);
            craftTab = CodeUI.AddTab("Craft", tabsRow, 0f, 0f, TabWidth, TabHeight, style, out craftTabLabel, out craftTabShown);
            craftTabLabel.text = "Craft";
            craftTab.onClick.AddListener(() => ShowUpgrade(false));
            upgradeTab = CodeUI.AddTab("Upgrade", tabsRow, TabWidth + 8f, 0f, TabWidth, TabHeight, style, out upgradeTabLabel, out upgradeTabShown);
            upgradeTabLabel.text = "Upgrade";
            upgradeTab.onClick.AddListener(() => ShowUpgrade(true));

            float listTop = top + TabHeight + 14f;
            float width = panel.rect.width - 48f;
            float height = panel.rect.height - listTop - 24f;
            upgradePanel = CodeUI.At("Upgrade Page", panel, 24f, listTop, width, height);
            upgradeView = new StationUpgradeTab(upgradePanel, RefreshUpgrade);
            upgradePanel.gameObject.SetActive(false);
            PadTabs.Register(this, () => IsOpen && showingUpgrade, direction => ShowUpgrade(!showingUpgrade), () => new[] { tabsRow });
        }

        bool has = station != null && station.TryGetComponent(out BuildingObject building) && building.TryGetComponent(out Construction construction)
                   && ConstructionUpgradeOffer.HasChoices(building, construction);
        tabsRow.gameObject.SetActive(has);
        PlaceTabs(panel);
    }

    // Where the craft parts start now (the banner moves them when a station has a painting): the tabs just above them, the page in their
    // place. A station with no painting (the Farm, the Dock) has no band to hold the tabs, so they stand beside its title.
    private void PlaceTabs(RectTransform panel)
    {
        float contentTop = float.MaxValue;
        foreach (GameObject part in craftParts)
        {
            if (part != null)
            {
                contentTop = Mathf.Min(contentTop, -((RectTransform)part.transform).anchoredPosition.y);
            }
        }

        if (contentTop == float.MaxValue)
        {
            return;
        }

        float tabsTop = contentTop - TabHeight - 10f;
        float tabsX = 24f;
        if (tabsTop < 60f)
        {
            // No painting: the tabs beside the title, and the page below the title's large letters.
            tabsTop = 18f;
            tabsX = 24f + Mathf.Min(titleLabel.preferredWidth, 420f) + 28f;
            contentTop += 34f;
        }

        tabsRow.anchoredPosition = new Vector2(tabsX, -tabsTop);
        float height = panel.rect.height - contentTop - 24f;
        upgradePanel.anchoredPosition = new Vector2(24f, -contentTop);
        upgradePanel.sizeDelta = new Vector2(panel.rect.width - 48f, height);
    }

    private void ShowUpgrade(bool upgrade)
    {
        showingUpgrade = upgrade && tabsRow != null && tabsRow.gameObject.activeSelf;
        foreach (GameObject part in craftParts)
        {
            if (part != null)
            {
                part.SetActive(!showingUpgrade);
            }
        }

        if (upgradePanel != null)
        {
            upgradePanel.gameObject.SetActive(showingUpgrade);
        }

        if (craftTabShown != null)
        {
            UIStyle style = UIStyle.Current;
            craftTabShown.enabled = !showingUpgrade;
            upgradeTabShown.enabled = showingUpgrade;
            craftTabLabel.color = showingUpgrade ? style.Muted : style.Accent;
            upgradeTabLabel.color = showingUpgrade ? style.Accent : style.Muted;
        }

        tooltip.Hide();
        if (showingUpgrade)
        {
            RefreshUpgrade();
        }
    }

    // The next tier and the modules, drawn by the tab from the building's own rules (StationUpgradeTab).
    private void RefreshUpgrade()
    {
        if (!showingUpgrade || station == null || upgradeView == null || !IsOpen)
        {
            return;
        }

        upgradeView.Show(station.GetComponent<BuildingObject>(), user);
    }

    public void Close()
    {
        window.Close();
    }

    // Hearthline 3: the station's painting across the window's top (KitBanner, Resources/WindowArt/<type>), the title
    // and the station's buttons on it; the window shrinks to fit where the screen is short.
    private const float BannerHeight = 170f;

    private void ShowBanner(CraftingStationType type)
    {
        var panel = titleLabel.transform.parent as RectTransform;
        Canvas canvas = titleLabel.canvas != null ? titleLabel.canvas.rootCanvas : null;
        if (panel == null || canvas == null)
        {
            return;
        }

        KitBanner.Show(panel, type.ToString(), BannerHeight, 40f);
        // Its name large on the painting.
        titleLabel.fontSize = 44f;
        titleLabel.overflowMode = TMPro.TextOverflowModes.Overflow;
        titleLabel.rectTransform.anchoredPosition = new Vector2(30f, -22f);
        titleLabel.rectTransform.sizeDelta = new Vector2(600f, 60f);
        Vector2 size = panel.rect.size;
        HearthKit.FitToScreen(panel, (RectTransform)canvas.transform, size.x, size.y, 30f);
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            station = null;
            user = null;
            crafting = null;
            hoveredGrid = null;
            tooltip.Hide();
        }
    }

    // What the output slot held last frame, to notice a craft finishing.
    private int shownOutput = -1;

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        // Opened from the Keeper's view, it goes with it: back on their feet the player collects at the station as ever.
        if (station == null || !station.Usable || user == null || (fromAfar && !KeeperView.IsOpen)
            || HorizontalDistance(user.transform.position, station.transform.position) > range)
        {
            Close();
            return;
        }

        StationQueue queue = station.Queue;
        float fraction = queue.Current != null ? Mathf.Clamp01(queue.Progress / queue.Current.CraftSeconds) : 0f;
        progressFill.anchorMax = new Vector2(queue.Blocked ? 1f : fraction, 1f);
        if (progressImage != null)
        {
            // The bar warms towards the accent as the craft nears done, and breathes red when the output is full.
            UIStyle style = UIStyle.Current;
            progressImage.color = queue.Blocked ? Color.Lerp(style.Danger, Color.white, UIMotion.Breath(1.2f) * 0.3f)
                : Color.Lerp(style.Good, style.Accent, fraction * fraction);
        }

        // A craft finished while watching: the output slot jumps and the smith's taps are heard.
        if (queue.OutputAmount > shownOutput)
        {
            UIMotion.Punch(outputGrid, 0.15f, 0.4f);
            if (progressImage != null)
            {
                UIMotion.Flash(progressImage, Color.white, 0.4f);
            }

            UIMotion.Sound(AudioManager.Sounds != null ? AudioManager.Sounds.Crafted : null);
        }

        shownOutput = queue.OutputAmount;

        tooltip.Follow(GameInput.PointerPosition);

        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    /// <summary>Redraws everything from the station's queue and the player's known recipes and materials.</summary>
    public void Refresh()
    {
        nextRefresh = Time.unscaledTime + refreshInterval;
        if (station == null)
        {
            return;
        }

        if (showingUpgrade)
        {
            RefreshUpgrade();
            return;
        }

        StationQueue queue = station.Queue;
        List<RecipeDefinition> known = StationCrafting.KnownRecipesFor(user, station.Type, queue);
        known.Sort(RecipeBook.CompareForDisplay);
        // Which recipes, not only how many: a module fitted swaps a recipe for its thriftier variant and the count stays.
        int mark = known.Count;
        foreach (RecipeDefinition recipe in known)
        {
            mark = unchecked(mark * 31 + recipe.Id.GetHashCode());
        }

        if (mark != knownCount)
        {
            knownCount = mark;
            recipes.Show(new StationMaker(user, queue), known);
        }
        else
        {
            recipes.Refresh();
        }

        RefreshQueue(queue);
        RefreshOutput(queue);
        stationLabel.text = DescribeStation(known);
        RefreshActions();
        RefreshTooltip();
    }

    // ----- Queue and output -----

    private void RefreshQueue(StationQueue queue)
    {
        IReadOnlyList<StationQueue.Job> jobs = queue.Jobs;
        queued.Resize(Mathf.Max(queue.MaxQueued, jobs.Count));
        for (int i = 0; i < queued.Views.Count; i++)
        {
            if (i < jobs.Count)
            {
                RecipeDefinition recipe = jobs[i].Recipe;
                string badge = jobs[i].Count > 1 ? "×" + jobs[i].Count : "";
                queued.Views[i].Show(recipe.OutputIcon, i == 0 ? UIStyle.Current.Accent : TintOf(recipe.OutputDefinition), "", badge);
            }
            else
            {
                queued.Views[i].Show(null, PlainTint);
            }
        }

        queueLabel.text = queue.Current == null
            ? $"Queue  <color=#{Muted}>empty (holds {queue.MaxQueued})</color>"
            : $"Queue  <color=#{Muted}>{queue.QueuedCount}/{queue.MaxQueued}, {ItemTooltipView.FormatSeconds(queue.TotalRemaining)} in all</color>";
        if (queue.Current == null)
        {
            progressLabel.text = "Idle: queue a recipe";
        }
        else if (queue.Blocked)
        {
            progressLabel.text = $"<b>{queue.Current.DisplayName}</b> waits: {queue.WaitingFor}";
        }
        else
        {
            progressLabel.text = $"<b>{queue.Current.DisplayName}</b>  ·  {ItemTooltipView.FormatSeconds(queue.CurrentRemaining)} left";
        }

        // The host's queue in a friend's Haven: greyed, as StationCrafting refuses it (HavenVisit's one rule), not lit and refused.
        cancelButton.interactable = queue.CanCancelLast && !HavenVisit.IsHostsToDecide;
        PlaceOutputUnderQueue();
    }

    private void PlaceOutputUnderQueue()
    {
        var layout = queueGrid.GetComponent<GridLayoutGroup>();
        int rows = Mathf.CeilToInt(queued.Views.Count / (float)Mathf.Max(1, layout.constraintCount));
        float height = rows * layout.cellSize.y + Mathf.Max(0, rows - 1) * layout.spacing.y;
        outputGroup.anchoredPosition = new Vector2(outputGroup.anchoredPosition.x, queueGrid.anchoredPosition.y - height - OutputGap);
    }

    private void CancelLast()
    {
        if (station != null)
        {
            StationCrafting.CancelLast(user, station.Queue);
            Refresh();
        }
    }

    private void RefreshOutput(StationQueue queue)
    {
        UnityEngine.Object thing = queue.Output;
        int stackSize = Mathf.Max(1, queue.Capacity);
        if (thing is IStackDefinition stack)
        {
            stackSize = Mathf.Min(stack.StackSize, stackSize);
        }

        int[] amounts = RecipeTabs.SplitIntoCells(thing != null ? queue.OutputAmount : 0, stackSize, outputCells);
        outputAmounts = amounts;
        output.Resize(amounts.Length);
        Sprite icon = IconOf(thing);
        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] > 0)
            {
                output.Views[i].Show(icon, TintOf(thing), "", amounts[i] > 1 ? amounts[i].ToString() : "");
            }
            else
            {
                output.Views[i].Show(null, PlainTint);
            }
        }

        string name = thing is IDisplayName named ? named.DisplayName : "";
        outputLabel.text = thing != null
            ? $"Output  <color=#{Muted}>{queue.OutputAmount}/{queue.Capacity} {name}</color>"
            : $"Output  <color=#{Muted}>empty (holds {queue.Capacity})</color>";
        collectButton.interactable = thing != null && !HavenVisit.IsHostsToDecide && !fromAfar;
        collectLabel.text = thing == null ? "Nothing to collect"
            : fromAfar ? $"{queue.OutputAmount} {name} waiting here"
            : HavenVisit.IsHostsToDecide ? $"{(string.IsNullOrEmpty(HavenVisit.HostName) ? "Your friend" : HavenVisit.HostName)}'s to collect" : $"Collect {queue.OutputAmount} {name}";
        // While it holds the queue up, collecting is the one thing to do.
        UIRole.Kind role = queue.Blocked ? UIRole.Kind.PrimaryButton : UIRole.Kind.Button;
        if (collectRole != null && collectRole.Role != role)
        {
            collectRole.Set(role);
            // Blending in Linear thins dark text on a light button.
            collectLabel.fontStyle = queue.Blocked ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    // ----- This station -----

    private string DescribeStation(List<RecipeDefinition> known)
    {
        var lines = new List<string>();
        int tier = station.Tier;
        if (station.TryGetComponent(out Construction construction) && construction.Definition.MaxLevel > 1)
        {
            lines.Add($"Tier {Tiers.ToRoman(tier)} of {Tiers.ToRoman(construction.Definition.MaxLevel)}");
        }

        int higher = 0;
        foreach (RecipeDefinition recipe in known)
        {
            if (recipe.RequiredStationTier > tier)
            {
                higher++;
            }
        }

        if (higher > 0)
        {
            lines.Add(higher == 1 ? "1 recipe you know needs a higher tier." : $"{higher} recipes you know need a higher tier.");
        }

        ResidentRecord worker = station.Worker;
        if (worker != null)
        {
            lines.Add($"{worker.Resident.DisplayName} works here.");
        }
        else if (anyoneCouldWorkHere)
        {
            lines.Add($"Nobody works here yet. Residents get their jobs in {GameInput.KeyLabel(GameInput.Haven)}.");
        }

        // How fast it works now: on its own, with its worker at it, and with the player at the bench (WorkerSettings).
        StationQueue now = station.Queue;
        if (now != null)
        {
            string who = now.WorkerHere && now.PlayerHere ? "with you and its worker at it" : now.WorkerHere ? "with its worker at it"
                : now.PlayerHere ? "with you at it" : "with nobody at it";
            lines.Add($"Works at {Mathf.RoundToInt(now.Speed * 100f)}% speed now, {who}.");
            string water = Fields.WaterLine(now, FarmSettings.Current);
            if (water.Length > 0)
            {
                lines.Add(water);
            }
        }

        return string.Join("\n", lines);
    }

    // Whether any resident in the game has a job here, so the window doesn't suggest a worker where none can come.
    private static bool HasAJob(CraftingStation source)
    {
        var database = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (database == null || !source.TryGetComponent(out BuildingObject building) || building.Definition == null)
        {
            return false;
        }

        foreach (ResidentDefinition resident in database.Residents)
        {
            if (resident == null)
            {
                continue;
            }

            foreach (JobDefinition job in resident.Jobs)
            {
                if (job != null && job.Workplace == building.Definition)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Collect()
    {
        // The output slot clicked in a friend's Haven: said whose it is, not shaken as if the bag were full.
        if (station != null && HavenVisit.LeftToHost())
        {
            return;
        }

        if (fromAfar)
        {
            GameMessages.Post("Collect it at the station: from the Keeper's view you can queue and watch.", MessageKind.Warning);
            return;
        }

        if (station != null)
        {
            int taken = StationCrafting.Collect(user, station.Queue);
            if (taken > 0)
            {
                UIMotion.Punch(outputGrid, 0.1f, 0.3f);
            }
            else if (station.Queue.Output != null)
            {
                UIMotion.Shake(outputGrid); // no room in the bag
            }

            shownOutput = station.Queue.OutputAmount;
            Refresh();
        }
    }

    // ----- Station actions -----

    private void RefreshActions()
    {
        var page = new MenuPage();
        bool upgradeTabShown = tabsRow != null && tabsRow.gameObject.activeSelf;
        foreach (IStationMenuExtension extension in station.GetComponents<IStationMenuExtension>())
        {
            // The Upgrade tab holds what the building's own "Upgrade…" button opened.
            if (upgradeTabShown && extension is Construction)
            {
                continue;
            }

            extension.AddActions(page, user);
        }

        actions.Clear();
        actions.AddRange(page.Actions);
        while (actionButtons.Count < actions.Count)
        {
            Button button = Instantiate(actionTemplate, actionBar);
            int index = actionButtons.Count;
            button.onClick.AddListener(() => OnActionChosen(index));
            actionButtons.Add(button);
        }

        for (int i = 0; i < actionButtons.Count; i++)
        {
            bool used = i < actions.Count;
            actionButtons[i].gameObject.SetActive(used);
            if (used)
            {
                actionButtons[i].name = actions[i].Label;
                actionButtons[i].interactable = actions[i].Enabled;
                actionButtons[i].GetComponentInChildren<TMP_Text>().text = actions[i].Label;
            }
        }

        actionBar.gameObject.SetActive(actions.Count > 0);
    }

    // Extension actions open their own windows (the repair list, the raid planner), so this one steps aside first.
    private void OnActionChosen(int index)
    {
        if (index < 0 || index >= actions.Count || !actions[index].Enabled)
        {
            return;
        }

        MenuAction action = actions[index];
        Close();
        action.Chosen?.Invoke();
    }

    // ----- Tooltip -----

    private void OnHovered(SlotGrid grid, int index)
    {
        hoveredGrid = grid;
        hoveredIndex = index;
        RefreshTooltip();
    }

    private void OnUnhovered(SlotGrid grid, int index)
    {
        if (grid == hoveredGrid && index == hoveredIndex)
        {
            hoveredGrid = null;
            hoveredIndex = -1;
            tooltip.Hide();
        }
    }

    private void RefreshTooltip()
    {
        if (hoveredGrid == null || station == null)
        {
            return;
        }

        StationQueue queue = station.Queue;
        if (hoveredGrid == queued)
        {
            if (hoveredIndex < queue.Jobs.Count)
            {
                tooltip.ShowRecipe(queue.Jobs[hoveredIndex].Recipe, crafting, null);
                return;
            }
        }
        else if (hoveredGrid == output && queue.Output != null && hoveredIndex < outputAmounts.Length && outputAmounts[hoveredIndex] > 0)
        {
            tooltip.ShowDefinition(queue.Output);
            return;
        }

        tooltip.Hide();
    }

    // ----- Helpers -----

    private static string Muted => ColorUtility.ToHtmlStringRGB(UIStyle.Current.Muted);

    private static Sprite IconOf(UnityEngine.Object definition)
    {
        return definition is IStackDefinition stack ? stack.Icon : null;
    }

    private static Color TintOf(UnityEngine.Object definition)
    {
        return definition is IStackDefinition stack && stack.SlotTint.HasValue ? stack.SlotTint.Value : PlainTint;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
