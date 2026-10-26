using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("GatheringProgression")]
public sealed class BootstrapProgressionTests
{
    [TestCase("Sticks")]
    [TestCase("SmallRocks")]
    public void BootstrapNodesAreHandPickedTierZeroAndHaveMatchingPrefabs(string name)
    {
        var definition = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(
            $"Assets/_Project/Data/Gathering/{name}.asset");
        Assert.IsNotNull(definition);
        Assert.AreEqual(0, definition.Tier);
        Assert.AreEqual(ToolType.None, definition.GatherTool);
        Assert.AreEqual(0, definition.RequiredToolTier);
        Assert.IsNotNull(definition.WorldPrefab);
        Assert.IsEmpty(ResourceNodePrefabBuilder.FindProblems(definition.WorldPrefab, definition));
        var yield = definition.Yields.Entries.Single();
        Assert.AreEqual(0, yield.Item.Tier);
        Assert.Greater(yield.MinQuantity, 0);
        Assert.AreEqual(1f, yield.Probability);
        Assert.IsNotNull(yield.Item.Icon);
        Assert.IsNotNull(yield.Item.Prefab);
    }

    [TestCase("StoneHatchet")]
    [TestCase("StonePickaxe")]
    [TestCase("StoneSickle")]
    public void HandPickedMaterialsCraftFirstToolsWithoutWoodOrGreyStone(string name)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(
            $"Assets/_Project/Data/Crafting/Recipes/{name}.asset");
        Assert.IsNotNull(recipe);
        Assert.IsTrue(recipe.IsHandcrafted);
        Assert.IsTrue(recipe.KnownFromStart);
        Assert.IsEmpty(recipe.ResourceCost);
        CollectionAssert.AreEquivalent(new[] { "sticks", "small_rocks" }, recipe.ItemCost.Select(c => c.Item.Id));
        Assert.IsTrue(recipe.ItemCost.All(c => c.Item.Tier == 0));

        var user = new GameObject("Bootstrap test");
        try
        {
            var inventory = user.AddComponent<ItemInventory>();
            var crafting = user.AddComponent<PlayerCrafting>();
            crafting.Unlock(new[] { recipe }, false);
            foreach (string nodeName in new[] { "Sticks", "SmallRocks" })
            {
                var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(
                    $"Assets/_Project/Data/Gathering/{nodeName}.asset");
                var source = node.WorldPrefab.GetComponent<ResourceSource>();
                Assert.IsTrue(source.GrantTo(user));
                Assert.IsTrue(source.GrantTo(user));
            }
            Assert.IsTrue(crafting.Check(recipe).CanCraft);
            Assert.IsTrue(crafting.TryCraft(recipe));
            Assert.AreEqual(1, inventory.Container.Count(recipe.Output));
        }
        finally
        {
            Object.DestroyImmediate(user);
        }
    }

    [Test]
    public void StoneHatchetStopsAtTierOneButPickaxeKeepsCopperEntry()
    {
        var hatchet = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StoneHatchet.asset");
        var pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StonePickaxe.asset");
        Assert.AreEqual(1, hatchet.ToolTier);
        Assert.AreEqual(2, pickaxe.ToolTier);
    }

    [TestCase("OakWood", ToolType.Axe)]
    [TestCase("GreyStone", ToolType.Pickaxe)]
    public void PrimaryMaterialsRequireTierOneTools(string name, ToolType type)
    {
        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(
            $"Assets/_Project/Data/Gathering/{name}.asset");
        Assert.IsNotNull(node);
        Assert.AreEqual(type, node.GatherTool);
        Assert.AreEqual(1, node.RequiredToolTier);
    }
}
