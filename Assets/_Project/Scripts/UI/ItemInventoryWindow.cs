using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The bag window (B or I): the grid of item slots and the carried essences (CarriedEssencesView) on one side and a RecipeBrowser on the other. Drag a stack to move it
/// (onto the same item to merge, onto another item to swap, into the safe pocket to protect it), Shift-click to split a
/// stack in half, right-click to use a consumable or equip gear and Ctrl+right-click to drop a stack. Stacks can also be
/// dragged to and from the character screen's gear slots (see ItemDrag). Locked slots (past what the
/// carried backpack unlocks) are shown dark. The browser makes what can be made by hand, and lists the recipes made at
/// stations to plan with. Everything shown comes from the player's ItemInventory, ResourceInventory and PlayerCrafting.
/// </summary>
public sealed class ItemInventoryWindow : MonoBehaviour, IItemSlotHandler
{
    private const float RecipeRefreshInterval = 0.4f;

    [Tooltip("Left empty, the first ItemInventory in the scene is used.")]
    [SerializeField] private ItemInventory inventory;

    [Header("Bag")]
    [Tooltip("Parent for the slots. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform grid;
    [Tooltip("Parent for the safe pocket's slots. Should have a GridLayoutGroup. Optional: without it the pocket isn't shown.")]
    [SerializeField] private RectTransform pocketGrid;
    [SerializeField] private ItemSlotView slotPrefab;
    [SerializeField] private ItemTooltipView tooltip;
    [Tooltip("Follows the pointer while a stack is dragged. Should not block raycasts.")]
    [SerializeField] private Image dragIcon;
    [Tooltip("Shows the player's tool tiers and carry bonus.")]
    [SerializeField] private TMP_Text gatheringLabel;

    [Header("Crafting")]
    [SerializeField] private RecipeBrowser recipes;
    [Tooltip("Where buildings come from, and how many recipes are still to be learned.")]
    [SerializeField] private TMP_Text recipeFooter;

    [Header("Layout")]
    [Tooltip("Gap (canvas units) between the character screen (C) and this window while both are open. The window moves right of the character screen and shrinks if the screen is too narrow.")]
    [SerializeField] private float characterScreenGap = 16f;

    private readonly List<ItemSlotView> slots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> pocketSlots = new List<ItemSlotView>();
    private ItemContainer container;
    private ItemContainer pocket;
    private ResourceStorage storage;
    private PlayerCrafting crafting;
    private BackpackCapacity backpack;
    private int dragFrom = -1;
    private ItemSlotView hovered;
    private float nextRecipeRefresh;
    private bool recipesDirty = true;
    private PlayerEquipment equipment;
    private RectTransform panel;
    private Vector2 homePosition;
    private bool homeKnown;
    private RectTransform characterPanel;
    private readonly Vector3[] corners = new Vector3[4];

    private void OnEnable()
    {
        inventory = inventory != null ? inventory : Player.Current != null ? Player.Current.Bag : null;
        if (inventory == null || grid == null || slotPrefab == null || tooltip == null || dragIcon == null || recipes == null)
        {
            Debug.LogError("ItemInventoryWindow is missing a reference (inventory, grid, slot prefab, tooltip, drag icon or recipe browser).", this);
            enabled = false;
            return;
        }

        container = inventory.Container;
        pocket = pocketGrid != null ? inventory.SafePocket : null;
        crafting = inventory.GetComponent<PlayerCrafting>();
        backpack = inventory.GetComponent<BackpackCapacity>();
        equipment = inventory.GetComponent<PlayerEquipment>();
        if (equipment != null)
        {
            equipment.Changed += OnEquipmentChanged;
        }
        var resources = inventory.GetComponent<ResourceInventory>();
        storage = resources != null ? resources.Storage : null;

        container.SlotChanged += OnSlotChanged;
        container.LayoutChanged += OnLayoutChanged;
        container.WeightChanged += RefreshGathering;
        if (pocket != null)
        {
            pocket.SlotChanged += OnPocketSlotChanged;
        }

        if (storage != null)
        {
            storage.Changed += OnResourcesChanged;
        }

        if (crafting != null)
        {
            crafting.KnownRecipesChanged += RebuildRecipes;
        }

        BuildSlots();
        BuildPocketSlots();
        RebuildRecipes();
        RefreshGathering();
        dragIcon.gameObject.SetActive(false);
        tooltip.Hide();
    }

    private void OnDisable()
    {
        if (container != null)
        {
            container.SlotChanged -= OnSlotChanged;
            container.LayoutChanged -= OnLayoutChanged;
            container.WeightChanged -= RefreshGathering;
        }

        if (pocket != null)
        {
            pocket.SlotChanged -= OnPocketSlotChanged;
        }

        if (storage != null)
        {
            storage.Changed -= OnResourcesChanged;
        }

        if (crafting != null)
        {
            crafting.KnownRecipesChanged -= RebuildRecipes;
        }

        if (equipment != null)
        {
            equipment.Changed -= OnEquipmentChanged;
        }

        CancelDrag();
        hovered = null;
        tooltip?.Hide();
    }

    private void Update()
    {
        UpdateLayout();
        Vector2 pointer = GameInput.PointerPosition;
        tooltip.Follow(pointer);

        // Recipes depend on what is carried, so they are rechecked now and then while the window is open.
        if (recipesDirty || Time.unscaledTime >= nextRecipeRefresh)
        {
            RefreshRecipes();
        }
    }

    private void UpdateLayout()
    {
        if (!homeKnown)
        {
            panel = (RectTransform)transform;
            homePosition = panel.anchoredPosition;
            homeKnown = true;
        }

        bool sideBySide = UIStateManager.Instance != null && UIStateManager.Instance.IsOpen(UIWindowId.Character);
        if (sideBySide && characterPanel == null)
        {
            CharacterWindow character = UIStateManager.Instance.Find<CharacterWindow>(UIWindowId.Character);
            characterPanel = character != null ? (RectTransform)character.transform : null;
        }

        if (!sideBySide || characterPanel == null || !characterPanel.gameObject.activeInHierarchy)
        {
            panel.anchoredPosition = homePosition;
            panel.localScale = Vector3.one;
            return;
        }

        // Both canvases are screen-space overlay, so world corners are screen pixels.
        Canvas root = panel.GetComponentInParent<Canvas>().rootCanvas;
        float pixelsPerUnit = root.scaleFactor;
        characterPanel.GetWorldCorners(corners);
        float left = corners[2].x + characterScreenGap * pixelsPerUnit;
        float available = Screen.width - characterScreenGap * pixelsPerUnit - left;
        float widthPixels = panel.rect.width * pixelsPerUnit;
        float scale = Mathf.Clamp(available / widthPixels, 0.5f, 1f);
        panel.localScale = new Vector3(scale, scale, 1f);

        float centerX = left + widthPixels * scale * panel.pivot.x;
        float rootCenterX = Screen.width * 0.5f;
        panel.anchoredPosition = new Vector2((centerX - rootCenterX) / pixelsPerUnit, homePosition.y);
    }

    // ----- Slots -----

    public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
    {
        if (eventData.dragging)
        {
            return;
        }

        ItemContainer owner = ContainerOf(slot);
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (GameInput.DiscardModifier.IsPressed())
            {
                inventory.Drop(owner, slot.Index);
            }
            else
            {
                inventory.Use(owner, slot.Index);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left && GameInput.SplitModifier.IsPressed())
        {
            if (!owner.SplitHalf(slot.Index) && owner.GetSlot(slot.Index).Count > 1)
            {
                GameMessages.Post("No empty slot to split into.", MessageKind.Warning);
            }
        }
    }

    public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
    {
        ItemContainer owner = ContainerOf(slot);
        ItemStack stack = owner.GetSlot(slot.Index);
        if (eventData.button != PointerEventData.InputButton.Left || stack.IsEmpty)
        {
            return;
        }

        dragFrom = slot.Index;
        ItemDrag.Begin(owner, slot.Index);
        dragIcon.sprite = stack.Item.Icon;
        dragIcon.gameObject.SetActive(stack.Item.Icon != null);
        dragIcon.transform.SetAsLastSibling();
        tooltip.Hide();
        OnSlotDragged(eventData);
    }

    public void OnSlotDragged(PointerEventData eventData)
    {
        if (dragFrom < 0)
        {
            return;
        }

        var area = (RectTransform)dragIcon.rectTransform.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local);
        dragIcon.rectTransform.anchoredPosition = local;
    }

    public void OnSlotDropped(ItemSlotView target)
    {
        ItemDrag.DropOn(ContainerOf(target), target.Index);
    }

    public void OnSlotDragEnded()
    {
        CancelDrag();
    }

    public void OnSlotHovered(ItemSlotView slot)
    {
        hovered = slot;
        if (dragFrom < 0)
        {
            tooltip.Show(ContainerOf(slot).GetSlot(slot.Index));
        }
    }

    public void OnSlotUnhovered(ItemSlotView slot)
    {
        if (hovered == slot)
        {
            hovered = null;
            tooltip.Hide();
        }
    }

    private ItemContainer ContainerOf(ItemSlotView slot)
    {
        return pocket != null && pocketSlots.Contains(slot) ? pocket : container;
    }

    private void CancelDrag()
    {
        if (dragFrom >= 0)
        {
            ItemDrag.End();
        }

        dragFrom = -1;
        if (dragIcon != null)
        {
            dragIcon.gameObject.SetActive(false);
        }
    }

    private void BuildSlots()
    {
        BuildGrid(grid, container, slots, "Slot ");
        ShowAll(container, slots);
    }

    private void BuildPocketSlots()
    {
        if (pocketGrid == null)
        {
            return;
        }

        pocketGrid.gameObject.SetActive(pocket != null);
        if (pocket != null)
        {
            BuildGrid(pocketGrid, pocket, pocketSlots, "Pocket ");
            ShowAll(pocket, pocketSlots);
        }
    }

    private void BuildGrid(RectTransform root, ItemContainer source, List<ItemSlotView> views, string prefix)
    {
        if (views.Count == source.SlotCount)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }

        views.Clear();
        var layout = root.GetComponent<GridLayoutGroup>();
        if (layout != null)
        {
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = source.Columns;
        }

        for (int i = 0; i < source.SlotCount; i++)
        {
            ItemSlotView slot = Instantiate(slotPrefab, root);
            slot.name = prefix + i;
            slot.Bind(this, i);
            views.Add(slot);
        }
    }

