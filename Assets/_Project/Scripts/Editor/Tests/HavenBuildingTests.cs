using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class HavenBuildingTests
{
    private GameObject holder;
    private BuildingDefinition definition;

    [TearDown]
    public void TearDown()
    {
        if (holder != null) Object.DestroyImmediate(holder);
        if (definition != null) Object.DestroyImmediate(definition);
    }

    [Test]
    public void Construction_RisesToLevelOne_ThenUpgradesAddTheirBonuses()
    {
        Construction construction = Building(4f,
            new BuildingLevel { title = "Sturdy", renown = 3, craftSpeed = 0.25f, extraQueue = 1 },
            new BuildingLevel { title = "Master's", renown = 5, craftSpeed = 0.25f, extraStorage = 6 });
        int finished = 0;
        Action<Construction> count = _ => finished++;
        Construction.Finished += count;
        try
        {
            construction.BeginBuilding();
            Assert.IsTrue(construction.IsBuilding);
            Assert.IsFalse(construction.IsStanding, "not usable while the scaffold stands");
            Assert.AreEqual(4f, construction.WorkNeeded);

            construction.Finish();
            Assert.AreEqual(1, construction.Level);
            Assert.IsFalse(construction.IsBuilding);
            Assert.IsTrue(construction.CanUpgrade);
            Assert.AreEqual(1, finished);

            construction.RestoreState("3,3,0,1");
            Assert.AreEqual(3, construction.Level);
            Assert.IsFalse(construction.CanUpgrade, "level 3 is the last");
            Assert.AreEqual(0.5f, construction.Sum(level => level.craftSpeed), 0.0001f);
            Assert.AreEqual(1f, construction.Sum(level => level.extraQueue));
            Assert.AreEqual(6f, construction.Sum(level => level.extraStorage));
            Assert.AreEqual("Master's Test Hut", construction.Title);
        }
        finally
        {
            Construction.Finished -= count;
        }
    }

    [Test]
    public void AModule_IsHammeredOn_AndCountsOnlyOnceItIsFitted_AndIsSavedMidWay()
    {
        // Fitting is hammered like a tier (Docs/BuildingUpgradesResearch_2026-11-21.md, decision 2): saved half done, it comes back so.
        Construction construction = Building(4f, new BuildingLevel { title = "Sturdy" });
        construction.RestoreState("1,1,2.5,1,,module.storageloft");
        Assert.IsTrue(construction.IsFitting, "being fitted");
        Assert.AreEqual(0, construction.Fitted.Count, "not counted yet");
        Assert.IsFalse(construction.IsBuilding, "the station keeps working");
        Assert.AreEqual(construction.Fitting.Work, construction.WorkNeeded, 0.001f, "its own hammering");
        Assert.AreEqual("1,1,2.5,1,,module.storageloft", construction.CaptureState());
        Assert.IsFalse(construction.CanUpgrade, "one job at a time");

        construction.Finish();
        Assert.IsFalse(construction.IsFitting);
        Assert.AreEqual(1, construction.Fitted.Count);
        Assert.AreEqual("module.storageloft", construction.Fitted[0].Id);
        Assert.AreEqual("1,1,0,1,module.storageloft", construction.CaptureState());
    }

    [Test]
    public void Construction_SavesItsLevelAndTheWorkDone()
    {
        Construction construction = Building(6f, new BuildingLevel { title = "Sturdy", work = 8f });
        construction.RestoreState("1,2,3.5,1");
        Assert.AreEqual("1,2,3.5,1", construction.CaptureState());
        Assert.IsTrue(construction.IsBuilding);
        Assert.AreEqual(8f, construction.WorkNeeded);
        Assert.AreEqual(3.5f / 8f, construction.Share, 0.0001f);
    }

    [Test]
    public void HavenLadder_StandsRanksAnEqualStepApart_AndEndsAtTheTop()
    {
        var ranks = new[]
        {
            new HavenSettings.Rank { renown = 0 },
            new HavenSettings.Rank { renown = 10 },
            new HavenSettings.Rank { renown = 100 },
        };
        Assert.AreEqual(0f, HavenOverview.ClimbOf(ranks, 0, 0));
        Assert.AreEqual(0.25f, HavenOverview.ClimbOf(ranks, 0, 5), 0.0001f, "halfway to the second of three stops");
        Assert.AreEqual(0.5f, HavenOverview.ClimbOf(ranks, 1, 10), 0.0001f, "the bigger gap doesn't move the stop");
        Assert.AreEqual(0.5f, HavenOverview.ClimbOf(ranks, 1, 3), 0.0001f, "a rank once reached is kept, whatever the renown");
        Assert.AreEqual(1f, HavenOverview.ClimbOf(ranks, 2, 100));
        Assert.AreEqual(1f, HavenOverview.ClimbOf(ranks, 2, 900));
        Assert.AreEqual(1f, HavenOverview.ClimbOf(System.Array.Empty<HavenSettings.Rank>(), 0, 0), "nothing to climb");
    }

    [Test]
    public void HavenOverview_PutsAKindOnOneLine_LargestFirst()
    {
        var parts = new[]
        {
            new HavenRenown.Part("Campfire", 3),
            new HavenRenown.Part("Forge", 10),
            new HavenRenown.Part("Campfire", 2),
            new HavenRenown.Part("Campfire", 2),
            new HavenRenown.Part("Clearing the overgrowth", 0),
        };
        var grouped = HavenOverview.Grouped(parts).ToList();
        Assert.AreEqual(2, grouped.Count, "nothing is shown for nothing");
        Assert.AreEqual("Forge", grouped[0].Label);
        Assert.AreEqual("Campfire ×3", grouped[1].Label);
        Assert.AreEqual(7, grouped[1].Renown);
    }

    [Test]
    public void ATrap_StaysWalkable_AndTheHealthCheckAgrees()
    {
        var trap = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/SpikeTrap.asset");
        Assert.IsNull(trap.Prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>(), "creatures have to walk onto it");
        CollectionAssert.DoesNotContain(BuildingPrefabBuilder.FindProblems(trap.Prefab, trap).Select(p => p.ToLowerInvariant().Contains("obstacle")), true);
    }

    [Test]
    public void EveryItem_HasAWorldPrefabOfItsOwn()
    {
        var seen = new System.Collections.Generic.Dictionary<GameObject, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item.Prefab == null)
            {
                continue;
            }

            Assert.IsFalse(seen.ContainsKey(item.Prefab), $"{item.name} and {(seen.TryGetValue(item.Prefab, out string other) ? other : "")} share a prefab, which only drops as one of them");
            seen[item.Prefab] = item.name;
        }
    }

    [Test]
    public void HavenRanks_ClimbWithRenown_AndTheWardOnlyGrows()
    {
        HavenSettings settings = HavenSettings.Current;
        Assert.GreaterOrEqual(settings.Ranks.Count, 4);
        Assert.AreEqual(0, settings.RankFor(0));
        Assert.AreEqual(1, settings.RankFor(settings.Ranks[1].renown));
        Assert.AreEqual(settings.Ranks.Count - 1, settings.RankFor(100000));
        for (int i = 1; i < settings.Ranks.Count; i++)
        {
            Assert.Greater(settings.Ranks[i].renown, settings.Ranks[i - 1].renown);
            Assert.GreaterOrEqual(settings.WardBonusAt(i), settings.WardBonusAt(i - 1), "each rank widens the ward");
        }

        var settlement = new SettlementState();
        Assert.IsTrue(settlement.ReachHavenRank(2));
        Assert.IsFalse(settlement.ReachHavenRank(1), "a rank once reached is kept");
        Assert.AreEqual(2, settlement.HavenRank);
        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, settlement: settlement);
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);
        Assert.AreEqual(2, loaded.Settlement.HavenRank);
    }

    [Test]
    public void AuthoredBuildings_HaveConstructionAndPricedUpgrades()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/_Project/Data/Buildings" }))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (building.Prefab == null || building.Faction != Faction.Player) continue;
            Assert.NotNull(building.Prefab.GetComponent<Construction>(), building.name);
            foreach (BuildingLevel level in building.Upgrades)
            {
                Assert.IsNotEmpty(level.cost, $"{building.name} {level.title}");
                Assert.IsTrue(level.cost.All(c => c.Item != null && c.Amount > 0), $"{building.name} {level.title}");
                Assert.Greater(level.work, 0f, $"{building.name} {level.title}");
            }
        }

        Assert.Greater(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Sawbench.asset").Upgrades.Count, 0);
        Assert.AreEqual(0f, AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Campfire.asset").BuildWork,
            "the prologue's campfire still stands at once");
    }

    [Test]
    public void SafeHaven_StartsOvergrownWithARuinToRepair()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SafeHaven.unity");
        try
        {
            Overgrowth[] overgrowth = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Overgrowth>(true)).ToArray();
            Assert.GreaterOrEqual(overgrowth.Length, 30);
            foreach (Overgrowth piece in overgrowth)
            {
                var node = piece.GetComponent<ResourceNode>();
                Assert.NotNull(node.Definition, piece.name);
                Assert.AreEqual(0f, node.Definition.RespawnSeconds, $"{piece.name} never grows back");
            }

            Assert.IsTrue(overgrowth.Any(o => o.GetComponent<ResourceNode>().Definition.RequiredToolTier >= 2), "some wait for better tools");
            // The ruins the Haven starts with: the Storage Shed to repair, and the cold hearth the shelter's ember lights (A20, 2026-11-21).
            Construction[] ruins = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Construction>(true)).Where(c => c.StartsRuined).ToArray();
            CollectionAssert.AreEquivalent(new[] { "StorageShed", "Campfire" }, ruins.Select(r => r.GetComponent<BuildingObject>().Definition.name).ToArray());
            Assert.IsNotNull(ruins.Single(r => r.GetComponent<BuildingObject>().Definition.name == "Campfire").GetComponent<ColdHearth>(), "the ruined campfire is the cold hearth");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private Construction Building(float work, params BuildingLevel[] levels)
    {
        definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        definition.name = "Test Hut";
        definition.ConfigureBuildingUp(work, 5, levels);
        holder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var building = holder.AddComponent<BuildingObject>();
        var data = new SerializedObject(building);
        data.FindProperty("definition").objectReferenceValue = definition;
        data.ApplyModifiedPropertiesWithoutUndo();
        return holder.AddComponent<Construction>();
    }
}
