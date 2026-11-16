using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TMPro;

public sealed class RaidHudTests
{
    [Test]
    public void TheWayLineIsShortUntilSomethingChangesTheChoice()
    {
        Assert.AreEqual("West Beacon  214 m", RaidHudText.Way("West Beacon", 214f, 500f, ""));
        Assert.AreEqual("West Beacon  214 m", RaidHudText.Way("West Beacon", 214f, -1f, ""), "An exit that never closes");
        StringAssert.Contains("closes in 0:45", RaidHudText.Way("West Beacon", 214f, 45f, ""));
        StringAssert.Contains("needs 2× Rune", RaidHudText.Way("West Beacon", 214f, 500f, "2× Rune"));
    }

    [Test]
    public void TheWorldMapKeepsWhatTheListSaid()
    {
        Assert.AreEqual("Hollow Oak, opens in 1:20", RaidHudText.MapLabel("Hollow Oak", ExtractionStatus.NotYetOpen, 80f, ""));
        Assert.AreEqual("East Beacon, gone dark", RaidHudText.MapLabel("East Beacon", ExtractionStatus.Closed, -1f, "Rune"));
        Assert.AreEqual("East Beacon, closes in 2:00, needs Rune", RaidHudText.MapLabel("East Beacon", ExtractionStatus.Open, 120f, "Rune"));
        Assert.AreEqual("West Beacon", RaidHudText.MapLabel("West Beacon", ExtractionStatus.Open, -1f, ""));
    }

    [Test]
    public void NewsComesOneAtATimeAndStaysLongEnoughToRead()
    {
        var line = new RaidAlertLine();
        Assert.AreEqual("", line.Current(0f));
        line.Post("first", 6f);
        line.Post("second", 6f);
        Assert.AreEqual("first", line.Current(1f));
        Assert.AreEqual("first", line.Current(6.9f));
        Assert.AreEqual("second", line.Current(7f));
        Assert.AreEqual("", line.Current(13f));
    }

    [Test]
    public void ABacklogDropsWhatIsLongPast()
    {
        var line = new RaidAlertLine();
        line.Post("one", 1f);
        line.Post("two", 1f);
        line.Post("three", 1f);
        line.Post("four", 1f);
        Assert.AreEqual("two", line.Current(0f));
    }

    [Test]
    public void NothingToSayIsNotNews()
    {
        var line = new RaidAlertLine();
        line.Post(null);
        line.Post("");
        Assert.AreEqual("", line.Current(0f));
    }

    [Test]
    public void ARumoursMarkHoldsStillAndLeadsToWhereItsBeaconShows()
    {
        const float BeaconShowsWithin = 40f;
        var random = new System.Random(7);
        for (int i = 0; i < 500; i++)
        {
            var spot = new Vector3((float)random.NextDouble() * 400f - 200f, 3f, (float)random.NextDouble() * 400f - 200f);
            Vector3 mark = RumourWay.RoughSpot(spot);
            Assert.Less(Vector3.Distance(spot, mark), BeaconShowsWithin, spot.ToString());
            Assert.AreEqual(mark, RumourWay.RoughSpot(mark), "The same mark however near the spot is");
        }

        Assert.AreNotEqual(new Vector3(1f, 0f, 1f), RumourWay.RoughSpot(new Vector3(1f, 0f, 1f)), "It doesn't give the spot away");
    }

    [Test]
    public void AnEventShowsAtOnceButARumourHasToBeFound()
    {
        var go = new GameObject("Site");
        try
        {
            var site = go.AddComponent<RaidEventSite>();
            go.transform.position = new Vector3(13f, 0f, 17f);
            Vector3 far = new Vector3(200f, 0f, 200f);
            Assert.IsTrue(site.IsFoundFrom(far));
            Assert.AreEqual(go.transform.position, site.MarkFrom(far));

            site.HideBeaconBeyond(40f);
            Assert.IsFalse(site.IsFoundFrom(far));
            Assert.AreEqual(RumourWay.RoughSpot(go.transform.position), site.MarkFrom(far));
            Vector3 near = new Vector3(13f, 5f, 40f);
            Assert.IsTrue(site.IsFoundFrom(near));
            Assert.AreEqual(go.transform.position, site.MarkFrom(near));
            Assert.IsTrue(site.IsFoundFrom(far), "once found it stays found: walking away doesn't bring the search area back");
            Assert.AreEqual(go.transform.position, site.MarkFrom(far));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void ThePrefabIsWiredForTheOneLine()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/RaidHUD.prefab");
        var view = new SerializedObject(prefab.GetComponent<RaidHudView>());
        foreach (string field in new[] { "alertLabel", "riskLabel", "companionLabel", "extractionLabel", "extractionArrow" })
        {
            Assert.IsNotNull(view.FindProperty(field).objectReferenceValue, field);
        }

        Assert.AreNotEqual(view.FindProperty("riskLabel").objectReferenceValue, view.FindProperty("companionLabel").objectReferenceValue);
        Assert.AreEqual("Exits", view.FindProperty("alertLabel").objectReferenceValue.name, "The serialized reference survived the rename");
    }
}
