using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Small scenes between friends (SmallScenes, SmallSceneSettings; the Keeper's view, the people) and the horn (HavenHorn): every scene
/// has two different people who speak in turn, short lines with nothing cut off, and something to show from above; nobody is cheered
/// before a scene; the horn has a keeper and a sound, and isn't sounded away from home.
/// </summary>
public sealed class SmallScenesTests
{
    [Test]
    public void EveryScene_HasTwoPeople_WhoSpeakInTurn()
    {
        SmallSceneSettings settings = SmallSceneSettings.Current;
        Assert.Greater(settings.Scenes.Count, 5, "enough scenes that a week doesn't repeat them");
        var pairs = new HashSet<(ResidentDefinition, ResidentDefinition)>();
        foreach (SmallSceneSettings.Scene scene in settings.Scenes)
        {
            Assert.IsNotNull(scene.first);
            Assert.IsNotNull(scene.second);
            Assert.AreNotEqual(scene.first, scene.second, "a scene is between two");
            Assert.IsFalse(string.IsNullOrWhiteSpace(scene.doing), $"{scene.first.name} and {scene.second.name}: something to show from above");
            Assert.GreaterOrEqual(scene.lines.Length, 2, "an exchange, not a speech");
            foreach (string line in scene.lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line));
                Assert.Less(line.Length, 110, $"\"{line}\" is a word in passing, not a speech");
                StringAssert.DoesNotContain("…", line);
                StringAssert.DoesNotContain("...", line);
            }

            Assert.IsTrue(pairs.Add((scene.first, scene.second)), $"{scene.first.name} and {scene.second.name} twice");
        }
    }

    [Test]
    public void NobodyIsCheered_BeforeAScene_AndTheCheerIsSmall()
    {
        SmallScenes.ForgetGame();
        Assert.AreEqual(0f, SmallScenes.CheerOf(null));
        foreach (SmallSceneSettings.Scene scene in SmallSceneSettings.Current.Scenes)
        {
            Assert.AreEqual(0f, SmallScenes.CheerOf(scene.first));
        }

        Assert.Greater(SmallSceneSettings.Current.CheerSpeed, 0f);
        Assert.LessOrEqual(SmallSceneSettings.Current.CheerSpeed, 0.25f, "a lift, never a reason to stage scenes");
        Assert.IsFalse(SmallScenes.SpotFor(SmallSceneSettings.Current.Scenes[0].first, out _, out _), "no scene under way");
    }

    [Test]
    public void TheHorn_HasAKeeperAndASound_AndIsSoundedOnlyAtHome()
    {
        DefenceSettings settings = DefenceSettings.Current;
        Assert.IsNotNull(settings.HornKeeper, "Sigrid brings the horn");
        Assert.IsNotNull(settings.HornSound);
        Assert.Greater(settings.HornSeconds, 30f);
        HavenHorn.ForgetGame();
        Assert.IsFalse(HavenHorn.IsSounding);
        Assert.IsNotNull(HavenHorn.WhyNot(), "not at home in an Editor test");
        Assert.IsNotNull(HavenHorn.ShortWhyNot());
    }
}
