using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's bag: a grid of item slots (see ItemContainer) for materials, weapons, tools, backpacks, consumables,
/// blueprints and unique finds, plus the safe pocket. Only essences live in the ResourceInventory as bars (GDD §12.1).
/// Gameplay gives items through Give, so the HUD can announce them; the UI moves them around through Container and
/// SafePocket, and right-click goes through Use (equip, study, eat, drink, heal).
///
/// Carry space: only the first baseUnlockedSlots slots are usable; the best backpack carried unlocks more
/// (BackpackCapacity calls SetBonusSlots). The bag also has a carry-weight budget (carryWeight, raised by the equipped
/// backpack); the safe pocket and equipped gear don't count against it. The safe pocket is a separate small container whose contents survive a
/// failed raid (see PlayerSnapshot.AfterFailedRaid).
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemInventory : MonoBehaviour
{
    [Tooltip("Slots per row.")]
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int rows = 5;
    [Tooltip("Slots usable without a backpack. The rest of the grid is shown locked until a backpack unlocks it.")]
    [SerializeField, Min(1)] private int baseUnlockedSlots = 16;
    [Tooltip("Slots in the safe pocket. Whatever is in it is kept even when a raid fails.")]
    [SerializeField, Min(0)] private int safePocketSlots = 1;
    [Tooltip("How much weight the bag carries before backpacks (GDD §12.4). 0 means no limit.")]
    [SerializeField, Min(0f)] private float carryWeight = 150f;
    [Tooltip("Items in the bag when the game starts. Useful for testing.")]
    [SerializeField] private List<ItemAmount> startingItems = new List<ItemAmount>();

    private ItemContainer container;
    private ItemContainer safePocket;
    private Damageable health;
    private int bonusSlots;

    /// <summary>Created on first use so other components can reach it regardless of Awake order.</summary>
    public ItemContainer Container
    {
        get
        {
            if (container == null)
            {
                container = new ItemContainer(columns, rows);
                container.SetUnlockedSlots(baseUnlockedSlots + bonusSlots);
                container.SetWeightCapacity(carryWeight);
                foreach (ItemAmount starting in startingItems)
                {
                    container.Add(starting.Item, starting.Amount);
                }
            }

            return container;
        }
    }

    /// <summary>The safe pocket, or null when the player has none (safePocketSlots is 0).</summary>
    public ItemContainer SafePocket
    {
        get
        {
            if (safePocket == null && safePocketSlots > 0)
            {
                safePocket = new ItemContainer(safePocketSlots, 1);
            }

            return safePocket;
        }
    }

    public float CarryWeight => carryWeight;

    /// <summary>Unlocks extra bag slots on top of the base amount. Called with the best backpack's bonus.</summary>
    public void SetBonusSlots(int extra)
    {
        bonusSlots = Mathf.Max(0, extra);
        Container.SetUnlockedSlots(baseUnlockedSlots + bonusSlots);
    }

    /// <summary>How many bag slots are usable with a backpack giving this many extra, for balancing tools.</summary>
    public int UnlockedSlotsWith(int extra) => Mathf.Min(columns * rows, baseUnlockedSlots + Mathf.Max(0, extra));

    /// <summary>Raised when items are given to the bag (loot, crafting), with how many arrived.</summary>
    public event Action<ItemDefinition, int> Received;

    private void Awake()
    {
        _ = Container;
        health = GetComponent<Damageable>();
    }

    /// <summary>Adds as many as fit and returns how many arrived.</summary>
    public int Give(ItemDefinition item, int amount)
    {
        int added = Container.Add(item, amount);
        if (added > 0)
        {
            Received?.Invoke(item, added);
            GameEvents.Raise(new GameEvent(GameEventKind.ItemObtained, item, added, gameObject));
            if (item.IsEquippable)
            {
                var equipment = GetComponent<PlayerEquipment>();
                if (equipment != null)
                {
                    equipment.AutoEquip(item);
                }
            }
        }

        return added;
    }

    /// <summary>Offers several items and reports what was stored and what did not fit.</summary>
    public ItemGrantResult Give(IReadOnlyList<ItemAmount> items)
    {
        var result = new ItemGrantResult();
        foreach (ItemAmount entry in items)
        {
            result.Record(entry.Item, entry.Amount, Give(entry.Item, entry.Amount));
        }

        return result;
    }

    /// <summary>Uses one item from the slot, such as drinking a potion. Returns true when it was used up.</summary>
    public bool Use(int slot) => Use(Container, slot);

    /// <summary>
    /// Uses an item from a slot of the bag or the safe pocket (right-click): equips a weapon, studies a blueprint, or eats,
    /// drinks or drinks a potion, restoring health, hunger and thirst. Consumables refuse when they wouldn't help, so they
    /// aren't wasted. Returns true when something happened.
    /// </summary>
    public bool Use(ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        if (stack.IsEmpty)
        {
            return false;
        }

        ItemDefinition item = stack.Item;
        if (!item.IsUsable)
        {
            GameMessages.Post($"{item.DisplayName} can't be used.", MessageKind.Warning);
            return false;
        }

        if (health == null || !health.IsAlive)
        {
            return false;
        }

        if (item.IsEquippable)
        {
            var equipment = GetComponent<PlayerEquipment>();
            return equipment != null && equipment.Equip(from, slot);
        }

        if (item.IsBlueprint)
        {
            return Study(from, slot, item);
        }

        return Consume(from, slot, item);
    }

    private bool Study(ItemContainer from, int slot, ItemDefinition item)
    {
        var crafting = GetComponent<PlayerCrafting>();
        if (crafting == null)
        {
            return false;
        }

        int learned = crafting.UnlockBuildings(item.TeachesBuildings) + crafting.Unlock(item.TeachesRecipes);
        if (learned == 0)
        {
            GameMessages.Post($"You already know everything in the {item.DisplayName}.", MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        return true;
    }

    private bool Consume(ItemContainer from, int slot, ItemDefinition item)
    {
        var needs = GetComponent<PlayerNeeds>();
        bool heals = item.HealAmount > 0f && health.CurrentHealth < health.MaxHealth;
        bool feeds = needs != null && ((item.HungerRestore > 0f && !needs.Hunger.IsFull) || (item.ThirstRestore > 0f && !needs.Thirst.IsFull));
        if (!heals && !feeds)
        {
            GameMessages.Post(item.HealAmount > 0f && item.HungerRestore <= 0f && item.ThirstRestore <= 0f
                ? "Already at full health."
                : "You don't need that right now.", MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        if (heals)
        {
            float healed = health.Heal(item.HealAmount);
            GameMessages.Post($"{item.DisplayName}: +{Mathf.RoundToInt(healed)} health", MessageKind.Gain);
        }

        if (feeds)
        {
            needs.TryConsume(item.DisplayName, item.HungerRestore, item.ThirstRestore);
        }

        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        return true;
    }

    /// <summary>Throws away a whole stack to make room. There are no ground items yet, so it is gone for good.</summary>
    public bool Discard(ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        if (stack.IsEmpty)
        {
            return false;
        }

        from.RemoveAt(slot, stack.Count);
        GameMessages.Post($"Dropped {stack.Count} {stack.Item.DisplayName}.", MessageKind.Warning);
        return true;
    }

    /// <summary>
    /// The tier of tool the user gathers with: the tool of that kind on their tool belt (character screen), or 1 for bare
    /// hands. Users without gear slots use their best tool in the bag. Nodes that need no tool are open to everyone.
    /// </summary>
    public static int GatheringTier(GameObject user, ToolType type)
    {
        if (type == ToolType.None)
        {
            return int.MaxValue;
        }

        PlayerEquipment equipment = user != null ? user.GetComponentInParent<PlayerEquipment>() : null;
        if (equipment != null)
        {
            return Mathf.Max(1, equipment.Gear.ToolTier(type));
        }

        ItemInventory inventory = user != null ? user.GetComponentInParent<ItemInventory>() : null;
        return Mathf.Max(1, inventory != null ? inventory.Container.BestToolTier(type) : 0);
    }
}
