using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// A daylight look-dev scene for the Misty Woods forest (`unity command script run --file AgentScripts/BuildDaylightTestScene.cs
/// --entry BuildDaylightTestScene.Apply`): one generated raid map (same paint, scatter and growth as a raid), a midday sun,
/// sky and wind, no mist or night. Saved as Scenes/Tests/MistyWoods Daylight Test.unity, opened beside the current scene.
/// `--entry BuildDaylightTestScene.Render` saves fixed views (trail, top-down, tree, slope) as PNGs under Temp/LookDev.
/// </summary>
public static class BuildDaylightTestScene
{
    private const string Folder = "Assets/_Project/Scenes/Tests";
    private const string ScenePath = Folder + "/MistyWoods Daylight Test.unity";
    private const string DataPath = Folder + "/MistyWoods Daylight Terrain.asset";
    private const string MapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string RulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string SourceTerrain = "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset";
    private const int Seed = 4242;

    public static string Apply()
    {
        var map = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(MapPath);
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(RulesPath);
        VegetationPackConverter.EnsureFolder(Folder);
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.isLoaded) EditorSceneManager.CloseScene(existing, true);

        AssetDatabase.DeleteAsset(DataPath);
        AssetDatabase.CopyAsset(SourceTerrain, DataPath);
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        data.terrainLayers = map.Layers;

        RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, map.Size, Seed);
        RaidLayoutField field = RaidLayoutField.Build(plan, rules);
        data.SetHeights(0, 0, field.Heights(data.heightmapResolution, data.size.y));
        field.Zones(out byte[] path, out byte[] forest, out byte[] clearing);
        RaidMapDefinition working = Object.Instantiate(map);
        working.UseGenerated(field.Resolution, path, forest, clearing, plan.Mood, plan.Seed);
        System.Func<Vector2, float> wetness = spot => plan.Wetness(spot, rules.WetReach, rules.DampGround);
        RaidMapSurface.Paint(data, working, wetness);
        var clearings = plan.Sites.Select(site => new RaidMapSurface.Clearing(site.Position * plan.Size, working.ClearAroundSites, 3f)).ToList();
        int trees = RaidMapSurface.Scatter(data, working, clearings, out int others, wetness, plan.InWater);
        Object.DestroyImmediate(working);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            GameObject ground = Terrain.CreateTerrainGameObject(data);
            ground.name = "Terrain";
            var terrain = ground.GetComponent<Terrain>();
            terrain.materialTemplate = map.TerrainMaterial;
            terrain.drawInstanced = false;
            terrain.heightmapPixelError = 4;
            terrain.detailObjectDistance = 80f;
            terrain.treeDistance = 300f;
            terrain.treeBillboardDistance = 300f;
            var surface = ground.AddComponent<TerrainSurface>();
            surface.Profile = AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(Path.ChangeExtension(SourceTerrain, null) + " Surface Profile.asset");
            Vector3 origin = terrain.transform.position;
            TerrainGrowth.Fill(terrain, map.Growth, Seed, TerrainGrowth.UnderTrees(terrain), map.Plants,
                point => plan.Wetness(new Vector2(point.x - origin.x, point.z - origin.z), rules.WetReach, rules.DampGround));
            var colour = ground.AddComponent<GrassTerrainColour>();
            Texture2D colourMap = TerrainGrowth.BakeColour(terrain, map.Growth);
            AssetDatabase.DeleteAsset(Folder + "/MistyWoods Daylight Ground Colour.asset");
            AssetDatabase.CreateAsset(colourMap, Folder + "/MistyWoods Daylight Ground Colour.asset");
            colour.ColourMap = colourMap;
            surface.Apply();
            terrain.Flush();
            // Terrain edits through the API don't mark the asset; without this the saved trees, plants and paint are the old ones.
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            string skyPath = Folder + "/MistyWoods Daylight Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.3f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.66f);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/Rendering/Volumes/SafeHaven_Volume.asset");

            var wind = new GameObject("Forest Wind");
            wind.AddComponent<WindZone>().windMain = 0.5f;
            wind.AddComponent<MossWind>();

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 600f;
            camera.fieldOfView = 55f;
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            Place(camera.transform, terrain, plan, 0);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        return $"{ScenePath}: {plan.Mood.name}, {trees} trees and {others} undergrowth, rocks and logs, {data.detailPrototypes.Length} details";
    }

    /// <summary>
    /// Renders the look-dev views to Temp/LookDev/*.png with the test scene open alone (another open scene would draw its own
    /// trees and light into the shots), then reopens what was open. Refuses when an open scene has unsaved changes.
    /// </summary>
    public static string Render()
    {
        var reopen = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty) return "an open scene has unsaved changes";
            if (!string.IsNullOrEmpty(open.path) && open.path != ScenePath) reopen.Add(open.path);
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        try
        {
            Terrain terrain = scene.GetRootGameObjects().Select(r => r.GetComponent<Terrain>()).First(t => t != null);
            Camera camera = scene.GetRootGameObjects().Select(r => r.GetComponent<Camera>()).First(c => c != null);
            terrain.GetComponent<TerrainSurface>().Apply();
            var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(RulesPath);
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, terrain.terrainData.size.x, Seed);
            string folder = Path.GetFullPath("Temp/LookDev");
            Directory.CreateDirectory(folder);
            var shots = new List<string>();
            for (int view = 0; view < 5; view++)
            {
                Place(camera.transform, terrain, plan, view);
                string file = Path.Combine(folder, $"view{view}.png");
                Shoot(camera, file);
                shots.Add(file);
            }

            return string.Join("\n", shots);
        }
        finally
        {
            for (int i = 0; i < reopen.Count; i++)
            {
                EditorSceneManager.OpenScene(reopen[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
        }
    }

    // 0 along a trail at eye height, 1 the game's top-down view, 2 a close tree, 3 the steepest slope near the trail.
    private static void Place(Transform camera, Terrain terrain, RaidLayoutPlan plan, int view)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 Ground(Vector2 p) => RaidMapSurface.Ground(terrain, origin + new Vector3(p.x, 0f, p.y));
        List<Vector2> trail = plan.Trails.OrderByDescending(t => t.Count).First();
        Vector2 a = trail[trail.Count / 3], b = trail[Mathf.Min(trail.Count - 1, trail.Count / 3 + 4)];
        Vector3 at = Ground(a), ahead = Ground(b);
        Vector3 along = (ahead - at).normalized;
        switch (view)
        {
            case 0:
                // Back from the trail's middle and a little up, looking along it: the forest as you walk through it.
                camera.position = at - along * 3f + Vector3.up * 2.2f;
                camera.rotation = Quaternion.LookRotation(ahead + Vector3.up * 1f - camera.position);
                break;
            case 1:
                camera.position = at + new Vector3(0f, 17f, -11f);
                camera.rotation = Quaternion.LookRotation(at - camera.position);
                break;
            case 2:
            {
                TerrainData data = terrain.terrainData;
                TreeInstance tree = data.treeInstances.Where(t => data.treePrototypes[t.prototypeIndex].prefab != null
                        && data.treePrototypes[t.prototypeIndex].prefab.GetComponent<ChoppableTree>() != null
                        && data.treePrototypes[t.prototypeIndex].prefab.name.StartsWith("Cedar"))
                    .OrderBy(t => (Vector3.Scale(t.position, data.size) + origin - at).sqrMagnitude).First();
                Vector3 foot = Vector3.Scale(tree.position, data.size) + origin;
                Vector3 side = Vector3.Cross(Vector3.up, (foot - at).normalized);
                camera.position = Ground(new Vector2(foot.x - origin.x, foot.z - origin.z) + new Vector2(side.x, side.z) * 11f) + Vector3.up * 2f;
                camera.rotation = Quaternion.LookRotation(foot + Vector3.up * 7f - camera.position);
                break;
            }
            case 4:
                // Low over the trail's edge: displacement, tiling and how path and forest floor meet.
                camera.position = at + Vector3.Cross(Vector3.up, along) * 2.5f + Vector3.up * 1.4f;
                camera.rotation = Quaternion.LookRotation(at + along * 3f - camera.position);
                break;
            default:
            {
                TerrainData data = terrain.terrainData;
                Vector2 steepest = a;
                float most = 0f;
                for (int i = 0; i < 400; i++)
                {
                    Vector2 p = a + new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) * (4f + i * 0.1f);
                    float s = data.GetSteepness(p.x / data.size.x, p.y / data.size.z);
                    if (s > most) { most = s; steepest = p; }
                }

                Vector3 slope = Ground(steepest);
                Vector3 normal = data.GetInterpolatedNormal(steepest.x / data.size.x, steepest.y / data.size.z);
                Vector3 back = new Vector3(normal.x, 0f, normal.z).normalized;
                camera.position = slope + back * 7f + Vector3.up * 3.5f;
                camera.rotation = Quaternion.LookRotation(slope - camera.position);
                break;
            }
        }
    }

    private static void Shoot(Camera camera, string file)
    {
        var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        // Twice: the first frame warms streaming, tessellation and shadow caches.
        camera.Render();
        camera.Render();
        camera.targetTexture = previous;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(file, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        RenderTexture.ReleaseTemporary(target);
    }
}
