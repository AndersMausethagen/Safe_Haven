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
        var worked = verbs.Scenes.SelectMany(s => s.stages).Where(st => st.gate == HavenPlaceVerbs.Gate.QuestDone && st.dressing != null && st.smokeRise >= 0f
            && verbs.Finds.Any(f => f.quest == st.quest && f.everyDays > 0)).ToList();
        Assert.GreaterOrEqual(worked.Count, 3, "the quarry, Asa's field, the fishers' rest");
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
