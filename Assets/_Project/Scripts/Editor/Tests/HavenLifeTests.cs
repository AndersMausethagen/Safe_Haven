using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/HavenWorldStory_2026-11-26.md step 6, life: someone from home works each worked place, out there in their free time
/// (PlaceWorkers, a habit at HabitPlace.Worked); and the Haven's happenings (HavenHappenings): one at a time, drawn from the span itself
/// (the same each time it is asked, never the same twice running, some spans quiet), at a remembered place, told on the homecoming card.
/// </summary>
public sealed class HavenLifeTests
{
    private static HavenHappenings Happenings => AssetDatabase.LoadAssetAtPath<HavenHappenings>("Assets/_Project/Resources/HavenHappenings.asset");

    [Test]
    public void SomeoneFromHome_WorksAWorkedPlace_InTheirFreeTime_AndOnlyThen()
    {
        var who = ScriptableObject.CreateInstance<ResidentDefinition>();
        var owner = new GameObject("Spot");
        try
        {
            var record = new ResidentRecord(who);
            PlaceWorkers.Register(owner, who, new PlaceWorkers.Spot(new Vector3(10f, 0f, 20f), Vector3.zero, 18f, 21.5f));
            Assert.IsTrue(PeopleHabits.Now(record, 19f, ResidentActivity.Fire, out TraitHabit habit), "their free time, out at their place");
            Assert.AreEqual(HabitPlace.Worked, habit.place);
            Assert.IsFalse(PeopleHabits.Now(record, 10f, ResidentActivity.Fire, out _), "not outside its hours");
            Assert.IsFalse(PeopleHabits.Now(record, 19f, ResidentActivity.Work, out _), "work is work: only their free time goes there");

            PlaceWorkers.Unregister(owner);
            Assert.IsFalse(PlaceWorkers.TryGet(who, 19f, out _), "once the place no longer shows, nobody goes");
        }
        finally
        {
            PlaceWorkers.Unregister(owner);
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(who);
        }
    }

    [Test]
    public void EveryWorkedPlace_HasSomeoneToWorkIt()
    {
        HavenPlaceVerbs verbs = HavenPlaceVerbs.Current;
        // A worked place: a stage a quest's end stands, where a yield of that same quest is taken every few days.
        var worked = verbs.Scenes.SelectMany(s => s.stages).Where(st => st.gate == HavenPlaceVerbs.Gate.QuestDone && st.dressing != null && st.worker != null).ToList();
        Assert.GreaterOrEqual(worked.Count, 4, "the clamp, the quarry, Asa's field, the fishers' rest, the spring");
        foreach (HavenPlaceVerbs.Find yield in verbs.Finds.Where(f => f.everyDays > 0 && f.gate == HavenPlaceVerbs.Gate.QuestDone))
        {
            HavenPlaceVerbs.PlaceScene scene = verbs.SceneOf(yield.placeId);
            Assert.IsTrue(scene != null && scene.stages.Any(st => st.quest == yield.quest && st.worker != null)
                          || yield.quest.name == "EastwoodRemembered", $"{yield.name}: a yield at a place nobody works");
        }
        foreach (HavenPlaceVerbs.Stage stage in worked)
        {
            Assert.IsNotNull(stage.worker, $"{stage.note}: somebody from home works it");
            Assert.Less(stage.workHours.x, stage.workHours.y, $"{stage.note}: out in the evening and home to sleep, not overnight");
        }
    }

    [Test]
    public void AHappening_IsTheSameEachTimeItIsAsked_NeverTwiceRunning_AndSomeSpansAreQuiet()
    {
        HavenHappenings settings = Happenings;
        Assert.IsNotNull(settings);
        int quiet = 0;
        var seen = new HashSet<string>();
        string before = null;
        for (int span = 0; span < 200; span++)
        {
            HavenHappenings.Happening now = settings.Pick(span, null);
            Assert.AreSame(now, settings.Pick(span, null), "drawn from the span alone: the same on every look and every load");
            if (now == null)
            {
                quiet++;
                before = null;
                continue;
            }

            Assert.AreNotEqual(before, now.id, $"span {span}: the same happening twice running");
            seen.Add(now.id);
            before = now.id;
        }

        Assert.That(quiet, Is.InRange(30, 110), "about a third of the spans are quiet");
        Assert.AreEqual(settings.Happenings.Count, seen.Count, "every happening comes round");
        Assert.IsNull(settings.Pick(5, h => false), "nothing possible: a quiet span");
    }

