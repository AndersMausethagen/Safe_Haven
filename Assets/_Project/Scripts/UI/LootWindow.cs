using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// What a slain creature carried (LootableRemains, BodyLootSettings), opened by looting its body: its items as the bag's slots and its
/// resources as chips with their icons. Click one to take it, or Take All (selected first on a controller). The game's own tooltip on a
/// hovered stack. It closes when everything is taken (the body sinks), when the player walks away, or with the close button; what was
/// left stays on the body. Built in code the first time a body is looted (Hearthline 3, as the cargo window).
/// </summary>
public sealed class LootWindow : MonoBehaviour
{
    // As small as what it holds (the user, 2026-11-24: "make it smaller. no need for so much space with nothing in it"): its width
    // follows the stacks and chips shown, never narrower than the title and the foot need.
    private const float Margin = 16f;
    private const float Cell = 56f;
    private const float Gap = 4f;
    private const float GridTop = 66f;
    private const float ChipHeight = 36f;
    private const float ChipWidth = 92f;
    private const float ChipGap = 4f;
    private const float HintHeight = 20f;
    private const float FootHeight = 50f;
    private const float ButtonWidth = 112f;
    private const float ButtonHeight = 38f;
    private const float LeastInside = 240f;
    private const int MostColumns = 5;
    private const float ScreenMargin = 24f;

    private sealed class Grid : IItemSlotHandler
    {
        private readonly LootWindow owner;
        private readonly List<ItemSlotView> views = new List<ItemSlotView>();

        public Grid(LootWindow owner, RectTransform root)
        {
            this.owner = owner;
            Root = root;
        }

        public RectTransform Root { get; }
        public ItemContainer Container { get; private set; }

        public void Show(ItemContainer container)
        {
            Hide();
            Container = container;
            container.SlotChanged += OnSlotChanged;
            for (int i = Root.childCount - 1; i >= 0; i--)
            {
                Destroy(Root.GetChild(i).gameObject);
            }

            views.Clear();
            Root.GetComponent<GridLayoutGroup>().constraintCount = Mathf.Min(MostColumns, container.Columns);
            for (int i = 0; i < container.SlotCount; i++)
            {
                ItemSlotView view = Instantiate(TransportSettings.Current.SlotPrefab, Root);
                view.name = "Slot " + i;
                view.Bind(this, i);
                views.Add(view);
                view.Show(container.GetSlot(i), false);
                // Only what it holds: an empty slot at opening isn't shown (taking leaves its slot, so nothing jumps).
                view.gameObject.SetActive(!container.GetSlot(i).IsEmpty);
            }
        }

        public void Hide()
        {
            if (Container != null)
            {
                Container.SlotChanged -= OnSlotChanged;
                Container = null;
            }
        }

        /// <summary>How many slots show (those that held something when it opened).</summary>
        public int Shown
        {
            get
            {
                int shown = 0;
                foreach (ItemSlotView view in views)
                {
                    shown += view.gameObject.activeSelf ? 1 : 0;
                }

                return shown;
            }
        }

        private void OnSlotChanged(int index)
        {
            if (index >= 0 && index < views.Count)
            {
                views[index].Show(Container.GetSlot(index), false);
            }
        }

