using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A grid of item slots, like V Rising's bag: each slot holds one stack of one item, up to the item's stack size.
/// Plain C# so the same logic can back the player's bag and later chests and stations, and be tested without a scene.
/// Every change is reported per slot through SlotChanged, and every method returns how much actually happened, so
/// callers can deal with partial results such as a full bag. Slots past UnlockedSlots are locked: they keep their items
/// but take no new ones, which is how the player's bag grows with backpacks (see ItemInventory and BackpackCapacity).
/// A container can also have a carry-weight budget (GDD §12.4): every item weighs ItemDefinition.Weight, and nothing is
/// added or moved in past it. Like locked slots, a lowered budget never destroys anything; it only blocks more.
/// </summary>
public sealed class ItemContainer
{
    private const float WeightEpsilon = 0.0001f;

    private readonly ItemStack[] slots;
    private readonly Dictionary<object, float> weightBonuses = new Dictionary<object, float>();
    private int unlockedSlots;
    private float baseWeightCapacity;
    private float currentWeight;

    public ItemContainer(int columns, int rows)
    {
        Columns = Mathf.Max(1, columns);
        Rows = Mathf.Max(1, rows);
        slots = new ItemStack[Columns * Rows];
        unlockedSlots = slots.Length;
    }

    public int Columns { get; }
    public int Rows { get; }
    public int SlotCount => slots.Length;

    /// <summary>
    /// How many slots, from the front, can receive items. The rest are locked: they keep what they hold (so a smaller
    /// backpack never destroys anything) but nothing new goes in. All slots are unlocked unless SetUnlockedSlots says less.
    /// </summary>
    public int UnlockedSlots => unlockedSlots;

    /// <summary>Raised after a slot's content changes, with the slot's index.</summary>
    public event Action<int> SlotChanged;

    /// <summary>
    /// Raised after MoveTo moved a stack out of one of this container's slots: (from slot, target container, to slot). In
    /// a swap the target's old stack is now in the from slot. Lets trackers such as PlayerEquipment follow a stack.
    /// </summary>
    public event Action<int, ItemContainer, int> StackMoved;

    /// <summary>Raised after the number of unlocked slots changes.</summary>
    public event Action LayoutChanged;

    /// <summary>Raised after the carried weight or the weight budget changes.</summary>
    public event Action WeightChanged;

    /// <summary>True when the container has a carry-weight budget. Without one, weight is tracked but never blocks.</summary>
    public bool HasWeightLimit => baseWeightCapacity > 0f;

    /// <summary>The budget including every bonus (backpacks), or infinity without a limit.</summary>
    public float WeightCapacity
    {
        get
        {
            if (!HasWeightLimit)
            {
                return float.PositiveInfinity;
            }

            float total = baseWeightCapacity;
            foreach (float bonus in weightBonuses.Values)
            {
                total += bonus;
            }

            return Mathf.Max(0f, total);
        }
    }

    public float CurrentWeight => Mathf.Max(0f, currentWeight);
    public float FreeWeight => HasWeightLimit ? Mathf.Max(0f, WeightCapacity - CurrentWeight) : float.PositiveInfinity;

    /// <summary>Sets the base budget. 0 removes the limit.</summary>
    public void SetWeightCapacity(float capacity)
    {
        capacity = Mathf.Max(0f, capacity);
        if (!Mathf.Approximately(capacity, baseWeightCapacity))
        {
            baseWeightCapacity = capacity;
            WeightChanged?.Invoke();
        }
    }

    /// <summary>Adds (or replaces) a bonus to the budget from one source, such as an equipped backpack.</summary>
    public void SetWeightBonus(object source, float amount)
    {
        if (source == null || (weightBonuses.TryGetValue(source, out float current) && Mathf.Approximately(current, amount)))
        {
            return;
        }

        weightBonuses[source] = amount;
        WeightChanged?.Invoke();
    }

    public void RemoveWeightBonus(object source)
    {
        if (source != null && weightBonuses.Remove(source))
        {
            WeightChanged?.Invoke();
        }
    }

