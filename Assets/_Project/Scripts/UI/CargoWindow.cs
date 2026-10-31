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
/// when the player walks away from the transport.
/// </summary>
public sealed class CargoWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 720f;
    private const float Margin = 44f;
    private const float GridTop = 196f;
    private const float Cell = 62f;
    private const float Gap = 4f;
    private const float ScreenMargin = 24f;
    private const float Reach = 6f;

    // One grid of slots showing one container. Clicking a stack sends it to the other grid.
    private sealed class Grid : IItemSlotHandler
    {
        private readonly CargoWindow owner;
        private readonly RectTransform root;
        private readonly List<ItemSlotView> views = new List<ItemSlotView>();

        public Grid(CargoWindow owner, RectTransform root)
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
            for (int i = 0; i < views.Count; i++)
            {
                views[i].Show(Container.GetSlot(i), !Container.IsUnlocked(i));
            }
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
    private TMP_Text weightValue;
    private TMP_Text speedValue;
    private TMP_Text bagValue;
    private TMP_Text detailLabel;
    private Grid bag;
    private Grid cargo;
    private Transform anchor;
    private Transport transport;
    private GameObject viewer;

    public static CargoWindow Current { get; private set; }

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

    public void Open(Transport shown, GameObject user)
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
        titleLabel.text = shown.Definition.DisplayName;
        descriptionLabel.text = shown.Definition.Description;
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

        if (viewer == null || transport == null || (anchor.position - viewer.transform.position).sqrMagnitude > Reach * Reach)
        {
            window.Close();
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        UIStyle style = UIStyle.Current;
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        weightValue.text = $"{transport.Weight:0}<size=60%> / {transport.Definition.Capacity:0}";
        weightValue.color = transport.IsOverfilled ? style.Warning : style.Text;
        speedValue.text = $"{(1f - transport.SpeedScale) * 100f:0}<size=60%>%";
        speedValue.color = transport.IsOverfilled ? style.Warning : style.Text;
        ItemContainer carried = bag.Container;
        bagValue.text = carried != null ? $"{carried.CurrentWeight:0} / {carried.WeightCapacity:0} weight" : "";
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
        detailLabel.text = stack.IsEmpty ? "<alpha=#88>Click a stack to move it across." : $"<b>{stack.DisplayName}</b>\n{ItemTooltipView.DescribeStack(stack)}";
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
        titleLabel = Label("Title", panel, Margin, 52f, 620f, 56f, 42f, style.Text);
        style.Title(titleLabel);
        descriptionLabel = Label("Description", panel, Margin, 108f, 760f, 30f, 19f, style.Muted);

        weightValue = Figure("Load", 836f, style);
        speedValue = Figure("Slower", 1006f, style);
        Hairline(panel, Margin, 150f, Width - Margin * 2f, 1f, style);

        Heading("Your bag", panel, Margin, 164f, 300f, style);
        bagValue = Label("Bag Weight", panel, Margin + 300f, 164f, 220f, 26f, 17f, style.Muted, TextAlignmentOptions.Right);
        Heading("Cargo", panel, 620f, 164f, 300f, style);
        bag = new Grid(this, SlotGrid("Bag", Margin));
        cargo = new Grid(this, SlotGrid("Cargo", 620f));

        detailLabel = Paragraph("Detail", panel, Margin, 544f, 760f, 120f, 18f, style.Text);
        AddButton("Load", panel, 620f, 660f, 170f, UIRole.Kind.PrimaryButton, () => MoveAll(bag, cargo), out _).text = "Load all";
        AddButton("Unload", panel, 802f, 660f, 170f, UIRole.Kind.Button, () => MoveAll(cargo, bag), out _).text = "Unload all";
        AddButton("Close", panel, Width - Margin - 150f, 660f, 150f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Cargo, content.gameObject);
        window.OpenChanged += open =>
        {
            if (!open)
            {
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

    private TMP_Text Figure(string label, float x, UIStyle style)
    {
        Hairline(panel, x - 20f, 40f, 1f, 76f, style);
        TMP_Text value = Label(label + " Value", panel, x, 34f, 150f, 50f, 36f, style.Text);
        Label(label + " Label", panel, x, 86f, 150f, 24f, 17f, style.Muted).text = label;
        return value;
    }
}
