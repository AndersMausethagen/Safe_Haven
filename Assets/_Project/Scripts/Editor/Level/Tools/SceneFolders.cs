using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// A scene's top level sorted into numbered folders, the convention GameplayTestScene started: 00 Systems, 01 Player &amp;
/// Cameras, 02 Lighting &amp; Atmosphere, 03 UI, 04 World, 05 Gameplay, 06 Story &amp; Events, 07 Ambience (Safe Haven ›
/// Scene › Tidy Hierarchy). Folders are plain empty objects at the origin, so moving an object into one changes nothing
/// in the game. Tools that look for a named object (World, Sky and Weather, Atmosphere, Level Props, Level Scatter,
/// the Make Playable pieces) find it at the top level or inside a folder.
/// </summary>
public static class SceneFolders
{
    public const string Systems = "00 Systems";
    public const string PlayerAndCameras = "01 Player & Cameras";
    public const string Lighting = "02 Lighting & Atmosphere";
    public const string UI = "03 UI";
    public const string World = "04 World";
    public const string Gameplay = "05 Gameplay";
    public const string Story = "06 Story & Events";
    public const string Ambience = "07 Ambience";

    public static readonly string[] Order = { Systems, PlayerAndCameras, Lighting, UI, World, Gameplay, Story, Ambience };

    // Scenes this small are already easy to read.
    private const int SmallScene = 5;
    private static readonly Regex FolderName = new Regex(@"^\d\d ");

    public static bool IsFolder(GameObject go) =>
        go != null && FolderName.IsMatch(go.name) && go.GetComponents<Component>().Length == 1 && go.transform.parent == null;

