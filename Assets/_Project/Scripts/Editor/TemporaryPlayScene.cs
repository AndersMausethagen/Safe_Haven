using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Plays a scene once without opening it, and makes sure it is only once: the scene set as Unity's play mode start scene
/// is cleared again as soon as Play mode ends, however it ends (the Stop button, a script, a domain reload). Without
/// this, a start scene left behind made every later press of Play start in that scene instead of the open one. Used by
/// editor scripts and agent probes (Play(scenePath)).
/// </summary>
[InitializeOnLoad]
public static class TemporaryPlayScene
{
    private const string Armed = "SafeHaven.TemporaryPlayScene.Armed";

    static TemporaryPlayScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // Recovers from an editor that was closed or reloaded while armed.
        if (SessionState.GetBool(Armed, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Clear();
        }
    }

    /// <summary>Enters Play mode in <paramref name="scenePath"/>; the open scene and its unsaved changes stay as they are.</summary>
    public static bool Play(string scenePath)
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        if (scene == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        SessionState.SetBool(Armed, true);
        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.EnterPlaymode();
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Armed, false))
        {
            Clear();
        }
    }

    private static void Clear()
    {
        SessionState.SetBool(Armed, false);
        EditorSceneManager.playModeStartScene = null;
    }
}
