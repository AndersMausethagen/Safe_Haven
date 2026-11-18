using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Guards the base progression's rules on the real content: every step can be reached, in order, with what the step before gave.</summary>
public sealed class BaseProgressionTests
{
    private static IEnumerable<T> All<T>() where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    private static BuildingDefinition Building(string file) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{file}.asset");

    private static ItemDefinition Item(string name) => All<ItemDefinition>().First(item => item.name == name);

    private static List<BuildingDefinition> InMenu() => All<BuildingDefinition>().Where(b => b.ShowInBuildMenu).ToList();

    [TestCase("ConstructionTable")]
    [TestCase("Sawbench")]
    [TestCase("Forge")]
    public void TheCoreBuildings_HaveFiveTiers(string file)
    {
        BuildingDefinition building = Building(file);
        Assert.AreEqual(Tiers.Max, building.MaxLevel);
        Assert.IsTrue(building.ShowInBuildMenu);
    }

    [Test]
    public void EveryRecipe_IsMadeAtAStationThatCanReachItsTier()
    {
        List<BuildingDefinition> buildings = InMenu();
        foreach (RecipeDefinition recipe in All<RecipeDefinition>())
        {
            if (recipe.IsHandcrafted || recipe.Station == CraftingStationType.Portal)
            {
                continue;
            }

            int most = buildings.Where(b => b.Station == recipe.Station).Select(b => b.MaxLevel).DefaultIfEmpty(0).Max();
            Assert.GreaterOrEqual(most, recipe.RequiredStationTier, $"{recipe.name}: no {recipe.Station} reaches tier {recipe.RequiredStationTier}");
        }
    }

    [Test]
    public void GearIsMadeAtItsOwnTier_AndMaterialsOneEarlier()
    {
        RecipeDefinition sword = All<RecipeDefinition>().First(recipe => recipe.Output == Item("CopperSword"));
        RecipeDefinition ingot = All<RecipeDefinition>().First(recipe => recipe.Output == Item("CopperIngot"));
        Assert.AreEqual(2, sword.RequiredStationTier);
        Assert.AreEqual(1, ingot.RequiredStationTier);
    }

    [TestCase("ConstructionTable")]
    [TestCase("Sawbench")]
    [TestCase("Forge")]
    public void EachUpgrade_CostsNothingFromAboveItsOwnTier(string file)
    {
        BuildingDefinition building = Building(file);
        for (int tier = 2; tier <= building.MaxLevel; tier++)
        {
            foreach (ItemAmount cost in building.UpgradeTo(tier).cost)
            {
                Assert.LessOrEqual(cost.Item.Tier, tier, $"{building.DisplayName} tier {tier} costs {cost.Item.DisplayName}");
            }
        }
    }

    [Test]
    public void TheSmithy_CanSmeltWhatItsNextUpgradeCosts()
    {
        BuildingDefinition smithy = Building("Forge");
        List<RecipeDefinition> recipes = All<RecipeDefinition>().ToList();
        for (int tier = 2; tier <= 4; tier++)
        {
            foreach (ItemAmount cost in smithy.UpgradeTo(tier).cost)
            {
                RecipeDefinition made = recipes.FirstOrDefault(recipe => recipe.Output == cost.Item && recipe.Station == CraftingStationType.Anvil);
                if (made != null)
                {
                    Assert.Less(made.RequiredStationTier, tier, $"{cost.Item.DisplayName} for tier {tier}");
                }
            }
        }
    }

    [Test]
    public void TheChain_HasNoLoops_AndStartsFromSomethingOpen()
    {
        // Each building's requirements are walked once, keeping the path that reached it: a building met again on its own path
        // is a loop, and fails here, where re-walking what was seen would run for ever.
        List<BuildingDefinition> menu = InMenu();
        var walked = new HashSet<BuildingDefinition>();
        var onPath = new HashSet<BuildingDefinition>();
        void Walk(BuildingDefinition building, string path)
        {
            if (building == null || walked.Contains(building))
            {
                return;
            }

            Assert.IsTrue(onPath.Add(building), $"a loop of requirements:{path} > {building.name}");
            foreach (BuildingRequirement requirement in building.Requires)
            {
                Walk(requirement.building, path + " > " + building.name);
            }

            onPath.Remove(building);
            walked.Add(building);
        }

        foreach (BuildingDefinition building in menu)
        {
            Walk(building, "");
        }

        Assert.IsTrue(menu.Any(building => !building.Requires.Any()), "some building needs nothing: the chain starts somewhere open");
    }

    [TestCase("CopperOre", "StonePickaxe")]
    [TestCase("IronOre", "CopperPickaxe")]
    [TestCase("MoonsilverOre", "IronPickaxe")]
    public void EachOre_NeedsThePickaxeOfTheTierBelow(string node, string pickaxe)
    {
        GatheringNodeDefinition definition = All<GatheringNodeDefinition>().First(one => one.name == node);
        int tool = Item(pickaxe).ToolTier;
        Assert.AreEqual(definition.RequiredToolTier, tool);
    }

    [Test]
    public void AQueue_RefusesARecipeAboveItsStationsTier()
    {
        var queue = new StationQueue("smithy", CraftingStationType.Anvil, 10, 5) { Tier = 1 };
        RecipeDefinition sword = All<RecipeDefinition>().First(recipe => recipe.Output == Item("CopperSword"));
        var player = new GameObject("Player");
        try
        {
            PlayerCrafting crafting = player.AddComponent<PlayerCrafting>();
            crafting.Unlock(new[] { sword }, false);
            StringAssert.Contains("Tier II", StationCrafting.Check(player, queue, sword));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void OpeningARequirement_NamesWhatIsMissing()
    {
        BuildingDefinition palisade = Building("Palisade");
        Assert.IsFalse(BuildingUnlocks.IsOpen(palisade));
        StringAssert.Contains(Building("Sawbench").DisplayName, BuildingUnlocks.Missing(palisade));
    }

    [Test]
    public void WatchPostLevels_TeachTheRaidRunes()
    {
        BuildingDefinition post = Building("WatchPost");
        Assert.AreEqual(3, post.MaxLevel);
        Assert.IsTrue(post.TeachesRecipes.Any(recipe => recipe.name == "WayfinderRune"));
        Assert.IsTrue(post.UpgradeTo(3).teachesRecipes.Any(recipe => recipe.name == "BloodMoonRune"));
    }
}
