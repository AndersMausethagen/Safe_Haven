using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The raid's music follows the danger (Docs/GameDesignResearch_2026-11-17.md T1, Docs/LookAndFeelResearch_2026-11-18.md A5): each
/// stage begins with its stinger, the hunt's stage hushes the exploring music for a minute (a fight's music still plays), and the held
/// beacon's heartbeat quickens as it fills.
/// </summary>
public sealed class RaidMusicTests
{
    [Test]
    public void AHushSilencesTheExploringMusicButNotAFight()
    {
        var defaults = ScriptableObject.CreateInstance<AudioSettingsDefinition>();
        try
        {
            MusicChoice calm = MusicRules.Choose(null, defaults, AudioMood.Exploring, float.PositiveInfinity, true);
            Assert.IsNull(calm.Playlist, "hushed: the ambience alone");
            StringAssert.Contains("Hushed", calm.Reason);
            MusicChoice fight = MusicRules.Choose(null, defaults, AudioMood.Combat, float.PositiveInfinity, true);
            Assert.AreEqual(MusicRules.Choose(null, defaults, AudioMood.Combat, float.PositiveInfinity).Playlist, fight.Playlist,
                "a fight's music plays through the hush");
        }
        finally
        {
            Object.DestroyImmediate(defaults);
        }
    }

    [Test]
    public void TheHeldBeaconsHeartbeatQuickensAsItFills()
    {
        Assert.AreEqual(1.3f, ExtractionPoint.HeartbeatInterval(0f, 1.3f, 0.55f), 1e-4f, "slow as it is lit");
        Assert.AreEqual(0.55f, ExtractionPoint.HeartbeatInterval(1f, 1.3f, 0.55f), 1e-4f, "racing at the end");
        Assert.Less(ExtractionPoint.HeartbeatInterval(0.6f, 1.3f, 0.55f), ExtractionPoint.HeartbeatInterval(0.3f, 1.3f, 0.55f));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/ExtractionPoint.prefab");
        var heartbeat = new SerializedObject(prefab.GetComponentInChildren<ExtractionPoint>(true)).FindProperty("heartbeat").objectReferenceValue as SoundDefinition;
        Assert.IsNotNull(heartbeat, "the beacon drums");
        Assert.GreaterOrEqual(heartbeat.Clips.Count, 3, "never the same beat twice in a row");
    }

    [TestCase("MistyWoods")]
    [TestCase("TrollValley")]
    [TestCase("MossMarsh")]
    public void EachStageBeginsWithItsStingerAndTheHuntHushes(string raid)
    {
        var definition = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/" + raid + ".asset");
        Assert.AreEqual(3, definition.Stages.Count);
        for (int i = 0; i < definition.Stages.Count; i++)
        {
            Assert.IsNotNull(definition.Stages[i].Stinger, $"{raid} stage {i + 1} has a stinger");
            Assert.AreEqual(AudioChannel.Music, definition.Stages[i].Stinger.Channel, "under the music slider");
        }

        Assert.AreEqual(60f, definition.Stages[1].HushSeconds, "the quiet before the hunt");
        Assert.AreEqual(0f, definition.Stages[2].HushSeconds, "the last stage has its final stretch music");
    }
}
