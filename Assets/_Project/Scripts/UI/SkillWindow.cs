using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The skill window (K), laid out and handled like the bag: the five skill bar slots on top and the book of known skills
/// below, both made of the same ItemSlotView cells, tooltip and drag icon as the bag window. Drag a known skill onto a bar
/// slot to equip it (it swaps with what was there, or moves if it was already slotted), drag between bar slots to
/// rearrange, and drag a bar skill back into the book to take it off. Right-click a known skill to slot it in the first
/// free slot (or take it off when it is slotted); right-click a bar slot to empty it. Everything shown comes from the
/// player's PlayerSkills. Since 2026-11-27 the frame is larger and the book scrolls in sections (SkillGroups: by type or by element,
/// two tabs), each skill a picture card with its name under it.
/// </summary>
public sealed class SkillWindow : MonoBehaviour, IItemSlotHandler
{
    [Tooltip("Left empty, the player's own skills are used.")]
    [SerializeField] private PlayerSkills skills;
    [Tooltip("Parent for the five bar slots. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform barGrid;
    [Tooltip("Parent for the known skills. Should have a GridLayoutGroup.")]
    [SerializeField] private RectTransform bookGrid;
    [SerializeField] private ItemSlotView slotPrefab;
    [SerializeField] private ItemTooltipView tooltip;
    [Tooltip("Follows the pointer while a skill is dragged. Should not block raycasts.")]
    [SerializeField] private Image dragIcon;
    // The window's size since 2026-11-27 (the user: "We need a larger frame, and maybe some grouping of the skills in order of type or
    // element"): the nine bar slots at full size, and the book scrolling in sections of picture cards, each skill's name under it.
    private const float WindowWidth = 1040f;
    private const float WindowHeight = 700f;
    private const float Margin = 24f;
    private const float BarTop = 96f;
    private const float BookTop = 222f;
    private const float FooterHeight = 40f;
    private const float CardWidth = 96f;
    private const float CardHeight = 122f;
    private const float CardGap = 10f;
    private const float CardPicture = 72f;
    private const float SectionHeading = 34f;
    private const float SectionGap = 14f;
    private const string GroupingKey = "SkillWindow.GroupByElement";
    // Kept clear above and below the window: a margin, and the HUD's skill bar.
    private const float ScreenTop = 24f;
    private const float ScreenBottom = 96f;
    private float windowHeight = WindowHeight;

    private readonly List<ItemSlotView> barSlots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> bookSlots = new List<ItemSlotView>();
    // The known skills in the order the book shows them (by type or by element), which a book card's Index points into.
    private readonly List<SkillDefinition> bookOrder = new List<SkillDefinition>();
    private ItemSlotView dragSource;
    private ItemSlotView hovered;
    private bool laidOut;
    private bool byElement;
    private RectTransform bookContent;
    private (TMP_Text label, Image shown) byTypeTab;
    private (TMP_Text label, Image shown) byElementTab;

    private void OnEnable()
    {
        skills = skills != null ? skills : Player.Current != null ? Player.Current.Skills : null;
        if (skills == null || barGrid == null || bookGrid == null || slotPrefab == null || tooltip == null || dragIcon == null)
        {
            Debug.LogError("SkillWindow is missing a reference (player skills, bar grid, book grid, slot prefab, tooltip or drag icon).", this);
            enabled = false;
            return;
        }

        if (!laidOut)
        {
            LayOut();
        }

        skills.Loadout.SlotChanged += OnLoadoutChanged;
        skills.KnownSkillsChanged += RebuildBook;
        BuildBar();
        RebuildBook();
        dragIcon.gameObject.SetActive(false);
        tooltip.Hide();
    }

    private void OnDisable()
    {
        if (skills != null)
        {
            skills.Loadout.SlotChanged -= OnLoadoutChanged;
            skills.KnownSkillsChanged -= RebuildBook;
        }

        CancelDrag();
        hovered = null;
        if (tooltip != null)
        {
            tooltip.Hide();
        }
    }

    private void Update()
    {
        tooltip.Follow(GameInput.PointerPosition);
    }

    // ----- Slots -----

    public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
    {
        if (eventData.dragging || eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (IsBar(slot))
        {
            skills.Loadout.Clear(slot.Index);
            return;
        }

        SkillDefinition skill = SkillOf(slot);
        if (skill == null)
        {
            return;
        }

        int equipped = skills.Loadout.IndexOf(skill);
        if (equipped >= 0)
        {
            skills.Loadout.Clear(equipped);
            return;
        }

        int free = skills.Loadout.FirstFittingEmpty(skill);
        if (free < 0)
        {
            GameMessages.Post($"No free {SkillKinds.Name(skill.Kind)} slot. Drag it onto one to swap.", MessageKind.Warning);
            return;
        }

        skills.Equip(free, skill);
    }

    /// <summary>Says why a skill can't go in a slot: each slot takes its own kind (SkillLoadout).</summary>
    public static void SayDoesNotFit(int slot, SkillDefinition skill)
    {
        if (skill != null)
        {
            GameMessages.Post($"{PlayerInputHandler.SkillKeyLabel(slot)} takes a {SkillLoadout.SlotName(slot)}, not a {SkillKinds.Name(skill.Kind)}.",
                MessageKind.Warning);
        }
    }

    public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
    {
        SkillDefinition skill = SkillOf(slot);
        if (eventData.button != PointerEventData.InputButton.Left || skill == null)
        {
            return;
        }

        dragSource = slot;
        dragIcon.sprite = skill.Icon;
        dragIcon.gameObject.SetActive(skill.Icon != null);
        dragIcon.transform.SetAsLastSibling();
        tooltip.Hide();
        OnSlotDragged(eventData);
    }

    public void OnSlotDragged(PointerEventData eventData)
    {
        if (dragSource == null)
        {
            return;
        }

        var area = (RectTransform)dragIcon.rectTransform.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local);
        dragIcon.rectTransform.anchoredPosition = local;
    }

    public void OnSlotDropped(ItemSlotView target)
    {
        if (dragSource == null || target == dragSource)
        {
            return;
        }

        bool fromBar = IsBar(dragSource);
        if (IsBar(target))
        {
            SkillDefinition moving = SkillOf(dragSource);
            SkillDefinition there = skills.Loadout.Get(target.Index);
            if (!SkillLoadout.Fits(target.Index, moving))
            {
                SayDoesNotFit(target.Index, moving);
            }
            else if (fromBar && !SkillLoadout.Fits(dragSource.Index, there))
            {
                SayDoesNotFit(dragSource.Index, there);
            }
            else if (fromBar)
            {
                skills.Loadout.Move(dragSource.Index, target.Index);
            }
            else
            {
                skills.Equip(target.Index, moving);
            }
        }
        else if (fromBar)
        {
            // Dropped back into the book: taken off the bar.
            skills.Loadout.Clear(dragSource.Index);
        }
    }

    public void OnSlotDragEnded()
    {
        CancelDrag();
    }

    public void OnSlotHovered(ItemSlotView slot)
    {
        hovered = slot;
        if (dragSource == null)
        {
            ShowTooltip(slot);
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

    private bool IsBar(ItemSlotView slot) => barSlots.Contains(slot);

    private SkillDefinition SkillOf(ItemSlotView slot)
    {
        if (IsBar(slot))
        {
            return skills.Loadout.Get(slot.Index);
        }

        return slot.Index < bookOrder.Count ? bookOrder[slot.Index] : null;
    }

    private void ShowTooltip(ItemSlotView slot)
    {
        SkillDefinition skill = SkillOf(slot);
        if (skill == null && IsBar(slot) && slot.Index < SkillLoadout.SkillSlotCount && skills.Loadout.IsEmpty(slot.Index))
        {
            // An empty slot: what kind it takes, and what pays for that kind.
            var words = new System.Text.StringBuilder();
            foreach (SkillKind kind in SkillLoadout.SlotTakes(slot.Index))
            {
                words.Append(words.Length > 0 ? "\n" : "").Append($"{SkillKinds.Name(kind)}: {SkillKinds.Explain(kind)}");
            }

            tooltip.Show($"{PlayerInputHandler.SkillKeyLabel(slot.Index)}: {SkillLoadout.SlotName(slot.Index)}", UIStyle.Current.Accent, words.ToString());
            return;
        }

        if (skill == null)
        {
            tooltip.Hide();
            return;
        }

        tooltip.Show(SkillText.Name(skill), SkillSchools.ColorOf(skill.School), SkillText.Describe(skill, skills.Loadout.IndexOf(skill)));
    }

    private void CancelDrag()
    {
        dragSource = null;
        if (dragIcon != null)
        {
            dragIcon.gameObject.SetActive(false);
        }
    }

    // ----- Building and refreshing -----

    private void BuildBar()
    {
        if (barSlots.Count != SkillLoadout.SlotCount)
        {
            BuildGrid(barGrid, SkillLoadout.SlotCount, SkillLoadout.SlotCount, barSlots, "Bar Slot ");
        }

        RefreshAll();
    }

    // The frame made larger and the prefab's parts put in their places once, before it is first filled; the book's grid becomes a
    // scrolling view of sections.
    private void LayOut()
    {
        laidOut = true;
        byElement = PlayerPrefs.GetInt(GroupingKey, 0) == 1;
        // As tall as the screen allows, above the HUD's skill bar at the bottom (a small game view cut its title off).
        var window = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        float screen = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.height : WindowHeight + ScreenTop + ScreenBottom;
        windowHeight = Mathf.Clamp(screen - ScreenTop - ScreenBottom, 480f, WindowHeight);
        window.sizeDelta = new Vector2(WindowWidth, windowHeight);
        window.anchoredPosition = new Vector2(window.anchoredPosition.x, (ScreenBottom - ScreenTop) * 0.5f);

        float barWidth = SkillLoadout.SlotCount * 64f + (SkillLoadout.SlotCount - 1) * 6f;
        Place(barGrid, Margin, BarTop, barWidth, 64f);
        Place(Child("Bar Hint"), Margin + barWidth + 20f, BarTop, WindowWidth - barWidth - Margin * 2f - 20f, 64f);
        Place(Child("Book Title"), Margin, BookTop - 42f, 300f, 26f);
        Place(Child("Book Hint"), Margin, windowHeight - FooterHeight + 8f, WindowWidth - Margin * 2f, 22f);

        // Two framed tabs at the right of the book's title: its sections by type (what slot takes it) or by element.
        UIStyle style = UIStyle.Current;
        const float tabWidth = 150f;
        float tabX = WindowWidth - Margin - tabWidth * 2f - 8f;
        Button type = CodeUI.AddTab("By Type", transform, tabX, BookTop - 48f, tabWidth, 38f, style, out TMP_Text typeLabel, out Image typeShown);
        typeLabel.text = "By Type";
        type.onClick.AddListener(() => GroupBy(false));
        Button element = CodeUI.AddTab("By Element", transform, tabX + tabWidth + 8f, BookTop - 48f, tabWidth, 38f, style, out TMP_Text elementLabel, out Image elementShown);
        elementLabel.text = "By Element";
        element.onClick.AddListener(() => GroupBy(true));
        byTypeTab = (typeLabel, typeShown);
        byElementTab = (elementLabel, elementShown);

        // The book: the prefab's grid, now the viewport of a scroll of sections.
        if (bookGrid.TryGetComponent(out GridLayoutGroup grid))
        {
            DestroyImmediate(grid);
        }

        Place(bookGrid, Margin, BookTop, WindowWidth - Margin * 2f, windowHeight - BookTop - FooterHeight);
        var scroll = bookGrid.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        bookGrid.gameObject.AddComponent<RectMask2D>();
        // A clear graphic lets the wheel scroll over the gaps between cards.
        bookGrid.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        bookContent = CodeUI.At("Sections", bookGrid, 0f, 0f, WindowWidth - Margin * 2f, 0f);
        scroll.content = bookContent;
        scroll.viewport = bookGrid;
        ShowGrouping();
    }

    private RectTransform Child(string name) => transform.Find(name) as RectTransform;

    // A part placed from the window's top left corner.
    private static void Place(RectTransform part, float x, float y, float width, float height)
    {
        if (part == null)
        {
            return;
        }

        part.anchorMin = part.anchorMax = new Vector2(0f, 1f);
        part.pivot = new Vector2(0f, 1f);
        part.anchoredPosition = new Vector2(x, -y);
        part.sizeDelta = new Vector2(width, height);
    }

    private void GroupBy(bool element)
    {
        if (byElement == element)
        {
            return;
        }

        byElement = element;
        PlayerPrefs.SetInt(GroupingKey, element ? 1 : 0);
        ShowGrouping();
        RebuildBook();
    }

    private void ShowGrouping()
    {
        UIStyle style = UIStyle.Current;
        byTypeTab.shown.enabled = !byElement;
        byTypeTab.label.color = byElement ? style.Text : style.Accent;
        byElementTab.shown.enabled = byElement;
        byElementTab.label.color = byElement ? style.Accent : style.Text;
    }

    // The book in sections: by type (Ultimates, Powers, Techniques, Weapon Arts: what each bar slot takes), each by element within; or
    // by element (Steel, Beast, Frost, Ember, Wyrd), each by type within. A card is the skill's picture with its name under it.
    private void RebuildBook()
    {
        for (int i = bookContent.childCount - 1; i >= 0; i--)
        {
            Destroy(bookContent.GetChild(i).gameObject);
        }

        bookSlots.Clear();
        bookOrder.Clear();
        UIStyle style = UIStyle.Current;
        float width = bookContent.sizeDelta.x;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + CardGap) / (CardWidth + CardGap)));
        float y = 0f;
        foreach (SkillGroup group in SkillGroups.Of(skills.KnownSkills, byElement))
        {
            TMP_Text heading = CodeUI.Heading(group.Name, bookContent, 0f, y, width * 0.5f, style);
            heading.color = group.Colour ?? style.Accent;
            TMP_Text note = CodeUI.Label("Note", bookContent, width * 0.5f, y + 4f, width * 0.5f, 22f, 14f, style.Muted, TextAlignmentOptions.Right);
            note.text = group.Note;
            y += SectionHeading;
            for (int i = 0; i < group.Skills.Count; i++)
            {
                SkillDefinition skill = group.Skills[i];
                float x = (i % columns) * (CardWidth + CardGap);
                float top = y + (i / columns) * (CardHeight + CardGap);
                RectTransform card = CodeUI.At(skill.name, bookContent, x, top, CardWidth, CardHeight);
                ItemSlotView slot = Instantiate(slotPrefab, card);
                var rect = (RectTransform)slot.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(CardPicture, CardPicture);
                slot.name = "Skill " + bookOrder.Count;
                slot.Bind(this, bookOrder.Count);
                TMP_Text name = CodeUI.Paragraph("Name", card, 0f, CardPicture + 6f, CardWidth, CardHeight - CardPicture - 6f, 14f, style.Text);
                name.alignment = TextAlignmentOptions.Top;
                name.text = SkillText.Name(skill);
                bookSlots.Add(slot);
                bookOrder.Add(skill);
            }

            y += Mathf.CeilToInt(group.Skills.Count / (float)columns) * (CardHeight + CardGap) + SectionGap;
        }

        bookContent.sizeDelta = new Vector2(width, y);
        RefreshAll();
    }

    private void BuildGrid(RectTransform root, int count, int columns, List<ItemSlotView> views, string prefix)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }

        views.Clear();
        var layout = root.GetComponent<GridLayoutGroup>();
        if (layout != null)
        {
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            // A row wider than its frame (the bar has nine slots since 2026-11-14) shrinks its cells to fit.
            float room = root.rect.width - layout.padding.horizontal - layout.spacing.x * (columns - 1);
            if (room > 0f && layout.cellSize.x * columns > room)
            {
                float width = room / columns;
                layout.cellSize = new Vector2(width, width * layout.cellSize.y / Mathf.Max(1f, layout.cellSize.x));
            }
        }

        for (int i = 0; i < count; i++)
        {
            ItemSlotView slot = Instantiate(slotPrefab, root);
            slot.name = prefix + i;
            slot.Bind(this, i);
            views.Add(slot);
        }
    }

    private void OnLoadoutChanged(int index)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        for (int i = 0; i < barSlots.Count; i++)
        {
            SkillDefinition skill = skills.Loadout.Get(i);
            ItemDefinition item = skills.Loadout.GetItem(i);
            // A consumable put on the bar from the bag (a skill key over it there).
            barSlots[i].Show(skill != null ? skill.Icon : item != null ? item.Icon : null, Tint(skill), PlayerInputHandler.SkillKeyLabel(i));
            barSlots[i].SetPlaceholder(skill == null && item == null ? SkillLoadout.SlotBadge(i) : "");
        }

        for (int i = 0; i < bookSlots.Count; i++)
        {
            SkillDefinition skill = i < bookOrder.Count ? bookOrder[i] : null;
            int equipped = skills.Loadout.IndexOf(skill);
            bookSlots[i].Show(skill != null ? skill.Icon : null, Tint(skill), equipped >= 0 ? PlayerInputHandler.SkillKeyLabel(equipped) : "");
        }

        if (hovered != null && dragSource == null)
        {
            ShowTooltip(hovered);
        }
    }

    private static Color Tint(SkillDefinition skill) => skill != null ? SkillSchools.ColorOf(skill.School) : Color.clear;
}