    private void ShowAll(ItemContainer source, List<ItemSlotView> views)
    {
        for (int i = 0; i < views.Count; i++)
        {
            views[i].Show(source.GetSlot(i), !source.IsUnlocked(i), false);
        }
    }

    // Tool tiers come from the tool belt.
    private void OnEquipmentChanged()
    {
        if (container != null)
        {
            RefreshGathering();
        }
    }

    private void OnSlotChanged(int index) => RefreshSlot(container, slots, index);

    private void OnPocketSlotChanged(int index) => RefreshSlot(pocket, pocketSlots, index);

    private void RefreshSlot(ItemContainer source, List<ItemSlotView> views, int index)
    {
        if (index >= 0 && index < views.Count)
        {
            views[index].Show(source.GetSlot(index), !source.IsUnlocked(index), false);
            if (hovered == views[index] && dragFrom < 0)
            {
                tooltip.Show(source.GetSlot(index));
            }
        }

        recipesDirty = true;
        RefreshGathering();
    }

    private void OnLayoutChanged()
    {
        ShowAll(container, slots);
        RefreshGathering();
    }

    private void OnResourcesChanged(ResourceChange change)
    {
        recipesDirty = true;
    }

    private void RefreshGathering()
    {
        if (gatheringLabel == null)
        {
            return;
        }

        var builder = new StringBuilder("Gathering:  ");
        ToolType[] tools = { ToolType.Axe, ToolType.Pickaxe, ToolType.Sickle };
        for (int i = 0; i < tools.Length; i++)
        {
            int tier = ItemInventory.GatheringTier(inventory.gameObject, tools[i]);
            builder.Append(i > 0 ? "   " : "").Append(tools[i]).Append(' ')
                .Append($"<color=#{ColorUtility.ToHtmlStringRGB(Tiers.ColorOf(tier))}>{Tiers.ToRoman(tier)}</color>");
        }

        int used = 0;
        for (int i = 0; i < container.UnlockedSlots; i++)
        {
            if (!container.GetSlot(i).IsEmpty)
            {
                used++;
            }
        }

        string bagColor = used >= container.UnlockedSlots ? "#FF7B6B" : "#AAB0BC";
        builder.Append($"     <color={bagColor}>Bag {used}/{container.UnlockedSlots}</color>");
        if (container.HasWeightLimit)
        {
            float weight = container.CurrentWeight;
            float budget = container.WeightCapacity;
            string weightColor = weight >= budget - 0.01f ? "#FF7B6B" : weight >= budget * 0.8f ? "#E8C46A" : "#AAB0BC";
            builder.Append($"     <color={weightColor}>Weight {weight:0.#}/{budget:0}</color>");
        }

        gatheringLabel.text = builder.ToString();
    }

