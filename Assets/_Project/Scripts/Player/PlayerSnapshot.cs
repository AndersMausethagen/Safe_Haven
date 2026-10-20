using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A copy of what the player owns, knows and how they are at one moment: the bag and safe pocket slot by slot (with
/// durability), the worn gear, resources, discovered resources, learned recipes and buildings, skills, hunger and
/// thirst. Scene changes hand one from the scene being left to the player in the next (see GameSession and
/// PlayerPersistence), raids compare two of them to report what was gained or lost, and saves are written from one. It
/// holds references to authored definitions only, never to scene objects, and is never changed after it is taken.
/// <para>
/// Extending, to carry a new part of the player between scenes: capture it in Capture(GameObject), apply it in
/// ApplyTo(GameObject), and write/read it in SaveMapper.
/// </para>
/// </summary>
public sealed class PlayerSnapshot
{
    /// <summary>One occupied bag slot: where it is and what it holds.</summary>
    public readonly struct SlotRecord
    {
        public readonly int Index;
        public readonly ItemStack Stack;

        public SlotRecord(int index, ItemStack stack)
        {
            Index = index;
            Stack = stack;
        }
    }

    /// <summary>
    /// How the player is: hunger and thirst. HasNeeds is false when not captured. EquippedSlot is the bag slot of the weapon
    /// equipped in saves made before gear slots existed (-1 otherwise); restoring such a save moves it to the main hand.
    /// </summary>
    public struct Condition
    {
        public bool HasNeeds;
        public float Hunger;
        public float Thirst;
        public int EquippedSlot;

        public static Condition None => new Condition { EquippedSlot = -1 };

        public Condition(float hunger, float thirst, int equippedSlot)
        {
            HasNeeds = true;
            Hunger = hunger;
            Thirst = thirst;
            EquippedSlot = equippedSlot;
        }
    }

    private readonly List<SlotRecord> slots = new List<SlotRecord>();
    private readonly List<SlotRecord> pocket = new List<SlotRecord>();
    private readonly List<SlotRecord> gear = new List<SlotRecord>();
    // False when the snapshot was taken without gear slots (an old save): applying it then equips gear from the bag.
    private bool hasGear;
    private readonly List<ResourceAmount> resources = new List<ResourceAmount>();
    private readonly List<ResourceDefinition> discovered = new List<ResourceDefinition>();
    private readonly List<RecipeDefinition> knownRecipes = new List<RecipeDefinition>();
    private readonly List<BuildingDefinition> knownBuildings = new List<BuildingDefinition>();
    private readonly List<SkillDefinition> knownSkills = new List<SkillDefinition>();
    // Null when the snapshot was taken without a PlayerSkills (a save file): applying it then leaves the skills alone.
    private SkillDefinition[] skillLoadout;
    private Condition condition = Condition.None;

    private PlayerSnapshot()
    {
    }

    /// <summary>True when the bag, the safe pocket or the resource inventory holds anything.</summary>
    public bool HasBelongings => slots.Count > 0 || pocket.Count > 0 || gear.Count > 0 || resources.Count > 0;

    /// <summary>The occupied bag slots, in slot order.</summary>
    public IReadOnlyList<SlotRecord> Slots => slots;
    /// <summary>The occupied safe pocket slots, in slot order. Kept even when a raid fails.</summary>
    public IReadOnlyList<SlotRecord> Pocket => pocket;
    /// <summary>The occupied gear slots (index = GearSlot), in slot order.</summary>
    public IReadOnlyList<SlotRecord> Gear => gear;
    /// <summary>False when the snapshot has no gear slots at all (taken without them, or from an old save).</summary>
    public bool HasGear => hasGear;
    public IReadOnlyList<ResourceAmount> Resources => resources;
    public IReadOnlyList<ResourceDefinition> Discovered => discovered;
    public IReadOnlyList<RecipeDefinition> KnownRecipes => knownRecipes;
    public IReadOnlyList<BuildingDefinition> KnownBuildings => knownBuildings;
    public IReadOnlyList<SkillDefinition> KnownSkills => knownSkills;
    /// <summary>The skill bar, slot by slot (null entries are empty slots), or null when skills weren't captured.</summary>
    public IReadOnlyList<SkillDefinition> SkillBar => skillLoadout;
    public Condition State => condition;

    public static PlayerSnapshot Capture(ItemContainer bag, ResourceStorage storage, IEnumerable<RecipeDefinition> known,
        ItemContainer safePocket = null, IEnumerable<BuildingDefinition> buildings = null, Condition? state = null,
        ItemContainer gear = null)
    {
        var snapshot = new PlayerSnapshot();
        Record(bag, snapshot.slots);
        Record(safePocket, snapshot.pocket);
        Record(gear, snapshot.gear);
        snapshot.hasGear = gear != null;

        if (storage != null)
        {
            snapshot.resources.AddRange(storage.StoredAmounts);
            snapshot.discovered.AddRange(storage.DiscoveredResources);
        }

        if (known != null)
        {
            snapshot.knownRecipes.AddRange(known);
        }

        if (buildings != null)
        {
            snapshot.knownBuildings.AddRange(buildings);
        }

        snapshot.condition = state ?? Condition.None;
        return snapshot;
    }

    private static void Record(ItemContainer container, List<SlotRecord> into)
    {
        if (container == null)
        {
            return;
        }

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemStack stack = container.GetSlot(i);
            if (!stack.IsEmpty)
            {
                into.Add(new SlotRecord(i, stack));
            }
        }
    }

    /// <summary>
    /// Takes the snapshot from a player's ItemInventory, ResourceInventory, PlayerCrafting, PlayerSkills, PlayerNeeds and
    /// PlayerEquipment.
    /// </summary>
    public static PlayerSnapshot Capture(GameObject player)
    {
        ItemInventory items = player != null ? player.GetComponent<ItemInventory>() : null;
        ResourceInventory resourceInventory = player != null ? player.GetComponent<ResourceInventory>() : null;
        PlayerCrafting crafting = player != null ? player.GetComponent<PlayerCrafting>() : null;
        PlayerNeeds needs = player != null ? player.GetComponent<PlayerNeeds>() : null;
        PlayerEquipment equipment = player != null ? player.GetComponent<PlayerEquipment>() : null;
        Condition state = needs != null ? new Condition(needs.Hunger.Current, needs.Thirst.Current, -1) : Condition.None;
        PlayerSnapshot snapshot = Capture(
            items != null ? items.Container : null,
            resourceInventory != null ? resourceInventory.Storage : null,
            crafting != null ? crafting.KnownRecipes : null,
            items != null ? items.SafePocket : null,
            crafting != null ? crafting.KnownBuildings : null,
            state,
            equipment != null ? equipment.Gear.Slots : null);

        PlayerSkills skills = player != null ? player.GetComponent<PlayerSkills>() : null;
        if (skills != null)
        {
            snapshot.knownSkills.AddRange(skills.KnownSkills);
            snapshot.skillLoadout = skills.Loadout.ToArray();
        }

        return snapshot;
    }

    /// <summary>
    /// Makes the inventories hold exactly what the snapshot holds, slot by slot, and teaches the recipes it knows. Stacks
    /// whose slot no longer exists (a smaller bag) go wherever they fit; pocket stacks that don't fit the pocket go to the
    /// bag. Returns what could not be placed at all.
    /// </summary>
    public List<ItemAmount> ApplyTo(ItemContainer bag, ResourceStorage storage, PlayerCrafting crafting, ItemContainer safePocket = null)
    {
        var unplaced = new List<ItemAmount>();
        var homeless = new List<ItemStack>();
        if (safePocket != null)
        {
            safePocket.Clear();
        }

        Place(pocket, safePocket, homeless);
        if (bag != null)
        {
            bag.Clear();
            Place(slots, bag, homeless);
            foreach (ItemStack stack in homeless)
            {
                int added = bag.AddStack(stack);
                if (added < stack.Count)
                {
                    unplaced.Add(new ItemAmount(stack.Item, stack.Count - added));
                }
            }
        }
        else
        {
            foreach (ItemStack stack in homeless)
            {
                unplaced.Add(new ItemAmount(stack.Item, stack.Count));
            }
        }

        if (storage != null)
        {
            foreach (ResourceAmount stored in new List<ResourceAmount>(storage.StoredAmounts))
            {
                storage.SetAmount(stored.Resource, 0);
            }

            foreach (ResourceDefinition resource in discovered)
            {
                storage.MarkDiscovered(resource);
            }

            foreach (ResourceAmount amount in resources)
            {
                storage.SetAmount(amount.Resource, amount.Amount);
            }
        }

        if (crafting != null)
        {
            crafting.Unlock(knownRecipes, announce: false);
            crafting.UnlockBuildings(knownBuildings, announce: false);
        }

        return unplaced;
    }

    /// <summary>
    /// Applies the snapshot to a player's ItemInventory, ResourceInventory, PlayerCrafting, PlayerSkills, PlayerNeeds and
    /// PlayerEquipment.
    /// </summary>
    public List<ItemAmount> ApplyTo(GameObject player)
    {
        ItemInventory items = player.GetComponent<ItemInventory>();
        ResourceInventory resourceInventory = player.GetComponent<ResourceInventory>();
        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        if (skills != null && skillLoadout != null)
        {
            skills.Restore(knownSkills, skillLoadout);
        }

        List<ItemAmount> unplaced = ApplyTo(
            items != null ? items.Container : null,
            resourceInventory != null ? resourceInventory.Storage : null,
            player.GetComponent<PlayerCrafting>(),
            items != null ? items.SafePocket : null);

        PlayerNeeds needs = player.GetComponent<PlayerNeeds>();
        if (needs != null && condition.HasNeeds)
        {
            needs.Restore(condition.Hunger, condition.Thirst);
        }

        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        if (equipment != null)
        {
            equipment.Restore(gear, hasGear, condition.EquippedSlot);
        }

        return unplaced;
    }

    private static void Place(List<SlotRecord> records, ItemContainer into, List<ItemStack> homeless)
    {
        foreach (SlotRecord record in records)
        {
            if (into != null && into.IsValid(record.Index))
            {
                into.SetSlot(record.Index, record.Stack);
            }
            else
            {
                homeless.Add(record.Stack);
            }
        }
    }

    /// <summary>
    /// The same knowledge, condition and safe pocket with an empty bag and no resources. AfterFailedRaid builds on it.
    /// </summary>
    public PlayerSnapshot WithoutBelongings()
    {
        var stripped = new PlayerSnapshot();
        stripped.pocket.AddRange(pocket);
        stripped.discovered.AddRange(discovered);
        stripped.knownRecipes.AddRange(knownRecipes);
        stripped.knownBuildings.AddRange(knownBuildings);
        // Skills are knowledge, so a failed raid keeps them like recipes.
        stripped.knownSkills.AddRange(knownSkills);
        stripped.skillLoadout = skillLoadout != null ? (SkillDefinition[])skillLoadout.Clone() : null;
        stripped.condition = condition;
        stripped.condition.EquippedSlot = -1;
        stripped.hasGear = hasGear;
        return stripped;
    }

    /// <summary>
    /// What a player keeps after a failed raid (GDD §8.3): knowledge, condition and the safe pocket, plus the deployed
    /// equipment – gear (armor, weapons, tools, backpacks) that was worn or in the bag when the raid began (the loadout)
    /// and is still worn or carried – each worn down by durabilityLoss. Kept gear stays where it was: worn or in the bag. Gear looted during the raid, materials and consumables are lost.
    /// </summary>
    /// <param name="loadout">What the player carried into the raid. Null keeps no equipment.</param>
    /// <param name="wear">Filled with each kept piece of equipment's durability before and after, for the raid report.</param>
    public PlayerSnapshot AfterFailedRaid(PlayerSnapshot loadout, float durabilityLoss, List<EquipmentWear> wear = null)
    {
        PlayerSnapshot kept = WithoutBelongings();
        if (loadout == null)
        {
            return kept;
        }

        var deployed = new List<ItemStack>();
        foreach (SlotRecord record in loadout.slots)
        {
            if (record.Stack.Item.HasDurability)
            {
                deployed.Add(record.Stack);
            }
        }

        foreach (SlotRecord record in loadout.gear)
        {
            deployed.Add(record.Stack);
        }

        // Worn gear is matched first, so it stays on when the same item was also in the bag.
        Keep(gear, kept.gear, deployed, durabilityLoss, wear);
        foreach (SlotRecord record in slots)
        {
            ItemStack stack = record.Stack;
            if (!stack.Item.HasDurability)
            {
                continue;
            }

            // Durability doesn't change during a raid, so the carried-in copy is the one with the same wear.
            int match = deployed.FindIndex(candidate => candidate.Item == stack.Item && Mathf.Approximately(candidate.Durability, stack.Durability));
            if (match < 0)
            {
                continue;
            }

            deployed.RemoveAt(match);
            ItemStack worn = stack.WithDurability(stack.Durability - durabilityLoss);
            kept.slots.Add(new SlotRecord(record.Index, worn));
            wear?.Add(new EquipmentWear(stack.Item, stack.Durability, worn.Durability));
            if (record.Index == condition.EquippedSlot)
            {
                kept.condition.EquippedSlot = record.Index;
            }
        }

        return kept;
    }

    private static void Keep(List<SlotRecord> records, List<SlotRecord> into, List<ItemStack> deployed, float durabilityLoss,
        List<EquipmentWear> wear)
    {
        foreach (SlotRecord record in records)
        {
            ItemStack stack = record.Stack;
            int match = deployed.FindIndex(candidate => candidate.Item == stack.Item && Mathf.Approximately(candidate.Durability, stack.Durability));
            if (match < 0)
            {
                continue;
            }

            deployed.RemoveAt(match);
            ItemStack worn = stack.WithDurability(stack.Durability - durabilityLoss);
            into.Add(new SlotRecord(record.Index, worn));
            wear?.Add(new EquipmentWear(stack.Item, stack.Durability, worn.Durability));
        }
    }

    /// <summary>How many of each item the bag, safe pocket and worn gear hold, across all their stacks.</summary>
    public Dictionary<ItemDefinition, int> CountItems()
    {
        var counts = new Dictionary<ItemDefinition, int>();
        foreach (SlotRecord record in gear)
        {
            counts.TryGetValue(record.Stack.Item, out int count);
            counts[record.Stack.Item] = count + record.Stack.Count;
        }

        foreach (SlotRecord record in slots)
        {
            counts.TryGetValue(record.Stack.Item, out int count);
            counts[record.Stack.Item] = count + record.Stack.Count;
        }

        foreach (SlotRecord record in pocket)
        {
            counts.TryGetValue(record.Stack.Item, out int count);
            counts[record.Stack.Item] = count + record.Stack.Count;
        }

        return counts;
    }

    public Dictionary<ResourceDefinition, int> CountResources()
    {
        var counts = new Dictionary<ResourceDefinition, int>();
        foreach (ResourceAmount amount in resources)
        {
            counts.TryGetValue(amount.Resource, out int count);
            counts[amount.Resource] = count + amount.Amount;
        }

        return counts;
    }
}
