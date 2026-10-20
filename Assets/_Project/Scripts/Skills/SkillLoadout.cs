using System;
using UnityEngine;

/// <summary>
/// The five skill slots on the skill bar, like V Rising's ability bar. Each slot holds at most one skill and a skill is
/// in at most one slot: equipping a skill that is already slotted moves it, swapping with whatever was in the target.
/// Plain C# so it can be tested without a scene; every change is reported per slot through SlotChanged, like
/// ItemContainer.
/// </summary>
public sealed class SkillLoadout
{
    public const int SlotCount = 5;

    private readonly SkillDefinition[] slots = new SkillDefinition[SlotCount];

    /// <summary>Raised after a slot's skill changes, with the slot's index.</summary>
    public event Action<int> SlotChanged;

    public bool IsValid(int index) => index >= 0 && index < SlotCount;

    public SkillDefinition Get(int index) => IsValid(index) ? slots[index] : null;

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
            Set(index, null);
            return true;
        }

        int current = IndexOf(skill);
        if (current >= 0)
        {
            return Move(current, index);
        }

        Set(index, skill);
        return true;
    }

    /// <summary>Moves a slot's skill onto another slot, swapping the two. Returns false when nothing moved.</summary>
    public bool Move(int from, int to)
    {
        if (!IsValid(from) || !IsValid(to) || from == to || slots[from] == null)
        {
            return false;
        }

        SkillDefinition moving = slots[from];
        Set(from, slots[to]);
        Set(to, moving);
        return true;
    }

    public bool Clear(int index)
    {
        if (!IsValid(index) || slots[index] == null)
        {
            return false;
        }

        Set(index, null);
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
                Set(i, skill);
            }
        }
    }

    public SkillDefinition[] ToArray() => (SkillDefinition[])slots.Clone();

    public int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private void Set(int index, SkillDefinition skill)
    {
        slots[index] = skill;
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