        public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
        {
            if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Left)
            {
                owner.TakeSlot(slot.Index);
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
    private TMP_Text bagLabel;
    private TMP_Text hint;
    private RectTransform closeButton;
    private RectTransform hairline;
    private RectTransform chips;
    private Button takeAll;
    private ItemTooltipView tooltip;
    private Grid grid;
    private LootableRemains shown;
    private GameObject looter;
    private int chipsFor = -1;

    public static LootWindow Current { get; private set; }

    /// <summary>The body whose loot is shown, while open.</summary>
    public LootableRemains Shown => window != null && window.IsOpen ? shown : null;

    public bool IsShowing(LootableRemains remains) => remains != null && Shown == remains;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Opens <paramref name="remains"/>' loot for <paramref name="user"/>, building the window the first time.</summary>
    public static void Show(LootableRemains remains, GameObject user)
    {
        (Current != null ? Current : Create()).Open(remains, user);
    }

    public static LootWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Loot Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Under 100: a quick look at a body, not a full-screen window, so the world stays in view behind it (no blurred backdrop).
        canvas.sortingOrder = 98;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<LootWindow>();
        Current = view;
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    public void Close()
    {
        if (window != null && window.IsOpen)
        {
            window.Close();
        }
    }

    private void Open(LootableRemains remains, GameObject user)
    {
        if (remains == null || user == null || TransportSettings.Current.SlotPrefab == null)
        {
            return;
        }

        shown = remains;
        looter = user;
        titleLabel.text = remains.Title;
        grid.Show(remains.Items);
        chipsFor = -1;
        Describe(ItemStack.Empty);
        window.Open();
        Refresh();
        if (Application.isPlaying)
        {
            UIMotion.Pop(panel, 0.95f, 0.2f);
        }

        if (UnityEngine.InputSystem.Gamepad.current != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(takeAll.gameObject);
        }
    }

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
        if (window == null || !window.IsOpen)
        {
            return;
        }

        float reach = BodyLootSettings.Current.WindowReach;
        if (shown == null || looter == null || (shown.transform.position - looter.transform.position).sqrMagnitude > reach * reach)
        {
            window.Close();
            return;
        }

        Refresh();
        tooltip.Follow(GameInput.PointerPosition);
    }

    private void TakeSlot(int index)
    {
        if (shown != null && !shown.Items.GetSlot(index).IsEmpty && !shown.Take(index, looter))
        {
            GameMessages.Post("No room in your bag.", MessageKind.Warning);
        }

        Describe(ItemStack.Empty);
    }

    private void TakeResource(int index)
    {
        if (shown != null && !shown.TakeResource(index, looter))
        {
            GameMessages.Post("You can't carry more of that.", MessageKind.Warning);
        }
    }

    private void TakeAll()
    {
        if (shown != null)
        {
            shown.TakeAll(looter);
        }
    }

    // The chips follow the resources left; the panel is as tall as what it shows.
    private void Refresh()
    {
        if (shown == null)
        {
            return;
        }

        IReadOnlyList<ResourceAmount> resources = shown.Resources;
        int signature = resources.Count;
        foreach (ResourceAmount resource in resources)
        {
            signature = signature * 31 + resource.Amount;
        }

        if (signature != chipsFor)
        {
            chipsFor = signature;
            BuildChips(resources);
        }

        ItemInventory bag = looter != null ? looter.GetComponent<ItemInventory>() : null;
        ItemContainer carried = bag != null ? bag.Container : null;
        bagLabel.text = carried != null && carried.HasWeightLimit ? HearthKit.ToneNumbers($"Bag {carried.CurrentWeight:0} / {carried.WeightCapacity:0}") : "";

        // Wide enough for the stacks or the chips, whichever is wider; tall enough for their rows.
        int stacks = grid.Shown;
        int columns = Mathf.Clamp(stacks, 1, MostColumns);
        int chipColumns = Mathf.Clamp(resources.Count, 0, 3);
        float inside = Mathf.Max(LeastInside, columns * (Cell + Gap) - Gap, chipColumns * (ChipWidth + ChipGap) - ChipGap);
        float width = inside + Margin * 2f;
        ((RectTransform)grid.Root).GetComponent<GridLayoutGroup>().constraintCount = columns;
        float gridBottom = stacks > 0 ? GridTop + Mathf.CeilToInt(stacks / (float)columns) * (Cell + Gap) - Gap : GridTop - 6f;
        float chipsTop = gridBottom + (resources.Count > 0 ? 8f : 0f);
        chips.anchoredPosition = new Vector2(Margin, -chipsTop);
        float chipRows = resources.Count > 0 ? Mathf.Ceil(resources.Count / 3f) : 0f;
        float below = chipsTop + chipRows * (ChipHeight + ChipGap) + (resources.Count > 0 ? HintHeight : 0f);
        float height = below + 10f + FootHeight;
        hint.rectTransform.anchoredPosition = new Vector2(Margin, -(below - HintHeight + 2f));
        hint.rectTransform.sizeDelta = new Vector2(inside, HintHeight);
        panel.sizeDelta = new Vector2(width, height);
        titleLabel.rectTransform.sizeDelta = new Vector2(inside - 40f, titleLabel.rectTransform.sizeDelta.y);
        closeButton.anchoredPosition = new Vector2(width - 46f, closeButton.anchoredPosition.y);
        hairline.sizeDelta = new Vector2(inside, hairline.sizeDelta.y);
        // The carved title's own rule is at least 240 wide, centred on the letters: past the edge of so small a panel. The hairline
        // under the header does its work here.
        Transform rule = titleLabel.transform.Find(HearthlineArt.RuleName);
        if (rule != null && rule.gameObject.activeSelf)
        {
            rule.gameObject.SetActive(false);
        }
        ((RectTransform)takeAll.transform).anchoredPosition = new Vector2(width - Margin - ButtonWidth, 10f);
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / width, (screen.height - ScreenMargin) / height);
    }

