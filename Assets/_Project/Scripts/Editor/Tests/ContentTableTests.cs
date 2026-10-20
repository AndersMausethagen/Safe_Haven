using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for the Content Editor's table view: CSV reading and importing balance changes.</summary>
public sealed class ContentTableTests
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

    [Test]
    public void ParseCsv_HandlesQuotesCommasAndLineBreaks()
    {
        List<List<string>> rows = ContentTableView.ParseCsv("\uFEFFa,\"b, c\",\"say \"\"hi\"\"\"\r\n1,\"two\nlines\",\n");

        Assert.AreEqual(2, rows.Count);
        CollectionAssert.AreEqual(new[] { "a", "b, c", "say \"hi\"" }, rows[0]);
        CollectionAssert.AreEqual(new[] { "1", "two\nlines", "" }, rows[1]);
    }

    [Test]
    public void Export_ThenImport_RoundTripsWithoutChanges()
    {
        RaidDefinition raid = Raid("wilds");
        string csv = ContentTableView.BuildCsv(new Object[] { raid }, new[] { "durationSeconds", "lootMultiplier", "tier" });

        ContentTableView.CsvImportPlan plan = ContentTableView.PlanImport(csv, new Object[] { raid });

        Assert.AreEqual(0, plan.ChangeCount);
        Assert.IsEmpty(plan.Errors);
    }

    [Test]
    public void Import_AppliesChangedValues_ClampsToLimits_AndReportsBadRows()
    {
        RaidDefinition first = Raid("first");
        RaidDefinition second = Raid("second");
        const string csv =
            "asset,name,durationSeconds,lootMultiplier\n" +
            "first,First,900,1\n" +
            "second,Second,5,abc\n" +
            "missing,Missing,100,1\n";

        ContentTableView.CsvImportPlan plan = ContentTableView.PlanImport(csv, new Object[] { first, second });

        Assert.AreEqual(2, plan.ChangeCount, "Only values that differ become changes.");
        Assert.AreEqual(2, plan.AssetCount);
        Assert.AreEqual(2, plan.Errors.Count, "The unreadable number and the unknown row are reported.");
        Assert.AreEqual(720f, first.GetBase(RaidStat.Duration), "Nothing changes before Apply.");

        plan.Apply();

        Assert.AreEqual(900f, first.GetBase(RaidStat.Duration));
        Assert.AreEqual(RaidParameters.MinimumDuration, second.GetBase(RaidStat.Duration), "[Min] is respected.");
        Assert.AreEqual(1f, second.GetBase(RaidStat.LootQuantity), "Invalid values are skipped.");
    }

    [Test]
    public void Import_RejectsFilesNotMadeByExport()
    {
        ContentTableView.CsvImportPlan plan = ContentTableView.PlanImport("name,value\nx,1\n", new Object[0]);

        Assert.AreEqual(0, plan.ChangeCount);
        Assert.AreEqual(1, plan.Errors.Count);
    }

    private RaidDefinition Raid(string name)
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        raid.name = name;
        created.Add(raid);
        return raid;
    }
}
