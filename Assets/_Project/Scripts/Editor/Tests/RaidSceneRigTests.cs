using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Each raid has a scene of its own (2026-11-17: Troll Valley was split from the Misty Woods' scene, Docs/TrollValleyPlan.md step 1).
/// Read from the scene files, so nothing is opened: both carry the same raid systems, each has its own map, terrain and look, and the
/// Woods' quests and story stay in the Woods.
/// </summary>
public sealed class RaidSceneRigTests
{
    private const string Woods = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string Valley = "Assets/_Project/Scenes/Raids/Raid_TrollValley.unity";

    private static readonly System.Type[] Systems =
    {
        typeof(RaidMap), typeof(RaidGenerator), typeof(RaidDirector), typeof(WeatherSystem), typeof(DayNightCycle), typeof(SceneAudio),
        typeof(TerrainSurface), typeof(TerrainMoss), typeof(TerrainMist), typeof(SpawnGroup), typeof(GameStateManager),
    };

    private static string Guid(string path) => AssetDatabase.AssetPathToGUID(path);

    private static string ScriptGuid(System.Type type)
    {
        MonoScript script = MonoImporter.GetAllRuntimeMonoScripts().FirstOrDefault(each => each.GetClass() == type);
        Assert.IsNotNull(script, type.Name);
        return Guid(AssetDatabase.GetAssetPath(script));
    }

    [Test]
    public void TrollValley_PlaysInItsOwnScene()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset");
        Assert.AreEqual("Raid_TrollValley", raid.SceneName);
        Assert.IsTrue(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == Valley), "in Build Settings");
        Assert.AreEqual("Troll Valley", SaveSlotInfo.PlaceName("Raid_TrollValley"));
    }

    [Test]
    public void BothRaidScenes_CarryTheSameSystems()
    {
        string woods = File.ReadAllText(Woods);
        string valley = File.ReadAllText(Valley);
        foreach (System.Type type in Systems)
        {
            string guid = ScriptGuid(type);
            Assert.IsTrue(woods.Contains(guid), $"the Woods have a {type.Name}");
            Assert.IsTrue(valley.Contains(guid), $"the valley has a {type.Name}");
        }
    }

    [Test]
    public void EachRaidScene_HasItsOwnMapAndLook()
    {
        string woods = File.ReadAllText(Woods);
        string valley = File.ReadAllText(Valley);
        string[][] pairs =
        {
            new[] { "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset", "Assets/_Project/Data/Raids/Maps/TrollValley Map.asset" },
            new[] { "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset", "Assets/_Project/Data/Raids/Maps/TrollValley Map Terrain.asset" },
            new[] { "Assets/_Project/Settings/Rendering/Volumes/MistyWoods_Raid_Volume.asset", "Assets/_Project/Settings/Rendering/Volumes/TrollValley_Raid_Volume.asset" },
            new[] { "Assets/_Project/Settings/Rendering/Volumes/MistyWoods_Raid_Night.asset", "Assets/_Project/Settings/Rendering/Volumes/TrollValley_Raid_Night.asset" },
            new[] { "Assets/_Project/Data/Environment/Misty Woods Climate.asset", "Assets/_Project/Data/Environment/Troll Valley Climate.asset" },
            new[] { "Assets/_Project/Data/Environment/Misty Woods Day Night.asset", "Assets/_Project/Data/Environment/Troll Valley Day Night.asset" },
            new[] { "Assets/_Project/Data/World/Birds - Misty Woods.asset", "Assets/_Project/Data/World/Birds - Troll Valley.asset" },
        };

        foreach (string[] pair in pairs)
        {
            string mine = Guid(pair[0]), theirs = Guid(pair[1]);
            Assert.IsNotEmpty(theirs, pair[1]);
            Assert.IsTrue(woods.Contains(mine), pair[0]);
            Assert.IsFalse(woods.Contains(theirs), $"the Woods use {pair[1]}");
            Assert.IsTrue(valley.Contains(theirs), pair[1]);
            Assert.IsFalse(valley.Contains(mine), $"the valley uses {pair[0]}");
        }

        // The copied terrain material bakes its own layers' masks, so a new ground in the valley never changes the Woods'.
        var valleyMap = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(pairs[0][1]);
        var woodsMap = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(pairs[0][0]);
        Assert.AreNotSame(woodsMap.TerrainMaterial, valleyMap.TerrainMaterial);
        Assert.AreNotSame(woodsMap.TerrainMaterial.GetTexture("_DisplacementMasks"), valleyMap.TerrainMaterial.GetTexture("_DisplacementMasks"));
    }

    [Test]
    public void TheWoodsQuestsAndStory_StayInTheWoods()
    {
        string valley = File.ReadAllText(Valley);
        foreach (string path in new[]
                 {
                     "Assets/_Project/Data/Quests/EchoesInTheWilds.asset", "Assets/_Project/Data/Quests/AStrangerInTheMist.asset",
                     "Assets/_Project/Data/Quests/Targets/Wilds Echo.asset", "Assets/_Project/Data/Quests/Targets/Western Beacon.asset",
                     "Assets/_Project/Prefabs/Characters/StrandedTraveler.prefab", "Assets/_Project/Prefabs/Enemies/OldMossTroll.prefab",
                 })
        {
            Assert.IsNotEmpty(Guid(path), path);
            Assert.IsFalse(valley.Contains(Guid(path)), path);
        }

        Assert.IsTrue(valley.Contains(Guid("Assets/_Project/Prefabs/Enemies/Bergjutul.prefab")), "a Bergjutul walks the valley's apex round");
        Assert.IsTrue(valley.Contains(Guid("Assets/_Project/Data/Raids/TrollValley.asset")), "played on its own, the scene is Troll Valley");
    }
}
