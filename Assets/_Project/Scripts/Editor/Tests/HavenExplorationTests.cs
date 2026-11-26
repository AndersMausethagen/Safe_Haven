using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Exploring the Haven (Docs/HavenExplorationReview_2026-11-19.md): every kind of named place in its atlas does something
/// (HavenPlaceVerbs), what is left at a place is a real item at a real place, a far find pays more than a near one, the land known adds
/// a little renown, and a place's sound keeps its hours.
/// </summary>
public sealed class HavenExplorationTests
{
    private static WorldAtlas Atlas => WorldAtlas.For("SafeHaven");

    [Test]
    public void EveryKindOfPlace_DoesSomething()
    {
        HavenPlaceVerbs verbs = HavenPlaceVerbs.Current;
        Assert.IsNotNull(verbs, "Resources/HavenPlaceVerbs");
        Assert.Greater(Atlas.Places.Count, 10, "the Haven's atlas has its places");
        var idle = new List<string>();
        foreach (WorldAtlas.Place place in Atlas.Places)
        {
            HavenPlaceVerbs.Kind kind = verbs.KindOf(place.kind);
            if (kind == null || (kind.thoughts.Length == 0 && !kind.lookout && kind.sound == null))
            {
                idle.Add($"{place.name} ({place.kind})");
            }
        }

        CollectionAssert.IsEmpty(idle, "a named place that gives nothing but its name");
    }

    [Test]
    public void EveryFind_IsARealItem_AtARealPlace()
    {
        HavenPlaceVerbs verbs = HavenPlaceVerbs.Current;
        Assert.Greater(verbs.Finds.Count, 0);
        var ids = new HashSet<string>();
        foreach (WorldAtlas.Place place in Atlas.Places)
        {
            ids.Add(place.id);
        }

        foreach (HavenPlaceVerbs.Find find in verbs.Finds)
        {
            Assert.IsTrue(ids.Contains(find.placeId), $"{find.name}: no place {find.placeId} in the atlas (renamed?)");
            if (find.item == null)
            {
                // A thing only to use (a lantern to light): something to see, and a quest it counts for.
                Assert.IsNotNull(find.marker, $"{find.name}: a thing only to use needs a marker to be seen");
                Assert.IsNotNull(find.questTarget, $"{find.name}: a thing only to use counts for a quest, or it is nothing");
                continue;
            }

            Assert.IsFalse(find.item.RaidOnly, $"{find.name}: {find.item.name} is raid-only, never a Haven find");
            Assert.Greater(find.amount, 0, find.name);
        }
    }

    [Test]
    public void AFarFind_PaysMoreThanANearOne()
    {
        TerritorySettings settings = TerritorySettings.Current;
        int meadows = settings.FindInsightAt(120f), far = settings.FindInsightAt(575f);
        Assert.Less(meadows, far);
        Assert.AreEqual(18, meadows, "the review's numbers: 18 in the meadows");
        Assert.AreEqual(48, far, "and 48 at the far edge");
    }

    [Test]
    public void TheLandKnown_AddsALittleRenown_ToACap()
    {
        TerritorySettings settings = TerritorySettings.Current;
        var journal = new JournalRecord();
        Assert.AreEqual(0, HavenRenown.KnownLand(journal, Atlas, settings));
        journal.NoteDiscovered(Atlas.Places[0].id);
        journal.NoteDiscovered(Atlas.Places[1].id);
        journal.NoteDiscovered("a.raid.place.not.of.the.haven");
        Assert.AreEqual(2 * settings.KnownLandRenown, HavenRenown.KnownLand(journal, Atlas, settings), "only the Haven's own places count");
        foreach (WorldAtlas.Place place in Atlas.Places)
        {
            journal.NoteDiscovered(place.id);
        }

        Assert.AreEqual(Mathf.Min(Atlas.Places.Count * settings.KnownLandRenown, settings.KnownLandMost), HavenRenown.KnownLand(journal, Atlas, settings));
    }

    [Test]
    public void APlacesSound_KeepsItsHours()
    {
        Assert.IsTrue(PlaceBeacon.PlaysAt(HavenPlaceVerbs.When.Night, 23f));
        Assert.IsFalse(PlaceBeacon.PlaysAt(HavenPlaceVerbs.When.Night, 13f));
        Assert.IsTrue(PlaceBeacon.PlaysAt(HavenPlaceVerbs.When.Day, 13f));
        Assert.IsFalse(PlaceBeacon.PlaysAt(HavenPlaceVerbs.When.Day, 3f));
        Assert.IsTrue(PlaceBeacon.PlaysAt(HavenPlaceVerbs.When.Always, 3f));
    }

    [Test]
    public void ALookout_NamesTheWayToWhatItSees()
    {
        Assert.AreEqual("north", HavenPlaceVerbs.Compass(Vector2.zero, new Vector2(0f, 50f)));
        Assert.AreEqual("south-west", HavenPlaceVerbs.Compass(Vector2.zero, new Vector2(-40f, -40f)));
        Assert.AreEqual("east", HavenPlaceVerbs.Compass(new Vector2(10f, 10f), new Vector2(90f, 12f)));
    }
}
