using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Raids with a reason from home (the Haven's asks), what staying on pays, and the skigard and its gate.</summary>
public sealed class HavenRequestTests
{
    [Test]
    public void Asks_ComeFromPeopleWhoLiveThere_OneEach_AndTheSameOnAReload()
    {
        var settings = AssetDatabase.LoadAssetAtPath<HavenRequestSettings>("Assets/_Project/Resources/HavenRequestSettings.asset");
        Assert.IsNotNull(settings);
        Assert.GreaterOrEqual(settings.Requests.Count, 10);
        foreach (HavenRequestDefinition request in settings.Requests)
        {
            Assert.IsNotNull(request.Wanted, request.name);
            Assert.Greater(request.Count, 0, request.name);
            Assert.IsFalse(string.IsNullOrEmpty(request.Ask), request.name);
            Assert.IsTrue(request.Reward != null || request.Coins > 0 || request.Experience > 0, $"{request.name} gives something");
        }

        // Nobody lives there: only the Haven's own asks.
        var empty = new SettlementState();
        foreach (HavenRequestDefinition request in HavenRequestSettings.Choose(settings.Requests, null, 3, empty, "Summer", 4))
        {
            Assert.IsNull(request.Asker, $"{request.name}: its asker doesn't live there");
        }

        // Everyone lives there: three asks, never two from one person, the same for the same day.
        var full = new SettlementState();
        foreach (HavenRequestDefinition request in settings.Requests)
        {
            if (request.Asker != null && full.Find(request.Asker) == null)
            {
                full.AddResident(request.Asker, null);
            }
        }

        List<HavenRequestDefinition> chosen = HavenRequestSettings.Choose(settings.Requests, null, 3, full, "Summer", 9);
        Assert.AreEqual(3, chosen.Count);
        var askers = new HashSet<ResidentDefinition>();
        foreach (HavenRequestDefinition request in chosen)
        {
            Assert.IsTrue(request.Asker == null || askers.Add(request.Asker), "One ask per person.");
        }

        CollectionAssert.AreEqual(chosen, HavenRequestSettings.Choose(settings.Requests, null, 3, full, "Summer", 9), "A reload asks the same.");
        List<HavenRequestDefinition> more = HavenRequestSettings.Choose(settings.Requests, chosen, 2, full, "Summer", 9);
        Assert.IsFalse(more.Exists(chosen.Contains), "Nothing asked twice.");
    }

    [Test]
    public void SeasonalAsks_OnlyInTheirSeason()
    {
        var settings = AssetDatabase.LoadAssetAtPath<HavenRequestSettings>("Assets/_Project/Resources/HavenRequestSettings.asset");
        HavenRequestDefinition winter = settings.Find("winter-cold");
        Assert.IsNotNull(winter);
        var settlement = new SettlementState();
        Assert.IsTrue(winter.CanAsk(settlement, "Winter"));
        Assert.IsFalse(winter.CanAsk(settlement, "Summer"));
        StringAssert.Contains("(1/6 home)", winter.BriefingLine(1));
    }

    [Test]
    public void DangerStages_PayMoreTheLongerYouStay()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        float last = 0f;
        foreach (RaidStage stage in raid.Stages)
        {
            Assert.Greater(stage.LootBonus, last, $"{stage.DisplayName} pays more than the stage before");
            StringAssert.Contains("loot +", stage.Message);
            last = stage.LootBonus;
        }

        Assert.AreEqual(1f, RaidDirector.StageLootNow, "Outside a raid nothing changes.");
    }

    [Test]
    public void Skigard_IsTheFirstFence_100HealthEach_BuiltFromTheStart()
    {
        foreach (string name in new[] { "Skigard", "Skigard Gate" })
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{name}.asset");
            Assert.IsNotNull(building, name);
            Assert.AreEqual(100f, building.CombatStats.GetValue(StatType.MaxHealth), name);
            Assert.IsTrue(building.KnownFromStart, name);
            Assert.AreEqual(0, building.Requires.Count, $"{name}: no workshop needed");
            Assert.IsNotNull(building.Icon, name);
            GameObject prefab = building.Prefab;
            Assert.IsNotNull(prefab, name);
            Assert.AreEqual(building.CombatStats, new SerializedObject(prefab.GetComponent<Combatant>()).FindProperty("baseStats").objectReferenceValue, name);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)), prefab.GetComponent<SaveableEntity>().PrefabId,
                $"{name} comes back from a save as itself, not as the Palisade it was made from");
            Assert.AreEqual(building, prefab.GetComponent<BuildingObject>().Definition, name);
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
            CollectionAssert.Contains(book.Buildings, building, $"{name} is in the build menu's book");
        }

        var fence = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Skigard.asset");
        Assert.IsTrue(fence.PlaceInLines, "The fence is drawn in lines.");
        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Skigard Gate.asset").PlaceInLines,
            "The gate carries on from a fence's end.");
        Bounds size = fence.Prefab.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
        Assert.AreEqual(4f, size.size.x, 0.2f, "A 4 m piece, like the other walls.");
    }

    [Test]
    public void ALine_BendsRoundThePortal_InChordsThatMeet()
    {
        var centers = new List<Vector3>();
        var directions = new List<Vector3>();
        Vector3 portal = Vector3.zero, start = new Vector3(40f, 0f, 0f);
        BuildPlacement.LayArc(portal, start, new Vector3(0f, 0f, 40f), 4f, 60, centers, directions);
        float most = 2f * Mathf.Asin(4f / 80f);
        Assert.AreEqual(Mathf.CeilToInt(Mathf.PI / 2f / most - 0.05f), centers.Count, "A quarter circle's worth of pieces.");
        float even = Mathf.PI / 2f / centers.Count;
        float half = 40f * Mathf.Sin(even / 2f);
        Assert.LessOrEqual(half * 2f, 4.001f, "No chord longer than a piece: they meet or overlap, never gap.");
        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 a = centers[i] - directions[i] * half, b = centers[i] + directions[i] * half;
            Assert.AreEqual(40f, new Vector2(a.x, a.z).magnitude, 0.01f, "Each piece's ends are on the circle.");
            Assert.AreEqual(40f, new Vector2(b.x, b.z).magnitude, 0.01f);
            if (i > 0)
            {
                Vector3 last = centers[i - 1] + directions[i - 1] * half;
                Assert.Less((last - a).magnitude, 0.01f, "Each starts where the last ended: no gaps.");
            }
        }

        Vector3 far = centers[centers.Count - 1] + directions[directions.Count - 1] * half;
        Assert.Less((far - new Vector3(0f, 0f, 40f)).magnitude, 0.01f, "It ends exactly at the bearing dragged to.");

        BuildPlacement.LayArc(portal, start, new Vector3(0f, 0f, -40f), 4f, 60, centers, directions);
        Assert.Less(centers[0].z, 0f, "The other way round when the cursor is that way.");
        BuildPlacement.LayArc(portal, new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 2f), 4f, 60, centers, directions);
        Assert.AreEqual(0, centers.Count, "Too tight to bend round.");
    }

    [Test]
    public void Gate_OpensAwayFromWhoeverComes_AndLeavesTheWayThroughOpen()
    {
        Assert.Less(FenceGate.SwingFor(1f, 100f), 0f);
        Assert.Greater(FenceGate.SwingFor(-1f, 100f), 0f);
        var gate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Skigard Gate.prefab");
        Assert.IsNotNull(gate.GetComponent<FenceGate>());
        Transform leaf = gate.transform.Find("Leaf");
        Assert.IsNotNull(leaf);
        Assert.IsFalse(leaf.GetComponent<NavMeshObstacle>().carving, "Residents still plan their way through.");
        Assert.IsNull(gate.GetComponent<NavMeshObstacle>(), "No obstacle over the opening.");
        Assert.AreEqual(2, gate.GetComponentsInChildren<NavMeshObstacle>().Length - 1, "The sides carve.");
    }
}