    /// <summary>How many of the item the free weight allows, ignoring slots. int.MaxValue when weight doesn't limit it.</summary>
    public int FitsByWeight(ItemDefinition item)
    {
        if (item == null || !HasWeightLimit || item.Weight <= 0f)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, Mathf.FloorToInt((FreeWeight + WeightEpsilon) / item.Weight));
    }

    private bool CanTakeWeight(float added)
    {
        return added <= 0f || !HasWeightLimit || added <= FreeWeight + WeightEpsilon;
    }

    private static float WeightOf(ItemStack stack) => stack.IsEmpty ? 0f : stack.Item.Weight * stack.Count;

    /// <summary>
    /// Optional rule for which items a slot takes, such as the player's gear slots (a helmet only fits the head). Moves
    /// and swaps that would break it are refused. Null lets every slot take anything.
    /// </summary>
    public Func<int, ItemDefinition, bool> SlotRule { get; set; }

    /// <summary>Whether the slot's rule lets the item in. Empty stacks always fit.</summary>
    public bool CanHold(int index, ItemDefinition item) => item == null || SlotRule == null || SlotRule(index, item);

    public ItemStack GetSlot(int index)
    {
        return IsValid(index) ? slots[index] : ItemStack.Empty;
    }

    public bool IsValid(int index)
    {
        return index >= 0 && index < slots.Length;
    }

    public bool IsUnlocked(int index)
    {
        return index >= 0 && index < unlockedSlots;
    }

    public void SetUnlockedSlots(int count)
    {
        count = Mathf.Clamp(count, 0, slots.Length);
        if (count == unlockedSlots)
        {
            return;
        }

        unlockedSlots = count;
        LayoutChanged?.Invoke();
    }

    public int Count(ItemDefinition item)
    {
        int count = 0;
        if (item == null)
        {
            return count;
        }

        foreach (ItemStack stack in slots)
        {
            if (stack.Item == item)
            {
                count += stack.Count;
            }
        }

        return count;
    }

    public bool Contains(ItemDefinition item, int amount)
    {
        return item != null && amount >= 0 && Count(item) >= amount;
    }

    /// <summary>How many of the item would fit right now: room on its stacks and in empty unlocked slots, within the weight budget.</summary>
    public int SpaceFor(ItemDefinition item)
    {
        if (item == null)
        {
            return 0;
        }

        int space = 0;
        for (int i = 0; i < unlockedSlots; i++)
        {
            ItemStack stack = slots[i];
            if (stack.IsEmpty && CanHold(i, item))
            {
                space += item.MaximumStack;
            }
            else if (stack.Item == item)
            {
                space += stack.SpaceLeft;
            }
        }

        return Mathf.Min(space, FitsByWeight(item));
    }

    /// <summary>
    /// Adds as many as fit in the unlocked slots and the weight budget, topping up existing stacks before using empty
    /// slots. Returns how many were added.
    /// </summary>
    public int Add(ItemDefinition item, int amount)
    {
        amount = item != null ? Mathf.Min(amount, FitsByWeight(item)) : 0;
        if (item == null || amount <= 0)
        {
            return 0;
        }

        int remaining = amount;
        for (int i = 0; i < unlockedSlots && remaining > 0; i++)
        {
            if (slots[i].Item == item && slots[i].SpaceLeft > 0)
            {
                int added = Mathf.Min(slots[i].SpaceLeft, remaining);
                Set(i, new ItemStack(item, slots[i].Count + added));
                remaining -= added;
            }
        }

        for (int i = 0; i < unlockedSlots && remaining > 0; i++)
        {
            if (slots[i].IsEmpty && CanHold(i, item))
            {
                int added = Mathf.Min(item.MaximumStack, remaining);
                Set(i, new ItemStack(item, added));
                remaining -= added;
            }
        }

        return amount - remaining;
    }

    /// <summary>
    /// Removes up to the amount, taking from the last slots first so the front of the bag stays as the player arranged
    /// it. Returns how many were removed.
    /// </summary>
    public int Remove(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return 0;
        }

        int remaining = amount;
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            if (slots[i].Item == item)
            {
                remaining -= RemoveAt(i, remaining);
            }
        }

        return amount - remaining;
    }

    /// <summary>Removes up to the amount from one slot. Returns how many were removed.</summary>
    public int RemoveAt(int index, int amount)
    {
        if (!IsValid(index) || amount <= 0 || slots[index].IsEmpty)
        {
            return 0;
        }

        ItemStack stack = slots[index];
        int removed = Mathf.Min(stack.Count, amount);
        Set(index, new ItemStack(stack.Item, stack.Count - removed));
        return removed;
    }

    /// <summary>
    /// Puts a stack into a slot as it is, replacing what was there, for restoring a saved layout. Counts above the item's
    /// stack size are clamped and the weight budget is ignored. Use Add for gameplay, which respects stacking, space and weight.
    /// </summary>
    public void SetSlot(int index, ItemStack stack)
    {
        if (!IsValid(index))
        {
            return;
        }

        Set(index, stack.IsEmpty ? ItemStack.Empty : new ItemStack(stack.Item, Mathf.Min(stack.Count, stack.Item.MaximumStack), stack.Durability, stack.Empower, stack.Enchantment));
    }

    /// <summary>How many items are in the container, of every kind.</summary>
    public int CountItems()
    {
        int count = 0;
        foreach (ItemStack stack in slots)
        {
            count += stack.Count;
        }

        return count;
    }

    /// <summary>Changes the durability of the item in a slot (wear on death, a repair). Does nothing to empty slots.</summary>
    public void SetDurability(int index, float durability)
    {
        if (IsValid(index) && !slots[index].IsEmpty)
        {
            Set(index, slots[index].WithDurability(durability));
        }
    }

    /// <summary>
    /// Puts a whole stack, durability (or a water container's level) included, into the first free unlocked slot, topping up equal stacks first when it
    /// stacks. Returns how many arrived. Use it to move existing gear; Add makes new items.
    /// </summary>
    public int AddStack(ItemStack stack)
    {
        if (stack.IsEmpty)
        {
            return 0;
        }

        if (!stack.Item.KeepsCondition)
        {
            return Add(stack.Item, stack.Count);
        }

        int empty = FirstEmptySlotFor(stack.Item);
        if (empty < 0 || !CanTakeWeight(WeightOf(stack)))
        {
            return 0;
        }

        Set(empty, stack);
        return stack.Count;
    }

    /// <summary>
    /// Moves as much of one slot's stack as fits into another container (bag to stash, say). What doesn't fit stays in the
    /// slot. Returns how many were moved.
    /// </summary>
    public int TransferTo(int index, ItemContainer target)
    {
        ItemStack stack = GetSlot(index);
        if (target == null || target == this || stack.IsEmpty)
        {
            return 0;
        }

        int moved = stack.Item.KeepsCondition ? target.AddStack(stack) : target.Add(stack.Item, stack.Count);
        if (moved > 0)
        {
            RemoveAt(index, moved);
        }

        return moved;
    }

    /// <summary>
    /// Moves a slot's stack onto another slot: into it when empty, onto it when it holds the same stacking item (what
    /// doesn't fit stays behind), and swapping the two otherwise. Nothing moves into a locked slot.
    /// </summary>
    public void Move(int from, int to)
    {
        MoveTo(from, this, to);
    }

    /// <summary>
    /// Moves a slot's stack onto a slot of this or another container (bag to safe pocket, say), with the same rules as
    /// Move. Returns false when nothing moved, such as when the target slot is locked.
    /// </summary>
    public bool MoveTo(int from, ItemContainer target, int to)
    {
        if (target == null || (target == this && from == to) || !IsValid(from) || !target.IsValid(to) || slots[from].IsEmpty
            || !target.IsUnlocked(to) || !target.CanHold(to, slots[from].Item))
        {
            return false;
        }

        ItemStack source = slots[from];
        ItemStack destination = target.slots[to];
        bool otherContainer = target != this;
        if (destination.IsEmpty)
        {
            if (otherContainer && !target.CanTakeWeight(WeightOf(source)))
            {
                return false;
            }

            target.Set(to, source);
            Set(from, ItemStack.Empty);
            StackMoved?.Invoke(from, target, to);
            return true;
        }

        // Gear never stacks, so two copies of it swap: one may be worn, the other enchanted.
        if (destination.Item == source.Item && source.Item.Stackable)
        {
            int moved = Mathf.Min(destination.SpaceLeft, source.Count);
            if (otherContainer)
            {
                moved = Mathf.Min(moved, target.FitsByWeight(source.Item));
            }

            if (moved <= 0)
            {
                return false;
            }

            target.Set(to, new ItemStack(destination.Item, destination.Count + moved));
            Set(from, new ItemStack(source.Item, source.Count - moved));
            StackMoved?.Invoke(from, target, to);
            return true;
        }

        // A swap sends the other stack back into the source slot, which must be able to take it.
        float shift = WeightOf(source) - WeightOf(destination);
        if (!IsUnlocked(from) || !CanHold(from, destination.Item)
            || (otherContainer && (!target.CanTakeWeight(shift) || !CanTakeWeight(-shift))))
        {
            return false;
        }

        Set(from, destination);
        target.Set(to, source);
        StackMoved?.Invoke(from, target, to);
        return true;
    }

    /// <summary>Moves half of a stack (rounded down) into the first empty slot. Returns false when it can't be split.</summary>
    public bool SplitHalf(int index)
    {
        if (!IsValid(index) || slots[index].Count < 2)
        {
            return false;
        }

        int empty = FirstEmptySlot();
        if (empty < 0)
        {
            return false;
        }

        ItemStack stack = slots[index];
        int half = stack.Count / 2;
        Set(index, new ItemStack(stack.Item, stack.Count - half));
        Set(empty, new ItemStack(stack.Item, half));
        return true;
    }

    public int FirstEmptySlot() => FirstEmptySlotFor(null);

    /// <summary>The first empty unlocked slot whose rule takes the item, or -1.</summary>
    public int FirstEmptySlotFor(ItemDefinition item)
    {
        for (int i = 0; i < unlockedSlots; i++)
        {
            if (slots[i].IsEmpty && CanHold(i, item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The highest tier among the unbroken tools of this kind in the container, or 0 when there are none.</summary>
    public int BestToolTier(ToolType type)
    {
        int best = 0;
        if (type == ToolType.None)
        {
            return best;
        }

        foreach (ItemStack stack in slots)
        {
            if (!stack.IsEmpty && stack.Item.ToolType == type && !stack.IsBroken)
            {
                best = Mathf.Max(best, stack.Item.ToolTier);
            }
        }

        return best;
    }

    /// <summary>The slot of the highest-tier unbroken tool of this kind, or -1 when there is none.</summary>
    public int BestToolIndex(ToolType type)
    {
        int best = -1;
        if (type == ToolType.None)
        {
            return best;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack stack = slots[i];
            if (!stack.IsEmpty && stack.Item.ToolType == type && !stack.IsBroken
                && (best < 0 || stack.Item.ToolTier > slots[best].Item.ToolTier))
            {
                best = i;
            }
        }

        return best;
    }

    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].IsEmpty)
            {
                Set(i, ItemStack.Empty);
            }
        }
    }

    private void Set(int index, ItemStack stack)
    {
        float change = WeightOf(stack) - WeightOf(slots[index]);
        slots[index] = stack;
        if (change != 0f)
        {
            currentWeight += change;
            WeightChanged?.Invoke();
        }

        Action<int> handlers = SlotChanged;
        if (handlers == null)
        {
            return;
        }

        // Each listener runs in isolation so a faulty one (a UI bug, say) can't break looting or crafting.
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

/// <summary>What happened when items were offered to a container: what was stored and what did not fit.</summary>
public sealed class ItemGrantResult
{
    private readonly List<ItemAmount> stored = new List<ItemAmount>();
    private readonly List<ItemAmount> overflow = new List<ItemAmount>();

    public IReadOnlyList<ItemAmount> Stored => stored;
    public IReadOnlyList<ItemAmount> Overflow => overflow;
    public bool HasOverflow => overflow.Count > 0;

    public void Record(ItemDefinition item, int offered, int accepted)
    {
        if (accepted > 0)
        {
            stored.Add(new ItemAmount(item, accepted));
        }

        if (offered > accepted)
        {
            overflow.Add(new ItemAmount(item, offered - accepted));
        }
    }
}