    // ----- Crafting -----

    private void RebuildRecipes()
    {
        if (crafting == null)
        {
            SetFooter("No crafting on this character.");
            return;
        }

        var byHand = new List<RecipeDefinition>();
        var atStations = new List<RecipeDefinition>();
        foreach (RecipeDefinition recipe in crafting.Recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            if (!crafting.IsKnown(recipe))
            {
                continue;
            }

            if (recipe.IsHandcrafted)
            {
                byHand.Add(recipe);
            }
            else if (crafting.IsNearStation(recipe.Station))
            {
                atStations.Add(recipe);
            }
        }

        byHand.Sort(RecipeBook.CompareForDisplay);
        atStations.Sort(RecipeBook.CompareForDisplay);
        recipes.Show(new HandMaker(crafting), byHand, atStations);
        SetFooter($"Double-click or right-click a recipe to craft one. Buildings: build menu ({GameInput.KeyLabel(GameInput.Build)}).");
        recipesDirty = true;
    }

    private void RefreshRecipes()
    {
        recipesDirty = false;
        nextRecipeRefresh = Time.unscaledTime + RecipeRefreshInterval;
        recipes.Refresh();
    }

    private void SetFooter(string text)
    {
        if (recipeFooter != null)
        {
            recipeFooter.text = text;
        }
    }
}
