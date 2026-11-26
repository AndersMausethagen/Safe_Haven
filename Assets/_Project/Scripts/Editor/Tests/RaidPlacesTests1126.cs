using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The raid places round of 2026-11-26 (Docs/RaidLayoutAnalysis_2026-11-26.md): themed resource sites shared by weight and distance, packs
/// at the camps with a guarded stash, events in the event rooms, walkers on the trails, every pocket holding something, the Valley meeting
/// its quotas, and the "came upon" log row.
/// </summary>
public sealed class RaidPlacesTests1126
{
    private static readonly string[] Generated = { "MistyWoods", "TrollValley", "MossMarsh" };

    private static RaidDefinition Raid(string id) => AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{id}.asset");

    // --- Sharing sites among groups (R1) ---

    [Test]
    public void EqualSharesWithNoneFarthest_GiveThePlainTurnAndTurnAbout()
    {
        foreach ((int sites, int groups) in new[] { (5, 2), (14, 4), (3, 4), (9, 3) })
        {
            var distances = Enumerable.Range(0, sites).Select(i => (float)((i * 37) % 11)).ToList();
            int[] weighted = RaidMapMarkers.ShareOut(distances, Enumerable.Repeat(1f, groups).ToList(), Enumerable.Repeat(false, groups).ToList());
            CollectionAssert.AreEqual(RaidMapMarkers.ShareOut(sites, groups), weighted, $"{sites} sites, {groups} groups");
        }
    }

    [Test]
    public void SharesFollowTheWeights_AndAFarthestGroupTakesTheFarSites()
    {
        var distances = new List<float> { 10f, 90f, 30f, 120f, 50f, 70f };
        int[] owners = RaidMapMarkers.ShareOut(distances, new List<float> { 2f, 1f }, new List<bool> { false, true });
        Assert.AreEqual(4, owners.Count(o => o == 0), "two thirds of six");
        Assert.AreEqual(2, owners.Count(o => o == 1), "one third of six");
        Assert.AreEqual(1, owners[3], "the farthest site (120 m)");
        Assert.AreEqual(1, owners[1], "the next farthest (90 m)");
        Assert.AreEqual(0, owners[0], "the nearest stays with the others");
    }

    [Test]
    public void EveryResourceGroupHasItsOwnList_AndOreTakesTheFarSites()
    {
        foreach (string id in Generated)
        {
            List<RaidSpawnList> resourceLists = Raid(id).SpawnLists.Where(list => list.category == SpawnCategory.Resources).ToList();
            Assert.AreEqual(4, resourceLists.Count, $"{id}: one list per kind of resource site");
            foreach (RaidSpawnList list in resourceLists)
            {
                Assert.AreEqual(1, list.pointGroups.Count, $"{id} › {list.name}: one group, so each site is one kind of place");
            }

            var ore = resourceLists.First(list => list.pointGroups[0] == "Ore Veins");
            Assert.IsTrue(ore.entries.Where(e => e.prefab != null).All(e => e.prefab.GetComponentInChildren<ResourceNode>(true) != null),
                $"{id}: ore veins hold only what is mined");
            Assert.IsFalse(resourceLists.Where(list => list != ore).SelectMany(list => list.entries)
                    .Any(e => e.prefab != null && e.prefab.name.Contains("Ore") && e.prefab.name.EndsWith("OreNode")),
                $"{id}: ore stands only in the ore veins");
        }

        foreach (string id in Generated)
        {
            Assert.AreEqual("1", GroupField(id, "Ore Veins", "farthestSites"), $"{id}: the Ore Veins group takes the far sites");
            Assert.AreEqual("0", GroupField(id, "Timber", "farthestSites"), $"{id}: timber stands anywhere");
            Assert.Less(float.Parse(GroupField(id, "Ore Veins", "siteShare"), System.Globalization.CultureInfo.InvariantCulture), 1f,
                $"{id}: ore is the rarer kind of site");
        }
    }

