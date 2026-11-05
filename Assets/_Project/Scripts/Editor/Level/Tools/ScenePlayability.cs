using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Makes the open scene playable as a Safe Haven (home) scene or a raid, and checks what is missing
/// (Safe Haven › Scene › Make Playable, and World Tools › Scene).
/// <para>
/// Make Playable copies what every gameplay scene needs from the real scene of that kind (SafeHaven.unity or
/// Raid_MistyWoods.unity), so it is always the current setup: the player rig (player, cameras, HUD and windows, game
/// state, sun, sky, weather and fog, kept together so their references hold), the NavMesh surface, save keys and scene
/// audio; for a home also its wind, persistence, raid portal and wake-up point; for a raid its director (with a
/// raid to test with) and an exit. It stands the player on the ground (at an arrival point, or the middle of the Scene
/// view), bakes the NavMesh and adds the scene to Build Settings. Everything is one undo step; save the scene to keep it.
/// </para>
/// </summary>
public static class ScenePlayability
{
    public enum Kind
    {
        SafeHaven = 0,
        Raid = 1,
    }

    public enum Severity
    {
        Error = 0,
        Warning = 1,
        Info = 2,
    }

    public sealed class Issue
    {
        public Severity Severity;
        public string Message;
        public GameObject Target;
        public string FixLabel;
        public Action Fix;
    }

    private const string HavenTemplate = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string RaidTemplate = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string ExitPrefab = "Assets/_Project/Prefabs/Raids/ExtractionPoint.prefab";

    /// <summary>The player rig: copied together, so the references between its parts (camera follow, HUD, sun) hold.</summary>
    private static readonly string[] Rig =
    {
        "Directional Light", "Global Volume", "Main Camera", "Player Cinemachine Camera", "PlayerArmature", "GameStateManager",
        "UI Root", "Sky and Weather", "Atmosphere",
    };

    /// <summary>Parts that stand alone, copied when the scene has none, with how to tell whether it has one.</summary>
    private static readonly (string Root, Type Marker, Kind? Only)[] Singles =
    {
        ("NavMesh", typeof(NavMeshSurface), null),
        ("SaveHotkeys", typeof(SaveHotkeys), null),
        ("Scene Audio", typeof(SceneAudio), null),
        ("Forest Wind", typeof(MossWind), Kind.SafeHaven),
        ("Persistent Scene", typeof(PersistentScene), Kind.SafeHaven),
        ("Portal", typeof(RaidPortal), Kind.SafeHaven),
        ("Raid", typeof(RaidDirector), Kind.Raid),
    };

    [MenuItem("Safe Haven/Scene/Make Playable as Safe Haven", false, 0)]
    private static void MakeHavenMenu() => Report(MakePlayable(SceneManager.GetActiveScene(), Kind.SafeHaven));

    [MenuItem("Safe Haven/Scene/Make Playable as Raid", false, 1)]
    private static void MakeRaidMenu() => Report(MakePlayable(SceneManager.GetActiveScene(), Kind.Raid));

    private static void Report(string message)
    {
        Debug.Log("Make Playable: " + message);
        WorldToolsWindow.Open(WorldToolsWindow.Tab.Scene);
    }

    /// <summary>The kind the scene already is (a raid director makes it a raid), or null.</summary>
    public static Kind? Detect(Scene scene)
    {
        if (Find<RaidDirector>(scene).Any())
        {
            return Kind.Raid;
        }

        if (Find<PersistentScene>(scene).Any() || Find<HomePortal>(scene).Any() || Find<RaidPortal>(scene).Any())
        {
            return Kind.SafeHaven;
        }

        return null;
    }

    // ------------------------------------------------------------------ checking

