using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>The Dock (Fishing's mentor building, nets and lines that train the angler) and the Fisher who works it.</summary>
public sealed class DockTests
{
    [Test]
    public void TheDock_IsFishingsMentor_AndItsCatchesTrainFishing()
    {
        var dock = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Dock.asset");
        Assert.IsNotNull(dock);
        Assert.AreEqual(CraftingStationType.Dock, dock.Station);
        Assert.IsNotNull(dock.Prefab);
        Assert.IsNotNull(dock.Icon);
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition fishing = settings.FindSkill("fishing");
        Assert.AreEqual(dock, fishing.Mentor);

        var catches = AssetDatabase.FindAssets("t:RecipeDefinition", new[] { "Assets/_Project/Data/Crafting/Recipes" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(recipe => recipe != null && recipe.Station == CraftingStationType.Dock).ToList();
        Assert.GreaterOrEqual(catches.Count, 4);
        foreach (RecipeDefinition recipe in catches)
        {
            Assert.AreEqual(CraftedGoods.Catch, CraftedWork.Of(recipe), recipe.name);
            Assert.AreEqual(fishing, settings.SkillCrafting(CraftedWork.Of(recipe)), $"{recipe.name} trains Fishing");
        }

        Assert.Less(settings.CraftWork(CraftedGoods.Catch), settings.CatchWork, "a net brings in less than a fish landed by hand");
    }
}
