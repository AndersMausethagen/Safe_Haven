using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Asked before taught (the user, 2026-11-15: "we need to build a building which requires stone blocks, but the player is not taught
/// how to make them"; Docs/QuestAndStoryResearch.md 1.4). For every quest it works out what the player is sure to have by the time it
/// can start: the quests before it done, the people they brought home (their jobs teach buildings and recipes) and the buildings they
/// had the player build. Then it asks whether what the quest wants can be had from that alone:
/// <list type="bullet">
/// <item>a building to build must be known, with what it needs standing already built by a quest before it;</item>
/// <item>a material counts as had when the world gives it (gathering, trees, water, kills, things lying about, the scenes) or when a
/// known recipe makes it at a station a quest before this one had the player build, from materials that count as had;</item>
/// <item>luck doesn't count: supply caches, raid events, trade and blueprints found on a raid;</item>
/// <item>but a sure thing does (the user asked for quests that send the player into a raid for a rare part, 2026-11-21): what a creature
/// always drops (a boss's trophy), and what a rumour's site always gives while the quest asking for it is under way (the Moonsilver
/// Folio rule of Docs/BuildingUpgradesResearch_2026-11-21.md §5.1).</item>
/// </list>
/// QuestAuditTests asks for none. Read-only: loads assets and reads the two home scenes as text, like ContentAudit.
/// </summary>
public static class QuestReachability
{
    // Where an item comes from that needs no teaching: the world and kills. Prefixes of ContentAudit's "Type.field" names.
    private static readonly string[] WorldSources =
    {
        "GatheringNodeDefinition.yields", "GatheringNodeDefinition.burstYields", "ChoppableTree.wood", "TreeChoppingSettings.woodByTier",
        "TreeChoppingSettings.limbingItem", "ResourceSource.drops", "FishingSettings.fish", "FireflyProfile.catchable", "WaterSource.fillItem",
        "SurvivalSettings.openWaterFillItem", "PrologueDefinition.", "ItemWorldObject.definition", "MonsterDefinition.drops",
        "RaidLayoutRules.pondFillItem",
        // Not LootOnDeath.drops: that is the supply crates' luck (planks came from them "50% × 1-2", which taught nobody planks).
    };

    private static readonly string[] HomeScenes = { "Assets/_Project/Scenes/SafeHaven.unity", "Assets/_Project/Scenes/Prologue_MistyWoods.unity" };

    /// <summary>What the player is sure to have when a quest can start.</summary>
    private sealed class Reach
    {
        public readonly HashSet<QuestDefinition> Done = new HashSet<QuestDefinition>();
        public readonly HashSet<BuildingDefinition> Known = new HashSet<BuildingDefinition>();
        public readonly Dictionary<BuildingDefinition, int> Built = new Dictionary<BuildingDefinition, int>();
        public readonly HashSet<RecipeDefinition> Recipes = new HashSet<RecipeDefinition>();
        public readonly HashSet<ItemDefinition> Rewarded = new HashSet<ItemDefinition>();
        // What a rumour's site is sure to give while this quest is under way (or with no quest asked).
        public readonly HashSet<ItemDefinition> RumourGiven = new HashSet<ItemDefinition>();

        public int TierBuilt(BuildingDefinition building) => building != null && Built.TryGetValue(building, out int tier) ? tier : 0;
    }

    private sealed class World
    {
        public readonly HashSet<ItemDefinition> Natural = new HashSet<ItemDefinition>();
        public readonly Dictionary<ItemDefinition, List<RecipeDefinition>> MadeBy = new Dictionary<ItemDefinition, List<RecipeDefinition>>();
        public readonly List<BuildingDefinition> Buildings = new List<BuildingDefinition>();
        public readonly List<RecipeDefinition> AllRecipes = new List<RecipeDefinition>();
        // Item -> the quests a rumour sure to give it waits for (a null entry: any time).
        public readonly Dictionary<ItemDefinition, List<QuestDefinition>> RumourGives = new Dictionary<ItemDefinition, List<QuestDefinition>>();
    }

    /// <summary>"Quest: what it asks for, and why that can't be had yet", for every quest that asks before it teaches.</summary>
    public static List<string> AskedBeforeTaught()
    {
        World world = LoadWorld();
        var found = new List<string>();
        foreach (QuestDefinition quest in QuestAudit.Quests().OrderBy(q => q.name))
        {
            Reach reach = ReachBefore(quest, world);
            foreach (KeyValuePair<ItemDefinition, List<QuestDefinition>> given in world.RumourGives)
            {
                if (given.Value.Any(waitsFor => waitsFor == null || waitsFor == quest))
                {
                    reach.RumourGiven.Add(given.Key);
                }
            }

            // In the order they are listed: a step that has the player build something teaches the steps after it ("raise the
            // Lodge, build the Yard, make a barrow there").
            foreach (QuestObjective objective in quest.Objectives)
            {
                if (objective == null)
                {
                    continue;
                }

                foreach (string problem in Check(objective, reach, world))
                {
                    found.Add($"{quest.name}: {problem}");
                }

                Apply(objective, reach);
            }
        }

        return found.Distinct().ToList();
    }

    private static IEnumerable<string> Check(QuestObjective objective, Reach reach, World world)
    {
        switch (objective.Type)
        {
            case ObjectiveType.BuildStructure:
                return CheckBuilding(objective.Building, 1, reach, world);
            case ObjectiveType.UpgradeBuilding:
                return CheckBuilding(objective.Building, objective.Tier, reach, world);
            case ObjectiveType.CraftItem:
                return objective.Item == null || Makeable(objective.Item, reach, world, 0)
                    ? Enumerable.Empty<string>()
                    : new[] { $"asks to make {objective.Item.name}, {WhyNot(objective.Item, reach, world)}" };
            case ObjectiveType.CollectItem:
            case ObjectiveType.StoreItem:
                return objective.Item == null || Had(objective.Item, reach, world, 0)
                    ? Enumerable.Empty<string>()
                    : new[] { $"asks for {objective.Item.name}, {WhyNot(objective.Item, reach, world)}" };
            default:
                return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> CheckBuilding(BuildingDefinition building, int tier, Reach reach, World world)
    {
        if (building == null)
        {
            yield break;
        }

        if (!reach.Known.Contains(building))
        {
            yield return $"asks for {building.name} before anyone or anything before it teaches it";
            yield break;
        }

        foreach (BuildingRequirement needed in building.Requires)
        {
            if (needed.building != null && reach.TierBuilt(needed.building) < Mathf.Max(1, needed.tier))
            {
                yield return $"{building.name} needs {needed.building.name}{(needed.tier > 1 ? " at tier " + needed.tier : "")} standing, and no quest before it has the player build one";
            }
        }

        var costs = new List<ItemAmount>(building.ConstructionItems);
        for (int level = 2; level <= tier; level++)
        {
            BuildingLevel upgrade = building.UpgradeTo(level);
            if (upgrade != null)
            {
                costs.AddRange(upgrade.cost);
            }
        }

        foreach (ItemAmount cost in costs)
        {
            if (cost.Item != null && cost.Amount > 0 && !Had(cost.Item, reach, world, 0))
            {
                yield return $"{building.name} needs {cost.Item.name}, {WhyNot(cost.Item, reach, world)}";
            }
        }
    }

    // Had: the world gives it, a quest before this one gave it, or it can be made.
    private static bool Had(ItemDefinition item, Reach reach, World world, int depth) =>
        world.Natural.Contains(item) || reach.Rewarded.Contains(item) || reach.RumourGiven.Contains(item) || Makeable(item, reach, world, depth);

    private static bool Makeable(ItemDefinition item, Reach reach, World world, int depth)
    {
        if (depth > 6 || !world.MadeBy.TryGetValue(item, out List<RecipeDefinition> recipes))
        {
            return false;
        }

        foreach (RecipeDefinition recipe in recipes)
        {
            if (reach.Recipes.Contains(recipe) && StationReady(recipe, reach, world)
                && recipe.ItemCost.All(input => input.Item == null || input.Item == item || Had(input.Item, reach, world, depth + 1)))
            {
                return true;
            }
        }

        return false;
    }

    // By hand, at a station no building gives (the portal), or at a building of its kind a quest before this one had built, high enough.
    private static bool StationReady(RecipeDefinition recipe, Reach reach, World world)
    {
        if (recipe.IsHandcrafted || !world.Buildings.Any(b => b.Station == recipe.Station))
        {
            return true;
        }

        return world.Buildings.Any(b => b.Station == recipe.Station && reach.TierBuilt(b) >= recipe.RequiredStationTier);
    }

    private static string WhyNot(ItemDefinition item, Reach reach, World world)
    {
        if (!world.MadeBy.TryGetValue(item, out List<RecipeDefinition> recipes))
        {
            return "which only luck gives (no recipe makes it, the world doesn't give it)";
        }

        var ways = new List<string>();
        foreach (RecipeDefinition recipe in recipes)
        {
            string where = recipe.IsHandcrafted ? "by hand" : recipe.Station.DisplayName();
            if (!reach.Recipes.Contains(recipe))
            {
                ways.Add($"{recipe.name} ({where}) isn't known yet");
            }
            else if (!StationReady(recipe, reach, world))
            {
                ways.Add($"{recipe.name} needs the {where}{(recipe.RequiredStationTier > 1 ? " at tier " + recipe.RequiredStationTier : "")}, which no quest before it has the player build");
            }
            else
            {
                ways.Add($"{recipe.name} ({where}) needs what can't be had yet");
            }
        }

        return string.Join("; ", ways);
    }

    // The quests before it (all done), and what they leave the player with.
    private static Reach ReachBefore(QuestDefinition quest, World world)
    {
        var reach = new Reach();
        var stack = new Stack<QuestDefinition>(quest.Prerequisites.Where(q => q != null));
        while (stack.Count > 0)
        {
            QuestDefinition before = stack.Pop();
            if (!reach.Done.Add(before))
            {
                continue;
            }

            foreach (QuestDefinition earlier in before.Prerequisites)
            {
                if (earlier != null)
                {
                    stack.Push(earlier);
                }
            }
        }

        foreach (BuildingDefinition building in world.Buildings.Where(b => b.KnownFromStart))
        {
            reach.Known.Add(building);
        }

        foreach (RecipeDefinition recipe in world.AllRecipes.Where(r => r.KnownFromStart))
        {
            reach.Recipes.Add(recipe);
        }

        // A quest's own start teaches what it introduces (BuildingGate), as do the quests done before it.
        reach.Known.UnionWith(quest.UnlocksBuildings.Where(b => b != null));
        // And the recipes it teaches as it starts (Quest › Teaches At Start), as do the quests done before it.
        foreach (RecipeDefinition recipe in quest.TeachesAtStart.Where(r => r != null))
        {
            reach.Recipes.Add(recipe);
        }

        foreach (QuestDefinition done in reach.Done)
        {
            reach.Known.UnionWith(done.UnlocksBuildings.Where(b => b != null));
            foreach (RecipeDefinition recipe in done.RewardRecipes.Where(r => r != null))
            {
                reach.Recipes.Add(recipe);
            }

            foreach (RecipeDefinition recipe in done.TeachesAtStart.Where(r => r != null))
            {
                reach.Recipes.Add(recipe);
            }

            foreach (ItemAmount reward in done.RewardItems.Where(r => r.Item != null))
            {
                reach.Rewarded.Add(reward.Item);
            }

            foreach (QuestObjective objective in done.Objectives.Where(o => o != null))
            {
                Apply(objective, reach);
            }
        }

        return reach;
    }

    // What a done step leaves: a building standing (and what it and its upgrades teach), a person home (what their jobs teach).
    private static void Apply(QuestObjective objective, Reach reach)
    {
        if ((objective.Type == ObjectiveType.BuildStructure || objective.Type == ObjectiveType.UpgradeBuilding) && objective.Building != null)
        {
            BuildingDefinition building = objective.Building;
            int tier = Mathf.Max(objective.Type == ObjectiveType.UpgradeBuilding ? objective.Tier : 1, reach.TierBuilt(building));
            reach.Built[building] = tier;
            reach.Known.Add(building);
            reach.Recipes.UnionWith(building.TeachesRecipes.Where(r => r != null));
            for (int level = 2; level <= tier; level++)
            {
                BuildingLevel upgrade = building.UpgradeTo(level);
                if (upgrade != null)
                {
                    reach.Recipes.UnionWith(upgrade.teachesRecipes.Where(r => r != null));
                }
            }
        }
        else if (objective.Type == ObjectiveType.RescuePerson && objective.Person != null && objective.Person.Jobs != null)
        {
            foreach (JobDefinition job in objective.Person.Jobs.Where(j => j != null))
            {
                // Not what a quest introduces: that waits for its quest (BuildingGate), counted above when it has started.
                reach.Known.UnionWith(job.TeachesBuildings.Where(b => b != null && BuildingGate.IntroducedBy(b).Count == 0));
                reach.Recipes.UnionWith(job.TeachesRecipes.Where(r => r != null));
            }
        }
    }

    private static World LoadWorld()
    {
        var world = new World();
        world.Buildings.AddRange(Load<BuildingDefinition>());
        world.AllRecipes.AddRange(Load<RecipeDefinition>());
        foreach (RecipeDefinition recipe in world.AllRecipes)
        {
            if (recipe.Output == null)
            {
                continue;
            }

            if (!world.MadeBy.TryGetValue(recipe.Output, out List<RecipeDefinition> list))
            {
                world.MadeBy[recipe.Output] = list = new List<RecipeDefinition>();
            }

            list.Add(recipe);
        }

        var naturalPaths = new HashSet<string>(ContentAudit.Index()
            .Where(r => r.Kind == "Item" && WorldSources.Any(prefix => r.Field.StartsWith(prefix, System.StringComparison.Ordinal)))
            .Select(r => r.Target));
        // Kills: a creature's drops live in its Loot Table now (Data/Loot), not its inline list. What a creature gives anywhere counts
        // (no From Tier, not a raid-only part); a cache's table is luck and doesn't.
        foreach (MonsterDefinition monster in Load<MonsterDefinition>())
        {
            foreach (ItemDropEntry entry in monster.LootTable != null ? monster.LootTable.Drops.Entries : System.Array.Empty<ItemDropEntry>())
            {
                // A sure drop is not luck, raid-only part or not (the Brukam's tusk).
                if (entry != null && entry.Item != null && ((entry.FromTier <= 0 && !entry.Item.RaidOnly) || entry.Probability >= 1f))
                {
                    naturalPaths.Add(AssetDatabase.GetAssetPath(entry.Item));
                }
            }
        }

        // Rumours whose site gives a thing for sure, and the quest each waits for.
        foreach (RaidRumourDefinition rumour in Load<RaidRumourDefinition>())
        {
            if (rumour.Site == null || !rumour.Urgent)
            {
                continue;
            }

            foreach (RaidEventChoice choice in rumour.Site.Choices)
            {
                foreach (GiveLootOutcome give in (choice?.outcomes ?? new List<RaidEventOutcome>()).OfType<GiveLootOutcome>())
                {
                    foreach (ItemDropEntry entry in give.Loot.Entries)
                    {
                        if (entry != null && entry.Item != null && entry.Probability >= 1f && entry.FromTier <= 0)
                        {
                            if (!world.RumourGives.TryGetValue(entry.Item, out List<QuestDefinition> waits))
                            {
                                world.RumourGives[entry.Item] = waits = new List<QuestDefinition>();
                            }

                            waits.Add(rumour.WhileQuest);
                        }
                    }
                }
            }
        }

        string scenes = string.Concat(HomeScenes.Where(File.Exists).Select(File.ReadAllText));
        foreach (ItemDefinition item in Load<ItemDefinition>())
        {
            string path = AssetDatabase.GetAssetPath(item);
            if (naturalPaths.Contains(path) || scenes.Contains(AssetDatabase.AssetPathToGUID(path)) || item.IsWaterMeasure)
            {
                world.Natural.Add(item);
            }
        }

        return world;
    }

    private static List<T> Load<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g))).Where(a => a != null).ToList();
}
