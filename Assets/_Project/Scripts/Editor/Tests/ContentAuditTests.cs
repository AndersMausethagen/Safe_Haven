using NUnit.Framework;

/// <summary>
/// The game's content has no loose ends (ContentAudit): nothing points at what was deleted, everything can be learned,
/// everything asked for can be had, everything named can be met. The build runs the same checks (SafeHavenBuild).
/// </summary>
public sealed class ContentAuditTests
{
    [OneTimeSetUp]
    public void ReadOnce() => ContentAudit.Invalidate();

    [Test]
    public void NothingPointsAtWhatIsGone()
    {
        CollectionAssert.IsEmpty(ContentAudit.BrokenReferences());
    }

    [Test]
    public void EveryContentField_IsSorted()
    {
        CollectionAssert.IsEmpty(ContentAudit.UnsortedFields(), "a new field points at items, recipes, buildings or monsters: say what it means in ContentAudit's lists");
    }

    [Test]
    public void EveryRecipe_CanBeLearned()
    {
        CollectionAssert.IsEmpty(ContentAudit.UnlearnableRecipes());
    }

    [Test]
    public void EveryBuilding_CanBeLearned()
    {
        CollectionAssert.IsEmpty(ContentAudit.UnlearnableBuildings());
    }

    [Test]
    public void EveryItemAskedFor_CanBeHad()
    {
        CollectionAssert.IsEmpty(ContentAudit.ItemsThatNeverCome());
    }

    [Test]
    public void EveryMonsterNamed_IsSpawnedSomewhere()
    {
        CollectionAssert.IsEmpty(ContentAudit.MonstersNothingSpawns());
    }
}
