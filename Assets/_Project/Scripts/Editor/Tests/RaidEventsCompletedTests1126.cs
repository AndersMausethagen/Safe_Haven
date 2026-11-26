using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What Docs/RaidEventsPlan_2026-11-26.md still lacked (the user, 2026-11-26: "gjør det som mangler"): Track and Escort, crossing to a way
/// out, choices the past opens, marks that weigh a pick, visits at home, the legends' page in the journal, the fall last raid kept in the
/// save, and the new events and follow-ups.
/// </summary>
public sealed class RaidEventsCompletedTests1126
{
    private static readonly string[] Generated = { "MistyWoods", "TrollValley", "MossMarsh" };
    private const string Events = "Assets/_Project/Data/Raids/Events/";

    private static RaidEventSettings Settings(string id) => RaidEventSettings.Of(AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset"));
    private static RaidEventDefinition Event(string name) => AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(Events + name + ".asset");

    private static IEnumerable<RaidEventOutcome> Deep(IEnumerable<RaidEventOutcome> outcomes)
    {
        foreach (RaidEventOutcome outcome in outcomes.Where(o => o != null))
        {
            yield return outcome;
            var nested = outcome.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(List<RaidEventOutcome>))
                .SelectMany(f => (List<RaidEventOutcome>)f.GetValue(outcome) ?? new List<RaidEventOutcome>());
            foreach (RaidEventOutcome inner in Deep(nested))
            {
                yield return inner;
            }
        }
    }

    [Test]
    public void ATrailIsSpacedAlongThePath_AndEndsOnTheHiddenPlace()
    {
        var corners = new List<Vector3> { Vector3.zero, new Vector3(30f, 0f, 0f), new Vector3(30f, 0f, 25f) };
        List<Vector3> points = TrackTask.Spaced(corners, 10f);
        Assert.AreEqual(corners[2], points[points.Count - 1], "the last sign is the place itself");
        Assert.AreEqual(new Vector3(10f, 0f, 0f), points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            Assert.LessOrEqual(Vector3.Distance(points[i - 1], points[i]), 10.01f, "no gap wider than the spacing");
        }

        Assert.AreEqual(6, points.Count, "55 m of path at 10 m: five signs on the way and the place");
    }

    [Test]
    public void ATrailShowsItsSignsOneByOne_AndPaysOnlyAtTheEnd()
    {
        var progress = new CharacterProgress();
        GameSession.UseProgressForTests(progress);
        var holder = new GameObject("Trail");
        try
        {
            TrackTask task = holder.AddComponent<TrackTask>();
            var found = new List<RaidEventOutcome> { new SetMarkOutcome("test:found") };
            task.LayForTests(new[] { new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(30f, 0f, 0f) }, found);
            task.Step(new Vector3(-20f, 0f, 0f));
            Assert.AreEqual(0, task.Next, "far off: nothing moves on");
            task.Step(new Vector3(8f, 0f, 0f));
            Assert.AreEqual(1, task.Next);
            task.Step(new Vector3(25.5f, 0f, 0f));
            Assert.AreEqual(2, task.Next, "past the second sign: the place is next");
            Assert.IsFalse(task.IsDone, "near the end isn't at it");
            Assert.IsFalse(progress.Journal.HasNoticed("test:found"));
            task.Step(new Vector3(29f, 0f, 0f));
            Assert.IsTrue(task.IsDone);
            Assert.IsTrue(progress.Journal.HasNoticed("test:found"), "found: its outcomes carried out");

            // One who walks straight to the place is credited, signs or no.
            TrackTask straight = new GameObject("Straight").AddComponent<TrackTask>();
            try
            {
                straight.LayForTests(new[] { new Vector3(10f, 0f, 0f), new Vector3(20f, 0f, 0f), new Vector3(30f, 0f, 0f) }, found);
                straight.Step(new Vector3(29.5f, 0f, 0f));
                Assert.IsTrue(straight.IsDone);
            }
            finally
            {
                Object.DestroyImmediate(straight.gameObject);
            }
        }
        finally
        {
            Object.DestroyImmediate(holder);
            GameSession.UseProgressForTests(null);
        }
    }

