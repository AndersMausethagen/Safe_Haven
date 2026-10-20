using System;

/// <summary>
/// The player's worn gear (the character screen): one slot per GearSlot – armor and jewelry, the main-hand weapon, the
/// backpack and the tool belt. Plain C# so it can be tested without a scene; PlayerEquipment owns one and connects it to
/// the bag, the stats and combat.
/// <para>
/// Equipping moves an item out of a container (the bag) into its slot, swapping whatever was there back; unequipping
/// moves it back. The slots only take matching items (ItemContainer.SlotRule), so drag and drop can't put a helmet on the
/// feet. While bound to a StatSheet, every unbroken item adds its StatBonuses, removed again when it leaves or breaks.
/// </para>
/// </summary>
public sealed class GearSet
{
    private readonly object[] sources = new object[GearSlots.Count];
    private StatSheet stats;

    public GearSet()
    {
        Slots = new ItemContainer(GearSlots.Count, 1) { SlotRule = GearSlots.Accepts };
        for (int i = 0; i < sources.Length; i++)
        {
            sources[i] = new object();
        }

        Slots.SlotChanged += OnSlotChanged;
    }

    /// <summary>The gear slots as a container, indexed by GearSlot. Saved and shown slot by slot.</summary>
    public ItemContainer Slots { get; }

    /// <summary>Raised after an item is equipped, unequipped, worn or repaired, with its slot.</summary>
    public event Action<GearSlot> Changed;

    public ItemStack Get(GearSlot slot) => Slots.GetSlot((int)slot);

    /// <summary>
    /// Whether the stack can be equipped; reason says why not. Broken gear can't be put on: it has to be repaired first.
    /// </summary>
    public static bool CanEquip(ItemStack stack, out string reason)
    {
        reason = null;
        if (stack.IsEmpty)
        {
            return false;
        }

        if (!stack.Item.IsEquippable)
        {
            reason = $"{stack.Item.DisplayName} can't be equipped.";
            return false;
        }

        if (stack.IsBroken)
        {
            reason = $"{stack.Item.DisplayName} is broken. Repair it at a Construction Table first.";
            return false;
        }

        return true;
    }

    /// <summary>Equips the item in a container slot into the slot it fits, preferring an empty one. Swaps otherwise.</summary>
    public bool Equip(ItemContainer from, int index, out string reason)
    {
        ItemStack stack = from != null ? from.GetSlot(index) : ItemStack.Empty;
        if (!CanEquip(stack, out reason))
        {
            return false;
        }

        GearSlots.TryFindSlot(stack.Item, Slots, out GearSlot slot);
        return EquipTo(from, index, slot, out reason);
    }

    /// <summary>Equips the item in a container slot into one particular gear slot, swapping what was there back.</summary>
    public bool EquipTo(ItemContainer from, int index, GearSlot slot, out string reason)
    {
        ItemStack stack = from != null ? from.GetSlot(index) : ItemStack.Empty;
        if (!CanEquip(stack, out reason))
        {
            return false;
        }

        if (!GearSlots.Accepts(slot, stack.Item))
        {
            reason = $"{stack.Item.DisplayName} doesn't go in the {GearSlots.Label(slot)} slot.";
            return false;
        }

        if (from.MoveTo(index, Slots, (int)slot))
        {
            return true;
        }

        // The swap failed (the source slot is locked): take the old item off first, then put the new one on.
        if (!Unequip(slot, from, out reason))
        {
            return false;
        }

        return from.MoveTo(index, Slots, (int)slot);
    }

    /// <summary>Moves the item in a gear slot into the first free slot of a container (the bag).</summary>
    public bool Unequip(GearSlot slot, ItemContainer into, out string reason)
    {
        reason = null;
        ItemStack stack = Get(slot);
        if (stack.IsEmpty || into == null)
        {
            return false;
        }

        int free = into.FirstEmptySlotFor(stack.Item);
        if (free < 0)
        {
            reason = "Your bag is full.";
            return false;
        }

        return Slots.MoveTo((int)slot, into, free);
    }

    /// <summary>
    /// Equips unbroken gear from a container into slots that are still empty, never replacing anything. Used when gear
    /// arrives (crafted, looted) and to move gear out of the bag of saves made before gear slots existed.
    /// Returns how many items were equipped.
    /// </summary>
    public int AutoEquipFrom(ItemContainer from, ItemDefinition only = null)
    {
        int equipped = 0;
        if (from == null)
        {
            return equipped;
        }

        for (int i = 0; i < from.SlotCount; i++)
        {
            ItemStack stack = from.GetSlot(i);
            if (stack.IsEmpty || (only != null && stack.Item != only) || !CanEquip(stack, out _))
            {
                continue;
            }

            for (int s = 0; s < GearSlots.Count; s++)
            {
                if (Slots.GetSlot(s).IsEmpty && GearSlots.Accepts((GearSlot)s, stack.Item) && from.MoveTo(i, Slots, s))
                {
                    equipped++;
                    break;
                }
            }
        }

        return equipped;
    }

    /// <summary>The tier of the equipped tool of this kind, or 0 when there is none or it is broken.</summary>
    public int ToolTier(ToolType tool) => Slots.BestToolTier(tool);

    /// <summary>Starts adding the gear's stat bonuses to the sheet, and keeps them up to date. Null unbinds.</summary>
    public void Bind(StatSheet sheet)
    {
        if (stats == sheet)
        {
            return;
        }

        RemoveAllStats();
        stats = sheet;
        for (int i = 0; i < GearSlots.Count; i++)
        {
            ApplyStats(i);
        }
    }

    private void OnSlotChanged(int index)
    {
        ApplyStats(index);
        Changed?.Invoke((GearSlot)index);
    }

    private void ApplyStats(int index)
    {
        if (stats == null)
        {
            return;
        }

        stats.RemoveModifiersFrom(sources[index]);
        ItemStack stack = Slots.GetSlot(index);
        if (stack.IsEmpty || stack.IsBroken)
        {
            return;
        }

        foreach (StatBonus bonus in stack.Item.StatBonuses)
        {
            if (bonus.value != 0f)
            {
                stats.AddModifier(new StatModifier(bonus.stat, bonus.op, bonus.value, sources[index]));
            }
        }
    }

    private void RemoveAllStats()
    {
        if (stats == null)
        {
            return;
        }

        foreach (object source in sources)
        {
            stats.RemoveModifiersFrom(source);
        }
    }
}
