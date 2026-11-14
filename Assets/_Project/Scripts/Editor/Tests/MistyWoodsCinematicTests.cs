using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The first-raid cinematic "The Misty Woods" (Docs/MistyWoodsCinematic.md): the user's words whole and in order, no
/// mechanics on screen, the raid that plays it once per save, the fade into the game, and the fog and calm after it.
/// </summary>
public sealed class MistyWoodsCinematicTests
{
    private const string CinematicPath = "Assets/_Project/Data/Cinematics/MistyWoods.asset";
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";

    // The user's narration (2026-11-14), sentence by sentence.
    private static readonly string[] Narration =
    {
        "Beyond the reach of the Ward, lies a place where the light has long since faded.",
        "The Misty Woods swallow all who wander too deep.",
        "Its shadows hide things best left undisturbed.",
        "The longer you remain, the deeper its corruption grows.",
        "And the more it draws the attention of what lurks within.",
        "You are here for what the forest holds.",
        "Its resources, or its prisoners.",
        "Take what you need, save who you can.",
        "Avoid what you cannot fight.",
        "For this is no place to call home.",
        "Return to Haven… before the forest claims you.",
    };

    private static CinematicDefinition Cinematic => AssetDatabase.LoadAssetAtPath<CinematicDefinition>(CinematicPath);

    [Test]
    public void TheMistyWoods_SaysTheUsersWords_InOrder_EachWithAVoice()
    {
        CinematicDefinition cinematic = Cinematic;
        Assert.IsNotNull(cinematic, CinematicPath);
        Assert.AreEqual(6, cinematic.Shots.Count);
        var timeline = new CinematicTimeline(cinematic);
        CollectionAssert.AreEqual(Narration, timeline.Lines.Select(l => l.Line.subtitle).ToArray());
        foreach (CinematicTimeline.SpokenLine line in timeline.Lines)
        {
            Assert.IsNotNull(line.Line.voice, line.Line.subtitle);
            StringAssert.Contains("Voice/MistyWoods/", AssetDatabase.GetAssetPath(line.Line.voice));
        }

        Assert.That(timeline.Seconds, Is.InRange(30f, 75f), "a short welcome to the woods, not a film");
    }

    [Test]
    public void TheMistyWoods_ShowsNoCountdownOrMechanics()
    {
        string said = string.Join(" ", new CinematicTimeline(Cinematic).Lines.Select(l => l.Line.subtitle)).ToLowerInvariant();
        Assert.IsFalse(said.Any(char.IsDigit), said);
        foreach (string mechanic in new[] { "minute", "second", "extract", "timer", "clock", "loot", "press" })
        {
            StringAssert.DoesNotContain(mechanic, said);
        }
    }

    [Test]
    public void TheMistyWoods_NeverShowsTheEdgeOfAPainting()
    {
        CinematicDefinition cinematic = Cinematic;
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
    public void TheMistyWoods_FadesIntoTheGame()
    {
        Assert.Greater(Cinematic.FadeIntoSceneSeconds, 1f, "it ends on the player at the entrance, not on a loading screen");
    }

    [Test]
    public void TheMistyWoodsRaid_PlaysIt_OnTheFirstVisit_ThenCalmAndFog()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath);
        Assert.AreSame(Cinematic, raid.FirstVisitCinematic);
        Assert.That(raid.CalmAfterCinematic, Is.InRange(3f, 15f), "a few seconds before anything notices the player");
        Assert.IsNotNull(raid.ArrivalSound, "a faint unsettling sound as the picture comes up");
        Assert.IsNotNull(raid.ArrivalEffect);
        Assert.IsNotNull(raid.ArrivalEffect.GetComponent<ArrivalMist>(), "fog moving round the player");
        Assert.IsNotNull(raid.ArrivalEffect.GetComponent<MistVolume>());
    }

    [Test]
    public void AVisit_IsFirstOnce_AndSaves_AndOldSavesHaveNone()
    {
        var progress = new CharacterProgress();
        Assert.IsFalse(progress.HasVisitedRaid("wildlands"));
        Assert.IsTrue(progress.MarkRaidVisited("wildlands"));
        Assert.IsFalse(progress.MarkRaidVisited("wildlands"), "the second visit is not a first");
        Assert.IsTrue(progress.HasVisitedRaid("wildlands"));
        Assert.IsFalse(progress.MarkRaidVisited(""));

        var content = Resources.Load<ContentDatabase>("ContentDatabase");
        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(new ItemContainer(2, 1), null, null), null, "SafeHaven", null, DateTime.UtcNow, progress: progress);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);
        Assert.IsTrue(loaded.Progress.HasVisitedRaid("wildlands"));

        string json = JsonUtility.ToJson(data).Replace("\"raidsVisited\":[\"wildlands\"]", "\"older\":[]");
        LoadedGame old = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(json), content);
        Assert.IsFalse(old.Progress.HasVisitedRaid("wildlands"));
    }

    [Test]
    public void ArrivalMist_RollsIn_Holds_AndThinsToNothing()
    {
        Assert.AreEqual(0.6f, ArrivalMist.Thickness(0f, 1.5f, 0.6f, 6f, 14f), 0.001f);
        Assert.AreEqual(1f, ArrivalMist.Thickness(1.5f, 1.5f, 0.6f, 6f, 14f), 0.001f);
        Assert.AreEqual(1f, ArrivalMist.Thickness(7f, 1.5f, 0.6f, 6f, 14f), 0.001f);
        float half = ArrivalMist.Thickness(14.5f, 1.5f, 0.6f, 6f, 14f);
        Assert.That(half, Is.InRange(0.3f, 0.7f));
        Assert.AreEqual(0f, ArrivalMist.Thickness(21.5f, 1.5f, 0.6f, 6f, 14f), 0.001f);
    }

    [Test]
    public void OutsideARaid_ThereIsNoArrivalCalm()
    {
        Assert.IsNull(RaidDirector.Current);
        Assert.IsFalse(RaidDirector.IsArrivalCalm);
    }
}
