using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Every building in the game as Safe Haven stands with it, for the Haven window: what stands (level, what it is doing, what the
/// next upgrade asks), what could be built now, and what isn't unlocked yet and how to unlock it. The ward's own upgrades lead the list.
/// </summary>
public static class HavenBuildings
{
    public const string StandingShelf = "Standing";
    public const string ReadyShelf = "Ready to build";
    public const string LockedShelf = "Not unlocked yet";
    private const int MostNamed = 5;

    /// <summary>The buildings as the player's knowledge and the loaded scene have them. A null placer: no buttons.</summary>
    public static List<HavenShelf> Read(PlayerCrafting crafting, MaterialPool pool, IContentTeachers teachers, BuildPlacer builder)
    {
        var standing = new HavenShelf(StandingShelf);
        var ready = new HavenShelf(ReadyShelf);
        var locked = new HavenShelf(LockedShelf);
        HavenEntry ward = WardEntry(pool);
        if (ward != null)
        {
            standing.Entries.Add(ward);
        }

        ILookup<BuildingDefinition, BuildingObject> placed = BuildingObject.Active
            .Where(building => building.Definition != null && building.Definition.Faction == Faction.Player)
            .ToLookup(building => building.Definition);
        IEnumerable<BuildingDefinition> known = crafting != null ? crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu) : Enumerable.Empty<BuildingDefinition>();
        foreach (BuildingDefinition building in known.Union(placed.Select(group => group.Key)).OrderBy(b => b.SortOrder).ThenBy(b => b.DisplayName))
        {
            if (placed.Contains(building))
            {
                standing.Entries.Add(StandingEntry(building, placed[building].ToList(), crafting, pool, builder));
            }
            else if (crafting != null && crafting.IsKnown(building) && BuildingUnlocks.IsOpen(building))
            {
                ready.Entries.Add(ReadyEntry(building, pool, builder));
            }
            else
            {
                locked.Entries.Add(LockedEntry(building, crafting, pool, teachers));
            }
        }

        return new List<HavenShelf> { standing, ready, locked };
    }

    private static HavenEntry StandingEntry(BuildingDefinition building, List<BuildingObject> placed, PlayerCrafting crafting, MaterialPool pool, BuildPlacer builder)
    {
        int up = 0;
        int scaffolds = 0;
        int ruins = 0;
        int highest = 0;
        int canUpgrade = 0;
        Construction next = null;
        foreach (BuildingObject one in placed)
        {
            if (!one.TryGetComponent(out Construction construction))
            {
                up++;
                highest = Mathf.Max(highest, 1);
                continue;
            }

            if (construction.IsStanding)
            {
                up++;
                highest = Mathf.Max(highest, construction.Level);
            }

            if (construction.IsBuilding && !construction.Paid)
            {
                ruins++;
            }
            else if (construction.IsBuilding)
            {
                scaffolds++;
            }
            else if (construction.CanUpgrade)
            {
                canUpgrade++;
                next = next == null || construction.Level < next.Level ? construction : next;
            }
        }

        int hurt = BuildingRepairs.Damaged().Count(health => health.TryGetComponent(out BuildingObject one) && one.Definition == building);
        string doing = WhatItIsDoing(placed);
        var notes = new List<string>();
        if (ruins > 0) notes.Add(ruins == 1 && placed.Count == 1 ? "In ruins" : $"{ruins} in ruins");
        if (scaffolds > 0) notes.Add(scaffolds == 1 && placed.Count == 1 ? "Being built" : $"{scaffolds} being built");
        if (hurt > 0) notes.Add(hurt == 1 && placed.Count == 1 ? "Damaged" : $"{hurt} damaged");
        if (doing != null) notes.Add(doing);
        string level = building.MaxLevel > 1 && up > 0 ? $"Level {highest} of {building.MaxLevel}" : up > 0 ? "Built" : "";
        string line = string.Join(", ", new[] { level }.Concat(notes).Where(part => part.Length > 0));

        var entry = new HavenEntry
        {
            Key = building,
            Name = placed.Count > 1 ? $"{building.DisplayName} ×{placed.Count}" : building.DisplayName,
            Icon = building.Icon,
            Line = line,
            Badge = building.MaxLevel > 1 && up > 0 ? $"{highest}/{building.MaxLevel}" : "",
            State = ruins > 0 || hurt > 0 ? HavenEntryState.Attention : scaffolds > 0 || doing != null ? HavenEntryState.Working : HavenEntryState.Good,
            Status = line,
            Description = building.Description,
        };

        if (building.MaxLevel > 1)
        {
            entry.Details.Add(new HavenDetail("Levels", LevelList(building, highest)));
        }

        if (next != null)
        {
            BuildingLevel upgrade = building.UpgradeTo(next.Level + 1);
            string who = placed.Count > 1 ? $" ({canUpgrade} can)" : "";
            entry.Details.Add(new HavenDetail($"Next: Tier {Tiers.ToRoman(next.Level + 1)}, {upgrade.title}{who}", WhatTierBrings(building, next.Level + 1, upgrade, crafting), LevelCosts(upgrade, pool)));
        }
        else if (up > 0 && building.MaxLevel > 1 && highest >= building.MaxLevel)
        {
            entry.Details.Add(new HavenDetail("Next", "It has reached its highest level."));
        }

        GameObject player = crafting != null ? crafting.gameObject : null;
        if (next != null && player != null)
        {
            Construction chosen = next;
            next.Cost(out IReadOnlyList<ResourceAmount> resources, out IReadOnlyList<ItemAmount> items);
            bool affordable = pool == null || pool.CanAfford(resources, items);
            bool open = BuildingUnlocks.IsOpen(building.UpgradeTo(next.Level + 1));
            entry.Actions.Add(new HavenAction(!open ? "Not open yet" : affordable ? "Upgrade" : "Not enough materials", () => chosen.TryOrderUpgrade(player), open && affordable, closesWindow: false));
        }
        else if (builder != null && crafting != null && crafting.IsKnown(building))
        {
            AddBuildAction(entry, building, pool, builder, "Build another");
        }

        // A scaffold or ruin first: that is where the player is wanted.
        BuildingObject target = placed.FirstOrDefault(one => one.TryGetComponent(out Construction construction) && construction.IsBuilding) ?? placed[0];
        HavenTravel.AddGoTo(entry, target.transform);
        return entry;
    }

    private static HavenEntry ReadyEntry(BuildingDefinition building, MaterialPool pool, BuildPlacer builder)
    {
        var entry = new HavenEntry
        {
            Key = building,
            Name = building.DisplayName,
            Icon = building.Icon,
            Line = BuildCostText(building),
            Badge = building.Renown > 0 ? $"+{building.Renown}" : "",
            State = HavenEntryState.Ready,
            Status = building.Renown > 0 ? $"Not built yet. Brings {building.Renown} renown." : "Not built yet.",
            Description = building.Description,
        };
        entry.Details.Add(new HavenDetail("Cost", "", BuildCosts(building, pool)));
        if (builder != null)
        {
            AddBuildAction(entry, building, pool, builder, "Build");
        }

        return entry;
    }

    private static HavenEntry LockedEntry(BuildingDefinition building, PlayerCrafting crafting, MaterialPool pool, IContentTeachers teachers)
    {
        // Learned but waiting on another building: what stands in the way is the building, not the knowledge.
        string how = crafting != null && crafting.IsKnown(building) ? BuildingUnlocks.Missing(building) : HowToUnlock(building, pool, teachers);
        string missing = BuildingUnlocks.Missing(building);
        if (missing.Length > 0 && !how.Contains(missing))
        {
            how = $"{how} {missing}";
        }

        var entry = new HavenEntry
        {
            Key = building,
            Name = building.DisplayName,
            Icon = building.Icon,
            Line = how,
            State = HavenEntryState.Locked,
            Status = "Not unlocked yet",
            Description = building.Description,
        };
        entry.Details.Add(new HavenDetail("How to unlock it", how));
        entry.Details.Add(new HavenDetail("It will cost", "", BuildCosts(building, pool)));
        return entry;
    }

    /// <summary>What a building's teachers ask: a job someone can take, or a blueprint to find and use.</summary>
    public static string HowToUnlock(BuildingDefinition building, MaterialPool pool, IContentTeachers teachers)
    {
        var ways = new List<string>();
        if (teachers != null)
        {
            foreach (ResidentDefinition resident in teachers.Residents)
            {
                if (resident == null)
                {
                    continue;
                }

                foreach (JobDefinition job in resident.Jobs)
                {
                    if (job != null && job.TeachesBuildings.Contains(building))
                    {
                        ways.Add(GameSession.Here.HasResident(resident)
                            ? $"Give {resident.DisplayName} the {job.DisplayName} job."
                            : $"Bring {resident.DisplayName} home: the {job.DisplayName} knows how.");
                    }
                }
            }

            foreach (ItemDefinition item in teachers.Items)
            {
                if (item != null && item.TeachesBuildings.Contains(building))
                {
                    ways.Add(pool != null && pool.Have(item) > 0
                        ? $"You have the {item.DisplayName}. Use it from your bag."
                        : $"Find the {item.DisplayName}.");
                }
            }
        }

        return ways.Count > 0 ? string.Join(" ", ways) : "Nobody here knows how yet.";
    }

    private static void AddBuildAction(HavenEntry entry, BuildingDefinition building, MaterialPool pool, BuildPlacer builder, string label)
    {
        bool affordable = pool == null || pool.CanAfford(building.ConstructionCost, building.ConstructionItems);
        entry.Actions.Add(new HavenAction(affordable ? label : "Not enough materials", () => builder.Place(building), affordable));
    }

    // The ward isn't a building of its own: it grows at the portal, and that is an upgrade like any other.
    private static HavenEntry WardEntry(MaterialPool pool)
    {
        SettlementSettings settings = SettlementSettings.Current;
        if (settings == null || settings.WardLevels.Count == 0)
        {
            return null;
        }

        int level = GameSession.Here.WardLevel;
        int most = settings.WardLevels.Count;
        string radius = HomePortal.Current != null ? $"{HomePortal.Current.WardRadius:0} m" : $"{settings.WardRadiusAt(level):0} m";
        var entry = new HavenEntry
        {
            Key = settings,
            Name = "Portal ward",
            Line = $"Level {level + 1} of {most + 1}, {radius}",
            Badge = $"{level + 1}/{most + 1}",
            State = HavenEntryState.Good,
            Status = $"The ward reaches {radius}.",
            Description = "Build anywhere in the Haven's meadows. The portal's ward keeps the ground nearest it safe at night: renown and strengthening it at the portal both widen it.",
        };
        if (HomePortal.Current != null)
        {
            HavenTravel.AddGoTo(entry, HomePortal.Current.transform);
        }

        if (level < most)
        {
            SettlementSettings.WardLevel next = settings.WardLevels[level];
            var costs = new List<HavenCost>();
            foreach (ResourceAmount cost in next.cost)
            {
                if (cost.Resource != null)
                {
                    costs.Add(new HavenCost(cost.Resource.DisplayName, pool != null ? pool.Have(cost.Resource) : 0, cost.Amount));
                }
            }

            entry.Details.Add(new HavenDetail("Next", $"{next.extraRadius:0} m wider. Strengthen it at the portal.", costs));
        }
        else
        {
            entry.Details.Add(new HavenDetail("Next", "The ward is as strong as the portal can make it."));
        }

        return entry;
    }

    private static string WhatItIsDoing(List<BuildingObject> placed)
    {
        foreach (BuildingObject one in placed)
        {
            if (!one.TryGetComponent(out CraftingStation station))
            {
                continue;
            }

            // Find, not station.Queue: only looking mustn't create a queue that gets saved.
            StationQueue queue = GameSession.Here.Workshops.Find(station.Id);
            if (queue != null && queue.Current != null)
            {
                return $"Making {queue.Current.DisplayName}";
            }

            if (queue != null && queue.OutputAmount > 0)
            {
                return $"{queue.OutputAmount} ready to collect";
            }
        }

        return null;
    }

    private static string LevelList(BuildingDefinition building, int highest)
    {
        var levels = new List<string>();
        for (int level = 1; level <= building.MaxLevel; level++)
        {
            string title = level == 1 ? "Built" : building.UpgradeTo(level).title;
            levels.Add(level == highest ? $"<b>{level}. {title}</b>" : $"{level}. {title}");
        }

        return string.Join("\n", levels);
    }

    /// <summary>What a tier makes and opens comes first: that is why anyone would build it. Then its other bonuses.</summary>
    public static string WhatTierBrings(BuildingDefinition building, int tier, BuildingLevel level, PlayerCrafting crafting)
    {
        var parts = new List<string>();
        if (crafting != null)
        {
            List<string> makes = crafting.Recipes.Where(recipe => recipe != null && building.Station != CraftingStationType.None
                    && recipe.Station == building.Station && recipe.RequiredStationTier == tier && crafting.IsKnown(recipe))
                .Select(recipe => recipe.DisplayName).Take(MostNamed + 1).ToList();
            if (makes.Count > 0)
            {
                parts.Add($"Makes {string.Join(", ", makes.Take(MostNamed))}{(makes.Count > MostNamed ? " and more" : "")}.");
            }

            List<string> opens = BuildingUnlocks.OpenedBy(building, tier, crafting.Buildings);
            if (opens.Count > 0)
            {
                parts.Add($"Opens {string.Join(", ", opens)}.");
            }
        }

        parts.Add(WhatItBrings(level));
        parts.Add(BuildingUnlocks.Missing(level));
        return string.Join("\n", parts.Where(part => part.Length > 0));
    }

    private static string WhatItBrings(BuildingLevel level)
    {
        var parts = new List<string>();
        if (level.craftSpeed > 0f) parts.Add($"{Mathf.RoundToInt(level.craftSpeed * 100f)}% faster");
        if (level.extraQueue > 0) parts.Add($"{level.extraQueue} more in the queue");
        if (level.extraOutput > 0) parts.Add($"{level.extraOutput} more output");
        if (level.extraStorage > 0) parts.Add($"{level.extraStorage} more stash slots");
        if (level.raidSeconds > 0f) parts.Add($"{RaidStats.FormatTime(level.raidSeconds)} more in every raid");
        if (level.extraRumours > 0) parts.Add($"{level.extraRumours} more rumours");
        if (level.comfort > 0) parts.Add($"{level.comfort} more comfort");
        if (level.extraLogs > 0) parts.Add($"{level.extraLogs} more logs per cut at home");
        if (level.beds > 0) parts.Add($"{level.beds} more beds");
        if (level.purifyYield > 0f) parts.Add($"{Mathf.RoundToInt(level.purifyYield * 100f)}% more Purified Essence");
        if (level.extraMealSlots > 0) parts.Add($"{level.extraMealSlots} more meal at once");
        if (level.extraTonicSlots > 0) parts.Add($"{level.extraTonicSlots} more tonic at once");
        if (level.extraGame > 0) parts.Add($"{level.extraGame} more game at home");
        if (level.extraLight > 0f) parts.Add($"{level.extraLight:0} m more light");
        foreach (RecipeDefinition recipe in level.teachesRecipes)
        {
            if (recipe != null) parts.Add($"teaches {recipe.DisplayName}");
        }

        if (level.renown > 0) parts.Add($"{level.renown} renown");
        string brings = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(level.description) ? brings : brings.Length > 0 ? $"{level.description}\n{brings}" : level.description;
    }

    private static string BuildCostText(BuildingDefinition building)
    {
        string resources = AmountText.Describe(building.ConstructionCost);
        string items = AmountText.Describe(building.ConstructionItems);
        string cost = resources.Length > 0 && items.Length > 0 ? $"{resources}, {items}" : resources + items;
        return cost.Length > 0 ? cost : "Free";
    }

    private static List<HavenCost> BuildCosts(BuildingDefinition building, MaterialPool pool) =>
        Costs(building.ConstructionCost, building.ConstructionItems, pool);

    private static List<HavenCost> LevelCosts(BuildingLevel level, MaterialPool pool) => Costs(level.essence, level.cost, pool);

    private static List<HavenCost> Costs(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items, MaterialPool pool)
    {
        var costs = new List<HavenCost>();
        foreach (ResourceAmount cost in resources)
        {
            if (cost.Resource != null && cost.Amount > 0)
            {
                costs.Add(new HavenCost(cost.Resource.DisplayName, pool != null ? pool.Have(cost.Resource) : 0, cost.Amount));
            }
        }

        foreach (ItemAmount cost in items)
        {
            if (cost.Item != null && cost.Amount > 0)
            {
                costs.Add(new HavenCost(cost.Item.DisplayName, pool != null ? pool.Have(cost.Item) : 0, cost.Amount));
            }
        }

        return costs;
    }
}

/// <summary>Who and what can teach a building: every resident and item in the game. ContentDatabase is the real one.</summary>
public interface IContentTeachers
{
    IReadOnlyList<ResidentDefinition> Residents { get; }
    IReadOnlyList<ItemDefinition> Items { get; }
}
