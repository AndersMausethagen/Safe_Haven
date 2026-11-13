using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The cargo of a transport beside the bag: click a stack to move it across, or move everything at once. It shows the load
/// against the capacity and what that does to the player's speed, because moving weight out of the bag is the point and
/// the price is the speed (GDD §8.5). Built in code the first time cargo is opened, as raids have no stash window; closes
/// when the player walks away from the transport. Hearthline 3: the kit's close button, the bag's grid down to its open slots
/// and one row of locked ones, the load as a bar in a card under the cargo, and the game's own tooltip on a hovered stack.
/// </summary>
public sealed class CargoWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 720f;
    // The panel is as tall as what it shows (a saddle bag's two rows leave no field of empty leather), never shorter.
    private const float MinimumHeight = 520f;
    private const float FootRoom = 112f;
    private const float Margin = 44f;
    private const float GridTop = 196f;
    private const float Cell = 62f;
    private const float Gap = 4f;
    private const float ScreenMargin = 24f;
    // Where the cargo column starts: an eight-column cart (the widest) ends on the right margin.
    private const float CargoX = Width - Margin - 8f * (Cell + Gap) + Gap;
    private const float Reach = 6f;

    // One grid of slots showing one container. Clicking a stack sends it to the other grid.
    private sealed class Grid : IItemSlotHandler
    {
        private readonly CargoWindow owner;
        private readonly RectTransform root;
        private readonly bool compact;
        private readonly List<ItemSlotView> views = new List<ItemSlotView>();

        /// <param name="compact">Shows the open slots and one row of locked ones (the bag), not every slot.</param>
        public Grid(CargoWindow owner, RectTransform root, bool compact)
        {
            this.owner = owner;
            this.root = root;
            this.compact = compact;
        }

        public ItemContainer Container { get; private set; }

        /// <summary>How many rows of slots show.</summary>
        public int Rows { get; private set; }

        public void Show(ItemContainer container)
        {
            Hide();
            Container = container;
            container.SlotChanged += OnSlotChanged;
            container.LayoutChanged += ShowAll;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }

            views.Clear();
            root.GetComponent<GridLayoutGroup>().constraintCount = container.Columns;
            for (int i = 0; i < container.SlotCount; i++)
            {
                ItemSlotView view = Instantiate(TransportSettings.Current.SlotPrefab, root);
                view.name = "Slot " + i;
                view.Bind(this, i);
                views.Add(view);
            }

            ShowAll();
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

        private void ShowAll()
        {
            int columns = Mathf.Max(1, Container.Columns);
            int shown = views.Count;
            if (compact)
            {
                int open = Mathf.CeilToInt(Container.UnlockedSlots / (float)columns) * columns;
                shown = Mathf.Min(views.Count, open + columns);
            }

            int last = -1;
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Show(Container.GetSlot(i), !Container.IsUnlocked(i));
                bool show = i < shown || !Container.GetSlot(i).IsEmpty;
                if (views[i].gameObject.activeSelf != show)
                {
                    views[i].gameObject.SetActive(show);
                }

                if (show)
                {
                    last = i;
                }
            }

            Rows = last < 0 ? 0 : last / columns + 1;
            owner.PlaceBelowGrids();
        }

        private void OnSlotChanged(int index)
        {
            if (index >= 0 && index < views.Count)
            {
                views[index].Show(Container.GetSlot(index), !Container.IsUnlocked(index));
            }
        }

        public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                owner.Move(this, slot.Index);
            }
        }

        public void OnSlotHovered(ItemSlotView slot) => owner.Describe(Container.GetSlot(slot.Index));

        public float Bottom => GridTop + Rows * (Cell + Gap) - Gap;

        public void OnSlotUnhovered(ItemSlotView slot) => owner.Describe(ItemStack.Empty);

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

    private UIWindow window;
    private RectTransform panel;
    private TMP_Text titleLabel;
    private TMP_Text descriptionLabel;
    private KitStatRow loadRow;
    private KitStatRow speedRow;
    private RectTransform loadCard;
    private TMP_Text bagValue;
    private TMP_Text detailLabel;
    private ItemTooltipView tooltip;
    private TMP_Text storageNote;
    private float shownWeight = -1f, shownCapacity = -1f, shownSpeed = -1f, shownCarried = -1f;
    private Button storageButton;
    private Grid bag;
    private Grid cargo;
    private Transform anchor;
    private ICargoHold transport;
    private GameObject viewer;

    public static CargoWindow Current { get; private set; }

    /// <summary>True while it shows this hold's cargo.</summary>
    public bool IsShowing(ICargoHold hold) => window != null && window.IsOpen && hold != null && ReferenceEquals(transport, hold);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static CargoWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Cargo Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<CargoWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    public void Open(ICargoHold shown, GameObject user)
    {
        ItemInventory items = user != null ? user.GetComponent<ItemInventory>() : null;
        if (items == null || TransportSettings.Current.SlotPrefab == null)
        {
            return;
        }

        transport = shown;
        viewer = user;
        anchor = shown.transform;
        bag.Show(items.Container);
        cargo.Show(shown.Cargo);
        titleLabel.text = shown.CargoTitle;
        descriptionLabel.text = shown.CargoDescription;
        shownWeight = shownCapacity = shownSpeed = shownCarried = -1f;
        Describe(ItemStack.Empty);
        window.Open();
        Refresh();
    }

    public void Close() => window.Close();

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (viewer == null || transport == null || anchor == null || (anchor.position - viewer.transform.position).sqrMagnitude > Reach * Reach)
        {
            window.Close();
            return;
        }

        Refresh();
        tooltip.Follow(GameInput.PointerPosition);
    }

    private void Refresh()
    {
        UIStyle style = UIStyle.Current;
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / panel.rect.height);
        float weight = transport.Weight, capacity = transport.Capacity, slower = (1f - transport.SpeedScale) * 100f;
        if (weight != shownWeight || capacity != shownCapacity || slower != shownSpeed)
        {
            shownWeight = weight;
            shownCapacity = capacity;
            shownSpeed = slower;
            Color bar = transport.IsOverfilled ? style.Warning : style.Accent;
            string load = $"{weight:0} / {capacity:0}";
            loadRow.Set(transport.IsOverfilled ? $"<color={style.WarningHex}>{load}</color>" : load, capacity > 0f ? weight / capacity : 0f, bar, bar);
            speedRow.Set(slower < 0.5f ? "Full speed" : $"<color={style.WarningHex}>{slower:0}% slower</color>", 0f, bar, bar);
        }

        ItemContainer carried = bag.Container;
        float carriedWeight = carried != null ? carried.CurrentWeight : 0f;
        if (carriedWeight != shownCarried)
        {
            shownCarried = carriedWeight;
            bagValue.text = carried != null ? HearthKit.ToneNumbers($"{carried.CurrentWeight:0} / {carried.WeightCapacity:0} weight") : "";
        }

        // Storage only exists at home, and the load still has to be brought to it.
        bool atHome = RaidDirector.Current == null;
        bool inReach = atHome && StashChest.AnyWithin(transport.transform.position, TransportSettings.Current.StorageReach);
        storageButton.gameObject.SetActive(atHome);
        storageButton.interactable = inReach && transport.Cargo.CountItems() > 0;
        storageNote.text = atHome && !inReach ? "Bring it near a chest." : "";
    }

    private void SendToStorage()
    {
        ItemContainer load = transport.Cargo;
        for (int i = 0; i < load.SlotCount; i++)
        {
            if (!load.GetSlot(i).IsEmpty)
            {
                GameSession.Stash.Store(load, i, viewer);
            }
        }

        GameMessages.Post(load.CountItems() > 0 ? "Storage is full. The rest stays in the cargo." : "The cargo goes into storage.",
            load.CountItems() > 0 ? MessageKind.Warning : MessageKind.Info);
    }

    private void Move(Grid from, int index)
    {
        Grid to = from == bag ? cargo : bag;
        if (from.Container != null && to.Container != null && !from.Container.GetSlot(index).IsEmpty && from.Container.TransferTo(index, to.Container) == 0)
        {
            GameMessages.Post(to == cargo ? "It can't take any more." : "No room in your bag.", MessageKind.Warning);
        }
    }

    private static void MoveAll(Grid from, Grid to)
    {
        if (from.Container == null || to.Container == null)
        {
            return;
        }

        for (int i = 0; i < from.Container.SlotCount; i++)
        {
            if (!from.Container.GetSlot(i).IsEmpty)
            {
                from.Container.TransferTo(i, to.Container);
            }
        }
    }

    private void Describe(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            tooltip.Hide();
        }
        else
        {
            tooltip.Show(stack, "Click to move it across.");
        }
    }

    // What sits under each grid follows its rows: the hint under the bag, the load card under the cargo.
    private void PlaceBelowGrids()
    {
        if (detailLabel == null || loadCard == null)
        {
            return;
        }

        detailLabel.rectTransform.anchoredPosition = new Vector2(Margin, -(bag.Bottom + 14f));
        loadCard.anchoredPosition = new Vector2(CargoX, -(cargo.Bottom + 18f));
        float bottom = Mathf.Max(bag.Bottom + 14f + detailLabel.rectTransform.rect.height, cargo.Bottom + 18f + loadCard.rect.height);
        panel.sizeDelta = new Vector2(Width, Mathf.Clamp(bottom + FootRoom, MinimumHeight, Height));
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text kicker = Label("Kicker", panel, Margin, 30f, 500f, 24f, 17f, style.Accent);
        kicker.text = "CARGO";
        kicker.characterSpacing = 12f;
        titleLabel = Label("Title", panel, Margin, 52f, 760f, 56f, 42f, style.Text);
        style.Title(titleLabel);
        descriptionLabel = Label("Description", panel, Margin, 112f, 900f, 30f, 19f, style.Muted);
        HearthKit.CloseButton(panel, Width - 66f, 18f, () => window.Close());
        Hairline(panel, Margin, 150f, Width - Margin * 2f, 1f, style);

        Heading("Your bag", panel, Margin, 164f, 300f, style);
        bagValue = Label("Bag Weight", panel, Margin + 300f, 164f, 224f, 26f, 17f, style.Muted, TextAlignmentOptions.Right);
        Heading("Cargo", panel, CargoX, 164f, 300f, style);
        detailLabel = Label("Hint", panel, Margin, 544f, 530f, 26f, 17f, style.Muted);
        detailLabel.text = "Click a stack to move it across.";
        detailLabel.fontStyle = FontStyles.Italic;

        // The load against what it carries easily, and what more costs in speed (GDD §8.5).
        loadCard = HearthKit.Card(panel, "Load Card", CargoX, 420f, Width - Margin - CargoX, 104f);
        loadRow = new KitStatRow(loadCard, 18f, 16f, Width - Margin - CargoX - 36f, "Load", "Weight", true);
        speedRow = new KitStatRow(loadCard, 18f, 66f, Width - Margin - CargoX - 36f, "Speed", "Horseshoe", false);

        bag = new Grid(this, SlotGrid("Bag", Margin), true);
        cargo = new Grid(this, SlotGrid("Cargo", CargoX), false);

        // The buttons keep to the foot as the panel grows and shrinks.
        AddButton("Store", panel, Margin, 0f, 210f, UIRole.Kind.Button, SendToStorage, out storageButton).text = "Send to storage";
        AtFoot((RectTransform)storageButton.transform);
        storageNote = Label("Storage Note", panel, Margin + 222f, 0f, 300f, 46f, 18f, style.Muted);
        AtFoot(storageNote.rectTransform);
        const float buttonWidth = 170f;
        AddButton("Load", panel, Width - Margin - buttonWidth * 2f - 12f, 0f, buttonWidth, UIRole.Kind.PrimaryButton, () => MoveAll(bag, cargo), out Button load).text = "Load all";
        AtFoot((RectTransform)load.transform);
        AddButton("Unload", panel, Width - Margin - buttonWidth, 0f, buttonWidth, UIRole.Kind.Button, () => MoveAll(cargo, bag), out Button unload).text = "Unload all";
        AtFoot((RectTransform)unload.transform);
        BuildTooltip(content, style);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Cargo, content.gameObject);
        window.OpenChanged += open =>
        {
            if (!open)
            {
                tooltip.Hide();
                bag.Hide();
                cargo.Hide();
                transport = null;
                viewer = null;
            }
        };
    }

    private RectTransform SlotGrid(string name, float x)
    {
        RectTransform grid = At(name + " Grid", panel, x, GridTop, 530f, 340f);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(Cell, Cell);
        layout.spacing = new Vector2(Gap, Gap);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        return grid;
    }

    private static void AtFoot(RectTransform rect)
    {
        float x = rect.anchoredPosition.x;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, 14f);
    }

    // The game's item tooltip, laid over the content rather than the panel so it keeps its size when a small screen
    // shrinks the panel.
    private void BuildTooltip(RectTransform content, UIStyle style)
    {
        var holder = new GameObject("Tooltip Holder", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(content, false);
        RectTransform tip = Box("Tooltip", content, new Vector2(0.5f, 0.5f), new Vector2(380f, 100f), Vector2.zero);
        tip.pivot = new Vector2(0f, 1f);
        tip.gameObject.AddComponent<Image>().raycastTarget = false;
        tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = Paragraph("Title", tip, 0f, 0f, 352f, 28f, 19f, style.Text);
        TMP_Text body = Paragraph("Body", tip, 0f, 0f, 352f, 28f, 16f, style.Text);
        tooltip = holder.AddComponent<ItemTooltipView>();
        tooltip.Configure(tip, title, body);
        holder.SetActive(true);
    }
}
