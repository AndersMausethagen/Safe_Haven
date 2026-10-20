using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SoundtrackMusicTests
{
    private AudioClip exploringClip, combatClip, finalClip, defaultExploringClip, defaultCombatClip;
    private GameObject host;
    private AudioSettingsDefinition defaults;

    [SetUp]
    public void SetUp()
    {
        exploringClip = AudioClip.Create("SceneExploring", 100, 1, 22050, false);
        combatClip = AudioClip.Create("SceneCombat", 100, 1, 22050, false);
        finalClip = AudioClip.Create("SceneFinal", 100, 1, 22050, false);
        defaultExploringClip = AudioClip.Create("DefaultExploring", 100, 1, 22050, false);
        defaultCombatClip = AudioClip.Create("DefaultCombat", 100, 1, 22050, false);
        host = new GameObject("Scene Music Test");
        defaults = ScriptableObject.CreateInstance<AudioSettingsDefinition>();
        defaults.DefaultExploring.tracks.Add(new MusicTrack(defaultExploringClip, 1f));
        defaults.DefaultCombat.tracks.Add(new MusicTrack(defaultCombatClip, 1f));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(defaults);
        foreach (AudioClip clip in new[] { exploringClip, combatClip, finalClip, defaultExploringClip, defaultCombatClip })
        {
            Object.DestroyImmediate(clip);
        }
    }

    private SceneAudio Scene(bool exploring, bool combat, bool final, bool combatMusic = true)
    {
        var scene = host.AddComponent<SceneAudio>();
        if (exploring) scene.Exploring.tracks.Add(new MusicTrack(exploringClip, 1f));
        if (combat) scene.Combat.tracks.Add(new MusicTrack(combatClip, 1f));
        if (final) scene.FinalStretch.tracks.Add(new MusicTrack(finalClip, 1f));
        var data = new SerializedObject(scene);
        data.Update();
        data.FindProperty("combatMusic").boolValue = combatMusic;
        data.ApplyModifiedPropertiesWithoutUndo();
        return scene;
    }

    private static AudioClip FirstClip(MusicChoice choice) => choice.Playlist?.tracks[choice.Playlist.NextIndex(-1)].clip;

    [Test]
    public void SceneMusic_WinsOverDefaults()
    {
        SceneAudio scene = Scene(true, true, false);
        Assert.AreSame(exploringClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Exploring, float.PositiveInfinity)));
        Assert.AreSame(combatClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Combat, float.PositiveInfinity)));
    }

    [Test]
    public void EmptyScenePlaylists_FallBackToDefaults()
    {
        SceneAudio scene = Scene(false, false, false);
        Assert.AreSame(defaultExploringClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Exploring, float.PositiveInfinity)));
        Assert.AreSame(defaultCombatClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Combat, float.PositiveInfinity)));
        Assert.AreSame(defaultExploringClip, FirstClip(MusicRules.Choose(null, defaults, AudioMood.Exploring, float.PositiveInfinity)));
    }

    [Test]
    public void FinalStretch_ReplacesMusicOnlyWhenTimeIsLow()
    {
        SceneAudio scene = Scene(true, true, true);
        Assert.AreSame(combatClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Combat, 120f)));
        Assert.AreSame(finalClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Combat, 30f)));
        Assert.AreSame(finalClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Exploring, 30f)));
    }

    [Test]
    public void CombatMusicOff_KeepsExploringMusic()
    {
        SceneAudio scene = Scene(true, true, false, combatMusic: false);
        Assert.AreSame(exploringClip, FirstClip(MusicRules.Choose(scene, defaults, AudioMood.Combat, float.PositiveInfinity)));
    }

    [Test]
    public void Playlist_InOrderWrapsAndSkipsEmptyRows()
    {
        var playlist = new MusicPlaylist { order = PlaylistOrder.InOrder };
        playlist.tracks.Add(new MusicTrack(exploringClip, 1f));
        playlist.tracks.Add(new MusicTrack(null, 1f));
        playlist.tracks.Add(new MusicTrack(combatClip, 1f));
        Assert.AreEqual(0, playlist.NextIndex(-1));
        Assert.AreEqual(2, playlist.NextIndex(0));
        Assert.AreEqual(0, playlist.NextIndex(2));
        Assert.AreEqual(2, playlist.PlayableCount);
    }

    [Test]
    public void Playlist_ShuffleNeverRepeatsTheSameTrack()
    {
        var playlist = new MusicPlaylist { order = PlaylistOrder.Shuffle };
        playlist.tracks.Add(new MusicTrack(exploringClip, 1f));
        playlist.tracks.Add(new MusicTrack(combatClip, 1f));
        for (int i = 0; i < 20; i++)
        {
            Assert.AreEqual(1, playlist.NextIndex(0));
        }
    }

    [TestCase("defaultExploring", "Heartwood Sanctuary")]
    [TestCase("defaultCombat", "Roots Against the Dark")]
    public void GameWideDefaults_PlayComposedTracks(string playlist, string trackTitle)
    {
        var settings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        Assert.IsNotNull(settings, "AudioSettings is missing from Resources.");
        MusicPlaylist list = playlist == "defaultExploring" ? settings.DefaultExploring : settings.DefaultCombat;
        Assert.IsTrue(list.HasTracks, $"{playlist} has no music.");
        AudioClip clip = list.tracks[list.NextIndex(-1)].clip;
        StringAssert.Contains(trackTitle, clip.name);
        Assert.AreEqual(AudioClipLoadType.Streaming, clip.loadType, "Long music should stream from disk.");
        Assert.Greater(clip.length, 60f);
    }
}