    /// <summary>What keeps the scene from playing as the kind (errors), what will work badly (warnings) and what is
    /// worth knowing (info), each with a fix where there is one.</summary>
    public static List<Issue> Check(Scene scene, Kind kind)
    {
        var issues = new List<Issue>();
        void Add(Severity severity, string message, GameObject target = null, string fixLabel = null, Action fix = null) =>
            issues.Add(new Issue { Severity = severity, Message = message, Target = target, FixLabel = fixLabel, Fix = fix });
        Action make = () => MakePlayable(scene, kind);

        List<Player> players = Find<Player>(scene).ToList();
        if (players.Count == 0)
        {
            Add(Severity.Error, "No player.", null, "Make Playable", make);
        }
        else if (players.Count > 1)
        {
            Add(Severity.Error, $"{players.Count} players: there should be one.", players[1].gameObject);
        }

        if (!Find<CinemachineBrain>(scene).Any(b => b.CompareTag("MainCamera")))
        {
            Add(Severity.Error, "No game camera (a Main Camera with a Cinemachine Brain).", null, "Make Playable", make);
        }

        if (Find<AudioListener>(scene).Count() > 1)
        {
            Add(Severity.Warning, "More than one Audio Listener: Unity will complain and sound comes from one at random.",
                Find<AudioListener>(scene).Skip(1).First().gameObject);
        }

        if (!Find<GameStateManager>(scene).Any())
        {
            Add(Severity.Error, "No Game State Manager (pausing, windows and the game's state).", null, "Make Playable", make);
        }

        if (!Find<UIStateManager>(scene).Any())
        {
            Add(Severity.Error, "No UI Root: no HUD, inventory or menus.", null, "Make Playable", make);
        }

        if (Find<Light>(scene).Count(l => l.type == LightType.Directional && l.isActiveAndEnabled) > 1)
        {
            Add(Severity.Warning, "More than one sun (directional light): shadows and the day cycle use only one.");
        }

        if (!Find<DayNightCycle>(scene).Any())
        {
            Add(Severity.Warning, "No day, night and weather rig: the light never changes.", null, "Make Playable", make);
        }

        if (!Find<FogGround>(scene).Any())
        {
            Add(Severity.Info, "No atmosphere (fog, fog of war).", null, "Make Playable", make);
        }

        Player player = players.FirstOrDefault();
        if (player != null && !GroundUnder(player.transform.position, player.transform, out _))
        {
            Add(Severity.Error, "Nothing to stand on under the player: they would fall.", player.gameObject, "Stand on Ground",
                () => StandOnGround(player.transform, player.transform.position));
        }

        List<NavMeshSurface> surfaces = Find<NavMeshSurface>(scene).ToList();
        if (surfaces.Count == 0)
        {
            Add(Severity.Error, "No NavMesh Surface: monsters and residents cannot walk.", null, "Make Playable", make);
        }
        else if (surfaces.Any(s => s.navMeshData == null))
        {
            Add(Severity.Error, "The NavMesh is not baked.", surfaces[0].gameObject, "Bake NavMesh", () => Bake(surfaces));
        }
        else if (player != null && !NavMesh.SamplePosition(player.transform.position, out _, 3f, NavMesh.AllAreas))
        {
            Add(Severity.Warning, "The player starts off the NavMesh (rebake after changing the ground).", player.gameObject,
                "Bake NavMesh", () => Bake(surfaces));
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            Add(Severity.Error, "The scene is not saved yet: save it before making it playable.");
        }
        else if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled))
        {
            Add(Severity.Warning, "Not in Build Settings: the game cannot load it (portals, raids, scene changes).", null,
                "Add to Build Settings", () => AddToBuild(scene));
        }

        if (kind == Kind.Raid)
        {
            CheckRaid(scene, Add, make);
        }
        else
        {
            CheckHaven(scene, Add, make);
        }

        int levelProblems = LevelCheck.Run(scene).Count;
        if (levelProblems > 0)
        {
            Add(Severity.Warning, $"Level Check finds {levelProblems} problem(s) (floating, buried, unreachable objects...).", null,
                "Open Level Check", LevelCheckWindow.Open);
        }

        return issues.OrderBy(i => i.Severity).ToList();
    }

    private static void CheckRaid(Scene scene, Action<Severity, string, GameObject, string, Action> add, Action make)
    {
        RaidDirector director = Find<RaidDirector>(scene).FirstOrDefault();
        if (director == null)
        {
            add(Severity.Error, "No Raid Director: the raid never starts or ends.", null, "Make Playable", make);
        }
        else if (new SerializedObject(director).FindProperty("fallbackRaid").objectReferenceValue == null)
        {
            add(Severity.Error, "The Raid Director has no raid to run when the scene is played directly.", director.gameObject,
                "Make a Raid for This Scene", () => CreateRaid(scene, director));
        }

        if (!Find<RaidGenerator>(scene).Any())
        {
            add(Severity.Warning, "No Raid Generator: spawn groups never fill and every exit is always open.", null, "Add a Raid Layout",
                () => AddLayout(scene));
        }

        if (!Find<ExtractionPoint>(scene).Any())
        {
            add(Severity.Error, "No extraction point: the player can never get home.", null, "Add an Exit", () => AddExit(scene));
        }

        if (!Find<SpawnGroup>(scene).Any())
        {
            add(Severity.Warning, "No spawn groups: no monsters, resources or caches appear (add them from the Content Editor's raid pages).", null, null, null);
        }

        string name = Path.GetFileNameWithoutExtension(scene.path);
        if (!string.IsNullOrEmpty(name) && !AllRaids().Any(r => r.SceneName == name))
        {
            add(Severity.Warning, "No raid uses this scene: a portal can't send the player here.", null, "Make a Raid for This Scene",
                () => CreateRaid(scene, director));
        }

        if (!Find<RaidMap>(scene).Any())
        {
            add(Severity.Info, "No Raid Map: the layout does not turn between raids (fine for a hand-made map).", null, null, null);
        }
    }

    private static void CheckHaven(Scene scene, Action<Severity, string, GameObject, string, Action> add, Action make)
    {
        if (!Find<PersistentScene>(scene).Any())
        {
            add(Severity.Warning, "No Persistent Scene: buildings and changes are not kept when the player leaves.", null, "Make Playable", make);
        }

        if (!Find<PlayerArrivalPoint>(scene).Any(p => p.Id == PlayerArrivalPoint.HomePortal))
        {
            add(Severity.Warning, "No wake-up point (Player Arrival Point \"HomePortal\"): waking and reviving keep the player wherever they were.",
                null, "Add at the Player", () => AddArrival(scene, PlayerArrivalPoint.HomePortal));
        }

        if (!Find<RaidPortal>(scene).Any())
        {
            add(Severity.Warning, "No raid portal: the player cannot go raiding from here.", null, "Make Playable", make);
        }

        if (!Find<HomePortal>(scene).Any())
        {
            add(Severity.Info, "No Portal: no ward, home revival or building area.", null, null, null);
        }
    }

    // ------------------------------------------------------------------ making playable

    /// <summary>Adds what the scene needs to play as the kind (see the class summary); returns what it did.</summary>
    public static string MakePlayable(Scene scene, Kind kind)
    {
        if (EditorApplication.isPlaying)
        {
            return "stop playing first";
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            return "save the scene first (File › Save As), then make it playable";
        }

        var done = new List<string>();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Make Playable");
        GroundStick.Suspend(2.0);
        bool needsRig = !Find<Player>(scene).Any() || !Find<CinemachineBrain>(scene).Any();
        List<string> roots = needsRig ? Rig.ToList() : new List<string>();
        roots.AddRange(Singles.Where(s => (s.Only == null || s.Only == kind) && !Find(scene, s.Marker).Any()).Select(s => s.Root));
        if (roots.Count > 0)
        {
            done.Add(CopyFromTemplate(scene, kind == Kind.Raid ? RaidTemplate : HavenTemplate, roots, needsRig));
        }

        Player player = Find<Player>(scene).FirstOrDefault();
        if (player != null && needsRig)
        {
            // A home wakes the player at the portal (not at the portal, which comes along from the template).
            PlayerArrivalPoint arrival = Find<PlayerArrivalPoint>(scene)
                .FirstOrDefault(p => kind == Kind.Raid || p.Id == PlayerArrivalPoint.HomePortal);
            Vector3 start = arrival != null ? arrival.transform.position : SceneCentre();
            StandOnGround(player.transform, start);
            done.Add($"player at {player.transform.position:F1}");
        }

        if (player != null)
        {
            // Copied pieces of the home or the raid stand where they stood in the template: bring them near the player.
            foreach (string root in new[] { "Portal", "Raid" }.Where(roots.Contains))
            {
                GameObject moved = SceneFolders.Find(scene, root);
                if (moved != null)
                {
                    Vector3 spot = player.transform.position + player.transform.forward * 10f + player.transform.right * 4f;
                    StandOnGround(moved.transform, spot);
                    foreach (Transform part in moved.transform)
                    {
                        if (GroundStick.IsGroundObject(part.gameObject))
                        {
                            GroundPlacement.Drop(part, false); // the portal, its notice board: each on the ground here
                        }
                    }
                }
            }
        }

        if (kind == Kind.SafeHaven && !Find<PlayerArrivalPoint>(scene).Any(p => p.Id == PlayerArrivalPoint.HomePortal))
        {
            done.Add(AddArrival(scene, PlayerArrivalPoint.HomePortal));
        }

        if (kind == Kind.Raid)
        {
            RaidDirector director = Find<RaidDirector>(scene).FirstOrDefault();
            if (director != null && new SerializedObject(director).FindProperty("fallbackRaid").objectReferenceValue == null)
            {
                done.Add(CreateRaid(scene, director));
            }

            if (!Find<ExtractionPoint>(scene).Any())
            {
                done.Add(AddExit(scene));
            }

            if (!Find<RaidGenerator>(scene).Any())
            {
                done.Add(AddLayout(scene));
            }
        }

        AtmosphereSetup.EnsureSceneDefaults(scene);
        SaveableEntityTools.AssignSceneIds(scene);
        SceneFolders.Tidy(scene);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        done.Add(AddToBuild(scene));
        List<NavMeshSurface> surfaces = Find<NavMeshSurface>(scene).ToList();
        if (surfaces.Count > 0)
        {
            Bake(surfaces);
            done.Add("baking the NavMesh");
        }

        return string.Join("; ", done.Where(d => !string.IsNullOrEmpty(d))) + ". Save the scene to keep it.";
    }

    /// <summary>Copies root objects from the template scene (opened alongside, then closed unchanged). With the rig,
    /// the scene's own cameras, suns, global volumes and players are replaced and its sky and fog settings copied.</summary>
    private static string CopyFromTemplate(Scene scene, string templatePath, List<string> roots, bool rig)
    {
        Scene template = EditorSceneManager.OpenScene(templatePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] originals = SceneFolders.TopLevel(template).Where(r => roots.Contains(r.name)).ToArray();
            if (rig)
            {
                EditorSceneManager.SetActiveScene(template);
                var sky = new SkySettings();
                EditorSceneManager.SetActiveScene(scene);
                foreach (GameObject old in SceneFolders.TopLevel(scene).Where(r =>
                             r.GetComponent<Camera>() != null || r.GetComponent<Player>() != null || r.GetComponent<CinemachineCamera>() != null ||
                             r.TryGetComponent(out Light l) && l.type == LightType.Directional ||
                             r.TryGetComponent(out Volume v) && v.isGlobal).ToArray())
                {
                    Undo.DestroyObjectImmediate(old);
                }

                sky.Apply();
            }

            Selection.objects = originals;
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            GameObject[] copies = Selection.gameObjects;
            foreach (GameObject copy in copies)
            {
                GameObject original = originals.FirstOrDefault(o => copy.name.StartsWith(o.name, StringComparison.Ordinal));
                if (original != null)
                {
                    copy.name = original.name; // drop the " (1)"
                }

                copy.transform.SetParent(null, true); // out of the template's folder, so it can change scenes
                SceneManager.MoveGameObjectToScene(copy, scene);
                Undo.RegisterCreatedObjectUndo(copy, "Make Playable");
                SceneFolders.Place(copy);
            }

            EditorSceneManager.SetActiveScene(scene);
            if (rig)
            {
                GameObject sun = copies.FirstOrDefault(c => c.name == "Directional Light");
                RenderSettings.sun = sun != null ? sun.GetComponent<Light>() : null;
                if (Camera.main != null)
                {
                    Camera.main.farClipPlane = Mathf.Max(Camera.main.farClipPlane, 600f);
                }
            }

            Selection.activeGameObject = copies.FirstOrDefault(c => c.GetComponent<Player>() != null) ?? copies.FirstOrDefault();
            return "copied " + string.Join(", ", copies.Select(c => c.name)) + " from " + Path.GetFileNameWithoutExtension(templatePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(template, true);
        }
    }

    /// <summary>The sky, ambient light and fog of the scene active when made (scene settings, not objects).</summary>
    private sealed class SkySettings
    {
        private readonly Material skybox = RenderSettings.skybox;
        private readonly AmbientMode ambientMode = RenderSettings.ambientMode;
        private readonly Color ambientSky = RenderSettings.ambientSkyColor;
        private readonly Color ambientEquator = RenderSettings.ambientEquatorColor;
        private readonly Color ambientGround = RenderSettings.ambientGroundColor;
        private readonly float ambientIntensity = RenderSettings.ambientIntensity;
        private readonly bool fog = RenderSettings.fog;
        private readonly FogMode fogMode = RenderSettings.fogMode;
        private readonly Color fogColor = RenderSettings.fogColor;
        private readonly float fogDensity = RenderSettings.fogDensity;
        private readonly float fogStart = RenderSettings.fogStartDistance;
        private readonly float fogEnd = RenderSettings.fogEndDistance;

        public void Apply()
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }

    // ------------------------------------------------------------------ fixes

    /// <summary>Moves an object (and, for the player, its cameras) onto the ground at a point.</summary>
    public static void StandOnGround(Transform target, Vector3 at)
    {
        if (!GroundUnder(at, target, out Vector3 ground))
        {
            return;
        }

        Undo.RecordObject(target, "Stand on Ground");
        Vector3 offset = ground + Vector3.up * 0.1f - target.position;
        target.position += offset;
        if (target.GetComponent<Player>() != null)
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>())
            {
                Undo.RecordObject(camera.transform, "Stand on Ground");
                camera.transform.position += offset;
            }

            foreach (CinemachineCamera camera in Object.FindObjectsByType<CinemachineCamera>())
            {
                Undo.RecordObject(camera.transform, "Stand on Ground");
                camera.transform.position += offset;
            }
        }
    }

    /// <summary>The ground under a point, looking down from well above it (ignoring <paramref name="ignore"/>, such as
    /// the player's own collider).</summary>
    public static bool GroundUnder(Vector3 at, Transform ignore, out Vector3 ground) =>
        GroundPlacement.TryGround(at, out ground, out _, ignore);

    private static Vector3 SceneCentre()
    {
        SceneView view = SceneView.lastActiveSceneView;
        return view != null ? view.pivot : Vector3.zero;
    }

    private static void Bake(List<NavMeshSurface> surfaces) =>
        NavMeshAssetManager.instance.StartBakingSurfaces(surfaces.Cast<Object>().ToArray());

    private static string AddToBuild(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path) || EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled))
        {
            return null;
        }

        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != scene.path).ToList();
        scenes.Add(new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        return "added to Build Settings";
    }

    private static string AddArrival(Scene scene, string id)
    {
        Player player = Find<Player>(scene).FirstOrDefault();
        var point = new GameObject("Arrival: " + id);
        SceneManager.MoveGameObjectToScene(point, scene);
        Undo.RegisterCreatedObjectUndo(point, "Add Arrival Point");
        var arrival = point.AddComponent<PlayerArrivalPoint>();
        var so = new SerializedObject(arrival);
        so.FindProperty("id").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (player != null)
        {
            point.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
        }

        return $"added the {id} arrival point";
    }

    private static string AddExit(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefab);
        if (prefab == null)
        {
            return "no extraction point prefab at " + ExitPrefab;
        }

        GroundStick.Suspend();
        Player player = Find<Player>(scene).FirstOrDefault();
        Vector3 at = player != null ? player.transform.position + player.transform.forward * 30f : SceneCentre();
        var exit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(exit, "Add Exit");
        RaidMap map = Find<RaidMap>(scene).FirstOrDefault();
        if (map != null && map.Content != null)
        {
            exit.transform.SetParent(map.Content, true); // turns with the map
        }

        exit.transform.position = at;
        GroundPlacement.Drop(exit.transform, false);
        Selection.activeGameObject = exit;
        return "added an extraction point 30 m ahead of the player (move it where it belongs)";
    }

    /// <summary>A "Raid Layout" with the Raid Generator, which fills the spawn groups put under it (in a raid map, under
    /// its Map Content, so it turns with the map).</summary>
    private static string AddLayout(Scene scene)
    {
        var layout = new GameObject("Raid Layout");
        SceneManager.MoveGameObjectToScene(layout, scene);
        Undo.RegisterCreatedObjectUndo(layout, "Add Raid Layout");
        RaidMap map = Find<RaidMap>(scene).FirstOrDefault();
        if (map != null && map.Content != null)
        {
            layout.transform.SetParent(map.Content, false);
        }

        layout.AddComponent<RaidGenerator>();
        return "added a Raid Layout (put spawn groups under it; its Raid Generator fills them each raid)";
    }

    /// <summary>A raid for this scene (a copy of the director's raid, or the first raid, pointed at this scene), set as
    /// the director's raid for playing the scene directly.</summary>
    private static string CreateRaid(Scene scene, RaidDirector director)
    {
        string name = Path.GetFileNameWithoutExtension(scene.path);
        RaidDefinition existing = AllRaids().FirstOrDefault(r => r.SceneName == name);
        RaidDefinition raid = existing;
        string result;
        if (raid == null)
        {
            RaidDefinition source = director != null
                ? new SerializedObject(director).FindProperty("fallbackRaid").objectReferenceValue as RaidDefinition
                : null;
            source ??= AllRaids().FirstOrDefault();
            if (source == null)
            {
                return "no raid to copy: make one in the Content Editor first";
            }

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
            raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(path);
            var so = new SerializedObject(raid);
            so.FindProperty("sceneName").stringValue = name;
            so.FindProperty("displayName").stringValue = ObjectNames.NicifyVariableName(name.Replace("Raid_", ""));
            so.FindProperty("id").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            result = $"made the raid {path} (a copy of {source.name}: adjust it in the Content Editor)";
        }
        else
        {
            result = $"using the raid {existing.name}";
        }

        if (director != null)
        {
            var so = new SerializedObject(director);
            so.FindProperty("fallbackRaid").objectReferenceValue = raid;
            so.ApplyModifiedProperties();
        }

        return result;
    }

    // ------------------------------------------------------------------ helpers

    private static IEnumerable<RaidDefinition> AllRaids() =>
        AssetDatabase.FindAssets("t:RaidDefinition").Select(g => AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                     .Where(r => r != null);

    private static IEnumerable<T> Find<T>(Scene scene) where T : Component => Find(scene, typeof(T)).Cast<T>();

    private static IEnumerable<Component> Find(Scene scene, Type type) =>
        scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true))
            : Enumerable.Empty<Component>();
}
