using System;
using UnityEngine;

/// <summary>
/// The nine slots on the skill bar (keys 1-9; five until 2026-11-14), like V Rising's ability bar. Each slot holds at most one skill or one usable item (a
/// consumable: its key uses one from the bag, as the quickbar does), and a skill or an item is in at most one slot:
/// putting one that is already slotted moves it, swapping with whatever was in the target. Plain C# so it can be tested
/// without a scene; every change is reported per slot through SlotChanged, like ItemContainer.
/// </summary>
/// <para>
/// Each of the five skill slots takes one kind (Docs/SkillChoiceResearch_2026-11-27.md 3.2: the user, 2026-11-27, "the meta is to
/// just put all skills on the bar and spam the one that does the most damage"): 1 the weapon's art, 2-3 techniques, 4 a power, 5 a
/// power or the ultimate. 6-9 hold consumables only. A skill never goes where it doesn't fit; one restored from an older bar is moved
/// to a slot that takes it (SetAll). A consumable may still go in any slot.
/// </para>
public sealed class SkillLoadout
{
    public const int SlotCount = 9;
    /// <summary>Slots 1-5 hold skills, each its kind (SlotTakes); 6-9 only consumables.</summary>
    public const int SkillSlotCount = 5;

    private static readonly SkillKind[][] Takes =
    {
        new[] { SkillKind.Art },
        new[] { SkillKind.Technique },
        new[] { SkillKind.Technique },
        new[] { SkillKind.Power },
        new[] { SkillKind.Power, SkillKind.Ultimate },
    };

    private readonly SkillDefinition[] slots = new SkillDefinition[SlotCount];
    private readonly ItemDefinition[] items = new ItemDefinition[SlotCount];

    /// <summary>Raised after a slot's skill changes, with the slot's index.</summary>
    public event Action<int> SlotChanged;

    /// <summary>The kinds of skill a slot takes; none for 6-9.</summary>
    public static SkillKind[] SlotTakes(int index) => index >= 0 && index < SkillSlotCount ? (SkillKind[])Takes[index].Clone() : new SkillKind[0];

    /// <summary>True when the slot can hold the skill (or nothing).</summary>
    public static bool Fits(int index, SkillDefinition skill) =>
        skill == null || index >= 0 && index < SkillSlotCount && Array.IndexOf(Takes[index], skill.Kind) >= 0;

    /// <summary>What the slot takes, in words: "Technique", "Power or Ultimate", "Consumable".</summary>
    public static string SlotName(int index)
    {
        if (index < 0 || index >= SkillSlotCount)
        {
            return "Consumable";
        }

        return string.Join(" or ", Array.ConvertAll(Takes[index], SkillKinds.Name));
    }

    /// <summary>The badge an empty skill slot wears so the player sees what goes there: "Art", "Tech", "Power", "Ult" (slot 5 takes a power too: its tooltip says so); empty for 6-9.</summary>
    public static string SlotBadge(int index) => index switch
    {
        0 => "Art",
        1 => "Tech",
        2 => "Tech",
        3 => "Power",
        4 => "Ult",
        _ => "",
    };

    /// <summary>The first empty slot that takes the skill, or -1.</summary>
    public int FirstFittingEmpty(SkillDefinition skill)
    {
        for (int i = 0; i < SkillSlotCount; i++)
        {
            if (IsEmpty(i) && Fits(i, skill))
            {
                return i;
            }
        }

        return -1;
    }

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
            if (Move(current, index))
            {
                return true;
            }

            // The skill there can't go to the item's old slot (a technique to 6-9): the item moves, the skill goes back to the book.
            Set(current, null, null);
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
    /// false when nothing changed, or the slot doesn't take its kind (Fits).
    /// </summary>
    public bool Equip(int index, SkillDefinition skill)
    {
        if (!IsValid(index) || slots[index] == skill || !Fits(index, skill))
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
            if (Move(current, index))
            {
                return true;
            }

            // What is there can't go to the skill's old slot (an ultimate to a power's): the skill moves, that goes back to the book.
            Set(current, null, null);
        }

        Set(index, skill, null);
        return true;
    }

    /// <summary>Moves a slot's skill onto another slot, swapping the two. Returns false when nothing moved, or either skill would
    /// land in a slot that doesn't take it.</summary>
    public bool Move(int from, int to)
    {
        if (!IsValid(from) || !IsValid(to) || from == to || IsEmpty(from) || !Fits(to, slots[from]) || !Fits(from, slots[to]))
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

    /// <summary>Replaces every slot, for restoring a carried loadout. Duplicates after the first are dropped. A skill whose slot doesn't
    /// take it (a bar from before the slots had kinds, 2026-11-27) goes to the first free slot that does, after the fitting ones are
    /// placed, and after the bar's own consumables (<paramref name="keepItems"/>) are back where they were; with none free it goes back
    /// to the book.</summary>
    public void SetAll(SkillDefinition[] skills, ItemDefinition[] keepItems = null)
    {
        var misplaced = new System.Collections.Generic.List<SkillDefinition>();
        for (int i = 0; i < SlotCount; i++)
        {
            SkillDefinition skill = skills != null && i < skills.Length ? skills[i] : null;
            if (skill != null && Array.IndexOf(skills, skill) != i)
            {
                skill = null;
            }

            if (skill != null && !Fits(i, skill))
            {
                misplaced.Add(skill);
                skill = null;
            }

            if (slots[i] != skill)
            {
                Set(i, skill, skill != null ? null : items[i]);
            }
        }

        // The saved consumables first: a misplaced skill took a slot that looked free and the item there was dropped (review 2026-11-27).
        if (keepItems != null)
        {
            SetItems(keepItems);
        }

        foreach (SkillDefinition skill in misplaced)
        {
            int free = FirstFittingEmpty(skill);
            if (free >= 0)
            {
                Set(free, skill, null);
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