    [Test]
    public void TheWayOutIsReachedShortOfIt_OnThePlayersSide()
    {
        Vector3 landing = CrossOutcome.Landing(new Vector3(500f, 0f, 500f), new Vector3(400f, 0f, 500f), 12f);
        Assert.AreEqual(488f, landing.x, 0.6f);
        Assert.AreEqual(500f, landing.z, 0.6f);
    }

    [Test]
    public void APastMarkWeighsAPick_WithoutBeingNeeded()
    {
        RaidEventDefinition lindorm = Event("LindormSleeps");
        float plain = lindorm.WeightIn(null, mark => false);
        float heard = lindorm.WeightIn(null, mark => mark == "event:lindorm:heard");
        Assert.Greater(plain, 0f, "a legend still comes without it");
        Assert.Greater(heard, plain * 2f, "the bell told where it sleeps");
    }

    [Test]
    public void AChoiceThePastOpens_IsHiddenOnlyWhileItsNeedsAreUnmet()
    {
        foreach (RaidEventDefinition definition in Generated.SelectMany(id => Settings(id).pool).Where(e => e != null).Distinct())
        {
            foreach (RaidEventChoice choice in definition.Choices.Where(c => c.hiddenUntilMet))
            {
                Assert.IsNotEmpty(choice.requires, $"{definition.name} › {choice.label}: hidden until something, or it would always show");
            }
        }

        Assert.IsTrue(Event("TollTroll").Choices.Any(c => c.hiddenUntilMet), "the toll troll remembers a tusk");
        Assert.IsTrue(Event("OldBarrow").Choices.Any(c => c.hiddenUntilMet), "a sealed barrow's dead stay quiet");
    }

