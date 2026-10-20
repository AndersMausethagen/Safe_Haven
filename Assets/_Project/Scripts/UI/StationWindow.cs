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
/// <item>The station's known recipes as an icon grid, split into tabs by what they make (RecipeTabs).</item>
/// <item>The selected recipe's costs and a Queue button.</item>
/// <item>The queue, with a progress bar for the craft being worked on.</item>
/// <item>The output slot as its own grid. Clicking it collects.</item>
/// <item>The station's extra actions (IStationMenuExtension: "Repair gear…", "Plan a raid") as buttons.</item>
/// </list>
/// Hovering a recipe, queued craft or output shows its stats and description in the tooltip.
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

    private static readonly Color SelectedTint = new Color(0.95f, 0.72f, 0.3f, 1f);
    private static readonly Color PlainTint = new Color(0.3f, 0.31f, 0.36f, 1f);
    private static readonly Color TabColor = new Color(0.3f, 0.235f, 0.17f, 1f);
    private static readonly Color ActiveTabColor = new Color(0.62f, 0.45f, 0.2f, 1f);
    private static readonly Color WorkingFill = new Color(0.33f, 0.5f, 0.22f, 1f);
    private static readonly Color BlockedFill = new Color(0.55f, 0.2f, 0.16f, 1f);

    [SerializeField] private UIWindow window;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private ItemTooltipView tooltip;
    [SerializeField] private ItemSlotView slotPrefab;

    [Header("Recipes")]
    [Tooltip("Parent for the tab buttons. Should have a horizontal layout group.")]
    [SerializeField] private RectTransform tabBar;
    [Tooltip("A hidden button with a TMP label, copied for each tab.")]
    [SerializeField] private Button tabTemplate;
    [Tooltip("Parent for the recipe cells. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform recipeGrid;
    [Tooltip("Shown instead of the grid when no recipes for this station are known.")]
    [SerializeField] private TMP_Text emptyLabel;

    [Header("Selected recipe")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [Tooltip("Costs, craft time and why it can't be queued, with rich text.")]
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private Button queueButton;

    [Header("Queue")]
    [Tooltip("Parent for the queued crafts. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform queueGrid;
    [Tooltip("The bar's fill; its right anchor follows the craft's progress.")]
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Button cancelButton;

    [Header("Output")]
    [Tooltip("Parent for the output cells. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform outputGrid;
    [SerializeField, Min(1)] private int outputCells = 8;
    [SerializeField] private TMP_Text outputLabel;
    [SerializeField] private Button collectButton;

    [Header("Station actions")]
    [Tooltip("Parent for the station's extra actions (Repair gear, Plan a raid). Should have a horizontal layout group.")]
    [SerializeField] private RectTransform actionBar;
    [Tooltip("A hidden button with a TMP label, copied for each action.")]
    [SerializeField] private Button actionTemplate;

    [Tooltip("Seconds between refreshes of costs, queue and output while open. The progress bar moves every frame.")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<Button> actionButtons = new List<Button>();
    private readonly List<MenuAction> actions = new List<MenuAction>();
    private List<RecipeTabs.Tab> tabs = new List<RecipeTabs.Tab>();
    private SlotGrid recipes;
    private SlotGrid queued;
    private SlotGrid output;
    private CraftingStation station;
    private GameObject user;
    private PlayerCrafting crafting;
    private float range;
    private string activeTab;
    private RecipeDefinition selected;
    private int knownCount = -1;
    private float nextRefresh;
    private SlotGrid hoveredGrid;
    private int hoveredIndex = -1;
    private int[] outputAmounts = new int[0];
    private Image progressImage;

    private List<RecipeDefinition> Shown => tabs.Count > 0 ? TabNamed(activeTab).Recipes : new List<RecipeDefinition>();

    private void Awake()
    {
        recipes = new SlotGrid(recipeGrid, slotPrefab, OnRecipeClicked, OnHovered, OnUnhovered);
        queued = new SlotGrid(queueGrid, slotPrefab, null, OnHovered, OnUnhovered);
        output = new SlotGrid(outputGrid, slotPrefab, (index, data) => Collect(), OnHovered, OnUnhovered);
        progressImage = progressFill.GetComponent<Image>();
        tabTemplate.gameObject.SetActive(false);
        actionTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
        queueButton.onClick.AddListener(() => Queue(selected));
        cancelButton.onClick.AddListener(CancelLast);
        collectButton.onClick.AddListener(Collect);
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        window.OpenChanged -= OnOpenChanged;
    }

    public bool IsOpen => window != null && window.IsOpen;

    /// <summary>Opens the window for a station. It closes when the user is further than maxDistance meters from it.</summary>
    public void Open(CraftingStation source, GameObject viewer, float maxDistance)
    {
        station = source;
        user = viewer;
        crafting = viewer != null ? viewer.GetComponent<PlayerCrafting>() : null;
        range = maxDistance;
        titleLabel.text = source.Type.DisplayName();
        knownCount = -1;
        activeTab = null;
        selected = null;
        hoveredGrid = null;
        tooltip.Hide();
        window.Open();
        Refresh();
    }

    public void Close()
    {
        window.Close();
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

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (station == null || !station.Usable || user == null || HorizontalDistance(user.transform.position, station.transform.position) > range)
        {
            Close();
            return;
        }

        StationQueue queue = station.Queue;
        float fraction = queue.Current != null ? Mathf.Clamp01(queue.Progress / queue.Current.CraftSeconds) : 0f;
        progressFill.anchorMax = new Vector2(queue.Blocked ? 1f : fraction, 1f);
        if (progressImage != null)
        {
            progressImage.color = queue.Blocked ? BlockedFill : WorkingFill;
        }

        if (Pointer.current != null)
        {
            tooltip.Follow(Pointer.current.position.ReadValue());
        }

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

        StationQueue queue = station.Queue;
        List<RecipeDefinition> known = StationCrafting.KnownRecipesFor(user, station.Type);
        known.Sort(RecipeBook.CompareForDisplay);
        if (known.Count != knownCount)
        {
            knownCount = known.Count;
            BuildTabs(known);
        }

        if (selected != null && !known.Contains(selected))
        {
            selected = null;
        }

        RefreshRecipes(queue);
        RefreshDetail(queue);
        RefreshQueue(queue);
        RefreshOutput(queue);
        RefreshActions();
        RefreshTooltip();
    }

    // ----- Recipes -----

    private void BuildTabs(List<RecipeDefinition> known)
    {
        tabs = RecipeTabs.Build(known);
        if (tabs.Count > 0 && TabNamed(activeTab) == null)
        {
            activeTab = tabs[0].Label;
        }

        // A single tab says nothing, so the bar only shows with two or more.
        int shownTabs = tabs.Count > 1 ? tabs.Count : 0;
        while (tabButtons.Count < shownTabs)
        {
            Button button = Instantiate(tabTemplate, tabBar);
            int index = tabButtons.Count;
            button.onClick.AddListener(() => SelectTab(index));
            tabButtons.Add(button);
        }

        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool used = i < shownTabs;
            tabButtons[i].gameObject.SetActive(used);
            if (used)
            {
                tabButtons[i].name = "Tab " + tabs[i].Label;
                tabButtons[i].GetComponentInChildren<TMP_Text>().text = $"{tabs[i].Label} <size=12>({tabs[i].Recipes.Count})</size>";
            }
        }

        tabBar.gameObject.SetActive(shownTabs > 0);
        emptyLabel.gameObject.SetActive(known.Count == 0);
        PaintTabs();
    }

    private RecipeTabs.Tab TabNamed(string label)
    {
        foreach (RecipeTabs.Tab tab in tabs)
        {
            if (tab.Label == label)
            {
                return tab;
            }
        }

        return null;
    }

    private void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Count)
        {
            return;
        }

        activeTab = tabs[index].Label;
        PaintTabs();
        Refresh();
    }

    private void PaintTabs()
    {
        for (int i = 0; i < tabButtons.Count && i < tabs.Count; i++)
        {
            if (tabButtons[i].targetGraphic != null)
            {
                tabButtons[i].targetGraphic.color = tabs[i].Label == activeTab ? ActiveTabColor : TabColor;
            }
        }
    }

    private void RefreshRecipes(StationQueue queue)
    {
        List<RecipeDefinition> shown = Shown;
        recipes.Resize(shown.Count);
        for (int i = 0; i < shown.Count; i++)
        {
            RecipeDefinition recipe = shown[i];
            bool canQueue = StationCrafting.Check(user, queue, recipe) == null;
            Color tint = recipe == selected ? SelectedTint : TintOf(recipe.OutputDefinition);
            string badge = recipe.OutputAmount > 1 ? "×" + recipe.OutputAmount : "";
            recipes.Views[i].Show(recipe.OutputIcon, tint, Tiers.ToRoman(recipe.Tier), badge, !canQueue);
        }
    }

    private void OnRecipeClicked(int index, PointerEventData eventData)
    {
        List<RecipeDefinition> shown = Shown;
        if (index < 0 || index >= shown.Count)
        {
            return;
        }

        selected = shown[index];
        // Right-click or double-click queues straight away.
        if (eventData.button == PointerEventData.InputButton.Right || eventData.clickCount >= 2)
        {
            Queue(selected);
        }
        else
        {
            Refresh();
        }
    }

    private void RefreshDetail(StationQueue queue)
    {
        bool has = selected != null;
        detailIcon.enabled = has && selected.OutputIcon != null;
        detailIcon.sprite = has ? selected.OutputIcon : null;
        queueButton.gameObject.SetActive(has);
        if (!has)
        {
            detailName.text = knownCount > 0 ? "Choose a recipe" : "";
            detailBody.text = knownCount > 0 ? "<color=#AAB0BC>Click a recipe to see what it costs. Right-click or double-click queues it.</color>" : "";
            return;
        }

        string amount = selected.OutputAmount > 1 ? $" ×{selected.OutputAmount}" : "";
        detailName.text = $"{selected.DisplayName}{amount}  <size=14>{Tiers.RichLabel(selected.Tier)}</size>";
        string problem = StationCrafting.Check(user, queue, selected);
        string body = "<b>Costs</b>" + ItemTooltipView.DescribeCosts(selected, crafting)
            + $"\n<color=#AAB0BC>Takes {ItemTooltipView.FormatSeconds(selected.CraftSeconds)}</color>";
        if (problem != null)
        {
            body += $"\n<color=#FF7B6B>{problem}</color>";
        }

        detailBody.text = body;
        queueButton.interactable = problem == null;
    }

    private void Queue(RecipeDefinition recipe)
    {
        if (station != null && recipe != null)
        {
            StationCrafting.TryQueue(user, station.Queue, recipe);
            Refresh();
        }
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
                queued.Views[i].Show(recipe.OutputIcon, i == 0 ? SelectedTint : TintOf(recipe.OutputDefinition), "", badge);
            }
            else
            {
                queued.Views[i].Show(null, PlainTint);
            }
        }

        if (queue.Current == null)
        {
            progressLabel.text = "<color=#AAB0BC>Idle – queue a recipe</color>";
        }
        else if (queue.Blocked)
        {
            progressLabel.text = $"<b>{queue.Current.DisplayName}</b> is done but the output is full – collect it";
        }
        else
        {
            progressLabel.text = $"<b>{queue.Current.DisplayName}</b>  ·  {ItemTooltipView.FormatSeconds(queue.CurrentRemaining)} left"
                + $"  ({queue.QueuedCount}/{queue.MaxQueued} queued)";
        }

        cancelButton.interactable = queue.CanCancelLast;
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
        int stackSize = thing is ItemDefinition item ? item.MaximumStack : Mathf.Max(1, queue.Capacity);
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

        string name = thing is ItemDefinition outputItem ? outputItem.DisplayName : thing is ResourceDefinition resource ? resource.DisplayName : "";
        outputLabel.text = thing != null
            ? $"Output  <color=#AAB0BC>{queue.OutputAmount}/{queue.Capacity} {name}</color>"
            : $"Output  <color=#AAB0BC>empty (holds {queue.Capacity})</color>";
        collectButton.interactable = thing != null;
    }

    private void Collect()
    {
        if (station != null)
        {
            StationCrafting.Collect(user, station.Queue);
            Refresh();
        }
    }

    // ----- Station actions -----

    private void RefreshActions()
    {
        var page = new MenuPage();
        foreach (IStationMenuExtension extension in station.GetComponents<IStationMenuExtension>())
        {
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
        if (hoveredGrid == recipes)
        {
            List<RecipeDefinition> shown = Shown;
            if (hoveredIndex < shown.Count)
            {
                tooltip.ShowRecipe(shown[hoveredIndex], crafting, StationCrafting.Check(user, queue, shown[hoveredIndex]));
                return;
            }
        }
        else if (hoveredGrid == queued)
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

    private static Sprite IconOf(UnityEngine.Object definition)
    {
        return definition is ItemDefinition item ? item.Icon : definition is ResourceDefinition resource ? resource.Icon : null;
    }

    private static Color TintOf(UnityEngine.Object definition)
    {
        if (definition is ItemDefinition item && item.Rarity != ItemRarity.Common)
        {
            return Rarity.ColorOf(item.Rarity);
        }

        return definition is ResourceDefinition resource ? resource.Color : PlainTint;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
