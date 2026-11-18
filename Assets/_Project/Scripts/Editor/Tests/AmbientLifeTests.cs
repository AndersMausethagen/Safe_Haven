using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Life in the raids' air between fights (Docs/LookAndFeelResearch_2026-11-18.md V2, V4): each raid and arena has its biome's
/// ambient life, the swarms come and go with the night and the rain as their profile says, gusts carry debris instead of keeping a
/// count, the Valley has its bird shadows, and the mote shader compiles.
/// </summary>
public sealed class AmbientLifeTests
{
    private const string DataDir = "Assets/_Project/Data/Environment/";

    [Test]
    public void ASwarmFollowsTheNightAndTheRain()
    {
        var moths = new AmbientLifeProfile.Swarm { byDay = 0, byNight = 10, inRain = 0.2f };
        Assert.AreEqual(0, AmbientLifeProfile.CountFor(moths, 0f, 0f), "no moths by day");
        Assert.AreEqual(10, AmbientLifeProfile.CountFor(moths, 1f, 0f), "all of them at night");
        Assert.AreEqual(2, AmbientLifeProfile.CountFor(moths, 1f, 1f), "a fifth in full rain");
        Assert.AreEqual(5, AmbientLifeProfile.CountFor(moths, 0.5f, 0f), "half at dusk");
        Assert.AreEqual(0, AmbientLifeProfile.CountFor(new AmbientLifeProfile.Swarm { gusts = true, byDay = 30 }, 0f, 0f),
            "a gust's debris comes with the gust, not kept at a count");
        Assert.AreEqual(0, AmbientLifeProfile.CountFor(null, 0f, 0f));
        var summer = new AmbientLifeProfile.Swarm { byDay = 20, summer = true };
        Assert.AreEqual(20, AmbientLifeProfile.CountFor(summer, 0f, 0f), "no seasons: all of summer's life");
        Assert.AreEqual(5, AmbientLifeProfile.CountFor(summer, 0f, 0f, 0.25f), "late in the year: a quarter");
        Assert.AreEqual(20, AmbientLifeProfile.CountFor(new AmbientLifeProfile.Swarm { byDay = 20 }, 0f, 0f, 0f), "dust and leaves all year");
    }

    [TestCase("Misty Woods Ambient Life", "Dust Motes", "Moths")]
    [TestCase("Troll Valley Ambient Life", "Pollen and Fluff", "Dust in the Gust")]
    [TestCase("Moss Marsh Ambient Life", "Midges", "Dragonflies")]
    public void EachBiomeHasItsOwnLife(string profileName, string first, string second)
    {
        var profile = AssetDatabase.LoadAssetAtPath<AmbientLifeProfile>(DataDir + profileName + ".asset");
        Assert.IsNotNull(profile, profileName);
        CollectionAssert.IsSupersetOf(profile.Swarms.Select(s => s.name).ToList(), new[] { first, second });
        foreach (AmbientLifeProfile.Swarm swarm in profile.Swarms)
        {
            Assert.IsNotNull(swarm.material, swarm.name);
            Assert.AreEqual("Safe Haven/Ambient Mote", swarm.material.shader.name, swarm.name);
            Assert.IsTrue(swarm.gusts || swarm.byDay + swarm.byNight > 0, $"{swarm.name} is ever seen");
        }

        Assert.IsFalse(ShaderUtil.ShaderHasError(Shader.Find("Safe Haven/Ambient Mote")));
        Assert.AreEqual(profileName == "Troll Valley Ambient Life", profile.BirdShadows.enabled, "a big bird's shadow over the open valley only");
        Assert.IsNotNull(profile.BirdShadows.shadowMaterial);
    }

    [TestCase("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity", "Misty Woods Ambient Life")]
    [TestCase("Assets/_Project/Scenes/Raids/Raid_TrollValley.unity", "Troll Valley Ambient Life")]
    [TestCase("Assets/_Project/Scenes/Raids/Raid_MossMarsh.unity", "Moss Marsh Ambient Life")]
    [TestCase("Assets/_Project/Scenes/Raids/Encounter_BrukamsBridge.unity", "Troll Valley Ambient Life")]
    [TestCase("Assets/_Project/Scenes/Raids/Encounter_GuardiansHollow.unity", "Misty Woods Ambient Life")]
    [TestCase("Assets/_Project/Scenes/SafeHaven.unity", "Haven Ambient Life")]
    [TestCase("Assets/_Project/Scenes/Prologue_MistyWoods.unity", "Misty Woods Ambient Life")]
    public void EachRaidSceneHasItsBiomesLife(string scenePath, string profileName)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            AmbientLife[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AmbientLife>(true)).ToArray();
            Assert.AreEqual(1, found.Length, "one per scene");
            Assert.IsNotNull(found[0].Profile);
            Assert.AreEqual(profileName, found[0].Profile.name);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
