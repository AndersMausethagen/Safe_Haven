using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The skill window (K), laid out and handled like the bag: the five skill bar slots on top and the book of known skills
/// below, both made of the same ItemSlotView cells, tooltip and drag icon as the bag window. Drag a known skill onto a bar
/// slot to equip it (it swaps with what was there, or moves if it was already slotted), drag between bar slots to
/// rearrange, and drag a bar skill back into the book to take it off. Right-click a known skill to slot it in the first
/// free slot (or take it off when it is slotted); right-click a bar slot to empty it. Everything shown comes from the
/// player's PlayerSkills.
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
    [Tooltip("Columns of the book grid.")]
    [SerializeField, Min(1)] private int bookColumns = 8;
    [Tooltip("Rows always shown in the book, so it reads as a grid even with few skills.")]
    [SerializeField, Min(1)] private int bookMinimumRows = 2;

    private readonly List<ItemSlotView> barSlots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> bookSlots = new List<ItemSlotView>();
    private ItemSlotView dragSource;
    private ItemSlotView hovered;

    private void OnEnable()
    {
        skills = skills != null ? skills : Player.Current != null ? Player.Current.Skills : FindAnyObjectByType<PlayerSkills>();
        if (skills == null || barGrid == null || bookGrid == null || slotPrefab == null || tooltip == null || dragIcon == null)
        {
            Debug.LogError("SkillWindow is missing a reference (player skills, bar grid, book grid, slot prefab, tooltip or drag icon).", this);
            enabled = false;
            return;
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
        tooltip?.Hide();
    }

    private void Update()
    {
        Vector2 pointer = UnityEngine.InputSystem.Pointer.current != null
            ? UnityEngine.InputSystem.Pointer.current.position.ReadValue()
            : Vector2.zero;
        tooltip.Follow(pointer);
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

        int free = skills.Loadout.FirstEmptySlot();
        if (free < 0)
        {
            GameMessages.Post("The skill bar is full. Drag the skill onto a slot to replace one.", MessageKind.Warning);
            return;
        }

        skills.Equip(free, skill);
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
            if (fromBar)
            {
                skills.Loadout.Move(dragSource.Index, target.Index);
            }
            else
            {
                skills.Equip(target.Index, SkillOf(dragSource));
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

        return slot.Index < skills.KnownSkills.Count ? skills.KnownSkills[slot.Index] : null;
    }

    private void ShowTooltip(ItemSlotView slot)
    {
        SkillDefinition skill = SkillOf(slot);
        if (skill == null)
        {
            tooltip.Hide();
            return;
        }

        tooltip.Show(skill.DisplayName, SkillSchools.ColorOf(skill.School), SkillText.Describe(skill, skills.Loadout.IndexOf(skill)));
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

    private void RebuildBook()
    {
        int rows = Mathf.Max(bookMinimumRows, Mathf.CeilToInt(skills.KnownSkills.Count / (float)bookColumns));
        int count = rows * bookColumns;
        if (bookSlots.Count != count)
        {
            BuildGrid(bookGrid, count, bookColumns, bookSlots, "Skill ");
        }

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
            barSlots[i].Show(skill != null ? skill.Icon : null, Tint(skill), PlayerInputHandler.SkillKeyLabel(i));
        }

        for (int i = 0; i < bookSlots.Count; i++)
        {
            SkillDefinition skill = i < skills.KnownSkills.Count ? skills.KnownSkills[i] : null;
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
