using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/HavenWorldStory_2026-11-26.md steps 3-4: the places' scenes (HavenPlaceVerbs › Scenes, HavenPlaceScenes) and the first walk
/// out (Smoke in the Grey Pines): the errand leads to a place, the thing to use there shows only on the errand, the place changes once
/// it is done, and a work site's yield comes back every few days without ever being there twice in one span.
/// </summary>
public sealed class HavenPlaceScenesTests
{
    private static HavenPlaceVerbs Verbs => AssetDatabase.LoadAssetAtPath<HavenPlaceVerbs>("Assets/_Project/Resources/HavenPlaceVerbs.asset");
    private static WorldAtlas Atlas => AssetDatabase.LoadAssetAtPath<WorldAtlas>("Assets/_Project/Resources/Atlases/SafeHaven.asset");

    [Test]
    public void TheFirstWalkOut_RelightsTheClamp_OnTheWayHomeFromTheQuarry()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/SmokeInTheGreyPines.asset");
        Assert.IsNotNull(quest);
        Assert.That(quest.Prerequisites.Select(p => p.name), Does.Contain("CutStone"), "it follows the first walk out, to the Old Quarry");
        Assert.IsFalse(quest.HeldForHomecoming, "it carries on the walk, not held for another homecoming");

        QuestObjective reach = quest.Objectives.First(o => o.Type == ObjectiveType.ReachPlace);
        QuestObjective use = quest.Objectives.First(o => o.Type == ObjectiveType.UseObject);
        HavenPlaceVerbs verbs = Verbs;
        string placeId = verbs.Targets.First(t => t.target == reach.Target).placeId;
        WorldAtlas.Place place = Atlas.Places.First(p => p.id == placeId);
        Assert.AreEqual("The Grey Pines", Atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y)).name, "the clamp burns in the Grey Pines");

        HavenPlaceVerbs.Find clamp = verbs.Finds.Single(f => f.questTarget == use.Target);
        Assert.AreEqual(placeId, clamp.placeId, "what is lit stands where the errand leads");
        Assert.AreEqual(HavenPlaceVerbs.Gate.QuestStarted, clamp.gate, "only there on the errand: before it, a cold clamp nobody asked for");
        Assert.AreEqual(quest, clamp.quest);

        HavenPlaceVerbs.PlaceScene scene = verbs.SceneOf(placeId);
        Assert.IsTrue(scene.stages.Any(s => s.gate == HavenPlaceVerbs.Gate.QuestDone && s.quest == quest && s.smokeRise > 10f),
            "once lit it smokes high enough to be seen from the meadows");
        Assert.IsTrue(verbs.Finds.Any(f => f.placeId == placeId && f.gate == HavenPlaceVerbs.Gate.QuestDone && f.quest == quest && f.everyDays > 0),
            "a work site: its charcoal comes back");
        Assert.IsNotNull(verbs.Smoke, "the smoke has something to be drawn with");
    }

    [Test]
    public void AYield_IsOncePerSpan_AndComesBack()
    {
        var place = new WorldAtlas.Place { id = "test.place" };
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var yield = new HavenPlaceVerbs.Find { placeId = place.id, item = item, everyDays = 2 };
            Assert.AreEqual(PlaceFind.KeyOf(place, yield, 4), PlaceFind.KeyOf(place, yield, 5), "taken on day 4, not there again on day 5");
            Assert.AreNotEqual(PlaceFind.KeyOf(place, yield, 5), PlaceFind.KeyOf(place, yield, 6), "there again two days on");

            var once = new HavenPlaceVerbs.Find { placeId = place.id, item = item };
            Assert.AreEqual(PlaceFind.KeyOf(place, once, 0), PlaceFind.KeyOf(place, once, 40), "a find for good is never there again");
            Assert.AreEqual(PlaceFind.TakenKey(place.id, item.Id), PlaceFind.KeyOf(place, once, 3), "old saves' finds stay taken");
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void EveryScene_IsAtAPlace_AndEveryStageHasSomethingToShow()
    {
        HavenPlaceVerbs verbs = Verbs;
        var ids = Atlas.Places.Select(p => p.id).ToHashSet();
        foreach (HavenPlaceVerbs.PlaceScene scene in verbs.Scenes)
        {
            Assert.IsTrue(ids.Contains(scene.placeId), $"{scene.placeId}: no such place");
            foreach (HavenPlaceVerbs.Stage stage in scene.stages)
            {
                Assert.IsTrue(stage.dressing != null || stage.thoughts.Length > 0 || stage.smokeRise > 0f, $"{scene.placeId} {stage.note}: shows nothing");
                if (stage.gate == HavenPlaceVerbs.Gate.QuestStarted || stage.gate == HavenPlaceVerbs.Gate.QuestDone)
                {
                    Assert.IsNotNull(stage.quest, $"{scene.placeId} {stage.note}: waits on no quest, so it never shows");
                }
            }
        }

        foreach (HavenPlaceVerbs.Find find in verbs.Finds)
        {
            Assert.IsTrue(ids.Contains(find.placeId), $"{find.name}: no such place {find.placeId}");
            if (find.gate == HavenPlaceVerbs.Gate.QuestStarted || find.gate == HavenPlaceVerbs.Gate.QuestDone)
            {
                Assert.IsNotNull(find.quest, $"{find.name}: waits on no quest, so it never shows");
            }
        }
    }

    [Test]
    public void AGate_WithNoQuest_NeverOpens_AndAlwaysDoes()
    {
        Assert.IsTrue(HavenPlaceVerbs.Passes(HavenPlaceVerbs.Gate.Always, null, "The Grey Pines"));
        Assert.IsFalse(HavenPlaceVerbs.Passes(HavenPlaceVerbs.Gate.QuestStarted, null, "The Grey Pines"));
        Assert.IsFalse(HavenPlaceVerbs.Passes(HavenPlaceVerbs.Gate.QuestDone, null, "The Grey Pines"));
    }
}
