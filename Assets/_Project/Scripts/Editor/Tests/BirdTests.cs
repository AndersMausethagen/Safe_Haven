using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Birds: they settle, they startle by what they hear, and they leave. Mood only, so nothing here touches the game.</summary>
public sealed class BirdTests
{
    private const string Haven = "Assets/_Project/Data/World/Birds - Haven.asset";
    private const string Misty = "Assets/_Project/Data/World/Birds - Misty Woods.asset";

    private BirdProfile profile;

    [SetUp]
    public void SetUp() => profile = AssetDatabase.LoadAssetAtPath<BirdProfile>(Haven);

    private BirdFlock Settled()
    {
        var spots = new[] { new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f) };
        BirdFlock flock = BirdFlock.Landing(profile, new Vector3(0f, 8f, 20f), Vector3.zero, spots);
        for (int i = 0; i < 2000 && !flock.IsSettled; i++)
        {
            flock.Tick(0.02f);
        }

        return flock;
    }

    [Test]
    public void AFlock_FliesInAndSettles()
    {
        BirdFlock flock = Settled();
        Assert.IsTrue(flock.IsSettled);
        Assert.IsFalse(flock.IsLeaving);
        Assert.IsFalse(flock.IsGone);
    }

    [Test]
    public void StartledFlocks_GoUpAndAway_ThenAreGone()
    {
        BirdFlock flock = Settled();
        Vector3 before = flock.Birds[0].Position;
        flock.Startle(new Vector3(0f, 0f, -5f));
        Assert.IsTrue(flock.IsLeaving);
        Assert.IsFalse(flock.IsSettled, "a flock already up can't be startled again");
        for (int i = 0; i < 100; i++)
        {
            flock.Tick(0.02f);
        }

        Assert.Greater(flock.Birds[0].Position.y, before.y + 1f);
        for (int i = 0; i < 1000 && !flock.IsGone; i++)
        {
            flock.Tick(0.02f);
        }

        Assert.IsTrue(flock.IsGone);
    }

    [Test]
    public void TheyFlyFromWhatFrightenedThem()
    {
        BirdFlock flock = Settled();
        Vector3 threat = new Vector3(0f, 0f, -8f);
        flock.Startle(threat);
        for (int i = 0; i < 150; i++)
        {
            flock.Tick(0.02f);
        }

        foreach (Bird bird in flock.Birds)
        {
            Assert.Greater(bird.Position.z, threat.z + 8f, "away, give or take their scatter");
        }
    }

    [Test]
    public void StartlingTwice_ChangesNothing()
    {
        BirdFlock flock = Settled();
        flock.Startle(Vector3.zero);
        Vector3 velocity = flock.Birds[0].Velocity;
        flock.Startle(new Vector3(50f, 0f, 50f));
        Assert.AreEqual(velocity, flock.Birds[0].Velocity);
    }

    [Test]
    public void APassingFlock_CantBeStartled_AndLeavesByItself()
    {
        BirdFlock flock = BirdFlock.Passing(profile, new Vector3(0f, 10f, 0f), Vector3.forward * 8f, 4);
        Assert.IsTrue(flock.IsLeaving);
        Assert.IsFalse(flock.IsSettled);
        for (int i = 0; i < 1000 && !flock.IsGone; i++)
        {
            flock.Tick(0.02f);
        }

        Assert.IsTrue(flock.IsGone);
    }

    [Test]
    public void Sneaking_GetsCloserThanRunning()
    {
        float sneaking = profile.StartleDistance(PerceptionSettings.Current.SneakingFootstepNoise);
        float running = profile.StartleDistance(PerceptionSettings.Current.FootstepNoise);
        Assert.Less(sneaking, running);
        Assert.Greater(profile.StartleDistance(0f), 0f, "nothing walks right up to a bird, however quiet");
        Assert.AreEqual(profile.StartleDistance(1000f), profile.StartleDistance(2000f), "a limit, so a fight doesn't clear the whole map");
        Assert.Less(profile.StartleDistance(1000f), profile.LandingDistance.x, "they don't land only to leave");
    }

    [Test]
    public void AtNightAndInRain_FewerStayOut()
    {
        BirdProfile crows = AssetDatabase.LoadAssetAtPath<BirdProfile>(Misty);
        Assert.Greater(profile.FlocksFor(0f, 0f), 0);
        Assert.AreEqual(0, profile.FlocksFor(1f, 0f), "songbirds roost");
        Assert.Greater(crows.FlocksFor(1f, 0f), 0, "a few crows don't");
        Assert.Less(crows.FlocksFor(1f, 0f), crows.FlocksFor(0f, 0f));
        Assert.Less(profile.FlocksFor(0f, 1f), profile.FlocksFor(0f, 0f));
    }

    [Test]
    public void TheFrames_FollowWhatItIsDoing()
    {
        var bird = new Bird { State = BirdState.Perched };
        Assert.AreEqual(Bird.PerchedFrame, BirdFlock.FrameOf(bird));
        bird.Pecking = true;
        Assert.AreEqual(Bird.PeckingFrame, BirdFlock.FrameOf(bird));
        bird = new Bird { State = BirdState.Leaving };
        for (float flap = 0f; flap < 3f; flap += 0.07f)
        {
            bird.Flap = flap;
            int frame = BirdFlock.FrameOf(bird);
            Assert.GreaterOrEqual(frame, Bird.FirstFlapFrame);
            Assert.Less(frame, Bird.FirstFlapFrame + Bird.FlapFrames);
        }

        bird.Gliding = true;
        Assert.AreEqual(Bird.HeldOutFrame, BirdFlock.FrameOf(bird));
    }

    [TestCase(0)]
    [TestCase(7)]
    public void APictureMapsToAParticleLifeOnTheSheet(int picture)
    {
        float life = Birds.LifeForPicture(picture);
        Assert.Greater(life, 0f);
        Assert.Less(life, 1f);
        Assert.AreEqual(picture, Mathf.FloorToInt((1f - life) * 8f));
    }

    [TestCase("Assets/_Project/Scenes/SafeHaven.unity")]
    [TestCase("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity")]
    [TestCase("Assets/_Project/Scenes/Raids/Raid_TrollValley.unity")]
    [TestCase("Assets/_Project/Scenes/Prologue_MistyWoods.unity")]
    public void TheScenes_HaveTheirBirds(string scene)
    {
        string prefab = AssetDatabase.AssetPathToGUID("Assets/_Project/Prefabs/Environment/Birds.prefab");
        StringAssert.Contains(prefab, File.ReadAllText(scene));
    }

    [Test]
    public void EachPlace_HasItsOwnBirds()
    {
        BirdProfile crows = AssetDatabase.LoadAssetAtPath<BirdProfile>(Misty);
        Assert.IsNotNull(profile.Call);
        Assert.IsNotNull(profile.TakeOff);
        Assert.IsNotNull(crows.Call);
        Assert.AreNotEqual(profile.Call, crows.Call);
        Assert.IsTrue(crows.CallsOnTakeOff, "crows go up shouting");
    }
}
