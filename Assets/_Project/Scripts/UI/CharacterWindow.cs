using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The character screen (C), laid out like World of Warcraft's: armor slots down both sides, the main-hand weapon,
/// backpack and tool belt along the bottom, and the player's stats in the middle. Right-click a worn item to take it off
/// into the bag; drag items between this window and the bag (see ItemDrag) to put them on, take them off or swap them.
/// Everything shown comes from the player's PlayerEquipment, Combatant stats and Damageable health; the window only
/// displays them and forwards input.
/// </summary>
public sealed class CharacterWindow : MonoBehaviour, IItemSlotHandler
{
    // Slot order in each column, top to bottom (left to right for the bottom row).
    // Armor down the left, jewellery and the belt down the right (see GearSlots for the retired slots).
    private static readonly GearSlot[] LeftSlots = { GearSlot.Head, GearSlot.Chest, GearSlot.Hands, GearSlot.Feet };
    private static readonly GearSlot[] RightSlots = { GearSlot.Neck, GearSlot.Waist, GearSlot.Finger1, GearSlot.Light };
    private static readonly GearSlot[] BottomSlots = { GearSlot.MainHand, GearSlot.OffHand, GearSlot.Backpack, GearSlot.AxeTool, GearSlot.PickaxeTool, GearSlot.SickleTool };

    [Tooltip("Left empty, the player's PlayerEquipment is used.")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private ItemSlotView slotPrefab;
    [Tooltip("Parents for the gear slots. Should have layout groups.")]
    [SerializeField] private RectTransform leftColumn;
    [SerializeField] private RectTransform rightColumn;
    [SerializeField] private RectTransform bottomRow;
    [Tooltip("Shows the player's stats with rich text.")]
    [SerializeField] private TMP_Text statsLabel;
    [SerializeField] private ItemTooltipView tooltip;
    [Tooltip("Follows the pointer while a stack is dragged. Should not block raycasts.")]
    [SerializeField] private Image dragIcon;
    [Tooltip("Frame tint of empty gear slots, so the slot names stay readable.")]
    [SerializeField] private Color emptySlotTint = new Color(0.35f, 0.37f, 0.42f, 1f);

    private readonly Dictionary<GearSlot, ItemSlotView> views = new Dictionary<GearSlot, ItemSlotView>();
    private ItemContainer gear;
    private Combatant combatant;
    private Damageable health;
    private ItemSlotView hovered;
    private int dragFrom = -1;
    private bool statsDirty;

    private void OnEnable()
    {
        if (equipment == null)
        {
            equipment = Player.Current != null ? Player.Current.Equipment : null;
        }

        if (equipment == null || slotPrefab == null || leftColumn == null || rightColumn == null || bottomRow == null
            || statsLabel == null || tooltip == null || dragIcon == null)
        {
            Debug.LogError("CharacterWindow is missing a reference (player equipment, slot prefab, slot parents, stats label, tooltip or drag icon).", this);
            enabled = false;
            return;
        }

        gear = equipment.Gear.Slots;
        combatant = equipment.GetComponent<Combatant>();
        health = equipment.GetComponent<Damageable>();
        gear.SlotChanged += OnGearSlotChanged;
        equipment.Changed += MarkStatsDirty;
        if (combatant != null)
        {
            combatant.Stats.Changed += OnStatChanged;
        }

        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
        }

        BuildSlots();
        for (int i = 0; i < GearSlots.Count; i++)
        {
            ShowSlot((GearSlot)i);
        }

        RefreshStats();
        dragIcon.gameObject.SetActive(false);
        tooltip.Hide();
    }

    private void OnDisable()
    {
        if (gear != null)
        {
            gear.SlotChanged -= OnGearSlotChanged;
        }

        if (equipment != null)
        {
            equipment.Changed -= MarkStatsDirty;
        }

        if (combatant != null)
        {
            combatant.Stats.Changed -= OnStatChanged;
        }

        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }

