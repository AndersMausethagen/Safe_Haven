using System.Linq;
using NUnit.Framework;

/// <summary>
/// Every quest can be started and done, and nothing waits for what can never come (QuestAudit, QuestChecklist). The first
/// audit (2026-11-11) found a quest waiting on a removed Drinking Spot and the Road North finishing before its conversation.
/// </summary>
public sealed class QuestAuditTests
{
    [OneTimeSetUp]
    public void FreshIndex() => QuestSceneIndex.Invalidate();

    [Test]
    public void EveryQuest_CanStart()
    {
        CollectionAssert.IsEmpty(QuestAudit.CantStart().Select(pair => $"{pair.Key.name}: {pair.Value}"));
    }

    [Test]
    public void EveryQuest_HasNothingMissing()
    {
        CollectionAssert.IsEmpty(QuestAudit.Missing(), "the quest's checklist in the Content Editor says what is missing, with a fix where there is one");
    }

    [Test]
    public void NothingWaits_ForWhatNeverComes()
    {
        CollectionAssert.IsEmpty(QuestAudit.WaitsForNothing());
    }

    [Test]
    public void NoStory_WaitsForWhatHappensInAnotherScene()
    {
        CollectionAssert.IsEmpty(QuestAudit.MissedInAnotherScene());
    }

    [Test]
    public void EveryPortalMoment_HasAStoryToPlay()
    {
        CollectionAssert.IsEmpty(QuestAudit.SilentPortalMoments());
    }

    [Test]
    public void EveryRescue_IsPlacedOnARaid()
    {
        CollectionAssert.IsEmpty(QuestAudit.UnplacedRescues());
    }
}