    /// <summary>A field of the Spawn Group on the raid scene's object of this name, read from the scene file (no scene is opened).</summary>
    private static string GroupField(string id, string groupName, string field)
    {
        string text = System.IO.File.ReadAllText($"Assets/_Project/Scenes/Raids/Raid_{id}.unity");
        string[] blocks = System.Text.RegularExpressions.Regex.Split(text, @"^--- !u!", System.Text.RegularExpressions.RegexOptions.Multiline);
        string objectId = blocks.Where(b => b.StartsWith("1 &") && System.Text.RegularExpressions.Regex.IsMatch(b, $@"^\s+m_Name: {System.Text.RegularExpressions.Regex.Escape(groupName)}\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
            .Select(b => b.Substring(3, b.IndexOfAny(new[] { ' ', '\r', '\n' }, 3) - 3)).FirstOrDefault();
        Assert.IsNotNull(objectId, $"{id}: no object named {groupName}");
        string group = blocks.FirstOrDefault(b => b.StartsWith("114 &") && b.Contains($"m_GameObject: {{fileID: {objectId}}}") && b.Contains("siteShare:"));
        Assert.IsNotNull(group, $"{id}: {groupName} has no Spawn Group");
        return System.Text.RegularExpressions.Regex.Match(group, $@"^\s+{field}: (.*)$", System.Text.RegularExpressions.RegexOptions.Multiline).Groups[1].Value.Trim();
    }

    // --- Every pocket holds something (R2) ---

    [Test]
    public void EveryCachePocketHoldsSomething_MostlySmallFinds()
    {
        foreach (string id in Generated)
        {
            RaidDefinition raid = Raid(id);
            int pockets = raid.LayoutRules.Sites.Where(q => q.kind == RaidSiteKind.SupplyCache).Sum(q => q.count);
            RaidSpawnList caches = raid.SpawnLists.First(list => list.pointGroups.Contains("Supply Caches"));
            Assert.GreaterOrEqual(caches.MinCount, pockets, $"{id}: every cache pocket is filled");
            float small = caches.entries.Where(e => e.prefab != null && e.prefab.TryGetComponent(out LootCache c) && c.SmallFind).Sum(e => e.weight);
            Assert.Greater(small, 0f, $"{id}: small finds fill the pockets the real caches don't");
            Assert.IsNotEmpty(raid.LayoutRules.SiteClutter.Where(c => c != null).ToList(), $"{id}: camps and caches have something about them");
        }
    }

    [Test]
    public void ASmallFindHasNoLock()
    {
        foreach (string name in new[] { "Dropped Sack", "Old Offering", "Wanderer's Remains" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Raids/Caches/{name}.prefab");
            Assert.IsNotNull(prefab, name);
            Assert.IsTrue(prefab.GetComponent<LootCache>().SmallFind, name);
            Assert.AreEqual(0, prefab.GetComponent<LootCache>().LockPins, name + ": nothing to pick");
            Assert.IsNotNull(prefab.GetComponent<LootCache>().Table, name);
        }
    }

    // --- Camps (R4) ---

    [Test]
    public void APackStandsOnItsSitesOtherPoints_NeverAnotherSites()
    {
        var points = new List<Vector3> { new(0, 0, 0), new(4, 0, 0), new(-4, 0, 0), new(30, 0, 0), new(34, 0, 0) };
        List<int> two = RaidSpawnList.PackPoints(points, 0, 2);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, two);
        List<int> five = RaidSpawnList.PackPoints(points, 0, 5);
        Assert.IsTrue(five.All(i => i <= 2), "round the same site again, never the camp 30 m off");
    }

    [Test]
    public void DensityGrowsTheCampsPacks()
    {
        var list = new RaidSpawnList { packSize = new Vector2Int(1, 2), entries = Raid("MistyWoods").SpawnLists.First(l => l.name == "Creatures").entries };
        Assert.AreEqual(new Vector2Int(1, 2), list.PackFor(new SpawnRules(1f, null, 60f, 110f)));
        Assert.AreEqual(new Vector2Int(2, 3), list.PackFor(new SpawnRules(1.5f, null, 60f, 110f)), "a raid's density (and night's) puts more at each camp");
    }

    [Test]
    public void EveryCampHasAGuardedStash()
    {
        foreach (string id in Generated)
        {
            RaidSpawnList stashes = Raid(id).SpawnLists.FirstOrDefault(list => list.name == "Camp Stashes");
            Assert.IsNotNull(stashes, id);
            Assert.IsTrue(stashes.entries.All(e => e.prefab != null && e.prefab.GetComponent<GuardedCache>() != null), $"{id}: a stash opens only once its guards are dead");
            Assert.AreEqual("1", GroupField(id, "Camp Stashes", "everySite"), $"{id}: the stash group takes every camp");
            Assert.AreEqual("3", GroupField(id, "Camp Stashes", "site"), $"{id}: ... and its kind is the camps (EnemyCamp)");
            Assert.AreEqual("0", GroupField(id, "Wolf Packs", "everySite"), $"{id}: the creatures share the camps");
        }
    }

    // Far from anything an open scene holds, so only the test's own creatures are near.
    private static readonly Vector3 Away = new Vector3(5000f, 0f, 5000f);

    private static GameObject Creature(string name, Vector3 at)
    {
        var made = new GameObject(name);
        made.AddComponent<MonsterAI>();
        made.transform.position = Away + at;
        return made;
    }

    [Test]
    public void AStashCantBeSearchedWhileItsGuardLives()
    {
        var stash = new GameObject("Stash", typeof(Interactable), typeof(GuardedCache));
        stash.transform.position = Away;
        GameObject guard = Creature("Guard", new Vector3(5f, 0f, 0f));
        GameObject far = Creature("Far", new Vector3(40f, 0f, 0f));
        try
        {
            GuardedCache cache = stash.GetComponent<GuardedCache>();
            Assert.IsFalse(cache.CanInteract(null, out string reason), "its guard stands 5 m off");
            Assert.AreEqual(GuardedCache.GuardedText(1), reason, "only the one in reach guards it");
            // A creature's death destroys its body, which edit mode refuses with an error; the death itself is what counts here.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));
            guard.GetComponent<Damageable>().Finish((Combatant)null);
            Assert.IsTrue(cache.CanInteract(null, out _), "the guard is dead");
        }
        finally
        {
            Object.DestroyImmediate(stash);
            Object.DestroyImmediate(guard);
            Object.DestroyImmediate(far);
        }
    }

    [Test]
    public void ACampWithNobodyAtTheStart_KeepsItsStashOpen_WhoeverPassesLater()
    {
        var stash = new GameObject("Stash", typeof(Interactable), typeof(GuardedCache));
        stash.transform.position = Away;
        GameObject walker = Creature("Walker", new Vector3(3f, 0f, 0f));
        walker.AddComponent<MonsterPatrol>();
        GameObject passer = null;
        try
        {
            GuardedCache cache = stash.GetComponent<GuardedCache>();
            Assert.IsTrue(cache.CanInteract(null, out _), "a walker on its round guards nothing");
            passer = Creature("Passer", new Vector3(4f, 0f, 0f));
            Assert.IsTrue(cache.CanInteract(null, out _), "a creature that comes by later isn't one of the camp");
        }
        finally
        {
            Object.DestroyImmediate(stash);
            Object.DestroyImmediate(walker);
            if (passer != null) Object.DestroyImmediate(passer);
        }
    }

    [Test]
    public void ACappedCreatureStandsAloneInAPackList()
    {
        // Packs add copies past the list's picks, so a cap (max per raid) only holds for an entry that stands alone.
        foreach (string id in Generated)
        {
            foreach (RaidSpawnList list in Raid(id).SpawnLists.Where(list => list.PlacesPacks))
            {
                foreach (SpawnTable.Entry entry in list.entries.Where(e => e.prefab != null && e.maxPerGroup > 0))
                {
                    Assert.IsTrue(entry.alone, $"{id} › {list.name} › {entry.prefab.name}: capped at {entry.maxPerGroup}, so alone");
                }
            }
        }
    }

    // --- Events and walkers (R3, R10) ---

    [Test]
    public void AnEventTakesAFreeEventRoomAtItsDistance_NearestFirst()
    {
        var places = new List<RaidPlace>
        {
            new(RaidSiteKind.EventSite, null, new Vector3(50f, 0f, 0f)),
            new(RaidSiteKind.EnemyCamp, null, new Vector3(60f, 0f, 0f)),
            new(RaidSiteKind.EventSite, null, new Vector3(0f, 0f, 70f)),
            new(RaidSiteKind.EventSite, null, new Vector3(10f, 0f, 0f)),
            new(RaidSiteKind.EventSite, null, new Vector3(0f, 9f, 200f)),
        };
        CollectionAssert.AreEqual(new[] { 0, 2 }, RaidSpots.RoomsWithin(places, Vector3.zero, 35f, 110f, new HashSet<int>()));
        CollectionAssert.AreEqual(new[] { 2 }, RaidSpots.RoomsWithin(places, Vector3.zero, 35f, 110f, new HashSet<int> { 0 }), "a room is given once");
    }

    [Test]
    public void AWalkersRoundGoesThroughTheNearestCampsAndPatrolSpots()
    {
        var places = new List<RaidPlace>
        {
            new(RaidSiteKind.EnemyCamp, null, new Vector3(5f, 0f, 0f)),      // too near to be a stop
            new(RaidSiteKind.EnemyCamp, null, new Vector3(30f, 0f, 0f)),
            new(RaidSiteKind.Patrol, null, new Vector3(60f, 0f, 0f)),
            new(RaidSiteKind.SupplyCache, null, new Vector3(20f, 0f, 0f)),   // not a stop
            new(RaidSiteKind.EnemyCamp, null, new Vector3(-25f, 0f, 0f)),
            new(RaidSiteKind.Patrol, null, new Vector3(200f, 0f, 0f)),
        };
        List<Vector3> round = RaidGenerator.RoundFrom(Vector3.zero, places);
        Assert.AreEqual(4, round.Count, "where it stands and three stops");
        Assert.AreEqual(Vector3.zero, round[0]);
        Assert.AreEqual(new Vector3(-25f, 0f, 0f), round[1], "the nearest first");
        Assert.AreEqual(new Vector3(30f, 0f, 0f), round[2], "then the nearest to that");
        Assert.AreEqual(new Vector3(60f, 0f, 0f), round[3]);
    }

    [Test]
    public void EveryRaidHasWalkersOnItsTrails()
    {
        foreach (string id in Generated)
        {
            RaidSpawnList walkers = Raid(id).SpawnLists.FirstOrDefault(list => list.walksTheTrails);
            Assert.IsNotNull(walkers, id);
            Assert.AreEqual(SpawnTrigger.RaidStart, walkers.trigger, $"{id}: walking from the start, not from a stage");
        }
    }

    // --- The Woods' own creatures (Q2), landmark sounds (R6) ---

    [Test]
    public void MarshLightsAreRareInTheWoods()
    {
        RaidSpawnList creatures = Raid("MistyWoods").SpawnLists.First(list => list.name == "Creatures");
        float total = creatures.entries.Where(e => e.prefab != null).Sum(e => e.weight);
        float lights = creatures.entries.Where(e => e.prefab != null && e.prefab.name == "MarshLight").Sum(e => e.weight);
        Assert.LessOrEqual(lights / total, 0.05f, "another raid's creature stays under 5% of the list");
    }

    [Test]
    public void EveryNodeLandmarkMakesASound()
    {
        foreach (string id in Generated)
        {
            foreach (RaidLandmarkRule rule in Raid(id).LayoutRules.Landmarks.Where(rule => rule.prefab != null && rule.atNodes))
            {
                Assert.IsNotNull(rule.sound, $"{id} › {rule.prefab.name}: the camera sees 15 m; a sound calls from 60-100");
                Assert.GreaterOrEqual(rule.sound.AudibleDistance, 50f, $"{id} › {rule.prefab.name}");
            }
        }
    }

    // --- The plans keep their quotas (R5) ---

    [Test]
    public void EveryRaidsMapsMeetTheirQuotas()
    {
        var short_ = new List<string>();
        foreach (string id in Generated)
        {
            RaidLayoutRules rules = Raid(id).LayoutRules;
            var placed = new Dictionary<RaidSiteKind, int>();
            const int Seeds = 24;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, rules.MapSize, seed * 7919);
                foreach (RaidSiteQuota quota in rules.Sites)
                {
                    placed[quota.kind] = (placed.TryGetValue(quota.kind, out int had) ? had : 0) + plan.SitesOf(quota.kind).Count;
                }
            }

            foreach (RaidSiteQuota quota in rules.Sites.Where(q => q.count > 0))
            {
                float share = placed[quota.kind] / (float)(quota.count * Seeds);
                if (share < 0.9f)
                {
                    short_.Add($"{id} {quota.kind}: {share:P0} of {quota.count}");
                }
            }
        }

        Assert.IsEmpty(short_, "a quota the map has no room for leaves the raid poorer than its data says:\n" + string.Join("\n", short_));
    }

    // --- The log (R9) ---

    [Test]
    public void ThePlacesRowNamesTheGroupWhereGroupsShareAKind()
    {
        Assert.AreEqual("ResourceSite: Ore Veins", RaidPlacesSeen.NameOf(new RaidPlace(RaidSiteKind.ResourceSite, "Ore Veins", Vector3.zero)));
        Assert.AreEqual("Apex", RaidPlacesSeen.NameOf(new RaidPlace(RaidSiteKind.Apex, null, Vector3.zero)));
        Assert.IsTrue(RaidPlacesSeen.Within(new Vector3(10f, 30f, 0f), Vector3.zero, RaidPlacesSeen.Reach), "measured flat: a place on a ledge above counts");
        Assert.IsFalse(RaidPlacesSeen.Within(new Vector3(20f, 0f, 0f), Vector3.zero, RaidPlacesSeen.Reach));
    }
}
