using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haven as a town (Docs/BuildingSystemResearch.md §3.4): home quality from what stands round a house (only ever
/// adding), yards found from fence rings with a gate and given a role by what is inside, drawn paths, residents finishing
/// scaffolds while the player raids, and station extensions with slots. The rules are pure; the data assets are checked too.
/// </summary>
public sealed class HavenTownTests
{
    private static BuildingDefinition Building(string name) => AssetDatabase.FindAssets("t:BuildingDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .FirstOrDefault(building => building != null && building.name == name);

    private static List<BuildingDefinition> Buildings() => AssetDatabase.FindAssets("t:BuildingDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(b => b != null).ToList();

    // ----- Home quality -----

    private static HomeQuality.Input At(Vector2 door) => new HomeQuality.Input { Door = door };

    [Test]
    public void AHome_IsBetter_ForWhatStandsRoundIt()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        Assert.IsNotEmpty(settings.Features, "Resources/HomeLifeSettings has its features");
        HomeQuality.Input bare = At(Vector2.zero);
        HomeQuality.Report alone = HomeQuality.Rate(bare, settings);

        HomeQuality.Input village = At(Vector2.zero);
        village.Buildings.Add(new HomeQuality.Around(Building("Campfire"), new Vector2(6f, 0f)));
        village.Buildings.Add(new HomeQuality.Around(Building("Well"), new Vector2(0f, 14f)));
        village.Buildings.Add(new HomeQuality.Around(Building("LogBench"), new Vector2(2f, 2f)));
        village.Lights.Add((new Vector2(4f, 0f), 8f));
        village.Work.Add(new Vector2(20f, 0f));
        HomeQuality.Report good = HomeQuality.Rate(village, settings);
        Assert.Greater(good.Quality, alone.Quality);
        foreach (string feature in new[] { "Warm", "Water close", "Lit lane", "A place to sit", "Near work" })
        {
            Assert.IsTrue(good.Has(feature), feature);
            Assert.IsFalse(alone.Has(feature), feature + " on bare ground");
        }

        Assert.IsNotNull(alone.Next, "a bare home says what to build next");
    }

    [Test]
    public void NothingNearAHome_EverMakesItWorse()
    {
        // The user (2026-11-15): every effect positive. A forge, a sauna, a lodge beside a house only ever add or leave it.
        HomeLifeSettings settings = HomeLifeSettings.Current;
        HomeQuality.Input quiet = At(Vector2.zero);
        quiet.Buildings.Add(new HomeQuality.Around(Building("Well"), new Vector2(5f, 0f)));
        int before = HomeQuality.Rate(quiet, settings).Quality;
        foreach (BuildingDefinition kind in Buildings())
        {
            HomeQuality.Input busy = At(Vector2.zero);
            busy.Buildings.Add(new HomeQuality.Around(Building("Well"), new Vector2(5f, 0f)));
            busy.Buildings.Add(new HomeQuality.Around(kind, new Vector2(3f, 1f)));
            Assert.GreaterOrEqual(HomeQuality.Rate(busy, settings).Quality, before, $"a {kind.name} beside a home");
        }
    }

    [Test]
    public void AHomesTier_ItsComforts_AndItsYard_AddToIt()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        Assert.IsNotNull(settings.FindComfort("beds"), "Proper Beds");
        Assert.IsNotNull(settings.FindComfort("kitchen"), "a kitchen hearth");
        HomeQuality.Input plain = At(Vector2.zero);
        HomeQuality.Input furnished = At(Vector2.zero);
        furnished.Tier = 2;
        furnished.Comforts.Add("beds");
        furnished.Comforts.Add("kitchen");
        furnished.YardPoints = 1;
        furnished.YardName = "Home Yard";
        HomeQuality.Report report = HomeQuality.Rate(furnished, settings);
        Assert.AreEqual(HomeQuality.Rate(plain, settings).Quality + settings.PointsPerTier + 1 + 1 + 1, report.Quality,
            "tier, beds, yard, and the kitchen hearth, which makes it warm: one purchase counts once (review 2026-11-16)");
        Assert.IsTrue(report.Has("Warm"), "a kitchen hearth is warm");
    }

    [Test]
    public void AKitchenHearth_CountsOnce_WhetherOrNotAFireIsNear()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        HomeQuality.Input byTheFire = At(Vector2.zero);
        byTheFire.Buildings.Add(new HomeQuality.Around(Building("Campfire"), new Vector2(4f, 0f)));
        HomeQuality.Input byTheFireWithKitchen = At(Vector2.zero);
        byTheFireWithKitchen.Buildings.Add(new HomeQuality.Around(Building("Campfire"), new Vector2(4f, 0f)));
        byTheFireWithKitchen.Comforts.Add("kitchen");
        Assert.IsTrue(HomeQuality.Rate(byTheFire, settings).Has("Warm"), "a campfire by the door is warm");
        Assert.AreEqual(HomeQuality.Rate(byTheFire, settings).Quality + 1, HomeQuality.Rate(byTheFireWithKitchen, settings).Quality,
            "warm already: the kitchen adds its own point");

        HomeQuality.Input alone = At(Vector2.zero);
        HomeQuality.Input aloneWithKitchen = At(Vector2.zero);
        aloneWithKitchen.Comforts.Add("kitchen");
        Assert.AreEqual(HomeQuality.Rate(alone, settings).Quality + 1, HomeQuality.Rate(aloneWithKitchen, settings).Quality,
            "nothing warm near: the kitchen makes it warm, and that is its point");
    }

    [Test]
    public void HomeLevels_RiseWithQuality_AndOnlyGive()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        HomeLifeSettings.Level last = null;
        for (int quality = 0; quality <= 12; quality++)
        {
            HomeLifeSettings.Level level = settings.LevelFor(quality);
            Assert.GreaterOrEqual(level.workSpeed, 0f);
            Assert.GreaterOrEqual(level.comfort, 0);
            if (last != null)
            {
                Assert.GreaterOrEqual(level.workSpeed, last.workSpeed);
                Assert.GreaterOrEqual(level.comfort, last.comfort);
                Assert.GreaterOrEqual(level.renown, last.renown);
            }

            last = level;
        }

        Assert.LessOrEqual(settings.LevelFor(99).workSpeed + settings.ContentWorkSpeed, 0.3f, "small, capped numbers");
        Assert.IsNotEmpty(settings.Likings, "each resident cares for something in a home");
        foreach (HomeLifeSettings.Liking liking in settings.Likings)
        {
            Assert.IsTrue(settings.Features.Any(feature => feature.name == liking.likes), $"{liking.resident?.name} likes '{liking.likes}', which is a feature");
        }
    }

    // ----- Yards -----

    private static YardRings.Edge Fence(float ax, float ay, float bx, float by, int owner, bool gate = false) =>
        new YardRings.Edge(new Vector2(ax, ay), new Vector2(bx, by), gate, true, owner);

    private static List<YardRings.Edge> Square(float x0, float y0, float side, int firstOwner, bool gate)
    {
        return new List<YardRings.Edge>
        {
            Fence(x0, y0, x0 + side, y0, firstOwner),
            Fence(x0 + side, y0, x0 + side, y0 + side, firstOwner + 1),
            Fence(x0 + side, y0 + side, x0, y0 + side, firstOwner + 2, gate),
            Fence(x0, y0 + side, x0, y0, firstOwner + 3),
        };
    }

    [Test]
    public void AClosedRingWithAGate_IsOneYard()
    {
        List<YardRings.Ring> rings = YardRings.Find(Square(0f, 0f, 8f, 0, true), 0.5f, 12f, 900f);
        Assert.AreEqual(1, rings.Count);
        Assert.AreEqual(64f, rings[0].Area, 0.01f);
        Assert.IsTrue(rings[0].HasGate);
        Assert.IsTrue(rings[0].Contains(new Vector2(4f, 4f)));
        Assert.IsFalse(rings[0].Contains(new Vector2(9f, 4f)));
        Assert.AreEqual(4, rings[0].Owners.Count);
    }

    [Test]
    public void ARingWithoutAGate_IsFound_ButHasNone()
    {
        List<YardRings.Ring> rings = YardRings.Find(Square(0f, 0f, 8f, 0, false), 0.5f, 12f, 900f);
        Assert.AreEqual(1, rings.Count);
        Assert.IsFalse(rings[0].HasGate, "a yard needs a gate (the user, 2026-11-15)");
    }

    [Test]
    public void AnOpenRing_IsNoYard_AndABrokenPieceOpensIt()
    {
        List<YardRings.Edge> edges = Square(0f, 0f, 8f, 0, true);
        edges.RemoveAt(1);
        Assert.IsEmpty(YardRings.Find(edges, 0.5f, 12f, 900f));
    }

    [Test]
    public void EndsNearlyMeeting_StillClose_TheRing()
    {
        var edges = new List<YardRings.Edge>
        {
            Fence(0f, 0f, 8f, 0.3f, 0), Fence(8.4f, 0f, 8f, 8f, 1), Fence(8f, 8.3f, 0f, 8f, 2, true), Fence(0.2f, 8f, 0f, 0.2f, 3),
        };
        Assert.AreEqual(1, YardRings.Find(edges, 0.9f, 12f, 900f).Count, "ends within the join distance are one post");
    }

    [Test]
    public void TwoRingsSharingAFence_AreTwoYards()
    {
        var edges = Square(0f, 0f, 8f, 0, true);
        edges.Add(Fence(8f, 0f, 16f, 0f, 10));
        edges.Add(Fence(16f, 0f, 16f, 8f, 11, gate: true));
        edges.Add(Fence(16f, 8f, 8f, 8f, 12));
        List<YardRings.Ring> rings = YardRings.Find(edges, 0.5f, 12f, 900f);
        Assert.AreEqual(2, rings.Count);
        Assert.IsTrue(rings.All(ring => Mathf.Abs(ring.Area - 64f) < 0.01f));
    }

    [Test]
    public void AHouseWall_MayCloseOneSide()
    {
        // Three fences run up to a house's front wall (the farmstead): its walls are edges too, not fences.
        var edges = new List<YardRings.Edge>
        {
            new YardRings.Edge(new Vector2(0f, 0f), new Vector2(4f, 0f), false, false, 9),
            new YardRings.Edge(new Vector2(4f, 0f), new Vector2(4f, -3f), false, false, 9),
            new YardRings.Edge(new Vector2(4f, -3f), new Vector2(0f, -3f), false, false, 9),
            new YardRings.Edge(new Vector2(0f, -3f), new Vector2(0f, 0f), false, false, 9),
            Fence(-2f, 0f, -2f, 6f, 0),
            Fence(-2f, 6f, 6f, 6f, 1, gate: true),
            Fence(6f, 6f, 6f, 0f, 2),
            Fence(-2f, 0f, 0f, 0f, 3),
            Fence(4f, 0f, 6f, 0f, 4),
        };
        List<YardRings.Ring> rings = YardRings.Find(edges, 0.5f, 12f, 900f);
        Assert.AreEqual(1, rings.Count, "the yard; the house's own footprint is no yard (no fence)");
        Assert.AreEqual(48f, rings[0].Area, 0.01f);
        Assert.Contains(9, rings[0].Owners, "the house closes it");
    }

    [Test]
    public void TooSmallOrTooBig_IsNoYard()
    {
        Assert.IsEmpty(YardRings.Find(Square(0f, 0f, 3f, 0, true), 0.5f, 12f, 900f), "a fence round a post");
        Assert.IsEmpty(YardRings.Find(Square(0f, 0f, 40f, 0, true), 0.5f, 12f, 900f), "the whole village");
    }

    [Test]
    public void TheSameFences_InAnyOrder_GiveTheSameYards()
    {
        var edges = Square(0f, 0f, 8f, 0, true);
        edges.Add(Fence(8f, 0f, 16f, 0f, 10));
        edges.Add(Fence(16f, 0f, 16f, 8f, 11, gate: true));
        edges.Add(Fence(16f, 8f, 8f, 8f, 12));
        float Total(List<YardRings.Edge> list) => YardRings.Find(list, 0.5f, 12f, 900f).Sum(ring => ring.Area + ring.Centre.x * 7f + ring.Centre.y * 3f);
        float first = Total(edges);
        edges.Reverse();
        Assert.AreEqual(first, Total(edges), 0.01f, "every machine in co-op finds the same yards from the same pieces");
    }

    [Test]
    public void AYardsRole_IsWhatStandsInIt_TiesToTheFirstListed()
    {
        YardSettings settings = YardSettings.Current;
        Assert.AreEqual(5, settings.Roles.Count, "Home Yard, Work Yard, Kitchen Garden, Feast Green, Paddock");
        YardRoleDefinition Best(params string[] kinds)
        {
            var inside = kinds.Select(Building).ToList();
            YardRoleDefinition best = null;
            int score = -1;
            foreach (YardRoleDefinition role in settings.Roles)
            {
                int s = role.Score(inside, 100f);
                if (s > score)
                {
                    score = s;
                    best = role;
                }
            }

            return best;
        }

        Assert.AreEqual("Home Yard", Best("Hut", "LogBench")?.DisplayName);
        Assert.AreEqual("Feast Green", Best("Campfire", "LogBench", "PlankBench", "TrestleTable")?.DisplayName);
        Assert.AreEqual("Kitchen Garden", Best("HerbPlanter", "HerbPlanter", "Well")?.DisplayName);
        Assert.AreEqual("Paddock", Best("Stable")?.DisplayName);
        Assert.IsNull(Best("Signpost"), "a signpost alone makes no role");
        Assert.IsNotNull(settings.Roles.First(role => role.DisplayName == "Paddock").Missing(new List<BuildingDefinition> { Building("Stable") }, 20f),
            "a paddock wants room");
        foreach (YardRoleDefinition role in settings.Roles)
        {
            Assert.GreaterOrEqual(role.Comfort + role.Renown + role.HomeQuality + role.ExtensionSlots + (role.StationSpeed > 0f ? 1 : 0)
                + (role.GatheringPlace ? 1 : 0), 1, $"{role.name} gives something");
        }
    }

    [Test]
    public void TheFences_CloseYards_AndTheGateIsAGate()
    {
        foreach (string fence in new[] { "Skigard", "Palisade", "StoneWall", "Hedge", "DryStoneWall" })
        {
            BuildingDefinition kind = Building(fence);
            Assert.IsNotNull(kind, fence);
            Assert.IsTrue(kind.Encloses && kind.PlaceInLines, fence);
            Assert.IsFalse(kind.IsGate, fence);
        }

        Assert.IsTrue(Building("Skigard Gate").IsGate);
    }

    // ----- Paths -----

    [Test]
    public void Paths_DirtIsFree_StoneCostsBlocks_AndIsBetter()
    {
        BuildingDefinition dirt = Building("DirtPath");
        BuildingDefinition stone = Building("StonePath");
        Assert.IsNotNull(dirt);
        Assert.IsNotNull(stone);
        Assert.IsTrue(dirt.IsPath && dirt.PlaceInLines && stone.IsPath && stone.PlaceInLines);
        Assert.AreEqual(1.1f, dirt.PathSpeed, 0.001f, "the player 10% faster at home (the user, 2026-11-15)");
        Assert.Greater(stone.PathSpeed, dirt.PathSpeed);
        Assert.IsTrue(stone.Paved && !dirt.Paved);
        Assert.IsEmpty(dirt.ConstructionItems.Where(cost => cost.Amount > 0), "a dirt path costs nothing");
        Assert.IsTrue(stone.ConstructionItems.Any(cost => cost.Item != null && cost.Item.name == "StoneBlock"), "stone bricks: the Stone Block");
        Assert.IsNotNull(dirt.PathDecal);
        Assert.IsNotNull(stone.PathDecal);
        Assert.AreEqual(BuildCategory.Walls, BuildCategories.Of(dirt), "drawn like fences, on the same tab");
        Assert.IsTrue(dirt.Prefab.GetComponent<PathPiece>() != null && dirt.Prefab.GetComponentInChildren<Collider>() == null, "blocks nothing");
    }

    [Test]
    public void APavedPath_PastTheDoor_IsALane()
    {
        HomeQuality.Input home = At(Vector2.zero);
        home.Paved.Add((new Vector2(-3f, 2f), new Vector2(3f, 2f)));
        Assert.IsTrue(HomeQuality.Rate(home, HomeLifeSettings.Current).Has("Lit lane"), "a lit or paved lane");
    }

    // ----- Builders while away -----

    [Test]
    public void BuildersWork_IsSpreadOldestFirst_AndKept()
    {
        float[] given = ResidentBuilders.Spread(10f, new[] { 4f, 3f, 8f });
        CollectionAssert.AreEqual(new[] { 4f, 3f, 3f }, given);
        CollectionAssert.AreEqual(new[] { 0f, 0f }, ResidentBuilders.Spread(0f, new[] { 4f, 3f }));

        HomeLifeSettings settings = HomeLifeSettings.Current;
        Assert.IsTrue(settings.Builders.Any(job => job != null && job.name == "Carpenter"), "Tor the Carpenter");
        Assert.IsTrue(settings.Builders.Any(job => job != null && job.name == "Mason"), "Hild the Mason");

        var settlement = new SettlementState();
        settlement.AddBuilderSeconds(120f);
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), content);
        Assert.AreEqual(120f, back.BuilderSeconds, 0.01f);
        Assert.AreEqual(120f, back.TakeBuilderSeconds(), 0.01f);
        Assert.AreEqual(0f, back.BuilderSeconds);
    }

    [Test]
    public void OnlyBuildersAtHome_Build_NotOneAlongOnTheRaid_NorOnePaused()
    {
        HomeLifeSettings settings = HomeLifeSettings.Current;
        JobDefinition carpenter = settings.Builders.First(job => job != null && job.name == "Carpenter");
        JobDefinition mason = settings.Builders.First(job => job != null && job.name == "Mason");
        ResidentDefinition[] people = AssetDatabase.FindAssets("t:ResidentDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(one => one != null).Take(3).ToArray();
        var settlement = new SettlementState();
        settlement.AddResident(people[0], carpenter);
        settlement.AddResident(people[1], mason);
        settlement.AddResident(people[2]);
        Assert.AreEqual(2, ResidentBuilders.Count(settlement));
        Assert.AreEqual(1, ResidentBuilders.Count(settlement, people[0]), "the carpenter went along on the raid");
        StringAssert.DoesNotContain(people[0].DisplayName, ResidentBuilders.Names(settlement, people[0]));

        settlement.Find(people[1]).WorkPaused = true;
        Assert.AreEqual(1, ResidentBuilders.Count(settlement), "the mason's work is paused");
        Assert.AreEqual(0, ResidentBuilders.Count(settlement, people[0]), "nobody at home to build");
        Assert.IsFalse(ResidentBuilders.WorkAway(settlement, 10f, people[0]), "no work done");
        Assert.AreEqual(0f, settlement.BuilderSeconds);

        settlement.Find(people[1]).WorkPaused = false;
        Assert.IsTrue(ResidentBuilders.WorkAway(settlement, 10f, null));
        Assert.AreEqual(20f, settlement.BuilderSeconds, 0.01f, "builder-seconds: ten seconds, two at home");
        Assert.IsTrue(ResidentBuilders.WorkAway(settlement, 10f, people[0]));
        Assert.AreEqual(30f, settlement.TakeBuilderSeconds(), 0.01f, "the next ten seconds with the carpenter away");
    }

    [Test]
    public void HouseComforts_AreSaved()
    {
        var settlement = new SettlementState();
        Assert.IsTrue(settlement.AddComfort("spawned-hut", "beds"));
        Assert.IsFalse(settlement.AddComfort("spawned-hut", "beds"), "once");
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), content);
        Assert.IsTrue(back.HasComfort("spawned-hut", "beds"));
        Assert.IsTrue(HavenDecisions.All.ContainsKey("house.comfort"), "put in where the Haven is kept");
    }

    [Test]
    public void Footpaths_PackOnlyWhatIsWorn()
    {
        var map = new float[10];
        map[3] = 0.75f;
        map[7] = 1.2f;
        Assert.AreEqual("3:75;7:120;", Footpaths.Pack(map, new[] { 3, 7 }));
    }

    [Test]
    public void Footpaths_KeepWearThatDoesntShowYet_SoPathsCanForm()
    {
        var map = new float[200];
        map[2] = 0.04f;  // walked once: forgotten
        map[3] = 0.12f;  // a few passes: kept, though it doesn't show
        map[7] = 0.75f;
        map[150] = 1.2f;
        string packed = Footpaths.Pack(map, Footpaths.KeptFrom);
        Assert.AreEqual("3:12;+4:75;+143:120;", packed, "in cell order, each after the first as steps on from the last");

        var back = new float[200];
        var showing = new HashSet<int>();
        Footpaths.Unpack(packed, back, showing);
        Assert.AreEqual(0f, back[2]);
        Assert.AreEqual(0.12f, back[3], 0.001f);
        Assert.AreEqual(0.75f, back[7], 0.001f);
        Assert.AreEqual(1.2f, back[150], 0.001f);
        CollectionAssert.AreEquivalent(new[] { 7, 150 }, showing, "only what is worn enough shows");

        var old = new float[200];
        Footpaths.Unpack("3:75;7:120;", old, null);
        Assert.AreEqual(0.75f, old[3], 0.001f, "an older save's paths still read");
        Assert.AreEqual(1.2f, old[7], 0.001f);
    }

    // ----- Doors -----

    [Test]
    public void TheDoor_IsOnTheYardSide_AndTheGhostsReadoutPutsItThere()
    {
        var local = new Bounds(new Vector3(0f, 1.5f, 0f), new Vector3(4f, 3f, 6f));
        Assert.AreEqual(new Vector3(0f, 0f, 3.55f), BuildingObject.DoorAt(Vector3.zero, 0f, local, YardSide.Front, out _));
        Assert.AreEqual(new Vector3(0f, 0f, -3.55f), BuildingObject.DoorAt(Vector3.zero, 0f, local, YardSide.Back, out _));
        Assert.AreEqual(new Vector3(2.55f, 0f, 0f), BuildingObject.DoorAt(Vector3.zero, 0f, local, YardSide.Right, out Quaternion right));
        Assert.Less(Vector3.Distance(Vector3.right, right * Vector3.forward), 0.001f, "it opens to the right");
        Vector3 turned = BuildingObject.DoorAt(new Vector3(10f, 2f, 0f), 90f, local, YardSide.Left, out _);
        Assert.AreEqual(10f, turned.x, 0.001f, "turned a quarter, its left side faces north");
        Assert.AreEqual(2.55f, turned.z, 0.001f);
        Assert.AreEqual(2f, turned.y, "on the ground it stands on");

        // A built house asks the same, with its definition's side.
        BuildingDefinition sideways = Buildings().FirstOrDefault(kind => kind.YardSide != YardSide.Front && kind.Prefab != null);
        Assume.That(sideways != null, "a building with its door on another side");
        var go = Object.Instantiate(sideways.Prefab);
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(5f, 0f, -3f), Quaternion.Euler(0f, 30f, 0f));
            BuildingObject built = go.GetComponent<BuildingObject>();
            Vector3 door = built.Door(out _);
            Assert.AreEqual(BuildingObject.DoorAt(go.transform.position, 30f, built.LocalBounds, sideways.YardSide, out _), door);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ----- Station extensions -----

    [Test]
    public void TheSmithyLodgeAndKitchen_EachHaveExtensions_ThatDoMoreThanSpeed()
    {
        foreach (string station in new[] { "Forge", "Sawbench", "Kitchen" })
        {
            BuildingDefinition kind = Building(station);
            List<BuildingDefinition> extensions = Buildings().Where(b => b.Extends == kind).ToList();
            Assert.GreaterOrEqual(extensions.Count, 3, $"{station}: more extensions than slots, so the player chooses");
            Assert.GreaterOrEqual(kind.ExtensionSlots, 1, $"{station} takes one at its first level");
            int slots = kind.ExtensionSlots + kind.Upgrades.Sum(level => level.extensionSlots);
            Assert.Less(slots, extensions.Count, $"{station}: fewer slots than extensions");
            foreach (BuildingDefinition extension in extensions)
            {
                Assert.IsTrue(extension.Extension.DoesMoreThanSpeed, $"{extension.name} is never only speed");
                Assert.AreEqual(BuildCategory.Stations, BuildCategories.Of(extension), $"{extension.name} is on the stations' tab");
            }
        }
    }
}
