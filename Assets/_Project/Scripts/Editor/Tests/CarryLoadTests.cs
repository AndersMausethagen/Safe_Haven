using NUnit.Framework;
using UnityEditor;

public sealed class CarryLoadTests
{
    [TestCase(0f, 100f, 0f)]
    [TestCase(80f, 100f, 0f)]
    [TestCase(90f, 100f, 0.5f)]
    [TestCase(100f, 100f, 1f)]
    [TestCase(120f, 100f, 1f)]
    public void ABagTellsOnlyPastTheMarkAndEasesInToFull(float weight, float capacity, float over)
    {
        Assert.AreEqual(over, CarryLoad.Over(weight, capacity, 0.8f), 0.001f);
    }

    [Test]
    public void ABagWithNoBudgetIsNeverHeavy()
    {
        Assert.AreEqual(0f, CarryLoad.Over(50f, 0f, 0.8f));
    }

    [Test]
    public void ScalesGoFromNormalToTheirFullBagValue()
    {
        Assert.AreEqual(1f, CarryLoad.Scale(0f, 0.8f));
        Assert.AreEqual(0.9f, CarryLoad.Scale(0.5f, 0.8f), 0.001f);
        Assert.AreEqual(0.8f, CarryLoad.Scale(1f, 0.8f));
    }

    [Test]
    public void TheHaulSackIsABargainWithACost()
    {
        var sack = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HaulSack.asset");
        var rucksack = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/HideRucksack.asset");
        Assert.Greater(sack.CarryCapacityBonus, rucksack.CarryCapacityBonus, "It has to carry more than the best pack");
        Assert.Less(sack.BagSlotBonus, rucksack.BagSlotBonus, "with fewer slots");
        Assert.Greater(sack.CarryNoise, 0f, "and be heard");
        Assert.Less(sack.CarrySpeedScale, 1f, "and be slow");
        Assert.AreEqual(1f, rucksack.CarrySpeedScale, "An ordinary pack costs nothing");
        Assert.AreEqual(0f, rucksack.CarryNoise);
        Assert.IsNotNull(ContentLookup(sack.Id), "It has to survive a save");
    }

    private static ItemDefinition ContentLookup(string id)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (ItemDefinition item in database.Items)
            {
                if (item != null && item.Id == id)
                {
                    return item;
                }
            }
        }

        return null;
    }
}
