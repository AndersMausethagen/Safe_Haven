using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Who or what answers a problem a raid showed: someone to bring home or give work, a plan to find, or a building the player
/// already knows of. Worded for the raid summary.
/// </summary>
public sealed class HavenAnswer
{
    /// <summary>Who knows how; null when it is a plan to find, or something the player can already do.</summary>
    public ResidentDefinition Person { get; set; }
    public JobDefinition Job { get; set; }
    /// <summary>True when they live in Safe Haven already: they need the job, not a rescue.</summary>
    public bool PersonIsHome { get; set; }
    /// <summary>A found item that teaches it, when nobody does.</summary>
    public ItemDefinition Plan { get; set; }
    /// <summary>Where it would be done: the workplace, or the station a carrier is made at.</summary>
    public BuildingDefinition Building { get; set; }
    /// <summary>For a load left behind: what would have carried it (a barrow, a cart).</summary>
    public ItemDefinition Carrier { get; set; }
    /// <summary>When building (1) or raising (2 and up) a known building teaches it: the tier to reach. 0 otherwise.</summary>
    public int Tier { get; set; }

    /// <summary>"Bring Bjarne home: the Smith knows how." Empty when nothing is to be done but use what is known.</summary>
    public string HowTo()
    {
        if (Person != null && Job != null)
        {
            return PersonIsHome ? $"Give {Person.DisplayName} the {Job.DisplayName} job." : $"Bring {Person.DisplayName} home: the {Job.DisplayName} knows how.";
        }

        if (Plan != null)
        {
            return $"Find the {Plan.DisplayName}.";
        }

        return Building == null || Tier <= 0 ? "" : Tier == 1 ? $"Build the {Building.DisplayName}." : $"Raise the {Building.DisplayName} to Tier {Tiers.ToRoman(Tier)}.";
    }
}

/// <summary>Something brought home that nobody at home can use yet, and who could.</summary>
public readonly struct UnusedFind
{
    public readonly Object What;
    public readonly int Amount;
    /// <summary>Who would know its use, best first (at most two). Empty when nobody in the game teaches it.</summary>
    public readonly IReadOnlyList<HavenAnswer> Answers;

    public UnusedFind(Object what, int amount, IReadOnlyList<HavenAnswer> answers)
    {
        What = what;
        Amount = amount;
        Answers = answers;
    }

    public string Name => What is IStackDefinition stack ? stack.DisplayName : What != null ? What.name : "";
}

/// <summary>
/// The problems a raid makes the player feel, and who at home answers each (Docs/RescueOrder.md: each person answers a problem
/// the player has just met). Three, for the raid summary:
/// <list type="bullet">
/// <item>Finds nobody at home can use yet: a material no known recipe or building takes, and who would know its use (copper
/// ore before the Smith, hides before the Hunter).</item>
/// <item>A load left behind: what would have carried it (a barrow from the Wainwright's Yard) and who knows how to make one.</item>
/// <item>Worn gear: who would mend it for less (the Smith), while nobody at home does.</item>
/// </list>
/// Everything is read from the recipes, buildings, jobs and residents as they are, so no hint is written by hand: a recipe
/// moved to another station moves its answer with it. Filled in once the raid is settled (RaidDirector), after a rescue has
/// taught what the rescued know.
/// </summary>
public static class HavenAnswers
{
    private const int MostAnswers = 2;

    /// <summary>Fills the report's problems: what came home unusable, what would have carried the load, who would mend the gear.</summary>
    public static void Fill(RaidReport report, PlayerCrafting crafting, IContentTeachers teachers, IHavenView settlement)
    {
        if (report == null || crafting == null || settlement == null)
        {
            return;
        }

        report.NoUseYet.Clear();
        if (report.Extracted)
        {
            foreach (ItemAmount gained in report.ItemsGained)
            {
                AddUnused(report, gained.Item, gained.Amount, crafting, teachers, settlement);
            }

            foreach (ResourceAmount gained in report.ResourcesGained)
            {
                AddUnused(report, gained.Resource, gained.Amount, crafting, teachers, settlement);
            }
        }

        report.Carrier = report.Extracted && (report.ItemsLeftBehind.Count > 0 || report.ResourcesLeftBehind.Count > 0)
            ? CarrierFor(crafting, teachers, settlement) : null;
        report.Mender = report.EquipmentDamaged.Count > 0 ? Mender(teachers, settlement) : null;
    }

