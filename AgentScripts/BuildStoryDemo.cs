using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds Scenes/Demo/StoryDemo.unity (`unity command run_script --file AgentScripts/BuildStoryDemo.cs --entry BuildStoryDemo.Build`):
/// a grassy playable test field with the two example stories as Story Scripts.
/// <list type="bullet">
/// <item>"At least it's not raining": walking north, the player jokes; the rain comes; the ground shakes, a troll roars
/// on the ridge and the camera turns to it; "I better shut up now. Run!"; combat music.</item>
/// <item>"The Waking Stone": using the stone starts a quest; it glows and hums; the grass around it dies; the player
/// wonders aloud; wolves come out of it; when they are dead the stone goes dark and can't be used again, and the quest
/// completes with its reward.</item>
/// </list>
/// Re-running rebuilds the scene from scratch.
/// </summary>
public static class BuildStoryDemo
{
    private const string Folder = "Assets/_Project/Scenes/Demo";
    private const string ScenePath = Folder + "/StoryDemo.unity";
    private const string DataFolder = "Assets/_Project/Data/Quests/StoryDemo";

    public static string Build()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Terrain terrain = Ground();
        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneView.lastActiveSceneView.pivot = Vector3.zero;
        string made = ScenePlayability.MakePlayable(scene, ScenePlayability.Kind.Raid);
        Player player = Object.FindAnyObjectByType<Player>();
        player.transform.position = new Vector3(0f, terrain.SampleHeight(Vector3.zero), -10f);

        string rain = RainStory(terrain);
        string stone = StoneStory(terrain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{made}\n{rain}\n{stone}";
    }

