using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Taking a building down gives back what it cost (BuildingDemolition, the user 2026-11-20), and refuses when it would lose something.</summary>
public sealed class BuildingDemolitionTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private static BuildingDefinition Building(string name) =>
        AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/" + name + ".asset");

    // A placed building of the kind, made inactive so nothing of it runs.
    private BuildingObject Placed(BuildingDefinition definition)
    {
        var go = new GameObject(definition.name);
        go.SetActive(false);
        created.Add(go);
        BuildingObject building = go.AddComponent<BuildingObject>();
        var data = new SerializedObject(building);
        data.FindProperty("definition").objectReferenceValue = definition;
        data.ApplyModifiedPropertiesWithoutUndo();
        return building;
    }

    [Test]
    public void TheRefundIsWhatTheBuildingCost()
    {
        int checkedBuildings = 0;
        foreach (string name in new[] { "StorageShed", "SmallChest", "ConstructionTable" })
        {
            BuildingDefinition definition = Building(name);
            Assert.IsNotNull(definition, name);
            var resources = new List<ResourceAmount>();
            var items = new List<ItemAmount>();
            BuildingDemolition.Refund(Placed(definition), resources, items);
            foreach (ItemAmount cost in definition.ConstructionItems.Where(c => c.Item != null && c.Amount > 0))
            {
                Assert.AreEqual(definition.ConstructionItems.Where(c => c.Item == cost.Item).Sum(c => c.Amount),
                    items.Where(i => i.Item == cost.Item).Sum(i => i.Amount), $"{name}: {cost.Item.name} comes back");
            }

            Assert.AreEqual(definition.ConstructionItems.Where(c => c.Item != null).Sum(c => c.Amount), items.Sum(i => i.Amount), $"{name}: nothing extra");
            checkedBuildings++;
        }

        Assert.AreEqual(3, checkedBuildings);
    }
}
