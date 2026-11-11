using System;
using UnityEngine;

/// <summary>
/// The five slots on the skill bar, like V Rising's ability bar. Each slot holds at most one skill or one usable item (a
/// consumable: its key uses one from the bag, as the quickbar does), and a skill or an item is in at most one slot:
/// putting one that is already slotted moves it, swapping with whatever was in the target. Plain C# so it can be tested
/// without a scene; every change is reported per slot through SlotChanged, like ItemContainer.
/// </summary>
public sealed class SkillLoadout
{
    public const int SlotCount = 5;

    private readonly SkillDefinition[] slots = new SkillDefinition[SlotCount];
    private readonly ItemDefinition[] items = new ItemDefinition[SlotCount];

    /// <summary>Raised after a slot's skill changes, with the slot's index.</summary>
    public event Action<int> SlotChanged;

    public bool IsValid(int index) => index >= 0 && index < SlotCount;

    public SkillDefinition Get(int index) => IsValid(index) ? slots[index] : null;

    /// <summary>The item in the slot, or null (a skill or nothing is there).</summary>
    public ItemDefinition GetItem(int index) => IsValid(index) ? items[index] : null;

    public bool IsEmpty(int index) => IsValid(index) && slots[index] == null && items[index] == null;

    public int IndexOf(ItemDefinition item)
    {
        if (item == null)
        {
            return -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (items[i] == item)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Puts a usable item in the slot, in place of what was there (a skill goes back to the book). When the item is
    /// already in another slot, the two slots swap. Null clears the slot. Returns false when nothing changed.
    /// </summary>
    public bool EquipItem(int index, ItemDefinition item)
    {
        if (!IsValid(index) || (items[index] == item && slots[index] == null))
        {
            return false;
        }

        if (item == null)
        {
            return Clear(index);
        }

        int current = IndexOf(item);
        if (current >= 0)
        {
            return Move(current, index);
        }

        Set(index, null, item);
        return true;
    }

    public int IndexOf(SkillDefinition skill)
    {
        if (skill == null)
        {
            return -1;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == skill)
            {
                return i;
            }
        }

        return -1;
    }


    /// <summary>
    /// Puts the skill in the slot. When it is already in another slot, the two slots swap. Null clears the slot. Returns
    /// false when nothing changed.
    /// </summary>
    public bool Equip(int index, SkillDefinition skill)
    {
        if (!IsValid(index) || slots[index] == skill)
        {
            return false;
        }

        if (skill == null)
        {
            Set(index, null, null);
            return true;
        }

        int current = IndexOf(skill);
        if (current >= 0)
        {
            return Move(current, index);
        }

        Set(index, skill, null);
        return true;
    }

    /// <summary>Moves a slot's skill onto another slot, swapping the two. Returns false when nothing moved.</summary>
    public bool Move(int from, int to)
    {
        if (!IsValid(from) || !IsValid(to) || from == to || IsEmpty(from))
        {
            return false;
        }

        SkillDefinition moving = slots[from];
        ItemDefinition movingItem = items[from];
        Set(from, slots[to], items[to]);
        Set(to, moving, movingItem);
        return true;
    }

    public bool Clear(int index)
    {
        if (!IsValid(index) || IsEmpty(index))
        {
            return false;
        }

        Set(index, null, null);
        return true;
    }

    /// <summary>Replaces every slot, for restoring a carried loadout. Duplicates after the first are dropped.</summary>
    public void SetAll(SkillDefinition[] skills)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            SkillDefinition skill = skills != null && i < skills.Length ? skills[i] : null;
            if (skill != null && Array.IndexOf(skills, skill) != i)
            {
                skill = null;
            }

            if (slots[i] != skill)
            {
                Set(i, skill, skill != null ? null : items[i]);
            }
        }
    }

    /// <summary>Puts back the items a carried or saved bar held, in the slots no skill took. Duplicates after the first
    /// are dropped.</summary>
    public void SetItems(ItemDefinition[] saved)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            ItemDefinition item = saved != null && i < saved.Length ? saved[i] : null;
            if (item != null && (Array.IndexOf(saved, item) != i || slots[i] != null))
            {
                item = null;
            }

            if (items[i] != item && slots[i] == null)
            {
                Set(i, null, item);
            }
        }
    }

    public SkillDefinition[] ToArray() => (SkillDefinition[])slots.Clone();

    public ItemDefinition[] ToItemArray() => (ItemDefinition[])items.Clone();

    public int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (IsEmpty(i))
            {
                return i;
            }
        }

        return -1;
    }

    private void Set(int index, SkillDefinition skill, ItemDefinition item)
    {
        slots[index] = skill;
        items[index] = skill != null ? null : item;
        Action<int> handlers = SlotChanged;
        if (handlers == null)
        {
            return;
        }

        // Each listener runs in isolation so a faulty one (a UI bug, say) can't break the loadout.
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<int>)handler)(index);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
