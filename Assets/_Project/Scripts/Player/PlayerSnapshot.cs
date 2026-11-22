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
    // Null when not captured (an older save): applying it then leaves the quickbar alone.
    private ItemDefinition[] quickItems;
    private ItemDefinition[] keptItems;
    private ItemDefinition[] skillBarItems;
    private Condition condition = Condition.None;
    // Meals and other boons at work. Kept through the portal; a failed raid and a save file leave them behind.
    private readonly List<BoonRecord> boons = new List<BoonRecord>();
    // True for WhatWasWorn: applying it adds to the player the next scene has, and replaces nothing but the worn gear.
    private bool addsToPlayer;

    private PlayerSnapshot()
    {
    }

    /// <summary>True when the bag, the safe pocket or the resource inventory holds anything.</summary>
    public bool HasBelongings => slots.Count > 0 || pocket.Count > 0 || gear.Count > 0 || resources.Count > 0;

    /// <summary>Crafts by hand a loaded save had under way, already paid (HandCraftQueue.Restore). Only a load sets it: a raid's or
    /// a visit's handover stops the crafts first, so nothing else carries them.</summary>
    public HandCraftSaveData HandCrafts { get; set; }

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
    /// <summary>The quickbar's items in order, or null when it wasn't captured.</summary>
    public IReadOnlyList<ItemDefinition> QuickBar => quickItems;
    /// <summary>What is marked to keep in the bag (KeptItems), or null when it wasn't captured.</summary>
    public IReadOnlyList<ItemDefinition> KeptItems => keptItems;

    /// <summary>Sets what is marked to keep (read from a save file). Returns this snapshot.</summary>
    public PlayerSnapshot WithKeptItems(IEnumerable<ItemDefinition> items)
    {
        keptItems = items != null ? new List<ItemDefinition>(items).ToArray() : null;
        return this;
    }
    /// <summary>The items on the skill bar, slot by slot (null where a skill or nothing is), or null when not captured.</summary>
    public IReadOnlyList<ItemDefinition> SkillBarItems => skillBarItems;

    /// <summary>Sets the skill bar's items (read from a save file). Returns this snapshot.</summary>
    public PlayerSnapshot WithSkillBarItems(IEnumerable<ItemDefinition> items)
    {
        skillBarItems = items != null ? new List<ItemDefinition>(items).ToArray() : null;
        return this;
    }

    /// <summary>Sets the skill book and bar (read from a save file). Returns this snapshot.</summary>
    public PlayerSnapshot WithSkills(IEnumerable<SkillDefinition> known, SkillDefinition[] bar)
    {
        knownSkills.Clear();
        knownSkills.AddRange(known);
        skillLoadout = bar;
        return this;
    }

    /// <summary>Sets the quickbar (read from a save file). Returns this snapshot.</summary>
    public PlayerSnapshot WithQuickItems(IEnumerable<ItemDefinition> items)
    {
        quickItems = items != null ? new List<ItemDefinition>(items).ToArray() : null;
        return this;
    }
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

        if (items != null)
        {
            snapshot.quickItems = new List<ItemDefinition>(items.Quick.Items).ToArray();
            snapshot.keptItems = new List<ItemDefinition>(items.Kept.Items).ToArray();
        }

        PlayerSkills skills = player != null ? player.GetComponent<PlayerSkills>() : null;
        if (skills != null)
        {
            snapshot.knownSkills.AddRange(skills.KnownSkills);
            snapshot.skillLoadout = skills.Loadout.ToArray();
            snapshot.skillBarItems = skills.Loadout.ToItemArray();
        }

        PlayerBoons playerBoons = player != null ? player.GetComponent<PlayerBoons>() : null;
        if (playerBoons != null)
        {
            snapshot.boons.AddRange(playerBoons.Capture());
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
        if (addsToPlayer)
        {
            return AddTo(player);
        }

        ItemInventory items = player.GetComponent<ItemInventory>();
        ResourceInventory resourceInventory = player.GetComponent<ResourceInventory>();
        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        // Putting a saved bag back is not new consumables arriving (they'd fill the bars the save says are empty).
        if (items == null)
        {
            return ApplyRestoring(player, null, resourceInventory, skills);
        }

        items.BeginRestore();
        try
        {
            return ApplyRestoring(player, items, resourceInventory, skills);
        }
        finally
        {
            items.EndRestore();
        }
    }

    private List<ItemAmount> ApplyRestoring(GameObject player, ItemInventory items, ResourceInventory resourceInventory, PlayerSkills skills)
    {
        if (skills != null && skillLoadout != null)
        {
            skills.Restore(knownSkills, skillLoadout, skillBarItems);
        }
        else if (skills != null && skillBarItems != null)
        {
            skills.Loadout.SetItems(skillBarItems);
        }

        if (items != null && quickItems != null)
        {
            items.Quick.Restore(quickItems);
        }

        if (items != null && keptItems != null)
        {
            items.Kept.Restore(keptItems);
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

        PlayerBoons playerBoons = player.GetComponent<PlayerBoons>();
        if (playerBoons != null)
        {
            playerBoons.Restore(boons);
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
    /// Only what the player wears (armor, weapon, the tools on the belt, backpack, lantern) and the arrows in the bag.
    /// Applying it puts that gear on and adds the arrows, and leaves everything else as the next scene has it: its starting
    /// contents, recipes, hunger and thirst. The prologue hands this to the Haven by the rule of a failed raid: what was
    /// worn comes through, what was carried does not.
    /// </summary>
    public PlayerSnapshot WhatWasWorn()
    {
        var worn = new PlayerSnapshot { addsToPlayer = true, hasGear = hasGear };
        worn.gear.AddRange(gear);
        foreach (SlotRecord record in slots)
        {
            if (record.Stack.Item.IsArrow)
            {
                worn.slots.Add(record);
            }
        }

        return worn;
    }

    // WhatWasWorn: the gear goes on first, so a backpack's room is there for what follows.
    private List<ItemAmount> AddTo(GameObject player)
    {
        var unplaced = new List<ItemAmount>();
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        if (equipment != null && hasGear)
        {
            equipment.Restore(gear, true);
        }

        ItemInventory items = player.GetComponent<ItemInventory>();
        foreach (SlotRecord record in slots)
        {
            int added = items != null ? items.Container.AddStack(record.Stack) : 0;
            if (added < record.Stack.Count)
            {
                unplaced.Add(new ItemAmount(record.Stack.Item, record.Stack.Count - added));
            }
        }

        return unplaced;
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
        // The quickbar is a preference, not belongings: it stays, its items greyed until found again.
        stripped.quickItems = quickItems != null ? (ItemDefinition[])quickItems.Clone() : null;
        stripped.keptItems = keptItems != null ? (ItemDefinition[])keptItems.Clone() : null;
        stripped.skillBarItems = skillBarItems != null ? (ItemDefinition[])skillBarItems.Clone() : null;
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
    /// <param name="keepNothing">The death rules' Harsh (DeathRuleSet › Raid Takes Everything): nothing is kept, not the gear, not the
    /// safe pocket; knowledge and condition only.</param>
    public PlayerSnapshot AfterFailedRaid(PlayerSnapshot loadout, float durabilityLoss, List<EquipmentWear> wear = null, bool keepNothing = false)
    {
        PlayerSnapshot kept = WithoutBelongings();
        if (keepNothing)
        {
            kept.pocket.Clear();
            return kept;
        }

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

        // A carried-in piece moved into the safe pocket is kept there anyway; it must not leave its place in the loadout free
        // for a copy found out there.
        foreach (bool exact in new[] { true, false })
        {
            foreach (SlotRecord record in pocket)
            {
                int match = record.Stack.Item.HasDurability ? CarriedIn(deployed, record.Stack, exact) : -1;
                if (match >= 0)
                {
                    deployed.RemoveAt(match);
                }
            }
        }

        // Worn gear is matched first, so it stays on when the same item was also in the bag. Every piece with its exact wear is
        // matched before any chipped one (a glancing blow wears an axe on the way), so a piece found out there can't take the
        // place of the one carried in.
        var held = new List<SlotRecord>(gear);
        int wornCount = held.Count;
        foreach (SlotRecord record in slots)
        {
            if (record.Stack.Item.HasDurability)
            {
                held.Add(record);
            }
        }

        var done = new bool[held.Count];
        foreach (bool exact in new[] { true, false })
        {
            for (int i = 0; i < held.Count; i++)
            {
                if (done[i])
                {
                    continue;
                }

                ItemStack stack = held[i].Stack;
                int match = CarriedIn(deployed, stack, exact);
                if (match < 0)
                {
                    continue;
                }

                done[i] = true;
                deployed.RemoveAt(match);
                // A lantern keeps its light: that is what it holds, not wear.
                ItemStack worn = stack.Item.IsLantern ? stack : stack.WithDurability(stack.Durability - durabilityLoss);
                (i < wornCount ? kept.gear : kept.slots).Add(new SlotRecord(held[i].Index, worn));
                wear?.Add(new EquipmentWear(stack.Item, stack.Durability, worn.Durability));
                if (i >= wornCount && held[i].Index == condition.EquippedSlot)
                {
                    kept.condition.EquippedSlot = held[i].Index;
                }
            }
        }

        return kept;
    }

    // Which carried-in piece a piece held at the end is: the same piece (item, quality, maker, empowering, enchantment) with
    // the same wear, or (not exact) the least worn-down copy that was more whole going in. A piece found out there in better
    // shape than anything carried in, or of another make (a relic's enchantment, a plain copy of a Masterwork), matches none.
    private static int CarriedIn(List<ItemStack> deployed, ItemStack stack, bool exact)
    {
        if (exact)
        {
            return deployed.FindIndex(candidate => candidate.SameKind(stack) && Mathf.Approximately(candidate.Durability, stack.Durability));
        }

        int best = -1;
        for (int i = 0; i < deployed.Count; i++)
        {
            ItemStack candidate = deployed[i];
            // Nothing done to a piece in a raid changes what it is (empowering and enchanting are the altar's work at home), so
            // the whole make has to agree, wear aside.
            if (candidate.SameKind(stack) && candidate.Durability > stack.Durability && (best < 0 || candidate.Durability < deployed[best].Durability))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Gear that wore down since <paramref name="loadout"/> (an extraction's report): each worn piece, matched to what was worn
    /// in the same slot going in, with its durability then and now. Empty when nothing wore, or with no loadout.
    /// </summary>
    public List<EquipmentWear> WornSince(PlayerSnapshot loadout)
    {
        var worn = new List<EquipmentWear>();
        if (loadout == null)
        {
            return worn;
        }

        foreach (SlotRecord now in gear)
        {
            int at = loadout.gear.FindIndex(then => then.Index == now.Index && then.Stack.Item == now.Stack.Item);
            if (at >= 0 && loadout.gear[at].Stack.Durability - now.Stack.Durability > 0.001f)
            {
                worn.Add(new EquipmentWear(now.Stack.Item, loadout.gear[at].Stack.Durability, now.Stack.Durability));
            }
        }

        return worn;
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
