using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tries Make Playable on a throwaway scene (`unity command run_script --file AgentScripts/ProbePlayable.cs --entry
/// ProbePlayable.<Step>`): Create("Raid"|"SafeHaven") makes a saved scene with a bare terrain and makes it playable;
/// Report checks it (also while playing); Cleanup deletes the scene, its terrain, NavMesh and raid and reopens the menu.
/// </summary>
public static class ProbePlayable
{
    private const string Folder = "Assets/_Project/Scenes/Demo";
    private const string ScenePath = Folder + "/_PlayableTest.unity";

    public static string Create(string kind)
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var data = new TerrainData { heightmapResolution = 129 };
        data.size = new Vector3(120f, 20f, 120f);
        AssetDatabase.CreateAsset(data, Folder + "/_PlayableTest Terrain.asset");
        GameObject terrain = Terrain.CreateTerrainGameObject(data);
        terrain.transform.position = new Vector3(-60f, 0f, -60f);
        EditorSceneManager.SaveScene(scene, ScenePath);
        SceneView.lastActiveSceneView.pivot = Vector3.zero;
        var which = kind == "Raid" ? ScenePlayability.Kind.Raid : ScenePlayability.Kind.SafeHaven;
        string made = ScenePlayability.MakePlayable(scene, which);
        EditorSceneManager.SaveScene(scene);
        return made + "\n" + Report(kind);
    }

    public static string Report(string kind)
    {
        Scene scene = SceneManager.GetActiveScene();
        var which = kind == "Raid" ? ScenePlayability.Kind.Raid : ScenePlayability.Kind.SafeHaven;
        string issues = EditorApplication.isPlaying
            ? ""
            : string.Join("\n", ScenePlayability.Check(scene, which).Select(i => $"  {i.Severity}: {i.Message}"));
        Player player = Object.FindAnyObjectByType<Player>();
        return $"scene={scene.name} dirty={scene.isDirty} playing={EditorApplication.isPlaying} roots={string.Join(",", scene.GetRootGameObjects().Select(g => g.name))}\n" +
               $"player={(player != null ? player.transform.position.ToString("F1") : "none")} raid={(RaidDirector.Current != null ? (RaidDirector.Current.Session != null ? "running" : "no session") : "none")}\n" + issues;
    }

    public static string Cleanup()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity");
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
        foreach (string path in new[] { ScenePath, Folder + "/_PlayableTest Terrain.asset", Folder + "/_PlayableTest" })
        {
            AssetDatabase.DeleteAsset(path);
        }

        foreach (string guid in AssetDatabase.FindAssets("_PlayableTest t:RaidDefinition"))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        return "cleaned; build scenes: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)));
    }
}