    [Test]
    public void ASpansHappening_StaysWhatWasTold_WhateverChangesInIt()
    {
        HavenHappenings settings = Happenings;
        var journal = new CharacterProgress().Journal;
        int span = Enumerable.Range(0, 400).First(s => settings.Pick(s, null) != null && settings.Pick(s, h => h.id != settings.Pick(s, null).id) != null);
        HavenHappenings.Happening told = settings.Now(span, journal, null);
        Assert.IsNotNull(told);
        // Something remembered mid-span would draw another now: the span keeps the one it had.
        Assert.AreSame(told, settings.Now(span, journal, h => h != told), "what the card told stands all through its span");
        HavenHappenings.Happening next = settings.Now(span + 1, journal, null);
        Assert.AreNotSame(told, next, "never the same twice running, as it was had");
        Assert.IsNull(settings.Pick(5, h => false), "nothing possible: a quiet span");
    }

    [Test]
    public void AYield_BuildsUp_ForAFewSpans_AndIsTakenTogether()
    {
        var place = new WorldAtlas.Place { id = "test.place" };
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var used = new HashSet<string>();
            var yield = new HavenPlaceVerbs.Find { placeId = place.id, name = "Yield", item = item, everyDays = 2, keepsSpans = 3 };
            Assert.AreEqual(3, PlaceFind.Waiting(place, yield, 10, used.Contains), "days 6-11 untaken: three spans' worth wait (no more)");
            for (int span = 3; span <= 5; span++)
            {
                used.Add(PlaceFind.KeyOf(place, yield, span * 2));
            }

            Assert.AreEqual(0, PlaceFind.Waiting(place, yield, 11, used.Contains), "all taken this span");
            Assert.AreEqual(1, PlaceFind.Waiting(place, yield, 12, used.Contains), "one span on, one span's worth");

            var lantern = new HavenPlaceVerbs.Find { placeId = place.id, name = "Lantern" };
            Assert.AreEqual(1, PlaceFind.Waiting(place, lantern, 0, used.Contains));
            used.Add(PlaceFind.KeyOf(place, lantern, 0));
            Assert.AreEqual(0, PlaceFind.Waiting(place, lantern, 50, used.Contains), "a thing used stays used");
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void AStoneAnArcAsksFor_LightsWithoutARune_AndOneAlreadyLitCounts()
    {
        GameSession.StartNewGame();
        try
        {
            var end = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/RavenwoodRemembered.asset");
            Assert.IsFalse(Territory.AnArcAsks("Ravenwood"), "before the errand, the stone wants its rune as ever");
            GameSession.Quests.TryStart(end, true);
            Assert.IsTrue(Territory.AnArcAsks("Ravenwood"), "the errand asks for it");
            Assert.IsFalse(Territory.AnArcAsks("Stillwater"), "and for no other region's");

            QuestObjective light = end.Objectives.Last();
            Assert.AreEqual(0, light.Owned(null, GameSession.Quests), "not lit yet");
            GameSession.Here.Territory.Claim("Ravenwood", TerritorySettings.Current);
            Assert.AreEqual(1, light.Owned(null, GameSession.Quests), "lit before the quest (or in an older save) counts as lit");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void EveryHappening_IsAtAPlace_IsToldOnTheCard_AndHasSomethingToSeeOrTake()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>("Assets/_Project/Resources/Atlases/SafeHaven.asset");
        foreach (HavenHappenings.Happening happening in Happenings.Happenings)
        {
            Assert.IsNotNull(HavenHappenings.Find(atlas, happening.placeId), $"{happening.id}: no place {happening.placeId}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(happening.foretold), $"{happening.id}: told on the homecoming card");
            Assert.IsTrue(happening.item != null || happening.dressing != null, $"{happening.id}: something to see or take");
            if (happening.item != null)
            {
                Assert.IsFalse(happening.item.RaidOnly, $"{happening.id}: a Haven find is never raid-only");
                Assert.Greater(happening.amount, 0, happening.id);
            }
        }
    }
}
