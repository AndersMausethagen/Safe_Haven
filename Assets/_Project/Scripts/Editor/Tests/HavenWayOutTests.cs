using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Getting out to the old places without minutes of walking (the user, 2026-11-27: "walking for several minutes is not fun", "now we have a
/// reason to get the horse, so lets get the quest to unlock it before the second walking quest"): the horse comes after the first walk out
/// and every region arc waits for it; the tracker points at every arc's place, thing or stone (HavenQuestWaypoints); the journal's
/// "Go there" goes only to a place already found; a lit stone knows the way home and the portal the way back out.
/// </summary>
public sealed class HavenWayOutTests
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private static QuestDefinition Quest(string name) => AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + name + ".asset");

    private static HashSet<QuestDefinition> Before(QuestDefinition quest)
    {
        var seen = new HashSet<QuestDefinition>();
        var open = new Stack<QuestDefinition>(quest.Prerequisites.Where(p => p != null));
        while (open.Count > 0)
        {
            QuestDefinition next = open.Pop();
            if (seen.Add(next))
            {
                foreach (QuestDefinition p in next.Prerequisites.Where(p => p != null)) open.Push(p);
            }
        }

        return seen;
    }

    private static IEnumerable<QuestDefinition> ResidentsArcs() => AssetDatabase.FindAssets("t:ResidentDefinition")
        .Select(g => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(g)))
        .SelectMany(r => r.SideQuests).Where(o => o != null && o.Quest != null).Select(o => o.Quest).Distinct();

    [Test]
    public void TheHorse_ComesAfterTheFirstWalk_AndEveryRegionArcWaitsForIt()
    {
        QuestDefinition horse = Quest("AHorseOfYourOwn");
        QuestDefinition firstWalk = Quest("SmokeInTheGreyPines");
        Assert.Contains(firstWalk, Before(horse).ToList(), "the horse after the clamp is lit");
        Assert.AreEqual(firstWalk, HorseSettings.Current.FirstHorseAfter, "and the calm first horse comes then, not only by its day");
        List<QuestDefinition> arcs = ResidentsArcs().ToList();
        Assert.GreaterOrEqual(arcs.Count, 20, "the residents' arcs");
        foreach (QuestDefinition arc in arcs)
        {
            Assert.IsTrue(Before(arc).Contains(horse), $"{arc.name}: a long walk out, so the horse first");
            Assert.IsFalse(Before(horse).Contains(arc), $"{arc.name} can't come before the horse it waits for");
        }

        QuestDefinition stable = Quest("AStableForYourHorse");
        Assert.IsNotNull(stable, "the Stable is a quest of its own after the taming, so it doesn't hold the arcs back");
        Assert.Contains(horse, stable.Prerequisites.ToList());
        Assert.IsTrue(stable.Objectives.Any(o => o.Type == ObjectiveType.BuildStructure && o.Building != null && o.Building.name == "Stable"));
        Assert.IsFalse(horse.Objectives.Any(o => o.Type == ObjectiveType.BuildStructure), "taming is lasso, barley and the ride");
    }

    [Test]
    public void TheFirstHorse_IsDue_OnceTheClampIsLit_OrByItsDay()
    {
        GameSession.StartNewGame();
        try
        {
            HorseSettings settings = HorseSettings.Current;
            QuestLog log = GameSession.Quests;
            Assert.IsFalse(settings.FirstHorseDue(1f, log), "not on the first day, before the walk");
            Assert.IsTrue(settings.FirstHorseDue(settings.FirstHorseByDay, log), "by its day whatever");
            log.ForceComplete(settings.FirstHorseAfter);
            Assert.IsTrue(settings.FirstHorseDue(1f, log), "and as soon as the clamp is lit");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void EveryArcsPlaceThingAndStone_IsFoundInTheLand_ForTheTrackersArrow()
    {
        WorldAtlas atlas = WorldAtlas.For("SafeHaven");
        Assert.IsNotNull(atlas);
        var holder = new GameObject("Ways");
        try
        {
            var ways = holder.AddComponent<HavenQuestWaypoints>();
            ways.Begin(atlas, HavenPlaceVerbs.Current);
            var none = new BoundaryStone[0];
            int found = 0;
            foreach (QuestDefinition arc in ResidentsArcs())
            {
                foreach (QuestObjective objective in arc.Objectives.Where(o => o.Target != null &&
                             (o.Type == ObjectiveType.ReachPlace || o.Type == ObjectiveType.UseObject)))
                {
                    if (ways.Locate(objective.Target, none, out Vector3 at, out string placeId))
                    {
                        Assert.IsTrue(atlas.Places.Any(p => p.id == placeId), $"{arc.name}: {objective.Target.name} is at a place of the map");
                        Assert.Greater(new Vector2(at.x, at.z).magnitude, 50f, $"{arc.name}: {objective.Target.name} out in the land");
                        found++;
                        continue;
                    }

                    // A stone isn't in an edit-mode test's scene: its region's stone stands where the atlas says.
                    string region = HavenRegionSettings.Current.RegionOfStone(objective.Target);
                    Assert.IsNotNull(region, $"{arc.name}: {objective.Target.name} is neither a place, a thing at one nor a stone: no arrow points there");
                    Assert.AreNotEqual(Vector2.zero, atlas.Regions.First(r => r.name == region).stoneAt, $"{region} has a stone");
                    found++;
                }
            }

            Assert.GreaterOrEqual(found, 20);
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void AQuestUnderWay_GetsItsArrow_AndTheJournalGoesThere_OnlyOnceThePlaceIsFound()
    {
        GameSession.StartNewGame();
        var holder = new GameObject("Ways");
        QuestWaypoint plain = null;
        try
        {
            QuestDefinition spring = Quest("TheHealersSpring");
            Assert.IsTrue(GameSession.Quests.TryStart(spring, true));
            var ways = holder.AddComponent<HavenQuestWaypoints>();
            ways.Begin(WorldAtlas.For("SafeHaven"), HavenPlaceVerbs.Current);
            ways.Refresh();
            QuestWaypoint walk = ways.Made.FirstOrDefault(w => w.Quest == spring && w.Objective == 1);
            Assert.IsNotNull(walk, "the walk out to Alder Spring has its arrow");
            Assert.IsTrue(walk.IsWanted(GameSession.Quests), "shown while it is the step at hand");
            Assert.IsFalse(walk.CanGoThere, "never found: the journal doesn't skip the first walk");
            GameSession.Progress.Journal.NoteDiscovered("haven.authored.alder_spring");
            Assert.IsTrue(walk.CanGoThere, "found once: the journal goes straight there");

            ways.Refresh();
            Assert.AreSame(walk, ways.Made.FirstOrDefault(w => w.Quest == spring && w.Objective == 1),
                "a refresh keeps the waypoint it made (a pickup refreshes; the journal's Go there holds its transform)");
            GameSession.Quests.ForceComplete(spring);
            ways.Refresh();
            Assert.IsFalse(ways.Made.Any(w => w != null && w.Quest == spring), "a quest done has no arrow left");

            plain = new GameObject("By hand").AddComponent<QuestWaypoint>();
            Assert.IsTrue(plain.CanGoThere, "a waypoint placed by hand goes as before");
            plain.GoThereOnceFound(null);
            Assert.IsFalse(plain.CanGoThere, "with no place to find first, only walked");
        }
        finally
        {
            if (plain != null)
            {
                Object.DestroyImmediate(plain.gameObject);
            }

            Object.DestroyImmediate(holder);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void ASaveFromBeforeTheStableWasSplitOff_KeepsTheTamingsProgress()
    {
        QuestDefinition horse = Quest("AHorseOfYourOwn");
        Assert.AreEqual(horse.Objectives.Count + 1, horse.FormerStepCount, "it had the Stable as a fourth step");
        var log = new QuestLog();
        log.Restore(horse, QuestStatus.Active, new[] { 1, 4, 1, 0 });
        CollectionAssert.AreEqual(new[] { 1, 4, 1 }, log.Get(horse).Counts, "lasso, barley and the taming kept; the Stable is its own quest");

        QuestDefinition other = Quest("SmokeInTheGreyPines");
        log.Restore(other, QuestStatus.Active, new[] { 1, 1, 1 });
        CollectionAssert.AreEqual(new[] { 0, 0 }, log.Get(other).Counts, "a quest whose steps changed otherwise starts them again");
    }
}
