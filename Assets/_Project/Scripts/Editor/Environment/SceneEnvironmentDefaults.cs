using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>New scenes get the same weather, mist and light-shaft support as authored game scenes.</summary>
[InitializeOnLoad]
public static class SceneEnvironmentDefaults
{
    static SceneEnvironmentDefaults()
    {
        EditorSceneManager.newSceneCreated += OnNewScene;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.hierarchyChanged += CompleteTerrainMist;
    }

    private static void OnNewScene(Scene scene, NewSceneSetup setup, NewSceneMode mode)
    {
        // Additive scenes include tooling/test scratch scenes; do not populate those automatically.
        if (mode != NewSceneMode.Single || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try { WeatherSetup.AddToScene(WeatherSetup.EnsureAssets()); }
        finally { SceneManager.SetActiveScene(previous); }
    }

    private static void CompleteTerrainMist()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene)
                && scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<DayNightCycle>(true).Length > 0)
                && scene.GetRootGameObjects().Any(root => root.GetComponentsInChildren<FogGround>(true).Length > 0))
                AtmosphereSetup.EnsureTerrainMist(scene);
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        AtmosphereSetup.PersistSceneProfiles(scene, path);
    }
}
