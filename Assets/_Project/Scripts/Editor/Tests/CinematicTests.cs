using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The cinematic's timing and camera arithmetic, and that the backstory asset is whole and wired to New Game.</summary>
public sealed class CinematicTests
{
    private const string BackstoryPath = "Assets/_Project/Data/Cinematics/Backstory.asset";
    private const string MenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";

    private static CinematicDefinition Backstory => AssetDatabase.LoadAssetAtPath<CinematicDefinition>(BackstoryPath);

    private static CinematicLine Line(string text, float pause, float seconds) =>
        new CinematicLine { subtitle = text, pauseBefore = pause, secondsWithoutVoice = seconds };

    private static CinematicShot Shot(CinematicTransition arrives, float arriveSeconds, float hold, params CinematicLine[] lines) =>
        new CinematicShot { arrives = arrives, arriveSeconds = arriveSeconds, holdAfter = hold, lines = lines.ToList() };

    private static CinematicDefinition Make(params CinematicShot[] shots)
    {
        var definition = ScriptableObject.CreateInstance<CinematicDefinition>();
        definition.Author("Test", shots.ToList());
        return definition;
    }

    private static CinematicTimeline TwoShots(CinematicTransition second, out CinematicDefinition definition)
    {
        // 0.5 + 2 + 0.5 + 3 + 1 = 7 seconds, then 1 + 2 + 2 = 5.
        definition = Make(
            Shot(CinematicTransition.ThroughBlack, 2f, 1f, Line("One.", 0.5f, 2f), Line("Two.", 0.5f, 3f)),
            Shot(second, 2f, 2f, Line("Three.", 1f, 2f)));
        return new CinematicTimeline(definition);
    }

