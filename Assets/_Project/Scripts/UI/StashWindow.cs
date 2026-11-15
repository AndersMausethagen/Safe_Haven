using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The stash, opened at a stash chest in Safe Haven: the bag on one side and that chest on the other. Click a stack to
/// move it across, or drag it onto a slot on either side (the user, 2026-11-15); raw resources move all at once with the
/// buttons. What is in the stash stays home during raids, so a failed raid can't take it. The bag side shows the player's
/// ItemInventory and ResourceInventory, the chest side its own block of the GameSession's stash (StashChest.Block). It
/// closes when the player walks away from the chest.
/// </summary>
public sealed partial class StashWindow : MonoBehaviour
{
    // One grid of slots showing one container, or one chest's block of it (First, Count). Clicking a stack sends it to
    // the other grid; dragging drops it on a slot of either. Indexes passed to the window are the container's own.
    private sealed class Grid : IItemSlotHandler
    {
        private readonly StashWindow owner;
        private readonly RectTransform root;
        private readonly List<ItemSlotView> views = new List<ItemSlotView>();

        public Grid(StashWindow owner, RectTransform root)
        {
            this.owner = owner;
            this.root = root;
        }

        public ItemContainer Container { get; private set; }

        /// <summary>The container's first slot shown here.</summary>
        public int First { get; private set; }

        /// <summary>How many of the container's slots are shown, from First.</summary>
        public int Count => views.Count;

        // A chest's grid leaves out the locked slots past its size, unless something is kept in one.
        private bool hideLockedEmpty;

        public void Show(ItemContainer container) => Show(container, 0, container.SlotCount, false);

        public void Show(ItemContainer container, int first, int count, bool hideLocked)
        {
            Hide();
            Container = container;
            First = first;
            hideLockedEmpty = hideLocked;
            container.SlotChanged += OnSlotChanged;
            container.LayoutChanged += ShowAll;
            if (views.Count != count)
            {
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(root.GetChild(i).gameObject);
                }

                views.Clear();
                var layout = root.GetComponent<GridLayoutGroup>();
                if (layout != null)
                {
                    layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    layout.constraintCount = container.Columns;
                }

                for (int i = 0; i < count; i++)
                {
                    ItemSlotView view = Object.Instantiate(owner.slotPrefab, root);
                    view.name = "Slot " + i;
                    view.Bind(this, i);
                    views.Add(view);
                }
            }

            ShowAll();
        }

        private void ShowAll()
        {
            for (int i = 0; i < views.Count; i++)
            {
                ShowSlot(i);
            }

            owner.Compacted(Container, views, root);
        }

        private void ShowSlot(int view)
        {
            int index = First + view;
            ItemStack stack = Container.GetSlot(index);
            bool locked = !Container.IsUnlocked(index);
            views[view].Show(stack, locked);
            if (hideLockedEmpty && views[view].gameObject.activeSelf != (!locked || !stack.IsEmpty))
            {
                views[view].gameObject.SetActive(!locked || !stack.IsEmpty);
            }
        }

        public void Hide()
        {
            if (Container != null)
            {
                Container.SlotChanged -= OnSlotChanged;
                Container.LayoutChanged -= ShowAll;
                Container = null;
            }
        }

        private void OnSlotChanged(int index)
        {
            int view = index - First;
            if (view < 0 || view >= views.Count)
            {
                return;
            }

            ShowSlot(view);
            owner.Compacted(Container, views, root);
            owner.OnSlotChanged(this, index);
        }