    // A 160 m field with a ridge to the north for the troll, and grass.
    private static Terrain Ground()
    {
        var data = new TerrainData { heightmapResolution = 129 };
        data.size = new Vector3(160f, 30f, 160f);
        int n = data.heightmapResolution;
        var heights = new float[n, n];
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                float north = z / (float)(n - 1);
                float ridge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.86f, north)) * 0.3f;
                heights[z, x] = ridge + Mathf.PerlinNoise(x * 0.07f, z * 0.07f) * 0.012f;
            }
        }

        data.SetHeights(0, 0, heights);
        AssetDatabase.DeleteAsset(Folder + "/StoryDemo Terrain.asset");
        AssetDatabase.CreateAsset(data, Folder + "/StoryDemo Terrain.asset");
        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.transform.position = new Vector3(-80f, 0f, -80f);
        var terrain = go.GetComponent<Terrain>();

        List<GameObject> grass = GrassSetup.EnsureAssets().Where(p => p.name.Contains("Grass")).Take(2).ToList();
        GrassSetup.AddTo(terrain, grass);
        data.SetDetailResolution(256, 16);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var map = new int[256, 256];
            for (int z = 0; z < 256; z++)
            {
                for (int x = 0; x < 256; x++)
                {
                    map[z, x] = layer == 0 ? 8 : 3;
                }
            }

            data.SetDetailLayer(0, 0, layer, map);
        }

        GrassSetup.SyncDrawDistance(terrain, grass);
        return terrain;
    }

    private static string RainStory(Terrain terrain)
    {
        Vector3 ridge = OnGround(terrain, new Vector3(0f, 0f, 52f));
        var troll = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/OldMossTroll.prefab"));
        troll.transform.SetPositionAndRotation(ridge, Quaternion.Euler(0f, 180f, 0f));
        troll.name = "Troll on the Ridge";
        // Only to be seen: it doesn't come down.
        foreach (Behaviour part in troll.GetComponentsInChildren<Behaviour>())
        {
            if (part is MonsterAI || part is UnityEngine.AI.NavMeshAgent)
            {
                part.enabled = false;
            }
        }

        GameObject story = Story("Story - At least it's not raining", new Vector3(0f, 0f, 2f));
        Transform place = Marker("Where the joke is made", terrain, new Vector3(0f, 0f, 6f));
        var script = story.GetComponent<StoryScript>();
        var start = new WhenPlayerNear { place = place, radius = 5f };
        var joke = Line(true, "At least it's not raining.");
        var rain = new ChangeWeatherNode { weather = AssetDatabase.LoadAssetAtPath<WeatherDefinition>("Assets/_Project/Data/Environment/Weather/Rain.asset"), transitionSeconds = 3f };
        var pause = new WaitNode { seconds = 2.5f };
        var worse = Line(true, "Oh, well. At least it can't get worse.");
        var shake = new ShakeCameraNode { strength = 0.8f };
        var roar = new PlaySoundNode { sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/TrollStep.asset"), at = troll.transform };
        var look = new FocusCameraNode { target = troll.transform, seconds = 3f };
        var run = Line(true, "I better shut up now. Run!");
        var music = new MusicNode { change = MusicNode.Change.Play };
        music.playlist.tracks.Add(new MusicTrack(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Placeholder/Music_Combat.wav"), 0.8f));
        Chain(script, start, joke, rain, pause, worse, shake, look, run, music);
        Link(script, shake, roar);
        Place(roar, shake, 1);
        return $"rain story: {script.Nodes.Count} nodes";
    }

    private static string StoneStory(Terrain terrain)
    {
        AssetDatabase.CreateFolder("Assets/_Project/Data/Quests", "StoryDemo");
        var silenced = Asset<GameEventDefinition>(DataFolder + "/StoneSilenced.asset", so =>
        {
            so.FindProperty("displayName").stringValue = "The stone fell silent";
            so.FindProperty("objectiveText").stringValue = "Silence the waking stone";
        });
        var quest = Asset<QuestDefinition>(DataFolder + "/TheWakingStone.asset", so =>
        {
            so.FindProperty("id").stringValue = "story_demo_waking_stone";
            so.FindProperty("displayName").stringValue = "The Waking Stone";
            so.FindProperty("description").stringValue = "A standing stone hums with something old. Whatever is inside wants out.";
            so.FindProperty("chain").stringValue = "Story Demo";
            so.FindProperty("start").intValue = (int)QuestStart.ByQuestGiver;
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.ClearArray();
            objectives.InsertArrayElementAtIndex(0);
            SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
            objective.FindPropertyRelative("type").intValue = (int)ObjectiveType.QuestEvent;
            objective.FindPropertyRelative("amount").intValue = 1;
            objective.FindPropertyRelative("questEvent").objectReferenceValue = silenced;
            SerializedProperty rewards = so.FindProperty("rewardItems");
            rewards.ClearArray();
            rewards.InsertArrayElementAtIndex(0);
            rewards.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Bone.asset");
            rewards.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 3;
        });

        Vector3 at = OnGround(terrain, new Vector3(14f, 0f, 8f));
        var stone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Environment/World/Lichen Boulder.prefab"));
        stone.name = "The Waking Stone";
        stone.transform.position = at;
        var use = stone.AddComponent<Interactable>();
        var useData = new SerializedObject(use);
        useData.FindProperty("duration").floatValue = 1f;
        useData.FindProperty("range").floatValue = 2.5f;
        useData.FindProperty("deactivateOnComplete").boolValue = false;
        useData.ApplyModifiedPropertiesWithoutUndo();

        var zonePrefab = AssetDatabase.LoadAssetAtPath<MobSpawnZone>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab");
        var zone = (MobSpawnZone)PrefabUtility.InstantiatePrefab(zonePrefab);
        zone.name = "Out of the Stone";
        zone.transform.position = at;
        var zoneData = new SerializedObject(zone);
        SerializedProperty creatures = zoneData.FindProperty("creatures");
        creatures.ClearArray();
        creatures.InsertArrayElementAtIndex(0);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.FindAssets("ForestBittenWolf t:Prefab")
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).First(p => p.GetComponent<MonsterAI>() != null);
        creatures.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        zoneData.FindProperty("trigger").intValue = (int)MobSpawnTrigger.Manual;
        zoneData.FindProperty("count").vector2IntValue = new Vector2Int(3, 3);
        zoneData.ApplyModifiedPropertiesWithoutUndo();
        zone.SetCorners(new[] { new Vector3(-6f, 0f, -6f), new Vector3(6f, 0f, -6f), new Vector3(6f, 0f, 6f), new Vector3(-6f, 0f, 6f) });

        GameObject story = Story("Story - The Waking Stone", at + new Vector3(-3f, 0f, 0f));
        var script = story.GetComponent<StoryScript>();
        var used = new WhenUsed { target = use };
        var begin = new StartQuestNode { quest = quest };
        var glow = new GlowNode { target = stone.transform, glow = true, colour = new Color(0.45f, 1f, 0.6f), intensity = 6f, range = 8f };
        var hum = new PlaySoundNode { sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Spotted.asset"), at = stone.transform };
        var wither = new WitherGroundNode { centre = stone.transform, radius = 7f, seconds = 3f };
        var wonder = Line(true, "The grass... it's dying. What did I wake?");
        var wolves = new SpawnEnemiesNode { zone = zone, waitUntilDefeated = true };
        var dark = new GlowNode { target = stone.transform, glow = false, fadeSeconds = 2f };
        var spent = new SetUsableNode { target = use, usable = false };
        var done = new RaiseQuestEventNode { questEvent = silenced };
        Chain(script, used, begin, glow, wither, wonder, wolves, dark, spent, done);
        Link(script, glow, hum);
        Place(hum, glow, 1);
        return $"stone story: {script.Nodes.Count} nodes";
    }

    // ----- Helpers -----

    private static SayNode Line(bool player, string text) => new SayNode { speaker = new StorySpeaker { player = player }, line = text };

    private static void Chain(StoryScript script, params StoryNode[] steps)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            steps[i].position = new Vector2(i * 330f, (i % 2) * 40f);
            script.AddNode(steps[i]);
            if (i > 0)
            {
                script.Connect(steps[i - 1], 0, steps[i]);
            }
        }

        EditorUtility.SetDirty(script);
    }

    private static void Link(StoryScript script, StoryNode from, StoryNode to)
    {
        script.AddNode(to);
        script.Connect(from, 0, to);
        EditorUtility.SetDirty(script);
    }

    private static void Place(StoryNode node, StoryNode beside, int row) => node.position = beside.position + new Vector2(0f, 360f * row);

    private static GameObject Story(string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        go.AddComponent<SaveableEntity>();
        go.AddComponent<StoryScript>();
        return go;
    }

    private static Transform Marker(string name, Terrain terrain, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.position = OnGround(terrain, at);
        return go.transform;
    }

    private static Vector3 OnGround(Terrain terrain, Vector3 at)
    {
        at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
        return at;
    }

    private static T Asset<T>(string path, System.Action<SerializedObject> fill) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var so = new SerializedObject(asset);
        fill(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }
}
