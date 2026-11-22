using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards and their posts (GuardPosts, DefenceSettings; the Keeper's view, phase 7): how many may stand by the defences built, the order
/// guards take the posts, where they stand toward a corruption night, when a post can be set, and posts kept by a save.
/// </summary>
public sealed class GuardPostsTests
{
    private static (ResidentDefinition who, JobDefinition job) ADefender()
    {
        foreach (ResidentDefinition resident in Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Residents)
        {
            foreach (JobDefinition job in resident.Jobs)
            {
                if (job != null && job.DefendsAs != null)
                {
                    return (resident, job);
                }
            }
        }

        return (null, null);
    }

    [Test]
    public void TheDefencesBuilt_SetHowManyGuardsStand()
    {
        DefenceSettings settings = DefenceSettings.Current;
        Assert.AreEqual(settings.BasePosts, GuardPosts.Cap(0, settings), "with nothing built, the base");
        Assert.AreEqual(Mathf.Min(settings.MostPosts, settings.BasePosts + 2 * settings.PostsPerWatchPostTier), GuardPosts.Cap(2, settings),
            "a Watch Post II adds its tiers");
        Assert.AreEqual(settings.MostPosts, GuardPosts.Cap(1000, settings), "never more than Most Posts");
        Assert.GreaterOrEqual(settings.BasePosts, 2, "the Warden and the Hunter both stand as they did before posts");
    }

    [Test]
    public void GuardsTakeThePostsInOrder_AndOnlyThoseWhoDefend()
    {
        (ResidentDefinition defender, JobDefinition job) = ADefender();
        Assert.IsNotNull(defender, "some job keeps guard");
        var home = new SettlementState();
        ResidentDefinition other = null;
        foreach (ResidentDefinition resident in Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Residents)
        {
            other = resident != defender ? resident : other;
        }

        home.AddResident(other, null);
        home.AddResident(defender, job);
        Assert.AreEqual(-1, GuardPosts.Rank(home, home.Find(other)), "no defending job, no post");
        Assert.AreEqual(0, GuardPosts.Rank(home, home.Find(defender)), "the first who defends takes the first post");

        Assert.IsNull(GuardPosts.WhyNoPost(home, DefenceSettings.Current, false));
        Assert.IsNotNull(GuardPosts.WhyNoPost(home, DefenceSettings.Current, true), "a friend's Haven");
        Assert.AreEqual(defender.DisplayName, GuardPosts.GuardFor(home, 0), "the first post is theirs");
        home.AddPost(new Vector3(1f, 0f, 0f));
        Assert.IsNotNull(GuardPosts.WhyNoPost(home, DefenceSettings.Current, false), "a post for each guard, no more: one keeps guard here");
        Assert.IsNotNull(GuardPosts.WhyNoPost(new SettlementState(), DefenceSettings.Current, false), "nobody keeps guard");
    }

    [Test]
    public void OnACorruptionNight_GuardsStandTowardItsSide()
    {
        DefenceSettings settings = DefenceSettings.Current;
        Vector3 first = GuardPosts.TowardTheNight(Vector3.zero, 90f, 0, settings);
        Assert.AreEqual(settings.NightPostDistance, first.x, 1e-3f, "east, at the night's distance");
        Assert.AreEqual(0f, first.z, 1e-3f);
        Vector3 second = GuardPosts.TowardTheNight(Vector3.zero, 90f, 1, settings);
        Vector3 third = GuardPosts.TowardTheNight(Vector3.zero, 90f, 2, settings);
        Assert.Less(second.z * third.z, 0f, "the next two stand either side");
    }

    [Test]
    public void OnACorruptionNight_OnlyThePostsOnItsSideAreTaken()
    {
        var posts = new System.Collections.Generic.List<Vector3> { new Vector3(10f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(8f, 0f, 3f) };
        Assert.AreEqual(3, GuardPosts.PostsFor(posts, Vector3.zero, false, 90f, DefenceSettings.Current).Count, "a breach: every post");
        System.Collections.Generic.List<Vector3> east = GuardPosts.PostsFor(posts, Vector3.zero, true, 90f, DefenceSettings.Current);
        Assert.AreEqual(2, east.Count, "a night from the east: the two eastern posts");
        Assert.AreEqual(10f, east[0].x, 1e-3f, "in order");
    }

    [Test]
    public void Posts_AreSaved()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var home = new SettlementState();
        home.AddPost(new Vector3(3f, 1f, -8f));
        home.AddPost(new Vector3(-2f, 0f, 5f));
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.AreEqual(2, back.Posts.Count);
        Assert.AreEqual(-8f, back.Posts[0].z, 1e-3f);
        Assert.AreEqual(-2f, back.Posts[1].x, 1e-3f, "in order");
    }
}
