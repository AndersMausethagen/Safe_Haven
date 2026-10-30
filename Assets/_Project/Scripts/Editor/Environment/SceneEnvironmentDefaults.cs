using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Weather, mist and light-shaft support for game scenes. A new scene gets it from the Safe Haven Scene template
/// (File › New Scene, <see cref="SafeHavenScenePipeline"/>); scenes made by code, such as tests and tools, stay as they
/// were made. Any scene with the weather rig then keeps its terrain mist complete and saves its fog profile beside it.
/// </summary>
[InitializeOnLoad]
public static class SceneEnvironmentDefaults
{
    static SceneEnvironmentDefaults()
    {
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.hierarchyChanged += CompleteTerrainMist;
    }

    /// <summary>Adds the weather rig, mist and light shafts to <paramref name="scene"/>, as in authored game scenes.</summary>
    public static void AddTo(Scene scene)
    {
        Scene previous = SceneManager.GetActiveScene();
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
