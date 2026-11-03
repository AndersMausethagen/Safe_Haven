using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The stash, opened at the stash chest in Safe Haven: the bag on one side and the stash on the other. Click a stack to
/// move it across; raw resources move all at once with the buttons. What is in the stash stays home during raids, so a
/// failed raid can't take it. The bag side shows the player's ItemInventory and ResourceInventory, the stash side the
/// GameSession's stash. It closes when the player walks away from the chest.
/// </summary>
public sealed class StashWindow : MonoBehaviour
{
    // One grid of slots showing one container. Clicking a stack sends it to the other grid.
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

        public void Show(ItemContainer container)
        {
            Hide();
            Container = container;
            container.SlotChanged += OnSlotChanged;
            container.LayoutChanged += ShowAll;
            if (views.Count != container.SlotCount)
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

                for (int i = 0; i < container.SlotCount; i++)
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
                views[i].Show(Container.GetSlot(i), !Container.IsUnlocked(i));
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
            if (index >= 0 && index < views.Count)
            {
                views[index].Show(Container.GetSlot(index), !Container.IsUnlocked(index));
            }

            owner.OnSlotChanged(this, index);
        }

        public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                owner.Transfer(this, slot.Index);
            }
        }

        public void OnSlotHovered(ItemSlotView slot) => owner.ShowTooltip(this, slot.Index);

        public void OnSlotUnhovered(ItemSlotView slot) => owner.HideTooltip(this, slot.Index);

        // Stacks move with a click here; dragging does nothing.
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
        stash.Show(GameSession.Stash.Items);

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

        if (from.Container.TransferTo(index, to.Container) == 0)
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
        int moved = carried != null ? carried.TransferAllTo(GameSession.Stash.Resources) : 0;
        moved += GameSession.Stash.StoreMaterials(bag.Container);
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
        resourcesLabel.text = $"<b>In storage:</b>  {Describe(GameSession.Stash.Resources)}\n<b>Carried:</b>  {Describe(carried)}";
    }

    private static string Describe(ResourceStorage storage)
    {
        var amounts = storage != null ? new List<ResourceAmount>(storage.StoredAmounts) : new List<ResourceAmount>();
        if (amounts.Count == 0)
        {
            return "<color=#AAB0BC>nothing</color>";
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