        public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                owner.Transfer(this, First + slot.Index);
            }
        }

        public void OnSlotHovered(ItemSlotView slot) => owner.ShowTooltip(this, First + slot.Index);

        public void OnSlotUnhovered(ItemSlotView slot) => owner.HideTooltip(this, First + slot.Index);

        public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData) => owner.BeginDrag(this, First + slot.Index, eventData);

        public void OnSlotDragged(PointerEventData eventData) => owner.MoveDragIcon(eventData);

        public void OnSlotDropped(ItemSlotView target) => owner.DropOn(this, First + target.Index);

        public void OnSlotDragEnded() => owner.EndDrag();
    }

    [SerializeField] private UIWindow window;
    [Tooltip("Parent for the bag's slots. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform bagGrid;
    [Tooltip("Parent for the stash's slots. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform stashGrid;
    [SerializeField] private ItemSlotView slotPrefab;
    [SerializeField] private ItemTooltipView tooltip;
    [Tooltip("Lists the resources in the stash and on the player, with rich text.")]
    [SerializeField] private TMP_Text resourcesLabel;
    [SerializeField] private Button storeResourcesButton;
    [SerializeField] private Button takeResourcesButton;
    [SerializeField] private Button closeButton;

    private Grid bag;
    private Grid stash;
    private Grid hoveredGrid;
    private int hoveredIndex = -1;
    private StashChest chest;
    private GameObject player;
    private ResourceStorage carried;

    private void Awake()
    {
        bag = new Grid(this, bagGrid);
        stash = new Grid(this, stashGrid);
        storeResourcesButton.onClick.AddListener(StoreResources);
        storeResourcesButton.GetComponentInChildren<TMP_Text>().text = "Store all materials";
        takeResourcesButton.onClick.AddListener(TakeResources);
        closeButton.onClick.AddListener(Close);
        window.OpenChanged += OnOpenChanged;
    }

    private void OnDestroy()
    {
        window.OpenChanged -= OnOpenChanged;
        Release();
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (chest == null || player == null || !chest.IsInReach(player.transform.position))
        {
            Close();
            return;
        }

        tooltip.Follow(GameInput.PointerPosition);
    }

    public void Open(StashChest source, GameObject user)
    {
        ItemInventory items = user != null ? user.GetComponentInParent<ItemInventory>() : null;
        if (items == null)
        {
            Debug.LogError("StashWindow needs a user with an ItemInventory.", this);
            return;
        }

        Release();
        chest = source;
        player = user;
        bag.Show(items.Container);
        ShowChest();
        BuildKit();
        bag.Show(items.Container);

        ResourceInventory resources = items.GetComponent<ResourceInventory>();
        carried = resources != null ? resources.Storage : null;
        if (carried != null)
        {
            carried.Changed += OnResourcesChanged;
        }

        GameSession.Stash.Resources.Changed += OnResourcesChanged;
        RefreshResources();
        window.Open();
        tooltip.Hide();
    }

    public void Close()
    {
        window.Close();
    }

    // This chest's own block of the stash: its slots, and any locked slot of the block that still keeps something.
    private void ShowChest()
    {
        stash.Show(GameSession.Stash.Items, chest.FirstSlot, StashStorage.ChestSlots, true);
    }

    // ----- Dragging between the bag and the chest -----

    private Image dragIcon;
    private bool dragging;

    private void BeginDrag(Grid grid, int index, PointerEventData eventData)
    {
        ItemStack stack = grid.Container != null ? grid.Container.GetSlot(index) : ItemStack.Empty;
        if (eventData.button != PointerEventData.InputButton.Left || stack.IsEmpty)
        {
            return;
        }

        dragging = true;
        ItemDrag.Begin(grid.Container, index);
        Image icon = DragIcon();
        icon.sprite = stack.Item.Icon;
        icon.gameObject.SetActive(stack.Item.Icon != null);
        icon.transform.SetAsLastSibling();
        tooltip.Hide();
        MoveDragIcon(eventData);
    }

    private void MoveDragIcon(PointerEventData eventData)
    {
        if (!dragging || dragIcon == null)
        {
            return;
        }

        var area = (RectTransform)dragIcon.rectTransform.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local);
        dragIcon.rectTransform.anchoredPosition = local;
    }

    private void DropOn(Grid grid, int index)
    {
        if (!ItemDrag.Active || grid.Container == null)
        {
            return;
        }

        ItemContainer source = ItemDrag.Source;
        int from = ItemDrag.Index;
        ItemStack moving = source.GetSlot(from);
        if (ItemDrag.DropOn(grid.Container, index) && grid == stash && source != stash.Container && !moving.IsEmpty)
        {
            // Put away by hand, as a click would: a quest can ask for it ("A Safe Place").
            ItemStack left = source.GetSlot(from);
            int moved = left.Item == moving.Item ? moving.Count - left.Count : moving.Count;
            if (moved > 0)
            {
                GameEvents.Raise(new GameEvent(GameEventKind.Stored, moving.Item, moved, player));
            }
        }
    }

    private void EndDrag()
    {
        if (dragging)
        {
            ItemDrag.End();
        }

        dragging = false;
        if (dragIcon != null)
        {
            dragIcon.gameObject.SetActive(false);
        }
    }

    // Made on first use, over everything in the window, never in the way of the pointer.
    private Image DragIcon()
    {
        if (dragIcon == null)
        {
            var rect = (RectTransform)new GameObject("Drag Icon", typeof(RectTransform)).transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(56f, 56f);
            dragIcon = rect.gameObject.AddComponent<Image>();
            dragIcon.raycastTarget = false;
            dragIcon.preserveAspect = true;
        }

        return dragIcon;
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            Release();
        }
    }

    private void Release()
    {
        bag?.Hide();
        stash?.Hide();
        if (carried != null)
        {
            carried.Changed -= OnResourcesChanged;
            carried = null;
        }

        GameSession.Stash.Resources.Changed -= OnResourcesChanged;
        EndDrag();
        hoveredGrid = null;
        hoveredIndex = -1;
        chest = null;
        player = null;
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    private void Transfer(Grid from, int index)
    {
        Grid to = from == bag ? stash : bag;
        if (from.Container == null || to.Container == null || from.Container.GetSlot(index).IsEmpty)
        {
            return;
        }

        int moved = to == stash ? GameSession.Stash.Store(from.Container, index, player, chest.Block) : from.Container.TransferTo(index, to.Container);
        if (moved == 0)
        {
            GameMessages.Post(to == stash ? "Storage is full." : "No room in the bag.", MessageKind.Warning);
        }
    }

    private void OnSlotChanged(Grid grid, int index)
    {
        if (grid == hoveredGrid && index == hoveredIndex)
        {
            ShowTooltip(grid, index);
        }
    }

    private void ShowTooltip(Grid grid, int index)
    {
        hoveredGrid = grid;
        hoveredIndex = index;
        if (grid.Container != null)
        {
            tooltip.Show(grid.Container.GetSlot(index));
        }
    }

    private void HideTooltip(Grid grid, int index)
    {
        if (grid == hoveredGrid && index == hoveredIndex)
        {
            hoveredGrid = null;
            hoveredIndex = -1;
            tooltip.Hide();
        }
    }

    private void StoreResources()
    {
        int moved = GameSession.Stash.StoreResources(carried, player);
        moved += GameSession.Stash.StoreMaterials(bag.Container, player, chest.Block);
        GameMessages.Post(moved > 0 ? $"Stored {moved} materials." : "You carry no materials.", moved > 0 ? MessageKind.Info : MessageKind.Warning);
    }

    private void TakeResources()
    {
        ResourceStorage stored = GameSession.Stash.Resources;
        int before = Total(stored);
        int moved = carried != null ? stored.TransferAllTo(carried) : 0;
        if (before == 0)
        {
            GameMessages.Post("There are no resources in storage.", MessageKind.Warning);
        }
        else if (moved < before)
        {
            GameMessages.Post("You can't carry it all; the rest stays in storage.", MessageKind.Warning);
        }
    }

    private void OnResourcesChanged(ResourceChange change)
    {
        RefreshResources();
    }

    private void RefreshResources()
    {
        resourcesLabel.text = HearthKit.ToneNumbers($"<b>In storage:</b>  {Describe(GameSession.Stash.Resources)}\n<b>Carried:</b>  {Describe(carried)}");
    }

    private static string Describe(ResourceStorage storage)
    {
        var amounts = storage != null ? new List<ResourceAmount>(storage.StoredAmounts) : new List<ResourceAmount>();
        if (amounts.Count == 0)
        {
            return $"<color={UIStyle.Current.MutedHex}>nothing</color>";
        }

        amounts.Sort((a, b) =>
        {
            int result = a.Resource.Tier.CompareTo(b.Resource.Tier);
            return result != 0 ? result : string.CompareOrdinal(a.Resource.DisplayName, b.Resource.DisplayName);
        });

        var builder = new StringBuilder();
        foreach (ResourceAmount amount in amounts)
        {
            builder.Append(builder.Length > 0 ? "  ·  " : "").Append(amount.Resource.DisplayName).Append(' ').Append(amount.Amount);
        }

        return builder.ToString();
    }

    private static int Total(ResourceStorage storage)
    {
        int total = 0;
        foreach (ResourceAmount amount in storage.StoredAmounts)
        {
            total += amount.Amount;
        }

        return total;
    }
}
