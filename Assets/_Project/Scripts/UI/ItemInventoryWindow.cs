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

    // The prefab's hints say what the mouse does; on a controller they say what its buttons do (the pointer is PadCursor's).
    private TMP_Text bagHint;
    private TMP_Text closeHint;
    private string bagHintForMouse;
    private string closeHintForMouse;

    private void OnEnable()
    {
        inventory = inventory != null ? inventory : Player.Current != null ? Player.Current.Bag : null;
        if (inventory == null || grid == null || slotPrefab == null || tooltip == null || dragIcon == null || recipes == null)
        {
            Debug.LogError("ItemInventoryWindow is missing a reference (inventory, grid, slot prefab, tooltip, drag icon or recipe browser).", this);
            enabled = false;
            return;
        }

        // Hearthline 3: the sections' headings carry their icons.
        HearthKit.DecorateHeadings(transform, ("Bag", "Bags"), ("Safe Pocket", "Lock"), ("Essences", "Spark"), ("Crafting", "Hammer"),
            ("Needs", "Wheat"));

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
        inventory.Quick.Changed += OnQuickChanged;
        inventory.ItemUsed += OnItemUsed;
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
        GameInput.KeysChanged += OnKeysChanged;
        WriteHints();
    }

    private void OnKeysChanged()
    {
        WriteHints();
        RebuildRecipes();
    }

    private void WriteHints()
    {
        if (bagHint == null && transform.Find("Bag Hint") is Transform bag && bag.TryGetComponent(out bagHint))
        {
            bagHintForMouse = bagHint.text;
        }

        if (closeHint == null && transform.Find("Close Hint") is Transform close && close.TryGetComponent(out closeHint))
        {
            closeHintForMouse = closeHint.text;
        }

        bool pad = InputDevices.UsingGamepad;
        PadFamily family = InputDevices.Family;
        string a = ControllerGlyphs.For("<Gamepad>/buttonSouth", family), x = ControllerGlyphs.For("<Gamepad>/buttonWest", family);
        if (bagHint != null)
        {
            bagHint.text = !pad ? bagHintForMouse
                : $"hold {a} and move to drag  ·  {GameInput.KeyLabel(GameInput.SplitModifier)} + {a} split  ·  {x} use  ·  {GameInput.KeyLabel(GameInput.DiscardModifier)} + {x} drop";
        }

        if (closeHint != null)
        {
            closeHint.text = !pad ? closeHintForMouse : $"{GameInput.KeyLabel(GameInput.Inventory)} or {GameInput.KeyLabel(GameInput.Back)} to close";
        }
    }

    private void OnDisable()
    {
        GameInput.KeysChanged -= OnKeysChanged;
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

        if (inventory != null)
        {
            inventory.Quick.Changed -= OnQuickChanged;
            inventory.ItemUsed -= OnItemUsed;
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
        if (hovered != null && dragFrom < 0 && QuickKeyPressed())
        {
            ToggleQuick(ContainerOf(hovered).GetSlot(hovered.Index));
        }

        // A skill key over a usable item puts it on that slot of the skill bar (again: takes it off).
        for (int slot = 0; hovered != null && dragFrom < 0 && slot < SkillLoadout.SlotCount; slot++)
        {
            if (Pressed(GameInput.Skill(slot)))
            {
                ToggleOnSkillBar(ContainerOf(hovered).GetSlot(hovered.Index), slot);
                break;
            }
        }

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

        bool sideBySide = UIStateManager.Current != null && UIStateManager.Current.IsOpen(UIWindowId.Character);
        if (sideBySide && characterPanel == null)
        {
            CharacterWindow character = UIStateManager.Current.Find<CharacterWindow>(UIWindowId.Character);
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
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2 && !GameInput.SplitModifier.IsPressed())
        {
            // A double-click uses it too, for those who reach for the left button.
            inventory.Use(owner, slot.Index);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
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
            ItemStack stack = ContainerOf(slot).GetSlot(slot.Index);
            tooltip.Show(stack, QuickHint(stack));
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

    // ----- Quickbar and use feedback -----

    // The Player map is off while a window is open, so the quickbar key is read from its bound controls (rebinds included).
    private static bool QuickKeyPressed() => Pressed(GameInput.QuickItems);

    private static bool Pressed(InputAction action)
    {
        if (action == null)
        {
            return false;
        }

        foreach (InputControl control in action.controls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    private string QuickHint(ItemStack stack)
    {
        if (stack.IsEmpty || !QuickItems.Accepts(stack.Item))
        {
            return null;
        }

        string key = GameInput.KeyLabel(GameInput.QuickItems);
        string quick = inventory.Quick.Contains(stack.Item)
            ? $"<color={UIStyle.Current.AccentHex}>On the quickbar (hold {key}). {key}: take it off.</color>"
            : $"<color={UIStyle.Current.MutedHex}>{key}: add to the quickbar (hold {key} to use it anywhere).</color>";
        PlayerSkills skills = Player.Current != null ? Player.Current.Skills : null;
        int onBar = skills != null ? skills.Loadout.IndexOf(stack.Item) : -1;
        string first = PlayerInputHandler.SkillKeyLabel(0), last = PlayerInputHandler.SkillKeyLabel(SkillLoadout.SlotCount - 1);
        string bar = onBar >= 0
            ? $"<color={UIStyle.Current.AccentHex}>On the skill bar ({PlayerInputHandler.SkillKeyLabel(onBar)}). {PlayerInputHandler.SkillKeyLabel(onBar)}: take it off.</color>"
            : $"<color={UIStyle.Current.MutedHex}>{first}-{last}: put it on the skill bar.</color>";
        return quick + "\n" + bar;
    }

    private void ToggleOnSkillBar(ItemStack stack, int slot)
    {
        PlayerSkills skills = Player.Current != null ? Player.Current.Skills : null;
        if (stack.IsEmpty || skills == null)
        {
            return;
        }

        string key = PlayerInputHandler.SkillKeyLabel(slot);
        if (!QuickItems.Accepts(stack.Item))
        {
            GameMessages.Post("Only things you can use go on the skill bar.", MessageKind.Warning);
        }
        else if (skills.Loadout.GetItem(slot) == stack.Item)
        {
            skills.Loadout.Clear(slot);
            GameMessages.Post($"{stack.Item.DisplayName} is off the skill bar.", MessageKind.Info);
        }
        else
        {
            SkillDefinition replaced = skills.Loadout.Get(slot);
            skills.Loadout.EquipItem(slot, stack.Item);
            GameMessages.Post($"{stack.Item.DisplayName} is on the skill bar: {key} uses it" + (replaced != null ? $" ({replaced.DisplayName} went back to the skill book)." : "."), MessageKind.Info);
        }

        tooltip.Show(stack, QuickHint(stack));
    }

    private void ToggleQuick(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return;
        }

        string key = GameInput.KeyLabel(GameInput.QuickItems);
        if (!QuickItems.Accepts(stack.Item))
        {
            GameMessages.Post("Only things you can use go on the quickbar.", MessageKind.Warning);
        }
        else if (inventory.Quick.Toggle(stack.Item))
        {
            GameMessages.Post($"{stack.Item.DisplayName} is on the quickbar: hold {key} to use it.", MessageKind.Info);
        }
        else if (inventory.Quick.IsFull && !inventory.Quick.Contains(stack.Item))
        {
            GameMessages.Post($"The quickbar is full ({QuickItems.Capacity}). Take something off it first.", MessageKind.Warning);
        }
        else
        {
            GameMessages.Post($"{stack.Item.DisplayName} is off the quickbar.", MessageKind.Info);
        }
    }

    private void OnQuickChanged()
    {
        ShowAll(container, slots);
        if (pocket != null)
        {
            ShowAll(pocket, pocketSlots);
        }

        if (hovered != null && dragFrom < 0)
        {
            ItemStack stack = ContainerOf(hovered).GetSlot(hovered.Index);
            tooltip.Show(stack, QuickHint(stack));
        }
    }

    // Used from this window: its slot gives a little jump, so the right-click is seen to have worked (the use bar says what).
    private void OnItemUsed(ItemDefinition item, ItemContainer from, int index)
    {
        List<ItemSlotView> views = from == container ? slots : from == pocket ? pocketSlots : null;
        if (views != null && index >= 0 && index < views.Count && isActiveAndEnabled)
        {
            StartCoroutine(Pulse((RectTransform)views[index].transform));
        }
    }

    private static System.Collections.IEnumerator Pulse(RectTransform slot)
    {
        const float seconds = 0.28f;
        for (float t = 0f; t < seconds && slot != null; t += Time.unscaledDeltaTime)
        {
            float k = t / seconds;
            slot.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }

        if (slot != null)
        {
            slot.localScale = Vector3.one;
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
            ItemStack stack = source.GetSlot(i);
            views[i].Show(stack, !source.IsUnlocked(i), false, !stack.IsEmpty && inventory.Quick.Contains(stack.Item));
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
            ItemStack stack = source.GetSlot(index);
            views[index].Show(stack, !source.IsUnlocked(index), false, !stack.IsEmpty && inventory.Quick.Contains(stack.Item));
            if (hovered == views[index] && dragFrom < 0)
            {
                tooltip.Show(stack, QuickHint(stack));
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

        // Hearthline 3: each tool, the bag and the weight with its kit icon (HearthKit.InlineIcon) rather than a word.
        var builder = new StringBuilder();
        ToolType[] tools = { ToolType.Axe, ToolType.Pickaxe, ToolType.Sickle };
        for (int i = 0; i < tools.Length; i++)
        {
            int tier = ItemInventory.GatheringTier(inventory.gameObject, tools[i]);
            builder.Append(i > 0 ? "    " : "").Append(HearthKit.InlineIcon(tools[i].ToString())).Append(' ').Append(tools[i]).Append(' ')
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

        string bagColor = used >= container.UnlockedSlots ? UIStyle.Current.DangerHex : UIStyle.Current.MutedHex;
        builder.Append($"      {HearthKit.InlineIcon("Bags")} <color={bagColor}>Bag {used}/{container.UnlockedSlots}</color>");
        if (container.HasWeightLimit)
        {
            float weight = container.CurrentWeight;
            float budget = container.WeightCapacity;
            string weightColor = weight >= budget - 0.01f ? UIStyle.Current.DangerHex : weight >= budget * 0.8f ? UIStyle.Current.WarningHex : UIStyle.Current.MutedHex;
            builder.Append($"      {HearthKit.InlineIcon("Weight")} <color={weightColor}>Weight {weight:0.#}/{budget:0}</color>");
            if (Player.Current != null && Player.Current.TryGetComponent(out PlayerBurden burden) && burden.IsHeavy)
            {
                builder.Append($"  <color={UIStyle.Current.WarningHex}>Heavy: slower, tires faster</color>");
            }
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
        if (InputDevices.UsingGamepad)
        {
            PadFamily family = InputDevices.Family;
            SetFooter($"{ControllerGlyphs.For("<Gamepad>/buttonSouth", family)} twice or {ControllerGlyphs.For("<Gamepad>/buttonWest", family)} crafts a recipe. Buildings: hold {GameInput.KeyLabel(GameInput.WindowsWheel)}, Build.");
        }
        else
        {
            SetFooter($"Double-click or right-click a recipe to craft one. Buildings: build menu ({GameInput.KeyLabel(GameInput.Build)}).");
        }
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
