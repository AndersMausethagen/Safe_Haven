using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Edit Mode tests for raid tension: danger stages, extraction windows and which exits a raid offers.</summary>
public sealed class RaidTensionTests
{
    private static List<RaidStage> Stages()
    {
        var stages = new List<RaidStage>
        {
            new RaidStage("Collapse", 60f),
            new RaidStage("Stirring", 480f),
            new RaidStage("Hunt", 240f),
        };
        RaidStages.Sort(stages);
        return stages;
    }

    [Test]
    public void Stages_SortEarliestFirst()
    {
        List<RaidStage> stages = Stages();

        Assert.AreEqual("Stirring", stages[0].DisplayName);
        Assert.AreEqual("Hunt", stages[1].DisplayName);
        Assert.AreEqual("Collapse", stages[2].DisplayName);
    }

    [Test]
    public void StageIndex_FollowsTheClock()
    {
        List<RaidStage> stages = Stages();

        Assert.AreEqual(0, RaidStages.IndexAt(stages, 720f), "Calm at the start.");
        Assert.AreEqual(0, RaidStages.IndexAt(stages, 480.1f));
        Assert.AreEqual(1, RaidStages.IndexAt(stages, 480f), "A stage begins exactly at its threshold.");
        Assert.AreEqual(2, RaidStages.IndexAt(stages, 200f));
        Assert.AreEqual(3, RaidStages.IndexAt(stages, 0f));
        Assert.AreEqual(0, RaidStages.IndexAt(null, 0f));
    }

    [Test]
    public void StageGet_IsNullForTheCalmAndOutOfRange()
    {
        List<RaidStage> stages = Stages();

        Assert.IsNull(RaidStages.Get(stages, 0));
        Assert.AreEqual("Hunt", RaidStages.Get(stages, 2).DisplayName);
        Assert.IsNull(RaidStages.Get(stages, 4));
    }

    [Test]
    public void ExtractionWindow_OpensAndCloses()
    {
        // Open from the start, never closes.
        Assert.AreEqual(ExtractionStatus.Open, ExtractionWindow.StatusAt(0f, 0f, 700f));
        Assert.AreEqual(-1f, ExtractionWindow.SecondsUntilChange(0f, 0f, 700f));

        // Opens in the last three minutes.
        Assert.AreEqual(ExtractionStatus.NotYetOpen, ExtractionWindow.StatusAt(180f, 0f, 300f));
        Assert.AreEqual(120f, ExtractionWindow.SecondsUntilChange(180f, 0f, 300f), 0.001f);
        Assert.AreEqual(ExtractionStatus.Open, ExtractionWindow.StatusAt(180f, 0f, 180f));

        // Goes dark with four minutes left.
        Assert.AreEqual(ExtractionStatus.Open, ExtractionWindow.StatusAt(0f, 240f, 600f));
        Assert.AreEqual(360f, ExtractionWindow.SecondsUntilChange(0f, 240f, 600f), 0.001f);
        Assert.AreEqual(ExtractionStatus.Closed, ExtractionWindow.StatusAt(0f, 240f, 240f));
        Assert.AreEqual(-1f, ExtractionWindow.SecondsUntilChange(0f, 240f, 100f));
    }

    [Test]
    public void PickAvailable_AlwaysOffersAWayOut()
    {
        var chances = new List<float> { 0.05f, 0.05f, 0.05f };
        for (int seed = 0; seed < 300; seed++)
        {
            bool[] available = RaidLayout.PickAvailable(new System.Random(seed), chances);
            Assert.IsTrue(System.Array.Exists(available, a => a), $"Seed {seed} left no exit.");
        }

        bool[] none = RaidLayout.PickAvailable(new System.Random(1), new List<float> { 0f, 0f });
        Assert.IsTrue(none[0], "With every chance at 0 the first exit is kept.");
    }

    [Test]
    public void PickAvailable_CertainExitsAlwaysOfferedAndIsDeterministic()
    {
        var chances = new List<float> { 1f, 0.5f, 0.5f };
        int offered = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            bool[] available = RaidLayout.PickAvailable(new System.Random(seed), chances);
            Assert.IsTrue(available[0]);
            offered += available[1] ? 1 : 0;
            CollectionAssert.AreEqual(available, RaidLayout.PickAvailable(new System.Random(seed), chances));
        }

        Assert.That(offered, Is.InRange(140, 260), "A 50% exit is offered about half the time.");
    }
}
