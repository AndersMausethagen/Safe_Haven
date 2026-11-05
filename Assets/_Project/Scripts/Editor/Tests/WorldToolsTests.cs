using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class WorldToolsTests
{
    [Test]
    public void NewSceneDefaults_SupportWeatherMistAndShaftsBeforeSaveAndPersistOnSave()
    {
        Scene scene = TestScenes.StartClean();
        string folder = "Assets/EnvironmentDefaultsTest_" + System.Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
        TerrainData terrainData = null;
        VolumeProfile transientProfile = null;
        try
        {
            new SafeHavenScenePipeline().AfterTemplateInstantiation(SafeHavenSceneTemplate.Ensure(), scene, false, "");
            Assert.AreEqual("", scene.path, "Support must work before the scene is saved.");
            var rig = scene.GetRootGameObjects().Single(root => root.name == WeatherSetup.RigName);
            Assert.IsNotNull(rig.GetComponentInChildren<WeatherSystem>());
            Assert.IsNotNull(rig.GetComponentInChildren<DayNightCycle>());
            Assert.IsNotNull(RenderSettings.sun);
            Assert.AreNotEqual(LightShadows.None, RenderSettings.sun.shadows);
            Volume volume = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true))
                .Single(v => v.isGlobal && v.priority <= 0f && v.sharedProfile != null && v.sharedProfile.Has<AtmosphericFog>());
            transientProfile = volume.sharedProfile;
            Assert.IsFalse(AssetDatabase.Contains(transientProfile));
            Assert.IsTrue(transientProfile.TryGet(out AtmosphericFog fog));
            Assert.AreEqual(0.025f, fog.density.value, "Authored fog strength is available when fog weather arrives.");
            Assert.AreEqual(0f, fog.driftSpeed.value, "Fog stays stationary independently of moving terrain mist.");
            Assert.Greater(fog.shaftDensity.value, 0f);
            Assert.IsTrue(fog.shaftDensity.overrideState);
            Assert.IsFalse(transientProfile.Has<FogOfWar>());

            terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(20f, 4f, 20f) };
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            typeof(SceneEnvironmentDefaults).GetMethod("CompleteTerrainMist",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            var mist = terrainObject.GetComponent<TerrainMist>();
            Assert.IsNotNull(mist, "A terrain added after scene creation must receive mist automatically.");
            var settings = new SerializedObject(mist);
            Assert.AreEqual(0f, settings.FindProperty("baseAmount").floatValue);
            Assert.AreEqual(1f, settings.FindProperty("weatherAmount").floatValue);
            Assert.AreEqual(1f, settings.FindProperty("goldenHourAmount").floatValue);
            settings.FindProperty("density").floatValue = 0.07f;
            settings.ApplyModifiedProperties();
            mist.enabled = false;
            AtmosphereSetup.EnsureSceneDefaults(scene);
            Assert.AreEqual(0.07f, mist.Density);
            Assert.IsFalse(mist.enabled, "Opting out of mist must survive completion.");

            var cycle = rig.GetComponentInChildren<DayNightCycle>();
            var customProfile = ScriptableObject.CreateInstance<DayNightProfile>();
            AssetDatabase.CreateAsset(customProfile, folder + "/CustomDayNight.asset");
            var cycleSettings = new SerializedObject(cycle);
            cycleSettings.FindProperty("profile").objectReferenceValue = customProfile;
            cycleSettings.FindProperty("startHour").floatValue = 17f;
            cycleSettings.ApplyModifiedProperties();
            Volume nightVolume = rig.transform.Find("Night Volume").GetComponent<Volume>();
            nightVolume.priority = 5f;
            Assert.AreSame(rig, WeatherSetup.AddToScene(WeatherSetup.EnsureAssets()));
            cycleSettings.Update();
            Assert.AreSame(customProfile, cycleSettings.FindProperty("profile").objectReferenceValue);
            Assert.AreEqual(17f, cycleSettings.FindProperty("startHour").floatValue);
            Assert.AreEqual(5f, nightVolume.priority);
            Assert.AreEqual(1, rig.GetComponentsInChildren<DayNightCycle>(true).Length);
            Assert.AreEqual(1, rig.GetComponentsInChildren<WeatherSystem>(true).Length);

            float shaftDensity = fog.shaftDensity.value;
            AtmosphereSetup.SetUpFog();
            Assert.Greater(fog.density.value, 0f);
            Assert.AreEqual(shaftDensity, fog.shaftDensity.value, "Fog setup must not change shaft tuning.");
            float fogDensity = fog.density.value;
            AtmosphereSetup.SetUpLightShafts();
            Assert.AreEqual(fogDensity, fog.density.value, "Shaft setup must not change fog tuning.");
            AssetDatabase.CreateAsset(terrainData, folder + "/Terrain.asset");
            string path = folder + "/DefaultScene.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, path));
            string profilePath = AssetDatabase.GetAssetPath(volume.sharedProfile);
            Assert.IsTrue(profilePath.StartsWith(folder + "/"));
            Assert.AreEqual(profilePath, AssetDatabase.GetAssetPath(fog), "Fog must be stored with the scene's profile.");
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var savedMist = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TerrainMist>(true)).Single();
            Assert.AreEqual(0.07f, savedMist.Density);
            Assert.IsFalse(savedMist.enabled);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath));
            Volume savedVolume = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true))
                .Single(v => v.isGlobal && v.priority <= 0f && v.sharedProfile != null && v.sharedProfile.Has<AtmosphericFog>());
            Assert.IsTrue(savedVolume.sharedProfile.TryGet(out AtmosphericFog savedFog));
            Assert.AreEqual(shaftDensity, savedFog.shaftDensity.value);
            Assert.AreEqual(fogDensity, savedFog.density.value);
        }
        finally
        {
            TestScenes.StartClean();
            AssetDatabase.DeleteAsset(folder);
            if (terrainData != null && !AssetDatabase.Contains(terrainData)) Object.DestroyImmediate(terrainData);
            if (transientProfile != null && !AssetDatabase.Contains(transientProfile)) Object.DestroyImmediate(transientProfile);
        }
    }

    [Test]
    public void NewScenes_ComeFromTheSafeHavenTemplateNotFromEveryNewScene()
    {
        var template = AssetDatabase.LoadAssetAtPath<UnityEditor.SceneTemplate.SceneTemplateAsset>(SafeHavenSceneTemplate.TemplatePath);
        Assert.IsNotNull(template, "Safe Haven › World › Create Scene Template makes it.");
        Assert.IsNotNull(template.templateScene);
        Assert.AreEqual(typeof(SafeHavenScenePipeline), template.templatePipeline.GetClass());
        Assert.IsTrue(template.addToDefaults, "Pinned in File › New Scene.");

        Scene scene = TestScenes.StartClean();
        Assert.AreEqual(0, scene.rootCount, "A scene made by code is left as it was made.");
    }

    [Test]
    public void AtmosphereSetup_IsIdempotentAndDoesNotAddExplorationOrReplaceExistingRig()
    {
        Scene scene = TestScenes.StartClean();
        string folder = "Assets/AtmosphereAuthoringTest_" + System.Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
        try
        {
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, folder + "/Authoring.unity"));
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, folder + "/Existing.asset");
            var volume = new GameObject("Existing Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            var rig = new GameObject("Existing Sky Rig");
            AtmosphereSetup.SetUpLightShafts();
            Assert.IsTrue(profile.TryGet(out AtmosphericFog fog));
            fog.density.Override(0.017f);
            AtmosphereSetup.SetUpLightShafts();
            Assert.AreSame(profile, volume.sharedProfile);
            Assert.AreEqual(0.017f, fog.density.value);
            Assert.Greater(fog.shaftDensity.value, 0f);
            Assert.IsTrue(fog.shaftDensity.overrideState);
            Assert.IsTrue(rig != null);
            Assert.AreEqual(1, scene.GetRootGameObjects().Count(g => g.name == "Atmosphere"));
            Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).Count());
            Assert.IsFalse(profile.Has<FogOfWar>());
            Assert.IsFalse(scene.GetRootGameObjects().Any(g => g.GetComponent<ExplorationMap>() != null));
        }
        finally
        {
            TestScenes.StartClean();
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void AtmosphereSetup_EnablesShaftsWithoutResettingFogOrExistingTuning()
    {
        var fog = UnityEngine.ScriptableObject.CreateInstance<AtmosphericFog>();
        try
        {
            fog.density.Override(0.023f);
            AtmosphereSetup.EnableLightShafts(fog);
            Assert.AreEqual(0.004f, fog.shaftDensity.value);
            Assert.AreEqual(48, fog.steps.value);
            Assert.AreEqual(0.023f, fog.density.value);
            fog.shaftDensity.Override(0.012f);
            fog.shaftIntensity.Override(7f);
            fog.steps.Override(64);
            AtmosphereSetup.EnableLightShafts(fog);
            Assert.AreEqual(0.012f, fog.shaftDensity.value);
            Assert.AreEqual(7f, fog.shaftIntensity.value);
            Assert.AreEqual(64, fog.steps.value);
            Assert.AreEqual(0.023f, fog.density.value);
        }
        finally { UnityEngine.Object.DestroyImmediate(fog); }
    }

    [Test]
    public void EveryWorldToolsButtonRunsAMenuThatExists()
    {
        var menus = TypeCache.GetMethodsWithAttribute<MenuItem>()
                             .SelectMany(m => m.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>())
                             .Select(a => a.menuItem)
                             .ToHashSet();
        foreach (string path in WorldToolsWindow.MenuPaths)
        {
            Assert.IsTrue(menus.Contains(path), $"World Tools runs '{path}', which no menu item has (renamed?)");
        }
    }

    [TestCase("Assets/_Project/Prefabs/Environment/World/Rock Face A.prefab", "Rock Face A", "Rocks & Cliffs")]
    [TestCase("Assets/_Project/Prefabs/Environment/World/Norway Spruce B.prefab", "Norway Spruce B", "Trees")]
    [TestCase("Assets/_Project/Prefabs/Environment/World/Old Stump A.prefab", "Old Stump A", "Forest Floor")]
    [TestCase("Assets/_Project/Prefabs/Resources/GreyStoneNode.prefab", "GreyStoneNode", "Resources")]
    [TestCase("Assets/_Project/Prefabs/Raids/ExtractionPoint.prefab", "ExtractionPoint", "Raid")]
    [TestCase("Assets/_Project/Prefabs/Structures/Torch.prefab", "Torch", "Buildings")]
    [TestCase("Assets/_Project/Art/Vegetation/Misty Woods/Trees/Cedar A.prefab", "Cedar A", "Trees")]
    [TestCase("Assets/_Project/Art/Vegetation/Misty Woods/Rocks and Logs/Cliff Slab.prefab", "Cliff Slab", "Rocks & Cliffs")]
    [TestCase("Assets/_Project/Art/Vegetation/Misty Woods/Rocks and Logs/Mossy Rotten Log.prefab", "Mossy Rotten Log", "Forest Floor")]
    [TestCase("Assets/_Project/Art/Vegetation/Safe Haven/Plants/Lake Fern A.prefab", "Lake Fern A", "Plants & Grass")]
    [TestCase("Assets/_Project/Art/Vegetation/Safe Haven/Rocks/Lake Crag A.prefab", "Lake Crag A", "Rocks & Cliffs")]
    public void PrefabsAreSortedIntoTheGroupsADesignerLooksFor(string path, string name, string expected)
    {
        Assert.AreEqual(expected, PrefabCatalog.CategoryOf(path, name));
    }

    [Test]
    public void TidyHierarchySortsLooseObjectsIntoNumberedFolders()
    {
        Scene scene = TestScenes.StartClean();
        Assume.That(string.IsNullOrEmpty(scene.path), "only ever in an untitled test scene");
        var made = new System.Collections.Generic.List<GameObject>();
        try
        {
            GameObject Make(string name, System.Type component = null)
            {
                var go = component != null ? new GameObject(name, component) : new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                made.Add(go);
                return go;
            }

            Make("Directional Light", typeof(Light));
            Make("Main Camera", typeof(Camera));
            Make("Pond", typeof(WaterSurface));
            Make("Gathering");
            Make("Residents");
            Make("World");
            Assert.AreEqual(6, scene.GetRootGameObjects().Length);

            SceneFolders.Tidy(scene);
            Assert.IsTrue(scene.GetRootGameObjects().All(SceneFolders.IsFolder), "only folders are left at the top");
            Assert.AreEqual(SceneFolders.Lighting, SceneFolders.Find(scene, "Directional Light").transform.parent.name);
            Assert.AreEqual(SceneFolders.PlayerAndCameras, SceneFolders.Find(scene, "Main Camera").transform.parent.name);
            Assert.AreEqual(SceneFolders.World, SceneFolders.Find(scene, "Pond").transform.parent.name);
            Assert.AreEqual(SceneFolders.World, SceneFolders.Find(scene, "World").transform.parent.name, "the World Builder's root is found inside it");
            Assert.AreEqual(SceneFolders.Gameplay, SceneFolders.Find(scene, "Gathering").transform.parent.name);
            CollectionAssert.AreEqual(scene.GetRootGameObjects().Select(r => r.name).OrderBy(n => n, System.StringComparer.Ordinal).ToArray(),
                                      scene.GetRootGameObjects().Select(r => r.name).ToArray(), "folders in their numbered order");
            Assert.AreEqual("already tidy", SceneFolders.Tidy(scene));
            Assert.IsTrue(SceneFolders.IsUnder(SceneFolders.Find(scene, "World").transform, "World"));
        }
        finally
        {
            // A test camera left behind in a scene that gets saved draws over the game's camera (it happened).
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            foreach (GameObject go in made.Where(g => g != null))
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    [Test]
    public void TheCatalogOffersTheWorldBuildersRocksAndTrees()
    {
        PrefabCatalog.Refresh();
        Assert.IsTrue(PrefabCatalog.All.Any(e => e.Category == "Rocks & Cliffs" && e.Name.Contains("Mossy Boulder")));
        Assert.IsTrue(PrefabCatalog.All.Any(e => e.Category == "Trees" && e.Name.Contains("Norway Spruce")));
        Assert.IsFalse(PrefabCatalog.All.Any(e => e.Path.Contains("_Leaves_Wind")), "tree parts are not offered on their own");
        foreach (WorldLook look in WorldLook.All)
        {
            Assert.IsTrue(PrefabCatalog.All.Any(e => e.Set == look.Name && e.Category == "Trees"), $"{look.Name} trees are offered");
            Assert.IsTrue(PrefabCatalog.All.Any(e => e.Set == look.Name && e.Category == "Rocks & Cliffs"), $"{look.Name} rocks are offered");
        }

        Assert.IsFalse(PrefabCatalog.All.Any(e => e.Path.Contains("/Art/Vegetation/") && e.Path.Contains("/Effects/")), "falling-needle effects are not level pieces");
    }

    [Test]
    public void TheBiomesAndToolsUseTheNewLooks()
    {
        WorldAssets.EnsureAll();
        foreach (BiomeArea.Layer layer in WorldMenus.DarkForestLayers().Concat(WorldMenus.HavenGroveLayers()))
        {
            Assert.IsNotNull(layer.palette, layer.name);
            Assert.IsTrue(layer.palette.entries.All(e => e.prefab != null && AssetDatabase.GetAssetPath(e.prefab).Contains("/Art/Vegetation/")),
                          $"{layer.palette.name} is built from the converted packs");
        }

        Assert.IsTrue(WorldMenus.DarkForestLayers().All(l => AssetDatabase.GetAssetPath(l.palette).StartsWith(WorldLook.MossyForest.DataFolder)));
        Assert.IsTrue(WorldMenus.HavenGroveLayers().All(l => AssetDatabase.GetAssetPath(l.palette).StartsWith(WorldLook.ForestLake.DataFolder)));
        foreach (WorldLook look in WorldLook.All)
        {
            Assert.IsNotNull(look.Crags, look.Name);
            Assert.IsNotEmpty(look.Crags.Pieces);
            Assert.IsTrue(look.Crags.Pieces.All(p => p.prefab.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial.shader.name == MeshSurface.ShaderName)),
                          $"{look.Name} cliffs are tessellated");
            Assert.IsNotNull(look.PathGround);
            Assert.IsNotNull(look.RiverBed);
            Assert.IsNotNull(look.TerrainTemplate);
            Assert.AreEqual(TerrainDisplacementEditor.ShaderName, look.TerrainTemplate.shader.name);
            foreach (string rules in look.PaintLooks)
            {
                Assert.IsTrue(look.Rules(rules).Rules.All(r => r.layer != null && AssetDatabase.GetAssetPath(r.layer).StartsWith(look.GroundFolder)),
                              $"{look.Name} {rules} paints its own grounds");
            }
        }
    }

    [Test]
    public void GeneratedTreesCantBeWalkedThrough()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Models/Generated/Vegetation" }))
        {
            var tree = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            bool whole = tree.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Any(f => f.name == "Trunk" && f.transform != tree.transform);
            if (whole)
            {
                Assert.IsNotNull(tree.GetComponentInChildren<UnityEngine.Collider>(true), $"{tree.name} has no trunk collider");
            }
        }
    }

    [Test]
    public void AnEmptySceneIsReportedAsUnplayable()
    {
        // The test runner's own scene: untitled and empty of gameplay.
        Scene scene = SceneManager.GetActiveScene();
        Assume.That(string.IsNullOrEmpty(scene.path), "runs in the test runner's untitled scene");
        var raid = ScenePlayability.Check(scene, ScenePlayability.Kind.Raid);
        Assert.IsTrue(raid.Any(i => i.Severity == ScenePlayability.Severity.Error && i.Message.Contains("No player")));
        Assert.IsTrue(raid.Any(i => i.Message.Contains("extraction point")), "a raid needs a way home");
        Assert.IsTrue(raid.Any(i => i.Message.Contains("not saved")), "an unsaved scene can't be made playable");
        var home = ScenePlayability.Check(scene, ScenePlayability.Kind.SafeHaven);
        Assert.IsTrue(home.Any(i => i.Message.Contains("raid portal")));
        Assert.IsFalse(home.Any(i => i.Message.Contains("extraction point")), "a home has no exits to check");
        Assert.AreEqual(ScenePlayability.Severity.Error, raid[0].Severity, "errors come first");
    }
}
