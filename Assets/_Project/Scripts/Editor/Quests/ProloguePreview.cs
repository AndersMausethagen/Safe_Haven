using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Designer preview that survives domain reload without writing to the player's save slot.</summary>
[InitializeOnLoad]
public static class ProloguePreview
{
    private const string Armed = "SafeHaven.ProloguePreview.Armed";
    private const string PreviousScene = "SafeHaven.ProloguePreview.PreviousScene";
    private const string PreviousBackground = "SafeHaven.ProloguePreview.PreviousBackground";
    private const string ScenePath = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";

    static ProloguePreview()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Safe Haven/Quests/Play Prologue (Isolated Saves)")]
    public static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before starting a prologue preview.");
            return;
        }
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (scene == null)
        {
            Debug.LogError("The authored prologue scene is missing.");
            return;
        }
        SessionState.SetString(PreviousScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(PreviousBackground, PlayerSettings.runInBackground);
        SessionState.SetBool(Armed, true);
        EditorSceneManager.playModeStartScene = scene;
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (!SessionState.GetBool(Armed, false)) return;
        SaveGameService.Storage = new SaveStorage(Path.Combine(Application.temporaryCachePath, "PrologueVerification"));
        Application.runInBackground = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode && HasConflictingScenes())
        {
            Debug.LogError("The prologue cannot run alongside other scenes: duplicate players and raid setup can move the player below terrain. " +
                "Open Prologue_MistyWoods on its own, or use Safe Haven > Quests > Play Prologue (Isolated Saves).");
            EditorApplication.isPlaying = false;
            return;
        }
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Armed, false)) return;
        SaveGameService.Storage = new SaveStorage();
        PlayerSettings.runInBackground = SessionState.GetBool(PreviousBackground, false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene, ""));
        SessionState.SetBool(Armed, false);
    }

    internal static bool HasConflictingScenes()
    {
        if (EditorSceneManager.playModeStartScene != null) return false;
        Scene prologue = SceneManager.GetSceneByPath(ScenePath);
        return prologue.isLoaded && SceneManager.sceneCount > 1;
    }
}