    private static void AddUnused(RaidReport report, Object what, int amount, PlayerCrafting crafting, IContentTeachers teachers, IHavenView settlement)
    {
        List<HavenAnswer> answers = WhoCouldUse(what, crafting, teachers, settlement, out bool unusable);
        if (unusable)
        {
            report.NoUseYet.Add(new UnusedFind(what, amount, answers));
        }
    }

    /// <summary>
    /// Whether nobody at home can put a find to use yet (<paramref name="unusable"/>), and if so who would know its use, best
    /// first. Usable: something to eat, wear or read; or a known recipe at a known workplace takes it; or a known building costs
    /// it. A find no recipe or building in the game takes (a trophy, a valuable) is never unusable: there is nobody to ask.
    /// </summary>
    public static List<HavenAnswer> WhoCouldUse(Object what, PlayerCrafting crafting, IContentTeachers teachers, IHavenView settlement, out bool unusable)
    {
        var answers = new List<HavenAnswer>();
        unusable = false;
        if (what == null || crafting == null || (what is ItemDefinition item && item.IsUsable))
        {
            return answers;
        }

        var wantedBuildings = new List<BuildingDefinition>();
        var wantedRecipes = new List<RecipeDefinition>();
        foreach (RecipeDefinition recipe in crafting.Recipes)
        {
            if (recipe == null || !Takes(recipe, what))
            {
                continue;
            }

            bool stationKnown = StationKnown(recipe.Station, crafting, settlement);
            if (crafting.IsKnown(recipe) && stationKnown)
            {
                return new List<HavenAnswer>();
            }

            if (!stationKnown)
            {
                wantedBuildings.AddRange(crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu && b.Station == recipe.Station && !crafting.IsKnown(b)));
            }
            else
            {
                wantedRecipes.Add(recipe);
            }
        }

        foreach (BuildingDefinition building in crafting.Buildings)
        {
            if (building == null || !Costs(building, what))
            {
                continue;
            }

            if (Available(building, crafting, settlement))
            {
                return new List<HavenAnswer>();
            }

            if (building.ShowInBuildMenu)
            {
                wantedBuildings.Add(building);
            }
        }

        unusable = wantedBuildings.Count > 0 || wantedRecipes.Count > 0;
        // The earliest workplace in the build menu first: that is roughly who comes home first.
        foreach (BuildingDefinition building in wantedBuildings.Distinct().OrderBy(b => b.SortOrder))
        {
            AddTeachers(answers, teachers, settlement, building, null);
        }

        foreach (RecipeDefinition recipe in wantedRecipes.Distinct())
        {
            AddTeachers(answers, teachers, settlement, null, recipe);
            if (answers.Count == 0)
            {
                AddBuildingThatTeaches(answers, crafting, recipe);
            }
        }

        if (answers.Count > MostAnswers)
        {
            answers.RemoveRange(MostAnswers, answers.Count - MostAnswers);
        }

