using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edit Mode tests for the planner import: new vs changed vs unchanged, no duplicates, and the planner never overwrites.</summary>
public sealed class PlannerImporterTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    private static PlannerEntity Item(string id, int tier, bool isNew = true)
    {
        var entity = new PlannerEntity { kind = "item", key = "item:" + id, id = id, isNew = isNew, typeName = "ItemDefinition", path = "Assets/_Project/Data/Items/Materials/" + id + ".asset" };
        entity.fields.Add(new PlannerField { name = "tier", ftype = "int", value = tier.ToString() });
        entity.fields.Add(new PlannerField { name = "category", ftype = "enum", value = "2" });
        entity.fields.Add(new PlannerField { name = "weight", ftype = "float", value = "0.5" });
        return entity;
    }

    private Object InMemory(Type type, string path)
    {
        var asset = ScriptableObject.CreateInstance(type);
        created.Add(asset);
        return asset;
    }

    [TestCase(0)]
    [TestCase(2)]
    public void New_ThenSameManifest_IsUnchangedAndNotDuplicated(int tier)
    {
        var manifest = new PlannerManifest { version = 1 };
        manifest.entities.Add(Item("plannertest_ore", tier));

        var plan = PlannerImporter.Plan(manifest, new Object[0]);
        Assert.AreEqual(PlannerPlanEntry.Kind.New, plan[0].State);
        PlannerImporter.Apply(plan, InMemory);

        var item = (ItemDefinition)plan[0].Asset;
        Assert.AreEqual("plannertest_ore", item.Id);
        Assert.AreEqual(tier, item.Tier);
        Assert.AreEqual(0.5f, item.Weight);

        var again = PlannerImporter.Plan(manifest, new Object[] { item });
        Assert.AreEqual(PlannerPlanEntry.Kind.Unchanged, again[0].State);
    }

    [Test]
    public void Changed_OnlyTouchesListedFields()
    {
        var manifest = new PlannerManifest { version = 1 };
        manifest.entities.Add(Item("plannertest_wood", 1));
        var first = PlannerImporter.Plan(manifest, new Object[0]);
        PlannerImporter.Apply(first, InMemory);
        var item = (ItemDefinition)first[0].Asset;

        var edit = new PlannerManifest { version = 1 };
        var entity = Item("plannertest_wood", 3, isNew: false);
        entity.fields.RemoveAll(f => f.name != "tier");
        edit.entities.Add(entity);

        var plan = PlannerImporter.Plan(edit, new Object[] { item });
        Assert.AreEqual(PlannerPlanEntry.Kind.Changed, plan[0].State);
        Assert.AreEqual(1, plan[0].Changes.Count);
        Assert.AreEqual("1", plan[0].Changes[0].Old);
        PlannerImporter.Apply(plan, InMemory);
        Assert.AreEqual(3, item.Tier);
        Assert.AreEqual(0.5f, item.Weight);
    }

    [Test]
    public void UnknownReference_IsAnErrorAndNotApplied()
    {
        var manifest = new PlannerManifest { version = 1 };
        var recipe = new PlannerEntity { kind = "recipe", key = "recipe:plannertest_r", id = "plannertest_r", isNew = true, path = "Assets/_Project/Data/Crafting/Recipes/R.asset" };
        var cost = new PlannerField { name = "itemCost", ftype = "amounts" };
        cost.lines.Add(new PlannerLine { target = "item:does_not_exist", amount = 1 });
        recipe.fields.Add(cost);
        manifest.entities.Add(recipe);

        var plan = PlannerImporter.Plan(manifest, new Object[0]);

        Assert.AreEqual(PlannerPlanEntry.Kind.Error, plan[0].State);
        Assert.IsFalse(plan[0].Apply);
    }

    [Test]
    public void NewPathOutsideAssets_IsRejected()
    {
        var manifest = new PlannerManifest { version = 1 };
        var entity = Item("plannertest_bad", 1);
        entity.path = "../outside.asset";
        manifest.entities.Add(entity);

        Assert.AreEqual(PlannerPlanEntry.Kind.Error, PlannerImporter.Plan(manifest, new Object[0])[0].State);
    }
}
