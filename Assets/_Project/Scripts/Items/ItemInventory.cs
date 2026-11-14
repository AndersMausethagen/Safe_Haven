using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's bag: a grid of item slots (see ItemContainer) for materials, weapons, tools, backpacks, consumables,
/// blueprints and unique finds, plus the safe pocket. Only essences live in the ResourceInventory as bars (GDD §12.1).
/// Gameplay gives items through Give, so the HUD can announce them; the UI moves them around through Container and
/// SafePocket, and right-click goes through Use (equip, study, read a rune book, eat, drink, heal).
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

    /// <summary>The favourites on the quickbar (hold F). Which items, not the stacks: those stay in the bag.</summary>
    public QuickItems Quick { get; } = new QuickItems();

    /// <summary>Something was used from the bag or the pocket (it worked): the item and where it was. Screens show it.</summary>
    public event Action<ItemDefinition, ItemContainer, int> ItemUsed;

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
                // Laid out at its full size, so improving the portal only has to unlock slots.
                int most = safePocketSlots + SettlementSettings.Current.ExtraPocketSlotsAt(int.MaxValue);
                safePocket = new ItemContainer(most, 1);
                safePocket.SetUnlockedSlots(safePocketSlots + SettlementSettings.Current.ExtraPocketSlotsAt(GameSession.Settlement.PortalLevel));
            }

            return safePocket;
        }
    }

    /// <summary>Opens the slots the portal's improvements have earned since the pocket was made.</summary>
    public void RefreshPocket()
    {
        SafePocket?.SetUnlockedSlots(safePocketSlots + SettlementSettings.Current.ExtraPocketSlotsAt(GameSession.Settlement.PortalLevel));
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
    public int Give(ItemDefinition item, int amount) => Give(item, amount, 0f);

    /// <summary>The same, for a fish of a length (centimetres), which the bag shows (ItemStack.Size).</summary>
    public int Give(ItemDefinition item, int amount, float size)
    {
        bool had = Container.Count(item) > 0;
        int added = Container.Add(item, amount, size);
        if (added > 0)
        {
            Received?.Invoke(item, added);
            QuickOnArrival(item, had);
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

    /// <summary>Gives one piece just made, with its quality and condition (CraftedWork.Make), as Give gives a new item.
    /// Returns how many arrived.</summary>
    public int GiveMade(ItemStack made)
    {
        if (made.IsEmpty)
        {
            return 0;
        }

        if (!made.Item.KeepsCondition)
        {
            return Give(made.Item, made.Count);
        }

        bool had = Container.Count(made.Item) > 0;
        int added = Container.AddStack(made);
        if (added > 0)
        {
            Received?.Invoke(made.Item, added);
            QuickOnArrival(made.Item, had);
            GameEvents.Raise(new GameEvent(GameEventKind.ItemObtained, made.Item, added, gameObject));
            if (made.Item.IsEquippable && TryGetComponent(out PlayerEquipment equipment))
            {
                equipment.AutoEquip(made.Item);
            }
        }

        return added;
    }

    /// <summary>
    /// A consumable new to the bag (none were in it) goes on the quickbar when there is room (the user, 2026-11-14). Only on
    /// arrival: one the player took off the bar stays off while they still carry some.
    /// </summary>
    private void QuickOnArrival(ItemDefinition item, bool alreadyCarried)
    {
        if (!alreadyCarried && item.Category == ItemCategory.Consumable && QuickItems.Accepts(item) && !Quick.Contains(item) && !Quick.IsFull)
        {
            Quick.Toggle(item);
        }
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
        ItemStack used = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        bool worked = UseStack(from, slot);
        if (worked && !used.IsEmpty)
        {
            ItemUsed?.Invoke(used.Item, from, slot);
        }

        return worked;
    }

    /// <summary>Uses one of <paramref name="item"/> from the bag, else the safe pocket (the quickbar). False when there is
    /// none, with a word to the player.</summary>
    public bool UseFirst(ItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        foreach (ItemContainer source in new[] { Container, SafePocket })
        {
            for (int i = 0; source != null && i < source.UnlockedSlots; i++)
            {
                ItemStack stack = source.GetSlot(i);
                if (!stack.IsEmpty && stack.Item == item)
                {
                    return Use(source, i);
                }
            }
        }

        GameMessages.Post($"No {item.DisplayName} left.", MessageKind.Warning);
        return false;
    }

    /// <summary>How many of <paramref name="item"/> the bag and the safe pocket hold together.</summary>
    public int CountCarried(ItemDefinition item) =>
        item == null ? 0 : Container.Count(item) + (SafePocket != null ? SafePocket.Count(item) : 0);

    private bool UseStack(ItemContainer from, int slot)
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

        if (item.IsLantern && Refill(from, slot))
        {
            return true;
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

        if (item.IsSapling)
        {
            return Plant(from, slot, item);
        }

        if (item.Transport != null)
        {
            return TransportDeployment.Deploy(gameObject, from, slot);
        }

        if (item.IsLasso)
        {
            return HorseLasso.Throw(gameObject);
        }

        if (item.Experience > 0)
        {
            // Reading a rune book: the experience is granted by whoever listens (ExperienceTracker).
            from.RemoveAt(slot, 1);
            GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
            return true;
        }

        if (item.HoldsWater)
        {
            return DrinkFrom(from, slot, stack);
        }

        return Consume(from, slot, item);
    }

    // A sapling goes into the ground a step in front of the player, at home only.
    private bool Plant(ItemContainer from, int slot, ItemDefinition item)
    {
        PlantedTrees home = PlantedTrees.Current;
        if (home == null)
        {
            GameMessages.Post($"A {item.DisplayName.ToLowerInvariant()} only takes root at home, in Safe Haven.", MessageKind.Warning);
            return false;
        }

        Vector3 ahead = transform.forward;
        ahead.y = 0f;
        Vector3 at = transform.position + (ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward) * 1.6f;
        if (!home.TryPlant(at, item.Sapling, out string problem))
        {
            GameMessages.Post(problem, MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        float days = item.Sapling.GrowDays;
        GameMessages.Post($"You plant the {item.DisplayName.ToLowerInvariant()}. It should be a tree in about {days:0} day{(Mathf.Approximately(days, 1f) ? "" : "s")}.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        return true;
    }

    /// <summary>
    /// Refills a lantern that isn't full with one of its refills (a firefly) from the bag or the pocket it is in: it is full
    /// again. False when it is full or there is nothing to refill it with.
    /// </summary>
    public bool Refill(ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        LanternDefinition lantern = stack.IsEmpty ? null : stack.Item.Lantern;
        if (lantern == null || lantern.Refill == null || stack.Durability >= 0.999f)
        {
            return false;
        }

        ItemContainer source = from.Count(lantern.Refill) > 0 ? from : Container.Count(lantern.Refill) > 0 ? Container : null;
        if (source == null)
        {
            return false;
        }

        source.Remove(lantern.Refill, 1);
        from.SetDurability(slot, 1f);
        GameMessages.Post($"You let a {lantern.Refill.DisplayName.ToLowerInvariant()} into the {stack.Item.DisplayName}. It glows full again.", MessageKind.Gain);
        return true;
    }

    // A water container: drinks what thirst lacks, from what is in it, and keeps the container.
    private bool DrinkFrom(ItemContainer from, int slot, ItemStack stack)
    {
        var needs = GetComponent<PlayerNeeds>();
        if (needs == null)
        {
            return false;
        }

        float water = stack.Water;
        if (water < 0.5f)
        {
            GameMessages.Post($"Your {stack.Item.DisplayName} is empty. Fill it at a stream or a well.", MessageKind.Warning);
            return false;
        }

        float wanted = needs.Thirst.Max - needs.Thirst.Current;
        if (wanted < 0.5f)
        {
            GameMessages.Post("You're not thirsty.", MessageKind.Warning);
            return false;
        }

        float drunk = Mathf.Min(water, wanted);
        needs.TryConsume(stack.Item.DisplayName, 0f, drunk);
        from.SetDurability(slot, stack.WithWater(water - drunk).Durability);
        return true;
    }

    private bool Study(ItemContainer from, int slot, ItemDefinition item)
    {
        var crafting = GetComponent<PlayerCrafting>();
        if (crafting == null)
        {
            return false;
        }

        // Otherwise it would be read on the spot, and a raid's best find would never be at risk.
        if (item.IsLearnedAtHome && RaidDirector.Current != null)
        {
            GameMessages.Post($"{item.DisplayName} is no use out here. Get it home.", MessageKind.Warning);
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
        if (item.GearRepair > 0f)
        {
            return MendWorn(from, slot, item);
        }

        if (item.PlacesInRaid != null)
        {
            return PlaceInRaid(from, slot, item);
        }

        var needs = GetComponent<PlayerNeeds>();
        bool heals = item.HealAmount > 0f && health.CurrentHealth < health.MaxHealth;
        bool feeds = needs != null && ((item.HungerRestore > 0f && !needs.Hunger.IsFull) || (item.ThirstRestore > 0f && !needs.Thirst.IsFull));
        // A meal's bonuses are worth eating for on a full belly: that is the meal before a raid.
        bool helps = item.HasBoon || (item.Cures.Count > 0 && Cures(item));
        if (!heals && !feeds && !helps)
        {
            GameMessages.Post(item.HealAmount > 0f && item.HungerRestore <= 0f && item.ThirstRestore <= 0f
                ? "Already at full health."
                : "You don't need that right now.", MessageKind.Warning);
            return false;
        }

        // The player stops to eat: it takes a moment, can be interrupted, and heals over time (PlayerEating).
        if (TryGetComponent(out PlayerEating eating))
        {
            if (!eating.Begin(item, from))
            {
                GameMessages.Post(eating.IsEating ? "Already eating." : "Not now: you're busy fighting.", MessageKind.Warning);
                return false;
            }

            return true;
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

    // A repair kit: every worn piece gets its share back, broken or not. Refused while nothing worn needs it, so it isn't wasted.
    private bool MendWorn(ItemContainer from, int slot, ItemDefinition item)
    {
        var equipment = GetComponent<PlayerEquipment>();
        ItemContainer worn = equipment != null ? equipment.Gear.Slots : null;
        int mended = 0;
        for (int i = 0; worn != null && i < worn.SlotCount; i++)
        {
            ItemStack stack = worn.GetSlot(i);
            if (stack.IsDamaged)
            {
                worn.SetDurability(i, stack.Durability + item.GearRepair);
                mended++;
                GameEvents.Raise(new GameEvent(GameEventKind.Repaired, stack.Item, 1, gameObject));
            }
        }

        if (mended == 0)
        {
            GameMessages.Post("Nothing you wear needs mending.", MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        GameMessages.Post($"{item.DisplayName}: {mended} piece{(mended == 1 ? "" : "s")} mended.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        return true;
    }

    // A field kit: set down where the player stands. Only out there, where it is worth carrying.
    private bool PlaceInRaid(ItemContainer from, int slot, ItemDefinition item)
    {
        if (RaidDirector.Current == null)
        {
            GameMessages.Post($"{item.DisplayName} is for the woods. You won't need it at home.", MessageKind.Warning);
            return false;
        }

        from.RemoveAt(slot, 1);
        Instantiate(item.PlacesInRaid, transform.position, transform.rotation);
        GameMessages.Post($"{item.DisplayName} is set.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.Consumed, item, 1, gameObject));
        return true;
    }

    // True when one of the status effects the item cures is on its user.
    private bool Cures(ItemDefinition item)
    {
        if (!TryGetComponent(out StatusEffects effects))
        {
            return false;
        }

        foreach (StatusEffectDefinition effect in item.Cures)
        {
            if (effects.Has(effect))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Puts a whole stack on the ground to make room. It lies there until it is picked up again (DroppedItem).</summary>
    public bool Drop(ItemContainer from, int slot)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        if (stack.IsEmpty || DroppedItem.DropByChoice(transform, stack) == null)
        {
            return false;
        }

        from.RemoveAt(slot, stack.Count);
        GameMessages.Post($"Dropped {stack.Count} {stack.DisplayName}.");
        return true;
    }

    /// <summary>
    /// The tier of tool the user gathers with: the tool of that kind on their tool belt (character screen), or 0 for bare
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
            return equipment.Gear.ToolTier(type);
        }

        ItemInventory inventory = user != null ? user.GetComponentInParent<ItemInventory>() : null;
        return inventory != null ? inventory.Container.BestToolTier(type) : 0;
    }
}
