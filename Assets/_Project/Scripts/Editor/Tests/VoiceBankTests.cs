using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Spoken dialogue: how a line finds its recording, and that the recordings the game ships are in place.</summary>
public class VoiceBankTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private AudioClip Clip(string name)
    {
        AudioClip clip = AudioClip.Create(name, 2400, 1, 24000, false);
        made.Add(clip);
        return clip;
    }

    private VoiceBank Bank(params VoiceLine[] lines)
    {
        var bank = ScriptableObject.CreateInstance<VoiceBank>();
        made.Add(bank);
        bank.Author(lines.ToList());
        return bank;
    }

    [Test]
    public void ALine_IsFoundWhateverItsMarkupCaseOrSpacing()
    {
        AudioClip clip = Clip("line");
        VoiceBank bank = Bank(new VoiceLine { speaker = "The Portal", text = "<i>Listen, little one.  Rest your hand on the stone.</i>", clip = clip });

        Assert.AreSame(clip, bank.Find("The Portal", "Listen, little one. Rest your hand on the stone."));
        Assert.AreSame(clip, bank.Find("the portal ", "<i>LISTEN, little one.\nRest your hand on the stone.</i>"));
        Assert.IsNull(bank.Find("The Portal", "Listen, little one."), "Other words are another line.");
        Assert.IsNull(bank.Find("Sigrid", "Listen, little one. Rest your hand on the stone."), "The same words from someone else are another line.");
        Assert.IsNull(bank.Find("The Portal", ""));
        Assert.IsNull(bank.Find("The Portal", null));
    }

    [Test]
    public void ALineWithAKeyHint_IsStillFoundAfterThePlayerRebindsTheKey()
    {
        AudioClip clip = Clip("line");
        VoiceBank bank = Bank(new VoiceLine { speaker = "The Portal", text = "Open the build menu (G) and light it.", clip = clip });

        // GameInput.WithRebinds marks a changed key with \u0001.
        Assert.AreSame(clip, bank.Find("The Portal", "Open the build menu (\u0001B) and light it."));
        Assert.AreSame(clip, bank.Find("The Portal", "Open the build menu [Left Shift] and light it."));
        Assert.AreEqual(VoiceBank.Id("The Portal", "Open the build menu (G) and light it."), VoiceBank.Id("The Portal", "Open the build menu (\u0001B) and light it."));
    }

    [Test]
    public void ALinesFileName_StaysTheSameAsLongAsItsWordsDo()
    {
        string id = VoiceBank.Id("Sigrid", "Keep walking.");

        Assert.AreEqual(12, id.Length);
        Assert.IsTrue(id.All(c => "0123456789abcdef".IndexOf(c) >= 0), id);
        Assert.AreEqual(id, VoiceBank.Id(" sigrid", "<b>Keep  walking.</b>"));
        Assert.AreNotEqual(id, VoiceBank.Id("Eirik", "Keep walking."));
        Assert.AreNotEqual(id, VoiceBank.Id("Sigrid", "Keep walking!"));
    }

    [Test]
    public void ARecordingPlacedByHand_WinsOverAGeneratedOne_AndSurvivesARebuild()
    {
        AudioClip generated = Clip("generated");
        AudioClip actor = Clip("actor");
        VoiceBank bank = Bank(new VoiceLine { speaker = "Sigrid", text = "Keep walking.", clip = generated });
        var serialized = new SerializedObject(bank);
        SerializedProperty placed = serialized.FindProperty("handPlaced");
        placed.arraySize = 1;
        placed.GetArrayElementAtIndex(0).FindPropertyRelative("speaker").stringValue = "Sigrid";
        placed.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue = "Keep walking.";
        placed.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = actor;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreSame(actor, bank.Find("Sigrid", "Keep walking."));

        bank.Author(new List<VoiceLine> { new VoiceLine { speaker = "Sigrid", text = "Keep walking.", clip = generated } });
        Assert.AreSame(actor, bank.Find("Sigrid", "Keep walking."), "Rebuilding the generated list leaves hand-placed recordings alone.");
        Assert.AreEqual(1, bank.HandPlaced.Count);
    }

    [Test]
    public void ALineWithoutARecording_IsNotInTheWay()
    {
        VoiceBank bank = Bank(new VoiceLine { speaker = "Sigrid", text = "Keep walking.", clip = null });

        Assert.IsNull(bank.Find("Sigrid", "Keep walking."));
        Assert.IsFalse(bank.SpeaksFor("Sigrid"), "nothing of hers is recorded");
    }

    [Test]
    public void TheBankKnowsWhomItSpeaksFor()
    {
        VoiceBank bank = Bank(new VoiceLine { speaker = "Sigrid", text = "Keep walking.", clip = Clip("line") });

        Assert.IsTrue(bank.SpeaksFor(" sigrid "));
        Assert.IsFalse(bank.SpeaksFor("You"), "the player's own lines are read, not spoken");
        Assert.IsFalse(bank.SpeaksFor(null));
        bank.Author(new List<VoiceLine>());
        Assert.IsFalse(bank.SpeaksFor("Sigrid"), "a rebuilt list is asked afresh");
    }

    // ----- The game's own bank -----

    private static VoiceBank Shipped() => Resources.Load<VoiceBank>(VoiceBank.ResourceName);

    [Test]
    public void TheGamesBank_HasARecordingForEveryLineItLists_AndNoLineTwice()
    {
        VoiceBank bank = Shipped();
        Assert.IsNotNull(bank, "Resources/VoiceBank.asset: written by AgentScripts/BuildDialogueVoices.cs (Apply).");
        Assert.Greater(bank.Lines.Count, 300, "Nearly all of the game's dialogue is spoken.");
        var seen = new HashSet<string>();
        foreach (VoiceLine line in bank.Lines)
        {
            Assert.IsNotNull(line.clip, $"{line.speaker}: {line.text}");
            Assert.IsTrue(seen.Add(VoiceBank.Key(line.speaker, line.text)), $"Twice in the bank: {line.speaker}: {line.text}");
            Assert.AreSame(line.clip, bank.Find(line.speaker, line.text));
        }
    }

    [Test]
    public void TheGamesBank_KeepsEachRecordingInItsSpeakersFolder_UnderTheLinesOwnName()
    {
        foreach (VoiceLine line in Shipped().Lines)
        {
            string path = AssetDatabase.GetAssetPath(line.clip);
            StringAssert.EndsWith($"/{VoiceBank.Id(line.speaker, line.text)}.wav", path, $"{line.speaker}: {line.text}");
            StringAssert.StartsWith("Assets/_Project/Audio/Voice/Dialogue/", path);
        }
    }

    [Test]
    public void TheGamesBank_SpeaksForThePortalAndTheExpedition()
    {
        var speakers = new HashSet<string>(Shipped().Lines.Select(line => line.speaker));

        foreach (string name in new[] { HomePortal.SpeakerName, "Sigrid", "Eirik", "Runa", "Ingrid", "Bjarne", "Tor", "Liv" })
        {
            Assert.IsTrue(speakers.Contains(name), $"{name} has no spoken lines.");
        }
    }

    [Test]
    public void TheGamesRecordings_AreShortSpeechKeptCompressed()
    {
        foreach (VoiceLine line in Shipped().Lines)
        {
            Assert.AreEqual(1, line.clip.channels, line.clip.name);
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, line.clip.loadType, line.clip.name);
            Assert.That(line.clip.length, Is.InRange(0.3f, 60f), $"{line.speaker}: {line.text}");
        }
    }

    [Test]
    public void TheSubtitleOfASpokenLine_OutlastsItsRecordingALittle()
    {
        Assert.That(Shipped().SubtitleTail, Is.InRange(0.2f, 2f));
    }
}
