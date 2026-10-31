using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The sound system's rules: what gives way, what repeats, and how music comes back after a fight.</summary>
public sealed class AudioSystemTests
{
    private static AudioClip Clip(string name) => AudioClip.Create(name, 441, 1, 44100, false);

    private static SoundDefinition Sound(params AudioClip[] clips)
    {
        var sound = ScriptableObject.CreateInstance<SoundDefinition>();
        var so = new SerializedObject(sound);
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return sound;
    }

    [Test]
    public void ASound_NeverPlaysTheSameClipTwiceRunning()
    {
        AudioClip a = Clip("a");
        AudioClip b = Clip("b");
        AudioClip c = Clip("c");
        SoundDefinition sound = Sound(a, b, c);
        AudioClip last = null;
        for (int i = 0; i < 200; i++)
        {
            AudioClip next = sound.PickClip(last);
            Assert.IsNotNull(next);
            Assert.AreNotEqual(last, next);
            last = next;
        }
    }

    [Test]
    public void ASoundWithOneClip_StillPlays()
    {
        AudioClip only = Clip("only");
        Assert.AreEqual(only, Sound(only).PickClip(only));
        Assert.AreEqual(only, Sound(only).PickClip());
        Assert.IsNull(Sound().PickClip());
    }

    [Test]
    public void BirdsNeverCutOffAParry_AndAWarningCutsOffBirds()
    {
        Assert.IsFalse(AudioManager.CanCutOff(SoundPriority.Important, SoundPriority.Background));
        Assert.IsFalse(AudioManager.CanCutOff(SoundPriority.Important, SoundPriority.Normal));
        Assert.IsFalse(AudioManager.CanCutOff(SoundPriority.Background, SoundPriority.Background), "mood never takes a voice");
        Assert.IsTrue(AudioManager.CanCutOff(SoundPriority.Background, SoundPriority.Normal));
        Assert.IsTrue(AudioManager.CanCutOff(SoundPriority.Normal, SoundPriority.Important));
        Assert.IsTrue(AudioManager.CanCutOff(SoundPriority.Important, SoundPriority.Important));
    }

    [Test]
    public void ACrossfade_KeepsItsPower()
    {
        Assert.AreEqual(0f, AudioManager.EqualPower(0f), 0.001f);
        Assert.AreEqual(1f, AudioManager.EqualPower(1f), 0.001f);
        for (float fade = 0f; fade <= 1f; fade += 0.1f)
        {
            float a = AudioManager.EqualPower(fade);
            float b = AudioManager.EqualPower(1f - fade);
            Assert.AreEqual(1f, a * a + b * b, 0.001f, "no dip in the middle of the change");
        }
    }

    [Test]
    public void MusicComesBackWhereItWasLeft_AfterAShortFight()
    {
        var memory = new MusicMemory();
        var exploring = new MusicPlaylist();
        memory.Leave(exploring, 1, 42f, 100f);
        Assert.IsTrue(memory.TryReturn(exploring, 130f, 120f, out int index, out float time));
        Assert.AreEqual(1, index);
        Assert.AreEqual(42f, time, 0.001f);
        Assert.IsFalse(memory.TryReturn(exploring, 131f, 120f, out _, out _), "a place is taken once");
    }

    [Test]
    public void MusicStartsAfresh_AfterALongWhile()
    {
        var memory = new MusicMemory();
        var exploring = new MusicPlaylist();
        memory.Leave(exploring, 0, 42f, 100f);
        Assert.IsFalse(memory.TryReturn(exploring, 500f, 120f, out _, out _));
        Assert.IsFalse(memory.TryReturn(null, 500f, 120f, out _, out _));
    }

    [Test]
    public void LongClips_AreNotHeldDecompressedInMemory()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null && clip.length >= 8f && AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                Assert.AreNotEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType, path);
            }
        }
    }

    [Test]
    public void EveryMoment_HasASound()
    {
        var settings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        var moments = new[] { settings.Consumed, settings.ItemDropped, settings.Repaired, settings.BreachWarning, settings.BreachBegins, settings.BreachWon, settings.BreachLost };
        Assert.IsTrue(moments.All(sound => sound != null && sound.HasClips));
        Assert.IsTrue(new[] { settings.BreachWarning, settings.BreachBegins, settings.BreachWon, settings.BreachLost }.All(sound => !sound.Positional),
            "a warning has to reach a player anywhere in the clearing");
    }

    [Test]
    public void WhatThePlayerMustHear_OutranksTheMood()
    {
        var settings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        Assert.AreEqual(SoundPriority.Important, settings.PlayerHurt.Priority);
        Assert.AreEqual(SoundPriority.Important, settings.Parry.Priority);
        Assert.AreEqual(SoundPriority.Background, settings.Footstep.Priority);
        var chirp = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Bird Chirp.asset");
        Assert.AreEqual(SoundPriority.Background, chirp.Priority);
    }

    [Test]
    public void TheWorldSoundsLevel_LeavesRoomForCloseSounds()
    {
        var settings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        Assert.Greater(settings.WorldSoundsLevel, 0.3f);
        Assert.Less(settings.WorldSoundsLevel, 1f);
    }
}