    [Test]
    public void SomeoneMetOutThere_ComesByTheGateOnce()
    {
        var settings = ScriptableObject.CreateInstance<HomecomingSettings>();
        var stash = new StashStorage();
        try
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<HomecomingSettings>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:HomecomingSettings").First()))
                .Visits.First().item;
            var so = new SerializedObject(settings);
            SerializedProperty visits = so.FindProperty("visits");
            visits.arraySize = 1;
            visits.GetArrayElementAtIndex(0).FindPropertyRelative("needsMark").stringValue = "a";
            visits.GetArrayElementAtIndex(0).FindPropertyRelative("doneMark").stringValue = "a:done";
            visits.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = item;
            visits.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            var marks = new HashSet<string>();
            Assert.IsNull(AwayGifts.Visit(settings, stash, marks.Contains, m => marks.Add(m)), "not before the choice that brings them");
            marks.Add("a");
            Assert.IsNotNull(AwayGifts.Visit(settings, stash, marks.Contains, m => marks.Add(m)));
            Assert.AreEqual(2, stash.Items.Count(item), "what they left is in the chests");
            Assert.IsNull(AwayGifts.Visit(settings, stash, marks.Contains, m => marks.Add(m)), "once");
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void AFallLastRaid_IsReadFromTheJournalTheSaveKeeps()
    {
        var journal = new JournalRecord();
        Assert.IsFalse(RaidReportText.FellLastRaid(journal));
        journal.AddRaid(new RaidRecord { Outcome = RaidReportText.DiedTitle });
        Assert.IsTrue(RaidReportText.FellLastRaid(journal));
        journal.AddRaid(new RaidRecord { Outcome = "Extracted!" });
        Assert.IsFalse(RaidReportText.FellLastRaid(journal), "only the last raid counts");
    }

    [Test]
    public void TheLegendsMet_AreInTheJournalsStrangeThings()
    {
        JournalSettings settings = AssetDatabase.LoadAssetAtPath<JournalSettings>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:JournalSettings").First()));
        var legends = Generated.SelectMany(id => Settings(id).pool).Where(e => e != null && e.OncePerSave).Distinct().ToList();
        CollectionAssert.IsSubsetOf(legends, settings.Legends.ToList(), "every raid's legend has its page");
        var journal = new JournalRecord();
        HavenShelf none = JournalContent.StrangeThings(settings, journal, out int met, out int all);
        Assert.AreEqual(0, met);
        Assert.IsEmpty(none.Entries);
        journal.NoteNoticed(legends[0].OnceMark);
        HavenShelf one = JournalContent.StrangeThings(settings, journal, out met, out all);
        Assert.AreEqual(1, met);
        Assert.AreEqual(legends[0].DisplayName, one.Entries[0].Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(one.Entries[0].Description), "its page");
    }

    [Test]
    public void TheNewTasksHaveWhatTheyNeed()
    {
        var all = Generated.SelectMany(id => Settings(id).pool).Where(e => e != null).Distinct().ToList();
        List<RaidEventOutcome> outcomes = all.SelectMany(e => e.Choices.SelectMany(c => Deep(c.outcomes))).ToList();
        Assert.IsTrue(outcomes.OfType<TrackOutcome>().Any(), "a trail to follow");
        Assert.IsTrue(outcomes.OfType<EscortOutcome>().Any(), "someone to bring out");
        Assert.IsTrue(outcomes.OfType<CrossOutcome>().Any(), "a way across");
        foreach (EscortOutcome escort in outcomes.OfType<EscortOutcome>())
        {
            var follower = (EscortFollower)typeof(EscortOutcome).GetField("follower", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(escort);
            Assert.IsNotNull(follower, "an escort's follower");
            Assert.IsNotNull(follower.GetComponent<Damageable>(), "it can be hurt");
            Assert.IsNotNull(follower.GetComponent<UnityEngine.AI.NavMeshAgent>());
            Assert.IsNull(follower.GetComponent<RescuableTraveler>(), "not a resident to rescue as well");
        }

        foreach (TrackOutcome track in outcomes.OfType<TrackOutcome>())
        {
            var leader = (GameObject)typeof(TrackOutcome).GetField("leader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(track);
            var sign = (GameObject)typeof(TrackOutcome).GetField("sign", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(track);
            Assert.IsTrue(leader != null || sign != null, "something to follow");
            if (leader != null)
            {
                Assert.IsNotNull(leader.GetComponent<TrackLeader>());
                Assert.IsNull(leader.GetComponent<Wildlife>(), "no wildlife wandering off with it");
            }
        }

        foreach (RaidEventDefinition definition in all)
        {
            foreach (RaidEventChoice choice in definition.Choices.Where(c => Deep(c.outcomes).OfType<CrossOutcome>().Any()))
            {
                Assert.IsTrue(choice.requires.OfType<NotMounted>().Any(), $"{definition.name} › {choice.label}: greyed on horseback, not paid for and lost");
            }
        }

        Assert.IsTrue(all.Any(e => e.NearWater), "a shore's event");
        Assert.IsTrue(Event("WhiteHart").Choices.SelectMany(c => c.outcomes).OfType<TrackOutcome>().Any(), "the hart is followed, not a menu");
        Assert.IsTrue(Event("Oskorei").Choices.Any(c => c.requires.OfType<Mounted>().Any()), "ride with the Oskorei on a horse");
        Assert.IsTrue(Event("ThickeningFog").Choices.Any(c => c.requires.OfType<CompanionAlong>().Any()), "Liv reads the fog's lights");
        Assert.IsTrue(Deep(Event("CursedTree").Choices.SelectMany(c => c.outcomes)).OfType<EaseBlightOutcome>().Any(), "the rot drawn eases the blight at home");
    }

    [Test]
    public void ARiderIsMounted_OnlyOnAHorse()
    {
        var walker = new GameObject("Walker");
        try
        {
            Assert.IsFalse(new Mounted().IsMet(walker, null));
            Assert.IsFalse(new Mounted().IsMet(null, null));
        }
        finally
        {
            Object.DestroyImmediate(walker);
        }
    }
}