    private void BuildChips(IReadOnlyList<ResourceAmount> resources)
    {
        for (int i = chips.childCount - 1; i >= 0; i--)
        {
            Destroy(chips.GetChild(i).gameObject);
        }

        UIStyle style = UIStyle.Current;
        for (int i = 0; i < resources.Count; i++)
        {
            int index = i;
            ResourceAmount resource = resources[i];
            float x = (i % 3) * (ChipWidth + ChipGap);
            float y = (i / 3) * (ChipHeight + ChipGap);
            RectTransform chip = HearthKit.Card(chips, "Chip " + resource.Resource.DisplayName, x, y, ChipWidth, ChipHeight, 0.35f);
            var button = chip.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => TakeResource(index));
            var hover = chip.gameObject.AddComponent<UIHover>();
            string named = resource.Resource.DisplayName;
            hover.Entered = () => hint.text = named + "  ·  click to take it";
            hover.Exited = () => hint.text = "";
            if (resource.Resource.Icon != null)
            {
                Image icon = At("Icon", chip, 4f, 4f, ChipHeight - 8f, ChipHeight - 8f).gameObject.AddComponent<Image>();
                icon.sprite = resource.Resource.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            TMP_Text amount = Label("Amount", chip, ChipHeight + 2f, 0f, ChipWidth - ChipHeight - 6f, ChipHeight, 16f, style.Text);
            amount.text = HearthKit.ToneNumbers($"{resource.Amount}");
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
            tooltip.Show(stack, "Click to take it.");
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(LeastInside + Margin * 2f, 200f), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text kicker = Label("Kicker", panel, Margin, 10f, 200f, 16f, 12f, style.Accent);
        kicker.text = "LOOT";
        kicker.characterSpacing = 10f;
        titleLabel = Label("Title", panel, Margin, 24f, LeastInside - 40f, 30f, 24f, style.Text);
        style.Title(titleLabel);
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 16f;
        titleLabel.fontSizeMax = 24f;
        closeButton = (RectTransform)HearthKit.CloseButton(panel, LeastInside + Margin * 2f - 46f, 8f, Close).transform;
        hairline = (RectTransform)Hairline(panel, Margin, 58f, LeastInside, 1f, style).transform;

        RectTransform slots = At("Loot Grid", panel, Margin, GridTop, MostColumns * (Cell + Gap), 340f);
        var layout = slots.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(Cell, Cell);
        layout.spacing = new Vector2(Gap, Gap);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid = new Grid(this, slots);
        chips = At("Resources", panel, Margin, GridTop, 3f * (ChipWidth + ChipGap), ChipHeight);
        hint = Label("Hint", panel, Margin, 0f, LeastInside, HintHeight, 14f, style.Muted);
        bagLabel = Label("Bag", panel, Margin, 0f, 120f, ButtonHeight, 15f, style.Muted);
        AtFoot(bagLabel.rectTransform);
        TMP_Text take = AddButton("Take All", panel, LeastInside + Margin - ButtonWidth, 0f, ButtonWidth, UIRole.Kind.PrimaryButton, TakeAll, out takeAll);
        take.text = "Take all";
        take.fontSize = 17f;
        var takeRect = (RectTransform)takeAll.transform;
        takeRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        take.rectTransform.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        AtFoot(takeRect);
        BuildTooltip(content, style);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Loot, content.gameObject);
        window.OpenChanged += open =>
        {
            if (!open)
            {
                tooltip.Hide();
                grid.Hide();
                shown = null;
                looter = null;
            }
        };
    }

    private static void AtFoot(RectTransform rect)
    {
        float x = rect.anchoredPosition.x;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, 10f);
    }

    // The game's item tooltip, over the content so it keeps its size when a small screen shrinks the panel (as the cargo window's).
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