    /// <summary>The scene's top-level objects: its roots, and what sits directly inside its folders.</summary>
    public static IEnumerable<GameObject> TopLevel(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (IsFolder(root))
            {
                foreach (Transform child in root.transform)
                {
                    yield return child.gameObject;
                }
            }
            else
            {
                yield return root;
            }
        }
    }

    /// <summary>A top-level object by name, at the root or inside a folder.</summary>
    public static GameObject Find(Scene scene, string name) => TopLevel(scene).FirstOrDefault(g => g.name == name);

    /// <summary>True when the object is, or is inside, a top-level object of that name (with or without folders).</summary>
    public static bool IsUnder(Transform transform, string name)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.name == name && (t.parent == null || IsFolder(t.parent.gameObject)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The top-level object an object belongs to (its root, or the folder's child it is in).</summary>
    public static Transform TopOf(Transform transform)
    {
        Transform t = transform;
        while (t.parent != null && !IsFolder(t.parent.gameObject))
        {
            t = t.parent;
        }

        return t;
    }

    /// <summary>Puts a new top-level object where it belongs when the scene uses folders (otherwise it stays at the root).</summary>
    public static void Place(GameObject go)
    {
        Scene scene = go.scene;
        if (go.transform.parent != null || !scene.GetRootGameObjects().Any(IsFolder))
        {
            return;
        }

        Transform folder = Folder(scene, CategoryOf(go), true);
        Undo.SetTransformParent(go.transform, folder, "Place in Folder");
    }

    public static Transform Folder(Scene scene, string name, bool create)
    {
        GameObject found = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name && IsFolder(r));
        if (found != null || !create)
        {
            return found != null ? found.transform : null;
        }

        var folder = new GameObject(name);
        SceneManager.MoveGameObjectToScene(folder, scene);
        Undo.RegisterCreatedObjectUndo(folder, "Create Folder");
        return folder.transform;
    }

    /// <summary>Which folder an object belongs in, from what it is.</summary>
    public static string CategoryOf(GameObject go)
    {
        string name = go.name;
        bool Has<T>() where T : Component => go.GetComponentInChildren<T>(true) != null;
        bool Own<T>() where T : Component => go.GetComponent<T>() != null;

        if (Own<Player>() || Own<PlayerStateMachine>() || Own<Camera>() || Own<CinemachineCamera>() || Own<CinemachineBrain>() ||
            name == "PlayerArmature" || name == "Player")
        {
            return PlayerAndCameras;
        }

        if (name == WeatherSetup.RigName || name == "Atmosphere" || Own<Light>() || Own<Volume>() || Own<WindZone>() || Own<MossWind>() ||
            Own<FogGround>() || Own<FogArea>() || Own<ReflectionProbe>())
        {
            return Lighting;
        }

        if (Own<Canvas>() || Own<UIStateManager>() || Own<EventSystem>() || Own<UIWindow>())
        {
            return UI;
        }

        // A NavMesh that bakes only its children holds the level itself (the Prologue's terrains): it is the world.
        if (go.TryGetComponent(out Unity.AI.Navigation.NavMeshSurface surface) && surface.collectObjects == Unity.AI.Navigation.CollectObjects.Children &&
            go.transform.childCount > 0)
        {
            return World;
        }

        // The Prologue's encounters, routes and moments, kept together.
        if (name == "Prologue Meadow")
        {
            return Gameplay;
        }

        if (Own<GameStateManager>() || Own<SaveHotkeys>() || Own<PersistentScene>() || Own<SceneAudio>() ||
            Own<Unity.AI.Navigation.NavMeshSurface>() || Own<ViewBounds>() || name == "VisualScripting SceneVariables")
        {
            return Systems;
        }

        if (Own<Fireflies>() || Own<Birds>() || Own<CanopyDebris>() || Own<AmbientOneShots>() || Own<ParticleSystem>() && !Has<Interactable>())
        {
            return Ambience;
        }

        if (Has<StoryScript>() && !Has<Terrain>() || (Own<GameEventListener>() || Own<GameEventTrigger>()) && !Own<Interactable>() || Own<PrologueHomecoming>() ||
            name.StartsWith("Story") || name.Contains("Moments") || name.Contains("Homecoming") || name.StartsWith("Where the joke"))
        {
            return Story;
        }

        if (name == WorldMenusRoot || Own<Terrain>() || Own<WaterSurface>() || Own<RaidMap>() || name == ScatterBrush.RootName ||
            name == "Level Props" || name == "Environment" || name == "Overgrowth" || name == "Clearing" || name.Contains("Trees") ||
            Own<ChoppableTree>() && !Has<SaveableEntity>())
        {
            return World;
        }

        return Gameplay;
    }

    private const string WorldMenusRoot = "World";

    [MenuItem("Safe Haven/Scene/Tidy Hierarchy")]
    private static void TidyMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log($"{scene.name}: {Tidy(scene)}");
    }

    /// <summary>Sorts the scene's loose top-level objects into the numbered folders (keeping their order), and a lone
    /// tree into "Hand-placed Trees" when the scene has one. Returns a summary; scenes of five objects or fewer are left.</summary>
    public static string Tidy(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        List<GameObject> loose = roots.Where(r => !IsFolder(r)).ToList();
        // Scenes laid out with these folders are re-filed by the current rules; a scene with folders of its own is kept.
        GameObject[] folders = roots.Where(IsFolder).ToArray();
        if (folders.Length > 0 && folders.All(f => Order.Contains(f.name)))
        {
            loose.AddRange(folders.SelectMany(f => f.transform.Cast<Transform>().Select(t => t.gameObject))
                                  .Where(g => CategoryOf(g) != g.transform.parent.name));
        }

        if (roots.Length <= SmallScene && !roots.Any(IsFolder))
        {
            return "small enough already, left as it is";
        }

        if (loose.Count == 0)
        {
            SortFolders(scene);
            return "already tidy";
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Tidy Hierarchy");
        var moved = new Dictionary<string, int>();
        Transform trees = TopLevel(scene).FirstOrDefault(g => g.name == "Hand-placed Trees")?.transform;
        foreach (GameObject go in loose)
        {
            string category = CategoryOf(go);
            Transform parent = category == World && trees != null && go != trees.gameObject && go.GetComponent<ChoppableTree>() != null
                ? trees
                : Folder(scene, category, true);
            Undo.SetTransformParent(go.transform, parent, "Tidy Hierarchy");
            moved[category] = moved.TryGetValue(category, out int n) ? n + 1 : 1;
        }

        // Folders in their numbered order, at the top; one emptied by re-filing goes.
        foreach (GameObject empty in scene.GetRootGameObjects().Where(r => IsFolder(r) && Order.Contains(r.name) && r.transform.childCount == 0).ToList())
        {
            Undo.DestroyObjectImmediate(empty);
        }

        SortFolders(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        return string.Join(", ", Order.Where(moved.ContainsKey).Select(c => $"{moved[c]} into {c}"));
    }

    private static void SortFolders(Scene scene)
    {
        int index = 0;
        foreach (GameObject folder in scene.GetRootGameObjects().Where(IsFolder).OrderBy(f => f.name, System.StringComparer.Ordinal).ToList())
        {
            if (folder.transform.GetSiblingIndex() != index)
            {
                Undo.RegisterFullObjectHierarchyUndo(folder, "Tidy Hierarchy");
                folder.transform.SetSiblingIndex(index);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            index++;
        }
    }
}
