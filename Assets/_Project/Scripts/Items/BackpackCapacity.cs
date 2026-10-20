using UnityEngine;

/// <summary>
/// Applies the equipped backpack (the character screen's Backpack slot): its extra carry weight to the bag's weight
/// budget, and its extra bag slots to the ItemInventory. Unequipping or losing it shrinks the bag again; a broken
/// backpack (0% durability) gives nothing until it is repaired. Players without gear slots (no PlayerEquipment) use the
/// best backpack carried in the bag or safe pocket instead.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemInventory))]
public sealed class BackpackCapacity : MonoBehaviour
{
    private ItemInventory items;
    private PlayerEquipment equipment;
    private ItemDefinition applied;

    /// <summary>The backpack whose bonus is applied, or null.</summary>
    public ItemDefinition Applied => applied;

    private void Awake()
    {
        items = GetComponent<ItemInventory>();
        equipment = GetComponent<PlayerEquipment>();
    }

    private void OnEnable()
    {
        if (equipment != null)
        {
            equipment.Gear.Slots.SlotChanged += OnSlotChanged;
        }
        else
        {
            items.Container.SlotChanged += OnSlotChanged;
            if (items.SafePocket != null)
            {
                items.SafePocket.SlotChanged += OnSlotChanged;
            }
        }

        Apply();
    }

    private void OnDisable()
    {
        if (equipment != null)
        {
            equipment.Gear.Slots.SlotChanged -= OnSlotChanged;
        }
        else
        {
            items.Container.SlotChanged -= OnSlotChanged;
            if (items.SafePocket != null)
            {
                items.SafePocket.SlotChanged -= OnSlotChanged;
            }
        }

        items.Container.RemoveWeightBonus(this);
        items.SetBonusSlots(0);
        applied = null;
    }

    private void OnSlotChanged(int slot) => Apply();

    private void Apply()
    {
        ItemDefinition best = null;
        int slotBonus = 0;
        if (equipment != null)
        {
            Consider(equipment.Gear.Get(GearSlot.Backpack), ref best, ref slotBonus);
        }
        else
        {
            Consider(items.Container, ref best, ref slotBonus);
            Consider(items.SafePocket, ref best, ref slotBonus);
        }

        items.SetBonusSlots(slotBonus);

        if (best == applied)
        {
            return;
        }

        applied = best;
        if (best == null)
        {
            items.Container.RemoveWeightBonus(this);
        }
        else
        {
            items.Container.SetWeightBonus(this, best.CarryCapacityBonus);
        }
    }

    private static void Consider(ItemContainer container, ref ItemDefinition best, ref int slotBonus)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            Consider(container.GetSlot(i), ref best, ref slotBonus);
        }
    }

    private static void Consider(ItemStack stack, ref ItemDefinition best, ref int slotBonus)
    {
        ItemDefinition item = stack.Item;
        if (item == null || !item.IsBackpack || stack.IsBroken)
        {
            return;
        }

        slotBonus = Mathf.Max(slotBonus, item.BagSlotBonus);
        if (item.CarryCapacityBonus > 0 && (best == null || item.CarryCapacityBonus > best.CarryCapacityBonus))
        {
            best = item;
        }
    }
}