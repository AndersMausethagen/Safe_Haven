using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The people (the Keeper's view, phase 6): a wish for a home waits until met and is thanked for once (HomeWishes, ResidentRecord.WishMet,
/// saved), every resident who likes something says thanks in their own words, and the lines about a raid fill in without breaking.
/// </summary>
public sealed class HomeWishesTests
{
    [Test]
    public void AWish_WaitsUntilMet_AndIsSaved()
    {
        HomeLifeSettings.Liking liking = null;
        foreach (HomeLifeSettings.Liking one in HomeLifeSettings.Current.Likings)
        {
            liking = one.resident != null ? one : liking;
        }

        Assert.IsNotNull(liking, "someone likes something in a home");
        var home = new SettlementState();
        home.AddResident(liking.resident, null);
        ResidentRecord record = home.Find(liking.resident);
        Assert.AreEqual(liking.likes, HomeWishes.WishOf(record), "no home yet: the wish waits");
        record.WishMet = true;
        Assert.IsNull(HomeWishes.WishOf(record), "met once, it is no longer shown");

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.IsTrue(back.Find(liking.resident).WishMet, "a wish met stays met");
    }

    [Test]
    public void EveryoneWhoLikesSomething_SaysThanksInTheirOwnWords()
    {
        int likings = 0;
        foreach (HomeLifeSettings.Liking liking in HomeLifeSettings.Current.Likings)
        {
            likings++;
            Assert.IsFalse(string.IsNullOrWhiteSpace(liking.thanks), $"{(liking.resident != null ? liking.resident.name : "?")} has no thanks for their wish");
        }

        Assert.Greater(likings, 5);
    }

    [Test]
    public void TheRaidLines_FillInWithoutBreaking()
    {
        HomecomingSettings settings = HomecomingSettings.Current;
        Assert.Greater(settings.BroughtLines.Length, 0);
        foreach (string line in settings.BroughtLines)
        {
            string said = string.Format(line, 4, "Copper Ore", "Smithy");
            StringAssert.DoesNotContain("{", said, "a line names a part that isn't given");
        }

        Assert.Greater(settings.FellLines.Length + settings.AlongLines.Length, 0);
    }
}