        CancelDrag();
        hovered = null;
        tooltip?.Hide();
    }

    private void Update()
    {
        Vector2 pointer = GameInput.PointerPosition;
        tooltip.Follow(pointer);

        // Several stats change at once when gear swaps, so the text is rebuilt once per frame at most.
        if (statsDirty)
        {
            RefreshStats();
        }
    }

    // ----- Slots -----

    public void OnSlotClicked(ItemSlotView slot, PointerEventData eventData)
    {
        if (!eventData.dragging && eventData.button == PointerEventData.InputButton.Right && !gear.GetSlot(slot.Index).IsEmpty)
        {
            equipment.Unequip((GearSlot)slot.Index);
        }
    }

    public void OnSlotDragStarted(ItemSlotView slot, PointerEventData eventData)
    {
        ItemStack stack = gear.GetSlot(slot.Index);
        if (eventData.button != PointerEventData.InputButton.Left || stack.IsEmpty)
        {
            return;
        }

        dragFrom = slot.Index;
        ItemDrag.Begin(gear, slot.Index);
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
        ItemDrag.DropOn(gear, target.Index);
    }

    public void OnSlotDragEnded()
    {
        CancelDrag();
    }

    public void OnSlotHovered(ItemSlotView slot)
    {
        hovered = slot;
        if (dragFrom >= 0)
        {
            return;
        }

        ItemStack stack = gear.GetSlot(slot.Index);
        if (stack.IsEmpty)
        {
            tooltip.Show(GearSlots.Label((GearSlot)slot.Index), new Color(0.92f, 0.87f, 0.74f, 1f), $"<color={UIStyle.Current.MutedHex}>Empty. Drag gear here from the bag, or right-click it there.</color>");
        }
        else
        {
            tooltip.Show(stack);
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
        if (views.Count == GearSlots.Count)
        {
            return;
        }

        views.Clear();
        BuildColumn(leftColumn, LeftSlots);
        BuildColumn(rightColumn, RightSlots);
        BuildColumn(bottomRow, BottomSlots);
    }

    private void BuildColumn(RectTransform root, GearSlot[] order)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }

        var horizontal = root == bottomRow ? root.GetComponent<HorizontalLayoutGroup>() : null;
        if (horizontal != null) horizontal.spacing = Mathf.Min(horizontal.spacing, 4f);
        foreach (GearSlot slot in order)
        {
            ItemSlotView view = Instantiate(slotPrefab, root);
            if (horizontal != null)
            {
                float width = (root.rect.width - horizontal.padding.horizontal - horizontal.spacing * (order.Length - 1)) / order.Length;
                var rect = (RectTransform)view.transform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(rect.rect.width, width));
            }
            view.name = "Gear " + slot;
            view.Bind(this, (int)slot);
            views[slot] = view;
        }
    }

    private void ShowSlot(GearSlot slot)
    {
        if (!views.TryGetValue(slot, out ItemSlotView view))
        {
            return;
        }

        ItemStack stack = gear.GetSlot((int)slot);
        if (stack.IsEmpty)
        {
            view.Show(null, emptySlotTint, ShortLabel(slot));
        }
        else
        {
            view.Show(stack);
        }

        if (hovered == view && dragFrom < 0)
        {
            OnSlotHovered(view);
        }
    }

    private static string ShortLabel(GearSlot slot)
    {
        switch (slot)
        {
            case GearSlot.Finger1:
            case GearSlot.Finger2: return "Ring";
            case GearSlot.Chest: return "Body";
            case GearSlot.Waist: return "Belt";
            case GearSlot.Neck: return "Amulet";
            case GearSlot.MainHand: return "Weapon";
            case GearSlot.OffHand: return "Off Hand";
            case GearSlot.Backpack: return "Pack";
            case GearSlot.AxeTool: return "Axe";
            case GearSlot.PickaxeTool: return "Pickaxe";
            case GearSlot.SickleTool: return "Sickle";
            case GearSlot.Light: return "Light";
            default: return slot.ToString();
        }
    }

    private void OnGearSlotChanged(int index)
    {
        ShowSlot((GearSlot)index);
        statsDirty = true;
    }

    // ----- Stats -----

    private void MarkStatsDirty() => statsDirty = true;

    private void OnStatChanged(StatType stat) => statsDirty = true;

    private void OnHealthChanged(float current, float max) => statsDirty = true;

    private void RefreshStats()
    {
        statsDirty = false;
        if (combatant == null)
        {
            statsLabel.text = "";
            return;
        }

        StatSheet stats = combatant.Stats;
        CombatRules rules = combatant.Rules;
        float armor = stats.Get(StatType.Armor);
        var text = new StringBuilder();
        text.Append($"<color={UIStyle.Current.AccentHex}><b>Attributes</b></color>\n");
        text.Append(Line("Gear Score", GearScore.Of(combatant.gameObject).ToString()));
        if (health != null)
        {
            text.Append(Line("Health", $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}"));
        }

        text.Append(Line("Strength", Number(stats.Get(StatType.Strength))));
        text.Append(Line("Armor", $"{Number(armor)}  <color={UIStyle.Current.MutedHex}>({Mathf.RoundToInt(rules.GetArmorReduction(armor) * 100f)}% less physical damage)</color>"));

        text.Append($"\n<color={UIStyle.Current.AccentHex}><b>Offense</b></color>\n");
        text.Append(Line("Physical Power", Number(rules.PowerFor(stats, DamageType.Physical))));
        text.Append(Line("Spell Power", Number(rules.PowerFor(stats, DamageType.Fire))));
        text.Append(Line("Physical Crit", $"{Percent(stats.Get(StatType.PhysicalCritChance))}  <color={UIStyle.Current.MutedHex}>×{1f + stats.Get(StatType.PhysicalCritDamage):0.##}</color>"));
        text.Append(Line("Spell Crit", $"{Percent(stats.Get(StatType.SpellCritChance))}  <color={UIStyle.Current.MutedHex}>×{1f + stats.Get(StatType.SpellCritDamage):0.##}</color>"));
        if (equipment.MainHand.IsEmpty || equipment.MainHand.IsBroken)
        {
            text.Append(Line("Weapon", $"<color={UIStyle.Current.MutedHex}>Unarmed</color>"));
        }
        else
        {
            text.Append(Line("Weapon", $"{equipment.MainHand.Item.DisplayName}  <color={UIStyle.Current.MutedHex}>{Mathf.RoundToInt(equipment.MainHand.Item.Weapon.Damage)} dmg</color>"));
        }

        text.Append($"\n<color={UIStyle.Current.AccentHex}><b>Resistances</b></color>\n");
        text.Append(Line("Physical", Percent(rules.ClampResistance(stats.Get(StatType.PhysicalResistance)))));
        text.Append(Line("Fire", Percent(rules.ClampResistance(stats.Get(StatType.FireResistance)))));
        text.Append(Line("Frost", Percent(rules.ClampResistance(stats.Get(StatType.FrostResistance)))));
        text.Append(Line("Holy", Percent(rules.ClampResistance(stats.Get(StatType.HolyResistance)))));

        text.Append($"\n<color={UIStyle.Current.AccentHex}><b>Gathering</b></color>\n");
        ToolType[] tools = { ToolType.Axe, ToolType.Pickaxe, ToolType.Sickle };
        foreach (ToolType tool in tools)
        {
            int tier = equipment.Gear.ToolTier(tool);
            text.Append(Line(tool.ToString(), tier > 0
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(Tiers.ColorOf(tier))}>Tier {Tiers.ToRoman(tier)}</color>"
                : $"<color={UIStyle.Current.MutedHex}>none</color>"));
        }

        statsLabel.text = text.ToString();
    }

    private static string Line(string name, string value) => $"{name}<pos=52%>{value}\n";

    private static string Number(float value) => value.ToString("0.#");

    private static string Percent(float fraction) => $"{fraction * 100f:0.#}%";
}