    [Test]
    public void AShot_LastsAsLongAsItsLinesPausesAndHold()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.Cut, out CinematicDefinition definition);
        Assert.AreEqual(7f, definition.Shots[0].Seconds, 0.001f);
        Assert.AreEqual(0f, timeline.ShotStart(0), 0.001f);
        Assert.AreEqual(7f, timeline.ShotStart(1), 0.001f);
        Assert.AreEqual(12f, timeline.Seconds, 0.001f);
        Assert.AreEqual(0, timeline.ShotAt(6.99f));
        Assert.AreEqual(1, timeline.ShotAt(7f));
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void Lines_AreSpokenAfterTheirPauses_AndTheirSubtitlesLinger()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.Cut, out CinematicDefinition definition);
        Assert.AreEqual(3, timeline.Lines.Count);
        Assert.AreEqual(0.5f, timeline.Lines[0].Start, 0.001f);
        Assert.AreEqual(3f, timeline.Lines[1].Start, 0.001f);
        Assert.AreEqual(8f, timeline.Lines[2].Start, 0.001f);
        Assert.AreEqual(-1, timeline.SpeakingAt(0.2f));
        Assert.AreEqual(0, timeline.SpeakingAt(1f));
        Assert.AreEqual(-1, timeline.SpeakingAt(2.7f));
        Assert.AreEqual(1, timeline.SpeakingAt(3.1f));
        Assert.AreEqual("", timeline.SubtitleAt(0.2f));
        Assert.AreEqual("One.", timeline.SubtitleAt(1f));
        Assert.AreEqual("One.", timeline.SubtitleAt(2.8f), "still up just after the line");
        Assert.AreEqual("Two.", timeline.SubtitleAt(3.1f));
        Assert.AreEqual("", timeline.SubtitleAt(6f + definition.SubtitleLinger + 0.1f));
        Assert.AreEqual("Two.", timeline.SubtitleAt(6f + definition.SubtitleLinger + 0.1f, 1.5f), "a slow reader keeps it longer");
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void ADissolve_BringsTheNextShotInOverTheOneBefore()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.Dissolve, out CinematicDefinition definition);
        Assert.AreEqual(1f, timeline.Visibility(0, 6.9f), 0.001f);
        Assert.AreEqual(0f, timeline.Visibility(1, 6.9f), 0.001f);
        Assert.AreEqual(0.5f, timeline.Visibility(1, 8f), 0.001f);
        Assert.AreEqual(1f, timeline.Visibility(0, 8f), 0.001f, "the old shot stays underneath");
        Assert.Greater(timeline.Progress(0, 8f), 1f, "and keeps moving");
        Assert.AreEqual(0f, timeline.Visibility(0, 9.01f), 0.001f);
        Assert.AreEqual(1f, timeline.Visibility(1, 9.01f), 0.001f);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void ACut_ChangesAtOnce()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.Cut, out CinematicDefinition definition);
        Assert.AreEqual(1f, timeline.Visibility(0, 6.99f), 0.001f);
        Assert.AreEqual(0f, timeline.Visibility(0, 7f), 0.001f);
        Assert.AreEqual(1f, timeline.Visibility(1, 7f), 0.001f);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void TheCurtain_OpensFromBlack_CrossesWhite_AndClosesAtTheEnd()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.ThroughWhite, out CinematicDefinition definition);
        Assert.AreEqual(1f, timeline.Curtain(0f).a, 0.001f);
        Assert.AreEqual(0f, timeline.Curtain(0f).r, 0.001f, "black");
        Assert.AreEqual(0.5f, timeline.Curtain(0.5f).a, 0.001f);
        Assert.AreEqual(0f, timeline.Curtain(3f).a, 0.001f);
        Assert.AreEqual(1f, timeline.Curtain(7f).a, 0.001f);
        Assert.AreEqual(1f, timeline.Curtain(7f).r, 0.001f, "white");
        Assert.AreEqual(0.5f, timeline.Curtain(6.5f).a, 0.001f, "the shot before fades into it");
        Assert.AreEqual(1f, timeline.Curtain(12f).a, 0.001f);
        Assert.AreEqual(0f, timeline.Curtain(12f).r, 0.001f, "black again at the end");
        Assert.AreEqual(0f, timeline.Curtain(12f - definition.EndFadeSeconds - 0.01f).a, 0.02f);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void TheCamera_MovesNearLayersMoreThanFarOnes()
    {
        var pan = new CinematicPose(0.1f, 0f, 0f);
        CinematicCamera.Place(0f, pan, out float subjectScale, out Vector2 subject);
        CinematicCamera.Place(4f, pan, out float farScale, out Vector2 far);
        CinematicCamera.Place(-0.5f, pan, out float nearScale, out Vector2 near);
        Assert.AreEqual(-0.1f, subject.x, 0.0001f, "the subject slides against the camera by as much as it moved");
        Assert.AreEqual(-0.02f, far.x, 0.0001f);
        Assert.AreEqual(-0.2f, near.x, 0.0001f);
        Assert.AreEqual(1f, subjectScale, 0.0001f);
        Assert.AreEqual(1f, farScale, 0.0001f);
        Assert.AreEqual(1f, nearScale, 0.0001f);

        var push = new CinematicPose(0f, 0f, 0.2f);
        CinematicCamera.Place(0f, push, out subjectScale, out _);
        CinematicCamera.Place(4f, push, out farScale, out _);
        CinematicCamera.Place(-0.5f, push, out nearScale, out _);
        Assert.AreEqual(1.25f, subjectScale, 0.0001f);
        Assert.Less(farScale, subjectScale);
        Assert.Greater(nearScale, subjectScale);
    }

    [Test]
    public void ALayerSizedToCover_HidesTheFrameAllThroughTheMove()
    {
        var from = new CinematicPose(-0.04f, 0.01f, 0f);
        var to = new CinematicPose(0.05f, -0.02f, 0.1f);
        foreach (float depth in new[] { -0.45f, 0f, 1f, 4f })
        {
            var layer = new CinematicLayer { depth = depth, size = CinematicCamera.SizeToCover(depth, Vector2.zero, from, to) };
            for (int i = 0; i <= 10; i++)
            {
                Assert.IsTrue(CinematicCamera.Covers(layer, CinematicPose.Lerp(from, to, i / 10f)), $"depth {depth} at {i / 10f}");
            }

            layer.size *= 0.97f;
            Assert.IsFalse(CinematicCamera.Covers(layer, from) && CinematicCamera.Covers(layer, to), $"depth {depth}: no larger than it has to be");
        }
    }

    [Test]
    public void Music_FadesInAtItsShot_AndOutAtItsLast()
    {
        CinematicTimeline timeline = TwoShots(CinematicTransition.Cut, out CinematicDefinition definition);
        var cue = new CinematicMusic { fromShot = 0, delay = 1f, toShot = 0, volume = 0.5f, fadeIn = 2f, fadeOut = 2f };
        Assert.AreEqual(1f, timeline.MusicStart(cue), 0.001f);
        Assert.AreEqual(7f, timeline.MusicEnd(cue), 0.001f);
        Assert.AreEqual(0f, timeline.MusicLevel(cue, 0.5f), 0.001f);
        Assert.AreEqual(0.25f, timeline.MusicLevel(cue, 2f), 0.001f);
        Assert.AreEqual(0.5f, timeline.MusicLevel(cue, 4f), 0.001f);
        Assert.AreEqual(0.25f, timeline.MusicLevel(cue, 6f), 0.001f);
        Assert.AreEqual(0f, timeline.MusicLevel(cue, 7.5f), 0.001f);
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void Specks_StayInTheirArea_AndAreTheSameEveryTime()
    {
        var motes = new CinematicMotes { count = 50, velocity = new Vector2(0.01f, 0.07f), wander = 0.03f };
        for (int i = 0; i < motes.count; i++)
        {
            Vector2 at = CinematicMotesGraphic.Place(motes, i, 12.3f, out float visibility, out float size);
            Assert.That(at.x, Is.InRange(0f, 1f));
            Assert.That(at.y, Is.InRange(0f, 1f));
            Assert.That(visibility, Is.InRange(0f, 1f));
            Assert.That(size, Is.InRange(motes.size.x, motes.size.y));
            Assert.AreEqual(at, CinematicMotesGraphic.Place(motes, i, 12.3f, out _, out _));
        }

        Assert.AreNotEqual(CinematicMotesGraphic.Place(motes, 0, 1f, out _, out _), CinematicMotesGraphic.Place(motes, 1, 1f, out _, out _));
    }

    [Test]
    public void Skipping_NeedsTheKeyHeldAllTheWay()
    {
        var skip = new HoldToSkip();
        Assert.IsFalse(skip.Step(true, 0.6f, 1f));
        Assert.AreEqual(0.6f, skip.Progress, 0.001f);
        Assert.IsFalse(skip.Step(false, 0.1f, 1f), "letting go starts over");
        Assert.AreEqual(0f, skip.Progress, 0.001f);
        Assert.IsFalse(skip.Step(true, 0.6f, 1f));
        Assert.IsTrue(skip.Step(true, 0.5f, 1f));
    }

    [Test]
    public void Flicker_NeverBrightensALayer_AndLeavesASteadyOneAlone()
    {
        var steady = new CinematicLayer();
        var fire = new CinematicLayer { flicker = 0.4f, flickerSpeed = 9f };
        for (int i = 0; i < 50; i++)
        {
            Assert.AreEqual(1f, CinematicScreen.Flicker(steady, i * 0.13f));
            Assert.That(CinematicScreen.Flicker(fire, i * 0.13f), Is.InRange(0.6f, 1f));
        }
    }

    // ----- The backstory itself -----

    [Test]
    public void TheBackstory_HasAVoiceAndASubtitleForEveryLine()
    {
        CinematicDefinition backstory = Backstory;
        Assert.IsNotNull(backstory, BackstoryPath);
        var timeline = new CinematicTimeline(backstory);
        Assert.AreEqual(26, timeline.Lines.Count, "the user's script, one sentence a line (Docs/BackstoryCinematic.md)");
        foreach (CinematicTimeline.SpokenLine line in timeline.Lines)
        {
            Assert.IsNotNull(line.Line.voice, line.Line.subtitle);
            Assert.IsNotEmpty(line.Line.subtitle);
            Assert.That(line.Line.subtitle.Length, Is.LessThanOrEqualTo(95), "one sentence a subtitle: " + line.Line.subtitle);
        }

        Assert.That(timeline.Seconds, Is.InRange(95f, 140f), "about two minutes: the script's 1:35, and room for the violin between lines");
        Assert.AreEqual("A fire is a small thing, little one.", timeline.Lines[0].Line.subtitle);
    }

    [Test]
    public void TheBackstory_NeverShowsTheEdgeOfAPainting()
    {
        CinematicDefinition backstory = Backstory;
        var timeline = new CinematicTimeline(backstory);
        for (int i = 0; i < backstory.Shots.Count; i++)
        {
            CinematicShot shot = backstory.Shots[i];
            foreach (CinematicLayer layer in shot.layers)
            {
                if (layer.kind != CinematicLayerKind.Picture || layer.additive || layer.size < 1f)
                {
                    continue; // glows and specks are smaller than the frame on purpose
                }

                Assert.IsNotNull(layer.picture, $"{shot.name} / {layer.name}");
                // From its first frame to the end of the dissolve that covers it.
                for (float time = timeline.ShotStart(i); time <= timeline.StaysUntil(i); time += 0.25f)
                {
                    Assert.IsTrue(CinematicCamera.Covers(layer, timeline.Camera(i, time)), $"{shot.name} / {layer.name} at {time - timeline.ShotStart(i):0.0} s");
                }
            }
        }
    }

    // Where a phrase of Nightsong's violin enters, in seconds of the track (measured: Docs/BackstoryCinematic.md, As built).
    private static readonly float[] NightsongViolin = { 23.15f, 31.14f, 38.77f, 45.42f, 54.43f, 61.61f, 68.23f, 76.28f, 85.14f };

    // The violin comes in between the lines, never under one (the user, 2026-11-16: "I really like that violin sound that comes
    // in between the dialogue. try and time it with that"). A line spoken again at another length must keep it so.
    [Test]
    public void TheBackstory_LeavesRoomForNightsongsViolin()
    {
        CinematicDefinition backstory = Backstory;
        var timeline = new CinematicTimeline(backstory);
        int heard = 0;
        foreach (CinematicMusic cue in backstory.Music.Where(cue => cue.clip != null && cue.clip.name.Contains("Nightsong")))
        {
            foreach (float entry in NightsongViolin.Where(entry => entry >= cue.startAt))
            {
                float at = timeline.MusicStart(cue) + entry - cue.startAt;
                if (at > timeline.MusicEnd(cue))
                {
                    continue;
                }

                heard++;
                foreach (CinematicTimeline.SpokenLine line in timeline.Lines)
                {
                    Assert.IsFalse(line.Start < at + 1.5f && line.End > at - 0.2f, $"\"{line.Line.subtitle}\" is said as the violin comes in at {at:0.0} s");
                }
            }
        }

        Assert.GreaterOrEqual(heard, 6, "the violin comes in under the warm shots and the morning");
    }

    [Test]
    public void TheBackstory_KeepsItsFirstActSecrets()
    {
        // Who let the fire go out, what the guardians became and the troll are the first act's to tell.
        string said = string.Join(" ", new CinematicTimeline(Backstory).Lines.Select(l => l.Line.subtitle)).ToLowerInvariant();
        foreach (string word in new[] { "heartroot", "troll", "broke", "broken", "let the fire", "let it go", "your kind" })
        {
            StringAssert.DoesNotContain(word, said);
        }
    }

    [Test]
    public void TheBackstory_HasMusicThroughout_ExceptWhereTheFireGoesOut()
    {
        CinematicDefinition backstory = Backstory;
        var timeline = new CinematicTimeline(backstory);
        Assert.IsNotEmpty(backstory.Music);
        foreach (CinematicMusic cue in backstory.Music)
        {
            Assert.IsNotNull(cue.clip);
            Assert.Less(timeline.MusicStart(cue), timeline.MusicEnd(cue));
        }

        int coldHearth = Enumerable.Range(0, backstory.Shots.Count).First(i => backstory.Shots[i].name.Contains("Fire Dies"));
        float middle = (timeline.ShotStart(coldHearth) + timeline.ShotEnd(coldHearth)) * 0.5f;
        Assert.AreEqual(0f, backstory.Music.Sum(cue => timeline.MusicLevel(cue, middle)), 0.001f, "silence is part of the score");
    }

    [Test]
    public void ANewGame_OpensWithTheBackstory()
    {
        string guid = AssetDatabase.AssetPathToGUID(BackstoryPath);
        Assert.IsNotEmpty(guid);
        StringAssert.Contains(guid, File.ReadAllText(MenuScenePath), "MainMenuView's Opening Cinematic should be the backstory.");
    }

    // ----- The prologue's end: "The Catch" -----

    private const string CatchPath = "Assets/_Project/Data/Cinematics/TheCatch.asset";
    private const string ProloguePath = "Assets/_Project/Data/Prologue/ForestPrologue.asset";

    private static CinematicDefinition Catch => AssetDatabase.LoadAssetAtPath<CinematicDefinition>(CatchPath);

    [Test]
    public void TheCatch_HasAVoiceAndASubtitleForEveryLine()
    {
        CinematicDefinition cinematic = Catch;
        Assert.IsNotNull(cinematic, CatchPath + ": written by AgentScripts/BuildPrologueEndCinematic.cs");
        Assert.AreEqual(9, cinematic.Shots.Count);
        var timeline = new CinematicTimeline(cinematic);
        Assert.AreEqual(21, timeline.Lines.Count);
        foreach (CinematicTimeline.SpokenLine line in timeline.Lines)
        {
            Assert.IsNotNull(line.Line.voice, line.Line.subtitle);
            Assert.IsNotEmpty(line.Line.subtitle);
            Assert.That(line.Line.subtitle.Length, Is.LessThanOrEqualTo(95), "one sentence a subtitle: " + line.Line.subtitle);
        }

        Assert.That(timeline.Seconds, Is.InRange(70f, 135f), "a bridge between two scenes, not a film");
    }

    [Test]
    public void TheCatch_NeverShowsTheEdgeOfAPainting()
    {
        CinematicDefinition cinematic = Catch;
        var timeline = new CinematicTimeline(cinematic);
        for (int i = 0; i < cinematic.Shots.Count; i++)
        {
            CinematicShot shot = cinematic.Shots[i];
            foreach (CinematicLayer layer in shot.layers)
            {
                if (layer.kind != CinematicLayerKind.Picture || layer.additive || layer.size < 1f)
                {
                    continue;
                }

                Assert.IsNotNull(layer.picture, $"{shot.name} / {layer.name}");
                for (float time = timeline.ShotStart(i); time <= timeline.StaysUntil(i); time += 0.25f)
                {
                    Assert.IsTrue(CinematicCamera.Covers(layer, timeline.Camera(i, time)), $"{shot.name} / {layer.name} at {time - timeline.ShotStart(i):0.0} s");
                }
            }
        }
    }

    [Test]
    public void TheCatch_SaysWhatHappened_WhoCaughtThePlayer_AndWhatToDo()
    {
        string said = string.Join(" ", new CinematicTimeline(Catch).Lines.Select(l => l.Line.subtitle)).ToLowerInvariant();
        foreach (string told in new[] { "hand on the stone", "the stones broke", "i caught one", "forgets them", "a hearth", "light one", "bring them back" })
        {
            StringAssert.Contains(told, said);
        }

        // Why the old troll came, who let the hearth go cold and what the guardian was are the first act's to tell.
        foreach (string secret in new[] { "heartroot", "for the door", "guardian", "pact", "your kind", "went cold" })
        {
            StringAssert.DoesNotContain(secret, said);
        }
    }

    [Test]
    public void ThePrologue_EndsWithTheCatch_BeforeHomeLoads()
    {
        var prologue = AssetDatabase.LoadAssetAtPath<PrologueDefinition>(ProloguePath);
        Assert.IsNotNull(prologue, ProloguePath);
        Assert.AreSame(Catch, prologue.EndingCinematic);
        Assert.IsTrue(prologue.LoadHomeDuringCinematic);
        // The director plays it after the gear is set to come home and before the scene is asked for.
        string director = File.ReadAllText("Assets/_Project/Scripts/Prologue/PrologueDirector.cs");
        int carried = director.IndexOf("GameSession.CarryPlayerState", System.StringComparison.Ordinal);
        int played = director.IndexOf("CinematicScreen.Play(definition.EndingCinematic", System.StringComparison.Ordinal);
        int loaded = director.IndexOf("SceneLoader.Load(definition.HomeScene", System.StringComparison.Ordinal);
        Assert.That(carried, Is.GreaterThan(0));
        Assert.That(played, Is.GreaterThan(carried));
        Assert.That(loaded, Is.GreaterThan(played));
    }

    [Test]
    public void TheVoice_HasItsOwnVolume()
    {
        var sounds = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>("Assets/_Project/Resources/AudioSettings.asset");
        Assert.Greater(sounds.DefaultVolume(AudioChannel.Voice), 0.5f);
        Assert.AreEqual(3, (int)AudioChannel.Voice, "channels are saved as numbers: new ones go at the end");
    }
}
