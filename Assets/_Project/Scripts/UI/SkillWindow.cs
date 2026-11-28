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
    // UX pass 2026-11-28 (Docs/MenuUXResearch_2026-11-28.md §3): a detail pane on the right for the skill chosen (hover a short card,
    // select the full one), every skill in the game in the book (those not learned as dark shapes with where they are learned), each
    // card and bar slot in its kind's colour, and while a skill is dragged the bar slots that can't take it fade.
    private const float DetailWidth = 300f;
    private const float DetailGap = 20f;
    private const float BookWidth = WindowWidth - Margin * 2f - DetailWidth - DetailGap;
    private const float DetailX = WindowWidth - Margin - DetailWidth;
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
    private PlayerCombat combat;
    private bool byElement;
    private RectTransform bookContent;
    private (TMP_Text label, Image shown) byTypeTab;
    private (TMP_Text label, Image shown) byElementTab;
    private RectTransform detail;
    private Image detailIcon;
    private TMP_Text detailName;
    private TMP_Text detailKind;
    private TMP_Text detailBody;
    private TMP_Text detailHint;
    private SkillDefinition selected;
    // A gold rim round each book card's picture, lit on the one chosen (the pane's skill is seen in the book too).
    private readonly List<Image> bookRims = new List<Image>();
    private Button equipButton;
    private float bodyHeight = 160f;
    private RectTransform detailView;

    private bool Knows(SkillDefinition skill) => skill != null && skills.IsKnown(skill);

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

        Fit();
        skills.Loadout.SlotChanged += OnLoadoutChanged;
        skills.KnownSkillsChanged += RebuildBook;
        combat = skills.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.WeaponChanged += OnWeaponChanged;
        }

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

        if (combat != null)
        {
            combat.WeaponChanged -= OnWeaponChanged;
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
        if (eventData.dragging)
        {
            return;
        }

        // A left click (or a pad's press) chooses the skill: its whole card in the pane on the right.
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // A bar slot clicked while a learned skill is chosen that it takes (and doesn't hold): the skill goes there.
            if (IsBar(slot) && Knows(selected) && slot.Index < SkillLoadout.SkillSlotCount && SkillLoadout.Fits(slot.Index, selected)
                && skills.Loadout.Get(slot.Index) != selected)
            {
                skills.Equip(slot.Index, selected);
                return;
            }

            SkillDefinition chosen = SkillOf(slot);
            if (chosen != null)
            {
                Select(chosen);
            }

            return;
        }

        if (eventData.button != PointerEventData.InputButton.Right)
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

        if (!Knows(skill))
        {
            GameMessages.Post($"{SkillText.Name(skill)} isn't learned yet. {SkillSources.Of(skill, skills.Settings)}", MessageKind.Info);
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
        if (eventData.button != PointerEventData.InputButton.Left || skill == null || !Knows(skill))
        {
            return;
        }

        dragSource = slot;
        dragIcon.sprite = skill.Icon;
        dragIcon.gameObject.SetActive(skill.Icon != null);
        dragIcon.transform.SetAsLastSibling();
        tooltip.Hide();
        ShowFits(skill);
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

        // Hover: a short card (the kind and how it is had); the whole of it is in the pane once chosen.
        string kindLine = $"<color=#{ColorUtility.ToHtmlStringRGB(SkillKinds.Colour(skill.Kind))}>{SkillKinds.Name(skill.Kind)}</color> · {skill.School}";
        string body = Knows(skill)
            ? $"{kindLine}\n{SkillText.Costs(skill)}\n<color={UIStyle.Current.MutedHex}>Click for the whole of it. Drag it to the bar, or right-click.</color>"
            : $"{kindLine}\n<color={UIStyle.Current.WarningHex}>Not learned yet.</color> {SkillSources.Of(skill, skills.Settings)}";
        tooltip.Show(SkillText.Name(skill), SkillKinds.Colour(skill.Kind), body);
    }

    private void CancelDrag()
    {
        dragSource = null;
        if (dragIcon != null)
        {
            dragIcon.gameObject.SetActive(false);
        }

        ShowFits(null);
    }

    // While a skill is dragged, the bar slots that can't take it fade (Diablo IV, Last Epoch: where it may go is seen before it drops).
    private void ShowFits(SkillDefinition moving)
    {
        foreach (ItemSlotView slot in barSlots)
        {
            if (slot == null)
            {
                continue;
            }

            CanvasGroup group = slot.TryGetComponent(out CanvasGroup had) ? had : slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = moving == null || SkillLoadout.Fits(slot.Index, moving) ? 1f : 0.3f;
        }
    }

    // ----- The chosen skill's card -----

    private void Select(SkillDefinition skill)
    {
        selected = skill;
        for (int i = 0; i < bookRims.Count && i < bookOrder.Count; i++)
        {
            bookRims[i].enabled = bookOrder[i] == selected;
        }

        ShowDetail();
    }

    // The pane's button (and a click on a bar slot with a skill chosen): onto the bar without dragging, for a pad as for a mouse.
    private void EquipSelected(int slot)
    {
        if (!Knows(selected))
        {
            return;
        }

        if (slot < 0)
        {
            slot = skills.Loadout.FirstFittingEmpty(selected);
            for (int i = 0; slot < 0 && i < SkillLoadout.SkillSlotCount; i++)
            {
                slot = SkillLoadout.Fits(i, selected) ? i : -1;
            }
        }

        if (slot < 0 || !SkillLoadout.Fits(slot, selected))
        {
            SayDoesNotFit(Mathf.Max(0, slot), selected);
            return;
        }

        skills.Equip(slot, selected);
    }

    private void ShowDetail()
    {
        if (detail == null)
        {
            return;
        }

        UIStyle style = UIStyle.Current;
        SkillDefinition skill = selected;
        detail.gameObject.SetActive(skill != null);
        if (skill == null)
        {
            return;
        }

        bool known = Knows(skill);
        detailIcon.sprite = skill.Icon;
        detailIcon.enabled = skill.Icon != null;
        detailIcon.color = known ? Color.white : new Color(0.46f, 0.42f, 0.37f, 0.85f);
        detailName.text = SkillText.Name(skill);
        detailName.color = known ? style.Text : style.Muted;
        detailKind.text = $"<color=#{ColorUtility.ToHtmlStringRGB(SkillKinds.Colour(skill.Kind))}>{SkillKinds.Name(skill.Kind)}</color>  ·  {skill.School}";
        int equipped = skills.Loadout.IndexOf(skill);
        detailBody.text = known ? SkillText.Describe(skill, equipped)
            : $"<color={style.WarningHex}>Not learned yet.</color>\n{SkillSources.Of(skill, skills.Settings)}\n\n{SkillKinds.Explain(skill.Kind)}";
        detailHint.text = !known ? "" : equipped >= 0 ? $"On the bar: {PlayerInputHandler.SkillKeyLabel(equipped)}. Right-click to take it off."
            : "Or click a slot of its colour on the bar.";
        equipButton.gameObject.SetActive(known && equipped < 0);
        // Without the button, the words take its room.
        detailView.sizeDelta = new Vector2(DetailWidth - 36f, bodyHeight + (equipButton.gameObject.activeSelf ? 0f : 50f));
        detailBody.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void BuildDetail()
    {
        UIStyle style = UIStyle.Current;
        detail = HearthKit.Card(transform, "Detail", DetailX, BookTop - 48f, DetailWidth, 400f, 0.45f);
        RectTransform frame = HearthKit.Card(detail, "Picture", (DetailWidth - 96f) * 0.5f, 16f, 96f, 96f, 0.6f);
        detailIcon = CodeUI.At("Icon", frame, 6f, 6f, 84f, 84f).gameObject.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        detailName = CodeUI.Label("Name", detail, 14f, 122f, DetailWidth - 28f, 30f, 22f, style.Text, TextAlignmentOptions.Center);
        style.Title(detailName);
        detailName.enableAutoSizing = true;
        detailName.fontSizeMin = 15f;
        detailName.fontSizeMax = 22f;
        detailKind = CodeUI.Label("Kind", detail, 14f, 154f, DetailWidth - 28f, 22f, 15f, style.Muted, TextAlignmentOptions.Center);
        detailKind.richText = true;
        HearthKit.Rule(detail, 24f, 184f, DetailWidth - 48f);
        // The words scroll when they are longer than the pane (design review 2026-11-28: a long art's last line was cut mid-word).
        detailView = CodeUI.At("Words View", detail, 18f, 194f, DetailWidth - 36f, 160f);
        detailView.gameObject.AddComponent<RectMask2D>();
        detailView.gameObject.AddComponent<Image>().color = Color.clear;
        detailBody = CodeUI.Paragraph("Words", detailView, 0f, 0f, DetailWidth - 36f, 0f, 14f, style.Text);
        detailBody.richText = true;
        detailBody.overflowMode = TextOverflowModes.Overflow;
        detailBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var wordsScroll = detailView.gameObject.AddComponent<ScrollRect>();
        wordsScroll.viewport = detailView;
        wordsScroll.content = detailBody.rectTransform;
        wordsScroll.horizontal = false;
        wordsScroll.movementType = ScrollRect.MovementType.Clamped;
        wordsScroll.scrollSensitivity = 30f;
        detailHint = CodeUI.Paragraph("Hint", detail, 18f, 360f, DetailWidth - 36f, 34f, 13f, style.Muted);
        TMP_Text equip = CodeUI.AddButton("Equip", detail, (DetailWidth - 180f) * 0.5f, 300f, 180f, UIRole.Kind.PrimaryButton, () => EquipSelected(-1), out equipButton);
        equip.text = "Put on the bar";
        equip.fontSize = 17f;
        ((RectTransform)equipButton.transform).sizeDelta = new Vector2(180f, 40f);
        equip.rectTransform.sizeDelta = new Vector2(180f, 40f);
        detail.gameObject.SetActive(false);
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
        float barWidth = SkillLoadout.SlotCount * 64f + (SkillLoadout.SlotCount - 1) * 6f;
        Place(barGrid, Margin, BarTop, barWidth, 64f);
        Place(Child("Bar Hint"), Margin + barWidth + 20f, BarTop, WindowWidth - barWidth - Margin * 2f - 20f, 64f);
        Place(Child("Book Title"), Margin, BookTop - 42f, 300f, 26f);
        // The book shows every skill now, learned or not (keeping the heading's icon, if the kit put one before it).
        TMP_Text bookTitle = Child("Book Title") != null ? Child("Book Title").GetComponentInChildren<TMP_Text>(true) : null;
        if (bookTitle != null)
        {
            bookTitle.text = bookTitle.text.Replace("Known Skills", "Skill Book");
        }

        // Two framed tabs at the right of the book's title: its sections by type (what slot takes it) or by element.
        UIStyle style = UIStyle.Current;
        const float tabWidth = 150f;
        float tabX = Margin + BookWidth - tabWidth * 2f - 8f;
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

        var scroll = bookGrid.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        bookGrid.gameObject.AddComponent<RectMask2D>();
        // A clear graphic lets the wheel scroll over the gaps between cards.
        bookGrid.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        bookContent = CodeUI.At("Sections", bookGrid, 0f, 0f, BookWidth, 0f);
        scroll.content = bookContent;
        scroll.viewport = bookGrid;
        // A bar skill dropped anywhere in the book (between cards, on a heading) is taken off the bar, as on a card (review 2026-11-27:
        // only the cards took the drop, the rest lost it).
        bookGrid.gameObject.AddComponent<DropArea>().Dropped += () =>
        {
            if (dragSource != null && IsBar(dragSource))
            {
                skills.Loadout.Clear(dragSource.Index);
            }
        };
        ShowGrouping();
        BuildDetail();
    }

    // As tall as the screen allows, above the HUD's skill bar at the bottom (a small game view cut its title off): measured each time
    // it opens, so a new resolution is fitted too.
    private void Fit()
    {
        var window = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        float screen = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.height : WindowHeight + ScreenTop + ScreenBottom;
        windowHeight = Mathf.Clamp(screen - ScreenTop - ScreenBottom, 480f, WindowHeight);
        window.sizeDelta = new Vector2(WindowWidth, windowHeight);
        window.anchoredPosition = new Vector2(window.anchoredPosition.x, (ScreenBottom - ScreenTop) * 0.5f);
        Place(Child("Book Hint"), Margin, windowHeight - FooterHeight + 8f, WindowWidth - Margin * 2f, 22f);
        Place(bookGrid, Margin, BookTop, BookWidth, windowHeight - BookTop - FooterHeight);
        if (detail != null)
        {
            float height = windowHeight - (BookTop - 48f) - FooterHeight;
            Place(detail, DetailX, BookTop - 48f, DetailWidth, height);
            bodyHeight = Mathf.Max(60f, height - 194f - 104f);
            Place((RectTransform)equipButton.transform, (DetailWidth - 180f) * 0.5f, height - 96f, 180f, 40f);
            Place(detailHint.rectTransform, 18f, height - 48f, DetailWidth - 36f, 38f);
        }
    }

    // A weapon taken up or put down while the window is open: the weapon-art cards take the new art's name.
    private void OnWeaponChanged(WeaponDefinition weapon) => RebuildBook();

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
        // A card under the pointer or being dragged is about to go: its tooltip and drag go with it (its Index would name a skill of
        // the new order, and a destroyed card never ends its drag).
        if (hovered != null && bookSlots.Contains(hovered))
        {
            hovered = null;
            tooltip.Hide();
        }

        if (dragSource != null && bookSlots.Contains(dragSource))
        {
            CancelDrag();
        }

        for (int i = bookContent.childCount - 1; i >= 0; i--)
        {
            Destroy(bookContent.GetChild(i).gameObject);
        }

        bookSlots.Clear();
        bookOrder.Clear();
        bookRims.Clear();
        UIStyle style = UIStyle.Current;
        float width = bookContent.sizeDelta.x;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + CardGap) / (CardWidth + CardGap)));
        float y = 0f;
        // Every skill there is, the known first in each section; those not learned are dark shapes, goals with where they are had.
        var shown = new List<SkillDefinition>(skills.KnownSkills);
        foreach (SkillDefinition other in SkillSources.All())
        {
            if (!shown.Contains(other))
            {
                shown.Add(other);
            }
        }

        foreach (SkillGroup group in SkillGroups.Of(shown, byElement))
        {
            group.Skills.Sort((a, b) => Knows(b).CompareTo(Knows(a)));
            int knownHere = group.Skills.FindAll(Knows).Count;
            TMP_Text heading = CodeUI.Heading(group.Name, bookContent, 0f, y, width * 0.5f, style);
            heading.color = byElement || group.Skills.Count == 0 ? style.Accent : SkillKinds.Colour(group.Skills[0].Kind);
            TMP_Text note = CodeUI.Label("Note", bookContent, width * 0.5f, y + 4f, width * 0.5f, 22f, 14f, style.Muted, TextAlignmentOptions.Right);
            note.richText = true;
            note.text = HearthKit.ToneNumbers($"{knownHere}/{group.Skills.Count}  ·  {group.Note}");
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
                Image rim = HearthKit.Rim(rect, 1f);
                rim.color = UIStyle.Current.AccentBright;
                rim.enabled = false;
                bookRims.Add(rim);
                // The kind's colour under the picture, as on the bar's slots.
                Image kind = CodeUI.At("Kind", card, (CardWidth - 40f) * 0.5f, CardPicture + 3f, 40f, 3f).gameObject.AddComponent<Image>();
                kind.color = SkillKinds.Colour(skill.Kind);
                kind.raycastTarget = false;
                TMP_Text name = CodeUI.Paragraph("Name", card, 0f, CardPicture + 9f, CardWidth, CardHeight - CardPicture - 9f, 14f, Knows(skill) ? style.Text : style.Muted);
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
            // An empty slot's word in the colour of the kind it takes.
            Color? takes = null;
            foreach (SkillKind slotKind in SkillLoadout.SlotTakes(i))
            {
                takes = SkillKinds.Colour(slotKind); // the last it takes: slot 5's Ultimate, not its Power
            }

            barSlots[i].SetPlaceholder(skill == null && item == null ? SkillLoadout.SlotBadge(i) : "", takes);
        }

        for (int i = 0; i < bookSlots.Count; i++)
        {
            SkillDefinition skill = i < bookOrder.Count ? bookOrder[i] : null;
            int equipped = skills.Loadout.IndexOf(skill);
            bookSlots[i].Show(skill != null ? skill.Icon : null, Tint(skill), equipped >= 0 ? PlayerInputHandler.SkillKeyLabel(equipped) : "");
            if (skill != null && !Knows(skill))
            {
                // Not learned: the locked frame and a grey shape (design review: a dark tint over a dark icon read as an empty slot).
                bookSlots[i].Show(skill.Icon, Color.clear, "", "?", isLocked: true);
                bookSlots[i].SetSilhouette();
            }

            if (i < bookRims.Count)
            {
                bookRims[i].enabled = skill != null && skill == selected;
            }
        }

        if (selected == null || !bookOrder.Contains(selected))
        {
            selected = skills.Loadout.Get(0) ?? (skills.KnownSkills.Count > 0 ? skills.KnownSkills[0] : null);
        }

        ShowDetail();

        if (hovered != null && dragSource == null)
        {
            ShowTooltip(hovered);
        }
    }

    // In this window a hue means a kind, one colour one meaning (design review 2026-11-28: the kinds' and the elements' colours were near
    // twins, so blue meant Technique on the strip and Frost on the frame). The element is named in words.
    private static Color Tint(SkillDefinition skill) => skill != null ? SkillKinds.Colour(skill.Kind) : Color.clear;
}