        return answers;
    }

    /// <summary>
    /// What would have carried a load left behind: the first carrier (an item that is a Transport) the player can make, or
    /// else who would teach the cheapest one. Null when the game has no carrier to make.
    /// </summary>
    public static HavenAnswer CarrierFor(PlayerCrafting crafting, IContentTeachers teachers, IHavenView settlement)
    {
        RecipeDefinition cheapest = null;
        foreach (RecipeDefinition recipe in crafting.Recipes.Where(r => r != null && r.Output != null && r.Output.Transport != null).OrderBy(r => r.Tier).ThenBy(r => r.SortOrder))
        {
            if (crafting.IsKnown(recipe) && StationKnown(recipe.Station, crafting, settlement))
            {
                return new HavenAnswer { Carrier = recipe.Output, Building = StationBuilding(recipe.Station, crafting) };
            }

            cheapest = cheapest ?? recipe;
        }

        if (cheapest == null)
        {
            return null;
        }

        var answers = new List<HavenAnswer>();
        BuildingDefinition station = StationBuilding(cheapest.Station, crafting);
        if (station != null && !crafting.IsKnown(station))
        {
            AddTeachers(answers, teachers, settlement, station, null);
        }
        else
        {
            AddTeachers(answers, teachers, settlement, null, cheapest);
        }

        HavenAnswer answer = answers.Count > 0 ? answers[0] : new HavenAnswer();
        answer.Carrier = cheapest.Output;
        answer.Building = station;
        return answer;
    }

    /// <summary>Who would mend gear for less (a job with a Gear Repair Discount), while nobody at home works it. Null otherwise.</summary>
    public static HavenAnswer Mender(IContentTeachers teachers, IHavenView settlement)
    {
        if (teachers == null || settlement == null || settlement.JobBonus(job => job.GearRepairDiscount) > 0f)
        {
            return null;
        }

        foreach (ResidentDefinition person in teachers.Residents)
        {
            JobDefinition job = person != null ? person.Jobs.FirstOrDefault(j => j != null && j.GearRepairDiscount > 0f) : null;
            if (job != null)
            {
                return new HavenAnswer { Person = person, Job = job, PersonIsHome = settlement.Find(person) != null, Building = job.Workplace };
            }
        }

        return null;
    }

    // Someone (a job that teaches it) or something (a plan) for each way to learn the building or recipe; a person once.
    private static void AddTeachers(List<HavenAnswer> answers, IContentTeachers teachers, IHavenView settlement, BuildingDefinition building, RecipeDefinition recipe)
    {
        if (teachers == null)
        {
            return;
        }

        foreach (ResidentDefinition person in teachers.Residents)
        {
            JobDefinition job = person != null ? person.Jobs.FirstOrDefault(j => j != null
                && (building != null ? j.TeachesBuildings.Contains(building) : j.TeachesRecipes.Contains(recipe))) : null;
            if (job != null && !answers.Any(a => a.Person == person) && !settlement.IsFallen(person))
            {
                answers.Add(new HavenAnswer { Person = person, Job = job, PersonIsHome = settlement.Find(person) != null, Building = building });
            }
        }

        if (answers.Count > 0)
        {
            return;
        }

        foreach (ItemDefinition plan in teachers.Items)
        {
            if (plan != null && (building != null ? plan.TeachesBuildings.Contains(building) : plan.TeachesRecipes.Contains(recipe)))
            {
                answers.Add(new HavenAnswer { Plan = plan, Building = building });
                return;
            }
        }
    }

    // A known building that teaches the recipe when it is built, or raised to a tier.
    private static void AddBuildingThatTeaches(List<HavenAnswer> answers, PlayerCrafting crafting, RecipeDefinition recipe)
    {
        foreach (BuildingDefinition building in crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu && crafting.IsKnown(b)).OrderBy(b => b.SortOrder))
        {
            int tier = building.TeachesRecipes.Contains(recipe) ? 1 : 0;
            for (int level = 2; tier == 0 && level <= building.MaxLevel; level++)
            {
                BuildingLevel upgrade = building.UpgradeTo(level);
                tier = upgrade != null && upgrade.teachesRecipes.Contains(recipe) ? level : 0;
            }

            if (tier > 0)
            {
                answers.Add(new HavenAnswer { Building = building, Tier = tier });
                return;
            }
        }
    }

    // A building the player has: one they can build from the menu, or one standing at home (an old one no longer in the menu
    // still works where it stands). A plan for a building that left the menu (the old Furnace) is no use.
    private static bool Available(BuildingDefinition building, PlayerCrafting crafting, IHavenView settlement) =>
        (building.ShowInBuildMenu && crafting.IsKnown(building)) || settlement.BuiltTier(building) > 0;

    // A station with no building of its own (the portal) is always there.
    private static bool StationKnown(CraftingStationType station, PlayerCrafting crafting, IHavenView settlement)
    {
        if (station == CraftingStationType.None)
        {
            return true;
        }

        bool any = false;
        foreach (BuildingDefinition building in crafting.Buildings)
        {
            if (building != null && building.Station == station)
            {
                any = true;
                if (Available(building, crafting, settlement))
                {
                    return true;
                }
            }
        }

        return !any;
    }

    private static BuildingDefinition StationBuilding(CraftingStationType station, PlayerCrafting crafting) =>
        station == CraftingStationType.None ? null : crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu && b.Station == station).OrderBy(b => b.SortOrder).FirstOrDefault();

    private static bool Takes(RecipeDefinition recipe, Object what) =>
        recipe.ItemCost.Any(cost => cost.Item != null && cost.Item == what) || recipe.ResourceCost.Any(cost => cost.Resource != null && cost.Resource == what);

    private static bool Costs(BuildingDefinition building, Object what)
    {
        if (building.ConstructionItems.Any(cost => cost.Item == what) || building.ConstructionCost.Any(cost => cost.Resource == what))
        {
            return true;
        }

        return building.Upgrades.Any(level => level != null && (level.cost.Any(cost => cost.Item == what) || level.essence.Any(cost => cost.Resource == what)));
    }
}
